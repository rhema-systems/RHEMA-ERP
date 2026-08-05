using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// WORK SCHEDULE
// ============================================================================

#region Work Schedule

public interface IWorkScheduleRepository : IGenericRepository<WorkSchedule>
{
    /// <summary>Returns the tenant-wide default work schedule, or null if none is set.</summary>
    Task<WorkSchedule?> GetDefaultScheduleAsync();

    /// <summary>Returns all active work schedules for the tenant.</summary>
    Task<IEnumerable<WorkSchedule>> GetActiveSchedulesAsync();

    /// <summary>Returns work schedules filtered by schedule type.</summary>
    Task<IEnumerable<WorkSchedule>> GetByTypeAsync(WorkScheduleType type);

    /// <summary>Returns a work schedule fully loaded with its shift definitions.</summary>
    Task<WorkSchedule?> GetWithShiftsAsync(Guid id);
}

#endregion

// ============================================================================
// EMPLOYEE WORK SCHEDULE
// ============================================================================

#region Employee Work Schedule

public interface IEmployeeWorkScheduleRepository : IGenericRepository<EmployeeWorkSchedule>
{
    /// <summary>Returns the currently active schedule assignment for an employee (IsCurrent = true).</summary>
    Task<EmployeeWorkSchedule?> GetCurrentForEmployeeAsync(Guid employeeId);

    /// <summary>Returns all schedule assignment history for an employee, newest first.</summary>
    Task<IEnumerable<EmployeeWorkSchedule>> GetAllForEmployeeAsync(Guid employeeId);

    /// <summary>Returns all employee assignments to a specific work schedule.</summary>
    Task<IEnumerable<EmployeeWorkSchedule>> GetByWorkScheduleIdAsync(Guid workScheduleId);

    /// <summary>Returns a page of all schedule assignments across employees (with total count), newest effective date first.</summary>
    Task<(IEnumerable<EmployeeWorkSchedule> Items, int TotalCount)> GetPagedWithCountAsync(int pageNumber, int pageSize);

    /// <summary>Returns all assignments that were active as of a specific date.</summary>
    Task<IEnumerable<EmployeeWorkSchedule>> GetActiveAssignmentsAsOfAsync(DateOnly asOfDate);
}

#endregion

// ============================================================================
// SHIFT DEFINITION
// ============================================================================

#region Shift Definition

public interface IShiftDefinitionRepository : IGenericRepository<ShiftDefinition>
{
    /// <summary>Returns all shift definitions belonging to a work schedule, ordered by display order.</summary>
    Task<IEnumerable<ShiftDefinition>> GetByWorkScheduleIdAsync(Guid workScheduleId);

    /// <summary>Returns all active shift definitions across all work schedules.</summary>
    Task<IEnumerable<ShiftDefinition>> GetActiveShiftsAsync();

    /// <summary>Returns shift definitions flagged as night shifts.</summary>
    Task<IEnumerable<ShiftDefinition>> GetNightShiftsAsync();

    /// <summary>Returns shift definitions filtered by shift type.</summary>
    Task<IEnumerable<ShiftDefinition>> GetByTypeAsync(ShiftType type);

    /// <summary>Returns all shift definitions across work schedules.</summary>
    Task<IEnumerable<ShiftDefinition>> GetAllAsync();
}

#endregion

// ============================================================================
// SHIFT ASSIGNMENT
// ============================================================================

#region Shift Assignment

public interface IShiftAssignmentRepository : IGenericRepository<ShiftAssignment>
{
    /// <summary>Returns all shift assignments for an employee, ordered by assignment date descending.</summary>
    Task<IEnumerable<ShiftAssignment>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns the currently active shift assignment for an employee, or null if none.</summary>
    Task<ShiftAssignment?> GetCurrentAssignmentForEmployeeAsync(Guid employeeId);

    /// <summary>Returns all assignments for a specific shift definition.</summary>
    Task<IEnumerable<ShiftAssignment>> GetByShiftDefinitionIdAsync(Guid shiftDefinitionId);

    /// <summary>Returns all assignments that are active on or after the given date (open-ended or end date not passed).</summary>
    Task<IEnumerable<ShiftAssignment>> GetActiveAssignmentsAsync(DateTime? asOf = null);
}

#endregion

// ============================================================================
// SHIFT ROTATION PLAN
// ============================================================================

#region Shift Rotation Plan

public interface IShiftRotationPlanRepository : IGenericRepository<ShiftRotationPlan>
{
    /// <summary>Returns all active rotation plans (IsActive = true).</summary>
    Task<IEnumerable<ShiftRotationPlan>> GetActivePlansAsync();

    /// <summary>Returns rotation plans filtered by their rotation cycle (Weekly, Monthly, etc.).</summary>
    Task<IEnumerable<ShiftRotationPlan>> GetByRotationCycleAsync(ShiftRotationCycle cycle);

    /// <summary>Returns a rotation plan fully loaded with its ordered stages and shift details.</summary>
    Task<ShiftRotationPlan?> GetWithStagesAsync(Guid id);

    /// <summary>Returns a rotation plan fully loaded with all its current members and their employee details.</summary>
    Task<ShiftRotationPlan?> GetWithMembersAsync(Guid id);
}

#endregion

// ============================================================================
// SHIFT ROTATION STAGE
// ============================================================================

#region Shift Rotation Stage

public interface IShiftRotationStageRepository : IGenericRepository<ShiftRotationStage>
{
    /// <summary>Returns all stages for a rotation plan, ordered by stage order ascending.</summary>
    Task<IEnumerable<ShiftRotationStage>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns all stages across plans that reference a specific shift definition.</summary>
    Task<IEnumerable<ShiftRotationStage>> GetByShiftDefinitionIdAsync(Guid shiftDefinitionId);
}

#endregion

// ============================================================================
// SHIFT ROTATION MEMBER
// ============================================================================

#region Shift Rotation Member

public interface IShiftRotationMemberRepository : IGenericRepository<ShiftRotationMember>
{
    /// <summary>Returns all active members enrolled in a rotation plan (ExitDate is null).</summary>
    Task<IEnumerable<ShiftRotationMember>> GetByPlanIdAsync(Guid planId);

    /// <summary>Returns all rotation plan memberships for an employee.</summary>
    Task<IEnumerable<ShiftRotationMember>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Returns the active rotation plan membership for an employee, or null if not enrolled in any plan.</summary>
    Task<ShiftRotationMember?> GetCurrentPlanForEmployeeAsync(Guid employeeId);

    /// <summary>Returns all active members of a plan assigned to a specific organization unit.</summary>
    Task<IEnumerable<ShiftRotationMember>> GetByOrganizationUnitIdAsync(Guid planId, Guid organizationUnitId);
}

#endregion
