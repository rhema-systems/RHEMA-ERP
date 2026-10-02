'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Send, Gavel, Banknote, Pencil, Scale, Paperclip, Undo2 } from 'lucide-react';
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
import { FinancePostingCard } from '@/components/hr/common/FinancePostingCard';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { CurrencyField } from '@/components/hr/common/CurrencyPicker';
import { TravelQueryError } from '@/components/hr/travel/TravelQueryError';
import { TravelReasonDialog } from '@/components/hr/travel/TravelReasonDialog';
import { financePostingSourceKey } from '@/components/hr/common/FinancePostingCard';
import { fmtTravelMoney as fmtMoney } from '@/components/hr/travel/travel-format';
import { useTravelAccess } from '@/components/hr/travel/useTravelAccess';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import { travelFinanceService } from '@/services/hr/travel-finance.service';
import type {
  StaffTravelExpenseClaimLine,
  TravelClaimStatus,
  TravelExpenseCategory,
  TravelPaymentMethod,
} from '@/types/hr/travel-finance';

const EXPENSE_CATEGORIES: TravelExpenseCategory[] = [
  'Airfare', 'Accommodation', 'Meals', 'LocalTransport', 'TaxiRideshare', 'CarRental', 'Fuel',
  'VisaFees', 'Insurance', 'Communication', 'ConferenceFees', 'GiftsEntertainment', 'TipsGratuity',
  'Laundry', 'Medical', 'BaggageFees', 'Miscellaneous',
];

// Not PayrollOffset (travel final closure, lane 3, D-10): payroll cannot receive travel claims yet, so a claim
// "paid" that way reached nobody. The server refuses it too.
const PAYMENT_METHODS: TravelPaymentMethod[] = ['BankTransfer', 'Cash', 'Cheque', 'CorporateCard'];

/**
 * The review's outcomes (lane 3). Approving approves what the expenses' reviews approved — the server records
 * Approved or Partially approved from them, and refuses while any expense is undecided. Rejecting and returning
 * need the reason, which the claimant sees.
 */
const REVIEW_OUTCOMES: { value: TravelClaimStatus; label: string; hint: string; needsNotes: boolean }[] = [
  {
    value: 'Approved',
    label: 'Approve',
    hint: 'As the expenses were reviewed: all of it approved, or partly approved where some was cut.',
    needsNotes: false,
  },
  { value: 'Rejected', label: 'Reject', hint: 'Nothing is payable. Say why.', needsNotes: true },
  {
    value: 'Returned',
    label: 'Return to the claimant',
    hint: 'Send it back for more detail or receipts. Say what is needed.',
    needsNotes: true,
  },
  { value: 'UnderReview', label: 'Mark under review', hint: 'Still being looked at.', needsNotes: false },
];

/** Advance cash still with the traveller. */
const CASH_OUT = ['Disbursed', 'PartiallySettled', 'Overdue'];

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');

