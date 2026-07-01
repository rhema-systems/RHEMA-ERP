using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance.Fleet;

public class FleetVehicleService : IFleetVehicleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IMaintenanceAssetService _maintenanceAssetService;

    public FleetVehicleService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IMaintenanceAssetService maintenanceAssetService)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _maintenanceAssetService = maintenanceAssetService;
    }

    public async Task<PagedResult<FleetVehicleListDto>> GetVehiclesPagedAsync(
        int page,
        int pageSize,
        string? searchTerm = null,
        Guid? categoryId = null,
        string? assetType = null)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _currentUserProvider.TenantId;
        var assetRepo = _unitOfWork.Repository<MaintenanceAsset>();
        var assignmentRepo = _unitOfWork.Repository<FleetVehicleAssignment>();
        var activeAssignmentsQ = assignmentRepo.GetQueryable(x => x.TenantId == tenantId && x.IsActive);
        var fleetTripRepo = _unitOfWork.Repository<FleetTrip>();
        var fleetTripActivityQ = fleetTripRepo.GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted);
        var maintenanceScheduleRepo = _unitOfWork.Repository<MaintenanceSchedule>();
        var activeMaintenanceSchedulesQ = maintenanceScheduleRepo.GetQueryable(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted);

        var q = assetRepo.GetQueryable(a => a.TenantId == tenantId)
            .Where(a => a.IsFleetAsset);

        var scope = await GetMaintenanceLocationScopeAsync();
        if (scope.IsScoped)
        {
            q = scope.LocationId.HasValue
                ? q.Where(a => a.CurrentSiteLocationId == scope.LocationId.Value)
                : q.Where(a => false);
        }

        // Optional filter for screens that specifically need an asset type (e.g., trip dispatch expects Vehicle assets).
        if (!string.IsNullOrWhiteSpace(assetType))
        {
            var t = assetType.Trim();
            q = q.Where(a => a.AssetCategory != null && a.AssetCategory.AssetType == t);
        }

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            q = q.Where(a => a.AssetCategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            q = q.Where(a =>
                a.Name.Contains(term) ||
                a.AssetNumber.Contains(term) ||
                (a.LicensePlate != null && a.LicensePlate.Contains(term)) ||
                (a.VIN != null && a.VIN.Contains(term)));
        }

        var total = await q.CountAsync();

        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new FleetVehicleListDto
            {
                Id = a.Id,
                Name = a.Name,
                AssetNumber = a.AssetNumber,
                AssetCategoryId = a.AssetCategoryId,
                AssetType = a.AssetCategory != null ? a.AssetCategory.AssetType : null,
                LicensePlate = a.LicensePlate,
                Vin = a.VIN,
                FuelType = a.FuelType,
                Status = a.Status.ToString(),
                CategoryName = a.AssetCategory != null ? a.AssetCategory.Name : string.Empty,
                Manufacturer = a.Manufacturer,
                Model = a.Model,
                Mileage = a.Mileage,
                OperatingHours = a.OperatingHours,
                Location = a.Location,
                CurrentProjectId = a.CurrentProjectId,
                CurrentProjectName = a.CurrentProject != null ? a.CurrentProject.Title : null,
                CurrentSiteLocationId = a.CurrentSiteLocationId,
                CurrentSiteLocationName = a.CurrentSiteLocation != null ? a.CurrentSiteLocation.Name : null,
                LastUsedAtUtc = fleetTripActivityQ
                    .Where(x => x.VehicleAssetId == a.Id &&
                        (x.Status == FleetTripStatuses.Dispatched || x.Status == FleetTripStatuses.Completed))
                    .OrderByDescending(x => x.CompletedAt ?? x.ActualEndAt ?? x.DispatchedAt ?? x.ActualStartAt ?? x.PlannedStartAt ?? x.UpdatedAt ?? (DateTime?)x.CreatedAt)
                    .Select(x => x.CompletedAt ?? x.ActualEndAt ?? x.DispatchedAt ?? x.ActualStartAt ?? x.PlannedStartAt ?? x.UpdatedAt ?? (DateTime?)x.CreatedAt)
                    .FirstOrDefault()
                    ?? a.LastMileageUpdate
                    ?? a.LastOperatingHoursUpdate
                    ?? (a.Status == AssetStatus.InUse ? (DateTime?)(a.UpdatedAt ?? a.CreatedAt) : null),
                LastServiceDate = a.LastServiceDate,
                NextServiceDue = a.NextServiceDue,
                NextMaintenanceDate = activeMaintenanceSchedulesQ
                    .Where(x => x.AssetId == a.Id)
                    .OrderBy(x => x.NextDueDate)
                    .Select(x => (DateTime?)x.NextDueDate)
                    .FirstOrDefault(),
                NextMaintenanceScheduleDueAt = activeMaintenanceSchedulesQ
                    .Where(x => x.AssetId == a.Id)
                    .OrderBy(x => x.NextDueDate)
                    .Select(x => (DateTime?)x.NextDueDate)
                    .FirstOrDefault(),
                CurrentDriverEmployeeId = activeAssignmentsQ
                    .Where(x => x.VehicleAssetId == a.Id)
                    .OrderByDescending(x => x.AssignedFromUtc)
                    .Select(x => (Guid?)x.EmployeeId)
                    .FirstOrDefault(),
                CurrentDriverEmployeeName = activeAssignmentsQ
                    .Where(x => x.VehicleAssetId == a.Id)
                    .OrderByDescending(x => x.AssignedFromUtc)
                    .Select(x => x.Employee.FirstName + " " + x.Employee.LastName)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return new PagedResult<FleetVehicleListDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<(bool IsScoped, Guid? LocationId)> GetMaintenanceLocationScopeAsync()
    {
        if (!_currentUserProvider.IsAuthenticated)
        {
            return (false, null);
        }

        var unrestrictedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Constants.Roles.SuperAdmin,
            Constants.Roles.TenantAdmin,
            Constants.Roles.Manager,
            "MaintenanceManager",
            "MaintenanceSupervisor",
            "MaintenanceDirector",
            "FleetManager",
            "FleetSupervisor"
        };

        if ((_currentUserProvider.Roles ?? Enumerable.Empty<string>()).Any(unrestrictedRoles.Contains))
        {
            return (false, null);
        }

        if (!_currentUserProvider.Claims.TryGetValue("employee_id", out var employeeClaim) ||
            !Guid.TryParse(employeeClaim, out var employeeId))
        {
            return (false, null);
        }

        var employee = await _unitOfWork.Repository<Employee>()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == _currentUserProvider.TenantId && e.IsActive, e => e.Department);

        if (employee?.Department == null ||
            !employee.Department.Name.Contains("Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null);
        }

        return (true, employee.LocationId);
    }

    public async Task<MaintenanceAssetDto?> GetVehicleByIdAsync(Guid vehicleAssetId)
    {
        if (vehicleAssetId == Guid.Empty) return null;

        var asset = await _maintenanceAssetService.GetAssetByIdAsync(vehicleAssetId);
        if (asset == null) return null;

        if (!asset.IsFleetAsset)
            return null;

        return asset;
    }

    public async Task<MaintenanceAssetDto> CreateVehicleAsync(CreateFleetVehicleDto dto)
    {
        dto ??= new CreateFleetVehicleDto();

        var category = await _unitOfWork.Repository<MaintenanceAssetCategory>()
            .FirstOrDefaultAsync(c => c.Id == dto.AssetCategoryId && c.TenantId == _currentUserProvider.TenantId);

        if (category == null)
            throw new ArgumentException("Asset category not found.");

        if (!string.Equals(category.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected asset category is not a Vehicle category.");

        var createAssetDto = new CreateMaintenanceAssetDto
        {
            Name = dto.Name,
            AssetCategoryId = dto.AssetCategoryId,
            Manufacturer = dto.Manufacturer,
            Model = dto.Model,
            SerialNumber = dto.SerialNumber,
            Location = dto.Location,
            Status = "Active",
            Criticality = "Medium",
            Mileage = dto.Mileage,
            OperatingHours = dto.OperatingHours,
            LicensePlate = dto.LicensePlate,
            VIN = dto.Vin,
            FuelType = dto.FuelType,
            IsFleetAsset = true
        };

        return await _maintenanceAssetService.CreateAssetAsync(createAssetDto);
    }

    public async Task<MaintenanceAssetDto> UpdateVehicleAsync(Guid vehicleAssetId, UpdateFleetVehicleDto dto)
    {
        if (vehicleAssetId == Guid.Empty) throw new ArgumentException("VehicleAssetId is required.");
        dto ??= new UpdateFleetVehicleDto();

        var existingEntity = await _unitOfWork.Repository<MaintenanceAsset>()
            .FirstOrDefaultAsync(a => a.Id == vehicleAssetId && a.TenantId == _currentUserProvider.TenantId, a => a.AssetCategory);

        if (existingEntity == null)
            throw new ArgumentException($"Vehicle with ID {vehicleAssetId} not found");

        if (!string.Equals(existingEntity.AssetCategory?.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Asset with ID {vehicleAssetId} is not a Vehicle.");

        var category = await _unitOfWork.Repository<MaintenanceAssetCategory>()
            .FirstOrDefaultAsync(c => c.Id == dto.AssetCategoryId && c.TenantId == _currentUserProvider.TenantId);

        if (category == null)
            throw new ArgumentException("Asset category not found.");

        if (!string.Equals(category.AssetType, "Vehicle", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected asset category is not a Vehicle category.");

        var updateAssetDto = new UpdateMaintenanceAssetDto
        {
            Name = dto.Name,
            Description = existingEntity.Description,
            AssetCategoryId = dto.AssetCategoryId,
            Manufacturer = dto.Manufacturer,
            Model = dto.Model,
            SerialNumber = dto.SerialNumber,
            PurchaseDate = existingEntity.PurchaseDate,
            PurchasePrice = existingEntity.PurchasePrice,
            CurrentValue = existingEntity.CurrentValue,
            Location = dto.Location,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? existingEntity.Status.ToString() : dto.Status,
            Criticality = existingEntity.Criticality.ToString(),
            WarrantyStartDate = existingEntity.WarrantyStartDate,
            WarrantyEndDate = existingEntity.WarrantyEndDate,
            WarrantyProvider = existingEntity.WarrantyProvider,
            ParentAssetId = existingEntity.ParentAssetId,
            Specifications = existingEntity.Specifications,
            DocumentLinks = existingEntity.DocumentLinks,
            Images = existingEntity.Images,
            LicensePlate = dto.LicensePlate,
            VIN = dto.Vin,
            FuelType = dto.FuelType
        };

        var updated = await _maintenanceAssetService.UpdateAssetAsync(vehicleAssetId, updateAssetDto);

        // Initial/override readings should be updated via the dedicated endpoints.
        if (dto.Mileage.HasValue)
        {
            await _maintenanceAssetService.UpdateAssetMileageAsync(vehicleAssetId, dto.Mileage.Value);
        }
        if (dto.OperatingHours.HasValue)
        {
            await _maintenanceAssetService.UpdateAssetOperatingHoursAsync(vehicleAssetId, dto.OperatingHours.Value);
        }

        return updated;
    }
}
