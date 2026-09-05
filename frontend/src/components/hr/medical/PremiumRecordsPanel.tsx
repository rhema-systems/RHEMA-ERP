'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Loader2 } from 'lucide-react';
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
import { DateField, FieldRow, NumberField, SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { useToast } from '@/hooks/use-toast';
import { medicalInsuranceService } from '@/services/hr/medical-reference.service';
import {
  PAYMENT_METHOD_OPTIONS,
  type MedicalInsurancePremiumRecord,
  type PaymentMethod,
} from '@/types/hr/medical';

const dateOnly = (v?: string | null) => (v ? v.slice(0, 10) : '');
const orNull = (v?: string | null) => {
  const t = (v ?? '').trim();
  return t.length > 0 ? t : null;
};
const money = (v?: number | null) =>
  v == null ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const num = (min = 0) =>
  z.preprocess(
    (v) => (v === '' || v === null || v === undefined ? null : Number(v)),
    z.number({ error: 'Enter an amount' }).min(min, `Must be ${min} or more`),
  );

const schema = z
  .object({
    planId: z.string().min(1, 'Which plan was billed?'),
    billingPeriodStart: z.string().min(1, 'When does the period start?'),
    billingPeriodEnd: z.string().min(1, 'When does it end?'),
    totalPremiumAmount: num(0),
    employerContribution: num(0),
    employeeContribution: num(0),
    coveredLivesCount: z.coerce.number().int().min(0),
    dueDate: z.string().optional(),
    notes: z.string().max(1000).optional(),
  })
  .refine((v) => v.billingPeriodEnd > v.billingPeriodStart, {
    message: 'The period must end after it starts',
    path: ['billingPeriodEnd'],
  });

type Form = z.infer<typeof schema>;

const empty: Form = {
  planId: '',
  billingPeriodStart: '',
  billingPeriodEnd: '',
  totalPremiumAmount: 0,
  employerContribution: 0,
  employeeContribution: 0,
  coveredLivesCount: 0,
  dueDate: '',
  notes: '',
};

const STATUS_TONE: Record<string, string> = {
  Pending: 'bg-amber-100 text-amber-800',
  Paid: 'bg-emerald-100 text-emerald-800',
  Overdue: 'bg-red-100 text-red-800',
  Waived: 'bg-slate-100 text-slate-600',
  Refunded: 'bg-slate-100 text-slate-600',
};

/**
 * What the employer was billed for cover over a period.
 *
 * ⚠ **`billingPeriodStart` and `billingPeriodEnd` are `DateOnly` on the wire** — `YYYY-MM-DD`, no
 * time component. `dueDate` beside them is a full DateTime, so one payload carries both shapes and
 * sending an ISO instant for the first two is a 400.
 *
 * ⚠ **Nothing computes the total from the two contributions.** It is what the insurer billed, and
 * the split is how it is shared — they need not agree, and the API checks nothing. The form shows
 * the difference so a mismatch is visible rather than silent. `totalPremiumAmount` and
 * `coveredLivesCount` were both in the closure ledger's "no form can set" table, which is exactly
 * how a premium of zero gets recorded.
 *
 * ⚠ **There is no update and no delete.** A premium record is a bill: it can be paid, and that is
 * a transition that stamps the reference, method and date. Correcting one is not offered by the API.
 */
export function PremiumRecordsPanel({
  providerId,
  canWrite,
}: {
  providerId: string;
  canWrite: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const queryKey = ['hr', 'medical-providers', providerId, 'premium-records'];

  const { data: plans } = useQuery({
    queryKey: ['hr', 'medical-providers', providerId, 'plans'],
    queryFn: () => medicalInsuranceService.getPlansByProvider(providerId),
  });

  const [paying, setPaying] = useState<MedicalInsurancePremiumRecord | null>(null);
  const [payment, setPayment] = useState({
    paymentMethod: 'BankTransfer' as PaymentMethod,
    paymentReference: '',
    paymentDate: new Date().toISOString().slice(0, 10),
    notes: '',
  });

  const recordPayment = useMutation({
    mutationFn: (record: MedicalInsurancePremiumRecord) =>
      medicalInsuranceService.recordPremiumPayment(record.id, {
        paymentMethod: payment.paymentMethod,
        paymentReference: payment.paymentReference.trim(),
        paymentDate: payment.paymentDate || null,
        notes: orNull(payment.notes),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: 'Payment recorded' });
      setPaying(null);
      setPayment({
        paymentMethod: 'BankTransfer',
        paymentReference: '',
        paymentDate: new Date().toISOString().slice(0, 10),
        notes: '',
      });
    },
    onError: (e: any) =>
      toast({
        title: 'Could not record the payment',
        description: e?.response?.data?.detail ?? e?.message ?? 'Please try again.',
        variant: 'destructive',
      }),
  });

  // ⚠ The plan type calls it `name`; the premium record calls the same thing `planName`.
  const planOptions = (plans ?? []).map((p) => ({ value: p.id, label: p.name }));

  return (
    <>
      <ResourceCollectionTab<MedicalInsurancePremiumRecord, Form>
        parentId={providerId}
        title="premium records"
        singular="premium record"
        queryKey={queryKey}
        readOnly={!canWrite}
        allowUpdate={false}
        dialogClassName="sm:max-w-[640px]"
        dialogHint="One billing period on one plan."
        emptyDescription="Nothing has been billed against this provider yet."
        list={(id) => medicalInsuranceService.getPremiumRecords(id)}
        create={(id, v) =>
          medicalInsuranceService.createPremiumRecord(id, {
            planId: v.planId,
            // ⚠ DateOnly — a plain YYYY-MM-DD, never an ISO instant.
            billingPeriodStart: v.billingPeriodStart,
            billingPeriodEnd: v.billingPeriodEnd,
            totalPremiumAmount: v.totalPremiumAmount,
            employerContribution: v.employerContribution,
            employeeContribution: v.employeeContribution,
            coveredLivesCount: v.coveredLivesCount,
            dueDate: orNull(v.dueDate),
            notes: orNull(v.notes),
          })
        }
        // No update route exists — a bill is not edited.
        update={() => Promise.reject(new Error('A premium record cannot be edited.'))}
        getId={(r) => r.id}
        actions={[
          {
            label: 'Record payment',
            visible: (r) => r.status !== 'Paid' && r.status !== 'Waived' && r.status !== 'Refunded',
            run: async (r) => { setPaying(r); },
          },
        ]}
        columns={[
          { header: 'Plan', cell: (r) => r.planName },
          {
            header: 'Period',
            cell: (r) => `${dateOnly(r.billingPeriodStart)} → ${dateOnly(r.billingPeriodEnd)}`,
          },
          { header: 'Lives', cell: (r) => r.coveredLivesCount, className: 'text-right' },
          { header: 'Total', cell: (r) => money(r.totalPremiumAmount), className: 'text-right' },
          {
            header: 'Employer / employee',
            cell: (r) => `${money(r.employerContribution)} / ${money(r.employeeContribution)}`,
            className: 'text-right',
          },
          { header: 'Due', cell: (r) => dateOnly(r.dueDate) || '—' },
          {
            header: 'Status',
            cell: (r) => (
              <div className="flex flex-col gap-1">
                <Badge className={STATUS_TONE[r.status] ?? 'bg-slate-100 text-slate-700'}>
                  {r.statusName ?? r.status}
                </Badge>
                {r.paymentReference && (
                  <span className="text-xs text-muted-foreground">{r.paymentReference}</span>
                )}
              </div>
            ),
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(r) => ({
          planId: r.planId,
          billingPeriodStart: dateOnly(r.billingPeriodStart),
          billingPeriodEnd: dateOnly(r.billingPeriodEnd),
          totalPremiumAmount: r.totalPremiumAmount,
          employerContribution: r.employerContribution,
          employeeContribution: r.employeeContribution,
          coveredLivesCount: r.coveredLivesCount,
          dueDate: dateOnly(r.dueDate),
          notes: r.notes ?? '',
        })}
        renderFields={(form) => {
          const total = Number(form.watch('totalPremiumAmount') ?? 0);
          const employer = Number(form.watch('employerContribution') ?? 0);
          const employee = Number(form.watch('employeeContribution') ?? 0);
          const split = employer + employee;
          const mismatch = total > 0 && Math.abs(split - total) > 0.005;

          return (
            <>
              <SelectField
                form={form}
                name="planId"
                label="Plan billed"
                required
                placeholder={planOptions.length ? 'Choose a plan' : 'This provider has no plans yet'}
                options={planOptions}
              />

              <FieldRow>
                <DateField form={form} name="billingPeriodStart" label="Period from" required />
                <DateField form={form} name="billingPeriodEnd" label="Period to" required />
              </FieldRow>

              <FieldRow>
                <NumberField
                  form={form}
                  name="totalPremiumAmount"
                  label="Total premium billed"
                  step="0.01"
                  required
                />
                <NumberField form={form} name="coveredLivesCount" label="Lives covered" required />
              </FieldRow>

              <FieldRow>
                <NumberField form={form} name="employerContribution" label="Employer share" step="0.01" />
                <NumberField form={form} name="employeeContribution" label="Employee share" step="0.01" />
              </FieldRow>

              {/* The API checks nothing here — showing the gap is the only thing that catches it. */}
              {mismatch && (
                <p className="text-xs text-amber-700">
                  The two shares add up to {money(split)}, not {money(total)}. That is allowed, but
                  worth a second look.
                </p>
              )}

              <DateField form={form} name="dueDate" label="Payment due" />
              <TextareaField form={form} name="notes" label="Notes" rows={2} />
            </>
          );
        }}
      />

      <Dialog open={paying !== null} onOpenChange={(o) => !o && setPaying(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record a premium payment</DialogTitle>
            <DialogDescription>
              {paying
                ? `${paying.planName} · ${dateOnly(paying.billingPeriodStart)} → ${dateOnly(paying.billingPeriodEnd)} · ${money(paying.totalPremiumAmount)}`
                : ''}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="pay-method">Method</Label>
                <Select
                  value={payment.paymentMethod}
                  onValueChange={(v) => setPayment({ ...payment, paymentMethod: v as PaymentMethod })}
                >
                  <SelectTrigger id="pay-method"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {PAYMENT_METHOD_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="pay-date">Paid on</Label>
                <Input
                  id="pay-date"
                  type="date"
                  value={payment.paymentDate}
                  onChange={(e) => setPayment({ ...payment, paymentDate: e.target.value })}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="pay-ref">Reference<span className="ml-0.5 text-red-500">*</span></Label>
              <Input
                id="pay-ref"
                value={payment.paymentReference}
                onChange={(e) => setPayment({ ...payment, paymentReference: e.target.value })}
                placeholder="Transfer or cheque reference"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="pay-notes">Notes</Label>
              <Textarea
                id="pay-notes"
                rows={2}
                value={payment.notes}
                onChange={(e) => setPayment({ ...payment, notes: e.target.value })}
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setPaying(null)} disabled={recordPayment.isPending}>
              Cancel
            </Button>
            <Button
              onClick={() => paying && recordPayment.mutate(paying)}
              disabled={recordPayment.isPending || !payment.paymentReference.trim() || !paying}
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
