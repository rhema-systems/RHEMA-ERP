using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Inventory;

public class PhysicalCountCounter : TenantEntity
{
    public Guid PhysicalCountId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid UserId { get; set; }
    [MaxLength(50)] public string EmployeeNumber { get; set; } = string.Empty;
    [MaxLength(250)] public string EmployeeName { get; set; } = string.Empty;
    [MaxLength(256)] public string? EmailAddress { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid AssignedById { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public Guid? RemovedById { get; set; }
    public DateTime? RemovedAtUtc { get; set; }
    [MaxLength(1000)] public string? ChangeReason { get; set; }
    public Guid? InAppNotificationId { get; set; }
    public Guid? EmailNotificationId { get; set; }
    public virtual PhysicalCount PhysicalCount { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;
}
