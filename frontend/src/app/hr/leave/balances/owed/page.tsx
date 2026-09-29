'use client';

import { useMemo, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Download, Loader2, Wallet } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { fmtDay } from '@/components/hr/leave/AccrualStatementPanel';
import { useLeaveYear } from '@/components/hr/leave/use-leave-year';
import { leaveService } from '@/services/hr/leave.service';

/** Saves a blob the browser already has, rather than navigating to a URL that carries no token. */
function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

const PAGE_SIZE = 50;
const n = (value: number) => Math.round(value * 100) / 100;
const iso = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const todayIso = () => iso(new Date());
/** The calendar day before a `YYYY-MM-DD` date. */
const dayBefore = (value: string) => {
  const [y, m, d] = value.split('-').map(Number);
  return iso(new Date(y, m - 1, d - 1));
};

function Total({ label, value, strong }: { label: string; value: number; strong?: boolean }) {
  return (
    <div className="rounded-md border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className={strong ? 'text-2xl font-bold' : 'text-lg font-semibold'}>{n(value).toLocaleString()}</div>
    </div>
  );
}

/**
 * Leave owed as at a date — round 5, lane C6 (decision A7).
 *
 * Finance asked what the organisation owes in annual leave that has been earned and not taken, at a
 * date — the financial year end, for its books. This gives it in DAYS, per employee on the books at
 * that date; Finance puts the money on them.
 *
 * Owed is built up, plus carried in, plus HR's adjustments, less what has been taken and what was
 * paid out instead. Leave that is approved but not yet taken is still owed — the person has not had
 * it — and is shown beside it, as is leave awaiting approval.
 */
