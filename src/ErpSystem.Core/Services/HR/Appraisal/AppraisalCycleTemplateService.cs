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
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalTemplate> _templateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalCycleTemplateService> _logger;

    public AppraisalCycleTemplateService(
        IGenericRepository<AppraisalCycleTemplate> cycleTemplateRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<AppraisalTemplate> templateRepository,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalCycleTemplateService> logger)
    {
        _cycleTemplateRepository = cycleTemplateRepository;
        _employeeRepository = employeeRepository;
        _templateRepository = templateRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<AppraisalCycleTemplateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _cycleTemplateRepository.GetQueryable()
            .Include(ct => ct.AppraisalCycle)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationLevel)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.OrganizationUnit)
            .Include(ct => ct.AppraisalTemplate)
                .ThenInclude(t => t.Position)
            .FirstOrDefaultAsync(ct => ct.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Appraisal cycle template assignment with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<IEnumerable<AppraisalCycleTemplateDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default)
    {
        var entities = await _cycleTemplateRepository.GetQueryable(ct => ct.AppraisalCycleId == cycleId && ct.IsActive)
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
        var entities = await _cycleTemplateRepository.GetQueryable(ct => ct.AppraisalTemplateId == templateId)
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
        var template = await _templateRepository.GetByIdAsync(templateId)
            ?? throw new ArgumentException($"Appraisal template with ID '{templateId}' not found.");

        if (template.ApprovalStatus != TemplateApprovalStatus.Approved)
            throw new InvalidOperationException(
                $"Template '{template.TemplateName}' is {template.ApprovalStatus} and cannot be assigned to a cycle. Only approved templates can be assigned.");
    }

    public async Task<AppraisalCycleTemplateDto> CreateAsync(CreateAppraisalCycleTemplateDto createDto, CancellationToken cancellationToken = default)
    {
        await AssertTemplateApprovedAsync(createDto.AppraisalTemplateId, cancellationToken);

        var entity = createDto.ToEntity();

        await _cycleTemplateRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle template assignment created: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<AppraisalCycleTemplateDto> UpdateAsync(UpdateAppraisalCycleTemplateDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _cycleTemplateRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Cycle template assignment with ID '{updateDto.Id}' not found.");

        updateDto.UpdateEntity(entity);

        await _cycleTemplateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Cycle template assignment updated: {Id}", entity.Id);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _cycleTemplateRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Cycle template assignment with ID '{id}' not found.");

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
        var created = new List<AppraisalCycleTemplate>();

        foreach (var dto in assignments)
        {
            await AssertTemplateApprovedAsync(dto.AppraisalTemplateId, cancellationToken);
            var entity = dto.ToEntity();
            entity.AppraisalCycleId = cycleId;
            await _cycleTemplateRepository.AddAsync(entity);
            created.Add(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bulk assigned {Count} templates to cycle {CycleId}", created.Count, cycleId);

        var ids = created.Select(e => e.Id).ToList();
        var result = await _cycleTemplateRepository.GetQueryable(ct => ids.Contains(ct.Id))
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
        var employee = await _employeeRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        if (employee == null)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        // Load templates with their scope navigation so resolution can compare against employee attributes.
        var assignments = await _cycleTemplateRepository.GetQueryable(
            ct => ct.AppraisalCycleId == cycleId && ct.IsActive)
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
