using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class TrainingNeedsAssessmentService : ITrainingNeedsAssessmentService
{
    private readonly ITrainingNeedsAssessmentRepository _assessmentRepository;
    private readonly ITrainingNeedsAssessmentProgramRepository _programRepository;
    private readonly ITrainingNeedsAssessmentSkillRepository _skillRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingNeedsAssessmentService> _logger;

    public TrainingNeedsAssessmentService(
        ITrainingNeedsAssessmentRepository assessmentRepository,
        ITrainingNeedsAssessmentProgramRepository programRepository,
        ITrainingNeedsAssessmentSkillRepository skillRepository,
        IUnitOfWork unitOfWork,
        ILogger<TrainingNeedsAssessmentService> logger)
    {
        _assessmentRepository = assessmentRepository;
        _programRepository = programRepository;
        _skillRepository = skillRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Assessment queries ────────────────────────────────────────────────────

    public async Task<TrainingNeedsAssessmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _assessmentRepository.GetWithFullDetailsAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training needs assessment with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _assessmentRepository.GetAllAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var entities = await _assessmentRepository.GetByYearAsync(year);
        return entities.ToSummaryDtoList();
    }

    public async Task<PagedResult<TrainingNeedsAssessmentSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _assessmentRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.IdentifiedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TrainingNeedsAssessmentSummaryDto>
        {
            Items = items.ToSummaryDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByDepartmentIdAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var entities = await _assessmentRepository.GetQueryable()
            .Where(a => a.Employee.OrganizationUnitId == departmentId)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetUnfulfilledAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _assessmentRepository.GetUnfulfilledAsync();
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByPriorityAsync(TrainingPriority priority, CancellationToken cancellationToken = default)
    {
        var entities = await _assessmentRepository.GetByPriorityAsync(priority);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _assessmentRepository.GetByEmployeeIdAsync(employeeId);
        return entities.ToSummaryDtoList();
    }

    // ── Assessment CRUD ───────────────────────────────────────────────────────

    public async Task<TrainingNeedsAssessmentDto> CreateAsync(CreateTrainingNeedsAssessmentDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _assessmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training needs assessment created for employee {EmployeeId}", dto.EmployeeId);

        return entity.ToDto();
    }

    public async Task<BulkNeedsAssessmentResultDto> BulkCreateAsync(BulkCreateTrainingNeedsAssessmentDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var result = new BulkNeedsAssessmentResultDto { RequestedCount = dto.EmployeeIds.Count };

        foreach (var employeeId in dto.EmployeeIds.Distinct())
        {
            var entity = new CreateTrainingNeedsAssessmentDto
            {
                EmployeeId = employeeId,
                Year = dto.Year,
                Source = dto.Source,
                IdentifiedGaps = dto.IdentifiedGaps,
                Priority = dto.Priority,
                IdentifiedById = createdByUserId,
                AdditionalNotes = dto.AdditionalNotes
            }.ToEntity(tenantId, createdByUserId);

            await _assessmentRepository.AddAsync(entity);
            result.CreatedCount++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bulk needs assessment: {Count} created", result.CreatedCount);
        return result;
    }

    public async Task<TrainingNeedsAssessmentDto> UpdateAsync(UpdateTrainingNeedsAssessmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _assessmentRepository.GetByIdAsync(dto.Id);

        if (entity == null)
            throw new ArgumentException($"Training needs assessment with ID '{dto.Id}' not found.");

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _assessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _assessmentRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Training needs assessment with ID '{id}' not found.");

        await _assessmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training needs assessment {AssessmentId} deleted", id);

        return true;
    }

    // ── Recommended program sub-operations ───────────────────────────────────

    public async Task<TrainingNeedsAssessmentProgramDto> AddRecommendedProgramAsync(CreateTrainingNeedsAssessmentProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var assessment = await _assessmentRepository.GetByIdAsync(dto.AssessmentId);

        if (assessment == null)
            throw new ArgumentException($"Training needs assessment with ID '{dto.AssessmentId}' not found.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentProgramDto>> GetRecommendedProgramsAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var entities = await _programRepository.GetByAssessmentIdAsync(assessmentId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteRecommendedProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _programRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Recommended program with ID '{id}' not found.");

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Skill gap sub-operations ──────────────────────────────────────────────

    public async Task<TrainingNeedsAssessmentSkillDto> AddSkillGapAsync(CreateTrainingNeedsAssessmentSkillDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var assessment = await _assessmentRepository.GetByIdAsync(dto.AssessmentId);

        if (assessment == null)
            throw new ArgumentException($"Training needs assessment with ID '{dto.AssessmentId}' not found.");

        var entity = dto.ToEntity(tenantId, createdByUserId);

        await _skillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSkillDto>> GetSkillGapsAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var entities = await _skillRepository.GetByAssessmentIdAsync(assessmentId);
        return entities.Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteSkillGapAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _skillRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"Skill gap with ID '{id}' not found.");

        await _skillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
