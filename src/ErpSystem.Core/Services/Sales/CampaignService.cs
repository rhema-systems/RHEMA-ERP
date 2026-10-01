using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class CampaignService : ICampaignService
{
    private readonly IGenericRepository<Campaign> _campaignRepo;
    private readonly IGenericRepository<CampaignMember> _memberRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(
        IGenericRepository<Campaign> campaignRepo,
        IGenericRepository<CampaignMember> memberRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<CampaignService> logger)
    {
        _campaignRepo = campaignRepo;
        _memberRepo = memberRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region CRUD

    public async Task<CampaignDetailDto> CreateAsync(CreateCampaignDto dto)
    {
        var campaign = new Campaign
        {
            Name = dto.Name,
            CampaignType = dto.CampaignType,
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CampaignStatus = "Planning",
            Budget = dto.Budget,
            ExpectedRevenue = dto.ExpectedRevenue,
            ManagerId = dto.ManagerId ?? _currentUserProvider.UserId,
            TargetAudience = dto.TargetAudience,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _campaignRepo.AddAsync(campaign);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Campaign {CampaignName} of type {Type}", campaign.Name, campaign.CampaignType);
        return await GetByIdAsync(campaign.Id) ?? throw new InvalidOperationException("Failed to retrieve created Campaign");
    }

    public async Task<CampaignDetailDto> UpdateAsync(Guid id, UpdateCampaignDto dto)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Campaign {id} not found");

        if (dto.Name != null) campaign.Name = dto.Name;
        if (dto.CampaignType != null) campaign.CampaignType = dto.CampaignType;
        if (dto.Description != null) campaign.Description = dto.Description;
        if (dto.StartDate.HasValue) campaign.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue) campaign.EndDate = dto.EndDate;
        if (dto.Budget.HasValue) campaign.Budget = dto.Budget.Value;
        if (dto.ActualCost.HasValue) campaign.ActualCost = dto.ActualCost.Value;
        if (dto.ExpectedRevenue.HasValue) campaign.ExpectedRevenue = dto.ExpectedRevenue.Value;
        if (dto.ActualRevenue.HasValue) campaign.ActualRevenue = dto.ActualRevenue.Value;
        if (dto.ManagerId.HasValue) campaign.ManagerId = dto.ManagerId;
        if (dto.TargetAudience.HasValue) campaign.TargetAudience = dto.TargetAudience.Value;
        if (dto.ActualAudience.HasValue) campaign.ActualAudience = dto.ActualAudience.Value;
        if (dto.ResponseCount.HasValue) campaign.ResponseCount = dto.ResponseCount.Value;
        if (dto.LeadsGenerated.HasValue) campaign.LeadsGenerated = dto.LeadsGenerated.Value;
        if (dto.OpportunitiesGenerated.HasValue) campaign.OpportunitiesGenerated = dto.OpportunitiesGenerated.Value;
        if (dto.Notes != null) campaign.Notes = dto.Notes;

        await _campaignRepo.UpdateAsync(campaign);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated Campaign");
    }

    public async Task<CampaignDetailDto?> GetByIdAsync(Guid id)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id,
            c => c.Manager!,
            c => c.CampaignMembers);

        return campaign == null ? null : MapToDetailDto(campaign);
    }

    public async Task<PagedResult<CampaignSummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null, string? campaignType = null,
        Guid? managerId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _campaignRepo.GetQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c => c.Name.Contains(search) || (c.Description != null && c.Description.Contains(search)));
        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.CampaignStatus == status);
        if (!string.IsNullOrEmpty(campaignType))
            query = query.Where(c => c.CampaignType == campaignType);
        if (managerId.HasValue)
            query = query.Where(c => c.ManagerId == managerId.Value);
        if (startDate.HasValue)
            query = query.Where(c => c.StartDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(c => c.StartDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(c => c.Manager)
            .Include(c => c.CampaignMembers)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<CampaignSummaryDto>
        {
            Items = items.Select(MapToSummaryDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Lifecycle

    public async Task<CampaignDetailDto> ActivateAsync(Guid id)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Campaign {id} not found");

        if (campaign.CampaignStatus != "Planning" && campaign.CampaignStatus != "Paused")
            throw new InvalidOperationException($"Cannot activate campaign in {campaign.CampaignStatus} status");

        campaign.CampaignStatus = "Active";
        await _campaignRepo.UpdateAsync(campaign);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Campaign {CampaignName} activated", campaign.Name);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CampaignDetailDto> PauseAsync(Guid id)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Campaign {id} not found");

        if (campaign.CampaignStatus != "Active")
            throw new InvalidOperationException("Only active campaigns can be paused");

        campaign.CampaignStatus = "Paused";
        await _campaignRepo.UpdateAsync(campaign);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CampaignDetailDto> CompleteAsync(Guid id)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Campaign {id} not found");

        campaign.CampaignStatus = "Completed";
        campaign.EndDate ??= DateTime.UtcNow;

        await _campaignRepo.UpdateAsync(campaign);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Campaign {CampaignName} completed", campaign.Name);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<CampaignDetailDto> CancelAsync(Guid id, string? reason = null)
    {
        var campaign = await _campaignRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Campaign {id} not found");

        campaign.CampaignStatus = "Cancelled";
        if (reason != null) campaign.Notes = $"{campaign.Notes}\n[Cancelled] {reason}".Trim();

        await _campaignRepo.UpdateAsync(campaign);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    #endregion

    #region Member Management

    public async Task<CampaignMemberDto> AddMemberAsync(Guid campaignId, AddCampaignMemberDto dto)
    {
        var campaign = await _campaignRepo.GetByIdAsync(campaignId)
            ?? throw new InvalidOperationException($"Campaign {campaignId} not found");

        var member = new CampaignMember
        {
            CampaignId = campaignId,
            LeadId = dto.LeadId,
            CustomerId = dto.CustomerId,
            MemberStatus = "Active",
            DateAdded = DateTime.UtcNow,
            Notes = dto.Notes,
            TenantId = campaign.TenantId
        };

        await _memberRepo.AddAsync(member);
        await _unitOfWork.SaveChangesAsync();

        return new CampaignMemberDto
        {
            Id = member.Id,
            CampaignId = campaignId,
            LeadId = member.LeadId,
            CustomerId = member.CustomerId,
            MemberStatus = member.MemberStatus,
            DateAdded = member.DateAdded,
            Notes = member.Notes
        };
    }

    public async Task RemoveMemberAsync(Guid campaignId, Guid memberId)
    {
        var member = await _memberRepo.GetByIdAsync(memberId)
            ?? throw new InvalidOperationException($"Campaign member {memberId} not found");

        if (member.CampaignId != campaignId)
            throw new InvalidOperationException("Member does not belong to this campaign");

        await _memberRepo.DeleteAsync(member);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<List<CampaignMemberDto>> GetMembersAsync(Guid campaignId)
    {
        var members = await _memberRepo.GetQueryable()
            .Where(m => m.CampaignId == campaignId)
            .Include(m => m.Lead)
            .OrderByDescending(m => m.DateAdded)
            .ToListAsync();

        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, members.Select(member => member.CustomerId));
        return members.Select(m => new CampaignMemberDto
        {
            Id = m.Id,
            CampaignId = m.CampaignId,
            LeadId = m.LeadId,
            LeadName = m.Lead != null ? m.Lead.FullName : null,
            CustomerId = m.CustomerId,
            CustomerName = customerNames.GetValueOrDefault(m.CustomerId ?? Guid.Empty),
            MemberStatus = m.MemberStatus,
            DateAdded = m.DateAdded,
            ResponseDate = m.ResponseDate,
            ResponseType = m.ResponseType,
            Notes = m.Notes
        }).ToList();
    }

    #endregion

    #region Mapping

    private static CampaignSummaryDto MapToSummaryDto(Campaign c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        CampaignType = c.CampaignType,
        CampaignStatus = c.CampaignStatus,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        Budget = c.Budget,
        ActualCost = c.ActualCost,
        ExpectedRevenue = c.ExpectedRevenue,
        ActualRevenue = c.ActualRevenue,
        TargetAudience = c.TargetAudience,
        ResponseCount = c.ResponseCount,
        ResponseRate = c.ResponseRate,
        LeadsGenerated = c.LeadsGenerated,
        MemberCount = c.CampaignMembers?.Count ?? 0,
        ManagerName = c.Manager?.UserName,
        CreatedAt = c.CreatedAt
    };

    private static CampaignDetailDto MapToDetailDto(Campaign c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        CampaignType = c.CampaignType,
        CampaignStatus = c.CampaignStatus,
        Description = c.Description,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        Budget = c.Budget,
        ActualCost = c.ActualCost,
        ExpectedRevenue = c.ExpectedRevenue,
        ActualRevenue = c.ActualRevenue,
        ManagerId = c.ManagerId,
        ManagerName = c.Manager?.UserName,
        TargetAudience = c.TargetAudience,
        ActualAudience = c.ActualAudience,
        ResponseCount = c.ResponseCount,
        ResponseRate = c.ResponseRate,
        LeadsGenerated = c.LeadsGenerated,
        OpportunitiesGenerated = c.OpportunitiesGenerated,
        MemberCount = c.CampaignMembers?.Count ?? 0,
        Notes = c.Notes,
        CreatedAt = c.CreatedAt,
        Members = c.CampaignMembers?.Select(m => new CampaignMemberDto
        {
            Id = m.Id,
            CampaignId = m.CampaignId,
            LeadId = m.LeadId,
            CustomerId = m.CustomerId,
            MemberStatus = m.MemberStatus,
            DateAdded = m.DateAdded,
            ResponseDate = m.ResponseDate,
            ResponseType = m.ResponseType,
            Notes = m.Notes
        }).ToList() ?? new()
    };

    #endregion
}
