using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// TRAINING VENDOR SERVICE
// ============================================================================

#region Training Vendor Service

public interface ITrainingVendorService
{
    // Queries
    /// <summary>Gets full vendor details by ID, including trainer list.</summary>
    Task<TrainingVendorDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets a vendor by its unique vendor code.</summary>
    Task<TrainingVendorDto?> GetByVendorCodeAsync(string vendorCode, CancellationToken cancellationToken = default);
    /// <summary>Gets summary list of all vendors.</summary>
    Task<IEnumerable<TrainingVendorSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets a paged summary list of all vendors.</summary>
    Task<PagedResult<TrainingVendorSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Gets all active, non-blacklisted vendors.</summary>
    Task<IEnumerable<TrainingVendorSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all preferred vendors.</summary>
    Task<IEnumerable<TrainingVendorSummaryDto>> GetPreferredAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all blacklisted vendors.</summary>
    Task<IEnumerable<TrainingVendorSummaryDto>> GetBlacklistedAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets vendors whose accreditation expires within the specified number of days.</summary>
    Task<IEnumerable<TrainingVendorSummaryDto>> GetWithExpiringAccreditationAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    /// <summary>Gets vendors by vendor type.</summary>
    Task<IEnumerable<TrainingVendorSummaryDto>> GetByVendorTypeAsync(TrainingVendorType vendorType, CancellationToken cancellationToken = default);

