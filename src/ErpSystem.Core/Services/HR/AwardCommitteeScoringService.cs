using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The committee's decision: the average score each nomination received, and who that makes the winner.
/// </summary>
/// <remarks>
/// <para>TDC's note, in full: <i>"the committee members will score, and the winner will be the one
/// with the highest average score"</i>. That is the whole rule, and it is a mean rather than a sum —
/// so a nomination scored by two generous members must not beat one scored by five moderate ones
/// merely by having fewer reviewers.</para>
///
/// <para><b>The reviewer minimum is honoured, and it is the reason the mean alone is not enough.</b>
/// <c>AwardType.MinRequiredReviewers</c> already existed and, like the rest of that entity's
/// eligibility vocabulary, was applied by nothing. A nomination scored by one member out of five has
/// an average, and it is not a committee's opinion. Such nominations are listed with their partial
/// average — the committee has to see who is still outstanding — but they cannot win.</para>
///
/// <para><b>Ties are reported, never broken</b>, for the same reason as in the staff vote: any rule
/// this code invented would settle a real award on a basis nobody at TDC chose.</para>
/// </remarks>
public class AwardCommitteeScoringService : IAwardCommitteeScoringService
{
    private readonly IAwardNominationReviewRepository _reviewRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IAwardCycleRepository _cycleRepo;
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly ICurrentUserProvider _currentUserProvider;

    public AwardCommitteeScoringService(
        IAwardNominationReviewRepository reviewRepo,
        IAwardNominationRepository nominationRepo,
        IAwardCycleRepository cycleRepo,
        IAwardTypeRepository awardTypeRepo,
        ICurrentUserProvider currentUserProvider)
    {
        _reviewRepo = reviewRepo;
        _nominationRepo = nominationRepo;
        _cycleRepo = cycleRepo;
        _awardTypeRepo = awardTypeRepo;
        _currentUserProvider = currentUserProvider;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    public async Task<AwardCommitteeResultDto> GetResultAsync(Guid cycleId)
    {
        var tenantId = GetTenantId();

        var cycle = await _cycleRepo.GetByIdAsync(cycleId);
        if (cycle == null || cycle.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardCycle {cycleId} not found.");

        var awardType = await _awardTypeRepo.GetByIdAsync(cycle.AwardTypeId);
        if (awardType == null || awardType.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardType {cycle.AwardTypeId} not found.");

        var result = new AwardCommitteeResultDto
        {
            AwardCycleId = cycleId,
            CycleName = cycle.Name,
            AwardTypeName = awardType.Name,
            MinRequiredReviewers = awardType.MinRequiredReviewers,
        };

        if (awardType.WinnerDecision != AwardWinnerDecision.CommitteeScore)
        {
            result.WithheldReason =
                $"'{awardType.Name}' is decided by {awardType.WinnerDecision}, not by committee scoring.";
            return result;
        }

        var nominations = (await _nominationRepo.GetByTenantAsync(tenantId))
            .Where(n => n.AwardCycleId == cycleId
                && n.Status is AwardNominationStatus.Submitted or AwardNominationStatus.UnderReview)
            .ToList();

        var summary = await _reviewRepo.GetScoreSummaryByCycleAsync(cycleId);
        var minimum = awardType.MinRequiredReviewers;

        result.Scores = nominations
            .Select(n =>
            {
                var has = summary.TryGetValue(n.Id, out var s);
                var reviewers = has ? s.Reviewers : 0;
                return new AwardCommitteeScoreDto
                {
                    NominationId = n.Id,
                    NominationNumber = n.NominationNumber,
                    NomineeId = n.NomineeId,
                    NomineeName = n.Nominee != null
                        ? $"{n.Nominee.FirstName} {n.Nominee.LastName}".Trim()
                        : n.TeamName ?? string.Empty,
                    AverageScore = has ? Math.Round(s.Average, 2) : null,
                    ReviewerCount = reviewers,
                    MeetsReviewerMinimum = minimum == null || reviewers >= minimum,
                };
            })
            // Unscored nominations sort last rather than first: a null average is not a low score,
            // and ordering them among the zeros would read as though the committee had rejected them.
            .OrderByDescending(x => x.AverageScore ?? double.MinValue)
            .ThenBy(x => x.NomineeName)
            .ToList();

        var contenders = result.Scores.Where(x => x.AverageScore != null && x.MeetsReviewerMinimum).ToList();

        if (contenders.Count == 0)
        {
            var scoredButShort = result.Scores.Count(x => x.AverageScore != null && !x.MeetsReviewerMinimum);
            result.WithheldReason = scoredButShort > 0
                ? $"{scoredButShort} nomination(s) have been scored, but none has reached the " +
                  $"{minimum} reviewer(s) this award requires."
                : "No nomination in this cycle has been scored yet.";
            return result;
        }

        var best = contenders[0].AverageScore!.Value;
        var tied = contenders.Where(x => Math.Abs(x.AverageScore!.Value - best) < 0.0001).ToList();

        if (tied.Count == 1)
        {
            result.ResultAvailable = true;
            result.WinningNominationId = tied[0].NominationId;
            result.WinnerName = tied[0].NomineeName;
        }
        else
        {
            result.ResultAvailable = true;
            result.IsTied = true;
            result.TiedNominationIds = tied.Select(t => t.NominationId).ToList();
            result.WithheldReason =
                $"{tied.Count} nominations are tied on an average of {best:0.##}. The system does not " +
                "break ties: the committee decides.";
        }

        return result;
    }
}
