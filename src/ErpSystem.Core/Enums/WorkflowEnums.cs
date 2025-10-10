namespace ErpSystem.Core.Enums;

/// <summary>
/// Types of workflow steps
/// </summary>
public enum WorkflowStepType
{
    /// <summary>
    /// Manual task that requires user interaction
    /// </summary>
    Manual = 0,
    
    /// <summary>
    /// Automatic system task
    /// </summary>
    Automatic = 1,
    
    /// <summary>
    /// Approval step that requires user approval/rejection
    /// </summary>
    Approval = 2,
    
    /// <summary>
    /// Decision point that evaluates conditions to determine next steps
    /// </summary>
    Decision = 3,
    
    /// <summary>
    /// Script execution step
    /// </summary>
    Script = 4,
    
    /// <summary>
    /// Notification step
    /// </summary>
    Notification = 5,
    
    /// <summary>
    /// Sub-workflow step
    /// </summary>
    SubWorkflow = 6,
    
    /// <summary>
    /// Validation step that checks business rules
    /// </summary>
    Validation = 7,
    
    /// <summary>
    /// Quality control step that requires quality inspection
    /// </summary>
    QualityControl = 8
}

/// <summary>
/// Status of workflow instances
/// </summary>
public enum WorkflowInstanceStatus
{
    /// <summary>
    /// Workflow instance has been created but not started
    /// </summary>
    Created = 0,
    
    /// <summary>
    /// Workflow is currently being executed
    /// </summary>
    InProgress = 1,
    
    /// <summary>
    /// Workflow has completed successfully
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// Workflow was cancelled before completion
    /// </summary>
    Cancelled = 3,
    
    /// <summary>
    /// Workflow failed due to an error
    /// </summary>
    Failed = 4,
    
    /// <summary>
    /// Workflow is paused/suspended
    /// </summary>
    Suspended = 5,
    
    /// <summary>
    /// Workflow is waiting for external input
    /// </summary>
    Waiting = 6
}

/// <summary>
/// Status of individual workflow step instances
/// </summary>
public enum WorkflowStepInstanceStatus
{
    /// <summary>
    /// Step is waiting to be executed
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Step is currently being executed
    /// </summary>
    InProgress = 1,
    
    /// <summary>
    /// Step has been completed successfully
    /// </summary>
    Completed = 2,
    
    /// <summary>
    /// Step was cancelled
    /// </summary>
    Cancelled = 3,
    
    /// <summary>
    /// Step failed during execution
    /// </summary>
    Failed = 4
}

/// <summary>
/// Status of workflow approvals
/// </summary>
public enum WorkflowApprovalStatus
{
    /// <summary>
    /// Approval is pending decision
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Approval has been granted
    /// </summary>
    Approved = 1,
    
    /// <summary>
    /// Approval has been rejected
    /// </summary>
    Rejected = 2,
    
    /// <summary>
    /// Approval has been delegated to another user
    /// </summary>
    Delegated = 3,
    
    /// <summary>
    /// Approval has expired without decision
    /// </summary>
    Expired = 4,
    
    /// <summary>
    /// More information was requested
    /// </summary>
    MoreInfoRequested = 5
}

/// <summary>
/// Types of workflow activity log entries
/// </summary>
public enum WorkflowActivityType
{
    /// <summary>
    /// Workflow instance was created
    /// </summary>
    WorkflowCreated = 0,
    
    /// <summary>
    /// Workflow instance was started
    /// </summary>
    WorkflowStarted = 1,
    
    /// <summary>
    /// Workflow instance completed
    /// </summary>
    WorkflowCompleted = 2,
    
    /// <summary>
    /// Workflow instance was cancelled
    /// </summary>
    WorkflowCancelled = 3,
    
    /// <summary>
    /// Workflow step was started
    /// </summary>
    StepStarted = 4,
    
    /// <summary>
    /// Workflow step was completed
    /// </summary>
    StepCompleted = 5,
    
    /// <summary>
    /// Workflow step was skipped
    /// </summary>
    StepSkipped = 6,
    
    /// <summary>
    /// Workflow step failed
    /// </summary>
    StepFailed = 7,
    
    /// <summary>
    /// Workflow transitioned from one step to another
    /// </summary>
    TransitionTaken = 8,
    
