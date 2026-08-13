'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { CalendarClock, Search } from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { orientationSessionService } from '@/services/hr/orientation-session.service';
import { ORIENTATION_DELIVERY_MODE_OPTIONS } from '@/types/hr/orientation';
import type { OrientationSessionSummary } from '@/types/hr/orientation';

type Scope = 'upcoming' | 'open' | 'all';

const deliveryLabel = (value: string) =>
  ORIENTATION_DELIVERY_MODE_OPTIONS.find((o) => o.value === value)?.label ?? value;

const formatWhen = (iso?: string | null) => {
  if (!iso) return 'Not scheduled';
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
};

/**
 * Seats remaining, or null when the session is uncapped.
 *
 * `availableSeats` on the detail DTO is 0 for an uncapped session, which reads as "full" — so the
 * capacity is derived here from maxParticipants instead, and an uncapped session shows a dash.
 */
function seatsLeft(s: OrientationSessionSummary): number | null {
  if (s.maxParticipants == null) return null;
  return Math.max(0, s.maxParticipants - s.enrolledCount);
}

export default function OrientationSessionsPage() {
  const [scope, setScope] = useState<Scope>('upcoming');
  const [search, setSearch] = useState('');

  const { data: sessions = [], isLoading } = useQuery({
    queryKey: ['hr', 'orientation-sessions', scope],
    queryFn: () => {
      if (scope === 'open') return orientationSessionService.getOpenForEnrollment();
      if (scope === 'upcoming') return orientationSessionService.getUpcoming(60);
      return orientationSessionService.getByStatus('Published');
    },
  });

  const term = search.trim().toLowerCase();
  const filtered = term
    ? sessions.filter(
        (s) =>
          s.title.toLowerCase().includes(term) ||
          s.sessionCode.toLowerCase().includes(term) ||
          (s.programTitle ?? '').toLowerCase().includes(term),
      )
    : sessions;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Orientation Sessions"
        description="Scheduled runs of an induction programme — dates, venue, facilitators and the attendance register."
        backHref="/hr/orientation"
      />

      <Card>
        <CardContent className="flex flex-wrap items-center justify-between gap-3 p-4">
          <Tabs value={scope} onValueChange={(v) => setScope(v as Scope)}>
            <TabsList>
              <TabsTrigger value="upcoming">Next 60 days</TabsTrigger>
              <TabsTrigger value="open">Open for enrollment</TabsTrigger>
              <TabsTrigger value="all">Published</TabsTrigger>
            </TabsList>
          </Tabs>

          <div className="relative min-w-[240px] flex-1">
            <Search className="text-muted-foreground absolute left-2.5 top-2.5 h-4 w-4" />
            <Input
              placeholder="Search by title, code or programme…"
              className="pl-8"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <p className="text-muted-foreground p-6 text-sm">Loading sessions…</p>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={CalendarClock}
              title="No sessions"
              description={
                scope === 'upcoming'
                  ? 'Nothing is scheduled in the next 60 days.'
                  : scope === 'open'
                    ? 'No session is currently accepting enrollments.'
                    : 'No published sessions yet.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Code</TableHead>
                  <TableHead>Session</TableHead>
                  <TableHead>Programme</TableHead>
                  <TableHead>Delivery</TableHead>
                  <TableHead>Starts</TableHead>
                  <TableHead className="text-right">Enrolled</TableHead>
                  <TableHead className="text-right">Seats left</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filtered.map((s) => {
                  const left = seatsLeft(s);
                  return (
                    <TableRow key={s.id}>
                      <TableCell className="font-mono text-xs">
                        <Link
                          href={`/hr/orientation/sessions/${s.id}`}
                          className="hover:underline"
                        >
                          {s.sessionCode}
                        </Link>
                      </TableCell>
                      <TableCell>
                        <Link
                          href={`/hr/orientation/sessions/${s.id}`}
                          className="font-medium hover:underline"
                        >
                          {s.title}
                        </Link>
                      </TableCell>
                      <TableCell>{s.programTitle ?? '—'}</TableCell>
                      <TableCell>{deliveryLabel(s.deliveryMode)}</TableCell>
                      <TableCell>{formatWhen(s.scheduledStartAt)}</TableCell>
                      <TableCell className="text-right">{s.enrolledCount}</TableCell>
                      <TableCell className="text-right">
                        {left === null ? (
                          <span className="text-muted-foreground">Uncapped</span>
                        ) : (
                          <Badge variant={left === 0 ? 'destructive' : left <= 3 ? 'secondary' : 'outline'}>
                            {left}
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={s.status} />
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
