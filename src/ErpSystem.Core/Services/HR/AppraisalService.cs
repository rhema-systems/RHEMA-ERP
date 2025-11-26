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

#region Appraisal Grade Definition

public class AppraisalGradeDefinitionService : IAppraisalGradeDefinitionService
{
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeDefinitionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveService> _logger;

    public AppraisalGradeDefinitionService(IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository, IUnitOfWork unitOfWork, ILogger<LeaveService> logger)
    {
        _gradeDefinitionRepository = gradeDefinitionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AppraisalGradeDefinitionDto> CreateAsync(CreateAppraisalGradeDefinitionDto createDto, CancellationToken cancellationToken = default)
    {
        var gradeDefinition = createDto.ToEntity();

        await _gradeDefinitionRepository.AddAsync(gradeDefinition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Appraisal grade definition created successfully: {gradeDefinitionId}", gradeDefinition.Id);

        return gradeDefinition.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _gradeDefinitionRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"Grade definition with ID '{id}' not found.");
        }

        await _gradeDefinitionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grade definition deleted: {id}", id);

        return true;
    }

    public async Task<IEnumerable<AppraisalGradeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var gradeDefinitions = await _gradeDefinitionRepository.GetAllAsync();
        return gradeDefinitions.ToDtoList();
    }

    public async Task<AppraisalGradeDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {

        var gradeDefinition = await _gradeDefinitionRepository
                .GetQueryable()
                .Include(g => g.MappingGradeRanges)
                .FirstOrDefaultAsync(g => g.Id == id);

        if (gradeDefinition == null)
        {
            throw new ArgumentException($"Grade definition with ID '{id}' not found.");
        }

        // var dto = _mapper.Map<AppraisalGradeDefinitionDto>(entity);
        return gradeDefinition.ToDto();
    }

    public async Task<PagedResult<AppraisalGradeDefinitionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var definitionsQuery = _gradeDefinitionRepository.GetQueryable();
        var totalCount = await definitionsQuery.CountAsync(cancellationToken);

        var pagedGrades = await definitionsQuery.Skip((pageNumber - 1) * pageSize)
                                                .Take(pageSize)
                                                .ToListAsync(cancellationToken);

        var gradeDtos = pagedGrades.ToDtoList();

        return new PagedResult<AppraisalGradeDefinitionDto>
        {
            Items = gradeDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalGradeDefinitionDto> UpdateAsync(UpdateAppraisalGradeDefinitionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _gradeDefinitionRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
        {
            throw new ArgumentException($"Appraisal grade definition with ID '{updateDto.Id}' not found.");
        }

        updateDto.UpdateEntity(entity);

        await _gradeDefinitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal grade definition updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }
}

#endregion Appraisal Grade Definition

#region KPI Definition

public class KpiDefinitionService : IKpiDefinitionService
{
    private readonly IGenericRepository<KpiDefinition> _kpiDefinitionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<KpiDefinitionService> _logger;

