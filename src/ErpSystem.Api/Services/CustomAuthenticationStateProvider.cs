using System.Security.Claims;
using ErpSystem.Core.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ErpSystem.Web.Services
{
    public class CustomRevalidatingAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IdentityOptions _options;
        private readonly ILogger<CustomRevalidatingAuthenticationStateProvider> _logger;

        public CustomRevalidatingAuthenticationStateProvider(
            ILoggerFactory loggerFactory,
            IServiceScopeFactory scopeFactory,
            IOptions<IdentityOptions> optionsAccessor)
            : base(loggerFactory)
        {
            _scopeFactory = scopeFactory;
            _options = optionsAccessor.Value;
            _logger = loggerFactory.CreateLogger<CustomRevalidatingAuthenticationStateProvider>();
            _logger.LogInformation("CustomRevalidatingAuthenticationStateProvider created");
        }

        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            _logger.LogInformation("GetAuthenticationStateAsync called");

            // First get the base authentication state from the server
            var authState = await base.GetAuthenticationStateAsync();

            _logger.LogInformation("Base auth state - Authenticated: {IsAuthenticated}, Name: {Name}, Claims: {ClaimsCount}",
                authState?.User?.Identity?.IsAuthenticated,
                authState?.User?.Identity?.Name,
                authState?.User?.Claims?.Count() ?? 0);

            return authState;
        }

        /// <summary>
        /// Forces a refresh of the authentication state and notifies all listeners
        /// </summary>
        public void ForceRefresh()
        {
            _logger.LogInformation("ForceRefresh called - triggering authentication state change notification");

            // Trigger a notification that the authentication state has changed
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        protected override async Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState, CancellationToken cancellationToken)
        {
            _logger.LogInformation("ValidateAuthenticationStateAsync called. User authenticated: {IsAuthenticated}, Name: {Name}",
                authenticationState?.User?.Identity?.IsAuthenticated,
                authenticationState?.User?.Identity?.Name);

            // Get the user manager from a new scope to ensure it does not get disposed
            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var result = await ValidateSecurityStampAsync(userManager, authenticationState.User);
            _logger.LogInformation("ValidateSecurityStampAsync result: {Result}", result);
            return result;
        }

        private async Task<bool> ValidateSecurityStampAsync(UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
        {
            var user = await userManager.GetUserAsync(principal);
            if (user == null)
            {
                return false;
            }
            else if (!userManager.SupportsUserSecurityStamp)
            {
                return true;
            }
            else
            {
                var principalStamp = principal.FindFirstValue(_options.ClaimsIdentity.SecurityStampClaimType);
                var userStamp = await userManager.GetSecurityStampAsync(user);
                return principalStamp == userStamp;
            }
        }
    }
}
