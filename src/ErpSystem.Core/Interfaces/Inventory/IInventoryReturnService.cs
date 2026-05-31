using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryReturnService
{
    /// <summary>
    /// Processes inventory returns generated from the Finance/AP module.
    /// This abstracts the inventory return movement logic away from the Finance module.
    /// </summary>
    /// <param name="tenantId">The current tenant ID.</param>
    /// <param name="returnId">The ID of the Finance Supplier Return.</param>
    /// <param name="lines">The list of inventory lines that were returned.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessSupplierReturnAsync(Guid tenantId, Guid returnId, IEnumerable<FinanceReceiptInventoryLine> lines);

    /// <summary>
    /// Processes inventory returns generated from the Sales/AR module.
    /// This abstracts the inventory return movement logic away from the Sales module.
    /// </summary>
    /// <param name="tenantId">The current tenant ID.</param>
    /// <param name="returnId">The ID of the Customer Return.</param>
    /// <param name="lines">The list of inventory lines that were returned.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ProcessCustomerReturnAsync(Guid tenantId, Guid returnId, IEnumerable<FinanceReceiptInventoryLine> lines);
}

