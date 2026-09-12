'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Send, Gavel, Banknote, Check, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import { travelFinanceService } from '@/services/hr/travel-finance.service';
import type {
  TravelClaimStatus,
  TravelExpenseCategory,
  TravelPaymentMethod,
} from '@/types/hr/travel-finance';

const EXPENSE_CATEGORIES: TravelExpenseCategory[] = [
  'Airfare', 'Accommodation', 'Meals', 'LocalTransport', 'TaxiRideshare', 'CarRental', 'Fuel',
  'VisaFees', 'Insurance', 'Communication', 'ConferenceFees', 'GiftsEntertainment', 'TipsGratuity',
  'Laundry', 'Medical', 'BaggageFees', 'Miscellaneous',
];

const PAYMENT_METHODS: TravelPaymentMethod[] = [
  'BankTransfer', 'PayrollOffset', 'Cash', 'Cheque', 'CorporateCard',
];

/**
 * The verdicts a reviewer can reach. Deliberately the real statuses rather than an approve/reject
 * pair: the claim's status is set outright by the review — it is NOT derived from its lines — and
 * `pay` refuses anything that is not `Approved`, so a screen offering only "approve" would leave
 * partially-approved claims unpayable and stuck.
 */
const REVIEW_OUTCOMES: { value: TravelClaimStatus; label: string; hint: string }[] = [
  { value: 'Approved', label: 'Approve', hint: 'Everything stands; the claim becomes payable.' },
  {
    value: 'PartiallyApproved',
    label: 'Partially approve',
    hint: 'Some lines were cut. Review those lines first, then record this.',
  },
  { value: 'Rejected', label: 'Reject', hint: 'Nothing is payable.' },
  {
    value: 'Returned',
    label: 'Return to the claimant',
    hint: 'Send it back for more detail or receipts.',
  },
  { value: 'UnderReview', label: 'Keep under review', hint: 'Still being looked at.' },
];

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const fmtMoney = (amount?: number | null, currency?: string) =>
  amount === null || amount === undefined
    ? '—'
    : new Intl.NumberFormat(undefined, {
        style: 'currency', currency: currency || 'GHS', currencyDisplay: 'code',
      }).format(amount);

const lineSchema = z.object({
  expenseCategory: z.enum(EXPENSE_CATEGORIES as [string, ...string[]]),
  expenseDate: z.string().min(1, 'Required'),
  description: z.string().max(500).optional(),
  merchantName: z.string().max(200).optional(),
  amountOriginal: z.coerce.number().min(0),
  currencyOriginal: z.string().min(1, 'Select a currency'),
  isPerDiem: z.boolean(),
});

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="py-1.5">
      <p className="text-xs text-muted-foreground">{label}</p>
      <div className="text-sm">{value ?? '—'}</div>
    </div>
  );
}

