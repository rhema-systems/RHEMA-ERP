namespace ErpSystem.Core.DTOs.Maintenance;

public sealed class FleetCostSummaryRowDto
{
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public int EntryCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal FuelAmount { get; set; }
    public decimal ExternalRepairAmount { get; set; }
    public decimal InternalMaintenanceAmount { get; set; }
    public decimal OtherAmount { get; set; }
}

public sealed class FleetCostSummaryDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public decimal TotalAmount { get; set; }
    public IReadOnlyList<FleetCostSummaryRowDto> Rows { get; set; } = Array.Empty<FleetCostSummaryRowDto>();
}

public sealed class FleetUtilizationRowDto
{
    public Guid VehicleAssetId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public int CompletedTrips { get; set; }
    public decimal TotalKm { get; set; }
    public decimal TotalHours { get; set; }
    public decimal? AverageKmPerTrip { get; set; }
    public decimal? AverageHoursPerTrip { get; set; }
}

public sealed class FleetUtilizationSummaryDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int CompletedTrips { get; set; }
    public decimal TotalKm { get; set; }
    public decimal TotalHours { get; set; }
    public IReadOnlyList<FleetUtilizationRowDto> Rows { get; set; } = Array.Empty<FleetUtilizationRowDto>();
}
