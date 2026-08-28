using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Shared;
using Microsoft.IdentityModel.Tokens;

namespace ErpSystem.Api.Services;

public interface IProcurementSupplierApplicantJwtService
{
    string Issue(SupplierApplicantSessionDto session);
}

public sealed class ProcurementSupplierApplicantJwtService :
    IProcurementSupplierApplicantJwtService
{
    private readonly IConfiguration _configuration;

    public ProcurementSupplierApplicantJwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string Issue(SupplierApplicantSessionDto session)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = Encoding.ASCII.GetBytes(
            jwtSettings["SecretKey"] ??
            throw new InvalidOperationException("JWT SecretKey not found"));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.ApplicantActorId.ToString()),
            new(ClaimTypes.Name, $"supplier-applicant:{session.RegistrationId:N}"),
            new("tenant_id", session.TenantId.ToString()),
            new("auth_provider", "ApplicantToken"),
            new(ClaimTypes.Role, Constants.Roles.ExternalUser),
            new("supplier_applicant_session", session.SessionReference.ToString()),
            new("supplier_applicant_registration", session.RegistrationId.ToString()),
            new("supplier_applicant_token", session.TokenId.ToString()),
            new("supplier_applicant_scope", session.PaymentOnly
                ? "payment-status"
                : "application-documents-status"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = session.ExpiresAtUtc,
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
