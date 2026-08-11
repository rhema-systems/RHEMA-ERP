'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Calculator,
  CheckCircle2,
  Eye,
  History,
  Send,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  quantitySurveyEscalationCalculationService,
  type QuantitySurveyEscalationCalculation,
  type QuantitySurveyEscalationImpactTargetType,
} from '@/services/quantity-survey-escalation-calculation.service';

interface Props {
  canManage: boolean;
  canApprove: boolean;
  canAudit: boolean;
}

type LifecycleAction = 'submit' | 'approve' | 'reject';

const currentMonth = () => new Date().toISOString().slice(0, 7);
const money = (currency: string, value: number) =>
  new Intl.NumberFormat('en-GH', {
    style: 'currency',
    currency: currency || 'GHS',
    maximumFractionDigits: 2,
  }).format(value);
const dateTime = (value: string) =>
  new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
const targetType = (
  value: QuantitySurveyEscalationCalculation['impactTargetType']
) =>
  value === 0 || value === 'PaymentCertificate'
    ? 'Payment certificate'
    : 'Final account';
const componentName = (value: string | number) =>
  ({ 0: 'Material', 1: 'Labour', 2: 'Plant', 3: 'Other' })[String(value)] ??
  String(value);

export function QuantitySurveyEscalationCalculationPanel({
  canManage,
  canApprove,
  canAudit,
}: Props) {
  const client = useQueryClient();
  const { toast } = useToast();
  const [formulaId, setFormulaId] = useState('');
  const [impactTargetType, setImpactTargetType] =
    useState<QuantitySurveyEscalationImpactTargetType>('PaymentCertificate');
  const [impactTargetId, setImpactTargetId] = useState('');
  const [indexMonth, setIndexMonth] = useState(currentMonth);
  const [reason, setReason] = useState('');
  const [clientRequestId, setClientRequestId] = useState(() =>
    crypto.randomUUID()
  );
  const [status, setStatus] = useState('all');
  const [detail, setDetail] =
    useState<QuantitySurveyEscalationCalculation | null>(null);
  const [lifecycle, setLifecycle] = useState<{
    value: QuantitySurveyEscalationCalculation;
    action: LifecycleAction;
  } | null>(null);
  const [lifecycleReason, setLifecycleReason] = useState('');
  const [review, setReview] =
    useState<QuantitySurveyEscalationCalculation | null>(null);
  const [reviewAmount, setReviewAmount] = useState('0');
  const [reviewReason, setReviewReason] = useState('');
  const [historyId, setHistoryId] = useState<string | null>(null);

  const lookups = useQuery({
    queryKey: ['quantity-survey-escalation-calculation-lookups'],
    queryFn: quantitySurveyEscalationCalculationService.lookups,
  });
  const targets = useQuery({
    queryKey: ['quantity-survey-escalation-impact-targets', formulaId],
    queryFn: () =>
      quantitySurveyEscalationCalculationService.impactTargets(formulaId),
    enabled: Boolean(formulaId),
  });
  const runs = useQuery({
    queryKey: ['quantity-survey-escalation-calculations', status],
    queryFn: () =>
      quantitySurveyEscalationCalculationService.list({
        status: status === 'all' ? undefined : status,
        page: 1,
        pageSize: 200,
      }),
  });
  const history = useQuery({
    queryKey: ['quantity-survey-escalation-calculation-history', historyId],
    queryFn: () => {
      if (!historyId) throw new Error('Select a calculation first.');
      return quantitySurveyEscalationCalculationService.history(historyId);
    },
    enabled: Boolean(historyId && canAudit),
  });

  const targetOptions = useMemo(
    () =>
      impactTargetType === 'PaymentCertificate'
        ? (targets.data?.paymentCertificates ?? [])
        : (targets.data?.finalAccounts ?? []),
    [impactTargetType, targets.data]
  );
  const refresh = async () => {
    await Promise.all([
      client.invalidateQueries({
        queryKey: ['quantity-survey-escalation-calculations'],
      }),
      client.invalidateQueries({
        queryKey: ['quantity-survey-escalation-calculation-history'],
      }),
      client.invalidateQueries({
        queryKey: ['quantity-survey-escalation-impact-targets'],
      }),
    ]);
  };

  const calculate = useMutation({
    mutationFn: () =>
      quantitySurveyEscalationCalculationService.calculate({
        clientRequestId,
        formulaId,
        impactTargetType,
        impactTargetId,
        currentIndexPeriod: `${indexMonth}-01`,
        reason,
      }),
    onSuccess: async (value) => {
      setDetail(value);
      setReason('');
      setClientRequestId(crypto.randomUUID());
      await refresh();
      toast({ title: 'Escalation calculation retained', variant: 'success' });
    },
    onError: (error) =>
      toast({
        title: 'Escalation calculation failed',
        description: error instanceof Error ? error.message : undefined,
        variant: 'destructive',
      }),
  });
  const lifecycleMutation = useMutation({
    mutationFn: () => {
      if (!lifecycle) throw new Error('Select a lifecycle action.');
      return quantitySurveyEscalationCalculationService.lifecycle(
        lifecycle.value.id,
        lifecycle.action,
        lifecycle.value.rowVersion,
        lifecycleReason
      );
    },
    onSuccess: async (value) => {
      setLifecycle(null);
      setLifecycleReason('');
      setDetail(value);
      await refresh();
      toast({ title: `Calculation ${value.status}`, variant: 'success' });
    },
    onError: (error) =>
      toast({
        title: 'Calculation action failed',
        description: error instanceof Error ? error.message : undefined,
        variant: 'destructive',
      }),
  });
  const reviewMutation = useMutation({
    mutationFn: () => {
      if (!review) throw new Error('Select a calculation to review.');
      return quantitySurveyEscalationCalculationService.reviewAdjustment(
        review.id,
        review.rowVersion,
        Number(reviewAmount),
        reviewReason
      );
    },
    onSuccess: async (value) => {
      setReview(null);
      setReviewAmount('0');
      setReviewReason('');
      setDetail(value);
      await refresh();
      toast({ title: 'Reviewer adjustment retained', variant: 'success' });
    },
    onError: (error) =>
      toast({
        title: 'Reviewer adjustment failed',
        description: error instanceof Error ? error.message : undefined,
        variant: 'destructive',
      }),
  });

  const canCalculate = Boolean(
    canManage && formulaId && impactTargetId && indexMonth && reason.trim()
  );

  return (
    <div className="space-y-3">
      {canManage ? (
        <Card>
          <CardContent className="grid gap-3 pt-4 lg:grid-cols-[minmax(220px,1.3fr)_180px_minmax(260px,1.5fr)_160px_minmax(240px,1.5fr)_auto] lg:items-end">
            <div className="space-y-1">
              <Label>Approved formula</Label>
              <Select
                value={formulaId || undefined}
                onValueChange={(value) => {
                  setFormulaId(value);
                  setImpactTargetId('');
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select formula" />
                </SelectTrigger>
                <SelectContent>
                  {(lookups.data?.formulas ?? []).map((value) => (
                    <SelectItem key={value.id} value={value.id}>
                      {value.label} · {value.group}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Impact type</Label>
              <Select
                value={impactTargetType}
                onValueChange={(
                  value: QuantitySurveyEscalationImpactTargetType
                ) => {
                  setImpactTargetType(value);
                  setImpactTargetId('');
                }}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="PaymentCertificate">
                    Payment certificate
                  </SelectItem>
                  <SelectItem value="FinalAccount">Final account</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Controlled impact target</Label>
              <Select
                value={impactTargetId || undefined}
                onValueChange={setImpactTargetId}
                disabled={!formulaId || targets.isLoading}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select target" />
                </SelectTrigger>
                <SelectContent>
                  {targetOptions.map((value) => (
                    <SelectItem key={value.id} value={value.id}>
                      {value.label} · {value.currencyCode}{' '}
                      {value.amount?.toLocaleString()} · {value.status}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Current index month</Label>
              <Input
                type="month"
                max={currentMonth()}
                value={indexMonth}
                onChange={(event) => setIndexMonth(event.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>Calculation reason</Label>
              <Input
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                maxLength={1000}
                placeholder="Contractual basis for this run"
              />
            </div>
            <Button
              disabled={!canCalculate || calculate.isPending}
              onClick={() => calculate.mutate()}
            >
              <Calculator className="mr-2 h-4 w-4" />
              {calculate.isPending ? 'Calculating…' : 'Calculate'}
            </Button>
          </CardContent>
        </Card>
      ) : null}

      <Card>
        <CardContent className="pt-4">
          <div className="mb-3 flex flex-wrap items-end justify-between gap-3">
            <div>
              <h3 className="font-semibold">Escalation calculation runs</h3>
              <p className="text-xs text-muted-foreground">
                {runs.data?.totalCount ?? 0} governed run(s); approved impacts
                await the later certificate or final-account application
                control.
              </p>
            </div>
            <div className="w-56 space-y-1">
              <Label>Status</Label>
              <Select value={status} onValueChange={setStatus}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All statuses</SelectItem>
                  {[
                    'Draft',
                    'PendingApproval',
                    'ApprovedPendingApplication',
                    'Rejected',
                  ].map((value) => (
                    <SelectItem key={value} value={value}>
                      {value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="overflow-x-auto rounded-md border">
            <table className="w-full text-sm">
              <thead className="bg-muted/60 text-left">
                <tr>
                  <th className="p-2">Run / project</th>
                  <th className="p-2">Target</th>
                  <th className="p-2">Base / revised</th>
                  <th className="p-2">Fluctuation / approved impact</th>
                  <th className="p-2">Status</th>
                  <th className="p-2 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {(runs.data?.items ?? []).map((value) => (
                  <tr key={value.id} className="border-t align-top">
                    <td className="p-2">
                      <button
                        className="font-medium text-primary hover:underline"
                        onClick={() => setDetail(value)}
                      >
                        {value.runReference}
                      </button>
                      <div className="text-xs text-muted-foreground">
                        {value.projectCode} · {value.contractNumber}
                      </div>
                    </td>
                    <td className="p-2">
                      {targetType(value.impactTargetType)}
                      <div className="text-xs text-muted-foreground">
                        {value.impactTargetReference}
                      </div>
                    </td>
                    <td className="p-2">
                      {money(value.currencyCode, value.baseRate)}
                      <div className="text-xs text-muted-foreground">
                        {money(value.currencyCode, value.revisedRate)}
                      </div>
                    </td>
                    <td className="p-2">
                      {money(
                        value.currencyCode,
                        value.calculatedFluctuationAmount
                      )}
                      <div className="font-medium">
                        {money(value.currencyCode, value.approvedImpactAmount)}
                      </div>
                    </td>
                    <td className="p-2">
                      <Badge
                        variant={
                          value.status === 'Rejected'
                            ? 'destructive'
                            : value.status === 'ApprovedPendingApplication'
                              ? 'secondary'
                              : 'default'
                        }
                      >
                        {value.status}
                      </Badge>
                      <div className="mt-1 text-xs text-muted-foreground">
                        {value.impactApplicationStatus}
                      </div>
                    </td>
                    <td className="p-2">
                      <div className="flex justify-end gap-1">
                        <Button
                          size="icon"
                          variant="ghost"
                          title="View"
                          onClick={() => setDetail(value)}
                        >
                          <Eye className="h-4 w-4" />
                        </Button>
                        {canAudit ? (
                          <Button
                            size="icon"
                            variant="ghost"
                            title="History"
                            onClick={() => setHistoryId(value.id)}
                          >
                            <History className="h-4 w-4" />
                          </Button>
                        ) : null}
                        {canManage &&
                        (value.status === 'Draft' ||
                          value.status === 'Rejected') ? (
                          <Button
                            size="icon"
                            variant="ghost"
                            title="Submit"
                            onClick={() =>
                              setLifecycle({ value, action: 'submit' })
                            }
                          >
                            <Send className="h-4 w-4" />
                          </Button>
                        ) : null}
                        {canApprove && value.status === 'PendingApproval' ? (
                          <Button
                            size="icon"
                            variant="ghost"
                            title="Review adjustment"
                            onClick={() => {
                              setReview(value);
                              setReviewAmount(
                                String(value.reviewerAdjustmentAmount)
                              );
                            }}
                          >
                            <Calculator className="h-4 w-4" />
                          </Button>
                        ) : null}
                        {canApprove && value.status === 'PendingApproval' ? (
                          <Button
                            size="icon"
                            variant="ghost"
                            title="Approve"
                            onClick={() =>
                              setLifecycle({ value, action: 'approve' })
                            }
                          >
                            <CheckCircle2 className="h-4 w-4" />
                          </Button>
                        ) : null}
                        {canApprove && value.status === 'PendingApproval' ? (
                          <Button
                            size="icon"
                            variant="ghost"
                            title="Reject"
                            onClick={() =>
                              setLifecycle({ value, action: 'reject' })
                            }
                          >
                            <XCircle className="h-4 w-4" />
                          </Button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                ))}
                {!runs.isLoading && !runs.data?.items.length ? (
                  <tr>
                    <td
                      colSpan={6}
                      className="p-8 text-center text-muted-foreground"
                    >
                      No calculation runs match this filter.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      <Dialog
        open={Boolean(lifecycle)}
        onOpenChange={(open) => !open && setLifecycle(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{lifecycle?.action} calculation</DialogTitle>
            <DialogDescription>
              The configured shared workflow, project scope, authority and
              maker-checker controls are revalidated.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Label>Reason</Label>
            <Textarea
              value={lifecycleReason}
              onChange={(event) => setLifecycleReason(event.target.value)}
              maxLength={1000}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLifecycle(null)}>
              Cancel
            </Button>
            <Button
              disabled={!lifecycleReason.trim() || lifecycleMutation.isPending}
              onClick={() => lifecycleMutation.mutate()}
            >
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(review)}
        onOpenChange={(open) => !open && setReview(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Independent reviewer adjustment</DialogTitle>
            <DialogDescription>
              Record only the reviewed adjustment. The original calculated
              fluctuation remains immutable and separately visible.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1">
              <Label>Adjustment amount</Label>
              <Input
                type="number"
                step="0.01"
                value={reviewAmount}
                onChange={(event) => setReviewAmount(event.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>Detailed reason</Label>
              <Input
                value={reviewReason}
                onChange={(event) => setReviewReason(event.target.value)}
                minLength={10}
                maxLength={1000}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReview(null)}>
              Cancel
            </Button>
            <Button
              disabled={
                !Number.isFinite(Number(reviewAmount)) ||
                reviewReason.trim().length < 10 ||
                reviewMutation.isPending
              }
              onClick={() => reviewMutation.mutate()}
            >
              Save review
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(detail)}
        onOpenChange={(open) => !open && setDetail(null)}
      >
        <DialogContent className="max-w-4xl">
          <DialogHeader>
            <DialogTitle>{detail?.runReference}</DialogTitle>
            <DialogDescription>
              {detail?.projectCode} · {detail?.contractNumber} ·{' '}
              {detail ? targetType(detail.impactTargetType) : ''}{' '}
              {detail?.impactTargetReference}
            </DialogDescription>
          </DialogHeader>
          {detail ? (
            <ScrollArea className="max-h-[65vh] pr-3">
              <div className="space-y-3 text-sm">
                <div className="grid gap-2 sm:grid-cols-4">
                  <div>
                    Base
                    <div className="font-semibold">
                      {money(detail.currencyCode, detail.baseRate)}
                    </div>
                  </div>
                  <div>
                    Revised
                    <div className="font-semibold">
                      {money(detail.currencyCode, detail.revisedRate)}
                    </div>
                  </div>
                  <div>
                    Calculated fluctuation
                    <div className="font-semibold">
                      {money(
                        detail.currencyCode,
                        detail.calculatedFluctuationAmount
                      )}
                    </div>
                  </div>
                  <div>
                    Approved impact
                    <div className="font-semibold">
                      {money(detail.currencyCode, detail.approvedImpactAmount)}
                    </div>
                  </div>
                </div>
                <div className="grid gap-2 md:grid-cols-2">
                  {detail.lines.map((line) => (
                    <div key={line.sequence} className="rounded-md border p-2">
                      <strong>{componentName(line.component)}</strong> ·{' '}
                      {line.coefficient}%
                      <div className="text-muted-foreground">
                        {line.indexFamilyCode}: {line.baseIndexValue} →{' '}
                        {line.currentIndexValue} · contribution{' '}
                        {line.weightedContribution}
                      </div>
                    </div>
                  ))}
                </div>
                <div className="break-all text-xs text-muted-foreground">
                  Integrity: {detail.snapshotHash}
                </div>
              </div>
            </ScrollArea>
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(historyId)}
        onOpenChange={(open) => !open && setHistoryId(null)}
      >
        <DialogContent className="max-w-3xl">
          <DialogHeader>
            <DialogTitle>Calculation history</DialogTitle>
            <DialogDescription>
              Immutable preparation, review and approval revisions.
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[60vh] pr-3">
            <div className="space-y-2">
              {(history.data ?? []).map((value) => (
                <div key={value.id} className="rounded-md border p-3 text-sm">
                  <div className="flex justify-between gap-3">
                    <strong>{value.action}</strong>
                    <span>{dateTime(value.createdAt)}</span>
                  </div>
                  <div>{value.actorName}</div>
                  <div className="text-muted-foreground">
                    {value.reason ?? 'No reason recorded'} ·{' '}
                    {value.correlationId}
                  </div>
                </div>
              ))}
              {!history.isLoading && !history.data?.length ? (
                <p className="text-muted-foreground">
                  No calculation revisions are available.
                </p>
              ) : null}
            </div>
          </ScrollArea>
        </DialogContent>
      </Dialog>
    </div>
  );
}