export default function TravelClaimDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [showLine, setShowLine] = useState(false);
  const [showReview, setShowReview] = useState(false);
  const [showPay, setShowPay] = useState(false);
  const [outcome, setOutcome] = useState<TravelClaimStatus>('Approved');
  const [notes, setNotes] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<TravelPaymentMethod>('BankTransfer');
  const [paymentReference, setPaymentReference] = useState('');

  const { data: claim, isLoading } = useQuery({
    queryKey: ['travel-claim', id],
    queryFn: () => travelFinanceService.getClaim(id),
  });

  const { data: currencies } = useQuery({
    queryKey: ['finance', 'currencies', 'active'],
    queryFn: () => financeDataService.getCurrencies({ isActive: true }),
  });

  // Needed only to anticipate the recovery in the pay dialog — the claim carries the advance's
  // number but not what is still outstanding on it.
  const { data: linkedAdvance } = useQuery({
    queryKey: ['travel-advance', claim?.travelAdvanceId],
    queryFn: () => travelFinanceService.getAdvance(claim?.travelAdvanceId as string),
    enabled: !!claim?.travelAdvanceId,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['travel-claim', id] });

  const lineForm = useForm<z.input<typeof lineSchema>>({
    resolver: zodResolver(lineSchema),
    defaultValues: {
      expenseCategory: 'Meals', expenseDate: '', amountOriginal: 0,
      currencyOriginal: claim?.currencyCode ?? 'GHS', isPerDiem: false,
    },
  });

  const addLine = useMutation({
    mutationFn: (values: z.input<typeof lineSchema>) => {
      const v = lineSchema.parse(values);
      return travelFinanceService.addClaimLine(id, {
        ...v,
        expenseCategory: v.expenseCategory as TravelExpenseCategory,
      });
    },
    onSuccess: async () => {
      toast({ title: 'Expense added' });
      setShowLine(false);
      lineForm.reset();
      await refresh();
    },
    // A currency Finance has no rate for on that date is refused here, with the reason.
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'The expense was refused', description: e.message }),
  });

  const submit = useMutation({
    mutationFn: () => travelFinanceService.submitClaim(id),
    onSuccess: async () => { toast({ title: 'Claim submitted' }); await refresh(); },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not submit', description: e.message }),
  });

  const review = useMutation({
    mutationFn: () =>
      travelFinanceService.reviewClaim(id, { newStatus: outcome, notes: notes.trim() || null }),
    onSuccess: async () => {
      toast({ title: 'Claim reviewed' });
      setShowReview(false);
      setNotes('');
      await refresh();
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not record the review', description: e.message }),
  });

  const reviewLine = useMutation({
    mutationFn: (v: { lineId: string; approve: boolean; amount?: number }) =>
      travelFinanceService.reviewClaimLine(v.lineId, {
        status: v.approve ? 'Approved' : 'Rejected',
        amountApproved: v.approve ? v.amount ?? null : 0,
        amountRejected: v.approve ? 0 : v.amount ?? null,
        rejectionReason: v.approve ? null : 'Rejected on review',
      }),
    onSuccess: async () => { toast({ title: 'Line reviewed' }); await refresh(); },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not review the line', description: e.message }),
  });

  const pay = useMutation({
    mutationFn: () =>
      travelFinanceService.payClaim(id, {
        paymentMethod,
        paymentReference: paymentReference.trim() || null,
      }),
    onSuccess: async () => {
      toast({ title: 'Claim paid' });
      setShowPay(false);
      setPaymentReference('');
      await refresh();
      await queryClient.invalidateQueries({ queryKey: ['travel-claims-register'] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not record payment', description: e.message }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (!claim) {
    return (
      <div className="p-6">
        <EmptyState title="Not found" description="This expense claim does not exist." />
      </div>
    );
  }

  const currencyOptions = (currencies ?? []).map((c) => ({
    value: c.currencyCode, label: `${c.currencyCode} — ${c.currencyName}`,
  }));

  const isDraft = claim.status === 'Draft' || claim.status === 'Returned';
  const isReviewable = claim.status === 'Submitted' || claim.status === 'UnderReview';
  const isPayable = claim.status === 'Approved' || claim.status === 'PartiallyApproved';

  // Mirrors `SettleLinkedAdvanceAsync`: recover min(what is outstanding, what is being paid), where
  // "what is being paid" is the approved total when there is one and the claimed total otherwise.
  // Duplicated deliberately so the two can be seen to agree — the appraisal-scoring lesson.
  const payableBeforeRecovery = claim.totalApproved > 0 ? claim.totalApproved : claim.totalClaimed;
  const advanceOutstanding = linkedAdvance?.unsettledAmount ?? 0;
  const anticipatedRecovery = Math.min(advanceOutstanding, payableBeforeRecovery);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={claim.claimNumber}
        description={`${claim.employeeName} · ${humanize(claim.claimTypeName)}${
          claim.requestNumber ? ` · trip ${claim.requestNumber}` : ''
        }`}
        backHref="/hr/travel/claims"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={humanize(claim.statusName)} />
            {isDraft && (
              <>
                <Button variant="outline" onClick={() => setShowLine(true)}>
                  <Plus className="mr-2 h-4 w-4" /> Add an expense
                </Button>
                <Button onClick={() => submit.mutate()} disabled={submit.isPending}>
                  <Send className="mr-2 h-4 w-4" /> Submit
                </Button>
              </>
            )}
            {isReviewable && (
              <Button onClick={() => setShowReview(true)}>
                <Gavel className="mr-2 h-4 w-4" /> Review
              </Button>
            )}
            {isPayable && (
              <Button onClick={() => setShowPay(true)}>
                <Banknote className="mr-2 h-4 w-4" /> Record payment
              </Button>
            )}
          </div>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">The claim</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-4">
          <InfoRow label="Claimed" value={fmtMoney(claim.totalClaimed, claim.currencyCode)} />
          <InfoRow label="Approved" value={fmtMoney(claim.totalApproved, claim.currencyCode)} />
          <InfoRow label="Rejected" value={fmtMoney(claim.totalRejected, claim.currencyCode)} />
          <InfoRow
            label="Advance recovered"
            value={fmtMoney(claim.advanceDeducted, claim.currencyCode)}
          />
          <InfoRow
            label="Net payable"
            value={
              <span className="font-semibold">
                {fmtMoney(claim.netPayable, claim.currencyCode)}
              </span>
            }
          />
          <InfoRow
            label="Against advance"
            value={
              claim.travelAdvanceNumber ? (
                <span>{claim.travelAdvanceNumber}</span>
              ) : (
                'None'
              )
            }
          />
          <InfoRow label="Submitted" value={fmtDateTime(claim.submittedAt)} />
          <InfoRow
            label="Reviewed"
            value={
              claim.financeReviewedByName
                ? `${claim.financeReviewedByName} · ${fmtDateTime(claim.financeReviewedAt)}`
                : '—'
            }
          />
          {claim.paidAt && (
            <>
              <InfoRow label="Paid" value={fmtDateTime(claim.paidAt)} />
              <InfoRow
                label="Payment"
                value={`${humanize(claim.paymentMethodName ?? '')}${
                  claim.paymentReference ? ` · ${claim.paymentReference}` : ''
                }`}
              />
            </>
          )}
          {claim.requestNumber && (
            <InfoRow
              label="Trip"
              value={
                <Link
                  href={`/hr/travel/${claim.staffTravelRequestId}`}
                  className="hover:underline"
                >
                  {claim.requestNumber}
                </Link>
              }
            />
          )}
        </CardContent>
      </Card>

      {claim.advanceDeducted > 0 && (
        <p className="text-xs text-muted-foreground">
          {fmtMoney(claim.advanceDeducted, claim.currencyCode)} was recovered from the linked advance
          when this claim was paid, so the net payable is what actually left the organisation.
        </p>
      )}
      {claim.travelAdvanceId && claim.advanceDeducted === 0 && !claim.paidAt && (
        <p className="text-xs text-muted-foreground">
          The linked advance has not been recovered yet — that happens when the claim is paid, so
          the net payable above still shows the full amount.
        </p>
      )}

      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
          <CardTitle className="text-base">Expenses</CardTitle>
          {isDraft && (
            <Button variant="outline" size="sm" onClick={() => setShowLine(true)}>
              <Plus className="mr-2 h-4 w-4" /> Add
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {claim.lines.length === 0 ? (
            <EmptyState title="No expenses" description="Nothing has been claimed on this yet." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead className="text-right">Spent</TableHead>
                  <TableHead className="text-right">In {claim.currencyCode}</TableHead>
                  <TableHead className="text-right">Approved</TableHead>
                  <TableHead>Status</TableHead>
                  {isReviewable && <TableHead className="w-24" />}
                </TableRow>
              </TableHeader>
              <TableBody>
                {claim.lines.map((l) => (
                  <TableRow key={l.id}>
                    <TableCell className="whitespace-nowrap">{fmtDate(l.expenseDate)}</TableCell>
                    <TableCell>{humanize(l.expenseCategoryName)}</TableCell>
                    <TableCell>
                      {l.description || '—'}
                      {l.merchantName && (
                        <span className="text-muted-foreground"> · {l.merchantName}</span>
                      )}
                      {l.isPerDiem && <span className="text-muted-foreground"> · per diem</span>}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(l.amountOriginal, l.currencyOriginal)}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(l.amountBaseCurrency, claim.currencyCode)}
                      {l.currencyOriginal !== claim.currencyCode && (
                        <span className="block text-xs text-muted-foreground">
                          @ {l.exchangeRate}
                        </span>
                      )}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(l.amountApproved, claim.currencyCode)}
                    </TableCell>
                    <TableCell><StatusBadge status={humanize(l.statusName)} /></TableCell>
                    {isReviewable && (
                      <TableCell>
                        <div className="flex gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label="Approve this expense"
                            disabled={reviewLine.isPending}
                            onClick={() => reviewLine.mutate({
                              lineId: l.id, approve: true, amount: l.amountBaseCurrency,
                            })}
                          >
                            <Check className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label="Reject this expense"
                            disabled={reviewLine.isPending}
                            onClick={() => reviewLine.mutate({
                              lineId: l.id, approve: false, amount: l.amountBaseCurrency,
                            })}
                          >
                            <X className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Add an expense */}
      <Dialog open={showLine} onOpenChange={setShowLine}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add an expense</DialogTitle>
            <DialogDescription>
              Record what was spent and in what currency. The rate and the converted amount come
              from Finance.
            </DialogDescription>
          </DialogHeader>
          <form
            id="line-form"
            className="space-y-4"
            onSubmit={lineForm.handleSubmit((v) => addLine.mutate(v))}
          >
            <FieldRow>
              <SelectField
                form={lineForm} name="expenseCategory" label="Category" required
                options={options(EXPENSE_CATEGORIES)}
              />
              <DateField form={lineForm} name="expenseDate" label="Date" required />
            </FieldRow>
            <TextField form={lineForm} name="description" label="Description" />
            <TextField form={lineForm} name="merchantName" label="Merchant" />
            <FieldRow>
              <NumberField form={lineForm} name="amountOriginal" label="Amount spent" required />
              <SelectField
                form={lineForm} name="currencyOriginal" label="Currency spent in" required
                options={currencyOptions}
              />
            </FieldRow>
            <p className="text-xs text-muted-foreground">
              A currency Finance holds no rate for on that date is refused, with the reason — the
              amount is never quietly treated as though it were already in {claim.currencyCode}.
            </p>
            <SwitchField form={lineForm} name="isPerDiem" label="This is a per-diem claim" />
          </form>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowLine(false)}>Cancel</Button>
            <Button type="submit" form="line-form" disabled={addLine.isPending}>
              {addLine.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Review */}
      <Dialog open={showReview} onOpenChange={setShowReview}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Review this claim</DialogTitle>
            <DialogDescription>
              You are recorded as the reviewer. Reviewing the individual expenses does not by itself
              settle the claim — this does.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            {REVIEW_OUTCOMES.map((o) => (
              <label
                key={o.value}
                className={`flex cursor-pointer items-start gap-3 rounded-md border p-3 ${
                  outcome === o.value ? 'border-primary' : ''
                }`}
              >
                <input
                  type="radio"
                  className="mt-1"
                  checked={outcome === o.value}
                  onChange={() => setOutcome(o.value)}
                />
                <span>
                  <span className="text-sm font-medium">{o.label}</span>
                  <span className="block text-xs text-muted-foreground">{o.hint}</span>
                </span>
              </label>
            ))}
            {/* Plain state, not the line form — these notes belong to the claim, not an expense. */}
            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="review-notes">Notes</label>
              <Textarea
                id="review-notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                rows={3}
                placeholder="Optional — kept on the claim."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowReview(false)}>Cancel</Button>
            <Button disabled={review.isPending} onClick={() => review.mutate()}>
              {review.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record the review
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Pay */}
      <Dialog open={showPay} onOpenChange={setShowPay}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record payment</DialogTitle>
            <DialogDescription>
              To {claim.employeeName}. The payment date is taken from the clock.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            {/*
              ⚠ Do NOT show `netPayable` here. The advance is recovered INSIDE the pay call, so
              until it completes netPayable still reads as the full approved amount — showing it
              would promise the payer a figure the act itself is about to change. The anticipated
              split is computed with the server's own rule, min(outstanding, approved), and is
              labelled as anticipated.
            */}
            <div className="rounded-md border p-3 text-sm">
              {advanceOutstanding > 0 ? (
                <>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Approved</span>
                    <span>{fmtMoney(payableBeforeRecovery, claim.currencyCode)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">
                      Less advance {claim.travelAdvanceNumber}
                    </span>
                    <span>−{fmtMoney(anticipatedRecovery, claim.currencyCode)}</span>
                  </div>
                  <div className="mt-1 flex justify-between border-t pt-1 font-medium">
                    <span>To pay</span>
                    <span>
                      {fmtMoney(payableBeforeRecovery - anticipatedRecovery, claim.currencyCode)}
                    </span>
                  </div>
                  <p className="mt-2 text-xs text-muted-foreground">
                    The advance is recovered as part of this payment; the figures are confirmed once
                    it is recorded.
                  </p>
                </>
              ) : (
                <div className="flex justify-between font-medium">
                  <span>To pay</span>
                  <span>{fmtMoney(claim.netPayable, claim.currencyCode)}</span>
                </div>
              )}
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="payment-method">Method</label>
              <select
                id="payment-method"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value as TravelPaymentMethod)}
              >
                {PAYMENT_METHODS.map((m) => (
                  <option key={m} value={m}>{humanize(m)}</option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="payment-reference">Reference</label>
              <input
                id="payment-reference"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={paymentReference}
                onChange={(e) => setPaymentReference(e.target.value)}
                placeholder="Transfer or cheque reference"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowPay(false)}>Cancel</Button>
            <Button disabled={pay.isPending} onClick={() => pay.mutate()}>
              {pay.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record payment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
