'use client';

import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, Info, Loader2, Search, Users, XCircle } from 'lucide-react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
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
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { awardsService } from '@/services/hr/awards.service';

/**
 * Who qualifies for an award, and why anyone else does not (AWD-01, D-9).
 *
 * ⚠ **The ineligible are shown on purpose.** TDC's note has management "set the criteria and then it
 * will qualify some employees"; whoever sets them needs to see why an expected name is missing, or a
 * mis-set rule and a correct one produce exactly the same screen.
 *
 * ⚠ **The counts come from the payload, never from `items.length`.** They are computed over the
 * whole workforce regardless of filter or page — "12 of 5,579" is the useful sentence, and "12 of
 * 12" is not one at all.
 *
 * ⚠ **Paged server-side.** Every active employee gets a verdict, which on the live tenant is 5,579
 * of them, each carrying its own reasons. This must never fetch the lot and filter in the browser.
 */
export default function AwardEligibilityPage() {
  const [awardTypeId, setAwardTypeId] = useState('');
  const [filter, setFilter] = useState<'all' | 'eligible' | 'ineligible'>('all');
  const [search, setSearch] = useState('');
  const [debounced, setDebounced] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 25;

  useEffect(() => {
    const t = setTimeout(() => { setDebounced(search.trim()); setPage(1); }, 300);
    return () => clearTimeout(t);
  }, [search]);

  const { data: types } = useQuery({
    queryKey: ['award-types'],
    queryFn: () => awardsService.getTypes(),
  });

  const activeTypes = (types ?? []).filter((t) => t.isActive);

  const { data, isFetching } = useQuery({
    queryKey: ['award-eligibility', awardTypeId, filter, debounced, page],
    queryFn: () =>
      awardsService.getEligible(awardTypeId, {
        filter,
        page,
        pageSize,
        search: debounced || undefined,
      }),
    enabled: Boolean(awardTypeId),
  });

  const rows = data?.items ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Who qualifies"
        description="The employees an award's criteria admit — and, deliberately, the ones they do not, with the reason."
        backHref="/hr/awards"
      />

      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <span className="text-sm text-muted-foreground">Award</span>
          <Select
            value={awardTypeId}
            onValueChange={(v) => { setAwardTypeId(v); setPage(1); }}
          >
            <SelectTrigger className="w-96">
              <SelectValue placeholder="Choose an award" />
            </SelectTrigger>
            <SelectContent>
              {activeTypes.map((t) => (
                <SelectItem key={t.id} value={t.id}>
                  {t.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <Select
            value={filter}
            onValueChange={(v) => { setFilter(v as typeof filter); setPage(1); }}
          >
            <SelectTrigger className="w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Everybody</SelectItem>
              <SelectItem value="eligible">Eligible only</SelectItem>
              <SelectItem value="ineligible">Ineligible only</SelectItem>
            </SelectContent>
          </Select>

          <div className="relative min-w-[14rem] flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-9"
              placeholder="Search by name or employee number"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      {!awardTypeId ? (
        <EmptyState
          icon={Users}
          title="Choose an award"
          description="Eligibility is meaningless until you say eligible for what — the criteria belong to the award."
        />
      ) : (
        <>
          {data && (
            <MetricTiles
              tiles={[
                {
                  label: 'Eligible',
                  value: data.eligibleCount,
                  hint: `of ${data.consideredCount} serving employees`,
                  icon: CheckCircle2,
                  tone: data.eligibleCount > 0 ? 'success' : 'warning',
                },
                {
                  label: 'Ineligible',
                  value: data.ineligibleCount,
                  hint: 'each with a reason',
                  icon: XCircle,
                },
                {
                  label: 'Considered',
                  value: data.consideredCount,
                  hint: 'active employees',
                  icon: Users,
                },
              ]}
            />
          )}

          {data?.eligibleCount === 0 && (
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                Nobody qualifies for this award. That is as likely to be a mis-set rule as a real
                result — the reasons beside each name below say which.
              </AlertDescription>
            </Alert>
          )}

          <Card>
            <CardContent className="p-0">
              {isFetching ? (
                <div className="flex items-center justify-center p-12">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : rows.length === 0 ? (
                <EmptyState
                  icon={Users}
                  title="Nothing to show"
                  description={debounced ? 'Nobody matches that search.' : 'No verdicts for this filter.'}
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Number</TableHead>
                      <TableHead>Standing</TableHead>
                      <TableHead>Why not</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rows.map((v) => (
                      <TableRow key={v.employeeId}>
                        <TableCell>{v.employeeName}</TableCell>
                        <TableCell className="text-muted-foreground">{v.employeeNumber ?? '—'}</TableCell>
                        <TableCell>
                          {v.isEligible ? (
                            <Badge
                              variant="secondary"
                              className="bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200"
                            >
                              Eligible
                            </Badge>
                          ) : (
                            <Badge variant="secondary">Not eligible</Badge>
                          )}
                        </TableCell>
                        {/* One entry per criterion failed — this is the column that tells a
                            mis-set rule from a correct one. */}
                        <TableCell className="text-sm text-muted-foreground">
                          {v.reasons.length === 0 ? '—' : v.reasons.join('; ')}
                        </TableCell>
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
                Page {data.page} of {data.totalPages} — {data.totalCount} matching this filter
              </p>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasPrevious}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!data.hasNext}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
