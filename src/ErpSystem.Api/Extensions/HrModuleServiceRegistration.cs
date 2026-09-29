// <auto-ported> HR module — dependency injection registrations.
//
// Kept OUT of ServiceCollectionExtensions.cs (shared by all module developers) so that re-syncing
// HR from HRApi only rewrites this file. Wired up with a single call:
//     services.AddHrModuleServices();
//
// Only registrations whose interface AND implementation exist in RHEMA are emitted, which
// automatically excludes the carved-out Performance area and HRApi's Bank services.
// See HR_MODULE_PORT_PLAN.md.
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Services.HR;
using ErpSystem.Core.Services.HR.Appraisal;
using ErpSystem.Core.Services.HR.Assets;
using ErpSystem.Core.Services.HR.Handlers;
using ErpSystem.Core.Services.HR.Benefits;
using ErpSystem.Core.Services.Common;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Repositories.HR;
using ErpSystem.Data.Services;
using Microsoft.AspNetCore.Identity;

namespace ErpSystem.Api.Extensions;

public static class HrModuleServiceRegistration
{
    /// <summary>Registers every ported HR service and repository.</summary>
    public static IServiceCollection AddHrModuleServices(this IServiceCollection services)
    {
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<ISectionRepository, SectionRepository>();
        services.AddScoped<IEmployeeSkillRepository, EmployeeSkillRepository>();
        services.AddScoped<IEmployeeContractDetailRepository, EmployeeContractDetailRepository>();
        services.AddScoped<IOrganizationStructureRepository, OrganizationStructureRepository>();
        services.AddScoped<IOrganizationLevelRepository, OrganizationLevelRepository>();
        services.AddScoped<IOrganizationUnitRepository, OrganizationUnitRepository>();
        services.AddScoped<IOrganizationUnitHistoryRepository, OrganizationUnitHistoryRepository>();
        services.AddScoped<ILocationStructureRepository, LocationStructureRepository>();
        services.AddScoped<ILocationLevelRepository, LocationLevelRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ILocationContactRepository, LocationContactRepository>();
        services.AddScoped<IEmployeePositionRepository, EmployeePositionRepository>();
        services.AddScoped<IStaffLevelRepository, StaffLevelRepository>();
        services.AddScoped<IBenefitPolicyRepository, BenefitPolicyRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<IQualificationCatalogueRepository, QualificationCatalogueRepository>();
        services.AddScoped<IEmployeePositionHistoryRepository, EmployeePositionHistoryRepository>();
        services.AddScoped<IEmployeeSalaryAssignmentRepository, EmployeeSalaryAssignmentRepository>();
        services.AddScoped<IEmployeeRefereeRepository, EmployeeRefereeRepository>();
        services.AddScoped<IEmployeeQualificationRepository, EmployeeQualificationRepository>();
        services.AddScoped<IEmployeeWorkHistoryRepository, EmployeeWorkHistoryRepository>();
        services.AddScoped<ISalaryGradeRepository, SalaryGradeRepository>();
        services.AddScoped<ISalaryLevelRepository, SalaryLevelRepository>();
        services.AddScoped<ISalaryNotchRepository, SalaryNotchRepository>();
        services.AddScoped<ILeaveRepository, LeaveRepository>();
        services.AddScoped<ILeaveTypeRepository, LeaveTypeRepository>();
        services.AddScoped<ILeavePlanRepository, LeavePlanRepository>();
        services.AddScoped<ILeaveBalanceRepository, LeaveBalanceRepository>();
        services.AddScoped<IPublicHolidayRepository, PublicHolidayRepository>();
        services.AddScoped<IAssetTypeRepository, AssetTypeRepository>();
        services.AddScoped<IAssetTypeAttributeRepository, AssetTypeAttributeRepository>();
        services.AddScoped<ICompanyAssetRepository, CompanyAssetRepository>();
        services.AddScoped<IAssetAttributeValueRepository, AssetAttributeValueRepository>();
        services.AddScoped<IAssetImageRepository, AssetImageRepository>();
        services.AddScoped<IAssetAssignmentRepository, AssetAssignmentRepository>();
        services.AddScoped<IAssetMaintenanceRepository, AssetMaintenanceRepository>();
        services.AddScoped<IAssetAttachmentRepository, AssetAttachmentRepository>();
        services.AddScoped<IAssetRequisitionRepository, AssetRequisitionRepository>();
        services.AddScoped<IAssetTransferRepository, AssetTransferRepository>();
        // AST-3 — the surcharge and what has been collected against it (slice 7).
        services.AddScoped<IAssetSurchargeRepository, AssetSurchargeRepository>();
        services.AddScoped<IAssetSurchargeRecoveryRepository, AssetSurchargeRecoveryRepository>();
        services.AddScoped<IAwardTypeRepository, AwardTypeRepository>();
        services.AddScoped<IAwardLevelRepository, AwardLevelRepository>();
        services.AddScoped<IAwardBudgetRepository, AwardBudgetRepository>();
        services.AddScoped<IEmployeeAwardRepository, EmployeeAwardRepository>();
        services.AddScoped<IAwardAttachmentRepository, AwardAttachmentRepository>();
        services.AddScoped<ITeamAwardRecipientRepository, TeamAwardRecipientRepository>();
        services.AddScoped<IAwardNominationRepository, AwardNominationRepository>();
        services.AddScoped<ITeamAwardNomineeRepository, TeamAwardNomineeRepository>();
        services.AddScoped<IAwardNomineeContributionRepository, AwardNomineeContributionRepository>();
        services.AddScoped<IAwardNominationAttachmentRepository, AwardNominationAttachmentRepository>();
        services.AddScoped<IAwardCommitteeRepository, AwardCommitteeRepository>();
        services.AddScoped<IAwardCommitteeMemberRepository, AwardCommitteeMemberRepository>();
        services.AddScoped<IAwardNominationReviewRepository, AwardNominationReviewRepository>();
        services.AddScoped<ILongServiceAwardRepository, LongServiceAwardRepository>();
        services.AddScoped<ILongServiceMilestoneRepository, LongServiceMilestoneRepository>();
        services.AddScoped<IAwardTypeTargetRepository, AwardTypeTargetRepository>();
        services.AddScoped<IAwardCycleRepository, AwardCycleRepository>();
        services.AddScoped<IAwardVoteRepository, AwardVoteRepository>();
        // Who may vote. Mirrors the eligibility evaluator on purpose - same targets, same
        // matching - so the two cannot drift into behaving differently.
        services.AddScoped<IAwardElectorateEvaluator, AwardElectorateEvaluator>();
        // Resolves the polymorphic AwardTypeTarget.TargetId to a name. Not a repository for an
        // entity of its own — it reads four different tables depending on the target's kind.
        services.AddScoped<IAwardTargetNameResolver, AwardTargetNameResolver>();
        // Applies every criterion an award type carries - service years, age, target scoping and
        // the per-employee cap - not just the target scoping the old check looked at.
        services.AddScoped<IAwardEligibilityEvaluator, AwardEligibilityEvaluator>();
        // Reads the appraisal and goal stores to decide who an award puts forward automatically.
        services.AddScoped<IAwardPerformanceTriggerEvaluator, AwardPerformanceTriggerEvaluator>();
        // Reads service years and disciplinary records to decide who has reached a long-service
        // milestone. Shared by the preview and the run so the two cannot compute different answers.
        services.AddScoped<ILongServiceSweepEvaluator, LongServiceSweepEvaluator>();
        services.AddScoped<ICompanyEventRepository, CompanyEventRepository>();
        services.AddScoped<IEventParticipantRepository, EventParticipantRepository>();
        services.AddScoped<IEventAttendanceRepository, EventAttendanceRepository>();
        services.AddScoped<IEventAttachmentRepository, EventAttachmentRepository>();
        services.AddScoped<IEventTaskRepository, EventTaskRepository>();
        services.AddScoped<IMeetingRoomRepository, MeetingRoomRepository>();
        services.AddScoped<IRoomBookingRepository, RoomBookingRepository>();
        services.AddScoped<ICompanyMilestoneRepository, CompanyMilestoneRepository>();
        services.AddScoped<IBusinessClosureRepository, BusinessClosureRepository>();
        services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
        services.AddScoped<IFiscalPeriodRepository, FiscalPeriodRepository>();
        services.AddScoped<IJobDescriptionRepository, JobDescriptionRepository>();
        services.AddScoped<IJobResponsibilityRepository, JobResponsibilityRepository>();
        services.AddScoped<IJobQualificationRepository, JobQualificationRepository>();
        services.AddScoped<IJobCompetencyRepository, JobCompetencyRepository>();
        services.AddScoped<IManpowerBudgetRepository, ManpowerBudgetRepository>();
        services.AddScoped<IManpowerBudgetLineRepository, ManpowerBudgetLineRepository>();
        services.AddScoped<IJobPhysicalDemandRepository, JobPhysicalDemandRepository>();
        services.AddScoped<IJobWorkingConditionRepository, JobWorkingConditionRepository>();
        services.AddScoped<IJobEquipmentToolRepository, JobEquipmentToolRepository>();
        services.AddScoped<IJobReportingRelationshipRepository, JobReportingRelationshipRepository>();
        services.AddScoped<IJobDutyItemRepository, JobDutyItemRepository>();
        services.AddScoped<IJobPpeRequirementRepository, JobPpeRequirementRepository>();
        services.AddScoped<IJobEquipmentTrainingRepository, JobEquipmentTrainingRepository>();
        services.AddScoped<IJobMedicalRequirementRepository, JobMedicalRequirementRepository>();
        services.AddScoped<IJobResponsibilityKpiRepository, JobResponsibilityKpiRepository>();
        services.AddScoped<IUnionRepository, UnionRepository>();
        services.AddScoped<ICollectiveBargainingAgreementRepository, CollectiveBargainingAgreementRepository>();
        services.AddScoped<IJobFamilyRepository, JobFamilyRepository>();
        services.AddScoped<IJobSubFamilyRepository, JobSubFamilyRepository>();
        services.AddScoped<IJobLevelRepository, JobLevelRepository>();
        services.AddScoped<ICompetencyRepository, CompetencyRepository>();
        services.AddScoped<ICompetencySkillIndicatorRepository, CompetencySkillIndicatorRepository>();
        services.AddScoped<IPositionCompetencyRepository, PositionCompetencyRepository>();
        services.AddScoped<IEmployeeCompetencyRepository, EmployeeCompetencyRepository>();
        services.AddScoped<IEmployeeCompetencyHistoryRepository, EmployeeCompetencyHistoryRepository>();
        services.AddScoped<ISuccessionPlanRepository, SuccessionPlanRepository>();
        services.AddScoped<ISuccessionCompetencyRequirementRepository, SuccessionCompetencyRequirementRepository>();
        services.AddScoped<ISuccessionCandidateRepository, SuccessionCandidateRepository>();
        services.AddScoped<ISuccessionCandidateGapRepository, SuccessionCandidateGapRepository>();
        services.AddScoped<ISuccessionDevelopmentActivityRepository, SuccessionDevelopmentActivityRepository>();
        services.AddScoped<ISuccessionDevelopmentMilestoneRepository, SuccessionDevelopmentMilestoneRepository>();
        services.AddScoped<ISuccessionActionRepository, SuccessionActionRepository>();
        services.AddScoped<ISuccessionPlanHistoryRepository, SuccessionPlanHistoryRepository>();
        services.AddScoped<ISuccessionDocumentRepository, SuccessionDocumentRepository>();
        services.AddScoped<ITalentPoolRepository, TalentPoolRepository>();
        services.AddScoped<ITalentPoolMemberRepository, TalentPoolMemberRepository>();
        services.AddScoped<ITalentReviewSessionRepository, TalentReviewSessionRepository>();
        services.AddScoped<ITalentReviewRatingRepository, TalentReviewRatingRepository>();
        services.AddScoped<IOrientationCategoryRepository, OrientationCategoryRepository>();
        services.AddScoped<IOrientationProgramRepository, OrientationProgramRepository>();
        services.AddScoped<IOrientationModuleRepository, OrientationModuleRepository>();
        services.AddScoped<IOrientationContentItemRepository, OrientationContentItemRepository>();
        services.AddScoped<IOrientationPrerequisiteRepository, OrientationPrerequisiteRepository>();
        services.AddScoped<IOrientationAudienceRuleRepository, OrientationAudienceRuleRepository>();
        services.AddScoped<IOrientationSessionRepository, OrientationSessionRepository>();
        services.AddScoped<IOrientationSessionFacilitatorRepository, OrientationSessionFacilitatorRepository>();
        services.AddScoped<IOrientationAttendanceRecordRepository, OrientationAttendanceRecordRepository>();
        services.AddScoped<IEmployeeOrientationRepository, EmployeeOrientationRepository>();
        services.AddScoped<IOrientationContentProgressRepository, OrientationContentProgressRepository>();
        services.AddScoped<IOrientationAssessmentQuestionRepository, OrientationAssessmentQuestionRepository>();
        services.AddScoped<IOrientationAssessmentOptionRepository, OrientationAssessmentOptionRepository>();
        services.AddScoped<IOrientationAssessmentResponseRepository, OrientationAssessmentResponseRepository>();
        services.AddScoped<IOrientationAcknowledgementRepository, OrientationAcknowledgementRepository>();
        services.AddScoped<IOrientationFeedbackRepository, OrientationFeedbackRepository>();
        services.AddScoped<IOrientationCertificateRepository, OrientationCertificateRepository>();
        services.AddScoped<IOrientationNotificationRepository, OrientationNotificationRepository>();
        services.AddScoped<IStaffTravelRequestRepository, StaffTravelRequestRepository>();
        services.AddScoped<IStaffGroupTravelRepository, StaffGroupTravelRepository>();
        services.AddScoped<IStaffTravelRequestCommentRepository, StaffTravelRequestCommentRepository>();
        services.AddScoped<IStaffTravelRequestAttachmentRepository, StaffTravelRequestAttachmentRepository>();
        services.AddScoped<IStaffTravelItineraryRepository, StaffTravelItineraryRepository>();
        services.AddScoped<IStaffTravelItineraryLegRepository, StaffTravelItineraryLegRepository>();
        services.AddScoped<IStaffTravelItineraryActivityRepository, StaffTravelItineraryActivityRepository>();
        services.AddScoped<IStaffTravelFlightBookingRepository, StaffTravelFlightBookingRepository>();
        services.AddScoped<IStaffTravelFlightSegmentRepository, StaffTravelFlightSegmentRepository>();
        services.AddScoped<IStaffTravelHotelBookingRepository, StaffTravelHotelBookingRepository>();
        services.AddScoped<IStaffTravelGroundTransportRepository, StaffTravelGroundTransportRepository>();
        services.AddScoped<IStaffTravelCarRentalBookingRepository, StaffTravelCarRentalBookingRepository>();
        services.AddScoped<IStaffTravelBudgetRepository, StaffTravelBudgetRepository>();
        services.AddScoped<IStaffTravelExpenseClaimRepository, StaffTravelExpenseClaimRepository>();
        services.AddScoped<IStaffTravelExpenseClaimLineRepository, StaffTravelExpenseClaimLineRepository>();
        services.AddScoped<IStaffTravelAdvanceRepository, StaffTravelAdvanceRepository>();
        services.AddScoped<IStaffTravelPerDiemRateRepository, StaffTravelPerDiemRateRepository>();
        services.AddScoped<IStaffTravelPolicyRepository, StaffTravelPolicyRepository>();
        services.AddScoped<IStaffTravelPolicyRuleRepository, StaffTravelPolicyRuleRepository>();
        services.AddScoped<IStaffTravelPolicyExceptionRepository, StaffTravelPolicyExceptionRepository>();
        services.AddScoped<IStaffTravelDocumentRepository, StaffTravelDocumentRepository>();
        services.AddScoped<IStaffTravelVisaRequirementRepository, StaffTravelVisaRequirementRepository>();
        services.AddScoped<IStaffTravelVisaApplicationRepository, StaffTravelVisaApplicationRepository>();
        services.AddScoped<IStaffTravelRiskAssessmentRepository, StaffTravelRiskAssessmentRepository>();
        services.AddScoped<IStaffTravelAlertRepository, StaffTravelAlertRepository>();
        services.AddScoped<IStaffTravelAlertNotificationRepository, StaffTravelAlertNotificationRepository>();
        services.AddScoped<IStaffTravelInsurancePolicyRepository, StaffTravelInsurancePolicyRepository>();
        services.AddScoped<IStaffTravelHealthRequirementRepository, StaffTravelHealthRequirementRepository>();
        services.AddScoped<IJobCandidateRepository, JobCandidateRepository>();
        services.AddScoped<IJobCandidateQualificationRepository, JobCandidateQualificationRepository>();
        services.AddScoped<IJobCandidateWorkHistoryRepository, JobCandidateWorkHistoryRepository>();
        services.AddScoped<IJobCandidateRefereeRepository, JobCandidateRefereeRepository>();
        services.AddScoped<IJobCandidateSkillRepository, JobCandidateSkillRepository>();
        services.AddScoped<IJobCandidateInterestRepository, JobCandidateInterestRepository>();
        services.AddScoped<IJobCandidateDocumentRepository, JobCandidateDocumentRepository>();
        services.AddScoped<IJobCandidateNoteRepository, JobCandidateNoteRepository>();
        services.AddScoped<IJobCandidateLanguageRepository, JobCandidateLanguageRepository>();
        services.AddScoped<ICandidateTalentSegmentRepository, CandidateTalentSegmentRepository>();
        services.AddScoped<ICandidateSegmentMembershipRepository, CandidateSegmentMembershipRepository>();
        services.AddScoped<ICandidateEngagementEventRepository, CandidateEngagementEventRepository>();
        services.AddScoped<IJobVacancyRepository, JobVacancyRepository>();
        services.AddScoped<IJobVacancyAttachmentRepository, JobVacancyAttachmentRepository>();
        services.AddScoped<IJobVacancyStatusHistoryRepository, JobVacancyStatusHistoryRepository>();
        services.AddScoped<IVacancyPipelineStageAssignmentRepository, VacancyPipelineStageAssignmentRepository>();
        services.AddScoped<IJobPostingRepository, JobPostingRepository>();
        services.AddScoped<IRecruitmentPipelineRepository, RecruitmentPipelineRepository>();
        services.AddScoped<IRecruitmentPipelineStageRepository, RecruitmentPipelineStageRepository>();
        services.AddScoped<IJobShortlistingCriteriaRepository, JobShortlistingCriteriaRepository>();
        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
        services.AddScoped<IJobApplicationStageHistoryRepository, JobApplicationStageHistoryRepository>();
        services.AddScoped<IJobApplicantTestResultRepository, JobApplicantTestResultRepository>();
        services.AddScoped<IJobApplicantCommunicationRepository, JobApplicantCommunicationRepository>();
        services.AddScoped<IShortlistDecisionLogRepository, ShortlistDecisionLogRepository>();
        services.AddScoped<IShortlistReviewRepository, ShortlistReviewRepository>();
        services.AddScoped<IJobInterviewQuestionTypeRepository, JobInterviewQuestionTypeRepository>();
        services.AddScoped<IJobInterviewQuestionDetailRepository, JobInterviewQuestionDetailRepository>();
        services.AddScoped<IInterviewQuestionPresetRepository, InterviewQuestionPresetRepository>();
        services.AddScoped<IInterviewQuestionPresetItemRepository, InterviewQuestionPresetItemRepository>();
        services.AddScoped<IJobInterviewRepository, JobInterviewRepository>();
        // Round 4, lane E: one repository for the whole test engine — the reference-number
        // generator needs the Data layer; everything else goes through IUnitOfWork.Repository<T>().
        services.AddScoped<IRecruitmentTestRepository, RecruitmentTestRepository>();
        services.AddScoped<IJobInterviewPanelistRepository, JobInterviewPanelistRepository>();
        services.AddScoped<IJobInterviewExternalPanelistRepository, JobInterviewExternalPanelistRepository>();
        services.AddScoped<IJobIntervieweeRepository, JobIntervieweeRepository>();
        services.AddScoped<IJobInterviewQuestionRepository, JobInterviewQuestionRepository>();
        services.AddScoped<IJobInterviewSelectedQuestionRepository, JobInterviewSelectedQuestionRepository>();
        services.AddScoped<IJobInterviewScoreSummaryRepository, JobInterviewScoreSummaryRepository>();
        services.AddScoped<IJobInterviewScoreEntryRepository, JobInterviewScoreEntryRepository>();
        services.AddScoped<IJobInterviewScoreDraftRepository, JobInterviewScoreDraftRepository>();
        services.AddScoped<IJobOfferRepository, JobOfferRepository>();
        services.AddScoped<IJobOfferBenefitRepository, JobOfferBenefitRepository>();
        services.AddScoped<IJobOfferNoteRepository, JobOfferNoteRepository>();
        services.AddScoped<IJobHireRecordRepository, JobHireRecordRepository>();
        services.AddScoped<IPreEmploymentCheckRepository, PreEmploymentCheckRepository>();
        services.AddScoped<IPreEmploymentCheckItemRepository, PreEmploymentCheckItemRepository>();
        services.AddScoped<IReferenceCheckResponseRepository, ReferenceCheckResponseRepository>();
        services.AddScoped<IPreEmploymentCheckTemplateRepository, PreEmploymentCheckTemplateRepository>();
        services.AddScoped<IOnboardingPlanTemplateRepository, OnboardingPlanTemplateRepository>();
        services.AddScoped<IOnboardingTaskTemplateRepository, OnboardingTaskTemplateRepository>();
        services.AddScoped<IOnboardingPlanRepository, OnboardingPlanRepository>();
        services.AddScoped<IOnboardingTaskRepository, OnboardingTaskRepository>();
        services.AddScoped<IOnboardingTaskCommentRepository, OnboardingTaskCommentRepository>();
        services.AddScoped<IOnboardingAssetRepository, OnboardingAssetRepository>();
        services.AddScoped<IProbationPeriodRepository, ProbationPeriodRepository>();
        services.AddScoped<IProbationReviewRepository, ProbationReviewRepository>();
        services.AddScoped<IProbationExtensionRepository, ProbationExtensionRepository>();
        services.AddScoped<IStaffMovementRepository, StaffMovementRepository>();
        services.AddScoped<IStaffMovementApprovalLevelRepository, StaffMovementApprovalLevelRepository>();
        services.AddScoped<IStaffMovementStatusHistoryRepository, StaffMovementStatusHistoryRepository>();
        services.AddScoped<IStaffMovementAttachmentRepository, StaffMovementAttachmentRepository>();
        services.AddScoped<IStaffMovementChecklistItemRepository, StaffMovementChecklistItemRepository>();
        services.AddScoped<IStaffPromotionRepository, StaffPromotionRepository>();
        services.AddScoped<IStaffTransferRepository, StaffTransferRepository>();
        services.AddScoped<IStaffDemotionRepository, StaffDemotionRepository>();
        services.AddScoped<IStaffSecondmentRepository, StaffSecondmentRepository>();
        services.AddScoped<IStaffActingAppointmentRepository, StaffActingAppointmentRepository>();
        services.AddScoped<IEmployeeCareerPathRepository, EmployeeCareerPathRepository>();
        services.AddScoped<IStaffOffenseRepository, StaffOffenseRepository>();
        services.AddScoped<IStaffOffenseProcedureRepository, StaffOffenseProcedureRepository>();
        services.AddScoped<IStaffDisciplinaryActionTypeRepository, StaffDisciplinaryActionTypeRepository>();
        services.AddScoped<IStaffDisciplinaryActionRepository, StaffDisciplinaryActionRepository>();
        services.AddScoped<IStaffDisciplineInvestigationRepository, StaffDisciplineInvestigationRepository>();
        services.AddScoped<IStaffDisciplineHearingRepository, StaffDisciplineHearingRepository>();
        services.AddScoped<IStaffDisciplineWarningRepository, StaffDisciplineWarningRepository>();
        services.AddScoped<IStaffDisciplineSuspensionRepository, StaffDisciplineSuspensionRepository>();
        services.AddScoped<IStaffDisciplineFineRepository, StaffDisciplineFineRepository>();
        services.AddScoped<IStaffDisciplineTerminationRepository, StaffDisciplineTerminationRepository>();
        services.AddScoped<IStaffDisciplineSeparationRepository, StaffDisciplineSeparationRepository>();
        services.AddScoped<IStaffDisciplineAppealRepository, StaffDisciplineAppealRepository>();
        services.AddScoped<IStaffDisciplineCorrectiveActionRepository, StaffDisciplineCorrectiveActionRepository>();
        services.AddScoped<IStaffDisciplineCorrectiveActionItemRepository, StaffDisciplineCorrectiveActionItemRepository>();
        services.AddScoped<IStaffDisciplineActionStepRepository, StaffDisciplineActionStepRepository>();
        services.AddScoped<IStaffDisciplineWitnessRepository, StaffDisciplineWitnessRepository>();
        services.AddScoped<IStaffDisciplineDocumentRepository, StaffDisciplineDocumentRepository>();
        services.AddScoped<IStaffDisciplineNoteRepository, StaffDisciplineNoteRepository>();
        services.AddScoped<IStaffDisciplineNotificationRepository, StaffDisciplineNotificationRepository>();
        services.AddScoped<IStaffDisciplineLegalReviewRepository, StaffDisciplineLegalReviewRepository>();
        services.AddScoped<IStaffRequisitionRepository, StaffRequisitionRepository>();
        services.AddScoped<IPositionVacancyRepository, PositionVacancyRepository>();
        services.AddScoped<IStaffRequisitionCostRepository, StaffRequisitionCostRepository>();
        services.AddScoped<IStaffRequisitionAttachmentRepository, StaffRequisitionAttachmentRepository>();
        services.AddScoped<IStaffRequisitionCommentRepository, StaffRequisitionCommentRepository>();
        services.AddScoped<IStaffRequisitionHistoryRepository, StaffRequisitionHistoryRepository>();
        services.AddScoped<ISheIncidentTypeRepository, SheIncidentTypeRepository>();
        services.AddScoped<ISheInjuryTypeRepository, SheInjuryTypeRepository>();
        services.AddScoped<ISheBodyPartRepository, SheBodyPartRepository>();
        services.AddScoped<ISheCorrectiveActionTemplateRepository, SheCorrectiveActionTemplateRepository>();
        services.AddScoped<ISheRegulatoryBodyRepository, SheRegulatoryBodyRepository>();
        services.AddScoped<ISafetyIncidentRepository, SafetyIncidentRepository>();
        services.AddScoped<ISafetyIncidentCorrectiveActionRepository, SafetyIncidentCorrectiveActionRepository>();
        services.AddScoped<ISheHazardRepository, SheHazardRepository>();
        services.AddScoped<ISheRiskAssessmentRepository, SheRiskAssessmentRepository>();
        services.AddScoped<ISheInspectionChecklistRepository, SheInspectionChecklistRepository>();
        services.AddScoped<ISafetyInspectionRepository, SafetyInspectionRepository>();
        services.AddScoped<IShePermitToWorkRepository, ShePermitToWorkRepository>();
        services.AddScoped<IPpeTypeRepository, PpeTypeRepository>();
        services.AddScoped<IPpeInventoryRepository, PpeInventoryRepository>();
        services.AddScoped<IPpeIssuanceRepository, PpeIssuanceRepository>();
        services.AddScoped<IJobRolePpeRequirementRepository, JobRolePpeRequirementRepository>();
        services.AddScoped<ISafetyEquipmentRepository, SafetyEquipmentRepository>();
        services.AddScoped<ISafetyEquipmentInspectionRepository, SafetyEquipmentInspectionRepository>();
        services.AddScoped<ISheContractorRepository, SheContractorRepository>();
        services.AddScoped<ISheContractorInspectionRepository, SheContractorInspectionRepository>();
        services.AddScoped<ISheContractorNonComplianceRepository, SheContractorNonComplianceRepository>();
        services.AddScoped<ISheContractorDocumentRepository, SheContractorDocumentRepository>();
        services.AddScoped<ISheTrainingPlanRepository, SheTrainingPlanRepository>();
        services.AddScoped<ISheTrainingProgramRepository, SheTrainingProgramRepository>();
        services.AddScoped<ISheTrainingAttendanceRepository, SheTrainingAttendanceRepository>();
        services.AddScoped<ISheWasteTypeRepository, SheWasteTypeRepository>();
        services.AddScoped<ISheWasteDisposalRecordRepository, SheWasteDisposalRecordRepository>();
        services.AddScoped<ISheEnvironmentalIncidentRepository, SheEnvironmentalIncidentRepository>();
        services.AddScoped<ISheEnvironmentalMonitoringRecordRepository, SheEnvironmentalMonitoringRecordRepository>();
        services.AddScoped<ISheOccupationalHealthSurveillanceRepository, SheOccupationalHealthSurveillanceRepository>();
        services.AddScoped<ISheFirstAidStationRepository, SheFirstAidStationRepository>();
        services.AddScoped<ISheWellnessProgramRepository, SheWellnessProgramRepository>();
        services.AddScoped<IEmergencyPlanRepository, EmergencyPlanRepository>();
        services.AddScoped<IEmergencyDrillRepository, EmergencyDrillRepository>();
        services.AddScoped<IEmergencyResponseTeamRepository, EmergencyResponseTeamRepository>();
        services.AddScoped<ISheRegulatoryObligationRepository, SheRegulatoryObligationRepository>();
        services.AddScoped<ISafetySignRepository, SafetySignRepository>();
        services.AddScoped<IShePerformanceSnapshotRepository, ShePerformanceSnapshotRepository>();
        services.AddScoped<ISafetyCommitteeRepository, SafetyCommitteeRepository>();
        services.AddScoped<ISafetyCommitteeMemberRepository, SafetyCommitteeMemberRepository>();
        services.AddScoped<ISafetyMeetingRepository, SafetyMeetingRepository>();
        services.AddScoped<ISafetyMeetingActionItemRepository, SafetyMeetingActionItemRepository>();
        services.AddScoped<ISheReturnToWorkPlanRepository, SheReturnToWorkPlanRepository>();
        services.AddScoped<IHealthcareFacilityRepository, HealthcareFacilityRepository>();
        services.AddScoped<IPhysicianRepository, PhysicianRepository>();
        services.AddScoped<IFacilityServiceRepository, FacilityServiceRepository>();
        services.AddScoped<IMedicalInsuranceProviderRepository, MedicalInsuranceProviderRepository>();
        services.AddScoped<IMedicalInsurancePlanRepository, MedicalInsurancePlanRepository>();
        services.AddScoped<IEmployeeMedicalInsurancePolicyRepository, EmployeeMedicalInsurancePolicyRepository>();
        services.AddScoped<IMedicalInsurancePolicyDependentRepository, MedicalInsurancePolicyDependentRepository>();
        services.AddScoped<IMedicalInsuranceClaimRepository, MedicalInsuranceClaimRepository>();
        services.AddScoped<IMedicalInsuranceProviderFacilityRepository, MedicalInsuranceProviderFacilityRepository>();
        services.AddScoped<IMedicalInsuranceProviderDocumentRepository, MedicalInsuranceProviderDocumentRepository>();
        services.AddScoped<IMedicalInsurancePremiumRecordRepository, MedicalInsurancePremiumRecordRepository>();
        services.AddScoped<IMedicalBenefitSchemeRepository, MedicalBenefitSchemeRepository>();
        services.AddScoped<IMedicalBenefitTierRepository, MedicalBenefitTierRepository>();
        services.AddScoped<IEmployeeHealthProfileRepository, EmployeeHealthProfileRepository>();
        services.AddScoped<IEmployeeHealthConditionRepository, EmployeeHealthConditionRepository>();
        services.AddScoped<IEmployeeAllergyRepository, EmployeeAllergyRepository>();
        services.AddScoped<IEmployeeMedicalExamRepository, EmployeeMedicalExamRepository>();
        services.AddScoped<IEmployeeMedicalExamDocumentRepository, EmployeeMedicalExamDocumentRepository>();
        services.AddScoped<IMedicalClaimPreAuthorizationRepository, MedicalClaimPreAuthorizationRepository>();
        services.AddScoped<IMedicalReferralRepository, MedicalReferralRepository>();
        services.AddScoped<IMedicalAppointmentRepository, MedicalAppointmentRepository>();
        services.AddScoped<INHISClaimRepository, NHISClaimRepository>();
        services.AddScoped<INHISClaimDocumentRepository, NHISClaimDocumentRepository>();
        services.AddScoped<IMedicalExpenseClaimRepository, MedicalExpenseClaimRepository>();
        services.AddScoped<IMedicalExpenseApprovalRepository, MedicalExpenseApprovalRepository>();
        services.AddScoped<IMedicalExpenseItemRepository, MedicalExpenseItemRepository>();
        services.AddScoped<IMedicalExpenseDocumentRepository, MedicalExpenseDocumentRepository>();
        services.AddScoped<IMedicalExpenseClaimNoteRepository, MedicalExpenseClaimNoteRepository>();
        services.AddScoped<IStaffAttendanceRecordRepository, StaffAttendanceRecordRepository>();
        services.AddScoped<IStaffDailyAttendanceRepository, StaffDailyAttendanceRepository>();
        services.AddScoped<IStaffAttendanceLogRepository, StaffAttendanceLogRepository>();
        services.AddScoped<IStaffAttendanceRegularizationRepository, StaffAttendanceRegularizationRepository>();
        services.AddScoped<IStaffMonthlyAttendanceSummaryRepository, StaffMonthlyAttendanceSummaryRepository>();
        services.AddScoped<IStaffBulkAttendanceImportRepository, StaffBulkAttendanceImportRepository>();
        services.AddScoped<IStaffBulkAttendanceImportRowRepository, StaffBulkAttendanceImportRowRepository>();
        services.AddScoped<IWorkScheduleRepository, WorkScheduleRepository>();
        services.AddScoped<IEmployeeWorkScheduleRepository, EmployeeWorkScheduleRepository>();
        services.AddScoped<IShiftDefinitionRepository, ShiftDefinitionRepository>();
        services.AddScoped<IShiftAssignmentRepository, ShiftAssignmentRepository>();
        services.AddScoped<IShiftRotationPlanRepository, ShiftRotationPlanRepository>();
        services.AddScoped<IShiftRotationStageRepository, ShiftRotationStageRepository>();
        services.AddScoped<IShiftRotationMemberRepository, ShiftRotationMemberRepository>();
        services.AddScoped<IPositionOvertimePolicyRepository, PositionOvertimePolicyRepository>();
        services.AddScoped<IEmployeeOvertimeOverrideRepository, EmployeeOvertimeOverrideRepository>();
        services.AddScoped<IStaffOvertimeRequestRepository, StaffOvertimeRequestRepository>();
        services.AddScoped<IEmployeeBiometricRepository, EmployeeBiometricRepository>();
        services.AddScoped<IStaffAttendanceDeviceRepository, StaffAttendanceDeviceRepository>();
        services.AddScoped<IGeofenceZoneRepository, GeofenceZoneRepository>();
        services.AddScoped<IAttendanceLocationVerificationLogRepository, AttendanceLocationVerificationLogRepository>();
        services.AddScoped<IRemoteWorkRequestRepository, RemoteWorkRequestRepository>();
        services.AddScoped<IHolidayCalendarRepository, HolidayCalendarRepository>();
        services.AddScoped<IPayPeriodRepository, PayPeriodRepository>();
        services.AddScoped<IStaffAttendancePayrollExportRepository, StaffAttendancePayrollExportRepository>();
        services.AddScoped<IStaffAttendanceAlertRuleRepository, StaffAttendanceAlertRuleRepository>();
        services.AddScoped<IStaffAttendanceAlertRepository, StaffAttendanceAlertRepository>();
        services.AddScoped<IConsultantClientRepository, ConsultantClientRepository>();
        services.AddScoped<IClientEngagementRepository, ClientEngagementRepository>();
        services.AddScoped<IConsultantTimesheetRepository, ConsultantTimesheetRepository>();
        services.AddScoped<IConsultantTimesheetEntryRepository, ConsultantTimesheetEntryRepository>();
        services.AddScoped<IClientTimesheetConfirmationRepository, ClientTimesheetConfirmationRepository>();
        services.AddScoped<ITimesheetInvoiceRepository, TimesheetInvoiceRepository>();
        services.AddScoped<ITimesheetInvoiceLinkRepository, TimesheetInvoiceLinkRepository>();
        // Payroll owns the salary structure; HR mirrors it. Registered before the salary services because
        // they depend on it for reconcile-on-read.
        services.AddScoped<ISalaryStructureProjectionService, SalaryStructureProjectionService>();
        services.AddScoped<IPayComponentProjectionService, PayComponentProjectionService>();
        services.AddScoped<ISalaryStructureService, SalaryStructureService>();
        services.AddScoped<ISalaryGradeService, SalaryStructureService>();
        services.AddScoped<ISalaryLevelService, SalaryStructureService>();
        services.AddScoped<ISalaryNotchService, SalaryStructureService>();
        services.AddScoped<IStaffLevelService, StaffLevelService>();
        services.AddScoped<IBenefitPolicyService, BenefitPolicyService>();
        services.AddScoped<IEmployeeBenefitEnrollmentService, EmployeeBenefitEnrollmentService>();
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<ILeaveTypeService, LeaveTypeService>();
        services.AddScoped<ILeavePlanService, LeavePlanService>();
        services.AddScoped<IPublicHolidayService, PublicHolidayService>();
        services.AddScoped<ILeaveEncashmentService, LeaveEncashmentService>();
        services.AddScoped<ILeaveBalanceRecalculationService, LeaveBalanceRecalculationService>();
        services.AddScoped<ILeaveEntitlementService, LeaveEntitlementService>();
        // Round 5, lane G: "which days had been used by then" — the expiry run, reminder sweep 5 and
        // the leave owed report share it, so they cannot disagree about a lapse.
        services.AddScoped<ILeaveUsageReader, LeaveUsageReader>();
        // Round 5, lane L2: "how much annual leave is owed at a date" — the leave owed report and a
        // leaver's final settlement share it, so Finance's figure and the leaver's cannot disagree.
        services.AddScoped<ILeaveOwedCalculator, LeaveOwedCalculator>();
        // Approved leave reaches the attendance register through this. Before it, StaffDailyAttendance
        // .LeaveRequestId and StaffAttendanceStatus.OnLeave were both written by nothing, so the
        // payroll export read zero days on leave for everybody (closure plan L-27).
        services.AddScoped<ILeaveAttendancePostingService, LeaveAttendancePostingService>();
        services.AddScoped<IReasonCodeService, ReasonCodeService>();
        // Round-2 lane D1 (Q-4): the seeded contract-kind vocabulary, which had no service at all.
        services.AddScoped<IEmployeeContractTypeService, EmployeeContractTypeService>();

        // The relationship catalogue the referee, guarantor, next-of-kin and candidate-referee
        // screens pick from (round 2, lane D2).
        services.AddScoped<IRelationshipTypeService, RelationshipTypeService>();
        services.AddScoped<IDisabilityTypeService, DisabilityTypeService>(); // round 3, lane P2
        services.AddScoped<ILanguageService, LanguageService>(); // round 3, lane C1

        // Teams and committees (round 2, lane F).
        //
        // ⚠ The access guard is registered FIRST and shared by both slices' services. It holds the
        // whole authorisation story — HR acts on any team, lead/deputy on their own, a member only
        // on a task assigned to them — and two copies of an authorisation rule is exactly what
        // lane D2 measured the cost of.
        services.AddScoped<ITeamAccessGuard, TeamAccessGuard>();
        services.AddScoped<ITeamActivityService, TeamActivityService>();   // F1: charter, objectives, tasks
        services.AddScoped<ITeamMeetingService, TeamMeetingService>();     // F2: meetings, reviews, dashboard
        services.AddScoped<ITeamReminderService, TeamReminderService>();   // F2: the nightly sweep
        services.AddScoped<IEmployeeRelieverService, EmployeeRelieverService>();
        services.AddScoped<ILeaveYearEndService, LeaveYearEndService>();
        services.AddScoped<IEmolumentService, EmolumentService>();
        services.AddScoped<IAppraisalGradeDefinitionService, AppraisalGradeDefinitionService>();
        services.AddScoped<IKpiDefinitionService, KpiDefinitionService>();
        services.AddScoped<IAppraisalCompetencyService, AppraisalCompetencyService>();
        services.AddScoped<IPerformanceAppraisalService, PerformanceAppraisalService>();
        services.AddScoped<IPerformanceImprovementPlanService, PerformanceImprovementPlanService>();
        services.AddScoped<IEffectiveAppraisalConfigurationService, EffectiveAppraisalConfigurationService>();
        services.AddScoped<IAppraisalSettingsService, AppraisalSettingsService>();
        services.AddScoped<ICompanyHrPolicyProvider, CompanyHrPolicyProvider>();
        // ⚠ SCOPED is the contract, not a preference: the leave-year start month is cached for
        // one request and no longer. See ILeaveYearContext (entitlement plan C1).
        services.AddScoped<ILeaveYearContext, LeaveYearContext>();
        services.AddScoped<ICompanyHrPolicySettingsService, CompanyHrPolicySettingsService>();
        services.AddScoped<IProbationLetterService, ProbationLetterService>();
        services.AddScoped<ICompanyProfileProvider, CompanyProfileProvider>();
        services.AddScoped<ICompanyProfileService, CompanyProfileService>();
        services.AddScoped<IAppraisalCycleService, AppraisalCycleService>();
        services.AddScoped<IAppraisalCycleTargetService, AppraisalCycleTargetService>();
        services.AddScoped<IPeerNominationService, PeerNominationService>();
        services.AddScoped<IPeerEvaluationService, PeerEvaluationService>();
        services.AddScoped<IAppraisalTemplateService, AppraisalTemplateService>();
        services.AddScoped<IGoalLibraryService, GoalLibraryService>();
        services.AddScoped<IAppraisalCycleTemplateService, AppraisalCycleTemplateService>();
        services.AddScoped<ICycleCoverageService, CycleCoverageService>();
        services.AddScoped<IStrategicGoalService, StrategicGoalService>();
        services.AddScoped<IPerformanceLinkService, PerformanceLinkService>();
        services.AddScoped<IPerformanceAnalyticsService, PerformanceAnalyticsService>();
        services.AddScoped<ISalaryReviewProposalService, SalaryReviewProposalService>();
        services.AddScoped<IEmployeeSalaryChangeRequestService, EmployeeSalaryChangeRequestService>();
        services.AddScoped<IEmploymentActionProposalService, EmploymentActionProposalService>();
        services.AddScoped<IPerformanceRatingResolver, PerformanceRatingResolver>();
        services.AddScoped<ITalentRatingSyncService, TalentRatingSyncService>();
        services.AddScoped<IAppraisalOutcomeService, AppraisalOutcomeService>();
        services.AddScoped<IOutcomeRecommendationHandler, SuccessionNominationHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, TrainingRequestHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, MeritIncreaseHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, BonusHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, ConfirmProbationHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, ExtendProbationHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, PipRecommendationHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, PromotionActionHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, DemotionActionHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, ContractRenewalActionHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, TerminationActionHandler>();
        services.AddScoped<IOutcomeRecommendationHandler, RecognitionActionHandler>();
        services.AddScoped<ICompanyGoalService, CompanyGoalService>();
        services.AddScoped<IUnitGoalService, UnitGoalService>();
        services.AddScoped<IEmployeeGoalService, EmployeeGoalService>();
        services.AddScoped<ITeamGoalsQueryService, TeamGoalsQueryService>();
        services.AddScoped<IGoalDetailQueryService, GoalDetailQueryService>();
        services.AddScoped<IAtRiskGoalsQueryService, AtRiskGoalsQueryService>();
        services.AddScoped<IGoalWorkflowCommandService, GoalWorkflowCommandService>();
        services.AddScoped<IGoalRiskSettingsProvider, GoalRiskSettingsProvider>();
        services.AddScoped<IGoalRiskSettingsService, GoalRiskSettingsService>();
        services.AddScoped<IGoalRiskEvaluator, GoalRiskEvaluator>();
        services.AddScoped<ICheckInService, CheckInService>();
        services.AddScoped<IPerformanceJournalService, PerformanceJournalService>();
        services.AddScoped<IDevelopmentPlanService, DevelopmentPlanService>();
        services.AddScoped<IDevelopmentPlanFeedbackService, DevelopmentPlanFeedbackService>();
        services.AddScoped<IAppraisalReviewEventService, AppraisalReviewEventService>();
        services.AddScoped<IAppraisalConversationService, AppraisalConversationService>();
        services.AddScoped<ICalibrationSessionService, CalibrationSessionService>();
        services.AddScoped<IAppraisalWorkflowService, AppraisalWorkflowService>();
        services.AddScoped<IHRCycleDashboardQueryService, HRCycleDashboardQueryService>();
        services.AddScoped<IAppraisalNotificationService, AppraisalNotificationService>();
        services.AddScoped<IIdentificationTypeService, IdentificationTypeService>();
        services.AddScoped<IOrganizationStructureService, OrganizationStructureService>();
        services.AddScoped<IOrganizationLevelService, OrganizationLevelService>();
        services.AddScoped<IOrganizationUnitService, OrganizationUnitService>();
        services.AddScoped<IOrganizationUnitHistoryService, OrganizationUnitHistoryService>();
        services.AddScoped<IOrganogramService, OrganogramService>();
        // Slice 4b. Until this line the Team/TeamMember/TeamMemberHistory entities had DbSets,
        // tables and EF configuration but no way at all to write them.
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ILocationStructureService, LocationStructureService>();
        services.AddScoped<ILocationLevelService, LocationLevelService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<ILocationContactService, LocationContactService>();
        services.AddScoped<IAssetTypeService, AssetTypeService>();
        services.AddScoped<IAssetTypeAttributeService, AssetTypeAttributeService>();
        services.AddScoped<ICompanyAssetService, CompanyAssetService>();
        services.AddScoped<IAssetAttributeValueService, AssetAttributeValueService>();
        services.AddScoped<IAssetImageService, AssetImageService>();
        services.AddScoped<IAssetAssignmentService, AssetAssignmentService>();
        services.AddScoped<IAssetMaintenanceService, AssetMaintenanceService>();
        services.AddScoped<IAssetAttachmentService, AssetAttachmentService>();
        services.AddScoped<IAssetRequisitionService, AssetRequisitionService>();
        services.AddScoped<IAssetTransferService, AssetTransferService>();
        // AST-5 / AST-5b — the responsibility document, rendered from the HR-editable template.
        // Its email-event catalog is registered with the other catalogs further down.
        services.AddScoped<IAssetTermsLetterService, AssetTermsLetterService>();
        // AST-3 / D-d — charging an employee for an asset they damaged, lost or never returned.
        services.AddScoped<IAssetSurchargeService, AssetSurchargeService>();
        services.AddScoped<IAwardTypeService, AwardTypeService>();
        services.AddScoped<IAwardCycleService, AwardCycleService>();
        services.AddScoped<IAwardEligibilityService, AwardEligibilityService>();
        services.AddScoped<IAwardVotingService, AwardVotingService>();
        services.AddScoped<IAwardCommitteeScoringService, AwardCommitteeScoringService>();
        services.AddScoped<IAwardCandidateGenerationService, AwardCandidateGenerationService>();
        services.AddScoped<IAwardLevelService, AwardLevelService>();
        services.AddScoped<IAwardBudgetService, AwardBudgetService>();
        services.AddScoped<IEmployeeAwardService, EmployeeAwardService>();
        services.AddScoped<IAwardAttachmentService, AwardAttachmentService>();
        services.AddScoped<IAwardNominationService, AwardNominationService>();
        services.AddScoped<ITeamAwardNomineeService, TeamAwardNomineeService>();
        services.AddScoped<IAwardNomineeContributionService, AwardNomineeContributionService>();
        services.AddScoped<IAwardNominationAttachmentService, AwardNominationAttachmentService>();
        services.AddScoped<IAwardCommitteeService, AwardCommitteeService>();
        services.AddScoped<IAwardCommitteeMemberService, AwardCommitteeMemberService>();
        services.AddScoped<IAwardCommitteeReviewService, AwardCommitteeReviewService>();
        services.AddScoped<ILongServiceAwardService, LongServiceAwardService>();
        services.AddScoped<ILongServiceMilestoneService, LongServiceMilestoneService>();
        services.AddScoped<ILongServiceSweepService, LongServiceSweepService>();
        services.AddScoped<IAwardTypeTargetService, AwardTypeTargetService>();
        // Round 4, D5 — one person's diary, assembled from the same commitment sources the
        // interview clash check fans out over.
        services.AddScoped<ErpSystem.Core.Services.HR.CompanySchedule.IPersonalScheduleService,
            ErpSystem.Core.Services.HR.CompanySchedule.PersonalScheduleService>();
        services.AddScoped<ICompanyEventService, CompanyEventService>();
        // ⚠ Round 4, D6. NOT optional: without the catalogue registration TemplatedEmailService has
        // no fallback for the CompanySchedule module, and every invitation, reschedule notice and
        // cancellation throws instead of rendering its shipped default. The same trap lane F
        // recorded for the interview paper.
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog,
            ErpSystem.Core.Services.HR.CompanySchedule.CompanyScheduleEmailEventCatalog>();
        services.AddScoped<IMeetingRoomService, MeetingRoomService>();
        services.AddScoped<IRoomBookingService, RoomBookingService>();
        services.AddScoped<ICompanyMilestoneService, CompanyMilestoneService>();
        services.AddScoped<IBusinessClosureService, BusinessClosureService>();
        services.AddScoped<IFiscalYearService, FiscalYearService>();
        services.AddScoped<IJobDescriptionService, JobDescriptionService>();
        services.AddScoped<IUnionService, UnionService>();
        services.AddScoped<IJobArchitectureService, JobArchitectureService>();
        services.AddScoped<IManpowerBudgetService, ManpowerBudgetService>();
        services.AddScoped<ICompetencyService, CompetencyService>();
        services.AddScoped<ICompetencySkillIndicatorService, CompetencySkillIndicatorService>();
        services.AddScoped<IPositionCompetencyService, PositionCompetencyService>();
        services.AddScoped<IEmployeeCompetencyService, EmployeeCompetencyService>();
        services.AddScoped<IEmployeeCompetencyHistoryService, EmployeeCompetencyHistoryService>();
        services.AddScoped<ISuccessionPlanService, SuccessionPlanService>();
        services.AddScoped<ISuccessionCandidateService, SuccessionCandidateService>();
        services.AddScoped<ISuccessionDevelopmentActivityService, SuccessionDevelopmentActivityService>();
        services.AddScoped<ITalentPoolService, TalentPoolService>();
        services.AddScoped<ITalentPoolTypeDefinitionService, TalentPoolTypeDefinitionService>();
        services.AddScoped<ISuccessionCandidateSearchService, SuccessionCandidateSearchService>();
        services.AddScoped<ITalentReviewSessionService, TalentReviewSessionService>();
        services.AddScoped<ISheDashboardService, SheDashboardService>();
        services.AddScoped<ISheReferenceDataService, SheReferenceDataService>();
        services.AddScoped<ISafetyIncidentService, SafetyIncidentService>();
        services.AddScoped<ISheHazardService, SheHazardService>();
        services.AddScoped<ISheRiskAssessmentService, SheRiskAssessmentService>();
        services.AddScoped<ISheInspectionChecklistService, SheInspectionChecklistService>();
        services.AddScoped<ISafetyInspectionService, SafetyInspectionService>();
        services.AddScoped<IShePermitToWorkService, ShePermitToWorkService>();
        services.AddScoped<IPpeManagementService, PpeManagementService>();
        services.AddScoped<ISafetyEquipmentService, SafetyEquipmentService>();
        services.AddScoped<ISheContractorService, SheContractorService>();
        services.AddScoped<ISheTrainingService, SheTrainingService>();
        services.AddScoped<ISheWasteManagementService, SheWasteManagementService>();
        services.AddScoped<ISheEnvironmentalService, SheEnvironmentalService>();
        services.AddScoped<ISheOccupationalHealthService, SheOccupationalHealthService>();
        services.AddScoped<ISheEmergencyService, SheEmergencyService>();
        services.AddScoped<ISheRegulatoryComplianceService, SheRegulatoryComplianceService>();
        services.AddScoped<ISafetySignageService, SafetySignageService>();
        services.AddScoped<IShePerformanceService, ShePerformanceService>();
        services.AddScoped<ISafetyCommitteeService, SafetyCommitteeService>();
        services.AddScoped<ISheReturnToWorkService, SheReturnToWorkService>();
        services.AddScoped<ISheReminderService, SheReminderService>();
        services.AddScoped<IStaffMovementReminderService, StaffMovementReminderService>();
        services.AddScoped<ISheCorrectiveActionTrackerService, SheCorrectiveActionTrackerService>();
        services.AddScoped<ISheKpiComputationService, SheKpiComputationService>();
        services.AddScoped<ISheAuditService, SheAuditService>();
        services.AddScoped<ISheStopWorkService, SheStopWorkService>();
        services.AddScoped<ISheControlledDocumentService, SheControlledDocumentService>();
        services.AddScoped<ISheEnvironmentalPermitService, SheEnvironmentalPermitService>();
        services.AddScoped<ISheEnvironmentalGovernanceService, SheEnvironmentalGovernanceService>();
        services.AddScoped<ISheEnvironmentalReviewService, SheEnvironmentalReviewService>();
        services.AddScoped<ISheMonthlyEnvironmentalReportService, SheMonthlyEnvironmentalReportService>();
        services.AddScoped<IOrientationCategoryService, OrientationCategoryService>();
        services.AddScoped<IOrientationProgramService, OrientationProgramService>();
        services.AddScoped<IOrientationSessionService, OrientationSessionService>();
        services.AddScoped<IEmployeeOrientationService, EmployeeOrientationService>();
        services.AddScoped<IOrientationNotificationService, OrientationNotificationService>();
        services.AddScoped<IOrientationDashboardService, OrientationDashboardService>();

        // Round 4, lane I — the audience rules made to fire, and onboarding templates made to
        // choose themselves. The nightly host is registered in ServiceCollectionExtensions.
        services.AddScoped<IOrientationEnrollmentTriggerService, OrientationEnrollmentTriggerService>();
        // Round 4, lane K: the orientation & onboarding reminder engine — the first HR sweep that
        // delivers (an in-app notification and an email), not only logs.
        services.AddScoped<IOnboardingOrientationReminderService, OnboardingOrientationReminderService>();
        // Round 4, lane K-b: the lifecycle notices (staged with the event they report) and the
        // dispatcher that sends their queued emails.
        services.AddScoped<IOnboardingOrientationNotices, ErpSystem.Core.Services.HR.Orientation.OnboardingOrientationNoticeService>();
        services.AddScoped<IOrientationNoticeEmailDispatcher, ErpSystem.Core.Services.HR.Orientation.OrientationNoticeEmailDispatcher>();
        // Round 4, lane N: every HR email, listed from the registered catalogues, reworded per tenant.
        services.AddScoped<IHrLetterTemplateService, ErpSystem.Core.Services.HR.Templates.HrLetterTemplateService>();
        // Round 4 lane N-b: an HR letter as a PDF, for the offer letter attached to the Offer Issued email.
        services.AddScoped<ErpSystem.Core.Interfaces.Common.IHtmlToPdfRenderer, ErpSystem.Api.Services.DocumentManagement.HtmlToPdfRenderer>();
        services.AddScoped<IOnboardingTemplateApplicabilityService, OnboardingTemplateApplicabilityService>();
        services.AddScoped<IStaffTravelRequestService, StaffTravelRequestService>();
        services.AddScoped<IStaffTravelItineraryService, StaffTravelItineraryService>();
        services.AddScoped<IStaffTravelBookingService, StaffTravelBookingService>();
        services.AddScoped<IStaffTravelFinanceService, StaffTravelFinanceService>();
        services.AddScoped<IStaffTravelPolicyService, StaffTravelPolicyService>();
        services.AddScoped<IStaffTravelComplianceService, StaffTravelComplianceService>();
        services.AddScoped<IHealthcareFacilityService, HealthcareFacilityService>();
        services.AddScoped<IMedicalInsuranceService, MedicalInsuranceService>();
        services.AddScoped<IMedicalBenefitSchemeService, MedicalBenefitSchemeService>();
        services.AddScoped<IEmployeeHealthService, EmployeeHealthService>();
        services.AddScoped<IMedicalClinicalService, MedicalClinicalService>();
        services.AddScoped<INHISService, NHISService>();
        services.AddScoped<IMedicalExpenseClaimService, MedicalExpenseClaimService>();
        services.AddScoped<IMedicalDashboardService, MedicalDashboardService>();
        // Medical boards (residue plan G4). ⚠ Leave and separation READ these; neither writes one.
        services.AddScoped<IMedicalBoardService, MedicalBoardService>();
        services.AddScoped<IStaffMovementService, StaffMovementService>();
        services.AddScoped<IStaffPromotionService, StaffPromotionService>();
        services.AddScoped<IStaffTransferService, StaffTransferService>();
        services.AddScoped<IStaffDemotionService, StaffDemotionService>();
        services.AddScoped<IStaffSecondmentService, StaffSecondmentService>();
        services.AddScoped<IStaffActingAppointmentService, StaffActingAppointmentService>();
        services.AddScoped<IEmployeeCareerPathService, EmployeeCareerPathService>();
        // Round 4, lane B. The criterion-value rules and the scoring engine are shared by the
        // vacancy path and the talent-pool screen, so neither can drift from the other.
        services.AddScoped<ErpSystem.Core.Services.HR.Recruitment.IShortlistingCriteriaResolver,
            ErpSystem.Core.Services.HR.Recruitment.ShortlistingCriteriaResolver>();
        services.AddScoped<IJobVacancyService, JobVacancyService>();
        services.AddScoped<IJobPostingService, JobPostingService>();
        services.AddScoped<IRecruitmentPipelineService, RecruitmentPipelineService>();
        services.AddScoped<IJobCandidateService, JobCandidateService>();
        services.AddScoped<ICandidateTalentSegmentService, CandidateTalentSegmentService>();
        services.AddScoped<ICandidateEngagementEventService, CandidateEngagementEventService>();
        // Round 4, lane B: screening the pool by a vacancy's criteria, then acting on the result.
        services.AddScoped<ITalentPoolScreeningService, TalentPoolScreeningService>();
        // Round 4, lane E: the recruitment test engine — authoring, sitting, marking, and the
        // ledger row that finally gives JobVacancy.TestScoreWeight something to blend.
        services.AddScoped<IRecruitmentTestService, RecruitmentTestService>();
        // Lane E6 — the printed paper and marking key. ⚠ The catalogue line is not optional: without
        // it TemplatedEmailService has no fallback for the RecruitmentTests module and every paper
        // throws rather than rendering its shipped default. Lane F's interview paper has the same pair.
        services.AddScoped<IRecruitmentTestPaperService, RecruitmentTestPaperService>();
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog,
            ErpSystem.Core.Services.HR.Recruitment.RecruitmentTestPaperEmailEventCatalog>();
        services.AddScoped<IJobApplicationService, JobApplicationService>();
        services.AddSingleton<IApplicationSnapshotService, ApplicationSnapshotService>();
        services.AddScoped<IApplicationPipelineService, ApplicationPipelineService>();
        services.AddScoped<IPipelineQueryService, PipelineQueryService>();
        services.AddScoped<IAutoScoringService, AutoScoringService>();
        services.AddScoped<IJobInterviewQuestionBankService, JobInterviewQuestionBankService>();
        services.AddScoped<IInterviewQuestionPresetService, InterviewQuestionPresetService>();
        // ── Round 4, lane D1 — where a panelist may already be ──────────────────────────────
        //
        // ⚠ SEVEN sources, and every one of them must be here. A source that is written and not
        // registered contributes nothing, and the clash check then answers "free" — the exact
        // failure the interface exists to stop, reappearing as a DI omission. The check reports
        // `sourcesConsulted` so a missing registration is visible rather than silent.
        services.AddScoped<IPanelistCommitmentSource, ErpSystem.Core.Services.HR.Recruitment.InterviewPanelCommitmentSource>();
        services.AddScoped<IPanelistCommitmentSource, ErpSystem.Core.Services.HR.Recruitment.LeaveCommitmentSource>();
        services.AddScoped<IPanelistCommitmentSource, ErpSystem.Core.Services.HR.Recruitment.TravelCommitmentSource>();
        services.AddScoped<IPanelistCommitmentSource, ErpSystem.Core.Services.HR.Recruitment.CompanyEventCommitmentSource>();
        services.AddScoped<IPanelistCommitmentSource, ErpSystem.Core.Services.HR.Recruitment.RoomBookingCommitmentSource>();
        services.AddScoped<IPanelistCommitmentSource, ErpSystem.Core.Services.HR.Recruitment.TrainingCommitmentSource>();
        services.AddScoped<IPanelistCommitmentSource, ErpSystem.Core.Services.HR.Recruitment.ClosureCommitmentSource>();
        services.AddScoped<IJobInterviewService, JobInterviewService>();
        // Round 4, lane F — the printed scoring sheet. ⚠ The catalogue registration below it is not
        // optional: without it TemplatedEmailService has no fallback for the Interviews module and
        // every paper throws rather than rendering its shipped default.
        services.AddScoped<IInterviewPaperService, InterviewPaperService>();
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog,
            ErpSystem.Core.Services.HR.Recruitment.InterviewPaperEmailEventCatalog>();
        services.AddScoped<IJobOfferService, JobOfferService>();
        services.AddScoped<IOfferLetterService, OfferLetterService>();
        services.AddScoped<IJobHireService, JobHireService>();
        services.AddScoped<IPreEmploymentCheckService, PreEmploymentCheckService>();
        services.AddScoped<IPreEmploymentCheckTemplateService, PreEmploymentCheckTemplateService>();
        services.AddScoped<IOnboardingPlanTemplateService, OnboardingPlanTemplateService>();
        services.AddScoped<IOnboardingPlanService, OnboardingPlanService>();
        services.AddScoped<IProbationService, ProbationService>();
        services.AddScoped<IRecruitmentAnalyticsService, RecruitmentAnalyticsService>();
        // Working days for HR's statutory deadlines (FR-HR-180's appeal windows). Org-level by
        // design — Monday to Friday less the tenant's public holidays, never the appellant's own
        // roster, or the same deadline would fall on different dates for different people.
        services.AddScoped<IHrWorkingDayCalculator, HrWorkingDayCalculator>();

        // FR-HR-181's grievance ladder. Separate from the disciplinary case on purpose: a grievance
        // is raised BY an employee and a case ABOUT one, which gives them opposite read rules.
        services.AddScoped<IStaffGrievanceService, StaffGrievanceService>();
        // Area 9c slice 5 — FR-HR-084's responder matrix. StaffGrievanceService depends on it
        // to name a rung's responder on file and on escalate.
        services.AddScoped<IEmployeeRelationsResponderService, EmployeeRelationsResponderService>();
        // Area 9c slice 6 — anonymous / whistleblower intake.
        services.AddScoped<IEmployeeRelationsConcernService, EmployeeRelationsConcernService>();
        // Area 9c slice 8 — ER analytics.
        services.AddScoped<IEmployeeRelationsAnalyticsService, EmployeeRelationsAnalyticsService>();

        // Area 25 slice 12 (D6) — the employee's own profile, and the approval path for the parts
        // of it that carry identity or payment consequences. Registered beside the grievance
        // service because it is the other surface an employee raises ABOUT their own record.
        services.AddScoped<IEmployeeProfileChangeService, EmployeeProfileChangeService>();

        // Area 25 slice 12b (D7) — letters an employee asks HR for. The catalog registration is
        // what makes the built-in templates resolvable, so a tenant that has never opened the
        // template editor can still issue a letter.
        services.AddScoped<IHrLetterRequestService, ErpSystem.Core.Services.HR.Letters.HrLetterRequestService>();

        // Area 25 slice 12c — staff announcements, and the audience resolver they share with
        // whatever broadcasts next. The resolver is the shared one on purpose: the only working
        // rule-to-employee expansion before this was private to AppraisalCycleService, and
        // OrientationAudienceRule has never had one at all.
        services.AddScoped<IHrAudienceResolver, HrAudienceResolver>();
        services.AddScoped<IHrAnnouncementService, HrAnnouncementService>();

        // Area 25 slice 12d — the policy library and its acknowledgements. Shares the resolver
        // above: a policy applies to whoever is in scope now, so the outstanding roster is
        // computed rather than pre-seeded.
        services.AddScoped<IHrPolicyService, HrPolicyService>();
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog,
            ErpSystem.Core.Services.HR.Letters.HrLettersEmailEventCatalog>();

        // Area 25 slice 13a — the staff directory and "my team". Third consumer of the audience
        // resolver, for its unit-subtree walk: browsing a unit means the unit AND everything
        // under it, and that walk already existed here rather than being copied a fourth time.
        services.AddScoped<IStaffDirectoryService, StaffDirectoryService>();

        // The reminder sweep spans both halves of the area — disciplinary clocks and unanswered
        // grievance rungs. Scoped so the daily host and the run-now endpoint share one code path.
        services.AddScoped<IDisciplineReminderService, DisciplineReminderService>();
        services.AddScoped<IProbationConfirmingAuthorityService, ProbationConfirmingAuthorityService>();
        services.AddScoped<IEmployeeOathOfSecrecyService, EmployeeOathOfSecrecyService>();

        // Finish plan lane 3c — the employee document file, its shared vocabulary and the position
        // requirements that make it answerable.
        services.AddScoped<IEmployeeDocumentService, EmployeeDocumentService>();
        // The exit register (area 9b) — one separation record per employee leaving, by any route.
        services.AddScoped<ISeparationService, SeparationService>();
        // HR Assets' read-only answer to "what has this leaver not given back?" — FR-HR-183, the
        // seam area 9b's decision D4 left open because area 16 did not exist yet. Registered beside
        // the separation service it feeds rather than with the asset services it reads, so that the
        // one place this crosses areas is visible from the side that consumes it.
        services.AddScoped<AssetCustodyClearanceBridge>();
        // The sixth reminder engine in the system, after SHE, movements, discipline, travel and
        // probation. Same shape: a run header, one dispatch row per reminder, a dedupe key.
        services.AddScoped<ISeparationReminderService, SeparationReminderService>();
        services.AddScoped<IProbationReminderService, ProbationReminderService>();
        services.AddScoped<IStaffTravelReminderService, StaffTravelReminderService>();
        // The twelfth HR reminder engine, and the last to be built — for the module with more dates
        // that matter than any of the others (closure plan R-6 / R-10 / L-23).
        services.AddScoped<ILeaveReminderService, LeaveReminderService>();
        // Asset reminder engine (area 16 slice 9, AST-1): maintenance due, overdue, and
        // never scheduled. Slice 11 adds insurance expiry and overdue returns to this one
        // rather than starting a seventh.
        services.AddScoped<IAssetReminderService, AssetReminderService>();
        // Travel's read-only window onto Finance's currency and exchange-rate masters —
        // replaces the retired StaffTravelCurrencyExchangeRate table (slice 6).
        services.AddScoped<HrCurrencyBridge>();
        // Area 12's alias for the same bridge — registered separately so its existing constructor
        // injections resolve unchanged. Retire with the alias.
        services.AddScoped<StaffTravelCurrencyBridge>();
        // HR → Finance posting (HR finish plan lane 8): the HR side of FIN-INT-001. One adapter for
        // every HR money event; the store is split out so its contract tests need no EF provider.
        services.AddScoped<ErpSystem.Core.Services.HR.Finance.IHrFinancePostingStore, ErpSystem.Core.Services.HR.Finance.HrFinancePostingStore>();
        services.AddScoped<IHrFinancePostingAdapter, ErpSystem.Core.Services.HR.Finance.HrFinancePostingAdapter>();
        services.AddScoped<IHrFinancePostingAdminService, ErpSystem.Core.Services.HR.Finance.HrFinancePostingAdminService>();
        services.AddScoped<IHrFinanceActualsService, ErpSystem.Core.Services.HR.Finance.HrFinanceActualsService>();
        // Resolves the travel policy's spend caps and refuses a booking above them (slice 8).
        services.AddScoped<StaffTravelPolicyGuard>();
        // Rolls a travel budget's committed/actual spend up from its bookings and claims (slice 9).
        services.AddScoped<StaffTravelBudgetRollup>();

        services.AddScoped<IStaffOffenseService, StaffOffenseService>();
        services.AddScoped<IStaffDisciplinaryActionTypeService, StaffDisciplinaryActionTypeService>();
        services.AddScoped<IStaffDisciplinaryCaseService, StaffDisciplinaryCaseService>();
        services.AddScoped<IStaffDisciplineInvestigationService, StaffDisciplineInvestigationService>();
        services.AddScoped<IStaffDisciplineHearingService, StaffDisciplineHearingService>();
        services.AddScoped<IStaffDisciplineWarningService, StaffDisciplineWarningService>();
        services.AddScoped<IStaffDisciplineSuspensionService, StaffDisciplineSuspensionService>();
        services.AddScoped<IStaffDisciplineFineService, StaffDisciplineFineService>();
        services.AddScoped<IStaffDisciplineAppealService, StaffDisciplineAppealService>();
        services.AddScoped<IStaffDisciplineCorrectiveActionService, StaffDisciplineCorrectiveActionService>();
        services.AddScoped<IStaffDisciplineTerminationService, StaffDisciplineTerminationService>();
        services.AddScoped<IStaffDisciplineActionStepService, StaffDisciplineActionStepService>();
        services.AddScoped<IStaffDisciplineWitnessService, StaffDisciplineWitnessService>();
        services.AddScoped<IStaffDisciplineDocumentService, StaffDisciplineDocumentService>();
        services.AddScoped<IStaffDisciplineNoteService, StaffDisciplineNoteService>();
        services.AddScoped<IStaffDisciplineNotificationService, StaffDisciplineNotificationService>();
        services.AddScoped<IStaffDisciplineLegalReviewService, StaffDisciplineLegalReviewService>();
        services.AddScoped<IStaffRequisitionService, StaffRequisitionService>();
        services.AddScoped<IPositionVacancyService, PositionVacancyService>();
        // The recruitment module's only date-driven job (G-2.4, G-6.2). Scoped so the run-now
        // endpoint and RecruitmentLifecycleSweepBackgroundService share one code path; the hosted
        // service that actually schedules it is registered in ServiceCollectionExtensions.
        services.AddScoped<IRecruitmentLifecycleSweepService,
            ErpSystem.Core.Services.HR.Recruitment.RecruitmentLifecycleSweepService>();
        services.AddScoped<IExternalAssociateRepository, ExternalAssociateRepository>();
        services.AddScoped<IExternalAssociateService, ExternalAssociateService>();
        services.AddScoped<ITrainingVendorRepository, TrainingVendorRepository>();
        services.AddScoped<ITrainerProfileRepository, TrainerProfileRepository>();
        services.AddScoped<ITrainerSkillRepository, TrainerSkillRepository>();
        services.AddScoped<ITrainerAvailabilityRepository, TrainerAvailabilityRepository>();
        services.AddScoped<ITrainingProgramRepository, TrainingProgramRepository>();
        services.AddScoped<ITrainingMaterialRepository, TrainingMaterialRepository>();
        services.AddScoped<ITrainingProgramCompetencyRepository, TrainingProgramCompetencyRepository>();
        services.AddScoped<ITrainingProgramSkillRepository, TrainingProgramSkillRepository>();
        services.AddScoped<ITrainingScheduleRepository, TrainingScheduleRepository>();
        services.AddScoped<ITrainingSessionRepository, TrainingSessionRepository>();
        services.AddScoped<ITrainingNominationRepository, TrainingNominationRepository>();
        services.AddScoped<ITrainingCompletionRepository, TrainingCompletionRepository>();
        services.AddScoped<ITrainingAttendanceRepository, TrainingAttendanceRepository>();
        services.AddScoped<ITrainingFeedbackRepository, TrainingFeedbackRepository>();
        services.AddScoped<ITrainingFollowUpAssessmentRepository, TrainingFollowUpAssessmentRepository>();
        services.AddScoped<ITrainingCertificateRepository, TrainingCertificateRepository>();
        services.AddScoped<IEmployeeCertificateRepository, EmployeeCertificateRepository>();
        services.AddScoped<IComplianceTrainingRequirementRepository, ComplianceTrainingRequirementRepository>();
        services.AddScoped<IEmployeeComplianceRecordRepository, EmployeeComplianceRecordRepository>();
        services.AddScoped<ITrainingBudgetRepository, TrainingBudgetRepository>();
        services.AddScoped<ITrainingBudgetTransactionRepository, TrainingBudgetTransactionRepository>();
        services.AddScoped<ITrainingPlanRepository, TrainingPlanRepository>();
        services.AddScoped<ITrainingPlanItemRepository, TrainingPlanItemRepository>();
        services.AddScoped<ITrainingPlanBudgetLineRepository, TrainingPlanBudgetLineRepository>();
        services.AddScoped<ITrainingNeedsAssessmentRepository, TrainingNeedsAssessmentRepository>();
        services.AddScoped<ITrainingNeedsAssessmentProgramRepository, TrainingNeedsAssessmentProgramRepository>();
        services.AddScoped<ITrainingNeedsAssessmentSkillRepository, TrainingNeedsAssessmentSkillRepository>();
        services.AddScoped<ITrainingWaitlistRepository, TrainingWaitlistRepository>();
        services.AddScoped<ITrainingRequestRepository, TrainingRequestRepository>();
        services.AddScoped<ILearningPathRepository, LearningPathRepository>();
        services.AddScoped<ILearningPathProgramRepository, LearningPathProgramRepository>();
        services.AddScoped<ILearningPathSkillRepository, LearningPathSkillRepository>();
        services.AddScoped<IEmployeeLearningPathRepository, EmployeeLearningPathRepository>();
        services.AddScoped<IEmployeeLearningPathStepRepository, EmployeeLearningPathStepRepository>();
        services.AddScoped<IMentoringProgramRepository, MentoringProgramRepository>();
        services.AddScoped<IMentoringPairRepository, MentoringPairRepository>();
        services.AddScoped<IMentoringSessionRepository, MentoringSessionRepository>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<ITrainingCategoryOptionService, TrainingCategoryOptionService>();
        services.AddScoped<ITrainingProgramGroupService, TrainingProgramGroupService>();
        services.AddScoped<ITrainingServiceBondService, TrainingServiceBondService>();
        services.AddScoped<ITrainingStatusHistoryService, TrainingStatusHistoryService>();
        services.AddScoped<ICertificateVerificationService, CertificateVerificationService>();
        services.AddScoped<ITrainingVendorService, TrainingVendorService>();
        services.AddScoped<ITrainerService, TrainerService>();
        services.AddScoped<ITrainingProgramService, TrainingProgramService>();
        services.AddScoped<ITrainingScheduleService, TrainingScheduleService>();
        services.AddScoped<ITrainingNominationService, TrainingNominationService>();
        services.AddScoped<ITrainingCompletionService, TrainingCompletionService>();
        services.AddScoped<IEmployeeCertificateService, EmployeeCertificateService>();
        services.AddScoped<IComplianceTrainingService, ComplianceTrainingService>();
        services.AddScoped<ITrainingBudgetService, TrainingBudgetService>();
        services.AddScoped<ITrainingPlanService, TrainingPlanService>();
        services.AddScoped<ITrainingNeedsAssessmentService, TrainingNeedsAssessmentService>();
        services.AddScoped<ITrainingWaitlistService, TrainingWaitlistService>();
        services.AddScoped<ITrainingRequestService, TrainingRequestService>();
        services.AddScoped<ILearningPathService, LearningPathService>();
        services.AddScoped<IMentoringService, MentoringService>();
        services.AddScoped<ITrainingDashboardService, TrainingDashboardService>();
        services.AddScoped<IStaffAttendanceRecordService, StaffAttendanceRecordService>();
        services.AddScoped<IStaffDailyAttendanceService, StaffDailyAttendanceService>();
        services.AddScoped<IStaffAttendanceLogService, StaffAttendanceLogService>();
        services.AddScoped<IStaffAttendanceRegularizationService, StaffAttendanceRegularizationService>();
        services.AddScoped<IStaffMonthlyAttendanceSummaryService, StaffMonthlyAttendanceSummaryService>();
        services.AddScoped<IStaffBulkAttendanceImportService, StaffBulkAttendanceImportService>();
        services.AddScoped<IWorkScheduleService, WorkScheduleService>();
        services.AddScoped<IEmployeeWorkScheduleService, EmployeeWorkScheduleService>();
        services.AddScoped<IShiftDefinitionService, ShiftDefinitionService>();
        services.AddScoped<IShiftAssignmentService, ShiftAssignmentService>();
        services.AddScoped<IShiftRotationPlanService, ShiftRotationPlanService>();
        services.AddScoped<IPositionOvertimePolicyService, PositionOvertimePolicyService>();
        services.AddScoped<IEmployeeOvertimeOverrideService, EmployeeOvertimeOverrideService>();
        services.AddScoped<IStaffOvertimeRequestService, StaffOvertimeRequestService>();
        services.AddScoped<IEmployeeBiometricService, EmployeeBiometricService>();
        services.AddScoped<IStaffAttendanceDeviceService, StaffAttendanceDeviceService>();
        services.AddScoped<IGeofenceVerificationService, GeofenceVerificationService>();
        services.AddScoped<IGeofenceZoneService, GeofenceZoneService>();
        services.AddScoped<IRemoteWorkRequestService, RemoteWorkRequestService>();
        services.AddScoped<IHolidayCalendarService, HolidayCalendarService>();
        services.AddScoped<IPayPeriodService, PayPeriodService>();
        services.AddScoped<IStaffAttendancePayrollExportService, StaffAttendancePayrollExportService>();
        services.AddScoped<IStaffAttendanceAlertRuleService, StaffAttendanceAlertRuleService>();
        services.AddScoped<IStaffAttendanceAlertService, StaffAttendanceAlertService>();
        services.AddScoped<IAttendanceDashboardService, AttendanceDashboardService>();
        services.AddScoped<IConsultantClientService, ConsultantClientService>();
        services.AddScoped<IClientEngagementService, ClientEngagementService>();
        services.AddScoped<IConsultantTimesheetService, ConsultantTimesheetService>();
        services.AddScoped<ITimesheetInvoiceService, TimesheetInvoiceService>();
        // The candidate portal's own auth (PortalBearer scheme, CandidatePortalAuthService,
        // CandidateJwtService) was retired 2026-08-30 — candidates move onto the main JWT scheme
        // with the Candidate role. ICandidatePortalService survives: its application/profile/
        // document logic is reused by the main-scheme candidate surface.
        services.AddScoped<ICandidatePortalService, CandidatePortalService>();
        // The consultant-client portal's own auth (the PortalBearer scheme's LAST tenant —
        // ConsultantClientPortalAuthService, ConsultantClientPortalJwtService) was retired
        // 2026-08-31: contacts are invited by HR onto main-scheme Identity accounts with the
        // ConsultantClient role. IConsultantClientPortalService survives on new signatures for
        // the rebuilt api/client-portal surface; the contact service owns the invite lifecycle.
        services.AddScoped<IConsultantClientContactService, ConsultantClientContactService>();
        services.AddScoped<IConsultantClientPortalService, ConsultantClientPortalService>();

        // HR support services (implementations outside Core/Data or outside Interfaces/HR)
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IDateTimeProvider, ErpSystem.Api.Services.SystemDateTimeProvider>();
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog, ErpSystem.Core.Services.HR.Recruitment.RecruitmentEmailEventCatalog>();
        // The probation module ships one document (FR-HR-032's confirmation letter). Registering the
        // catalog is what gives TemplatedEmailService a built-in default, so the letter renders on a
        // tenant that has never opened the template editor.
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog, ErpSystem.Core.Services.HR.Probation.ProbationEmailEventCatalog>();
        // Staff assets ship one document too (AST-5's responsibility-and-terms form), and register
        // for the same reason: the built-in default is what makes it render before anyone has
        // opened the template editor.
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog, ErpSystem.Core.Services.HR.Assets.AssetsEmailEventCatalog>();
        // Orientation & onboarding (round 4, lane K): the reminder digest. Registered so the built-in
        // default renders before anyone has opened the template editor — the sweep emails from day one.
        services.AddSingleton<ErpSystem.Core.Interfaces.Common.IEmailEventCatalog, ErpSystem.Core.Services.HR.Orientation.OnboardingOrientationEmailEventCatalog>();
        services.AddScoped<ErpSystem.Core.Interfaces.Common.ITemplatedEmailService, ErpSystem.Core.Services.Common.TemplatedEmailService>();
        services.AddScoped<ErpSystem.Core.Interfaces.INumberSequenceService, ErpSystem.Data.Services.NumberSequenceService>();
        services.AddSingleton<ErpSystem.Core.Services.Common.IEmailTemplateRenderer, ErpSystem.Core.Services.Common.EmailTemplateRenderer>();

        return services;
    }
}
