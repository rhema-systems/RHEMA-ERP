namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown when HR is asked to write data that payroll owns.
///
/// Payroll is the single source of truth for the salary structure and the pay-component master;
/// the HR tables are a mirror kept current by a projection. A write here would be silently
/// overwritten by the next pass, so it is refused outright and the caller is pointed at the
/// payroll screen. Controllers surface this as 409 Conflict rather than 400 — the request is
/// well-formed, it just conflicts with where that data is owned.
/// </summary>
public class PayrollOwnedException : InvalidOperationException
{
    public PayrollOwnedException(string message) : base(message)
    {
    }
}
