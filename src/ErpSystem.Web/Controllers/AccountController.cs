using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Services;
using ErpSystem.Shared;
using ErpSystem.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace ErpSystem.Web.Controllers;

[AllowAnonymous]
[Route("api/[controller]")]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITenantService _tenantService;
    private readonly ILdapAuthenticationService _ldapService;
    private readonly IEmailService _emailService;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ITenantService tenantService,
        ILdapAuthenticationService ldapService,
        IEmailService emailService,
        AuthenticationStateProvider authenticationStateProvider,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _tenantService = tenantService;
        _ldapService = ldapService;
        _emailService = emailService;
        _authenticationStateProvider = authenticationStateProvider;
        _logger = logger;
    }

    [HttpGet]
    [Route("TestLogin")]
    public IActionResult TestLogin()
    {
        _logger.LogInformation("GET /Account/TestLogin accessed");
        return Ok("AccountController is working!");
    }

    [HttpPost]
    [Route("Login")]
    // [ValidateAntiForgeryToken] // Temporarily disabled for testing
    public async Task<IActionResult> Login([FromForm] LoginRequest request)
    {
        _logger.LogInformation("=== LOGIN ATTEMPT START ===" );
        _logger.LogInformation("Raw request received. Request object null: {IsNull}", request == null);
        _logger.LogInformation("Login attempt for user {Username} on tenant {TenantCode}", request?.Username ?? "null", request?.TenantCode ?? "null");
        
        if (request == null)
        {
            _logger.LogError("Request object is null");
            return BadRequest("Request cannot be null");
        }
        
        _logger.LogInformation("Request details - Username: '{Username}', Password: '{PasswordLength} chars', TenantCode: '{TenantCode}', RememberMe: {RememberMe}", 
            request.Username ?? "null", 
            request.Password?.Length ?? 0, 
            request.TenantCode ?? "null", 
            request.RememberMe);
        
        if (!ModelState.IsValid)
        {
            var errors = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            _logger.LogWarning("Invalid model state: {Errors}", errors);
            foreach (var modelError in ModelState)
            {
                foreach (var error in modelError.Value.Errors)
                {
                    _logger.LogWarning("Model validation error for {Field}: {Error}", modelError.Key, error.ErrorMessage);
                }
            }
            return BadRequest($"Invalid request: {errors}");
        }

        try
        {
            var tenant = await _tenantService.GetTenantByCodeAsync(request.TenantCode);
            if (tenant == null)
            {
                return BadRequest("Tenant not found");
            }

            var user = await AuthenticateUserAsync(request.Username, request.Password, tenant);
            if (user == null)
            {
                return BadRequest("Invalid credentials");
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, request.Password, request.RememberMe, false);
            if (result.Succeeded)
            {
                _logger.LogInformation("User {Username} logged in successfully", request.Username);
                
                // Notify Blazor authentication state provider of the change
                _logger.LogInformation("Notifying authentication state provider of login");
                if (_authenticationStateProvider is ErpSystem.Web.Services.CustomRevalidatingAuthenticationStateProvider customProvider)
                {
                    customProvider.ForceRefresh();
                }
                else
                {
                    _logger.LogWarning("Authentication state provider is not CustomRevalidatingAuthenticationStateProvider: {Type}", 
                        _authenticationStateProvider.GetType().Name);
                }
                
                return Redirect("/loginsuccess");
            }

            return BadRequest("Authentication failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for user {Username}", request.Username);
            return BadRequest("An error occurred during login");
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");
        return Redirect("/");
    }

    [HttpPost]
    [Route("ForgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromForm] ForgotPasswordRequest request)
    {
        _logger.LogInformation("Password reset request for {Email} on tenant {TenantCode}", request?.Email ?? "null", request?.TenantCode ?? "null");
        
        if (!ModelState.IsValid)
        {
            var errors = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            _logger.LogWarning("Invalid forgot password request: {Errors}", errors);
            return BadRequest($"Invalid request: {errors}");
        }

        try
        {
            // Get the tenant
            var tenant = await _tenantService.GetTenantByCodeAsync(request.TenantCode);
            if (tenant == null)
            {
                _logger.LogWarning("Password reset attempted for non-existent tenant: {TenantCode}", request.TenantCode);
                // Don't reveal that tenant doesn't exist - return success anyway
                return Ok(new { success = true, message = "If your email exists in our system, you will receive password reset instructions." });
            }

            // Find user by email and tenant
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || user.TenantId != tenant.Id || !user.IsActive)
            {
                _logger.LogWarning("Password reset attempted for non-existent or inactive user: {Email} on tenant {TenantCode}", request.Email, request.TenantCode);
                // Don't reveal that user doesn't exist - return success anyway for security
                return Ok(new { success = true, message = "If your email exists in our system, you will receive password reset instructions." });
            }

            // Generate password reset token
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            
            // Create reset URL
            var resetUrl = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(resetToken)}&tenant={Uri.EscapeDataString(request.TenantCode)}";
            
            // Send reset email
            var emailSent = await _emailService.SendPasswordResetEmailAsync(user, resetToken, resetUrl);
            
            if (emailSent)
            {
                _logger.LogInformation("Password reset email sent successfully to {Email}", request.Email);
            }
            else
            {
                _logger.LogError("Failed to send password reset email to {Email}", request.Email);
            }
            
            // Always return success to prevent email enumeration
            return Ok(new { success = true, message = "If your email exists in our system, you will receive password reset instructions." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for {Email}", request.Email);
            return StatusCode(500, "An error occurred processing your request. Please try again later.");
        }
    }

    [HttpPost]
    [Route("ResetPassword")]
    public async Task<IActionResult> ResetPassword([FromForm] ResetPasswordRequest request)
    {
        _logger.LogInformation("Password reset attempt for {Email} on tenant {TenantCode}", request?.Email ?? "null", request?.TenantCode ?? "null");
        
        if (!ModelState.IsValid)
        {
            var errors = string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            _logger.LogWarning("Invalid reset password request: {Errors}", errors);
            return BadRequest($"Invalid request: {errors}");
        }

        try
        {
            // Get the tenant
            var tenant = await _tenantService.GetTenantByCodeAsync(request.TenantCode);
            if (tenant == null)
            {
                _logger.LogWarning("Password reset attempted for non-existent tenant: {TenantCode}", request.TenantCode);
                return BadRequest("Invalid reset request.");
            }

            // Find user by email and tenant
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || user.TenantId != tenant.Id || !user.IsActive)
            {
                _logger.LogWarning("Password reset attempted for non-existent or inactive user: {Email} on tenant {TenantCode}", request.Email, request.TenantCode);
                return BadRequest("Invalid reset request.");
            }

            // Reset the password
            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("Password successfully reset for user {Email}", request.Email);
                
                // Clear any lockout
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
                
                // Update last login date
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = user.UserName ?? "System";
                await _userManager.UpdateAsync(user);
                
                return Ok(new { success = true, message = "Your password has been reset successfully. You can now login with your new password." });
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Password reset failed for {Email}: {Errors}", request.Email, errors);
                return BadRequest($"Password reset failed: {errors}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing password reset for {Email}", request.Email);
            return StatusCode(500, "An error occurred processing your request. Please try again later.");
        }
    }

    private async Task<ApplicationUser?> AuthenticateUserAsync(string username, string password, Tenant tenant)
    {
        // Try LDAP authentication first if enabled
        if (tenant.LdapEnabled)
        {
            var ldapResult = await _ldapService.AuthenticateAsync(username, password, tenant);
            if (ldapResult.Success && ldapResult.User != null)
            {
                // Get or create user from LDAP
                var user = await _userManager.FindByNameAsync(username);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = username,
                        Email = ldapResult.User.Email,
                        FirstName = ldapResult.User.FirstName,
                        LastName = ldapResult.User.LastName,
                        TenantId = tenant.Id,
                        AuthenticationProvider = AuthenticationProvider.LDAP,
                        LdapDn = ldapResult.User.DistinguishedName,
                        EmailConfirmed = true
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        _logger.LogError("Failed to create LDAP user: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
                        return null;
                    }

                    // Add default role
                    await _userManager.AddToRoleAsync(user, ErpSystem.Shared.Constants.Roles.Employee);
                }
                else
                {
                    // Update LDAP user info
                    user.Email = ldapResult.User.Email;
                    user.FirstName = ldapResult.User.FirstName;
                    user.LastName = ldapResult.User.LastName;
                    user.LastLoginDate = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                }

                return user;
            }
        }

        // Fallback to local authentication
        var localUser = await _userManager.FindByNameAsync(username);
        if (localUser != null && localUser.TenantId == tenant.Id)
        {
            var result = await _signInManager.CheckPasswordSignInAsync(localUser, password, false);
            if (result.Succeeded)
            {
                localUser.LastLoginDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(localUser);
                return localUser;
            }
        }

        return null;
    }

    public class LoginRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string TenantCode { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string TenantCode { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string TenantCode { get; set; } = string.Empty;
    }
}
