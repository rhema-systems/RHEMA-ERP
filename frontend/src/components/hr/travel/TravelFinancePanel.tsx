'use client';

import { useState } from 'react';
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
import { useToast } from '@/hooks/use-toast';
import { financeDataService } from '@/services/finance/finance-data.service';
import { travelFinanceService } from '@/services/hr/travel-finance.service';
import type { StaffTravelRequest } from '@/types/hr/travel';
import type { StaffTravelBudget } from '@/types/hr/travel-finance';

const ADVANCE_TYPES = ['Cash', 'CorporateCardLoad', 'PettyCash', 'WireTransfer'] as const;
const CLAIM_TYPES = ['PostTravel', 'AdvanceSettlement', 'PartialClaim', 'Amendment'] as const;

const humanize = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');
const options = (values: readonly string[]) => values.map((v) => ({ value: v, label: humanize(v) }));

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtMoney = (amount?: number | null, currency?: string) =>
  amount === null || amount === undefined
    ? '—'
    : new Intl.NumberFormat(undefined, {
        style: 'currency', currency: currency || 'GHS', currencyDisplay: 'code',
      }).format(amount);

// ── Budget ───────────────────────────────────────────────────────────────────

const budgetSchema = z.object({
  budgetYear: z.coerce.number().min(2000).max(2100),
  approvedTotal: z.coerce.number().min(0),
  currencyCode: z.string().min(1, 'Select a currency'),
  flightBudget: z.coerce.number().min(0),
  accommodationBudget: z.coerce.number().min(0),
  perDiemBudget: z.coerce.number().min(0),
  transportBudget: z.coerce.number().min(0),
  miscellaneousBudget: z.coerce.number().min(0),
});

function BudgetDialog({
  requestId, existing, open, onOpenChange, currencyOptions, defaultCurrency,
}: {
  requestId: string;
  existing?: StaffTravelBudget | null;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  currencyOptions: { value: string; label: string }[];
  defaultCurrency: string;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const form = useForm<z.input<typeof budgetSchema>>({
    resolver: zodResolver(budgetSchema),
    defaultValues: {
      budgetYear: existing?.budgetYear ?? new Date().getFullYear(),
      approvedTotal: existing?.approvedTotal ?? 0,
      currencyCode: existing?.currencyCode ?? defaultCurrency,
      flightBudget: existing?.flightBudget ?? 0,
      accommodationBudget: existing?.accommodationBudget ?? 0,
      perDiemBudget: existing?.perDiemBudget ?? 0,
      transportBudget: existing?.transportBudget ?? 0,
      miscellaneousBudget: existing?.miscellaneousBudget ?? 0,
    },
  });

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

  // The allocation lines are guidance, not a constraint the server enforces — say so rather than
  // silently letting them disagree with the approved total.
  const lines = ['flightBudget', 'accommodationBudget', 'perDiemBudget', 'transportBudget',
    'miscellaneousBudget'] as const;
  const allocated = lines.reduce((sum, k) => sum + (Number(form.watch(k)) || 0), 0);
  const approved = Number(form.watch('approvedTotal')) || 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{existing ? 'Edit the budget' : 'Set a budget'}</DialogTitle>
          <DialogDescription>
            Committed and actual spend are worked out from this trip&apos;s bookings and paid
            claims — there is nothing to enter for them.
          </DialogDescription>
        </DialogHeader>
        <form id="budget-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <FieldRow>
            <NumberField form={form} name="approvedTotal" label="Approved total" required />
            <SelectField
              form={form} name="currencyCode" label="Currency" required options={currencyOptions}
            />
          </FieldRow>
          <NumberField form={form} name="budgetYear" label="Budget year" required />

          <p className="text-sm font-medium">Allocation</p>
          <FieldRow>
            <NumberField form={form} name="flightBudget" label="Flights" />
            <NumberField form={form} name="accommodationBudget" label="Accommodation" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="perDiemBudget" label="Per diem" />
            <NumberField form={form} name="transportBudget" label="Transport" />
          </FieldRow>
          <NumberField form={form} name="miscellaneousBudget" label="Miscellaneous" />

          {allocated !== approved && (
            <p className="rounded-md border border-dashed p-3 text-sm text-muted-foreground">
              The allocation adds up to {fmtMoney(allocated, form.watch('currencyCode'))} against an
              approved total of {fmtMoney(approved, form.watch('currencyCode'))}. That is allowed —
              the lines are guidance and the approved total is the limit — but it is worth a second
              look.
            </p>
          )}
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" form="budget-form" disabled={save.isPending}>
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
  requestedAmount: z.coerce.number().min(0),
  currencyCode: z.string().min(1, 'Select a currency'),
  advanceType: z.enum(ADVANCE_TYPES),
  settlementDeadline: z.string().optional(),
});

