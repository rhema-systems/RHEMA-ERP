// Types for HR Training & Learning — Slice 1 (Setup/Catalog Foundation): categories, program
// groups, trainers, vendors, programs. Mirrors ErpSystem.Core.DTOs.HR.TrainingDTOs.

const opts = <T extends string>(entries: [T, string][]) =>
  entries.map(([value, label]) => ({ value, label }));

// ── Enums (serialize as strings; option lists use the backend [Description] text) ──────────

export type TrainingVendorType =
  | 'IndividualConsultant'
  | 'TrainingFirm'
  | 'AccreditedInstitution'
  | 'University'
  | 'GovernmentAgency'
  | 'NGO'
  | 'Other';

export const TRAINING_VENDOR_TYPE_OPTIONS = opts<TrainingVendorType>([
  ['IndividualConsultant', 'Individual Consultant'],
  ['TrainingFirm', 'Training Firm'],
  ['AccreditedInstitution', 'Accredited Institution'],
  ['University', 'University / Tertiary'],
  ['GovernmentAgency', 'Government Agency'],
  ['NGO', 'NGO / Non-Profit'],
  ['Other', 'Other'],
]);

export type VendorAccreditationStatus = 'Active' | 'Pending' | 'Expired' | 'Suspended' | 'Revoked';

export const VENDOR_ACCREDITATION_STATUS_OPTIONS = opts<VendorAccreditationStatus>([
  ['Active', 'Active'],
  ['Pending', 'Pending'],
  ['Expired', 'Expired'],
  ['Suspended', 'Suspended'],
  ['Revoked', 'Revoked'],
]);

export type TrainingType =
  | 'Workshop'
  | 'Seminar'
  | 'Conference'
  | 'OnTheJob'
  | 'Mentoring'
  | 'Certification';

export const TRAINING_TYPE_OPTIONS = opts<TrainingType>([
  ['Workshop', 'Workshop'],
  ['Seminar', 'Seminar'],
  ['Conference', 'Conference'],
  ['OnTheJob', 'On-the-Job Training'],
  ['Mentoring', 'Mentoring/Coaching'],
  ['Certification', 'Certification Program'],
]);

export type TrainingSource = 'Internal' | 'External' | 'OnlineELearning';

export const TRAINING_SOURCE_OPTIONS = opts<TrainingSource>([
  ['Internal', 'Internal'],
  ['External', 'External'],
  ['OnlineELearning', 'Online / E-Learning'],
]);

export type TrainingLevel = 'Beginner' | 'Intermediate' | 'Advanced' | 'Expert' | 'AnyLevel';

export const TRAINING_LEVEL_OPTIONS = opts<TrainingLevel>([
  ['Beginner', 'Beginner'],
  ['Intermediate', 'Intermediate'],
  ['Advanced', 'Advanced'],
  ['Expert', 'Expert'],
  ['AnyLevel', 'Any Level'],
]);

export type MaterialType =
  | 'Handbook'
  | 'Slides'
  | 'Video'
  | 'Document'
  | 'Exercise'
  | 'Assessment'
  | 'Reference';

export const MATERIAL_TYPE_OPTIONS = opts<MaterialType>([
  ['Handbook', 'Handbook'],
  ['Slides', 'Presentation Slides'],
  ['Video', 'Video'],
  ['Document', 'Document'],
  ['Exercise', 'Exercise/Activity'],
  ['Assessment', 'Assessment'],
  ['Reference', 'Reference Material'],
]);

export type ProficiencyLevel = 'Basic' | 'WorkingKnowledge' | 'Proficient' | 'Advanced' | 'Expert';

export const PROFICIENCY_LEVEL_OPTIONS = opts<ProficiencyLevel>([
  ['Basic', 'Basic/Awareness'],
  ['WorkingKnowledge', 'Working Knowledge'],
  ['Proficient', 'Proficient'],
  ['Advanced', 'Advanced'],
  ['Expert', 'Expert'],
]);

export type TrainerEngagementType = 'Internal' | 'External' | 'Other';

export const TRAINER_ENGAGEMENT_TYPE_OPTIONS = opts<TrainerEngagementType>([
  ['Internal', 'Internal Engagement'],
  ['External', 'External Engagement'],
  ['Other', 'Other'],
]);

