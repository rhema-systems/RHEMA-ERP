using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Manages the two lightweight goal/check-in linkages added for stakeholder feedback:
/// soft skills required per goal (Theme 3) and check-in ↔ yearly objective links (Theme 6).
/// Both use a replace-all set semantics.
/// </summary>
public class PerformanceLinkService : IPerformanceLinkService
{
    private readonly IGenericRepository<GoalRequiredSkill> _skillRepository;
    private readonly IGenericRepository<CheckInObjectiveLink> _objectiveRepository;
    private readonly IGenericRepository<AppraisalCompetency> _competencyRepository;
    private readonly IGenericRepository<CompanyGoal> _companyGoalRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PerformanceLinkService> _logger;

    public PerformanceLinkService(
        IGenericRepository<GoalRequiredSkill> skillRepository,
        IGenericRepository<CheckInObjectiveLink> objectiveRepository,
        IGenericRepository<AppraisalCompetency> competencyRepository,
        IGenericRepository<CompanyGoal> companyGoalRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        ILogger<PerformanceLinkService> logger)
    {
        _skillRepository = skillRepository;
        _objectiveRepository = objectiveRepository;
        _competencyRepository = competencyRepository;
        _companyGoalRepository = companyGoalRepository;
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

    private async Task<EmployeeGoal> GetOwnedGoalAsync(Guid employeeGoalId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var goal = await _unitOfWork.Repository<EmployeeGoal>().GetByIdAsync(employeeGoalId);
        if (goal == null || goal.TenantId != tenantId)
            throw new ArgumentException($"Employee goal with ID '{employeeGoalId}' not found.");
        return goal;
    }

    private async Task<CheckIn> GetOwnedCheckInAsync(Guid checkInId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var checkIn = await _unitOfWork.Repository<CheckIn>().GetByIdAsync(checkInId);
        if (checkIn == null || checkIn.TenantId != tenantId)
            throw new ArgumentException($"Check-in with ID '{checkInId}' not found.");
        return checkIn;
    }

    // ── Theme 3 — soft skills per goal ──────────────────────────────────────

    public async Task<IEnumerable<GoalRequiredSkillDto>> GetGoalRequiredSkillsAsync(Guid employeeGoalId, CancellationToken cancellationToken = default)
    {
        await GetOwnedGoalAsync(employeeGoalId, cancellationToken);
        var skills = await _skillRepository.GetQueryable(s => s.EmployeeGoalId == employeeGoalId)
            .Include(s => s.Competency)
            .ToListAsync(cancellationToken);

        return skills.Select(ToDto).ToList();
    }

    /// <summary>
    /// Replace-set: the argument is the complete list, and an empty one clears every link.
    /// </summary>
    /// <remarks>
    /// <para>Two traps this navigates, both of which used to bite:</para>
    /// <para><b>Validate before mutating.</b> Every incoming competency is checked first, so a bad
    /// id cannot leave the set half-rewritten. (It also confirms the competency is this tenant's,
    /// as <see cref="SetCheckInObjectivesAsync"/> already did for company goals — without it a
    /// caller could link a goal to another tenant's competency and leak its name through every
    /// read.)</para>
    /// <para><b>Revive, do not re-insert.</b> <c>DeleteAsync</c> is a soft delete and the unique
    /// index on (EmployeeGoalId, CompetencyId) does not filter <c>IsDeleted</c>, so a removed
    /// competency keeps its slot forever: adding it back died on a duplicate-key violation, and
    /// removing a skill was therefore permanent. Existing rows are un-deleted and updated in place.</para>
    /// </remarks>
    public async Task<IEnumerable<GoalRequiredSkillDto>> SetGoalRequiredSkillsAsync(Guid employeeGoalId, IEnumerable<SetGoalRequiredSkillDto> skills, CancellationToken cancellationToken = default)
    {
        var goal = await GetOwnedGoalAsync(employeeGoalId, cancellationToken);
        var incoming = skills.DistinctBy(s => s.CompetencyId).ToList();

        foreach (var dto in incoming)
        {
            var competency = await _competencyRepository.GetByIdAsync(dto.CompetencyId);
            if (competency == null || competency.TenantId != goal.TenantId)
                throw new ArgumentException($"Competency with ID '{dto.CompetencyId}' not found.");
        }

        // Soft-deleted rows included on purpose — see the remarks.
        var existing = await _skillRepository
            .GetQueryableIncludingDeleted(s => s.EmployeeGoalId == employeeGoalId)
            .ToListAsync(cancellationToken);

        var wanted = incoming.ToDictionary(s => s.CompetencyId);

        foreach (var row in existing)
        {
            if (wanted.TryGetValue(row.CompetencyId, out var dto))
            {
                row.IsDeleted = false;
                row.DeletedAt = null;
                row.DevelopmentNeeded = dto.DevelopmentNeeded;
                row.Note = dto.Note;
                await _skillRepository.UpdateAsync(row);
                wanted.Remove(row.CompetencyId);
            }
            else if (!row.IsDeleted)
            {
                await _skillRepository.DeleteAsync(row);
            }
        }

        foreach (var dto in wanted.Values)
        {
            await _skillRepository.AddAsync(new GoalRequiredSkill
            {
                TenantId = goal.TenantId,
                EmployeeGoalId = employeeGoalId,
                CompetencyId = dto.CompetencyId,
                DevelopmentNeeded = dto.DevelopmentNeeded,
                Note = dto.Note
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Replaced required skills for goal {GoalId}", employeeGoalId);

        return await GetGoalRequiredSkillsAsync(employeeGoalId, cancellationToken);
    }

    // ── Theme 6 — check-in ↔ yearly objective ───────────────────────────────

    public async Task<IEnumerable<CheckInObjectiveLinkDto>> GetCheckInObjectivesAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var links = await _objectiveRepository.GetQueryable(l => l.CheckInId == checkInId)
            .Include(l => l.CompanyGoal)
            .ToListAsync(cancellationToken);

        return links.Select(ToDto).ToList();
    }

    /// <summary>
    /// Replace-set, with the same two traps as <see cref="SetGoalRequiredSkillsAsync"/>: validate
    /// every id before touching anything, and revive soft-deleted rows rather than re-inserting
    /// them, because the unique index on (CheckInId, CompanyGoalId) ignores <c>IsDeleted</c> and
    /// would reject an objective that had been unlinked earlier.
    /// </summary>
    public async Task<IEnumerable<CheckInObjectiveLinkDto>> SetCheckInObjectivesAsync(Guid checkInId, IEnumerable<Guid> companyGoalIds, CancellationToken cancellationToken = default)
    {
        var checkIn = await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = checkIn.TenantId;
        var incoming = companyGoalIds.Distinct().ToList();

        foreach (var goalId in incoming)
        {
            var companyGoal = await _companyGoalRepository.GetByIdAsync(goalId);
            if (companyGoal == null || companyGoal.TenantId != tenantId)
                throw new ArgumentException($"Company goal with ID '{goalId}' not found.");
        }

        var existing = await _objectiveRepository
            .GetQueryableIncludingDeleted(l => l.CheckInId == checkInId)
            .ToListAsync(cancellationToken);

        var wanted = new HashSet<Guid>(incoming);

        foreach (var row in existing)
        {
            if (wanted.Remove(row.CompanyGoalId))
            {
                row.IsDeleted = false;
                row.DeletedAt = null;
                await _objectiveRepository.UpdateAsync(row);
            }
            else if (!row.IsDeleted)
            {
                await _objectiveRepository.DeleteAsync(row);
            }
        }

        foreach (var goalId in wanted)
        {
            await _objectiveRepository.AddAsync(new CheckInObjectiveLink
            {
                TenantId = tenantId,
                CheckInId = checkInId,
                CompanyGoalId = goalId
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Replaced objective links for check-in {CheckInId}", checkInId);

        return await GetCheckInObjectivesAsync(checkInId, cancellationToken);
    }

    // ── Theme 3 — development-plan suggestions ───────────────────────────────

    public async Task<IEnumerable<DevelopmentSkillSuggestionDto>> GetDevelopmentSkillSuggestionsAsync(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var skills = await _skillRepository.GetQueryable(s => s.TenantId == tenantId && s.DevelopmentNeeded)
            .Include(s => s.EmployeeGoal)
            .Include(s => s.Competency)
            .Where(s => s.EmployeeGoal.EmployeeId == employeeId && s.EmployeeGoal.AppraisalCycleId == cycleId)
            .ToListAsync(cancellationToken);

        return skills
            .GroupBy(s => s.CompetencyId)
            .Select(g => new DevelopmentSkillSuggestionDto
            {
                CompetencyId = g.Key,
                CompetencyName = g.First().Competency?.CriteriaName,
                FromGoals = g.Select(s => s.EmployeeGoal?.Title ?? string.Empty)
                             .Where(t => !string.IsNullOrWhiteSpace(t))
                             .Distinct()
                             .ToList()
            })
            .OrderBy(d => d.CompetencyName)
            .ToList();
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private static GoalRequiredSkillDto ToDto(GoalRequiredSkill s) => new()
    {
        Id = s.Id,
        EmployeeGoalId = s.EmployeeGoalId,
        CompetencyId = s.CompetencyId,
        CompetencyName = s.Competency?.CriteriaName,
        DevelopmentNeeded = s.DevelopmentNeeded,
        Note = s.Note
    };

    private static CheckInObjectiveLinkDto ToDto(CheckInObjectiveLink l) => new()
    {
        Id = l.Id,
        CheckInId = l.CheckInId,
        CompanyGoalId = l.CompanyGoalId,
        ObjectiveTitle = l.CompanyGoal?.Title
    };
}
