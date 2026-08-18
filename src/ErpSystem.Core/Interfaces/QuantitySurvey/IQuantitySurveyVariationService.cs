using ErpSystem.Core.DTOs.QuantitySurvey;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyVariationService
{
    Task<QuantitySurveyVariationWorkspaceDto> GetWorkspaceAsync(Guid projectId, CancellationToken token = default);
    Task<QuantitySurveyVariationDto> GetAsync(Guid id, CancellationToken token = default);
    Task<QuantitySurveyVariationDto> SaveAsync(Guid projectId, SaveQuantitySurveyVariationRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyVariationEvidenceDto> UploadEvidenceAsync(Guid id, Guid clientRequestId, string title, string fileName, string contentType, long fileSize, Func<Stream> openRead, CancellationToken token = default);
    Task<QuantitySurveyVariationDto> SubmitAsync(Guid id, QuantitySurveyVariationActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyVariationDto> ApproveAsync(Guid id, QuantitySurveyVariationActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyVariationDto> RejectAsync(Guid id, QuantitySurveyVariationActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyVariationDto> ApplyAsync(Guid id, QuantitySurveyVariationActionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyVariationRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
    Task<bool> IsGovernedAsync(Guid? variationId, CancellationToken token = default);
}

public abstract class QuantitySurveyVariationException(string message) : Exception(message);
public sealed class QuantitySurveyVariationNotFoundException(string message) : QuantitySurveyVariationException(message);
public sealed class QuantitySurveyVariationValidationException(string message) : QuantitySurveyVariationException(message);
public sealed class QuantitySurveyVariationConflictException(string message) : QuantitySurveyVariationException(message);
