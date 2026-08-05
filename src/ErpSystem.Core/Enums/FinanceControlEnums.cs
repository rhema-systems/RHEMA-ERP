namespace ErpSystem.Core.Enums;

/// <summary>
/// Determines which accounting date Finance uses when correcting a posted source document.
/// The default reflects TDC's control objective: corrections should normally be recognised in
/// the current open period while retaining an immutable reference to the original transaction.
/// </summary>
public enum FinanceReversalDatePolicy
{
    /// <summary>
    /// Use a date in the most recent open fiscal period. A requested date is accepted only when
    /// it belongs to that period; otherwise the service selects an appropriate date in the period.
    /// </summary>
    CurrentOpenPeriod = 1,

    /// <summary>
    /// Use the source document date only while its fiscal period remains open. If that period has
    /// closed, fall back to the most recent open period rather than reopening history implicitly.
    /// </summary>
    OriginalDocumentPeriodIfOpen = 2
}

/// <summary>
/// Finance data dimensions that can be assigned to a user. Scope values are stored as stable
/// strings because some dimensions are GUID-backed records while others (for example a future
/// cost-centre segment) are governed codes. The service validates values for the dimensions it
/// currently enforces.
/// </summary>
public enum FinanceAccessScopeType
{
    Tenant = 1,
    BankAccount = 2,
    GeneralLedgerAccount = 3,
    Branch = 4,
    CostCentre = 5
}

/// <summary>
/// Ordered Finance data-scope capability. Identity permissions still decide whether an action is
/// available; this level independently decides which Finance data the user may act upon.
/// </summary>
public enum FinanceAccessLevel
{
    Read = 1,
    Operate = 2,
    Approve = 3,
    Administer = 4
}
