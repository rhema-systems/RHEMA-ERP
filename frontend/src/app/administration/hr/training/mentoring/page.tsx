'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Users, Plus, Search, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { mentoringService } from '@/services/hr/mentoring.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MentoringProgramsPage() {
  const router = useRouter();
  const [view, setView] = useState<'all' | 'active'>('all');
  const [search, setSearch] = useState('');

  const { data: all, isLoading: loadingAll } = useQuery({
    queryKey: ['hr', 'mentoring', 'programs', 'all'],
    queryFn: () => mentoringService.getAllPrograms(),
  });
  const { data: active, isLoading: loadingActive } = useQuery({
    queryKey: ['hr', 'mentoring', 'programs', 'active'],
    queryFn: () => mentoringService.getActivePrograms(),
    enabled: view === 'active',
  });

  const rows = useMemo(() => {
    const list = view === 'active' ? active ?? [] : all ?? [];
    const term = search.trim().toLowerCase();
    if (!term) return list;
    return list.filter(
      (p) =>
        p.programName.toLowerCase().includes(term) ||
        (p.coordinatedByName ?? '').toLowerCase().includes(term),
    );
  }, [all, active, view, search]);

  const isLoading = view === 'active' ? loadingActive : loadingAll;
  const programs = all ?? [];

  const tiles = useMemo(
    () => [
      { label: 'Programmes', value: programs.length, icon: Users },
      { label: 'Active', value: programs.filter((p) => p.isActive).length },
      {
        label: 'Pairs',
        value: programs.reduce((a, p) => a + p.totalPairsCount, 0),
        icon: UserCheck,
      },
      {
        label: 'Active pairs',
        value: programs.reduce((a, p) => a + p.activePairsCount, 0),
      },
    ],
    [programs],
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Mentoring Schemes"
        description="The schemes. Pairs and session logs live under HR → Training → Mentoring."
        backHref="/administration/hr/training"
        actions={
          <Button size="sm" onClick={() => router.push('/administration/hr/training/mentoring/new')}>
            <Plus className="mr-2 h-4 w-4" /> New programme
          </Button>
        }
      />

      <MetricTiles tiles={tiles} />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Programmes</CardTitle>
              <CardDescription>Open one to see its pairs and coordinator.</CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Tabs value={view} onValueChange={(v) => setView(v as typeof view)}>
                <TabsList>
                  <TabsTrigger value="all">All</TabsTrigger>
                  <TabsTrigger value="active">Active</TabsTrigger>
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
                  <TableHead>Programme</TableHead>
                  <TableHead>Coordinator</TableHead>
                  <TableHead>Runs</TableHead>
                  <TableHead className="text-right">Pairs</TableHead>
                  <TableHead className="text-right">Active</TableHead>
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
                        icon={Users}
                        title="No mentoring programmes"
                        description="Create one, then pair mentors with mentees inside it."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((p) => (
                    <TableRow
                      key={p.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() =>
                        router.push(`/administration/hr/training/mentoring/${p.id}`)
                      }
                    >
                      <TableCell className="font-medium">{p.programName}</TableCell>
                      <TableCell>{p.coordinatedByName ?? '—'}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {fmt(p.startDate)} – {fmt(p.endDate)}
                      </TableCell>
                      <TableCell className="text-right">{p.totalPairsCount}</TableCell>
                      <TableCell className="text-right">{p.activePairsCount}</TableCell>
                      <TableCell>
                        <Badge variant={p.isActive ? 'default' : 'outline'}>
                          {p.isActive ? 'Active' : 'Inactive'}
                        </Badge>
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
