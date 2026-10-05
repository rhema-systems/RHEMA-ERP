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

public class AppraisalCycleTemplateService : IAppraisalCycleTemplateService
{
    private readonly IGenericRepository<AppraisalCycleTemplate> _cycleTemplateRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalTemplate> _templateRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCycleTemplateService> _logger;

    public AppraisalCycleTemplateService(
        IGenericRepository<AppraisalCycleTemplate> cycleTemplateRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<AppraisalTemplate> templateRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCycleTemplateService> logger)
    {
        _cycleTemplateRepository = cycleTemplateRepository;
        _cycleRepository = cycleRepository;
        _employeeRepository = employeeRepository;
        _templateRepository = templateRepository;
        _appraisalRepository = appraisalRepository;
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

    // A cycle template assignment owned by another tenant is reported as missing rather than forbidden,
    // so the endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalCycleTemplate> GetOwnedAsync(Guid id)
    {
        var entity = await _cycleTemplateRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal cycle template assignment with ID '{id}' not found.");
        return entity;
    }

    // An appraisal cycle owned by another tenant is reported as missing rather than forbidden.
    private async Task<AppraisalCycle> GetOwnedCycleAsync(Guid cycleId)
    {
        var entity = await _cycleRepository.GetByIdAsync(cycleId);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");
        return entity;
    }

    public async Task<AppraisalCycleTemplateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _cycleTemplateRepository.GetQueryable()
            .Include(ct => ct.AppraisalCycle)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .FirstOrDefaultAsync(ct => ct.Id == id && ct.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Appraisal cycle template assignment with ID '{id}' not found.");

        return (await WithUseAsync(new List<AppraisalCycleTemplateDto> { entity.ToDto() }, cancellationToken))[0];
    }

    public async Task<IEnumerable<AppraisalCycleTemplateDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        var entities = await _cycleTemplateRepository.GetQueryable()
            .Where(ct => ct.TenantId == tenantId && ct.AppraisalCycleId == cycleId && ct.IsActive)
            .Include(ct => ct.AppraisalCycle)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .OrderByDescending(ct => ct.Priority)
            .ToListAsync(cancellationToken);

        return await WithUseAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<IEnumerable<AppraisalCycleTemplateDto>> GetByTemplateIdAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _cycleTemplateRepository.GetQueryable()
            .Where(ct => ct.TenantId == tenantId && ct.AppraisalTemplateId == templateId)
            .Include(ct => ct.AppraisalCycle)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .OrderByDescending(ct => ct.AppraisalCycle.Year)
            .ToListAsync(cancellationToken);

        return await WithUseAsync(entities.ToDtoList(), cancellationToken);
    }

