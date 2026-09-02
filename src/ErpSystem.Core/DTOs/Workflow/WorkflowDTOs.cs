using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Workflow;

/// <summary>
/// DTO for creating a new workflow definition with conditional logic support
/// </summary>
public class CreateWorkflowDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? Configuration { get; set; }
    public Guid CreatedById { get; set; }
    public List<CreateWorkflowStepDto> Steps { get; set; } = new();
    public List<CreateWorkflowTransitionDto> Transitions { get; set; } = new();
}

/// <summary>
/// DTO for updating workflow definitions
/// </summary>
public class UpdateWorkflowDefinitionDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? EntityType { get; set; }
    public string? Configuration { get; set; }
    public Guid LastModifiedById { get; set; }
    public List<CreateWorkflowStepDto>? Steps { get; set; }
    public List<CreateWorkflowTransitionDto>? Transitions { get; set; }
}

/// <summary>
/// DTO for creating workflow steps with conditional logic
/// </summary>
public class CreateWorkflowStepDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStepType StepType { get; set; }
    public int Order { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? RequiredRole { get; set; }
    public double? EstimatedHours { get; set; }
    public WorkflowStepConfigurationDto? Configuration { get; set; }
}

/// <summary>
/// DTO for creating workflow transitions with conditional logic
/// </summary>
public class CreateWorkflowTransitionDto
{
    public Guid FromStepId { get; set; }
    public Guid ToStepId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowConditionDto? Condition { get; set; }
    public bool IsDefault { get; set; } = false;
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Configuration for workflow steps with conditional logic
/// </summary>
public class WorkflowStepConfigurationDto
{
    /// <summary>
    /// Auto-assignment rules based on conditions
    /// </summary>
    public List<WorkflowAssignmentRuleDto>? AssignmentRules { get; set; }

    /// <summary>
    /// Approval configuration for approval steps
    /// </summary>
    public WorkflowApprovalConfigDto? ApprovalConfig { get; set; }

    /// <summary>
    /// Quality control configuration for quality check steps
    /// </summary>
    public WorkflowQualityConfigDto? QualityConfig { get; set; }

    /// <summary>
    /// Notification configuration
    /// </summary>
    public WorkflowNotificationConfigDto? NotificationConfig { get; set; }

    /// <summary>
    /// Manual task configuration for task/document steps
    /// </summary>
    public WorkflowTaskConfigDto? TaskConfig { get; set; }

    /// <summary>
    /// Escalation rules
    /// </summary>
    public List<WorkflowEscalationRuleDto>? EscalationRules { get; set; }

    /// <summary>
    /// Custom form fields for data collection
    /// </summary>
    public List<WorkflowFormFieldDto>? FormFields { get; set; }

    /// <summary>
    /// Skip conditions - when this step should be skipped
    /// </summary>
    public WorkflowConditionDto? SkipCondition { get; set; }
}

/// <summary>
/// Conditional logic for workflow decisions
/// </summary>
public class WorkflowConditionDto
{
    /// <summary>
    /// Type of condition (Expression, Script, Rule)
    /// </summary>
    public WorkflowConditionType ConditionType { get; set; }

    /// <summary>
    /// Condition expression or script
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Variables and their expected values
    /// </summary>
    public Dictionary<string, object>? Variables { get; set; }

    /// <summary>
    /// Logical operator for combining multiple conditions
    /// </summary>
    public WorkflowLogicalOperator LogicalOperator { get; set; } = WorkflowLogicalOperator.And;

    /// <summary>
    /// Child conditions for complex logical expressions
    /// </summary>
    public List<WorkflowConditionDto>? ChildConditions { get; set; }
}

/// <summary>
/// Assignment rules with conditional logic
/// </summary>
public class WorkflowAssignmentRuleDto
{
    /// <summary>
    /// One-based approval group used when the approval step runs sequentially.
    /// Rules in the same group are activated together.
    /// </summary>
    public int ApprovalGroup { get; set; } = 1;

