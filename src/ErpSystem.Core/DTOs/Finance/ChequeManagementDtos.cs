using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

#region Cheque DTOs

public class ChequeDto
{
    public Guid Id { get; set; }
    public string ChequeNumber { get; set; } = string.Empty;
    public Guid BankAccountId { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public string? PayeeName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GHS";
    public ChequeStatus Status { get; set; }
    public DateTime? PresentedDate { get; set; }
    public DateTime? ClearedDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public string? Memo { get; set; }
    public Guid? CashTransactionId { get; set; }
    public string? StatusReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateChequeDto
{
    public string ChequeNumber { get; set; } = string.Empty;
    public Guid BankAccountId { get; set; }
    public DateTime IssueDate { get; set; }
    public string? PayeeName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GHS";
    public string? Memo { get; set; }
}

public class UpdateChequeStatusDto
{
    public ChequeStatus Status { get; set; }
    public DateTime? StatusDate { get; set; }
    public string? StatusReason { get; set; }
}

public class VoidChequeDto
{
    public string Reason { get; set; } = string.Empty;
}

#endregion

#region PaymentMethod DTOs

public class PaymentMethodDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public PaymentMethodType Type { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresBankAccount { get; set; }
    public bool RequiresReference { get; set; }
    public Guid? DefaultGLAccountId { get; set; }
    public string? DefaultGLAccountNumber { get; set; }
    public string? DefaultGLAccountName { get; set; }
}

public class CreatePaymentMethodDto
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public PaymentMethodType Type { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresBankAccount { get; set; } = true;
    public bool RequiresReference { get; set; }
    public Guid? DefaultGLAccountId { get; set; }
}

#endregion
