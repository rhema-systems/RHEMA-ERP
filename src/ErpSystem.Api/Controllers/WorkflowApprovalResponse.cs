using ErpSystem.Core.Enums;

namespace ErpSystem.Api.Controllers;

/// <summary>
/// Scalar result of a central approval action. Deliberately excludes workflow,
/// tenant and user navigation properties from the mutation response contract.
/// </summary>
public sealed class WorkflowApprovalResponse
{
    public Guid Id { get; init; }
    public Guid StepInstanceId { get; init; }
    public Guid? ApproverId { get; init; }
    public string? ApproverRole { get; init; }
    public WorkflowApprovalStatus Status { get; init; }
    public DateTime RequestedDate { get; init; }
    public DateTime? ProcessedDate { get; init; }
    public DateTime? DueDate { get; init; }
    public string? Comments { get; init; }
    public Guid TenantId { get; init; }
}
