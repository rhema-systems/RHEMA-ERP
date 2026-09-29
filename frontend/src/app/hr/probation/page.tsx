'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  CalendarClock,
  ClipboardCheck,
  Loader2,
  Plus,
  Search,
  UserCheck,
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
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { HR_ADMIN_ROLES } from '@/components/hr/common/PermissionGate';
import { ImportedConfirmationRepairDialog } from '@/components/hr/probation/ImportedConfirmationRepairDialog';
import { useAuth } from '@/hooks/use-auth';
import { probationService } from '@/services/hr/probation.service';
import type { ProbationPeriodSummary, ProbationStatus } from '@/types/hr/probation';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * ⚠ Five statuses, and three of them mean "still unconfirmed". Colouring by position in the enum
 * would put PendingConfirmation and ConfirmationApproved in the wrong camp: they are in flight, not
 * finished, and a register that shows them as done would hide the work still owed.
 */
const STATUS_TONE: Record<ProbationStatus, string> = {
  Active: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  PendingConfirmation: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  ConfirmationApproved: 'bg-violet-100 text-violet-800 dark:bg-violet-900/40 dark:text-violet-200',
  Completed: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  Terminated: 'bg-muted text-muted-foreground',
};

const STATUS_LABEL: Record<ProbationStatus, string> = {
  Active: 'Running',
  PendingConfirmation: 'With the authority',
  ConfirmationApproved: 'Approved — awaiting HR',
  Completed: 'Confirmed',
  Terminated: 'Terminated',
};

const STATUSES: ProbationStatus[] = [
  'Active',
  'PendingConfirmation',
  'ConfirmationApproved',
  'Completed',
  'Terminated',
];

/** Days until the end date; negative once it has passed. */
const daysLeft = (endDate: string) =>
  Math.round((new Date(endDate).getTime() - Date.now()) / 86_400_000);

export default function ProbationRegisterPage() {
  const [status, setStatus] = useState<ProbationStatus | 'all'>('Active');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const [repairing, setRepairing] = useState(false);

  // The repair is HR.Probation.Admin, like confirming one probation; the HR role holds Read and
  // Write only. Hidden, not a boundary — the API refuses it regardless.
  const { hasAnyPermission, hasAnyRole } = useAuth();
  const canAdminister = hasAnyPermission(['HR.Probation.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const { data, isLoading } = useQuery({
    queryKey: ['probations', status, search, page],
    queryFn: () =>
      probationService.getPaged({
        page,
        pageSize,
        status: status === 'all' ? undefined : status,
        search: search.trim() || undefined,
      }),
  });

  const rows: ProbationPeriodSummary[] = data?.items ?? [];
  const total = data?.totalCount ?? 0;
  const pages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Probation & confirmation"
        description="Who is on probation, when it ends, and what is still owed before it can be confirmed."
        backHref="/hr"
        actions={
          <div className="flex gap-2">
            {/* HR finish plan lane 11: the imported workforce, put on probations that had ended. */}
            {canAdminister && (
              <Button variant="outline" onClick={() => setRepairing(true)}>
                <UserCheck className="mr-2 h-4 w-4" />
                Confirm imported staff
              </Button>
            )}
            <Button variant="outline" asChild>
              <Link href="/hr/probation/reviews">
                <ClipboardCheck className="mr-2 h-4 w-4" />
                My reviews
              </Link>
            </Button>
            <Button asChild>
              <Link href="/hr/probation/new">
                <Plus className="mr-2 h-4 w-4" />
                Open a probation
              </Link>
            </Button>
          </div>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-center gap-3 p-4">
          <div className="relative min-w-[16rem] flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-9"
              placeholder="Search by name or employee number"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </div>
          <Select
            value={status}
            onValueChange={(v) => {
              setStatus(v as ProbationStatus | 'all');
              setPage(1);
            }}
          >
            <SelectTrigger className="w-64">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {STATUSES.map((s) => (
                <SelectItem key={s} value={s}>
                  {STATUS_LABEL[s]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={UserCheck}
              title="Nothing here"
              description={
                search
                  ? 'No probation matches that search.'
                  : status === 'Active'
                    ? 'Nobody is currently on probation. New hires appear here once their probation is opened.'
                    : 'No probation has this status yet.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Started</TableHead>
                  <TableHead>Ends</TableHead>
                  <TableHead className="text-right">Length</TableHead>
                  <TableHead className="text-right">Reviews</TableHead>
                  <TableHead className="text-right">Extensions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => {
                  const left = daysLeft(row.currentEndDate);
                  const unconfirmed =
                    row.statusName === 'Active' ||
                    row.statusName === 'PendingConfirmation' ||
                    row.statusName === 'ConfirmationApproved';
                  return (
                    <TableRow key={row.id} className="cursor-pointer">
                      <TableCell>
                        <Link href={`/hr/probation/${row.id}`} className="font-medium hover:underline">
                          {row.employeeName}
                        </Link>
                        <div className="text-xs text-muted-foreground">{row.employeeNumber}</div>
                      </TableCell>
                      <TableCell>
                        <Badge className={STATUS_TONE[row.statusName]} variant="secondary">
                          {STATUS_LABEL[row.statusName]}
                        </Badge>
                      </TableCell>
                      <TableCell>{fmtDate(row.startDate)}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          {fmtDate(row.currentEndDate)}
                          {/* ⚠ Only flag an overrun while it is still unconfirmed. A completed
                              probation whose end date has passed is simply history. */}
                          {unconfirmed && left < 0 && (
                            <span className="inline-flex items-center gap-1 text-xs font-medium text-red-600">
                              <AlertTriangle className="h-3 w-3" />
                              {Math.abs(left)}d overdue
                            </span>
                          )}
                          {unconfirmed && left >= 0 && left <= 30 && (
                            <span className="inline-flex items-center gap-1 text-xs text-amber-600">
                              <CalendarClock className="h-3 w-3" />
                              {left}d left
                            </span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">{row.durationMonths} mo</TableCell>
                      <TableCell className="text-right">{row.reviewCount}</TableCell>
                      <TableCell className="text-right">
                        {row.extensionCount > 0 ? row.extensionCount : '—'}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {pages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">
            Page {page} of {pages} · {total} record{total === 1 ? '' : 's'}
          </p>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              Previous
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= pages}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </div>
        </div>
      )}

      <ImportedConfirmationRepairDialog open={repairing} onOpenChange={setRepairing} />
    </div>
  );
}
