'use client';

import {
  useEffect,
  useMemo,
  useRef,
  useState,
  useTransition,
  type Dispatch,
  type RefObject,
  type SetStateAction,
  type WheelEventHandler,
} from 'react';
import { useParams, useRouter } from 'next/navigation';
import { format, getISOWeek } from 'date-fns';
import * as XLSX from 'xlsx';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { IssueRequisitionDialog } from '@/components/inventory/IssueRequisitionDialog';
import { ReturnRequisitionDialog } from '@/components/inventory/ReturnRequisitionDialog';
import { RequisitionDialog } from '@/components/inventory/RequisitionDialog';
import {
  getProjectWorkspaceTabs,
  PROJECT_WORKSPACE_TAB_LABELS,
  type ProjectWorkspaceTab,
} from './projectWorkspaceTabs';
import { ProjectAccessTab } from './components/ProjectAccessTab';
import { ProjectAnalysisTab } from './components/ProjectAnalysisTab';
import { ProjectApprovalsTab } from './components/ProjectApprovalsTab';
import {
  ProjectBudgetingTab,
  type ProjectBoqBudgetWorksheetUpdate,
} from './components/ProjectBudgetingTab';
import { ProjectCommercialAdminTab } from './components/ProjectCommercialAdminTab';
import { ProjectCommercialTab } from './components/ProjectCommercialTab';
import { ProjectCustomerVariationsTab } from './components/ProjectCustomerVariationsTab';
import { ProjectDesignTab } from './components/ProjectDesignTab';
import { ProjectDefectsTab } from './components/ProjectDefectsTab';
import { ProjectDocumentsTab } from './components/ProjectDocumentsTab';
import { ProjectExecutionTab } from './components/ProjectExecutionTab';
import { ProjectGanttPlannerEditor } from './components/ProjectGanttPlannerEditor';
import { ProjectGovernanceTab } from './components/ProjectGovernanceTab';
import { ProjectHandoverTab } from './components/ProjectHandoverTab';
import { ProjectHistoryTab } from './components/ProjectHistoryTab';
import { ProjectMaterialsTab } from './components/ProjectMaterialsTab';
import { ProjectOverviewTab } from './components/ProjectOverviewTab';
import { ProjectPackagesTab } from './components/ProjectPackagesTab';
import { ProjectPhasesTab } from './components/ProjectPhasesTab';
import { ProjectPlanTab } from './components/ProjectPlanTab';
import { ProjectSiteControlsTab } from './components/ProjectSiteControlsTab';
import { ProjectUnitsTab } from './components/ProjectUnitsTab';
import {
  DEFAULT_PROJECT_CURRENCY,
  buildProjectCurrencyOptions,
  findProjectCurrency,
  formatProjectCurrencyLabel,
  formatProjectMoney,
  loadProjectCurrencyContext,
  resolveProjectBaseCurrency,
  type ProjectCurrencyReference,
} from '@/lib/project-currency';
import {
  ArrowLeft,
  ChevronDown,
  ChevronRight,
  Download,
  Plus,
  RefreshCw,
  Save,
  Trash2,
} from 'lucide-react';
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import { contractService, type ContractDto } from '@/services/contractService';
import { type CurrencyListDto } from '@/services/financeCommonService';
import { fixedAssetsDataService } from '@/services/finance/fixed-assets-data.service';
import {
  inventoryManagementService,
  type InventoryItemDto,
  type UnitOfMeasureDto,
  type WarehouseDto,
} from '@/services/inventoryManagementService';
import {
  inventoryRequisitionService,
  type InventoryRequisitionDto,
} from '@/services/inventoryRequisitionService';
import {
  maintenanceDataService,
  type Asset as MaintenanceAssetLookupOption,
} from '@/services/maintenanceDataService';
import { userService } from '@/services/user';
import {
  AddProjectMemberDto,
  AttachProjectDocumentDto,
  CreateProjectActionItemDto,
  CreateProjectApprovalRegisterItemDto,
  CreateProjectAssetLinkDto,
  CreateProjectBaselineDto,
  CreateProjectBillingScheduleDto,
  CreateProjectBoqItemDto,
  CreateProjectBuildingDto,
  CreateProjectBudgetRevisionDto,
  CreateProjectChangeRequestDto,
  CreateProjectCommentDto,
  CreateProjectCustomerVariationDto,
  CreateProjectDrawingDto,
  CreateProjectDefectLiabilityCaseDto,
  CreateProjectDecisionDto,
  CreateProjectDeliverableDto,
  CreateProjectExpenseDto,
  CreateProjectExternalAccessPolicyDto,
  CreateProjectForecastVersionDto,
  CreateProjectFloorDto,
  CreateProjectCommissioningItemDto,
  CreateProjectExtensionOfTimeDto,
  CreateProjectHandoverItemDto,
  CreateProjectUnitHandoverBatchDto,
  CreateProjectInterimValuationDto,
  CreateProjectIssueDto,
  CreateProjectNonConformanceDto,
  CreateProjectPackageDto,
  CreateProjectPaymentCertificateDto,
  CreateProjectQualityCheckpointDto,
  CreateProjectRfiDto,
  CreateProjectMilestoneDto,
  CreateProjectLessonLearnedDto,
  CreateProjectInvoiceRequestDto,
  CreateProjectMeetingMinuteDto,
  CreateProjectPhaseDto,
  CreateProjectResourceAllocationDto,
  CreateProjectRiskDto,
  CreateProjectSnagItemDto,
  CreateProjectTaskDependencyDto,
  CreateProjectTimesheetEntryDto,
  CreateProjectUnitDto,
  CreateProjectUnitReleaseBatchDto,
  CreateProjectVariationOrderDto,
  CreateProjectWorkItemDto,
  CreateProjectSiteInstructionDto,
  CreateProjectSubmittalDto,
  ProjectAiInsightDto,
  ProjectBaselineComparisonDto,
  ProjectBudgetRevisionDto,
  ProjectCatalogEntryDto,
  ProjectBoqItemDto,
  ProjectCommercialSummaryDto,
  ProjectCustomerVariationDto,
  ProjectDetailDto,
  ProjectDefectLiabilityCaseDto,
  ProjectDrawingDto,
  ProjectFinancialControlSummaryDto,
  ProjectForecastVersionDto,
  ProjectExtensionOfTimeDto,
  ProjectFinalAccountDto,
  ProjectGovernanceSummaryDto,
  ProjectIntegrationSummaryDto,
  ProjectInterimValuationDto,
  ProjectInvoiceRequestDto,
  ProjectLinkOptionsDto,
  ProjectClosureDto,
  ProjectPackageDto,
  ProjectPostHandoverSummaryDto,
  ProjectPaymentCertificateDto,
  ProjectPortfolioDto,
  ProjectPhaseDto,
  ProjectPhaseGateEvaluationDto,
  ProjectProcurementPlanItemLookupDto,
  ProjectPriorityDto,
  ProjectProgramDto,
  ProjectPurchaseOrderLookupDto,
  ProjectPurchaseRequisitionLookupDto,
  ProjectQualityCheckpointDto,
  ProjectRfiDto,
  ProjectScheduleAnalysisDto,
  ProjectSiteInstructionDto,
  ProjectSnagItemDto,
  ProjectSubmittalDto,
  ProjectTenderLookupDto,
  ProjectTemplateDto,
  ProjectTypeDto,
  ProjectUnitDto,
  ProjectUnitTypeTemplateDto,
  ProjectVariationOrderDto,
  ProjectWorkspaceDto,
  ProjectWorkItemDto,
  ProjectNonConformanceDto,
  UpsertProjectClosureDto,
  UpdateProjectDto,
  projectService,
} from '@/services/projectService';
import type { FixedAsset } from '@/types/fixed-assets';
import type { User } from '@/types';

const today = () => new Date().toISOString().slice(0, 10);
const DEFAULT_METHODOLOGIES = [
  'Waterfall',
  'Agile',
  'Hybrid',
  'Program',
  'Internal',
];
const DEFAULT_BILLING_TYPES = [
  'Milestone',
  'FixedPrice',
  'TimeAndMaterials',
  'Retainer',
  'CostPlus',
  'NonBillable',
];
const DEFAULT_FUNDING_SOURCES = [
  'Customer Contract',
  'Internal Budget',
  'Capex Allocation',
  'Grant Funding',
  'Department Allocation',
];
const DEFAULT_RESOURCE_ROLES = [
  'ProjectManager',
  'TeamMember',
  'TaskOwner',
  'FinanceOfficer',
  'RiskOfficer',
  'ProcurementOfficer',
  'ExternalContributor',
];
const DEFAULT_RESOURCE_ROUTING_POLICIES = [
  'Balanced',
  'BestMatch',
  'CertifiedFirst',
  'AvailabilityFirst',
];
const DEFAULT_MEMBER_ROLES = [
  'Sponsor',
  'Project Manager',
  'Team Member',
  'Task Owner',
  'Finance Officer',
  'External Contributor',
];
const DEFAULT_TASK_STATUSES = [
  'New',
  'Assigned',
  'InProgress',
  'Blocked',
  'PendingReview',
  'Completed',
  'Closed',
  'Cancelled',
];
const DEFAULT_TASK_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];
const DEFAULT_DELIVERABLE_STATUSES = [
  'Draft',
  'InReview',
  'Approved',
  'Rejected',
  'Issued',
  'Accepted',
];
const DEFAULT_RISK_STATUSES = [
  'Open',
  'Monitoring',
  'Mitigated',
  'Closed',
  'Escalated',
];
const DEFAULT_RISK_CATEGORIES = [
  'Scope',
  'Schedule',
  'Resource',
  'Quality',
  'Vendor',
  'Compliance',
  'Financial',
  'Operational',
];
const DEFAULT_RISK_RESPONSE_STRATEGIES = [
  'Monitor',
  'Mitigate',
  'Avoid',
  'Transfer',
  'Accept',
  'Escalate',
];
const DEFAULT_EXPENSE_CATEGORIES = [
  'Travel',
  'Meals',
  'Lodging',
  'Supplies',
  'Equipment',
  'Other',
];
const DEFAULT_CHANGE_TYPES = [
  'Scope',
  'Budget',
  'Schedule',
  'Quality',
  'Contract',
];
const DEFAULT_ISSUE_STATUSES = [
  'Open',
  'InProgress',
  'PendingReview',
  'Resolved',
  'Closed',
  'Escalated',
];
const DEFAULT_CHANGE_STATUSES = [
  'Draft',
  'PendingApproval',
  'Approved',
  'Rejected',
  'Implemented',
  'Archived',
];
const DEFAULT_ISSUE_SEVERITIES = ['Low', 'Medium', 'High', 'Critical'];
const DEFAULT_QUALITY_CHECKPOINT_STATUSES = [
  'Open',
  'InReview',
  'SignedOff',
  'Passed',
  'Failed',
  'Waived',
];
const DEFAULT_NON_CONFORMANCE_STATUSES = [
  'Open',
  'InReview',
  'Resolved',
  'Closed',
  'Waived',
];
const DEFAULT_NON_CONFORMANCE_SEVERITIES = [
  'Low',
  'Medium',
  'High',
  'Critical',
];
const DEFAULT_TIMESHEET_WORK_TYPES = [
  'Standard',
  'Overtime',
  'Travel',
  'Support',
  'BillableDelivery',
  'Admin',
];
const DEFAULT_DECISION_STATUSES = ['Draft', 'Approved', 'Rejected'];
const DEFAULT_MEETING_TYPES = [
  'Status',
  'RiskReview',
  'SteeringCommittee',
  'Closure',
  'Customer',
];
const DEFAULT_ACTION_ITEM_STATUSES = [
  'Open',
  'InProgress',
  'Completed',
  'Closed',
];
const DEFAULT_ACTION_ITEM_PRIORITIES = ['Low', 'Normal', 'High', 'Critical'];
const DEFAULT_LESSON_CATEGORIES = [
  'General',
  'Delivery',
  'Process',
  'Quality',
  'Commercial',
  'Stakeholder',
];
const DEFAULT_LESSON_VISIBILITIES = ['Internal', 'Tenant', 'External'];
const DEFAULT_ASSET_LINK_TYPES = [
  'Asset',
  'Equipment',
  'Installation',
  'Transfer',
  'Maintenance',
];
const DEFAULT_ASSET_LINK_STATUSES = [
  'Linked',
  'Reserved',
  'Installed',
  'Transferred',
  'Returned',
];
const DEFAULT_DOCUMENT_CATEGORIES = [
  'Charter',
  'Plan',
  'Requirements',
  'Design',
  'Minutes',
  'Contracts',
  'Drawings',
  'Reports',
  'AcceptanceCertificates',
  'RiskLogs',
  'ChangeApprovals',
  'ClosureDocuments',
  'General',
];
const DEFAULT_DOCUMENT_TYPES = [
  'Attachment',
  'Evidence',
  'Approval',
  'Reference',
  'Contract',
  'Drawing',
  'Minutes',
];
const DEFAULT_PACKAGE_TYPES = [
  'WorkPackage',
  'TradePackage',
  'SupplyPackage',
  'ProvisionalSum',
];
const DEFAULT_PACKAGE_STATUSES = [
  'Planned',
  'ProcurementPending',
  'Awarded',
  'Active',
  'Completed',
  'OnHold',
  'Cancelled',
];
const DEFAULT_BOQ_ITEM_TYPES = [
  'Item',
  'ProvisionalSum',
  'PrimeCost',
  'Variation',
  'Allowance',
];
const DEFAULT_APPROVAL_TYPES = [
  'PlanningPermission',
  'BuildingPermit',
  'EnvironmentalApproval',
  'FireClearance',
  'UtilityClearance',
  'OccupancyCertificate',
  'Other',
];
const DEFAULT_APPROVAL_STATUSES = [
  'Planned',
  'Submitted',
  'Approved',
  'Rejected',
  'Expired',
  'ConditionallyApproved',
];
const DEFAULT_PROJECT_DRAWING_STATUSES = [
  'Draft',
  'ForReview',
  'ApprovedForConstruction',
  'ApprovedAsBuilt',
  'Superseded',
  'Archived',
];
const DEFAULT_PROJECT_DRAWING_DISCIPLINES = [
  'Architectural',
  'Structural',
  'Mechanical',
  'Electrical',
  'Plumbing',
  'Civil',
  'FireProtection',
  'Interior',
  'Other',
];
const DEFAULT_PROJECT_SUBMITTAL_STATUSES = [
  'Draft',
  'Submitted',
  'UnderReview',
  'Approved',
  'ApprovedWithComments',
  'Rejected',
  'ResubmissionRequired',
  'Closed',
];
const DEFAULT_PROJECT_SUBMITTAL_TYPES = [
  'Material',
  'ShopDrawing',
  'MethodStatement',
  'Sample',
  'TechnicalData',
  'Mockup',
  'Other',
];
const DEFAULT_PROJECT_RFI_STATUSES = [
  'Draft',
  'Submitted',
  'Answered',
  'Closed',
  'Void',
];
const DEFAULT_PROJECT_RFI_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];
const DEFAULT_PROJECT_SITE_INSTRUCTION_STATUSES = [
  'Draft',
  'Issued',
  'Acknowledged',
  'InProgress',
  'Completed',
  'Closed',
  'Cancelled',
];
const DEFAULT_PROJECT_SITE_INSTRUCTION_TYPES = [
  'SiteInstruction',
  'ArchitectInstruction',
  'EngineerInstruction',
  'VariationInstruction',
  'SafetyInstruction',
  'QualityInstruction',
  'Other',
];
const DEFAULT_PROJECT_PHASE_STATUSES = [
  'NotStarted',
  'InProgress',
  'Blocked',
  'Completed',
  'Waived',
  'Cancelled',
];
const DEFAULT_PROJECT_VARIATION_ORDER_STATUSES = [
  'Draft',
  'Submitted',
  'UnderReview',
  'Approved',
  'Rejected',
  'Implemented',
  'Closed',
];
const DEFAULT_PROJECT_VARIATION_ORDER_TYPES = [
  'ScopeChange',
  'QuantityAdjustment',
  'ProvisionalSum',
  'RateChange',
  'Omission',
  'Other',
];
const DEFAULT_PROJECT_INTERIM_VALUATION_STATUSES = [
  'Draft',
  'Submitted',
  'UnderReview',
  'Certified',
  'Paid',
  'Rejected',
];
const DEFAULT_PROJECT_PAYMENT_CERTIFICATE_STATUSES = [
  'Draft',
  'Issued',
  'Approved',
  'Paid',
  'Cancelled',
];
const DEFAULT_PROJECT_EXTENSION_OF_TIME_STATUSES = [
  'Draft',
  'Submitted',
  'UnderReview',
  'Approved',
  'Rejected',
  'Implemented',
  'Closed',
];
const DEFAULT_PROJECT_UNIT_TYPES = [
  'WholeBuilding',
  'Apartment',
  'OfficeSuite',
  'RetailShop',
  'Warehouse',
  'Unit',
];
const DEFAULT_PROJECT_UNIT_STATUSES = [
  'Planned',
  'Available',
  'Reserved',
  'Sold',
  'Leased',
  'HandedOver',
  'Occupied',
  'Archived',
];
const DEFAULT_PROJECT_CUSTOMER_VARIATION_STATUSES = [
  'Requested',
  'UnderReview',
  'Quoted',
  'Approved',
  'Rejected',
  'InProgress',
  'Completed',
  'Billed',
  'Cancelled',
];
const DEFAULT_PROJECT_CUSTOMER_VARIATION_TIMINGS = [
  'PreHandover',
  'PostHandover',
];
const DEFAULT_PROJECT_COMMISSIONING_STATUSES = [
  'Planned',
  'InProgress',
  'ReadyForInspection',
  'Completed',
  'Waived',
];
const DEFAULT_PROJECT_HANDOVER_ITEM_TYPES = [
  'PracticalCompletion',
  'AsBuiltDrawing',
  'OperationManual',
  'KeyHandover',
  'OccupancyCertificate',
  'FinalCompletion',
  'Other',
];
const DEFAULT_PROJECT_HANDOVER_ITEM_STATUSES = [
  'Planned',
  'InPreparation',
  'Ready',
  'Completed',
  'Waived',
];
const DEFAULT_PROJECT_SNAG_STATUSES = [
  'Open',
  'InProgress',
  'ReadyForVerification',
  'Closed',
  'Waived',
];
const DEFAULT_PROJECT_SNAG_SEVERITIES = ['Low', 'Medium', 'High', 'Critical'];
const DEFAULT_PROJECT_DEFECT_LIABILITY_STATUSES = [
  'Reported',
  'UnderReview',
  'InProgress',
  'Resolved',
  'Closed',
  'WarrantyExpired',
];
const TASK_BOARD_STATUSES = [
  'New',
  'Assigned',
  'InProgress',
  'Blocked',
  'PendingReview',
  'Completed',
  'Closed',
];
const MATERIAL_STATUS_ORDER = [1, 2, 3, 4, 5, 6, 7, 8, 9];
const GANTT_DAY_WIDTH = 36;
const GANTT_LEFT_GRID_TEMPLATE = '52px 240px 62px 54px 92px 72px 72px 52px';
const GANTT_LEFT_GRID_WIDTH = 696;
const PROJECT_WORKSPACE_CACHE_TTL_MS = 5 * 60 * 1000;
const USER_ID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

type UserLabelLike = {
  id?: string;
  username?: string;
  firstName?: string;
  lastName?: string;
  displayName?: string;
};

type BusinessPartnerLabelLike = {
  id?: string;
  partnerName?: string;
  partnerType?: string;
  displayName?: string;
};

type ContractLabelLike = {
  id?: string;
  contractNumber?: string;
  contractTitle?: string;
  title?: string;
};

type ProjectWorkspaceCurrencyContext = Awaited<
  ReturnType<typeof loadProjectCurrencyContext>
>;

type ProjectWorkspaceBootstrapData = {
  types: ProjectTypeDto[];
  priorities: ProjectPriorityDto[];
  templates: ProjectTemplateDto[];
  portfolios: ProjectPortfolioDto[];
  businessPartners: BusinessPartnerDto[];
  customerBusinessPartners: BusinessPartnerDto[];
  unitTypeTemplates: ProjectUnitTypeTemplateDto[];
};

type ProjectWorkspaceStaticReferenceData = {
  users: User[];
  contracts: ContractDto[];
  currencyContext: ProjectWorkspaceCurrencyContext;
  unitsOfMeasure: UnitOfMeasureDto[];
  maintenanceAssets: MaintenanceAssetLookupOption[];
  companyAssets: FixedAsset[];
  methodologyCatalog: ProjectCatalogEntryDto[];
  billingTypeCatalog: ProjectCatalogEntryDto[];
  fundingSourceCatalog: ProjectCatalogEntryDto[];
  resourceRoleCatalog: ProjectCatalogEntryDto[];
  memberRoleCatalog: ProjectCatalogEntryDto[];
  taskStatusCatalog: ProjectCatalogEntryDto[];
  taskPriorityCatalog: ProjectCatalogEntryDto[];
  deliverableStatusCatalog: ProjectCatalogEntryDto[];
  riskStatusCatalog: ProjectCatalogEntryDto[];
  riskCategoryCatalog: ProjectCatalogEntryDto[];
  riskResponseStrategyCatalog: ProjectCatalogEntryDto[];
  qualityCheckpointStatusCatalog: ProjectCatalogEntryDto[];
  nonConformanceStatusCatalog: ProjectCatalogEntryDto[];
  nonConformanceSeverityCatalog: ProjectCatalogEntryDto[];
  expenseCategoryCatalog: ProjectCatalogEntryDto[];
  changeTypeCatalog: ProjectCatalogEntryDto[];
  issueStatusCatalog: ProjectCatalogEntryDto[];
  changeStatusCatalog: ProjectCatalogEntryDto[];
  issueSeverityCatalog: ProjectCatalogEntryDto[];
  timesheetWorkTypeCatalog: ProjectCatalogEntryDto[];
  decisionStatusCatalog: ProjectCatalogEntryDto[];
  meetingTypeCatalog: ProjectCatalogEntryDto[];
  actionItemStatusCatalog: ProjectCatalogEntryDto[];
  actionItemPriorityCatalog: ProjectCatalogEntryDto[];
  lessonCategoryCatalog: ProjectCatalogEntryDto[];
  lessonVisibilityCatalog: ProjectCatalogEntryDto[];
  assetLinkTypeCatalog: ProjectCatalogEntryDto[];
  assetLinkStatusCatalog: ProjectCatalogEntryDto[];
  documentCategoryCatalog: ProjectCatalogEntryDto[];
  documentTypeCatalog: ProjectCatalogEntryDto[];
  boqItemTypeCatalog: ProjectCatalogEntryDto[];
};

type TimedCacheEntry<T> = {
  loadedAt: number;
  value: T;
};

let projectWorkspaceBootstrapCache: TimedCacheEntry<ProjectWorkspaceBootstrapData> | null =
  null;
let projectWorkspaceStaticReferenceCache: TimedCacheEntry<ProjectWorkspaceStaticReferenceData> | null =
  null;
const projectWorkspaceProgramsCache = new Map<
  string,
  TimedCacheEntry<ProjectProgramDto[]>
>();

const isWorkspaceCacheFresh = (loadedAt: number) =>
  Date.now() - loadedAt < PROJECT_WORKSPACE_CACHE_TTL_MS;

const normalizeRequisitionStatus = (status: number | string) => {
  if (typeof status === 'number') return status;
  const statusMap: Record<string, number> = {
    Draft: 1,
    Submitted: 2,
    Approved: 3,
    InProgress: 4,
    PartiallyIssued: 5,
    Issued: 6,
    Completed: 7,
    Cancelled: 8,
    Rejected: 9,
  };

  return statusMap[status] ?? 0;
};

const formatCatalogLabel = (value?: string | null) =>
  (value || 'Not set')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[-_]/g, ' ');

const formatUserLabel = (user: UserLabelLike) => {
  const fullName = [user.firstName, user.lastName]
    .filter(Boolean)
    .join(' ')
    .trim();
  const username = user.username?.trim();
  const displayName = user.displayName?.trim();

  if (fullName && username) return `${fullName} (${username})`;
  if (fullName) return fullName;
  if (displayName && username && displayName !== username)
    return `${displayName} (${username})`;
  return displayName || username || user.id || 'Unknown user';
};

const formatBusinessPartnerLabel = (partner: BusinessPartnerLabelLike) =>
  `${partner.partnerName || partner.displayName || partner.id || 'Unknown partner'}${partner.partnerType ? ` (${partner.partnerType})` : ''}`;

const isCustomerBusinessPartner = (
  partner: Pick<BusinessPartnerDto, 'partnerType' | 'customerType'>
) => {
  return (partner.partnerType || '').trim().toLowerCase() === 'customer';
};

const formatContractLabel = (contract: ContractLabelLike) => {
  const contractNumber = contract.contractNumber?.trim();
  const contractTitle =
    contract.contractTitle?.trim() || contract.title?.trim();
  if (contractNumber && contractTitle)
    return `${contractNumber} - ${contractTitle}`;
  return contractNumber || contractTitle || contract.id || 'Unknown contract';
};

const resolveCatalogOptions = (
  entries: ProjectCatalogEntryDto[],
  fallbackValues: string[],
  currentValue?: string
) => {
  const configured = entries
    .filter((entry) => entry.isActive)
    .map((entry) => entry.name.trim())
    .filter(Boolean);

  const values = configured.length > 0 ? configured : fallbackValues;
  return currentValue && !values.includes(currentValue)
    ? [currentValue, ...values]
    : values;
};

const memberInit: AddProjectMemberDto = { userId: '', role: 'Team Member' };
const workInit: CreateProjectWorkItemDto = {
  nodeType: 'Task',
  title: '',
  status: 'New',
  percentComplete: 0,
  isRollupEnabled: true,
};
const milestoneInit: CreateProjectMilestoneDto = {
  title: '',
  targetDate: '',
  status: 'Draft',
  requiresApproval: false,
  projectPhaseIds: [],
};
const resourceInit: CreateProjectResourceAllocationDto = {
  userId: '',
  allocationRole: 'TeamMember',
  allocationType: 'Hours',
  allocationValue: 40,
  plannedHours: 40,
  startDate: today(),
  endDate: today(),
  bookingType: 'Soft',
  status: 'Requested',
  requiredSkills: [],
  requiredCertifications: [],
  routingPolicy: 'Balanced',
};
const riskInit: CreateProjectRiskDto = {
  title: '',
  status: 'Open',
  probability: 1,
  impact: 1,
};
const issueInit: CreateProjectIssueDto = {
  title: '',
  status: 'Open',
  severity: 'Medium',
};
const qualityCheckpointInit: CreateProjectQualityCheckpointDto = {
  title: '',
  status: 'Open',
  requiresQaSignOff: false,
};
const nonConformanceInit: CreateProjectNonConformanceDto = {
  title: '',
  severity: 'Medium',
  status: 'Open',
};
const changeInit: CreateProjectChangeRequestDto = {
  title: '',
  status: 'Draft',
  changeType: 'Scope',
};
const billingInit: CreateProjectBillingScheduleDto = {
  name: '',
  billingType: 'Milestone',
  amount: 0,
  billingDate: today(),
  status: 'Draft',
  isBillable: true,
};
const invoiceInit: CreateProjectInvoiceRequestDto = {
  requestedAmount: 0,
  currency: '',
  status: 'Draft',
};
const docInit: AttachProjectDocumentDto = {
  artifactType: 'Project',
  documentName: '',
  category: 'General',
  documentType: 'Attachment',
  filePath: '',
  versionLabel: '1.0',
  status: 'Active',
  isExternalVisible: false,
};
const commentInit: CreateProjectCommentDto = {
  body: '',
  commentType: 'General',
};
const deliverableInit: CreateProjectDeliverableDto = {
  title: '',
  status: 'Draft',
  externalSubmissionAllowed: false,
  externalSignOffRequired: false,
  isExternalVisible: false,
};
const dependencyInit: CreateProjectTaskDependencyDto = {
  predecessorWorkItemId: '',
  successorWorkItemId: '',
  dependencyType: 'FS',
  lagDays: 0,
  isEnforced: true,
};
const baselineInit: CreateProjectBaselineDto = { name: '', notes: '' };
const timesheetInit: CreateProjectTimesheetEntryDto = {
  userId: '',
  entryDate: today(),
  hours: 8,
  isBillable: false,
  hourlyRate: 0,
  workType: 'Standard',
  notes: '',
};
const expenseInit: CreateProjectExpenseDto = {
  userId: '',
  expenseDate: today(),
  category: 'Other',
  currency: '',
  amount: 0,
  taxAmount: 0,
  isBillable: false,
  notes: '',
};
const budgetRevisionInit: CreateProjectBudgetRevisionDto = {
  revisionName: '',
  revisionType: 'Revision',
  estimatedBudget: 0,
  approvedBudget: 0,
  committedCost: 0,
  forecastCost: 0,
  thresholdWarningPercent: 75,
  thresholdCriticalPercent: 90,
  effectiveDate: today(),
  changeReason: '',
  notes: '',
};
const forecastVersionInit: CreateProjectForecastVersionDto = {
  versionName: '',
  asOfDate: today(),
  forecastCost: 0,
  estimateAtCompletion: 0,
  forecastRevenue: 0,
  forecastMargin: 0,
  isActive: true,
  notes: '',
};
const assetLinkInit: CreateProjectAssetLinkDto = {
  linkType: 'Asset',
  status: 'Linked',
  notes: '',
};
const externalPolicyInit: CreateProjectExternalAccessPolicyDto = {
  businessPartnerId: '',
  artifactType: 'Project',
  accessLevel: 'Read',
  canComment: false,
  canUpload: false,
  canApprove: false,
  notes: '',
};
const decisionInit: CreateProjectDecisionDto = {
  title: '',
  decisionDate: today(),
  status: 'Draft',
  rationale: '',
  alternativesConsidered: '',
  impactSummary: '',
};
const meetingInit: CreateProjectMeetingMinuteDto = {
  title: '',
  meetingDate: today(),
  meetingType: 'Status',
  minutes: '',
  attendeesJson: '',
};
const actionItemInit: CreateProjectActionItemDto = {
  title: '',
  description: '',
  status: 'Open',
  priority: 'Normal',
  dueDate: today(),
};
const lessonLearnedInit: CreateProjectLessonLearnedDto = {
  title: '',
  category: 'General',
  description: '',
  recommendation: '',
  appliedPhase: '',
  visibility: 'Internal',
};
const projectPhaseInit: CreateProjectPhaseDto = {
  name: '',
  description: '',
  code: '',
  status: 'NotStarted',
  sortOrder: 0,
  isOptional: false,
  isStageGateRequired: false,
  completionWeightPercent: 0,
};
const packageInit: CreateProjectPackageDto = {
  name: '',
  packageType: 'WorkPackage',
  status: 'Planned',
  currency: '',
  completionWeightPercent: 0,
};
const boqItemInit: CreateProjectBoqItemDto = {
  projectPackageId: '',
  description: '',
  itemType: 'Item',
  quantity: 1,
  currency: '',
};
const approvalRegisterItemInit: CreateProjectApprovalRegisterItemDto = {
  approvalType: 'BuildingPermit',
  title: '',
  status: 'Planned',
  isRequired: true,
};
const drawingInit: CreateProjectDrawingDto = {
  drawingNumber: '',
  title: '',
  discipline: 'Architectural',
  status: 'Draft',
  isAsBuilt: false,
};
const submittalInit: CreateProjectSubmittalDto = {
  title: '',
  submittalType: 'Material',
  status: 'Draft',
};
const rfiInit: CreateProjectRfiDto = {
  subject: '',
  question: '',
  priority: 'Medium',
  status: 'Draft',
};
const siteInstructionInit: CreateProjectSiteInstructionDto = {
  title: '',
  instructionType: 'SiteInstruction',
  status: 'Draft',
  currency: '',
};
const variationOrderInit: CreateProjectVariationOrderDto = {
  title: '',
  variationType: 'ScopeChange',
  status: 'Draft',
  requestedDate: today(),
  currency: '',
};
const interimValuationInit: CreateProjectInterimValuationDto = {
  title: '',
  status: 'Draft',
  valuationDate: today(),
  grossWorkValue: 0,
  netValuationAmount: 0,
  retentionAmount: 0,
  currency: '',
  completedProjectPackageIds: [],
};
const paymentCertificateInit: CreateProjectPaymentCertificateDto = {
  title: '',
  status: 'Draft',
  issueDate: today(),
  grossCertifiedAmount: 0,
  netCertifiedAmount: 0,
  retentionHeldAmount: 0,
  retentionReleasedAmount: 0,
  otherDeductionsAmount: 0,
  currency: '',
};
const extensionOfTimeInit: CreateProjectExtensionOfTimeDto = {
  title: '',
  status: 'Draft',
  requestedDate: today(),
  daysRequested: 0,
  daysApproved: 0,
};
const unitInit: CreateProjectUnitDto = {
  name: '',
  unitType: 'Unit',
  status: 'Planned',
  currency: '',
  isReleasedForMarket: false,
  amenities: [],
};
const customerVariationInit: CreateProjectCustomerVariationDto = {
  title: '',
  timing: 'PreHandover',
  status: 'Requested',
  currency: '',
  requiresScheduleAdjustment: false,
};
const commissioningInit: CreateProjectCommissioningItemDto = {
  title: '',
  status: 'Planned',
  requiresRegulatoryInspection: false,
};
const handoverItemInit: CreateProjectHandoverItemDto = {
  title: '',
  handoverType: 'PracticalCompletion',
  status: 'Planned',
};
const snagItemInit: CreateProjectSnagItemDto = {
  title: '',
  severity: 'Medium',
  status: 'Open',
};
const defectLiabilityCaseInit: CreateProjectDefectLiabilityCaseDto = {
  title: '',
  status: 'Reported',
  currency: '',
  isWarrantyRelated: true,
};
const closureInit: UpsertProjectClosureDto = {
  finalBudget: 0,
  finalCost: 0,
  deliverablesAccepted: false,
  tasksCompletedOrWaived: false,
  assetsReconciled: false,
  openItemsDisposed: false,
  closureChecklistJson: '',
  openItemsDisposition: '',
  assetReconciliationNotes: '',
  lessonsLearnedSummary: '',
  postImplementationReview: '',
  overrideReason: '',
};

