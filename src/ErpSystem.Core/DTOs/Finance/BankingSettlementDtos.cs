using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

public class LiquidityAccountDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LiquidityAccountType AccountType { get; set; }
    public string Currency { get; set; } = "GHS";
    public Guid GLAccountId { get; set; }
    public string GLAccountNumber { get; set; } = string.Empty;
    public string GLAccountName { get; set; } = string.Empty;
    public Guid? BankAccountId { get; set; }
    public string? BankAccountName { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderAccountReference { get; set; }
    public bool AllowsNegativeBalance { get; set; }
    public bool AllowsManualAllocations { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemAccount { get; set; }
    public string? Notes { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal AvailableToSettle { get; set; }
    public int OpenEntryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public class CreateLiquidityAccountDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public LiquidityAccountType AccountType { get; set; }
    public string Currency { get; set; } = "GHS";
    public Guid GLAccountId { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderAccountReference { get; set; }
    public bool AllowsNegativeBalance { get; set; }
    public bool AllowsManualAllocations { get; set; } = true;
    public string? Notes { get; set; }
}

public class UpdateLiquidityAccountDto
{
    public string Name { get; set; } = string.Empty;
    public Guid GLAccountId { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderAccountReference { get; set; }
    public bool AllowsNegativeBalance { get; set; }
    public bool AllowsManualAllocations { get; set; } = true;
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public class LiquidityAccountEntryDto
{
    public Guid Id { get; set; }
    public Guid LiquidityAccountId { get; set; }
    public string LiquidityAccountName { get; set; } = string.Empty;
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public LiquidityEntryType EntryType { get; set; }
    public LiquidityEntryDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "GHS";
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? CounterpartyName { get; set; }
    public string? Description { get; set; }
    public bool IsReversed { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

/// <summary>
/// A posted GL credit to a liquidity control account that has not yet been registered
/// in the operational banking queue.
/// </summary>
public class PostedLiquidityPaymentCandidateDto
{
    public Guid AccountTransactionId { get; set; }
    public Guid JournalEntryId { get; set; }
    public string JournalEntryNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public Guid LiquidityAccountId { get; set; }
    public string LiquidityAccountName { get; set; } = string.Empty;
    public string Currency { get; set; } = "GHS";
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
}

public class RegisterPostedLiquidityPaymentDto
{
    public Guid AccountTransactionId { get; set; }
    /// <summary>
    /// Optional when more than one operational liquidity account shares the posted GL control
    /// account (for example, undeposited cash and a named physical till).
    /// </summary>
    public Guid? LiquidityAccountId { get; set; }
    public LiquidityEntryType EntryType { get; set; } = LiquidityEntryType.OtherPayment;
}

public class BankDepositAllocationRequestDto
{
    public Guid LiquidityAccountEntryId { get; set; }
    public BankDepositAllocationType AllocationType { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public class CreateBankDepositDto
{
    public Guid BankAccountId { get; set; }
    public DateTime DepositDate { get; set; }
    public string DepositReference { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<BankDepositAllocationRequestDto> Allocations { get; set; } = new();
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class UpdateBankDepositDto : CreateBankDepositDto
{
    public string RowVersion { get; set; } = string.Empty;
}

/// <summary>
/// Records the bank's acknowledgement of a deposit that has already passed approval and posting.
/// The reference is mandatory; a controlled file is optional because some banks confirm directly
/// on the original primary deposit slip while others issue a separate advice.
/// </summary>
public class ConfirmBankDepositDto
{
    public string BankConfirmationReference { get; set; } = string.Empty;
    public DateTime BankConfirmationDate { get; set; }
    public Guid? ConfirmationEvidenceFileId { get; set; }
    public string? Notes { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public class BankDepositAllocationDto
{
    public Guid Id { get; set; }
    public Guid LiquidityAccountEntryId { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string LiquidityAccountName { get; set; } = string.Empty;
    public Guid GLAccountId { get; set; }
    public LiquidityEntryType EntryType { get; set; }
    public BankDepositAllocationType AllocationType { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? CounterpartyName { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
}

public class BankingAttachmentDto
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public string DocumentType { get; set; } = "Other";
    public bool IsPrimaryEvidence { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
}

public class LinkBankingAttachmentDto
{
    public Guid FileUploadRecordId { get; set; }
    public string DocumentType { get; set; } = "Other";
    public bool IsPrimaryEvidence { get; set; }
}

public class BankDepositDto
{
    public Guid Id { get; set; }
    public string DepositNumber { get; set; } = string.Empty;
    public Guid BankAccountId { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public Guid? BankGLAccountId { get; set; }
    public DateTime DepositDate { get; set; }
    public string DepositReference { get; set; } = string.Empty;
    public string Currency { get; set; } = "GHS";
    public BankDepositStatus Status { get; set; }
    public DepositPolicy PolicySnapshot { get; set; }
    public decimal TotalReceipts { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetAmount { get; set; }
    public string? Notes { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? CashTransactionId { get; set; }
    public BankDepositConfirmationStatus ConfirmationStatus { get; set; }
    public string? BankConfirmationReference { get; set; }
    public DateTime? BankConfirmationDate { get; set; }
    public DateTime? BankConfirmedAt { get; set; }
    public Guid? BankConfirmedById { get; set; }
    public string? BankConfirmationNotes { get; set; }
    public BankingAttachmentDto? BankConfirmationEvidence { get; set; }
    /// <summary>
    /// Reconciliation facts are projected from the canonical bank-facing cash transaction and
    /// its reconciliation rather than persisted again on the deposit batch.
    /// </summary>
    public bool IsReconciled { get; set; }
    public Guid? BankReconciliationId { get; set; }
    public ReconciliationStatus? ReconciliationStatus { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public DateTime? ReconciliationApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }
    public IReadOnlyList<BankDepositAllocationDto> Allocations { get; set; } = Array.Empty<BankDepositAllocationDto>();
    public IReadOnlyList<BankingAttachmentDto> Attachments { get; set; } = Array.Empty<BankingAttachmentDto>();
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
    public IReadOnlyList<FinanceSettlementDimensionComponentDto> SettlementDimensionEvidence { get; set; } =
        Array.Empty<FinanceSettlementDimensionComponentDto>();
    public string RowVersion { get; set; } = string.Empty;
}

public class BankingWorkflowActionDto
{
    public string? Comments { get; set; }
    public string? Reason { get; set; }
}

public class BankingSetupStatusDto
{
    public bool IsConfigured { get; set; }
    public bool RequiresProvisioningWizard { get; set; }
    public string CoaType { get; set; } = "Segmented";
    public string BaseCurrency { get; set; } = "GHS";
    public DepositPolicy DepositPolicy { get; set; }
    public bool RequirePrimaryEvidence { get; set; }
    public IReadOnlyList<LiquidityAccountType> MissingAccountTypes { get; set; } = Array.Empty<LiquidityAccountType>();
    public IReadOnlyList<LiquidityAccountDto> Accounts { get; set; } = Array.Empty<LiquidityAccountDto>();
}

public class LiquidityAccountProvisioningLineDto
{
    public LiquidityAccountType AccountType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "GHS";
    public Guid GLAccountId { get; set; }
}

public class CompleteBankingSetupDto
{
    public DepositPolicy DepositPolicy { get; set; } = DepositPolicy.DepositIntact;
    public bool RequirePrimaryEvidence { get; set; } = true;
    public List<LiquidityAccountProvisioningLineDto> Accounts { get; set; } = new();
}

public class CreateReturnedChequeCaseDto
{
    public Guid CustomerPaymentId { get; set; }
    public Guid? BankDepositBatchId { get; set; }
    public Guid BankAccountId { get; set; }
    public DateTime ReturnDate { get; set; }
    public string BankReference { get; set; } = string.Empty;
    public string ReturnReason { get; set; } = string.Empty;
    public decimal BankChargeAmount { get; set; }
    public ReturnedChequeChargeTreatment ChargeTreatment { get; set; }
    public decimal? CustomerRecoverableChargeAmount { get; set; }
    public decimal? ExpenseChargeAmount { get; set; }
    public string? DrawerBank { get; set; }
    public string? Notes { get; set; }
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
}

public class ReturnedChequeCaseDto
{
    public Guid Id { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public Guid CustomerPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid? BankDepositBatchId { get; set; }
    public string? DepositNumber { get; set; }
    public Guid BankAccountId { get; set; }
    public string BankAccountName { get; set; } = string.Empty;
    public string ChequeNumber { get; set; } = string.Empty;
    public string? DrawerBank { get; set; }
    public DateTime ReturnDate { get; set; }
    public string BankReference { get; set; } = string.Empty;
    public string ReturnReason { get; set; } = string.Empty;
    public decimal ReturnedAmount { get; set; }
    public decimal BankChargeAmount { get; set; }
    public ReturnedChequeChargeTreatment ChargeTreatment { get; set; }
    public decimal CustomerRecoverableChargeAmount { get; set; }
    public decimal ExpenseChargeAmount { get; set; }
    public ReturnedChequeCaseStatus Status { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? ReturnCashTransactionId { get; set; }
    public Guid? ChargeCashTransactionId { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
    public IReadOnlyList<BankingAttachmentDto> Attachments { get; set; } = Array.Empty<BankingAttachmentDto>();
    public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
    public IReadOnlyList<FinanceSettlementDimensionComponentDto> SettlementDimensionEvidence { get; set; } =
        Array.Empty<FinanceSettlementDimensionComponentDto>();
    public string RowVersion { get; set; } = string.Empty;
}
