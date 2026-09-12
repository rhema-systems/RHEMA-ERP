'use client';

import { useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Search, ShieldCheck, ShieldAlert, AlertTriangle, Users } from 'lucide-react';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { trainingComplianceService } from '@/services/hr/training-compliance.service';
import { COMPLIANCE_STATUS_OPTIONS } from '@/types/hr/training-compliance';

const statusLabel = (v: string) => COMPLIANCE_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * The org-wide compliance position.
 *
 * Overdue is a strict subset of non-compliant — someone can be non-compliant without being late
 * (newly assigned, never due yet), so the two tabs are not the same list and the tiles say so.
 */
export default function ComplianceDashboardPage() {
  const router = useRouter();
  const [view, setView] = useState<'overdue' | 'non-compliant' | 'employee'>('overdue');
  const [search, setSearch] = useState('');

  const pickerForm = useForm<{ employeeId: string }>({ defaultValues: { employeeId: '' } });
  const selectedEmployee = pickerForm.watch('employeeId');

  const { data: overdue, isLoading: loadingOverdue } = useQuery({
    queryKey: ['hr', 'training', 'compliance', 'overdue'],
    queryFn: () => trainingComplianceService.getOverdue(),
  });
  const { data: nonCompliant, isLoading: loadingNon } = useQuery({
    queryKey: ['hr', 'training', 'compliance', 'non-compliant'],
    queryFn: () => trainingComplianceService.getNonCompliant(),
  });
  const { data: employeeRecords, isLoading: loadingEmp } = useQuery({
    queryKey: ['hr', 'training', 'compliance', 'employee', selectedEmployee],
    queryFn: () => trainingComplianceService.getRecordsForEmployee(selectedEmployee),
    enabled: view === 'employee' && !!selectedEmployee,
  });

  const active =
    view === 'overdue' ? overdue ?? [] : view === 'non-compliant' ? nonCompliant ?? [] : employeeRecords ?? [];
  const isLoading =
    view === 'overdue' ? loadingOverdue : view === 'non-compliant' ? loadingNon : loadingEmp;

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase();
    const all = active as any[];
    if (!term) return all;
    return all.filter(
      (r) =>
        (r.employeeName ?? '').toLowerCase().includes(term) ||
        (r.requirementName ?? '').toLowerCase().includes(term) ||
        (r.programName ?? '').toLowerCase().includes(term),
    );
  }, [active, search]);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Training Compliance"
        description="Who is behind on mandatory training, and how far."
        backHref="/hr/training"
      />

      <MetricTiles
        tiles={[
          {
            label: 'Overdue',
            value: overdue?.length ?? 0,
            hint: 'Past the due date',
            icon: AlertTriangle,
            tone: (overdue?.length ?? 0) > 0 ? 'danger' : 'default',
          },
          {
            label: 'Non-compliant',
            value: nonCompliant?.length ?? 0,
            hint: 'Includes those not yet due',
            icon: ShieldAlert,
            tone: (nonCompliant?.length ?? 0) > 0 ? 'warning' : 'default',
          },
          {
            label: 'Not yet due',
            value: Math.max((nonCompliant?.length ?? 0) - (overdue?.length ?? 0), 0),
            hint: 'Assigned, still in time',
          },
          {
            label: 'Requirements',
            value: 'Configure',
            hint: 'Administration → HR → Training',
          },
        ]}
      />

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Compliance position</CardTitle>
              <CardDescription>
                Overdue is the subset of non-compliant that has passed its due date.
              </CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Tabs value={view} onValueChange={(v) => setView(v as typeof view)}>
                <TabsList>
                  <TabsTrigger value="overdue">Overdue</TabsTrigger>
                  <TabsTrigger value="non-compliant">Non-compliant</TabsTrigger>
                  <TabsTrigger value="employee">By employee</TabsTrigger>
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
        <CardContent className="space-y-4">
          {view === 'employee' && (
            <div className="max-w-sm">
              <EmployeePickerField form={pickerForm} name="employeeId" label="Employee" />
            </div>
          )}

          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Requirement</TableHead>
                  <TableHead>Satisfied by</TableHead>
                  <TableHead>Next due</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(5)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : view === 'employee' && !selectedEmployee ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={Users}
                        title="Pick an employee"
                        description="Choose someone above to see everything they must hold."
                      />
                    </TableCell>
                  </TableRow>
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={ShieldCheck}
                        title={view === 'overdue' ? 'Nothing overdue' : 'Everyone is compliant'}
                        description={
                          view === 'overdue'
                            ? 'No mandatory training has passed its due date.'
                            : 'No outstanding compliance requirements.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((r) => (
                    <TableRow
                      key={r.id}
                      className={r.requirementId ? 'cursor-pointer hover:bg-muted/50' : undefined}
                      onClick={() =>
                        r.requirementId &&
                        router.push(`/administration/hr/training/compliance/${r.requirementId}`)
                      }
                    >
                      <TableCell className="font-medium">{r.employeeName}</TableCell>
                      <TableCell>{r.requirementName}</TableCell>
                      <TableCell className="text-muted-foreground">{r.programName || '—'}</TableCell>
                      <TableCell className="text-muted-foreground">{fmt(r.nextDueDate)}</TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1.5">
                          <StatusBadge status={statusLabel(r.status)} />
                          {r.isOverdue && (
                            <Badge variant="destructive" className="text-[10px]">
                              Overdue
                            </Badge>
                          )}
                          {r.isExempt && (
                            <Badge variant="outline" className="text-[10px]">
                              Exempt
                            </Badge>
                          )}
                        </div>
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
