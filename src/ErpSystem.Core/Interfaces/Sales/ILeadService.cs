using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Lead management — the entry point of the CRM pipeline.
/// Handles full lifecycle: New → Qualified → Converted to Customer.
/// </summary>
public interface ILeadService
{
    // ── CRUD ─────────────────────────────────────────────────────────────

    Task<LeadDetailDto> CreateAsync(CreateLeadDto dto);
    Task<LeadDetailDto> UpdateAsync(Guid id, UpdateLeadDto dto);
    Task<LeadDetailDto?> GetByIdAsync(Guid id);
    Task<PagedResult<LeadSummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null,
        string? status = null,
        string? source = null,
        Guid? assignedToId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    // ── Lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Mark a lead as qualified — increases score and flags for conversion
    /// </summary>
    Task<LeadDetailDto> QualifyAsync(Guid id, int? score = null);

    /// <summary>
    /// Mark a lead as unqualified — dead end
    /// </summary>
    Task<LeadDetailDto> DisqualifyAsync(Guid id, string? reason = null);

    /// <summary>
    /// Convert a qualified lead into a Customer (BusinessPartner) and optionally create an Opportunity
    /// </summary>
    Task<LeadDetailDto> ConvertToCustomerAsync(Guid id, ConvertLeadDto dto);

    // ── Queries ──────────────────────────────────────────────────────────

    Task<List<LeadSummaryDto>> GetByStatusAsync(string status);
    Task<List<LeadSummaryDto>> GetUpcomingFollowUpsAsync(int daysAhead = 7);
}
