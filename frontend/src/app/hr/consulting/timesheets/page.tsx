'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Plus, FileClock, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  useWorkflowEntitySummaries,
  formatPendingApprovers,
} from '@/hooks/useWorkflowEntitySummaries';
import { consultantTimesheetService } from '@/services/hr/consultant.service';
import { formatDate, formatHours } from '@/lib/hr/attendance-format';
import { TIMESHEET_STATUS_OPTIONS } from '@/types/hr/consultant';
import type { TimesheetStatus, ConsultantTimesheetSummary } from '@/types/hr/consultant';

const PENDING_APPROVAL = '__pending_approval__';
const PENDING_CLIENT = '__pending_client__';
const FOR_INVOICING = '__for_invoicing__';
const ALL = '__all__';

/**
 * Consultant timesheets.
 *
 * Two separate gates matter here and the filters name both: internal approval (the workflow)
 * and client confirmation (the emailed token). A timesheet only becomes invoiceable after
 * the client has confirmed it.
 */
export default function ConsultantTimesheetsPage() {
  const router = useRouter();
  const [filter, setFilter] = useState<string>(PENDING_APPROVAL);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ['hr', 'consultant-timesheets', 'list', filter],
    queryFn: () => {
      if (filter === PENDING_APPROVAL) return consultantTimesheetService.getPendingApproval();
      if (filter === PENDING_CLIENT)
        return consultantTimesheetService.getPendingClientConfirmation();
      if (filter === FOR_INVOICING) return consultantTimesheetService.getApprovedForInvoicing();
      if (filter === ALL) return consultantTimesheetService.getPaged(1, 100).then((p) => p.items);
      return consultantTimesheetService.getByStatus(filter as TimesheetStatus);
    },
  });

  const rows: ConsultantTimesheetSummary[] = data ?? [];

  const summaries = useWorkflowEntitySummaries(
    'ConsultantTimesheet',
    rows.map((r) => r.id),
    rows.length > 0,
  );

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Consultant Timesheets"
        description="Billing-period timesheets, their internal approval and client confirmation."
        backHref="/hr/consulting"
        actions={
          <Button onClick={() => router.push('/hr/consulting/timesheets/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Timesheet
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Filter</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="max-w-sm space-y-2">
            <label className="text-sm font-medium">Show</label>
            <Select value={filter} onValueChange={setFilter}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={PENDING_APPROVAL}>Awaiting internal approval</SelectItem>
                <SelectItem value={PENDING_CLIENT}>Awaiting client confirmation</SelectItem>
                <SelectItem value={FOR_INVOICING}>Ready to invoice</SelectItem>
                <SelectItem value={ALL}>All timesheets</SelectItem>
                {TIMESHEET_STATUS_OPTIONS.map((o) => (
                  <SelectItem key={o.value} value={o.value}>
                    {o.label}
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
              {rows.length} {rows.length === 1 ? 'timesheet' : 'timesheets'}
            </CardTitle>
            {isFetching && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
          </div>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Consultant</TableHead>
                  <TableHead>Client</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead className="text-right">Hours</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Awaiting</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={FileClock}
                        title="No timesheets"
                        description="Nothing matches this filter."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((t) => {
                    const summary = summaries.summariesById?.[t.id];
                    return (
                      <TableRow
                        key={t.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => router.push(`/hr/consulting/timesheets/${t.id}`)}
                      >
                        <TableCell className="font-medium">{t.timesheetNumber}</TableCell>
                        <TableCell>{t.consultantName}</TableCell>
                        <TableCell>{t.clientName}</TableCell>
                        <TableCell>
                          {formatDate(t.periodStartDate)} – {formatDate(t.periodEndDate)}
                        </TableCell>
                        <TableCell className="text-right">{formatHours(t.totalHours)}</TableCell>
                        <TableCell>
                          <StatusBadge status={t.status} />
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {(() => {
                            if (!summary?.currentStepName) return '—';
                            const approvers = formatPendingApprovers(summary.pendingApprovers ?? []);
                            return approvers.short
                              ? `${summary.currentStepName} · ${approvers.short}`
                              : summary.currentStepName;
                          })()}
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
