namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Commercial state of the returned goods when Inventory confirms physical dispatch.
/// This is deliberately explicit: Finance must not infer AP status from a PO or receipt alone.
/// </summary>
public enum SupplierReturnInvoiceState
{
    Uninvoiced = 1,
    Invoiced = 2
}

/// <summary>
/// Supplier's independently-timed commercial response to a post-acceptance return.
/// Physical dispatch (FIN-INT-012) and commercial resolution (FIN-INT-013) are separate contracts.
/// </summary>
public enum SupplierReturnCommercialResolutionType
{
    SupplierCreditNote = 1,
    CashRefund = 2,
    Replacement = 3,
    RepairOrWarranty = 4,
    AgreedFutureCredit = 5,
    ClaimRejected = 6
}

public enum SupplierReturnFinanceOutcomeStatus
{
    Posted = 1,
    FinanceDocumentCreated = 2,
    NoFinancePostingRequired = 3,
    DecisionRequired = 4
}

/// <summary>
/// Immutable, producer-owned valuation assertion for one physically dispatched return line.
/// Procurement/Inventory supplies business and valuation facts only; it must never supply a GL account.
/// Amounts are in the tenant's functional currency. Before any posting, Finance must independently
/// derive and compare them against tenant-scoped Inventory/receipt evidence; this DTO is not proof.
/// </summary>
public sealed record SupplierReturnDispatchLineDto
{
    public Guid PurchaseOrderLineId { get; init; }
    public Guid InventoryItemId { get; init; }
    public Guid InventoryMovementId { get; init; }
    public string InventoryMovementReference { get; init; } = string.Empty;
    public bool IsInventoryMovementPosted { get; init; }
    public DateTime InventoryMovementPostedAtUtc { get; init; }
    public Guid WarehouseId { get; init; }
    public Guid? WarehouseLocationId { get; init; }
    public string? LotNumber { get; init; }
    public IReadOnlyList<string> SerialNumbers { get; init; } = Array.Empty<string>();
    public decimal Quantity { get; init; }
    public string UnitOfMeasure { get; init; } = string.Empty;
    public decimal InventoryCarryingAmountFunctional { get; init; }

    /// <summary>
    /// Exact returned portion of the original GRV accrual. Required only while the goods are uninvoiced.
    /// v0.1 fails closed on any difference because no governed return-variance policy exists yet.
    /// </summary>
    public decimal? OriginalGrvAccrualAmountFunctional { get; init; }
}

