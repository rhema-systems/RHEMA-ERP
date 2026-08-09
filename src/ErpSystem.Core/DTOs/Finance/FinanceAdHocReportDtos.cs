using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class FinanceAdHocDatasetFieldDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool CanFilter { get; set; }
    public bool CanGroup { get; set; }
    public bool CanAggregate { get; set; }
}

public sealed class FinanceAdHocDatasetDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<FinanceAdHocDatasetFieldDto> Fields { get; set; } = [];
}

public sealed class FinanceAdHocColumnDto
{
    [Required, StringLength(80)]
    public string Field { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Aggregation { get; set; } = "None";
}

public sealed class FinanceAdHocFilterDto
{
    [Required, StringLength(80)]
    public string Field { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Operator { get; set; } = "Equals";

    [StringLength(500)]
    public string? Value { get; set; }

    [StringLength(500)]
    public string? ValueTo { get; set; }
}

public sealed class FinanceAdHocSortDto
{
    [Required, StringLength(80)]
    public string Field { get; set; } = string.Empty;

    public bool Descending { get; set; }
}

/// <summary>
/// A declarative, non-SQL report specification. Every field/operator is checked
/// against a server-owned Finance dataset catalogue before it can be saved or run.
/// </summary>
public class CreateFinanceAdHocReportDto
{
    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string DatasetCode { get; set; } = string.Empty;

    [MinLength(1)]
    public List<FinanceAdHocColumnDto> Columns { get; set; } = [];

    public List<FinanceAdHocFilterDto> Filters { get; set; } = [];
    public List<FinanceAdHocSortDto> Sorts { get; set; } = [];

    [Required, StringLength(20)]
    public string Visibility { get; set; } = "Private";

    [Range(1, 5000)]
    public int MaximumRows { get; set; } = 5000;
}

public sealed class UpdateFinanceAdHocReportDto : CreateFinanceAdHocReportDto
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class FinanceAdHocReportDefinitionDto
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DatasetCode { get; set; } = string.Empty;
    public string DatasetName { get; set; } = string.Empty;
    public List<FinanceAdHocColumnDto> Columns { get; set; } = [];
    public List<FinanceAdHocFilterDto> Filters { get; set; } = [];
    public List<FinanceAdHocSortDto> Sorts { get; set; } = [];
    public string Visibility { get; set; } = string.Empty;
    public int MaximumRows { get; set; }
    public Guid OwnerUserId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    /// <summary>
    /// Tells the client whether the current user owns this definition. Shared definitions remain
    /// executable by authorised Finance users, but only their owner can alter or delete them.
    /// </summary>
    public bool CanMaintain { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRun { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class FinanceAdHocReportWorkspaceDto
{
    public List<FinanceAdHocDatasetDto> Datasets { get; set; } = [];
    public List<FinanceAdHocReportDefinitionDto> Definitions { get; set; } = [];
    public int PrivateDefinitions { get; set; }
    public int SharedDefinitions { get; set; }
}
