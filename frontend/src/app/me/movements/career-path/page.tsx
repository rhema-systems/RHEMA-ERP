'use client';

/**
 * Area 25 slice 7 — the career timeline: spec destination #20, built fresh (census verdict
 * E — the endpoint existed, no screen anywhere; /hr/movements/career-paths/[employeeId] is
 * the HR-shaped register view).
 *
 * The read is implemented/approved movements in chronological order, token-derived. The
 * page renders them as a vertical timeline — where you started, each step, where you are now.
 */

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Route, TrendingUp } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { mePortalService } from '@/services/hr/me-portal.service';

const fmtDate = (v?: string | null) =>
  v ? new Date(v).toLocaleDateString(undefined, { month: 'short', year: 'numeric' }) : '—';

export default function MyCareerPathPage() {
  const { data: steps, isLoading } = useQuery({
    queryKey: ['me', 'movements', 'career-path'],
    queryFn: () => mePortalService.getCareerPath(),
  });

  const rows = steps ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Career Timeline"
        description="Every implemented move, in order — where you started and how you got here."
        backHref="/me/movements"
      />

      {isLoading ? (
        <div className="flex items-center justify-center py-24">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : rows.length === 0 ? (
        <EmptyState
          icon={Route}
          title="No movements on record yet"
          description="Once a movement is approved and implemented it becomes a step on this timeline."
        />
      ) : (
        <Card>
          <CardContent className="py-6">
            <ol className="relative ml-3 space-y-8 border-l pl-6">
              {rows.map((m, i) => {
                const isLatest = i === rows.length - 1;
                return (
                  <li key={m.id} className="relative">
                    <span
                      className={`absolute -left-[31px] flex h-4 w-4 items-center justify-center rounded-full border-2 ${
                        isLatest ? 'border-primary bg-primary' : 'border-muted-foreground/40 bg-background'
                      }`}
                    />
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm text-muted-foreground">
                        {fmtDate(m.effectiveDate ?? m.requestDate)}
                      </span>
                      <Badge variant={isLatest ? 'default' : 'outline'}>{m.movementTypeName}</Badge>
                      {m.isTemporary && (
                        <Badge variant="secondary" className="text-[10px]">
                          Temporary
                        </Badge>
                      )}
                      {isLatest && (
                        <span className="inline-flex items-center gap-1 text-xs text-primary">
                          <TrendingUp className="h-3.5 w-3.5" /> Current
                        </span>
                      )}
                    </div>
                    <Link href={`/me/movements/${m.id}`} className="mt-1 block hover:underline">
                      <span className="font-medium">{m.newPositionTitle}</span>
                    </Link>
                    <p className="text-sm text-muted-foreground">{m.newOrganizationUnitName}</p>
                    {m.currentPositionTitle && (
                      <p className="mt-0.5 text-xs text-muted-foreground">
                        from {m.currentPositionTitle} · {m.currentOrganizationUnitName}
                      </p>
                    )}
                  </li>
                );
              })}
            </ol>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
