'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Wallet, Receipt, Plus, Loader2, TrendingUp, HandCoins } from 'lucide-react';
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
import { Progress } from '@/components/ui/progress';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { FinancePostingInlineStatus } from '@/components/hr/common/FinancePostingCard';
import {
  DateField,
  FieldRow,
  NumberField,
  SelectField,
} from '@/components/hr/employee/tabs/fields';
import { CurrencyField } from '@/components/hr/common/CurrencyPicker';
import { useToast } from '@/hooks/use-toast';
import { travelFinanceService } from '@/services/hr/travel-finance.service';
import type { StaffTravelRequest } from '@/types/hr/travel';
import type { StaffTravelAdvanceSummary, StaffTravelBudget } from '@/types/hr/travel-finance';
import { TravelQueryError } from './TravelQueryError';
import { TravelReasonDialog } from './TravelReasonDialog';
import { useTravelAccess } from './useTravelAccess';
import { fmtTravelMoney as fmtMoney } from './travel-format';

const ADVANCE_TYPES = ['Cash', 'CorporateCardLoad', 'PettyCash', 'WireTransfer'] as const;

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

// ── Budget ───────────────────────────────────────────────────────────────────

const budgetSchema = z.object({
  budgetYear: z.coerce.number().min(2000).max(2100),
  approvedTotal: z.coerce.number().min(0),
  flightBudget: z.coerce.number().min(0),
  accommodationBudget: z.coerce.number().min(0),
  perDiemBudget: z.coerce.number().min(0),
  transportBudget: z.coerce.number().min(0),
  miscellaneousBudget: z.coerce.number().min(0),
});

const cents = (v: unknown) => Math.round((Number(v) || 0) * 100);

/** The trip's approved budget — its estimate on a trip approved before lane 2 set one. */
const tripBudgetOf = (request: StaffTravelRequest) => request.approvedBudget ?? request.estimatedTotalCost;

/**
 * Lane 3 (B10, O-9, T-22): the budget is in the trip's currency — the server sets it, so there is no currency
 * field; its total starts at the trip's approved budget and may not exceed it; its parts are all 0 or add up to
 * the total. Changing an approved budget withdraws its approval.
 */