// ── Training Category Option (mirrors TrainingCategoryOptionDto) ───────────────────────────

export interface TrainingCategoryOption {
  id: string;
  code: string;
  name: string;
  colorHex?: string | null;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
  programsCount: number;
}

// Mirrors CreateTrainingCategoryOptionDto; UpdateTrainingCategoryOptionDto is the same minus code.
export interface TrainingCategoryOptionRequest {
  code: string;
  name: string;
  colorHex?: string | null;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
}

// ── Training Program Group (mirrors TrainingProgramGroupDto) ───────────────────────────────

export interface TrainingProgramGroup {
  id: string;
  code: string;
  name: string;
  colorHex?: string | null;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
  programsCount: number;
}

export interface TrainingProgramGroupRequest {
  code: string;
  name: string;
  colorHex?: string | null;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
}

// ── Training Vendor (mirrors TrainingVendorDto / TrainingVendorSummaryDto) ─────────────────

export interface TrainingVendor {
  id: string;
  vendorCode: string;
  name: string;
  vendorType: TrainingVendorType;
  isActive: boolean;
  isPreferred: boolean;
  preferredSince?: string | null;
  isBlacklisted: boolean;
  blacklistedDate?: string | null;
  blacklistReason?: string | null;
  accreditationBody?: string | null;
  accreditationNumber?: string | null;
  accreditationStatus?: VendorAccreditationStatus | null;
  accreditationExpiryDate?: string | null;
  isAccreditationExpired: boolean;
  primaryContactName?: string | null;
  primaryContactEmail?: string | null;
  primaryContactPhone?: string | null;
  website?: string | null;
  address?: string | null;
  currency: string;
  defaultDailyRate?: number | null;
  contractReference?: string | null;
  contractDocumentPath?: string | null;
  contractStartDate?: string | null;
  contractEndDate?: string | null;
  isContractActive: boolean;
  notes?: string | null;
  totalTrainersCount: number;
}

export interface TrainingVendorSummary {
  id: string;
  vendorCode: string;
  name: string;
  vendorType: TrainingVendorType;
  isActive: boolean;
  isPreferred: boolean;
  isBlacklisted: boolean;
  accreditationStatus?: VendorAccreditationStatus | null;
  primaryContactName?: string | null;
  primaryContactEmail?: string | null;
  totalTrainersCount: number;
}

// Shared field set behind CreateTrainingVendorDto / UpdateTrainingVendorDto — Create additionally
// carries vendorCode (immutable after creation, so the update form never edits it).
export interface TrainingVendorFields {
  name: string;
  vendorType: TrainingVendorType;
  isActive: boolean;
  isPreferred: boolean;
  preferredSince?: string | null;
  accreditationBody?: string | null;
  accreditationNumber?: string | null;
  accreditationStatus?: VendorAccreditationStatus | null;
  accreditationExpiryDate?: string | null;
  primaryContactName?: string | null;
  primaryContactEmail?: string | null;
  primaryContactPhone?: string | null;
  website?: string | null;
  address?: string | null;
  currency: string;
  defaultDailyRate?: number | null;
  contractReference?: string | null;
  contractStartDate?: string | null;
  contractEndDate?: string | null;
  notes?: string | null;
}

export interface TrainingVendorCreateRequest extends TrainingVendorFields {
  vendorCode: string;
}

export type TrainingVendorUpdateRequest = TrainingVendorFields;

// ── Trainer Profile (mirrors TrainerProfileDto / TrainerProfileSummaryDto) ─────────────────

export interface TrainerProfile {
  id: string;
  name: string;
  employeeId?: string | null;
  employeeName?: string | null;
  employeeNumber?: string | null;
  vendorId?: string | null;
  vendorName?: string | null;
  bio?: string | null;
  contact?: string | null;
  expertiseAreas?: string | null;
  totalTrainingHoursDelivered: number;
  totalSessionsDelivered: number;
  averageRating?: number | null;
  totalRatingsCount: number;
  isActive: boolean;
  skills: TrainerSkill[];
  availability: TrainerAvailability[];
}

