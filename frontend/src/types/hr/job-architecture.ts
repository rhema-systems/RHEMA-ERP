/**
 * Job architecture, competency and manpower budget — FRD §A1.1 (FR-HR-134, FR-HR-135, FR-HR-136).
 *
 * ⚠ Every type here was written by reading the C# DTO, not the endpoint name. This area produced
 * four fresh examples of why, in four consecutive slices: `employee-competencies/.../gaps` returns
 * an OBJECT with a list inside it and not a list; the detail projection calls its collection
 * `workingConditions` while the entity navigation is `JobWorkingConditions`;
 * `Workflow/instances/entity/{type}/{id}` does not exist at all (it is `entity-summary`, with query
 * parameters); and two update DTOs inherit `Id` from `UpdateDtoBase`, so their own declarations
 * show no id while the controller demands one. See `hr-travel-area-survey`.
 */

// ── enums, as the API serialises them (names, not numbers) ───────────────────

export type JobDescriptionStatus =
  | 'Draft'
  | 'PendingReview'
  | 'UnderRevision'
  | 'Approved'
  | 'Active'
  | 'Superseded'
  | 'Archived';

export type ResponsibilityType = 'Core' | 'Secondary' | 'Occasional';

export type QualificationType =
  | 'Education'
  | 'Experience'
  | 'Certification'
  | 'License'
  | 'Membership'
  | 'Other';

export type CompetencyType = 'Technical' | 'Behavioral' | 'Leadership' | 'Core' | 'Functional';

export type ProficiencyLevel = 'Basic' | 'WorkingKnowledge' | 'Proficient' | 'Advanced' | 'Expert';

export type PhysicalDemandFrequency =
  | 'Never'
  | 'Rarely'
  | 'Occasionally'
  | 'Frequently'
  | 'Constantly';

export type WorkEnvironmentType =
  | 'Office'
  | 'Outdoor'
  | 'Industrial'
  | 'Laboratory'
  | 'Warehouse'
  | 'Site'
  | 'Remote';

export type ExposureLevel = 'None' | 'Rare' | 'Occasional' | 'Frequent' | 'Constant';

export type ReportingRelationshipType =
  | 'CollaboratesWith'
  | 'InternalCustomers'
  | 'ExternalCustomers'
  | 'Vendors'
  | 'RegulatoryBodies'
  | 'MatrixReport';

export type RoleCriticalityLevel = 'Low' | 'Medium' | 'High' | 'MissionCritical';

/** Autonomy the role carries. ⚠ Three values only — not the five a "level" suggests. */
export type DecisionAuthorityLevel = 'Operational' | 'Tactical' | 'Strategic';

export type CompetencyCategory =
  | 'Leadership'
  | 'Technical'
  | 'Behavioral'
  | 'Functional'
  | 'Core'
  | 'Other';

export type SkillLevel = 'Beginner' | 'Intermediate' | 'Advanced' | 'Expert';

export type ManpowerBudgetStatus =
  | 'Draft'
  | 'Submitted'
  | 'UnderReview'
  | 'Approved'
  | 'Rejected'
  | 'Active'
  | 'Closed';

export type BudgetPriority = 'Critical' | 'High' | 'Medium' | 'Low';

/** Shared by the requisition budget check and the FR-HR-136 establishment check. */
export type BudgetEnforcementMode = 'Off' | 'Warn' | 'Block';

/** ⚠ Null when the competency has never been assessed — distinct from a gap. */
export type GapStatus = 'Met' | 'Exceeded' | 'Gap';

// ── job description ──────────────────────────────────────────────────────────

export interface JobDescription {
  id: string;
  tenantId: string;
  jobDescriptionNumber: string;
  positionId: string;
  positionTitle: string;
  jobTitle: string;
  versionNumber: number;
  effectiveDate: string;
  expiryDate?: string | null;
  revisionReason?: string | null;
  supersededByVersionId?: string | null;
  jobSummary: string;
  status: JobDescriptionStatus;
  statusName?: string;
  preparedById?: string | null;
  preparedByName?: string | null;
  preparedDate?: string | null;
  reviewedById?: string | null;
  reviewedByName?: string | null;
  reviewedDate?: string | null;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  nextReviewDate?: string | null;
  reviewCycleMonths: number;
  roleIntrinsicValue?: number | null;
  roleCriticality?: RoleCriticalityLevel | null;
  roleCriticalityName?: string | null;
  industryBenchmarkSalary?: number | null;
  estimatedSalaryLow?: number | null;
  estimatedSalaryHigh?: number | null;
  suggestedSalaryGradeId?: string | null;
  suggestedSalaryGradeName?: string | null;
  valuationNotes?: string | null;
  autonomyLevel?: DecisionAuthorityLevel | null;
  decisionMakingScope?: string | null;
  financialAuthorityLimit?: number | null;
  approvalAuthorityNotes?: string | null;
  staffLevelId?: string | null;
  staffLevelName?: string | null;
  intendedEmploymentType?: string | null;
  isBargainingUnitRole: boolean;
  unionId?: string | null;
  unionName?: string | null;
  occupationCode?: string | null;
  essentialFunctionsSummary?: string | null;
  jobFamilyId?: string | null;
  jobFamilyName?: string | null;
  jobSubFamilyId?: string | null;
  jobSubFamilyName?: string | null;
  jobLevelId?: string | null;
  jobLevelName?: string | null;
}

