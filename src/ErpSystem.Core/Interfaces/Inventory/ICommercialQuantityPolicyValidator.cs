namespace ErpSystem.Core.Interfaces.Inventory;

public sealed record CommercialQuantityEvidence(
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    int DecimalPlaces,
    decimal? RoundingIncrement,
    decimal Quantity);

/// <summary>Persisted snapshot carried by commercial transaction lines and copied into posting evidence.</summary>
public interface ICommercialQuantityEvidenceLine
{
    Guid? UnitOfMeasureId { get; set; }
    string? UnitOfMeasureCodeSnapshot { get; set; }
    int? UnitOfMeasureDecimalPlacesSnapshot { get; set; }
    decimal? UnitOfMeasureRoundingIncrementSnapshot { get; set; }
}

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
