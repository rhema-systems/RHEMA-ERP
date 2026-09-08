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
  | 'TechnicalSkills'
  | 'Language'
  | 'Membership'
  | 'Other';

/**
 * ⚠ **Not the same enum as `CompetencyCategory`.** This one classifies a competency *on a job
 * description* and has FOUR values; the master competency catalogue is categorised by
 * `CompetencyCategory`, which has six and includes `Functional`. This union carried `Functional`
 * until the authoring forms were built — a value the API's converter rejects outright, so the
 * dropdown it would have produced could never have saved.
 */
export type CompetencyType = 'Technical' | 'Behavioral' | 'Leadership' | 'Core';

export type ProficiencyLevel = 'Basic' | 'WorkingKnowledge' | 'Proficient' | 'Advanced' | 'Expert';

/** ⚠ The last value is `Continuously`, not `Constantly` — `ExposureLevel` is the one ending in Constant. */
export type PhysicalDemandFrequency =
  | 'Never'
  | 'Rarely'
  | 'Occasionally'
  | 'Frequently'
  | 'Continuously';

/** The 27 recognised demands. `Other` is the catch-all; the description carries the detail. */
export type PhysicalDemandType =
  | 'Sitting'
  | 'Standing'
  | 'Walking'
  | 'Running'
  | 'Climbing'
  | 'Balancing'
  | 'Stooping'
  | 'Kneeling'
  | 'Crouching'
  | 'Crawling'
  | 'Reaching'
  | 'Handling'
  | 'Fingering'
  | 'Feeling'
  | 'Talking'
  | 'Hearing'
  | 'SeeingNear'
  | 'SeeingFar'
  | 'SeeingPeripheral'
  | 'SeeingColor'
  | 'SeeingDepth'
  | 'TastingSmelling'
  | 'LiftingCarrying'
  | 'PushingPulling'
  | 'KeyboardingTyping'
  | 'RepetitiveMotion'
  | 'Other';

/** ⚠ `Warehouse` and `Site` are NOT members — both were invented by the first draft of this file. */
export type WorkEnvironmentType =
  | 'Office'
  | 'Outdoor'
  | 'Industrial'
  | 'Laboratory'
  | 'Remote'
  | 'Hybrid'
  | 'FieldBased'
  | 'Other';

export type EquipmentType =
  | 'SoftwareApplication'
  | 'ComputerHardware'
  | 'MobileDevice'
  | 'OfficeEquipment'
  | 'HandTool'
  | 'PowerTool'
  | 'HeavyMachinery'
  | 'Vehicle'
  | 'SpecializedInstrument'
  | 'SafetyEquipment'
  | 'Other';

export type MedicalRequirementCategory = 'Medical' | 'Mental' | 'Health' | 'Sensory' | 'Other';

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

// ── option lists for the authoring forms ─────────────────────────────────

/**
 * Every list below is `{ value, label }` because the API takes the **member name** while a form
 * has to show something a person would say. `SeeingNear` and `KeyboardingTyping` are not labels.
 *
 * ⚠ The values are the contract. `JsonStringEnumConverter` matches the C# member name and rejects
 * anything else with a 400, so a prettier value is a broken save.
 */
export interface EnumOption<T extends string> {
  value: T;
  label: string;
}

export const RESPONSIBILITY_TYPES: EnumOption<ResponsibilityType>[] = [
  { value: 'Core', label: 'Core responsibility' },
  { value: 'Secondary', label: 'Secondary responsibility' },
  { value: 'Occasional', label: 'Occasional responsibility' },
];

export const QUALIFICATION_TYPES: EnumOption<QualificationType>[] = [
  { value: 'Education', label: 'Education' },
  { value: 'Experience', label: 'Work experience' },
  { value: 'Certification', label: 'Certification' },
  { value: 'License', label: 'License' },
  { value: 'TechnicalSkills', label: 'Technical skills' },
  { value: 'Language', label: 'Language' },
  { value: 'Membership', label: 'Professional membership' },
  { value: 'Other', label: 'Other' },
];

export const JOB_COMPETENCY_TYPES: EnumOption<CompetencyType>[] = [
  { value: 'Technical', label: 'Technical' },
  { value: 'Behavioral', label: 'Behavioural' },
  { value: 'Leadership', label: 'Leadership' },
  { value: 'Core', label: 'Core competency' },
];

