using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ErpSystem.Api.Services
{
    /// <summary>
    /// Service for retrieving tenant-specific configuration settings.
    /// Provides centralized access to tenant preferences with fallback defaults.
    /// </summary>
    public class TenantSettingsService : ITenantSettingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public TenantSettingsService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<string> GetBaseCurrencyAsync()
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                return "GHS"; // Fallback for non-tenant context

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            return tenant?.BaseCurrency ?? "GHS";
        }

        public async Task<string> GetBaseCurrencyNameAsync()
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                return "Ghana Cedis";

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            return tenant?.BaseCurrencyName ?? "Ghana Cedis";
        }

        public async Task<string> GetCurrencySymbolAsync()
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                return "₵";

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            return tenant?.CurrencySymbol ?? "₵";
        }

        public async Task<int> GetCurrencyDecimalPlacesAsync()
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                return 2;

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            return tenant?.CurrencyDecimalPlaces ?? 2;
        }

        public async Task<string> GetCompanyNameAsync()
        {
            var tenantId = _currentUserService.TenantId;
            if (tenantId == null)
                return "RHEMA ERP";

            var tenant = await _context.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            return tenant?.Name ?? "RHEMA ERP";
        }
    }
}
