// <auto-ported> HR module — EF Core configuration.
//
// This partial holds ALL HR DbSets and HR Fluent configuration for the HR module ported from the
// standalone HRApi solution. It is deliberately kept OUT of ApplicationDbContext.cs so that the
// shared DbContext (worked on by other module developers) only gains a single call:
//     ConfigureHrModule(builder);   // inside OnModelCreating
// Re-syncing HR from HRApi therefore only rewrites this file.
// See HR_MODULE_PORT_PLAN.md.
//
// NOTE: the Performance/appraisal area is carved out — RHEMA keeps its own appraisal model, so
// HRApi's Performance configuration is intentionally absent here.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data.Configuration;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Entities.HR.Awards;
using ErpSystem.Core.Entities.HR.CompanySchedule;
using ErpSystem.Core.Entities.HR.JobAnalysis;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Entities.HR.Letters;
using ErpSystem.Core.Entities.HR.ProfileChanges;
using ErpSystem.Core.Entities.HR.PromotionTransfer;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Entities.HR.StaffGrievance;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Entities.HR.SuccessionPlanning;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Entities.HR.StaffAttendance;
using ErpSystem.Core.Entities.HR.StaffTravel;

namespace ErpSystem.Data;

public partial class ApplicationDbContext
{
    #region HR DbSets (ported from HRApi)

    public DbSet<ReasonCode> ReasonCodes { get; set; } = null!;
    public DbSet<Team> Teams { get; set; } = null!;
    public DbSet<TeamMember> TeamMembers { get; set; } = null!;
    public DbSet<TeamMemberHistory> TeamMemberHistories { get; set; } = null!;
    public DbSet<BenefitGradeValue> BenefitGradeValues { get; set; } = null!;
    public DbSet<EmployeeBenefitEnrollment> EmployeeBenefitEnrollments { get; set; } = null!;
    public DbSet<BenefitBeneficiary> BenefitBeneficiaries { get; set; } = null!;
    public DbSet<BenefitUtilization> BenefitUtilizations { get; set; } = null!;
    public DbSet<Competency> Competencies { get; set; } = null!;
    public DbSet<CompetencySkillIndicator> CompetencySkillIndicators { get; set; } = null!;
    public DbSet<PositionCompetency> PositionCompetencies { get; set; } = null!;
    public DbSet<EmployeeCompetency> EmployeeCompetencies { get; set; } = null!;
    public DbSet<EmployeeCompetencyHistory> EmployeeCompetencyHistories { get; set; } = null!;
    public DbSet<StaffAttendanceRecord> StaffAttendanceRecords { get; set; } = null!;
    public DbSet<StaffDailyAttendance> StaffDailyAttendances { get; set; } = null!;
    public DbSet<StaffAttendanceLog> StaffAttendanceLogs { get; set; } = null!;
    public DbSet<AttendanceLocationVerificationLog> AttendanceLocationVerificationLogs { get; set; } = null!;
    public DbSet<StaffAttendanceRegularization> StaffAttendanceRegularizations { get; set; } = null!;
    public DbSet<StaffMonthlyAttendanceSummary> StaffMonthlyAttendanceSummaries { get; set; } = null!;
    public DbSet<StaffBulkAttendanceImport> StaffBulkAttendanceImports { get; set; } = null!;
    public DbSet<StaffBulkAttendanceImportRow> StaffBulkAttendanceImportRows { get; set; } = null!;
    public DbSet<WorkSchedule> WorkSchedules { get; set; } = null!;
    public DbSet<EmployeeWorkSchedule> EmployeeWorkSchedules { get; set; } = null!;
    public DbSet<ShiftDefinition> ShiftDefinitions { get; set; } = null!;
    public DbSet<ShiftAssignment> ShiftAssignments { get; set; } = null!;
    public DbSet<ShiftRotationPlan> ShiftRotationPlans { get; set; } = null!;
    public DbSet<ShiftRotationStage> ShiftRotationStages { get; set; } = null!;
    public DbSet<ShiftRotationMember> ShiftRotationMembers { get; set; } = null!;
    public DbSet<PositionOvertimePolicy> PositionOvertimePolicies { get; set; } = null!;
    public DbSet<EmployeeOvertimeOverride> EmployeeOvertimeOverrides { get; set; } = null!;
    public DbSet<StaffOvertimeRequest> StaffOvertimeRequests { get; set; } = null!;
    public DbSet<EmployeeBiometric> EmployeeBiometrics { get; set; } = null!;
    public DbSet<StaffAttendanceDevice> StaffAttendanceDevices { get; set; } = null!;
    public DbSet<GeofenceZone> GeofenceZones { get; set; } = null!;
    public DbSet<RemoteWorkRequest> RemoteWorkRequests { get; set; } = null!;
    public DbSet<HolidayCalendar> HolidayCalendars { get; set; } = null!;
    public DbSet<PublicHoliday> PublicHolidays { get; set; } = null!;
    public DbSet<PayPeriod> PayPeriods { get; set; } = null!;
    public DbSet<StaffAttendancePayrollExport> StaffAttendancePayrollExports { get; set; } = null!;
    public DbSet<StaffAttendanceAlertRule> StaffAttendanceAlertRules { get; set; } = null!;
    public DbSet<StaffAttendanceAlert> StaffAttendanceAlerts { get; set; } = null!;
    public DbSet<ConsultantClient> ConsultantClients { get; set; } = null!;
    public DbSet<ClientEngagement> ClientEngagements { get; set; } = null!;
    public DbSet<ConsultantTimesheet> ConsultantTimesheets { get; set; } = null!;
    public DbSet<ConsultantTimesheetEntry> ConsultantTimesheetEntries { get; set; } = null!;
    public DbSet<ClientTimesheetConfirmation> ClientTimesheetConfirmations { get; set; } = null!;
    public DbSet<ConsultantClientPortalAccount> ConsultantClientPortalAccounts { get; set; } = null!;
    public DbSet<TimesheetInvoice> TimesheetInvoices { get; set; } = null!;
    public DbSet<TimesheetInvoiceLink> TimesheetInvoiceLinks { get; set; } = null!;
    public DbSet<LeaveTypeEligibility> LeaveTypeEligibilities { get; set; } = null!;
    public DbSet<LeaveAccrualPolicy> LeaveAccrualPolicies { get; set; } = null!;
    public DbSet<LeaveRequestAttachment> LeaveRequestAttachments { get; set; } = null!;
    public DbSet<LeaveEncashment> LeaveEncashments { get; set; } = null!;
    public DbSet<LeaveAdjustment> LeaveAdjustments { get; set; } = null!;
    public DbSet<EmployeeReliever> EmployeeRelievers { get; set; } = null!;
    public DbSet<LeaveTypeAllowance> LeaveTypeAllowances { get; set; } = null!;
    public DbSet<PayComponent> PayComponents { get; set; } = null!;
    public DbSet<PositionPayComponent> PositionPayComponents { get; set; } = null!;
    public DbSet<EmployeePayComponent> EmployeePayComponents { get; set; } = null!;
    public DbSet<AppraisalCompetency> AppraisalCompetencies { get; set; } = null!;
    public DbSet<AppraisalTemplate> AppraisalTemplates { get; set; } = null!;
    public DbSet<AppraisalTemplateSection> AppraisalTemplateSections { get; set; } = null!;
    public DbSet<AppraisalTemplateItem> AppraisalTemplateItems { get; set; } = null!;
    public DbSet<TemplateItemGradeRange> TemplateItemGradeRanges { get; set; } = null!;
    public DbSet<AppraisalSettings> AppraisalSettings { get; set; } = null!;
    public DbSet<AppraisalCycle> AppraisalCycles { get; set; } = null!;
    public DbSet<AppraisalCycleTarget> AppraisalCycleTargets { get; set; } = null!;
    public DbSet<AppraisalCycleTargetExclusion> AppraisalCycleTargetExclusions { get; set; } = null!;
    public DbSet<AppraisalCycleTemplate> AppraisalCycleTemplates { get; set; } = null!;
    public DbSet<GoalLibrary> GoalLibraries { get; set; } = null!;
    public DbSet<StrategicGoal> StrategicGoals { get; set; } = null!;
    public DbSet<CompanyGoal> CompanyGoals { get; set; } = null!;
    public DbSet<UnitGoal> UnitGoals { get; set; } = null!;
    public DbSet<EmployeeGoal> EmployeeGoals { get; set; } = null!;
    public DbSet<GoalProgressEntry> GoalProgressEntries { get; set; } = null!;
    public DbSet<EmployeeGoalAppraisalAssessment> EmployeeGoalAppraisalAssessments { get; set; } = null!;
    public DbSet<GoalRequiredSkill> GoalRequiredSkills { get; set; } = null!;
    public DbSet<CompanyHrPolicySettings> CompanyHrPolicySettings { get; set; } = null!;
    public DbSet<CompanyProfile> CompanyProfiles { get; set; } = null!;
    public DbSet<CheckIn> CheckIns { get; set; } = null!;
    public DbSet<CheckInGoalUpdate> CheckInGoalUpdates { get; set; } = null!;
    public DbSet<CheckInObjectiveLink> CheckInObjectiveLinks { get; set; } = null!;
    public DbSet<AppraisalOutcomeRecommendation> AppraisalOutcomeRecommendations { get; set; } = null!;
    public DbSet<SalaryReviewProposal> SalaryReviewProposals { get; set; } = null!;
    public DbSet<EmploymentActionProposal> EmploymentActionProposals { get; set; } = null!;
    public DbSet<PerformanceJournalEntry> PerformanceJournalEntries { get; set; } = null!;
    public DbSet<EmployeeDevelopmentPlan> EmployeeDevelopmentPlans { get; set; } = null!;
    public DbSet<EmployeeDevelopmentObjective> EmployeeDevelopmentObjectives { get; set; } = null!;
    public DbSet<EmployeeDevelopmentPlanFeedback> EmployeeDevelopmentPlanFeedbacks { get; set; } = null!;
    public DbSet<AppraisalReviewEvent> AppraisalReviewEvents { get; set; } = null!;
    public DbSet<AppraisalConversation> AppraisalConversations { get; set; } = null!;
    public DbSet<PerformanceAppraisalCriterionConfig> PerformanceAppraisalCriterionConfigs { get; set; } = null!;
    public DbSet<PerformanceAppraisalCriterionConfigGradeRange> PerformanceAppraisalCriterionConfigGradeRanges { get; set; } = null!;
    public DbSet<PeerNomination> PeerNominations { get; set; } = null!;
    public DbSet<AppraisalHRReview> AppraisalHRReviews { get; set; } = null!;
    public DbSet<AppraisalCustomQuestionResponse> AppraisalCustomQuestionResponses { get; set; } = null!;
    public DbSet<AppraisalManualAdvanceLog> AppraisalManualAdvanceLogs { get; set; } = null!;
    public DbSet<CalibrationSession> CalibrationSessions { get; set; } = null!;
    public DbSet<CalibrationParticipant> CalibrationParticipants { get; set; } = null!;
    public DbSet<CalibrationRatingAdjustment> CalibrationRatingAdjustments { get; set; } = null!;
    public DbSet<AppraisalAppeal> AppraisalAppeals { get; set; } = null!;
    public DbSet<AppraisalAppealItem> AppraisalAppealItems { get; set; } = null!;
    public DbSet<PipGoal> PipGoals { get; set; } = null!;
    public DbSet<AppraisalEvaluationSnapshot> AppraisalEvaluationSnapshots { get; set; } = null!;
    public DbSet<AppraisalCriterionScoreSnapshot> AppraisalCriterionScoreSnapshots { get; set; } = null!;
    public DbSet<AppraisalKpiEvaluationSnapshot> AppraisalKpiEvaluationSnapshots { get; set; } = null!;
    public DbSet<AppraisalNotification> AppraisalNotifications { get; set; } = null!;
    public DbSet<AssetTypeAttribute> AssetTypeAttributes { get; set; } = null!;
    public DbSet<CompanyAsset> CompanyAssets { get; set; } = null!;
    public DbSet<AssetAttributeValue> AssetAttributeValues { get; set; } = null!;
    public DbSet<AssetImage> AssetImages { get; set; } = null!;
    public DbSet<AssetAssignment> AssetAssignments { get; set; } = null!;
    public DbSet<AssetMaintenance> AssetMaintenances { get; set; } = null!;
    public DbSet<AssetRequisition> AssetRequisitions { get; set; } = null!;
    public DbSet<AssetSurcharge> AssetSurcharges { get; set; } = null!;
    public DbSet<AssetSurchargeRecovery> AssetSurchargeRecoveries { get; set; } = null!;
    public DbSet<AwardType> AwardTypes { get; set; } = null!;
    public DbSet<AwardLevel> AwardLevels { get; set; } = null!;
    public DbSet<AwardTypeTarget> AwardTypeTargets { get; set; } = null!;
    public DbSet<AwardBudget> AwardBudgets { get; set; } = null!;
    public DbSet<AwardCycle> AwardCycles { get; set; } = null!;
    public DbSet<AwardVote> AwardVotes { get; set; } = null!;
    public DbSet<LongServiceMilestone> LongServiceMilestones { get; set; } = null!;
    public DbSet<EmployeeAward> EmployeeAwards { get; set; } = null!;
    public DbSet<TeamAwardRecipient> TeamAwardRecipients { get; set; } = null!;
    public DbSet<AwardAttachment> AwardAttachments { get; set; } = null!;
    public DbSet<AwardNomination> AwardNominations { get; set; } = null!;
    public DbSet<AwardNomineeContribution> AwardNomineeContributions { get; set; } = null!;
    public DbSet<AwardNominationAttachment> AwardNominationAttachments { get; set; } = null!;
    public DbSet<TeamAwardNominee> TeamAwardNominees { get; set; } = null!;
    public DbSet<AwardCommittee> AwardCommittees { get; set; } = null!;
    public DbSet<AwardCommitteeMember> AwardCommitteeMembers { get; set; } = null!;
    public DbSet<AwardNominationReview> AwardNominationReviews { get; set; } = null!;
    public DbSet<LongServiceAward> LongServiceAwards { get; set; } = null!;
    public DbSet<CompanyEvent> CompanyEvents { get; set; } = null!;
    public DbSet<EventParticipant> EventParticipants { get; set; } = null!;
    public DbSet<EventAttendance> EventAttendances { get; set; } = null!;
    public DbSet<EventAttachment> EventAttachments { get; set; } = null!;
    public DbSet<EventTask> EventTasks { get; set; } = null!;
    public DbSet<MeetingRoom> MeetingRooms { get; set; } = null!;
    public DbSet<RoomBooking> RoomBookings { get; set; } = null!;
    public DbSet<CompanyMilestone> CompanyMilestones { get; set; } = null!;
    public DbSet<BusinessClosure> BusinessClosures { get; set; } = null!;
    public DbSet<JobDescription> JobDescriptions { get; set; } = null!;
    public DbSet<JobDutyItem> JobDutyItems { get; set; } = null!;
    public DbSet<JobResponsibility> JobResponsibilities { get; set; } = null!;
    public DbSet<JobQualification> JobQualifications { get; set; } = null!;
    public DbSet<JobCompetency> JobCompetencies { get; set; } = null!;
    public DbSet<ManpowerBudget> ManpowerBudgets { get; set; } = null!;
    public DbSet<ManpowerBudgetLine> ManpowerBudgetLines { get; set; } = null!;
    public DbSet<JobPhysicalDemand> JobPhysicalDemands { get; set; } = null!;
    public DbSet<JobWorkingCondition> JobWorkingConditions { get; set; } = null!;
    public DbSet<JobPpeRequirement> JobPpeRequirements { get; set; } = null!;
    public DbSet<JobEquipmentTool> JobEquipmentTools { get; set; } = null!;
    public DbSet<JobEquipmentTraining> JobEquipmentTrainings { get; set; } = null!;
    public DbSet<JobReportingRelationship> JobReportingRelationships { get; set; } = null!;
    public DbSet<JobMedicalRequirement> JobMedicalRequirements { get; set; } = null!;
    public DbSet<JobResponsibilityKpi> JobResponsibilityKpis { get; set; } = null!;
    public DbSet<Union> Unions { get; set; } = null!;
    public DbSet<CollectiveBargainingAgreement> CollectiveBargainingAgreements { get; set; } = null!;
    public DbSet<JobFamily> JobFamilies { get; set; } = null!;
    public DbSet<JobSubFamily> JobSubFamilies { get; set; } = null!;
    public DbSet<CareerLevel> JobLevels { get; set; } = null!;
    public DbSet<HealthcareFacility> HealthcareFacilities { get; set; } = null!;
    public DbSet<Physician> Physicians { get; set; } = null!;
    public DbSet<FacilityService> FacilityServices { get; set; } = null!;
    public DbSet<MedicalInsuranceProvider> MedicalInsuranceProviders { get; set; } = null!;
    public DbSet<MedicalInsurancePlan> MedicalInsurancePlans { get; set; } = null!;
    public DbSet<EmployeeMedicalInsurancePolicy> EmployeeMedicalInsurancePolicies { get; set; } = null!;
    public DbSet<MedicalInsurancePolicyDependent> MedicalInsurancePolicyDependents { get; set; } = null!;
    public DbSet<MedicalInsuranceClaim> MedicalInsuranceClaims { get; set; } = null!;
    public DbSet<MedicalInsuranceProviderFacility> MedicalInsuranceProviderFacilities { get; set; } = null!;
    public DbSet<MedicalInsuranceProviderDocument> MedicalInsuranceProviderDocuments { get; set; } = null!;
    public DbSet<MedicalInsurancePremiumRecord> MedicalInsurancePremiumRecords { get; set; } = null!;
    public DbSet<MedicalBenefitScheme> MedicalBenefitSchemes { get; set; } = null!;
    public DbSet<MedicalBenefitTier> MedicalBenefitTiers { get; set; } = null!;
    public DbSet<EmployeeHealthProfile> EmployeeHealthProfiles { get; set; } = null!;
    public DbSet<EmployeeHealthCondition> EmployeeHealthConditions { get; set; } = null!;
    public DbSet<EmployeeAllergy> EmployeeAllergies { get; set; } = null!;
    public DbSet<EmployeeMedicalExam> EmployeeMedicalExams { get; set; } = null!;
    public DbSet<EmployeeMedicalExamDocument> EmployeeMedicalExamDocuments { get; set; } = null!;
    public DbSet<MedicalClaimPreAuthorization> MedicalClaimPreAuthorizations { get; set; } = null!;
    public DbSet<MedicalReferral> MedicalReferrals { get; set; } = null!;
    public DbSet<MedicalAppointment> MedicalAppointments { get; set; } = null!;
    public DbSet<NHISClaim> NHISClaims { get; set; } = null!;
    public DbSet<NHISClaimDocument> NHISClaimDocuments { get; set; } = null!;
    public DbSet<MedicalExpenseClaim> MedicalExpenseClaims { get; set; } = null!;
    public DbSet<MedicalExpenseApproval> MedicalExpenseApprovals { get; set; } = null!;
    public DbSet<MedicalExpenseItem> MedicalExpenseItems { get; set; } = null!;
    public DbSet<MedicalExpenseDocument> MedicalExpenseDocuments { get; set; } = null!;
    public DbSet<MedicalExpenseClaimNote> MedicalExpenseClaimNotes { get; set; } = null!;
    public DbSet<StaffMovement> StaffMovements { get; set; } = null!;
    public DbSet<StaffMovementApprovalLevel> StaffMovementApprovalLevels { get; set; } = null!;
    public DbSet<StaffMovementStatusHistory> StaffMovementStatusHistories { get; set; } = null!;
    public DbSet<StaffMovementAttachment> StaffMovementAttachments { get; set; } = null!;
    public DbSet<StaffMovementChecklistItem> StaffMovementChecklistItems { get; set; } = null!;
    public DbSet<StaffMovementReminderRun> StaffMovementReminderRuns { get; set; } = null!;
    public DbSet<StaffMovementReminderDispatchLog> StaffMovementReminderDispatchLogs { get; set; } = null!;
    public DbSet<StaffPromotion> StaffPromotions { get; set; } = null!;
    public DbSet<StaffTransfer> StaffTransfers { get; set; } = null!;
    public DbSet<StaffDemotion> StaffDemotions { get; set; } = null!;
    public DbSet<StaffSecondment> StaffSecondments { get; set; } = null!;
    public DbSet<StaffActingAppointment> StaffActingAppointments { get; set; } = null!;
    public DbSet<EmployeeCareerPath> EmployeeCareerPaths { get; set; } = null!;
    public DbSet<PositionVacancy> PositionVacancies { get; set; } = null!;
    public DbSet<JobVacancy> JobVacancies { get; set; } = null!;
    public DbSet<JobVacancyAttachment> JobVacancyAttachments { get; set; } = null!;
    public DbSet<JobVacancyStatusHistory> JobVacancyStatusHistories { get; set; } = null!;
    public DbSet<JobPosting> JobPostings { get; set; } = null!;
    public DbSet<JobPostingAttachment> JobPostingAttachments { get; set; } = null!;
    public DbSet<VacancyPipelineStageAssignment> VacancyPipelineStageAssignments { get; set; } = null!;
    public DbSet<RecruitmentPipeline> RecruitmentPipelines { get; set; } = null!;
    public DbSet<RecruitmentPipelineStage> RecruitmentPipelineStages { get; set; } = null!;
    public DbSet<JobShortlistingCriteria> JobShortlistingCriterias { get; set; } = null!;
    public DbSet<CandidatePortalAccount> CandidatePortalAccounts { get; set; } = null!;
    public DbSet<JobCandidate> JobCandidates { get; set; } = null!;
    public DbSet<JobCandidateQualification> JobCandidateQualifications { get; set; } = null!;
    public DbSet<JobCandidateWorkHistory> JobCandidateWorkHistories { get; set; } = null!;
    public DbSet<JobCandidateReferee> JobCandidateReferees { get; set; } = null!;
    public DbSet<JobCandidateSkill> JobCandidateSkills { get; set; } = null!;
    public DbSet<JobCandidateLanguage> JobCandidateLanguages { get; set; } = null!;
    public DbSet<JobCandidateInterest> JobCandidateInterests { get; set; } = null!;
    public DbSet<JobCandidateDocument> JobCandidateDocuments { get; set; } = null!;
    public DbSet<JobCandidateNote> JobCandidateNotes { get; set; } = null!;

    // HR controlled-document intake — see ConfigureHrDocumentIntake.
    public DbSet<PublicCvUploadTicket> PublicCvUploadTickets { get; set; } = null!;
    public DbSet<HrLegacyFileMigrationEntry> HrLegacyFileMigrationEntries { get; set; } = null!;
    public DbSet<CandidateTalentSegment> CandidateTalentSegments { get; set; } = null!;
    public DbSet<CandidateSegmentMembership> CandidateSegmentMemberships { get; set; } = null!;
    public DbSet<CandidateEngagementEvent> CandidateEngagementEvents { get; set; } = null!;
    public DbSet<JobApplication> JobApplications { get; set; } = null!;
    public DbSet<JobApplicationStageHistory> JobApplicationStageHistories { get; set; } = null!;
    public DbSet<JobApplicantTestResult> JobApplicantTestResults { get; set; } = null!;
    public DbSet<JobApplicantCommunication> JobApplicantCommunications { get; set; } = null!;
    public DbSet<ShortlistDecisionLog> ShortlistDecisionLogs { get; set; } = null!;
    public DbSet<ShortlistReview> ShortlistReviews { get; set; } = null!;
    public DbSet<JobInterviewQuestionType> JobInterviewQuestionTypes { get; set; } = null!;
    public DbSet<JobInterviewQuestionDetail> JobInterviewQuestionDetails { get; set; } = null!;
    public DbSet<InterviewQuestionPreset> InterviewQuestionPresets { get; set; } = null!;
    public DbSet<InterviewQuestionPresetItem> InterviewQuestionPresetItems { get; set; } = null!;
    public DbSet<JobInterview> JobInterviews { get; set; } = null!;
    public DbSet<JobInterviewPanelist> JobInterviewPanelists { get; set; } = null!;
    public DbSet<JobInterviewExternalPanelist> JobInterviewExternalPanelists { get; set; } = null!;
    public DbSet<JobInterviewee> JobInterviewees { get; set; } = null!;
    public DbSet<JobInterviewQuestion> JobInterviewQuestions { get; set; } = null!;
    public DbSet<JobInterviewSelectedQuestion> JobInterviewSelectedQuestions { get; set; } = null!;
    public DbSet<JobInterviewScoreSummary> JobInterviewScoreSummaries { get; set; } = null!;
    public DbSet<JobInterviewScoreEntry> JobInterviewScoreEntries { get; set; } = null!;
    public DbSet<JobInterviewScoreDraft> JobInterviewScoreDrafts { get; set; } = null!;
    public DbSet<JobOffer> JobOffers { get; set; } = null!;
    public DbSet<JobOfferBenefit> JobOfferBenefits { get; set; } = null!;
    public DbSet<JobOfferNote> JobOfferNotes { get; set; } = null!;
    public DbSet<OfferCandidateToken> OfferCandidateTokens { get; set; } = null!;
    public DbSet<JobHireRecord> JobHireRecords { get; set; } = null!;
    public DbSet<PreEmploymentCheck> PreEmploymentChecks { get; set; } = null!;
    public DbSet<PreEmploymentCheckItem> PreEmploymentCheckItems { get; set; } = null!;
    public DbSet<PreEmploymentCheckTemplate> PreEmploymentCheckTemplates { get; set; } = null!;
    public DbSet<PreEmploymentCheckTemplateItem> PreEmploymentCheckTemplateItems { get; set; } = null!;
    public DbSet<ReferenceCheckResponse> ReferenceCheckResponses { get; set; } = null!;
    public DbSet<OnboardingPlanTemplate> OnboardingPlanTemplates { get; set; } = null!;
    public DbSet<OnboardingTaskTemplate> OnboardingTaskTemplates { get; set; } = null!;
    public DbSet<OnboardingPlan> OnboardingPlans { get; set; } = null!;
    public DbSet<OnboardingTask> OnboardingTasks { get; set; } = null!;
    public DbSet<OnboardingTaskComment> OnboardingTaskComments { get; set; } = null!;
    public DbSet<OnboardingAsset> OnboardingAssets { get; set; } = null!;
    /// <summary>
    /// The exit register (area 9b) — one row per employee leaving, by any route. See
    /// <see cref="EmployeeSeparation"/> for why the disciplinary route writes here too.
    /// </summary>
    public DbSet<EmployeeSeparation> EmployeeSeparations { get; set; } = null!;

    /// <summary>Files attached to a separation, all through the controlled-upload gate.</summary>
    public DbSet<EmployeeSeparationDocument> EmployeeSeparationDocuments { get; set; } = null!;

    /// <summary>The tenant's clearance form — the catalogue every checklist is built from (FR-HR-183).</summary>
    public DbSet<SeparationClearanceTemplate> SeparationClearanceTemplates { get; set; } = null!;

    /// <summary>One line of one employee's clearance form.</summary>
    public DbSet<SeparationClearanceItem> SeparationClearanceItems { get; set; } = null!;

    /// <summary>What the leaver said on the way out. One per separation.</summary>
    public DbSet<SeparationExitInterview> SeparationExitInterviews { get; set; } = null!;

    /// <summary>One pass of the separation reminder sweep (FR-HR-111).</summary>
    public DbSet<SeparationReminderRun> SeparationReminderRuns { get; set; } = null!;

    /// <summary>One reminder the sweep raised, with the key that stops it repeating.</summary>
    public DbSet<SeparationReminderDispatchLog> SeparationReminderDispatchLogs { get; set; } = null!;

    /// <summary>What a leaver is owed and owes back (FR-HR-184). One per separation.</summary>
    public DbSet<SeparationSettlement> SeparationSettlements { get; set; } = null!;

    /// <summary>One line of a final settlement.</summary>
    public DbSet<SeparationSettlementLine> SeparationSettlementLines { get; set; } = null!;
    public DbSet<ProbationPeriod> ProbationPeriods { get; set; } = null!;
    public DbSet<ProbationReview> ProbationReviews { get; set; } = null!;
    public DbSet<ProbationExtension> ProbationExtensions { get; set; } = null!;
    public DbSet<StaffRequisition> StaffRequisitions { get; set; } = null!;
    public DbSet<StaffRequisitionCost> StaffRequisitionCosts { get; set; } = null!;
    public DbSet<StaffRequisitionAttachment> StaffRequisitionAttachments { get; set; } = null!;
    public DbSet<StaffRequisitionComment> StaffRequisitionComments { get; set; } = null!;
    public DbSet<StaffRequisitionHistory> StaffRequisitionHistories { get; set; } = null!;
    public DbSet<StaffOffense> StaffOffenses { get; set; } = null!;
    public DbSet<StaffOffenseProcedure> StaffOffenseProcedures { get; set; } = null!;
    public DbSet<StaffDisciplinaryActionType> StaffDisciplinaryActionTypes { get; set; } = null!;
    public DbSet<StaffDisciplinaryAction> StaffDisciplinaryActions { get; set; } = null!;
    public DbSet<StaffDisciplineInvestigation> StaffDisciplineInvestigations { get; set; } = null!;
    public DbSet<StaffDisciplineHearing> StaffDisciplineHearings { get; set; } = null!;
    public DbSet<StaffDisciplineWarning> StaffDisciplineWarnings { get; set; } = null!;
    public DbSet<StaffDisciplineSuspension> StaffDisciplineSuspensions { get; set; } = null!;
    public DbSet<StaffDisciplineFine> StaffDisciplineFines { get; set; } = null!;
    public DbSet<StaffDisciplineTermination> StaffDisciplineTerminations { get; set; } = null!;
    public DbSet<StaffDisciplineSeparation> StaffDisciplineSeparations { get; set; } = null!;
    public DbSet<StaffDisciplineActionStep> StaffDisciplineActionSteps { get; set; } = null!;
    public DbSet<StaffDisciplineWitness> StaffDisciplineWitnesses { get; set; } = null!;
    public DbSet<StaffDisciplineDocument> StaffDisciplineDocuments { get; set; } = null!;
    public DbSet<StaffDisciplineNote> StaffDisciplineNotes { get; set; } = null!;
    public DbSet<StaffDisciplineNotification> StaffDisciplineNotifications { get; set; } = null!;
    public DbSet<StaffDisciplineAppeal> StaffDisciplineAppeals { get; set; } = null!;
    public DbSet<StaffDisciplineCorrectiveAction> StaffDisciplineCorrectiveActions { get; set; } = null!;
    public DbSet<StaffDisciplineCorrectiveActionItem> StaffDisciplineCorrectiveActionItems { get; set; } = null!;
    public DbSet<StaffDisciplineLegalReview> StaffDisciplineLegalReviews { get; set; } = null!;

    // Area 9 slice 7 — FR-HR-181's grievance ladder. Deliberately separate from the disciplinary
    // case: a grievance is raised BY an employee, a disciplinary case is raised ABOUT one, and
    // conflating them would put the two under one set of read rules.
    public DbSet<StaffGrievance> StaffGrievances { get; set; } = null!;
    public DbSet<StaffGrievanceStep> StaffGrievanceSteps { get; set; } = null!;

    // Area 25 slice 12 — personal-data change requests (decision D6). The employee edits
    // low-risk contact fields directly; identity- and payment-bearing fields arrive here for
    // an HR officer to approve, and approval applies them.
    public DbSet<EmployeeProfileChangeRequest> EmployeeProfileChangeRequests { get; set; } = null!;
    public DbSet<EmployeeProfileChangeItem> EmployeeProfileChangeItems { get; set; } = null!;

    // Area 25 slice 12b — letters an employee asks HR for. HR fulfils either by issuing a
    // generated letter (frozen on the row) or by uploading a signed scan.
    public DbSet<HrLetterRequest> HrLetterRequests { get; set; } = null!;

