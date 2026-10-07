'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { ScrollText, Loader2, Play } from 'lucide-react';
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
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { attendanceLogService } from '@/services/hr/attendance.service';
import { formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import type { StaffAttendanceLogSummary } from '@/types/hr/attendance';

const UNPROCESSED = '__unprocessed__';
const ALL = '__all__';

/**
 * Raw punches as they arrive from devices and the self-service clock.
 *
 * A punch is only a fact about a moment; it becomes attendance when it is *processed* into a
 * daily record. Unprocessed punches are the interesting ones — they usually mean a device
 * synced late or the employee could not be matched — so that is the default view, and each
 * row can be pushed through individually.
 */
export default function AttendanceLogsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [filter, setFilter] = useState<string>(UNPROCESSED);
  const [processingId, setProcessingId] = useState<string | null>(null);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'attendance-logs', 'list', filter],
    queryFn: () =>
      filter === UNPROCESSED
        ? attendanceLogService.getUnprocessed()
        : attendanceLogService.getPaged(1, 100).then((p) => p.items),
  });

  const rows: StaffAttendanceLogSummary[] = data ?? [];

  const process = async (log: StaffAttendanceLogSummary) => {
    setProcessingId(log.id);
    try {
      await attendanceLogService.process(log.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'attendance-logs'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'daily-attendance'] });
      toast({ title: 'Processed', description: 'The punch was folded into daily attendance.' });
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to process the punch.',
        variant: 'destructive',
      });
    } finally {
      setProcessingId(null);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Punch Logs"
        description="Raw check-in and check-out events from devices and the self-service clock."
        backHref="/hr/attendance"
      />

      <Card>
        <CardHeader>
          <CardTitle>Filter</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-xs space-y-2">
            <label className="text-sm font-medium">Show</label>
            <Select value={filter} onValueChange={setFilter}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={UNPROCESSED}>Unprocessed only</SelectItem>
                <SelectItem value={ALL}>All punches</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {rows.length} {rows.length === 1 ? 'punch' : 'punches'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>When</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Device</TableHead>
                  <TableHead>Processed</TableHead>
                  <TableHead className="w-[120px]" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(6)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6}>
                      <EmptyState
                        icon={ScrollText}
                        title={filter === UNPROCESSED ? 'Nothing unprocessed' : 'No punches'}
                        description={
                          filter === UNPROCESSED
                            ? 'Every punch has been folded into a daily attendance record.'
                            : 'No punches have been recorded yet.'
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((log) => (
                    <TableRow key={log.id}>
                      <TableCell className="font-medium">{log.employeeName}</TableCell>
                      <TableCell>{formatDateTime(log.logDateTime)}</TableCell>
                      <TableCell>{humanizeEnum(log.logType)}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {log.deviceSerialNumber || '—'}
                      </TableCell>
                      <TableCell>
                        {log.isProcessed ? (
                          <Badge variant="outline">Processed</Badge>
                        ) : (
                          <Badge variant="secondary">Pending</Badge>
                        )}
                      </TableCell>
                      <TableCell>
                        {!log.isProcessed && (
                          <Button
                            size="sm"
                            variant="outline"
                            disabled={processingId === log.id}
                            onClick={() => process(log)}
                          >
                            {processingId === log.id ? (
                              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                            ) : (
                              <Play className="mr-2 h-4 w-4" />
                            )}
                            Process
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
