using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for CRM Activity management — calls, meetings, emails, tasks.
/// Provides timeline tracking for leads, customers, and opportunities.
/// </summary>
public interface IActivityService
{
    // ── CRUD ─────────────────────────────────────────────────────────────

    Task<ActivityDetailDto> CreateAsync(CreateActivityDto dto);
    Task<ActivityDetailDto> UpdateAsync(Guid id, UpdateActivityDto dto);
    Task<ActivityDetailDto?> GetByIdAsync(Guid id);
    Task<PagedResult<ActivitySummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null,
        string? activityType = null,
        string? status = null,
        Guid? assignedToId = null,
        Guid? leadId = null,
        Guid? customerId = null,
        Guid? opportunityId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    // ── Lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Mark an activity as completed with outcome
    /// </summary>
    Task<ActivityDetailDto> CompleteAsync(Guid id, string? outcome = null, string? notes = null);

    /// <summary>
    /// Cancel a planned activity
    /// </summary>
    Task<ActivityDetailDto> CancelAsync(Guid id, string? reason = null);

    // ── Timeline Queries ────────────────────────────────────────────────

    Task<List<ActivitySummaryDto>> GetTimelineAsync(Guid? leadId = null, Guid? customerId = null, Guid? opportunityId = null);
    Task<List<ActivitySummaryDto>> GetUpcomingAsync(int daysAhead = 7, Guid? assignedToId = null);
    Task<List<ActivitySummaryDto>> GetOverdueAsync(Guid? assignedToId = null);
}
