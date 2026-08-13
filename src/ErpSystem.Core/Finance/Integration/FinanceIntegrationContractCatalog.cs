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
            "1.0",
            "IFinancePostingEngine.PostAsync(FinancePostingRequestDto)",
            "Defined by the calling adapter",
            null,
            "Use only after the producer has completed its own approval; Finance owns period, balance, currency, idempotency and audit validation."),
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
            FinanceIntegrationContractStatus.Planned,
            "0.1",
            "Proposed Procurement-to-Fixed-Assets adapter",
            "To be agreed with Procurement owner",
            "FIN-LIM-0028",
            "Procurement must publish the accepted asset, source cost and inspection evidence; Finance will own asset policy, book values and capitalization posting."),
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
            "TDC must define SH Fund, PF, ESB and fuel-allocation source systems, events, balances and reconciliation outcomes before an interface can be designed.")
    ];

    public static FinanceIntegrationContractDefinition GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return Contracts.SingleOrDefault(contract =>
                   string.Equals(contract.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
               ?? throw new KeyNotFoundException($"Finance integration contract '{id}' is not registered.");
    }
}
