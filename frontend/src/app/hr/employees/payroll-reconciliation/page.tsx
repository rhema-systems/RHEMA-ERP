'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { RefreshCw, Scale } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { employeeService } from '@/services/hr/employee.service';
import {
  PAYROLL_ISSUE_LABELS,
  offPayrollReasonLabel,
  type PayrollReconciliationIssue,
} from '@/types/hr/employee';

type Filter = 'all' | PayrollReconciliationIssue;

const ISSUE_ORDER: PayrollReconciliationIssue[] = [
  'StillActiveInPayroll',
  'AwaitingPayrollSetup',
  'InactiveInPayroll',
  'BasicPayMismatch',
  'NoPayBasis',
];

const ISSUE_HINT: Record<PayrollReconciliationIssue, string> = {
  StillActiveInPayroll:
    'HR took these people off payroll but Payroll still has them active — the next run will pay them. Payroll switches them off; HR does not reach into Payroll to do it.',
  AwaitingPayrollSetup:
    'HR says they are paid through the run but Payroll has no profile. Saving the employee again with "On payroll" ticked creates one; otherwise set them up in Payroll → Employee profiles.',
  InactiveInPayroll:
    'HR says on payroll; Payroll has switched their profile off. One side is stale — agree which.',
  NoPayBasis:
    'On payroll with no salary and no graded notch on either side. A run skips a zero basis silently, so this is the case nobody notices until payday.',
  BasicPayMismatch:
    "On the salary scale and placed on a notch, but Payroll's active basis is a different amount — the run pays Payroll's figure while HR's placement says another, every month until one side is corrected. Not raised for negotiated pay, where Payroll's figure is the basis.",
};

const money = (v?: number | null) =>
  v == null ? '—' : new Intl.NumberFormat('en-GH', { style: 'currency', currency: 'GHS' }).format(v);

/**
 * HR's "on payroll" statement beside Payroll's own employee profile. The two are different owners'
 * facts and are deliberately not collapsed into one column; this screen lists where they disagree.
 */
export default function PayrollReconciliationPage() {
  const router = useRouter();
  const [filter, setFilter] = useState<Filter>('all');

  const { data, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['hr', 'employees', 'payroll-reconciliation'],
    queryFn: () => employeeService.getPayrollReconciliation(),
  });

  const rows = useMemo(
    () => (data?.rows ?? []).filter((r) => filter === 'all' || r.issue === filter),
    [data, filter],
  );

  const counts: Record<PayrollReconciliationIssue, number> = {
    StillActiveInPayroll: data?.stillActiveInPayroll ?? 0,
    AwaitingPayrollSetup: data?.awaitingPayrollSetup ?? 0,
    InactiveInPayroll: data?.inactiveInPayroll ?? 0,
    NoPayBasis: data?.noPayBasis ?? 0,
    BasicPayMismatch: data?.basicPayMismatch ?? 0,
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Payroll reconciliation"
        description="Where HR's on-payroll statement and Payroll's employee profiles disagree."
        actions={
          <Button variant="outline" onClick={() => refetch()} disabled={isFetching}>
            <RefreshCw className={`mr-2 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} /> Refresh
          </Button>
        }
      />

      <div className="grid grid-cols-2 gap-3 md:grid-cols-7">
        <Tile label="On payroll (HR)" value={data?.onPayrollCount} active={false} />
        <Tile label="Not on payroll (HR)" value={data?.offPayrollCount} active={false} />
        {ISSUE_ORDER.map((issue) => (
          <Tile
            key={issue}
            label={PAYROLL_ISSUE_LABELS[issue]}
            value={counts[issue]}
            active={filter === issue}
            warn={counts[issue] > 0}
            onClick={() => setFilter(filter === issue ? 'all' : issue)}
          />
        ))}
      </div>

      {filter !== 'all' && (
        <p className="rounded-md border bg-muted/40 px-3 py-2 text-sm text-muted-foreground">
          {ISSUE_HINT[filter]}
        </p>
      )}

      <Card>
        <CardHeader>
          <CardTitle>
            {filter === 'all' ? 'All discrepancies' : PAYROLL_ISSUE_LABELS[filter]}
            {data && (
              <span className="ml-2 text-sm font-normal text-muted-foreground">
                {rows.length} of {data.rows.length}
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Position</TableHead>
                  <TableHead>HR says</TableHead>
                  <TableHead>Payroll has</TableHead>
                  <TableHead className="text-right">HR basic pay</TableHead>
                  <TableHead>Issue</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(6)].map((__, j) => (
                        <TableCell key={j}><Skeleton className="h-4 w-[120px]" /></TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={Scale}
                        title="Nothing to reconcile"
                        description={
                          filter === 'all'
                            ? "Every live employee's payroll membership agrees with Payroll's profile."
                            : 'No employees in this category.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((r) => (
                    <TableRow
                      key={r.employeeId}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/employees/${r.employeeId}`)}
                    >
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{r.fullName}</span>
                          <span className="text-xs text-muted-foreground">
                            {r.employeeNumber} · {r.employmentType} · {r.staffStatus}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        <div className="flex flex-col">
                          <span>{r.positionTitle || '—'}</span>
                          <span className="text-xs">{r.organizationUnitName || ''}</span>
                        </div>
                      </TableCell>
                      <TableCell>
                        {r.isOnPayroll ? (
                          'On payroll'
                        ) : (
                          <div className="flex flex-col">
                            <span>Not on payroll</span>
                            <span className="text-xs text-muted-foreground">
                              {offPayrollReasonLabel(r.offPayrollReason)}
                            </span>
                          </div>
                        )}
                      </TableCell>
                      <TableCell>
                        {!r.hasPayrollProfile
                          ? 'No profile'
                          : r.payrollActive
                            ? 'Profile · active'
                            : 'Profile · switched off'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">{money(r.hrMonthlyBasicPay)}</TableCell>
                      <TableCell>
                        <span className="inline-flex items-center rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-900 dark:bg-amber-900 dark:text-amber-100">
                          {PAYROLL_ISSUE_LABELS[r.issue]}
                        </span>
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

function Tile({
  label,
  value,
  active,
  warn = false,
  onClick,
}: {
  label: string;
  value?: number;
  active: boolean;
  warn?: boolean;
  onClick?: () => void;
}) {
  const base = 'rounded-lg border p-3 text-left transition-colors';
  const tone = active
    ? 'border-primary bg-primary/10'
    : warn
      ? 'border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950'
      : 'bg-card';
  const Comp = onClick ? 'button' : 'div';
  return (
    <Comp className={`${base} ${tone} ${onClick ? 'hover:bg-muted/60' : ''}`} onClick={onClick} type={onClick ? 'button' : undefined}>
      <div className="text-2xl font-semibold tabular-nums">{value ?? '—'}</div>
      <div className="text-xs text-muted-foreground">{label}</div>
    </Comp>
  );
}
