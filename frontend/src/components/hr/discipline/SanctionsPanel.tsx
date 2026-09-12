'use client';

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { disciplineSanctionService } from '@/services/hr/discipline.service';
import type {
  DisciplinaryCase, DisciplinaryFinePaymentStatus, DisciplinaryWarningType,
  EmployeeTerminationType,
} from '@/types/hr/discipline';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });
const toIso = (v: string) => (v ? new Date(`${v}T00:00:00`).toISOString() : null);
const toDay = (v?: string | null) => (v ? v.slice(0, 10) : '');

const WARNING_TYPES: DisciplinaryWarningType[] = ['Verbal', 'Written', 'Final'];
const PAYMENT_STATUSES: DisciplinaryFinePaymentStatus[] = ['Pending', 'PartiallyPaid', 'FullyPaid', 'Waived'];
const TERMINATION_TYPES: EmployeeTerminationType[] = [
  'InvoluntaryForCause', 'InvoluntaryPerformance', 'InvoluntaryRedundancy',
  'VoluntaryResignation', 'VoluntaryRetirement', 'MutualAgreement', 'ContractExpiry', 'Death',
];

const spaced = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');

/**
 * The sanctions on a disciplinary case: warning, suspension, fine and its payments, termination,
 * and the separation checklist that follows one.
 *
 * ⚠ **Read-only since slice 1, and this is what unblocked it.** The case screen carried a note
 * saying the sanctions "must stay read-only until the issuing-authority rule is in place". Two
 * things were true, and only one of them was what the note said:
 *
 * 1. **The authority rule capped by the ABSENCE of a role**, so a TenantAdmin who headed nothing
 *    held head-of-department authority over everybody, while a real head of department held none.
 *    It now resolves from the organisation (FR-HR-080).
 * 2. **Three of the four sanctions had no decision behind them.** A warning, a suspension and a
 *    fine were all accepted against a case nobody had decided; only termination was guarded. Every
 *    one of those entities says in its own summary that it is "created when the decision includes"
 *    that penalty, so an ungated sanction contradicted the model as designed.
 *
 * Both are fixed and proven by `hr-discipline/probe-sanctions.mjs` and `probe-authority-gate.mjs`.
 *
 * ⚠ **The server is the gate, and this panel only mirrors it.** Every action here is refused until
 * the decision is confirmed — including while it is merely PROPOSED, which is a state a screen can
 * easily mistake for decided. The controls disable themselves and say why rather than offering a
 * form that will be refused.
 */
