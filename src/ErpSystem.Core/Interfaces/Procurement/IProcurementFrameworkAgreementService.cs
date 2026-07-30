using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.DocumentManagement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementFrameworkAgreementService
{
    Task<ProcurementFrameworkAgreementSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementPageDto> SearchAsync(ProcurementFrameworkAgreementSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementFrameworkWorkflowOptionDto>> GetWorkflowOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementFrameworkCategoryOptionDto>> GetCategoryOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementFrameworkItemOptionDto>> GetItemOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementFrameworkSourceOptionDto>> GetSourceOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDto> CreateAsync(CreateProcurementFrameworkAgreementRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDto> UpdateAsync(Guid id, UpdateProcurementFrameworkAgreementRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDto> CloneAsync(Guid id, CloneProcurementFrameworkAgreementRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDto> SubmitAsync(Guid id, ProcurementFrameworkAgreementLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDto> ApproveAsync(Guid id, ProcurementFrameworkAgreementLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDto> RejectAsync(Guid id, ProcurementFrameworkAgreementLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDto> TerminateAsync(Guid id, ProcurementFrameworkAgreementLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDocumentDto> AddDocumentAsync(Guid id, AddProcurementFrameworkAgreementDocumentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementDocumentDto> RetireDocumentAsync(Guid id, Guid documentId, string correlationId, CancellationToken cancellationToken = default);
    Task<CentralDocumentRepositoryContent?> OpenDocumentAsync(Guid id, Guid documentId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementExtensionDto> RequestExtensionAsync(Guid id, RequestProcurementFrameworkAgreementExtension request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementFrameworkAgreementExtensionDto> DecideExtensionAsync(Guid id, Guid extensionId, DecideProcurementFrameworkAgreementExtension request, string correlationId, CancellationToken cancellationToken = default);
    Task<int> ProcessLifecycleAsync(Guid? tenantId = null, DateTime? atUtc = null, CancellationToken cancellationToken = default);
}

public sealed class ProcurementFrameworkAgreementNotFoundException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementFrameworkAgreementValidationException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementFrameworkAgreementConflictException(string code, string message) :
    Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementFrameworkAgreementAuthorizationException(string message) :
    Exception(message);