export interface TrainerProfileSummary {
  id: string;
  name: string;
  employeeId?: string | null;
  employeeName?: string | null;
  vendorId?: string | null;
  vendorName?: string | null;
  expertiseAreas?: string | null;
  averageRating?: number | null;
  totalSessionsDelivered: number;
  isActive: boolean;
}

// Mirrors Create/UpdateTrainerProfileDto — identical field sets, reused for both like SkillRequest.
export interface TrainerProfileRequest {
  name: string;
  employeeId?: string | null;
  vendorId?: string | null;
  bio?: string | null;
  contact?: string | null;
  expertiseAreas?: string | null;
  isActive: boolean;
}

// ── Trainer Skill (mirrors TrainerSkillDto) ─────────────────────────────────────────────────

export interface TrainerSkill {
  id: string;
  trainerProfileId: string;
  trainerName: string;
  skillId: string;
  skillName: string;
  skillCategory?: string | null;
  trainerProficiency: ProficiencyLevel;
  isCertifiedToTrain: boolean;
  certificateNumber?: string | null;
  certificateName?: string | null;
  certificateFilePath?: string | null;
}

export interface TrainerSkillFields {
  trainerProficiency: ProficiencyLevel;
  isCertifiedToTrain: boolean;
  certificateNumber?: string | null;
  certificateName?: string | null;
}

export interface TrainerSkillCreateRequest extends TrainerSkillFields {
  trainerProfileId: string;
  skillId: string;
}

export type TrainerSkillUpdateRequest = TrainerSkillFields;

// ── Trainer Availability (mirrors TrainerAvailabilityDto) ──────────────────────────────────

export interface TrainerAvailability {
  id: string;
  trainerProfileId: string;
  trainerName: string;
  fromDate: string;
  toDate: string;
  isAvailable: boolean;
  engagementType?: TrainerEngagementType | null;
  notes?: string | null;
}

export interface TrainerAvailabilityFields {
  fromDate: string;
  toDate: string;
  isAvailable: boolean;
  engagementType?: TrainerEngagementType | null;
  notes?: string | null;
}

export interface TrainerAvailabilityCreateRequest extends TrainerAvailabilityFields {
  trainerProfileId: string;
}

export type TrainerAvailabilityUpdateRequest = TrainerAvailabilityFields;

// ── Training Program (mirrors TrainingProgramDto / TrainingProgramSummaryDto) ──────────────

export interface TrainingProgram {
  id: string;
  programCode: string;
  programName: string;
  description: string;
  categoryOptionId?: string | null;
  categoryName?: string | null;
  categoryColor?: string | null;
  programGroupId?: string | null;
  groupName?: string | null;
  groupColor?: string | null;
  type: TrainingType;
  source: TrainingSource;
  level: TrainingLevel;
  durationDays: number;
  durationHours: number;
  prerequisites?: string | null;
  learningObjectives?: string | null;
  costPerParticipant: number;
  currency: string;
  includesAccommodation: boolean;
  includesMeals: boolean;
  includesTransport: boolean;
  providesCertificate: boolean;
  certificateName?: string | null;
  certificateValidityMonths?: number | null;
  minParticipants?: number | null;
  maxParticipants?: number | null;
  isActive: boolean;
  requiresApproval: boolean;
  requiresServiceBond: boolean;
  serviceBondMonths?: number | null;
  serviceBondTerms?: string | null;
  competencies: TrainingProgramCompetency[];
  skills: TrainingProgramSkill[];
  materials: TrainingMaterial[];
}

export interface TrainingProgramSummary {
  id: string;
  programCode: string;
  programName: string;
  categoryOptionId?: string | null;
  categoryName?: string | null;
  categoryColor?: string | null;
  programGroupId?: string | null;
  groupName?: string | null;
  groupColor?: string | null;
  type: TrainingType;
  source: TrainingSource;
  level: TrainingLevel;
  durationDays: number;
  durationHours: number;
  costPerParticipant: number;
  currency: string;
  providesCertificate: boolean;
  isActive: boolean;
  requiresApproval: boolean;
}