/**
 * ⚠ What `descriptions/position/{id}` and `descriptions/status/{status}` return — a deliberate
 * narrow projection, NOT a JobDescription that lost its fields. The register uses
 * `descriptions/paged`, which carries the full shape.
 */
export interface JobDescriptionSummary {
  id: string;
  jobDescriptionNumber: string;
  positionTitle: string;
  jobTitle: string;
  versionNumber: number;
  effectiveDate: string;
  status: JobDescriptionStatus;
  nextReviewDate?: string | null;
}

export interface JobResponsibility {
  id: string;
  jobDescriptionId: string;
  responsibilityDescription: string;
  type: ResponsibilityType;
  percentageOfTime?: number | null;
  importanceWeight?: number | null;
  qualifications?: JobQualification[];
  competencies?: JobCompetency[];
  kpis?: JobResponsibilityKpi[];
}

export interface JobResponsibilityKpi {
  id: string;
  jobResponsibilityId: string;
  kpiStatement: string;
  targetOrStandard?: string | null;
  unitOfMeasure?: string | null;
  weight?: number | null;
  sequenceNumber: number;
}

export interface JobQualification {
  id: string;
  jobDescriptionId: string;
  jobResponsibilityId?: string | null;
  type: QualificationType;
  qualificationId?: string | null;
  title: string;
  description: string;
  isRequired: boolean;
  jobSpecificRequirements?: string | null;
  monetaryValue?: number | null;
}

export interface JobCompetency {
  id: string;
  jobDescriptionId: string;
  jobResponsibilityId?: string | null;
  skillId?: string | null;
  competencyId?: string | null;
  competencyName: string;
  description?: string | null;
  type: CompetencyType;
  requiredLevel: ProficiencyLevel;
  isCritical: boolean;
  monetaryValue?: number | null;
}

export interface JobPhysicalDemand {
  id: string;
  jobDescriptionId: string;
  demandType: string;
  demandDescription: string;
  frequency: PhysicalDemandFrequency;
  weightOrForceKg?: number | null;
  distanceOrDuration?: string | null;
  isEssential: boolean;
  notesOrExamples?: string | null;
  isPhysicalAttribute: boolean;
  attributeRequirement?: string | null;
  justification?: string | null;
}

export interface JobWorkingCondition {
  id: string;
  jobDescriptionId: string;
  environmentType: WorkEnvironmentType;
  description: string;
  exposureLevel: ExposureLevel;
  requiresPPE: boolean;
  ppeRequirements?: string | null;
  travelPercentage?: number | null;
  travelRequirements?: string | null;
}

export interface JobEquipmentTool {
  id: string;
  jobDescriptionId: string;
  itemName: string;
  type: string;
  descriptionOrSpecification: string;
  requiredProficiency: ProficiencyLevel;
  isEssential: boolean;
  trainingRequired?: string | null;
  linkedQualificationId?: string | null;
  trainingRequirements?: JobEquipmentTraining[];
}

export interface JobEquipmentTraining {
  id: string;
  jobEquipmentToolId: string;
  trainingProgramId?: string | null;
  requirementText: string;
  isMandatory: boolean;
}

export interface JobReportingRelationship {
  id: string;
  jobDescriptionId: string;
  relationshipType: ReportingRelationshipType;
  titleOrRole: string;
  employeeOrPositionId?: string | null;
  description: string;
  numberOfDirectReports?: number | null;
  isPrimarySupervisor: boolean;
}

export interface JobDutyItem {
  id: string;
  jobDescriptionId: string;
  sequenceNumber: number;
  dutyStatement: string;
  notes?: string | null;
}

export interface JobPpeRequirement {
  id: string;
  jobDescriptionId: string;
  ppeTypeId?: string | null;
  customPpeName?: string | null;
  isMandatory: boolean;
  notes?: string | null;
}

