'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Banknote, Loader2, ShieldCheck } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { FieldRow, NumberField, SelectField, TextareaField, TextField } from '@/components/hr/employee/tabs/fields';
import { useToast } from '@/hooks/use-toast';
import { medicalInsuranceService } from '@/services/hr/medical-reference.service';
import {
  INSURANCE_CLAIM_STATUS_OPTIONS,
  type MedicalInsuranceClaim,
  type MedicalInsuranceClaimStatus,
} from '@/types/hr/medical';

const money = (v?: number | null) =>
  v == null ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const orNull = (v?: string | null) => {
  const t = (v ?? '').trim();
  return t.length > 0 ? t : null;
};

const optionalMoney = z.preprocess(
  (v) => (v === '' || v === null || v === undefined ? null : Number(v)),
  z.number({ error: 'Enter an amount' }).min(0, 'Cannot be negative').nullable(),
);

const schema = z.object({
  policyId: z.string().min(1, 'Which policy is this claimed against?'),
  insuranceClaimNumber: z.string().max(100).optional(),
  claimedAmount: z.preprocess(
    (v) => (v === '' || v === null || v === undefined ? null : Number(v)),
    z.number({ error: 'Enter the amount claimed' }).min(0, 'Cannot be negative'),
  ),
  coPayAmount: optionalMoney,
  notes: z.string().max(2000).optional(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  policyId: '',
  insuranceClaimNumber: '',
  claimedAmount: 0,
  coPayAmount: null,
  notes: '',
};

const STATUS_TONE: Record<string, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  Submitted: 'bg-blue-100 text-blue-800',
  UnderReview: 'bg-amber-100 text-amber-800',
  AdditionalInfoRequired: 'bg-amber-100 text-amber-800',
  Approved: 'bg-emerald-100 text-emerald-800',
  PartiallyApproved: 'bg-emerald-100 text-emerald-800',
  Rejected: 'bg-red-100 text-red-800',
  PendingPayment: 'bg-blue-100 text-blue-800',
  Paid: 'bg-emerald-100 text-emerald-800',
  PartiallyPaid: 'bg-amber-100 text-amber-800',
  Appealed: 'bg-orange-100 text-orange-800',
  Cancelled: 'bg-slate-100 text-slate-500',
};

const SETTLED: MedicalInsuranceClaimStatus[] = ['Paid', 'Rejected', 'Cancelled'];

/**
 * What was claimed from an insurer against this medical expense claim.
 *
 * It lives on the expense claim's screen rather than the provider's because the expense claim is
 * what exists first and what a user is looking at when they decide to claim it back. The row joins
 * a POLICY to this claim.
 *
 * ⚠ **Status and payment are two separate acts.** `insurance-claims/{id}/status` moves the claim
 * through review and carries the approved amount; `insurance-claims/{id}/payment` records money
 * received and stamps a reference and date. Neither does the other's job, so setting `Paid` through
 * the status route leaves `paidAmount` and `paymentReference` empty — which is why this panel puts
 * payment on its own action and keeps `Paid` out of reach until it is used.
 *
 * ⚠ **`insuranceClaimNumber` was in the ledger's "no form can set" table.** It is the insurer's own
 * reference, and without it a rejection cannot be chased with the insurer at all.
 */
export function InsuranceClaimsPanel({
  expenseClaimId,
  employeeId,
  canWrite,
}: {
  expenseClaimId: string;
  employeeId?: string | null;
  canWrite: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const queryKey = ['hr', 'medical-claims', expenseClaimId, 'insurance-claims'];

  /**
   * The policies a claim may be filed against.
   *
   * Scoped to the employee when the screen knows who they are, because filing against another
   * employee's policy is not a mistake a dropdown should make possible. Falls back to the full
   * list only when the caller cannot say.
   */
  const { data: policies } = useQuery({
    queryKey: ['hr', 'medical-policies', employeeId ?? 'all'],
    queryFn: () =>
      employeeId
        ? medicalInsuranceService.getPoliciesForEmployee(employeeId)
        : medicalInsuranceService.getPolicies(),
    staleTime: 5 * 60 * 1000,
  });

  const [statusTarget, setStatusTarget] = useState<MedicalInsuranceClaim | null>(null);
  const [statusForm, setStatusForm] = useState({
    status: 'Submitted' as MedicalInsuranceClaimStatus,
    approvedAmount: '',
    rejectionReason: '',
    notes: '',
  });

  const [payTarget, setPayTarget] = useState<MedicalInsuranceClaim | null>(null);
  const [payForm, setPayForm] = useState({
    paidAmount: '',
    paymentReference: '',
    paymentDate: new Date().toISOString().slice(0, 10),
    notes: '',
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey });

  const fail = (title: string) => (e: any) =>
    toast({
      title,
      description: e?.response?.data?.detail ?? e?.response?.data?.message ?? e?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const updateStatus = useMutation({
    mutationFn: (claim: MedicalInsuranceClaim) =>
      medicalInsuranceService.updateInsuranceClaimStatus(claim.id, {
        status: statusForm.status,
        approvedAmount: statusForm.approvedAmount === '' ? null : Number(statusForm.approvedAmount),
        rejectionReason: orNull(statusForm.rejectionReason),
        notes: orNull(statusForm.notes),
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Claim status updated' });
      setStatusTarget(null);
    },
    onError: fail('Could not update the claim'),
  });

  const recordPayment = useMutation({
    mutationFn: (claim: MedicalInsuranceClaim) =>
      medicalInsuranceService.recordInsuranceClaimPayment(claim.id, {
        paidAmount: Number(payForm.paidAmount),
        paymentReference: payForm.paymentReference.trim(),
        paymentDate: payForm.paymentDate || null,
        notes: orNull(payForm.notes),
      }),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Payment recorded' });
      setPayTarget(null);
    },
    onError: fail('Could not record the payment'),
  });

  const policyOptions = (policies ?? []).map((p) => ({
    value: p.id,
    label: `${p.policyNumber} — ${p.planName} (${p.providerName})`,
  }));

  // Paid is reachable only through the payment action, which stamps the money and the reference.
  const statusChoices = INSURANCE_CLAIM_STATUS_OPTIONS.filter(
    (o) => o.value !== 'Paid' && o.value !== 'PartiallyPaid',
  );

  return (
    <>
      <ResourceCollectionTab<MedicalInsuranceClaim, Form>
        parentId={expenseClaimId}
        title="insurance claims"
        singular="insurance claim"
        queryKey={queryKey}
        readOnly={!canWrite}
        allowUpdate={false}
        dialogClassName="sm:max-w-[620px]"
        dialogHint="File this expense against an employee's insurance policy."
        emptyDescription="Nothing has been claimed from an insurer against this expense."
        list={(id) => medicalInsuranceService.getInsuranceClaimsForExpenseClaim(id)}
        create={(id, v) =>
          medicalInsuranceService.createInsuranceClaim({
            policyId: v.policyId,
            medicalExpenseClaimId: id,
            insuranceClaimNumber: (v.insuranceClaimNumber ?? '').trim(),
            claimedAmount: v.claimedAmount,
            coPayAmount: v.coPayAmount,
            notes: orNull(v.notes),
          })
        }
        // No general update route — status and payment are their own acts.
        update={() => Promise.reject(new Error('Use the status or payment action.'))}
        getId={(c) => c.id}
        actions={[
          {
            label: 'Update status',
            visible: (c) => !SETTLED.includes(c.status),
            run: async (c) => {
              setStatusForm({
                status: c.status,
                approvedAmount: c.approvedAmount == null ? '' : String(c.approvedAmount),
                rejectionReason: c.rejectionReason ?? '',
                notes: c.notes ?? '',
              });
              setStatusTarget(c);
            },
          },
          {
            label: 'Record payment',
            visible: (c) => c.status !== 'Cancelled' && c.status !== 'Rejected' && c.status !== 'Paid',
            run: async (c) => {
              setPayForm({
                // Default to what the insurer approved, since that is what they pay.
                paidAmount: String(c.approvedAmount ?? c.claimedAmount ?? ''),
                paymentReference: '',
                paymentDate: new Date().toISOString().slice(0, 10),
                notes: '',
              });
              setPayTarget(c);
            },
          },
        ]}
        columns={[
          {
            header: 'Insurer reference',
            cell: (c) => c.insuranceClaimNumber || <span className="text-muted-foreground">Not issued</span>,
          },
          { header: 'Policy', cell: (c) => c.policyNumber },
          { header: 'Submitted', cell: (c) => fmtDate(c.submissionDate) },
          { header: 'Claimed', cell: (c) => money(c.claimedAmount), className: 'text-right' },
          { header: 'Approved', cell: (c) => money(c.approvedAmount), className: 'text-right' },
          { header: 'Paid', cell: (c) => money(c.paidAmount), className: 'text-right' },
          {
            header: 'Status',
            cell: (c) => (
              <div className="flex flex-col gap-1">
                <Badge className={STATUS_TONE[c.status] ?? 'bg-slate-100 text-slate-700'}>
                  {c.statusName ?? c.status}
                </Badge>
                {c.rejectionReason && (
                  <span className="max-w-[220px] text-xs text-muted-foreground">{c.rejectionReason}</span>
                )}
              </div>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(c) => ({
          policyId: c.policyId,
          insuranceClaimNumber: c.insuranceClaimNumber,
          claimedAmount: c.claimedAmount,
          coPayAmount: c.coPayAmount ?? null,
          notes: c.notes ?? '',
        })}
        renderFields={(form) => (
          <>
            <SelectField
              form={form}
              name="policyId"
              label="Claim against policy"
              required
              placeholder={
                policyOptions.length
                  ? 'Choose the policy'
                  : employeeId
                    ? 'This employee has no insurance policy'
                    : 'No policies found'
              }
              options={policyOptions}
            />
            <FieldRow>
              <NumberField form={form} name="claimedAmount" label="Amount claimed" step="0.01" required />
              <NumberField
                form={form}
                name="coPayAmount"
                label="Co-pay"
                step="0.01"
                placeholder="What the employee bears"
              />
            </FieldRow>
            <TextField
              form={form}
              name="insuranceClaimNumber"
              label="Insurer's reference"
              placeholder="Leave blank until the insurer issues one"
            />
            <TextareaField form={form} name="notes" label="Notes" rows={3} />
            <p className="text-xs text-muted-foreground">
              The approved amount and payment are recorded later, through their own actions.
            </p>
          </>
        )}
      />

      {/* Status — carries the approved amount and the rejection reason, but never money received. */}
      <Dialog open={statusTarget !== null} onOpenChange={(o) => !o && setStatusTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <ShieldCheck className="h-4 w-4" />
              Update the insurer&rsquo;s decision
            </DialogTitle>
            <DialogDescription>
              {statusTarget
                ? `${statusTarget.insuranceClaimNumber || 'No reference'} · claimed ${money(statusTarget.claimedAmount)}`
                : ''}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="ic-status">Status</Label>
              <Select
                value={statusForm.status}
                onValueChange={(v) => setStatusForm({ ...statusForm, status: v as MedicalInsuranceClaimStatus })}
              >
                <SelectTrigger id="ic-status"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {statusChoices.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Paid and partially paid are set by recording a payment, which captures the amount
                and reference too.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="ic-approved">Amount approved</Label>
              <Input
                id="ic-approved"
                type="number"
                step="0.01"
                value={statusForm.approvedAmount}
                onChange={(e) => setStatusForm({ ...statusForm, approvedAmount: e.target.value })}
                placeholder="What the insurer agreed to cover"
              />
            </div>

            {(statusForm.status === 'Rejected' || statusForm.status === 'AdditionalInfoRequired') && (
              <div className="space-y-2">
                <Label htmlFor="ic-reason">
                  {statusForm.status === 'Rejected' ? 'Reason for rejection' : 'What the insurer asked for'}
                </Label>
                <Textarea
                  id="ic-reason"
                  rows={3}
                  value={statusForm.rejectionReason}
                  onChange={(e) => setStatusForm({ ...statusForm, rejectionReason: e.target.value })}
                />
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor="ic-notes">Notes</Label>
              <Textarea
                id="ic-notes"
                rows={2}
                value={statusForm.notes}
                onChange={(e) => setStatusForm({ ...statusForm, notes: e.target.value })}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setStatusTarget(null)} disabled={updateStatus.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => statusTarget && updateStatus.mutate(statusTarget)}
              disabled={updateStatus.isPending || !statusTarget}
            >
              {updateStatus.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Payment — the only route that records money received. */}
      <Dialog open={payTarget !== null} onOpenChange={(o) => !o && setPayTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <Banknote className="h-4 w-4" />
              Record what the insurer paid
            </DialogTitle>
            <DialogDescription>
              {payTarget
                ? `${payTarget.insuranceClaimNumber || 'No reference'} · approved ${money(payTarget.approvedAmount)}`
                : ''}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="ic-paid">Amount paid<span className="ml-0.5 text-red-500">*</span></Label>
                <Input
                  id="ic-paid"
                  type="number"
                  step="0.01"
                  value={payForm.paidAmount}
                  onChange={(e) => setPayForm({ ...payForm, paidAmount: e.target.value })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="ic-paid-date">Paid on</Label>
                <Input
                  id="ic-paid-date"
                  type="date"
                  value={payForm.paymentDate}
                  onChange={(e) => setPayForm({ ...payForm, paymentDate: e.target.value })}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="ic-ref">Payment reference<span className="ml-0.5 text-red-500">*</span></Label>
              <Input
                id="ic-ref"
                value={payForm.paymentReference}
                onChange={(e) => setPayForm({ ...payForm, paymentReference: e.target.value })}
                placeholder="The insurer's remittance reference"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="ic-pay-notes">Notes</Label>
              <Textarea
                id="ic-pay-notes"
                rows={2}
                value={payForm.notes}
                onChange={(e) => setPayForm({ ...payForm, notes: e.target.value })}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setPayTarget(null)} disabled={recordPayment.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => payTarget && recordPayment.mutate(payTarget)}
              disabled={
                recordPayment.isPending ||
                !payTarget ||
                !payForm.paymentReference.trim() ||
                payForm.paidAmount === ''
              }
            >
              {recordPayment.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record payment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
