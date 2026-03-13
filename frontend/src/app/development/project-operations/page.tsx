'use client';

import Link from 'next/link';
import { useEffect, useMemo, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { projectService, type ProjectBillingSummaryReportItemDto, type ProjectDependencyWatchReportItemDto, type ProjectExpenseApprovalQueueItemDto, type ProjectInvoiceRequestQueueItemDto, type ProjectMaterialReconciliationReportItemDto, type ProjectPortfolioPrioritizationReportItemDto, type ProjectStrategicInitiativeReportItemDto, type ProjectTimesheetApprovalQueueItemDto, type ProjectWorkflowApprovalQueueItemDto } from '@/services/projectService';
import { RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

const formatMoney = (value: number, currency: string = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);

export default function ProjectOperationsPage() {
  const [loading, setLoading] = useState(true);
  const [secondaryLoading, setSecondaryLoading] = useState(true);
  const [workflowQueue, setWorkflowQueue] = useState<ProjectWorkflowApprovalQueueItemDto[]>([]);
  const [invoiceQueue, setInvoiceQueue] = useState<ProjectInvoiceRequestQueueItemDto[]>([]);
  const [timesheetQueue, setTimesheetQueue] = useState<ProjectTimesheetApprovalQueueItemDto[]>([]);
  const [expenseQueue, setExpenseQueue] = useState<ProjectExpenseApprovalQueueItemDto[]>([]);
  const [reconciliation, setReconciliation] = useState<ProjectMaterialReconciliationReportItemDto[]>([]);
  const [priorities, setPriorities] = useState<ProjectPortfolioPrioritizationReportItemDto[]>([]);
  const [billingSummary, setBillingSummary] = useState<ProjectBillingSummaryReportItemDto[]>([]);
  const [dependencyWatch, setDependencyWatch] = useState<ProjectDependencyWatchReportItemDto[]>([]);
  const [strategicInitiatives, setStrategicInitiatives] = useState<ProjectStrategicInitiativeReportItemDto[]>([]);

  const load = async () => {
    try {
      setLoading(true);
      setSecondaryLoading(true);
      const primaryResults = await Promise.allSettled([
        projectService.getWorkflowApprovalQueueReport(20),
        projectService.getInvoiceRequestQueueReport(20),
        projectService.getTimesheetApprovalQueue(undefined, 'Submitted', undefined, 20),
        projectService.getExpenseApprovalQueue(undefined, 'Submitted', undefined, 20),
        projectService.getMaterialReconciliationReport(20),
      ]);

      setWorkflowQueue(primaryResults[0].status === 'fulfilled' ? primaryResults[0].value : []);
      setInvoiceQueue(primaryResults[1].status === 'fulfilled' ? primaryResults[1].value : []);
      setTimesheetQueue(primaryResults[2].status === 'fulfilled' ? primaryResults[2].value : []);
      setExpenseQueue(primaryResults[3].status === 'fulfilled' ? primaryResults[3].value : []);
      setReconciliation(primaryResults[4].status === 'fulfilled' ? primaryResults[4].value : []);
      setLoading(false);

      const secondaryResults = await Promise.allSettled([
        projectService.getPortfolioPrioritizationReport(undefined, 12),
        projectService.getBillingSummaryReport(12),
        projectService.getDependencyWatchReport(undefined, undefined, 12),
        projectService.getStrategicInitiativeReport(undefined, 8),
      ]);
      setPriorities(secondaryResults[0].status === 'fulfilled' ? secondaryResults[0].value : []);
      setBillingSummary(secondaryResults[1].status === 'fulfilled' ? secondaryResults[1].value : []);
      setDependencyWatch(secondaryResults[2].status === 'fulfilled' ? secondaryResults[2].value : []);
      setStrategicInitiatives(secondaryResults[3].status === 'fulfilled' ? secondaryResults[3].value : []);
    } catch (error: any) {
      toast.error(error.message || 'Failed to load project operations');
    } finally {
      setLoading(false);
      setSecondaryLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => {
    const approvalBacklog = workflowQueue.filter((item) => item.status === 'PendingApproval').length;
    const financeBacklog = invoiceQueue.filter((item) => item.status !== 'Paid').length;
    const timeBacklogHours = timesheetQueue.reduce((sum, item) => sum + item.hours, 0);
    const expenseBacklogAmount = expenseQueue.reduce((sum, item) => sum + item.totalAmount, 0);
    const materialWatch = reconciliation.filter((item) => item.reconciliationStatus !== 'Balanced').length;
    const portfolioWatch = priorities.filter((item) => item.priorityBand !== 'Maintain').length;
    const dependencyAlerts = dependencyWatch.filter((item) => item.coordinationState !== 'Resolved').length;
    const activeInitiativeCount = strategicInitiatives.filter((item) => item.activeProjectCount > 0).length;
    return {
      approvalBacklog,
      financeBacklog,
      timeBacklogHours,
      expenseBacklogAmount,
      materialWatch,
      portfolioWatch,
      dependencyAlerts,
      activeInitiativeCount,
    };
  }, [dependencyWatch, expenseQueue, invoiceQueue, priorities, reconciliation, strategicInitiatives, timesheetQueue, workflowQueue]);

  const billingWatch = useMemo(
    () => billingSummary.filter((item) => item.overdueBillingScheduleCount > 0 || item.unbilledAmount > 0).slice(0, 6),
    [billingSummary],
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Project Operations</h1>
          <p className="text-muted-foreground">Central queue and exception view across approvals, finance handoff, materials, and portfolio watch items.</p>
        </div>
        <Button variant="outline" onClick={() => void load()}>
          <RefreshCw className="mr-2 h-4 w-4" />
          Refresh
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-3 xl:grid-cols-8">
        <Card><CardHeader className="pb-2"><CardDescription>Approval Backlog</CardDescription><CardTitle>{stats.approvalBacklog}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Finance Backlog</CardDescription><CardTitle>{stats.financeBacklog}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Pending Time</CardDescription><CardTitle>{stats.timeBacklogHours.toLocaleString()}h</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Pending Expenses</CardDescription><CardTitle>{formatMoney(stats.expenseBacklogAmount)}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Material Watch</CardDescription><CardTitle>{stats.materialWatch}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Portfolio Watch</CardDescription><CardTitle>{stats.portfolioWatch}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Dependency Alerts</CardDescription><CardTitle>{stats.dependencyAlerts}</CardTitle></CardHeader></Card>
        <Card><CardHeader className="pb-2"><CardDescription>Active Initiatives</CardDescription><CardTitle>{stats.activeInitiativeCount}</CardTitle></CardHeader></Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Workflow Backlog</CardTitle>
                <CardDescription>Pending approvals and recent workflow items.</CardDescription>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link href="/development/project-approvals">Open Approvals</Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-8 text-center text-muted-foreground">Loading workflow queue...</div> : null}
            {!loading && workflowQueue.length === 0 ? <div className="py-8 text-center text-muted-foreground">No workflow items are waiting.</div> : null}
            {workflowQueue.slice(0, 6).map((item) => (
              <div key={`${item.entityType}-${item.entityId}`} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.projectCode} | {item.itemTitle}</div>
                    <div className="text-sm text-muted-foreground">{item.entityType.replace('Project', '').replace(/([A-Z])/g, ' $1').trim()} | {item.queueStage}</div>
                  </div>
                  <Badge variant={item.status === 'PendingApproval' ? 'secondary' : 'outline'}>{item.status}</Badge>
                </div>
                <div className="mt-3">
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/development/projects/${item.projectId}`}>Open Project</Link>
                  </Button>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Finance Handoff</CardTitle>
                <CardDescription>Invoice requests that still need project-side follow-through.</CardDescription>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link href="/development/project-billing">Open Billing</Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-8 text-center text-muted-foreground">Loading finance queue...</div> : null}
            {!loading && invoiceQueue.length === 0 ? <div className="py-8 text-center text-muted-foreground">No invoice request backlog is open.</div> : null}
            {invoiceQueue.slice(0, 6).map((item) => (
              <div key={item.invoiceRequestId} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.projectCode} | {item.requestNumber}</div>
                    <div className="text-sm text-muted-foreground">{formatMoney(item.requestedAmount, item.currency)} | {item.queueStage}</div>
                  </div>
                  <Badge variant={item.status === 'Paid' ? 'outline' : 'secondary'}>{item.status}</Badge>
                </div>
                <div className="mt-3">
                  <Button asChild variant="outline" size="sm">
                    <Link href={`/development/projects/${item.projectId}`}>Open Project</Link>
                  </Button>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Time And Expense Queue</CardTitle>
                <CardDescription>Submitted operational transactions waiting on review.</CardDescription>
              </div>
              <div className="flex gap-2">
                <Button asChild variant="outline" size="sm">
                  <Link href="/development/timesheets">Timesheets</Link>
                </Button>
                <Button asChild variant="outline" size="sm">
                  <Link href="/development/expenses">Expenses</Link>
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-8 text-center text-muted-foreground">Loading operational queue...</div> : null}
            {!loading && timesheetQueue.length === 0 && expenseQueue.length === 0 ? <div className="py-8 text-center text-muted-foreground">No submitted time or expense items are waiting.</div> : null}
            {timesheetQueue.slice(0, 3).map((item) => (
              <div key={item.entryId} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.projectCode} | {item.hours}h</div>
                    <div className="text-sm text-muted-foreground">Time | {item.workType} | {item.queueStage}</div>
                  </div>
                  <Badge variant="secondary">{item.status}</Badge>
                </div>
              </div>
            ))}
            {expenseQueue.slice(0, 3).map((item) => (
              <div key={item.expenseId} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.projectCode} | {formatMoney(item.totalAmount, item.currency)}</div>
                    <div className="text-sm text-muted-foreground">Expense | {item.category} | {item.queueStage}</div>
                  </div>
                  <Badge variant="secondary">{item.status}</Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Materials Watch</CardTitle>
                <CardDescription>Projects where issued material and tracked cost are out of line.</CardDescription>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link href="/development/project-materials">Open Materials</Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-8 text-center text-muted-foreground">Loading material reconciliation...</div> : null}
            {!loading && reconciliation.length === 0 ? <div className="py-8 text-center text-muted-foreground">No material watch items are open.</div> : null}
            {reconciliation.filter((item) => item.reconciliationStatus !== 'Balanced').slice(0, 6).map((item) => (
              <div key={item.projectId} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.projectCode}</div>
                    <div className="text-sm text-muted-foreground">{item.reconciliationStatus} | variance {formatMoney(item.materialCostVariance)}</div>
                  </div>
                  <Badge variant={item.reconciliationStatus === 'UnderTracked' ? 'destructive' : 'outline'}>{item.reconciliationStatus}</Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Priority Watchlist</CardTitle>
                <CardDescription>Projects needing intervention or reprioritization.</CardDescription>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link href="/development/project-analytics">Open Analytics</Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-8 text-center text-muted-foreground">Loading priorities...</div> : null}
            {!loading && priorities.length === 0 ? <div className="py-8 text-center text-muted-foreground">No portfolio watch items are available.</div> : null}
            {priorities.slice(0, 6).map((item) => (
              <div key={item.projectId} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.projectCode}</div>
                    <div className="text-sm text-muted-foreground">{item.priorityBand} | score {item.priorityScore.toFixed(1)} | {item.recommendedAction}</div>
                  </div>
                  <Badge variant={item.priorityBand === 'Stabilize' ? 'destructive' : 'outline'}>{item.priorityBand}</Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Billing Watchlist</CardTitle>
                <CardDescription>Projects with overdue schedules or low billing coverage.</CardDescription>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link href="/development/project-billing">Open Billing</Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-8 text-center text-muted-foreground">Loading billing watchlist...</div> : null}
            {!loading && billingWatch.length === 0 ? <div className="py-8 text-center text-muted-foreground">No billing watch items are open.</div> : null}
            {billingWatch.map((item) => (
              <div key={item.projectId} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.projectCode}</div>
                    <div className="text-sm text-muted-foreground">
                      overdue {item.overdueBillingScheduleCount} | unbilled {formatMoney(item.unbilledAmount)} | margin {item.marginPercent.toFixed(1)}%
                    </div>
                  </div>
                  <Badge variant={item.overdueBillingScheduleCount > 0 ? 'destructive' : 'outline'}>{item.projectTitle}</Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Dependency Watch</CardTitle>
                <CardDescription>Cross-project coordination items that need action.</CardDescription>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link href="/development/project-dependencies">Open Dependencies</Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-8 text-center text-muted-foreground">Loading dependency watch...</div> : null}
            {secondaryLoading ? <div className="py-8 text-center text-muted-foreground">Loading dependency watch...</div> : null}
            {!loading && !secondaryLoading && dependencyWatch.length === 0 ? <div className="py-8 text-center text-muted-foreground">No dependency watch items are open.</div> : null}
            {dependencyWatch.slice(0, 6).map((item) => (
              <div key={item.interdependencyId} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.title}</div>
                    <div className="text-sm text-muted-foreground">
                      {item.sourceProjectCode} | {item.targetProjectCode} | {item.dependencyType}
                    </div>
                  </div>
                  <Badge variant={item.coordinationState === 'Overdue' ? 'destructive' : item.coordinationState === 'Watch' ? 'secondary' : 'outline'}>
                    {item.coordinationState}
                  </Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex items-center justify-between gap-3">
              <div>
                <CardTitle>Strategic Initiatives</CardTitle>
                <CardDescription>Business initiatives with the broadest active delivery footprint.</CardDescription>
              </div>
              <Button asChild variant="outline" size="sm">
                <Link href="/development/project-analytics">Open Analytics</Link>
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {loading ? <div className="py-8 text-center text-muted-foreground">Loading initiatives...</div> : null}
            {secondaryLoading ? <div className="py-8 text-center text-muted-foreground">Loading strategic initiatives...</div> : null}
            {!loading && !secondaryLoading && strategicInitiatives.length === 0 ? <div className="py-8 text-center text-muted-foreground">No strategic initiative groups are available.</div> : null}
            {strategicInitiatives.slice(0, 6).map((item) => (
              <div key={item.initiative} className="rounded-lg border p-4">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.initiative}</div>
                    <div className="text-sm text-muted-foreground">
                      active {item.activeProjectCount} | delayed {item.delayedProjectCount} | high-risk items {item.highRiskItemCount}
                    </div>
                  </div>
                  <Badge variant="outline">{item.projectCount} projects</Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
