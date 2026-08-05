using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// CONSULTANT CLIENT REPOSITORY
// ============================================================================

#region Consultant Client Repository

public class ConsultantClientRepository : GenericRepository<ConsultantClient>, IConsultantClientRepository
{
    public ConsultantClientRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ConsultantClient?> GetByClientCodeAsync(string clientCode)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.ClientCode == clientCode && !c.IsDeleted);
    }

    public async Task<IEnumerable<ConsultantClient>> GetActiveClientsAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.ClientName)
            .ToListAsync();
    }

    public async Task<ConsultantClient?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.Country)
            .Include(c => c.Engagements.Where(e => !e.IsDeleted))
                .ThenInclude(e => e.Consultant)
            .Include(c => c.Timesheets.Where(t => !t.IsDeleted))
                .ThenInclude(t => t.Consultant)
            .Include(c => c.Invoices.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }
}

#endregion

// ============================================================================
// CLIENT ENGAGEMENT REPOSITORY
// ============================================================================

#region Client Engagement Repository

public class ClientEngagementRepository : GenericRepository<ClientEngagement>, IClientEngagementRepository
{
    public ClientEngagementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ClientEngagement?> GetByEngagementCodeAsync(string engagementCode)
    {
        return await _dbSet
            .Include(e => e.Client)
            .Include(e => e.Consultant)
            .FirstOrDefaultAsync(e => e.EngagementCode == engagementCode && !e.IsDeleted);
    }

