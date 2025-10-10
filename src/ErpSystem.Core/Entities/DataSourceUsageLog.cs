using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities;

public class DataSourceUsageLog : BaseEntity
{
    [Required]
    public Guid DataSourceId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public DateTime AccessedAt { get; set; }

    [MaxLength(100)]
    public string? OperationType { get; set; } // Query, TestConnection, etc.

    public TimeSpan? ExecutionTime { get; set; }

    [MaxLength(2000)]
    public string? QueryExecuted { get; set; }

    public int? RecordsReturned { get; set; }

    public bool Success { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public virtual DataSource DataSource { get; set; } = null!;
}