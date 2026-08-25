'use client';

/**
 * Area 25 slice 6 — My Service Bonds: the owed bond self-accept surface (training W3
 * slice-8 residual).
 *
 * A bond is raised by HR against a sponsored nomination; the /mine read is token-derived.
 * A PendingAcceptance bond is the row that matters — it blocks nothing technically, but it
 * is a personal financial obligation waiting on the caller's signature, so it sorts first
 * and carries the accent. Acceptance itself happens on the detail page, in front of the
 * full terms text — never from a list row.
 */

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Scale } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { trainingServiceBondService } from '@/services/hr/outcomes.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/** Pending first — it is the one waiting on the caller — then by recency. */
const sortRank: Record<string, number> = {
  PendingAcceptance: 0,
  Active: 1,
  Breached: 2,
  Settled: 3,
  Fulfilled: 4,
  Waived: 5,
  Cancelled: 6,
};

export default function MyBondsPage() {
  const router = useRouter();

  const { data: bonds, isLoading } = useQuery({
    queryKey: ['me', 'training', 'bonds', 'mine'],
    queryFn: () => trainingServiceBondService.getMine(),
  });

  const rows = [...(bonds ?? [])].sort(
    (a, b) => (sortRank[a.status] ?? 9) - (sortRank[b.status] ?? 9),
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Service Bonds"
        description="Service obligations attached to training the organisation sponsored for you."
        backHref="/me/training"
      />

      <Card>
        <CardHeader>
          <CardTitle>Bonds</CardTitle>
          <CardDescription>
            Open one to read its full terms. A pending bond is waiting for your acceptance.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Programme</TableHead>
                  <TableHead>Obligation</TableHead>
                  <TableHead>Amount</TableHead>
                  <TableHead>Runs</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(5)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={Scale}
                        title="No service bonds"
                        description="Bonds appear here when sponsored training carries a service obligation."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((b) => (
                    <TableRow
                      key={b.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/me/training/bonds/${b.id}`)}
                    >
                      <TableCell className="font-medium">
                        {b.programName ?? '—'}
                        {b.nominationNumber && (
                          <div className="font-mono text-xs text-muted-foreground">
                            {b.nominationNumber}
                          </div>
                        )}
                      </TableCell>
                      <TableCell>{b.bondDurationMonths} months</TableCell>
                      <TableCell>
                        {b.currency} {b.bondAmount.toLocaleString()}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {b.bondStartDate ? `${fmt(b.bondStartDate)} – ${fmt(b.bondEndDate)}` : '—'}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <StatusBadge status={b.statusName} />
                          {b.status === 'PendingAcceptance' && (
                            <Badge variant="destructive" className="text-[10px]">
                              Needs your acceptance
                            </Badge>
                          )}
                          {b.status === 'Active' && b.monthsRemaining > 0 && (
                            <span className="text-xs text-muted-foreground">
                              {b.monthsRemaining} months left
                            </span>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        Questions about a bond&apos;s terms, waivers or settlement are for the HR desk —{' '}
        <Link href="/me" className="underline underline-offset-2">
          contact HR
        </Link>{' '}
        rather than declining training you need.
      </p>
    </div>
  );
}
