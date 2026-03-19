using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Stores lookup values for segments that require validation.
    /// Example: If Department segment requires lookup, this table stores HR, IT, FIN, etc.
    /// </summary>
    public class SegmentLookupValue : TenantEntity
    {
        /// <summary>
        /// Reference to the segment this value belongs to
        /// </summary>
        [Required]
        public Guid SegmentStructureId { get; set; }

        /// <summary>
        /// The actual code/value that will appear in account numbers
        /// Example: 'HR', 'IT', 'FIN', 'SALES'
        /// Must match the SegmentLength defined in AccountSegmentStructure
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string SegmentValue { get; set; } = string.Empty;

        /// <summary>
        /// Full descriptive name for the value
        /// Example: 'Human Resources Department', 'Information Technology'
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Parent segment value for hierarchical structures
        /// Example: 'IT' might be parent of 'IT-SUPPORT', 'IT-DEV'
        /// Enables drill-down reporting from parent to children
        /// </summary>
        public Guid? ParentValueId { get; set; }

        /// <summary>
        /// Date from which this value becomes available for use
        /// Allows future-dated values (e.g., new department opening next quarter)
        /// </summary>
        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional: Date after which value cannot be used for new accounts
        /// Useful for phasing out departments or cost centers
        /// Existing accounts remain unaffected
        /// </summary>
        public DateTime? ExpiryDate { get; set; }

        /// <summary>
        /// Active status - controls availability for new accounts
        /// Inactive values cannot be used for new accounts but historical data remains
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Display order for UI dropdowns and reports
        /// Lower numbers appear first
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// Additional notes or instructions for this segment value
        /// </summary>
        [MaxLength(1000)]
        public string? Notes { get; set; }

        // Navigation Properties
        /// <summary>
        /// Reference to the segment structure this value belongs to
        /// </summary>
        [ForeignKey(nameof(SegmentStructureId))]
        public AccountSegmentStructure SegmentStructure { get; set; } = null!;

        /// <summary>
        /// Parent value for hierarchical relationships
        /// </summary>
        [ForeignKey(nameof(ParentValueId))]
        public SegmentLookupValue? ParentValue { get; set; }

        /// <summary>
        /// Child values if this is a parent in hierarchy
        /// </summary>
        public ICollection<SegmentLookupValue> ChildValues { get; set; } = new List<SegmentLookupValue>();

        /// <summary>
        /// Accounts using this segment value
        /// </summary>
        public ICollection<AccountSegmentValue> AccountSegmentValues { get; set; } = new List<AccountSegmentValue>();
    }
}