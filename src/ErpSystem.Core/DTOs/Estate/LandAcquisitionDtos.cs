namespace ErpSystem.Core.DTOs.Estate;

public record LandAcquisitionBoardDto(IReadOnlyList<LandAcquisitionStageDto> Stages);

public record LandAcquisitionStageDto(
    int Id,
    int Order,
    string Title,
    string Description,
    string WorkspaceKind,
    string DemoUiRoute,
    string Method,
    string RequiredRole,
    int Count,
    string PrimaryAction,
    string? RejectAction,
    IReadOnlyList<LandAcquisitionItemDto> Items);

public record LandAcquisitionItemDto(
    Guid Id,
    string ProjectReference,
    string Location,
    string Status,
    int StageOrder,
    string CurrentStage,
    string IntendedUse,
    string EstimatedSize,
    string? OwnerName,
    string? AcquisitionType,
    int Documents,
    int Notes,
    string LastActivity,
    string? ValueEstimate,
    string? RiskLevel,
    bool StageInputsComplete,
    IReadOnlyList<string> MissingInputs);

public class LandAcquisitionWorkspaceRequest
{
    public Guid? AcquisitionId { get; set; }
    public int ProcedureId { get; set; }
    public string WorkspaceKind { get; set; } = string.Empty;
    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class LandAcquisitionWorkspaceResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid AcquisitionId { get; set; }
    public LandAcquisitionItemDto? Item { get; set; }
    public bool StageInputsComplete { get; set; }
    public IReadOnlyList<string> MissingInputs { get; set; } = Array.Empty<string>();
}

public class LandAcquisitionWorkspaceDataResponse
{
    public Guid AcquisitionId { get; set; }
    public int ProcedureId { get; set; }
    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool StageInputsComplete { get; set; }
    public IReadOnlyList<string> MissingInputs { get; set; } = Array.Empty<string>();
}

public class LandAcquisitionSummaryResponse
{
    public Guid AcquisitionId { get; set; }
    public IReadOnlyList<LandAcquisitionStageSummaryDto> Stages { get; set; } = Array.Empty<LandAcquisitionStageSummaryDto>();
}

public class LandAcquisitionStageSummaryDto
{
    public int ProcedureId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class LandAcquisitionDocumentDto
{
    public Guid Id { get; set; }
    public Guid LandAcquisitionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentName { get; set; }
    public int ProcedureId { get; set; }
    public string Procedure { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
}

public class LandAcquisitionWorkflowActionRequest
{
    public Guid AcquisitionId { get; set; }
    public int Procedure { get; set; }
    public string ActionType { get; set; } = "primary";
    public string? Comments { get; set; }
}

public class LandAcquisitionWorkflowActionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? WorkflowOutcome { get; set; }
    public LandAcquisitionItemDto? Item { get; set; }
}

public class LandAcquisitionWorkflowTaskCompletionRequest
{
    public Guid StepInstanceId { get; set; }
    public string? Comments { get; set; }
}