export function SanctionsPanel({ detail }: { detail: DisciplinaryCase }) {
  const caseId = detail.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState<null | 'warning' | 'suspension' | 'fine' | 'payment' | 'termination' | 'separation'>(null);

  // The server accepts a sanction only from these states. `AwaitingDecision` — proposed but not
  // confirmed — is deliberately NOT among them.
  const decided = ['DecisionMade', 'UnderAppeal', 'Closed'].includes(detail.status);
  const blocked = decided
    ? null
    : detail.status === 'AwaitingDecision'
      ? 'The decision has been proposed and is waiting to be confirmed. A sanction cannot be recorded until it is.'
      : 'A sanction needs a confirmed decision behind it. Record the decision first, and have it confirmed.';

  const [warning, setWarning] = useState({ warningType: 'Verbal' as DisciplinaryWarningType, expiry: '', reference: '' });
  const [suspension, setSuspension] = useState({ start: '', end: '', withPay: false });
  const [fine, setFine] = useState({ amount: '', due: '' });
  const [payment, setPayment] = useState({ amount: '', date: toDay(new Date().toISOString()), status: 'PartiallyPaid' as DisciplinaryFinePaymentStatus });
  const [termination, setTermination] = useState({
    type: 'InvoluntaryForCause' as EmployeeTerminationType,
    eligible: false, eligibleDate: '', restrictions: '', notes: '',
  });
  const [separation, setSeparation] = useState({
    interviewDone: false, interviewDate: '', interviewNotes: '',
    equipmentReturned: false, equipmentDate: '', missing: '',
  });

  const done = async (title: string) => {
    await queryClient.invalidateQueries({ queryKey: ['discipline-case', caseId] });
    setOpen(null);
    toast({ title });
  };
  const failed = (title: string) => (e: any) =>
    toast({ variant: 'destructive', title, description: e?.body?.message ?? e?.body?.detail ?? e?.message });

  const saveWarning = useMutation({
    mutationFn: () => {
      const payload = {
        warningType: warning.warningType,
        warningExpiryDate: toIso(warning.expiry),
        warningLetterReference: warning.reference.trim() || null,
      };
      return detail.warning
        ? disciplineSanctionService.updateWarning(caseId, payload)
        : disciplineSanctionService.recordWarning(caseId, payload);
    },
    onSuccess: () => done(detail.warning ? 'Warning corrected' : 'Warning recorded'),
    onError: failed('The warning could not be saved'),
  });

  const saveSuspension = useMutation({
    mutationFn: () => {
      const payload = {
        suspensionStartDate: toIso(suspension.start),
        suspensionEndDate: toIso(suspension.end),
        suspensionWithPay: suspension.withPay,
      };
      return detail.suspension
        ? disciplineSanctionService.updateSuspension(caseId, payload)
        : disciplineSanctionService.recordSuspension(caseId, payload);
    },
    onSuccess: () => done(detail.suspension ? 'Suspension corrected' : 'Suspension recorded'),
    onError: failed('The suspension could not be saved'),
  });

  const saveFine = useMutation({
    mutationFn: () => disciplineSanctionService.recordFine(caseId, {
      fineAmount: Number(fine.amount),
      fineDueDate: toIso(fine.due),
    }),
    onSuccess: () => done('Fine recorded'),
    onError: failed('The fine could not be saved'),
  });

  const savePayment = useMutation({
    mutationFn: () => disciplineSanctionService.recordFinePayment(caseId, {
      amountPaid: Number(payment.amount),
      paymentDate: toIso(payment.date) ?? new Date().toISOString(),
      paymentStatus: payment.status,
    }),
    onSuccess: () => done('Payment recorded'),
    onError: failed('The payment could not be recorded'),
  });

  const saveTermination = useMutation({
    mutationFn: () => {
      const payload = {
        type: termination.type,
        isEligibleForRehire: termination.eligible,
        eligibleForRehireDate: termination.eligible ? toIso(termination.eligibleDate) : null,
        rehireRestrictions: termination.restrictions.trim() || null,
      };
      return detail.termination
        ? disciplineSanctionService.updateTermination(caseId, payload)
        : disciplineSanctionService.recordTermination(caseId, {
            ...payload, separationNotes: termination.notes.trim() || null,
          });
    },
    onSuccess: () => done(detail.termination ? 'Termination corrected' : 'Termination recorded'),
    onError: failed('The termination could not be saved'),
  });

  const saveSeparation = useMutation({
    mutationFn: () => (detail.separation
      ? disciplineSanctionService.updateSeparation(caseId, {
          exitInterviewCompleted: separation.interviewDone,
          exitInterviewDate: toIso(separation.interviewDate),
          exitInterviewNotes: separation.interviewNotes.trim() || null,
          equipmentReturned: separation.equipmentReturned,
          equipmentReturnedDate: toIso(separation.equipmentDate),
          missingEquipment: separation.missing.trim() || null,
        })
      : disciplineSanctionService.initiateSeparation(caseId, {})),
    onSuccess: () => done(detail.separation ? 'Separation checklist updated' : 'Separation checklist started'),
    onError: failed('The separation could not be saved'),
  });

  const openWarning = () => {
    if (detail.warning) setWarning({
      warningType: detail.warning.warningType,
      expiry: toDay(detail.warning.warningExpiryDate),
      reference: detail.warning.warningLetterReference ?? '',
    });
    setOpen('warning');
  };
  const openSuspension = () => {
    if (detail.suspension) setSuspension({
      start: toDay(detail.suspension.suspensionStartDate),
      end: toDay(detail.suspension.suspensionEndDate),
      withPay: detail.suspension.suspensionWithPay,
    });
    setOpen('suspension');
  };
  const openTermination = () => {
    if (detail.termination) setTermination({
      type: detail.termination.type,
      eligible: detail.termination.isEligibleForRehire,
      eligibleDate: toDay(detail.termination.eligibleForRehireDate),
      restrictions: detail.termination.rehireRestrictions ?? '',
      notes: '',
    });
    setOpen('termination');
  };
  const openSeparation = () => {
    if (detail.separation) setSeparation({
      interviewDone: detail.separation.exitInterviewCompleted,
      interviewDate: toDay(detail.separation.exitInterviewDate),
      interviewNotes: detail.separation.exitInterviewNotes ?? '',
      equipmentReturned: detail.separation.equipmentReturned,
      equipmentDate: toDay(detail.separation.equipmentReturnedDate),
      missing: detail.separation.missingEquipment ?? '',
    });
    setOpen('separation');
  };

  const Action = ({ existing, onClick }: { existing: boolean; onClick: () => void }) => (
    <Button size="sm" variant={existing ? 'ghost' : 'outline'} disabled={!decided} onClick={onClick}>
      {existing ? <Pencil className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}
      {existing ? 'Correct' : 'Record'}
    </Button>
  );

  const Field = ({ label, value }: { label: string; value: React.ReactNode }) => (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <div className="text-sm">{value ?? '—'}</div>
    </div>
  );

  return (
    <div className="space-y-4">
      {blocked && (
        <Alert>
          <AlertDescription>{blocked}</AlertDescription>
        </Alert>
      )}

      {/* ── warning ─────────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle>Warning</CardTitle>
          <Action existing={!!detail.warning} onClick={openWarning} />
        </CardHeader>
        <CardContent>
          {detail.warning ? (
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Type" value={<StatusBadge status={detail.warning.warningTypeName} />} />
              <Field label="Expires" value={fmtDate(detail.warning.warningExpiryDate)} />
              <Field label="Letter reference" value={detail.warning.warningLetterReference} />
            </div>
          ) : (
            <EmptyState title="No warning" description="No warning has been issued on this case." />
          )}
        </CardContent>
      </Card>

      {/* ── suspension ──────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle>Suspension</CardTitle>
          <Action existing={!!detail.suspension} onClick={openSuspension} />
        </CardHeader>
        <CardContent>
          {detail.suspension ? (
            <div className="grid gap-4 sm:grid-cols-4">
              <Field label="From" value={fmtDate(detail.suspension.suspensionStartDate)} />
              <Field label="To" value={fmtDate(detail.suspension.suspensionEndDate)} />
              <Field label="Pay" value={detail.suspension.suspensionWithPay ? 'With pay' : 'Without pay'} />
              {/* Computed by the server from the two dates — shown, never entered. */}
              <Field label="Days" value={detail.suspension.suspensionDays ?? '—'} />
            </div>
          ) : (
            <EmptyState title="No suspension" description="No suspension has been imposed on this case." />
          )}
        </CardContent>
      </Card>

      {/* ── fine ────────────────────────────────────────────────────────────── */}
      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle>Fine</CardTitle>
          <div className="flex gap-2">
            {detail.fine && (
              <Button size="sm" variant="outline" disabled={!decided}
                onClick={() => { setPayment((p) => ({ ...p, amount: '' })); setOpen('payment'); }}>
                Record a payment
              </Button>
            )}
            {!detail.fine && <Action existing={false} onClick={() => setOpen('fine')} />}
          </div>
        </CardHeader>
        <CardContent>
          {detail.fine ? (
            <div className="grid gap-4 sm:grid-cols-4">
              <Field label="Amount" value={money(detail.fine.fineAmount)} />
              <Field label="Paid" value={money(detail.fine.finePaidAmount)} />
              <Field label="Due" value={fmtDate(detail.fine.fineDueDate)} />
              <Field label="Status" value={detail.fine.finePaymentStatusName
                ? <StatusBadge status={detail.fine.finePaymentStatusName} /> : '—'} />
            </div>
          ) : (
            <EmptyState title="No fine" description="No fine has been imposed on this case." />
          )}
        </CardContent>
      </Card>

      {/* ── termination and separation ──────────────────────────────────────── */}
      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle>Termination</CardTitle>
          <Action existing={!!detail.termination} onClick={openTermination} />
        </CardHeader>
        <CardContent>
          {detail.termination ? (
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Type" value={detail.termination.typeName ?? spaced(String(detail.termination.type))} />
              <Field label="Eligible for rehire"
                value={detail.termination.isEligibleForRehire
                  ? `Yes${detail.termination.eligibleForRehireDate ? ` — from ${fmtDate(detail.termination.eligibleForRehireDate)}` : ''}`
                  : 'No'} />
              <Field label="Restrictions" value={detail.termination.rehireRestrictions} />
            </div>
          ) : (
            <EmptyState title="No termination" description="No termination has been recorded on this case." />
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex-row items-center justify-between space-y-0">
          <CardTitle>Separation checklist</CardTitle>
          {/* ⚠ Refused until a termination exists — the server says so, and so does this. */}
          <Button
            size="sm"
            variant={detail.separation ? 'ghost' : 'outline'}
            disabled={!decided || !detail.termination}
            title={!detail.termination ? 'Record the termination first' : undefined}
            onClick={openSeparation}
          >
            {detail.separation ? <Pencil className="mr-2 h-4 w-4" /> : <Plus className="mr-2 h-4 w-4" />}
            {detail.separation ? 'Update' : 'Start'}
          </Button>
        </CardHeader>
        <CardContent>
          {detail.separation ? (
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Exit interview"
                value={detail.separation.exitInterviewCompleted
                  ? `Held ${fmtDate(detail.separation.exitInterviewDate)}` : 'Outstanding'} />
              <Field label="Equipment"
                value={detail.separation.equipmentReturned
                  ? `Returned ${fmtDate(detail.separation.equipmentReturnedDate)}` : 'Outstanding'} />
              <Field label="Missing" value={detail.separation.missingEquipment} />
            </div>
          ) : (
            <EmptyState
              title="Not started"
              description={detail.termination
                ? 'Start the checklist to record the exit interview and what was returned.'
                : 'The checklist follows a termination — record that first.'}
            />
          )}
        </CardContent>
      </Card>

      {/* ═══ dialogs ══════════════════════════════════════════════════════════ */}

      <Dialog open={open === 'warning'} onOpenChange={(o) => !o && setOpen(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{detail.warning ? 'Correct the warning' : 'Record a warning'}</DialogTitle>
            <DialogDescription>
              One warning per case. The reasoning lives on the case decision, not here.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="w-type">Type</Label>
              <Select value={warning.warningType}
                onValueChange={(v) => setWarning((w) => ({ ...w, warningType: v as DisciplinaryWarningType }))}>
                <SelectTrigger id="w-type"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {WARNING_TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="w-expiry">Expires</Label>
              <Input id="w-expiry" type="date" value={warning.expiry}
                onChange={(e) => setWarning((w) => ({ ...w, expiry: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="w-ref">Letter reference</Label>
              <Input id="w-ref" value={warning.reference}
                onChange={(e) => setWarning((w) => ({ ...w, reference: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(null)}>Cancel</Button>
            <Button onClick={() => saveWarning.mutate()} disabled={saveWarning.isPending}>
              {saveWarning.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={open === 'suspension'} onOpenChange={(o) => !o && setOpen(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{detail.suspension ? 'Correct the suspension' : 'Record a suspension'}</DialogTitle>
            <DialogDescription>
              A suspension imposed as a penalty. Sending somebody home pending an investigation is a
              different act, and this record is not it.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="s-from">From</Label>
              <Input id="s-from" type="date" value={suspension.start}
                onChange={(e) => setSuspension((s) => ({ ...s, start: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="s-to">To</Label>
              <Input id="s-to" type="date" value={suspension.end}
                onChange={(e) => setSuspension((s) => ({ ...s, end: e.target.value }))} />
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Checkbox id="s-pay" checked={suspension.withPay}
              onCheckedChange={(c) => setSuspension((s) => ({ ...s, withPay: c === true }))} />
            <Label htmlFor="s-pay">With pay</Label>
          </div>
          <p className="text-xs text-muted-foreground">
            The number of days is worked out from the dates.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(null)}>Cancel</Button>
            <Button onClick={() => saveSuspension.mutate()} disabled={saveSuspension.isPending}>
              {saveSuspension.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={open === 'fine'} onOpenChange={(o) => !o && setOpen(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record a fine</DialogTitle>
            <DialogDescription>
              The amount and when it falls due. Payments against it are recorded separately.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="f-amount">Amount</Label>
              <Input id="f-amount" type="number" min={0.01} step="0.01" value={fine.amount}
                onChange={(e) => setFine((f) => ({ ...f, amount: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="f-due">Due</Label>
              <Input id="f-due" type="date" value={fine.due}
                onChange={(e) => setFine((f) => ({ ...f, due: e.target.value }))} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(null)}>Cancel</Button>
            <Button onClick={() => saveFine.mutate()}
              disabled={saveFine.isPending || !(Number(fine.amount) > 0)}>
              {saveFine.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={open === 'payment'} onOpenChange={(o) => !o && setOpen(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record a payment</DialogTitle>
            {/* ⚠ Payments accumulate on the server — 100 then 150.50 against a 250.50 fine leaves
                250.50 paid. So this asks what is being paid NOW, never the running total. */}
            <DialogDescription>
              What is being paid now, not the total to date — {money(detail.fine?.finePaidAmount)} of{' '}
              {money(detail.fine?.fineAmount)} is already recorded.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="p-amount">Amount paid now</Label>
              <Input id="p-amount" type="number" min={0.01} step="0.01" value={payment.amount}
                onChange={(e) => setPayment((p) => ({ ...p, amount: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="p-date">Date</Label>
              <Input id="p-date" type="date" value={payment.date}
                onChange={(e) => setPayment((p) => ({ ...p, date: e.target.value }))} />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="p-status">Status after this payment</Label>
            <Select value={payment.status}
              onValueChange={(v) => setPayment((p) => ({ ...p, status: v as DisciplinaryFinePaymentStatus }))}>
              <SelectTrigger id="p-status"><SelectValue /></SelectTrigger>
              <SelectContent>
                {PAYMENT_STATUSES.map((t) => <SelectItem key={t} value={t}>{spaced(t)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(null)}>Cancel</Button>
            <Button onClick={() => savePayment.mutate()}
              disabled={savePayment.isPending || !(Number(payment.amount) > 0)}>
              {savePayment.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={open === 'termination'} onOpenChange={(o) => !o && setOpen(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{detail.termination ? 'Correct the termination' : 'Record a termination'}</DialogTitle>
            <DialogDescription>
              Who signs this is decided by the action type&rsquo;s authority and the approval that
              confirmed it — it is not re-asserted here.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="t-type">Type</Label>
              <Select value={termination.type}
                onValueChange={(v) => setTermination((t) => ({ ...t, type: v as EmployeeTerminationType }))}>
                <SelectTrigger id="t-type"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {TERMINATION_TYPES.map((t) => <SelectItem key={t} value={t}>{spaced(t)}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox id="t-eligible" checked={termination.eligible}
                onCheckedChange={(c) => setTermination((t) => ({ ...t, eligible: c === true }))} />
              <Label htmlFor="t-eligible">Eligible for rehire</Label>
            </div>
            {termination.eligible && (
              <div className="space-y-2">
                <Label htmlFor="t-eligible-date">Eligible from</Label>
                <Input id="t-eligible-date" type="date" value={termination.eligibleDate}
                  onChange={(e) => setTermination((t) => ({ ...t, eligibleDate: e.target.value }))} />
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="t-restrictions">Restrictions</Label>
              <Textarea id="t-restrictions" rows={2} value={termination.restrictions}
                onChange={(e) => setTermination((t) => ({ ...t, restrictions: e.target.value }))} />
            </div>
            {!detail.termination && (
              <div className="space-y-2">
                <Label htmlFor="t-notes">Separation notes</Label>
                <Textarea id="t-notes" rows={2} value={termination.notes}
                  onChange={(e) => setTermination((t) => ({ ...t, notes: e.target.value }))} />
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(null)}>Cancel</Button>
            <Button onClick={() => saveTermination.mutate()} disabled={saveTermination.isPending}>
              {saveTermination.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={open === 'separation'} onOpenChange={(o) => !o && setOpen(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{detail.separation ? 'Update the checklist' : 'Start the separation checklist'}</DialogTitle>
            <DialogDescription>
              What this area owns is the record of the exit steps as they are ticked off. It does not
              compute what the leaver is owed, and it does not block anything on clearance.
            </DialogDescription>
          </DialogHeader>
          {detail.separation ? (
            <div className="space-y-4">
              <div className="flex items-center gap-2">
                <Checkbox id="sp-interview" checked={separation.interviewDone}
                  onCheckedChange={(c) => setSeparation((s) => ({ ...s, interviewDone: c === true }))} />
                <Label htmlFor="sp-interview">Exit interview held</Label>
              </div>
              {separation.interviewDone && (
                <>
                  <div className="space-y-2">
                    <Label htmlFor="sp-date">Interview date</Label>
                    <Input id="sp-date" type="date" value={separation.interviewDate}
                      onChange={(e) => setSeparation((s) => ({ ...s, interviewDate: e.target.value }))} />
                  </div>
                  <div className="space-y-2">
                    <Label htmlFor="sp-notes">Interview notes</Label>
                    <Textarea id="sp-notes" rows={3} value={separation.interviewNotes}
                      onChange={(e) => setSeparation((s) => ({ ...s, interviewNotes: e.target.value }))} />
                  </div>
                </>
              )}
              <div className="flex items-center gap-2">
                <Checkbox id="sp-equip" checked={separation.equipmentReturned}
                  onCheckedChange={(c) => setSeparation((s) => ({ ...s, equipmentReturned: c === true }))} />
                <Label htmlFor="sp-equip">Equipment returned</Label>
              </div>
              {separation.equipmentReturned ? (
                <div className="space-y-2">
                  <Label htmlFor="sp-equip-date">Returned on</Label>
                  <Input id="sp-equip-date" type="date" value={separation.equipmentDate}
                    onChange={(e) => setSeparation((s) => ({ ...s, equipmentDate: e.target.value }))} />
                </div>
              ) : (
                <div className="space-y-2">
                  <Label htmlFor="sp-missing">What is outstanding</Label>
                  <Textarea id="sp-missing" rows={2} value={separation.missing}
                    onChange={(e) => setSeparation((s) => ({ ...s, missing: e.target.value }))} />
                </div>
              )}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              This starts the checklist against the termination already recorded. The steps are then
              ticked off here as they happen.
            </p>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(null)}>Cancel</Button>
            <Button onClick={() => saveSeparation.mutate()} disabled={saveSeparation.isPending}>
              {saveSeparation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {detail.separation ? 'Save' : 'Start'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