export interface TrainingProgramFields {
  programName: string;
  description: string;
  categoryOptionId?: string | null;
  programGroupId?: string | null;
  type: TrainingType;
  source: TrainingSource;
  level: TrainingLevel;
  durationDays: number;
  durationHours: number;
  prerequisites?: string | null;
  learningObjectives?: string | null;
  costPerParticipant: number;
  currency: string;
  includesAccommodation: boolean;
  includesMeals: boolean;
  includesTransport: boolean;
  providesCertificate: boolean;
  certificateName?: string | null;
  certificateValidityMonths?: number | null;
  minParticipants?: number | null;
  maxParticipants?: number | null;
  isActive: boolean;
  requiresApproval: boolean;
  requiresServiceBond: boolean;
  serviceBondMonths?: number | null;
  serviceBondTerms?: string | null;
}

export interface TrainingProgramCreateRequest extends TrainingProgramFields {
  programCode: string;
}

export type TrainingProgramUpdateRequest = TrainingProgramFields;

// ── Training Material (mirrors TrainingMaterialDto) ─────────────────────────────────────────

export interface TrainingMaterial {
  id: string;
  programId: string;
  programName: string;
  materialName: string;
  type: MaterialType;
  filePath?: string | null;
  externalUrl?: string | null;
  isPublic: boolean;
  isActive: boolean;
  uploadDate: string;
}

export interface TrainingMaterialFields {
  materialName: string;
  type: MaterialType;
  filePath?: string | null;
  externalUrl?: string | null;
  isPublic: boolean;
  isActive: boolean;
}

export interface TrainingMaterialCreateRequest extends TrainingMaterialFields {
  programId: string;
  uploadDate?: string;
}

export type TrainingMaterialUpdateRequest = TrainingMaterialFields;

// ── Training Program Competency (mirrors TrainingProgramCompetencyDto; add/remove only) ────

export interface TrainingProgramCompetency {
  id: string;
  programId: string;
  programName: string;
  competencyId: string;
  competencyCode: string;
  competencyName: string;
  targetLevel: number;
}

export interface TrainingProgramCompetencyCreateRequest {
  programId: string;
  competencyId: string;
  targetLevel: number;
}

// ── Training Program Skill (mirrors TrainingProgramSkillDto; add/remove only) ──────────────

export interface TrainingProgramSkill {
  id: string;
  programId: string;
  programName: string;
  skillId: string;
  skillName: string;
  targetProficiency: ProficiencyLevel;
}

export interface TrainingProgramSkillCreateRequest {
  programId: string;
  skillId: string;
  targetProficiency: ProficiencyLevel;
}

// ── Planning — Slice 2: Needs Assessment, Training Plans, Training Budgets ─────────────────
// Mirrors ErpSystem.Core.DTOs.HR.TrainingDTOs (Training Budget / Plan / Needs Assessment regions).

export type AssessmentSource =
  | 'PerformanceReview'
  | 'SelfAssessment'
  | 'ManagerRequest'
  | 'SkillsGapAnalysis'
  | 'JobRoleChange';

export const ASSESSMENT_SOURCE_OPTIONS = opts<AssessmentSource>([
  ['PerformanceReview', 'Performance Review'],
  ['SelfAssessment', 'Self Assessment'],
  ['ManagerRequest', 'Manager Request'],
  ['SkillsGapAnalysis', 'Skills Gap Analysis'],
  ['JobRoleChange', 'Job Role Change'],
]);

export type TrainingPriority = 'Critical' | 'High' | 'Medium' | 'Low';

export const TRAINING_PRIORITY_OPTIONS = opts<TrainingPriority>([
  ['Critical', 'Critical'],
  ['High', 'High'],
  ['Medium', 'Medium'],
  ['Low', 'Low'],
]);

export type TrainingPlanStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'InProgress'
  | 'Completed'
  | 'Revised';

export const TRAINING_PLAN_STATUS_OPTIONS = opts<TrainingPlanStatus>([
  ['Draft', 'Draft'],
  ['PendingApproval', 'Pending Approval'],
  ['Approved', 'Approved'],
  ['InProgress', 'In Progress'],
  ['Completed', 'Completed'],
  ['Revised', 'Revised'],
]);

