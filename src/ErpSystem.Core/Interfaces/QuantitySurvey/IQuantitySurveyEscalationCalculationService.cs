using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyEscalationCalculationService
{
    Task<QuantitySurveyEscalationCalculationLookupsDto> GetLookupsAsync(CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationImpactTargetsDto> GetImpactTargetsAsync(Guid formulaId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationCalculationPageDto> ListAsync(QuantitySurveyEscalationCalculationListRequest request, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationCalculationDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationCalculationDto> CalculateAsync(CreateQuantitySurveyEscalationCalculationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationCalculationDto> SubmitAsync(Guid id, QuantitySurveyEscalationCalculationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationCalculationDto> ReviewAdjustmentAsync(Guid id, ReviewQuantitySurveyEscalationCalculationRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationCalculationDto> ApproveAsync(Guid id, QuantitySurveyEscalationCalculationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyEscalationCalculationDto> RejectAsync(Guid id, QuantitySurveyEscalationCalculationLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task ApplyApprovedFinalAccountImpactsAsync(Guid finalAccountId, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyEscalationCalculationRevisionDto>> HistoryAsync(Guid id, CancellationToken cancellationToken = default);
}

public abstract class QuantitySurveyEscalationCalculationException(string message) : Exception(message);
public sealed class QuantitySurveyEscalationCalculationValidationException(string message) : QuantitySurveyEscalationCalculationException(message);
public sealed class QuantitySurveyEscalationCalculationConflictException(string message) : QuantitySurveyEscalationCalculationException(message);
public sealed class QuantitySurveyEscalationCalculationNotFoundException(string message) : QuantitySurveyEscalationCalculationException(message);
