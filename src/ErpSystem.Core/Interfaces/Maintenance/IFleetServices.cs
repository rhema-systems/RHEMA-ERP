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

