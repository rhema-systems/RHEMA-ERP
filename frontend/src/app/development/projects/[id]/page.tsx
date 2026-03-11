'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { format, getISOWeek } from 'date-fns';
import * as XLSX from 'xlsx';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { IssueRequisitionDialog } from '@/components/inventory/IssueRequisitionDialog';
import { ReturnRequisitionDialog } from '@/components/inventory/ReturnRequisitionDialog';
import { RequisitionDialog } from '@/components/inventory/RequisitionDialog';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowApprovalHistoryPanel } from '@/components/workflow/WorkflowApprovalHistoryPanel';
import { ArrowLeft, ChevronDown, ChevronRight, Download, Maximize2, Pencil, Plus, RefreshCw, Save, Trash2 } from 'lucide-react';
import { businessPartnerService, type BusinessPartnerDto } from '@/services/businessPartnerService';
import { contractService, type ContractDto } from '@/services/contractService';
import { currencyService, type CurrencyListDto } from '@/services/financeCommonService';
import { inventoryManagementService, type WarehouseDto } from '@/services/inventoryManagementService';
import { inventoryRequisitionService, type InventoryRequisitionDto, RequisitionStatusMap } from '@/services/inventoryRequisitionService';
import { userService } from '@/services/user';
import {
  AddProjectMemberDto,
  AttachProjectDocumentDto,
  CreateProjectAssetLinkDto,
  CreateProjectActionItemDto,
  CreateProjectBaselineDto,
  CreateProjectBillingScheduleDto,
  CreateProjectBudgetRevisionDto,
  CreateProjectChangeRequestDto,
  CreateProjectCommentDto,
  CreateProjectDecisionDto,
  CreateProjectDeliverableDto,
  CreateProjectExpenseDto,
  CreateProjectExternalAccessPolicyDto,
  CreateProjectForecastVersionDto,
  CreateProjectIssueDto,
  CreateProjectNonConformanceDto,
  CreateProjectQualityCheckpointDto,
  CreateProjectMilestoneDto,
  CreateProjectLessonLearnedDto,
  CreateProjectInvoiceRequestDto,
  CreateProjectMeetingMinuteDto,
  CreateProjectResourceAllocationDto,
  CreateProjectRiskDto,
  CreateProjectTaskDependencyDto,
  CreateProjectTimesheetEntryDto,
  CreateProjectWorkItemDto,
  ProjectAiInsightDto,
  ProjectBaselineComparisonDto,
  ProjectBudgetRevisionDto,
  ProjectCatalogEntryDto,
  ProjectDetailDto,
  ProjectFinancialControlSummaryDto,
  ProjectForecastVersionDto,
  ProjectGovernanceSummaryDto,
  ProjectIntegrationSummaryDto,
  ProjectInvoiceRequestDto,
  ProjectClosureDto,
  ProjectPortfolioDto,
  ProjectPriorityDto,
  ProjectProgramDto,
  ProjectQualityCheckpointDto,
  ProjectScheduleAnalysisDto,
  ProjectTemplateDto,
  ProjectTypeDto,
  ProjectWorkItemDto,
  ProjectNonConformanceDto,
  UpsertProjectClosureDto,
  UpdateProjectDto,
  projectService,
} from '@/services/projectService';
import type { User } from '@/types';

const today = () => new Date().toISOString().slice(0, 10);
const DEFAULT_METHODOLOGIES = ['Waterfall', 'Agile', 'Hybrid', 'Program', 'Internal'];
const DEFAULT_BILLING_TYPES = ['Milestone', 'FixedPrice', 'TimeAndMaterials', 'Retainer', 'CostPlus', 'NonBillable'];
const DEFAULT_FUNDING_SOURCES = ['Customer Contract', 'Internal Budget', 'Capex Allocation', 'Grant Funding', 'Department Allocation'];
const DEFAULT_RESOURCE_ROLES = ['ProjectManager', 'TeamMember', 'TaskOwner', 'FinanceOfficer', 'RiskOfficer', 'ProcurementOfficer', 'ExternalContributor'];
const DEFAULT_MEMBER_ROLES = ['Sponsor', 'Project Manager', 'Team Member', 'Task Owner', 'Finance Officer', 'External Contributor'];
const DEFAULT_TASK_STATUSES = ['New', 'Assigned', 'InProgress', 'Blocked', 'PendingReview', 'Completed', 'Closed', 'Cancelled'];
const DEFAULT_TASK_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'];
const DEFAULT_DELIVERABLE_STATUSES = ['Draft', 'InReview', 'Approved', 'Rejected', 'Issued', 'Accepted'];
const DEFAULT_RISK_STATUSES = ['Open', 'Monitoring', 'Mitigated', 'Closed', 'Escalated'];
const DEFAULT_RISK_CATEGORIES = ['Scope', 'Schedule', 'Resource', 'Quality', 'Vendor', 'Compliance', 'Financial', 'Operational'];
const DEFAULT_RISK_RESPONSE_STRATEGIES = ['Monitor', 'Mitigate', 'Avoid', 'Transfer', 'Accept', 'Escalate'];
const DEFAULT_EXPENSE_CATEGORIES = ['Travel', 'Meals', 'Lodging', 'Supplies', 'Equipment', 'Other'];
const DEFAULT_CHANGE_TYPES = ['Scope', 'Budget', 'Schedule', 'Quality', 'Contract'];
const DEFAULT_ISSUE_STATUSES = ['Open', 'InProgress', 'PendingReview', 'Resolved', 'Closed', 'Escalated'];
const DEFAULT_CHANGE_STATUSES = ['Draft', 'PendingApproval', 'Approved', 'Rejected', 'Implemented', 'Archived'];
const DEFAULT_ISSUE_SEVERITIES = ['Low', 'Medium', 'High', 'Critical'];
const DEFAULT_QUALITY_CHECKPOINT_STATUSES = ['Open', 'InReview', 'SignedOff', 'Passed', 'Failed', 'Waived'];
const DEFAULT_NON_CONFORMANCE_STATUSES = ['Open', 'InReview', 'Resolved', 'Closed', 'Waived'];
const DEFAULT_NON_CONFORMANCE_SEVERITIES = ['Low', 'Medium', 'High', 'Critical'];
const DEFAULT_TIMESHEET_WORK_TYPES = ['Standard', 'Overtime', 'Travel', 'Support', 'BillableDelivery', 'Admin'];
const DEFAULT_CURRENCIES = ['USD'];
const DEFAULT_DECISION_STATUSES = ['Draft', 'Approved', 'Rejected'];
const DEFAULT_MEETING_TYPES = ['Status', 'RiskReview', 'SteeringCommittee', 'Closure', 'Customer'];
const DEFAULT_ACTION_ITEM_STATUSES = ['Open', 'InProgress', 'Completed', 'Closed'];
const DEFAULT_ACTION_ITEM_PRIORITIES = ['Low', 'Normal', 'High', 'Critical'];
const DEFAULT_LESSON_CATEGORIES = ['General', 'Delivery', 'Process', 'Quality', 'Commercial', 'Stakeholder'];
const DEFAULT_LESSON_VISIBILITIES = ['Internal', 'Tenant', 'External'];
const DEFAULT_ASSET_LINK_TYPES = ['Asset', 'Equipment', 'Installation', 'Transfer', 'Maintenance'];
const DEFAULT_ASSET_LINK_STATUSES = ['Linked', 'Reserved', 'Installed', 'Transferred', 'Returned'];
const DEFAULT_DOCUMENT_CATEGORIES = ['Charter', 'Plan', 'Requirements', 'Design', 'Minutes', 'Contracts', 'Drawings', 'Reports', 'AcceptanceCertificates', 'RiskLogs', 'ChangeApprovals', 'ClosureDocuments', 'General'];
const DEFAULT_DOCUMENT_TYPES = ['Attachment', 'Evidence', 'Approval', 'Reference', 'Contract', 'Drawing', 'Minutes'];
const TASK_BOARD_STATUSES = ['New', 'Assigned', 'InProgress', 'Blocked', 'PendingReview', 'Completed', 'Closed'];
const MATERIAL_STATUS_ORDER = [1, 2, 3, 4, 5, 6, 7, 8, 9];
const GANTT_DAY_WIDTH = 36;
const GANTT_LEFT_GRID_TEMPLATE = '52px 240px 62px 54px 92px 72px 72px 52px';
const GANTT_LEFT_GRID_WIDTH = 696;

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

const formatCatalogLabel = (value: string) =>
  value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[-_]/g, ' ');

const formatUserLabel = (user: User) => {
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  return fullName ? `${fullName} (${user.username})` : user.username;
};

const formatBusinessPartnerLabel = (partner: BusinessPartnerDto) =>
  `${partner.partnerName}${partner.partnerType ? ` (${partner.partnerType})` : ''}`;

const formatContractLabel = (contract: ContractDto) =>
  `${contract.contractNumber} - ${contract.contractTitle}`;

const resolveCatalogOptions = (entries: ProjectCatalogEntryDto[], fallbackValues: string[], currentValue?: string) => {
  const configured = entries
    .filter((entry) => entry.isActive)
    .map((entry) => entry.name.trim())
    .filter(Boolean);

  const values = configured.length > 0 ? configured : fallbackValues;
  return currentValue && !values.includes(currentValue) ? [currentValue, ...values] : values;
};

const memberInit: AddProjectMemberDto = { userId: '', role: 'Team Member' };
const workInit: CreateProjectWorkItemDto = { nodeType: 'Task', title: '', status: 'New', percentComplete: 0, isRollupEnabled: true };
const milestoneInit: CreateProjectMilestoneDto = { title: '', targetDate: '', status: 'Draft', requiresApproval: false };
const resourceInit: CreateProjectResourceAllocationDto = { userId: '', allocationRole: 'TeamMember', allocationType: 'Hours', allocationValue: 40, plannedHours: 40, startDate: today(), endDate: today(), bookingType: 'Soft', status: 'Requested' };
const riskInit: CreateProjectRiskDto = { title: '', status: 'Open', probability: 1, impact: 1 };
const issueInit: CreateProjectIssueDto = { title: '', status: 'Open', severity: 'Medium' };
const qualityCheckpointInit: CreateProjectQualityCheckpointDto = { title: '', status: 'Open', requiresQaSignOff: false };
const nonConformanceInit: CreateProjectNonConformanceDto = { title: '', severity: 'Medium', status: 'Open' };
const changeInit: CreateProjectChangeRequestDto = { title: '', status: 'Draft', changeType: 'Scope' };
const billingInit: CreateProjectBillingScheduleDto = { name: '', billingType: 'Milestone', amount: 0, billingDate: today(), status: 'Draft', isBillable: true };
const invoiceInit: CreateProjectInvoiceRequestDto = { requestedAmount: 0, currency: 'USD', status: 'Draft' };
const docInit: AttachProjectDocumentDto = { documentName: '', category: 'General', documentType: 'Attachment', filePath: '', versionLabel: '1.0', status: 'Active', isExternalVisible: false };
const commentInit: CreateProjectCommentDto = { body: '', commentType: 'General' };
const deliverableInit: CreateProjectDeliverableDto = { title: '', status: 'Draft', externalSubmissionAllowed: false, externalSignOffRequired: false, isExternalVisible: false };
const dependencyInit: CreateProjectTaskDependencyDto = { predecessorWorkItemId: '', successorWorkItemId: '', dependencyType: 'FS', lagDays: 0, isEnforced: true };
const baselineInit: CreateProjectBaselineDto = { name: '', notes: '' };
const timesheetInit: CreateProjectTimesheetEntryDto = { userId: '', entryDate: today(), hours: 8, isBillable: false, hourlyRate: 0, workType: 'Standard', notes: '' };
const expenseInit: CreateProjectExpenseDto = { userId: '', expenseDate: today(), category: 'Other', currency: 'USD', amount: 0, taxAmount: 0, isBillable: false, notes: '' };
const budgetRevisionInit: CreateProjectBudgetRevisionDto = { revisionName: '', revisionType: 'Revision', estimatedBudget: 0, approvedBudget: 0, committedCost: 0, forecastCost: 0, thresholdWarningPercent: 75, thresholdCriticalPercent: 90, effectiveDate: today(), changeReason: '', notes: '' };
const forecastVersionInit: CreateProjectForecastVersionDto = { versionName: '', asOfDate: today(), forecastCost: 0, estimateAtCompletion: 0, forecastRevenue: 0, forecastMargin: 0, isActive: true, notes: '' };
const assetLinkInit: CreateProjectAssetLinkDto = { linkType: 'Asset', status: 'Linked', notes: '' };
const externalPolicyInit: CreateProjectExternalAccessPolicyDto = { businessPartnerId: '', artifactType: 'Project', accessLevel: 'Read', canComment: false, canUpload: false, canApprove: false, notes: '' };
const decisionInit: CreateProjectDecisionDto = { title: '', decisionDate: today(), status: 'Draft', rationale: '', alternativesConsidered: '', impactSummary: '' };
const meetingInit: CreateProjectMeetingMinuteDto = { title: '', meetingDate: today(), meetingType: 'Status', minutes: '', attendeesJson: '' };
const actionItemInit: CreateProjectActionItemDto = { title: '', description: '', status: 'Open', priority: 'Normal', dueDate: today() };
const lessonLearnedInit: CreateProjectLessonLearnedDto = { title: '', category: 'General', description: '', recommendation: '', appliedPhase: '', visibility: 'Internal' };
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

const flatten = (
  items: ProjectWorkItemDto[],
  depth = 0,
  outlinePrefix = '',
): Array<ProjectWorkItemDto & { depth: number; outline: string }> =>
  items.flatMap((x, index) => {
    const outline = outlinePrefix ? `${outlinePrefix}.${index + 1}` : `${index + 1}`;
    return [{ ...x, depth, outline }, ...flatten(x.children || [], depth + 1, outline)];
  });

const formatDateLabel = (value?: string) => (value ? format(new Date(value), 'MMM dd, yyyy') : 'N/A');
const boolValue = (value?: boolean) => (value ? 'true' : 'false');
const formatCurrencyLabel = (currency: CurrencyListDto) => `${currency.code} | ${currency.name}`;
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

const getPlannerStatusBadgeStyle = (item: Pick<ProjectWorkItemDto, 'nodeType' | 'status'>) => {
  const palette = getGanttPalette(item as ProjectWorkItemDto);
  return {
    backgroundColor: item.nodeType === 'Phase' || item.nodeType === 'Workstream' ? palette.soft : `${palette.soft}CC`,
    borderColor: palette.border,
    color: palette.border,
  };
};
const getTaskNameStyle = (item: Pick<ProjectWorkItemDto, 'nodeType' | 'status'>) => {
  const palette = getGanttPalette(item as ProjectWorkItemDto);
  return {
    backgroundColor: `${palette.soft}D9`,
    borderColor: `${palette.border}33`,
    color: palette.border,
  };
};
const isWorkItemOverdue = (item: Pick<ProjectWorkItemDto, 'plannedEndDate' | 'status'>) => {
  if (!item.plannedEndDate) return false;
  const status = (item.status || '').toLowerCase();
  if (['completed', 'closed', 'cancelled'].includes(status)) return false;
  return new Date(item.plannedEndDate).setHours(0, 0, 0, 0) < new Date().setHours(0, 0, 0, 0);
};
const normalizeDateInputValue = (value?: string | Date | null) => (value ? String(value).slice(0, 10) : '');
const formatBaselineVarianceLabel = (days: number) => {
  if (days === 0) return 'On baseline';
  return days > 0 ? `+${days}d variance` : `${days}d variance`;
};

function WorkTree({
  items,
  onDelete,
  onEdit,
  editingId,
  depth = 0,
}: {
  items: ProjectWorkItemDto[];
  onDelete: (id: string) => void;
  onEdit: (item: ProjectWorkItemDto) => void;
  editingId?: string | null;
  depth?: number;
}) {
  return (
    <>
      {items.map((item) => (
        <div key={item.id} className="space-y-2">
          <div
            className={`flex items-center justify-between rounded-lg border p-4 ${editingId === item.id ? 'border-primary bg-primary/5' : ''}`}
            style={{ marginLeft: depth * 16 }}
          >
            <div>
              <div className="font-medium">{item.title}</div>
              <div className="text-xs text-muted-foreground">{item.nodeType} | {item.status} | {item.percentComplete}%</div>
            </div>
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="sm" onClick={() => onEdit(item)}><Pencil className="h-4 w-4" /></Button>
              <Button variant="ghost" size="sm" onClick={() => onDelete(item.id)}><Trash2 className="h-4 w-4" /></Button>
            </div>
          </div>
          {!!item.children?.length && <WorkTree items={item.children} onDelete={onDelete} onEdit={onEdit} editingId={editingId} depth={depth + 1} />}
        </div>
      ))}
    </>
  );
}