const EMPTY_PROJECT_LINK_OPTIONS: ProjectLinkOptionsDto = {
  salesAgreements: [],
  salesOrders: [],
  jobCards: [],
  workOrders: [],
};

const flatten = (
  items: ProjectWorkItemDto[],
  depth = 0,
  outlinePrefix = ''
): Array<ProjectWorkItemDto & { depth: number; outline: string }> =>
  items.flatMap((x, index) => {
    const outline = outlinePrefix
      ? `${outlinePrefix}.${index + 1}`
      : `${index + 1}`;
    return [
      { ...x, depth, outline },
      ...flatten(x.children || [], depth + 1, outline),
    ];
  });

const formatDateLabel = (value?: string) =>
  value ? format(new Date(value), 'MMM dd, yyyy') : 'N/A';
const boolValue = (value?: boolean | null) => (value ? 'true' : 'false');
const parseTagList = (value: string) =>
  value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
const formatTrackerHours = (hours?: number) => {
  if (!hours || hours <= 0) return '-';
  const wholeHours = Math.floor(hours);
  const minutes = Math.round((hours - wholeHours) * 60);
  if (wholeHours === 0) return `${minutes}m`;
  if (minutes === 0) return `${wholeHours}h`;
  return `${wholeHours}h ${minutes}m`;
};
const getUserInitials = (label?: string) =>
  (label || '?')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() || '')
    .join('') || '?';
const getGanttStatusTone = (item: ProjectWorkItemDto) => {
  if (item.nodeType === 'Phase') return 'bg-sky-600';
  if (item.nodeType === 'Workstream') return 'bg-emerald-600';

  switch ((item.status || '').toLowerCase()) {
    case 'completed':
    case 'closed':
      return 'bg-slate-500';
    case 'blocked':
    case 'onhold':
      return 'bg-amber-500';
    case 'pendingreview':
      return 'bg-indigo-500';
    case 'inprogress':
    case 'assigned':
      return 'bg-cyan-600';
    default:
      return 'bg-zinc-400';
  }
};
const getGanttStrokeTone = (item: ProjectWorkItemDto) => {
  if (item.nodeType === 'Phase') return '#0284c7';
  if (item.nodeType === 'Workstream') return '#059669';

  switch ((item.status || '').toLowerCase()) {
    case 'completed':
    case 'closed':
      return '#64748b';
    case 'blocked':
    case 'onhold':
      return '#d97706';
    case 'pendingreview':
      return '#6366f1';
    case 'inprogress':
    case 'assigned':
      return '#0891b2';
    default:
      return '#71717a';
  }
};
const getGanttPalette = (item: ProjectWorkItemDto) => {
  if (item.nodeType === 'Phase') {
    return {
      start: '#0ea5e9',
      end: '#2563eb',
      soft: '#e0f2fe',
      border: '#0284c7',
      progress: '#082f49',
      text: '#ffffff',
    };
  }

  if (item.nodeType === 'Workstream') {
    return {
      start: '#22c55e',
      end: '#059669',
      soft: '#dcfce7',
      border: '#059669',
      progress: '#064e3b',
      text: '#ffffff',
    };
  }

  switch ((item.status || '').toLowerCase()) {
    case 'completed':
    case 'closed':
      return {
        start: '#94a3b8',
        end: '#64748b',
        soft: '#e2e8f0',
        border: '#64748b',
        progress: '#334155',
        text: '#ffffff',
      };
    case 'blocked':
    case 'onhold':
      return {
        start: '#f59e0b',
        end: '#d97706',
        soft: '#fef3c7',
        border: '#d97706',
        progress: '#78350f',
        text: '#ffffff',
      };
    case 'pendingreview':
      return {
        start: '#8b5cf6',
        end: '#6366f1',
        soft: '#ede9fe',
        border: '#6366f1',
        progress: '#312e81',
        text: '#ffffff',
      };
    case 'inprogress':
    case 'assigned':
      return {
        start: '#06b6d4',
        end: '#0284c7',
        soft: '#cffafe',
        border: '#0891b2',
        progress: '#164e63',
        text: '#ffffff',
      };
    default:
      return {
        start: '#a78bfa',
        end: '#8b5cf6',
        soft: '#f3e8ff',
        border: '#8b5cf6',
        progress: '#581c87',
        text: '#ffffff',
      };
  }
};

const getPlannerStatusBadgeStyle = (
  item: Pick<ProjectWorkItemDto, 'nodeType' | 'status'>
) => {
  const palette = getGanttPalette(item as ProjectWorkItemDto);
  return {
    backgroundColor:
      item.nodeType === 'Phase' || item.nodeType === 'Workstream'
        ? palette.soft
        : `${palette.soft}CC`,
    borderColor: palette.border,
    color: palette.border,
  };
};
const getTaskNameStyle = (
  item: Pick<ProjectWorkItemDto, 'nodeType' | 'status'>
) => {
  const palette = getGanttPalette(item as ProjectWorkItemDto);
  return {
    backgroundColor: `${palette.soft}D9`,
    borderColor: `${palette.border}33`,
    color: palette.border,
  };
};
const isWorkItemOverdue = (
  item: Pick<ProjectWorkItemDto, 'plannedEndDate' | 'status'>
) => {
  if (!item.plannedEndDate) return false;
  const status = (item.status || '').toLowerCase();
  if (['completed', 'closed', 'cancelled'].includes(status)) return false;
  return (
    new Date(item.plannedEndDate).setHours(0, 0, 0, 0) <
    new Date().setHours(0, 0, 0, 0)
  );
};
const normalizeDateInputValue = (value?: string | Date | null) =>
  value ? String(value).slice(0, 10) : '';
const formatBaselineVarianceLabel = (days: number) => {
  if (days === 0) return 'On baseline';
  return days > 0 ? `+${days}d variance` : `${days}d variance`;
};