export interface JobMedicalRequirement {
  id: string;
  jobDescriptionId: string;
  category: string;
  requirementDescription: string;
  rationale?: string | null;
  contraindications?: string | null;
  isMandatory: boolean;
}

/**
 * ⚠ The collection is `workingConditions` here. The ENTITY navigation is `JobWorkingConditions`,
 * and reading the entity instead of the DTO is how a panel ships blank.
 */
export interface JobDescriptionDetail extends JobDescription {
  dutyItems: JobDutyItem[];
  responsibilities: JobResponsibility[];
  qualifications: JobQualification[];
  competencies: JobCompetency[];
  physicalDemands: JobPhysicalDemand[];
  workingConditions: JobWorkingCondition[];
  ppeRequirements: JobPpeRequirement[];
  equipmentTools: JobEquipmentTool[];
  reportingRelationships: JobReportingRelationship[];
  medicalRequirements: JobMedicalRequirement[];
}

/**
 * ⚠ Create carries the classification. Until slice 2 it did not, and a form had to save twice —
 * anything that skipped the second save left the taxonomy with no consumer at all.
 */
export interface CreateJobDescription {
  positionId: string;
  jobTitle: string;
  jobSummary: string;
  effectiveDate: string;
  expiryDate?: string | null;
  revisionReason?: string | null;
  reviewCycleMonths: number;
  jobFamilyId?: string | null;
  jobSubFamilyId?: string | null;
  jobLevelId?: string | null;
  staffLevelId?: string | null;
  suggestedSalaryGradeId?: string | null;
  unionId?: string | null;
  isBargainingUnitRole?: boolean;
  occupationCode?: string | null;
  essentialFunctionsSummary?: string | null;
  roleCriticality?: RoleCriticalityLevel | null;
  roleIntrinsicValue?: number | null;
  industryBenchmarkSalary?: number | null;
  valuationNotes?: string | null;
  autonomyLevel?: DecisionAuthorityLevel | null;
  decisionMakingScope?: string | null;
  financialAuthorityLimit?: number | null;
  approvalAuthorityNotes?: string | null;
}

/** ⚠ `id` is required and must match the route — the API compares them. */
export interface UpdateJobDescription extends CreateJobDescription {
  id: string;
  status: JobDescriptionStatus;
  nextReviewDate?: string | null;
}

export interface JobAnalytics {
  totalJobDescriptions: number;
  draftCount: number;
  pendingReviewCount: number;
  approvedCount: number;
  activeCount: number;
  dueForReviewCount: number;
  positionsCovered: number;
  totalPositions: number;
  positionsUncovered: number;
  positionsEstablished: number;
  positionsOverStrength: number;
  valuedRoleCount: number;
  averageEstimatedSalary?: number | null;
  missionCriticalRoleCount: number;
  statusBreakdown: NameCount[];
  topCompetencies: NameCount[];
  familyBreakdown: NameCount[];
}

export interface NameCount {
  name: string;
  count: number;
}

export interface UncoveredPosition {
  positionId: string;
  positionTitle: string;
  organizationUnitName?: string | null;
  currentlyFilled: number;
  /** True when a draft exists but has not been approved — needs an approver, not an author. */
  hasUnapprovedDraft: boolean;
}

// ── taxonomy ─────────────────────────────────────────────────────────────────

export interface JobFamily {
  id: string;
  tenantId: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  subFamilyCount: number;
  subFamilies?: JobSubFamily[];
}

export interface JobSubFamily {
  id: string;
  tenantId: string;
  jobFamilyId: string;
  jobFamilyName: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
}

/** The C# entity is `CareerLevel`; the API calls it a job level throughout. */
export interface JobLevel {
  id: string;
  tenantId: string;
  code: string;
  name: string;
  rank: number;
  description?: string | null;
  salaryGradeId?: string | null;
  salaryGradeName?: string | null;
  isActive: boolean;
}

// ── competency ───────────────────────────────────────────────────────────────

