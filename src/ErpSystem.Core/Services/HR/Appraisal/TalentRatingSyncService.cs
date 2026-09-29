using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Theme 9 — on appraisal finalization, maps the overall score to a PerformanceRating and refreshes
/// the cached rating on the employee's talent-pool memberships and succession candidacies (9-box feed).
/// Called only by the settle path (<see cref="IAppraisalScoreService"/>), once an appraisal is final.
/// </summary>
public class TalentRatingSyncService : ITalentRatingSyncService
{
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<TalentPoolMember> _memberRepository;
    private readonly IGenericRepository<SuccessionCandidate> _candidateRepository;
    private readonly IPerformanceRatingResolver _ratingResolver;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TalentRatingSyncService> _logger;

    public TalentRatingSyncService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<TalentPoolMember> memberRepository,
        IGenericRepository<SuccessionCandidate> candidateRepository,
        IPerformanceRatingResolver ratingResolver,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TalentRatingSyncService> logger)
    {
        _appraisalRepository = appraisalRepository;
        _memberRepository = memberRepository;
        _candidateRepository = candidateRepository;
        _ratingResolver = ratingResolver;
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

    public async Task<bool> SyncFromAppraisalAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var appraisal = await _appraisalRepository.GetQueryable()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .Select(a => new { a.Id, a.AppraisalNumber, a.EmployeeId, a.OverallScore, a.EndDate })
            .FirstOrDefaultAsync(cancellationToken);

        if (appraisal == null) return false;

        // A null score is "nothing was scored", not "rated at the bottom" — the rating the
        // employee already has is left as it is.
        if (appraisal.OverallScore == null)
        {
            _logger.LogInformation(
                "Talent rating sync: appraisal {AppraisalNumber} has no settled score; employee {EmployeeId}'s rating is left as it is.",
                appraisal.AppraisalNumber, appraisal.EmployeeId);
            return false;
        }

        // An older period's finalisation — an appeal resolved late, a reopened appraisal — must not
        // overwrite the rating a newer one already published (performance closure A8). A newer
        // appraisal has published when it is final: Completed, Closed, under appeal after that, or
        // signed off by HR and waiting only on the employee's acknowledgment.
        var newer = await _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId
                     && a.EmployeeId == appraisal.EmployeeId
                     && a.Id != appraisal.Id
                     && a.OverallScore != null
                     && a.EndDate > appraisal.EndDate
                     && (a.Status == AppraisalStatus.Completed
                         || a.Status == AppraisalStatus.Closed
                         || a.Status == AppraisalStatus.Appealed
                         || (a.Status == AppraisalStatus.Governance
                             && a.HRReviews.Any(r => !r.IsDeleted && r.ReviewCompletedDate != null && r.IsApproved))))
            .OrderByDescending(a => a.EndDate)
            .Select(a => a.AppraisalNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (newer != null)
        {
            _logger.LogInformation(
                "Talent rating sync: appraisal {AppraisalNumber} is older than {Newer}, which already rated employee {EmployeeId}; nothing overwritten.",
                appraisal.AppraisalNumber, newer, appraisal.EmployeeId);
            return false;
        }

        var rating = await _ratingResolver.ResolveAsync(appraisal.OverallScore, cancellationToken);
        if (rating == null) return false;

        var now = DateTime.UtcNow;
        var changed = 0;

        var members = await _memberRepository.GetQueryable(m => m.EmployeeId == appraisal.EmployeeId && m.TenantId == tenantId).ToListAsync(cancellationToken);
        foreach (var m in members)
        {
            m.LatestPerformanceRating = rating;
            m.RatingLastUpdated = now;
            changed++;
        }

        var candidates = await _candidateRepository.GetQueryable(c => c.EmployeeId == appraisal.EmployeeId && c.TenantId == tenantId).ToListAsync(cancellationToken);
        foreach (var c in candidates)
        {
            c.LatestPerformanceRating = rating;
            changed++;
        }

        if (changed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Talent rating sync: appraisal {AppraisalNumber} → employee {EmployeeId} rated {Rating} across {Count} record(s)",
                appraisal.AppraisalNumber, appraisal.EmployeeId, rating, changed);
        }

        return changed > 0;
    }
}
