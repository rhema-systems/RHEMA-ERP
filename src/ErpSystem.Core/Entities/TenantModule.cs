using System.ComponentModel.DataAnnotations;
using ErpSystem.Shared;

namespace ErpSystem.Core.Entities;

public class TenantModule : BaseEntity
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(100)]
    public string ModuleName { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public ModuleStatus Status { get; set; } = ModuleStatus.Enabled;

    public DateTime? EnabledDate { get; set; }
    public DateTime? DisabledDate { get; set; }

    public string? Configuration { get; set; } // JSON configuration for module-specific settings

    // Navigation properties
    public virtual Tenant Tenant { get; set; } = null!;
}