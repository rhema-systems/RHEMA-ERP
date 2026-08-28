using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveySubcontractService
{
    Task<QuantitySurveySubcontractWorkspaceDto> GetWorkspaceAsync(Guid projectId, bool external, CancellationToken token = default);
    Task<QuantitySurveySubcontractDto> SaveAsync(Guid projectId, SaveQuantitySurveySubcontractRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractDto> SubmitSubcontractAsync(Guid id, QuantitySurveySubcontractActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractDto> DecideSubcontractAsync(Guid id, QuantitySurveySubcontractActionRequest request, bool approve, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractValuationDto> SaveValuationAsync(Guid subcontractId, SaveQuantitySurveySubcontractValuationRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractEvidenceDto> UploadEvidenceAsync(Guid subcontractId, Guid? valuationId, Guid clientRequestId, string title, string fileName, string contentType, long fileSize, Func<Stream> openRead, bool external, string correlationId, CancellationToken token = default);
    Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid subcontractId, Guid evidenceId, bool external, CancellationToken token = default);
    Task<QuantitySurveySubcontractValuationDto> SubmitValuationAsync(Guid id, QuantitySurveySubcontractActionRequest request, bool external, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractValuationDto> AssessValuationAsync(Guid id, AssessQuantitySurveySubcontractValuationRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractValuationDto> DecideValuationAsync(Guid id, QuantitySurveySubcontractActionRequest request, bool approve, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractValuationDto> HandoffToApAsync(Guid id, QuantitySurveySubcontractActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractValuationDto> RefreshPaymentAsync(Guid id, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractDto> CloseAsync(Guid id, QuantitySurveySubcontractActionRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveySubcontractRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}

public class QuantitySurveySubcontractException(string message) : Exception(message);
public sealed class QuantitySurveySubcontractNotFoundException(string message) : QuantitySurveySubcontractException(message);
public sealed class QuantitySurveySubcontractValidationException(string message) : QuantitySurveySubcontractException(message);
public sealed class QuantitySurveySubcontractConflictException(string message) : QuantitySurveySubcontractException(message);
