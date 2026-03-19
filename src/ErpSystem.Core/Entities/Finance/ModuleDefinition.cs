using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// ERP Module Definition entity for extensible module registry.
/// Used for module-level period locking and access control.
/// </summary>
public class ModuleDefinition : BaseEntity
{
    /// <summary>
    /// Tenant Identifier
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Unique module code/identifier.
    /// Examples: "FIN", "MNT", "INV", "FA", "HR"
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable module name.
    /// Examples: "Finance", "Maintenance", "Inventory", "Fixed Assets", "HR/Payroll"
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the module's purpose and scope.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Display order for UI lists.
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Whether the module is currently active and available.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indicates if this is a core/system module that cannot be disabled.
    /// </summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// Icon class or name for UI display (e.g., "fa-calculator", "wrench").
    /// </summary>
    [MaxLength(50)]
    public string? IconClass { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================

    /// <summary>
    /// Collection of period module locks associated with this module.
    /// </summary>
    public virtual ICollection<PeriodModuleLock> PeriodModuleLocks { get; set; } = new List<PeriodModuleLock>();

    /// <summary>
    /// Collection of document type mappings for this module.
    /// </summary>
    public virtual ICollection<TransactionDocumentModuleMapping> DocumentMappings { get; set; } = new List<TransactionDocumentModuleMapping>();
}