    public async Task<IEnumerable<ClientEngagement>> GetByClientIdAsync(Guid clientId)
    {
        return await _dbSet
            .Include(e => e.Consultant)
            .Where(e => e.ClientId == clientId && !e.IsDeleted)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClientEngagement>> GetByConsultantIdAsync(Guid consultantId)
    {
        return await _dbSet
            .Include(e => e.Client)
            .Where(e => e.ConsultantId == consultantId && !e.IsDeleted)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClientEngagement>> GetActiveEngagementsAsync()
    {
        return await _dbSet
            .Include(e => e.Client)
            .Include(e => e.Consultant)
            .Where(e => e.Status == ClientEngagementStatus.Active && !e.IsDeleted)
            .OrderBy(e => e.Client.ClientName)
            .ThenBy(e => e.Consultant.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClientEngagement>> GetByClientAndConsultantAsync(Guid clientId, Guid consultantId)
    {
        return await _dbSet
            .Where(e => e.ClientId == clientId && e.ConsultantId == consultantId && !e.IsDeleted)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClientEngagement>> GetExpiringSoonAsync(int daysAhead = 30)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Include(e => e.Client)
            .Include(e => e.Consultant)
            .Where(e => e.Status == ClientEngagementStatus.Active
                     && e.EndDate != null
                     && e.EndDate >= today
                     && e.EndDate <= cutoff
                     && !e.IsDeleted)
            .OrderBy(e => e.EndDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// CONSULTANT TIMESHEET REPOSITORY
// ============================================================================

#region Consultant Timesheet Repository

public class ConsultantTimesheetRepository : GenericRepository<ConsultantTimesheet>, IConsultantTimesheetRepository
{
    public ConsultantTimesheetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ConsultantTimesheet?> GetByTimesheetNumberAsync(string timesheetNumber)
    {
        return await _dbSet
            .Include(t => t.Consultant)
            .Include(t => t.Client)
            .Include(t => t.Engagement)
            .FirstOrDefaultAsync(t => t.TimesheetNumber == timesheetNumber && !t.IsDeleted);
    }

    public async Task<IEnumerable<ConsultantTimesheet>> GetByConsultantIdAsync(Guid consultantId)
    {
        return await _dbSet
            .Include(t => t.Client)
            .Include(t => t.Engagement)
            .Where(t => t.ConsultantId == consultantId && !t.IsDeleted)
            .OrderByDescending(t => t.PeriodStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultantTimesheet>> GetByClientIdAsync(Guid clientId)
    {
        return await _dbSet
            .Include(t => t.Consultant)
            .Include(t => t.Engagement)
            .Where(t => t.ClientId == clientId && !t.IsDeleted)
            .OrderByDescending(t => t.PeriodStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultantTimesheet>> GetByEngagementIdAsync(Guid engagementId)
    {
        return await _dbSet
            .Include(t => t.Consultant)
            .Include(t => t.Client)
            .Where(t => t.EngagementId == engagementId && !t.IsDeleted)
            .OrderByDescending(t => t.PeriodStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultantTimesheet>> GetByStatusAsync(TimesheetStatus status)
    {
        return await _dbSet
            .Include(t => t.Consultant)
            .Include(t => t.Client)
            .Where(t => t.Status == status && !t.IsDeleted)
            .OrderByDescending(t => t.PeriodStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultantTimesheet>> GetByPeriodAsync(DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(t => t.Consultant)
            .Include(t => t.Client)
            .Where(t => t.PeriodStartDate <= to && t.PeriodEndDate >= from && !t.IsDeleted)
            .OrderBy(t => t.PeriodStartDate)
            .ThenBy(t => t.Consultant.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<ConsultantTimesheet>> GetReadyForInvoicingAsync(Guid clientId)
    {
        return await _dbSet
            .Include(t => t.Consultant)
            .Include(t => t.Engagement)
            .Where(t => t.ClientId == clientId
                     && t.Status == TimesheetStatus.ClientConfirmed
                     && !t.InvoiceLinks.Any()
                     && !t.IsDeleted)
            .OrderBy(t => t.PeriodStartDate)
            .ToListAsync();
    }

    public async Task<ConsultantTimesheet?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.Consultant)
            .Include(t => t.Client)
            .Include(t => t.Engagement)
            .Include(t => t.Entries)
            .Include(t => t.Confirmations).ThenInclude(c => c.SentBy)
            .Include(t => t.InvoiceLinks).ThenInclude(l => l.Invoice)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }
}

#endregion

// ============================================================================
// CONSULTANT TIMESHEET ENTRY REPOSITORY
// ============================================================================

#region Consultant Timesheet Entry Repository

public class ConsultantTimesheetEntryRepository : GenericRepository<ConsultantTimesheetEntry>, IConsultantTimesheetEntryRepository
{
    public ConsultantTimesheetEntryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ConsultantTimesheetEntry>> GetByTimesheetIdAsync(Guid timesheetId)
    {
        return await _dbSet
            .Where(e => e.TimesheetId == timesheetId && !e.IsDeleted)
            .OrderBy(e => e.WorkDate)
            .ToListAsync();
    }

    public async Task<ConsultantTimesheetEntry?> GetByTimesheetAndDateAsync(Guid timesheetId, DateOnly workDate)
    {
        return await _dbSet
            .FirstOrDefaultAsync(e => e.TimesheetId == timesheetId && e.WorkDate == workDate && !e.IsDeleted);
    }
}

#endregion

// ============================================================================
// CLIENT TIMESHEET CONFIRMATION REPOSITORY
// ============================================================================

#region Client Timesheet Confirmation Repository

public class ClientTimesheetConfirmationRepository : GenericRepository<ClientTimesheetConfirmation>, IClientTimesheetConfirmationRepository
{
    public ClientTimesheetConfirmationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ClientTimesheetConfirmation>> GetByTimesheetIdAsync(Guid timesheetId)
    {
        return await _dbSet
            .Include(c => c.SentBy)
            .Where(c => c.TimesheetId == timesheetId && !c.IsDeleted)
            .OrderByDescending(c => c.SentDate)
            .ToListAsync();
    }

    public async Task<ClientTimesheetConfirmation?> GetByTokenAsync(Guid confirmationToken)
    {
        return await _dbSet
            .Include(c => c.Timesheet).ThenInclude(t => t.Consultant)
            .Include(c => c.Timesheet).ThenInclude(t => t.Client)
            .Include(c => c.Timesheet).ThenInclude(t => t.Entries)
            .FirstOrDefaultAsync(c => c.ConfirmationToken == confirmationToken && !c.IsDeleted);
    }

    public async Task<IEnumerable<ClientTimesheetConfirmation>> GetPendingConfirmationsAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(c => c.Timesheet).ThenInclude(t => t.Consultant)
            .Include(c => c.Timesheet).ThenInclude(t => t.Client)
            .Where(c => c.Status == TimesheetConfirmationStatus.Sent
                     && c.TokenExpiryDate > now
                     && !c.IsDeleted)
            .OrderBy(c => c.SentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClientTimesheetConfirmation>> GetExpiredConfirmationsAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(c => c.Timesheet).ThenInclude(t => t.Consultant)
            .Include(c => c.Timesheet).ThenInclude(t => t.Client)
            .Where(c => c.Status == TimesheetConfirmationStatus.Sent
                     && c.TokenExpiryDate <= now
                     && !c.IsDeleted)
            .OrderBy(c => c.TokenExpiryDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// TIMESHEET INVOICE REPOSITORY
// ============================================================================

#region Timesheet Invoice Repository

public class TimesheetInvoiceRepository : GenericRepository<TimesheetInvoice>, ITimesheetInvoiceRepository
{
    public TimesheetInvoiceRepository(ApplicationDbContext context) : base(context) { }

    public async Task<TimesheetInvoice?> GetByInvoiceNumberAsync(string invoiceNumber)
    {
        return await _dbSet
            .Include(i => i.Client)
            .Include(i => i.Consultant)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber && !i.IsDeleted);
    }

    public async Task<IEnumerable<TimesheetInvoice>> GetByClientIdAsync(Guid clientId)
    {
        return await _dbSet
            .Include(i => i.Consultant)
            .Where(i => i.ClientId == clientId && !i.IsDeleted)
            .OrderByDescending(i => i.BillingPeriodStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<TimesheetInvoice>> GetByConsultantIdAsync(Guid consultantId)
    {
        return await _dbSet
            .Include(i => i.Client)
            .Where(i => i.ConsultantId == consultantId && !i.IsDeleted)
            .OrderByDescending(i => i.BillingPeriodStart)
            .ToListAsync();
    }

    public async Task<IEnumerable<TimesheetInvoice>> GetByStatusAsync(TimesheetInvoiceStatus status)
    {
        return await _dbSet
            .Include(i => i.Client)
            .Include(i => i.Consultant)
            .Where(i => i.Status == status && !i.IsDeleted)
            .OrderByDescending(i => i.IssuedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TimesheetInvoice>> GetOutstandingInvoicesAsync(Guid clientId)
    {
        return await _dbSet
            .Include(i => i.Consultant)
            .Where(i => i.ClientId == clientId
                     && i.Status != TimesheetInvoiceStatus.Paid
                     && i.Status != TimesheetInvoiceStatus.Voided
                     && !i.IsDeleted)
            .OrderBy(i => i.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TimesheetInvoice>> GetOverdueInvoicesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Include(i => i.Client)
            .Include(i => i.Consultant)
            .Where(i => i.DueDate != null
                     && i.DueDate < today
                     && i.Status != TimesheetInvoiceStatus.Paid
                     && i.Status != TimesheetInvoiceStatus.Voided
                     && !i.IsDeleted)
            .OrderBy(i => i.DueDate)
            .ToListAsync();
    }

    public async Task<TimesheetInvoice?> GetWithLinkedTimesheetsAsync(Guid id)
    {
        return await _dbSet
            .Include(i => i.Client)
            .Include(i => i.Consultant)
            .Include(i => i.LinkedTimesheets).ThenInclude(l => l.Timesheet).ThenInclude(t => t.Entries)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
    }
}

#endregion

// ============================================================================
// TIMESHEET INVOICE LINK REPOSITORY
// ============================================================================

#region Timesheet Invoice Link Repository

public class TimesheetInvoiceLinkRepository : GenericRepository<TimesheetInvoiceLink>, ITimesheetInvoiceLinkRepository
{
    public TimesheetInvoiceLinkRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<TimesheetInvoiceLink>> GetByInvoiceIdAsync(Guid invoiceId)
    {
        return await _dbSet
            .Include(l => l.Timesheet).ThenInclude(t => t.Consultant)
            .Where(l => l.InvoiceId == invoiceId && !l.IsDeleted)
            .OrderBy(l => l.Timesheet.PeriodStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TimesheetInvoiceLink>> GetByTimesheetIdAsync(Guid timesheetId)
    {
        return await _dbSet
            .Include(l => l.Invoice).ThenInclude(i => i.Client)
            .Where(l => l.TimesheetId == timesheetId && !l.IsDeleted)
            .ToListAsync();
    }
}

#endregion
