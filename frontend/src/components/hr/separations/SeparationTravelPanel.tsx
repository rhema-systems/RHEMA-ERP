'use client';

import Link from 'next/link';
import { Plane } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import type { SeparationTravel } from '@/types/hr/separation';

const money = (currency: string, amount: number) =>
  `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const spaced = (v: string) => v.replace(/([a-z])([A-Z])/g, '$1 $2');

/**
 * The leaver's staff travel on the clearance (travel final closure, lane 9, D-56) — read live, as the asset register is.
 * Advisory: it is no clearance line and does not hold the clearance; cash-out advances are deducted by the final
 * settlement (and settled in travel when it is released, D-58), and the separation's approval cancels the trips not yet
 * approved (D-57). Each record opens on the travel desk's own page.
 */
export function SeparationTravelPanel({ travel }: { travel?: SeparationTravel | null }) {
  const trips = travel?.trips ?? [];
  const advances = travel?.advances ?? [];
  const claims = travel?.claims ?? [];
  const empty = trips.length + advances.length + claims.length === 0;

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="flex items-center gap-2 text-base">
          <Plane className="h-4 w-4" />
          Staff travel
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        {empty ? (
          <p className="text-muted-foreground">Nothing open in staff travel.</p>
        ) : (
          <>
            {trips.map((t) => (
              <div key={t.id} className="flex flex-wrap items-start justify-between gap-2 rounded border p-3">
                <div className="space-y-0.5">
                  <Link href={`/hr/travel/${t.id}`} className="font-medium underline">
                    {t.requestNumber}
                  </Link>
                  <span className="text-muted-foreground">
                    {' '}· {t.destination}, {t.travelStartDate.slice(0, 10)} to {t.travelEndDate.slice(0, 10)}
                    {t.liveBookings > 0 && ` · ${t.liveBookings} live booking(s)`}
                  </span>
                  <p className="text-xs text-muted-foreground">{t.note}</p>
                </div>
                <Badge variant="secondary">{spaced(t.status)}</Badge>
              </div>
            ))}
            {advances.map((a) => (
              <div key={a.id} className="flex flex-wrap items-start justify-between gap-2 rounded border p-3">
                <div className="space-y-0.5">
                  <span className="font-medium">Advance {a.advanceNumber}</span>
                  <span className="text-muted-foreground">
                    {a.tripNumber && ` · ${a.tripNumber}`} · {money(a.currencyCode, a.amount)}
                    {a.unsettled > 0 && `, ${money(a.currencyCode, a.unsettled)} still out`}
                  </span>
                  <p className="text-xs text-muted-foreground">{a.note}</p>
                </div>
                <Badge variant="secondary">{spaced(a.status)}</Badge>
              </div>
            ))}
            {claims.map((c) => (
              <div key={c.id} className="flex flex-wrap items-start justify-between gap-2 rounded border p-3">
                <div>
                  <Link href={`/hr/travel/claims/${c.id}`} className="font-medium underline">
                    Claim {c.claimNumber}
                  </Link>
                  <span className="text-muted-foreground">
                    {c.tripNumber && ` · ${c.tripNumber}`} · {money(c.currencyCode, c.amount)} claimed
                  </span>
                </div>
                <Badge variant="secondary">{spaced(c.status)}</Badge>
              </div>
            ))}
          </>
        )}
        <p className="text-xs text-muted-foreground">
          A warning, not a clearance line — it does not hold the clearance. Cash still out on an advance is deducted by the
          final settlement and settled in travel when the settlement is released.
        </p>
      </CardContent>
    </Card>
  );
}
