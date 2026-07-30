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
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<KpiDefinitionService> _logger;

    public KpiDefinitionService(
        IGenericRepository<KpiDefinition> kpiDefinitionRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<KpiDefinitionService> logger)
    {
        _kpiDefinitionRepository = kpiDefinitionRepository;
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

    // A KPI definition owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<KpiDefinition> GetOwnedAsync(Guid id)
    {
        var entity = await _kpiDefinitionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"KPI definition with ID '{id}' not found.");
        return entity;
    }

    public async Task<KpiDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<KpiDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var kpiDefinitions = await _kpiDefinitionRepository.GetQueryable()
            .Where(k => k.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return kpiDefinitions.ToDtoList();
    }

    public async Task<PagedResult<KpiDefinitionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var definitionsQuery = _kpiDefinitionRepository.GetQueryable()
            .Where(k => k.TenantId == tenantId)
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
        kpiDefinition.TenantId = GetTenantId();

        await _kpiDefinitionRepository.AddAsync(kpiDefinition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("KPI definition created: {kpiDefinitionId}", kpiDefinition.Id);

        return kpiDefinition.ToDto();
    }

    public async Task<KpiDefinitionDto> UpdateAsync(UpdateKpiDefinitionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        updateDto.UpdateEntity(entity);

        await _kpiDefinitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("KPI definition updated successfully: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await _kpiDefinitionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("KPI definition deleted successfully: {id}", id);

        return true;
    }
}

#endregion KPI Definition
