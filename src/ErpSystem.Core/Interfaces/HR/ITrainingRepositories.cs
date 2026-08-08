using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// VENDOR & TRAINER INTERFACES
// ============================================================================

#region Training Vendor

public interface ITrainingVendorRepository : IGenericRepository<TrainingVendor>
{
    /// <summary>Gets a vendor by its unique vendor code, including its trainer profiles.</summary>
    Task<TrainingVendor?> GetByVendorCodeAsync(string vendorCode);

    /// <summary>Gets all vendors of the specified type.</summary>
    Task<IEnumerable<TrainingVendor>> GetByVendorTypeAsync(TrainingVendorType type);

    /// <summary>Gets all active, non-blacklisted vendors.</summary>
    Task<IEnumerable<TrainingVendor>> GetActiveVendorsAsync();

    /// <summary>Gets all preferred, active vendors.</summary>
    Task<IEnumerable<TrainingVendor>> GetPreferredVendorsAsync();

    /// <summary>Gets all blacklisted vendors.</summary>
    Task<IEnumerable<TrainingVendor>> GetBlacklistedVendorsAsync();

    /// <summary>Gets vendors whose accreditation has already expired.</summary>
    Task<IEnumerable<TrainingVendor>> GetWithExpiredAccreditationAsync();

    /// <summary>Gets vendors whose accreditation expires within the specified number of days.</summary>
    Task<IEnumerable<TrainingVendor>> GetWithExpiringAccreditationAsync(int daysAhead = 30);

    /// <summary>Gets a vendor with full details including trainers, trainer skills, and availability.</summary>
    Task<TrainingVendor?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Trainer Profile

public interface ITrainerProfileRepository : IGenericRepository<TrainerProfile>
{
    /// <summary>Gets the trainer profile for an internal employee by employee ID.</summary>
    Task<TrainerProfile?> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets all trainer profiles belonging to an external vendor.</summary>
    Task<IEnumerable<TrainerProfile>> GetByVendorIdAsync(Guid vendorId);

    /// <summary>Gets all active trainer profiles.</summary>
    Task<IEnumerable<TrainerProfile>> GetActiveTrainersAsync();

    /// <summary>Gets a trainer profile with full details including employee, vendor, skills, and availability.</summary>
    Task<TrainerProfile?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Gets trainers who have at least one availability window covering the specified date range.</summary>
    Task<IEnumerable<TrainerProfile>> GetAvailableForDateRangeAsync(DateTime from, DateTime to);
}

#endregion

#region Trainer Skill

public interface ITrainerSkillRepository : IGenericRepository<TrainerSkill>
{
    /// <summary>Gets all skills registered for a trainer profile, including skill details.</summary>
    Task<IEnumerable<TrainerSkill>> GetByTrainerProfileIdAsync(Guid trainerProfileId);

    /// <summary>Gets all trainer-skill records for a given skill, indicating which trainers can deliver it.</summary>
    Task<IEnumerable<TrainerSkill>> GetBySkillIdAsync(Guid skillId);
}

#endregion

#region Trainer Availability

public interface ITrainerAvailabilityRepository : IGenericRepository<TrainerAvailability>
{
    /// <summary>Gets all availability windows for a trainer profile.</summary>
    Task<IEnumerable<TrainerAvailability>> GetByTrainerProfileIdAsync(Guid trainerProfileId);

    /// <summary>Gets blocked (unavailable) periods for a trainer profile.</summary>
    Task<IEnumerable<TrainerAvailability>> GetBlockedPeriodsAsync(Guid trainerProfileId);
}

#endregion

// ============================================================================
// TRAINING PROGRAM INTERFACES
// ============================================================================

#region Training Program

public interface ITrainingProgramRepository : IGenericRepository<TrainingProgram>
{
    /// <summary>Gets a training program by its unique program code.</summary>
    Task<TrainingProgram?> GetByProgramCodeAsync(string programCode);

    /// <summary>Gets all training programs in the specified (configurable) category.</summary>
    Task<IEnumerable<TrainingProgram>> GetByCategoryAsync(Guid categoryOptionId);

    /// <summary>Gets all training programs of the specified type.</summary>
    Task<IEnumerable<TrainingProgram>> GetByTypeAsync(TrainingType type);

    /// <summary>Gets all active training programs.</summary>
    Task<IEnumerable<TrainingProgram>> GetActiveAsync();

    /// <summary>Gets training programs that provide a certificate upon completion.</summary>
    Task<IEnumerable<TrainingProgram>> GetWithCertificateAsync();

    /// <summary>Gets training programs that require management approval before nomination.</summary>
    Task<IEnumerable<TrainingProgram>> GetRequiringApprovalAsync();

