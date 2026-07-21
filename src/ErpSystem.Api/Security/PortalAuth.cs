using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ErpSystem.Api.Security;

/// <summary>
/// Authentication constants and token parameters for the <b>external</b> portals
/// (candidate careers portal, consultant-client portal).
///
/// <para><b>Why these are separate from internal staff tokens:</b> external portal accounts are
/// self-registered by members of the public. Previously their tokens were signed with the same key,
/// issuer and audience as internal staff tokens, so they validated on the default JWT scheme and
/// therefore satisfied every bare <c>[Authorize]</c> on the internal HR controllers — anyone who
/// registered on the careers portal could read applications and candidate PII, reject applicants and
/// approve staff requisitions.</para>
///
/// <para>Portal tokens now use a distinct signing key and audience and are validated only on the
/// <see cref="Scheme"/> scheme. An internal endpoint (default scheme) cannot validate them at all.
/// <c>AddErpSystemAuthorization</c> additionally hardens the default policy so a portal token is
/// rejected even if it ever did authenticate.</para>
/// </summary>
public static class PortalAuth
{
    /// <summary>Authentication scheme used by every external portal endpoint.</summary>
    public const string Scheme = "PortalBearer";

    /// <summary>Claim carrying the kind of principal (internal staff vs which portal).</summary>
    public const string UserTypeClaim = "user_type";

    public const string CandidateUserType = "portal_candidate";
    public const string ClientUserType    = "portal_client";

    /// <summary>Portal user types that must never be accepted on an internal endpoint.</summary>
    public static readonly string[] ExternalUserTypes = { CandidateUserType, ClientUserType };

    public static string SigningKey(IConfiguration config) =>
        config["JwtSettings:PortalSecretKey"]
        ?? throw new InvalidOperationException(
            "JwtSettings:PortalSecretKey is not configured. External portal tokens must be signed " +
            "with a key distinct from JwtSettings:SecretKey.");

    public static string Audience(IConfiguration config) =>
        config["JwtSettings:PortalAudience"]
        ?? throw new InvalidOperationException("JwtSettings:PortalAudience is not configured.");

    public static string Issuer(IConfiguration config) =>
        config["JwtSettings:Issuer"]
        ?? throw new InvalidOperationException("JwtSettings:Issuer is not configured.");

    /// <summary>
    /// The single definition of how a portal token is validated — used both by the
    /// <see cref="Scheme"/> bearer handler and by the portals' own <c>ValidateToken</c> methods,
    /// so the two can never drift apart.
    /// </summary>
    public static TokenValidationParameters TokenValidationParameters(IConfiguration config) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(SigningKey(config))),
        ValidateIssuer           = true,
        ValidIssuer              = Issuer(config),
        ValidateAudience         = true,
        ValidAudience            = Audience(config),
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero,
    };
}
