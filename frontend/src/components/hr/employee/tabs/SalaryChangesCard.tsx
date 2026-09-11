'use client';

/**
 * Salary changes — the card that sits first on the Salary tab (round 3, lane S).
 *
 * A change of pay is raised here, approved on the workflow engine, and APPLIED by the server when
 * approved: the placement, the pay basis, the record figure and payroll's monthly basic. When the
 * tenant's policy setting requires approval, the direct writes lower down the tab are locked and
 * point here. This card shows the one live request (with the engine's actions) and the history.
 */
import { useMemo, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, RotateCw, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { SalaryScalePicker, type SalaryScaleSelection } from '@/components/hr/common/SalaryScalePicker';
import { salaryChangeRequestService } from '@/services/hr/salary-change-request.service';
import type { EmployeeDetail, PayBasis } from '@/types/hr/employee';
import {
  SALARY_CHANGE_STATUS_LABELS,
  type SalaryChangeKind,
  type SalaryChangeRequest,
} from '@/types/hr/salary-change-request';

const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });
const day = (iso?: string | null) => (iso ? iso.slice(0, 10) : '—');

const LIVE: SalaryChangeRequest['status'][] = ['Draft', 'PendingApproval', 'Approved', 'AwaitingPayrollEntry'];

function describe(r: SalaryChangeRequest): string {
  if (r.kind === 'Placement' || (r.kind === 'PayBasisSwitch' && r.proposedPayBasis === 'SalaryScale')) {
    const where = [r.proposedGradeCode, r.proposedLevelCode, r.proposedNotchNumber ? `notch ${r.proposedNotchNumber}` : null]
      .filter(Boolean)
      .join(' / ');
    const prefix = r.kind === 'PayBasisSwitch' ? 'Onto the scale: ' : 'Placement: ';
    return `${prefix}${where || 'grade'}${r.proposedNotchAmount ? ` (${money(r.proposedNotchAmount)})` : ''}`;
  }
  const prefix = r.kind === 'PayBasisSwitch' ? 'To a negotiated amount: ' : 'Negotiated amount: ';
  return `${prefix}${money(r.proposedAmount)} ${r.proposedCurrencyCode ?? ''}`.trim();
}

function statusVariant(s: SalaryChangeRequest['status']): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (s === 'Applied') return 'default';
  if (s === 'Rejected') return 'destructive';
  if (s === 'PendingApproval' || s === 'Approved' || s === 'AwaitingPayrollEntry') return 'secondary';
  return 'outline';
}