    /// <summary>Gets a training program with full details including competencies, skills, materials, and schedules.</summary>
    Task<TrainingProgram?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Training Material

public interface ITrainingMaterialRepository : IGenericRepository<TrainingMaterial>
{
    /// <summary>Gets all materials for a training program.</summary>
    Task<IEnumerable<TrainingMaterial>> GetByProgramIdAsync(Guid programId);

    /// <summary>Gets publicly accessible materials for a training program.</summary>
    Task<IEnumerable<TrainingMaterial>> GetPublicMaterialsAsync(Guid programId);

    /// <summary>Gets active materials for a training program.</summary>
    Task<IEnumerable<TrainingMaterial>> GetActiveByProgramAsync(Guid programId);
}

#endregion

#region Training Program Competency

public interface ITrainingProgramCompetencyRepository : IGenericRepository<TrainingProgramCompetency>
{
    /// <summary>Gets all competencies linked to a training program, including competency details.</summary>
    Task<IEnumerable<TrainingProgramCompetency>> GetByProgramIdAsync(Guid programId);

    /// <summary>Gets all program-competency links for a given competency, showing which programs address it.</summary>
    Task<IEnumerable<TrainingProgramCompetency>> GetByCompetencyIdAsync(Guid competencyId);
}

#endregion

#region Training Program Skill

public interface ITrainingProgramSkillRepository : IGenericRepository<TrainingProgramSkill>
{
    /// <summary>Gets all skills linked to a training program, including skill details.</summary>
    Task<IEnumerable<TrainingProgramSkill>> GetByProgramIdAsync(Guid programId);

    /// <summary>Gets all program-skill links for a given skill, showing which programs build it.</summary>
    Task<IEnumerable<TrainingProgramSkill>> GetBySkillIdAsync(Guid skillId);
}

#endregion

// ============================================================================
// SCHEDULE & SESSION INTERFACES
// ============================================================================

#region Training Schedule

public interface ITrainingScheduleRepository : IGenericRepository<TrainingSchedule>
{
    /// <summary>Gets a training schedule by its unique schedule number.</summary>
    Task<TrainingSchedule?> GetByScheduleNumberAsync(string scheduleNumber);

    /// <summary>Gets all schedules for a training program.</summary>
    Task<IEnumerable<TrainingSchedule>> GetByProgramIdAsync(Guid programId);

    /// <summary>Gets all schedules with the specified status.</summary>
    Task<IEnumerable<TrainingSchedule>> GetByStatusAsync(ScheduleStatus status);

    /// <summary>Gets upcoming schedules starting within the specified number of days.</summary>
    Task<IEnumerable<TrainingSchedule>> GetUpcomingSchedulesAsync(int daysAhead = 90);

    /// <summary>Gets schedules that are currently in progress (started but not yet ended).</summary>
    Task<IEnumerable<TrainingSchedule>> GetCurrentlyRunningAsync();

    /// <summary>Gets all schedules assigned to a specific trainer profile.</summary>
    Task<IEnumerable<TrainingSchedule>> GetByTrainerProfileIdAsync(Guid trainerProfileId);

    /// <summary>Gets all schedules associated with a specific training vendor.</summary>
    Task<IEnumerable<TrainingSchedule>> GetByVendorIdAsync(Guid vendorId);

    /// <summary>Gets all schedules charged against a training budget.</summary>
    Task<IEnumerable<TrainingSchedule>> GetByBudgetIdAsync(Guid budgetId);

    /// <summary>Gets schedules awaiting approval.</summary>
    Task<IEnumerable<TrainingSchedule>> GetPendingApprovalAsync();

    /// <summary>Gets schedules whose registration window is currently open.</summary>
    Task<IEnumerable<TrainingSchedule>> GetWithRegistrationOpenAsync();

    /// <summary>Gets schedules that are open for registration and still have available participant slots.</summary>
    Task<IEnumerable<TrainingSchedule>> GetWithAvailableSlotsAsync();

    /// <summary>Gets a schedule with full details including program, trainer, vendor, sessions, nominations, attendance, feedback, and budget.</summary>
    Task<TrainingSchedule?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Training Session

public interface ITrainingSessionRepository : IGenericRepository<TrainingSession>
{
    /// <summary>Gets all sessions for a training schedule, ordered by session date.</summary>
    Task<IEnumerable<TrainingSession>> GetByScheduleIdAsync(Guid scheduleId);

    /// <summary>Gets all training sessions taking place on a specific date.</summary>
    Task<IEnumerable<TrainingSession>> GetByDateAsync(DateTime date);
}

#endregion

// ============================================================================
// NOMINATION & COMPLETION INTERFACES
// ============================================================================

#region Training Nomination

public interface ITrainingNominationRepository : IGenericRepository<TrainingNomination>
{
    /// <summary>Gets a nomination by its unique nomination number.</summary>
    Task<TrainingNomination?> GetByNominationNumberAsync(string nominationNumber);

