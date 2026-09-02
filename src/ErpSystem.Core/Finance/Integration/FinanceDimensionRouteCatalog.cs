namespace ErpSystem.Core.Finance.Integration;

/// <summary>
/// Stable, compiled identifiers for Finance dimension-capable producer routes.  Callers select a
/// known identifier; they never submit producer names or route capabilities as free text.
/// </summary>
public enum FinanceDimensionRouteId
{
    ManualJournalEntry = 1,
    FinanceApVendorInvoice = 10,
    FinanceApSupplierDebitNote = 11,
    FinanceApVendorPayment = 12,
    FinanceArCustomerInvoice = 20,
    FinanceArCustomerPayment = 21,
    SalesCreditNote = 30,
    FinanceCashPayment = 40,
    FinanceCashReceipt = 41,
    FinanceCashBankTransfer = 42,
    FinanceBankDeposit = 43,
    FinanceReturnedCheque = 44,
    FinanceBankReconciliationAdjustment = 45,
    ProcurementAcceptedInventoryReceipt = 61,
    InventoryStockAdjustment = 62,
    InventoryLandedCost = 63,
    InventoryDisposalProceeds = 64,
    QuantitySurveyPaymentCertificate = 65,
    QuantitySurveySubcontractCertificate = 66,
    EstateGroundRentCharge = 67,
    EstateGroundRentPenalty = 68,
    EstatePropertyRentBilling = 69,
    EstatePropertySaleBilling = 70,
    EstateFacilitiesBilling = 71,
    EstateLandAcquisition = 72,
    LegalTransferFeeBilling = 73,
    MaintenanceWorkOrderBilling = 74,
    HrPayrollJournal = 75,
    ProcurementSupplierReturnDispatch = 76,
    ProcurementSupplierReturnResolution = 77
}

public enum FinanceDimensionCertificationState
{
    LegacyReadOnly = 0,
    CaptureOptional = 1,
    Enforced = 2
}

public enum FinanceDimensionGrain
{
    JournalLine = 0,
    SourceDocumentLine = 1,
    SettlementAllocationLine = 2
}

/// <summary>
/// A settlement amount is retained by economic component so derived entries never collapse
/// incompatible source dimension combinations into an unassigned aggregate posting line.
/// </summary>
public enum FinanceSettlementComponentType
{
    Principal = 0,
    Discount = 1,
    WithholdingTax = 2,
    VatWithholdingTax = 3,
    Fee = 4,
    WriteOff = 5,
    RealizedFx = 6
}

public sealed record FinanceDimensionRouteDefinition(
    FinanceDimensionRouteId Id,
    string ProducerModule,
    string PostingSourceModule,
    string SourceRoute,
    string DocumentType,
    string ContractVersion,
    FinanceDimensionGrain Grain,
    FinanceDimensionCertificationState DefaultState,
    bool SupportsDocumentDefaults,
    bool RequiresReadinessProvider,
    string Owner,
    string Notes);

