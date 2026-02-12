using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Maps transaction document types to their owning ERP modules.
/// Used for module-level period lock validation.
/// Configurable via database rather than hardcoded logic.
/// </summary>
public class TransactionDocumentModuleMapping : BaseEntity
{
    /// <summary>
    /// Tenant Identifier.
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Document type identifier.
    /// Examples: "JournalEntry", "WorkOrder", "InventoryAdjustment", 
    ///           "Invoice", "Payment", "PurchaseOrder", "AssetDepreciation"
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the module that owns this document type.
    /// </summary>
    [Required]
    public Guid ModuleDefinitionId { get; set; }

    /// <summary>
    /// Human-readable description of the document type.
    /// </summary>
    [MaxLength(250)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this mapping is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================

    /// <summary>
    /// The module that owns this document type.
    /// </summary>
    [ForeignKey(nameof(ModuleDefinitionId))]
    public virtual ModuleDefinition ModuleDefinition { get; set; } = null!;
}