    /// <summary>Gets all nominations for a training schedule, including employee details.</summary>
    Task<IEnumerable<TrainingNomination>> GetByScheduleIdAsync(Guid scheduleId);

    /// <summary>Gets all nominations for an employee.</summary>
    Task<IEnumerable<TrainingNomination>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets all nominations with the specified status.</summary>
    Task<IEnumerable<TrainingNomination>> GetByStatusAsync(NominationStatus status);

    /// <summary>Gets nominations awaiting supervisor approval.</summary>
    Task<IEnumerable<TrainingNomination>> GetPendingSupervisorApprovalAsync();

    /// <summary>Gets nominations awaiting HR approval.</summary>
    Task<IEnumerable<TrainingNomination>> GetPendingHrApprovalAsync();

    /// <summary>Gets confirmed nominations for a schedule.</summary>
    Task<IEnumerable<TrainingNomination>> GetConfirmedForScheduleAsync(Guid scheduleId);

    /// <summary>Gets nominations linked to a specific training needs assessment.</summary>
    Task<IEnumerable<TrainingNomination>> GetByNeedsAssessmentIdAsync(Guid assessmentId);

    /// <summary>Gets a nomination with full details including schedule, program, employee, approvers, and completion record.</summary>
    Task<TrainingNomination?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Training Completion

public interface ITrainingCompletionRepository : IGenericRepository<TrainingCompletion>
{
    /// <summary>Gets the completion record for a specific nomination.</summary>
    Task<TrainingCompletion?> GetByNominationIdAsync(Guid nominationId);

    /// <summary>Gets all training completion records for an employee.</summary>
    Task<IEnumerable<TrainingCompletion>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets all completion records with the specified status.</summary>
    Task<IEnumerable<TrainingCompletion>> GetByStatusAsync(TrainingCompletionStatus status);

    /// <summary>Gets passed completion records for a training program across all its schedules.</summary>
    Task<IEnumerable<TrainingCompletion>> GetPassedCompletionsForProgramAsync(Guid programId);

    /// <summary>Gets completion records pending manager verification.</summary>
    Task<IEnumerable<TrainingCompletion>> GetPendingManagerVerificationAsync();

    /// <summary>Gets all completion records for a training schedule.</summary>
    Task<IEnumerable<TrainingCompletion>> GetByScheduleIdAsync(Guid scheduleId);
}

#endregion

#region Training Attendance

public interface ITrainingAttendanceRepository : IGenericRepository<TrainingAttendance>
{
    /// <summary>Gets all attendance records for a training schedule, including employee details.</summary>
    Task<IEnumerable<TrainingAttendance>> GetByScheduleIdAsync(Guid scheduleId);

    /// <summary>Gets the attendance history for an employee across all schedules.</summary>
    Task<IEnumerable<TrainingAttendance>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets the attendance register for a specific schedule on a specific date.</summary>
    Task<IEnumerable<TrainingAttendance>> GetByScheduleAndDateAsync(Guid scheduleId, DateTime date);

    /// <summary>Gets attendance records where the employee was absent for a schedule.</summary>
    Task<IEnumerable<TrainingAttendance>> GetAbsenteesForScheduleAsync(Guid scheduleId);
}

#endregion

#region Training Feedback

public interface ITrainingFeedbackRepository : IGenericRepository<TrainingFeedback>
{
    /// <summary>Gets all feedback submitted for a training schedule, including employee details.</summary>
    Task<IEnumerable<TrainingFeedback>> GetByScheduleIdAsync(Guid scheduleId);

    /// <summary>Gets all feedback submitted by a specific employee.</summary>
    Task<IEnumerable<TrainingFeedback>> GetByEmployeeIdAsync(Guid employeeId);
}

#endregion

#region Training Follow-Up Assessment

public interface ITrainingFollowUpAssessmentRepository : IGenericRepository<TrainingFollowUpAssessment>
{
    /// <summary>Gets all follow-up assessments for a training schedule.</summary>
    Task<IEnumerable<TrainingFollowUpAssessment>> GetByScheduleIdAsync(Guid scheduleId);

    /// <summary>Gets all follow-up assessments for an employee.</summary>
    Task<IEnumerable<TrainingFollowUpAssessment>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets follow-up assessments of a specific type for a training schedule.</summary>
    Task<IEnumerable<TrainingFollowUpAssessment>> GetByAssessmentTypeAsync(Guid scheduleId, TrainingAssessmentType assessmentType);

