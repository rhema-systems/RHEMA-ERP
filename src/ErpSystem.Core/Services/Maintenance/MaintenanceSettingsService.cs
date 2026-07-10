using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

public class MaintenanceSettingsService : IMaintenanceSettingsService
{
    private readonly IMaintenanceSettingsRepository _settingsRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<MaintenanceSettingsService> _logger;

    public MaintenanceSettingsService(
        IMaintenanceSettingsRepository settingsRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<MaintenanceSettingsService> logger)
    {
        _settingsRepository = settingsRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<MaintenanceSettingsDto> GetSettingsAsync()
    {
        try
        {
            var settings = await _settingsRepository.GetOrCreateDefaultAsync(
                _currentUserProvider.TenantId,
                _currentUserProvider.UserId);

            return MapToDto(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance settings for tenant {TenantId}", _currentUserProvider.TenantId);
            throw;
        }
    }

    public async Task<MaintenanceSettingsDto> UpdateSettingsAsync(UpdateMaintenanceSettingsDto dto)
    {
        try
        {
            Validate(dto);

            var settings = await _settingsRepository.GetOrCreateDefaultAsync(
                _currentUserProvider.TenantId,
                _currentUserProvider.UserId);

            settings.FleetComplianceDueSoonDays = dto.FleetComplianceDueSoonDays;
            settings.BlockFleetDispatchWhenComplianceDueSoon = dto.BlockFleetDispatchWhenComplianceDueSoon;
            settings.RequirePredefinedFleetTripDestinationOnDispatch = dto.RequirePredefinedFleetTripDestinationOnDispatch;
            settings.DefaultFleetDefectWorkOrderTypeId = NormalizeGuid(dto.DefaultFleetDefectWorkOrderTypeId);
            settings.DefaultFleetDefectMaintenanceTypeId = NormalizeGuid(dto.DefaultFleetDefectMaintenanceTypeId);
            settings.DefaultFleetDefectPriorityLevelId = NormalizeGuid(dto.DefaultFleetDefectPriorityLevelId);
            settings.DefaultFleetDefectBillingType = NormalizeBillingType(dto.DefaultFleetDefectBillingType);
            settings.UpdatedAt = DateTime.UtcNow;

            await _settingsRepository.UpdateAsync(settings);
            await _unitOfWork.SaveChangesAsync();

            return MapToDto(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance settings for tenant {TenantId}", _currentUserProvider.TenantId);
            throw;
        }
    }

    private static void Validate(UpdateMaintenanceSettingsDto dto)
    {
        if (dto.FleetComplianceDueSoonDays < 0 || dto.FleetComplianceDueSoonDays > 3650)
        {
            throw new InvalidOperationException("Fleet compliance due soon days must be between 0 and 3650.");
        }
    }

    private static Guid? NormalizeGuid(Guid? value) => value.HasValue && value.Value != Guid.Empty ? value : null;

    private static string NormalizeBillingType(string? value) =>
        string.Equals(value?.Trim(), "Maintenance", StringComparison.OrdinalIgnoreCase) ? "Maintenance" : "Repairs";

    private static MaintenanceSettingsDto MapToDto(MaintenanceSettings settings)
    {
        return new MaintenanceSettingsDto
        {
            Id = settings.Id,
            TenantId = settings.TenantId,
            FleetComplianceDueSoonDays = settings.FleetComplianceDueSoonDays,
            BlockFleetDispatchWhenComplianceDueSoon = settings.BlockFleetDispatchWhenComplianceDueSoon,
            RequirePredefinedFleetTripDestinationOnDispatch = settings.RequirePredefinedFleetTripDestinationOnDispatch,
            DefaultFleetDefectWorkOrderTypeId = settings.DefaultFleetDefectWorkOrderTypeId,
            DefaultFleetDefectMaintenanceTypeId = settings.DefaultFleetDefectMaintenanceTypeId,
            DefaultFleetDefectPriorityLevelId = settings.DefaultFleetDefectPriorityLevelId,
            DefaultFleetDefectBillingType = NormalizeBillingType(settings.DefaultFleetDefectBillingType),
            CreatedAt = settings.CreatedAt,
            CreatedById = settings.CreatedById,
            UpdatedAt = settings.UpdatedAt
        };
    }
}
