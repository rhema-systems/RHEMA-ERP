'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Play, Pause, CheckCheck, Ban } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
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
import { clientEngagementService } from '@/services/hr/consultant.service';
import { formatDate, formatHours, formatMoney, today } from '@/lib/hr/attendance-format';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

type Action = 'activate' | 'suspend' | 'resume' | 'complete' | 'terminate';

/**
 * One engagement and its lifecycle.
 *
 * Suspending pauses billing without ending the contract (resume puts it back); completing
 * closes it normally with an actual end date; terminating ends it early and wants a reason.
 * They are separate endpoints because they mean different things commercially.
 */
export default function EngagementDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [action, setAction] = useState<Action | null>(null);
  const [busy, setBusy] = useState(false);
  const [actualEndDate, setActualEndDate] = useState(today());
  const [reason, setReason] = useState('');

  const { data: e, isLoading, isError } = useQuery({
    queryKey: ['hr', 'client-engagements', id],
    queryFn: () => clientEngagementService.getById(id),
    enabled: !!id,
  });

  const runAction = async () => {
    if (!action) return false;
    setBusy(true);
    try {
      switch (action) {
        case 'activate':
          await clientEngagementService.activate(id);
          break;
        case 'suspend':
          await clientEngagementService.suspend(id);
          break;
        case 'resume':
          await clientEngagementService.resume(id);
          break;
        case 'complete':
          await clientEngagementService.complete(id, actualEndDate);
          break;
        case 'terminate':
          await clientEngagementService.terminate(id, reason.trim() || null);
          break;
      }
      await queryClient.invalidateQueries({ queryKey: ['hr', 'client-engagements'] });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'consultant-clients'] });
      toast({ title: 'Done', description: 'The engagement was updated.' });
      setAction(null);
      setReason('');
      return true;
    } catch (err: any) {
      toast({
        title: 'Error',
        description: err?.message || 'Action failed.',
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

  if (isError || !e) {
    return (
      <div className="p-6">
        <EmptyState title="Engagement not found" description="It may have been removed." />
      </div>
    );
  }

  const isOpen = e.status === 'Active' || e.status === 'Suspended';

  const dialogCopy: Record<Action, { title: string; description: string; confirm: string }> = {
    activate: {
      title: 'Activate this engagement?',
      description: 'The consultant can start logging billable time against it.',
      confirm: 'Activate',
    },
    suspend: {
      title: 'Suspend this engagement?',
      description: 'Pauses billing without ending the contract. It can be resumed later.',
      confirm: 'Suspend',
    },
    resume: {
      title: 'Resume this engagement?',
      description: 'Returns the engagement to active so time can be logged again.',
      confirm: 'Resume',
    },
    complete: {
      title: 'Complete this engagement?',
      description: 'Closes the engagement normally. Timesheets already raised are unaffected.',
      confirm: 'Complete',
    },
    terminate: {
      title: 'Terminate this engagement?',
      description: 'Ends the contract early. This is recorded separately from a normal completion.',
      confirm: 'Terminate',
    },
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${e.engagementCode} · ${e.title}`}
        description={`${e.clientName} · ${e.consultantName}`}
        backHref="/hr/consulting/engagements"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={e.status} />
            {e.status === 'Draft' && (
              <Button onClick={() => setAction('activate')}>
                <Play className="mr-2 h-4 w-4" /> Activate
              </Button>
            )}
            {e.status === 'Active' && (
              <Button variant="outline" onClick={() => setAction('suspend')}>
                <Pause className="mr-2 h-4 w-4" /> Suspend
              </Button>
            )}
            {e.status === 'Suspended' && (
              <Button variant="outline" onClick={() => setAction('resume')}>
                <Play className="mr-2 h-4 w-4" /> Resume
              </Button>
            )}
            {isOpen && (
              <>
                <Button variant="outline" onClick={() => setAction('complete')}>
                  <CheckCheck className="mr-2 h-4 w-4" /> Complete
                </Button>
                <Button variant="destructive" onClick={() => setAction('terminate')}>
                  <Ban className="mr-2 h-4 w-4" /> Terminate
                </Button>
              </>
            )}
          </div>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Contract</CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
          <InfoRow label="Client" value={`${e.clientName} (${e.clientCode})`} />
          <InfoRow label="Consultant" value={`${e.consultantName} (${e.consultantNumber})`} />
          <InfoRow label="Start date" value={formatDate(e.startDate)} />
          <InfoRow label="End date" value={formatDate(e.endDate)} />
          <InfoRow label="Hourly rate" value={formatMoney(e.hourlyRate, e.currency)} />
          <InfoRow label="Billing cycle" value={e.billingCycle} />
          <InfoRow
            label="Max hours / week"
            value={e.maxHoursPerWeek != null ? formatHours(e.maxHoursPerWeek) : '—'}
          />
          <InfoRow
            label="Contract value"
            value={e.contractValue != null ? formatMoney(e.contractValue, e.currency) : '—'}
          />
          <InfoRow label="PO number" value={e.purchaseOrderNumber} />
        </CardContent>
      </Card>

      {(e.description || e.notes) && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Detail</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <div>
              <p className="text-xs text-muted-foreground">Description</p>
              <p className="whitespace-pre-wrap">{e.description || '—'}</p>
            </div>
            <div>
              <p className="text-xs text-muted-foreground">Notes</p>
              <p className="whitespace-pre-wrap">{e.notes || '—'}</p>
            </div>
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Timesheets ({e.timesheetCount})</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {e.timesheets.length === 0 ? (
            <EmptyState
              title="No timesheets yet"
              description="Time logged against this engagement will appear here."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Number</TableHead>
                  <TableHead>Period</TableHead>
                  <TableHead className="text-right">Hours</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {e.timesheets.map((t) => (
                  <TableRow
                    key={t.id}
                    className="cursor-pointer hover:bg-muted/50"
                    onClick={() => router.push(`/hr/consulting/timesheets/${t.id}`)}
                  >
                    <TableCell className="font-medium">{t.timesheetNumber}</TableCell>
                    <TableCell>
                      {formatDate(t.periodStartDate)} – {formatDate(t.periodEndDate)}
                    </TableCell>
                    <TableCell className="text-right">{formatHours(t.totalHours)}</TableCell>
                    <TableCell>
                      <StatusBadge status={t.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={action !== null}
        onOpenChange={(open) => !open && setAction(null)}
        title={action ? dialogCopy[action].title : ''}
        description={action ? dialogCopy[action].description : ''}
        confirmText={action ? dialogCopy[action].confirm : 'Confirm'}
        variant={action === 'terminate' ? 'destructive' : 'default'}
        isLoading={busy}
        onConfirm={runAction}
      >
        {action === 'complete' && (
          <div className="space-y-2">
            <Label htmlFor="actualEndDate">Actual end date</Label>
            <Input
              id="actualEndDate"
              type="date"
              value={actualEndDate}
              onChange={(ev) => setActualEndDate(ev.target.value)}
            />
          </div>
        )}
        {action === 'terminate' && (
          <div className="space-y-2">
            <Label htmlFor="terminateReason">Reason</Label>
            <Textarea
              id="terminateReason"
              rows={3}
              value={reason}
              onChange={(ev) => setReason(ev.target.value)}
              placeholder="Why is the engagement ending early?"
            />
          </div>
        )}
      </ConfirmationDialog>
    </div>
  );
}
