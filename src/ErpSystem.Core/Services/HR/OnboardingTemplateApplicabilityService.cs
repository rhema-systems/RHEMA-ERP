using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Which onboarding plan template a hire gets, and why (round 4, lane I4).
/// </summary>
/// <remarks>
/// <para><b>The precedence rule — most specific wins.</b> Each template's inclusive audience rows
/// are matched against the hire's placement and the best match scores the template:</para>
/// <list type="table">
///   <item><term>Position</term><description>100 — the job itself</description></item>
///   <item><term>Organisation unit</term><description>90 for the hire's own unit, one less per level
///   up the tree, never below 51 — so "Estates" beats "Operations" when Estates sits inside
///   Operations, and any unit still beats a level</description></item>
///   <item><term>Organisation level</term><description>40</description></item>
///   <item><term>Location</term><description>30</description></item>
///   <item><term>Everyone</term><description>10</description></item>
///   <item><term>The default template</term><description>0 — the fallback when nothing matches</description></item>
/// </list>
/// <para>An exclusion row that matches removes the template whatever it scored. A tie at the top is
/// resolved by name — deterministic, but flagged <c>IsAmbiguous</c>, because it is a choice HR
/// should make on purpose. A template with no audience rows is never chosen unless it is the
/// default: it is one somebody assigns by hand.</para>
///
/// <para><b>Organisation level, not grade.</b> The plan named a <c>gradeId</c>; grade is payroll's
/// axis and not one the shared audience model has, so a template targets the organisation level
/// instead — the axis every other HR audience already uses.</para>
/// </remarks>
public sealed class OnboardingTemplateApplicabilityService : IOnboardingTemplateApplicabilityService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHrAudienceResolver _audienceResolver;
    private readonly IOnboardingPlanService _planService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<OnboardingTemplateApplicabilityService> _logger;

    public OnboardingTemplateApplicabilityService(
        IUnitOfWork unitOfWork,
        IHrAudienceResolver audienceResolver,
        IOnboardingPlanService planService,
        ICurrentUserProvider currentUserProvider,
        ILogger<OnboardingTemplateApplicabilityService> logger)
    {
        _unitOfWork = unitOfWork;
        _audienceResolver = audienceResolver;
        _planService = planService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    private Guid CurrentTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ====================================================================
    // APPLICABILITY
    // ====================================================================

    public async Task<OnboardingTemplateApplicabilityDto> FindApplicableAsync(
        Guid tenantId, Guid? positionId, Guid? organizationUnitId, Guid? organizationLevelId, Guid? locationId,
        CancellationToken cancellationToken = default)
    {
        var templates = await _unitOfWork.Repository<OnboardingPlanTemplate>().GetQueryable()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.IsActive)
            .Select(t => new { t.Id, t.Name, t.IsDefault })
            .ToListAsync(cancellationToken);

        var result = new OnboardingTemplateApplicabilityDto();
        if (templates.Count == 0)
        {
            result.Reason = "There is no active onboarding plan template.";
            return result;
        }

        var ids = templates.Select(t => t.Id).ToList();
        var audiences = await _unitOfWork.Repository<OnboardingPlanTemplateAudience>().GetQueryable()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted && ids.Contains(a.PlanTemplateId))
            .Select(a => new { a.PlanTemplateId, a.TargetType, a.TargetEntityId, a.IsInclusive })
            .ToListAsync(cancellationToken);

        // The hire's unit, then each unit above it — nearest first, which is what the score falls along.
        var unitChain = organizationUnitId is { } unitId
            ? await _audienceResolver.UnitAncestryAsync(tenantId, unitId, cancellationToken)
            : Array.Empty<Guid>();

        var names = await HrAudienceTargets.ResolveNamesAsync(_unitOfWork, tenantId,
            audiences.Select(a => (a.TargetType, a.TargetEntityId)), cancellationToken);

        // How specifically one audience row matches this placement; null when it does not.
        (int Score, string MatchedOn)? Match(HrAudienceTargetType type, Guid? target)
        {
            switch (type)
            {
                case HrAudienceTargetType.Position when target is { } p && p == positionId:
                    return (100, "Position");
                case HrAudienceTargetType.OrganizationUnit when target is { } u:
                    var distance = unitChain.ToList().IndexOf(u);
                    return distance < 0 ? null : (Math.Max(90 - distance, 51), "OrganizationUnit");
                case HrAudienceTargetType.OrganizationLevel when target is { } l && l == organizationLevelId:
                    return (40, "OrganizationLevel");
                case HrAudienceTargetType.Location when target is { } loc && loc == locationId:
                    return (30, "Location");
                case HrAudienceTargetType.AllEmployees:
                    return (10, "AllEmployees");
                default:
                    return null;
            }
        }

        foreach (var template in templates)
        {
            var rows = audiences.Where(a => a.PlanTemplateId == template.Id).ToList();

            var best = rows.Where(a => a.IsInclusive)
                .Select(a => (Match: Match(a.TargetType, a.TargetEntityId), Row: a))
                .Where(x => x.Match is not null)
                .OrderByDescending(x => x.Match!.Value.Score)
                .FirstOrDefault();

            OnboardingTemplateCandidateDto? candidate = null;
            if (best.Match is { } m)
                candidate = new OnboardingTemplateCandidateDto
                {
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    IsDefault = template.IsDefault,
                    MatchedOn = m.MatchedOn,
                    MatchedTargetName = HrAudienceTargets.Describe(best.Row.TargetType, best.Row.TargetEntityId, names),
                    Specificity = m.Score,
                };
            else if (template.IsDefault)
                candidate = new OnboardingTemplateCandidateDto
                {
                    TemplateId = template.Id,
                    TemplateName = template.Name,
                    IsDefault = true,
                    MatchedOn = "Default",
                    Specificity = 0,
                };

            if (candidate is null) continue;

            var exclusion = rows.Where(a => !a.IsInclusive).FirstOrDefault(a => Match(a.TargetType, a.TargetEntityId) is not null);
            if (exclusion is not null)
            {
                candidate.IsExcluded = true;
                candidate.ExcludedBy = HrAudienceTargets.Describe(exclusion.TargetType, exclusion.TargetEntityId, names);
            }

            result.Candidates.Add(candidate);
        }

        result.Candidates = result.Candidates
            .OrderBy(c => c.IsExcluded)
            .ThenByDescending(c => c.Specificity)
            .ThenBy(c => c.TemplateName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var eligible = result.Candidates.Where(c => !c.IsExcluded).ToList();
        if (eligible.Count == 0)
        {
            result.Reason = result.Candidates.Count == 0
                ? "No template's audience matches this placement and no template is marked default."
                : "Every template that matched is excluded for this placement.";
            return result;
        }

        var chosen = eligible[0];
        result.TemplateId = chosen.TemplateId;
        result.TemplateName = chosen.TemplateName;
        result.MatchedOn = chosen.MatchedOn;
        result.IsAmbiguous = eligible.Count > 1 && eligible[1].Specificity == chosen.Specificity;
        result.Reason = chosen.MatchedOn == "Default"
            ? "The default template — no other template's audience matched."
            : $"Most specific match — {chosen.MatchedTargetName}.";
        if (result.IsAmbiguous)
            result.Reason += $" ⚠ \"{eligible[1].TemplateName}\" matched equally; chosen by name. Narrow one of them.";

        return result;
    }

    public async Task<OnboardingTemplateApplicabilityDto> FindApplicableForEmployeeAsync(
        Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var placement = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted)
            .Select(e => new { e.PositionId, e.OrganizationUnitId, e.OrganizationLevelId, e.LocationId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        return await FindApplicableAsync(tenantId,
            placement.PositionId == Guid.Empty ? null : placement.PositionId,
            placement.OrganizationUnitId, placement.OrganizationLevelId, placement.LocationId, cancellationToken);
    }

    // ====================================================================
    // AUDIENCE ROWS
    // ====================================================================

    private async Task<OnboardingPlanTemplate> GetOwnedTemplateAsync(Guid templateId, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        return await _unitOfWork.Repository<OnboardingPlanTemplate>().GetQueryable()
                   .FirstOrDefaultAsync(t => t.Id == templateId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
               ?? throw new ArgumentException($"Onboarding plan template with ID '{templateId}' not found.");
    }

    public async Task<IReadOnlyList<OnboardingPlanTemplateAudienceDto>> GetAudiencesAsync(
        Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await GetOwnedTemplateAsync(templateId, cancellationToken);

        var rows = await _unitOfWork.Repository<OnboardingPlanTemplateAudience>().GetQueryable()
            .Where(a => a.PlanTemplateId == template.Id && a.TenantId == template.TenantId && !a.IsDeleted)
            .OrderByDescending(a => a.IsInclusive).ThenBy(a => a.TargetType).ThenBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        var names = await HrAudienceTargets.ResolveNamesAsync(_unitOfWork, template.TenantId,
            rows.Select(a => (a.TargetType, a.TargetEntityId)), cancellationToken);

        return rows.Select(a => ToDto(a, names)).ToList();
    }

    public async Task<OnboardingPlanTemplateAudienceDto> AddAudienceAsync(
        Guid templateId, CreateOnboardingPlanTemplateAudienceDto dto, Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var template = await GetOwnedTemplateAsync(templateId, cancellationToken);
        var targetId = HrAudienceTargets.NeedsTarget(dto.TargetType) ? dto.TargetEntityId : null;

        await HrAudienceTargets.ValidateAsync(_unitOfWork, template.TenantId, dto.TargetType, dto.TargetEntityId,
            allowEmployee: false, cancellationToken);

        var repository = _unitOfWork.Repository<OnboardingPlanTemplateAudience>();
        var duplicate = await repository.GetQueryable().AnyAsync(a =>
            a.PlanTemplateId == template.Id && !a.IsDeleted
            && a.TargetType == dto.TargetType && a.TargetEntityId == targetId, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException("This template already has that audience. Remove it first to change whether it includes or excludes.");

        var entity = new OnboardingPlanTemplateAudience
        {
            TenantId = template.TenantId,
            PlanTemplateId = template.Id,
            TargetType = dto.TargetType,
            TargetEntityId = targetId,
            IsInclusive = dto.IsInclusive,
            CreatedById = createdByUserId,
            CreatedBy = createdByUserId.ToString(),
        };
        await repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var names = await HrAudienceTargets.ResolveNamesAsync(_unitOfWork, template.TenantId,
            [(entity.TargetType, entity.TargetEntityId)], cancellationToken);
        return ToDto(entity, names);
    }

    public async Task<bool> RemoveAudienceAsync(
        Guid templateId, Guid audienceId, Guid deletedByUserId, CancellationToken cancellationToken = default)
    {
        var template = await GetOwnedTemplateAsync(templateId, cancellationToken);
        var repository = _unitOfWork.Repository<OnboardingPlanTemplateAudience>();

        var entity = await repository.GetQueryable()
                         .FirstOrDefaultAsync(a => a.Id == audienceId && a.PlanTemplateId == template.Id && !a.IsDeleted, cancellationToken)
                     ?? throw new ArgumentException($"Template audience with ID '{audienceId}' not found.");

        entity.DeletedBy = deletedByUserId.ToString();
        await repository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static OnboardingPlanTemplateAudienceDto ToDto(OnboardingPlanTemplateAudience a, IReadOnlyDictionary<Guid, string> names) => new()
    {
        Id = a.Id,
        PlanTemplateId = a.PlanTemplateId,
        TargetType = a.TargetType,
        TargetEntityId = a.TargetEntityId,
        TargetEntityName = HrAudienceTargets.Describe(a.TargetType, a.TargetEntityId, names),
        IsInclusive = a.IsInclusive,
    };

    // ====================================================================
    // THE HIRE HOOK
    // ====================================================================

    /// <remarks>
    /// ⚠ <b>The coordinator was not set until round 4 lane K-a's first scheduled run showed why it
    /// must be.</b> An onboarding task nobody is assigned to is reminded to the plan's coordinator,
    /// and template tasks name a position, not a person (on UAT not one task had an assignee) — so a
    /// plan created here with no coordinator had reminders nobody received (the run logs them
    /// <c>NotRouted</c>). The
    /// officer who confirmed the start is HR, knows the person has arrived, and is the one name the
    /// system has at this moment; HR can hand the plan to someone else from the plan screen.
    /// </remarks>
    public async Task<Guid?> CreatePlanOnHireAsync(
        Guid tenantId, Guid employeeId, DateOnly startDate, Guid actingUserId, Guid? coordinatorEmployeeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // A plan somebody already made — before the start was confirmed, or for an internal
            // hire who has one from an earlier job — is theirs; the system does not add a second.
            var hasPlan = await _unitOfWork.Repository<OnboardingPlan>().GetQueryable()
                .AnyAsync(p => p.TenantId == tenantId && p.EmployeeId == employeeId && !p.IsDeleted
                            && p.Status != OnboardingStatus.Cancelled, cancellationToken);
            if (hasPlan)
            {
                _logger.LogInformation("Onboarding plan not created on hire for {EmployeeId}: one already exists.", employeeId);
                return null;
            }

            var applicable = await FindApplicableForEmployeeAsync(tenantId, employeeId, cancellationToken);
            if (applicable.TemplateId is not { } templateId)
            {
                _logger.LogInformation("Onboarding plan not created on hire for {EmployeeId}: {Reason}", employeeId, applicable.Reason);
                return null;
            }

            Guid? coordinatorId = null;
            if (coordinatorEmployeeId is { } candidateId)
            {
                var isEmployee = await _unitOfWork.Repository<Employee>().GetQueryable()
                    .AnyAsync(e => e.Id == candidateId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken);
                if (isEmployee) coordinatorId = candidateId;
                else _logger.LogWarning("Onboarding plan for {EmployeeId} created with no coordinator: {CoordinatorId} is not an employee of the tenant.",
                    employeeId, candidateId);
            }

            var created = await _planService.CreateAsync(new CreateOnboardingPlanDto
            {
                EmployeeId = employeeId,
                TemplatePlanId = templateId,
                StartDate = startDate,
                OnboardingCoordinatorId = coordinatorId,
            }, tenantId, actingUserId, cancellationToken);

            // The reason is the system's, not a field a caller of the create endpoint may set, so it
            // is written here rather than carried on the create DTO.
            var plan = await _unitOfWork.Repository<OnboardingPlan>().GetQueryable()
                .FirstAsync(p => p.Id == created.Id, cancellationToken);
            var reason = $"Created on hire from \"{applicable.TemplateName}\". {applicable.Reason}";
            plan.TemplateSelectionReason = reason.Length > 500 ? reason[..500] : reason;
            await _unitOfWork.Repository<OnboardingPlan>().UpdateAsync(plan);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Onboarding plan {PlanId} created on hire for {EmployeeId} from template {TemplateId}.",
                plan.Id, employeeId, templateId);
            return plan.Id;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort: the hire has committed and stands.
            _logger.LogError(ex, "Onboarding plan could not be created on hire for {EmployeeId}.", employeeId);
            _unitOfWork.ClearTrackedChanges();
            return null;
        }
    }
}