    /// <summary>
    /// Condition that must be met for this assignment rule
    /// </summary>
    public WorkflowConditionDto? Condition { get; set; }

    /// <summary>
    /// Assignment type (User, Role, Dynamic)
    /// </summary>
    public WorkflowAssignmentType AssignmentType { get; set; }

    /// <summary>
    /// Target user ID (for User assignment type)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Target role (for Role assignment type)
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Dynamic assignment expression (for Dynamic assignment type)
    /// </summary>
    public string? DynamicExpression { get; set; }

    /// <summary>
    /// Priority of this assignment rule (higher number = higher priority)
    /// </summary>
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Approval step configuration with conditional logic
/// </summary>
public class WorkflowApprovalConfigDto
{
    /// <summary>
    /// Type of approval (Single, Multiple, Consensus, Majority)
    /// </summary>
    public WorkflowApprovalType ApprovalType { get; set; }

    /// <summary>
    /// Determines whether every group is available immediately or one group at a time.
    /// </summary>
    public WorkflowApprovalActivationMode ActivationMode { get; set; } = WorkflowApprovalActivationMode.Parallel;

    /// <summary>
    /// Required approvers based on conditions
    /// </summary>
    public List<WorkflowAssignmentRuleDto> ApproverRules { get; set; } = new();

    /// <summary>
    /// Minimum number of approvals required
    /// </summary>
    public int MinApprovalsRequired { get; set; } = 1;

    /// <summary>
    /// Auto-approval conditions
    /// </summary>
    public WorkflowConditionDto? AutoApprovalCondition { get; set; }

    /// <summary>
    /// Rejection handling
    /// </summary>
    public WorkflowRejectionHandling RejectionHandling { get; set; } = WorkflowRejectionHandling.StopWorkflow;

    /// <summary>
    /// Prevents the user who initiated the workflow from approving this step.
    /// </summary>
    public bool PreventInitiatorApproval { get; set; }

    /// <summary>
    /// Prevents one user from satisfying more than one approval slot in this step.
    /// </summary>
    public bool RequireDistinctApprovers { get; set; }

    /// <summary>
    /// Cross-step segregation rules evaluated before approval.
    /// </summary>
    public List<WorkflowApprovalConflictRuleDto> ConflictRules { get; set; } = new();
    public WorkflowSignaturePolicyDto? SignaturePolicy { get; set; }

    /// <summary>
    /// Controlled documents that must support the transaction before its approval workflow may
    /// complete. Keeping these requirements on the effective-dated approval policy means an audit
    /// can recover the exact evidence rule that accompanied the monetary authority route.
    /// </summary>
    public List<WorkflowEvidenceRequirementDto> EvidenceRequirements { get; set; } = new();

    /// <summary>
    /// Whether the policy permits a specifically requested evidence exception. The exception does
    /// not silently waive evidence: it forces the senior authority route and retains the request,
    /// reason, requester and final approver on the source transaction.
    /// </summary>
    public bool AllowEvidenceException { get; set; }

    public string EvidenceExceptionApproverRole { get; set; } = "Managing Director";

    /// <summary>
    /// Minimum narrative length for exceptional-payment and evidence-exception reasons. This is
    /// configuration rather than a controller constant so TDC can strengthen it without a release.
    /// </summary>
    public int MinimumExceptionReasonLength { get; set; } = 30;

    /// <summary>
    /// Marks amount/payment-type bands that require Managing Director authority even when the
    /// transaction has not been manually classified as exceptional.
    /// </summary>
    public bool RequiresManagingDirectorApproval { get; set; }

