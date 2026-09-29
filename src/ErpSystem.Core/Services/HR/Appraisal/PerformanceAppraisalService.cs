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

#region Performance Appraisal

public class PerformanceAppraisalService : IPerformanceAppraisalService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalCompetency> _appraisalCompetencyRepository;
    private readonly IGenericRepository<EvaluatorEvaluation> _evaluatorEvaluationRepository;
    private readonly IGenericRepository<AppraisalTemplateItem> _templateItemRepository;
    private readonly IGenericRepository<CriterionScore> _criterionScoreRepository;
    private readonly IGenericRepository<AppraisalEmployeeResponse> _employeeResponseRepository;
    private readonly IGenericRepository<AppraisalCustomQuestionResponse> _customQuestionResponseRepository;
    private readonly IGenericRepository<AppraisalAttachment> _appraisalAttachmentRepository;
    private readonly IGenericRepository<AppraisalCycle> _appraisalCycleRepository;
    private readonly IGenericRepository<AppraisalAppeal> _appealRepository;
    private readonly IGenericRepository<AppraisalAppealItem> _appealItemRepository;
    private readonly IGenericRepository<AppraisalEvaluationSnapshot> _evaluationSnapshotRepository;
    private readonly IGenericRepository<AppraisalCriterionScoreSnapshot> _criterionScoreSnapshotRepository;
    private readonly IGenericRepository<AppraisalKpiEvaluationSnapshot> _kpiEvaluationSnapshotRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfig> _criterionConfigRepository;
    private readonly IGenericRepository<EmployeeGoalAppraisalAssessment> _goalAssessmentRepository;
    private readonly IGenericRepository<AppraisalHRReview> _hrReviewRepository;
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeDefinitionRepository;
    private readonly IAppraisalScoreService _scores;
    private readonly IAppraisalLifecycleService _lifecycle;
    private readonly IAppraisalNotificationService _notifications;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PerformanceAppraisal> _logger;

    public PerformanceAppraisalService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository, 
        IUnitOfWork unitOfWork, 
        ILogger<PerformanceAppraisal> logger, 
        IGenericRepository<Employee> employeeRepository, 
        IGenericRepository<EvaluatorEvaluation> evaluatorEvaluationRepository, 
        IGenericRepository<AppraisalCompetency> appraisalCompetencyRepository, 
        IGenericRepository<AppraisalTemplateItem> templateItemRepository,
        IGenericRepository<CriterionScore> criterionScoreRepository,
        IGenericRepository<AppraisalEmployeeResponse> employeeResponseRepository,
        IGenericRepository<AppraisalCustomQuestionResponse> customQuestionResponseRepository,
        IGenericRepository<AppraisalAttachment> appraisalAttachmentRepository, 
        IGenericRepository<AppraisalCycle> appraisalCycleRepository,
        IGenericRepository<AppraisalAppeal> appealRepository,
        IGenericRepository<AppraisalAppealItem> appealItemRepository,
        IGenericRepository<AppraisalEvaluationSnapshot> evaluationSnapshotRepository,
        IGenericRepository<AppraisalCriterionScoreSnapshot> criterionScoreSnapshotRepository,
        IGenericRepository<AppraisalKpiEvaluationSnapshot> kpiEvaluationSnapshotRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfig> criterionConfigRepository,
        IGenericRepository<EmployeeGoalAppraisalAssessment> goalAssessmentRepository,
        IGenericRepository<AppraisalHRReview> hrReviewRepository,
        IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository,
        IAppraisalScoreService scores,
        IAppraisalLifecycleService lifecycle,
        IAppraisalNotificationService notifications,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUser)
    {
        _appraisalRepository = appraisalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _employeeRepository = employeeRepository;
        _evaluatorEvaluationRepository = evaluatorEvaluationRepository;
        _appraisalCompetencyRepository = appraisalCompetencyRepository;
        _templateItemRepository = templateItemRepository;
        _criterionScoreRepository = criterionScoreRepository;
        _employeeResponseRepository = employeeResponseRepository;
        _customQuestionResponseRepository = customQuestionResponseRepository;
        _appraisalAttachmentRepository = appraisalAttachmentRepository;
        _appraisalCycleRepository = appraisalCycleRepository;
        _appealRepository = appealRepository;
        _appealItemRepository = appealItemRepository;
        _evaluationSnapshotRepository = evaluationSnapshotRepository;
        _criterionScoreSnapshotRepository = criterionScoreSnapshotRepository;
        _kpiEvaluationSnapshotRepository = kpiEvaluationSnapshotRepository;
        _criterionConfigRepository = criterionConfigRepository;
        _goalAssessmentRepository = goalAssessmentRepository;
        _hrReviewRepository = hrReviewRepository;
        _gradeDefinitionRepository = gradeDefinitionRepository;
        _scores = scores;
        _lifecycle = lifecycle;
        _notifications = notifications;
        _currentUserProvider = currentUserProvider;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Raises in-app appraisal notifications without ever failing the operation that produced
    /// them. Every caller here has already saved its work by the time it notifies, so a bad
    /// recipient or a notification write failure must not surface as a 500 on an evaluation
    /// that was in fact submitted. Matches the best-effort pattern in <c>AppraisalCycleService</c>.
    /// </summary>
    private async Task NotifyQuietlyAsync(IEnumerable<AppraisalNotificationRequest> requests, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.RaiseAsync(requests, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to raise appraisal notification(s); the originating action stands.");
        }
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

    // An appraisal owned by another tenant is reported as missing rather than forbidden, so the endpoints do
    // not confirm that the id exists elsewhere.
    private async Task<PerformanceAppraisal> GetOwnedAppraisalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appraisalRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Performance appraisal with ID '{id}' not found.");
        return entity;
    }

    private async Task<AppraisalAppeal> GetOwnedAppealAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _appealRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Appeal with ID '{id}' not found.");
        return entity;
    }

    private IQueryable<PerformanceAppraisal> TenantAppraisalQuery()
    {
        var tenantId = GetTenantId();
        return _appraisalRepository.GetQueryable().Where(a => a.TenantId == tenantId);
    }

    private IQueryable<AppraisalAppeal> TenantAppealQuery()
    {
        var tenantId = GetTenantId();
        return _appealRepository.GetQueryable().Where(a => a.TenantId == tenantId);
    }

    private IQueryable<EvaluatorEvaluation> TenantEvaluationQuery()
    {
        var tenantId = GetTenantId();
        return _evaluatorEvaluationRepository.GetQueryable().Where(e => e.TenantId == tenantId);
    }

    private IQueryable<CriterionScore> TenantCriterionScoreQuery()
    {
        var tenantId = GetTenantId();
        return _criterionScoreRepository.GetQueryable().Where(cs => cs.TenantId == tenantId);
    }

    /// <summary>
    /// Settles the overall score. A delegate kept for the calculate-score route: the arithmetic,
    /// the grade and the talent-pool publish all live in <see cref="IAppraisalScoreService"/>, the
    /// only writer of <c>OverallScore</c> (performance closure lane A).
    /// </summary>
    public async Task<bool> CalculateOverallScoreAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        await _scores.SettleAsync(appraisalId, AppraisalScoreChangeSource.Settle, publish: true, cancellationToken);
        return true;
    }

    /// <summary>
    /// Each criterion's weight in its section as the appraisal was scored: the snapshot's
    /// <c>WeightUsed</c>, not today's template (A12). An appraisal generated before the snapshot
    /// existed reads its template, as its scoring does.
    /// </summary>
    private async Task<Dictionary<Guid, int>> ItemWeightsAsync(PerformanceAppraisal appraisal, CancellationToken cancellationToken)
    {
        var snapshot = await _criterionConfigRepository.GetQueryable()
            .Where(c => c.TenantId == appraisal.TenantId && c.PerformanceAppraisalId == appraisal.Id && c.TemplateItemId != null)
            .Select(c => new { TemplateItemId = c.TemplateItemId!.Value, c.WeightUsed })
            .ToListAsync(cancellationToken);

        if (snapshot.Count > 0)
            return snapshot.GroupBy(c => c.TemplateItemId).ToDictionary(g => g.Key, g => g.First().WeightUsed);

        return await _templateItemRepository.GetQueryable()
            .Where(i => i.Section.AppraisalTemplateId == appraisal.AppraisalTemplateId)
            .ToDictionaryAsync(i => i.Id, i => i.Weight, cancellationToken);
    }

    /// <summary>
    /// Every appraisal read passes through here (performance closure P2): it marks each row with
    /// whether its outcome is released to the appraisee, and withholds the outcome from the
    /// caller's OWN unreleased appraisals — whatever route or policy brought them there, so an
    /// HR officer reading the desk list sees their own appraisal as the appraisee does.
    /// </summary>
    private async Task<List<PerformanceAppraisalDto>> ForViewerAsync(
        List<PerformanceAppraisalDto> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return rows;

        var tenantId = GetTenantId();
        var ids = rows.Select(r => r.Id).Distinct().ToList();
        var facts = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && ids.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.Status,
                a.IsCalibrated,
                Remanded = a.AppealRemandedDate != null,
                a.AppraisalCycle.AppraisalSettings.RequireCalibration,
                a.AppraisalCycle.AppraisalSettings.RequireHRReview,
                HrApproved = a.HRReviews.Any(r => !r.IsDeleted && r.ReviewCompletedDate != null && r.IsApproved),
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var viewer = _currentUser.EmployeeId;
        foreach (var row in rows)
        {
            row.OutcomeReleased = facts.TryGetValue(row.Id, out var f)
                && AppraisalRelease.IsReleased(f.Status, f.IsCalibrated, f.HrApproved, f.Remanded, f.RequireCalibration, f.RequireHRReview);

            if (!row.OutcomeReleased && viewer is Guid me && me == row.EmployeeId)
                AppraisalRelease.WithholdOutcome(row);
        }

        return rows;
    }

    private async Task<PerformanceAppraisalDto> ForViewerAsync(PerformanceAppraisalDto row, CancellationToken cancellationToken)
        => (await ForViewerAsync(new List<PerformanceAppraisalDto> { row }, cancellationToken))[0];

    /// <summary>Grade name for an appraisal's stored grade id, or null when it is ungraded.</summary>
    private async Task<string?> GradeNameAsync(Guid? gradeDefinitionId, CancellationToken cancellationToken)
    {
        if (gradeDefinitionId is not Guid id) return null;

        var tenantId = GetTenantId();
        return await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.Id == id && g.TenantId == tenantId)
            .Select(g => g.GradeName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PerformanceAppraisalDto> CreateAsync(CreatePerformanceAppraisalDto createDto, CancellationToken cancellationToken = default)
    {
        // ── Data integrity: one appraisal per employee per cycle ──────────────
        var tenantId = GetTenantId();
        var duplicate = await _appraisalRepository.ExistsAsync(
            a => a.TenantId == tenantId
              && a.EmployeeId == createDto.EmployeeId
              && a.AppraisalCycleId == createDto.AppraisalCycleId);

        if (duplicate)
            throw new InvalidOperationException(
                "An appraisal already exists for this employee in the specified cycle. " +
                "Only one appraisal per employee per cycle is permitted.");

        var entity = createDto.ToEntity();
        entity.TenantId = tenantId;

        entity.AppraisalNumber = await GenerateAppraisalNumberAsync(createDto.Year, cancellationToken);
        entity.Status = AppraisalStatus.Draft;

        await _appraisalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal created successfully: {appraisalId}", entity.Id);

        // Re-read so the response carries the employee and cycle names. Mapping the tracked
        // entity returns blanks: it was created from a DTO and never loaded with includes.
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAppraisalAsync(id, cancellationToken);

        await _appraisalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal deleted: {id}", id);

        return true;
    }

    public async Task<AppraisalAppealDto> FileAppealAsync(CreateAppraisalAppealDto appealDto, CancellationToken cancellationToken = default)
    {
        var appraisal = await GetOwnedAppraisalAsync(appealDto.PerformanceAppraisalId, cancellationToken);

        // Create new appeal entity
        var appealEntity = new AppraisalAppeal
        {
            TenantId = appraisal.TenantId,
            PerformanceAppraisalId = appealDto.PerformanceAppraisalId,
            EmployeeId = appraisal.EmployeeId,
            SubmittedDate = DateTime.UtcNow,
            AppealReason = appealDto.AppealReason,
            Status = AppraisalAppealStatus.Submitted
        };

        // Add appeal items if provided
        if (appealDto.Items != null && appealDto.Items.Any())
        {
            foreach (var itemDto in appealDto.Items)
            {
                var appealItem = new AppraisalAppealItem
                {
                    TenantId = appraisal.TenantId,
                    TemplateItemId = itemDto.TemplateItemId,
                    Reason = itemDto.Reason
                };
                appealEntity.Items.Add(appealItem);
            }
        }

        // Update appraisal HasAppeal flag
        appraisal.HasAppeal = true;
        appraisal.CurrentAppealStatus = AppraisalAppealStatus.Submitted;

        // Save entities
        await _appealRepository.AddAsync(appealEntity);
        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal filed successfully for appraisal: {appraisalId}", appealDto.PerformanceAppraisalId);

        return appealEntity.ToDto();
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Same NullReferenceException as the paged read had: the DTO dereferences
        // `Employee.FullName` unguarded, so the navigations must be loaded. This also stops the
        // read pulling every tenant's appraisals back before filtering them in memory.
        var entities = await TenantAppraisalQuery()
            .Include(p => p.Employee)
                .ThenInclude(e => e.Department)
            .Include(p => p.Employee)
                .ThenInclude(e => e.Position)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return await ForViewerAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entities = await TenantAppraisalQuery()
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .Where(p => p.EmployeeId == employeeId)
                                                .OrderByDescending(p => p.Year)
                                                .ToListAsync(cancellationToken);

        return await ForViewerAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<PerformanceAppraisalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await TenantAppraisalQuery()
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Department)
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Position)
                                            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (entity == null)
            throw new ArgumentException($"Performance appraisal with ID '{id}' not found.");

        return await ForViewerAsync(entity.ToDto(), cancellationToken);
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByStatusAsync(AppraisalStatus status, CancellationToken cancellationToken = default)
    {
        var entities = await TenantAppraisalQuery().Where(p => p.Status == status)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .ToListAsync(cancellationToken);

        return await ForViewerAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<IEnumerable<PerformanceAppraisalDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var entities = await TenantAppraisalQuery().Where(p => p.Year == year)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Department)
                                                .Include(p => p.Employee)
                                                    .ThenInclude(e => e.Position)
                                                .ToListAsync(cancellationToken);

        return await ForViewerAsync(entities.ToDtoList(), cancellationToken);
    }

    public async Task<PagedResult<PerformanceAppraisalDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = TenantAppraisalQuery();
        var totalCount = await query.CountAsync(cancellationToken);

        // ⚠ Employee, Department and Position have to be included: PerformanceAppraisalDto reads
        // `entity.Employee.FullName` unguarded, so without them this threw a
        // NullReferenceException on every call that returned a row — the endpoint 500'd for any
        // tenant that had ever generated an appraisal. GetAllAsync included them; this did not.
        var items = await query.OrderByDescending(p => p.CreatedAt)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .Include(p => p.Employee)
                                .ThenInclude(e => e.Department)
                            .Include(p => p.Employee)
                                .ThenInclude(e => e.Position)
                            .ToListAsync(cancellationToken);

        var appraisalDtos = await ForViewerAsync(items.ToDtoList(), cancellationToken);

        return new PagedResult<PerformanceAppraisalDto>
        {
            Items = appraisalDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<bool> ResolveAppealAsync(ResolveAppraisalAppealDto resolveDto, CancellationToken cancellationToken = default)
    {
        var appeal = await TenantAppealQuery()
            .Include(a => a.Items)
            .Include(a => a.PerformanceAppraisal)
            .FirstOrDefaultAsync(a => a.Id == resolveDto.AppealId, cancellationToken);

        if (appeal == null)
        {
            throw new ArgumentException($"Appeal with ID '{resolveDto.AppealId}' not found.");
        }

        // Update appeal status and resolution
        appeal.Status = resolveDto.Status;
        appeal.ResolutionNotes = resolveDto.ResolutionNotes;
        appeal.ResolvedDate = DateTime.UtcNow;
        // Note: ReviewedById should be set from the current user context in the controller

        // Update individual appeal items if provided
        if (resolveDto.ItemResolutions != null && resolveDto.ItemResolutions.Any())
        {
            foreach (var itemResolution in resolveDto.ItemResolutions)
            {
                var appealItem = appeal.Items.FirstOrDefault(i => i.Id == itemResolution.AppealItemId);
                if (appealItem != null)
                {
                    appealItem.ResolutionNotes = itemResolution.ResolutionNotes;
                    appealItem.ScoreAdjusted = itemResolution.ScoreAdjusted;
                }
            }
        }

        // Update appraisal CurrentAppealStatus and (optionally) AdjustedScore
        if (appeal.PerformanceAppraisal != null)
        {
            appeal.PerformanceAppraisal.CurrentAppealStatus = resolveDto.Status;

            if (resolveDto.AdjustedScore.HasValue)
                appeal.PerformanceAppraisal.AdjustedScore = resolveDto.AdjustedScore;
        }

        await _appealRepository.UpdateAsync(appeal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal resolved successfully: {appealId}", resolveDto.AppealId);

        return true;
    }

    public async Task<PerformanceAppraisalDto> UpdateAsync(UpdatePerformanceAppraisalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await TenantAppraisalQuery()
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Department)
                                            .Include(p => p.Employee)
                                                .ThenInclude(e => e.Position)
                                            .FirstOrDefaultAsync(p => p.Id == updateDto.Id, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Performance appraisal with ID '{updateDto.Id}' not found.");
        }

        updateDto.UpdateEntity(entity);

        await _appraisalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Performance appraisal updated successfully: {Id}", updateDto.Id);

        return await ForViewerAsync(entity.ToDto(), cancellationToken);
    }

    public async Task<bool> UpdateStatusAsync(UpdateAppraisalStatusDto statusDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAppraisalAsync(statusDto.AppraisalId, cancellationToken);

        // The one lifecycle table (performance closure E1): this method kept an identical copy of
        // the workflow service's, which B1's sync would have made a third.
        AppraisalLifecycle.EnsureTransition(entity.Status, statusDto.Status);

        entity.Status = statusDto.Status;

        await _appraisalRepository.UpdateAsync(entity);

        // Completed by any route settles and publishes in the same save (performance closure A7).
        if (statusDto.Status == AppraisalStatus.Completed)
            await _scores.SettleAsync(entity.Id, AppraisalScoreChangeSource.Settle, publish: true, cancellationToken);
        else
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal status updated successfully: {Id}", statusDto.AppraisalId);

        return true;
    }

    private async Task<string> GenerateAppraisalNumberAsync(int year, CancellationToken cancellationToken)
    {
        var count = await TenantAppraisalQuery().Where(p => p.Year == year)
                                            .CountAsync(cancellationToken);

        return $"APR-{year}-{(count + 1):D5}";
    }

    // ⚠ The raw evaluator-evaluation and criterion-score CRUD (add/read/update/delete, eight routes)
    // was removed in performance closure lane P1 (2026-09-29). No screen called it; its read handed
    // the appraisee every evaluator row — peer names, scores, the manager's notes and recommendation —
    // and its writes could re-parent an evaluation or score it outside the settle path. Evaluations
    // are written by the self, manager and peer forms, and read through their contexts and the HR review.

    #region AppraisalEmployeeResponse Operations

    public async Task<AppraisalEmployeeResponseDto> AddEmployeeResponseAsync(Guid appraisalId, CreateAppraisalEmployeeResponseDto createDto, Guid respondingUserId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle).ThenInclude(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);
        if (appraisal == null)
            throw new ArgumentException("Performance appraisal not found");

        // Gate: employee responses must be enabled in settings.
        if (appraisal.AppraisalCycle?.AppraisalSettings?.AllowEmployeeResponse == false)
            throw new InvalidOperationException("Employee responses are not enabled for this appraisal cycle.");

        // ⚠ This block used to compute `templateItemConfigExists` and then DO NOTHING WITH IT — a
        // validation that reads as present and is dead, with a comment ("FK constraint will also
        // enforce validity") explaining why it was left. The foreign key does enforce validity, but
        // it does so as a 500 out of the generic handler, naming neither the field nor the
        // constraint. Confirmed live by hr-performance/probe-lane3-appraisals.mjs.
        if (createDto.TemplateItemId.HasValue)
        {
            var templateItemConfigExists = await _criterionConfigRepository.ExistsAsync(
                c => c.TenantId == GetTenantId() && c.PerformanceAppraisalId == appraisalId && c.TemplateItemId == createDto.TemplateItemId.Value);

            if (!templateItemConfigExists)
                throw new ArgumentException(
                    $"Template item '{createDto.TemplateItemId.Value}' is not part of this appraisal.");
        }

        var entity = createDto.ToEntity();
        entity.TenantId = appraisal.TenantId;
        entity.AppraisalId = appraisalId;
        // ⚠ AppraisalEmployeeResponse carries NO employee foreign key — there is no RespondedById to
        // stamp, the same position D-16 found on the medical appointment and referral. So the audit
        // column is the only place an actor can go without a migration, and the identity of "the
        // employee" is carried by the ROUTE instead: AddOwnEmployeeResponseAsync refuses anyone but
        // the appraisal's own employee, which is what makes the record mean what it says.
        entity.CreatedBy = respondingUserId.ToString();

        await _employeeResponseRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _employeeResponseRepository.GetQueryable()
            .Include(r => r.TemplateItem)
            .FirstOrDefaultAsync(r => r.Id == entity.Id && r.TenantId == GetTenantId(), cancellationToken);

        _logger.LogInformation("Employee response added successfully: {Id}", entity!.Id);

        return entity!.ToDto();
    }

    /// <summary>
    /// The employee's own written answer to their own appraisal.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>This is what makes the record mean what it says.</b> The response entity has no
    /// employee foreign key, so nothing on the row identifies its author; the author is established
    /// by the route refusing anyone but the appraisal's own employee. The desk route
    /// (<c>AddEmployeeResponseAsync</c>, on the HR write policy) is kept for HR transcribing a
    /// paper response and is <b>deliberately not wired to any screen</b> — the same disposition
    /// D-32 gave the travel desk's alert acknowledgement, and for the same reason: an "employee
    /// response" that HR types is not an employee response.</para>
    ///
    /// <para>Someone else's appraisal is a <b>404, not a 403</b>. A 403 confirms the id exists.</para>
    /// </remarks>
    public async Task<AppraisalEmployeeResponseDto> AddOwnEmployeeResponseAsync(
        Guid appraisalId,
        CreateAppraisalEmployeeResponseDto createDto,
        Guid respondingEmployeeId,
        Guid respondingUserId,
        CancellationToken cancellationToken = default)
    {
        var owns = await TenantAppraisalQuery()
            .AnyAsync(a => a.Id == appraisalId && a.EmployeeId == respondingEmployeeId, cancellationToken);

        if (!owns)
            throw new ArgumentException("Performance appraisal not found");

        return await AddEmployeeResponseAsync(appraisalId, createDto, respondingUserId, cancellationToken);
    }

    public async Task<IEnumerable<AppraisalEmployeeResponseDto>> GetEmployeeResponsesAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entities = await _employeeResponseRepository.GetQueryable(r => r.AppraisalId == appraisalId && r.TenantId == tenantId)
                                                        .Include(r => r.TemplateItem)
                                                        .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    #endregion

    #region AppraisalAttachment Operations

    /// <summary>
    /// Attaches a file to an appraisal.
    ///
    /// <para>Replaces a path that could never have run — see the note on
    /// <c>CheckInService.AddAttachmentAsync</c>. This was the worst of the four: besides the
    /// unset <c>UploadedById</c> (a required Employee FK), it set neither <c>EntityType</c> nor
    /// <c>UploadDate</c>, so a row that somehow survived the FK would have sorted at
    /// <c>0001-01-01</c> under the list read's own ordering.</para>
    /// </summary>
    public async Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid appraisalId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null)
    {
        var tenantId = GetTenantId();
        var appraisalExists = await _appraisalRepository.ExistsAsync(a => a.TenantId == tenantId && a.Id == appraisalId);

        if (!appraisalExists)
            throw new ArgumentException("Performance appraisal not found");

        var entity = new AppraisalAttachment
        {
            TenantId               = tenantId,
            PerformanceAppraisalId = appraisalId,
            EntityType             = AppraisalAttachmentEntityType.PerformanceAppraisal,
            FileName               = fileName,
            FilePath               = string.Empty,
            FileSizeBytes          = fileSizeBytes,
            Description            = description,
            UploadDate             = DateTime.UtcNow,
            UploadedById           = uploadedById,
            FileUploadRecordId     = fileUploadRecordId,
            DocumentRecordId       = documentRecordId,
            DocumentVersionId      = documentVersionId,
        };

        await _appraisalAttachmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _appraisalAttachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(a => a.Id == entity.Id && a.TenantId == tenantId, cancellationToken);

        _logger.LogInformation("Attachment added successfully: {Id}", entity.Id);

        return saved!.ToDto();
    }

    public async Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid appraisalId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _appraisalAttachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .FirstOrDefaultAsync(
                a => a.Id == attachmentId && a.PerformanceAppraisalId == appraisalId && a.TenantId == tenantId,
                cancellationToken);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        // The mapper reads UploadedBy?.FullName, so without this Include every row's uploader
        // column came back blank — the recurring missing-Include shape.
        var entities = await _appraisalAttachmentRepository.GetQueryable()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .Where(a => a.TenantId == tenantId && a.PerformanceAppraisalId == appraisalId)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync(cancellationToken);

        return entities.ToDtoList();
    }

    /// <remarks>
    /// Performance closure P9: an attachment is evidence on the record, so only the person who put
    /// it there, or the HR desk, removes it — and nobody once the appraisal is complete. It was open
    /// to the appraisee, the line manager and any Write holder whoever had uploaded it, and after
    /// completion. An HR officer who is the appraisee is the appraisee here (the two-actor rule).
    /// </remarks>
    public async Task<bool> DeleteAttachmentAsync(Guid appraisalId, Guid attachmentId, bool actorIsDesk, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _appraisalAttachmentRepository.GetQueryable()
            .Include(a => a.PerformanceAppraisal)
            .FirstOrDefaultAsync(
                a => a.Id == attachmentId && a.PerformanceAppraisalId == appraisalId && a.TenantId == tenantId,
                cancellationToken);

        if (entity?.PerformanceAppraisal == null)
            throw new ArgumentException("Attachment not found.");

        if (entity.PerformanceAppraisal.Status is AppraisalStatus.Completed or AppraisalStatus.Closed
            or AppraisalStatus.Withdrawn or AppraisalStatus.Appealed)
            throw new InvalidOperationException(
                "This appraisal is complete, so its attachments are part of the record and cannot be removed.");

        var me = _currentUser.EmployeeId;
        var isUploader = me is Guid uploader && uploader == entity.UploadedById;
        var deskActs = actorIsDesk && !(me is Guid subject && subject == entity.PerformanceAppraisal.EmployeeId);
        if (!isUploader && !deskActs)
            throw new UnauthorizedAccessException("Only the person who attached this file, or HR, can remove it.");

        await _appraisalAttachmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Get all appraisals for a specific employee, including their role and pending actions
    /// </summary>
    public async Task<IEnumerable<MyAppraisalDto>> GetMyAppraisalsAsync(Guid employeeId, string? cycleFilter = null, CancellationToken cancellationToken = default)
    {
        // Get all appraisals where the employee is the subject
        var appraisalsQuery = TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.EvaluatorEvaluations)
            .Include(a => a.HRReviews)
            .Where(a => a.EmployeeId == employeeId);

        // P2: the score is the employee's to see only once released; HR or the manager reading this
        // list for someone else sees it as before.
        var viewerIsSubject = _currentUser.EmployeeId is Guid viewer && viewer == employeeId;

        // Apply cycle filter if provided
        if (!string.IsNullOrEmpty(cycleFilter))
        {
            switch (cycleFilter.ToLower())
            {
                case "open":
                    appraisalsQuery = appraisalsQuery.Where(a => 
                        a.Status == AppraisalStatus.Active ||
                        a.Status == AppraisalStatus.Draft);
                    break;
                case "completed":
                    appraisalsQuery = appraisalsQuery.Where(a => a.Status == AppraisalStatus.Completed);
                    break;
            }
        }

        var appraisals = await appraisalsQuery
            .OrderByDescending(a => a.Year)
            .ThenByDescending(a => a.StartDate)
            .ToListAsync(cancellationToken);

        var result = new List<MyAppraisalDto>();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var states = await _lifecycle.GetStatesAsync(appraisals.Select(a => a.Id).ToList(), cancellationToken);

        foreach (var appraisal in appraisals)
        {
            // Every row here is the employee's own appraisal — the query is filtered on
            // EmployeeId, and an employee cannot be nominated as a peer on themselves, so the
            // peer branch that used to live here could never match. Peer work an employee owes
            // on *other people's* appraisals is a separate list, from
            // IPeerEvaluationService.GetPeerEvaluationAssignmentsAsync, and is kept separate
            // deliberately: those rows carry another employee's scores.
            const string role = "Self";

            // Check self-evaluation completion
            var selfEvaluation = appraisal.EvaluatorEvaluations
                .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
            bool selfEvaluationComplete = selfEvaluation?.SubmittedDate != null;
            const bool peerEvaluationComplete = false;

            // Determine action required and action text
            bool actionRequired = false;
            string? actionText = null;
            DateOnly? dueDate = null;
            bool isOverdue = false;

            var settings = appraisal.AppraisalCycle?.AppraisalSettings;
            var cycle = appraisal.AppraisalCycle;
            states.TryGetValue(appraisal.Id, out var state);

            // What the row asks of the employee is the step the gates put the appraisal at — the
            // step the write will be held to (performance closure B1). It read the major status,
            // so it said "Acknowledge appraisal" from the moment the manager submitted, while the
            // acknowledgment was refused until HR had signed off, and "Complete self-evaluation"
            // while goals or peer nominations still held the appraisal back.
            if (state != null)
            {
                switch (state.SubStatus)
                {
                    case AppraisalSubStatus.GoalSetting:
                        actionRequired = true;
                        actionText = $"Goal setting — {state.Block.Reason}";
                        dueDate = cycle?.GoalSettingDeadline;
                        break;

                    // In Manager mode the manager chooses the peers: nothing is the employee's to do.
                    case AppraisalSubStatus.PeerNomination when state.Settings.PeerNominationMode != PeerNominationMode.Manager:
                        actionRequired = true;
                        actionText = "Nominate your peer reviewers";
                        dueDate = cycle?.PeerNominationDeadline;
                        break;

                    case AppraisalSubStatus.SelfEvaluation:
                        actionRequired = true;
                        actionText = "Complete self-evaluation";
                        dueDate = cycle?.SelfEvaluationDeadline;
                        break;

                    case AppraisalSubStatus.PendingAcknowledgment:
                        actionRequired = true;
                        actionText = "Acknowledge appraisal";
                        dueDate = cycle?.EmployeeAcknowledgeDeadline ?? today.AddDays(7);
                        break;
                }
            }

            isOverdue = actionRequired && dueDate.HasValue && dueDate.Value < today;

            // One appeal rule for the list, the appeal page and the submit (B1): the window runs
            // from the acknowledgment (else HR's sign-off, else completion), not from the cycle's end.
            bool canFileAppeal = state != null && AppraisalGates.CanFileAppeal(state.Facts, state.Settings, now).Allowed;

            var released = AppraisalRelease.IsReleased(
                appraisal.Status,
                appraisal.IsCalibrated,
                appraisal.HRReviews.Any(r => r.ReviewCompletedDate != null && r.IsApproved),
                appraisal.AppealRemandedDate != null,
                settings?.RequireCalibration ?? false,
                settings?.RequireHRReview ?? false);

            result.Add(new MyAppraisalDto
            {
                AppraisalId = appraisal.Id,
                AppraisalNumber = appraisal.AppraisalNumber,
                AppraisalCycleId = appraisal.AppraisalCycleId,
                AppraisalCycleName = appraisal.AppraisalCycle?.CycleName ?? "N/A",
                Year = appraisal.Year,
                PeriodStart = appraisal.StartDate,
                PeriodEnd = appraisal.EndDate,
                Status = appraisal.Status,
                MyRole = role,
                ActionRequired = actionRequired,
                ActionText = actionText,
                DueDate = dueDate,
                OverallScore = released || !viewerIsSubject ? appraisal.OverallScore : null,
                OutcomeReleased = released,
                IsAcknowledged = appraisal.EmployeeAcknowledgedDate.HasValue,
                AppealFiled = appraisal.HasAppeal,
                AppealStatus = appraisal.CurrentAppealStatus,
                CanFileAppeal = canFileAppeal,
                SelfEvaluationComplete = selfEvaluationComplete,
                PeerEvaluationComplete = peerEvaluationComplete,
                IsOverdue = isOverdue
            });
        }

        return result;
    }

    #endregion

    #region Self-Evaluation Operations

    /// <summary>
    /// Gets the complete context for an employee's self-evaluation page
    /// Includes appraisal info, KPIs with targets, grade ranges, and existing evaluations
    /// </summary>
    public async Task<SelfEvaluationContextDto> GetSelfEvaluationContextAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        // Get appraisal with all related data
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Self))
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.GradeRanges)
                    .ThenInclude(gr => gr.GradeDefinition)
            .Include(a => a.Template)
                .ThenInclude(t => t.Sections)
                    .ThenInclude(s => s.TemplateItems)
            .Include(a => a.CustomQuestionResponses)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found");

        // Check if employee is authorized (basic check)
        // Additional authorization should be done at controller/API level

        // Get employee's KPI targets are now managed via EmployeeGoal - not loaded here.

        // Get existing self-evaluation if it exists
        var selfEvaluation = appraisal.EvaluatorEvaluations
            .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);

        bool isSelfEvaluationSubmitted = selfEvaluation?.SubmittedDate.HasValue ?? false;
        // Editable if not submitted and status is Open or SelfEvaluation
        bool isEditable = !isSelfEvaluationSubmitted && 
            (appraisal.Status == AppraisalStatus.Active || appraisal.Status == AppraisalStatus.Draft);
        
        // Get soft skill self-rating flag
        bool allowSelfSoftSkillRating = appraisal.AppraisalCycle?.AppraisalSettings?.AllowSelfSoftSkillRating ?? false;

        return new SelfEvaluationContextDto
        {
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            EmployeeNumber = appraisal.Employee.EmployeeNumber,
            AppraisalCycleName = appraisal.AppraisalCycle?.CycleName ?? "N/A",
            PeriodStart = appraisal.StartDate,
            PeriodEnd = appraisal.EndDate,
            Status = appraisal.Status,
            IsSelfEvaluationSubmitted = isSelfEvaluationSubmitted,
            SelfEvaluationSubmittedDate = selfEvaluation?.SubmittedDate,
            IsEditable = isEditable,
            SelfEvaluationDeadline = appraisal.AppraisalCycle?.SelfEvaluationDeadline,
            AllowSelfSoftSkillRating = allowSelfSoftSkillRating,
            Settings = appraisal.AppraisalCycle?.AppraisalSettings?.ToDto(),
            Sections = BuildSelfEvaluationSections(appraisal, selfEvaluation, appraisal.CustomQuestionResponses)
        };
    }

    /// <summary>
    /// Saves or updates self-evaluation (draft or submission)
    /// </summary>
    public async Task<SelfEvaluationResultDto> SaveSelfEvaluationAsync(SaveSelfEvaluationDto saveDto, CancellationToken cancellationToken = default)
    {
        // Get appraisal
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Self))
            .Include(a => a.PeerNominations)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == saveDto.AppraisalId, cancellationToken);

        if (appraisal == null)
            return new SelfEvaluationResultDto { Success = false, Message = "Appraisal not found" };

        // Validate employee
        if (appraisal.EmployeeId != saveDto.EmployeeId)
            return new SelfEvaluationResultDto { Success = false, Message = "Employee mismatch" };

        // Check if editable
        var existingSelfEval = appraisal.EvaluatorEvaluations
            .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);

        if (existingSelfEval?.SubmittedDate.HasValue ?? false)
            return new SelfEvaluationResultDto { Success = false, Message = "Self-evaluation already submitted" };

        if (appraisal.Status != AppraisalStatus.Active && appraisal.Status != AppraisalStatus.Draft)
            return new SelfEvaluationResultDto { Success = false, Message = "Appraisal is not open for self-evaluation" };

        // Every score on its item's own scale (drafts and submissions alike) — A11.
        var scaleError = await _scores.ValidateItemScoresAsync(appraisal.Id, saveDto.ItemScores, cancellationToken);
        if (scaleError != null)
            return new SelfEvaluationResultDto { Success = false, Message = scaleError };

        // Validate all KPIs are evaluated if submitting (not draft)
        if (!saveDto.IsDraft)
        {
            // The submission is the self-evaluation step's: goals agreed, the setup conversations held
            // and the peer nominations in, as the cycle requires (performance closure B1). A draft can
            // be saved at any point. Refused with a 422 that names the step the appraisal is at.
            await _lifecycle.EnsureAtAsync(appraisal.Id, "The self-evaluation cannot be submitted yet",
                [AppraisalSubStatus.SelfEvaluation], cancellationToken);

            // ItemScores only contains scored items (client filters out unscored ones before sending).
            // A score row with both values null should never arrive; treat it as incomplete.
            if (saveDto.ItemScores.Any(e => !e.ActualValue.HasValue && !e.NumericScore.HasValue))
                return new SelfEvaluationResultDto { Success = false, Message = "One or more scored items are missing a value. Please complete all evaluations before submitting." };

            // Enforce peer nominations when required and employee-driven nomination mode
            var settings = appraisal.AppraisalCycle?.AppraisalSettings;
            if (settings != null && settings.RequirePeerReviews && settings.PeerNominationMode == PeerNominationMode.Employee)
            {
                var nominatedPeers = appraisal.PeerNominations?.Count ?? 0;
                if (nominatedPeers < settings.MinPeerEvaluators || nominatedPeers > settings.MaxPeerEvaluators)
                {
                    return new SelfEvaluationResultDto
                    {
                        Success = false,
                        Message = $"Peer reviews are required. Please nominate at least {settings.MinPeerEvaluators} and no more than {settings.MaxPeerEvaluators} peers before submitting."
                    };
                }
            }

            // Enforce competency/soft-skill completion when enabled
            var allowSelfSoftSkillRating = appraisal.AppraisalCycle?.AppraisalSettings?.AllowSelfSoftSkillRating ?? false;
            if (allowSelfSoftSkillRating && appraisal.Employee.PositionId != Guid.Empty)
            {
                var requiredCompetencyIds = await _templateItemRepository.GetQueryable()
                    .Include(i => i.Section)
                    .Where(i => i.Section.AppraisalTemplateId == appraisal.AppraisalTemplateId)
                    .Where(i => i.CompetencyId != null)
                    .Select(i => i.Id)
                    .ToListAsync(cancellationToken);

                if (requiredCompetencyIds.Any())
                {
                    var submittedCompetencyIds = saveDto.ItemScores
                        .Where(r => r.NumericScore.HasValue)
                        .Select(r => r.TemplateItemId)
                        .ToHashSet();

                    var missing = requiredCompetencyIds
                        .Where(id => !submittedCompetencyIds.Contains(id))
                        .ToList();
                    if (missing.Any())
                    {
                        return new SelfEvaluationResultDto
                        {
                            Success = false,
                            Message = "Please rate all competencies/soft skills before submitting."
                        };
                    }
                }
            }
        }

        try
        {
            // Create or update EvaluatorEvaluation
            EvaluatorEvaluation evaluatorEvaluation;

            if (existingSelfEval == null)
            {
                // Create new self-evaluation
                var settings = appraisal.AppraisalCycle.AppraisalSettings;
                
                evaluatorEvaluation = new EvaluatorEvaluation
                {
                    TenantId = appraisal.TenantId,
                    AppraisalId = appraisal.Id,
                    EvaluatorId = saveDto.EmployeeId,
                    EvaluatorRole = EvaluatorRole.Self,
                    EvaluatorWeight = settings.SelfEvaluationWeight,
                    StartedDate = DateTime.UtcNow, // Set on first save (draft or submit)
                    SubmittedDate = saveDto.IsDraft ? null : DateTime.UtcNow
                };
                
                await _evaluatorEvaluationRepository.AddAsync(evaluatorEvaluation);
            }
            else
            {
                evaluatorEvaluation = existingSelfEval;
                
                // Set StartedDate if not already set (in case first save was missed)
                if (!evaluatorEvaluation.StartedDate.HasValue)
                {
                    evaluatorEvaluation.StartedDate = DateTime.UtcNow;
                }
                
                if (!saveDto.IsDraft)
                {
                    evaluatorEvaluation.SubmittedDate = DateTime.UtcNow;
                }
            }

            // Save or update scores (KPI and competency items unified under ItemScores)
            var allowSelfSoftSkillRating = appraisal.AppraisalCycle?.AppraisalSettings?.AllowSelfSoftSkillRating ?? false;

            // One query for the whole form, not three or four per item. Declared out here because
            // the submit branch below reuses it to recompute the total.
            var scoring = await _scores.LoadScoringAsync(appraisal.Id, cancellationToken);

            if (saveDto.ItemScores.Any())
            {
                // Need to save changes first to get the evaluatorEvaluation.Id if it's new
                if (existingSelfEval == null)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                foreach (var itemInput in saveDto.ItemScores)
                {
                    // Check if criterion score already exists
                    var existingScore = await TenantCriterionScoreQuery()
                        .FirstOrDefaultAsync(cs => cs.EvaluatorEvaluationId == evaluatorEvaluation.Id
                                                 && cs.TemplateItemId == itemInput.TemplateItemId,
                                           cancellationToken);

                    if (existingScore == null)
                    {
                        // Create new criterion score
                        var newScore = new CriterionScore
                        {
                            TenantId = appraisal.TenantId,
                            EvaluatorEvaluationId = evaluatorEvaluation.Id,
                            TemplateItemId = itemInput.TemplateItemId,
                            NumericScore = itemInput.NumericScore,
                            ActualValue  = itemInput.ActualValue,
                            Notes = itemInput.Notes
                        };

                        // Calculate WeightedScore immediately per spec
                        await _scores.ScoreCriterionAsync(newScore, scoring, cancellationToken);

                        await _criterionScoreRepository.AddAsync(newScore);
                    }
                    else
                    {
                        // Update existing score
                        existingScore.NumericScore = itemInput.NumericScore;
                        existingScore.ActualValue  = itemInput.ActualValue;
                        existingScore.Notes = itemInput.Notes;

                        // Recalculate WeightedScore
                        await _scores.ScoreCriterionAsync(existingScore, scoring, cancellationToken);

                        await _criterionScoreRepository.UpdateAsync(existingScore);
                    }
                }
            }

            // Calculate and set TotalScore only when submitting (not draft)
            if (!saveDto.IsDraft)
            {
                // Need to reload the evaluator evaluation with all criterion scores to calculate TotalScore
                var evalWithScores = await TenantEvaluationQuery()
                    .Include(e => e.CriterionScores)
                    .FirstOrDefaultAsync(e => e.Id == evaluatorEvaluation.Id, cancellationToken);
                
                if (evalWithScores != null && evalWithScores.CriterionScores.Any())
                {
                    // A weighted mean over what was scored, not a bare sum — see AppraisalScoring.
                    evalWithScores.TotalScore = await _scores.ScoreEvaluatorAsync(
                        evalWithScores.CriterionScores, scoring, cancellationToken);
                    await _evaluatorEvaluationRepository.UpdateAsync(evalWithScores);
                }

                // The first save opens a Draft appraisal. Where it goes from there is the gates'
                // call, made once this is saved (the sync below).
                if (appraisal.Status == AppraisalStatus.Draft)
                    appraisal.Status = AppraisalStatus.Active;
            }
            else
            {
                // Update status when saving as draft (from Open to SelfEvaluation)
                var settings = appraisal.AppraisalCycle.AppraisalSettings;
                var draftStatus = UpdateStatusOnDraft(appraisal.Status, EvaluatorRole.Self, settings);
                if (draftStatus != appraisal.Status)
                {
                    appraisal.Status = draftStatus;
                    await _appraisalRepository.UpdateAsync(appraisal);
                }
            }

            // Save custom question responses
            if (saveDto.CustomQuestionResponses.Any())
            {
                foreach (var input in saveDto.CustomQuestionResponses)
                {
                    var existingResp = await _customQuestionResponseRepository.GetQueryable()
                        .FirstOrDefaultAsync(r => r.PerformanceAppraisalId == appraisal.Id
                                               && r.TemplateItemId == input.TemplateItemId,
                                           cancellationToken);

                    if (existingResp == null)
                    {
                        await _customQuestionResponseRepository.AddAsync(new AppraisalCustomQuestionResponse
                        {
                            TenantId = appraisal.TenantId,
                            PerformanceAppraisalId = appraisal.Id,
                            TemplateItemId         = input.TemplateItemId,
                            ResponseText           = input.ResponseText,
                            IsDraft                = saveDto.IsDraft,
                            SubmittedDate          = saveDto.IsDraft ? null : DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existingResp.ResponseText  = input.ResponseText;
                        existingResp.IsDraft        = saveDto.IsDraft;
                        if (!saveDto.IsDraft)
                            existingResp.SubmittedDate = DateTime.UtcNow;
                        await _customQuestionResponseRepository.UpdateAsync(existingResp);
                    }
                }
            }

            // Upsert goal appraisal assessments (self-assessment fields)
            if (saveDto.GoalAssessments != null && saveDto.GoalAssessments.Count > 0)
            {
                var goalLookup = appraisal.Goals?.ToDictionary(g => g.Id) ?? new Dictionary<Guid, EmployeeGoal>();

                foreach (var ga in saveDto.GoalAssessments)
                {
                    // Derive FinalProgressPercent server-side for KPI goals so the stored
                    // value always reflects the goal's measurement semantics rather than
                    // whatever the client slider happened to be set to.
                    decimal? computedProgress = ga.FinalProgressPercent;
                    if (goalLookup.TryGetValue(ga.GoalId, out var goal)
                        && goal.KpiDefinitionId.HasValue
                        && goal.MeasurementType != MeasurementType.Boolean
                        && ga.FinalActualValue.HasValue)
                    {
                        computedProgress = CalculateKpiAchievement(
                            ga.FinalActualValue.Value, goal.TargetValue, goal.MinValue, goal.MaxValue);
                    }

                    var existing = await _goalAssessmentRepository.GetQueryable()
                        .FirstOrDefaultAsync(a => a.EmployeeGoalId == ga.GoalId
                                               && a.PerformanceAppraisalId == appraisal.Id,
                                           cancellationToken);

                    if (existing == null)
                    {
                        await _goalAssessmentRepository.AddAsync(new EmployeeGoalAppraisalAssessment
                        {
                            EmployeeGoalId          = ga.GoalId,
                            PerformanceAppraisalId  = appraisal.Id,
                            TenantId                = appraisal.TenantId,
                            SelfFinalProgressPercent = computedProgress,
                            SelfFinalStatus         = ga.FinalStatus,
                            SelfFinalActualValue    = ga.FinalActualValue,
                            SelfAssessmentNotes     = ga.AssessmentNotes,
                            SelfEvidenceLinks       = ga.EvidenceLinks
                        });
                    }
                    else
                    {
                        existing.SelfFinalProgressPercent = computedProgress;
                        existing.SelfFinalStatus         = ga.FinalStatus;
                        existing.SelfFinalActualValue    = ga.FinalActualValue;
                        existing.SelfAssessmentNotes     = ga.AssessmentNotes;
                        existing.SelfEvidenceLinks       = ga.EvidenceLinks;
                        await _goalAssessmentRepository.UpdateAsync(existing);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (!saveDto.IsDraft)
            {
                await _lifecycle.SyncAsync(appraisal.Id, cancellationToken: cancellationToken);
                await NotifySelfEvaluationSubmittedAsync(appraisal, cancellationToken);
            }

            return new SelfEvaluationResultDto
            {
                Success = true,
                Message = saveDto.IsDraft ? "Self-evaluation saved as draft" : "Self-evaluation submitted successfully",
                EvaluatorEvaluationId = evaluatorEvaluation.Id,
                SubmittedDate = evaluatorEvaluation.SubmittedDate
            };
        }
        catch (Exception ex)
        {
            return new SelfEvaluationResultDto
            {
                Success = false,
                Message = $"Error saving self-evaluation: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Tells the manager their report's self-evaluation is in, and — when the cycle lets peers
    /// start only after self-evaluation — nudges the approved peers that their form is now open.
    /// </summary>
    private async Task NotifySelfEvaluationSubmittedAsync(PerformanceAppraisal appraisal, CancellationToken cancellationToken)
    {
        var cycleName = appraisal.AppraisalCycle?.CycleName;
        var employeeName = appraisal.Employee?.FullName ?? "An employee";
        var requests = new List<AppraisalNotificationRequest>();

        if (appraisal.Employee?.ManagerId is Guid managerId && managerId != Guid.Empty)
        {
            requests.Add(new AppraisalNotificationRequest(
                managerId,
                AppraisalNotificationType.SelfEvalSubmitted,
                $"{employeeName} submitted their self-evaluation",
                "Their self-scores are now visible alongside yours on the manager evaluation form.",
                cycleName,
                $"/hr/performance/team-appraisals/{appraisal.Id}",
                appraisal.Id,
                employeeName));
        }

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings?.RequirePeerReviews == true && settings.PeerEvaluationOpenMode == PeerEvaluationOpenMode.AfterSelfEval)
        {
            foreach (var nomination in appraisal.PeerNominations.Where(n => n.NominationStatus == PeerNominationStatus.Approved))
            {
                requests.Add(new AppraisalNotificationRequest(
                    nomination.PeerEmployeeId,
                    AppraisalNotificationType.PeerEvaluationAssigned,
                    $"Peer feedback open for {employeeName}",
                    "Self-evaluation is in, so the peer feedback form for this appraisal is now open.",
                    cycleName,
                    "/me/performance/peer-reviews",
                    appraisal.Id,
                    employeeName));
            }
        }

        if (requests.Count > 0)
            await NotifyQuietlyAsync(requests, cancellationToken);
    }

    /// <summary>
    /// Get read-only view of submitted self-evaluation
    /// </summary>
    public async Task<ViewSubmittedEvaluationDto> GetViewSubmittedEvaluationAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee.Department)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Self))
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.GradeRanges)
                    .ThenInclude(gr => gr.GradeDefinition)
            .Include(a => a.Template)
                .ThenInclude(t => t.Sections)
                    .ThenInclude(s => s.TemplateItems)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new InvalidOperationException($"Appraisal {appraisalId} not found");

        var selfEvaluation = appraisal.EvaluatorEvaluations
            .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);

        if (selfEvaluation == null || !selfEvaluation.SubmittedDate.HasValue)
            throw new InvalidOperationException("Self-evaluation has not been submitted yet");

        var settings = appraisal.AppraisalCycle.AppraisalSettings;

        // KPI targets are now managed via EmployeeGoal - not loaded here.

        // Load attachments (read-only list)
        var tenantId = GetTenantId();
        var attachments = await _appraisalAttachmentRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.PerformanceAppraisalId == appraisalId)
            .ToListAsync(cancellationToken);

        // Check peer/manager review progress (visual-only)
        var peerEvaluations = await TenantEvaluationQuery()
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Peer)
            .ToListAsync(cancellationToken);

        var managerEvaluation = await TenantEvaluationQuery()
            .FirstOrDefaultAsync(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager, cancellationToken);

        var hrEvaluation = await TenantEvaluationQuery()
            .FirstOrDefaultAsync(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.HR, cancellationToken);

        return new ViewSubmittedEvaluationDto
        {
            AppraisalId = appraisal.Id,
            AppraisalCycleName = appraisal.AppraisalCycle.CycleName,
            PeriodStart = appraisal.StartDate,
            PeriodEnd = appraisal.EndDate,
            Year = appraisal.Year,

            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            EmployeePosition = appraisal.Employee.Position?.Title,
            Department = appraisal.Employee.Department?.Name,

            SubmittedDate = selfEvaluation.SubmittedDate.Value,
            CurrentStatus = appraisal.Status,

            RequirePeerReviews = settings.RequirePeerReviews,
            PeerReviewsInProgress = peerEvaluations.Any(),
            ManagerReviewComplete = managerEvaluation?.SubmittedDate.HasValue ?? false,
            RequireHRReview = settings.RequireHRReview,
            HRReviewComplete = hrEvaluation?.SubmittedDate.HasValue ?? false,

            AllowSelfSoftSkillRating = settings.AllowSelfSoftSkillRating,

            Sections = BuildSubmittedEvaluationSections(appraisal, selfEvaluation),

            Attachments = attachments
                .Select(a => new SubmittedAttachmentDto
                {
                    AttachmentId = a.Id,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    Description = a.Description,
                    UploadedDate = a.UploadDate
                })
                .ToList(),
        };
    }

    /// <summary>
    /// KPI achievement percentage. Delegates to <see cref="AppraisalScoring.KpiAchievementPercent"/>
    /// so the goal-assessment path and the scoring path cannot drift apart.
    /// </summary>
    private static decimal CalculateKpiAchievement(decimal actualValue, decimal? targetValue, decimal? minValue, decimal? maxValue)
        => AppraisalScoring.KpiAchievementPercent(actualValue, targetValue, minValue, maxValue);

    // ⚠ DetermineNextStatusAsync — the status after a self or manager submission, chosen from the
    // settings alone — was removed in performance closure B1/B5: it never asked where the appraisal
    // was (a manager's submission went to Governance or Completed whatever was still outstanding),
    // its final-conversation blind spot completed appraisals the settings said must wait, and its
    // peer and HR arms had no caller. The gates decide now (IAppraisalLifecycleService.SyncAsync).

    /// <summary>
    /// Updates appraisal status when an evaluation is started and saved as a draft.
    /// In the new lifecycle model, the only draft-save transition that changes status is
    /// <see cref="AppraisalStatus.Draft"/> → <see cref="AppraisalStatus.Active"/> when any
    /// evaluator first touches the appraisal. All in-progress phases are encoded in Active;
    /// no further promotions happen on draft saves.
    /// </summary>
    private static AppraisalStatus UpdateStatusOnDraft(AppraisalStatus currentStatus, EvaluatorRole role, AppraisalSettings settings)
    {
        // Transition Draft → Active the first time someone starts filling in the appraisal
        if (currentStatus == AppraisalStatus.Draft)
            return AppraisalStatus.Active;

        return currentStatus;
    }
    
    /// <summary>
    /// The reason string a remand snapshot is written with, and the one the post-remand
    /// comparisons look it up by.
    ///
    /// ⚠ These were two different literals — the remand wrote "Appeal Remand - Pre-Reevaluation
    /// Snapshot" while both readers searched for "Appeal Remand" — so the snapshot was never
    /// found: every pre/post comparison rendered against nothing and the employee's outcome view
    /// always reported that scores had not changed. One constant now, so they cannot drift again.
    /// </summary>
    private const string AppealRemandSnapshotReason = "Appeal Remand";

    /// <summary>
    /// Creates immutable snapshot of manager evaluation state before appeal remand.
    /// Preserves original scores for before/after comparison.
    /// </summary>
    private async Task<Guid> CreateManagerEvaluationSnapshotAsync(
        Guid appraisalId,
        string snapshotReason = AppealRemandSnapshotReason,
        CancellationToken cancellationToken = default)
    {
        // Load manager evaluation with all related data
        var managerEvaluation = await TenantEvaluationQuery()
            .Include(e => e.CriterionScores)
            .Include(e => e.Appraisal)
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager)
            .FirstOrDefaultAsync(cancellationToken);
            
        if (managerEvaluation == null)
        {
            throw new InvalidOperationException(
                $"No manager evaluation found for appraisal {appraisalId}. Cannot create snapshot.");
        }
        
        if (!managerEvaluation.SubmittedDate.HasValue)
        {
            throw new InvalidOperationException(
                "Manager evaluation must be submitted before creating snapshot.");
        }
        
        var snapshotDate = DateTime.UtcNow;
        
        // Create evaluation snapshot
        var evaluationSnapshot = new AppraisalEvaluationSnapshot
        {
            AppraisalId = appraisalId,
            EvaluatorId = managerEvaluation.EvaluatorId,
            EvaluatorRole = EvaluatorRole.Manager,
            TotalScore = managerEvaluation.TotalScore,
            SnapshotDate = snapshotDate,
            SnapshotReason = snapshotReason,
            TenantId = managerEvaluation.TenantId
        };
        
        await _evaluationSnapshotRepository.AddAsync(evaluationSnapshot);
        
        // Snapshot each criterion score
        foreach (var criterionScore in managerEvaluation.CriterionScores)
        {
            var criterionScoreSnapshot = new AppraisalCriterionScoreSnapshot
            {
                AppraisalEvaluationSnapshotId = evaluationSnapshot.Id,
                TemplateItemId = criterionScore.TemplateItemId,
                NumericScore = criterionScore.NumericScore,
                WeightedScore = criterionScore.WeightedScore,
                Notes = criterionScore.Notes,
                TenantId = criterionScore.TenantId
            };
            
            await _criterionScoreSnapshotRepository.AddAsync(criterionScoreSnapshot);
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Created manager evaluation snapshot {SnapshotId} for appraisal {AppraisalId}. Reason: {Reason}",
            evaluationSnapshot.Id,
            appraisalId,
            snapshotReason);
            
        return evaluationSnapshot.Id;
    }

    #endregion

    #region Manager Evaluation Operations

    /// <summary>
    /// Gets all appraisal cycles where the manager has team members to evaluate
    /// </summary>
    public async Task<IEnumerable<TeamAppraisalCycleSummaryDto>> GetTeamAppraisalCyclesAsync(Guid managerId, CancellationToken cancellationToken = default)
    {
        // Get all cycles where this manager has direct reports with appraisals
        var tenantId = GetTenantId();
        var cycles = await _appraisalCycleRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId)
            .Include(c => c.PerformanceAppraisals)
                .ThenInclude(a => a.Employee)
            .Include(c => c.PerformanceAppraisals)
                .ThenInclude(a => a.EvaluatorEvaluations)
            .Where(c => c.Status == AppraisalCycleStatus.Open || c.Status == AppraisalCycleStatus.InProgress)
            .Where(c => c.PerformanceAppraisals.Any(a => a.Employee.ManagerId == managerId))
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

        var summaries = new List<TeamAppraisalCycleSummaryDto>();

        foreach (var cycle in cycles)
        {
            var teamAppraisals = cycle.PerformanceAppraisals.Where(a => a.Employee.ManagerId == managerId).ToList();
            
            var totalEmployees = teamAppraisals.Count;
            var evaluatedCount = teamAppraisals.Count(a => 
                a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.SubmittedDate.HasValue));
            var inProgressCount = teamAppraisals.Count(a => 
                a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.Manager && e.StartedDate.HasValue && !e.SubmittedDate.HasValue));
            var pendingCount = totalEmployees - evaluatedCount - inProgressCount;

            summaries.Add(new TeamAppraisalCycleSummaryDto
            {
                CycleId = cycle.Id,
                CycleName = cycle.CycleName,
                PeriodStart = cycle.StartDate,
                PeriodEnd = cycle.EndDate,
                Status = cycle.Status,
                TotalEmployees = totalEmployees,
                EvaluatedCount = evaluatedCount,
                PendingCount = pendingCount,
                InProgressCount = inProgressCount
            });
        }

        return summaries;
    }

    /// <summary>
    /// Gets all team members (direct reports) in a specific appraisal cycle
    /// </summary>
    public async Task<IEnumerable<TeamMemberAppraisalDto>> GetTeamMemberAppraisalsAsync(Guid cycleId, Guid managerId, CancellationToken cancellationToken = default)
    {
        var appraisals = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.EvaluatorEvaluations)
            .Where(a => a.AppraisalCycleId == cycleId && a.Employee.ManagerId == managerId)
            .OrderBy(a => a.Employee.FirstName)
            .ThenBy(a => a.Employee.LastName)
            .ToListAsync(cancellationToken);

        var teamMembers = new List<TeamMemberAppraisalDto>();

        foreach (var appraisal in appraisals)
        {
            var selfEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
            var managerEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);

            teamMembers.Add(new TeamMemberAppraisalDto
            {
                AppraisalId = appraisal.Id,
                CycleName = appraisal.AppraisalCycle?.CycleName ?? string.Empty,
                EmployeeId = appraisal.EmployeeId,
                EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
                EmployeeNumber = appraisal.Employee.EmployeeNumber,
                Position = appraisal.Employee.Position?.Title,
                OrganizationUnit = appraisal.Employee.OrganizationUnit?.Name,
                SelfEvaluationSubmitted = selfEvaluation?.SubmittedDate.HasValue ?? false,
                SelfEvaluationSubmittedDate = selfEvaluation?.SubmittedDate,
                ManagerEvaluationStarted = managerEvaluation?.StartedDate.HasValue ?? false,
                ManagerEvaluationSubmitted = managerEvaluation?.SubmittedDate.HasValue ?? false,
                ManagerEvaluationSubmittedDate = managerEvaluation?.SubmittedDate,
                AppraisalStatus = appraisal.Status,
                RequireSelfEvaluation = appraisal.AppraisalCycle?.AppraisalSettings?.RequireSelfEvaluation ?? true,
                IsRemandedAppeal = appraisal.AppealRemandedDate.HasValue,
                AppealRemandDeadline = appraisal.AppealRemandDeadline,
                OverallScore = appraisal.OverallScore
            });
        }

        return teamMembers;
    }

    /// <summary>
    /// Gets complete context for manager evaluation page, including employee self-evaluations
    /// </summary>
    public async Task<ManagerEvaluationContextDto> GetManagerEvaluationContextAsync(Guid appraisalId, Guid managerId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Self || e.EvaluatorRole == EvaluatorRole.Manager))
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.Appeals)
                .ThenInclude(aa => aa.Items)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Include(a => a.CriterionConfigs)
                .ThenInclude(cc => cc.GradeRanges)
                    .ThenInclude(gr => gr.GradeDefinition)
            .Include(a => a.Template)
                .ThenInclude(t => t.Sections)
                    .ThenInclude(s => s.TemplateItems)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        // Verify manager authorization
        if (appraisal.Employee.ManagerId != managerId)
            throw new UnauthorizedAccessException("You are not authorized to evaluate this employee");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        var selfEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
        var managerEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);

        bool isManagerEvaluationSubmitted = managerEvaluation?.SubmittedDate.HasValue ?? false;
        // Editable if not submitted and status is appropriate for manager evaluation (Open, PeerEvaluation, or ManagerEvaluation)
        // Editable if manager has not yet submitted and appraisal is in an active workflow state
        bool isEditable = !isManagerEvaluationSubmitted && 
            (appraisal.Status == AppraisalStatus.Active || appraisal.Status == AppraisalStatus.Draft);

        // Pre-compute appealed criteria IDs (passed to section builder so it can mark items)
        var appealedCriteriaIds = new List<Guid>();
        if (appraisal.AppealRemandedDate.HasValue && appraisal.Appeals.Any())
        {
            var latestAppeal = appraisal.Appeals
                .OrderByDescending(a => a.SubmittedDate)
                .FirstOrDefault();
            if (latestAppeal?.Items != null)
            {
                appealedCriteriaIds = latestAppeal.Items
                    .Where(item => item.TemplateItemId.HasValue)
                    .Select(item => item.TemplateItemId!.Value)
                    .ToList();
            }
        }

        var result = new ManagerEvaluationContextDto
        {
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            EmployeeNumber = appraisal.Employee.EmployeeNumber,
            Position = appraisal.Employee.Position?.Title,
            Department = appraisal.Employee.OrganizationUnit?.Name,
            CycleId = appraisal.AppraisalCycleId,
            AppraisalCycleName = appraisal.AppraisalCycle?.CycleName ?? "N/A",
            PeriodStart = appraisal.StartDate,
            PeriodEnd = appraisal.EndDate,
            ManagerEvaluationDeadline = appraisal.AppraisalCycle?.ManagerEvaluationDeadline,
            Status = appraisal.Status,

            // Appeal remand information
            IsRemandedAppeal = appraisal.AppealRemandedDate.HasValue,
            AppealRemandedDate = appraisal.AppealRemandedDate,
            AppealRemandDeadline = appraisal.AppealRemandDeadline,
            IsRemandDeadlineExceeded = appraisal.AppealRemandDeadline.HasValue && DateTime.UtcNow > appraisal.AppealRemandDeadline.Value,
            AppealedKpiIds = new(),
            AppealedTemplateItemIds = appealedCriteriaIds,

            ManagerEvaluatorEvaluationId = managerEvaluation?.Id,
            IsManagerEvaluationSubmitted = isManagerEvaluationSubmitted,
            ManagerEvaluationSubmittedDate = managerEvaluation?.SubmittedDate,
            IsEditable = isEditable,
            SelfEvaluationWeight = settings?.SelfEvaluationWeight ?? 0,
            ManagerEvaluationWeight = settings?.ManagerEvaluationWeight ?? 0,
            PeerEvaluationWeight = settings?.PeerEvaluationWeight ?? 0,
            Settings = settings?.ToDto(),
            // Manager Final Assessment fields from PerformanceAppraisal entity
            OverallComments = appraisal.OverallComments,
            StrengthsIdentified = appraisal.StrengthsIdentified,
            AreasForImprovement = appraisal.AreasForImprovement,
            TrainingNeeds = appraisal.TrainingNeeds,
            CareerAspirations = appraisal.CareerAspirations,
            RecommendPromotion = appraisal.RecommendPromotion,
            RecommendIncrement = appraisal.RecommendIncrement,
            RecommendTraining = appraisal.RecommendTraining,
            RecommendPIP = appraisal.RecommendPIP,
            RecommendTermination = appraisal.RecommendTermination,
            RecommendationNotes = appraisal.RecommendationNotes,
            Sections = BuildManagerEvaluationSections(appraisal, selfEvaluation, managerEvaluation, appealedCriteriaIds)
        };

        return result;
    }

    /// <summary>
    /// Saves or updates manager evaluation (draft or submission)
    /// </summary>
    public async Task<ManagerEvaluationResultDto> SaveManagerEvaluationAsync(SaveManagerEvaluationDto saveDto, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.Employee)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Manager))
                .ThenInclude(e => e.CriterionScores)
            .Include(a => a.Goals)
            .FirstOrDefaultAsync(a => a.Id == saveDto.AppraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        // Verify manager authorization
        if (appraisal.Employee.ManagerId != saveDto.ManagerId)
            throw new UnauthorizedAccessException("You are not authorized to evaluate this employee");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal settings not found");

        // Every score on its item's own scale (drafts and submissions alike) — A11.
        var scaleError = await _scores.ValidateItemScoresAsync(appraisal.Id, saveDto.ItemScores, cancellationToken);
        if (scaleError != null)
            return new ManagerEvaluationResultDto { Success = false, Message = scaleError };

        // Check if this is a remanded appeal re-evaluation
        bool isRemandedReevaluation = appraisal.AppealRemandedDate.HasValue;

        // Strict sequence (decision 5, B1): the submission is the manager-evaluation step's — the
        // self-evaluation and the peer minimum in, as the cycle requires. Drafts can be saved at any
        // point; HR's audited advance is the way past a step that will not be completed. A remanded
        // re-evaluation is the appeal's own step and is not held to the pipeline.
        if (!saveDto.IsDraft && !(isRemandedReevaluation && appraisal.CurrentAppealStatus == AppraisalAppealStatus.Remanded))
        {
            await _lifecycle.EnsureAtAsync(appraisal.Id, "The manager evaluation cannot be submitted yet",
                [AppraisalSubStatus.ManagerEvaluation], cancellationToken);
        }

        if (isRemandedReevaluation)
        {
            // Enforce remand deadline
            if (appraisal.AppealRemandDeadline.HasValue && DateTime.UtcNow > appraisal.AppealRemandDeadline.Value)
            {
                throw new InvalidOperationException(
                    $"The re-evaluation deadline ({appraisal.AppealRemandDeadline.Value:yyyy-MM-dd HH:mm}) has passed. " +
                    "Please contact HR for further guidance.");
            }
            
            _logger.LogInformation(
                "Manager re-evaluating remanded appeal for appraisal {AppraisalId}. Deadline: {Deadline}",
                appraisal.Id,
                appraisal.AppealRemandDeadline);
        }

        // Get or create manager evaluator evaluation
        var managerEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);

        // Whether this record is new decides if it may be Updated later in the method. Calling
        // UpdateAsync on something added in the same unit of work flips EF's tracking from Added to
        // Modified, so it emits an UPDATE for a row that does not exist yet and the save dies with
        // "expected to affect 1 row(s), but actually affected 0" — which reads like a concurrency
        // problem rather than the create problem it is.
        var managerEvaluationIsNew = managerEvaluation == null;

        if (managerEvaluation == null)
        {
            managerEvaluation = new EvaluatorEvaluation
            {
                Id = Guid.NewGuid(),
                AppraisalId = appraisal.Id,
                EvaluatorId = saveDto.ManagerId,
                EvaluatorRole = EvaluatorRole.Manager,
                EvaluatorWeight = settings.ManagerEvaluationWeight,
                StartedDate = DateTime.UtcNow, // Set on first save (draft or submit)
                TenantId = appraisal.TenantId
            };
            await _evaluatorEvaluationRepository.AddAsync(managerEvaluation);
        }
        else if (managerEvaluation.SubmittedDate.HasValue)
        {
            // Block all writes (draft autosave included) once submitted. A legitimate re-evaluation
            // (appeal remand) first clears SubmittedDate, so this won't block that flow.
            return new ManagerEvaluationResultDto
            {
                Success = false,
                Message = "Manager evaluation has already been submitted and cannot be modified"
            };
        }
        else
        {
            // Set StartedDate if not already set (in case first save was missed)
            if (!managerEvaluation.StartedDate.HasValue)
            {
                managerEvaluation.StartedDate = DateTime.UtcNow;
            }
        }

        // One query for the whole form, not three or four per item.
        var scoring = await _scores.LoadScoringAsync(appraisal.Id, cancellationToken);

        // Save criterion scores from ItemScores (covers both KPI and competency items)
        foreach (var itemInput in saveDto.ItemScores)
        {
            if (!itemInput.NumericScore.HasValue && !itemInput.ActualValue.HasValue)
                continue;

            var existingScore = await TenantCriterionScoreQuery()
                .FirstOrDefaultAsync(cs => cs.EvaluatorEvaluationId == managerEvaluation.Id
                                        && cs.TemplateItemId == itemInput.TemplateItemId,
                                      cancellationToken);

            if (existingScore != null)
            {
                existingScore.NumericScore = itemInput.NumericScore;
                existingScore.ActualValue  = itemInput.ActualValue;
                existingScore.Notes = itemInput.Notes;
                await _scores.ScoreCriterionAsync(existingScore, scoring, cancellationToken);
                await _criterionScoreRepository.UpdateAsync(existingScore);
            }
            else
            {
                existingScore = new CriterionScore
                {
                    Id = Guid.NewGuid(),
                    EvaluatorEvaluationId = managerEvaluation.Id,
                    TemplateItemId = itemInput.TemplateItemId,
                    NumericScore = itemInput.NumericScore,
                    ActualValue  = itemInput.ActualValue,
                    Notes = itemInput.Notes,
                    TenantId = appraisal.TenantId
                };
                // Weight the score *before* handing it to the repository, and do not call
                // UpdateAsync afterwards.
                //
                // ⚠ UpdateAsync on a freshly-Added entity flips its EF state from Added to
                // Modified, so SaveChanges emits an UPDATE for a row that does not exist yet:
                // "expected to affect 1 row(s), but actually affected 0". Every manager
                // evaluation starts with no criterion scores, so this branch ran on the first
                // save every time and the whole request 500'd — the manager evaluation could
                // never be saved at all. The self-evaluation path never had the stray Update.
                await _scores.ScoreCriterionAsync(existingScore, scoring, cancellationToken);
                await _criterionScoreRepository.AddAsync(existingScore);
            }
        }

        // Update overall notes and recommendation
        managerEvaluation.OverallNotes = saveDto.OverallNotes;
        managerEvaluation.Recommendation = saveDto.Recommendation;

        // Update Manager Final Assessment & Recommendations on PerformanceAppraisal entity
        // These fields are only editable during ManagerEvaluation status
        appraisal.OverallComments = saveDto.OverallComments;
        appraisal.StrengthsIdentified = saveDto.StrengthsIdentified;
        appraisal.AreasForImprovement = saveDto.AreasForImprovement;
        appraisal.TrainingNeeds = saveDto.TrainingNeeds;
        appraisal.CareerAspirations = saveDto.CareerAspirations;
        appraisal.RecommendPromotion = saveDto.RecommendPromotion;
        appraisal.RecommendIncrement = saveDto.RecommendIncrement;
        appraisal.RecommendTraining = saveDto.RecommendTraining;
        appraisal.RecommendPIP = saveDto.RecommendPIP;
        appraisal.RecommendTermination = saveDto.RecommendTermination;
        appraisal.RecommendationNotes = saveDto.RecommendationNotes;

        // If submitting (not draft), set submitted date and calculate TotalScore
        if (!saveDto.IsDraft)
        {
            managerEvaluation.SubmittedDate = DateTime.UtcNow;
            
            // Calculate and set TotalScore only when submitting (not draft)
            var evalWithScores = await TenantEvaluationQuery()
                .Include(e => e.CriterionScores)
                .FirstOrDefaultAsync(e => e.Id == managerEvaluation.Id, cancellationToken);
            
            if (evalWithScores != null && evalWithScores.CriterionScores.Any())
            {
                // A weighted mean over what was scored, not a bare sum — see AppraisalScoring.
                managerEvaluation.TotalScore = await _scores.ScoreEvaluatorAsync(
                    evalWithScores.CriterionScores, scoring, cancellationToken);
            }

            // Handle remanded appeal re-evaluation completion. The overall is re-settled with the
            // save below; it is not published, because HR's post-remand decision is still to come.
            if (isRemandedReevaluation)
            {
                // The re-evaluation replaces the judgement a calibration restated: an earlier
                // calibrated overall left in place would stop the redo moving the score at all.
                appraisal.CalibratedOverallScore = null;

                // Clear remand fields and progress to HR Review
                appraisal.AppealRemandDeadline = null;
                appraisal.Status = AppraisalStatus.Governance;
                
                _logger.LogInformation(
                    "Manager completed remanded re-evaluation for appraisal {AppraisalId}. Returned to HR/Governance.",
                    appraisal.Id);
            }
            else if (appraisal.Status == AppraisalStatus.Draft)
            {
                // The first save opens a Draft appraisal. Where it goes from there is the gates'
                // call, made once this is saved (the sync below).
                appraisal.Status = AppraisalStatus.Active;
            }

            await _appraisalRepository.UpdateAsync(appraisal);
        }
        else
        {
            // Update status when saving as draft (from previous status to ManagerEvaluation)
            var draftStatus = UpdateStatusOnDraft(appraisal.Status, EvaluatorRole.Manager, settings);
            if (draftStatus != appraisal.Status)
            {
                appraisal.Status = draftStatus;
                await _appraisalRepository.UpdateAsync(appraisal);
            }
        }

        // Only when it already existed — see managerEvaluationIsNew above. A record added in this
        // same unit of work is already pending insert; marking it Modified turns that insert into
        // an UPDATE of a row that is not there.
        if (!managerEvaluationIsNew)
            await _evaluatorEvaluationRepository.UpdateAsync(managerEvaluation);

        // Upsert goal appraisal assessments (manager-side fields)
        if (saveDto.GoalAssessments != null && saveDto.GoalAssessments.Count > 0)
        {
            var goalLookup = appraisal.Goals?.ToDictionary(g => g.Id) ?? new Dictionary<Guid, EmployeeGoal>();

            foreach (var ga in saveDto.GoalAssessments)
            {
                // Derive FinalProgressPercent server-side for KPI goals
                decimal? computedProgress = ga.FinalProgressPercent;
                if (goalLookup.TryGetValue(ga.GoalId, out var goal)
                    && goal.KpiDefinitionId.HasValue
                    && goal.MeasurementType != MeasurementType.Boolean
                    && ga.FinalActualValue.HasValue)
                {
                    computedProgress = CalculateKpiAchievement(
                        ga.FinalActualValue.Value, goal.TargetValue, goal.MinValue, goal.MaxValue);
                }

                var existing = await _goalAssessmentRepository.GetQueryable()
                    .FirstOrDefaultAsync(a => a.EmployeeGoalId == ga.GoalId
                                           && a.PerformanceAppraisalId == appraisal.Id,
                                       cancellationToken);

                if (existing == null)
                {
                    await _goalAssessmentRepository.AddAsync(new EmployeeGoalAppraisalAssessment
                    {
                        EmployeeGoalId             = ga.GoalId,
                        PerformanceAppraisalId     = appraisal.Id,
                        TenantId                   = appraisal.TenantId,
                        ManagerFinalProgressPercent = computedProgress,
                        ManagerFinalStatus         = ga.FinalStatus,
                        ManagerFinalActualValue    = ga.FinalActualValue,
                        ManagerAssessmentNotes     = ga.AssessmentNotes,
                        ManagerEvidenceLinks       = ga.EvidenceLinks
                    });
                }
                else
                {
                    existing.ManagerFinalProgressPercent = computedProgress;
                    existing.ManagerFinalStatus         = ga.FinalStatus;
                    existing.ManagerFinalActualValue    = ga.FinalActualValue;
                    existing.ManagerAssessmentNotes     = ga.AssessmentNotes;
                    existing.ManagerEvidenceLinks       = ga.EvidenceLinks;
                    await _goalAssessmentRepository.UpdateAsync(existing);
                }
            }
        }

        // Saved first: the gates read what is saved.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Where a submission leaves the appraisal is the gates' call (B1), and the sync settles in
        // the same save as the move, so a Completed appraisal is never left without its score (A7):
        //   • nothing after the manager → Completed: settled and published;
        //   • only the final conversation or the acknowledgment left → settled, published only once
        //     final, so the employee acknowledges a settled score, not a blank;
        //   • a remanded re-evaluation → re-settled, not published until HR's post-remand decision.
        if (!saveDto.IsDraft)
        {
            if (isRemandedReevaluation)
                await _scores.SettleAsync(appraisal.Id, AppraisalScoreChangeSource.Appeal, publish: false, cancellationToken);
            else
                await _lifecycle.SyncAsync(appraisal.Id, AppraisalScoreChangeSource.Settle, publish: true, cancellationToken);
        }

        _logger.LogInformation("Manager evaluation saved for appraisal {AppraisalId} by manager {ManagerId}. IsDraft: {IsDraft}",
            saveDto.AppraisalId, saveDto.ManagerId, saveDto.IsDraft);

        if (!saveDto.IsDraft)
        {
            // Submitting the manager evaluation is what hands the appraisal to HR, so this is
            // where the HR reviewer gets assigned. Best-effort: the evaluation is already saved,
            // and a tenant with no resolvable HR employee must not lose a submitted evaluation —
            // ApproveAndFinalizeAsync creates the record itself if this did not.
            if (settings.RequireHRReview)
            {
                try
                {
                    await ProgressToHRReviewAsync(appraisal.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not assign an HR reviewer for appraisal {AppraisalId}; the manager evaluation stands and HR can pick it up from the review queue.",
                        appraisal.Id);
                }
            }

            await NotifyManagerEvaluationSubmittedAsync(appraisal, cancellationToken);
        }

        return new ManagerEvaluationResultDto
        {
            Success = true,
            Message = saveDto.IsDraft ? "Draft saved successfully" : "Manager evaluation submitted successfully",
            EvaluatorEvaluationId = managerEvaluation.Id,
            SubmittedDate = managerEvaluation.SubmittedDate
        };
    }

    /// <summary>
    /// Tells the employee their manager has scored them. The employee is told only that the
    /// evaluation is in — the scores themselves stay behind the HR sign-off, which raises its
    /// own notification. The HR reviewer is notified by <see cref="ProgressToHRReviewAsync"/>,
    /// which is what assigns them.
    /// </summary>
    private Task NotifyManagerEvaluationSubmittedAsync(PerformanceAppraisal appraisal, CancellationToken cancellationToken)
        => NotifyQuietlyAsync(new[]
        {
            new AppraisalNotificationRequest(
                appraisal.EmployeeId,
                AppraisalNotificationType.ManagerEvalSubmitted,
                "Your manager has completed your evaluation",
                "Your appraisal has moved on for review. You will be notified when the outcome is confirmed.",
                appraisal.AppraisalCycle?.CycleName,
                $"/me/performance/appraisals/{appraisal.Id}",
                appraisal.Id,
                appraisal.Employee?.FullName ?? "An employee"),
        }, cancellationToken);

    /// <summary>
    /// Gets detailed peer evaluations for manager to review
    /// </summary>
    public async Task<ManagerPeerEvaluationReviewDto> GetManagerPeerEvaluationReviewAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c!.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer))
                .ThenInclude(e => e.Evaluator)
                    .ThenInclude(emp => emp.Position)
            .Include(a => a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer))
                .ThenInclude(e => e.CriterionScores)
                    .ThenInclude(cs => cs.TemplateItem)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        var isAnonymous = settings?.PeerReviewsAnonymous ?? false;
        var allowKpiEvaluation = settings?.AllowPeerKpiEvaluation ?? false;

        var peerEvaluationsList = appraisal.EvaluatorEvaluations
            .Where(e => e.EvaluatorRole == EvaluatorRole.Peer)
            .ToList();

        var peerDetails = new List<PeerEvaluatorDetailDto>();

        foreach (var peerEval in peerEvaluationsList)
        {
            var competencyScores = peerEval.CriterionScores
                .Where(cs => cs.TemplateItem?.CompetencyId != null)
                .Select(cs => new PeerCompetencyScoreDto
                {
                    CriterionScoreId = cs.Id,
                    CriteriaName = cs.TemplateItem?.Competency?.CriteriaName ?? string.Empty,
                    CriteriaDescription = cs.TemplateItem?.Competency?.Description,
                    Weight = 0, // Weight is stored in PositionCriteriaMapping, not in AppraisalCompetency
                    NumericScore = cs.NumericScore ?? 0,
                    WeightedScore = cs.WeightedScore,
                    Comments = cs.Notes,
                    AchievedGrade = null // Grade calculation would require additional logic
                })
                .ToList();

            var kpiEvaluations = new List<PeerKpiEvaluationDto>();
            // KPI peer evaluations are deprecated (EmployeeKpiTarget removed)

            peerDetails.Add(new PeerEvaluatorDetailDto
            {
                EvaluationId = peerEval.Id,
                EvaluatorId = peerEval.EvaluatorId,
                EvaluatorName = $"{peerEval.Evaluator.FirstName} {peerEval.Evaluator.LastName}", // Always show actual name to managers
                EvaluatorEmployeeNumber = peerEval.Evaluator.EmployeeNumber, // Always show to managers
                EvaluatorPosition = peerEval.Evaluator.Position?.Title, // Always show to managers
                IsSubmitted = peerEval.SubmittedDate.HasValue,
                SubmittedDate = peerEval.SubmittedDate,
                TotalScore = peerEval.TotalScore,
                CompetencyScores = competencyScores,
                KpiEvaluations = kpiEvaluations
            });
        }

        return new ManagerPeerEvaluationReviewDto
        {
            AppraisalId = appraisalId,
            IsAnonymous = isAnonymous,
            AllowKpiEvaluation = allowKpiEvaluation,
            TotalPeerEvaluators = peerEvaluationsList.Count,
            SubmittedEvaluations = peerEvaluationsList.Count(e => e.SubmittedDate.HasValue),
            PeerEvaluations = peerDetails
        };
    }

    #endregion
    
    #region Employee Actions
    
    /// <summary>
    /// Employee acknowledges receipt of finalized appraisal
    /// </summary>
    public async Task AcknowledgeAppraisalAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c!.AppraisalSettings)
            .Include(a => a.Employee)
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken)
            ?? throw new ArgumentException($"Performance appraisal with ID '{appraisalId}' not found.");

        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only acknowledge your own appraisal.");

        if (appraisal.EmployeeAcknowledgedDate.HasValue)
            throw new InvalidOperationException("Appraisal has already been acknowledged.");

        // The acknowledgment step's (B1): every step before it behind the appraisal — calibration
        // and HR's sign-off as the cycle requires them (read from the sign-off record, AppraisalHRReview,
        // which HR's advance writes too), and the final conversation unless the cycle lets the
        // acknowledgment go first. It checked the status and the HR evaluation's date, so a required
        // final conversation never held anything back.
        await _lifecycle.EnsureAtAsync(appraisal.Id, "The appraisal cannot be acknowledged yet",
            [AppraisalSubStatus.PendingAcknowledgment], cancellationToken);

        appraisal.EmployeeAcknowledgedDate = DateTime.UtcNow;
        appraisal.EmployeeAcknowledged = true;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Acknowledgment is the last step, so the sync completes the appraisal and settles the score
        // in the same save, and publishes it (A7). With HR review and calibration off it used to
        // finish with no score, no grade and no talent rating at all.
        await _lifecycle.SyncAsync(appraisal.Id, AppraisalScoreChangeSource.Settle, publish: true, cancellationToken);

        if (appraisal.Employee?.ManagerId is Guid managerId && managerId != Guid.Empty)
        {
            await NotifyQuietlyAsync(new[]
            {
                new AppraisalNotificationRequest(
                    managerId,
                    AppraisalNotificationType.EmployeeAcknowledged,
                    $"{appraisal.Employee?.FullName} acknowledged their appraisal",
                    "The appraisal is now complete — no further action is needed from you.",
                    appraisal.AppraisalCycle?.CycleName,
                    $"/hr/performance/team-appraisals/{appraisal.Id}",
                    appraisal.Id,
                    appraisal.Employee?.FullName),
            }, cancellationToken);
        }
    }
    
    /// <summary>
    /// Get appeal page data for an employee
    /// </summary>
    public async Task<AppealPageDataDto> GetAppealPageDataAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Load appraisal with evaluations and scores
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
            .Include(a => a.CriterionConfigs)
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.CriterionScores)
                    .ThenInclude(cs => cs.TemplateItem)
                        .ThenInclude(ti => ti.Competency)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);
        
        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");
        
        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only appeal your own appraisal.");
        
        // Whether they may appeal, by the same rule the submit is held to (B1).
        var state = await _lifecycle.GetStateAsync(appraisal.Id, cancellationToken);
        var (canAppeal, cannotAppealReason, _) = AppraisalGates.CanFileAppeal(state.Facts, state.Settings, DateTime.UtcNow);
        
        // Get manager evaluation (final scores)
        var managerEval = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        
        var appealableKpis = new List<AppealableKpiDto>();
        var appealableCompetencies = new List<AppealableCompetencyDto>();
        
        if (managerEval != null)
        {
            // KPI appeals are deprecated - EmployeeKpiTarget has been replaced by EmployeeGoal
            // appealableKpis remains empty

            // Get Competency scores
            var competencyScores = managerEval.CriterionScores
                .Where(cs => cs.TemplateItem?.CompetencyId != null)
                .ToList();
            
            foreach (var score in competencyScores)
            {
                // Weight needs to come from the weighted score calculation, not directly from criteria
                // Use 0 as placeholder since weight is in PositionCriteriaMapping
                appealableCompetencies.Add(new AppealableCompetencyDto
                {
                    TemplateItemId = score.TemplateKey(),
                    ItemName = score.TemplateItem?.Competency?.CriteriaName ?? string.Empty,
                    Description = score.TemplateItem?.Competency?.Description,
                    NumericScore = score.NumericScore,
                    Weight = appraisal.CriterionConfigs
                                .FirstOrDefault(cc => cc.TemplateItemId == score.TemplateItemId)?.WeightUsed ?? 0,
                    WeightedScore = score.WeightedScore
                });
            }
        }
        
        return new AppealPageDataDto
        {
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            FinalScore = appraisal.OverallScore,
            FinalGrade = await GradeNameAsync(appraisal.OverallGradeDefinitionId, cancellationToken),
            CanAppeal = canAppeal,
            CannotAppealReason = cannotAppealReason,
            AppealableKpis = appealableKpis,
            AppealableCompetencies = appealableCompetencies
        };
    }
    
    /// <summary>
    /// Submit an appeal
    /// </summary>
    public async Task<AppraisalAppealDto> SubmitAppealAsync(SubmitAppealDto submitDto, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var appraisal = await GetOwnedAppraisalAsync(submitDto.AppraisalId, cancellationToken);

        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only appeal your own appraisal.");

        // One rule for the submit, the appeal page and the employee's list (B1): Completed, no
        // appeal yet, appeals enabled for the cycle, and inside the window. This checked the status
        // alone — EnableAppeals and AppealWindowDays reached only the list, which measured the
        // window from the end of the cycle.
        var state = await _lifecycle.GetStateAsync(appraisal.Id, cancellationToken);
        var (mayAppeal, cannotAppealReason, _) = AppraisalGates.CanFileAppeal(state.Facts, state.Settings, DateTime.UtcNow);
        if (!mayAppeal)
            throw new AppraisalGateException(state.SubStatus, cannotAppealReason!);

        if (submitDto.AppealedItems == null || !submitDto.AppealedItems.Any())
            throw new ArgumentException("At least one item must be appealed.");
        
        // Create appeal
        var appeal = new AppraisalAppeal
        {
            Id = Guid.NewGuid(),
            TenantId = appraisal.TenantId,
            PerformanceAppraisalId = appraisal.Id,
            EmployeeId = employeeId,
            AppealReason = submitDto.OverallReason ?? "Appeal submitted",
            Status = AppraisalAppealStatus.Submitted,
            SubmittedDate = DateTime.UtcNow,
            // What the appeal is against, kept on the appeal: the appraisal's own score moves
            // when the appeal is decided, and the outcome has to say what it moved from (A5).
            OriginalOverallScore = appraisal.OverallScore,
            CreatedBy = employeeId.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        
        // Create appeal items
        var appealItems = new List<AppraisalAppealItem>();
        foreach (var item in submitDto.AppealedItems)
        {
            appealItems.Add(new AppraisalAppealItem
            {
                Id = Guid.NewGuid(),
                TenantId = appraisal.TenantId,
                AppraisalAppealId = appeal.Id,
                TemplateItemId = item.TemplateItemId,
                Reason = item.Reason,
                CreatedBy = employeeId.ToString(),
                CreatedAt = DateTime.UtcNow
            });
        }
        
        // Update appraisal - set status to Appealed when appeal is filed
        appraisal.HasAppeal = true;
        appraisal.CurrentAppealStatus = AppraisalAppealStatus.Submitted;
        appraisal.Status = AppraisalStatus.Appealed;
        
        // Save to database
        await _appealRepository.AddAsync(appeal);
        foreach (var item in appealItems)
        {
            await _appealItemRepository.AddAsync(item);
        }
        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal submitted for appraisal {AppraisalId} with {ItemCount} items", appraisal.Id, appealItems.Count);

        await NotifyAppealSubmittedAsync(appraisal, appealItems.Count, cancellationToken);

        // Return DTO
        return new AppraisalAppealDto
        {
            Id = appeal.Id,
            TenantId = appeal.TenantId,
            PerformanceAppraisalId = appeal.PerformanceAppraisalId,
            AppealReason = appeal.AppealReason,
            Status = appeal.Status,
            SubmittedDate = appeal.SubmittedDate,
            Items = appealItems.Select(ai => new AppraisalAppealItemDto
            {
                Id = ai.Id,
                TenantId = ai.TenantId,
                AppraisalAppealId = ai.AppraisalAppealId,
                TemplateItemId = ai.TemplateItemId,
                Reason = ai.Reason
            }).ToList()
        };
    }
    
    /// <summary>
    /// Tells the people who have to act on an appeal that one has arrived: the HR reviewer
    /// already assigned to the appraisal, and the employee's manager, whose evaluation is what
    /// is being contested and who will have to re-do it if HR remands.
    ///
    /// <para>Best-effort — the appeal is saved by the time this runs.</para>
    /// </summary>
    private async Task NotifyAppealSubmittedAsync(
        PerformanceAppraisal appraisal, int itemCount, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = GetTenantId();

            var context = await TenantAppraisalQuery()
                .Where(a => a.Id == appraisal.Id)
                .Select(a => new
                {
                    a.Employee.FullName,
                    a.Employee.ManagerId,
                    CycleName = a.AppraisalCycle.CycleName,
                })
                .FirstOrDefaultAsync(cancellationToken);

            var hrReviewerId = await TenantEvaluationQuery()
                .Where(e => e.AppraisalId == appraisal.Id && e.EvaluatorRole == EvaluatorRole.HR)
                .Select(e => (Guid?)e.EvaluatorId)
                .FirstOrDefaultAsync(cancellationToken);

            var recipients = new List<Guid>();
            if (hrReviewerId.HasValue) recipients.Add(hrReviewerId.Value);
            if (context?.ManagerId is Guid managerId) recipients.Add(managerId);

            var subject = context?.FullName ?? "An employee";
            var requests = recipients.Distinct().Select(id => new AppraisalNotificationRequest(
                id,
                AppraisalNotificationType.AppealSubmitted,
                $"{subject} has appealed their appraisal",
                itemCount == 1
                    ? "One item is under appeal. Review it and decide whether to uphold, reject or remand."
                    : $"{itemCount} items are under appeal. Review them and decide whether to uphold, reject or remand.",
                context?.CycleName,
                $"/hr/performance/appeals/{appraisal.Id}",
                appraisal.Id,
                subject,
                NotificationUrgency.Warning));

            await NotifyQuietlyAsync(requests, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify on appeal submission for appraisal {AppraisalId}; the appeal stands.", appraisal.Id);
        }
    }

    /// <summary>
    /// Tells the appraisee where their appeal landed, and — on a remand — tells their manager
    /// that a re-evaluation is now owed, with the deadline.
    /// </summary>
    private async Task NotifyAppealResolvedAsync(
        PerformanceAppraisal appraisal, AppraisalAppealStatus decision, CancellationToken cancellationToken)
    {
        try
        {
            var context = await TenantAppraisalQuery()
                .Where(a => a.Id == appraisal.Id)
                .Select(a => new
                {
                    a.Employee.FullName,
                    a.Employee.ManagerId,
                    CycleName = a.AppraisalCycle.CycleName,
                })
                .FirstOrDefaultAsync(cancellationToken);

            var subject = context?.FullName ?? "An employee";
            var requests = new List<AppraisalNotificationRequest>();

            if (decision == AppraisalAppealStatus.Remanded)
            {
                requests.Add(new AppraisalNotificationRequest(
                    appraisal.EmployeeId,
                    AppraisalNotificationType.AppealResolved,
                    "Your appeal has been sent back for re-evaluation",
                    "HR found merit in your appeal and has asked your manager to look at the scores again. You will be told the final outcome once they have.",
                    context?.CycleName,
                    $"/me/performance/appraisals/{appraisal.Id}/appeal-status",
                    appraisal.Id,
                    subject));

                if (context?.ManagerId is Guid managerId)
                {
                    var deadline = appraisal.AppealRemandDeadline;
                    requests.Add(new AppraisalNotificationRequest(
                        managerId,
                        AppraisalNotificationType.ActionRequired,
                        $"Re-evaluation required: {subject}",
                        deadline.HasValue
                            ? $"HR has remanded this appraisal following an appeal. Re-submit your evaluation by {deadline.Value:dd MMM yyyy}."
                            : "HR has remanded this appraisal following an appeal. Re-submit your evaluation.",
                        context?.CycleName,
                        $"/hr/performance/team-appraisals/{appraisal.Id}",
                        appraisal.Id,
                        subject,
                        NotificationUrgency.Urgent));
                }
            }
            else
            {
                var upheld = decision == AppraisalAppealStatus.Upheld;
                requests.Add(new AppraisalNotificationRequest(
                    appraisal.EmployeeId,
                    AppraisalNotificationType.AppealResolved,
                    upheld ? "Your appeal was upheld" : "Your appeal was not upheld",
                    upheld
                        ? "HR agreed with your appeal. Open the outcome to see the final scores and HR's reasoning."
                        : "HR has confirmed the original scores. Open the outcome to see their reasoning.",
                    context?.CycleName,
                    $"/me/performance/appraisals/{appraisal.Id}/appeal-outcome",
                    appraisal.Id,
                    subject));
            }

            await NotifyQuietlyAsync(requests, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify on appeal resolution for appraisal {AppraisalId}; the decision stands.", appraisal.Id);
        }
    }

    /// <summary>
    /// Get appeal status for viewing (read-only)
    /// </summary>
    public async Task<AppealStatusViewDto> GetAppealStatusAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);
        
        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");
        
        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only view your own appeal.");
        
        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal has been filed for this appraisal.");
        
        // Load the appeal with related data
        var appeal = await TenantAppealQuery()
            .Include(a => a.Items)
            .Include(a => a.Reviewer)
            .FirstOrDefaultAsync(a => a.PerformanceAppraisalId == appraisalId, cancellationToken);
        
        if (appeal == null)
            throw new InvalidOperationException("Appeal not found.");
        
        // Load appealed items details
        var appealedItemsView = new List<AppealedItemViewDto>();
        
        foreach (var item in appeal.Items)
        {
            if (item.TemplateItemId.HasValue)
            {
                // It's a competency
                var competency = await _appraisalCompetencyRepository.GetByIdAsync(
                    (await _criterionConfigRepository.GetQueryable()
                        .Where(cc => cc.TenantId == GetTenantId() && cc.TemplateItemId == item.TemplateItemId.Value)
                        .Select(cc => cc.TemplateItem.CompetencyId)
                        .FirstOrDefaultAsync(cancellationToken)) ?? Guid.Empty);

                // Get the criterion score
                var score = await TenantCriterionScoreQuery()
                    .Include(cs => cs.EvaluatorEvaluation)
                    .FirstOrDefaultAsync(cs => 
                        cs.TemplateItemId == item.TemplateItemId.Value && 
                        cs.EvaluatorEvaluation.AppraisalId == appraisalId &&
                        cs.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager, 
                        cancellationToken);

                appealedItemsView.Add(new AppealedItemViewDto
                {
                    ItemId = item.Id,
                    ItemType = "Competency",
                    ItemName = competency?.CriteriaName ?? item.TemplateItem?.Competency?.CriteriaName ?? "Item",
                    Reason = item.Reason,
                    OriginalScore = score?.NumericScore,
                    TargetValue = null,
                    ActualValue = null
                });
            }
        }
        
        return new AppealStatusViewDto
        {
            AppealId = appeal.Id,
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            Status = appeal.Status,
            SubmittedDate = appeal.SubmittedDate,
            AppealReason = appeal.AppealReason,
            ReviewedByName = appeal.Reviewer?.FirstName != null ? $"{appeal.Reviewer.FirstName} {appeal.Reviewer.LastName}" : null,
            ResolvedDate = appeal.ResolvedDate,
            ResolutionNotes = appeal.ResolutionNotes,
            // The score the appeal was filed against, and the score it produced only when it
            // changed one — both used to read the appraisal's current score, so an upheld
            // appeal reported "no change" (A5). Appeals filed before the original was kept
            // fall back to the current score.
            OriginalScore = appeal.OriginalOverallScore ?? appraisal.OverallScore,
            AdjustedScore = appraisal.AdjustedScore,
            AppealedItems = appealedItemsView
        };
    }
    
    /// <summary>
    /// Get list of all appeals for HR/Manager review
    /// </summary>
    public async Task<List<AppealListItemDto>> GetAppealsListAsync(Guid? cycleId = null, AppraisalAppealStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = TenantAppealQuery()
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(pa => pa.Employee)
            .Include(a => a.PerformanceAppraisal)
                .ThenInclude(pa => pa.AppraisalCycle)
            .Include(a => a.Items)
            .AsQueryable();
        
        // Filter by cycle if specified
        if (cycleId.HasValue)
        {
            query = query.Where(a => a.PerformanceAppraisal.AppraisalCycleId == cycleId.Value);
        }
        
        // Filter by status if specified
        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }
        
        var appeals = await query
            .OrderByDescending(a => a.SubmittedDate)
            .ToListAsync(cancellationToken);
        
        return appeals.Select(a => new AppealListItemDto
        {
            AppealId = a.Id,
            AppraisalId = a.PerformanceAppraisalId,
            AppraisalNumber = a.PerformanceAppraisal.AppraisalNumber,
            EmployeeName = $"{a.PerformanceAppraisal.Employee.FirstName} {a.PerformanceAppraisal.Employee.LastName}",
            EmployeeNumber = a.PerformanceAppraisal.Employee.EmployeeNumber ?? "",
            CycleName = a.PerformanceAppraisal.AppraisalCycle?.CycleName ?? "",
            CycleId = a.PerformanceAppraisal.AppraisalCycleId,
            SubmittedDate = a.SubmittedDate,
            AppealedItemsCount = a.Items.Count,
            Status = a.Status
        }).ToList();
    }

    /// <summary>
    /// Get comprehensive appeal review data for HR resolution page
    /// </summary>
    public async Task<AppealReviewDto> GetAppealReviewDataAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        // Load appraisal with basic info
        var appraisal = await TenantAppraisalQuery()
            .Include(pa => pa.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(pa => pa.AppraisalCycle)
                .ThenInclude(ac => ac!.AppraisalSettings)
            .FirstOrDefaultAsync(pa => pa.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal exists for this appraisal");

        // Load the appeal
        var appeal = await TenantAppealQuery()
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.PerformanceAppraisalId == appraisalId, cancellationToken);

        if (appeal == null)
            throw new InvalidOperationException("Appeal not found");

        if (appeal.Status == AppraisalAppealStatus.Upheld || appeal.Status == AppraisalAppealStatus.Rejected)
            throw new InvalidOperationException("This appeal has already been resolved");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal settings not found");

        // Get employee position
        var employee = await _employeeRepository.GetQueryable()
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .FirstOrDefaultAsync(e => e.TenantId == GetTenantId() && e.Id == appraisal.EmployeeId, cancellationToken);

        var positionTitle = employee?.Position?.Title;

        // Load evaluator evaluations to get individual scores
        var evaluations = await TenantEvaluationQuery()
            .Where(e => e.AppraisalId == appraisalId && e.SubmittedDate.HasValue)
            .ToListAsync(cancellationToken);

        var selfEval = evaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
        var managerEval = evaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        var peerEvals = evaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer).ToList();

        // Build DTO
        var dto = new AppealReviewDto
        {
            AppealId = appeal.Id,
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            SubmittedDate = appeal.SubmittedDate,
            Status = appeal.Status,
            OverallAppealReason = appeal.AppealReason,
            
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            EmployeeNumber = appraisal.Employee.EmployeeNumber ?? "",
            PositionTitle = positionTitle,
            OrganizationUnitName = appraisal.Employee.OrganizationUnit?.Name,
            
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            CycleId = appraisal.AppraisalCycleId,
            CycleStartDate = appraisal.AppraisalCycle?.StartDate ?? default,
            CycleEndDate = appraisal.AppraisalCycle?.EndDate ?? default,
            
            SelfEvaluationScore = selfEval?.TotalScore,
            PeerEvaluationScore = peerEvals.Any() ? peerEvals.Average(p => p.TotalScore) : null,
            ManagerEvaluationScore = managerEval?.TotalScore,
            OverallScore = appraisal.OverallScore ?? 0,
            
            HRCanModifyScores = settings.HRCanModifyScores,
            AppraisalSettingsId = settings.Id,
            AppraisalSettingsName = settings.SettingsName
        };

        // Load appealed items details
        foreach (var item in appeal.Items)
        {
            if (item.TemplateItemId.HasValue)
            {
                // Load criteria details
                var scores = await TenantCriterionScoreQuery()
                    .Include(cs => cs.EvaluatorEvaluation)
                    .Where(cs => cs.TemplateItemId == item.TemplateItemId.Value && cs.EvaluatorEvaluation.AppraisalId == appraisalId)
                    .ToListAsync(cancellationToken);

                var managerScore = scores.FirstOrDefault(s => s.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager);
                var selfScore = scores.FirstOrDefault(s => s.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Self);

                var criterionDto = new AppealedCriterionReviewDto
                {
                    AppealItemId = item.Id,
                    TemplateItemId = item.TemplateItemId.Value,
                    ItemName = item.TemplateItem?.Competency?.CriteriaName ?? item.TemplateItem?.KpiDefinition?.KpiName ?? string.Empty,
                    ItemDescription = item.TemplateItem?.Competency?.Description ?? string.Empty,
                    Weight = 0, // Weight already reflected in WeightedScore
                    AppealReason = item.Reason,
                    SelfScore = selfScore?.NumericScore,
                    ManagerScore = managerScore?.NumericScore,
                    SelfWeightedScore = selfScore?.WeightedScore,
                    ManagerWeightedScore = managerScore?.WeightedScore,
                    ManagerComments = managerScore?.Notes,
                    SelfComments = selfScore?.Notes
                };
                
                dto.AppealedCriteria.Add(criterionDto);
            }
        }

        return dto;
    }

    /// <summary>
    /// Resolve an appraisal appeal with optional score modifications
    /// </summary>
    public async Task ResolveAppealAsync(Guid appraisalId, ResolveAppealDto resolveDto, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(pa => pa.AppraisalCycle)
                .ThenInclude(ac => ac!.AppraisalSettings)
            .FirstOrDefaultAsync(pa => pa.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException("Appraisal not found");

        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal exists for this appraisal");

        var appeal = await TenantAppealQuery()
            .FirstOrDefaultAsync(a => a.PerformanceAppraisalId == appraisalId, cancellationToken);

        if (appeal == null)
            throw new InvalidOperationException("Appeal not found");

        if (appeal.Status == AppraisalAppealStatus.Upheld || appeal.Status == AppraisalAppealStatus.Rejected)
            throw new InvalidOperationException("This appeal has already been resolved");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal settings not found");

        // Validate score modifications if provided
        if (resolveDto.CriteriaModifications?.Any() ?? false)
        {
            if (!settings.HRCanModifyScores)
                throw new InvalidOperationException("Score modifications are not allowed by appraisal policy");

            // On the item's own scale, like every other score input (A11). A KPI item's new score
            // is an achievement-percent override (D-22), so its scale is 0–100.
            var scaleError = await _scores.ValidateItemScoresAsync(
                appraisalId,
                resolveDto.CriteriaModifications.Select(m => new EvaluationItemInputDto
                {
                    TemplateItemId = m.TemplateItemId,
                    NumericScore = m.NewScore,
                }),
                cancellationToken);
            if (scaleError != null)
                throw new InvalidOperationException(scaleError);
        }

        // Apply score modifications if allowed and provided
        var scoresModified = false;
        if (settings.HRCanModifyScores)
        {
            if (resolveDto.CriteriaModifications != null)
            {
                foreach (var mod in resolveDto.CriteriaModifications)
                {
                    var scores = await TenantCriterionScoreQuery()
                        .Include(cs => cs.EvaluatorEvaluation)
                        .Where(cs => cs.TemplateItemId == mod.TemplateItemId && 
                                   cs.EvaluatorEvaluation.AppraisalId == appraisalId &&
                                   cs.EvaluatorEvaluation.EvaluatorRole == EvaluatorRole.Manager)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (scores != null)
                    {
                        scores.NumericScore = mod.NewScore;
                        scores.Notes = (scores.Notes ?? "") +
                            $"\n\n[HR Appeal Resolution - {DateTime.UtcNow:yyyy-MM-dd}]: {mod.Justification}";
                        await _criterionScoreRepository.UpdateAsync(scores);
                        scoresModified = true;
                    }
                }
            }

            // KPI score modifications are deprecated (EmployeeKpiTarget removed)

            // An appeal that restates items restates the overall through them: a calibrated
            // overall left in place would mask the very change the appeal made.
            if (scoresModified)
                appraisal.CalibratedOverallScore = null;
        }

        // Update appeal status and resolution details
        appeal.Status = resolveDto.ResolutionDecision;
        appeal.ReviewedById = reviewerId;
        appeal.ResolvedDate = DateTime.UtcNow;
        appeal.ResolutionNotes = resolveDto.ResolutionNotes;
        
        await _appealRepository.UpdateAsync(appeal);

        // Update appraisal status based on resolution decision
        switch (resolveDto.ResolutionDecision)
        {
            case AppraisalAppealStatus.Remanded:
                // CRITICAL: Create immutable snapshot before remanding
                // This preserves the original manager evaluation state for before/after comparison
                try
                {
                    var snapshotId = await CreateManagerEvaluationSnapshotAsync(
                        appraisalId, 
                        AppealRemandSnapshotReason,
                        cancellationToken);
                    
                    _logger.LogInformation(
                        "Created evaluation snapshot {SnapshotId} before remanding appraisal {AppraisalId}",
                        snapshotId,
                        appraisalId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to create snapshot before remanding appraisal {AppraisalId}. Remand operation aborted.",
                        appraisalId);
                    throw new InvalidOperationException(
                        "Cannot remand appeal: Failed to create evaluation snapshot. " +
                        "The original evaluation state must be preserved before remanding.", ex);
                }
                
                // Roll back to Active so manager can re-evaluate
                appraisal.Status = AppraisalStatus.Active;
                appraisal.CurrentAppealStatus = AppraisalAppealStatus.Remanded;
                
                // Set remand tracking fields
                appraisal.AppealRemandedDate = DateTime.UtcNow;
                appraisal.AppealRemandDeadline = DateTime.UtcNow.AddDays(settings.AppealReevaluationWindowDays);
                
                _logger.LogInformation(
                    "Appeal remanded for appraisal {AppraisalId}. Manager must re-evaluate by {Deadline}",
                    appraisalId, 
                    appraisal.AppealRemandDeadline);
                break;

            case AppraisalAppealStatus.Upheld:
            case AppraisalAppealStatus.Rejected:
                // Final state: the verdict is recorded and no further score changes are allowed.
                //
                // ⚠ HasAppeal is deliberately NOT cleared. It means "this appraisal has been
                // appealed", not "an appeal is open" — and every reader of the appeal record
                // (appeal status, HR appeal review, the employee's outcome page) refuses to load
                // when it is false. Clearing it here made the whole post-decision half of the
                // feature unreachable: the outcome screen threw "No appeal exists for this
                // appraisal" on exactly the appraisals it exists to show, and the cycle activity
                // feed could never report an appeal as resolved. Whether an appeal is still open
                // is CurrentAppealStatus's job.
                appraisal.CurrentAppealStatus = resolveDto.ResolutionDecision;
                appraisal.AppealRemandedDate = null;
                appraisal.AppealRemandDeadline = null;

                // The appraisal was moved to Appealed when the appeal was filed and nothing ever
                // moved it back, so a decided appraisal sat in Appealed for good.
                appraisal.Status = AppraisalStatus.Completed;
                break;
        }

        await _appraisalRepository.UpdateAsync(appraisal);

        if (resolveDto.ResolutionDecision is AppraisalAppealStatus.Upheld or AppraisalAppealStatus.Rejected)
        {
            // A decided appeal completes the appraisal again: the settle recomputes from the
            // (possibly modified) item scores, saves the decision with it, and publishes (A7).
            // Modifications used to write NumericScore and then re-sum the stored weighted scores,
            // so an upheld appeal never moved the result.
            var settled = await _scores.SettleAsync(appraisalId, AppraisalScoreChangeSource.Appeal, publish: true, cancellationToken);

            // AdjustedScore is the appeal's outcome as the employee sees it: the new overall when
            // the appeal changed it, and null when it did not (A5).
            var original = appeal.OriginalOverallScore ?? settled.ScoreBefore;
            appraisal.AdjustedScore = settled.ScoreAfter != original ? settled.ScoreAfter : null;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await NotifyAppealResolvedAsync(appraisal, resolveDto.ResolutionDecision, cancellationToken);
    }

    /// <summary>
    /// Marks an appeal as actively under HR review.
    ///
    /// <para><see cref="AppraisalAppealStatus.UnderReview"/> is a documented stage — the appeals
    /// list filters on it and the sub-status resolver maps it — but no endpoint could ever set
    /// it, so an appeal jumped from Submitted straight to a verdict and nobody could tell a
    /// queue of untouched appeals from ones someone was already working through.</para>
    /// </summary>
    public async Task<AppraisalAppealDto> BeginAppealReviewAsync(
        Guid appraisalId, Guid reviewerId, CancellationToken cancellationToken = default)
    {
        var appraisal = await GetOwnedAppraisalAsync(appraisalId, cancellationToken);

        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal exists for this appraisal.");

        var appeal = await TenantAppealQuery()
            .Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.PerformanceAppraisalId == appraisalId, cancellationToken)
            ?? throw new ArgumentException("Appeal not found.");

        if (appeal.Status != AppraisalAppealStatus.Submitted)
            throw new InvalidOperationException($"Only a submitted appeal can be picked up for review. This one is {appeal.Status}.");

        appeal.Status = AppraisalAppealStatus.UnderReview;
        appeal.ReviewedById = reviewerId;
        appraisal.CurrentAppealStatus = AppraisalAppealStatus.UnderReview;

        await _appealRepository.UpdateAsync(appeal);
        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appeal for appraisal {AppraisalId} picked up for review by {ReviewerId}", appraisalId, reviewerId);

        return new AppraisalAppealDto
        {
            Id = appeal.Id,
            TenantId = appeal.TenantId,
            PerformanceAppraisalId = appeal.PerformanceAppraisalId,
            AppealReason = appeal.AppealReason,
            Status = appeal.Status,
            SubmittedDate = appeal.SubmittedDate,
            Items = appeal.Items.Select(ai => new AppraisalAppealItemDto
            {
                Id = ai.Id,
                TenantId = ai.TenantId,
                AppraisalAppealId = ai.AppraisalAppealId,
                TemplateItemId = ai.TemplateItemId,
                Reason = ai.Reason
            }).ToList()
        };
    }
    
    /// <summary>
    /// Gets comprehensive post-remand review data for HR final decision
    /// Compares pre-remand (snapshot) vs post-remand (current) manager evaluation
    /// </summary>
    public async Task<PostRemandReviewDto> GetPostRemandReviewDataAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
            .Include(a => a.Appeals)
                .ThenInclude(ap => ap.Items)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new InvalidOperationException("Appraisal not found");

        if (appraisal.CurrentAppealStatus != AppraisalAppealStatus.Remanded)
            throw new InvalidOperationException("Appraisal is not in remanded status");

        var latestAppeal = appraisal.Appeals
            .OrderByDescending(a => a.SubmittedDate)
            .FirstOrDefault();

        if (latestAppeal == null)
            throw new InvalidOperationException("No appeal found for this appraisal");

        // Get the evaluation snapshot (pre-remand state)
        var snapshot = await _evaluationSnapshotRepository.GetQueryable()
            .Include(s => s.CriterionScores)
                .ThenInclude(cs => cs.KpiSnapshots)
            .Where(s => s.AppraisalId == appraisalId && s.SnapshotReason == AppealRemandSnapshotReason)
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot == null)
            throw new InvalidOperationException("Pre-remand snapshot not found");

        // Get current manager evaluation (post-remand state)
        var currentManagerEval = await TenantEvaluationQuery()
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentManagerEval == null || !currentManagerEval.SubmittedDate.HasValue)
            throw new InvalidOperationException("Manager has not completed post-remand re-evaluation");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;

        // Get template item weights as dictionary: templateItemId -> weight
        var positionMappings = await ItemWeightsAsync(appraisal, cancellationToken);

        var result = new PostRemandReviewDto
        {
            AppraisalId = appraisalId,
            AppraisalNumber = appraisal.AppraisalNumber,
            AppealId = latestAppeal.Id,
            AppealStatus = latestAppeal.Status,
            
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = appraisal.Employee.FullName,
            EmployeeNumber = appraisal.Employee.EmployeeNumber,
            PositionTitle = appraisal.Employee.Position?.Title,
            OrganizationUnitName = appraisal.Employee.OrganizationUnit?.Name,
            
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            CycleId = appraisal.AppraisalCycleId,
            CycleStartDate = appraisal.AppraisalCycle?.StartDate ?? default,
            CycleEndDate = appraisal.AppraisalCycle?.EndDate ?? default,
            
            AppealSubmittedDate = latestAppeal.SubmittedDate,
            AppealRemandedDate = appraisal.AppealRemandedDate ?? DateTime.UtcNow,
            AppealRemandDeadline = appraisal.AppealRemandDeadline ?? DateTime.UtcNow,
            ManagerReevaluationDate = currentManagerEval.SubmittedDate,
            
            OverallAppealReason = latestAppeal.AppealReason,
            HRRemandJustification = latestAppeal.ResolutionNotes ?? "",
            
            PreRemandOverallScore = snapshot.TotalScore ?? 0,
            PostRemandOverallScore = appraisal.OverallScore ?? 0,
            
            HRCanModifyScores = settings?.HRCanModifyScores ?? false
        };

        // Build criterion comparisons
        var appealedTemplateItemIds = latestAppeal.Items
            .Where(i => i.TemplateItemId.HasValue)
            .Select(i => i.TemplateItemId!.Value)
            .ToHashSet();

        foreach (var currentScore in currentManagerEval.CriterionScores)
        {
            var templateItemId = currentScore.TemplateKey();
            var competency = currentScore.TemplateItem?.Competency;

            var snapshotScore = snapshot?.CriterionScores
                .FirstOrDefault(s => s.TemplateItemId == templateItemId);

            var appealItem = latestAppeal.Items
                .FirstOrDefault(i => i.TemplateItemId == templateItemId);

            result.CriteriaComparisons.Add(new CriterionScoreComparisonDto
            {
                TemplateItemId = templateItemId,
                ItemName = competency?.CriteriaName ?? currentScore.TemplateItem?.KpiDefinition?.KpiName ?? string.Empty,
                ItemDescription = competency?.Description ?? string.Empty,
                Weight = positionMappings.GetValueOrDefault(templateItemId, 0),
                WasAppealed = appealedTemplateItemIds.Contains(templateItemId),
                AppealReason = appealItem?.Reason,
                
                PreRemandScore = snapshotScore?.NumericScore,
                PreRemandWeightedScore = snapshotScore?.WeightedScore,
                PreRemandComments = snapshotScore?.Notes,
                
                PostRemandScore = currentScore.NumericScore,
                PostRemandWeightedScore = currentScore.WeightedScore,
                PostRemandComments = currentScore.Notes
            });
        }

        // KPI comparisons are deprecated (EmployeeKpiTarget removed)

        return result;
    }

    /// <summary>
    /// Finalizes post-remand appeal with HR's final decision (Upheld or Rejected)
    /// This locks scores and closes the appeal lifecycle
    /// </summary>
    public async Task FinalizePostRemandAppealAsync(
        Guid appraisalId, 
        PostRemandFinalDecisionDto decisionDto, 
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.Appeals)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new InvalidOperationException("Appraisal not found");

        if (appraisal.CurrentAppealStatus != AppraisalAppealStatus.Remanded)
            throw new InvalidOperationException("Appraisal is not in remanded status");

        if (decisionDto.FinalDecision != AppraisalAppealStatus.Upheld && 
            decisionDto.FinalDecision != AppraisalAppealStatus.Rejected)
            throw new InvalidOperationException("Final decision must be either Upheld or Rejected");

        var latestAppeal = appraisal.Appeals
            .OrderByDescending(a => a.SubmittedDate)
            .FirstOrDefault();

        if (latestAppeal == null)
            throw new InvalidOperationException("No appeal found");

        // Verify manager has completed re-evaluation
        var managerEval = await TenantEvaluationQuery()
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager)
            .FirstOrDefaultAsync(cancellationToken);

        if (managerEval == null || !managerEval.SubmittedDate.HasValue)
            throw new InvalidOperationException("Manager has not completed post-remand re-evaluation");

        // Update appeal status
        latestAppeal.Status = decisionDto.FinalDecision;
        latestAppeal.ResolutionNotes = decisionDto.HRFinalNotes;
        latestAppeal.ReviewedById = reviewerId;
        latestAppeal.ResolvedDate = DateTime.UtcNow;

        // Update appraisal status. HasAppeal stays true — see the note in ResolveAppealAsync;
        // clearing it here hid the outcome from the employee this decision is about.
        appraisal.CurrentAppealStatus = decisionDto.FinalDecision;
        appraisal.AppealRemandedDate = null;
        appraisal.AppealRemandDeadline = null;

        // If upheld, post-remand scores are already in place (manager re-evaluated)
        // If rejected, we keep current scores (which are the post-remand scores anyway)
        // In both cases, we're confirming the current state as final.
        //
        // The remand had rolled the appraisal back to Active so the manager could re-evaluate;
        // with a final verdict in, it is complete again.
        appraisal.Status = AppraisalStatus.Completed;

        await _appealRepository.UpdateAsync(latestAppeal);
        await _appraisalRepository.UpdateAsync(appraisal);

        // Complete again: settle from the re-evaluated scores in the same save, and publish (A7).
        // AdjustedScore is set only when the appeal moved the overall — it was set to the current
        // score unconditionally, so every post-remand outcome read as a change (A5).
        var settled = await _scores.SettleAsync(appraisalId, AppraisalScoreChangeSource.Appeal, publish: true, cancellationToken);
        var original = latestAppeal.OriginalOverallScore ?? settled.ScoreBefore;
        appraisal.AdjustedScore = settled.ScoreAfter != original ? settled.ScoreAfter : null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Post-remand appeal finalized with decision {Decision} for appraisal {AppraisalId}",
            decisionDto.FinalDecision,
            appraisalId);

        await NotifyAppealResolvedAsync(appraisal, decisionDto.FinalDecision, cancellationToken);
    }
    
    /// <summary>
    /// Gets employee read-only view of final appeal outcome after HR decision
    /// Shows final scores, HR notes, and outcome message
    /// </summary>
    public async Task<EmployeeAppealOutcomeDto> GetEmployeeAppealOutcomeAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
            .Include(a => a.Appeals)
                .ThenInclude(ap => ap.Items)
                    .ThenInclude(i => i.TemplateItem)
                        .ThenInclude(ti => ti.Competency)
            .Include(a => a.Appeals)
                .ThenInclude(ap => ap.Items)
                    .ThenInclude(i => i.TemplateItem)
                        .ThenInclude(ti => ti.KpiDefinition)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new InvalidOperationException("Appraisal not found");

        if (appraisal.EmployeeId != employeeId)
            throw new UnauthorizedAccessException("You can only view your own appeal outcomes");

        if (!appraisal.HasAppeal)
            throw new InvalidOperationException("No appeal exists for this appraisal");

        var latestAppeal = appraisal.Appeals
            .OrderByDescending(a => a.SubmittedDate)
            .FirstOrDefault();

        if (latestAppeal == null)
            throw new InvalidOperationException("Appeal not found");

        if (latestAppeal.Status != AppraisalAppealStatus.Upheld && latestAppeal.Status != AppraisalAppealStatus.Rejected)
            throw new InvalidOperationException("Appeal outcome is not yet final");

        // Get snapshot to determine if scores changed
        var snapshot = await _evaluationSnapshotRepository.GetQueryable()
            .Include(s => s.CriterionScores)
            .Where(s => s.AppraisalId == appraisalId && s.SnapshotReason == AppealRemandSnapshotReason)
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync(cancellationToken);

        // Get final manager evaluation
        var managerEval = await TenantEvaluationQuery()
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
                    .ThenInclude(ti => ti.Competency)
            .Include(e => e.CriterionScores)
                .ThenInclude(cs => cs.TemplateItem)
                    .ThenInclude(ti => ti.KpiDefinition)
            .Where(e => e.AppraisalId == appraisalId && e.EvaluatorRole == EvaluatorRole.Manager)
            .FirstOrDefaultAsync(cancellationToken);

        if (managerEval == null)
            throw new InvalidOperationException("Manager evaluation not found");

        // Get template item weights as dictionary: templateItemId -> weight
        var positionMappings = await ItemWeightsAsync(appraisal, cancellationToken);

        var result = new EmployeeAppealOutcomeDto
        {
            AppraisalId = appraisalId,
            AppraisalNumber = appraisal.AppraisalNumber,
            CycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            CycleStartDate = appraisal.AppraisalCycle?.StartDate ?? default,
            CycleEndDate = appraisal.AppraisalCycle?.EndDate ?? default,
            PositionTitle = appraisal.Employee.Position?.Title ?? "",
            OrganizationUnitName = appraisal.Employee.OrganizationUnit?.Name ?? "",
            
            AppealStatus = latestAppeal.Status,
            AppealResolvedDate = latestAppeal.ResolvedDate ?? DateTime.UtcNow,
            
            AppealSubmittedDate = latestAppeal.SubmittedDate,
            EmployeeAppealReason = latestAppeal.AppealReason,
            
            HRFinalNotes = latestAppeal.ResolutionNotes ?? "",
            OutcomeMessage = latestAppeal.Status == AppraisalAppealStatus.Upheld
                ? "Your appeal was accepted. Your appraisal scores were adjusted after review."
                : "Your appeal was reviewed, but the original appraisal outcome stands.",
            
            FinalOverallScore = appraisal.OverallScore ?? 0,
            OriginalOverallScore = latestAppeal.OriginalOverallScore,
            // Against the score the appeal was filed on (A5). It compared the remand snapshot's
            // manager total with the overall — two different numbers even when nothing moved —
            // and read false whenever there was no remand. Appeals filed before the original was
            // kept fall back to that comparison.
            ScoresChangedAfterAppeal = latestAppeal.OriginalOverallScore.HasValue
                ? latestAppeal.OriginalOverallScore != appraisal.OverallScore
                : snapshot != null && snapshot.TotalScore != appraisal.OverallScore
        };

        // Build appealed items list
        foreach (var item in latestAppeal.Items)
        {
            if (item.TemplateItemId.HasValue)
            {
                var itemName = item.TemplateItem?.Competency?.CriteriaName 
                    ?? item.TemplateItem?.KpiDefinition?.KpiName 
                    ?? "Unknown";
                result.AppealedItems.Add($"Criterion: {itemName}");
            }
        }

        // Build final criterion scores
        var appealedTemplateItemIds = latestAppeal.Items
            .Where(i => i.TemplateItemId.HasValue)
            .Select(i => i.TemplateItemId!.Value)
            .ToHashSet();

        foreach (var score in managerEval.CriterionScores)
        {
            var competency = score.TemplateItem?.Competency;
            var templateItemId = score.TemplateKey();
            result.FinalCriteriaScores.Add(new FinalCriterionScoreDto
            {
                TemplateItemId = templateItemId,
                ItemName = competency?.CriteriaName ?? score.TemplateItem?.KpiDefinition?.KpiName ?? string.Empty,
                ItemDescription = competency?.Description ?? string.Empty,
                FinalScore = score.NumericScore,
                FinalWeightedScore = score.WeightedScore,
                Weight = positionMappings.GetValueOrDefault(templateItemId, 0),
                ManagerComments = score.Notes ?? "",
                WasAppealed = appealedTemplateItemIds.Contains(templateItemId),
                AchievementOverridden = score.NumericScore.HasValue && score.TemplateItem?.KpiDefinitionId != null
            });
        }

        // FinalKpiScores are deprecated (EmployeeKpiTarget removed); result.FinalKpiScores remains empty.

        return result;
    }
    
    #endregion

    #region HR Review Workflow

    /// <summary>
    /// Progresses an appraisal to HR review stage by creating an HR evaluator evaluation
    /// This method orchestrates the transition to HR review phase
    /// </summary>
    public async Task<bool> ProgressToHRReviewAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        // Get the appraisal with necessary related data
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal cycle settings not found.");

        // Check if HR review is required
        if (!settings.RequireHRReview)
        {
            _logger.LogInformation("HR review not required for appraisal {AppraisalId}", appraisalId);
            return false;
        }

        // Check if HR evaluation already exists
        var existingHREvaluation = appraisal.EvaluatorEvaluations
            .Any(e => e.EvaluatorRole == EvaluatorRole.HR);

        if (existingHREvaluation)
        {
            _logger.LogWarning("HR evaluation already exists for appraisal {AppraisalId}", appraisalId);
            return false;
        }

        // Validate that manager evaluation is complete (if required)
        if (settings.RequireManagerEvaluation)
        {
            var managerEvaluation = appraisal.EvaluatorEvaluations
                .FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);

            if (managerEvaluation == null || !managerEvaluation.SubmittedDate.HasValue)
            {
                throw new InvalidOperationException("Manager evaluation must be completed before HR review.");
            }
        }

        // Create the HR evaluation directly. The evaluator CRUD this once went through
        // (AddEvaluatorEvaluationAsync, removed with the rest of that block in performance
        // closure P1) rejected an addition whose EvaluatorWeight took the *sum over records* past
        // 1.0 — but EvaluatorWeight is the role's weight copied onto every record, so N peers each
        // carrying the peer weight already blew the total on any cycle with peer reviews, and HR
        // review could not be reached. HR governs the score without carrying weight in it: the
        // record is created at weight 0.
        var hrEval = await EnsureHRReviewEvaluationAsync(appraisal, reviewerId: null, cancellationToken);
        hrEval.OverallNotes ??= "HR Review - Automatically assigned";
        await _evaluatorEvaluationRepository.UpdateAsync(hrEval);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("HR review successfully assigned for appraisal {AppraisalId} to HR reviewer {HRReviewerId}",
            appraisalId, hrEval.EvaluatorId);

        var employeeName = await _employeeRepository.GetQueryable()
            .Where(e => e.Id == appraisal.EmployeeId && e.TenantId == GetTenantId())
            .Select(e => e.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        await NotifyQuietlyAsync(new[]
        {
            new AppraisalNotificationRequest(
                hrEval.EvaluatorId,
                AppraisalNotificationType.ActionRequired,
                $"You have been assigned HR review for {employeeName ?? "an employee"}",
                "The manager evaluation is in. Review the scores and finalise or return the appraisal.",
                appraisal.AppraisalCycle?.CycleName,
                $"/hr/performance/hr-review/{appraisalId}",
                appraisalId,
                employeeName,
                NotificationUrgency.Warning),
        }, cancellationToken);

        return true;
    }

    /// <summary>
    /// Resolves the HR reviewer for an appraisal deterministically:
    /// (1) the configured <see cref="AppraisalSettings.DefaultHRReviewerId"/> when it points to an active
    /// employee, otherwise (2) the least-loaded active HR employee (fewest open HR reviews), with a stable
    /// EmployeeNumber tiebreak. Returns <see cref="Guid.Empty"/> only when no eligible HR employee exists —
    /// the caller turns that into a clear error rather than silently skipping HR review.
    /// </summary>
    private async Task<Guid> AssignHRReviewerAsync(PerformanceAppraisal appraisal, CancellationToken cancellationToken)
    {
        // 1. Prefer an explicitly configured default HR reviewer (must be at work).
        //
        // ⚠ "At work" is Active OR on probation (HR finish plan lane 11; TDC's call of 2026-09-23:
        // probation is a contract status, not an availability). Both steps here asked for Active
        // only, so an HR officer on probation was never assigned — and on a tenant whose HR staff
        // were all imported onto probation, nobody was, and the appraisal had no HR reviewer.
        var configuredId = appraisal.AppraisalCycle?.AppraisalSettings?.DefaultHRReviewerId;
        if (configuredId.HasValue && configuredId.Value != Guid.Empty)
        {
            var configured = await _employeeRepository.GetQueryable()
                .FirstOrDefaultAsync(e => e.TenantId == GetTenantId() && e.Id == configuredId.Value && e.IsActive
                                          && (e.StaffStatus == StaffStatus.Active || e.StaffStatus == StaffStatus.Probation),
                    cancellationToken);
            if (configured != null)
            {
                _logger.LogInformation("Assigned configured HR reviewer {EmployeeId} for appraisal {AppraisalId}", configured.Id, appraisal.Id);
                return configured.Id;
            }
            _logger.LogWarning("Configured DefaultHRReviewerId {Id} is not at work; falling back to load-based assignment.", configuredId.Value);
        }

        // 2. Otherwise pick the least-loaded HR employee at work.
        var hrCandidates = await _employeeRepository.GetQueryable()
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .Where(e => e.TenantId == GetTenantId() && e.IsActive
                        && (e.StaffStatus == StaffStatus.Active || e.StaffStatus == StaffStatus.Probation))
            .Where(e =>
                (e.Position != null && (e.Position.Title.Contains("HR") || e.Position.Title.Contains("Human Resource"))) ||
                (e.OrganizationUnit != null && e.OrganizationUnit.Name.Contains("HR")))
            .Select(e => new { e.Id, e.EmployeeNumber })
            .ToListAsync(cancellationToken);

        if (hrCandidates.Count == 0)
        {
            _logger.LogWarning("No active HR employee found for appraisal {AppraisalId}. HR assignment failed.", appraisal.Id);
            return Guid.Empty;
        }

        var candidateIds = hrCandidates.Select(c => c.Id).ToList();
        var openLoads = await TenantEvaluationQuery()
            .Where(ev => ev.EvaluatorRole == EvaluatorRole.HR && ev.SubmittedDate == null && candidateIds.Contains(ev.EvaluatorId))
            .GroupBy(ev => ev.EvaluatorId)
            .Select(g => new { EvaluatorId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var loadById = openLoads.ToDictionary(x => x.EvaluatorId, x => x.Count);

        var chosen = hrCandidates
            .OrderBy(c => loadById.TryGetValue(c.Id, out var n) ? n : 0)
            .ThenBy(c => c.EmployeeNumber)
            .First();

        _logger.LogInformation("Assigned least-loaded HR reviewer {EmployeeId} for appraisal {AppraisalId}", chosen.Id, appraisal.Id);
        return chosen.Id;
    }

    /// <summary>
    /// Gets complete HR review context for an appraisal
    /// </summary>
    public async Task<HRReviewDto> GetHRReviewAsync(Guid appraisalId, Guid? requestingEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.CriterionScores)
                    .ThenInclude(cs => cs.TemplateItem)
                        .ThenInclude(ti => ti.Competency)
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.CriterionScores)
                    .ThenInclude(cs => cs.TemplateItem)
                        .ThenInclude(ti => ti.KpiDefinition)
            // Needed for "finalised by" — without it the name could only be a placeholder.
            .Include(a => a.EvaluatorEvaluations)
                .ThenInclude(e => e.Evaluator)
            // The snapshot is what was scored: targets, weights (A12).
            .Include(a => a.CriterionConfigs)
            // The HR sign-off record, for the release rule (P2).
            .Include(a => a.HRReviews)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");

        var settings = appraisal.AppraisalCycle?.AppraisalSettings;
        if (settings == null)
            throw new InvalidOperationException("Appraisal cycle settings not found.");

        var outcomeReleased = AppraisalRelease.IsReleased(
            appraisal.Status,
            appraisal.IsCalibrated,
            appraisal.HRReviews.Any(r => !r.IsDeleted && r.ReviewCompletedDate != null && r.IsApproved),
            appraisal.AppealRemandedDate != null,
            settings.RequireCalibration,
            settings.RequireHRReview);

        var configsByTemplateItemId = appraisal.CriterionConfigs
            .Where(c => c.TemplateItemId.HasValue)
            .GroupBy(c => c.TemplateItemId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // Check if the requester is the appraisee and peer reviews are anonymous
        var isAppraiseeViewing = requestingEmployeeId.HasValue && requestingEmployeeId.Value == appraisal.EmployeeId;
        var shouldHidePeerDetails = isAppraiseeViewing && settings.PeerReviewsAnonymous;

        // Get evaluations
        var selfEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
        var managerEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        var peerEvaluations = appraisal.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer).ToList();
        var hrEvaluation = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.HR);

        // Check prerequisites. Whether HR may sign off now is the gates' answer — the one the sign-off
        // itself is held to (B4): it demanded the self-evaluation and the peer minimum whatever the
        // settings required, and ignored calibration, so the screen offered a Finalise the server refused.
        var isSelfComplete = selfEvaluation?.SubmittedDate.HasValue ?? false;
        var isManagerComplete = managerEvaluation?.SubmittedDate.HasValue ?? false;
        var requiresPeerReviews = settings.RequirePeerReviews && settings.MinPeerEvaluators > 0;
        var minPeerReviews = requiresPeerReviews ? settings.MinPeerEvaluators : 0;
        var completedPeerReviews = peerEvaluations.Count(e => e.SubmittedDate.HasValue);
        var arePeerReviewsComplete = completedPeerReviews >= minPeerReviews;
        var gateState = await _lifecycle.GetStateAsync(appraisal.Id, cancellationToken);
        var canProceed = AppraisalGates.Check(gateState.Block, AppraisalSubStatus.PendingHRReview);

        // Build evaluation summaries
        var selfSummary = selfEvaluation != null ? BuildEvaluationSummary(selfEvaluation, configsByTemplateItemId) : null;
        var managerSummary = managerEvaluation != null ? BuildEvaluationSummary(managerEvaluation, configsByTemplateItemId) : null;

        // Build peer evaluation summary - HIDE details if anonymous and appraisee is viewing.
        // Submitted peers only: a draft is not a peer's view yet, and it is not in the score.
        PeerEvaluationSummaryDto? peerSummary = null;
        var submittedPeers = peerEvaluations.Where(e => e.SubmittedDate.HasValue).ToList();
        if (submittedPeers.Any() && !shouldHidePeerDetails)
        {
            var peerCompetencyScores = submittedPeers
                .SelectMany(e => e.CriterionScores)
                .Where(cs => cs.TemplateItem?.CompetencyId != null)
                .GroupBy(cs => cs.TemplateItemId)
                .Select(g => new PeerCompetencyScoreSummaryDto
                {
                    CriteriaName = g.First().TemplateItem?.Competency?.CriteriaName ?? "Unknown",
                    AverageScore = (decimal?)g.Average(cs => cs.NumericScore),
                    ResponseCount = g.Count()
                })
                .ToList();

            // KPI items scored by peers (only present when AllowPeerKpiEvaluation) → aggregated KPI rows.
            var peerKpiScores = submittedPeers
                .SelectMany(e => e.CriterionScores)
                .Where(cs => cs.TemplateItem?.KpiDefinitionId != null)
                .GroupBy(cs => cs.TemplateItemId)
                .Select(g => new PeerKpiScoreSummaryDto
                {
                    KpiName = g.First().TemplateItem?.KpiDefinition?.KpiName ?? "Unknown",
                    AverageActual = g.Any(cs => cs.ActualValue.HasValue)
                        ? g.Where(cs => cs.ActualValue.HasValue).Average(cs => cs.ActualValue!.Value)
                        : (decimal?)null,
                    // The snapshot's target — the one scored — not today's template (A12).
                    AverageTarget = g.Key is Guid itemId && configsByTemplateItemId.TryGetValue(itemId, out var config)
                        ? config.KpiTargetValue
                        : null,
                    ResponseCount = g.Count()
                })
                .ToList();

            peerSummary = new PeerEvaluationSummaryDto
            {
                IsAnonymous = settings.PeerReviewsAnonymous,
                AllowKpiEvaluation = settings.AllowPeerKpiEvaluation,
                CompetencyScores = peerCompetencyScores,
                KpiScores = peerKpiScores
            };
        }
        // If shouldHidePeerDetails is true, peerSummary remains null

        var totalWeight = settings.SelfEvaluationWeight + settings.ManagerEvaluationWeight + settings.PeerEvaluationWeight;

        // P2: the appraisee's own copy carries the outcome only once it is released to them. The
        // employee's page reads this record, not the appraisal, so withholding the score on the
        // appraisal alone left the manager's evaluation, the peer average, the final score and
        // grade — and HR's remarks, a draft included — in the body the page merely declined to show.
        var withholdOutcome = isAppraiseeViewing && !outcomeReleased;

        return new HRReviewDto
        {
            OutcomeReleased = outcomeReleased,
            AppraisalId = appraisal.Id,
            AppraisalNumber = appraisal.AppraisalNumber,
            EmployeeId = appraisal.EmployeeId,
            EmployeeName = $"{appraisal.Employee.FirstName} {appraisal.Employee.LastName}",
            Position = appraisal.Employee.Position?.Title ?? "",
            OrganizationUnit = appraisal.Employee.OrganizationUnit?.Name ?? "",
            AppraisalCycleName = appraisal.AppraisalCycle?.CycleName ?? "",
            Status = appraisal.Status,
            IsSelfEvaluationComplete = isSelfComplete,
            IsManagerEvaluationComplete = isManagerComplete,
            RequiresPeerReviews = requiresPeerReviews,
            ArePeerReviewsComplete = arePeerReviewsComplete,
            RequiredPeerReviews = minPeerReviews,
            CompletedPeerReviews = completedPeerReviews,
            CanProceedToHRReview = canProceed,
            WeightTotalValid = Math.Abs(totalWeight - 1.0m) < 0.01m,
            TotalWeight = totalWeight,
            IsCycleActive = appraisal.AppraisalCycle.Status == AppraisalCycleStatus.Open,
            SelfEvaluation = selfSummary,
            ManagerEvaluation = withholdOutcome ? null : managerSummary,
            PeerEvaluationSummary = withholdOutcome ? null : peerSummary,
            SelfWeight = settings.SelfEvaluationWeight,
            ManagerWeight = settings.ManagerEvaluationWeight,
            PeerWeight = settings.PeerEvaluationWeight,
            SelfScore = selfEvaluation?.TotalScore,
            ManagerScore = withholdOutcome ? null : managerEvaluation?.TotalScore,
            PeerScore = withholdOutcome ? null
                : peerEvaluations.Any() ? peerEvaluations.Where(e => e.SubmittedDate.HasValue).Average(e => e.TotalScore) : null,
            FinalScore = withholdOutcome ? null : appraisal.OverallScore,
            FinalGrade = withholdOutcome ? null : await GradeNameAsync(appraisal.OverallGradeDefinitionId, cancellationToken),
            // The HR sign-off, not the appraisal's status: an appraisal whose cycle requires an
            // employee acknowledgment stays in Governance after HR has finalised it.
            IsFinalized = hrEvaluation?.SubmittedDate.HasValue == true || appraisal.Status == AppraisalStatus.Completed,
            HRRemarks = withholdOutcome ? null : hrEvaluation?.OverallNotes,
            FinalizedDate = hrEvaluation?.SubmittedDate,
            FinalizedByName = hrEvaluation?.SubmittedDate.HasValue == true ? hrEvaluation.Evaluator?.FullName : null,
            EmployeeAcknowledgedDate = appraisal.EmployeeAcknowledgedDate,
            HasAppeal = appraisal.HasAppeal,
            CurrentAppealStatus = appraisal.CurrentAppealStatus,
            IsRemandedAppeal = appraisal.AppealRemandedDate.HasValue,
            AppealRemandedDate = appraisal.AppealRemandedDate,
            AppealRemandDeadline = appraisal.AppealRemandDeadline,
            IsRemandDeadlineExceeded = appraisal.AppealRemandDeadline.HasValue && DateTime.UtcNow > appraisal.AppealRemandDeadline.Value
        };
    }

    /// <summary>
    /// Helper method to build evaluation summary from an EvaluatorEvaluation. Targets and weights
    /// come from the appraisal's criterion snapshot — what was scored (A12).
    /// </summary>
    private static EvaluationSummaryDto BuildEvaluationSummary(
        EvaluatorEvaluation evaluation, IReadOnlyDictionary<Guid, PerformanceAppraisalCriterionConfig> configsByTemplateItemId)
    {
        PerformanceAppraisalCriterionConfig? ConfigFor(CriterionScore cs)
            => cs.TemplateItemId is Guid id && configsByTemplateItemId.TryGetValue(id, out var c) ? c : null;

        // Competency criteria (non-KPI items) → competency score rows.
        var competencyScores = evaluation.CriterionScores
            .Where(cs => cs.TemplateItem?.CompetencyId != null)
            .Select(cs => new CompetencyScoreSummaryDto
            {
                CriteriaName = cs.TemplateItem?.Competency?.CriteriaName ?? "Unknown",
                Description = cs.TemplateItem?.Competency?.Description,
                NumericScore = cs.NumericScore,
                // Was hard-coded 0 ("would need to look this up"): the item's weight in its section.
                Weight = ConfigFor(cs)?.WeightUsed ?? 0,
                WeightedScore = cs.WeightedScore,
                Comments = cs.Notes
            })
            .ToList();

        // KPI criteria → KPI score rows. The achievement is the one the score used: a restated
        // percentage when calibration or an appeal set one (D-22, flagged), otherwise the actual
        // against the snapshot's target, floor and ceiling. It used to be NumericScore alone —
        // empty for every KPI scored the normal way, by its actual.
        var kpiScores = evaluation.CriterionScores
            .Where(cs => cs.TemplateItem?.KpiDefinitionId != null)
            .Select(cs =>
            {
                var config = ConfigFor(cs);
                return new KpiScoreSummaryDto
                {
                    KpiName = cs.TemplateItem?.KpiDefinition?.KpiName ?? "Unknown",
                    Description = cs.TemplateItem?.KpiDefinition?.Description,
                    TargetValue = config?.KpiTargetValue,
                    ActualValue = cs.ActualValue,
                    AchievementPercentage = cs.NumericScore.HasValue
                        ? (decimal?)cs.NumericScore.Value
                        : cs.ActualValue.HasValue
                            ? AppraisalScoring.KpiAchievementPercent(cs.ActualValue.Value, config?.KpiTargetValue, config?.KpiMinValue, config?.KpiMaxValue)
                            : null,
                    AchievementOverridden = cs.NumericScore.HasValue,
                    Unit = cs.TemplateItem?.KpiDefinition?.Unit,
                    Notes = cs.Notes
                };
            })
            .ToList();

        return new EvaluationSummaryDto
        {
            CompetencyScores = competencyScores,
            KpiScores = kpiScores,
            GeneralComments = evaluation.OverallNotes,
            TotalScore = evaluation.TotalScore
        };
    }

    /// <summary>
    /// Gets list of appraisals for HR review
    /// </summary>
    public async Task<IEnumerable<HRReviewListItemDto>> GetHRReviewListAsync(Guid? cycleId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = TenantAppraisalQuery()
            .Include(a => a.Employee)
                .ThenInclude(e => e.Position)
            .Include(a => a.Employee)
                .ThenInclude(e => e.OrganizationUnit)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.EvaluatorEvaluations)
                // Without this the "finalised by" column can only be a placeholder.
                .ThenInclude(e => e.Evaluator)
            .AsQueryable();

        // Filter by cycle if specified
        if (cycleId.HasValue)
        {
            query = query.Where(a => a.AppraisalCycleId == cycleId.Value);
        }

        // Filter by status if specified. These mirror the hrReviewStatus computed below — an
        // appraisal is finalised when HR has signed off, which is not the same as the appraisal
        // being Completed (acknowledgment can still be outstanding).
        if (!string.IsNullOrEmpty(status))
        {
            switch (status.ToLower())
            {
                case "not started":
                    query = query.Where(a => a.Status == AppraisalStatus.Draft);
                    break;
                case "in review":
                    query = query.Where(a => a.Status == AppraisalStatus.Governance
                        && !a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.HR && e.SubmittedDate != null));
                    break;
                case "finalized":
                    query = query.Where(a => a.Status == AppraisalStatus.Completed
                        || a.EvaluatorEvaluations.Any(e => e.EvaluatorRole == EvaluatorRole.HR && e.SubmittedDate != null));
                    break;
            }
        }

        var appraisals = await query.ToListAsync(cancellationToken);

        // Grade names for the whole page in one query rather than one per row.
        var gradeIds = appraisals
            .Where(a => a.OverallGradeDefinitionId.HasValue)
            .Select(a => a.OverallGradeDefinitionId!.Value)
            .Distinct()
            .ToList();

        var gradeNames = gradeIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _gradeDefinitionRepository.GetQueryable()
                .Where(g => g.TenantId == GetTenantId() && gradeIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.GradeName, cancellationToken);

        // Ready for HR is the gates' answer, as on the review itself (B4).
        var gateStates = await _lifecycle.GetStatesAsync(appraisals.Select(a => a.Id).ToList(), cancellationToken);

        var results = appraisals.Select(a =>
        {
            var settings = a.AppraisalCycle?.AppraisalSettings;
            var selfEval = a.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Self);
            var managerEval = a.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
            var peerEvals = a.EvaluatorEvaluations.Where(e => e.EvaluatorRole == EvaluatorRole.Peer).ToList();
            var hrEval = a.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.HR);

            var isSelfComplete = selfEval?.SubmittedDate.HasValue ?? false;
            var isManagerComplete = managerEval?.SubmittedDate.HasValue ?? false;
            var requiredPeerReviews = settings is { RequirePeerReviews: true } ? settings.MinPeerEvaluators : 0;
            var completedPeerReviews = peerEvals.Count(e => e.SubmittedDate.HasValue);
            var arePeerReviewsComplete = completedPeerReviews >= requiredPeerReviews;
            var isReadyForHR = gateStates.TryGetValue(a.Id, out var gate)
                && AppraisalGates.Check(gate.Block, AppraisalSubStatus.PendingHRReview);

            // Finalisation is the HR sign-off itself, not the appraisal's status: when the cycle
            // requires an acknowledgment the appraisal stays in Governance *after* HR signs off,
            // waiting on the employee. Reading Completed as "finalised" put every one of those in
            // the wrong bucket and left them in HR's queue with nothing to do.
            var isFinalized = hrEval?.SubmittedDate.HasValue == true || a.Status == AppraisalStatus.Completed;

            string hrReviewStatus;
            if (isFinalized)
                hrReviewStatus = "Finalized";
            else if (a.Status == AppraisalStatus.Governance)
                hrReviewStatus = "In Review";
            else
                hrReviewStatus = "Not Started";

            return new HRReviewListItemDto
            {
                AppraisalId = a.Id,
                AppraisalNumber = a.AppraisalNumber,
                EmployeeId = a.EmployeeId,
                EmployeeNumber = a.Employee.EmployeeNumber,
                EmployeeName = $"{a.Employee.FirstName} {a.Employee.LastName}",
                Position = a.Employee.Position?.Title ?? "",
                OrganizationUnit = a.Employee.OrganizationUnit?.Name ?? "",
                AppraisalCycleName = a.AppraisalCycle?.CycleName ?? "",
                CycleYear = a.AppraisalCycle?.Year ?? 0,
                IsSelfEvaluationComplete = isSelfComplete,
                IsManagerEvaluationComplete = isManagerComplete,
                RequiredPeerReviews = requiredPeerReviews,
                CompletedPeerReviews = completedPeerReviews,
                ArePeerReviewsComplete = arePeerReviewsComplete,
                IsReadyForHRReview = isReadyForHR,
                HRReviewStatus = hrReviewStatus,
                IsFinalized = isFinalized,
                FinalizedDate = hrEval?.SubmittedDate,
                FinalizedByName = hrEval?.SubmittedDate.HasValue == true ? hrEval.Evaluator?.FullName : null,
                FinalScore = a.OverallScore,
                FinalGrade = a.OverallGradeDefinitionId.HasValue
                    && gradeNames.TryGetValue(a.OverallGradeDefinitionId.Value, out var gradeName)
                        ? gradeName
                        : null
            };
        }).ToList();

        return results;
    }

    /// <summary>
    /// Approves and finalizes an appraisal.
    /// </summary>
    /// <param name="reviewerId">
    /// The HR employee signing off. Used only when no HR evaluation record exists yet — see
    /// <see cref="EnsureHRReviewEvaluationAsync"/>.
    /// </param>
    public async Task<HRReviewDto> ApproveAndFinalizeAsync(Guid appraisalId, ApproveAppraisalDto dto, Guid? reviewerId = null, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .Include(a => a.Employee)
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");

        // HR signs off at the HR-review step (B1, B4): every step before it behind the appraisal as
        // the cycle requires — the self-evaluation and the peer minimum only when required (they
        // were checked unconditionally), calibration first only under AfterCalibration (it was
        // demanded whatever the timing, so BeforeCalibration could never be finalised), and a
        // step HR's advance waived counts as behind it. Refused with a 422 naming the step.
        await _lifecycle.EnsureAtAsync(appraisalId, "HR cannot sign this appraisal off yet",
            [AppraisalSubStatus.PendingHRReview], cancellationToken);

        var hrEval = await EnsureHRReviewEvaluationAsync(appraisal, reviewerId, cancellationToken);

        // Atomic finalize: HR sign-off + recalculated OverallScore + the status change persist
        // together, so we can never leave a finalized appraisal with a NULL OverallScore. Safe under
        // the retrying execution strategy (the mutations are idempotent on a retry).
        var hrReviewRecord = await EnsureHRReviewRecordAsync(appraisal, hrEval.EvaluatorId, cancellationToken);

        AppraisalSyncResult? sync = null;
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var now = DateTime.UtcNow;

            hrEval.OverallNotes = dto.HRRemarks;
            hrEval.SubmittedDate = now;

            // AppraisalHRReview is the record the gates read to decide whether the HR-review step
            // has been passed; the EvaluatorEvaluation above is what this service's own HR flow
            // reads. Both are written so the step agrees with the sign-off.
            hrReviewRecord.ReviewCompletedDate = now;
            hrReviewRecord.IsApproved = true;
            hrReviewRecord.HRNotes = dto.HRRemarks;

            await _evaluatorEvaluationRepository.UpdateAsync(hrEval);
            await _hrReviewRepository.UpdateAsync(hrReviewRecord);
            await _unitOfWork.SaveChangesAsync(ct);

            // Where the sign-off leaves the appraisal is the gates' call — Completed when it was the
            // last step; waiting on the final conversation, the acknowledgment, or (HR before
            // calibration) the panel otherwise. It used to be Governance-if-acknowledgment-else-
            // Completed, which completed appraisals a required final conversation or a later
            // calibration still held.
            //
            // Settled inside the transaction, so sign-off and score commit together; a committed
            // calibration restatement stands (CalibratedOverallScore) instead of being recomputed
            // away (P-39). Not published here: a talent-pool failure swallowed inside a retrying
            // transaction would leave tracked entities the retry cannot trust.
            sync = await _lifecycle.SyncAsync(appraisalId, AppraisalScoreChangeSource.Settle, publish: false, ct);
            if (!sync.Settled)
                await _scores.SettleAsync(appraisalId, AppraisalScoreChangeSource.Settle, publish: false, ct);
        }, cancellationToken);

        // Theme 9 — the settled rating goes to the talent pools once the sign-off has committed.
        // Best-effort inside PublishAsync: a sync failure must not fail a finalisation that stands.
        await _scores.PublishAsync(appraisalId, cancellationToken);
        var finalScore = appraisal.OverallScore;

        _logger.LogInformation("Appraisal {AppraisalId} approved and finalized by HR", appraisalId);

        // The employee is told when the sign-off releases the outcome to them (P2). With HR before
        // calibration it does not — the panel may still restate the score — and the message used
        // to carry that pre-calibration score.
        var step = sync?.SubStatus;
        if (step is AppraisalSubStatus.PendingAcknowledgment or AppraisalSubStatus.PendingConversation or AppraisalSubStatus.Completed)
        {
            var scoreText = finalScore.HasValue ? $" Final score: {finalScore.Value:0.##}." : string.Empty;
            var awaitingAck = step == AppraisalSubStatus.PendingAcknowledgment;
            await NotifyQuietlyAsync(new[]
            {
                new AppraisalNotificationRequest(
                    appraisal.EmployeeId,
                    AppraisalNotificationType.HRReviewApproved,
                    step switch
                    {
                        AppraisalSubStatus.PendingAcknowledgment => "Your appraisal is ready to acknowledge",
                        AppraisalSubStatus.PendingConversation => "Your appraisal has been signed off",
                        _ => "Your appraisal has been finalised",
                    },
                    step switch
                    {
                        AppraisalSubStatus.PendingAcknowledgment => $"HR has signed off your appraisal.{scoreText} Open it to review the outcome and acknowledge it.",
                        AppraisalSubStatus.PendingConversation => $"HR has signed off your appraisal.{scoreText} Your manager will take you through it at the final review conversation.",
                        _ => $"HR has signed off your appraisal.{scoreText}",
                    },
                    appraisal.AppraisalCycle?.CycleName,
                    $"/me/performance/appraisals/{appraisal.Id}",
                    appraisal.Id,
                    appraisal.Employee?.FullName,
                    awaitingAck ? NotificationUrgency.Warning : NotificationUrgency.Normal),
            }, cancellationToken);
        }

        // Return updated HR review
        return await GetHRReviewAsync(appraisalId, null, cancellationToken);
    }

    /// <summary>
    /// Returns an appraisal to the manager for corrections, reopening their evaluation.
    /// </summary>
    /// <param name="reviewerId">The HR employee returning it — used only when no HR review record exists yet.</param>
    public async Task<HRReviewDto> ReturnToManagerAsync(Guid appraisalId, ReturnAppraisalDto dto, Guid? reviewerId = null, CancellationToken cancellationToken = default)
    {
        var appraisal = await TenantAppraisalQuery()
            .Include(a => a.AppraisalCycle)
            .Include(a => a.Employee)
            .Include(a => a.EvaluatorEvaluations)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken);

        if (appraisal == null)
            throw new ArgumentException($"Appraisal with ID '{appraisalId}' not found.");

        if (string.IsNullOrWhiteSpace(dto.HRRemarks))
            throw new ArgumentException("HR remarks are required when returning to manager.");

        var hrEval = await EnsureHRReviewEvaluationAsync(appraisal, reviewerId, cancellationToken);
        var hrReviewRecord = await EnsureHRReviewRecordAsync(appraisal, hrEval.EvaluatorId, cancellationToken);

        hrEval.OverallNotes = dto.HRRemarks;
        // The sign-off is explicitly not complete — leaving ReviewCompletedDate null keeps the
        // computed phase on HRReview, which is where the appraisal actually is.
        hrReviewRecord.IsApproved = false;
        hrReviewRecord.ReviewCompletedDate = null;
        hrReviewRecord.HRNotes = dto.HRRemarks;

        // Return to Active so manager can revise evaluation
        appraisal.Status = AppraisalStatus.Active;

        // Reopen manager evaluation (remove submitted date to allow edits)
        var managerEval = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.Manager);
        if (managerEval != null)
        {
            managerEval.SubmittedDate = null; // Reopen for editing
            await _evaluatorEvaluationRepository.UpdateAsync(managerEval);
        }

        await _evaluatorEvaluationRepository.UpdateAsync(hrEval);
        await _hrReviewRepository.UpdateAsync(hrReviewRecord);
        await _appraisalRepository.UpdateAsync(appraisal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal {AppraisalId} returned to manager by HR", appraisalId);

        if (appraisal.Employee?.ManagerId is Guid managerId && managerId != Guid.Empty)
        {
            await NotifyQuietlyAsync(new[]
            {
                new AppraisalNotificationRequest(
                    managerId,
                    AppraisalNotificationType.ActionRequired,
                    $"HR returned {appraisal.Employee?.FullName}'s appraisal",
                    $"HR asked for changes before this appraisal can be finalised: {dto.HRRemarks}",
                    appraisal.AppraisalCycle?.CycleName,
                    $"/hr/performance/team-appraisals/{appraisal.Id}",
                    appraisal.Id,
                    appraisal.Employee?.FullName,
                    NotificationUrgency.Warning),
            }, cancellationToken);
        }

        // Return updated HR review
        return await GetHRReviewAsync(appraisalId, null, cancellationToken);
    }

    /// <summary>
    /// Returns the appraisal's HR <see cref="EvaluatorEvaluation"/>, creating it if the appraisal
    /// reached HR without one.
    ///
    /// ⚠ This record used to be created only by <see cref="ProgressToHRReviewAsync"/>, which had
    /// no caller and no route — so it never existed, and both HR actions failed on
    /// "HR evaluation not found". Finalisation is now self-healing: the signed-in HR reviewer
    /// becomes the assignee when nothing assigned one earlier.
    /// </summary>
    private async Task<EvaluatorEvaluation> EnsureHRReviewEvaluationAsync(
        PerformanceAppraisal appraisal, Guid? reviewerId, CancellationToken cancellationToken)
    {
        var existing = appraisal.EvaluatorEvaluations.FirstOrDefault(e => e.EvaluatorRole == EvaluatorRole.HR)
            ?? await TenantEvaluationQuery()
                .FirstOrDefaultAsync(e => e.AppraisalId == appraisal.Id && e.EvaluatorRole == EvaluatorRole.HR, cancellationToken);

        if (existing != null) return existing;

        var assignedTo = reviewerId is Guid explicitReviewer && explicitReviewer != Guid.Empty
            ? explicitReviewer
            : await AssignHRReviewerAsync(appraisal, cancellationToken);

        if (assignedTo == Guid.Empty)
            throw new InvalidOperationException(
                "No HR reviewer could be resolved for this appraisal. Set a default HR reviewer on the "
                + "appraisal settings profile, or sign in as an HR user linked to an employee record.");

        var hrEval = new EvaluatorEvaluation
        {
            TenantId = appraisal.TenantId,
            AppraisalId = appraisal.Id,
            EvaluatorId = assignedTo,
            EvaluatorRole = EvaluatorRole.HR,
            EvaluatorWeight = 0m,   // HR governs the score; it does not carry weight in it.
            StartedDate = DateTime.UtcNow,
        };

        await _evaluatorEvaluationRepository.AddAsync(hrEval);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        appraisal.EvaluatorEvaluations.Add(hrEval);

        _logger.LogInformation(
            "Created HR evaluation for appraisal {AppraisalId}, assigned to {ReviewerId}.", appraisal.Id, assignedTo);

        return hrEval;
    }

    /// <summary>Returns the open <see cref="AppraisalHRReview"/> for this appraisal, creating one if needed.</summary>
    private async Task<AppraisalHRReview> EnsureHRReviewRecordAsync(
        PerformanceAppraisal appraisal, Guid reviewerId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var existing = await _hrReviewRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.AppraisalId == appraisal.Id)
            .OrderByDescending(r => r.ReviewStartedDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null) return existing;

        var review = new AppraisalHRReview
        {
            TenantId = appraisal.TenantId,
            AppraisalId = appraisal.Id,
            ReviewedByHRId = reviewerId,
            ReviewStartedDate = DateTime.UtcNow,
        };

        await _hrReviewRepository.AddAsync(review);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return review;
    }

    #endregion

    #region Section Building Helpers

    /// <summary>
    /// Builds the section/item tree for a self-evaluation context from the appraisal's
    /// CriterionConfig snapshot and (optionally) the linked AppraisalTemplate sections.
    /// Falls back to a single synthetic section when no template is attached.
    /// </summary>
    private List<SelfEvaluationSectionDto> BuildSelfEvaluationSections(
        PerformanceAppraisal appraisal,
        EvaluatorEvaluation? selfEval,
        IEnumerable<AppraisalCustomQuestionResponse>? customResponses = null)
    {
        if (!appraisal.CriterionConfigs.Any()) return new();

        var configsByTemplateItemId = appraisal.CriterionConfigs.ToDictionary(cc => cc.TemplateKey());
        var scoresByTemplateItemId   = selfEval?.CriterionScores.ToDictionary(cs => cs.TemplateKey())
                                  ?? new Dictionary<Guid, CriterionScore>();
        var goalsByKpiDefId     = appraisal.Goals
                                  .Where(g => g.KpiDefinitionId.HasValue)
                                  .ToLookup(g => g.KpiDefinitionId!.Value);
        var responsesByTemplateItemId = (customResponses ?? Enumerable.Empty<AppraisalCustomQuestionResponse>())
                                         .ToDictionary(r => r.TemplateItemId);

        if (appraisal.Template?.Sections == null || !appraisal.Template.Sections.Any())
        {
            // Fallback: single section from all configured criteria
            var fallbackItems = appraisal.CriterionConfigs
                .Select(cc =>
                {
                    scoresByTemplateItemId.TryGetValue(cc.TemplateKey(), out var score);
                    var kpiDef = cc.TemplateItem?.KpiDefinition;
                    var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                    return MapSelfItem(cc, cc.TemplateItem?.Competency, kpiDef, goal, score);
                })
                .ToList();
            return new List<SelfEvaluationSectionDto>
            {
                new() { SectionName = "Evaluation Criteria", SectionWeight = 100, Items = fallbackItems }
            };
        }

        return appraisal.Template.Sections
            .OrderBy(s => s.DisplayOrder)
            .Select(section =>
            {
                var items = section.TemplateItems
                    .OrderBy(ti => ti.DisplayOrder)
                    .Where(ti => configsByTemplateItemId.ContainsKey(ti.Id))
                    .Select(ti =>
                    {
                        var config = configsByTemplateItemId[ti.Id];
                        scoresByTemplateItemId.TryGetValue(ti.Id, out var score);
                        var kpiDef = config.TemplateItem?.KpiDefinition;
                        var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                        var item   = MapSelfItem(config, config.TemplateItem?.Competency, kpiDef, goal, score);
                        item.DisplayOrder   = ti.DisplayOrder;
                        item.CustomQuestion = ti.CustomQuestion;
                        return item;
                    })
                    .ToList();

                var customQuestions = section.TemplateItems
                    .OrderBy(ti => ti.DisplayOrder)
                    .Where(ti => ti.CompetencyId == null && ti.KpiDefinitionId == null
                              && !string.IsNullOrWhiteSpace(ti.CustomQuestion))
                    .Select(ti =>
                    {
                        responsesByTemplateItemId.TryGetValue(ti.Id, out var existing);
                        return new SelfEvaluationCustomQuestionDto
                        {
                            TemplateItemId  = ti.Id,
                            QuestionText    = ti.CustomQuestion!,
                            DisplayOrder    = ti.DisplayOrder,
                            ExistingResponse = existing?.ResponseText,
                            IsSubmitted      = !(existing?.IsDraft ?? true)
                        };
                    })
                    .ToList();

                return new SelfEvaluationSectionDto
                {
                    SectionId          = section.Id,
                    SectionName        = section.SectionName,
                    SectionDescription = section.Description,
                    DisplayOrder       = section.DisplayOrder,
                    SectionWeight      = section.ScoredWeight(configsByTemplateItemId),
                    Items              = items,
                    CustomQuestions    = customQuestions
                };
            })
            .Where(s => s.Items.Any() || s.CustomQuestions.Any())
            .ToList();
    }

    private SelfEvaluationItemDto MapSelfItem(
        PerformanceAppraisalCriterionConfig config,
        AppraisalCompetency? competency,
        KpiDefinition? kpiDef,
        EmployeeGoal? goal,
        CriterionScore? existingScore)
    {
        // The snapshot's target, floor and ceiling, and nothing else: they are what the score is
        // computed from. Falling back to the live goal showed a target the score never used (A12).
        var effectiveTarget = config.KpiTargetValue;
        var effectiveMin    = config.KpiMinValue;
        var effectiveMax    = config.KpiMaxValue;

        return new SelfEvaluationItemDto
        {
            TemplateItemId          = config.TemplateKey(),
            CriterionConfigId       = config.Id,
            ItemName                = competency?.CriteriaName ?? kpiDef?.KpiName ?? string.Empty,
            ItemDescription         = competency?.Description,
            ItemWeight              = config.WeightUsed,
            RequireEvidence         = competency?.RequireEvidence ?? false,
            KpiDefinitionId         = kpiDef?.Id,
            KpiUnit                 = kpiDef?.Unit ?? goal?.Unit,
            MeasurementType         = kpiDef?.MeasurementType,
            KpiTargetValue          = effectiveTarget,
            KpiMinValue             = effectiveMin,
            KpiMaxValue             = effectiveMax,
            GradeRanges             = config.GradeRanges.Select(gr => new EvaluationGradeRangeDto
            {
                GradeDefinitionId = gr.GradeDefinitionId,
                GradeName         = gr.GradeDefinition?.GradeName ?? string.Empty,
                GradeDescription  = gr.GradeDefinition?.Description,
                LowScore          = gr.LowScore,
                HighScore         = gr.HighScore
            }).ToList(),
            ExistingCriterionScoreId = existingScore?.Id,
            ExistingNumericScore     = existingScore?.NumericScore,
            ExistingActualValue      = existingScore?.ActualValue,
            ExistingNotes            = existingScore?.Notes,
            ExistingEvidenceLinks    = existingScore?.EvidenceLinks,
            AchievementPercent       = KpiAchievementShown(existingScore, config),
        };
    }

    /// <summary>
    /// The achievement a KPI row shows: the restated percentage when calibration or an appeal set
    /// one (D-22), otherwise the actual measured the way the score measures it — against the
    /// snapshot's floor, target and ceiling, clamped (A12). A bare actual ÷ target showed 150 %
    /// for an actual the score caps at 100 %, and ignored the floor altogether.
    /// </summary>
    private static decimal? KpiAchievementShown(CriterionScore? score, PerformanceAppraisalCriterionConfig config)
    {
        if (score is null) return null;
        if (score.NumericScore.HasValue && config.TemplateItem?.KpiDefinitionId != null)
            return score.NumericScore.Value;
        if (score.ActualValue is not decimal actual || config.KpiTargetValue is null)
            return null;

        return Math.Round(
            AppraisalScoring.KpiAchievementPercent(actual, config.KpiTargetValue, config.KpiMinValue, config.KpiMaxValue),
            1, MidpointRounding.AwayFromZero);
    }

    /// <summary>A KPI row whose achievement was restated by calibration or an appeal (D-22, A14).</summary>
    private static bool IsAchievementOverridden(CriterionScore? score, PerformanceAppraisalCriterionConfig config)
        => score?.NumericScore.HasValue == true && config.TemplateItem?.KpiDefinitionId != null;

    // ─────────────────────────────────────────────────────────────────────────

    private List<ManagerEvaluationSectionDto> BuildManagerEvaluationSections(
        PerformanceAppraisal appraisal,
        EvaluatorEvaluation? selfEval,
        EvaluatorEvaluation? managerEval,
        List<Guid> appealedTemplateItemIds)
    {
        if (!appraisal.CriterionConfigs.Any()) return new();

        var configsByTemplateItemId       = appraisal.CriterionConfigs.ToDictionary(cc => cc.TemplateKey());
        var selfScoresByTemplateItemId    = selfEval?.CriterionScores.ToDictionary(cs => cs.TemplateKey())
                                           ?? new Dictionary<Guid, CriterionScore>();
        var managerScoresByTemplateItemId = managerEval?.CriterionScores.ToDictionary(cs => cs.TemplateKey())
                                           ?? new Dictionary<Guid, CriterionScore>();
        var goalsByKpiDefId               = appraisal.Goals
                                           .Where(g => g.KpiDefinitionId.HasValue)
                                           .ToLookup(g => g.KpiDefinitionId!.Value);
        var appealedSet                   = appealedTemplateItemIds.ToHashSet();

        if (appraisal.Template?.Sections == null || !appraisal.Template.Sections.Any())
        {
            var fallbackItems = appraisal.CriterionConfigs
                .Select(cc =>
                {
                    selfScoresByTemplateItemId.TryGetValue(cc.TemplateKey(), out var selfScore);
                    managerScoresByTemplateItemId.TryGetValue(cc.TemplateKey(), out var managerScore);
                    var kpiDef = cc.TemplateItem?.KpiDefinition;
                    var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                    return MapManagerItem(cc, cc.TemplateItem?.Competency, kpiDef, goal, selfScore, managerScore, appealedSet.Contains(cc.TemplateKey()));
                })
                .ToList();
            return new List<ManagerEvaluationSectionDto>
            {
                new() { SectionName = "Evaluation Criteria", SectionWeight = 100, Items = fallbackItems }
            };
        }

        return appraisal.Template.Sections
            .OrderBy(s => s.DisplayOrder)
            .Select(section =>
            {
                var items = section.TemplateItems
                    .OrderBy(ti => ti.DisplayOrder)
                    .Where(ti => configsByTemplateItemId.ContainsKey(ti.Id))
                    .Select(ti =>
                    {
                        var config = configsByTemplateItemId[ti.Id];
                        selfScoresByTemplateItemId.TryGetValue(ti.Id, out var selfScore);
                        managerScoresByTemplateItemId.TryGetValue(ti.Id, out var managerScore);
                        var kpiDef = config.TemplateItem?.KpiDefinition;
                        var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                        var item   = MapManagerItem(config, config.TemplateItem?.Competency, kpiDef, goal, selfScore, managerScore, appealedSet.Contains(ti.Id));
                        item.DisplayOrder   = ti.DisplayOrder;
                        item.CustomQuestion = ti.CustomQuestion;
                        return item;
                    })
                    .ToList();
                return new ManagerEvaluationSectionDto
                {
                    SectionId          = section.Id,
                    SectionName        = section.SectionName,
                    SectionDescription = section.Description,
                    DisplayOrder       = section.DisplayOrder,
                    SectionWeight      = section.ScoredWeight(configsByTemplateItemId),
                    Items              = items
                };
            })
            .Where(s => s.Items.Any())
            .ToList();
    }

    private ManagerEvaluationItemDto MapManagerItem(
        PerformanceAppraisalCriterionConfig config,
        AppraisalCompetency? competency,
        KpiDefinition? kpiDef,
        EmployeeGoal? goal,
        CriterionScore? selfScore,
        CriterionScore? managerScore,
        bool isAppealed) => new()
    {
        TemplateItemId           = config.TemplateKey(),
        CriterionConfigId        = config.Id,
        ItemName                 = competency?.CriteriaName ?? kpiDef?.KpiName ?? string.Empty,
        ItemDescription          = competency?.Description,
        ItemWeight               = config.WeightUsed,
        RequireEvidence          = competency?.RequireEvidence ?? false,
        KpiDefinitionId          = kpiDef?.Id,
        KpiUnit                  = kpiDef?.Unit ?? goal?.Unit,
        MeasurementType          = kpiDef?.MeasurementType,
        // The snapshot's values — the ones scored. This read the live goal, so the manager saw a
        // target the score never used whenever the goal moved after generation (A12).
        KpiTargetValue           = config.KpiTargetValue,
        KpiMinValue              = config.KpiMinValue,
        KpiMaxValue              = config.KpiMaxValue,
        GradeRanges              = config.GradeRanges.Select(gr => new EvaluationGradeRangeDto
        {
            GradeDefinitionId = gr.GradeDefinitionId,
            GradeName         = gr.GradeDefinition?.GradeName ?? string.Empty,
            GradeDescription  = gr.GradeDefinition?.Description,
            LowScore          = gr.LowScore,
            HighScore         = gr.HighScore
        }).ToList(),
        // Employee self-score (read-only reference)
        EmployeeSelfCriterionScoreId  = selfScore?.Id,
        EmployeeSelfNumericScore      = selfScore?.NumericScore,
        EmployeeSelfActualValue       = selfScore?.ActualValue,
        EmployeeSelfNotes             = selfScore?.Notes,
        EmployeeSelfEvidenceLinks     = selfScore?.EvidenceLinks,
        // Manager score
        ManagerCriterionScoreId       = managerScore?.Id,
        ManagerNumericScore           = managerScore?.NumericScore,
        ManagerActualValue            = managerScore?.ActualValue,
        ManagerNotes                  = managerScore?.Notes,
        ManagerEvidenceLinks          = managerScore?.EvidenceLinks,
        ManagerAchievementPercent     = KpiAchievementShown(managerScore, config),
        ManagerAchievementOverridden  = IsAchievementOverridden(managerScore, config),
        IsAppealed                    = isAppealed,
    };

    // ─────────────────────────────────────────────────────────────────────────

    private List<SubmittedEvaluationSectionDto> BuildSubmittedEvaluationSections(
        PerformanceAppraisal appraisal,
        EvaluatorEvaluation selfEval)
    {
        if (!appraisal.CriterionConfigs.Any()) return new();

        var configsByTemplateItemId = appraisal.CriterionConfigs.ToDictionary(cc => cc.TemplateKey());
        var scoresByTemplateItemId   = selfEval.CriterionScores.ToDictionary(cs => cs.TemplateKey());
        var goalsByKpiDefId     = appraisal.Goals
                                  .Where(g => g.KpiDefinitionId.HasValue)
                                  .ToLookup(g => g.KpiDefinitionId!.Value);

        if (appraisal.Template?.Sections == null || !appraisal.Template.Sections.Any())
        {
            var fallbackItems = appraisal.CriterionConfigs
                .Select(cc =>
                {
                    scoresByTemplateItemId.TryGetValue(cc.TemplateKey(), out var score);
                    var kpiDef = cc.TemplateItem?.KpiDefinition;
                    var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                    return MapSubmittedItem(cc, cc.TemplateItem?.Competency, kpiDef, goal, score);
                })
                .ToList();
            return new List<SubmittedEvaluationSectionDto>
            {
                new() { SectionName = "Evaluation Criteria", SectionWeight = 100, Items = fallbackItems }
            };
        }

        return appraisal.Template.Sections
            .OrderBy(s => s.DisplayOrder)
            .Select(section =>
            {
                var items = section.TemplateItems
                    .OrderBy(ti => ti.DisplayOrder)
                    .Where(ti => configsByTemplateItemId.ContainsKey(ti.Id))
                    .Select(ti =>
                    {
                        var config = configsByTemplateItemId[ti.Id];
                        scoresByTemplateItemId.TryGetValue(ti.Id, out var score);
                        var kpiDef = config.TemplateItem?.KpiDefinition;
                        var goal   = kpiDef != null ? goalsByKpiDefId[kpiDef.Id].FirstOrDefault() : null;
                        return MapSubmittedItem(config, config.TemplateItem?.Competency, kpiDef, goal, score);
                    })
                    .ToList();
                return new SubmittedEvaluationSectionDto
                {
                    SectionName   = section.SectionName,
                    SectionWeight = section.ScoredWeight(configsByTemplateItemId),
                    DisplayOrder  = section.DisplayOrder,
                    Items         = items
                };
            })
            .Where(s => s.Items.Any())
            .ToList();
    }

    private SubmittedEvaluationItemDto MapSubmittedItem(
        PerformanceAppraisalCriterionConfig config,
        AppraisalCompetency? competency,
        KpiDefinition? kpiDef,
        EmployeeGoal? goal,
        CriterionScore? score) => new()
    {
        TemplateItemId      = config.TemplateKey(),
        ItemName            = competency?.CriteriaName ?? kpiDef?.KpiName ?? string.Empty,
        ItemDescription     = competency?.Description,
        ItemWeight          = config.WeightUsed,
        NumericScore        = score?.NumericScore,
        ActualValue         = score?.ActualValue,
        Notes               = score?.Notes,
        EvidenceLinks       = score?.EvidenceLinks,
        KpiUnit             = kpiDef?.Unit ?? goal?.Unit,
        // The snapshot's target and the score's own achievement rule (A12).
        KpiTargetValue      = config.KpiTargetValue,
        AchievementPercent  = KpiAchievementShown(score, config)
    };

    #endregion
}

#endregion Performance Appraisal

