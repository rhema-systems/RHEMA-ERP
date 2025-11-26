using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Stores the actual segment values for each GL account.
    /// Example: If account 001-FIN-1000-01 has 4 segments,
    /// this table will have 4 records linking to that account.
    /// </summary>
    public class AccountSegmentValue : TenantEntity
    {
        /// <summary>
        /// Reference to the GL Account this segment belongs to
        /// </summary>
        [Required]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Reference to the segment structure definition
        /// Identifies which segment this is (Company, Department, etc.)
        /// </summary>
        [Required]
        public Guid SegmentStructureId { get; set; }

        /// <summary>
        /// The actual segment value used in the account number
        /// Example: 'FIN', '001', '1000', 'HR'
        /// Must match SegmentLength from AccountSegmentStructure
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string SegmentValue { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Reference to the lookup value if this segment uses lookup table
        /// Links to SegmentLookupValue for validation and hierarchy
        /// </summary>
        public Guid? SegmentLookupValueId { get; set; }

        /// <summary>
        /// Cached description from lookup value for performance
        /// Updated automatically when lookup value changes
        /// </summary>
        [MaxLength(200)]
        public string? SegmentValueDescription { get; set; }

        /// <summary>
        /// Position of this segment in the account number (1-20)
        /// Redundant with SegmentStructure.Position but improves query performance
        /// </summary>
        [Required]
        public int SegmentPosition { get; set; }

        /// <summary>
        /// Indicates if this segment value can be changed
        /// Set to false if transactions exist (prevents breaking audit trail)
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Date when this segment value became effective
        /// Used for historical tracking during segment changes
        /// </summary>
        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional: End date if segment value was changed
        /// Maintains history of segment changes over time
        /// </summary>
        public DateTime? EndDate { get; set; }

        // Navigation Properties
        /// <summary>
        /// The GL Account this segment value belongs to
        /// </summary>
        [ForeignKey(nameof(AccountId))]
        public Account Account { get; set; } = null!;

        /// <summary>
        /// The segment structure definition
        /// </summary>
        [ForeignKey(nameof(SegmentStructureId))]
        public AccountSegmentStructure SegmentStructure { get; set; } = null!;

        /// <summary>
        /// The lookup value if segment uses lookup table
        /// </summary>
        [ForeignKey(nameof(SegmentLookupValueId))]
        public SegmentLookupValue? SegmentLookupValue { get; set; }
    }
}