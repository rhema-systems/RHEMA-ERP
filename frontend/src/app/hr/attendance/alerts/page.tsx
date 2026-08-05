'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { BellRing, Loader2, Check } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Checkbox } from '@/components/ui/checkbox';
import { Textarea } from '@/components/ui/textarea';
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { attendanceAlertService } from '@/services/hr/attendance.service';
import { formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { ALERT_SEVERITY_OPTIONS, ALERT_STATUS_OPTIONS } from '@/types/hr/attendance';
import type {
  AttendanceAlertSeverity,
  AttendanceAlertStatus,
  StaffAttendanceAlertSummary,
} from '@/types/hr/attendance';

const UNACKNOWLEDGED = '__unacknowledged__';
const ALL = '__all__';

const SEVERITY_VARIANT = {
  Critical: 'destructive',
  Warning: 'secondary',
  Info: 'outline',
} as const;

/**
 * Attendance alerts raised by the alert rules configured under Administration.
 *
 * Acknowledging is a bulk operation in practice — a rule firing across a department
 * produces a row per employee — so the table supports multi-select against the bulk
 * endpoint rather than making someone clear them one at a time.
 */
export default function AttendanceAlertsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [filter, setFilter] = useState<string>(UNACKNOWLEDGED);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [acknowledging, setAcknowledging] = useState(false);
  const [busy, setBusy] = useState(false);
  const [comments, setComments] = useState('');

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'attendance-alerts', 'list', filter],
    queryFn: () => {
      if (filter === UNACKNOWLEDGED) return attendanceAlertService.getUnacknowledged();
      if (filter === ALL) return attendanceAlertService.getPaged(1, 100).then((p) => p.items);
      if (ALERT_SEVERITY_OPTIONS.some((o) => o.value === filter)) {
        return attendanceAlertService.getBySeverity(filter as AttendanceAlertSeverity);
      }
      // Status filters have no dedicated endpoint, so the paged read is narrowed here.
      return attendanceAlertService
        .getPaged(1, 200)
        .then((p) => p.items.filter((a) => a.status === (filter as AttendanceAlertStatus)));
    },
  });

  const rows: StaffAttendanceAlertSummary[] = data ?? [];
  const selectable = rows.filter((r) => r.status === 'Active');
  const allSelected = selectable.length > 0 && selectable.every((r) => selected.has(r.id));

  const toggleAll = () => {
    setSelected(allSelected ? new Set() : new Set(selectable.map((r) => r.id)));
  };

  const toggleOne = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const acknowledgeSelected = async () => {
    setBusy(true);
    try {
      const count = await attendanceAlertService.bulkAcknowledge(
        Array.from(selected),
        comments.trim() || null,
      );
      await queryClient.invalidateQueries({ queryKey: ['hr', 'attendance-alerts'] });
      toast({
        title: 'Acknowledged',
        description: `${count} ${count === 1 ? 'alert was' : 'alerts were'} acknowledged.`,
      });
      setSelected(new Set());
      setComments('');
      setAcknowledging(false);
      return true;
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to acknowledge the alerts.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Attendance Alerts"
        description="Absence, lateness, missing-punch and overtime alerts raised by the alert rules."
        backHref="/hr/attendance"
        actions={
          selected.size > 0 ? (
            <Button onClick={() => setAcknowledging(true)}>
              <Check className="mr-2 h-4 w-4" /> Acknowledge {selected.size}
            </Button>
          ) : undefined
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Filter</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-xs space-y-2">
            <label className="text-sm font-medium">Show</label>
            <Select
              value={filter}
              onValueChange={(v) => {
                setFilter(v);
                setSelected(new Set());
              }}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={UNACKNOWLEDGED}>Unacknowledged</SelectItem>
                <SelectItem value={ALL}>All alerts</SelectItem>
                {ALERT_SEVERITY_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    Severity: {o.label}
                  </SelectItem>
                ))}
                {ALERT_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    Status: {o.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>
              {rows.length} {rows.length === 1 ? 'alert' : 'alerts'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[40px]">
                    <Checkbox
                      checked={allSelected}
                      onCheckedChange={toggleAll}
                      aria-label="Select all active alerts"
                      disabled={selectable.length === 0}
                    />
                  </TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Rule</TableHead>
                  <TableHead>Trigger</TableHead>
                  <TableHead>Severity</TableHead>
                  <TableHead>Raised</TableHead>
                  <TableHead>Detail</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={BellRing}
                        title="No alerts"
                        description="Nothing matches this filter."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell>
                        <Checkbox
                          checked={selected.has(a.id)}
                          onCheckedChange={() => toggleOne(a.id)}
                          disabled={a.status !== 'Active'}
                          aria-label={`Select alert for ${a.employeeName}`}
                        />
                      </TableCell>
                      <TableCell className="font-medium">{a.employeeName}</TableCell>
                      <TableCell>{a.ruleName}</TableCell>
                      <TableCell>{humanizeEnum(a.triggerType)}</TableCell>
                      <TableCell>
                        <Badge variant={SEVERITY_VARIANT[a.severity]}>{a.severity}</Badge>
                      </TableCell>
                      <TableCell>{formatDateTime(a.triggeredDate)}</TableCell>
                      <TableCell className="max-w-[280px] text-muted-foreground">
                        {a.triggerDescription}
                      </TableCell>
                      <TableCell>
                        <StatusBadge status={a.status} />
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
        open={acknowledging}
        onOpenChange={setAcknowledging}
        title={`Acknowledge ${selected.size} ${selected.size === 1 ? 'alert' : 'alerts'}?`}
        description="Acknowledging records that someone has seen the alert. It does not resolve the underlying attendance issue."
        confirmText="Acknowledge"
        isLoading={busy}
        onConfirm={acknowledgeSelected}
      >
        <div className="space-y-2">
          <Label htmlFor="ackComments">Comments</Label>
          <Textarea
            id="ackComments"
            rows={3}
            value={comments}
            onChange={(e) => setComments(e.target.value)}
            placeholder="Optional"
          />
        </div>
      </ConfirmationDialog>
    </div>
  );
}
