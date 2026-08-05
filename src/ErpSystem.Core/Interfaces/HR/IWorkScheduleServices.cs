using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// WORK SCHEDULE SERVICE
// ============================================================================

#region Work Schedule Service

public interface IWorkScheduleService
{
    Task<WorkScheduleDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkScheduleDto?> GetDefaultScheduleAsync(CancellationToken ct = default);
    Task<IEnumerable<WorkScheduleSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<WorkScheduleSummaryDto>> GetActiveSchedulesAsync(CancellationToken ct = default);
    Task<IEnumerable<WorkScheduleSummaryDto>> GetByTypeAsync(WorkScheduleType type, CancellationToken ct = default);
    Task<PagedResult<WorkScheduleSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<WorkScheduleDto> CreateAsync(CreateWorkScheduleDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<WorkScheduleDto> UpdateAsync(UpdateWorkScheduleDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // Shift definition sub-operations
    Task<ShiftDefinitionDto> AddShiftAsync(CreateShiftDefinitionDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ShiftDefinitionSummaryDto>> GetShiftsAsync(Guid workScheduleId, CancellationToken ct = default);
    Task<ShiftDefinitionDto> UpdateShiftAsync(UpdateShiftDefinitionDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteShiftAsync(Guid shiftId, CancellationToken ct = default);
}

#endregion

// ============================================================================
// EMPLOYEE WORK SCHEDULE SERVICE
// ============================================================================

#region Employee Work Schedule Service

public interface IEmployeeWorkScheduleService
{
    Task<EmployeeWorkScheduleDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EmployeeWorkScheduleDto?> GetCurrentForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<EmployeeWorkScheduleDto>> GetAllForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<EmployeeWorkScheduleDto>> GetByWorkScheduleIdAsync(Guid workScheduleId, CancellationToken ct = default);
    Task<PagedResult<EmployeeWorkScheduleDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<EmployeeWorkScheduleDto> AssignAsync(AssignEmployeeWorkScheduleDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<EmployeeWorkScheduleDto> UpdateAsync(UpdateEmployeeWorkScheduleDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// SHIFT DEFINITION SERVICE
// ============================================================================

#region Shift Definition Service

public interface IShiftDefinitionService
{
    Task<ShiftDefinitionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ShiftDefinitionSummaryDto>> GetByWorkScheduleIdAsync(Guid workScheduleId, CancellationToken ct = default);
    Task<IEnumerable<ShiftDefinitionSummaryDto>> GetActiveShiftsAsync(CancellationToken ct = default);
    Task<IEnumerable<ShiftDefinitionSummaryDto>> GetNightShiftsAsync(CancellationToken ct = default);
    Task<IEnumerable<ShiftDefinitionSummaryDto>> GetByTypeAsync(ShiftType type, CancellationToken ct = default);
    Task<IEnumerable<ShiftDefinitionSummaryDto>> GetAllAsync(CancellationToken ct = default);

    Task<ShiftDefinitionDto> CreateAsync(CreateShiftDefinitionDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<ShiftDefinitionDto> UpdateAsync(UpdateShiftDefinitionDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// SHIFT ASSIGNMENT SERVICE
// ============================================================================

#region Shift Assignment Service

public interface IShiftAssignmentService
{
    Task<ShiftAssignmentDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ShiftAssignmentDto?> GetCurrentAssignmentForEmployeeAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<ShiftAssignmentDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken ct = default);
    Task<IEnumerable<ShiftAssignmentDto>> GetByShiftDefinitionIdAsync(Guid shiftDefinitionId, CancellationToken ct = default);
    Task<IEnumerable<ShiftAssignmentDto>> GetActiveAssignmentsAsync(DateTime? asOf = null, CancellationToken ct = default);

    Task<ShiftAssignmentDto> AssignAsync(CreateShiftAssignmentDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<ShiftAssignmentDto> UpdateAsync(UpdateShiftAssignmentDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

#endregion

// ============================================================================
// SHIFT ROTATION PLAN SERVICE
// ============================================================================

#region Shift Rotation Plan Service

public interface IShiftRotationPlanService
{
    Task<ShiftRotationPlanDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetActivePlansAsync(CancellationToken ct = default);
    Task<IEnumerable<ShiftRotationPlanSummaryDto>> GetByRotationCycleAsync(ShiftRotationCycle cycle, CancellationToken ct = default);
    Task<ShiftRotationPlanDto> GetWithStagesAsync(Guid id, CancellationToken ct = default);
    Task<ShiftRotationPlanDto> GetWithMembersAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ShiftRotationPlanSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);

    Task<ShiftRotationPlanDto> CreateAsync(CreateShiftRotationPlanDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<ShiftRotationPlanDto> UpdateAsync(UpdateShiftRotationPlanDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // Stage sub-operations
    Task<ShiftRotationStageDto> AddStageAsync(CreateShiftRotationStageDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ShiftRotationStageDto>> GetStagesAsync(Guid planId, CancellationToken ct = default);
    Task<ShiftRotationStageDto> UpdateStageAsync(UpdateShiftRotationStageDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteStageAsync(Guid stageId, CancellationToken ct = default);

    // Member sub-operations
    Task<ShiftRotationMemberDto> EnrollMemberAsync(AddShiftRotationMemberDto dto, Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IEnumerable<ShiftRotationMemberDto>> GetMembersAsync(Guid planId, CancellationToken ct = default);
    Task<ShiftRotationMemberDto> UpdateMemberAsync(UpdateShiftRotationMemberDto dto, Guid userId, CancellationToken ct = default);
    Task<bool> RemoveMemberAsync(Guid memberId, Guid userId, CancellationToken ct = default);
}

#endregion
