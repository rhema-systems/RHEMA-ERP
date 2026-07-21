using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// CONSULTANT CLIENT
// ============================================================================

#region Consultant Client

public interface IConsultantClientRepository : IGenericRepository<ConsultantClient>
{
    /// <summary>Returns the client matching the unique client code, or null.</summary>
    Task<ConsultantClient?> GetByClientCodeAsync(string clientCode);

    /// <summary>Returns all active consultant clients.</summary>
    Task<IEnumerable<ConsultantClient>> GetActiveClientsAsync();

    /// <summary>Returns a client fully loaded with its engagements, timesheets, and invoices.</summary>
    Task<ConsultantClient?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// CLIENT ENGAGEMENT
// ============================================================================

#region Client Engagement

public interface IClientEngagementRepository : IGenericRepository<ClientEngagement>
{
    /// <summary>Returns the engagement matching the unique engagement code, or null.</summary>
    Task<ClientEngagement?> GetByEngagementCodeAsync(string engagementCode);

    /// <summary>Returns all engagements for a specific client.</summary>
    Task<IEnumerable<ClientEngagement>> GetByClientIdAsync(Guid clientId);

    /// <summary>Returns all engagements assigned to a specific consultant (employee).</summary>
    Task<IEnumerable<ClientEngagement>> GetByConsultantIdAsync(Guid consultantId);

    /// <summary>Returns currently active engagements (Status = Active).</summary>
    Task<IEnumerable<ClientEngagement>> GetActiveEngagementsAsync();

    /// <summary>Returns engagements between a client and a specific consultant.</summary>
    Task<IEnumerable<ClientEngagement>> GetByClientAndConsultantAsync(Guid clientId, Guid consultantId);

    /// <summary>
    /// Returns engagements whose end date falls within the specified number of days,
    /// for proactive renewal reminders.
    /// </summary>
    Task<IEnumerable<ClientEngagement>> GetExpiringSoonAsync(int daysAhead = 30);
}

#endregion

// ============================================================================
// CONSULTANT TIMESHEET
// ============================================================================

#region Consultant Timesheet

public interface IConsultantTimesheetRepository : IGenericRepository<ConsultantTimesheet>
{
    /// <summary>Returns the timesheet matching the unique timesheet number, or null.</summary>
    Task<ConsultantTimesheet?> GetByTimesheetNumberAsync(string timesheetNumber);

    /// <summary>Returns all timesheets submitted by a specific consultant (employee), newest first.</summary>
    Task<IEnumerable<ConsultantTimesheet>> GetByConsultantIdAsync(Guid consultantId);

    /// <summary>Returns all timesheets for a specific client.</summary>
    Task<IEnumerable<ConsultantTimesheet>> GetByClientIdAsync(Guid clientId);

    /// <summary>Returns timesheets for a specific engagement.</summary>
    Task<IEnumerable<ConsultantTimesheet>> GetByEngagementIdAsync(Guid engagementId);

    /// <summary>Returns timesheets filtered by lifecycle status.</summary>
    Task<IEnumerable<ConsultantTimesheet>> GetByStatusAsync(TimesheetStatus status);

    /// <summary>Returns timesheets whose billing period overlaps with the specified date range.</summary>
    Task<IEnumerable<ConsultantTimesheet>> GetByPeriodAsync(DateOnly from, DateOnly to);

    /// <summary>
    /// Returns timesheets that are client-confirmed and have not yet been linked to an invoice.
    /// These are ready for invoicing.
    /// </summary>
    Task<IEnumerable<ConsultantTimesheet>> GetReadyForInvoicingAsync(Guid clientId);