    public KpiDefinitionService(
        IGenericRepository<KpiDefinition> kpiDefinitionRepository,
        IUnitOfWork unitOfWork,
        ILogger<KpiDefinitionService> logger)
    {
        _kpiDefinitionRepository = kpiDefinitionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<KpiDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiDefinitionRepository.GetByIdAsync(id);

        if (entity == null)
            throw new ArgumentException($"KPI definition with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<KpiDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var kpiDefinitions = await _kpiDefinitionRepository.GetAllAsync();
        return kpiDefinitions.ToDtoList();
    }

    public async Task<PagedResult<KpiDefinitionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var definitionsQuery = _kpiDefinitionRepository.GetQueryable();
        var totalCount = await definitionsQuery.CountAsync(cancellationToken);

        var pagedDefinitions = await definitionsQuery.Skip((pageNumber - 1) * pageSize)
                                                .Take(pageSize)
                                                .ToListAsync(cancellationToken);

        var gradeDtos = pagedDefinitions.ToDtoList();

        return new PagedResult<KpiDefinitionDto>
        {
            Items = gradeDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<KpiDefinitionDto> CreateAsync(CreateKpiDefinitionDto createDto, CancellationToken cancellationToken = default)
    {
        var kpiDefinition = createDto.ToEntity();

        await _kpiDefinitionRepository.AddAsync(kpiDefinition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("KPI definition created: {kpiDefinitionId}", kpiDefinition.Id);

        return kpiDefinition.ToDto();
    }

    public async Task<KpiDefinitionDto> UpdateAsync(UpdateKpiDefinitionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiDefinitionRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
        {
            throw new ArgumentException($"KPI definition with ID '{updateDto.Id}' not found.");
        }

        updateDto.UpdateEntity(entity);

        await _kpiDefinitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("KPI definition updated successfully: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiDefinitionRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"KPI definition with ID '{id}' not found.");
        }

        await _kpiDefinitionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("KPI definition deleted successfully: {id}", id);

        return true;
    }
}

#endregion KPI Definition

#region Appraisal Criteria

public class AppraisalCriteriaService : IAppraisalCriteriaService
{
    private readonly IGenericRepository<AppraisalCriteria> _appraisalCriteriaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCriteriaService> _logger;

    public AppraisalCriteriaService(
        IGenericRepository<AppraisalCriteria> appraisalCriteriaRepository,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCriteriaService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _appraisalCriteriaRepository = appraisalCriteriaRepository;
    }

    public async Task<AppraisalCriteriaDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalCriteriaRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"Appraisal criteria with ID '{id}' not found.");
        }

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCriteriaDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalCriteriaRepository.GetAllAsync();
        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalCriteriaDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var criteriaQuery = _appraisalCriteriaRepository.GetQueryable();
        var totalCount = await criteriaQuery.CountAsync(cancellationToken);

        var pagedCriteria = await criteriaQuery.Skip((pageNumber - 1) * pageSize)
                                                .Take(pageSize)
                                                .ToListAsync(cancellationToken);

        var criteriaDtos = pagedCriteria.ToDtoList();

        return new PagedResult<AppraisalCriteriaDto>
        {
            Items = criteriaDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalCriteriaDto> CreateAsync(CreateAppraisalCriteriaDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();

        await _appraisalCriteriaRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal criteria created successfully: {criteriaId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<AppraisalCriteriaDto> UpdateAsync(UpdateAppraisalCriteriaDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalCriteriaRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
        {
            throw new ArgumentException($"Appraisal criteria with ID '{updateDto.Id}' not found.");
        }

        updateDto.UpdateEntity(entity);
        await _appraisalCriteriaRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal criteria updated successfully: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalCriteriaRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"Appraisal criteria with ID '{id}' not found.");
        }

        await _appraisalCriteriaRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal criteria deleted successfully: {id}", id);

        return true;
    }
}

#endregion Appraisal Criteria

#region Performance Appraisal

public class PerformanceAppraisalService : IPerformanceAppraisalService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalCriteria> _appraisalCriteriaRepository;
    private readonly IGenericRepository<EvaluatorEvaluation> _evaluatorEvaluationRepository;
    private readonly IGenericRepository<PositionCriteriaMapping> _positionCriteriaMappingRepository;
    private readonly IGenericRepository<CriterionScore> _criterionScoreRepository;
    private readonly IGenericRepository<AppraisalEmployeeResponse> _employeeResponseRepository;
    private readonly IGenericRepository<AppraisalAttachment> _appraisalAttachmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PerformanceAppraisal> _logger;

    public PerformanceAppraisalService(IGenericRepository<PerformanceAppraisal> appraisalRepository, IUnitOfWork unitOfWork, ILogger<PerformanceAppraisal> logger, IGenericRepository<Employee> employeeRepository, IGenericRepository<EvaluatorEvaluation> evaluatorEvaluationRepository, IGenericRepository<AppraisalCriteria> appraisalCriteriaRepository, IGenericRepository<PositionCriteriaMapping> positionCriteriaMappingRepository, IGenericRepository<CriterionScore> criterionScoreRepository, IGenericRepository<AppraisalEmployeeResponse> employeeResponseRepository, IGenericRepository<AppraisalAttachment> appraisalAttachmentRepository)
    {
        _appraisalRepository = appraisalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _employeeRepository = employeeRepository;
        _evaluatorEvaluationRepository = evaluatorEvaluationRepository;
        _appraisalCriteriaRepository = appraisalCriteriaRepository;
        _positionCriteriaMappingRepository = positionCriteriaMappingRepository;
        _criterionScoreRepository = criterionScoreRepository;
        _employeeResponseRepository = employeeResponseRepository;
        _appraisalAttachmentRepository = appraisalAttachmentRepository;
    }

    public async Task<bool> CalculateOverallScoreAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
                                                .Include(p => p.EvaluatorEvaluations)
                                                    .ThenInclude(e => e.CriterionScores)
                                                .FirstOrDefaultAsync(p => p.Id == appraisalId, cancellationToken);

        if (appraisal == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{appraisalId}' not found.");
        }

        // Calculate weighted average across all evaluations
        decimal totalWeightedScore = 0;
        decimal totalWeight = 0;

        foreach (var evaluation in appraisal.EvaluatorEvaluations)
        {
            decimal evaluationScore = 0;
            if (evaluation.CriterionScores.Any())
            {
                evaluationScore = evaluation.CriterionScores.Average(cs => cs.WeightedScore);
            }

            totalWeightedScore += evaluationScore * evaluation.EvaluatorWeight;
            totalWeight += evaluation.EvaluatorWeight;
        }

        appraisal.OverallScore = totalWeight > 0 ? totalWeightedScore / totalWeight : 0;

        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<PerformanceAppraisalDto> CreateAsync(CreatePerformanceAppraisalDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();

        entity.AppraisalNumber = await GenerateAppraisalNumberAsync(createDto.Year, cancellationToken);
        entity.Status = AppraisalStatus.Open;

        await _appraisalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal created successfully: {appraisalId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{id}' not found.");
        }

        await _appraisalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal deleted: {id}", id);

        return true;
    }

    public async Task<bool> FileAppealAsync(FileAppraisalAppealDto appealDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(appealDto.AppraisalId);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{appealDto.AppraisalId}' not found.");
        }

        entity.AppealFiled = true;
        entity.AppealDate = DateTime.UtcNow;
        entity.AppealReason = appealDto.AppealReason;

        await _appraisalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal filed successfully: {id}", appealDto.AppraisalId);

        return true;
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var performanceAppraisals = await _appraisalRepository.GetAllAsync();
        return performanceAppraisals.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalRepository.GetQueryable()
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .Where(p => p.EmployeeId == employeeId)
                                                .OrderByDescending(p => p.Year)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PerformanceAppraisalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetQueryable()
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Department)
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Position)
                                            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{id}' not found.");
        }

        return entity.ToDto();
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByStatusAsync(AppraisalStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalRepository.GetQueryable(p => p.Status == status)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalRepository.GetQueryable(p => p.Year == year)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<PerformanceAppraisalDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _appraisalRepository.GetQueryable();
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query.OrderByDescending(p => p.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync(cancellationToken);

        var appraisalDtos = items.ToDtoList();

        return new PagedResult<PerformanceAppraisalDto>
        {
            Items = appraisalDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<bool> ResolveAppealAsync(ResolveAppraisalAppealDto resolveDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(resolveDto.AppraisalId);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{resolveDto.AppraisalId}' not found.");
        }

        if (!entity.AppealFiled)
        {
            throw new InvalidOperationException($"No appeal filed for this appraisal '{resolveDto.AppraisalId}'.");
        }

        entity.AppealOutcome = resolveDto.AppealOutcome;

        await _appraisalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal resolved successfully: {appraisalNumber}", entity.AppraisalNumber);

        return true;
    }

    public async Task<PerformanceAppraisalDto> UpdateAsync(UpdatePerformanceAppraisalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetQueryable()
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Department)
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Position)
                                            .FirstOrDefaultAsync(p => p.Id == updateDto.Id, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{updateDto.Id}' not found.");
        }

        updateDto.UpdateEntity(entity);

        await _appraisalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal updated successfully: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> UpdateStatusAsync(UpdateAppraisalStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(statusDto.AppraisalId);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{statusDto.AppraisalId}' not found.");
        }

        entity.Status = statusDto.Status;

        await _appraisalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal status updated successfully: {Id}", statusDto.AppraisalId);

        return true;
    }

    private async Task<string> GenerateAppraisalNumberAsync(int year, CancellationToken cancellationToken)
    {
        var count = await _appraisalRepository.GetQueryable(p => p.Year == year)
                                            .CountAsync(cancellationToken);

        return $"APR-{year}-{(count + 1):D5}";
    }

    #region EvaluatorEvaluation Operations

    public async Task<EvaluatorEvaluationDto> AddEvaluatorEvaluationAsync(Guid appraisalId, CreateEvaluatorEvaluationDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate appraisal exists
        var appraisal = await _appraisalRepository.GetQueryable()
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found");

        // Validate evaluator exists
        var evaluatorExists = await _employeeRepository.ExistsAsync(e => e.Id == createDto.EvaluatorId);
        if (!evaluatorExists)
            throw new ArgumentException("Evaluator not found");

        // Check if evaluator already evaluated this appraisal
        var duplicateExists = appraisal.EvaluatorEvaluations
            .Any(e => e.EvaluatorId == createDto.EvaluatorId && e.EvaluatorRole == createDto.EvaluatorRole);

        if (duplicateExists)
        {
            throw new InvalidOperationException("This evaluator has already submitted an evaluation for this appraisal with the same role");
        }

        // Validate total evaluator weights don't exceed 1.0
        var totalWeight = appraisal.EvaluatorEvaluations.Sum(e => e.EvaluatorWeight) + createDto.EvaluatorWeight;
        if (totalWeight > 1.0m)
        {
            throw new InvalidOperationException($"Total evaluator weight cannot exceed 1.0. Current total: {appraisal.EvaluatorEvaluations.Sum(e => e.EvaluatorWeight)}, attempting to add: {createDto.EvaluatorWeight}");
        }

        var entity = createDto.ToEntity();
        entity.AppraisalId = appraisalId;

        await _evaluatorEvaluationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.Evaluator)
            .FirstOrDefaultAsync(e => e.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Evaluator evaluation added successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<EvaluatorEvaluationDto>> GetEvaluatorEvaluationsAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await _evaluatorEvaluationRepository.GetQueryable(e => e.AppraisalId == appraisalId)
                                                .Include(e => e.Evaluator)
                                                .OrderByDescending(e => e.IsAuthoritative)
                                                .ThenByDescending(e => e.EvaluatorWeight)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<EvaluatorEvaluationDto> UpdateEvaluatorEvaluationAsync(Guid appraisalId, UpdateEvaluatorEvaluationDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.Evaluator)
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(e => e.Id == updateDto.Id && e.AppraisalId == appraisalId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Evaluator evaluation not found");

        // Validate total weights (excluding current)
        var otherEvaluations = entity.Appraisal.EvaluatorEvaluations.Where(e => e.Id != updateDto.Id);
        var totalWeight = otherEvaluations.Sum(e => e.EvaluatorWeight) + updateDto.EvaluatorWeight;

        if (totalWeight > 1.0m)
        {
            throw new InvalidOperationException($"Total evaluator weight cannot exceed 1.0. Current total would be: {totalWeight}");
        }

        updateDto.UpdateEntity(entity);
        await _evaluatorEvaluationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Evaluator evaluation updated successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteEvaluatorEvaluationAsync(Guid appraisalId, Guid evaluationId, CancellationToken cancellationToken = default)
    {
        var entity = await _evaluatorEvaluationRepository.GetQueryable()
                                                        .FirstOrDefaultAsync(e => e.Id == evaluationId && e.AppraisalId == appraisalId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Evaluator evaluation not found");

        await _evaluatorEvaluationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Evaluator evaluation deleted successfully");

        return true;
    }

    #endregion

    #region CriterionScore Operations

    public async Task<CriterionScoreDto> AddCriterionScoreAsync(Guid evaluationId, CreateCriterionScoreDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate evaluation exists and get criteria mapping info
        var evaluation = await _evaluatorEvaluationRepository.GetQueryable()
            .Include(e => e.Appraisal)
                .ThenInclude(a => a.Employee)
                    .ThenInclude(emp => emp.Position)
            .Include(e => e.CriterionScores)
            .FirstOrDefaultAsync(e => e.Id == evaluationId, cancellationToken);

        if (evaluation == null)
            throw new ArgumentException("Evaluator evaluation not found");

        // Validate criteria exists
        var criteriaExists = await _appraisalCriteriaRepository.ExistsAsync(c => c.Id == createDto.CriteriaId);
        if (!criteriaExists)
            throw new ArgumentException("Appraisal criteria not found");

        // Check if this criteria has already been scored by this evaluator
        var duplicateExists = evaluation.CriterionScores.Any(cs => cs.CriteriaId == createDto.CriteriaId);
        if (duplicateExists)
        {
            throw new InvalidOperationException("This criteria has already been scored in this evaluation");
        }

        // Get the criteria mapping to retrieve weight
        var mapping = await _positionCriteriaMappingRepository.GetQueryable()
                                                            .FirstOrDefaultAsync(m => m.CriteriaId == createDto.CriteriaId &&
                                                                                    m.PositionId == evaluation.Appraisal.Employee.PositionId,
                                                                                cancellationToken);

        var entity = createDto.ToEntity();
        entity.EvaluatorEvaluationId = evaluationId;

        // Calculate weighted score
        if (createDto.NumericScore.HasValue && mapping != null)
        {
            entity.WeightedScore = (createDto.NumericScore.Value * mapping.Weight) / 100m;
        }

        await _criterionScoreRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _criterionScoreRepository.GetQueryable()
                                                .Include(cs => cs.AppraisalCriteria)
                                                .FirstOrDefaultAsync(cs => cs.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Criterion score added successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<CriterionScoreDto>> GetCriterionScoresAsync(Guid evaluationId, CancellationToken cancellationToken = default)
    {
        var entities = await _criterionScoreRepository.GetQueryable(cs => cs.EvaluatorEvaluationId == evaluationId)
                                            .Include(cs => cs.AppraisalCriteria)
                                            .OrderBy(cs => cs.AppraisalCriteria.CriteriaName)
                                            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<CriterionScoreDto> UpdateCriterionScoreAsync(Guid evaluationId, UpdateCriterionScoreDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _criterionScoreRepository.GetQueryable()
            .Include(cs => cs.AppraisalCriteria)
            .Include(cs => cs.EvaluatorEvaluation)
                .ThenInclude(e => e.Appraisal)
                    .ThenInclude(a => a.Employee)
            .FirstOrDefaultAsync(cs => cs.Id == updateDto.Id && cs.EvaluatorEvaluationId == evaluationId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Criterion score not found");

        // Get the criteria mapping to retrieve weight
        var mapping = await _positionCriteriaMappingRepository.GetQueryable()
            .FirstOrDefaultAsync(m => m.CriteriaId == updateDto.CriteriaId &&
                                     m.PositionId == entity.EvaluatorEvaluation.Appraisal.Employee.PositionId,
                                cancellationToken);

        updateDto.UpdateEntity(entity);

        // Recalculate weighted score
        if (updateDto.NumericScore.HasValue && mapping != null)
        {
            entity.WeightedScore = (updateDto.NumericScore.Value * mapping.Weight) / 100m;
        }

        await _criterionScoreRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Criterion score updated successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCriterionScoreAsync(Guid evaluationId, Guid scoreId, CancellationToken cancellationToken = default)
    {
        var entity = await _criterionScoreRepository.GetQueryable()
            .FirstOrDefaultAsync(cs => cs.Id == scoreId && cs.EvaluatorEvaluationId == evaluationId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Criterion score not found");

        await _criterionScoreRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Criterion score deleted successfully");

        return true;
    }

    #endregion

    #region AppraisalEmployeeResponse Operations

    public async Task<AppraisalEmployeeResponseDto> AddEmployeeResponseAsync(Guid appraisalId, CreateAppraisalEmployeeResponseDto createDto, CancellationToken cancellationToken = default)
    {
        var appraisalExists = await _appraisalRepository.ExistsAsync(a => a.Id == appraisalId);
        if (!appraisalExists)
            throw new ArgumentException("Performance appraisal not found");

        if (createDto.CriteriaId.HasValue)
        {
            var criteriaExists = await _appraisalCriteriaRepository.ExistsAsync(c => c.Id == createDto.CriteriaId.Value);
            if (!criteriaExists)
                throw new ArgumentException("Appraisal criteria not found");
        }

        var entity = createDto.ToEntity();
        entity.AppraisalId = appraisalId;

        await _employeeResponseRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _employeeResponseRepository.GetQueryable()
            .Include(r => r.AppraisalCriteria)
            .FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Employee response added successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<AppraisalEmployeeResponseDto>> GetEmployeeResponsesAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await _employeeResponseRepository.GetQueryable(r => r.AppraisalId == appraisalId)
                                                        .Include(r => r.AppraisalCriteria)
                                                        .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    #endregion

    #region AppraisalAttachment Operations

    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(Guid appraisalId, CreateAppraisalAttachmentDto createDto, CancellationToken cancellationToken = default)
    {
        var appraisalExists = await _appraisalRepository.ExistsAsync(a => a.Id == appraisalId);
        if (!appraisalExists)
            throw new ArgumentException("Performance appraisal not found");

        var entity = createDto.ToEntity();
        entity.AppraisalId = appraisalId;

        await _appraisalAttachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Attachment added successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalAttachmentRepository.GetQueryable(a => a.AppraisalId == appraisalId)
                                                .OrderByDescending(a => a.UploadDate)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    #endregion
}

#endregion Performance Appraisal

#region Performance Improvement Plan

public class PerformanceImprovementPlanService : IPerformanceImprovementPlanService
{
    private readonly IGenericRepository<PerformanceImprovementPlan> _improvementPlanRepository;
    private readonly IGenericRepository<PipReviewMeeting> _reviewMeetingRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PerformanceImprovementPlan> _logger;

    public PerformanceImprovementPlanService(IGenericRepository<PerformanceImprovementPlan> improvementPlanRepository, IUnitOfWork unitOfWork, ILogger<PerformanceImprovementPlan> logger, IGenericRepository<Employee> employeeRepository, IGenericRepository<PipReviewMeeting> reviewMeetingRepository)
    {
        _improvementPlanRepository = improvementPlanRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _employeeRepository = employeeRepository;
        _reviewMeetingRepository = reviewMeetingRepository;
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
}

#endregion Performance Improvement Plan

#region Position Criteria Mapping

public class PositionCriteriaMappingService : IPositionCriteriaMappingService
{
    private readonly IGenericRepository<PositionCriteriaMapping> _mappingRepository;
    private readonly IGenericRepository<AppraisalCriteria> _criteriaRepository;
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeDefinitionRepository;
    private readonly IGenericRepository<MappingGradeRange> _mappingGradeRangeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PositionCriteriaMappingService> _logger;

    public PositionCriteriaMappingService(
        IGenericRepository<PositionCriteriaMapping> mappingRepository,
        IUnitOfWork unitOfWork,
        ILogger<PositionCriteriaMappingService> logger,
        IGenericRepository<AppraisalCriteria> criteriaRepository,
        IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository,
        IGenericRepository<MappingGradeRange> mappingGradeRangeRepository)
    {
        _mappingRepository = mappingRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _criteriaRepository = criteriaRepository;
        _gradeDefinitionRepository = gradeDefinitionRepository;
        _mappingGradeRangeRepository = mappingGradeRangeRepository;
    }

    public async Task<PositionCriteriaMappingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _mappingRepository.GetQueryable()
                                            .Include(m => m.Department)
                                            .Include(m => m.Position)
                                            .Include(m => m.AppraisalCriteria)
                                            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Position criteria mapping not found");

        return entity.ToDto();
    }

    public async Task<IEnumerable<PositionCriteriaMappingDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _mappingRepository.GetQueryable()
                                            .Include(m => m.Department)
                                            .Include(m => m.Position)
                                            .Include(m => m.AppraisalCriteria)
                                            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PositionCriteriaMappingDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var entities = await _mappingRepository.GetQueryable(m => m.PositionId == positionId)
                                    .Include(m => m.Department)
                                    .Include(m => m.Position)
                                    .Include(m => m.AppraisalCriteria)
                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<PositionCriteriaMappingDto>> GetByDepartmentIdAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var entities = await _mappingRepository.GetQueryable(m => m.DepartmentId == departmentId)
                                    .Include(m => m.Department)
                                    .Include(m => m.Position)
                                    .Include(m => m.AppraisalCriteria)
                                    .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PositionCriteriaMappingDto> CreateAsync(CreatePositionCriteriaMappingDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate that criteria exists
        var criteriaExists = await _criteriaRepository.ExistsAsync(c => c.Id == createDto.CriteriaId);
        if (!criteriaExists)
            throw new ArgumentException("Appraisal criteria not found");

        // Validate total weights don't exceed 100 for the position/department
        var existingMappings = await _mappingRepository
            .GetQueryable(m => (m.PositionId == createDto.PositionId || m.DepartmentId == createDto.DepartmentId))
            .ToListAsync(cancellationToken);

        var totalWeight = existingMappings.Sum(m => m.Weight) + createDto.Weight;
        if (totalWeight > 100)
        {
            throw new InvalidOperationException($"Total weight cannot exceed 100. Current total: {existingMappings.Sum(m => m.Weight)}, attempting to add: {createDto.Weight}");
        }

        var entity = createDto.ToEntity();

        await _mappingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _mappingRepository.GetQueryable()
                                        .Include(m => m.Department)
                                        .Include(m => m.Position)
                                        .Include(m => m.AppraisalCriteria)
                                        .FirstOrDefaultAsync(m => m.Id == entity.Id, cancellationToken);

        return entity!.ToDto();
    }

    public async Task<PositionCriteriaMappingDto> UpdateAsync(UpdatePositionCriteriaMappingDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _mappingRepository.GetQueryable()
                                    .Include(m => m.Department)
                                    .Include(m => m.Position)
                                    .Include(m => m.AppraisalCriteria)
                                    .FirstOrDefaultAsync(m => m.Id == updateDto.Id, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Mapping not found");

        // Validate total weights
        var existingMappings = await _mappingRepository
            .GetQueryable(m => m.Id != updateDto.Id && (m.PositionId == updateDto.PositionId || m.DepartmentId == updateDto.DepartmentId))
            .ToListAsync(cancellationToken);

        var totalWeight = existingMappings.Sum(m => m.Weight) + updateDto.Weight;
        if (totalWeight > 100)
        {
            throw new InvalidOperationException($"Total weight cannot exceed 100. Current total would be: {totalWeight}");
        }

        updateDto.UpdateEntity(entity);
        await _mappingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _mappingRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException("Mapping not found");

        await _mappingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Mapping deleted successfully");

        return true;
    }

    #region MappingGradeRange Operations

    public async Task<MappingGradeRangeDto> AddGradeRangeAsync(Guid mappingId, CreateMappingGradeRangeDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate mapping exists
        var mappingExists = await _mappingRepository.ExistsAsync(m => m.Id == mappingId);
        if (!mappingExists)
            throw new ArgumentException("Position criteria mapping not found");

        // Validate grade definition exists
        var gradeExists = await _gradeDefinitionRepository.ExistsAsync(g => g.Id == createDto.GradeDefinitionId);
        if (!gradeExists)
            throw new ArgumentException("Grade definition not found");

        // Validate no overlapping ranges
        var existingRanges = await _mappingGradeRangeRepository.GetQueryable(r => r.PositionCriteriaMappingId == mappingId)
                                                            .ToListAsync(cancellationToken);

        foreach (var range in existingRanges)
        {
            if ((createDto.LowScore >= range.LowScore && createDto.LowScore <= range.HighScore) ||
                (createDto.HighScore >= range.LowScore && createDto.HighScore <= range.HighScore))
            {
                throw new InvalidOperationException($"Score range overlaps with existing grade range: {range.LowScore}-{range.HighScore}");
            }
        }

        var entity = createDto.ToEntity();
        entity.PositionCriteriaMappingId = mappingId;

        await _mappingGradeRangeRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _mappingGradeRangeRepository.GetQueryable()
            .Include(r => r.GradeDefinition)
            .FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Grade range added successfully: {id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<MappingGradeRangeDto>> GetGradeRangesAsync(Guid mappingId, CancellationToken cancellationToken = default)
    {
        var entities = await _mappingGradeRangeRepository.GetQueryable(r => r.PositionCriteriaMappingId == mappingId)
                                                .Include(r => r.GradeDefinition)
                                                .OrderBy(r => r.LowScore)
                                                .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<MappingGradeRangeDto> UpdateGradeRangeAsync(Guid mappingId, UpdateMappingGradeRangeDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _mappingGradeRangeRepository.GetQueryable()
            .Include(r => r.GradeDefinition)
            .FirstOrDefaultAsync(r => r.Id == updateDto.Id && r.PositionCriteriaMappingId == mappingId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Grade range not found");

        // Validate no overlapping ranges (excluding current)
        var existingRanges = await _mappingGradeRangeRepository.GetQueryable(r => r.PositionCriteriaMappingId == mappingId && r.Id != updateDto.Id)
                                                            .ToListAsync(cancellationToken);

        foreach (var range in existingRanges)
        {
            if ((updateDto.LowScore >= range.LowScore && updateDto.LowScore <= range.HighScore) ||
                (updateDto.HighScore >= range.LowScore && updateDto.HighScore <= range.HighScore))
            {
                throw new InvalidOperationException($"Score range overlaps with existing grade range: {range.LowScore}-{range.HighScore}");
            }
        }

        updateDto.UpdateEntity(entity);
        await _mappingGradeRangeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grade range updated successfully: {id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteGradeRangeAsync(Guid mappingId, Guid rangeId, CancellationToken cancellationToken = default)
    {
        var entity = await _mappingGradeRangeRepository.FirstOrDefaultAsync(r => r.Id == rangeId && r.PositionCriteriaMappingId == mappingId);

        if (entity == null)
            throw new ArgumentException("Grade range not found");

        await _mappingGradeRangeRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grade range deleted successfully");

        return true;
    }

    #endregion
}

#endregion Position Criteria Mapping

#region Employee KPI Target

public class EmployeeKpiTargetService : IEmployeeKpiTargetService
{
    private readonly IGenericRepository<EmployeeKpiTarget> _kpiTargetRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<KpiDefinition> _kpiDefinitionRepository;
    private readonly IGenericRepository<KpiEvaluationRecord> _kpiEvaluationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeKpiTargetService> _logger;

    public EmployeeKpiTargetService(
        IUnitOfWork unitOfWork,
        ILogger<EmployeeKpiTargetService> logger,
        IGenericRepository<EmployeeKpiTarget> kpiTargetRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<KpiDefinition> kpiDefinitionRepository,
        IGenericRepository<KpiEvaluationRecord> kpiEvaluationRepository)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _kpiTargetRepository = kpiTargetRepository;
        _employeeRepository = employeeRepository;
        _kpiDefinitionRepository = kpiDefinitionRepository;
        _kpiEvaluationRepository = kpiEvaluationRepository;
    }

    public async Task<EmployeeKpiTargetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiTargetRepository.GetQueryable()
                                            .Include(t => t.Employee)
                                            .Include(t => t.KpiDefinition)
                                            .Include(t => t.PositionCriteriaMapping)
                                            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Employee KPI target not found");

        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeKpiTargetDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await _kpiTargetRepository.GetQueryable(t => t.EmployeeId == employeeId)
                                        .Include(t => t.Employee)
                                        .Include(t => t.KpiDefinition)
                                        .Include(t => t.PositionCriteriaMapping)
                                        .OrderByDescending(t => t.PeriodStart)
                                        .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<IEnumerable<EmployeeKpiTargetDto>> GetByPeriodAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var entities = await _kpiTargetRepository.GetQueryable(t => t.PeriodStart >= startDate && t.PeriodEnd <= endDate)
                                        .Include(t => t.Employee)
                                        .Include(t => t.KpiDefinition)
                                        .Include(t => t.PositionCriteriaMapping)
                                        .OrderBy(t => t.Employee.FirstName)
                                        .ThenBy(t => t.Employee.LastName)
                                        .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<EmployeeKpiTargetDto> CreateAsync(CreateEmployeeKpiTargetDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate employee exists
        var employeeExists = await _employeeRepository.ExistsAsync(e => e.Id == createDto.EmployeeId);
        if (!employeeExists)
            throw new ArgumentException("Employee not found");

        // Validate KPI definition exists
        var kpiExists = await _kpiDefinitionRepository.ExistsAsync(k => k.Id == createDto.KpiDefinitionId);
        if (!kpiExists)
            throw new ArgumentException("KPI definition not found");

        // Check for duplicate target in the same period
        var duplicateExists = await _kpiTargetRepository.GetQueryable()
                                                        .AnyAsync(t => t.EmployeeId == createDto.EmployeeId &&
                                                                    t.KpiDefinitionId == createDto.KpiDefinitionId &&
                                                                    t.PeriodStart == createDto.PeriodStart &&
                                                                    t.PeriodEnd == createDto.PeriodEnd,
                                                                    cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("A KPI target for this employee, KPI, and period already exists");
        }

        var entity = createDto.ToEntity();

        await _kpiTargetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _kpiTargetRepository.GetQueryable()
                                        .Include(t => t.Employee)
                                        .Include(t => t.KpiDefinition)
                                        .Include(t => t.PositionCriteriaMapping)
                                        .FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken);

        _logger.LogInformation("KPI target created successfully: {id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<EmployeeKpiTargetDto> UpdateAsync(UpdateEmployeeKpiTargetDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiTargetRepository.GetQueryable()
                                    .Include(t => t.Employee)
                                    .Include(t => t.KpiDefinition)
                                    .Include(t => t.PositionCriteriaMapping)
                                    .FirstOrDefaultAsync(t => t.Id == updateDto.Id, cancellationToken);

        if (entity == null)
            throw new ArgumentException("KPI target not found");

        updateDto.UpdateEntity(entity);
        await _kpiTargetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("KPI target updated successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiTargetRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException("KPI target not found");

        await _kpiTargetRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("KPI target deleted successfully");

        return true;
    }

    #region KpiEvaluationRecord Operations

    public async Task<KpiEvaluationRecordDto> AddEvaluationRecordAsync(Guid targetId, CreateKpiEvaluationRecordDto createDto, CancellationToken cancellationToken = default)
    {
        // Validate target exists
        var target = await _kpiTargetRepository.GetQueryable()
                                            .Include(t => t.KpiDefinition)
                                            .FirstOrDefaultAsync(t => t.Id == targetId, cancellationToken);

        if (target == null)
            throw new ArgumentException("KPI target not found");

        // Validate evaluator exists
        var evaluatorExists = await _employeeRepository.ExistsAsync(e => e.Id == createDto.EvaluatorId);
        if (!evaluatorExists)
            throw new ArgumentException("Evaluator not found");

        // Check if final evaluation already exists (only one final evaluation allowed)
        if (createDto.IsFinal)
        {
            var finalExists = await _kpiEvaluationRepository.GetQueryable()
                .AnyAsync(r => r.EmployeeKpiTargetId == targetId && r.IsFinal, cancellationToken);

            if (finalExists)
            {
                throw new InvalidOperationException("A final evaluation already exists for this KPI target");
            }
        }

        var entity = createDto.ToEntity();
        entity.EmployeeKpiTargetId = targetId;

        // Calculate achievement percentage
        entity.AchievementPercent = CalculateAchievementPercent(
            target.KpiDefinition.MeasurementType,
            entity.ActualValue,
            target.TargetValue,
            target.MinValue,
            target.MaxValue,
            target.KpiDefinition.TolerancePercent);

        await _kpiEvaluationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Reload with includes
        entity = await _kpiEvaluationRepository.GetQueryable()
            .Include(r => r.Evaluator)
            .Include(r => r.EmployeeKpiTarget)
            .FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Evaluation record created successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<KpiEvaluationRecordDto>> GetEvaluationRecordsAsync(Guid targetId, CancellationToken cancellationToken = default)
    {
        var entities = await _kpiEvaluationRepository.GetQueryable(r => r.EmployeeKpiTargetId == targetId)
                                            .Include(r => r.Evaluator)
                                            .Include(r => r.EmployeeKpiTarget)
                                            .OrderByDescending(r => r.EvaluationDate)
                                            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<KpiEvaluationRecordDto> UpdateEvaluationRecordAsync(Guid targetId, UpdateKpiEvaluationRecordDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiEvaluationRepository.GetQueryable()
            .Include(r => r.Evaluator)
            .Include(r => r.EmployeeKpiTarget)
                .ThenInclude(t => t.KpiDefinition)
            .FirstOrDefaultAsync(r => r.Id == updateDto.Id && r.EmployeeKpiTargetId == targetId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Evaluation record not found");

        // Check if trying to mark as final when another final exists
        if (updateDto.IsFinal && !entity.IsFinal)
        {
            var otherFinalExists = await _kpiEvaluationRepository.GetQueryable()
                .AnyAsync(r => r.EmployeeKpiTargetId == targetId && r.IsFinal && r.Id != updateDto.Id, cancellationToken);

            if (otherFinalExists)
            {
                throw new InvalidOperationException("Another final evaluation already exists for this KPI target");
            }
        }

        updateDto.UpdateEntity(entity);

        // Recalculate achievement percentage
        entity.AchievementPercent = CalculateAchievementPercent(
            entity.EmployeeKpiTarget.KpiDefinition.MeasurementType,
            entity.ActualValue,
            entity.EmployeeKpiTarget.TargetValue,
            entity.EmployeeKpiTarget.MinValue,
            entity.EmployeeKpiTarget.MaxValue,
            entity.EmployeeKpiTarget.KpiDefinition.TolerancePercent);

        await _kpiEvaluationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Evaluation record updated successfully: {Id}", entity.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteEvaluationRecordAsync(Guid targetId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiEvaluationRepository.GetQueryable()
            .FirstOrDefaultAsync(r => r.Id == recordId && r.EmployeeKpiTargetId == targetId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Evaluation record not found");

        await _kpiEvaluationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Evaluation record deleted successfully");

        return true;
    }

    public async Task<KpiEvaluationRecordDto> GetFinalEvaluationAsync(Guid targetId, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiEvaluationRepository.GetQueryable()
            .Include(r => r.Evaluator)
            .Include(r => r.EmployeeKpiTarget)
            .FirstOrDefaultAsync(r => r.EmployeeKpiTargetId == targetId && r.IsFinal, cancellationToken);

        if (entity == null)
            throw new InvalidOperationException("No final evaluation found for this KPI target");

        return entity.ToDto();
    }

    #endregion

    #region Helper Methods

    private decimal CalculateAchievementPercent(
        MeasurementType measurementType,
        decimal? actualValue,
        decimal? targetValue,
        decimal? minValue,
        decimal? maxValue,
        decimal? tolerancePercent)
    {
        if (!actualValue.HasValue || !targetValue.HasValue || targetValue.Value == 0)
            return 0;

        decimal achievement = 0;

        switch (measurementType)
        {
            case MeasurementType.NumericAbsolute:
                // Calculate percentage of target achieved
                achievement = (actualValue.Value / targetValue.Value) * 100;

                // Apply tolerance
                if (tolerancePercent.HasValue)
                {
                    var lowerBound = targetValue.Value * (1 - tolerancePercent.Value / 100);
                    var upperBound = targetValue.Value * (1 + tolerancePercent.Value / 100);

                    if (actualValue.Value >= lowerBound && actualValue.Value <= upperBound)
                    {
                        achievement = 100; // Within tolerance = 100%
                    }
                }
                break;

            case MeasurementType.PercentageTarget:
                // Direct percentage comparison
                achievement = actualValue.Value;
                break;

            case MeasurementType.Boolean:
                // Boolean: 100% if achieved, 0% if not
                achievement = actualValue.Value >= 1 ? 100 : 0;
                break;

            case MeasurementType.Range:
                // Calculate position within range
                if (minValue.HasValue && maxValue.HasValue && maxValue.Value > minValue.Value)
                {
                    var rangeSize = maxValue.Value - minValue.Value;
                    var positionInRange = actualValue.Value - minValue.Value;
                    achievement = (positionInRange / rangeSize) * 100;
                    achievement = Math.Max(0, Math.Min(100, achievement)); // Clamp between 0-100
                }
                break;
        }

        return Math.Round(achievement, 2);
    }

    #endregion
}

#endregion Employee KPI Target
