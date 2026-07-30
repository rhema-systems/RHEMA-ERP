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

public class AppraisalCycleTemplateService : IAppraisalCycleTemplateService
{
    private readonly IGenericRepository<AppraisalCycleTemplate> _cycleTemplateRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalTemplate> _templateRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCycleTemplateService> _logger;

    public AppraisalCycleTemplateService(
        IGenericRepository<AppraisalCycleTemplate> cycleTemplateRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<AppraisalTemplate> templateRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCycleTemplateService> logger)
    {
        _cycleTemplateRepository = cycleTemplateRepository;
        _cycleRepository = cycleRepository;
        _employeeRepository = employeeRepository;
        _templateRepository = templateRepository;
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

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleTemplateDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();

        var entities = await _cycleTemplateRepository.GetQueryable()
            .Where(ct => ct.TenantId == tenantId && ct.AppraisalCycleId == cycleId && ct.IsActive)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .OrderByDescending(ct => ct.Priority)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
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

        return entities.ToDtoList();
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
        await GetOwnedCycleAsync(createDto.AppraisalCycleId);
        await AssertTemplateApprovedAsync(createDto.AppraisalTemplateId, cancellationToken);

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

        updateDto.UpdateEntity(entity);

        await _cycleTemplateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle template assignment updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

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
        await GetOwnedCycleAsync(cycleId);
        var tenantId = GetTenantId();
        var created = new List<AppraisalCycleTemplate>();

        foreach (var dto in assignments)
        {
            await AssertTemplateApprovedAsync(dto.AppraisalTemplateId, cancellationToken);
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
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .ToListAsync(cancellationToken);

        return result.ToDtoList();
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
            ?? assignments.FirstOrDefault(ct =>
                ct.AppraisalTemplate.OrganizationLevelId != null &&
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