/// <summary>
/// Code-owned route identity and capability catalogue.  Tenant state can promote only these exact
/// definitions; adding a new producer route requires a reviewed code change and consumer contract.
/// </summary>
public static class FinanceDimensionRouteCatalog
{
    public static IReadOnlyList<FinanceDimensionRouteDefinition> Routes { get; } =
    [
        new(
            FinanceDimensionRouteId.ManualJournalEntry,
            "Finance",
            "GL",
            "finance.gl.manual-journals",
            "ManualJournalEntry",
            "1.1",
            FinanceDimensionGrain.JournalLine,
            FinanceDimensionCertificationState.Enforced,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: false,
            "Finance / General Ledger",
            "Existing certified manual-journal dimension entry and posting route."),
        new(
            FinanceDimensionRouteId.FinanceApVendorInvoice,
            "Finance",
            "AP",
            "finance.ap.vendor-invoices.manual",
            "VendorInvoice",
            "1.0",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Accounts Payable",
            "Manual Finance AP invoice route. External producers using the same service require their own route."),
        new(
            FinanceDimensionRouteId.FinanceApSupplierDebitNote,
            "Finance",
            "AP",
            "finance.ap.supplier-debit-notes.manual",
            "SupplierDebitNote",
            "1.0",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Accounts Payable",
            "Finance-owned buyer-side supplier debit-note lifecycle."),
        new(
            FinanceDimensionRouteId.FinanceApVendorPayment,
            "Finance",
            "AP",
            "finance.ap.vendor-payments.manual",
            "VendorPayment",
            "1.0",
            FinanceDimensionGrain.SettlementAllocationLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Accounts Payable",
            "Finance-owned vendor payments. Allocation evidence inherits exact originating invoice-line combinations."),
        new(
            FinanceDimensionRouteId.FinanceArCustomerInvoice,
            "Finance",
            "AR",
            "finance.ar.customer-invoices.manual",
            "CustomerInvoice",
            "1.0",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Accounts Receivable",
            "Manual Finance AR invoice route. Generated invoices require producer-specific certification."),
        new(
            FinanceDimensionRouteId.FinanceArCustomerPayment,
            "Finance",
            "AR",
            "finance.ar.customer-payments.manual",
            "CustomerPayment",
            "1.0",
            FinanceDimensionGrain.SettlementAllocationLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Accounts Receivable",
            "Finance-owned customer receipts. Allocation evidence inherits exact originating invoice-line combinations."),
        new(
            FinanceDimensionRouteId.SalesCreditNote,
            "Sales",
            "AR",
            "sales.credit-notes",
            "SalesCreditNote",
            "1.1",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: false,
            RequiresReadinessProvider: true,
            "Sales (producer) / Finance (consumer)",
            "Additive consumer contract only. Sales retains source lifecycle and UI ownership."),
        new(
            FinanceDimensionRouteId.FinanceCashPayment,
            "Finance",
            "CASHBANK",
            "finance.cash.payments.direct",
            "CashBankPayment",
            "1.0",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Cash Management",
            "Direct Finance cash/bank payment only; external payment producers require separate adapters."),
        new(
            FinanceDimensionRouteId.FinanceCashReceipt,
            "Finance",
            "CASHBANK",
            "finance.cash.receipts.direct",
            "CashBankReceipt",
            "1.0",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Cash Management",
            "Direct Finance cash/bank receipt only; deposits and external receipt producers remain uncertified."),
        new(
            FinanceDimensionRouteId.FinanceCashBankTransfer,
            "Finance",
            "CASHBANK",
            "finance.cash.bank-transfers",
            "CashBankTransfer",
            "1.0",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Cash Management",
            "Each bank-transfer leg is an independent source line and resolves its own account rules."),
        new(
            FinanceDimensionRouteId.FinanceBankDeposit,
            "Finance",
            "CASHBANK",
            "finance.cash.bank-deposits",
            "BankDepositBatch",
            "1.0",
            FinanceDimensionGrain.SettlementAllocationLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Cash Management",
            "Finance-owned bank deposits. The destination bank leg and every persisted liquidity allocation retain independent account-rule evidence."),
        new(
            FinanceDimensionRouteId.FinanceReturnedCheque,
            "Finance",
            "CASHBANK",
            "finance.cash.returned-cheques",
            "ReturnedChequeCase",
            "1.0",
            FinanceDimensionGrain.SettlementAllocationLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Cash Management",
            "Finance-owned returned customer cheques. Reopened principal and discounts retain the exact frozen receipt-allocation combinations; bank and charge lines resolve their own account rules."),
        new(
            FinanceDimensionRouteId.FinanceBankReconciliationAdjustment,
            "Finance",
            "CASHBANK",
            "finance.cash.bank-reconciliation-adjustments",
            "BankReconciliationAdjustment",
            "1.0",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            "Finance / Cash Management",
            "Finance-owned bank-only reconciliation adjustments. The bank and offset legs are independent stable source lines; ordinary cash routes remain separate."),
        External(FinanceDimensionRouteId.ProcurementAcceptedInventoryReceipt, "Procurement", "INVENTORY",
            "procurement.receipts.accepted-inventory", "ProcurementPurchaseOrderReceipt", "Accepted receipt inventory/GRV accrual."),
        External(FinanceDimensionRouteId.InventoryStockAdjustment, "Inventory", "INVENTORY",
            "inventory.stock-adjustments", "StockAdjustment", "Approved stock adjustment and its exact reversal."),
        External(FinanceDimensionRouteId.InventoryLandedCost, "Inventory", "INVENTORY",
            "inventory.landed-cost", "InventoryLandedCost", "Posted landed-cost valuation and variance."),
        External(FinanceDimensionRouteId.InventoryDisposalProceeds, "Inventory", "INVENTORY",
            "inventory.disposals.proceeds", "InventoryDisposal", "Approved inventory-disposal proceeds only."),
        External(FinanceDimensionRouteId.QuantitySurveyPaymentCertificate, "QuantitySurvey", "AP",
            "quantity-survey.payment-certificates.ap", "VendorInvoice", "Approved main-contract payment certificate handed to AP."),
        External(FinanceDimensionRouteId.QuantitySurveySubcontractCertificate, "QuantitySurvey", "AP",
            "quantity-survey.subcontract-certificates.ap", "VendorInvoice", "Approved subcontract certificate handed to AP."),
        External(FinanceDimensionRouteId.EstateGroundRentCharge, "Estate", "AR",
            "estate.ground-rent.charges", "CustomerInvoice", "Ground-rent charge generated by Estate."),
        External(FinanceDimensionRouteId.EstateGroundRentPenalty, "Estate", "AR",
            "estate.ground-rent.penalties", "CustomerInvoice", "Ground-rent arrears penalty generated by Estate."),
        External(FinanceDimensionRouteId.EstatePropertyRentBilling, "Estate", "AR",
            "estate.property.rent-billing", "CustomerInvoice", "Property rent invoice generated by Estate."),
        External(FinanceDimensionRouteId.EstatePropertySaleBilling, "Estate", "AR",
            "estate.property.sale-billing", "CustomerInvoice", "Property-sale invoice generated by Estate."),
        External(FinanceDimensionRouteId.EstateFacilitiesBilling, "Estate", "AR",
            "estate.facilities.billing", "CustomerInvoice", "Facilities invoice generated by Estate."),
        External(FinanceDimensionRouteId.EstateLandAcquisition, "Estate", "AP",
            "estate.land-acquisitions.ap", "VendorInvoice", "Land-acquisition or compensation invoice generated by Estate."),
        External(FinanceDimensionRouteId.LegalTransferFeeBilling, "Legal", "AR",
            "legal.transfer-fees.billing", "CustomerInvoice", "Legal transfer-fee invoice generated by a procedure case."),
        External(FinanceDimensionRouteId.MaintenanceWorkOrderBilling, "Maintenance", "AR",
            "maintenance.work-orders.billing", "CustomerInvoice", "Billable completed work order handed to AR."),
        External(FinanceDimensionRouteId.HrPayrollJournal, "HR", "GL",
            "hr.payroll.journals", "PayrollRun", "Approved payroll run journal."),
        External(FinanceDimensionRouteId.ProcurementSupplierReturnDispatch, "Procurement", "AP",
            "procurement.supplier-returns.dispatch", "SupplierReturnDispatch", "Physical return dispatch; posting remains policy-gated."),
        External(FinanceDimensionRouteId.ProcurementSupplierReturnResolution, "Procurement", "AP",
            "procurement.supplier-returns.resolution", "SupplierReturnCommercialResolution", "Supplier commercial resolution; posting remains policy-gated.")
    ];

