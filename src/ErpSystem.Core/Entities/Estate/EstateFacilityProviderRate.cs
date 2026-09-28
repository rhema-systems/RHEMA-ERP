using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Estate;

public sealed class EstateFacilityProviderRate : TenantEntity
{
    public Guid BusinessPartnerId { get; set; }
    public Guid? ContractId { get; set; }

    [Required, MaxLength(160)]
    public string ServiceName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string UnitOfMeasure { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}