    // Area 9 slice 8 — the discipline reminder sweep. Covers both halves of the area, which is why
    // it sits with the grievance sets rather than the disciplinary ones.
    public DbSet<DisciplineReminderRun> DisciplineReminderRuns { get; set; } = null!;
    public DbSet<DisciplineReminderDispatchLog> DisciplineReminderDispatchLogs { get; set; } = null!;
    public DbSet<EmployeeOathOfSecrecy> EmployeeOathsOfSecrecy { get; set; } = null!;
    public DbSet<ProbationConfirmingAuthority> ProbationConfirmingAuthorities { get; set; } = null!;
    public DbSet<ProbationReminderRun> ProbationReminderRuns { get; set; } = null!;
    public DbSet<ProbationReminderDispatchLog> ProbationReminderDispatchLogs { get; set; } = null!;
    public DbSet<SheIncidentType> SheIncidentTypes { get; set; } = null!;
    public DbSet<SheIncidentTypeCorrectiveAction> SheIncidentTypeCorrectiveActions { get; set; } = null!;
    public DbSet<SheInjuryType> SheInjuryTypes { get; set; } = null!;
    public DbSet<SheBodyPart> SheBodyParts { get; set; } = null!;
    public DbSet<SheCorrectiveActionTemplate> SheCorrectiveActionTemplates { get; set; } = null!;
    public DbSet<SheRegulatoryBody> SheRegulatoryBodies { get; set; } = null!;
    public DbSet<SafetyIncident> SafetyIncidents { get; set; } = null!;
    public DbSet<SafetyIncidentInvolvedPerson> SafetyIncidentInvolvedPersons { get; set; } = null!;
    public DbSet<SafetyIncidentInjuredBodyPart> SafetyIncidentInjuredBodyParts { get; set; } = null!;
    public DbSet<SafetyIncidentWitness> SafetyIncidentWitnesses { get; set; } = null!;
    public DbSet<SafetyIncidentInvestigationTeamMember> SafetyIncidentInvestigationTeamMembers { get; set; } = null!;
    public DbSet<SafetyIncidentCorrectiveAction> SafetyIncidentCorrectiveActions { get; set; } = null!;
    public DbSet<SafetyIncidentDocument> SafetyIncidentDocuments { get; set; } = null!;
    public DbSet<SafetyIncidentFollowUp> SafetyIncidentFollowUps { get; set; } = null!;
    public DbSet<SheHazard> SheHazards { get; set; } = null!;
    public DbSet<SheHazardControl> SheHazardControls { get; set; } = null!;
    public DbSet<SheHazardCorrectiveAction> SheHazardCorrectiveActions { get; set; } = null!;
    public DbSet<SheRiskAssessment> SheRiskAssessments { get; set; } = null!;
    public DbSet<SheRiskAssessmentHazard> SheRiskAssessmentHazards { get; set; } = null!;
    public DbSet<SheRiskAssessmentAcknowledgement> SheRiskAssessmentAcknowledgements { get; set; } = null!;
    public DbSet<SheInspectionChecklist> SheInspectionChecklists { get; set; } = null!;
    public DbSet<SheInspectionChecklistItem> SheInspectionChecklistItems { get; set; } = null!;
    public DbSet<SafetyInspection> SafetyInspections { get; set; } = null!;
    public DbSet<SafetyInspectionItem> SafetyInspectionItems { get; set; } = null!;
    public DbSet<SafetyInspectionHazard> SafetyInspectionHazards { get; set; } = null!;
    public DbSet<SafetyInspectionHazardAction> SafetyInspectionHazardActions { get; set; } = null!;
    public DbSet<SafetyInspectionDocument> SafetyInspectionDocuments { get; set; } = null!;
    public DbSet<ShePermitToWork> ShePermitToWorks { get; set; } = null!;
    public DbSet<ShePermitToWorkWorker> ShePermitToWorkWorkers { get; set; } = null!;
    public DbSet<ShePermitToWorkExtension> ShePermitToWorkExtensions { get; set; } = null!;
    public DbSet<ShePermitToWorkDocument> ShePermitToWorkDocuments { get; set; } = null!;
    public DbSet<PpeType> PpeTypes { get; set; } = null!;
    public DbSet<PpeInventory> PpeInventories { get; set; } = null!;
    public DbSet<PpeIssuance> PpeIssuances { get; set; } = null!;
    public DbSet<JobRolePpeRequirement> JobRolePpeRequirements { get; set; } = null!;
    public DbSet<SafetyEquipment> SafetyEquipment { get; set; } = null!;
    public DbSet<SafetyEquipmentInspection> SafetyEquipmentInspections { get; set; } = null!;
    public DbSet<SafetyEquipmentInspectionAction> SafetyEquipmentInspectionActions { get; set; } = null!;
    public DbSet<SafetyEquipmentMaintenance> SafetyEquipmentMaintenanceRecords { get; set; } = null!;
    public DbSet<SheContractor> SheContractors { get; set; } = null!;
    public DbSet<SheContractorInduction> SheContractorInductions { get; set; } = null!;
    public DbSet<SheContractorInspection> SheContractorInspections { get; set; } = null!;
    public DbSet<SheContractorNonCompliance> SheContractorNonCompliances { get; set; } = null!;
    public DbSet<SheContractorDocument> SheContractorDocuments { get; set; } = null!;
    public DbSet<SheTrainingPlan> SheTrainingPlans { get; set; } = null!;
    public DbSet<SheTrainingProgram> SheTrainingPrograms { get; set; } = null!;
    public DbSet<SheTrainingAttendance> SheTrainingAttendances { get; set; } = null!;
    public DbSet<SheWasteType> SheWasteTypes { get; set; } = null!;
    public DbSet<SheWasteDisposalRecord> SheWasteDisposalRecords { get; set; } = null!;
    public DbSet<SheEnvironmentalIncident> SheEnvironmentalIncidents { get; set; } = null!;
    public DbSet<SheEnvironmentalMonitoringRecord> SheEnvironmentalMonitoringRecords { get; set; } = null!;
    public DbSet<SheOccupationalHealthSurveillance> SheOccupationalHealthSurveillances { get; set; } = null!;
    public DbSet<SheFirstAidStation> SheFirstAidStations { get; set; } = null!;
    public DbSet<SheWellnessProgram> SheWellnessPrograms { get; set; } = null!;
    public DbSet<EmergencyPlan> EmergencyPlans { get; set; } = null!;
    public DbSet<SheAssemblyPoint> SheAssemblyPoints { get; set; } = null!;
    public DbSet<EmergencyContact> EmergencyContacts { get; set; } = null!;
    public DbSet<EmergencyDrill> EmergencyDrills { get; set; } = null!;
    public DbSet<EmergencyResponseTeam> EmergencyResponseTeams { get; set; } = null!;
    public DbSet<SheRegulatoryObligation> SheRegulatoryObligations { get; set; } = null!;
    public DbSet<SheRegulatoryComplianceEvidence> SheRegulatoryComplianceEvidences { get; set; } = null!;
    public DbSet<SafetySign> SafetySigns { get; set; } = null!;
    public DbSet<ShePerformanceSnapshot> ShePerformanceSnapshots { get; set; } = null!;
    public DbSet<SafetyCommittee> SafetyCommittees { get; set; } = null!;
    public DbSet<SafetyCommitteeMember> SafetyCommitteeMembers { get; set; } = null!;
    public DbSet<SafetyMeeting> SafetyMeetings { get; set; } = null!;
    public DbSet<SafetyMeetingAttendee> SafetyMeetingAttendees { get; set; } = null!;
    public DbSet<SafetyMeetingActionItem> SafetyMeetingActionItems { get; set; } = null!;
    public DbSet<SafetyMeetingDocument> SafetyMeetingDocuments { get; set; } = null!;
    public DbSet<SheReturnToWorkPlan> SheReturnToWorkPlans { get; set; } = null!;
    public DbSet<SheReturnToWorkPhase> SheReturnToWorkPhases { get; set; } = null!;
    public DbSet<SheReturnToWorkReview> SheReturnToWorkReviews { get; set; } = null!;
    public DbSet<SheReminderRun> SheReminderRuns { get; set; } = null!;
    public DbSet<SheReminderDispatchLog> SheReminderDispatchLogs { get; set; } = null!;
    public DbSet<SheAudit> SheAudits { get; set; } = null!;
    public DbSet<SheAuditTeamMember> SheAuditTeamMembers { get; set; } = null!;
    public DbSet<SheAuditFinding> SheAuditFindings { get; set; } = null!;
    public DbSet<SheAuditFindingAction> SheAuditFindingActions { get; set; } = null!;
    public DbSet<SheStopWorkOrder> SheStopWorkOrders { get; set; } = null!;
    public DbSet<SheStatutoryIncidentSubmission> SheStatutoryIncidentSubmissions { get; set; } = null!;
    public DbSet<SheControlledDocument> SheControlledDocuments { get; set; } = null!;
    public DbSet<SheEnvironmentalPermit> SheEnvironmentalPermits { get; set; } = null!;
    public DbSet<SheEnvironmentalMonitoringSchedule> SheEnvironmentalMonitoringSchedules { get; set; } = null!;
    public DbSet<SheRegulatoryUpdate> SheRegulatoryUpdates { get; set; } = null!;
    public DbSet<SheSustainabilityInitiative> SheSustainabilityInitiatives { get; set; } = null!;
    public DbSet<SheEnvironmentalReview> SheEnvironmentalReviews { get; set; } = null!;
    public DbSet<SheEnvironmentalReviewAction> SheEnvironmentalReviewActions { get; set; } = null!;
    public DbSet<SheMonthlyEnvironmentalReport> SheMonthlyEnvironmentalReports { get; set; } = null!;
    public DbSet<SuccessionPlan> SuccessionPlans { get; set; } = null!;
    public DbSet<SuccessionCompetencyRequirement> SuccessionCompetencyRequirements { get; set; } = null!;
    public DbSet<SuccessionCandidate> SuccessionCandidates { get; set; } = null!;
    public DbSet<SuccessionCandidateGap> SuccessionCandidateGaps { get; set; } = null!;
    public DbSet<SuccessionCandidateFeedback> SuccessionCandidateFeedback { get; set; } = null!;
    public DbSet<SuccessionDevelopmentActivity> SuccessionDevelopmentActivities { get; set; } = null!;
    public DbSet<SuccessionDevelopmentMilestone> SuccessionDevelopmentMilestones { get; set; } = null!;
    public DbSet<SuccessionAction> SuccessionActions { get; set; } = null!;
    public DbSet<SuccessionPlanHistory> SuccessionPlanHistories { get; set; } = null!;
    public DbSet<SuccessionDocument> SuccessionDocuments { get; set; } = null!;
    public DbSet<TalentPool> TalentPools { get; set; } = null!;
    public DbSet<TalentPoolTypeDefinition> TalentPoolTypeDefinitions { get; set; } = null!;
    public DbSet<TalentPoolMember> TalentPoolMembers { get; set; } = null!;
    public DbSet<TalentReviewSession> TalentReviewSessions { get; set; } = null!;
    public DbSet<TalentReviewRating> TalentReviewRatings { get; set; } = null!;
    public DbSet<TrainingVendor> TrainingVendors { get; set; } = null!;
    public DbSet<TrainerProfile> TrainerProfiles { get; set; } = null!;
    public DbSet<TrainerSkill> TrainerSkills { get; set; } = null!;
    public DbSet<TrainerAvailability> TrainerAvailabilities { get; set; } = null!;
    public DbSet<TrainingCategoryOption> TrainingCategoryOptions { get; set; } = null!;
    public DbSet<TrainingProgramGroup> TrainingProgramGroups { get; set; } = null!;
    public DbSet<TrainingProgram> TrainingPrograms { get; set; } = null!;
    public DbSet<TrainingServiceBond> TrainingServiceBonds { get; set; } = null!;
    public DbSet<TrainingStatusHistory> TrainingStatusHistories { get; set; } = null!;
    public DbSet<TrainingMaterial> TrainingMaterials { get; set; } = null!;
    public DbSet<TrainingSchedule> TrainingSchedules { get; set; } = null!;
    public DbSet<TrainingSession> TrainingSessions { get; set; } = null!;
    public DbSet<TrainingProgramCompetency> TrainingProgramCompetencies { get; set; } = null!;
    public DbSet<TrainingProgramSkill> TrainingProgramSkills { get; set; } = null!;
    public DbSet<TrainingNomination> TrainingNominations { get; set; } = null!;
    public DbSet<TrainingCompletion> TrainingCompletions { get; set; } = null!;
    public DbSet<TrainingAttendance> TrainingAttendances { get; set; } = null!;
    public DbSet<TrainingFeedback> TrainingFeedbacks { get; set; } = null!;
    public DbSet<TrainingFollowUpAssessment> TrainingFollowUpAssessments { get; set; } = null!;
    public DbSet<TrainingCertificate> TrainingCertificates { get; set; } = null!;
    public DbSet<EmployeeCertificate> EmployeeCertificates { get; set; } = null!;
    public DbSet<ComplianceTrainingRequirement> ComplianceTrainingRequirements { get; set; } = null!;
    public DbSet<EmployeeComplianceRecord> EmployeeComplianceRecords { get; set; } = null!;
    public DbSet<TrainingBudget> TrainingBudgets { get; set; } = null!;
    public DbSet<TrainingBudgetTransaction> TrainingBudgetTransactions { get; set; } = null!;
    public DbSet<TrainingPlan> TrainingPlans { get; set; } = null!;
    public DbSet<TrainingPlanItem> TrainingPlanItems { get; set; } = null!;
    public DbSet<TrainingPlanBudgetLine> TrainingPlanBudgetLines { get; set; } = null!;
    public DbSet<TrainingNeedsAssessment> TrainingNeedsAssessments { get; set; } = null!;
    public DbSet<TrainingNeedsAssessmentProgram> TrainingNeedsAssessmentPrograms { get; set; } = null!;
    public DbSet<TrainingNeedsAssessmentSkill> TrainingNeedsAssessmentSkills { get; set; } = null!;
    public DbSet<TrainingWaitlist> TrainingWaitlists { get; set; } = null!;
    public DbSet<TrainingRequest> TrainingRequests { get; set; } = null!;
    public DbSet<LearningPath> LearningPaths { get; set; } = null!;
    public DbSet<LearningPathProgram> LearningPathPrograms { get; set; } = null!;
    public DbSet<LearningPathSkill> LearningPathSkills { get; set; } = null!;
    public DbSet<EmployeeLearningPath> EmployeeLearningPaths { get; set; } = null!;
    public DbSet<EmployeeLearningPathStep> EmployeeLearningPathSteps { get; set; } = null!;
    public DbSet<MentoringProgram> MentoringPrograms { get; set; } = null!;
    public DbSet<MentoringPair> MentoringPairs { get; set; } = null!;
    public DbSet<MentoringSession> MentoringSessions { get; set; } = null!;
    public DbSet<OrientationCategory> OrientationCategories { get; set; } = null!;
    public DbSet<OrientationProgram> OrientationPrograms { get; set; } = null!;
    public DbSet<OrientationModule> OrientationModules { get; set; } = null!;
    public DbSet<OrientationContentItem> OrientationContentItems { get; set; } = null!;
    public DbSet<OrientationPrerequisite> OrientationPrerequisites { get; set; } = null!;
    public DbSet<OrientationAudienceRule> OrientationAudienceRules { get; set; } = null!;
    public DbSet<OrientationSession> OrientationSessions { get; set; } = null!;
    public DbSet<OrientationSessionFacilitator> OrientationSessionFacilitators { get; set; } = null!;
    public DbSet<OrientationAttendanceRecord> OrientationAttendanceRecords { get; set; } = null!;
    public DbSet<EmployeeOrientation> EmployeeOrientations { get; set; } = null!;
    public DbSet<OrientationContentProgress> OrientationContentProgresses { get; set; } = null!;
    public DbSet<OrientationAssessmentQuestion> OrientationAssessmentQuestions { get; set; } = null!;
    public DbSet<OrientationAssessmentOption> OrientationAssessmentOptions { get; set; } = null!;
    public DbSet<OrientationAssessmentResponse> OrientationAssessmentResponses { get; set; } = null!;
    public DbSet<OrientationAcknowledgement> OrientationAcknowledgements { get; set; } = null!;
    public DbSet<OrientationFeedback> OrientationFeedbacks { get; set; } = null!;
    public DbSet<OrientationCertificate> OrientationCertificates { get; set; } = null!;
    public DbSet<OrientationNotification> OrientationNotifications { get; set; } = null!;
    public DbSet<StaffTravelRequest> StaffTravelRequests { get; set; } = null!;
    public DbSet<StaffGroupTravel> StaffGroupTravels { get; set; } = null!;
    public DbSet<StaffTravelRequestComment> StaffTravelRequestComments { get; set; } = null!;
    public DbSet<StaffTravelRequestAttachment> StaffTravelRequestAttachments { get; set; } = null!;
    public DbSet<StaffTravelReminderRun> StaffTravelReminderRuns { get; set; } = null!;
    public DbSet<StaffTravelReminderDispatchLog> StaffTravelReminderDispatchLogs { get; set; } = null!;

    // ---- Asset reminder engine (area 16 slice 9, AST-1) ----
    public DbSet<AssetReminderRun> AssetReminderRuns { get; set; } = null!;
    public DbSet<AssetReminderDispatchLog> AssetReminderDispatchLogs { get; set; } = null!;
    public DbSet<StaffTravelItinerary> StaffTravelItineraries { get; set; } = null!;
    public DbSet<StaffTravelItineraryLeg> StaffTravelItineraryLegs { get; set; } = null!;
    public DbSet<StaffTravelItineraryActivity> StaffTravelItineraryActivities { get; set; } = null!;
    public DbSet<StaffTravelFlightBooking> StaffTravelFlightBookings { get; set; } = null!;
    public DbSet<StaffTravelFlightSegment> StaffTravelFlightSegments { get; set; } = null!;
    public DbSet<StaffTravelHotelBooking> StaffTravelHotelBookings { get; set; } = null!;
    public DbSet<StaffTravelGroundTransport> StaffTravelGroundTransports { get; set; } = null!;
    public DbSet<StaffTravelCarRentalBooking> StaffTravelCarRentalBookings { get; set; } = null!;
    public DbSet<StaffTravelBudget> StaffTravelBudgets { get; set; } = null!;
    public DbSet<StaffTravelExpenseClaim> StaffTravelExpenseClaims { get; set; } = null!;
    public DbSet<StaffTravelExpenseClaimLine> StaffTravelExpenseClaimLines { get; set; } = null!;
    public DbSet<StaffTravelAdvance> StaffTravelAdvances { get; set; } = null!;
    public DbSet<StaffTravelPerDiemRate> StaffTravelPerDiemRates { get; set; } = null!;
    public DbSet<StaffTravelPolicy> StaffTravelPolicies { get; set; } = null!;
    public DbSet<StaffTravelPolicyRule> StaffTravelPolicyRules { get; set; } = null!;
    public DbSet<StaffTravelPolicyException> StaffTravelPolicyExceptions { get; set; } = null!;
    public DbSet<StaffTravelDocument> StaffTravelDocuments { get; set; } = null!;
    public DbSet<StaffTravelVisaRequirement> StaffTravelVisaRequirements { get; set; } = null!;
    public DbSet<StaffTravelVisaApplication> StaffTravelVisaApplications { get; set; } = null!;
    public DbSet<StaffTravelRiskAssessment> StaffTravelRiskAssessments { get; set; } = null!;
    public DbSet<StaffTravelAlert> StaffTravelAlerts { get; set; } = null!;
    public DbSet<StaffTravelAlertNotification> StaffTravelAlertNotifications { get; set; } = null!;
    public DbSet<StaffTravelInsurancePolicy> StaffTravelInsurancePolicies { get; set; } = null!;
    public DbSet<StaffTravelHealthRequirement> StaffTravelHealthRequirements { get; set; } = null!;
    public DbSet<NumberSequence> NumberSequences { get; set; } = null!;

    #endregion

    /// <summary>Entry point: configures every HR entity. Called from OnModelCreating.</summary>
    private void ConfigureHrModule(ModelBuilder builder)
    {
        ConfigureHREntities(builder);
        ConfigureTrainingEntities(builder);
        ConfigureOrientationEntities(builder);
        ConfigureSuccessionPlanningEntities(builder);
        ConfigureStaffTravelEntities(builder);
        ConfigureHrDocumentIntake(builder);
    }

    /// <summary>
    /// Indexes linking HR domain rows to their controlled uploads and central-DMS
    /// documents, plus the two intake tables backing public CV uploads and the
    /// legacy-file migration.
    /// </summary>
    /// <remarks>
    /// Indexes only — deliberately no foreign keys to FileUploadRecords or the DMS
    /// tables. Controlled uploads are removed by soft-delete, FileUploadRecords carries
    /// a delete-guard trigger, and the database here is rebuilt from the model rather
    /// than from migrations, so a dozen extra Restrict relationships would be a dozen
    /// new ways for that rebuild to fail while buying integrity the soft-delete contract
    /// already provides. This mirrors how FileUploadRecord.UploadedByUserId is treated.
    /// </remarks>
    private void ConfigureHrDocumentIntake(ModelBuilder builder)
    {
        builder.Entity<JobCandidate>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.CvFileUploadRecordId });
            entity.HasIndex(item => new { item.TenantId, item.ProfilePhotoFileUploadRecordId });
        });

        builder.Entity<JobCandidateDocument>(entity =>
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId }));

        builder.Entity<LeaveRequestAttachment>(entity =>
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId }));

        builder.Entity<AppraisalAttachment>(entity =>
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId }));

        builder.Entity<StaffDisciplineDocument>(entity =>
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId }));

        builder.Entity<StaffMovementAttachment>(entity =>
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId }));

        builder.Entity<EmployeeMedicalExamDocument>(entity =>
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId }));

        builder.Entity<MedicalExpenseDocument>(entity =>
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId }));

        builder.Entity<JobOffer>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.OfferLetterFileUploadRecordId });
            entity.HasIndex(item => new { item.TenantId, item.SignedOfferLetterFileUploadRecordId });
        });

        builder.Entity<PublicCvUploadTicket>(entity =>
        {
            // Claiming looks a ticket up by tenant + hash; uniqueness makes a replayed
            // token a lookup miss rather than an ambiguous match.
            entity.HasIndex(item => new { item.TenantId, item.TokenHash }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId });
            // Drives the sweeper's "unclaimed and expired" scan.
            entity.HasIndex(item => new { item.ClaimedAtUtc, item.ExpiresAtUtc });
            entity.Property(item => item.TokenHash).IsUnicode(false).IsFixedLength();
        });

        builder.Entity<HrLegacyFileMigrationEntry>(entity =>
        {
            // One ledger row per owning record keeps re-runs idempotent.
            entity.HasIndex(item => new { item.TenantId, item.EntityType, item.EntityId })
                .IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Status });
        });
    }

