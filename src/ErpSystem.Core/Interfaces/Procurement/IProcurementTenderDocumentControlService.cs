using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IProcurementTenderDocumentControlService
{
    Task<ProcurementTenderDocumentTemplateSummaryDto> GetTemplateSummaryAsync(CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplatePageDto> SearchTemplatesAsync(ProcurementTenderDocumentTemplateSearchRequest request, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> GetTemplateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementTenderDocumentWorkflowOptionDto>> GetTemplateWorkflowOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProcurementTenderDocumentPolicyOptionDto>> GetTemplatePolicyOptionsAsync(CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> CreateTemplateAsync(SaveProcurementTenderDocumentTemplateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> UpdateTemplateAsync(Guid id, SaveProcurementTenderDocumentTemplateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> AttachTemplateContentAsync(Guid id, AttachProcurementTenderDocumentTemplateContentRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> SubmitTemplateAsync(Guid id, ProcurementTenderDocumentTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> PublishTemplateAsync(Guid id, ProcurementTenderDocumentTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> RejectTemplateAsync(Guid id, ProcurementTenderDocumentTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> CloneTemplateAsync(Guid id, CloneProcurementTenderDocumentTemplateRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentTemplateDto> RetireTemplateAsync(Guid id, ProcurementTenderDocumentTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task DeleteDraftTemplateAsync(Guid id, ProcurementTenderDocumentTemplateLifecycleRequest request, string correlationId, CancellationToken cancellationToken = default);

    Task<ProcurementTenderDocumentRegisterReadinessDto> GetRegisterReadinessAsync(ProcurementTenderDocumentSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentRegisterDto> GetRegisterAsync(ProcurementTenderDocumentSourceType sourceType, Guid sourceId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentRegisterDto> BindAsync(BindProcurementTenderDocumentRegisterRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentIssuanceDto> IssueAsync(IssueProcurementTenderDocumentControlRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentChangeDto> CreateChangeAsync(CreateProcurementTenderDocumentChangeRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentChangeDto> DecideChangeAsync(Guid changeId, DecideProcurementTenderDocumentChangeRequest request, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentAcknowledgementDto> AcknowledgeAsync(AcknowledgeProcurementTenderDocumentRequest request, string correlationId, CancellationToken cancellationToken = default);

    Task<ProcurementTenderDocumentEffectiveStateDto> EnsurePublicationReadyAsync(ProcurementTenderDocumentSourceType sourceType, Guid sourceId, DateTime expectedDeadlineUtc, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentEffectiveStateDto> EnsureDispatchReadyAsync(ProcurementTenderDocumentSourceType sourceType, Guid sourceId, IReadOnlyCollection<Guid> businessPartnerIds, IReadOnlyCollection<string> externalEmails, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentEffectiveStateDto> EnsureSubmissionReadyAsync(ProcurementTenderDocumentSourceType sourceType, Guid sourceId, Guid businessPartnerId, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentEffectiveStateDto> EnsureOpeningReadyAsync(ProcurementTenderDocumentSourceType sourceType, Guid sourceId, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentEffectiveStateDto> EnsureEvaluationReadyAsync(ProcurementTenderDocumentSourceType sourceType, Guid sourceId, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentEffectiveStateDto> EnsureAwardReadyAsync(ProcurementTenderDocumentSourceType sourceType, Guid sourceId, Guid? businessPartnerId, string correlationId, CancellationToken cancellationToken = default);
    Task<ProcurementTenderDocumentIssuanceDto> IssueTenderCompatibilityAsync(Guid tenderId, IssueProcurementTenderDocumentRequest request, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class ProcurementTenderDocumentControlNotFoundException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementTenderDocumentControlConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementTenderDocumentControlValidationException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class ProcurementTenderDocumentControlAuthorizationException(string message) : UnauthorizedAccessException(message);
