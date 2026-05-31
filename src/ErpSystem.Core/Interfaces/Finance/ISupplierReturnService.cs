using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface ISupplierReturnService
{
    Task<SupplierReturn> CreateReturnAsync(CreateSupplierReturnDto dto);
    Task<SupplierReturn> GetByIdAsync(Guid id);
    Task<IEnumerable<SupplierReturn>> GetAllAsync();
    Task<SupplierReturn> ApproveReturnAsync(Guid id);
}
