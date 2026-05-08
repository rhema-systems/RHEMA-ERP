import { type Dispatch, type SetStateAction } from 'react';
import { format } from 'date-fns';
import { Plus, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import type {
  CreateProjectBaselineDto,
  ProjectAiInsightDto,
  ProjectBaselineComparisonDto,
  ProjectDetailDto,
  ProjectFinancialControlSummaryDto,
  ProjectIntegrationSummaryDto,
  ProjectScheduleAnalysisDto,
} from '@/services/projectService';

type ProjectAnalysisTabProps = {
  project: ProjectDetailDto;
  scheduleAnalysis: ProjectScheduleAnalysisDto | null;
  workItemTitles: Map<string, string>;
  formatDateLabel: (value?: string) => string;
  formatMoney: (value: number | undefined, currency?: string | null, maximumFractionDigits?: number) => string;
  baseline: CreateProjectBaselineDto;
  setBaseline: Dispatch<SetStateAction<CreateProjectBaselineDto>>;
  baselineComparison: ProjectBaselineComparisonDto | null;
  onCompareBaseline: (baselineId: string) => void;
  financialSummary: ProjectFinancialControlSummaryDto | null;
  integrationSummary: ProjectIntegrationSummaryDto | null;
  aiInsights: ProjectAiInsightDto[];
  onRefresh: () => void;
  onCreateBaseline: () => void;
  onGenerateRevenueRecognition: () => void;
};

export function ProjectAnalysisTab({
  project,
  scheduleAnalysis,
  workItemTitles,
  formatDateLabel,
  formatMoney,
  baseline,
  setBaseline,
  baselineComparison,
  onCompareBaseline,
  financialSummary,
  integrationSummary,
  aiInsights,
  onRefresh,
  onCreateBaseline,
  onGenerateRevenueRecognition,
}: ProjectAnalysisTabProps) {
  return (
    <div className="space-y-6">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>Schedule Analysis</CardTitle>
          <Button variant="outline" size="sm" onClick={onRefresh}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </CardHeader>
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
            <div className="grid gap-2"><Label>Name</Label><Input value={baseline.name} onChange={(event) => setBaseline((current) => ({ ...current, name: event.target.value }))} /></div>
            <div className="grid gap-2"><Label>Notes</Label><Input value={baseline.notes || ''} onChange={(event) => setBaseline((current) => ({ ...current, notes: event.target.value }))} /></div>
            <div className="flex items-end"><Button disabled={!baseline.name.trim()} onClick={onCreateBaseline}><Plus className="mr-2 h-4 w-4" />Create Baseline</Button></div>
          </div>
          <div className="space-y-3">
            {project.baselines.length === 0 ? <div className="text-sm text-muted-foreground">No baselines are available.</div> : null}
            {project.baselines.map((baselineRecord) => <div key={baselineRecord.id} className="flex items-center justify-between rounded-lg border p-4"><div><div className="font-medium">{baselineRecord.name}</div><div className="text-sm text-muted-foreground">{format(new Date(baselineRecord.createdOn), 'MMM dd, yyyy HH:mm')} | {baselineRecord.isLocked ? 'Locked' : 'Editable'}{baselineRecord.snapshotFinishDate ? ` | finish ${format(new Date(baselineRecord.snapshotFinishDate), 'MMM dd, yyyy')}` : ''}</div></div><Button variant="outline" size="sm" onClick={() => onCompareBaseline(baselineRecord.id)}>Compare</Button></div>)}
          </div>
          {baselineComparison ? <div className="rounded-lg border p-4"><div className="font-medium">{baselineComparison.baselineName}</div><div className="mt-3 grid gap-3 md:grid-cols-3"><div><div className="text-sm text-muted-foreground">Baseline Progress</div><div className="font-medium">{baselineComparison.baselineProgressPercent}%</div></div><div><div className="text-sm text-muted-foreground">Current Progress</div><div className="font-medium">{baselineComparison.currentProgressPercent}%</div></div><div><div className="text-sm text-muted-foreground">Schedule Variance</div><div className="font-medium">{baselineComparison.scheduleVarianceDays} day(s)</div></div><div><div className="text-sm text-muted-foreground">Budget Variance</div><div className="font-medium">{formatMoney(baselineComparison.budgetVariance)}</div></div><div><div className="text-sm text-muted-foreground">Changed Work Items</div><div className="font-medium">{baselineComparison.changedWorkItemCount}</div></div><div><div className="text-sm text-muted-foreground">Changed Milestones</div><div className="font-medium">{baselineComparison.changedMilestoneCount}</div></div></div><div className="mt-4 space-y-3">{baselineComparison.workItemChanges.slice(0, 5).map((change) => <div key={change.workItemId} className="rounded-lg border p-4 text-sm"><div className="font-medium">{change.workItemTitle}</div><div className="text-muted-foreground">{change.baselinePlannedStartDate ? format(new Date(change.baselinePlannedStartDate), 'MMM dd, yyyy') : 'N/A'} to {change.baselinePlannedEndDate ? format(new Date(change.baselinePlannedEndDate), 'MMM dd, yyyy') : 'N/A'} {'->'} {change.currentPlannedStartDate ? format(new Date(change.currentPlannedStartDate), 'MMM dd, yyyy') : 'N/A'} to {change.currentPlannedEndDate ? format(new Date(change.currentPlannedEndDate), 'MMM dd, yyyy') : 'N/A'} | variance {change.scheduleVarianceDays} day(s)</div></div>)}</div></div> : null}
        </CardContent>
      </Card>
      <div className="grid gap-6 xl:grid-cols-2">
        <Card>
          <CardHeader><CardTitle>Financial Control</CardTitle></CardHeader>
          <CardContent className="space-y-4">
            {financialSummary ? (
              <div className="grid gap-4 md:grid-cols-2">
                <div><div className="text-sm text-muted-foreground">Pending Cost</div><div className="text-xl font-semibold">{formatMoney(financialSummary.pendingCost)}</div></div>
                <div><div className="text-sm text-muted-foreground">Forecast Cost</div><div className="text-xl font-semibold">{formatMoney(financialSummary.forecastCost)}</div></div>
                <div><div className="text-sm text-muted-foreground">Procurement Requested</div><div className="text-xl font-semibold">{formatMoney(financialSummary.procurementRequestedAmount)}</div></div>
                <div><div className="text-sm text-muted-foreground">Procurement Committed</div><div className="text-xl font-semibold">{formatMoney(financialSummary.procurementCommittedAmount)}</div></div>
                <div><div className="text-sm text-muted-foreground">Procurement Received</div><div className="text-xl font-semibold">{formatMoney(financialSummary.procurementReceivedAmount)}</div></div>
                <div><div className="text-sm text-muted-foreground">Pending Inspection</div><div className="text-xl font-semibold">{formatMoney(financialSummary.procurementPendingInspectionAmount)}</div></div>
                <div><div className="text-sm text-muted-foreground">Planned Value</div><div className="text-xl font-semibold">{formatMoney(financialSummary.plannedValue)}</div></div>
                <div><div className="text-sm text-muted-foreground">Earned Value</div><div className="text-xl font-semibold">{formatMoney(financialSummary.earnedValue)}</div></div>
                <div><div className="text-sm text-muted-foreground">Schedule Variance</div><div className="text-xl font-semibold">{formatMoney(financialSummary.scheduleVariance)}</div></div>
                <div><div className="text-sm text-muted-foreground">Cost Variance</div><div className="text-xl font-semibold">{formatMoney(financialSummary.costVariance)}</div></div>
                <div><div className="text-sm text-muted-foreground">Total Exposure</div><div className="text-xl font-semibold">{formatMoney(financialSummary.totalExposureAmount)}</div></div>
                <div><div className="text-sm text-muted-foreground">Remaining Budget</div><div className="text-xl font-semibold">{formatMoney(financialSummary.remainingBudget)}</div></div>
                <div><div className="text-sm text-muted-foreground">Scheduled Billing</div><div className="text-xl font-semibold">{formatMoney(financialSummary.scheduledBillingAmount)}</div></div>
                <div><div className="text-sm text-muted-foreground">Invoice Requested</div><div className="text-xl font-semibold">{formatMoney(financialSummary.invoiceRequestedAmount)}</div></div>
                <div><div className="text-sm text-muted-foreground">Recognized Revenue</div><div className="text-xl font-semibold">{formatMoney(financialSummary.recognizedRevenue)}</div></div>
                <div><div className="text-sm text-muted-foreground">Gross Margin</div><div className="text-xl font-semibold">{formatMoney(financialSummary.grossMargin)}</div></div>
                <div><div className="text-sm text-muted-foreground">Profitability</div><div className="text-xl font-semibold">{financialSummary.profitabilityPercent.toFixed(2)}%</div></div>
                <div><div className="text-sm text-muted-foreground">TCPI</div><div className="text-xl font-semibold">{financialSummary.toCompletePerformanceIndex?.toFixed(2) ?? 'N/A'}</div></div>
              </div>
            ) : <div className="text-sm text-muted-foreground">Financial details are not available for this user.</div>}
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
                  <div><div className="text-sm text-muted-foreground">Purchase orders</div><div className="text-xl font-semibold">{integrationSummary.purchaseOrderCount}</div><div className="text-sm text-muted-foreground">Open {integrationSummary.openPurchaseOrderCount} | Amount {formatMoney(integrationSummary.purchaseOrderAmount)}</div></div>
                  <div><div className="text-sm text-muted-foreground">Purchase receipts</div><div className="text-xl font-semibold">{integrationSummary.purchaseReceiptCount}</div><div className="text-sm text-muted-foreground">Pending inspection {integrationSummary.pendingPurchaseReceiptInspectionCount}</div></div>
                  <div><div className="text-sm text-muted-foreground">Revenue snapshots</div><div className="text-xl font-semibold">{integrationSummary.revenueRecognitionCount}</div></div>
                  <div><div className="text-sm text-muted-foreground">Net issued inventory</div><div className="text-xl font-semibold">{integrationSummary.issuedInventoryRequisitionCount}</div><div className="text-sm text-muted-foreground">Net {formatMoney(integrationSummary.netIssuedInventoryValue)} | Returned {formatMoney(integrationSummary.returnedInventoryValue)}</div></div>
                  <div><div className="text-sm text-muted-foreground">Portfolio / program</div><div className="text-xl font-semibold">{integrationSummary.hasPortfolio || integrationSummary.hasProgram ? 'Linked' : 'Standalone'}</div></div>
                </div>
                {integrationSummary.warnings.length === 0 ? <div className="text-sm text-muted-foreground">No integration warnings are currently flagged.</div> : null}
              </>
            ) : <div className="text-sm text-muted-foreground">Integration details are not available for this user.</div>}
          </CardContent>
        </Card>
      </div>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between"><CardTitle>Revenue Recognition</CardTitle><Button onClick={onGenerateRevenueRecognition}>Generate</Button></CardHeader>
        <CardContent className="space-y-3">
          {project.revenueRecognitions.length === 0 ? <div className="text-sm text-muted-foreground">No revenue recognition records are available.</div> : null}
          {project.revenueRecognitions.map((recognition) => <div key={recognition.id} className="rounded-lg border p-4"><div className="flex items-center justify-between"><div className="font-medium">{recognition.recognitionPeriod}</div><Badge variant="outline">{recognition.status}</Badge></div><div className="mt-2 grid gap-3 text-sm md:grid-cols-4"><div>Revenue: {formatMoney(recognition.recognizedRevenue)}</div><div>Cost: {formatMoney(recognition.recognizedCost)}</div><div>Margin: {formatMoney(recognition.grossMargin)}</div><div>Cash: {formatMoney(recognition.cashCollected)}</div></div>{recognition.notes ? <div className="mt-2 text-sm text-muted-foreground">{recognition.notes}</div> : null}</div>)}
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between"><CardTitle>AI Insights</CardTitle><Button variant="outline" size="sm" onClick={onRefresh}><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button></CardHeader>
        <CardContent className="space-y-3">
          {aiInsights.map((item, index) => <div key={`${item.category}-${index}`} className="rounded-lg border p-4"><div className="flex items-center gap-2"><Badge variant="outline">{item.category}</Badge><Badge>{item.severity}</Badge></div><div className="mt-2 font-medium">{item.title}</div><div className="mt-1 text-sm text-muted-foreground">{item.recommendation}</div></div>)}
        </CardContent>
      </Card>
    </div>
  );
}
