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

#region Appraisal Grade Definition

public class AppraisalGradeDefinitionService : IAppraisalGradeDefinitionService
{
    private readonly IGenericRepository<AppraisalGradeDefinition> _gradeDefinitionRepository;
    private readonly IGenericRepository<TemplateItemGradeRange> _templateBandRepository;
    private readonly IGenericRepository<PerformanceAppraisalCriterionConfigGradeRange> _snapshotBandRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<CriterionScore> _criterionScoreRepository;
    private readonly IGenericRepository<AppraisalScoreChange> _scoreChangeRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LeaveService> _logger;

    public AppraisalGradeDefinitionService(
        IGenericRepository<AppraisalGradeDefinition> gradeDefinitionRepository,
        IGenericRepository<TemplateItemGradeRange> templateBandRepository,
        IGenericRepository<PerformanceAppraisalCriterionConfigGradeRange> snapshotBandRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<CriterionScore> criterionScoreRepository,
        IGenericRepository<AppraisalScoreChange> scoreChangeRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<LeaveService> logger)
    {
        _gradeDefinitionRepository = gradeDefinitionRepository;
        _templateBandRepository = templateBandRepository;
        _snapshotBandRepository = snapshotBandRepository;
        _appraisalRepository = appraisalRepository;
        _criterionScoreRepository = criterionScoreRepository;
        _scoreChangeRepository = scoreChangeRepository;
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

    // A grade definition owned by another tenant is reported as missing rather than forbidden, so the
    // endpoints do not confirm that the id exists elsewhere.
    private async Task<AppraisalGradeDefinition> GetOwnedAsync(Guid id)
    {
        var entity = await _gradeDefinitionRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Grade definition with ID '{id}' not found.");
        return entity;
    }

    /// <summary>
    /// Refuses an overall band the one grade resolver could not use (performance closure A2).
    /// A band needs both bounds inside 0–100, a rating for the talent feed, and no overlap with
    /// another active band — two bands claiming one score made the grade depend on row order
    /// (P-6). A definition with no bounds is an item-only grade and is not checked. These are
    /// rules, not missing records: they answer 422 (E-g1 — the update answered 404, the create 400).
    /// </summary>
    private async Task ValidateOverallBandAsync(AppraisalGradeDefinition candidate, CancellationToken cancellationToken)
    {
        var min = candidate.OverallMinScore;
        var max = candidate.OverallMaxScore;
        if (min is null && max is null) return;

        if (min is null || max is null)
            throw new InvalidOperationException("An overall band needs both a minimum and a maximum score.");
        if (min < AppraisalScoring.MinScore || max > AppraisalScoring.MaxScore || min > max)
            throw new InvalidOperationException(
                $"An overall band must lie within {AppraisalScoring.MinScore:0}–{AppraisalScoring.MaxScore:0} with its minimum no higher than its maximum.");
        if (candidate.MappedRating is null)
            throw new InvalidOperationException(
                "An overall band needs a mapped rating — it is what the talent pools and the rating reports read.");

        if (!candidate.IsActive) return;

        var tenantId = GetTenantId();
        var clash = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId
                     && g.Id != candidate.Id
                     && g.IsActive
                     && g.OverallMinScore != null
                     && g.OverallMaxScore != null
                     && g.OverallMinScore <= max
                     && min <= g.OverallMaxScore)
            .Select(g => new { g.GradeName, g.OverallMinScore, g.OverallMaxScore })
            .FirstOrDefaultAsync(cancellationToken);

        if (clash != null)
            throw new InvalidOperationException(
                $"The band {min:0.##}–{max:0.##} overlaps \"{clash.GradeName}\" ({clash.OverallMinScore:0.##}–{clash.OverallMaxScore:0.##}). Each score must fall in one band only.");
    }