    /// <summary>Gets follow-up assessments awaiting manager observation submission.</summary>
    Task<IEnumerable<TrainingFollowUpAssessment>> GetPendingManagerObservationAsync();
}

#endregion

// ============================================================================
// CERTIFICATE INTERFACES
// ============================================================================

#region Training Certificate

public interface ITrainingCertificateRepository : IGenericRepository<TrainingCertificate>
{
    /// <summary>Gets a training certificate by its unique certificate number.</summary>
    Task<TrainingCertificate?> GetByCertificateNumberAsync(string certificateNumber);

    /// <summary>Gets all training certificates issued to an employee, including program details.</summary>
    Task<IEnumerable<TrainingCertificate>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets all certificates issued for a training program.</summary>
    Task<IEnumerable<TrainingCertificate>> GetByProgramIdAsync(Guid programId);

    /// <summary>Gets all certificates with the specified status.</summary>
    Task<IEnumerable<TrainingCertificate>> GetByStatusAsync(CertificateStatus status);

    /// <summary>Gets all currently active training certificates.</summary>
    Task<IEnumerable<TrainingCertificate>> GetActiveAsync();

    /// <summary>Gets all training certificates that have already expired.</summary>
    Task<IEnumerable<TrainingCertificate>> GetExpiredAsync();

    /// <summary>Gets training certificates expiring within the specified number of days.</summary>
    Task<IEnumerable<TrainingCertificate>> GetExpiringAsync(int daysAhead = 30);

    /// <summary>Gets renewal certificates issued to replace a previous certificate.</summary>
    Task<IEnumerable<TrainingCertificate>> GetRenewalsForCertificateAsync(Guid previousCertificateId);
}

#endregion

#region Employee Certificate

public interface IEmployeeCertificateRepository : IGenericRepository<EmployeeCertificate>
{
    /// <summary>Gets all externally-held certificates for an employee, including verifier details.</summary>
    Task<IEnumerable<EmployeeCertificate>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets employee certificates that have not yet been verified.</summary>
    Task<IEnumerable<EmployeeCertificate>> GetUnverifiedAsync();

    /// <summary>Gets employee certificates that have already expired.</summary>
    Task<IEnumerable<EmployeeCertificate>> GetExpiredAsync();

    /// <summary>Gets employee certificates expiring within the specified number of days.</summary>
    Task<IEnumerable<EmployeeCertificate>> GetExpiringAsync(int daysAhead = 30);

    /// <summary>Gets employee certificates with the specified status.</summary>
    Task<IEnumerable<EmployeeCertificate>> GetByStatusAsync(CertificateStatus status);
}

#endregion

// ============================================================================
// COMPLIANCE INTERFACES
// ============================================================================

#region Compliance Training Requirement

public interface IComplianceTrainingRequirementRepository : IGenericRepository<ComplianceTrainingRequirement>
{
    /// <summary>Gets a compliance requirement by its unique requirement code.</summary>
    Task<ComplianceTrainingRequirement?> GetByRequirementCodeAsync(string requirementCode);

    /// <summary>Gets all currently active compliance requirements (effective and not yet expired).</summary>
    Task<IEnumerable<ComplianceTrainingRequirement>> GetActiveAsync();

    /// <summary>Gets all compliance requirements linked to a specific training program.</summary>
    Task<IEnumerable<ComplianceTrainingRequirement>> GetByProgramIdAsync(Guid programId);

    /// <summary>Gets all compliance requirements applicable to a specific organization level.</summary>
    Task<IEnumerable<ComplianceTrainingRequirement>> GetByOrganizationLevelAsync(Guid orgLevelId);

    /// <summary>Gets all compliance requirements applicable to a specific organization unit.</summary>
    Task<IEnumerable<ComplianceTrainingRequirement>> GetByOrganizationUnitAsync(Guid orgUnitId);

    /// <summary>Gets all compliance requirements applicable to a specific position.</summary>
    Task<IEnumerable<ComplianceTrainingRequirement>> GetByPositionAsync(Guid positionId);

    /// <summary>Gets a compliance requirement with full details including program, org unit, position, and employee records.</summary>
    Task<ComplianceTrainingRequirement?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Employee Compliance Record

public interface IEmployeeComplianceRecordRepository : IGenericRepository<EmployeeComplianceRecord>
{
    /// <summary>Gets all compliance records for an employee, including requirement and program details.</summary>
    Task<IEnumerable<EmployeeComplianceRecord>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets all employee compliance records for a specific requirement.</summary>
    Task<IEnumerable<EmployeeComplianceRecord>> GetByRequirementIdAsync(Guid requirementId);

    /// <summary>Gets all compliance records with the specified compliance status.</summary>
    Task<IEnumerable<EmployeeComplianceRecord>> GetByStatusAsync(ComplianceStatus status);

    /// <summary>Gets all non-compliant, non-exempt compliance records.</summary>
    Task<IEnumerable<EmployeeComplianceRecord>> GetNonCompliantAsync();

