namespace ErpSystem.Core.DTOs.Maintenance;

// Request DTOs
public class CheckoutToolDto
{
    public Guid? WorkOrderId { get; set; }
    public Guid? JobCardId { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public string? CheckoutNotes { get; set; }
    public string ConditionOnCheckout { get; set; } = "Good"; // Good, Fair, Damaged
}

public class ReturnToolDto
{
    public string ConditionOnReturn { get; set; } = "Good";
    public string? ReturnNotes { get; set; }
    public bool DamageReported { get; set; }
    public string? DamageDescription { get; set; }
    public decimal? DamageCost { get; set; }
}

public class ToolDamageDto
{
    public string DamageDescription { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public bool RequiresRepair { get; set; }
}

// Response DTOs
public class ToolCheckoutResult
{
    public Guid CheckoutId { get; set; }
    public Guid ToolId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ToolCode { get; set; } = string.Empty;
    public DateTime CheckoutDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public string CheckedOutBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ToolReturnResult
{
    public Guid CheckoutId { get; set; }
    public DateTime ReturnDate { get; set; }
    public int DaysCheckedOut { get; set; }
    public bool IsOverdue { get; set; }
    public int? OverdueDays { get; set; }
    public bool DamageReported { get; set; }
}

public class ToolCheckoutDto
{
    public Guid Id { get; set; }
    public Guid ToolId { get; set; }
    public string ToolCode { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public Guid CheckedOutById { get; set; }
    public string CheckedOutByName { get; set; } = string.Empty;
    public DateTime CheckoutDate { get; set; }
    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int DaysOut { get; set; }
    public bool IsOverdue { get; set; }
    public Guid? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string? ConditionOnCheckout { get; set; }
    public string? CheckoutNotes { get; set; }
}

public class ToolAvailabilityDto
{
    public Guid ToolId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ToolCode { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string CurrentStatus { get; set; } = string.Empty;
    public DateTime? AvailableFrom { get; set; }
    public List<ToolCheckoutDto> UpcomingCheckouts { get; set; } = new();
}

public class MaintenanceToolDto
{
    public Guid Id { get; set; }
    public string ToolCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CurrentLocation { get; set; }
    public string? HomeLocation { get; set; }
    public bool RequiresCertification { get; set; }
    public bool RequiresTraining { get; set; }
    public decimal DailyRentalRate { get; set; }
    public DateTime? LastUsedDate { get; set; }
    public int TotalUsageDays { get; set; }
}

public class ToolCheckoutHistoryDto
{
    public Guid ToolId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ToolCode { get; set; } = string.Empty;
    public int TotalCheckouts { get; set; }
    public int TotalUsageDays { get; set; }
    public decimal TotalRentalCost { get; set; }
    public List<ToolCheckoutDto> RecentCheckouts { get; set; } = new();
}
