using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Inventory;

public class InventoryReceiptService : IInventoryReceiptService
{
    private readonly ILogger<InventoryReceiptService> _logger;

    public InventoryReceiptService(ILogger<InventoryReceiptService> logger)
    {
        _logger = logger;
    }

    public Task ProcessFinanceReceiptAsync(Guid tenantId, Guid financeGrvId, IEnumerable<FinanceReceiptInventoryLine> lines)
    {
        // TODO: In a real implementation, this would insert records into the InventoryModule's stock movement tables
        // and adjust the physical/available quantities.
        // For this rehearsal, we log and return immediately to simulate the boundary integration without affecting existing Procurement/Inventory tables.
        
        _logger.LogInformation("Processing Finance GRV {GrvId} for Tenant {TenantId}. Lines count: {Count}", 
            financeGrvId, tenantId, lines.Count());
            
        return Task.CompletedTask;
    }
}