export const PROFICIENCY_LEVELS: EnumOption<ProficiencyLevel>[] = [
  { value: 'Basic', label: 'Basic / awareness' },
  { value: 'WorkingKnowledge', label: 'Working knowledge' },
  { value: 'Proficient', label: 'Proficient' },
  { value: 'Advanced', label: 'Advanced' },
  { value: 'Expert', label: 'Expert' },
];

/** Ordered as an assessor walks a body: posture, movement, manipulation, senses, then load. */
export const PHYSICAL_DEMAND_TYPES: EnumOption<PhysicalDemandType>[] = [
  { value: 'Sitting', label: 'Sitting' },
  { value: 'Standing', label: 'Standing' },
  { value: 'Walking', label: 'Walking' },
  { value: 'Running', label: 'Running' },
  { value: 'Climbing', label: 'Climbing' },
  { value: 'Balancing', label: 'Balancing' },
  { value: 'Stooping', label: 'Stooping (bending at the waist)' },
  { value: 'Kneeling', label: 'Kneeling' },
  { value: 'Crouching', label: 'Crouching' },
  { value: 'Crawling', label: 'Crawling' },
  { value: 'Reaching', label: 'Reaching' },
  { value: 'Handling', label: 'Handling (grasping, turning)' },
  { value: 'Fingering', label: 'Fingering (fine finger work)' },
  { value: 'Feeling', label: 'Feeling (perceiving by touch)' },
  { value: 'Talking', label: 'Talking' },
  { value: 'Hearing', label: 'Hearing' },
  { value: 'SeeingNear', label: 'Seeing — near acuity' },
  { value: 'SeeingFar', label: 'Seeing — distance acuity' },
  { value: 'SeeingPeripheral', label: 'Seeing — peripheral vision' },
  { value: 'SeeingColor', label: 'Seeing — colour vision' },
  { value: 'SeeingDepth', label: 'Seeing — depth perception' },
  { value: 'TastingSmelling', label: 'Tasting or smelling' },
  { value: 'LiftingCarrying', label: 'Lifting or carrying' },
  { value: 'PushingPulling', label: 'Pushing or pulling' },
  { value: 'KeyboardingTyping', label: 'Keyboarding or typing' },
  { value: 'RepetitiveMotion', label: 'Repetitive motion' },
  { value: 'Other', label: 'Other' },
];

/** The labels carry the percentage bands — the enum names alone do not say what they mean. */
export const PHYSICAL_DEMAND_FREQUENCIES: EnumOption<PhysicalDemandFrequency>[] = [
  { value: 'Never', label: 'Never' },
  { value: 'Rarely', label: 'Rarely (up to 5%)' },
  { value: 'Occasionally', label: 'Occasionally (6–33%)' },
  { value: 'Frequently', label: 'Frequently (34–66%)' },
  { value: 'Continuously', label: 'Continuously (67–100%)' },
];

export const WORK_ENVIRONMENT_TYPES: EnumOption<WorkEnvironmentType>[] = [
  { value: 'Office', label: 'Office' },
  { value: 'Outdoor', label: 'Outdoor' },
  { value: 'Industrial', label: 'Industrial' },
  { value: 'Laboratory', label: 'Laboratory' },
  { value: 'Remote', label: 'Remote' },
  { value: 'Hybrid', label: 'Hybrid' },
  { value: 'FieldBased', label: 'Field-based' },
  { value: 'Other', label: 'Other' },
];

export const EXPOSURE_LEVELS: EnumOption<ExposureLevel>[] = [
  { value: 'None', label: 'None' },
  { value: 'Rare', label: 'Rare (under 1%)' },
  { value: 'Occasional', label: 'Occasional (6–33%)' },
  { value: 'Frequent', label: 'Frequent (34–66%)' },
  { value: 'Constant', label: 'Constant (67–100%)' },
];

export const EQUIPMENT_TYPES: EnumOption<EquipmentType>[] = [
  { value: 'SoftwareApplication', label: 'Software application' },
  { value: 'ComputerHardware', label: 'Computer hardware' },
  { value: 'MobileDevice', label: 'Mobile device' },
  { value: 'OfficeEquipment', label: 'Office equipment' },
  { value: 'HandTool', label: 'Hand tool' },
  { value: 'PowerTool', label: 'Power tool' },
  { value: 'HeavyMachinery', label: 'Heavy machinery' },
  { value: 'Vehicle', label: 'Vehicle' },
  { value: 'SpecializedInstrument', label: 'Specialised instrument' },
  { value: 'SafetyEquipment', label: 'Safety equipment' },
  { value: 'Other', label: 'Other' },
];

/**
 * ⚠ There is deliberately no "reports to" or "supervises" here — the C# enum's own remarks say
 * the supervisory line lives on the position, not on the job description. Do not add one.
 */
