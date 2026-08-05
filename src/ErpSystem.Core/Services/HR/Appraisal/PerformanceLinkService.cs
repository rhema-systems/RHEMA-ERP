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

    public async Task<IEnumerable<GoalRequiredSkillDto>> SetGoalRequiredSkillsAsync(Guid employeeGoalId, IEnumerable<SetGoalRequiredSkillDto> skills, CancellationToken cancellationToken = default)
    {
        var goal = await GetOwnedGoalAsync(employeeGoalId, cancellationToken);
        var existing = await _skillRepository.GetQueryable(s => s.EmployeeGoalId == employeeGoalId)
            .ToListAsync(cancellationToken);

        foreach (var e in existing)
            await _skillRepository.DeleteAsync(e);

        foreach (var dto in skills.DistinctBy(s => s.CompetencyId))
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

    public async Task<IEnumerable<CheckInObjectiveLinkDto>> SetCheckInObjectivesAsync(Guid checkInId, IEnumerable<Guid> companyGoalIds, CancellationToken cancellationToken = default)
    {
        var checkIn = await GetOwnedCheckInAsync(checkInId, cancellationToken);
        var tenantId = checkIn.TenantId;

        var existing = await _objectiveRepository.GetQueryable(l => l.CheckInId == checkInId)
            .ToListAsync(cancellationToken);

        foreach (var e in existing)
            await _objectiveRepository.DeleteAsync(e);

        foreach (var goalId in companyGoalIds.Distinct())
        {
            var companyGoal = await _companyGoalRepository.GetByIdAsync(goalId);
            if (companyGoal == null || companyGoal.TenantId != tenantId)
                throw new ArgumentException($"Company goal with ID '{goalId}' not found.");

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
