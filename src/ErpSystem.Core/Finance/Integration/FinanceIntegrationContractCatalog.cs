namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Describes the delivery state of a cross-module Finance contract.  The values are intentionally
/// broader than "implemented/not implemented" because some interfaces cannot be completed until
/// the producing module owner agrees the source event and ownership boundary.
/// </summary>
public enum FinanceIntegrationContractStatus
{
    Available,
    Planned,
    DecisionRequired,
    RequirementsClarification
}

/// <summary>
/// A stable, reviewable description of one module-to-Finance boundary.
/// </summary>
/// <param name="Id">Permanent catalogue identifier used in code reviews and consumer tests.</param>
/// <param name="Name">Business-readable name of the integration.</param>
/// <param name="ProducerOwner">Module that owns the source transaction and its approval.</param>
/// <param name="FinanceOwner">Finance capability that validates and records the accounting effect.</param>
/// <param name="Status">Current delivery state; planned entries are not callable promises.</param>
/// <param name="Version">Contract version. Breaking request or outcome changes require a new major version.</param>
/// <param name="EntryPoint">Existing callable interface or the proposed boundary when still planned.</param>
/// <param name="SourceDocumentType">Stable Finance posting/audit discriminator.</param>
/// <param name="LimitationId">Development limitation that prevents completion, when applicable.</param>
/// <param name="Notes">Short ownership or rollout clarification.</param>
public sealed record FinanceIntegrationContractDefinition(
    string Id,
    string Name,
    string ProducerOwner,
    string FinanceOwner,
    FinanceIntegrationContractStatus Status,
    string Version,
    string EntryPoint,
    string SourceDocumentType,
    string? LimitationId,
    string Notes);