/// <summary>
/// FIN-INT-012 v0.1 producer payload. Procurement owns return approval and dispatch evidence; Inventory
/// owns the posted outbound movement and carrying value. Finance consumes this snapshot without mutating
/// either producer's entities or recreating their approval workflow.
/// </summary>
public sealed record SupplierReturnDispatchFinanceDto
{
    public string ContractVersion { get; init; } = "0.1";
    public Guid TenantId { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public Guid SupplierReturnId { get; init; }
    public string SupplierReturnReference { get; init; } = string.Empty;
    public Guid DispatchId { get; init; }
    public string DispatchReference { get; init; } = string.Empty;
    /// <summary>
    /// Shared BusinessPartner supplier identifier used by Finance AP. Do not substitute a module-local
    /// Procurement Supplier id unless the shared-master mapping proves that they are the same record.
    /// </summary>
    public Guid SupplierBusinessPartnerId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid PurchaseOrderReceiptId { get; init; }
    public Guid? OriginalVendorInvoiceId { get; init; }
    public SupplierReturnInvoiceState InvoiceState { get; init; }
    public DateTime ApprovedAtUtc { get; init; }
    public Guid ApprovedByUserId { get; init; }
    public string ApprovalReference { get; init; } = string.Empty;
    public DateTime DispatchedAtUtc { get; init; }

    /// <summary>
    /// Must be false for FIN-INT-012 v0.1. Inventory supplies its movement reference and valuation
    /// assertion, but Finance is the only GL writer. Finance must re-derive authoritative evidence;
    /// a producer-owned GL credit would duplicate Inventory Control.
    /// </summary>
    public bool InventoryMovementHasSeparateFinancePosting { get; init; }

    public string FunctionalCurrencyCode { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<string> EvidenceReferences { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Corrections and reversals retain the original identity rather than overwriting it. FIN-INT-012
    /// v0.1 validates this lineage but fails closed until Finance reversal orchestration is approved.
    /// </summary>
    public Guid? CorrectionOfDispatchId { get; init; }
    public Guid? ReversalOfDispatchId { get; init; }
    public string? CorrectionOrReversalReason { get; init; }

    /// <summary>
    /// Lower- or upper-case SHA-256 hex of the producer's canonical immutable dispatch envelope.
    /// This detects payload mutation/retry drift; it is not authentication and does not prove the
    /// referenced source records or valuations exist.
    /// </summary>
    public string SourceIntegrityHash { get; init; } = string.Empty;

    public IReadOnlyList<SupplierReturnDispatchLineDto> Lines { get; init; } =
        Array.Empty<SupplierReturnDispatchLineDto>();
}

/// <summary>
/// Supplier-originated value/tax evidence for one line on the original posted AP invoice. Finance
/// resolves every GL account from the invoice and tenant settings; producer modules cannot nominate one.
/// </summary>
public sealed record SupplierReturnCommercialResolutionLineDto
{
    public Guid OriginalVendorInvoiceLineItemId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public Guid? TaxGroupId { get; init; }
    public decimal TaxRate { get; init; }
    public decimal? TaxAmount { get; init; }
    public decimal DiscountPercentage { get; init; }
    public decimal? DiscountAmount { get; init; }
    public decimal? LineTotal { get; init; }
}

/// <summary>
/// FIN-INT-013 v0.1 producer payload. This event occurs when the supplier accepts a credit, refund or
/// replacement outcome; it must not be emitted merely because the goods were dispatched.
/// </summary>
public sealed record SupplierReturnCommercialResolutionFinanceDto
{
    public string ContractVersion { get; init; } = "0.1";
    public Guid TenantId { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public Guid ResolutionId { get; init; }
    public Guid SupplierReturnId { get; init; }
    public string SupplierReturnReference { get; init; } = string.Empty;
    public Guid DispatchId { get; init; }
    public Guid SupplierBusinessPartnerId { get; init; }
    public Guid PurchaseOrderId { get; init; }
    public Guid? OriginalVendorInvoiceId { get; init; }
    public SupplierReturnCommercialResolutionType ResolutionType { get; init; }
    public DateTime ResolvedAtUtc { get; init; }
    public Guid ApprovedByUserId { get; init; }
    public string ApprovalReference { get; init; } = string.Empty;
    public string? SupplierCreditNoteReference { get; init; }
    public string? SupplierCashRefundReference { get; init; }
    public string? ReplacementReference { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal ExchangeRate { get; init; } = 1m;
    public string Reason { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public IReadOnlyList<string> EvidenceReferences { get; init; } = Array.Empty<string>();
    public Guid? CorrectionOfResolutionId { get; init; }
    public Guid? ReversalOfResolutionId { get; init; }
    public string? CorrectionOrReversalReason { get; init; }
    public string SourceIntegrityHash { get; init; } = string.Empty;
    public IReadOnlyList<SupplierReturnCommercialResolutionLineDto> Lines { get; init; } =
        Array.Empty<SupplierReturnCommercialResolutionLineDto>();
}

/// <summary>
/// Explicit adapter outcome. DecisionRequired is a fail-closed result: no Finance posting or AP
/// document was created. Calling modules should display the decision codes and retain the source event.
/// </summary>
public sealed record SupplierReturnFinanceOutcomeDto
{
    public string ContractId { get; init; } = string.Empty;
    public string ContractVersion { get; init; } = string.Empty;
    public SupplierReturnFinanceOutcomeStatus Status { get; init; }
    public bool WasDuplicate { get; init; }
    public Guid? PostingEventId { get; init; }
    public Guid? JournalEntryId { get; init; }
    public Guid? SupplierDebitNoteId { get; init; }
    /// <summary>
    /// Reserved for a future, explicitly non-executable Finance preview. FIN-INT-013 v0.1 always leaves
    /// this null: the existing SupplierDebitNote line model would reverse Inventory twice instead of
    /// clearing a governed return-to-vendor balance.
    /// </summary>
    public CreateSupplierDebitNoteDto? PreparedSupplierDebitNote { get; init; }
    public bool RequiresCashSettlement { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> DecisionCodes { get; init; } = Array.Empty<string>();
}
