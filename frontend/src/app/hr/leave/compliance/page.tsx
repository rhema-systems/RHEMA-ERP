'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Download, Loader2, ShieldCheck } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
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
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
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

const currentYear = new Date().getFullYear();
const years = [currentYear, currentYear - 1, currentYear - 2];

/**
 * Mandatory-leave compliance: who still owes statutory leave days this year. Driven by the
 * `mandatoryAnnualLeave` flag on a leave type.
 */
export default function LeaveCompliancePage() {
  const [year, setYear] = useState(String(currentYear));
  const [exporting, setExporting] = useState(false);
  const [unit, setUnit] = useState('all');
  const [status, setStatus] = useState('all');
  const { toast } = useToast();

  // The compliance register is the one leave screen that is purely a list of people who owe
  // something, and it had no way out of the browser at all (L-22 / R-11).
  const exportCsv = async () => {
    setExporting(true);
    try {
      const blob = await leaveService.exportCompliance(Number(year));
      saveBlob(blob, `leave-compliance-${year}.csv`);
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

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'leave-compliance', year],
    queryFn: () => leaveService.getMandatoryCompliance(Number(year)),
  });

  const allRows = data ?? [];

  // Distinct units present in the data, so the filter only offers what is actually there.
  const units = Array.from(
    new Set(allRows.map((r) => r.organizationUnitName).filter(Boolean) as string[]),
  ).sort();

  const rows = allRows.filter(
    (r) =>
      (unit === 'all' || r.organizationUnitName === unit) &&
      (status === 'all' || r.status === status),
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Mandatory Leave Compliance"
        description="Employees who have not yet taken their required leave."
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
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="w-40 space-y-2">
            <Label htmlFor="compliance-year">Year</Label>
            <Select value={year} onValueChange={setYear}>
              <SelectTrigger id="compliance-year">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {years.map((y) => (
                  <SelectItem key={y} value={String(y)}>
                    {y}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/*
            ⚠ L-22. The register listed everybody with no way to narrow it and no way out of a
            row, so it answered "who is outstanding" and nothing else. Outstanding mandatory leave
            is acted on by a DEPARTMENT — it is the head who has to release people — and the next
            move from a row is either to look at the person or to book the leave for them.

            Filtered on the client: the whole register is already loaded for the export, so a
            round trip per filter change would be slower and no more correct.
          */}
          <div className="w-64 space-y-2">
            <Label htmlFor="compliance-unit">Organisation unit</Label>
            <Select value={unit} onValueChange={setUnit}>
              <SelectTrigger id="compliance-unit">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All units</SelectItem>
                {units.map((u) => (
                  <SelectItem key={u} value={u}>{u}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="w-48 space-y-2">
            <Label htmlFor="compliance-status">Status</Label>
            <Select value={status} onValueChange={setStatus}>
              <SelectTrigger id="compliance-status">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="Outstanding">Outstanding</SelectItem>
                <SelectItem value="Scheduled">Scheduled</SelectItem>
                <SelectItem value="Compliant">Compliant</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <p className="pb-2 text-sm text-muted-foreground">
            {rows.length} of {allRows.length} row(s)
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Compliance</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Unit</TableHead>
                  <TableHead>Leave type</TableHead>
                  <TableHead className="text-right">Entitled</TableHead>
                  <TableHead className="text-right">Taken</TableHead>
                  <TableHead className="text-right">Scheduled</TableHead>
                  <TableHead className="text-right">Outstanding</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(9)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[70px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={9}>
                      <EmptyState
                        icon={ShieldCheck}
                        title="Nothing to report"
                        description="No leave type is flagged as mandatory, or everyone is compliant."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((c) => (
                    <TableRow key={`${c.employeeId}-${c.leaveTypeId}`}>
                      <TableCell className="font-medium">
                        {/* A row that names somebody should take you to them. */}
                        <Link
                          href={`/hr/employees/${c.employeeId}`}
                          className="hover:underline"
                        >
                          {c.employeeName}
                        </Link>
                        <div className="text-xs text-muted-foreground">{c.employeeNumber}</div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {c.organizationUnitName ?? '—'}
                      </TableCell>
                      <TableCell>{c.leaveTypeName}</TableCell>
                      <TableCell className="text-right">{c.entitledDays}</TableCell>
                      <TableCell className="text-right">{c.takenDays}</TableCell>
                      <TableCell className="text-right">{c.scheduledDays}</TableCell>
                      <TableCell className="text-right font-medium">
                        <span className={c.outstandingDays > 0 ? 'text-amber-600' : undefined}>
                          {c.outstandingDays}
                        </span>
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={c.status} />
                      </TableCell>
                      <TableCell className="text-right">
                        {/*
                          The move after reading this register is to book the leave. Prefilled with
                          the employee and the leave type, so the desk does not re-key what the row
                          already says.
                        */}
                        {c.outstandingDays > 0 && (
                          <Button variant="ghost" size="sm" asChild>
                            <Link
                              href={`/hr/leave/requests/new?employeeId=${c.employeeId}&leaveTypeId=${c.leaveTypeId}`}
                            >
                              Book leave
                            </Link>
                          </Button>
                        )}
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
