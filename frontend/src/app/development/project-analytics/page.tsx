'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import {
  ProjectDependencyWatchReportItemDto,
  ProjectBillingSummaryReportItemDto,
  ProjectBudgetActualReportItemDto,
  ProjectInvoiceRequestQueueItemDto,
  ProjectPortfolioPrioritizationReportItemDto,
  ProjectPerformanceAnalyticsReportItemDto,
  ProjectPortfolioSummaryReportItemDto,
  ProjectProgramSummaryReportItemDto,
  ProjectStrategicInitiativeReportItemDto,
  ProjectWorkflowApprovalQueueItemDto,
  projectService,
} from '@/services/projectService';
import { toast } from 'sonner';

type AnalyticsState = {
  performance: ProjectPerformanceAnalyticsReportItemDto[];
  portfolios: ProjectPortfolioSummaryReportItemDto[];
  programs: ProjectProgramSummaryReportItemDto[];
  budget: ProjectBudgetActualReportItemDto[];
  billing: ProjectBillingSummaryReportItemDto[];
  invoiceQueue: ProjectInvoiceRequestQueueItemDto[];
  workflowApprovalQueue: ProjectWorkflowApprovalQueueItemDto[];
  prioritization: ProjectPortfolioPrioritizationReportItemDto[];
  strategicInitiatives: ProjectStrategicInitiativeReportItemDto[];
  dependencyWatch: ProjectDependencyWatchReportItemDto[];
};

const emptyState: AnalyticsState = {
  performance: [],
  portfolios: [],
  programs: [],
  budget: [],
  billing: [],
  invoiceQueue: [],
  workflowApprovalQueue: [],
  prioritization: [],
  strategicInitiatives: [],
  dependencyWatch: [],
};

const currency = (value: number | undefined) => (value ?? 0).toLocaleString();

const percent = (value: number | undefined) => `${(value ?? 0).toFixed(2)}%`;

