'use client';

/**
 * Area 25 slice 6 — My Mentoring: the caller's mentoring relationships (D9: my-mentorships
 * move in; programme administration stays desk).
 *
 * Token-derived /mine only — the org-wide "all active" view lives on the desk register at
 * /hr/training/mentoring. Opening a pair someone is not part of is refused by the server
 * rather than hidden here; the pair detail is the shared working surface for both sides.
 */

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Handshake } from 'lucide-react';
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
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { mentoringService } from '@/services/hr/mentoring.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyMentoringPage() {
  const router = useRouter();

  const { data: mine, isLoading } = useQuery({
    queryKey: ['me', 'mentoring', 'pairs', 'mine'],
    queryFn: () => mentoringService.getMyPairs(),
  });

  const rows = mine ?? [];
  const tiles = useMemo(
    () => [
      { label: 'My pairs', value: rows.length, icon: Handshake },
      { label: 'Active', value: rows.filter((p) => p.status === 'Active').length },
      { label: 'Sessions logged', value: rows.reduce((a, p) => a + p.totalSessionsCount, 0) },
      { label: 'Completed', value: rows.filter((p) => p.status === 'Completed').length },
    ],
    [rows],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Mentoring"
        description="Your mentoring relationships and their session logs."
        backHref="/me"
      />

      <MetricTiles tiles={tiles} />

      <Card>
        <CardHeader>
          <CardTitle>Pairs</CardTitle>
          <CardDescription>
            Open one to log a session. What you write in your own notes stays yours.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Programme</TableHead>
                  <TableHead>Mentor</TableHead>
                  <TableHead>Mentee</TableHead>
                  <TableHead>Started</TableHead>
                  <TableHead>Sessions</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(6)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Handshake}
                        title="No mentoring relationships"
                        description="Pairs you are part of — as mentor or mentee — appear here."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((p) => (
                    <TableRow
                      key={p.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/me/mentoring/${p.id}`)}
                    >
                      <TableCell className="font-medium">{p.programName}</TableCell>
                      <TableCell>{p.mentorName}</TableCell>
                      <TableCell>{p.menteeName}</TableCell>
                      <TableCell className="text-muted-foreground">{fmt(p.startDate)}</TableCell>
                      <TableCell className="text-muted-foreground">{p.totalSessionsCount}</TableCell>
                      <TableCell>
                        <StatusBadge status={p.statusName} />
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
