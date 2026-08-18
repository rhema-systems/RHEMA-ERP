'use client';

import { useCallback, useMemo, useState } from 'react';
import { AlertTriangle, BarChart3, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import {
  projectService,
  type QuantitySurveyCostReconciliationDto,
  type QuantitySurveyEstimateVersionDto,
} from '@/services/projectService';

type Props = { projectId: string };

const formatMoney = (value: number, currency: string) =>
  new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: currency || 'GHS',
  }).format(value || 0);

const estimateTypeLabel = (value: string) =>
  value === 'BudgetEstimate'
    ? 'Budget estimate'
    : value === 'TenderEstimate'
      ? 'Tender estimate'
      : 'Cost plan';

export function QuantitySurveyCostReconciliationDialog({ projectId }: Props) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission('quantity-survey.workspace.read');
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [approvedEstimates, setApprovedEstimates] = useState<
    QuantitySurveyEstimateVersionDto[]
  >([]);
  const [estimateVersionId, setEstimateVersionId] = useState('');
  const [report, setReport] =
    useState<QuantitySurveyCostReconciliationDto | null>(null);

  const loadOptions = useCallback(async () => {
    setLoading(true);
    try {
      const workspace =
        await projectService.getQuantitySurveyEstimateWorkspace(projectId);
      const approved = workspace.versions
        .filter((version) => version.status === 'Approved')
        .sort((left, right) =>
          left.estimateType === right.estimateType
            ? right.versionNumber - left.versionNumber
            : left.estimateType.localeCompare(right.estimateType)
        );
      setApprovedEstimates(approved);
      setEstimateVersionId((current) =>
        approved.some((version) => version.id === current)
          ? current
          : (approved[0]?.id ?? '')
      );
      setReport(null);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Failed to load QS estimates'
      );
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  const runReport = useCallback(async () => {
    if (!estimateVersionId) {
      toast.error('Select an approved estimate version.');
      return;
    }
    setLoading(true);
    try {
      setReport(
        await projectService.getQuantitySurveyCostReconciliation(
          projectId,
          estimateVersionId
        )
      );
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Failed to reconcile the approved estimate'
      );
    } finally {
      setLoading(false);
    }
  }, [estimateVersionId, projectId]);

  const summary = useMemo(
    () =>
      report
        ? ([
            ['Estimate', report.estimateAmount],
            ['Approved budget', report.approvedBudgetAmount],
            ['Committed', report.committedAmount],
            ['Certified', report.certifiedAmount],
            ['Actual', report.actualAmount],
            ['Forecast / EAC', report.forecastAmount],
            ['Budget vs estimate', report.budgetVarianceAmount],
            ['Budget vs forecast', report.forecastVarianceAmount],
          ] as const)
        : [],
    [report]
  );

  if (!canRead) return null;

  return (
    <Dialog
      open={open}
      onOpenChange={(nextOpen) => {
        setOpen(nextOpen);
        if (nextOpen) void loadOptions();
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline" className="gap-2">
          <BarChart3 className="h-4 w-4" />
          Cost reconciliation
        </Button>
      </DialogTrigger>
      <DialogContent className="max-h-[92vh] max-w-[96vw] overflow-y-auto xl:max-w-[1480px]">
        <DialogHeader>
          <DialogTitle>QS estimate cost reconciliation</DialogTitle>
        </DialogHeader>

        <div className="flex flex-wrap items-end gap-3 rounded-lg border bg-muted/20 p-3">
          <div className="min-w-[280px] flex-1 space-y-1.5">
            <Label>Approved estimate</Label>
            <Select
              value={estimateVersionId}
              onValueChange={(value) => {
                setEstimateVersionId(value);
                setReport(null);
              }}
              disabled={loading || approvedEstimates.length === 0}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select an approved estimate" />
              </SelectTrigger>
              <SelectContent>
                {approvedEstimates.map((version) => (
                  <SelectItem key={version.id} value={version.id}>
                    {estimateTypeLabel(version.estimateType)} · {version.name} ·
                    v{version.versionNumber}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <Button
            className="gap-2"
            onClick={() => void runReport()}
            disabled={loading || !estimateVersionId}
          >
            <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            Run reconciliation
          </Button>
        </div>

        {!loading && approvedEstimates.length === 0 ? (
          <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100">
            Approve a QS cost plan, tender estimate, or budget estimate before
            running this reconciliation.
          </div>
        ) : null}

        {report ? (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
              <div>
                <span className="font-semibold">{report.estimateName}</span>{' '}
                <span className="text-muted-foreground">
                  {estimateTypeLabel(report.estimateType)} v
                  {report.estimateVersionNumber} · {report.currencyCode}
                </span>
              </div>
              <div className="text-muted-foreground">
                Budget:{' '}
                {report.approvedBudgetRevisionName ?? 'Project approved budget'}{' '}
                · Forecast:{' '}
                {report.activeForecastVersionName ?? 'Package roll-up'}
              </div>
            </div>

            <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4 xl:grid-cols-8">
              {summary.map(([label, value]) => (
                <div key={label} className="rounded-lg border bg-card p-3">
                  <div className="text-xs text-muted-foreground">{label}</div>
                  <div className="mt-1 text-sm font-semibold tabular-nums">
                    {formatMoney(value, report.currencyCode)}
                  </div>
                </div>
              ))}
            </div>

            {report.warnings.length > 0 ? (
              <div className="space-y-1 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100">
                {report.warnings.map((warning) => (
                  <div key={warning} className="flex gap-2">
                    <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                    <span>{warning}</span>
                  </div>
                ))}
              </div>
            ) : null}

            <div className="overflow-x-auto rounded-lg border">
              <Table className="min-w-[1420px]">
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-[110px]">Line</TableHead>
                    <TableHead className="min-w-[220px]">Description</TableHead>
                    <TableHead>Mapping</TableHead>
                    <TableHead className="text-right">Estimate</TableHead>
                    <TableHead className="text-right">Budget</TableHead>
                    <TableHead className="text-right">Committed</TableHead>
                    <TableHead className="text-right">Certified</TableHead>
                    <TableHead className="text-right">Actual</TableHead>
                    <TableHead className="text-right">Forecast</TableHead>
                    <TableHead className="text-right">
                      Budget variance
                    </TableHead>
                    <TableHead className="text-right">
                      Forecast variance
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {report.lines.map((line) => (
                    <TableRow
                      key={
                        line.estimateLineId ?? `unallocated-${line.sequence}`
                      }
                      className={
                        line.mappingStatus === 'Unallocated'
                          ? 'bg-amber-50/70 font-medium dark:bg-amber-950/20'
                          : undefined
                      }
                    >
                      <TableCell>
                        <div className="font-medium">{line.lineNumber}</div>
                        <div className="text-xs text-muted-foreground">
                          {line.packageCode ?? line.packageName ?? 'Project'}
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>{line.description}</div>
                        {line.itemCode ? (
                          <div className="text-xs text-muted-foreground">
                            {line.itemCode}
                          </div>
                        ) : null}
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant={
                            line.mappingStatus === 'Direct'
                              ? 'default'
                              : 'secondary'
                          }
                        >
                          {line.mappingStatus}
                        </Badge>
                      </TableCell>
                      {[
                        line.estimateAmount,
                        line.approvedBudgetAmount,
                        line.committedAmount,
                        line.certifiedAmount,
                        line.actualAmount,
                        line.forecastAmount,
                        line.budgetVarianceAmount,
                        line.forecastVarianceAmount,
                      ].map((value, index) => (
                        <TableCell
                          key={index}
                          className="text-right tabular-nums"
                        >
                          {formatMoney(value, report.currencyCode)}
                        </TableCell>
                      ))}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </div>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}
