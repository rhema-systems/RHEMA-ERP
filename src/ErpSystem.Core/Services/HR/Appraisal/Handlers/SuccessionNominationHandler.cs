using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Handlers;

/// <summary>
/// Theme 9 handler. On approval of a SuccessionNomination recommendation, adds the appraised
/// employee to a per-tenant default "Appraisal Nominations" talent pool (created on demand),
/// seeding the cached performance rating from the appraisal's overall score.
/// </summary>
public class SuccessionNominationHandler : IOutcomeRecommendationHandler
{
    public const string DefaultPoolName = "Appraisal Nominations";

    private readonly IGenericRepository<TalentPool> _poolRepository;
    private readonly IGenericRepository<TalentPoolMember> _memberRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IPerformanceRatingResolver _ratingResolver;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SuccessionNominationHandler> _logger;

    public SuccessionNominationHandler(
        IGenericRepository<TalentPool> poolRepository,
        IGenericRepository<TalentPoolMember> memberRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IPerformanceRatingResolver ratingResolver,
        IUnitOfWork unitOfWork,
        ILogger<SuccessionNominationHandler> logger)
    {
        _poolRepository = poolRepository;
        _memberRepository = memberRepository;
        _appraisalRepository = appraisalRepository;
        _ratingResolver = ratingResolver;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public RecommendationType Type => RecommendationType.SuccessionNomination;

    public async Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(
        AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == recommendation.PerformanceAppraisalId, cancellationToken);
        if (appraisal == null)
        {
            _logger.LogWarning("SuccessionNominationHandler: appraisal {Id} not found", recommendation.PerformanceAppraisalId);
            return null;
        }

        var ownerId = recommendation.ApprovedById ?? appraisal.EmployeeId;

        // Configurable pool name + default readiness (AppraisalSettings), with the prior constants as fallback.
        var config = await _appraisalRepository.GetQueryable()
            .Where(a => a.Id == recommendation.PerformanceAppraisalId)
            .Select(a => new
            {
                PoolName = a.AppraisalCycle.AppraisalSettings.SuccessionPoolName,
                Readiness = (ReadinessLevel?)a.AppraisalCycle.AppraisalSettings.SuccessionDefaultReadiness
            })
            .FirstOrDefaultAsync(cancellationToken);

        var poolName = !string.IsNullOrWhiteSpace(config?.PoolName) ? config!.PoolName : DefaultPoolName;
        var defaultReadiness = config?.Readiness ?? ReadinessLevel.ReadyIn12Months;

        // Find-or-create the per-tenant nominations pool (repo is tenant-scoped).
        var pool = await _poolRepository.GetQueryable()
            .FirstOrDefaultAsync(p => p.Name == poolName, cancellationToken);

        if (pool == null)
        {
            pool = new TalentPool
            {
                Name = poolName,
                Description = "Auto-created pool for employees nominated to succession via appraisal recommendations.",
                PoolTypeId = await ResolveDefaultPoolTypeIdAsync(cancellationToken),
                TargetSize = 0,
                IsActive = true,
                OwnerId = ownerId
            };
            await _poolRepository.AddAsync(pool);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Idempotency: if already a member, return the existing membership.
        var existing = await _memberRepository.GetQueryable()
            .FirstOrDefaultAsync(m => m.TalentPoolId == pool.Id && m.EmployeeId == appraisal.EmployeeId, cancellationToken);
        if (existing != null)
            return ("TalentPoolMember", existing.Id);

        var nextRank = (await _memberRepository.GetQueryable()
            .Where(m => m.TalentPoolId == pool.Id)
            .CountAsync(cancellationToken)) + 1;

        var latestRating = await _ratingResolver.ResolveAsync(appraisal.OverallScore, cancellationToken);

        var member = new TalentPoolMember
        {
            TalentPoolId = pool.Id,
            EmployeeId = appraisal.EmployeeId,
            Rank = nextRank,
            Readiness = defaultReadiness,
            EnrolledDate = DateTime.UtcNow,
            NominatedById = recommendation.ApprovedById,
            NominationNotes = recommendation.Notes,
            LatestPerformanceRating = latestRating,
            RatingLastUpdated = appraisal.OverallScore.HasValue ? DateTime.UtcNow : null
        };

        await _memberRepository.AddAsync(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Succession: enrolled employee {EmployeeId} in pool {PoolId} from appraisal {AppraisalId}",
            appraisal.EmployeeId, pool.Id, appraisal.Id);

        return ("TalentPoolMember", member.Id);
    }

    /// <summary>
    /// Resolves a pool type for the auto-created nominations pool: prefers the built-in
    /// "HighPotential" type, else the first active type by sort order. (Tenant-scoped via the
    /// global query filter.)
    /// </summary>
    private async Task<Guid> ResolveDefaultPoolTypeIdAsync(CancellationToken cancellationToken)
    {
        var typeQuery = _unitOfWork.Repository<TalentPoolTypeDefinition>().GetQueryable()
            .Where(t => t.IsActive && !t.IsDeleted);

        var highPotential = await typeQuery
            .FirstOrDefaultAsync(t => t.Code == "HighPotential", cancellationToken);
        if (highPotential != null)
            return highPotential.Id;

        var firstActive = await typeQuery
            .OrderBy(t => t.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (firstActive != null)
            return firstActive.Id;

        throw new InvalidOperationException(
            "No active talent pool type is configured for this tenant. Configure at least one pool type before nominating to succession.");
    }
}