export default function ProjectWorkspacePage({
  initialTab = 'overview',
}: {
  initialTab?: ProjectWorkspaceTab;
}) {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const [, startTabTransition] = useTransition();
  const id = params?.id;
  const [activeTab, setActiveTab] = useState<ProjectWorkspaceTab>(initialTab);
  const navigateToTab = (tab: string) => {
    const nextTab = tab as ProjectWorkspaceTab;
    if (!id || nextTab === activeTab) {
      return;
    }

    setActiveTab(nextTab);
    startTabTransition(() => {
      router.push(`/development/projects/${id}/${nextTab}`, { scroll: false });
    });
  };
  const ganttChartScrollRef = useRef<HTMLDivElement | null>(null);
  const ganttBottomScrollRef = useRef<HTMLDivElement | null>(null);
  const ganttScrollSyncRef = useRef(false);
  const [project, setProject] = useState<ProjectDetailDto | null>(null);
  const [types, setTypes] = useState<ProjectTypeDto[]>([]);
  const [priorities, setPriorities] = useState<ProjectPriorityDto[]>([]);
  const [templates, setTemplates] = useState<ProjectTemplateDto[]>([]);
  const [unitTypeTemplates, setUnitTypeTemplates] = useState<
    ProjectUnitTypeTemplateDto[]
  >([]);
  const [portfolios, setPortfolios] = useState<ProjectPortfolioDto[]>([]);
  const [programs, setPrograms] = useState<ProjectProgramDto[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [baseCurrency, setBaseCurrency] = useState<ProjectCurrencyReference>(
    DEFAULT_PROJECT_CURRENCY
  );
  const [businessPartners, setBusinessPartners] = useState<
    BusinessPartnerDto[]
  >([]);
  const [customerPartnerOptions, setCustomerPartnerOptions] = useState<
    BusinessPartnerDto[]
  >([]);
  const [contracts, setContracts] = useState<ContractDto[]>([]);
  const [tenderLookup, setTenderLookup] = useState<ProjectTenderLookupDto[]>(
    []
  );
  const [procurementPlanItemLookup, setProcurementPlanItemLookup] = useState<
    ProjectProcurementPlanItemLookupDto[]
  >([]);
  const [purchaseRequisitionLookup, setPurchaseRequisitionLookup] = useState<
    ProjectPurchaseRequisitionLookupDto[]
  >([]);
  const [purchaseOrderLookup, setPurchaseOrderLookup] = useState<
    ProjectPurchaseOrderLookupDto[]
  >([]);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemDto[]>([]);
  const [maintenanceAssets, setMaintenanceAssets] = useState<
    MaintenanceAssetLookupOption[]
  >([]);
  const [companyAssets, setCompanyAssets] = useState<FixedAsset[]>([]);
  const [methodologyCatalog, setMethodologyCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [billingTypeCatalog, setBillingTypeCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [fundingSourceCatalog, setFundingSourceCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [resourceRoleCatalog, setResourceRoleCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [memberRoleCatalog, setMemberRoleCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [taskStatusCatalog, setTaskStatusCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [taskPriorityCatalog, setTaskPriorityCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [deliverableStatusCatalog, setDeliverableStatusCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [riskStatusCatalog, setRiskStatusCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [riskCategoryCatalog, setRiskCategoryCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [riskResponseStrategyCatalog, setRiskResponseStrategyCatalog] =
    useState<ProjectCatalogEntryDto[]>([]);
  const [qualityCheckpointStatusCatalog, setQualityCheckpointStatusCatalog] =
    useState<ProjectCatalogEntryDto[]>([]);
  const [nonConformanceStatusCatalog, setNonConformanceStatusCatalog] =
    useState<ProjectCatalogEntryDto[]>([]);
  const [nonConformanceSeverityCatalog, setNonConformanceSeverityCatalog] =
    useState<ProjectCatalogEntryDto[]>([]);
  const [expenseCategoryCatalog, setExpenseCategoryCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [changeTypeCatalog, setChangeTypeCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [issueStatusCatalog, setIssueStatusCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [changeStatusCatalog, setChangeStatusCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [issueSeverityCatalog, setIssueSeverityCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [timesheetWorkTypeCatalog, setTimesheetWorkTypeCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [decisionStatusCatalog, setDecisionStatusCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [meetingTypeCatalog, setMeetingTypeCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [actionItemStatusCatalog, setActionItemStatusCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [actionItemPriorityCatalog, setActionItemPriorityCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [lessonCategoryCatalog, setLessonCategoryCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [lessonVisibilityCatalog, setLessonVisibilityCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [assetLinkTypeCatalog, setAssetLinkTypeCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [assetLinkStatusCatalog, setAssetLinkStatusCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [documentCategoryCatalog, setDocumentCategoryCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [documentTypeCatalog, setDocumentTypeCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [boqItemTypeCatalog, setBoqItemTypeCatalog] = useState<
    ProjectCatalogEntryDto[]
  >([]);
  const [overview, setOverview] = useState<UpdateProjectDto>({ title: '' });
  const [member, setMember] = useState(memberInit);
  const [work, setWork] = useState(workInit);
  const [milestone, setMilestone] = useState(milestoneInit);
  const [resource, setResource] = useState(resourceInit);
  const [risk, setRisk] = useState(riskInit);
  const [issue, setIssue] = useState(issueInit);
  const [qualityCheckpoint, setQualityCheckpoint] =
    useState<CreateProjectQualityCheckpointDto>(qualityCheckpointInit);
  const [nonConformance, setNonConformance] =
    useState<CreateProjectNonConformanceDto>(nonConformanceInit);
  const [change, setChange] = useState(changeInit);
  const [billing, setBilling] = useState(billingInit);
  const [invoice, setInvoice] = useState(invoiceInit);
  const [doc, setDoc] = useState(docInit);
  const [docFile, setDocFile] = useState<File | null>(null);
  const [comment, setComment] = useState(commentInit);
  const [deliverable, setDeliverable] = useState(deliverableInit);
  const [dependency, setDependency] = useState(dependencyInit);
  const [baseline, setBaseline] = useState(baselineInit);
  const [timesheet, setTimesheet] = useState(timesheetInit);
  const [expense, setExpense] = useState(expenseInit);
  const [budgetRevision, setBudgetRevision] =
    useState<CreateProjectBudgetRevisionDto>(budgetRevisionInit);
  const [forecastVersion, setForecastVersion] =
    useState<CreateProjectForecastVersionDto>(forecastVersionInit);
  const [assetLink, setAssetLink] = useState(assetLinkInit);
  const [externalPolicy, setExternalPolicy] = useState(externalPolicyInit);
  const [decision, setDecision] =
    useState<CreateProjectDecisionDto>(decisionInit);
  const [meeting, setMeeting] =
    useState<CreateProjectMeetingMinuteDto>(meetingInit);
  const [actionItem, setActionItem] =
    useState<CreateProjectActionItemDto>(actionItemInit);
  const [lessonLearned, setLessonLearned] =
    useState<CreateProjectLessonLearnedDto>(lessonLearnedInit);
  const [projectPhaseDraft, setProjectPhaseDraft] =
    useState<CreateProjectPhaseDto>(projectPhaseInit);
  const [projectPackageDraft, setProjectPackageDraft] =
    useState<CreateProjectPackageDto>(packageInit);
  const [boqItemDraft, setBoqItemDraft] =
    useState<CreateProjectBoqItemDto>(boqItemInit);
  const [approvalDraft, setApprovalDraft] =
    useState<CreateProjectApprovalRegisterItemDto>(approvalRegisterItemInit);
  const [drawingDraft, setDrawingDraft] =
    useState<CreateProjectDrawingDto>(drawingInit);
  const [submittalDraft, setSubmittalDraft] =
    useState<CreateProjectSubmittalDto>(submittalInit);
  const [rfiDraft, setRfiDraft] = useState<CreateProjectRfiDto>(rfiInit);
  const [siteInstructionDraft, setSiteInstructionDraft] =
    useState<CreateProjectSiteInstructionDto>(siteInstructionInit);
  const [variationOrderDraft, setVariationOrderDraft] =
    useState<CreateProjectVariationOrderDto>(variationOrderInit);
  const [interimValuationDraft, setInterimValuationDraft] =
    useState<CreateProjectInterimValuationDto>(interimValuationInit);
  const [paymentCertificateDraft, setPaymentCertificateDraft] =
    useState<CreateProjectPaymentCertificateDto>(paymentCertificateInit);
  const [extensionOfTimeDraft, setExtensionOfTimeDraft] =
    useState<CreateProjectExtensionOfTimeDto>(extensionOfTimeInit);
  const [unitDraft, setUnitDraft] = useState<CreateProjectUnitDto>(unitInit);
  const [customerVariationDraft, setCustomerVariationDraft] =
    useState<CreateProjectCustomerVariationDto>(customerVariationInit);
  const [commissioningDraft, setCommissioningDraft] =
    useState<CreateProjectCommissioningItemDto>(commissioningInit);
  const [handoverDraft, setHandoverDraft] =
    useState<CreateProjectHandoverItemDto>(handoverItemInit);
  const [snagDraft, setSnagDraft] =
    useState<CreateProjectSnagItemDto>(snagItemInit);
  const [defectLiabilityDraft, setDefectLiabilityDraft] =
    useState<CreateProjectDefectLiabilityCaseDto>(defectLiabilityCaseInit);
  const [closure, setClosure] = useState<UpsertProjectClosureDto>(closureInit);
  const [aiInsights, setAiInsights] = useState<ProjectAiInsightDto[]>([]);
  const [scheduleAnalysis, setScheduleAnalysis] =
    useState<ProjectScheduleAnalysisDto | null>(null);
  const [baselineComparison, setBaselineComparison] =
    useState<ProjectBaselineComparisonDto | null>(null);
  const [financialSummary, setFinancialSummary] =
    useState<ProjectFinancialControlSummaryDto | null>(null);
  const [commercialSummary, setCommercialSummary] =
    useState<ProjectCommercialSummaryDto | null>(null);
  const [postHandoverSummary, setPostHandoverSummary] =
    useState<ProjectPostHandoverSummaryDto | null>(null);
  const [budgetRevisions, setBudgetRevisions] = useState<
    ProjectBudgetRevisionDto[]
  >([]);
  const [forecastVersions, setForecastVersions] = useState<
    ProjectForecastVersionDto[]
  >([]);
  const [integrationSummary, setIntegrationSummary] =
    useState<ProjectIntegrationSummaryDto | null>(null);
  const [governanceSummary, setGovernanceSummary] =
    useState<ProjectGovernanceSummaryDto | null>(null);
  const [projectLinkOptions, setProjectLinkOptions] =
    useState<ProjectLinkOptionsDto>(EMPTY_PROJECT_LINK_OPTIONS);
  const [phaseGateEvaluations, setPhaseGateEvaluations] = useState<
    ProjectPhaseGateEvaluationDto[]
  >([]);
  const [materialRequisitions, setMaterialRequisitions] = useState<
    InventoryRequisitionDto[]
  >([]);
  const [materialWarehouses, setMaterialWarehouses] = useState<WarehouseDto[]>(
    []
  );
  const [unitsOfMeasure, setUnitsOfMeasure] = useState<UnitOfMeasureDto[]>([]);
  const [materialDialogOpen, setMaterialDialogOpen] = useState(false);
  const [materialDialogMode, setMaterialDialogMode] = useState<
    'create' | 'edit' | 'view'
  >('create');
  const [selectedMaterialRequisitionId, setSelectedMaterialRequisitionId] =
    useState<string | undefined>();
  const [materialIssueDialogOpen, setMaterialIssueDialogOpen] = useState(false);
  const [materialIssueRequisitionId, setMaterialIssueRequisitionId] = useState<
    string | null
  >(null);
  const [materialReturnDialogOpen, setMaterialReturnDialogOpen] =
    useState(false);
  const [materialReturnRequisitionId, setMaterialReturnRequisitionId] =
    useState<string | null>(null);
  const [expandedBudgetRevisionHistoryId, setExpandedBudgetRevisionHistoryId] =
    useState<string | null>(null);
  const [expandedDeliverableHistoryId, setExpandedDeliverableHistoryId] =
    useState<string | null>(null);
  const [followThroughBusyKey, setFollowThroughBusyKey] = useState<
    string | null
  >(null);
  const [loading, setLoading] = useState(true);
  const [analysisLoaded, setAnalysisLoaded] = useState(false);
  const [materialsLoaded, setMaterialsLoaded] = useState(false);
  const [packageLookupsLoaded, setPackageLookupsLoaded] = useState(false);
  const [referenceDataLoaded, setReferenceDataLoaded] = useState(false);
  const workspaceTabs = useMemo(
    () =>
      getProjectWorkspaceTabs({
        deliveryStructure: project?.developmentProfile?.deliveryStructure,
        developmentType: project?.developmentProfile?.developmentType,
      }),
    [
      project?.developmentProfile?.deliveryStructure,
      project?.developmentProfile?.developmentType,
    ]
  );
  const [taskView, setTaskView] = useState<'tree' | 'kanban' | 'timeline'>(
    'tree'
  );
  const [ganttDialogOpen, setGanttDialogOpen] = useState(false);
  const [collapsedGanttItems, setCollapsedGanttItems] = useState<string[]>([]);
  const [hoveredGanttItemId, setHoveredGanttItemId] = useState<string | null>(
    null
  );
  const [editingWorkItemId, setEditingWorkItemId] = useState<string | null>(
    null
  );
  const [editingProjectPhaseId, setEditingProjectPhaseId] = useState<
    string | null
  >(null);
  const [editingProjectPackageId, setEditingProjectPackageId] = useState<
    string | null
  >(null);
  const [editingProjectBoqItemId, setEditingProjectBoqItemId] = useState<
    string | null
  >(null);
  const [editingVariationOrderId, setEditingVariationOrderId] = useState<
    string | null
  >(null);
  const [editingInterimValuationId, setEditingInterimValuationId] = useState<
    string | null
  >(null);
  const [editingPaymentCertificateId, setEditingPaymentCertificateId] =
    useState<string | null>(null);
  const [editingExtensionOfTimeId, setEditingExtensionOfTimeId] = useState<
    string | null
  >(null);
  const [editingUnitId, setEditingUnitId] = useState<string | null>(null);
  const [editingCustomerVariationId, setEditingCustomerVariationId] = useState<
    string | null
  >(null);
  const [editingSnagItemId, setEditingSnagItemId] = useState<string | null>(
    null
  );
  const [editingDefectLiabilityCaseId, setEditingDefectLiabilityCaseId] =
    useState<string | null>(null);
  const [ganttQuickFilters, setGanttQuickFilters] = useState({
    overdue: false,
    offBaseline: false,
    assignedToMe: false,
  });

  const closureRecord: ProjectClosureDto | null = project?.closure ?? null;

  const currentUserId = useMemo(() => {
    if (typeof window === 'undefined') return '';
    try {
      const raw = localStorage.getItem('user');
      if (!raw) return '';
      const parsed = JSON.parse(raw);
      return parsed?.id || parsed?.userId || '';
    } catch {
      return '';
    }
  }, []);

  useEffect(() => {
    setActiveTab(initialTab);
  }, [initialTab]);

  useEffect(() => {
    const projectCurrencyCode = (
      overview.baseCurrencyCode || project?.baseCurrencyCode
    )?.trim();
    if (projectCurrencyCode) {
      const resolvedProjectCurrency = findProjectCurrency(
        currencies,
        projectCurrencyCode,
        DEFAULT_PROJECT_CURRENCY
      ) ?? {
        code: projectCurrencyCode,
        name: projectCurrencyCode,
        symbol: '',
        decimalPlaces: DEFAULT_PROJECT_CURRENCY.decimalPlaces,
      };
      setBaseCurrency(
        resolveProjectBaseCurrency(resolvedProjectCurrency, currencies)
      );
      return;
    }

    if (currencies.length > 0) {
      setBaseCurrency(resolveProjectBaseCurrency(undefined, currencies));
    }
  }, [overview.baseCurrencyCode, project?.baseCurrencyCode, currencies]);

  useEffect(() => {
    if (!id) {
      return;
    }

    workspaceTabs.forEach((tab) => {
      if (tab !== activeTab) {
        router.prefetch(`/development/projects/${id}/${tab}`);
      }
    });
  }, [activeTab, id, router, workspaceTabs]);

  const loadAnalysisData = async (projectId: string) => {
    const [
      insightsResult,
      analysisResult,
      budgetRevisionResult,
      forecastVersionResult,
    ] = await Promise.allSettled([
      projectService.getAiInsights(projectId),
      projectService.analyzeSchedule(projectId),
      projectService.getBudgetRevisions(projectId),
      projectService.getForecastVersions(projectId),
    ]);

    setAiInsights(
      insightsResult.status === 'fulfilled' ? insightsResult.value : []
    );
    setScheduleAnalysis(
      analysisResult.status === 'fulfilled' ? analysisResult.value : null
    );
    setBudgetRevisions(
      budgetRevisionResult.status === 'fulfilled'
        ? budgetRevisionResult.value
        : []
    );
    setForecastVersions(
      forecastVersionResult.status === 'fulfilled'
        ? forecastVersionResult.value
        : []
    );
    setAnalysisLoaded(true);
  };

  const loadMaterials = async (projectId: string) => {
    const [requisitionResult, warehouseResult] = await Promise.allSettled([
      inventoryRequisitionService.getByProject(projectId),
      inventoryManagementService.getWarehouses(),
    ]);

    setMaterialRequisitions(
      requisitionResult.status === 'fulfilled'
        ? [...requisitionResult.value].sort(
            (left, right) =>
              new Date(right.requestDate).getTime() -
              new Date(left.requestDate).getTime()
          )
        : []
    );
    setMaterialWarehouses(
      warehouseResult.status === 'fulfilled' ? warehouseResult.value : []
    );
    setMaterialsLoaded(true);
  };

  const loadReferenceData = async (portfolioId?: string) => {
    let staticReferenceData = projectWorkspaceStaticReferenceCache?.value;
    if (
      !projectWorkspaceStaticReferenceCache ||
      !isWorkspaceCacheFresh(projectWorkspaceStaticReferenceCache.loadedAt)
    ) {
      const [
        loadedUsers,
        loadedContracts,
        currencyContext,
        loadedUnitsOfMeasure,
        loadedMaintenanceAssets,
        loadedCompanyAssets,
        catalogResults,
      ] = await Promise.all([
        userService.searchUsers('').catch(() => []),
        contractService.getActiveContracts().catch(() => []),
        loadProjectCurrencyContext(),
        inventoryManagementService.getUnitsOfMeasure(true).catch(() => []),
        maintenanceDataService.getAssets().catch(() => []),
        fixedAssetsDataService.getAssets().catch(() => []),
        Promise.allSettled([
          projectService.getCatalogEntries('methodologies'),
          projectService.getCatalogEntries('billing-types'),
          projectService.getCatalogEntries('funding-sources'),
          projectService.getCatalogEntries('resource-roles'),
          projectService.getCatalogEntries('member-roles'),
          projectService.getCatalogEntries('task-statuses'),
          projectService.getCatalogEntries('task-priorities'),
          projectService.getCatalogEntries('deliverable-statuses'),
          projectService.getCatalogEntries('risk-statuses'),
          projectService.getCatalogEntries('risk-categories'),
          projectService.getCatalogEntries('risk-response-strategies'),
          projectService.getCatalogEntries('quality-checkpoint-statuses'),
          projectService.getCatalogEntries('non-conformance-statuses'),
          projectService.getCatalogEntries('non-conformance-severities'),
          projectService.getCatalogEntries('expense-categories'),
          projectService.getCatalogEntries('change-categories'),
          projectService.getCatalogEntries('issue-statuses'),
          projectService.getCatalogEntries('change-statuses'),
          projectService.getCatalogEntries('issue-severities'),
          projectService.getCatalogEntries('timesheet-work-types'),
          projectService.getCatalogEntries('decision-statuses'),
          projectService.getCatalogEntries('meeting-types'),
          projectService.getCatalogEntries('action-item-statuses'),
          projectService.getCatalogEntries('action-item-priorities'),
          projectService.getCatalogEntries('lesson-categories'),
          projectService.getCatalogEntries('lesson-visibility-levels'),
          projectService.getCatalogEntries('asset-link-types'),
          projectService.getCatalogEntries('asset-link-statuses'),
          projectService.getCatalogEntries('document-categories'),
          projectService.getCatalogEntries('document-types'),
          projectService.getCatalogEntries('boq-item-types'),
        ]),
      ]);

      staticReferenceData = {
        users: loadedUsers,
        contracts: loadedContracts,
        currencyContext,
        unitsOfMeasure: loadedUnitsOfMeasure,
        maintenanceAssets: loadedMaintenanceAssets,
        companyAssets: loadedCompanyAssets,
        methodologyCatalog:
          catalogResults[0].status === 'fulfilled'
            ? catalogResults[0].value
            : [],
        billingTypeCatalog:
          catalogResults[1].status === 'fulfilled'
            ? catalogResults[1].value
            : [],
        fundingSourceCatalog:
          catalogResults[2].status === 'fulfilled'
            ? catalogResults[2].value
            : [],
        resourceRoleCatalog:
          catalogResults[3].status === 'fulfilled'
            ? catalogResults[3].value
            : [],
        memberRoleCatalog:
          catalogResults[4].status === 'fulfilled'
            ? catalogResults[4].value
            : [],
        taskStatusCatalog:
          catalogResults[5].status === 'fulfilled'
            ? catalogResults[5].value
            : [],
        taskPriorityCatalog:
          catalogResults[6].status === 'fulfilled'
            ? catalogResults[6].value
            : [],
        deliverableStatusCatalog:
          catalogResults[7].status === 'fulfilled'
            ? catalogResults[7].value
            : [],
        riskStatusCatalog:
          catalogResults[8].status === 'fulfilled'
            ? catalogResults[8].value
            : [],
        riskCategoryCatalog:
          catalogResults[9].status === 'fulfilled'
            ? catalogResults[9].value
            : [],
        riskResponseStrategyCatalog:
          catalogResults[10].status === 'fulfilled'
            ? catalogResults[10].value
            : [],
        qualityCheckpointStatusCatalog:
          catalogResults[11].status === 'fulfilled'
            ? catalogResults[11].value
            : [],
        nonConformanceStatusCatalog:
          catalogResults[12].status === 'fulfilled'
            ? catalogResults[12].value
            : [],
        nonConformanceSeverityCatalog:
          catalogResults[13].status === 'fulfilled'
            ? catalogResults[13].value
            : [],
        expenseCategoryCatalog:
          catalogResults[14].status === 'fulfilled'
            ? catalogResults[14].value
            : [],
        changeTypeCatalog:
          catalogResults[15].status === 'fulfilled'
            ? catalogResults[15].value
            : [],
        issueStatusCatalog:
          catalogResults[16].status === 'fulfilled'
            ? catalogResults[16].value
            : [],
        changeStatusCatalog:
          catalogResults[17].status === 'fulfilled'
            ? catalogResults[17].value
            : [],
        issueSeverityCatalog:
          catalogResults[18].status === 'fulfilled'
            ? catalogResults[18].value
            : [],
        timesheetWorkTypeCatalog:
          catalogResults[19].status === 'fulfilled'
            ? catalogResults[19].value
            : [],
        decisionStatusCatalog:
          catalogResults[20].status === 'fulfilled'
            ? catalogResults[20].value
            : [],
        meetingTypeCatalog:
          catalogResults[21].status === 'fulfilled'
            ? catalogResults[21].value
            : [],
        actionItemStatusCatalog:
          catalogResults[22].status === 'fulfilled'
            ? catalogResults[22].value
            : [],
        actionItemPriorityCatalog:
          catalogResults[23].status === 'fulfilled'
            ? catalogResults[23].value
            : [],
        lessonCategoryCatalog:
          catalogResults[24].status === 'fulfilled'
            ? catalogResults[24].value
            : [],
        lessonVisibilityCatalog:
          catalogResults[25].status === 'fulfilled'
            ? catalogResults[25].value
            : [],
        assetLinkTypeCatalog:
          catalogResults[26].status === 'fulfilled'
            ? catalogResults[26].value
            : [],
        assetLinkStatusCatalog:
          catalogResults[27].status === 'fulfilled'
            ? catalogResults[27].value
            : [],
        documentCategoryCatalog:
          catalogResults[28].status === 'fulfilled'
            ? catalogResults[28].value
            : [],
        documentTypeCatalog:
          catalogResults[29].status === 'fulfilled'
            ? catalogResults[29].value
            : [],
        boqItemTypeCatalog:
          catalogResults[30].status === 'fulfilled'
            ? catalogResults[30].value
            : [],
      };

      projectWorkspaceStaticReferenceCache = {
        loadedAt: Date.now(),
        value: staticReferenceData,
      };
    }

    const normalizedPortfolioId = portfolioId?.trim();
    let loadedPrograms: ProjectProgramDto[] = [];
    if (normalizedPortfolioId) {
      const cachedPrograms = projectWorkspaceProgramsCache.get(
        normalizedPortfolioId
      );
      if (cachedPrograms && isWorkspaceCacheFresh(cachedPrograms.loadedAt)) {
        loadedPrograms = cachedPrograms.value;
      } else {
        loadedPrograms = await projectService
          .getPrograms(normalizedPortfolioId)
          .catch(() => []);
        projectWorkspaceProgramsCache.set(normalizedPortfolioId, {
          loadedAt: Date.now(),
          value: loadedPrograms,
        });
      }
    }

    if (!staticReferenceData) {
      setReferenceDataLoaded(true);
      return;
    }

    setUsers(staticReferenceData.users);
    setContracts(staticReferenceData.contracts);
    setCurrencies(staticReferenceData.currencyContext.activeCurrencies);
    setBaseCurrency(staticReferenceData.currencyContext.baseCurrency);
    setUnitsOfMeasure(staticReferenceData.unitsOfMeasure);
    setMaintenanceAssets(staticReferenceData.maintenanceAssets);
    setCompanyAssets(staticReferenceData.companyAssets);
    setMethodologyCatalog(staticReferenceData.methodologyCatalog);
    setBillingTypeCatalog(staticReferenceData.billingTypeCatalog);
    setFundingSourceCatalog(staticReferenceData.fundingSourceCatalog);
    setResourceRoleCatalog(staticReferenceData.resourceRoleCatalog);
    setMemberRoleCatalog(staticReferenceData.memberRoleCatalog);
    setTaskStatusCatalog(staticReferenceData.taskStatusCatalog);
    setTaskPriorityCatalog(staticReferenceData.taskPriorityCatalog);
    setDeliverableStatusCatalog(staticReferenceData.deliverableStatusCatalog);
    setRiskStatusCatalog(staticReferenceData.riskStatusCatalog);
    setRiskCategoryCatalog(staticReferenceData.riskCategoryCatalog);
    setRiskResponseStrategyCatalog(
      staticReferenceData.riskResponseStrategyCatalog
    );
    setQualityCheckpointStatusCatalog(
      staticReferenceData.qualityCheckpointStatusCatalog
    );
    setNonConformanceStatusCatalog(
      staticReferenceData.nonConformanceStatusCatalog
    );
    setNonConformanceSeverityCatalog(
      staticReferenceData.nonConformanceSeverityCatalog
    );
    setExpenseCategoryCatalog(staticReferenceData.expenseCategoryCatalog);
    setChangeTypeCatalog(staticReferenceData.changeTypeCatalog);
    setIssueStatusCatalog(staticReferenceData.issueStatusCatalog);
    setChangeStatusCatalog(staticReferenceData.changeStatusCatalog);
    setIssueSeverityCatalog(staticReferenceData.issueSeverityCatalog);
    setTimesheetWorkTypeCatalog(staticReferenceData.timesheetWorkTypeCatalog);
    setDecisionStatusCatalog(staticReferenceData.decisionStatusCatalog);
    setMeetingTypeCatalog(staticReferenceData.meetingTypeCatalog);
    setActionItemStatusCatalog(staticReferenceData.actionItemStatusCatalog);
    setActionItemPriorityCatalog(staticReferenceData.actionItemPriorityCatalog);
    setLessonCategoryCatalog(staticReferenceData.lessonCategoryCatalog);
    setLessonVisibilityCatalog(staticReferenceData.lessonVisibilityCatalog);
    setAssetLinkTypeCatalog(staticReferenceData.assetLinkTypeCatalog);
    setAssetLinkStatusCatalog(staticReferenceData.assetLinkStatusCatalog);
    setDocumentCategoryCatalog(staticReferenceData.documentCategoryCatalog);
    setDocumentTypeCatalog(staticReferenceData.documentTypeCatalog);
    setBoqItemTypeCatalog(staticReferenceData.boqItemTypeCatalog);
    setPrograms(loadedPrograms);
    setReferenceDataLoaded(true);
  };

  const loadPackageLookups = async (projectId: string) => {
    const [
      loadedTenders,
      loadedPlanItems,
      loadedPurchaseRequisitions,
      loadedPurchaseOrders,
      loadedInventoryItems,
    ] = await Promise.all([
      projectService.getTenderLookup().catch(() => []),
      projectService.getProcurementPlanItemLookup(projectId).catch(() => []),
      projectService.getPurchaseRequisitionLookup(projectId).catch(() => []),
      projectService.getPurchaseOrderLookup(projectId).catch(() => []),
      inventoryManagementService
        .getInventoryItems({ isActive: true })
        .catch(() => []),
    ]);

    setTenderLookup(loadedTenders);
    setProcurementPlanItemLookup(loadedPlanItems);
    setPurchaseRequisitionLookup(loadedPurchaseRequisitions);
    setPurchaseOrderLookup(loadedPurchaseOrders);
    setInventoryItems(loadedInventoryItems);
    setPackageLookupsLoaded(true);
  };

  const load = async () => {
    if (!id) return;
    try {
      setLoading(true);
      setReferenceDataLoaded(false);
      const workspacePromise = projectService.getProjectWorkspaceById(id);
      const bootstrapPromise =
        projectWorkspaceBootstrapCache &&
        isWorkspaceCacheFresh(projectWorkspaceBootstrapCache.loadedAt)
          ? Promise.resolve(projectWorkspaceBootstrapCache.value)
          : Promise.all([
              projectService.getProjectTypes().catch(() => []),
              projectService.getProjectPriorities().catch(() => []),
              projectService.getProjectTemplates().catch(() => []),
              projectService.getPortfolios().catch(() => []),
              businessPartnerService.getAllPartnersForDropdown(),
              businessPartnerService
                .getActivePartners('Customer')
                .then((partners) =>
                  partners.filter((partner) =>
                    isCustomerBusinessPartner(partner)
                  )
                )
                .catch(() =>
                  businessPartnerService
                    .getAllPartnersForDropdown()
                    .then((partners) =>
                      partners.filter((partner) =>
                        isCustomerBusinessPartner(partner)
                      )
                    )
                    .catch(() => [])
                ),
              projectService.getProjectUnitTypeTemplates().catch(() => []),
            ]).then(
              ([
                types,
                priorities,
                templates,
                portfolios,
                businessPartners,
                customerBusinessPartners,
                unitTypeTemplates,
              ]) => {
                const bootstrapData: ProjectWorkspaceBootstrapData = {
                  types,
                  priorities,
                  templates,
                  portfolios,
                  businessPartners,
                  customerBusinessPartners,
                  unitTypeTemplates,
                };
                projectWorkspaceBootstrapCache = {
                  loadedAt: Date.now(),
                  value: bootstrapData,
                };
                return bootstrapData;
              }
            );

      const [workspace, bootstrap] = await Promise.all([
        workspacePromise,
        bootstrapPromise,
      ]);
      const p = (workspace as ProjectWorkspaceDto).project;
      setProject(p);
      setFinancialSummary(workspace.financialSummary ?? null);
      setCommercialSummary(workspace.commercialSummary ?? null);
      setPostHandoverSummary(workspace.postHandoverSummary ?? null);
      setIntegrationSummary(workspace.integrationSummary ?? null);
      setGovernanceSummary(workspace.governanceSummary ?? null);
      setProjectLinkOptions(
        workspace.linkOptions ?? EMPTY_PROJECT_LINK_OPTIONS
      );
      setPhaseGateEvaluations(workspace.phaseGateEvaluations ?? []);
      setTypes(bootstrap.types);
      setPriorities(bootstrap.priorities);
      setTemplates(bootstrap.templates);
      setUnitTypeTemplates(bootstrap.unitTypeTemplates);
      setPortfolios(bootstrap.portfolios);
      setBusinessPartners(bootstrap.businessPartners);
      setCustomerPartnerOptions(
        bootstrap.customerBusinessPartners ??
          bootstrap.businessPartners.filter((partner) =>
            isCustomerBusinessPartner(partner)
          )
      );
      setAiInsights([]);
      setScheduleAnalysis(null);
      setBudgetRevisions([]);
      setForecastVersions([]);
      setMaterialRequisitions([]);
      setMaterialWarehouses([]);
      setTenderLookup([]);
      setProcurementPlanItemLookup([]);
      setPurchaseRequisitionLookup([]);
      setPurchaseOrderLookup([]);
      setInventoryItems([]);
      setPackageLookupsLoaded(false);
      setAnalysisLoaded(false);
      setMaterialsLoaded(false);
      setCollapsedGanttItems([]);
      void loadReferenceData(p.portfolioId).catch(() => {
        toast.error('Some project reference data could not be loaded');
      });
      setOverview({
        title: p.title,
        summary: p.summary,
        businessCase: p.businessCase,
        objectives: p.objectives,
        methodology: p.methodology,
        projectTypeId: p.projectTypeId,
        projectPriorityId: p.projectPriorityId,
        templateId: p.templateId,
        portfolioId: p.portfolioId,
        programId: p.programId,
        sponsorId: p.sponsorId,
        projectManagerId: p.projectManagerId,
        businessPartnerId: p.businessPartnerId,
        contractId: p.contractId,
        startDate: p.startDate,
        targetEndDate: p.targetEndDate,
        slackMonths: p.slackMonths,
        estimatedBudget: p.estimatedBudget,
        approvedBudget: p.approvedBudget,
        actualCost: p.actualCost,
        baseCurrencyCode: p.baseCurrencyCode,
        fundingSource: p.fundingSource,
        budgetStatus: p.budgetStatus,
        approvalRequired: p.approvalRequired,
        externalPortalAccessEnabled: p.externalPortalAccessEnabled,
        externalCollaborationEnabled: p.externalCollaborationEnabled,
        developmentProfile: p.developmentProfile
          ? {
              deliveryStructure: p.developmentProfile.deliveryStructure,
              developmentType: p.developmentProfile.developmentType,
              siteName: p.developmentProfile.siteName,
              siteAddress: p.developmentProfile.siteAddress,
              landReference: p.developmentProfile.landReference,
              procurementRoute: p.developmentProfile.procurementRoute,
              contractStrategy: p.developmentProfile.contractStrategy,
              consultantTeam: p.developmentProfile.consultantTeam,
              fundingArrangement: p.developmentProfile.fundingArrangement,
              handoverStrategy: p.developmentProfile.handoverStrategy,
              notes: p.developmentProfile.notes,
            }
          : {
              deliveryStructure: 'WholeDevelopment',
            },
      });
      setBudgetRevision((prev) => ({
        ...prev,
        estimatedBudget: p.estimatedBudget ?? 0,
        approvedBudget: p.approvedBudget ?? p.estimatedBudget ?? 0,
        forecastCost: p.actualCost ?? 0,
      }));
      setClosure({
        finalBudget:
          p.closure?.finalBudget ?? p.approvedBudget ?? p.estimatedBudget ?? 0,
        finalCost: p.closure?.finalCost ?? p.actualCost ?? 0,
        deliverablesAccepted: p.closure?.deliverablesAccepted ?? false,
        tasksCompletedOrWaived: p.closure?.tasksCompletedOrWaived ?? false,
        assetsReconciled: p.closure?.assetsReconciled ?? false,
        openItemsDisposed: p.closure?.openItemsDisposed ?? false,
        closureChecklistJson: p.closure?.closureChecklistJson ?? '',
        openItemsDisposition: p.closure?.openItemsDisposition ?? '',
        assetReconciliationNotes: p.closure?.assetReconciliationNotes ?? '',
        lessonsLearnedSummary: p.closure?.lessonsLearnedSummary ?? '',
        postImplementationReview: p.closure?.postImplementationReview ?? '',
        overrideReason: p.closure?.overrideReason ?? '',
      });
      if (currentUserId) {
        setTimesheet((prev) => ({
          ...prev,
          userId: prev.userId || currentUserId,
        }));
        setExpense((prev) => ({
          ...prev,
          userId: prev.userId || currentUserId,
        }));
        setActionItem((prev) => ({
          ...prev,
          ownerId: prev.ownerId || currentUserId,
        }));
        setDecision((prev) => ({
          ...prev,
          approverId: prev.approverId || currentUserId,
        }));
        setMeeting((prev) => ({
          ...prev,
          facilitatorId: prev.facilitatorId || currentUserId,
        }));
      }
      setProjectPhaseDraft({
        ...projectPhaseInit,
        sortOrder: p.phases.length,
      });
      setEditingProjectPhaseId(null);
      setEditingProjectPackageId(null);
      setEditingProjectBoqItemId(null);
      setEditingVariationOrderId(null);
      setEditingInterimValuationId(null);
      setEditingPaymentCertificateId(null);
      setEditingExtensionOfTimeId(null);
      setEditingUnitId(null);
      setProjectPackageDraft({ ...packageInit, currency: baseCurrency.code });
      setBoqItemDraft({
        ...boqItemInit,
        currency: baseCurrency.code,
        projectPackageId: p.packages[0]?.id || '',
      });
      setApprovalDraft({
        ...approvalRegisterItemInit,
        approvalType: approvalRegisterItemInit.approvalType,
        status: approvalRegisterItemInit.status,
        isRequired: approvalRegisterItemInit.isRequired,
        projectPhaseId: p.phases[0]?.id,
      });
      setDrawingDraft({
        ...drawingInit,
        projectPhaseId: p.phases[0]?.id,
      });
      setSubmittalDraft({
        ...submittalInit,
        projectPhaseId: p.phases[0]?.id,
        projectPackageId: p.packages[0]?.id,
      });
      setRfiDraft({
        ...rfiInit,
        projectPhaseId: p.phases[0]?.id,
        projectPackageId: p.packages[0]?.id,
      });
      setSiteInstructionDraft({
        ...siteInstructionInit,
        currency: baseCurrency.code,
        projectPhaseId: p.phases[0]?.id,
        projectPackageId: p.packages[0]?.id,
      });
      setVariationOrderDraft({
        ...variationOrderInit,
        currency: baseCurrency.code,
        projectPhaseId: p.phases[0]?.id,
        projectPackageId: p.packages[0]?.id,
        contractId:
          p.contractId ||
          p.packages.find((item) => !!item.contractId)?.contractId,
      });
      setInterimValuationDraft({
        ...interimValuationInit,
        currency: baseCurrency.code,
        projectPhaseId: p.phases[0]?.id,
        projectPackageId: p.packages[0]?.id,
        projectMilestoneId: p.milestones[0]?.id,
        contractId:
          p.contractId ||
          p.packages.find((item) => !!item.contractId)?.contractId,
      });
      setPaymentCertificateDraft({
        ...paymentCertificateInit,
        currency: baseCurrency.code,
        projectPhaseId: p.phases[0]?.id,
        projectPackageId: p.packages[0]?.id,
        contractId:
          p.contractId ||
          p.packages.find((item) => !!item.contractId)?.contractId,
        projectInterimValuationId: p.interimValuations[0]?.id,
      });
      setExtensionOfTimeDraft({
        ...extensionOfTimeInit,
        projectPhaseId: p.phases[0]?.id,
        projectPackageId: p.packages[0]?.id,
        contractId:
          p.contractId ||
          p.packages.find((item) => !!item.contractId)?.contractId,
      });
      setUnitDraft({ ...unitInit, currency: baseCurrency.code });
      setCustomerVariationDraft({
        ...customerVariationInit,
        currency: baseCurrency.code,
        projectUnitId: p.units[0]?.id,
      });
      setCommissioningDraft({
        ...commissioningInit,
        projectUnitId: p.units[0]?.id,
      });
      setHandoverDraft({
        ...handoverItemInit,
        projectUnitId: p.units[0]?.id,
      });
      setSnagDraft({
        ...snagItemInit,
        projectUnitId: p.units[0]?.id,
      });
      setDefectLiabilityDraft({
        ...defectLiabilityCaseInit,
        currency: baseCurrency.code,
        projectUnitId: p.units[0]?.id,
      });
      setEditingVariationOrderId(null);
      setEditingInterimValuationId(null);
      setEditingPaymentCertificateId(null);
      setEditingExtensionOfTimeId(null);
      setEditingUnitId(null);
      setEditingCustomerVariationId(null);
      setEditingSnagItemId(null);
      setEditingDefectLiabilityCaseId(null);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project');
    } finally {
      setLoading(false);
    }
  };

  const resetWorkEditor = () => {
    setWork(workInit);
    setEditingWorkItemId(null);
  };
  const resetProjectPhaseEditor = () => {
    setProjectPhaseDraft({
      ...projectPhaseInit,
      sortOrder: project?.phases.length ?? 0,
    });
    setEditingProjectPhaseId(null);
  };
  const resetProjectPackageEditor = () => {
    setProjectPackageDraft({ ...packageInit, currency: baseCurrency.code });
    setEditingProjectPackageId(null);
  };
  const resetProjectBoqItemEditor = () => {
    setBoqItemDraft({
      ...boqItemInit,
      currency: baseCurrency.code,
      projectPackageId: project?.packages[0]?.id || '',
    });
    setEditingProjectBoqItemId(null);
  };
  const resetVariationOrderEditor = () => {
    setVariationOrderDraft({
      ...variationOrderInit,
      currency: baseCurrency.code,
      projectPhaseId: project?.phases[0]?.id,
      projectPackageId: project?.packages[0]?.id,
      contractId:
        project?.finalAccount?.contractId ||
        project?.contractId ||
        project?.packages.find((item) => !!item.contractId)?.contractId,
    });
    setEditingVariationOrderId(null);
  };
  const resetInterimValuationEditor = () => {
    setInterimValuationDraft({
      ...interimValuationInit,
      currency: baseCurrency.code,
      projectPhaseId: project?.phases[0]?.id,
      projectPackageId: project?.packages[0]?.id,
      projectMilestoneId: project?.milestones[0]?.id,
      contractId:
        project?.finalAccount?.contractId ||
        project?.contractId ||
        project?.packages.find((item) => !!item.contractId)?.contractId,
    });
    setEditingInterimValuationId(null);
  };
  const resetPaymentCertificateEditor = () => {
    setPaymentCertificateDraft({
      ...paymentCertificateInit,
      currency: baseCurrency.code,
      projectPhaseId: project?.phases[0]?.id,
      projectPackageId: project?.packages[0]?.id,
      contractId:
        project?.finalAccount?.contractId ||
        project?.contractId ||
        project?.packages.find((item) => !!item.contractId)?.contractId,
      projectInterimValuationId: project?.interimValuations[0]?.id,
    });
    setEditingPaymentCertificateId(null);
  };
  const resetExtensionOfTimeEditor = () => {
    setExtensionOfTimeDraft({
      ...extensionOfTimeInit,
      projectPhaseId: project?.phases[0]?.id,
      projectPackageId: project?.packages[0]?.id,
      contractId:
        project?.finalAccount?.contractId ||
        project?.contractId ||
        project?.packages.find((item) => !!item.contractId)?.contractId,
    });
    setEditingExtensionOfTimeId(null);
  };
  const resetUnitEditor = () => {
    setUnitDraft({ ...unitInit, currency: baseCurrency.code });
    setEditingUnitId(null);
  };
  const resetCustomerVariationEditor = () => {
    setCustomerVariationDraft({
      ...customerVariationInit,
      currency: baseCurrency.code,
      projectUnitId: project?.units[0]?.id,
    });
    setEditingCustomerVariationId(null);
  };
  const resetSnagEditor = () => {
    setSnagDraft({
      ...snagItemInit,
      projectUnitId: project?.units[0]?.id,
    });
    setEditingSnagItemId(null);
  };
  const resetDefectLiabilityEditor = () => {
    setDefectLiabilityDraft({
      ...defectLiabilityCaseInit,
      currency: baseCurrency.code,
      projectUnitId: project?.units[0]?.id,
    });
    setEditingDefectLiabilityCaseId(null);
  };

  const beginEditWorkItem = (
    item: ProjectWorkItemDto,
    options?: { keepCurrentView?: boolean }
  ) => {
    if (!options?.keepCurrentView) {
      setTaskView('tree');
    }
    setEditingWorkItemId(item.id);
    setWork({
      parentId: item.parentId || undefined,
      projectPackageId: item.projectPackageId || undefined,
      nodeType: item.nodeType,
      title: item.title,
      description: item.description || undefined,
      status: item.status || 'New',
      priority: item.priority || 'Normal',
      assignedToUserId: item.assignedToUserId || undefined,
      plannedStartDate: item.plannedStartDate || undefined,
      plannedEndDate: item.plannedEndDate || undefined,
      actualStartDate: item.actualStartDate || undefined,
      actualEndDate: item.actualEndDate || undefined,
      percentComplete: item.percentComplete ?? 0,
      isRollupEnabled: item.isRollupEnabled ?? true,
      effortEstimateHours: item.effortEstimateHours ?? undefined,
      actualEffortHours: item.actualEffortHours ?? undefined,
    });
  };
  const beginEditProjectPhase = (phase: ProjectPhaseDto) => {
    setEditingProjectPhaseId(phase.id);
    setProjectPhaseDraft({
      parentPhaseId: phase.parentPhaseId || undefined,
      code: phase.code || '',
      name: phase.name,
      description: phase.description || '',
      status: phase.status || DEFAULT_PROJECT_PHASE_STATUSES[0],
      sortOrder: phase.sortOrder,
      isOptional: phase.isOptional,
      isStageGateRequired: phase.isStageGateRequired,
      completionWeightPercent: phase.completionWeightPercent ?? 0,
      plannedStartDate:
        normalizeDateInputValue(phase.plannedStartDate) || undefined,
      plannedEndDate:
        normalizeDateInputValue(phase.plannedEndDate) || undefined,
      actualStartDate:
        normalizeDateInputValue(phase.actualStartDate) || undefined,
      actualEndDate: normalizeDateInputValue(phase.actualEndDate) || undefined,
    });
  };
  const beginEditProjectPackage = (projectPackage: ProjectPackageDto) => {
    setEditingProjectPackageId(projectPackage.id);
    setProjectPackageDraft({
      projectPhaseId: projectPackage.projectPhaseId || undefined,
      code: projectPackage.code || undefined,
      name: projectPackage.name,
      description: projectPackage.description || undefined,
      packageType: projectPackage.packageType,
      status: projectPackage.status,
      sortOrder: projectPackage.sortOrder,
      completionWeightPercent: projectPackage.completionWeightPercent ?? 0,
      plannedStartDate:
        normalizeDateInputValue(projectPackage.plannedStartDate) || undefined,
      plannedEndDate:
        normalizeDateInputValue(projectPackage.plannedEndDate) || undefined,
      procurementRoute: projectPackage.procurementRoute || undefined,
      contractStrategy: projectPackage.contractStrategy || undefined,
      businessPartnerId: projectPackage.businessPartnerId || undefined,
      tenderId: projectPackage.tenderId || undefined,
      contractId: projectPackage.contractId || undefined,
      procurementPlanItemId: projectPackage.procurementPlanItemId || undefined,
      purchaseRequisitionId: projectPackage.purchaseRequisitionId || undefined,
      purchaseOrderId: projectPackage.purchaseOrderId || undefined,
      budgetAmount: projectPackage.budgetAmount ?? undefined,
      committedAmount: projectPackage.committedAmount ?? undefined,
      actualAmount: projectPackage.actualAmount ?? undefined,
      forecastAmount: projectPackage.forecastAmount ?? undefined,
      currency: projectPackage.currency || baseCurrency.code,
      notes: projectPackage.notes || undefined,
    });
  };
  const beginEditProjectBoqItem = (boqItem: ProjectBoqItemDto) => {
    setEditingProjectBoqItemId(boqItem.id);
    setBoqItemDraft({
      projectPackageId: boqItem.projectPackageId,
      sectionCatalogEntryId: boqItem.sectionCatalogEntryId || undefined,
      tradeCatalogEntryId: boqItem.tradeCatalogEntryId || undefined,
      costCodeCatalogEntryId: boqItem.costCodeCatalogEntryId || undefined,
      measurementCodeCatalogEntryId:
        boqItem.measurementCodeCatalogEntryId || undefined,
      lineNumber: boqItem.lineNumber || undefined,
      itemCode: boqItem.itemCode || undefined,
      itemType: boqItem.itemType,
      description: boqItem.description,
      quantity: boqItem.quantity ?? undefined,
      unitOfMeasure: boqItem.unitOfMeasure || undefined,
      unitRate: boqItem.unitRate ?? undefined,
      budgetQuantity: boqItem.budgetQuantity ?? undefined,
      budgetUnitRate: boqItem.budgetUnitRate ?? undefined,
      budgetAmount: boqItem.budgetAmount ?? undefined,
      committedAmount: boqItem.committedAmount ?? undefined,
      actualAmount: boqItem.actualAmount ?? undefined,
      forecastAmount: boqItem.forecastAmount ?? undefined,
      currency: boqItem.currency || baseCurrency.code,
      inventoryItemId: boqItem.inventoryItemId || undefined,
      tenderItemId: boqItem.tenderItemId || undefined,
      procurementPlanItemId: boqItem.procurementPlanItemId || undefined,
      purchaseRequisitionItemId: boqItem.purchaseRequisitionItemId || undefined,
      purchaseOrderItemId: boqItem.purchaseOrderItemId || undefined,
      notes: boqItem.notes || undefined,
      sortOrder: boqItem.sortOrder,
    });
  };
  const beginEditVariationOrder = (
    variationOrder: ProjectVariationOrderDto
  ) => {
    setEditingVariationOrderId(variationOrder.id);
    setVariationOrderDraft({
      projectPhaseId: variationOrder.projectPhaseId || undefined,
      projectPackageId: variationOrder.projectPackageId || undefined,
      contractId: variationOrder.contractId || undefined,
      referenceNumber: variationOrder.referenceNumber || undefined,
      title: variationOrder.title,
      description: variationOrder.description || undefined,
      variationType: variationOrder.variationType,
      status: variationOrder.status,
      requestedDate:
        normalizeDateInputValue(variationOrder.requestedDate) || undefined,
      approvedDate:
        normalizeDateInputValue(variationOrder.approvedDate) || undefined,
      implementedDate:
        normalizeDateInputValue(variationOrder.implementedDate) || undefined,
      requestedByName: variationOrder.requestedByName || undefined,
      approvedByName: variationOrder.approvedByName || undefined,
      estimatedAmount: variationOrder.estimatedAmount ?? undefined,
      approvedAmount: variationOrder.approvedAmount ?? undefined,
      currency: variationOrder.currency || baseCurrency.code,
      scheduleImpactDays: variationOrder.scheduleImpactDays ?? undefined,
      notes: variationOrder.notes || undefined,
    });
  };
  const beginEditInterimValuation = (valuation: ProjectInterimValuationDto) => {
    const normalizeGuid = (value?: string | null) =>
      (value || '').trim().toLowerCase();
    const resolvedCompletedProjectPackageIds =
      valuation.completedProjectPackageIds &&
      valuation.completedProjectPackageIds.length > 0
        ? valuation.completedProjectPackageIds
        : valuation.projectPackageId
          ? [valuation.projectPackageId]
          : [];
    const relatedPhaseIds = new Set<string>();

    if (valuation.projectPhaseId) {
      relatedPhaseIds.add(normalizeGuid(valuation.projectPhaseId));
    }

    resolvedCompletedProjectPackageIds.forEach((projectPackageId) => {
      const matchedPackage = project?.packages.find(
        (item) => normalizeGuid(item.id) === normalizeGuid(projectPackageId)
      );
      if (matchedPackage?.projectPhaseId) {
        relatedPhaseIds.add(normalizeGuid(matchedPackage.projectPhaseId));
      }
    });

    const inferredMilestoneId =
      valuation.projectMilestoneId ||
      project?.milestones.find(
        (milestone) =>
          relatedPhaseIds.size > 0 &&
          Array.from(relatedPhaseIds).every((phaseId) =>
            milestone.phases.some(
              (phase) => normalizeGuid(phase.projectPhaseId) === phaseId
            )
          )
      )?.id ||
      project?.milestones.find(
        (milestone) =>
          relatedPhaseIds.size > 0 &&
          milestone.phases.some((phase) =>
            relatedPhaseIds.has(normalizeGuid(phase.projectPhaseId))
          )
      )?.id;

    setEditingInterimValuationId(valuation.id);
    setInterimValuationDraft({
      projectPhaseId: valuation.projectPhaseId || undefined,
      projectPackageId: valuation.projectPackageId || undefined,
      projectMilestoneId: inferredMilestoneId || undefined,
      contractId: valuation.contractId || undefined,
      valuationNumber: valuation.valuationNumber || undefined,
      title: valuation.title,
      status: valuation.status,
      valuationDate:
        normalizeDateInputValue(valuation.valuationDate) || undefined,
      grossWorkValue: valuation.grossWorkValue ?? undefined,
      materialsOnSiteValue: valuation.materialsOnSiteValue ?? undefined,
      variationValue: valuation.variationValue ?? undefined,
      retentionPercentage: valuation.retentionPercentage ?? undefined,
      retentionAmount: valuation.retentionAmount ?? undefined,
      previousCertifiedAmount: valuation.previousCertifiedAmount ?? undefined,
      netValuationAmount: valuation.netValuationAmount ?? undefined,
      currency: valuation.currency || baseCurrency.code,
      notes: valuation.notes || undefined,
      completedProjectPackageIds: resolvedCompletedProjectPackageIds,
    });
  };
  const beginEditPaymentCertificate = (
    certificate: ProjectPaymentCertificateDto
  ) => {
    setEditingPaymentCertificateId(certificate.id);
    setPaymentCertificateDraft({
      projectPhaseId: certificate.projectPhaseId || undefined,
      projectPackageId: certificate.projectPackageId || undefined,
      contractId: certificate.contractId || undefined,
      projectInterimValuationId:
        certificate.projectInterimValuationId || undefined,
      certificateNumber: certificate.certificateNumber || undefined,
      title: certificate.title,
      status: certificate.status,
      issueDate: normalizeDateInputValue(certificate.issueDate) || undefined,
      paymentDueDate:
        normalizeDateInputValue(certificate.paymentDueDate) || undefined,
      grossCertifiedAmount: certificate.grossCertifiedAmount ?? undefined,
      retentionHeldAmount: certificate.retentionHeldAmount ?? undefined,
      retentionReleasedAmount: certificate.retentionReleasedAmount ?? undefined,
      otherDeductionsAmount: certificate.otherDeductionsAmount ?? undefined,
      netCertifiedAmount: certificate.netCertifiedAmount ?? undefined,
      currency: certificate.currency || baseCurrency.code,
      notes: certificate.notes || undefined,
    });
  };
  const beginEditExtensionOfTime = (extension: ProjectExtensionOfTimeDto) => {
    setEditingExtensionOfTimeId(extension.id);
    setExtensionOfTimeDraft({
      projectPhaseId: extension.projectPhaseId || undefined,
      projectPackageId: extension.projectPackageId || undefined,
      contractId: extension.contractId || undefined,
      referenceNumber: extension.referenceNumber || undefined,
      title: extension.title,
      reason: extension.reason || undefined,
      status: extension.status,
      requestedDate:
        normalizeDateInputValue(extension.requestedDate) || undefined,
      decisionDate:
        normalizeDateInputValue(extension.decisionDate) || undefined,
      daysRequested: extension.daysRequested ?? undefined,
      daysApproved: extension.daysApproved ?? undefined,
      revisedCompletionDate:
        normalizeDateInputValue(extension.revisedCompletionDate) || undefined,
      requestedByName: extension.requestedByName || undefined,
      decidedByName: extension.decidedByName || undefined,
      notes: extension.notes || undefined,
    });
  };
  const beginEditProjectUnit = (unit: ProjectUnitDto) => {
    setEditingUnitId(unit.id);
    setUnitDraft({
      projectBuildingId: unit.projectBuildingId || undefined,
      projectFloorId: unit.projectFloorId || undefined,
      projectUnitReleaseBatchId: unit.projectUnitReleaseBatchId || undefined,
      projectUnitTypeTemplateId: unit.projectUnitTypeTemplateId || undefined,
      isReleasedForMarket: unit.isReleasedForMarket,
      customerBusinessPartnerId: unit.customerBusinessPartnerId || undefined,
      salesAgreementId: unit.salesAgreementId || undefined,
      salesOrderId: unit.salesOrderId || undefined,
      code: unit.code || undefined,
      name: unit.name,
      unitType: unit.unitType || undefined,
      status: unit.status || undefined,
      blockName: unit.blockName || undefined,
      floorLabel: unit.floorLabel || undefined,
      areaSquareMeters: unit.areaSquareMeters ?? undefined,
      valuationRate: unit.valuationRate ?? undefined,
      basePrice: unit.basePrice ?? undefined,
      currency: unit.currency || baseCurrency.code,
      handoverDate: normalizeDateInputValue(unit.handoverDate) || undefined,
      sortOrder: unit.sortOrder,
      notes: unit.notes || undefined,
      amenities: (unit.amenities || []).map((amenity, index) => ({
        inventoryItemId: amenity.inventoryItemId,
        itemCode: amenity.itemCode,
        amenityName: amenity.amenityName,
        quantity: amenity.quantity,
        unitCost: amenity.unitCost,
        sortOrder: amenity.sortOrder ?? index,
      })),
    });
  };
  const beginEditCustomerVariation = (
    variation: ProjectCustomerVariationDto
  ) => {
    setEditingCustomerVariationId(variation.id);
    setCustomerVariationDraft({
      projectUnitId: variation.projectUnitId || undefined,
      customerBusinessPartnerId:
        variation.customerBusinessPartnerId || undefined,
      salesAgreementId: variation.salesAgreementId || undefined,
      salesOrderId: variation.salesOrderId || undefined,
      jobCardId: variation.jobCardId || undefined,
      workOrderId: variation.workOrderId || undefined,
      title: variation.title,
      description: variation.description || undefined,
      timing: variation.timing,
      status: variation.status,
      variationType: variation.variationType || undefined,
      requestDate: normalizeDateInputValue(variation.requestDate) || undefined,
      targetCompletionDate:
        normalizeDateInputValue(variation.targetCompletionDate) || undefined,
      estimatedAmount: variation.estimatedAmount ?? undefined,
      quotedAmount: variation.quotedAmount ?? undefined,
      approvedAmount: variation.approvedAmount ?? undefined,
      billedAmount: variation.billedAmount ?? undefined,
      currency: variation.currency || baseCurrency.code,
      scheduleImpactDays: variation.scheduleImpactDays ?? undefined,
      requiresScheduleAdjustment: variation.requiresScheduleAdjustment,
      notes: variation.notes || undefined,
    });
  };
  const beginEditSnagItem = (snagItem: ProjectSnagItemDto) => {
    setEditingSnagItemId(snagItem.id);
    setSnagDraft({
      projectUnitId: snagItem.projectUnitId || undefined,
      title: snagItem.title,
      description: snagItem.description || undefined,
      severity: snagItem.severity,
      status: snagItem.status,
      reportedDate: normalizeDateInputValue(snagItem.reportedDate) || undefined,
      targetClosureDate:
        normalizeDateInputValue(snagItem.targetClosureDate) || undefined,
      closedDate: normalizeDateInputValue(snagItem.closedDate) || undefined,
      raisedByName: snagItem.raisedByName || undefined,
      responsibleParty: snagItem.responsibleParty || undefined,
      notes: snagItem.notes || undefined,
    });
  };
  const beginEditDefectLiabilityCase = (
    defectLiabilityCase: ProjectDefectLiabilityCaseDto
  ) => {
    setEditingDefectLiabilityCaseId(defectLiabilityCase.id);
    setDefectLiabilityDraft({
      projectUnitId: defectLiabilityCase.projectUnitId || undefined,
      customerBusinessPartnerId:
        defectLiabilityCase.customerBusinessPartnerId || undefined,
      jobCardId: defectLiabilityCase.jobCardId || undefined,
      workOrderId: defectLiabilityCase.workOrderId || undefined,
      title: defectLiabilityCase.title,
      description: defectLiabilityCase.description || undefined,
      status: defectLiabilityCase.status,
      reportedDate:
        normalizeDateInputValue(defectLiabilityCase.reportedDate) || undefined,
      targetResolutionDate:
        normalizeDateInputValue(defectLiabilityCase.targetResolutionDate) ||
        undefined,
      resolvedDate:
        normalizeDateInputValue(defectLiabilityCase.resolvedDate) || undefined,
      isWarrantyRelated: defectLiabilityCase.isWarrantyRelated,
      warrantyCategory: defectLiabilityCase.warrantyCategory || undefined,
      warrantyExpiryDate:
        normalizeDateInputValue(defectLiabilityCase.warrantyExpiryDate) ||
        undefined,
      firstResponseDate:
        normalizeDateInputValue(defectLiabilityCase.firstResponseDate) ||
        undefined,
      responseSlaDays: defectLiabilityCase.responseSlaDays ?? undefined,
      resolutionSlaDays: defectLiabilityCase.resolutionSlaDays ?? undefined,
      rectificationCost: defectLiabilityCase.rectificationCost ?? undefined,
      chargeableAmount: defectLiabilityCase.chargeableAmount ?? undefined,
      currency: defectLiabilityCase.currency || baseCurrency.code,
      notes: defectLiabilityCase.notes || undefined,
    });
  };
  const saveWorkEditor = async () => {
    if (!project?.id) {
      return;
    }

    if (scheduleReasonRequired && !work.scheduleChangeReason?.trim()) {
      toast.error(
        'Provide a schedule change reason before saving date changes on a locked baseline.'
      );
      return;
    }

    await act(
      () =>
        editingWorkItemId
          ? projectService
              .updateWorkItem(editingWorkItemId, work)
              .then(() => Promise.resolve())
          : projectService
              .addWorkItem(project.id, work)
              .then(() => Promise.resolve()),
      editingWorkItemId ? 'Work item updated' : 'Work item added',
      resetWorkEditor
    );
  };
  const saveProjectPhase = async () => {
    if (!project?.id) {
      return false;
    }

    const name = projectPhaseDraft.name?.trim();
    if (!name) {
      toast.error('Phase name is required');
      return false;
    }

    const payload = {
      ...projectPhaseDraft,
      name,
      code: projectPhaseDraft.code?.trim() || undefined,
      description: projectPhaseDraft.description?.trim() || undefined,
      status: projectPhaseDraft.status || DEFAULT_PROJECT_PHASE_STATUSES[0],
      sortOrder: Number.isFinite(projectPhaseDraft.sortOrder)
        ? Number(projectPhaseDraft.sortOrder)
        : project.phases.length,
    };

    let saved = false;
    await act(
      () =>
        editingProjectPhaseId
          ? projectService
              .updateProjectPhase(editingProjectPhaseId, payload)
              .then(() => {
                saved = true;
                return Promise.resolve();
              })
          : projectService.addProjectPhase(project.id, payload).then(() => {
              saved = true;
              return Promise.resolve();
            }),
      editingProjectPhaseId ? 'Project phase updated' : 'Project phase added',
      resetProjectPhaseEditor
    );

    return saved;
  };

  useEffect(() => {
    load();
  }, [id]);

  useEffect(() => {
    if (!project?.id) {
      return;
    }

    if (!analysisLoaded && (activeTab === 'plan' || activeTab === 'analysis')) {
      void loadAnalysisData(project.id);
    }

    if (!materialsLoaded && activeTab === 'materials') {
      void loadMaterials(project.id);
    }

    if (!packageLookupsLoaded && activeTab === 'packages') {
      void loadPackageLookups(project.id).catch(() => {
        toast.error('Some work component lookup data could not be loaded');
      });
    }
  }, [
    activeTab,
    analysisLoaded,
    materialsLoaded,
    packageLookupsLoaded,
    project?.id,
  ]);

  useEffect(() => {
    if (taskView === 'timeline') {
      setGanttDialogOpen(true);
    }
  }, [taskView]);

  useEffect(() => {
    if (!project?.packages?.length) {
      return;
    }

    if (
      !boqItemDraft.projectPackageId ||
      !project.packages.some(
        (item) => item.id === boqItemDraft.projectPackageId
      )
    ) {
      setBoqItemDraft((current) => ({
        ...current,
        projectPackageId: project.packages[0]?.id || '',
      }));
    }
  }, [boqItemDraft.projectPackageId, project?.packages]);

  useEffect(() => {
    const firstUnitId = project?.units[0]?.id;
    const hasUnit = (unitId?: string) =>
      !!unitId && !!project?.units.some((item) => item.id === unitId);

    setCustomerVariationDraft((current) =>
      hasUnit(current.projectUnitId)
        ? current
        : { ...current, projectUnitId: firstUnitId }
    );
    setCommissioningDraft((current) =>
      hasUnit(current.projectUnitId)
        ? current
        : { ...current, projectUnitId: firstUnitId }
    );
    setHandoverDraft((current) =>
      hasUnit(current.projectUnitId)
        ? current
        : { ...current, projectUnitId: firstUnitId }
    );
    setSnagDraft((current) =>
      hasUnit(current.projectUnitId)
        ? current
        : { ...current, projectUnitId: firstUnitId }
    );
    setDefectLiabilityDraft((current) =>
      hasUnit(current.projectUnitId)
        ? current
        : { ...current, projectUnitId: firstUnitId }
    );
  }, [project?.units]);

  useEffect(() => {
    if (baseCurrency.code) {
      setProjectPackageDraft((current) =>
        current.currency ? current : { ...current, currency: baseCurrency.code }
      );
      setBoqItemDraft((current) =>
        current.currency ? current : { ...current, currency: baseCurrency.code }
      );
      setVariationOrderDraft((current) =>
        current.currency ? current : { ...current, currency: baseCurrency.code }
      );
      setInterimValuationDraft((current) =>
        current.currency ? current : { ...current, currency: baseCurrency.code }
      );
      setPaymentCertificateDraft((current) =>
        current.currency ? current : { ...current, currency: baseCurrency.code }
      );
      setDefectLiabilityDraft((current) =>
        current.currency ? current : { ...current, currency: baseCurrency.code }
      );
    }
  }, [baseCurrency.code]);

  const flat = useMemo(
    () => flatten(project?.workItems || []),
    [project?.workItems]
  );
  const plannerEditingItem = useMemo(
    () =>
      editingWorkItemId
        ? (flat.find((item) => item.id === editingWorkItemId) ?? null)
        : null,
    [editingWorkItemId, flat]
  );
  const workScheduleChangePending = useMemo(() => {
    if (!plannerEditingItem) return false;
    return (
      normalizeDateInputValue(plannerEditingItem.plannedStartDate) !==
        normalizeDateInputValue(work.plannedStartDate) ||
      normalizeDateInputValue(plannerEditingItem.plannedEndDate) !==
        normalizeDateInputValue(work.plannedEndDate)
    );
  }, [plannerEditingItem, work.plannedEndDate, work.plannedStartDate]);
  const scheduleReasonRequired = Boolean(
    editingWorkItemId &&
    governanceSummary?.hasLockedBaseline &&
    workScheduleChangePending
  );
  const workItemTitles = useMemo(
    () => new Map(flat.map((item) => [item.id, item.title])),
    [flat]
  );
  const milestoneTitles = useMemo(
    () =>
      new Map((project?.milestones || []).map((item) => [item.id, item.title])),
    [project?.milestones]
  );
  const taskBoardItems = useMemo(
    () =>
      flat.filter((item) =>
        ['Task', 'Subtask', 'ChecklistItem'].includes(item.nodeType)
      ),
    [flat]
  );
  const timelineItems = useMemo(
    () => flat.filter((item) => item.plannedStartDate && item.plannedEndDate),
    [flat]
  );
  const offBaselineItemCount = useMemo(
    () => timelineItems.filter((item) => item.isOffBaseline).length,
    [timelineItems]
  );
  const ganttParentLookup = useMemo(
    () => new Map(flat.map((item) => [item.id, item.parentId])),
    [flat]
  );
  const ganttChildrenLookup = useMemo(
    () =>
      new Map(flat.map((item) => [item.id, (item.children?.length ?? 0) > 0])),
    [flat]
  );
  const visibleTimelineItems = useMemo(
    () =>
      timelineItems.filter((item) => {
        let currentParentId = item.parentId;
        while (currentParentId) {
          if (collapsedGanttItems.includes(currentParentId)) {
            return false;
          }
          currentParentId = ganttParentLookup.get(currentParentId);
        }
        if (ganttQuickFilters.overdue && !isWorkItemOverdue(item)) {
          return false;
        }
        if (ganttQuickFilters.offBaseline && !item.isOffBaseline) {
          return false;
        }
        if (
          ganttQuickFilters.assignedToMe &&
          (!currentUserId || item.assignedToUserId !== currentUserId)
        ) {
          return false;
        }
        return true;
      }),
    [
      collapsedGanttItems,
      currentUserId,
      ganttParentLookup,
      ganttQuickFilters,
      timelineItems,
    ]
  );
  const timelineBounds = useMemo(() => {
    if (timelineItems.length === 0) return null;
    const starts = timelineItems.map((item) =>
      new Date(item.plannedStartDate as string).setHours(0, 0, 0, 0)
    );
    const ends = timelineItems.map((item) =>
      new Date(item.plannedEndDate as string).setHours(0, 0, 0, 0)
    );
    const min = Math.min(...starts);
    const max = Math.max(...ends);
    const totalDays = Math.max(1, Math.round((max - min) / 86400000) + 1);
    return { min, max, totalDays };
  }, [timelineItems]);
  const ganttColumns = useMemo(() => {
    if (!timelineBounds) return [];
    return Array.from({ length: timelineBounds.totalDays }, (_, index) => {
      const date = new Date(timelineBounds.min + index * 86400000);
      return {
        key: `${date.getUTCFullYear()}-${date.getUTCMonth()}-${date.getUTCDate()}`,
        date,
        isMonthStart: index === 0 || date.getDate() === 1,
      };
    });
  }, [timelineBounds]);
  const ganttMonthSegments = useMemo(() => {
    if (ganttColumns.length === 0) return [];
    return ganttColumns.reduce<
      Array<{ key: string; label: string; span: number }>
    >((segments, column) => {
      const label = format(column.date, 'MMMM yyyy');
      const current = segments[segments.length - 1];
      if (current?.label === label) {
        current.span += 1;
      } else {
        segments.push({ key: `${label}-${segments.length}`, label, span: 1 });
      }
      return segments;
    }, []);
  }, [ganttColumns]);
  const ganttWeekSegments = useMemo(() => {
    if (ganttColumns.length === 0) return [];
    return ganttColumns.reduce<
      Array<{ key: string; label: string; span: number }>
    >((segments, column) => {
      const label = `Week ${getISOWeek(column.date)}`;
      const current = segments[segments.length - 1];
      if (current?.label === label) {
        current.span += 1;
      } else {
        segments.push({ key: `${label}-${segments.length}`, label, span: 1 });
      }
      return segments;
    }, []);
  }, [ganttColumns]);
  const ganttWidth = useMemo(
    () => ganttColumns.length * GANTT_DAY_WIDTH,
    [ganttColumns.length]
  );
  const ganttSummary = useMemo(() => {
    if (!timelineBounds) return null;
    const todayMs = new Date().setHours(0, 0, 0, 0);
    const overdueItems = timelineItems.filter((item) => {
      const end = item.plannedEndDate
        ? new Date(item.plannedEndDate).setHours(0, 0, 0, 0)
        : null;
      return (
        end !== null &&
        end < todayMs &&
        !['Completed', 'Closed'].includes(item.status)
      );
    }).length;

    return {
      scheduledItems: timelineItems.length,
      spanDays: timelineBounds.totalDays,
      overdueItems,
      phases: timelineItems.filter((item) => item.nodeType === 'Phase').length,
    };
  }, [timelineBounds, timelineItems]);
  const ganttItemPositions = useMemo(() => {
    if (!timelineBounds)
      return new Map<
        string,
        { left: number; right: number; centerY: number }
      >();

    return new Map(
      visibleTimelineItems.map((item, index) => {
        const start = new Date(item.plannedStartDate as string).setHours(
          0,
          0,
          0,
          0
        );
        const end = new Date(item.plannedEndDate as string).setHours(
          0,
          0,
          0,
          0
        );
        const left =
          Math.max(0, Math.round((start - timelineBounds.min) / 86400000)) *
          GANTT_DAY_WIDTH;
        const width =
          Math.max(1, Math.round((end - start) / 86400000) + 1) *
          GANTT_DAY_WIDTH;

        return [
          item.id,
          {
            left,
            right: left + width,
            centerY: index * 56 + 28,
          },
        ];
      })
    );
  }, [timelineBounds, visibleTimelineItems]);
  const ganttTodayOffset = useMemo(() => {
    if (!timelineBounds) return null;
    const todayMs = new Date().setHours(0, 0, 0, 0);
    if (todayMs < timelineBounds.min || todayMs > timelineBounds.max) {
      return null;
    }
    return (
      Math.round((todayMs - timelineBounds.min) / 86400000) * GANTT_DAY_WIDTH
    );
  }, [timelineBounds]);
  const ganttDependencyLines = useMemo(() => {
    if (!project || !timelineBounds) return [];

    return project.taskDependencies
      .map((dependency) => {
        const predecessor = ganttItemPositions.get(
          dependency.predecessorWorkItemId
        );
        const successor = ganttItemPositions.get(
          dependency.successorWorkItemId
        );
        if (!predecessor || !successor) {
          return null;
        }

        const predecessorAnchor =
          dependency.dependencyType === 'SS' ||
          dependency.dependencyType === 'SF'
            ? predecessor.left
            : predecessor.right;
        const successorBaseAnchor =
          dependency.dependencyType === 'SS' ||
          dependency.dependencyType === 'FS'
            ? successor.left
            : successor.right;
        const successorAnchor =
          successorBaseAnchor + (dependency.lagDays ?? 0) * GANTT_DAY_WIDTH;
        const elbowX = Math.max(predecessorAnchor, successorAnchor) + 18;
        const midY =
          predecessor.centerY + (successor.centerY - predecessor.centerY) / 2;

        return {
          id: dependency.id,
          path: `M ${predecessorAnchor} ${predecessor.centerY} H ${elbowX} V ${successor.centerY} H ${successorAnchor}`,
          labelX: elbowX + 4,
          labelY: midY - 4,
          label: dependency.lagDays
            ? `${dependency.dependencyType} (${dependency.lagDays}d)`
            : dependency.dependencyType,
          stroke: dependency.isEnforced ? '#0f172a' : '#64748b',
          dashed: !dependency.isEnforced,
        };
      })
      .filter((value): value is NonNullable<typeof value> => value !== null);
  }, [ganttItemPositions, project, timelineBounds]);
  const ganttHighlight = useMemo(() => {
    const taskIds = new Set<string>();
    const dependencyIds = new Set<string>();
    const milestoneIds = new Set<string>();

    if (!project || !hoveredGanttItemId) {
      return { taskIds, dependencyIds, milestoneIds };
    }

    taskIds.add(hoveredGanttItemId);
    project.taskDependencies.forEach((dependency) => {
      if (
        dependency.predecessorWorkItemId === hoveredGanttItemId ||
        dependency.successorWorkItemId === hoveredGanttItemId
      ) {
        dependencyIds.add(dependency.id);
        taskIds.add(dependency.predecessorWorkItemId);
        taskIds.add(dependency.successorWorkItemId);
      }
    });
    project.milestones.forEach((milestone) => {
      if (milestone.workItemId === hoveredGanttItemId) {
        milestoneIds.add(milestone.id);
      }
    });

    return { taskIds, dependencyIds, milestoneIds };
  }, [hoveredGanttItemId, project]);
  const ganttMilestones = useMemo(() => {
    if (!project || !timelineBounds || visibleTimelineItems.length === 0)
      return [];

    const fallbackY = 18;
    return project.milestones
      .filter((milestone) => !!milestone.targetDate)
      .map((milestone) => {
        const target = new Date(milestone.targetDate).setHours(0, 0, 0, 0);
        if (target < timelineBounds.min || target > timelineBounds.max) {
          return null;
        }

        const linkedRow = milestone.workItemId
          ? ganttItemPositions.get(milestone.workItemId)
          : null;
        const x =
          Math.round((target - timelineBounds.min) / 86400000) *
          GANTT_DAY_WIDTH;
        const y = linkedRow?.centerY ?? fallbackY;

        return {
          id: milestone.id,
          title: milestone.title,
          status: milestone.status,
          x,
          y,
        };
      })
      .filter((value): value is NonNullable<typeof value> => value !== null);
  }, [
    ganttItemPositions,
    project,
    timelineBounds,
    visibleTimelineItems.length,
  ]);
  const toggleGanttItem = (itemId: string) => {
    setCollapsedGanttItems((current) =>
      current.includes(itemId)
        ? current.filter((value) => value !== itemId)
        : [...current, itemId]
    );
  };
  const exportGanttToExcel = () => {
    if (!project || !timelineBounds || timelineItems.length === 0) {
      toast.error('Add planned dates before exporting the Gantt plan');
      return;
    }

    const leftColumnHeaders = [
      'WBS',
      'Task',
      'Status',
      'Owner',
      'Start',
      'End',
      'Dur',
      'Notes',
    ];
    const leftColumnCount = leftColumnHeaders.length;
    const projectManagerLabel = getResolvedUserLabel(
      project.projectManagerId,
      project.projectManagerDisplayName,
      'Not assigned'
    );
    const scheduleStart = formatDateLabel(
      new Date(timelineBounds.min).toISOString()
    );
    const scheduleEnd = formatDateLabel(
      new Date(timelineBounds.max).toISOString()
    );
    const totalColumns = leftColumnCount + ganttColumns.length;
    const titleRowIndex = 0;
    const metaRowOneIndex = 2;
    const metaRowTwoIndex = 3;
    const legendRowIndex = 4;
    const monthHeaderRowIndex = 5;
    const weekHeaderRowIndex = 6;
    const dayHeaderRowIndex = 7;
    const bodyStartRowIndex = 8;
    const todayIndex =
      ganttTodayOffset !== null
        ? Math.round(ganttTodayOffset / GANTT_DAY_WIDTH)
        : -1;
    const toExcelColor = (value: string) =>
      value.replace('#', '').toUpperCase();
    const monthRow = new Array<string | number>(totalColumns).fill('');
    const weekRow = new Array<string | number>(totalColumns).fill('');
    const dayHeaderRow = [
      ...leftColumnHeaders,
      ...ganttColumns.map((column) => format(column.date, 'dd')),
    ];
    const legendRow = new Array<string | number>(totalColumns).fill('');
    legendRow[0] = 'Legend';
    legendRow[1] = 'Phase';
    legendRow[2] = 'Workstream';
    legendRow[3] = 'Task';
    legendRow[4] = 'Milestone';
    legendRow[5] = 'Today';
    const rows: Array<Array<string | number>> = [
      ['Project Plan Gantt'],
      [],
      [
        'Project Title',
        project.title,
        '',
        'Start Date',
        scheduleStart,
        '',
        'Project Duration (days)',
        timelineBounds.totalDays,
      ],
      [
        'Project Manager',
        projectManagerLabel,
        '',
        'End Date',
        scheduleEnd,
        '',
        '',
        '',
      ],
      legendRow,
      monthRow,
      weekRow,
      dayHeaderRow,
    ];

    let monthOffset = 0;
    ganttMonthSegments.forEach((segment) => {
      monthRow[leftColumnCount + monthOffset] = segment.label;
      monthOffset += segment.span;
    });

    let weekOffset = 0;
    ganttWeekSegments.forEach((segment) => {
      weekRow[leftColumnCount + weekOffset] = segment.label;
      weekOffset += segment.span;
    });

    const taskSpanMerges: Array<{
      rowIndex: number;
      startColumn: number;
      endColumn: number;
      label: string;
      palette: ReturnType<typeof getGanttPalette>;
      isRollup: boolean;
    }> = [];

    visibleTimelineItems.forEach((item) => {
      const start = new Date(item.plannedStartDate as string).setHours(
        0,
        0,
        0,
        0
      );
      const end = new Date(item.plannedEndDate as string).setHours(0, 0, 0, 0);
      const linkedMilestones = project.milestones.filter(
        (milestone) => milestone.workItemId === item.id
      );
      const workComponentContext = [
        item.projectPhaseName,
        item.projectPackageName,
      ]
        .filter(Boolean)
        .join(' / ');
      const ganttCells = ganttColumns.map(() => '');
      const durationDays = Math.max(
        1,
        Math.round((end - start) / 86400000) + 1
      );
      const assignedTo = getResolvedUserLabel(
        item.assignedToUserId,
        item.assignedToUserDisplayName
      );
      const rowIndex = bodyStartRowIndex + rows.length - bodyStartRowIndex;
      const startOffset = Math.max(
        0,
        Math.round((start - timelineBounds.min) / 86400000)
      );
      const endOffset = Math.min(
        ganttColumns.length - 1,
        Math.max(startOffset, Math.round((end - timelineBounds.min) / 86400000))
      );

      if (ganttColumns.length > 0) {
        taskSpanMerges.push({
          rowIndex,
          startColumn: leftColumnCount + startOffset,
          endColumn: leftColumnCount + endOffset,
          label: ganttChildrenLookup.get(item.id)
            ? `${item.title} (${durationDays}d, ${item.percentComplete || 0}%)`
            : item.title,
          palette: getGanttPalette(item),
          isRollup: !!ganttChildrenLookup.get(item.id),
        });
      }

      rows.push([
        item.outline,
        `${'  '.repeat(item.depth)}${item.title}`,
        formatCatalogLabel(item.status || 'New'),
        assignedTo,
        formatDateLabel(item.plannedStartDate),
        formatDateLabel(item.plannedEndDate),
        `${durationDays}d`,
        linkedMilestones
          .map(
            (milestone) =>
              `${milestone.title}${milestone.targetDate ? ` (${formatDateLabel(milestone.targetDate)})` : ''}`
          )
          .join(', ') || workComponentContext,
        ...ganttCells,
      ]);
    });

    const worksheet = XLSX.utils.aoa_to_sheet(rows) as XLSX.WorkSheet &
      Record<string, any>;
    const merges: XLSX.Range[] = [
      {
        s: { r: titleRowIndex, c: 0 },
        e: { r: titleRowIndex, c: totalColumns - 1 },
      },
    ];

    monthOffset = 0;
    ganttMonthSegments.forEach((segment) => {
      if (segment.span > 1) {
        merges.push({
          s: { r: monthHeaderRowIndex, c: leftColumnCount + monthOffset },
          e: {
            r: monthHeaderRowIndex,
            c: leftColumnCount + monthOffset + segment.span - 1,
          },
        });
      }
      monthOffset += segment.span;
    });

    weekOffset = 0;
    ganttWeekSegments.forEach((segment) => {
      if (segment.span > 1) {
        merges.push({
          s: { r: weekHeaderRowIndex, c: leftColumnCount + weekOffset },
          e: {
            r: weekHeaderRowIndex,
            c: leftColumnCount + weekOffset + segment.span - 1,
          },
        });
      }
      weekOffset += segment.span;
    });

    taskSpanMerges.forEach((span) => {
      if (span.endColumn > span.startColumn) {
        merges.push({
          s: { r: span.rowIndex, c: span.startColumn },
          e: { r: span.rowIndex, c: span.endColumn },
        });
      }
    });

    worksheet['!cols'] = [
      { wch: 7 },
      { wch: 32 },
      { wch: 15 },
      { wch: 16 },
      { wch: 12 },
      { wch: 12 },
      { wch: 10 },
      { wch: 30 },
      ...ganttColumns.map(() => ({ wch: 4.4 })),
    ];
    worksheet['!rows'] = [
      { hpt: 28 },
      { hpt: 18 },
      { hpt: 22 },
      { hpt: 22 },
      { hpt: 10 },
      { hpt: 22 },
      { hpt: 20 },
      { hpt: 22 },
      ...visibleTimelineItems.map(() => ({ hpt: 26 })),
    ];
    worksheet['!merges'] = merges;
    worksheet['!autofilter'] = {
      ref: XLSX.utils.encode_range({
        s: { r: dayHeaderRowIndex, c: 0 },
        e: { r: dayHeaderRowIndex, c: leftColumnCount - 1 },
      }),
    };
    worksheet['!freeze'] = {
      xSplit: leftColumnCount,
      ySplit: bodyStartRowIndex,
      topLeftCell: XLSX.utils.encode_cell({
        r: bodyStartRowIndex,
        c: leftColumnCount,
      }),
      activePane: 'bottomRight',
      state: 'frozen',
    };

    const titleStyle = {
      font: { bold: true, sz: 16, color: { rgb: '0F172A' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'DBEAFE' } },
      alignment: { horizontal: 'left', vertical: 'center' },
      border: { bottom: { style: 'thin', color: { rgb: 'BFDBFE' } } },
    };
    const metaLabelStyle = {
      font: { bold: true, color: { rgb: '334155' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'E2E8F0' } },
      alignment: { horizontal: 'left', vertical: 'center' },
      border: { bottom: { style: 'thin', color: { rgb: 'CBD5E1' } } },
    };
    const metaValueStyle = {
      font: { color: { rgb: '0F172A' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'F8FAFC' } },
      alignment: { horizontal: 'left', vertical: 'center' },
      border: { bottom: { style: 'thin', color: { rgb: 'CBD5E1' } } },
    };
    const leftHeaderStyle = {
      font: { bold: true, color: { rgb: 'FFFFFF' } },
      fill: { patternType: 'solid', fgColor: { rgb: '1E3A8A' } },
      alignment: { horizontal: 'center', vertical: 'center' },
      border: {
        right: { style: 'thin', color: { rgb: 'BFDBFE' } },
        bottom: { style: 'thin', color: { rgb: 'BFDBFE' } },
      },
    };
    const monthHeaderStyle = {
      font: { bold: true, color: { rgb: '334155' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'E2E8F0' } },
      alignment: { horizontal: 'center', vertical: 'center' },
      border: {
        right: { style: 'thin', color: { rgb: 'CBD5E1' } },
        bottom: { style: 'thin', color: { rgb: 'CBD5E1' } },
      },
    };
    const weekHeaderStyle = {
      font: { bold: true, color: { rgb: '475569' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'F1F5F9' } },
      alignment: { horizontal: 'center', vertical: 'center' },
      border: {
        right: { style: 'thin', color: { rgb: 'CBD5E1' } },
        bottom: { style: 'thin', color: { rgb: 'CBD5E1' } },
      },
    };
    const dayHeaderStyle = {
      font: { bold: true, color: { rgb: '334155' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'F8FAFC' } },
      alignment: { horizontal: 'center', vertical: 'center' },
      border: {
        right: { style: 'thin', color: { rgb: 'E2E8F0' } },
        bottom: { style: 'thin', color: { rgb: 'CBD5E1' } },
      },
    };
    const baseCellStyle = {
      alignment: { vertical: 'center' },
      border: {
        right: { style: 'thin', color: { rgb: 'E2E8F0' } },
        bottom: { style: 'thin', color: { rgb: 'E2E8F0' } },
      },
    };
    const legendLabelStyle = {
      font: { bold: true, color: { rgb: '0F172A' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'F8FAFC' } },
      alignment: { horizontal: 'center', vertical: 'center' },
      border: {
        right: { style: 'thin', color: { rgb: 'E2E8F0' } },
        bottom: { style: 'thin', color: { rgb: 'E2E8F0' } },
      },
    };

    const setCellStyle = (
      rowIndex: number,
      columnIndex: number,
      style: any
    ) => {
      const address = XLSX.utils.encode_cell({ r: rowIndex, c: columnIndex });
      if (!worksheet[address]) {
        worksheet[address] = { t: 's', v: '' };
      }
      worksheet[address].s = style;
    };

    const weekBandByIndex = ganttColumns.reduce<number[]>(
      (bands, _, dateIndex) => {
        let spanOffset = 0;
        let bandIndex = 0;
        for (let index = 0; index < ganttWeekSegments.length; index += 1) {
          const segment = ganttWeekSegments[index];
          if (
            dateIndex >= spanOffset &&
            dateIndex < spanOffset + segment.span
          ) {
            bandIndex = index;
            break;
          }
          spanOffset += segment.span;
        }
        bands.push(bandIndex);
        return bands;
      },
      []
    );

    setCellStyle(titleRowIndex, 0, titleStyle);
    [metaRowOneIndex, metaRowTwoIndex].forEach((rowIndex) => {
      [0, 3, 6].forEach((columnIndex) =>
        setCellStyle(rowIndex, columnIndex, metaLabelStyle)
      );
      [1, 4, 7].forEach((columnIndex) =>
        setCellStyle(rowIndex, columnIndex, metaValueStyle)
      );
    });
    [
      { columnIndex: 0, fill: 'E2E8F0', color: '0F172A' },
      { columnIndex: 1, fill: 'DBEAFE', color: '1D4ED8' },
      { columnIndex: 2, fill: 'DCFCE7', color: '047857' },
      { columnIndex: 3, fill: 'CFFAFE', color: '0F766E' },
      { columnIndex: 4, fill: 'FEF3C7', color: 'A16207' },
      { columnIndex: 5, fill: 'DBEAFE', color: '2563EB' },
    ].forEach(({ columnIndex, fill, color }) => {
      setCellStyle(legendRowIndex, columnIndex, {
        ...legendLabelStyle,
        fill: { patternType: 'solid', fgColor: { rgb: fill } },
        font: { bold: true, color: { rgb: color } },
      });
    });

    for (let columnIndex = 0; columnIndex < leftColumnCount; columnIndex += 1) {
      setCellStyle(dayHeaderRowIndex, columnIndex, leftHeaderStyle);
    }
    for (
      let columnIndex = leftColumnCount;
      columnIndex < totalColumns;
      columnIndex += 1
    ) {
      setCellStyle(monthHeaderRowIndex, columnIndex, monthHeaderStyle);
      setCellStyle(weekHeaderRowIndex, columnIndex, weekHeaderStyle);
      setCellStyle(dayHeaderRowIndex, columnIndex, dayHeaderStyle);
      if (columnIndex - leftColumnCount === todayIndex) {
        [monthHeaderRowIndex, weekHeaderRowIndex, dayHeaderRowIndex].forEach(
          (rowIndex) => {
            const baseStyle =
              rowIndex === monthHeaderRowIndex
                ? monthHeaderStyle
                : rowIndex === weekHeaderRowIndex
                  ? weekHeaderStyle
                  : dayHeaderStyle;
            setCellStyle(rowIndex, columnIndex, {
              ...baseStyle,
              border: {
                ...baseStyle.border,
                left: { style: 'medium', color: { rgb: '2563EB' } },
              },
            });
          }
        );
      }
    }

    visibleTimelineItems.forEach((item, index) => {
      const rowIndex = bodyStartRowIndex + index;
      const rowFill = index % 2 === 0 ? 'FFFFFF' : 'F8FAFC';
      const palette = getGanttPalette(item);
      const itemStart = new Date(item.plannedStartDate as string).setHours(
        0,
        0,
        0,
        0
      );
      const itemEnd = new Date(item.plannedEndDate as string).setHours(
        0,
        0,
        0,
        0
      );
      const durationDays = Math.max(
        1,
        Math.round((itemEnd - itemStart) / 86400000) + 1
      );
      const linkedMilestones = project.milestones.filter(
        (milestone) => milestone.workItemId === item.id && milestone.targetDate
      );
      const itemAssignee = getResolvedUserLabel(
        item.assignedToUserId,
        item.assignedToUserDisplayName
      );
      const workComponentContext = [
        item.projectPhaseName,
        item.projectPackageName,
      ]
        .filter(Boolean)
        .join(' / ');
      const commentsLabel =
        linkedMilestones.length > 0
          ? linkedMilestones
              .map(
                (milestone) =>
                  `${milestone.title} (${formatDateLabel(milestone.targetDate)})`
              )
              .join(', ')
          : workComponentContext || item.nodeType;

      for (
        let columnIndex = 0;
        columnIndex < leftColumnCount;
        columnIndex += 1
      ) {
        setCellStyle(rowIndex, columnIndex, {
          ...baseCellStyle,
          fill: {
            patternType: 'solid',
            fgColor: {
              rgb:
                item.nodeType === 'Phase'
                  ? 'E0F2FE'
                  : item.nodeType === 'Workstream'
                    ? 'DCFCE7'
                    : rowFill,
            },
          },
          font: {
            bold: item.nodeType === 'Phase' || item.nodeType === 'Workstream',
          },
        });
      }
      setCellStyle(rowIndex, 2, {
        ...baseCellStyle,
        fill: {
          patternType: 'solid',
          fgColor: {
            rgb:
              item.nodeType === 'Phase'
                ? 'DBEAFE'
                : item.nodeType === 'Workstream'
                  ? 'DCFCE7'
                  : toExcelColor(palette.soft),
          },
        },
        font: {
          bold: true,
          color: {
            rgb:
              item.nodeType === 'Phase' || item.nodeType === 'Workstream'
                ? toExcelColor(palette.border)
                : '334155',
          },
        },
      });
      const assigneeAddress = XLSX.utils.encode_cell({ r: rowIndex, c: 3 });
      if (worksheet[assigneeAddress]) {
        worksheet[assigneeAddress].v = itemAssignee;
      }
      const commentsAddress = XLSX.utils.encode_cell({ r: rowIndex, c: 7 });
      if (worksheet[commentsAddress]) {
        worksheet[commentsAddress].v = commentsLabel;
      }

      ganttColumns.forEach((column, dateIndex) => {
        const columnIndex = leftColumnCount + dateIndex;
        const columnTime = new Date(column.date).setHours(0, 0, 0, 0);
        const weekBand = weekBandByIndex[dateIndex];
        const baseFill =
          item.nodeType === 'Phase'
            ? weekBand % 2 === 0
              ? 'EFF6FF'
              : 'DBEAFE'
            : item.nodeType === 'Workstream'
              ? weekBand % 2 === 0
                ? 'F0FDF4'
                : 'DCFCE7'
              : weekBand % 2 === 0
                ? 'F8FAFC'
                : 'F1F5F9';
        const cellStyle: any = {
          ...baseCellStyle,
          alignment: {
            horizontal: 'center',
            vertical: 'center',
            wrapText: true,
          },
          fill: { patternType: 'solid', fgColor: { rgb: baseFill } },
        };

        if (dateIndex === todayIndex) {
          cellStyle.border = {
            ...cellStyle.border,
            left: { style: 'medium', color: { rgb: '2563EB' } },
          };
        }

        if (columnTime >= itemStart && columnTime <= itemEnd) {
          const isRollup = !!ganttChildrenLookup.get(item.id);
          const fillColor = isRollup
            ? toExcelColor(palette.soft)
            : toExcelColor(palette.start);
          cellStyle.fill = {
            patternType: 'solid',
            fgColor: { rgb: fillColor },
          };
          cellStyle.font = {
            bold: true,
            color: {
              rgb: isRollup
                ? toExcelColor(palette.border)
                : toExcelColor(palette.text),
            },
          };
          cellStyle.border = {
            ...cellStyle.border,
            top: {
              style: isRollup ? 'medium' : 'thin',
              color: { rgb: toExcelColor(palette.border) },
            },
            bottom: {
              style: isRollup ? 'medium' : 'thin',
              color: { rgb: toExcelColor(palette.border) },
            },
          };
          const address = XLSX.utils.encode_cell({
            r: rowIndex,
            c: columnIndex,
          });
          if (!worksheet[address]) {
            worksheet[address] = { t: 's', v: '' };
          }
        }

        const linkedMilestone = linkedMilestones.find(
          (milestone) =>
            new Date(milestone.targetDate).setHours(0, 0, 0, 0) === columnTime
        );
        if (linkedMilestone) {
          const address = XLSX.utils.encode_cell({
            r: rowIndex,
            c: columnIndex,
          });
          if (!worksheet[address]) {
            worksheet[address] = { t: 's', v: '' };
          }
          worksheet[address].v = '◆';
          cellStyle.fill = { patternType: 'solid', fgColor: { rgb: 'FEF3C7' } };
          cellStyle.font = { bold: true, color: { rgb: 'A16207' } };
          cellStyle.border = {
            ...cellStyle.border,
            top: { style: 'thin', color: { rgb: 'D97706' } },
            bottom: { style: 'thin', color: { rgb: 'D97706' } },
            left: { style: 'thin', color: { rgb: 'D97706' } },
            right: { style: 'thin', color: { rgb: 'D97706' } },
          };
        }

        setCellStyle(rowIndex, columnIndex, cellStyle);
      });
    });

    taskSpanMerges.forEach((span) => {
      for (
        let columnIndex = span.startColumn;
        columnIndex <= span.endColumn;
        columnIndex += 1
      ) {
        const address = XLSX.utils.encode_cell({
          r: span.rowIndex,
          c: columnIndex,
        });
        if (!worksheet[address]) {
          worksheet[address] = { t: 's', v: '' };
        }
        worksheet[address].s = {
          alignment: { horizontal: 'left', vertical: 'center' },
          fill: {
            patternType: 'solid',
            fgColor: {
              rgb: span.isRollup
                ? toExcelColor(span.palette.soft)
                : toExcelColor(span.palette.start),
            },
          },
          font: {
            bold: true,
            color: {
              rgb: span.isRollup
                ? toExcelColor(span.palette.border)
                : toExcelColor(span.palette.text),
            },
          },
          border: {
            top: {
              style: span.isRollup ? 'medium' : 'thin',
              color: { rgb: toExcelColor(span.palette.border) },
            },
            bottom: {
              style: span.isRollup ? 'medium' : 'thin',
              color: { rgb: toExcelColor(span.palette.border) },
            },
            left:
              columnIndex === span.startColumn
                ? {
                    style: 'medium',
                    color: { rgb: toExcelColor(span.palette.border) },
                  }
                : {
                    style: 'thin',
                    color: { rgb: toExcelColor(span.palette.border) },
                  },
            right:
              columnIndex === span.endColumn
                ? {
                    style: 'medium',
                    color: { rgb: toExcelColor(span.palette.border) },
                  }
                : {
                    style: 'thin',
                    color: { rgb: toExcelColor(span.palette.border) },
                  },
          },
        };
      }

      const startAddress = XLSX.utils.encode_cell({
        r: span.rowIndex,
        c: span.startColumn,
      });
      worksheet[startAddress].v = span.label;
    });

    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, 'Project Plan');
    const milestoneSheetRows: Array<Array<string | number>> = [
      ['Milestones'],
      ['Title', 'Task', 'Target Date', 'Status'],
      ...project.milestones.map((milestone) => [
        milestone.title,
        flat.find((item) => item.id === milestone.workItemId)?.title ||
          'General',
        formatDateLabel(milestone.targetDate),
        milestone.status,
      ]),
    ];
    const milestoneSheet = XLSX.utils.aoa_to_sheet(
      milestoneSheetRows
    ) as XLSX.WorkSheet & Record<string, any>;
    milestoneSheet['!cols'] = [
      { wch: 30 },
      { wch: 32 },
      { wch: 16 },
      { wch: 16 },
    ];
    XLSX.utils.book_append_sheet(workbook, milestoneSheet, 'Milestones');
    XLSX.writeFile(workbook, `${project.projectCode}_GanttPlan.xlsx`);
  };
  const scrollGanttChart = (delta: number) => {
    ganttChartScrollRef.current?.scrollBy({ left: delta, behavior: 'smooth' });
  };
  const syncGanttScroll = (source: 'chart' | 'bottom') => {
    if (ganttScrollSyncRef.current) {
      return;
    }

    ganttScrollSyncRef.current = true;
    const sourceElement =
      source === 'chart'
        ? ganttChartScrollRef.current
        : ganttBottomScrollRef.current;
    const targetElement =
      source === 'chart'
        ? ganttBottomScrollRef.current
        : ganttChartScrollRef.current;

    if (sourceElement && targetElement) {
      targetElement.scrollLeft = sourceElement.scrollLeft;
    }

    requestAnimationFrame(() => {
      ganttScrollSyncRef.current = false;
    });
  };
  const handleGanttChartWheel: React.WheelEventHandler<HTMLDivElement> = (
    event
  ) => {
    if (!ganttChartScrollRef.current) {
      return;
    }

    if (Math.abs(event.deltaY) > Math.abs(event.deltaX)) {
      event.preventDefault();
      ganttChartScrollRef.current.scrollBy({
        left: event.deltaY,
        behavior: 'auto',
      });
    }
  };
  const methodologyOptions = useMemo(
    () =>
      resolveCatalogOptions(
        methodologyCatalog,
        DEFAULT_METHODOLOGIES,
        overview.methodology
      ),
    [methodologyCatalog, overview.methodology]
  );
  const billingTypeOptions = useMemo(
    () =>
      resolveCatalogOptions(
        billingTypeCatalog,
        DEFAULT_BILLING_TYPES,
        billing.billingType
      ),
    [billingTypeCatalog, billing.billingType]
  );
  const fundingSourceOptions = useMemo(
    () =>
      resolveCatalogOptions(
        fundingSourceCatalog,
        DEFAULT_FUNDING_SOURCES,
        overview.fundingSource
      ),
    [fundingSourceCatalog, overview.fundingSource]
  );
  const resourceRoleOptions = useMemo(
    () =>
      resolveCatalogOptions(
        resourceRoleCatalog,
        DEFAULT_RESOURCE_ROLES,
        resource.allocationRole
      ),
    [resourceRoleCatalog, resource.allocationRole]
  );
  const memberRoleOptions = useMemo(
    () =>
      resolveCatalogOptions(
        memberRoleCatalog,
        DEFAULT_MEMBER_ROLES,
        member.role
      ),
    [memberRoleCatalog, member.role]
  );
  const taskStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        taskStatusCatalog,
        DEFAULT_TASK_STATUSES,
        work.status
      ),
    [taskStatusCatalog, work.status]
  );
  const taskPriorityOptions = useMemo(
    () =>
      resolveCatalogOptions(
        taskPriorityCatalog,
        DEFAULT_TASK_PRIORITIES,
        work.priority
      ),
    [taskPriorityCatalog, work.priority]
  );
  const deliverableStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        deliverableStatusCatalog,
        DEFAULT_DELIVERABLE_STATUSES,
        deliverable.status
      ),
    [deliverableStatusCatalog, deliverable.status]
  );
  const boqItemTypeOptions = useMemo(
    () =>
      resolveCatalogOptions(
        boqItemTypeCatalog,
        DEFAULT_BOQ_ITEM_TYPES,
        boqItemDraft.itemType
      ),
    [boqItemDraft.itemType, boqItemTypeCatalog]
  );
  const riskStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        riskStatusCatalog,
        DEFAULT_RISK_STATUSES,
        risk.status
      ),
    [riskStatusCatalog, risk.status]
  );
  const riskCategoryOptions = useMemo(
    () =>
      resolveCatalogOptions(
        riskCategoryCatalog,
        DEFAULT_RISK_CATEGORIES,
        risk.category
      ),
    [riskCategoryCatalog, risk.category]
  );
  const riskResponseStrategyOptions = useMemo(
    () =>
      resolveCatalogOptions(
        riskResponseStrategyCatalog,
        DEFAULT_RISK_RESPONSE_STRATEGIES,
        risk.responseStrategy
      ),
    [riskResponseStrategyCatalog, risk.responseStrategy]
  );
  const qualityCheckpointStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        qualityCheckpointStatusCatalog,
        DEFAULT_QUALITY_CHECKPOINT_STATUSES,
        qualityCheckpoint.status
      ),
    [qualityCheckpointStatusCatalog, qualityCheckpoint.status]
  );
  const nonConformanceStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        nonConformanceStatusCatalog,
        DEFAULT_NON_CONFORMANCE_STATUSES,
        nonConformance.status
      ),
    [nonConformanceStatusCatalog, nonConformance.status]
  );
  const nonConformanceSeverityOptions = useMemo(
    () =>
      resolveCatalogOptions(
        nonConformanceSeverityCatalog,
        DEFAULT_NON_CONFORMANCE_SEVERITIES,
        nonConformance.severity
      ),
    [nonConformanceSeverityCatalog, nonConformance.severity]
  );
  const expenseCategoryOptions = useMemo(
    () =>
      resolveCatalogOptions(
        expenseCategoryCatalog,
        DEFAULT_EXPENSE_CATEGORIES,
        expense.category
      ),
    [expenseCategoryCatalog, expense.category]
  );
  const invoiceCurrencyOptions = useMemo(
    () =>
      buildProjectCurrencyOptions(currencies, baseCurrency, invoice.currency),
    [baseCurrency, currencies, invoice.currency]
  );
  const packageCurrencyOptions = useMemo(
    () =>
      buildProjectCurrencyOptions(
        currencies,
        baseCurrency,
        projectPackageDraft.currency
      ),
    [baseCurrency, currencies, projectPackageDraft.currency]
  );
  const boqCurrencyOptions = useMemo(
    () =>
      buildProjectCurrencyOptions(
        currencies,
        baseCurrency,
        boqItemDraft.currency
      ),
    [baseCurrency, currencies, boqItemDraft.currency]
  );
  const projectCurrencyOptions = useMemo(
    () =>
      buildProjectCurrencyOptions(
        currencies,
        baseCurrency,
        overview.baseCurrencyCode || project?.baseCurrencyCode
      ),
    [
      baseCurrency,
      currencies,
      overview.baseCurrencyCode,
      project?.baseCurrencyCode,
    ]
  );
  const commercialAdminCurrencyOptions = useMemo(
    () =>
      Array.from(
        new Set([
          ...projectCurrencyOptions,
          ...buildProjectCurrencyOptions(
            currencies,
            baseCurrency,
            variationOrderDraft.currency
          ),
          ...buildProjectCurrencyOptions(
            currencies,
            baseCurrency,
            interimValuationDraft.currency
          ),
          ...buildProjectCurrencyOptions(
            currencies,
            baseCurrency,
            paymentCertificateDraft.currency
          ),
        ])
      ),
    [
      baseCurrency,
      currencies,
      interimValuationDraft.currency,
      paymentCertificateDraft.currency,
      projectCurrencyOptions,
      variationOrderDraft.currency,
    ]
  );
  const expenseCurrencyOptions = useMemo(
    () =>
      buildProjectCurrencyOptions(currencies, baseCurrency, expense.currency),
    [baseCurrency, currencies, expense.currency]
  );
  const siteInstructionCurrencyOptions = useMemo(
    () =>
      buildProjectCurrencyOptions(
        currencies,
        baseCurrency,
        siteInstructionDraft.currency
      ),
    [baseCurrency, currencies, siteInstructionDraft.currency]
  );
  const changeTypeOptions = useMemo(
    () =>
      resolveCatalogOptions(
        changeTypeCatalog,
        DEFAULT_CHANGE_TYPES,
        change.changeType
      ),
    [changeTypeCatalog, change.changeType]
  );
  const issueStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        issueStatusCatalog,
        DEFAULT_ISSUE_STATUSES,
        issue.status
      ),
    [issueStatusCatalog, issue.status]
  );
  const changeStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        changeStatusCatalog,
        DEFAULT_CHANGE_STATUSES,
        change.status
      ),
    [changeStatusCatalog, change.status]
  );
  const issueSeverityOptions = useMemo(
    () =>
      resolveCatalogOptions(
        issueSeverityCatalog,
        DEFAULT_ISSUE_SEVERITIES,
        issue.severity
      ),
    [issueSeverityCatalog, issue.severity]
  );
  const timesheetWorkTypeOptions = useMemo(
    () =>
      resolveCatalogOptions(
        timesheetWorkTypeCatalog,
        DEFAULT_TIMESHEET_WORK_TYPES,
        timesheet.workType
      ),
    [timesheetWorkTypeCatalog, timesheet.workType]
  );
  const decisionStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        decisionStatusCatalog,
        DEFAULT_DECISION_STATUSES,
        decision.status
      ),
    [decisionStatusCatalog, decision.status]
  );
  const meetingTypeOptions = useMemo(
    () =>
      resolveCatalogOptions(
        meetingTypeCatalog,
        DEFAULT_MEETING_TYPES,
        meeting.meetingType
      ),
    [meetingTypeCatalog, meeting.meetingType]
  );
  const actionItemStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        actionItemStatusCatalog,
        DEFAULT_ACTION_ITEM_STATUSES,
        actionItem.status
      ),
    [actionItemStatusCatalog, actionItem.status]
  );
  const actionItemPriorityOptions = useMemo(
    () =>
      resolveCatalogOptions(
        actionItemPriorityCatalog,
        DEFAULT_ACTION_ITEM_PRIORITIES,
        actionItem.priority
      ),
    [actionItemPriorityCatalog, actionItem.priority]
  );
  const lessonCategoryOptions = useMemo(
    () =>
      resolveCatalogOptions(
        lessonCategoryCatalog,
        DEFAULT_LESSON_CATEGORIES,
        lessonLearned.category
      ),
    [lessonCategoryCatalog, lessonLearned.category]
  );
  const lessonVisibilityOptions = useMemo(
    () =>
      resolveCatalogOptions(
        lessonVisibilityCatalog,
        DEFAULT_LESSON_VISIBILITIES,
        lessonLearned.visibility
      ),
    [lessonVisibilityCatalog, lessonLearned.visibility]
  );
  const assetLinkTypeOptions = useMemo(
    () =>
      resolveCatalogOptions(
        assetLinkTypeCatalog,
        DEFAULT_ASSET_LINK_TYPES,
        assetLink.linkType
      ),
    [assetLinkTypeCatalog, assetLink.linkType]
  );
  const assetLinkStatusOptions = useMemo(
    () =>
      resolveCatalogOptions(
        assetLinkStatusCatalog,
        DEFAULT_ASSET_LINK_STATUSES,
        assetLink.status
      ),
    [assetLinkStatusCatalog, assetLink.status]
  );
  const documentCategoryOptions = useMemo(
    () =>
      resolveCatalogOptions(
        documentCategoryCatalog,
        DEFAULT_DOCUMENT_CATEGORIES,
        doc.category
      ),
    [documentCategoryCatalog, doc.category]
  );
  const documentTypeOptions = useMemo(
    () =>
      resolveCatalogOptions(
        documentTypeCatalog,
        DEFAULT_DOCUMENT_TYPES,
        doc.documentType
      ),
    [documentTypeCatalog, doc.documentType]
  );
  const approvalTypeOptions = useMemo(() => {
    const values = DEFAULT_APPROVAL_TYPES;
    return approvalDraft.approvalType &&
      !values.includes(approvalDraft.approvalType)
      ? [approvalDraft.approvalType, ...values]
      : values;
  }, [approvalDraft.approvalType]);
  const approvalStatusOptions = useMemo(() => {
    const values = DEFAULT_APPROVAL_STATUSES;
    return approvalDraft.status && !values.includes(approvalDraft.status)
      ? [approvalDraft.status, ...values]
      : values;
  }, [approvalDraft.status]);
  const projectUnitTypeOptions = useMemo(() => {
    const values = Array.from(
      new Set([
        ...DEFAULT_PROJECT_UNIT_TYPES,
        ...unitTypeTemplates
          .map((item) => item.defaultProjectUnitType)
          .filter(Boolean),
      ])
    );
    return unitDraft.unitType && !values.includes(unitDraft.unitType)
      ? [unitDraft.unitType, ...values]
      : values;
  }, [unitDraft.unitType, unitTypeTemplates]);
  const projectUnitStatusOptions = useMemo(() => {
    const values = DEFAULT_PROJECT_UNIT_STATUSES;
    return unitDraft.status && !values.includes(unitDraft.status)
      ? [unitDraft.status, ...values]
      : values;
  }, [unitDraft.status]);
  const customerVariationStatusOptions = useMemo(() => {
    const values = DEFAULT_PROJECT_CUSTOMER_VARIATION_STATUSES;
    return customerVariationDraft.status &&
      !values.includes(customerVariationDraft.status)
      ? [customerVariationDraft.status, ...values]
      : values;
  }, [customerVariationDraft.status]);
  const customerVariationTimingOptions = useMemo(() => {
    const values = DEFAULT_PROJECT_CUSTOMER_VARIATION_TIMINGS;
    return customerVariationDraft.timing &&
      !values.includes(customerVariationDraft.timing)
      ? [customerVariationDraft.timing, ...values]
      : values;
  }, [customerVariationDraft.timing]);
  const activeUsers = useMemo(
    () =>
      users.filter((user) => user.isActive && USER_ID_PATTERN.test(user.id)),
    [users]
  );
  const customerBusinessPartners = useMemo(
    () =>
      [...customerPartnerOptions].sort((left, right) =>
        formatBusinessPartnerLabel(left).localeCompare(
          formatBusinessPartnerLabel(right)
        )
      ),
    [customerPartnerOptions]
  );
  const activeContracts = useMemo(
    () =>
      contracts.filter(
        (contract) =>
          !overview.businessPartnerId ||
          contract.businessPartnerId === overview.businessPartnerId
      ),
    [contracts, overview.businessPartnerId]
  );
  const portfolioLookup = useMemo(
    () =>
      new Map(portfolios.map((portfolio) => [portfolio.id, portfolio.name])),
    [portfolios]
  );
  const programLookup = useMemo(
    () => new Map(programs.map((program) => [program.id, program.name])),
    [programs]
  );
  const selectedPortfolioLabel = overview.portfolioId
    ? portfolioLookup.get(overview.portfolioId) ||
      project?.portfolioName ||
      overview.portfolioId
    : 'No portfolio';
  const selectedProgramLabel = overview.programId
    ? programLookup.get(overview.programId) ||
      project?.programName ||
      overview.programId
    : 'No program';
  const userLookup = useMemo(
    () => new Map(users.map((user) => [user.id, formatUserLabel(user)])),
    [users]
  );
  const getResolvedUserLabel = (
    userId?: string,
    displayName?: string,
    fallback: string = 'Unassigned'
  ) => {
    if (displayName?.trim()) {
      return displayName;
    }

    if (userId) {
      return userLookup.get(userId) || 'Unknown user';
    }

    return fallback;
  };
  const formatMoney = (
    value: number | undefined,
    currency?: string | null,
    maximumFractionDigits: number = 0
  ) =>
    formatProjectMoney(
      value ?? 0,
      currency,
      baseCurrency.code,
      maximumFractionDigits
    );
  const getCurrencyOptionLabel = (code: string) =>
    formatProjectCurrencyLabel(
      findProjectCurrency(currencies, code, baseCurrency),
      code
    );
  const orderedMaterialRequisitions = useMemo(
    () =>
      [...materialRequisitions].sort((left, right) => {
        const leftRank = MATERIAL_STATUS_ORDER.indexOf(
          normalizeRequisitionStatus(left.status)
        );
        const rightRank = MATERIAL_STATUS_ORDER.indexOf(
          normalizeRequisitionStatus(right.status)
        );
        if (leftRank !== rightRank) {
          return leftRank - rightRank;
        }

        return (
          new Date(right.requestDate).getTime() -
          new Date(left.requestDate).getTime()
        );
      }),
    [materialRequisitions]
  );
  const materialSnapshot = useMemo(() => {
    const pendingApproval = materialRequisitions.filter(
      (item) => normalizeRequisitionStatus(item.status) === 2
    ).length;
    const pendingIssue = materialRequisitions.filter((item) =>
      [3, 4, 5].includes(normalizeRequisitionStatus(item.status))
    ).length;
    const completed = materialRequisitions.filter(
      (item) => normalizeRequisitionStatus(item.status) === 7
    ).length;
    const totalValue = materialRequisitions.reduce(
      (sum, item) => sum + (item.totalValue || 0),
      0
    );
    return { pendingApproval, pendingIssue, completed, totalValue };
  }, [materialRequisitions]);
  const externalArtifactOptions = useMemo(() => {
    if (!project) return [];
    switch (externalPolicy.artifactType) {
      case 'WorkItem':
        return flat.map((item) => ({ id: item.id, label: item.title }));
      case 'Deliverable':
        return project.deliverables.map((item) => ({
          id: item.id,
          label: item.title,
        }));
      case 'Document':
        return project.documents.map((item) => ({
          id: item.id,
          label: item.documentName,
        }));
      default:
        return [];
    }
  }, [externalPolicy.artifactType, flat, project]);

  const act = async (
    fn: () => Promise<unknown>,
    message: string,
    reset?: () => void
  ) => {
    try {
      await fn();
      reset?.();
      await load();
      toast.success(message);
    } catch (error: any) {
      toast.error(error.message || message);
    }
  };

  const actWithBusyKey = async (
    busyKey: string,
    fn: () => Promise<unknown>,
    message: string
  ) => {
    try {
      setFollowThroughBusyKey(busyKey);
      await fn();
      await load();
      toast.success(message);
    } catch (error: any) {
      toast.error(error.message || message);
    } finally {
      setFollowThroughBusyKey((current) =>
        current === busyKey ? null : current
      );
    }
  };

  const submitInvoiceRequest = async (invoiceRequestId: string) => {
    await act(
      () => projectService.submitInvoiceRequest(invoiceRequestId),
      'Invoice request submitted'
    );
  };

  const sendInvoiceRequestToFinance = async (
    invoiceRequest: ProjectInvoiceRequestDto
  ) => {
    const defaultReference =
      invoiceRequest.externalReference || invoiceRequest.requestNumber;
    const externalReference = window.prompt(
      'Finance reference / exported invoice number',
      defaultReference
    );
    if (externalReference === null) {
      return;
    }

    await act(
      () =>
        projectService
          .sendInvoiceRequestToFinance(invoiceRequest.id, {
            externalReference: externalReference.trim() || undefined,
          })
          .then(() => Promise.resolve()),
      'Invoice request sent to finance'
    );
  };

  const markInvoiceRequestInvoiced = async (
    invoiceRequest: ProjectInvoiceRequestDto
  ) => {
    const defaultReference =
      invoiceRequest.externalReference || invoiceRequest.requestNumber;
    const externalReference = window.prompt(
      'Invoice number / finance reference',
      defaultReference
    );
    if (externalReference === null) {
      return;
    }

    await act(
      () =>
        projectService
          .markInvoiceRequestInvoiced(invoiceRequest.id, {
            externalReference: externalReference.trim() || undefined,
          })
          .then(() => Promise.resolve()),
      'Invoice request marked as invoiced'
    );
  };

  const markInvoiceRequestPaid = async (invoiceRequestId: string) => {
    await act(
      () =>
        projectService
          .markInvoiceRequestPaid(invoiceRequestId)
          .then(() => Promise.resolve()),
      'Invoice request marked as paid'
    );
  };

  const openMaterialDialog = (
    mode: 'create' | 'edit' | 'view',
    requisitionId?: string
  ) => {
    setMaterialDialogMode(mode);
    setSelectedMaterialRequisitionId(requisitionId);
    setMaterialDialogOpen(true);
  };

  const openIssueDialog = (requisitionId: string) => {
    setMaterialIssueRequisitionId(requisitionId);
    setMaterialIssueDialogOpen(true);
  };

  const openReturnDialog = (requisitionId: string) => {
    setMaterialReturnRequisitionId(requisitionId);
    setMaterialReturnDialogOpen(true);
  };

  const getLoadedProject = () => {
    if (!project) {
      throw new Error('Project workspace is still loading');
    }

    return project;
  };

  const getProjectId = () => getLoadedProject().id;

  const refreshAnalysis = () =>
    act(() => loadAnalysisData(getProjectId()), 'Schedule analysis refreshed');
  const createBaseline = () =>
    act(
      () =>
        projectService
          .createBaseline(getProjectId(), baseline)
          .then(() => Promise.resolve()),
      'Baseline created',
      () => setBaseline(baselineInit)
    );
  const compareBaseline = async (baselineId: string) => {
    try {
      setBaselineComparison(await projectService.compareBaseline(baselineId));
    } catch (error: any) {
      toast.error(error.message || 'Failed to compare baseline');
    }
  };
  const generateRevenueRecognition = () =>
    act(
      () =>
        projectService
          .generateRevenueRecognition(getProjectId())
          .then(() => Promise.resolve()),
      'Revenue recognition generated'
    );
  const refreshMaterials = () =>
    act(() => loadMaterials(getProjectId()), 'Project materials refreshed');
  const submitMaterialRequisition = (requisitionId: string) =>
    act(
      () => inventoryRequisitionService.submit(requisitionId),
      'Material requisition submitted'
    );
  const completeMaterialRequisition = (requisitionId: string) =>
    act(
      () => inventoryRequisitionService.complete(requisitionId),
      'Material requisition completed'
    );
  const addAssetLink = () =>
    act(
      () =>
        projectService
          .addAssetLink(getProjectId(), assetLink)
          .then(() => Promise.resolve()),
      'Asset link added',
      () => setAssetLink(assetLinkInit)
    );
  const saveExternalAccessPolicy = () =>
    act(
      () =>
        projectService
          .upsertExternalAccessPolicy(getProjectId(), externalPolicy)
          .then(() => Promise.resolve()),
      'External access policy saved',
      () => setExternalPolicy(externalPolicyInit)
    );
  const deleteExternalAccessPolicy = (policyId: string) =>
    act(
      () => projectService.deleteExternalAccessPolicy(policyId),
      'External access policy deleted'
    );
  const attachDocument = () =>
    act(
      async () => {
        if (docFile) {
          await projectService.uploadProjectDocument(getProjectId(), docFile, {
            artifactType: doc.artifactType,
            artifactId: doc.artifactId,
            documentName: doc.documentName || docFile.name,
            category: doc.category,
            documentType: doc.documentType,
            versionLabel: doc.versionLabel,
            status: doc.status,
            isExternalVisible: doc.isExternalVisible,
          });
          return;
        }

        if (!doc.filePath) {
          throw new Error('Provide a file path or upload a file');
        }

        await projectService.attachDocument(getProjectId(), doc);
      },
      'Document attached',
      () => {
        setDoc(docInit);
        setDocFile(null);
      }
    );
  const deleteDocument = (documentId: string) =>
    act(() => projectService.deleteDocument(documentId), 'Document deleted');
  const postComment = () =>
    act(
      () =>
        projectService
          .addComment(getProjectId(), comment)
          .then(() => Promise.resolve()),
      'Comment added',
      () => setComment(commentInit)
    );
  const openWorkflowAdmin = () => router.push('/administration/workflow');
  const handlePortfolioChange = async (value: string) => {
    const portfolioId = value === 'none' ? undefined : value;
    setOverview((current) => ({
      ...current,
      portfolioId,
      programId: undefined,
    }));
    setPrograms(await projectService.getPrograms(portfolioId));
  };
  const addMember = () =>
    act(
      () =>
        projectService
          .addMember(getProjectId(), member)
          .then(() => Promise.resolve()),
      'Member added',
      () => setMember(memberInit)
    );
  const removeMember = (memberId: string) =>
    act(() => projectService.removeMember(memberId), 'Member removed');
  const addBillingSchedule = () =>
    act(
      () =>
        projectService
          .addBillingSchedule(getProjectId(), billing)
          .then(() => Promise.resolve()),
      'Billing schedule added',
      () => setBilling(billingInit)
    );
  const handleInvoiceScheduleChange = (value: string) => {
    const selected = getLoadedProject().billingSchedules.find(
      (item) => item.id === value
    );
    setInvoice((current) => ({
      ...current,
      billingScheduleId: value === 'none' ? undefined : value,
      requestedAmount: selected?.amount ?? current.requestedAmount,
    }));
  };
  const addInvoiceRequest = () =>
    act(
      () =>
        projectService
          .createInvoiceRequest(getProjectId(), {
            ...invoice,
            currency: invoice.currency || baseCurrency.code,
          })
          .then(() => Promise.resolve()),
      'Invoice request created',
      () => setInvoice(invoiceInit)
    );
  const addProjectPhase = () => saveProjectPhase();
  const advanceProjectPhase = async (
    phase: ProjectPhaseDto,
    evaluation?: ProjectPhaseGateEvaluationDto
  ) => {
    try {
      let overrideStageGate = false;
      if (
        phase.status === 'InProgress' &&
        (evaluation?.blockingFailureCount ?? 0) > 0
      ) {
        overrideStageGate = window.confirm(
          `This phase still has ${evaluation?.blockingFailureCount ?? 0} blocking gate requirement(s). Override the gate and complete the phase anyway?`
        );
        if (!overrideStageGate) {
          return;
        }
      }

      const result = await projectService.advanceProjectPhase(phase.id, {
        overrideStageGate,
        startNextPhase: true,
      });

      if (editingProjectPhaseId === phase.id) {
        resetProjectPhaseEditor();
      }

      await load();
      toast.success(result.message || 'Project phase advanced');
    } catch (error: any) {
      toast.error(error.message || 'Failed to advance project phase');
    }
  };
  const deleteProjectPhase = (phaseId: string) =>
    act(
      () => projectService.deleteProjectPhase(phaseId),
      'Project phase deleted',
      () => {
        if (editingProjectPhaseId === phaseId) {
          resetProjectPhaseEditor();
        }
      }
    );
  const addProjectPackage = async () => {
    let saved = false;
    const packagePayload = {
      ...projectPackageDraft,
      committedAmount: undefined,
      actualAmount: undefined,
      currency: projectPackageDraft.currency || baseCurrency.code,
    };
    await act(
      () =>
        (editingProjectPackageId
          ? projectService.updateProjectPackage(
              editingProjectPackageId,
              packagePayload
            )
          : projectService.addProjectPackage(getProjectId(), packagePayload)
        ).then(() => {
          saved = true;
          return Promise.resolve();
        }),
      editingProjectPackageId
        ? 'Work component updated'
        : 'Work component added',
      resetProjectPackageEditor
    );
    return saved;
  };
  const deleteProjectPackage = (packageId: string) =>
    act(
      () => projectService.deleteProjectPackage(packageId),
      'Work component deleted',
      () => {
        if (editingProjectPackageId === packageId) {
          resetProjectPackageEditor();
        }
        if (
          editingProjectBoqItemId &&
          project?.boqItems.some(
            (item) =>
              item.id === editingProjectBoqItemId &&
              item.projectPackageId === packageId
          )
        ) {
          resetProjectBoqItemEditor();
        }
        setBoqItemDraft((current) =>
          current.projectPackageId === packageId
            ? { ...boqItemInit, currency: baseCurrency.code }
            : current
        );
      }
    );
  const addProjectBoqItem = async () => {
    let saved = false;
    const boqPayload = {
      ...boqItemDraft,
      committedAmount: undefined,
      actualAmount: undefined,
      currency: boqItemDraft.currency || baseCurrency.code,
    };
    await act(
      () =>
        (editingProjectBoqItemId
          ? projectService.updateProjectBoqItem(
              editingProjectBoqItemId,
              boqPayload
            )
          : projectService.addProjectBoqItem(getProjectId(), boqPayload)
        ).then(() => {
          saved = true;
          return Promise.resolve();
        }),
      editingProjectBoqItemId ? 'BOQ item updated' : 'BOQ item added',
      () => {
        setEditingProjectBoqItemId(null);
        setBoqItemDraft((current) => ({
          ...boqItemInit,
          currency: baseCurrency.code,
          projectPackageId:
            current.projectPackageId || project?.packages[0]?.id || '',
        }));
      }
    );
    return saved;
  };
  const saveProjectBoqBudgetWorksheet = async (
    updates: ProjectBoqBudgetWorksheetUpdate[]
  ) => {
    if (!project || updates.length === 0) {
      return false;
    }

    let saved = false;
    await act(async () => {
      const updateRequests = updates.map((update) => {
        const currentItem = project.boqItems.find(
          (item) => item.id === update.boqItemId
        );
        if (!currentItem) {
          throw new Error(
            'One or more BOQ items could not be found while saving the budget worksheet.'
          );
        }

        return projectService.updateProjectBoqItem(update.boqItemId, {
          projectPackageId: currentItem.projectPackageId,
          sectionCatalogEntryId: currentItem.sectionCatalogEntryId,
          tradeCatalogEntryId: currentItem.tradeCatalogEntryId,
          costCodeCatalogEntryId: currentItem.costCodeCatalogEntryId,
          measurementCodeCatalogEntryId:
            currentItem.measurementCodeCatalogEntryId,
          lineNumber: currentItem.lineNumber,
          itemCode: currentItem.itemCode,
          itemType: currentItem.itemType,
          description: currentItem.description,
          quantity: currentItem.quantity,
          unitOfMeasure: currentItem.unitOfMeasure,
          unitRate: currentItem.unitRate,
          budgetQuantity: update.budgetQuantity,
          budgetUnitRate: update.budgetUnitRate,
          budgetAmount: update.budgetAmount,
          committedAmount: undefined,
          actualAmount: undefined,
          forecastAmount: currentItem.forecastAmount,
          currency: currentItem.currency || baseCurrency.code,
          inventoryItemId: currentItem.inventoryItemId,
          tenderItemId: currentItem.tenderItemId,
          procurementPlanItemId: currentItem.procurementPlanItemId,
          purchaseRequisitionItemId: currentItem.purchaseRequisitionItemId,
          purchaseOrderItemId: currentItem.purchaseOrderItemId,
          notes: currentItem.notes,
          sortOrder: currentItem.sortOrder,
        });
      });

      await Promise.all(updateRequests);
      saved = true;
    }, 'Budget worksheet updated');

    return saved;
  };
  const deleteProjectBoqItem = (boqItemId: string) =>
    act(
      () => projectService.deleteProjectBoqItem(boqItemId),
      'BOQ item deleted',
      () => {
        if (editingProjectBoqItemId === boqItemId) {
          resetProjectBoqItemEditor();
        }
      }
    );
  const addApprovalRegisterItem = () =>
    act(
      () =>
        projectService
          .addApprovalRegisterItem(getProjectId(), approvalDraft)
          .then(() => Promise.resolve()),
      'Approval register item added',
      () =>
        setApprovalDraft((current) => ({
          ...approvalRegisterItemInit,
          approvalType:
            current.approvalType || approvalRegisterItemInit.approvalType,
          status: current.status || approvalRegisterItemInit.status,
          isRequired: current.isRequired ?? approvalRegisterItemInit.isRequired,
          projectPhaseId: current.projectPhaseId,
        }))
    );
  const deleteApprovalRegisterItem = (approvalRegisterItemId: string) =>
    act(
      () => projectService.deleteApprovalRegisterItem(approvalRegisterItemId),
      'Approval register item deleted'
    );
  const addProjectDrawing = () =>
    act(
      () =>
        projectService
          .addProjectDrawing(getProjectId(), drawingDraft)
          .then(() => Promise.resolve()),
      'Project drawing saved',
      () =>
        setDrawingDraft((current) => ({
          ...drawingInit,
          discipline: current.discipline || drawingInit.discipline,
          status: current.status || drawingInit.status,
          projectPhaseId: current.projectPhaseId,
        }))
    );
  const deleteProjectDrawing = (drawingId: string) =>
    act(
      () => projectService.deleteProjectDrawing(drawingId),
      'Project drawing deleted'
    );
  const addProjectSubmittal = () =>
    act(
      () =>
        projectService
          .addProjectSubmittal(getProjectId(), submittalDraft)
          .then(() => Promise.resolve()),
      'Project submittal saved',
      () =>
        setSubmittalDraft((current) => ({
          ...submittalInit,
          submittalType: current.submittalType || submittalInit.submittalType,
          status: current.status || submittalInit.status,
          projectPhaseId: current.projectPhaseId,
          projectPackageId: current.projectPackageId,
        }))
    );
  const deleteProjectSubmittal = (submittalId: string) =>
    act(
      () => projectService.deleteProjectSubmittal(submittalId),
      'Project submittal deleted'
    );
  const addProjectRfi = () =>
    act(
      () =>
        projectService
          .addProjectRfi(getProjectId(), rfiDraft)
          .then(() => Promise.resolve()),
      'Project RFI saved',
      () =>
        setRfiDraft((current) => ({
          ...rfiInit,
          priority: current.priority || rfiInit.priority,
          status: current.status || rfiInit.status,
          projectPhaseId: current.projectPhaseId,
          projectPackageId: current.projectPackageId,
        }))
    );
  const deleteProjectRfi = (rfiId: string) =>
    act(() => projectService.deleteProjectRfi(rfiId), 'Project RFI deleted');
  const addProjectSiteInstruction = () =>
    act(
      () =>
        projectService
          .addProjectSiteInstruction(getProjectId(), {
            ...siteInstructionDraft,
            currency: siteInstructionDraft.currency || baseCurrency.code,
          })
          .then(() => Promise.resolve()),
      'Project site instruction saved',
      () =>
        setSiteInstructionDraft((current) => ({
          ...siteInstructionInit,
          currency: current.currency || baseCurrency.code,
          instructionType:
            current.instructionType || siteInstructionInit.instructionType,
          status: current.status || siteInstructionInit.status,
          projectPhaseId: current.projectPhaseId,
          projectPackageId: current.projectPackageId,
        }))
    );
  const deleteProjectSiteInstruction = (siteInstructionId: string) =>
    act(
      () => projectService.deleteProjectSiteInstruction(siteInstructionId),
      'Project site instruction deleted'
    );
  const saveVariationOrder = () =>
    act(
      () =>
        (editingVariationOrderId
          ? projectService.updateProjectVariationOrder(
              editingVariationOrderId,
              {
                ...variationOrderDraft,
                currency: variationOrderDraft.currency || baseCurrency.code,
              }
            )
          : projectService.addProjectVariationOrder(getProjectId(), {
              ...variationOrderDraft,
              currency: variationOrderDraft.currency || baseCurrency.code,
            })
        ).then(() => Promise.resolve()),
      editingVariationOrderId
        ? 'Variation order updated'
        : 'Variation order added',
      resetVariationOrderEditor
    );
  const deleteVariationOrder = (variationOrderId: string) =>
    act(
      () => projectService.deleteProjectVariationOrder(variationOrderId),
      'Variation order deleted',
      () => {
        if (editingVariationOrderId === variationOrderId) {
          resetVariationOrderEditor();
        }
      }
    );
  const saveInterimValuation = () =>
    act(
      () =>
        (editingInterimValuationId
          ? projectService.updateProjectInterimValuation(
              editingInterimValuationId,
              {
                ...interimValuationDraft,
                currency: interimValuationDraft.currency || baseCurrency.code,
              }
            )
          : projectService.addProjectInterimValuation(getProjectId(), {
              ...interimValuationDraft,
              currency: interimValuationDraft.currency || baseCurrency.code,
            })
        ).then(() => Promise.resolve()),
      editingInterimValuationId
        ? 'Interim valuation updated'
        : 'Interim valuation added',
      resetInterimValuationEditor
    );
  const deleteInterimValuation = (valuationId: string) =>
    act(
      () => projectService.deleteProjectInterimValuation(valuationId),
      'Interim valuation deleted',
      () => {
        if (editingInterimValuationId === valuationId) {
          resetInterimValuationEditor();
        }
      }
    );
  const savePaymentCertificate = () =>
    act(
      () =>
        (editingPaymentCertificateId
          ? projectService.updateProjectPaymentCertificate(
              editingPaymentCertificateId,
              {
                ...paymentCertificateDraft,
                currency: paymentCertificateDraft.currency || baseCurrency.code,
              }
            )
          : projectService.addProjectPaymentCertificate(getProjectId(), {
              ...paymentCertificateDraft,
              currency: paymentCertificateDraft.currency || baseCurrency.code,
            })
        ).then(() => Promise.resolve()),
      editingPaymentCertificateId
        ? 'Payment certificate updated'
        : 'Payment certificate added',
      resetPaymentCertificateEditor
    );
  const deletePaymentCertificate = (certificateId: string) =>
    act(
      () => projectService.deleteProjectPaymentCertificate(certificateId),
      'Payment certificate deleted',
      () => {
        if (editingPaymentCertificateId === certificateId) {
          resetPaymentCertificateEditor();
        }
      }
    );
  const saveExtensionOfTime = () =>
    act(
      () =>
        (editingExtensionOfTimeId
          ? projectService.updateProjectExtensionOfTimeRequest(
              editingExtensionOfTimeId,
              extensionOfTimeDraft
            )
          : projectService.addProjectExtensionOfTimeRequest(
              getProjectId(),
              extensionOfTimeDraft
            )
        ).then(() => Promise.resolve()),
      editingExtensionOfTimeId ? 'EOT request updated' : 'EOT request added',
      resetExtensionOfTimeEditor
    );
  const deleteExtensionOfTime = (extensionOfTimeId: string) =>
    act(
      () =>
        projectService.deleteProjectExtensionOfTimeRequest(extensionOfTimeId),
      'EOT request deleted',
      () => {
        if (editingExtensionOfTimeId === extensionOfTimeId) {
          resetExtensionOfTimeEditor();
        }
      }
    );
  const addProjectBuilding = (dto: CreateProjectBuildingDto) =>
    act(
      () =>
        projectService
          .addProjectBuilding(getProjectId(), dto)
          .then(() => Promise.resolve()),
      'Project building added'
    );
  const deleteProjectBuilding = (buildingId: string) =>
    act(
      () => projectService.deleteProjectBuilding(buildingId),
      'Project building deleted',
      () => {
        setUnitDraft((current) =>
          current.projectBuildingId === buildingId
            ? {
                ...current,
                projectBuildingId: undefined,
                projectFloorId: undefined,
                projectUnitReleaseBatchId: undefined,
              }
            : current
        );
      }
    );
  const addProjectFloor = (dto: CreateProjectFloorDto) =>
    act(
      () =>
        projectService
          .addProjectFloor(getProjectId(), dto)
          .then(() => Promise.resolve()),
      'Project floor added'
    );
  const deleteProjectFloor = (floorId: string) =>
    act(
      () => projectService.deleteProjectFloor(floorId),
      'Project floor deleted',
      () => {
        setUnitDraft((current) =>
          current.projectFloorId === floorId
            ? {
                ...current,
                projectFloorId: undefined,
                projectUnitReleaseBatchId: undefined,
              }
            : current
        );
      }
    );
  const addProjectUnitReleaseBatch = (dto: CreateProjectUnitReleaseBatchDto) =>
    act(
      () =>
        projectService
          .addProjectUnitReleaseBatch(getProjectId(), dto)
          .then(() => Promise.resolve()),
      'Unit release batch added'
    );
  const deleteProjectUnitReleaseBatch = (unitReleaseBatchId: string) =>
    act(
      () => projectService.deleteProjectUnitReleaseBatch(unitReleaseBatchId),
      'Unit release batch deleted',
      () => {
        setUnitDraft((current) =>
          current.projectUnitReleaseBatchId === unitReleaseBatchId
            ? { ...current, projectUnitReleaseBatchId: undefined }
            : current
        );
      }
    );
  const saveProjectUnit = () =>
    act(
      () =>
        (editingUnitId
          ? projectService.updateProjectUnit(editingUnitId, {
              ...unitDraft,
              currency: unitDraft.currency || baseCurrency.code,
            })
          : projectService.addProjectUnit(getProjectId(), {
              ...unitDraft,
              currency: unitDraft.currency || baseCurrency.code,
            })
        ).then(() => Promise.resolve()),
      editingUnitId ? 'Project unit updated' : 'Project unit added',
      resetUnitEditor
    );
  const releaseProjectUnit = (unitId: string) =>
    actWithBusyKey(
      `unit-release:${unitId}`,
      () => projectService.releaseProjectUnit(unitId),
      'Project unit released for market'
    );
  const withdrawProjectUnitRelease = (unitId: string) =>
    actWithBusyKey(
      `unit-withdraw-release:${unitId}`,
      () => projectService.withdrawProjectUnitRelease(unitId),
      'Project unit withdrawn from market'
    );
  const publishProjectUnitToEstate = (unitId: string) =>
    actWithBusyKey(
      `unit-publish-estate:${unitId}`,
      () => projectService.publishProjectUnitToEstate(unitId),
      'Project unit published to Estate property and facilities management.'
    );
  const createSalesAgreementFromProjectUnit = (unitId: string) =>
    actWithBusyKey(
      `unit-sales-agreement:${unitId}`,
      () => projectService.createSalesAgreementFromProjectUnit(unitId),
      'Sales agreement created and linked to the unit. It is now visible in Sales > Agreements.'
    );
  const createLeaseAgreementFromProjectUnit = (unitId: string) =>
    actWithBusyKey(
      `unit-lease-agreement:${unitId}`,
      () => projectService.createLeaseAgreementFromProjectUnit(unitId),
      'Lease agreement created and linked to the unit. It is now visible in Sales > Agreements.'
    );
  const createSalesOrderFromProjectUnit = (unitId: string) =>
    actWithBusyKey(
      `unit-sales-order:${unitId}`,
      () => projectService.createSalesOrderFromProjectUnit(unitId),
      'Sales order created and linked to the unit. It is now visible in Sales > Orders.'
    );
  const deleteProjectUnit = (unitId: string) =>
    act(
      () => projectService.deleteProjectUnit(unitId),
      'Project unit deleted',
      () => {
        if (editingUnitId === unitId) {
          resetUnitEditor();
        }
        setCustomerVariationDraft((current) =>
          current.projectUnitId === unitId
            ? { ...customerVariationInit, currency: baseCurrency.code }
            : current
        );
      }
    );
  const saveCustomerVariation = () =>
    act(
      () =>
        editingCustomerVariationId
          ? projectService
              .updateCustomerVariation(editingCustomerVariationId, {
                ...customerVariationDraft,
                currency: customerVariationDraft.currency || baseCurrency.code,
              })
              .then(() => Promise.resolve())
          : projectService
              .addCustomerVariation(getProjectId(), {
                ...customerVariationDraft,
                currency: customerVariationDraft.currency || baseCurrency.code,
              })
              .then(() => Promise.resolve()),
      editingCustomerVariationId
        ? 'Customer variation updated'
        : 'Customer variation added',
      resetCustomerVariationEditor
    );
  const deleteCustomerVariation = (variationId: string) =>
    act(
      () => projectService.deleteCustomerVariation(variationId),
      'Customer variation deleted',
      () => {
        if (editingCustomerVariationId === variationId) {
          resetCustomerVariationEditor();
        }
      }
    );
  const createCustomerVariationJobCard = (variationId: string) =>
    actWithBusyKey(
      `variation-job-card:${variationId}`,
      () => projectService.createCustomerVariationJobCard(variationId),
      'Job card created from customer variation'
    );
  const createCustomerVariationWorkOrder = (variationId: string) =>
    actWithBusyKey(
      `variation-work-order:${variationId}`,
      () => projectService.createCustomerVariationWorkOrder(variationId),
      'Work order created from customer variation'
    );
  const addCommissioningItem = () =>
    act(
      () =>
        projectService
          .addProjectCommissioningItem(getProjectId(), commissioningDraft)
          .then(() => Promise.resolve()),
      'Commissioning item added',
      () =>
        setCommissioningDraft((current) => ({
          ...commissioningInit,
          projectUnitId: current.projectUnitId,
        }))
    );
  const deleteCommissioningItem = (commissioningItemId: string) =>
    act(
      () => projectService.deleteProjectCommissioningItem(commissioningItemId),
      'Commissioning item deleted'
    );
  const addHandoverItem = () =>
    act(
      () =>
        projectService
          .addProjectHandoverItem(getProjectId(), handoverDraft)
          .then(() => Promise.resolve()),
      'Handover item added',
      () =>
        setHandoverDraft((current) => ({
          ...handoverItemInit,
          projectUnitId: current.projectUnitId,
          projectUnitHandoverBatchId: current.projectUnitHandoverBatchId,
        }))
    );
  const addProjectUnitHandoverBatch = (
    dto: CreateProjectUnitHandoverBatchDto
  ) =>
    act(
      () =>
        projectService
          .addProjectUnitHandoverBatch(getProjectId(), dto)
          .then(() => Promise.resolve()),
      'Unit handover batch added'
    );
  const deleteProjectUnitHandoverBatch = (unitHandoverBatchId: string) =>
    act(
      () => projectService.deleteProjectUnitHandoverBatch(unitHandoverBatchId),
      'Unit handover batch deleted',
      () => {
        setHandoverDraft((current) =>
          current.projectUnitHandoverBatchId === unitHandoverBatchId
            ? { ...current, projectUnitHandoverBatchId: undefined }
            : current
        );
      }
    );
  const deleteHandoverItem = (handoverItemId: string) =>
    act(
      () => projectService.deleteProjectHandoverItem(handoverItemId),
      'Handover item deleted'
    );
  const saveSnagItem = () =>
    act(
      () =>
        editingSnagItemId
          ? projectService
              .updateProjectSnagItem(editingSnagItemId, snagDraft)
              .then(() => Promise.resolve())
          : projectService
              .addProjectSnagItem(getProjectId(), snagDraft)
              .then(() => Promise.resolve()),
      editingSnagItemId ? 'Snag item updated' : 'Snag item added',
      resetSnagEditor
    );
  const deleteSnagItem = (snagItemId: string) =>
    act(
      () => projectService.deleteProjectSnagItem(snagItemId),
      'Snag item deleted',
      () => {
        if (editingSnagItemId === snagItemId) {
          resetSnagEditor();
        }
      }
    );
  const saveDefectLiabilityCase = () =>
    act(
      () =>
        editingDefectLiabilityCaseId
          ? projectService
              .updateProjectDefectLiabilityCase(editingDefectLiabilityCaseId, {
                ...defectLiabilityDraft,
                currency: defectLiabilityDraft.currency || baseCurrency.code,
              })
              .then(() => Promise.resolve())
          : projectService
              .addProjectDefectLiabilityCase(getProjectId(), {
                ...defectLiabilityDraft,
                currency: defectLiabilityDraft.currency || baseCurrency.code,
              })
              .then(() => Promise.resolve()),
      editingDefectLiabilityCaseId
        ? 'Defect liability case updated'
        : 'Defect liability case added',
      resetDefectLiabilityEditor
    );
  const deleteDefectLiabilityCase = (defectLiabilityCaseId: string) =>
    act(
      () =>
        projectService.deleteProjectDefectLiabilityCase(defectLiabilityCaseId),
      'Defect liability case deleted',
      () => {
        if (editingDefectLiabilityCaseId === defectLiabilityCaseId) {
          resetDefectLiabilityEditor();
        }
      }
    );
  const createDefectLiabilityJobCard = (defectLiabilityCaseId: string) =>
    actWithBusyKey(
      `defect-job-card:${defectLiabilityCaseId}`,
      () => projectService.createDefectLiabilityJobCard(defectLiabilityCaseId),
      'Job card created from defect liability case'
    );
  const createDefectLiabilityWorkOrder = (defectLiabilityCaseId: string) =>
    actWithBusyKey(
      `defect-work-order:${defectLiabilityCaseId}`,
      () =>
        projectService.createDefectLiabilityWorkOrder(defectLiabilityCaseId),
      'Work order created from defect liability case'
    );
  const generateInvoiceRequestFromSchedule = (billingScheduleId: string) =>
    act(
      () =>
        projectService.generateInvoiceRequestFromSchedule(billingScheduleId),
      'Invoice request generated'
    );
  const deleteBillingSchedule = (billingScheduleId: string) =>
    act(
      () => projectService.deleteBillingSchedule(billingScheduleId),
      'Billing schedule deleted'
    );
  const createBudgetRevision = () =>
    act(
      () =>
        projectService
          .createBudgetRevision(getProjectId(), budgetRevision)
          .then(() => Promise.resolve()),
      'Budget revision created',
      () =>
        setBudgetRevision({
          ...budgetRevisionInit,
          estimatedBudget: overview.estimatedBudget ?? 0,
          approvedBudget:
            overview.approvedBudget ?? overview.estimatedBudget ?? 0,
          forecastCost: financialSummary?.forecastCost ?? 0,
        })
    );
  const createForecastVersion = () =>
    act(
      () =>
        projectService
          .createForecastVersion(getProjectId(), forecastVersion)
          .then(() => Promise.resolve()),
      'Forecast version created',
      () =>
        setForecastVersion({
          ...forecastVersionInit,
          forecastCost: financialSummary?.forecastCost ?? 0,
          estimateAtCompletion: financialSummary?.estimateAtCompletion ?? 0,
          forecastMargin: financialSummary?.grossMargin ?? 0,
        })
    );
  const activateForecastVersion = (forecastVersionId: string) =>
    act(
      () =>
        projectService
          .activateForecastVersion(forecastVersionId)
          .then(() => Promise.resolve()),
      'Forecast version activated'
    );
  const deleteWorkItem = (workItemId: string) =>
    act(
      () => projectService.deleteWorkItem(workItemId),
      'Work item deleted',
      () => {
        if (editingWorkItemId === workItemId) {
          resetWorkEditor();
        }
      }
    );
  const addTaskDependency = () =>
    act(
      () =>
        projectService
          .addTaskDependency(getProjectId(), dependency)
          .then(() => Promise.resolve()),
      'Dependency added',
      () => setDependency(dependencyInit)
    );
  const deleteTaskDependency = (dependencyId: string) =>
    act(
      () => projectService.deleteTaskDependency(dependencyId),
      'Dependency deleted'
    );
  const addMilestone = () =>
    act(
      () =>
        projectService
          .addMilestone(getProjectId(), milestone)
          .then(() => Promise.resolve()),
      'Milestone added',
      () => setMilestone(milestoneInit)
    );
  const deleteMilestone = (milestoneId: string) =>
    act(() => projectService.deleteMilestone(milestoneId), 'Milestone deleted');
  const addResourceAllocation = () =>
    act(
      () =>
        projectService
          .addResourceAllocation(getProjectId(), resource)
          .then(() => Promise.resolve()),
      'Resource allocation added',
      () => setResource(resourceInit)
    );
  const approveResourceAllocation = (resourceAllocationId: string) =>
    act(
      () =>
        projectService
          .approveResourceAllocation(resourceAllocationId)
          .then(() => Promise.resolve()),
      'Resource allocation approved'
    );
  const deleteResourceAllocation = (resourceAllocationId: string) =>
    act(
      () => projectService.deleteResourceAllocation(resourceAllocationId),
      'Resource allocation deleted'
    );
  const addDeliverable = () =>
    act(
      () =>
        projectService
          .addDeliverable(getProjectId(), deliverable)
          .then(() => Promise.resolve()),
      'Deliverable added',
      () => setDeliverable(deliverableInit)
    );
  const deleteDeliverable = (deliverableId: string) =>
    act(
      () => projectService.deleteDeliverable(deliverableId),
      'Deliverable deleted'
    );
  const addTimesheet = () =>
    act(
      () =>
        projectService
          .addTimesheet(getProjectId(), timesheet)
          .then(() => Promise.resolve()),
      'Timesheet entry added',
      () => setTimesheet({ ...timesheetInit, userId: currentUserId })
    );
  const approveTimesheet = (timesheetId: string) =>
    act(
      () =>
        projectService
          .approveTimesheet(timesheetId)
          .then(() => Promise.resolve()),
      'Timesheet approved'
    );
  const addExpense = () =>
    act(
      () =>
        projectService
          .addExpense(getProjectId(), {
            ...expense,
            currency: expense.currency || baseCurrency.code,
          })
          .then(() => Promise.resolve()),
      'Expense added',
      () => setExpense({ ...expenseInit, userId: currentUserId })
    );
  const approveExpense = (expenseId: string) =>
    act(
      () =>
        projectService.approveExpense(expenseId).then(() => Promise.resolve()),
      'Expense approved'
    );

  if (loading || !project)
    return (
      <div className="py-20 text-center text-muted-foreground">
        Loading project workspace...
      </div>
    );

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => router.push('/development/projects')}
          >
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-3xl font-bold tracking-tight">
                {project.title}
              </h1>
              <Badge variant="outline">{project.projectCode}</Badge>
              <Badge>{project.status}</Badge>
            </div>
            <p className="text-muted-foreground">
              {project.summary || 'No summary provided.'}
            </p>
            {(project.portfolioName || project.programName) && (
              <p className="text-sm text-muted-foreground">
                {[project.portfolioName, project.programName]
                  .filter(Boolean)
                  .join(' / ')}
              </p>
            )}
          </div>
        </div>
        <Button
          onClick={() =>
            act(
              () =>
                projectService
                  .updateProject(project.id, overview)
                  .then(() => Promise.resolve()),
              'Project updated'
            )
          }
        >
          <Save className="mr-2 h-4 w-4" />
          Save
        </Button>
      </div>

      <Tabs
        value={activeTab}
        onValueChange={navigateToTab}
        className="space-y-6"
      >
        <TabsList className="flex h-auto w-full flex-wrap justify-start gap-2 bg-transparent p-0">
          {workspaceTabs.map((tab) => (
            <TabsTrigger
              key={tab}
              value={tab}
              className="border bg-muted/60 px-3 py-2 data-[state=active]:border-primary data-[state=active]:bg-background"
            >
              {PROJECT_WORKSPACE_TAB_LABELS[tab]}
            </TabsTrigger>
          ))}
        </TabsList>

        <TabsContent value="overview" className="space-y-6">
          <ProjectOverviewTab
            project={project}
            overview={overview}
            setOverview={setOverview}
            financialSummary={financialSummary}
            integrationSummary={integrationSummary}
            types={types}
            priorities={priorities}
            templates={templates}
            portfolios={portfolios}
            programs={programs}
            selectedPortfolioLabel={selectedPortfolioLabel}
            selectedProgramLabel={selectedProgramLabel}
            methodologyOptions={methodologyOptions}
            fundingSourceOptions={fundingSourceOptions}
            activeBusinessPartners={businessPartners}
            activeContracts={contracts}
            activeUsers={activeUsers}
            member={member}
            setMember={setMember}
            memberRoleOptions={memberRoleOptions}
            billing={billing}
            setBilling={setBilling}
            billingTypeOptions={billingTypeOptions}
            invoice={invoice}
            setInvoice={setInvoice}
            baseCurrencyCode={baseCurrency.code}
            projectCurrencyOptions={projectCurrencyOptions}
            invoiceCurrencyOptions={invoiceCurrencyOptions}
            budgetRevision={budgetRevision}
            setBudgetRevision={setBudgetRevision}
            budgetRevisions={budgetRevisions}
            forecastVersion={forecastVersion}
            setForecastVersion={setForecastVersion}
            forecastVersions={forecastVersions}
            expandedBudgetRevisionHistoryId={expandedBudgetRevisionHistoryId}
            setExpandedBudgetRevisionHistoryId={
              setExpandedBudgetRevisionHistoryId
            }
            boolValue={boolValue}
            formatMoney={formatMoney}
            formatDateLabel={formatDateLabel}
            formatCatalogLabel={formatCatalogLabel}
            formatUserLabel={formatUserLabel}
            formatBusinessPartnerLabel={formatBusinessPartnerLabel}
            formatContractLabel={formatContractLabel}
            getResolvedUserLabel={getResolvedUserLabel}
            getCurrencyOptionLabel={getCurrencyOptionLabel}
            today={today}
            onLoad={load}
            onOpenWorkflows={openWorkflowAdmin}
            onPortfolioChange={handlePortfolioChange}
            onAddMember={addMember}
            onRemoveMember={removeMember}
            onAddBillingSchedule={addBillingSchedule}
            onInvoiceScheduleChange={handleInvoiceScheduleChange}
            onAddInvoiceRequest={addInvoiceRequest}
            onSubmitInvoiceRequest={submitInvoiceRequest}
            onSendInvoiceRequestToFinance={sendInvoiceRequestToFinance}
            onMarkInvoiceRequestInvoiced={markInvoiceRequestInvoiced}
            onMarkInvoiceRequestPaid={markInvoiceRequestPaid}
            onGenerateInvoiceRequestFromSchedule={
              generateInvoiceRequestFromSchedule
            }
            onDeleteBillingSchedule={deleteBillingSchedule}
            onCreateBudgetRevision={createBudgetRevision}
            onCreateForecastVersion={createForecastVersion}
            onActivateForecastVersion={activateForecastVersion}
          />
        </TabsContent>
        <TabsContent value="phases" className="space-y-6">
          <ProjectPhasesTab
            phases={project.phases}
            phaseGateEvaluations={phaseGateEvaluations}
            phaseDraft={projectPhaseDraft}
            editingPhaseId={editingProjectPhaseId}
            onPhaseDraftChange={setProjectPhaseDraft}
            onSavePhase={addProjectPhase}
            onDeletePhase={deleteProjectPhase}
            onEditPhase={beginEditProjectPhase}
            onAdvancePhase={advanceProjectPhase}
            onCancelEdit={resetProjectPhaseEditor}
          />
        </TabsContent>
        <TabsContent value="design" className="space-y-6">
          <ProjectDesignTab
            projectId={project.id}
            drawings={project.drawings}
            submittals={project.submittals}
            phases={project.phases}
            packages={project.packages}
            drawingDraft={drawingDraft}
            setDrawingDraft={setDrawingDraft}
            submittalDraft={submittalDraft}
            setSubmittalDraft={setSubmittalDraft}
            drawingStatusOptions={DEFAULT_PROJECT_DRAWING_STATUSES}
            drawingDisciplineOptions={DEFAULT_PROJECT_DRAWING_DISCIPLINES}
            submittalStatusOptions={DEFAULT_PROJECT_SUBMITTAL_STATUSES}
            submittalTypeOptions={DEFAULT_PROJECT_SUBMITTAL_TYPES}
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            onAddDrawing={addProjectDrawing}
            onDeleteDrawing={deleteProjectDrawing}
            onAddSubmittal={addProjectSubmittal}
            onDeleteSubmittal={deleteProjectSubmittal}
          />
        </TabsContent>
        <TabsContent value="plan" className="space-y-6">
          <ProjectPlanTab
            project={project}
            flat={flat}
            editingWorkItemId={editingWorkItemId}
            work={work}
            setWork={setWork}
            taskStatusOptions={taskStatusOptions}
            taskPriorityOptions={taskPriorityOptions}
            formatCatalogLabel={formatCatalogLabel}
            saveWorkEditor={saveWorkEditor}
            resetWorkEditor={resetWorkEditor}
            scheduleReasonRequired={scheduleReasonRequired}
            taskView={taskView}
            setTaskView={setTaskView}
            taskBoardStatuses={TASK_BOARD_STATUSES}
            taskBoardItems={taskBoardItems}
            timelineBounds={timelineBounds}
            ganttSummary={ganttSummary}
            workItemTitles={workItemTitles}
            formatDateLabel={formatDateLabel}
            onOpenGantt={() => setGanttDialogOpen(true)}
            onBeginEditWorkItem={beginEditWorkItem}
            onDeleteWorkItem={deleteWorkItem}
            dependency={dependency}
            setDependency={setDependency}
            boolValue={boolValue}
            onAddTaskDependency={addTaskDependency}
            onDeleteTaskDependency={deleteTaskDependency}
            milestone={milestone}
            setMilestone={setMilestone}
            onAddMilestone={addMilestone}
            onDeleteMilestone={deleteMilestone}
            resource={resource}
            setResource={setResource}
            activeUsers={activeUsers}
            formatUserLabel={formatUserLabel}
            resourceRoleOptions={resourceRoleOptions}
            resourceRoutingPolicies={DEFAULT_RESOURCE_ROUTING_POLICIES}
            getResolvedUserLabel={getResolvedUserLabel}
            onAddResourceAllocation={addResourceAllocation}
            onApproveResourceAllocation={approveResourceAllocation}
            onDeleteResourceAllocation={deleteResourceAllocation}
          />
        </TabsContent>
        <TabsContent value="packages" className="space-y-6">
          <ProjectPackagesTab
            project={project}
            phases={project.phases}
            packageDraft={projectPackageDraft}
            editingPackageId={editingProjectPackageId}
            setPackageDraft={setProjectPackageDraft}
            boqDraft={boqItemDraft}
            editingBoqItemId={editingProjectBoqItemId}
            setBoqDraft={setBoqItemDraft}
            activeBusinessPartners={businessPartners}
            activeContracts={contracts}
            tenders={tenderLookup}
            procurementPlanItems={procurementPlanItemLookup}
            purchaseRequisitions={purchaseRequisitionLookup}
            purchaseOrders={purchaseOrderLookup}
            inventoryItems={inventoryItems}
            packageTypeOptions={DEFAULT_PACKAGE_TYPES}
            packageStatusOptions={DEFAULT_PACKAGE_STATUSES}
            boqItemTypeOptions={boqItemTypeOptions}
            unitOfMeasures={unitsOfMeasure}
            packageCurrencyOptions={packageCurrencyOptions}
            boqCurrencyOptions={boqCurrencyOptions}
            getCurrencyOptionLabel={getCurrencyOptionLabel}
            formatMoney={formatMoney}
            formatCatalogLabel={formatCatalogLabel}
            onSavePackage={addProjectPackage}
            onEditPackage={beginEditProjectPackage}
            onCancelPackageEdit={resetProjectPackageEditor}
            onDeletePackage={deleteProjectPackage}
            onSaveBoqItem={addProjectBoqItem}
            onEditBoqItem={beginEditProjectBoqItem}
            onCancelBoqItemEdit={resetProjectBoqItemEditor}
            onDeleteBoqItem={deleteProjectBoqItem}
            onImportCompleted={load}
          />
        </TabsContent>
        <TabsContent value="budgeting" className="space-y-6">
          <ProjectBudgetingTab
            project={project}
            commercialSummary={commercialSummary}
            activeBusinessPartners={businessPartners}
            activeContracts={contracts}
            tenders={tenderLookup}
            procurementPlanItems={procurementPlanItemLookup}
            purchaseRequisitions={purchaseRequisitionLookup}
            purchaseOrders={purchaseOrderLookup}
            inventoryItems={inventoryItems}
            packageTypeOptions={DEFAULT_PACKAGE_TYPES}
            packageStatusOptions={DEFAULT_PACKAGE_STATUSES}
            boqItemTypeOptions={boqItemTypeOptions}
            unitOfMeasures={unitsOfMeasure}
            packageCurrencyOptions={packageCurrencyOptions}
            boqCurrencyOptions={boqCurrencyOptions}
            getCurrencyOptionLabel={getCurrencyOptionLabel}
            formatCatalogLabel={formatCatalogLabel}
            formatMoney={formatMoney}
            onSaveBudgetWorksheet={saveProjectBoqBudgetWorksheet}
          />
        </TabsContent>
        <TabsContent value="commercial" className="space-y-6">
          <ProjectCommercialTab
            commercialSummary={commercialSummary}
            formatMoney={formatMoney}
          />
        </TabsContent>
        <TabsContent value="commercial-admin" className="space-y-6">
          <ProjectCommercialAdminTab
            project={project}
            phases={project.phases}
            packages={project.packages}
            activeContracts={activeContracts}
            currencyOptions={commercialAdminCurrencyOptions}
            variationOrderDraft={variationOrderDraft}
            setVariationOrderDraft={setVariationOrderDraft}
            editingVariationOrderId={editingVariationOrderId}
            interimValuationDraft={interimValuationDraft}
            setInterimValuationDraft={setInterimValuationDraft}
            editingInterimValuationId={editingInterimValuationId}
            paymentCertificateDraft={paymentCertificateDraft}
            setPaymentCertificateDraft={setPaymentCertificateDraft}
            editingPaymentCertificateId={editingPaymentCertificateId}
            extensionOfTimeDraft={extensionOfTimeDraft}
            setExtensionOfTimeDraft={setExtensionOfTimeDraft}
            editingExtensionOfTimeId={editingExtensionOfTimeId}
            variationOrderStatusOptions={
              DEFAULT_PROJECT_VARIATION_ORDER_STATUSES
            }
            variationOrderTypeOptions={DEFAULT_PROJECT_VARIATION_ORDER_TYPES}
            interimValuationStatusOptions={
              DEFAULT_PROJECT_INTERIM_VALUATION_STATUSES
            }
            paymentCertificateStatusOptions={
              DEFAULT_PROJECT_PAYMENT_CERTIFICATE_STATUSES
            }
            extensionOfTimeStatusOptions={
              DEFAULT_PROJECT_EXTENSION_OF_TIME_STATUSES
            }
            formatCatalogLabel={formatCatalogLabel}
            getCurrencyOptionLabel={getCurrencyOptionLabel}
            formatDateLabel={formatDateLabel}
            formatMoney={formatMoney}
            onSaveVariationOrder={saveVariationOrder}
            onEditVariationOrder={beginEditVariationOrder}
            onCancelVariationOrderEdit={resetVariationOrderEditor}
            onDeleteVariationOrder={deleteVariationOrder}
            onSaveInterimValuation={saveInterimValuation}
            onEditInterimValuation={beginEditInterimValuation}
            onCancelInterimValuationEdit={resetInterimValuationEditor}
            onDeleteInterimValuation={deleteInterimValuation}
            onSavePaymentCertificate={savePaymentCertificate}
            onEditPaymentCertificate={beginEditPaymentCertificate}
            onCancelPaymentCertificateEdit={resetPaymentCertificateEditor}
            onDeletePaymentCertificate={deletePaymentCertificate}
            onSaveExtensionOfTime={saveExtensionOfTime}
            onEditExtensionOfTime={beginEditExtensionOfTime}
            onCancelExtensionOfTimeEdit={resetExtensionOfTimeEditor}
            onDeleteExtensionOfTime={deleteExtensionOfTime}
          />
        </TabsContent>
        <TabsContent value="approvals" className="space-y-6">
          <ProjectApprovalsTab
            project={project}
            phases={project.phases}
            approvalDraft={approvalDraft}
            setApprovalDraft={setApprovalDraft}
            approvalTypeOptions={approvalTypeOptions}
            approvalStatusOptions={approvalStatusOptions}
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            onAddApproval={addApprovalRegisterItem}
            onDeleteApproval={deleteApprovalRegisterItem}
          />
        </TabsContent>
        <TabsContent value="site-controls" className="space-y-6">
          <ProjectSiteControlsTab
            rfis={project.rfis}
            siteInstructions={project.siteInstructions}
            phases={project.phases}
            packages={project.packages}
            rfiDraft={rfiDraft}
            setRfiDraft={setRfiDraft}
            siteInstructionDraft={siteInstructionDraft}
            setSiteInstructionDraft={setSiteInstructionDraft}
            rfiStatusOptions={DEFAULT_PROJECT_RFI_STATUSES}
            rfiPriorityOptions={DEFAULT_PROJECT_RFI_PRIORITIES}
            siteInstructionStatusOptions={
              DEFAULT_PROJECT_SITE_INSTRUCTION_STATUSES
            }
            siteInstructionTypeOptions={DEFAULT_PROJECT_SITE_INSTRUCTION_TYPES}
            currencyOptions={siteInstructionCurrencyOptions}
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            formatMoney={formatMoney}
            onAddRfi={addProjectRfi}
            onDeleteRfi={deleteProjectRfi}
            onAddSiteInstruction={addProjectSiteInstruction}
            onDeleteSiteInstruction={deleteProjectSiteInstruction}
          />
        </TabsContent>
        <TabsContent value="units" className="space-y-6">
          <ProjectUnitsTab
            project={project}
            activeBusinessPartners={customerBusinessPartners}
            linkOptions={projectLinkOptions}
            unitDraft={unitDraft}
            setUnitDraft={setUnitDraft}
            editingUnitId={editingUnitId}
            unitTypeTemplates={unitTypeTemplates}
            unitTypeOptions={projectUnitTypeOptions}
            unitStatusOptions={projectUnitStatusOptions}
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            formatMoney={formatMoney}
            unitActionBusyKey={followThroughBusyKey}
            onAddBuilding={addProjectBuilding}
            onDeleteBuilding={deleteProjectBuilding}
            onAddFloor={addProjectFloor}
            onDeleteFloor={deleteProjectFloor}
            onAddUnitReleaseBatch={addProjectUnitReleaseBatch}
            onDeleteUnitReleaseBatch={deleteProjectUnitReleaseBatch}
            onSaveUnit={saveProjectUnit}
            onEditUnit={beginEditProjectUnit}
            onCancelUnitEdit={resetUnitEditor}
            onReleaseUnit={releaseProjectUnit}
            onWithdrawUnitRelease={withdrawProjectUnitRelease}
            onPublishUnitToEstate={publishProjectUnitToEstate}
            onCreateSalesAgreement={createSalesAgreementFromProjectUnit}
            onCreateLeaseAgreement={createLeaseAgreementFromProjectUnit}
            onCreateSalesOrder={createSalesOrderFromProjectUnit}
            onDeleteUnit={deleteProjectUnit}
          />
        </TabsContent>
        <TabsContent value="variations" className="space-y-6">
          <ProjectCustomerVariationsTab
            project={project}
            units={project.units}
            activeBusinessPartners={customerBusinessPartners}
            linkOptions={projectLinkOptions}
            variationDraft={customerVariationDraft}
            setVariationDraft={setCustomerVariationDraft}
            editingVariationId={editingCustomerVariationId}
            variationStatusOptions={customerVariationStatusOptions}
            variationTimingOptions={customerVariationTimingOptions}
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            formatMoney={formatMoney}
            followThroughBusyKey={followThroughBusyKey}
            onSaveVariation={saveCustomerVariation}
            onEditVariation={beginEditCustomerVariation}
            onCancelVariationEdit={resetCustomerVariationEditor}
            onDeleteVariation={deleteCustomerVariation}
            onCreateVariationJobCard={createCustomerVariationJobCard}
            onCreateVariationWorkOrder={createCustomerVariationWorkOrder}
          />
        </TabsContent>
        <TabsContent value="handover" className="space-y-6">
          <ProjectHandoverTab
            project={project}
            units={project.units}
            commissioningDraft={commissioningDraft}
            setCommissioningDraft={setCommissioningDraft}
            handoverDraft={handoverDraft}
            setHandoverDraft={setHandoverDraft}
            commissioningStatusOptions={DEFAULT_PROJECT_COMMISSIONING_STATUSES}
            handoverStatusOptions={DEFAULT_PROJECT_HANDOVER_ITEM_STATUSES}
            handoverTypeOptions={DEFAULT_PROJECT_HANDOVER_ITEM_TYPES}
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            onAddHandoverBatch={addProjectUnitHandoverBatch}
            onDeleteHandoverBatch={deleteProjectUnitHandoverBatch}
            onAddCommissioningItem={addCommissioningItem}
            onDeleteCommissioningItem={deleteCommissioningItem}
            onAddHandoverItem={addHandoverItem}
            onDeleteHandoverItem={deleteHandoverItem}
          />
        </TabsContent>
        <TabsContent value="defects" className="space-y-6">
          <ProjectDefectsTab
            project={project}
            units={project.units}
            activeBusinessPartners={customerBusinessPartners}
            linkOptions={projectLinkOptions}
            postHandoverSummary={postHandoverSummary}
            snagDraft={snagDraft}
            setSnagDraft={setSnagDraft}
            editingSnagItemId={editingSnagItemId}
            defectLiabilityDraft={defectLiabilityDraft}
            setDefectLiabilityDraft={setDefectLiabilityDraft}
            editingDefectLiabilityCaseId={editingDefectLiabilityCaseId}
            snagStatusOptions={DEFAULT_PROJECT_SNAG_STATUSES}
            snagSeverityOptions={DEFAULT_PROJECT_SNAG_SEVERITIES}
            defectLiabilityStatusOptions={
              DEFAULT_PROJECT_DEFECT_LIABILITY_STATUSES
            }
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            formatMoney={formatMoney}
            followThroughBusyKey={followThroughBusyKey}
            onSaveSnagItem={saveSnagItem}
            onEditSnagItem={beginEditSnagItem}
            onCancelSnagItemEdit={resetSnagEditor}
            onDeleteSnagItem={deleteSnagItem}
            onSaveDefectLiabilityCase={saveDefectLiabilityCase}
            onEditDefectLiabilityCase={beginEditDefectLiabilityCase}
            onCancelDefectLiabilityCaseEdit={resetDefectLiabilityEditor}
            onDeleteDefectLiabilityCase={deleteDefectLiabilityCase}
            onCreateDefectLiabilityJobCard={createDefectLiabilityJobCard}
            onCreateDefectLiabilityWorkOrder={createDefectLiabilityWorkOrder}
          />
        </TabsContent>
        <TabsContent value="execution" className="space-y-6">
          <ProjectExecutionTab
            project={project}
            flat={flat}
            milestoneTitles={milestoneTitles}
            workItemTitles={workItemTitles}
            deliverable={deliverable}
            setDeliverable={setDeliverable}
            deliverableStatusOptions={deliverableStatusOptions}
            boolValue={boolValue}
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            expandedDeliverableHistoryId={expandedDeliverableHistoryId}
            setExpandedDeliverableHistoryId={setExpandedDeliverableHistoryId}
            onLoad={load}
            onOpenWorkflows={openWorkflowAdmin}
            onAddDeliverable={addDeliverable}
            onDeleteDeliverable={deleteDeliverable}
            activeUsers={activeUsers}
            formatUserLabel={formatUserLabel}
            timesheet={timesheet}
            setTimesheet={setTimesheet}
            timesheetWorkTypeOptions={timesheetWorkTypeOptions}
            currentUserId={currentUserId}
            onAddTimesheet={addTimesheet}
            onApproveTimesheet={approveTimesheet}
            expense={expense}
            setExpense={setExpense}
            expenseCategoryOptions={expenseCategoryOptions}
            expenseCurrencyOptions={expenseCurrencyOptions}
            baseCurrencyCode={baseCurrency.code}
            getCurrencyOptionLabel={getCurrencyOptionLabel}
            onAddExpense={addExpense}
            onApproveExpense={approveExpense}
            formatMoney={formatMoney}
            getResolvedUserLabel={getResolvedUserLabel}
          />
        </TabsContent>

        <TabsContent value="analysis" className="space-y-6">
          <ProjectAnalysisTab
            project={project}
            scheduleAnalysis={scheduleAnalysis}
            workItemTitles={workItemTitles}
            formatDateLabel={formatDateLabel}
            formatMoney={formatMoney}
            baseline={baseline}
            setBaseline={setBaseline}
            baselineComparison={baselineComparison}
            onCompareBaseline={compareBaseline}
            financialSummary={financialSummary}
            integrationSummary={integrationSummary}
            aiInsights={aiInsights}
            onRefresh={refreshAnalysis}
            onCreateBaseline={createBaseline}
            onGenerateRevenueRecognition={generateRevenueRecognition}
          />
        </TabsContent>
        <TabsContent value="materials" className="space-y-6">
          <ProjectMaterialsTab
            materialRequisitions={materialRequisitions}
            orderedMaterialRequisitions={orderedMaterialRequisitions}
            materialSnapshot={materialSnapshot}
            formatMoney={formatMoney}
            formatDateLabel={formatDateLabel}
            onRefresh={refreshMaterials}
            onOpenMaterialDialog={openMaterialDialog}
            onSubmitRequisition={submitMaterialRequisition}
            onOpenIssueDialog={openIssueDialog}
            onOpenReturnDialog={openReturnDialog}
            onCompleteRequisition={completeMaterialRequisition}
          />
        </TabsContent>
        <TabsContent value="governance" className="space-y-6">
          <ProjectGovernanceTab
            project={project}
            flat={flat}
            governanceSummary={governanceSummary}
            risk={risk}
            setRisk={setRisk}
            riskStatusOptions={riskStatusOptions}
            riskCategoryOptions={riskCategoryOptions}
            riskResponseStrategyOptions={riskResponseStrategyOptions}
            issue={issue}
            setIssue={setIssue}
            issueStatusOptions={issueStatusOptions}
            issueSeverityOptions={issueSeverityOptions}
            qualityCheckpoint={qualityCheckpoint}
            setQualityCheckpoint={setQualityCheckpoint}
            qualityCheckpointStatusOptions={qualityCheckpointStatusOptions}
            nonConformance={nonConformance}
            setNonConformance={setNonConformance}
            nonConformanceStatusOptions={nonConformanceStatusOptions}
            nonConformanceSeverityOptions={nonConformanceSeverityOptions}
            change={change}
            setChange={setChange}
            changeTypeOptions={changeTypeOptions}
            changeStatusOptions={changeStatusOptions}
            decision={decision}
            setDecision={setDecision}
            decisionStatusOptions={decisionStatusOptions}
            meeting={meeting}
            setMeeting={setMeeting}
            meetingTypeOptions={meetingTypeOptions}
            actionItem={actionItem}
            setActionItem={setActionItem}
            actionItemPriorityOptions={actionItemPriorityOptions}
            actionItemStatusOptions={actionItemStatusOptions}
            lessonLearned={lessonLearned}
            setLessonLearned={setLessonLearned}
            lessonCategoryOptions={lessonCategoryOptions}
            lessonVisibilityOptions={lessonVisibilityOptions}
            closure={closure}
            setClosure={setClosure}
            closureRecord={closureRecord}
            currentUserId={currentUserId}
            activeUsers={activeUsers}
            formatUserLabel={formatUserLabel}
            formatCatalogLabel={formatCatalogLabel}
            formatDateLabel={formatDateLabel}
            boolValue={boolValue}
            getResolvedUserLabel={getResolvedUserLabel}
            act={act}
            onLoad={load}
            onOpenWorkflows={openWorkflowAdmin}
          />
        </TabsContent>
        <TabsContent value="access" className="space-y-6">
          <ProjectAccessTab
            project={project}
            integrationSummary={integrationSummary}
            formatMoney={formatMoney}
            formatCatalogLabel={formatCatalogLabel}
            boolValue={boolValue}
            assetLink={assetLink}
            setAssetLink={setAssetLink}
            assetLinkTypeOptions={assetLinkTypeOptions}
            assetLinkStatusOptions={assetLinkStatusOptions}
            maintenanceAssets={maintenanceAssets}
            companyAssets={companyAssets}
            jobCards={projectLinkOptions.jobCards}
            onAddAssetLink={addAssetLink}
            externalPolicy={externalPolicy}
            setExternalPolicy={setExternalPolicy}
            businessPartners={businessPartners}
            externalArtifactOptions={externalArtifactOptions}
            onSaveExternalPolicy={saveExternalAccessPolicy}
            onDeleteExternalPolicy={deleteExternalAccessPolicy}
          />
        </TabsContent>
        <TabsContent value="documents" className="space-y-6">
          <ProjectDocumentsTab
            project={project}
            doc={doc}
            setDoc={setDoc}
            docFile={docFile}
            setDocFile={setDocFile}
            documentCategoryOptions={documentCategoryOptions}
            documentTypeOptions={documentTypeOptions}
            boolValue={boolValue}
            formatCatalogLabel={formatCatalogLabel}
            onAttachDocument={attachDocument}
            onDeleteDocument={deleteDocument}
            comment={comment}
            setComment={setComment}
            flatWorkItems={flat}
            onPostComment={postComment}
          />
        </TabsContent>
        <TabsContent value="history" className="space-y-6">
          <ProjectHistoryTab project={project} />
        </TabsContent>
      </Tabs>
      <ProjectGanttPlannerDialog
        ganttDialogOpen={ganttDialogOpen}
        setGanttDialogOpen={setGanttDialogOpen}
        timelineBounds={timelineBounds}
        formatDateLabel={formatDateLabel}
        ganttSummary={ganttSummary}
        ganttDependencyLines={ganttDependencyLines}
        ganttMilestones={ganttMilestones}
        offBaselineItemCount={offBaselineItemCount}
        setCollapsedGanttItems={setCollapsedGanttItems}
        timelineItems={timelineItems}
        ganttChildrenLookup={ganttChildrenLookup}
        ganttQuickFilters={ganttQuickFilters}
        setGanttQuickFilters={setGanttQuickFilters}
        currentUserId={currentUserId}
        scrollGanttChart={scrollGanttChart}
        exportGanttToExcel={exportGanttToExcel}
        editingWorkItemId={editingWorkItemId}
        governanceSummary={governanceSummary}
        plannerEditingItem={plannerEditingItem}
        resetWorkEditor={resetWorkEditor}
        saveWorkEditor={saveWorkEditor}
        work={work}
        setWork={setWork}
        projectPackages={project.packages}
        taskStatusOptions={taskStatusOptions}
        formatCatalogLabel={formatCatalogLabel}
        activeUsers={activeUsers}
        formatUserLabel={formatUserLabel}
        scheduleReasonRequired={scheduleReasonRequired}
        visibleTimelineItems={visibleTimelineItems}
        ganttHighlight={ganttHighlight}
        beginEditWorkItem={beginEditWorkItem}
        setHoveredGanttItemId={setHoveredGanttItemId}
        collapsedGanttItems={collapsedGanttItems}
        toggleGanttItem={toggleGanttItem}
        getResolvedUserLabel={getResolvedUserLabel}
        ganttChartScrollRef={ganttChartScrollRef}
        ganttBottomScrollRef={ganttBottomScrollRef}
        handleGanttChartWheel={handleGanttChartWheel}
        syncGanttScroll={syncGanttScroll}
        ganttWidth={ganttWidth}
        ganttMonthSegments={ganttMonthSegments}
        ganttWeekSegments={ganttWeekSegments}
        ganttColumns={ganttColumns}
        todayMarkerOffset={ganttTodayOffset}
      />
      <RequisitionDialog
        open={materialDialogOpen}
        onOpenChange={setMaterialDialogOpen}
        mode={materialDialogMode}
        requisitionId={selectedMaterialRequisitionId}
        warehouses={materialWarehouses}
        projectContext={{
          projectId: project.id,
          projectCode: project.projectCode,
          projectTitle: project.title,
          departmentId: project.departmentId,
        }}
        onSuccess={() => {
          void load();
        }}
      />
      <IssueRequisitionDialog
        open={materialIssueDialogOpen}
        onOpenChange={setMaterialIssueDialogOpen}
        requisitionId={materialIssueRequisitionId}
        onSuccess={() => {
          void load();
        }}
      />
      <ReturnRequisitionDialog
        open={materialReturnDialogOpen}
        onOpenChange={setMaterialReturnDialogOpen}
        requisitionId={materialReturnRequisitionId}
        onSuccess={() => {
          void load();
        }}
      />
    </div>
  );
}

type GanttTimelineItem = ProjectWorkItemDto & {
  depth: number;
  outline: string;
};

type GanttQuickFilters = {
  overdue: boolean;
  offBaseline: boolean;
  assignedToMe: boolean;
};

type ProjectGanttPlannerDialogProps = {
  ganttDialogOpen: boolean;
  setGanttDialogOpen: Dispatch<SetStateAction<boolean>>;
  timelineBounds: { min: number; max: number; totalDays: number } | null;
  formatDateLabel: (value?: string) => string;
  ganttSummary: {
    scheduledItems: number;
    spanDays: number;
    overdueItems: number;
    phases: number;
  } | null;
  ganttDependencyLines: Array<{
    id: string;
    path: string;
    labelX: number;
    labelY: number;
    label: string;
    stroke: string;
    dashed?: boolean;
  }>;
  ganttMilestones: Array<{
    id: string;
    title: string;
    status?: string;
    x: number;
    y: number;
  }>;
  offBaselineItemCount: number;
  setCollapsedGanttItems: Dispatch<SetStateAction<string[]>>;
  timelineItems: GanttTimelineItem[];
  ganttChildrenLookup: Map<string, boolean>;
  ganttQuickFilters: GanttQuickFilters;
  setGanttQuickFilters: Dispatch<SetStateAction<GanttQuickFilters>>;
  currentUserId?: string;
  scrollGanttChart: (delta: number) => void;
  exportGanttToExcel: () => void;
  editingWorkItemId: string | null;
  governanceSummary: ProjectGovernanceSummaryDto | null;
  plannerEditingItem: GanttTimelineItem | null;
  resetWorkEditor: () => void;
  saveWorkEditor: () => void;
  work: CreateProjectWorkItemDto;
  setWork: Dispatch<SetStateAction<CreateProjectWorkItemDto>>;
  projectPackages: ProjectPackageDto[];
  taskStatusOptions: string[];
  formatCatalogLabel: (value?: string | null) => string;
  activeUsers: User[];
  formatUserLabel: (user: User) => string;
  scheduleReasonRequired: boolean;
  visibleTimelineItems: GanttTimelineItem[];
  ganttHighlight: {
    taskIds: Set<string>;
    dependencyIds: Set<string>;
    milestoneIds: Set<string>;
  };
  beginEditWorkItem: (
    item: ProjectWorkItemDto,
    options?: { keepCurrentView?: boolean }
  ) => void;
  setHoveredGanttItemId: Dispatch<SetStateAction<string | null>>;
  collapsedGanttItems: string[];
  toggleGanttItem: (itemId: string) => void;
  getResolvedUserLabel: (
    userId?: string,
    displayName?: string,
    fallback?: string
  ) => string;
  ganttChartScrollRef: RefObject<HTMLDivElement | null>;
  ganttBottomScrollRef: RefObject<HTMLDivElement | null>;
  handleGanttChartWheel: WheelEventHandler<HTMLDivElement>;
  syncGanttScroll: (source: 'chart' | 'bottom') => void;
  ganttWidth: number;
  ganttMonthSegments: Array<{ key: string; label: string; span: number }>;
  ganttWeekSegments: Array<{ key: string; label: string; span: number }>;
  ganttColumns: Array<{ key: string; date: Date; isMonthStart: boolean }>;
  todayMarkerOffset: number | null;
};

function ProjectGanttPlannerDialog({
  ganttDialogOpen,
  setGanttDialogOpen,
  timelineBounds,
  formatDateLabel,
  ganttSummary,
  ganttDependencyLines,
  ganttMilestones,
  offBaselineItemCount,
  setCollapsedGanttItems,
  timelineItems,
  ganttChildrenLookup,
  ganttQuickFilters,
  setGanttQuickFilters,
  currentUserId,
  scrollGanttChart,
  exportGanttToExcel,
  editingWorkItemId,
  governanceSummary,
  plannerEditingItem,
  resetWorkEditor,
  saveWorkEditor,
  work,
  setWork,
  projectPackages,
  taskStatusOptions,
  formatCatalogLabel,
  activeUsers,
  formatUserLabel,
  scheduleReasonRequired,
  visibleTimelineItems,
  ganttHighlight,
  beginEditWorkItem,
  setHoveredGanttItemId,
  collapsedGanttItems,
  toggleGanttItem,
  getResolvedUserLabel,
  ganttChartScrollRef,
  ganttBottomScrollRef,
  handleGanttChartWheel,
  syncGanttScroll,
  ganttWidth,
  ganttMonthSegments,
  ganttWeekSegments,
  ganttColumns,
  todayMarkerOffset,
}: ProjectGanttPlannerDialogProps) {
  return (
    <Dialog open={ganttDialogOpen} onOpenChange={setGanttDialogOpen}>
      <DialogContent className="flex h-[92vh] max-h-[92vh] w-[96vw] max-w-[96vw] flex-col overflow-hidden p-0">
        <div className="flex min-h-0 flex-1 flex-col">
          <DialogHeader className="border-b px-6 py-4">
            <div className="flex flex-wrap items-start justify-between gap-4 pr-10">
              <div className="space-y-2">
                <DialogTitle>Project Gantt Planner</DialogTitle>
                <DialogDescription>
                  Full project schedule view with WBS grid, timeline bars, and
                  export actions.
                </DialogDescription>
                <div className="flex flex-wrap gap-2 text-sm text-muted-foreground">
                  {timelineBounds ? (
                    <Badge variant="outline">
                      {formatDateLabel(
                        new Date(timelineBounds.min).toISOString()
                      )}{' '}
                      to{' '}
                      {formatDateLabel(
                        new Date(timelineBounds.max).toISOString()
                      )}
                    </Badge>
                  ) : null}
                  {ganttSummary ? (
                    <>
                      <Badge variant="outline">
                        {ganttSummary.scheduledItems} scheduled items
                      </Badge>
                      <Badge variant="outline">
                        {ganttSummary.spanDays} day span
                      </Badge>
                      <Badge variant="outline">
                        {ganttSummary.phases} phases
                      </Badge>
                      <Badge
                        variant={
                          ganttSummary.overdueItems > 0
                            ? 'destructive'
                            : 'secondary'
                        }
                      >
                        {ganttSummary.overdueItems} overdue
                      </Badge>
                    </>
                  ) : null}
                  <Badge variant="outline">
                    {ganttDependencyLines.length} dependencies visualized
                  </Badge>
                  <Badge variant="outline">
                    {ganttMilestones.length} milestones
                  </Badge>
                  <Badge
                    variant={offBaselineItemCount > 0 ? 'secondary' : 'outline'}
                  >
                    {offBaselineItemCount} off baseline
                  </Badge>
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  onClick={() => setCollapsedGanttItems([])}
                >
                  Expand All
                </Button>
                <Button
                  variant="outline"
                  onClick={() =>
                    setCollapsedGanttItems(
                      timelineItems
                        .filter((item) => ganttChildrenLookup.get(item.id))
                        .map((item) => item.id)
                    )
                  }
                >
                  Collapse All
                </Button>
              </div>
            </div>
          </DialogHeader>
          <div className="sticky top-0 z-30 border-b bg-background/95 px-6 py-3 backdrop-blur supports-[backdrop-filter]:bg-background/80">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="space-y-2">
                <div className="text-sm text-muted-foreground">
                  Use the planner actions here. The schedule grid stays below,
                  and the bottom scrollbar remains visible while you move across
                  weeks.
                </div>
                <div className="flex flex-wrap items-center gap-2 text-xs">
                  <span className="text-muted-foreground">Name colors:</span>
                  <span
                    className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold"
                    style={getTaskNameStyle({
                      nodeType: 'Phase',
                      status: 'InProgress',
                    })}
                  >
                    Phase
                  </span>
                  <span
                    className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold"
                    style={getTaskNameStyle({
                      nodeType: 'Workstream',
                      status: 'InProgress',
                    })}
                  >
                    Workstream
                  </span>
                  <span
                    className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold"
                    style={getTaskNameStyle({
                      nodeType: 'Task',
                      status: 'InProgress',
                    })}
                  >
                    In progress
                  </span>
                  <span
                    className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold"
                    style={getTaskNameStyle({
                      nodeType: 'Task',
                      status: 'Blocked',
                    })}
                  >
                    Blocked
                  </span>
                  <span
                    className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold"
                    style={getTaskNameStyle({
                      nodeType: 'Task',
                      status: 'Completed',
                    })}
                  >
                    Completed
                  </span>
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button
                  variant={ganttQuickFilters.overdue ? 'default' : 'outline'}
                  onClick={() =>
                    setGanttQuickFilters((current) => ({
                      ...current,
                      overdue: !current.overdue,
                    }))
                  }
                >
                  Overdue
                </Button>
                <Button
                  variant={
                    ganttQuickFilters.offBaseline ? 'default' : 'outline'
                  }
                  onClick={() =>
                    setGanttQuickFilters((current) => ({
                      ...current,
                      offBaseline: !current.offBaseline,
                    }))
                  }
                >
                  Off Baseline
                </Button>
                <Button
                  variant={
                    ganttQuickFilters.assignedToMe ? 'default' : 'outline'
                  }
                  onClick={() =>
                    setGanttQuickFilters((current) => ({
                      ...current,
                      assignedToMe: !current.assignedToMe,
                    }))
                  }
                  disabled={!currentUserId}
                >
                  Assigned To Me
                </Button>
                <Button
                  variant="outline"
                  onClick={() => scrollGanttChart(-480)}
                >
                  Scroll Left
                </Button>
                <Button variant="outline" onClick={() => scrollGanttChart(480)}>
                  Scroll Right
                </Button>
                <Button onClick={exportGanttToExcel}>
                  <Download className="mr-2 h-4 w-4" />
                  Export Excel
                </Button>
              </div>
            </div>
          </div>
          <ProjectGanttPlannerEditor
            editingWorkItemId={editingWorkItemId}
            governanceSummary={governanceSummary}
            plannerEditingItem={plannerEditingItem}
            resetWorkEditor={resetWorkEditor}
            saveWorkEditor={saveWorkEditor}
            work={work}
            setWork={setWork}
            projectPackages={projectPackages}
            taskStatusOptions={taskStatusOptions}
            formatCatalogLabel={formatCatalogLabel}
            activeUsers={activeUsers}
            formatUserLabel={formatUserLabel}
            scheduleReasonRequired={scheduleReasonRequired}
          />
          <div className="min-h-0 flex-1 overflow-auto p-6">
            {timelineBounds ? (
              <div className="flex min-h-full flex-col rounded-lg border">
                <div className="flex min-w-0">
                  <div className="sticky left-0 z-20 shrink-0 border-r bg-background">
                    <div
                      className="grid h-20 border-b bg-gradient-to-r from-slate-100 via-white to-slate-100"
                      style={{
                        width: `${GANTT_LEFT_GRID_WIDTH}px`,
                        gridTemplateColumns: GANTT_LEFT_GRID_TEMPLATE,
                      }}
                    >
                      <div className="flex items-center border-r border-slate-300/70 px-2">
                        <div className="rounded-full bg-white/90 px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-600 shadow-sm">
                          WBS
                        </div>
                      </div>
                      <div className="flex items-center border-r border-slate-300/70 px-3">
                        <div className="space-y-0.5">
                          <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">
                            Task
                          </div>
                          <div className="text-xs font-medium text-slate-700">
                            Name
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center border-r border-slate-300/70 px-2">
                        <div className="space-y-0.5">
                          <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">
                            Owner
                          </div>
                          <div className="text-xs font-medium text-slate-700">
                            Assignee
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center border-r border-slate-300/70 px-2">
                        <div className="space-y-0.5">
                          <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">
                            Track
                          </div>
                          <div className="text-xs font-medium text-slate-700">
                            Effort
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center border-r border-slate-300/70 px-3">
                        <div className="space-y-0.5">
                          <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">
                            Status
                          </div>
                          <div className="text-xs font-medium text-slate-700">
                            Progress
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center border-r border-slate-300/70 px-2">
                        <div className="space-y-0.5">
                          <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">
                            Start
                          </div>
                          <div className="text-xs font-medium text-slate-700">
                            Date
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center border-r border-slate-300/70 px-2">
                        <div className="space-y-0.5">
                          <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">
                            End
                          </div>
                          <div className="text-xs font-medium text-slate-700">
                            Date
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center px-2">
                        <div className="space-y-0.5">
                          <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">
                            Dur
                          </div>
                          <div className="text-xs font-medium text-slate-700">
                            Days
                          </div>
                        </div>
                      </div>
                    </div>
                    {visibleTimelineItems.map((item, index) => {
                      const durationDays = Math.max(
                        1,
                        Math.round(
                          (new Date(item.plannedEndDate as string).setHours(
                            0,
                            0,
                            0,
                            0
                          ) -
                            new Date(item.plannedStartDate as string).setHours(
                              0,
                              0,
                              0,
                              0
                            )) /
                            86400000
                        ) + 1
                      );
                      const isOverdue = isWorkItemOverdue(item);
                      const isOffBaseline = item.isOffBaseline;
                      return (
                        <div
                          key={item.id}
                          className={`relative grid h-14 cursor-pointer border-b text-sm transition-colors ${
                            editingWorkItemId === item.id
                              ? 'bg-primary/10 ring-1 ring-inset ring-primary/30'
                              : isOffBaseline
                                ? 'bg-amber-50/70'
                                : isOverdue
                                  ? 'bg-red-50'
                                  : ganttHighlight.taskIds.has(item.id)
                                    ? 'bg-sky-50'
                                    : index % 2 === 0
                                      ? 'bg-background'
                                      : 'bg-muted/10'
                          }`}
                          style={{
                            width: `${GANTT_LEFT_GRID_WIDTH}px`,
                            gridTemplateColumns: GANTT_LEFT_GRID_TEMPLATE,
                          }}
                          role="button"
                          tabIndex={0}
                          onClick={() =>
                            beginEditWorkItem(item, { keepCurrentView: true })
                          }
                          onKeyDown={(event) => {
                            if (event.key === 'Enter' || event.key === ' ') {
                              event.preventDefault();
                              beginEditWorkItem(item, {
                                keepCurrentView: true,
                              });
                            }
                          }}
                          onMouseEnter={() => setHoveredGanttItemId(item.id)}
                          onMouseLeave={() =>
                            setHoveredGanttItemId((current) =>
                              current === item.id ? null : current
                            )
                          }
                        >
                          {editingWorkItemId === item.id ? (
                            <div className="pointer-events-none absolute inset-y-1 left-1 z-10 w-1 rounded-full bg-primary shadow-[0_0_0_1px_rgba(59,130,246,0.18)]" />
                          ) : null}
                          <div className="flex items-center border-r px-2 text-muted-foreground">
                            {item.outline}
                          </div>
                          <div
                            className="flex items-center gap-2 border-r px-3"
                            style={{ paddingLeft: `${12 + item.depth * 18}px` }}
                          >
                            {ganttChildrenLookup.get(item.id) ? (
                              <button
                                type="button"
                                className="rounded-sm border bg-background/70 p-0.5 text-muted-foreground transition hover:bg-muted"
                                onClick={(event) => {
                                  event.stopPropagation();
                                  toggleGanttItem(item.id);
                                }}
                              >
                                {collapsedGanttItems.includes(item.id) ? (
                                  <ChevronRight className="h-3.5 w-3.5" />
                                ) : (
                                  <ChevronDown className="h-3.5 w-3.5" />
                                )}
                              </button>
                            ) : (
                              <span className="w-4" />
                            )}
                            <span
                              className={`h-2.5 w-2.5 rounded-full ${getGanttStatusTone(item)}`}
                            />
                            <div className="min-w-0">
                              <div className="flex items-center gap-2">
                                <div
                                  className="max-w-full truncate rounded-md border px-2 py-0.5 text-[12px] font-semibold"
                                  style={getTaskNameStyle(item)}
                                  title={item.title}
                                >
                                  {item.title}
                                </div>
                                {editingWorkItemId === item.id ? (
                                  <span className="inline-flex shrink-0 items-center rounded-full border border-primary/20 bg-primary/10 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-primary">
                                    Selected
                                  </span>
                                ) : null}
                                {isOffBaseline ? (
                                  <span
                                    className="inline-flex shrink-0 items-center rounded-full border border-amber-200 bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-amber-700"
                                    title={formatBaselineVarianceLabel(
                                      item.baselineVarianceDays
                                    )}
                                  >
                                    {item.baselineVarianceDays > 0
                                      ? `+${item.baselineVarianceDays}d`
                                      : `${item.baselineVarianceDays}d`}
                                  </span>
                                ) : null}
                                {isOverdue ? (
                                  <span className="inline-flex shrink-0 items-center rounded-full border border-red-200 bg-red-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-red-700">
                                    Overdue
                                  </span>
                                ) : null}
                              </div>
                              <div className="truncate text-xs text-muted-foreground">
                                {item.nodeType}
                                {item.projectPackageName
                                  ? ` · ${[item.projectPhaseName, item.projectPackageName].filter(Boolean).join(' / ')}`
                                  : ''}
                              </div>
                            </div>
                          </div>
                          <div className="flex items-center justify-center border-r px-2">
                            {(() => {
                              const assigneeLabel = getResolvedUserLabel(
                                item.assignedToUserId,
                                item.assignedToUserDisplayName
                              );
                              return (
                                <div
                                  className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-[11px] font-semibold text-primary"
                                  title={assigneeLabel}
                                >
                                  {item.assignedToUserId
                                    ? getUserInitials(assigneeLabel)
                                    : '--'}
                                </div>
                              );
                            })()}
                          </div>
                          <div className="flex items-center border-r px-2 text-muted-foreground">
                            {formatTrackerHours(
                              item.effortEstimateHours ?? item.actualEffortHours
                            )}
                          </div>
                          <div className="min-w-0 border-r px-2 py-2">
                            <span
                              className="inline-flex max-w-full items-center rounded-full border px-2 py-1 text-[11px] font-medium leading-none"
                              style={getPlannerStatusBadgeStyle(item)}
                              title={formatCatalogLabel(item.status || 'New')}
                            >
                              <span className="truncate">
                                {formatCatalogLabel(item.status || 'New')}
                              </span>
                            </span>
                          </div>
                          <div className="flex items-center border-r px-2 text-muted-foreground">
                            {formatDateLabel(item.plannedStartDate)}
                          </div>
                          <div className="flex items-center border-r px-2 text-muted-foreground">
                            {formatDateLabel(item.plannedEndDate)}
                          </div>
                          <div className="flex items-center px-2 text-muted-foreground">
                            {durationDays}d
                          </div>
                        </div>
                      );
                    })}
                  </div>
                  <div
                    ref={ganttChartScrollRef}
                    className="min-w-0 flex-1 overflow-x-auto overflow-y-hidden"
                    onWheel={handleGanttChartWheel}
                    onScroll={() => syncGanttScroll('chart')}
                  >
                    <div
                      className="shrink-0"
                      style={{ width: `${ganttWidth}px` }}
                    >
                      <div className="border-b bg-slate-50">
                        <div
                          className="flex h-9 border-b bg-gradient-to-r from-slate-100 via-slate-50 to-slate-100"
                          style={{ width: `${ganttWidth}px` }}
                        >
                          {ganttMonthSegments.map((segment) => (
                            <div
                              key={segment.key}
                              className="flex items-center border-r border-slate-300/70 px-3"
                              style={{
                                width: `${segment.span * GANTT_DAY_WIDTH}px`,
                              }}
                            >
                              <div className="rounded-full bg-white/90 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-700 shadow-sm">
                                {segment.label}
                              </div>
                            </div>
                          ))}
                        </div>
                        <div
                          className="flex h-8 border-b bg-gradient-to-r from-slate-50 via-white to-slate-50"
                          style={{ width: `${ganttWidth}px` }}
                        >
                          {ganttWeekSegments.map((segment) => (
                            <div
                              key={segment.key}
                              className="flex items-center border-r border-slate-200 px-2"
                              style={{
                                width: `${segment.span * GANTT_DAY_WIDTH}px`,
                              }}
                            >
                              <div className="inline-flex items-center rounded-full border border-sky-100 bg-sky-50 px-2 py-0.5 text-[11px] font-semibold text-sky-700 shadow-sm">
                                {segment.label.replace('Week ', 'W')}
                              </div>
                            </div>
                          ))}
                        </div>
                        <div
                          className="grid h-11 bg-white"
                          style={{
                            gridTemplateColumns: `repeat(${ganttColumns.length}, ${GANTT_DAY_WIDTH}px)`,
                          }}
                        >
                          {ganttColumns.map((column) => (
                            <div
                              key={column.key}
                              className={`border-r px-1 text-center ${format(column.date, 'EEE') === 'Sat' || format(column.date, 'EEE') === 'Sun' ? 'bg-slate-50' : ''} ${todayMarkerOffset !== null && Math.round(todayMarkerOffset / GANTT_DAY_WIDTH) === ganttColumns.findIndex((entry) => entry.key === column.key) ? 'bg-blue-50' : ''}`}
                            >
                              <div className="flex h-full flex-col items-center justify-center">
                                <div className="text-[9px] font-semibold uppercase tracking-[0.16em] text-slate-400">
                                  {format(column.date, 'EEE')}
                                </div>
                                <div className="text-[13px] font-semibold text-slate-700">
                                  {format(column.date, 'dd')}
                                </div>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                      <div className="relative">
                        {todayMarkerOffset !== null ? (
                          <>
                            <div
                              className="pointer-events-none absolute top-0 z-30 h-full border-l-2 border-blue-500"
                              style={{ left: `${todayMarkerOffset}px` }}
                            />
                            <div
                              className="pointer-events-none absolute top-2 z-30 -translate-x-1/2 rounded-full bg-blue-500 px-2 py-0.5 text-[10px] font-medium text-white"
                              style={{ left: `${todayMarkerOffset}px` }}
                            >
                              Today
                            </div>
                          </>
                        ) : null}
                        <svg
                          className="absolute left-0 top-0 z-20"
                          width={ganttWidth}
                          height={visibleTimelineItems.length * 56}
                          viewBox={`0 0 ${ganttWidth} ${visibleTimelineItems.length * 56}`}
                          aria-hidden="true"
                        >
                          <defs>
                            <marker
                              id="gantt-arrowhead"
                              markerWidth="8"
                              markerHeight="8"
                              refX="7"
                              refY="4"
                              orient="auto"
                            >
                              <path d="M 0 0 L 8 4 L 0 8 z" fill="#475569" />
                            </marker>
                          </defs>
                          {ganttDependencyLines.map((line) => (
                            <g key={line.id}>
                              <path
                                d={line.path}
                                fill="none"
                                stroke={
                                  ganttHighlight.dependencyIds.has(line.id)
                                    ? '#0369a1'
                                    : line.stroke
                                }
                                strokeWidth={
                                  ganttHighlight.dependencyIds.has(line.id)
                                    ? '2.5'
                                    : '1.5'
                                }
                                strokeDasharray={
                                  line.dashed ? '5 4' : undefined
                                }
                                markerEnd="url(#gantt-arrowhead)"
                                className="cursor-pointer"
                              />
                              <rect
                                x={line.labelX - 2}
                                y={line.labelY - 10}
                                width={line.label.length * 6.2 + 8}
                                height="16"
                                rx="4"
                                fill={
                                  ganttHighlight.dependencyIds.has(line.id)
                                    ? '#e0f2fe'
                                    : '#ffffff'
                                }
                                opacity="0.96"
                              />
                              <text
                                x={line.labelX + 2}
                                y={line.labelY}
                                fontSize="10"
                                fill={
                                  ganttHighlight.dependencyIds.has(line.id)
                                    ? '#0369a1'
                                    : '#475569'
                                }
                              >
                                {line.label}
                              </text>
                            </g>
                          ))}
                          {ganttMilestones.map((milestone) => (
                            <g
                              key={milestone.id}
                              transform={`translate(${milestone.x}, ${milestone.y})`}
                            >
                              <rect
                                x="-8"
                                y="-8"
                                width="16"
                                height="16"
                                transform="rotate(45)"
                                fill={
                                  ganttHighlight.milestoneIds.has(milestone.id)
                                    ? '#f59e0b'
                                    : '#eab308'
                                }
                                stroke={
                                  ganttHighlight.milestoneIds.has(milestone.id)
                                    ? '#92400e'
                                    : '#a16207'
                                }
                                strokeWidth={
                                  ganttHighlight.milestoneIds.has(milestone.id)
                                    ? '1.8'
                                    : '1.2'
                                }
                                rx="1"
                              />
                              <text
                                x="12"
                                y="4"
                                fontSize="10"
                                fill={
                                  ganttHighlight.milestoneIds.has(milestone.id)
                                    ? '#92400e'
                                    : '#475569'
                                }
                              >
                                {milestone.title}
                              </text>
                            </g>
                          ))}
                        </svg>
                        {visibleTimelineItems.map((item, index) => {
                          const start = new Date(
                            item.plannedStartDate as string
                          ).setHours(0, 0, 0, 0);
                          const end = new Date(
                            item.plannedEndDate as string
                          ).setHours(0, 0, 0, 0);
                          const offset =
                            Math.max(
                              0,
                              Math.round(
                                (start - timelineBounds.min) / 86400000
                              )
                            ) * GANTT_DAY_WIDTH;
                          const durationDays = Math.max(
                            1,
                            Math.round((end - start) / 86400000) + 1
                          );
                          const width = durationDays * GANTT_DAY_WIDTH;
                          const progressWidth = Math.max(
                            6,
                            Math.round(
                              (width * (item.percentComplete || 0)) / 100
                            )
                          );
                          const isRollup = !!ganttChildrenLookup.get(item.id);
                          const isOverdue = isWorkItemOverdue(item);
                          const isOffBaseline = item.isOffBaseline;
                          const palette = getGanttPalette(item);
                          return (
                            <div
                              key={`${item.id}-gantt`}
                              className={`relative h-14 cursor-pointer border-b transition-colors ${
                                editingWorkItemId === item.id
                                  ? 'bg-primary/10 ring-1 ring-inset ring-primary/30'
                                  : isOffBaseline
                                    ? 'bg-amber-50/60'
                                    : isOverdue
                                      ? 'bg-red-50/70'
                                      : ganttHighlight.taskIds.has(item.id)
                                        ? 'bg-sky-50'
                                        : index % 2 === 0
                                          ? 'bg-background'
                                          : 'bg-muted/10'
                              }`}
                              style={{ width: `${ganttWidth}px` }}
                              role="button"
                              tabIndex={0}
                              onClick={() =>
                                beginEditWorkItem(item, {
                                  keepCurrentView: true,
                                })
                              }
                              onKeyDown={(event) => {
                                if (
                                  event.key === 'Enter' ||
                                  event.key === ' '
                                ) {
                                  event.preventDefault();
                                  beginEditWorkItem(item, {
                                    keepCurrentView: true,
                                  });
                                }
                              }}
                              onMouseEnter={() =>
                                setHoveredGanttItemId(item.id)
                              }
                              onMouseLeave={() =>
                                setHoveredGanttItemId((current) =>
                                  current === item.id ? null : current
                                )
                              }
                            >
                              {editingWorkItemId === item.id ? (
                                <>
                                  <div className="pointer-events-none absolute inset-x-0 top-0 z-10 h-px bg-primary/60" />
                                  <div className="pointer-events-none absolute inset-y-1 left-1 z-10 w-1 rounded-full bg-primary shadow-[0_0_0_1px_rgba(59,130,246,0.18)]" />
                                </>
                              ) : null}
                              <div className="absolute inset-0 flex">
                                {ganttWeekSegments.map((segment, weekIndex) => (
                                  <div
                                    key={`${item.id}-week-${segment.key}`}
                                    className={
                                      weekIndex % 2 === 0
                                        ? 'bg-slate-50/70'
                                        : 'bg-slate-100/70'
                                    }
                                    style={{
                                      width: `${segment.span * GANTT_DAY_WIDTH}px`,
                                    }}
                                  />
                                ))}
                              </div>
                              <div
                                className="absolute inset-0 grid"
                                style={{
                                  gridTemplateColumns: `repeat(${ganttColumns.length}, ${GANTT_DAY_WIDTH}px)`,
                                }}
                              >
                                {ganttColumns.map((column) => (
                                  <div
                                    key={`${item.id}-${column.key}`}
                                    className="border-r"
                                  />
                                ))}
                              </div>
                              {isRollup ? (
                                <div
                                  className={`absolute top-3 z-30 h-2 rounded-full border-2 bg-background ${editingWorkItemId === item.id ? 'shadow-[0_0_0_4px_rgba(59,130,246,0.12)]' : ''} ${isOffBaseline ? 'shadow-[0_0_0_4px_rgba(245,158,11,0.16)]' : ''}`}
                                  style={{
                                    left: `${offset}px`,
                                    width: `${Math.max(width, 10)}px`,
                                    borderColor: isOverdue
                                      ? '#dc2626'
                                      : isOffBaseline
                                        ? '#d97706'
                                        : ganttHighlight.taskIds.has(item.id)
                                          ? '#0369a1'
                                          : palette.border,
                                    backgroundColor: isOverdue
                                      ? '#fee2e2'
                                      : isOffBaseline
                                        ? '#fef3c7'
                                        : palette.soft,
                                  }}
                                >
                                  <div
                                    className="absolute -left-0.5 top-[-5px] h-4 w-1 rounded-full"
                                    style={{
                                      backgroundColor: isOverdue
                                        ? '#dc2626'
                                        : isOffBaseline
                                          ? '#d97706'
                                          : ganttHighlight.taskIds.has(item.id)
                                            ? '#0369a1'
                                            : palette.border,
                                    }}
                                  />
                                  <div
                                    className="absolute -right-0.5 top-[-5px] h-4 w-1 rounded-full"
                                    style={{
                                      backgroundColor: isOverdue
                                        ? '#dc2626'
                                        : isOffBaseline
                                          ? '#d97706'
                                          : ganttHighlight.taskIds.has(item.id)
                                            ? '#0369a1'
                                            : palette.border,
                                    }}
                                  />
                                  <div
                                    className={`absolute -top-5 left-0 text-[11px] font-semibold ${isOverdue ? 'text-red-700' : isOffBaseline ? 'text-amber-700' : 'text-slate-700'}`}
                                  >
                                    {durationDays} days,{' '}
                                    {item.percentComplete || 0}%
                                  </div>
                                  {isOffBaseline ? (
                                    <div className="absolute -right-2 -top-2 rounded-sm bg-amber-500 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white">
                                      {item.baselineVarianceDays > 0
                                        ? `+${item.baselineVarianceDays}d`
                                        : `${item.baselineVarianceDays}d`}
                                    </div>
                                  ) : null}
                                  {isOverdue ? (
                                    <div className="absolute -left-2 -top-2 rounded-sm bg-red-600 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white">
                                      Overdue
                                    </div>
                                  ) : null}
                                  <div
                                    className="absolute left-2 top-3 max-w-[calc(100%-0.5rem)] truncate rounded-md border px-2 py-0.5 text-[11px] font-semibold"
                                    style={{
                                      ...getTaskNameStyle(item),
                                      color: isOverdue
                                        ? '#991b1b'
                                        : isOffBaseline
                                          ? '#92400e'
                                          : getTaskNameStyle(item).color,
                                    }}
                                  >
                                    {item.title}
                                  </div>
                                </div>
                              ) : (
                                <div
                                  className={`absolute top-3 z-30 h-8 rounded-lg shadow-sm ${ganttHighlight.taskIds.has(item.id) ? 'ring-2 ring-sky-500' : ''} ${editingWorkItemId === item.id ? 'ring-2 ring-primary ring-offset-2 ring-offset-white' : ''} ${isOffBaseline ? 'ring-1 ring-amber-400/80 ring-offset-1 ring-offset-white' : ''}`}
                                  style={{
                                    left: `${offset}px`,
                                    width: `${Math.max(width, 10)}px`,
                                    background: isOverdue
                                      ? 'linear-gradient(135deg, #ef4444 0%, #b91c1c 100%)'
                                      : isOffBaseline
                                        ? 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)'
                                        : `linear-gradient(135deg, ${palette.start} 0%, ${palette.end} 100%)`,
                                    boxShadow: isOverdue
                                      ? '0 8px 18px rgba(185, 28, 28, 0.22)'
                                      : isOffBaseline
                                        ? '0 8px 18px rgba(217, 119, 6, 0.24)'
                                        : '0 8px 18px rgba(15, 23, 42, 0.12)',
                                  }}
                                >
                                  {isOffBaseline ? (
                                    <div className="absolute -left-2 -top-2 rounded-sm bg-amber-500 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white">
                                      {item.baselineVarianceDays > 0
                                        ? `+${item.baselineVarianceDays}d`
                                        : `${item.baselineVarianceDays}d`}
                                    </div>
                                  ) : null}
                                  {isOverdue ? (
                                    <div className="absolute -left-2 -top-2 rounded-sm bg-red-600 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white">
                                      Overdue
                                    </div>
                                  ) : null}
                                  {editingWorkItemId === item.id ? (
                                    <div className="absolute -right-2 -top-2 rounded-sm bg-primary px-1 py-0.5 text-[9px] font-bold uppercase tracking-wide text-primary-foreground">
                                      Selected
                                    </div>
                                  ) : null}
                                  <div
                                    className="h-full rounded-lg"
                                    style={{
                                      width: `${Math.min(progressWidth, width)}px`,
                                      background: isOverdue
                                        ? 'linear-gradient(90deg, #7f1d1d 0%, rgba(15, 23, 42, 0.08) 100%)'
                                        : `linear-gradient(90deg, ${palette.progress} 0%, rgba(15, 23, 42, 0.08) 100%)`,
                                    }}
                                  />
                                  <div
                                    className="absolute inset-0 flex items-center justify-between px-2 text-[11px] font-medium"
                                    style={{ color: palette.text }}
                                  >
                                    <span
                                      className="max-w-[70%] truncate rounded-md border px-2 py-0.5 text-[11px] font-semibold"
                                      style={{
                                        ...getTaskNameStyle(item),
                                        backgroundColor: isOverdue
                                          ? 'rgba(255,255,255,0.18)'
                                          : isOffBaseline
                                            ? 'rgba(255,251,235,0.28)'
                                            : `${getTaskNameStyle(item).backgroundColor}`,
                                        borderColor: isOverdue
                                          ? 'rgba(255,255,255,0.28)'
                                          : getTaskNameStyle(item).borderColor,
                                        color: isOverdue
                                          ? '#fff'
                                          : isOffBaseline
                                            ? '#fff7ed'
                                            : getTaskNameStyle(item).color,
                                      }}
                                    >
                                      {item.depth <= 1 ? item.title : ''}
                                    </span>
                                    <span>{item.percentComplete}%</span>
                                  </div>
                                </div>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  </div>
                </div>
                <div className="sticky bottom-0 z-20 border-t bg-background/95 px-3 py-2 backdrop-blur supports-[backdrop-filter]:bg-background/80">
                  <div className="rounded-lg border bg-gradient-to-r from-slate-50 via-white to-slate-50 p-3 shadow-sm">
                    <div className="mb-2 flex items-center justify-between text-xs">
                      <div className="font-medium text-slate-700">
                        Planner Navigator
                      </div>
                      <div className="text-muted-foreground">
                        Drag this bar to move across the schedule while the left
                        planning columns remain fixed.
                      </div>
                    </div>
                    <div
                      ref={ganttBottomScrollRef}
                      className="overflow-x-auto overflow-y-hidden rounded-md border bg-white shadow-inner"
                      onScroll={() => syncGanttScroll('bottom')}
                    >
                      <div
                        className="relative"
                        style={{ width: `${ganttWidth}px`, height: '22px' }}
                      >
                        <div className="absolute inset-0 flex">
                          {ganttWeekSegments.map((segment, weekIndex) => (
                            <div
                              key={`navigator-${segment.key}`}
                              className={
                                weekIndex % 2 === 0
                                  ? 'border-r bg-sky-50/70'
                                  : 'border-r bg-slate-100/80'
                              }
                              style={{
                                width: `${segment.span * GANTT_DAY_WIDTH}px`,
                              }}
                            />
                          ))}
                        </div>
                        {todayMarkerOffset !== null ? (
                          <div
                            className="absolute inset-y-0 border-l-2 border-blue-500"
                            style={{ left: `${todayMarkerOffset}px` }}
                          />
                        ) : null}
                        <div className="absolute inset-x-0 top-1/2 h-px -translate-y-1/2 bg-slate-300" />
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            ) : (
              <div className="text-sm text-muted-foreground">
                Add planned start and end dates to work items to build the Gantt
                view.
              </div>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
