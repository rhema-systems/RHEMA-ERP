using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public sealed class FleetHealthService : IFleetHealthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FleetHealthService(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<FleetHealthDto> GetHealthAsync()
    {
        var tenantId = _currentUserProvider.TenantId;

        var vehicleCategoriesCount = await _unitOfWork.Repository<MaintenanceAssetCategory>()
            .GetQueryable(c =>
                c.TenantId == tenantId &&
                !c.IsDeleted &&
                c.IsActive &&
                c.AssetType != null &&
                c.AssetType.ToLower() == "vehicle")
            .CountAsync();

        var vehiclesCount = await _unitOfWork.Repository<MaintenanceAsset>()
            .GetQueryable(a => a.TenantId == tenantId && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .Where(a => a.AssetCategory != null && a.AssetCategory.AssetType == "Vehicle")
            .CountAsync();

        var activeEmployeesQ = _unitOfWork.Repository<Employee>()
            .GetQueryable(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive);

        var maintenanceDepartmentsQ = _unitOfWork.Repository<Department>()
            .GetQueryable(d =>
                d.TenantId == tenantId &&
                !d.IsDeleted &&
                d.IsActive &&
                d.DepartmentType == ErpSystem.Core.Enums.DepartmentType.Maintenance);

        var maintenanceEmployeesCount = await (from e in activeEmployeesQ
                                               join d in maintenanceDepartmentsQ on e.DepartmentId equals d.Id
                                               select e.Id)
            .Distinct()
            .CountAsync();

        var employeesWithDriverLicenseCount = await _unitOfWork.Repository<EmployeeIdentificationCard>()
            .GetQueryable(c =>
                c.TenantId == tenantId &&
                !c.IsDeleted &&
                c.DocumentType.ToLower().Contains("driver"))
            .Select(c => c.EmployeeId)
            .Distinct()
            .CountAsync();

        var warnings = new List<string>();
        if (vehicleCategoriesCount == 0)
            warnings.Add("No vehicle asset categories found (MaintenanceAssetCategory.AssetType = \"Vehicle\"). Create at least one vehicle category before setting up fleet vehicles.");
        if (vehiclesCount == 0)
            warnings.Add("No vehicles found. Vehicles are assets whose category AssetType is \"Vehicle\".");
        if (maintenanceEmployeesCount == 0)
            warnings.Add("No active Maintenance employees found. Add employees with DepartmentType = Maintenance to assign technicians/drivers.");
        if (employeesWithDriverLicenseCount == 0)
            warnings.Add("No employee driver's licenses found. Add an Employee Identification Card whose DocumentType contains \"Driver\" to dispatch fleet trips with driver license blocking enabled.");

        return new FleetHealthDto
        {
            VehicleCategoriesCount = vehicleCategoriesCount,
            VehiclesCount = vehiclesCount,
            MaintenanceEmployeesCount = maintenanceEmployeesCount,
            EmployeesWithDriverLicenseCount = employeesWithDriverLicenseCount,
            Warnings = warnings
        };
    }
}
