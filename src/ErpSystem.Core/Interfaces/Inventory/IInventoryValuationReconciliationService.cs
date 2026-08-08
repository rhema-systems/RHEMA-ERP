using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryValuationReconciliationService
{
    Task<IReadOnlyList<InventoryValuationReconciliationDto>> GetAsync(
        Guid? fiscalPeriodId,
        InventoryValuationReconciliationStatus? status,
        int take,
        CancellationToken cancellationToken = default);
    Task<InventoryValuationReconciliationDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<InventoryValuationReconciliationDto> GenerateAsync(
        GenerateInventoryValuationReconciliationRequest request,
        CancellationToken cancellationToken = default);
    Task<InventoryValuationReconciliationDto> FreezeAsync(
        Guid id,
        FreezeInventoryValuationReconciliationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IInventoryValuationReconciliationReportSource
{
    Task<IReadOnlyList<InventoryValuationReconciliationDto>> GetReportSourceAsync(
        Guid? fiscalPeriodId,
        InventoryValuationReconciliationStatus? status,
        CancellationToken cancellationToken = default);
}

public class InventoryValuationReconciliationException : Exception
{
    public InventoryValuationReconciliationException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

public sealed class InventoryValuationReconciliationNotFoundException
    : InventoryValuationReconciliationException
{
    public InventoryValuationReconciliationNotFoundException(string message)
        : base("INV_VALUATION_RECONCILIATION_NOT_FOUND", message) { }
}
