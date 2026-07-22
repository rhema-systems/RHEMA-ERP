using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Sales;

// ═════════════════════════════════════════════
//  RETURN ORDER DTOs
// ═════════════════════════════════════════════

public class ReturnOrderSummaryDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public ReturnOrderStatus ReturnStatus { get; set; }
    public ReturnReasonCode ReasonCode { get; set; }
    public string? CustomerName { get; set; }
    public string? SalesOrderNumber { get; set; }
    public decimal TotalAmount { get; set; }
    public int LineCount { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ReturnOrderDetailDto : ReturnOrderSummaryDto
{
    public Guid SalesOrderId { get; set; }
    public Guid? DeliveryNoteId { get; set; }
    public Guid CustomerId { get; set; }
    public string? ReasonDescription { get; set; }
    public DateTime? InspectedDate { get; set; }
    public string? InspectedByName { get; set; }
    public string? InspectionNotes { get; set; }
    public Guid? CreditNoteId { get; set; }
    public string? CreditNoteNumber { get; set; }
    public Guid? RefundId { get; set; }
    public List<ReturnOrderLineDto> Lines { get; set; } = new();
}

public class ReturnOrderLineDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public ReturnReasonCode ReasonCode { get; set; }
    public string? Condition { get; set; }
    public bool IsRestockable { get; set; }
}

public class CreateReturnOrderDto
{
    [Required]
    public Guid SalesOrderId { get; set; }
    public Guid? DeliveryNoteId { get; set; }
    [Required]
    public Guid CustomerId { get; set; }
    public ReturnReasonCode ReasonCode { get; set; }
    public string? ReasonDescription { get; set; }
    [Required]
    public List<CreateReturnOrderLineDto> Lines { get; set; } = new();
}

public class CreateReturnOrderLineDto
{
    public Guid? SalesOrderLineId { get; set; }
    [Required]
    public string Description { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public ReturnReasonCode ReasonCode { get; set; }
    public string? Condition { get; set; }
    public bool IsRestockable { get; set; } = true;
}

// ═════════════════════════════════════════════
//  CREDIT NOTE DTOs
// ═════════════════════════════════════════════

public class CreditNoteSummaryDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public CreditNoteStatus CreditNoteStatus { get; set; }
    public string? CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Reason { get; set; }
    public DateTime? AppliedDate { get; set; }
    public int LineCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreditNoteDetailDto : CreditNoteSummaryDto
{
    public Guid CustomerId { get; set; }
    public Guid? ReturnOrderId { get; set; }
    public string? ReturnOrderNumber { get; set; }
    public Guid? OriginalInvoiceId { get; set; }
    public Guid? AppliedToInvoiceId { get; set; }
    public decimal TaxAmount { get; set; }
    public List<CreditNoteLineDto> Lines { get; set; } = new();
}

public class CreditNoteLineDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public string? TaxCode { get; set; }
}

public class CreateCreditNoteDto
{
    [Required]
    public Guid CustomerId { get; set; }
    public Guid? ReturnOrderId { get; set; }
    public Guid? OriginalInvoiceId { get; set; }
    public string? Reason { get; set; }
    [Required]
    public List<CreateCreditNoteLineDto> Lines { get; set; } = new();
}

public class CreateCreditNoteLineDto
{
    [Required]
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public string? TaxCode { get; set; }
}

public class CreditNoteApprovalDto
{
    public bool IsApproved { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}

// ═════════════════════════════════════════════
//  REFUND DTOs
// ═════════════════════════════════════════════

public class RefundSummaryDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public RefundStatus RefundStatus { get; set; }
    public string? CustomerName { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundMethod { get; set; } = string.Empty;
    public DateTime? ProcessedDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RefundDetailDto : RefundSummaryDto
{
    public Guid CustomerId { get; set; }
    public Guid? CreditNoteId { get; set; }
    public string? CreditNoteNumber { get; set; }
    public Guid? ReturnOrderId { get; set; }
    public string? Reason { get; set; }
    public string? ProcessedByName { get; set; }
    public string? PaymentReference { get; set; }
}

public class CreateRefundDto
{
    [Required]
    public Guid CustomerId { get; set; }
    public Guid? CreditNoteId { get; set; }
    public Guid? ReturnOrderId { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundMethod { get; set; } = "BankTransfer";
    public string? Reason { get; set; }
}

public class RefundApprovalDto
{
    public bool IsApproved { get; set; }

    [MaxLength(1000)]
    public string? Comments { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }
}