export const REPORTING_RELATIONSHIP_TYPES: EnumOption<ReportingRelationshipType>[] = [
  { value: 'CollaboratesWith', label: 'Collaborates with' },
  { value: 'InternalCustomers', label: 'Internal customers' },
  { value: 'ExternalCustomers', label: 'External customers' },
  { value: 'Vendors', label: 'Vendors' },
  { value: 'RegulatoryBodies', label: 'Regulatory bodies' },
  { value: 'MatrixReport', label: 'Matrix report' },
];

export const MEDICAL_REQUIREMENT_CATEGORIES: EnumOption<MedicalRequirementCategory>[] = [
  { value: 'Medical', label: 'Medical' },
  { value: 'Mental', label: 'Mental' },
  { value: 'Health', label: 'General health' },
  { value: 'Sensory', label: 'Sensory' },
  { value: 'Other', label: 'Other' },
];

/**
 * The statuses a job description may still be authored in.
 *
 * ⚠ **The API does not enforce this — the screen does.** Not one child-collection write checks
 * status; `AddPhysicalDemandAsync` and its eleven siblings call `GetOwnedJobDescriptionAsync`,
 * which verifies the tenant and nothing else. So the API will rewrite the duties of an approved,
 * in-force job description with no new version and no trace. The register offers "New version" for
 * exactly that, and the authoring panels go read-only outside these two.
 */
export const AUTHORABLE_JOB_DESCRIPTION_STATUSES: JobDescriptionStatus[] = ['Draft', 'UnderRevision'];

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
  /** Server-rendered `type.ToString()`. Read-only — the write DTOs take `type`. */
  typeName?: string;
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
  typeName?: string;
  qualificationId?: string | null;
  /** Resolved from the qualification catalogue; null when `qualificationId` is unset. */
  qualificationName?: string | null;
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
  /** Resolved from the skill catalogue. */
  skillName?: string | null;
  competencyId?: string | null;
  /**
   * ⚠ Resolved from the competency FRAMEWORK, and not the same field as `competencyName`.
   * `competencyName` is the free text typed on this row; this is what the linked master record
   * is called. They can disagree, and when they do the free text is what the document prints.
   */
  masterCompetencyName?: string | null;
  competencyName: string;
  description?: string | null;
  type: CompetencyType;
  typeName?: string;
  requiredLevel: ProficiencyLevel;
  requiredLevelName?: string;
  isCritical: boolean;
  monetaryValue?: number | null;
}

export interface JobPhysicalDemand {
  id: string;
  jobDescriptionId: string;
  demandType: PhysicalDemandType;
  demandTypeName?: string;
  demandDescription: string;
  frequency: PhysicalDemandFrequency;
  frequencyName?: string;
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
  environmentTypeName?: string;
  description: string;
  exposureLevel: ExposureLevel;
  exposureLevelName?: string;
  requiresPPE: boolean;
  ppeRequirements?: string | null;
  travelPercentage?: number | null;
  travelRequirements?: string | null;
}

export interface JobEquipmentTool {
  id: string;
  jobDescriptionId: string;
  itemName: string;
  type: EquipmentType;
  typeName?: string;
  descriptionOrSpecification: string;
  requiredProficiency: ProficiencyLevel;
  requiredProficiencyName?: string;
  isEssential: boolean;
  trainingRequired?: string | null;
  linkedQualificationId?: string | null;
  trainingRequirements?: JobEquipmentTraining[];
}

export interface JobEquipmentTraining {
  id: string;
  jobEquipmentToolId: string;
  trainingProgramId?: string | null;
  /** Resolved from the training catalogue; null when not linked to a program. */
  trainingProgramName?: string | null;
  requirementText: string;
  isMandatory: boolean;
}

export interface JobReportingRelationship {
  id: string;
  jobDescriptionId: string;
  relationshipType: ReportingRelationshipType;
  relationshipTypeName?: string;
  titleOrRole: string;
  /** ⚠ An `EmployeePosition` id, despite the name — the FK targets the position table. */
  employeeOrPositionId?: string | null;
  /**
   * Resolved position title. ⚠ Comes back **null on a create or update response**: neither
   * service method re-includes the navigation, only the list read does. Refetch after a write
   * rather than reading this off the response.
   */
  relatedPositionTitle?: string | null;
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
  /** Resolved from the safety module's PPE catalogue. */
  ppeTypeName?: string | null;
  customPpeName?: string | null;
  /** Server-computed: the catalogue name if there is one, else the custom name, else ''. */
  displayName?: string;
  isMandatory: boolean;
  notes?: string | null;
}

