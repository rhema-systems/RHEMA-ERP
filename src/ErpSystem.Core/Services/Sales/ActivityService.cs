using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class ActivityService : IActivityService
{
    private readonly IGenericRepository<Activity> _activityRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(
        IGenericRepository<Activity> activityRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ActivityService> logger)
    {
        _activityRepo = activityRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region CRUD

    public async Task<ActivityDetailDto> CreateAsync(CreateActivityDto dto)
    {
        var activity = new Activity
        {
            Subject = dto.Subject,
            ActivityType = dto.ActivityType,
            Description = dto.Description,
            ActivityDate = dto.ActivityDate ?? DateTime.UtcNow,
            DueDate = dto.DueDate,
            ActivityStatus = "Planned",
            Priority = dto.Priority,
            Duration = dto.Duration,
            AssignedToId = dto.AssignedToId ?? _currentUserProvider.UserId,
            LeadId = dto.LeadId,
            CustomerId = dto.CustomerId,
            OpportunityId = dto.OpportunityId,
            Location = dto.Location,
            Attendees = dto.Attendees,
            Notes = dto.Notes,
            TenantId = _currentUserProvider.TenantId
        };

        await _activityRepo.AddAsync(activity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created {Type} activity: {Subject}", activity.ActivityType, activity.Subject);
        return await GetByIdAsync(activity.Id) ?? throw new InvalidOperationException("Failed to retrieve created Activity");
    }

    public async Task<ActivityDetailDto> UpdateAsync(Guid id, UpdateActivityDto dto)
    {
        var activity = await _activityRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Activity {id} not found");

        if (dto.Subject != null) activity.Subject = dto.Subject;
        if (dto.ActivityType != null) activity.ActivityType = dto.ActivityType;
        if (dto.Description != null) activity.Description = dto.Description;
        if (dto.ActivityDate.HasValue) activity.ActivityDate = dto.ActivityDate.Value;
        if (dto.DueDate.HasValue) activity.DueDate = dto.DueDate;
        if (dto.ActivityStatus != null) activity.ActivityStatus = dto.ActivityStatus;
        if (dto.Priority.HasValue) activity.Priority = dto.Priority.Value;
        if (dto.Duration.HasValue) activity.Duration = dto.Duration;
        if (dto.AssignedToId.HasValue) activity.AssignedToId = dto.AssignedToId;
        if (dto.Location != null) activity.Location = dto.Location;
        if (dto.Attendees != null) activity.Attendees = dto.Attendees;
        if (dto.Outcome != null) activity.Outcome = dto.Outcome;
        if (dto.Notes != null) activity.Notes = dto.Notes;
        if (dto.RequiresFollowUp.HasValue) activity.RequiresFollowUp = dto.RequiresFollowUp.Value;
        if (dto.NextFollowUpDate.HasValue) activity.NextFollowUpDate = dto.NextFollowUpDate;

        await _activityRepo.UpdateAsync(activity);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated Activity");
    }

    public async Task<ActivityDetailDto?> GetByIdAsync(Guid id)
    {
        var activity = await _activityRepo.GetByIdAsync(id,
            a => a.AssignedTo!,
            a => a.Lead!,
            a => a.Opportunity!);

        if (activity == null) return null;
        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, new[] { activity.CustomerId });
        return MapToDetailDto(activity, customerNames);
    }

    public async Task<PagedResult<ActivitySummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? activityType = null, string? status = null,
        Guid? assignedToId = null, Guid? leadId = null, Guid? customerId = null,
        Guid? opportunityId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _activityRepo.GetQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(a => a.Subject.Contains(search) || (a.Description != null && a.Description.Contains(search)));
        if (!string.IsNullOrEmpty(activityType))
            query = query.Where(a => a.ActivityType == activityType);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(a => a.ActivityStatus == status);
        if (assignedToId.HasValue)
            query = query.Where(a => a.AssignedToId == assignedToId.Value);
        if (leadId.HasValue)
            query = query.Where(a => a.LeadId == leadId.Value);
        if (customerId.HasValue)
            query = query.Where(a => a.CustomerId == customerId.Value);
        if (opportunityId.HasValue)
            query = query.Where(a => a.OpportunityId == opportunityId.Value);
        if (startDate.HasValue)
            query = query.Where(a => a.ActivityDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(a => a.ActivityDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(a => a.AssignedTo)
            .Include(a => a.Lead)
            .Include(a => a.Opportunity)
            .OrderByDescending(a => a.ActivityDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, items.Select(item => item.CustomerId));

        return new PagedResult<ActivitySummaryDto>
        {
            Items = items.Select(item => MapToSummaryDto(item, customerNames)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Lifecycle

    public async Task<ActivityDetailDto> CompleteAsync(Guid id, string? outcome = null, string? notes = null)
    {
        var activity = await _activityRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Activity {id} not found");

        activity.ActivityStatus = "Completed";
        if (outcome != null) activity.Outcome = outcome;
        if (notes != null) activity.Notes = $"{activity.Notes}\n[Completed] {notes}".Trim();

        await _activityRepo.UpdateAsync(activity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Activity {Subject} completed with outcome: {Outcome}", activity.Subject, outcome);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<ActivityDetailDto> CancelAsync(Guid id, string? reason = null)
    {
        var activity = await _activityRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Activity {id} not found");

        activity.ActivityStatus = "Cancelled";
        if (reason != null) activity.Notes = $"{activity.Notes}\n[Cancelled] {reason}".Trim();

        await _activityRepo.UpdateAsync(activity);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    #endregion

    #region Timeline Queries

    public async Task<List<ActivitySummaryDto>> GetTimelineAsync(Guid? leadId = null, Guid? customerId = null, Guid? opportunityId = null)
    {
        var query = _activityRepo.GetQueryable();

        if (leadId.HasValue)
            query = query.Where(a => a.LeadId == leadId.Value);
        if (customerId.HasValue)
            query = query.Where(a => a.CustomerId == customerId.Value);
        if (opportunityId.HasValue)
            query = query.Where(a => a.OpportunityId == opportunityId.Value);

        var items = await query
            .Include(a => a.AssignedTo)
            .OrderByDescending(a => a.ActivityDate)
            .Take(50)
            .ToListAsync();

        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, items.Select(item => item.CustomerId));
        return items.Select(item => MapToSummaryDto(item, customerNames)).ToList();
    }

    public async Task<List<ActivitySummaryDto>> GetUpcomingAsync(int daysAhead = 7, Guid? assignedToId = null)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        var query = _activityRepo.GetQueryable()
            .Where(a => a.ActivityStatus == "Planned" && a.DueDate.HasValue && a.DueDate <= cutoff);

        if (assignedToId.HasValue)
            query = query.Where(a => a.AssignedToId == assignedToId.Value);

        var items = await query
            .Include(a => a.AssignedTo)
            .Include(a => a.Lead)
            .OrderBy(a => a.DueDate)
            .ToListAsync();

        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, items.Select(item => item.CustomerId));
        return items.Select(item => MapToSummaryDto(item, customerNames)).ToList();
    }

    public async Task<List<ActivitySummaryDto>> GetOverdueAsync(Guid? assignedToId = null)
    {
        var query = _activityRepo.GetQueryable()
            .Where(a => a.ActivityStatus == "Planned" && a.DueDate.HasValue && a.DueDate < DateTime.UtcNow);

        if (assignedToId.HasValue)
            query = query.Where(a => a.AssignedToId == assignedToId.Value);

        var items = await query
            .Include(a => a.AssignedTo)
            .Include(a => a.Lead)
            .OrderBy(a => a.DueDate)
            .ToListAsync();

        var customerNames = await SalesBusinessPartnerNames.LoadAsync(_unitOfWork, _currentUserProvider.TenantId, items.Select(item => item.CustomerId));
        return items.Select(item => MapToSummaryDto(item, customerNames)).ToList();
    }

    #endregion

    #region Mapping

    private static ActivitySummaryDto MapToSummaryDto(Activity a, IReadOnlyDictionary<Guid, string> customerNames) => new()
    {
        Id = a.Id,
        Subject = a.Subject,
        ActivityType = a.ActivityType,
        ActivityDate = a.ActivityDate,
        DueDate = a.DueDate,
        ActivityStatus = a.ActivityStatus,
        Priority = a.Priority,
        AssignedToName = a.AssignedTo?.UserName,
        LeadName = a.Lead != null ? a.Lead.FullName : null,
        CustomerName = customerNames.GetValueOrDefault(a.CustomerId ?? Guid.Empty),
        OpportunityName = a.Opportunity?.Name,
        Outcome = a.Outcome,
        RequiresFollowUp = a.RequiresFollowUp,
        CreatedAt = a.CreatedAt
    };

    private static ActivityDetailDto MapToDetailDto(Activity a, IReadOnlyDictionary<Guid, string> customerNames) => new()
    {
        Id = a.Id,
        Subject = a.Subject,
        ActivityType = a.ActivityType,
        Description = a.Description,
        ActivityDate = a.ActivityDate,
        DueDate = a.DueDate,
        ActivityStatus = a.ActivityStatus,
        Priority = a.Priority,
        Duration = a.Duration,
        AssignedToId = a.AssignedToId,
        AssignedToName = a.AssignedTo?.UserName,
        LeadId = a.LeadId,
        LeadName = a.Lead != null ? a.Lead.FullName : null,
        CustomerId = a.CustomerId,
        CustomerName = customerNames.GetValueOrDefault(a.CustomerId ?? Guid.Empty),
        OpportunityId = a.OpportunityId,
        OpportunityName = a.Opportunity?.Name,
        Location = a.Location,
        Attendees = a.Attendees,
        Outcome = a.Outcome,
        Notes = a.Notes,
        RequiresFollowUp = a.RequiresFollowUp,
        NextFollowUpDate = a.NextFollowUpDate,
        CreatedAt = a.CreatedAt
    };

    #endregion
}
