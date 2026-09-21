namespace ErpSystem.Core.DTOs.Procedures;

public sealed record ProcedureCaseSummaryDto(
    Guid Id,
    string Module,
    string EntityType,
    string Title,
    string? ReferenceNumber,
    string? ApplicantName,
    string Status,
    int CurrentStageIndex,
    string CurrentStageName,
    string? CurrentAssignedRole,
    bool UsesConfiguredWorkflow,
    Guid? WorkflowInstanceId,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public Guid? OrganizationLevelId { get; init; }
    public string? OrganizationLevelName { get; init; }
    public Guid? OrganizationUnitId { get; init; }
    public string? OrganizationUnitName { get; init; }
}

public sealed record ProcedureCaseDetailDto(
    Guid Id,
    string Module,
    string EntityType,
    string Title,
    string? ReferenceNumber,
    string? ApplicantName,
    string? SourceDepartment,
    DateTime? ReceivedDate,
    string? Description,
    string Status,
    int CurrentStageIndex,
    string CurrentStageName,
    string? CurrentStageOwner,
    string? CurrentAssignedRole,
    bool UsesConfiguredWorkflow,
    Guid? WorkflowInstanceId,
    bool CanEditCurrentStage,
    IReadOnlyList<string> CurrentStageFieldKeys,
    IReadOnlyList<ProcedureCaseFieldDto> Fields,
    IReadOnlyList<ProcedureCaseChecklistItemDto> ChecklistItems,
    IReadOnlyList<ProcedureCaseDocumentDto> Documents,
    IReadOnlyList<ProcedureCaseActivityDto> Activities)
{
    public Guid? OrganizationLevelId { get; init; }
    public string? OrganizationLevelName { get; init; }
    public Guid? OrganizationUnitId { get; init; }
    public string? OrganizationUnitName { get; init; }
}

public sealed record ProcedureCaseFieldDto(
    Guid Id,
    string Key,
    string Label,
    string FieldType,
    string? Value,
    IReadOnlyList<string>? Options);

public sealed record ProcedureCaseChecklistItemDto(
    Guid Id,
    int StageIndex,
    string StageName,
    string Text,
    bool IsCompleted,
    Guid? CompletedById,
    DateTime? CompletedAt);

public sealed record ProcedureCaseDocumentDto(
    Guid Id,
    string Name,
    string? RequiredFrom,
    string ProvidedBy,
    bool IsMandatory,
    string? FileName,
    string? FileUrl,
    Guid? CentralDocumentRecordId,
    Guid? CentralDocumentVersionId,
    string? CentralDocumentVersion,
    string? CentralDocumentRepositoryPath,
    string? CentralDocumentRenditionPath,
    string? CentralDocumentContentType,
    string? CentralDocumentAnnotationStateJson,
    bool CanUploadAtCurrentStage,
    string? Notes,
    Guid? UploadedById,
    DateTime? UploadedAt);

public sealed record ProcedureCaseSubmissionDocumentRequirementDto(
    string Name,
    string? DocumentType,
    string AppliesTo,
    bool IsMandatory);

public sealed record ProcedureCaseActivityDto(
    Guid Id,
    string Action,
    string? StageName,
    string? Details,
    Guid PerformedById,
    DateTime PerformedAt);

public sealed record ProcedureCaseDocumentContentDto(
    Stream FileStream,
    string FileName,
    string ContentType);

public sealed record CreateProcedureCaseRequest(
    string Module,
    string EntityType,
    string? Title,
    string? ReferenceNumber,
    string? ApplicantName,
    string? SourceDepartment,
    DateTime? ReceivedDate,
    string? Description,
    IDictionary<string, string?>? FieldValues)
{
    public Guid? OrganizationLevelId { get; init; }
    public Guid? OrganizationUnitId { get; init; }
}

public sealed record CreateLinkedLegalMatterRequest(
    string MatterType,
    string? Description);

public sealed record UpdateProcedureCaseFieldsRequest(
    IDictionary<string, string?> FieldValues,
    string? ReferenceNumber,
    string? ApplicantName,
    string? SourceDepartment,
    DateTime? ReceivedDate,
    string? Description)
{
    public Guid? OrganizationLevelId { get; init; }
    public Guid? OrganizationUnitId { get; init; }
}

public sealed record UpdateProcedureCaseChecklistRequest(bool IsCompleted);

public sealed record AttachProcedureCaseDocumentRequest(
    string? FileName,
    string? FileUrl,
    string? Notes);

public sealed record SignProcedureCaseDocumentRequest(
    string? SignatureRole,
    string? Notes);

public sealed record CompleteProcedureCaseStageRequest(string? Notes);

public sealed record ReviewProcedureCaseRequest(string Action, string Reason);
