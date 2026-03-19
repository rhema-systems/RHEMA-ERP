using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Sales Agreement management.
/// Handles full lifecycle: Draft → Approval → Active → Renewal/Expiry/Termination.
/// </summary>
public interface ISalesAgreementService
{
    // ── CRUD ─────────────────────────────────────────────────────────────

    Task<SalesAgreementDetailDto> GetByIdAsync(Guid id);
    Task<(List<SalesAgreementSummaryDto> Items, int TotalCount)> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null,
        string? status = null,
        string? agreementType = null,
        Guid? customerId = null,
        DateTime? startDateFrom = null,
        DateTime? startDateTo = null);

    Task<SalesAgreementDetailDto> CreateAsync(CreateSalesAgreementDto dto);
    Task<SalesAgreementDetailDto> UpdateAsync(Guid id, UpdateSalesAgreementDto dto);

    // ── Lifecycle ────────────────────────────────────────────────────────

    Task<SalesAgreementDetailDto> SubmitForApprovalAsync(Guid id);
    Task<SalesAgreementDetailDto> ProcessApprovalAsync(Guid id, SalesAgreementApprovalDto dto);
    Task<SalesAgreementDetailDto> ActivateAsync(Guid id);
    Task<SalesAgreementDetailDto> SuspendAsync(Guid id, string? reason = null);
    Task<SalesAgreementDetailDto> ResumeAsync(Guid id);
    Task<SalesAgreementDetailDto> TerminateAsync(Guid id, TerminateAgreementDto dto);
    Task<SalesAgreementDetailDto> RenewAsync(Guid id, RenewAgreementDto dto);

    // ── Milestones ──────────────────────────────────────────────────────

    Task<SalesAgreementMilestoneDto> UpdateMilestoneStatusAsync(Guid milestoneId, UpdateMilestoneStatusDto dto);

    // ── Queries ─────────────────────────────────────────────────────────

    Task<List<SalesAgreementSummaryDto>> GetExpiringAgreementsAsync(int daysAhead = 30);
    Task<List<SalesAgreementSummaryDto>> GetByCustomerAsync(Guid businessPartnerId);
}
