using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IFleetVehicleService
{
    Task<PagedResult<FleetVehicleListDto>> GetVehiclesPagedAsync(int page, int pageSize, string? searchTerm = null, Guid? categoryId = null, string? assetType = null);
    Task<MaintenanceAssetDto?> GetVehicleByIdAsync(Guid vehicleAssetId);
    Task<MaintenanceAssetDto> CreateVehicleAsync(CreateFleetVehicleDto dto);
    Task<MaintenanceAssetDto> UpdateVehicleAsync(Guid vehicleAssetId, UpdateFleetVehicleDto dto);
}

public interface IFleetDriverDirectoryService
{
    Task<FleetDriverDirectoryDto> GetDriversPagedAsync(
        int page,
        int pageSize,
        string? searchTerm = null,
        string? licenseStatus = null,
        bool? assigned = null);
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

public interface IFleetTripDestinationService
{
    Task<IReadOnlyList<FleetTripDestinationDto>> GetAllAsync(bool activeOnly = false);
    Task<FleetTripDestinationDto?> GetByIdAsync(Guid id);
    Task<FleetTripDestinationDto> CreateAsync(CreateFleetTripDestinationDto dto);
    Task<FleetTripDestinationDto> UpdateAsync(Guid id, UpdateFleetTripDestinationDto dto);
    Task<bool> DeleteAsync(Guid id);
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

public interface IFleetComplianceTemplateService
{
    Task<IReadOnlyList<FleetComplianceTemplateDto>> GetTemplatesAsync(bool includeInactive = false);
    Task<FleetComplianceTemplateDto?> GetTemplateByIdAsync(Guid templateId);
    Task<FleetComplianceTemplateDto> CreateTemplateAsync(CreateFleetComplianceTemplateDto dto);
    Task<FleetComplianceTemplateDto> UpdateTemplateAsync(Guid templateId, UpdateFleetComplianceTemplateDto dto);
    Task<bool> DeleteTemplateAsync(Guid templateId);

    Task<FleetVehicleComplianceTemplateDto?> GetVehicleTemplateAsync(Guid vehicleAssetId);
    Task<FleetVehicleComplianceTemplateDto> ApplyTemplateToVehicleAsync(Guid vehicleAssetId, Guid templateId);
}

public interface IFleetFuelService
{
    Task<PagedResult<FleetFuelTransactionDto>> GetFuelTransactionsPagedAsync(Guid vehicleAssetId, int page, int pageSize);
    Task<FleetFuelTransactionDto?> GetByIdAsync(Guid id);
    Task<FleetFuelTransactionDto> CreateAsync(CreateFleetFuelTransactionDto dto);
    Task<FleetFuelTransactionDto> UpdateAsync(Guid id, UpdateFleetFuelTransactionDto dto);
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
    Task<IReadOnlyList<FleetTripInspectionDto>> GetAssetInspectionsAsync(Guid assetId, int take = 50);
    Task<FleetTripInspectionDto?> GetByIdAsync(Guid id);
    Task<FleetTripInspectionDto> StartAsync(StartFleetTripInspectionDto dto);
    Task<FleetTripInspectionDto> CompleteAsync(Guid inspectionId, CompleteFleetTripInspectionDto dto);
    Task<FleetTripInspectionDto> SubmitAssetInspectionAsync(Guid assetId, SubmitFleetAssetInspectionDto dto);
    Task<FleetTripInspectionDto> SubmitForApprovalAsync(Guid inspectionId);
    Task<FleetTripInspectionDto> ApproveAsync(Guid inspectionId, string? comments = null);
    Task<FleetTripInspectionDto> RejectAsync(Guid inspectionId, string? comments = null);
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

public interface IFleetIncidentService
{
    Task<PagedResult<FleetIncidentDto>> GetPagedAsync(int page, int pageSize, Guid? vehicleAssetId = null, string? status = null, string? searchTerm = null);
    Task<FleetIncidentDto?> GetByIdAsync(Guid id);
    Task<FleetIncidentDto> CreateAsync(CreateFleetIncidentDto dto);
    Task<FleetIncidentDto> UpdateAsync(Guid id, UpdateFleetIncidentDto dto);
    Task<Guid> CreateWorkOrderAsync(CreateWorkOrderFromFleetIncidentDto dto);
}

public interface IFleetTyreService
{
    Task<PagedResult<FleetTyreDto>> GetPagedAsync(Guid vehicleAssetId, int page, int pageSize);
    Task<FleetTyreDto?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<FleetTyreEventDto>> GetEventsAsync(Guid fleetTyreId);
    Task<FleetTyreDto> CreateAsync(CreateFleetTyreDto dto);
    Task<FleetTyreDto> UpdateAsync(Guid id, CreateFleetTyreDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IFleetBatteryService
{
    Task<PagedResult<FleetBatteryDto>> GetPagedAsync(Guid vehicleAssetId, int page, int pageSize);
    Task<FleetBatteryDto?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<FleetBatteryEventDto>> GetEventsAsync(Guid fleetBatteryId);
    Task<FleetBatteryKpisDto> GetKpisAsync(Guid vehicleAssetId, DateTime? asAtUtc = null);
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
    Task<PagedResult<FleetCostEntryDto>> GetPagedForTripAsync(Guid fleetTripId, int page, int pageSize, DateTime? fromUtc = null, DateTime? toUtc = null);
    Task<FleetCostEntryDto> CreateAsync(CreateFleetCostEntryDto dto);
    Task<bool> DeleteAsync(Guid id);
}

public interface IFleetReportsService
{
    Task<FleetCostSummaryDto> GetCostSummaryAsync(DateTime? fromUtc = null, DateTime? toUtc = null, int top = 10, Guid? vehicleAssetId = null);
    Task<FleetUtilizationSummaryDto> GetUtilizationSummaryAsync(DateTime? fromUtc = null, DateTime? toUtc = null, int top = 10, Guid? vehicleAssetId = null);
}

public interface IFleetHealthService
{
    Task<FleetHealthDto> GetHealthAsync();
}
