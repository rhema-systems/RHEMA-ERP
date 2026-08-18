using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Stores the governed builder definition behind one shared <see cref="Report"/>.
/// The JSON contains only catalogue field keys, operators and values; executable
/// SQL is never accepted from the browser or persisted here.
/// </summary>
public class FinanceAdHocReportDefinition : TenantEntity
{
    [Required]
    public Guid ReportId { get; set; }

    [Required]
    [MaxLength(50)]
    public string DatasetCode { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string DefinitionJson { get; set; } = "{}";

    [Required]
    public Guid OwnerUserId { get; set; }

    /// <summary>
    /// Private definitions are visible only to their owner. Finance definitions
    /// are visible to authorised Finance report users in the same tenant.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Visibility { get; set; } = FinanceAdHocReportValues.PrivateVisibility;

    public int MaximumRows { get; set; } = 5000;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Report Report { get; set; } = null!;
    public virtual ApplicationUser OwnerUser { get; set; } = null!;
}

/// <summary>
/// Canonical values shared by the Finance builder API, provider and UI.
/// </summary>
public static class FinanceAdHocReportValues
{
    public const string QueryPrefix = "finance-adhoc:";
    public const string PrivateVisibility = "Private";
    public const string FinanceVisibility = "Finance";

    public static readonly IReadOnlySet<string> Visibilities = new HashSet<string>(
        new[] { PrivateVisibility, FinanceVisibility }, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlySet<string> Aggregations = new HashSet<string>(
        new[] { "None", "Count", "Sum", "Average", "Minimum", "Maximum" },
        StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlySet<string> FilterOperators = new HashSet<string>(new[]
    {
        "Equals", "NotEquals", "Contains", "StartsWith", "GreaterThan",
        "GreaterThanOrEqual", "LessThan", "LessThanOrEqual", "Between",
        "IsBlank", "IsNotBlank"
    }, StringComparer.OrdinalIgnoreCase);
}