export interface Competency {
  id: string;
  code: string;
  name: string;
  description: string;
  competencyCategory: CompetencyCategory;
  proficiencyScaleMax: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CompetencySkillIndicator {
  id: string;
  competencyId: string;
  competencyName: string;
  skillId: string;
  skillName: string;
  skillCategory?: string | null;
  minimumSkillLevelRequired: SkillLevel;
  rationale?: string | null;
}

export interface PositionCompetency {
  id: string;
  positionId: string;
  positionTitle: string;
  competencyId: string;
  competencyCode: string;
  competencyName: string;
  competencyCategory: CompetencyCategory;
  proficiencyScaleMax: number;
  requiredProficiencyLevel: number;
  notes?: string | null;
}

export interface EmployeeCompetency {
  id: string;
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  competencyId: string;
  competencyCode: string;
  competencyName: string;
  competencyCategory: CompetencyCategory;
  proficiencyScaleMax: number;
  currentProficiencyLevel: number;
  assessmentDate: string;
  assessedById?: string | null;
  assessedByName?: string | null;
  assessmentMethod?: string | null;
  evidenceNotes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface EmployeeCompetencyProfile {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  positionTitle: string;
  competencies: EmployeeCompetency[];
  totalAssessed: number;
  lastAssessmentDate?: string | null;
}

export interface EmployeeCompetencyGap {
  competencyId: string;
  competencyCode: string;
  competencyName: string;
  competencyCategory: CompetencyCategory;
  proficiencyScaleMax: number;
  requiredLevel: number;
  /** ⚠ Null when never assessed. */
  currentLevel?: number | null;
  /** ⚠ Null when never assessed — NOT 'Gap'. An unknown is not a shortfall. */
  gapStatus?: GapStatus | null;
}

/**
 * ⚠ An OBJECT, not a list. The endpoint is called `gaps` and the first version of the harness
 * asserted `Array.isArray` on it — the mistake this file's header warns about, made once.
 */
export interface EmployeePositionCompetencyGapSummary {
  employeeId: string;
  employeeName: string;
  employeeNumber: string;
  positionId: string;
  positionTitle: string;
  totalCompetencies: number;
  metOrExceeded: number;
  hasGap: number;
  notAssessed: number;
  competencyGaps: EmployeeCompetencyGap[];
}

export interface OrganisationCompetencyGap {
  competencyId: string;
  competencyCode: string;
  competencyName: string;
  competencyCategory: string;
  employeesRequiring: number;
  meetingRequirement: number;
  belowRequirement: number;
  /** ⚠ Counted separately from `belowRequirement`: an unknown is not a training need. */
  notAssessed: number;
}

// ── manpower budget & establishment ──────────────────────────────────────────

export interface ManpowerBudget {
  id: string;
  tenantId: string;
  budgetNumber: string;
  fiscalYear: number;
  organizationLevelId?: string | null;
  organizationLevelName?: string | null;
  organizationUnitId?: string | null;
  organizationUnitName?: string | null;
  status: ManpowerBudgetStatus;
  statusName?: string;
  periodStartDate: string;
  periodEndDate: string;
  currentHeadcount: number;
  currentSalaryCost: number;
  plannedHeadcount: number;
  plannedSalaryCost: number;
  plannedNewHires: number;
  plannedTerminations: number;
  plannedPromotions: number;
  plannedTransfers: number;
  salaryBudget: number;
  benefitsBudget: number;
  recruitmentBudget: number;
  trainingBudget: number;
  /** Computed from the four component budgets — do not send it. */
  totalBudget: number;
  /** ⚠ Nothing writes this. It can only come from Finance actuals; see the integration backlog. */
  actualSpent: number;
  /** ⚠ Nothing writes this either — permanently zero until the post-HR Finance sweep. */
  variance: number;
  businessJustification?: string | null;
  approvedById?: string | null;
  approvedByName?: string | null;
  approvalDate?: string | null;
  rejectionReason?: string | null;
}

export interface ManpowerBudgetLine {
  id: string;
  tenantId: string;
  manpowerBudgetId: string;
  positionId: string;
  positionTitle: string;
  jobDescriptionId?: string | null;
  jobDescriptionNumber?: string | null;
  currentCount: number;
  currentFilled: number;
  currentVacant: number;
  currentAverageSalary: number;
  currentTotalCost: number;
  plannedCount: number;
  plannedNewPositions: number;
  plannedEliminations: number;
  plannedAverageSalary: number;
  plannedTotalCost: number;
  quarter?: number | null;
  targetFillDate?: string | null;
  priority: BudgetPriority;
  isCritical: boolean;
  notes?: string | null;
}

/**
 * FR-HR-136. ⚠ `isEstablished` is the field every rule keys off, not `expectedHeadcount`: an
 * unestablished post carries a headcount of 1 by column default and is constrained by nothing.
 */
export interface PositionEstablishment {
  positionId: string;
  positionTitle: string;
  expectedHeadcount: number;
  /** Live count of active employees — not a figure anyone typed. */
  currentlyFilled: number;
  establishmentApprovedOn?: string | null;
  establishmentSourceBudgetId?: string | null;
  establishmentSourceBudgetNumber?: string | null;
  isEstablished: boolean;
}
