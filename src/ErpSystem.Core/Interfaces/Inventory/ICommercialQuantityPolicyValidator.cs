namespace ErpSystem.Core.Interfaces.Inventory;

public sealed record CommercialQuantityEvidence(
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    int DecimalPlaces,
    decimal? RoundingIncrement,
    decimal Quantity);

/// <summary>Shared commercial boundary contract. Stable UOM ID is authoritative; code is legacy fallback only.</summary>
public interface ICommercialQuantityPolicyValidator
{
    Task<CommercialQuantityEvidence> ResolveAndValidateAsync(
        Guid? unitOfMeasureId,
        string? legacyUnitCode,
        decimal quantity,
        string boundary,
        CancellationToken cancellationToken = default);
}
