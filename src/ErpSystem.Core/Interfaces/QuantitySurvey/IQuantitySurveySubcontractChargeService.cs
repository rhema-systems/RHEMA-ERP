using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveySubcontractChargeService
{
    Task<IReadOnlyList<QuantitySurveySubcontractChargeDto>> GetAsync(Guid projectId, Guid subcontractId, bool external, CancellationToken token = default);
    Task<QuantitySurveySubcontractChargeDto> SaveAsync(Guid subcontractId, SaveQuantitySurveySubcontractChargeRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractChargeEvidenceDto> UploadEvidenceAsync(Guid chargeNoticeId, Guid clientRequestId, string title, string fileName, string contentType, long fileSize, Func<Stream> openRead, bool external, string correlationId, CancellationToken token = default);
    Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid chargeNoticeId, Guid evidenceId, bool external, CancellationToken token = default);
    Task<QuantitySurveySubcontractChargeDto> IssueAsync(Guid id, QuantitySurveySubcontractActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractChargeDto> RespondAsync(Guid id, RespondQuantitySurveySubcontractChargeRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractChargeDto> SubmitAsync(Guid id, QuantitySurveySubcontractActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractChargeDto> DecideAsync(Guid id, DecideQuantitySurveySubcontractChargeRequest request, bool approve, string correlationId, CancellationToken token = default);
    Task<QuantitySurveySubcontractChargeDto> RetryCommunicationAsync(Guid id, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveySubcontractChargeRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}
