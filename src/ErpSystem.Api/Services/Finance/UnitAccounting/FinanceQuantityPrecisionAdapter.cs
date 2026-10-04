using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Inventory;

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
        if (string.IsNullOrWhiteSpace(unitTypeCode))
            return;
        await CommercialQuantityPolicyValidator.ResolveAndValidateAsync(
            unitOfWork, tenantId, null, unitTypeCode, quantity, boundary, cancellationToken);
    }
}