    /// <summary>
    /// Approval was requested
    /// </summary>
    ApprovalRequested = 9,
    
    /// <summary>
    /// Approval was granted
    /// </summary>
    ApprovalGranted = 10,
    
    /// <summary>
    /// Approval was rejected
    /// </summary>
    ApprovalRejected = 11,
    
    /// <summary>
    /// Task was assigned to a user
    /// </summary>
    TaskAssigned = 12,
    
    /// <summary>
    /// Data was updated in the workflow
    /// </summary>
    DataUpdated = 13,
    
    /// <summary>
    /// Comment was added
    /// </summary>
    CommentAdded = 14,
    
    /// <summary>
    /// Step was assigned to a user
    /// </summary>
    StepAssigned = 15,
    
    /// <summary>
    /// Step was reassigned to a different user
    /// </summary>
    StepReassigned = 16,
    
    /// <summary>
    /// Step was escalated
    /// </summary>
    StepEscalated = 17,
    
    /// <summary>
    /// Step transitioned to next step
    /// </summary>
    StepTransitioned = 18,
    
    /// <summary>
    /// Approval was processed
    /// </summary>
    ApprovalProcessed = 19,
    
    /// <summary>
    /// Approval was approved
    /// </summary>
    ApprovalApproved = 20,
    
    /// <summary>
    /// Approval was delegated
    /// </summary>
    ApprovalDelegated = 21,
    
    /// <summary>
    /// Approval was escalated
    /// </summary>
    ApprovalEscalated = 22,
    
    /// <summary>
    /// Approval required more info
    /// </summary>
    ApprovalMoreInfoRequested = 23
}

/// <summary>
/// Priority levels for workflow instances and approvals
/// </summary>
public enum WorkflowPriority
{
    /// <summary>
    /// Lowest priority
    /// </summary>
    Low = 5,
    
    /// <summary>
    /// Normal priority
    /// </summary>
    Normal = 3,
    
    /// <summary>
    /// High priority
    /// </summary>
    High = 2,
    
    /// <summary>
    /// Critical priority - requires immediate attention
    /// </summary>
    Critical = 1
}

/// <summary>
/// Actions that can be taken on workflow approvals
/// </summary>
public enum WorkflowApprovalAction
{
    /// <summary>
    /// Approve the request
    /// </summary>
    Approve = 0,
    
    /// <summary>
    /// Reject the request
    /// </summary>
    Reject = 1,
    
    /// <summary>
    /// Delegate the approval to another user
    /// </summary>
    Delegate = 2,
    
    /// <summary>
    /// Request more information
    /// </summary>
    RequestMoreInfo = 3
}

/// <summary>
/// Actions that can be taken on workflow steps
/// </summary>
public enum WorkflowStepAction
{
    /// <summary>
    /// Complete the step
    /// </summary>
    Complete = 0,
    
    /// <summary>
    /// Reject the step
    /// </summary>
    Reject = 1,
    
    /// <summary>
    /// Delegate the step to another user
    /// </summary>
    Delegate = 2,
    
    /// <summary>
    /// Request additional information
    /// </summary>
    RequestInformation = 3,
    
    /// <summary>
    /// Skip the step
    /// </summary>
    Skip = 4
}

/// <summary>
/// Types of conditions supported
/// </summary>
public enum WorkflowConditionType
{
    /// <summary>
    /// Simple property comparison
    /// </summary>
    Expression = 0,
    
    /// <summary>
    /// JavaScript-like expression
    /// </summary>
    Script = 1,
    
    /// <summary>
    /// Business rule reference
    /// </summary>
    Rule = 2,
    
    /// <summary>
    /// Always true
    /// </summary>
    Always = 3,
    
    /// <summary>
    /// Always false
    /// </summary>
    Never = 4
}

/// <summary>
/// Logical operators for combining conditions
/// </summary>
public enum WorkflowLogicalOperator
{
    /// <summary>
    /// Logical AND
    /// </summary>
    And = 0,
    
    /// <summary>
    /// Logical OR
    /// </summary>
    Or = 1,
    
    /// <summary>
    /// Logical NOT
    /// </summary>
    Not = 2
}

