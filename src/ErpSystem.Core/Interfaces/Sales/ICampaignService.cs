using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Marketing Campaign management.
/// Handles full lifecycle: Planning → Active → Paused → Completed.
/// Manages campaign members and tracks metrics.
/// </summary>
public interface ICampaignService
{
    // ── CRUD ─────────────────────────────────────────────────────────────

    Task<CampaignDetailDto> CreateAsync(CreateCampaignDto dto);
    Task<CampaignDetailDto> UpdateAsync(Guid id, UpdateCampaignDto dto);
    Task<CampaignDetailDto?> GetByIdAsync(Guid id);
    Task<PagedResult<CampaignSummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null,
        string? status = null,
        string? campaignType = null,
        Guid? managerId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    // ── Lifecycle ────────────────────────────────────────────────────────

    Task<CampaignDetailDto> ActivateAsync(Guid id);
    Task<CampaignDetailDto> PauseAsync(Guid id);
    Task<CampaignDetailDto> CompleteAsync(Guid id);
    Task<CampaignDetailDto> CancelAsync(Guid id, string? reason = null);

    // ── Member Management ───────────────────────────────────────────────

    Task<CampaignMemberDto> AddMemberAsync(Guid campaignId, AddCampaignMemberDto dto);
    Task RemoveMemberAsync(Guid campaignId, Guid memberId);
    Task<List<CampaignMemberDto>> GetMembersAsync(Guid campaignId);
}
