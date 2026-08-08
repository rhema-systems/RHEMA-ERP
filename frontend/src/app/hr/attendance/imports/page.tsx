'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Upload, Loader2 } from 'lucide-react';
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
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { bulkImportService } from '@/services/hr/attendance.service';
import { formatDateTime } from '@/lib/hr/attendance-format';
import type { StaffBulkAttendanceImportSummary } from '@/types/hr/attendance';

/**
 * Bulk attendance import batches.
 *
 * An import is staged first and processed second, so a batch can be inspected — and its bad
 * rows spotted — before anything is written to attendance. This list is the history; the
 * per-batch page shows the rows and their errors.
 */
export default function BulkImportsPage() {
  const router = useRouter();
  const [page] = useState(1);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'bulk-imports', page],
    queryFn: () => bulkImportService.getPaged(page, 50).then((p) => p.items),
  });

  const rows: StaffBulkAttendanceImportSummary[] = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Bulk Attendance Imports"
        description="Batch loads of attendance data, staged for review before they are applied."
        backHref="/hr/attendance"
        actions={
          <Button onClick={() => router.push('/hr/attendance/imports/new')}>
            <Upload className="mr-2 h-4 w-4" /> New import
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {rows.length} {rows.length === 1 ? 'batch' : 'batches'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Reference</TableHead>
                  <TableHead>Source</TableHead>
                  <TableHead>File</TableHead>
                  <TableHead>Imported</TableHead>
                  <TableHead>By</TableHead>
                  <TableHead className="text-right">Rows</TableHead>
                  <TableHead className="text-right">OK</TableHead>
                  <TableHead className="text-right">Failed</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(4)].map((_, i) => (
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
                        icon={Upload}
                        title="No imports yet"
                        description="Stage a batch to load attendance from a spreadsheet or device extract."
                        action={
                          <Button
                            size="sm"
                            onClick={() => router.push('/hr/attendance/imports/new')}
                          >
                            <Upload className="mr-2 h-4 w-4" /> New import
                          </Button>
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((imp) => (
                    <TableRow
                      key={imp.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/hr/attendance/imports/${imp.id}`)}
                    >
                      <TableCell className="font-medium">{imp.importReference}</TableCell>
                      <TableCell>{imp.sourceType}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {imp.sourceFileName || '—'}
                      </TableCell>
                      <TableCell>{formatDateTime(imp.importDate)}</TableCell>
                      <TableCell className="text-muted-foreground">{imp.importedByName}</TableCell>
                      <TableCell className="text-right">{imp.totalRows}</TableCell>
                      <TableCell className="text-right">{imp.successCount}</TableCell>
                      <TableCell className="text-right">
                        {imp.failureCount > 0 ? (
                          <span className="font-medium text-red-600">{imp.failureCount}</span>
                        ) : (
                          imp.failureCount
                        )}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={imp.status} />
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