    // CRUD
    /// <summary>Creates a new training vendor.</summary>
    Task<TrainingVendorDto> CreateAsync(CreateTrainingVendorDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates an existing training vendor.</summary>
    Task<TrainingVendorDto> UpdateAsync(UpdateTrainingVendorDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a training vendor.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>Blacklists a vendor, preventing future use.</summary>
    Task<bool> BlacklistVendorAsync(BlacklistVendorDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Removes a vendor from the blacklist, restoring active status.</summary>
    Task<bool> UnblacklistVendorAsync(Guid vendorId, Guid updatedByUserId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINER SERVICE
// ============================================================================

#region Trainer Service

public interface ITrainerService
{
    // Trainer profile queries
    /// <summary>Gets full trainer profile details by ID.</summary>
    Task<TrainerProfileDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets all trainer profile summaries.</summary>
    Task<IEnumerable<TrainerProfileSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all active trainer profile summaries.</summary>
    Task<IEnumerable<TrainerProfileSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets trainer profiles for a vendor.</summary>
    Task<IEnumerable<TrainerProfileSummaryDto>> GetByVendorIdAsync(Guid vendorId, CancellationToken cancellationToken = default);
    /// <summary>Gets trainers available for a given date range.</summary>
    Task<IEnumerable<TrainerProfileSummaryDto>> GetAvailableForDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    // Trainer profile CRUD
    /// <summary>Creates a new trainer profile.</summary>
    Task<TrainerProfileDto> CreateAsync(CreateTrainerProfileDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates a trainer profile.</summary>
    Task<TrainerProfileDto> UpdateAsync(UpdateTrainerProfileDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a trainer profile.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Trainer skill sub-operations
    /// <summary>Adds a skill to a trainer profile.</summary>
    Task<TrainerSkillDto> AddSkillAsync(CreateTrainerSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all skills registered for a trainer profile.</summary>
    Task<IEnumerable<TrainerSkillDto>> GetSkillsAsync(Guid trainerProfileId, CancellationToken cancellationToken = default);
    /// <summary>Updates a trainer skill record.</summary>
    Task<TrainerSkillDto> UpdateSkillAsync(UpdateTrainerSkillDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Removes a skill from a trainer profile.</summary>
    Task<bool> DeleteSkillAsync(Guid trainerSkillId, CancellationToken cancellationToken = default);

    // Trainer availability sub-operations
    /// <summary>Adds an availability window for a trainer.</summary>
    Task<TrainerAvailabilityDto> AddAvailabilityAsync(CreateTrainerAvailabilityDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all availability windows for a trainer.</summary>
    Task<IEnumerable<TrainerAvailabilityDto>> GetAvailabilityAsync(Guid trainerProfileId, CancellationToken cancellationToken = default);
    /// <summary>Updates a trainer availability record.</summary>
    Task<TrainerAvailabilityDto> UpdateAvailabilityAsync(UpdateTrainerAvailabilityDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a trainer availability record.</summary>
    Task<bool> DeleteAvailabilityAsync(Guid availabilityId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING PROGRAM SERVICE
// ============================================================================

#region Training Program Service

public interface ITrainingProgramService
{
    // Program queries
    /// <summary>Gets full training program details by ID, including competencies, skills, and materials.</summary>
    Task<TrainingProgramDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets a training program by its unique code.</summary>
    Task<TrainingProgramDto?> GetByProgramCodeAsync(string programCode, CancellationToken cancellationToken = default);
    /// <summary>Gets summary list of all training programs.</summary>
    Task<IEnumerable<TrainingProgramSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets a paged summary list of all training programs.</summary>
    Task<PagedResult<TrainingProgramSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Gets all active training programs.</summary>
    Task<IEnumerable<TrainingProgramSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets training programs by category.</summary>
    Task<IEnumerable<TrainingProgramSummaryDto>> GetByCategoryAsync(Guid categoryOptionId, CancellationToken cancellationToken = default);
    /// <summary>Gets training programs by type.</summary>
    Task<IEnumerable<TrainingProgramSummaryDto>> GetByTypeAsync(TrainingType type, CancellationToken cancellationToken = default);
    /// <summary>Gets training programs that award a certificate.</summary>
    Task<IEnumerable<TrainingProgramSummaryDto>> GetWithCertificateAsync(CancellationToken cancellationToken = default);

    // Program CRUD
    /// <summary>Creates a new training program.</summary>
    Task<TrainingProgramDto> CreateAsync(CreateTrainingProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates an existing training program.</summary>
    Task<TrainingProgramDto> UpdateAsync(UpdateTrainingProgramDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a training program (only if it has no associated schedules).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Material sub-operations
    /// <summary>Adds a material to a training program.</summary>
    Task<TrainingMaterialDto> AddMaterialAsync(CreateTrainingMaterialDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all materials for a training program.</summary>
    Task<IEnumerable<TrainingMaterialDto>> GetMaterialsAsync(Guid programId, CancellationToken cancellationToken = default);
    /// <summary>Updates a training material record.</summary>
    Task<TrainingMaterialDto> UpdateMaterialAsync(UpdateTrainingMaterialDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a training material.</summary>
    Task<bool> DeleteMaterialAsync(Guid materialId, CancellationToken cancellationToken = default);

    // Competency sub-operations
    /// <summary>Links a competency to a training program.</summary>
    Task<TrainingProgramCompetencyDto> AddCompetencyAsync(CreateTrainingProgramCompetencyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all competencies linked to a training program.</summary>
    Task<IEnumerable<TrainingProgramCompetencyDto>> GetCompetenciesAsync(Guid programId, CancellationToken cancellationToken = default);
    /// <summary>Removes a competency link from a training program.</summary>
    Task<bool> DeleteCompetencyAsync(Guid programCompetencyId, CancellationToken cancellationToken = default);

    // Skill sub-operations
    /// <summary>Links a skill to a training program.</summary>
    Task<TrainingProgramSkillDto> AddSkillAsync(CreateTrainingProgramSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all skills linked to a training program.</summary>
    Task<IEnumerable<TrainingProgramSkillDto>> GetSkillsAsync(Guid programId, CancellationToken cancellationToken = default);
    /// <summary>Removes a skill link from a training program.</summary>
    Task<bool> DeleteSkillAsync(Guid programSkillId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING SCHEDULE SERVICE
// ============================================================================

#region Training Schedule Service

public interface ITrainingScheduleService
{
    // Queries
    /// <summary>Gets full schedule details by ID, including sessions, nominations, and attendance.</summary>
    Task<TrainingScheduleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Checks a trainer for schedule conflicts and blocked availability over a date range.</summary>
    Task<TrainerAvailabilityCheckDto> CheckTrainerAvailabilityAsync(Guid trainerProfileId, DateTime from, DateTime to, Guid? excludeScheduleId, CancellationToken cancellationToken = default);
    /// <summary>Gets a schedule by its unique schedule number.</summary>
    Task<TrainingScheduleDto?> GetByScheduleNumberAsync(string scheduleNumber, CancellationToken cancellationToken = default);
    /// <summary>Gets summary list of all schedules.</summary>
    Task<IEnumerable<TrainingScheduleSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets a paged summary list of all schedules.</summary>
    Task<PagedResult<TrainingScheduleSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Gets schedules for a training program.</summary>
    Task<IEnumerable<TrainingScheduleSummaryDto>> GetByProgramIdAsync(Guid programId, CancellationToken cancellationToken = default);
    /// <summary>Gets schedules assigned to a specific trainer profile.</summary>
    Task<IEnumerable<TrainingScheduleSummaryDto>> GetByTrainerProfileIdAsync(Guid trainerProfileId, CancellationToken cancellationToken = default);
    /// <summary>Gets schedules with the specified status.</summary>
    Task<IEnumerable<TrainingScheduleSummaryDto>> GetByStatusAsync(ScheduleStatus status, CancellationToken cancellationToken = default);
    /// <summary>Gets upcoming schedules starting within the specified number of days.</summary>
    Task<IEnumerable<TrainingScheduleSummaryDto>> GetUpcomingAsync(int daysAhead = 90, CancellationToken cancellationToken = default);
    /// <summary>Gets schedules that are currently open for registration and have available slots.</summary>
    Task<IEnumerable<TrainingScheduleSummaryDto>> GetOpenForRegistrationAsync(CancellationToken cancellationToken = default);

    // CRUD
    /// <summary>Creates a new training schedule. Auto-generates the schedule number.</summary>
    Task<TrainingScheduleDto> CreateAsync(CreateTrainingScheduleDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates an existing training schedule.</summary>
    Task<TrainingScheduleDto> UpdateAsync(UpdateTrainingScheduleDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a training schedule (only if it has no confirmed nominations).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>Approves a training schedule, making it visible for nomination.</summary>
    Task<bool> ApproveAsync(ApproveTrainingScheduleDto dto, CancellationToken cancellationToken = default);
    /// <summary>Cancels a training schedule with a stated reason.</summary>
    Task<bool> CancelAsync(CancelTrainingScheduleDto dto, CancellationToken cancellationToken = default);
    /// <summary>Marks a training schedule as completed.</summary>
    Task<bool> CompleteAsync(CompleteTrainingScheduleDto dto, CancellationToken cancellationToken = default);

    // Session sub-operations
    /// <summary>Adds a session to a training schedule.</summary>
    Task<TrainingSessionDto> AddSessionAsync(CreateTrainingSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all sessions for a training schedule.</summary>
    Task<IEnumerable<TrainingSessionDto>> GetSessionsAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    /// <summary>Updates a training session.</summary>
    Task<TrainingSessionDto> UpdateSessionAsync(UpdateTrainingSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a training session.</summary>
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING NOMINATION SERVICE
// ============================================================================

#region Training Nomination Service

public interface ITrainingNominationService
{
    // Queries
    /// <summary>Gets full nomination details by ID.</summary>
    Task<TrainingNominationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets a nomination by its unique nomination number.</summary>
    Task<TrainingNominationDto?> GetByNominationNumberAsync(string nominationNumber, CancellationToken cancellationToken = default);
    /// <summary>Gets paged summary list of all nominations.</summary>
    Task<PagedResult<TrainingNominationSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Gets all nominations for a training schedule.</summary>
    Task<IEnumerable<TrainingNominationSummaryDto>> GetByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    /// <summary>Gets all nominations for an employee.</summary>
    Task<IEnumerable<TrainingNominationSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets nominations by status.</summary>
    Task<IEnumerable<TrainingNominationSummaryDto>> GetByStatusAsync(NominationStatus status, CancellationToken cancellationToken = default);
    /// <summary>Gets nominations awaiting supervisor approval.</summary>
    Task<IEnumerable<TrainingNominationSummaryDto>> GetPendingSupervisorApprovalAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets nominations awaiting HR approval.</summary>
    Task<IEnumerable<TrainingNominationSummaryDto>> GetPendingHrApprovalAsync(CancellationToken cancellationToken = default);

    // CRUD
    /// <summary>Creates a new training nomination. Auto-generates the nomination number.</summary>
    Task<TrainingNominationDto> CreateAsync(CreateTrainingNominationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Nominates multiple employees to one schedule at once, skipping duplicates.</summary>
    Task<BulkNominationResultDto> BulkCreateAsync(BulkCreateTrainingNominationDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates editable fields of an existing nomination.</summary>
    Task<TrainingNominationDto> UpdateAsync(Guid id, UpdateTrainingNominationDto dto, CancellationToken cancellationToken = default);
    /// <summary>Promotes a Draft nomination to Submitted, entering the approval workflow.</summary>
    Task<TrainingNominationDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Deletes a draft or submitted nomination.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>Approves a nomination at the specified role level ("Supervisor" or "HR").</summary>
    Task<bool> ApproveAsync(ApproveNominationDto dto, CancellationToken cancellationToken = default);
    /// <summary>Rejects a nomination with a reason.</summary>
    Task<bool> RejectAsync(RejectNominationDto dto, CancellationToken cancellationToken = default);
    /// <summary>Withdraws a confirmed or approved nomination.</summary>
    Task<bool> WithdrawAsync(WithdrawNominationDto dto, CancellationToken cancellationToken = default);

    // Attendance sub-operations
    /// <summary>Records attendance for an employee on a specific date.</summary>
    Task<TrainingAttendanceDto> MarkAttendanceAsync(MarkAttendanceDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Bulk-marks attendance for all nominated employees on a schedule and date.</summary>
    Task<IEnumerable<TrainingAttendanceDto>> BulkMarkAttendanceAsync(BulkMarkAttendanceDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets attendance records for a schedule.</summary>
    Task<IEnumerable<TrainingAttendanceDto>> GetAttendanceForScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    /// <summary>Gets attendance records for a schedule on a specific date.</summary>
    Task<IEnumerable<TrainingAttendanceDto>> GetAttendanceByDateAsync(Guid scheduleId, DateTime date, CancellationToken cancellationToken = default);

    // Feedback sub-operations
    /// <summary>Submits training feedback from an employee.</summary>
    Task<TrainingFeedbackDto> SubmitFeedbackAsync(SubmitTrainingFeedbackDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all feedback submitted for a schedule.</summary>
    Task<IEnumerable<TrainingFeedbackDto>> GetFeedbackForScheduleAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    // Follow-up assessment sub-operations
    /// <summary>Submits a post-training follow-up assessment.</summary>
    Task<TrainingFollowUpAssessmentDto> SubmitFollowUpAssessmentAsync(SubmitFollowUpAssessmentDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Records manager observation notes on a follow-up assessment.</summary>
    Task<TrainingFollowUpAssessmentDto> SubmitManagerObservationAsync(SubmitManagerObservationDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all follow-up assessments for a schedule.</summary>
    Task<IEnumerable<TrainingFollowUpAssessmentDto>> GetFollowUpAssessmentsAsync(Guid scheduleId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING COMPLETION SERVICE
// ============================================================================

#region Training Completion Service

public interface ITrainingCompletionService
{
    // Queries
    /// <summary>Gets a training completion record by ID.</summary>
    Task<TrainingCompletionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets the completion record for a specific nomination.</summary>
    Task<TrainingCompletionDto?> GetByNominationIdAsync(Guid nominationId, CancellationToken cancellationToken = default);
    /// <summary>Gets all completion records for an employee.</summary>
    Task<IEnumerable<TrainingCompletionDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets all completion records for a schedule.</summary>
    Task<IEnumerable<TrainingCompletionDto>> GetByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    /// <summary>Gets completion records pending manager verification.</summary>
    Task<IEnumerable<TrainingCompletionDto>> GetPendingVerificationAsync(CancellationToken cancellationToken = default);

    // Completion recording
    /// <summary>Records a training completion result for a nomination.</summary>
    Task<TrainingCompletionDto> RecordCompletionAsync(RecordTrainingCompletionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Records completion for multiple participants of one schedule at once, skipping any already recorded.</summary>
    Task<BulkCompletionResultDto> BulkRecordCompletionAsync(BulkRecordCompletionDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates scores, status, or result on an existing completion record.</summary>
    Task<TrainingCompletionDto> UpdateAsync(UpdateTrainingCompletionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Verifies a training completion on behalf of a manager.</summary>
    Task<TrainingCompletionDto> VerifyCompletionAsync(VerifyTrainingCompletionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Certificate sub-operations (training certificates issued from completions)
    /// <summary>Issues a training certificate for a completed nomination.</summary>
    Task<TrainingCertificateDto> IssueCertificateAsync(IssueCertificateDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Revokes an issued training certificate.</summary>
    Task<bool> RevokeCertificateAsync(RevokeCertificateDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all training certificates for an employee.</summary>
    Task<IEnumerable<TrainingCertificateSummaryDto>> GetCertificatesForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets training certificates expiring within the specified number of days.</summary>
    Task<IEnumerable<TrainingCertificateSummaryDto>> GetExpiringCertificatesAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// EMPLOYEE CERTIFICATE SERVICE
// ============================================================================

#region Employee Certificate Service

public interface IEmployeeCertificateService
{
    // Queries
    /// <summary>Gets an employee certificate record by ID.</summary>
    Task<EmployeeCertificateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets all certificates held by an employee.</summary>
    Task<IEnumerable<EmployeeCertificateDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets certificates that have not yet been verified.</summary>
    Task<IEnumerable<EmployeeCertificateSummaryDto>> GetUnverifiedAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets employee certificates expiring within the specified number of days.</summary>
    Task<IEnumerable<EmployeeCertificateSummaryDto>> GetExpiringAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    // CRUD
    /// <summary>Adds an externally-held certificate to an employee's record.</summary>
    Task<EmployeeCertificateDto> CreateAsync(CreateEmployeeCertificateDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates certificate details (name, issuing body, dates, etc.).</summary>
    Task<EmployeeCertificateDto> UpdateAsync(UpdateEmployeeCertificateDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes an employee certificate record.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>Marks an employee certificate as verified by HR.</summary>
    Task<EmployeeCertificateDto> VerifyAsync(VerifyEmployeeCertificateDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// COMPLIANCE TRAINING SERVICE
// ============================================================================

#region Compliance Training Service

public interface IComplianceTrainingService
{
    // Requirement queries
    /// <summary>Gets full compliance requirement details by ID.</summary>
    Task<ComplianceTrainingRequirementDto> GetRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets all compliance requirement summaries.</summary>
    Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetAllRequirementsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all active compliance requirements.</summary>
    Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetActiveRequirementsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets requirements applicable to a specific program.</summary>
    Task<IEnumerable<ComplianceTrainingRequirementSummaryDto>> GetRequirementsByProgramAsync(Guid programId, CancellationToken cancellationToken = default);

    // Requirement CRUD
    /// <summary>Creates a new compliance training requirement.</summary>
    Task<ComplianceTrainingRequirementDto> CreateRequirementAsync(CreateComplianceTrainingRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates a compliance training requirement.</summary>
    Task<ComplianceTrainingRequirementDto> UpdateRequirementAsync(UpdateComplianceTrainingRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a compliance training requirement.</summary>
    Task<bool> DeleteRequirementAsync(Guid id, CancellationToken cancellationToken = default);

    // Employee compliance record queries
    /// <summary>Gets all compliance records for an employee.</summary>
    Task<IEnumerable<EmployeeComplianceRecordDto>> GetRecordsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets all compliance records for a requirement.</summary>
    Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetRecordsForRequirementAsync(Guid requirementId, CancellationToken cancellationToken = default);
    /// <summary>Gets all overdue compliance records.</summary>
    Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetOverdueRecordsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all non-compliant records.</summary>
    Task<IEnumerable<EmployeeComplianceRecordSummaryDto>> GetNonCompliantRecordsAsync(CancellationToken cancellationToken = default);

    // Employee compliance record workflow
    /// <summary>Assigns a compliance requirement to an employee, creating a tracking record.</summary>
    Task<EmployeeComplianceRecordDto> AssignRequirementToEmployeeAsync(Guid employeeId, Guid requirementId, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Grants an exemption to an employee for a compliance requirement.</summary>
    Task<EmployeeComplianceRecordDto> ExemptEmployeeAsync(ExemptEmployeeComplianceDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Marks a compliance record as fulfilled by nominating the employee in the relevant program.</summary>
    Task<EmployeeComplianceRecordDto> MarkFulfilledAsync(Guid recordId, Guid nominationId, Guid updatedByUserId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING BUDGET SERVICE
// ============================================================================

#region Training Budget Service

public interface ITrainingBudgetService
{
    // Budget queries
    /// <summary>Gets full budget details by ID, including transactions and schedules.</summary>
    Task<TrainingBudgetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets a budget by its unique code.</summary>
    Task<TrainingBudgetDto?> GetByBudgetCodeAsync(string budgetCode, CancellationToken cancellationToken = default);
    /// <summary>Gets all budget summaries.</summary>
    Task<IEnumerable<TrainingBudgetSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets budgets for a fiscal year.</summary>
    Task<IEnumerable<TrainingBudgetSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);
    /// <summary>Gets budgets for a fiscal year and optional quarter.</summary>
    Task<IEnumerable<TrainingBudgetSummaryDto>> GetByYearAndQuarterAsync(int year, int? quarter, CancellationToken cancellationToken = default);
    /// <summary>Gets approved budgets.</summary>
    Task<IEnumerable<TrainingBudgetSummaryDto>> GetApprovedAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets budgets whose spending has exceeded the allocated amount.</summary>
    Task<IEnumerable<TrainingBudgetSummaryDto>> GetOverBudgetAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets budgets for a specific organization unit.</summary>
    Task<IEnumerable<TrainingBudgetSummaryDto>> GetByOrganizationUnitIdAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);

    // Budget CRUD
    /// <summary>Creates a new training budget. Auto-generates the budget code.</summary>
    Task<TrainingBudgetDto> CreateAsync(CreateTrainingBudgetDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates an existing training budget.</summary>
    Task<TrainingBudgetDto> UpdateAsync(UpdateTrainingBudgetDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a draft training budget.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>Approves a training budget, making it available for schedule allocation.</summary>
    Task<bool> ApproveAsync(ApproveTrainingBudgetDto dto, CancellationToken cancellationToken = default);

    // Transaction sub-operations
    /// <summary>Records a debit or credit transaction against a training budget.</summary>
    Task<TrainingBudgetTransactionDto> RecordTransactionAsync(CreateTrainingBudgetTransactionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all transactions for a budget.</summary>
    Task<IEnumerable<TrainingBudgetTransactionDto>> GetTransactionsAsync(Guid budgetId, CancellationToken cancellationToken = default);
    /// <summary>Gets transactions for a budget within a date range.</summary>
    Task<IEnumerable<TrainingBudgetTransactionDto>> GetTransactionsByDateRangeAsync(Guid budgetId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING PLAN SERVICE
// ============================================================================

#region Training Plan Service

public interface ITrainingPlanService
{
    // Plan queries
    /// <summary>Gets full training plan details by ID, including items and budget lines.</summary>
    Task<TrainingPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets a training plan by its unique plan number.</summary>
    Task<TrainingPlanDto?> GetByPlanNumberAsync(string planNumber, CancellationToken cancellationToken = default);
    /// <summary>Gets all training plan summaries.</summary>
    Task<IEnumerable<TrainingPlanSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets training plans for a given year.</summary>
    Task<IEnumerable<TrainingPlanSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);
    /// <summary>Gets training plans with the specified status.</summary>
    Task<IEnumerable<TrainingPlanSummaryDto>> GetByStatusAsync(TrainingPlanStatus status, CancellationToken cancellationToken = default);

    // Plan CRUD
    /// <summary>Creates a new training plan. Auto-generates the plan number.</summary>
    Task<TrainingPlanDto> CreateAsync(CreateTrainingPlanDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates an existing training plan.</summary>
    Task<TrainingPlanDto> UpdateAsync(UpdateTrainingPlanDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a draft training plan.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>Approves a training plan.</summary>
    Task<bool> ApproveAsync(ApproveTrainingPlanDto dto, CancellationToken cancellationToken = default);
    /// <summary>Submits a training plan for approval.</summary>
    Task<bool> SubmitForApprovalAsync(Guid planId, Guid submittedByUserId, CancellationToken cancellationToken = default);

    // Plan item sub-operations
    /// <summary>Adds an item to a training plan.</summary>
    Task<TrainingPlanItemDto> AddItemAsync(CreateTrainingPlanItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all items in a training plan.</summary>
    Task<IEnumerable<TrainingPlanItemDto>> GetItemsAsync(Guid planId, CancellationToken cancellationToken = default);
    /// <summary>Updates a training plan item.</summary>
    Task<TrainingPlanItemDto> UpdateItemAsync(UpdateTrainingPlanItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a training plan item.</summary>
    Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default);

    // Budget line sub-operations
    /// <summary>Adds a budget line to a training plan.</summary>
    Task<TrainingPlanBudgetLineDto> AddBudgetLineAsync(CreateTrainingPlanBudgetLineDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all budget lines for a training plan.</summary>
    Task<IEnumerable<TrainingPlanBudgetLineDto>> GetBudgetLinesAsync(Guid planId, CancellationToken cancellationToken = default);
    /// <summary>Updates a training plan budget line.</summary>
    Task<TrainingPlanBudgetLineDto> UpdateBudgetLineAsync(UpdateTrainingPlanBudgetLineDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a training plan budget line.</summary>
    Task<bool> DeleteBudgetLineAsync(Guid budgetLineId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING NEEDS ASSESSMENT SERVICE
// ============================================================================

#region Training Needs Assessment Service

public interface ITrainingNeedsAssessmentService
{
    // Assessment queries
    /// <summary>Gets full TNA details by ID, including recommended programs and skill gaps.</summary>
    Task<TrainingNeedsAssessmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets all TNA summaries.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets paged TNA summaries.</summary>
    Task<PagedResult<TrainingNeedsAssessmentSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Gets all TNAs for an employee.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets TNAs for a given year.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);
    /// <summary>Gets TNAs where training has not yet been provided.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetUnfulfilledAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets TNAs with the specified priority.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentSummaryDto>> GetByPriorityAsync(TrainingPriority priority, CancellationToken cancellationToken = default);

    // Assessment CRUD
    /// <summary>Creates a new training needs assessment.</summary>
    Task<TrainingNeedsAssessmentDto> CreateAsync(CreateTrainingNeedsAssessmentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Creates the same needs assessment for multiple employees at once.</summary>
    Task<BulkNeedsAssessmentResultDto> BulkCreateAsync(BulkCreateTrainingNeedsAssessmentDto dto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates an existing training needs assessment.</summary>
    Task<TrainingNeedsAssessmentDto> UpdateAsync(UpdateTrainingNeedsAssessmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a training needs assessment.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Recommended program sub-operations
    /// <summary>Links a recommended program to a TNA.</summary>
    Task<TrainingNeedsAssessmentProgramDto> AddRecommendedProgramAsync(CreateTrainingNeedsAssessmentProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all recommended programs for a TNA.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentProgramDto>> GetRecommendedProgramsAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    /// <summary>Removes a recommended program from a TNA.</summary>
    Task<bool> DeleteRecommendedProgramAsync(Guid assessmentProgramId, CancellationToken cancellationToken = default);

    // Skill gap sub-operations
    /// <summary>Adds a skill gap to a TNA.</summary>
    Task<TrainingNeedsAssessmentSkillDto> AddSkillGapAsync(CreateTrainingNeedsAssessmentSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all skill gaps identified in a TNA.</summary>
    Task<IEnumerable<TrainingNeedsAssessmentSkillDto>> GetSkillGapsAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    /// <summary>Removes a skill gap from a TNA.</summary>
    Task<bool> DeleteSkillGapAsync(Guid assessmentSkillId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING WAITLIST SERVICE
// ============================================================================

#region Training Waitlist Service

public interface ITrainingWaitlistService
{
    // Queries
    /// <summary>Gets a waitlist entry by ID.</summary>
    Task<TrainingWaitlistDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets all waitlist entries for a schedule.</summary>
    Task<IEnumerable<TrainingWaitlistDto>> GetByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    /// <summary>Gets all waitlist entries for an employee.</summary>
    Task<IEnumerable<TrainingWaitlistDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets the active waitlist queue for a schedule, ordered by position.</summary>
    Task<IEnumerable<TrainingWaitlistDto>> GetActiveWaitlistAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    // CRUD
    /// <summary>Adds an employee to the waitlist for a schedule. Auto-assigns queue position.</summary>
    Task<TrainingWaitlistDto> AddToWaitlistAsync(CreateTrainingWaitlistDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Removes an employee from the waitlist.</summary>
    Task<bool> RemoveFromWaitlistAsync(Guid waitlistId, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>Offers a slot to the next candidate in the waitlist queue.</summary>
    Task<TrainingWaitlistDto> OfferSlotAsync(OfferWaitlistPositionDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Records the employee's response to a slot offer (accepted or declined).</summary>
    Task<TrainingWaitlistDto> RecordOfferResponseAsync(RespondToWaitlistOfferDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING REQUEST SERVICE
// ============================================================================

#region Training Request Service

public interface ITrainingRequestService
{
    // Queries
    /// <summary>Gets a training request by ID.</summary>
    Task<TrainingRequestDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets a training request by its unique request number.</summary>
    Task<TrainingRequestDto?> GetByRequestNumberAsync(string requestNumber, CancellationToken cancellationToken = default);
    /// <summary>Gets paged summary list of all training requests.</summary>
    Task<PagedResult<TrainingRequestSummaryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    /// <summary>Gets all training requests for an employee.</summary>
    Task<IEnumerable<TrainingRequestSummaryDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets requests by status.</summary>
    Task<IEnumerable<TrainingRequestSummaryDto>> GetByStatusAsync(TrainingRequestStatus status, CancellationToken cancellationToken = default);
    /// <summary>Gets requests awaiting approval.</summary>
    Task<IEnumerable<TrainingRequestSummaryDto>> GetPendingApprovalAsync(CancellationToken cancellationToken = default);

    // CRUD
    /// <summary>Creates a new training request (in Draft status). Auto-generates the request number.</summary>
    Task<TrainingRequestDto> CreateAsync(CreateTrainingRequestDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates a draft training request (title, description, justification, linked program).</summary>
    Task<TrainingRequestDto> UpdateAsync(UpdateTrainingRequestDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a draft training request.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    /// <summary>Submits a draft training request for approval.</summary>
    Task<bool> SubmitAsync(Guid requestId, Guid submittedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Approves a training request, optionally linking it to an existing program.</summary>
    Task<TrainingRequestDto> ApproveAsync(ApproveTrainingRequestDto dto, CancellationToken cancellationToken = default);
    /// <summary>Rejects a training request with a reason.</summary>
    Task<bool> RejectAsync(RejectTrainingRequestDto dto, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// LEARNING PATH SERVICE
// ============================================================================

#region Learning Path Service

public interface ILearningPathService
{
    // Learning path queries
    /// <summary>Gets full learning path details by ID, including programs, target skills, and enrollments.</summary>
    Task<LearningPathDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets summary list of all learning paths.</summary>
    Task<IEnumerable<LearningPathSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all active learning paths.</summary>
    Task<IEnumerable<LearningPathSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets learning paths by status.</summary>
    Task<IEnumerable<LearningPathSummaryDto>> GetByStatusAsync(LearningPathStatus status, CancellationToken cancellationToken = default);
    /// <summary>Gets learning paths applicable to a position.</summary>
    Task<IEnumerable<LearningPathSummaryDto>> GetByPositionAsync(Guid positionId, CancellationToken cancellationToken = default);

    // Learning path CRUD
    /// <summary>Creates a new learning path.</summary>
    Task<LearningPathDto> CreateAsync(CreateLearningPathDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates an existing learning path.</summary>
    Task<LearningPathDto> UpdateAsync(UpdateLearningPathDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a learning path (only if it has no enrollments).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Program sub-operations
    /// <summary>Adds a program to a learning path at the specified sequence position.</summary>
    Task<LearningPathProgramDto> AddProgramAsync(CreateLearningPathProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all programs in a learning path, ordered by sequence.</summary>
    Task<IEnumerable<LearningPathProgramDto>> GetProgramsAsync(Guid learningPathId, CancellationToken cancellationToken = default);
    /// <summary>Updates a learning path program (sequence, mandatory flag, etc.).</summary>
    Task<LearningPathProgramDto> UpdateProgramAsync(UpdateLearningPathProgramDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Removes a program from a learning path.</summary>
    Task<bool> DeleteProgramAsync(Guid learningPathProgramId, CancellationToken cancellationToken = default);

    // Skill sub-operations
    /// <summary>Adds a target skill to a learning path.</summary>
    Task<LearningPathSkillDto> AddSkillAsync(CreateLearningPathSkillDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all target skills for a learning path.</summary>
    Task<IEnumerable<LearningPathSkillDto>> GetSkillsAsync(Guid learningPathId, CancellationToken cancellationToken = default);
    /// <summary>Removes a target skill from a learning path.</summary>
    Task<bool> DeleteSkillAsync(Guid learningPathSkillId, CancellationToken cancellationToken = default);

    // Employee enrollment operations
    /// <summary>Enrolls an employee in a learning path, creating step records for each program.</summary>
    Task<EmployeeLearningPathDto> EnrollEmployeeAsync(EnrollEmployeeInLearningPathDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all learning path enrollments for an employee.</summary>
    Task<IEnumerable<EmployeeLearningPathSummaryDto>> GetEnrollmentsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets the full enrollment record with all steps.</summary>
    Task<EmployeeLearningPathDto> GetEnrollmentByIdAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    /// <summary>Updates a learning path step when the employee completes a program.</summary>
    Task<EmployeeLearningPathStepDto> UpdateStepAsync(UpdateLearningPathStepDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Recalculates and persists the progress percentage for a learning path enrollment.</summary>
    Task<EmployeeLearningPathDto> RecalculateProgressAsync(Guid enrollmentId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets all enrollments for a specific learning path (used on the path detail page).</summary>
    Task<IEnumerable<EmployeeLearningPathSummaryDto>> GetEnrollmentsByPathIdAsync(Guid pathId, CancellationToken cancellationToken = default);
    /// <summary>Removes an enrollment (and its steps) from a learning path.</summary>
    Task<bool> RemoveEnrollmentAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
    /// <summary>Gets all enrollments across all learning paths with full employee and path details.</summary>
    Task<IEnumerable<EnrollmentListItemDto>> GetAllEnrollmentsAsync(CancellationToken cancellationToken = default);
    /// <summary>Updates the target completion date and notes on an enrollment.</summary>
    Task<EmployeeLearningPathDto> UpdateEnrollmentAsync(UpdateEnrollmentDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Gets the full detail package for an employee's step-level detail page, including program info, materials, available schedules, nomination, attendance, completion, and feedback.</summary>
    Task<StepDetailPageDto> GetStepDetailAsync(Guid stepId, Guid employeeId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// MENTORING SERVICE
// ============================================================================

#region Mentoring Service

public interface IMentoringService
{
    // Mentoring program queries
    /// <summary>Gets full mentoring program details by ID, including all pairs.</summary>
    Task<MentoringProgramDto> GetProgramByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets all mentoring program summaries.</summary>
    Task<IEnumerable<MentoringProgramSummaryDto>> GetAllProgramsAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets all active mentoring programs.</summary>
    Task<IEnumerable<MentoringProgramSummaryDto>> GetActiveProgramsAsync(CancellationToken cancellationToken = default);

    // Mentoring program CRUD
    /// <summary>Creates a new mentoring program.</summary>
    Task<MentoringProgramDto> CreateProgramAsync(CreateMentoringProgramDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates an existing mentoring program.</summary>
    Task<MentoringProgramDto> UpdateProgramAsync(UpdateMentoringProgramDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a mentoring program (only if it has no pairs).</summary>
    Task<bool> DeleteProgramAsync(Guid id, CancellationToken cancellationToken = default);

    // Mentoring pair operations
    /// <summary>Gets full mentoring pair details by ID, including all sessions.</summary>
    Task<MentoringPairDto> GetPairByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets all mentoring pairs in a program.</summary>
    Task<IEnumerable<MentoringPairSummaryDto>> GetPairsForProgramAsync(Guid programId, CancellationToken cancellationToken = default);
    /// <summary>Gets all pairs where the employee is mentor or mentee.</summary>
    Task<IEnumerable<MentoringPairSummaryDto>> GetPairsForEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Gets all active mentoring pairs.</summary>
    Task<IEnumerable<MentoringPairSummaryDto>> GetActivePairsAsync(CancellationToken cancellationToken = default);
    /// <summary>Creates a new mentoring pair within a program.</summary>
    Task<MentoringPairDto> CreatePairAsync(CreateMentoringPairDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates a mentoring pair (status, goals, ratings, closure notes).</summary>
    Task<MentoringPairDto> UpdatePairAsync(UpdateMentoringPairDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Closes a mentoring pair with closure notes.</summary>
    Task<bool> ClosePairAsync(Guid pairId, string closureNotes, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Mentoring session operations
    /// <summary>Gets a mentoring session by ID.</summary>
    Task<MentoringSessionDto> GetSessionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Gets all sessions for a mentoring pair.</summary>
    Task<IEnumerable<MentoringSessionDto>> GetSessionsForPairAsync(Guid pairId, CancellationToken cancellationToken = default);
    /// <summary>Records a new mentoring session.</summary>
    Task<MentoringSessionDto> LogSessionAsync(CreateMentoringSessionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    /// <summary>Updates a recorded mentoring session.</summary>
    Task<MentoringSessionDto> UpdateSessionAsync(UpdateMentoringSessionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    /// <summary>Deletes a mentoring session.</summary>
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// TRAINING DASHBOARD SERVICE
// ============================================================================

#region Training Dashboard Service

public interface ITrainingDashboardService
{
    /// <summary>Gets the training dashboard summary for the current or specified year.</summary>
    Task<TrainingDashboardDto> GetDashboardAsync(int? year = null, CancellationToken cancellationToken = default);

    /// <summary>Gets the combined operations + Kirkpatrick-effectiveness analytics for a year.</summary>
    Task<TrainingAnalyticsDto> GetAnalyticsAsync(int? year = null, CancellationToken cancellationToken = default);

    /// <summary>Gets a comprehensive training summary for a single employee.</summary>
    Task<EmployeeTrainingSummaryDto> GetEmployeeSummaryAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

#endregion
