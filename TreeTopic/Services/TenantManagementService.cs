using Microsoft.EntityFrameworkCore;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Models.OpenIdConnect;
using System.Security.Cryptography;

namespace TreeTopic.Services;

/// <summary>
/// テナント管理サービス
/// テナント登録、初期化、マイグレーション実行を管理
/// </summary>
public class TenantManagementService
{
    private readonly TenantCatalogDbContext _tenantDb;
    private readonly TenantIdObfuscationService _obfuscationService;
    private readonly MigrationService _migrationService;
    private readonly EncryptionService _encryptionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantManagementService> _logger;

    public TenantManagementService(
        TenantCatalogDbContext tenantDb,
        TenantIdObfuscationService obfuscationService,
        MigrationService migrationService,
        EncryptionService encryptionService,
        IConfiguration configuration,
        ILogger<TenantManagementService> logger)
    {
        _tenantDb = tenantDb;
        _obfuscationService = obfuscationService;
        _migrationService = migrationService;
        _encryptionService = encryptionService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 新しいテナントを登録
    /// </summary>
    public async Task<CreateTenantResponse> CreateTenantAsync(CreateTenantRequest request)
    {
        if (string.IsNullOrEmpty(request.Identifier))
        {
            throw new ArgumentException("Identifier is required", nameof(request.Identifier));
        }

        if (string.IsNullOrEmpty(request.Name))
        {
            throw new ArgumentException("Name is required", nameof(request.Name));
        }

        // 同じ Identifier が既に存在するか確認
        var existingTenant = await _tenantDb.Tenants
            .FirstOrDefaultAsync(t => t.Identifier == request.Identifier);

        if (existingTenant != null)
        {
            throw new InvalidOperationException($"Tenant with identifier '{request.Identifier}' already exists");
        }

        try
        {
            // 暗号化キーを生成
            var (k0, k1) = _obfuscationService.GenerateNewKey();

            // DbProvider を小文字に正規化（postgresql → postgres）
            var dbProvider = (request.DbProvider?.ToLower() ?? "postgres").Replace("postgresql", "postgres");

            // ConnectionString の取得（未指定の場合は SharedApp を使用）
            var connectionString = request.ConnectionString
                ?? _configuration.GetConnectionString("SharedApp")
                ?? throw new InvalidOperationException("No connection string configured for SharedApp");

            // テナント用暗号化キーを生成（AES-256 キー: 32 bytes → Base64 で 44文字）
            var tenantKeyBytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(tenantKeyBytes);
            }
            var tenantKeyString = Convert.ToBase64String(tenantKeyBytes);

            // テナント用キーをマスターキーで暗号化
            var encryptedTenantKey = _encryptionService.Encrypt(tenantKeyString);

            // テナントキーで Connection String を暗号化
            // 新しい EncryptionService インスタンスをテナントキーで初期化（内部で実装）
            var tenantEncryptionService = new EncryptionService(tenantKeyString, _logger);
            var encryptedConnectionString = tenantEncryptionService.Encrypt(connectionString);

            // OpenIdConnect ClientSecret を テナントキーで暗号化
            var encryptedClientSecret = !string.IsNullOrEmpty(request.OpenIdConnectClientSecret)
                ? tenantEncryptionService.Encrypt(request.OpenIdConnectClientSecret)
                : null;

            // OpenID Connect メタデータから エンドポイント情報を取得
            string? authority = null;
            string? authorizationEndpoint = null;
            string? tokenEndpoint = null;
            string? jwksUri = null;
            string? endSessionEndpoint = null;

            if (!string.IsNullOrEmpty(request.OpenIdConnectMetadataAddress))
            {
                try
                {
                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        var metadata = await httpClient.GetStringAsync(request.OpenIdConnectMetadataAddress);
                        var oidcMetadata = System.Text.Json.JsonSerializer.Deserialize<OpenIdConnectMetadata>(metadata);

                        if (oidcMetadata != null)
                        {
                            authority = oidcMetadata.Issuer;
                            authorizationEndpoint = oidcMetadata.AuthorizationEndpoint;
                            tokenEndpoint = oidcMetadata.TokenEndpoint;
                            jwksUri = oidcMetadata.JwksUri;
                            endSessionEndpoint = oidcMetadata.EndSessionEndpoint;
                        }
                    }
                    _logger.LogInformation("OIDC metadata retrieved for tenant: {TenantIdentifier}", request.Identifier);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve OIDC metadata for tenant: {TenantIdentifier}", request.Identifier);
                    throw new InvalidOperationException($"Failed to retrieve OIDC metadata from {request.OpenIdConnectMetadataAddress}", ex);
                }
            }

            // テナント情報を作成
            var tenant = new ApplicationTenantInfo
            {
                Id = Guid.NewGuid().ToString(),
                Identifier = request.Identifier,
                Name = request.Name,
                DbProvider = dbProvider,
                TenantEncryptionKey = encryptedTenantKey,
                ConnectionString = encryptedConnectionString,
                RoleClaimName = request.RoleClaimName,
                OpenIdConnectMetadataAddress = request.OpenIdConnectMetadataAddress,
                OpenIdConnectAuthority = authority,
                OpenIdConnectAuthorizationEndpoint = authorizationEndpoint,
                OpenIdConnectTokenEndpoint = tokenEndpoint,
                OpenIdConnectJwksUri = jwksUri,
                OpenIdConnectEndSessionEndpoint = endSessionEndpoint,
                OpenIdConnecClientId = request.OpenIdConnectClientId,
                OpenIdConnecClientSecret = encryptedClientSecret,
                TenantObfuscationKeyK0 = k0,
                TenantObfuscationKeyK1 = k1
            };

            // TenantCatalog DB に保存
            _tenantDb.Tenants.Add(tenant);
            await _tenantDb.SaveChangesAsync();
            _logger.LogInformation("Tenant created: {TenantIdentifier} (ID: {TenantId})",
                request.Identifier, tenant.Id);

            // セットアップトークンを生成
            var setupToken = SetupToken.GenerateToken();
            var setupTokenRecord = new SetupToken
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                TokenHash = SetupToken.HashToken(setupToken),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1) // 1時間の有効期限
            };
            _tenantDb.SetupTokens.Add(setupTokenRecord);
            await _tenantDb.SaveChangesAsync();

            // テナント用DB のマイグレーション実行
            await MigrateTenantsDbAsync(tenant);

            // テナント情報とセットアップトークンを返す
            return new CreateTenantResponse
            {
                Tenant = tenant,
                SetupToken = setupToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tenant: {Identifier}", request.Identifier);
            throw;
        }
    }

    /// <summary>
    /// テナント用DB のマイグレーション実行
    /// </summary>
    private async Task MigrateTenantsDbAsync(ApplicationTenantInfo tenant)
    {
        try
        {
            _logger.LogInformation("Starting migration for tenant: {TenantIdentifier}", tenant.Identifier);
            await _migrationService.MigrateTenantAsync(tenant);
            _logger.LogInformation("Migration completed for tenant: {TenantIdentifier}", tenant.Identifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating tenant DB: {TenantIdentifier}", tenant.Identifier);
            throw;
        }
    }

    /// <summary>
    /// すべてのテナント情報を取得
    /// </summary>
    public async Task<List<ApplicationTenantInfo>> GetAllTenantsAsync()
    {
        return await _tenantDb.Tenants.ToListAsync();
    }

    /// <summary>
    /// テナント情報を Identifier で取得
    /// </summary>
    public async Task<ApplicationTenantInfo?> GetTenantByIdentifierAsync(string identifier)
    {
        return await _tenantDb.Tenants
            .FirstOrDefaultAsync(t => t.Identifier == identifier);
    }

    /// <summary>
    /// テナント情報を削除
    /// </summary>
    public async Task DeleteTenantAsync(string tenantId)
    {
        var tenant = await _tenantDb.Tenants.FindAsync(tenantId);
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' not found");
        }

        try
        {
            _tenantDb.Tenants.Remove(tenant);
            await _tenantDb.SaveChangesAsync();
            _logger.LogInformation("Tenant deleted: {TenantIdentifier}", tenant.Identifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tenant: {TenantId}", tenantId);
            throw;
        }
    }
}
