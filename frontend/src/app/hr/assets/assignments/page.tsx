'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Package, Search } from 'lucide-react';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useDebounce } from '@/hooks/use-debounce';
import { assetRegisterService } from '@/services/hr/asset-register.service';

const PAGE_SIZE = 20;
const ANY = '__any__';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * ⚠ `Overdue` is deliberately absent from this filter, although the enum has it.
 *
 * Nothing in the system writes `AssignmentStatus.Overdue` and nothing should: whether a custody is
 * late is derived from its expected return date against today, and a derived fact stored in a
 * status column is stale the moment the day turns. Offering it as a filter would produce a list
 * that is permanently empty and read as "nothing is late" — which is exactly wrong. Lateness lives
 * on the returns watchlists, computed server-side.
 */
const STATUS_FILTERS = ['Active', 'Returned', 'Transferred', 'Lost', 'Damaged'] as const;

export default function AssetAssignmentsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>('Active');
  const debouncedSearch = useDebounce(search, 300);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'assignments', page, debouncedSearch, status],
    queryFn: () =>
      assetRegisterService.getAssignmentsPaged({
        pageNumber: page,
        pageSize: PAGE_SIZE,
        searchTerm: debouncedSearch || undefined,
        status: status === ANY ? undefined : status,
      }),
  });

  const rows = data?.items ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Assignments"
        description="Who signed for what, and when it is due back."
        backHref="/hr/assets"
      />

      <Card>
        <CardContent className="flex flex-wrap gap-3 p-4">
          <div className="relative min-w-[240px] flex-1">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-8"
              placeholder="Assignment number, asset, employee…"
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            />
          </div>
          <Select value={status} onValueChange={(v) => { setStatus(v); setPage(1); }}>
            <SelectTrigger className="w-[190px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Any status</SelectItem>
              {STATUS_FILTERS.map((s) => <SelectItem key={s} value={s}>{s}</SelectItem>)}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState icon={Package} title="No assignments match"
              description="No custody record answers to those filters." />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Assignment</TableHead>
                  <TableHead>Asset</TableHead>
                  <TableHead>Held by</TableHead>
                  <TableHead>Issued</TableHead>
                  <TableHead>Due back</TableHead>
                  <TableHead>Signed for</TableHead>
                  <TableHead>Rent</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell>
                      <Link href={`/hr/assets/assignments/${a.id}`} className="hover:underline">
                        {a.assignmentNumber}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{a.assetName}</div>
                      <div className="text-xs text-muted-foreground">
                        {a.assetNumber} · {a.assetTypeName}
                      </div>
                    </TableCell>
                    <TableCell>{a.employeeName}</TableCell>
                    <TableCell>{fmtDate(a.assignmentDate)}</TableCell>
                    <TableCell>
                      {a.expectedReturnDate ? (
                        <>
                          {fmtDate(a.expectedReturnDate)}
                          {/* Server-computed and signed. Never recomputed here: a page left open
                              overnight would start disagreeing with the watchlist it came from. */}
                          {a.status === 'Active' && a.daysUntilReturnDue !== null && (
                            <div className={a.daysUntilReturnDue < 0
                              ? 'text-xs text-red-600 dark:text-red-500'
                              : 'text-xs text-muted-foreground'}>
                              {a.daysUntilReturnDue < 0
                                ? `${Math.abs(a.daysUntilReturnDue)} days late`
                                : `in ${a.daysUntilReturnDue} days`}
                            </div>
                          )}
                        </>
                      ) : <span className="text-muted-foreground">Open-ended</span>}
                    </TableCell>
                    <TableCell>
                      {a.employeeAcknowledged
                        ? fmtDate(a.acknowledgementDate)
                        : <span className="text-amber-600 dark:text-amber-500">Not yet</span>}
                    </TableCell>
                    <TableCell>
                      {/* ⚠ 0 is not the same as null: zero means provided free — a stated, usually
                          taxable, arrangement — while null means no terms were ever set. */}
                      {a.rentalAmount === null
                        ? <span className="text-muted-foreground">—</span>
                        : a.rentalAmount === 0
                          ? 'Free of charge'
                          : `${a.rentalCurrencyCode ?? ''} ${a.rentalAmount.toLocaleString()} ${a.rentalFrequencyName ?? ''}`.trim()}
                    </TableCell>
                    <TableCell><StatusBadge status={a.statusName} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {data.page} of {data.totalPages} · {data.totalCount} assignment
            {data.totalCount === 1 ? '' : 's'}
          </p>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={!data.hasPrevious}
              onClick={() => setPage((p) => p - 1)}>Previous</Button>
            <Button variant="outline" size="sm" disabled={!data.hasNext}
              onClick={() => setPage((p) => p + 1)}>Next</Button>
          </div>
        </div>
      )}
    </div>
  );
}