export type TrainingBudgetStatus = 'Draft' | 'Approved' | 'Denied' | 'Active' | 'Closed';

export const TRAINING_BUDGET_STATUS_OPTIONS = opts<TrainingBudgetStatus>([
  ['Draft', 'Draft'],
  ['Approved', 'Approved'],
  ['Denied', 'Denied'],
  ['Active', 'Active'],
  ['Closed', 'Closed'],
]);

// ── Training Needs Assessment (mirrors TrainingNeedsAssessmentDto / SummaryDto) ────────────

export interface TrainingNeedsAssessment {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  employeeDepartment?: string | null;
  employeePosition?: string | null;
  year: number;
  source: AssessmentSource;
  identifiedGaps: string;
  priority: TrainingPriority;
  identifiedById?: string | null;
  identifiedByName?: string | null;
  identifiedDate: string;
  additionalNotes?: string | null;
  trainingProvided: boolean;
  trainingProvidedDate?: string | null;
  recommendedPrograms: TrainingNeedsAssessmentProgram[];
  skillGaps: TrainingNeedsAssessmentSkill[];
}

export interface TrainingNeedsAssessmentSummary {
  id: string;
  employeeName: string;
  employeeNumber: string;
  year: number;
  source: AssessmentSource;
  priority: TrainingPriority;
  trainingProvided: boolean;
  recommendedProgramsCount: number;
  skillGapsCount: number;
}

export interface CreateTrainingNeedsAssessmentRequest {
  employeeId: string;
  year: number;
  source: AssessmentSource;
  identifiedGaps: string;
  priority: TrainingPriority;
  identifiedById: string;
  identifiedDate?: string;
  additionalNotes?: string | null;
}

export interface UpdateTrainingNeedsAssessmentRequest {
  source: AssessmentSource;
  identifiedGaps: string;
  priority: TrainingPriority;
  additionalNotes?: string | null;
  trainingProvided: boolean;
  trainingProvidedDate?: string | null;
}

export interface BulkCreateTrainingNeedsAssessmentRequest {
  employeeIds: string[];
  year: number;
  source: AssessmentSource;
  identifiedGaps: string;
  priority: TrainingPriority;
  additionalNotes?: string | null;
}

export interface BulkNeedsAssessmentResult {
  requestedCount: number;
  createdCount: number;
}

// ── Recommended Program / Skill Gap (add/remove only — no update endpoint) ─────────────────

export interface TrainingNeedsAssessmentProgram {
  id: string;
  assessmentId: string;
  programId: string;
  programCode: string;
  programName: string;
  priority: TrainingPriority;
  rationale?: string | null;
}

export interface TrainingNeedsAssessmentProgramCreateRequest {
  assessmentId: string;
  programId: string;
  priority: TrainingPriority;
  rationale?: string | null;
}

export interface TrainingNeedsAssessmentSkill {
  id: string;
  assessmentId: string;
  skillId: string;
  skillName: string;
  currentProficiency: ProficiencyLevel;
  requiredProficiency: ProficiencyLevel;
  gapPriority: TrainingPriority;
}

export interface TrainingNeedsAssessmentSkillCreateRequest {
  assessmentId: string;
  skillId: string;
  currentProficiency: ProficiencyLevel;
  requiredProficiency: ProficiencyLevel;
  gapPriority: TrainingPriority;
}

// ── Training Plan (mirrors TrainingPlanDto / SummaryDto) ───────────────────────────────────

export interface TrainingPlan {
  id: string;
  planNumber: string;
  year: number;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  status: TrainingPlanStatus;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  notes?: string | null;
  totalItemsCount: number;
  completedItemsCount: number;
  completionRate: number;
  items: TrainingPlanItem[];
  budgetLines: TrainingPlanBudgetLine[];
}

export interface TrainingPlanSummary {
  id: string;
  planNumber: string;
  year: number;
  organizationUnitName?: string | null;
  status: TrainingPlanStatus;
  totalItemsCount: number;
  completedItemsCount: number;
  approvalDate?: string | null;
}

export interface TrainingPlanRequest {
  year: number;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  notes?: string | null;
}

// ── Training Plan Item (mirrors TrainingPlanItemDto) ───────────────────────────────────────

