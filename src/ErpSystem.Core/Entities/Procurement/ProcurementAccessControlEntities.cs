using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

public sealed class ProcurementResponsibilityAssignment : TenantEntity
{
    [Required] public Guid UserId { get; set; }
    [Required] public Guid RoleId { get; set; }
    [Required, StringLength(100)] public string RoleName { get; set; } = string.Empty;
    public ProcurementWarehouseScopeMode WarehouseScopeMode { get; set; }
    public ProcurementLocationScopeMode LocationScopeMode { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ApplicationUser User { get; set; } = null!;
    public ApplicationRole Role { get; set; } = null!;
    public ICollection<ProcurementResponsibilityWarehouse> Warehouses { get; set; } = new List<ProcurementResponsibilityWarehouse>();
    public ICollection<ProcurementResponsibilityLocation> Locations { get; set; } = new List<ProcurementResponsibilityLocation>();
    public ICollection<ProcurementCommitteeMember> CommitteeMemberships { get; set; } = new List<ProcurementCommitteeMember>();
}

public sealed class ProcurementResponsibilityWarehouse : TenantEntity
{
    [Required] public Guid AssignmentId { get; set; }
    [Required] public Guid WarehouseId { get; set; }

    public ProcurementResponsibilityAssignment Assignment { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}

public sealed class ProcurementResponsibilityLocation : TenantEntity
{
    [Required] public Guid AssignmentId { get; set; }
    [Required] public Guid WarehouseId { get; set; }
    [Required] public Guid WarehouseLocationId { get; set; }

    public ProcurementResponsibilityAssignment Assignment { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation WarehouseLocation { get; set; } = null!;
}

public sealed class ProcurementCommittee : TenantEntity
{
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public ProcurementCommitteeType CommitteeType { get; set; }
    public ProcurementCommitteeStatus Status { get; set; } = ProcurementCommitteeStatus.Draft;
    [Range(1, 50)] public int RequiredQuorum { get; set; } = 1;
    [Required, StringLength(100)] public string RequiredRoleName { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<ProcurementCommitteeMember> Members { get; set; } = new List<ProcurementCommitteeMember>();
}

public sealed class ProcurementCommitteeMember : TenantEntity
{
    [Required] public Guid CommitteeId { get; set; }
    [Required] public Guid AssignmentId { get; set; }
    public ProcurementCommitteeMemberKind MemberKind { get; set; }
    public bool IsVoting { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementCommittee Committee { get; set; } = null!;
    public ProcurementResponsibilityAssignment Assignment { get; set; } = null!;
}
