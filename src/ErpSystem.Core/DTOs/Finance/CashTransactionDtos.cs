using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

#region CashTransaction DTOs

public class CashTransactionDto
{
    public Guid Id { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public CashTransactionType TransactionType { get; set; }
    public Guid BankAccountId { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public Guid? ToBankAccountId { get; set; }
    public string? ToBankAccountName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? ExchangeRate { get; set; }
    public decimal BaseAmount { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? PayeeOrPayer { get; set; }
    public string? Description { get; set; }
    public Guid? GLAccountId { get; set; }
    public string? GLAccountNumber { get; set; }
    public string? GLAccountName { get; set; }
    public bool IsReconciled { get; set; }
    public Guid? ReconciliationId { get; set; }
    public Guid? ChequeId { get; set; }
    public string? ChequeNumber { get; set; }
    public bool IsPosted { get; set; }
    public CashTransactionApprovalStatus ApprovalStatus { get; set; }
    public string ApprovalStatusName { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? RejectedAt { get; set; }
    public Guid? RejectedById { get; set; }
    public string? ApprovalComments { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime? PostedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateCashReceiptDto
{
    public DateTime TransactionDate { get; set; }
    public Guid BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? ExchangeRate { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? PayerName { get; set; }
    public string? Description { get; set; }
    public Guid? GLAccountId { get; set; }
}

public class CreateCashPaymentDto
{
    public DateTime TransactionDate { get; set; }
    public Guid BankAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? ExchangeRate { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? PayeeName { get; set; }
    public string? Description { get; set; }
    public Guid? GLAccountId { get; set; }
    public Guid? ChequeId { get; set; }
}

public class CreateBankTransferDto
{
    public DateTime TransactionDate { get; set; }
    public Guid FromBankAccountId { get; set; }
    public Guid ToBankAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
}

public class CashTransactionWorkflowActionDto
{
    public string? Comments { get; set; }
    public string? Reason { get; set; }
}

#endregion
