using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Defines the segment structure for chart of accounts.
    /// Supports up to 20 segments for multi-dimensional financial reporting.
    /// Example: Company-Department-Account-SubAccount (4 segments)
    /// </summary>
    public class AccountSegmentStructure : TenantEntity
    {
        /// <summary>
        /// Descriptive name of the segment (e.g., 'Company', 'Department', 'Cost Center')
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string SegmentName { get; set; } = string.Empty;

        /// <summary>
        /// Unique code for system reference (e.g., 'SEG01', 'DEPT', 'CC')
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string SegmentCode { get; set; } = string.Empty;

        /// <summary>
        /// Sequential position in account number (1 to 20)
        /// Example: Company=1, Department=2, Account=3, SubAccount=4
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int SegmentPosition { get; set; }

        /// <summary>
        /// Fixed number of characters for this segment (1-10)
        /// Example: Department might be 2 characters (HR, IT, FN)
        /// </summary>
        [Required]
        [Range(1, 10)]
        public int SegmentLength { get; set; }

        /// <summary>
        /// Data type for segment values (currently only Alphanumeric supported)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string DataType { get; set; } = "Alphanumeric";

        /// <summary>
        /// Character used to separate segments in account number
        /// Options: Dash (-), Dot (.), Underscore (_), None
        /// </summary>
        [MaxLength(1)]
        public string? SeparatorCharacter { get; set; }

        /// <summary>
        /// Determines if segment values must exist in SegmentLookupValues table
        /// True = Values must be pre-defined (e.g., Departments must be set up first)
        /// False = Free-form entry allowed
        /// </summary>
        [Required]
        public bool LookupTableRequired { get; set; } = false;

        /// <summary>
        /// NEW FEATURE: Controls if segment appears in reports and BI tools
        /// True = Available in report filters and pivot tables
        /// False = Hidden from reporting UI (but still stored)
        /// 
        /// BENEFIT: Prevents report clutter when you have many segments
        /// Example: 10 total segments, but only 4 marked as reporting dimensions
        /// </summary>
        [Required]
        public bool IsReportingDimension { get; set; } = true;

        /// <summary>
        /// Designates this segment as the "Natural Account" segment
        /// (typically the main account classification like Asset, Liability, etc.)
        /// </summary>
        public bool IsNaturalAccount { get; set; } = false;

        /// <summary>
        /// Active status - allows deactivation without deletion
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Authoritative structure lifecycle. Active and Frozen segments are always required.
        /// IsActive remains a read/query compatibility projection and is maintained from this state.
        /// </summary>
        public AccountSegmentLifecycleStatus LifecycleStatus { get; set; } = AccountSegmentLifecycleStatus.Draft;

        public bool IsSystemDefined { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Guid? FrozenByUserId { get; set; }
        public DateTime? FrozenAtUtc { get; set; }

        [MaxLength(500)]
        public string? RetirementReason { get; set; }

        /// <summary>
        /// Optional description providing additional details about this segment
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        // Navigation Properties
        /// <summary>
        /// Collection of lookup values if LookupTableRequired = true
        /// </summary>
        public ICollection<SegmentLookupValue> LookupValues { get; set; } = new List<SegmentLookupValue>();
    }
}
