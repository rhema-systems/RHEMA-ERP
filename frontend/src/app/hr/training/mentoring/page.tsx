'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Handshake, Search, Settings, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { mentoringService } from '@/services/hr/mentoring.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Mentoring from the operational side: the caller's own relationships, and — for whoever is entitled
 * to it — the active ones across the organisation.
 *
 * "Mine" is token-derived, so the page never sends an employee id for the caller's own data. Opening
 * a pair someone is not part of is refused by the server rather than hidden here, because hiding a
 * row is a UI convenience and refusing a request is the actual rule.
 */
export default function MentoringPage() {
  const router = useRouter();
  const [view, setView] = useState<'mine' | 'active'>('mine');
  const [search, setSearch] = useState('');

  const { data: mine, isLoading: loadingMine } = useQuery({
    queryKey: ['hr', 'mentoring', 'pairs', 'mine'],
    queryFn: () => mentoringService.getMyPairs(),
  });
  const { data: active, isLoading: loadingActive } = useQuery({
    queryKey: ['hr', 'mentoring', 'pairs', 'active'],
    queryFn: () => mentoringService.getActivePairs(),
    enabled: view === 'active',
  });

  const rows = useMemo(() => {
    const list = view === 'mine' ? mine ?? [] : active ?? [];
    const term = search.trim().toLowerCase();
    if (!term) return list;
    return list.filter(
      (p) =>
        p.mentorName.toLowerCase().includes(term) ||
        p.menteeName.toLowerCase().includes(term) ||
        p.programName.toLowerCase().includes(term),
    );
  }, [mine, active, view, search]);

  const isLoading = view === 'mine' ? loadingMine : loadingActive;
  const myRows = mine ?? [];

  const tiles = useMemo(
    () => [
      { label: 'My pairs', value: myRows.length, icon: Handshake },
      { label: 'Active', value: myRows.filter((p) => p.status === 'Active').length },
      {
        label: 'Sessions logged',
        value: myRows.reduce((a, p) => a + p.totalSessionsCount, 0),
      },
      { label: 'Completed', value: myRows.filter((p) => p.status === 'Completed').length },
    ],
    [myRows],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Mentoring"
        description="Your mentoring relationships and their session logs."
        backHref="/hr/training"
        actions={
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push('/administration/hr/training/mentoring')}
          >
            <Settings className="mr-2 h-4 w-4" /> Programmes
          </Button>
        }
      />

      <MetricTiles tiles={tiles} />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Pairs</CardTitle>
              <CardDescription>
                Open one to log a session. What you write in your own notes stays yours.
              </CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Tabs value={view} onValueChange={(v) => setView(v as typeof view)}>
                <TabsList>
                  <TabsTrigger value="mine">Mine</TabsTrigger>
                  <TabsTrigger value="active">All active</TabsTrigger>
                </TabsList>
              </Tabs>
              <div className="relative w-56">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  placeholder="Search…"
                  className="pl-8"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                />
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Mentor</TableHead>
                  <TableHead>Mentee</TableHead>
                  <TableHead>Programme</TableHead>
                  <TableHead>Started</TableHead>
                  <TableHead className="text-right">Sessions</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(6)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={UserCheck}
                        title={view === 'mine' ? 'You are not in a mentoring pair' : 'No active pairs'}
                        description={
                          view === 'mine'
                            ? 'Pairs you mentor or are mentored in will appear here.'
                            : 'Pairs are created inside a programme, under Administration.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((p) => (
                    <TableRow
                      key={p.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/training/mentoring/${p.id}`)}
                    >
                      <TableCell className="font-medium">{p.mentorName}</TableCell>
                      <TableCell>{p.menteeName}</TableCell>
                      <TableCell className="text-muted-foreground">{p.programName}</TableCell>
                      <TableCell className="text-muted-foreground">{fmt(p.startDate)}</TableCell>
                      <TableCell className="text-right">{p.totalSessionsCount}</TableCell>
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