private void ConfigureHREntities(ModelBuilder builder)
    {
        builder.Entity<OrganizationStructure>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_OrgStructure_Tenant_Code");

            entity.HasIndex(e => new { e.TenantId, e.IsDefault })
                .HasDatabaseName("IX_OrgStructure_Tenant_Default");

            entity.HasIndex(e => new { e.TenantId, e.IsActive })
                .HasDatabaseName("IX_OrgStructure_Tenant_Active");

            entity.HasMany(e => e.Levels)
                .WithOne(e => e.OrganizationStructure)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // ORGANIZATION LEVEL
        // ============================================================================
        builder.Entity<OrganizationLevel>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.LevelNumber })
                .IsUnique()
                .HasDatabaseName("IX_OrgLevel_Tenant_Structure_LevelNum");

            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_OrgLevel_Tenant_Structure_Code");

            entity.HasIndex(e => new { e.StructureId, e.IsActive })
                .HasDatabaseName("IX_OrgLevel_Structure_Active");

            entity.HasOne(e => e.OrganizationStructure)
                .WithMany(e => e.Levels)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.OrganizationUnits)
                .WithOne(e => e.OrganizationLevel)
                .HasForeignKey(e => e.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // ORGANIZATION UNIT
        // ============================================================================
        builder.Entity<OrganizationUnit>(entity =>
        {
            // Self-referencing relationship for hierarchy
            entity.HasOne(e => e.ParentUnit)
                .WithMany(e => e.ChildUnits)
                .HasForeignKey(e => e.ParentUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OrganizationLevel)
                .WithMany(e => e.OrganizationUnits)
                .HasForeignKey(e => e.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.HeadEmployee)
                .WithMany()
                .HasForeignKey(e => e.HeadEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Employees)
                .WithOne(e => e.OrganizationUnit)
                .HasForeignKey(e => e.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Positions)
                .WithOne(p => p.OrganizationUnit)
                .HasForeignKey(p => p.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Teams)
                .WithOne(t => t.OrganizationUnit)
                .HasForeignKey(t => t.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_OrgUnit_Tenant_Code");

            entity.HasIndex(e => e.ParentUnitId)
                .HasDatabaseName("IX_OrgUnit_ParentId");

            entity.HasIndex(e => e.OrganizationLevelId)
                .HasDatabaseName("IX_OrgUnit_LevelId");

            entity.HasIndex(e => e.HeadEmployeeId)
                .HasDatabaseName("IX_OrgUnit_HeadEmployeeId");

            entity.HasIndex(e => new { e.IsActive })
                .HasDatabaseName("IX_OrgUnit_Active");

            entity.HasIndex(e => new { e.ParentUnitId, e.Sequence })
                .HasDatabaseName("IX_OrgUnit_Parent_Sequence");

            entity.HasIndex(e => new { e.OrganizationLevelId })
                .HasDatabaseName("IX_OrgUnit_Level");
        });

        // ============================================================================
        // ORGANIZATION UNIT HISTORY
        // ============================================================================
        builder.Entity<OrganizationUnitHistory>(entity =>
        {
            entity.ToTable("OrganizationUnitHistories");

            entity.HasOne(e => e.OrganizationUnit)
                .WithMany()
                .HasForeignKey(e => e.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict); // Keep history even if unit is deleted

            // Indexes
            entity.HasIndex(e => e.OrganizationUnitId)
                .HasDatabaseName("IX_OrgUnitHistory_UnitId");

            entity.HasIndex(e => new { e.OrganizationUnitId, e.EffectiveFrom })
                .HasDatabaseName("IX_OrgUnitHistory_Unit_EffectiveFrom");

            entity.HasIndex(e => new { e.TenantId, e.EffectiveFrom })
                .HasDatabaseName("IX_OrgUnitHistory_Tenant_EffectiveFrom");
        });

        // ============================================================================
        // TEAMS
        // ============================================================================
        builder.Entity<Team>(entity =>
        {
            // Filtered, and the filter is the whole point. `DeleteAsync` on this store is a SOFT
            // delete, so without `WHERE IsDeleted = 0` a dissolved team keeps its code for ever:
            // the service's own duplicate check reads through the soft-delete filter, sees nothing,
            // approves the write, and SQL then rejects it with an opaque 500. That is exactly D-9
            // (the reliever priority) and D-10 (the reissued associate number) from slice 0 — the
            // same trap, third occurrence in this bundle. Whenever a store soft-deletes, every
            // uniqueness claim over it is wrong until it is filtered.
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_Team_Tenant_Code");

            entity.HasIndex(e => e.OrganizationUnitId)
                .HasDatabaseName("IX_Team_OrganizationUnitId");

            entity.HasIndex(e => e.TeamLeadId)
                .HasDatabaseName("IX_Team_TeamLeadId");

            entity.HasIndex(e => e.ParentTeamId)
                .HasDatabaseName("IX_Team_ParentTeamId");

            entity.HasIndex(e => e.LocationId)
                .HasDatabaseName("IX_Team_LocationId");

            entity.HasIndex(e => e.ShiftId)
                .HasDatabaseName("IX_Team_ShiftId");

            entity.HasIndex(e => new { e.TenantId, e.Status })
                .HasDatabaseName("IX_Team_Tenant_Status");

            entity.HasIndex(e => new { e.OrganizationUnitId, e.Sequence })
                .HasDatabaseName("IX_Team_OrgUnit_Sequence");

            entity.HasOne(e => e.TeamLead)
                .WithMany(e => e.LedTeams)
                .HasForeignKey(e => e.TeamLeadId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParentTeam)
                .WithMany(e => e.ChildTeams)
                .HasForeignKey(e => e.ParentTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Location)
                .WithMany()
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Shift)
                .WithMany()
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Members)
                .WithOne(m => m.Team)
                .HasForeignKey(m => m.TeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TeamMember>(entity =>
        {
            entity.HasIndex(e => e.TeamId)
                .HasDatabaseName("IX_TeamMember_TeamId");

            entity.HasIndex(e => e.EmployeeId)
                .HasDatabaseName("IX_TeamMember_EmployeeId");

            entity.HasIndex(e => new { e.TeamId, e.EmployeeId })
                .HasDatabaseName("IX_TeamMember_Team_Employee");

            entity.HasIndex(e => new { e.EmployeeId, e.IsPrimary })
                .HasDatabaseName("IX_TeamMember_Employee_Primary");

            entity.HasIndex(e => new { e.TeamId, e.IsActive })
                .HasDatabaseName("IX_TeamMember_Team_Active");

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.TeamMemberships)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TeamMemberHistory>(entity =>
        {
            entity.ToTable("TeamMemberHistories");

            entity.HasOne(e => e.Team)
                .WithMany()
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.TeamId)
                .HasDatabaseName("IX_TeamMemberHistory_TeamId");

            entity.HasIndex(e => e.EmployeeId)
                .HasDatabaseName("IX_TeamMemberHistory_EmployeeId");

            entity.HasIndex(e => new { e.TeamId, e.EmployeeId, e.EffectiveFrom })
                .HasDatabaseName("IX_TeamMemberHistory_Team_Employee_EffectiveFrom");
        });

        // ============================================================================
        // LOCATION STRUCTURE
        // ============================================================================
        builder.Entity<LocationStructure>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_LocStructure_Tenant_Code");

            entity.HasIndex(e => new { e.TenantId, e.IsDefault })
                .HasDatabaseName("IX_LocStructure_Tenant_Default");

            entity.HasMany(e => e.LocationLevels)
                .WithOne(e => e.Structure)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Locations)
                .WithOne(e => e.Structure)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // LOCATION LEVEL
        // ============================================================================
        builder.Entity<LocationLevel>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.LevelNumber })
                .IsUnique()
                .HasDatabaseName("IX_LocLevel_Tenant_Structure_LevelNum");

            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_LocLevel_Tenant_Structure_Code");

            entity.HasOne(e => e.Structure)
                .WithMany(e => e.LocationLevels)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Locations)
                .WithOne(e => e.LocationLevel)
                .HasForeignKey(e => e.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // LOCATION
        // ============================================================================
        builder.Entity<Location>(entity =>
        {
            // Self-referencing relationship
            entity.HasOne(e => e.ParentLocation)
                .WithMany(e => e.ChildLocations)
                .HasForeignKey(e => e.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Structure
            entity.HasOne(e => e.Structure)
                .WithMany(e => e.Locations)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with LocationLevel
            entity.HasOne(e => e.LocationLevel)
                .WithMany(e => e.Locations)
                .HasForeignKey(e => e.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Country
            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Employees collection
            entity.HasMany(e => e.Employees)
                .WithOne(e => e.Location)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Contacts collection
            entity.HasMany(e => e.LocationContacts)
                .WithOne(e => e.Location)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Cascade); // Delete contacts when location is deleted

            // Indexes
            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_Location_Tenant_Structure_Code");

            entity.HasIndex(e => e.ParentLocationId)
                .HasDatabaseName("IX_Location_ParentId");

            entity.HasIndex(e => e.LocationLevelId)
                .HasDatabaseName("IX_Location_LevelId");

            entity.HasIndex(e => e.CountryId)
                .HasDatabaseName("IX_Location_CountryId");

            entity.HasIndex(e => new { e.StructureId, e.IsActive })
                .HasDatabaseName("IX_Location_Structure_Active");

            entity.HasIndex(e => new { e.ParentLocationId, e.Sequence })
                .HasDatabaseName("IX_Location_Parent_Sequence");
        });

        // ============================================================================
        // LOCATION CONTACT
        // ============================================================================
        builder.Entity<LocationContact>(entity =>
        {
            entity.HasOne(e => e.Location)
                .WithMany(e => e.LocationContacts)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.LocationId)
                .HasDatabaseName("IX_LocContact_LocationId");

            entity.HasIndex(e => new { e.LocationId, e.IsPrimary })
                .HasDatabaseName("IX_LocContact_Location_Primary");

            entity.HasIndex(e => e.EmployeeId)
                .HasDatabaseName("IX_LocContact_EmployeeId");
        });

        // ============================================================================
        // SALARY GRADE / LEVEL / NOTCH
        // ============================================================================
        builder.Entity<SalaryGrade>(entity =>
        {
            entity.HasMany(e => e.Levels)
                .WithOne(e => e.Grade)
                .HasForeignKey(e => e.SalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_SalaryGrade_Tenant_Code");

            entity.HasIndex(e => new { e.TenantId, e.IsActive })
                .HasDatabaseName("IX_SalaryGrade_Tenant_Active");

            entity.HasIndex(e => new { e.TenantId, e.EffectiveDate })
                .HasDatabaseName("IX_SalaryGrade_Tenant_EffectiveDate");
        });

        builder.Entity<SalaryLevel>(entity =>
        {
            entity.HasOne(e => e.Grade)
                .WithMany(e => e.Levels)
                .HasForeignKey(e => e.SalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Notches)
                .WithOne(e => e.Level)
                .HasForeignKey(e => e.SalaryLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.SalaryGradeId)
                .HasDatabaseName("IX_SalaryLevel_GradeId");

            entity.HasIndex(e => new { e.TenantId, e.SalaryGradeId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_SalaryLevel_Tenant_Grade_Code");

            entity.HasIndex(e => new { e.TenantId, e.SalaryGradeId, e.Sequence })
                .IsUnique()
                .HasDatabaseName("IX_SalaryLevel_Tenant_Grade_Sequence");

            entity.HasIndex(e => new { e.TenantId, e.IsActive })
                .HasDatabaseName("IX_SalaryLevel_Tenant_Active");
        });

        builder.Entity<SalaryNotch>(entity =>
        {
            entity.HasOne(e => e.Level)
                .WithMany(e => e.Notches)
                .HasForeignKey(e => e.SalaryLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.SalaryLevelId)
                .HasDatabaseName("IX_SalaryNotch_LevelId");

            entity.HasIndex(e => new { e.TenantId, e.SalaryLevelId, e.NotchNumber })
                .IsUnique()
                .HasDatabaseName("IX_SalaryNotch_Tenant_Level_NotchNumber");

            entity.HasIndex(e => new { e.TenantId, e.IsActive })
                .HasDatabaseName("IX_SalaryNotch_Tenant_Active");
        });

        // Configure Employee entity
        builder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber })
                .IsUnique()
                .HasDatabaseName("IX_Employee_Tenant_EmployeeNumber");

            entity.HasIndex(e => new { e.TenantId, e.EmailAddress })
                .IsUnique()
                .HasDatabaseName("IX_Employee_Tenant_EmailAddress");

            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.SectionId);
            entity.HasIndex(e => e.PositionId);
            entity.HasIndex(e => e.OrganizationLevelId);
            entity.HasIndex(e => e.OrganizationUnitId);
            entity.HasIndex(e => e.LocationLevelId);
            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.CountryId);
            entity.HasIndex(e => e.StaffStatus);
            entity.HasIndex(e => e.ManagerId);
            entity.HasIndex(e => e.DateEmployed);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Section)
                .WithMany(s => s.Employees)
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Position)
                .WithMany(p => p.Employees)
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Country)
                .WithMany(c => c.Employees)
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.LocationLevel)
                .WithMany()
                .HasForeignKey(e => e.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // Location relationship is configured from Location side (lines 2131-2135)
            // Removed duplicate configuration here to avoid ambiguity

            entity.HasOne(e => e.OrganizationLevel)
                .WithMany()
                .HasForeignKey(e => e.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // OrganizationUnit relationship is configured from OrganizationUnit side (lines 1999-2002)
            // Removed duplicate configuration here to avoid ambiguity

            // Self-referencing relationship for Manager/DirectReports
            entity.HasOne(e => e.Manager)
                .WithMany(m => m.DirectReports)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.NoAction); // Prevent cascading deletes
        });

        builder.Entity<EmployeeContact>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => new { e.EmployeeId, e.IsPrimary });
            entity.HasIndex(e => e.ContactType);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Contacts)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeEmergencyContact>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => new { e.EmployeeId, e.IsPrimary });
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.EmergencyContacts)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeDependent>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.Relationship);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Dependents)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.EmployeeDependentBenefits)
                .WithOne(e => e.EmployeeDependent)
                .HasForeignKey(e => e.EmployeeDependentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeDependentBenefit>(entity =>
        {
            entity.HasIndex(e => e.EmployeeDependentId);
            entity.HasIndex(e => e.PolicyId);
            entity.HasIndex(e => e.EnrolledDate);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.EmployeeDependent)
                .WithMany(d => d.EmployeeDependentBenefits)
                .HasForeignKey(e => e.EmployeeDependentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.BenefitPolicy)
                .WithMany()
                .HasForeignKey(e => e.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.EnrollmentId);

            entity.HasOne(e => e.Enrollment)
                .WithMany(en => en.Dependents)
                .HasForeignKey(e => e.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeQualification>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.QualificationId);
            entity.HasIndex(e => e.CountryId);
            entity.HasIndex(e => e.IsVerified);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Qualifications)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Qualification)
                .WithMany()
                .HasForeignKey(e => e.QualificationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdentificationType>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique()
                .HasDatabaseName("IX_IdentificationType_Tenant_Name");

            entity.HasIndex(e => new { e.TenantId, e.Code })
                .HasDatabaseName("IX_IdentificationType_Tenant_Code");

            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.IssuingCountry)
                .WithMany()
                .HasForeignKey(e => e.IssuingCountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeIdentificationCard>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.IdentificationTypeId);
            entity.HasIndex(e => e.DocumentNumber);
            entity.HasIndex(e => e.IsVerified);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.IdentificationCards)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.IdentificationType)
                .WithMany(t => t.EmployeeIdentificationCards)
                .HasForeignKey(e => e.IdentificationTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeWorkHistory>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.StartDate);
            entity.HasIndex(e => e.EndDate);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.WorkHistories)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeContractDetail>(entity =>
        {
            entity.Property(e => e.EmploymentType).HasConversion<int>();
            entity.Property(e => e.PayFrequency).HasConversion<int>();
            entity.Property(e => e.TaxTreatmentType).HasConversion<int>();
            entity.Property(e => e.ContractStatus).HasConversion<int>();

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => new { e.TenantId, e.ContractNumber })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeContractDetail_Tenant_ContractNumber");
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.ContractDetails)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExpatriateAssignment>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.HomeCountryId);
            entity.HasIndex(e => new { e.EmployeeId, e.StartDate });

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.ExpatriateAssignments)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.HomeCountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Department entity
        builder.Entity<Department>(entity =>
        {
            entity.HasIndex(d => d.Code).IsUnique();
            entity.HasIndex(d => d.Name);
            entity.HasIndex(d => d.DepartmentHeadId);
            entity.HasIndex(d => d.IsActive);

            // Department head relationship
            entity.HasOne(d => d.DepartmentHead)
                .WithMany()
                .HasForeignKey(d => d.DepartmentHeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Section entity
        builder.Entity<Section>(entity =>
        {
            entity.HasIndex(s => s.Code);
            entity.HasIndex(s => s.DepartmentId);
            entity.HasIndex(s => s.SectionHeadId);
            entity.HasIndex(s => s.IsActive);

            // Department relationship
            entity.HasOne(s => s.Department)
                .WithMany(d => d.Sections)
                .HasForeignKey(s => s.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Section head relationship
            entity.HasOne(s => s.SectionHead)
                .WithMany()
                .HasForeignKey(s => s.SectionHeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Section entity
        builder.Entity<Unit>(entity =>
        {
            entity.HasIndex(u => u.Name);
            entity.HasIndex(u => u.Code);
            entity.HasIndex(u => u.UnitHeadId);
            entity.HasIndex(u => u.IsActive);

            // Section relationship
            entity.HasOne(u => u.Section)
                .WithMany(s => s.Units)
                .HasForeignKey(u => u.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unit head relationship
            entity.HasOne(u => u.UnitHead)
                .WithMany()
                .HasForeignKey(u => u.UnitHeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure StaffLevel entity
        builder.Entity<StaffLevel>(entity =>
        {
            entity.Property(sl => sl.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(sl => sl.Code)
                .HasMaxLength(50);

            entity.Property(sl => sl.Description)
                .HasMaxLength(1000);

            entity.Property(sl => sl.Rank)
                .HasDefaultValue(1);

            entity.Property(sl => sl.IsActive)
                .HasDefaultValue(true);

            // Indexes (avoid unique constraints since Code defaults to empty string)
            entity.HasIndex(sl => new { sl.TenantId, sl.Name })
                .HasDatabaseName("IX_StaffLevel_Tenant_Name");

            entity.HasIndex(sl => new { sl.TenantId, sl.Code })
                .HasDatabaseName("IX_StaffLevel_Tenant_Code");

            entity.HasIndex(sl => new { sl.TenantId, sl.Rank })
                .HasDatabaseName("IX_StaffLevel_Tenant_Rank");

            entity.HasIndex(sl => new { sl.TenantId, sl.IsActive })
                .HasDatabaseName("IX_StaffLevel_Tenant_Active");

            // Relationship: StaffLevel (1) -> EmployeePosition (many)
            entity.HasMany(sl => sl.EmployeePositions)
                .WithOne(ep => ep.StaffLevel)
                .HasForeignKey(ep => ep.StaffLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure EmployeePosition entity
        builder.Entity<EmployeePosition>(entity =>
        {
            entity.Property(ep => ep.Title)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(ep => ep.Code)
                .HasMaxLength(20);

            entity.Property(ep => ep.Description)
                .HasMaxLength(1000);

            entity.Property(ep => ep.IsActive)
                .HasDefaultValue(true);

            // Indexes (avoid unique constraints since Code defaults to empty string)
            entity.HasIndex(ep => new { ep.TenantId, ep.Title })
                .HasDatabaseName("IX_EmployeePosition_Tenant_Title");

            entity.HasIndex(ep => new { ep.TenantId, ep.Code })
                .HasDatabaseName("IX_EmployeePosition_Tenant_Code");

            entity.HasIndex(ep => new { ep.TenantId, ep.OrganizationUnitId })
                .HasDatabaseName("IX_EmployeePosition_Tenant_OrgUnit");

            entity.HasIndex(ep => new { ep.TenantId, ep.IsActive })
                .HasDatabaseName("IX_EmployeePosition_Tenant_Active");

            entity.HasIndex(ep => ep.StaffLevelId)
                .HasDatabaseName("IX_EmployeePosition_StaffLevelId");

            entity.HasIndex(ep => ep.ReportsToPositionId)
                .HasDatabaseName("IX_EmployeePosition_ReportsToPositionId");

            // Relationships
            entity.HasOne(ep => ep.OrganizationUnit)
                .WithMany(ou => ou.Positions)
                .HasForeignKey(ep => ep.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ep => ep.ReportsToPosition)
                .WithMany()
                .HasForeignKey(ep => ep.ReportsToPositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Skill entity
        builder.Entity<Skill>(entity =>
        {
            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(s => s.Description)
                .HasMaxLength(1000);

            entity.Property(s => s.Category)
                .HasMaxLength(100);

            entity.Property(s => s.IsActive)
                .HasDefaultValue(true);

            entity.Property(s => s.RequiresCertification)
                .HasDefaultValue(false);

            entity.HasIndex(s => new { s.TenantId, s.Name })
                .IsUnique()
                .HasDatabaseName("IX_Skill_Tenant_Name");

            entity.HasIndex(s => new { s.TenantId, s.Category })
                .HasDatabaseName("IX_Skill_Tenant_Category");

            entity.HasIndex(s => new { s.TenantId, s.IsActive })
                .HasDatabaseName("IX_Skill_Tenant_Active");
        });

        // Configure PositionSkillRequirement entity
        builder.Entity<PositionSkillRequirement>(entity =>
        {
            entity.Property(psr => psr.RequiredLevel)
                .HasConversion<int>();

            entity.Property(psr => psr.IsRequired)
                .HasDefaultValue(true);

            entity.Property(psr => psr.Priority)
                .HasDefaultValue(1);

            entity.HasIndex(psr => new { psr.TenantId, psr.PositionId, psr.SkillId })
                .IsUnique()
                .HasDatabaseName("IX_PositionSkillRequirement_Tenant_Position_Skill");

            entity.HasIndex(psr => new { psr.TenantId, psr.PositionId })
                .HasDatabaseName("IX_PositionSkillRequirement_Tenant_PositionId");

            entity.HasIndex(psr => new { psr.TenantId, psr.SkillId })
                .HasDatabaseName("IX_PositionSkillRequirement_Tenant_SkillId");

            entity.HasOne(psr => psr.Position)
                .WithMany(p => p.SkillRequirements)
                .HasForeignKey(psr => psr.PositionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(psr => psr.Skill)
                .WithMany(s => s.PositionRequirements)
                .HasForeignKey(psr => psr.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BenefitPolicy entity
        builder.Entity<BenefitPolicy>(entity =>
        {
            entity.Property(bp => bp.PolicyName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(bp => bp.PolicyCode)
                .HasMaxLength(50);

            entity.Property(bp => bp.Description)
                .HasMaxLength(1000);

            entity.Property(bp => bp.PolicyType)
                .HasConversion<int>()
                .HasDefaultValue(BenefitPolicyType.Medical);

            entity.Property(bp => bp.Recipient)
                .HasConversion<int>()
                .HasDefaultValue(BenefitRecipient.Staff);

            entity.Property(bp => bp.LimitPeriod)
                .HasConversion<int>()
                .HasDefaultValue(BenefitLimitPeriod.Annual);

            entity.Property(bp => bp.IsActive)
                .HasDefaultValue(true);

            entity.HasIndex(bp => new { bp.TenantId, bp.PolicyName })
                .HasDatabaseName("IX_BenefitPolicy_Tenant_Name");

            entity.HasIndex(bp => new { bp.TenantId, bp.PolicyCode })
                .IsUnique()
                .HasDatabaseName("IX_BenefitPolicy_Tenant_Code");

            entity.HasIndex(bp => new { bp.TenantId, bp.PolicyType })
                .HasDatabaseName("IX_BenefitPolicy_Tenant_Type");

            entity.HasIndex(bp => new { bp.TenantId, bp.IsActive })
                .HasDatabaseName("IX_BenefitPolicy_Tenant_Active");

            entity.HasIndex(bp => new { bp.TenantId, bp.EffectiveFrom })
                .HasDatabaseName("IX_BenefitPolicy_Tenant_EffectiveFrom");

            entity.HasMany(bp => bp.BenefitPolicyRelations)
                .WithOne(r => r.BenefitPolicy)
                .HasForeignKey(r => r.BenefitPolicyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(bp => bp.PositionBenefits)
                .WithOne(pb => pb.BenefitPolicy)
                .HasForeignKey(pb => pb.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Enterprise enhancement fields
            entity.Property(bp => bp.Currency).HasMaxLength(3).HasDefaultValue("GHS");
            entity.Property(bp => bp.DeliveryType).HasConversion<int>().HasDefaultValue(BenefitDeliveryType.Cash);
            entity.Property(bp => bp.Frequency).HasConversion<int>().HasDefaultValue(PayFrequency.Monthly);
            entity.Property(bp => bp.CalculationBasis).HasConversion<int>().HasDefaultValue(BenefitCalculationBasis.FixedAmount);
            entity.Property(bp => bp.TaxTreatment).HasConversion<int>().HasDefaultValue(BenefitTaxTreatment.FullyTaxable);
            entity.Property(bp => bp.ValuationMethod).HasConversion<int>().HasDefaultValue(BenefitValuationMethod.FlatRate);
            entity.Property(bp => bp.ContributionResponsibility).HasConversion<int>().HasDefaultValue(BenefitContributionResponsibility.EmployerPaysAll);
            entity.Property(bp => bp.IsTaxable).HasDefaultValue(true);
            entity.Property(bp => bp.AffectsGrossPay).HasDefaultValue(true);
            entity.Property(bp => bp.AffectsNetPay).HasDefaultValue(true);
            entity.Property(bp => bp.AvailableDuringProbation).HasDefaultValue(true);

            entity.HasOne(bp => bp.PayComponent)
                .WithMany()
                .HasForeignKey(bp => bp.PayComponentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(bp => bp.GradeValues)
                .WithOne(gv => gv.BenefitPolicy)
                .HasForeignKey(gv => gv.BenefitPolicyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(bp => bp.Enrollments)
                .WithOne(en => en.BenefitPolicy)
                .HasForeignKey(en => en.BenefitPolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BenefitGradeValue entity
        builder.Entity<BenefitGradeValue>(entity =>
        {
            entity.Property(gv => gv.IsActive).HasDefaultValue(true);

            entity.HasIndex(gv => new { gv.TenantId, gv.BenefitPolicyId })
                .HasDatabaseName("IX_BenefitGradeValue_Tenant_Policy");

            entity.HasOne(gv => gv.SalaryGrade)
                .WithMany()
                .HasForeignKey(gv => gv.SalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(gv => gv.StaffLevel)
                .WithMany()
                .HasForeignKey(gv => gv.StaffLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure EmployeeBenefitEnrollment entity
        builder.Entity<EmployeeBenefitEnrollment>(entity =>
        {
            entity.Property(en => en.Status).HasConversion<int>().HasDefaultValue(EmployeeBenefitEnrollmentStatus.Draft);
            entity.Property(en => en.Source).HasConversion<int>().HasDefaultValue(BenefitEnrollmentSource.Manual);
            entity.Property(en => en.Currency).HasMaxLength(3).HasDefaultValue("GHS");
            entity.Property(en => en.TerminationReason).HasMaxLength(500);
            entity.Property(en => en.Notes).HasMaxLength(1000);

            entity.HasIndex(en => new { en.TenantId, en.EmployeeId, en.BenefitPolicyId })
                .HasDatabaseName("IX_EmployeeBenefitEnrollment_Tenant_Employee_Policy");
            entity.HasIndex(en => new { en.TenantId, en.Status })
                .HasDatabaseName("IX_EmployeeBenefitEnrollment_Tenant_Status");

            entity.HasOne(en => en.Employee)
                .WithMany()
                .HasForeignKey(en => en.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(en => en.SourcePositionBenefit)
                .WithMany()
                .HasForeignKey(en => en.SourcePositionBenefitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure BenefitBeneficiary entity
        builder.Entity<BenefitBeneficiary>(entity =>
        {
            entity.Property(b => b.FullName).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Relationship).HasConversion<int>();
            entity.Property(b => b.PhoneNumber).HasMaxLength(50);
            entity.Property(b => b.IsActive).HasDefaultValue(true);

            entity.HasIndex(b => new { b.TenantId, b.EnrollmentId })
                .HasDatabaseName("IX_BenefitBeneficiary_Tenant_Enrollment");

            entity.HasOne(b => b.Enrollment)
                .WithMany(en => en.Beneficiaries)
                .HasForeignKey(b => b.EnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.EmployeeDependent)
                .WithMany()
                .HasForeignKey(b => b.EmployeeDependentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BenefitUtilization entity
        builder.Entity<BenefitUtilization>(entity =>
        {
            entity.Property(u => u.Type).HasConversion<int>().HasDefaultValue(BenefitUtilizationType.Expense);
            entity.Property(u => u.Status).HasConversion<int>().HasDefaultValue(BenefitClaimStatus.Pending);
            entity.Property(u => u.Amount).HasColumnType("decimal(18,2)");
            entity.Property(u => u.Description).HasMaxLength(1000);
            entity.Property(u => u.ReferenceNumber).HasMaxLength(100);
            entity.Property(u => u.RejectionReason).HasMaxLength(500);

            entity.HasIndex(u => new { u.TenantId, u.EnrollmentId })
                .HasDatabaseName("IX_BenefitUtilization_Tenant_Enrollment");
            entity.HasIndex(u => new { u.TenantId, u.Status })
                .HasDatabaseName("IX_BenefitUtilization_Tenant_Status");

            entity.HasOne(u => u.Enrollment)
                .WithMany(en => en.Utilizations)
                .HasForeignKey(u => u.EnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(u => u.EmployeeDependent)
                .WithMany()
                .HasForeignKey(u => u.EmployeeDependentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BenefitPolicyRelation entity
        builder.Entity<BenefitPolicyRelation>(entity =>
        {
            entity.Property(r => r.RelationType)
                .HasConversion<int>();

            entity.Property(r => r.IsActive)
                .HasDefaultValue(true);

            entity.HasIndex(r => new { r.TenantId, r.BenefitPolicyId })
                .HasDatabaseName("IX_BenefitPolicyRelation_Tenant_PolicyId");

            entity.HasIndex(r => new { r.TenantId, r.BenefitPolicyId, r.RelationType })
                .IsUnique()
                .HasDatabaseName("IX_BenefitPolicyRelation_Tenant_Policy_RelationType");
        });

        // Configure EmployeePositionBenefit entity
        builder.Entity<EmployeePositionBenefit>(entity =>
        {
            entity.HasIndex(pb => new { pb.TenantId, pb.PositionId, pb.PolicyId })
                .IsUnique()
                .HasDatabaseName("IX_EmployeePositionBenefit_Tenant_Position_Policy");

            entity.HasOne(pb => pb.Position)
                .WithMany(p => p.PositionBenefits)
                .HasForeignKey(pb => pb.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pb => pb.BenefitPolicy)
                .WithMany(bp => bp.PositionBenefits)
                .HasForeignKey(pb => pb.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ===== Emoluments / Pay Components (Phase 4) =====

        // Configure PayComponent entity
        builder.Entity<PayComponent>(entity =>
        {
            entity.Property(p => p.Code).IsRequired().HasMaxLength(50);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(150);
            entity.Property(p => p.Description).HasMaxLength(1000);
            entity.Property(p => p.ComponentType).HasConversion<int>();
            entity.Property(p => p.CalculationBasis).HasConversion<int>();
            entity.Property(p => p.IsTaxable).HasDefaultValue(true);
            entity.Property(p => p.IsActive).HasDefaultValue(true);
            entity.Property(p => p.AffectsGrossPay).HasDefaultValue(true);
            entity.Property(p => p.StatutoryTreatment).HasConversion<int>().HasDefaultValue(TaxTreatmentType.PAYE);

            entity.HasIndex(p => new { p.TenantId, p.Code })
                .IsUnique()
                .HasDatabaseName("IX_PayComponent_Tenant_Code");
            entity.HasIndex(p => new { p.TenantId, p.ComponentType })
                .HasDatabaseName("IX_PayComponent_Tenant_Type");
            entity.HasIndex(p => new { p.TenantId, p.IsActive })
                .HasDatabaseName("IX_PayComponent_Tenant_Active");

            entity.HasMany(p => p.PositionAssignments)
                .WithOne(pp => pp.PayComponent)
                .HasForeignKey(pp => pp.PayComponentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.EmployeeAssignments)
                .WithOne(ep => ep.PayComponent)
                .HasForeignKey(ep => ep.PayComponentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure PositionPayComponent entity
        builder.Entity<PositionPayComponent>(entity =>
        {
            entity.Property(pp => pp.IsActive).HasDefaultValue(true);

            entity.HasIndex(pp => new { pp.TenantId, pp.PositionId, pp.PayComponentId })
                .IsUnique()
                .HasDatabaseName("IX_PositionPayComponent_Tenant_Position_Component");

            entity.HasOne(pp => pp.Position)
                .WithMany()
                .HasForeignKey(pp => pp.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure EmployeePayComponent entity
        builder.Entity<EmployeePayComponent>(entity =>
        {
            entity.Property(ep => ep.IsActive).HasDefaultValue(true);

            entity.HasIndex(ep => new { ep.TenantId, ep.EmployeeId, ep.PayComponentId })
                .HasDatabaseName("IX_EmployeePayComponent_Tenant_Employee_Component");

            entity.HasOne(ep => ep.Employee)
                .WithMany()
                .HasForeignKey(ep => ep.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure LeaveTypeAllowance entity
        builder.Entity<LeaveTypeAllowance>(entity =>
        {
            entity.HasIndex(la => new { la.TenantId, la.LeaveTypeId, la.PayComponentId })
                .IsUnique()
                .HasDatabaseName("IX_LeaveTypeAllowance_Tenant_LeaveType_Component");

            entity.HasOne(la => la.LeaveType)
                .WithMany(lt => lt.LeaveTypeAllowances)
                .HasForeignKey(la => la.LeaveTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(la => la.PayComponent)
                .WithMany()
                .HasForeignKey(la => la.PayComponentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Division entity
        builder.Entity<Division>(entity =>
        {
            entity.HasIndex(d => d.Name);
            entity.HasIndex(d => d.Code);
            entity.HasIndex(d => d.DivisionHeadId);
            entity.HasIndex(d => d.IsActive);

            // Division head relationship
            entity.HasOne(d => d.DivisionHead)
                .WithMany()
                .HasForeignKey(d => d.DivisionHeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Position History entity
        builder.Entity<EmployeePositionHistory>(entity =>
        {
            entity.Property(h => h.ChangeReason).HasConversion<int>();

            entity.HasIndex(h => h.EmployeeId);
            entity.HasIndex(h => h.PositionId);
            entity.HasIndex(h => h.StartDate);
            entity.HasIndex(h => h.EndDate);
            entity.HasIndex(h => new { h.EmployeeId, h.StartDate });

            entity.HasOne(h => h.Employee)
                .WithMany(e => e.PositionHistories)
                .HasForeignKey(h => h.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.LocationLevel)
                .WithMany()
                .HasForeignKey(h => h.LocationLevelId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Location)
                .WithMany()
                .HasForeignKey(h => h.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.OrganizationLevel)
                .WithMany()
                .HasForeignKey(h => h.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.OrganizationUnit)
                .WithMany()
                .HasForeignKey(h => h.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Position)
                .WithMany(p => p.PositionHistories)
                .HasForeignKey(h => h.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeSalaryAssignment>(entity =>
        {
            entity.Property(e => e.AssignmentReason)
                .HasMaxLength(200);

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.GradeId);
            entity.HasIndex(e => e.LevelId);
            entity.HasIndex(e => e.NotchId);
            entity.HasIndex(e => e.EffectiveDate);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.SalaryAssignments)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Level)
                .WithMany()
                .HasForeignKey(e => e.LevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Notch)
                .WithMany()
                .HasForeignKey(e => e.NotchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeReferee>(entity =>
        {
            entity.Property(e => e.RefereeType).HasConversion<int>();

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.RefereeType);
            entity.HasIndex(e => new { e.EmployeeId, e.IsPrimary });
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Referees)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeGuarantor>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.CountryId);
            entity.HasIndex(e => e.VerifiedByEmployeeId);
            entity.HasIndex(e => e.IsPrimary);
            entity.HasIndex(e => e.IsVerified);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Guarantors)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.VerifiedByEmployee)
                .WithMany()
                .HasForeignKey(e => e.VerifiedByEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeBank>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeBank_Tenant_Code");

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeBankBranch>(entity =>
        {
            entity.HasIndex(e => e.BankId);
            entity.HasIndex(e => new { e.TenantId, e.BankId, e.Code })
                .HasDatabaseName("IX_EmployeeBankBranch_Tenant_EmployeeBank_Code");

            entity.HasOne(e => e.Bank)
                .WithMany(b => b.Branches)
                .HasForeignKey(e => e.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeBankDetail>(entity =>
        {
            entity.Property(e => e.AccountType).HasConversion<int>();

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.AccountNumber })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeBankDetail_Tenant_Employee_AccountNumber");

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.BankDetails)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Bank)
                .WithMany()
                .HasForeignKey(e => e.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeSkill>(entity =>
        {
            entity.Property(e => e.SkillLevel).HasConversion<int>();

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.SkillId);
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.SkillId })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeSkill_Tenant_Employee_Skill");

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Skills)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Skill)
                .WithMany(s => s.EmployeeSkills)
                .HasForeignKey(e => e.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // COMPETENCY
        // ============================================================================
        builder.Entity<Competency>(entity =>
        {
            entity.Property(c => c.CompetencyCategory).HasConversion<int>();

            // ⚠ Filtered on IsDeleted, and it must stay that way: DeleteAsync is a soft delete,
            // so an unfiltered unique index means a deleted competency's code can never be used
            // again. See migration 20260819020000_FilterCompetencyUniqueIndexesOnIsDeleted.
            entity.HasIndex(c => new { c.TenantId, c.Code })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_Competency_Tenant_Code");

            entity.HasIndex(c => new { c.TenantId, c.Name })
                .HasDatabaseName("IX_Competency_Tenant_Name");

            entity.HasIndex(c => new { c.TenantId, c.CompetencyCategory })
                .HasDatabaseName("IX_Competency_Tenant_Category");

            entity.HasIndex(c => new { c.TenantId, c.IsActive })
                .HasDatabaseName("IX_Competency_Tenant_Active");
        });

        // ============================================================================
        // COMPETENCY SKILL INDICATOR
        // ============================================================================
        builder.Entity<CompetencySkillIndicator>(entity =>
        {
            entity.Property(csi => csi.MinimumSkillLevelRequired).HasConversion<int>();

            entity.HasIndex(csi => new { csi.TenantId, csi.CompetencyId, csi.SkillId })
                .IsUnique()
                .HasDatabaseName("IX_CompetencySkillIndicator_Tenant_Competency_Skill");

            entity.HasIndex(csi => csi.CompetencyId)
                .HasDatabaseName("IX_CompetencySkillIndicator_CompetencyId");

            entity.HasIndex(csi => csi.SkillId)
                .HasDatabaseName("IX_CompetencySkillIndicator_SkillId");

            entity.HasOne(csi => csi.Competency)
                .WithMany(c => c.SkillIndicators)
                .HasForeignKey(csi => csi.CompetencyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(csi => csi.Skill)
                .WithMany(s => s.CompetencyIndicators)
                .HasForeignKey(csi => csi.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // POSITION COMPETENCY
        // ============================================================================
        builder.Entity<PositionCompetency>(entity =>
        {
            // ⚠ Filtered on IsDeleted. Beyond the general rule, BulkReplaceForPositionAsync
            // soft-deletes the current set and inserts the new one in a single SaveChanges, which
            // an unfiltered unique index rejects mid-transaction.
            entity.HasIndex(pc => new { pc.TenantId, pc.PositionId, pc.CompetencyId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_PositionCompetency_Tenant_Position_Competency");

            entity.HasIndex(pc => pc.PositionId)
                .HasDatabaseName("IX_PositionCompetency_PositionId");

            entity.HasIndex(pc => pc.CompetencyId)
                .HasDatabaseName("IX_PositionCompetency_CompetencyId");

            entity.HasOne(pc => pc.Position)
                .WithMany()
                .HasForeignKey(pc => pc.PositionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pc => pc.Competency)
                .WithMany(c => c.PositionCompetencies)
                .HasForeignKey(pc => pc.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // EMPLOYEE COMPETENCY
        // ============================================================================
        builder.Entity<EmployeeCompetency>(entity =>
        {
            // ⚠ Filtered on IsDeleted: unfiltered, deleting an assessment meant that employee
            // could never be re-assessed on that competency.
            entity.HasIndex(ec => new { ec.TenantId, ec.EmployeeId, ec.CompetencyId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_EmployeeCompetency_Tenant_Employee_Competency");

            entity.HasIndex(ec => ec.EmployeeId)
                .HasDatabaseName("IX_EmployeeCompetency_EmployeeId");

            entity.HasIndex(ec => ec.CompetencyId)
                .HasDatabaseName("IX_EmployeeCompetency_CompetencyId");

            entity.HasIndex(ec => ec.AssessmentDate)
                .HasDatabaseName("IX_EmployeeCompetency_AssessmentDate");

            entity.HasOne(ec => ec.Employee)
                .WithMany()
                .HasForeignKey(ec => ec.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ec => ec.Competency)
                .WithMany(c => c.EmployeeCompetencies)
                .HasForeignKey(ec => ec.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ec => ec.AssessedBy)
                .WithMany()
                .HasForeignKey(ec => ec.AssessedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ============================================================================
        // EMPLOYEE COMPETENCY HISTORY
        // ============================================================================
        builder.Entity<EmployeeCompetencyHistory>(entity =>
        {
            entity.HasIndex(ech => ech.EmployeeCompetencyId)
                .HasDatabaseName("IX_EmployeeCompetencyHistory_CompetencyId");

            entity.HasIndex(ech => ech.AssessmentDate)
                .HasDatabaseName("IX_EmployeeCompetencyHistory_AssessmentDate");

            entity.HasIndex(ech => ech.RecordedAt)
                .HasDatabaseName("IX_EmployeeCompetencyHistory_RecordedAt");

            entity.HasOne(ech => ech.EmployeeCompetency)
                .WithMany(ec => ec.History)
                .HasForeignKey(ech => ech.EmployeeCompetencyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ech => ech.AssessedBy)
                .WithMany()
                .HasForeignKey(ech => ech.AssessedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ech => ech.RecordedBy)
                .WithMany()
                .HasForeignKey(ech => ech.RecordedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Leave Balance entity
        builder.Entity<LeaveBalance>(entity =>
        {
            entity.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.LeaveSubTypeId, x.Year }).IsUnique();
            entity.HasIndex(x => x.Year);

            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.LeaveBalances)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LeaveSubType)
                .WithMany(x => x.LeaveBalances)
                .HasForeignKey(x => x.LeaveSubTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany(e => e.LeaveBalances)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Adjustments)
                .WithOne(a => a.LeaveBalance)
                .HasForeignKey(a => a.LeaveBalanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Leave Adjustment entity
        builder.Entity<LeaveAdjustment>(entity =>
        {
            entity.HasIndex(x => x.LeaveBalanceId);
            entity.HasIndex(x => new { x.EmployeeId, x.Year });

            entity.Property(x => x.Days).HasColumnType("decimal(5,2)");

            entity.HasOne(x => x.LeaveBalance)
                .WithMany(lb => lb.Adjustments)
                .HasForeignKey(x => x.LeaveBalanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.PerformedByEmployee)
                .WithMany()
                .HasForeignKey(x => x.PerformedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReasonCode)
                .WithMany()
                .HasForeignKey(x => x.ReasonCodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Reason Code lookup (system-wide)
        builder.Entity<ReasonCode>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Category });
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        // Configure pre-defined Employee Reliever setup
        builder.Entity<EmployeeReliever>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.EmployeeId });

            // One reliever slot per (employee, priority) — among LIVE rows.
            //
            // ⚠ The filter is the fix for D-9, the fourth occurrence of one trap in the areas 19-23
            // bundle. EmployeeRelievers soft-deletes, so without it a removed reliever held its
            // priority slot for ever: EmployeeRelieverService's own duplicate check reads live rows,
            // sees nothing, approves the write, and SQL then rejects it. Measured 2026-08-22 —
            // deleting the priority-1 row and re-creating it answered **500**, with no body, in
            // place of the sentence the service was written to give.
            entity.HasIndex(x => new { x.EmployeeId, x.Priority })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RelieverEmployee)
                .WithMany()
                .HasForeignKey(x => x.RelieverEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Request entity
        builder.Entity<LeaveRequest>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.LeaveTypeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.StartDate, x.EndDate });
            // Guards against duplicate request numbers under concurrent creation; the service
            // retries on the resulting unique-constraint violation. Scoped per tenant.
            entity.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();

            // Relationship with Employee
            entity.HasOne(x => x.Employee)
                .WithMany(x => x.LeaveRequests)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Reliever
            entity.HasOne(x => x.RelieverEmployee)
                .WithMany()
                .HasForeignKey(x => x.RelieverEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Second Reliever
            entity.HasOne(x => x.SecondRelieverEmployee)
                .WithMany()
                .HasForeignKey(x => x.SecondRelieverEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Leave Type
            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.LeaveRequests)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Leave SubType
            entity.HasOne(x => x.LeaveSubType)
                .WithMany(x => x.LeaveRequests)
                .HasForeignKey(x => x.LeaveSubTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Leave Plan
            entity.HasOne(x => x.LeavePlan)
                .WithMany()
                .HasForeignKey(x => x.LeavePlanId)
                .OnDelete(DeleteBehavior.Restrict);

            // Encashment (one-to-one)
            entity.HasOne(x => x.Encashment)
                .WithOne(x => x.LeaveRequest)
                .HasForeignKey<LeaveEncashment>(x => x.LeaveRequestId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Plan entity
        builder.Entity<LeavePlan>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.LeaveTypeId);
            entity.HasIndex(x => x.PlannedBy);
            entity.HasIndex(x => new { x.EmployeeId, x.Year });

            entity.HasOne(x => x.Employee)
                .WithMany(e => e.LeavePlans)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LeaveType)
                .WithMany()
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LeaveSubType)
                .WithMany(x => x.LeavePlans)
                .HasForeignKey(x => x.LeaveSubTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RelieverEmployee)
                .WithMany()
                .HasForeignKey(x => x.RelieverId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SecondRelieverEmployee)
                .WithMany()
                .HasForeignKey(x => x.SecondRelieverId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PlannedByEmployee)
                .WithMany()
                .HasForeignKey(x => x.PlannedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Sub Type entity
        builder.Entity<LeaveSubType>(entity =>
        {
            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.LeaveSubTypes)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Category Allocation entity
        builder.Entity<LeaveCategoryAllocation>(entity =>
        {
            entity.HasIndex(x => new { x.LeaveTypeId, x.LeaveSubTypeId, x.StaffLevelId, x.EffectiveFrom });

            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.LeaveCategoryAllocations)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LeaveSubType)
                .WithMany(x => x.LeaveCategoryAllocations)
                .HasForeignKey(x => x.LeaveSubTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.StaffLevel)
                .WithMany()
                .HasForeignKey(x => x.StaffLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Type Eligibility entity
        builder.Entity<LeaveTypeEligibility>(entity =>
        {
            entity.HasIndex(x => x.LeaveTypeId);

            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.EligibilityRules)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Accrual Policy entity
        builder.Entity<LeaveAccrualPolicy>(entity =>
        {
            entity.HasIndex(x => x.LeaveTypeId);

            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.AccrualPolicies)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Request Attachment entity
        builder.Entity<LeaveRequestAttachment>(entity =>
        {
            entity.HasIndex(x => x.LeaveRequestId);

            entity.HasOne(x => x.LeaveRequest)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.LeaveRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.UploadedByEmployee)
                .WithMany()
                .HasForeignKey(x => x.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Encashment entity
        builder.Entity<LeaveEncashment>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Year);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ProcessedByEmployee)
                .WithMany()
                .HasForeignKey(x => x.ProcessedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Public Holiday entity
        builder.Entity<PublicHoliday>(entity =>
        {
            entity.HasIndex(x => x.DateFrom);
            entity.HasIndex(x => x.DateTo);
        });

        builder.Entity<AppraisalGradeDefinition>(entity =>
        {
            entity.HasIndex(x => x.GradeName);
        });

        builder.Entity<AppraisalCompetency>(entity =>
        {
            entity.HasIndex(x => x.CriteriaName);
            entity.HasIndex(x => x.Code);
        });

        builder.Entity<KpiDefinition>(entity =>
        {
            entity.HasIndex(x => x.KpiName);
            entity.HasIndex(x => x.MeasurementType);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.MeasurementType).HasConversion<int>();

            entity.HasMany(x => x.GoalsUsingThisKpi)
                .WithOne(x => x.KpiDefinition)
                .HasForeignKey(x => x.KpiDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TemplateItemGradeRange>(entity =>
        {
            entity.HasIndex(x => x.AppraisalTemplateItemId);
            entity.HasIndex(x => x.GradeDefinitionId);

            entity.HasOne(x => x.TemplateItem)
                .WithMany(x => x.GradeRanges)
                .HasForeignKey(x => x.AppraisalTemplateItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.GradeDefinition)
                .WithMany(x => x.TemplateItemGradeRanges)
                .HasForeignKey(x => x.GradeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PerformanceAppraisal>(entity =>
        {
            entity.HasIndex(x => x.AppraisalNumber).IsUnique(false);
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.HasAppeal);
            entity.HasIndex(x => x.CurrentAppealStatus);
            entity.HasIndex(x => x.AppraisalTemplateId);
            entity.HasIndex(x => x.CalibrationSessionId);
            entity.HasIndex(x => x.IsCalibrated);

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.CurrentAppealStatus).HasConversion<int>();

            entity.HasOne(x => x.AppraisalCycle)
                .WithMany(x => x.PerformanceAppraisals)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany(x => x.PerformanceAppraisals)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Template)
                .WithMany()
                .HasForeignKey(x => x.AppraisalTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DevelopmentPlan)
                .WithMany()
                .HasForeignKey(x => x.DevelopmentPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CalibrationSession)
                .WithMany(x => x.CalibratedAppraisals)
                .HasForeignKey(x => x.CalibrationSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OverallGrade)
                .WithMany()
                .HasForeignKey(x => x.OverallGradeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EvaluatorEvaluations)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EmployeeResponses)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CustomQuestionResponses)
                .WithOne(x => x.PerformanceAppraisal)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.PerformanceAppraisal)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.PeerNominations)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Appeals)
                .WithOne(x => x.PerformanceAppraisal)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Goals)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Conversations)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.HRReviews)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EvaluationSnapshots)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ReviewEvents)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CriterionConfigs)
                .WithOne(x => x.PerformanceAppraisal)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PerformanceAppraisalCriterionConfig>(entity =>
        {
            entity.HasIndex(x => x.PerformanceAppraisalId);
            entity.HasIndex(x => x.TemplateItemId);

            entity.Property(x => x.KpiTargetSource).HasConversion<int>().IsRequired(false);

            entity.HasOne(x => x.PerformanceAppraisal)
                .WithMany(x => x.CriterionConfigs)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.TemplateItem)
                .WithMany()
                .HasForeignKey(x => x.TemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.GradeRanges)
                .WithOne(x => x.CriterionConfig)
                .HasForeignKey(x => x.PerformanceAppraisalCriterionConfigId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PerformanceAppraisalCriterionConfigGradeRange>(entity =>
        {
            entity.HasIndex(x => x.PerformanceAppraisalCriterionConfigId);
            entity.HasIndex(x => x.GradeDefinitionId);

            entity.HasOne(x => x.CriterionConfig)
                .WithMany(x => x.GradeRanges)
                .HasForeignKey(x => x.PerformanceAppraisalCriterionConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.GradeDefinition)
                .WithMany()
                .HasForeignKey(x => x.GradeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaluatorEvaluation>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.EvaluatorId);
            entity.HasIndex(x => new { x.AppraisalId, x.EvaluatorId }).IsUnique(false);

            entity.Property(x => x.EvaluatorRole).HasConversion<int>();

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.EvaluatorEvaluations)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Evaluator)
                .WithMany()
                .HasForeignKey(x => x.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CriterionScores)
                .WithOne(x => x.EvaluatorEvaluation)
                .HasForeignKey(x => x.EvaluatorEvaluationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CriterionScore>(entity =>
        {
            entity.HasIndex(x => x.EvaluatorEvaluationId);
            entity.HasIndex(x => x.TemplateItemId);
            entity.HasIndex(x => x.GradeDefinitionId);

            entity.HasOne(x => x.EvaluatorEvaluation)
                .WithMany(x => x.CriterionScores)
                .HasForeignKey(x => x.EvaluatorEvaluationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TemplateItem)
                .WithMany()
                .HasForeignKey(x => x.TemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.GradeDefinition)
                .WithMany()
                .HasForeignKey(x => x.GradeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalEmployeeResponse>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.TemplateItemId);
            entity.HasIndex(x => x.ResponseStatus);

            entity.Property(x => x.ResponseStatus).HasConversion<int>();

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.EmployeeResponses)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TemplateItem)
                .WithMany()
                .HasForeignKey(x => x.TemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalCustomQuestionResponse>(entity =>
        {
            entity.HasIndex(x => x.PerformanceAppraisalId);
            entity.HasIndex(x => x.TemplateItemId);
            entity.HasIndex(new[] { "PerformanceAppraisalId", "TemplateItemId" }).IsUnique();

            entity.HasOne(x => x.PerformanceAppraisal)
                .WithMany(x => x.CustomQuestionResponses)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.TemplateItem)
                .WithMany()
                .HasForeignKey(x => x.TemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalAttachment>(entity =>
        {
            entity.HasIndex(x => x.UploadedById);
            entity.HasIndex(x => x.EntityType);
            entity.HasIndex(x => x.PerformanceAppraisalId);
            entity.HasIndex(x => x.EmployeeGoalId);
            entity.HasIndex(x => x.UnitGoalId);
            entity.HasIndex(x => x.CheckInId);
            entity.HasIndex(x => x.PipId);
            entity.HasIndex(x => x.AppealId);
            entity.HasIndex(x => x.CalibrationSessionId);
            entity.HasIndex(x => x.ReviewEventId);

            entity.Property(x => x.EntityType).HasConversion<int>();

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PerformanceAppraisal)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EmployeeGoal)
                .WithMany()
                .HasForeignKey(x => x.EmployeeGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UnitGoal)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.UnitGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CheckIn)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.CheckInId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Pip)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.PipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Appeal)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CalibrationSession)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.CalibrationSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewEvent)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.ReviewEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PerformanceImprovementPlan>(entity =>
        {
            entity.HasIndex(x => x.PipNumber).IsUnique(false);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.SupervisorId);
            entity.HasIndex(x => x.HROwnerId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Outcome).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Appraisal)
                .WithMany()
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HROwner)
                .WithMany()
                .HasForeignKey(x => x.HROwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.PipGoals)
                .WithOne(x => x.Pip)
                .HasForeignKey(x => x.PipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ReviewMeetings)
                .WithOne(x => x.Pip)
                .HasForeignKey(x => x.PipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Pip)
                .HasForeignKey(x => x.PipId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PipReviewMeeting>(entity =>
        {
            entity.HasIndex(x => x.PipId);
            entity.HasIndex(x => x.ConductedById);
            entity.HasIndex(x => x.MeetingDate);

            entity.HasOne(x => x.Pip)
                .WithMany(x => x.ReviewMeetings)
                .HasForeignKey(x => x.PipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConductedBy)
                .WithMany()
                .HasForeignKey(x => x.ConductedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalSettings>(entity =>
        {
            entity.HasIndex(x => x.SettingsName);
            entity.HasIndex(x => x.RequireSelfEvaluation);
            entity.HasIndex(x => x.RequirePeerReviews);
            entity.HasIndex(x => x.RequireManagerEvaluation);
            entity.HasIndex(x => x.RequireHRReview);

            entity.Property(x => x.PeerNominationMode).HasConversion<int>();
            entity.Property(x => x.PeerEvaluationOpenMode).HasConversion<int>();
            entity.Property(x => x.HRReviewTiming).HasConversion<int>();
            entity.Property(x => x.ReviewFrequency).HasConversion<int>();
            entity.Property(x => x.InterimReviewDepth).HasConversion<int>();

            entity.HasMany(x => x.AppraisalCycles)
                .WithOne(x => x.AppraisalSettings)
                .HasForeignKey(x => x.AppraisalSettingsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalCycle>(entity =>
        {
            entity.HasIndex(x => x.CycleCode);
            entity.HasIndex(x => x.CycleName);
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.AppraisalType);
            entity.HasIndex(x => x.StartDate);
            entity.HasIndex(x => x.EndDate);
            entity.HasIndex(x => x.AppraisalSettingsId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.AppraisalType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.AppraisalSettings)
                .WithMany(x => x.AppraisalCycles)
                .HasForeignKey(x => x.AppraisalSettingsId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OpenedBy)
                .WithMany()
                .HasForeignKey(x => x.OpenedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ClosedBy)
                .WithMany()
                .HasForeignKey(x => x.ClosedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.PerformanceAppraisals)
                .WithOne(x => x.AppraisalCycle)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.AppraisalTargets)
                .WithOne(x => x.AppraisalCycle)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.TemplateAssignments)
                .WithOne(x => x.AppraisalCycle)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CalibrationSessions)
                .WithOne(x => x.AppraisalCycle)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CompanyGoals)
                .WithOne(x => x.AppraisalCycle)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ReviewEvents)
                .WithOne(x => x.Cycle)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalCycleTarget>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.TargetType);
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.TargetType).HasConversion<int>();

            entity.HasOne(x => x.AppraisalCycle)
                .WithMany(x => x.AppraisalTargets)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Exclusions)
                .WithOne(x => x.Target)
                .HasForeignKey(x => x.AppraisalCycleTargetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PeerNomination>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.PeerEmployeeId);
            // One active nomination per (appraisal, peer) — closes the find-or-create race on concurrent
            // nominations. Filtered so soft-deleted rows don't block re-nominating the same peer later.
            entity.HasIndex(x => new { x.AppraisalId, x.PeerEmployeeId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.NominatedById);
            entity.HasIndex(x => x.NominationDate);
            entity.HasIndex(x => x.NominationStatus);
            entity.HasIndex(x => x.ApprovedByManagerId);

            entity.Property(x => x.NominationStatus).HasConversion<int>();

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.PeerNominations)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PeerEmployee)
                .WithMany()
                .HasForeignKey(x => x.PeerEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NominatedBy)
                .WithMany()
                .HasForeignKey(x => x.NominatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedByManager)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalAppeal>(entity =>
        {
            entity.HasIndex(x => x.PerformanceAppraisalId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.SubmittedDate);
            entity.HasIndex(x => x.ReviewedById);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.PerformanceAppraisal)
                .WithMany(x => x.Appeals)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Reviewer)
                .WithMany()
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Items)
                .WithOne(x => x.AppraisalAppeal)
                .HasForeignKey(x => x.AppraisalAppealId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Appeal)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalAppealItem>(entity =>
        {
            entity.HasIndex(x => x.AppraisalAppealId);
            entity.HasIndex(x => x.TemplateItemId);

            entity.HasOne(x => x.AppraisalAppeal)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.AppraisalAppealId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TemplateItem)
                .WithMany()
                .HasForeignKey(x => x.TemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // APPRAISAL TEMPLATES
        // =====================================================

        builder.Entity<AppraisalTemplate>(entity =>
        {
            entity.HasIndex(x => x.TemplateName);
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.ApprovalStatus);

            entity.Property(x => x.ApprovalStatus).HasConversion<int>();

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Sections)
                .WithOne(x => x.AppraisalTemplate)
                .HasForeignKey(x => x.AppraisalTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CycleAssignments)
                .WithOne(x => x.AppraisalTemplate)
                .HasForeignKey(x => x.AppraisalTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalTemplateSection>(entity =>
        {
            entity.HasIndex(x => x.AppraisalTemplateId);
            entity.HasIndex(x => x.DisplayOrder);

            entity.HasOne(x => x.AppraisalTemplate)
                .WithMany(x => x.Sections)
                .HasForeignKey(x => x.AppraisalTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.TemplateItems)
                .WithOne(x => x.Section)
                .HasForeignKey(x => x.AppraisalTemplateSectionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalTemplateItem>(entity =>
        {
            entity.HasIndex(x => x.AppraisalTemplateSectionId);
            entity.HasIndex(x => x.CompetencyId);
            entity.HasIndex(x => x.KpiDefinitionId);
            entity.HasIndex(x => x.DisplayOrder);

            entity.HasOne(x => x.Section)
                .WithMany(x => x.TemplateItems)
                .HasForeignKey(x => x.AppraisalTemplateSectionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Competency)
                .WithMany()
                .HasForeignKey(x => x.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KpiDefinition)
                .WithMany()
                .HasForeignKey(x => x.KpiDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalCycleTemplate>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.AppraisalTemplateId);
            entity.HasIndex(x => x.Priority);
            entity.HasIndex(x => x.IsActive);

            entity.HasOne(x => x.AppraisalCycle)
                .WithMany(x => x.TemplateAssignments)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AppraisalTemplate)
                .WithMany(x => x.CycleAssignments)
                .HasForeignKey(x => x.AppraisalTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalCycleTargetExclusion>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleTargetId);
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.IsActive);

            entity.HasOne(x => x.Target)
                .WithMany(x => x.Exclusions)
                .HasForeignKey(x => x.AppraisalCycleTargetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // GOALS
        // =====================================================

        builder.Entity<GoalLibrary>(entity =>
        {
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.IsActive);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EmployeeGoals)
                .WithOne(x => x.LibraryItem)
                .HasForeignKey(x => x.GoalLibraryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StrategicGoal>(entity =>
        {
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.StartYear);
            entity.HasIndex(x => x.Priority);

            entity.Property(x => x.Priority).HasConversion<int>();

            entity.HasMany(x => x.CompanyGoals)
                .WithOne(x => x.StrategicGoal)
                .HasForeignKey(x => x.StrategicGoalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CompanyGoal>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.StrategicGoalId);
            entity.HasIndex(x => x.Priority);
            entity.HasIndex(x => x.IsVisible);

            entity.Property(x => x.Priority).HasConversion<int>();

            entity.HasOne(x => x.AppraisalCycle)
                .WithMany(x => x.CompanyGoals)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.StrategicGoal)
                .WithMany(x => x.CompanyGoals)
                .HasForeignKey(x => x.StrategicGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.UnitGoals)
                .WithOne(x => x.ParentCompanyGoal)
                .HasForeignKey(x => x.ParentCompanyGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EmployeeGoals)
                .WithOne(x => x.ParentCompanyGoal)
                .HasForeignKey(x => x.CompanyGoalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UnitGoal>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.ParentCompanyGoalId);
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.CreatedByManagerId);
            entity.HasIndex(x => x.Priority);

            entity.Property(x => x.Priority).HasConversion<int>();

            entity.HasOne(x => x.AppraisalCycle)
                .WithMany()
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ParentUnitGoalId);

            entity.HasOne(x => x.ParentCompanyGoal)
                .WithMany(x => x.UnitGoals)
                .HasForeignKey(x => x.ParentCompanyGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ParentUnitGoal)
                .WithMany(x => x.ChildUnitGoals)
                .HasForeignKey(x => x.ParentUnitGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByManager)
                .WithMany()
                .HasForeignKey(x => x.CreatedByManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EmployeeGoals)
                .WithOne(x => x.ParentUnitGoal)
                .HasForeignKey(x => x.UnitGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.UnitGoal)
                .HasForeignKey(x => x.UnitGoalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeGoal>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.PerformanceAppraisalId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Priority);
            entity.HasIndex(x => x.GoalLibraryId);
            entity.HasIndex(x => x.KpiDefinitionId);
            entity.HasIndex(x => x.IsLocked);
            entity.HasIndex(x => x.SubmittedToManagerId);
            entity.HasIndex(x => x.CompanyGoalId);
            entity.HasIndex(x => x.UnitGoalId);
            entity.HasIndex(x => x.ParentGoalId);
            // Risk pre-filter columns — queried together in GetAtRiskGoalsAsync WHERE clause
            entity.HasIndex(x => x.DueDate)
                  .HasDatabaseName("IX_EmployeeGoal_DueDate");
            entity.HasIndex(x => x.ProgressPercent)
                  .HasDatabaseName("IX_EmployeeGoal_ProgressPercent");

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.MeasurementType).HasConversion<int>();
            entity.Property(x => x.ParentType).HasConversion<int>();
            entity.Property(x => x.Period).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AppraisalCycle)
                .WithMany()
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.Goals)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ParentCompanyGoal)
                .WithMany(x => x.EmployeeGoals)
                .HasForeignKey(x => x.CompanyGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ParentUnitGoal)
                .WithMany(x => x.EmployeeGoals)
                .HasForeignKey(x => x.UnitGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ParentGoal)
                .WithMany()
                .HasForeignKey(x => x.ParentGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LibraryItem)
                .WithMany(x => x.EmployeeGoals)
                .HasForeignKey(x => x.GoalLibraryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KpiDefinition)
                .WithMany(x => x.GoalsUsingThisKpi)
                .HasForeignKey(x => x.KpiDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Manager)
                .WithMany()
                .HasForeignKey(x => x.SubmittedToManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ProgressEntries)
                .WithOne(x => x.EmployeeGoal)
                .HasForeignKey(x => x.EmployeeGoalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoalProgressEntry>(entity =>
        {
            entity.HasIndex(x => x.EmployeeGoalId);
            entity.HasIndex(x => x.RecordedById);
            entity.HasIndex(x => x.EntryDate);
            entity.HasIndex(x => x.ReviewEventId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.EmployeeGoal)
                .WithMany(x => x.ProgressEntries)
                .HasForeignKey(x => x.EmployeeGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RecordedBy)
                .WithMany()
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewEvent)
                .WithMany(x => x.ProgressEntries)
                .HasForeignKey(x => x.ReviewEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // GOAL RISK SETTINGS
        // =====================================================

        builder.Entity<GoalRiskSetting>(entity =>
        {
            entity.Property(x => x.DaysRemainingThreshold).IsRequired();
            entity.Property(x => x.MinimumProgressPercent).IsRequired();
            entity.Property(x => x.ExpectedProgressTolerancePercent).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();

            // Queried by provider on every risk evaluation request.
            entity.HasIndex(x => x.IsActive)
                  .HasDatabaseName("IX_GoalRiskSetting_IsActive");

            // Seed the default active record scoped to the system default tenant.
            // TenantId matches the seeded Tenant record ("DEFAULT", id 00000000-…-0001).
            // Dates are fixed constants so that the migration snapshot is stable;
            // re-running migrations will not produce a new HasData diff.
            entity.HasData(new
            {
                Id                               = new Guid("a1b2c3d4-0000-0000-0000-000000000001"),
                TenantId                         = new Guid("00000000-0000-0000-0000-000000000001"),
                DaysRemainingThreshold           = 14,
                MinimumProgressPercent           = 60,
                ExpectedProgressTolerancePercent = 20,
                IsActive                         = true,
                CreatedAt                        = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt                        = (DateTime?)null,
                CreatedBy                        = "System",
                UpdatedBy                        = (string?)null,
                CreatedById                      = (Guid?)null,
                LastModifiedById                 = (Guid?)null,
                IsDeleted                        = false,
                DeletedAt                        = (DateTime?)null,
                DeletedBy                        = (string?)null
            });
        });

        // =====================================================
        // COMPANY HR POLICY SETTINGS
        // =====================================================

        builder.Entity<CompanyProfile>(entity =>
        {
            entity.Property(x => x.LegalName).HasMaxLength(200);

            // One active company profile per tenant.
            entity.HasIndex(x => x.TenantId)
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0")
                  .HasDatabaseName("UX_CompanyProfile_Tenant");

            // Country FKs (registered/duty country + country of incorporation). Restrict so a
            // referenced Country cannot be deleted out from under a profile, and to avoid the
            // multiple-cascade-paths error from two FKs into the same table.
            entity.HasOne(x => x.Country)
                  .WithMany()
                  .HasForeignKey(x => x.CountryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CountryOfIncorporation)
                  .WithMany()
                  .HasForeignKey(x => x.CountryOfIncorporationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CompanyHrPolicySettings>(entity =>
        {
            entity.Property(x => x.LongServiceMilestoneYears).HasMaxLength(200);
            entity.Property(x => x.DefaultCurrencyCode).HasMaxLength(3);
            entity.Property(x => x.SuccessionPlanNumberPrefix).HasMaxLength(10);

            // One active settings record per tenant.
            entity.HasIndex(x => x.TenantId)
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0")
                  .HasDatabaseName("UX_CompanyHrPolicySettings_Tenant");

            // Seed a default record scoped to the system default tenant ("DEFAULT", id …-0001).
            // Fixed constants keep the migration snapshot stable across re-runs.
            entity.HasData(new
            {
                Id                             = new Guid("b2c3d4e5-0000-0000-0000-000000000001"),
                TenantId                       = new Guid("00000000-0000-0000-0000-000000000001"),
                CompulsoryRetirementAge        = 60,
                VoluntaryRetirementAge         = 55,
                UseGenderSpecificRetirementAge = false,
                MaleRetirementAge              = (int?)null,
                FemaleRetirementAge            = (int?)null,
                DefaultProbationMonths         = 6,
                DefaultResignationNoticeDays   = 30,
                DefaultTerminationNoticeDays   = 30,
                // FR-HR-092's only stated exception to the MD's signature. At or above this many
                // days of unauthorised absence a termination is procedural and HR may approve it;
                // 0 would put every termination back on the MD.
                ProceduralAbsenceDays          = 10,
                VacancyAlertLeadDays           = 90,
                ReviewDueLeadDays              = 30,
                ContractExpiryLeadDays         = 60,
                ProbationEndLeadDays           = 30,
                RetirementCountdownLeadDays    = 365,
                LongServiceMilestoneYears      = "5,10,15,20,25",
                DefaultCurrencyCode            = "GHS",
                FiscalYearStartMonth           = 1,
                MinimumWorkingAge              = 18,
                BudgetEnforcementMode          = ErpSystem.Core.Enums.BudgetEnforcementMode.Warn,
                // ⚠ Stricter than the budget ladder on purpose: this only ever fires for a position
                // whose establishment completed FR-HR-135's chain. See the entity for the reasoning.
                EstablishmentEnforcementMode   = ErpSystem.Core.Enums.BudgetEnforcementMode.Block,
                FitWeightPerformance           = 35,
                FitWeightCompetency            = 30,
                FitWeightPotential             = 20,
                FitWeightTenure                = 15,
                SuccessionPlanNumberPrefix     = "SP",
                CreatedAt                      = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt                      = (DateTime?)null,
                CreatedBy                      = "System",
                UpdatedBy                      = (string?)null,
                CreatedById                    = (Guid?)null,
                LastModifiedById               = (Guid?)null,
                IsDeleted                      = false,
                DeletedAt                      = (DateTime?)null,
                DeletedBy                      = (string?)null
            });
        });

        // =====================================================
        // CHECK-INS & JOURNAL
        // =====================================================

        builder.Entity<CheckIn>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.ConductedById);
            entity.HasIndex(x => x.ScheduledDate);
            entity.HasIndex(x => x.CheckInType);

            entity.Property(x => x.CheckInType).HasConversion<int>();

            entity.HasOne(x => x.Cycle)
                .WithMany()
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConductedBy)
                .WithMany()
                .HasForeignKey(x => x.ConductedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.GoalUpdates)
                .WithOne(x => x.CheckIn)
                .HasForeignKey(x => x.CheckInId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.CheckIn)
                .HasForeignKey(x => x.CheckInId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ObjectiveLinks)
                .WithOne(x => x.CheckIn)
                .HasForeignKey(x => x.CheckInId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AppraisalOutcomeRecommendation>(entity =>
        {
            entity.HasIndex(x => x.PerformanceAppraisalId);
            entity.HasIndex(x => x.RecommendationType);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.RecommendationType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.PerformanceAppraisal)
                .WithMany()
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SalaryReviewProposal>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.SourceAppraisalId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.ProposalType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SourceAppraisal)
                .WithMany()
                .HasForeignKey(x => x.SourceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmploymentActionProposal>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.SourceAppraisalId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.ActionType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SourceAppraisal)
                .WithMany()
                .HasForeignKey(x => x.SourceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CheckInObjectiveLink>(entity =>
        {
            entity.HasIndex(x => x.CheckInId);
            entity.HasIndex(x => x.CompanyGoalId);
            entity.HasIndex(x => new { x.CheckInId, x.CompanyGoalId }).IsUnique();

            entity.HasOne(x => x.CheckIn)
                .WithMany(x => x.ObjectiveLinks)
                .HasForeignKey(x => x.CheckInId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CompanyGoal)
                .WithMany()
                .HasForeignKey(x => x.CompanyGoalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GoalRequiredSkill>(entity =>
        {
            entity.HasIndex(x => x.EmployeeGoalId);
            entity.HasIndex(x => x.CompetencyId);
            entity.HasIndex(x => new { x.EmployeeGoalId, x.CompetencyId }).IsUnique();

            entity.HasOne(x => x.EmployeeGoal)
                .WithMany(x => x.RequiredSkills)
                .HasForeignKey(x => x.EmployeeGoalId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Competency)
                .WithMany()
                .HasForeignKey(x => x.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CheckInGoalUpdate>(entity =>
        {
            entity.HasIndex(x => x.CheckInId);
            entity.HasIndex(x => x.EmployeeGoalId);

            entity.Property(x => x.UpdatedStatus).HasConversion<int>();

            entity.HasOne(x => x.CheckIn)
                .WithMany(x => x.GoalUpdates)
                .HasForeignKey(x => x.CheckInId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EmployeeGoal)
                .WithMany()
                .HasForeignKey(x => x.EmployeeGoalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PerformanceJournalEntry>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.OwnerId);
            entity.HasIndex(x => x.SubjectEmployeeId);
            entity.HasIndex(x => x.RelatedGoalId);
            entity.HasIndex(x => x.EntryDate);
            entity.HasIndex(x => x.IsPrivate);

            entity.HasOne(x => x.AppraisalCycle)
                .WithMany()
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Owner)
                .WithMany()
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SubjectEmployee)
                .WithMany()
                .HasForeignKey(x => x.SubjectEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RelatedGoal)
                .WithMany(x => x.JournalEntries)
                .HasForeignKey(x => x.RelatedGoalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // DEVELOPMENT PLANS
        // =====================================================

        builder.Entity<EmployeeDevelopmentPlan>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.PlanStatus);

            entity.Property(x => x.PlanStatus).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Cycle)
                .WithMany()
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Objectives)
                .WithOne(x => x.Plan)
                .HasForeignKey(x => x.DevelopmentPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeDevelopmentObjective>(entity =>
        {
            entity.HasIndex(x => x.DevelopmentPlanId);
            entity.HasIndex(x => x.ObjectiveStatus);
            entity.HasIndex(x => x.UpdatedInReviewEventId);

            entity.Property(x => x.ObjectiveStatus).HasConversion<int>();

            entity.HasOne(x => x.Plan)
                .WithMany(x => x.Objectives)
                .HasForeignKey(x => x.DevelopmentPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UpdatedInEvent)
                .WithMany()
                .HasForeignKey(x => x.UpdatedInReviewEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // REVIEW EVENTS & CONVERSATIONS
        // =====================================================

        builder.Entity<AppraisalReviewEvent>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.PerformanceAppraisalId);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.EventDate);

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Cycle)
                .WithMany(x => x.ReviewEvents)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.ReviewEvents)
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Conversation)
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UpdatedDevelopmentPlan)
                .WithMany(x => x.ReviewEvents)
                .HasForeignKey(x => x.UpdatedDevelopmentPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.ReviewEvent)
                .HasForeignKey(x => x.ReviewEventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ProgressEntries)
                .WithOne(x => x.ReviewEvent)
                .HasForeignKey(x => x.ReviewEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalConversation>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.ScheduledDate);
            entity.HasIndex(x => x.IsCompleted);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.Conversations)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ScheduledBy)
                .WithMany()
                .HasForeignKey(x => x.ScheduledById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConductedBy)
                .WithMany()
                .HasForeignKey(x => x.ConductedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewEvent)
                .WithMany()
                .HasForeignKey(x => x.ReviewEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // CALIBRATION
        // =====================================================

        builder.Entity<CalibrationSession>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCycleId);
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ScheduledDate);
            entity.HasIndex(x => x.FacilitatedById);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.AppraisalCycle)
                .WithMany(x => x.CalibrationSessions)
                .HasForeignKey(x => x.AppraisalCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FacilitatedBy)
                .WithMany()
                .HasForeignKey(x => x.FacilitatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CompletedBy)
                .WithMany()
                .HasForeignKey(x => x.CompletedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Participants)
                .WithOne(x => x.CalibrationSession)
                .HasForeignKey(x => x.CalibrationSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.RatingAdjustments)
                .WithOne(x => x.CalibrationSession)
                .HasForeignKey(x => x.CalibrationSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CalibratedAppraisals)
                .WithOne(x => x.CalibrationSession)
                .HasForeignKey(x => x.CalibrationSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.CalibrationSession)
                .HasForeignKey(x => x.CalibrationSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CalibrationParticipant>(entity =>
        {
            entity.HasIndex(x => x.CalibrationSessionId);
            entity.HasIndex(x => x.EmployeeId);

            entity.HasOne(x => x.CalibrationSession)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.CalibrationSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CalibrationRatingAdjustment>(entity =>
        {
            entity.HasIndex(x => x.CalibrationSessionId);
            entity.HasIndex(x => x.PerformanceAppraisalId);
            entity.HasIndex(x => x.TemplateItemId);
            entity.HasIndex(x => x.AdjustedById);
            entity.HasIndex(x => x.AdjustmentDate);

            entity.HasOne(x => x.CalibrationSession)
                .WithMany(x => x.RatingAdjustments)
                .HasForeignKey(x => x.CalibrationSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PerformanceAppraisal)
                .WithMany()
                .HasForeignKey(x => x.PerformanceAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TemplateItem)
                .WithMany()
                .HasForeignKey(x => x.TemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AdjustedBy)
                .WithMany()
                .HasForeignKey(x => x.AdjustedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR REVIEW & PIP GOALS
        // =====================================================

        builder.Entity<AppraisalHRReview>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.ReviewedByHRId);
            entity.HasIndex(x => x.IsApproved);

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.HRReviews)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewedByHR)
                .WithMany()
                .HasForeignKey(x => x.ReviewedByHRId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PipGoal>(entity =>
        {
            entity.HasIndex(x => x.PipId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.DueDate);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Pip)
                .WithMany(x => x.PipGoals)
                .HasForeignKey(x => x.PipId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // APPRAISAL SNAPSHOTS
        // =====================================================

        builder.Entity<AppraisalEvaluationSnapshot>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.EvaluatorId);
            entity.HasIndex(x => x.SnapshotDate);

            entity.Property(x => x.EvaluatorRole).HasConversion<int>();

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.EvaluationSnapshots)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Evaluator)
                .WithMany()
                .HasForeignKey(x => x.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CriterionScores)
                .WithOne(x => x.EvaluationSnapshot)
                .HasForeignKey(x => x.AppraisalEvaluationSnapshotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalCriterionScoreSnapshot>(entity =>
        {
            entity.HasIndex(x => x.AppraisalEvaluationSnapshotId);
            entity.HasIndex(x => x.TemplateItemId);

            entity.Property(x => x.KpiTargetSource).HasConversion<int>().IsRequired(false);

            entity.HasOne(x => x.EvaluationSnapshot)
                .WithMany(x => x.CriterionScores)
                .HasForeignKey(x => x.AppraisalEvaluationSnapshotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TemplateItem)
                .WithMany()
                .HasForeignKey(x => x.TemplateItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.KpiSnapshots)
                .WithOne(x => x.CriterionScoreSnapshot)
                .HasForeignKey(x => x.AppraisalCriterionScoreSnapshotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalKpiEvaluationSnapshot>(entity =>
        {
            entity.HasIndex(x => x.AppraisalCriterionScoreSnapshotId);

            entity.HasOne(x => x.CriterionScoreSnapshot)
                .WithMany(x => x.KpiSnapshots)
                .HasForeignKey(x => x.AppraisalCriterionScoreSnapshotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR COMPANY ASSETS CONFIGURATION
        // =====================================================

        builder.Entity<Core.Entities.HR.Assets.AssetType>(entity =>
        {
            entity.ToTable("HrAssetTypes");
            entity.HasIndex(x => x.Name);

            entity.HasMany(x => x.AssetTypeAttributes)
                .WithOne(x => x.AssetType)
                .HasForeignKey(x => x.AssetTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetTypeAttribute>(entity =>
        {
            entity.HasIndex(x => x.AssetTypeId);
            entity.HasIndex(x => x.AttributeName);
            entity.Property(x => x.DataType).HasConversion<int>();

            entity.HasOne(x => x.AssetType)
                .WithMany(x => x.AssetTypeAttributes)
                .HasForeignKey(x => x.AssetTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CompanyAsset>(entity =>
        {
            entity.HasIndex(x => x.AssetNumber).IsUnique(false);
            entity.HasIndex(x => x.AssetTag);
            entity.HasIndex(x => x.SerialNumber);
            entity.HasIndex(x => x.AssetTypeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Condition);
            entity.HasIndex(x => x.CurrentAssignedToId);
            entity.HasIndex(x => x.LocationId);

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Condition).HasConversion<int>();
            entity.Property(x => x.DisposalMethod).HasConversion<int>();
            entity.Property(x => x.PurchaseCost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.InsuredValue).HasColumnType("decimal(18,2)");
            // AST-9. Indexed because "what do we let to staff" is a register question in its own
            // right, and slice 8's projection filters on it before anything else.
            entity.Property(x => x.StandardRentalAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(x => x.IsRentable);

            // AST-11. The default matters: every asset that existed before this column did was
            // created in HR, so HrCreated is the truthful description of it rather than a
            // placeholder. Without the default those rows would read as source 0 — a value the
            // enum does not define, which no screen could render and no rule could branch on.
            entity.Property(x => x.Source).HasConversion<int>()
                .HasDefaultValue(AssetSource.HrCreated);
            entity.HasIndex(x => x.Source);

            // AST-4. Indexed because the expiry sweep (slice 11) reads exactly this column, and an
            // expiry date nothing ever queries is the kind of field that quietly stays null.
            entity.HasIndex(x => x.InsuranceExpiryDate);

            entity.HasOne(x => x.AssetType)
                .WithMany()
                .HasForeignKey(x => x.AssetTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // The organisation unit an asset sits in. ⚠ This navigation existed on the entity and
            // was mapped into CompanyAssetSummaryDto, but was configured nowhere, absent from the
            // create and update DTOs, and absent from the read DTO — so `unitId` and `unitName` on
            // every list row were permanently null. Defect D-i(b). Configured explicitly here so
            // the pairing cannot fall back to a shadow FK.
            entity.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.UnitId);

            // AST-11 — the link into Finance's register. Nullable, Restrict, and paired explicitly
            // with WithMany() so EF cannot mint a duplicate shadow FK beside it. The direction is
            // new (HR -> Finance) but the shape is not: FixedAsset itself carries exactly this kind
            // of nullable FK into Maintenance.
            entity.HasOne(x => x.FixedAsset)
                .WithMany()
                .HasForeignKey(x => x.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);

            // Not unique, and deliberately so. A unique index would be the obvious choice — one HR
            // entry per fixed asset — but every delete in this area is a SOFT delete, so a deleted
            // link would hold its slot forever and re-linking the same fixed asset would fail with
            // no way for a user to see why. The one-link rule is enforced in the service, where it
            // can read IsDeleted and can explain itself. (Area 13 lost five faces to this exact
            // trap.)
            entity.HasIndex(x => x.FixedAssetId);

            // Slice 9 — the link into the Maintenance module's register, deciding which engine owns
            // this asset's servicing. Same shape as the Finance link above, paired explicitly with
            // WithMany() for the same reason, and non-unique for the same soft-delete reason.
            entity.HasOne(x => x.MaintenanceAsset)
                .WithMany()
                .HasForeignKey(x => x.MaintenanceAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.MaintenanceAssetId);

            entity.HasOne(x => x.CurrentAssignedTo)
                .WithMany()
                .HasForeignKey(x => x.CurrentAssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.AssignmentHistory)
                .WithOne(x => x.Asset)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.MaintenanceRecords)
                .WithOne(x => x.Asset)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.AssetAttributeValues)
                .WithOne(x => x.Asset)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Images)
                .WithOne(x => x.Asset)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Asset)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetAttributeValue>(entity =>
        {
            entity.HasIndex(x => x.AssetId);
            entity.HasIndex(x => x.AssetTypeAttributeId);

            entity.HasOne(x => x.Asset)
                .WithMany(x => x.AssetAttributeValues)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssetTypeAttribute)
                .WithMany()
                .HasForeignKey(x => x.AssetTypeAttributeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetImage>(entity =>
        {
            entity.HasIndex(x => x.AssetId);

            entity.HasOne(x => x.Asset)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetAssignment>(entity =>
        {
            entity.HasIndex(x => x.AssignmentNumber);
            entity.HasIndex(x => x.AssetId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.AssignmentDate);

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Purpose).HasConversion<int>();
            entity.Property(x => x.ConditionAtAssignment).HasConversion<int>();
            entity.Property(x => x.ConditionAtReturn).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.RepairCost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ReplacementCost).HasColumnType("decimal(18,2)");
            // Slice 8 — AST-10. Declared to payroll, deducted by payroll.
            entity.Property(x => x.RentalAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.BenefitInKindValue).HasColumnType("decimal(18,2)");
            entity.Property(x => x.RentalFrequency).HasConversion<int>();
            entity.HasIndex(x => x.RentalEffectiveFrom);

            entity.HasOne(x => x.Asset)
                .WithMany(x => x.AssignmentHistory)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReturnedTo)
                .WithMany()
                .HasForeignKey(x => x.ReturnedToId)
                .OnDelete(DeleteBehavior.Restrict);

            // D-e. Paired explicitly with the requisition's collection so EF cannot mint a shadow
            // FK beside it — the trap this module has already been bitten by.
            entity.HasOne(x => x.Requisition)
                .WithMany(x => x.FulfilledAssignments)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.RequisitionId);

            // Slice 4. Configured EXPLICITLY rather than left to convention, for the same reason
            // the requisition link above is: an unpaired navigation is how this module ended up
            // with duplicate shadow FK columns before. `WithMany()` with no inverse is deliberate —
            // a transfer produces at most one assignment and does not need a collection to say so.
            entity.HasOne(x => x.Transfer)
                .WithMany()
                .HasForeignKey(x => x.TransferId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.TransferId);

            // AST-5b. Explicit for the same reason as every other employee link on this entity.
            entity.HasOne(x => x.TermsDocumentSentBy)
                .WithMany()
                .HasForeignKey(x => x.TermsDocumentSentById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Area 16 slice 7 — AST-3, the surcharge. Every employee link is configured EXPLICITLY,
        // for the reason recorded on AssetAssignment above: an unpaired navigation is how this
        // module ended up with duplicate shadow FK columns before.
        builder.Entity<AssetSurcharge>(entity =>
        {
            entity.HasIndex(x => x.SurchargeNumber);
            entity.HasIndex(x => x.AssignmentId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Reason).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.EmployeeResponse).HasConversion<int>();
            entity.Property(x => x.RecoveryMethod).HasConversion<int>();
            entity.Property(x => x.AssessedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.AmountRecovered).HasColumnType("decimal(18,2)");
            entity.Property(x => x.BasisRepairCost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.BasisReplacementCost).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Assignment)
                .WithMany()
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RaisedBy)
                .WithMany()
                .HasForeignKey(x => x.RaisedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.WaivedBy)
                .WithMany()
                .HasForeignKey(x => x.WaivedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetSurchargeRecovery>(entity =>
        {
            entity.HasIndex(x => x.SurchargeId);
            entity.HasIndex(x => x.RecoveredOn);

            entity.Property(x => x.Method).HasConversion<int>();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");

            // Paired with the collection on purpose, so EF cannot mint a shadow FK beside it.
            entity.HasOne(x => x.Surcharge)
                .WithMany(x => x.Recoveries)
                .HasForeignKey(x => x.SurchargeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RecordedBy)
                .WithMany()
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetMaintenance>(entity =>
        {
            entity.HasIndex(x => x.MaintenanceNumber);
            entity.HasIndex(x => x.AssetId);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.MaintenanceDate);

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Asset)
                .WithMany(x => x.MaintenanceRecords)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PerformedBy)
                .WithMany()
                .HasForeignKey(x => x.PerformedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Core.Entities.HR.Assets.AssetAttachment>(entity =>
        {
            entity.HasIndex(x => x.AssetId);

            entity.HasOne(x => x.Asset)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetRequisition>(entity =>
        {
            entity.HasIndex(x => x.RequisitionNumber);
            entity.HasIndex(x => x.RequestedById);
            entity.HasIndex(x => x.AssetTypeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.RequestDate);

            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.RequestedBy)
                .WithMany()
                .HasForeignKey(x => x.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssetType)
                .WithMany()
                .HasForeignKey(x => x.AssetTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FulfilledBy)
                .WithMany()
                .HasForeignKey(x => x.FulfilledById)
                .OnDelete(DeleteBehavior.Restrict);

            // AST-6b. Indexed because "what has been requested for this employee" is a question the
            // employee's own screen asks, and it is not the same question as "what did they raise".
            entity.HasOne(x => x.BeneficiaryEmployee)
                .WithMany()
                .HasForeignKey(x => x.BeneficiaryEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.BeneficiaryEmployeeId);
        });

        builder.Entity<AssetTransfer>(entity =>
        {
            entity.HasIndex(x => x.TransferNumber);
            entity.HasIndex(x => x.AssetId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.TransferDate);

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Asset)
                .WithMany()
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FromEmployee)
                .WithMany()
                .HasForeignKey(x => x.FromEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FromLocation)
                .WithMany()
                .HasForeignKey(x => x.FromLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToEmployee)
                .WithMany()
                .HasForeignKey(x => x.ToEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToLocation)
                .WithMany()
                .HasForeignKey(x => x.ToLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InitiatedBy)
                .WithMany()
                .HasForeignKey(x => x.InitiatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR AWARDS CONFIGURATION
        // =====================================================

        builder.Entity<AwardType>(entity =>
        {
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.Category).HasConversion<int>();
            entity.Property(x => x.Frequency).HasConversion<int>();
            entity.Property(x => x.MinMonetaryAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.MaxMonetaryAmount).HasColumnType("decimal(18,2)");

            // How a winner is arrived at (area 14, decision D-3). Defaults chosen so that a row
            // written before these columns existed reads as the safest description of itself:
            // open nomination decided by the committee — not a staff vote, which would imply a
            // ballot that never happened.
            entity.Property(x => x.NominationSource).HasConversion<int>()
                .HasDefaultValue(AwardNominationSource.OpenNomination);
            entity.Property(x => x.WinnerDecision).HasConversion<int>()
                .HasDefaultValue(AwardWinnerDecision.CommitteeScore);

            entity.HasIndex(x => x.NominationSource);
            entity.HasIndex(x => x.WinnerDecision);

            // False is both the CLR default and the intended value for every existing row: an award
            // nobody classified should not silently permit self-nomination.
            entity.Property(x => x.AllowSelfNomination).HasDefaultValue(false);
            entity.Property(x => x.DisqualifyOnDisciplinaryRecord).HasDefaultValue(false);

            // A score out of 100 to two places - the same shape the appraisal module stores.
            entity.Property(x => x.MinPerformanceScore).HasColumnType("decimal(5,2)");

            entity.HasMany(x => x.Awards)
                .WithOne(x => x.AwardType)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Levels)
                .WithOne(x => x.AwardType)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Targets)
                .WithOne(x => x.AwardType)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Budgets)
                .WithOne(x => x.AwardType)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Nominations)
                .WithOne(x => x.AwardType)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardCycle>(entity =>
        {
            entity.HasIndex(x => x.AwardTypeId);
            entity.HasIndex(x => x.CycleCode);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.AwardTypeId, x.Year, x.Quarter, x.Month });

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.AwardType)
                .WithMany()
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Nominations)
                .WithOne(x => x.AwardCycle)
                .HasForeignKey(x => x.AwardCycleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LongServiceMilestone>(entity =>
        {
            entity.HasIndex(x => x.AwardTypeId);

            // One rung per number of years per award. Two rows both claiming "20 years" would make
            // the sweep's choice of which to grant arbitrary.
            entity.HasIndex(x => new { x.AwardTypeId, x.Years })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.Property(x => x.MonetaryAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.AwardType)
                .WithMany()
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeAward>(entity =>
        {
            entity.HasIndex(x => x.AwardCycleId);

            entity.HasOne(x => x.AwardCycle)
                .WithMany()
                .HasForeignKey(x => x.AwardCycleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardVote>(entity =>
        {
            entity.HasIndex(x => x.AwardCycleId);
            entity.HasIndex(x => x.AwardNominationId);

            // One ballot per voter per cycle, enforced by the database rather than by a check the
            // service could be refactored past. Filtered so a withdrawn ballot does not block a
            // replacement.
            entity.HasIndex(x => new { x.AwardCycleId, x.VoterId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            entity.HasOne(x => x.AwardCycle)
                .WithMany()
                .HasForeignKey(x => x.AwardCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AwardNomination)
                .WithMany()
                .HasForeignKey(x => x.AwardNominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Voter)
                .WithMany()
                .HasForeignKey(x => x.VoterId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardLevel>(entity =>
        {
            entity.HasIndex(x => x.AwardTypeId);
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.Rank);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.MonetaryAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.AwardType)
                .WithMany(x => x.Levels)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Awards)
                .WithOne(x => x.AwardLevel)
                .HasForeignKey(x => x.AwardLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Nominations)
                .WithOne(x => x.AwardLevel)
                .HasForeignKey(x => x.AwardLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardTypeTarget>(entity =>
        {
            entity.HasIndex(x => x.AwardTypeId);
            entity.HasIndex(x => x.TargetType);
            entity.HasIndex(x => x.TargetId);
            entity.HasIndex(x => new { x.AwardTypeId, x.Purpose });

            entity.Property(x => x.TargetType).HasConversion<int>();

            // Eligibility is the default so every target written before voting existed keeps the
            // meaning it had: it scoped who could win, not who could vote.
            entity.Property(x => x.Purpose).HasConversion<int>()
                .HasDefaultValue(AwardTargetPurpose.Eligibility);

            entity.HasOne(x => x.AwardType)
                .WithMany(x => x.Targets)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardBudget>(entity =>
        {
            entity.HasIndex(x => x.AwardTypeId);
            entity.HasIndex(x => x.BudgetCode);
            entity.HasIndex(x => x.Year);

            entity.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.SpentAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ReservedAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.AwardType)
                .WithMany(x => x.Budgets)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeAward>(entity =>
        {
            entity.HasIndex(x => x.AwardNumber);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.AwardTypeId);
            entity.HasIndex(x => x.AwardDate);
            entity.HasIndex(x => x.AwardNominationId);

            entity.Property(x => x.MonetaryAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AwardType)
                .WithMany(x => x.Awards)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AwardLevel)
                .WithMany(x => x.Awards)
                .HasForeignKey(x => x.AwardLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PresentedBy)
                .WithMany()
                .HasForeignKey(x => x.PresentedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AwardNomination)
                .WithOne(x => x.Award)
                .HasForeignKey<EmployeeAward>(x => x.AwardNominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Award)
                .HasForeignKey(x => x.AwardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.TeamRecipients)
                .WithOne(x => x.Award)
                .HasForeignKey(x => x.AwardId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TeamAwardRecipient>(entity =>
        {
            entity.HasIndex(x => x.AwardId);
            entity.HasIndex(x => x.EmployeeId);

            entity.HasOne(x => x.Award)
                .WithMany(x => x.TeamRecipients)
                .HasForeignKey(x => x.AwardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardAttachment>(entity =>
        {
            entity.HasIndex(x => x.AwardId);
            entity.HasIndex(x => x.UploadedById);

            entity.Property(x => x.AttachmentType).HasConversion<int>();

            entity.HasOne(x => x.Award)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.AwardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardNomination>(entity =>
        {
            entity.HasIndex(x => x.NominationNumber);
            entity.HasIndex(x => x.AwardTypeId);
            entity.HasIndex(x => x.NomineeId);
            entity.HasIndex(x => x.NominatedById);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.CommitteeId);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.AwardType)
                .WithMany(x => x.Nominations)
                .HasForeignKey(x => x.AwardTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AwardLevel)
                .WithMany(x => x.Nominations)
                .HasForeignKey(x => x.AwardLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Nominee)
                .WithMany()
                .HasForeignKey(x => x.NomineeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NominatedBy)
                .WithMany()
                .HasForeignKey(x => x.NominatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Committee)
                .WithMany(x => x.Nominations)
                .HasForeignKey(x => x.CommitteeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Award)
                .WithOne(x => x.AwardNomination)
                .HasForeignKey<AwardNomination>(x => x.EmployeeAwardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.TeamNominees)
                .WithOne(x => x.Nomination)
                .HasForeignKey(x => x.NominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.NomineeContributions)
                .WithOne(x => x.Nomination)
                .HasForeignKey(x => x.AwardNominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Nomination)
                .HasForeignKey(x => x.AwardNominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Reviews)
                .WithOne(x => x.AwardNomination)
                .HasForeignKey(x => x.AwardNominationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardNomineeContribution>(entity =>
        {
            entity.HasIndex(x => x.AwardNominationId);

            entity.HasOne(x => x.Nomination)
                .WithMany(x => x.NomineeContributions)
                .HasForeignKey(x => x.AwardNominationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardNominationAttachment>(entity =>
        {
            entity.HasIndex(x => x.AwardNominationId);
            entity.HasIndex(x => x.UploadedById);

            entity.HasOne(x => x.Nomination)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.AwardNominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TeamAwardNominee>(entity =>
        {
            entity.HasIndex(x => x.NominationId);
            entity.HasIndex(x => x.EmployeeId);

            entity.HasOne(x => x.Nomination)
                .WithMany(x => x.TeamNominees)
                .HasForeignKey(x => x.NominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardCommittee>(entity =>
        {
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.EffectiveFrom);
            entity.HasIndex(x => x.EffectiveTo);

            entity.HasMany(x => x.Members)
                .WithOne(x => x.Committee)
                .HasForeignKey(x => x.CommitteeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Nominations)
                .WithOne(x => x.Committee)
                .HasForeignKey(x => x.CommitteeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardCommitteeMember>(entity =>
        {
            entity.HasIndex(x => x.CommitteeId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.IsActive);

            entity.HasOne(x => x.Committee)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.CommitteeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AwardNominationReview>(entity =>
        {
            entity.HasIndex(x => x.AwardNominationId);
            entity.HasIndex(x => x.ReviewerId);

            entity.HasOne(x => x.AwardNomination)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.AwardNominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Reviewer)
                .WithMany()
                .HasForeignKey(x => x.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LongServiceAward>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.YearsOfService);
            entity.HasIndex(x => x.MilestoneDate);

            entity.Property(x => x.MonetaryAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR COMPANY SCHEDULE CONFIGURATION
        // =====================================================

        builder.Entity<CompanyEvent>(entity =>
        {
            entity.HasIndex(x => x.EventNumber);
            entity.HasIndex(x => x.EventName);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.StartDate);
            entity.HasIndex(x => x.EndDate);
            entity.HasIndex(x => x.OrganizerId);
            entity.HasIndex(x => x.DepartmentId);

            entity.Property(x => x.Category).HasConversion<int>();
            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.RecurrencePattern).HasConversion<int>();
            entity.Property(x => x.LocationType).HasConversion<int>();
            entity.Property(x => x.Scope).HasConversion<int>();
            entity.Property(x => x.Visibility).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ActualCost).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Organizer)
                .WithMany()
                .HasForeignKey(x => x.OrganizerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Participants)
                .WithOne(x => x.Event)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.AttendanceRecords)
                .WithOne(x => x.Event)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Event)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Tasks)
                .WithOne(x => x.Event)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventParticipant>(entity =>
        {
            entity.HasIndex(x => x.EventId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.InvitationStatus);

            entity.Property(x => x.Role).HasConversion<int>();
            entity.Property(x => x.InvitationStatus).HasConversion<int>();

            entity.HasOne(x => x.Event)
                .WithMany(x => x.Participants)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventAttendance>(entity =>
        {
            entity.HasIndex(x => x.EventId);
            entity.HasIndex(x => x.EmployeeId);

            entity.HasOne(x => x.Event)
                .WithMany(x => x.AttendanceRecords)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MarkedBy)
                .WithMany()
                .HasForeignKey(x => x.MarkedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventAttachment>(entity =>
        {
            entity.HasIndex(x => x.EventId);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Event)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventTask>(entity =>
        {
            entity.HasIndex(x => x.EventId);
            entity.HasIndex(x => x.AssignedToId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Category).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Event)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedTo)
                .WithMany()
                .HasForeignKey(x => x.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MeetingRoom>(entity =>
        {
            entity.HasIndex(x => x.RoomCode);
            entity.HasIndex(x => x.RoomName);
            entity.HasIndex(x => x.StationId);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Bookings)
                .WithOne(x => x.Room)
                .HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RoomBooking>(entity =>
        {
            entity.HasIndex(x => x.BookingNumber);
            entity.HasIndex(x => x.RoomId);
            entity.HasIndex(x => x.EventId);
            entity.HasIndex(x => x.BookedById);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.StartDateTime, x.EndDateTime });

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Room)
                .WithMany(x => x.Bookings)
                .HasForeignKey(x => x.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Event)
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.BookedBy)
                .WithMany()
                .HasForeignKey(x => x.BookedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CompanyMilestone>(entity =>
        {
            entity.HasIndex(x => x.Title);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.MilestoneDate);

            entity.Property(x => x.Category).HasConversion<int>();
        });

        builder.Entity<BusinessClosure>(entity =>
        {
            entity.HasIndex(x => x.StartDate);
            entity.HasIndex(x => x.EndDate);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.StationId);
            entity.HasIndex(x => x.DepartmentId);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Station)
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AnnouncedBy)
                .WithMany()
                .HasForeignKey(x => x.AnnouncedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FiscalYear>(entity =>
        {
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.IsCurrent);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasMany(x => x.Periods)
                .WithOne(x => x.FiscalYear)
                .HasForeignKey(x => x.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FiscalPeriod>(entity =>
        {
            entity.HasIndex(x => x.FiscalYearId);
            entity.HasIndex(x => x.PeriodNumber);
            entity.HasIndex(x => x.StartDate);
            entity.HasIndex(x => x.EndDate);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.FiscalYear)
                .WithMany(x => x.Periods)
                .HasForeignKey(x => x.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR JOB ANALYSIS CONFIGURATION
        // =====================================================

        builder.Entity<JobDescription>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionNumber);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.JobTitle);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.EffectiveDate);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SupersededByVersion)
                .WithMany()
                .HasForeignKey(x => x.SupersededByVersionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreparedBy)
                .WithMany()
                .HasForeignKey(x => x.PreparedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewedBy)
                .WithMany()
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SuggestedSalaryGrade)
                .WithMany()
                .HasForeignKey(x => x.SuggestedSalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.StaffLevel)
                .WithMany()
                .HasForeignKey(x => x.StaffLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Union)
                .WithMany()
                .HasForeignKey(x => x.UnionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobFamily)
                .WithMany()
                .HasForeignKey(x => x.JobFamilyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobSubFamily)
                .WithMany()
                .HasForeignKey(x => x.JobSubFamilyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobLevel)
                .WithMany()
                .HasForeignKey(x => x.JobLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Responsibilities)
                .WithOne(x => x.JobDescription)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.MedicalRequirements)
                .WithOne(x => x.JobDescription)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.DutyItems)
                .WithOne(x => x.JobDescription)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.PpeRequirements)
                .WithOne(x => x.JobDescription)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobDutyItem>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.DutyItems)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobMedicalRequirement>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);

            entity.Property(x => x.Category).HasConversion<int>();

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.MedicalRequirements)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobResponsibility>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.Responsibilities)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Qualifications)
                .WithOne(x => x.JobResponsibility)
                .HasForeignKey(x => x.JobResponsibilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Competencies)
                .WithOne(x => x.JobResponsibility)
                .HasForeignKey(x => x.JobResponsibilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Kpis)
                .WithOne(x => x.JobResponsibility)
                .HasForeignKey(x => x.JobResponsibilityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobResponsibilityKpi>(entity =>
        {
            entity.HasIndex(x => x.JobResponsibilityId);

            entity.HasOne(x => x.JobResponsibility)
                .WithMany(x => x.Kpis)
                .HasForeignKey(x => x.JobResponsibilityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Union>(entity =>
        {
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.Name);

            entity.HasMany(x => x.Agreements)
                .WithOne(x => x.Union)
                .HasForeignKey(x => x.UnionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CollectiveBargainingAgreement>(entity =>
        {
            entity.HasIndex(x => x.UnionId);

            entity.HasOne(x => x.Union)
                .WithMany(x => x.Agreements)
                .HasForeignKey(x => x.UnionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobFamily>(entity =>
        {
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.Name);

            entity.HasMany(x => x.SubFamilies)
                .WithOne(x => x.JobFamily)
                .HasForeignKey(x => x.JobFamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobSubFamily>(entity =>
        {
            entity.HasIndex(x => x.JobFamilyId);

            entity.HasOne(x => x.JobFamily)
                .WithMany(x => x.SubFamilies)
                .HasForeignKey(x => x.JobFamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CareerLevel>(entity =>
        {
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.Rank);

            entity.HasOne(x => x.SalaryGrade)
                .WithMany()
                .HasForeignKey(x => x.SalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobQualification>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);
            entity.HasIndex(x => x.JobResponsibilityId);
            entity.HasIndex(x => x.Type);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.Qualifications)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobResponsibility)
                .WithMany(x => x.Qualifications)
                .HasForeignKey(x => x.JobResponsibilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Qualification)
                .WithMany()
                .HasForeignKey(x => x.QualificationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobCompetency>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);
            entity.HasIndex(x => x.JobResponsibilityId);
            entity.HasIndex(x => x.Type);

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.RequiredLevel).HasConversion<int>();

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.Competencies)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobResponsibility)
                .WithMany(x => x.Competencies)
                .HasForeignKey(x => x.JobResponsibilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Skill)
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Competency)
                .WithMany()
                .HasForeignKey(x => x.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ManpowerBudget>(entity =>
        {
            entity.HasIndex(x => x.BudgetNumber);
            entity.HasIndex(x => x.FiscalYear);
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.BudgetLines)
                .WithOne(x => x.ManpowerBudget)
                .HasForeignKey(x => x.ManpowerBudgetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ManpowerBudgetLine>(entity =>
        {
            entity.HasIndex(x => x.ManpowerBudgetId);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.Priority);

            entity.Property(x => x.Priority).HasConversion<int>();

            entity.HasOne(x => x.ManpowerBudget)
                .WithMany(x => x.BudgetLines)
                .HasForeignKey(x => x.ManpowerBudgetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobDescription)
                .WithMany()
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobPhysicalDemand>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);
            entity.HasIndex(x => x.DemandType);

            entity.Property(x => x.DemandType).HasConversion<int>();
            entity.Property(x => x.Frequency).HasConversion<int>();

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.PhysicalDemands)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobWorkingCondition>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);
            entity.HasIndex(x => x.EnvironmentType);

            entity.Property(x => x.EnvironmentType).HasConversion<int>();
            entity.Property(x => x.ExposureLevel).HasConversion<int>();

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.JobWorkingConditions)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobPpeRequirement>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.PpeRequirements)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.PpeType)
                .WithMany()
                .HasForeignKey(x => x.PpeTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobEquipmentTool>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);
            entity.HasIndex(x => x.Type);

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.RequiredProficiency).HasConversion<int>();

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.EquipmentTools)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LinkedQualification)
                .WithMany()
                .HasForeignKey(x => x.LinkedQualificationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.TrainingRequirements)
                .WithOne(x => x.JobEquipmentTool)
                .HasForeignKey(x => x.JobEquipmentToolId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobEquipmentTraining>(entity =>
        {
            entity.HasIndex(x => x.JobEquipmentToolId);

            entity.HasOne(x => x.JobEquipmentTool)
                .WithMany(x => x.TrainingRequirements)
                .HasForeignKey(x => x.JobEquipmentToolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.TrainingProgram)
                .WithMany()
                .HasForeignKey(x => x.TrainingProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobReportingRelationship>(entity =>
        {
            entity.HasIndex(x => x.JobDescriptionId);
            entity.HasIndex(x => x.RelationshipType);

            entity.Property(x => x.RelationshipType).HasConversion<int>();

            entity.HasOne(x => x.JobDescription)
                .WithMany(x => x.ReportingRelationships)
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RelatedPosition)
                .WithMany()
                .HasForeignKey(x => x.EmployeeOrPositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR MEDICAL & INSURANCE CONFIGURATION
        // =====================================================

        builder.Entity<HealthcareFacility>(entity =>
        {
            entity.HasIndex(x => x.FacilityCode);
            entity.HasIndex(x => x.FacilityName);
            entity.HasIndex(x => x.FacilityType);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => new { x.TenantId, x.FacilityCode }).IsUnique();

            entity.Property(x => x.FacilityType).HasConversion<int>();

            entity.HasOne(x => x.Country)
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Bank)
                .WithMany()
                .HasForeignKey(x => x.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Physicians)
                .WithOne(x => x.Facility)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Services)
                .WithOne(x => x.Facility)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ProviderFacilities)
                .WithOne(x => x.Facility)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Appointments)
                .WithOne(x => x.Facility)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ExpenseClaims)
                .WithOne(x => x.Facility)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Physician>(entity =>
        {
            entity.HasIndex(x => x.FacilityId);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.MedicalLicenseNumber);

            entity.HasOne(x => x.Facility)
                .WithMany(x => x.Physicians)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ExpenseClaims)
                .WithOne(x => x.Physician)
                .HasForeignKey(x => x.PhysicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Appointments)
                .WithOne(x => x.Physician)
                .HasForeignKey(x => x.PhysicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FacilityService>(entity =>
        {
            entity.HasIndex(x => x.FacilityId);
            entity.HasIndex(x => x.ServiceType);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.ServiceType).HasConversion<int>();
            entity.Property(x => x.EstimatedCost).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Facility)
                .WithMany(x => x.Services)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalInsuranceProvider>(entity =>
        {
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.ProviderType);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

            entity.Property(x => x.ProviderType).HasConversion<int>();
            entity.Property(x => x.PreferredPaymentMethod).HasConversion<int>();

            entity.HasOne(x => x.Country)
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Bank)
                .WithMany()
                .HasForeignKey(x => x.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Plans)
                .WithOne(x => x.MedicalInsuranceProvider)
                .HasForeignKey(x => x.MedicalInsuranceProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ProviderFacilities)
                .WithOne(x => x.MedicalInsuranceProvider)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents)
                .WithOne(x => x.MedicalInsuranceProvider)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EmployeePolicies)
                .WithOne(x => x.MedicalInsuranceProvider)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.PremiumRecords)
                .WithOne(x => x.MedicalInsuranceProvider)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalInsurancePlan>(entity =>
        {
            entity.HasIndex(x => x.MedicalInsuranceProviderId);
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.PlanType);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.PlanType).HasConversion<int>();

            entity.HasOne(x => x.MedicalInsuranceProvider)
                .WithMany(x => x.Plans)
                .HasForeignKey(x => x.MedicalInsuranceProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EmployeePolicies)
                .WithOne(x => x.MedicalInsurancePlan)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.PremiumRecords)
                .WithOne(x => x.MedicalInsurancePlan)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeMedicalInsurancePolicy>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.ProviderId);
            entity.HasIndex(x => x.PlanId);
            entity.HasIndex(x => x.BenefitTierId);
            entity.HasIndex(x => x.PolicyNumber);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MedicalInsuranceProvider)
                .WithMany(x => x.EmployeePolicies)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MedicalInsurancePlan)
                .WithMany(x => x.EmployeePolicies)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.BenefitTier)
                .WithMany(x => x.Policies)
                .HasForeignKey(x => x.BenefitTierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Dependents)
                .WithOne(x => x.Policy)
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ExpenseClaims)
                .WithOne(x => x.InsurancePolicy)
                .HasForeignKey(x => x.InsurancePolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.InsuranceClaims)
                .WithOne(x => x.Policy)
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.PreAuthorizations)
                .WithOne(x => x.Policy)
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.PremiumRecords)
                .WithOne(x => x.Policy)
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalInsurancePolicyDependent>(entity =>
        {
            entity.HasIndex(x => x.PolicyId);
            entity.HasIndex(x => x.DependentId);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => new { x.PolicyId, x.DependentId }).IsUnique();

            entity.HasOne(x => x.Policy)
                .WithMany(x => x.Dependents)
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Dependent)
                .WithMany()
                .HasForeignKey(x => x.DependentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalInsuranceClaim>(entity =>
        {
            entity.HasIndex(x => x.PolicyId);
            entity.HasIndex(x => x.MedicalExpenseClaimId).IsUnique();
            entity.HasIndex(x => x.InsuranceClaimNumber);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Policy)
                .WithMany(x => x.InsuranceClaims)
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MedicalExpenseClaim)
                .WithMany(x => x.InsuranceClaims)
                .HasForeignKey(x => x.MedicalExpenseClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalInsuranceProviderFacility>(entity =>
        {
            entity.HasIndex(x => x.ProviderId);
            entity.HasIndex(x => x.FacilityId);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => new { x.ProviderId, x.FacilityId, x.EffectiveDate });

            entity.HasOne(x => x.MedicalInsuranceProvider)
                .WithMany(x => x.ProviderFacilities)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Facility)
                .WithMany(x => x.ProviderFacilities)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalInsuranceProviderDocument>(entity =>
        {
            entity.HasIndex(x => x.ProviderId);
            entity.HasIndex(x => x.DocumentType);

            entity.Property(x => x.DocumentType).HasConversion<int>();

            entity.HasOne(x => x.MedicalInsuranceProvider)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalInsurancePremiumRecord>(entity =>
        {
            entity.HasIndex(x => x.ProviderId);
            entity.HasIndex(x => x.PlanId);
            entity.HasIndex(x => x.PolicyId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.PlanId, x.BillingPeriodStart, x.BillingPeriodEnd });

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.PaymentMethod).HasConversion<int>();

            entity.HasOne(x => x.MedicalInsuranceProvider)
                .WithMany(x => x.PremiumRecords)
                .HasForeignKey(x => x.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MedicalInsurancePlan)
                .WithMany(x => x.PremiumRecords)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Policy)
                .WithMany(x => x.PremiumRecords)
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalBenefitScheme>(entity =>
        {
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

            entity.HasMany(x => x.Tiers)
                .WithOne(x => x.Scheme)
                .HasForeignKey(x => x.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalBenefitTier>(entity =>
        {
            entity.HasIndex(x => x.SchemeId);
            entity.HasIndex(x => x.StaffLevelId);
            entity.HasIndex(x => x.IsActive);

            entity.HasOne(x => x.Scheme)
                .WithMany(x => x.Tiers)
                .HasForeignKey(x => x.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.StaffLevel)
                .WithMany()
                .HasForeignKey(x => x.StaffLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Policies)
                .WithOne(x => x.BenefitTier)
                .HasForeignKey(x => x.BenefitTierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeHealthProfile>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.EmployeeId }).IsUnique();

            entity.Property(x => x.BloodGroup).HasConversion<int>();
            entity.Property(x => x.DisabilityStatus).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreferredFacility)
                .WithMany()
                .HasForeignKey(x => x.PreferredFacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreferredPhysician)
                .WithMany()
                .HasForeignKey(x => x.PreferredPhysicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Conditions)
                .WithOne(x => x.HealthProfile)
                .HasForeignKey(x => x.HealthProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Allergies)
                .WithOne(x => x.HealthProfile)
                .HasForeignKey(x => x.HealthProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.MedicalExams)
                .WithOne(x => x.HealthProfile)
                .HasForeignKey(x => x.HealthProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeHealthCondition>(entity =>
        {
            entity.HasIndex(x => x.HealthProfileId);
            entity.HasIndex(x => x.ICDCode);

            entity.Property(x => x.Severity).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.HealthProfile)
                .WithMany(x => x.Conditions)
                .HasForeignKey(x => x.HealthProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeAllergy>(entity =>
        {
            entity.HasIndex(x => x.HealthProfileId);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.AllergyType).HasConversion<int>();
            entity.Property(x => x.Severity).HasConversion<int>();

            entity.HasOne(x => x.HealthProfile)
                .WithMany(x => x.Allergies)
                .HasForeignKey(x => x.HealthProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeMedicalExam>(entity =>
        {
            entity.HasIndex(x => x.HealthProfileId);
            entity.HasIndex(x => x.ExamDate);
            entity.HasIndex(x => x.FacilityId);
            entity.HasIndex(x => x.PhysicianId);
            entity.HasIndex(x => x.LinkedClaimId);

            entity.Property(x => x.Result).HasConversion<int>();

            entity.HasOne(x => x.HealthProfile)
                .WithMany(x => x.MedicalExams)
                .HasForeignKey(x => x.HealthProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Facility)
                .WithMany()
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Physician)
                .WithMany()
                .HasForeignKey(x => x.PhysicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LinkedClaim)
                .WithMany()
                .HasForeignKey(x => x.LinkedClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents)
                .WithOne(x => x.Exam)
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeMedicalExamDocument>(entity =>
        {
            entity.HasIndex(x => x.ExamId);

            entity.HasOne(x => x.Exam)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.ExamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalClaimPreAuthorization>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.PolicyId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.AuthorizationNumber);

            entity.Property(x => x.ServiceType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Dependent)
                .WithMany()
                .HasForeignKey(x => x.DependentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Policy)
                .WithMany(x => x.PreAuthorizations)
                .HasForeignKey(x => x.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Facility)
                .WithMany()
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Physician)
                .WithMany()
                .HasForeignKey(x => x.PhysicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Approver)
                .WithMany()
                .HasForeignKey(x => x.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalReferral>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.ReferralNumber);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.TenantId, x.ReferralNumber }).IsUnique();

            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Dependent)
                .WithMany()
                .HasForeignKey(x => x.DependentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReferringFacility)
                .WithMany()
                .HasForeignKey(x => x.ReferringFacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReferringPhysician)
                .WithMany(x => x.IssuedReferrals)
                .HasForeignKey(x => x.ReferringPhysicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReferredToFacility)
                .WithMany()
                .HasForeignKey(x => x.ReferredToFacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReferredToPhysician)
                .WithMany(x => x.ReceivedReferrals)
                .HasForeignKey(x => x.ReferredToPhysicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalAppointment>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.FacilityId);
            entity.HasIndex(x => x.AppointmentDateTime);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.TenantId, x.AppointmentNumber }).IsUnique();

            entity.Property(x => x.ServiceType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Dependent)
                .WithMany()
                .HasForeignKey(x => x.DependentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Facility)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Physician)
                .WithMany(x => x.Appointments)
                .HasForeignKey(x => x.PhysicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LinkedReferral)
                .WithMany()
                .HasForeignKey(x => x.LinkedReferralId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LinkedClaim)
                .WithMany()
                .HasForeignKey(x => x.LinkedClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NHISClaim>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.FacilityId);
            entity.HasIndex(x => x.ServiceDate);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.LinkedMedicalClaimId);
            entity.HasIndex(x => new { x.TenantId, x.ClaimNumber }).IsUnique();

            entity.Property(x => x.ServiceType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Dependent)
                .WithMany()
                .HasForeignKey(x => x.DependentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Facility)
                .WithMany()
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Physician)
                .WithMany()
                .HasForeignKey(x => x.PhysicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LinkedMedicalClaim)
                .WithMany(x => x.LinkedNHISClaims)
                .HasForeignKey(x => x.LinkedMedicalClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents)
                .WithOne(x => x.NHISClaim)
                .HasForeignKey(x => x.NHISClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<NHISClaimDocument>(entity =>
        {
            entity.HasIndex(x => x.NHISClaimId);

            entity.HasOne(x => x.NHISClaim)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.NHISClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalExpenseClaim>(entity =>
        {
            entity.HasIndex(x => x.ClaimNumber);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.DependentId);
            entity.HasIndex(x => x.FacilityId);
            entity.HasIndex(x => x.InsurancePolicyId);
            entity.HasIndex(x => x.PreAuthorizationId);
            entity.HasIndex(x => x.ReferralId);
            entity.HasIndex(x => x.LeaveRequestId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ClaimDate);
            entity.HasIndex(x => new { x.TenantId, x.ClaimNumber }).IsUnique();

            entity.Property(x => x.ExpenseType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.PaymentMethod).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Dependent)
                .WithMany()
                .HasForeignKey(x => x.DependentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Facility)
                .WithMany(x => x.ExpenseClaims)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Physician)
                .WithMany(x => x.ExpenseClaims)
                .HasForeignKey(x => x.PhysicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreAuthorization)
                .WithMany()
                .HasForeignKey(x => x.PreAuthorizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Referral)
                .WithMany()
                .HasForeignKey(x => x.ReferralId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InsurancePolicy)
                .WithMany(x => x.ExpenseClaims)
                .HasForeignKey(x => x.InsurancePolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LeaveRequest)
                .WithMany()
                .HasForeignKey(x => x.LeaveRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.InsuranceClaims)
                .WithOne(x => x.MedicalExpenseClaim)
                .HasForeignKey(x => x.MedicalExpenseClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.LinkedNHISClaims)
                .WithOne(x => x.LinkedMedicalClaim)
                .HasForeignKey(x => x.LinkedMedicalClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Approvals)
                .WithOne(x => x.Claim)
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents)
                .WithOne(x => x.Claim)
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Items)
                .WithOne(x => x.Claim)
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Notes)
                .WithOne(x => x.Claim)
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalExpenseApproval>(entity =>
        {
            entity.HasIndex(x => x.ClaimId);
            entity.HasIndex(x => x.ApproverId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Claim)
                .WithMany(x => x.Approvals)
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Approver)
                .WithMany()
                .HasForeignKey(x => x.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalExpenseItem>(entity =>
        {
            entity.HasIndex(x => x.ClaimId);

            entity.Property(x => x.ItemType).HasConversion<int>();

            entity.HasOne(x => x.Claim)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalExpenseDocument>(entity =>
        {
            entity.HasIndex(x => x.ClaimId);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Claim)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MedicalExpenseClaimNote>(entity =>
        {
            entity.HasIndex(x => x.ClaimId);
            entity.HasIndex(x => x.AuthorId);
            entity.HasIndex(x => x.NoteType);
            entity.HasIndex(x => x.NoteDate);

            entity.Property(x => x.NoteType).HasConversion<int>();

            entity.HasOne(x => x.Claim)
                .WithMany(x => x.Notes)
                .HasForeignKey(x => x.ClaimId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Author)
                .WithMany()
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR PROMOTION & TRANSFER CONFIGURATION
        // =====================================================

        // ---- StaffMovement ----
        builder.Entity<StaffMovement>(entity =>
        {
            entity.HasIndex(x => x.MovementNumber);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.MovementType);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.EffectiveDate);
            entity.HasIndex(x => x.CurrentPositionId);
            entity.HasIndex(x => x.CurrentOrganizationUnitId);
            entity.HasIndex(x => x.NewPositionId);
            entity.HasIndex(x => x.NewOrganizationUnitId);
            entity.HasIndex(x => x.RequestedById);
            entity.HasIndex(x => new { x.TenantId, x.MovementNumber }).IsUnique();

            entity.Property(x => x.MovementType).HasConversion<int>();
            entity.Property(x => x.Category).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.CurrentSalary).HasColumnType("decimal(18,2)");
            entity.Property(x => x.NewSalary).HasColumnType("decimal(18,2)");
            entity.Property(x => x.SalaryIncreaseAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.SalaryIncreasePercentage).HasColumnType("decimal(5,2)");

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentPosition)
                .WithMany()
                .HasForeignKey(x => x.CurrentPositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentOrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.CurrentOrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentOrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.CurrentOrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentLocation)
                .WithMany()
                .HasForeignKey(x => x.CurrentLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentLocationLevel)
                .WithMany()
                .HasForeignKey(x => x.CurrentLocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentSupervisor)
                .WithMany()
                .HasForeignKey(x => x.CurrentSupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentSalaryGrade)
                .WithMany()
                .HasForeignKey(x => x.CurrentSalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentSalaryLevel)
                .WithMany()
                .HasForeignKey(x => x.CurrentSalaryLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CurrentSalaryNotch)
                .WithMany()
                .HasForeignKey(x => x.CurrentSalaryNotchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewPosition)
                .WithMany()
                .HasForeignKey(x => x.NewPositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewOrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.NewOrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewOrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.NewOrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewLocation)
                .WithMany()
                .HasForeignKey(x => x.NewLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewLocationLevel)
                .WithMany()
                .HasForeignKey(x => x.NewLocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewSupervisor)
                .WithMany()
                .HasForeignKey(x => x.NewSupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewSalaryGrade)
                .WithMany()
                .HasForeignKey(x => x.NewSalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewSalaryLevel)
                .WithMany()
                .HasForeignKey(x => x.NewSalaryLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NewSalaryNotch)
                .WithMany()
                .HasForeignKey(x => x.NewSalaryNotchId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SuccessionPlan)
                .WithMany()
                .HasForeignKey(x => x.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            // Self-referencing: the return movement that closes a temporary assignment.
            entity.HasOne(x => x.ReturnMovement)
                .WithMany()
                .HasForeignKey(x => x.ReturnMovementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RequestedBy)
                .WithMany()
                .HasForeignKey(x => x.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AuthorizedBy)
                .WithMany()
                .HasForeignKey(x => x.AuthorizedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RejectedBy)
                .WithMany()
                .HasForeignKey(x => x.RejectedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CancelledBy)
                .WithMany()
                .HasForeignKey(x => x.CancelledById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.BasedOnAppraisal)
                .WithMany()
                .HasForeignKey(x => x.BasedOnAppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ApprovalLevels)
                .WithOne(x => x.Movement)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.StatusHistory)
                .WithOne(x => x.Movement)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Movement)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.ChecklistItems)
                .WithOne(x => x.Movement)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- StaffMovementApprovalLevel ----
        builder.Entity<StaffMovementApprovalLevel>(entity =>
        {
            entity.HasIndex(x => x.MovementId);
            entity.HasIndex(x => x.ApproverId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.MovementId, x.Level }).IsUnique();

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Movement)
                .WithMany(x => x.ApprovalLevels)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Approver)
                .WithMany()
                .HasForeignKey(x => x.ApproverId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DelegatedTo)
                .WithMany()
                .HasForeignKey(x => x.DelegatedToId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffMovementStatusHistory ----
        builder.Entity<StaffMovementStatusHistory>(entity =>
        {
            entity.HasIndex(x => x.MovementId);
            entity.HasIndex(x => x.ChangedDate);

            entity.Property(x => x.FromStatus).HasConversion<int>();
            entity.Property(x => x.ToStatus).HasConversion<int>();

            entity.HasOne(x => x.Movement)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ChangedBy)
                .WithMany()
                .HasForeignKey(x => x.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffMovementAttachment ----
        builder.Entity<StaffMovementAttachment>(entity =>
        {
            entity.HasIndex(x => x.MovementId);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Movement)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffMovementChecklistItem ----
        builder.Entity<StaffMovementChecklistItem>(entity =>
        {
            entity.HasIndex(x => x.MovementId);
            entity.HasIndex(x => x.ResponsiblePersonId);
            entity.HasIndex(x => x.IsCompleted);

            entity.Property(x => x.Category).HasConversion<int>();

            entity.HasOne(x => x.Movement)
                .WithMany(x => x.ChecklistItems)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ResponsiblePerson)
                .WithMany()
                .HasForeignKey(x => x.ResponsiblePersonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffPromotion ----
        builder.Entity<StaffPromotion>(entity =>
        {
            entity.HasIndex(x => x.MovementId).IsUnique();

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Movement)
                .WithOne(x => x.Promotion)
                .HasForeignKey<StaffPromotion>(x => x.MovementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffTransfer ----
        builder.Entity<StaffTransfer>(entity =>
        {
            entity.HasIndex(x => x.MovementId).IsUnique();
            entity.HasIndex(x => x.ReplacementEmployeeId);

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.ReasonCategory).HasConversion<int>();
            entity.Property(x => x.RelocationAllowance).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Movement)
                .WithOne(x => x.Transfer)
                .HasForeignKey<StaffTransfer>(x => x.MovementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReplacementEmployee)
                .WithMany()
                .HasForeignKey(x => x.ReplacementEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDemotion ----
        builder.Entity<StaffDemotion>(entity =>
        {
            entity.HasIndex(x => x.MovementId).IsUnique();
            entity.HasIndex(x => x.DisciplinaryActionId);
            entity.HasIndex(x => x.PerformanceImprovementPlanId);

            entity.Property(x => x.Reason).HasConversion<int>();

            entity.HasOne(x => x.Movement)
                .WithOne(x => x.Demotion)
                .HasForeignKey<StaffDemotion>(x => x.MovementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DisciplinaryAction)
                .WithMany()
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PerformanceImprovementPlan)
                .WithMany()
                .HasForeignKey(x => x.PerformanceImprovementPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffSecondment ----
        builder.Entity<StaffSecondment>(entity =>
        {
            entity.HasIndex(x => x.MovementId).IsUnique();

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.SecondmentAllowance).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Movement)
                .WithOne(x => x.Secondment)
                .HasForeignKey<StaffSecondment>(x => x.MovementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffActingAppointment ----
        builder.Entity<StaffActingAppointment>(entity =>
        {
            entity.HasIndex(x => x.AppointmentNumber);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.ActingPositionId);
            entity.HasIndex(x => x.ActingForEmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.StartDate);
            entity.HasIndex(x => new { x.TenantId, x.AppointmentNumber }).IsUnique();

            entity.Property(x => x.Reason).HasConversion<int>();
            entity.Property(x => x.AllowanceCalculation).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.ActingAllowance).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ActingPosition)
                .WithMany()
                .HasForeignKey(x => x.ActingPositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ActingForEmployee)
                .WithMany()
                .HasForeignKey(x => x.ActingForEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Movement)
                .WithMany()
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConversionMovement)
                .WithMany()
                .HasForeignKey(x => x.ConversionMovementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- EmployeeCareerPath ----
        builder.Entity<EmployeeCareerPath>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.LocationId);
            entity.HasIndex(x => x.IsCurrent);
            entity.HasIndex(x => x.StartDate);
            entity.HasIndex(x => new { x.EmployeeId, x.IsCurrent });

            entity.Property(x => x.Salary).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LocationLevel)
                .WithMany()
                .HasForeignKey(x => x.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Movement)
                .WithMany()
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SalaryGrade)
                .WithMany()
                .HasForeignKey(x => x.SalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SalaryLevel)
                .WithMany()
                .HasForeignKey(x => x.SalaryLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SalaryNotch)
                .WithMany()
                .HasForeignKey(x => x.SalaryNotchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR RECRUITMENT CONFIGURATION
        // =====================================================

        // ---- PositionVacancy ----
        builder.Entity<PositionVacancy>(entity =>
        {
            entity.HasIndex(x => x.PositionId).HasDatabaseName("IX_PositionVacancy_PositionId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_PositionVacancy_Status");
            entity.HasIndex(x => x.OrganizationUnitId).HasDatabaseName("IX_PositionVacancy_OrgUnit");
            entity.HasIndex(x => new { x.TenantId, x.PositionId, x.Status })
                .HasDatabaseName("IX_PositionVacancy_Tenant_Position_Status");

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Classification).HasConversion<int>();
            entity.Property(x => x.Reason).HasConversion<int>();
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.VacatedByEmployee)
                .WithMany()
                .HasForeignKey(x => x.VacatedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.StaffRequisition)
                .WithMany()
                .HasForeignKey(x => x.StaffRequisitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- JobVacancy ----
        builder.Entity<JobVacancy>(entity =>
        {
            entity.HasIndex(x => x.VacancyNumber).HasDatabaseName("IX_JobVacancy_Number");
            entity.HasIndex(x => x.StaffRequisitionId).HasDatabaseName("IX_JobVacancy_RequisitionId");
            entity.HasIndex(x => x.PositionId).HasDatabaseName("IX_JobVacancy_PositionId");
            entity.HasIndex(x => x.VacancyStatus).HasDatabaseName("IX_JobVacancy_Status");
            entity.HasIndex(x => x.ApplicationDeadline).HasDatabaseName("IX_JobVacancy_Deadline");
            entity.HasIndex(x => new { x.TenantId, x.VacancyNumber })
                .IsUnique().HasDatabaseName("IX_JobVacancy_Tenant_Number");

            entity.Property(x => x.VacancyStatus).HasConversion<int>();
            entity.Property(x => x.ClosureReason).HasConversion<int>();
            entity.Property(x => x.EmploymentType).HasConversion<int>();
            entity.Property(x => x.WorkMode).HasConversion<int>();
            entity.Property(x => x.ShortlistApprovalStatus).HasConversion<int>();
            entity.Property(x => x.ShortlistApprovalNotes).HasMaxLength(2000);
            entity.Property(x => x.SalaryRangeMin).HasColumnType("decimal(18,2)");
            entity.Property(x => x.SalaryRangeMax).HasColumnType("decimal(18,2)");
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.Ignore(x => x.JobTitle);

            entity.HasOne(x => x.Requisition)
                .WithMany()
                .HasForeignKey(x => x.StaffRequisitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HiringManager)
                .WithMany()
                .HasForeignKey(x => x.HiringManagerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Recruiter)
                .WithMany()
                .HasForeignKey(x => x.RecruiterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Pipeline)
                .WithMany()
                .HasForeignKey(x => x.RecruitmentPipelineId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.JobVacancy)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.JobPostings)
                .WithOne(x => x.JobVacancy)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ShortlistingCriteria)
                .WithOne(x => x.JobVacancy)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.JobApplications)
                .WithOne(x => x.JobVacancy)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Interviews)
                .WithOne(x => x.JobVacancy)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.StatusHistory)
                .WithOne(x => x.JobVacancy)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobVacancyAttachment>(entity =>
        {
            entity.HasIndex(x => x.JobVacancyId).HasDatabaseName("IX_JobVacancyAttachment_VacancyId");

            entity.HasOne(x => x.JobVacancy)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobVacancyStatusHistory>(entity =>
        {
            entity.HasIndex(x => x.JobVacancyId).HasDatabaseName("IX_JobVacancyStatusHistory_VacancyId");
            entity.HasIndex(x => x.ChangedDate).HasDatabaseName("IX_JobVacancyStatusHistory_Date");

            entity.Property(x => x.FromStatus).HasConversion<int>();
            entity.Property(x => x.ToStatus).HasConversion<int>();

            entity.HasOne(x => x.JobVacancy)
                .WithMany(x => x.StatusHistory)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ChangedBy)
                .WithMany()
                .HasForeignKey(x => x.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobPosting>(entity =>
        {
            entity.HasIndex(x => x.JobVacancyId).HasDatabaseName("IX_JobPosting_VacancyId");
            entity.HasIndex(x => x.Channel).HasDatabaseName("IX_JobPosting_Channel");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_JobPosting_Status");
            entity.HasIndex(x => x.PublishDate).HasDatabaseName("IX_JobPosting_PublishDate");

            entity.Property(x => x.Channel).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.JobVacancy)
                .WithMany(x => x.JobPostings)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PostedBy)
                .WithMany()
                .HasForeignKey(x => x.PostedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.JobPosting)
                .HasForeignKey(x => x.JobPostingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobPostingAttachment>(entity =>
        {
            entity.HasIndex(x => x.JobPostingId).HasDatabaseName("IX_JobPostingAttachment_PostingId");

            entity.HasOne(x => x.JobPosting)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.JobPostingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- RecruitmentPipeline ----
        builder.Entity<RecruitmentPipeline>(entity =>
        {
            entity.HasIndex(x => x.IsDefault).HasDatabaseName("IX_RecruitmentPipeline_IsDefault");
            entity.HasIndex(x => new { x.TenantId, x.Name })
                .IsUnique().HasDatabaseName("IX_RecruitmentPipeline_Tenant_Name");

            entity.HasMany(x => x.Stages)
                .WithOne(x => x.Pipeline)
                .HasForeignKey(x => x.RecruitmentPipelineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RecruitmentPipelineStage>(entity =>
        {
            entity.HasIndex(x => x.RecruitmentPipelineId).HasDatabaseName("IX_PipelineStage_PipelineId");
            entity.HasIndex(x => new { x.RecruitmentPipelineId, x.Order })
                .HasDatabaseName("IX_PipelineStage_Pipeline_Order");

            entity.Property(x => x.StageType).HasConversion<int>();

            entity.HasOne(x => x.Pipeline)
                .WithMany(x => x.Stages)
                .HasForeignKey(x => x.RecruitmentPipelineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- VacancyPipelineStageAssignment ----
        builder.Entity<VacancyPipelineStageAssignment>(entity =>
        {
            entity.HasIndex(x => x.JobVacancyId).HasDatabaseName("IX_VacancyStageAssignment_VacancyId");
            entity.HasIndex(x => x.PipelineStageId).HasDatabaseName("IX_VacancyStageAssignment_StageId");
            entity.HasIndex(x => new { x.JobVacancyId, x.PipelineStageId })
                .IsUnique().HasDatabaseName("IX_VacancyStageAssignment_Vacancy_Stage");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_VacancyStageAssignment_Status");
            entity.HasIndex(x => x.DueDate).HasDatabaseName("IX_VacancyStageAssignment_DueDate");

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.JobVacancy)
                .WithMany(x => x.PipelineStageAssignments)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PipelineStage)
                .WithMany()
                .HasForeignKey(x => x.PipelineStageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedTo)
                .WithMany()
                .HasForeignKey(x => x.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedBy)
                .WithMany()
                .HasForeignKey(x => x.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CompletedBy)
                .WithMany()
                .HasForeignKey(x => x.CompletedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EscalateTo)
                .WithMany()
                .HasForeignKey(x => x.EscalateToId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- JobShortlistingCriteria ----
        builder.Entity<JobShortlistingCriteria>(entity =>
        {
            entity.HasIndex(x => x.JobVacancyId).HasDatabaseName("IX_ShortlistingCriteria_VacancyId");

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.ComparisonOperator).HasConversion<int>();
            entity.Property(x => x.MinValue).HasColumnType("decimal(18,4)");
            entity.Property(x => x.MaxValue).HasColumnType("decimal(18,4)");

            entity.HasOne(x => x.JobVacancy)
                .WithMany(x => x.ShortlistingCriteria)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- JobCandidate ----
        builder.Entity<JobCandidate>(entity =>
        {
            entity.HasIndex(x => x.CandidateNumber).HasDatabaseName("IX_JobCandidate_Number");
            entity.HasIndex(x => x.Email).HasDatabaseName("IX_JobCandidate_Email");
            entity.HasIndex(x => x.IsInTalentPool).HasDatabaseName("IX_JobCandidate_TalentPool");
            entity.HasIndex(x => new { x.TenantId, x.CandidateNumber })
                .IsUnique().HasDatabaseName("IX_JobCandidate_Tenant_Number");

            entity.Property(x => x.Gender).HasConversion<int>();
            entity.Ignore(x => x.FullName);

            entity.HasOne(x => x.Country)
                .WithMany()
                .HasForeignKey(x => x.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Qualifications)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.WorkHistories)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Referees)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Skills)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Interests)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Notes)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Applications)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.SegmentMemberships)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EngagementEvents)
                .WithOne(x => x.JobCandidate)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.TalentPoolSource).HasConversion<int>();
            entity.Property(x => x.TalentPoolStatus).HasConversion<int>();
            entity.HasIndex(x => x.TalentPoolStatus).HasDatabaseName("IX_JobCandidate_TalentPoolStatus");
            entity.HasIndex(x => x.TalentPoolReviewDate).HasDatabaseName("IX_JobCandidate_TalentPoolReviewDate");
            entity.HasIndex(x => x.LastEngagedDate).HasDatabaseName("IX_JobCandidate_LastEngagedDate");
        });

        // ---- CandidateTalentSegment ----
        builder.Entity<CandidateTalentSegment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Name }).HasDatabaseName("IX_CandidateTalentSegment_Tenant_Name");
            entity.HasIndex(x => x.IsActive).HasDatabaseName("IX_CandidateTalentSegment_IsActive");
        });

        // ---- CandidateSegmentMembership ----
        builder.Entity<CandidateSegmentMembership>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateSegmentMembership_CandidateId");
            entity.HasIndex(x => x.SegmentId).HasDatabaseName("IX_CandidateSegmentMembership_SegmentId");
            entity.HasIndex(x => new { x.JobCandidateId, x.SegmentId })
                .HasDatabaseName("IX_CandidateSegmentMembership_Unique");

            entity.HasOne(x => x.Segment)
                .WithMany(x => x.Memberships)
                .HasForeignKey(x => x.SegmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- CandidateEngagementEvent ----
        builder.Entity<CandidateEngagementEvent>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateEngagementEvent_CandidateId");
            entity.HasIndex(x => x.EventDate).HasDatabaseName("IX_CandidateEngagementEvent_EventDate");
            entity.Property(x => x.EventType).HasConversion<int>();
        });

        builder.Entity<JobCandidateQualification>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateQualification_CandidateId");
            entity.HasIndex(x => x.QualificationId).HasDatabaseName("IX_CandidateQualification_QualificationId");

            entity.Property(x => x.QualificationType).HasConversion<int>();

            entity.HasOne(x => x.JobCandidate)
                .WithMany(x => x.Qualifications)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Qualification)
                .WithMany()
                .HasForeignKey(x => x.QualificationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobCandidateWorkHistory>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateWorkHistory_CandidateId");

            entity.HasOne(x => x.JobCandidate)
                .WithMany(x => x.WorkHistories)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobCandidateReferee>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateReferee_CandidateId");

            entity.HasOne(x => x.JobCandidate)
                .WithMany(x => x.Referees)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobCandidateSkill>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateSkill_CandidateId");
            entity.HasIndex(x => x.SkillId).HasDatabaseName("IX_CandidateSkill_SkillId");

            entity.Property(x => x.Proficiency).HasConversion<int>();

            entity.HasOne(x => x.JobCandidate)
                .WithMany(x => x.Skills)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Skill)
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobCandidateInterest>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateInterest_CandidateId");

            entity.HasOne(x => x.JobCandidate)
                .WithMany(x => x.Interests)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobCandidateDocument>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateDocument_CandidateId");

            entity.Property(x => x.DocumentType).HasConversion<int>();

            entity.HasOne(x => x.JobCandidate)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobCandidateNote>(entity =>
        {
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_CandidateNote_CandidateId");

            entity.HasOne(x => x.JobCandidate)
                .WithMany(x => x.Notes)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- JobApplication ----
        builder.Entity<JobApplication>(entity =>
        {
            entity.HasIndex(x => x.ApplicationNumber).HasDatabaseName("IX_JobApplication_Number");
            entity.HasIndex(x => x.JobVacancyId).HasDatabaseName("IX_JobApplication_VacancyId");
            entity.HasIndex(x => x.JobCandidateId).HasDatabaseName("IX_JobApplication_CandidateId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_JobApplication_Status");
            entity.HasIndex(x => x.ApplicationDate).HasDatabaseName("IX_JobApplication_Date");
            entity.HasIndex(x => new { x.TenantId, x.ApplicationNumber })
                .IsUnique().HasDatabaseName("IX_JobApplication_Tenant_Number");
            // One application per candidate per vacancy
            entity.HasIndex(x => new { x.JobVacancyId, x.JobCandidateId })
                .IsUnique().HasDatabaseName("IX_JobApplication_Vacancy_Candidate");

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Source).HasConversion<int>();
            entity.Property(x => x.DecisionSource).HasConversion<int?>();
            entity.Property(x => x.WaitlistReason).HasMaxLength(1000);
            entity.Property(x => x.ShortlistingNotes).HasMaxLength(2000);

            entity.HasOne(x => x.JobVacancy)
                .WithMany(x => x.JobApplications)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobCandidate)
                .WithMany(x => x.Applications)
                .HasForeignKey(x => x.JobCandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobPosting)
                .WithMany()
                .HasForeignKey(x => x.JobPostingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ShortlistedBy)
                .WithMany()
                .HasForeignKey(x => x.ShortlistedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RejectedBy)
                .WithMany()
                .HasForeignKey(x => x.RejectedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.StageHistories)
                .WithOne(x => x.JobApplication)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.TestResults)
                .WithOne(x => x.JobApplication)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.InterviewSlots)
                .WithOne(x => x.JobApplication)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Communications)
                .WithOne(x => x.JobApplication)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            // New enterprise fields
            entity.Property(x => x.AggregatedReviewScore).HasColumnType("decimal(5,2)");

            entity.HasOne(x => x.InternalEmployee)
                .WithMany()
                .HasForeignKey(x => x.InternalEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobApplicationStageHistory>(entity =>
        {
            entity.HasIndex(x => x.JobApplicationId).HasDatabaseName("IX_StageHistory_ApplicationId");
            entity.HasIndex(x => x.PipelineStageId).HasDatabaseName("IX_StageHistory_StageId");
            entity.HasIndex(x => x.IsCurrent).HasDatabaseName("IX_StageHistory_IsCurrent");
            entity.HasIndex(x => new { x.JobApplicationId, x.IsCurrent })
                .HasDatabaseName("IX_StageHistory_Application_Current");

            entity.Property(x => x.ExitReason).HasConversion<int>();

            entity.HasOne(x => x.JobApplication)
                .WithMany(x => x.StageHistories)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PipelineStage)
                .WithMany()
                .HasForeignKey(x => x.PipelineStageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MovedBy)
                .WithMany()
                .HasForeignKey(x => x.MovedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobApplicantTestResult>(entity =>
        {
            entity.HasIndex(x => x.JobApplicationId).HasDatabaseName("IX_TestResult_ApplicationId");
            entity.HasIndex(x => x.TestType).HasDatabaseName("IX_TestResult_TestType");
            entity.HasIndex(x => x.TestDate).HasDatabaseName("IX_TestResult_Date");

            entity.Property(x => x.TestType).HasConversion<int>();
            entity.Ignore(x => x.ScorePercentage);

            entity.HasOne(x => x.JobApplication)
                .WithMany(x => x.TestResults)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InvigilatedBy)
                .WithMany()
                .HasForeignKey(x => x.InvigilatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.MarkedBy)
                .WithMany()
                .HasForeignKey(x => x.MarkedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobApplicantCommunication>(entity =>
        {
            entity.HasIndex(x => x.JobApplicationId).HasDatabaseName("IX_Communication_ApplicationId");
            entity.HasIndex(x => x.SentAt).HasDatabaseName("IX_Communication_SentAt");
            entity.HasIndex(x => x.Type).HasDatabaseName("IX_Communication_Type");

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Direction).HasConversion<int>();

            entity.HasOne(x => x.JobApplication)
                .WithMany(x => x.Communications)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SentBy)
                .WithMany()
                .HasForeignKey(x => x.SentById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- ShortlistDecisionLog ----
        builder.Entity<ShortlistDecisionLog>(entity =>
        {
            entity.HasIndex(x => x.JobApplicationId).HasDatabaseName("IX_ShortlistDecisionLog_ApplicationId");
            entity.HasIndex(x => x.DecisionType).HasDatabaseName("IX_ShortlistDecisionLog_DecisionType");
            entity.HasIndex(x => x.DecisionAt).HasDatabaseName("IX_ShortlistDecisionLog_DecisionAt");

            entity.Property(x => x.DecisionType).HasConversion<int>();
            entity.Property(x => x.ApplicationNumber).HasMaxLength(50);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.AutoScoreAtDecision).HasColumnType("decimal(18,4)");

            entity.HasOne(x => x.JobApplication)
                .WithMany(x => x.DecisionLogs)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DecisionBy)
                .WithMany()
                .HasForeignKey(x => x.DecisionById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- ShortlistReview ----
        builder.Entity<ShortlistReview>(entity =>
        {
            entity.HasIndex(x => x.JobApplicationId).HasDatabaseName("IX_ShortlistReview_ApplicationId");
            entity.HasIndex(x => x.ReviewerId).HasDatabaseName("IX_ShortlistReview_ReviewerId");

            entity.Property(x => x.Score).HasColumnType("decimal(5,2)");
            entity.Property(x => x.Notes).HasMaxLength(2000);

            entity.HasOne(x => x.JobApplication)
                .WithMany(x => x.ShortlistReviews)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Reviewer)
                .WithMany()
                .HasForeignKey(x => x.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Interview Question EmployeeBank ----
        builder.Entity<JobInterviewQuestionType>(entity =>
        {
            entity.HasIndex(x => x.TypeName).HasDatabaseName("IX_InterviewQuestionType_Name");
            entity.HasIndex(x => x.Code).HasDatabaseName("IX_InterviewQuestionType_Code");
            entity.HasIndex(x => x.IsActive).HasDatabaseName("IX_InterviewQuestionType_Active");

            entity.HasMany(x => x.QuestionDetails)
                .WithOne(x => x.QuestionType)
                .HasForeignKey(x => x.QuestionTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewQuestionDetail>(entity =>
        {
            entity.HasIndex(x => x.QuestionTypeId).HasDatabaseName("IX_InterviewQuestionDetail_TypeId");
            entity.HasIndex(x => x.IsActive).HasDatabaseName("IX_InterviewQuestionDetail_Active");

            entity.HasOne(x => x.QuestionType)
                .WithMany(x => x.QuestionDetails)
                .HasForeignKey(x => x.QuestionTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- JobInterview ----
        builder.Entity<JobInterview>(entity =>
        {
            entity.HasIndex(x => x.InterviewNumber).HasDatabaseName("IX_JobInterview_Number");
            entity.HasIndex(x => x.JobVacancyId).HasDatabaseName("IX_JobInterview_VacancyId");
            entity.HasIndex(x => x.Round).HasDatabaseName("IX_JobInterview_Round");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_JobInterview_Status");
            entity.HasIndex(x => x.ScheduledDate).HasDatabaseName("IX_JobInterview_ScheduledDate");

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.JobVacancy)
                .WithMany(x => x.Interviews)
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Interviewees)
                .WithOne(x => x.JobInterview)
                .HasForeignKey(x => x.JobInterviewId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Panelists)
                .WithOne(x => x.JobInterview)
                .HasForeignKey(x => x.JobInterviewId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ExternalPanelists)
                .WithOne(x => x.JobInterview)
                .HasForeignKey(x => x.JobInterviewId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Questions)
                .WithOne(x => x.JobInterview)
                .HasForeignKey(x => x.JobInterviewId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewPanelist>(entity =>
        {
            entity.HasIndex(x => x.JobInterviewId).HasDatabaseName("IX_InterviewPanelist_InterviewId");
            entity.HasIndex(x => x.EmployeeId).HasDatabaseName("IX_InterviewPanelist_EmployeeId");

            entity.Property(x => x.Role).HasConversion<int>();

            entity.HasOne(x => x.JobInterview)
                .WithMany(x => x.Panelists)
                .HasForeignKey(x => x.JobInterviewId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ScoreSummaries)
                .WithOne(x => x.InternalPanelist)
                .HasForeignKey(x => x.InternalPanelistId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewExternalPanelist>(entity =>
        {
            entity.HasIndex(x => x.JobInterviewId).HasDatabaseName("IX_ExtPanelist_InterviewId");
            entity.HasIndex(x => x.AssociateId).HasDatabaseName("IX_ExtPanelist_AssociateId");

            entity.Property(x => x.Role).HasConversion<int>();

            entity.HasOne(x => x.JobInterview)
                .WithMany(x => x.ExternalPanelists)
                .HasForeignKey(x => x.JobInterviewId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExternalAssociate)
                .WithMany()
                .HasForeignKey(x => x.AssociateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ScoreSummaries)
                .WithOne(x => x.ExternalPanelist)
                .HasForeignKey(x => x.ExternalPanelistId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewee>(entity =>
        {
            entity.HasIndex(x => x.JobInterviewId).HasDatabaseName("IX_Interviewee_InterviewId");
            entity.HasIndex(x => x.JobApplicationId).HasDatabaseName("IX_Interviewee_ApplicationId");

            entity.Property(x => x.Outcome).HasConversion<int>();

            entity.HasOne(x => x.JobInterview)
                .WithMany(x => x.Interviewees)
                .HasForeignKey(x => x.JobInterviewId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobApplication)
                .WithMany(x => x.InterviewSlots)
                .HasForeignKey(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Scores)
                .WithOne(x => x.JobInterviewee)
                .HasForeignKey(x => x.JobIntervieweeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewQuestion>(entity =>
        {
            entity.HasIndex(x => x.JobInterviewId).HasDatabaseName("IX_InterviewQuestion_InterviewId");
            entity.HasIndex(x => x.QuestionTypeId).HasDatabaseName("IX_InterviewQuestion_TypeId");

            entity.HasOne(x => x.JobInterview)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.JobInterviewId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.QuestionType)
                .WithMany()
                .HasForeignKey(x => x.QuestionTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.SelectedQuestions)
                .WithOne(x => x.JobInterviewQuestion)
                .HasForeignKey(x => x.JobInterviewQuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewSelectedQuestion>(entity =>
        {
            entity.HasIndex(x => x.JobInterviewQuestionId).HasDatabaseName("IX_SelectedQuestion_QuestionId");
            entity.HasIndex(x => x.QuestionDetailId).HasDatabaseName("IX_SelectedQuestion_DetailId");

            entity.HasOne(x => x.JobInterviewQuestion)
                .WithMany(x => x.SelectedQuestions)
                .HasForeignKey(x => x.JobInterviewQuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionDetailId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewScoreSummary>(entity =>
        {
            entity.HasIndex(x => x.JobIntervieweeId).HasDatabaseName("IX_ScoreSummary_IntervieweeId");
            entity.HasIndex(x => x.InternalPanelistId).HasDatabaseName("IX_ScoreSummary_InternalPanelistId");
            entity.HasIndex(x => x.ExternalPanelistId).HasDatabaseName("IX_ScoreSummary_ExternalPanelistId");

            entity.Property(x => x.Recommendation).HasConversion<int>();

            // Exactly one of InternalPanelistId / ExternalPanelistId must be populated
            entity.ToTable("JobInterviewScoreSummaries", t =>
                t.HasCheckConstraint(
                    "CK_ScoreSummary_SinglePanelist",
                    "([InternalPanelistId] IS NOT NULL AND [ExternalPanelistId] IS NULL) " +
                    "OR ([InternalPanelistId] IS NULL AND [ExternalPanelistId] IS NOT NULL)"));

            entity.HasOne(x => x.JobInterviewee)
                .WithMany(x => x.Scores)
                .HasForeignKey(x => x.JobIntervieweeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InternalPanelist)
                .WithMany(x => x.ScoreSummaries)
                .HasForeignKey(x => x.InternalPanelistId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExternalPanelist)
                .WithMany(x => x.ScoreSummaries)
                .HasForeignKey(x => x.ExternalPanelistId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ScoreEntries)
                .WithOne(x => x.ScoreSummary)
                .HasForeignKey(x => x.ScoreSummaryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewScoreEntry>(entity =>
        {
            entity.HasIndex(x => x.ScoreSummaryId).HasDatabaseName("IX_ScoreEntry_SummaryId");
            entity.HasIndex(x => x.QuestionDetailId).HasDatabaseName("IX_ScoreEntry_QuestionDetailId");

            entity.HasOne(x => x.ScoreSummary)
                .WithMany(x => x.ScoreEntries)
                .HasForeignKey(x => x.ScoreSummaryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionDetailId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JobInterviewScoreDraft>(entity =>
        {
            entity.HasIndex(x => x.JobIntervieweeId)
                  .HasDatabaseName("IX_ScoreDraft_IntervieweeId");
            entity.HasIndex(x => x.InternalPanelistId)
                  .HasDatabaseName("IX_ScoreDraft_InternalPanelistId");
            entity.HasIndex(x => new { x.JobIntervieweeId, x.InternalPanelistId, x.ExternalPanelistId })
                  .HasDatabaseName("IX_ScoreDraft_Panelist_Interviewee");

            entity.Property(x => x.DraftJson).HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.JobInterview)
                  .WithMany()
                  .HasForeignKey(x => x.JobInterviewId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Interviewee)
                  .WithMany()
                  .HasForeignKey(x => x.JobIntervieweeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InternalPanelist)
                  .WithMany()
                  .HasForeignKey(x => x.InternalPanelistId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExternalPanelist)
                  .WithMany()
                  .HasForeignKey(x => x.ExternalPanelistId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- JobOffer ----
        builder.Entity<JobOffer>(entity =>
        {
            entity.HasIndex(x => x.OfferNumber).HasDatabaseName("IX_JobOffer_Number");
            entity.HasIndex(x => x.JobApplicationId).HasDatabaseName("IX_JobOffer_ApplicationId");
            entity.HasIndex(x => x.OfferStatus).HasDatabaseName("IX_JobOffer_Status");
            entity.HasIndex(x => new { x.TenantId, x.OfferNumber })
                .IsUnique().HasDatabaseName("IX_JobOffer_Tenant_Number");

            entity.Property(x => x.OfferStatus).HasConversion<int>();
            entity.Property(x => x.EmploymentType).HasConversion<int>();
            entity.Property(x => x.BaseSalary).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Bonus).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Commission).HasColumnType("decimal(18,2)");

            // One application → one offer (principal side is JobOffer)
            entity.HasOne(x => x.Application)
                .WithOne(x => x.Offer)
                .HasForeignKey<JobOffer>(x => x.JobApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LocationLevel)
                .WithMany()
                .HasForeignKey(x => x.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreparedBy)
                .WithMany()
                .HasForeignKey(x => x.PreparedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Self-referencing for counter-offer revisions
            entity.HasOne(x => x.PreviousOffer)
                .WithMany()
                .HasForeignKey(x => x.PreviousOfferId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Benefits)
                .WithOne(x => x.JobOffer)
                .HasForeignKey(x => x.JobOfferId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OfferCandidateToken>(entity =>
        {
            entity.HasIndex(x => x.Token).IsUnique().HasDatabaseName("IX_OfferCandidateToken_Token");
            entity.HasIndex(x => x.JobOfferId).HasDatabaseName("IX_OfferCandidateToken_OfferId");

            entity.HasOne(x => x.JobOffer)
                .WithMany()
                .HasForeignKey(x => x.JobOfferId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobOfferBenefit>(entity =>
        {
            entity.HasIndex(x => x.JobOfferId).HasDatabaseName("IX_JobOfferBenefit_OfferId");
            entity.HasIndex(x => new { x.JobOfferId, x.DisplayOrder }).HasDatabaseName("IX_JobOfferBenefit_Offer_Order");

            entity.HasOne(x => x.JobOffer)
                .WithMany(x => x.Benefits)
                .HasForeignKey(x => x.JobOfferId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- JobHireRecord ----
        builder.Entity<JobHireRecord>(entity =>
        {
            entity.HasIndex(x => x.HireNumber).HasDatabaseName("IX_JobHireRecord_Number");
            entity.HasIndex(x => x.ApplicationId).HasDatabaseName("IX_JobHireRecord_ApplicationId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_JobHireRecord_Status");
            entity.HasIndex(x => new { x.TenantId, x.HireNumber })
                .IsUnique().HasDatabaseName("IX_JobHireRecord_Tenant_Number");

            entity.Property(x => x.Status).HasConversion<int>();

            // One application → one hire record (principal side is JobHireRecord)
            entity.HasOne(x => x.Application)
                .WithOne(x => x.HireRecord)
                .HasForeignKey<JobHireRecord>(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Offer)
                .WithMany()
                .HasForeignKey(x => x.OfferId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConfirmedBy)
                .WithMany()
                .HasForeignKey(x => x.ConfirmedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- PreEmploymentCheck ----
        builder.Entity<PreEmploymentCheck>(entity =>
        {
            entity.HasIndex(x => x.JobOfferId).HasDatabaseName("IX_PreEmpCheck_OfferId");
            entity.HasIndex(x => x.OverallStatus).HasDatabaseName("IX_PreEmpCheck_Status");

            entity.Property(x => x.OverallStatus).HasConversion<int>();

            entity.HasOne(x => x.JobOffer)
                .WithOne(x => x.PreEmploymentCheck)
                .HasForeignKey<PreEmploymentCheck>(x => x.JobOfferId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CoordinatedBy)
                .WithMany()
                .HasForeignKey(x => x.CoordinatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Items)
                .WithOne(x => x.PreEmploymentCheck)
                .HasForeignKey(x => x.PreEmploymentCheckId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PreEmploymentCheckItem>(entity =>
        {
            entity.HasIndex(x => x.PreEmploymentCheckId).HasDatabaseName("IX_PreEmpCheckItem_CheckId");
            entity.HasIndex(x => x.CheckType).HasDatabaseName("IX_PreEmpCheckItem_Type");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_PreEmpCheckItem_Status");

            entity.Property(x => x.CheckType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.PreEmploymentCheck)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PreEmploymentCheckId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewedBy)
                .WithMany()
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            // One check item → one reference response (FK lives on ReferenceCheckResponse)
            entity.HasOne(x => x.ReferenceResponse)
                .WithOne(x => x.CheckItem)
                .HasForeignKey<ReferenceCheckResponse>(x => x.CheckItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReferenceCheckResponse>(entity =>
        {
            entity.HasIndex(x => x.CheckItemId).HasDatabaseName("IX_RefCheckResponse_CheckItemId");
            entity.HasIndex(x => x.RefereeId).HasDatabaseName("IX_RefCheckResponse_RefereeId");

            entity.Property(x => x.ResponseMethod).HasConversion<int>();
            entity.Property(x => x.OverallRating).HasConversion<int>();

            entity.HasOne(x => x.Referee)
                .WithMany()
                .HasForeignKey(x => x.RefereeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- PreEmploymentCheckTemplate ----
        builder.Entity<PreEmploymentCheckTemplate>(entity =>
        {
            entity.HasIndex(x => x.TenantId).HasDatabaseName("IX_PreEmpCheckTemplate_TenantId");
            entity.HasIndex(x => new { x.TenantId, x.Name }).HasDatabaseName("IX_PreEmpCheckTemplate_TenantName").IsUnique();

            entity.HasMany(x => x.Items)
                .WithOne(x => x.Template)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PreEmploymentCheckTemplateItem>(entity =>
        {
            entity.HasIndex(x => x.TemplateId).HasDatabaseName("IX_PreEmpCheckTemplateItem_TemplateId");
            entity.HasIndex(x => x.CheckType).HasDatabaseName("IX_PreEmpCheckTemplateItem_Type");

            entity.Property(x => x.CheckType).HasConversion<int>();

            entity.HasOne(x => x.Template)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Onboarding ----
        builder.Entity<OnboardingPlanTemplate>(entity =>
        {
            entity.HasIndex(x => x.IsDefault).HasDatabaseName("IX_OnboardingTemplate_IsDefault");
            entity.HasIndex(x => x.IsActive).HasDatabaseName("IX_OnboardingTemplate_Active");

            entity.HasMany(x => x.TaskTemplates)
                .WithOne(x => x.PlanTemplate)
                .HasForeignKey(x => x.PlanTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OnboardingTaskTemplate>(entity =>
        {
            entity.HasIndex(x => x.PlanTemplateId).HasDatabaseName("IX_OnboardingTaskTemplate_PlanId");
            entity.HasIndex(x => x.OwnerPositionId).HasDatabaseName("IX_OnboardingTaskTemplate_OwnerPositionId");

            entity.Property(x => x.Category).HasConversion<int>();

            entity.HasOne(x => x.PlanTemplate)
                .WithMany(x => x.TaskTemplates)
                .HasForeignKey(x => x.PlanTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OwnerPosition)
                .WithMany()
                .HasForeignKey(x => x.OwnerPositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OnboardingPlan>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId).HasDatabaseName("IX_OnboardingPlan_EmployeeId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_OnboardingPlan_Status");

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany(e => e.OnboardingPlans)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TemplatePlan)
                .WithMany()
                .HasForeignKey(x => x.TemplatePlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedBuddy)
                .WithMany()
                .HasForeignKey(x => x.AssignedBuddyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OnboardingCoordinator)
                .WithMany()
                .HasForeignKey(x => x.OnboardingCoordinatorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Tasks)
                .WithOne(x => x.OnboardingPlan)
                .HasForeignKey(x => x.OnboardingPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Assets)
                .WithOne(x => x.OnboardingPlan)
                .HasForeignKey(x => x.OnboardingPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OnboardingTask>(entity =>
        {
            entity.HasIndex(x => x.OnboardingPlanId).HasDatabaseName("IX_OnboardingTask_PlanId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_OnboardingTask_Status");
            entity.HasIndex(x => x.DueDate).HasDatabaseName("IX_OnboardingTask_DueDate");

            entity.Property(x => x.Category).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.OnboardingPlan)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.OnboardingPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TaskTemplate)
                .WithMany()
                .HasForeignKey(x => x.TaskTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedTo)
                .WithMany()
                .HasForeignKey(x => x.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedOrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.AssignedOrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OwnerPosition)
                .WithMany()
                .HasForeignKey(x => x.OwnerPositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CompletedBy)
                .WithMany()
                .HasForeignKey(x => x.CompletedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.VerifiedBy)
                .WithMany()
                .HasForeignKey(x => x.VerifiedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Comments)
                .WithOne(x => x.Task)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OnboardingTaskComment>(entity =>
        {
            entity.HasIndex(x => x.TaskId).HasDatabaseName("IX_OnboardingTaskComment_TaskId");

            entity.HasOne(x => x.Task)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Author)
                .WithMany()
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OnboardingAsset>(entity =>
        {
            entity.HasIndex(x => x.OnboardingPlanId).HasDatabaseName("IX_OnboardingAsset_PlanId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_OnboardingAsset_Status");

            entity.Property(x => x.AssetType).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.OnboardingPlan)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.OnboardingPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ProvisionedBy)
                .WithMany()
                .HasForeignKey(x => x.ProvisionedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- ProbationPeriod ----
        builder.Entity<ProbationPeriod>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId).HasDatabaseName("IX_ProbationPeriod_EmployeeId");
            entity.HasIndex(x => x.ContractDetailId).HasDatabaseName("IX_ProbationPeriod_ContractDetailId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_ProbationPeriod_Status");

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany(e => e.ProbationPeriods)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EmployeeContractDetail)
                .WithMany()
                .HasForeignKey(x => x.ContractDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Reviews)
                .WithOne(x => x.ProbationPeriod)
                .HasForeignKey(x => x.ProbationPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProbationReview>(entity =>
        {
            entity.HasIndex(x => x.ProbationPeriodId).HasDatabaseName("IX_ProbationReview_ProbationId");
            entity.HasIndex(x => x.ScheduledDate).HasDatabaseName("IX_ProbationReview_ScheduledDate");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_ProbationReview_Status");

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.PerformanceRating).HasConversion<int>();
            entity.Property(x => x.ConductRating).HasConversion<int>();
            entity.Property(x => x.AttitudeRating).HasConversion<int>();
            entity.Property(x => x.Recommendation).HasConversion<int>();

            entity.HasOne(x => x.ProbationPeriod)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.ProbationPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewedBy)
                .WithMany()
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SecondReviewer)
                .WithMany()
                .HasForeignKey(x => x.SecondReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HrApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.HrApprovedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProbationExtension>(entity =>
        {
            entity.HasIndex(x => x.ProbationPeriodId).HasDatabaseName("IX_ProbationExtension_ProbationId");
            entity.HasIndex(x => x.ExtendedDate).HasDatabaseName("IX_ProbationExtension_ExtendedDate");

            entity.HasOne(x => x.ProbationPeriod)
                .WithMany(x => x.Extensions)
                .HasForeignKey(x => x.ProbationPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExtendedBy)
                .WithMany()
                .HasForeignKey(x => x.ExtendedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR SEPARATION, CLEARANCE & EXIT CONFIGURATION (area 9b)
        // =====================================================

        builder.Entity<EmployeeSeparation>(entity =>
        {
            // The number is unique per tenant, and the filter is not optional: DeleteAsync here is
            // a soft delete, and a soft-deleted row still occupies an unfiltered unique index — the
            // area-13 lesson, which cost five faces there before it was understood.
            entity.HasIndex(x => new { x.TenantId, x.SeparationNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("UX_EmployeeSeparation_Tenant_Number");

            entity.HasIndex(x => x.EmployeeId).HasDatabaseName("IX_EmployeeSeparation_EmployeeId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_EmployeeSeparation_Status");
            entity.HasIndex(x => x.SeparationType).HasDatabaseName("IX_EmployeeSeparation_Type");
            entity.HasIndex(x => x.EffectiveDate).HasDatabaseName("IX_EmployeeSeparation_EffectiveDate");
            entity.HasIndex(x => x.DisciplinaryActionId).HasDatabaseName("IX_EmployeeSeparation_DisciplinaryActionId");

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.SeparationType).HasConversion<int>();
            entity.Property(x => x.ReasonCategory).HasConversion<int>();

            // All four employee navigations are configured explicitly. An unpaired navigation left
            // to convention mints a duplicate shadow FK column (EmployeeId1, EmployeeId2, …) —
            // see the HR sweep that removed a batch of those in 2026-08.
            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InitiatedBy)
                .WithMany()
                .HasForeignKey(x => x.InitiatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CancelledBy)
                .WithMany()
                .HasForeignKey(x => x.CancelledById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SubmittedBy)
                .WithMany()
                .HasForeignKey(x => x.SubmittedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RejectedBy)
                .WithMany()
                .HasForeignKey(x => x.RejectedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NoticeDecidedBy)
                .WithMany()
                .HasForeignKey(x => x.NoticeDecidedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents)
                .WithOne(x => x.Separation)
                .HasForeignKey(x => x.SeparationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SeparationExitInterview>(entity =>
        {
            // One interview per separation. Filtered so a soft-deleted record does not block a
            // replacement being taken.
            entity.HasIndex(x => x.SeparationId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("UX_SeparationExitInterview_SeparationId");

            entity.HasIndex(x => x.PrimaryReason).HasDatabaseName("IX_SeparationExitInterview_PrimaryReason");
            entity.Property(x => x.PrimaryReason).HasConversion<int>();

            entity.HasOne(x => x.Separation)
                .WithMany()
                .HasForeignKey(x => x.SeparationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConductedBy)
                .WithMany()
                .HasForeignKey(x => x.ConductedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RecordedBy)
                .WithMany()
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SeparationReminderRun>(entity =>
        {
            entity.HasIndex(x => x.StartedAt).HasDatabaseName("IX_SeparationReminderRun_StartedAt");
            entity.Property(x => x.Trigger).HasMaxLength(30);

            entity.HasMany(x => x.DispatchLogs)
                .WithOne(x => x.Run)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SeparationReminderDispatchLog>(entity =>
        {
            entity.HasIndex(x => x.RunId).HasDatabaseName("IX_SeparationReminderDispatch_RunId");
            entity.HasIndex(x => x.EmployeeId).HasDatabaseName("IX_SeparationReminderDispatch_EmployeeId");
            entity.HasIndex(x => x.Kind).HasDatabaseName("IX_SeparationReminderDispatch_Kind");

            // The dedupe key is looked up on every sweep, for every candidate — the one index that
            // decides whether a daily pass over a whole workforce is cheap or not.
            entity.HasIndex(x => new { x.TenantId, x.DedupeKey })
                .HasDatabaseName("IX_SeparationReminderDispatch_Tenant_DedupeKey");

            entity.HasOne(x => x.Run)
                .WithMany(x => x.DispatchLogs)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SeparationSettlement>(entity =>
        {
            // One settlement per separation. Filtered, so a soft-deleted draft does not block a
            // replacement being prepared.
            entity.HasIndex(x => x.SeparationId)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("UX_SeparationSettlement_SeparationId");

            entity.Property(x => x.CurrencyCode).HasMaxLength(3);

            entity.HasOne(x => x.Separation)
                .WithMany()
                .HasForeignKey(x => x.SeparationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreparedBy)
                .WithMany()
                .HasForeignKey(x => x.PreparedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FinalisedBy)
                .WithMany()
                .HasForeignKey(x => x.FinalisedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.ReviewOutcome).HasConversion<int>();

            entity.HasOne(x => x.ReviewedBy)
                .WithMany()
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Settlement)
                .HasForeignKey(x => x.SettlementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SeparationSettlementLine>(entity =>
        {
            entity.HasIndex(x => x.SettlementId).HasDatabaseName("IX_SeparationSettlementLine_SettlementId");
            entity.HasIndex(x => x.Category).HasDatabaseName("IX_SeparationSettlementLine_Category");

            entity.Property(x => x.Category).HasConversion<int>();
            entity.Property(x => x.Computation).HasConversion<int>();

            entity.HasOne(x => x.Settlement)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.SettlementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SeparationClearanceTemplate>(entity =>
        {
            // One catalogue line per name per tenant. Filtered, because a soft-deleted row still
            // occupies an unfiltered unique index.
            entity.HasIndex(x => new { x.TenantId, x.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("UX_SeparationClearanceTemplate_Tenant_Name");

            entity.HasIndex(x => x.Kind).HasDatabaseName("IX_SeparationClearanceTemplate_Kind");
            entity.HasIndex(x => x.IsActive).HasDatabaseName("IX_SeparationClearanceTemplate_IsActive");

            entity.Property(x => x.Kind).HasConversion<int>();

            entity.HasOne(x => x.OwningOrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OwningOrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SeparationClearanceItem>(entity =>
        {
            entity.HasIndex(x => x.SeparationId).HasDatabaseName("IX_SeparationClearanceItem_SeparationId");
            entity.HasIndex(x => x.Status).HasDatabaseName("IX_SeparationClearanceItem_Status");
            entity.HasIndex(x => x.TemplateId).HasDatabaseName("IX_SeparationClearanceItem_TemplateId");

            entity.Property(x => x.Kind).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Separation)
                .WithMany(x => x.ClearanceItems)
                .HasForeignKey(x => x.SeparationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OwningOrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OwningOrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RecordedBy)
                .WithMany()
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeSeparationDocument>(entity =>
        {
            entity.HasIndex(x => x.SeparationId).HasDatabaseName("IX_EmployeeSeparationDocument_SeparationId");
            entity.HasIndex(x => x.Category).HasDatabaseName("IX_EmployeeSeparationDocument_Category");

            entity.Property(x => x.Category).HasConversion<int>();

            entity.HasOne(x => x.Separation)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.SeparationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR STAFF REQUISITION CONFIGURATION
        // =====================================================

        builder.Entity<StaffRequisition>(entity =>
        {
            entity.HasIndex(x => x.RequisitionNumber);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.RequestDate);
            entity.HasIndex(x => x.RequestedById);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.LocationId);

            // Requisition numbers must be unique per tenant. JobVacancy / JobCandidate / JobApplication
            // already have this; StaffRequisition did not, so the old Count()+1 generator could hand out
            // a duplicate REQ number and the database would happily store it.
            entity.HasIndex(x => new { x.TenantId, x.RequisitionNumber })
                .IsUnique().HasDatabaseName("IX_StaffRequisition_Tenant_Number");

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.ReplacementReason).HasConversion<int>();

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LocationLevel)
                .WithMany()
                .HasForeignKey(x => x.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Location)
                .WithMany()
                .HasForeignKey(x => x.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobDescription)
                .WithMany()
                .HasForeignKey(x => x.JobDescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReplacementForEmployee)
                .WithMany()
                .HasForeignKey(x => x.ReplacementForEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RequestedBy)
                .WithMany()
                .HasForeignKey(x => x.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CancelledBy)
                .WithMany()
                .HasForeignKey(x => x.CancelledById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.JobVacancy)
                .WithMany()
                .HasForeignKey(x => x.JobVacancyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Costs)
                .WithOne(x => x.Requisition)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Requisition)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.History)
                .WithOne(x => x.Requisition)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Comments)
                .WithOne(x => x.Requisition)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StaffRequisitionCost>(entity =>
        {
            entity.HasIndex(x => x.RequisitionId);
            entity.HasIndex(x => x.RecordedById);

            entity.Property(x => x.Category).HasConversion<int>();

            entity.HasOne(x => x.Requisition)
                .WithMany(x => x.Costs)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.RecordedBy)
                .WithMany()
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StaffRequisitionAttachment>(entity =>
        {
            entity.HasIndex(x => x.RequisitionId);
            entity.HasIndex(x => x.UploadedById);

            entity.HasOne(x => x.Requisition)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StaffRequisitionComment>(entity =>
        {
            entity.HasIndex(x => x.RequisitionId);
            entity.HasIndex(x => x.AuthorId);
            entity.HasIndex(x => x.ParentCommentId);

            entity.HasOne(x => x.Requisition)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ParentComment)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Author)
                .WithMany()
                .HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StaffRequisitionHistory>(entity =>
        {
            entity.HasIndex(x => x.RequisitionId);
            entity.HasIndex(x => x.ChangedById);
            entity.HasIndex(x => x.ActionDate);

            entity.Property(x => x.FromStatus).HasConversion<int>();
            entity.Property(x => x.ToStatus).HasConversion<int>();

            entity.HasOne(x => x.Requisition)
                .WithMany(x => x.History)
                .HasForeignKey(x => x.RequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ChangedBy)
                .WithMany()
                .HasForeignKey(x => x.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR STAFF DISCIPLINE CONFIGURATION
        // =====================================================

        // ---- StaffOffense ----
        builder.Entity<StaffOffense>(entity =>
        {
            entity.HasIndex(x => x.OffenseCode);
            entity.HasIndex(x => new { x.TenantId, x.OffenseCode }).IsUnique();
        });

        // ---- StaffOffenseProcedure ----
        builder.Entity<StaffOffenseProcedure>(entity =>
        {
            entity.HasIndex(x => x.OffenseId);
            entity.HasIndex(x => new { x.OffenseId, x.Sequence }).IsUnique();

            entity.HasOne(x => x.Offense)
                .WithMany(x => x.OffenseProcedures)
                .HasForeignKey(x => x.OffenseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplinaryActionType ----
        builder.Entity<StaffDisciplinaryActionType>(entity =>
        {
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

            entity.Property(x => x.DefaultFineAmount).HasColumnType("decimal(18,2)");
        });

        // ---- StaffDisciplinaryAction ----
        builder.Entity<StaffDisciplinaryAction>(entity =>
        {
            entity.HasIndex(x => x.CaseNumber);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.StaffOffenseId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Severity);
            entity.HasIndex(x => x.IncidentDate);
            entity.HasIndex(x => x.ReportedById);
            entity.HasIndex(x => new { x.TenantId, x.CaseNumber }).IsUnique();

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Severity).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.StaffOffense)
                .WithMany()
                .HasForeignKey(x => x.StaffOffenseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReportedBy)
                .WithMany()
                .HasForeignKey(x => x.ReportedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReportedTo)
                .WithMany()
                .HasForeignKey(x => x.ReportedToId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ActionType)
                .WithMany()
                .HasForeignKey(x => x.ActionTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DecisionBy)
                .WithMany()
                .HasForeignKey(x => x.DecisionById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ClosedBy)
                .WithMany()
                .HasForeignKey(x => x.ClosedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ActionSteps)
                .WithOne(x => x.DisciplinaryAction)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Witnesses)
                .WithOne(x => x.DisciplinaryAction)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Documents)
                .WithOne(x => x.DisciplinaryAction)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Notes)
                .WithOne(x => x.DisciplinaryAction)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Notifications)
                .WithOne(x => x.DisciplinaryAction)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.LegalReviews)
                .WithOne(x => x.DisciplinaryAction)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineInvestigation ----
        builder.Entity<StaffDisciplineInvestigation>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();
            entity.HasIndex(x => x.InvestigatorId);

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.Investigation)
                .HasForeignKey<StaffDisciplineInvestigation>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Investigator)
                .WithMany()
                .HasForeignKey(x => x.InvestigatorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineHearing ----
        builder.Entity<StaffDisciplineHearing>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();
            entity.HasIndex(x => x.HearingOfficerId);

            entity.Property(x => x.RepresentativeType).HasConversion<int>();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.Hearing)
                .HasForeignKey<StaffDisciplineHearing>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HearingOfficer)
                .WithMany()
                .HasForeignKey(x => x.HearingOfficerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RepresentativeEmployee)
                .WithMany()
                .HasForeignKey(x => x.RepresentativeEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineWarning ----
        builder.Entity<StaffDisciplineWarning>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();
            entity.HasIndex(x => x.WarningType);

            entity.Property(x => x.WarningType).HasConversion<int>();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.Warning)
                .HasForeignKey<StaffDisciplineWarning>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineSuspension ----
        builder.Entity<StaffDisciplineSuspension>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.Suspension)
                .HasForeignKey<StaffDisciplineSuspension>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineFine ----
        builder.Entity<StaffDisciplineFine>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();
            entity.HasIndex(x => x.FinePaymentStatus);

            entity.Property(x => x.FinePaymentStatus).HasConversion<int>();
            entity.Property(x => x.FineAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.FinePaidAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.Fine)
                .HasForeignKey<StaffDisciplineFine>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineActionStep ----
        builder.Entity<StaffDisciplineActionStep>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId);
            entity.HasIndex(x => x.OffenseProcedureId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.DisciplinaryActionId, x.OffenseProcedureId }).IsUnique();

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithMany(x => x.ActionSteps)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OffenseProcedure)
                .WithMany()
                .HasForeignKey(x => x.OffenseProcedureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ActionedBy)
                .WithMany()
                .HasForeignKey(x => x.ActionedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineWitness ----
        builder.Entity<StaffDisciplineWitness>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId);
            entity.HasIndex(x => x.EmployeeId);

            entity.HasOne(x => x.DisciplinaryAction)
                .WithMany(x => x.Witnesses)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineDocument ----
        builder.Entity<StaffDisciplineDocument>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId);
            entity.HasIndex(x => x.ActionStepId);
            entity.HasIndex(x => x.AppealId);
            entity.HasIndex(x => x.Scope);
            entity.HasIndex(x => x.Category);

            entity.Property(x => x.Scope).HasConversion<int>();
            entity.Property(x => x.Category).HasConversion<int>();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ActionStep)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.ActionStepId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Appeal)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.AppealId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineNote ----
        builder.Entity<StaffDisciplineNote>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId);
            entity.HasIndex(x => x.CreatedByEmployeeId);
            entity.HasIndex(x => x.NoteDate);

            entity.HasOne(x => x.DisciplinaryAction)
                .WithMany(x => x.Notes)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByEmployee)
                .WithMany()
                .HasForeignKey(x => x.CreatedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineNotification ----
        builder.Entity<StaffDisciplineNotification>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId);
            entity.HasIndex(x => x.SentById);
            entity.HasIndex(x => x.NotificationType);

            entity.Property(x => x.NotificationType).HasConversion<int>();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SentBy)
                .WithMany()
                .HasForeignKey(x => x.SentById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineAppeal ----
        builder.Entity<StaffDisciplineAppeal>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.AppealStatus);

            entity.Property(x => x.AppealStatus).HasConversion<int>();
            entity.Property(x => x.AppealOutcome).HasConversion<int>();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.Appeal)
                .HasForeignKey<StaffDisciplineAppeal>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AppealOfficer)
                .WithMany()
                .HasForeignKey(x => x.AppealOfficerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AppealOutcomeBy)
                .WithMany()
                .HasForeignKey(x => x.AppealOutcomeById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineCorrectiveAction ----
        builder.Entity<StaffDisciplineCorrectiveAction>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.SupervisorId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.CorrectiveAction)
                .HasForeignKey<StaffDisciplineCorrectiveAction>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Items)
                .WithOne(x => x.CorrectiveAction)
                .HasForeignKey(x => x.CorrectiveActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineCorrectiveActionItem ----
        builder.Entity<StaffDisciplineCorrectiveActionItem>(entity =>
        {
            entity.HasIndex(x => x.CorrectiveActionId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.CorrectiveAction)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.CorrectiveActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineLegalReview ----
        builder.Entity<StaffDisciplineLegalReview>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId);
            entity.HasIndex(x => x.LegalRiskLevel);

            entity.Property(x => x.LegalRiskLevel).HasConversion<int>();
            entity.Property(x => x.LegalCostsIncurred).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.DisciplinaryAction)
                .WithMany(x => x.LegalReviews)
                .HasForeignKey(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReferredBy)
                .WithMany()
                .HasForeignKey(x => x.ReferredById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExternalCounsel)
                .WithMany()
                .HasForeignKey(x => x.ExternalCounselId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- EmployeeProfileChangeRequest (area 25 slice 12, D6) ----
        builder.Entity<EmployeeProfileChangeRequest>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReviewedBy)
                .WithMany()
                .HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            // The targeted account may be replaced or retired later; the request must survive
            // it, because the audit question is what was asked for at the time.
            entity.HasOne(x => x.BankDetail)
                .WithMany()
                .HasForeignKey(x => x.BankDetailId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- EmployeeProfileChangeItem ----
        builder.Entity<EmployeeProfileChangeItem>(entity =>
        {
            entity.HasIndex(x => x.RequestId);
            entity.HasIndex(x => x.Field);

            entity.Property(x => x.Field).HasConversion<int>();

            // Cascade: an item has no meaning apart from its request (the grievance-step rule).
            entity.HasOne(x => x.Request)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- HrLetterRequest (area 25 slice 12b, D7) ----
        builder.Entity<HrLetterRequest>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.LetterType);

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.LetterType).HasConversion<int>();

            // The frozen letter: unbounded, because a letter body is prose and a MaxLength here
            // would silently truncate the document somebody is about to hand to a bank.
            entity.Property(x => x.IssuedDocumentHtml).HasColumnType("nvarchar(max)");

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.IssuedBy)
                .WithMany()
                .HasForeignKey(x => x.IssuedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffGrievance (FR-HR-181) ----
        builder.Entity<StaffGrievance>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.GrievanceNumber }).IsUnique();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CurrentLevel);

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.CurrentLevel).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffGrievanceStep ----
        builder.Entity<StaffGrievanceStep>(entity =>
        {
            entity.HasIndex(x => x.GrievanceId);
            entity.HasIndex(x => x.AssignedToId);
            entity.HasIndex(x => x.Outcome);

            entity.Property(x => x.Level).HasConversion<int>();
            entity.Property(x => x.Outcome).HasConversion<int>();

            // Cascade from the grievance is deliberate here, unlike the Restrict used across the
            // disciplinary case: a step has no meaning apart from its grievance, whereas a
            // disciplinary sub-entity is a record in its own right that must survive.
            entity.HasOne(x => x.Grievance)
                .WithMany(x => x.Steps)
                .HasForeignKey(x => x.GrievanceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.AssignedTo)
                .WithMany()
                .HasForeignKey(x => x.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RespondedBy)
                .WithMany()
                .HasForeignKey(x => x.RespondedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Discipline reminder engine (area 9 slice 8) ----
        builder.Entity<DisciplineReminderRun>(e => e.HasIndex(x => new { x.TenantId, x.StartedAt }));
        builder.Entity<DisciplineReminderDispatchLog>(e =>
        {
            // The engine's send-once guarantee — a sweep claims a key before it publishes.
            e.HasIndex(x => new { x.TenantId, x.DedupeKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => x.RunId);

            e.HasOne(x => x.Run)
                .WithMany(x => x.DispatchLogs)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Oath of secrecy (area 15b slice 9, FR-HR-030) ----
        builder.Entity<EmployeeOathOfSecrecy>(e =>
        {
            // No unique index on the employee: a rehire swears again, so several per employee is
            // normal and the current one is the latest by SwornOn.
            e.HasIndex(x => new { x.TenantId, x.EmployeeId, x.SwornOn });

            e.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.WitnessedBy)
                .WithMany()
                .HasForeignKey(x => x.WitnessedById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RecordedBy)
                .WithMany()
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Probation confirming authority (area 15b slice 8a, decision D-2) ----
        builder.Entity<ProbationConfirmingAuthority>(e =>
        {
            // One rule per (unit, level) slot. ⚠ The filter MUST include IsDeleted: a soft delete
            // does not release a unique index, so without it one deleted rule would hold a slot no
            // live rule could ever occupy — the defect that cost area 13 five separate faces.
            e.HasIndex(x => new { x.TenantId, x.OrganizationUnitId, x.StaffLevelId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            e.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.StaffLevel)
                .WithMany()
                .HasForeignKey(x => x.StaffLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.AuthorityEmployee)
                .WithMany()
                .HasForeignKey(x => x.AuthorityEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Probation reminder engine (area 15b slice 7) ----
        builder.Entity<ProbationReminderRun>(e => e.HasIndex(x => new { x.TenantId, x.StartedAt }));
        builder.Entity<ProbationReminderDispatchLog>(e =>
        {
            // The engine's send-once guarantee — a sweep claims a key before it publishes.
            e.HasIndex(x => new { x.TenantId, x.DedupeKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => x.RunId);
            e.HasIndex(x => x.ProbationPeriodId);

            e.HasOne(x => x.Run)
                .WithMany(x => x.DispatchLogs)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Asset reminder engine (area 16 slice 9) ----
        builder.Entity<AssetReminderRun>(e => e.HasIndex(x => new { x.TenantId, x.StartedAt }));
        builder.Entity<AssetReminderDispatchLog>(e =>
        {
            // The engine's send-once guarantee — a sweep claims a key before it publishes.
            e.HasIndex(x => new { x.TenantId, x.DedupeKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => x.RunId);
            e.HasIndex(x => new { x.TenantId, x.AssetId });

            e.HasOne(x => x.Run)
                .WithMany(x => x.DispatchLogs)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Staff travel reminder engine (area 12 slice 5a) ----
        builder.Entity<StaffTravelReminderRun>(e => e.HasIndex(x => new { x.TenantId, x.StartedAt }));
        builder.Entity<StaffTravelReminderDispatchLog>(e =>
        {
            // The engine's send-once guarantee — a sweep claims a key before it publishes.
            e.HasIndex(x => new { x.TenantId, x.DedupeKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => x.RunId);

            e.HasOne(x => x.Run)
                .WithMany(x => x.DispatchLogs)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- StaffDisciplineTermination ----
        builder.Entity<StaffDisciplineTermination>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.FinalPaycheckAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.Termination)
                .HasForeignKey<StaffDisciplineTermination>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ---- StaffDisciplineSeparation ----
        builder.Entity<StaffDisciplineSeparation>(entity =>
        {
            entity.HasIndex(x => x.DisciplinaryActionId).IsUnique();

            entity.HasOne(x => x.DisciplinaryAction)
                .WithOne(x => x.Separation)
                .HasForeignKey<StaffDisciplineSeparation>(x => x.DisciplinaryActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ExitInterviewer)
                .WithMany()
                .HasForeignKey(x => x.ExitInterviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AccessRevokedBy)
                .WithMany()
                .HasForeignKey(x => x.AccessRevokedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // HR STAFF SAFETY / SHE (Safety, Health & Environment) CONFIGURATION
        // =====================================================
        // Relationships are discovered from the [ForeignKey] attributes + navigation
        // properties on the entities (EF also auto-indexes FK columns). Because many SHE
        // entities carry several FKs to Employee (ReportedBy / Supervisor / Investigator /
        // ClosedBy ...), the loop at the END of this block forces every relationship
        // declared on a HR.Safety entity to DeleteBehavior.Restrict, which prevents SQL
        // Server "multiple cascade paths" errors. Here we only add unique keys, a few
        // query indexes, and decimal precision.

        // A. Reference / lookup catalog ───────────────────────────────
        builder.Entity<SheIncidentType>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.IsActive);
        });
        builder.Entity<SheInjuryType>(e => e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique());
        builder.Entity<SheBodyPart>(e => e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique());
        builder.Entity<SheCorrectiveActionTemplate>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            e.HasIndex(x => x.Category);
        });
        builder.Entity<SheRegulatoryBody>(e => e.HasIndex(x => x.Domain));

        // B. Incident management ──────────────────────────────────────
        builder.Entity<SafetyIncident>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.IncidentNumber }).IsUnique();
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.Severity);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.IncidentDate);
        });

        // C. Hazard register & formal risk assessment ─────────────────
        builder.Entity<SheHazard>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Code });
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ResidualRiskLevel);
        });
        builder.Entity<SheRiskAssessment>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.AssessmentNumber }).IsUnique();
            e.HasIndex(x => x.Type);
            e.HasIndex(x => x.Status);
        });

        // D. Safety inspections & audits ──────────────────────────────
        builder.Entity<SheInspectionChecklist>(e => e.HasIndex(x => new { x.TenantId, x.ChecklistNumber }).IsUnique());
        builder.Entity<SafetyInspection>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.InspectionNumber }).IsUnique();
            e.HasIndex(x => x.Type);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.InspectionDate);
        });

        // E. Permit-to-work ───────────────────────────────────────────
        builder.Entity<ShePermitToWork>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.PermitNumber }).IsUnique();
            e.HasIndex(x => x.PermitType);
            e.HasIndex(x => x.Status);
        });

        // F. PPE management ───────────────────────────────────────────
        builder.Entity<PpeType>(e => e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique());
        builder.Entity<PpeInventory>(e => e.HasIndex(x => new { x.TenantId, x.ItemCode }).IsUnique());

        // G. Safety equipment ─────────────────────────────────────────
        builder.Entity<SafetyEquipment>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.EquipmentNumber }).IsUnique();
            e.HasIndex(x => x.Type);
            e.HasIndex(x => x.Status);
        });

        // H. Contractor SHE management ────────────────────────────────
        builder.Entity<SheContractor>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ContractorCode }).IsUnique();
            e.HasIndex(x => x.SheStatus);
        });
        builder.Entity<SheContractorInspection>(e => e.HasIndex(x => new { x.TenantId, x.InspectionNumber }).IsUnique());
        builder.Entity<SheContractorNonCompliance>(e => e.HasIndex(x => new { x.TenantId, x.NoticeNumber }).IsUnique());

        // I. SHE training & awareness ─────────────────────────────────
        builder.Entity<SheTrainingPlan>(e => e.HasIndex(x => new { x.TenantId, x.PlanNumber }).IsUnique());
        builder.Entity<SheTrainingProgram>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ProgramCode }).IsUnique();
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.Status);
        });

        // J. Waste management ─────────────────────────────────────────
        builder.Entity<SheWasteType>(e => e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique());
        builder.Entity<SheWasteDisposalRecord>(e => e.HasIndex(x => new { x.TenantId, x.RecordNumber }).IsUnique());

        // K. Environmental management ─────────────────────────────────
        builder.Entity<SheEnvironmentalIncident>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.IncidentNumber }).IsUnique();
            e.HasIndex(x => x.Type);
            e.HasIndex(x => x.Status);
        });
        builder.Entity<SheEnvironmentalMonitoringRecord>(e => e.HasIndex(x => new { x.TenantId, x.RecordNumber }).IsUnique());

        // L. Occupational health management ───────────────────────────
        builder.Entity<SheOccupationalHealthSurveillance>(e => e.HasIndex(x => new { x.TenantId, x.SurveillanceNumber }).IsUnique());
        builder.Entity<SheFirstAidStation>(e => e.HasIndex(x => new { x.TenantId, x.StationCode }).IsUnique());
        builder.Entity<SheWellnessProgram>(e => e.HasIndex(x => new { x.TenantId, x.ProgramCode }).IsUnique());

        // M. Emergency preparedness & response ────────────────────────
        builder.Entity<EmergencyPlan>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.PlanNumber }).IsUnique();
            e.HasIndex(x => x.Type);
        });
        builder.Entity<EmergencyDrill>(e => e.HasIndex(x => new { x.TenantId, x.DrillNumber }).IsUnique());
        builder.Entity<SheAssemblyPoint>(e =>
        {
            e.Property(x => x.GpsLatitude).HasColumnType("decimal(9,6)");
            e.Property(x => x.GpsLongitude).HasColumnType("decimal(9,6)");
        });

        // N. Regulatory compliance register ───────────────────────────
        builder.Entity<SheRegulatoryObligation>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ObligationCode }).IsUnique();
            e.HasIndex(x => x.Domain);
            e.HasIndex(x => x.ComplianceStatus);
        });

        // O. Safety signage register ──────────────────────────────────
        builder.Entity<SafetySign>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.SignCode }).IsUnique();
            e.HasIndex(x => x.SignType);
            e.HasIndex(x => x.Status);
        });

        // P. SHE performance metrics / KPIs ───────────────────────────
        builder.Entity<ShePerformanceSnapshot>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.SnapshotNumber }).IsUnique();
            e.HasIndex(x => new { x.Year, x.PeriodType, x.PeriodNumber });
        });

        // Q. Safety committee & meetings ──────────────────────────────
        builder.Entity<SafetyMeeting>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.MeetingNumber }).IsUnique();
            e.HasIndex(x => x.MeetingDate);
        });

        // R. Return-to-work plans ─────────────────────────────────────
        builder.Entity<SheReturnToWorkPlan>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.PlanNumber }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        // Staff movement reminder engine (area 8 slice 5) ─────────────
        builder.Entity<StaffMovementReminderRun>(e => e.HasIndex(x => new { x.TenantId, x.StartedAt }));
        builder.Entity<StaffMovementReminderDispatchLog>(e =>
        {
            // The engine's send-once guarantee — a sweep claims a key before it publishes.
            e.HasIndex(x => new { x.TenantId, x.DedupeKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        // S. SHE reminder engine ──────────────────────────────────────
        builder.Entity<SheReminderRun>(e => e.HasIndex(x => new { x.TenantId, x.StartedAt }));
        builder.Entity<SheReminderDispatchLog>(e =>
        {
            // The engine's send-once guarantee — sweeps claim a key before publishing.
            e.HasIndex(x => new { x.TenantId, x.DedupeKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        // T. SHE audit management ─────────────────────────────────────
        builder.Entity<SheAudit>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.AuditNumber }).IsUnique();
            e.HasIndex(x => x.Status);
        });
        builder.Entity<SheAuditFinding>(e => e.HasIndex(x => new { x.AuditId, x.FindingNumber }));

        // U. Stop-work authority ──────────────────────────────────────
        builder.Entity<SheStopWorkOrder>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        // V. Statutory incident submissions ───────────────────────────
        builder.Entity<SheStatutoryIncidentSubmission>(e => e.HasIndex(x => x.IncidentId));

        // W. SHE controlled document register ─────────────────────────
        builder.Entity<SheControlledDocument>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.DocumentNumber }).IsUnique();
            e.HasIndex(x => x.Status);
            // The review sweep's scan: Active documents by review date.
            e.HasIndex(x => new { x.TenantId, x.NextReviewDate });
        });

        // X. Environmental permit & licence register ──────────────────
        builder.Entity<SheEnvironmentalPermit>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.RegisterNumber }).IsUnique();
            e.HasIndex(x => x.Status);
            // The renewal sweep's scan (FR-ENV-018): live permits by expiry date.
            e.HasIndex(x => new { x.TenantId, x.ExpiryDate });
        });

        // Y. Environmental monitoring schedules ───────────────────────
        builder.Entity<SheEnvironmentalMonitoringSchedule>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ScheduleNumber }).IsUnique();
            // The monitoring sweep's scan (FR-ENV-024): active schedules by due date.
            e.HasIndex(x => new { x.TenantId, x.NextDueDate });
        });

        // Z. Regulatory updates register ──────────────────────────────
        builder.Entity<SheRegulatoryUpdate>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.UpdateNumber }).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Domain);
        });

        // AA. Sustainability initiatives ──────────────────────────────
        builder.Entity<SheSustainabilityInitiative>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.InitiativeNumber }).IsUnique();
            e.HasIndex(x => x.Category);
            e.HasIndex(x => x.Status);
        });

        // AB. Environmental compliance reviews ────────────────────────
        builder.Entity<SheEnvironmentalReview>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ReviewNumber }).IsUnique();
            e.HasIndex(x => x.Status);
            // The project-notification sweep's scan (FR-ENV-014).
            e.HasIndex(x => new { x.TenantId, x.PlannedStartDate });
        });
        builder.Entity<SheEnvironmentalReviewAction>(e => e.HasIndex(x => x.ReviewId));

        // AC. Monthly environmental reports ───────────────────────────
        builder.Entity<SheMonthlyEnvironmentalReport>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.ReportNumber }).IsUnique();
            // One report per period — regeneration reuses the row.
            e.HasIndex(x => new { x.TenantId, x.Year, x.Month }).IsUnique();
        });

        // ── Force every relationship declared on a HR.Safety entity to Restrict ──
        // Prevents multiple-cascade-path errors (many SHE FKs target Employee). Scoped
        // to the Safety namespace so existing modules' delete behaviour is untouched.
        foreach (var fk in builder.Model.GetEntityTypes()
                     .Where(t => t.ClrType?.Namespace == "ErpSystem.Core.Entities.HR.Safety")
                     .SelectMany(t => t.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // =====================================================
        // HR MISCELLANEOUS CONFIGURATION
        // =====================================================

        builder.Entity<Qualification>(entity =>
        {
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.ShortCode);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.Type).HasConversion<int>();
        });

        builder.Entity<OrganizationChartNode>(entity =>
        {
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.ParentNodeId);
            entity.HasIndex(x => x.Level);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ParentNode)
                .WithMany(x => x.ChildNodes)
                .HasForeignKey(x => x.ParentNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExternalAssociate>(entity =>
        {
            // ⚠ D-10, and the THIRD migration in this bundle for one trap: a uniqueness claim over a
            // soft-deleting store. `AssociateNumber` was indexed but NOT unique, and the generator
            // read the highest EXT-nnnn through the soft-delete filter, so a removed associate's
            // number was invisible and was minted again — 38 rows on DEFAULT carried EXT-0008, every
            // one of them deleted, and `GET number/{n}` resolved with a FirstOrDefault.
            //
            // The filter is `IsDeleted = 0` rather than none at all, for a reason the data settles:
            // those 38 duplicates are real rows and a full unique index cannot be built over them.
            // Uniqueness among LIVE rows is the claim the register actually makes; the generator not
            // reissuing is what keeps a dead number out of circulation.
            entity.HasIndex(x => new { x.TenantId, x.AssociateNumber })
                  .HasDatabaseName("IX_ExternalAssociates_Tenant_Number")
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => x.AssociateNumber);
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.IsActive);
        });
    }
private void ConfigureTrainingEntities(ModelBuilder builder)
    {
        // =====================================================
        // NUMBER SEQUENCE (reference-number generator backing store)
        // =====================================================
        builder.Entity<NumberSequence>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.SequenceKey, x.Year }).IsUnique();
            entity.Property(x => x.SequenceKey).HasMaxLength(50);
        });

        // =====================================================
        // TRAINING VENDOR
        // =====================================================
        builder.Entity<TrainingVendor>(entity =>
        {
            entity.HasIndex(x => x.VendorCode).IsUnique();
            entity.HasIndex(x => x.VendorType);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.IsPreferred);
            entity.HasIndex(x => x.AccreditationStatus);

            entity.Property(x => x.VendorType).HasConversion<int>();
            entity.Property(x => x.AccreditationStatus).HasConversion<int>();
            entity.Property(x => x.DefaultDailyRate).HasColumnType("decimal(18,2)");
        });

        // =====================================================
        // TRAINER PROFILE
        // =====================================================
        builder.Entity<TrainerProfile>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.VendorId);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.AverageRating).HasColumnType("decimal(3,2)");

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Vendor)
                .WithMany(x => x.Trainers)
                .HasForeignKey(x => x.VendorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINER SKILL
        // =====================================================
        builder.Entity<TrainerSkill>(entity =>
        {
            entity.HasIndex(x => new { x.TrainerProfileId, x.SkillId }).IsUnique();
            entity.HasIndex(x => x.SkillId);

            entity.Property(x => x.TrainerProficiency).HasConversion<int>();

            entity.HasOne(x => x.TrainerProfile)
                .WithMany(x => x.TrainerSkills)
                .HasForeignKey(x => x.TrainerProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Skill)
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINER AVAILABILITY
        // =====================================================
        builder.Entity<TrainerAvailability>(entity =>
        {
            entity.HasIndex(x => x.TrainerProfileId);
            entity.HasIndex(x => new { x.TrainerProfileId, x.FromDate, x.ToDate });

            entity.Property(x => x.EngagementType).HasConversion<int>();

            entity.HasOne(x => x.TrainerProfile)
                .WithMany(x => x.Availability)
                .HasForeignKey(x => x.TrainerProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================
        // TRAINING PROGRAM
        // =====================================================
        // TRAINING CATEGORY OPTION (user-configurable program categories)
        builder.Entity<TrainingCategoryOption>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        builder.Entity<TrainingProgramGroup>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => x.IsActive);
        });

        builder.Entity<TrainingProgram>(entity =>
        {
            entity.HasIndex(x => x.ProgramCode).IsUnique();
            entity.HasIndex(x => x.CategoryOptionId);
            entity.HasIndex(x => x.ProgramGroupId);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.Level);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Source).HasConversion<int>();
            entity.Property(x => x.Level).HasConversion<int>();
            entity.Property(x => x.CostPerParticipant).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.CategoryOption)
                .WithMany(x => x.Programs)
                .HasForeignKey(x => x.CategoryOptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ProgramGroup)
                .WithMany(x => x.Programs)
                .HasForeignKey(x => x.ProgramGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINING STATUS HISTORY (audit trail)
        // =====================================================
        builder.Entity<TrainingStatusHistory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
            entity.HasIndex(x => x.ChangedAt);
        });

        // =====================================================
        // TRAINING SERVICE BOND
        // =====================================================
        builder.Entity<TrainingServiceBond>(entity =>
        {
            entity.HasIndex(x => x.NominationId).IsUnique();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.BondAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.RepaymentAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Nomination)
                .WithOne(n => n.ServiceBond)
                .HasForeignKey<TrainingServiceBond>(x => x.NominationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AcceptanceRecordedBy)
                .WithMany()
                .HasForeignKey(x => x.AcceptanceRecordedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.WaivedBy)
                .WithMany()
                .HasForeignKey(x => x.WaivedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING MATERIAL
        // =====================================================
        builder.Entity<TrainingMaterial>(entity =>
        {
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Materials)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================
        // TRAINING SCHEDULE
        // =====================================================
        builder.Entity<TrainingSchedule>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ScheduleNumber }).IsUnique();
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.TrainerProfileId);
            entity.HasIndex(x => x.VendorId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.StartDate);
            entity.HasIndex(x => new { x.ProgramId, x.Status });

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.ActualCost).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Schedules)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TrainerProfile)
                .WithMany()
                .HasForeignKey(x => x.TrainerProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Vendor)
                .WithMany()
                .HasForeignKey(x => x.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TrainingBudget)
                .WithMany(x => x.Schedules)
                .HasForeignKey(x => x.TrainingBudgetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING SESSION
        // =====================================================
        builder.Entity<TrainingSession>(entity =>
        {
            entity.HasIndex(x => x.ScheduleId);
            entity.HasIndex(x => x.Date);

            entity.HasOne(x => x.Schedule)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================
        // TRAINING PROGRAM COMPETENCY
        // =====================================================
        builder.Entity<TrainingProgramCompetency>(entity =>
        {
            entity.HasIndex(x => new { x.ProgramId, x.CompetencyId }).IsUnique();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Competencies)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Competency)
                .WithMany()
                .HasForeignKey(x => x.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINING PROGRAM SKILL
        // =====================================================
        builder.Entity<TrainingProgramSkill>(entity =>
        {
            entity.HasIndex(x => new { x.ProgramId, x.SkillId }).IsUnique();

            entity.Property(x => x.TargetProficiency).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Skills)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Skill)
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINING NOMINATION
        // =====================================================
        builder.Entity<TrainingNomination>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.NominationNumber }).IsUnique();
            entity.HasIndex(x => x.ScheduleId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.NominationDate);
            entity.HasIndex(x => new { x.ScheduleId, x.EmployeeId });

            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.ActualCost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.EmployeeContribution).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Schedule)
                .WithMany(x => x.Nominations)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.NominatedBy)
                .WithMany()
                .HasForeignKey(x => x.NominatedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.TrainingNeedsAssessment)
                .WithMany()
                .HasForeignKey(x => x.TrainingNeedsAssessmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.SupervisorApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.SupervisorApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.HrApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.HrApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING COMPLETION
        // =====================================================
        builder.Entity<TrainingCompletion>(entity =>
        {
            entity.HasIndex(x => x.NominationId).IsUnique(); // one completion per nomination
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CompletionDate);

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.FinalScore).HasColumnType("decimal(5,2)");
            entity.Property(x => x.PreAssessmentScore).HasColumnType("decimal(5,2)");
            entity.Property(x => x.PostAssessmentScore).HasColumnType("decimal(5,2)");

            entity.HasOne(x => x.Nomination)
                .WithOne(x => x.CompletionRecord)
                .HasForeignKey<TrainingCompletion>(x => x.NominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINING ATTENDANCE
        // =====================================================
        builder.Entity<TrainingAttendance>(entity =>
        {
            entity.HasIndex(x => x.ScheduleId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.NominationId);
            entity.HasIndex(x => x.AttendanceDate);
            entity.HasIndex(x => new { x.ScheduleId, x.EmployeeId, x.AttendanceDate });

            entity.HasOne(x => x.Schedule)
                .WithMany(x => x.Attendance)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Nomination)
                .WithMany()
                .HasForeignKey(x => x.NominationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.MarkedBy)
                .WithMany()
                .HasForeignKey(x => x.MarkedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING FEEDBACK
        // =====================================================
        builder.Entity<TrainingFeedback>(entity =>
        {
            entity.HasIndex(x => x.ScheduleId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.NominationId);
            entity.HasIndex(x => new { x.ScheduleId, x.EmployeeId }).IsUnique();

            entity.HasOne(x => x.Schedule)
                .WithMany(x => x.Feedbacks)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Nomination)
                .WithMany()
                .HasForeignKey(x => x.NominationId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING FOLLOW-UP ASSESSMENT
        // =====================================================
        builder.Entity<TrainingFollowUpAssessment>(entity =>
        {
            entity.HasIndex(x => x.ScheduleId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.NominationId);
            entity.HasIndex(x => x.AssessmentType);
            entity.HasIndex(x => new { x.ScheduleId, x.EmployeeId, x.AssessmentType }).IsUnique();

            entity.Property(x => x.AssessmentType).HasConversion<int>();

            entity.HasOne(x => x.Schedule)
                .WithMany(x => x.FollowUpAssessments)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Nomination)
                .WithMany()
                .HasForeignKey(x => x.NominationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Manager)
                .WithMany()
                .HasForeignKey(x => x.ManagerId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING CERTIFICATE
        // =====================================================
        builder.Entity<TrainingCertificate>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.CertificateNumber }).IsUnique();
            entity.HasIndex(x => x.VerificationCode).IsUnique().HasFilter("[VerificationCode] IS NOT NULL");
            entity.HasIndex(x => x.NominationId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ExpiryDate);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Nomination)
                .WithMany()
                .HasForeignKey(x => x.NominationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreviousCertificate)
                .WithMany()
                .HasForeignKey(x => x.PreviousCertificateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.IssuedBy)
                .WithMany()
                .HasForeignKey(x => x.IssuedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // EMPLOYEE CERTIFICATE
        // =====================================================
        builder.Entity<EmployeeCertificate>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ExpiryDate);
            entity.HasIndex(x => x.IsVerified);

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Category).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.VerifiedBy)
                .WithMany()
                .HasForeignKey(x => x.VerifiedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // COMPLIANCE TRAINING REQUIREMENT
        // =====================================================
        builder.Entity<ComplianceTrainingRequirement>(entity =>
        {
            entity.HasIndex(x => x.RequirementCode).IsUnique();
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.OrganizationLevelId);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.Frequency).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // EMPLOYEE COMPLIANCE RECORD
        // =====================================================
        builder.Entity<EmployeeComplianceRecord>(entity =>
        {
            entity.HasIndex(x => new { x.EmployeeId, x.RequirementId }).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.NextDueDate);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Requirement)
                .WithMany(x => x.EmployeeRecords)
                .HasForeignKey(x => x.RequirementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FulfillingNomination)
                .WithMany()
                .HasForeignKey(x => x.FulfillingNominationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.ExemptedBy)
                .WithMany()
                .HasForeignKey(x => x.ExemptedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING BUDGET
        // =====================================================
        builder.Entity<TrainingBudget>(entity =>
        {
            entity.HasIndex(x => x.BudgetCode).IsUnique();
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.OrganizationUnitId, x.Year, x.Quarter });

            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.AllocatedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.CommittedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.SpentAmount).HasColumnType("decimal(18,2)");
            entity.Ignore(x => x.RemainingAmount);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING BUDGET TRANSACTION
        // =====================================================
        builder.Entity<TrainingBudgetTransaction>(entity =>
        {
            entity.HasIndex(x => x.BudgetId);
            entity.HasIndex(x => x.ScheduleId);
            entity.HasIndex(x => x.TransactionDate);

            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Budget)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.BudgetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Schedule)
                .WithMany()
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RecordedBy)
                .WithMany()
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING PLAN
        // =====================================================
        builder.Entity<TrainingPlan>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.PlanNumber }).IsUnique();
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.OrganizationUnitId);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.OrganizationLevel)
                .WithMany()
                .HasForeignKey(x => x.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING PLAN ITEM
        // =====================================================
        builder.Entity<TrainingPlanItem>(entity =>
        {
            entity.HasIndex(x => x.PlanId);
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.Quarter);

            entity.Property(x => x.EstimatedCost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ActualCost).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Plan)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.FulfilledBySchedule)
                .WithMany()
                .HasForeignKey(x => x.FulfilledByScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINING PLAN BUDGET LINE
        // =====================================================
        builder.Entity<TrainingPlanBudgetLine>(entity =>
        {
            entity.HasIndex(x => x.PlanId);

            entity.Property(x => x.BudgetedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ActualAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.CommittedAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Plan)
                .WithMany(x => x.BudgetLines)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // =====================================================
        // TRAINING NEEDS ASSESSMENT
        // =====================================================
        builder.Entity<TrainingNeedsAssessment>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.Priority);
            entity.HasIndex(x => x.Source);
            entity.HasIndex(x => new { x.EmployeeId, x.Year });

            entity.Property(x => x.Source).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.IdentifiedBy)
                .WithMany()
                .HasForeignKey(x => x.IdentifiedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING NEEDS ASSESSMENT PROGRAM
        // =====================================================
        builder.Entity<TrainingNeedsAssessmentProgram>(entity =>
        {
            entity.HasIndex(x => new { x.AssessmentId, x.ProgramId }).IsUnique();

            entity.Property(x => x.Priority).HasConversion<int>();

            entity.HasOne(x => x.Assessment)
                .WithMany(x => x.RecommendedPrograms)
                .HasForeignKey(x => x.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINING NEEDS ASSESSMENT SKILL
        // =====================================================
        builder.Entity<TrainingNeedsAssessmentSkill>(entity =>
        {
            entity.HasIndex(x => new { x.AssessmentId, x.SkillId }).IsUnique();

            entity.Property(x => x.CurrentProficiency).HasConversion<int>();
            entity.Property(x => x.RequiredProficiency).HasConversion<int>();
            entity.Property(x => x.GapPriority).HasConversion<int>();

            entity.HasOne(x => x.Assessment)
                .WithMany(x => x.SkillGaps)
                .HasForeignKey(x => x.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Skill)
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // TRAINING WAITLIST
        // =====================================================
        builder.Entity<TrainingWaitlist>(entity =>
        {
            entity.HasIndex(x => x.ScheduleId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.ScheduleId, x.EmployeeId }).IsUnique();
            entity.HasIndex(x => new { x.ScheduleId, x.Position });

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Schedule)
                .WithMany(x => x.Waitlist)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Nomination)
                .WithMany()
                .HasForeignKey(x => x.CreatedNominationId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // TRAINING REQUEST
        // =====================================================
        builder.Entity<TrainingRequest>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.RequestDate);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ApprovedBy)
                .WithMany()
                .HasForeignKey(x => x.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.LinkedProgram)
                .WithMany()
                .HasForeignKey(x => x.LinkedProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // LEARNING PATH
        // =====================================================
        builder.Entity<LearningPath>(entity =>
        {
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.OrganizationUnitId);
            entity.HasIndex(x => x.PositionId);

            entity.Property(x => x.Status).HasConversion<int>();
        });

        // =====================================================
        // LEARNING PATH PROGRAM
        // =====================================================
        builder.Entity<LearningPathProgram>(entity =>
        {
            entity.HasIndex(x => new { x.LearningPathId, x.ProgramId }).IsUnique();
            entity.HasIndex(x => new { x.LearningPathId, x.SequenceOrder });

            entity.HasOne(x => x.LearningPath)
                .WithMany(x => x.Programs)
                .HasForeignKey(x => x.LearningPathId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Program)
                .WithMany(x => x.LearningPathPrograms)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PrerequisitePathProgram)
                .WithMany()
                .HasForeignKey(x => x.PrerequisitePathProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // LEARNING PATH SKILL
        // =====================================================
        builder.Entity<LearningPathSkill>(entity =>
        {
            entity.HasIndex(x => new { x.LearningPathId, x.SkillId }).IsUnique();

            entity.Property(x => x.TargetProficiency).HasConversion<int>();

            entity.HasOne(x => x.LearningPath)
                .WithMany(x => x.TargetSkills)
                .HasForeignKey(x => x.LearningPathId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Skill)
                .WithMany()
                .HasForeignKey(x => x.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // EMPLOYEE LEARNING PATH
        // =====================================================
        builder.Entity<EmployeeLearningPath>(entity =>
        {
            entity.HasIndex(x => new { x.EmployeeId, x.LearningPathId }).IsUnique();
            entity.HasIndex(x => x.IsCompleted);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LearningPath)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.LearningPathId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedBy)
                .WithMany()
                .HasForeignKey(x => x.AssignedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // EMPLOYEE LEARNING PATH STEP
        // =====================================================
        builder.Entity<EmployeeLearningPathStep>(entity =>
        {
            entity.HasIndex(x => new { x.EmployeeLearningPathId, x.LearningPathProgramId }).IsUnique();

            entity.HasOne(x => x.EmployeeLearningPath)
                .WithMany(x => x.Steps)
                .HasForeignKey(x => x.EmployeeLearningPathId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.LearningPathProgram)
                .WithMany()
                .HasForeignKey(x => x.LearningPathProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Nomination)
                .WithMany()
                .HasForeignKey(x => x.NominationId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // MENTORING PROGRAM
        // =====================================================
        builder.Entity<MentoringProgram>(entity =>
        {
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.StartDate);

            entity.HasOne(x => x.CoordinatedBy)
                .WithMany()
                .HasForeignKey(x => x.CoordinatedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================
        // MENTORING PAIR
        // =====================================================
        builder.Entity<MentoringPair>(entity =>
        {
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.MentorId);
            entity.HasIndex(x => x.MenteeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.ProgramId, x.MentorId, x.MenteeId }).IsUnique();

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Pairs)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Mentor)
                .WithMany()
                .HasForeignKey(x => x.MentorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Mentee)
                .WithMany()
                .HasForeignKey(x => x.MenteeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // MENTORING SESSION
        // =====================================================
        builder.Entity<MentoringSession>(entity =>
        {
            entity.HasIndex(x => x.PairId);
            entity.HasIndex(x => x.SessionDate);

            entity.Property(x => x.Format).HasConversion<int>();

            entity.HasOne(x => x.Pair)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.PairId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
private void ConfigureOrientationEntities(ModelBuilder builder)
    {
        // All explicit relationships use DeleteBehavior.Restrict so the Tenant
        // relationship remains the single cascade path (avoids SQL Server
        // "multiple cascade paths" errors on this richly cross-linked graph).

        // =====================================================
        // ORIENTATION CATEGORY (self-referencing tree)
        // =====================================================
        builder.Entity<OrientationCategory>(entity =>
        {
            entity.HasIndex(x => x.ParentCategoryId);
            entity.HasIndex(x => x.IsActive);

            entity.HasOne(x => x.ParentCategory)
                .WithMany(x => x.SubCategories)
                .HasForeignKey(x => x.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION PROGRAM
        // =====================================================
        builder.Entity<OrientationProgram>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ProgramCode }).IsUnique();
            entity.HasIndex(x => x.CategoryId);
            entity.HasIndex(x => x.OwnerOrganizationUnitId);
            entity.HasIndex(x => x.ProgramType);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.AudienceScope);

            entity.Property(x => x.ProgramType).HasConversion<int>();
            entity.Property(x => x.DefaultDeliveryMode).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.AudienceScope).HasConversion<int>();
            entity.Property(x => x.RecurrenceFrequency).HasConversion<int>();

            entity.HasOne(x => x.Category)
                .WithMany(x => x.Programs)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OwnerOrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.OwnerOrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION MODULE
        // =====================================================
        builder.Entity<OrientationModule>(entity =>
        {
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => new { x.ProgramId, x.SequenceOrder });
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.ModuleType).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Modules)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION CONTENT ITEM
        // =====================================================
        builder.Entity<OrientationContentItem>(entity =>
        {
            entity.HasIndex(x => x.ModuleId);
            entity.HasIndex(x => new { x.ModuleId, x.SequenceOrder });

            entity.Property(x => x.ContentType).HasConversion<int>();

            entity.HasOne(x => x.Module)
                .WithMany(x => x.ContentItems)
                .HasForeignKey(x => x.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION PREREQUISITE (two FKs to OrientationProgram)
        // =====================================================
        builder.Entity<OrientationPrerequisite>(entity =>
        {
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.PrerequisiteProgramId);
            entity.HasIndex(x => new { x.ProgramId, x.PrerequisiteProgramId }).IsUnique();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Prerequisites)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PrerequisiteProgram)
                .WithMany()
                .HasForeignKey(x => x.PrerequisiteProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION AUDIENCE RULE
        // =====================================================
        builder.Entity<OrientationAudienceRule>(entity =>
        {
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.TargetType);
            entity.HasIndex(x => x.IsActive);

            entity.Property(x => x.TargetType).HasConversion<int>();
            entity.Property(x => x.Trigger).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.AudienceRules)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION SESSION
        // =====================================================
        builder.Entity<OrientationSession>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.SessionCode }).IsUnique();
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ScheduledStartAt);

            entity.Property(x => x.DeliveryMode).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION SESSION FACILITATOR
        // =====================================================
        builder.Entity<OrientationSessionFacilitator>(entity =>
        {
            entity.HasIndex(x => x.SessionId);
            entity.HasIndex(x => x.EmployeeId);

            entity.Property(x => x.Role).HasConversion<int>();

            entity.HasOne(x => x.Session)
                .WithMany(x => x.Facilitators)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION ATTENDANCE RECORD
        // =====================================================
        builder.Entity<OrientationAttendanceRecord>(entity =>
        {
            entity.HasIndex(x => x.EnrollmentId);
            entity.HasIndex(x => new { x.EnrollmentId, x.SessionDay });

            entity.Property(x => x.AttendanceStatus).HasConversion<int>();

            entity.HasOne(x => x.Enrollment)
                .WithMany(x => x.AttendanceRecords)
                .HasForeignKey(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // EMPLOYEE ORIENTATION (enrollment / primary tracking unit)
        // =====================================================
        builder.Entity<EmployeeOrientation>(entity =>
        {
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.SessionId);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.EnrollmentStatus);
            entity.HasIndex(x => x.CompletionStatus);
            entity.HasIndex(x => new { x.ProgramId, x.EmployeeId });

            entity.Property(x => x.EnrollmentStatus).HasConversion<int>();
            entity.Property(x => x.EnrollmentSource).HasConversion<int>();
            entity.Property(x => x.CompletionStatus).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Session)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION CONTENT PROGRESS
        // =====================================================
        builder.Entity<OrientationContentProgress>(entity =>
        {
            entity.HasIndex(x => x.EmployeeOrientationId);
            entity.HasIndex(x => x.ContentItemId);
            entity.HasIndex(x => new { x.EmployeeOrientationId, x.ContentItemId }).IsUnique();

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.EmployeeOrientation)
                .WithMany(x => x.ContentProgress)
                .HasForeignKey(x => x.EmployeeOrientationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ContentItem)
                .WithMany(x => x.ContentProgress)
                .HasForeignKey(x => x.ContentItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION ASSESSMENT QUESTION
        // =====================================================
        builder.Entity<OrientationAssessmentQuestion>(entity =>
        {
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => new { x.ProgramId, x.SequenceOrder });

            entity.Property(x => x.QuestionType).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany(x => x.AssessmentQuestions)
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION ASSESSMENT OPTION
        // =====================================================
        builder.Entity<OrientationAssessmentOption>(entity =>
        {
            entity.HasIndex(x => x.QuestionId);

            entity.HasOne(x => x.Question)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION ASSESSMENT RESPONSE
        // =====================================================
        builder.Entity<OrientationAssessmentResponse>(entity =>
        {
            entity.HasIndex(x => x.EmployeeOrientationId);
            entity.HasIndex(x => x.QuestionId);
            entity.HasIndex(x => x.SelectedOptionId);

            entity.HasOne(x => x.EmployeeOrientation)
                .WithMany(x => x.AssessmentResponses)
                .HasForeignKey(x => x.EmployeeOrientationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Question)
                .WithMany(x => x.Responses)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SelectedOption)
                .WithMany()
                .HasForeignKey(x => x.SelectedOptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION ACKNOWLEDGEMENT
        // =====================================================
        builder.Entity<OrientationAcknowledgement>(entity =>
        {
            entity.HasIndex(x => x.EmployeeOrientationId);
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.EmployeeOrientation)
                .WithMany(x => x.Acknowledgements)
                .HasForeignKey(x => x.EmployeeOrientationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION FEEDBACK
        // =====================================================
        builder.Entity<OrientationFeedback>(entity =>
        {
            entity.HasIndex(x => x.EmployeeOrientationId);

            entity.HasOne(x => x.EmployeeOrientation)
                .WithMany(x => x.Feedbacks)
                .HasForeignKey(x => x.EmployeeOrientationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION CERTIFICATE
        // =====================================================
        builder.Entity<OrientationCertificate>(entity =>
        {
            entity.HasIndex(x => x.EmployeeOrientationId);
            entity.HasIndex(x => new { x.TenantId, x.CertificateNumber }).IsUnique();
            entity.HasIndex(x => x.Status);

            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.EmployeeOrientation)
                .WithMany(x => x.Certificates)
                .HasForeignKey(x => x.EmployeeOrientationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =====================================================
        // ORIENTATION NOTIFICATION
        // =====================================================
        builder.Entity<OrientationNotification>(entity =>
        {
            entity.HasIndex(x => x.ProgramId);
            entity.HasIndex(x => x.EmployeeOrientationId);
            entity.HasIndex(x => x.RecipientEmployeeId);
            entity.HasIndex(x => x.IsRead);

            entity.Property(x => x.Type).HasConversion<int>();

            entity.HasOne(x => x.Program)
                .WithMany()
                .HasForeignKey(x => x.ProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EmployeeOrientation)
                .WithMany()
                .HasForeignKey(x => x.EmployeeOrientationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
private void ConfigureSuccessionPlanningEntities(ModelBuilder builder)
    {
        // ============================================================================
        // SUCCESSION PLAN
        // ============================================================================
        builder.Entity<SuccessionPlan>(entity =>
        {
            entity.Property(sp => sp.Status).HasConversion<int>();
            entity.Property(sp => sp.Criticality).HasConversion<int>();
            entity.Property(sp => sp.RiskLevel).HasConversion<int>();
            entity.Property(sp => sp.AnticipatedVacancyReason).HasConversion<int>();

            // Filtered unique index: only one active version per position
            entity.HasIndex(sp => new { sp.PositionId, sp.IsActiveVersion })
                .HasFilter("[IsActiveVersion] = 1")
                .IsUnique()
                .HasDatabaseName("UX_SuccessionPlan_ActiveVersion");

            entity.HasIndex(sp => new { sp.TenantId, sp.PlanNumber })
                .IsUnique()
                .HasDatabaseName("IX_SuccessionPlan_Tenant_PlanNumber");

            entity.HasIndex(sp => new { sp.TenantId, sp.PositionId })
                .HasDatabaseName("IX_SuccessionPlan_Tenant_PositionId");

            entity.HasIndex(sp => new { sp.TenantId, sp.Status })
                .HasDatabaseName("IX_SuccessionPlan_Tenant_Status");

            entity.HasIndex(sp => new { sp.TenantId, sp.PlanYear })
                .HasDatabaseName("IX_SuccessionPlan_Tenant_PlanYear");

            entity.HasOne(sp => sp.Position)
                .WithMany()
                .HasForeignKey(sp => sp.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sp => sp.CurrentIncumbent)
                .WithMany()
                .HasForeignKey(sp => sp.CurrentIncumbentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Self-referencing — must use Restrict to prevent cascade cycles
            entity.HasOne(sp => sp.SupersededByPlan)
                .WithMany()
                .HasForeignKey(sp => sp.SupersededByPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sp => sp.EmergencySuccessor)
                .WithMany()
                .HasForeignKey(sp => sp.EmergencySuccessorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sp => sp.ReviewedBy)
                .WithMany()
                .HasForeignKey(sp => sp.ReviewedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(sp => sp.ApprovedBy)
                .WithMany()
                .HasForeignKey(sp => sp.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(sp => sp.CompetencyRequirements)
                .WithOne(cr => cr.SuccessionPlan)
                .HasForeignKey(cr => cr.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(sp => sp.Actions)
                .WithOne(a => a.SuccessionPlan)
                .HasForeignKey(a => a.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(sp => sp.History)
                .WithOne(h => h.SuccessionPlan)
                .HasForeignKey(h => h.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(sp => sp.Candidates)
                .WithOne(c => c.SuccessionPlan)
                .HasForeignKey(c => c.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(sp => sp.Documents)
                .WithOne(d => d.SuccessionPlan)
                .HasForeignKey(d => d.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // SUCCESSION COMPETENCY REQUIREMENT
        // ============================================================================
        builder.Entity<SuccessionCompetencyRequirement>(entity =>
        {
            entity.HasIndex(scr => new { scr.TenantId, scr.SuccessionPlanId, scr.CompetencyId })
                .IsUnique()
                .HasDatabaseName("IX_SuccessionCompetencyReq_Tenant_Plan_Competency");

            entity.HasIndex(scr => scr.SuccessionPlanId)
                .HasDatabaseName("IX_SuccessionCompetencyReq_PlanId");

            entity.HasOne(scr => scr.SuccessionPlan)
                .WithMany(sp => sp.CompetencyRequirements)
                .HasForeignKey(scr => scr.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(scr => scr.Competency)
                .WithMany()
                .HasForeignKey(scr => scr.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // SUCCESSION CANDIDATE
        // ============================================================================
        builder.Entity<SuccessionCandidate>(entity =>
        {
            entity.Property(sc => sc.Type).HasConversion<int>();
            entity.Property(sc => sc.CurrentReadiness).HasConversion<int>();
            entity.Property(sc => sc.LatestPerformanceRating).HasConversion<int>();
            entity.Property(sc => sc.PotentialRating).HasConversion<int>();
            entity.Property(sc => sc.RetentionRisk).HasConversion<int>();

            // Unique rank per plan
            entity.HasIndex(sc => new { sc.SuccessionPlanId, sc.Rank })
                .IsUnique()
                .HasDatabaseName("UX_SuccessionCandidate_Plan_Rank");

            entity.HasIndex(sc => new { sc.TenantId, sc.SuccessionPlanId })
                .HasDatabaseName("IX_SuccessionCandidate_Tenant_PlanId");

            entity.HasIndex(sc => new { sc.TenantId, sc.EmployeeId })
                .HasDatabaseName("IX_SuccessionCandidate_Tenant_EmployeeId");

            entity.HasIndex(sc => sc.CurrentReadiness)
                .HasDatabaseName("IX_SuccessionCandidate_Readiness");

            entity.HasOne(sc => sc.SuccessionPlan)
                .WithMany(sp => sp.Candidates)
                .HasForeignKey(sc => sc.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(sc => sc.Employee)
                .WithMany()
                .HasForeignKey(sc => sc.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sc => sc.TalentPoolMember)
                .WithMany(tpm => tpm.SuccessionCandidacies)
                .HasForeignKey(sc => sc.TalentPoolMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sc => sc.TalentReviewRating)
                .WithMany(trr => trr.LinkedCandidates)
                .HasForeignKey(sc => sc.TalentReviewRatingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sc => sc.AssessedBy)
                .WithMany()
                .HasForeignKey(sc => sc.AssessedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(sc => sc.RecommendedBy)
                .WithMany()
                .HasForeignKey(sc => sc.RecommendedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(sc => sc.DevelopmentActivities)
                .WithOne(da => da.Candidate)
                .HasForeignKey(da => da.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(sc => sc.CompetencyGaps)
                .WithOne(g => g.Candidate)
                .HasForeignKey(g => g.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================================
        // SUCCESSION CANDIDATE GAP
        // ============================================================================
        builder.Entity<SuccessionCandidateGap>(entity =>
        {
            // GapSize and Status are computed properties — not mapped to columns
            entity.Ignore(g => g.GapSize);
            entity.Ignore(g => g.Status);

            entity.HasIndex(g => new { g.TenantId, g.CandidateId, g.CompetencyId })
                .IsUnique()
                .HasDatabaseName("IX_SuccessionCandidateGap_Tenant_Candidate_Competency");

            entity.HasIndex(g => g.CandidateId)
                .HasDatabaseName("IX_SuccessionCandidateGap_CandidateId");

            entity.HasIndex(g => g.CompetencyId)
                .HasDatabaseName("IX_SuccessionCandidateGap_CompetencyId");

            entity.HasIndex(g => g.Addressed)
                .HasDatabaseName("IX_SuccessionCandidateGap_Addressed");

            entity.HasOne(g => g.Candidate)
                .WithMany(sc => sc.CompetencyGaps)
                .HasForeignKey(g => g.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(g => g.Competency)
                .WithMany()
                .HasForeignKey(g => g.CompetencyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(g => g.AddressedByActivity)
                .WithMany(da => da.AddressedGaps)
                .HasForeignKey(g => g.AddressedByActivityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // SUCCESSION CANDIDATE FEEDBACK
        // ============================================================================
        builder.Entity<SuccessionCandidateFeedback>(entity =>
        {
            entity.Property(f => f.Note).HasMaxLength(2000).IsRequired();

            entity.HasIndex(f => f.CandidateId)
                .HasDatabaseName("IX_SuccessionCandidateFeedback_CandidateId");

            entity.HasOne(f => f.Candidate)
                .WithMany(sc => sc.Feedback)
                .HasForeignKey(f => f.CandidateId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Reviewer)
                .WithMany()
                .HasForeignKey(f => f.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // SUCCESSION DEVELOPMENT ACTIVITY
        // ============================================================================
        builder.Entity<SuccessionDevelopmentActivity>(entity =>
        {
            entity.Property(da => da.Type).HasConversion<int>();
            entity.Property(da => da.Status).HasConversion<int>();

            entity.Property(da => da.EstimatedCost)
                .HasColumnType("decimal(18,2)");

            entity.Property(da => da.ActualCost)
                .HasColumnType("decimal(18,2)");

            entity.HasIndex(da => da.CandidateId)
                .HasDatabaseName("IX_SuccessionDevActivity_CandidateId");

            entity.HasIndex(da => da.TalentPoolMemberId)
                .HasDatabaseName("IX_SuccessionDevActivity_TalentPoolMemberId");

            entity.HasIndex(da => da.Status)
                .HasDatabaseName("IX_SuccessionDevActivity_Status");

            entity.HasOne(da => da.Candidate)
                .WithMany(sc => sc.DevelopmentActivities)
                .HasForeignKey(da => da.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(da => da.TalentPoolMember)
                .WithMany(tpm => tpm.DevelopmentActivities)
                .HasForeignKey(da => da.TalentPoolMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(da => da.Supervisor)
                .WithMany()
                .HasForeignKey(da => da.SupervisorId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(da => da.Milestones)
                .WithOne(m => m.Activity)
                .HasForeignKey(m => m.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================================
        // SUCCESSION DEVELOPMENT MILESTONE
        // ============================================================================
        builder.Entity<SuccessionDevelopmentMilestone>(entity =>
        {
            entity.HasIndex(m => m.ActivityId)
                .HasDatabaseName("IX_SuccessionDevMilestone_ActivityId");

            entity.HasIndex(m => m.TargetDate)
                .HasDatabaseName("IX_SuccessionDevMilestone_TargetDate");

            entity.HasIndex(m => m.IsCompleted)
                .HasDatabaseName("IX_SuccessionDevMilestone_IsCompleted");

            entity.HasOne(m => m.Activity)
                .WithMany(da => da.Milestones)
                .HasForeignKey(m => m.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================================
        // SUCCESSION ACTION
        // ============================================================================
        builder.Entity<SuccessionAction>(entity =>
        {
            entity.Property(a => a.Type).HasConversion<int>();
            entity.Property(a => a.Priority).HasConversion<int>();
            entity.Property(a => a.Status).HasConversion<int>();

            entity.HasIndex(a => a.SuccessionPlanId)
                .HasDatabaseName("IX_SuccessionAction_PlanId");

            entity.HasIndex(a => a.CandidateId)
                .HasDatabaseName("IX_SuccessionAction_CandidateId");

            entity.HasIndex(a => a.Status)
                .HasDatabaseName("IX_SuccessionAction_Status");

            entity.HasIndex(a => a.DueDate)
                .HasDatabaseName("IX_SuccessionAction_DueDate");

            entity.HasOne(a => a.SuccessionPlan)
                .WithMany(sp => sp.Actions)
                .HasForeignKey(a => a.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Candidate)
                .WithMany()
                .HasForeignKey(a => a.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.ResponsiblePerson)
                .WithMany()
                .HasForeignKey(a => a.ResponsiblePersonId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.AssignedBy)
                .WithMany()
                .HasForeignKey(a => a.AssignedById)
                .OnDelete(DeleteBehavior.NoAction);

            // Self-referencing dependency chain — must use Restrict to prevent cycles
            entity.HasOne(a => a.DependsOnAction)
                .WithMany()
                .HasForeignKey(a => a.DependsOnActionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // SUCCESSION PLAN HISTORY (immutable audit snapshot)
        // ============================================================================
        builder.Entity<SuccessionPlanHistory>(entity =>
        {
            entity.Property(h => h.StatusAtSnapshot).HasConversion<int>();
            entity.Property(h => h.RiskLevelAtSnapshot).HasConversion<int>();

            // Store the JSON snapshot in a large text column
            entity.Property(h => h.PlanSnapshot)
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(h => h.SuccessionPlanId)
                .HasDatabaseName("IX_SuccessionPlanHistory_PlanId");

            entity.HasIndex(h => new { h.SuccessionPlanId, h.VersionNumber })
                .HasDatabaseName("IX_SuccessionPlanHistory_Plan_Version");

            entity.HasIndex(h => h.SnapshotDate)
                .HasDatabaseName("IX_SuccessionPlanHistory_SnapshotDate");

            entity.HasOne(h => h.SuccessionPlan)
                .WithMany(sp => sp.History)
                .HasForeignKey(h => h.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.SnapshotCreatedBy)
                .WithMany()
                .HasForeignKey(h => h.SnapshotCreatedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ============================================================================
        // SUCCESSION DOCUMENT
        // ============================================================================
        builder.Entity<SuccessionDocument>(entity =>
        {
            entity.HasIndex(sd => sd.SuccessionPlanId)
                .HasDatabaseName("IX_SuccessionDocument_PlanId");

            entity.HasIndex(sd => sd.CandidateId)
                .HasDatabaseName("IX_SuccessionDocument_CandidateId");

            entity.HasIndex(sd => sd.TalentPoolMemberId)
                .HasDatabaseName("IX_SuccessionDocument_TalentPoolMemberId");

            entity.HasIndex(sd => sd.UploadDate)
                .HasDatabaseName("IX_SuccessionDocument_UploadDate");

            entity.HasOne(sd => sd.SuccessionPlan)
                .WithMany(sp => sp.Documents)
                .HasForeignKey(sd => sd.SuccessionPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sd => sd.Candidate)
                .WithMany()
                .HasForeignKey(sd => sd.CandidateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sd => sd.TalentPoolMember)
                .WithMany(tpm => tpm.Documents)
                .HasForeignKey(sd => sd.TalentPoolMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(sd => sd.UploadedBy)
                .WithMany()
                .HasForeignKey(sd => sd.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================================================
        // TALENT POOL
        // ============================================================================
        builder.Entity<TalentPool>(entity =>
        {
            entity.HasIndex(tp => new { tp.TenantId, tp.Name })
                .IsUnique()
                .HasDatabaseName("IX_TalentPool_Tenant_Name");

            entity.HasIndex(tp => new { tp.TenantId, tp.IsActive })
                .HasDatabaseName("IX_TalentPool_Tenant_Active");

            entity.HasIndex(tp => tp.OwnerId)
                .HasDatabaseName("IX_TalentPool_OwnerId");

            entity.HasIndex(tp => tp.PoolTypeId)
                .HasDatabaseName("IX_TalentPool_PoolTypeId");

            entity.HasOne(tp => tp.Owner)
                .WithMany()
                .HasForeignKey(tp => tp.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(tp => tp.PoolType)
                .WithMany(t => t.Pools)
                .HasForeignKey(tp => tp.PoolTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(tp => tp.TargetPosition)
                .WithMany()
                .HasForeignKey(tp => tp.TargetPositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(tp => tp.Members)
                .WithOne(m => m.TalentPool)
                .HasForeignKey(m => m.TalentPoolId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================================
        // TALENT POOL TYPE DEFINITION (tenant-configurable pool types)
        // ============================================================================
        builder.Entity<TalentPoolTypeDefinition>(entity =>
        {
            entity.Property(t => t.Code).HasMaxLength(50).IsRequired();
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(500);
            entity.Property(t => t.ColorHex).HasMaxLength(9);

            entity.HasIndex(t => new { t.TenantId, t.Name })
                .IsUnique()
                .HasDatabaseName("UX_TalentPoolType_Tenant_Name");

            entity.HasIndex(t => new { t.TenantId, t.Code })
                .IsUnique()
                .HasDatabaseName("UX_TalentPoolType_Tenant_Code");

            // Seed the built-in types (from the former enum) for the default tenant.
            entity.HasData(TalentPoolTypeSeed.Rows.Select(r => new
            {
                Id               = r.Id,
                TenantId         = TalentPoolTypeSeed.DefaultTenantId,
                Code             = r.Code,
                Name             = r.Name,
                Description      = (string?)null,
                ColorHex         = (string?)r.ColorHex,
                SortOrder        = r.SortOrder,
                IsActive         = true,
                IsSystemDefault  = true,
                CreatedAt        = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt        = (DateTime?)null,
                CreatedBy        = "System",
                UpdatedBy        = (string?)null,
                CreatedById      = (Guid?)null,
                LastModifiedById = (Guid?)null,
                IsDeleted        = false,
                DeletedAt        = (DateTime?)null,
                DeletedBy        = (string?)null
            }));
        });

        // ============================================================================
        // TALENT POOL MEMBER
        // ============================================================================
        builder.Entity<TalentPoolMember>(entity =>
        {
            entity.Property(tpm => tpm.Readiness).HasConversion<int>();
            entity.Property(tpm => tpm.LatestPerformanceRating).HasConversion<int>();
            entity.Property(tpm => tpm.LatestPotentialRating).HasConversion<int>();

            entity.HasIndex(tpm => new { tpm.TenantId, tpm.TalentPoolId, tpm.EmployeeId })
                .IsUnique()
                .HasDatabaseName("IX_TalentPoolMember_Tenant_Pool_Employee");

            entity.HasIndex(tpm => tpm.TalentPoolId)
                .HasDatabaseName("IX_TalentPoolMember_TalentPoolId");

            entity.HasIndex(tpm => tpm.EmployeeId)
                .HasDatabaseName("IX_TalentPoolMember_EmployeeId");

            entity.HasIndex(tpm => new { tpm.TenantId, tpm.IsActive })
                .HasDatabaseName("IX_TalentPoolMember_Tenant_Active");

            entity.HasOne(tpm => tpm.TalentPool)
                .WithMany(tp => tp.Members)
                .HasForeignKey(tpm => tpm.TalentPoolId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tpm => tpm.Employee)
                .WithMany()
                .HasForeignKey(tpm => tpm.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(tpm => tpm.NominatedBy)
                .WithMany()
                .HasForeignKey(tpm => tpm.NominatedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ============================================================================
        // TALENT REVIEW SESSION
        // ============================================================================
        builder.Entity<TalentReviewSession>(entity =>
        {
            entity.HasIndex(trs => new { trs.TenantId, trs.ReviewYear })
                .HasDatabaseName("IX_TalentReviewSession_Tenant_Year");

            entity.HasIndex(trs => trs.SessionDate)
                .HasDatabaseName("IX_TalentReviewSession_SessionDate");

            entity.HasIndex(trs => new { trs.TenantId, trs.IsFinalized })
                .HasDatabaseName("IX_TalentReviewSession_Tenant_Finalized");

            entity.HasOne(trs => trs.FacilitatedBy)
                .WithMany()
                .HasForeignKey(trs => trs.FacilitatedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(trs => trs.OrganizationLevel)
                .WithMany()
                .HasForeignKey(trs => trs.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(trs => trs.OrganizationUnit)
                .WithMany()
                .HasForeignKey(trs => trs.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(trs => trs.FinalizedBy)
                .WithMany()
                .HasForeignKey(trs => trs.FinalizedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(trs => trs.Ratings)
                .WithOne(r => r.Session)
                .HasForeignKey(r => r.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================================================
        // TALENT REVIEW RATING
        // ============================================================================
        builder.Entity<TalentReviewRating>(entity =>
        {
            entity.Property(trr => trr.Performance).HasConversion<int>();
            entity.Property(trr => trr.Potential).HasConversion<int>();
            entity.Property(trr => trr.PreviousPerformance).HasConversion<int>();
            entity.Property(trr => trr.PreviousPotential).HasConversion<int>();

            // One rating per employee per session
            entity.HasIndex(trr => new { trr.SessionId, trr.EmployeeId })
                .IsUnique()
                .HasDatabaseName("IX_TalentReviewRating_Session_Employee");

            entity.HasIndex(trr => trr.TalentPoolMemberId)
                .HasDatabaseName("IX_TalentReviewRating_TalentPoolMemberId");

            entity.HasIndex(trr => new { trr.TenantId, trr.CalibrationConfirmed })
                .HasDatabaseName("IX_TalentReviewRating_Tenant_Calibrated");

            entity.HasOne(trr => trr.Session)
                .WithMany(s => s.Ratings)
                .HasForeignKey(trr => trr.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(trr => trr.Employee)
                .WithMany()
                .HasForeignKey(trr => trr.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(trr => trr.TalentPoolMember)
                .WithMany(tpm => tpm.ReviewRatings)
                .HasForeignKey(trr => trr.TalentPoolMemberId)
                .OnDelete(DeleteBehavior.Restrict);

            // Self-referencing session for previous ratings
            entity.HasOne(trr => trr.PreviousRatingSession)
                .WithMany()
                .HasForeignKey(trr => trr.PreviousRatingSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(trr => trr.RatedBy)
                .WithMany()
                .HasForeignKey(trr => trr.RatedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(trr => trr.CalibrationConfirmedBy)
                .WithMany()
                .HasForeignKey(trr => trr.CalibrationConfirmedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // =====================================================================
        // Staff Attendance Module
        // =====================================================================

        builder.Entity<StaffAttendanceRecord>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Date });
            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.AttendanceRecords)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StaffDailyAttendance>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.AttendanceDate }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.AttendanceDate });
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.PayPeriodId });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.DailyAttendances)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OvertimeApprovedBy)
                .WithMany()
                .HasForeignKey(e => e.OvertimeApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.VerifiedBy)
                .WithMany()
                .HasForeignKey(e => e.VerifiedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.ExceptionApprovedBy)
                .WithMany()
                .HasForeignKey(e => e.ExceptionApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.WorkSchedule)
                .WithMany()
                .HasForeignKey(e => e.WorkScheduleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Location)
                .WithMany(l => l.AttendanceDays)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CheckInGeofenceZone)
                .WithMany()
                .HasForeignKey(e => e.CheckInGeofenceZoneId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RemoteWorkRequest)
                .WithMany(r => r.AttendanceDays)
                .HasForeignKey(e => e.RemoteWorkRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PayPeriod)
                .WithMany()
                .HasForeignKey(e => e.PayPeriodId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.LeaveRequest)
                .WithMany(lr => lr.AttendanceDays)
                .HasForeignKey(e => e.LeaveRequestId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PublicHoliday)
                .WithMany()
                .HasForeignKey(e => e.PublicHolidayId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StaffAttendanceLog>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.LogDateTime });
            entity.HasIndex(e => new { e.TenantId, e.IsProcessed });

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Device)
                .WithMany()
                .HasForeignKey(e => e.DeviceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Attendance)
                .WithMany(a => a.AttendanceLogs)
                .HasForeignKey(e => e.AttendanceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AttendanceLocationVerificationLog>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.VerificationDateTime });
            entity.HasIndex(e => new { e.TenantId, e.AttendanceLogId });

            entity.HasOne(e => e.AttendanceLog)
                .WithMany(al => al.VerificationLogs)
                .HasForeignKey(e => e.AttendanceLogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.GeofenceZone)
                .WithMany(gz => gz.VerificationLogs)
                .HasForeignKey(e => e.GeofenceZoneId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StaffAttendanceRegularization>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.RegularizationNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Status });

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Attendance)
                .WithMany(a => a.Regularizations)
                .HasForeignKey(e => e.AttendanceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ApprovedBy)
                .WithMany()
                .HasForeignKey(e => e.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StaffMonthlyAttendanceSummary>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Year, e.Month }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.PayPeriodId });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.MonthlySummaries)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.FinalizedBy)
                .WithMany()
                .HasForeignKey(e => e.FinalizedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.PayPeriod)
                .WithMany(p => p.AttendanceSummaries)
                .HasForeignKey(e => e.PayPeriodId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StaffBulkAttendanceImport>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ImportReference }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status });

            entity.HasOne(e => e.ImportedBy)
                .WithMany()
                .HasForeignKey(e => e.ImportedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StaffBulkAttendanceImportRow>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ImportId, e.RowNumber });

            entity.HasOne(e => e.Import)
                .WithMany(i => i.ImportRows)
                .HasForeignKey(e => e.ImportId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.CreatedAttendance)
                .WithMany()
                .HasForeignKey(e => e.CreatedAttendanceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<WorkSchedule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ScheduleName }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsDefault });
            entity.HasIndex(e => new { e.TenantId, e.IsActive });
        });

        builder.Entity<EmployeeWorkSchedule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.IsCurrent });
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.EffectiveDate });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.WorkSchedules)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.WorkSchedule)
                .WithMany(ws => ws.EmployeeSchedules)
                .HasForeignKey(e => e.WorkScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AssignedBy)
                .WithMany()
                .HasForeignKey(e => e.AssignedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ShiftDefinition>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.WorkScheduleId });
            entity.HasIndex(e => new { e.TenantId, e.IsActive });

            entity.HasOne(e => e.WorkSchedule)
                .WithMany(ws => ws.Shifts)
                .HasForeignKey(e => e.WorkScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ShiftAssignment>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.AssignmentDate });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.ShiftAssignments)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ShiftDefinition)
                .WithMany(sd => sd.ShiftAssignments)
                .HasForeignKey(e => e.ShiftDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AssignedBy)
                .WithMany()
                .HasForeignKey(e => e.AssignedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ShiftRotationStage>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ShiftRotationPlanId, e.StageOrder });

            entity.HasOne(e => e.ShiftRotationPlan)
                .WithMany(p => p.Stages)
                .HasForeignKey(e => e.ShiftRotationPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ShiftDefinition)
                .WithMany(sd => sd.RotationStages)
                .HasForeignKey(e => e.ShiftDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ShiftRotationMember>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ShiftRotationPlanId });
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId });

            entity.HasOne(e => e.ShiftRotationPlan)
                .WithMany(p => p.Members)
                .HasForeignKey(e => e.ShiftRotationPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.OrganizationUnit)
                .WithMany()
                .HasForeignKey(e => e.OrganizationUnitId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Team)
                .WithMany()
                .HasForeignKey(e => e.TeamId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<PositionOvertimePolicy>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PositionId, e.AllowanceType });

            entity.HasOne(e => e.Position)
                .WithMany(p => p.OvertimePolicies)
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeOvertimeOverride>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.AllowanceType });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.OvertimeOverrides)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Policy)
                .WithMany(p => p.EmployeeOverrides)
                .HasForeignKey(e => e.PolicyId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ApprovedBy)
                .WithMany()
                .HasForeignKey(e => e.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StaffOvertimeRequest>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.RequestNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Status });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.OvertimeRequests)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ApprovedBy)
                .WithMany()
                .HasForeignKey(e => e.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.SupervisorConfirmedBy)
                .WithMany()
                .HasForeignKey(e => e.SupervisorConfirmedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Attendance)
                .WithMany()
                .HasForeignKey(e => e.AttendanceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<EmployeeBiometric>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.BiometricType, e.IsActive });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.Biometrics)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.EnrolledBy)
                .WithMany()
                .HasForeignKey(e => e.EnrolledById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StaffAttendanceDevice>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.DeviceId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsActive });

            entity.HasOne(e => e.Location)
                .WithMany(l => l.Devices)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<GeofenceZone>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ZoneName });
            entity.HasIndex(e => new { e.TenantId, e.IsActive });
        });

        builder.Entity<RemoteWorkRequest>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.RequestNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Status });

            entity.HasOne(e => e.Employee)
                .WithMany(emp => emp.RemoteWorkRequests)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ApprovedBy)
                .WithMany()
                .HasForeignKey(e => e.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<HolidayCalendar>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.CalendarName }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsDefault });

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PublicHoliday>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.HolidayCalendarId, e.DateFrom });
            entity.HasIndex(e => new { e.TenantId, e.HolidayCalendarId, e.IsActive });
            entity.Ignore(e => e.Year);

            entity.HasOne(e => e.HolidayCalendar)
                .WithMany(hc => hc.PublicHolidays)
                .HasForeignKey(e => e.HolidayCalendarId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayPeriod>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PeriodName }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.StartDate, e.EndDate });

            entity.HasOne(e => e.ClosedBy)
                .WithMany()
                .HasForeignKey(e => e.ClosedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.ExportedBy)
                .WithMany()
                .HasForeignKey(e => e.ExportedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<StaffAttendancePayrollExport>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ExportReference }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.PayPeriodId });

            entity.HasOne(e => e.PayPeriod)
                .WithMany(p => p.PayrollExports)
                .HasForeignKey(e => e.PayPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ExportedBy)
                .WithMany()
                .HasForeignKey(e => e.ExportedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StaffAttendanceAlertRule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.TriggerType, e.IsActive });

            entity.HasOne(e => e.OrganizationUnit)
                .WithMany()
                .HasForeignKey(e => e.OrganizationUnitId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Position)
                .WithMany()
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StaffAttendanceAlert>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.AlertRuleId });
            entity.HasIndex(e => new { e.TenantId, e.TriggeredDate });

            entity.HasOne(e => e.AlertRule)
                .WithMany(r => r.Alerts)
                .HasForeignKey(e => e.AlertRuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AcknowledgedBy)
                .WithMany()
                .HasForeignKey(e => e.AcknowledgedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.ResolvedBy)
                .WithMany()
                .HasForeignKey(e => e.ResolvedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.DismissedBy)
                .WithMany()
                .HasForeignKey(e => e.DismissedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<ConsultantClient>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ClientCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsActive });

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ConsultantClientPortalAccount>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.HasIndex(e => e.ConsultantClientId);

            entity.HasOne(e => e.ConsultantClient)
                .WithMany(c => c.PortalAccounts)
                .HasForeignKey(e => e.ConsultantClientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClientEngagement>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EngagementCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ClientId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.ConsultantId });

            entity.HasOne(e => e.Client)
                .WithMany(c => c.Engagements)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Consultant)
                .WithMany(emp => emp.ConsultantEngagements)
                .HasForeignKey(e => e.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ConsultantTimesheet>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.TimesheetNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ConsultantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.ClientId });

            entity.HasOne(e => e.Consultant)
                .WithMany(emp => emp.ConsultantTimesheets)
                .HasForeignKey(e => e.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Client)
                .WithMany(c => c.Timesheets)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Engagement)
                .WithMany(eng => eng.Timesheets)
                .HasForeignKey(e => e.EngagementId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ConsultantTimesheetEntry>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.TimesheetId, e.WorkDate });

            entity.HasOne(e => e.Timesheet)
                .WithMany(ts => ts.Entries)
                .HasForeignKey(e => e.TimesheetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ClientTimesheetConfirmation>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.TimesheetId });
            entity.HasIndex(e => e.ConfirmationToken).IsUnique();

            entity.HasOne(e => e.Timesheet)
                .WithMany(ts => ts.Confirmations)
                .HasForeignKey(e => e.TimesheetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.SentBy)
                .WithMany()
                .HasForeignKey(e => e.SentById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TimesheetInvoice>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.InvoiceNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ClientId, e.Status });

            entity.HasOne(e => e.Client)
                .WithMany(c => c.Invoices)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Consultant)
                .WithMany()
                .HasForeignKey(e => e.ConsultantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TimesheetInvoiceLink>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.InvoiceId, e.TimesheetId }).IsUnique();

            entity.HasOne(e => e.Invoice)
                .WithMany(inv => inv.LinkedTimesheets)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Timesheet)
                .WithMany(ts => ts.InvoiceLinks)
                .HasForeignKey(e => e.TimesheetId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
private void ConfigureStaffTravelEntities(ModelBuilder builder)
    {
        // --- Indexes, unique natural keys & FX precision -------------------------
        // (EF auto-creates indexes on FK columns, so only non-FK / unique indexes
        //  are declared here. Tenant relationships and base decimal precision are
        //  handled by ConfigureGlobalTenantRelationships / ConfigureDecimalPrecision.)

        builder.Entity<StaffTravelRequest>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.TravelType);
            entity.HasIndex(x => x.TravelStartDate);

            // One-to-one: a request has at most one budget.
            entity.HasOne(r => r.Budget)
                .WithOne(b => b.StaffTravelRequest)
                .HasForeignKey<StaffTravelBudget>(b => b.StaffTravelRequestId);
        });

        builder.Entity<StaffGroupTravel>(entity =>
        {
            entity.HasIndex(x => x.Status);
        });

        builder.Entity<StaffTravelItinerary>(entity =>
        {
            entity.HasIndex(x => x.IsCurrentVersion);
        });

        builder.Entity<StaffTravelExpenseClaim>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ClaimNumber }).IsUnique();
            entity.HasIndex(x => x.Status);
        });

        builder.Entity<StaffTravelExpenseClaimLine>(entity =>
        {
            // Preserve FX precision (global convention would coerce this to 18,4).
            entity.Property(x => x.ExchangeRate).HasColumnType("decimal(18,6)");
            entity.HasIndex(x => x.Status);
        });

        builder.Entity<StaffTravelAdvance>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.AdvanceNumber }).IsUnique();
            entity.HasIndex(x => x.Status);
        });

        builder.Entity<StaffTravelPerDiemRate>(entity =>
        {
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => new { x.CountryId, x.EffectiveFrom });
        });

        builder.Entity<StaffTravelPolicy>(entity =>
        {
            entity.HasIndex(x => x.IsCurrentVersion);
            entity.HasIndex(x => x.EffectiveFrom);
        });

        builder.Entity<StaffTravelPolicyRule>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.PolicyId, x.RuleCode }).IsUnique();
        });

        builder.Entity<StaffTravelVisaRequirement>(entity =>
        {
            entity.HasIndex(x => new { x.PassportCountryId, x.DestinationCountryId });
        });

        builder.Entity<StaffTravelAlert>(entity =>
        {
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => new { x.CountryId, x.EffectiveFrom });
        });

        builder.Entity<StaffTravelHealthRequirement>(entity =>
        {
            entity.HasIndex(x => x.IsActive);
        });

        // --- Delete behaviour ----------------------------------------------------
        // Force Restrict on every relationship inside the Staff Travel module to
        // avoid multiple cascade paths in SQL Server (e.g. a flight booking is
        // reachable from the request directly and via an itinerary leg, and many
        // tables reference Employee/Country). Deletion is performed via soft delete
        // (IsDeleted), so cascade delete is not relied upon. Mirrors the pattern in
        // ConfigureGlobalTenantRelationships.
        var staffTravelNamespace = typeof(StaffTravelRequest).Namespace;
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == staffTravelNamespace))
        {
            foreach (var fk in entityType.GetForeignKeys())
            {
                fk.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
