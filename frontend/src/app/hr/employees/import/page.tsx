'use client';

/**
 * Employee bulk imports — the history, and the door to a new one.
 *
 * An import is checked first and committed second, so a whole workbook can be reviewed — and its
 * bad rows fixed or skipped — before anything is written to the register. This list is every
 * session the tenant has run; the per-session page is where the review, commit and follow-up live.
 *
 * Write-tier, the same permission as the create form: loading and amending employee records is
 * what the write tier means, and the HR role holds write but not admin.
 */

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Download, FileSpreadsheet, Loader2, Upload } from 'lucide-react';
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
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { employeeImportService } from '@/services/hr/employee-import.service';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { SessionStatusBadge } from '@/components/hr/employee-import/SessionStatusBadge';
import type { EmployeeImportSessionSummary } from '@/types/hr/employee-import';

const SESSIONS_KEY = ['hr', 'employee-import', 'sessions'] as const;

export default function EmployeeImportSessionsPage() {
  const router = useRouter();
  const { toast } = useToast();
  const [downloading, setDownloading] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: SESSIONS_KEY,
    queryFn: () => employeeImportService.list(),
  });
  const sessions: EmployeeImportSessionSummary[] = data ?? [];

  const downloadTemplate = async () => {
    setDownloading(true);
    try {
      await employeeImportService.downloadTemplate();
    } catch (error: any) {
      toast({ title: 'Template not downloaded', description: error?.message ?? 'Please try again.', variant: 'destructive' });
    } finally {
      setDownloading(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee Import"
        description="Load many employees from the system's own Excel template. Every row is checked before anything is written."
        backHref="/hr/employees"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={downloadTemplate} disabled={downloading}>
              {downloading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Download className="mr-2 h-4 w-4" />}
              Download template
            </Button>
            <Button onClick={() => router.push('/hr/employees/import/new')}>
              <Upload className="mr-2 h-4 w-4" /> New import
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>
            {sessions.length} {sessions.length === 1 ? 'import' : 'imports'}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-2">
              {[0, 1, 2].map((i) => <Skeleton key={i} className="h-10 w-full" />)}
            </div>
          ) : sessions.length === 0 ? (
            <EmptyState
              icon={FileSpreadsheet}
              title="No imports yet"
              description="Download the template, fill it in, and upload it here. Nothing is written until you confirm."
              action={
                <Button onClick={() => router.push('/hr/employees/import/new')}>
                  <Upload className="mr-2 h-4 w-4" /> New import
                </Button>
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reference</TableHead>
                  <TableHead>File</TableHead>
                  <TableHead>Uploaded</TableHead>
                  <TableHead className="text-right">Rows</TableHead>
                  <TableHead className="text-right">Ready</TableHead>
                  <TableHead className="text-right">Warnings</TableHead>
                  <TableHead className="text-right">Errors</TableHead>
                  <TableHead className="text-right">Created</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {sessions.map((s) => (
                  <TableRow
                    key={s.id}
                    className="cursor-pointer"
                    onClick={() => router.push(`/hr/employees/import/${s.id}`)}
                  >
                    <TableCell className="font-medium">{s.reference}</TableCell>
                    <TableCell className="max-w-[260px] truncate" title={s.fileName}>{s.fileName}</TableCell>
                    <TableCell>
                      <div>{formatDateTime(s.uploadedOn)}</div>
                      <div className="text-xs text-muted-foreground">{s.uploadedByName ?? '—'}</div>
                    </TableCell>
                    <TableCell className="text-right">{s.totalRows}</TableCell>
                    <TableCell className="text-right">{s.readyCount}</TableCell>
                    <TableCell className="text-right">{s.warningCount}</TableCell>
                    <TableCell className="text-right">{s.errorCount}</TableCell>
                    <TableCell className="text-right">{s.committedCount}</TableCell>
                    <TableCell><SessionStatusBadge status={s.status} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
