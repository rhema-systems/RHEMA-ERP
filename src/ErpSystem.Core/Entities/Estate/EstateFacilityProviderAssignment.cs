using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Estate;

public sealed class EstateFacilityProviderAssignment : TenantEntity
{
    public Guid BusinessPartnerId { get; set; }
    public Guid EstateManagedAssetId { get; set; }
    public Guid? ContractId { get; set; }

    [Required, MaxLength(160)]
    public string ServiceScope { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? ServiceArea { get; set; }

    [Required, MaxLength(40)]
    public string AssignmentStatus { get; set; } = "Active";

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(160)]
    public string? SchedulePattern { get; set; }

    [MaxLength(200)]
    public string? SupervisorName { get; set; }

    [MaxLength(120)]
    public string? SlaReference { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
