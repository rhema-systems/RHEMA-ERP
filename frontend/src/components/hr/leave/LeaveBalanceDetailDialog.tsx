'use client';

import type { ReactNode } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { LEAVE_STATUS_BADGE } from '@/components/me/leave/leave-status';
import { leaveService } from '@/services/hr/leave.service';
import { AccrualStatementPanel, fmtDay } from './AccrualStatementPanel';

function Figure({ label, value, hint }: { label: string; value: ReactNode; hint?: ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 text-sm font-medium">{value}</dd>
      {hint && <dd className="text-xs text-muted-foreground">{hint}</dd>}
    </div>
  );
}

/**
 * One balance, opened — round 5, lane C2.
 *
 * The balance detail endpoint existed with nothing on screen to reach it, so a balance could be read
 * only as a row of numbers. Here are the figures, the accrual statement behind the *Accrued* one,
 * and the requests, adjustments and cashed-in days that make up the rest.
 */
export function LeaveBalanceDetailDialog({
  balanceId,
  onClose,
}: {
  balanceId: string | null;
  onClose: () => void;
}) {
  const { data: b, isLoading } = useQuery({
    queryKey: ['hr', 'leave-balance-detail', balanceId],
    queryFn: () => leaveService.getBalanceDetail(balanceId!),
    enabled: !!balanceId,
  });

  return (
    <Dialog open={!!balanceId} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[820px]">
        <DialogHeader>
          <DialogTitle>
            {b ? `${b.employeeName} · ${b.leaveTypeName} ${b.year}` : 'Leave balance'}
          </DialogTitle>
          <DialogDescription>
            {b?.organizationUnitName ?? 'The balance, how it built up, and what was taken from it.'}
          </DialogDescription>
        </DialogHeader>

        {isLoading || !b ? (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading…
          </div>
        ) : (
          <div className="space-y-5">
            <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-4">
              <Figure label="Entitled" value={b.entitledDays} />
              <Figure
                label="Accrued"
                value={b.accruedToDateDays}
                hint={b.accruedAsOf ? `as at ${fmtDay(b.accruedAsOf)}` : 'does not build up'}
              />
              <Figure label="Carried over" value={b.carriedOverDays} />
              <Figure label="Adjustments" value={b.adjustmentDays} />
              <Figure label="Used" value={b.usedDays} />
              <Figure label="Pending" value={b.pendingDays} />
              <Figure label="Encashed" value={b.encashedDays} />
              <Figure
                label="Can take now"
                value={b.accruedAvailableDays}
                hint={`${b.availableDays} for the whole year`}
              />
            </dl>

            <AccrualStatementPanel balanceId={b.id} audience="desk" />

            <section className="space-y-2">
              <h3 className="text-sm font-semibold">Requests in {b.year}</h3>
              {b.requests.length === 0 ? (
                <p className="text-sm text-muted-foreground">None.</p>
              ) : (
                <ul className="divide-y rounded-md border text-sm">
                  {b.requests.map((r) => (
                    <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                      <Link href={`/hr/leave/requests/${r.id}`} className="font-medium hover:underline">
                        {r.requestNumber}
                      </Link>
                      <span className="text-muted-foreground">
                        {fmtDay(r.startDate)} – {fmtDay(r.endDate)} · {r.totalDays} day
                        {r.totalDays === 1 ? '' : 's'}
                      </span>
                      <Badge variant="outline" className={LEAVE_STATUS_BADGE[r.status] ?? ''}>
                        {r.status}
                      </Badge>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            {b.adjustments.length > 0 && (
              <section className="space-y-2">
                <h3 className="text-sm font-semibold">Adjustments</h3>
                <ul className="divide-y rounded-md border text-sm">
                  {b.adjustments.map((a) => (
                    <li key={a.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                      <span className="font-medium">
                        {a.days > 0 ? `+${a.days}` : a.days} day{Math.abs(a.days) === 1 ? '' : 's'}
                      </span>
                      <span className="min-w-0 flex-1 truncate text-muted-foreground">
                        {a.reasonCodeName ? `${a.reasonCodeName} — ` : ''}
                        {a.reason}
                      </span>
                      <span className="text-xs text-muted-foreground">
                        {fmtDay(a.adjustmentDate)} · {a.performedByName}
                      </span>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            {b.encashments.length > 0 && (
              <section className="space-y-2">
                <h3 className="text-sm font-semibold">Cashed in</h3>
                <ul className="divide-y rounded-md border text-sm">
                  {b.encashments.map((e) => (
                    <li key={e.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                      <span className="font-medium">
                        {e.daysEncashed} day{e.daysEncashed === 1 ? '' : 's'}
                      </span>
                      <Badge variant="outline">{e.status}</Badge>
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
