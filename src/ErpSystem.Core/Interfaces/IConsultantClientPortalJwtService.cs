using ErpSystem.Core.Entities.HR;
using System.Security.Claims;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Generates and validates JWT tokens scoped exclusively to the consultant client portal.
/// </summary>
public interface IConsultantClientPortalJwtService
{
    string GenerateToken(ConsultantClientPortalAccount account, Guid tenantId);

    ClaimsPrincipal? ValidateToken(string token);
}
