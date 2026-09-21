using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.QuantitySurvey;

public interface IQuantitySurveyContractClaimService
{
    Task<QuantitySurveyContractClaimWorkspaceDto> GetWorkspaceAsync(Guid projectId, bool external, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> GetAsync(Guid id, bool external, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> SaveExternalAsync(Guid projectId, SaveQuantitySurveyContractClaimRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> SaveInternalAsync(Guid projectId, SaveQuantitySurveyContractClaimRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimEvidenceDto> UploadEvidenceAsync(Guid id, Guid clientRequestId, string title, string fileName, string contentType, long fileSize, Func<Stream> openRead, bool external, string correlationId, CancellationToken token = default);
    Task<CentralDocumentRepositoryContent> OpenEvidenceAsync(Guid id, Guid evidenceId, bool external, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> SubmitExternalAsync(Guid id, QuantitySurveyContractClaimActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> SubmitInternalAsync(Guid id, QuantitySurveyContractClaimActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> VetAsync(Guid id, VetQuantitySurveyContractClaimRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> SubmitApprovalAsync(Guid id, QuantitySurveyContractClaimActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> DecideAsync(Guid id, QuantitySurveyContractClaimActionRequest request, bool approve, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> OpenDisputeExternalAsync(Guid id, QuantitySurveyContractClaimActionRequest request, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> ResolveDisputeAsync(Guid id, QuantitySurveyContractClaimActionRequest request, bool accepted, string correlationId, CancellationToken token = default);
    Task<QuantitySurveyContractClaimDto> SettleAsync(Guid id, SettleQuantitySurveyContractClaimRequest request, string correlationId, CancellationToken token = default);
    Task<IReadOnlyList<QuantitySurveyContractClaimRevisionDto>> HistoryAsync(Guid id, CancellationToken token = default);
}

public abstract class QuantitySurveyContractClaimException(string message) : Exception(message);
public sealed class QuantitySurveyContractClaimNotFoundException(string message) : QuantitySurveyContractClaimException(message);
public sealed class QuantitySurveyContractClaimValidationException(string message) : QuantitySurveyContractClaimException(message);
public sealed class QuantitySurveyContractClaimConflictException(string message) : QuantitySurveyContractClaimException(message);
