namespace ErpSystem.Core.Interfaces.Finance;

public interface IInventorySupplierReturnFinanceHandoff
{
    /// <summary>Posts only an unambiguous invoiced return with configured Finance accounts. Unsupported sources remain pending.</summary>
    Task<bool> TryRecordDispatchAsync(Guid inventoryReturnId, CancellationToken cancellationToken = default);
}
