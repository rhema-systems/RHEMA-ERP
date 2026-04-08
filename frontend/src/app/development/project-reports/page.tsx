'use client';

import { useEffect, useMemo, useState } from 'react';
import { addDays, format } from 'date-fns';
import { Download, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  DEFAULT_PROJECT_CURRENCY,
  formatProjectMoney,
  loadProjectCurrencyContext,
} from '@/lib/project-currency';
import { ProjectLookupDto, projectService } from '@/services/projectService';
import { toast } from 'sonner';

type ReportKey =
  | 'register'
  | 'taskAging'
  | 'milestones'
  | 'performance'
  | 'budgetActual'
  | 'phaseGates'
  | 'approvalWatch'
  | 'commercialAdmin'
  | 'postHandover'
  | 'designControl'
  | 'siteControls'
  | 'unitCommercialization'
  | 'portfolioSummary'
  | 'programSummary'
  | 'prioritization'
  | 'dependencyWatch'
  | 'strategic'
  | 'resourceCapacity'
  | 'resourceRecommendations'
  | 'riskIssue'
  | 'billing'
  | 'invoiceQueue'
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
  { value: 'performance', label: 'Performance Analytics' },
  { value: 'budgetActual', label: 'Budget vs Actual' },
  { value: 'phaseGates', label: 'Phase Gate Readiness' },
  { value: 'approvalWatch', label: 'Permit & Approval Watch' },
  { value: 'commercialAdmin', label: 'Commercial Admin Watch' },
  { value: 'postHandover', label: 'Post-Handover Watchlist' },
  { value: 'designControl', label: 'Design Control Watch' },
  { value: 'siteControls', label: 'Site Controls Watch' },
  { value: 'unitCommercialization', label: 'Unit Commercialization Watch' },
  { value: 'portfolioSummary', label: 'Portfolio Summary' },
  { value: 'programSummary', label: 'Program Summary' },
  { value: 'prioritization', label: 'Portfolio Prioritization' },
  { value: 'dependencyWatch', label: 'Dependency Watch' },
  { value: 'strategic', label: 'Strategic Initiatives' },
  { value: 'resourceCapacity', label: 'Resource Capacity' },
  { value: 'resourceRecommendations', label: 'Resource Capacity Recommendations' },
  { value: 'riskIssue', label: 'Risk and Issue Summary' },
  { value: 'billing', label: 'Billing Summary' },
  { value: 'invoiceQueue', label: 'Invoice Request Queue' },
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

