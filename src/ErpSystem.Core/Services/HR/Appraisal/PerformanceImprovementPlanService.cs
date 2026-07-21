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

#region Performance Improvement Plan

public class PerformanceImprovementPlanService : IPerformanceImprovementPlanService
{
    private readonly IGenericRepository<PerformanceImprovementPlan> _improvementPlanRepository;
    private readonly IGenericRepository<PipReviewMeeting> _reviewMeetingRepository;
    private readonly IGenericRepository<PipGoal> _pipGoalRepository;
    private readonly IGenericRepository<AppraisalAttachment> _attachmentRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PerformanceImprovementPlan> _logger;

    public PerformanceImprovementPlanService(
        IGenericRepository<PerformanceImprovementPlan> improvementPlanRepository,
        IGenericRepository<PipReviewMeeting> reviewMeetingRepository,
        IGenericRepository<PipGoal> pipGoalRepository,
        IGenericRepository<AppraisalAttachment> attachmentRepository,
        IGenericRepository<Employee> employeeRepository,
        IUnitOfWork unitOfWork,
        ILogger<PerformanceImprovementPlan> logger)
    {
        _improvementPlanRepository = improvementPlanRepository;
        _reviewMeetingRepository = reviewMeetingRepository;
        _pipGoalRepository = pipGoalRepository;
        _attachmentRepository = attachmentRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PipReviewMeetingDto> AddReviewMeetingAsync(Guid pipId, CreatePipReviewMeetingDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate PIP exists
        var pipExists = await _improvementPlanRepository.ExistsAsync(p => p.Id == pipId);
        if (!pipExists)
            throw new ArgumentException("Performance Improvement Plan not found");

        // Validate conductor exists
        var conductorExists = await _employeeRepository.ExistsAsync(e => e.Id == createDto.ConductedById);
        if (!conductorExists)
            throw new ArgumentException("Meeting conductor not found");

        var entity = createDto.ToEntity();
        entity.PipId = pipId;

        await _reviewMeetingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _reviewMeetingRepository.GetQueryable()
                                            .Include(m => m.ConductedBy)
                                            .FirstOrDefaultAsync(m => m.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Review meeting added successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<bool> CompletePipAsync(CompletePipDto completeDto, CancellationToken cancellationToken = default)
    {
        var entity = await _improvementPlanRepository.GetByIdAsync(completeDto.PipId);

        if (entity == null)
        {
            throw new ArgumentException($"Performance Improvement Plan with ID '{completeDto.PipId}' not found.");
        }

        entity.Status = completeDto.Outcome == PipOutcome.PerformanceImproved ? PipStatus.Completed : PipStatus.Unsuccessful;
        entity.CompletionDate = DateTime.UtcNow;
        entity.Outcome = completeDto.Outcome;
        entity.OutcomeNotes = completeDto.OutcomeNotes;

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan completed: {Id}", completeDto.PipId);

        return true;
    }

    public async Task<PerformanceImprovementPlanDto> CreateAsync(CreatePerformanceImprovementPlanDto createDto, CancellationToken cancellationToken = default)
    {
        // ── Data integrity: only one active PIP per employee at a time ──────────
        var hasActivePip = await _improvementPlanRepository.ExistsAsync(
            p => p.EmployeeId == createDto.EmployeeId
              && (p.Status == PipStatus.Active || p.Status == PipStatus.InProgress));

        if (hasActivePip)
            throw new InvalidOperationException(
                "This employee already has an active Performance Improvement Plan. " +
                "Complete or cancel the existing PIP before creating a new one.");

        var performanceImprovementPlan = createDto.ToEntity();

        await _improvementPlanRepository.AddAsync(performanceImprovementPlan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Performance Improvement Plan created: {pipId}", performanceImprovementPlan.Id);

        return performanceImprovementPlan.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _improvementPlanRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"Performance Improvement Plan with ID '{id}' not found.");
        }

        await _improvementPlanRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan deleted: {id}", id);

        return true;
    }

    public async Task<bool> DeleteReviewMeetingAsync(Guid pipId, Guid meetingId, CancellationToken cancellationToken = default)
    {
        var entity = await _reviewMeetingRepository.FirstOrDefaultAsync(m => m.Id == meetingId && m.PipId == pipId);

        if (entity == null)
            throw new ArgumentException("Review meeting not found");

        await _reviewMeetingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review meeting deleted successfully");

        return true;
    }

    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetActivePipsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _improvementPlanRepository.GetQueryable(p => p.Status == PipStatus.Active || p.Status == PipStatus.InProgress)
                                                    .Include(p => p.Employee)
                                                    .Include(p => p.Supervisor)
                                                    .Include(p => p.Appraisal)
                                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _improvementPlanRepository.GetQueryable()
                                                    .Include(p => p.Employee)
                                                    .Include(p => p.Supervisor)
                                                    .Include(p => p.Appraisal)
                                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _improvementPlanRepository.GetQueryable(p => p.EmployeeId == employeeId)
                                    .Include(p => p.Employee)
                                    .Include(p => p.Supervisor)
                                    .Include(p => p.Appraisal)
                                    .OrderByDescending(p => p.StartDate)
                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PerformanceImprovementPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _improvementPlanRepository.GetQueryable()
                                                    .Include(p => p.Employee)
                                                    .Include(p => p.Supervisor)
                                                    .Include(p => p.Appraisal)
                                                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Performance Improvement Plan with ID '{id}' not found.");
        }

        return entity.ToDto();
    }

