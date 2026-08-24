'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Ban,
  CheckCircle2,
  Coins,
  HandCoins,
  Loader2,
  Mail,
  Send,
  XCircle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { useToast } from '@/hooks/use-toast';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });
const today = () => new Date().toISOString().slice(0, 10);

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm">{children}</dd>
    </div>
  );
}

/**
 * One charge against an employee — AST-3, decision D9.
 *
 * The order of the buttons is the order of the decision, and it is not the obvious one:
 *
 *   1. **Serve it on them.** Until this, the charge is a draft and the employee cannot see it at
 *      all — the API answers 404, not 403, so nothing shows somebody a draft being written about
 *      them.
 *   2. **They answer**, on their own screen. Accepting and disputing both send it on; the
 *      difference is recorded, not procedural.
 *   3. **Submit for approval.** If they never answered, submitting demands a reason — a charge can
 *      proceed over silence, but not silently.
 *   4. **Approve.** ⚠ The approver may LOWER the amount and never raise it. Raising it would be a
 *      new charge the employee never had a chance to answer.
 *   5. **Set a recovery plan**, which is a statement of intent to payroll. HR computes no payslip.
 *
 * ⚠ A served charge is **cancelled or waived, never deleted** — the delete answers 409 by design,
 * so that the record of it survives.
 */
