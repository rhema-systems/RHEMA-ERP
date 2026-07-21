using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class CheckInService : ICheckInService
{
    private readonly IGenericRepository<CheckIn> _checkInRepository;
    private readonly IGenericRepository<CheckInGoalUpdate> _goalUpdateRepository;
    private readonly IGenericRepository<AppraisalAttachment> _attachmentRepository;
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CheckInService> _logger;

    public CheckInService(
        IGenericRepository<CheckIn> checkInRepository,
        IGenericRepository<CheckInGoalUpdate> goalUpdateRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IGenericRepository<EmployeeGoal> goalRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IUnitOfWork unitOfWork,
        ILogger<CheckInService> logger)
    {
        _checkInRepository = checkInRepository;
        _goalUpdateRepository = goalUpdateRepository;
        _attachmentRepository = attachmentRepository;
        _goalRepository = goalRepository;
        _cycleRepository = cycleRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private IQueryable<CheckIn> BaseQuery => _checkInRepository.GetQueryable()
        .Include(c => c.Employee)
        .Include(c => c.ConductedBy)
        .Include(c => c.Cycle);

    public async Task<CheckInDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Check-in with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<CheckInDto>> GetByEmployeeIdAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.Where(c => c.EmployeeId == employeeId);
        if (cycleId.HasValue)
            query = query.Where(c => c.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderByDescending(c => c.ScheduledDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<CheckInDto>> GetByConductedByIdAsync(Guid conductedById, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.Where(c => c.ConductedById == conductedById);
        if (cycleId.HasValue)
            query = query.Where(c => c.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderByDescending(c => c.ScheduledDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<CheckInDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.OrderByDescending(c => c.ScheduledDate);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CheckInDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<CheckInDto>> GetUpcomingAsync(Guid employeeId, int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        var entities = await BaseQuery
            .Where(c => c.EmployeeId == employeeId &&
                        c.ScheduledDate >= DateTime.UtcNow &&
                        c.ScheduledDate <= cutoff &&
                        c.ConductedDate == null)
            .OrderBy(c => c.ScheduledDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<CheckInDto> CreateAsync(CreateCheckInDto createDto, CancellationToken cancellationToken = default)
    {
        // Gate: check-ins must be enabled for this cycle.
        var settings = await _cycleRepository.GetQueryable(c => c.Id == createDto.AppraisalCycleId)
            .Include(c => c.AppraisalSettings)
            .Select(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(cancellationToken);
        if (settings is { EnableCheckIns: false })
            throw new InvalidOperationException("Check-ins are not enabled for this appraisal cycle.");

        var entity = createDto.ToEntity();
        await _checkInRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Check-in created: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<CheckInDto> UpdateAsync(UpdateCheckInDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _checkInRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Check-in with ID '{updateDto.Id}' not found.");
        updateDto.UpdateEntity(entity);
        await _checkInRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Check-in updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _checkInRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Check-in with ID '{id}' not found.");
        await _checkInRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Check-in deleted: {Id}", id);
        return true;
    }

    public async Task<CheckInDto> CompleteAsync(
        Guid checkInId, string? sharedNotes, string? privateNotes, string? actionItems,
        CancellationToken cancellationToken = default)
    {
        var entity = await _checkInRepository.GetByIdAsync(checkInId);
        if (entity == null)
            throw new ArgumentException($"Check-in with ID '{checkInId}' not found.");

        entity.ConductedDate = DateTime.UtcNow;
        entity.SharedNotes = sharedNotes;
        entity.PrivateNotes = privateNotes;
        entity.ActionItems = actionItems;

        await _checkInRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Check-in completed: {Id}", checkInId);
        return await GetByIdAsync(checkInId, cancellationToken);
    }

    // ─── Goal Updates ────────────────────────────────────────────────────────

    public async Task<CheckInGoalUpdateDto> AddGoalUpdateAsync(Guid checkInId, CreateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var checkInExists = await _checkInRepository.ExistsAsync(c => c.Id == checkInId);
        if (!checkInExists)
            throw new ArgumentException("Check-in not found.");

        var goal = await _goalRepository.GetByIdAsync(dto.EmployeeGoalId);
        if (goal == null)
            throw new ArgumentException("Employee goal not found.");

        var entity = dto.ToEntity();
        entity.CheckInId = checkInId;

        await _goalUpdateRepository.AddAsync(entity);

        // Sync progress back to the goal
        if (entity.UpdatedProgress.HasValue)
        {
            goal.ProgressPercent = entity.UpdatedProgress.Value;
            await _goalRepository.UpdateAsync(goal);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _goalUpdateRepository.GetQueryable()
            .Include(u => u.EmployeeGoal)
            .FirstOrDefaultAsync(u => u.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Goal update added to check-in {CheckInId}: {UpdateId}", checkInId, entity!.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<CheckInGoalUpdateDto>> GetGoalUpdatesAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        var entities = await _goalUpdateRepository.GetQueryable(u => u.CheckInId == checkInId)
            .Include(u => u.EmployeeGoal)
            .OrderBy(u => u.EmployeeGoal.Title)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<CheckInGoalUpdateDto> UpdateGoalUpdateAsync(Guid checkInId, UpdateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _goalUpdateRepository.GetQueryable()
            .Include(u => u.EmployeeGoal)
            .FirstOrDefaultAsync(u => u.Id == dto.Id && u.CheckInId == checkInId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Goal update not found.");

        dto.UpdateEntity(entity);
        await _goalUpdateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Check-in goal update updated: {UpdateId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteGoalUpdateAsync(Guid checkInId, Guid updateId, CancellationToken cancellationToken = default)
    {
        var entity = await _goalUpdateRepository.GetQueryable()
            .FirstOrDefaultAsync(u => u.Id == updateId && u.CheckInId == checkInId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Goal update not found.");

        await _goalUpdateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ─── Attachments ─────────────────────────────────────────────────────────

    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(Guid checkInId, CreateAppraisalAttachmentDto dto, CancellationToken cancellationToken = default)
    {
        var exists = await _checkInRepository.ExistsAsync(c => c.Id == checkInId);
        if (!exists)
            throw new ArgumentException("Check-in not found.");

        var entity = dto.ToEntity();
        entity.CheckInId = checkInId;
        entity.EntityType = AppraisalAttachmentEntityType.CheckIn;
        entity.UploadDate = DateTime.UtcNow;

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment added to check-in {CheckInId}: {AttachmentId}", checkInId, entity.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        var entities = await _attachmentRepository.GetQueryable(a => a.CheckInId == checkInId)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<bool> DeleteAttachmentAsync(Guid checkInId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _attachmentRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.CheckInId == checkInId, cancellationToken);
        if (entity == null)
            throw new ArgumentException("Attachment not found.");
        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
