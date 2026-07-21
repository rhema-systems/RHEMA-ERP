using ErpSystem.Core.DTOs.HR;
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

    public SheDashboardService(
        ISafetyIncidentService incidents,
        ISheHazardService hazards,
        ISheRiskAssessmentService riskAssessments,
        ISafetyInspectionService inspections,
        IShePermitToWorkService permits,
        ISafetyEquipmentService equipment,
        IPpeManagementService ppe)
    {
        _incidents = incidents;
        _hazards = hazards;
        _riskAssessments = riskAssessments;
        _inspections = inspections;
        _permits = permits;
        _equipment = equipment;
        _ppe = ppe;
    }

    public async Task<SheDashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
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