/// <summary>
/// Canonical catalogue for module-to-Finance contracts.
///
/// This catalogue is deliberately code-backed rather than documentation-only.  Stable identifiers,
/// ownership and delivery status can therefore be checked in CI and referenced by producer-module
/// consumer tests.  Business implementation remains in the existing Finance services; this type does
/// not create a second posting path.
/// </summary>
public static class FinanceIntegrationContractCatalog
{
    public static IReadOnlyList<FinanceIntegrationContractDefinition> Contracts { get; } =
    [
        new(
            "FIN-INT-001",
            "Approved source transaction to General Ledger",
            "Calling module",
            "Finance / General Ledger",
            FinanceIntegrationContractStatus.Available,
            "2.1",
            "IFinancePostingEngine.PostAsync(FinancePostingRequestV2Dto, FinancePostingProducerContext)",
            "Defined by the calling adapter",
            null,
            "Final V2-only boundary after coordinated V1 retirement. AccountingBookCode selects one canonical book. Detailed AccountClassificationId is Finance-owned and cannot be producer supplied. Use only after producer approval; Finance owns book membership, period, balance, currency, idempotency and canonical evidence."),
        new(
            "FIN-INT-002",
            "Accepted procurement stock receipt to inventory and GRV accrual",
            "Procurement and Inventory",
            "Finance / General Ledger",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IInventoryReceiptFinancePostingService.PostAcceptedReceiptAsync",
            "ProcurementPurchaseOrderReceipt",
            null,
            "Procurement retains purchase-order and inspection ownership, Inventory supplies posted valuation movements, and Finance resolves inventory/GRV control accounts."),
        new(
            "FIN-INT-003",
            "Inventory adjustment to GL",
            "Inventory",
            "Finance / General Ledger",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IInventoryAdjustmentFinancePostingService.PostAsync",
            "StockAdjustment",
            null,
            "Inventory supplies the approved quantity/value evidence; Finance owns account resolution and posting."),
        new(
            "FIN-INT-004",
            "Inventory landed-cost valuation to GL",
            "Inventory",
            "Finance / General Ledger",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IInventoryLandedCostFinancePostingService.PostLandedCostAsync",
            "InventoryLandedCost",
            null,
            "The producer supplies accepted valuation evidence and Finance records the balanced accounting event."),
        new(
            "FIN-INT-005",
            "Sales return credit note to AR and GL",
            "Sales",
            "Finance / Accounts Receivable",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IReturnOrderService.PostCreditNoteAsync",
            "SalesCreditNote",
            null,
            "Sales owns return approval; the adapter uses Finance posting and preserves the Sales origin module for inquiry."),
        new(
            "FIN-INT-006",
            "Fixed-asset disposal proceeds to AR, tax and cash",
            "Finance / Fixed Assets",
            "Finance / AR, Tax and Cash",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IAssetDisposalService.CompleteDisposalAsync",
            "FixedAssetDisposal / CustomerInvoice / CustomerPayment",
            "FIN-LIM-0040",
            "The disposal service orchestrates existing Finance services; it does not duplicate AR invoice, tax calculation, receipt allocation or GL logic."),
        new(
            "FIN-INT-007",
            "Accepted procured asset to fixed-asset capitalization",
            "Procurement",
            "Finance / Fixed Assets",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IProcurementFixedAssetCapitalizationAdapter",
            "ProcurementFixedAssetCapitalization",
            "FIN-LIM-0028",
            "Procurement owns accepted receipt/inspection evidence; Finance reserves individual assets, applies maker-checker policy and reclassifies posted Inventory Control value without duplicating AP or GRV cost."),
        new(
            "FIN-INT-008",
            "Sales billing to AR invoice",
            "Sales",
            "Finance / Accounts Receivable",
            FinanceIntegrationContractStatus.Planned,
            "0.1",
            "Proposed Sales-to-AR adapter using IInvoiceService",
            "To be agreed with Sales owner",
            "FIN-LIM-0051",
            "Sales retains order/delivery ownership; Finance owns customer accounting, tax evidence, posting and settlement."),
        new(
            "FIN-INT-009",
            "Shared payment-method ownership",
            "Sales, Procurement and other producers",
            "Finance / Cash and Bank",
            FinanceIntegrationContractStatus.DecisionRequired,
            "0.1",
            "Shared payment-method reference contract",
            "Not applicable",
            "FIN-LIM-0050",
            "Agree the authoritative master-data owner before introducing adapters or migrations."),
        new(
            "FIN-INT-010",
            "Asymmetric customer and supplier payment terms",
            "Sales and Procurement",
            "Finance / AR and AP",
            FinanceIntegrationContractStatus.DecisionRequired,
            "0.1",
            "Shared partner/payment-term reference contract",
            "Not applicable",
            "FIN-LIM-0053",
            "The data ownership decision must allow different AR and AP terms without making either subledger own shared partner data."),
        new(
            "FIN-INT-011",
            "SRS-INT-004 specialist ledger integration",
            "Owner not specified in the supplied requirement",
            "Finance",
            FinanceIntegrationContractStatus.RequirementsClarification,
            "0.0",
            "Not defined",
            "Not defined",
            null,
            "TDC must define SH Fund, PF, ESB and fuel-allocation source systems, events, balances and reconciliation outcomes before an interface can be designed."),

        // FIN-INT-012 and FIN-INT-013 deliberately model two different business dates. Dispatching
        // accepted stock back to a supplier does not prove that the supplier has accepted a credit,
        // refund or replacement. Procurement owns the Return-to-Vendor lifecycle, Inventory owns
        // quantity movements and carrying-cost layers, and Finance owns AP/GRV, tax and GL effects.
        // The modules exchange immutable contract DTOs; no module may write another module's tables.
        new(
            "FIN-INT-012",
            "Post-acceptance supplier return dispatch and valuation handoff",
            "Procurement and Inventory",
            "Finance / Accounts Payable, GRV and General Ledger",
            FinanceIntegrationContractStatus.Planned,
            "0.1",
            "SupplierReturnFinanceAdapter.ConsumeDispatchAsync (fail-closed)",
            "SupplierReturnDispatch v0.1",
            null,
            "Procurement owns the approved return and dispatch lifecycle; Inventory owns the authoritative outbound quantity movement and carrying-cost layers; Finance owns AP/GRV and GL treatment. Producers must not write Finance tables directly. Keep Planned until the callable Finance consumer and producer consumer-contract tests pass."),
        new(
            "FIN-INT-013",
            "Supplier return commercial resolution to AP, tax and settlement",
            "Procurement",
            "Finance / Accounts Payable, Tax and Cash",
            FinanceIntegrationContractStatus.Planned,
            "0.1",
            "SupplierReturnFinanceAdapter.ConsumeCommercialResolutionAsync (fail-closed)",
            "SupplierReturnCommercialResolution v0.1",
            null,
            "Procurement owns the supplier credit, refund, replacement or warranty-resolution evidence; Inventory remains authoritative for quantity and cost layers; Finance owns AP application, GRV clearing, tax, cash/refund and GL effects. Producers must not write Finance tables directly. Keep Planned until the callable Finance consumer and producer consumer-contract tests pass."),
        new(
            "FIN-INT-015",
            "Approved Procurement demand to Finance budget commitment",
            "Procurement",
            "Finance / Budget",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IFinanceBudgetCommitmentService",
            "ProcurementRequisition",
            null,
            "Finance provides canonical adopted-budget selection, availability, reservations, idempotency and GL-derived actuals. Procurement owns its workflow and must add the consumer adapter without writing Finance tables or reserving again at purchase-order issue."),
        new(
            "FIN-INT-016",
            "Direct AP expense invoice to Finance budget commitment",
            "Finance / Accounts Payable",
            "Finance / Budget and General Ledger",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IVendorInvoiceService and IFinanceBudgetCommitmentService",
            "VendorInvoice",
            null,
            "Direct budget-controlled expense lines select an adopted Finance Budget Entry, reserve before the existing AP approval workflow, release on rejection and consume atomically with central GL posting. Opening, PO/GRV, Inventory and Fixed Asset lines are excluded to prevent duplicate commitments."),
        new(
            "FIN-INT-017",
            "Approved external producer to dimension-aware Finance posting",
            "Procurement, Inventory, Sales, Quantity Survey, Estate, Legal, Maintenance and HR",
            "Finance / AP, AR and General Ledger",
            FinanceIntegrationContractStatus.Available,
            "1.0",
            "IExternalFinancePostingAdapter",
            "Resolved from FinanceExternalProducerContractCatalog",
            null,
            "Each producer semantic has a distinct compiled route. Finance validates tenant, approval, accounts, balanced amounts, currency/rate and stable line provenance, then freezes canonical dimension evidence and posts idempotently. CaptureOptional does not imply producer adoption; Enforced promotion remains blocked until the producer supplies an authoritative tenant-scoped document census.")
    ];

    public static FinanceIntegrationContractDefinition GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Contracts.SingleOrDefault(contract =>
                   string.Equals(contract.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new KeyNotFoundException($"Finance integration contract '{id}' is not registered.");
    }
}