    /// <summary>
    /// Refuses a change that opens a gap in the grade scale (performance closure E-g1, D-79). A band's minimum is its
    /// threshold (<see cref="PerformanceRatingResolver"/>), so a score in a gap — 50 between 0–40 and 60–100 — took
    /// the band below, ten points past its maximum. Bands meet edge to edge: on the whole-number convention the next
    /// band starts one point above the last one's maximum (90, then 91 — a score of 90.5 is in the lower band, as the
    /// resolver documents). <paramref name="after"/> is the definition as it would be saved, or null when it is
    /// deleted. A scale that already had a gap is not refused for it — only for a change that widens one.
    /// </summary>
    private async Task EnsureNoNewGapAsync(Guid id, AppraisalGradeDefinition? after, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var bands = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId && g.IsActive && g.OverallMinScore != null && g.OverallMaxScore != null)
            .Select(g => new { g.Id, Min = g.OverallMinScore!.Value, Max = g.OverallMaxScore!.Value })
            .ToListAsync(cancellationToken);

        var before = bands.Select(b => (b.Min, b.Max)).ToList();
        var changed = bands.Where(b => b.Id != id).Select(b => (b.Min, b.Max)).ToList();
        if (after is { IsActive: true, OverallMinScore: decimal min, OverallMaxScore: decimal max })
            changed.Add((min, max));

        var gapsBefore = Gaps(before);
        var gapsAfter = Gaps(changed);
        if (gapsAfter.Sum(g => g.Width) <= gapsBefore.Sum(g => g.Width))
            return;

