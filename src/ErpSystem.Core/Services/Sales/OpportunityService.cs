using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class OpportunityService : IOpportunityService
{
    private readonly IGenericRepository<Opportunity> _opportunityRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OpportunityService> _logger;

    public OpportunityService(
        IGenericRepository<Opportunity> opportunityRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<OpportunityService> logger)
    {
        _opportunityRepo = opportunityRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region CRUD

    public async Task<OpportunityDetailDto> CreateAsync(CreateOpportunityDto dto)
    {
        var opp = new Opportunity
        {
            Name = dto.Name,
            Description = dto.Description,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            Stage = dto.Stage,
            Probability = dto.Probability,
            Amount = dto.Amount,
            Currency = dto.Currency ?? "USD",
            ExpectedCloseDate = dto.ExpectedCloseDate,
            LeadSource = dto.LeadSource ?? "Unknown",
            OpportunityType = dto.OpportunityType ?? "New Business",
            AssignedToId = dto.AssignedToId,
            Competitors = dto.Competitors,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _opportunityRepo.AddAsync(opp);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Opportunity {OppName} worth {Amount} {Currency}", opp.Name, opp.Amount, opp.Currency);
        return await GetByIdAsync(opp.Id) ?? throw new InvalidOperationException("Failed to retrieve created Opportunity");
    }

    public async Task<OpportunityDetailDto> UpdateAsync(Guid id, UpdateOpportunityDto dto)
    {
        var opp = await _opportunityRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Opportunity {id} not found");

        if (dto.Name != null) opp.Name = dto.Name;
        if (dto.Description != null) opp.Description = dto.Description;
        if (dto.CustomerId.HasValue) opp.CustomerId = dto.CustomerId;
        if (dto.Stage != null) opp.Stage = dto.Stage;
        if (dto.Probability.HasValue) opp.Probability = dto.Probability.Value;
        if (dto.Amount.HasValue) opp.Amount = dto.Amount.Value;
        if (dto.ExpectedCloseDate.HasValue) opp.ExpectedCloseDate = dto.ExpectedCloseDate.Value;
        if (dto.LeadSource != null) opp.LeadSource = dto.LeadSource;
        if (dto.OpportunityType != null) opp.OpportunityType = dto.OpportunityType;
        if (dto.AssignedToId.HasValue) opp.AssignedToId = dto.AssignedToId;
        if (dto.Competitors != null) opp.Competitors = dto.Competitors;
        if (dto.Notes != null) opp.Notes = dto.Notes;

        await _opportunityRepo.UpdateAsync(opp);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated Opportunity");
    }

    public async Task<OpportunityDetailDto?> GetByIdAsync(Guid id)
    {
        var opp = await _opportunityRepo.GetByIdAsync(id,
            o => o.Customer!,
            o => o.Lead!,
            o => o.AssignedTo!,
            o => o.Quotes,
            o => o.Activities);

        return opp == null ? null : MapToDetailDto(opp);
    }

    public async Task<PagedResult<OpportunitySummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? stage = null, Guid? customerId = null,
        Guid? assignedToId = null, string? opportunityType = null,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _opportunityRepo.GetQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(o => o.Name.Contains(search) || (o.Description != null && o.Description.Contains(search)));
        if (!string.IsNullOrEmpty(stage))
            query = query.Where(o => o.Stage == stage);
        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);
        if (assignedToId.HasValue)
            query = query.Where(o => o.AssignedToId == assignedToId.Value);
        if (!string.IsNullOrEmpty(opportunityType))
            query = query.Where(o => o.OpportunityType == opportunityType);
        if (startDate.HasValue)
            query = query.Where(o => o.CreatedAt >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(o => o.CreatedAt <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedTo)
            .Include(o => o.Quotes)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<OpportunitySummaryDto>
        {
            Items = items.Select(MapToSummaryDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Pipeline Stage Transitions

    public async Task<OpportunityDetailDto> AdvanceStageAsync(Guid id, string newStage, string? notes = null)
    {
        var opp = await _opportunityRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Opportunity {id} not found");

        if (opp.Stage == "Closed Won" || opp.Stage == "Closed Lost")
            throw new InvalidOperationException("Cannot advance a closed opportunity");

        var previousStage = opp.Stage;
        opp.Stage = newStage;

        // Auto-adjust probability based on stage
        opp.Probability = newStage switch
        {
            "Prospecting" => 10,
            "Qualification" => 20,
            "Needs Analysis" => 40,
            "Value Proposition" => 50,
            "Proposal" => 60,
            "Negotiation" => 80,
            _ => opp.Probability
        };

        if (notes != null) opp.Notes = $"{opp.Notes}\n[{previousStage} → {newStage}] {notes}".Trim();

        await _opportunityRepo.UpdateAsync(opp);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Opportunity {OppName} advanced from {From} to {To}", opp.Name, previousStage, newStage);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<OpportunityDetailDto> CloseWonAsync(Guid id, string? notes = null)
    {
        var opp = await _opportunityRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Opportunity {id} not found");

        opp.Stage = "Closed Won";
        opp.Probability = 100;
        opp.ActualCloseDate = DateTime.UtcNow;
        if (notes != null) opp.Notes = $"{opp.Notes}\n[Won] {notes}".Trim();

        await _opportunityRepo.UpdateAsync(opp);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Opportunity {OppName} closed as Won — {Amount} {Currency}", opp.Name, opp.Amount, opp.Currency);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<OpportunityDetailDto> CloseLostAsync(Guid id, CloseOpportunityDto dto)
    {
        var opp = await _opportunityRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Opportunity {id} not found");

        opp.Stage = "Closed Lost";
        opp.Probability = 0;
        opp.ActualCloseDate = DateTime.UtcNow;
        opp.LossReason = dto.LossReason;
        if (dto.Notes != null) opp.Notes = $"{opp.Notes}\n[Lost] {dto.Notes}".Trim();

        await _opportunityRepo.UpdateAsync(opp);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Opportunity {OppName} closed as Lost: {Reason}", opp.Name, dto.LossReason);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    #endregion

    #region Queries

    public async Task<List<OpportunitySummaryDto>> GetPipelineAsync(Guid? assignedToId = null)
    {
        var query = _opportunityRepo.GetQueryable()
            .Where(o => o.Stage != "Closed Won" && o.Stage != "Closed Lost");

        if (assignedToId.HasValue)
            query = query.Where(o => o.AssignedToId == assignedToId.Value);

        var items = await query
            .Include(o => o.Customer)
            .Include(o => o.AssignedTo)
            .OrderBy(o => o.ExpectedCloseDate)
            .ToListAsync();

        return items.Select(MapToSummaryDto).ToList();
    }

    public async Task<List<OpportunitySummaryDto>> GetByCustomerAsync(Guid customerId)
    {
        var items = await _opportunityRepo.GetQueryable()
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.Customer)
            .Include(o => o.AssignedTo)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return items.Select(MapToSummaryDto).ToList();
    }

    #endregion

    #region Mapping

    private static OpportunitySummaryDto MapToSummaryDto(Opportunity o) => new()
    {
        Id = o.Id,
        Name = o.Name,
        Stage = o.Stage,
        Probability = o.Probability,
        Amount = o.Amount,
        Currency = o.Currency,
        ExpectedCloseDate = o.ExpectedCloseDate,
        ActualCloseDate = o.ActualCloseDate,
        CustomerName = o.Customer?.CustomerName,
        LeadName = o.Lead != null ? o.Lead.FullName : null,
        AssignedToName = o.AssignedTo?.UserName,
        OpportunityType = o.OpportunityType,
        LeadSource = o.LeadSource,
        QuoteCount = o.Quotes?.Count ?? 0,
        CreatedAt = o.CreatedAt
    };

    private static OpportunityDetailDto MapToDetailDto(Opportunity o) => new()
    {
        Id = o.Id,
        Name = o.Name,
        Description = o.Description,
        Stage = o.Stage,
        Probability = o.Probability,
        Amount = o.Amount,
        Currency = o.Currency,
        ExpectedCloseDate = o.ExpectedCloseDate,
        ActualCloseDate = o.ActualCloseDate,
        CustomerId = o.CustomerId,
        CustomerName = o.Customer?.CustomerName,
        LeadId = o.LeadId,
        LeadName = o.Lead != null ? o.Lead.FullName : null,
        AssignedToId = o.AssignedToId,
        AssignedToName = o.AssignedTo?.UserName,
        OpportunityType = o.OpportunityType,
        LeadSource = o.LeadSource,
        Competitors = o.Competitors,
        Notes = o.Notes,
        LossReason = o.LossReason,
        QuoteCount = o.Quotes?.Count ?? 0,
        CreatedAt = o.CreatedAt,
        Quotes = o.Quotes?.Select(q => new QuoteSummaryDto
        {
            Id = q.Id,
            DocumentNumber = q.DocumentNumber,
            QuoteName = q.QuoteName,
            QuoteStatus = q.QuoteStatus,
            TotalAmount = q.TotalAmount,
            TaxAmount = q.TaxAmount,
            ValidUntil = q.ValidUntil,
            SentDate = q.SentDate,
            AcceptedDate = q.AcceptedDate,
            LineCount = q.LineItems?.Count ?? 0,
            CreatedAt = q.CreatedAt
        }).ToList() ?? new(),
        Activities = o.Activities?.Select(a => new ActivitySummaryDto
        {
            Id = a.Id,
            Subject = a.Subject,
            ActivityType = a.ActivityType,
            ActivityDate = a.ActivityDate,
            ActivityStatus = a.ActivityStatus,
            Priority = a.Priority,
            Outcome = a.Outcome,
            CreatedAt = a.CreatedAt
        }).ToList() ?? new()
    };

    #endregion
}