export default function ProjectAnalyticsPage() {
  const [state, setState] = useState<AnalyticsState>(emptyState);
  const [loading, setLoading] = useState(true);
  const [secondaryLoading, setSecondaryLoading] = useState(true);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        setSecondaryLoading(true);
        const primaryResults = await Promise.allSettled([
          projectService.getPerformanceAnalyticsReport(150),
          projectService.getPortfolioSummaryReport(20),
          projectService.getProgramSummaryReport(undefined, 30),
          projectService.getBudgetActualReport(150),
          projectService.getBillingSummaryReport(150),
          projectService.getInvoiceRequestQueueReport(100),
          projectService.getWorkflowApprovalQueueReport(100),
        ]);

        setState((current) => ({
          ...current,
          performance: primaryResults[0].status === 'fulfilled' ? primaryResults[0].value : [],
          portfolios: primaryResults[1].status === 'fulfilled' ? primaryResults[1].value : [],
          programs: primaryResults[2].status === 'fulfilled' ? primaryResults[2].value : [],
          budget: primaryResults[3].status === 'fulfilled' ? primaryResults[3].value : [],
          billing: primaryResults[4].status === 'fulfilled' ? primaryResults[4].value : [],
          invoiceQueue: primaryResults[5].status === 'fulfilled' ? primaryResults[5].value : [],
          workflowApprovalQueue: primaryResults[6].status === 'fulfilled' ? primaryResults[6].value : [],
        }));
        setLoading(false);

        const secondaryResults = await Promise.allSettled([
          projectService.getPortfolioPrioritizationReport(undefined, 50),
          projectService.getStrategicInitiativeReport(undefined, 10),
          projectService.getDependencyWatchReport(undefined, undefined, 10),
        ]);

        setState((current) => ({
          ...current,
          prioritization: secondaryResults[0].status === 'fulfilled' ? secondaryResults[0].value : [],
          strategicInitiatives: secondaryResults[1].status === 'fulfilled' ? secondaryResults[1].value : [],
          dependencyWatch: secondaryResults[2].status === 'fulfilled' ? secondaryResults[2].value : [],
        }));
      } catch (error: any) {
        toast.error(error.message || 'Failed to load project analytics');
      } finally {
        setLoading(false);
        setSecondaryLoading(false);
      }
    };

    void load();
  }, []);

  const watchItems = state.performance.filter((item) => item.healthStatus === 'Watch');
  const profitableItems = state.billing.filter((item) => item.marginAmount > 0);
  const overrunItems = state.performance.filter((item) => item.projectedVariance < 0);
  const totalBaseline = state.performance.reduce((sum, item) => sum + item.budgetBaseline, 0);
  const totalActualCost = state.performance.reduce((sum, item) => sum + item.actualCost, 0);
  const totalForecast = state.performance.reduce((sum, item) => sum + item.estimateAtCompletion, 0);
  const totalProjectedVariance = state.performance.reduce((sum, item) => sum + item.projectedVariance, 0);
  const totalCollectedCash = state.billing.reduce((sum, item) => sum + item.collectedCashAmount, 0);
  const averageCpiRaw = state.performance.reduce((sum, item) => sum + (item.costPerformanceIndex ?? 0), 0);
  const averageCpi = state.performance.length > 0 ? averageCpiRaw / state.performance.length : 0;
  const topPortfolios = [...state.portfolios].sort((a, b) => b.totalEstimatedBudget - a.totalEstimatedBudget).slice(0, 5);
  const topPrograms = [...state.programs].sort((a, b) => b.projectCount - a.projectCount).slice(0, 6);
  const commercialLeaders = [...state.billing].sort((a, b) => b.marginAmount - a.marginAmount).slice(0, 8);
  const budgetPressure = [...state.budget].sort((a, b) => a.budgetVariance - b.budgetVariance).slice(0, 8);
  const financeBacklog = [...state.invoiceQueue].sort((a, b) => b.daysOutstanding - a.daysOutstanding).slice(0, 8);
  const approvalBacklog = [...state.workflowApprovalQueue].sort((a, b) => b.daysPending - a.daysPending).slice(0, 8);
  const prioritizationBoard = [...state.prioritization].slice(0, 8);
  const strategicInitiatives = [...state.strategicInitiatives].slice(0, 6);
  const dependencyWatch = [...state.dependencyWatch].slice(0, 6);
  const financeAgingCount = state.invoiceQueue.filter((item) => item.daysOutstanding > 7 && item.status !== 'SentToFinance').length;
  const pendingApprovalCount = state.workflowApprovalQueue.filter((item) => item.status === 'PendingApproval').length;
  const approvedApprovalCount = state.workflowApprovalQueue.filter((item) => item.status === 'Approved').length;
  const rejectedApprovalCount = state.workflowApprovalQueue.filter((item) => item.status === 'Rejected').length;
  const paidItems = state.billing.filter((item) => item.paidInvoiceRequestCount > 0).length;
  const activeInitiatives = state.strategicInitiatives.filter((item) => item.activeProjectCount > 0).length;
  const dependencyAlertCount = state.dependencyWatch.filter((item) => item.coordinationState !== 'Resolved').length;
  const collectionCoverage = state.billing.reduce((sum, item) => sum + item.invoiceRequestedAmount, 0) > 0
    ? (totalCollectedCash / state.billing.reduce((sum, item) => sum + item.invoiceRequestedAmount, 0)) * 100
    : 0;
  const maxPortfolioBudget = topPortfolios.reduce((max, item) => Math.max(max, item.totalEstimatedBudget), 0);

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-2">
            <h1 className="text-3xl font-bold tracking-tight">Project Analytics</h1>
            <p className="text-muted-foreground">
              Portfolio-level delivery, profitability, and forecast trends across active projects.
            </p>
          </div>
          <Button variant="outline" asChild>
            <Link href="/development/project-approvals">Open Approvals</Link>
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Budget Baseline</CardDescription>
            <CardTitle>{currency(totalBaseline)}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Actual Cost</CardDescription>
            <CardTitle>{currency(totalActualCost)}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Forecast at Completion</CardDescription>
            <CardTitle>{currency(totalForecast)}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Projected Variance</CardDescription>
            <CardTitle className={totalProjectedVariance < 0 ? 'text-red-600' : ''}>{currency(totalProjectedVariance)}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Average CPI</CardDescription>
            <CardTitle>{averageCpi.toFixed(2)}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Collected Cash</CardDescription>
            <CardTitle>{currency(totalCollectedCash)}</CardTitle>
          </CardHeader>
        </Card>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Projects Analysed</CardDescription>
            <CardTitle>{state.performance.length}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Watchlist Projects</CardDescription>
            <CardTitle>{watchItems.length}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Profitable Projects</CardDescription>
            <CardTitle>{profitableItems.length}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Finance Aging Queue</CardDescription>
            <CardTitle>{financeAgingCount}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Pending Approvals</CardDescription>
            <CardTitle>{pendingApprovalCount}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Cash Conversion</CardDescription>
            <CardTitle>{collectionCoverage.toFixed(2)}%</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Active Initiatives</CardDescription>
            <CardTitle>{activeInitiatives}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Dependency Alerts</CardDescription>
            <CardTitle>{dependencyAlertCount}</CardTitle>
          </CardHeader>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Approval Snapshot</CardTitle>
            <CardDescription>Workflow-driven approvals waiting on governance action.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3 sm:grid-cols-3">
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Pending</div>
              <div className="mt-1 text-2xl font-semibold">{pendingApprovalCount}</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Approved</div>
              <div className="mt-1 text-2xl font-semibold">{approvedApprovalCount}</div>
            </div>
            <div className="rounded-lg border p-4">
              <div className="text-sm text-muted-foreground">Rejected</div>
              <div className="mt-1 text-2xl font-semibold">{rejectedApprovalCount}</div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Approval Queue</CardTitle>
            <CardDescription>Projects, deliverables, budget revisions, and closures awaiting action.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading approval queue...</div> : null}
            {!loading && approvalBacklog.length === 0 ? <div className="text-sm text-muted-foreground">No workflow approvals are pending.</div> : null}
            {approvalBacklog.map((item) => (
              <div key={`${item.entityType}-${item.entityId}`} className="rounded-lg border p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.itemTitle}</div>
                    <div className="text-sm text-muted-foreground">
                      {item.projectCode} | {item.projectTitle}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    <Badge variant={item.status === 'PendingApproval' ? 'destructive' : 'outline'}>
                      {item.queueStage}
                    </Badge>
                    <Badge variant="secondary">{item.entityType.replace('Project', '').replace(/([A-Z])/g, ' $1').trim()}</Badge>
                  </div>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-3">
                  <div>Status: {item.status}</div>
                  <div>Submitted: {item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : 'Not recorded'}</div>
                  <div>Days pending: {item.daysPending}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Portfolio Prioritization</CardTitle>
          <CardDescription>Projects that need the most portfolio attention based on delivery pressure, open risks and issues, and overdue milestones.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {loading ? <div className="py-10 text-center text-muted-foreground">Loading prioritization board...</div> : null}
          {!loading && prioritizationBoard.length === 0 ? <div className="text-sm text-muted-foreground">No prioritization data is available.</div> : null}
          {prioritizationBoard.map((item) => (
            <div key={item.projectId} className="rounded-lg border p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="font-semibold">{item.projectTitle}</div>
                  <div className="text-sm text-muted-foreground">{item.projectCode} | {item.portfolioName || 'No portfolio'}{item.programName ? ` | ${item.programName}` : ''}</div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Badge variant={item.priorityBand === 'Stabilize' ? 'destructive' : item.priorityBand === 'Focus' ? 'secondary' : 'outline'}>
                    {item.priorityBand}
                  </Badge>
                  <Badge variant="outline">Score {item.priorityScore}</Badge>
                </div>
              </div>
              <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                <div>Status: {item.status}</div>
                <div>Health: {item.healthStatus}</div>
                <div>Open risks/issues: {item.openRiskCount}/{item.openIssueCount}</div>
                <div>Overdue milestones: {item.overdueMilestoneCount}</div>
              </div>
              <div className="mt-3 text-sm text-muted-foreground">{item.recommendedAction}</div>
            </div>
          ))}
        </CardContent>
      </Card>

      <div className="grid gap-6 xl:grid-cols-[1.05fr_0.95fr]">
        <Card>
          <CardHeader>
            <CardTitle>Strategic Initiatives</CardTitle>
            <CardDescription>Cross-portfolio delivery grouped by business intent.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading strategic initiatives...</div> : null}
            {secondaryLoading ? <div className="text-sm text-muted-foreground">Loading strategic initiatives...</div> : null}
            {!loading && !secondaryLoading && strategicInitiatives.length === 0 ? <div className="text-sm text-muted-foreground">No strategic initiative groupings are available.</div> : null}
            {strategicInitiatives.map((item) => (
              <div key={item.initiative} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.initiative}</div>
                    <div className="text-sm text-muted-foreground">
                      {item.portfolioNames.join(', ') || 'No portfolio'}
                      {item.programNames.length ? ` | ${item.programNames.join(', ')}` : ''}
                    </div>
                  </div>
                  <Badge variant="outline">{item.projectCount} projects</Badge>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                  <div>Active: {item.activeProjectCount}</div>
                  <div>Delayed: {item.delayedProjectCount}</div>
                  <div>High risk items: {item.highRiskItemCount}</div>
                  <div>Budget: {currency(item.totalEstimatedBudget)}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Dependency Watch</CardTitle>
            <CardDescription>Dependencies that need portfolio coordination before they affect delivery.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading dependencies...</div> : null}
            {secondaryLoading ? <div className="text-sm text-muted-foreground">Loading dependency watch...</div> : null}
            {!loading && !secondaryLoading && dependencyWatch.length === 0 ? <div className="text-sm text-muted-foreground">No dependency watch items are currently open.</div> : null}
            {dependencyWatch.map((item) => (
              <div key={item.interdependencyId} className="rounded-lg border p-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.title}</div>
                    <div className="text-sm text-muted-foreground">
                      {item.sourceProjectCode} | {item.targetProjectCode}
                    </div>
                  </div>
                  <div className="flex gap-2">
                    <Badge variant={item.coordinationState === 'Overdue' ? 'destructive' : item.coordinationState === 'Watch' ? 'secondary' : 'outline'}>
                      {item.coordinationState}
                    </Badge>
                    <Badge variant="outline">{item.impactLevel}</Badge>
                  </div>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-3">
                  <div>Type: {item.dependencyType}</div>
                  <div>Status: {item.status}</div>
                  <div>Due: {item.dueDate ? new Date(item.dueDate).toLocaleDateString() : 'Open'}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
        <Card>
          <CardHeader>
            <CardTitle>Portfolio Concentration</CardTitle>
            <CardDescription>Where budget and delivery risk are currently concentrated.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading portfolio analytics...</div> : null}
            {!loading && topPortfolios.length === 0 ? <div className="text-sm text-muted-foreground">No portfolio rollups available.</div> : null}
            {topPortfolios.map((item) => {
              const allocationPercent = maxPortfolioBudget > 0 ? (item.totalEstimatedBudget / maxPortfolioBudget) * 100 : 0;
              return (
                <div key={item.portfolioId} className="rounded-lg border p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-semibold">{item.portfolioName}</div>
                      <div className="text-sm text-muted-foreground">{item.portfolioCode}</div>
                    </div>
                    <Badge variant={item.highRiskProjectCount > 0 ? 'destructive' : 'outline'}>
                      {item.highRiskProjectCount} high risk
                    </Badge>
                  </div>
                  <div className="mt-3">
                    <Progress value={allocationPercent} />
                  </div>
                  <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                    <div>Programs: {item.programCount}</div>
                    <div>Projects: {item.projectCount}</div>
                    <div>Active: {item.activeProjectCount}</div>
                    <div>Budget: {currency(item.totalEstimatedBudget)}</div>
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Program Throughput</CardTitle>
            <CardDescription>Largest grouped delivery streams by scope and average progress.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading program analytics...</div> : null}
            {!loading && topPrograms.length === 0 ? <div className="text-sm text-muted-foreground">No program rollups available.</div> : null}
            {topPrograms.map((item) => (
              <div key={item.programId} className="rounded-lg border p-4">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.programName}</div>
                    <div className="text-sm text-muted-foreground">{item.portfolioName || 'No portfolio'} | {item.programCode}</div>
                  </div>
                  <Badge variant="outline">{item.projectCount} projects</Badge>
                </div>
                <div className="mt-3">
                  <Progress value={item.averageProgressPercent} />
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-3">
                  <div>Active: {item.activeProjectCount}</div>
                  <div>Budget: {currency(item.totalEstimatedBudget)}</div>
                  <div>Avg progress: {percent(item.averageProgressPercent)}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1.05fr_0.95fr]">
        <Card>
          <CardHeader>
            <CardTitle>Commercial Leaders</CardTitle>
            <CardDescription>Projects with the strongest current margins.</CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="py-10 text-center text-muted-foreground">Loading commercial analytics...</div>
            ) : !commercialLeaders.length ? (
              <div className="py-10 text-center text-muted-foreground">No billing summary data is available.</div>
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Project</TableHead>
                    <TableHead>Scheduled</TableHead>
                    <TableHead>Requested</TableHead>
                    <TableHead>Collected</TableHead>
                    <TableHead>Actual</TableHead>
                    <TableHead>Margin</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {commercialLeaders.map((item) => (
                    <TableRow key={item.projectId}>
                      <TableCell>
                        <div className="font-medium">{item.projectCode}</div>
                        <div className="text-sm text-muted-foreground">{item.projectTitle}</div>
                      </TableCell>
                      <TableCell>{currency(item.scheduledBillingAmount)}</TableCell>
                      <TableCell>{currency(item.invoiceRequestedAmount)}</TableCell>
                      <TableCell>{currency(item.collectedCashAmount)}</TableCell>
                      <TableCell>{currency(item.actualCost)}</TableCell>
                      <TableCell>
                        <div className={item.marginAmount < 0 ? 'text-red-600' : ''}>{currency(item.marginAmount)}</div>
                        <div className="text-xs text-muted-foreground">{percent(item.marginPercent)}</div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Budget Pressure</CardTitle>
            <CardDescription>Projects with the largest adverse budget variance.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-10 text-center text-muted-foreground">Loading budget summary...</div> : null}
            {!loading && budgetPressure.length === 0 ? <div className="text-sm text-muted-foreground">No budget variance data is available.</div> : null}
            {budgetPressure.map((item) => (
              <div key={item.projectId} className="rounded-lg border p-4">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <div className="font-semibold">{item.projectCode}</div>
                    <div className="text-sm text-muted-foreground">{item.projectTitle}</div>
                  </div>
                  <Badge variant={item.budgetVariance < 0 ? 'destructive' : 'outline'}>{item.status}</Badge>
                </div>
                <div className="mt-3 grid gap-2 text-sm md:grid-cols-4">
                  <div>Approved: {currency(item.approvedBudget)}</div>
                  <div>Actual: {currency(item.actualCost)}</div>
                  <div>Progress: {percent(item.progressPercent)}</div>
                  <div className={item.budgetVariance < 0 ? 'text-red-600 font-medium' : ''}>Variance: {currency(item.budgetVariance)}</div>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Finance Backlog</CardTitle>
          <CardDescription>Invoice requests waiting for submission or finance handoff.</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading finance backlog...</div>
          ) : !financeBacklog.length ? (
            <div className="py-10 text-center text-muted-foreground">No finance queue items are currently open.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Request</TableHead>
                  <TableHead>Project</TableHead>
                  <TableHead>Stage</TableHead>
                    <TableHead>Amount</TableHead>
                    <TableHead>Outstanding</TableHead>
                    <TableHead>Reference</TableHead>
                    <TableHead>Lifecycle</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {financeBacklog.map((item) => (
                  <TableRow key={item.invoiceRequestId}>
                    <TableCell>
                      <div className="font-medium">{item.requestNumber}</div>
                      <div className="text-sm text-muted-foreground">{item.billingScheduleName || 'Manual request'}</div>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{item.projectCode}</div>
                      <div className="text-sm text-muted-foreground">{item.projectTitle}</div>
                    </TableCell>
                    <TableCell>
                      <Badge variant={item.status === 'SentToFinance' ? 'secondary' : item.status === 'Submitted' ? 'outline' : 'destructive'}>
                        {item.queueStage}
                      </Badge>
                    </TableCell>
                    <TableCell>{item.currency} {currency(item.requestedAmount)}</TableCell>
                    <TableCell>
                      <span className={item.daysOutstanding > 7 && item.status !== 'SentToFinance' ? 'font-medium text-red-600' : ''}>
                        {item.daysOutstanding} day{item.daysOutstanding === 1 ? '' : 's'}
                      </span>
                    </TableCell>
                    <TableCell>{item.externalReference || 'Pending'}</TableCell>
                    <TableCell>
                      {item.status === 'Paid' ? `${paidItems} paid project(s)` : item.canMarkPaid ? 'Collectible' : item.canMarkInvoiced ? 'Awaiting invoice' : 'Queued'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Delivery Watchlist</CardTitle>
          <CardDescription>Projects where schedule, cost, or forecast indicators warrant intervention.</CardDescription>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div className="py-10 text-center text-muted-foreground">Loading watchlist...</div>
          ) : !watchItems.length && !overrunItems.length ? (
            <div className="py-10 text-center text-muted-foreground">No delivery exceptions are currently flagged.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Project</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Budget</TableHead>
                  <TableHead>EV / PV</TableHead>
                  <TableHead>CPI</TableHead>
                  <TableHead>EAC</TableHead>
                  <TableHead>Projected Variance</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {state.performance
                  .filter((item) => item.healthStatus === 'Watch' || item.projectedVariance < 0)
                  .sort((a, b) => a.projectedVariance - b.projectedVariance)
                  .map((item) => (
                    <TableRow key={item.projectId}>
                      <TableCell>
                        <div className="font-medium">{item.projectCode}</div>
                        <div className="text-sm text-muted-foreground">{item.projectTitle}</div>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Badge variant={item.healthStatus === 'Watch' ? 'destructive' : 'outline'}>{item.healthStatus}</Badge>
                          <span className="text-sm text-muted-foreground">{item.status}</span>
                        </div>
                      </TableCell>
                      <TableCell>{currency(item.budgetBaseline)}</TableCell>
                      <TableCell>
                        <div>{currency(item.earnedValue)}</div>
                        <div className="text-xs text-muted-foreground">PV {currency(item.plannedValue)}</div>
                      </TableCell>
                      <TableCell>{item.costPerformanceIndex?.toFixed(2) ?? 'N/A'}</TableCell>
                      <TableCell>
                        <div>{currency(item.estimateAtCompletion)}</div>
                        <div className="text-xs text-muted-foreground">ETC {currency(item.estimateToComplete)}</div>
                      </TableCell>
                      <TableCell className={item.projectedVariance < 0 ? 'text-red-600 font-medium' : ''}>
                        {currency(item.projectedVariance)}
                      </TableCell>
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