    /// <summary>Gets compliance records that are overdue (past due date, not compliant, and not exempt).</summary>
    Task<IEnumerable<EmployeeComplianceRecord>> GetOverdueAsync();

    /// <summary>Gets compliance records whose due date falls within the specified number of days.</summary>
    Task<IEnumerable<EmployeeComplianceRecord>> GetExpiringAsync(int daysAhead = 30);

    /// <summary>Gets all exempt compliance records.</summary>
    Task<IEnumerable<EmployeeComplianceRecord>> GetExemptAsync();

    /// <summary>Gets the compliance record for a specific employee and requirement combination.</summary>
    Task<EmployeeComplianceRecord?> GetEmployeeRecordAsync(Guid employeeId, Guid requirementId);
}

#endregion

// ============================================================================
// BUDGET INTERFACES
// ============================================================================

#region Training Budget

// Every member takes the caller's tenant so it lands in the SQL predicate. The DI-created
// ApplicationDbContext carries no tenant, so its global tenant filter is inert and the inherited
// IGenericRepository members (GetByIdAsync, GetAllAsync, GetQueryable, ...) are cross-tenant.
public interface ITrainingBudgetRepository : IGenericRepository<TrainingBudget>
{
    /// <summary>Gets a training budget owned by the tenant, or null when it belongs to another tenant.</summary>
    Task<TrainingBudget?> GetForTenantAsync(Guid id, Guid tenantId);

    /// <summary>Gets a training budget by its budget code within the tenant.</summary>
    Task<TrainingBudget?> GetByBudgetCodeAsync(string budgetCode, Guid tenantId);

    /// <summary>Gets all training budgets for the tenant.</summary>
    Task<IEnumerable<TrainingBudget>> GetAllForTenantAsync(Guid tenantId);

    /// <summary>Gets all training budgets for a given fiscal year within the tenant.</summary>
    Task<IEnumerable<TrainingBudget>> GetByYearAsync(int year, Guid tenantId);

    /// <summary>Gets training budgets filtered by fiscal year and optional quarter within the tenant.</summary>
    Task<IEnumerable<TrainingBudget>> GetByYearAndQuarterAsync(int year, int? quarter, Guid tenantId);

    /// <summary>Gets all training budgets assigned to a specific organization unit within the tenant.</summary>
    Task<IEnumerable<TrainingBudget>> GetByOrganizationUnitAsync(Guid orgUnitId, Guid tenantId);

    /// <summary>Gets all training budgets with the specified status within the tenant.</summary>
    Task<IEnumerable<TrainingBudget>> GetByStatusAsync(TrainingBudgetStatus status, Guid tenantId);

    /// <summary>Gets all approved training budgets for the tenant.</summary>
    Task<IEnumerable<TrainingBudget>> GetApprovedAsync(Guid tenantId);

    /// <summary>Gets budgets where the spent amount has exceeded the allocated amount within the tenant.</summary>
    Task<IEnumerable<TrainingBudget>> GetWithExceededBudgetAsync(Guid tenantId);

    /// <summary>Gets a tenant-owned budget with full details including organization level, unit, approver, transactions, and schedules.</summary>
    Task<TrainingBudget?> GetWithFullDetailsAsync(Guid id, Guid tenantId);
}

#endregion

#region Training Budget Transaction

public interface ITrainingBudgetTransactionRepository : IGenericRepository<TrainingBudgetTransaction>
{
    /// <summary>Gets all transactions for a training budget within the tenant, ordered by transaction date descending.</summary>
    Task<IEnumerable<TrainingBudgetTransaction>> GetByBudgetIdAsync(Guid budgetId, Guid tenantId);

    /// <summary>Gets all budget transactions linked to a specific training schedule within the tenant.</summary>
    Task<IEnumerable<TrainingBudgetTransaction>> GetByScheduleIdAsync(Guid scheduleId, Guid tenantId);

    /// <summary>Gets transactions for a budget within a date range, scoped to the tenant.</summary>
    Task<IEnumerable<TrainingBudgetTransaction>> GetByDateRangeAsync(Guid budgetId, DateTime from, DateTime to, Guid tenantId);
}

#endregion

// ============================================================================
// TRAINING PLAN INTERFACES
// ============================================================================

#region Training Plan

public interface ITrainingPlanRepository : IGenericRepository<TrainingPlan>
{
    /// <summary>Gets a training plan by its unique plan number.</summary>
    Task<TrainingPlan?> GetByPlanNumberAsync(string planNumber);

    /// <summary>Gets all training plans for a given fiscal year.</summary>
    Task<IEnumerable<TrainingPlan>> GetByYearAsync(int year);

    /// <summary>Gets all training plans with the specified status.</summary>
    Task<IEnumerable<TrainingPlan>> GetByStatusAsync(TrainingPlanStatus status);