export default function LeaveOwedPage() {
  const { toast } = useToast();
  const leaveYear = useLeaveYear();
  const [asOf, setAsOf] = useState(todayIso);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);

  const { data: report, isLoading, isFetching, isError, error } = useQuery({
    queryKey: ['hr', 'leave-owed', asOf],
    queryFn: () => leaveService.getLeaveOwed(asOf || undefined),
    placeholderData: keepPreviousData,
  });

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = report?.rows ?? [];
    if (!term) return all;
    return all.filter((r) =>
      [r.employeeName, r.staffNumber, r.organizationUnitName]
        .filter(Boolean)
        .some((v) => v!.toLowerCase().includes(term)),
    );
  }, [report, search]);

  const pages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const current = Math.min(page, pages);
  const shown = rows.slice((current - 1) * PAGE_SIZE, current * PAGE_SIZE);

  const exportCsv = async () => {
    setExporting(true);
    try {
      saveBlob(await leaveService.exportLeaveOwed(asOf || undefined), `leave-owed-${asOf}.csv`);
    } catch (e: any) {
      toast({
        title: 'Export failed',
        description: e?.message || 'The report could not be exported.',
        variant: 'destructive',
      });
    } finally {
      setExporting(false);
    }
  };

  // The last leave year ended the day before this one started — whatever month it starts in.
  const lastYearEnd = dayBefore(leaveYear.startDate);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave owed"
        description="Annual leave built up and not yet taken, as at a date. Days only — Finance puts the money on them."
        backHref="/hr/leave/balances"
        actions={
          <Button variant="outline" onClick={exportCsv} disabled={exporting || !report}>
            {exporting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Download className="mr-2 h-4 w-4" />
            )}
            Export CSV
          </Button>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-4 pt-6">
          <div className="space-y-2">
            <Label htmlFor="owed-as-at">As at</Label>
            <Input
              id="owed-as-at"
              type="date"
              value={asOf}
              onChange={(e) => {
                setAsOf(e.target.value || todayIso());
                setPage(1);
              }}
              className="w-[170px]"
            />
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="ghost" size="sm" onClick={() => setAsOf(todayIso())}>
              Today
            </Button>
            <Button variant="ghost" size="sm" onClick={() => setAsOf(leaveYear.endDate)}>
              End of this leave year ({fmtDay(leaveYear.endDate)})
            </Button>
            <Button variant="ghost" size="sm" onClick={() => setAsOf(lastYearEnd)}>
              End of last leave year ({fmtDay(lastYearEnd)})
            </Button>
          </div>
          {isFetching && !isLoading && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
        </CardContent>
      </Card>

      {isError ? (
        <Card>
          <CardContent className="pt-6 text-sm text-destructive">
            {(error as Error | undefined)?.message || 'The report could not be worked out.'}
          </CardContent>
        </Card>
      ) : isLoading || !report ? (
        <Skeleton className="h-64" />
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle>
                {report.leaveTypeName} owed as at {fmtDay(report.asOf)}
              </CardTitle>
              <p className="text-sm text-muted-foreground">
                Leave year {report.year} ({fmtDay(report.yearStart)} – {fmtDay(report.yearEnd)}) ·{' '}
                {report.totals.employees.toLocaleString()} employees on the books at that date.
              </p>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-3 md:grid-cols-4 lg:grid-cols-7">
                <Total label="Built up" value={report.totals.builtUpDays} />
                <Total label="Carried in" value={report.totals.carriedInDays} />
                <Total label="Adjustments" value={report.totals.adjustmentDays} />
                <Total label="Taken" value={report.totals.takenDays} />
                <Total label="Cashed in" value={report.totals.cashedInDays} />
                <Total label="Owed" value={report.totals.owedDays} strong />
                <Total label="…of which booked" value={report.totals.bookedDays} />
              </div>
              <ul className="list-disc space-y-1 pl-5 text-xs text-muted-foreground">
                <li>
                  <strong>Owed</strong> = built up + carried in + adjustments − taken − cashed in.
                  Approved leave that has not started is still owed (<em>booked</em>); leave
                  awaiting approval is too.
                </li>
                <li>
                  <strong>Built up</strong> is what has accrued by the date; somebody still in their
                  qualifying months has built up nothing.
                </li>
                <li>
                  <strong>Taken</strong> counts approved leave on or before the date; leave that runs
                  past it counts only its days up to it.
                </li>
                {report.carryOverExpiresOn && (
                  <li>
                    <strong>Carried in</strong> days are usable until{' '}
                    {fmtDay(report.carryOverExpiresOn)}; after that only those taken by then count.
                  </li>
                )}
                <li>
                  Everybody on the books at the date: hired on or before it, and still serving or
                  gone only since.
                </li>
              </ul>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3 space-y-0">
              <CardTitle>Employees</CardTitle>
              <Input
                placeholder="Search name, staff number or unit"
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
                className="w-[280px]"
              />
            </CardHeader>
            <CardContent>
              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead className="text-right">Entitled</TableHead>
                      <TableHead className="text-right">Built up</TableHead>
                      <TableHead className="text-right">Carried in</TableHead>
                      <TableHead className="text-right">Adjust.</TableHead>
                      <TableHead className="text-right">Taken</TableHead>
                      <TableHead className="text-right">Cashed in</TableHead>
                      <TableHead className="text-right">Owed</TableHead>
                      <TableHead className="text-right">Booked</TableHead>
                      <TableHead className="text-right">Awaiting</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {shown.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={10}>
                          <EmptyState
                            icon={Wallet}
                            title={search ? 'Nobody matches' : 'Nobody on the books at that date'}
                            description={
                              search
                                ? 'Try a shorter search.'
                                : 'The report lists employees hired on or before the date.'
                            }
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      shown.map((r) => (
                        <TableRow key={r.employeeId}>
                          <TableCell className="font-medium">
                            {r.employeeName}
                            <span className="block text-xs font-normal text-muted-foreground">
                              {[r.staffNumber, r.organizationUnitName].filter(Boolean).join(' · ')}
                              {r.leftOn ? ` · left ${fmtDay(r.leftOn)}` : ''}
                            </span>
                          </TableCell>
                          <TableCell className="text-right">{n(r.entitledDays)}</TableCell>
                          <TableCell className="text-right">{n(r.builtUpDays)}</TableCell>
                          <TableCell className="text-right">{n(r.carriedInDays)}</TableCell>
                          <TableCell className="text-right">{n(r.adjustmentDays)}</TableCell>
                          <TableCell className="text-right">{n(r.takenDays)}</TableCell>
                          <TableCell className="text-right">{n(r.cashedInDays)}</TableCell>
                          <TableCell className="text-right font-semibold">{n(r.owedDays)}</TableCell>
                          <TableCell className="text-right text-muted-foreground">{n(r.bookedDays)}</TableCell>
                          <TableCell className="text-right text-muted-foreground">
                            {n(r.awaitingApprovalDays)}
                          </TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
              </div>
              {pages > 1 && (
                <div className="mt-3 flex items-center justify-end gap-2 text-sm">
                  <span className="text-muted-foreground">
                    {(current - 1) * PAGE_SIZE + 1}–{Math.min(current * PAGE_SIZE, rows.length)} of{' '}
                    {rows.length.toLocaleString()}
                  </span>
                  <Button variant="outline" size="sm" disabled={current <= 1} onClick={() => setPage(current - 1)}>
                    Previous
                  </Button>
                  <Button variant="outline" size="sm" disabled={current >= pages} onClick={() => setPage(current + 1)}>
                    Next
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}
