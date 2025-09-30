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
        var requestPath = context.Request.Path;
        _logger.LogInformation("🔍 JWT Blacklist Middleware processing request: {Path}", requestPath);
        
        // Only check JWT tokens for authenticated endpoints
        if (context.Request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            _logger.LogInformation("🔍 Found Authorization header: {Header}", authHeader?.Substring(0, Math.Min(20, authHeader?.Length ?? 0)) + "...");
            
            if (authHeader?.StartsWith("Bearer ") == true)
            {
                var jwt = authHeader["Bearer ".Length..];
                _logger.LogDebug("Extracted JWT token, length: {Length}", jwt.Length);
                
                try
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var jsonToken = tokenHandler.ReadJwtToken(jwt);
                    var jti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
                    
                _logger.LogInformation("🔍 Extracted JTI from token: {Jti}", jti);
                    
                    if (!string.IsNullOrEmpty(jti))
                    {
                        _logger.LogInformation("🔍 Checking if JTI {Jti} is blacklisted...", jti);
                        var isBlacklisted = await jwtBlacklistService.IsTokenBlacklistedAsync(jti);
                        _logger.LogInformation("🔍 JTI {Jti} blacklist status: {IsBlacklisted}", jti, isBlacklisted);
                        
                        if (isBlacklisted)
                        {
                            _logger.LogWarning("🚫 BLOCKED REQUEST: Blacklisted JWT token (JTI: {Jti}) for path: {Path}", jti, requestPath);
                            context.Response.StatusCode = 401;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("{\"error\": \"Token has been revoked\", \"code\": \"TOKEN_BLACKLISTED\"}");
                            return;
                        }
                        else
                        {
                            _logger.LogInformation("✅ Token {Jti} is valid, continuing request to {Path}", jti, requestPath);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("JWT token does not contain JTI claim");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error validating JWT token blacklist status for path: {Path}", requestPath);
                    // Continue processing - don't block valid tokens due to validation errors
                }
            }
            else
            {
                _logger.LogDebug("Authorization header does not start with 'Bearer '");
            }
        }
        else
        {
            _logger.LogDebug("No Authorization header found for request: {Path}", requestPath);
        }

        await _next(context);
    }
}