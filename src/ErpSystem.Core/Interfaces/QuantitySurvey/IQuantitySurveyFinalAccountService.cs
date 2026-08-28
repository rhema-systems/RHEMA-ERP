using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyFinalAccountService
{
    Task<QuantitySurveyFinalAccountWorkspaceDto> GetWorkspaceAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyFinalAccountDto> PrepareAsync(Guid projectId, PrepareQuantitySurveyFinalAccountRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyFinalAccountDto> SubmitAsync(Guid id, QuantitySurveyFinalAccountActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyFinalAccountDto> ApproveAsync(Guid id, QuantitySurveyFinalAccountActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyFinalAccountDto> RejectAsync(Guid id, QuantitySurveyFinalAccountActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<QuantitySurveyFinalAccountDto> CloseAsync(Guid id, QuantitySurveyFinalAccountActionRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuantitySurveyFinalAccountRevisionDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
