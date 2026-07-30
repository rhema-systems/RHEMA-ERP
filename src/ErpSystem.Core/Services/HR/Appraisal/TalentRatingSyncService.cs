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
/// </summary>
public class TalentRatingSyncService : ITalentRatingSyncService
{
    private readonly IGenericRepository<TalentPoolMember> _memberRepository;
    private readonly IGenericRepository<SuccessionCandidate> _candidateRepository;
    private readonly IPerformanceRatingResolver _ratingResolver;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TalentRatingSyncService> _logger;

    public TalentRatingSyncService(
        IGenericRepository<TalentPoolMember> memberRepository,
        IGenericRepository<SuccessionCandidate> candidateRepository,
        IPerformanceRatingResolver ratingResolver,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<TalentRatingSyncService> logger)
    {
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

    public async Task SyncFromAppraisalAsync(Guid employeeId, decimal? overallScore, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var rating = await _ratingResolver.ResolveAsync(overallScore, cancellationToken);
        if (rating == null) return;

        var now = DateTime.UtcNow;
        var changed = 0;

        var members = await _memberRepository.GetQueryable(m => m.EmployeeId == employeeId && m.TenantId == tenantId).ToListAsync(cancellationToken);
        foreach (var m in members)
        {
            m.LatestPerformanceRating = rating;
            m.RatingLastUpdated = now;
            await _memberRepository.UpdateAsync(m);
            changed++;
        }

        var candidates = await _candidateRepository.GetQueryable(c => c.EmployeeId == employeeId && c.TenantId == tenantId).ToListAsync(cancellationToken);
        foreach (var c in candidates)
        {
            c.LatestPerformanceRating = rating;
            await _candidateRepository.UpdateAsync(c);
            changed++;
        }

        if (changed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Talent rating sync: employee {EmployeeId} → {Rating} across {Count} record(s)", employeeId, rating, changed);
        }
    }
}