export interface JobMedicalRequirement {
  id: string;
  jobDescriptionId: string;
  category: MedicalRequirementCategory;
  categoryName?: string;
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

// ── child-collection writes ──────────────────────────────────────────────────

/**
 * The twelve collections that hang off a job description, as their C# create/update DTOs declare
 * them — **not** as the read DTO looks.
 *
 * Three rules the whole family follows, each learned from a DTO that breaks the guess:
 *
 * 1. **The parent id is never in the body.** Every `POST` carries it in the route and the
 *    controller assigns `dto.JobDescriptionId = jobDescriptionId` itself. Sending it changes
 *    nothing; omitting it is correct.
 * 2. **`id` IS in the update body.** It comes from `UpdateDtoBase`, so the C# class declaration
 *    shows no id at all, while every controller does `if (id != dto.Id) return BadRequest`. An
 *    update without it 400s on a field you cannot see by reading the DTO.
 * 3. **Update is not create-minus-the-parent.** `UpdateJobQualificationDto` drops
 *    `jobResponsibilityId` and `UpdateJobCompetencyDto` drops it too, so a row's link to a
 *    responsibility is fixed at creation. `UpdateJobEquipmentTrainingDto` likewise cannot be
 *    repointed at another tool.
 */

export interface CreateJobDutyItem {
  /** 0 asks the server for the next number in sequence. */
  sequenceNumber: number;
  dutyStatement: string;
  notes?: string | null;
}

export interface UpdateJobDutyItem extends CreateJobDutyItem {
  id: string;
}

export interface CreateJobResponsibility {
  responsibilityDescription: string;
  type: ResponsibilityType;
  percentageOfTime?: number | null;
  importanceWeight?: number | null;
}

export interface UpdateJobResponsibility extends CreateJobResponsibility {
  id: string;
}

export interface CreateJobResponsibilityKpi {
  kpiStatement: string;
  targetOrStandard?: string | null;
  unitOfMeasure?: string | null;
  weight?: number | null;
  sequenceNumber: number;
}

export interface UpdateJobResponsibilityKpi extends CreateJobResponsibilityKpi {
  id: string;
}

export interface CreateJobQualification {
  /** Optional: pins the qualification to one responsibility rather than the whole job. */
  jobResponsibilityId?: string | null;
  type: QualificationType;
  /** Optional link to the qualification catalogue; `title` still carries the wording. */
  qualificationId?: string | null;
  title: string;
  description: string;
  isRequired: boolean;
  jobSpecificRequirements?: string | null;
  monetaryValue?: number | null;
}

/** ⚠ `jobResponsibilityId` is absent — the link is fixed at creation. */
/**
 * ⚠ `jobResponsibilityId` is part of an update now — it was omitted here because the C# DTO did not
 * declare it, which made the attachment permanent once made. Like every field on these DTOs it
 * REPLACES: omit it and the row is detached from its responsibility.
 */
export interface UpdateJobQualification extends CreateJobQualification {
  id: string;
}

export interface CreateJobCompetency {
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

/** ⚠ `jobResponsibilityId` is absent — the link is fixed at creation. */
/** ⚠ Carries `jobResponsibilityId` for the same reason as {@link UpdateJobQualification}. */
export interface UpdateJobCompetency extends CreateJobCompetency {
  id: string;
}

export interface CreateJobPhysicalDemand {
  demandType: PhysicalDemandType;
  demandDescription: string;
  frequency: PhysicalDemandFrequency;
  weightOrForceKg?: number | null;
  distanceOrDuration?: string | null;
  isEssential: boolean;
  notesOrExamples?: string | null;
  /**
   * Marks the row as an inherent attribute of the person rather than an action of the job
   * ("normal colour vision"). Both `attributeRequirement` and `justification` belong to it —
   * a requirement about a person's body needs a stated reason.
   */
  isPhysicalAttribute: boolean;
  attributeRequirement?: string | null;
  justification?: string | null;
}

export interface UpdateJobPhysicalDemand extends CreateJobPhysicalDemand {
  id: string;
}

export interface CreateJobWorkingCondition {
  environmentType: WorkEnvironmentType;
  description: string;
  exposureLevel: ExposureLevel;
  /** ⚠ Capital PPE on both of these — `requiresPPE`, `ppeRequirements`. */
  requiresPPE: boolean;
  ppeRequirements?: string | null;
  travelPercentage?: number | null;
  travelRequirements?: string | null;
}

export interface UpdateJobWorkingCondition extends CreateJobWorkingCondition {
  id: string;
}

export interface CreateJobEquipmentTool {
  itemName: string;
  type: EquipmentType;
  descriptionOrSpecification: string;
  requiredProficiency: ProficiencyLevel;
  isEssential: boolean;
  /** Free-text summary. The itemised requirements are the equipment-training collection. */
  trainingRequired?: string | null;
  linkedQualificationId?: string | null;
}

export interface UpdateJobEquipmentTool extends CreateJobEquipmentTool {
  id: string;
}

export interface CreateJobEquipmentTraining {
  trainingProgramId?: string | null;
  requirementText: string;
  isMandatory: boolean;
}

/** ⚠ No tool id — a training row cannot be moved to a different tool. */
export interface UpdateJobEquipmentTraining extends CreateJobEquipmentTraining {
  id: string;
}

export interface CreateJobReportingRelationship {
  relationshipType: ReportingRelationshipType;
  titleOrRole: string;
  /** An `EmployeePosition` id, despite the name. */
  employeeOrPositionId?: string | null;
  description: string;
  numberOfDirectReports?: number | null;
  isPrimarySupervisor: boolean;
}

export interface UpdateJobReportingRelationship extends CreateJobReportingRelationship {
  id: string;
}

export interface CreateJobPpeRequirement {
  /** Either this or `customPpeName` — the catalogue link wins for display. */
  ppeTypeId?: string | null;
  customPpeName?: string | null;
  isMandatory: boolean;
  notes?: string | null;
}

export interface UpdateJobPpeRequirement extends CreateJobPpeRequirement {
  id: string;
}

export interface CreateJobMedicalRequirement {
  category: MedicalRequirementCategory;
  requirementDescription: string;
  rationale?: string | null;
  contraindications?: string | null;
  isMandatory: boolean;
}

export interface UpdateJobMedicalRequirement extends CreateJobMedicalRequirement {
  id: string;
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
  /**
   * ⚠ On both the create and the update DTO, and on neither form. It is here so an edit can carry
   * it back unchanged — `UpdateEntity` replaces every column, so omitting it nulls it.
   */
  intendedEmploymentType?: string | null;
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

/**
 * ⚠ `id` is required and must match the route — the API compares them.
 *
 * ⚠ `status` is NOT a transition. The API never writes it (submit, review and approve own it) and
 * refuses a value that contradicts the record, so send back the one that was read. It is required
 * here rather than optional because the update is a REPLACE: a caller assembling this payload is
 * round-tripping a whole document, and a status it cannot state is a document it has not read.
 */
export interface UpdateJobDescription extends CreateJobDescription {
  id: string;
  status: JobDescriptionStatus;
  nextReviewDate?: string | null;
}

/** One qualification or competency, with the money the organisation attaches to it for this role. */
export interface JobValuationLine {
  id: string;
  name: string;
  monetaryValue?: number | null;
}

/**
 * What a job valuation works out. See `jobArchitectureService.getValuation` — ⚠ reading this
 * WRITES the estimate back onto the job description.
 */
export interface JobValuationSummary {
  jobDescriptionId: string;
  jobTitle: string;
  totalQualificationValue: number;
  totalCompetencyValue: number;
  roleIntrinsicValue: number;
  /** Server-computed: the three figures above added together. */
  totalEstimatedValue: number;
  roleCriticality?: RoleCriticalityLevel | null;
  roleCriticalityName?: string | null;
  industryBenchmarkSalary?: number | null;
  estimatedSalaryLow?: number | null;
  estimatedSalaryHigh?: number | null;
  suggestedSalaryGradeId?: string | null;
  suggestedSalaryGradeName?: string | null;
  suggestedGradeMinSalary?: number | null;
  suggestedGradeMaxSalary?: number | null;
  valuationNotes?: string | null;
  qualificationLines: JobValuationLine[];
  competencyLines: JobValuationLine[];
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

/** One row of a batch assessment that did not land, and why. */
export interface BatchAssessmentError {
  employeeCompetencyId?: string | null;
  competencyId?: string | null;
  errorMessage: string;
}

/**
 * The outcome of a batch assessment.
 *
 * ⚠ Partial success is the normal case, not an error state — the screen shows `errors` row by row
 * rather than treating a non-zero `failed` as a failed request.
 */
export interface BatchAssessmentResult {
  totalSubmitted: number;
  succeeded: number;
  failed: number;
  errors: BatchAssessmentError[];
}
