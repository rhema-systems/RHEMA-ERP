using ErpSystem.Core.DTOs.Procedures;

namespace ErpSystem.Core.Interfaces.Procedures;

public interface IProcedureCaseService
{
    Task<IReadOnlyList<ProcedureCaseSummaryDto>> GetCasesAsync(string? module, string? entityType, bool mineOnly);
    Task<ProcedureCaseDetailDto?> GetCaseAsync(Guid id);
    Task<ProcedureCaseDetailDto> CreateCaseAsync(CreateProcedureCaseRequest request);
    Task<ProcedureCaseDetailDto?> UpdateFieldsAsync(Guid id, UpdateProcedureCaseFieldsRequest request);
    Task<ProcedureCaseDetailDto?> UpdateChecklistItemAsync(Guid id, Guid checklistItemId, UpdateProcedureCaseChecklistRequest request);
    Task<ProcedureCaseDetailDto?> AttachDocumentAsync(Guid id, Guid documentId, AttachProcedureCaseDocumentRequest request);
    Task<ProcedureCaseDetailDto?> UploadDocumentAsync(Guid id, Guid documentId, Stream fileStream, string fileName, string contentType, long fileSize, string? notes);
    Task<ProcedureCaseDocumentContentDto?> GetDocumentContentAsync(Guid id, Guid documentId);
    Task<ProcedureCaseDetailDto?> CompleteCurrentStageAsync(Guid id, CompleteProcedureCaseStageRequest request);
    Task SyncFromWorkflowRuntimeAsync(Guid procedureCaseId, Guid workflowInstanceId, Guid actorUserId, string? notes = null);
}