    /// <summary>Gets all training plans for a specific organization unit.</summary>
    Task<IEnumerable<TrainingPlan>> GetByOrganizationUnitAsync(Guid orgUnitId);

    /// <summary>Gets training plans awaiting approval.</summary>
    Task<IEnumerable<TrainingPlan>> GetPendingApprovalAsync();

    /// <summary>Gets a training plan with full details including org unit, approver, items, and budget lines.</summary>
    Task<TrainingPlan?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Training Plan Item

public interface ITrainingPlanItemRepository : IGenericRepository<TrainingPlanItem>
{
    /// <summary>Gets all items in a training plan, ordered by quarter then planned start date.</summary>
    Task<IEnumerable<TrainingPlanItem>> GetByPlanIdAsync(Guid planId);

    /// <summary>Gets all plan items linked to a specific training program.</summary>
    Task<IEnumerable<TrainingPlanItem>> GetByProgramIdAsync(Guid programId);

    /// <summary>Gets completed items in a training plan.</summary>
    Task<IEnumerable<TrainingPlanItem>> GetCompletedItemsAsync(Guid planId);

    /// <summary>Gets pending (not yet completed) items in a training plan.</summary>
    Task<IEnumerable<TrainingPlanItem>> GetPendingItemsAsync(Guid planId);

    /// <summary>Gets training plan items scheduled for a specific quarter.</summary>
    Task<IEnumerable<TrainingPlanItem>> GetByQuarterAsync(Guid planId, int quarter);
}

#endregion

#region Training Plan Budget Line

public interface ITrainingPlanBudgetLineRepository : IGenericRepository<TrainingPlanBudgetLine>
{
    /// <summary>Gets all budget lines for a training plan.</summary>
    Task<IEnumerable<TrainingPlanBudgetLine>> GetByPlanIdAsync(Guid planId);
}

#endregion

// ============================================================================
// TRAINING NEEDS ASSESSMENT INTERFACES
// ============================================================================

#region Training Needs Assessment

public interface ITrainingNeedsAssessmentRepository : IGenericRepository<TrainingNeedsAssessment>
{
    /// <summary>Gets all training needs assessments for an employee.</summary>
    Task<IEnumerable<TrainingNeedsAssessment>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets the training needs assessment for an employee for a specific year.</summary>
    Task<TrainingNeedsAssessment?> GetByEmployeeAndYearAsync(Guid employeeId, int year);

    /// <summary>Gets all training needs assessments conducted in a specific year.</summary>
    Task<IEnumerable<TrainingNeedsAssessment>> GetByYearAsync(int year);

    /// <summary>Gets assessments for which the recommended training has not yet been provided.</summary>
    Task<IEnumerable<TrainingNeedsAssessment>> GetUnfulfilledAsync();

    /// <summary>Gets assessments with the specified training priority.</summary>
    Task<IEnumerable<TrainingNeedsAssessment>> GetByPriorityAsync(TrainingPriority priority);

    /// <summary>Gets an assessment with full details including employee, identifier, recommended programs, and skill gaps.</summary>
    Task<TrainingNeedsAssessment?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Training Needs Assessment Program

public interface ITrainingNeedsAssessmentProgramRepository : IGenericRepository<TrainingNeedsAssessmentProgram>
{
    /// <summary>Gets all program recommendations for a training needs assessment, including program details.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentProgram>> GetByAssessmentIdAsync(Guid assessmentId);

    /// <summary>Gets all assessment-program links for a program, showing how often it is recommended.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentProgram>> GetByProgramIdAsync(Guid programId);
}

#endregion

#region Training Needs Assessment Skill

public interface ITrainingNeedsAssessmentSkillRepository : IGenericRepository<TrainingNeedsAssessmentSkill>
{
    /// <summary>Gets all skill gaps identified in a training needs assessment, including skill details.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentSkill>> GetByAssessmentIdAsync(Guid assessmentId);

    /// <summary>Gets all assessment-skill links for a skill, showing how frequently it appears as a gap.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentSkill>> GetBySkillIdAsync(Guid skillId);
}

#endregion

// ============================================================================
// WAITLIST & REQUEST INTERFACES
// ============================================================================

#region Training Waitlist

public interface ITrainingWaitlistRepository : IGenericRepository<TrainingWaitlist>
{
    /// <summary>Gets all waitlist entries for a schedule, ordered by queue position, including employee details.</summary>
    Task<IEnumerable<TrainingWaitlist>> GetByScheduleIdAsync(Guid scheduleId);

    /// <summary>Gets all waitlist entries for an employee across all schedules.</summary>
    Task<IEnumerable<TrainingWaitlist>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets active waitlist entries for a schedule.</summary>
    Task<IEnumerable<TrainingWaitlist>> GetActiveWaitlistAsync(Guid scheduleId);

