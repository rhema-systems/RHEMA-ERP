'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Banknote, Loader2, Plus, Search, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import type { ManpowerBudgetStatus } from '@/types/hr/job-architecture';

const fmtMoney = (v?: number | null) =>
  v == null ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 0 });

const STATUS_TONE: Record<string, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  Submitted: 'bg-amber-100 text-amber-800',
  UnderReview: 'bg-amber-100 text-amber-800',
  Approved: 'bg-emerald-100 text-emerald-800',
  Active: 'bg-emerald-100 text-emerald-800',
  Rejected: 'bg-rose-100 text-rose-800',
  Closed: 'bg-slate-100 text-slate-500',
};

/**
 * Manpower budgets — headcount and the money behind it, approved Department Head → HR → Managing
 * Director (FR-HR-135).
 *
 * ⚠ **An approved budget is not just a plan: it sets the establishment** (decision D-2), which then
 * gates whether a vacancy may be opened at all under FR-HR-136. That is why the register shows what
 * a budget authorises in POSTS, not only in money.
 */
export default function ManpowerBudgetsPage() {
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['manpower-budgets', 'paged'],
    queryFn: () => jobArchitectureService.getBudgetsPaged({ pageNumber: 1, pageSize: 200 }),
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    return (data?.items ?? []).filter(
      (b) =>
        !term ||
        b.budgetNumber?.toLowerCase().includes(term) ||
        b.organizationUnitName?.toLowerCase().includes(term) ||
        String(b.fiscalYear).includes(term),
    );
  }, [data, search]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Manpower budgets"
        description="How many posts each unit is authorised to hold, and what they cost (FR-HR-135)."
        actions={
          <Link href="/hr/manpower-budgets/new">
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              New budget
            </Button>
          </Link>
        }
      />

      <Card>
        <CardContent className="space-y-4 pt-6">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search by number, unit or year"
              className="pl-9"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading…
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={Banknote}
              title="No manpower budgets"
              description="A budget authorises headcount for a unit and a fiscal year."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Organisation unit</TableHead>
                  <TableHead className="text-right">Year</TableHead>
                  <TableHead className="text-right">Posts</TableHead>
                  <TableHead className="text-right">Total budget</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((b) => {
                  const s = (b.statusName ?? b.status) as ManpowerBudgetStatus;
                  return (
                    <TableRow key={b.id}>
                      <TableCell className="font-mono text-xs">
                        <Link href={`/hr/manpower-budgets/${b.id}`} className="hover:underline">
                          {b.budgetNumber}
                        </Link>
                      </TableCell>
                      <TableCell className="font-medium">
                        <Link href={`/hr/manpower-budgets/${b.id}`} className="hover:underline">
                          {b.organizationUnitName || b.organizationLevelName || '—'}
                        </Link>
                      </TableCell>
                      <TableCell className="text-right">{b.fiscalYear}</TableCell>
                      <TableCell className="text-right">
                        {/* What it authorises, and the change it represents. */}
                        <span className="font-medium">{b.plannedHeadcount}</span>
                        <span className="ml-1 text-xs text-muted-foreground">
                          ({b.plannedHeadcount - b.currentHeadcount >= 0 ? '+' : ''}
                          {b.plannedHeadcount - b.currentHeadcount})
                        </span>
                      </TableCell>
                      <TableCell className="text-right">{fmtMoney(b.totalBudget)}</TableCell>
                      <TableCell>
                        <Badge className={STATUS_TONE[s] ?? 'bg-slate-100 text-slate-700'}>{s}</Badge>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <p className="flex items-center gap-2 text-xs text-muted-foreground">
        <Users className="h-3 w-3" />
        Approving a budget sets the establishment for every position it names, which then constrains
        recruitment and movements into those posts.
      </p>
    </div>
  );
}
