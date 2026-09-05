using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Puts employees forward automatically, from their performance record (AWD-08).
/// </summary>
/// <remarks>
/// <para>TDC's note: <i>"some of the nomination will be due to performance or target reached"</i> —
/// the third way a name reaches a ballot, alongside somebody nominating it and management picking
/// it outright.</para>
///
/// <para><b>A generated nomination is a nomination, not a shortcut past the rules.</b> It still has
/// to pass the award's eligibility criteria, still belongs to a cycle, and is still submitted rather
/// than approved — the trigger decides who is <i>put forward</i>, and the committee or the vote
/// still decides who wins. Generating straight to a winner would let an appraisal score award a
/// prize.</para>
///
/// <para><b>Every number in the result is reported, including the ones that are zero.</b> A run that
/// creates nothing has several possible causes — no trigger configured, nobody appraised, everybody
/// ineligible, everybody already nominated — and they call for completely different responses from
/// whoever pressed the button. Returning a bare count would make them indistinguishable.</para>
/// </remarks>
public class AwardCandidateGenerationService : IAwardCandidateGenerationService
{
    private readonly IAwardCycleRepository _cycleRepo;
    private readonly IAwardTypeRepository _awardTypeRepo;
    private readonly IAwardNominationRepository _nominationRepo;
    private readonly IAwardPerformanceTriggerEvaluator _triggers;
    private readonly IAwardEligibilityEvaluator _eligibility;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public AwardCandidateGenerationService(
        IAwardCycleRepository cycleRepo,
        IAwardTypeRepository awardTypeRepo,
        IAwardNominationRepository nominationRepo,
        IAwardPerformanceTriggerEvaluator triggers,
        IAwardEligibilityEvaluator eligibility,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _cycleRepo = cycleRepo;
        _awardTypeRepo = awardTypeRepo;
        _nominationRepo = nominationRepo;
        _triggers = triggers;
        _eligibility = eligibility;
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

    public async Task<AwardGenerationResultDto> GenerateAsync(Guid cycleId, Guid userId)
    {
        var tenantId = GetTenantId();
        var now = DateTime.UtcNow;

        var cycle = await _cycleRepo.GetByIdAsync(cycleId);
        if (cycle == null || cycle.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardCycle {cycleId} not found.");

        var awardType = await _awardTypeRepo.GetByIdAsync(cycle.AwardTypeId);
        if (awardType == null || awardType.TenantId != tenantId)
            throw AwardsWorkflowException.NotFound($"AwardType {cycle.AwardTypeId} not found.");

        var result = new AwardGenerationResultDto
        {
            AwardCycleId = cycleId,
            CycleName = cycle.Name,
            AwardTypeName = awardType.Name,
            MinPerformanceScore = awardType.MinPerformanceScore,
            MinGoalsAchieved = awardType.MinGoalsAchieved,
        };

        // 1. Only an award whose candidates come from performance has anything to generate.
        if (awardType.NominationSource != AwardNominationSource.PerformanceTriggered)
            throw AwardsWorkflowException.InvalidState(
                $"'{awardType.Name}' takes its candidates by {awardType.NominationSource}, not from " +
                "performance, so there is nothing to generate. Change the award's nomination source " +
                "to PerformanceTriggered if it should put people forward automatically.");

        // 2. The window still governs. Generating outside it would add names to a closed list.
        if (cycle.Status != AwardCycleStatus.Published)
            throw AwardsWorkflowException.InvalidState(
                $"Cycle '{cycle.Name}' is {cycle.Status}, so candidates cannot be generated into it.");

        if (cycle.NominationOpensOn == null || cycle.NominationClosesOn == null)
            throw AwardsWorkflowException.InvalidState($"Cycle '{cycle.Name}' has no nomination window.");

        if (now < cycle.NominationOpensOn)
            throw AwardsWorkflowException.InvalidState(
                $"Nominations for '{cycle.Name}' open on {cycle.NominationOpensOn:yyyy-MM-dd HH:mm}.");

        if (now > cycle.NominationClosesOn)
            throw AwardsWorkflowException.InvalidState(
                $"Nominations for '{cycle.Name}' closed on {cycle.NominationClosesOn:yyyy-MM-dd HH:mm}.");

        // 3. What performance says.
        var triggered = await _triggers.EvaluateAsync(awardType, tenantId);
        result.AppraisalsExamined = triggered.AppraisalsExamined;
        result.GoalsExamined = triggered.GoalsExamined;
        result.MatchedByPerformance = triggered.EmployeeIds.Count;

        if (triggered.NoTriggerConfigured)
        {
            result.Note =
                $"'{awardType.Name}' takes its candidates from performance but has no trigger set. " +
                "Set a minimum appraisal score, a minimum number of completed goals, or both.";
            return result;
        }

        if (triggered.EmployeeIds.Count == 0)
        {
            result.Note = triggered.AppraisalsExamined == 0 && triggered.GoalsExamined == 0
                ? "Nobody was put forward, and there was nothing to judge: no appraisal carries a " +
                  "score and no goal is recorded as complete. This is a gap in the performance " +
                  "records rather than a result."
                : $"Nobody met the trigger. {triggered.AppraisalsExamined} scored appraisal(s) and " +
                  $"{triggered.GoalsExamined} completed goal(s) were considered.";
            return result;
        }

        // 4. Eligibility still applies. A high scorer who is out of scope is still out of scope.
        var existing = (await _nominationRepo.GetByTenantAsync(tenantId))
            .Where(n => n.AwardCycleId == cycleId && !n.IsDeleted && n.NomineeId != null)
            .Select(n => n.NomineeId!.Value)
            .ToHashSet();

        foreach (var employeeId in triggered.EmployeeIds)
        {
            if (existing.Contains(employeeId))
            {
                result.AlreadyNominated++;
                continue;
            }

            var verdict = await _eligibility.EvaluateEmployeeAsync(awardType.Id, employeeId, tenantId, now);
            if (!verdict.IsEligible)
            {
                result.ExcludedByEligibility++;
                continue;
            }

            var nomination = new AwardNomination
            {
                TenantId = tenantId,
                NominationNumber = $"NOM-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                AwardTypeId = awardType.Id,
                AwardCycleId = cycleId,
                NomineeId = employeeId,

                // The nominator is the employee themselves, because nobody put them forward — the
                // record says the nomination arose from their own performance. Leaving it blank was
                // not an option: NominatedById is a required Employee foreign key, which is the very
                // shape that made this area's slice-0 nomination endpoint fail with a 500.
                NominatedById = employeeId,

                NominationDate = now,
                Year = cycle.Year,
                Quarter = cycle.Quarter,
                Month = cycle.Month,
                Justification = BuildJustification(awardType, verdict.EmployeeName),

                // Submitted, not approved: the trigger decides who stands, not who wins.
                Status = AwardNominationStatus.Submitted,
                CreatedBy = userId.ToString(),
            };

            await _nominationRepo.AddAsync(nomination);
            result.Created.Add(new AwardGeneratedNomineeDto
            {
                NominationId = nomination.Id,
                NominationNumber = nomination.NominationNumber,
                EmployeeId = employeeId,
                EmployeeName = verdict.EmployeeName,
            });
        }

        if (result.Created.Count > 0)
            await _unitOfWork.SaveChangesAsync();

        result.NominationsCreated = result.Created.Count;

        if (result.NominationsCreated == 0)
            result.Note =
                $"{result.MatchedByPerformance} employee(s) met the trigger, but none was added: " +
                $"{result.ExcludedByEligibility} did not meet the award's eligibility criteria and " +
                $"{result.AlreadyNominated} already had a nomination in this cycle.";

        return result;
    }

    /// <summary>
    /// The justification a generated nomination carries.
    /// </summary>
    /// <remarks>
    /// Written from the trigger that fired, so a voter or committee member reading the ballot can
    /// see why this name is on it. A generated nomination with an empty justification would look
    /// like somebody had nominated a colleague and not bothered to say why.
    /// </remarks>
    private static string BuildJustification(AwardType awardType, string employeeName)
    {
        var parts = new List<string>();
        if (awardType.MinPerformanceScore is { } score)
            parts.Add($"an appraisal score of at least {score:0.##}");
        if (awardType.MinGoalsAchieved is { } goals)
            parts.Add($"{goals} or more completed goal(s)");

        return $"Put forward automatically: {employeeName} meets this award's performance trigger " +
               $"({string.Join(" and ", parts)}).";
    }
}
