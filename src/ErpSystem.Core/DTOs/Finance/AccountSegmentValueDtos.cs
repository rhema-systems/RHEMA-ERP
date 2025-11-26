// FILE: src/ErpSystem.Core/DTOs/Finance/AccountSegmentValueDtos.cs

using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    // ========================================================================
    // READ DTO: WHAT THE API RETURNS TO CLIENTS
    // ========================================================================

    /// <summary>
    /// DTO for returning account segment values from the API.
    /// 
    /// BUSINESS CONTEXT:
    /// - Represents a single segment value attached to a GL Account.
    /// - Example: For account "001-FIN-1500-MACH":
    ///   - Segment 1: Company   = "001"
    ///   - Segment 2: Department = "FIN"
    ///   - Segment 3: Account    = "1500"
    ///   - Segment 4: SubAccount = "MACH"
    /// - The AccountSegmentValue table stores one record per segment per account.
    /// 
    /// TYPICAL USE CASES (FRONTEND):
    /// - Displaying the breakdown of a segmented account number in Account maintenance UI.
    /// - Editing which segments/values compose an account.
    /// - Validating segment values when restructuring or cloning accounts.
    /// 
    /// SOURCE ENTITY:
    /// - <see cref="ErpSystem.Core.Entities.Finance.AccountSegmentValue"/>
    /// </summary>
    public class AccountSegmentValueDto
    {
        /// <summary>
        /// Unique identifier of this segment value (GUID).
        /// 
        /// USAGE:
        /// - Primary key for editing or removing a specific segment value.
        /// - Passed in route/body for update and delete operations.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Tenant identifier (GUID) for multi-tenancy isolation.
        /// 
        /// NOTE:
        /// - Derived from the authenticated user in normal operations.
        /// - Exposed here for admin tools, logging and diagnostics.
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Reference to the GL Account this segment value belongs to.
        /// 
        /// EXAMPLE:
        /// - The GUID of the "Cash - Main Bank" account.
        /// 
        /// FRONTEND:
        /// - Usually not edited directly when working inside an Account detail view
        ///   (the account is implicit in the route).
        /// </summary>
        public Guid AccountId { get; set; }

        /// <summary>
        /// Reference to the segment structure definition.
        /// 
        /// EXAMPLE:
        /// - The GUID of the "Department" segment definition.
        /// 
        /// Used to:
        /// - Know which segment this value represents (Company/Dept/Project/etc.).
        /// - Enforce length and lookup rules from <c>AccountSegmentStructure</c>.
        /// </summary>
        public Guid SegmentStructureId { get; set; }

        /// <summary>
        /// The actual code for this segment in the account number.
        /// 
        /// EXAMPLES:
        /// - "001" for Company, "FIN" for Department, "1500" for Natural Account.
        /// 
        /// RULES:
        /// - Must satisfy SegmentLength and DataType defined in the related segment structure.
        /// </summary>
        public string SegmentValue { get; set; } = string.Empty;

        /// <summary>
        /// Optional reference to a lookup value, if the segment uses a lookup table.
        /// 
        /// EXAMPLES:
        /// - If Department segment is lookup-based, this links to "FIN" in SegmentLookupValue.
        /// 
        /// NOTES:
        /// - NULL if the segment is free-form (LookupTableRequired = false).
        /// - Enables hierarchy and reporting via lookup value configuration.
        /// </summary>
        public Guid? SegmentLookupValueId { get; set; }

        /// <summary>
        /// Cached description for this segment value.
        /// 
        /// SOURCE:
        /// - Typically copied from the related SegmentLookupValue.Description.
        /// 
        /// PURPOSE:
        /// - Improves read performance for account screens and reports.
        /// - Keeps account segment display independent from live joins where appropriate.
        /// </summary>
        public string? SegmentValueDescription { get; set; }

        /// <summary>
        /// Sequential position of this segment within the account number (1–20).
        /// 
        /// NOTE:
        /// - Duplicates SegmentStructure.SegmentPosition for performance and denormalized querying.
        /// - Allows ordering segment values for a given account without extra joins.
        /// </summary>
        public int SegmentPosition { get; set; }

        /// <summary>
        /// Indicates if this segment value is locked from modification.
        /// 
        /// TRUE:
        /// - Value cannot be changed because dependent transactions exist.
        /// - Prevents breaking the audit trail or historical reporting.
        /// 
        /// FALSE:
        /// - Changes are allowed (subject to service-layer business rules).
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Date when this segment value became effective for the account.
        /// 
        /// USAGE:
        /// - Supports historical tracking when segment assignments change over time.
        /// </summary>
        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// Optional end date when this segment value stopped being effective.
        /// 
        /// EXAMPLES:
        /// - Account moved from Department "FIN" to "CORP" on a given date.
        /// - Old Department value gets an EndDate; new one gets a new EffectiveDate.
        /// </summary>
        public DateTime? EndDate { get; set; }

        // ---------------------------------------------------------------------
        // OPTIONAL CONVENIENCE FIELDS FOR FRONTEND (NOT DIRECTLY IN ENTITY)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Convenience: Name of the segment definition (from AccountSegmentStructure).
        /// 
        /// EXAMPLES:
        /// - "Company", "Department", "Natural Account", "Project".
        /// 
        /// PURPOSE:
        /// - Allows UI to display understandable labels without extra round-trips.
        /// - Populated by the service layer via join to AccountSegmentStructure.
        /// </summary>
        public string? SegmentName { get; set; }

        /// <summary>
        /// Convenience: Code of the segment definition (from AccountSegmentStructure).
        /// 
        /// EXAMPLES:
        /// - "COMP", "DEPT", "ACCT", "PROJ".
        /// 
        /// PURPOSE:
        /// - Useful for client-side logic and filtering.
        /// - Populated by the service layer.
        /// </summary>
        public string? SegmentCode { get; set; }

        // ---------------------------------------------------------------------
        // AUDIT FIELDS (from BaseEntity)
        // ---------------------------------------------------------------------

        /// <summary>
        /// UTC timestamp when this segment value row was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// UTC timestamp when this segment value row was last updated (if ever).
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Username/identifier of the user who created this record.
        /// </summary>
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Username/identifier of the user who last updated this record.
        /// </summary>
        public string? UpdatedBy { get; set; }
    }

    // ========================================================================
    // CREATE DTO: INPUT WHEN DEFINING SEGMENT VALUES FOR AN ACCOUNT
    // ========================================================================

    /// <summary>
    /// DTO used when creating a new segment value for an account.
    /// 
    /// TYPICAL WORKFLOWS:
    /// - Creating a new segmented GL Account and attaching its segments.
    /// - Adding a missing segment value for an existing account (where business rules allow).
    /// 
    /// PATTERN:
    /// - Input-only; no Id, TenantId or audit fields.
    /// - Validation attributes enforce basic constraints.
    /// - Deeper validation (lookup enforcement, position uniqueness, length checks)
    ///   is implemented in the service layer.
    /// </summary>
    public class AccountSegmentValueCreateDto
    {
        /// <summary>
        /// GL Account that this segment value belongs to.
        /// 
        /// NOTE:
        /// - In many APIs, this might be taken from the route instead of the body.
        /// - Still included here for flexibility and self-contained payloads.
        /// </summary>
        [Required]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Segment structure definition for this segment value.
        /// 
        /// RULES:
        /// - Must reference an existing AccountSegmentStructure for the same tenant.
        /// - Service layer will enforce that each SegmentPosition is unique per account.
        /// </summary>
        [Required]
        public Guid SegmentStructureId { get; set; }

        /// <summary>
        /// The actual segment code/value that appears in the account number.
        /// 
        /// EXAMPLES:
        /// - "001" (Company)
        /// - "FIN" (Department)
        /// - "1500" (Account)
        /// - "MACH" (SubAccount)
        /// 
        /// VALIDATION:
        /// - Required.
        /// - Max length 10 characters (same as entity).
        /// - Service layer will validate the length against SegmentLength and
        ///   possibly DataType/pattern rules.
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string SegmentValue { get; set; } = string.Empty;

        /// <summary>
        /// Optional reference to a lookup value if the segment uses a lookup table.
        /// 
        /// BUSINESS RULE:
        /// - If the referenced segment requires lookup (LookupTableRequired = true),
        ///   then SegmentLookupValueId MUST be provided and must match SegmentValue.
        /// - If the segment is free-form, this should be NULL.
        /// </summary>
        public Guid? SegmentLookupValueId { get; set; }

        /// <summary>
        /// Position of this segment within the account number (1–20).
        /// 
        /// VALIDATION:
        /// - 1 ≤ SegmentPosition ≤ 20 (per enhanced workflow).
        /// - Service layer enforces uniqueness of SegmentPosition for each account.
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int SegmentPosition { get; set; }

        /// <summary>
        /// Initial locked status.
        /// 
        /// RECOMMENDATION:
        /// - Normally left as false on creation.
        /// - System will set to true when transactions exist that depend on this value.
        /// </summary>
        public bool IsLocked { get; set; } = false;

        /// <summary>
        /// Effective date from which this segment value is valid for the account.
        /// 
        /// DEFAULT BEHAVIOR:
        /// - If null, backend may default to the current date (UTC).
        /// 
        /// UI:
        /// - Use for advanced scenarios (segment reassignment over time).
        /// - Can usually be omitted in typical setups.
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>
        /// Optional end date if this segment value will stop being valid at some point.
        /// 
        /// VALIDATION:
        /// - If provided, must be greater than or equal to EffectiveDate (service layer).
        /// </summary>
        public DateTime? EndDate { get; set; }
    }

    // ========================================================================
    // UPDATE DTO: INPUT WHEN MODIFYING EXISTING SEGMENT VALUES
    // ========================================================================

    /// <summary>
    /// DTO used when updating an existing segment value for an account.
    /// 
    /// USAGE:
    /// - PUT /api/finance/accounts/{accountId}/segments/{id}
    /// - Admin UI for segment reassignment / corrections (subject to IsLocked and rules).
    /// 
    /// DESIGN:
    /// - Includes Id to identify the specific row.
    /// - Other fields mirror Create DTO, with same validation rules.
    /// </summary>
    public class AccountSegmentValueUpdateDto
    {
        /// <summary>
        /// Unique identifier of the segment value being updated.
        /// 
        /// NOTE:
        /// - Should match the id in the route for consistency.
        /// - Backend will typically validate route Id == body Id.
        /// </summary>
        [Required]
        public Guid Id { get; set; }

        /// <summary>
        /// GL Account this segment value belongs to.
        /// 
        /// - Kept mutable to allow controlled moves between accounts if ever required.
        /// - Most implementations will NOT allow changing this once created.
        /// </summary>
        [Required]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Segment structure definition this value is associated with.
        /// 
        /// - Changing this effectively moves the value to a different segment.
        /// - Service layer should carefully control/validate such changes.
        /// </summary>
        [Required]
        public Guid SegmentStructureId { get; set; }

        /// <summary>
        /// Updated segment value code.
        /// 
        /// WARNING:
        /// - If the account has posted transactions, system may disallow changes
        ///   depending on audit requirements.
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string SegmentValue { get; set; } = string.Empty;

        /// <summary>
        /// Updated lookup value reference (for lookup-based segments).
        /// 
        /// - May be changed when migrating accounts to a new lookup code.
        /// - Service layer should ensure consistency with SegmentValue.
        /// </summary>
        public Guid? SegmentLookupValueId { get; set; }

        /// <summary>
        /// Updated positional order of this segment in the account number (1–20).
        /// 
        /// - Reordering might be restricted once accounts are in heavy use.
        /// - Service layer enforces globally valid configuration.
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int SegmentPosition { get; set; }

        /// <summary>
        /// Updated lock status.
        /// 
        /// - Typically controlled by the system based on transactional usage.
        /// - Manual unlocking (if allowed) should be highly restricted.
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Updated effective date.
        /// 
        /// - May be used when correcting the historical start of a segment assignment.
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>
        /// Updated end date.
        /// 
        /// - Used to end-date a segment assignment when restructuring accounts.
        /// </summary>
        public DateTime? EndDate { get; set; }
    }
}
