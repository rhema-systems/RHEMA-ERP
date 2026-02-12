// FILE: src/ErpSystem.Core/DTOs/Finance/AccountCombinationDtos.cs

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// Request DTO for generating account combinations from selected segment values.
    /// 
    /// USAGE:
    /// - POST /api/finance/accounts/combinations/preview
    /// - Generates preview of all possible account combinations
    /// 
    /// DESIGN:
    /// - Segment selections use SegmentStructureId to identify which segment
    /// - Account number construction respects current SegmentPosition ordering
    /// - Works correctly even after segment reordering operations
    /// </summary>
    public class CombinationRequestDto
    {
        /// <summary>
        /// List of segment selections - one entry per segment with selected lookup values
        /// </summary>
        [Required]
        public List<SegmentSelectionDto> SegmentSelections { get; set; } = new();

        /// <summary>
        /// Account type for all generated accounts (Asset, Liability, Equity, Revenue, Expense)
        /// </summary>
        [Required]
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// Optional account category for all generated accounts
        /// </summary>
        public string? AccountCategory { get; set; }

        /// <summary>
        /// Optional account sub-category for all generated accounts
        /// </summary>
        public string? AccountSubCategory { get; set; }

        /// <summary>
        /// Currency code for all generated accounts (defaults to base currency)
        /// </summary>
        [Required]
        [StringLength(3)]
        public string CurrencyCode { get; set; } = "GHS";

        /// <summary>
        /// Whether generated accounts should support multi-currency
        /// </summary>
        public bool IsMultiCurrency { get; set; } = false;

        /// <summary>
        /// Whether generated accounts allow direct posting
        /// </summary>
        public bool AllowDirectPosting { get; set; } = true;

        /// <summary>
        /// Whether budget tracking is enabled for generated accounts
        /// </summary>
        public bool BudgetTrackingEnabled { get; set; } = false;

        /// <summary>
        /// If true, include existing accounts in preview (marked as duplicates)
        /// If false, exclude existing accounts from preview
        /// </summary>
        public bool IncludeExistingInPreview { get; set; } = true;
    }

    /// <summary>
    /// Represents selected lookup values for a single segment
    /// </summary>
    public class SegmentSelectionDto
    {
        /// <summary>
        /// ID of the segment structure (e.g., Department segment, Cost Center segment)
        /// </summary>
        [Required]
        public Guid SegmentStructureId { get; set; }

        /// <summary>
        /// List of selected lookup value IDs for this segment
        /// </summary>
        [Required]
        public List<Guid> SelectedLookupValueIds { get; set; } = new();
    }

    // ========================================================================
    // PREVIEW DTO
    // ========================================================================

    /// <summary>
    /// Preview of a single account that would be created from segment combination.
    /// 
    /// USAGE:
    /// - Returned from preview endpoint before bulk creation
    /// - Shows validation status (valid, duplicate, invalid)
    /// - Allows user review before committing
    /// </summary>
    public class AccountCombinationPreviewDto
    {
        /// <summary>
        /// Generated account number from segment values
        /// Respects current SegmentPosition ordering
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Auto-generated account name from segment descriptions
        /// Pattern: {NaturalAccount} - {Segment1} - {Segment2} ...
        /// </summary>
        public string GeneratedName { get; set; } = string.Empty;

        /// <summary>
        /// Segment values that compose this account
        /// </summary>
        public List<SegmentValuePreviewDto> SegmentValues { get; set; } = new();

        /// <summary>
        /// Validation status of this combination
        /// </summary>
        public CombinationStatus Status { get; set; } = CombinationStatus.Valid;

        /// <summary>
        /// If status is Duplicate, the ID of the existing account
        /// </summary>
        public Guid? ExistingAccountId { get; set; }

        /// <summary>
        /// Validation message if status is Invalid
        /// </summary>
        public string? ValidationMessage { get; set; }

        /// <summary>
        /// Account type to be assigned
        /// </summary>
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// Account category if specified
        /// </summary>
        public string? AccountCategory { get; set; }

        /// <summary>
        /// Currency code for the account
        /// </summary>
        public string CurrencyCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Preview of a single segment value in a combination
    /// </summary>
    public class SegmentValuePreviewDto
    {
        /// <summary>
        /// ID of the segment structure
        /// </summary>
        public Guid SegmentStructureId { get; set; }

        /// <summary>
        /// Name of the segment (e.g., "Department", "Cost Center")
        /// </summary>
        public string SegmentName { get; set; } = string.Empty;

        /// <summary>
        /// Position of segment in account number
        /// </summary>
        public int SegmentPosition { get; set; }

        /// <summary>
        /// ID of the selected lookup value
        /// </summary>
        public Guid LookupValueId { get; set; }

        /// <summary>
        /// The segment value code (e.g., "100", "FIN")
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Description of the segment value (e.g., "Finance Department")
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status of a combination preview
    /// </summary>
    public enum CombinationStatus
    {
        /// <summary>
        /// Combination is valid and can be created
        /// </summary>
        Valid,

        /// <summary>
        /// Account with this number already exists
        /// </summary>
        Duplicate,

        /// <summary>
        /// Combination is invalid (e.g., fails cross-validation)
        /// </summary>
        Invalid
    }

    // ========================================================================
    // BULK CREATE REQUEST/RESULT DTOs
    // ========================================================================

    /// <summary>
    /// Request to bulk create accounts from previewed combinations
    /// </summary>
    public class BulkCreateAccountsRequestDto
    {
        /// <summary>
        /// List of previewed combinations to create (should be filtered to valid ones)
        /// </summary>
        [Required]
        public List<AccountCombinationPreviewDto> Combinations { get; set; } = new();

        /// <summary>
        /// Skip combinations marked as Duplicate (default: true)
        /// </summary>
        public bool SkipDuplicates { get; set; } = true;
    }

    /// <summary>
    /// Result of bulk account creation operation
    /// </summary>
    public class BulkCreationResultDto
    {
        /// <summary>
        /// Total number of combinations requested
        /// </summary>
        public int TotalRequested { get; set; }

        /// <summary>
        /// Number of accounts successfully created
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Number of accounts skipped (duplicates or invalid)
        /// </summary>
        public int SkipCount { get; set; }

        /// <summary>
        /// Number of accounts that failed to create
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// IDs of successfully created accounts
        /// </summary>
        public List<Guid> CreatedAccountIds { get; set; } = new();

        /// <summary>
        /// Details of any creation errors
        /// </summary>
        public List<BulkCreationErrorDto> Errors { get; set; } = new();
    }

    /// <summary>
    /// Details of a single error during bulk creation
    /// </summary>
    public class BulkCreationErrorDto
    {
        /// <summary>
        /// Account number that failed
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Error message
        /// </summary>
        public string Error { get; set; } = string.Empty;
    }
}