    /// <summary>Returns a fully-loaded timesheet including entries, confirmations, and invoice links.</summary>
    Task<ConsultantTimesheet?> GetWithFullDetailsAsync(Guid id);
}

#endregion

// ============================================================================
// CONSULTANT TIMESHEET ENTRY
// ============================================================================

#region Consultant Timesheet Entry

public interface IConsultantTimesheetEntryRepository : IGenericRepository<ConsultantTimesheetEntry>
{
    /// <summary>Returns all daily entries for a timesheet, ordered by work date.</summary>
    Task<IEnumerable<ConsultantTimesheetEntry>> GetByTimesheetIdAsync(Guid timesheetId);

    /// <summary>Returns the entry for a specific work date within a timesheet, or null.</summary>
    Task<ConsultantTimesheetEntry?> GetByTimesheetAndDateAsync(Guid timesheetId, DateOnly workDate);
}

#endregion

// ============================================================================
// CLIENT TIMESHEET CONFIRMATION
// ============================================================================

#region Client Timesheet Confirmation

public interface IClientTimesheetConfirmationRepository : IGenericRepository<ClientTimesheetConfirmation>
{
    /// <summary>Returns all confirmation requests sent for a timesheet, newest first.</summary>
    Task<IEnumerable<ClientTimesheetConfirmation>> GetByTimesheetIdAsync(Guid timesheetId);

    /// <summary>Returns the confirmation record matching the one-time confirmation token, or null.</summary>
    Task<ClientTimesheetConfirmation?> GetByTokenAsync(Guid confirmationToken);

    /// <summary>Returns confirmations that are still pending (Sent status) and not yet expired.</summary>
    Task<IEnumerable<ClientTimesheetConfirmation>> GetPendingConfirmationsAsync();

    /// <summary>Returns confirmations whose token has expired but are still in Sent status.</summary>
    Task<IEnumerable<ClientTimesheetConfirmation>> GetExpiredConfirmationsAsync();
}

#endregion

// ============================================================================
// TIMESHEET INVOICE
// ============================================================================

#region Timesheet Invoice

public interface ITimesheetInvoiceRepository : IGenericRepository<TimesheetInvoice>
{
    /// <summary>Returns the invoice matching the unique invoice number, or null.</summary>
    Task<TimesheetInvoice?> GetByInvoiceNumberAsync(string invoiceNumber);

    /// <summary>Returns all invoices raised for a specific client, newest first.</summary>
    Task<IEnumerable<TimesheetInvoice>> GetByClientIdAsync(Guid clientId);

    /// <summary>Returns all invoices for a specific consultant.</summary>
    Task<IEnumerable<TimesheetInvoice>> GetByConsultantIdAsync(Guid consultantId);

    /// <summary>Returns invoices filtered by payment status.</summary>
    Task<IEnumerable<TimesheetInvoice>> GetByStatusAsync(TimesheetInvoiceStatus status);

    /// <summary>Returns unpaid invoices (not Paid or Voided) for a client — the outstanding balance list.</summary>
    Task<IEnumerable<TimesheetInvoice>> GetOutstandingInvoicesAsync(Guid clientId);

    /// <summary>Returns invoices whose due date has passed and are not yet paid or voided.</summary>
    Task<IEnumerable<TimesheetInvoice>> GetOverdueInvoicesAsync();

    /// <summary>Returns a fully-loaded invoice including all linked timesheet details.</summary>
    Task<TimesheetInvoice?> GetWithLinkedTimesheetsAsync(Guid id);
}

#endregion

// ============================================================================
// TIMESHEET INVOICE LINK
// ============================================================================

#region Timesheet Invoice Link

public interface ITimesheetInvoiceLinkRepository : IGenericRepository<TimesheetInvoiceLink>
{
    /// <summary>Returns all timesheet links for a specific invoice.</summary>
    Task<IEnumerable<TimesheetInvoiceLink>> GetByInvoiceIdAsync(Guid invoiceId);

    /// <summary>Returns all invoice links that include a specific timesheet.</summary>
    Task<IEnumerable<TimesheetInvoiceLink>> GetByTimesheetIdAsync(Guid timesheetId);
}

#endregion
