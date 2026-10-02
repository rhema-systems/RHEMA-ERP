using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.UnitAccounting;

public sealed class FinanceQuantityPrecisionAdapter : IFinanceQuantityPrecisionAdapter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public FinanceQuantityPrecisionAdapter(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public Task ValidateAsync(
        string? unitTypeCode,
        decimal quantity,
        string boundary,
        CancellationToken cancellationToken = default) =>
        ValidateAsync(
            _unitOfWork,
            _currentUserService.GetRequiredFinanceTenantId(),
            unitTypeCode,
            quantity,
            boundary,
            cancellationToken);

    public static async Task ValidateAsync(
        IUnitOfWork unitOfWork,
        Guid tenantId,
        string? unitTypeCode,
        decimal quantity,
        string boundary,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = unitTypeCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizedCode))
            return;

        var policy = await unitOfWork.Repository<UnitType>()
            .GetQueryable(unitType =>
                unitType.TenantId == tenantId &&
                unitType.Code == normalizedCode &&
                unitType.IsActive &&
                !unitType.IsDeleted)
            .Select(unitType => new
            {
                unitType.Code,
                unitType.DecimalPlaces,
                unitType.RoundingIncrement
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Non-Finance UOM masters remain owned by their source module. Finance
        // enforces only codes explicitly governed by a Finance Unit Type.
        if (policy == null)
            return;

        try
        {
            PrecisionRoundingPolicy.ValidateQuantity(
                quantity,
                policy.DecimalPlaces,
                policy.RoundingIncrement);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"{boundary} quantity {quantity} is invalid for Unit Type '{policy.Code}': {exception.Message}",
                exception);
        }
    }
}