export function SalaryChangesCard({
  employee,
  canWrite,
  requiresApproval,
  onApplied,
}: {
  employee: EmployeeDetail;
  canWrite: boolean;
  /** The tenant's policy: when true the direct doors are locked and this card is the only way to change pay. */
  requiresApproval: boolean;
  /** Called after any change that could have moved the placement, basis or payroll figure. */
  onApplied: () => void | Promise<void>;
}) {
  const { toast } = useToast();
  const router = useRouter();
  const search = useSearchParams();
  const queryClient = useQueryClient();
  const [raiseOpen, setRaiseOpen] = useState(false);

  const list = useQuery({
    queryKey: ['hr', 'employees', employee.id, 'salary-change-requests'],
    queryFn: () => salaryChangeRequestService.list(employee.id),
  });

  const live = useMemo(() => (list.data ?? []).find((r) => LIVE.includes(r.status)) ?? null, [list.data]);
  const history = useMemo(() => (list.data ?? []).filter((r) => r.id !== live?.id), [list.data, live]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'employees', employee.id, 'salary-change-requests'] });
    await onApplied();
  };

  // The engine's actions for the ONE live request; the hook is keyed on that request's id.
  const workflow = useWorkflowRecord({
    entityType: 'HrEmployeeSalaryChangeRequest',
    entityId: live?.id ?? '',
    entityLabel: 'Salary change request',
    entityNumber: live ? describe(live) : undefined,
    status: live?.status ?? 'Draft',
    canSubmit: canWrite && (live?.status === 'Draft' || live?.status === 'Rejected'),
    canApproveReject: live?.status === 'PendingApproval',
    enabled: !!live,
    commands: {
      submit: live ? () => salaryChangeRequestService.submit(live.id) : undefined,
      approve: live ? () => salaryChangeRequestService.approve(live.id) : undefined,
      reject: live ? (ctx) => salaryChangeRequestService.reject(live.id, ctx.comments || null) : undefined,
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const recall = useMutation({
    mutationFn: () => salaryChangeRequestService.recall(live!.id),
    onSuccess: () => { toast({ title: 'Request recalled' }); void refresh(); },
    onError: (e: any) => toast({ title: 'Could not recall', description: e?.message, variant: 'destructive' }),
  });
  const retry = useMutation({
    mutationFn: () => salaryChangeRequestService.retryApply(live!.id),
    onSuccess: (r) => {
      toast({ title: r.status === 'Applied' ? 'Applied' : 'Still not applied', description: r.applyFailure ?? undefined });
      void refresh();
    },
    onError: (e: any) => toast({ title: 'Could not apply', description: e?.message, variant: 'destructive' }),
  });
  const remove = useMutation({
    mutationFn: () => salaryChangeRequestService.remove(live!.id),
    onSuccess: () => { toast({ title: 'Draft deleted' }); void refresh(); },
    onError: (e: any) => toast({ title: 'Could not delete', description: e?.message, variant: 'destructive' }),
  });

  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <CardTitle className="text-base">Salary changes</CardTitle>
            <CardDescription>
              {requiresApproval
                ? 'On this tenant a change of pay is raised here and applied when approved — the placement, the pay basis and payroll’s monthly basic together.'
                : 'Raise a change here to have it approved before it is applied; direct edits below are also allowed on this tenant.'}
            </CardDescription>
          </div>
          {canWrite && !live && employee.isOnPayroll && (
            <Button size="sm" onClick={() => setRaiseOpen(true)}>
              <Plus className="mr-2 h-4 w-4" /> Raise a request
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {list.isLoading ? (
          <div className="flex items-center gap-2 py-4 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Reading the requests…
          </div>
        ) : live ? (
          <div className="rounded-md border p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <div className="flex items-center gap-2">
                  <Badge variant={statusVariant(live.status)}>{SALARY_CHANGE_STATUS_LABELS[live.status]}</Badge>
                  <span className="text-sm font-medium">{describe(live)}</span>
                </div>
                <p className="mt-1 text-xs text-muted-foreground">
                  Effective {day(live.effectiveDate)} · raised by {live.requestedByName ?? '—'} on {day(live.createdAt)}
                  {live.currentAmount != null && ` · currently ${money(live.currentAmount)}`}
                  {live.resultingMonthlyBasicPay != null && ` → ${money(live.resultingMonthlyBasicPay)}`}
                </p>
                <p className="mt-1 whitespace-pre-wrap text-sm">{live.reason}</p>
                {live.rejectionReason && (
                  <p className="mt-1 text-sm text-destructive">Rejected: {live.rejectionReason}</p>
                )}
                {live.applyFailure && (
                  <p className="mt-1 text-sm text-amber-700 dark:text-amber-300">{live.applyFailure}</p>
                )}
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <WorkflowApprovalActions {...workflow.actionProps} />
                {canWrite && live.status === 'PendingApproval' && (
                  <Button size="sm" variant="outline" disabled={recall.isPending} onClick={() => recall.mutate()}>
                    Recall
                  </Button>
                )}
                {canWrite && (live.status === 'Approved' || live.status === 'AwaitingPayrollEntry') && (
                  <Button size="sm" variant="outline" disabled={retry.isPending} onClick={() => retry.mutate()}>
                    <RotateCw className="mr-2 h-3.5 w-3.5" /> Apply again
                  </Button>
                )}
                {canWrite && live.status === 'Draft' && (
                  <Button size="sm" variant="ghost" disabled={remove.isPending} onClick={() => remove.mutate()}>
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                )}
              </div>
            </div>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">No change of pay is in progress.</p>
        )}

        {history.length > 0 && (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Change</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Raised by</TableHead>
                  <TableHead className="text-right">Result</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {history.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="text-sm">{describe(r)}</TableCell>
                    <TableCell className="text-sm">{day(r.effectiveDate)}</TableCell>
                    <TableCell>
                      <Badge variant={statusVariant(r.status)}>{SALARY_CHANGE_STATUS_LABELS[r.status]}</Badge>
                    </TableCell>
                    <TableCell className="text-sm">{r.requestedByName ?? '—'}</TableCell>
                    <TableCell className="text-right text-sm">{money(r.resultingMonthlyBasicPay)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </CardContent>

      <RaiseDialog
        open={raiseOpen}
        onOpenChange={setRaiseOpen}
        employee={employee}
        sourceProposalId={search?.get('fromProposal') ?? null}
        onRaised={() => { setRaiseOpen(false); void refresh(); }}
      />
    </Card>
  );
}

/**
 * Two questions, not three kinds: "where on the scale" or "what negotiated amount". The KIND is
 * derived from the person's current basis — same basis is a Placement / NegotiatedAmount, the
 * other basis is a PayBasisSwitch carrying the matching figure.
 */
function RaiseDialog({
  open,
  onOpenChange,
  employee,
  sourceProposalId,
  onRaised,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  employee: EmployeeDetail;
  sourceProposalId: string | null;
  onRaised: () => void;
}) {
  const { toast } = useToast();
  const [target, setTarget] = useState<PayBasis>(employee.payBasis);
  const [scale, setScale] = useState<SalaryScaleSelection>({ gradeId: '', levelId: '', notchId: '' });
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('GHS');
  const [effectiveDate, setEffectiveDate] = useState(new Date().toISOString().slice(0, 10));
  const [reason, setReason] = useState(sourceProposalId ? 'Raised from an approved salary review proposal.' : '');

  const kind: SalaryChangeKind =
    target !== employee.payBasis ? 'PayBasisSwitch' : target === 'SalaryScale' ? 'Placement' : 'NegotiatedAmount';

  const create = useMutation({
    mutationFn: () =>
      salaryChangeRequestService.create({
        employeeId: employee.id,
        kind,
        proposedPayBasis: kind === 'PayBasisSwitch' ? target : null,
        proposedGradeId: target === 'SalaryScale' ? scale.gradeId || null : null,
        proposedLevelId: target === 'SalaryScale' ? scale.levelId || null : null,
        proposedNotchId: target === 'SalaryScale' ? scale.notchId || null : null,
        proposedAmount: target === 'Negotiated' ? Number(amount) || null : null,
        proposedCurrencyCode: target === 'Negotiated' ? currency.trim().toUpperCase() || 'GHS' : null,
        effectiveDate: new Date(effectiveDate).toISOString(),
        reason: reason.trim(),
        sourceProposalId,
      }),
    onSuccess: () => { toast({ title: 'Request raised', description: 'Submit it to send it for approval.' }); onRaised(); },
    onError: (e: any) => toast({ title: 'Could not raise the request', description: e?.message, variant: 'destructive' }),
  });

  const valid =
    reason.trim().length > 0 &&
    !!effectiveDate &&
    (target === 'SalaryScale' ? !!scale.gradeId : Number(amount) > 0);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle>Raise a salary change — {employee.fullName}</DialogTitle>
          <DialogDescription>
            Currently paid {employee.payBasis === 'SalaryScale' ? 'on the salary scale' : 'a negotiated amount'}.
            The change is applied — to HR and to payroll — only once it is approved.
          </DialogDescription>
        </DialogHeader>

        <div className="max-h-[60vh] space-y-4 overflow-y-auto py-2">
          <div className="space-y-1.5">
            <Label>What changes</Label>
            <div className="flex gap-2">
              <Button type="button" size="sm" variant={target === 'SalaryScale' ? 'default' : 'outline'} onClick={() => setTarget('SalaryScale')}>
                A place on the scale
              </Button>
              <Button type="button" size="sm" variant={target === 'Negotiated' ? 'default' : 'outline'} onClick={() => setTarget('Negotiated')}>
                A negotiated amount
              </Button>
            </div>
            {kind === 'PayBasisSwitch' && (
              <p className="text-xs text-muted-foreground">
                This switches the pay basis to {target === 'SalaryScale' ? 'the salary scale' : 'a negotiated amount'}.
              </p>
            )}
          </div>

          {target === 'SalaryScale' ? (
            <SalaryScalePicker value={scale} onChange={setScale} idPrefix="salary-change" gradeLabel="Grade" />
          ) : (
            <div className="grid gap-3 md:grid-cols-3">
              <div className="space-y-1.5 md:col-span-2">
                <Label htmlFor="sc-amount">Monthly amount</Label>
                <Input id="sc-amount" type="number" step="0.01" min={0} value={amount} onChange={(e) => setAmount(e.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="sc-currency">Currency</Label>
                <Input id="sc-currency" value={currency} maxLength={10} onChange={(e) => setCurrency(e.target.value)} />
              </div>
            </div>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="sc-effective">Effective date</Label>
            <Input id="sc-effective" type="date" value={effectiveDate} onChange={(e) => setEffectiveDate(e.target.value)} />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="sc-reason">Why</Label>
            <Textarea id="sc-reason" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="The approver reads this before the figure." />
          </div>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="button" disabled={!valid || create.isPending} onClick={() => create.mutate()}>
            {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Raise
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
