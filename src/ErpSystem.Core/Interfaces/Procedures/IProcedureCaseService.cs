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
    Task<ProcedureCaseDetailDto?> CompleteCurrentStageAsync(Guid id, CompleteProcedureCaseStageRequest request);
}