const lineSchema = z.object({
  expenseCategory: z.enum(EXPENSE_CATEGORIES as [string, ...string[]]),
  expenseDate: z.string().min(1, 'Required'),
  description: z.string().max(500).optional(),
  merchantName: z.string().max(200).optional(),
  amountOriginal: z.coerce.number().positive('Enter the amount spent'),
  currencyOriginal: z.string().min(1, 'Select a currency'),
  receiptAttachmentId: z.string().optional(),
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
  // Lane 3, N8: the claim's controls render only for the travel desk.
  const access = useTravelAccess();
  const [lineDialog, setLineDialog] = useState<{ line: StaffTravelExpenseClaimLine | null } | null>(null);
  const [showReview, setShowReview] = useState(false);
  const [showPay, setShowPay] = useState(false);
  const [showVoid, setShowVoid] = useState(false);
  const [outcome, setOutcome] = useState<TravelClaimStatus>('Approved');
  const [notes, setNotes] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<TravelPaymentMethod>('BankTransfer');
  const [paymentReference, setPaymentReference] = useState('');
  const [waiver, setWaiver] = useState('');
  // The expense being decided: approve (in whole or in part) or reject, with the reason for any cut.
  const [lineReview, setLineReview] = useState<
    { line: StaffTravelExpenseClaimLine; approve: boolean; amount: string; reason: string } | null>(null);

  const { data: claim, isLoading, isError, error } = useQuery({
    queryKey: ['travel-claim', id],
    queryFn: () => travelFinanceService.getClaim(id),
  });

  // ⚠ The currency list is read through `api/hr/currencies` (inside CurrencyField). This page read
  // `api/finance/currencies`, which answers 403 without a Finance permission, so the HR desk could
  // add no expense in any currency (travel final closure, lane 0 — finding O-19).

  // The anticipated recovery in the pay dialog — the claim carries the advance's number, not what is still owed.
  const { data: linkedAdvance } = useQuery({
    queryKey: ['travel-advance', claim?.travelAdvanceId],
    queryFn: () => travelFinanceService.getAdvance(claim?.travelAdvanceId as string),
    enabled: !!claim?.travelAdvanceId,
  });

  // The trip's advances: cash the traveller holds that this claim does not name needs a recorded reason to pay
  // past (lane 3, O-2).
  const { data: tripAdvances } = useQuery({
    queryKey: ['travel-advances', claim?.staffTravelRequestId],
    queryFn: () => travelFinanceService.getAdvancesByRequest(claim?.staffTravelRequestId as string),
    enabled: !!claim?.staffTravelRequestId,
  });

  // The trip's attachments: a receipt is one of them, linked to its expense (lane 3, B3 and N6).
  const { data: attachments, isSuccess: attachmentsLoaded } = useQuery({
    queryKey: ['travel-attachments', claim?.staffTravelRequestId],
    queryFn: () => travelService.getAttachments(claim?.staffTravelRequestId as string),
    enabled: !!claim?.staffTravelRequestId,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['travel-claim', id] });

  const lineForm = useForm<z.input<typeof lineSchema>>({
    resolver: zodResolver(lineSchema),
    defaultValues: {
      expenseCategory: 'Meals', expenseDate: '', amountOriginal: 0,
      currencyOriginal: '', receiptAttachmentId: '', isPerDiem: false,
    },
  });

  // ⚠ The currency defaulted to `claim?.currencyCode ?? 'GHS'` in the form's defaults, which are
  // read once, on the first render — while the claim was still loading. So every expense started
  // in GHS whatever the claim's currency. The dialog now starts in the claim's own currency.
  // ⚠ It opens only once the trip's attachments are loaded: the receipt picker's SelectField blanks a value that
  // arrives before its options, which would unlink an edited expense's receipt on save.
  const openLineDialog = (line: StaffTravelExpenseClaimLine | null) => {
    lineForm.reset(line
      ? {
        expenseCategory: line.expenseCategory, expenseDate: String(line.expenseDate).slice(0, 10),
        description: line.description ?? '', merchantName: line.merchantName ?? '',
        amountOriginal: line.amountOriginal, currencyOriginal: line.currencyOriginal,
        receiptAttachmentId: line.receiptAttachmentId ?? '', isPerDiem: line.isPerDiem,
      }
      : {
        expenseCategory: 'Meals', expenseDate: '', amountOriginal: 0,
        currencyOriginal: claim?.currencyCode ?? '', receiptAttachmentId: '', isPerDiem: false,
      });
    setLineDialog({ line });
  };

  const saveLine = useMutation({
    mutationFn: (values: z.input<typeof lineSchema>) => {
      const v = lineSchema.parse(values);
      const editing = lineDialog?.line;
      const payload = {
        expenseCategory: v.expenseCategory as TravelExpenseCategory,
        expenseDate: v.expenseDate,
        description: v.description || null,
        merchantName: v.merchantName || null,
        amountOriginal: v.amountOriginal,
        currencyOriginal: v.currencyOriginal,
        receiptAttachmentId: v.receiptAttachmentId || null,
        isPerDiem: v.isPerDiem,
        perDiemRateId: editing?.perDiemRateId ?? null,
        policyLimit: editing?.policyLimit ?? null,
      };
      return editing
        ? travelFinanceService.updateClaimLine({ ...payload, id: editing.id })
        : travelFinanceService.addClaimLine(id, payload);
    },
    onSuccess: async () => {
      toast({ title: lineDialog?.line ? 'Expense changed' : 'Expense added' });
      setLineDialog(null);
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
    mutationFn: () => {
      if (!lineReview) throw new Error('No expense selected');
      return travelFinanceService.reviewClaimLine(lineReview.line.id, {
        status: lineReview.approve ? 'Approved' : 'Rejected',
        amountApproved: lineReview.approve ? Number(lineReview.amount) : null,
        rejectionReason: lineReview.reason.trim() || null,
      });
    },
    onSuccess: async () => { toast({ title: 'Expense reviewed' }); setLineReview(null); await refresh(); },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not review the expense', description: e.message }),
  });

  const pay = useMutation({
    mutationFn: () =>
      travelFinanceService.payClaim(id, {
        paymentMethod,
        paymentReference: paymentReference.trim() || null,
        advanceWaiverReason: waiver.trim() || null,
      }),
    onSuccess: async () => {
      toast({ title: 'Claim paid' });
      setShowPay(false);
      setPaymentReference('');
      setWaiver('');
      await refresh();
      await queryClient.invalidateQueries({ queryKey: ['travel-claims-register'] });
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', claim?.staffTravelRequestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not record payment', description: e.message }),
  });

  // Lane 3, T-39: the journal reversed, the advance settlement undone, the claim back to approved.
  const voidPayment = useMutation({
    mutationFn: (reason: string) => travelFinanceService.voidClaimPayment(id, { reason }),
    onSuccess: async () => {
      toast({ title: 'Payment voided', description: 'The claim is back to approved, to be paid again or not.' });
      await refresh();
      await queryClient.invalidateQueries({ queryKey: financePostingSourceKey(id) });
      await queryClient.invalidateQueries({ queryKey: ['travel-claims-register'] });
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', claim?.staffTravelRequestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not void the payment', description: e.message }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  if (isError && !claim) {
    return (
      <div className="p-6">
        <TravelQueryError error={error} what="this expense claim" />
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

  const canWrite = access.canWrite;
  const isDraft = claim.status === 'Draft' || claim.status === 'Returned';
  const isReviewable = claim.status === 'Submitted' || claim.status === 'UnderReview';
  const isPayable = claim.status === 'Approved' || claim.status === 'PartiallyApproved';

  const attachmentName = new Map((attachments ?? []).map((a) => [a.id, a.fileName]));
  const receiptOptions = (attachments ?? []).map((a) => ({
    value: a.id,
    label: `${a.fileName} (${humanize(a.attachmentTypeName)})`,
  }));

  // Mirrors `SettleLinkedAdvanceAsync`: recover min(what is outstanding, what is approved). Duplicated
  // deliberately so the two can be seen to agree — the appraisal-scoring lesson. An advance in another currency
  // is recovered at Finance's rate on the day of payment (D-15), which the screen does not know in advance.
  const payableBeforeRecovery = claim.totalApproved;
  const advanceOutstanding = linkedAdvance && CASH_OUT.includes(linkedAdvance.status)
    ? linkedAdvance.unsettledAmount : 0;
  const advanceForeign = !!linkedAdvance && linkedAdvance.currencyCode !== claim.currencyCode;
  const anticipatedRecovery = Math.min(advanceOutstanding, payableBeforeRecovery);
  const unnamedCashOut = (tripAdvances ?? []).filter((a) =>
    a.id !== claim.travelAdvanceId && a.employeeId === claim.employeeId
    && CASH_OUT.includes(a.status) && a.unsettledAmount > 0);

  const outcomeNeedsNotes = REVIEW_OUTCOMES.find((o) => o.value === outcome)?.needsNotes ?? false;
  const lineReviewAmount = Number(lineReview?.amount ?? 0);
  const lineReviewCut = !!lineReview && (!lineReview.approve || lineReviewAmount < lineReview.line.amountBaseCurrency);
  const lineReviewValid = !!lineReview
    && (!lineReview.approve || (lineReviewAmount > 0 && lineReviewAmount <= lineReview.line.amountBaseCurrency))
    && (!lineReviewCut || lineReview.reason.trim().length > 0);

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
            {canWrite && isDraft && (
              <>
                <Button variant="outline" onClick={() => openLineDialog(null)} disabled={!attachmentsLoaded}>
                  <Plus className="mr-2 h-4 w-4" /> Add an expense
                </Button>
                <Button onClick={() => submit.mutate()} disabled={submit.isPending}>
                  <Send className="mr-2 h-4 w-4" /> Submit
                </Button>
              </>
            )}
            {canWrite && isReviewable && (
              <Button onClick={() => { setOutcome('Approved'); setShowReview(true); }}>
                <Gavel className="mr-2 h-4 w-4" /> Review
              </Button>
            )}
            {canWrite && isPayable && (
              <Button onClick={() => setShowPay(true)}>
                <Banknote className="mr-2 h-4 w-4" /> Record payment
              </Button>
            )}
            {access.canAdmin && claim.status === 'Paid' && (
              <Button variant="outline" onClick={() => setShowVoid(true)}>
                <Undo2 className="mr-2 h-4 w-4" /> Void payment
              </Button>
            )}
          </div>
        }
      />

      {claim.reviewNotes && (
        <Card className={claim.status === 'Returned' || claim.status === 'Rejected' ? 'border-amber-500/60' : undefined}>
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">
              {claim.status === 'Returned' ? 'Returned to the claimant' : claim.status === 'Rejected' ? 'Rejected' : 'Reviewer’s notes'}
              {claim.financeReviewedByName ? ` — ${claim.financeReviewedByName}` : ''}
            </p>
            <p className="mt-1 whitespace-pre-wrap text-sm">{claim.reviewNotes}</p>
          </CardContent>
        </Card>
      )}

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
            value={claim.travelAdvanceNumber ? <span>{claim.travelAdvanceNumber}</span> : 'None'}
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
              <InfoRow
                label="Paid"
                value={`${fmtDateTime(claim.paidAt)}${claim.paidByName ? ` · ${claim.paidByName}` : ''}`}
              />
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
                <Link href={`/hr/travel/${claim.staffTravelRequestId}`} className="hover:underline">
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
      {claim.advanceWaiverReason && (
        <p className="text-xs text-muted-foreground">
          Paid in full past advance cash the traveller held on this trip: {claim.advanceWaiverReason}
        </p>
      )}
      {claim.paymentVoidedAt && (
        <Card className="border-amber-500/60">
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">
              {claim.status === 'Paid' ? 'An earlier payment was voided' : 'Payment voided'} on{' '}
              {fmtDateTime(claim.paymentVoidedAt)}
              {claim.paymentVoidedByName ? ` by ${claim.paymentVoidedByName}` : ''}
            </p>
            <p className="mt-1 whitespace-pre-wrap text-sm">{claim.paymentVoidReason}</p>
          </CardContent>
        </Card>
      )}

      {/* What Finance holds for this claim: recognition on approval, settlement on payment (lane 8). */}
      <FinancePostingCard sourceDocumentId={id} />

      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
          <CardTitle className="text-base">Expenses</CardTitle>
          {canWrite && isDraft && (
            <Button variant="outline" size="sm" onClick={() => openLineDialog(null)} disabled={!attachmentsLoaded}>
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
                  <TableHead>Receipt</TableHead>
                  <TableHead>Status</TableHead>
                  {canWrite && (isDraft || isReviewable) && <TableHead className="w-24" />}
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
                        <span className="block text-xs text-muted-foreground">@ {l.exchangeRate}</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(l.amountApproved, claim.currencyCode)}
                    </TableCell>
                    <TableCell className="max-w-[12rem] truncate text-xs">
                      {l.receiptAttachmentId ? (
                        <span className="inline-flex items-center gap-1">
                          <Paperclip className="h-3 w-3" />
                          {attachmentName.get(l.receiptAttachmentId) ?? 'Linked'}
                        </span>
                      ) : '—'}
                    </TableCell>
                    <TableCell>
                      <StatusBadge status={humanize(l.statusName)} />
                      {l.rejectionReason && (
                        <p className="mt-1 max-w-[14rem] text-xs text-muted-foreground">{l.rejectionReason}</p>
                      )}
                    </TableCell>
                    {canWrite && (isDraft || isReviewable) && (
                      <TableCell>
                        {isDraft && (
                          <Button
                            variant="ghost"
                            size="icon"
                            aria-label="Change this expense"
                            disabled={!attachmentsLoaded}
                            onClick={() => openLineDialog(l)}
                          >
                            <Pencil className="h-4 w-4" />
                          </Button>
                        )}
                        {isReviewable && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setLineReview({
                              line: l, approve: true, amount: String(l.amountBaseCurrency), reason: '',
                            })}
                          >
                            <Scale className="mr-1 h-4 w-4" /> Review
                          </Button>
                        )}
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Add or change an expense */}
      <Dialog open={!!lineDialog} onOpenChange={(v) => !v && setLineDialog(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{lineDialog?.line ? 'Change this expense' : 'Add an expense'}</DialogTitle>
            <DialogDescription>
              Record what was spent and in what currency. The rate and the converted amount come
              from Finance, for the date of the expense.
              {lineDialog?.line?.status && lineDialog.line.status !== 'Pending'
                ? ' This expense was reviewed; changing it sends it back to be reviewed again.'
                : ''}
            </DialogDescription>
          </DialogHeader>
          <form
            id="line-form"
            className="space-y-4"
            onSubmit={lineForm.handleSubmit((v) => saveLine.mutate(v))}
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
              <CurrencyField
                form={lineForm} name="currencyOriginal" label="Currency spent in" required
              />
            </FieldRow>
            <p className="text-xs text-muted-foreground">
              A currency Finance holds no rate for on that date is refused, with the reason — the
              amount is never quietly treated as though it were already in {claim.currencyCode}.
            </p>
            <SelectField
              form={lineForm}
              name="receiptAttachmentId"
              label="Receipt"
              options={receiptOptions}
              allowEmpty
              emptyLabel={receiptOptions.length ? 'No receipt' : 'No attachment on the trip yet'}
            />
            <p className="text-xs text-muted-foreground">
              A receipt is one of the trip&apos;s attachments — upload it on the trip&apos;s Attachments
              tab first. Under an approved travel policy, an expense above its receipt threshold
              needs one before the claim can be submitted (a per diem does not).
            </p>
            <SwitchField form={lineForm} name="isPerDiem" label="This is a per-diem claim" />
          </form>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLineDialog(null)}>Cancel</Button>
            <Button type="submit" form="line-form" disabled={saveLine.isPending}>
              {saveLine.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {lineDialog?.line ? 'Save' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Review one expense */}
      <Dialog open={!!lineReview} onOpenChange={(v) => !v && setLineReview(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Review this expense</DialogTitle>
            <DialogDescription>
              {lineReview && (
                <>
                  {humanize(lineReview.line.expenseCategoryName)} on {fmtDate(lineReview.line.expenseDate)} —{' '}
                  {fmtMoney(lineReview.line.amountBaseCurrency, claim.currencyCode)}.{' '}
                </>
              )}
              You are recorded as the reviewer, so you will not be the one who pays this claim.
            </DialogDescription>
          </DialogHeader>
          {lineReview && (
            <div className="space-y-3">
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant={lineReview.approve ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setLineReview({ ...lineReview, approve: true })}
                >
                  Approve
                </Button>
                <Button
                  type="button"
                  variant={!lineReview.approve ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setLineReview({ ...lineReview, approve: false })}
                >
                  Reject
                </Button>
              </div>
              {lineReview.approve && (
                <div className="space-y-2">
                  <label className="text-sm font-medium" htmlFor="line-approved">
                    Amount approved ({claim.currencyCode})
                  </label>
                  <input
                    id="line-approved"
                    type="number"
                    step="0.01"
                    min="0.01"
                    max={lineReview.line.amountBaseCurrency}
                    className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                    value={lineReview.amount}
                    onChange={(e) => setLineReview({ ...lineReview, amount: e.target.value })}
                  />
                  <p className="text-xs text-muted-foreground">
                    The whole expense, or less — whatever is not approved is rejected, with the reason below.
                  </p>
                </div>
              )}
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="line-reason">
                  {lineReviewCut ? 'Why it is not approved (the claimant sees this)' : 'Reason'}
                </label>
                <Textarea
                  id="line-reason"
                  rows={3}
                  maxLength={1000}
                  value={lineReview.reason}
                  onChange={(e) => setLineReview({ ...lineReview, reason: e.target.value })}
                  placeholder={lineReviewCut ? 'Required' : 'Not needed when the whole expense is approved'}
                />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setLineReview(null)}>Cancel</Button>
            <Button disabled={!lineReviewValid || reviewLine.isPending} onClick={() => reviewLine.mutate()}>
              {reviewLine.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Review the claim */}
      <Dialog open={showReview} onOpenChange={setShowReview}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Review this claim</DialogTitle>
            <DialogDescription>
              You are recorded as the reviewer, so you will not be the one who pays it. Decide each
              expense first — approving the claim approves what its expenses&apos; reviews approved.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            {REVIEW_OUTCOMES
              .filter((o) => o.value !== 'UnderReview' || claim.status === 'Submitted')
              .map((o) => (
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
              <label className="text-sm font-medium" htmlFor="review-notes">
                {outcomeNeedsNotes ? 'Why (the claimant sees this)' : 'Notes'}
              </label>
              <Textarea
                id="review-notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                rows={3}
                maxLength={2000}
                placeholder={outcomeNeedsNotes ? 'Required' : 'Optional — kept on the claim.'}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowReview(false)}>Cancel</Button>
            <Button
              disabled={review.isPending || (outcomeNeedsNotes && !notes.trim())}
              onClick={() => review.mutate()}
            >
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
              To {claim.employeeName}. The payment date is taken from the clock, and you are recorded
              as the officer who paid — it cannot be the claimant or anyone who reviewed the claim or
              its expenses.
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
              {advanceOutstanding > 0 && !advanceForeign ? (
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
                <>
                  <div className="flex justify-between font-medium">
                    <span>Approved</span>
                    <span>{fmtMoney(payableBeforeRecovery, claim.currencyCode)}</span>
                  </div>
                  {advanceOutstanding > 0 && advanceForeign && linkedAdvance && (
                    <p className="mt-2 text-xs text-muted-foreground">
                      Less what advance {claim.travelAdvanceNumber} still holds (
                      {fmtMoney(linkedAdvance.unsettledAmount, linkedAdvance.currencyCode)}), valued at
                      Finance&apos;s rate today — confirmed once the payment is recorded.
                    </p>
                  )}
                </>
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
            {unnamedCashOut.length > 0 && (
              <div className="space-y-2 rounded-md border border-amber-500/60 p-3">
                <p className="text-sm">
                  The traveller still holds advance{' '}
                  {unnamedCashOut.map((a) => `${a.advanceNumber} (${fmtMoney(a.unsettledAmount, a.currencyCode)})`).join(', ')}{' '}
                  on this trip, and this claim does not name it — paid as it stands, the claim pays in
                  full and the advance stays owed. Link the advance to the claim instead, or say why it
                  is paid in full.
                </p>
                <Textarea
                  rows={2}
                  maxLength={1000}
                  value={waiver}
                  onChange={(e) => setWaiver(e.target.value)}
                  placeholder="Why the claim is paid in full"
                />
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowPay(false)}>Cancel</Button>
            <Button
              disabled={pay.isPending || (unnamedCashOut.length > 0 && !waiver.trim())}
              onClick={() => pay.mutate()}
            >
              {pay.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record payment
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <TravelReasonDialog
        open={showVoid}
        onOpenChange={setShowVoid}
        title={`Void the payment of ${claim.claimNumber}`}
        description={
          `The payment of ${fmtMoney(claim.netPayable, claim.currencyCode)} is undone: its Finance journal, if one was ` +
          'posted, is reversed' +
          (claim.advanceDeducted > 0
            ? `, the ${fmtMoney(claim.advanceDeducted, claim.currencyCode)} it recovered goes back onto ${claim.travelAdvanceNumber ?? 'the advance'}`
            : '') +
          ', and the claim returns to approved, to be paid again or not. Neither the claimant nor the person who paid it ' +
          'can void it. The reason is kept on the claim and the trip.'
        }
        minLength={5}
        placeholder="At least five characters"
        confirmLabel="Void payment"
        destructive
        pending={voidPayment.isPending}
        onConfirm={(reason) => voidPayment.mutateAsync(reason)}
      />
    </div>
  );
}
