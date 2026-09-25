using ErpSystem.Core.DTOs.Procedures;
using ErpSystem.Core.DTOs.Common;

namespace ErpSystem.Core.Interfaces.Procedures;

public interface IProcedureCaseService
{
    Task<IReadOnlyList<ProcedureCaseSummaryDto>> GetCasesAsync(string? module, string? entityType, bool mineOnly);
    Task<PagedResult<ProcedureCaseSummaryDto>> GetCasesPageAsync(string? module, string? entityType, bool mineOnly, int page, int pageSize);
    Task<IReadOnlyList<ProcedureCaseSubmissionDocumentRequirementDto>> GetSubmissionDocumentRequirementsAsync(string module, string entityType);
    Task<ProcedureCaseDetailDto?> GetCaseAsync(Guid id);
    Task<ProcedureCaseDetailDto> CreateCaseAsync(CreateProcedureCaseRequest request);
    Task<ProcedureCaseDetailDto> CreateLinkedLegalMatterAsync(
        Guid sourceProcedureCaseId,
        CreateLinkedLegalMatterRequest request);
    Task<ProcedureCaseDetailDto?> UpdateFieldsAsync(Guid id, UpdateProcedureCaseFieldsRequest request);
    Task<ProcedureCaseDetailDto?> UpdateChecklistItemAsync(Guid id, Guid checklistItemId, UpdateProcedureCaseChecklistRequest request);
    Task<ProcedureCaseDetailDto?> AttachDocumentAsync(Guid id, Guid documentId, AttachProcedureCaseDocumentRequest request);
    Task<ProcedureCaseDetailDto?> LinkCentralDocumentAsync(Guid id, Guid documentId, Guid recordId);
    Task<ProcedureCaseDetailDto?> UploadDocumentAsync(Guid id, Guid documentId, Stream fileStream, string fileName, string contentType, long fileSize, string? notes);
    Task<ProcedureCaseDetailDto?> UploadCustomerIntakeDocumentAsync(Guid id, Guid documentId, Stream fileStream, string fileName, string contentType, long fileSize, string? notes);
    Task<ProcedureCaseDocumentContentDto?> GetDocumentContentAsync(Guid id, Guid documentId);
    Task<ProcedureCaseDetailDto?> SignLegalTransferExecutedDocumentAsync(Guid id, Guid documentId, SignProcedureCaseDocumentRequest request);
    Task<ProcedureCaseDetailDto?> SignPropertyAgreementAsync(Guid id, Guid documentId, SignProcedureCaseDocumentRequest request);
    Task<ProcedureCaseDetailDto?> SyncLegalTransferFeePaymentStatusAsync(Guid id);
    Task<ProcedureCaseDetailDto?> CompleteCurrentStageAsync(Guid id, CompleteProcedureCaseStageRequest request);
    Task<ProcedureCaseDetailDto?> ApplyReviewActionAsync(Guid id, ReviewProcedureCaseRequest request);
    Task SyncFromWorkflowRuntimeAsync(Guid procedureCaseId, Guid workflowInstanceId, Guid actorUserId, string? notes = null);
}
