using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ErpSystem.Core.Interfaces.Inventory;

public class FinanceReceiptInventoryLine
{
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal QuantityReceived { get; set; }
    public string? Reference { get; set; }
}

public interface IInventoryReceiptService
{
    /// <summary>
    /// Processes inventory receipts generated from the Finance/AP module.
    /// This abstracts the inventory movement logic away from the Finance module.
    /// </summary>
    /// <param ref="tenantId">The current tenant ID.</param>
    /// <param name="financeGrvId">The ID of the Finance Goods Receipt Voucher.</param>
    /// <param name="lines">The list of inventory lines that were received.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessFinanceReceiptAsync(Guid tenantId, Guid financeGrvId, IEnumerable<FinanceReceiptInventoryLine> lines);
}
