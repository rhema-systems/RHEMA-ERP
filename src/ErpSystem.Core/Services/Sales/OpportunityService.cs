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
        var stage = await ResolveStageAsync(dto.StageDefinitionId, dto.Stage, useFirstOpenStageWhenEmpty: true);
        var opp = new Opportunity
        {
            Name = dto.Name,
            Description = dto.Description,
            CustomerId = dto.CustomerId,
            LeadId = dto.LeadId,
            StageDefinitionId = stage.Id,
            Stage = stage.Name,
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
        if (stage.IsClosed)
            opp.ActualCloseDate = DateTime.UtcNow;

        await _opportunityRepo.AddAsync(opp);
        await AddStageHistoryAsync(opp, stage.Id);
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
        OpportunityStageDefinition? targetStage = null;
        if (dto.StageDefinitionId.HasValue || !string.IsNullOrWhiteSpace(dto.Stage))
            targetStage = await ResolveStageAsync(dto.StageDefinitionId, dto.Stage);
        if (dto.Probability.HasValue) opp.Probability = dto.Probability.Value;
        if (dto.Amount.HasValue) opp.Amount = dto.Amount.Value;
        if (dto.ExpectedCloseDate.HasValue) opp.ExpectedCloseDate = dto.ExpectedCloseDate.Value;
        if (dto.LeadSource != null) opp.LeadSource = dto.LeadSource;
        if (dto.OpportunityType != null) opp.OpportunityType = dto.OpportunityType;
        if (dto.AssignedToId.HasValue) opp.AssignedToId = dto.AssignedToId;
        if (dto.Competitors != null) opp.Competitors = dto.Competitors;
        if (dto.Notes != null) opp.Notes = dto.Notes;

        if (targetStage is not null && targetStage.Id != opp.StageDefinitionId)
            await ApplyStageTransitionAsync(opp, targetStage);

        await _opportunityRepo.UpdateAsync(opp);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated Opportunity");
    }

    public async Task<OpportunityDetailDto?> GetByIdAsync(Guid id)
    {
        var opp = await _opportunityRepo.GetByIdAsync(id,
            o => o.Lead!,
            o => o.AssignedTo!,
            o => o.StageDefinition!,
            o => o.Quotes,
            o => o.Activities);

        if (opp == null) return null;
        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, new[] { opp.CustomerId });
        return MapToDetailDto(opp, customerNames);
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
            .Include(o => o.Lead)
            .Include(o => o.AssignedTo)
            .Include(o => o.Quotes)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, items.Select(item => item.CustomerId));

        return new PagedResult<OpportunitySummaryDto>
        {
            Items = items.Select(item => MapToSummaryDto(item, customerNames)).ToList(),
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

        var currentStage = await ResolveCurrentStageAsync(opp);
        if (currentStage.IsClosed)
            throw new InvalidOperationException("Cannot advance a closed opportunity");

        var previousStage = opp.Stage;
        var targetStage = await ResolveStageAsync(null, newStage);
        await ApplyStageTransitionAsync(opp, targetStage);

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

        var wonStage = await GetOutcomeStageAsync(won: true);
        await ApplyStageTransitionAsync(opp, wonStage);
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

        var lostStage = await GetOutcomeStageAsync(won: false);
        await ApplyStageTransitionAsync(opp, lostStage);
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
            .Where(o => o.StageDefinitionId.HasValue && o.StageDefinition != null && !o.StageDefinition.IsClosed);

        if (assignedToId.HasValue)
            query = query.Where(o => o.AssignedToId == assignedToId.Value);

        var items = await query
            .Include(o => o.AssignedTo)
            .OrderBy(o => o.ExpectedCloseDate)
            .ToListAsync();

        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, items.Select(item => item.CustomerId));
        return items.Select(item => MapToSummaryDto(item, customerNames)).ToList();
    }

    public async Task<List<OpportunitySummaryDto>> GetByCustomerAsync(Guid customerId)
    {
        var items = await _opportunityRepo.GetQueryable()
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.AssignedTo)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, items.Select(item => item.CustomerId));
        return items.Select(item => MapToSummaryDto(item, customerNames)).ToList();
    }

    #endregion

    #region Mapping

    private static OpportunitySummaryDto MapToSummaryDto(Opportunity o, IReadOnlyDictionary<Guid, string> customerNames) => new()
    {
        Id = o.Id,
        Name = o.Name,
        StageDefinitionId = o.StageDefinitionId,
        Stage = o.Stage,
        Probability = o.Probability,
        Amount = o.Amount,
        Currency = o.Currency,
        ExpectedCloseDate = o.ExpectedCloseDate,
        ActualCloseDate = o.ActualCloseDate,
        CustomerName = customerNames.GetValueOrDefault(o.CustomerId ?? Guid.Empty),
        LeadName = o.Lead != null ? o.Lead.FullName : null,
        AssignedToName = o.AssignedTo?.UserName,
        OpportunityType = o.OpportunityType,
        LeadSource = o.LeadSource,
        QuoteCount = o.Quotes?.Count ?? 0,
        CreatedAt = o.CreatedAt
    };

    private static OpportunityDetailDto MapToDetailDto(Opportunity o, IReadOnlyDictionary<Guid, string> customerNames) => new()
    {
        Id = o.Id,
        Name = o.Name,
        StageDefinitionId = o.StageDefinitionId,
        Description = o.Description,
        Stage = o.Stage,
        Probability = o.Probability,
        Amount = o.Amount,
        Currency = o.Currency,
        ExpectedCloseDate = o.ExpectedCloseDate,
        ActualCloseDate = o.ActualCloseDate,
        CustomerId = o.CustomerId,
        CustomerName = customerNames.GetValueOrDefault(o.CustomerId ?? Guid.Empty),
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

    private async Task<OpportunityStageDefinition> ResolveStageAsync(
        Guid? stageDefinitionId,
        string? stage,
        bool useFirstOpenStageWhenEmpty = false)
    {
        var tenantId = _currentUserProvider.TenantId;
        var stages = _unitOfWork.Repository<OpportunityStageDefinition>().GetQueryable()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive);

        OpportunityStageDefinition? resolved = null;
        if (stageDefinitionId.HasValue)
        {
            resolved = await stages.SingleOrDefaultAsync(value => value.Id == stageDefinitionId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(stage))
        {
            var requested = stage.Trim();
            resolved = await stages.SingleOrDefaultAsync(value =>
                value.Name == requested || value.Code == requested);
        }
        else if (useFirstOpenStageWhenEmpty)
        {
            resolved = await stages
                .Where(value => !value.IsClosed)
                .OrderBy(value => value.SortOrder)
                .ThenBy(value => value.Name)
                .FirstOrDefaultAsync();
        }

        return resolved ?? throw new InvalidOperationException(
            "Select an active configured opportunity stage before saving the opportunity.");
    }

    private async Task<OpportunityStageDefinition> ResolveCurrentStageAsync(Opportunity opportunity)
    {
        if (opportunity.StageDefinitionId.HasValue)
            return await ResolveStageAsync(opportunity.StageDefinitionId, null);

        return await ResolveStageAsync(null, opportunity.Stage);
    }

    private async Task<OpportunityStageDefinition> GetOutcomeStageAsync(bool won)
    {
        var tenantId = _currentUserProvider.TenantId;
        var matches = await _unitOfWork.Repository<OpportunityStageDefinition>().GetQueryable()
            .Where(value => value.TenantId == tenantId && !value.IsDeleted && value.IsActive
                && value.IsClosed && (won ? value.IsWon : value.IsLost))
            .OrderBy(value => value.SortOrder)
            .Take(2)
            .ToListAsync();
        if (matches.Count != 1)
            throw new InvalidOperationException(won
                ? "Configure exactly one active Won opportunity stage before closing this opportunity."
                : "Configure exactly one active Lost opportunity stage before closing this opportunity.");
        return matches[0];
    }

    private async Task ApplyStageTransitionAsync(Opportunity opportunity, OpportunityStageDefinition stage)
    {
        if (opportunity.StageDefinitionId == stage.Id) return;

        opportunity.StageDefinitionId = stage.Id;
        opportunity.Stage = stage.Name;
        if (stage.DefaultProbability.HasValue)
            opportunity.Probability = stage.DefaultProbability.Value;
        opportunity.ActualCloseDate = stage.IsClosed ? DateTime.UtcNow : null;
        await AddStageHistoryAsync(opportunity, stage.Id);
    }

    private Task AddStageHistoryAsync(Opportunity opportunity, Guid stageDefinitionId)
    {
        return _unitOfWork.Repository<OpportunityStageHistory>().AddAsync(new OpportunityStageHistory
        {
            TenantId = _currentUserProvider.TenantId,
            OpportunityId = opportunity.Id,
            StageDefinitionId = stageDefinitionId,
            EnteredAt = DateTime.UtcNow,
            AmountSnapshot = opportunity.Amount,
            CurrencySnapshot = NormalizeCurrency(opportunity.Currency),
            ProbabilitySnapshot = Math.Clamp(opportunity.Probability, 0, 100),
            IsLegacySnapshot = false
        });
    }

    private static string? NormalizeCurrency(string? currency)
    {
        var code = currency?.Trim().ToUpperInvariant();
        return code is { Length: 3 } && code.All(character => character is >= 'A' and <= 'Z')
            ? code
            : null;
    }

    #endregion
}
