using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyMaterialReconciliationService
{
    Task<QuantitySurveyMaterialReconciliationWorkspaceDto> GetWorkspaceAsync(Guid projectId, bool external = false, CancellationToken token = default);
    Task<QuantitySurveyMaterialReconciliationDto> GetAsync(Guid id, bool external = false, CancellationToken token = default);
    Task<QuantitySurveyMaterialReconciliationDto> SaveAsync(Guid projectId, SaveQuantitySurveyMaterialReconciliationRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyMaterialReconciliationDto> ConfirmAsync(Guid id, QuantitySurveyMaterialContractorConfirmationRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyMaterialReconciliationDto> SubmitAsync(Guid id, QuantitySurveyMaterialReconciliationActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyMaterialReconciliationDto> ApproveAsync(Guid id, QuantitySurveyMaterialReconciliationActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyMaterialReconciliationDto> RejectAsync(Guid id, QuantitySurveyMaterialReconciliationActionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyMaterialReconciliationRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}

public abstract class QuantitySurveyMaterialReconciliationException(string message) : Exception(message);
public sealed class QuantitySurveyMaterialReconciliationNotFoundException(string message) : QuantitySurveyMaterialReconciliationException(message);
public sealed class QuantitySurveyMaterialReconciliationValidationException(string message) : QuantitySurveyMaterialReconciliationException(message);
public sealed class QuantitySurveyMaterialReconciliationConflictException(string message) : QuantitySurveyMaterialReconciliationException(message);