const formatDate = (value?: string) => (value ? format(new Date(value), 'MMM dd, yyyy') : 'n/a');
const toInputDate = (value: Date) => format(value, 'yyyy-MM-dd');

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
  const [windowStart, setWindowStart] = useState<string>(() => toInputDate(new Date()));
  const [windowEnd, setWindowEnd] = useState<string>(() => toInputDate(addDays(new Date(), 30)));

  const selectedReportConfig = useMemo(
    () => REPORT_OPTIONS.find((option) => option.value === selectedReport),
    [selectedReport],
  );
  const supportsProjectFilter = ['taskAging', 'milestones', 'phaseGates', 'approvalWatch', 'commercialAdmin', 'postHandover', 'designControl', 'siteControls', 'unitCommercialization'].includes(selectedReport);
  const supportsDateRange = selectedReport === 'resourceCapacity' || selectedReport === 'resourceRecommendations';

  const loadReport = async (reportKey: ReportKey, projectId: string) => {
    setLoading(true);
    try {
      const projectFilter = projectId === 'all' ? undefined : projectId;
      const rangeStart = windowStart || toInputDate(new Date());
      const rangeEnd = windowEnd || rangeStart;
      if ((reportKey === 'resourceCapacity' || reportKey === 'resourceRecommendations')
        && new Date(rangeEnd).getTime() < new Date(rangeStart).getTime()) {
        throw new Error('End date must be on or after the start date');
      }
      const currencyContext = await loadProjectCurrencyContext().catch(() => ({
        activeCurrencies: [],
        baseCurrency: DEFAULT_PROJECT_CURRENCY,
        rawBaseCurrency: null,
      }));
      const formatCurrency = (value: number | undefined, currency?: string | null) =>
        formatProjectMoney(value ?? 0, currency, currencyContext.baseCurrency.code);

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
        case 'performance': {
          const items = await projectService.getPerformanceAnalyticsReport(250);
          next = {
            title: 'Performance Analytics',
            description: 'Earned value, forecast posture, and delivery health across tracked projects.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              status: item.status,
              healthStatus: item.healthStatus,
              budgetBaseline: formatCurrency(item.budgetBaseline),
              plannedValue: formatCurrency(item.plannedValue),
              earnedValue: formatCurrency(item.earnedValue),
              actualCost: formatCurrency(item.actualCost),
              costPerformanceIndex: item.costPerformanceIndex?.toFixed(2) ?? 'n/a',
              estimateAtCompletion: formatCurrency(item.estimateAtCompletion),
              projectedVariance: formatCurrency(item.projectedVariance),
            })),
            columns: ['projectCode', 'projectTitle', 'status', 'healthStatus', 'budgetBaseline', 'plannedValue', 'earnedValue', 'actualCost', 'costPerformanceIndex', 'estimateAtCompletion', 'projectedVariance'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Watch Status',
            secondaryValue: `${items.filter((item) => item.healthStatus === 'Watch').length}`,
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
        case 'phaseGates': {
          const items = await projectService.getPhaseGateReadinessReport(projectFilter, 250);
          next = {
            title: 'Phase Gate Readiness',
            description: 'Construction-stage gate posture across configured project phases.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              phaseName: item.projectPhaseName,
              gateStatus: item.gateStatus,
              stageGateRequired: item.isStageGateRequired ? 'Yes' : 'No',
              configuredRules: item.configuredRuleCount,
              satisfiedRules: item.satisfiedRuleCount,
              blockingFailures: item.blockingFailureCount,
              topBlockingMessage: item.topBlockingMessage || 'n/a',
            })),
            columns: ['projectCode', 'projectTitle', 'phaseName', 'gateStatus', 'stageGateRequired', 'configuredRules', 'satisfiedRules', 'blockingFailures', 'topBlockingMessage'],
            primaryLabel: 'Phase Gates',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Blocked / Needs Setup',
            secondaryValue: `${items.filter((item) => item.gateStatus === 'Blocked' || item.gateStatus === 'NeedsSetup').length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'approvalWatch': {
          const items = await projectService.getApprovalWatchReport(projectFilter, 250);
          next = {
            title: 'Permit & Approval Watch',
            description: 'Permit, approval, expiry, and pending-decision watchlist for construction delivery.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              phaseName: item.projectPhaseName || 'n/a',
              approvalType: item.approvalType,
              title: item.title,
              watchState: item.watchState,
              severity: item.severity,
              status: item.status,
              targetDecisionDate: formatDate(item.targetDecisionDate || undefined),
              daysToTargetDecision: item.daysToTargetDecision ?? 'n/a',
              expiryDate: formatDate(item.expiryDate || undefined),
              daysToExpiry: item.daysToExpiry ?? 'n/a',
            })),
            columns: ['projectCode', 'projectTitle', 'phaseName', 'approvalType', 'title', 'watchState', 'severity', 'status', 'targetDecisionDate', 'daysToTargetDecision', 'expiryDate', 'daysToExpiry'],
            primaryLabel: 'Watch Items',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Critical / High',
            secondaryValue: `${items.filter((item) => item.severity === 'Critical' || item.severity === 'High').length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'commercialAdmin': {
          const items = await projectService.getCommercialAdministrationReport(projectFilter, 250);
          next = {
            title: 'Commercial Admin Watch',
            description: 'Construction-commercial posture covering packages, valuations, certification, retention, and final account.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              watchState: item.watchState,
              alertCount: item.alertCount,
              approvedBudget: formatCurrency(item.approvedBudget, item.currency),
              packageForecastAmount: formatCurrency(item.packageForecastAmount, item.currency),
              forecastVarianceAmount: formatCurrency(item.forecastVarianceAmount, item.currency),
              variationOrderCount: item.variationOrderCount,
              netValuationAmount: formatCurrency(item.netValuationAmount, item.currency),
              netCertifiedAmount: formatCurrency(item.netCertifiedAmount, item.currency),
              retentionHeldAmount: formatCurrency(item.retentionHeldAmount, item.currency),
              finalAccountStatus: item.finalAccountStatus || 'n/a',
              topAlertMessage: item.topAlertMessage || 'n/a',
            })),
            columns: ['projectCode', 'projectTitle', 'watchState', 'alertCount', 'approvedBudget', 'packageForecastAmount', 'forecastVarianceAmount', 'variationOrderCount', 'netValuationAmount', 'netCertifiedAmount', 'retentionHeldAmount', 'finalAccountStatus', 'topAlertMessage'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Critical / Attention',
            secondaryValue: `${items.filter((item) => item.watchState === 'Critical' || item.watchState === 'Attention').length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'postHandover': {
          const items = await projectService.getPostHandoverWatchReport(projectFilter, 250);
          next = {
            title: 'Post-Handover Watchlist',
            description: 'Defects-liability, warranty, SLA, and unresolved handover exposure across completed delivery.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              watchState: item.watchState,
              highestSeverity: item.highestSeverity,
              openHandoverItems: item.openHandoverItemCount,
              activeDefectLiabilityCases: item.activeDefectLiabilityCount,
              responseBreaches: item.responseBreachCount,
              resolutionBreaches: item.resolutionBreachCount,
              warrantyExpiringSoon: item.warrantyExpiringSoonCount,
              totalRectificationExposure: formatCurrency(item.totalRectificationExposure),
              chargeableExposure: formatCurrency(item.chargeableExposure),
              warrantyExposure: formatCurrency(item.warrantyExposure),
            })),
            columns: ['projectCode', 'projectTitle', 'watchState', 'highestSeverity', 'openHandoverItems', 'activeDefectLiabilityCases', 'responseBreaches', 'resolutionBreaches', 'warrantyExpiringSoon', 'totalRectificationExposure', 'chargeableExposure', 'warrantyExposure'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Critical Exposure',
            secondaryValue: `${items.filter((item) => item.watchState === 'Critical').length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'designControl': {
          const items = await projectService.getDesignControlWatchReport(projectFilter, 250);
          next = {
            title: 'Design Control Watch',
            description: 'Drawing and submittal review pressure across active construction delivery.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              itemType: item.itemType,
              referenceCode: item.referenceCode,
              title: item.title,
              category: item.category,
              phaseName: item.projectPhaseName || 'n/a',
              packageName: item.projectPackageName || 'n/a',
              watchState: item.watchState,
              severity: item.severity,
              status: item.status,
              actionDueDate: formatDate(item.actionDueDate || undefined),
              daysToActionDue: item.daysToActionDue ?? 'n/a',
              responsibleParty: item.responsibleParty || 'n/a',
            })),
            columns: ['projectCode', 'projectTitle', 'itemType', 'referenceCode', 'title', 'category', 'phaseName', 'packageName', 'watchState', 'severity', 'status', 'actionDueDate', 'daysToActionDue', 'responsibleParty'],
            primaryLabel: 'Control Items',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Critical / High',
            secondaryValue: `${items.filter((item) => item.severity === 'Critical' || item.severity === 'High').length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'siteControls': {
          const items = await projectService.getSiteControlsWatchReport(projectFilter, 250);
          next = {
            title: 'Site Controls Watch',
            description: 'RFI response pressure and open site instruction exposure across project delivery.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              itemType: item.itemType,
              referenceCode: item.referenceCode,
              title: item.title,
              category: item.category,
              phaseName: item.projectPhaseName || 'n/a',
              packageName: item.projectPackageName || 'n/a',
              watchState: item.watchState,
              severity: item.severity,
              status: item.status,
              actionDueDate: formatDate(item.actionDueDate || undefined),
              daysToActionDue: item.daysToActionDue ?? 'n/a',
              estimatedCostImpact: item.estimatedCostImpact != null ? formatCurrency(item.estimatedCostImpact) : 'n/a',
              scheduleImpactDays: item.scheduleImpactDays ?? 'n/a',
              responsibleParty: item.responsibleParty || 'n/a',
            })),
            columns: ['projectCode', 'projectTitle', 'itemType', 'referenceCode', 'title', 'category', 'phaseName', 'packageName', 'watchState', 'severity', 'status', 'actionDueDate', 'daysToActionDue', 'estimatedCostImpact', 'scheduleImpactDays', 'responsibleParty'],
            primaryLabel: 'Site Items',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Critical / High',
            secondaryValue: `${items.filter((item) => item.severity === 'Critical' || item.severity === 'High').length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'unitCommercialization': {
          const items = await projectService.getUnitCommercializationWatchReport(projectFilter, 250);
          next = {
            title: 'Unit Commercialization Watch',
            description: 'Release, pricing, reservation, sales, lease, and handover consistency for project units.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              unitCode: item.unitCode || 'n/a',
              unitName: item.unitName,
              building: item.projectBuildingName || 'n/a',
              floor: item.projectFloorName || 'n/a',
              unitType: item.unitType,
              releaseState: item.releaseState,
              commercialStatus: item.commercialStatus,
              commercialIntent: item.commercialIntent || 'n/a',
              handoverStatus: item.handoverStatus,
              basePrice: item.basePrice != null ? formatCurrency(item.basePrice, item.currency) : 'n/a',
              currency: item.currency,
              watchState: item.watchState,
              severity: item.severity,
              watchMessage: item.watchMessage || 'n/a',
            })),
            columns: ['projectCode', 'projectTitle', 'unitCode', 'unitName', 'building', 'floor', 'unitType', 'releaseState', 'commercialStatus', 'commercialIntent', 'handoverStatus', 'basePrice', 'currency', 'watchState', 'severity', 'watchMessage'],
            primaryLabel: 'Units',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Attention / Critical',
            secondaryValue: `${items.filter((item) => item.watchState === 'Attention' || item.watchState === 'Critical').length}`,
            supportsProjectFilter: true,
          };
          break;
        }
        case 'portfolioSummary': {
          const items = await projectService.getPortfolioSummaryReport(120);
          next = {
            title: 'Portfolio Summary',
            description: 'Top-level portfolio rollups for budget, risk concentration, and active delivery load.',
            rows: items.map((item) => ({
              portfolioCode: item.portfolioCode,
              portfolioName: item.portfolioName,
              programCount: item.programCount,
              projectCount: item.projectCount,
              activeProjectCount: item.activeProjectCount,
              totalEstimatedBudget: formatCurrency(item.totalEstimatedBudget),
              totalActualCost: formatCurrency(item.totalActualCost),
              highRiskProjectCount: item.highRiskProjectCount,
            })),
            columns: ['portfolioCode', 'portfolioName', 'programCount', 'projectCount', 'activeProjectCount', 'totalEstimatedBudget', 'totalActualCost', 'highRiskProjectCount'],
            primaryLabel: 'Portfolios',
            primaryValue: `${items.length}`,
            secondaryLabel: 'High-Risk Portfolios',
            secondaryValue: `${items.filter((item) => item.highRiskProjectCount > 0).length}`,
          };
          break;
        }
        case 'programSummary': {
          const items = await projectService.getProgramSummaryReport(undefined, 150);
          next = {
            title: 'Program Summary',
            description: 'Program rollups covering project count, spend, and average delivery progress.',
            rows: items.map((item) => ({
              programCode: item.programCode,
              programName: item.programName,
              portfolioName: item.portfolioName,
              projectCount: item.projectCount,
              activeProjectCount: item.activeProjectCount,
              totalEstimatedBudget: formatCurrency(item.totalEstimatedBudget),
              totalActualCost: formatCurrency(item.totalActualCost),
              averageProgressPercent: `${item.averageProgressPercent.toFixed(2)}%`,
            })),
            columns: ['programCode', 'programName', 'portfolioName', 'projectCount', 'activeProjectCount', 'totalEstimatedBudget', 'totalActualCost', 'averageProgressPercent'],
            primaryLabel: 'Programs',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Active Programs',
            secondaryValue: `${items.filter((item) => item.activeProjectCount > 0).length}`,
          };
          break;
        }
        case 'prioritization': {
          const items = await projectService.getPortfolioPrioritizationReport(undefined, 150);
          next = {
            title: 'Portfolio Prioritization',
            description: 'Projects ordered by delivery pressure, variance, governance load, and overdue outcomes.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              portfolioName: item.portfolioName,
              programName: item.programName,
              status: item.status,
              priorityBand: item.priorityBand,
              priorityScore: item.priorityScore.toFixed(2),
              healthStatus: item.healthStatus,
              projectedVariance: formatCurrency(item.projectedVariance),
              openRiskCount: item.openRiskCount,
              openIssueCount: item.openIssueCount,
              overdueMilestoneCount: item.overdueMilestoneCount,
              recommendedAction: item.recommendedAction,
            })),
            columns: ['projectCode', 'projectTitle', 'portfolioName', 'programName', 'status', 'priorityBand', 'priorityScore', 'healthStatus', 'projectedVariance', 'openRiskCount', 'openIssueCount', 'overdueMilestoneCount', 'recommendedAction'],
            primaryLabel: 'Projects',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Stabilize',
            secondaryValue: `${items.filter((item) => item.priorityBand === 'Stabilize').length}`,
          };
          break;
        }
        case 'dependencyWatch': {
          const items = await projectService.getDependencyWatchReport(undefined, undefined, 150);
          next = {
            title: 'Dependency Watch',
            description: 'Cross-project dependencies that need escalation, coordination, or recovery attention.',
            rows: items.map((item) => ({
              sourceProjectCode: item.sourceProjectCode,
              targetProjectCode: item.targetProjectCode,
              title: item.title,
              dependencyType: item.dependencyType,
              impactLevel: item.impactLevel,
              status: item.status,
              dueDate: formatDate(item.dueDate),
              daysToDue: item.daysToDue,
              coordinationState: item.coordinationState,
            })),
            columns: ['sourceProjectCode', 'targetProjectCode', 'title', 'dependencyType', 'impactLevel', 'status', 'dueDate', 'daysToDue', 'coordinationState'],
            primaryLabel: 'Dependencies',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Alerts',
            secondaryValue: `${items.filter((item) => item.coordinationState !== 'Resolved').length}`,
          };
          break;
        }
        case 'strategic': {
          const items = await projectService.getStrategicInitiativeReport(undefined, 120);
          next = {
            title: 'Strategic Initiatives',
            description: 'Delivery grouped by strategic initiative for executive oversight and funding review.',
            rows: items.map((item) => ({
              initiative: item.initiative,
              projectCount: item.projectCount,
              activeProjectCount: item.activeProjectCount,
              atRiskProjectCount: item.atRiskProjectCount,
              delayedProjectCount: item.delayedProjectCount,
              highRiskItemCount: item.highRiskItemCount,
              totalEstimatedBudget: formatCurrency(item.totalEstimatedBudget),
              totalActualCost: formatCurrency(item.totalActualCost),
              averageProgressPercent: `${item.averageProgressPercent.toFixed(2)}%`,
              portfolioNames: item.portfolioNames.join(', ') || 'n/a',
              programNames: item.programNames.join(', ') || 'n/a',
            })),
            columns: ['initiative', 'projectCount', 'activeProjectCount', 'atRiskProjectCount', 'delayedProjectCount', 'highRiskItemCount', 'totalEstimatedBudget', 'totalActualCost', 'averageProgressPercent', 'portfolioNames', 'programNames'],
            primaryLabel: 'Initiatives',
            primaryValue: `${items.length}`,
            secondaryLabel: 'At-Risk Projects',
            secondaryValue: `${items.reduce((sum, item) => sum + item.atRiskProjectCount, 0)}`,
          };
          break;
        }
        case 'resourceCapacity': {
          const items = await projectService.getResourceCapacityReport(rangeStart, rangeEnd);
          next = {
            title: 'Resource Capacity',
            description: 'Capacity, leave impact, conflicts, and qualification risk across the selected planning window.',
            rows: items.map((item) => ({
              userDisplayName: item.userDisplayName || item.userId,
              allocationCount: item.allocationCount,
              totalAllocatedHours: item.totalAllocatedHours.toFixed(2),
              totalAllocatedPercent: item.totalAllocatedPercent.toFixed(2),
              effectiveCapacityHours: item.effectiveCapacityHours.toFixed(2),
              approvedLeaveHours: item.approvedLeaveHours.toFixed(2),
              approvedLeaveDays: item.approvedLeaveDays.toFixed(2),
              capacityUtilizationPercent: `${item.capacityUtilizationPercent.toFixed(2)}%`,
              conflictCount: item.conflictCount,
              certifiedSkillCount: item.certifiedSkillCount,
              expiredCertificationCount: item.expiredCertificationCount,
              qualificationRisk: item.qualificationRisk,
            })),
            columns: ['userDisplayName', 'allocationCount', 'totalAllocatedHours', 'totalAllocatedPercent', 'effectiveCapacityHours', 'approvedLeaveHours', 'approvedLeaveDays', 'capacityUtilizationPercent', 'conflictCount', 'certifiedSkillCount', 'expiredCertificationCount', 'qualificationRisk'],
            primaryLabel: 'Resources',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Over Capacity',
            secondaryValue: `${items.filter((item) => item.capacityUtilizationPercent > 100).length}`,
          };
          break;
        }
        case 'resourceRecommendations': {
          const items = await projectService.getResourceCapacityRecommendations(rangeStart, rangeEnd);
          next = {
            title: 'Resource Capacity Recommendations',
            description: 'Priority recommendations for overloaded or risk-exposed resources in the selected window.',
            rows: items.map((item) => ({
              userDisplayName: item.userDisplayName || item.userId,
              severity: item.severity,
              capacityUtilizationPercent: `${item.capacityUtilizationPercent.toFixed(2)}%`,
              approvedLeaveDays: item.approvedLeaveDays.toFixed(2),
              conflictCount: item.conflictCount,
              qualificationRisk: item.qualificationRisk,
              suggestedReductionHours: item.suggestedReductionHours.toFixed(2),
              suggestedReplacement: item.suggestedReplacementUserDisplayName || 'n/a',
              matchedSkills: item.matchedSkills.join(', ') || 'n/a',
              projectCodes: item.projectCodes.join(', ') || 'n/a',
              recommendation: item.recommendation,
            })),
            columns: ['userDisplayName', 'severity', 'capacityUtilizationPercent', 'approvedLeaveDays', 'conflictCount', 'qualificationRisk', 'suggestedReductionHours', 'suggestedReplacement', 'matchedSkills', 'projectCodes', 'recommendation'],
            primaryLabel: 'Recommendations',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Critical',
            secondaryValue: `${items.filter((item) => item.severity === 'Critical').length}`,
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
        case 'invoiceQueue': {
          const items = await projectService.getInvoiceRequestQueueReport(250);
          next = {
            title: 'Invoice Request Queue',
            description: 'Project-owned invoice requests moving through handoff, invoicing, and collection stages.',
            rows: items.map((item) => ({
              projectCode: item.projectCode,
              projectTitle: item.projectTitle,
              requestNumber: item.requestNumber,
              status: item.status,
              queueStage: item.queueStage,
              requestedAmount: formatCurrency(item.requestedAmount),
              currency: item.currency,
              billingDate: formatDate(item.billingDate),
              submittedAt: formatDate(item.submittedAt),
              daysOutstanding: item.daysOutstanding,
              externalReference: item.externalReference || 'n/a',
            })),
            columns: ['projectCode', 'projectTitle', 'requestNumber', 'status', 'queueStage', 'requestedAmount', 'currency', 'billingDate', 'submittedAt', 'daysOutstanding', 'externalReference'],
            primaryLabel: 'Requests',
            primaryValue: `${items.length}`,
            secondaryLabel: 'Unpaid',
            secondaryValue: `${items.filter((item) => item.status !== 'Paid').length}`,
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
  }, [selectedReport, selectedProjectId, windowStart, windowEnd]);

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
          <CardDescription>Select a report and narrow it where project-specific or date-window filtering applies.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-5">
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
              disabled={!supportsProjectFilter}
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
            <Label>Window Start</Label>
            <Input type="date" value={windowStart} onChange={(event) => setWindowStart(event.target.value)} disabled={!supportsDateRange} />
          </div>
          <div className="grid gap-2">
            <Label>Window End</Label>
            <Input type="date" value={windowEnd} onChange={(event) => setWindowEnd(event.target.value)} disabled={!supportsDateRange} />
          </div>
          <div className="grid gap-2">
            <Label>Scope</Label>
            <div className="flex h-10 items-center rounded-md border px-3 text-sm text-muted-foreground">
              {supportsProjectFilter
                ? 'Project-specific filter available'
                : supportsDateRange
                  ? 'Date-window filter available'
                  : 'Cross-project report'}
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