function BudgetDialog({
  request, existing, open, onOpenChange,
}: {
  request: StaffTravelRequest;
  existing?: StaffTravelBudget | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const requestId = request.id;
  const currency = request.currencyCode;
  const tripBudget = tripBudgetOf(request);

  const form = useForm<z.input<typeof budgetSchema>>({ resolver: zodResolver(budgetSchema) });

  // Reset each time it opens: the form outlives the budget it was first given (a budget set, then edited).
  useEffect(() => {
    if (!open) return;
    form.reset({
      budgetYear: existing?.budgetYear ?? new Date(request.travelStartDate).getFullYear(),
      approvedTotal: existing?.approvedTotal ?? tripBudget,
      flightBudget: existing?.flightBudget ?? 0,
      accommodationBudget: existing?.accommodationBudget ?? 0,
      perDiemBudget: existing?.perDiemBudget ?? 0,
      transportBudget: existing?.transportBudget ?? 0,
      miscellaneousBudget: existing?.miscellaneousBudget ?? 0,
    });
  }, [open, existing, request.travelStartDate, tripBudget, form]);

  const save = useMutation({
    mutationFn: (values: z.input<typeof budgetSchema>) => {
      const v = budgetSchema.parse(values);
      return existing
        ? travelFinanceService.updateBudget({ ...v, id: existing.id })
        : travelFinanceService.createBudget({ ...v, staffTravelRequestId: requestId });
    },
    onSuccess: async () => {
      toast({ title: existing ? 'Budget updated' : 'Budget set' });
      onOpenChange(false);
      await queryClient.invalidateQueries({ queryKey: ['travel-budget', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not save the budget', description: e.message }),
  });

  // The same rules the server applies, so the dialog says what is wrong before Save rather than after it.
  const parts = ['flightBudget', 'accommodationBudget', 'perDiemBudget', 'transportBudget',
    'miscellaneousBudget'] as const;
  const allocated = parts.reduce((sum, k) => sum + cents(form.watch(k)), 0);
  const total = cents(form.watch('approvedTotal')) || cents(tripBudget);
  const overTrip = total > cents(tripBudget);
  const partsOff = allocated !== 0 && allocated !== total;
  const tripBudgetName = request.approvedBudget != null ? 'approved budget' : 'estimate';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{existing ? 'Edit the budget' : 'Set a budget'}</DialogTitle>
          <DialogDescription>
            In {currency}, the trip&apos;s currency, and within its {tripBudgetName} of{' '}
            {fmtMoney(tripBudget, currency)}. Committed and actual spend are worked out from the trip&apos;s
            bookings, advances and paid claims — there is nothing to enter for them.
            {existing?.approvedAt && ' Changing an approved budget withdraws its approval.'}
          </DialogDescription>
        </DialogHeader>
        <form id="budget-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <FieldRow>
            <NumberField form={form} name="approvedTotal" label={`Approved total (${currency})`} required />
            <NumberField form={form} name="budgetYear" label="Budget year" required />
          </FieldRow>

          <p className="text-sm font-medium">Allocation</p>
          <p className="text-xs text-muted-foreground">
            Leave every part at 0, or make them add up to the approved total.
          </p>
          <FieldRow>
            <NumberField form={form} name="flightBudget" label="Flights" />
            <NumberField form={form} name="accommodationBudget" label="Accommodation" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="perDiemBudget" label="Per diem" />
            <NumberField form={form} name="transportBudget" label="Transport" />
          </FieldRow>
          <NumberField form={form} name="miscellaneousBudget" label="Miscellaneous" />

          {overTrip && (
            <p className="rounded-md border border-destructive/50 p-3 text-sm text-destructive">
              The approved total is above the trip&apos;s {tripBudgetName} of {fmtMoney(tripBudget, currency)}.
            </p>
          )}
          {partsOff && (
            <p className="rounded-md border border-destructive/50 p-3 text-sm text-destructive">
              The parts add up to {fmtMoney(allocated / 100, currency)}, not the approved total of{' '}
              {fmtMoney(total / 100, currency)}.
            </p>
          )}
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="budget-form" disabled={save.isPending || overTrip || partsOff}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Advances ─────────────────────────────────────────────────────────────────

const advanceSchema = z.object({
  requestedAmount: z.coerce.number().positive('Enter the amount asked for'),
  currencyCode: z.string().min(1, 'Select a currency'),
  advanceType: z.enum(ADVANCE_TYPES),
  settlementDeadline: z.string().optional(),
});

/** The traveller is the trip's — the server sets it (lane 3, B3), so the dialog sends none. */
function AdvanceDialog({
  requestId, open, onOpenChange, defaultCurrency,
}: {
  requestId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  defaultCurrency: string;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const form = useForm<z.input<typeof advanceSchema>>({
    resolver: zodResolver(advanceSchema),
    defaultValues: { requestedAmount: 0, currencyCode: defaultCurrency, advanceType: 'Cash' },
  });

  const save = useMutation({
    mutationFn: (values: z.input<typeof advanceSchema>) => {
      const v = advanceSchema.parse(values);
      return travelFinanceService.createAdvance({
        ...v,
        staffTravelRequestId: requestId,
        settlementDeadline: v.settlementDeadline || null,
      });
    },
    onSuccess: async () => {
      toast({ title: 'Advance requested' });
      onOpenChange(false);
      form.reset();
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not request the advance', description: e.message }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Request a travel advance</DialogTitle>
          <DialogDescription>
            Approving and paying out are separate steps by different officers — whoever approves an
            advance cannot also pay it out, and nobody decides their own. Nothing is owed until it is
            paid out.
          </DialogDescription>
        </DialogHeader>
        <form id="advance-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <FieldRow>
            <NumberField form={form} name="requestedAmount" label="Amount requested" required />
            <CurrencyField form={form} name="currencyCode" label="Currency" required />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form} name="advanceType" label="Type" required options={options(ADVANCE_TYPES)}
            />
            <DateField form={form} name="settlementDeadline" label="Settle by" />
          </FieldRow>
          <p className="text-xs text-muted-foreground">
            The settlement deadline drives the chase list: an advance not settled by then is marked
            overdue, appears on overdue settlements and in the reminder sweep, and the traveller can
            take no new advance until it is settled. Left empty, it is set when the advance is paid
            out — the trip&apos;s end plus the policy&apos;s claim window, or 30 days.
          </p>
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="advance-form" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Request
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ApproveAdvanceDialog({
  advance, requestId, open, onOpenChange,
}: {
  advance: { id: string; requestedAmount: number; currencyCode: string } | null;
  requestId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [amount, setAmount] = useState('');

  // Prefilled with what was asked for whenever an advance is chosen. This sat in the Dialog's own
  // onOpenChange, which Radix does not call when the parent opens it through `open` — so the box
  // opened empty (lane 3).
  useEffect(() => {
    setAmount(advance ? String(advance.requestedAmount) : '');
  }, [advance]);

  const value = Number(amount);
  const tooMuch = !!advance && value > advance.requestedAmount;
  const valid = amount !== '' && value > 0 && !tooMuch;

  const approve = useMutation({
    mutationFn: () => {
      if (!advance) throw new Error('No advance selected');
      return travelFinanceService.approveAdvance(advance.id, value);
    },
    onSuccess: async () => {
      toast({ title: 'Advance approved' });
      onOpenChange(false);
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not approve', description: e.message }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Approve this advance</DialogTitle>
          <DialogDescription>
            Approve the amount requested or less — never more, and never your own advance. You are
            recorded as the approver, so another officer pays it out. With the trip&apos;s other approved
            advances it must stay within the trip&apos;s approved budget.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2">
          <label className="text-sm font-medium" htmlFor="approved-amount">
            Amount approved
          </label>
          <input
            id="approved-amount"
            type="number"
            step="0.01"
            min="0.01"
            max={advance?.requestedAmount}
            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
          {advance && (
            <p className={`text-xs ${tooMuch ? 'text-destructive' : 'text-muted-foreground'}`}>
              {tooMuch
                ? `More than the ${fmtMoney(advance.requestedAmount, advance.currencyCode)} requested.`
                : `${fmtMoney(advance.requestedAmount, advance.currencyCode)} was requested.`}
            </p>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!valid || approve.isPending} onClick={() => approve.mutate()}>
            {approve.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Approve
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Unused cash handed back (lane 3, O-8): at most what is outstanding, once per advance. */
function RefundAdvanceDialog({
  advance, requestId, onClose,
}: {
  advance: StaffTravelAdvanceSummary | null;
  requestId: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [amount, setAmount] = useState('');
  const [reference, setReference] = useState('');

  useEffect(() => {
    setAmount(advance ? String(advance.unsettledAmount) : '');
    setReference('');
  }, [advance]);

  const value = Number(amount);
  const tooMuch = !!advance && value > advance.unsettledAmount;
  const valid = amount !== '' && value > 0 && !tooMuch && reference.trim().length > 0;

  const refund = useMutation({
    mutationFn: () => {
      if (!advance) throw new Error('No advance selected');
      return travelFinanceService.refundAdvance(advance.id, { amount: value, reference: reference.trim() });
    },
    onSuccess: async () => {
      toast({ title: 'Refund recorded' });
      onClose();
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', requestId] });
      await queryClient.invalidateQueries({ queryKey: ['travel-overdue-settlements'] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not record the refund', description: e.message }),
  });

  return (
    <Dialog open={!!advance} onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Record cash handed back</DialogTitle>
          <DialogDescription>
            Unused advance cash the traveller returned. It settles the advance as a claim would, and
            posts to Finance when travel posting is switched on. One refund per advance — settle
            anything left through a claim, or write it off.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-3">
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="refund-amount">Amount handed back</label>
            <input
              id="refund-amount"
              type="number"
              step="0.01"
              min="0.01"
              max={advance?.unsettledAmount}
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
            />
            {advance && (
              <p className={`text-xs ${tooMuch ? 'text-destructive' : 'text-muted-foreground'}`}>
                {tooMuch ? 'More than is outstanding. ' : ''}
                {fmtMoney(advance.unsettledAmount, advance.currencyCode)} is outstanding.
              </p>
            )}
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="refund-reference">Receipt or bank reference</label>
            <input
              id="refund-reference"
              maxLength={100}
              className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button disabled={!valid || refund.isPending} onClick={() => refund.mutate()}>
            {refund.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Record refund
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Paying an advance out is money leaving: confirmed, and never by the officer who approved it (D-2). */
function DisburseAdvanceDialog({
  advance, requestId, onClose,
}: {
  advance: StaffTravelAdvanceSummary | null;
  requestId: string;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const disburse = useMutation({
    mutationFn: () => {
      if (!advance) throw new Error('No advance selected');
      return travelFinanceService.disburseAdvance(advance.id);
    },
    onSuccess: async () => {
      toast({ title: 'Advance disbursed' });
      onClose();
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not disburse', description: e.message }),
  });

  return (
    <Dialog open={!!advance} onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Pay out advance {advance?.advanceNumber}</DialogTitle>
          <DialogDescription>
            {advance && <>{fmtMoney(advance.approvedAmount, advance.currencyCode)} goes to {advance.employeeName}. </>}
            From now the traveller owes it until a claim, cash handed back or a write-off settles it.
            You are recorded as the officer who paid it out — it cannot be the officer who approved it.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancel</Button>
          <Button disabled={disburse.isPending} onClick={() => disburse.mutate()}>
            {disburse.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Pay out
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

const isCashOut = (a: StaffTravelAdvanceSummary) =>
  ['Disbursed', 'PartiallySettled', 'Overdue'].includes(a.status) && a.unsettledAmount > 0;

// ── The panel ────────────────────────────────────────────────────────────────

/**
 * The money on one trip: its budget, the advances paid out against it, and the expense claims
 * filed back.
 *
 * ⚠ **Nothing on this screen computes a stored figure.** Committed spend, actual spend, variance,
 * claim totals, what an advance has left outstanding — all are the server's, and all were once the
 * client's, which is how this area came to have an advance that was never recovered and a budget
 * showing whatever someone last typed. Where a number is shown it came off the wire.
 *
 * ⚠ **Committed and actual measure different routes and must not be added.** Committed is bookings
 * made; actual is cash paid out — claims paid and, since lane 3, advances. A booking paid direct to a vendor is committed and never becomes a
 * claim, so neither figure contains the other. The labels say which is which for that reason.
 *
 * Finance posting (since 2026-09-20): a disbursed advance, an approved claim and a paid claim each
 * post a journal through HR's posting adapter when a posting rule for the event is enabled under
 * HR Settings → Finance posting; without one the record is kept Unposted. Since lane 3 so do cash
 * handed back and a write-off. The Finance column on the advances shows which. (This remark said
 * "no GL posting exists" until the travel final closure.)
 */
export function TravelFinancePanel({ request }: { request: StaffTravelRequest }) {
  const requestId = request.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // Lane 3, N8: every money button rendered for any reader and answered 403. Write for the desk's
  // verbs; Admin for a write-off.
  const access = useTravelAccess();
  const [showBudget, setShowBudget] = useState(false);
  const [showAdvance, setShowAdvance] = useState(false);
  const [approving, setApproving] = useState<
    { id: string; requestedAmount: number; currencyCode: string } | null>(null);
  const [disbursing, setDisbursing] = useState<StaffTravelAdvanceSummary | null>(null);
  const [refunding, setRefunding] = useState<StaffTravelAdvanceSummary | null>(null);
  const [deciding, setDeciding] = useState<
    { advance: StaffTravelAdvanceSummary; verb: 'reject' | 'cancel' | 'write-off' } | null>(null);
  // D-16: an advance is cash for a trip that is going ahead; a budget is set once the trip is approved.
  const tripTakesAdvances = request.status === 'Approved' || request.status === 'InProgress';
  const tripTakesBudget = tripTakesAdvances || request.status === 'Completed';

  // ⚠ The currency lists are read through `api/hr/currencies` inside each CurrencyField. This panel
  // read `api/finance/currencies`, which answers 403 without a Finance permission, so no budget or
  // advance could be saved by the HR desk (travel final closure, lane 0 — finding O-19).

  const { data: budget, isLoading, isError: budgetFailed, error: budgetError } = useQuery({
    queryKey: ['travel-budget', requestId],
    queryFn: () => travelFinanceService.getBudget(requestId),
  });

  const { data: advances, isError: advancesFailed, error: advancesError } = useQuery({
    queryKey: ['travel-advances', requestId],
    queryFn: () => travelFinanceService.getAdvancesByRequest(requestId),
  });

  const { data: claims, isError: claimsFailed, error: claimsError } = useQuery({
    queryKey: ['travel-claims', requestId],
    queryFn: () => travelFinanceService.getClaimsByRequest(requestId),
  });

  const decide = useMutation({
    mutationFn: ({ id, verb, reason }: { id: string; verb: 'reject' | 'cancel' | 'write-off'; reason: string }) =>
      verb === 'reject' ? travelFinanceService.rejectAdvance(id, reason)
        : verb === 'cancel' ? travelFinanceService.cancelAdvance(id, reason)
          : travelFinanceService.writeOffAdvance(id, reason),
    onSuccess: async (_, { verb }) => {
      toast({ title: verb === 'reject' ? 'Advance rejected' : verb === 'cancel' ? 'Advance cancelled' : 'Advance written off' });
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', requestId] });
      await queryClient.invalidateQueries({ queryKey: ['travel-overdue-settlements'] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not record that', description: e.message }),
  });

  const approveBudget = useMutation({
    mutationFn: (id: string) => travelFinanceService.approveBudget(id),
    onSuccess: async () => {
      toast({ title: 'Budget approved' });
      await queryClient.invalidateQueries({ queryKey: ['travel-budget', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not approve the budget', description: e.message }),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  const committedPct = budget && budget.approvedTotal > 0
    ? Math.min(100, (budget.totalCommitted / budget.approvedTotal) * 100)
    : 0;
  const actualPct = budget && budget.approvedTotal > 0
    ? Math.min(100, (budget.totalActual / budget.approvedTotal) * 100)
    : 0;

  return (
    <div className="space-y-4">
      {/* Budget */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <TrendingUp className="h-4 w-4" />
            Budget
          </CardTitle>
          <div className="flex gap-2">
            {access.canAdmin && budget && !budget.approvedAt && tripTakesBudget && (
              <Button
                size="sm"
                onClick={() => approveBudget.mutate(budget.id)}
                disabled={approveBudget.isPending}
              >
                {approveBudget.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Approve the budget
              </Button>
            )}
            {access.canWrite && tripTakesBudget && (
              <Button variant="outline" size="sm" onClick={() => setShowBudget(true)}>
                {budget ? 'Edit budget' : <><Plus className="mr-2 h-4 w-4" /> Set a budget</>}
              </Button>
            )}
          </div>
        </CardHeader>
        <CardContent>
          {budgetFailed && !budget ? (
            <TravelQueryError error={budgetError} what="the budget" />
          ) : !budget ? (
            <EmptyState
              title="No budget set"
              description={tripTakesBudget
                ? "Set one to track this trip's spend against an approved figure."
                : 'A budget is set once the trip is approved.'}
            />
          ) : (
            <div className="space-y-4">
              <p className="text-xs text-muted-foreground">
                {budget.approvedAt
                  ? `Approved by ${budget.approvedByName ?? 'a travel administrator'} on ${fmtDate(budget.approvedAt)}.`
                  : 'Not approved yet — a travel administrator other than the traveller approves it.'}
                {budget.tripApprovedBudget != null &&
                  ` The trip's approved budget is ${fmtMoney(budget.tripApprovedBudget, request.currencyCode)}.`}
              </p>
              <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
                <div>
                  <p className="text-xs text-muted-foreground">Approved</p>
                  <p className="text-lg font-semibold">
                    {fmtMoney(budget.approvedTotal, budget.currencyCode)}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Committed</p>
                  <p className={`text-lg font-semibold ${budget.committedOverrun ? 'text-destructive' : ''}`}>
                    {fmtMoney(budget.totalCommitted, budget.currencyCode)}
                  </p>
                  <p className="text-xs text-muted-foreground">bookings made</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Actual</p>
                  <p className={`text-lg font-semibold ${budget.actualOverrun ? 'text-destructive' : ''}`}>
                    {fmtMoney(budget.totalActual, budget.currencyCode)}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    claims paid {fmtMoney(budget.actualClaimsPaid, budget.currencyCode)} · advances{' '}
                    {fmtMoney(budget.actualAdvancesPaidOut, budget.currencyCode)}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Variance</p>
                  <p
                    className={`text-lg font-semibold ${budget.variance < 0 ? 'text-destructive' : ''}`}
                  >
                    {fmtMoney(budget.variance, budget.currencyCode)}
                  </p>
                  <p className="text-xs text-muted-foreground">approved less actual</p>
                </div>
              </div>

              <div className="space-y-2">
                <div className="flex items-center justify-between text-xs text-muted-foreground">
                  <span>Committed against approved</span>
                  <span>{Math.round(committedPct)}%</span>
                </div>
                <Progress value={committedPct} />
                <div className="flex items-center justify-between text-xs text-muted-foreground">
                  <span>Paid against approved</span>
                  <span>{Math.round(actualPct)}%</span>
                </div>
                <Progress value={actualPct} />
              </div>

              {/*
                Said plainly because the two bars invite being read as parts of one whole, and they
                are not: a vendor-paid booking is committed and never becomes a claim.
              */}
              {(budget.committedOverrun || budget.actualOverrun) && (
                <p className="rounded-md border border-destructive/50 p-3 text-sm text-destructive">
                  {budget.committedOverrun && budget.actualOverrun
                    ? 'Committed and actual spend are both'
                    : budget.committedOverrun ? 'Committed spend is' : 'Actual spend is'}{' '}
                  above the approved total. This is flagged, not refused — bookings, advances and claims
                  still go ahead.
                </p>
              )}
              <p className="text-xs text-muted-foreground">
                Committed counts bookings made (not a no-show; a cancelled booking&apos;s fee); actual
                counts cash paid out — claims paid, and advances paid out less cash handed back. They
                measure different routes and do not add up to total spend — a booking paid direct to a
                vendor is committed but never becomes a claim.
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Advances */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <HandCoins className="h-4 w-4" />
            Advances
          </CardTitle>
          {access.canWrite && (
            <Button
              variant="outline"
              size="sm"
              disabled={!tripTakesAdvances}
              title={tripTakesAdvances ? undefined : 'An advance is for an approved trip or one under way'}
              onClick={() => setShowAdvance(true)}
            >
              <Plus className="mr-2 h-4 w-4" /> Request an advance
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {advancesFailed && !advances ? (
            <div className="p-4">
              <TravelQueryError error={advancesError} what="the advances" />
            </div>
          ) : (advances ?? []).length === 0 ? (
            <EmptyState
              icon={Wallet}
              title="No advances"
              description="Nothing has been paid out ahead of this trip."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead className="text-right">Requested</TableHead>
                  <TableHead className="text-right">Approved</TableHead>
                  <TableHead className="text-right">Outstanding</TableHead>
                  <TableHead>Settle by</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Finance</TableHead>
                  <TableHead className="w-40" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(advances ?? []).map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="font-medium">{a.advanceNumber}</TableCell>
                    <TableCell>{humanize(a.advanceTypeName)}</TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(a.requestedAmount, a.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(a.approvedAmount, a.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(a.unsettledAmount, a.currencyCode)}
                    </TableCell>
                    <TableCell className="whitespace-nowrap">
                      {fmtDate(a.settlementDeadline)}
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-wrap items-center gap-1">
                        <StatusBadge status={humanize(a.statusName)} />
                        {/* Past its deadline before the nightly sweep has written Overdue. */}
                        {a.isOverdue && a.status !== 'Overdue' && <StatusBadge status="Overdue" />}
                      </div>
                      {a.outcomeReason && (
                        <p className="mt-1 max-w-[16rem] text-xs text-muted-foreground">{a.outcomeReason}</p>
                      )}
                      {a.refundedAmount > 0 && (
                        <p className="mt-1 text-xs text-muted-foreground">
                          {fmtMoney(a.refundedAmount, a.currencyCode)} handed back
                        </p>
                      )}
                    </TableCell>
                    <TableCell><FinancePostingInlineStatus sourceDocumentId={a.id} /></TableCell>
                    <TableCell>
                      <div className="flex flex-wrap gap-1">
                        {access.canWrite && a.status === 'Requested' && (
                          <>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setApproving({
                                id: a.id,
                                requestedAmount: a.requestedAmount,
                                currencyCode: a.currencyCode,
                              })}
                            >
                              Approve
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => setDeciding({ advance: a, verb: 'reject' })}>
                              Reject
                            </Button>
                          </>
                        )}
                        {access.canWrite && a.status === 'Approved' && (
                          <Button variant="ghost" size="sm" onClick={() => setDisbursing(a)}>
                            Pay out
                          </Button>
                        )}
                        {access.canWrite && (a.status === 'Requested' || a.status === 'Approved') && (
                          <Button variant="ghost" size="sm" onClick={() => setDeciding({ advance: a, verb: 'cancel' })}>
                            Cancel
                          </Button>
                        )}
                        {access.canWrite && isCashOut(a) && a.refundedAmount === 0 && (
                          <Button variant="ghost" size="sm" onClick={() => setRefunding(a)}>
                            Cash back
                          </Button>
                        )}
                        {access.canAdmin && isCashOut(a) && (
                          <Button variant="ghost" size="sm" onClick={() => setDeciding({ advance: a, verb: 'write-off' })}>
                            Write off
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Claims */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 pb-3">
          <CardTitle className="flex items-center gap-2 text-base">
            <Receipt className="h-4 w-4" />
            Expense claims
          </CardTitle>
          {access.canWrite && (
            <Button variant="outline" size="sm" asChild>
              <Link href={`/hr/travel/claims/new?requestId=${requestId}`}>
                <Plus className="mr-2 h-4 w-4" /> File a claim
              </Link>
            </Button>
          )}
        </CardHeader>
        <CardContent className="p-0">
          {claimsFailed && !claims ? (
            <div className="p-4">
              <TravelQueryError error={claimsError} what="the expense claims" />
            </div>
          ) : (claims ?? []).length === 0 ? (
            <EmptyState
              icon={Receipt}
              title="No claims"
              description="Nothing has been claimed back for this trip yet."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead className="text-right">Claimed</TableHead>
                  <TableHead className="text-right">Payable</TableHead>
                  <TableHead>Submitted</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(claims ?? []).map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-medium">
                      <Link href={`/hr/travel/claims/${c.id}`} className="hover:underline">
                        {c.claimNumber}
                      </Link>
                    </TableCell>
                    <TableCell>{humanize(c.claimTypeName)}</TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(c.totalClaimed, c.currencyCode)}
                    </TableCell>
                    <TableCell className="text-right whitespace-nowrap">
                      {fmtMoney(c.netPayable, c.currencyCode)}
                    </TableCell>
                    <TableCell className="whitespace-nowrap">{fmtDate(c.submittedAt)}</TableCell>
                    <TableCell><StatusBadge status={humanize(c.statusName)} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <BudgetDialog
        request={request}
        existing={budget}
        open={showBudget}
        onOpenChange={setShowBudget}
      />
      <AdvanceDialog
        requestId={requestId}
        open={showAdvance}
        onOpenChange={setShowAdvance}
        defaultCurrency={request.currencyCode}
      />
      <ApproveAdvanceDialog
        advance={approving}
        requestId={requestId}
        open={!!approving}
        onOpenChange={(v) => !v && setApproving(null)}
      />
      <DisburseAdvanceDialog advance={disbursing} requestId={requestId} onClose={() => setDisbursing(null)} />
      <RefundAdvanceDialog advance={refunding} requestId={requestId} onClose={() => setRefunding(null)} />
      <TravelReasonDialog
        open={!!deciding}
        onOpenChange={(v) => !v && setDeciding(null)}
        title={
          deciding?.verb === 'reject' ? `Reject advance ${deciding.advance.advanceNumber}`
            : deciding?.verb === 'cancel' ? `Cancel advance ${deciding?.advance.advanceNumber}`
              : `Write off advance ${deciding?.advance.advanceNumber ?? ''}`
        }
        description={
          deciding?.verb === 'reject'
            ? 'The request for cash is refused. The traveller owes nothing; the reason is kept on the advance.'
            : deciding?.verb === 'cancel'
              ? 'Withdrawn before any money goes out — the advance is no longer wanted. The reason is kept on the advance.'
              : deciding
                ? `${fmtMoney(deciding.advance.unsettledAmount, deciding.advance.currencyCode)} the traveller still holds is given up. It posts to Finance as a write-off when travel posting is switched on. You cannot write off your own advance.`
                : ''
        }
        confirmLabel={deciding?.verb === 'reject' ? 'Reject' : deciding?.verb === 'cancel' ? 'Cancel the advance' : 'Write off'}
        destructive
        pending={decide.isPending}
        onConfirm={(reason) => {
          if (!deciding) return Promise.resolve();
          return decide.mutateAsync({ id: deciding.advance.id, verb: deciding.verb, reason });
        }}
      />
    </div>
  );
}

