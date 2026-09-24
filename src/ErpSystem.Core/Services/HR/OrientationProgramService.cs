using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Application.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class OrientationProgramService : IOrientationProgramService
{
    private readonly IOrientationProgramRepository _programRepository;
    private readonly IOrientationModuleRepository _moduleRepository;
    private readonly IOrientationContentItemRepository _contentItemRepository;
    private readonly IOrientationPrerequisiteRepository _prerequisiteRepository;
    private readonly IOrientationAudienceRuleRepository _audienceRuleRepository;
    private readonly IOrientationAssessmentQuestionRepository _questionRepository;
    private readonly IOrientationAssessmentOptionRepository _optionRepository;
    private readonly IEmployeeOrientationRepository _enrollmentRepository;
    private readonly IOrientationSessionRepository _sessionRepository;
    private readonly IOrientationEnrollmentTriggerService _triggers;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OrientationProgramService> _logger;

    public OrientationProgramService(
        IOrientationProgramRepository programRepository,
        IOrientationModuleRepository moduleRepository,
        IOrientationContentItemRepository contentItemRepository,
        IOrientationPrerequisiteRepository prerequisiteRepository,
        IOrientationAudienceRuleRepository audienceRuleRepository,
        IOrientationAssessmentQuestionRepository questionRepository,
        IOrientationAssessmentOptionRepository optionRepository,
        IEmployeeOrientationRepository enrollmentRepository,
        IOrientationSessionRepository sessionRepository,
        IOrientationEnrollmentTriggerService triggers,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<OrientationProgramService> logger)
    {
        _programRepository = programRepository;
        _moduleRepository = moduleRepository;
        _contentItemRepository = contentItemRepository;
        _prerequisiteRepository = prerequisiteRepository;
        _audienceRuleRepository = audienceRuleRepository;
        _questionRepository = questionRepository;
        _optionRepository = optionRepository;
        _enrollmentRepository = enrollmentRepository;
        _sessionRepository = sessionRepository;
        _triggers = triggers;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<OrientationProgram> GetOwnedProgramAsync(Guid id)
    {
        var entity = await _programRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation program with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationProgram> GetOwnedProgramWithDetailsAsync(Guid id)
    {
        var entity = await _programRepository.GetWithFullDetailsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation program with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationModule> GetOwnedModuleAsync(Guid id)
    {
        var entity = await _moduleRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation module with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationContentItem> GetOwnedContentItemAsync(Guid id)
    {
        var entity = await _contentItemRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation content item with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationPrerequisite> GetOwnedPrerequisiteAsync(Guid id)
    {
        var entity = await _prerequisiteRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation prerequisite with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationAudienceRule> GetOwnedAudienceRuleAsync(Guid id)
    {
        var entity = await _audienceRuleRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation audience rule with ID '{id}' not found.");
        return entity;
    }

    private async Task<OrientationAssessmentQuestion> GetOwnedQuestionAsync(Guid id)
    {
        var entity = await _questionRepository.GetWithOptionsAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Orientation assessment question with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// Fills ModuleCount / EnrollmentCount / CompletedCount on a set of program summaries with two
    /// grouped queries. List reads deliberately do not include the modules or enrollments collections
    /// — a program's enrollments run to thousands of rows and a list only wants the number — but an
    /// un-included collection is *empty, not null*, so the mapper's `.Count` used to render a confident
    /// 0 on every row while the detail screen for the same program showed the real figure.
    /// </summary>
    private async Task<List<OrientationProgramSummaryDto>> HydrateCountsAsync(
        List<OrientationProgramSummaryDto> summaries, Guid tenantId, CancellationToken cancellationToken)
    {
        if (summaries.Count == 0) return summaries;

        var ids = summaries.Select(s => s.Id).ToList();

        var moduleCounts = await _moduleRepository.GetQueryable()
            .Where(m => m.TenantId == tenantId && ids.Contains(m.ProgramId))
            .GroupBy(m => m.ProgramId)
            .Select(g => new { ProgramId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProgramId, x => x.Count, cancellationToken);

        var enrollmentCounts = await _enrollmentRepository.GetProgramEnrollmentCountsAsync(tenantId, ids);
        var liveContent = await LiveContentCountsAsync(tenantId, ids, cancellationToken);

        foreach (var summary in summaries)
        {
            summary.ModuleCount = moduleCounts.TryGetValue(summary.Id, out var m) ? m : 0;
            summary.CompletesByAttendance = OrientationCompletionRules.CompletesByAttendance(
                liveContent.TryGetValue(summary.Id, out var live) ? live : 0, summary.RequiresAssessment, summary.DefaultDeliveryMode);
            if (enrollmentCounts.TryGetValue(summary.Id, out var e))
            {
                summary.EnrollmentCount = e.Enrolled;
                summary.CompletedCount = e.Completed;
            }
        }

        return summaries;
    }

    /// <summary>
    /// The detail DTO carries the same four count fields, and the full-details read includes neither
    /// Sessions nor Enrollments — so they were 0 on the detail screen too. Counted rather than included
    /// for the same reason: a busy program has thousands of enrollments behind one number.
    /// </summary>
    private async Task<OrientationProgramDto> HydrateDetailCountsAsync(
        OrientationProgramDto dto, Guid tenantId, CancellationToken cancellationToken)
    {
        dto.SessionCount = await _sessionRepository.GetQueryable()
            .CountAsync(s => s.TenantId == tenantId && s.ProgramId == dto.Id, cancellationToken);

        var counts = await _enrollmentRepository.GetProgramEnrollmentCountsAsync(tenantId, new[] { dto.Id });
        if (counts.TryGetValue(dto.Id, out var e))
        {
            dto.EnrollmentCount = e.Enrolled;
            dto.CompletedCount = e.Completed;
        }

        var liveContent = await LiveContentCountsAsync(tenantId, new[] { dto.Id }, cancellationToken);
        dto.CompletesByAttendance = OrientationCompletionRules.CompletesByAttendance(
            liveContent.TryGetValue(dto.Id, out var live) ? live : 0, dto.RequiresAssessment, dto.DefaultDeliveryMode);

        return dto;
    }

    /// <summary>
    /// Live content items per programme: active items on active modules — the same count the completion
    /// gate reads — for <see cref="OrientationCompletionRules.CompletesByAttendance"/> (round 4, lane R).
    /// </summary>
    private async Task<Dictionary<Guid, int>> LiveContentCountsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> programIds, CancellationToken cancellationToken)
    {
        return await _contentItemRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.IsActive
                     && c.Module.IsActive && !c.Module.IsDeleted && programIds.Contains(c.Module.ProgramId))
            .GroupBy(c => c.Module.ProgramId)
            .Select(g => new { ProgramId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProgramId, x => x.Count, cancellationToken);
    }

    // ====================================================================
    // PROGRAM QUERIES
    // ====================================================================

    public async Task<OrientationProgramDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramWithDetailsAsync(id);
        return await HydrateDetailCountsAsync(entity.ToDto(), GetTenantId(), cancellationToken);
    }

    public async Task<OrientationProgramDto?> GetByProgramCodeAsync(string programCode, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _programRepository.GetByProgramCodeAsync(programCode);
        return entity != null && entity.TenantId == tenantId ? entity.ToDto() : null;
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = (await _programRepository.GetAllAsync()).Where(p => p.TenantId == tenantId);
        return await HydrateCountsAsync(entities.ToSummaryDtoList().ToList(), tenantId, cancellationToken);
    }

    public async Task<PagedResult<OrientationProgramSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = _programRepository.GetQueryable().Where(p => p.TenantId == tenantId).Include(p => p.Category);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrientationProgramSummaryDto>
        {
            Items = await HydrateCountsAsync(items.ToSummaryDtoList().ToList(), tenantId, cancellationToken),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetByStatusAsync(OrientationProgramStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateCountsAsync(
            (await _programRepository.GetByStatusAsync(status))
                .Where(p => p.TenantId == tenantId).ToSummaryDtoList().ToList(),
            tenantId, cancellationToken);
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetByCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateCountsAsync(
            (await _programRepository.GetByCategoryAsync(categoryId))
                .Where(p => p.TenantId == tenantId).ToSummaryDtoList().ToList(),
            tenantId, cancellationToken);
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetByTypeAsync(OrientationProgramType programType, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateCountsAsync(
            (await _programRepository.GetByTypeAsync(programType))
                .Where(p => p.TenantId == tenantId).ToSummaryDtoList().ToList(),
            tenantId, cancellationToken);
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetActiveProgramsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateCountsAsync(
            (await _programRepository.GetActiveProgramsAsync())
                .Where(p => p.TenantId == tenantId).ToSummaryDtoList().ToList(),
            tenantId, cancellationToken);
    }

    public async Task<IEnumerable<OrientationProgramSummaryDto>> GetByOwnerOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return await HydrateCountsAsync(
            (await _programRepository.GetByOwnerOrganizationUnitAsync(organizationUnitId))
                .Where(p => p.TenantId == tenantId).ToSummaryDtoList().ToList(),
            tenantId, cancellationToken);
    }

    // ====================================================================
    // PROGRAM CRUD + LIFECYCLE
    // ====================================================================

    public async Task<OrientationProgramDto> CreateAsync(CreateOrientationProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        ValidateLifecycle(createDto.IsRecurring, createDto.RecurrenceFrequency, createDto.EffectiveFrom, createDto.EffectiveTo);
        var entity = createDto.ToEntity(tenantId, createdByUserId);

        entity.ProgramCode = string.IsNullOrWhiteSpace(createDto.ProgramCode)
            ? await GenerateProgramCodeAsync(tenantId, cancellationToken)
            : createDto.ProgramCode.Trim();

        var codeExists = await _programRepository.ProgramCodeExistsAsync(tenantId, entity.ProgramCode);
        if (codeExists)
            throw new InvalidOperationException($"Program code '{entity.ProgramCode}' is already in use.");

        await _programRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation program created: {Code}", entity.ProgramCode);

        return await HydrateDetailCountsAsync(
            (await _programRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto(), tenantId, cancellationToken);
    }

    /// <summary>
    /// Copies a programme and everything it is made of (round 4, lane J2): modules and their content
    /// items, assessment questions and their options, prerequisites and audience rules. The copy is a
    /// <b>Draft</b>, so its audience rules are inert until somebody publishes it — at which point they
    /// fire as the original's would. Sessions and enrolments are deliveries of the original and stay
    /// with it.
    /// </summary>
    /// <remarks>
    /// <para>Mirrors <c>AppraisalTemplateService.CloneAsync</c> — TenantId stamped at every level, one
    /// SaveChanges, a re-read for the navigations — except that each child is added through its own
    /// repository with its key set, the way the pipeline clone does. A child discovered through a
    /// navigation off a parent EF already tracks is taken for an UPDATE of a row that was never
    /// inserted (see the EF graph-write notes); and the source's children are read UNTRACKED, so no
    /// loaded entity can end up in the new graph.</para>
    ///
    /// <para>Retired modules, items and questions come across still retired: the copy shows its
    /// authors exactly what the original showed them. Soft-deleted rows do not come at all. A content
    /// item's resource (a file path or a link) is shared, not duplicated — content is read-only.</para>
    /// </remarks>
    public async Task<OrientationProgramDto> CloneAsync(
        Guid sourceId, CloneOrientationProgramDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var title = dto.NewName?.Trim() ?? string.Empty;
        if (title.Length == 0)
            throw new InvalidOperationException("The copy needs a title.");

        var source = await GetOwnedProgramAsync(sourceId);

        var code = string.IsNullOrWhiteSpace(dto.NewCode)
            ? await GenerateProgramCodeAsync(tenantId, cancellationToken)
            : dto.NewCode.Trim();
        if (await _programRepository.ProgramCodeExistsAsync(tenantId, code))
            throw new InvalidOperationException($"Program code '{code}' is already in use.");

        var modules = await _moduleRepository.GetQueryable().AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ProgramId == sourceId && !m.IsDeleted)
            .OrderBy(m => m.SequenceOrder).ToListAsync(cancellationToken);
        var moduleIds = modules.Select(m => m.Id).ToList();
        var items = await _contentItemRepository.GetQueryable().AsNoTracking()
            .Where(i => i.TenantId == tenantId && moduleIds.Contains(i.ModuleId) && !i.IsDeleted)
            .ToListAsync(cancellationToken);
        var prerequisites = await _prerequisiteRepository.GetQueryable().AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.ProgramId == sourceId && !p.IsDeleted)
            .ToListAsync(cancellationToken);
        var rules = await _audienceRuleRepository.GetQueryable().AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.ProgramId == sourceId && !r.IsDeleted)
            .ToListAsync(cancellationToken);
        var questions = await _questionRepository.GetQueryable().AsNoTracking()
            .Where(q => q.TenantId == tenantId && q.ProgramId == sourceId && !q.IsDeleted)
            .OrderBy(q => q.SequenceOrder).ToListAsync(cancellationToken);
        var questionIds = questions.Select(q => q.Id).ToList();
        var options = await _optionRepository.GetQueryable().AsNoTracking()
            .Where(o => o.TenantId == tenantId && questionIds.Contains(o.QuestionId) && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        var by = createdByUserId.ToString();
        var clone = new OrientationProgram
        {
            TenantId = tenantId,
            ProgramCode = code,
            Title = title,
            Description = source.Description,
            Objectives = source.Objectives,
            CategoryId = source.CategoryId,
            ProgramType = source.ProgramType,
            DefaultDeliveryMode = source.DefaultDeliveryMode,
            Status = OrientationProgramStatus.Draft,
            Priority = source.Priority,
            AudienceScope = source.AudienceScope,
            EstimatedDurationMinutes = source.EstimatedDurationMinutes,
            RequiresAssessment = source.RequiresAssessment,
            PassingScorePercent = source.PassingScorePercent,
            RequiresAcknowledgement = source.RequiresAcknowledgement,
            AcknowledgementTitle = source.AcknowledgementTitle,
            AcknowledgementText = source.AcknowledgementText,
            CompletionDeadlineDays = source.CompletionDeadlineDays,
            IsCertificateIssued = source.IsCertificateIssued,
            CertificateValidityMonths = source.CertificateValidityMonths,
            IsRecurring = source.IsRecurring,
            RecurrenceFrequency = source.RecurrenceFrequency,
            EnableReminders = source.EnableReminders,
            Version = source.Version,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            Tags = source.Tags,
            OwnerEmployeeId = source.OwnerEmployeeId,
            OwnerOrganizationUnitId = source.OwnerOrganizationUnitId,
            CreatedBy = by,
        };
        await _programRepository.AddAsync(clone);

        foreach (var module in modules)
        {
            var moduleCopy = new OrientationModule
            {
                TenantId = tenantId,
                ProgramId = clone.Id,
                Title = module.Title,
                Description = module.Description,
                SequenceOrder = module.SequenceOrder,
                ModuleType = module.ModuleType,
                EstimatedDurationMinutes = module.EstimatedDurationMinutes,
                IsSequentiallyRequired = module.IsSequentiallyRequired,
                IsOptional = module.IsOptional,
                IsActive = module.IsActive,
                CreatedBy = by,
            };
            await _moduleRepository.AddAsync(moduleCopy);

            foreach (var item in items.Where(i => i.ModuleId == module.Id).OrderBy(i => i.SequenceOrder))
            {
                await _contentItemRepository.AddAsync(new OrientationContentItem
                {
                    TenantId = tenantId,
                    ModuleId = moduleCopy.Id,
                    Title = item.Title,
                    Description = item.Description,
                    ContentType = item.ContentType,
                    ResourceUrl = item.ResourceUrl,
                    OriginalFileName = item.OriginalFileName,
                    FileSizeBytes = item.FileSizeBytes,
                    MediaDurationSeconds = item.MediaDurationSeconds,
                    SequenceOrder = item.SequenceOrder,
                    IsRequired = item.IsRequired,
                    IsActive = item.IsActive,
                    CreatedBy = by,
                });
            }
        }

        foreach (var prerequisite in prerequisites)
        {
            await _prerequisiteRepository.AddAsync(new OrientationPrerequisite
            {
                TenantId = tenantId,
                ProgramId = clone.Id,
                PrerequisiteProgramId = prerequisite.PrerequisiteProgramId,
                IsMandatory = prerequisite.IsMandatory,
                Notes = prerequisite.Notes,
                CreatedBy = by,
            });
        }

        foreach (var rule in rules)
        {
            await _audienceRuleRepository.AddAsync(new OrientationAudienceRule
            {
                TenantId = tenantId,
                ProgramId = clone.Id,
                RuleName = rule.RuleName,
                Description = rule.Description,
                TargetType = rule.TargetType,
                TargetEntityId = rule.TargetEntityId,
                Population = rule.Population,
                Trigger = rule.Trigger,
                EnrollmentDelayDays = rule.EnrollmentDelayDays,
                IsInclusive = rule.IsInclusive,
                IsActive = rule.IsActive,
                CreatedBy = by,
            });
        }

        foreach (var question in questions)
        {
            var questionCopy = new OrientationAssessmentQuestion
            {
                TenantId = tenantId,
                ProgramId = clone.Id,
                QuestionText = question.QuestionText,
                QuestionType = question.QuestionType,
                Points = question.Points,
                Explanation = question.Explanation,
                SequenceOrder = question.SequenceOrder,
                IsActive = question.IsActive,
                CreatedBy = by,
            };
            await _questionRepository.AddAsync(questionCopy);

            foreach (var option in options.Where(o => o.QuestionId == question.Id).OrderBy(o => o.DisplayOrder))
            {
                await _optionRepository.AddAsync(new OrientationAssessmentOption
                {
                    TenantId = tenantId,
                    QuestionId = questionCopy.Id,
                    OptionText = option.OptionText,
                    IsCorrect = option.IsCorrect,
                    DisplayOrder = option.DisplayOrder,
                    CreatedBy = by,
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Orientation program copied: {Source} -> {Copy} ({Modules} modules, {Items} items, {Questions} questions, {Rules} rules, {Prerequisites} prerequisites)",
            source.ProgramCode, clone.ProgramCode, modules.Count, items.Count, questions.Count, rules.Count, prerequisites.Count);

        return await HydrateDetailCountsAsync(
            (await _programRepository.GetWithFullDetailsAsync(clone.Id))!.ToDto(), tenantId, cancellationToken);
    }

    public async Task<OrientationProgramDto> UpdateAsync(UpdateOrientationProgramDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(updateDto.Id);
        ValidateLifecycle(updateDto.IsRecurring, updateDto.RecurrenceFrequency, updateDto.EffectiveFrom, updateDto.EffectiveTo);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation program updated: {Code}", entity.ProgramCode);

        return await HydrateDetailCountsAsync(
            (await _programRepository.GetWithFullDetailsAsync(entity.Id))!.ToDto(), entity.TenantId, cancellationToken);
    }

    /// <summary>
    /// Round 4, lane I-b: recurrence and the effective dates now DO something — the nightly sweep
    /// renews on the frequency and nothing enrols outside the dates — so the two ways of saying
    /// something the sweep cannot act on are refused here rather than silently ignored. The form has
    /// always required a frequency; the API did not.
    /// </summary>
    private static void ValidateLifecycle(
        bool isRecurring, OrientationRecurrenceFrequency? frequency, DateTime? effectiveFrom, DateTime? effectiveTo)
    {
        if (isRecurring && frequency is null)
            throw new InvalidOperationException("A recurring programme needs a frequency — how often the next cycle opens.");
        if (frequency is { } f && !Enum.IsDefined(f))
            throw new InvalidOperationException($"'{(int)f}' is not a recurrence frequency.");
        if (effectiveFrom is { } starts && effectiveTo is { } ends && ends.Date < starts.Date)
            throw new InvalidOperationException("The effective-to date is before the effective-from date.");
    }

    public async Task<bool> ChangeStatusAsync(ChangeOrientationProgramStatusDto changeDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(changeDto.ProgramId);
        var wasActive = entity.Status == OrientationProgramStatus.Active;

        entity.Status = changeDto.NewStatus;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedByUserId.ToString();

        await _programRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation program {Code} status changed to {Status}", entity.ProgramCode, changeDto.NewStatus);

        // Round 4, lane I3: publishing is a trigger. The screen has always told HR that making a
        // programme Active means "its audience rules start firing"; until now nothing fired.
        // After the status commit, best-effort — the publish stands whatever the rules do.
        if (!wasActive && entity.Status == OrientationProgramStatus.Active)
            await _triggers.OnProgramPublishedAsync(entity.Id, null, cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedProgramAsync(id);

        if (entity.Status == OrientationProgramStatus.Active)
            throw new InvalidOperationException("An active program cannot be deleted. Suspend or retire it first.");

        await _programRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Orientation program deleted: {Code}", entity.ProgramCode);
        return true;
    }

    // ====================================================================
    // MODULES
    // ====================================================================

    public async Task<OrientationModuleDto> AddModuleAsync(CreateOrientationModuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await EnsureProgramExistsAsync(createDto.ProgramId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        if (entity.SequenceOrder <= 0)
            entity.SequenceOrder = await _moduleRepository.GetMaxSequenceOrderAsync(createDto.ProgramId) + 1;

        await _moduleRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationModuleDto>> GetModulesAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _moduleRepository.GetByProgramIdAsync(programId))
            .Where(m => m.TenantId == tenantId)
            .Select(m => m.ToDto());
    }

    public async Task<OrientationModuleDto> UpdateModuleAsync(UpdateOrientationModuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedModuleAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _moduleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteModuleAsync(Guid moduleId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedModuleAsync(moduleId);

        await _moduleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // CONTENT ITEMS
    // ====================================================================

    public async Task<OrientationContentItemDto> AddContentItemAsync(CreateOrientationContentItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var module = await GetOwnedModuleAsync(createDto.ModuleId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        if (entity.SequenceOrder <= 0)
            entity.SequenceOrder = await _contentItemRepository.GetMaxSequenceOrderAsync(module.Id) + 1;

        await _contentItemRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<IEnumerable<OrientationContentItemDto>> GetContentItemsAsync(Guid moduleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _contentItemRepository.GetByModuleIdAsync(moduleId))
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.ToDto());
    }

    public async Task<OrientationContentItemDto> UpdateContentItemAsync(UpdateOrientationContentItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContentItemAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _contentItemRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> DeleteContentItemAsync(Guid contentItemId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedContentItemAsync(contentItemId);

        await _contentItemRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // PREREQUISITES
    // ====================================================================

    public async Task<OrientationPrerequisiteDto> AddPrerequisiteAsync(CreateOrientationPrerequisiteDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        if (createDto.ProgramId == createDto.PrerequisiteProgramId)
            throw new InvalidOperationException("A program cannot be a prerequisite of itself.");

        await EnsureProgramExistsAsync(createDto.ProgramId);
        await EnsureProgramExistsAsync(createDto.PrerequisiteProgramId);

        if (await _prerequisiteRepository.ExistsAsync(createDto.ProgramId, createDto.PrerequisiteProgramId))
            throw new InvalidOperationException("This prerequisite is already configured for the program.");

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _prerequisiteRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _prerequisiteRepository.FirstOrDefaultAsync(p => p.Id == entity.Id, p => p.PrerequisiteProgram))!.ToDto();
    }

    public async Task<IEnumerable<OrientationPrerequisiteDto>> GetPrerequisitesAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _prerequisiteRepository.GetByProgramIdAsync(programId))
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.ToDto());
    }

    public async Task<bool> DeletePrerequisiteAsync(Guid prerequisiteId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedPrerequisiteAsync(prerequisiteId);

        await _prerequisiteRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // AUDIENCE RULES
    // ====================================================================

    public async Task<OrientationAudienceRuleDto> AddAudienceRuleAsync(CreateOrientationAudienceRuleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await EnsureProgramExistsAsync(createDto.ProgramId);
        await ValidateAudienceRuleAsync(tenantId, createDto.TargetType, createDto.TargetEntityId,
            createDto.Population, createDto.Trigger, createDto.EnrollmentDelayDays, cancellationToken);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _audienceRuleRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await DescribeRulesAsync(tenantId, [entity], cancellationToken))[0];
    }

    public async Task<IEnumerable<OrientationAudienceRuleDto>> GetAudienceRulesAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var rules = (await _audienceRuleRepository.GetByProgramIdAsync(programId))
            .Where(r => r.TenantId == tenantId)
            .ToList();
        return await DescribeRulesAsync(tenantId, rules, cancellationToken);
    }

    public async Task<OrientationAudienceRuleDto> UpdateAudienceRuleAsync(UpdateOrientationAudienceRuleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAudienceRuleAsync(updateDto.Id);
        await ValidateAudienceRuleAsync(entity.TenantId, updateDto.TargetType, updateDto.TargetEntityId,
            updateDto.Population, updateDto.Trigger, updateDto.EnrollmentDelayDays, cancellationToken);

        entity.UpdateEntity(updateDto, updatedByUserId);
        await _audienceRuleRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await DescribeRulesAsync(entity.TenantId, [entity], cancellationToken))[0];
    }

    /// <summary>
    /// Round 4, lane I2: a rule's target must be something the resolver can evaluate. The target id
    /// used to be a free-text GUID box, so a rule could name a unit that did not exist — or a
    /// position's id under "unit" — and silently reach nobody for ever.
    /// </summary>
    private async Task ValidateAudienceRuleAsync(
        Guid tenantId, HrAudienceTargetType targetType, Guid? targetEntityId,
        OrientationAudiencePopulation population, OrientationEnrollmentTrigger trigger, int delayDays,
        CancellationToken cancellationToken)
    {
        await _triggers.ValidateTargetAsync(tenantId, targetType, targetEntityId, allowEmployee: true, cancellationToken);

        if (!Enum.IsDefined(population))
            throw new InvalidOperationException($"'{(int)population}' is not an audience population.");
        if (!Enum.IsDefined(trigger))
            throw new InvalidOperationException($"'{(int)trigger}' is not an enrollment trigger.");
        if (delayDays < 0)
            throw new InvalidOperationException("The enrollment delay cannot be negative.");
        if (delayDays > 0 && !OrientationTriggerWindows.IsDated(trigger))
            throw new InvalidOperationException(
                "A delay only applies to hire, transfer and promotion rules — the others have no date to count from. Set it to 0.");
    }

    /// <summary>
    /// The rules as the list shows them: the target's NAME (§ 3 defect 15 — declared on the DTO and
    /// never mapped, so the list could only ever show a GUID) and how many people each reaches today.
    /// </summary>
    private async Task<List<OrientationAudienceRuleDto>> DescribeRulesAsync(
        Guid tenantId, List<OrientationAudienceRule> rules, CancellationToken cancellationToken)
    {
        var names = await _triggers.ResolveTargetNamesAsync(tenantId,
            rules.Select(r => (r.TargetType, r.TargetEntityId)), cancellationToken);
        var reach = await _triggers.CountRuleReachAsync(tenantId, rules, cancellationToken);

        return rules.Select(r =>
        {
            var dto = r.ToDto();
            dto.TargetEntityName = HrAudienceTargets.Describe(r.TargetType, r.TargetEntityId, names);
            dto.ReachCount = reach.TryGetValue(r.Id, out var n) ? n : null;
            return dto;
        }).ToList();
    }

    public async Task<bool> DeleteAudienceRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAudienceRuleAsync(ruleId);

        await _audienceRuleRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // ASSESSMENT QUESTIONS (+ options)
    // ====================================================================

    public async Task<OrientationAssessmentQuestionDto> AddQuestionAsync(CreateOrientationAssessmentQuestionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await EnsureProgramExistsAsync(createDto.ProgramId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        if (entity.SequenceOrder <= 0)
            entity.SequenceOrder = await _questionRepository.GetMaxSequenceOrderAsync(createDto.ProgramId) + 1;

        await _questionRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await _questionRepository.GetWithOptionsAsync(entity.Id))!.ToDto();
    }

    public async Task<IEnumerable<OrientationAssessmentQuestionDto>> GetQuestionsAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _questionRepository.GetByProgramIdAsync(programId))
            .Where(q => q.TenantId == tenantId)
            .Select(q => q.ToDto());
    }

    public async Task<OrientationAssessmentQuestionDto> UpdateQuestionAsync(UpdateOrientationAssessmentQuestionDto updateDto, Guid tenantId, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = await GetOwnedQuestionAsync(updateDto.Id);

        entity.UpdateEntity(updateDto, updatedByUserId);

        // Replace the option set wholesale: the old options are soft-deleted, the new ones added
        // through the repository with their key set.
        //
        // ⚠ Round 4, lane J found that this path had never once worked. The old options used to be
        // REMOVED from the tracked question's Options collection as well — so that EF's fixup could
        // not put the soft-deleted rows (still tracked, QuestionId intact) back into the response.
        // But Question → Options is a required relationship with DeleteBehavior.Restrict: removing a
        // child from the collection SEVERS it, and EF refuses the whole save ("the association …
        // has been severed"). Every edit of a question that had options failed that way, from the
        // area 15 sweep (2026-08-13) until the lane J harness became the first caller ever to send
        // one. The collection is left alone now; the response's options come from an UNTRACKED read,
        // which fixup cannot reach.
        if (entity.Options.Any())
            await _optionRepository.DeleteRangeAsync(entity.Options.ToList());

        var newOptions = updateDto.Options
            .Select(o => o.ToEntity(tenantId, updatedByUserId))
            .ToList();
        foreach (var opt in newOptions)
            opt.QuestionId = entity.Id;

        if (newOptions.Count > 0)
            await _optionRepository.AddRangeAsync(newOptions);

        await _questionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = entity.ToDto();
        dto.Options = (await _optionRepository.GetQueryable().AsNoTracking()
                .Where(o => o.QuestionId == entity.Id && !o.IsDeleted)
                .OrderBy(o => o.DisplayOrder)
                .ToListAsync(cancellationToken))
            .Select(o => o.ToDto())
            .ToList();
        return dto;
    }

    public async Task<bool> DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedQuestionAsync(questionId);

        if (entity.Options.Any())
            await _optionRepository.DeleteRangeAsync(entity.Options.ToList());

        await _questionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ====================================================================
    // HELPERS
    // ====================================================================

    private async Task EnsureProgramExistsAsync(Guid programId)
    {
        await GetOwnedProgramAsync(programId);
    }

    /// <summary>
    /// Numbers off the highest code ever issued, soft-deleted programs included — the repository method
    /// scans deleted rows. (TenantId, ProgramCode) is UNIQUE and a soft delete does not release the
    /// value, so numbering off live rows alone handed back a code the database still held and the next
    /// create died on a duplicate key.
    /// </summary>
    private async Task<string> GenerateProgramCodeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"ORI-{DateTime.UtcNow.Year}-";
        var max = await _programRepository.GetMaxProgramCodeSequenceAsync(tenantId, prefix);
        return $"{prefix}{(max + 1):D4}";
    }
}
