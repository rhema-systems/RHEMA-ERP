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
/// <para>Portal tokens use a distinct signing key and audience and are validated only on the
/// <see cref="Scheme"/> scheme, so an internal endpoint (default scheme) cannot validate them at all.
/// <see cref="ValidateDistinctFromInternal"/> is enforced at startup to guarantee that separation —
/// if the portal key or audience ever equalled its internal counterpart, portal tokens would validate
/// on the default bearer scheme and could satisfy a bare internal <c>[Authorize]</c> (they do not
/// carry the ExternalUser role the access middleware checks).</para>
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
    /// Fail fast at startup if the external-portal signing key or audience is NOT distinct from the
    /// internal ones. If they matched, portal tokens would validate on the internal default bearer
    /// scheme and could satisfy internal <c>[Authorize]</c> attributes. Existence of the portal values
    /// is enforced separately by <see cref="SigningKey"/> / <see cref="Audience"/>.
    /// </summary>
    public static void ValidateDistinctFromInternal(IConfiguration config)
    {
        var internalKey = config["JwtSettings:SecretKey"];
        var portalKey   = config["JwtSettings:PortalSecretKey"];
        if (!string.IsNullOrEmpty(portalKey) && string.Equals(portalKey, internalKey, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "JwtSettings:PortalSecretKey must be different from JwtSettings:SecretKey. When they are equal, " +
                "external portal tokens validate on the internal bearer scheme and can satisfy [Authorize] on " +
                "internal controllers.");

        var internalAudience = config["JwtSettings:Audience"];
        var portalAudience   = config["JwtSettings:PortalAudience"];
        if (!string.IsNullOrEmpty(portalAudience) && string.Equals(portalAudience, internalAudience, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "JwtSettings:PortalAudience must be different from JwtSettings:Audience so external portal " +
                "tokens are never accepted on internal endpoints.");
    }

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
