'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  Award,
  CalendarRange,
  Loader2,
  Search,
  Trophy,
  UserPlus,
  Users,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { awardsService } from '@/services/hr/awards.service';
import type { AwardNominationStatus } from '@/types/hr/awards';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * ⚠ Money is nullable and null is **not** zero. A long-service rung with no amount is a question
 * TDC has not answered, and printing "GHS 0.00" would answer it for them.
 */
const fmtMoney = (v?: number | null) =>
  v === null || v === undefined
    ? <span className="text-muted-foreground">not set</span>
    : v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * ⚠ Six statuses, and "in flight" is three of them. A register that showed Submitted and
 * UnderReview as finished would hide the work still owed to the awards desk.
 */
const NOMINATION_TONE: Record<AwardNominationStatus, string> = {
  Draft: 'bg-muted text-muted-foreground',
  Submitted: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  UnderReview: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  Approved: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  Rejected: 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200',
  Withdrawn: 'bg-muted text-muted-foreground',
};

const NOMINATION_STATUSES: AwardNominationStatus[] = [
  'Draft',
  'Submitted',
  'UnderReview',
  'Approved',
  'Rejected',
  'Withdrawn',
];

export default function AwardsRegisterPage() {
  const [nominationStatus, setNominationStatus] = useState<AwardNominationStatus | 'all'>('all');
  const [nominationSearch, setNominationSearch] = useState('');
  const [awardPage, setAwardPage] = useState(1);
  const pageSize = 25;

  const { data: nominations, isLoading: loadingNominations } = useQuery({
    queryKey: ['award-nominations'],
    queryFn: () => awardsService.getNominations(),
  });

  const { data: awards, isLoading: loadingAwards } = useQuery({
    queryKey: ['awards-conferred', awardPage],
    queryFn: () => awardsService.getAwardsPaged({ pageNumber: awardPage, pageSize }),
  });

  const { data: types } = useQuery({
    queryKey: ['award-types'],
    queryFn: () => awardsService.getTypes(),
  });

  // Conferred and not yet handed over. ⚠ The route existed and nothing called it, so an award could
  // be decided, paid, and then quietly never presented with nothing surfacing that.
  const { data: awaitingPresentation } = useQuery({
    queryKey: ['awards-pending-presentation'],
    queryFn: () => awardsService.getPendingPresentations(),
  });

  const visibleNominations = useMemo(() => {
    const rows = nominations ?? [];
    const term = nominationSearch.trim().toLowerCase();
    return rows.filter((n) => {
      if (nominationStatus !== 'all' && n.status !== nominationStatus) return false;
      if (!term) return true;
      return (
        n.nomineeName.toLowerCase().includes(term) ||
        n.nominationNumber.toLowerCase().includes(term) ||
        n.awardTypeName.toLowerCase().includes(term)
      );
    });
  }, [nominations, nominationStatus, nominationSearch]);

  // Counted over EVERY nomination, not the filtered view — a tile that moved when somebody typed
  // in the search box would be describing the filter rather than the workload.
  const pending = (nominations ?? []).filter(
    (n) => n.status === 'Submitted' || n.status === 'UnderReview',
  ).length;

  const awardRows = awards?.items ?? [];
  const awardPages = Math.max(1, awards?.totalPages ?? 1);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Awards & Recognition"
        description="Nominations on the desk, awards conferred, and the catalogue behind them."
        backHref="/hr"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" asChild>
              <Link href="/hr/awards/me">
                <UserPlus className="mr-2 h-4 w-4" />
                My awards
              </Link>
            </Button>
            <Button variant="outline" asChild>
              <Link href="/hr/awards/eligibility">
                <Users className="mr-2 h-4 w-4" />
                Who qualifies
              </Link>
            </Button>
            <Button asChild>
              <Link href="/hr/awards/new">
                <Trophy className="mr-2 h-4 w-4" />
                Confer an award
              </Link>
            </Button>
          </div>
        }
      />

      <MetricTiles
        tiles={[
          {
            label: 'Nominations awaiting a decision',
            value: pending,
            hint: `of ${(nominations ?? []).length} raised`,
            icon: UserPlus,
            tone: pending > 0 ? 'warning' : 'default',
          },
          {
            label: 'Awards conferred',
            value: awards?.totalCount ?? 0,
            icon: Trophy,
          },
          {
            label: 'Awaiting presentation',
            value: (awaitingPresentation ?? []).length,
            hint: 'conferred, not yet handed over',
            icon: CalendarRange,
            tone: (awaitingPresentation ?? []).length > 0 ? 'warning' : 'default',
          },
          {
            label: 'Award types',
            value: (types ?? []).length,
            hint: `${(types ?? []).filter((t) => t.isActive).length} active`,
            icon: Award,
          },
        ]}
      />

      <Tabs defaultValue="nominations">
        <TabsList>
          <TabsTrigger value="nominations">Nominations</TabsTrigger>
          <TabsTrigger value="conferred">Conferred awards</TabsTrigger>
        </TabsList>

        <TabsContent value="nominations" className="space-y-4">
          <Card>
            <CardContent className="flex flex-wrap items-center gap-3 p-4">
              <div className="relative min-w-[16rem] flex-1">
                <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  className="pl-9"
                  placeholder="Search by nominee, number or award"
                  value={nominationSearch}
                  onChange={(e) => setNominationSearch(e.target.value)}
                />
              </div>
              <Select
                value={nominationStatus}
                onValueChange={(v) => setNominationStatus(v as AwardNominationStatus | 'all')}
              >
                <SelectTrigger className="w-56">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All statuses</SelectItem>
                  {NOMINATION_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-0">
              {loadingNominations ? (
                <div className="flex items-center justify-center p-12">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : visibleNominations.length === 0 ? (
                <EmptyState
                  icon={UserPlus}
                  title="No nominations"
                  description={
                    nominationSearch || nominationStatus !== 'all'
                      ? 'Nothing matches those filters.'
                      : 'Nominations raised by staff appear here once submitted.'
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead>Nominee</TableHead>
                      <TableHead>Award</TableHead>
                      <TableHead>Nominated by</TableHead>
                      <TableHead>Raised</TableHead>
                      <TableHead>Status</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {visibleNominations.map((n) => (
                      <TableRow key={n.id}>
                        <TableCell className="font-medium">
                          <Link className="underline" href={`/hr/awards/nominations/${n.id}`}>
                            {n.nominationNumber}
                          </Link>
                        </TableCell>
                        {/* A team nomination has no nominee — teamName carries the subject instead. */}
                        <TableCell>{n.nomineeName || n.teamName || '—'}</TableCell>
                        <TableCell>{n.awardTypeName}</TableCell>
                        <TableCell>{n.nominatedByName}</TableCell>
                        <TableCell>{fmtDate(n.nominationDate)}</TableCell>
                        <TableCell>
                          <Badge variant="secondary" className={NOMINATION_TONE[n.status]}>
                            {n.statusName}
                          </Badge>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="conferred" className="space-y-4">
          <Card>
            <CardContent className="p-0">
              {loadingAwards ? (
                <div className="flex items-center justify-center p-12">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : awardRows.length === 0 ? (
                <EmptyState
                  icon={Trophy}
                  title="No awards conferred"
                  description="Awards appear here once a nomination is approved or one is conferred directly."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Number</TableHead>
                      <TableHead>Employee</TableHead>
                      <TableHead>Award</TableHead>
                      {/* ⚠ Until slice 8 a Gold and a Bronze award were indistinguishable on read. */}
                      <TableHead>Level</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead className="text-right">Value</TableHead>
                      <TableHead>Presented</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {awardRows.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-medium">
                          <Link className="underline" href={`/hr/awards/${a.id}`}>
                            {a.awardNumber}
                          </Link>
                        </TableCell>
                        <TableCell>
                          {a.employeeName}
                          <span className="ml-2 text-xs text-muted-foreground">
                            {a.employeeNumber}
                          </span>
                        </TableCell>
                        <TableCell>{a.awardTypeName}</TableCell>
                        <TableCell>{a.awardLevelName ?? '—'}</TableCell>
                        <TableCell>{fmtDate(a.awardDate)}</TableCell>
                        <TableCell className="text-right">{fmtMoney(a.monetaryAmount)}</TableCell>
                        <TableCell>{fmtDate(a.presentationDate)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          {awardPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-muted-foreground">
                Page {awards?.page ?? 1} of {awardPages} — {awards?.totalCount ?? 0} awards
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!awards?.hasPrevious}
                  onClick={() => setAwardPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!awards?.hasNext}
                  onClick={() => setAwardPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </TabsContent>
      </Tabs>

      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <CalendarRange className="h-4 w-4 text-muted-foreground" />
          <p className="text-sm text-muted-foreground">
            <Link className="underline" href="/hr/awards/results">Results</Link>
            {' · '}
            <Link className="underline" href="/hr/awards/long-service">Long-service awards</Link>
            {' · '}
            Cycles, eligibility, committees and the long-service ladder are configured in{' '}
            <Link className="underline" href="/administration/hr/awards">
              Award setup
            </Link>
            .
          </p>
          <Users className="ml-auto h-4 w-4 text-muted-foreground" />
        </CardContent>
      </Card>
    </div>
  );
}
