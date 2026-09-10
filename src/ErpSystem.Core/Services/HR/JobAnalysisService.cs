using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

#region Job Description Service

public class JobDescriptionService : IJobDescriptionService
{
    private readonly IJobDescriptionRepository _jobDescriptionRepository;
    private readonly IJobResponsibilityRepository _responsibilityRepository;
    private readonly IJobQualificationRepository _qualificationRepository;
    private readonly IJobCompetencyRepository _competencyRepository;
    private readonly IJobPhysicalDemandRepository _physicalDemandRepository;
    private readonly IJobWorkingConditionRepository _workingConditionRepository;
    private readonly IJobEquipmentToolRepository _equipmentToolRepository;
    private readonly IJobReportingRelationshipRepository _reportingRelationshipRepository;
    private readonly IJobDutyItemRepository _dutyItemRepository;
    private readonly IJobPpeRequirementRepository _ppeRequirementRepository;
    private readonly IJobEquipmentTrainingRepository _equipmentTrainingRepository;
    private readonly IJobMedicalRequirementRepository _medicalRequirementRepository;
    private readonly IJobResponsibilityKpiRepository _kpiRepository;
    private readonly ISalaryGradeRepository _salaryGradeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegration;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobDescriptionService> _logger;

    public JobDescriptionService(
        IJobDescriptionRepository jobDescriptionRepository,
        IJobResponsibilityRepository responsibilityRepository,
        IJobQualificationRepository qualificationRepository,
        IJobCompetencyRepository competencyRepository,
        IJobPhysicalDemandRepository physicalDemandRepository,
        IJobWorkingConditionRepository workingConditionRepository,
        IJobEquipmentToolRepository equipmentToolRepository,
        IJobReportingRelationshipRepository reportingRelationshipRepository,
        IJobDutyItemRepository dutyItemRepository,
        IJobPpeRequirementRepository ppeRequirementRepository,
        IJobEquipmentTrainingRepository equipmentTrainingRepository,
        IJobMedicalRequirementRepository medicalRequirementRepository,
        IJobResponsibilityKpiRepository kpiRepository,
        ISalaryGradeRepository salaryGradeRepository,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegration,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        IUnitOfWork unitOfWork,
        ILogger<JobDescriptionService> logger)
    {
        _jobDescriptionRepository = jobDescriptionRepository;
        _responsibilityRepository = responsibilityRepository;
        _qualificationRepository = qualificationRepository;
        _competencyRepository = competencyRepository;
        _physicalDemandRepository = physicalDemandRepository;
        _workingConditionRepository = workingConditionRepository;
        _equipmentToolRepository = equipmentToolRepository;
        _reportingRelationshipRepository = reportingRelationshipRepository;
        _dutyItemRepository = dutyItemRepository;
        _ppeRequirementRepository = ppeRequirementRepository;
        _equipmentTrainingRepository = equipmentTrainingRepository;
        _medicalRequirementRepository = medicalRequirementRepository;
        _kpiRepository = kpiRepository;
        _salaryGradeRepository = salaryGradeRepository;
        _currentUserProvider = currentUserProvider;
        _workflowIntegration = workflowIntegration;
        _workflowAdapters = workflowAdapters;
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

    // A job description owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<JobDescription> GetOwnedJobDescriptionAsync(Guid id)
    {
        var entity = await _jobDescriptionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound($"Job description with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// The statuses in which a job description's content may still be written. An approved, in-force
    /// description is a signed document: changing it needs a new version, not an edit.
    /// </summary>
    private static readonly JobDescriptionStatus[] AuthorableStatuses =
    {
        JobDescriptionStatus.Draft,
        JobDescriptionStatus.UnderRevision,
    };

    /// <summary>
    /// Ownership plus authorability, for the twelve child collections.
    /// </summary>
    /// <remarks>
    /// ⚠ Ledger D-03. Every child write used to stop at <see cref="GetOwnedJobDescriptionAsync"/>,
    /// which checks the tenant and nothing else — so the duties, qualifications and PPE of an
    /// APPROVED description could be rewritten with no new version and no trace. The detail screen
    /// refuses it client-side (AUTHORABLE_JOB_DESCRIPTION_STATUSES) and that was the only thing
    /// stopping it; a screen is not a rule. Deletes are gated too: removing a duty from an approved
    /// description is the same act as rewriting one.
    /// </remarks>
    private async Task<JobDescription> RequireAuthorableJobDescriptionAsync(Guid id)
    {
        var entity = await GetOwnedJobDescriptionAsync(id);

        if (!AuthorableStatuses.Contains(entity.Status))
            throw JobArchitectureException.InvalidState(
                $"Job description '{entity.JobTitle}' is {entity.Status} and its content can no longer be " +
                "changed. Raise a new version to revise it.");

        return entity;
    }

    /// <summary>
    /// A qualification or competency may hang off a responsibility of its OWN job description, and
    /// no other. Null is legitimate — the row then belongs to the document as a whole.
    /// </summary>
    /// <remarks>
    /// ⚠ Nothing checked this. `JobResponsibilityId` is a plain nullable Guid on the create DTO, so
    /// any responsibility id in the tenant was accepted — including one belonging to a different
    /// job description, which silently files a requirement under another document's accountability
    /// and makes the valuation group it there. The FK is satisfied, so nothing failed.
    /// </remarks>
    private async Task RequireResponsibilityOfAsync(
        Guid? responsibilityId, Guid jobDescriptionId, CancellationToken cancellationToken)
    {
        if (responsibilityId == null) return;

        var tenantId = GetTenantId();
        var belongs = await _responsibilityRepository.GetQueryable()
            .AnyAsync(r => r.Id == responsibilityId.Value
                        && r.TenantId == tenantId
                        && !r.IsDeleted
                        && r.JobDescriptionId == jobDescriptionId, cancellationToken);

        if (!belongs)
            throw JobArchitectureException.Invalid(
                "That responsibility belongs to a different job description. A qualification or " +
                "competency can only be attached to a responsibility of its own job description.");
    }

    /// <summary>The same gate for a KPI, which reaches its job description through its responsibility.</summary>
    private async Task<JobDescription> RequireAuthorableForResponsibilityAsync(Guid responsibilityId)
    {
        var responsibility = await _responsibilityRepository.GetByIdAsync(responsibilityId);
        if (responsibility == null || responsibility.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Responsibility not found");

        return await RequireAuthorableJobDescriptionAsync(responsibility.JobDescriptionId);
    }

    /// <summary>The same gate for equipment training, which hangs off an equipment tool.</summary>
    private async Task<JobDescription> RequireAuthorableForEquipmentToolAsync(Guid equipmentToolId)
    {
        var tool = await _equipmentToolRepository.GetByIdAsync(equipmentToolId);
        if (tool == null || tool.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Equipment tool not found");

        return await RequireAuthorableJobDescriptionAsync(tool.JobDescriptionId);
    }

    public async Task<JobDescriptionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _jobDescriptionRepository.GetQueryable()
            .WithLookups()
            .FirstOrDefaultAsync(jd => jd.Id == id && jd.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw JobArchitectureException.NotFound($"Job description with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<JobDescriptionDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _jobDescriptionRepository.GetQueryable()
            // ⚠ SPLIT, not one query. Thirteen sibling collections hang off a job description, and a
            // single-query include materialises their CARTESIAN PRODUCT — the row count is the
            // collections multiplied together, not added. It crossed the 30-second command timeout
            // as fixtures accumulated, and by 2026-09-01 this endpoint returned 500 for every one of
            // the tenant's 46 documents. Nothing had changed in the code; the data had grown into it,
            // which is why it passed its own suite when the area closed.
            .AsSplitQuery()
            .WithLookups()
            .Include(jd => jd.DutyItems)
            .Include(jd => jd.Responsibilities).ThenInclude(r => r.Qualifications).ThenInclude(q => q.Qualification)
            .Include(jd => jd.Responsibilities).ThenInclude(r => r.Competencies)
            .Include(jd => jd.Responsibilities).ThenInclude(r => r.Kpis)
            .Include(jd => jd.PhysicalDemands)
            .Include(jd => jd.JobWorkingConditions)
            .Include(jd => jd.PpeRequirements).ThenInclude(p => p.PpeType)
            .Include(jd => jd.EquipmentTools).ThenInclude(e => e.TrainingRequirements).ThenInclude(t => t.TrainingProgram)
            .Include(jd => jd.ReportingRelationships).ThenInclude(r => r.RelatedPosition)
            .Include(jd => jd.MedicalRequirements)
            .FirstOrDefaultAsync(jd => jd.Id == id && jd.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw JobArchitectureException.NotFound($"Job description with ID '{id}' not found.");

        var qualifications = (await _qualificationRepository.GetByJobDescriptionIdAsync(id))
            .Where(q => q.TenantId == tenantId);
        var competencies = (await _competencyRepository.GetByJobDescriptionIdAsync(id))
            .Where(c => c.TenantId == tenantId);

        return entity.ToDetailDto(
            qualifications,
            competencies,
            entity.PhysicalDemands,
            entity.JobWorkingConditions,
            entity.EquipmentTools,
            entity.ReportingRelationships,
            entity.DutyItems.OrderBy(d => d.SequenceNumber),
            entity.PpeRequirements,
            entity.MedicalRequirements);
    }

    public async Task<IEnumerable<JobDescriptionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _jobDescriptionRepository.GetQueryable()
            .Where(jd => jd.TenantId == tenantId)
            .WithLookups()
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<JobDescriptionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _jobDescriptionRepository.GetQueryable()
            .Where(jd => jd.TenantId == tenantId)
            .WithLookups();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(jd => jd.EffectiveDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<JobDescriptionDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<JobDescriptionSummaryDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _jobDescriptionRepository.GetByPositionIdAsync(positionId);
        return entities.Where(j => j.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobDescriptionSummaryDto>> GetByStatusAsync(JobDescriptionStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _jobDescriptionRepository.GetByStatusAsync(status);
        return entities.Where(j => j.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<JobDescriptionDto?> GetCurrentVersionForPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _jobDescriptionRepository.GetCurrentVersionForPositionAsync(positionId);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<IEnumerable<JobDescriptionSummaryDto>> GetDueForReviewAsync(int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _jobDescriptionRepository.GetDueForReviewAsync(daysAhead);
        return entities.Where(j => j.TenantId == tenantId).ToSummaryDtoList();
    }

    public async Task<IEnumerable<JobDescriptionSummaryDto>> GetVersionHistoryAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _jobDescriptionRepository.GetVersionHistoryAsync(positionId);
        return entities.Where(j => j.TenantId == tenantId).ToSummaryDtoList();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UncoveredPositionDto>> GetUncoveredPositionsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        var covered = await _jobDescriptionRepository.GetQueryable()
            .Where(jd => jd.TenantId == tenantId
                      && (jd.Status == JobDescriptionStatus.Approved || jd.Status == JobDescriptionStatus.Active))
            .Select(jd => jd.PositionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // A position with a draft sitting on it is a different conversation from one nobody has
        // started: the first needs an approval, the second needs an author.
        var drafted = await _jobDescriptionRepository.GetQueryable()
            .Where(jd => jd.TenantId == tenantId
                      && (jd.Status == JobDescriptionStatus.Draft
                       || jd.Status == JobDescriptionStatus.PendingReview
                       || jd.Status == JobDescriptionStatus.UnderRevision))
            .Select(jd => jd.PositionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var positions = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .Where(pos => pos.TenantId == tenantId && !covered.Contains(pos.Id))
            .Include(pos => pos.OrganizationUnit)
            .ToListAsync(cancellationToken);

        var positionIds = positions.Select(pos => pos.Id).ToList();
        var occupancy = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && positionIds.Contains(e.PositionId) && e.IsActive)
            .GroupBy(e => e.PositionId)
            .Select(g => new { PositionId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return positions
            .Select(pos => new UncoveredPositionDto
            {
                PositionId = pos.Id,
                PositionTitle = pos.Title,
                OrganizationUnitName = pos.OrganizationUnit?.Name,
                CurrentlyFilled = occupancy.FirstOrDefault(o => o.PositionId == pos.Id)?.Count ?? 0,
                HasUnapprovedDraft = drafted.Contains(pos.Id),
            })
            // Most people doing an undescribed job first: that is where the risk is.
            .OrderByDescending(p => p.CurrentlyFilled)
            .ThenBy(p => p.PositionTitle)
            .ToList();
    }

    public async Task<JobAnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var jds = await _jobDescriptionRepository.GetQueryable()
            .Where(jd => jd.TenantId == tenantId)
            .Include(jd => jd.JobFamily)
            .ToListAsync(cancellationToken);

        var today = DateTime.Today;
        var horizon = today.AddDays(30);

        var analytics = new JobAnalyticsDto
        {
            TotalJobDescriptions = jds.Count,
            DraftCount = jds.Count(j => j.Status == JobDescriptionStatus.Draft),
            PendingReviewCount = jds.Count(j => j.Status == JobDescriptionStatus.PendingReview),
            ApprovedCount = jds.Count(j => j.Status == JobDescriptionStatus.Approved),
            ActiveCount = jds.Count(j => j.Status == JobDescriptionStatus.Active),
            DueForReviewCount = jds.Count(j => j.Status == JobDescriptionStatus.Approved && j.NextReviewDate != null && j.NextReviewDate <= horizon),
            PositionsCovered = jds.Where(j => j.Status == JobDescriptionStatus.Approved || j.Status == JobDescriptionStatus.Active).Select(j => j.PositionId).Distinct().Count(),
            ValuedRoleCount = jds.Count(j => j.EstimatedSalaryLow != null),
            MissionCriticalRoleCount = jds.Count(j => j.RoleCriticality == RoleCriticalityLevel.MissionCritical),
        };

        // ⚠ Coverage needs its denominator. "1 position covered" says nothing without knowing
        // whether that is 1 of 2 or 1 of 146, and FR-HR-134 is a question about positions, not
        // about documents.
        var livePositions = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .Where(pos => pos.TenantId == tenantId)
            .Select(pos => new { pos.Id, pos.ExpectedHeadcount, pos.EstablishmentApprovedOn })
            .ToListAsync(cancellationToken);

        analytics.TotalPositions = livePositions.Count;
        analytics.PositionsUncovered = analytics.TotalPositions - analytics.PositionsCovered;
        analytics.PositionsEstablished = livePositions.Count(pos => pos.EstablishmentApprovedOn != null);

        var establishedIds = livePositions
            .Where(pos => pos.EstablishmentApprovedOn != null)
            .Select(pos => pos.Id)
            .ToList();
        if (establishedIds.Count > 0)
        {
            var occupancy = await _unitOfWork.Repository<Employee>().GetQueryable()
                .Where(e => e.TenantId == tenantId && establishedIds.Contains(e.PositionId) && e.IsActive)
                .GroupBy(e => e.PositionId)
                .Select(g => new { PositionId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            analytics.PositionsOverStrength = livePositions
                .Where(pos => pos.EstablishmentApprovedOn != null)
                .Count(pos => (occupancy.FirstOrDefault(o => o.PositionId == pos.Id)?.Count ?? 0)
                              > pos.ExpectedHeadcount);
        }

        var valued = jds.Where(j => j.EstimatedSalaryLow != null && j.EstimatedSalaryHigh != null)
            .Select(j => (j.EstimatedSalaryLow!.Value + j.EstimatedSalaryHigh!.Value) / 2m).ToList();
        analytics.AverageEstimatedSalary = valued.Count > 0 ? Math.Round(valued.Average(), 2) : null;

        analytics.StatusBreakdown = jds.GroupBy(j => j.Status)
            .Select(g => new NameCountDto { Name = g.Key.ToString(), Count = g.Count() })
            .OrderByDescending(x => x.Count).ToList();

        analytics.FamilyBreakdown = jds.Where(j => j.JobFamily != null)
            .GroupBy(j => j.JobFamily!.Name)
            .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ToList();

        analytics.TopCompetencies = await _competencyRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId)
            .GroupBy(c => c.CompetencyName)
            .Select(g => new NameCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        return analytics;
    }

    /// <summary>
    /// Refuses a classification that does not hang together: a sub-family that belongs to a
    /// different family, or either naming a row that is not there.
    /// </summary>
    /// <remarks>
    /// The two ids arrive independently on the DTO and nothing related them, so a job description
    /// could be filed under family "Finance" and sub-family "Architecture" at the same time. Neither
    /// value is wrong on its own, which is why no foreign key catches it and why a screen with two
    /// dropdowns will produce it the first time someone changes the family and not the sub-family.
    /// </remarks>
    private async Task RequireCoherentClassificationAsync(
        Guid? jobFamilyId, Guid? jobSubFamilyId, Guid? jobLevelId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        if (jobFamilyId.HasValue && !await _unitOfWork.Repository<JobFamily>().GetQueryable()
                .AnyAsync(f => f.Id == jobFamilyId && f.TenantId == tenantId, cancellationToken))
            throw JobArchitectureException.NotFound("Job family not found");

        if (jobLevelId.HasValue && !await _unitOfWork.Repository<CareerLevel>().GetQueryable()
                .AnyAsync(l => l.Id == jobLevelId && l.TenantId == tenantId, cancellationToken))
            throw JobArchitectureException.NotFound("Job level not found");

        if (!jobSubFamilyId.HasValue) return;

        var subFamily = await _unitOfWork.Repository<JobSubFamily>().GetQueryable()
            .FirstOrDefaultAsync(sf => sf.Id == jobSubFamilyId && sf.TenantId == tenantId, cancellationToken)
            ?? throw JobArchitectureException.NotFound("Sub-family not found");

        if (!jobFamilyId.HasValue)
            throw JobArchitectureException.Invalid(
                "A sub-family cannot be set without the job family it belongs to.");

        if (subFamily.JobFamilyId != jobFamilyId.Value)
            throw JobArchitectureException.Invalid(
                "The sub-family belongs to a different job family. Choose one from the selected family.");
    }

    public async Task<JobDescriptionDto> CreateAsync(CreateJobDescriptionDto createDto, Guid preparedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        await RequireCoherentClassificationAsync(
            createDto.JobFamilyId, createDto.JobSubFamilyId, createDto.JobLevelId, cancellationToken);

        entity.JobDescriptionNumber = await GenerateJobDescriptionNumberAsync(cancellationToken);
        entity.VersionNumber = await _jobDescriptionRepository.GetNextVersionNumberAsync(createDto.PositionId);
        entity.PreparedById = preparedById;
        entity.PreparedDate = DateTime.UtcNow;
        entity.Status = JobDescriptionStatus.Draft;

        await _jobDescriptionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Add responsibilities if provided
        if (createDto.Responsibilities?.Any() == true)
        {
            foreach (var respDto in createDto.Responsibilities)
            {
                respDto.JobDescriptionId = entity.Id;
                var responsibility = respDto.ToEntity();
                responsibility.TenantId = tenantId;
                await _responsibilityRepository.AddAsync(responsibility);

                // Add qualifications if provided
                if (respDto.Qualifications?.Any() == true)
                {
                    foreach (var qualDto in respDto.Qualifications)
                    {
                        qualDto.JobDescriptionId = entity.Id;
                        qualDto.JobResponsibilityId = responsibility.Id;
                        var qualification = qualDto.ToEntity();
                        qualification.TenantId = tenantId;
                        await _qualificationRepository.AddAsync(qualification);
                    }
                }

                // Add competencies if provided
                if (respDto.Competencies?.Any() == true)
                {
                    foreach (var compDto in respDto.Competencies)
                    {
                        compDto.JobDescriptionId = entity.Id;
                        compDto.JobResponsibilityId = responsibility.Id;
                        var competency = compDto.ToEntity();
                        competency.TenantId = tenantId;
                        await _competencyRepository.AddAsync(competency);
                    }
                }
            }
        }

        // Add physical demands if provided
        if (createDto.PhysicalDemands?.Any() == true)
        {
            foreach (var demandDto in createDto.PhysicalDemands)
            {
                demandDto.JobDescriptionId = entity.Id;
                var demand = demandDto.ToEntity();
                demand.TenantId = tenantId;
                await _physicalDemandRepository.AddAsync(demand);
            }
        }

        // Add working conditions if provided
        if (createDto.WorkingConditions?.Any() == true)
        {
            foreach (var condDto in createDto.WorkingConditions)
            {
                condDto.JobDescriptionId = entity.Id;
                var condition = condDto.ToEntity();
                condition.TenantId = tenantId;
                await _workingConditionRepository.AddAsync(condition);
            }
        }

        // Add equipment tools if provided
        if (createDto.EquipmentTools?.Any() == true)
        {
            foreach (var toolDto in createDto.EquipmentTools)
            {
                toolDto.JobDescriptionId = entity.Id;
                var tool = toolDto.ToEntity();
                tool.TenantId = tenantId;
                await _equipmentToolRepository.AddAsync(tool);
            }
        }

        // Add reporting relationships if provided
        if (createDto.ReportingRelationships?.Any() == true)
        {
            foreach (var relDto in createDto.ReportingRelationships)
            {
                relDto.JobDescriptionId = entity.Id;
                var relationship = relDto.ToEntity();
                relationship.TenantId = tenantId;
                await _reportingRelationshipRepository.AddAsync(relationship);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job description created: {JobDescriptionNumber}", entity.JobDescriptionNumber);

        // Re-read through the shared chain: the entity above was built from the DTO, so its
        // navigations are unloaded and every resolved name on the response would be blank while
        // the same row read a moment later comes back complete.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<JobDescriptionDto> UpdateAsync(UpdateJobDescriptionDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _jobDescriptionRepository.GetQueryable()
            .Include(jd => jd.Position)
            .FirstOrDefaultAsync(jd => jd.Id == updateDto.Id && jd.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw JobArchitectureException.NotFound($"Job description with ID '{updateDto.Id}' not found.");

        if (entity.Status == JobDescriptionStatus.Approved)
            throw JobArchitectureException.InvalidState("Cannot update an approved job description. Create a new version instead.");

        // ⚠ An edit does not move the status, and a caller who thinks it does is told so rather
        // than left to believe it worked. Sending the record's current status is how a client
        // round-trips the whole document (the update is a replace), so that is accepted; anything
        // else is an attempted transition through the wrong door.
        if (updateDto.Status.HasValue && updateDto.Status.Value != entity.Status)
            throw JobArchitectureException.Invalid(
                $"A job description's status is not changed by editing it. This one is {entity.Status}; " +
                "use submit, review or approve to move it.");

        await RequireCoherentClassificationAsync(
            updateDto.JobFamilyId, updateDto.JobSubFamilyId, updateDto.JobLevelId, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _jobDescriptionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job description updated: {JobDescriptionNumber}", entity.JobDescriptionNumber);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> SubmitForReviewAsync(SubmitJobDescriptionForReviewDto submitDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedJobDescriptionAsync(submitDto.JobDescriptionId);

        if (entity.Status != JobDescriptionStatus.Draft && entity.Status != JobDescriptionStatus.UnderRevision)
            throw JobArchitectureException.InvalidState("Only draft job descriptions can be submitted for review.");

        // On the engine when the tenant has published a definition; the direct path otherwise. Asked
        // rather than assumed so one build serves a tenant that has configured its approval chain
        // and one that has not — the same conditional the probation confirmation uses.
        if (await IsApprovalWorkflowConfiguredAsync(cancellationToken))
        {
            var submitResult = await _workflowIntegration.SubmitAsync(WorkflowEntityType, entity.Id);
            if (!submitResult.ExecutionResult.Success)
                throw JobArchitectureException.InvalidState(
                    submitResult.ExecutionResult.Message ?? "Failed to start the job description approval workflow.");

            _workflowAdapters.GetAdapter(WorkflowEntityType)
                .ApplySubmitOutcome(entity, submitResult.Outcome, _currentUserProvider.UserId);
        }
        else
        {
            entity.Status = JobDescriptionStatus.PendingReview;
        }

        await _jobDescriptionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job description submitted for review: {JobDescriptionNumber}", entity.JobDescriptionNumber);

        return true;
    }

    // ── Approval on the workflow engine (slice 3) ─────────────────────────────

    private const string WorkflowEntityType = "JobDescription";

    /// <summary>
    /// Whether this tenant has published a job-description approval workflow.
    /// </summary>
    /// <remarks>
    /// Treated as "not configured" if the engine cannot answer: refusing to approve a job
    /// description because a workflow lookup failed would be worse than allowing the direct path.
    /// </remarks>
    private async Task<bool> IsApprovalWorkflowConfiguredAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _unitOfWork.Repository<Entities.Workflow.WorkflowDefinition>().GetQueryable()
                .AnyAsync(d => d.TenantId == GetTenantId()
                            && !d.IsDeleted
                            && d.IsActive
                            && d.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published
                            && d.EntityType != null
                            && d.EntityType.Code == "JOB_DESCRIPTION",
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not determine whether a job description approval workflow is published; allowing the direct path.");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ApproveViaWorkflowAsync(Guid jobDescriptionId, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var userId = _currentUserProvider.UserId;

        if (!await _workflowIntegration.CanUserApproveAsync(WorkflowEntityType, jobDescriptionId, userId))
            throw new UnauthorizedAccessException(
                "You are not assigned as an approver for the current workflow step.");

        var result = await _workflowIntegration.ProcessApprovalAsync(WorkflowEntityType, jobDescriptionId, userId, "Approve");
        if (!result.ExecutionResult.Success)
            throw JobArchitectureException.InvalidState(
                result.ExecutionResult.Message ?? "Failed to process the job description approval.");

        _workflowAdapters.GetAdapter(WorkflowEntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, userId);

        // ⚠ The consequences, which the adapter cannot apply because it sees only this one entity.
        // Superseding matters beyond tidiness: OfferLetterService selects a position's job
        // description by SupersededByVersionId == null, so two unsuperseded approved versions make
        // an offer letter ambiguous. Only run when the engine actually approved — an intermediate
        // step returns Pending, and a mid-chain approval must not retire anything.
        if (result.Outcome == WorkflowOutcome.Approved)
            await ApplyApprovalConsequencesAsync(entity, approvedById, cancellationToken);

        await _jobDescriptionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job description {Number} approval step processed: {Outcome}",
            entity.JobDescriptionNumber, result.Outcome);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RejectViaWorkflowAsync(Guid jobDescriptionId, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var userId = _currentUserProvider.UserId;

        if (!await _workflowIntegration.CanUserApproveAsync(WorkflowEntityType, jobDescriptionId, userId))
            throw new UnauthorizedAccessException(
                "You are not assigned as an approver for the current workflow step.");

        var rejectionText = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();
        var result = await _workflowIntegration.ProcessApprovalAsync(
            WorkflowEntityType, jobDescriptionId, userId, "Reject", rejectionText);
        if (!result.ExecutionResult.Success)
            throw JobArchitectureException.InvalidState(
                result.ExecutionResult.Message ?? "Failed to process the job description rejection.");

        _workflowAdapters.GetAdapter(WorkflowEntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, userId, rejectionText);

        await _jobDescriptionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job description {Number} rejected on the workflow: {Reason}",
            entity.JobDescriptionNumber, rejectionText);

        return true;
    }

    public async Task<bool> ReviewAsync(ReviewJobDescriptionDto reviewDto, Guid reviewedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedJobDescriptionAsync(reviewDto.JobDescriptionId);

        if (entity.Status != JobDescriptionStatus.PendingReview)
            throw JobArchitectureException.InvalidState("Only job descriptions pending review can be reviewed.");

        entity.ReviewedById = reviewedById;
        entity.ReviewedDate = DateTime.UtcNow;
        entity.Status = reviewDto.IsApproved ? JobDescriptionStatus.PendingReview : JobDescriptionStatus.Draft;

        await _jobDescriptionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job description reviewed: {JobDescriptionNumber}, Approved: {IsApproved}", entity.JobDescriptionNumber, reviewDto.IsApproved);

        return true;
    }

    public async Task<bool> ApproveAsync(ApproveJobDescriptionDto approveDto, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await GetOwnedJobDescriptionAsync(approveDto.JobDescriptionId);

        if (entity.Status != JobDescriptionStatus.PendingReview)
            throw JobArchitectureException.InvalidState("Only job descriptions pending review can be approved.");

        // ⚠ Once a tenant publishes an approval workflow, the direct route closes. Leaving both open
        // would mean an Admin permission could quietly bypass the chain the tenant configured, which
        // defeats the point of configuring it — the same gate the probation confirmation applies.
        if (await IsApprovalWorkflowConfiguredAsync(cancellationToken))
            throw JobArchitectureException.InvalidState(
                "This tenant approves job descriptions through the workflow engine. "
                + "Approve it from the workflow queue instead.");

        entity.Status = JobDescriptionStatus.Approved;
        entity.ApprovalDate = DateTime.UtcNow;
        await ApplyApprovalConsequencesAsync(entity, approvedById, cancellationToken);

        await _jobDescriptionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job description approved: {JobDescriptionNumber}", entity.JobDescriptionNumber);

        return true;
    }

    /// <summary>
    /// What approving a job description means beyond its own status: it becomes the version in
    /// force for its position, which retires the one it replaces.
    /// </summary>
    /// <remarks>
    /// Extracted in slice 3 so the workflow route and the direct route apply identical
    /// consequences. An <c>IWorkflowStatusAdapter</c> is synchronous and sees only the entity it is
    /// handed, so it cannot supersede siblings — and superseding is not cosmetic here:
    /// <c>OfferLetterService</c> selects a position's job description by
    /// <c>SupersededByVersionId == null</c>, so two unsuperseded approved versions make an offer
    /// letter ambiguous rather than merely untidy.
    /// </remarks>
    private async Task ApplyApprovalConsequencesAsync(
        JobDescription entity, Guid approvedById, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        entity.ApprovedById = approvedById;
        entity.NextReviewDate = DateTime.Today.AddMonths(entity.ReviewCycleMonths);

        var existingApproved = await _jobDescriptionRepository.GetQueryable()
            .Where(jd => jd.TenantId == tenantId &&
                        jd.PositionId == entity.PositionId &&
                        jd.Id != entity.Id &&
                        jd.Status == JobDescriptionStatus.Approved)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingApproved)
        {
            existing.Status = JobDescriptionStatus.Superseded;
            existing.SupersededByVersionId = entity.Id;
            existing.ExpiryDate = DateTime.Today;
            await _jobDescriptionRepository.UpdateAsync(existing);
        }
    }

    public async Task<JobDescriptionDto> CreateNewVersionAsync(CreateJobDescriptionVersionDto versionDto, Guid preparedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var original = await _jobDescriptionRepository.GetQueryable()
            .Include(jd => jd.Responsibilities).ThenInclude(r => r.Qualifications)
            .Include(jd => jd.Responsibilities).ThenInclude(r => r.Competencies)
            .Include(jd => jd.PhysicalDemands)
            .Include(jd => jd.JobWorkingConditions)
            .Include(jd => jd.EquipmentTools)
            .Include(jd => jd.ReportingRelationships)
            .FirstOrDefaultAsync(jd => jd.Id == versionDto.OriginalJobDescriptionId && jd.TenantId == tenantId, cancellationToken);

        if (original == null)
            throw JobArchitectureException.NotFound($"Original job description with ID '{versionDto.OriginalJobDescriptionId}' not found.");

        var newVersion = new JobDescription
        {
            TenantId = tenantId,
            JobDescriptionNumber = await GenerateJobDescriptionNumberAsync(cancellationToken),
            PositionId = original.PositionId,
            JobTitle = original.JobTitle,
            JobSummary = original.JobSummary,
            VersionNumber = await _jobDescriptionRepository.GetNextVersionNumberAsync(original.PositionId),
            EffectiveDate = DateTime.Today,
            RevisionReason = versionDto.RevisionReason,
            ReviewCycleMonths = original.ReviewCycleMonths,
            PreparedById = preparedById,
            PreparedDate = DateTime.UtcNow,
            Status = JobDescriptionStatus.Draft,
            // ⚠ These were absent, so revising a job description silently emptied its
            // classification, valuation and authority — while CloneAsync, the *less* important
            // path, copied all of them. Versioning is the annual-review route: it is the one that
            // must not lose the record.
            RoleIntrinsicValue = original.RoleIntrinsicValue,
            RoleCriticality = original.RoleCriticality,
            IndustryBenchmarkSalary = original.IndustryBenchmarkSalary,
            ValuationNotes = original.ValuationNotes,
            AutonomyLevel = original.AutonomyLevel,
            DecisionMakingScope = original.DecisionMakingScope,
            FinancialAuthorityLimit = original.FinancialAuthorityLimit,
            ApprovalAuthorityNotes = original.ApprovalAuthorityNotes,
            StaffLevelId = original.StaffLevelId,
            SuggestedSalaryGradeId = original.SuggestedSalaryGradeId,
            IntendedEmploymentType = original.IntendedEmploymentType,
            IsBargainingUnitRole = original.IsBargainingUnitRole,
            UnionId = original.UnionId,
            OccupationCode = original.OccupationCode,
            EssentialFunctionsSummary = original.EssentialFunctionsSummary,
            JobFamilyId = original.JobFamilyId,
            JobSubFamilyId = original.JobSubFamilyId,
            JobLevelId = original.JobLevelId
        };

        await _jobDescriptionRepository.AddAsync(newVersion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Copy responsibilities, qualifications, and competencies
        foreach (var responsibility in original.Responsibilities)
        {
            var newResp = new JobResponsibility
            {
                TenantId = tenantId,
                JobDescriptionId = newVersion.Id,
                ResponsibilityDescription = responsibility.ResponsibilityDescription,
                Type = responsibility.Type,
                PercentageOfTime = responsibility.PercentageOfTime,
                ImportanceWeight = responsibility.ImportanceWeight
            };
            await _responsibilityRepository.AddAsync(newResp);

            foreach (var qual in responsibility.Qualifications)
            {
                var newQual = new JobQualification
                {
                    TenantId = tenantId,
                    JobDescriptionId = newVersion.Id,
                    JobResponsibilityId = newResp.Id,
                    Type = qual.Type,
                    QualificationId = qual.QualificationId,
                    Title = qual.Title,
                    Description = qual.Description,
                    IsRequired = qual.IsRequired,
                    JobSpecificRequirements = qual.JobSpecificRequirements
                };
                await _qualificationRepository.AddAsync(newQual);
            }

            foreach (var comp in responsibility.Competencies)
            {
                var newComp = new JobCompetency
                {
                    TenantId = tenantId,
                    JobDescriptionId = newVersion.Id,
                    JobResponsibilityId = newResp.Id,
                    CompetencyName = comp.CompetencyName,
                    Description = comp.Description,
                    Type = comp.Type,
                    RequiredLevel = comp.RequiredLevel,
                    IsCritical = comp.IsCritical
                };
                await _competencyRepository.AddAsync(newComp);
            }
        }

        // Copy physical demands
        foreach (var demand in original.PhysicalDemands)
        {
            await _physicalDemandRepository.AddAsync(new JobPhysicalDemand
            {
                TenantId = tenantId,
                JobDescriptionId = newVersion.Id,
                DemandType = demand.DemandType,
                DemandDescription = demand.DemandDescription,
                Frequency = demand.Frequency,
                WeightOrForceKg = demand.WeightOrForceKg,
                DistanceOrDuration = demand.DistanceOrDuration,
                IsEssential = demand.IsEssential,
                NotesOrExamples = demand.NotesOrExamples
            });
        }

        // Copy working conditions
        foreach (var condition in original.JobWorkingConditions)
        {
            await _workingConditionRepository.AddAsync(new JobWorkingCondition
            {
                TenantId = tenantId,
                JobDescriptionId = newVersion.Id,
                EnvironmentType = condition.EnvironmentType,
                Description = condition.Description,
                ExposureLevel = condition.ExposureLevel,
                RequiresPPE = condition.RequiresPPE,
                PPERequirements = condition.PPERequirements,
                TravelPercentage = condition.TravelPercentage,
                TravelRequirements = condition.TravelRequirements
            });
        }

        // Copy equipment tools
        foreach (var tool in original.EquipmentTools)
        {
            await _equipmentToolRepository.AddAsync(new JobEquipmentTool
            {
                TenantId = tenantId,
                JobDescriptionId = newVersion.Id,
                ItemName = tool.ItemName,
                Type = tool.Type,
                DescriptionOrSpecification = tool.DescriptionOrSpecification,
                RequiredProficiency = tool.RequiredProficiency,
                IsEssential = tool.IsEssential,
                TrainingRequired = tool.TrainingRequired
            });
        }

        // Copy reporting relationships
        foreach (var rel in original.ReportingRelationships)
        {
            await _reportingRelationshipRepository.AddAsync(new JobReportingRelationship
            {
                TenantId = tenantId,
                JobDescriptionId = newVersion.Id,
                RelationshipType = rel.RelationshipType,
                TitleOrRole = rel.TitleOrRole,
                EmployeeOrPositionId = rel.EmployeeOrPositionId,
                Description = rel.Description,
                NumberOfDirectReports = rel.NumberOfDirectReports,
                IsPrimarySupervisor = rel.IsPrimarySupervisor
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New job description version created: {JobDescriptionNumber} from {OriginalNumber}",
            newVersion.JobDescriptionNumber, original.JobDescriptionNumber);

        return newVersion.ToDto();
    }

    public async Task<JobDescriptionDto> CloneAsync(Guid id, Guid? preparedById, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var src = await _jobDescriptionRepository.GetQueryable()
            .Include(jd => jd.DutyItems)
            .Include(jd => jd.Responsibilities).ThenInclude(r => r.Kpis)
            .Include(jd => jd.PhysicalDemands)
            .Include(jd => jd.JobWorkingConditions)
            .Include(jd => jd.PpeRequirements)
            .Include(jd => jd.EquipmentTools).ThenInclude(e => e.TrainingRequirements)
            .Include(jd => jd.ReportingRelationships)
            .Include(jd => jd.MedicalRequirements)
            .FirstOrDefaultAsync(jd => jd.Id == id && jd.TenantId == tenantId, cancellationToken);
        if (src == null)
            throw JobArchitectureException.NotFound($"Job description with ID '{id}' not found.");

        var qualifications = (await _qualificationRepository.GetByJobDescriptionIdAsync(id))
            .Where(q => q.TenantId == tenantId).ToList();
        var competencies = (await _competencyRepository.GetByJobDescriptionIdAsync(id))
            .Where(c => c.TenantId == tenantId).ToList();

        var clone = new JobDescription
        {
            TenantId = tenantId,
            JobDescriptionNumber = await GenerateJobDescriptionNumberAsync(cancellationToken),
            PositionId = src.PositionId,
            JobTitle = src.JobTitle + " (Copy)",
            JobSummary = src.JobSummary,
            VersionNumber = await _jobDescriptionRepository.GetNextVersionNumberAsync(src.PositionId),
            EffectiveDate = DateTime.Today,
            ReviewCycleMonths = src.ReviewCycleMonths,
            Status = JobDescriptionStatus.Draft,
            PreparedById = preparedById,
            PreparedDate = DateTime.UtcNow,
            // Valuation
            RoleIntrinsicValue = src.RoleIntrinsicValue,
            RoleCriticality = src.RoleCriticality,
            IndustryBenchmarkSalary = src.IndustryBenchmarkSalary,
            ValuationNotes = src.ValuationNotes,
            // Authority + classification
            AutonomyLevel = src.AutonomyLevel,
            DecisionMakingScope = src.DecisionMakingScope,
            FinancialAuthorityLimit = src.FinancialAuthorityLimit,
            ApprovalAuthorityNotes = src.ApprovalAuthorityNotes,
            StaffLevelId = src.StaffLevelId,
            IntendedEmploymentType = src.IntendedEmploymentType,
            IsBargainingUnitRole = src.IsBargainingUnitRole,
            UnionId = src.UnionId,
            OccupationCode = src.OccupationCode,
            EssentialFunctionsSummary = src.EssentialFunctionsSummary,
            // Architecture
            JobFamilyId = src.JobFamilyId,
            JobSubFamilyId = src.JobSubFamilyId,
            JobLevelId = src.JobLevelId
        };
        await _jobDescriptionRepository.AddAsync(clone);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var d in src.DutyItems)
            await _dutyItemRepository.AddAsync(new JobDutyItem { TenantId = tenantId, JobDescriptionId = clone.Id, SequenceNumber = d.SequenceNumber, DutyStatement = d.DutyStatement, Notes = d.Notes });

        // Responsibilities + KPIs, tracking old->new ids for qualification/competency linkage
        var respMap = new Dictionary<Guid, Guid>();
        foreach (var r in src.Responsibilities)
        {
            var nr = new JobResponsibility { TenantId = tenantId, JobDescriptionId = clone.Id, ResponsibilityDescription = r.ResponsibilityDescription, Type = r.Type, PercentageOfTime = r.PercentageOfTime, ImportanceWeight = r.ImportanceWeight };
            await _responsibilityRepository.AddAsync(nr);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            respMap[r.Id] = nr.Id;
            foreach (var k in r.Kpis)
                await _kpiRepository.AddAsync(new JobResponsibilityKpi { TenantId = tenantId, JobResponsibilityId = nr.Id, KpiStatement = k.KpiStatement, TargetOrStandard = k.TargetOrStandard, UnitOfMeasure = k.UnitOfMeasure, Weight = k.Weight, SequenceNumber = k.SequenceNumber });
        }

        foreach (var q in qualifications)
            await _qualificationRepository.AddAsync(new JobQualification { TenantId = tenantId, JobDescriptionId = clone.Id, JobResponsibilityId = q.JobResponsibilityId.HasValue && respMap.TryGetValue(q.JobResponsibilityId.Value, out var rid) ? rid : null, Type = q.Type, QualificationId = q.QualificationId, Title = q.Title, Description = q.Description, IsRequired = q.IsRequired, JobSpecificRequirements = q.JobSpecificRequirements, MonetaryValue = q.MonetaryValue });

        foreach (var c in competencies)
            await _competencyRepository.AddAsync(new JobCompetency { TenantId = tenantId, JobDescriptionId = clone.Id, JobResponsibilityId = c.JobResponsibilityId.HasValue && respMap.TryGetValue(c.JobResponsibilityId.Value, out var rid) ? rid : null, SkillId = c.SkillId, CompetencyId = c.CompetencyId, CompetencyName = c.CompetencyName, Description = c.Description, Type = c.Type, RequiredLevel = c.RequiredLevel, IsCritical = c.IsCritical, MonetaryValue = c.MonetaryValue });

        foreach (var p in src.PhysicalDemands)
            await _physicalDemandRepository.AddAsync(new JobPhysicalDemand { TenantId = tenantId, JobDescriptionId = clone.Id, DemandType = p.DemandType, DemandDescription = p.DemandDescription, Frequency = p.Frequency, WeightOrForceKg = p.WeightOrForceKg, DistanceOrDuration = p.DistanceOrDuration, IsEssential = p.IsEssential, NotesOrExamples = p.NotesOrExamples, IsPhysicalAttribute = p.IsPhysicalAttribute, AttributeRequirement = p.AttributeRequirement, Justification = p.Justification });

        foreach (var w in src.JobWorkingConditions)
            await _workingConditionRepository.AddAsync(new JobWorkingCondition { TenantId = tenantId, JobDescriptionId = clone.Id, EnvironmentType = w.EnvironmentType, Description = w.Description, ExposureLevel = w.ExposureLevel, RequiresPPE = w.RequiresPPE, PPERequirements = w.PPERequirements, TravelPercentage = w.TravelPercentage, TravelRequirements = w.TravelRequirements });

        foreach (var pe in src.PpeRequirements)
            await _ppeRequirementRepository.AddAsync(new JobPpeRequirement { TenantId = tenantId, JobDescriptionId = clone.Id, PpeTypeId = pe.PpeTypeId, CustomPpeName = pe.CustomPpeName, IsMandatory = pe.IsMandatory, Notes = pe.Notes });

        foreach (var e in src.EquipmentTools)
        {
            var ne = new JobEquipmentTool { TenantId = tenantId, JobDescriptionId = clone.Id, ItemName = e.ItemName, Type = e.Type, DescriptionOrSpecification = e.DescriptionOrSpecification, RequiredProficiency = e.RequiredProficiency, IsEssential = e.IsEssential, TrainingRequired = e.TrainingRequired };
            await _equipmentToolRepository.AddAsync(ne);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            foreach (var t in e.TrainingRequirements)
                await _equipmentTrainingRepository.AddAsync(new JobEquipmentTraining { TenantId = tenantId, JobEquipmentToolId = ne.Id, TrainingProgramId = t.TrainingProgramId, RequirementText = t.RequirementText, IsMandatory = t.IsMandatory });
        }

        foreach (var rr in src.ReportingRelationships)
            await _reportingRelationshipRepository.AddAsync(new JobReportingRelationship { TenantId = tenantId, JobDescriptionId = clone.Id, RelationshipType = rr.RelationshipType, TitleOrRole = rr.TitleOrRole, EmployeeOrPositionId = rr.EmployeeOrPositionId, Description = rr.Description, NumberOfDirectReports = rr.NumberOfDirectReports, IsPrimarySupervisor = rr.IsPrimarySupervisor });

        foreach (var m in src.MedicalRequirements)
            await _medicalRequirementRepository.AddAsync(new JobMedicalRequirement { TenantId = tenantId, JobDescriptionId = clone.Id, Category = m.Category, RequirementDescription = m.RequirementDescription, Rationale = m.Rationale, Contraindications = m.Contraindications, IsMandatory = m.IsMandatory });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Job description cloned: {New} from {Old}", clone.JobDescriptionNumber, src.JobDescriptionNumber);
        return clone.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedJobDescriptionAsync(id);

        if (entity.Status == JobDescriptionStatus.Approved)
            throw JobArchitectureException.InvalidState("Cannot delete an approved job description.");

        await _jobDescriptionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job description deleted: {Id}", id);

        return true;
    }

    #region Responsibility Operations

    public async Task<JobResponsibilityDto> AddResponsibilityAsync(CreateJobResponsibilityDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _responsibilityRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Responsibility added to job description: {JobDescriptionId}", createDto.JobDescriptionId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<JobResponsibilityDto>> GetResponsibilitiesAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _responsibilityRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobResponsibilityDto> UpdateResponsibilityAsync(UpdateJobResponsibilityDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _responsibilityRepository.GetByIdAsync(updateDto.Id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Responsibility not found");

        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);

        updateDto.UpdateEntity(entity);

        await _responsibilityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Responsibility updated: {ResponsibilityId}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteResponsibilityAsync(Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        var entity = await _responsibilityRepository.GetByIdAsync(responsibilityId);

        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Responsibility not found");

        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);

        await _responsibilityRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Responsibility deleted: {ResponsibilityId}", responsibilityId);

        return true;
    }

    #endregion

    #region Qualification Operations

    public async Task<JobQualificationDto> AddQualificationAsync(CreateJobQualificationDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        await RequireResponsibilityOfAsync(
            createDto.JobResponsibilityId, createDto.JobDescriptionId, cancellationToken);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _qualificationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _qualificationRepository.GetQueryable()
            .Include(q => q.Qualification)
            .FirstOrDefaultAsync(q => q.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Qualification added to job description: {JobDescriptionId}", createDto.JobDescriptionId);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<JobQualificationDto>> GetQualificationsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _qualificationRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobQualificationDto> UpdateQualificationAsync(UpdateJobQualificationDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _qualificationRepository.GetQueryable()
            .Include(q => q.Qualification)
            .FirstOrDefaultAsync(q => q.Id == updateDto.Id, cancellationToken);

        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Qualification not found");

        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await RequireResponsibilityOfAsync(
            updateDto.JobResponsibilityId, entity.JobDescriptionId, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _qualificationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Qualification updated: {QualificationId}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default)
    {
        var entity = await _qualificationRepository.GetByIdAsync(qualificationId);

        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Qualification not found");

        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);

        await _qualificationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Qualification deleted: {QualificationId}", qualificationId);

        return true;
    }

    #endregion

    #region Competency Operations

    public async Task<JobCompetencyDto> AddCompetencyAsync(CreateJobCompetencyDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        await RequireResponsibilityOfAsync(
            createDto.JobResponsibilityId, createDto.JobDescriptionId, cancellationToken);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();

        await _competencyRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency added to job description: {JobDescriptionId}", createDto.JobDescriptionId);

        return entity.ToDto();
    }

    public async Task<IEnumerable<JobCompetencyDto>> GetCompetenciesAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _competencyRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobCompetencyDto> UpdateCompetencyAsync(UpdateJobCompetencyDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _competencyRepository.GetByIdAsync(updateDto.Id);

        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Competency not found");

        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await RequireResponsibilityOfAsync(
            updateDto.JobResponsibilityId, entity.JobDescriptionId, cancellationToken);

        updateDto.UpdateEntity(entity);

        await _competencyRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency updated: {CompetencyId}", updateDto.Id);

        return entity.ToDto();
    }

    public async Task<bool> DeleteCompetencyAsync(Guid competencyId, CancellationToken cancellationToken = default)
    {
        var entity = await _competencyRepository.GetByIdAsync(competencyId);

        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Competency not found");

        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);

        await _competencyRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Competency deleted: {CompetencyId}", competencyId);

        return true;
    }

    #endregion

    #region Physical Demand Operations

    public async Task<JobPhysicalDemandDto> AddPhysicalDemandAsync(CreateJobPhysicalDemandDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await _physicalDemandRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Physical demand added to job description: {JobDescriptionId}", createDto.JobDescriptionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobPhysicalDemandDto>> GetPhysicalDemandsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _physicalDemandRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobPhysicalDemandDto> UpdatePhysicalDemandAsync(UpdateJobPhysicalDemandDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _physicalDemandRepository.GetByIdAsync(updateDto.Id);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Physical demand not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        updateDto.UpdateEntity(entity);
        await _physicalDemandRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Physical demand updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeletePhysicalDemandAsync(Guid demandId, CancellationToken cancellationToken = default)
    {
        var entity = await _physicalDemandRepository.GetByIdAsync(demandId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Physical demand not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await _physicalDemandRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Physical demand deleted: {Id}", demandId);
        return true;
    }

    #endregion

    #region Working Condition Operations

    public async Task<JobWorkingConditionDto> AddWorkingConditionAsync(CreateJobWorkingConditionDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await _workingConditionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Working condition added to job description: {JobDescriptionId}", createDto.JobDescriptionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobWorkingConditionDto>> GetWorkingConditionsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _workingConditionRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobWorkingConditionDto> UpdateWorkingConditionAsync(UpdateJobWorkingConditionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _workingConditionRepository.GetByIdAsync(updateDto.Id);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Working condition not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        updateDto.UpdateEntity(entity);
        await _workingConditionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Working condition updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteWorkingConditionAsync(Guid conditionId, CancellationToken cancellationToken = default)
    {
        var entity = await _workingConditionRepository.GetByIdAsync(conditionId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Working condition not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await _workingConditionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Working condition deleted: {Id}", conditionId);
        return true;
    }

    #endregion

    #region Equipment Tool Operations

    public async Task<JobEquipmentToolDto> AddEquipmentToolAsync(CreateJobEquipmentToolDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await _equipmentToolRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Equipment tool added to job description: {JobDescriptionId}", createDto.JobDescriptionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobEquipmentToolDto>> GetEquipmentToolsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _equipmentToolRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobEquipmentToolDto> UpdateEquipmentToolAsync(UpdateJobEquipmentToolDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _equipmentToolRepository.GetByIdAsync(updateDto.Id);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Equipment tool not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        updateDto.UpdateEntity(entity);
        await _equipmentToolRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Equipment tool updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteEquipmentToolAsync(Guid toolId, CancellationToken cancellationToken = default)
    {
        var entity = await _equipmentToolRepository.GetByIdAsync(toolId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Equipment tool not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await _equipmentToolRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Equipment tool deleted: {Id}", toolId);
        return true;
    }

    #endregion

    #region Reporting Relationship Operations

    public async Task<JobReportingRelationshipDto> AddReportingRelationshipAsync(CreateJobReportingRelationshipDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await _reportingRelationshipRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Reporting relationship added to job description: {JobDescriptionId}", createDto.JobDescriptionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobReportingRelationshipDto>> GetReportingRelationshipsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _reportingRelationshipRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobReportingRelationshipDto> UpdateReportingRelationshipAsync(UpdateJobReportingRelationshipDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _reportingRelationshipRepository.GetByIdAsync(updateDto.Id);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Reporting relationship not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        updateDto.UpdateEntity(entity);
        await _reportingRelationshipRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Reporting relationship updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteReportingRelationshipAsync(Guid relationshipId, CancellationToken cancellationToken = default)
    {
        var entity = await _reportingRelationshipRepository.GetByIdAsync(relationshipId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Reporting relationship not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await _reportingRelationshipRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Reporting relationship deleted: {Id}", relationshipId);
        return true;
    }

    #endregion

    #region Duty Item Operations

    public async Task<JobDutyItemDto> AddDutyItemAsync(CreateJobDutyItemDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        if (entity.SequenceNumber <= 0)
            entity.SequenceNumber = await _dutyItemRepository.GetNextSequenceNumberAsync(createDto.JobDescriptionId);
        await _dutyItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Duty item added to job description: {JobDescriptionId}", createDto.JobDescriptionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobDutyItemDto>> GetDutyItemsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _dutyItemRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobDutyItemDto> UpdateDutyItemAsync(UpdateJobDutyItemDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _dutyItemRepository.GetByIdAsync(updateDto.Id);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Duty item not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        updateDto.UpdateEntity(entity);
        await _dutyItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Duty item updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteDutyItemAsync(Guid dutyItemId, CancellationToken cancellationToken = default)
    {
        var entity = await _dutyItemRepository.GetByIdAsync(dutyItemId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Duty item not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await _dutyItemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Duty item deleted: {Id}", dutyItemId);
        return true;
    }

    #endregion

    #region PPE Requirement Operations

    public async Task<JobPpeRequirementDto> AddPpeRequirementAsync(CreateJobPpeRequirementDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await _ppeRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _ppeRequirementRepository.GetQueryable()
            .Include(p => p.PpeType)
            .FirstOrDefaultAsync(p => p.Id == entity.Id, cancellationToken);

        _logger.LogInformation("PPE requirement added to job description: {JobDescriptionId}", createDto.JobDescriptionId);
        return entity!.ToDto();
    }

    public async Task<IEnumerable<JobPpeRequirementDto>> GetPpeRequirementsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _ppeRequirementRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobPpeRequirementDto> UpdatePpeRequirementAsync(UpdateJobPpeRequirementDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _ppeRequirementRepository.GetQueryable()
            .Include(p => p.PpeType)
            .FirstOrDefaultAsync(p => p.Id == updateDto.Id, cancellationToken);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("PPE requirement not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        updateDto.UpdateEntity(entity);
        await _ppeRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("PPE requirement updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeletePpeRequirementAsync(Guid ppeRequirementId, CancellationToken cancellationToken = default)
    {
        var entity = await _ppeRequirementRepository.GetByIdAsync(ppeRequirementId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("PPE requirement not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await _ppeRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("PPE requirement deleted: {Id}", ppeRequirementId);
        return true;
    }

    #endregion

    #region Equipment Training Operations

    public async Task<JobEquipmentTrainingDto> AddEquipmentTrainingAsync(CreateJobEquipmentTrainingDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableForEquipmentToolAsync(createDto.JobEquipmentToolId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await _equipmentTrainingRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _equipmentTrainingRepository.GetQueryable()
            .Include(t => t.TrainingProgram)
            .FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Equipment training added to tool: {ToolId}", createDto.JobEquipmentToolId);
        return entity!.ToDto();
    }

    public async Task<IEnumerable<JobEquipmentTrainingDto>> GetEquipmentTrainingsAsync(Guid jobEquipmentToolId, CancellationToken cancellationToken = default)
    {
        var tool = await _equipmentToolRepository.GetByIdAsync(jobEquipmentToolId);
        if (tool == null || tool.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Equipment tool not found");
        var tenantId = GetTenantId();
        var entities = await _equipmentTrainingRepository.GetByEquipmentToolIdAsync(jobEquipmentToolId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobEquipmentTrainingDto> UpdateEquipmentTrainingAsync(UpdateJobEquipmentTrainingDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _equipmentTrainingRepository.GetQueryable()
            .Include(t => t.TrainingProgram)
            .FirstOrDefaultAsync(t => t.Id == updateDto.Id, cancellationToken);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Equipment training not found");
        await RequireAuthorableForEquipmentToolAsync(entity.JobEquipmentToolId);
        updateDto.UpdateEntity(entity);
        await _equipmentTrainingRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Equipment training updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteEquipmentTrainingAsync(Guid equipmentTrainingId, CancellationToken cancellationToken = default)
    {
        var entity = await _equipmentTrainingRepository.GetByIdAsync(equipmentTrainingId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Equipment training not found");
        await RequireAuthorableForEquipmentToolAsync(entity.JobEquipmentToolId);
        await _equipmentTrainingRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Equipment training deleted: {Id}", equipmentTrainingId);
        return true;
    }

    #endregion

    #region Medical Requirement Operations

    public async Task<JobMedicalRequirementDto> AddMedicalRequirementAsync(CreateJobMedicalRequirementDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableJobDescriptionAsync(createDto.JobDescriptionId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        await _medicalRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Medical requirement added to job description: {JobDescriptionId}", createDto.JobDescriptionId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobMedicalRequirementDto>> GetMedicalRequirementsAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        await GetOwnedJobDescriptionAsync(jobDescriptionId);
        var tenantId = GetTenantId();
        var entities = await _medicalRequirementRepository.GetByJobDescriptionIdAsync(jobDescriptionId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobMedicalRequirementDto> UpdateMedicalRequirementAsync(UpdateJobMedicalRequirementDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _medicalRequirementRepository.GetByIdAsync(updateDto.Id);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Medical requirement not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        updateDto.UpdateEntity(entity);
        await _medicalRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Medical requirement updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteMedicalRequirementAsync(Guid medicalRequirementId, CancellationToken cancellationToken = default)
    {
        var entity = await _medicalRequirementRepository.GetByIdAsync(medicalRequirementId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("Medical requirement not found");
        await RequireAuthorableJobDescriptionAsync(entity.JobDescriptionId);
        await _medicalRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Medical requirement deleted: {Id}", medicalRequirementId);
        return true;
    }

    #endregion

    #region Job Evaluation / Valuation

    /// <summary>Works out what the role is worth and returns it. Changes nothing.</summary>
    /// <remarks>
    /// ⚠ This used to persist what it computed, which made a GET a write. Everything in an HTTP
    /// stack assumes a GET is safe and repeats it freely — a retry on a dropped connection, a cache
    /// revalidation, a client refetching on window focus, a health probe — and every one of those
    /// became an UPDATE. The values were at least deterministic, so nothing corrupted; what it did
    /// cost was real all the same: <c>SaveChanges</c> stamps <c>UpdatedAt</c> on any modified row,
    /// so reading a valuation bumped the job description's last-modified date, on an approved and
    /// in-force document as readily as on a draft. Persisting now belongs to
    /// <see cref="RecalculateValuationAsync"/>, which is a POST, is gated on Write, and refuses an
    /// approved record.
    /// </remarks>
    public Task<JobValuationSummaryDto> GetValuationAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
        => ValueRoleAsync(jobDescriptionId, persist: false, cancellationToken);

    /// <summary>Works the valuation out and STORES it on the job description.</summary>
    public async Task<JobValuationSummaryDto> RecalculateValuationAsync(Guid jobDescriptionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var status = await _jobDescriptionRepository.GetQueryable()
            .Where(x => x.Id == jobDescriptionId && x.TenantId == tenantId)
            .Select(x => (JobDescriptionStatus?)x.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status == null)
            throw JobArchitectureException.NotFound($"Job description with ID '{jobDescriptionId}' not found.");

        // The same rule the rest of the document follows: an approved description is the one in
        // force for its position, and its stored figures are part of what was approved. Re-valuing
        // it would rewrite them with no new version and no trace.
        if (status == JobDescriptionStatus.Approved)
            throw JobArchitectureException.InvalidState(
                "Cannot re-value an approved job description. Create a new version instead.");

        return await ValueRoleAsync(jobDescriptionId, persist: true, cancellationToken);
    }

    private async Task<JobValuationSummaryDto> ValueRoleAsync(
        Guid jobDescriptionId, bool persist, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var jd = await _jobDescriptionRepository.GetQueryable()
            .Include(x => x.SuggestedSalaryGrade)
            .FirstOrDefaultAsync(x => x.Id == jobDescriptionId && x.TenantId == tenantId, cancellationToken);
        if (jd == null)
            throw JobArchitectureException.NotFound($"Job description with ID '{jobDescriptionId}' not found.");

        var qualifications = (await _qualificationRepository.GetByJobDescriptionIdAsync(jobDescriptionId))
            .Where(q => q.TenantId == tenantId).ToList();
        var competencies = (await _competencyRepository.GetByJobDescriptionIdAsync(jobDescriptionId))
            .Where(c => c.TenantId == tenantId).ToList();

        var totalQual = qualifications.Sum(q => q.MonetaryValue ?? 0m);
        var totalComp = competencies.Sum(c => c.MonetaryValue ?? 0m);
        var roleValue = jd.RoleIntrinsicValue ?? 0m;
        var totalEstimated = totalQual + totalComp + roleValue;

        // Midpoint blends the computed value with any external industry benchmark.
        decimal midpoint;
        if (jd.IndustryBenchmarkSalary.HasValue && jd.IndustryBenchmarkSalary.Value > 0 && totalEstimated > 0)
            midpoint = (totalEstimated + jd.IndustryBenchmarkSalary.Value) / 2m;
        else if (jd.IndustryBenchmarkSalary.HasValue && jd.IndustryBenchmarkSalary.Value > 0)
            midpoint = jd.IndustryBenchmarkSalary.Value;
        else
            midpoint = totalEstimated;

        decimal? low = midpoint > 0 ? Math.Round(midpoint * 0.9m, 2) : (decimal?)null;
        decimal? high = midpoint > 0 ? Math.Round(midpoint * 1.1m, 2) : (decimal?)null;

        // Match a salary grade whose band contains the midpoint (tenant-scoped); else nearest by min salary.
        SalaryGrade? suggestedGrade = null;
        if (midpoint > 0)
        {
            var grades = await _salaryGradeRepository.GetAllAsync(jd.TenantId, includeInactive: false, cancellationToken);
            suggestedGrade = grades.FirstOrDefault(g => g.MinSalary <= midpoint && midpoint <= g.MaxSalary)
                          ?? grades.OrderBy(g => Math.Abs(g.MinSalary - midpoint)).FirstOrDefault();
        }

        // Only when asked. The read path computes the same figures and leaves the record alone.
        if (persist)
        {
            jd.EstimatedSalaryLow = low;
            jd.EstimatedSalaryHigh = high;
            jd.SuggestedSalaryGradeId = suggestedGrade?.Id;
            await _jobDescriptionRepository.UpdateAsync(jd);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new JobValuationSummaryDto
        {
            JobDescriptionId = jd.Id,
            JobTitle = jd.JobTitle,
            TotalQualificationValue = totalQual,
            TotalCompetencyValue = totalComp,
            RoleIntrinsicValue = roleValue,
            RoleCriticality = jd.RoleCriticality,
            IndustryBenchmarkSalary = jd.IndustryBenchmarkSalary,
            EstimatedSalaryLow = low,
            EstimatedSalaryHigh = high,
            SuggestedSalaryGradeId = suggestedGrade?.Id,
            SuggestedSalaryGradeName = suggestedGrade?.Name,
            SuggestedGradeMinSalary = suggestedGrade?.MinSalary,
            SuggestedGradeMaxSalary = suggestedGrade?.MaxSalary,
            ValuationNotes = jd.ValuationNotes,
            QualificationLines = qualifications
                .Select(q => new JobValuationLineDto { Id = q.Id, Name = q.Title, MonetaryValue = q.MonetaryValue })
                .ToList(),
            CompetencyLines = competencies
                .Select(c => new JobValuationLineDto { Id = c.Id, Name = c.CompetencyName, MonetaryValue = c.MonetaryValue })
                .ToList()
        };
    }

    #endregion

    #region Responsibility KPI Operations

    public async Task<JobResponsibilityKpiDto> AddResponsibilityKpiAsync(CreateJobResponsibilityKpiDto createDto, CancellationToken cancellationToken = default)
    {
        await RequireAuthorableForResponsibilityAsync(createDto.JobResponsibilityId);
        var entity = createDto.ToEntity();
        entity.TenantId = GetTenantId();
        if (entity.SequenceNumber <= 0)
        {
            var existing = await _kpiRepository.GetByResponsibilityIdAsync(createDto.JobResponsibilityId);
            entity.SequenceNumber = (existing.Any() ? existing.Max(k => k.SequenceNumber) : 0) + 1;
        }
        await _kpiRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("KPI added to responsibility: {ResponsibilityId}", createDto.JobResponsibilityId);
        return entity.ToDto();
    }

    public async Task<IEnumerable<JobResponsibilityKpiDto>> GetResponsibilityKpisAsync(Guid responsibilityId, CancellationToken cancellationToken = default)
    {
        var responsibility = await _responsibilityRepository.GetByIdAsync(responsibilityId);
        if (responsibility == null || responsibility.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Responsibility not found");
        var tenantId = GetTenantId();
        var entities = await _kpiRepository.GetByResponsibilityIdAsync(responsibilityId);
        return entities.Where(e => e.TenantId == tenantId).ToDtoList();
    }

    public async Task<JobResponsibilityKpiDto> UpdateResponsibilityKpiAsync(UpdateJobResponsibilityKpiDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiRepository.GetByIdAsync(updateDto.Id);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("KPI not found");
        await RequireAuthorableForResponsibilityAsync(entity.JobResponsibilityId);
        updateDto.UpdateEntity(entity);
        await _kpiRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("KPI updated: {Id}", updateDto.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteResponsibilityKpiAsync(Guid kpiId, CancellationToken cancellationToken = default)
    {
        var entity = await _kpiRepository.GetByIdAsync(kpiId);
        if (entity == null || entity.TenantId != GetTenantId()) throw JobArchitectureException.NotFound("KPI not found");
        await RequireAuthorableForResponsibilityAsync(entity.JobResponsibilityId);
        await _kpiRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("KPI deleted: {Id}", kpiId);
        return true;
    }

    #endregion

    #region Helper Methods

    private async Task<string> GenerateJobDescriptionNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var tenantId = GetTenantId();
        var prefix = $"JD-{year}-";

        // ⚠ This counted live rows and returned count + 1, which repeats a number the moment any
        // row is deleted — measured 2026-08-19: two job descriptions came back as JD-2026-00058.
        // There is no unique index on JobDescriptionNumber, so the collision does not fail; it just
        // produces two documents with one number, and the register shows them as duplicates.
        //
        // Take the highest number already issued instead of counting, and read through the soft
        // delete: a deleted job description has still consumed its number, and a document number
        // that gets reissued is worse than one with a gap. Same reasoning as the succession
        // document-number fix (a soft delete does not release what a counter assumes it released).
        var issued = await _jobDescriptionRepository.GetQueryableIncludingDeleted(
                jd => jd.TenantId == tenantId && jd.JobDescriptionNumber.StartsWith(prefix))
            .Select(jd => jd.JobDescriptionNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(number => int.TryParse(number[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
    }

    #endregion
}

#endregion Job Description Service

#region Manpower Budget Service

public class ManpowerBudgetService : IManpowerBudgetService
{
    private readonly IManpowerBudgetRepository _budgetRepository;
    private readonly IManpowerBudgetLineRepository _budgetLineRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IWorkflowIntegrationService _workflowIntegration;
    private readonly IWorkflowStatusAdapterRegistry _workflowAdapters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICompanyHrPolicyProvider _policyProvider;
    private readonly ILogger<ManpowerBudgetService> _logger;

    public ManpowerBudgetService(
        IManpowerBudgetRepository budgetRepository,
        IManpowerBudgetLineRepository budgetLineRepository,
        ICurrentUserProvider currentUserProvider,
        IWorkflowIntegrationService workflowIntegration,
        IWorkflowStatusAdapterRegistry workflowAdapters,
        IUnitOfWork unitOfWork,
        ICompanyHrPolicyProvider policyProvider,
        ILogger<ManpowerBudgetService> logger)
    {
        _budgetRepository = budgetRepository;
        _budgetLineRepository = budgetLineRepository;
        _currentUserProvider = currentUserProvider;
        _workflowIntegration = workflowIntegration;
        _workflowAdapters = workflowAdapters;
        _unitOfWork = unitOfWork;
        _policyProvider = policyProvider;
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

    private async Task<ManpowerBudget> GetOwnedBudgetAsync(Guid id)
    {
        var entity = await _budgetRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound($"Manpower budget with ID '{id}' not found.");
        return entity;
    }

    public async Task<ManpowerBudgetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _budgetRepository.GetQueryable()
            .Include(b => b.OrganizationLevel)
            .Include(b => b.OrganizationUnit)
            .Include(b => b.ApprovedBy)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw JobArchitectureException.NotFound($"Manpower budget with ID '{id}' not found.");

        return entity.ToDto();
    }

    public async Task<ManpowerBudgetDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _budgetRepository.GetQueryable()
            .Include(b => b.OrganizationLevel)
            .Include(b => b.OrganizationUnit)
            .Include(b => b.ApprovedBy)
            .Include(b => b.BudgetLines).ThenInclude(l => l.Position)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw JobArchitectureException.NotFound($"Manpower budget with ID '{id}' not found.");

        return entity.ToDetailDto();
    }

    public async Task<IEnumerable<ManpowerBudgetDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _budgetRepository.GetQueryable()
            .Where(b => b.TenantId == tenantId)
            .WithLookups()
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    public async Task<PagedResult<ManpowerBudgetDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _budgetRepository.GetQueryable()
            .Where(b => b.TenantId == tenantId)
            .WithLookups();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(b => b.FiscalYear)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ManpowerBudgetDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<ManpowerBudgetSummaryDto>> GetByFiscalYearAsync(int fiscalYear, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _budgetRepository.GetByFiscalYearAsync(fiscalYear)).Where(b => b.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ManpowerBudgetSummaryDto>> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _budgetRepository.GetByOrganizationUnitIdAsync(organizationUnitId)).Where(b => b.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ManpowerBudgetSummaryDto>> GetByOrganizationLevelIdAsync(Guid organizationLevelId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _budgetRepository.GetByOrganizationLevelIdAsync(organizationLevelId)).Where(b => b.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<IEnumerable<ManpowerBudgetSummaryDto>> GetByStatusAsync(ManpowerBudgetStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _budgetRepository.GetByStatusAsync(status)).Where(b => b.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<ManpowerBudgetDto?> GetCurrentBudgetForOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _budgetRepository.GetCurrentBudgetForOrganizationUnitAsync(organizationUnitId);
        return entity == null || entity.TenantId != tenantId ? null : entity.ToDto();
    }

    public async Task<IEnumerable<ManpowerBudgetSummaryDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _budgetRepository.GetPendingApprovalsAsync()).Where(b => b.TenantId == tenantId);
        return entities.ToSummaryDtoList();
    }

    public async Task<ManpowerBudgetDto> CreateAsync(CreateManpowerBudgetDto createDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Round 2b, R1: the unit must be this tenant's, and the level follows the unit when the
        // caller names none — the flat dropdown never sent a level, so every budget in the table
        // carried a unit and no level. Same rule as UpdateAsync.
        if (createDto.OrganizationUnitId.HasValue)
        {
            var unit = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                .FirstOrDefaultAsync(u => u.Id == createDto.OrganizationUnitId.Value
                                       && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
            if (unit is null)
                throw JobArchitectureException.Invalid(
                    "The organisation unit named for this budget does not exist in this organisation.");
            if (!createDto.OrganizationLevelId.HasValue)
                createDto.OrganizationLevelId = unit.OrganizationLevelId;
        }

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;
        entity.BudgetNumber = await GenerateBudgetNumberAsync(cancellationToken);
        entity.Status = ManpowerBudgetStatus.Draft;
        entity.TotalBudget = createDto.SalaryBudget + createDto.BenefitsBudget + createDto.RecruitmentBudget + createDto.TrainingBudget;

        await _budgetRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Add budget lines if provided
        if (createDto.BudgetLines?.Any() == true)
        {
            foreach (var lineDto in createDto.BudgetLines)
            {
                lineDto.ManpowerBudgetId = entity.Id;
                var line = lineDto.ToEntity();
                line.TenantId = tenantId;
                await _budgetLineRepository.AddAsync(line);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Manpower budget created: {BudgetNumber}", entity.BudgetNumber);

        // Re-read so the response carries the unit and level NAMES (the entity was built from the
        // DTO and has no navigations loaded — the lane F2 shape).
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ManpowerBudgetDto> UpdateAsync(UpdateManpowerBudgetDto updateDto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _budgetRepository.GetQueryable()
            .Include(b => b.OrganizationLevel)
            .Include(b => b.OrganizationUnit)
            .FirstOrDefaultAsync(b => b.Id == updateDto.Id && b.TenantId == tenantId, cancellationToken);

        if (entity == null)
            throw JobArchitectureException.NotFound($"Manpower budget with ID '{updateDto.Id}' not found.");

        // ⚠ Was "refuse Approved only", which let a budget be corrected WHILE it was out for
        // approval — the three approvers of FR-HR-135's chain reading a moving target. A budget is
        // its author's while Draft or Rejected and the approvers' from Submitted on (round 2b, R1).
        if (entity.Status != ManpowerBudgetStatus.Draft && entity.Status != ManpowerBudgetStatus.Rejected)
            throw JobArchitectureException.InvalidState(
                $"A {entity.Status} budget cannot be corrected. Only a Draft or Rejected budget can be " +
                "edited: one out for approval is what its approvers are reading, and an approved one is the record.");

        // The scope (year, unit, level) is editable here too — null on the DTO means unchanged. A
        // unit must exist in this tenant; the level follows the unit unless the caller names one.
        if (updateDto.OrganizationUnitId.HasValue && updateDto.OrganizationUnitId != entity.OrganizationUnitId)
        {
            var unit = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
                .FirstOrDefaultAsync(u => u.Id == updateDto.OrganizationUnitId.Value
                                       && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
            if (unit is null)
                throw JobArchitectureException.Invalid(
                    "The organisation unit named for this budget does not exist in this organisation.");
            if (!updateDto.OrganizationLevelId.HasValue)
                updateDto.OrganizationLevelId = unit.OrganizationLevelId;
        }

        // A correction does not move the budget through its chain — submit, approve and reject do.
        // A caller who sends the current status is round-tripping the record and is fine; one who
        // sends a different status is told, rather than left believing the transition happened.
        if (updateDto.Status.HasValue && updateDto.Status.Value != entity.Status)
            throw JobArchitectureException.Invalid(
                $"A manpower budget's status is not changed by correcting it. This one is {entity.Status}; " +
                "use submit, approve or reject to move it.");

        updateDto.UpdateEntity(entity);
        entity.TotalBudget = updateDto.SalaryBudget + updateDto.BenefitsBudget + updateDto.RecruitmentBudget + updateDto.TrainingBudget;
        // Against the stored actuals, which nothing in HR writes — not a caller-supplied figure.
        entity.Variance = entity.TotalBudget - entity.ActualSpent;

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manpower budget updated: {BudgetNumber}", entity.BudgetNumber);

        // Re-read: the tracked entity still carries the OLD unit/level navigations after a scope
        // change, and mapping it would answer with the previous unit's name (the lane F2 lesson).
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> SubmitForApprovalAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(budgetId);

        // A Rejected budget is resubmittable on purpose (round 2b, R1): correct-and-resubmit is the
        // loop that status exists for. Until R1 it could only be deleted — and a rejected budget
        // cannot be deleted either (Draft only), so it was simply stuck.
        if (entity.Status != ManpowerBudgetStatus.Draft && entity.Status != ManpowerBudgetStatus.Rejected)
            throw JobArchitectureException.InvalidState(
                "Only a Draft or Rejected budget can be submitted for approval.");

        // ⚠ Refuse an empty budget before troubling anyone with it. A manpower budget with no lines
        // authorises no posts, so sending one up FR-HR-135's three-step chain wastes three people's
        // time and — because slice 8 derives the establishment from the lines — would approve an
        // establishment of nothing.
        var lineCount = await _budgetLineRepository.GetQueryable()
            .CountAsync(l => l.ManpowerBudgetId == entity.Id && !l.IsDeleted, cancellationToken);
        if (lineCount == 0)
            throw JobArchitectureException.InvalidState(
                "This budget has no lines, so it authorises no posts. Add at least one before submitting it.");

        await RequireEstablishmentIsAchievableAsync(entity, cancellationToken);

        if (await IsBudgetWorkflowConfiguredAsync(cancellationToken))
        {
            var submitResult = await _workflowIntegration.SubmitAsync(BudgetWorkflowEntityType, entity.Id);
            if (!submitResult.ExecutionResult.Success)
                throw JobArchitectureException.InvalidState(
                    submitResult.ExecutionResult.Message ?? "Failed to start the manpower budget approval workflow.");

            _workflowAdapters.GetAdapter(BudgetWorkflowEntityType)
                .ApplySubmitOutcome(entity, submitResult.Outcome, _currentUserProvider.UserId);
        }
        else
        {
            entity.Status = ManpowerBudgetStatus.Submitted;
        }

        entity.RejectionReason = null;

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manpower budget submitted for approval: {BudgetNumber}", entity.BudgetNumber);

        return true;
    }

    // ── Approval on the workflow engine (slice 7, FR-HR-135) ──────────────────

    private const string BudgetWorkflowEntityType = "ManpowerBudget";

    /// <summary>Whether this tenant has published a manpower-budget approval workflow.</summary>
    private async Task<bool> IsBudgetWorkflowConfiguredAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _unitOfWork.Repository<Entities.Workflow.WorkflowDefinition>().GetQueryable()
                .AnyAsync(d => d.TenantId == GetTenantId()
                            && !d.IsDeleted
                            && d.IsActive
                            && d.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published
                            && d.EntityType != null
                            && d.EntityType.Code == "MANPOWER_BUDGET",
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not determine whether a manpower budget approval workflow is published; allowing the direct path.");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ApproveViaWorkflowAsync(Guid budgetId, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(budgetId);
        var userId = _currentUserProvider.UserId;

        if (!await _workflowIntegration.CanUserApproveAsync(BudgetWorkflowEntityType, budgetId, userId))
            throw new UnauthorizedAccessException(
                "You are not assigned as an approver for the current workflow step.");

        var result = await _workflowIntegration.ProcessApprovalAsync(BudgetWorkflowEntityType, budgetId, userId, "Approve");
        if (!result.ExecutionResult.Success)
            throw JobArchitectureException.InvalidState(
                result.ExecutionResult.Message ?? "Failed to process the manpower budget approval.");

        _workflowAdapters.GetAdapter(BudgetWorkflowEntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, userId);

        // ⚠ Only when the chain actually completes. FR-HR-135 has three steps, and a Department
        // Head approving the first must not stamp the budget as approved — the engine returns
        // Pending for a mid-chain step, and an approver is not the approver until the last one.
        if (result.Outcome == WorkflowOutcome.Approved)
        {
            entity.ApprovedById = approvedById;
            // Only now. A department head approving step 1 of 3 has authorised nothing yet, and
            // writing the establishment there would let the first approver set the headcount the
            // other two are still deciding on.
            await ApplyEstablishmentAsync(entity, cancellationToken);
        }

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manpower budget {Number} approval step processed: {Outcome}",
            entity.BudgetNumber, result.Outcome);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RejectViaWorkflowAsync(Guid budgetId, string? reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(budgetId);
        var userId = _currentUserProvider.UserId;

        if (!await _workflowIntegration.CanUserApproveAsync(BudgetWorkflowEntityType, budgetId, userId))
            throw new UnauthorizedAccessException(
                "You are not assigned as an approver for the current workflow step.");

        var rejectionText = string.IsNullOrWhiteSpace(reason) ? "Rejected" : reason.Trim();
        var result = await _workflowIntegration.ProcessApprovalAsync(
            BudgetWorkflowEntityType, budgetId, userId, "Reject", rejectionText);
        if (!result.ExecutionResult.Success)
            throw JobArchitectureException.InvalidState(
                result.ExecutionResult.Message ?? "Failed to process the manpower budget rejection.");

        _workflowAdapters.GetAdapter(BudgetWorkflowEntityType)
            .ApplyApprovalOutcome(entity, result.Outcome, userId, rejectionText);

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manpower budget {Number} rejected on the workflow", entity.BudgetNumber);

        return true;
    }

    public async Task<bool> ApproveAsync(ApproveManpowerBudgetDto approveDto, Guid approvedById, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(approveDto.BudgetId);

        if (entity.Status != ManpowerBudgetStatus.Submitted && entity.Status != ManpowerBudgetStatus.UnderReview)
            throw JobArchitectureException.InvalidState("Only submitted budgets can be approved.");

        if (await IsBudgetWorkflowConfiguredAsync(cancellationToken))
            throw JobArchitectureException.InvalidState(
                "This tenant approves manpower budgets through the workflow engine (FR-HR-135). "
                + "Approve it from the workflow queue instead.");

        entity.ApprovedById = approvedById;
        entity.RejectionReason = null;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.Status = ManpowerBudgetStatus.Approved;

        await ApplyEstablishmentAsync(entity, cancellationToken);

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manpower budget approved: {BudgetNumber}", entity.BudgetNumber);

        return true;
    }

    /// <inheritdoc />
    public async Task<PositionEstablishmentResultDto> SetPositionEstablishmentAsync(
        Guid positionId, SetPositionEstablishmentDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var position = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == positionId && p.TenantId == tenantId, cancellationToken)
            ?? throw JobArchitectureException.NotFound($"Position with ID '{positionId}' not found.");

        // ⚠ Refuse to establish a post below the number of people already in it. The establishment
        // is what a later requisition or movement is measured against, and one that is already
        // breached on the day it is set makes every subsequent action fail for a reason nobody took.
        var occupied = await _unitOfWork.Repository<Employee>().GetQueryable()
            .CountAsync(e => e.TenantId == tenantId && e.PositionId == positionId && e.IsActive,
                cancellationToken);
        if (dto.ExpectedHeadcount < occupied)
            throw JobArchitectureException.Invalid(
                $"{position.Title} already has {occupied} employee(s) in post, so it cannot be "
                + $"established for {dto.ExpectedHeadcount}. Move them first, or establish it for at least {occupied}.");

        position.ExpectedHeadcount = dto.ExpectedHeadcount;
        position.EstablishmentApprovedOn = DateTime.UtcNow;
        // Null on purpose: this number did NOT come from a budget, and a screen has to be able to
        // say so rather than implying an approval chain that never ran.
        position.EstablishmentSourceBudgetId = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Establishment for position {PositionId} set to {Headcount} directly by HR: {Reason}",
            positionId, dto.ExpectedHeadcount, dto.Reason);

        return await GetPositionEstablishmentAsync(positionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PositionEstablishmentResultDto> WithdrawPositionEstablishmentAsync(
        Guid positionId, string reason, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var position = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == positionId && p.TenantId == tenantId, cancellationToken)
            ?? throw JobArchitectureException.NotFound($"Position with ID '{positionId}' not found.");

        if (position.EstablishmentApprovedOn == null)
            throw JobArchitectureException.InvalidState(
                $"{position.Title} has no approved establishment to withdraw.");

        position.EstablishmentApprovedOn = null;
        position.EstablishmentSourceBudgetId = null;
        // ⚠ ExpectedHeadcount is deliberately LEFT AS IT IS. Withdrawing an establishment says "this
        // number is no longer authorised", not "this number is wrong" — and the column has no
        // meaningful null. Every rule keys off EstablishmentApprovedOn, so clearing that is what
        // actually releases the constraint; blanking the count as well would destroy the planning
        // figure for no benefit.

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Establishment withdrawn for position {PositionId}: {Reason}", positionId, reason);

        return await GetPositionEstablishmentAsync(positionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PositionEstablishmentResultDto> GetPositionEstablishmentAsync(
        Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var position = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == positionId && p.TenantId == tenantId, cancellationToken)
            ?? throw JobArchitectureException.NotFound($"Position with ID '{positionId}' not found.");

        var occupied = await _unitOfWork.Repository<Employee>().GetQueryable()
            .CountAsync(e => e.TenantId == tenantId && e.PositionId == positionId && e.IsActive,
                cancellationToken);

        string? sourceNumber = null;
        if (position.EstablishmentSourceBudgetId != null)
        {
            sourceNumber = await _budgetRepository.GetQueryable()
                .Where(b => b.Id == position.EstablishmentSourceBudgetId)
                .Select(b => b.BudgetNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new PositionEstablishmentResultDto
        {
            PositionId = position.Id,
            PositionTitle = position.Title,
            ExpectedHeadcount = position.ExpectedHeadcount,
            CurrentlyFilled = occupied,
            EstablishmentApprovedOn = position.EstablishmentApprovedOn,
            EstablishmentSourceBudgetId = position.EstablishmentSourceBudgetId,
            EstablishmentSourceBudgetNumber = sourceNumber,
            // ⚠ The whole point of the column. An unestablished post is not constrained by anything,
            // whatever its ExpectedHeadcount happens to say.
            IsEstablished = position.EstablishmentApprovedOn != null,
        };
    }

    /// <summary>
    /// Refuses a budget that would establish a post for fewer people than are already in it.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Written after the area-8 movements harness caught the omission: the <b>admin</b>
    /// establishment path had this guard from the start and the <b>budget</b> path did not, so an
    /// approved budget quietly established a post for 1 while 62 employees stood in it. Every
    /// movement into that post was then refused, correctly and unhelpfully, by a rule enforcing a
    /// number that had never been achievable.</para>
    ///
    /// <para>The budget path is the one that will carry most of the organisation, so it needed the
    /// stricter guard, not the looser one. An approved budget that establishes fewer posts than
    /// exist is a data error either way — either the planned count is wrong or people are in the
    /// wrong posts — and creating an immediately-breached establishment resolves neither.</para>
    ///
    /// <para>⚠ Checked at <b>submit</b> as well as at approval. Failing at step 3 of FR-HR-135's
    /// chain, after a department head, HR and the Managing Director have each spent time on it, is
    /// the worst moment to discover a number that was wrong when it was typed.</para>
    /// </remarks>
    private async Task RequireEstablishmentIsAchievableAsync(
        ManpowerBudget budget, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        var lines = await _budgetLineRepository.GetQueryable()
            .Where(l => l.ManpowerBudgetId == budget.Id && !l.IsDeleted && l.TenantId == tenantId)
            .Select(l => new { l.PositionId, l.PlannedCount })
            .ToListAsync(cancellationToken);
        if (lines.Count == 0) return;

        var positionIds = lines.Select(l => l.PositionId).Distinct().ToList();

        var occupancy = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && positionIds.Contains(e.PositionId) && e.IsActive)
            .GroupBy(e => e.PositionId)
            .Select(g => new { PositionId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var titles = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .Where(p => positionIds.Contains(p.Id) && p.TenantId == tenantId)
            .Select(p => new { p.Id, p.Title })
            .ToListAsync(cancellationToken);

        var breaches = new List<string>();
        foreach (var positionId in positionIds)
        {
            var planned = lines.Where(l => l.PositionId == positionId).Max(l => l.PlannedCount);
            var filled = occupancy.FirstOrDefault(o => o.PositionId == positionId)?.Count ?? 0;
            if (planned >= filled) continue;

            var title = titles.FirstOrDefault(t => t.Id == positionId)?.Title ?? positionId.ToString();
            breaches.Add($"{title} is budgeted for {planned} but {filled} are in post");
        }

        if (breaches.Count > 0)
            throw JobArchitectureException.Invalid(
                "This budget would establish fewer posts than are currently filled: "
                + string.Join("; ", breaches)
                + ". Raise the planned count, or move the employees first.");
    }

    /// <summary>
    /// Writes the approved budget's planned headcount onto each position it covers — decision D-2,
    /// and what makes FR-HR-136 enforceable at all.
    /// </summary>
    /// <remarks>
    /// <para>The budget IS the approved establishment. Once Department Head, HR and the Managing
    /// Director have signed off <c>PlannedCount</c> for a position (FR-HR-135), that number is what
    /// the organisation has authorised, and there is no second artefact to keep in step with it.</para>
    ///
    /// <para>⚠ <c>EstablishmentApprovedOn</c> is the load-bearing part, not the headcount.
    /// <c>ExpectedHeadcount</c> already existed and already held a number for every position — the
    /// default, 1, on 132 of 146. Stamping the date is what lets every downstream rule tell an
    /// authorised establishment from an untouched column, and therefore have teeth on the first
    /// without refusing everything on the second.</para>
    ///
    /// <para>Runs on both approval routes, from the one place, for the same reason superseding a
    /// job description does.</para>
    /// </remarks>
    private async Task ApplyEstablishmentAsync(ManpowerBudget budget, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        await RequireEstablishmentIsAchievableAsync(budget, cancellationToken);

        var lines = await _budgetLineRepository.GetQueryable()
            .Where(l => l.ManpowerBudgetId == budget.Id && !l.IsDeleted && l.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        if (lines.Count == 0) return;

        var positionIds = lines.Select(l => l.PositionId).Distinct().ToList();
        var positions = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .Where(pos => positionIds.Contains(pos.Id) && pos.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var stamped = DateTime.UtcNow;
        foreach (var position in positions)
        {
            // Highest planned count wins if a budget names a position twice — a budget that
            // contradicts itself should authorise the larger number rather than whichever row the
            // query happened to return last.
            position.ExpectedHeadcount = lines
                .Where(l => l.PositionId == position.Id)
                .Max(l => l.PlannedCount);
            position.EstablishmentApprovedOn = stamped;
            position.EstablishmentSourceBudgetId = budget.Id;
        }

        _logger.LogInformation(
            "Manpower budget {Number} set the establishment for {Count} position(s)",
            budget.BudgetNumber, positions.Count);
    }

    public async Task<bool> RejectAsync(Guid budgetId, string reason, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(budgetId);

        if (await IsBudgetWorkflowConfiguredAsync(cancellationToken))
            throw JobArchitectureException.InvalidState(
                "This tenant approves manpower budgets through the workflow engine (FR-HR-135). "
                + "Reject it from the workflow queue instead.");

        entity.Status = ManpowerBudgetStatus.Rejected;
        // ⚠ This took a reason and threw it away — the budget holder could see it had been refused
        // and had no way to find out why, which makes the rejection unactionable.
        entity.RejectionReason = string.IsNullOrWhiteSpace(reason)
            ? "Rejected without a stated reason."
            : reason.Trim();

        await _budgetRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manpower budget rejected: {BudgetNumber}", entity.BudgetNumber);

        return true;
    }

    // ── The planning baseline (round 2b, R2) ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ManpowerPlanningBaselineDto> GetPlanningBaselineAsync(
        Guid organizationUnitId, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        if (periodEnd < periodStart)
            throw JobArchitectureException.Invalid("The planning period must end after it starts.");

        // The subtree, walked over ParentUnitId in memory: one read of the tenant's units rather
        // than a query per level, and `Path` cannot be trusted on seeded rows (lane B1).
        var units = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable()
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .Select(u => new { u.Id, u.ParentUnitId, u.Name })
            .ToListAsync(cancellationToken);
        var root = units.FirstOrDefault(u => u.Id == organizationUnitId)
            ?? throw JobArchitectureException.NotFound("The organisation unit does not exist in this organisation.");

        var childrenOf = units.Where(u => u.ParentUnitId != null)
            .GroupBy(u => u.ParentUnitId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(u => u.Id).ToList());
        var unitIds = new List<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(root.Id);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (unitIds.Contains(id)) continue;
            unitIds.Add(id);
            if (childrenOf.TryGetValue(id, out var kids))
                foreach (var kid in kids) queue.Enqueue(kid);
        }
        var unitName = units.ToDictionary(u => u.Id, u => u.Name);

        // Everyone on strength in the subtree — one predicate (HrServingEmployees).
        var employees = await _unitOfWork.Repository<Employee>().GetQueryable()
            .AsNoTracking()
            .Include(e => e.Position)
            .Where(e => e.TenantId == tenantId
                     && e.OrganizationUnitId != null && unitIds.Contains(e.OrganizationUnitId.Value))
            .Where(HrServingEmployees.Predicate)
            .ToListAsync(cancellationToken);
        var employeeIds = employees.Select(e => e.Id).ToList();

        // The placement in force today, per person — the same predicate the pay resolvers use.
        var today = DateTime.Today;
        var placements = await _unitOfWork.Repository<EmployeeSalaryAssignment>().GetQueryable()
            .AsNoTracking()
            .Include(a => a.Grade).Include(a => a.Level).Include(a => a.Notch)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted
                     && employeeIds.Contains(a.EmployeeId)
                     && a.WithdrawnAt == null
                     && a.EffectiveDate <= today
                     && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .ToListAsync(cancellationToken);
        var placementOf = placements
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.EffectiveDate).First());

        decimal salaryCost = 0m;
        var withoutPay = 0;
        var payByPosition = new Dictionary<Guid, (decimal Sum, int Count)>();
        foreach (var e in employees)
        {
            placementOf.TryGetValue(e.Id, out var placement);
            var (amount, _) = HrBasicPay.ResolveForPlanning(e, placement);
            if (amount is > 0m)
            {
                salaryCost += amount.Value;
                payByPosition.TryGetValue(e.PositionId, out var acc);
                payByPosition[e.PositionId] = (acc.Sum + amount.Value, acc.Count + 1);
            }
            else withoutPay++;
        }

        // Exits due in the period.
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var todayDate = DateOnly.FromDateTime(today);

        var separations = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted
                     && employeeIds.Contains(s.EmployeeId)
                     && s.Status != SeparationStatus.Cancelled
                     && s.Status != SeparationStatus.Rejected
                     && s.Status != SeparationStatus.Completed)
            .Select(s => new { s.Id, s.EmployeeId, s.SeparationNumber, s.Status, s.LastWorkingDay, s.EffectiveDate, s.InitiatedOn })
            .ToListAsync(cancellationToken);
        var separationOf = separations.GroupBy(s => s.EmployeeId).ToDictionary(g => g.Key, g => g.First());

        var contractEnds = await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsCurrent
                     && employeeIds.Contains(c.EmployeeId)
                     && c.ContractEndDate != null
                     && c.ContractEndDate <= periodEnd)
            .Select(c => new { c.EmployeeId, c.ContractEndDate })
            .ToListAsync(cancellationToken);
        var contractEndOf = contractEnds.GroupBy(c => c.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Min(c => c.ContractEndDate!.Value));

        ManpowerPlanningExitDto Exit(Employee e, string kind, DateOnly? date)
        {
            separationOf.TryGetValue(e.Id, out var sep);
            return new ManpowerPlanningExitDto
            {
                EmployeeId = e.Id,
                EmployeeName = $"{e.FirstName} {e.LastName}".Trim(),
                EmployeeNumber = e.EmployeeNumber,
                PositionId = e.PositionId,
                PositionTitle = e.Position?.Title,
                OrganizationUnitId = e.OrganizationUnitId,
                OrganizationUnitName = e.OrganizationUnitId is { } uid && unitName.TryGetValue(uid, out var n) ? n : null,
                Kind = kind,
                Date = date,
                IsOverdue = date is { } d && d < todayDate,
                HasSeparation = sep != null,
                SeparationNumber = sep?.SeparationNumber,
                SeparationStatus = sep?.Status.ToString(),
            };
        }

        var retirements = new List<ManpowerPlanningExitDto>();
        var expiries = new List<ManpowerPlanningExitDto>();
        var inFlight = new List<ManpowerPlanningExitDto>();
        foreach (var e in employees)
        {
            // A retirement date in the period — or already past for someone still on strength,
            // which is an exit the plan must expect just as much.
            if (HrPolicyCalculations.RetirementDate(settings, e) is { } retire && retire <= periodEnd
                && (retire >= periodStart || retire < todayDate))
                retirements.Add(Exit(e, "Retirement", retire));

            if (contractEndOf.TryGetValue(e.Id, out var end) && (end >= periodStart || end < todayDate))
                expiries.Add(Exit(e, "ContractExpiry", end));

            if (separationOf.TryGetValue(e.Id, out var sep))
                inFlight.Add(Exit(e, "Separation", sep.LastWorkingDay ?? sep.EffectiveDate ?? sep.InitiatedOn));
        }
        var exitingIds = retirements.Concat(expiries).Concat(inFlight).Select(x => x.EmployeeId).Distinct().ToList();

        // Every live post in the subtree, against its establishment.
        var positions = await _unitOfWork.Repository<EmployeePosition>().GetQueryable()
            .AsNoTracking()
            // A position's unit is a non-nullable Guid (an employee's is nullable) - hence the two shapes.
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.IsActive
                     && unitIds.Contains(p.OrganizationUnitId))
            .Select(p => new { p.Id, p.Title, p.Code, p.OrganizationUnitId, p.SalaryGradeId, p.ExpectedHeadcount, p.EstablishmentApprovedOn })
            .ToListAsync(cancellationToken);
        var filledOf = employees.GroupBy(e => e.PositionId).ToDictionary(g => g.Key, g => g.Count());
        var exitsOf = employees.Where(e => exitingIds.Contains(e.Id))
            .GroupBy(e => e.PositionId).ToDictionary(g => g.Key, g => g.Count());

        var positionRows = positions.Select(p =>
        {
            filledOf.TryGetValue(p.Id, out var filled);
            exitsOf.TryGetValue(p.Id, out var exits);
            payByPosition.TryGetValue(p.Id, out var pay);
            var established = p.EstablishmentApprovedOn != null;
            int? gap = established ? Math.Max(0, p.ExpectedHeadcount - filled) : null;
            return new ManpowerPlanningPositionDto
            {
                CurrentSalaryCost = pay.Sum,
                CurrentAverageSalary = pay.Count == 0 ? 0m : Math.Round(pay.Sum / pay.Count, 2),
                PositionId = p.Id,
                Title = p.Title,
                Code = p.Code,
                OrganizationUnitId = p.OrganizationUnitId,
                OrganizationUnitName = unitName.TryGetValue(p.OrganizationUnitId, out var n) ? n : null,
                SalaryGradeId = p.SalaryGradeId,
                Filled = filled,
                ExpectedHeadcount = p.ExpectedHeadcount,
                IsEstablished = established,
                Gap = gap,
                ExitsDue = exits,
                SuggestedNewHires = (gap ?? 0) + exits,
            };
        })
        .OrderBy(p => p.OrganizationUnitName).ThenBy(p => p.Title)
        .ToList();

        return new ManpowerPlanningBaselineDto
        {
            OrganizationUnitId = root.Id,
            OrganizationUnitName = root.Name,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            UnitIds = unitIds,
            CurrentHeadcount = employees.Count,
            CurrentSalaryCost = salaryCost,
            EmployeesWithoutPay = withoutPay,
            SalaryCostNote = withoutPay == 0
                ? "Monthly basic pay of everyone on strength, from their placement on the salary scale (notch, else level mid-point) or the flat figure on their record."
                : $"Monthly basic pay of everyone on strength, from their placement on the salary scale or the flat figure on their record. {withoutPay} of {employees.Count} have no figure on record and contribute nothing — an estimate, not payroll's number.",
            RetirementsDue = retirements.OrderBy(x => x.Date).ToList(),
            ContractExpiriesDue = expiries.OrderBy(x => x.Date).ToList(),
            SeparationsInFlight = inFlight.OrderBy(x => x.Date).ToList(),
            ExitsDueTotal = exitingIds.Count,
            Positions = positionRows,
        };
    }

    // ── From the establishment (round 2b, R4a) ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ManpowerBudgetDetailDto> CreateFromEstablishmentAsync(
        CreateManpowerBudgetFromEstablishmentDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var baseline = await GetPlanningBaselineAsync(dto.OrganizationUnitId, dto.PeriodStart, dto.PeriodEnd, cancellationToken);

        // One live budget per unit and year. A second draft would be two answers to one question;
        // a correction goes into the existing one (Draft/Rejected are editable since R1).
        var clash = await _budgetRepository.GetQueryable().AsNoTracking()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted
                     && b.OrganizationUnitId == dto.OrganizationUnitId && b.FiscalYear == dto.FiscalYear
                     && (b.Status == ManpowerBudgetStatus.Draft || b.Status == ManpowerBudgetStatus.Submitted
                      || b.Status == ManpowerBudgetStatus.UnderReview || b.Status == ManpowerBudgetStatus.Rejected))
            .Select(b => new { b.BudgetNumber, b.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (clash != null)
            throw JobArchitectureException.Conflict(
                $"{baseline.OrganizationUnitName} already has a {clash.Status} budget for {dto.FiscalYear} ({clash.BudgetNumber}). Edit that one, or add posts to it from the establishment.");

        var unit = await _unitOfWork.Repository<OrganizationUnit>().GetQueryable().AsNoTracking()
            .FirstAsync(u => u.Id == dto.OrganizationUnitId && u.TenantId == tenantId, cancellationToken);

        var positions = baseline.Positions.Where(p => dto.IncludeUnestablished || p.IsEstablished).ToList();
        var lines = new List<ManpowerBudgetLine>();
        foreach (var p in positions)
            lines.Add(await BuildLineFromBaselineAsync(p, tenantId, cancellationToken));

        var budget = new ManpowerBudget
        {
            TenantId = tenantId,
            BudgetNumber = await GenerateBudgetNumberAsync(cancellationToken),
            FiscalYear = dto.FiscalYear,
            OrganizationUnitId = unit.Id,
            OrganizationLevelId = unit.OrganizationLevelId,
            Status = ManpowerBudgetStatus.Draft,
            PeriodStartDate = dto.PeriodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEndDate = dto.PeriodEnd.ToDateTime(TimeOnly.MinValue),
            CurrentHeadcount = baseline.CurrentHeadcount,
            CurrentSalaryCost = baseline.CurrentSalaryCost,
            PlannedTerminations = baseline.ExitsDueTotal,
            PlannedHeadcount = lines.Sum(l => l.PlannedCount),
            PlannedNewHires = lines.Sum(l => l.PlannedNewPositions),
            PlannedSalaryCost = lines.Sum(l => l.PlannedTotalCost),
            // The four budgets are the holder's to type: the establishment says how many posts and
            // what they cost on the scale, not what the organisation will spend on benefits,
            // recruitment or training. Zero here is "not yet decided", and the screen says so.
            BusinessJustification = dto.BusinessJustification ??
                $"Drafted from the establishment of {baseline.OrganizationUnitName} on {DateTime.UtcNow:dd MMM yyyy}: " +
                $"{positions.Count} post(s), {positions.Sum(p => p.Gap ?? 0)} below establishment, {baseline.ExitsDueTotal} exit(s) due in the period.",
        };
        budget.TotalBudget = 0m;

        await _budgetRepository.AddAsync(budget);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (var line in lines)
        {
            line.ManpowerBudgetId = budget.Id;
            await _budgetLineRepository.AddAsync(line);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manpower budget {Number} drafted from the establishment of unit {UnitId} with {Lines} line(s)",
            budget.BudgetNumber, unit.Id, lines.Count);

        return await GetDetailByIdAsync(budget.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AddLinesFromEstablishmentResultDto> AddLinesFromEstablishmentAsync(
        Guid budgetId, bool includeUnestablished, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var budget = await GetOwnedBudgetAsync(budgetId);
        if (budget.Status != ManpowerBudgetStatus.Draft && budget.Status != ManpowerBudgetStatus.Rejected)
            throw JobArchitectureException.InvalidState("Posts can be added from the establishment only while the budget is Draft or Rejected.");
        if (budget.OrganizationUnitId is null)
            throw JobArchitectureException.InvalidState("This budget names no organisation unit, so there is no establishment to read.");

        var baseline = await GetPlanningBaselineAsync(budget.OrganizationUnitId.Value,
            DateOnly.FromDateTime(budget.PeriodStartDate), DateOnly.FromDateTime(budget.PeriodEndDate), cancellationToken);
        var existing = await _budgetLineRepository.GetQueryable().AsNoTracking()
            .Where(l => l.ManpowerBudgetId == budget.Id && !l.IsDeleted)
            .Select(l => l.PositionId)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet();

        var result = new AddLinesFromEstablishmentResultDto();
        foreach (var p in baseline.Positions)
        {
            // ⚠ Never overwrites: a line already on the budget is the holder's figure.
            if (existingSet.Contains(p.PositionId)) { result.AlreadyOnBudget++; continue; }
            if (!includeUnestablished && !p.IsEstablished) { result.SkippedUnestablished++; continue; }
            var line = await BuildLineFromBaselineAsync(p, tenantId, cancellationToken);
            line.ManpowerBudgetId = budget.Id;
            await _budgetLineRepository.AddAsync(line);
            result.Added++;
        }
        if (result.Added > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var all = await _budgetLineRepository.GetByBudgetIdAsync(budget.Id);
            budget.PlannedHeadcount = all.Sum(l => l.PlannedCount);
            budget.PlannedNewHires = all.Sum(l => l.PlannedNewPositions);
            budget.PlannedSalaryCost = all.Sum(l => l.PlannedTotalCost);
            await _budgetRepository.UpdateAsync(budget);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        result.Lines = (await _budgetLineRepository.GetByBudgetIdAsync(budget.Id)).Where(l => l.TenantId == tenantId).ToDtoList();
        return result;
    }

    /// <summary>
    /// One line from one baseline row. Posts authorised = filled + gap (the establishment, where
    /// one exists; the people in post where none does); new posts = the gap plus the exits due;
    /// the salary from the post's grade on the scale, else the mean pay of the people in it.
    /// </summary>
    private async Task<ManpowerBudgetLine> BuildLineFromBaselineAsync(
        ManpowerPlanningPositionDto p, Guid tenantId, CancellationToken cancellationToken)
    {
        var line = new ManpowerBudgetLine
        {
            TenantId = tenantId,
            PositionId = p.PositionId,
            CurrentCount = p.IsEstablished ? p.ExpectedHeadcount : p.Filled,
            CurrentFilled = p.Filled,
            CurrentVacant = p.Gap ?? 0,
            CurrentAverageSalary = p.CurrentAverageSalary,
            CurrentTotalCost = p.CurrentSalaryCost,
            PlannedCount = p.Filled + (p.Gap ?? 0),
            PlannedNewPositions = p.SuggestedNewHires,
            PlannedEliminations = 0,
            Priority = BudgetPriority.Medium,
            IsCritical = false,
            Notes = p.IsEstablished
                ? $"From the establishment: {p.Filled} in post of {p.ExpectedHeadcount} authorised, {p.ExitsDue} exit(s) due."
                : $"From the establishment: {p.Filled} in post, not established (no gap can be stated), {p.ExitsDue} exit(s) due.",
        };
        await ApplyLineSalaryAsync(line, p.SalaryGradeId, null, null,
            p.SalaryGradeId is null ? p.CurrentAverageSalary : null, tenantId, cancellationToken);
        return line;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PositionEstablishmentResultDto>> GetEstablishmentListAsync(
        Guid? organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var positions = await _unitOfWork.Repository<EmployeePosition>().GetQueryable().AsNoTracking()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted
                     && (organizationUnitId == null || p.OrganizationUnitId == organizationUnitId.Value))
            .Select(p => new { p.Id, p.Title, p.ExpectedHeadcount, p.EstablishmentApprovedOn, p.EstablishmentSourceBudgetId })
            .ToListAsync(cancellationToken);
        var ids = positions.Select(p => p.Id).ToList();
        // The same count the per-position read and the enforcement paths use.
        var occupied = await _unitOfWork.Repository<Employee>().GetQueryable().AsNoTracking()
            .Where(e => e.TenantId == tenantId && ids.Contains(e.PositionId) && e.IsActive)
            .GroupBy(e => e.PositionId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var sourceIds = positions.Where(p => p.EstablishmentSourceBudgetId != null).Select(p => p.EstablishmentSourceBudgetId!.Value).Distinct().ToList();
        var sources = sourceIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _budgetRepository.GetQueryable().AsNoTracking()
                .Where(b => sourceIds.Contains(b.Id)).Select(b => new { b.Id, b.BudgetNumber })
                .ToDictionaryAsync(b => b.Id, b => b.BudgetNumber, cancellationToken);
        return positions.Select(p =>
        {
            occupied.TryGetValue(p.Id, out var filled);
            return new PositionEstablishmentResultDto
            {
                PositionId = p.Id,
                PositionTitle = p.Title,
                ExpectedHeadcount = p.ExpectedHeadcount,
                CurrentlyFilled = filled,
                EstablishmentApprovedOn = p.EstablishmentApprovedOn,
                EstablishmentSourceBudgetId = p.EstablishmentSourceBudgetId,
                EstablishmentSourceBudgetNumber = p.EstablishmentSourceBudgetId is { } sid && sources.TryGetValue(sid, out var n) ? n : null,
                IsEstablished = p.EstablishmentApprovedOn != null,
            };
        }).OrderBy(r => r.PositionTitle).ToList();
    }

    /// <inheritdoc />
    public async Task<RecruitmentSpendDto> GetRecruitmentSpendAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        var budget = await GetOwnedBudgetAsync(budgetId);
        var settings = await _policyProvider.GetAsync(cancellationToken);
        // Every cost on every requisition drawing down from one of this budget's lines. Cancelled
        // and Rejected requisitions are out of the envelope with their costs; a Rejected COST is
        // out too; a Recorded one is pending.
        var costs = await _unitOfWork.Repository<Entities.HR.Requisition.StaffRequisitionCost>().GetQueryable().AsNoTracking()
            .Where(c => c.TenantId == budget.TenantId && !c.IsDeleted
                     && c.Requisition != null && !c.Requisition.IsDeleted
                     && c.Requisition.Status != StaffRequisitionStatus.Cancelled
                     && c.Requisition.Status != StaffRequisitionStatus.Rejected
                     && c.Requisition.ManpowerBudgetLine != null
                     && c.Requisition.ManpowerBudgetLine.ManpowerBudgetId == budget.Id
                     && c.Status != StaffRequisitionCostStatus.Rejected)
            .Select(c => new
            {
                c.RequisitionId,
                c.Requisition!.RequisitionNumber,
                PositionTitle = c.Requisition.Position != null ? c.Requisition.Position.Title : null,
                c.Status,
                c.AmountBaseCurrency,
            })
            .ToListAsync(cancellationToken);

        var rows = costs.GroupBy(c => new { c.RequisitionId, c.RequisitionNumber, c.PositionTitle })
            .Select(g => new RecruitmentSpendByRequisitionDto
            {
                RequisitionId = g.Key.RequisitionId,
                RequisitionNumber = g.Key.RequisitionNumber,
                PositionTitle = g.Key.PositionTitle,
                Approved = g.Where(c => c.Status == StaffRequisitionCostStatus.Approved).Sum(c => c.AmountBaseCurrency),
                Pending = g.Where(c => c.Status == StaffRequisitionCostStatus.Recorded).Sum(c => c.AmountBaseCurrency),
            })
            .OrderBy(r => r.RequisitionNumber)
            .ToList();

        return new RecruitmentSpendDto
        {
            BudgetId = budget.Id,
            BudgetNumber = budget.BudgetNumber,
            RecruitmentBudget = budget.RecruitmentBudget,
            Approved = rows.Sum(r => r.Approved),
            Pending = rows.Sum(r => r.Pending),
            Mode = settings.BudgetEnforcementMode,
            ByRequisition = rows,
        };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedBudgetAsync(id);

        if (entity.Status == ManpowerBudgetStatus.Approved)
            throw JobArchitectureException.InvalidState("Cannot delete an approved budget.");

        await _budgetRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Manpower budget deleted: {Id}", id);

        return true;
    }

    #region Budget Line Operations

    public async Task<ManpowerBudgetLineDto> AddBudgetLineAsync(CreateManpowerBudgetLineDto createDto, CancellationToken cancellationToken = default)
    {
        await GetOwnedBudgetAsync(createDto.ManpowerBudgetId);
        var tenantId = GetTenantId();
        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        await ApplyLineSalaryAsync(entity, createDto.SalaryGradeId, createDto.SalaryLevelId, createDto.SalaryNotchId,
            createDto.PlannedAverageSalary, tenantId, cancellationToken);

        await _budgetLineRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _budgetLineRepository.GetQueryable()
            .Include(l => l.Position)
            .Include(l => l.SalaryGrade).Include(l => l.SalaryLevel).Include(l => l.SalaryNotch)
            .FirstOrDefaultAsync(l => l.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Budget line added to budget: {BudgetId}", createDto.ManpowerBudgetId);

        return entity!.ToDto();
    }

    public async Task<IEnumerable<ManpowerBudgetLineDto>> GetBudgetLinesAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        await GetOwnedBudgetAsync(budgetId);
        var tenantId = GetTenantId();
        var entities = await _budgetLineRepository.GetByBudgetIdAsync(budgetId);
        var dtos = entities.Where(l => l.TenantId == tenantId).ToDtoList();
        await DecorateDrawdownAsync(dtos, cancellationToken);
        return dtos;
    }

    /// <summary>D-8: posts on live requisitions per line, and what each line has left (round 2b, R5).</summary>
    private async Task DecorateDrawdownAsync(List<ManpowerBudgetLineDto> lines, CancellationToken cancellationToken)
    {
        if (lines.Count == 0) return;
        var ids = lines.Select(l => l.Id).ToList();
        var drawn = await _unitOfWork.Repository<Entities.HR.Requisition.StaffRequisition>().GetQueryable().AsNoTracking()
            .Where(r => r.ManpowerBudgetLineId != null && ids.Contains(r.ManpowerBudgetLineId.Value) && !r.IsDeleted
                     && r.Status != StaffRequisitionStatus.Cancelled && r.Status != StaffRequisitionStatus.Rejected)
            .GroupBy(r => r.ManpowerBudgetLineId!.Value)
            .Select(g => new { LineId = g.Key, Posts = g.Sum(r => r.NumberOfPositions) })
            .ToDictionaryAsync(g => g.LineId, g => g.Posts, cancellationToken);
        foreach (var l in lines)
        {
            drawn.TryGetValue(l.Id, out var posts);
            var budgeted = l.PlannedNewPositions > 0 ? l.PlannedNewPositions : Math.Max(0, l.PlannedCount - l.CurrentFilled);
            l.RequisitionedCount = posts;
            l.Remaining = budgeted - posts;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BudgetLineForRequisitionDto>> GetLinesForPositionAsync(Guid positionId, int? fiscalYear, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var lines = await _budgetLineRepository.GetQueryable().AsNoTracking()
            .Include(l => l.ManpowerBudget).ThenInclude(b => b.OrganizationUnit)
            .Where(l => l.TenantId == tenantId && !l.IsDeleted && l.PositionId == positionId
                     && l.ManpowerBudget != null && !l.ManpowerBudget.IsDeleted
                     && (l.ManpowerBudget.Status == ManpowerBudgetStatus.Approved || l.ManpowerBudget.Status == ManpowerBudgetStatus.Active)
                     && (fiscalYear == null || l.ManpowerBudget.FiscalYear == fiscalYear.Value))
            .ToListAsync(cancellationToken);
        var dtos = lines.Select(l => l.ToDto()).ToList();
        await DecorateDrawdownAsync(dtos, cancellationToken);
        return lines.Select(l =>
        {
            var d = dtos.First(x => x.Id == l.Id);
            return new BudgetLineForRequisitionDto
            {
                LineId = l.Id,
                BudgetId = l.ManpowerBudgetId,
                BudgetNumber = l.ManpowerBudget!.BudgetNumber,
                FiscalYear = l.ManpowerBudget.FiscalYear,
                BudgetStatus = l.ManpowerBudget.Status.ToString(),
                OrganizationUnitName = l.ManpowerBudget.OrganizationUnit?.Name,
                PlannedCount = l.PlannedCount,
                PlannedNewPositions = l.PlannedNewPositions,
                RequisitionedCount = d.RequisitionedCount,
                Remaining = d.Remaining,
                PlannedAverageSalary = l.PlannedAverageSalary,
            };
        }).OrderByDescending(x => x.FiscalYear).ThenBy(x => x.BudgetNumber).ToList();
    }

    public async Task<ManpowerBudgetLineDto> UpdateBudgetLineAsync(UpdateManpowerBudgetLineDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetLineRepository.GetQueryable()
            .Include(l => l.Position)
            .FirstOrDefaultAsync(l => l.Id == updateDto.Id, cancellationToken);

        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Budget line not found");

        updateDto.UpdateEntity(entity);
        await ApplyLineSalaryAsync(entity, updateDto.SalaryGradeId, updateDto.SalaryLevelId, updateDto.SalaryNotchId,
            updateDto.PlannedAverageSalary, entity.TenantId, cancellationToken);

        await _budgetLineRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Budget line updated: {LineId}", updateDto.Id);

        // Re-read with the scale navigations: a changed grade/level/notch id would otherwise be
        // mapped with the OLD rows' names (the F2 shape again).
        entity = await _budgetLineRepository.GetQueryable().AsNoTracking()
            .Include(l => l.Position)
            .Include(l => l.SalaryGrade).Include(l => l.SalaryLevel).Include(l => l.SalaryNotch)
            .FirstAsync(l => l.Id == updateDto.Id, cancellationToken);
        return entity.ToDto();
    }

    /// <summary>
    /// Round 2b, R3 (D-1): checks the place on the scale a line names and resolves its planned
    /// average salary from it — notch amount, else level mid-point, else grade minimum — unless
    /// the caller typed a figure, which is kept and marked <c>Manual</c>. The total is always
    /// average × planned count, so the two cannot drift.
    /// </summary>
    private async Task ApplyLineSalaryAsync(
        ManpowerBudgetLine line, Guid? gradeId, Guid? levelId, Guid? notchId, decimal? typedAmount,
        Guid tenantId, CancellationToken cancellationToken)
    {
        SalaryGrade? grade = null;
        SalaryLevel? level = null;
        SalaryNotch? notch = null;

        if (notchId.HasValue)
        {
            notch = await _unitOfWork.Repository<SalaryNotch>().GetQueryable().AsNoTracking()
                .Include(n => n.Level).ThenInclude(l => l.Grade)
                .FirstOrDefaultAsync(n => n.Id == notchId.Value && n.TenantId == tenantId && !n.IsDeleted, cancellationToken)
                ?? throw JobArchitectureException.Invalid("The salary notch named does not exist in this organisation's scale.");
            level = notch.Level;
            grade = notch.Level.Grade;
            if (levelId.HasValue && levelId.Value != level.Id)
                throw JobArchitectureException.Invalid($"Notch {notch.NotchNumber} is not on the level named; it belongs to level {level.Code} of grade {grade.Code}.");
            if (gradeId.HasValue && gradeId.Value != grade.Id)
                throw JobArchitectureException.Invalid($"Notch {notch.NotchNumber} is not on the grade named; it belongs to grade {grade.Code}.");
        }
        else if (levelId.HasValue)
        {
            level = await _unitOfWork.Repository<SalaryLevel>().GetQueryable().AsNoTracking()
                .Include(l => l.Grade)
                .FirstOrDefaultAsync(l => l.Id == levelId.Value && l.TenantId == tenantId && !l.IsDeleted, cancellationToken)
                ?? throw JobArchitectureException.Invalid("The salary level named does not exist in this organisation's scale.");
            grade = level.Grade;
            if (gradeId.HasValue && gradeId.Value != grade.Id)
                throw JobArchitectureException.Invalid($"Level {level.Code} is not on the grade named; it belongs to grade {grade.Code}.");
        }
        else if (gradeId.HasValue)
        {
            grade = await _unitOfWork.Repository<SalaryGrade>().GetQueryable().AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == gradeId.Value && g.TenantId == tenantId && !g.IsDeleted, cancellationToken)
                ?? throw JobArchitectureException.Invalid("The salary grade named does not exist in this organisation's scale.");
        }

        line.SalaryGradeId = grade?.Id;
        line.SalaryLevelId = level?.Id;
        line.SalaryNotchId = notch?.Id;

        if (typedAmount.HasValue)
        {
            line.PlannedAverageSalary = typedAmount.Value;
            line.PlannedSalarySource = PlannedSalarySource.Manual;
        }
        else if (notch != null)
        {
            line.PlannedAverageSalary = notch.SalaryAmount;
            line.PlannedSalarySource = PlannedSalarySource.Notch;
        }
        else if (level != null)
        {
            line.PlannedAverageSalary = level.MidSalary;
            line.PlannedSalarySource = PlannedSalarySource.LevelMidpoint;
        }
        else if (grade != null)
        {
            line.PlannedAverageSalary = grade.MinSalary;
            line.PlannedSalarySource = PlannedSalarySource.GradeMinimum;
        }
        else
        {
            throw JobArchitectureException.Invalid(
                "Give the line an average salary, or choose the grade (and notch) on the scale it should be read from.");
        }

        line.PlannedTotalCost = line.PlannedAverageSalary * line.PlannedCount;
    }

    /// <inheritdoc />
    public async Task<PositionSalaryReferenceDto> GetPositionSalaryReferenceAsync(Guid positionId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var position = await _unitOfWork.Repository<EmployeePosition>().GetQueryable().AsNoTracking()
            .Include(p => p.SalaryGrade)
            .FirstOrDefaultAsync(p => p.Id == positionId && p.TenantId == tenantId && !p.IsDeleted, cancellationToken)
            ?? throw JobArchitectureException.NotFound("The position does not exist in this organisation.");
        var settings = await _policyProvider.GetAsync(cancellationToken);
        var grade = position.SalaryGrade is { IsDeleted: false } g ? g : null;
        return new PositionSalaryReferenceDto
        {
            PositionId = position.Id,
            PositionTitle = position.Title,
            SalaryGradeId = grade?.Id,
            GradeCode = grade?.Code,
            GradeName = grade?.Name,
            MinSalary = grade?.MinSalary,
            MaxSalary = grade?.MaxSalary,
            Tiers = settings.SalaryStructureTiers,
        };
    }

    public async Task<bool> DeleteBudgetLineAsync(Guid lineId, CancellationToken cancellationToken = default)
    {
        var entity = await _budgetLineRepository.GetByIdAsync(lineId);

        if (entity == null || entity.TenantId != GetTenantId())
            throw JobArchitectureException.NotFound("Budget line not found");

        await _budgetLineRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Budget line deleted: {LineId}", lineId);

        return true;
    }

    public async Task<IEnumerable<ManpowerBudgetLineDto>> GetCriticalPositionsAsync(Guid budgetId, CancellationToken cancellationToken = default)
    {
        await GetOwnedBudgetAsync(budgetId);
        var tenantId = GetTenantId();
        var entities = await _budgetLineRepository.GetCriticalPositionsAsync(budgetId);
        return entities.Where(l => l.TenantId == tenantId).ToDtoList();
    }

    #endregion

    #region Helper Methods

    /// <summary>The next budget number for the current year: <c>MPB-{year}-{seq:D4}</c>.</summary>
    /// <remarks>
    /// ⚠ Rewritten in round 2b, R4a. The old body counted budgets whose <b>fiscal year</b> equalled
    /// the current calendar year and stamped the current year on the number — so every budget for
    /// any other fiscal year got the same <c>MPB-2026-0001</c> (four harness budgets for 2045–2048
    /// did, and the register would have shown it at the demo). Area 17 § 3.6 had recorded that
    /// this generator "counts rows". It now issues one past the highest sequence already used for
    /// the year's prefix, <b>deleted rows included</b> (the lane-4 lesson: a scan that ignores
    /// deleted rows re-issues their numbers). There is no unique index on <c>BudgetNumber</c>; a
    /// sequence service would be the next step if concurrency ever bites.
    /// </remarks>
    private async Task<string> GenerateBudgetNumberAsync(CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var tenantId = GetTenantId();
        var prefix = $"MPB-{year}-";
        var numbers = await _budgetRepository.GetQueryable()
            .IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.BudgetNumber.StartsWith(prefix))
            .Select(b => b.BudgetNumber)
            .ToListAsync(cancellationToken);
        var highest = 0;
        foreach (var n in numbers)
            if (n.Length > prefix.Length && int.TryParse(n[prefix.Length..], out var seq) && seq > highest)
                highest = seq;
        return $"{prefix}{(highest + 1):D4}";
    }

    #endregion
}

#endregion Manpower Budget Service