    public string ManagingDirectorApproverRole { get; set; } = "Managing Director";
}

/// <summary>
/// One named evidence requirement embedded in an effective-dated workflow approval policy.
/// Multiple current documents can satisfy the same key where a policy requires, for example,
/// both an instruction and a bank confirmation under one evidence class.
/// </summary>
public class WorkflowEvidenceRequirementDto
{
    public string RequirementKey { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public int MinimumDocuments { get; set; } = 1;
    public bool RequireVerification { get; set; } = true;
}

public class WorkflowSignaturePolicyDto
{
    public bool IsRequired { get; set; }
    public WorkflowSignatureMethod Method { get; set; } = WorkflowSignatureMethod.Attestation;
    public string? RequiredSigningRole { get; set; }
    public bool RequireValidCertificateChain { get; set; }
    public string AttestationText { get; set; } = "I confirm that I reviewed and approve this transaction.";
}

public class WorkflowSignatureSubmissionDto
{
    public WorkflowSignatureMethod Method { get; set; }
    public string Attestation { get; set; } = string.Empty;
    public string? CertificateBase64 { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
}

public class WorkflowApprovalConflictRuleDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public WorkflowApprovalActorSource ActorSource { get; set; }
    public string? SourceStepName { get; set; }
    public string? ContextField { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Quality control step configuration
/// </summary>
public class WorkflowQualityConfigDto
{
    /// <summary>
    /// Quality checks to perform
    /// </summary>
    public List<WorkflowQualityCheckDto> QualityChecks { get; set; } = new();

    /// <summary>
    /// Required inspection officer assignment rules
    /// </summary>
    public List<WorkflowAssignmentRuleDto>? InspectionOfficerRules { get; set; }

    /// <summary>
    /// Auto-pass conditions
    /// </summary>
    public WorkflowConditionDto? AutoPassCondition { get; set; }
}

/// <summary>
/// Individual quality check with conditional logic
/// </summary>
public class WorkflowQualityCheckDto
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public bool RequiresDocument { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentName { get; set; }
    public WorkflowConditionDto? ApplicabilityCondition { get; set; }
    public object? ExpectedValue { get; set; }
    public string? ValidationExpression { get; set; }
}

public class WorkflowApprovalChecklistResponseDto
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSatisfied { get; set; }
    public string? Notes { get; set; }
    public List<string> AttachmentIds { get; set; } = new();
    public Guid? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class WorkflowTaskConfigDto
{
    public string TaskActionType { get; set; } = "general";
    public string? DocumentName { get; set; }
    public bool RequiresDocument { get; set; }
    public string? DocumentRequirementKey { get; set; }
    // Stage document requirements are kept at task level so checklist items remain evidence-free controls.
    public List<WorkflowDocumentRequirementDto> DocumentRequirements { get; set; } = new();
    public string? Instructions { get; set; }
}

public class WorkflowDocumentRequirementDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RequirementKey { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public string ProvidedBy { get; set; } = "Internal";
    public string AppliesTo { get; set; } = "All";
    public bool IsRequired { get; set; } = true;
}

public class WorkflowTaskAttachmentDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? RequirementKey { get; set; }
    public string? ChecklistItemId { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentName { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedById { get; set; }
    public string? UploadedByName { get; set; }
    public Guid? DocumentOwnerId { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Version { get; set; } = 1;
    public string? ReplacesAttachmentId { get; set; }
    public WorkflowEvidenceVerificationStatus VerificationStatus { get; set; }
    public WorkflowMalwareScanStatus MalwareScanStatus { get; set; }
    public bool IsLegalHold { get; set; }
    public string? Sha256 { get; set; }
}

/// <summary>
/// Notification configuration with conditional triggers
/// </summary>
public class WorkflowNotificationConfigDto
{
    /// <summary>
    /// Notification triggers with conditions
    /// </summary>
    public List<WorkflowNotificationTriggerDto> Triggers { get; set; } = new();
}

/// <summary>
/// Notification trigger with conditional logic
/// </summary>
public class WorkflowNotificationTriggerDto
{
    public WorkflowNotificationEvent Event { get; set; }
    public WorkflowConditionDto? Condition { get; set; }
    public List<WorkflowAssignmentRuleDto> Recipients { get; set; } = new();
    public string? MessageTemplate { get; set; }
    public WorkflowNotificationChannel Channel { get; set; }
}

/// <summary>
/// Escalation rules with conditional logic
/// </summary>
public class WorkflowEscalationRuleDto
{
    /// <summary>
    /// Condition that triggers escalation
    /// </summary>
    public WorkflowConditionDto TriggerCondition { get; set; } = null!;

    /// <summary>
    /// Delay before escalation (in hours)
    /// </summary>
    public double DelayHours { get; set; }

    /// <summary>
    /// Escalation targets
    /// </summary>
    public List<WorkflowAssignmentRuleDto> EscalationTargets { get; set; } = new();

    /// <summary>
    /// Action to take on escalation
    /// </summary>
    public WorkflowEscalationAction Action { get; set; }
}

/// <summary>
/// Form field for data collection in workflow steps
/// </summary>
public class WorkflowFormFieldDto
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public WorkflowFieldType FieldType { get; set; }
    public bool IsRequired { get; set; } = false;
    public object? DefaultValue { get; set; }
    public List<string>? Options { get; set; }
    public string? ValidationRule { get; set; }
    public WorkflowConditionDto? VisibilityCondition { get; set; }
}

/// <summary>
/// Result of workflow execution
/// </summary>
public class WorkflowExecutionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public WorkflowInstanceStatus Status { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? CurrentStepId { get; set; }
    public List<WorkflowExecutionError>? Errors { get; set; }
    public object? ResultData { get; set; }
}

/// <summary>
/// Workflow execution error details
/// </summary>
public class WorkflowExecutionError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public Guid? StepId { get; set; }
}

/// <summary>
/// Workflow status and progress information
/// </summary>
public class WorkflowStatusDto
{
    public Guid WorkflowInstanceId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public WorkflowInstanceStatus Status { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? CurrentStepName { get; set; }
    public Guid? CurrentStepInstanceId { get; set; }
    public WorkflowProgressDto Progress { get; set; } = null!;
    public List<WorkflowStepStatusDto> Steps { get; set; } = new();
    public List<WorkflowApprovalStatusDto> PendingApprovals { get; set; } = new();
}

/// <summary>
/// Workflow progress information
/// </summary>
public class WorkflowProgressDto
{
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int PercentComplete { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// Individual step status
/// </summary>
public class WorkflowStepStatusDto
{
    public Guid StepInstanceId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public WorkflowStepInstanceStatus Status { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
}

/// <summary>
/// Approval status information
/// </summary>
public class WorkflowApprovalStatusDto
{
    public Guid ApprovalId { get; set; }
    public int ApprovalGroup { get; set; } = 1;
    public string StepName { get; set; } = string.Empty;
    public Guid ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
    public string? ApproverRole { get; set; }
    public WorkflowApprovalStatus Status { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsAdHoc { get; set; }
}

/// <summary>
/// Lightweight summary of the workflow state for a specific entity record (used by UI).
/// </summary>
public class WorkflowEntitySummaryDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }

    public bool HasActiveInstance { get; set; }
    /// <summary>
    /// True when this tenant/entity type has an active Published approval
    /// definition, or this record already has an active workflow instance.
    /// When false, module UIs must hide approval controls and use their
    /// authorized direct finalization action instead.
    /// </summary>
    public bool ApprovalRequired { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowName { get; set; }
    public WorkflowInstanceStatus? Status { get; set; }

    public string? CurrentStepName { get; set; }
    public Guid? CurrentStepInstanceId { get; set; }
    public WorkflowStepType? CurrentStepType { get; set; }

    public bool CanCurrentUserApprove { get; set; }
    public Guid? CurrentUserApprovalId { get; set; }
    public Guid? CurrentUserCorrectionId { get; set; }
    public bool CanCurrentUserResubmit { get; set; }
    public string? CorrectionInstructions { get; set; }
    public bool CanCurrentUserRecall { get; set; }
    public bool CanCurrentUserComplete { get; set; }
    public List<WorkflowPendingApproverDto> PendingApprovers { get; set; } = new();
    public List<WorkflowQualityCheckDto> CurrentStepChecklist { get; set; } = new();
    public WorkflowTaskConfigDto? CurrentStepTaskConfig { get; set; }
    public List<WorkflowTaskAttachmentDto> CurrentStepTaskAttachments { get; set; } = new();
    public WorkflowSignaturePolicyDto? CurrentStepSignaturePolicy { get; set; }
}

/// <summary>
/// Batch request for workflow entity summaries (used by list/grid UIs to avoid per-row calls).
/// </summary>
public class WorkflowEntitySummaryBatchRequestDto
{
    public List<WorkflowEntityRefDto> Entities { get; set; } = new();
}

/// <summary>
/// Identifies a workflow-enabled entity record.
/// </summary>
public class WorkflowEntityRefDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
}

public class WorkflowPendingApproverDto
{
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverRole { get; set; }
}

public class WorkflowEntityAuditDto
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public WorkflowInstanceStatus Status { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    public List<WorkflowStepAuditDto> Steps { get; set; } = new();
}

public class WorkflowStepAuditDto
{
    public Guid StepInstanceId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public WorkflowStepType StepType { get; set; }
    public WorkflowStepInstanceStatus Status { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public string? Comments { get; set; }
    public List<WorkflowQualityCheckDto> Checklist { get; set; } = new();
    public List<WorkflowApprovalChecklistResponseDto> ChecklistResponses { get; set; } = new();
    public WorkflowTaskConfigDto? TaskConfig { get; set; }
    public List<WorkflowTaskAttachmentDto> TaskAttachments { get; set; } = new();

    public List<WorkflowApprovalAuditDto> Approvals { get; set; } = new();
}

public class WorkflowApprovalAuditDto
{
    public Guid ApprovalId { get; set; }
    public int ApprovalGroup { get; set; } = 1;
    public bool IsAdHoc { get; set; }
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? ApproverRole { get; set; }
    public WorkflowApprovalStatus Status { get; set; }
    public DateTime RequestedDate { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string? Comments { get; set; }
    public Guid? ProcessedById { get; set; }
    public string? ProcessedByName { get; set; }
}

/// <summary>
/// Workflow validation result
/// </summary>
public class WorkflowValidationResult
{
    public bool IsValid { get; set; }
    public List<WorkflowValidationError> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Workflow validation error
/// </summary>
public class WorkflowValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? StepId { get; set; }
    public Guid? TransitionId { get; set; }
}

/// <summary>
/// Information about available workflow variables for condition expressions
/// </summary>
public class WorkflowVariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Type DataType { get; set; } = typeof(object);
    public string? Description { get; set; }
    public List<string>? PossibleValues { get; set; }
}

/// <summary>
/// DTO for workflow definition responses
/// </summary>
public class WorkflowDefinitionDto
{
    public Guid Id { get; set; }
    public Guid DefinitionKey { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string? Configuration { get; set; }
    public bool IsActive { get; set; }
    public int Version { get; set; }
    public WorkflowDefinitionLifecycleStatus LifecycleStatus { get; set; }
    public string? ChangeSummary { get; set; }
    public Guid? SupersedesDefinitionId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? RetiredAt { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public Guid? LastModifiedById { get; set; }
    public string? LastModifiedByName { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public List<WorkflowStepDto> Steps { get; set; } = new();
    public List<WorkflowTransitionDto> Transitions { get; set; } = new();
}

/// <summary>
/// DTO for workflow step responses
/// </summary>
public class WorkflowStepDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowStepType StepType { get; set; }
    public int Order { get; set; }
    public bool IsRequired { get; set; }
    public string? RequiredRole { get; set; }
    public double? EstimatedHours { get; set; }
    public WorkflowStepConfigurationDto? Configuration { get; set; }
}

/// <summary>
/// DTO for workflow transition responses
/// </summary>
public class WorkflowTransitionDto
{
    public Guid Id { get; set; }
    public Guid FromStepId { get; set; }
    public Guid ToStepId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowConditionDto? Condition { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
}

/// <summary>
/// Lightweight DTO for workflow entity types
/// </summary>
public class WorkflowEntityTypeInfoDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public string? Icon { get; set; }
    public string? ColorCode { get; set; }
    public bool IsActive { get; set; }
}

