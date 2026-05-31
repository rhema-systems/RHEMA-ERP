using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinancePurchaseOrderService
{
    Task<FinancePurchaseOrder> CreateAsync(CreateFinancePurchaseOrderDto dto);
    Task<FinancePurchaseOrder> GetByIdAsync(Guid id);
    Task<IEnumerable<FinancePurchaseOrder>> GetAllAsync(Guid tenantId);
    Task ApproveAsync(Guid id);
}