        var fresh = gapsAfter.Where(g => !gapsBefore.Contains(g)).ToList();
        var gap = fresh.Count > 0 ? fresh[0] : gapsAfter[0];
        throw new InvalidOperationException(
            $"That leaves a gap in the grade scale between {gap.Below:0.##} and {gap.Above:0.##}: a score there would fall in "
            + $"no band and take the band below. Bands meet edge to edge — the next band starts one point above the last one's maximum.");
    }

    /// <summary>The scores no band covers between the lowest and highest band (Below and Above are the bounds either side).</summary>
    private static List<(decimal Below, decimal Above, decimal Width)> Gaps(List<(decimal Min, decimal Max)> bands)
    {
        var gaps = new List<(decimal Below, decimal Above, decimal Width)>();
        decimal? reach = null;
        foreach (var band in bands.OrderBy(b => b.Min))
        {
            if (reach is decimal r && band.Min > r + 1)
                gaps.Add((r, band.Min, band.Min - r - 1));
            reach = reach is decimal current ? Math.Max(current, band.Max) : band.Max;
        }
        return gaps;
    }

    /// <summary>
    /// What uses a grade (E-g1, D-78): the bands on template criteria (generation and the forms read them, and drop
    /// a band on a deleted grade), and appraisals — their frozen bands, their criterion scores, their overall grade
    /// and their recorded score changes. Null when nothing does. A row under a deleted template, section or item is
    /// not a use.
    /// </summary>
    private async Task<string?> DescribeUseAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var criteria = await _templateBandRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.GradeDefinitionId == id
                     && !r.TemplateItem.IsDeleted && !r.TemplateItem.Section.IsDeleted
                     && !r.TemplateItem.Section.AppraisalTemplate.IsDeleted)
            .Select(r => r.AppraisalTemplateItemId)
            .Distinct()
            .CountAsync(cancellationToken);

        var appraisalIds = new HashSet<Guid>();
        appraisalIds.UnionWith(await _snapshotBandRepository.GetQueryable()
            .Where(r => r.TenantId == tenantId && r.GradeDefinitionId == id && !r.CriterionConfig.IsDeleted)
            .Select(r => r.CriterionConfig.PerformanceAppraisalId)
            .Distinct()
            .ToListAsync(cancellationToken));
        appraisalIds.UnionWith(await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId && a.OverallGradeDefinitionId == id)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken));
        appraisalIds.UnionWith(await _criterionScoreRepository.GetQueryable()
            .Where(s => s.TenantId == tenantId && s.GradeDefinitionId == id && !s.EvaluatorEvaluation.IsDeleted)
            .Select(s => s.EvaluatorEvaluation.AppraisalId)
            .Distinct()
            .ToListAsync(cancellationToken));
        appraisalIds.UnionWith(await _scoreChangeRepository.GetQueryable()
            .Where(c => c.TenantId == tenantId && (c.FromGradeDefinitionId == id || c.ToGradeDefinitionId == id))
            .Select(c => c.PerformanceAppraisalId)
            .Distinct()
            .ToListAsync(cancellationToken));

        return DefinitionUse.Describe(
            new DefinitionUse.Use(criteria, "a template criterion's bands", "template criteria's bands"),
            new DefinitionUse.Use(appraisalIds.Count, "an appraisal", "appraisals"));
    }

    public async Task<AppraisalGradeDefinitionDto> CreateAsync(CreateAppraisalGradeDefinitionDto createDto, CancellationToken cancellationToken = default)
    {
        var gradeDefinition = createDto.ToEntity();
        gradeDefinition.TenantId = GetTenantId();

        await ValidateOverallBandAsync(gradeDefinition, cancellationToken);
        await EnsureNoNewGapAsync(gradeDefinition.Id, gradeDefinition, cancellationToken);

        await _gradeDefinitionRepository.AddAsync(gradeDefinition);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Appraisal grade definition created successfully: {gradeDefinitionId}", gradeDefinition.Id);

        return gradeDefinition.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id);

        // A grade in use is not deleted (E-g1, D-78): its bands dropped out of generation and the forms, and the
        // appraisals graded with it lost the name of their grade.
        var uses = await DescribeUseAsync(id, cancellationToken);
        if (uses != null)
            throw DefinitionUse.DeleteRefused("grade", entity.GradeName, uses);
        await EnsureNoNewGapAsync(id, null, cancellationToken);

        await _gradeDefinitionRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Grade definition deleted: {id}", id);

        return true;
    }

    public async Task<IEnumerable<AppraisalGradeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var gradeDefinitions = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        return gradeDefinitions.ToDtoList();
    }

    public async Task<AppraisalGradeDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gradeDefinition = await _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == GetTenantId())
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (gradeDefinition == null)
            throw new ArgumentException($"Grade definition with ID '{id}' not found.");

        return gradeDefinition.ToDto();
    }

    public async Task<PagedResult<AppraisalGradeDefinitionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var definitionsQuery = _gradeDefinitionRepository.GetQueryable()
            .Where(g => g.TenantId == tenantId);
        var totalCount = await definitionsQuery.CountAsync(cancellationToken);

        var pagedGrades = await definitionsQuery.OrderBy(g => g.GradeName)
                                                .Skip((pageNumber - 1) * pageSize)
                                                .Take(pageSize)
                                                .ToListAsync(cancellationToken);

        var gradeDtos = pagedGrades.ToDtoList();

        return new PagedResult<AppraisalGradeDefinitionDto>
        {
            Items = gradeDtos,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<AppraisalGradeDefinitionDto> UpdateAsync(UpdateAppraisalGradeDefinitionDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(updateDto.Id);

        // While a grade is in use its band, rating and active flag stay as they are (E-g1, D-78): a moved band
        // re-graded every appraisal the resolver reads next, past what the graded ones were told.
        var meaningChanges = updateDto.OverallMinScore != entity.OverallMinScore
                          || updateDto.OverallMaxScore != entity.OverallMaxScore
                          || updateDto.MappedRating != entity.MappedRating
                          || updateDto.IsActive != entity.IsActive;
        if (meaningChanges)
        {
            var uses = await DescribeUseAsync(entity.Id, cancellationToken);
            if (uses != null)
                throw DefinitionUse.ChangeRefused("grade", entity.GradeName, uses, "score band, rating and active flag");
        }

        updateDto.UpdateEntity(entity);

        await ValidateOverallBandAsync(entity, cancellationToken);
        await EnsureNoNewGapAsync(entity.Id, entity, cancellationToken);

        await _gradeDefinitionRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Appraisal grade definition updated: {Id}", updateDto.Id);

        return entity.ToDto();
    }
}

#endregion Appraisal Grade Definition
