using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Finance.Settings
{
    public class FinanceSettingsService : IFinanceSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public FinanceSettingsService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<FinanceSettingsDto> GetSettingsAsync()
        {
            var tenantId = _currentUserService.TenantId 
                ?? throw new InvalidOperationException("Tenant context is required.");

            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            // Create default settings if none exist
            if (settings == null)
            {
                settings = new FinanceSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CoaType = "Segmented",
                    CoaConfigurationLocked = false,
                    BaseCurrency = "GHS"
                };

                _context.FinanceSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return MapToDto(settings);
        }

        public async Task<FinanceSettingsDto> UpdateSettingsAsync(UpdateFinanceSettingsDto dto)
        {
            var tenantId = _currentUserService.TenantId 
                ?? throw new InvalidOperationException("Tenant context is required.");

            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (settings == null)
            {
                // Create if doesn't exist
                settings = new FinanceSettings
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId
                };
                _context.FinanceSettings.Add(settings);
            }

            // Check if COA type can be changed
            if (dto.CoaType != null && dto.CoaType != settings.CoaType)
            {
                if (settings.CoaConfigurationLocked)
                {
                    throw new InvalidOperationException(
                        "Cannot change COA type. Segmented structure is enforced.");
                }

                if (dto.CoaType == "Standard")
                {
                    throw new InvalidOperationException(
                        "Standard Chart of Accounts structure is no longer supported. Please use Segmented.");
                }

                // Check if any accounts exist
                var accountsExist = await _context.Accounts.AnyAsync(a => a.TenantId == tenantId);
                if (accountsExist)
                {
                    // Lock the configuration
                    settings.CoaConfigurationLocked = true;
                    throw new InvalidOperationException(
                        "Cannot change COA type after accounts have been created.");
                }

                settings.CoaType = dto.CoaType;
            }

            // Update other settings
            if (dto.BaseCurrency != null)
                settings.BaseCurrency = dto.BaseCurrency;

            if (dto.AccountSeparator != null)
                settings.AccountSeparator = dto.AccountSeparator;

            if (dto.RetainedEarningsAccountId.HasValue)
                settings.RetainedEarningsAccountId = dto.RetainedEarningsAccountId;

            if (dto.UnrealizedGainLossAccountId.HasValue)
                settings.UnrealizedGainLossAccountId = dto.UnrealizedGainLossAccountId;

            if (dto.RealizedGainLossAccountId.HasValue)
                settings.RealizedGainLossAccountId = dto.RealizedGainLossAccountId;

            if (dto.SuspenseAccountId.HasValue)
                settings.SuspenseAccountId = dto.SuspenseAccountId;

            if (dto.ControlAccountArId.HasValue) settings.ControlAccountArId = dto.ControlAccountArId;
            if (dto.ControlAccountApId.HasValue) settings.ControlAccountApId = dto.ControlAccountApId;
            if (dto.ControlAccountInventoryId.HasValue) settings.ControlAccountInventoryId = dto.ControlAccountInventoryId;
            if (dto.ControlAccountPayrollId.HasValue) settings.ControlAccountPayrollId = dto.ControlAccountPayrollId;
            if (dto.ControlAccountTaxId.HasValue) settings.ControlAccountTaxId = dto.ControlAccountTaxId;

            await _context.SaveChangesAsync();

            return MapToDto(settings);
        }

        public async Task<bool> CanChangeCOATypeAsync()
        {
            var tenantId = _currentUserService.TenantId 
                ?? throw new InvalidOperationException("Tenant context is required.");

            var settings = await _context.FinanceSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (settings?.CoaConfigurationLocked == true)
                return false;

            var accountsExist = await _context.Accounts.AnyAsync(a => a.TenantId == tenantId);
            return !accountsExist;
        }

        private FinanceSettingsDto MapToDto(FinanceSettings settings)
        {
            return new FinanceSettingsDto
            {
                Id = settings.Id,
                TenantId = settings.TenantId,
                CoaType = settings.CoaType,
                CoaConfigurationLocked = settings.CoaConfigurationLocked,
                BaseCurrency = settings.BaseCurrency,
                AccountSeparator = settings.AccountSeparator,
                RetainedEarningsAccountId = settings.RetainedEarningsAccountId,
                UnrealizedGainLossAccountId = settings.UnrealizedGainLossAccountId,
                RealizedGainLossAccountId = settings.RealizedGainLossAccountId,
                SuspenseAccountId = settings.SuspenseAccountId,
                ControlAccountArId = settings.ControlAccountArId,
                ControlAccountApId = settings.ControlAccountApId,
                ControlAccountInventoryId = settings.ControlAccountInventoryId,
                ControlAccountPayrollId = settings.ControlAccountPayrollId,
                ControlAccountTaxId = settings.ControlAccountTaxId
            };
        }
    }
}
