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
        if (context.Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues value))
        {
            var authHeader = value.FirstOrDefault();
            _logger.LogInformation("🔍 Found Authorization header: {Header}", authHeader?.Substring(0, Math.Min(20, authHeader?.Length ?? 0)) + "...");

            if (authHeader?.StartsWith("Bearer ") == true)
            {
                var jwt = authHeader["Bearer ".Length..].Trim();
                _logger.LogDebug("Extracted JWT token, length: {Length}", jwt.Length);

                // Validate JWT format before processing
                if (string.IsNullOrWhiteSpace(jwt))
                {
                    _logger.LogWarning("JWT token is empty or whitespace");
                }
                else if (!IsValidJwtFormat(jwt))
                {
                    _logger.LogWarning("JWT token is not in valid format (should have 3 parts separated by dots). Token: {TokenPreview}",
                        jwt.Length > 50 ? string.Concat(jwt.AsSpan(0, 50), "...") : jwt);
                }
                else
                {
                    try
                    {
                        var tokenHandler = new JwtSecurityTokenHandler();

                        // First check if the token can be read without validation
                        if (!tokenHandler.CanReadToken(jwt))
                        {
                            _logger.LogWarning("JWT token cannot be read by token handler. Token preview: {TokenPreview}",
                                jwt.Length > 50 ? string.Concat(jwt.AsSpan(0, 50), "...") : jwt);
                        }
                        else
                        {
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
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading JWT token for blacklist validation. Path: {Path}, Token preview: {TokenPreview}",
                            requestPath, jwt.Length > 50 ? string.Concat(jwt.AsSpan(0, 50), "...") : jwt);
                        // Continue processing - don't block requests due to malformed tokens that might be handled elsewhere
                    }
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

    /// <summary>
    /// Validates that a JWT token has the correct format (3 parts separated by dots)
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>True if the token has valid format, false otherwise</returns>
    private static bool IsValidJwtFormat(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');

        // JWT should have exactly 3 parts: header.payload.signature
        if (parts.Length != 3)
        {
            return false;
        }

        // Each part should not be empty
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                return false;
            }
        }

        return true;
    }
}
