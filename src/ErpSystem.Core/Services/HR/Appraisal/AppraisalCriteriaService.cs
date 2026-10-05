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

#region Appraisal Competency

public class AppraisalCompetencyService : IAppraisalCompetencyService
{
    private readonly IGenericRepository<AppraisalCompetency> _appraisalCompetencyRepository;
    private readonly IGenericRepository<AppraisalTemplateItem> _templateItemRepository;
    private readonly IGenericRepository<GoalRequiredSkill> _requiredSkillRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCompetencyService> _logger;

    public AppraisalCompetencyService(
        IGenericRepository<AppraisalCompetency> appraisalCompetencyRepository,
        IGenericRepository<AppraisalTemplateItem> templateItemRepository,
        IGenericRepository<GoalRequiredSkill> requiredSkillRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCompetencyService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _appraisalCompetencyRepository = appraisalCompetencyRepository;
        _templateItemRepository = templateItemRepository;
        _requiredSkillRepository = requiredSkillRepository;
        _currentUserProvider = currentUserProvider;
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

    // An appraisal competency owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalCompetency> GetOwnedAsync(Guid id)
    {
        var entity = await _appraisalCompetencyRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal competency with ID '{id}' not found.");
        return entity;
    }

    public async Task<AppraisalCompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCompetencyDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _appraisalCompetencyRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<AppraisalCompetencyDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _appraisalCompetencyRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId)
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
        entity.TenantId = GetTenantId();

        await _appraisalCompetencyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal competency created successfully: {competencyId}", entity.Id);

        return entity.ToDto();
    }

    /// <summary>
    /// What uses a competency (performance closure E-g1, D-78): template criteria (a self-evaluation scores them, and
    /// its evidence rule binds there) and the skills goals require. Null when nothing does. A criterion under a deleted
    /// template or section, or a skill on a deleted goal, is not a use.
    /// </summary>
    private async Task<string?> DescribeUseAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var criteria = await _templateItemRepository.GetQueryable()
            .CountAsync(i => i.TenantId == tenantId && i.CompetencyId == id
                          && !i.Section.IsDeleted && !i.Section.AppraisalTemplate.IsDeleted, cancellationToken);
        var goals = await _requiredSkillRepository.GetQueryable()
            .Where(s => s.TenantId == tenantId && s.CompetencyId == id && !s.EmployeeGoal.IsDeleted)
            .Select(s => s.EmployeeGoalId)
            .Distinct()
            .CountAsync(cancellationToken);

        return DefinitionUse.Describe(
            new DefinitionUse.Use(criteria, "a template criterion", "template criteria"),
            new DefinitionUse.Use(goals, "an employee goal that requires it", "employee goals that require it"));
    }

    public async Task<AppraisalCompetencyDto> UpdateAsync(UpdateAppraisalCompetencyDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        // While a competency is in use its evidence rule stays as it is (E-g1, D-78): turning it on refused
        // self-evaluations already written without evidence, turning it off waved through what it was meant to hold.
        if (updateDto.RequireEvidence != entity.RequireEvidence)
        {
            var uses = await DescribeUseAsync(entity.Id, cancellationToken);
            if (uses != null)
                throw DefinitionUse.ChangeRefused("competency", entity.CriteriaName, uses, "evidence rule");
        }

        updateDto.UpdateEntity(entity);
        await _appraisalCompetencyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal competency updated successfully: {Id}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // A competency in use is not deleted (E-g1, D-78): its criteria dropped out of the forms and its evidence
        // rule stopped applying.
        var uses = await DescribeUseAsync(id, cancellationToken);
        if (uses != null)
            throw DefinitionUse.DeleteRefused("competency", entity.CriteriaName, uses,
                "Make it inactive instead, and it is no longer offered for new criteria.");

        await _appraisalCompetencyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal competency deleted successfully: {id}", id);

        return true;
    }
}

#endregion Appraisal Competency
