using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Inventory;

public sealed class CommercialQuantityPolicyValidator : ICommercialQuantityPolicyValidator
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;

    public CommercialQuantityPolicyValidator(IUnitOfWork unitOfWork, ICurrentUserProvider currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public Task<CommercialQuantityEvidence> ResolveAndValidateAsync(Guid? unitOfMeasureId, string? legacyUnitCode,
        decimal quantity, string boundary, CancellationToken cancellationToken = default) =>
        ResolveAndValidateAsync(_unitOfWork, _currentUser.TenantId, unitOfMeasureId, legacyUnitCode, quantity, boundary, cancellationToken);

    public static async Task<CommercialQuantityEvidence> ResolveAndValidateAsync(IUnitOfWork unitOfWork, Guid tenantId,
        Guid? unitOfMeasureId, string? legacyUnitCode, decimal quantity, string boundary, CancellationToken cancellationToken = default)
    {
        var normalizedCode = legacyUnitCode?.Trim().ToUpperInvariant();
        if (unitOfMeasureId is null && string.IsNullOrEmpty(normalizedCode))
            throw new InvalidOperationException($"{boundary} requires a stable Inventory UOM ID (or a legacy UOM code).");

        var query = unitOfWork.Repository<UnitOfMeasure>().GetQueryable(unit =>
            unit.TenantId == tenantId && unit.IsActive && !unit.IsDeleted);
        var candidates = unitOfMeasureId is not null
            ? await query.Where(unit => unit.Id == unitOfMeasureId).Take(2).ToListAsync(cancellationToken)
            : await query.Where(unit => unit.Code.ToUpper() == normalizedCode).Take(2).ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            throw new InvalidOperationException($"{boundary} cannot resolve the Inventory UOM for this tenant.");
        if (candidates.Count > 1)
            throw new InvalidOperationException($"{boundary} legacy UOM code '{normalizedCode}' is ambiguous; a stable Inventory UOM ID is required.");

        var unit = candidates[0];
        if (unitOfMeasureId is not null && !string.IsNullOrEmpty(normalizedCode) &&
            !string.Equals(unit.Code.Trim(), normalizedCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{boundary} UOM ID/code identity does not match.");

        try { CommercialQuantityPolicy.Validate(quantity, unit.DecimalPlaces, unit.RoundingIncrement); }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"{boundary} quantity {quantity} is invalid for Inventory UOM '{unit.Code}' ({unit.Id}): {exception.Message}", exception);
        }

        return new CommercialQuantityEvidence(unit.Id, unit.Code, unit.DecimalPlaces, unit.RoundingIncrement, quantity);
    }
}