    /// <summary>Sets <c>TemplateInUseInCycle</c> on each link read: whether its cycle's appraisals are scored on its template.</summary>
    private async Task<List<AppraisalCycleTemplateDto>> WithUseAsync(List<AppraisalCycleTemplateDto> links, CancellationToken cancellationToken)
    {
        if (links.Count == 0)
            return links;

        var tenantId = GetTenantId();
        var cycleIds = links.Select(l => l.AppraisalCycleId).Distinct().ToList();
        var used = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && cycleIds.Contains(a.AppraisalCycleId) && a.AppraisalTemplateId != null)
            .Select(a => new { a.AppraisalCycleId, TemplateId = a.AppraisalTemplateId!.Value })
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var link in links)
            link.TemplateInUseInCycle = used.Any(u => u.AppraisalCycleId == link.AppraisalCycleId && u.TemplateId == link.AppraisalTemplateId);
        return links;
    }

    /// <summary>
    /// Refuses a change to a link (performance closure E-e, D-68): a Closed cycle's links are the year's record, and a
    /// link whose template its cycle's appraisals are scored on stays as it is — removing or switching it off released
    /// the template's lock while those appraisals still read it.
    /// </summary>
    private async Task EnsureLinkChangeableAsync(AppraisalCycleTemplate link, string action, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var cycle = await _cycleRepository.GetQueryable()
            .Where(c => c.Id == link.AppraisalCycleId && c.TenantId == tenantId)
            .Select(c => new { c.Status, c.CycleName })
            .FirstOrDefaultAsync(cancellationToken);
        if (cycle?.Status == AppraisalCycleStatus.Closed)
            throw new AppraisalConfigurationLockedException($"{action}: its cycle, {cycle.CycleName}, is closed.");

        var scored = await _appraisalRepository.GetQueryable()
            .CountAsync(a => a.TenantId == tenantId && a.AppraisalCycleId == link.AppraisalCycleId
                          && a.AppraisalTemplateId == link.AppraisalTemplateId, cancellationToken);
        if (scored > 0)
            throw new AppraisalConfigurationLockedException(
                $"{action}: {scored} appraisal{(scored == 1 ? " in its cycle is" : "s in its cycle are")} scored on this " +
                "template, so it stays on the cycle as it is. Add another template for people not yet generated.");
    }

    /// <summary>
    /// Refuses a new link to a Closed cycle, or a template the cycle already has (E-e, D-68): a duplicate at the same
    /// priority made a "conflict" that named one template twice.
    /// </summary>
    private async Task EnsureLinkCanBeAddedAsync(AppraisalCycle cycle, Guid templateId, CancellationToken cancellationToken)
    {
        if (cycle.Status == AppraisalCycleStatus.Closed)
            throw new AppraisalConfigurationLockedException(
                $"A template cannot be added: the cycle, {cycle.CycleName}, is closed.");

        var tenantId = GetTenantId();
        if (await _cycleTemplateRepository.GetQueryable().AnyAsync(l => l.TenantId == tenantId
                && l.AppraisalCycleId == cycle.Id && l.AppraisalTemplateId == templateId, cancellationToken))
            throw new InvalidOperationException("This template is already on the cycle; change its priority there instead.");
    }

    /// <summary>Throws if the template is not Approved — only approved templates may be assigned to a cycle.</summary>
    private async Task AssertTemplateApprovedAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var template = await _templateRepository.GetQueryable()
            .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException($"Appraisal template with ID '{templateId}' not found.");

        if (template.ApprovalStatus != TemplateApprovalStatus.Approved)
            throw new InvalidOperationException(
                $"Template '{template.TemplateName}' is {template.ApprovalStatus} and cannot be assigned to a cycle. Only approved templates can be assigned.");
    }

    public async Task<AppraisalCycleTemplateDto> CreateAsync(CreateAppraisalCycleTemplateDto createDto, CancellationToken cancellationToken = default)
    {
        var cycle = await GetOwnedCycleAsync(createDto.AppraisalCycleId);
        await AssertTemplateApprovedAsync(createDto.AppraisalTemplateId, cancellationToken);
        // An Open cycle takes a new template — for people added to its scope and not yet generated (D-68).
        await EnsureLinkCanBeAddedAsync(cycle, createDto.AppraisalTemplateId, cancellationToken);

        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _cycleTemplateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle template assignment created: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<AppraisalCycleTemplateDto> UpdateAsync(UpdateAppraisalCycleTemplateDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        // The link is pinned to its cycle and template (performance closure E-e, D-68): re-pointing it checked
        // neither the tenant nor the approval, and freed the old template's lock. Another template is another link.
        if ((updateDto.AppraisalCycleId != Guid.Empty && updateDto.AppraisalCycleId != entity.AppraisalCycleId)
            || (updateDto.AppraisalTemplateId != Guid.Empty && updateDto.AppraisalTemplateId != entity.AppraisalTemplateId))
            throw new InvalidOperationException(
                "A template link stays on its cycle and template; add a new link for another template, or remove this one.");
        if (updateDto.Priority != entity.Priority || updateDto.IsActive != entity.IsActive)
            await EnsureLinkChangeableAsync(entity, "This template link cannot change", cancellationToken);

        updateDto.UpdateEntity(entity);

        await _cycleTemplateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle template assignment updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        await EnsureLinkChangeableAsync(entity, "This template cannot be removed from the cycle", cancellationToken);

        await _cycleTemplateRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle template assignment deleted: {Id}", id);
        return true;
    }

    public async Task<IEnumerable<AppraisalCycleTemplateDto>> BulkAssignAsync(
        Guid cycleId,
        IEnumerable<CreateAppraisalCycleTemplateDto> assignments,
        CancellationToken cancellationToken = default)
    {
        var cycle = await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();
        var created = new List<AppraisalCycleTemplate>();

        foreach (var dto in assignments)
        {
            await AssertTemplateApprovedAsync(dto.AppraisalTemplateId, cancellationToken);
            await EnsureLinkCanBeAddedAsync(cycle, dto.AppraisalTemplateId, cancellationToken);
            if (created.Any(c => c.AppraisalTemplateId == dto.AppraisalTemplateId))
                throw new InvalidOperationException("The same template is listed twice; a cycle takes each template once.");
            var entity = dto.ToEntity();
            entity.AppraisalCycleId = cycleId;
            entity.TenantId = tenantId;
            await _cycleTemplateRepository.AddAsync(entity);
            created.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bulk assigned {Count} templates to cycle {CycleId}", created.Count, cycleId);

        var ids = created.Select(e => e.Id).ToList();
        var result = await _cycleTemplateRepository.GetQueryable()
            .Where(ct => ct.TenantId == tenantId && ids.Contains(ct.Id))
            .Include(ct => ct.AppraisalCycle)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .ToListAsync(cancellationToken);

        return await WithUseAsync(result.ToDtoList(), cancellationToken);
    }

    /// <summary>
    /// Resolves the best-matching template for an employee in a cycle.
    /// Priority order (highest → lowest): Employee-specific > Position > OrgUnit > OrgLevel > No scope (global).
    /// Ties broken by the Priority field (descending) then most-recently created.
    /// </summary>
    public async Task<AppraisalTemplateDto?> ResolveTemplateForEmployeeAsync(
        Guid cycleId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        var employee = await _employeeRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId, cancellationToken);

        if (employee == null)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        // Load templates with their scope navigation so resolution can compare against employee attributes.
        var assignments = await _cycleTemplateRepository.GetQueryable()
            .Where(ct => ct.TenantId == tenantId && ct.AppraisalCycleId == cycleId && ct.IsActive)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .OrderByDescending(ct => ct.Priority)
            .ToListAsync(cancellationToken);

        // Resolution priority (scope from AppraisalTemplate):
        //   Position-specific > OrgUnit-specific > OrgLevel-specific > global (no scope)
        var match =
            assignments.FirstOrDefault(ct =>
                ct.AppraisalTemplate.PositionId == employee.PositionId &&
                ct.AppraisalTemplate.PositionId != null)
            ?? assignments.FirstOrDefault(ct =>
                ct.AppraisalTemplate.OrganizationUnitId == employee.OrganizationUnitId &&
                ct.AppraisalTemplate.OrganizationUnitId != null &&
                ct.AppraisalTemplate.PositionId == null)
            // A level template covers that level's employees — it matched every employee (performance closure E-e).
            ?? assignments.FirstOrDefault(ct =>
                ct.AppraisalTemplate.OrganizationLevelId != null &&
                ct.AppraisalTemplate.OrganizationLevelId == employee.OrganizationLevelId &&
                ct.AppraisalTemplate.OrganizationUnitId == null &&
                ct.AppraisalTemplate.PositionId == null)
            ?? assignments.FirstOrDefault(ct =>
                ct.AppraisalTemplate.OrganizationLevelId == null &&
                ct.AppraisalTemplate.OrganizationUnitId == null &&
                ct.AppraisalTemplate.PositionId == null);

        if (match == null)
            return null;

        return match.AppraisalTemplate.ToDto();
    }
}
