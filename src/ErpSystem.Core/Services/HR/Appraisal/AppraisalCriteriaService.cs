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

#region Appraisal Competency

public class AppraisalCompetencyService : IAppraisalCompetencyService
{
    private readonly IGenericRepository<AppraisalCompetency> _appraisalCompetencyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCompetencyService> _logger;

    public AppraisalCompetencyService(
        IGenericRepository<AppraisalCompetency> appraisalCompetencyRepository,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCompetencyService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _appraisalCompetencyRepository = appraisalCompetencyRepository;
    }

    public async Task<AppraisalCompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalCompetencyRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"Appraisal competency with ID '{id}' not found.");
        }

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCompetencyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _appraisalCompetencyRepository.GetAllAsync();
        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalCompetencyDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _appraisalCompetencyRepository.GetQueryable()
                                                  .OrderBy(c => c.CriteriaName);
        var totalCount = await query.CountAsync(cancellationToken);

        var paged = await query.Skip((pageNumber - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(cancellationToken);

        var dtos = paged.ToDtoList();

        return new PagedResult<AppraisalCompetencyDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalCompetencyDto> CreateAsync(CreateAppraisalCompetencyDto createDto, CancellationToken cancellationToken = default)
    {
        var entity = createDto.ToEntity();

        await _appraisalCompetencyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal competency created successfully: {competencyId}", entity.Id);

        return entity.ToDto();
    }

    public async Task<AppraisalCompetencyDto> UpdateAsync(UpdateAppraisalCompetencyDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalCompetencyRepository.GetByIdAsync(updateDto.Id);

        if (entity == null)
        {
            throw new ArgumentException($"Appraisal competency with ID '{updateDto.Id}' not found.");
        }

        updateDto.UpdateEntity(entity);
        await _appraisalCompetencyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal competency updated successfully: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalCompetencyRepository.GetByIdAsync(id);

        if (entity == null)
        {
            throw new ArgumentException($"Appraisal competency with ID '{id}' not found.");
        }

        await _appraisalCompetencyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal competency deleted successfully: {id}", id);

        return true;
    }
}

#endregion Appraisal Competency

