using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 1: CORE TRAVEL REQUEST SERVICE
// ============================================================================

#region Staff Travel Request Service

public class StaffTravelRequestService : IStaffTravelRequestService
{
    private readonly IStaffTravelRequestRepository _requestRepository;
    private readonly IStaffTravelRequestCommentRepository _commentRepository;
    private readonly IStaffTravelRequestAttachmentRepository _attachmentRepository;
    private readonly IStaffGroupTravelRepository _groupTravelRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StaffTravelRequestService> _logger;

    public StaffTravelRequestService(
        IStaffTravelRequestRepository requestRepository,
        IStaffTravelRequestCommentRepository commentRepository,
        IStaffTravelRequestAttachmentRepository attachmentRepository,
        IStaffGroupTravelRepository groupTravelRepository,
        IUnitOfWork unitOfWork,
        ILogger<StaffTravelRequestService> logger)
    {
        _requestRepository = requestRepository;
        _commentRepository = commentRepository;
        _attachmentRepository = attachmentRepository;
        _groupTravelRepository = groupTravelRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ---- Queries -----------------------------------------------------------

    public async Task<StaffTravelRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetWithFullDetailsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<StaffTravelRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByRequestNumberAsync(requestNumber);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _requestRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<StaffTravelRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _requestRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffTravelRequestSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
        => (await _requestRepository.GetByEmployeeIdAsync(employeeId)).ToSummaryDtoList();

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByStatusAsync(StaffTravelRequestStatus status, CancellationToken cancellationToken = default)
        => (await _requestRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByDateRangeAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
        => (await _requestRepository.GetByDateRangeAsync(start, end)).ToSummaryDtoList();

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
        => (await _requestRepository.GetByOrganizationUnitAsync(organizationUnitId)).ToSummaryDtoList();

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default)
        => (await _requestRepository.GetPendingApprovalAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetUpcomingTripsAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
        => (await _requestRepository.GetUpcomingTripsAsync(daysAhead)).ToSummaryDtoList();

    public async Task<IEnumerable<StaffTravelRequestSummaryDto>> GetChildRequestsAsync(Guid parentRequestId, CancellationToken cancellationToken = default)
        => (await _requestRepository.GetChildRequestsAsync(parentRequestId)).ToSummaryDtoList();

    // ---- Dashboard ---------------------------------------------------------

    public async Task<StaffTravelDashboardDto> GetDashboardAsync(int upcomingDays = 30, CancellationToken cancellationToken = default)
    {
        // Lightweight scalar projection of every request for in-memory aggregation.
        var rows = await _requestRepository.GetQueryable()
            .Select(r => new DashboardRow
            {
                Status = r.Status,
                TravelType = r.TravelType,
                RiskLevel = r.RiskLevel,
                IsInternational = r.IsInternational,
                EstimatedTotalCost = r.EstimatedTotalCost,
                ApprovedBudget = r.ApprovedBudget,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        bool IsActive(StaffTravelRequestStatus s) =>
            s != StaffTravelRequestStatus.Cancelled && s != StaffTravelRequestStatus.Rejected;

        var dto = new StaffTravelDashboardDto
        {
            TotalRequests        = rows.Count,
            DraftCount           = rows.Count(r => r.Status == StaffTravelRequestStatus.Draft),
            PendingApprovalCount = rows.Count(r => r.Status == StaffTravelRequestStatus.Submitted),
            ApprovedCount        = rows.Count(r => r.Status == StaffTravelRequestStatus.Approved),
            InProgressCount      = rows.Count(r => r.Status == StaffTravelRequestStatus.InProgress),
            CompletedCount       = rows.Count(r => r.Status == StaffTravelRequestStatus.Completed),
            RejectedCount        = rows.Count(r => r.Status == StaffTravelRequestStatus.Rejected),
            CancelledCount       = rows.Count(r => r.Status == StaffTravelRequestStatus.Cancelled),
            InternationalCount   = rows.Count(r => r.IsInternational),
            DomesticCount        = rows.Count(r => !r.IsInternational),
            HighRiskCount        = rows.Count(r => r.RiskLevel == TravelRiskLevel.High
                                               || r.RiskLevel == TravelRiskLevel.Critical
                                               || r.RiskLevel == TravelRiskLevel.Prohibited),
            TotalEstimatedCost   = rows.Where(r => IsActive(r.Status)).Sum(r => r.EstimatedTotalCost),
            TotalApprovedBudget  = rows.Where(r => IsActive(r.Status)).Sum(r => r.ApprovedBudget ?? 0m),
        };

        dto.ByStatus = rows
            .GroupBy(r => r.Status)
            .Select(g => new StaffTravelStatusCountDto { Status = g.Key, Count = g.Count() })
            .OrderByDescending(s => s.Count)
            .ToList();

        dto.ByTravelType = rows
            .GroupBy(r => r.TravelType)
            .Select(g => new StaffTravelTypeCountDto { TravelType = g.Key, Count = g.Count() })
            .OrderByDescending(t => t.Count)
            .ToList();

        // Monthly trend over the trailing six calendar months (by creation date).
        var anchor = DateTime.UtcNow;
        for (var i = 5; i >= 0; i--)
        {
            var month = anchor.AddMonths(-i);
            dto.MonthlyTrend.Add(new StaffTravelMonthlyCountDto
            {
                Year = month.Year,
                Month = month.Month,
                Label = new DateTime(month.Year, month.Month, 1).ToString("MMM yyyy"),
                Count = rows.Count(r => r.CreatedAt.Year == month.Year && r.CreatedAt.Month == month.Month),
            });
        }

        // Spotlight lists (lightweight navigation already loaded by these queries).
        var upcoming = (await _requestRepository.GetUpcomingTripsAsync(upcomingDays)).ToList();
        dto.UpcomingTripCount = upcoming.Count;
        dto.UpcomingTrips = upcoming.Take(5).ToSummaryDtoList().ToList();

        dto.PendingApprovals = (await _requestRepository.GetPendingApprovalAsync())
            .Take(5).ToSummaryDtoList().ToList();

        var recent = await _requestRepository.GetQueryable()
            .Include(r => r.Employee)
            .Include(r => r.DestinationCountry)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);
        dto.RecentRequests = recent.ToSummaryDtoList().ToList();

        return dto;
    }

    private sealed class DashboardRow
    {
        public StaffTravelRequestStatus Status { get; set; }
        public StaffTravelType TravelType { get; set; }
        public TravelRiskLevel RiskLevel { get; set; }
        public bool IsInternational { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public decimal? ApprovedBudget { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ---- CRUD --------------------------------------------------------------

    public async Task<StaffTravelRequestDto> CreateAsync(CreateStaffTravelRequestDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        entity.RequestNumber = await GenerateRequestNumberAsync(cancellationToken);
        entity.Status = StaffTravelRequestStatus.Draft;

        await _requestRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request created: {RequestNumber}", entity.RequestNumber);

        return (await _requestRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<StaffTravelRequestDto> UpdateAsync(UpdateStaffTravelRequestDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{updateDto.Id}' not found.");

        if (entity.Status is StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException($"A request in status '{entity.Status}' cannot be edited.");

        entity.UpdateEntity(updateDto, updatedByUserId);

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request updated: {RequestNumber}", entity.RequestNumber);

        return (await _requestRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{id}' not found.");

        if (entity.Status != StaffTravelRequestStatus.Draft)
            throw new InvalidOperationException("Only draft requests can be deleted. Cancel the request instead.");

        await _requestRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request deleted: {RequestNumber}", entity.RequestNumber);

        return true;
    }

    // ---- Workflow ----------------------------------------------------------

    public async Task<bool> SubmitAsync(SubmitStaffTravelRequestDto submitDto, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(submitDto.RequestId);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{submitDto.RequestId}' not found.");

        if (entity.Status is not (StaffTravelRequestStatus.Draft or StaffTravelRequestStatus.ReturnedForRevision))
            throw new InvalidOperationException("Only draft or returned requests can be submitted.");

        entity.Status = StaffTravelRequestStatus.Submitted;
        entity.SubmittedAt = submitDto.SubmittedAt;
        entity.UpdatedBy = submitDto.SubmittedById.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request submitted: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    public async Task<bool> ApproveAsync(ApproveStaffTravelRequestDto approveDto, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(approveDto.RequestId);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{approveDto.RequestId}' not found.");

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted requests can be approved.");

        entity.Status = StaffTravelRequestStatus.Approved;
        entity.ApprovedAt = approveDto.ApprovedAt;
        entity.ApprovedBudget = approveDto.ApprovedBudget ?? entity.EstimatedTotalCost;
        entity.UpdatedBy = approveDto.ApprovedById.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request approved: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    public async Task<bool> RejectAsync(Guid requestId, Guid rejectedByUserId, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(requestId);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");

        if (entity.Status != StaffTravelRequestStatus.Submitted)
            throw new InvalidOperationException("Only submitted requests can be rejected.");

        entity.Status = StaffTravelRequestStatus.Rejected;
        entity.CancellationReason = reason;
        entity.UpdatedBy = rejectedByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request rejected: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    public async Task<bool> CancelAsync(CancelStaffTravelRequestDto cancelDto, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(cancelDto.RequestId);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{cancelDto.RequestId}' not found.");

        if (entity.Status is StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException($"A request in status '{entity.Status}' cannot be cancelled.");

        entity.Status = StaffTravelRequestStatus.Cancelled;
        entity.CancellationReason = cancelDto.CancellationReason;
        entity.CancelledById = cancelDto.CancelledById;
        entity.CancelledAt = cancelDto.CancelledAt;
        entity.UpdatedBy = cancelDto.CancelledById.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request cancelled: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    public async Task<bool> MarkCompletedAsync(Guid requestId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(requestId);
        if (entity == null)
            throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");

        if (entity.Status is not (StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress))
            throw new InvalidOperationException("Only approved or in-progress requests can be marked completed.");

        entity.Status = StaffTravelRequestStatus.Completed;
        entity.CompletedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Staff travel request completed: {RequestNumber}", entity.RequestNumber);
        return true;
    }

    // ---- Comments ----------------------------------------------------------

    public async Task<StaffTravelRequestCommentDto> AddCommentAsync(CreateStaffTravelRequestCommentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _commentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRequestCommentDto>> GetCommentsAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _commentRepository.GetByRequestIdAsync(requestId)).Select(c => c.ToDto()).ToList();

    public async Task<StaffTravelRequestCommentDto> UpdateCommentAsync(UpdateStaffTravelRequestCommentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _commentRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Comment with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _commentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        var entity = await _commentRepository.GetByIdAsync(commentId);
        if (entity == null)
            throw new ArgumentException($"Comment with ID '{commentId}' not found.");

        await _commentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Attachments -------------------------------------------------------

    public async Task<StaffTravelRequestAttachmentDto> AddAttachmentAsync(CreateStaffTravelRequestAttachmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRequestAttachmentDto>> GetAttachmentsAsync(Guid requestId, CancellationToken cancellationToken = default)
        => (await _attachmentRepository.GetByRequestIdAsync(requestId)).Select(a => a.ToDto()).ToList();

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _attachmentRepository.GetByIdAsync(attachmentId);
        if (entity == null)
            throw new ArgumentException($"Attachment with ID '{attachmentId}' not found.");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Group travel ------------------------------------------------------

    public async Task<StaffGroupTravelDto> CreateGroupTravelAsync(CreateStaffGroupTravelDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _groupTravelRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Group travel created: {GroupName}", entity.GroupName);
        return entity.ToDto();
    }

    public async Task<StaffGroupTravelDto> GetGroupTravelByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _groupTravelRepository.GetWithRequestsAsync(id);
        if (entity == null)
            throw new ArgumentException($"Group travel with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffGroupTravelSummaryDto>> GetAllGroupTravelsAsync(CancellationToken cancellationToken = default)
        => (await _groupTravelRepository.GetAllAsync()).ToSummaryDtoList();

    public async Task<IEnumerable<StaffGroupTravelSummaryDto>> GetGroupTravelsByStatusAsync(GroupTravelStatus status, CancellationToken cancellationToken = default)
        => (await _groupTravelRepository.GetByStatusAsync(status)).ToSummaryDtoList();

    public async Task<StaffGroupTravelDto> UpdateGroupTravelAsync(UpdateStaffGroupTravelDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _groupTravelRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Group travel with ID '{updateDto.Id}' not found.");

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _groupTravelRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteGroupTravelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _groupTravelRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Group travel with ID '{id}' not found.");

        await _groupTravelRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<StaffGroupTravelDto> AddGroupParticipantsAsync(AddGroupTravelParticipantsDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var group = await _groupTravelRepository.GetWithRequestsAsync(dto.GroupTravelId)
            ?? throw new ArgumentException($"Group travel with ID '{dto.GroupTravelId}' not found.");

        // Skip employees already participating in the group.
        var existing = group.Requests.Select(r => r.EmployeeId).ToHashSet();
        var toAdd = dto.EmployeeIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Where(id => !existing.Contains(id))
            .ToList();

        foreach (var employeeId in toAdd)
        {
            var createDto = new CreateStaffTravelRequestDto
            {
                EmployeeId              = employeeId,
                InitiatedById           = createdByUserId,
                InitiatedByRole         = dto.InitiatedByRole,
                TravelType              = dto.TravelType,
                TravelPurpose           = dto.TravelPurpose,
                PurposeDescription      = dto.PurposeDescription,
                OrganizationUnitId      = dto.OrganizationUnitId,
                Priority                = dto.Priority,
                DestinationCountryId    = group.DestinationCountryId,
                DestinationCity         = group.DestinationCity,
                OriginCountryId         = dto.OriginCountryId,
                OriginCity              = dto.OriginCity,
                TravelStartDate         = group.TravelStartDate,
                TravelEndDate           = group.TravelEndDate,
                EstimatedTotalCost      = dto.EstimatedTotalCost,
                CurrencyCode            = dto.CurrencyCode,
                IsInternational         = dto.IsInternational,
                RequiresVisa            = dto.RequiresVisa,
                RequiresHealthClearance = dto.RequiresHealthClearance,
                RiskLevel               = dto.RiskLevel,
                GroupTravelId           = group.Id,
            };

            var entity = createDto.ToEntity(tenantId, createdByUserId);
            // Request numbers are derived from the persisted count, so save each in turn.
            entity.RequestNumber = await GenerateRequestNumberAsync(cancellationToken);
            entity.Status = StaffTravelRequestStatus.Draft;
            await _requestRepository.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Added {Count} participant(s) to group travel {GroupId}", toAdd.Count, group.Id);
        return (await _groupTravelRepository.GetWithRequestsAsync(group.Id))!.ToDto();
    }

    public async Task<bool> RemoveGroupParticipantAsync(Guid groupTravelId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await _requestRepository.GetByIdAsync(requestId);
        if (entity == null || entity.GroupTravelId != groupTravelId)
            return false;

        entity.GroupTravelId = null;
        await _requestRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Helpers -----------------------------------------------------------

    private async Task<string> GenerateRequestNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _requestRepository.CountByYearAsync(year);
        return $"TR-{year}-{(count + 1):D5}";
    }
}

#endregion
