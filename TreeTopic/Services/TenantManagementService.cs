using Microsoft.EntityFrameworkCore;
using MaskedUUID.AspNetCore.Types;
using TreeTopic.Dtos;
using TreeTopic.Models;
using TreeTopic.Models.OpenIdConnect;
using System.Net;
using System.Security.Cryptography;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;

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
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TenantManagementService> _logger;

    public TenantManagementService(
        TenantCatalogDbContext tenantDb,
        TenantIdObfuscationService obfuscationService,
        MigrationService migrationService,
        EncryptionService encryptionService,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<TenantManagementService> logger)
    {
        _tenantDb = tenantDb;
        _obfuscationService = obfuscationService;
        _migrationService = migrationService;
        _encryptionService = encryptionService;
        _configuration = configuration;
        _environment = environment;
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

            var metadataAddress = request.OpenIdConnectMetadataAddress;
            if (string.IsNullOrEmpty(metadataAddress) && !string.IsNullOrEmpty(request.OpenIdConnectAuthority))
            {
                metadataAddress = request.OpenIdConnectAuthority.TrimEnd('/') + "/.well-known/openid-configuration";
            }

            if (!string.IsNullOrEmpty(metadataAddress))
            {
                if (!IsAllowedMetadataAddress(metadataAddress, out var addressError))
                {
                    throw new ArgumentException($"Invalid OpenIdConnectMetadataAddress: {addressError}", nameof(request.OpenIdConnectMetadataAddress));
                }

                try
                {
                    using (var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false })
                    using (var httpClient = new System.Net.Http.HttpClient(handler))
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(10);
                        using var response = await httpClient.GetAsync(metadataAddress, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException($"Failed to retrieve OIDC metadata (HTTP {(int)response.StatusCode})");
                        }

                        var metadata = await response.Content.ReadAsStringAsync();
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
                    throw new InvalidOperationException($"Failed to retrieve OIDC metadata from {metadataAddress}", ex);
                }
            }
            if (string.IsNullOrEmpty(authority))
            {
                authority = request.OpenIdConnectAuthority;
            }

            // テナント情報を作成
            var tenantName = request.Identifier;
            var tenant = new ApplicationTenantInfo(request.Identifier, tenantName)
            {
                Identifier = request.Identifier,
                Name = tenantName,
                Detail = new ApplicationTenantDetail
                {
                    TenantId = string.Empty,
                    DbProvider = dbProvider,
                    TenantEncryptionKey = encryptedTenantKey,
                    ConnectionString = encryptedConnectionString,
                    RoleClaimName = request.RoleClaimName,
                    OpenIdConnectMetadataAddress = metadataAddress,
                    OpenIdConnectAuthority = authority,
                    OpenIdConnectAuthorizationEndpoint = authorizationEndpoint,
                    OpenIdConnectTokenEndpoint = tokenEndpoint,
                    OpenIdConnectJwksUri = jwksUri,
                    OpenIdConnectEndSessionEndpoint = endSessionEndpoint,
                    OpenIdConnectClientId = request.OpenIdConnectClientId,
                    OpenIdConnectClientSecret = encryptedClientSecret,
                    TenantObfuscationKeyK0 = k0,
                    TenantObfuscationKeyK1 = k1
                }
            };

            tenant.Detail!.TenantId = tenant.Id!;

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

    private bool IsAllowedMetadataAddress(string metadataAddress, out string? error)
    {
        error = null;

        if (!Uri.TryCreate(metadataAddress, UriKind.Absolute, out var uri))
        {
            error = "URL must be absolute.";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            error = "Query or fragment is not allowed.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Only http/https schemes are allowed.";
            return false;
        }

        if (!_environment.IsDevelopment() && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "HTTPS is required outside development.";
            return false;
        }

        if (uri.IsLoopback)
        {
            if (_environment.IsDevelopment())
            {
                return true;
            }

            error = "Loopback addresses are not allowed.";
            return false;
        }

        var host = uri.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Host is required.";
            return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            if (_environment.IsDevelopment())
            {
                return true;
            }

            error = "Localhost is not allowed.";
            return false;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsPrivateOrLocalIp(ip))
            {
                if (_environment.IsDevelopment() && IPAddress.IsLoopback(ip))
                {
                    return true;
                }

                error = "Private or local IP addresses are not allowed.";
                return false;
            }

            return true;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            if (addresses.Length == 0)
            {
                error = "Host could not be resolved.";
                return false;
            }

            if (addresses.Any(IsPrivateOrLocalIp))
            {
                if (_environment.IsDevelopment() && addresses.All(IPAddress.IsLoopback))
                {
                    return true;
                }

                error = "Host resolves to a private or local IP address.";
                return false;
            }
        }
        catch (SocketException)
        {
            error = "Host could not be resolved.";
            return false;
        }

        return true;
    }

    private static bool IsPrivateOrLocalIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) ||
                   bytes[0] == 127 ||
                   bytes[0] == 0;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
            {
                return true;
            }

            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return true; // Unique local address (fc00::/7)
            }
        }

        return false;
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
        return await _tenantDb.Tenants
            .Include(t => t.Detail)
            .ToListAsync();
    }

    /// <summary>
    /// テナント情報を Identifier で取得
    /// </summary>
    public async Task<ApplicationTenantInfo?> GetTenantByIdentifierAsync(string identifier)
    {
        return await _tenantDb.Tenants
            .Include(t => t.Detail)
            .FirstOrDefaultAsync(t => t.Identifier == identifier);
    }

    /// <summary>
    /// テナント情報を削除
    /// </summary>
    public async Task DeleteTenantAsync(MaskedGuid tenantId)
    {
        var tenant = await _tenantDb.Tenants.FindAsync(((Guid)tenantId).ToString());
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
