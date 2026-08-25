'use client';

/**
 * Area 25 slice 6 — the desk register of active mentoring pairs.
 *
 * The caller's own relationships re-homed to /me/mentoring (D3/D9: my-mentorships are
 * self-service; programme administration stays desk). What remains here is the org-wide
 * view — HR.Training.Read-gated server-side — and rows open the PORTAL pair detail: one
 * working surface for a pair's session log, not one per world (the enrollments-register
 * precedent from this same slice).
 */

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { UserCheck, Search, Settings } from 'lucide-react';
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
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { mentoringService } from '@/services/hr/mentoring.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MentoringRegisterPage() {
  const router = useRouter();
  const [search, setSearch] = useState('');

  const { data: active, isLoading } = useQuery({
    queryKey: ['hr', 'mentoring', 'pairs', 'active'],
    queryFn: () => mentoringService.getActivePairs(),
  });

  const rows = useMemo(() => {
    const list = active ?? [];
    const term = search.trim().toLowerCase();
    if (!term) return list;
    return list.filter(
      (p) =>
        p.mentorName.toLowerCase().includes(term) ||
        p.menteeName.toLowerCase().includes(term) ||
        p.programName.toLowerCase().includes(term),
    );
  }, [active, search]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Mentoring"
        description="Active mentoring pairs across the organisation. Your own relationships live in your self-service portal."
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

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Active pairs</CardTitle>
              <CardDescription>
                A row opens the pair&apos;s working surface — private notes stay withheld from
                anyone who is not in the pair.
              </CardDescription>
            </div>
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
                        title="No active pairs"
                        description="Pairs are created inside a programme, under Administration."
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
