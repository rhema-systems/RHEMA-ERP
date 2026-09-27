using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class PhysicalCountCounterOptionDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public bool HasEmail { get; set; }
    public bool CanAssign { get; set; }
    public string? IneligibilityReason { get; set; }
}

public sealed class PhysicalCountCounterDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
    public Guid AssignedById { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public Guid? RemovedById { get; set; }
    public DateTime? RemovedAtUtc { get; set; }
    public string? ChangeReason { get; set; }
    public bool EmailNotificationQueued { get; set; }
}

public sealed class AssignPhysicalCountCountersRequest : PhysicalCountMutationRequest
{
    [Required, MinLength(1), MaxLength(100)] public List<Guid> EmployeeIds { get; set; } = new();
}
