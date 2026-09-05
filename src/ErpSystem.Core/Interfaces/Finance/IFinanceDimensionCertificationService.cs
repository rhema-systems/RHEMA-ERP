using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinanceDimensionCertificationService
{
    Task<IReadOnlyList<FinanceDimensionRouteCertificationDto>> GetRoutesAsync(
        CancellationToken cancellationToken = default);

    Task<FinanceDimensionReadinessAssessmentDto> AssessReadinessAsync(
        FinanceDimensionRouteId routeId,
        FinanceDimensionCertificationState targetState,
        CancellationToken cancellationToken = default);

    Task<FinanceDimensionReadinessAssessmentDto> GetAssessmentAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);

    Task<FinanceDimensionRouteCertificationDto> PromoteAsync(
        FinanceDimensionRouteId routeId,
        PromoteFinanceDimensionRouteDto request,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportReadinessCsvAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Source-specific readiness providers are added by the owning route implementation (PR 2 and
/// later adapter certifications).  The shared registry fails promotion closed when a required
/// provider is absent.
/// </summary>
public interface IFinanceDimensionReadinessProvider
{
    FinanceDimensionRouteId RouteId { get; }

    Task<FinanceDimensionReadinessContribution> EvaluateAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken = default);
}

public sealed record FinanceDimensionReadinessContribution(
    string DataVersionWatermark,
    IReadOnlyList<FinanceDimensionReadinessBlockerDto> Blockers);
