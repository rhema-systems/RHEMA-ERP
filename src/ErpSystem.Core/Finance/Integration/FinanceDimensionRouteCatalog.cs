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
    FinanceArCustomerInvoice = 20,
    SalesCreditNote = 30
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
    SourceDocumentLine = 1
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
            "Additive consumer contract only. Sales retains source lifecycle and UI ownership.")
    ];

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
