using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// CONSULTANT CLIENT SERVICE
// ============================================================================

#region Consultant Client Service

public interface IConsultantClientService
{
    Task<ConsultantClientDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ConsultantClientDto?> GetByClientCodeAsync(string clientCode, CancellationToken ct = default);
    Task<IEnumerable<ConsultantClientSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<ConsultantClientSummaryDto>> GetActiveClientsAsync(CancellationToken ct = default);
    Task<IEnumerable<ConsultantClientSummaryDto>> GetByIndustryAsync(string industry, CancellationToken ct = default);
    Task<ConsultantClientDto> GetWithEngagementsAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ConsultantClientSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<ConsultantClientDto> CreateAsync(CreateConsultantClientDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<ConsultantClientDto> UpdateAsync(UpdateConsultantClientDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // Engagement sub-operations
    Task<ClientEngagementDto> AddEngagementAsync(CreateClientEngagementDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ClientEngagementSummaryDto>> GetEngagementsAsync(Guid clientId, CancellationToken ct = default);
    Task<ClientEngagementDto> GetEngagementByIdAsync(Guid engagementId, CancellationToken ct = default);
    Task<ClientEngagementDto> UpdateEngagementAsync(UpdateClientEngagementDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteEngagementAsync(Guid engagementId, CancellationToken ct = default);
}

#endregion

// ============================================================================
// CLIENT ENGAGEMENT SERVICE
// ============================================================================

#region Client Engagement Service

public interface IClientEngagementService
{
    Task<ClientEngagementDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClientEngagementDto?> GetByEngagementCodeAsync(string engagementCode, CancellationToken ct = default);
    Task<IEnumerable<ClientEngagementSummaryDto>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task<IEnumerable<ClientEngagementSummaryDto>> GetByConsultantIdAsync(Guid consultantEmployeeId, CancellationToken ct = default);
    Task<IEnumerable<ClientEngagementSummaryDto>> GetByStatusAsync(ClientEngagementStatus status, CancellationToken ct = default);
    Task<IEnumerable<ClientEngagementSummaryDto>> GetActiveEngagementsAsync(CancellationToken ct = default);
    Task<IEnumerable<ClientEngagementSummaryDto>> GetByBillingCycleAsync(BillingCycle cycle, CancellationToken ct = default);
    Task<IEnumerable<ClientEngagementSummaryDto>> GetEngagementsEndingWithinAsync(int days, CancellationToken ct = default);
    Task<PagedResult<ClientEngagementSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<ClientEngagementDto> CreateAsync(CreateClientEngagementDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<ClientEngagementDto> UpdateAsync(UpdateClientEngagementDto dto, Guid userId, CancellationToken ct = default);
    Task<ClientEngagementDto> ActivateAsync(Guid engagementId, Guid userId, CancellationToken ct = default);
    Task<ClientEngagementDto> CompleteAsync(Guid engagementId, DateOnly actualEndDate, Guid userId, CancellationToken ct = default);
    Task<ClientEngagementDto> SuspendAsync(Guid engagementId, Guid userId, CancellationToken ct = default);
    Task<ClientEngagementDto> ResumeAsync(Guid engagementId, Guid userId, CancellationToken ct = default);
    Task<ClientEngagementDto> TerminateAsync(Guid engagementId, string? reason, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// CONSULTANT TIMESHEET SERVICE
// ============================================================================

#region Consultant Timesheet Service

public interface IConsultantTimesheetService
{
    Task<ConsultantTimesheetDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ConsultantTimesheetDto?> GetByTimesheetNumberAsync(string timesheetNumber, CancellationToken ct = default);
    Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetByConsultantIdAsync(Guid consultantEmployeeId, CancellationToken ct = default);
    Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetByEngagementIdAsync(Guid engagementId, CancellationToken ct = default);
    Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetByStatusAsync(TimesheetStatus status, CancellationToken ct = default);
    Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetPendingApprovalAsync(CancellationToken ct = default);
    Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetPendingClientConfirmationAsync(CancellationToken ct = default);
    Task<IEnumerable<ConsultantTimesheetSummaryDto>> GetApprovedForInvoicingAsync(Guid? clientId = null, CancellationToken ct = default);
    Task<PagedResult<ConsultantTimesheetSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<ConsultantTimesheetDto> CreateAsync(CreateConsultantTimesheetDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<ConsultantTimesheetDto> UpdateAsync(UpdateConsultantTimesheetDto dto, Guid userId, CancellationToken ct = default);
    Task<ConsultantTimesheetDto> SubmitAsync(Guid timesheetId, Guid userId, CancellationToken ct = default);
    Task<ConsultantTimesheetDto> ApproveAsync(Guid timesheetId, string? comments, Guid userId, CancellationToken ct = default);
    Task<ConsultantTimesheetDto> RejectAsync(Guid timesheetId, string rejectionReason, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // Entry sub-operations
    Task<ConsultantTimesheetEntryDto> AddEntryAsync(CreateConsultantTimesheetEntryDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ConsultantTimesheetEntryDto>> GetEntriesAsync(Guid timesheetId, CancellationToken ct = default);
    Task<ConsultantTimesheetEntryDto> UpdateEntryAsync(UpdateConsultantTimesheetEntryDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteEntryAsync(Guid entryId, CancellationToken ct = default);

    // Client confirmation sub-operations
    Task<SendTimesheetConfirmationResultDto> SendConfirmationAsync(
        Guid timesheetId,
        SendTimesheetConfirmationRequestDto request,
        Guid tenantId,
        Guid sentById,
        CancellationToken ct = default);
    Task<SendTimesheetConfirmationResultDto> ResendConfirmationAsync(
        Guid timesheetId,
        Guid sentById,
        CancellationToken ct = default);
    Task<ClientTimesheetConfirmationDto?> GetConfirmationAsync(Guid timesheetId, CancellationToken ct = default);
    // Tenant is resolved from the globally-unique one-time token itself, so these public
    // (emailed-link) flows do not require an X-Tenant-Id header.
    Task<ClientTimesheetConfirmationPublicDto> ValidateConfirmationTokenAsync(
        Guid token,
        CancellationToken ct = default);
    Task<ClientTimesheetConfirmationDto> ConfirmByClientAsync(
        ClientConfirmTimesheetDto dto,
        CancellationToken ct = default);
    Task<ClientTimesheetConfirmationDto> RejectByClientAsync(
        ClientRejectTimesheetDto dto,
        CancellationToken ct = default);
    Task<ClientTimesheetConfirmationDto> ConfirmByPortalClientAsync(
        Guid timesheetId,
        Guid consultantClientId,
        string? clientNotes,
        CancellationToken ct = default);
    Task<ClientTimesheetConfirmationDto> RejectByPortalClientAsync(
        Guid timesheetId,
        Guid consultantClientId,
        string clientNotes,
        CancellationToken ct = default);
}

#endregion

// ============================================================================
// TIMESHEET INVOICE SERVICE
// ============================================================================

#region Timesheet Invoice Service

public interface ITimesheetInvoiceService
{
    Task<TimesheetInvoiceDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TimesheetInvoiceDto?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken ct = default);
    Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetByEngagementIdAsync(Guid engagementId, CancellationToken ct = default);
    Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetByClientIdAsync(Guid clientId, CancellationToken ct = default);
    Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetByStatusAsync(TimesheetInvoiceStatus status, CancellationToken ct = default);
    Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetOverdueInvoicesAsync(CancellationToken ct = default);
    Task<IEnumerable<TimesheetInvoiceSummaryDto>> GetByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<PagedResult<TimesheetInvoiceSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    /// <summary>Creates an invoice by pulling all approved timesheets for the engagement and period.</summary>
    Task<TimesheetInvoiceDto> GenerateAsync(CreateTimesheetInvoiceDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<TimesheetInvoiceDto> UpdateAsync(UpdateTimesheetInvoiceDto dto, Guid userId, CancellationToken ct = default);
    Task<TimesheetInvoiceDto> SendAsync(Guid invoiceId, Guid userId, CancellationToken ct = default);
    Task<TimesheetInvoiceDto> MarkPaidAsync(Guid invoiceId, DateOnly paidDate, decimal paidAmount, Guid userId, CancellationToken ct = default);
    Task<TimesheetInvoiceDto> VoidAsync(Guid invoiceId, string reason, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion
