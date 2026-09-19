using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// DTO for creating a new account segment structure
    /// </summary>
    public class SegmentStructureCreateDto
    {
        [Required]
        [MaxLength(100)]
        public string SegmentName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string SegmentCode { get; set; } = string.Empty;

        [Required]
        [Range(1, 20)]
        public int SegmentPosition { get; set; }

        [Required]
        [Range(1, 10)]
        public int SegmentLength { get; set; }

        [Required]
        [MaxLength(20)]
        public string DataType { get; set; } = "Alphanumeric";

        [MaxLength(1)]
        public string? SeparatorCharacter { get; set; } = "-";

        public bool LookupTableRequired { get; set; } = false;
        public bool IsNaturalAccount { get; set; } = false;

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing segment structure
    /// </summary>
    public class SegmentStructureUpdateDto
    {
        [Required]
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string? SegmentName { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for segment structure response
    /// </summary>
    public class SegmentStructureDto
    {
        public Guid Id { get; set; }
        public string SegmentName { get; set; } = string.Empty;
        public string SegmentCode { get; set; } = string.Empty;
        public int SegmentPosition { get; set; }
        public int SegmentLength { get; set; }
        public string DataType { get; set; } = string.Empty;
        public string? SeparatorCharacter { get; set; }
        public bool LookupTableRequired { get; set; }
        public bool IsRequired { get; set; } = true;
        public bool IsReportingDimension { get; set; }
        public bool IsNaturalAccount { get; set; }
        public bool IsActive { get; set; }
        public string LifecycleStatus { get; set; } = "Draft";
        public string RowVersion { get; set; } = string.Empty;
        public int AccountUsageCount { get; set; }
        public bool CanActivate { get; set; }
        public bool CanFreeze { get; set; }
        public bool IsSystemDefined { get; set; }
        public string? Description { get; set; }
        public int LookupValueCount { get; set; }

        /// <summary>
        /// List of lookup values for this segment (when loading with details)
        /// </summary>
        public List<SegmentLookupValueDto>? LookupValues { get; set; }
    }

    /// <summary>
    /// A valid reporting value derived from active tenant GL account combinations.
    /// </summary>
    public class ReportingSegmentOptionDto
    {
        public string SegmentValue { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int AccountCombinationCount { get; set; }
    }

    /// <summary>
    /// Bounded option response used by async financial-report segment selectors.
    /// </summary>
    public class ReportingSegmentOptionsDto
    {
        public List<ReportingSegmentOptionDto> Items { get; set; } = new();
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// DTO for validating a segmented account number
    /// </summary>
    public class SegmentedAccountValidationDto
    {
        [Required]
        public string AccountNumber { get; set; } = string.Empty;

        public bool IsValid { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<SegmentValueDto> ParsedSegments { get; set; } = new();
    }

    /// <summary>
    /// DTO for a parsed segment value
    /// </summary>
    public class SegmentValueDto
    {
        public int SegmentPosition { get; set; }
        public string SegmentName { get; set; } = string.Empty;
        public string SegmentValue { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? ValidationMessage { get; set; }
    }

    /// <summary>
    /// DTO for constructing an account number from segment values
    /// </summary>
    public class AccountNumberConstructionDto
    {
        public Dictionary<int, string> SegmentValues { get; set; } = new();
        public string ConstructedAccountNumber { get; set; } = string.Empty;
    }
}
