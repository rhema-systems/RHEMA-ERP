using ErpSystem.Core.Entities.HR.Recruitment;
using System.Security.Claims;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Generates and validates JWT tokens scoped exclusively to the external candidate portal.
/// The implementation lives in <c>ErpSystem.Api</c>; this interface is in Core
/// so that <c>CandidatePortalAuthService</c> can reference it without a circular dependency.
/// </summary>
public interface ICandidateJwtService
{
    /// <summary>
    /// Creates a signed JWT for the given <paramref name="account"/>.
    /// The token carries <c>user_type = portal_candidate</c> to prevent cross-use
    /// with internal-user tokens.
    /// </summary>
    string GenerateToken(CandidatePortalAccount account, Guid tenantId);

    /// <summary>
    /// Validates a token string and returns its <see cref="ClaimsPrincipal"/> if valid
    /// and the <c>user_type</c> claim equals <c>portal_candidate</c>.
    /// Returns <c>null</c> for any invalid or mismatched token.
    /// </summary>
    ClaimsPrincipal? ValidateToken(string token);
}
