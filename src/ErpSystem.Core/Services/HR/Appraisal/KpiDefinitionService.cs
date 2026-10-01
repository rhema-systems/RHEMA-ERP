using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region KPI Definition

public class KpiDefinitionService : IKpiDefinitionService
{
    private readonly IGenericRepository<KpiDefinition> _kpiDefinitionRepository;
    private readonly IGenericRepository<AppraisalTemplateItem> _templateItemRepository;
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<KpiDefinitionService> _logger;

    public KpiDefinitionService(
        IGenericRepository<KpiDefinition> kpiDefinitionRepository,
        IGenericRepository<AppraisalTemplateItem> templateItemRepository,
        IGenericRepository<EmployeeGoal> goalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<KpiDefinitionService> logger)
    {
        _kpiDefinitionRepository = kpiDefinitionRepository;
        _templateItemRepository = templateItemRepository;
        _goalRepository = goalRepository;
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

    /// <summary>
    /// What uses a KPI (performance closure E-g1, D-78): template criteria (a KPI row is measured, not rated) and
    /// employee goals. Null when nothing does. A criterion under a deleted template or section is not a use.
    /// </summary>
    private async Task<string?> DescribeUseAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var criteria = await _templateItemRepository.GetQueryable()
            .CountAsync(i => i.TenantId == tenantId && i.KpiDefinitionId == id
                          && !i.Section.IsDeleted && !i.Section.AppraisalTemplate.IsDeleted, cancellationToken);
        var goals = await _goalRepository.GetQueryable()
            .CountAsync(g => g.TenantId == tenantId && g.KpiDefinitionId == id, cancellationToken);

        return DefinitionUse.Describe(
            new DefinitionUse.Use(criteria, "a template criterion", "template criteria"),
            new DefinitionUse.Use(goals, "an employee goal", "employee goals"));
    }

    public async Task<KpiDefinitionDto> UpdateAsync(UpdateKpiDefinitionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        // While a KPI is in use its measurement type stays as it is (E-g1, D-78): the rows and goals on it are
        // scored by it, and changing it re-scored what was already measured.
        if (updateDto.MeasurementType != entity.MeasurementType)
        {
            var uses = await DescribeUseAsync(entity.Id, cancellationToken);
            if (uses != null)
                throw DefinitionUse.ChangeRefused("KPI", entity.KpiName, uses, "measurement type");
        }

        updateDto.UpdateEntity(entity);

        await _kpiDefinitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("KPI definition updated successfully: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // A KPI in use is not deleted (E-g1, D-78): its rows and goals lost what they measure.
        var uses = await DescribeUseAsync(id, cancellationToken);
        if (uses != null)
            throw DefinitionUse.DeleteRefused("KPI", entity.KpiName, uses,
                "Make it inactive instead, and it is no longer offered for new criteria and goals.");

        await _kpiDefinitionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("KPI definition deleted successfully: {id}", id);

        return true;
    }
}

#endregion KPI Definition