function AdvanceDialog({
  requestId, employeeId, open, onOpenChange, currencyOptions, defaultCurrency,
}: {
  requestId: string;
  employeeId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
  currencyOptions: { value: string; label: string }[];
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
        employeeId,
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
            Approving and disbursing are separate steps, each recorded against whoever did it.
          </DialogDescription>
        </DialogHeader>
        <form id="advance-form" className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <FieldRow>
            <NumberField form={form} name="requestedAmount" label="Amount requested" required />
            <SelectField
              form={form} name="currencyCode" label="Currency" required options={currencyOptions}
            />
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form} name="advanceType" label="Type" required options={options(ADVANCE_TYPES)}
            />
            <DateField form={form} name="settlementDeadline" label="Settle by" />
          </FieldRow>
          <p className="text-xs text-muted-foreground">
            The settlement deadline drives the chase list. An advance not settled by then appears on
            overdue settlements and in the reminder sweep.
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

  const approve = useMutation({
    mutationFn: () => {
      if (!advance) throw new Error('No advance selected');
      return travelFinanceService.approveAdvance(advance.id, Number(amount));
    },
    onSuccess: async () => {
      toast({ title: 'Advance approved' });
      onOpenChange(false);
      setAmount('');
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not approve', description: e.message }),
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(v) => {
        if (v && advance) setAmount(String(advance.requestedAmount));
        onOpenChange(v);
      }}
    >
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Approve this advance</DialogTitle>
          <DialogDescription>
            Approve the full amount requested, or less. You are recorded as the approver.
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
            className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
          {advance && (
            <p className="text-xs text-muted-foreground">
              {fmtMoney(advance.requestedAmount, advance.currencyCode)} was requested.
            </p>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!amount || approve.isPending} onClick={() => approve.mutate()}>
            {approve.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Approve
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

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
 * made; actual is claims paid. A booking paid direct to a vendor is committed and never becomes a
 * claim, so neither figure contains the other. The labels say which is which for that reason.
 *
 * ⚠ **No GL posting exists behind any of this** (decision D-4). An unsettled advance is an employee
 * receivable that appears in no trial balance until the post-module Finance sweep.
 */
export function TravelFinancePanel({ request }: { request: StaffTravelRequest }) {
  const requestId = request.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [showBudget, setShowBudget] = useState(false);
  const [showAdvance, setShowAdvance] = useState(false);
  const [approving, setApproving] = useState<
    { id: string; requestedAmount: number; currencyCode: string } | null>(null);

  const { data: currencies } = useQuery({
    queryKey: ['finance', 'currencies', 'active'],
    queryFn: () => financeDataService.getCurrencies({ isActive: true }),
  });

  const { data: budget, isLoading } = useQuery({
    queryKey: ['travel-budget', requestId],
    queryFn: () => travelFinanceService.getBudget(requestId),
  });

  const { data: advances } = useQuery({
    queryKey: ['travel-advances', requestId],
    queryFn: () => travelFinanceService.getAdvancesByRequest(requestId),
  });

  const { data: claims } = useQuery({
    queryKey: ['travel-claims', requestId],
    queryFn: () => travelFinanceService.getClaimsByRequest(requestId),
  });

  const disburse = useMutation({
    mutationFn: (id: string) => travelFinanceService.disburseAdvance(id),
    onSuccess: async () => {
      toast({ title: 'Advance disbursed' });
      await queryClient.invalidateQueries({ queryKey: ['travel-advances', requestId] });
    },
    onError: (e: Error) =>
      toast({ variant: 'destructive', title: 'Could not disburse', description: e.message }),
  });

  const currencyOptions = (currencies ?? []).map((c) => ({
    value: c.currencyCode, label: `${c.currencyCode} — ${c.currencyName}`,
  }));

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
          <Button variant="outline" size="sm" onClick={() => setShowBudget(true)}>
            {budget ? 'Edit budget' : <><Plus className="mr-2 h-4 w-4" /> Set a budget</>}
          </Button>
        </CardHeader>
        <CardContent>
          {!budget ? (
            <EmptyState
              title="No budget set"
              description="Set one to track this trip's spend against an approved figure."
            />
          ) : (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
                <div>
                  <p className="text-xs text-muted-foreground">Approved</p>
                  <p className="text-lg font-semibold">
                    {fmtMoney(budget.approvedTotal, budget.currencyCode)}
                  </p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Committed</p>
                  <p className="text-lg font-semibold">
                    {fmtMoney(budget.totalCommitted, budget.currencyCode)}
                  </p>
                  <p className="text-xs text-muted-foreground">bookings made</p>
                </div>
                <div>
                  <p className="text-xs text-muted-foreground">Actual</p>
                  <p className="text-lg font-semibold">
                    {fmtMoney(budget.totalActual, budget.currencyCode)}
                  </p>
                  <p className="text-xs text-muted-foreground">claims paid</p>
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
              <p className="text-xs text-muted-foreground">
                Committed counts bookings made; actual counts claims paid. They measure different
                routes and do not add up to total spend — a booking paid direct to a vendor is
                committed but never becomes a claim.
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
          <Button variant="outline" size="sm" onClick={() => setShowAdvance(true)}>
            <Plus className="mr-2 h-4 w-4" /> Request an advance
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {(advances ?? []).length === 0 ? (
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
                    <TableCell><StatusBadge status={humanize(a.statusName)} /></TableCell>
                    <TableCell><FinancePostingInlineStatus sourceDocumentId={a.id} /></TableCell>
                    <TableCell>
                      <div className="flex gap-1">
                        {a.status === 'Requested' && (
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
                        )}
                        {a.status === 'Approved' && (
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={disburse.isPending}
                            onClick={() => disburse.mutate(a.id)}
                          >
                            Disburse
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
          <Button variant="outline" size="sm" asChild>
            <Link href={`/hr/travel/claims/new?requestId=${requestId}`}>
              <Plus className="mr-2 h-4 w-4" /> File a claim
            </Link>
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {(claims ?? []).length === 0 ? (
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
        requestId={requestId}
        existing={budget}
        open={showBudget}
        onOpenChange={setShowBudget}
        currencyOptions={currencyOptions}
        defaultCurrency={request.currencyCode}
      />
      <AdvanceDialog
        requestId={requestId}
        employeeId={request.employeeId}
        open={showAdvance}
        onOpenChange={setShowAdvance}
        currencyOptions={currencyOptions}
        defaultCurrency={request.currencyCode}
      />
      <ApproveAdvanceDialog
        advance={approving}
        requestId={requestId}
        open={!!approving}
        onOpenChange={(v) => !v && setApproving(null)}
      />
    </div>
  );
}

export { CLAIM_TYPES };
