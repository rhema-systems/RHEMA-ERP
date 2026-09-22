'use client';

import { use, useState, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CalendarClock,
  CheckCircle2,
  Loader2,
  Pencil,
  Trash2,
  XCircle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import {
  AttachmentsPanel,
  AttendancePanel,
  ParticipantsPanel,
  TasksPanel,
} from '@/components/hr/company-schedule/EventPanels';
import { companyEventService } from '@/services/hr/company-schedule.service';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
const hhmm = (t?: string | null) => (t ? t.slice(0, 5) : null);

function Detail({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="space-y-1">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
      <div className="text-sm">{children ?? '—'}</div>
    </div>
  );
}

/**
 * One company event: its detail, the four collections that hang off it, and the lifecycle
 * actions.
 *
 * ⚠ **Approve takes no approver.** The API reads it from the token, so this page sends an empty
 * POST — passing an id would be act-as-anyone, which is exactly what the endpoint used to allow.
 *
 * Cancel, reschedule and complete each carry a body, so they open a dialog rather than firing on
 * click; a cancellation with no reason is refused by the API and would only produce a 400.
 */
export default function CompanyEventDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  // ⚠ Round 4, D7 (company-schedule defect C-1). Delete is gated on HR.Company.Admin server-side,
  // and the HR role holds Read, Write and Approve but NOT Admin — so this button was rendered, in
  // destructive red, for the very people it refuses. A screen that offers what it cannot do is
  // worse than one that offers less.
  //
  // ⚠ The button is hidden; the endpoint is NOT weakened. Whether HR may delete a company event is a
  // permission decision for TDC to make in role setup, not one to make by loosening a policy. The
  // two only have to agree about what is on offer.
  const { hasPermission } = useAuth();
  const canDelete = hasPermission('HR.Company.Admin');

  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const [reschedule, setReschedule] = useState({
    newStartDate: '',
    newStartTime: '',
    newEndDate: '',
    newEndTime: '',
    rescheduleReason: '',
  });
  const [completeOpen, setCompleteOpen] = useState(false);
  const [complete, setComplete] = useState({ actualAttendance: '', outcomeSummary: '' });
  const [deleteOpen, setDeleteOpen] = useState(false);

  const detailKey = ['hr', 'company-schedule', 'events', id, 'detail'];
  const { data: event, isLoading } = useQuery({
    queryKey: detailKey,
    queryFn: () => companyEventService.getDetail(id),
  });

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: detailKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events'] }),
    ]);

  const fail = (title: string) => (error: any) =>
    toast({
      title,
      description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
      variant: 'destructive',
    });

  const approve = useMutation({
    mutationFn: () => companyEventService.approve(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Event approved' });
    },
    onError: fail('Could not approve the event'),
  });

  const cancel = useMutation({
    mutationFn: () => companyEventService.cancel(id, cancelReason.trim()),
    onSuccess: async () => {
      await refresh();
      setCancelOpen(false);
      setCancelReason('');
      toast({ title: 'Event cancelled' });
    },
    onError: fail('Could not cancel the event'),
  });

  const doReschedule = useMutation({
    mutationFn: () =>
      companyEventService.reschedule(id, {
        newStartDate: reschedule.newStartDate,
        newStartTime: reschedule.newStartTime ? `${reschedule.newStartTime}:00` : null,
        newEndDate: reschedule.newEndDate,
        newEndTime: reschedule.newEndTime ? `${reschedule.newEndTime}:00` : null,
        rescheduleReason: reschedule.rescheduleReason.trim(),
      }),
    onSuccess: async () => {
      await refresh();
      setRescheduleOpen(false);
      toast({ title: 'Event rescheduled' });
    },
    onError: fail('Could not reschedule the event'),
  });

  const doComplete = useMutation({
    mutationFn: () =>
      companyEventService.complete(id, {
        actualStartTime: null,
        actualEndTime: null,
        actualAttendance: complete.actualAttendance ? Number(complete.actualAttendance) : null,
        outcomeSummary: complete.outcomeSummary.trim() || null,
      }),
    onSuccess: async () => {
      await refresh();
      setCompleteOpen(false);
      toast({ title: 'Event closed off' });
    },
    onError: fail('Could not complete the event'),
  });

  const remove = useMutation({
    mutationFn: () => companyEventService.remove(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events'] });
      toast({ title: 'Event deleted' });
      router.push('/hr/company-schedule/events');
    },
    onError: fail('Could not delete the event'),
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!event) {
    return <div className="p-6 text-muted-foreground">That event could not be found.</div>;
  }

  const open = !event.isCancelled && event.status !== 'Completed';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={event.eventName}
        description={`${event.eventNumber} · organised by ${event.organizerName}`}
        backHref="/hr/company-schedule/events"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            {event.requiresApproval && !event.approvedById && open && (
              <Button variant="outline" onClick={() => approve.mutate()} disabled={approve.isPending}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
              </Button>
            )}
            {open && (
              <>
                <Button variant="outline" onClick={() => setRescheduleOpen(true)}>
                  <CalendarClock className="mr-2 h-4 w-4" /> Reschedule
                </Button>
                <Button variant="outline" onClick={() => setCompleteOpen(true)}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Complete
                </Button>
                <Button variant="outline" onClick={() => setCancelOpen(true)}>
                  <XCircle className="mr-2 h-4 w-4" /> Cancel
                </Button>
              </>
            )}
            <Button variant="outline" onClick={() => router.push(`/hr/company-schedule/events/${id}/edit`)}>
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </Button>
            {canDelete && (
              <Button variant="destructive" onClick={() => setDeleteOpen(true)}>
                <Trash2 className="mr-2 h-4 w-4" /> Delete
              </Button>
            )}
          </div>
        }
      />

      <Card>
        <CardHeader><CardTitle>Overview</CardTitle></CardHeader>
        <CardContent className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <Detail label="Status"><StatusBadge status={spaced(event.status)} /></Detail>
          <Detail label="Category">{spaced(event.category)}</Detail>
          <Detail label="Type">{spaced(event.type)}</Detail>
          <Detail label="Priority">{spaced(event.priority)}</Detail>

          <Detail label="Starts">
            {event.startDate.slice(0, 10)}
            {event.isAllDayEvent ? ' · all day' : hhmm(event.startTime) ? ` · ${hhmm(event.startTime)}` : ''}
          </Detail>
          <Detail label="Ends">
            {event.endDate.slice(0, 10)}
            {!event.isAllDayEvent && hhmm(event.endTime) ? ` · ${hhmm(event.endTime)}` : ''}
          </Detail>
          <Detail label="Repeats">
            {event.isRecurring ? spaced(event.recurrencePattern) : 'One-off'}
          </Detail>
          <Detail label="Audience">{spaced(event.scope)}</Detail>

          <Detail label="Location type">{spaced(event.locationType)}</Detail>
          <Detail label="Site">{event.locationName || '—'}</Detail>
          <Detail label="Venue">{event.venueName || '—'}</Detail>
          <Detail label="Department">{event.departmentName || 'Company-wide'}</Detail>

          {event.locationType !== 'OnSite' && (
            <Detail label="Meeting link">
              {event.onlineMeetingLink ? (
                <a
                  className="text-primary underline underline-offset-2"
                  href={event.onlineMeetingLink}
                  target="_blank"
                  rel="noreferrer noopener"
                >
                  Join
                </a>
              ) : (
                '—'
              )}
            </Detail>
          )}
          <Detail label="Expected">{event.estimatedAttendees ?? '—'}</Detail>
          <Detail label="Attended">{event.actualAttendance ?? '—'}</Detail>
          <Detail label="Visibility">{spaced(event.visibility)}</Detail>

          {event.hasBudget && (
            <>
              <Detail label="Budget">{event.budgetAmount?.toLocaleString() ?? '—'}</Detail>
              <Detail label="Actual cost">{event.actualCost?.toLocaleString() ?? '—'}</Detail>
              <Detail label="Budget code">{event.budgetCode || '—'}</Detail>
            </>
          )}
          {event.approvedByName && (
            <Detail label="Approved by">
              {event.approvedByName}
              {event.approvalDate ? ` · ${event.approvalDate.slice(0, 10)}` : ''}
            </Detail>
          )}
          {event.isCancelled && (
            <Detail label="Cancelled">
              {event.cancellationDate?.slice(0, 10)} — {event.cancellationReason}
            </Detail>
          )}
          {event.isRescheduled && (
            <Detail label="Rescheduled">
              {event.rescheduledDate?.slice(0, 10)} — {event.rescheduleReason}
            </Detail>
          )}
        </CardContent>
      </Card>

      {(event.description || event.outcomeSummary || event.additionalNotes) && (
        <Card>
          <CardHeader><CardTitle>Notes</CardTitle></CardHeader>
          <CardContent className="space-y-4 text-sm">
            {event.description && <p>{event.description}</p>}
            {event.outcomeSummary && (
              <div>
                <p className="text-xs uppercase tracking-wide text-muted-foreground">Outcome</p>
                <p>{event.outcomeSummary}</p>
              </div>
            )}
            {event.additionalNotes && <p className="text-muted-foreground">{event.additionalNotes}</p>}
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="participants">
        <TabsList>
          <TabsTrigger value="participants">Participants ({event.participants?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="attendance">Attendance ({event.attendanceRecords?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="tasks">Tasks ({event.tasks?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="attachments">Attachments ({event.attachments?.length ?? 0})</TabsTrigger>
        </TabsList>
        <TabsContent value="participants" className="pt-4"><ParticipantsPanel eventId={id} /></TabsContent>
        <TabsContent value="attendance" className="pt-4"><AttendancePanel eventId={id} /></TabsContent>
        <TabsContent value="tasks" className="pt-4"><TasksPanel eventId={id} /></TabsContent>
        <TabsContent value="attachments" className="pt-4"><AttachmentsPanel eventId={id} /></TabsContent>
      </Tabs>

      {/* Cancel */}
      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel this event</DialogTitle>
            <DialogDescription>Everyone invited keeps their record; the event is marked cancelled.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="cancelReason">Reason</Label>
            <Textarea
              id="cancelReason"
              rows={3}
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder="Why is it being cancelled?"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>Keep it</Button>
            <Button
              variant="destructive"
              disabled={!cancelReason.trim() || cancel.isPending}
              onClick={() => cancel.mutate()}
            >
              {cancel.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Cancel event
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reschedule */}
      <Dialog open={rescheduleOpen} onOpenChange={setRescheduleOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reschedule</DialogTitle>
            <DialogDescription>The original dates are kept on the record as history.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="newStartDate">New start date</Label>
              <Input
                id="newStartDate"
                type="date"
                value={reschedule.newStartDate}
                onChange={(e) => setReschedule({ ...reschedule, newStartDate: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="newStartTime">New start time</Label>
              <Input
                id="newStartTime"
                type="time"
                value={reschedule.newStartTime}
                onChange={(e) => setReschedule({ ...reschedule, newStartTime: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="newEndDate">New end date</Label>
              <Input
                id="newEndDate"
                type="date"
                value={reschedule.newEndDate}
                onChange={(e) => setReschedule({ ...reschedule, newEndDate: e.target.value })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="newEndTime">New end time</Label>
              <Input
                id="newEndTime"
                type="time"
                value={reschedule.newEndTime}
                onChange={(e) => setReschedule({ ...reschedule, newEndTime: e.target.value })}
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="rescheduleReason">Reason</Label>
            <Textarea
              id="rescheduleReason"
              rows={3}
              value={reschedule.rescheduleReason}
              onChange={(e) => setReschedule({ ...reschedule, rescheduleReason: e.target.value })}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRescheduleOpen(false)}>Cancel</Button>
            <Button
              disabled={
                !reschedule.newStartDate ||
                !reschedule.newEndDate ||
                !reschedule.rescheduleReason.trim() ||
                doReschedule.isPending
              }
              onClick={() => doReschedule.mutate()}
            >
              {doReschedule.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Reschedule
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Complete */}
      <Dialog open={completeOpen} onOpenChange={setCompleteOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close off this event</DialogTitle>
            <DialogDescription>Record what actually happened.</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label htmlFor="actualAttendance">Actual attendance</Label>
            <Input
              id="actualAttendance"
              type="number"
              min={0}
              value={complete.actualAttendance}
              onChange={(e) => setComplete({ ...complete, actualAttendance: e.target.value })}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="outcomeSummary">Outcome</Label>
            <Textarea
              id="outcomeSummary"
              rows={4}
              value={complete.outcomeSummary}
              onChange={(e) => setComplete({ ...complete, outcomeSummary: e.target.value })}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCompleteOpen(false)}>Not yet</Button>
            <Button disabled={doComplete.isPending} onClick={() => doComplete.mutate()}>
              {doComplete.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Complete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete this event?"
        description="The event and everything recorded against it are removed. Cancelling instead keeps the history."
        confirmText="Delete"
        variant="destructive"
        onConfirm={async () => {
          await remove.mutateAsync();
          return true;
        }}
      />
    </div>
  );
}