    private static FinanceDimensionRouteDefinition External(
        FinanceDimensionRouteId id,
        string producer,
        string postingModule,
        string route,
        string documentType,
        string notes) => new(
            id,
            producer,
            postingModule,
            route,
            documentType,
            "1.0",
            FinanceDimensionGrain.SourceDocumentLine,
            FinanceDimensionCertificationState.CaptureOptional,
            SupportsDocumentDefaults: true,
            RequiresReadinessProvider: true,
            $"{producer} (producer) / Finance (consumer)",
            $"Finance-owned additive adapter. {notes} Producer adoption and route-level certification are required before Enforced rollout.");

    public static FinanceDimensionRouteDefinition GetRequired(FinanceDimensionRouteId id) =>
        Routes.SingleOrDefault(route => route.Id == id)
        ?? throw new KeyNotFoundException($"Finance dimension route '{id}' is not registered.");

    public static bool TryGet(FinanceDimensionRouteId id, out FinanceDimensionRouteDefinition definition)
    {
        definition = Routes.SingleOrDefault(route => route.Id == id)!;
        return definition is not null;
    }

    public static FinanceDimensionRouteDefinition? MatchLegacyPosting(
        string sourceModule,
        string documentType)
    {
        if (string.Equals(sourceModule?.Trim(), "GL", StringComparison.OrdinalIgnoreCase)
            && string.Equals(documentType?.Trim(), "ManualJournalEntry", StringComparison.OrdinalIgnoreCase))
            return GetRequired(FinanceDimensionRouteId.ManualJournalEntry);

        return null;
    }
}

/// <summary>
/// Typed server-side producer context.  The posting/creation service resolves all other identity
/// fields from <see cref="FinanceDimensionRouteCatalog"/> and rejects request/route mismatches.
/// </summary>
public sealed record FinancePostingProducerContext(FinanceDimensionRouteId RouteId)
{
    public FinanceDimensionRouteDefinition Definition => FinanceDimensionRouteCatalog.GetRequired(RouteId);
}
