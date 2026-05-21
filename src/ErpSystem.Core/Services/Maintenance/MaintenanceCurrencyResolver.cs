using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Maintenance;

internal static class MaintenanceCurrencyResolver
{
    private const string DefaultBaseCurrencyCode = "GHS";

    public static async Task<string> ResolveCurrencyCodeAsync(
        IUnitOfWork unitOfWork,
        Guid? tenantId,
        string? requestedCurrencyCode)
    {
        _ = requestedCurrencyCode;
        return await ResolveBaseCurrencyCodeAsync(unitOfWork, tenantId);
    }

    public static async Task<string> ResolveBaseCurrencyCodeAsync(IUnitOfWork unitOfWork, Guid? tenantId)
    {
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            return DefaultBaseCurrencyCode;
        }

        var currencyCode = await unitOfWork.Repository<Currency>()
            .GetQueryable(c => c.TenantId == tenantId.Value && c.IsBaseCurrency && !c.IsDeleted)
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => c.CurrencyCode)
            .FirstOrDefaultAsync();

        return NormalizeCurrencyCode(currencyCode) ?? DefaultBaseCurrencyCode;
    }

    public static string? NormalizeCurrencyCode(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        var normalized = currencyCode.Trim().ToUpperInvariant();
        return normalized.Length == 3 ? normalized : null;
    }
}
