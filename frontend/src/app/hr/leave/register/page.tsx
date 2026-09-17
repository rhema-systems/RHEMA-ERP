'use client';

/**
 * The organisation-wide leave register — closure plan slice E3 (L-6, L-18, R-11).
 *
 * Until this existed the only way to list leave requests was one employee at a time, so "who is off
 * in December" or "every rejected request this quarter" could not be asked at all. The Requests
 * screen keeps its per-employee history; this is the register that reads across everybody.
 */

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Download, Loader2, Search, ClipboardList } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { leaveService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { LEAVE_STATUS_OPTIONS, type LeaveStatus } from '@/types/hr/leave-request';

const ALL = '__all__';
const currentYear = new Date().getFullYear();

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

export default function LeaveRegisterPage() {
  const router = useRouter();
  const { toast } = useToast();

  const [from, setFrom] = useState(`${currentYear}-01-01`);
  const [to, setTo] = useState(`${currentYear}-12-31`);
  const [status, setStatus] = useState(ALL);
  const [leaveTypeId, setLeaveTypeId] = useState(ALL);
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [exporting, setExporting] = useState(false);

  const filter = {
    from: from || undefined,
    to: to || undefined,
    status: status === ALL ? undefined : (status as LeaveStatus),
    leaveTypeId: leaveTypeId === ALL ? undefined : leaveTypeId,
    employeeId: employeeId ?? undefined,
    search: search.trim() || undefined,
  };

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'leave-register', filter, page],
    queryFn: () => leaveService.getRegister(filter, page, 25),
  });

  const rows = data?.items ?? [];
  const total = data?.totalCount ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / 25));

  // Every filter change resets to page 1 — otherwise a narrower filter lands on a page that no
  // longer exists and the register reads as empty.
  const onFilter = (apply: () => void) => {
    apply();
    setPage(1);
  };

  const exportCsv = async () => {
    setExporting(true);
    try {
      const blob = await leaveService.exportRegister(filter);
      saveBlob(blob, `leave-register-${new Date().toISOString().slice(0, 10)}.csv`);
    } catch (e: any) {
      toast({
        title: 'Export failed',
        description: e?.message || 'The register could not be exported.',
        variant: 'destructive',
      });
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Register"
        description="Every leave request across the organisation."
        actions={
          <Button variant="outline" onClick={exportCsv} disabled={exporting}>
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
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="space-y-2">
              <label className="text-sm font-medium">From</label>
              <Input
                type="date"
                value={from}
                onChange={(e) => onFilter(() => setFrom(e.target.value))}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">To</label>
              <Input
                type="date"
                value={to}
                onChange={(e) => onFilter(() => setTo(e.target.value))}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Status</label>
              <Select value={status} onValueChange={(v) => onFilter(() => setStatus(v))}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All statuses</SelectItem>
                  {LEAVE_STATUS_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Leave type</label>
              <Select value={leaveTypeId} onValueChange={(v) => onFilter(() => setLeaveTypeId(v))}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL}>All types</SelectItem>
                  {(leaveTypes ?? []).map((t) => (
                    <SelectItem key={t.id} value={t.id}>
                      {t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Employee</label>
              <EmployeePicker
                value={employeeId}
                onChange={(v) => onFilter(() => setEmployeeId(v))}
              />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">Search</label>
              <div className="relative">
                <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  className="pl-8"
                  placeholder="Request number, name or staff number"
                  value={search}
                  onChange={(e) => onFilter(() => setSearch(e.target.value))}
                />
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex-row items-center justify-between pb-3">
          <CardTitle className="text-base">
            {isLoading ? 'Loading…' : `${total} request${total === 1 ? '' : 's'}`}
          </CardTitle>
          {isFetching && !isLoading && (
            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
          )}
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Leave type</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead>To</TableHead>
                  <TableHead className="text-right">Days</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(6)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[70px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={ClipboardList}
                        title="No leave matches those filters"
                        description="Widen the dates, or clear a filter."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((r) => (
                    <TableRow
                      key={r.id}
                      className="cursor-pointer"
                      onClick={() => router.push(`/hr/leave/requests/${r.id}`)}
                    >
                      <TableCell className="font-medium">{r.requestNumber}</TableCell>
                      <TableCell>
                        {r.employeeName}
                        <span className="block text-xs text-muted-foreground">
                          {r.employeeNumber}
                        </span>
                      </TableCell>
                      <TableCell>
                        {r.leaveTypeName}
                        {r.leaveSubTypeName ? ` · ${r.leaveSubTypeName}` : ''}
                      </TableCell>
                      <TableCell>{r.startDate?.slice(0, 10)}</TableCell>
                      <TableCell>{r.endDate?.slice(0, 10)}</TableCell>
                      <TableCell className="text-right">{r.totalDays}</TableCell>
                      <TableCell>
                        <StatusBadge status={r.status} />
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {pageCount > 1 && (
            <div className="mt-4 flex items-center justify-between text-sm">
              <span className="text-muted-foreground">
                Page {page} of {pageCount}
              </span>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => p - 1)}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= pageCount}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
