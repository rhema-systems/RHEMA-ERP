namespace ErpSystem.Core.DTOs.Maintenance;

public sealed class FleetHealthDto
{
    public int VehicleCategoriesCount { get; set; }
    public int VehiclesCount { get; set; }
    public int MaintenanceEmployeesCount { get; set; }
    public int EmployeesWithDriverLicenseCount { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

