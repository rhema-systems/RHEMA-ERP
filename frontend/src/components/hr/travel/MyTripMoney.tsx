'use client';

import { Banknote, Receipt } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import type { StaffTravelRequest } from '@/types/hr/travel';
import { fmtTravelMoney as fmtMoney } from './travel-format';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const humanize = (v?: string | null) => (v ? v.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

/**
 * The traveller's money on a trip (travel final closure, lane 7, slice 7c1 — E7): their advances, what they still hold
 * and by when it is settled, and their claims and where each has got to. Read from the request's own read, which already
 * carries both as summaries.
 */
export function MyTripMoney({ request }: { request: StaffTravelRequest }) {
  const advances = request.advances ?? [];
  const claims = request.expenseClaims ?? [];

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
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base"><Receipt className="h-4 w-4" /> Expense claims</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {claims.length === 0 ? (
            <p className="text-sm text-muted-foreground">No expense claim is recorded on this trip.</p>
          ) : (
            claims.map((c) => (
              <div key={c.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3">
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
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}
