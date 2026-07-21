using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

public class EmployeeGoalService : IEmployeeGoalService
{
    private readonly IGenericRepository<EmployeeGoal> _goalRepository;
    private readonly IGenericRepository<GoalProgressEntry> _progressRepository;
    private readonly IGenericRepository<Employee> _employeeRepository;
    private readonly IGenericRepository<AppraisalCycle> _cycleRepository;
    private readonly IGenericRepository<PerformanceAppraisal> _appraisalRepository;
    private readonly IGenericRepository<EmployeeGoalAppraisalAssessment> _assessmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmployeeGoalService> _logger;

    public EmployeeGoalService(
        IGenericRepository<EmployeeGoal> goalRepository,
        IGenericRepository<GoalProgressEntry> progressRepository,
        IGenericRepository<Employee> employeeRepository,
        IGenericRepository<AppraisalCycle> cycleRepository,
        IGenericRepository<PerformanceAppraisal> appraisalRepository,
        IGenericRepository<EmployeeGoalAppraisalAssessment> assessmentRepository,
        IUnitOfWork unitOfWork,
        ILogger<EmployeeGoalService> logger)
    {
        _goalRepository = goalRepository;
        _progressRepository = progressRepository;
        _employeeRepository = employeeRepository;
        _cycleRepository = cycleRepository;
        _appraisalRepository = appraisalRepository;
        _assessmentRepository = assessmentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    private IQueryable<EmployeeGoal> BaseQuery => _goalRepository.GetQueryable()
        .Include(g => g.Employee)
        .Include(g => g.AppraisalCycle)
        .Include(g => g.ParentCompanyGoal)
        .Include(g => g.ParentUnitGoal)
        .Include(g => g.ParentGoal)
        .Include(g => g.LibraryItem)
        .Include(g => g.KpiDefinition)
        .Include(g => g.Manager);

    /// <summary>Loads the AppraisalSettings for a cycle (1:1), or null if none is configured.</summary>
    private async Task<AppraisalSettings?> GetSettingsForCycleAsync(Guid cycleId, CancellationToken cancellationToken) =>
        (await _cycleRepository.GetQueryable(c => c.Id == cycleId)
            .Include(c => c.AppraisalSettings)
            .FirstOrDefaultAsync(cancellationToken))?.AppraisalSettings;

    // ─── CRUD ────────────────────────────────────────────────────────────────

    public async Task<EmployeeGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await BaseQuery.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<EmployeeGoalDto>> GetByEmployeeIdAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.Where(g => g.EmployeeId == employeeId);
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderBy(g => g.Status).ThenBy(g => g.DueDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<IEnumerable<EmployeeGoalDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        var appraisal = await _appraisalRepository.GetQueryable()
            .Where(a => a.Id == appraisalId)
            .Select(a => new { a.EmployeeId, a.AppraisalCycleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (appraisal == null)
            return Enumerable.Empty<EmployeeGoalDto>();

        var entities = await BaseQuery
            .Where(g => g.EmployeeId == appraisal.EmployeeId
                     && g.AppraisalCycleId == appraisal.AppraisalCycleId)
            .OrderBy(g => g.DueDate)
            .ToListAsync(cancellationToken);

        var dtos = entities.ToDtoList();

        // Merge in the persisted appraisal-scoped assessments
        var assessments = await _assessmentRepository.GetQueryable()
            .Where(a => a.PerformanceAppraisalId == appraisalId)
            .ToListAsync(cancellationToken);

        if (assessments.Count > 0)
        {
            var assessmentMap = assessments.ToDictionary(a => a.EmployeeGoalId);
            foreach (var dto in dtos)
            {
                if (!assessmentMap.TryGetValue(dto.Id, out var a)) continue;
                dto.SelfFinalProgressPercent  = a.SelfFinalProgressPercent;
                dto.SelfFinalStatus           = a.SelfFinalStatus;
                dto.SelfFinalActualValue      = a.SelfFinalActualValue;
                dto.SelfAssessmentNotes       = a.SelfAssessmentNotes;
                dto.SelfEvidenceLinks         = a.SelfEvidenceLinks;
                dto.ManagerFinalProgressPercent = a.ManagerFinalProgressPercent;
                dto.ManagerFinalStatus          = a.ManagerFinalStatus;
                dto.ManagerFinalActualValue     = a.ManagerFinalActualValue;
                dto.ManagerAssessmentNotes      = a.ManagerAssessmentNotes;
                dto.ManagerEvidenceLinks        = a.ManagerEvidenceLinks;
            }
        }

        return dtos;
    }

    public async Task<IEnumerable<EmployeeGoalDto>> GetPendingApprovalAsync(Guid managerId, Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.Where(g => g.SubmittedToManagerId == managerId && g.Status == GoalStatus.PendingApproval);
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        var entities = await query.OrderBy(g => g.SubmittedDate).ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<PagedResult<EmployeeGoalDto>> GetPagedAsync(
        int pageNumber, int pageSize,
        Guid? employeeId = null, Guid? cycleId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery.AsQueryable();
        if (employeeId.HasValue)
            query = query.Where(g => g.EmployeeId == employeeId.Value);
        if (cycleId.HasValue)
            query = query.Where(g => g.AppraisalCycleId == cycleId.Value);
        query = query.OrderBy(g => g.EmployeeId).ThenBy(g => g.DueDate);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<EmployeeGoalDto>
        {
            Items = items.ToDtoList(),
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<EmployeeGoalDto> CreateAsync(CreateEmployeeGoalDto createDto, CancellationToken cancellationToken = default)
    {
        var employeeExists = await _employeeRepository.ExistsAsync(e => e.Id == createDto.EmployeeId);
        if (!employeeExists)
            throw new ArgumentException("Employee not found.");

        var cycleExists = await _cycleRepository.ExistsAsync(c => c.Id == createDto.AppraisalCycleId);
        if (!cycleExists)
            throw new ArgumentException("Appraisal cycle not found.");

        // Enforce the configured per-cycle goal ceiling (AppraisalSettings.MaxGoalsPerEmployee).
        var settings = await GetSettingsForCycleAsync(createDto.AppraisalCycleId, cancellationToken);
        if (settings?.MaxGoalsPerEmployee is int maxGoals && maxGoals > 0)
        {
            var existingGoalCount = await _goalRepository.GetQueryable(g =>
                    g.EmployeeId == createDto.EmployeeId
                    && g.AppraisalCycleId == createDto.AppraisalCycleId
                    && g.Status != GoalStatus.Rejected)
                .CountAsync(cancellationToken);

            if (existingGoalCount >= maxGoals)
                throw new InvalidOperationException($"This employee already has the maximum of {maxGoals} goal(s) allowed for this cycle.");
        }

        var entity = createDto.ToEntity();

        // Auto-link to the appraisal if one already exists for this employee + cycle.
        if (entity.PerformanceAppraisalId == null)
        {
            var appraisal = await _appraisalRepository.FirstOrDefaultAsync(
                a => a.EmployeeId == entity.EmployeeId && a.AppraisalCycleId == entity.AppraisalCycleId);
            if (appraisal != null)
                entity.PerformanceAppraisalId = appraisal.Id;
        }

        await _goalRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee goal created: {Id} for employee {EmployeeId}", entity.Id, entity.EmployeeId);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<EmployeeGoalDto> UpdateAsync(UpdateEmployeeGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        var entity = await _goalRepository.GetByIdAsync(updateDto.Id);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{updateDto.Id}' not found.");

        if (entity.IsLocked)
            throw new InvalidOperationException("This goal is locked and cannot be edited.");

        updateDto.UpdateEntity(entity);
        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee goal updated: {Id}", entity.Id);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _goalRepository.GetByIdAsync(id);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{id}' not found.");

        if (entity.IsLocked)
            throw new InvalidOperationException("Locked goals cannot be deleted.");

        await _goalRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee goal deleted: {Id}", id);
        return true;
    }

    // ─── Approval Workflow ───────────────────────────────────────────────────

    public async Task<EmployeeGoalDto> SubmitForApprovalAsync(Guid goalId, Guid managerId, CancellationToken cancellationToken = default)
    {
        var entity = await _goalRepository.GetByIdAsync(goalId);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{goalId}' not found.");

        if (entity.Status != GoalStatus.Draft)
            throw new InvalidOperationException($"Only draft goals can be submitted. Current status: {entity.Status}");

        // Guard: total weights of all non-rejected goals in the cycle must equal 100
        await ValidateGoalWeightTotalAsync(entity.EmployeeId, entity.AppraisalCycleId, cancellationToken);

        var managerExists = await _employeeRepository.ExistsAsync(e => e.Id == managerId);
        if (!managerExists)
            throw new ArgumentException("Manager not found.");

        entity.Status = GoalStatus.PendingApproval;
        entity.SubmittedToManagerId = managerId;
        entity.SubmittedDate = DateTime.UtcNow;

        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} submitted for approval by manager {ManagerId}", goalId, managerId);
        return await GetByIdAsync(goalId, cancellationToken);
    }

    public async Task<EmployeeGoalDto> ApproveGoalAsync(Guid goalId, Guid managerId, string? feedback = null, CancellationToken cancellationToken = default)
    {
        var entity = await _goalRepository.GetByIdAsync(goalId);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{goalId}' not found.");

        if (entity.Status != GoalStatus.PendingApproval)
            throw new InvalidOperationException("Only goals pending approval can be approved.");

        if (entity.SubmittedToManagerId != managerId)
            throw new InvalidOperationException("You are not the assigned manager for this goal.");

        // Guard: manager cannot approve if the employee's total goal weights != 100
        await ValidateGoalWeightTotalAsync(entity.EmployeeId, entity.AppraisalCycleId, cancellationToken);

        entity.Status = GoalStatus.Approved;
        entity.ApprovalDate = DateTime.UtcNow;
        entity.ManagerFeedback = feedback;

        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} approved by manager {ManagerId}", goalId, managerId);
        return await GetByIdAsync(goalId, cancellationToken);
    }

    public async Task<EmployeeGoalDto> RejectGoalAsync(Guid goalId, Guid managerId, string? feedback = null, CancellationToken cancellationToken = default)
    {
        var entity = await _goalRepository.GetByIdAsync(goalId);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{goalId}' not found.");

        if (entity.Status != GoalStatus.PendingApproval)
            throw new InvalidOperationException("Only goals pending approval can be rejected.");

        if (entity.SubmittedToManagerId != managerId)
            throw new InvalidOperationException("You are not the assigned manager for this goal.");

        entity.Status = GoalStatus.Draft;
        entity.ManagerFeedback = feedback;

        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} rejected by manager {ManagerId}", goalId, managerId);
        return await GetByIdAsync(goalId, cancellationToken);
    }

    // ─── Progress Tracking ───────────────────────────────────────────────────

    public async Task<GoalProgressEntryDto> AddProgressEntryAsync(Guid goalId, CreateGoalProgressEntryDto dto, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByIdAsync(goalId);
        if (goal == null)
            throw new ArgumentException($"Employee goal with ID '{goalId}' not found.");

        if (goal.Status != GoalStatus.Approved && goal.Status != GoalStatus.AtRisk && goal.Status != GoalStatus.InProgress)
            throw new InvalidOperationException("Progress entries can only be added to approved, in-progress, or at-risk goals.");

        var entity = dto.ToEntity();
        entity.EmployeeGoalId = goalId;
        entity.EntryDate = DateTime.UtcNow;

        await _progressRepository.AddAsync(entity);

        // Update goal's progress percent from the entry
        if (entity.ProgressPercent.HasValue)
        {
            goal.ProgressPercent = entity.ProgressPercent.Value;

            // Auto-update goal status if progress is 100%
            if (entity.ProgressPercent.Value >= 100)
                goal.Status = GoalStatus.Completed;
            else if (goal.Status == GoalStatus.AtRisk && entity.ProgressPercent.Value > 0)
                goal.Status = GoalStatus.InProgress;
        }

        await _goalRepository.UpdateAsync(goal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity = await _progressRepository.GetQueryable()
            .Include(p => p.RecordedBy)
            .FirstOrDefaultAsync(p => p.Id == entity.Id, cancellationToken);

        _logger.LogInformation("Progress entry added to goal {GoalId}: {EntryId}", goalId, entity!.Id);
        return entity.ToDto();
    }

    public async Task<IEnumerable<GoalProgressEntryDto>> GetProgressEntriesAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var entities = await _progressRepository.GetQueryable(p => p.EmployeeGoalId == goalId)
            .Include(p => p.RecordedBy)
            .OrderByDescending(p => p.EntryDate)
            .ToListAsync(cancellationToken);
        return entities.ToDtoList();
    }

    public async Task<GoalProgressEntryDto> UpdateProgressEntryAsync(Guid goalId, UpdateGoalProgressEntryDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _progressRepository.GetQueryable()
            .Include(p => p.RecordedBy)
            .FirstOrDefaultAsync(p => p.Id == dto.Id && p.EmployeeGoalId == goalId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Progress entry not found.");

        dto.UpdateEntity(entity);
        await _progressRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Progress entry updated: {EntryId}", entity.Id);
        return entity.ToDto();
    }

    public async Task<bool> DeleteProgressEntryAsync(Guid goalId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entity = await _progressRepository.GetQueryable()
            .FirstOrDefaultAsync(p => p.Id == entryId && p.EmployeeGoalId == goalId, cancellationToken);

        if (entity == null)
            throw new ArgumentException("Progress entry not found.");

        await _progressRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Progress entry deleted: {EntryId}", entryId);
        return true;
    }

    // ─── Lock Management ─────────────────────────────────────────────────────

    public async Task<bool> LockGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var entity = await _goalRepository.GetByIdAsync(goalId);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{goalId}' not found.");

        entity.IsLocked = true;
        entity.LockedDate = DateTime.UtcNow;
        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} locked", goalId);
        return true;
    }

