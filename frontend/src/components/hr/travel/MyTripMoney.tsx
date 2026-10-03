'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation } from '@tanstack/react-query';
import { Banknote, Loader2, Plus, Receipt } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelRequest } from '@/types/hr/travel';
import type { TravelClaimType } from '@/types/hr/travel-finance';
import { fmtTravelMoney as fmtMoney } from './travel-format';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const humanize = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

const CLAIM_TYPES: TravelClaimType[] = ['PostTravel', 'AdvanceSettlement', 'PartialClaim', 'Amendment'];
/** A trip takes claims once it is approved — under way and completed too (lane 3, B3). */
const TAKES_CLAIMS = ['Approved', 'InProgress', 'Completed'];
/** Advance cash still with the traveller — what a claim recovers when it is paid. */
const CASH_OUT = ['Disbursed', 'PartiallySettled', 'Overdue'];
const NO_ADVANCE = 'none';

/**
 * The traveller's money on a trip (travel final closure, lane 7 — E7): their advances, what they still hold and by when
 * it is settled, and their claims and where each has got to, from the request's own read.
 *
 * Since slice 7d (D-38) the traveller **files their own claim** here — on a trip that is approved, under way or completed
 * — and opens each claim on its own page to add expenses and submit it. The claim is kept in the organisation's base
 * currency; an advance still out with the traveller is offered for it to settle. The desk reviews and pays as before.
 */
export function MyTripMoney({ request }: { request: StaffTravelRequest }) {
  const router = useRouter();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const advances = request.advances ?? [];
  const claims = request.expenseClaims ?? [];
  const cashOut = advances.filter((a) => CASH_OUT.includes(a.status));
  const [claimType, setClaimType] = useState<TravelClaimType>('PostTravel');
  const [advanceId, setAdvanceId] = useState<string>(NO_ADVANCE);
  const takesClaims = TAKES_CLAIMS.includes(request.status);

  const startClaim = () => {
    setClaimType(cashOut.length ? 'AdvanceSettlement' : 'PostTravel');
    setAdvanceId(cashOut[0]?.id ?? NO_ADVANCE);
    setOpen(true);
  };

  const file = useMutation({
    mutationFn: () => travelService.createMyClaim({
      staffTravelRequestId: request.id,
      claimType,
      travelAdvanceId: advanceId === NO_ADVANCE ? null : advanceId,
      lines: [],
    }),
    onSuccess: (claim) => {
      setOpen(false);
      toast({ title: `Claim ${claim.claimNumber} started`, description: 'Add your expenses, then send it to the travel desk.' });
      router.push(`/me/travel/claims/${claim.id}`);
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not start the claim', description: e.message }),
  });

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base"><Banknote className="h-4 w-4" /> Advances</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {advances.length === 0 ? (
            <p className="text-sm text-muted-foreground">No advance has been asked for on this trip.</p>
          ) : (
            advances.map((a) => (
              <div key={a.id} className="rounded-md border p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="text-sm font-medium">
                    {a.advanceNumber}
                    <span className="font-normal text-muted-foreground"> · {humanize(a.advanceTypeName)}</span>
                    {a.isOverdue && <Badge variant="destructive" className="ml-2">settlement overdue</Badge>}
                  </p>
                  <StatusBadge status={humanize(a.statusName)} />
                </div>
                <p className="text-xs text-muted-foreground">
                  Asked {fmtMoney(a.requestedAmount, a.currencyCode)}
                  {a.approvedAmount != null && ` · approved ${fmtMoney(a.approvedAmount, a.currencyCode)}`}
                  {a.disbursedAt && ` · paid ${fmtDate(a.disbursedAt)}`}
                </p>
                {a.disbursedAt && (
                  <p className="mt-1 text-sm">
                    You still hold {fmtMoney(a.unsettledAmount, a.currencyCode)}
                    {a.settlementDeadline && a.unsettledAmount > 0 && `, to be settled by ${fmtDate(a.settlementDeadline)}`}
                    <span className="text-muted-foreground">
                      {' '}(settled {fmtMoney(a.settledAmount, a.currencyCode)}
                      {a.refundedAmount > 0 && `, of which ${fmtMoney(a.refundedAmount, a.currencyCode)} handed back`})
                    </span>
                  </p>
                )}
                {a.outcomeReason && <p className="mt-1 text-sm text-muted-foreground">{a.outcomeReason}</p>}
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-start justify-between gap-4 pb-2">
          <CardTitle className="flex items-center gap-2 text-base"><Receipt className="h-4 w-4" /> Expense claims</CardTitle>
          {takesClaims && (
            <Button size="sm" variant="outline" onClick={startClaim}>
              <Plus className="mr-2 h-4 w-4" /> File a claim
            </Button>
          )}
        </CardHeader>
        <CardContent className="space-y-3">
          {!takesClaims && (
            <p className="text-xs text-muted-foreground">
              A claim is filed once the trip is approved — and while it is under way or completed.
            </p>
          )}
          {claims.length === 0 ? (
            <p className="text-sm text-muted-foreground">No expense claim is recorded on this trip.</p>
          ) : (
            claims.map((c) => (
              <Link key={c.id} href={`/me/travel/claims/${c.id}`}
                className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 hover:bg-muted/50">
                <div>
                  <p className="text-sm font-medium">
                    {c.claimNumber}
                    <span className="font-normal text-muted-foreground"> · {humanize(c.claimTypeName)}</span>
                  </p>
                  <p className="text-xs text-muted-foreground">
                    Claimed {fmtMoney(c.totalClaimed, c.currencyCode)} · payable {fmtMoney(c.netPayable, c.currencyCode)}
                    {c.submittedAt && ` · submitted ${fmtDate(c.submittedAt)}`}
                  </p>
                </div>
                <StatusBadge status={humanize(c.statusName)} />
              </Link>
            ))
          )}
        </CardContent>
      </Card>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>File an expense claim</DialogTitle>
            <DialogDescription>
              The claim is kept in the organisation&apos;s currency. You add the expenses — and their receipts — on the
              claim itself, then send it to the travel desk.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="my-claim-type">Type</Label>
              <Select value={claimType} onValueChange={(v) => v && setClaimType(v as TravelClaimType)}>
                <SelectTrigger id="my-claim-type"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {CLAIM_TYPES.map((t) => <SelectItem key={t} value={t}>{humanize(t)}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="my-claim-advance">Advance it settles</Label>
              <Select value={advanceId} onValueChange={(v) => v && setAdvanceId(v)}>
                <SelectTrigger id="my-claim-advance"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NO_ADVANCE}>None</SelectItem>
                  {cashOut.map((a) => (
                    <SelectItem key={a.id} value={a.id}>
                      {a.advanceNumber} — you hold {fmtMoney(a.unsettledAmount, a.currencyCode)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                When the claim is paid, what you still hold of the advance is taken off what you are paid.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button disabled={file.isPending} onClick={() => file.mutate()}>
              {file.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Start the claim
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