/// <summary>
/// Assignment types for workflow steps
/// </summary>
public enum WorkflowAssignmentType
{
    /// <summary>
    /// Specific user
    /// </summary>
    User = 0,
    
    /// <summary>
    /// Users with specific role
    /// </summary>
    Role = 1,
    
    /// <summary>
    /// Determined at runtime
    /// </summary>
    Dynamic = 2,
    
    /// <summary>
    /// Manager of the person who started the workflow
    /// </summary>
    RequestorManager = 3,
    
    /// <summary>
    /// User who completed the previous step
    /// </summary>
    PreviousStepUser = 4
}

/// <summary>
/// Approval types
/// </summary>
public enum WorkflowApprovalType
{
    /// <summary>
    /// Any one approver
    /// </summary>
    Single = 0,
    
    /// <summary>
    /// All specified approvers
    /// </summary>
    Multiple = 1,
    
    /// <summary>
    /// Unanimous approval
    /// </summary>
    Consensus = 2,
    
    /// <summary>
    /// Majority approval
    /// </summary>
    Majority = 3
}

/// <summary>
/// Rejection handling strategies
/// </summary>
public enum WorkflowRejectionHandling
{
    /// <summary>
    /// Stop the workflow
    /// </summary>
    StopWorkflow = 0,
    
    /// <summary>
    /// Return to the previous step
    /// </summary>
    ReturnToPreviousStep = 1,
    
    /// <summary>
    /// Return to the start
    /// </summary>
    ReturnToStart = 2,
    
    /// <summary>
    /// Continue to the next step
    /// </summary>
    ContinueToNextStep = 3
}

/// <summary>
/// Notification events
/// </summary>
public enum WorkflowNotificationEvent
{
    /// <summary>
    /// Step was assigned
    /// </summary>
    StepAssigned = 0,
    
    /// <summary>
    /// Step was started
    /// </summary>
    StepStarted = 6,
    
    /// <summary>
    /// Step was completed
    /// </summary>
    StepCompleted = 1,
    
    /// <summary>
    /// Approval was requested
    /// </summary>
    ApprovalRequested = 2,
    
    /// <summary>
    /// Workflow was completed
    /// </summary>
    WorkflowCompleted = 3,
    
    /// <summary>
    /// Escalation occurred
    /// </summary>
    Escalation = 4,
    
    /// <summary>
    /// Step is overdue
    /// </summary>
    Overdue = 5
}

/// <summary>
/// Notification channels
/// </summary>
public enum WorkflowNotificationChannel
{
    /// <summary>
    /// Email notification
    /// </summary>
    Email = 0,
    
    /// <summary>
    /// In-app notification
    /// </summary>
    InApp = 1,
    
    /// <summary>
    /// SMS notification
    /// </summary>
    SMS = 2,
    
    /// <summary>
    /// Microsoft Teams notification
    /// </summary>
    Teams = 3
}

/// <summary>
/// Escalation actions
/// </summary>
public enum WorkflowEscalationAction
{
    /// <summary>
    /// Send notification only
    /// </summary>
    Notify = 0,
    
    /// <summary>
    /// Reassign the task
    /// </summary>
    Reassign = 1,
    
    /// <summary>
    /// Auto-approve the request
    /// </summary>
    AutoApprove = 2,
    
    /// <summary>
    /// Cancel the workflow
    /// </summary>
    Cancel = 3,
    
    /// <summary>
    /// Notify manager about the escalation
    /// </summary>
    NotifyManager = 4
}

/// <summary>
/// Form field types
/// </summary>
public enum WorkflowFieldType
{
    /// <summary>
    /// Text field
    /// </summary>
    Text = 0,
    
    /// <summary>
    /// Number field
    /// </summary>
    Number = 1,
    
    /// <summary>
    /// Date field
    /// </summary>
    Date = 2,
    
    /// <summary>
    /// Boolean/checkbox field
    /// </summary>
    Boolean = 3,
    
    /// <summary>
    /// Single select dropdown
    /// </summary>
    Select = 4,
    
    /// <summary>
    /// Multi-select dropdown
    /// </summary>
    MultiSelect = 5,
    
    /// <summary>
    /// Text area field
    /// </summary>
    TextArea = 6,
    
    /// <summary>
    /// File upload field
    /// </summary>
    File = 7
}
