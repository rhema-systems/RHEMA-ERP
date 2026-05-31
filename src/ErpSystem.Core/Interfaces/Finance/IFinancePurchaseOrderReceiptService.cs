using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinancePurchaseOrderReceiptService
{
    Task<FinancePurchaseOrderReceipt> ReceiveAsync(FinancePurchaseOrderReceipt receipt);
    Task<FinancePurchaseOrderReceipt> ReceiveAsync(CreateFinancePurchaseReceiptDto dto);
    Task<FinancePurchaseOrderReceipt> GetByIdAsync(Guid id);
    Task<IEnumerable<FinancePurchaseOrderReceipt>> GetByPurchaseOrderIdAsync(Guid poId);
    Task<IEnumerable<FinancePurchaseOrderReceipt>> GetAllAsync();
    
    /// <summary>
    /// Converts a Finance PO Receipt (GRV) into a draft Vendor Invoice.
    /// Only processes quantities that haven't been invoiced yet.
    /// </summary>
    Task<VendorInvoice> ConvertToVendorInvoiceAsync(Guid receiptId, Guid currentUserId);
}

