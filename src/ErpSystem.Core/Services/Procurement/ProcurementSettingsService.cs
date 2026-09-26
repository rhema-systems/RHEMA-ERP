using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class ProcurementSettingsService : IProcurementSettingsService
{
    private readonly IProcurementSettingsRepository _settingsRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcurementSettingsService> _logger;

    public ProcurementSettingsService(
        IProcurementSettingsRepository settingsRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<ProcurementSettingsService> logger)
    {
        _settingsRepository = settingsRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ProcurementSettingsDto> GetSettingsAsync()
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
            _logger.LogError(ex, "Error retrieving procurement settings for tenant {TenantId}", _currentUserProvider.TenantId);
            throw;
        }
    }

    public async Task<ProcurementSettingsDto> UpdateSettingsAsync(UpdateProcurementSettingsDto dto)
    {
        try
        {
            // Validate settings
            ValidateSettings(dto);

            var settings = await _settingsRepository.GetOrCreateDefaultAsync(
                _currentUserProvider.TenantId,
                _currentUserProvider.UserId);

            var oldControls = new { settings.AutoCloseTenders, settings.EnforceSegregationOfDuties };
            // Update settings
            if (dto.EnforceSegregationOfDuties.HasValue) settings.EnforceSegregationOfDuties = dto.EnforceSegregationOfDuties.Value;
            if (dto.AutoCloseTenders.HasValue) settings.AutoCloseTenders = dto.AutoCloseTenders.Value;
            settings.AutoCreateInventoryItems = dto.AutoCreateInventoryItems;
            settings.AutoCreateSupplierItems = dto.AutoCreateSupplierItems;
            settings.AllowNonInventoryItems = dto.AllowNonInventoryItems;
            settings.DefaultItemCategoryId = dto.DefaultItemCategoryId;
            settings.DefaultUnitOfMeasureId = dto.DefaultUnitOfMeasureId;
            settings.DefaultValuationMethod = dto.DefaultValuationMethod;
            settings.PurchaseRequisitionNumberFormat = dto.PurchaseRequisitionNumberFormat;
            settings.PurchaseOrderNumberFormat = dto.PurchaseOrderNumberFormat;
            settings.PurchaseOrderReceiptNumberFormat = dto.PurchaseOrderReceiptNumberFormat;
            settings.RequireApprovalForPO = dto.RequireApprovalForPO;
            settings.AutoApprovalThreshold = dto.AutoApprovalThreshold;
            settings.AllowBackorders = dto.AllowBackorders;
            settings.RequireDeliveryDate = dto.RequireDeliveryDate;
            settings.EnforceSupplierCatalog = dto.EnforceSupplierCatalog;
            settings.AllowMultipleSuppliersPerItem = dto.AllowMultipleSuppliersPerItem;
            settings.ValidateBudgetBeforePO = dto.ValidateBudgetBeforePO;
            settings.RequireContractForPO = dto.RequireContractForPO;
            settings.Notes = dto.Notes;
            settings.UpdatedAt = DateTime.UtcNow;

            if (oldControls.AutoCloseTenders != settings.AutoCloseTenders ||
                oldControls.EnforceSegregationOfDuties != settings.EnforceSegregationOfDuties)
            {
                await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
                {
                    TenantId = _currentUserProvider.TenantId,
                    UserId = _currentUserProvider.UserId,
                    Username = _currentUserProvider.Username,
                    Action = "PROCUREMENT_CONTROLS_CHANGED",
                    Resource = "ProcurementSettings",
                    ResourceId = settings.Id.ToString(),
                    OldValues = JsonSerializer.Serialize(oldControls),
                    NewValues = JsonSerializer.Serialize(new { settings.AutoCloseTenders, settings.EnforceSegregationOfDuties }),
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "Unknown",
                    CreatedBy = _currentUserProvider.FullName,
                    CreatedById = _currentUserProvider.UserId
                });
            }
            await _settingsRepository.UpdateAsync(settings);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated procurement settings for tenant {TenantId}", _currentUserProvider.TenantId);

            return MapToDto(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating procurement settings for tenant {TenantId}", _currentUserProvider.TenantId);
            throw;
        }
    }

    public async Task<bool> ShouldAutoCreateInventoryItemsAsync()
    {
        try
        {
            var settings = await _settingsRepository.GetOrCreateDefaultAsync(
                _currentUserProvider.TenantId,
                _currentUserProvider.UserId);

            return settings.AutoCreateInventoryItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking auto-create inventory items setting");
            return false; // Default to false on error
        }
    }

    public async Task<bool> ShouldAutoCreateSupplierItemsAsync()
    {
        try
        {
            var settings = await _settingsRepository.GetOrCreateDefaultAsync(
                _currentUserProvider.TenantId,
                _currentUserProvider.UserId);

            return settings.AutoCreateSupplierItems && settings.AutoCreateInventoryItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking auto-create supplier items setting");
            return false; // Default to false on error
        }
    }

    public async Task<bool> AllowNonInventoryItemsAsync()
    {
        try
        {
            var settings = await _settingsRepository.GetOrCreateDefaultAsync(
                _currentUserProvider.TenantId,
                _currentUserProvider.UserId);

            return settings.AllowNonInventoryItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking allow non-inventory items setting");
            return true; // Default to true on error for backward compatibility
        }
    }

    private void ValidateSettings(UpdateProcurementSettingsDto dto)
    {
        // AutoCreateSupplierItems can only be enabled if AutoCreateInventoryItems is also enabled
        if (dto.AutoCreateSupplierItems && !dto.AutoCreateInventoryItems)
        {
            throw new InvalidOperationException(
                "Auto-create supplier items can only be enabled when auto-create inventory items is also enabled");
        }

        // If AllowNonInventoryItems is disabled, AutoCreateInventoryItems should be enabled
        if (!dto.AllowNonInventoryItems && !dto.AutoCreateInventoryItems)
        {
            throw new InvalidOperationException(
                "When non-inventory items are not allowed, auto-create inventory items must be enabled");
        }

        // AutoApprovalThreshold must be positive if set
        if (dto.AutoApprovalThreshold.HasValue && dto.AutoApprovalThreshold.Value <= 0)
        {
            throw new InvalidOperationException("Auto-approval threshold must be a positive value");
        }

        // DefaultValuationMethod must be valid
        if (!string.IsNullOrEmpty(dto.DefaultValuationMethod))
        {
            var validMethods = new[] { "FIFO", "WAC", "Standard", "LIFO" };
            if (!validMethods.Contains(dto.DefaultValuationMethod))
            {
                throw new InvalidOperationException(
                    $"Invalid valuation method. Must be one of: {string.Join(", ", validMethods)}");
            }
        }
    }

    private static ProcurementSettingsDto MapToDto(ProcurementSettings settings)
    {
        return new ProcurementSettingsDto
        {
            Id = settings.Id,
            TenantId = settings.TenantId,
            AutoCloseTenders = settings.AutoCloseTenders,
            EnforceSegregationOfDuties = settings.EnforceSegregationOfDuties,
            AutoCreateInventoryItems = settings.AutoCreateInventoryItems,
            AutoCreateSupplierItems = settings.AutoCreateSupplierItems,
            AllowNonInventoryItems = settings.AllowNonInventoryItems,
            DefaultItemCategoryId = settings.DefaultItemCategoryId,
            DefaultUnitOfMeasureId = settings.DefaultUnitOfMeasureId,
            DefaultValuationMethod = settings.DefaultValuationMethod,
            PurchaseRequisitionNumberFormat = settings.PurchaseRequisitionNumberFormat,
            PurchaseOrderNumberFormat = settings.PurchaseOrderNumberFormat,
            PurchaseOrderReceiptNumberFormat = settings.PurchaseOrderReceiptNumberFormat,
            RequireApprovalForPO = settings.RequireApprovalForPO,
            AutoApprovalThreshold = settings.AutoApprovalThreshold,
            AllowBackorders = settings.AllowBackorders,
            RequireDeliveryDate = settings.RequireDeliveryDate,
            EnforceSupplierCatalog = settings.EnforceSupplierCatalog,
            AllowMultipleSuppliersPerItem = settings.AllowMultipleSuppliersPerItem,
            ValidateBudgetBeforePO = settings.ValidateBudgetBeforePO,
            RequireContractForPO = settings.RequireContractForPO,
            Notes = settings.Notes,
            CreatedAt = settings.CreatedAt,
            CreatedById = settings.CreatedById,
            UpdatedAt = settings.UpdatedAt
        };
    }
}
