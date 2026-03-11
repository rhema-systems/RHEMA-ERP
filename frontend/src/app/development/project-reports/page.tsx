'use client';

import { useEffect, useMemo, useState } from 'react';
import { format } from 'date-fns';
import { Download, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { ProjectLookupDto, projectService } from '@/services/projectService';
import { toast } from 'sonner';

type ReportKey =
  | 'register'
  | 'taskAging'
  | 'milestones'
  | 'budgetActual'
  | 'riskIssue'
  | 'billing'
  | 'approvals'
  | 'external'
  | 'materials'
  | 'procurement';

type ReportRow = Record<string, string | number | boolean | null | undefined>;

type ReportDataset = {
  title: string;
  description: string;
  rows: ReportRow[];
  columns: string[];
  primaryLabel: string;
  primaryValue: string;
  secondaryLabel: string;
  secondaryValue: string;
  supportsProjectFilter?: boolean;
};

const REPORT_OPTIONS: { value: ReportKey; label: string }[] = [
  { value: 'register', label: 'Project Register' },
  { value: 'taskAging', label: 'Task Aging' },
  { value: 'milestones', label: 'Milestone Tracker' },
  { value: 'budgetActual', label: 'Budget vs Actual' },
  { value: 'riskIssue', label: 'Risk and Issue Summary' },
  { value: 'billing', label: 'Billing Summary' },
  { value: 'approvals', label: 'Workflow Approval Queue' },
  { value: 'external', label: 'External Collaboration' },
  { value: 'materials', label: 'Material Reconciliation' },
  { value: 'procurement', label: 'Procurement Reconciliation' },
];

const EMPTY_DATASET: ReportDataset = {
  title: 'Project Register',
  description: 'Central project reporting for operational and executive review.',
  rows: [],
  columns: [],
  primaryLabel: 'Rows',
  primaryValue: '0',
  secondaryLabel: 'Exceptions',
  secondaryValue: '0',
};

const formatCurrency = (value: number | undefined) => (value ?? 0).toLocaleString();
const formatDate = (value?: string) => (value ? format(new Date(value), 'MMM dd, yyyy') : 'n/a');

const toDisplayLabel = (key: string) =>
  key
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ')
    .replace(/\b\w/g, (char) => char.toUpperCase());

const toCsv = (columns: string[], rows: ReportRow[]) => {
  const escape = (value: unknown) => {
    const text = value == null ? '' : String(value);
    return `"${text.replace(/"/g, '""')}"`;
  };

  return [
    columns.map(escape).join(','),
    ...rows.map((row) => columns.map((column) => escape(row[column])).join(',')),
  ].join('\n');
};

export default function ProjectReportsPage() {
  const [projects, setProjects] = useState<ProjectLookupDto[]>([]);
  const [selectedProjectId, setSelectedProjectId] = useState<string>('all');
  const [selectedReport, setSelectedReport] = useState<ReportKey>('register');
  const [dataset, setDataset] = useState<ReportDataset>(EMPTY_DATASET);
  const [loading, setLoading] = useState(true);

  const selectedReportConfig = useMemo(
    () => REPORT_OPTIONS.find((option) => option.value === selectedReport),
    [selectedReport],
  );

  const loadReport = async (reportKey: ReportKey, projectId: string) => {
    setLoading(true);
    try {
      const projectFilter = projectId === 'all' ? undefined : projectId;
      let next: ReportDataset;

      switch (reportKey) {
        case 'register': {
          const items = await projectService.getProjectRegisterReport({ take: 250 });
          next = {
            title: 'Project Register',
            description: 'All governed projects with delivery, financial, and governance indicators.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              title: item.title,
              status: item.status,
              projectType: item.projectTypeName,
              priority: item.projectPriorityName,
              portfolio: item.portfolioName,
              program: item.programName,
              startDate: formatDate(item.startDate),
              targetEndDate: formatDate(item.targetEndDate),
              approvedBudget: formatCurrency(item.approvedBudget),
              actualCost: formatCurrency(item.actualCost),
              progressPercent: `${item.progressPercent.toFixed(2)}%`,
              openRiskCount: item.openRiskCount,
              openIssueCount: item.openIssueCount,
              overdueMilestones: item.overdueMilestoneCount,
            })),
            columns: ['projectCode', 'title', 'status', 'projectType', 'priority', 'portfolio', 'program', 'startDate', 'targetEndDate', 'approvedBudget', 'actualCost', 'progressPercent', 'openRiskCount', 'openIssueCount', 'overdueMilestones'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Watch Items',
            secondaryValue: `${items.filter((item) => item.openRiskCount > 0 || item.openIssueCount > 0 || item.overdueMilestoneCount > 0).length}`,
          };
          break;
        }
        case 'taskAging': {
          const items = await projectService.getTaskAgingReport(projectFilter, 250);
          next = {
            title: 'Task Aging',
            description: 'Overdue and aging work items across active projects.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              workItemTitle: item.workItemTitle,
              status: item.status,
              priority: item.priority,
              plannedEndDate: formatDate(item.plannedEndDate),
              daysOverdue: item.daysOverdue,
            })),
            columns: ['projectCode', 'projectTitle', 'workItemTitle', 'status', 'priority', 'plannedEndDate', 'daysOverdue'],
            primaryLabel: 'Tasks',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Overdue > 7 Days',
            secondaryValue: `${items.filter((item) => item.daysOverdue > 7).length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'milestones': {
          const items = await projectService.getMilestoneTrackerReport(projectFilter, 250);
          next = {
            title: 'Milestone Tracker',
            description: 'Milestone due dates, slip signals, and completion posture.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              milestoneTitle: item.milestoneTitle,
              status: item.status,
              targetDate: formatDate(item.targetDate),
              dueWindowDays: item.daysFromToday,
              overdue: item.isOverdue ? 'Yes' : 'No',
            })),
            columns: ['projectCode', 'projectTitle', 'milestoneTitle', 'status', 'targetDate', 'dueWindowDays', 'overdue'],
            primaryLabel: 'Milestones',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Overdue',
            secondaryValue: `${items.filter((item) => item.isOverdue).length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'budgetActual': {
          const items = await projectService.getBudgetActualReport(250);
          next = {
            title: 'Budget vs Actual',
            description: 'Budget posture, actual cost, and variance across active delivery.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              status: item.status,
              budgetStatus: item.budgetStatus,
              estimatedBudget: formatCurrency(item.estimatedBudget),
              approvedBudget: formatCurrency(item.approvedBudget),
              actualCost: formatCurrency(item.actualCost),
              budgetVariance: formatCurrency(item.budgetVariance),
              progressPercent: `${item.progressPercent.toFixed(2)}%`,
            })),
            columns: ['projectCode', 'projectTitle', 'status', 'budgetStatus', 'estimatedBudget', 'approvedBudget', 'actualCost', 'budgetVariance', 'progressPercent'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Negative Variance',
            secondaryValue: `${items.filter((item) => item.budgetVariance < 0).length}`,
          };
          break;
        }
        case 'riskIssue': {
          const items = await projectService.getRiskIssueSummaryReport(250);
          next = {
            title: 'Risk and Issue Summary',
            description: 'Open governance pressure by project.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              openRiskCount: item.openRiskCount,
              highRiskCount: item.highRiskCount,
              openIssueCount: item.openIssueCount,
            })),
            columns: ['projectCode', 'projectTitle', 'openRiskCount', 'highRiskCount', 'openIssueCount'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'High-Risk Projects',
            secondaryValue: `${items.filter((item) => item.highRiskCount > 0).length}`,
          };
          break;
        }
        case 'billing': {
          const items = await projectService.getBillingSummaryReport(250);
          next = {
            title: 'Billing Summary',
            description: 'Commercial readiness, cash conversion, and billing coverage by project.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              readySchedules: item.readyBillingScheduleCount,
              submittedRequests: item.submittedInvoiceRequestCount,
              sentToFinance: item.sentToFinanceInvoiceRequestCount,
              invoiced: item.invoicedInvoiceRequestCount,
              paid: item.paidInvoiceRequestCount,
              unbilledAmount: formatCurrency(item.unbilledAmount),
              collectedCash: formatCurrency(item.collectedCashAmount),
              marginPercent: `${item.marginPercent.toFixed(2)}%`,
            })),
            columns: ['projectCode', 'projectTitle', 'readySchedules', 'submittedRequests', 'sentToFinance', 'invoiced', 'paid', 'unbilledAmount', 'collectedCash', 'marginPercent'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Ready to Bill',
            secondaryValue: `${items.filter((item) => item.readyBillingScheduleCount > 0).length}`,
          };
          break;
        }
        case 'approvals': {
          const items = await projectService.getWorkflowApprovalQueueReport(250);
          next = {
            title: 'Workflow Approval Queue',
            description: 'Workflow-backed project approvals waiting on review or recently completed.',
            rows: items.map((item) => ({
              entityType: item.entityType,
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              itemTitle: item.itemTitle,
              status: item.status,
              queueStage: item.queueStage,
              submittedAt: formatDate(item.submittedAt),
              approvedAt: formatDate(item.approvedAt),
              daysPending: item.daysPending,
            })),
            columns: ['entityType', 'projectCode', 'projectTitle', 'itemTitle', 'status', 'queueStage', 'submittedAt', 'approvedAt', 'daysPending'],
            primaryLabel: 'Queue Items',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Pending Approval',
            secondaryValue: `${items.filter((item) => item.status === 'PendingApproval').length}`,
          };
          break;
        }
        case 'external': {
          const items = await projectService.getExternalCollaborationReport(250);
          next = {
            title: 'External Collaboration',
            description: 'Cross-project visibility into portal sharing, external submissions, sign-off pressure, and portal activity.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              status: item.status,
              collaborationState: item.collaborationState,
              policyCount: item.policyCount,
              externalVisibleDocuments: item.externalVisibleDocumentCount,
              externalVisibleDeliverables: item.externalVisibleDeliverableCount,
              pendingExternalSubmissions: item.pendingExternalSubmissionCount,
              pendingExternalSignOff: item.pendingExternalSignOffCount,
              externalComments: item.externalCommentCount,
              lastExternalCommentAt: formatDate(item.lastExternalCommentAt),
            })),
            columns: ['projectCode', 'projectTitle', 'status', 'collaborationState', 'policyCount', 'externalVisibleDocuments', 'externalVisibleDeliverables', 'pendingExternalSubmissions', 'pendingExternalSignOff', 'externalComments', 'lastExternalCommentAt'],
            primaryLabel: 'Shared Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Action Required',
            secondaryValue: `${items.filter((item) => item.collaborationState === 'ActionRequired').length}`,
          };
          break;
        }
        case 'materials': {
          const items = await projectService.getMaterialReconciliationReport(250);
          next = {
            title: 'Material Reconciliation',
            description: 'Inventory issues, returns, and posted material cost reconciliation.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              status: item.status,
              pendingRequisitions: item.pendingRequisitionCount,
              issuedRequisitions: item.issuedRequisitionCount,
              issuedValue: formatCurrency(item.issuedValue),
              returnedValue: formatCurrency(item.returnedValue),
              trackedMaterialCost: formatCurrency(item.trackedMaterialCost),
              materialVariance: formatCurrency(item.materialCostVariance),
              reconciliationStatus: item.reconciliationStatus,
            })),
            columns: ['projectCode', 'projectTitle', 'status', 'pendingRequisitions', 'issuedRequisitions', 'issuedValue', 'returnedValue', 'trackedMaterialCost', 'materialVariance', 'reconciliationStatus'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Under Tracked',
            secondaryValue: `${items.filter((item) => item.reconciliationStatus === 'UnderTracked').length}`,
          };
          break;
        }
        case 'procurement': {
          const items = await projectService.getProcurementReconciliationReport(250);
          next = {
            title: 'Procurement Reconciliation',
            description: 'Procurement, receipt, issue, and posting alignment by project.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              status: item.status,
              purchaseOrders: item.purchaseOrderCount,
              purchaseReceipts: item.purchaseReceiptCount,
              receivedAmount: formatCurrency(item.receivedAmount),
              acceptedReceiptAmount: formatCurrency(item.acceptedReceiptAmount),
              pendingInspectionAmount: formatCurrency(item.pendingInspectionAmount),
              netIssuedInventoryValue: formatCurrency(item.netIssuedInventoryValue),
              postedMaterialCost: formatCurrency(item.postedMaterialCost),
              issueToPostingVariance: formatCurrency(item.issueToPostingVariance),
              reconciliationStatus: item.reconciliationStatus,
            })),
            columns: ['projectCode', 'projectTitle', 'status', 'purchaseOrders', 'purchaseReceipts', 'receivedAmount', 'acceptedReceiptAmount', 'pendingInspectionAmount', 'netIssuedInventoryValue', 'postedMaterialCost', 'issueToPostingVariance', 'reconciliationStatus'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Pending Inspection',
            secondaryValue: `${items.filter((item) => item.reconciliationStatus === 'PendingInspection').length}`,
          };
          break;
        }
      }

      setDataset(next);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project report');
      setDataset(EMPTY_DATASET);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const loadProjects = async () => {
      try {
        setProjects(await projectService.lookupProjects(undefined, undefined, undefined, undefined, undefined));
      } catch (error: any) {
        toast.error(error.message || 'Failed to load project filter options');
      }
    };

    void loadProjects();
  }, []);

  useEffect(() => {
    void loadReport(selectedReport, selectedProjectId);
  }, [selectedReport, selectedProjectId]);

  const exportReport = () => {
    if (!dataset.rows.length) {
      toast.error('Load report data before exporting');
      return;
    }

    const csv = toCsv(dataset.columns, dataset.rows);
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${selectedReportConfig?.label.replace(/\s+/g, '_') || 'project_report'}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="space-y-2">
          <h1 className="text-3xl font-bold tracking-tight">Project Reports</h1>
          <p className="text-muted-foreground">
            Operational and executive reports for delivery, governance, billing, and reconciliation.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void loadReport(selectedReport, selectedProjectId)}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          <Button onClick={exportReport} disabled={!dataset.rows.length}>
            <Download className="mr-2 h-4 w-4" />
            Export CSV
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Report Controls</CardTitle>
          <CardDescription>Select a report and narrow it where project-specific filtering applies.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-3">
          <div className="grid gap-2">
            <Label>Report</Label>
            <Select value={selectedReport} onValueChange={(value) => setSelectedReport(value as ReportKey)}>
              <SelectTrigger><SelectValue placeholder="Select report" /></SelectTrigger>
              <SelectContent>
                {REPORT_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Project Filter</Label>
            <Select
              value={selectedProjectId}
              onValueChange={setSelectedProjectId}
              disabled={!dataset.supportsProjectFilter}
            >
              <SelectTrigger><SelectValue placeholder="All projects" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All projects</SelectItem>
                {projects.map((project) => (
                  <SelectItem key={project.id} value={project.id}>
                    {project.projectCode} | {project.title}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label>Scope</Label>
            <div className="flex h-10 items-center rounded-md border px-3 text-sm text-muted-foreground">
              {dataset.supportsProjectFilter ? 'Project-specific filter available' : 'Cross-project report'}
            </div>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{dataset.primaryLabel}</CardDescription>
            <CardTitle>{dataset.primaryValue}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>{dataset.secondaryLabel}</CardDescription>
            <CardTitle>{dataset.secondaryValue}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Selected Report</CardDescription>
            <CardTitle>{selectedReportConfig?.label}</CardTitle>
          </CardHeader>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <CardTitle>{dataset.title}</CardTitle>
              <CardDescription>{dataset.description}</CardDescription>
            </div>
            <Badge variant="outline">{dataset.rows.length} rows</Badge>
          </div>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading report...</div>
          ) : !dataset.rows.length ? (
            <div className="py-10 text-center text-muted-foreground">No rows returned for the selected report.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  {dataset.columns.map((column) => (
                    <TableHead key={column}>{toDisplayLabel(column)}</TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {dataset.rows.map((row, index) => (
                  <TableRow key={`${selectedReport}-${index}`}>
                    {dataset.columns.map((column) => (
                      <TableCell key={column}>
                        {row[column] == null || row[column] === '' ? 'n/a' : String(row[column])}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