    public async Task<bool> UnlockGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        var entity = await _goalRepository.GetByIdAsync(goalId);
        if (entity == null)
            throw new ArgumentException($"Employee goal with ID '{goalId}' not found.");

        entity.IsLocked = false;
        entity.LockedDate = null;
        await _goalRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Goal {GoalId} unlocked", goalId);
        return true;
    }

    // ─── Summaries ───────────────────────────────────────────────────────────

    public async Task<EmployeeGoalSummaryDto> GetGoalSummaryAsync(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetQueryable()
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        if (employee == null)
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var cycle = await _cycleRepository.GetByIdAsync(cycleId);
        if (cycle == null)
            throw new ArgumentException($"Appraisal cycle with ID '{cycleId}' not found.");

        var goals = await _goalRepository.GetQueryable(g => g.EmployeeId == employeeId && g.AppraisalCycleId == cycleId)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Configured minimum (defaults to 1 when unset) — drives MeetsMinGoalCount.
        var settings = await GetSettingsForCycleAsync(cycleId, cancellationToken);
        var minGoals = settings?.MinGoalsPerEmployee is int m && m > 0 ? m : 1;
        var nonRejectedCount = goals.Count(g => g.Status != GoalStatus.Rejected);

        return new EmployeeGoalSummaryDto
        {
            EmployeeId = employeeId,
            EmployeeName = $"{employee.FirstName} {employee.LastName}",
            EmployeeNumber = employee.EmployeeNumber,
            CycleId = cycleId,
            CycleName = cycle.CycleName,
            TotalGoals = goals.Count,
            DraftGoals = goals.Count(g => g.Status == GoalStatus.Draft),
            PendingApprovalGoals = goals.Count(g => g.Status == GoalStatus.PendingApproval),
            ApprovedGoals = goals.Count(g => g.Status == GoalStatus.Approved || g.Status == GoalStatus.AtRisk || g.Status == GoalStatus.InProgress),
            InProgressGoals = goals.Count(g => g.Status == GoalStatus.InProgress && g.ProgressPercent > 0 && g.ProgressPercent < 100),
            CompletedGoals = goals.Count(g => g.Status == GoalStatus.Completed),
            AtRiskGoals = goals.Count(g => g.Status == GoalStatus.AtRisk),
            OverallProgressPercent = goals.Any() ? goals.Average(g => g.ProgressPercent) : 0,
            GoalSettingComplete = goals.Any(g => g.Status == GoalStatus.Approved || g.Status == GoalStatus.InProgress),
            MeetsMinGoalCount = nonRejectedCount >= minGoals  // AppraisalSettings.MinGoalsPerEmployee (defaults to 1)
        };
    }

    public async Task<IEnumerable<TeamGoalSummaryDto>> GetTeamGoalSummaryAsync(Guid managerId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        // Get all direct reports' employee IDs from goals submitted to this manager
        var teamEmployeeIds = await _employeeRepository.GetQueryable(e => e.ManagerId == managerId)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (!teamEmployeeIds.Any())
            return Enumerable.Empty<TeamGoalSummaryDto>();

        var allGoals = await _goalRepository.GetQueryable(
            g => teamEmployeeIds.Contains(g.EmployeeId) && g.AppraisalCycleId == cycleId)
            .Include(g => g.Employee)
                .ThenInclude(e => e.Position)
            .Include(g => g.Employee)
                .ThenInclude(e => e.Department)
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = new List<TeamGoalSummaryDto>();

        foreach (var empId in teamEmployeeIds)
        {
            var empGoals = allGoals.Where(g => g.EmployeeId == empId).ToList();
            var emp = empGoals.FirstOrDefault()?.Employee;

            if (emp == null)
            {
                emp = await _employeeRepository.GetQueryable()
                    .Include(e => e.Position)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == empId, cancellationToken);
            }

            if (emp == null) continue;

            result.Add(new TeamGoalSummaryDto
            {
                EmployeeId = empId,
                EmployeeName = $"{emp.FirstName} {emp.LastName}",
                EmployeeNumber = emp.EmployeeNumber,
                PositionName = emp.Position?.Title,
                DepartmentName = emp.Department?.Name,
                TotalGoals = empGoals.Count,
                ApprovedGoals = empGoals.Count(g => g.Status == GoalStatus.Approved || g.Status == GoalStatus.InProgress || g.Status == GoalStatus.Completed),
                PendingApprovalGoals = empGoals.Count(g => g.Status == GoalStatus.PendingApproval),
                AtRiskGoals = empGoals.Count(g => g.Status == GoalStatus.AtRisk),
                OverallProgressPercent = empGoals.Any() ? empGoals.Average(g => g.ProgressPercent) : 0,
                HasOverdueGoals = empGoals.Any(g => g.DueDate < today && g.Status != GoalStatus.Completed),
                EarliestOverdueDueDate = empGoals
                    .Where(g => g.DueDate < today && g.Status != GoalStatus.Completed)
                    .OrderBy(g => g.DueDate)
                    .Select(g => g.DueDate)
                    .Cast<DateOnly?>()
                    .FirstOrDefault()
            });
        }

        return result.OrderBy(r => r.EmployeeName);
    }

    // ─── Private Validation Helpers ───────────────────────────────────────

    /// <summary>
    /// Validates that the sum of all non-rejected goal weights for an employee in a
    /// given cycle equals 100. Called before Submit and Approve to enforce the rule
    /// that goal weights must be properly distributed before progressing.
    /// </summary>
    private async Task ValidateGoalWeightTotalAsync(
        Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        var totalWeight = await _goalRepository
            .GetQueryable(g => g.EmployeeId == employeeId
                            && g.AppraisalCycleId == cycleId
                            && g.Status != GoalStatus.Rejected)
            .SumAsync(g => (int?)g.Weight, cancellationToken) ?? 0;

        if (totalWeight != 100)
            throw new InvalidOperationException(
                $"Goal weights for this employee in the current cycle must sum to exactly 100 " +
                $"(current total: {totalWeight}). " +
                $"Adjust goal weights across all goals in this cycle before proceeding.");
    }
}
