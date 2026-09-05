'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  BarChart3,
  DoorOpen,
  Loader2,
  Plus,
  Search,
  ShieldAlert,
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
import { Alert, AlertDescription } from '@/components/ui/alert';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { separationService } from '@/services/hr/separation.service';
import type { SeparationStatus, SeparationType } from '@/types/hr/separation';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * ⚠ Coloured by what the status MEANS, not by its position in the enum. Six of the eleven are "in
 * flight" and look alike to a reader skimming a register — the ones worth separating are the two
 * that mean somebody is waiting on a named person (PendingApproval, SettlementUnderReview) and the
 * two terminal ones.
 */
const STATUS_TONE: Record<SeparationStatus, string> = {
  Draft: 'bg-muted text-muted-foreground',
  PendingApproval: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  Approved: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  ClearanceInProgress: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  ClearanceCompleted: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  SettlementPending: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200',
  SettlementUnderReview: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200',
  SettlementApproved: 'bg-violet-100 text-violet-800 dark:bg-violet-900/40 dark:text-violet-200',
  Completed: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200',
  Cancelled: 'bg-muted text-muted-foreground',
  Rejected: 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200',
};

/** Plain words for what is actually happening, rather than the enum member. */
const STATUS_LABEL: Record<SeparationStatus, string> = {
  Draft: 'Draft',
  PendingApproval: 'Awaiting signature',
  Approved: 'Approved',
  ClearanceInProgress: 'Clearance running',
  ClearanceCompleted: 'Cleared',
  SettlementPending: 'Settlement being prepared',
  SettlementUnderReview: 'With Internal Audit',
  SettlementApproved: 'Cleared for payment',
  Completed: 'Completed',
  Cancelled: 'Withdrawn',
  Rejected: 'Refused',
};

const STATUSES = Object.keys(STATUS_LABEL) as SeparationStatus[];

const TYPE_LABEL: Partial<Record<SeparationType, string>> = {
  VoluntaryResignation: 'Resignation',
  CompulsoryRetirement: 'Retirement (compulsory)',
  VoluntaryRetirement: 'Retirement (voluntary)',
  MedicalRetirement: 'Retirement (medical)',
  ContractExpiry: 'Contract expiry',
  Death: 'Death',
  InvoluntaryRedundancy: 'Redundancy',
  InvoluntaryForCause: 'Termination (conduct)',
  InvoluntaryPerformance: 'Termination (performance)',
  SummaryDismissal: 'Summary dismissal',
  MutualAgreement: 'Mutual agreement',
  Other: 'Other',
};

export default function SeparationsRegisterPage() {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<SeparationStatus | 'all'>('all');
  const [onlyUnapplied, setOnlyUnapplied] = useState(false);
  const [page, setPage] = useState(1);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['separations', { search, status, onlyUnapplied, page }],
    queryFn: () =>
      separationService.getPaged({
        pageNumber: page,
        pageSize: 25,
        search: search.trim() || undefined,
        status: status === 'all' ? undefined : status,
        onlyUnappliedToEmployee: onlyUnapplied || undefined,
      }),
  });

  const items = data?.items ?? [];

  return (
    <div className="space-y-6">
      <PageHeader
        title="Separations"
        description="Everyone leaving the organisation, by any route — resignation, retirement, contract expiry, redundancy, dismissal or death."
        actions={
          <>
            <Button variant="outline" asChild>
              <Link href="/hr/separations/analytics">
                <BarChart3 className="mr-2 h-4 w-4" />
                Analytics
              </Link>
            </Button>
            <Button asChild>
              <Link href="/hr/separations/new">
                <Plus className="mr-2 h-4 w-4" />
                Raise a separation
              </Link>
            </Button>
          </>
        }
      />

      <Card>
        <CardContent className="flex flex-wrap items-end gap-3 pt-6">
          <div className="relative min-w-[240px] flex-1">
            <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-8"
              placeholder="Name, employee number or separation number"
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
              setStatus(v as SeparationStatus | 'all');
              setPage(1);
            }}
          >
            <SelectTrigger className="w-[220px]">
              <SelectValue placeholder="Any status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Any status</SelectItem>
              {STATUSES.map((s) => (
                <SelectItem key={s} value={s}>
                  {STATUS_LABEL[s]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {/*
            ⚠ Not a cosmetic filter. A completed separation whose employee record was never updated
            is the defect this whole area was built to close — somebody dismissed or retired who is
            still counted as staff. It is one click away on purpose.
          */}
          <Button
            variant={onlyUnapplied ? 'default' : 'outline'}
            onClick={() => {
              setOnlyUnapplied((v) => !v);
              setPage(1);
            }}
          >
            <ShieldAlert className="mr-2 h-4 w-4" />
            Not applied to the employee record
          </Button>
        </CardContent>
      </Card>

      {isError && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>
            {(error as Error)?.message ?? 'The register could not be loaded.'}
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center py-16 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading the register…
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={DoorOpen}
              title={onlyUnapplied ? 'Nothing outstanding' : 'No separations'}
              description={
                onlyUnapplied
                  ? 'Every completed separation has been applied to its employee record.'
                  : 'Nobody is currently leaving. Raise a separation when somebody resigns, retires or is dismissed.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Route out</TableHead>
                  <TableHead>Effective</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell className="font-mono text-xs">
                      <Link className="hover:underline" href={`/hr/separations/${s.id}`}>
                        {s.separationNumber}
                      </Link>
                    </TableCell>
                    <TableCell>
                      <div className="font-medium">{s.employeeName}</div>
                      <div className="text-xs text-muted-foreground">
                        {[s.employeeNumber, s.positionTitle, s.organizationUnitName]
                          .filter(Boolean)
                          .join(' · ') || '—'}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div>{TYPE_LABEL[s.separationType] ?? s.separationTypeName}</div>
                      <div className="flex gap-1 pt-1">
                        {s.isDisciplinary && (
                          <Badge variant="outline" className="text-xs">Disciplinary</Badge>
                        )}
                        {s.isSystemInitiated && (
                          <Badge variant="outline" className="text-xs">Raised by the system</Badge>
                        )}
                        {s.isProcedural && (
                          <Badge variant="outline" className="text-xs">Procedural</Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>{fmtDate(s.effectiveDate)}</TableCell>
                    <TableCell>
                      <Badge className={STATUS_TONE[s.status]} variant="secondary">
                        {STATUS_LABEL[s.status] ?? s.statusName}
                      </Badge>
                    </TableCell>
                    <TableCell>
                      {/*
                        Completed but never applied: the employee is still on strength. Shown on the
                        row rather than only behind the filter, because somebody scanning the
                        register should not have to know to look for it.
                      */}
                      {s.status === 'Completed' && !s.employeeRecordUpdated && (
                        <Badge variant="destructive" className="gap-1">
                          <ShieldAlert className="h-3 w-3" />
                          Employee still active
                        </Badge>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Page {data.page} of {data.totalPages} · {data.totalCount} separations
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={!data.hasPrevious} onClick={() => setPage((p) => p - 1)}>
              Previous
            </Button>
            <Button variant="outline" size="sm" disabled={!data.hasNext} onClick={() => setPage((p) => p + 1)}>
              Next
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
