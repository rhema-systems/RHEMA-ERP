'use client';

/**
 * Area 25 slice 4 — my leave encashments (spec destination #5).
 *
 * An encashment hangs off one of MY leave requests whose type allows cash conversion,
 * within my balance for that year. The payout is derived server-side from emoluments —
 * this screen never lets the employee name their own amount (the field the DTO carries is
 * a fallback for rate-less configurations, not an offer). The desk register at
 * /hr/leave/encashments stays where it is (D3).
 *
 * ⚠ Round 5, lane L. Where the company does not allow leave to be cashed in while employed — TDC,
 * FR-HR-046, the Labour Act's s.31 — the portal does not link here, and a visit says where the cash
 * comes from instead (the final settlement) and offers no request. Past encashments stay readable.
 * Where it is allowed, only annual leave from the CURRENT leave year can be cashed, up to what can
 * be taken now; the server refuses the rest, so the picker offers only what can work.
 */

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import { ArrowLeft, Coins, Loader2 } from 'lucide-react';
import { useAuth } from '@/hooks/use-auth';
import { leaveEncashmentService, leaveService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { ENCASHMENT_STATUS_BADGE } from '@/components/me/leave/leave-status';
import { useLeaveYear } from '@/components/hr/leave/use-leave-year';
import { useEncashmentAvailability } from '@/components/me/leave/use-encashment-availability';

const fmtDate = (d: string) =>
  new Date(d).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });

/** The leave year a date falls in, for a leave year starting in `startMonth` (1 = January). */
const leaveYearOf = (date: string, startMonth: number) => {
  const y = Number(date.slice(0, 4));
  const m = Number(date.slice(5, 7));
  return m >= startMonth ? y : y - 1;
};