export default function ProjectDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const id = params?.id;
  const ganttChartScrollRef = useRef<HTMLDivElement | null>(null);
  const ganttBottomScrollRef = useRef<HTMLDivElement | null>(null);
  const ganttScrollSyncRef = useRef(false);
  const [project, setProject] = useState<ProjectDetailDto | null>(null);
  const [types, setTypes] = useState<ProjectTypeDto[]>([]);
  const [priorities, setPriorities] = useState<ProjectPriorityDto[]>([]);
  const [templates, setTemplates] = useState<ProjectTemplateDto[]>([]);
  const [portfolios, setPortfolios] = useState<ProjectPortfolioDto[]>([]);
  const [programs, setPrograms] = useState<ProjectProgramDto[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [currencies, setCurrencies] = useState<CurrencyListDto[]>([]);
  const [businessPartners, setBusinessPartners] = useState<BusinessPartnerDto[]>([]);
  const [contracts, setContracts] = useState<ContractDto[]>([]);
  const [methodologyCatalog, setMethodologyCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [billingTypeCatalog, setBillingTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [fundingSourceCatalog, setFundingSourceCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [resourceRoleCatalog, setResourceRoleCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [memberRoleCatalog, setMemberRoleCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [taskStatusCatalog, setTaskStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [taskPriorityCatalog, setTaskPriorityCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [deliverableStatusCatalog, setDeliverableStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [riskStatusCatalog, setRiskStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [riskCategoryCatalog, setRiskCategoryCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [riskResponseStrategyCatalog, setRiskResponseStrategyCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [qualityCheckpointStatusCatalog, setQualityCheckpointStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [nonConformanceStatusCatalog, setNonConformanceStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [nonConformanceSeverityCatalog, setNonConformanceSeverityCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [expenseCategoryCatalog, setExpenseCategoryCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [changeTypeCatalog, setChangeTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [issueStatusCatalog, setIssueStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [changeStatusCatalog, setChangeStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [issueSeverityCatalog, setIssueSeverityCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [timesheetWorkTypeCatalog, setTimesheetWorkTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [decisionStatusCatalog, setDecisionStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [meetingTypeCatalog, setMeetingTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [actionItemStatusCatalog, setActionItemStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [actionItemPriorityCatalog, setActionItemPriorityCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [lessonCategoryCatalog, setLessonCategoryCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [lessonVisibilityCatalog, setLessonVisibilityCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [assetLinkTypeCatalog, setAssetLinkTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [assetLinkStatusCatalog, setAssetLinkStatusCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [documentCategoryCatalog, setDocumentCategoryCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [documentTypeCatalog, setDocumentTypeCatalog] = useState<ProjectCatalogEntryDto[]>([]);
  const [overview, setOverview] = useState<UpdateProjectDto>({ title: '' });
  const [member, setMember] = useState(memberInit);
  const [work, setWork] = useState(workInit);
  const [milestone, setMilestone] = useState(milestoneInit);
  const [resource, setResource] = useState(resourceInit);
  const [risk, setRisk] = useState(riskInit);
  const [issue, setIssue] = useState(issueInit);
  const [qualityCheckpoint, setQualityCheckpoint] = useState<CreateProjectQualityCheckpointDto>(qualityCheckpointInit);
  const [nonConformance, setNonConformance] = useState<CreateProjectNonConformanceDto>(nonConformanceInit);
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
  const [budgetRevision, setBudgetRevision] = useState<CreateProjectBudgetRevisionDto>(budgetRevisionInit);
  const [forecastVersion, setForecastVersion] = useState<CreateProjectForecastVersionDto>(forecastVersionInit);
  const [assetLink, setAssetLink] = useState(assetLinkInit);
  const [externalPolicy, setExternalPolicy] = useState(externalPolicyInit);
  const [decision, setDecision] = useState<CreateProjectDecisionDto>(decisionInit);
  const [meeting, setMeeting] = useState<CreateProjectMeetingMinuteDto>(meetingInit);
  const [actionItem, setActionItem] = useState<CreateProjectActionItemDto>(actionItemInit);
  const [lessonLearned, setLessonLearned] = useState<CreateProjectLessonLearnedDto>(lessonLearnedInit);
  const [closure, setClosure] = useState<UpsertProjectClosureDto>(closureInit);
  const [aiInsights, setAiInsights] = useState<ProjectAiInsightDto[]>([]);
  const [scheduleAnalysis, setScheduleAnalysis] = useState<ProjectScheduleAnalysisDto | null>(null);
  const [baselineComparison, setBaselineComparison] = useState<ProjectBaselineComparisonDto | null>(null);
  const [financialSummary, setFinancialSummary] = useState<ProjectFinancialControlSummaryDto | null>(null);
  const [budgetRevisions, setBudgetRevisions] = useState<ProjectBudgetRevisionDto[]>([]);
  const [forecastVersions, setForecastVersions] = useState<ProjectForecastVersionDto[]>([]);
  const [integrationSummary, setIntegrationSummary] = useState<ProjectIntegrationSummaryDto | null>(null);
  const [governanceSummary, setGovernanceSummary] = useState<ProjectGovernanceSummaryDto | null>(null);
  const [materialRequisitions, setMaterialRequisitions] = useState<InventoryRequisitionDto[]>([]);
  const [materialWarehouses, setMaterialWarehouses] = useState<WarehouseDto[]>([]);
  const [materialDialogOpen, setMaterialDialogOpen] = useState(false);
  const [materialDialogMode, setMaterialDialogMode] = useState<'create' | 'edit' | 'view'>('create');
  const [selectedMaterialRequisitionId, setSelectedMaterialRequisitionId] = useState<string | undefined>();
  const [materialIssueDialogOpen, setMaterialIssueDialogOpen] = useState(false);
  const [materialIssueRequisitionId, setMaterialIssueRequisitionId] = useState<string | null>(null);
  const [materialReturnDialogOpen, setMaterialReturnDialogOpen] = useState(false);
  const [materialReturnRequisitionId, setMaterialReturnRequisitionId] = useState<string | null>(null);
  const [showProjectApprovalHistory, setShowProjectApprovalHistory] = useState(false);
  const [showClosureApprovalHistory, setShowClosureApprovalHistory] = useState(false);
  const [expandedBudgetRevisionHistoryId, setExpandedBudgetRevisionHistoryId] = useState<string | null>(null);
  const [expandedDeliverableHistoryId, setExpandedDeliverableHistoryId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState('overview');
  const [analysisLoaded, setAnalysisLoaded] = useState(false);
  const [materialsLoaded, setMaterialsLoaded] = useState(false);
  const [referenceDataLoaded, setReferenceDataLoaded] = useState(false);
  const [taskView, setTaskView] = useState<'tree' | 'kanban' | 'timeline'>('tree');
  const [ganttDialogOpen, setGanttDialogOpen] = useState(false);
  const [collapsedGanttItems, setCollapsedGanttItems] = useState<string[]>([]);
  const [hoveredGanttItemId, setHoveredGanttItemId] = useState<string | null>(null);
  const [editingWorkItemId, setEditingWorkItemId] = useState<string | null>(null);
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

  const loadOverviewSummaries = async (projectId: string) => {
    const [financialResult, integrationResult, governanceResult] = await Promise.allSettled([
      projectService.getFinancialControlSummary(projectId),
      projectService.getIntegrationSummary(projectId),
      projectService.getGovernanceSummary(projectId),
    ]);

    setFinancialSummary(financialResult.status === 'fulfilled' ? financialResult.value : null);
    setIntegrationSummary(integrationResult.status === 'fulfilled' ? integrationResult.value : null);
    setGovernanceSummary(governanceResult.status === 'fulfilled' ? governanceResult.value : null);
  };

  const loadAnalysisData = async (projectId: string) => {
    const [insightsResult, analysisResult, budgetRevisionResult, forecastVersionResult] = await Promise.allSettled([
      projectService.getAiInsights(projectId),
      projectService.analyzeSchedule(projectId),
      projectService.getBudgetRevisions(projectId),
      projectService.getForecastVersions(projectId),
    ]);

    setAiInsights(insightsResult.status === 'fulfilled' ? insightsResult.value : []);
    setScheduleAnalysis(analysisResult.status === 'fulfilled' ? analysisResult.value : null);
    setBudgetRevisions(budgetRevisionResult.status === 'fulfilled' ? budgetRevisionResult.value : []);
    setForecastVersions(forecastVersionResult.status === 'fulfilled' ? forecastVersionResult.value : []);
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
            (left, right) => new Date(right.requestDate).getTime() - new Date(left.requestDate).getTime(),
          )
        : [],
    );
    setMaterialWarehouses(warehouseResult.status === 'fulfilled' ? warehouseResult.value : []);
    setMaterialsLoaded(true);
  };

  const loadReferenceData = async (portfolioId?: string) => {
    const [loadedUsers, loadedContracts, loadedCurrencies, catalogResults, loadedPrograms] = await Promise.all([
      userService.searchUsers('').catch(() => []),
      contractService.getActiveContracts().catch(() => []),
      currencyService.getActive().catch(() => []),
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
      ]),
      portfolioId ? projectService.getPrograms(portfolioId).catch(() => []) : Promise.resolve([]),
    ]);

    setUsers(loadedUsers);
    setContracts(loadedContracts);
    setCurrencies(loadedCurrencies);
    setMethodologyCatalog(catalogResults[0].status === 'fulfilled' ? catalogResults[0].value : []);
    setBillingTypeCatalog(catalogResults[1].status === 'fulfilled' ? catalogResults[1].value : []);
    setFundingSourceCatalog(catalogResults[2].status === 'fulfilled' ? catalogResults[2].value : []);
    setResourceRoleCatalog(catalogResults[3].status === 'fulfilled' ? catalogResults[3].value : []);
    setMemberRoleCatalog(catalogResults[4].status === 'fulfilled' ? catalogResults[4].value : []);
    setTaskStatusCatalog(catalogResults[5].status === 'fulfilled' ? catalogResults[5].value : []);
    setTaskPriorityCatalog(catalogResults[6].status === 'fulfilled' ? catalogResults[6].value : []);
    setDeliverableStatusCatalog(catalogResults[7].status === 'fulfilled' ? catalogResults[7].value : []);
    setRiskStatusCatalog(catalogResults[8].status === 'fulfilled' ? catalogResults[8].value : []);
    setRiskCategoryCatalog(catalogResults[9].status === 'fulfilled' ? catalogResults[9].value : []);
    setRiskResponseStrategyCatalog(catalogResults[10].status === 'fulfilled' ? catalogResults[10].value : []);
    setQualityCheckpointStatusCatalog(catalogResults[11].status === 'fulfilled' ? catalogResults[11].value : []);
    setNonConformanceStatusCatalog(catalogResults[12].status === 'fulfilled' ? catalogResults[12].value : []);
    setNonConformanceSeverityCatalog(catalogResults[13].status === 'fulfilled' ? catalogResults[13].value : []);
    setExpenseCategoryCatalog(catalogResults[14].status === 'fulfilled' ? catalogResults[14].value : []);
    setChangeTypeCatalog(catalogResults[15].status === 'fulfilled' ? catalogResults[15].value : []);
    setIssueStatusCatalog(catalogResults[16].status === 'fulfilled' ? catalogResults[16].value : []);
    setChangeStatusCatalog(catalogResults[17].status === 'fulfilled' ? catalogResults[17].value : []);
    setIssueSeverityCatalog(catalogResults[18].status === 'fulfilled' ? catalogResults[18].value : []);
    setTimesheetWorkTypeCatalog(catalogResults[19].status === 'fulfilled' ? catalogResults[19].value : []);
    setDecisionStatusCatalog(catalogResults[20].status === 'fulfilled' ? catalogResults[20].value : []);
    setMeetingTypeCatalog(catalogResults[21].status === 'fulfilled' ? catalogResults[21].value : []);
    setActionItemStatusCatalog(catalogResults[22].status === 'fulfilled' ? catalogResults[22].value : []);
    setActionItemPriorityCatalog(catalogResults[23].status === 'fulfilled' ? catalogResults[23].value : []);
    setLessonCategoryCatalog(catalogResults[24].status === 'fulfilled' ? catalogResults[24].value : []);
    setLessonVisibilityCatalog(catalogResults[25].status === 'fulfilled' ? catalogResults[25].value : []);
    setAssetLinkTypeCatalog(catalogResults[26].status === 'fulfilled' ? catalogResults[26].value : []);
    setAssetLinkStatusCatalog(catalogResults[27].status === 'fulfilled' ? catalogResults[27].value : []);
    setDocumentCategoryCatalog(catalogResults[28].status === 'fulfilled' ? catalogResults[28].value : []);
    setDocumentTypeCatalog(catalogResults[29].status === 'fulfilled' ? catalogResults[29].value : []);
    setPrograms(loadedPrograms);
    setReferenceDataLoaded(true);
  };

  const load = async () => {
    if (!id) return;
    try {
      setLoading(true);
      setReferenceDataLoaded(false);
      const [p, t, pr, tpl, pf, bp] = await Promise.all([
        projectService.getProjectById(id),
        projectService.getProjectTypes(),
        projectService.getProjectPriorities(),
        projectService.getProjectTemplates(),
        projectService.getPortfolios(),
        businessPartnerService.getActivePartners(),
      ]);
      setProject(p);
      setTypes(t);
      setPriorities(pr);
      setTemplates(tpl);
      setPortfolios(pf);
      setBusinessPartners(bp);
      setAiInsights([]);
      setScheduleAnalysis(null);
      setBudgetRevisions([]);
      setForecastVersions([]);
      setMaterialRequisitions([]);
      setMaterialWarehouses([]);
      setAnalysisLoaded(false);
      setMaterialsLoaded(false);
      setCollapsedGanttItems([]);
      await loadOverviewSummaries(p.id);
      void loadReferenceData(p.portfolioId).catch(() => {
        toast.error('Some project reference data could not be loaded');
      });
      setOverview({
        title: p.title, summary: p.summary, businessCase: p.businessCase, objectives: p.objectives, methodology: p.methodology,
        projectTypeId: p.projectTypeId, projectPriorityId: p.projectPriorityId, templateId: p.templateId, portfolioId: p.portfolioId, programId: p.programId,
        sponsorId: p.sponsorId, projectManagerId: p.projectManagerId, businessPartnerId: p.businessPartnerId, contractId: p.contractId,
        startDate: p.startDate, targetEndDate: p.targetEndDate, estimatedBudget: p.estimatedBudget, approvedBudget: p.approvedBudget,
        actualCost: p.actualCost, fundingSource: p.fundingSource, budgetStatus: p.budgetStatus, approvalRequired: p.approvalRequired,
        externalPortalAccessEnabled: p.externalPortalAccessEnabled, externalCollaborationEnabled: p.externalCollaborationEnabled,
      });
      setBudgetRevision((prev) => ({
        ...prev,
        estimatedBudget: p.estimatedBudget ?? 0,
        approvedBudget: p.approvedBudget ?? p.estimatedBudget ?? 0,
        forecastCost: p.actualCost ?? 0,
      }));
      setClosure({
        finalBudget: p.closure?.finalBudget ?? p.approvedBudget ?? p.estimatedBudget ?? 0,
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
        setTimesheet((prev) => ({ ...prev, userId: prev.userId || currentUserId }));
        setExpense((prev) => ({ ...prev, userId: prev.userId || currentUserId }));
        setActionItem((prev) => ({ ...prev, ownerId: prev.ownerId || currentUserId }));
        setDecision((prev) => ({ ...prev, approverId: prev.approverId || currentUserId }));
        setMeeting((prev) => ({ ...prev, facilitatorId: prev.facilitatorId || currentUserId }));
      }
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

  const beginEditWorkItem = (item: ProjectWorkItemDto, options?: { keepCurrentView?: boolean }) => {
    if (!options?.keepCurrentView) {
      setTaskView('tree');
    }
    setEditingWorkItemId(item.id);
    setWork({
      parentId: item.parentId || undefined,
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
  const saveWorkEditor = async () => {
    if (!project?.id) {
      return;
    }

    if (scheduleReasonRequired && !work.scheduleChangeReason?.trim()) {
      toast.error('Provide a schedule change reason before saving date changes on a locked baseline.');
      return;
    }

    await act(
      () =>
        editingWorkItemId
          ? projectService.updateWorkItem(editingWorkItemId, work).then(() => Promise.resolve())
          : projectService.addWorkItem(project.id, work).then(() => Promise.resolve()),
      editingWorkItemId ? 'Work item updated' : 'Work item added',
      resetWorkEditor,
    );
  };

  useEffect(() => { load(); }, [id]);

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
  }, [activeTab, analysisLoaded, materialsLoaded, project?.id]);

  useEffect(() => {
    if (taskView === 'timeline') {
      setGanttDialogOpen(true);
    }
  }, [taskView]);

  const flat = useMemo(() => flatten(project?.workItems || []), [project?.workItems]);
  const plannerEditingItem = useMemo(
    () => (editingWorkItemId ? flat.find((item) => item.id === editingWorkItemId) ?? null : null),
    [editingWorkItemId, flat],
  );
  const workScheduleChangePending = useMemo(() => {
    if (!plannerEditingItem) return false;
    return (
      normalizeDateInputValue(plannerEditingItem.plannedStartDate) !== normalizeDateInputValue(work.plannedStartDate) ||
      normalizeDateInputValue(plannerEditingItem.plannedEndDate) !== normalizeDateInputValue(work.plannedEndDate)
    );
  }, [plannerEditingItem, work.plannedEndDate, work.plannedStartDate]);
  const scheduleReasonRequired = Boolean(editingWorkItemId && governanceSummary?.hasLockedBaseline && workScheduleChangePending);
  const workItemTitles = useMemo(() => new Map(flat.map((item) => [item.id, item.title])), [flat]);
  const milestoneTitles = useMemo(() => new Map((project?.milestones || []).map((item) => [item.id, item.title])), [project?.milestones]);
  const taskBoardItems = useMemo(
    () => flat.filter((item) => ['Task', 'Subtask', 'ChecklistItem'].includes(item.nodeType)),
    [flat],
  );
  const timelineItems = useMemo(
    () => flat.filter((item) => item.plannedStartDate && item.plannedEndDate),
    [flat],
  );
  const offBaselineItemCount = useMemo(() => timelineItems.filter((item) => item.isOffBaseline).length, [timelineItems]);
  const ganttParentLookup = useMemo(() => new Map(flat.map((item) => [item.id, item.parentId])), [flat]);
  const ganttChildrenLookup = useMemo(
    () => new Map(flat.map((item) => [item.id, (item.children?.length ?? 0) > 0])),
    [flat],
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
        if (ganttQuickFilters.assignedToMe && (!currentUserId || item.assignedToUserId !== currentUserId)) {
          return false;
        }
        return true;
      }),
    [collapsedGanttItems, currentUserId, ganttParentLookup, ganttQuickFilters, timelineItems],
  );
  const timelineBounds = useMemo(() => {
    if (timelineItems.length === 0) return null;
    const starts = timelineItems.map((item) => new Date(item.plannedStartDate as string).setHours(0, 0, 0, 0));
    const ends = timelineItems.map((item) => new Date(item.plannedEndDate as string).setHours(0, 0, 0, 0));
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
    return ganttColumns.reduce<Array<{ key: string; label: string; span: number }>>((segments, column) => {
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
    return ganttColumns.reduce<Array<{ key: string; label: string; span: number }>>((segments, column) => {
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
  const ganttWidth = useMemo(() => ganttColumns.length * GANTT_DAY_WIDTH, [ganttColumns.length]);
  const ganttSummary = useMemo(() => {
    if (!timelineBounds) return null;
    const todayMs = new Date().setHours(0, 0, 0, 0);
    const overdueItems = timelineItems.filter((item) => {
      const end = item.plannedEndDate ? new Date(item.plannedEndDate).setHours(0, 0, 0, 0) : null;
      return end !== null && end < todayMs && !['Completed', 'Closed'].includes(item.status);
    }).length;

    return {
      scheduledItems: timelineItems.length,
      spanDays: timelineBounds.totalDays,
      overdueItems,
      phases: timelineItems.filter((item) => item.nodeType === 'Phase').length,
    };
  }, [timelineBounds, timelineItems]);
  const ganttItemPositions = useMemo(() => {
    if (!timelineBounds) return new Map<string, { left: number; right: number; centerY: number }>();

    return new Map(
      visibleTimelineItems.map((item, index) => {
        const start = new Date(item.plannedStartDate as string).setHours(0, 0, 0, 0);
        const end = new Date(item.plannedEndDate as string).setHours(0, 0, 0, 0);
        const left = Math.max(0, Math.round((start - timelineBounds.min) / 86400000)) * GANTT_DAY_WIDTH;
        const width = Math.max(1, Math.round((end - start) / 86400000) + 1) * GANTT_DAY_WIDTH;

        return [
          item.id,
          {
            left,
            right: left + width,
            centerY: index * 56 + 28,
          },
        ];
      }),
    );
  }, [timelineBounds, visibleTimelineItems]);
  const ganttTodayOffset = useMemo(() => {
    if (!timelineBounds) return null;
    const todayMs = new Date().setHours(0, 0, 0, 0);
    if (todayMs < timelineBounds.min || todayMs > timelineBounds.max) {
      return null;
    }
    return Math.round((todayMs - timelineBounds.min) / 86400000) * GANTT_DAY_WIDTH;
  }, [timelineBounds]);
  const ganttDependencyLines = useMemo(() => {
    if (!project || !timelineBounds) return [];

    return project.taskDependencies
      .map((dependency) => {
        const predecessor = ganttItemPositions.get(dependency.predecessorWorkItemId);
        const successor = ganttItemPositions.get(dependency.successorWorkItemId);
        if (!predecessor || !successor) {
          return null;
        }

        const predecessorAnchor = dependency.dependencyType === 'SS' || dependency.dependencyType === 'SF'
          ? predecessor.left
          : predecessor.right;
        const successorBaseAnchor = dependency.dependencyType === 'SS' || dependency.dependencyType === 'FS'
          ? successor.left
          : successor.right;
        const successorAnchor = successorBaseAnchor + ((dependency.lagDays ?? 0) * GANTT_DAY_WIDTH);
        const elbowX = Math.max(predecessorAnchor, successorAnchor) + 18;
        const midY = predecessor.centerY + ((successor.centerY - predecessor.centerY) / 2);

        return {
          id: dependency.id,
          path: `M ${predecessorAnchor} ${predecessor.centerY} H ${elbowX} V ${successor.centerY} H ${successorAnchor}`,
          labelX: elbowX + 4,
          labelY: midY - 4,
          label: dependency.lagDays ? `${dependency.dependencyType} (${dependency.lagDays}d)` : dependency.dependencyType,
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
      if (dependency.predecessorWorkItemId === hoveredGanttItemId || dependency.successorWorkItemId === hoveredGanttItemId) {
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
    if (!project || !timelineBounds || visibleTimelineItems.length === 0) return [];

    const fallbackY = 18;
    return project.milestones
      .filter((milestone) => !!milestone.targetDate)
      .map((milestone) => {
        const target = new Date(milestone.targetDate).setHours(0, 0, 0, 0);
        if (target < timelineBounds.min || target > timelineBounds.max) {
          return null;
        }

        const linkedRow = milestone.workItemId ? ganttItemPositions.get(milestone.workItemId) : null;
        const x = Math.round((target - timelineBounds.min) / 86400000) * GANTT_DAY_WIDTH;
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
  }, [ganttItemPositions, project, timelineBounds, visibleTimelineItems.length]);
  const toggleGanttItem = (itemId: string) => {
    setCollapsedGanttItems((current) =>
      current.includes(itemId) ? current.filter((value) => value !== itemId) : [...current, itemId],
    );
  };
  const exportGanttToExcel = () => {
    if (!project || !timelineBounds || timelineItems.length === 0) {
      toast.error('Add planned dates before exporting the Gantt plan');
      return;
    }

    const leftColumnHeaders = ['WBS', 'Task', 'Status', 'Owner', 'Start', 'End', 'Dur', 'Notes'];
    const leftColumnCount = leftColumnHeaders.length;
    const projectManagerLabel = project.projectManagerId ? userLookup.get(project.projectManagerId) || project.projectManagerId : 'Not assigned';
    const scheduleStart = formatDateLabel(new Date(timelineBounds.min).toISOString());
    const scheduleEnd = formatDateLabel(new Date(timelineBounds.max).toISOString());
    const totalColumns = leftColumnCount + ganttColumns.length;
    const titleRowIndex = 0;
    const metaRowOneIndex = 2;
    const metaRowTwoIndex = 3;
    const legendRowIndex = 4;
    const monthHeaderRowIndex = 5;
    const weekHeaderRowIndex = 6;
    const dayHeaderRowIndex = 7;
    const bodyStartRowIndex = 8;
    const todayIndex = ganttTodayOffset !== null ? Math.round(ganttTodayOffset / GANTT_DAY_WIDTH) : -1;
    const toExcelColor = (value: string) => value.replace('#', '').toUpperCase();
    const monthRow = new Array<string | number>(totalColumns).fill('');
    const weekRow = new Array<string | number>(totalColumns).fill('');
    const dayHeaderRow = [...leftColumnHeaders, ...ganttColumns.map((column) => format(column.date, 'dd'))];
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
      ['Project Title', project.title, '', 'Start Date', scheduleStart, '', 'Project Duration (days)', timelineBounds.totalDays],
      ['Project Manager', projectManagerLabel, '', 'End Date', scheduleEnd, '', '', ''],
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
      const start = new Date(item.plannedStartDate as string).setHours(0, 0, 0, 0);
      const end = new Date(item.plannedEndDate as string).setHours(0, 0, 0, 0);
      const linkedMilestones = project.milestones.filter((milestone) => milestone.workItemId === item.id);
      const ganttCells = ganttColumns.map(() => '');
      const durationDays = Math.max(1, Math.round((end - start) / 86400000) + 1);
      const assignedTo = item.assignedToUserId ? userLookup.get(item.assignedToUserId) || item.assignedToUserId : 'Unassigned';
      const rowIndex = bodyStartRowIndex + rows.length - bodyStartRowIndex;
      const startOffset = Math.max(0, Math.round((start - timelineBounds.min) / 86400000));
      const endOffset = Math.min(ganttColumns.length - 1, Math.max(startOffset, Math.round((end - timelineBounds.min) / 86400000)));

      if (ganttColumns.length > 0) {
        taskSpanMerges.push({
          rowIndex,
          startColumn: leftColumnCount + startOffset,
          endColumn: leftColumnCount + endOffset,
          label: ganttChildrenLookup.get(item.id) ? `${item.title} (${durationDays}d, ${item.percentComplete || 0}%)` : item.title,
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
        linkedMilestones.map((milestone) => `${milestone.title}${milestone.targetDate ? ` (${formatDateLabel(milestone.targetDate)})` : ''}`).join(', '),
        ...ganttCells,
      ]);
    });

    const worksheet = XLSX.utils.aoa_to_sheet(rows) as XLSX.WorkSheet & Record<string, any>;
    const merges: XLSX.Range[] = [
      { s: { r: titleRowIndex, c: 0 }, e: { r: titleRowIndex, c: totalColumns - 1 } },
    ];

    monthOffset = 0;
    ganttMonthSegments.forEach((segment) => {
      if (segment.span > 1) {
        merges.push({
          s: { r: monthHeaderRowIndex, c: leftColumnCount + monthOffset },
          e: { r: monthHeaderRowIndex, c: leftColumnCount + monthOffset + segment.span - 1 },
        });
      }
      monthOffset += segment.span;
    });

    weekOffset = 0;
    ganttWeekSegments.forEach((segment) => {
      if (segment.span > 1) {
        merges.push({
          s: { r: weekHeaderRowIndex, c: leftColumnCount + weekOffset },
          e: { r: weekHeaderRowIndex, c: leftColumnCount + weekOffset + segment.span - 1 },
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
      topLeftCell: XLSX.utils.encode_cell({ r: bodyStartRowIndex, c: leftColumnCount }),
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
      border: { right: { style: 'thin', color: { rgb: 'CBD5E1' } }, bottom: { style: 'thin', color: { rgb: 'CBD5E1' } } },
    };
    const weekHeaderStyle = {
      font: { bold: true, color: { rgb: '475569' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'F1F5F9' } },
      alignment: { horizontal: 'center', vertical: 'center' },
      border: { right: { style: 'thin', color: { rgb: 'CBD5E1' } }, bottom: { style: 'thin', color: { rgb: 'CBD5E1' } } },
    };
    const dayHeaderStyle = {
      font: { bold: true, color: { rgb: '334155' } },
      fill: { patternType: 'solid', fgColor: { rgb: 'F8FAFC' } },
      alignment: { horizontal: 'center', vertical: 'center' },
      border: { right: { style: 'thin', color: { rgb: 'E2E8F0' } }, bottom: { style: 'thin', color: { rgb: 'CBD5E1' } } },
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

    const setCellStyle = (rowIndex: number, columnIndex: number, style: any) => {
      const address = XLSX.utils.encode_cell({ r: rowIndex, c: columnIndex });
      if (!worksheet[address]) {
        worksheet[address] = { t: 's', v: '' };
      }
      worksheet[address].s = style;
    };

    const weekBandByIndex = ganttColumns.reduce<number[]>((bands, _, dateIndex) => {
      let spanOffset = 0;
      let bandIndex = 0;
      for (let index = 0; index < ganttWeekSegments.length; index += 1) {
        const segment = ganttWeekSegments[index];
        if (dateIndex >= spanOffset && dateIndex < spanOffset + segment.span) {
          bandIndex = index;
          break;
        }
        spanOffset += segment.span;
      }
      bands.push(bandIndex);
      return bands;
    }, []);

    setCellStyle(titleRowIndex, 0, titleStyle);
    [metaRowOneIndex, metaRowTwoIndex].forEach((rowIndex) => {
      [0, 3, 6].forEach((columnIndex) => setCellStyle(rowIndex, columnIndex, metaLabelStyle));
      [1, 4, 7].forEach((columnIndex) => setCellStyle(rowIndex, columnIndex, metaValueStyle));
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
    for (let columnIndex = leftColumnCount; columnIndex < totalColumns; columnIndex += 1) {
      setCellStyle(monthHeaderRowIndex, columnIndex, monthHeaderStyle);
      setCellStyle(weekHeaderRowIndex, columnIndex, weekHeaderStyle);
      setCellStyle(dayHeaderRowIndex, columnIndex, dayHeaderStyle);
      if (columnIndex - leftColumnCount === todayIndex) {
        [monthHeaderRowIndex, weekHeaderRowIndex, dayHeaderRowIndex].forEach((rowIndex) => {
          const baseStyle =
            rowIndex === monthHeaderRowIndex ? monthHeaderStyle : rowIndex === weekHeaderRowIndex ? weekHeaderStyle : dayHeaderStyle;
          setCellStyle(rowIndex, columnIndex, {
            ...baseStyle,
            border: {
              ...baseStyle.border,
              left: { style: 'medium', color: { rgb: '2563EB' } },
            },
          });
        });
      }
    }

    visibleTimelineItems.forEach((item, index) => {
      const rowIndex = bodyStartRowIndex + index;
      const rowFill = index % 2 === 0 ? 'FFFFFF' : 'F8FAFC';
      const palette = getGanttPalette(item);
      const itemStart = new Date(item.plannedStartDate as string).setHours(0, 0, 0, 0);
      const itemEnd = new Date(item.plannedEndDate as string).setHours(0, 0, 0, 0);
      const durationDays = Math.max(1, Math.round((itemEnd - itemStart) / 86400000) + 1);
      const linkedMilestones = project.milestones.filter((milestone) => milestone.workItemId === item.id && milestone.targetDate);
      const itemAssignee = item.assignedToUserId ? userLookup.get(item.assignedToUserId) || item.assignedToUserId : 'Unassigned';
      const commentsLabel = linkedMilestones.length > 0 ? linkedMilestones.map((milestone) => `${milestone.title} (${formatDateLabel(milestone.targetDate)})`).join(', ') : item.nodeType;

      for (let columnIndex = 0; columnIndex < leftColumnCount; columnIndex += 1) {
        setCellStyle(rowIndex, columnIndex, {
          ...baseCellStyle,
          fill: {
            patternType: 'solid',
            fgColor: {
              rgb: item.nodeType === 'Phase' ? 'E0F2FE' : item.nodeType === 'Workstream' ? 'DCFCE7' : rowFill,
            },
          },
          font: { bold: item.nodeType === 'Phase' || item.nodeType === 'Workstream' },
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
            ? weekBand % 2 === 0 ? 'EFF6FF' : 'DBEAFE'
            : item.nodeType === 'Workstream'
              ? weekBand % 2 === 0 ? 'F0FDF4' : 'DCFCE7'
              : weekBand % 2 === 0 ? 'F8FAFC' : 'F1F5F9';
        const cellStyle: any = {
          ...baseCellStyle,
          alignment: { horizontal: 'center', vertical: 'center', wrapText: true },
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
          const fillColor = isRollup ? toExcelColor(palette.soft) : toExcelColor(palette.start);
          cellStyle.fill = { patternType: 'solid', fgColor: { rgb: fillColor } };
          cellStyle.font = {
            bold: true,
            color: { rgb: isRollup ? toExcelColor(palette.border) : toExcelColor(palette.text) },
          };
          cellStyle.border = {
            ...cellStyle.border,
            top: { style: isRollup ? 'medium' : 'thin', color: { rgb: toExcelColor(palette.border) } },
            bottom: { style: isRollup ? 'medium' : 'thin', color: { rgb: toExcelColor(palette.border) } },
          };
          const address = XLSX.utils.encode_cell({ r: rowIndex, c: columnIndex });
          if (!worksheet[address]) {
            worksheet[address] = { t: 's', v: '' };
          }
        }

        const linkedMilestone = linkedMilestones.find((milestone) => new Date(milestone.targetDate).setHours(0, 0, 0, 0) === columnTime);
        if (linkedMilestone) {
          const address = XLSX.utils.encode_cell({ r: rowIndex, c: columnIndex });
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
      for (let columnIndex = span.startColumn; columnIndex <= span.endColumn; columnIndex += 1) {
        const address = XLSX.utils.encode_cell({ r: span.rowIndex, c: columnIndex });
        if (!worksheet[address]) {
          worksheet[address] = { t: 's', v: '' };
        }
        worksheet[address].s = {
          alignment: { horizontal: 'left', vertical: 'center' },
          fill: { patternType: 'solid', fgColor: { rgb: span.isRollup ? toExcelColor(span.palette.soft) : toExcelColor(span.palette.start) } },
          font: { bold: true, color: { rgb: span.isRollup ? toExcelColor(span.palette.border) : toExcelColor(span.palette.text) } },
          border: {
            top: { style: span.isRollup ? 'medium' : 'thin', color: { rgb: toExcelColor(span.palette.border) } },
            bottom: { style: span.isRollup ? 'medium' : 'thin', color: { rgb: toExcelColor(span.palette.border) } },
            left: columnIndex === span.startColumn ? { style: 'medium', color: { rgb: toExcelColor(span.palette.border) } } : { style: 'thin', color: { rgb: toExcelColor(span.palette.border) } },
            right: columnIndex === span.endColumn ? { style: 'medium', color: { rgb: toExcelColor(span.palette.border) } } : { style: 'thin', color: { rgb: toExcelColor(span.palette.border) } },
          },
        };
      }

      const startAddress = XLSX.utils.encode_cell({ r: span.rowIndex, c: span.startColumn });
      worksheet[startAddress].v = span.label;
    });

    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, 'Project Plan');
    const milestoneSheetRows: Array<Array<string | number>> = [
      ['Milestones'],
      ['Title', 'Task', 'Target Date', 'Status'],
      ...project.milestones.map((milestone) => [
        milestone.title,
        flat.find((item) => item.id === milestone.workItemId)?.title || 'General',
        formatDateLabel(milestone.targetDate),
        milestone.status,
      ]),
    ];
    const milestoneSheet = XLSX.utils.aoa_to_sheet(milestoneSheetRows) as XLSX.WorkSheet & Record<string, any>;
    milestoneSheet['!cols'] = [{ wch: 30 }, { wch: 32 }, { wch: 16 }, { wch: 16 }];
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
    const sourceElement = source === 'chart' ? ganttChartScrollRef.current : ganttBottomScrollRef.current;
    const targetElement = source === 'chart' ? ganttBottomScrollRef.current : ganttChartScrollRef.current;

    if (sourceElement && targetElement) {
      targetElement.scrollLeft = sourceElement.scrollLeft;
    }

    requestAnimationFrame(() => {
      ganttScrollSyncRef.current = false;
    });
  };
  const handleGanttChartWheel: React.WheelEventHandler<HTMLDivElement> = (event) => {
    if (!ganttChartScrollRef.current) {
      return;
    }

    if (Math.abs(event.deltaY) > Math.abs(event.deltaX)) {
      event.preventDefault();
      ganttChartScrollRef.current.scrollBy({ left: event.deltaY, behavior: 'auto' });
    }
  };
  const methodologyOptions = useMemo(() => resolveCatalogOptions(methodologyCatalog, DEFAULT_METHODOLOGIES, overview.methodology), [methodologyCatalog, overview.methodology]);
  const billingTypeOptions = useMemo(() => resolveCatalogOptions(billingTypeCatalog, DEFAULT_BILLING_TYPES, billing.billingType), [billingTypeCatalog, billing.billingType]);
  const fundingSourceOptions = useMemo(() => resolveCatalogOptions(fundingSourceCatalog, DEFAULT_FUNDING_SOURCES, overview.fundingSource), [fundingSourceCatalog, overview.fundingSource]);
  const resourceRoleOptions = useMemo(() => resolveCatalogOptions(resourceRoleCatalog, DEFAULT_RESOURCE_ROLES, resource.allocationRole), [resourceRoleCatalog, resource.allocationRole]);
  const memberRoleOptions = useMemo(() => resolveCatalogOptions(memberRoleCatalog, DEFAULT_MEMBER_ROLES, member.role), [memberRoleCatalog, member.role]);
  const taskStatusOptions = useMemo(() => resolveCatalogOptions(taskStatusCatalog, DEFAULT_TASK_STATUSES, work.status), [taskStatusCatalog, work.status]);
  const taskPriorityOptions = useMemo(() => resolveCatalogOptions(taskPriorityCatalog, DEFAULT_TASK_PRIORITIES, work.priority), [taskPriorityCatalog, work.priority]);
  const deliverableStatusOptions = useMemo(() => resolveCatalogOptions(deliverableStatusCatalog, DEFAULT_DELIVERABLE_STATUSES, deliverable.status), [deliverableStatusCatalog, deliverable.status]);
  const riskStatusOptions = useMemo(() => resolveCatalogOptions(riskStatusCatalog, DEFAULT_RISK_STATUSES, risk.status), [riskStatusCatalog, risk.status]);
  const riskCategoryOptions = useMemo(() => resolveCatalogOptions(riskCategoryCatalog, DEFAULT_RISK_CATEGORIES, risk.category), [riskCategoryCatalog, risk.category]);
  const riskResponseStrategyOptions = useMemo(() => resolveCatalogOptions(riskResponseStrategyCatalog, DEFAULT_RISK_RESPONSE_STRATEGIES, risk.responseStrategy), [riskResponseStrategyCatalog, risk.responseStrategy]);
  const qualityCheckpointStatusOptions = useMemo(() => resolveCatalogOptions(qualityCheckpointStatusCatalog, DEFAULT_QUALITY_CHECKPOINT_STATUSES, qualityCheckpoint.status), [qualityCheckpointStatusCatalog, qualityCheckpoint.status]);
  const nonConformanceStatusOptions = useMemo(() => resolveCatalogOptions(nonConformanceStatusCatalog, DEFAULT_NON_CONFORMANCE_STATUSES, nonConformance.status), [nonConformanceStatusCatalog, nonConformance.status]);
  const nonConformanceSeverityOptions = useMemo(() => resolveCatalogOptions(nonConformanceSeverityCatalog, DEFAULT_NON_CONFORMANCE_SEVERITIES, nonConformance.severity), [nonConformanceSeverityCatalog, nonConformance.severity]);
  const expenseCategoryOptions = useMemo(() => resolveCatalogOptions(expenseCategoryCatalog, DEFAULT_EXPENSE_CATEGORIES, expense.category), [expenseCategoryCatalog, expense.category]);
  const invoiceCurrencyOptions = useMemo(() => {
    const codes = currencies
      .filter((currency) => currency.isActive)
      .map((currency) => currency.code.trim().toUpperCase())
      .filter(Boolean);
    const values = codes.length > 0 ? codes : DEFAULT_CURRENCIES;
    return invoice.currency && !values.includes(invoice.currency) ? [invoice.currency, ...values] : values;
  }, [currencies, invoice.currency]);
  const expenseCurrencyOptions = useMemo(() => {
    const codes = currencies
      .filter((currency) => currency.isActive)
      .map((currency) => currency.code.trim().toUpperCase())
      .filter(Boolean);
    const values = codes.length > 0 ? codes : DEFAULT_CURRENCIES;
    return expense.currency && !values.includes(expense.currency) ? [expense.currency, ...values] : values;
  }, [currencies, expense.currency]);
  const changeTypeOptions = useMemo(() => resolveCatalogOptions(changeTypeCatalog, DEFAULT_CHANGE_TYPES, change.changeType), [changeTypeCatalog, change.changeType]);
  const issueStatusOptions = useMemo(() => resolveCatalogOptions(issueStatusCatalog, DEFAULT_ISSUE_STATUSES, issue.status), [issueStatusCatalog, issue.status]);
  const changeStatusOptions = useMemo(() => resolveCatalogOptions(changeStatusCatalog, DEFAULT_CHANGE_STATUSES, change.status), [changeStatusCatalog, change.status]);
  const issueSeverityOptions = useMemo(() => resolveCatalogOptions(issueSeverityCatalog, DEFAULT_ISSUE_SEVERITIES, issue.severity), [issueSeverityCatalog, issue.severity]);
  const timesheetWorkTypeOptions = useMemo(() => resolveCatalogOptions(timesheetWorkTypeCatalog, DEFAULT_TIMESHEET_WORK_TYPES, timesheet.workType), [timesheetWorkTypeCatalog, timesheet.workType]);
  const decisionStatusOptions = useMemo(() => resolveCatalogOptions(decisionStatusCatalog, DEFAULT_DECISION_STATUSES, decision.status), [decisionStatusCatalog, decision.status]);
  const meetingTypeOptions = useMemo(() => resolveCatalogOptions(meetingTypeCatalog, DEFAULT_MEETING_TYPES, meeting.meetingType), [meetingTypeCatalog, meeting.meetingType]);
  const actionItemStatusOptions = useMemo(() => resolveCatalogOptions(actionItemStatusCatalog, DEFAULT_ACTION_ITEM_STATUSES, actionItem.status), [actionItemStatusCatalog, actionItem.status]);
  const actionItemPriorityOptions = useMemo(() => resolveCatalogOptions(actionItemPriorityCatalog, DEFAULT_ACTION_ITEM_PRIORITIES, actionItem.priority), [actionItemPriorityCatalog, actionItem.priority]);
  const lessonCategoryOptions = useMemo(() => resolveCatalogOptions(lessonCategoryCatalog, DEFAULT_LESSON_CATEGORIES, lessonLearned.category), [lessonCategoryCatalog, lessonLearned.category]);
  const lessonVisibilityOptions = useMemo(() => resolveCatalogOptions(lessonVisibilityCatalog, DEFAULT_LESSON_VISIBILITIES, lessonLearned.visibility), [lessonVisibilityCatalog, lessonLearned.visibility]);
  const assetLinkTypeOptions = useMemo(() => resolveCatalogOptions(assetLinkTypeCatalog, DEFAULT_ASSET_LINK_TYPES, assetLink.linkType), [assetLinkTypeCatalog, assetLink.linkType]);
  const assetLinkStatusOptions = useMemo(() => resolveCatalogOptions(assetLinkStatusCatalog, DEFAULT_ASSET_LINK_STATUSES, assetLink.status), [assetLinkStatusCatalog, assetLink.status]);
  const documentCategoryOptions = useMemo(() => resolveCatalogOptions(documentCategoryCatalog, DEFAULT_DOCUMENT_CATEGORIES, doc.category), [documentCategoryCatalog, doc.category]);
  const documentTypeOptions = useMemo(() => resolveCatalogOptions(documentTypeCatalog, DEFAULT_DOCUMENT_TYPES, doc.documentType), [documentTypeCatalog, doc.documentType]);
  const activeUsers = useMemo(() => users.filter((user) => user.isActive), [users]);
  const activeBusinessPartners = useMemo(
    () => businessPartners.filter((partner) => partner.status === 'Active'),
    [businessPartners],
  );
  const activeContracts = useMemo(
    () => contracts.filter((contract) => !overview.businessPartnerId || contract.businessPartnerId === overview.businessPartnerId),
    [contracts, overview.businessPartnerId],
  );
  const userLookup = useMemo(
    () => new Map(users.map((user) => [user.id, formatUserLabel(user)])),
    [users],
  );
  const orderedMaterialRequisitions = useMemo(
    () =>
      [...materialRequisitions].sort((left, right) => {
        const leftRank = MATERIAL_STATUS_ORDER.indexOf(normalizeRequisitionStatus(left.status));
        const rightRank = MATERIAL_STATUS_ORDER.indexOf(normalizeRequisitionStatus(right.status));
        if (leftRank !== rightRank) {
          return leftRank - rightRank;
        }

        return new Date(right.requestDate).getTime() - new Date(left.requestDate).getTime();
      }),
    [materialRequisitions],
  );
  const materialSnapshot = useMemo(() => {
    const pendingApproval = materialRequisitions.filter((item) => normalizeRequisitionStatus(item.status) === 2).length;
    const pendingIssue = materialRequisitions.filter((item) => [3, 4, 5].includes(normalizeRequisitionStatus(item.status))).length;
    const completed = materialRequisitions.filter((item) => normalizeRequisitionStatus(item.status) === 7).length;
    const totalValue = materialRequisitions.reduce((sum, item) => sum + (item.totalValue || 0), 0);
    return { pendingApproval, pendingIssue, completed, totalValue };
  }, [materialRequisitions]);
  const externalArtifactOptions = useMemo(() => {
    if (!project) return [];
    switch (externalPolicy.artifactType) {
      case 'WorkItem':
        return flat.map((item) => ({ id: item.id, label: item.title }));
      case 'Deliverable':
        return project.deliverables.map((item) => ({ id: item.id, label: item.title }));
      case 'Document':
        return project.documents.map((item) => ({ id: item.id, label: item.documentName }));
      default:
        return [];
    }
  }, [externalPolicy.artifactType, flat, project]);

  const act = async (fn: () => Promise<void>, message: string, reset?: () => void) => {
    try {
      await fn();
      reset?.();
      await load();
      toast.success(message);
    } catch (error: any) {
      toast.error(error.message || message);
    }
  };

  const submitInvoiceRequest = async (invoiceRequestId: string) => {
    await act(() => projectService.submitInvoiceRequest(invoiceRequestId).then(() => Promise.resolve()), 'Invoice request submitted');
  };

  const sendInvoiceRequestToFinance = async (invoiceRequest: ProjectInvoiceRequestDto) => {
    const defaultReference = invoiceRequest.externalReference || invoiceRequest.requestNumber;
    const externalReference = window.prompt('Finance reference / exported invoice number', defaultReference);
    if (externalReference === null) {
      return;
    }

    await act(() => projectService.sendInvoiceRequestToFinance(invoiceRequest.id, {
      externalReference: externalReference.trim() || undefined,
    }).then(() => Promise.resolve()), 'Invoice request sent to finance');
  };

  const markInvoiceRequestInvoiced = async (invoiceRequest: ProjectInvoiceRequestDto) => {
    const defaultReference = invoiceRequest.externalReference || invoiceRequest.requestNumber;
    const externalReference = window.prompt('Invoice number / finance reference', defaultReference);
    if (externalReference === null) {
      return;
    }

    await act(() => projectService.markInvoiceRequestInvoiced(invoiceRequest.id, {
      externalReference: externalReference.trim() || undefined,
    }).then(() => Promise.resolve()), 'Invoice request marked as invoiced');
  };

  const markInvoiceRequestPaid = async (invoiceRequestId: string) => {
    await act(() => projectService.markInvoiceRequestPaid(invoiceRequestId).then(() => Promise.resolve()), 'Invoice request marked as paid');
  };

  const openMaterialDialog = (mode: 'create' | 'edit' | 'view', requisitionId?: string) => {
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

  if (loading || !project) return <div className="py-20 text-center text-muted-foreground">Loading project workspace...</div>;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="icon" onClick={() => router.push('/development/projects')}><ArrowLeft className="h-5 w-5" /></Button>
          <div>
            <div className="flex items-center gap-2"><h1 className="text-3xl font-bold tracking-tight">{project.title}</h1><Badge variant="outline">{project.projectCode}</Badge><Badge>{project.status}</Badge></div>
            <p className="text-muted-foreground">{project.summary || 'No summary provided.'}</p>
            {(project.portfolioName || project.programName) && <p className="text-sm text-muted-foreground">{[project.portfolioName, project.programName].filter(Boolean).join(' / ')}</p>}
          </div>
        </div>
        <Button onClick={() => act(() => projectService.updateProject(project.id, overview).then(() => Promise.resolve()), 'Project updated')}><Save className="mr-2 h-4 w-4" />Save</Button>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <TabsList className="grid w-full grid-cols-4 lg:grid-cols-8">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="plan">Plan</TabsTrigger>
          <TabsTrigger value="execution">Execution</TabsTrigger>
          <TabsTrigger value="analysis">Analysis</TabsTrigger>
          <TabsTrigger value="materials">Materials</TabsTrigger>
          <TabsTrigger value="governance">Governance</TabsTrigger>
          <TabsTrigger value="access">Access</TabsTrigger>
          <TabsTrigger value="documents">Documents</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="space-y-6">
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <Card><CardHeader className="pb-2"><CardTitle className="text-base">Progress</CardTitle></CardHeader><CardContent><div className="text-2xl font-semibold">{project.progressPercent}%</div></CardContent></Card>
            <Card><CardHeader className="pb-2"><CardTitle className="text-base">Budget</CardTitle></CardHeader><CardContent><div className="text-2xl font-semibold">{project.estimatedBudget?.toLocaleString() || 'N/A'}</div></CardContent></Card>
            <Card><CardHeader className="pb-2"><CardTitle className="text-base">Open Risks</CardTitle></CardHeader><CardContent><div className="text-2xl font-semibold">{project.risks.filter((x) => x.status === 'Open').length}</div></CardContent></Card>
            <Card><CardHeader className="pb-2"><CardTitle className="text-base">Open Issues</CardTitle></CardHeader><CardContent><div className="text-2xl font-semibold">{project.issues.filter((x) => x.status === 'Open').length}</div></CardContent></Card>
          </div>
          <Card>
            <CardHeader>
              <CardTitle>Workflow</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex flex-wrap gap-2">
                <Badge variant="outline">{project.status}</Badge>
                {project.portfolioName ? <Badge variant="outline">{project.portfolioName}</Badge> : null}
                {project.programName ? <Badge variant="outline">{project.programName}</Badge> : null}
              </div>
              <div className="flex flex-wrap gap-2">
                <WorkflowApprovalActions entityType="Project" entityId={project.id} entityLabel="Project" entityNumber={project.projectCode} status={project.status} loadWorkflowSummary showStepBadge canSubmit={project.status === 'Draft'} canApproveReject={project.status === 'PendingApproval'} onSubmit={() => projectService.submitProject(project.id)} onApprove={(comments) => projectService.approveProject(project.id, comments)} onReject={(comments) => projectService.rejectProject(project.id, comments || 'Rejected', comments)} onAfterAction={async () => load()} onOpenWorkflows={() => router.push('/administration/workflow')} />
                <Button variant="outline" size="sm" onClick={() => setShowProjectApprovalHistory((value) => !value)}>
                  {showProjectApprovalHistory ? 'Hide Approval History' : 'View Approval History'}
                </Button>
              </div>
              {showProjectApprovalHistory ? <WorkflowApprovalHistoryPanel entityType="Project" entityId={project.id} /> : null}
            </CardContent>
          </Card>
          <div className="grid gap-6 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Financial Control Snapshot</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {financialSummary ? (
                  <>
                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Budget Baseline</div><div className="mt-1 text-2xl font-semibold">{financialSummary.budgetBaseline.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Actual Cost</div><div className="mt-1 text-2xl font-semibold">{financialSummary.actualCost.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Committed Cost</div><div className="mt-1 text-2xl font-semibold">{financialSummary.committedCost.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Pending Cost</div><div className="mt-1 text-2xl font-semibold">{financialSummary.pendingCost.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Procurement Requested</div><div className="mt-1 text-2xl font-semibold">{financialSummary.procurementRequestedAmount.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Open Procurement Commitment</div><div className="mt-1 text-2xl font-semibold">{financialSummary.procurementOpenCommitmentAmount.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Planned Value</div><div className="mt-1 text-2xl font-semibold">{financialSummary.plannedValue.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Earned Value</div><div className="mt-1 text-2xl font-semibold">{financialSummary.earnedValue.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Estimate at Completion</div><div className="mt-1 text-2xl font-semibold">{financialSummary.estimateAtCompletion.toLocaleString()}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Total Exposure</div><div className="mt-1 text-2xl font-semibold">{financialSummary.totalExposureAmount.toLocaleString()}</div></div>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant={financialSummary.thresholdExceeded ? 'destructive' : 'secondary'}>
                        {financialSummary.thresholdExceeded ? 'Threshold exceeded' : 'Within threshold'}
                      </Badge>
                      <Badge variant="outline">{financialSummary.healthStatus}</Badge>
                      <span className="text-sm text-muted-foreground">Consumption {financialSummary.budgetConsumptionPercent}%</span>
                      <span className="text-sm text-muted-foreground">CPI {financialSummary.costPerformanceIndex?.toFixed(2) ?? 'N/A'} / SPI {financialSummary.schedulePerformanceIndex?.toFixed(2) ?? 'N/A'}</span>
                      <span className="text-sm text-muted-foreground">Warn {financialSummary.thresholdWarningPercent}% / Critical {financialSummary.thresholdCriticalPercent}%</span>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {financialSummary.currentBudgetRevisionName ? <Badge variant="outline">{financialSummary.currentBudgetRevisionName}</Badge> : null}
                      {financialSummary.activeForecastVersionName ? <Badge variant="outline">{financialSummary.activeForecastVersionName}</Badge> : null}
                      <Badge variant="outline">{financialSummary.budgetRevisionCount} budget revision(s)</Badge>
                      <Badge variant="outline">{financialSummary.forecastVersionCount} forecast version(s)</Badge>
                    </div>
                    <div className="space-y-2">
                      {financialSummary.alerts.length === 0 ? <div className="text-sm text-muted-foreground">No financial alerts are currently triggered.</div> : null}
                      {financialSummary.alerts.map((alert, index) => (
                        <div key={`${alert.severity}-${index}`} className="rounded-lg border p-4 text-sm">
                          <div className="font-medium">{alert.severity}</div>
                          <div className="text-muted-foreground">{alert.message}</div>
                        </div>
                      ))}
                    </div>
                  </>
                ) : (
                  <div className="text-sm text-muted-foreground">Financial details are not available for this user.</div>
                )}
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>Integration Snapshot</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                {integrationSummary ? (
                  <>
                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Business Partner</div><div className="mt-1 text-lg font-semibold">{integrationSummary.hasBusinessPartner ? 'Linked' : 'Not linked'}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Contract</div><div className="mt-1 text-lg font-semibold">{integrationSummary.hasContract ? 'Linked' : 'Not linked'}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Assets</div><div className="mt-1 text-lg font-semibold">{integrationSummary.linkedAssetCount}</div></div>
                      <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Access Policies</div><div className="mt-1 text-lg font-semibold">{integrationSummary.sharedExternalPolicyCount}</div></div>
                    </div>
                    <div className="space-y-2">
                      {integrationSummary.links.slice(0, 6).map((link, index) => (
                        <div key={`${link.linkType}-${link.reference}-${index}`} className="rounded-lg border p-4 text-sm">
                          <div className="font-medium">{link.linkType}</div>
                          <div className="text-muted-foreground">{link.status} | {link.reference}</div>
                        </div>
                      ))}
                      {integrationSummary.links.length === 0 ? <div className="text-sm text-muted-foreground">No integration links are available.</div> : null}
                    </div>
                    {integrationSummary.warnings.length ? (
                      <div className="space-y-2">
                        {integrationSummary.warnings.map((warning, index) => (
                          <div key={`${warning}-${index}`} className="rounded-lg border p-4 text-sm text-muted-foreground">{warning}</div>
                        ))}
                      </div>
                    ) : null}
                  </>
                ) : (
                  <div className="text-sm text-muted-foreground">Integration details are not available for this user.</div>
                )}
              </CardContent>
            </Card>
          </div>
          <Card>
            <CardHeader><CardTitle>Project Details</CardTitle></CardHeader>
            <CardContent className="grid gap-4 md:grid-cols-2">
              <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={overview.title || ''} onChange={(e) => setOverview((p) => ({ ...p, title: e.target.value }))} /></div>
              <div className="grid gap-2"><Label>Type</Label><Select value={overview.projectTypeId || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, projectTypeId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No type</SelectItem>{types.map((x) => <SelectItem key={x.id} value={x.id}>{x.name}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Priority</Label><Select value={overview.projectPriorityId || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, projectPriorityId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No priority</SelectItem>{priorities.map((x) => <SelectItem key={x.id} value={x.id}>{x.name}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Template</Label><Select value={overview.templateId || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, templateId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No template</SelectItem>{templates.map((x) => <SelectItem key={x.id} value={x.id}>{x.name}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Portfolio</Label><Select value={overview.portfolioId || 'none'} onValueChange={async (value) => { const portfolioId = value === 'none' ? undefined : value; setOverview((p) => ({ ...p, portfolioId, programId: undefined })); setPrograms(await projectService.getPrograms(portfolioId)); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No portfolio</SelectItem>{portfolios.map((x) => <SelectItem key={x.id} value={x.id}>{x.name}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2"><Label>Program</Label><Select value={overview.programId || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, programId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No program</SelectItem>{programs.map((x) => <SelectItem key={x.id} value={x.id}>{x.name}</SelectItem>)}</SelectContent></Select></div>
              <div className="grid gap-2">
                <Label>Methodology</Label>
                <Select value={overview.methodology || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, methodology: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select methodology" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No methodology</SelectItem>
                    {methodologyOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2"><Label>Start Date</Label><Input type="date" value={overview.startDate ? String(overview.startDate).slice(0, 10) : ''} onChange={(e) => setOverview((p) => ({ ...p, startDate: e.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Target End Date</Label><Input type="date" value={overview.targetEndDate ? String(overview.targetEndDate).slice(0, 10) : ''} onChange={(e) => setOverview((p) => ({ ...p, targetEndDate: e.target.value || undefined }))} /></div>
              <div className="grid gap-2"><Label>Estimated Budget</Label><Input type="number" value={overview.estimatedBudget ?? ''} onChange={(e) => setOverview((p) => ({ ...p, estimatedBudget: e.target.value ? Number(e.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Approved Budget</Label><Input type="number" value={overview.approvedBudget ?? ''} onChange={(e) => setOverview((p) => ({ ...p, approvedBudget: e.target.value ? Number(e.target.value) : undefined }))} /></div>
              <div className="grid gap-2"><Label>Actual Cost</Label><Input type="number" value={overview.actualCost ?? ''} onChange={(e) => setOverview((p) => ({ ...p, actualCost: e.target.value ? Number(e.target.value) : undefined }))} /></div>
              <div className="grid gap-2">
                <Label>Funding Source</Label>
                <Select value={overview.fundingSource || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, fundingSource: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select funding source" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No funding source</SelectItem>
                    {fundingSourceOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Business Partner</Label>
                <Select value={overview.businessPartnerId || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, businessPartnerId: value === 'none' ? undefined : value, contractId: undefined }))}>
                  <SelectTrigger><SelectValue placeholder="Select business partner" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No business partner</SelectItem>
                    {activeBusinessPartners.map((partner) => <SelectItem key={partner.id} value={partner.id}>{formatBusinessPartnerLabel(partner)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Contract</Label>
                <Select value={overview.contractId || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, contractId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select contract" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No contract</SelectItem>
                    {activeContracts.map((contract) => <SelectItem key={contract.id} value={contract.id}>{formatContractLabel(contract)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Sponsor</Label>
                <Select value={overview.sponsorId || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, sponsorId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select sponsor" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No sponsor</SelectItem>
                    {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2">
                <Label>Project Manager</Label>
                <Select value={overview.projectManagerId || 'none'} onValueChange={(value) => setOverview((p) => ({ ...p, projectManagerId: value === 'none' ? undefined : value }))}>
                  <SelectTrigger><SelectValue placeholder="Select project manager" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">No project manager</SelectItem>
                    {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid gap-2"><Label>Portal Access</Label><Select value={boolValue(overview.externalPortalAccessEnabled)} onValueChange={(value) => setOverview((p) => ({ ...p, externalPortalAccessEnabled: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Internal only</SelectItem><SelectItem value="true">Shared externally</SelectItem></SelectContent></Select></div>
              <div className="grid gap-2"><Label>Portal Collaboration</Label><Select value={boolValue(overview.externalCollaborationEnabled)} onValueChange={(value) => setOverview((p) => ({ ...p, externalCollaborationEnabled: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Read only</SelectItem><SelectItem value="true">Allow updates</SelectItem></SelectContent></Select></div>
              <div className="grid gap-2 md:col-span-2"><Label>Summary</Label><Textarea rows={3} value={overview.summary || ''} onChange={(e) => setOverview((p) => ({ ...p, summary: e.target.value }))} /></div>
              <div className="grid gap-2 md:col-span-2"><Label>Objectives</Label><Textarea rows={3} value={overview.objectives || ''} onChange={(e) => setOverview((p) => ({ ...p, objectives: e.target.value }))} /></div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Members</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-[1fr_1fr_auto]">
                <div className="grid gap-2">
                  <Label>Member</Label>
                  <Select value={member.userId || 'none'} onValueChange={(value) => setMember((p) => ({ ...p, userId: value === 'none' ? '' : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No user</SelectItem>
                      {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Role</Label>
                  <Select value={member.role || memberRoleOptions[0]} onValueChange={(value) => setMember((p) => ({ ...p, role: value }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{memberRoleOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.addMember(project.id, member).then(() => Promise.resolve()), 'Member added', () => setMember(memberInit))}><Plus className="mr-2 h-4 w-4" />Add</Button></div>
              </div>
              {project.members.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{userLookup.get(x.userId) || x.userId}</div><div className="text-sm text-muted-foreground">{x.role}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.removeMember(x.id), 'Member removed')}><Trash2 className="h-4 w-4" /></Button></div>)}
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Billing</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Name</Label><Input value={billing.name} onChange={(e) => setBilling((p) => ({ ...p, name: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Type</Label><Select value={billing.billingType} onValueChange={(value) => setBilling((p) => ({ ...p, billingType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{billingTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Amount</Label><Input type="number" value={billing.amount} onChange={(e) => setBilling((p) => ({ ...p, amount: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Billing Date</Label><Input type="date" value={String(billing.billingDate).slice(0, 10)} onChange={(e) => setBilling((p) => ({ ...p, billingDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Milestone</Label><Select value={billing.milestoneId || 'none'} onValueChange={(value) => setBilling((p) => ({ ...p, milestoneId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No milestone</SelectItem>{project.milestones.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-2"><Label>Description</Label><Input value={billing.description || ''} onChange={(e) => setBilling((p) => ({ ...p, description: e.target.value }))} /></div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.addBillingSchedule(project.id, billing).then(() => Promise.resolve()), 'Billing schedule added', () => setBilling(billingInit))}><Plus className="mr-2 h-4 w-4" />Add Schedule</Button></div>
              </div>
              <div className="grid gap-4 md:grid-cols-[1fr_1fr_1fr_auto]">
                <div className="grid gap-2"><Label>Schedule</Label><Select value={invoice.billingScheduleId || 'none'} onValueChange={(value) => { const selected = project.billingSchedules.find((x) => x.id === value); setInvoice((p) => ({ ...p, billingScheduleId: value === 'none' ? undefined : value, requestedAmount: selected?.amount ?? p.requestedAmount })); }}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Manual invoice</SelectItem>{project.billingSchedules.map((x) => <SelectItem key={x.id} value={x.id}>{x.name}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Requested Amount</Label><Input type="number" value={invoice.requestedAmount} onChange={(e) => setInvoice((p) => ({ ...p, requestedAmount: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2">
                  <Label>Currency</Label>
                  <Select value={invoice.currency || invoiceCurrencyOptions[0]} onValueChange={(value) => setInvoice((p) => ({ ...p, currency: value }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {invoiceCurrencyOptions.map((code) => {
                        const currency = currencies.find((item) => item.code.toUpperCase() === code.toUpperCase());
                        return (
                          <SelectItem key={code} value={code}>
                            {currency ? formatCurrencyLabel(currency) : code}
                          </SelectItem>
                        );
                      })}
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.createInvoiceRequest(project.id, invoice).then(() => Promise.resolve()), 'Invoice request created', () => setInvoice(invoiceInit))}><Plus className="mr-2 h-4 w-4" />Add Invoice</Button></div>
              </div>
              <div className="space-y-3">
                {project.billingSchedules.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.name}</div><div className="text-sm text-muted-foreground">{x.billingType} | {x.amount.toLocaleString()} | {formatDateLabel(x.billingDate)}</div></div><div className="flex gap-2"><Button variant="outline" size="sm" onClick={() => act(() => projectService.generateInvoiceRequestFromSchedule(x.id), 'Invoice request generated')}>Generate Invoice</Button><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteBillingSchedule(x.id), 'Billing schedule deleted')}><Trash2 className="h-4 w-4" /></Button></div></div>)}
                {project.invoiceRequests.map((x) => (
                  <div key={x.id} className="rounded-lg border p-4">
                    <div className="flex items-center justify-between gap-3">
                      <div className="font-medium">{x.requestNumber}</div>
                      <Badge variant={x.status === 'Paid' ? 'secondary' : x.status === 'Invoiced' ? 'secondary' : x.status === 'SentToFinance' ? 'secondary' : 'outline'}>{x.status}</Badge>
                    </div>
                    <div className="mt-1 text-sm text-muted-foreground">{x.currency} {x.requestedAmount.toLocaleString()} | {format(new Date(x.requestedAt), 'MMM dd, yyyy HH:mm')}</div>
                    {x.externalReference ? <div className="mt-1 text-xs text-muted-foreground">Reference: {x.externalReference}</div> : null}
                    <div className="mt-3 flex flex-wrap gap-2">
                      {x.status === 'Draft' ? <Button variant="outline" size="sm" onClick={() => submitInvoiceRequest(x.id)}>Submit</Button> : null}
                      {(x.status === 'Draft' || x.status === 'Submitted') ? <Button variant="outline" size="sm" onClick={() => sendInvoiceRequestToFinance(x)}>Send to Finance</Button> : null}
                      {(x.status === 'Submitted' || x.status === 'SentToFinance') ? <Button variant="outline" size="sm" onClick={() => markInvoiceRequestInvoiced(x)}>Mark Invoiced</Button> : null}
                      {(x.status === 'SentToFinance' || x.status === 'Invoiced') ? <Button variant="outline" size="sm" onClick={() => markInvoiceRequestPaid(x.id)}>Mark Paid</Button> : null}
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Budget Revisions</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Name</Label><Input value={budgetRevision.revisionName} onChange={(e) => setBudgetRevision((p) => ({ ...p, revisionName: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Type</Label><Select value={budgetRevision.revisionType || 'Revision'} onValueChange={(value) => setBudgetRevision((p) => ({ ...p, revisionType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Revision', 'Increase', 'Decrease', 'Baseline', 'ForecastAdjustment'].map((x) => <SelectItem key={x} value={x}>{x}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Effective Date</Label><Input type="date" value={budgetRevision.effectiveDate || today()} onChange={(e) => setBudgetRevision((p) => ({ ...p, effectiveDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Estimated</Label><Input type="number" value={budgetRevision.estimatedBudget} onChange={(e) => setBudgetRevision((p) => ({ ...p, estimatedBudget: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Approved</Label><Input type="number" value={budgetRevision.approvedBudget} onChange={(e) => setBudgetRevision((p) => ({ ...p, approvedBudget: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Committed</Label><Input type="number" value={budgetRevision.committedCost} onChange={(e) => setBudgetRevision((p) => ({ ...p, committedCost: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Forecast</Label><Input type="number" value={budgetRevision.forecastCost} onChange={(e) => setBudgetRevision((p) => ({ ...p, forecastCost: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Warn %</Label><Input type="number" value={budgetRevision.thresholdWarningPercent ?? 75} onChange={(e) => setBudgetRevision((p) => ({ ...p, thresholdWarningPercent: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Critical %</Label><Input type="number" value={budgetRevision.thresholdCriticalPercent ?? 90} onChange={(e) => setBudgetRevision((p) => ({ ...p, thresholdCriticalPercent: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Reason</Label><Input value={budgetRevision.changeReason || ''} onChange={(e) => setBudgetRevision((p) => ({ ...p, changeReason: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-3"><Label>Notes</Label><Textarea rows={2} value={budgetRevision.notes || ''} onChange={(e) => setBudgetRevision((p) => ({ ...p, notes: e.target.value }))} /></div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.createBudgetRevision(project.id, budgetRevision).then(() => Promise.resolve()), 'Budget revision created', () => setBudgetRevision({ ...budgetRevisionInit, estimatedBudget: overview.estimatedBudget ?? 0, approvedBudget: overview.approvedBudget ?? overview.estimatedBudget ?? 0, forecastCost: financialSummary?.forecastCost ?? 0 }))}><Plus className="mr-2 h-4 w-4" />Create Revision</Button></div>
              </div>
              <div className="space-y-3">
                {budgetRevisions.length === 0 ? <div className="text-sm text-muted-foreground">No budget revisions have been created yet.</div> : null}
                {budgetRevisions.map((x) => (
                  <div key={x.id} className="rounded-lg border p-4">
                    <div className="flex items-start justify-between gap-3">
                      <div className="space-y-2">
                        <div className="flex flex-wrap items-center gap-2">
                          <div className="font-medium">{x.revisionName}</div>
                          <Badge variant="outline">v{x.versionNumber}</Badge>
                          <Badge variant={x.status === 'Approved' ? 'secondary' : x.status === 'PendingApproval' ? 'default' : 'outline'}>{x.status}</Badge>
                        </div>
                        <div className="text-sm text-muted-foreground">{x.revisionType} | approved {x.approvedBudget.toLocaleString()} | committed {x.committedCost.toLocaleString()} | forecast {x.forecastCost.toLocaleString()}</div>
                        <div className="text-sm text-muted-foreground">Warn {x.thresholdWarningPercent}% / Critical {x.thresholdCriticalPercent}% | effective {formatDateLabel(x.effectiveDate)}</div>
                        {x.changeReason ? <div className="text-sm text-muted-foreground">{x.changeReason}</div> : null}
                        {x.rejectionReason ? <div className="text-sm text-red-600">{x.rejectionReason}</div> : null}
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <WorkflowApprovalActions
                          entityType="ProjectBudgetRevision"
                          entityId={x.id}
                          entityLabel="Budget Revision"
                          entityNumber={x.revisionName}
                          status={x.status}
                          loadWorkflowSummary
                          showStepBadge
                          canSubmit={x.status === 'Draft' || x.status === 'Rejected'}
                          canApproveReject={x.status === 'PendingApproval'}
                          onSubmit={() => projectService.submitBudgetRevision(x.id)}
                          onApprove={(comments) => projectService.approveBudgetRevision(x.id, comments)}
                          onReject={(comments) => projectService.rejectBudgetRevision(x.id, comments || 'Rejected', comments)}
                          onAfterAction={async () => load()}
                          onOpenWorkflows={() => router.push('/administration/workflow')}
                        />
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setExpandedBudgetRevisionHistoryId((current) => current === x.id ? null : x.id)}
                        >
                          {expandedBudgetRevisionHistoryId === x.id ? 'Hide Approval History' : 'View Approval History'}
                        </Button>
                      </div>
                    </div>
                    {expandedBudgetRevisionHistoryId === x.id ? (
                      <div className="mt-4">
                        <WorkflowApprovalHistoryPanel entityType="ProjectBudgetRevision" entityId={x.id} />
                      </div>
                    ) : null}
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Forecast Versions</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Name</Label><Input value={forecastVersion.versionName} onChange={(e) => setForecastVersion((p) => ({ ...p, versionName: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>As Of</Label><Input type="date" value={forecastVersion.asOfDate || today()} onChange={(e) => setForecastVersion((p) => ({ ...p, asOfDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Activate</Label><Select value={boolValue(forecastVersion.isActive)} onValueChange={(value) => setForecastVersion((p) => ({ ...p, isActive: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="true">Yes</SelectItem><SelectItem value="false">No</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Forecast Cost</Label><Input type="number" value={forecastVersion.forecastCost} onChange={(e) => setForecastVersion((p) => ({ ...p, forecastCost: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>EAC</Label><Input type="number" value={forecastVersion.estimateAtCompletion} onChange={(e) => setForecastVersion((p) => ({ ...p, estimateAtCompletion: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Revenue</Label><Input type="number" value={forecastVersion.forecastRevenue} onChange={(e) => setForecastVersion((p) => ({ ...p, forecastRevenue: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Margin</Label><Input type="number" value={forecastVersion.forecastMargin} onChange={(e) => setForecastVersion((p) => ({ ...p, forecastMargin: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2 md:col-span-3"><Label>Notes</Label><Textarea rows={2} value={forecastVersion.notes || ''} onChange={(e) => setForecastVersion((p) => ({ ...p, notes: e.target.value }))} /></div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.createForecastVersion(project.id, forecastVersion).then(() => Promise.resolve()), 'Forecast version created', () => setForecastVersion({ ...forecastVersionInit, forecastCost: financialSummary?.forecastCost ?? 0, estimateAtCompletion: financialSummary?.estimateAtCompletion ?? 0, forecastMargin: financialSummary?.grossMargin ?? 0 }))}><Plus className="mr-2 h-4 w-4" />Create Forecast</Button></div>
              </div>
              <div className="space-y-3">
                {forecastVersions.length === 0 ? <div className="text-sm text-muted-foreground">No forecast versions have been created yet.</div> : null}
                {forecastVersions.map((x) => (
                  <div key={x.id} className="flex items-center justify-between rounded-lg border p-4">
                    <div>
                      <div className="flex items-center gap-2">
                        <div className="font-medium">{x.versionName}</div>
                        <Badge variant="outline">v{x.versionNumber}</Badge>
                        {x.isActive ? <Badge>Active</Badge> : null}
                      </div>
                      <div className="text-sm text-muted-foreground">{formatDateLabel(x.asOfDate)} | forecast {x.forecastCost.toLocaleString()} | EAC {x.estimateAtCompletion.toLocaleString()} | revenue {x.forecastRevenue.toLocaleString()}</div>
                    </div>
                    {!x.isActive ? <Button variant="outline" size="sm" onClick={() => act(() => projectService.activateForecastVersion(x.id).then(() => Promise.resolve()), 'Forecast version activated')}>Activate</Button> : null}
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="plan" className="space-y-6">
          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <CardTitle>Work Items</CardTitle>
                {editingWorkItemId ? <Badge>Editing existing item</Badge> : null}
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2"><Label>Parent</Label><Select value={work.parentId || 'root'} onValueChange={(value) => setWork((p) => ({ ...p, parentId: value === 'root' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="root">Top level</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{`${' '.repeat(x.depth * 2)}${x.title}`}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Node Type</Label><Select value={work.nodeType} onValueChange={(value) => setWork((p) => ({ ...p, nodeType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Phase', 'Workstream', 'Task', 'Subtask', 'ChecklistItem'].map((x) => <SelectItem key={x} value={x}>{x}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={work.status || taskStatusOptions[0]} onValueChange={(value) => setWork((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{taskStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>% Complete</Label><Input type="number" value={work.percentComplete ?? 0} onChange={(e) => setWork((p) => ({ ...p, percentComplete: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2 md:col-span-3"><Label>Title</Label><Input value={work.title} onChange={(e) => setWork((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Priority</Label><Select value={work.priority || 'none'} onValueChange={(value) => setWork((p) => ({ ...p, priority: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select priority" /></SelectTrigger><SelectContent><SelectItem value="none">No priority</SelectItem>{taskPriorityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Planned Start</Label><Input type="date" value={work.plannedStartDate ? String(work.plannedStartDate).slice(0, 10) : ''} onChange={(e) => setWork((p) => ({ ...p, plannedStartDate: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Planned End</Label><Input type="date" value={work.plannedEndDate ? String(work.plannedEndDate).slice(0, 10) : ''} onChange={(e) => setWork((p) => ({ ...p, plannedEndDate: e.target.value || undefined }))} /></div>
                <div className="flex items-end gap-2">
                  {editingWorkItemId ? (
                    <Button variant="outline" onClick={resetWorkEditor}>Cancel</Button>
                  ) : null}
                  <Button onClick={saveWorkEditor}>
                    {editingWorkItemId ? <Save className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}
                    {editingWorkItemId ? 'Save Changes' : 'Add'}
                  </Button>
                </div>
              </div>
              {scheduleReasonRequired ? (
                <div className="grid gap-2 rounded-lg border border-amber-200 bg-amber-50/70 p-4">
                  <Label>Schedule Change Reason</Label>
                  <Textarea
                    rows={2}
                    placeholder="Explain why the planned dates changed."
                    value={work.scheduleChangeReason || ''}
                    onChange={(e) => setWork((p) => ({ ...p, scheduleChangeReason: e.target.value }))}
                  />
                  <div className="text-xs text-amber-700">
                    A locked baseline exists for this project, so date changes must include a reason.
                  </div>
                </div>
              ) : null}
              <div className="flex flex-wrap gap-2">
                <Button variant={taskView === 'tree' ? 'default' : 'outline'} size="sm" onClick={() => setTaskView('tree')}>List</Button>
                <Button variant={taskView === 'kanban' ? 'default' : 'outline'} size="sm" onClick={() => setTaskView('kanban')}>Kanban</Button>
                <Button variant={taskView === 'timeline' ? 'default' : 'outline'} size="sm" onClick={() => setTaskView('timeline')}>Gantt</Button>
              </div>
              {taskView === 'tree' ? (
                <WorkTree
                  items={project.workItems}
                  editingId={editingWorkItemId}
                  onEdit={beginEditWorkItem}
                  onDelete={(workId) => act(() => projectService.deleteWorkItem(workId), 'Work item deleted', () => {
                    if (editingWorkItemId === workId) {
                      resetWorkEditor();
                    }
                  })}
                />
              ) : null}
              {taskView === 'kanban' ? (
                <div className="grid gap-4 xl:grid-cols-4">
                  {TASK_BOARD_STATUSES.map((status) => {
                    const items = taskBoardItems.filter((item) => item.status === status);
                    return (
                      <div key={status} className="rounded-lg border bg-muted/20 p-3">
                        <div className="mb-3 flex items-center justify-between">
                          <div className="font-medium">{formatCatalogLabel(status)}</div>
                          <Badge variant="outline">{items.length}</Badge>
                        </div>
                        <div className="space-y-3">
                          {items.length === 0 ? <div className="text-sm text-muted-foreground">No items</div> : null}
                          {items.map((item) => (
                            <div key={item.id} className={`rounded-lg border bg-background p-4 ${editingWorkItemId === item.id ? 'border-primary bg-primary/5' : ''}`}>
                              <div className="flex items-start justify-between gap-2">
                                <div className="font-medium">{item.title}</div>
                                <Button variant="ghost" size="sm" onClick={() => beginEditWorkItem(item)}>
                                  <Pencil className="h-4 w-4" />
                                </Button>
                              </div>
                              <div className="text-xs text-muted-foreground">{item.nodeType} | {item.percentComplete}% complete</div>
                              {(item.plannedStartDate || item.plannedEndDate) ? <div className="mt-2 text-xs text-muted-foreground">{formatDateLabel(item.plannedStartDate)} - {formatDateLabel(item.plannedEndDate)}</div> : null}
                            </div>
                          ))}
                        </div>
                      </div>
                    );
                  })}
                </div>
              ) : null}
              {taskView === 'timeline' ? (
                timelineBounds ? (
                  <div className="rounded-lg border bg-muted/10 p-6">
                    <div className="flex flex-wrap items-start justify-between gap-4">
                      <div className="space-y-2">
                        <div className="text-lg font-semibold">Gantt Planner</div>
                        <div className="text-sm text-muted-foreground">
                          Open the project plan in a larger workspace for the full WBS grid, chart, and export actions.
                        </div>
                        <div className="flex flex-wrap gap-2 text-sm text-muted-foreground">
                          {ganttSummary ? (
                            <>
                              <Badge variant="outline">{ganttSummary.scheduledItems} scheduled items</Badge>
                              <Badge variant="outline">{ganttSummary.spanDays} day span</Badge>
                              <Badge variant="outline">{ganttSummary.phases} phases</Badge>
                              <Badge variant={ganttSummary.overdueItems > 0 ? 'destructive' : 'secondary'}>{ganttSummary.overdueItems} overdue</Badge>
                            </>
                          ) : null}
                        </div>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        <Button onClick={() => setGanttDialogOpen(true)}>
                          <Maximize2 className="mr-2 h-4 w-4" />
                          Open Gantt
                        </Button>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="text-sm text-muted-foreground">Add planned start and end dates to work items to build the Gantt view.</div>
                )
              ) : null}
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Task Dependencies</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2"><Label>Predecessor</Label><Select value={dependency.predecessorWorkItemId || 'none'} onValueChange={(value) => setDependency((p) => ({ ...p, predecessorWorkItemId: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select work item</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Successor</Label><Select value={dependency.successorWorkItemId || 'none'} onValueChange={(value) => setDependency((p) => ({ ...p, successorWorkItemId: value === 'none' ? '' : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Select work item</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Type</Label><Select value={dependency.dependencyType || 'FS'} onValueChange={(value) => setDependency((p) => ({ ...p, dependencyType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['FS', 'SS', 'FF', 'SF'].map((x) => <SelectItem key={x} value={x}>{x}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Lag Days</Label><Input type="number" value={dependency.lagDays ?? 0} onChange={(e) => setDependency((p) => ({ ...p, lagDays: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Enforced</Label><Select value={boolValue(dependency.isEnforced)} onValueChange={(value) => setDependency((p) => ({ ...p, isEnforced: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="true">Enforced</SelectItem><SelectItem value="false">Warning only</SelectItem></SelectContent></Select></div>
                <div className="flex items-end"><Button disabled={!dependency.predecessorWorkItemId || !dependency.successorWorkItemId || dependency.predecessorWorkItemId === dependency.successorWorkItemId} onClick={() => act(() => projectService.addTaskDependency(project.id, dependency).then(() => Promise.resolve()), 'Dependency added', () => setDependency(dependencyInit))}><Plus className="mr-2 h-4 w-4" />Add Dependency</Button></div>
              </div>
              <div className="space-y-3">
                {project.taskDependencies.length === 0 ? <div className="text-sm text-muted-foreground">No task dependencies are configured.</div> : null}
                {project.taskDependencies.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{workItemTitles.get(x.predecessorWorkItemId) || x.predecessorWorkItemId}{' -> '}{workItemTitles.get(x.successorWorkItemId) || x.successorWorkItemId}</div><div className="text-sm text-muted-foreground">{x.dependencyType} | lag {x.lagDays} day(s) | {x.isEnforced ? 'Enforced' : 'Warning only'}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteTaskDependency(x.id), 'Dependency deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Milestones</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={milestone.title} onChange={(e) => setMilestone((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Target Date</Label><Input type="date" value={milestone.targetDate ? String(milestone.targetDate).slice(0, 10) : ''} onChange={(e) => setMilestone((p) => ({ ...p, targetDate: e.target.value }))} /></div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.addMilestone(project.id, milestone).then(() => Promise.resolve()), 'Milestone added', () => setMilestone(milestoneInit))}><Plus className="mr-2 h-4 w-4" />Add</Button></div>
              </div>
              {project.milestones.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.title}</div><div className="text-sm text-muted-foreground">{formatDateLabel(x.targetDate)} | {x.status}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteMilestone(x.id), 'Milestone deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Resource Allocations</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2">
                  <Label>User</Label>
                  <Select value={resource.userId || 'none'} onValueChange={(value) => setResource((p) => ({ ...p, userId: value === 'none' ? '' : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No user</SelectItem>
                      {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Role</Label><Select value={resource.allocationRole} onValueChange={(value) => setResource((p) => ({ ...p, allocationRole: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{resourceRoleOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Type</Label><Select value={resource.allocationType} onValueChange={(value) => setResource((p) => ({ ...p, allocationType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Hours', 'Percent'].map((x) => <SelectItem key={x} value={x}>{x}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Value</Label><Input type="number" value={resource.allocationValue} onChange={(e) => setResource((p) => ({ ...p, allocationValue: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Work Item</Label><Select value={resource.workItemId || 'none'} onValueChange={(value) => setResource((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Project level</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Start Date</Label><Input type="date" value={String(resource.startDate).slice(0, 10)} onChange={(e) => setResource((p) => ({ ...p, startDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>End Date</Label><Input type="date" value={String(resource.endDate).slice(0, 10)} onChange={(e) => setResource((p) => ({ ...p, endDate: e.target.value }))} /></div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.addResourceAllocation(project.id, resource).then(() => Promise.resolve()), 'Resource allocation added', () => setResource(resourceInit))}><Plus className="mr-2 h-4 w-4" />Add Allocation</Button></div>
              </div>
              {project.resourceAllocations.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{userLookup.get(x.userId) || x.userId} | {x.allocationRole}</div><div className="text-sm text-muted-foreground">{formatDateLabel(x.startDate)} - {formatDateLabel(x.endDate)} | {x.allocationValue} {x.allocationType} | {x.capacityUtilizationPercent}%</div></div><div className="flex gap-2">{x.status !== 'Approved' && <Button variant="outline" size="sm" onClick={() => act(() => projectService.approveResourceAllocation(x.id).then(() => Promise.resolve()), 'Resource allocation approved')}>Approve</Button>}<Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteResourceAllocation(x.id), 'Resource allocation deleted')}><Trash2 className="h-4 w-4" /></Button></div></div>)}
            </CardContent>
          </Card>
        </TabsContent>
        <TabsContent value="execution" className="space-y-6">
          <Card>
            <CardHeader><CardTitle>Deliverables</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={deliverable.title} onChange={(e) => setDeliverable((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={deliverable.status || deliverableStatusOptions[0]} onValueChange={(value) => setDeliverable((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{deliverableStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Target Date</Label><Input type="date" value={deliverable.targetDate ? String(deliverable.targetDate).slice(0, 10) : ''} onChange={(e) => setDeliverable((p) => ({ ...p, targetDate: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Work Item</Label><Select value={deliverable.workItemId || 'none'} onValueChange={(value) => setDeliverable((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No work item</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Milestone</Label><Select value={deliverable.milestoneId || 'none'} onValueChange={(value) => setDeliverable((p) => ({ ...p, milestoneId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No milestone</SelectItem>{project.milestones.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>External Submission</Label><Select value={boolValue(deliverable.externalSubmissionAllowed)} onValueChange={(value) => setDeliverable((p) => ({ ...p, externalSubmissionAllowed: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Internal only</SelectItem><SelectItem value="true">Allowed</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>External Sign-off</Label><Select value={boolValue(deliverable.externalSignOffRequired)} onValueChange={(value) => setDeliverable((p) => ({ ...p, externalSignOffRequired: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Not required</SelectItem><SelectItem value="true">Required</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Portal Visibility</Label><Select value={boolValue(deliverable.isExternalVisible)} onValueChange={(value) => setDeliverable((p) => ({ ...p, isExternalVisible: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Internal only</SelectItem><SelectItem value="true">Visible externally</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-3"><Label>Description</Label><Textarea rows={2} value={deliverable.description || ''} onChange={(e) => setDeliverable((p) => ({ ...p, description: e.target.value }))} /></div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.addDeliverable(project.id, deliverable).then(() => Promise.resolve()), 'Deliverable added', () => setDeliverable(deliverableInit))}><Plus className="mr-2 h-4 w-4" />Add Deliverable</Button></div>
              </div>
              <div className="space-y-3">
                {project.deliverables.length === 0 ? <div className="text-sm text-muted-foreground">No deliverables are available.</div> : null}
                {project.deliverables.map((x) => (
                  <div key={x.id} className="rounded-lg border p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="space-y-2">
                        <div className="font-medium">{x.title}</div>
                        <div className="text-sm text-muted-foreground">
                          {x.status}
                          {x.targetDate ? ` | due ${formatDateLabel(x.targetDate)}` : ''}
                          {x.workItemId ? ` | ${workItemTitles.get(x.workItemId) || x.workItemId}` : ''}
                          {x.milestoneId ? ` | ${milestoneTitles.get(x.milestoneId) || x.milestoneId}` : ''}
                        </div>
                        <div className="flex flex-wrap gap-2">
                          {x.externalSubmissionAllowed ? <Badge variant="outline">Portal submission</Badge> : null}
                          {x.externalSignOffRequired ? <Badge variant="outline">External sign-off required</Badge> : null}
                          {x.isExternalVisible ? <Badge variant="outline">Visible externally</Badge> : null}
                        </div>
                      </div>
                      <div className="flex min-w-[220px] flex-col items-stretch gap-2">
                        <WorkflowApprovalActions
                          entityType="ProjectDeliverable"
                          entityId={x.id}
                          entityLabel="Deliverable"
                          entityNumber={x.title}
                          status={x.status}
                          loadWorkflowSummary
                          showStepBadge
                          canSubmit={x.status === 'Draft' || x.status === 'Rejected'}
                          canApproveReject={x.status === 'PendingApproval'}
                          onSubmit={() => projectService.submitDeliverable(x.id, { notes: 'Submitted from workspace' })}
                          onApprove={(comments) => projectService.approveDeliverable(x.id, comments)}
                          onReject={(comments) => projectService.rejectDeliverable(x.id, comments || 'Rejected from workspace')}
                          onAfterAction={async () => load()}
                          onOpenWorkflows={() => router.push('/administration/workflow')}
                        />
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setExpandedDeliverableHistoryId((current) => current === x.id ? null : x.id)}
                        >
                          {expandedDeliverableHistoryId === x.id ? 'Hide Approval History' : 'View Approval History'}
                        </Button>
                      </div>
                    </div>
                    {x.externalApprovedAt ? (
                      <div className="mt-2 text-sm text-muted-foreground">
                        External sign-off recorded {formatDateLabel(x.externalApprovedAt)}
                        {x.externalApprovalNotes ? ` | ${x.externalApprovalNotes}` : ''}
                      </div>
                    ) : null}
                    {x.acceptanceNotes ? <div className="mt-2 text-sm text-muted-foreground">{x.acceptanceNotes}</div> : null}
                    {expandedDeliverableHistoryId === x.id ? (
                      <div className="mt-4">
                        <WorkflowApprovalHistoryPanel entityType="ProjectDeliverable" entityId={x.id} />
                      </div>
                    ) : null}
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Timesheets</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2">
                  <Label>User</Label>
                  <Select value={timesheet.userId || 'none'} onValueChange={(value) => setTimesheet((p) => ({ ...p, userId: value === 'none' ? '' : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No user</SelectItem>
                      {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Work Item</Label><Select value={timesheet.workItemId || 'none'} onValueChange={(value) => setTimesheet((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Project level</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Entry Date</Label><Input type="date" value={timesheet.entryDate ? String(timesheet.entryDate).slice(0, 10) : ''} onChange={(e) => setTimesheet((p) => ({ ...p, entryDate: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Hours</Label><Input type="number" value={timesheet.hours} onChange={(e) => setTimesheet((p) => ({ ...p, hours: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Hourly Rate</Label><Input type="number" value={timesheet.hourlyRate ?? 0} onChange={(e) => setTimesheet((p) => ({ ...p, hourlyRate: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Work Type</Label><Select value={timesheet.workType || timesheetWorkTypeOptions[0]} onValueChange={(value) => setTimesheet((p) => ({ ...p, workType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{timesheetWorkTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Billable</Label><Select value={boolValue(timesheet.isBillable)} onValueChange={(value) => setTimesheet((p) => ({ ...p, isBillable: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Non-billable</SelectItem><SelectItem value="true">Billable</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Textarea rows={2} value={timesheet.notes || ''} onChange={(e) => setTimesheet((p) => ({ ...p, notes: e.target.value }))} /></div>
                <div className="flex items-end"><Button disabled={!timesheet.userId} onClick={() => act(() => projectService.addTimesheet(project.id, timesheet).then(() => Promise.resolve()), 'Timesheet entry added', () => setTimesheet({ ...timesheetInit, userId: currentUserId }))}><Plus className="mr-2 h-4 w-4" />Add Entry</Button></div>
              </div>
              <div className="space-y-3">
                {project.timesheetEntries.length === 0 ? <div className="text-sm text-muted-foreground">No timesheet entries are available.</div> : null}
                {project.timesheetEntries.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{userLookup.get(x.userId) || x.userId} | {x.hours}h</div><div className="text-sm text-muted-foreground">{formatDateLabel(x.entryDate)} | {x.workType} | {x.status} | cost {x.costAmount.toLocaleString()}</div></div>{x.status !== 'Approved' ? <Button variant="outline" size="sm" onClick={() => act(() => projectService.approveTimesheet(x.id).then(() => Promise.resolve()), 'Timesheet approved')}>Approve</Button> : null}</div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Expenses</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2">
                  <Label>User</Label>
                  <Select value={expense.userId || 'none'} onValueChange={(value) => setExpense((p) => ({ ...p, userId: value === 'none' ? '' : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select user" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No user</SelectItem>
                      {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Work Item</Label><Select value={expense.workItemId || 'none'} onValueChange={(value) => setExpense((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">Project level</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Expense Date</Label><Input type="date" value={expense.expenseDate ? String(expense.expenseDate).slice(0, 10) : ''} onChange={(e) => setExpense((p) => ({ ...p, expenseDate: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Category</Label><Select value={expense.category || expenseCategoryOptions[0]} onValueChange={(value) => setExpense((p) => ({ ...p, category: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{expenseCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2">
                  <Label>Currency</Label>
                  <Select value={expense.currency || expenseCurrencyOptions[0]} onValueChange={(value) => setExpense((p) => ({ ...p, currency: value }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {expenseCurrencyOptions.map((code) => {
                        const currency = currencies.find((item) => item.code.toUpperCase() === code.toUpperCase());
                        return (
                          <SelectItem key={code} value={code}>
                            {currency ? formatCurrencyLabel(currency) : code}
                          </SelectItem>
                        );
                      })}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Amount</Label><Input type="number" value={expense.amount} onChange={(e) => setExpense((p) => ({ ...p, amount: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Tax Amount</Label><Input type="number" value={expense.taxAmount ?? 0} onChange={(e) => setExpense((p) => ({ ...p, taxAmount: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Billable</Label><Select value={boolValue(expense.isBillable)} onValueChange={(value) => setExpense((p) => ({ ...p, isBillable: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Non-billable</SelectItem><SelectItem value="true">Billable</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-3"><Label>Notes</Label><Textarea rows={2} value={expense.notes || ''} onChange={(e) => setExpense((p) => ({ ...p, notes: e.target.value }))} /></div>
                <div className="flex items-end"><Button disabled={!expense.userId} onClick={() => act(() => projectService.addExpense(project.id, expense).then(() => Promise.resolve()), 'Expense added', () => setExpense({ ...expenseInit, userId: currentUserId }))}><Plus className="mr-2 h-4 w-4" />Add Expense</Button></div>
              </div>
              <div className="space-y-3">
                {project.expenses.length === 0 ? <div className="text-sm text-muted-foreground">No expense entries are available.</div> : null}
                {project.expenses.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{userLookup.get(x.userId) || x.userId} | {x.currency} {(x.amount + x.taxAmount).toLocaleString()}</div><div className="text-sm text-muted-foreground">{formatDateLabel(x.expenseDate)} | {x.category} | {x.status}</div></div>{x.status !== 'Approved' ? <Button variant="outline" size="sm" onClick={() => act(() => projectService.approveExpense(x.id).then(() => Promise.resolve()), 'Expense approved')}>Approve</Button> : null}</div>)}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="analysis" className="space-y-6">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between"><CardTitle>Schedule Analysis</CardTitle><Button variant="outline" size="sm" onClick={() => act(() => loadAdvanced(project.id), 'Schedule analysis refreshed')}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div><div className="text-sm text-muted-foreground">Dependencies</div><div className="text-2xl font-semibold">{scheduleAnalysis?.dependencyCount ?? 0}</div></div>
                <div><div className="text-sm text-muted-foreground">Critical Path Tasks</div><div className="text-2xl font-semibold">{scheduleAnalysis?.criticalPathTaskCount ?? 0}</div></div>
                <div><div className="text-sm text-muted-foreground">Forecast Finish</div><div className="text-2xl font-semibold">{formatDateLabel(scheduleAnalysis?.forecastFinishDate)}</div></div>
                <div><div className="text-sm text-muted-foreground">Total Slack</div><div className="text-2xl font-semibold">{scheduleAnalysis?.totalSlackDays ?? 0}d</div></div>
              </div>
              <div className="space-y-2">
                <div className="text-sm font-medium">Critical Path Work Items</div>
                {scheduleAnalysis?.criticalPathWorkItemIds?.length ? scheduleAnalysis.criticalPathWorkItemIds.map((itemId) => <div key={itemId} className="rounded-lg border p-4 text-sm">{workItemTitles.get(itemId) || itemId}</div>) : <div className="text-sm text-muted-foreground">No critical path items identified yet.</div>}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Baselines</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {project.hasLockedBaseline ? (
                <div className="rounded-lg border bg-muted/20 p-4">
                  <div className="font-medium">{project.activeBaselineName || 'Locked baseline'}</div>
                  <div className="text-sm text-muted-foreground">Active baseline captured {project.activeBaselineCreatedOn ? format(new Date(project.activeBaselineCreatedOn), 'MMM dd, yyyy HH:mm') : 'N/A'}</div>
                </div>
              ) : null}
              <div className="grid gap-4 md:grid-cols-[1fr_1fr_auto]">
                <div className="grid gap-2"><Label>Name</Label><Input value={baseline.name} onChange={(e) => setBaseline((p) => ({ ...p, name: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Notes</Label><Input value={baseline.notes || ''} onChange={(e) => setBaseline((p) => ({ ...p, notes: e.target.value }))} /></div>
                <div className="flex items-end"><Button disabled={!baseline.name.trim()} onClick={() => act(() => projectService.createBaseline(project.id, baseline).then(() => Promise.resolve()), 'Baseline created', () => setBaseline(baselineInit))}><Plus className="mr-2 h-4 w-4" />Create Baseline</Button></div>
              </div>
              <div className="space-y-3">
                {project.baselines.length === 0 ? <div className="text-sm text-muted-foreground">No baselines are available.</div> : null}
                {project.baselines.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.name}</div><div className="text-sm text-muted-foreground">{format(new Date(x.createdOn), 'MMM dd, yyyy HH:mm')} | {x.isLocked ? 'Locked' : 'Editable'}{x.snapshotFinishDate ? ` | finish ${format(new Date(x.snapshotFinishDate), 'MMM dd, yyyy')}` : ''}</div></div><Button variant="outline" size="sm" onClick={async () => { try { setBaselineComparison(await projectService.compareBaseline(x.id)); } catch (error: any) { toast.error(error.message || 'Failed to compare baseline'); } }}>Compare</Button></div>)}
              </div>
              {baselineComparison ? <div className="rounded-lg border p-4"><div className="font-medium">{baselineComparison.baselineName}</div><div className="mt-3 grid gap-3 md:grid-cols-3"><div><div className="text-sm text-muted-foreground">Baseline Progress</div><div className="font-medium">{baselineComparison.baselineProgressPercent}%</div></div><div><div className="text-sm text-muted-foreground">Current Progress</div><div className="font-medium">{baselineComparison.currentProgressPercent}%</div></div><div><div className="text-sm text-muted-foreground">Schedule Variance</div><div className="font-medium">{baselineComparison.scheduleVarianceDays} day(s)</div></div><div><div className="text-sm text-muted-foreground">Budget Variance</div><div className="font-medium">{baselineComparison.budgetVariance.toLocaleString()}</div></div><div><div className="text-sm text-muted-foreground">Changed Work Items</div><div className="font-medium">{baselineComparison.changedWorkItemCount}</div></div><div><div className="text-sm text-muted-foreground">Changed Milestones</div><div className="font-medium">{baselineComparison.changedMilestoneCount}</div></div></div><div className="mt-4 space-y-3">{baselineComparison.workItemChanges.slice(0, 5).map((x) => <div key={x.workItemId} className="rounded-lg border p-4 text-sm"><div className="font-medium">{x.workItemTitle}</div><div className="text-muted-foreground">{x.baselinePlannedStartDate ? format(new Date(x.baselinePlannedStartDate), 'MMM dd, yyyy') : 'N/A'} to {x.baselinePlannedEndDate ? format(new Date(x.baselinePlannedEndDate), 'MMM dd, yyyy') : 'N/A'} {'->'} {x.currentPlannedStartDate ? format(new Date(x.currentPlannedStartDate), 'MMM dd, yyyy') : 'N/A'} to {x.currentPlannedEndDate ? format(new Date(x.currentPlannedEndDate), 'MMM dd, yyyy') : 'N/A'} | variance {x.scheduleVarianceDays} day(s)</div></div>)}</div></div> : null}
            </CardContent>
          </Card>
          <div className="grid gap-6 xl:grid-cols-2">
            <Card>
              <CardHeader><CardTitle>Financial Control</CardTitle></CardHeader>
              <CardContent className="space-y-4">
                {financialSummary ? (
                  <>
                    <div className="grid gap-4 md:grid-cols-2">
                      <div><div className="text-sm text-muted-foreground">Pending Cost</div><div className="text-xl font-semibold">{financialSummary.pendingCost.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Forecast Cost</div><div className="text-xl font-semibold">{financialSummary.forecastCost.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Procurement Requested</div><div className="text-xl font-semibold">{financialSummary.procurementRequestedAmount.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Procurement Committed</div><div className="text-xl font-semibold">{financialSummary.procurementCommittedAmount.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Procurement Received</div><div className="text-xl font-semibold">{financialSummary.procurementReceivedAmount.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Pending Inspection</div><div className="text-xl font-semibold">{financialSummary.procurementPendingInspectionAmount.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Planned Value</div><div className="text-xl font-semibold">{financialSummary.plannedValue.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Earned Value</div><div className="text-xl font-semibold">{financialSummary.earnedValue.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Schedule Variance</div><div className="text-xl font-semibold">{financialSummary.scheduleVariance.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Cost Variance</div><div className="text-xl font-semibold">{financialSummary.costVariance.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Total Exposure</div><div className="text-xl font-semibold">{financialSummary.totalExposureAmount.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Remaining Budget</div><div className="text-xl font-semibold">{financialSummary.remainingBudget.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Scheduled Billing</div><div className="text-xl font-semibold">{financialSummary.scheduledBillingAmount.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Invoice Requested</div><div className="text-xl font-semibold">{financialSummary.invoiceRequestedAmount.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Recognized Revenue</div><div className="text-xl font-semibold">{financialSummary.recognizedRevenue.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Gross Margin</div><div className="text-xl font-semibold">{financialSummary.grossMargin.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Profitability</div><div className="text-xl font-semibold">{financialSummary.profitabilityPercent.toFixed(2)}%</div></div>
                      <div><div className="text-sm text-muted-foreground">TCPI</div><div className="text-xl font-semibold">{financialSummary.toCompletePerformanceIndex?.toFixed(2) ?? 'N/A'}</div></div>
                    </div>
                  </>
                ) : (
                  <div className="text-sm text-muted-foreground">Financial details are not available for this user.</div>
                )}
              </CardContent>
            </Card>
            <Card>
              <CardHeader><CardTitle>Integration Dependencies</CardTitle></CardHeader>
              <CardContent className="space-y-4">
                {integrationSummary ? (
                  <>
                    <div className="grid gap-4 md:grid-cols-2">
                      <div><div className="text-sm text-muted-foreground">Resource allocations</div><div className="text-xl font-semibold">{integrationSummary.resourceAllocationCount}</div></div>
                      <div><div className="text-sm text-muted-foreground">Invoice requests</div><div className="text-xl font-semibold">{integrationSummary.invoiceRequestCount}</div></div>
                      <div><div className="text-sm text-muted-foreground">Purchase orders</div><div className="text-xl font-semibold">{integrationSummary.purchaseOrderCount}</div><div className="text-sm text-muted-foreground">Open {integrationSummary.openPurchaseOrderCount} | Amount {integrationSummary.purchaseOrderAmount.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Purchase receipts</div><div className="text-xl font-semibold">{integrationSummary.purchaseReceiptCount}</div><div className="text-sm text-muted-foreground">Pending inspection {integrationSummary.pendingPurchaseReceiptInspectionCount}</div></div>
                      <div><div className="text-sm text-muted-foreground">Revenue snapshots</div><div className="text-xl font-semibold">{integrationSummary.revenueRecognitionCount}</div></div>
                      <div><div className="text-sm text-muted-foreground">Net issued inventory</div><div className="text-xl font-semibold">{integrationSummary.issuedInventoryRequisitionCount}</div><div className="text-sm text-muted-foreground">Net {integrationSummary.netIssuedInventoryValue.toLocaleString()} | Returned {integrationSummary.returnedInventoryValue.toLocaleString()}</div></div>
                      <div><div className="text-sm text-muted-foreground">Portfolio / program</div><div className="text-xl font-semibold">{integrationSummary.hasPortfolio || integrationSummary.hasProgram ? 'Linked' : 'Standalone'}</div></div>
                    </div>
                    {integrationSummary.warnings.length === 0 ? <div className="text-sm text-muted-foreground">No integration warnings are currently flagged.</div> : null}
                  </>
                ) : (
                  <div className="text-sm text-muted-foreground">Integration details are not available for this user.</div>
                )}
              </CardContent>
            </Card>
          </div>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between"><CardTitle>Revenue Recognition</CardTitle><Button onClick={() => act(() => projectService.generateRevenueRecognition(project.id).then(() => Promise.resolve()), 'Revenue recognition generated')}>Generate</Button></CardHeader>
            <CardContent className="space-y-3">
              {project.revenueRecognitions.length === 0 ? <div className="text-sm text-muted-foreground">No revenue recognition records are available.</div> : null}
              {project.revenueRecognitions.map((x) => <div key={x.id} className="rounded-lg border p-4"><div className="flex items-center justify-between"><div className="font-medium">{x.recognitionPeriod}</div><Badge variant="outline">{x.status}</Badge></div><div className="mt-2 grid gap-3 text-sm md:grid-cols-4"><div>Revenue: {x.recognizedRevenue.toLocaleString()}</div><div>Cost: {x.recognizedCost.toLocaleString()}</div><div>Margin: {x.grossMargin.toLocaleString()}</div><div>Cash: {x.cashCollected.toLocaleString()}</div></div>{x.notes ? <div className="mt-2 text-sm text-muted-foreground">{x.notes}</div> : null}</div>)}
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between"><CardTitle>AI Insights</CardTitle><Button variant="outline" size="sm" onClick={() => act(() => loadAdvanced(project.id), 'AI insights refreshed')}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></CardHeader>
            <CardContent className="space-y-3">
              {aiInsights.map((item, index) => <div key={`${item.category}-${index}`} className="rounded-lg border p-4"><div className="flex items-center gap-2"><Badge variant="outline">{item.category}</Badge><Badge>{item.severity}</Badge></div><div className="mt-2 font-medium">{item.title}</div><div className="mt-1 text-sm text-muted-foreground">{item.recommendation}</div></div>)}
            </CardContent>
          </Card>
        </TabsContent>
        <TabsContent value="materials" className="space-y-6">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle>Project Materials</CardTitle>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" onClick={() => act(() => loadMaterials(project.id), 'Project materials refreshed')}>
                  <RefreshCw className="mr-2 h-4 w-4" />
                  Refresh
                </Button>
                <Button size="sm" onClick={() => openMaterialDialog('create')}>
                  <Plus className="mr-2 h-4 w-4" />
                  New Material Requisition
                </Button>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="rounded-lg border p-4">
                  <div className="text-sm text-muted-foreground">Total Requisitions</div>
                  <div className="text-2xl font-semibold">{materialRequisitions.length}</div>
                  <div className="text-sm text-muted-foreground">Project-scoped material demand</div>
                </div>
                <div className="rounded-lg border p-4">
                  <div className="text-sm text-muted-foreground">Pending Approval</div>
                  <div className="text-2xl font-semibold">{materialSnapshot.pendingApproval}</div>
                  <div className="text-sm text-muted-foreground">Waiting on workflow approval</div>
                </div>
                <div className="rounded-lg border p-4">
                  <div className="text-sm text-muted-foreground">Pending Issue</div>
                  <div className="text-2xl font-semibold">{materialSnapshot.pendingIssue}</div>
                  <div className="text-sm text-muted-foreground">Approved but not fully issued</div>
                </div>
                <div className="rounded-lg border p-4">
                  <div className="text-sm text-muted-foreground">Requested Value</div>
                  <div className="text-2xl font-semibold">{materialSnapshot.totalValue.toLocaleString()}</div>
                  <div className="text-sm text-muted-foreground">Completed {materialSnapshot.completed}</div>
                </div>
              </div>
              <div className="space-y-3">
                {orderedMaterialRequisitions.length === 0 ? (
                  <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                    No project-linked inventory requisitions have been raised yet.
                  </div>
                ) : null}
                {orderedMaterialRequisitions.map((requisition) => {
                  const statusValue = normalizeRequisitionStatus(requisition.status);
                  const canIssue = [3, 4, 5].includes(statusValue);
                  const canReturn = [5, 6, 7].includes(statusValue);
                  const canComplete = statusValue === 6;
                  const canEdit = statusValue === 1;
                  const canSubmit = statusValue === 1;

                  return (
                    <div key={requisition.id} className="rounded-lg border p-4">
                      <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                        <div className="space-y-2">
                          <div className="flex flex-wrap items-center gap-2">
                            <div className="font-medium">{requisition.requisitionNumber}</div>
                            <Badge variant="outline">
                              {RequisitionStatusMap[statusValue] || requisition.status}
                            </Badge>
                            <Badge variant="secondary">
                              {requisition.priority}
                            </Badge>
                            {requisition.currentWorkflowStepName ? (
                              <Badge variant="outline">Step: {requisition.currentWorkflowStepName}</Badge>
                            ) : null}
                          </div>
                          <div className="text-sm text-muted-foreground">
                            Warehouse {requisition.warehouseName} | Request {formatDateLabel(requisition.requestDate)} | Required {formatDateLabel(requisition.requiredDate)}
                          </div>
                          <div className="text-sm text-muted-foreground">
                            Items {requisition.totalItems} | Quantity {requisition.totalQuantity} | Value {requisition.totalValue.toLocaleString()}
                          </div>
                          {requisition.purpose ? <div className="text-sm">{requisition.purpose}</div> : null}
                        </div>
                        <div className="flex flex-wrap gap-2">
                          <Button variant="outline" size="sm" onClick={() => openMaterialDialog('view', requisition.id)}>View</Button>
                          {canEdit ? <Button variant="outline" size="sm" onClick={() => openMaterialDialog('edit', requisition.id)}>Edit</Button> : null}
                          {canSubmit ? (
                            <Button variant="outline" size="sm" onClick={() => act(() => inventoryRequisitionService.submit(requisition.id), 'Material requisition submitted')}>
                              Submit
                            </Button>
                          ) : null}
                          {canIssue ? (
                            <Button size="sm" onClick={() => openIssueDialog(requisition.id)}>Issue</Button>
                          ) : null}
                          {canReturn ? (
                            <Button variant="outline" size="sm" onClick={() => openReturnDialog(requisition.id)}>Return</Button>
                          ) : null}
                          {canComplete ? (
                            <Button variant="outline" size="sm" onClick={() => act(() => inventoryRequisitionService.complete(requisition.id), 'Material requisition completed')}>
                              Complete
                            </Button>
                          ) : null}
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
        <TabsContent value="governance" className="space-y-6">
          <Card>
            <CardHeader><CardTitle>Governance Snapshot</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {governanceSummary ? (
                <>
                  <div className="grid gap-4 md:grid-cols-4">
                    <div><div className="text-sm text-muted-foreground">Open Risks</div><div className="text-2xl font-semibold">{governanceSummary.openRiskCount}</div></div>
                    <div><div className="text-sm text-muted-foreground">Open Issues</div><div className="text-2xl font-semibold">{governanceSummary.openIssueCount}</div></div>
                    <div><div className="text-sm text-muted-foreground">Open Changes</div><div className="text-2xl font-semibold">{governanceSummary.openChangeRequestCount}</div></div>
                    <div><div className="text-sm text-muted-foreground">Open Actions</div><div className="text-2xl font-semibold">{governanceSummary.openActionItemCount}</div></div>
                  </div>
                  <div className="grid gap-4 md:grid-cols-3">
                    <div className="rounded-lg border p-4 text-sm">Deliverables pending approval: {governanceSummary.pendingDeliverableApprovalCount}</div>
                    <div className="rounded-lg border p-4 text-sm">Timesheets pending approval: {governanceSummary.pendingTimesheetApprovalCount}</div>
                    <div className="rounded-lg border p-4 text-sm">Expenses pending approval: {governanceSummary.pendingExpenseApprovalCount}</div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={governanceSummary.hasLockedBaseline ? 'secondary' : 'outline'}>{governanceSummary.hasLockedBaseline ? 'Baseline locked' : 'No locked baseline'}</Badge>
                    <Badge variant={governanceSummary.hasClosureDraft ? 'secondary' : 'outline'}>{governanceSummary.hasClosureDraft ? 'Closure draft exists' : 'No closure draft'}</Badge>
                    <Badge variant={governanceSummary.hasApprovedClosure ? 'secondary' : 'outline'}>{governanceSummary.hasApprovedClosure ? 'Closure approved' : 'Closure not approved'}</Badge>
                  </div>
                  <div className="space-y-2">
                    {governanceSummary.violations.length === 0 ? <div className="text-sm text-muted-foreground">No governance alerts are currently flagged.</div> : null}
                    {governanceSummary.violations.map((violation, index) => (
                      <div key={`${violation.area}-${index}`} className="rounded-lg border p-4 text-sm">
                        <div className="font-medium">{violation.area} | {violation.severity}</div>
                        <div className="text-muted-foreground">{violation.message}</div>
                      </div>
                    ))}
                  </div>
                </>
              ) : (
                <div className="text-sm text-muted-foreground">Governance details are not available for this user.</div>
              )}
            </CardContent>
          </Card>
          <Card><CardHeader><CardTitle>Risks</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-4 md:grid-cols-4"><div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={risk.title} onChange={(e) => setRisk((p) => ({ ...p, title: e.target.value }))} /></div><div className="grid gap-2"><Label>Status</Label><Select value={risk.status || riskStatusOptions[0]} onValueChange={(value) => setRisk((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{riskStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Category</Label><Select value={risk.category || riskCategoryOptions[0]} onValueChange={(value) => setRisk((p) => ({ ...p, category: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{riskCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Owner</Label><Select value={risk.ownerId || 'none'} onValueChange={(value) => setRisk((p) => ({ ...p, ownerId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger><SelectContent><SelectItem value="none">No owner</SelectItem>{activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Probability</Label><Input type="number" value={risk.probability ?? 1} onChange={(e) => setRisk((p) => ({ ...p, probability: Number(e.target.value || '1') }))} /></div><div className="grid gap-2"><Label>Impact</Label><Input type="number" value={risk.impact ?? 1} onChange={(e) => setRisk((p) => ({ ...p, impact: Number(e.target.value || '1') }))} /></div><div className="grid gap-2 md:col-span-2"><Label>Response Strategy</Label><Select value={risk.responseStrategy || riskResponseStrategyOptions[0]} onValueChange={(value) => setRisk((p) => ({ ...p, responseStrategy: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{riskResponseStrategyOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div></div><div className="flex justify-end"><Button onClick={() => act(() => projectService.addRisk(project.id, risk).then(() => Promise.resolve()), 'Risk added', () => setRisk(riskInit))}><Plus className="mr-2 h-4 w-4" />Add Risk</Button></div>{project.risks.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.title}</div><div className="text-sm text-muted-foreground">{x.status} | {x.category || 'General'} | Exposure {x.exposure}{x.ownerId ? ` | ${userLookup.get(x.ownerId) || x.ownerId}` : ''}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteRisk(x.id), 'Risk deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}</CardContent></Card>
          <Card><CardHeader><CardTitle>Issues</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-4 md:grid-cols-4"><div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={issue.title} onChange={(e) => setIssue((p) => ({ ...p, title: e.target.value }))} /></div><div className="grid gap-2"><Label>Status</Label><Select value={issue.status || issueStatusOptions[0]} onValueChange={(value) => setIssue((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{issueStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Severity</Label><Select value={issue.severity || issueSeverityOptions[0]} onValueChange={(value) => setIssue((p) => ({ ...p, severity: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{issueSeverityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Owner</Label><Select value={issue.ownerId || 'none'} onValueChange={(value) => setIssue((p) => ({ ...p, ownerId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger><SelectContent><SelectItem value="none">No owner</SelectItem>{activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}</SelectContent></Select></div></div><div className="flex justify-end"><Button onClick={() => act(() => projectService.addIssue(project.id, issue).then(() => Promise.resolve()), 'Issue added', () => setIssue(issueInit))}><Plus className="mr-2 h-4 w-4" />Add Issue</Button></div>{project.issues.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.title}</div><div className="text-sm text-muted-foreground">{x.status} | {x.severity || 'Unspecified'}{x.ownerId ? ` | ${userLookup.get(x.ownerId) || x.ownerId}` : ''}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteIssue(x.id), 'Issue deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}</CardContent></Card>
          <Card>
            <CardHeader><CardTitle>Quality Checkpoints</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={qualityCheckpoint.title} onChange={(e) => setQualityCheckpoint((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={qualityCheckpoint.status || qualityCheckpointStatusOptions[0]} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{qualityCheckpointStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Due Date</Label><Input type="date" value={qualityCheckpoint.dueDate || ''} onChange={(e) => setQualityCheckpoint((p) => ({ ...p, dueDate: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Work Item</Label><Select value={qualityCheckpoint.workItemId || 'none'} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No work item</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Deliverable</Label><Select value={qualityCheckpoint.deliverableId || 'none'} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, deliverableId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No deliverable</SelectItem>{project.deliverables.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>QA Owner</Label><Select value={qualityCheckpoint.qaOwnerId || 'none'} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, qaOwnerId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger><SelectContent><SelectItem value="none">No owner</SelectItem>{activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>QA Sign-off</Label><Select value={boolValue(qualityCheckpoint.requiresQaSignOff)} onValueChange={(value) => setQualityCheckpoint((p) => ({ ...p, requiresQaSignOff: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Not required</SelectItem><SelectItem value="true">Required</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-4"><Label>Description</Label><Textarea rows={2} value={qualityCheckpoint.description || ''} onChange={(e) => setQualityCheckpoint((p) => ({ ...p, description: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addQualityCheckpoint(project.id, qualityCheckpoint).then(() => Promise.resolve()), 'Quality checkpoint added', () => setQualityCheckpoint(qualityCheckpointInit))}><Plus className="mr-2 h-4 w-4" />Add Checkpoint</Button></div>
              <div className="space-y-3">
                {project.qualityCheckpoints.length === 0 ? <div className="text-sm text-muted-foreground">No quality checkpoints have been defined.</div> : null}
                {project.qualityCheckpoints.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant="outline">{x.status}</Badge></div><div className="text-sm text-muted-foreground">{x.dueDate ? formatDateLabel(x.dueDate) : 'No due date'}{x.qaOwnerId ? ` | ${userLookup.get(x.qaOwnerId) || x.qaOwnerId}` : ''}{x.requiresQaSignOff ? ' | sign-off required' : ''}</div>{x.description ? <div className="mt-2 text-sm text-muted-foreground">{x.description}</div> : null}{x.signedOffAt ? <div className="mt-1 text-sm text-muted-foreground">Signed off {formatDateLabel(x.signedOffAt)}{x.signOffNotes ? ` | ${x.signOffNotes}` : ''}</div> : null}</div><div className="flex gap-2">{x.requiresQaSignOff && !x.signedOffAt ? <Button size="sm" variant="outline" onClick={() => act(() => projectService.signOffQualityCheckpoint(x.id, 'Signed off from workspace'), 'Quality checkpoint signed off')}>Sign Off</Button> : null}<Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteQualityCheckpoint(x.id), 'Quality checkpoint deleted')}><Trash2 className="h-4 w-4" /></Button></div></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Non-Conformances</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={nonConformance.title} onChange={(e) => setNonConformance((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={nonConformance.status || nonConformanceStatusOptions[0]} onValueChange={(value) => setNonConformance((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{nonConformanceStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Severity</Label><Select value={nonConformance.severity || nonConformanceSeverityOptions[0]} onValueChange={(value) => setNonConformance((p) => ({ ...p, severity: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{nonConformanceSeverityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Checkpoint</Label><Select value={nonConformance.qualityCheckpointId || 'none'} onValueChange={(value) => setNonConformance((p) => ({ ...p, qualityCheckpointId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No checkpoint</SelectItem>{project.qualityCheckpoints.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Deliverable</Label><Select value={nonConformance.deliverableId || 'none'} onValueChange={(value) => setNonConformance((p) => ({ ...p, deliverableId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No deliverable</SelectItem>{project.deliverables.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Owner</Label><Select value={nonConformance.ownerId || 'none'} onValueChange={(value) => setNonConformance((p) => ({ ...p, ownerId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger><SelectContent><SelectItem value="none">No owner</SelectItem>{activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Target Resolution</Label><Input type="date" value={nonConformance.targetResolutionDate || ''} onChange={(e) => setNonConformance((p) => ({ ...p, targetResolutionDate: e.target.value || undefined }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Description</Label><Textarea rows={2} value={nonConformance.description || ''} onChange={(e) => setNonConformance((p) => ({ ...p, description: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Corrective Action</Label><Textarea rows={2} value={nonConformance.correctiveAction || ''} onChange={(e) => setNonConformance((p) => ({ ...p, correctiveAction: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Preventive Action</Label><Textarea rows={2} value={nonConformance.preventiveAction || ''} onChange={(e) => setNonConformance((p) => ({ ...p, preventiveAction: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addNonConformance(project.id, nonConformance).then(() => Promise.resolve()), 'Non-conformance added', () => setNonConformance(nonConformanceInit))}><Plus className="mr-2 h-4 w-4" />Add Non-Conformance</Button></div>
              <div className="space-y-3">
                {project.nonConformances.length === 0 ? <div className="text-sm text-muted-foreground">No non-conformances have been logged.</div> : null}
                {project.nonConformances.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant={x.status === 'Resolved' ? 'outline' : x.severity === 'Critical' ? 'destructive' : 'secondary'}>{x.status}</Badge></div><div className="text-sm text-muted-foreground">{x.severity} | reported {formatDateLabel(x.reportedAt)}{x.ownerId ? ` | ${userLookup.get(x.ownerId) || x.ownerId}` : ''}</div>{x.description ? <div className="mt-2 text-sm text-muted-foreground">{x.description}</div> : null}{x.correctiveAction ? <div className="mt-1 text-sm text-muted-foreground">Corrective: {x.correctiveAction}</div> : null}{x.preventiveAction ? <div className="mt-1 text-sm text-muted-foreground">Preventive: {x.preventiveAction}</div> : null}{x.resolutionNotes ? <div className="mt-1 text-sm text-muted-foreground">Resolution: {x.resolutionNotes}</div> : null}</div><div className="flex gap-2">{x.status !== 'Resolved' ? <Button size="sm" variant="outline" onClick={() => act(() => projectService.resolveNonConformance(x.id, 'Resolved from workspace'), 'Non-conformance resolved')}>Resolve</Button> : null}<Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteNonConformance(x.id), 'Non-conformance deleted')}><Trash2 className="h-4 w-4" /></Button></div></div>)}
              </div>
            </CardContent>
          </Card>
          <Card><CardHeader><CardTitle>Change Requests</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-4 md:grid-cols-4"><div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={change.title} onChange={(e) => setChange((p) => ({ ...p, title: e.target.value }))} /></div><div className="grid gap-2"><Label>Type</Label><Select value={change.changeType || changeTypeOptions[0]} onValueChange={(value) => setChange((p) => ({ ...p, changeType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{changeTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Status</Label><Select value={change.status || changeStatusOptions[0]} onValueChange={(value) => setChange((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{changeStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div></div><div className="flex justify-end"><Button onClick={() => act(() => projectService.addChangeRequest(project.id, change).then(() => Promise.resolve()), 'Change request added', () => setChange(changeInit))}><Plus className="mr-2 h-4 w-4" />Add Change</Button></div>{project.changeRequests.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.title}</div><div className="text-sm text-muted-foreground">{x.status} | {x.changeType || 'General'}</div></div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteChangeRequest(x.id), 'Change request deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}</CardContent></Card>
          <Card>
            <CardHeader><CardTitle>Decisions</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={decision.title} onChange={(e) => setDecision((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Decision Date</Label><Input type="date" value={decision.decisionDate || today()} onChange={(e) => setDecision((p) => ({ ...p, decisionDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={decision.status || decisionStatusOptions[0]} onValueChange={(value) => setDecision((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{decisionStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-2"><Label>Rationale</Label><Textarea rows={2} value={decision.rationale || ''} onChange={(e) => setDecision((p) => ({ ...p, rationale: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Alternatives</Label><Textarea rows={2} value={decision.alternativesConsidered || ''} onChange={(e) => setDecision((p) => ({ ...p, alternativesConsidered: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Impact</Label><Textarea rows={2} value={decision.impactSummary || ''} onChange={(e) => setDecision((p) => ({ ...p, impactSummary: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addDecision(project.id, decision).then(() => Promise.resolve()), 'Decision logged', () => setDecision({ ...decisionInit, approverId: currentUserId || undefined }))}><Plus className="mr-2 h-4 w-4" />Add Decision</Button></div>
              <div className="space-y-3">
                {project.decisions.length === 0 ? <div className="text-sm text-muted-foreground">No decisions have been logged yet.</div> : null}
                {project.decisions.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant="outline">{x.status}</Badge></div><div className="text-sm text-muted-foreground">{formatDateLabel(x.decisionDate)}{x.approvedAt ? ` | approved ${formatDateLabel(x.approvedAt)}` : ''}</div>{x.rationale ? <div className="mt-2 text-sm text-muted-foreground">{x.rationale}</div> : null}{x.impactSummary ? <div className="mt-1 text-sm text-muted-foreground">{x.impactSummary}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteDecision(x.id), 'Decision deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Meetings</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={meeting.title} onChange={(e) => setMeeting((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Date</Label><Input type="date" value={meeting.meetingDate || today()} onChange={(e) => setMeeting((p) => ({ ...p, meetingDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Type</Label><Select value={meeting.meetingType || meetingTypeOptions[0]} onValueChange={(value) => setMeeting((p) => ({ ...p, meetingType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{meetingTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-3"><Label>Minutes</Label><Textarea rows={3} value={meeting.minutes || ''} onChange={(e) => setMeeting((p) => ({ ...p, minutes: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Attendees</Label><Input value={meeting.attendeesJson || ''} onChange={(e) => setMeeting((p) => ({ ...p, attendeesJson: e.target.value }))} placeholder="Comma separated or JSON" /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addMeeting(project.id, meeting).then(() => Promise.resolve()), 'Meeting logged', () => setMeeting({ ...meetingInit, facilitatorId: currentUserId || undefined }))}><Plus className="mr-2 h-4 w-4" />Add Meeting</Button></div>
              <div className="space-y-3">
                {project.meetings.length === 0 ? <div className="text-sm text-muted-foreground">No meeting minutes have been captured yet.</div> : null}
                {project.meetings.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant="outline">{x.meetingType}</Badge></div><div className="text-sm text-muted-foreground">{formatDateLabel(x.meetingDate)}</div>{x.minutes ? <div className="mt-2 text-sm text-muted-foreground whitespace-pre-wrap">{x.minutes}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteMeeting(x.id), 'Meeting deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Action Items</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={actionItem.title} onChange={(e) => setActionItem((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Priority</Label><Select value={actionItem.priority || actionItemPriorityOptions[0]} onValueChange={(value) => setActionItem((p) => ({ ...p, priority: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{actionItemPriorityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Due Date</Label><Input type="date" value={actionItem.dueDate || today()} onChange={(e) => setActionItem((p) => ({ ...p, dueDate: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Meeting</Label><Select value={actionItem.meetingMinuteId || 'none'} onValueChange={(value) => setActionItem((p) => ({ ...p, meetingMinuteId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No meeting</SelectItem>{project.meetings.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Work Item</Label><Select value={actionItem.workItemId || 'none'} onValueChange={(value) => setActionItem((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">No work item</SelectItem>{flat.map((item) => <SelectItem key={item.id} value={item.id}>{item.title}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2">
                  <Label>Owner</Label>
                  <Select value={actionItem.ownerId || 'none'} onValueChange={(value) => setActionItem((p) => ({ ...p, ownerId: value === 'none' ? undefined : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No owner</SelectItem>
                      {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2"><Label>Status</Label><Select value={actionItem.status || actionItemStatusOptions[0]} onValueChange={(value) => setActionItem((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{actionItemStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-4"><Label>Description</Label><Textarea rows={2} value={actionItem.description || ''} onChange={(e) => setActionItem((p) => ({ ...p, description: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addActionItem(project.id, actionItem).then(() => Promise.resolve()), 'Action item added', () => setActionItem({ ...actionItemInit, ownerId: currentUserId || undefined }))}><Plus className="mr-2 h-4 w-4" />Add Action</Button></div>
              <div className="space-y-3">
                {project.actionItems.length === 0 ? <div className="text-sm text-muted-foreground">No action items have been captured yet.</div> : null}
                {project.actionItems.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant={x.status === 'Completed' || x.status === 'Closed' ? 'secondary' : 'outline'}>{x.status}</Badge><Badge variant="outline">{x.priority}</Badge></div><div className="text-sm text-muted-foreground">{x.meetingTitle || 'General'}{x.workItemTitle ? ` | ${x.workItemTitle}` : ''}{x.ownerId ? ` | ${userLookup.get(x.ownerId) || x.ownerId}` : ''}{x.dueDate ? ` | due ${formatDateLabel(x.dueDate)}` : ''}</div>{x.description ? <div className="mt-2 text-sm text-muted-foreground">{x.description}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteActionItem(x.id), 'Action item deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Lessons Learned</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2 md:col-span-2"><Label>Title</Label><Input value={lessonLearned.title} onChange={(e) => setLessonLearned((p) => ({ ...p, title: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Category</Label><Select value={lessonLearned.category || lessonCategoryOptions[0]} onValueChange={(value) => setLessonLearned((p) => ({ ...p, category: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{lessonCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Phase</Label><Input value={lessonLearned.appliedPhase || ''} onChange={(e) => setLessonLearned((p) => ({ ...p, appliedPhase: e.target.value }))} /></div>
                <div className="grid gap-2"><Label>Visibility</Label><Select value={lessonLearned.visibility || lessonVisibilityOptions[0]} onValueChange={(value) => setLessonLearned((p) => ({ ...p, visibility: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{lessonVisibilityOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-3"><Label>Description</Label><Textarea rows={2} value={lessonLearned.description || ''} onChange={(e) => setLessonLearned((p) => ({ ...p, description: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-4"><Label>Recommendation</Label><Textarea rows={2} value={lessonLearned.recommendation || ''} onChange={(e) => setLessonLearned((p) => ({ ...p, recommendation: e.target.value }))} /></div>
              </div>
              <div className="flex justify-end"><Button onClick={() => act(() => projectService.addLessonLearned(project.id, lessonLearned).then(() => Promise.resolve()), 'Lesson learned added', () => setLessonLearned(lessonLearnedInit))}><Plus className="mr-2 h-4 w-4" />Add Lesson</Button></div>
              <div className="space-y-3">
                {project.lessonsLearned.length === 0 ? <div className="text-sm text-muted-foreground">No lessons learned have been recorded yet.</div> : null}
                {project.lessonsLearned.map((x) => <div key={x.id} className="flex items-start justify-between rounded-lg border p-4"><div><div className="flex items-center gap-2"><div className="font-medium">{x.title}</div><Badge variant="outline">{x.category}</Badge><Badge variant="outline">{x.visibility}</Badge></div>{x.appliedPhase ? <div className="text-sm text-muted-foreground">Applied phase: {x.appliedPhase}</div> : null}{x.description ? <div className="mt-2 text-sm text-muted-foreground">{x.description}</div> : null}{x.recommendation ? <div className="mt-1 text-sm text-muted-foreground">{x.recommendation}</div> : null}</div><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteLessonLearned(x.id), 'Lesson learned deleted')}><Trash2 className="h-4 w-4" /></Button></div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Closure</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="flex flex-wrap gap-2">
                <Badge variant={closureRecord ? 'secondary' : 'outline'}>{closureRecord ? `Status ${closureRecord.status}` : 'No closure record yet'}</Badge>
                <Badge variant={closureRecord?.submittedAt ? 'secondary' : 'outline'}>{closureRecord?.submittedAt ? `Submitted ${formatDateLabel(closureRecord.submittedAt)}` : 'Not submitted'}</Badge>
                <Badge variant={closureRecord?.approvedAt ? 'secondary' : 'outline'}>{closureRecord?.approvedAt ? `Approved ${formatDateLabel(closureRecord.approvedAt)}` : 'Not approved'}</Badge>
              </div>
              {closureRecord?.rejectionReason ? <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">{closureRecord.rejectionReason}</div> : null}
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2"><Label>Final Budget</Label><Input type="number" value={closure.finalBudget ?? 0} onChange={(e) => setClosure((p) => ({ ...p, finalBudget: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Final Cost</Label><Input type="number" value={closure.finalCost ?? 0} onChange={(e) => setClosure((p) => ({ ...p, finalCost: Number(e.target.value || '0') }))} /></div>
                <div className="grid gap-2"><Label>Deliverables Accepted</Label><Select value={boolValue(closure.deliverablesAccepted)} onValueChange={(value) => setClosure((p) => ({ ...p, deliverablesAccepted: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Tasks Completed</Label><Select value={boolValue(closure.tasksCompletedOrWaived)} onValueChange={(value) => setClosure((p) => ({ ...p, tasksCompletedOrWaived: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Assets Reconciled</Label><Select value={boolValue(closure.assetsReconciled)} onValueChange={(value) => setClosure((p) => ({ ...p, assetsReconciled: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Open Items Disposed</Label><Select value={boolValue(closure.openItemsDisposed)} onValueChange={(value) => setClosure((p) => ({ ...p, openItemsDisposed: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-2"><Label>Checklist</Label><Textarea rows={2} value={closure.closureChecklistJson || ''} onChange={(e) => setClosure((p) => ({ ...p, closureChecklistJson: e.target.value }))} placeholder="Checklist notes or JSON payload" /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Open Items Disposition</Label><Textarea rows={2} value={closure.openItemsDisposition || ''} onChange={(e) => setClosure((p) => ({ ...p, openItemsDisposition: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Asset Reconciliation</Label><Textarea rows={2} value={closure.assetReconciliationNotes || ''} onChange={(e) => setClosure((p) => ({ ...p, assetReconciliationNotes: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Lessons Learned Summary</Label><Textarea rows={2} value={closure.lessonsLearnedSummary || ''} onChange={(e) => setClosure((p) => ({ ...p, lessonsLearnedSummary: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Post-Implementation Review</Label><Textarea rows={2} value={closure.postImplementationReview || ''} onChange={(e) => setClosure((p) => ({ ...p, postImplementationReview: e.target.value }))} /></div>
                <div className="grid gap-2 md:col-span-2"><Label>Override Reason</Label><Textarea rows={2} value={closure.overrideReason || ''} onChange={(e) => setClosure((p) => ({ ...p, overrideReason: e.target.value }))} /></div>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button onClick={() => act(() => projectService.upsertClosure(project.id, closure).then(() => Promise.resolve()), 'Closure draft saved')}><Save className="mr-2 h-4 w-4" />Save Closure Draft</Button>
                {closureRecord ? (
                  <>
                    <WorkflowApprovalActions
                      entityType="ProjectClosure"
                      entityId={closureRecord.id}
                      entityLabel="Project Closure"
                      entityNumber={project.projectCode}
                      status={closureRecord.status}
                      loadWorkflowSummary
                      showStepBadge
                      canSubmit={closureRecord.status === 'Draft' || closureRecord.status === 'Rejected'}
                      canApproveReject={closureRecord.status === 'PendingApproval'}
                      onSubmit={() => projectService.submitClosure(project.id)}
                      onApprove={(comments) => projectService.approveClosure(closureRecord.id, comments)}
                      onReject={(comments) => projectService.rejectClosure(closureRecord.id, comments || 'Rejected', comments)}
                      onAfterAction={async () => load()}
                      onOpenWorkflows={() => router.push('/administration/workflow')}
                    />
                    <Button variant="outline" onClick={() => setShowClosureApprovalHistory((value) => !value)}>
                      {showClosureApprovalHistory ? 'Hide Approval History' : 'View Approval History'}
                    </Button>
                  </>
                ) : null}
              </div>
              {closureRecord && showClosureApprovalHistory ? <WorkflowApprovalHistoryPanel entityType="ProjectClosure" entityId={closureRecord.id} /> : null}
            </CardContent>
          </Card>
        </TabsContent>
        <TabsContent value="access" className="space-y-6">
          <Card>
            <CardHeader><CardTitle>ERP Integrations</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {integrationSummary ? (
                <>
                  <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
                    <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Procurement Requisitions</div><div className="text-2xl font-semibold">{integrationSummary.purchaseRequisitionCount}</div><div className="text-sm text-muted-foreground">Pending {integrationSummary.pendingPurchaseRequisitionCount} | Amount {integrationSummary.purchaseRequisitionAmount.toLocaleString()}</div></div>
                    <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Purchase Orders</div><div className="text-2xl font-semibold">{integrationSummary.purchaseOrderCount}</div><div className="text-sm text-muted-foreground">Open {integrationSummary.openPurchaseOrderCount} | Amount {integrationSummary.purchaseOrderAmount.toLocaleString()}</div></div>
                    <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Purchase Receipts</div><div className="text-2xl font-semibold">{integrationSummary.purchaseReceiptCount}</div><div className="text-sm text-muted-foreground">Pending inspection {integrationSummary.pendingPurchaseReceiptInspectionCount}</div></div>
                    <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Inventory Requisitions</div><div className="text-2xl font-semibold">{integrationSummary.inventoryRequisitionCount}</div><div className="text-sm text-muted-foreground">Pending {integrationSummary.pendingInventoryRequisitionCount} | Value {integrationSummary.inventoryRequisitionValue.toLocaleString()}</div></div>
                    <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Issued Inventory</div><div className="text-2xl font-semibold">{integrationSummary.issuedInventoryRequisitionCount}</div><div className="text-sm text-muted-foreground">Net {integrationSummary.netIssuedInventoryValue.toLocaleString()} | Returned {integrationSummary.returnedInventoryValue.toLocaleString()}</div></div>
                    <div className="rounded-lg border p-4"><div className="text-sm text-muted-foreground">Commercial Tracking</div><div className="text-2xl font-semibold">{integrationSummary.invoiceRequestCount + integrationSummary.revenueRecognitionCount}</div><div className="text-sm text-muted-foreground">Invoices {integrationSummary.invoiceRequestCount} | Revenue entries {integrationSummary.revenueRecognitionCount}</div></div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={integrationSummary.hasBusinessPartner ? 'secondary' : 'outline'}>{integrationSummary.hasBusinessPartner ? 'Business partner linked' : 'No business partner'}</Badge>
                    <Badge variant={integrationSummary.hasContract ? 'secondary' : 'outline'}>{integrationSummary.hasContract ? 'Contract linked' : 'No contract'}</Badge>
                    <Badge variant={integrationSummary.hasPortfolio ? 'secondary' : 'outline'}>{integrationSummary.hasPortfolio ? 'Portfolio linked' : 'No portfolio'}</Badge>
                    <Badge variant={integrationSummary.hasProgram ? 'secondary' : 'outline'}>{integrationSummary.hasProgram ? 'Program linked' : 'No program'}</Badge>
                  </div>
                  <div className="space-y-2">
                    {integrationSummary.warnings.length === 0 ? <div className="text-sm text-muted-foreground">No integration warnings are currently flagged.</div> : null}
                    {integrationSummary.warnings.map((warning, index) => <div key={`${warning}-${index}`} className="rounded-lg border p-4 text-sm text-muted-foreground">{warning}</div>)}
                  </div>
                  <div className="space-y-3">
                    {integrationSummary.links.length === 0 ? <div className="text-sm text-muted-foreground">No linked ERP records have been discovered yet.</div> : null}
                    {integrationSummary.links.map((link, index) => (
                      <div key={`${link.linkType}-${link.reference}-${index}`} className="flex items-center justify-between rounded-lg border p-4">
                        <div>
                          <div className="font-medium">{formatCatalogLabel(link.linkType)}</div>
                          <div className="text-sm text-muted-foreground">{link.reference}</div>
                        </div>
                        <Badge variant="outline">{link.status}</Badge>
                      </div>
                    ))}
                  </div>
                </>
              ) : (
                <div className="text-sm text-muted-foreground">Integration summary is only available to users with project access.</div>
              )}
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Asset Links</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2"><Label>Maintenance Asset Id</Label><Input value={assetLink.maintenanceAssetId || ''} onChange={(e) => setAssetLink((p) => ({ ...p, maintenanceAssetId: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Company Asset Id</Label><Input value={assetLink.companyAssetId || ''} onChange={(e) => setAssetLink((p) => ({ ...p, companyAssetId: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Job Card Id</Label><Input value={assetLink.jobCardId || ''} onChange={(e) => setAssetLink((p) => ({ ...p, jobCardId: e.target.value || undefined }))} /></div>
                <div className="grid gap-2"><Label>Link Type</Label><Select value={assetLink.linkType || assetLinkTypeOptions[0]} onValueChange={(value) => setAssetLink((p) => ({ ...p, linkType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{assetLinkTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Status</Label><Select value={assetLink.status || assetLinkStatusOptions[0]} onValueChange={(value) => setAssetLink((p) => ({ ...p, status: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{assetLinkStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Input value={assetLink.notes || ''} onChange={(e) => setAssetLink((p) => ({ ...p, notes: e.target.value }))} /></div>
                <div className="flex items-end"><Button onClick={() => act(() => projectService.addAssetLink(project.id, assetLink).then(() => Promise.resolve()), 'Asset link added', () => setAssetLink(assetLinkInit))}><Plus className="mr-2 h-4 w-4" />Link Asset</Button></div>
              </div>
              <div className="space-y-3">
                {project.assetLinks.length === 0 ? <div className="text-sm text-muted-foreground">No maintenance or asset links have been added yet.</div> : null}
                {project.assetLinks.map((x) => <div key={x.id} className="rounded-lg border p-4"><div className="font-medium">{x.assetName || x.jobCardNumber || x.linkType}</div><div className="text-sm text-muted-foreground">{x.status} | maintenance asset {x.maintenanceAssetId || 'N/A'} | company asset {x.companyAssetId || 'N/A'} | job card {x.jobCardId || 'N/A'}</div>{x.notes ? <div className="mt-2 text-sm text-muted-foreground">{x.notes}</div> : null}</div>)}
              </div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>External Access Policies</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-4">
                <div className="grid gap-2">
                  <Label>Business Partner</Label>
                  <Select value={externalPolicy.businessPartnerId || 'none'} onValueChange={(value) => setExternalPolicy((p) => ({ ...p, businessPartnerId: value === 'none' ? '' : value }))}>
                    <SelectTrigger><SelectValue placeholder="Select business partner" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Select business partner</SelectItem>
                      {businessPartners.map((x) => <SelectItem key={x.id} value={x.id}>{x.partnerName}</SelectItem>)}
                    </SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Artifact Type</Label>
                  <Select value={externalPolicy.artifactType || 'Project'} onValueChange={(value) => setExternalPolicy((p) => ({ ...p, artifactType: value, artifactId: value === 'Project' ? undefined : p.artifactId }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>{['Project', 'Document', 'Deliverable', 'WorkItem'].map((x) => <SelectItem key={x} value={x}>{x}</SelectItem>)}</SelectContent>
                  </Select>
                </div>
                <div className="grid gap-2">
                  <Label>Artifact</Label>
                  {externalPolicy.artifactType === 'Project' ? (
                    <Input value="Project-wide access" disabled />
                  ) : (
                    <Select value={externalPolicy.artifactId || 'none'} onValueChange={(value) => setExternalPolicy((p) => ({ ...p, artifactId: value === 'none' ? undefined : value }))}>
                      <SelectTrigger><SelectValue placeholder="Select artifact" /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">Select artifact</SelectItem>
                        {externalArtifactOptions.map((x) => <SelectItem key={x.id} value={x.id}>{x.label}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  )}
                </div>
                <div className="grid gap-2"><Label>Access Level</Label><Select value={externalPolicy.accessLevel || 'Read'} onValueChange={(value) => setExternalPolicy((p) => ({ ...p, accessLevel: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{['Read', 'Collaborate', 'Approve'].map((x) => <SelectItem key={x} value={x}>{x}</SelectItem>)}</SelectContent></Select></div>
                <div className="grid gap-2"><Label>Can Comment</Label><Select value={boolValue(externalPolicy.canComment)} onValueChange={(value) => setExternalPolicy((p) => ({ ...p, canComment: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Can Upload</Label><Select value={boolValue(externalPolicy.canUpload)} onValueChange={(value) => setExternalPolicy((p) => ({ ...p, canUpload: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2"><Label>Can Approve</Label><Select value={boolValue(externalPolicy.canApprove)} onValueChange={(value) => setExternalPolicy((p) => ({ ...p, canApprove: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">No</SelectItem><SelectItem value="true">Yes</SelectItem></SelectContent></Select></div>
                <div className="grid gap-2 md:col-span-2"><Label>Notes</Label><Input value={externalPolicy.notes || ''} onChange={(e) => setExternalPolicy((p) => ({ ...p, notes: e.target.value }))} /></div>
                <div className="flex items-end"><Button disabled={!externalPolicy.businessPartnerId || (externalPolicy.artifactType !== 'Project' && !externalPolicy.artifactId)} onClick={() => act(() => projectService.upsertExternalAccessPolicy(project.id, externalPolicy).then(() => Promise.resolve()), 'External access policy saved', () => setExternalPolicy(externalPolicyInit))}><Plus className="mr-2 h-4 w-4" />Save Policy</Button></div>
              </div>
              <div className="space-y-3">
                {project.externalAccessPolicies.length === 0 ? <div className="text-sm text-muted-foreground">No external access policies are configured yet.</div> : null}
                {project.externalAccessPolicies.map((x) => (
                  <div key={x.id} className="rounded-lg border p-4">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="font-medium">{x.businessPartnerName || x.businessPartnerId}</div>
                        <div className="text-sm text-muted-foreground">{x.artifactType} | {x.artifactLabel || 'Project-wide'} | {x.accessLevel} | comment {x.canComment ? 'yes' : 'no'} | upload {x.canUpload ? 'yes' : 'no'} | approve {x.canApprove ? 'yes' : 'no'}</div>
                        {x.notes ? <div className="mt-2 text-sm text-muted-foreground">{x.notes}</div> : null}
                      </div>
                      <Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteExternalAccessPolicy(x.id), 'External access policy deleted')}><Trash2 className="h-4 w-4" /></Button>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
        <TabsContent value="documents" className="space-y-6">
          <Card><CardHeader><CardTitle>Documents</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-4 md:grid-cols-3"><div className="grid gap-2"><Label>Name</Label><Input value={doc.documentName} onChange={(e) => setDoc((p) => ({ ...p, documentName: e.target.value }))} /></div><div className="grid gap-2"><Label>Category</Label><Select value={doc.category || documentCategoryOptions[0]} onValueChange={(value) => setDoc((p) => ({ ...p, category: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{documentCategoryOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div><div className="grid gap-2"><Label>Type</Label><Select value={doc.documentType || documentTypeOptions[0]} onValueChange={(value) => setDoc((p) => ({ ...p, documentType: value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent>{documentTypeOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent></Select></div></div><div className="grid gap-4 md:grid-cols-3"><div className="grid gap-2"><Label>Upload File</Label><Input type="file" onChange={(e) => setDocFile(e.target.files?.[0] || null)} /></div><div className="grid gap-2"><Label>Existing File Path</Label><Input value={doc.filePath} onChange={(e) => setDoc((p) => ({ ...p, filePath: e.target.value }))} /></div><div className="grid gap-2"><Label>Portal Visibility</Label><Select value={boolValue(doc.isExternalVisible)} onValueChange={(value) => setDoc((p) => ({ ...p, isExternalVisible: value === 'true' }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="false">Internal only</SelectItem><SelectItem value="true">Visible externally</SelectItem></SelectContent></Select></div></div><div className="flex justify-end"><Button onClick={() => act(async () => { if (docFile) { await projectService.uploadProjectDocument(project.id, docFile, { documentName: doc.documentName || docFile.name, category: doc.category, documentType: doc.documentType, versionLabel: doc.versionLabel, status: doc.status, isExternalVisible: doc.isExternalVisible }); } else { if (!doc.filePath) throw new Error('Provide a file path or upload a file'); await projectService.attachDocument(project.id, doc); } }, 'Document attached', () => { setDoc(docInit); setDocFile(null); })}><Plus className="mr-2 h-4 w-4" />Attach</Button></div>{project.documents.map((x) => <div key={x.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{x.documentName}</div><div className="text-sm text-muted-foreground">{x.category} | {x.documentType} | {x.isExternalVisible ? 'External' : 'Internal'}</div></div><div className="flex gap-2"><a className="text-sm underline" href={x.publicUrl || x.filePath} target="_blank" rel="noreferrer">Open</a><Button variant="ghost" size="sm" onClick={() => act(() => projectService.deleteDocument(x.id), 'Document deleted')}><Trash2 className="h-4 w-4" /></Button></div></div>)}</CardContent></Card>
          <Card><CardHeader><CardTitle>Comments</CardTitle></CardHeader><CardContent className="space-y-4"><div className="grid gap-4 md:grid-cols-[1fr_220px]"><div className="grid gap-2"><Label>Comment</Label><Textarea rows={3} value={comment.body} onChange={(e) => setComment((p) => ({ ...p, body: e.target.value }))} /></div><div className="grid gap-2"><Label>Work Item</Label><Select value={comment.workItemId || 'none'} onValueChange={(value) => setComment((p) => ({ ...p, workItemId: value === 'none' ? undefined : value }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="none">General</SelectItem>{flat.map((x) => <SelectItem key={x.id} value={x.id}>{x.title}</SelectItem>)}</SelectContent></Select></div></div><div className="flex justify-end"><Button onClick={() => act(() => projectService.addComment(project.id, comment).then(() => Promise.resolve()), 'Comment added', () => setComment(commentInit))}><Plus className="mr-2 h-4 w-4" />Post</Button></div>{project.comments.map((x) => <div key={x.id} className="rounded-lg border p-4"><div className="flex items-center justify-between"><div className="font-medium">{x.createdBy || 'System'}</div><div className="text-xs text-muted-foreground">{format(new Date(x.createdAt), 'MMM dd, yyyy HH:mm')}</div></div><div className="mt-2 text-sm">{x.body}</div></div>)}</CardContent></Card>
        </TabsContent>
        <TabsContent value="history" className="space-y-6">
          <Card><CardHeader><CardTitle>Initiation Versions</CardTitle></CardHeader><CardContent className="space-y-3">{project.initiationVersions.map((x) => <div key={x.id} className="rounded-lg border p-4"><div className="flex items-center justify-between"><div className="font-medium">Version {x.versionNumber} - {x.changeType}</div><div className="text-xs text-muted-foreground">{format(new Date(x.createdAt), 'MMM dd, yyyy HH:mm')}</div></div><div className="text-sm text-muted-foreground">{x.notes || 'No notes provided'}</div></div>)}</CardContent></Card>
          <Card><CardHeader><CardTitle>Status Timeline</CardTitle></CardHeader><CardContent className="grid gap-4 md:grid-cols-2"><div><div className="text-sm text-muted-foreground">Created</div><div className="font-medium">{format(new Date(project.createdAt), 'MMM dd, yyyy HH:mm')}</div></div><div><div className="text-sm text-muted-foreground">Submitted</div><div className="font-medium">{project.submittedAt ? format(new Date(project.submittedAt), 'MMM dd, yyyy HH:mm') : 'Not submitted'}</div></div><div><div className="text-sm text-muted-foreground">Approved</div><div className="font-medium">{project.approvedAt ? format(new Date(project.approvedAt), 'MMM dd, yyyy HH:mm') : 'Not approved'}</div></div><div><div className="text-sm text-muted-foreground">Status Remarks</div><div className="font-medium">{project.statusRemarks || 'None'}</div></div></CardContent></Card>
        </TabsContent>
      </Tabs>
      <Dialog open={ganttDialogOpen} onOpenChange={setGanttDialogOpen}>
        <DialogContent className="flex h-[92vh] max-h-[92vh] w-[96vw] max-w-[96vw] flex-col overflow-hidden p-0">
          <div className="flex min-h-0 flex-1 flex-col">
            <DialogHeader className="border-b px-6 py-4">
              <div className="flex flex-wrap items-start justify-between gap-4 pr-10">
                <div className="space-y-2">
                  <DialogTitle>Project Gantt Planner</DialogTitle>
                  <DialogDescription>
                    Full project schedule view with WBS grid, timeline bars, and export actions.
                  </DialogDescription>
                      <div className="flex flex-wrap gap-2 text-sm text-muted-foreground">
                        {timelineBounds ? <Badge variant="outline">{formatDateLabel(new Date(timelineBounds.min).toISOString())} to {formatDateLabel(new Date(timelineBounds.max).toISOString())}</Badge> : null}
                        {ganttSummary ? (
                          <>
                            <Badge variant="outline">{ganttSummary.scheduledItems} scheduled items</Badge>
                          <Badge variant="outline">{ganttSummary.spanDays} day span</Badge>
                          <Badge variant="outline">{ganttSummary.phases} phases</Badge>
                          <Badge variant={ganttSummary.overdueItems > 0 ? 'destructive' : 'secondary'}>{ganttSummary.overdueItems} overdue</Badge>
                        </>
                        ) : null}
                      <Badge variant="outline">{ganttDependencyLines.length} dependencies visualized</Badge>
                      <Badge variant="outline">{ganttMilestones.length} milestones</Badge>
                      <Badge variant={offBaselineItemCount > 0 ? 'secondary' : 'outline'}>{offBaselineItemCount} off baseline</Badge>
                    </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button variant="outline" onClick={() => setCollapsedGanttItems([])}>Expand All</Button>
                  <Button
                    variant="outline"
                    onClick={() =>
                      setCollapsedGanttItems(
                        timelineItems
                          .filter((item) => ganttChildrenLookup.get(item.id))
                          .map((item) => item.id),
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
                    Use the planner actions here. The schedule grid stays below, and the bottom scrollbar remains visible while you move across weeks.
                  </div>
                  <div className="flex flex-wrap items-center gap-2 text-xs">
                    <span className="text-muted-foreground">Name colors:</span>
                    <span className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold" style={getTaskNameStyle({ nodeType: 'Phase', status: 'InProgress' })}>Phase</span>
                    <span className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold" style={getTaskNameStyle({ nodeType: 'Workstream', status: 'InProgress' })}>Workstream</span>
                    <span className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold" style={getTaskNameStyle({ nodeType: 'Task', status: 'InProgress' })}>In progress</span>
                    <span className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold" style={getTaskNameStyle({ nodeType: 'Task', status: 'Blocked' })}>Blocked</span>
                    <span className="inline-flex items-center rounded-full border px-2 py-0.5 font-semibold" style={getTaskNameStyle({ nodeType: 'Task', status: 'Completed' })}>Completed</span>
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    variant={ganttQuickFilters.overdue ? 'default' : 'outline'}
                    onClick={() => setGanttQuickFilters((current) => ({ ...current, overdue: !current.overdue }))}
                  >
                    Overdue
                  </Button>
                  <Button
                    variant={ganttQuickFilters.offBaseline ? 'default' : 'outline'}
                    onClick={() => setGanttQuickFilters((current) => ({ ...current, offBaseline: !current.offBaseline }))}
                  >
                    Off Baseline
                  </Button>
                  <Button
                    variant={ganttQuickFilters.assignedToMe ? 'default' : 'outline'}
                    onClick={() => setGanttQuickFilters((current) => ({ ...current, assignedToMe: !current.assignedToMe }))}
                    disabled={!currentUserId}
                  >
                    Assigned To Me
                  </Button>
                  <Button variant="outline" onClick={() => scrollGanttChart(-480)}>Scroll Left</Button>
                  <Button variant="outline" onClick={() => scrollGanttChart(480)}>Scroll Right</Button>
                  <Button onClick={exportGanttToExcel}>
                    <Download className="mr-2 h-4 w-4" />
                    Export Excel
                  </Button>
                </div>
              </div>
            </div>
            {editingWorkItemId ? (
              <div className="border-b bg-muted/20 px-6 py-4">
                <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <div className="text-sm font-semibold">Planner Editor</div>
                      {governanceSummary?.hasLockedBaseline ? (
                        <Badge variant="secondary">Baseline locked</Badge>
                      ) : null}
                    </div>
                    <div className="text-xs text-muted-foreground">
                      {plannerEditingItem ? `${plannerEditingItem.outline} · ${plannerEditingItem.title}` : 'Editing selected work item'}
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={resetWorkEditor}>Cancel</Button>
                    <Button size="sm" onClick={saveWorkEditor}>
                      <Save className="mr-2 h-4 w-4" />
                      Save Changes
                    </Button>
                  </div>
                </div>
                <div className="grid gap-3 md:grid-cols-6 xl:grid-cols-8">
                  <div className="grid gap-2 md:col-span-2">
                    <Label>Title</Label>
                    <Input value={work.title} onChange={(e) => setWork((p) => ({ ...p, title: e.target.value }))} />
                  </div>
                  <div className="grid gap-2">
                    <Label>Status</Label>
                    <Select value={work.status || taskStatusOptions[0]} onValueChange={(value) => setWork((p) => ({ ...p, status: value }))}>
                      <SelectTrigger><SelectValue /></SelectTrigger>
                      <SelectContent>{taskStatusOptions.map((item) => <SelectItem key={item} value={item}>{formatCatalogLabel(item)}</SelectItem>)}</SelectContent>
                    </Select>
                  </div>
                  <div className="grid gap-2">
                    <Label>Owner</Label>
                    <Select value={work.assignedToUserId || 'none'} onValueChange={(value) => setWork((p) => ({ ...p, assignedToUserId: value === 'none' ? undefined : value }))}>
                      <SelectTrigger><SelectValue placeholder="Select owner" /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">Unassigned</SelectItem>
                        {activeUsers.map((user) => <SelectItem key={user.id} value={user.id}>{formatUserLabel(user)}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="grid gap-2">
                    <Label>Start</Label>
                    <Input type="date" value={work.plannedStartDate ? String(work.plannedStartDate).slice(0, 10) : ''} onChange={(e) => setWork((p) => ({ ...p, plannedStartDate: e.target.value || undefined }))} />
                    {governanceSummary?.hasLockedBaseline ? <div className="text-[11px] text-muted-foreground">Changing this date requires a reason.</div> : null}
                  </div>
                  <div className="grid gap-2">
                    <Label>End</Label>
                    <Input type="date" value={work.plannedEndDate ? String(work.plannedEndDate).slice(0, 10) : ''} onChange={(e) => setWork((p) => ({ ...p, plannedEndDate: e.target.value || undefined }))} />
                    {governanceSummary?.hasLockedBaseline ? <div className="text-[11px] text-muted-foreground">Changing this date requires a reason.</div> : null}
                  </div>
                  <div className="grid gap-2">
                    <Label>% Complete</Label>
                    <Input type="number" value={work.percentComplete ?? 0} onChange={(e) => setWork((p) => ({ ...p, percentComplete: Number(e.target.value || '0') }))} />
                  </div>
                  <div className="grid gap-2">
                    <Label>Track</Label>
                    <Input type="number" value={work.effortEstimateHours ?? 0} onChange={(e) => setWork((p) => ({ ...p, effortEstimateHours: Number(e.target.value || '0') }))} />
                  </div>
                </div>
                {scheduleReasonRequired ? (
                  <div className="mt-3 grid gap-2">
                    <Label>Schedule Change Reason</Label>
                    <Textarea
                      rows={2}
                      placeholder="Explain why the planned dates changed."
                      value={work.scheduleChangeReason || ''}
                      onChange={(e) => setWork((p) => ({ ...p, scheduleChangeReason: e.target.value }))}
                    />
                    <div className="text-xs text-amber-700">
                      A locked baseline exists for this project, so date changes must include a reason.
                    </div>
                  </div>
                ) : null}
              </div>
            ) : null}
            <div className="min-h-0 flex-1 overflow-auto p-6">
              {timelineBounds ? (
                <div className="flex min-h-full flex-col rounded-lg border">
                  <div className="flex min-w-0">
                    <div className="sticky left-0 z-20 shrink-0 border-r bg-background">
                      <div
                        className="grid h-20 border-b bg-gradient-to-r from-slate-100 via-white to-slate-100"
                        style={{ width: `${GANTT_LEFT_GRID_WIDTH}px`, gridTemplateColumns: GANTT_LEFT_GRID_TEMPLATE }}
                      >
                        <div className="flex items-center border-r border-slate-300/70 px-2">
                          <div className="rounded-full bg-white/90 px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-600 shadow-sm">
                            WBS
                          </div>
                        </div>
                        <div className="flex items-center border-r border-slate-300/70 px-3">
                          <div className="space-y-0.5">
                            <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">Task</div>
                            <div className="text-xs font-medium text-slate-700">Name</div>
                          </div>
                        </div>
                        <div className="flex items-center border-r border-slate-300/70 px-2">
                          <div className="space-y-0.5">
                            <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">Owner</div>
                            <div className="text-xs font-medium text-slate-700">Assignee</div>
                          </div>
                        </div>
                        <div className="flex items-center border-r border-slate-300/70 px-2">
                          <div className="space-y-0.5">
                            <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">Track</div>
                            <div className="text-xs font-medium text-slate-700">Effort</div>
                          </div>
                        </div>
                        <div className="flex items-center border-r border-slate-300/70 px-3">
                          <div className="space-y-0.5">
                            <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">Status</div>
                            <div className="text-xs font-medium text-slate-700">Progress</div>
                          </div>
                        </div>
                        <div className="flex items-center border-r border-slate-300/70 px-2">
                          <div className="space-y-0.5">
                            <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">Start</div>
                            <div className="text-xs font-medium text-slate-700">Date</div>
                          </div>
                        </div>
                        <div className="flex items-center border-r border-slate-300/70 px-2">
                          <div className="space-y-0.5">
                            <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">End</div>
                            <div className="text-xs font-medium text-slate-700">Date</div>
                          </div>
                        </div>
                        <div className="flex items-center px-2">
                          <div className="space-y-0.5">
                            <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-slate-500">Dur</div>
                            <div className="text-xs font-medium text-slate-700">Days</div>
                          </div>
                        </div>
                      </div>
                      {visibleTimelineItems.map((item, index) => {
                        const durationDays = Math.max(
                          1,
                          Math.round(
                            (new Date(item.plannedEndDate as string).setHours(0, 0, 0, 0)
                              - new Date(item.plannedStartDate as string).setHours(0, 0, 0, 0))
                              / 86400000,
                          ) + 1,
                        );
                        const isOverdue = isWorkItemOverdue(item);
                        const isOffBaseline = item.isOffBaseline;
                        return (
                          <div
                            key={item.id}
                            className={`relative grid h-14 cursor-pointer border-b text-sm transition-colors ${
                              editingWorkItemId === item.id
                                ? 'bg-primary/10 ring-1 ring-inset ring-primary/30'
                                :
                              isOffBaseline
                                ? 'bg-amber-50/70'
                                :
                              isOverdue
                                ? 'bg-red-50'
                                : ganttHighlight.taskIds.has(item.id)
                                ? 'bg-sky-50'
                                : index % 2 === 0
                                  ? 'bg-background'
                                  : 'bg-muted/10'
                            }`}
                            style={{ width: `${GANTT_LEFT_GRID_WIDTH}px`, gridTemplateColumns: GANTT_LEFT_GRID_TEMPLATE }}
                            role="button"
                            tabIndex={0}
                            onClick={() => beginEditWorkItem(item, { keepCurrentView: true })}
                            onKeyDown={(event) => {
                              if (event.key === 'Enter' || event.key === ' ') {
                                event.preventDefault();
                                beginEditWorkItem(item, { keepCurrentView: true });
                              }
                            }}
                            onMouseEnter={() => setHoveredGanttItemId(item.id)}
                            onMouseLeave={() => setHoveredGanttItemId((current) => (current === item.id ? null : current))}
                          >
                            {editingWorkItemId === item.id ? (
                              <div className="pointer-events-none absolute inset-y-1 left-1 z-10 w-1 rounded-full bg-primary shadow-[0_0_0_1px_rgba(59,130,246,0.18)]" />
                            ) : null}
                            <div className="flex items-center border-r px-2 text-muted-foreground">{item.outline}</div>
                            <div className="flex items-center gap-2 border-r px-3" style={{ paddingLeft: `${12 + item.depth * 18}px` }}>
                              {ganttChildrenLookup.get(item.id) ? (
                                <button
                                  type="button"
                                  className="rounded-sm border bg-background/70 p-0.5 text-muted-foreground transition hover:bg-muted"
                                  onClick={(event) => {
                                    event.stopPropagation();
                                    toggleGanttItem(item.id);
                                  }}
                                >
                                  {collapsedGanttItems.includes(item.id) ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
                                </button>
                              ) : (
                                <span className="w-4" />
                              )}
                              <span className={`h-2.5 w-2.5 rounded-full ${getGanttStatusTone(item)}`} />
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
                                      title={formatBaselineVarianceLabel(item.baselineVarianceDays)}
                                    >
                                      {item.baselineVarianceDays > 0 ? `+${item.baselineVarianceDays}d` : `${item.baselineVarianceDays}d`}
                                    </span>
                                  ) : null}
                                  {isOverdue ? (
                                    <span className="inline-flex shrink-0 items-center rounded-full border border-red-200 bg-red-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-red-700">
                                      Overdue
                                    </span>
                                  ) : null}
                                </div>
                                <div className="truncate text-xs text-muted-foreground">{item.nodeType}</div>
                                  </div>
                                </div>
                            <div className="flex items-center justify-center border-r px-2">
                              {(() => {
                                const assigneeLabel = item.assignedToUserId ? userLookup.get(item.assignedToUserId) || item.assignedToUserId : 'Unassigned';
                                return (
                                  <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-[11px] font-semibold text-primary" title={assigneeLabel}>
                                    {item.assignedToUserId ? getUserInitials(assigneeLabel) : '--'}
                                  </div>
                                );
                              })()}
                            </div>
                            <div className="flex items-center border-r px-2 text-muted-foreground">
                              {formatTrackerHours(item.effortEstimateHours ?? item.actualEffortHours)}
                            </div>
                            <div className="min-w-0 border-r px-2 py-2">
                              <span
                                className="inline-flex max-w-full items-center rounded-full border px-2 py-1 text-[11px] font-medium leading-none"
                                style={getPlannerStatusBadgeStyle(item)}
                                title={formatCatalogLabel(item.status || 'New')}
                              >
                                <span className="truncate">{formatCatalogLabel(item.status || 'New')}</span>
                              </span>
                            </div>
                            <div className="flex items-center border-r px-2 text-muted-foreground">{formatDateLabel(item.plannedStartDate)}</div>
                            <div className="flex items-center border-r px-2 text-muted-foreground">{formatDateLabel(item.plannedEndDate)}</div>
                            <div className="flex items-center px-2 text-muted-foreground">{durationDays}d</div>
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
                    <div className="shrink-0" style={{ width: `${ganttWidth}px` }}>
                      <div className="border-b bg-slate-50">
                        <div className="flex h-9 border-b bg-gradient-to-r from-slate-100 via-slate-50 to-slate-100" style={{ width: `${ganttWidth}px` }}>
                          {ganttMonthSegments.map((segment) => (
                            <div
                              key={segment.key}
                              className="flex items-center border-r border-slate-300/70 px-3"
                              style={{ width: `${segment.span * GANTT_DAY_WIDTH}px` }}
                            >
                              <div className="rounded-full bg-white/90 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-700 shadow-sm">
                                {segment.label}
                              </div>
                            </div>
                          ))}
                        </div>
                        <div className="flex h-8 border-b bg-gradient-to-r from-slate-50 via-white to-slate-50" style={{ width: `${ganttWidth}px` }}>
                          {ganttWeekSegments.map((segment) => (
                            <div
                              key={segment.key}
                              className="flex items-center border-r border-slate-200 px-2"
                              style={{ width: `${segment.span * GANTT_DAY_WIDTH}px` }}
                            >
                              <div className="inline-flex items-center rounded-full border border-sky-100 bg-sky-50 px-2 py-0.5 text-[11px] font-semibold text-sky-700 shadow-sm">
                                {segment.label.replace('Week ', 'W')}
                              </div>
                            </div>
                          ))}
                        </div>
                        <div
                          className="grid h-11 bg-white"
                          style={{ gridTemplateColumns: `repeat(${ganttColumns.length}, ${GANTT_DAY_WIDTH}px)` }}
                        >
                          {ganttColumns.map((column) => (
                            <div
                              key={column.key}
                              className={`border-r px-1 text-center ${format(column.date, 'EEE') === 'Sat' || format(column.date, 'EEE') === 'Sun' ? 'bg-slate-50' : ''} ${ganttTodayOffset !== null && Math.round(ganttTodayOffset / GANTT_DAY_WIDTH) === ganttColumns.findIndex((entry) => entry.key === column.key) ? 'bg-blue-50' : ''}`}
                            >
                              <div className="flex h-full flex-col items-center justify-center">
                                <div className="text-[9px] font-semibold uppercase tracking-[0.16em] text-slate-400">{format(column.date, 'EEE')}</div>
                                <div className="text-[13px] font-semibold text-slate-700">{format(column.date, 'dd')}</div>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                      <div className="relative">
                        {ganttTodayOffset !== null ? (
                          <>
                            <div className="pointer-events-none absolute top-0 z-30 h-full border-l-2 border-blue-500" style={{ left: `${ganttTodayOffset}px` }} />
                            <div className="pointer-events-none absolute top-2 z-30 -translate-x-1/2 rounded-full bg-blue-500 px-2 py-0.5 text-[10px] font-medium text-white" style={{ left: `${ganttTodayOffset}px` }}>
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
                            <marker id="gantt-arrowhead" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto">
                              <path d="M 0 0 L 8 4 L 0 8 z" fill="#475569" />
                            </marker>
                          </defs>
                          {ganttDependencyLines.map((line) => (
                            <g key={line.id}>
                              <path
                                d={line.path}
                                fill="none"
                                stroke={ganttHighlight.dependencyIds.has(line.id) ? '#0369a1' : line.stroke}
                                strokeWidth={ganttHighlight.dependencyIds.has(line.id) ? '2.5' : '1.5'}
                                strokeDasharray={line.dashed ? '5 4' : undefined}
                                markerEnd="url(#gantt-arrowhead)"
                                className="cursor-pointer"
                              />
                              <rect
                                x={line.labelX - 2}
                                y={line.labelY - 10}
                                width={line.label.length * 6.2 + 8}
                                height="16"
                                rx="4"
                                fill={ganttHighlight.dependencyIds.has(line.id) ? '#e0f2fe' : '#ffffff'}
                                opacity="0.96"
                              />
                              <text x={line.labelX + 2} y={line.labelY} fontSize="10" fill={ganttHighlight.dependencyIds.has(line.id) ? '#0369a1' : '#475569'}>
                                {line.label}
                              </text>
                            </g>
                          ))}
                          {ganttMilestones.map((milestone) => (
                            <g key={milestone.id} transform={`translate(${milestone.x}, ${milestone.y})`}>
                              <rect
                                x="-8"
                                y="-8"
                                width="16"
                                height="16"
                                transform="rotate(45)"
                                fill={ganttHighlight.milestoneIds.has(milestone.id) ? '#f59e0b' : '#eab308'}
                                stroke={ganttHighlight.milestoneIds.has(milestone.id) ? '#92400e' : '#a16207'}
                                strokeWidth={ganttHighlight.milestoneIds.has(milestone.id) ? '1.8' : '1.2'}
                                rx="1"
                              />
                              <text x="12" y="4" fontSize="10" fill={ganttHighlight.milestoneIds.has(milestone.id) ? '#92400e' : '#475569'}>
                                {milestone.title}
                              </text>
                            </g>
                          ))}
                        </svg>
                        {visibleTimelineItems.map((item, index) => {
                          const start = new Date(item.plannedStartDate as string).setHours(0, 0, 0, 0);
                          const end = new Date(item.plannedEndDate as string).setHours(0, 0, 0, 0);
                          const offset = Math.max(0, Math.round((start - timelineBounds.min) / 86400000)) * GANTT_DAY_WIDTH;
                          const durationDays = Math.max(1, Math.round((end - start) / 86400000) + 1);
                          const width = durationDays * GANTT_DAY_WIDTH;
                          const progressWidth = Math.max(6, Math.round((width * (item.percentComplete || 0)) / 100));
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
                                  :
                                isOffBaseline
                                  ? 'bg-amber-50/60'
                                  :
                                isOverdue
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
                              onClick={() => beginEditWorkItem(item, { keepCurrentView: true })}
                              onKeyDown={(event) => {
                                if (event.key === 'Enter' || event.key === ' ') {
                                  event.preventDefault();
                                  beginEditWorkItem(item, { keepCurrentView: true });
                                }
                              }}
                              onMouseEnter={() => setHoveredGanttItemId(item.id)}
                              onMouseLeave={() => setHoveredGanttItemId((current) => (current === item.id ? null : current))}
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
                                    className={weekIndex % 2 === 0 ? 'bg-slate-50/70' : 'bg-slate-100/70'}
                                    style={{ width: `${segment.span * GANTT_DAY_WIDTH}px` }}
                                  />
                                ))}
                              </div>
                              <div
                                className="absolute inset-0 grid"
                                style={{ gridTemplateColumns: `repeat(${ganttColumns.length}, ${GANTT_DAY_WIDTH}px)` }}
                              >
                                {ganttColumns.map((column) => (
                                  <div key={`${item.id}-${column.key}`} className="border-r" />
                                ))}
                              </div>
                              {isRollup ? (
                                <div
                                  className={`absolute top-3 z-30 h-2 rounded-full border-2 bg-background ${editingWorkItemId === item.id ? 'shadow-[0_0_0_4px_rgba(59,130,246,0.12)]' : ''} ${isOffBaseline ? 'shadow-[0_0_0_4px_rgba(245,158,11,0.16)]' : ''}`}
                                  style={{
                                    left: `${offset}px`,
                                    width: `${Math.max(width, 10)}px`,
                                    borderColor: isOverdue ? '#dc2626' : isOffBaseline ? '#d97706' : ganttHighlight.taskIds.has(item.id) ? '#0369a1' : palette.border,
                                    backgroundColor: isOverdue ? '#fee2e2' : isOffBaseline ? '#fef3c7' : palette.soft,
                                  }}
                                >
                                  <div className="absolute -left-0.5 top-[-5px] h-4 w-1 rounded-full" style={{ backgroundColor: isOverdue ? '#dc2626' : isOffBaseline ? '#d97706' : ganttHighlight.taskIds.has(item.id) ? '#0369a1' : palette.border }} />
                                  <div className="absolute -right-0.5 top-[-5px] h-4 w-1 rounded-full" style={{ backgroundColor: isOverdue ? '#dc2626' : isOffBaseline ? '#d97706' : ganttHighlight.taskIds.has(item.id) ? '#0369a1' : palette.border }} />
                                  <div className={`absolute -top-5 left-0 text-[11px] font-semibold ${isOverdue ? 'text-red-700' : isOffBaseline ? 'text-amber-700' : 'text-slate-700'}`}>
                                    {durationDays} days, {item.percentComplete || 0}%
                                  </div>
                                  {isOffBaseline ? (
                                    <div className="absolute -right-2 -top-2 rounded-sm bg-amber-500 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white">
                                      {item.baselineVarianceDays > 0 ? `+${item.baselineVarianceDays}d` : `${item.baselineVarianceDays}d`}
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
                                      color: isOverdue ? '#991b1b' : isOffBaseline ? '#92400e' : getTaskNameStyle(item).color,
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
                                    background: isOverdue ? 'linear-gradient(135deg, #ef4444 0%, #b91c1c 100%)' : isOffBaseline ? 'linear-gradient(135deg, #f59e0b 0%, #d97706 100%)' : `linear-gradient(135deg, ${palette.start} 0%, ${palette.end} 100%)`,
                                    boxShadow: isOverdue ? '0 8px 18px rgba(185, 28, 28, 0.22)' : isOffBaseline ? '0 8px 18px rgba(217, 119, 6, 0.24)' : '0 8px 18px rgba(15, 23, 42, 0.12)',
                                  }}
                                >
                                  {isOffBaseline ? (
                                    <div className="absolute -left-2 -top-2 rounded-sm bg-amber-500 px-1 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white">
                                      {item.baselineVarianceDays > 0 ? `+${item.baselineVarianceDays}d` : `${item.baselineVarianceDays}d`}
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
                                      background: isOverdue ? 'linear-gradient(90deg, #7f1d1d 0%, rgba(15, 23, 42, 0.08) 100%)' : `linear-gradient(90deg, ${palette.progress} 0%, rgba(15, 23, 42, 0.08) 100%)`,
                                    }}
                                  />
                                  <div className="absolute inset-0 flex items-center justify-between px-2 text-[11px] font-medium" style={{ color: palette.text }}>
                                    <span
                                      className="max-w-[70%] truncate rounded-md border px-2 py-0.5 text-[11px] font-semibold"
                                      style={{
                                        ...getTaskNameStyle(item),
                                        backgroundColor: isOverdue ? 'rgba(255,255,255,0.18)' : isOffBaseline ? 'rgba(255,251,235,0.28)' : `${getTaskNameStyle(item).backgroundColor}`,
                                        borderColor: isOverdue ? 'rgba(255,255,255,0.28)' : getTaskNameStyle(item).borderColor,
                                        color: isOverdue ? '#fff' : isOffBaseline ? '#fff7ed' : getTaskNameStyle(item).color,
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
                        <div className="font-medium text-slate-700">Planner Navigator</div>
                        <div className="text-muted-foreground">Drag this bar to move across the schedule while the left planning columns remain fixed.</div>
                      </div>
                    <div
                      ref={ganttBottomScrollRef}
                      className="overflow-x-auto overflow-y-hidden rounded-md border bg-white shadow-inner"
                      onScroll={() => syncGanttScroll('bottom')}
                    >
                        <div className="relative" style={{ width: `${ganttWidth}px`, height: '22px' }}>
                          <div className="absolute inset-0 flex">
                            {ganttWeekSegments.map((segment, weekIndex) => (
                              <div
                                key={`navigator-${segment.key}`}
                                className={weekIndex % 2 === 0 ? 'border-r bg-sky-50/70' : 'border-r bg-slate-100/80'}
                                style={{ width: `${segment.span * GANTT_DAY_WIDTH}px` }}
                              />
                            ))}
                          </div>
                          {ganttTodayOffset !== null ? (
                            <div className="absolute inset-y-0 border-l-2 border-blue-500" style={{ left: `${ganttTodayOffset}px` }} />
                          ) : null}
                          <div className="absolute inset-x-0 top-1/2 h-px -translate-y-1/2 bg-slate-300" />
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="text-sm text-muted-foreground">Add planned start and end dates to work items to build the Gantt view.</div>
              )}
            </div>
          </div>
        </DialogContent>
      </Dialog>
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
        onSuccess={() => { void load(); }}
      />
      <IssueRequisitionDialog
        open={materialIssueDialogOpen}
        onOpenChange={setMaterialIssueDialogOpen}
        requisitionId={materialIssueRequisitionId}
        onSuccess={() => { void load(); }}
      />
      <ReturnRequisitionDialog
        open={materialReturnDialogOpen}
        onOpenChange={setMaterialReturnDialogOpen}
        requisitionId={materialReturnRequisitionId}
        onSuccess={() => { void load(); }}
      />
    </div>
  );
}
