using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// Takes an appraisal out of its cycle without a result (performance closure E-d1, D-10): HR's
/// withdraw action, with a reason, and the leaver's exit, which withdraws in the same save as the
/// exit itself (D-54).
/// </summary>
/// <remarks>
/// <para>No status meant "not appraised". A leaver's appraisal waited in its cycle for evaluations
/// nobody would write, counted in every progress figure and dashboard, and — with the close refusing
/// an unfinished cycle (E-d2) — would have held the cycle open for good.</para>
///
/// <para>Only before the appraisal is final (D-52): from Draft, Active, or Governance before HR's
/// sign-off (and the panel's commit, when the cycle requires calibration). A final appraisal's
/// rating is already published to the talent pools; it stands, and HR's audited advance takes it
/// past the acknowledgment a leaver will not give.</para>
///
/// <para>A withdrawal keeps what was written — it is the record of how far the appraisal got —
/// and every read leaves the appraisal out of its counts and shows it without a score. There is no
/// undo yet: a reinstate joins D-17's audited reopen in lane N (D-53).</para>
/// </remarks>
public class AppraisalWithdrawalService : IAppraisalWithdrawalService
{
    /// <summary>The reason column's width (<c>PerformanceAppraisal.WithdrawnReason</c>).</summary>
    public const int ReasonMaxLength = 1000;

    private static readonly AppraisalStatus[] Withdrawable =
        [AppraisalStatus.Draft, AppraisalStatus.Active, AppraisalStatus.Governance];

    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<AppraisalOutcomeRecommendation> _recommendationRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AppraisalWithdrawalService> _logger;

