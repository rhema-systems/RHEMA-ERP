using ErpSystem.Api.Security;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ErpSystem.Api.Services;

/// <summary>
/// Issues and validates JWT tokens for external consultant-client portal accounts.
/// Signed with the portal key/audience and validated only on the <see cref="PortalAuth.Scheme"/>
/// scheme, so a client-portal token can never satisfy a bare <c>[Authorize]</c> on an internal
/// endpoint. See <see cref="PortalAuth"/>.
/// </summary>
public sealed class ConsultantClientPortalJwtService : IConsultantClientPortalJwtService
{
    private readonly IConfiguration _config;
    private const int ExpiryDays = 7;

    public ConsultantClientPortalJwtService(IConfiguration config) => _config = config;

    public string GenerateToken(ConsultantClientPortalAccount account, Guid tenantId)
    {
        var key = Encoding.ASCII.GetBytes(PortalAuth.SigningKey(_config));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Email, account.Email),
            new("tenant_id", tenantId.ToString()),
            new(PortalAuth.UserTypeClaim, PortalAuth.ClientUserType),
            new("consultant_client_id", account.ConsultantClientId.ToString()),
            new("email_verified", account.IsEmailVerified.ToString().ToLower()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (!string.IsNullOrWhiteSpace(account.ContactName))
            claims.Add(new Claim(ClaimTypes.GivenName, account.ContactName));

        if (account.ConsultantClient != null)
        {
            claims.Add(new Claim("client_name", account.ConsultantClient.ClientName));
            claims.Add(new Claim("client_code", account.ConsultantClient.ClientCode));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(ExpiryDays),
            Issuer = PortalAuth.Issuer(_config),
            Audience = PortalAuth.Audience(_config),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, PortalAuth.TokenValidationParameters(_config), out _);

            if (principal.FindFirstValue(PortalAuth.UserTypeClaim) != PortalAuth.ClientUserType)
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
