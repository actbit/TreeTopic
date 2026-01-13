using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Finbuckle.MultiTenant.Abstractions;
using TreeTopic.Models;

namespace TreeTopic.Services;

/// <summary>
/// OpenID Connect 認証時にユーザー情報をDBに同期するサービス
/// ロール情報は claim のみで管理（将来的に手動ロール割り当てに対応可能）
/// </summary>
    public class UserSyncService
    {
        private const string OidcLoginProvider = "oidc";
        private const string OidcProviderDisplay = "OpenID Connect";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IconService _iconService;
        private readonly ILogger<UserSyncService> _logger;

        public UserSyncService(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext dbContext,
            IconService iconService,
            ILogger<UserSyncService> logger)
        {
            _userManager = userManager;
            _dbContext = dbContext;
            _iconService = iconService;
            _logger = logger;
        }

        /// <summary>
        /// OpenID Connect のユーザー情報をDBに同期（ロール情報は除外）
        /// </summary>
        public async Task SyncUserAsync(ClaimsPrincipal? principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var sub = principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = principal.FindFirst("email")?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = principal.FindFirst("name")?.Value
                ?? principal.FindFirst("preferred_username")?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(sub))
            {
                _logger.LogWarning("sub claim not found");
                return;
            }

            try
            {
                var user = await FindUserBySubAsync(sub);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = name ?? sub,
                        Email = email,
                        DisplayName = name,
                        Sub = sub
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                    {
                        _logger.LogError("Failed to create user: {Sub} - {Errors}", sub,
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                        return;
                    }

                    var iconFileName = await _iconService.EnsureDefaultUserIconAsync(user);
                    if (!string.IsNullOrWhiteSpace(iconFileName))
                    {
                        user.IconFileName = iconFileName;
                        await _userManager.UpdateAsync(user);
                    }

                    _logger.LogInformation("User created: {Sub} ({Email})", sub, email);
                }
                else if (user.Email != email || user.DisplayName != name)
                {
                    user.Email = email;
                    user.DisplayName = name;
                    user.UserName = name ?? sub;

                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        _logger.LogError("Failed to update user: {Sub} - {Errors}", sub,
                            string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                    }
                    else
                    {
                        _logger.LogInformation("User updated: {Sub}", sub);
                    }
                }

                await EnsureOidcLoginAsync(user, sub);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing user: {Sub}", sub);
                // ユーザー同期エラーで認証全体を失敗させない。ログに記録して継続
            }
        }

        private async Task<ApplicationUser?> FindUserBySubAsync(string sub)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Sub == sub);
        }

        private async Task EnsureOidcLoginAsync(ApplicationUser user, string sub)
        {
            var logins = await _userManager.GetLoginsAsync(user);
            if (logins.Any(l => l.LoginProvider == OidcLoginProvider && l.ProviderKey == sub))
            {
                return;
            }

            var loginResult = await _userManager.AddLoginAsync(user, new UserLoginInfo(OidcLoginProvider, sub, OidcProviderDisplay));
            if (!loginResult.Succeeded)
            {
                _logger.LogError("Failed to add OIDC login for {UserId}: {Errors}", user.Id, string.Join(", ", loginResult.Errors.Select(e => e.Description)));
            }
        }
    }