    /// <summary>Gets the next candidate in the waitlist queue for a schedule (lowest position, active status).</summary>
    Task<TrainingWaitlist?> GetNextInQueueAsync(Guid scheduleId);

    /// <summary>Gets waitlist entries with the specified status for a schedule.</summary>
    Task<IEnumerable<TrainingWaitlist>> GetByStatusAsync(Guid scheduleId, TrainingWaitlistStatus status);

    /// <summary>Gets waitlist entries for a schedule where a slot offer has been made and is still valid.</summary>
    Task<IEnumerable<TrainingWaitlist>> GetOfferedAsync(Guid scheduleId);

    /// <summary>Gets waitlist entries where an offer was made but the offer expiry has passed.</summary>
    Task<IEnumerable<TrainingWaitlist>> GetExpiredOffersAsync();
}

#endregion

#region Training Request

public interface ITrainingRequestRepository : IGenericRepository<TrainingRequest>
{
    /// <summary>Gets a training request by its unique request number.</summary>
    Task<TrainingRequest?> GetByRequestNumberAsync(string requestNumber);

    /// <summary>Gets all training requests submitted by an employee.</summary>
    Task<IEnumerable<TrainingRequest>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets all training requests with the specified status.</summary>
    Task<IEnumerable<TrainingRequest>> GetByStatusAsync(TrainingRequestStatus status);

    /// <summary>Gets training requests awaiting approval.</summary>
    Task<IEnumerable<TrainingRequest>> GetPendingApprovalAsync();

    /// <summary>Gets training requests that have been linked to a specific training program.</summary>
    Task<IEnumerable<TrainingRequest>> GetLinkedToProgramAsync(Guid programId);
}

#endregion

// ============================================================================
// LEARNING PATH INTERFACES
// ============================================================================

#region Learning Path

public interface ILearningPathRepository : IGenericRepository<LearningPath>
{
    /// <summary>Gets all learning paths with the specified status.</summary>
    Task<IEnumerable<LearningPath>> GetByStatusAsync(LearningPathStatus status);

    /// <summary>Gets all active learning paths.</summary>
    Task<IEnumerable<LearningPath>> GetActiveAsync();

    /// <summary>Gets all learning paths applicable to a specific organization level.</summary>
    Task<IEnumerable<LearningPath>> GetByOrganizationLevelAsync(Guid orgLevelId);

    /// <summary>Gets all learning paths applicable to a specific organization unit.</summary>
    Task<IEnumerable<LearningPath>> GetByOrganizationUnitAsync(Guid orgUnitId);

    /// <summary>Gets all learning paths applicable to a specific position.</summary>
    Task<IEnumerable<LearningPath>> GetByPositionAsync(Guid positionId);

    /// <summary>Gets learning paths that award a certificate upon completion.</summary>
    Task<IEnumerable<LearningPath>> GetWithCertificateAsync();

    /// <summary>Gets a learning path with full details including programs, target skills, and enrollments.</summary>
    Task<LearningPath?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Learning Path Program

public interface ILearningPathProgramRepository : IGenericRepository<LearningPathProgram>
{
    /// <summary>Gets all programs in a learning path, ordered by sequence, including program details.</summary>
    Task<IEnumerable<LearningPathProgram>> GetByLearningPathIdAsync(Guid learningPathId);

    /// <summary>Gets all learning-path entries for a program, showing which paths include it.</summary>
    Task<IEnumerable<LearningPathProgram>> GetByProgramIdAsync(Guid programId);
}

#endregion

#region Learning Path Skill

public interface ILearningPathSkillRepository : IGenericRepository<LearningPathSkill>
{
    /// <summary>Gets all target skills for a learning path, including skill details.</summary>
    Task<IEnumerable<LearningPathSkill>> GetByLearningPathIdAsync(Guid learningPathId);

    /// <summary>Gets all learning-path-skill links for a skill, showing which paths target it.</summary>
    Task<IEnumerable<LearningPathSkill>> GetBySkillIdAsync(Guid skillId);
}

#endregion

#region Employee Learning Path

public interface IEmployeeLearningPathRepository : IGenericRepository<EmployeeLearningPath>
{
    /// <summary>Gets all learning path enrollments for an employee, including learning path details.</summary>
    Task<IEnumerable<EmployeeLearningPath>> GetByEmployeeIdAsync(Guid employeeId);

    /// <summary>Gets all employee enrollments for a specific learning path.</summary>
    Task<IEnumerable<EmployeeLearningPath>> GetByLearningPathIdAsync(Guid learningPathId);

    /// <summary>Gets active (not yet completed) learning path enrollments for an employee.</summary>
    Task<IEnumerable<EmployeeLearningPath>> GetActiveEnrollmentsAsync(Guid employeeId);

