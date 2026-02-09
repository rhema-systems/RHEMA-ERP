using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IFleetVehicleService
{
    Task<PagedResult<FleetVehicleListDto>> GetVehiclesPagedAsync(int page, int pageSize, string? searchTerm = null, Guid? categoryId = null);
    Task<MaintenanceAssetDto?> GetVehicleByIdAsync(Guid vehicleAssetId);
    Task<MaintenanceAssetDto> CreateVehicleAsync(CreateFleetVehicleDto dto);
    Task<MaintenanceAssetDto> UpdateVehicleAsync(Guid vehicleAssetId, UpdateFleetVehicleDto dto);
}

public interface IFleetTripService
{
    Task<PagedResult<FleetTripDto>> GetTripsPagedAsync(int page, int pageSize, string? searchTerm = null, string? status = null, Guid? vehicleAssetId = null);
    Task<FleetTripDto?> GetTripByIdAsync(Guid tripId);
    Task<FleetTripDto> CreateTripAsync(CreateFleetTripDto dto);
    Task<FleetTripDto> UpdateTripAsync(Guid tripId, UpdateFleetTripDto dto);
    Task<bool> SubmitForApprovalAsync(Guid tripId);
    Task<bool> ApproveAsync(Guid tripId, string? comments = null);
    Task<bool> RejectAsync(Guid tripId, string reason, string? comments = null);
    Task<FleetTripDto> DispatchAsync(Guid tripId, DispatchFleetTripDto dto);
    Task<FleetTripDto> CompleteAsync(Guid tripId, CompleteFleetTripDto dto);
    Task<bool> CancelAsync(Guid tripId, string reason);
}

public interface IFleetComplianceService
{
    Task<PagedResult<FleetComplianceItemDto>> GetComplianceItemsPagedAsync(Guid vehicleAssetId, int page, int pageSize);
    Task<FleetComplianceItemDto?> GetByIdAsync(Guid id);
    Task<FleetComplianceItemDto> CreateAsync(CreateFleetComplianceItemDto dto);
    Task<FleetComplianceItemDto> UpdateAsync(Guid id, UpdateFleetComplianceItemDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<IReadOnlyList<FleetComplianceItemDto>> GetDispatchBlockingItemsAsync(Guid vehicleAssetId, DateTime? asAtUtc = null);
}

public interface IFleetFuelService
{
    Task<PagedResult<FleetFuelTransactionDto>> GetFuelTransactionsPagedAsync(Guid vehicleAssetId, int page, int pageSize);
    Task<FleetFuelTransactionDto?> GetByIdAsync(Guid id);
    Task<FleetFuelTransactionDto> CreateAsync(CreateFleetFuelTransactionDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IFleetAssignmentService
{
    Task<IReadOnlyList<FleetVehicleAssignmentDto>> GetAssignmentsAsync(Guid vehicleAssetId);
    Task<FleetVehicleAssignmentDto?> GetCurrentAssignmentAsync(Guid vehicleAssetId);
    Task<FleetVehicleAssignmentDto> AssignDriverAsync(AssignFleetDriverDto dto);
    Task<bool> EndAssignmentAsync(Guid assignmentId, EndFleetDriverAssignmentDto dto);
}

public interface IFleetInspectionService
{
    Task<IReadOnlyList<FleetTripInspectionDto>> GetTripInspectionsAsync(Guid tripId);
    Task<FleetTripInspectionDto?> GetByIdAsync(Guid id);
    Task<FleetTripInspectionDto> StartAsync(StartFleetTripInspectionDto dto);
    Task<FleetTripInspectionDto> CompleteAsync(Guid inspectionId, CompleteFleetTripInspectionDto dto);
    Task<bool> CancelAsync(Guid inspectionId, string? notes = null);
}

public interface IFleetDefectService
{
    Task<PagedResult<FleetDefectDto>> GetDefectsPagedAsync(int page, int pageSize, Guid? vehicleAssetId = null, string? status = null, string? searchTerm = null);
    Task<FleetDefectDto?> GetByIdAsync(Guid id);
    Task<FleetDefectDto> CreateAsync(CreateFleetDefectDto dto);
    Task<FleetDefectDto> UpdateStatusAsync(Guid id, UpdateFleetDefectStatusDto dto);
    Task<Guid> CreateWorkOrderAsync(CreateWorkOrderFromFleetDefectDto dto);
}

public interface IFleetTyreService
{
    Task<PagedResult<FleetTyreDto>> GetPagedAsync(Guid vehicleAssetId, int page, int pageSize);
    Task<FleetTyreDto?> GetByIdAsync(Guid id);
    Task<FleetTyreDto> CreateAsync(CreateFleetTyreDto dto);
    Task<FleetTyreDto> UpdateAsync(Guid id, CreateFleetTyreDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IFleetBatteryService
{
    Task<PagedResult<FleetBatteryDto>> GetPagedAsync(Guid vehicleAssetId, int page, int pageSize);
    Task<FleetBatteryDto?> GetByIdAsync(Guid id);
    Task<FleetBatteryDto> CreateAsync(CreateFleetBatteryDto dto);
    Task<FleetBatteryDto> UpdateAsync(Guid id, CreateFleetBatteryDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IFleetExternalRepairService
{
    Task<PagedResult<FleetExternalRepairDto>> GetPagedAsync(int page, int pageSize, Guid? vehicleAssetId = null, string? status = null);
    Task<FleetExternalRepairDto?> GetByIdAsync(Guid id);
    Task<FleetExternalRepairDto> CreateAsync(CreateFleetExternalRepairDto dto);
    Task<FleetExternalRepairDto> UpdateStatusAsync(Guid id, UpdateFleetExternalRepairStatusDto dto);
}

public interface IFleetDashboardService
{
    Task<FleetDashboardSummaryDto> GetSummaryAsync(Guid? vehicleAssetId = null, DateTime? asAtUtc = null);
}

public interface IFleetCostService
{
    Task<PagedResult<FleetCostEntryDto>> GetPagedAsync(Guid vehicleAssetId, int page, int pageSize, DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<FleetCostEntryDto> CreateAsync(CreateFleetCostEntryDto dto);
    Task<bool> DeleteAsync(Guid id);
}

