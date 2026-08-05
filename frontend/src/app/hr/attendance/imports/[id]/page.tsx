'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Play } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { bulkImportService } from '@/services/hr/attendance.service';
import { formatDate, formatDateTime, formatTime } from '@/lib/hr/attendance-format';

/**
 * One import batch: its counts, and the rows it staged.
 *
 * Processing is the point of no return — until then the batch is inert, which is what makes
 * reviewing the failed rows first worthwhile.
 */
export default function BulkImportDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [rowFilter, setRowFilter] = useState<'all' | 'failed'>('all');
  const [processing, setProcessing] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: imp, isLoading, isError } = useQuery({
    queryKey: ['hr', 'bulk-imports', id],
    queryFn: () => bulkImportService.getById(id),
    enabled: !!id,
  });

  const { data: rows, isLoading: loadingRows } = useQuery({
    queryKey: ['hr', 'bulk-imports', id, 'rows', rowFilter],
    queryFn: () =>
      rowFilter === 'failed'
        ? bulkImportService.getFailedRows(id)
        : bulkImportService.getRows(id),
    enabled: !!id,
  });

  const runProcess = async () => {
    setBusy(true);
    try {
      const result = await bulkImportService.process(id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'bulk-imports'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'daily-attendance'] });
      toast({
        title: `Import ${result.status}`,
        description: `${result.successCount} of ${result.totalRows} rows applied${
          result.failureCount ? `, ${result.failureCount} failed` : ''
        }.`,
        variant: result.status === 'Failed' ? 'destructive' : undefined,
      });
      setProcessing(false);
      return true;
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to process the import.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !imp) {
    return (
      <div className="p-6">
        <EmptyState title="Import not found" description="It may have been removed." />
      </div>
    );
  }

  const canProcess = imp.status === 'Pending';
  const importRows = rows ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={imp.importReference}
        description={`${imp.sourceType}${imp.sourceFileName ? ` · ${imp.sourceFileName}` : ''} · staged ${formatDateTime(imp.importDate)} by ${imp.importedByName}`}
        backHref="/hr/attendance/imports"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={imp.status} />
            {canProcess && (
              <Button onClick={() => setProcessing(true)}>
                <Play className="mr-2 h-4 w-4" /> Process batch
              </Button>
            )}
          </div>
        }
      />

      <div className="grid gap-3 sm:grid-cols-3">
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">Total rows</p>
            <p className="mt-1 text-2xl font-semibold">{imp.totalRows}</p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">Applied</p>
            <p className="mt-1 text-2xl font-semibold">{imp.successCount}</p>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="p-4">
            <p className="text-xs text-muted-foreground">Failed</p>
            <p
              className={`mt-1 text-2xl font-semibold ${imp.failureCount > 0 ? 'text-red-600' : ''}`}
            >
              {imp.failureCount}
            </p>
          </CardContent>
        </Card>
      </div>

      {imp.errorSummary && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Error summary</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm text-red-600">{imp.errorSummary}</p>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Rows</CardTitle>
            <Select value={rowFilter} onValueChange={(v) => setRowFilter(v as 'all' | 'failed')}>
              <SelectTrigger className="w-[160px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All rows</SelectItem>
                <SelectItem value="failed">Failed only</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[60px]">#</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Date</TableHead>
                  <TableHead>In</TableHead>
                  <TableHead>Out</TableHead>
                  <TableHead>Result</TableHead>
                  <TableHead>Error</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loadingRows ? (
                  <TableRow>
                    <TableCell colSpan={7} className="py-8 text-center">
                      <Loader2 className="mx-auto h-5 w-5 animate-spin text-muted-foreground" />
                    </TableCell>
                  </TableRow>
                ) : importRows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        title={rowFilter === 'failed' ? 'No failed rows' : 'No rows'}
                        description={
                          rowFilter === 'failed'
                            ? 'Every row in this batch was accepted.'
                            : 'This batch was staged without any rows.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  importRows.map((row) => (
                    <TableRow key={row.id}>
                      <TableCell>{row.rowNumber}</TableCell>
                      <TableCell>{row.employeeName || '—'}</TableCell>
                      <TableCell>{formatDate(row.attendanceDate)}</TableCell>
                      <TableCell>{formatTime(row.checkInTime)}</TableCell>
                      <TableCell>{formatTime(row.checkOutTime)}</TableCell>
                      <TableCell>
                        {row.isSuccess ? (
                          <Badge variant="outline">OK</Badge>
                        ) : (
                          <Badge variant="destructive">Failed</Badge>
                        )}
                      </TableCell>
                      <TableCell className="max-w-[320px] text-sm text-muted-foreground">
                        {row.errorMessage || '—'}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={processing}
        onOpenChange={setProcessing}
        title="Process this batch?"
        description={`Applies ${imp.totalRows} staged rows to attendance. Rows that fail validation are reported but the rest still go through.`}
        confirmText="Process"
        isLoading={busy}
        onConfirm={runProcess}
      />
    </div>
  );
}
