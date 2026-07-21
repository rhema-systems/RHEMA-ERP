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
        var definitionsQuery = _kpiDefinitionRepository.GetQueryable()
                                                       .OrderBy(k => k.KpiName);
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