    public async Task<IEnumerable<PerformanceImprovementPlanDto>> GetByStatusAsync(PipStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _improvementPlanRepository.GetQueryable(p => p.Status == status)
                                                    .Include(p => p.Employee)
                                                    .Include(p => p.Supervisor)
                                                    .Include(p => p.Appraisal)
                                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PipReviewMeetingDto> GetLatestReviewMeetingAsync(Guid pipId, CancellationToken cancellationToken = default)
    {
        var entity = await _reviewMeetingRepository.GetQueryable(m => m.PipId == pipId)
                                                .Include(m => m.ConductedBy)
                                                .OrderByDescending(m => m.MeetingDate)
                                                .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
            throw new ArgumentException("No review meetings found for this Performance Improvement Plan");

        return entity.ToDto();
    }

    public async Task<PagedResult<PerformanceImprovementPlanDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _improvementPlanRepository.GetQueryable()
                                            .Include(p => p.Employee)
                                            .Include(p => p.Supervisor)
                                            .Include(p => p.Appraisal);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query.OrderByDescending(p => p.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);

        var improvementPlans = items.ToDtoList();

        return new PagedResult<PerformanceImprovementPlanDto>
        {
            Items = improvementPlans,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<PipReviewMeetingDto>> GetReviewMeetingsAsync(Guid pipId, CancellationToken cancellationToken = default)
    {
        var entities = await _reviewMeetingRepository.GetQueryable(m => m.PipId == pipId)
                                                    .Include(m => m.ConductedBy)
                                                    .OrderByDescending(m => m.MeetingDate)
                                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PipReviewMeetingDto?> GetReviewMeetingByIdAsync(Guid meetingId, CancellationToken cancellationToken = default)
    {
        var entity = await _reviewMeetingRepository.GetQueryable(m => m.Id == meetingId)
                                                   .Include(m => m.ConductedBy)
                                                   .FirstOrDefaultAsync(cancellationToken);
        return entity?.ToDto();
    }

    public async Task<PerformanceImprovementPlanDto> UpdateAsync(UpdatePerformanceImprovementPlanDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _improvementPlanRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
        {
            throw new ArgumentException($"Performance Improvement Plan with ID '{updateDto.Id}' not found.");
        }

        updateDto.UpdateEntity(entity);

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<PipReviewMeetingDto> UpdateReviewMeetingAsync(Guid pipId, UpdatePipReviewMeetingDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _reviewMeetingRepository.GetQueryable()
            .Include(m => m.ConductedBy)
            .FirstOrDefaultAsync(m => m.Id == updateDto.Id && m.PipId == pipId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Review meeting not found");

        updateDto.UpdateEntity(entity);
        await _reviewMeetingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Review meeting updated successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> UpdateStatusAsync(UpdatePipStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await _improvementPlanRepository.GetByIdAsync(statusDto.PipId);

        if (entity == null)
        {
            throw new ArgumentException($"Performance Improvement Plan with ID '{statusDto.PipId}' not found.");
        }

        entity.Status = statusDto.Status;

        await _improvementPlanRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance Improvement Plan status updated: {Id}", statusDto.PipId);

        return true;
    }

    // ─── PIP Goals ──────────────────────────────────────────────────────────────

    public async Task<PipGoalDto> AddPipGoalAsync(
        Guid pipId, CreatePipGoalDto dto, CancellationToken cancellationToken = default)
    {
        var pipExists = await _improvementPlanRepository.ExistsAsync(p => p.Id == pipId);
        if (!pipExists)
            throw new ArgumentException($"PIP with ID '{pipId}' not found.");

        var entity = dto.ToEntity();
        entity.PipId = pipId;
        entity.Status = GoalProgressStatus.NotStarted;

        await _pipGoalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP goal added to PIP {PipId}: {GoalId}", pipId, entity.Id);
        return (await _pipGoalRepository.GetByIdAsync(entity.Id))!.ToDto();
    }

    public async Task<IEnumerable<PipGoalDto>> GetPipGoalsAsync(
        Guid pipId, CancellationToken cancellationToken = default)
    {
        var entities = await _pipGoalRepository
            .GetQueryable(g => g.PipId == pipId)
            .OrderBy(g => g.DueDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PipGoalDto> UpdatePipGoalAsync(
        Guid pipId, UpdatePipGoalDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _pipGoalRepository.GetQueryable()
            .FirstOrDefaultAsync(g => g.Id == dto.Id && g.PipId == pipId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("PIP goal not found.");

        dto.UpdateEntity(entity);
        await _pipGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP goal updated: {GoalId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeletePipGoalAsync(
        Guid pipId, Guid goalId, CancellationToken cancellationToken = default)
    {
        var entity = await _pipGoalRepository.GetQueryable()
            .FirstOrDefaultAsync(g => g.Id == goalId && g.PipId == pipId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("PIP goal not found.");

        await _pipGoalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PipGoalDto> UpdatePipGoalProgressAsync(
        Guid pipId, Guid goalId, decimal progressPercent, string? notes,
        GoalProgressStatus status, CancellationToken cancellationToken = default)
    {
        var entity = await _pipGoalRepository.GetQueryable()
            .FirstOrDefaultAsync(g => g.Id == goalId && g.PipId == pipId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("PIP goal not found.");

        var clamped = Math.Clamp((int)progressPercent, 0, 100);
        entity.ProgressPercent = clamped;

        if (!string.IsNullOrWhiteSpace(notes))
            entity.ProgressNotes = notes;

        entity.Status = status;

        await _pipGoalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP goal {GoalId} progress updated to {Progress}%", goalId, clamped);
        return entity.ToDto();
    }

    public async Task<PipGoalDto?> GetGoalByIdAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var entity = await _pipGoalRepository.GetByIdAsync(goalId);
        return entity?.ToDto();
    }

    // ─── PIP Attachments ────────────────────────────────────────────────────────

    public async Task<IEnumerable<PipAttachmentDto>> GetPipAttachmentsAsync(
        Guid pipId, CancellationToken cancellationToken = default)
    {
        var entities = await _attachmentRepository
            .GetQueryable(a => a.PipId == pipId && a.EntityType == AppraisalAttachmentEntityType.PipPlan)
            .Include(a => a.UploadedBy)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);
        return entities.ToPipAttachmentDtoList();
    }

    public async Task<PipAttachmentDto?> GetAttachmentByIdAsync(
        Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _attachmentRepository
            .GetQueryable(a => a.Id == attachmentId)
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(cancellationToken);
        return entity?.ToPipAttachmentDto();
    }

    public async Task<PipAttachmentDto> CreatePipAttachmentAsync(
        Guid pipId, Guid uploadedById, string fileName, string filePath,
        string? publicUrl, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default)
    {
        var pipExists = await _improvementPlanRepository.ExistsAsync(p => p.Id == pipId);
        if (!pipExists)
            throw new ArgumentException($"PIP with ID '{pipId}' not found.");

        var entity = new AppraisalAttachment
        {
            PipId = pipId,
            EntityType = AppraisalAttachmentEntityType.PipPlan,
            FileName = fileName,
            FilePath = filePath,
            FileSizeBytes = fileSizeBytes,
            Description = description,
            UploadDate = DateTime.UtcNow,
            UploadedById = uploadedById,
        };

        await _attachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _attachmentRepository
            .GetQueryable(a => a.Id == entity.Id)
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(cancellationToken);

        _logger.LogInformation("PIP attachment created: {AttachmentId} for PIP {PipId}", entity!.Id, pipId);

        var dto = entity!.ToPipAttachmentDto();
        dto.PublicUrl = publicUrl;
        return dto;
    }

    public async Task<bool> DeletePipAttachmentAsync(
        Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await _attachmentRepository
            .GetQueryable(a => a.Id == attachmentId && a.EntityType == AppraisalAttachmentEntityType.PipPlan)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
            throw new ArgumentException($"PIP attachment with ID '{attachmentId}' not found.");

        await _attachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PIP attachment deleted: {AttachmentId}", attachmentId);
        return true;
    }
}

#endregion Performance Improvement Plan

