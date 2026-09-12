using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The staff vote: who may vote, what they may vote on, and what the result is.
/// </summary>
/// <remarks>
/// <para><b>The centre of TDC's note, and it had no model at all.</b> <i>"HR will setup the
/// eligibility criteria, then employees or management will do the nomination, and then staff can
/// vote for who is supposed to win"</i>. Before slice 5 a grep for <c>AwardVote</c>, <c>Ballot</c>
/// or <c>CastVote</c> across the whole solution returned nothing.</para>
///
/// <para><b>The tally is hidden while voting is open.</b> This is a deliberate design choice, not an
/// oversight: a visible running count changes the result it is reporting. People break towards a
/// leader, and an early lead in a small electorate is mostly noise. The count is available to the
/// awards desk the moment the window closes, and to nobody before it.</para>
/// </remarks>
public class AwardVotingService : IAwardVotingService
{
    private readonly IAwardVoteRepository _voteRepo;
    private readonly IAwardCycleRepository _cycleRepo;
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IAwardElectorateEvaluator _electorate;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardVotingService(
        IAwardVoteRepository voteRepo,
        IAwardCycleRepository cycleRepo,
        IAwardTypeRepository awardTypeRepo,
        IAwardNominationRepository nominationRepo,
        IAwardElectorateEvaluator electorate,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _voteRepo = voteRepo;
        _cycleRepo = cycleRepo;
        _awardTypeRepo = awardTypeRepo;
        _nominationRepo = nominationRepo;
        _electorate = electorate;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private async Task<AwardCycle> GetCycleAsync(Guid cycleId, Guid tenantId)
    {
        var cycle = await _cycleRepo.GetByIdAsync(cycleId);
        if (cycle == null || cycle.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardCycle {cycleId} not found.");
        return cycle;
    }

    private static bool VotingOpen(AwardCycle cycle, DateTime now)
        => cycle.Status == AwardCycleStatus.Published
           && cycle.VotingOpensOn != null && cycle.VotingClosesOn != null
           && now >= cycle.VotingOpensOn && now <= cycle.VotingClosesOn;

    private static bool VotingClosed(AwardCycle cycle, DateTime now)
        => cycle.VotingClosesOn != null && now > cycle.VotingClosesOn;

    /// <summary>
    /// Everything that must be true before a ballot is accepted, in the order it is cheapest to check.
    /// </summary>
    private async Task<(AwardCycle cycle, AwardType awardType, AwardNomination nomination)> ValidateBallotAsync(
        Guid cycleId, Guid nominationId, Guid voterId, Guid tenantId)
    {
        var cycle = await GetCycleAsync(cycleId, tenantId);

        var awardType = await _awardTypeRepo.GetByIdAsync(cycle.AwardTypeId);
        if (awardType == null || awardType.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardType {cycle.AwardTypeId} not found.");

        // 1. Only an award decided by a staff vote has a ballot at all.
        if (awardType.WinnerDecision != AwardWinnerDecision.StaffVote)
            throw AwardsWorkflowException.InvalidState(
                $"'{awardType.Name}' is decided by {awardType.WinnerDecision}, not by a staff vote, " +
                "so there is nothing to vote on.");

        // 2. The window, from the clock.
        var now = DateTime.UtcNow;
        if (cycle.Status != AwardCycleStatus.Published)
            throw AwardsWorkflowException.InvalidState(
                $"Cycle '{cycle.Name}' is {cycle.Status} and is not open to voting.");

        if (cycle.VotingOpensOn != null && now < cycle.VotingOpensOn)
            throw AwardsWorkflowException.InvalidState(
                $"Voting on '{cycle.Name}' opens on {cycle.VotingOpensOn:yyyy-MM-dd HH:mm}.");

        if (VotingClosed(cycle, now))
            throw AwardsWorkflowException.InvalidState(
                $"Voting on '{cycle.Name}' closed on {cycle.VotingClosesOn:yyyy-MM-dd HH:mm}.");

        if (!VotingOpen(cycle, now))
            throw AwardsWorkflowException.InvalidState($"Voting on '{cycle.Name}' is not open.");

        // 3. The ballot has to be one of this cycle's nominees.
        var nomination = await _nominationRepo.GetWithDetailsAsync(nominationId);
        if (nomination == null || nomination.TenantId != tenantId || nomination.IsDeleted)
            throw AwardsWorkflowException.NotFound($"AwardNomination {nominationId} not found.");

        if (nomination.AwardCycleId != cycleId)
            throw AwardsWorkflowException.Invalid(
                $"That nomination does not belong to '{cycle.Name}'.");

        if (!IsOnTheBallot(nomination))
            throw AwardsWorkflowException.InvalidState(
                $"That nomination is {nomination.Status} and is not on the ballot. Only submitted " +
                "nominations can be voted for.");

        // 4. The electorate. TDC's note: "a section of the employees or all of them can vote".
        if (!await _electorate.CanVoteAsync(cycle.AwardTypeId, voterId, tenantId, now))
            throw AwardsWorkflowException.Invalid(
                $"You are not in the electorate for '{awardType.Name}'. Voting on this award is " +
                "limited to a defined group of employees.");

        // 5. Voting for yourself, governed by the same setting as nominating yourself: both are
        //    ways of advancing your own candidacy, so one switch covers them. Off by default.
        if (nomination.NomineeId == voterId && !awardType.AllowSelfNomination)
            throw AwardsWorkflowException.Invalid(
                $"'{awardType.Name}' does not accept a vote for yourself.");

        return (cycle, awardType, nomination);
    }

    /// <summary>
    /// A nomination is on the ballot once it has been submitted and while it is still in play.
    /// </summary>
    /// <remarks>
    /// <para><c>Draft</c> is excluded because it is still being written, and <c>Rejected</c> and
    /// <c>Withdrawn</c> because they are out. <c>Approved</c> is excluded too: that status means the
    /// nomination has already produced an award, so putting it back on a ballot would be asking
    /// people to vote on a decision already taken.</para>
    ///
    /// <para>TDC's note speaks of nominees displayed <i>"based on the eligibility or
    /// shortlisting"</i>. There is no <c>Shortlisted</c> status in the enum — <c>UnderReview</c> is
    /// the state a shortlisted nomination is in — so shortlisting is expressed by moving a
    /// nomination to review rather than by a status of its own.</para>
    /// </remarks>
    private static bool IsOnTheBallot(AwardNomination n)
        => n.Status is AwardNominationStatus.Submitted or AwardNominationStatus.UnderReview;

    // ── the ballot ────────────────────────────────────────────────────────────

    public async Task<AwardBallotDto> GetBallotAsync(Guid cycleId, Guid voterId)
    {
        var tenantId = GetTenantId();
        var cycle = await GetCycleAsync(cycleId, tenantId);
        var awardType = await _awardTypeRepo.GetByIdAsync(cycle.AwardTypeId);
        var now = DateTime.UtcNow;

        var nominations = (await _nominationRepo.GetByTenantAsync(tenantId))
            .Where(n => n.AwardCycleId == cycleId && IsOnTheBallot(n))
            .ToList();

        var myVote = await _voteRepo.GetByVoterAsync(cycleId, voterId);
        var canVote = await _electorate.CanVoteAsync(cycle.AwardTypeId, voterId, tenantId, now);

        return new AwardBallotDto
        {
            AwardCycleId = cycleId,
            CycleName = cycle.Name,
            AwardTypeId = cycle.AwardTypeId,
            AwardTypeName = awardType?.Name ?? string.Empty,
            VotingOpensOn = cycle.VotingOpensOn,
            VotingClosesOn = cycle.VotingClosesOn,
            IsVotingOpen = VotingOpen(cycle, now),
            IsInElectorate = canVote,
            MyVoteNominationId = myVote?.AwardNominationId,
            Nominees = nominations.Select(n => new AwardBallotEntryDto
            {
                NominationId = n.Id,
                NominationNumber = n.NominationNumber,
                NomineeId = n.NomineeId,
                NomineeName = n.Nominee != null
                    ? $"{n.Nominee.FirstName} {n.Nominee.LastName}".Trim()
                    : n.TeamName ?? string.Empty,
                IsTeam = n.NomineeId == null,
                TeamName = n.TeamName,
                Justification = n.Justification,
                IsMyVote = myVote?.AwardNominationId == n.Id,
            }).OrderBy(e => e.NomineeName).ToList(),
        };
    }

    // ── casting ───────────────────────────────────────────────────────────────

    public async Task<AwardVoteDto> CastAsync(Guid cycleId, Guid voterId, Guid userId, CastAwardVoteDto dto)
    {
        var tenantId = GetTenantId();
        var (cycle, _, nomination) = await ValidateBallotAsync(cycleId, dto.AwardNominationId, voterId, tenantId);

        var existing = await _voteRepo.GetByVoterAsync(cycleId, voterId);
        if (existing != null)
        {
            // Changing your mind while the window is open updates the ballot you already cast.
            // A second row would break one-vote-per-voter and double-count the tally.
            existing.AwardNominationId = dto.AwardNominationId;
            existing.Justification = dto.Justification;
            existing.CastOn = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = userId.ToString();

            await _voteRepo.UpdateAsync(existing);
            await _unitOfWork.SaveChangesAsync();
            return Map(existing, cycle, nomination);
        }

        var vote = new AwardVote
        {
            TenantId = tenantId,
            AwardCycleId = cycleId,
            AwardNominationId = dto.AwardNominationId,
            VoterId = voterId,
            Justification = dto.Justification,
            CastOn = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };

        await _voteRepo.AddAsync(vote);
        await _unitOfWork.SaveChangesAsync();
        return Map(vote, cycle, nomination);
    }

    public async Task<AwardVoteDto?> GetMyVoteAsync(Guid cycleId, Guid voterId)
    {
        var tenantId = GetTenantId();
        var cycle = await GetCycleAsync(cycleId, tenantId);

        var vote = await _voteRepo.GetByVoterAsync(cycleId, voterId);
        if (vote == null) return null;

        var nomination = await _nominationRepo.GetWithDetailsAsync(vote.AwardNominationId);
        return Map(vote, cycle, nomination);
    }

    public async Task WithdrawMyVoteAsync(Guid cycleId, Guid voterId)
    {
        var tenantId = GetTenantId();
        var cycle = await GetCycleAsync(cycleId, tenantId);

        if (!VotingOpen(cycle, DateTime.UtcNow))
            throw AwardsWorkflowException.InvalidState(
                $"Voting on '{cycle.Name}' is not open, so a ballot can no longer be withdrawn.");

        var vote = await _voteRepo.GetByVoterAsync(cycleId, voterId);
        if (vote == null)
            throw AwardsWorkflowException.NotFound("You have not voted in this cycle.");

        await _voteRepo.DeleteAsync(vote.Id);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── the result ────────────────────────────────────────────────────────────

    public async Task<AwardVoteResultDto> GetResultAsync(Guid cycleId)
    {
        var tenantId = GetTenantId();
        var cycle = await GetCycleAsync(cycleId, tenantId);
        var awardType = await _awardTypeRepo.GetByIdAsync(cycle.AwardTypeId);
        var now = DateTime.UtcNow;

        var result = new AwardVoteResultDto
        {
            AwardCycleId = cycleId,
            CycleName = cycle.Name,
            AwardTypeName = awardType?.Name ?? string.Empty,
            VotingClosesOn = cycle.VotingClosesOn,
            IsVotingOpen = VotingOpen(cycle, now),
        };

        // The count is withheld while the window is open, on purpose. A visible running tally
        // changes the result it reports: people break towards a leader, and an early lead in a
        // small electorate is mostly noise. Turnout is safe to show — it encourages voting without
        // saying anything about who is winning.
        var votes = (await _voteRepo.GetByCycleAsync(cycleId)).ToList();
        result.VotesCast = votes.Count;

        if (!VotingClosed(cycle, now))
        {
            result.ResultsAvailable = false;
            result.WithheldReason = result.IsVotingOpen
                ? $"Voting is open until {cycle.VotingClosesOn:yyyy-MM-dd HH:mm}. The count is withheld " +
                  "until then so that it cannot influence the vote it reports."
                : "Voting has not opened yet.";
            return result;
        }

        var nominations = (await _nominationRepo.GetByTenantAsync(tenantId))
            .Where(n => n.AwardCycleId == cycleId && IsOnTheBallot(n))
            .ToList();

        var counts = votes.GroupBy(v => v.AwardNominationId).ToDictionary(g => g.Key, g => g.Count());

        result.ResultsAvailable = true;
        result.Tally = nominations
            .Select(n => new AwardVoteTallyDto
            {
                NominationId = n.Id,
                NomineeId = n.NomineeId,
                NomineeName = n.Nominee != null
                    ? $"{n.Nominee.FirstName} {n.Nominee.LastName}".Trim()
                    : n.TeamName ?? string.Empty,
                Votes = counts.TryGetValue(n.Id, out var c) ? c : 0,
            })
            .OrderByDescending(t => t.Votes)
            .ThenBy(t => t.NomineeName)
            .ToList();

        var top = result.Tally.FirstOrDefault();
        if (top is { Votes: > 0 })
        {
            var tied = result.Tally.Where(t => t.Votes == top.Votes).ToList();
            if (tied.Count == 1)
            {
                result.WinningNominationId = top.NominationId;
                result.WinnerName = top.NomineeName;
            }
            else
            {
                // A tie is reported rather than broken. Any rule this code invented - earliest
                // nomination, alphabetical - would decide a real award on an arbitrary basis, and
                // TDC has not said how they want ties settled.
                result.IsTied = true;
                result.TiedNominationIds = tied.Select(t => t.NominationId).ToList();
                result.WithheldReason =
                    $"{tied.Count} nominees are tied on {top.Votes} vote(s). The system does not break " +
                    "ties: the award committee decides.";
            }
        }
        else
        {
            result.WithheldReason = "No votes were cast.";
        }

        return result;
    }

    private static AwardVoteDto Map(AwardVote v, AwardCycle cycle, AwardNomination? nomination) => new()
    {
        Id = v.Id,
        AwardCycleId = v.AwardCycleId,
        CycleName = cycle.Name,
        AwardNominationId = v.AwardNominationId,
        NomineeName = nomination?.Nominee != null
            ? $"{nomination.Nominee.FirstName} {nomination.Nominee.LastName}".Trim()
            : nomination?.TeamName ?? string.Empty,
        VoterId = v.VoterId,
        CastOn = v.CastOn,
        Justification = v.Justification,
    };
}