    public AppraisalWithdrawalService(
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<AppraisalOutcomeRecommendation> recommendationRepository,
        ICurrentUserProvider currentUserProvider,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<AppraisalWithdrawalService> logger)
    {
        _appraisalRepository = appraisalRepository;
        _recommendationRepository = recommendationRepository;
        _currentUserProvider = currentUserProvider;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task WithdrawAsync(Guid appraisalId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A withdrawal takes a reason.", nameof(reason));

        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");

        var appraisal = await LoadQuery(tenantId)
            .FirstOrDefaultAsync(a => a.Id == appraisalId, cancellationToken)
            ?? throw new KeyNotFoundException($"Performance appraisal with ID '{appraisalId}' not found.");

        // The two-actor rule (E-a): an HR officer does not withdraw their own appraisal.
        if (Actor() is Guid me && me == appraisal.EmployeeId)
            throw new UnauthorizedAccessException(
                "You cannot withdraw your own appraisal: another HR officer does.");

        if (WhyNot(appraisal) is string refusal)
            throw new InvalidOperationException(refusal);

        await ApplyAsync(appraisal, reason, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Appraisal {AppraisalId} ({Number}) withdrawn by {ActorId}: {Reason}",
            appraisal.Id, appraisal.AppraisalNumber, appraisal.WithdrawnById, appraisal.WithdrawnReason);
    }

    /// <inheritdoc/>
    public async Task<int> StageLeaverWithdrawalsAsync(
        Guid tenantId, Guid employeeId, DateTime exitDate, string? detail, CancellationToken cancellationToken = default)
    {
        var open = await LoadQuery(tenantId)
            .Where(a => a.EmployeeId == employeeId
                     && (a.Status == AppraisalStatus.Draft
                         || a.Status == AppraisalStatus.Active
                         || a.Status == AppraisalStatus.Governance))
            .ToListAsync(cancellationToken);

        var reason = $"Left the organisation on {exitDate:yyyy-MM-dd}"
            + (string.IsNullOrWhiteSpace(detail) ? "." : $": {detail.Trim()}");

        var withdrawn = 0;
        foreach (var appraisal in open)
        {
            if (AppraisalScoreService.IsFinal(appraisal))
            {
                // D-52: its result stands. It waits only for the acknowledgment the leaver will not
                // give, and HR's audited advance takes it past that.
                _logger.LogInformation(
                    "Leaver {EmployeeId}: appraisal {AppraisalId} ({Number}) is final and is not withdrawn; " +
                    "HR's advance takes it past the acknowledgment.",
                    employeeId, appraisal.Id, appraisal.AppraisalNumber);
                continue;
            }

            await ApplyAsync(appraisal, reason, cancellationToken);
            withdrawn++;

            _logger.LogInformation(
                "Leaver {EmployeeId}: appraisal {AppraisalId} ({Number}) withdrawn with the exit",
                employeeId, appraisal.Id, appraisal.AppraisalNumber);
        }

        return withdrawn;
    }

    /// <summary>
    /// What the final check reads: the HR sign-off and the cycle's calibration rule. Tracked — the
    /// withdrawal writes the rows it loads.
    /// </summary>
    private IQueryable<PerformanceAppraisal> LoadQuery(Guid tenantId)
        => _appraisalRepository.GetQueryable()
            .Where(a => a.TenantId == tenantId)
            .Include(a => a.HRReviews)
            .Include(a => a.AppraisalCycle)
                .ThenInclude(c => c.AppraisalSettings)
            .AsSplitQuery();

    /// <summary>Why this appraisal cannot be withdrawn, or null when it can.</summary>
    private static string? WhyNot(PerformanceAppraisal appraisal)
    {
        if (appraisal.Status == AppraisalStatus.Withdrawn)
            return "This appraisal is already withdrawn.";

        if (!Withdrawable.Contains(appraisal.Status))
            return $"This appraisal is {appraisal.Status}, so its result stands: an appraisal is withdrawn " +
                   "from Draft, Active, or Governance before it is final.";

        if (AppraisalScoreService.IsFinal(appraisal))
            return "HR has signed this appraisal off" +
                   (appraisal.IsCalibrated ? " and the panel has calibrated it" : string.Empty) +
                   ", so it is final and its result stands. An appraisal is withdrawn before it is final; " +
                   "HR's advance takes a final one past the employee's acknowledgment.";

        return null;
    }

    /// <summary>The withdrawal's writes, on tracked rows; the caller saves.</summary>
    private async Task ApplyAsync(PerformanceAppraisal appraisal, string reason, CancellationToken cancellationToken)
    {
        AppraisalLifecycle.EnsureTransition(appraisal.Status, AppraisalStatus.Withdrawn);

        var text = reason.Trim();
        var actor = Actor();
        var now = DateTime.UtcNow;

        appraisal.Status = AppraisalStatus.Withdrawn;
        appraisal.WithdrawnReason = text.Length > ReasonMaxLength ? text[..ReasonMaxLength] : text;
        appraisal.WithdrawnById = actor;
        appraisal.WithdrawnDate = now;

        // A seat at a sitting panel that has not calibrated it is freed, as a cancelled session frees
        // its appraisals (E-b). A committed calibration keeps its link: it is the record of the panel.
        if (!appraisal.IsCalibrated)
            appraisal.CalibrationSessionId = null;

        // An outcome proposed on the appraisal, or approved and not yet actioned, has no result
        // behind it now: dismissed, with the reason, so the worklist shows why and approving or
        // re-dispatching it is refused. One already actioned stands — its record is the owning
        // module's.
        var open = await _recommendationRepository.GetQueryable()
            .Where(r => r.TenantId == appraisal.TenantId
                     && r.PerformanceAppraisalId == appraisal.Id
                     && (r.Status == RecommendationStatus.Proposed || r.Status == RecommendationStatus.Approved))
            .ToListAsync(cancellationToken);

        foreach (var recommendation in open)
        {
            var note = $"Dismissed: the appraisal was withdrawn from its cycle. {appraisal.WithdrawnReason}";
            recommendation.Status = RecommendationStatus.Dismissed;
            recommendation.ApprovedById = actor;
            recommendation.ApprovedDate = now;
            recommendation.ResolutionNotes = note.Length > 1000 ? note[..1000] : note;
        }
    }

    /// <summary>The employee acting, when a person is: the nightly host and a service login have none.</summary>
    private Guid? Actor()
        => _currentUser.EmployeeId is Guid id && id != Guid.Empty ? id : null;
}
