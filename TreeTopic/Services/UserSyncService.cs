using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
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
        private readonly ILogger<UserSyncService> _logger;

        public UserSyncService(
            UserManager<ApplicationUser> userManager,
            ILogger<UserSyncService> logger)
        {
            _userManager = userManager;
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

            var sub = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value;
            var name = principal.FindFirst("name")?.Value;

            if (string.IsNullOrEmpty(sub))
            {
                _logger.LogWarning("sub claim not found");
                return;
            }

            try
            {
                var user = await _userManager.FindByNameAsync(sub);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = sub,
                        Email = email,
                        DisplayName = name
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                    {
                        _logger.LogError("Failed to create user: {UserId} - {Errors}", sub,
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                        return;
                    }

                    _logger.LogInformation("User created: {UserId} ({Email})", sub, email);
                }
                else if (user.Email != email || user.DisplayName != name)
                {
                    user.Email = email;
                    user.DisplayName = name;
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("User updated: {UserId}", sub);
                }

                await EnsureOidcLoginAsync(user, sub);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing user: {UserId}", sub);
                throw;
            }
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
