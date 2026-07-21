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

        var pagedGrades = await definitionsQuery.OrderBy(g => g.GradeName)
                                                .Skip((pageNumber - 1) * pageSize)
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

