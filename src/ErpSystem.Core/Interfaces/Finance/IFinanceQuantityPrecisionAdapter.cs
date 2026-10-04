namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Additive Finance boundary for producers that carry a UOM code into a
/// Finance-owned quantity write or posting operation.
/// </summary>
public interface IFinanceQuantityPrecisionAdapter
{
    Task ValidateAsync(
        string? unitTypeCode,
        decimal quantity,
        string boundary,
        CancellationToken cancellationToken = default);
}