export default function MyLeaveEncashmentsPage() {
  const { user } = useAuth();
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const employeeId = user?.employeeId ?? '';
  // ⚠ The LEAVE year, not the calendar year (round 5, lane C4's rule; this screen was left to L).
  const { currentYear, startMonth } = useLeaveYear();
  const { allowed, explanation, loaded: availabilityKnown } = useEncashmentAvailability();
  const [chosenYear, setChosenYear] = useState<number | null>(null);
  const year = chosenYear ?? currentYear;
  const [open, setOpen] = useState(false);
  const [leaveRequestId, setLeaveRequestId] = useState('');
  const [days, setDays] = useState('');
  const [notes, setNotes] = useState('');

  const { data: encashments, isLoading } = useQuery({
    queryKey: ['me', 'leave-encashments', employeeId, year],
    queryFn: () => leaveEncashmentService.getByEmployee(employeeId, year),
    enabled: !!employeeId,
  });

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const { data: history } = useQuery({
    queryKey: ['me', 'leave-history', employeeId, currentYear],
    queryFn: () => leaveService.getEmployeeHistory(employeeId, currentYear, 1, 50),
    enabled: !!employeeId && open,
  });

  const { data: balances } = useQuery({
    queryKey: ['me', 'leave-balances', employeeId, currentYear],
    queryFn: () => leaveService.getEmployeeBalances(employeeId, currentYear),
    enabled: !!employeeId && open,
  });

  // Only requests of a cash-convertible type can carry an encashment, and each request
  // carries at most one — requests already encashed are filtered by the server's refusal,
  // but the obvious ones are trimmed here so the picker offers what can actually work.
  const encashableRequests = useMemo(() => {
    const convertible = new Set(
      (leaveTypes ?? []).filter((t) => t.allowCashConversion).map((t) => t.id),
    );
    const alreadyEncashed = new Set((encashments ?? []).map((e) => e.leaveRequestId));
    return (history?.items ?? []).filter(
      (r) =>
        convertible.has(r.leaveTypeId) &&
        !alreadyEncashed.has(r.id) &&
        !['Cancelled', 'Rejected'].includes(r.status) &&
        // Lane L3: only this leave year's days can be cashed; the server refuses any other year.
        leaveYearOf(r.startDate, startMonth) === currentYear,
    );
  }, [leaveTypes, history, encashments, startMonth, currentYear]);

  const selectedRequest = encashableRequests.find((r) => r.id === leaveRequestId);
  const selectedBalance = balances?.find(
    (b) => b.leaveTypeId === selectedRequest?.leaveTypeId,
  );

  const requestMutation = useMutation({
    mutationFn: () => {
      if (!selectedRequest) return Promise.reject(new Error('Choose a leave request first.'));
      return leaveEncashmentService.request({
        leaveRequestId,
        employeeId,
        leaveTypeId: selectedRequest.leaveTypeId,
        // The leave year the request falls in — which, by the filter above, is the current one.
        year: leaveYearOf(selectedRequest.startDate, startMonth),
        // Days only (leave settings audit 2): Finance puts the money on them when it pays.
        daysEncashed: Number(days),
        notes: notes || null,
      });
    },
    onSuccess: async () => {
      toast({
        title: 'Encashment requested',
        description: 'The payout amount is calculated by HR from your emoluments.',
      });
      setOpen(false);
      setLeaveRequestId('');
      setDays('');
      setNotes('');
      await queryClient.invalidateQueries({ queryKey: ['me', 'leave-encashments'] });
      await queryClient.invalidateQueries({ queryKey: ['me', 'leave-balances'] });
    },
    onError: (e: any) =>
      toast({
        title: 'Could not request the encashment',
        description: e?.message || 'Something went wrong.',
        variant: 'destructive',
      }),
  });

  const years = [currentYear, currentYear - 1, currentYear - 2];
  const daysNumber = Number(days);
  // Lane L3: what can be cashed is what can be TAKEN now — the days built up so far — not the
  // whole year's figure, which counts days not yet earned. The server applies the same test.
  const canCashNow = selectedBalance ? selectedBalance.accruedAvailableDays : null;
  const daysValid =
    !!days && Number.isFinite(daysNumber) && daysNumber > 0 &&
    (canCashNow == null || daysNumber <= canCashNow);

  return (
    <div className="space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="-ml-2 mb-2">
          <Link href="/me/leave">
            <ArrowLeft className="mr-1 h-4 w-4" /> My Leave
          </Link>
        </Button>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-bold tracking-tight">My encashments</h1>
            <p className="text-sm text-muted-foreground">
              Convert unused leave days to cash where your leave type allows it.
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Select value={String(year)} onValueChange={(v) => setChosenYear(Number(v))}>
              <SelectTrigger className="w-28">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {years.map((y) => (
                  <SelectItem key={y} value={String(y)}>
                    {y}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {allowed && (
              <Button onClick={() => setOpen(true)}>
                <Coins className="mr-2 h-4 w-4" /> Request encashment
              </Button>
            )}
          </div>
        </div>
      </div>

      {/* Round 5, lane L1: said, not merely hidden — somebody arriving by an old link should learn
          where the cash comes from rather than finding a button that only ever refuses. */}
      {availabilityKnown && !allowed && (
        <Card>
          <CardContent className="p-4 text-sm">
            <p className="font-medium">Leave is not cashed in while you are employed.</p>
            <p className="mt-1 text-muted-foreground">
              {explanation} Under the Labour Act an agreement to give up annual leave is void
              (s.31), so take your leave — plan it in the{' '}
              <Link href="/me/leave/planner" className="underline underline-offset-2">
                leave planner
              </Link>
              . Any encashments made before this changed are listed below.
            </p>
          </CardContent>
        </Card>
      )}

      {isLoading ? (
        <Skeleton className="h-40" />
      ) : encashments?.length ? (
        <div className="space-y-2">
          {encashments.map((e) => (
            <Card key={e.id}>
              <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4 text-sm">
                <div>
                  <div className="font-medium">
                    {e.leaveTypeName} · {e.daysEncashed} day{e.daysEncashed === 1 ? '' : 's'}
                  </div>
                  <div className="mt-0.5 text-xs text-muted-foreground">
                    {e.year}
                    {e.amountPaid ? ` · ${e.amountPaid.toLocaleString()} payable` : ''}
                    {e.processedDate ? ` · paid ${fmtDate(e.processedDate)}` : ''}
                    {e.paymentReference ? ` · ref ${e.paymentReference}` : ''}
                    {e.notes ? ` · ${e.notes}` : ''}
                  </div>
                  {/*
                    ⚠ L-20 said plainly: anybody disputing the figure has nothing to read. The
                    likeliest disputant is the employee looking at their own payout, which is THIS
                    screen — so the basis is spelled out here in full, not hidden in a `title`
                    tooltip the way the desk register can afford to. A tooltip is unreachable on a
                    phone, and this is the page most likely to be read on one.

                    ⚠ Shown only once the amount exists. On a pending request there is no payout to
                    explain yet, and an empty "how this was worked out" invites the reading that
                    somebody failed to record it.
                  */}
                  {e.amountPaid ? (
                    <div className="mt-1 text-xs text-muted-foreground">
                      {e.rateBasis ? (
                        <>
                          <span className="font-medium">How this was worked out: </span>
                          {e.rateBasis}
                        </>
                      ) : (
                        <span className="italic">
                          The basis for this figure was not recorded — it was paid before the
                          system kept one. Ask HR if you want it explained.
                        </span>
                      )}
                    </div>
                  ) : null}
                  {e.status === 'Rejected' && e.rejectionReason && (
                    <div className="mt-1 text-xs text-red-600">“{e.rejectionReason}”</div>
                  )}
                </div>
                <Badge className={ENCASHMENT_STATUS_BADGE[e.status] ?? ''} variant="outline">
                  {e.status}
                </Badge>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">
          No encashments in {year}. An encashment converts unused days on one of your leave
          requests into pay, where the leave type allows it.
        </p>
      )}

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Request an encashment</DialogTitle>
            <DialogDescription>
              Pick the leave request the days belong to. The payout is calculated by HR
              from your emoluments and the leave type&apos;s rate policy.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Leave request</Label>
              <Select value={leaveRequestId} onValueChange={setLeaveRequestId}>
                <SelectTrigger>
                  <SelectValue
                    placeholder={
                      encashableRequests.length
                        ? 'Choose a request'
                        : 'No encashable requests found'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {encashableRequests.map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.requestNumber} · {r.leaveTypeName} · {fmtDate(r.startDate)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {!encashableRequests.length && (
                <p className="text-xs text-muted-foreground">
                  Encashment needs a leave request on a cash-convertible leave type that has
                  not been encashed yet.
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label>Days to encash</Label>
              <Input
                type="number"
                min={0.5}
                step={0.5}
                value={days}
                onChange={(e) => setDays(e.target.value)}
              />
              {selectedBalance && (
                <p className="text-xs text-muted-foreground">
                  Up to {canCashNow} day{canCashNow === 1 ? '' : 's'} of{' '}
                  {selectedBalance.leaveTypeName} can be cashed in now — the days built up so far,
                  less those taken, booked or already cashed in.
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Close
            </Button>
            <Button
              disabled={!leaveRequestId || !daysValid || requestMutation.isPending}
              onClick={() => requestMutation.mutate()}
            >
              {requestMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Request
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