    /// <summary>Gets completed learning path enrollments for an employee.</summary>
    Task<IEnumerable<EmployeeLearningPath>> GetCompletedAsync(Guid employeeId);

    /// <summary>Gets the enrollment record for a specific employee and learning path.</summary>
    Task<EmployeeLearningPath?> GetEnrollmentAsync(Guid employeeId, Guid learningPathId);

    /// <summary>Gets enrollments that are overdue (past target completion date and not yet completed).</summary>
    Task<IEnumerable<EmployeeLearningPath>> GetOverdueAsync();

    /// <summary>Gets an enrollment with full details including employee, learning path, steps, and nominations.</summary>
    Task<EmployeeLearningPath?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Gets all enrollments with Employee (including OrgUnit &amp; Position), LearningPath, and AssignedBy loaded.</summary>
    Task<IEnumerable<EmployeeLearningPath>> GetAllWithDetailsAsync();
}

#endregion

#region Employee Learning Path Step

public interface IEmployeeLearningPathStepRepository : IGenericRepository<EmployeeLearningPathStep>
{
    /// <summary>Gets all steps for a learning path enrollment, ordered by sequence.</summary>
    Task<IEnumerable<EmployeeLearningPathStep>> GetByEmployeeLearningPathIdAsync(Guid enrollmentId);

    /// <summary>Gets incomplete steps for a learning path enrollment.</summary>
    Task<IEnumerable<EmployeeLearningPathStep>> GetIncompleteStepsAsync(Guid enrollmentId);

    /// <summary>Gets completed steps for a learning path enrollment.</summary>
    Task<IEnumerable<EmployeeLearningPathStep>> GetCompletedStepsAsync(Guid enrollmentId);

    /// <summary>Gets a step with full navigation: program + materials, prerequisite, enrollment + sibling steps, nomination + completion.</summary>
    Task<EmployeeLearningPathStep?> GetStepWithContextAsync(Guid stepId);
}

#endregion

// ============================================================================
// MENTORING INTERFACES
// ============================================================================

#region Mentoring Program

public interface IMentoringProgramRepository : IGenericRepository<MentoringProgram>
{
    /// <summary>Gets all active mentoring programs.</summary>
    Task<IEnumerable<MentoringProgram>> GetActiveAsync();

    /// <summary>Gets all mentoring programs coordinated by a specific employee.</summary>
    Task<IEnumerable<MentoringProgram>> GetByCoordinatorAsync(Guid employeeId);

    /// <summary>Gets a mentoring program with full details including coordinator and mentoring pairs.</summary>
    Task<MentoringProgram?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Mentoring Pair

public interface IMentoringPairRepository : IGenericRepository<MentoringPair>
{
    /// <summary>Gets all mentoring pairs in a program, including mentor and mentee details.</summary>
    Task<IEnumerable<MentoringPair>> GetByProgramIdAsync(Guid programId);

    /// <summary>Gets all mentoring pairs where the specified employee is the mentor.</summary>
    Task<IEnumerable<MentoringPair>> GetByMentorIdAsync(Guid mentorId);

    /// <summary>Gets all mentoring pairs where the specified employee is the mentee.</summary>
    Task<IEnumerable<MentoringPair>> GetByMenteeIdAsync(Guid menteeId);

    /// <summary>Gets all mentoring pairs with the specified status.</summary>
    Task<IEnumerable<MentoringPair>> GetByStatusAsync(MentoringStatus status);

    /// <summary>Gets all active mentoring pairs.</summary>
    Task<IEnumerable<MentoringPair>> GetActiveAsync();

    /// <summary>Gets the active mentoring pair (if any) between a specific mentor and mentee.</summary>
    Task<MentoringPair?> GetActivePairAsync(Guid mentorId, Guid menteeId);

    /// <summary>Gets a mentoring pair with full details including program, mentor, mentee, and sessions.</summary>
    Task<MentoringPair?> GetWithFullDetailsAsync(Guid id);
}

#endregion

#region Mentoring Session

public interface IMentoringSessionRepository : IGenericRepository<MentoringSession>
{
    /// <summary>Gets all sessions for a mentoring pair, ordered by session date descending.</summary>
    Task<IEnumerable<MentoringSession>> GetByPairIdAsync(Guid pairId);

    /// <summary>Gets sessions for a mentoring pair within a date range.</summary>
    Task<IEnumerable<MentoringSession>> GetByDateRangeAsync(Guid pairId, DateTime from, DateTime to);

    /// <summary>Gets sessions where the mentor or mentee did not attend.</summary>
    Task<IEnumerable<MentoringSession>> GetMissedSessionsAsync(Guid pairId);
}

#endregion
