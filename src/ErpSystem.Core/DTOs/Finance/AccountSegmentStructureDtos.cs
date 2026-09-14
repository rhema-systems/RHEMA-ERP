// FILE: src/ErpSystem.Core/DTOs/Finance/AccountSegmentStructureDtos.cs

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    // ========================================================================
    // READ DTO: WHAT THE API RETURNS TO CLIENTS
    // ========================================================================

    /// <summary>
    /// DTO for returning account segment structure data from the API.
    /// 
    /// PATTERN:
    /// - Read DTOs are used for OUTPUT only.
    /// - They include identifiers (Id, TenantId), audit info (CreatedBy, CreatedAt, etc.),
    ///   business properties, and useful computed fields for UI/UX.
    /// 
    /// BUSINESS CONTEXT:
    /// - Represents a single "segment definition" in a segmented Chart of Accounts.
    /// - Each segment defines how part of the GL account number behaves
    ///   (e.g., Company, Department, Natural Account, Cost Center, Project).
    /// - Supports up to 20 segments as per the enhanced workflow document.
    /// 
    /// TYPICAL USE CASES (FRONTEND):
    /// - Finance Setup → Segmented Account Configuration screen.
    /// - API response when listing configured segments for a tenant.
    /// - Used to render forms, validation messages, and report filter options.
    /// </summary>
    public class AccountSegmentStructureDto
    {
        /// <summary>
        /// Unique identifier for this segment definition (GUID).
        /// 
        /// USAGE:
        /// - Used as the primary key when editing or disabling a segment.
        /// - Passed back to the API on update/delete operations.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Tenant identifier (GUID).
        /// 
        /// MULTI-TENANCY:
        /// - Ensures that segment structures are isolated per organization.
        /// - Frontend usually does NOT allow editing this value directly.
        /// - Typically resolved from the authenticated user's context.
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Descriptive name of the segment.
        /// 
        /// EXAMPLES:
        /// - "Company"
        /// - "Department"
        /// - "Natural Account"
        /// - "Cost Center"
        /// - "Project"
        /// 
        /// UI HINT:
        /// - Display this as the field label in account creation forms
        ///   and segment configuration screens.
        /// </summary>
        public string SegmentName { get; set; } = string.Empty;

        /// <summary>
        /// Unique short code used internally to reference this segment.
        /// 
        /// EXAMPLES:
        /// - "COMP" (Company)
        /// - "DEPT" (Department)
        /// - "ACCT" (Natural Account)
        /// - "CC"   (Cost Center)
        /// 
        /// SYSTEM BEHAVIOR:
        /// - Used for backend logic, mappings, report metadata, etc.
        /// - Must be unique per tenant.
        /// </summary>
        public string SegmentCode { get; set; } = string.Empty;

        /// <summary>
        /// Sequential position of this segment within the full account number.
        /// 
        /// RANGE:
        /// - Allowed values: 1 to 20 (from workflow specification).
        /// 
        /// EXAMPLE:
        /// - 1 = Company
        /// - 2 = Department
        /// - 3 = Natural Account
        /// - 4 = Sub-Account
        /// 
        /// UI HINT:
        /// - Use this to sort segments when rendering account masks and input fields.
        /// </summary>
        public int SegmentPosition { get; set; }

        /// <summary>
        /// Fixed number of characters that this segment must contain.
        /// 
        /// RANGE:
        /// - 1 to 10 characters as per workflow.
        /// 
        /// EXAMPLES:
        /// - Department: 2 chars (e.g., "HR", "IT")
        /// - Company:    3 chars (e.g., "001", "002")
        /// - Natural Account: 4 chars (e.g., "1000", "2100")
        /// 
        /// VALIDATION:
        /// - The system validates that entered segment values match this length.
        /// </summary>
        public int SegmentLength { get; set; }

        /// <summary>
        /// Data type allowed for this segment's values.
        /// 
        /// CURRENTLY:
        /// - Typically "Alphanumeric" as per design, but kept as string for future flexibility
        ///   (e.g., numeric-only or specific patterns).
        /// 
        /// UI HINT:
        /// - Frontend can use this to restrict input characters if needed.
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// Character used to separate this segment from the next in the full account number.
        /// 
        /// OPTIONS:
        /// - Dash ('-')
        /// - Dot  ('.')
        /// - Underscore ('_')
        /// - null / empty = no separator after this segment
        /// 
        /// EXAMPLE:
        /// - "001-FIN-1000-01" → separator is '-' between segments.
        /// 
        /// UI HINT:
        /// - Use this to build a live "Account Number Preview" as users select segment values.
        /// </summary>
        public string? SeparatorCharacter { get; set; }

        /// <summary>
        /// Indicates whether values for this segment must exist in a lookup table.
        /// 
        /// TRUE:
        /// - Segment values MUST be pre-defined in SegmentLookupValues.
        /// - E.g., Department codes must be created first ("HR", "FIN", "OPS").
        /// - UI should use a dropdown, not free-text.
        /// 
        /// FALSE:
        /// - Free-form entry allowed (subject to length and data type).
        /// - UI may use a standard text input with validation.
        /// </summary>
        public bool LookupTableRequired { get; set; }

        /// <summary>Every Active or Frozen account-number segment is required by definition.</summary>
        public bool IsRequired => true;

        public string LifecycleStatus { get; set; } = "Draft";
        public string RowVersion { get; set; } = string.Empty;
        public bool IsSystemDefined { get; set; }
        public int AccountUsageCount { get; set; }
        public bool CanActivate { get; set; }
        public bool CanFreeze { get; set; }

        /// <summary>
        /// Controls whether this segment is exposed as a reporting dimension
        /// in financial reports, dashboards, and BI tools.
        /// 
        /// TRUE:
        /// - Appears in report filter drop-downs (e.g., "Filter by Department").
        /// - Exported as a dimension to BI tools (Power BI, Excel, etc.).
        /// 
        /// FALSE:
        /// - Still stored in the account number but hidden from reporting UI.
        /// - Helps avoid clutter when there are many technical segments.
        /// 
        /// EXAMPLE:
        /// - 10 total segments, but only 4 marked as reporting dimensions:
        ///   Department, Cost Center, Project, Region.
        /// </summary>
        public bool IsReportingDimension { get; set; }

        /// <summary>
        /// Indicates whether this segment represents the "Natural Account" portion
        /// of the GL account (Assets, Liabilities, Equity, Revenue, Expense).
        /// 
        /// RULE:
        /// - Typically only ONE segment per tenant should be flagged as natural.
        /// 
        /// IMPACT:
        /// - Drives account type validation and financial statement classification.
        /// - Used when determining if an account is an Asset, Liability, etc.
        /// </summary>
        public bool IsNaturalAccount { get; set; }

        /// <summary>
        /// Active status flag.
        /// 
        /// TRUE:
        /// - Segment can be used in new account definitions.
        /// 
        /// FALSE:
        /// - Segment is effectively "retired":
        ///   - Existing accounts remain valid.
        ///   - Segment is not available for new account setup.
        /// 
        /// UI HINT:
        /// - Show inactive segments as disabled / greyed out with a warning.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Optional additional description or notes about the segment.
        /// 
        /// EXAMPLES:
        /// - "Used for internal management reporting only."
        /// - "Mandatory for all P&amp;L accounts."
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Count of lookup values currently configured for this segment.
        /// 
        /// PURPOSE:
        /// - Allows frontend to show "X values configured" without loading them all.
        /// - Useful for showing badges, warnings (e.g., "0 values defined"), etc.
        /// 
        /// PERFORMANCE:
        /// - Populated by the service layer; not directly mapped to a DB field.
        /// </summary>
        public int LookupValuesCount { get; set; }

        /// <summary>
        /// Preview string describing how this segment appears in the account number.
        /// 
        /// EXAMPLES:
        /// - "Department (2 chars) - Position 2"
        /// - "Company (3 chars) - Position 1"
        /// 
        /// UI HINT:
        /// - Display under each segment in the configuration UI for clarity.
        /// </summary>
        public string SegmentFormat => $"{SegmentName} ({SegmentLength} chars) - Position {SegmentPosition}";

        /// <summary>
        /// Indicates whether this segment can safely be modified (e.g., position, length).
        /// 
        /// TRUE:
        /// - Either no accounts exist or changes are considered safe.
        /// 
        /// FALSE:
        /// - There are existing accounts relying on this configuration.
        /// - Changing it could break historical data or account numbers.
        /// 
        /// NOTE:
        /// - Value is populated by the service layer based on usage analysis.
        /// </summary>
        public bool CanBeModified { get; set; } = true;

        /// <summary>
        /// Warning or guidance message explaining any restrictions on editing this segment.
        /// 
        /// EXAMPLES:
        /// - "Cannot change position - 150 accounts already use this segment."
        /// - "Length cannot be reduced because values longer than new length exist."
        /// 
        /// UI HINT:
        /// - Display as a tooltip or inline warning icon next to segment fields.
        /// </summary>
        public string? RestrictionWarning { get; set; }

        /// <summary>
        /// Optional collection of lightweight lookup values associated with this segment.
        /// 
        /// PURPOSE:
        /// - Enables admin UIs to display segment + its values in a single API call.
        /// 
        /// PERFORMANCE:
        /// - Kept as summary DTOs (code, description, status) rather than full entities.
        /// - Can be omitted or trimmed for list endpoints if payload becomes too large.
        /// </summary>
        public IReadOnlyCollection<SegmentLookupValueSummaryDto> LookupValues { get; set; }
            = Array.Empty<SegmentLookupValueSummaryDto>();

        // --------------------------------------------------------------------
        // AUDIT INFORMATION (from BaseEntity / BusinessEntity)
        // --------------------------------------------------------------------

        /// <summary>
        /// Username or identifier of the user who created this segment definition.
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp when this segment definition was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Username or identifier of the last user who modified this segment.
        /// </summary>
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// UTC timestamp of the last modification, if any.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }

    // ========================================================================
    // CREATE DTO: INPUT WHEN CREATING NEW SEGMENTS
    // ========================================================================

    /// <summary>
    /// DTO used when creating a new account segment structure.
    /// 
    /// PATTERN:
    /// - Create DTOs are used for INPUT only.
    /// - They do NOT expose Id, TenantId, or audit fields.
    /// - They include DataAnnotations for server-side validation.
    /// 
    /// BUSINESS RULES:
    /// - SegmentPosition must be between 1 and 20.
    /// - SegmentLength must be between 1 and 10.
    /// - At least one segment in the system must eventually be marked as Natural Account.
    /// - At least one segment should be flagged as reporting dimension.
    /// </summary>
    public class AccountSegmentStructureCreateDto
    {
        /// <summary>
        /// Descriptive name of the segment.
        /// 
        /// UI HINT:
        /// - Displayed as label and in configuration lists.
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string SegmentName { get; set; } = string.Empty;

        /// <summary>
        /// Short unique code for this segment.
        /// 
        /// RULES:
        /// - Must be unique per tenant.
        /// - Typically no spaces; use underscores or uppercase letters.
        /// 
        /// EXAMPLES:
        /// - "COMP", "DEPT", "ACCT", "PROJ"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string SegmentCode { get; set; } = string.Empty;

        /// <summary>
        /// Sequential position within the GL account number (1–20).
        /// 
        /// VALIDATION:
        /// - Server enforces 1 ≤ value ≤ 20.
        /// - Service layer will also enforce uniqueness of position per tenant.
        /// </summary>
        [Required]
        [Range(1, 20)]
        public int SegmentPosition { get; set; }

        /// <summary>
        /// Fixed character length for this segment (1–10).
        /// 
        /// VALIDATION:
        /// - Server enforces 1 ≤ value ≤ 10.
        /// 
        /// UI HINT:
        /// - Use this to limit input length and display examples like "XX" or "XXX".
        /// </summary>
        [Required]
        [Range(1, 10)]
        public int SegmentLength { get; set; }

        /// <summary>
        /// Data type for values in this segment.
        /// 
        /// CURRENT STANDARD:
        /// - "Alphanumeric" is the default.
        /// 
        /// FUTURE:
        /// - Could support "Numeric", "Uppercase", or pattern-based types.
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string DataType { get; set; } = "Alphanumeric";

        /// <summary>
        /// Separator character after this segment in the full account number.
        /// 
        /// OPTIONS:
        /// - "-", ".", "_", or null/empty for none.
        /// 
        /// NOTE:
        /// - This value is optional and can be omitted for the last segment.
        /// </summary>
        [MaxLength(1)]
        public string? SeparatorCharacter { get; set; }

        /// <summary>
        /// True if values must come from a predefined lookup table (SegmentLookupValue).
        /// 
        /// EXAMPLE:
        /// - Departments, Projects, Cost Centers typically have lookup values.
        /// </summary>
        [Required]
        public bool LookupTableRequired { get; set; } = false;

        /// <summary>
        /// True if this is the "Natural Account" segment.
        /// 
        /// RULE:
        /// - Typically only one segment is marked as natural; validation can enforce this.
        /// </summary>
        public bool IsNaturalAccount { get; set; } = false;

        /// <summary>
        /// Optional notes describing usage, restrictions, or business meaning.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }
    }

    // ========================================================================
    // UPDATE DTO: INPUT WHEN EDITING EXISTING SEGMENTS
    // ========================================================================

    /// <summary>
    /// DTO used when updating an existing account segment structure.
    /// 
    /// PATTERN:
    /// - Inherits from Create DTO to reuse validation rules and documentation.
    /// - Adds required Id (GUID) to identify which segment to update.
    /// </summary>
    public class AccountSegmentStructureUpdateDto : AccountSegmentStructureCreateDto
    {
        /// <summary>
        /// Identifier of the segment to update (GUID).
        /// 
        /// NOTE:
        /// - Frontend must supply this value from the read DTO (AccountSegmentStructureDto.Id).
        /// </summary>
        [Required]
        public Guid Id { get; set; }

        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class AccountSegmentLifecycleTransitionDto
    {
        [Required]
        public string RowVersion { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public sealed class AccountSegmentDeleteDto
    {
        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    // ========================================================================
    // LOOKUP VALUE SUMMARY DTO
    // ========================================================================

    /// <summary>
    /// Lightweight DTO representing a lookup value for a specific segment.
    /// 
    /// PURPOSE:
    /// - Embedded inside AccountSegmentStructureDto.LookupValues for efficient admin UIs.
    /// - Not intended for full CRUD; there will be dedicated Create/Update DTOs later.
    /// </summary>
    public class SegmentLookupValueSummaryDto
    {
        /// <summary>
        /// Unique identifier of the lookup value (GUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Code/value that will actually appear inside the account number for this segment.
        /// 
        /// EXAMPLES:
        /// - "HR", "FIN", "OPS" for Department.
        /// - "001", "002" for Company.
        /// 
        /// VALIDATION:
        /// - Must match the SegmentLength and allowed DataType for its segment.
        /// </summary>
        public string SegmentValue { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of the lookup value.
        /// 
        /// EXAMPLES:
        /// - "Human Resources Department"
        /// - "Finance Department"
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if this lookup value is active and selectable for new accounts.
        /// 
        /// TRUE:
        /// - Can be selected for new account creation.
        /// 
        /// FALSE:
        /// - Preserved for historical accounts but excluded from new selections.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Optional display order for UI sorting.
        /// 
        /// EXAMPLE:
        /// - 1 = show at top of dropdown.
        /// - Higher numbers = appear later.
        /// 
        /// NOTE:
        /// - If 0 or not set, natural/default sort order may be used.
        /// </summary>
        public int DisplayOrder { get; set; }
    }
}
