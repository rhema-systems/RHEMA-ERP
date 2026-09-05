using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Training;
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
    private readonly IGenericRepository<TrainingProgram> _trainingProgramRepository;
    private readonly IGenericRepository<Skill> _skillCatalogRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrainingNeedsAssessmentService> _logger;

    public TrainingNeedsAssessmentService(
        ITrainingNeedsAssessmentRepository assessmentRepository,
        ITrainingNeedsAssessmentProgramRepository programRepository,
        ITrainingNeedsAssessmentSkillRepository skillRepository,
        IGenericRepository<TrainingProgram> trainingProgramRepository,
        IGenericRepository<Skill> skillCatalogRepository,
        IGenericRepository<Employee> employeeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TrainingNeedsAssessmentService> logger)
    {
        _assessmentRepository = assessmentRepository;
        _programRepository = programRepository;
        _skillRepository = skillRepository;
        _trainingProgramRepository = trainingProgramRepository;
        _skillCatalogRepository = skillCatalogRepository;
        _employeeRepository = employeeRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes reads/writes to
    // the current tenant explicitly.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // An assessment owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<TrainingNeedsAssessment> GetOwnedAssessmentAsync(Guid id)
    {
        var entity = await _assessmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Training needs assessment with ID '{id}' not found.");
        return entity;
    }

    // A recommendation owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<TrainingNeedsAssessmentProgram> GetOwnedRecommendedProgramAsync(Guid id)
    {
        var entity = await _programRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Recommended program with ID '{id}' not found.");
        return entity;
    }

    // A skill gap owned by another tenant is reported as missing rather than forbidden, so the endpoints
    // do not confirm that the id exists elsewhere.
    private async Task<TrainingNeedsAssessmentSkill> GetOwnedSkillGapAsync(Guid id)
    {
        var entity = await _skillRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Skill gap with ID '{id}' not found.");
        return entity;
    }

    // ── Assessment queries ────────────────────────────────────────────────────

    public async Task<TrainingNeedsAssessmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _assessmentRepository.GetWithFullDetailsAsync(id);

        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Training needs assessment with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _assessmentRepository.GetAllAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _assessmentRepository.GetByYearAsync(year);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<PagedResult<TrainingNeedsAssessmentSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var query = _assessmentRepository.GetQueryable().Where(a => a.TenantId == tenantId);
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
        var tenantId = GetTenantId();
        var entities = await _assessmentRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.Employee.OrganizationUnitId == departmentId)
            .ToListAsync(cancellationToken);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetUnfulfilledAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _assessmentRepository.GetUnfulfilledAsync();
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByPriorityAsync(TrainingPriority priority, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _assessmentRepository.GetByPriorityAsync(priority);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _assessmentRepository.GetByEmployeeIdAsync(employeeId);
        return entities.Where(e => e.TenantId == tenantId).ToSummaryDtoList();
    }

    // ── Assessment CRUD ───────────────────────────────────────────────────────

    // Named for what the controller actually passes — ICurrentUserService.EmployeeId, not a user id.
    public async Task<TrainingNeedsAssessmentDto> CreateAsync(CreateTrainingNeedsAssessmentDto dto, Guid tenantId, Guid createdByEmployeeId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        // IdentifiedById is [Required] on the DTO, but that attribute is a no-op on a non-nullable
        // Guid — an omitted value arrives as Guid.Empty and hits the Employees FK as a raw SQL 547.
        // Default it to the caller instead (the module's actor-from-the-token convention: whoever
        // records the need is the one who identified it), and validate an explicit id up front so a
        // bad one reads as a business rule rather than a 500.
        if (dto.IdentifiedById == Guid.Empty)
        {
            dto.IdentifiedById = createdByEmployeeId;
        }
        else
        {
            var identifier = await _employeeRepository.GetByIdAsync(dto.IdentifiedById);
            if (identifier == null || identifier.TenantId != current)
                throw new ArgumentException($"Employee with ID '{dto.IdentifiedById}' not found.");
        }

        var subject = await _employeeRepository.GetByIdAsync(dto.EmployeeId);
        if (subject == null || subject.TenantId != current)
            throw new ArgumentException($"Employee with ID '{dto.EmployeeId}' not found.");

        var entity = dto.ToEntity(current, createdByEmployeeId);

        await _assessmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training needs assessment created for employee {EmployeeId}", dto.EmployeeId);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<BulkNeedsAssessmentResultDto> BulkCreateAsync(BulkCreateTrainingNeedsAssessmentDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

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
            }.ToEntity(current, createdByUserId);

            await _assessmentRepository.AddAsync(entity);
            result.CreatedCount++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bulk needs assessment: {Count} created", result.CreatedCount);
        return result;
    }

    public async Task<TrainingNeedsAssessmentDto> UpdateAsync(UpdateTrainingNeedsAssessmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssessmentAsync(dto.Id);

        entity.UpdateEntity(dto, updatedByUserId);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _assessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAssessmentAsync(id);

        await _assessmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Training needs assessment {AssessmentId} deleted", id);

        return true;
    }

    // ── Recommended program sub-operations ───────────────────────────────────

    public async Task<TrainingNeedsAssessmentProgramDto> AddRecommendedProgramAsync(CreateTrainingNeedsAssessmentProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAssessmentAsync(dto.AssessmentId);

        var program = await _trainingProgramRepository.GetByIdAsync(dto.ProgramId);
        if (program == null || program.TenantId != current)
            throw new ArgumentException($"Training program with ID '{dto.ProgramId}' not found.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _programRepository.GetByAssessmentIdAsync(dto.AssessmentId);
        return reloaded.First(p => p.Id == entity.Id).ToDto();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentProgramDto>> GetRecommendedProgramsAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _programRepository.GetByAssessmentIdAsync(assessmentId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteRecommendedProgramAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRecommendedProgramAsync(id);

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    // ── Skill gap sub-operations ──────────────────────────────────────────────

    public async Task<TrainingNeedsAssessmentSkillDto> AddSkillGapAsync(CreateTrainingNeedsAssessmentSkillDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");

        await GetOwnedAssessmentAsync(dto.AssessmentId);

        var skill = await _skillCatalogRepository.GetByIdAsync(dto.SkillId);
        if (skill == null || skill.TenantId != current)
            throw new ArgumentException($"Skill with ID '{dto.SkillId}' not found.");

        var entity = dto.ToEntity(current, createdByUserId);

        await _skillRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _skillRepository.GetByAssessmentIdAsync(dto.AssessmentId);
        return reloaded.First(s => s.Id == entity.Id).ToDto();
    }

    public async Task<IEnumerable<TrainingNeedsAssessmentSkillDto>> GetSkillGapsAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _skillRepository.GetByAssessmentIdAsync(assessmentId);
        return entities.Where(e => e.TenantId == tenantId).Select(e => e.ToDto()).ToList();
    }

    public async Task<bool> DeleteSkillGapAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedSkillGapAsync(id);

        await _skillRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
