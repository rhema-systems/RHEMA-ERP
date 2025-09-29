using System.IdentityModel.Tokens.Jwt;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Api.Middleware;

public class JwtBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtBlacklistMiddleware> _logger;

    public JwtBlacklistMiddleware(RequestDelegate next, ILogger<JwtBlacklistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IJwtBlacklistService jwtBlacklistService)
    {
        // Only check JWT tokens for authenticated endpoints
        if (context.Request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            
            if (authHeader?.StartsWith("Bearer ") == true)
            {
                var jwt = authHeader["Bearer ".Length..];
                
                try
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var jsonToken = tokenHandler.ReadJwtToken(jwt);
                    var jti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
                    
                    if (!string.IsNullOrEmpty(jti))
                    {
                        var isBlacklisted = await jwtBlacklistService.IsTokenBlacklistedAsync(jti);
                        
                        if (isBlacklisted)
                        {
                            _logger.LogWarning("Blocked request with blacklisted JWT token (JTI: {Jti})", jti);
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync("Token has been revoked");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error validating JWT token blacklist status");
                    // Continue processing - don't block valid tokens due to validation errors
                }
            }
        }

        await _next(context);
    }
}