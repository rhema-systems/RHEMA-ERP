using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

/// <summary>
/// DTO for work order tool allocation
/// </summary>
public class WorkOrderToolDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid ToolId { get; set; }
    
    // Tool information
    public string ToolCode { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? CurrentLocation { get; set; }
    
    // Allocation status
    public bool IsRequired { get; set; }
    public bool IsAllocated { get; set; }
    public DateTime? AllocationDate { get; set; }
    
    // Checkout information
    public Guid? CheckoutId { get; set; }
    public bool IsCheckedOut { get; set; }
    public DateTime? CheckoutDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public string? CheckoutStatus { get; set; }
    
    // Checked out by (from work order assignment)
    public Guid? CheckedOutById { get; set; }
    public string? CheckedOutByName { get; set; }
    
    // Tool details
    public decimal DailyRentalRate { get; set; }
    public bool RequiresCertification { get; set; }
    public bool RequiresTraining { get; set; }
    public string? SafetyNotes { get; set; }
    
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for allocating a tool to a work order
/// </summary>
public class AllocateWorkOrderToolDto
{
    [Required]
    public Guid WorkOrderId { get; set; }
    
    [Required]
    public Guid ToolId { get; set; }
    
    [Required]
    public Guid WarehouseId { get; set; }
    
    public bool IsRequired { get; set; } = true;
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for checking out a tool for a work order
/// </summary>
public class CheckoutWorkOrderToolDto
{
    [Required]
    public Guid WorkOrderId { get; set; }
    
    [Required]
    public Guid ToolId { get; set; }
    
    [Required]
    [StringLength(20)]
    public string ConditionOnCheckout { get; set; } = "Good"; // Good, Fair, Damaged
    
    public DateTime? ExpectedReturnDate { get; set; }
    
    [StringLength(1000)]
    public string? CheckoutNotes { get; set; }
}

/// <summary>
/// DTO for returning a tool from a work order
/// </summary>
public class ReturnWorkOrderToolDto
{
    [Required]
    [StringLength(20)]
    public string ConditionOnReturn { get; set; } = "Good"; // Good, Fair, Damaged
    
    public bool DamageReported { get; set; } = false;
    
    [StringLength(2000)]
    public string? DamageDescription { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal? DamageCost { get; set; }
    
    [StringLength(1000)]
    public string? ReturnNotes { get; set; }
}

/// <summary>
/// Summary of tools for a work order
/// </summary>
public class WorkOrderToolSummaryDto
{
    public int TotalTools { get; set; }
    public int RequiredTools { get; set; }
    public int AllocatedTools { get; set; }
    public int CheckedOutTools { get; set; }
    public int ReturnedTools { get; set; }
    public int OverdueTools { get; set; }
    public decimal TotalRentalCost { get; set; }
}