export interface TrainingPlanItem {
  id: string;
  planId: string;
  planNumber: string;
  programId?: string | null;
  programCode?: string | null;
  trainingTitle: string;
  description?: string | null;
  quarter: number;
  plannedStartDate?: string | null;
  plannedEndDate?: string | null;
  estimatedParticipants: number;
  estimatedCost: number;
  isCompleted: boolean;
  completionDate?: string | null;
  actualParticipants?: number | null;
  actualCost?: number | null;
  fulfilledByScheduleId?: string | null;
  fulfilledByScheduleNumber?: string | null;
}

export interface TrainingPlanItemFields {
  programId?: string | null;
  trainingTitle: string;
  description?: string | null;
  quarter: number;
  plannedStartDate?: string | null;
  plannedEndDate?: string | null;
  estimatedParticipants: number;
  estimatedCost: number;
}

export interface TrainingPlanItemCreateRequest extends TrainingPlanItemFields {
  planId: string;
}

export interface TrainingPlanItemUpdateRequest extends TrainingPlanItemFields {
  isCompleted: boolean;
  completionDate?: string | null;
  actualParticipants?: number | null;
  actualCost?: number | null;
  fulfilledByScheduleId?: string | null;
}

// ── Training Plan Budget Line (mirrors TrainingPlanBudgetLineDto) ──────────────────────────

export interface TrainingPlanBudgetLine {
  id: string;
  planId: string;
  planNumber: string;
  category: string;
  budgetedAmount: number;
  actualAmount: number;
  committedAmount: number;
  variance: number;
  notes?: string | null;
}

export interface TrainingPlanBudgetLineCreateRequest {
  planId: string;
  category: string;
  budgetedAmount: number;
  notes?: string | null;
}

export interface TrainingPlanBudgetLineUpdateRequest {
  category: string;
  budgetedAmount: number;
  actualAmount: number;
  committedAmount: number;
  notes?: string | null;
}

// ── Training Budget (mirrors TrainingBudgetDto / SummaryDto) ───────────────────────────────

export interface TrainingBudget {
  id: string;
  budgetCode: string;
  year: number;
  quarter?: number | null;
  periodDescription: string;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  currency: string;
  allocatedAmount: number;
  committedAmount: number;
  spentAmount: number;
  remainingAmount: number;
  utilizationRate: number;
  status: TrainingBudgetStatus;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  glAccountCode?: string | null;
  costCenterCode?: string | null;
  notes?: string | null;
}

export interface TrainingBudgetSummary {
  id: string;
  budgetCode: string;
  year: number;
  quarter?: number | null;
  periodDescription: string;
  organizationUnitName?: string | null;
  currency: string;
  allocatedAmount: number;
  spentAmount: number;
  remainingAmount: number;
  status: TrainingBudgetStatus;
}

export interface TrainingBudgetFields {
  year: number;
  quarter?: number | null;
  organizationLevelId?: string | null;
  organizationUnitId?: string | null;
  currency: string;
  allocatedAmount: number;
  glAccountCode?: string | null;
  costCenterCode?: string | null;
  notes?: string | null;
}

export interface TrainingBudgetCreateRequest extends TrainingBudgetFields {
  budgetCode: string;
}

export type TrainingBudgetUpdateRequest = TrainingBudgetFields;

// ── Training Budget Transaction (mirrors TrainingBudgetTransactionDto) ─────────────────────

export interface TrainingBudgetTransaction {
  id: string;
  budgetId: string;
  budgetCode: string;
  scheduleId?: string | null;
  scheduleNumber?: string | null;
  description: string;
  amount: number;
  amountType: 'Debit' | 'Credit';
  transactionDate: string;
  recordedById?: string | null;
  recordedByName?: string | null;
  reference?: string | null;
  glAccountCode?: string | null;
  voucherNumber?: string | null;
  notes?: string | null;
}

export interface TrainingBudgetTransactionCreateRequest {
  budgetId: string;
  scheduleId?: string | null;
  description: string;
  amount: number;
  transactionDate: string;
  recordedById: string;
  reference?: string | null;
  glAccountCode?: string | null;
  voucherNumber?: string | null;
  notes?: string | null;
}
