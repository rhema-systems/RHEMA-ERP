using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Services.Sales;

public static class SalesCommercialQuantityEvidence
{
    public static void CopyToSalesOrderRequest(
        ICommercialQuantityEvidenceLine source,
        CreateSalesOrderLineDto target)
    {
        target.UnitOfMeasureId = source.UnitOfMeasureId;
        target.UnitOfMeasureCodeSnapshot = source.UnitOfMeasureCodeSnapshot;
        target.UnitOfMeasureDecimalPlacesSnapshot = source.UnitOfMeasureDecimalPlacesSnapshot;
        target.UnitOfMeasureRoundingIncrementSnapshot = source.UnitOfMeasureRoundingIncrementSnapshot;
    }

    public static async Task ValidateAndFreezeAsync(
        ICommercialQuantityPolicyValidator validator,
        ICommercialQuantityEvidenceLine line,
        string? legacyUnitCode,
        decimal quantity,
        string boundary,
        CancellationToken cancellationToken = default)
    {
        var evidence = await validator.ResolveAndValidateAsync(
            line.UnitOfMeasureId,
            line.UnitOfMeasureCodeSnapshot ?? legacyUnitCode,
            quantity,
            boundary,
            cancellationToken);

        if (line.UnitOfMeasureCodeSnapshot != null &&
            (!string.Equals(line.UnitOfMeasureCodeSnapshot, evidence.UnitOfMeasureCode, StringComparison.OrdinalIgnoreCase) ||
             line.UnitOfMeasureDecimalPlacesSnapshot != evidence.DecimalPlaces ||
             line.UnitOfMeasureRoundingIncrementSnapshot != evidence.RoundingIncrement))
        {
            throw new InvalidOperationException($"{boundary}: retained unit-of-measure evidence no longer matches the resolved Inventory UOM.");
        }

        line.UnitOfMeasureId = evidence.UnitOfMeasureId;
        line.UnitOfMeasureCodeSnapshot = evidence.UnitOfMeasureCode;
        line.UnitOfMeasureDecimalPlacesSnapshot = evidence.DecimalPlaces;
        line.UnitOfMeasureRoundingIncrementSnapshot = evidence.RoundingIncrement;
    }
}
