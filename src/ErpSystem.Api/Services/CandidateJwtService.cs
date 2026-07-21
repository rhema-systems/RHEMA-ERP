using ErpSystem.Api.Security;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ErpSystem.Api.Services;

/// <summary>
/// Issues and validates JWT tokens for external candidate portal accounts.
///
/// <para>These tokens are signed with <c>JwtSettings:PortalSecretKey</c> and issued to the
/// <c>JwtSettings:PortalAudience</c> audience — both distinct from the internal staff token's key and
/// audience — and are validated only on the <see cref="PortalAuth.Scheme"/> scheme. An internal HR
/// endpoint (default scheme) therefore cannot validate a candidate token at all, so a self-registered
/// careers-portal account can never satisfy a bare <c>[Authorize]</c>.</para>
/// </summary>
public sealed class CandidateJwtService : ICandidateJwtService
{
    private readonly IConfiguration _config;
    // Candidate portal tokens are valid for 7 days — typical for career portals.
    private const int ExpiryDays = 7;

    public CandidateJwtService(IConfiguration config) => _config = config;

    public string GenerateToken(CandidatePortalAccount account, Guid tenantId)
    {
        var key = Encoding.ASCII.GetBytes(PortalAuth.SigningKey(_config));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Email,          account.Email),
            new("tenant_id",               tenantId.ToString()),
            new(PortalAuth.UserTypeClaim,  PortalAuth.CandidateUserType),
            new("email_verified",          account.IsEmailVerified.ToString().ToLower()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (account.JobCandidateId.HasValue)
            claims.Add(new Claim("candidate_id", account.JobCandidateId.Value.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Expires            = DateTime.UtcNow.AddDays(ExpiryDays),
            Issuer             = PortalAuth.Issuer(_config),
            Audience           = PortalAuth.Audience(_config),
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

            // Ensure this is a portal candidate token (not, say, a consultant-client token,
            // which shares the portal key/audience).
            if (principal.FindFirstValue(PortalAuth.UserTypeClaim) != PortalAuth.CandidateUserType)
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