export default function AssetSurchargeDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [dialog, setDialog] = useState<
    null | 'notify' | 'submit' | 'approve' | 'reject' | 'plan' | 'waive' | 'cancel' | 'recovery'
  >(null);
  const [text, setText] = useState('');
  const [approvedAmount, setApprovedAmount] = useState('');
  const [plan, setPlan] = useState({ recoveryMethod: '1', instalmentCount: '1', recoveryStartDate: today() });
  const [recovery, setRecovery] = useState({
    amount: '', recoveredOn: today(), method: '1', reference: '', notes: '',
  });

  const { data: s, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'surcharge', id],
    queryFn: () => assetRegisterService.getSurcharge(id),
    enabled: Boolean(id),
  });

  const close = () => { setDialog(null); setText(''); setApprovedAmount(''); };

  const run = useMutation({
    mutationFn: () => {
      switch (dialog) {
        case 'notify': return assetRegisterService.notifySurchargeEmployee(id);
        case 'submit': return assetRegisterService.submitSurcharge(id, text || undefined);
        case 'approve': return assetRegisterService.approveSurcharge(id, {
          approvedAmount: approvedAmount === '' ? null : Number(approvedAmount),
          approvalComments: text || null,
        });
        case 'reject': return assetRegisterService.rejectSurcharge(id, text);
        case 'plan': return assetRegisterService.setSurchargeRecoveryPlan(id, {
          recoveryMethod: Number(plan.recoveryMethod),
          instalmentCount: Number(plan.instalmentCount),
          recoveryStartDate: plan.recoveryStartDate || null,
        });
        case 'recovery': return assetRegisterService.recordSurchargeRecovery(id, {
          amount: Number(recovery.amount),
          recoveredOn: recovery.recoveredOn,
          method: Number(recovery.method),
          reference: recovery.reference || null,
          notes: recovery.notes || null,
        });
        case 'waive': return assetRegisterService.waiveSurcharge(id, text);
        case 'cancel': return assetRegisterService.cancelSurcharge(id, text);
        default: throw new Error('No action chosen');
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      toast({ title: 'Done' });
      close();
    },
    onError: (e: Error) =>
      toast({ title: 'The action was refused', description: e.message, variant: 'destructive' }),
  });

  if (isLoading || !s) {
    return (
      <div className="flex justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const live = !['Rejected', 'Waived', 'Cancelled', 'Recovered'].includes(s.status);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={s.surchargeNumber}
        description={`${s.assetName} · against ${s.employeeName}`}
        backHref="/hr/assets/surcharges"
        actions={
          <div className="flex flex-wrap gap-2">
            {s.status === 'Draft' && (
              <Button onClick={() => setDialog('notify')}>
                <Mail className="mr-2 h-4 w-4" /> Serve it on the employee
              </Button>
            )}
            {s.status === 'WithEmployee' && (
              <Button onClick={() => setDialog('submit')}>
                <Send className="mr-2 h-4 w-4" /> Send for approval
              </Button>
            )}
            {s.status === 'Submitted' && (
              <>
                <Button onClick={() => { setApprovedAmount(String(s.assessedAmount)); setDialog('approve'); }}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
                </Button>
                <Button variant="outline" onClick={() => setDialog('reject')}>
                  <XCircle className="mr-2 h-4 w-4" /> Reject
                </Button>
              </>
            )}
            {(s.status === 'Approved' || s.status === 'Recovering') && (
              <>
                <Button variant="outline" onClick={() => setDialog('plan')}>
                  <HandCoins className="mr-2 h-4 w-4" /> Recovery plan
                </Button>
                {/* ⚠ Without this the plan is only a statement of intent: `amountRecovered` never
                    moves, the charge never reaches Recovered, and it sits on the outstanding list
                    for ever. Payroll makes the deduction; HR records that it happened. */}
                <Button onClick={() => {
                  setRecovery((r) => ({
                    ...r,
                    amount: String(s.instalmentAmount ?? s.amountOutstanding),
                    method: String(
                      s.recoveryMethod === 'DirectPayment' ? 2
                        : s.recoveryMethod === 'ExitSettlement' ? 3 : 1),
                  }));
                  setDialog('recovery');
                }}>
                  <Coins className="mr-2 h-4 w-4" /> Record a recovery
                </Button>
              </>
            )}
            {live && (
              <>
                <Button variant="outline" onClick={() => setDialog('waive')}>Waive</Button>
                <Button variant="ghost" onClick={() => setDialog('cancel')}>
                  <Ban className="mr-2 h-4 w-4" /> Cancel
                </Button>
              </>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={s.statusName} />
        {s.isAwaitingEmployee && (
          <span className="text-sm text-amber-600 dark:text-amber-500">
            Served on {fmtDate(s.notifiedAt)} — waiting on their answer
          </span>
        )}
        {s.isDisputed && (
          <span className="text-sm text-amber-600 dark:text-amber-500">The employee disputes it</span>
        )}
        {s.isBelowAssessedCost && (
          <span className="text-sm text-muted-foreground">
            Set below what the damage actually cost
          </span>
        )}
      </div>

      <Card>
        <CardHeader><CardTitle className="text-base">The charge</CardTitle></CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-3">
            <Field label="Against">{s.employeeName}</Field>
            <Field label="Asset">
              <Link href={`/hr/assets/register/${s.assetId}`} className="hover:underline">
                {s.assetName} ({s.assetNumber})
              </Link>
            </Field>
            <Field label="Custody">
              <Link href={`/hr/assets/assignments/${s.assignmentId}`} className="hover:underline">
                {s.assignmentNumber}
              </Link>
            </Field>
            <Field label="Reason">{s.reasonName}</Field>
            <Field label="Assessed">{s.currencyCode} {fmtNum(s.assessedAmount)}</Field>
            <Field label="Outstanding">{s.currencyCode} {fmtNum(s.amountOutstanding)}</Field>
            <Field label="Raised by">{s.raisedByName ?? '—'}</Field>
            <Field label="Raised on">{fmtDate(s.raisedAt)}</Field>
            <Field label="Recovered so far">{s.currencyCode} {fmtNum(s.amountRecovered)}</Field>
            <div className="sm:col-span-3"><Field label="What it is for">{s.description}</Field></div>
          </dl>

          {/* The basis is kept because the charge is a DECISION about a person, not a fact about an
              asset — the recorded costs establish that it is permissible and seed its default. */}
          <div className="mt-4 border-t pt-4">
            <dl className="grid gap-4 sm:grid-cols-3">
              <Field label="Repair cost recorded">{fmtNum(s.basisRepairCost)}</Field>
              <Field label="Replacement cost recorded">{fmtNum(s.basisReplacementCost)}</Field>
            </dl>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">The employee&rsquo;s side</CardTitle></CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-3">
            <Field label="Served on them">{fmtDate(s.notifiedAt)}</Field>
            <Field label="They answered">{fmtDate(s.employeeRespondedAt)}</Field>
            <Field label="Their answer">{s.employeeResponseName}</Field>
            {s.employeeResponseComments && (
              <div className="sm:col-span-3">
                <Field label="What they said">{s.employeeResponseComments}</Field>
              </div>
            )}
            {s.proceededWithoutResponseReason && (
              <div className="sm:col-span-3">
                <Field label="Why it went ahead without an answer">
                  {s.proceededWithoutResponseReason}
                </Field>
              </div>
            )}
          </dl>
        </CardContent>
      </Card>

      {(s.approvalDate || s.rejectedDate || s.waivedAt || s.cancelledAt) && (
        <Card>
          <CardHeader><CardTitle className="text-base">The decision</CardTitle></CardHeader>
          <CardContent>
            <dl className="grid gap-4 sm:grid-cols-3">
              <Field label="Approved by">{s.approvedByName ?? '—'}</Field>
              <Field label="Approved on">{fmtDate(s.approvalDate)}</Field>
              <Field label="Waived by">{s.waivedByName ?? '—'}</Field>
              <div className="sm:col-span-3">
                <Field label="Reason given">
                  {s.approvalComments ?? s.rejectionReason ?? s.waiverReason ?? s.cancellationReason ?? '—'}
                </Field>
              </div>
            </dl>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Recovery</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid gap-4 sm:grid-cols-3">
            <Field label="Method">{s.recoveryMethodName ?? 'Not set'}</Field>
            <Field label="Instalments">{s.instalmentCount ?? '—'}</Field>
            <Field label="Each instalment">
              {s.instalmentAmount === null ? '—' : `${s.currencyCode} ${fmtNum(s.instalmentAmount)}`}
            </Field>
            <Field label="Starting">{fmtDate(s.recoveryStartDate)}</Field>
          </dl>
          <p className="mt-3 text-xs text-muted-foreground">
            A statement of intent to payroll, not a schedule HR runs. Payroll makes the deduction.
          </p>

          {s.recoveries.length > 0 && (
            <div className="mt-4">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Recovered on</TableHead>
                    <TableHead className="text-right">Amount</TableHead>
                    <TableHead>How</TableHead>
                    <TableHead>Reference</TableHead>
                    <TableHead>Recorded by</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {s.recoveries.map((r) => (
                    <TableRow key={r.id}>
                      <TableCell>{fmtDate(r.recoveredOn)}</TableCell>
                      <TableCell className="text-right">{fmtNum(r.amount)}</TableCell>
                      <TableCell>{r.methodName}</TableCell>
                      <TableCell>{r.reference ?? '—'}</TableCell>
                      <TableCell>{r.recordedByName ?? '—'}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={dialog !== null} onOpenChange={(o) => !o && close()}>
        <DialogContent>
          {dialog === 'notify' && (
            <>
              <DialogHeader>
                <DialogTitle>Serve this charge on {s.employeeName}</DialogTitle>
                <DialogDescription>
                  Until now it has been invisible to them. Once served, they may accept or dispute it
                  before it goes for approval.
                </DialogDescription>
              </DialogHeader>
            </>
          )}
          {dialog === 'submit' && (
            <>
              <DialogHeader>
                <DialogTitle>Send for approval</DialogTitle>
                <DialogDescription>
                  {s.employeeRespondedAt
                    ? 'The employee has answered. The charge goes to an approver.'
                    : 'The employee has not answered. Say why it should proceed anyway — a charge can go over silence, but not silently.'}
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-2">
                <Label>Why it proceeds{s.employeeRespondedAt ? '' : ' *'}</Label>
                <Textarea rows={3} value={text} onChange={(e) => setText(e.target.value)} />
              </div>
            </>
          )}
          {dialog === 'approve' && (
            <>
              <DialogHeader>
                <DialogTitle>Approve the charge</DialogTitle>
                <DialogDescription>
                  You may reduce the amount. You cannot raise it — that would be a charge the
                  employee never had the chance to answer.
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label>Amount approved</Label>
                  <Input type="number" step="0.01" value={approvedAmount}
                    onChange={(e) => setApprovedAmount(e.target.value)} />
                  <p className="text-xs text-muted-foreground">
                    Assessed at {s.currencyCode} {fmtNum(s.assessedAmount)}.
                  </p>
                </div>
                <div className="space-y-2">
                  <Label>Comments</Label>
                  <Textarea rows={2} value={text} onChange={(e) => setText(e.target.value)} />
                </div>
              </div>
            </>
          )}
          {(dialog === 'reject' || dialog === 'waive' || dialog === 'cancel') && (
            <>
              <DialogHeader>
                <DialogTitle>
                  {dialog === 'reject' ? 'Reject the charge'
                    : dialog === 'waive' ? 'Waive the charge' : 'Cancel the charge'}
                </DialogTitle>
                <DialogDescription>
                  {dialog === 'cancel'
                    ? 'The record survives — a served charge is cancelled, never deleted.'
                    : 'Say why. The employee sees this.'}
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-2">
                <Label>Reason *</Label>
                <Textarea rows={3} value={text} onChange={(e) => setText(e.target.value)} />
              </div>
            </>
          )}
          {dialog === 'recovery' && (
            <>
              <DialogHeader>
                <DialogTitle>Record a recovery</DialogTitle>
                <DialogDescription>
                  Money that has actually come back. {s.currencyCode} {fmtNum(s.amountOutstanding)}
                  {' '}is outstanding.
                </DialogDescription>
              </DialogHeader>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>Amount *</Label>
                  <Input type="number" step="0.01" value={recovery.amount}
                    onChange={(e) => setRecovery((r) => ({ ...r, amount: e.target.value }))} />
                </div>
                <div className="space-y-2">
                  <Label>Recovered on</Label>
                  <Input type="date" value={recovery.recoveredOn}
                    onChange={(e) => setRecovery((r) => ({ ...r, recoveredOn: e.target.value }))} />
                </div>
                <div className="space-y-2">
                  <Label>How</Label>
                  <Select value={recovery.method}
                    onValueChange={(v) => setRecovery((r) => ({ ...r, method: v }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="1">Payroll deduction</SelectItem>
                      <SelectItem value="2">Direct payment</SelectItem>
                      <SelectItem value="3">From the exit settlement</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Reference</Label>
                  <Input value={recovery.reference}
                    placeholder="Payslip, receipt, settlement line"
                    onChange={(e) => setRecovery((r) => ({ ...r, reference: e.target.value }))} />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label>Notes</Label>
                  <Textarea rows={2} value={recovery.notes}
                    onChange={(e) => setRecovery((r) => ({ ...r, notes: e.target.value }))} />
                </div>
              </div>
            </>
          )}
          {dialog === 'plan' && (
            <>
              <DialogHeader>
                <DialogTitle>Recovery plan</DialogTitle>
                <DialogDescription>
                  What HR declares to payroll. Nothing here deducts anything.
                </DialogDescription>
              </DialogHeader>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label>How</Label>
                  <Select value={plan.recoveryMethod}
                    onValueChange={(v) => setPlan((p) => ({ ...p, recoveryMethod: v }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="1">Payroll deduction</SelectItem>
                      <SelectItem value="2">Direct payment</SelectItem>
                      <SelectItem value="3">From the exit settlement</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Instalments</Label>
                  <Input type="number" min={1} value={plan.instalmentCount}
                    onChange={(e) => setPlan((p) => ({ ...p, instalmentCount: e.target.value }))} />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label>Starting</Label>
                  <Input type="date" value={plan.recoveryStartDate}
                    onChange={(e) => setPlan((p) => ({ ...p, recoveryStartDate: e.target.value }))} />
                </div>
              </div>
            </>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={close}>Cancel</Button>
            <Button
              onClick={() => run.mutate()}
              disabled={
                run.isPending
                || (['reject', 'waive', 'cancel'].includes(dialog ?? '') && !text.trim())
                || (dialog === 'submit' && !s.employeeRespondedAt && !text.trim())
                || (dialog === 'recovery' && !(Number(recovery.amount) > 0))
              }
            >
              {run.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
