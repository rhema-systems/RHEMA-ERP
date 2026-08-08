using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Composes the SHE dashboard counts from the existing SHE services. Queries run
/// sequentially because the underlying services share one (non-thread-safe) DbContext.
/// </summary>
public class SheDashboardService : ISheDashboardService
{
    private readonly ISafetyIncidentService _incidents;
    private readonly ISheHazardService _hazards;
    private readonly ISheRiskAssessmentService _riskAssessments;
    private readonly ISafetyInspectionService _inspections;
    private readonly IShePermitToWorkService _permits;
    private readonly ISafetyEquipmentService _equipment;
    private readonly IPpeManagementService _ppe;
    private readonly ICurrentUserProvider _currentUserProvider;

    public SheDashboardService(
        ISafetyIncidentService incidents,
        ISheHazardService hazards,
        ISheRiskAssessmentService riskAssessments,
        ISafetyInspectionService inspections,
        IShePermitToWorkService permits,
        ISafetyEquipmentService equipment,
        IPpeManagementService ppe,
        ICurrentUserProvider currentUserProvider)
    {
        _incidents = incidents;
        _hazards = hazards;
        _riskAssessments = riskAssessments;
        _inspections = inspections;
        _permits = permits;
        _equipment = equipment;
        _ppe = ppe;
        _currentUserProvider = currentUserProvider;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    public async Task<SheDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        // Ensure an authenticated tenant is present; composed SHE services apply the same scope
        // to every underlying list/count so dashboard totals never fold in other tenants' rows.
        _ = GetTenantId();

        return new SheDashboardDto
        {
            // Incidents
            OpenIncidents = (await _incidents.GetOpenAsync(cancellationToken)).Count(),
            LostTimeInjuries = (await _incidents.GetLostTimeInjuriesAsync(cancellationToken)).Count(),
            IncidentsRequiringInvestigation = (await _incidents.GetRequiringInvestigationAsync(cancellationToken)).Count(),
            OverdueCorrectiveActions = (await _incidents.GetOverdueCorrectiveActionsAsync(cancellationToken)).Count(),

            // Hazards & risk assessments
            HazardsDueForReview = (await _hazards.GetDueForReviewAsync(30, cancellationToken)).Count(),
            HighRiskHazards = (await _hazards.GetHighResidualRiskAsync(12, cancellationToken)).Count(),
            RiskAssessmentsExpiring = (await _riskAssessments.GetExpiringAsync(30, cancellationToken)).Count(),
            RiskAssessmentsDueForReview = (await _riskAssessments.GetDueForReviewAsync(30, cancellationToken)).Count(),

            // Inspections
            InspectionsDue = (await _inspections.GetDueAsync(30, cancellationToken)).Count(),
            OpenInspectionFindings = (await _inspections.GetOpenWithFindingsAsync(cancellationToken)).Count(),

            // Permits
            ActivePermits = (await _permits.GetActiveAsync(cancellationToken)).Count(),
            ExpiringPermits = (await _permits.GetExpiringAsync(1, cancellationToken)).Count(),
            SuspendedPermits = (await _permits.GetSuspendedAsync(cancellationToken)).Count(),

            // Equipment & PPE
            EquipmentDueForInspection = (await _equipment.GetDueForInspectionAsync(30, cancellationToken)).Count(),
            EquipmentExpiringCertification = (await _equipment.GetExpiringCertificationAsync(30, cancellationToken)).Count(),
            EquipmentOutOfService = (await _equipment.GetOutOfServiceAsync(cancellationToken)).Count(),
            PpeBelowReorder = (await _ppe.GetBelowReorderLevelAsync(cancellationToken)).Count(),
        };
    }
}
