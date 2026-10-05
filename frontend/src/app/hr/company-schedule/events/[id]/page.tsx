'use client';

import { use, useState, type ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BellRing,
  CalendarClock,
  CheckCircle2,
  Loader2,
  MailQuestion,
  Megaphone,
  Pencil,
  Send,
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
import { toIsoInstant } from '@/components/hr/employee/tabs/fields';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import { EventAnnounceDialog } from '@/components/hr/company-schedule/EventAnnounceDialog';
import { EventSeriesCard } from '@/components/hr/company-schedule/EventSeriesCard';
import { RECURRENCE_PATTERN_LABELS } from '@/types/hr/company-schedule';
import { describeReach, describeSeriesChange } from '@/components/hr/company-schedule/noticeReach';
import { SeriesScopeField } from '@/components/hr/company-schedule/SeriesScopeField';
import type { CompanyEventChange, CompanyEventNoticeResult, SeriesScope } from '@/types/hr/company-schedule';

/**
 * What a cancel, move or delete did beyond the event, as one sentence for the toast (lane 2a) — and who was
 * told, counted from the email result and the in-app notice (lane 2e-2).
 */
function describeChange(change?: CompanyEventChange | null): string | undefined {
  if (!change) return undefined;
  const parts: string[] = [];
  const list = (n: string[]) => `${n.length} room booking${n.length === 1 ? '' : 's'} (${n.join(', ')})`;
  if (change.bookingsMoved?.length) parts.push(`${list(change.bookingsMoved)} moved with it`);
  if (change.bookingsCancelled?.length) parts.push(`${list(change.bookingsCancelled)} cancelled with it`);
  if (change.answersReset) parts.push(`${change.answersReset} accepted or tentative repl${change.answersReset === 1 ? 'y' : 'ies'} asked again`);
  if (change.approvalCleared) parts.push('it waits for approval again');
  const told = change.told?.issued ? describeReach(change.told, 'Guests told') : undefined;
  return [parts.length ? `${parts.join('; ')}.` : undefined, told].filter(Boolean).join(' ') || undefined;
}

/** Lane 2f-2b: the dates a series move or cancellation reached — who was told is in describeChange. */
function seriesLine(change?: CompanyEventChange | null): string | undefined {
  return change?.series ? describeSeriesChange({ ...change.series, told: null }) : undefined;
}

/** A day as the card reads it: "Tue 14 Oct 2026". */
const day = (s?: string | null) =>
  s ? new Date(`${s.slice(0, 10)}T00:00:00Z`).toLocaleDateString(undefined, { timeZone: 'UTC', weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' }) : '';

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
  // Lane 2f-2b: on a recurring event, which dates a cancellation or a move reaches.
  const [cancelScope, setCancelScope] = useState<SeriesScope>('ThisOccurrence');
  const [moveScope, setMoveScope] = useState<SeriesScope>('ThisOccurrence');
  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const [reschedule, setReschedule] = useState({
    newStartDate: '',
    newStartTime: '',
    newEndDate: '',
    newEndTime: '',
    rescheduleReason: '',
    newRsvpDeadline: '',
  });
  const [completeOpen, setCompleteOpen] = useState(false);
  const [complete, setComplete] = useState({ actualAttendance: '', outcomeSummary: '' });
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [announceOpen, setAnnounceOpen] = useState(false);

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

  // Round 4, lane N-b2: the reminder and the RSVP chase, sent now. The hourly sweep sends each once
  // when it falls due; sending it here counts as that send, so nobody is told twice. Lane 2e-2 (R4-6.3):
  // only once it reached somebody — by an email the mail server took, or in the app. One that reached
  // nobody stays due, and says so rather than "sent".
  const sentOrDue = (what: string, sent: string) => async (r: CompanyEventNoticeResult) => {
    await refresh();
    toast(
      r.stamped
        ? { title: sent, description: describeReach(r) }
        : {
            title: `${what} not delivered`,
            description: `${describeReach(r) ?? ''} It stays due: the hourly sweep tries again, or send it here once that is fixed.`,
            variant: 'destructive',
          },
    );
  };
  const remindNow = useMutation({
    mutationFn: () => companyEventService.sendEventReminders(id),
    onSuccess: sentOrDue('Reminder', 'Reminder sent'),
    onError: fail('Could not send the reminder'),
  });
  const chaseNow = useMutation({
    mutationFn: () => companyEventService.sendRsvpReminders(id),
    onSuccess: sentOrDue('Chase', 'Invitations chased'),
    onError: fail('Could not chase the invitations'),
  });
  // Lane 2e-2: an invitation that reached nobody is left "Not delivered"; this sends those again.
  const resendInvitations = useMutation({
    mutationFn: () => companyEventService.sendUndeliveredInvitations(id),
    onSuccess: async (r) => {
      await Promise.all([
        refresh(),
        queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events', id, 'participants'] }),
      ]);
      toast({
        title: r.reached ? 'Invitations sent' : 'Invitations still not delivered',
        description: describeReach(r),
        variant: r.reached ? undefined : 'destructive',
      });
    },
    onError: fail('Could not send the invitations'),
  });

  // Lane 2b (D-10): approval runs on the workflow engine. The shared actions show who it waits for and
  // offer Approve and Reject to whoever the engine names; the decision goes through this module's own
  // endpoints, which apply it to the event (the generic inbox path does not — cross-module #15).
  const awaitingApproval =
    !!event &&
    event.requiresApproval &&
    !event.approvalDate &&
    !event.isCancelled &&
    ['Scheduled', 'Rescheduled', 'Postponed'].includes(event.status);
  const workflow = useWorkflowRecord({
    entityType: 'CompanyEvent',
    entityId: id,
    entityLabel: 'Company event',
    entityNumber: event?.eventNumber,
    status: awaitingApproval ? 'PendingApproval' : (event?.status ?? 'Scheduled'),
    // Events have no draft: the approval starts when the event is created.
    canSubmit: false,
    canApproveReject: awaitingApproval,
    // No recall: the generic button would call the engine directly and leave the event waiting with
    // nothing under way.
    canRecall: false,
    enabled: !!event?.requiresApproval,
    commands: {
      approve: (ctx) => companyEventService.approve(id, ctx.comments || null),
      reject: (ctx) => companyEventService.reject(id, ctx.comments || ''),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const cancel = useMutation({
    mutationFn: () =>
      companyEventService.cancel(id, cancelReason.trim(), event?.recurrenceSeriesId ? cancelScope : undefined),
    onSuccess: async (change) => {
      await refresh();
      setCancelOpen(false);
      setCancelReason('');
      toast({
        title: change.series ? 'Dates cancelled' : 'Event cancelled',
        description: [seriesLine(change), describeChange(change)].filter(Boolean).join(' ') || undefined,
      });
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
        newRsvpDeadline: toIsoInstant(reschedule.newRsvpDeadline),
        ...(event?.recurrenceSeriesId ? { seriesScope: moveScope } : {}),
      }),
    onSuccess: async (change) => {
      await refresh();
      setRescheduleOpen(false);
      toast({
        title: change.series ? 'Dates moved' : 'Event rescheduled',
        description:
          [seriesLine(change), describeChange(change), ...(change.warnings ?? []).map((w) => `⚠ ${w}`)]
            .filter(Boolean).join(' ') || undefined,
      });
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
    onSuccess: async (change) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events'] });
      toast({ title: 'Event deleted', description: describeChange(change) });
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
  // Complete only once it has started (F-40); event times are GMT, as the server compares them.
  const startsAt = new Date(
    `${event.startDate.slice(0, 10)}T${event.isAllDayEvent || !event.startTime ? '00:00:00' : event.startTime}Z`,
  );
  const started = startsAt.getTime() <= Date.now();
  // Lane 2e-1 (F-33): the buttons follow the sweep's rule, which the server applies to them too — live,
  // approved where approval is needed, not postponed, and not yet begun; a chase also needs a reply-by
  // date still ahead.
  const today = new Date().toISOString().slice(0, 10);
  const canRemind =
    open && !awaitingApproval && event.status !== 'Postponed' && event.status !== 'InProgress'
    && event.startDate.slice(0, 10) >= today;
  const canChase =
    canRemind && event.requiresRsvp && !!event.rsvpDeadline && new Date(event.rsvpDeadline).getTime() > Date.now();

  // Lane 2e-2 (R4-6.3): issued vs delivered. A reminder or chase is stamped only once it reached somebody, so one
  // past its day with no stamp reached nobody; an invitation is Sent only once it reached its guest.
  const reminderDue = canRemind && !event.reminderSentDate && !!event.reminderDueOn && event.reminderDueOn.slice(0, 10) <= today;
  const chaseDue = canChase && !event.rsvpReminderSentDate && !!event.rsvpChaseDueOn && event.rsvpChaseDueOn.slice(0, 10) <= today;
  const guests = event.participants ?? [];
  const notDelivered = guests.filter((p) => p.invitationStatus === 'NotSent').length;
  // The server's rule (RefuseInviting): open, not awaiting approval, not yet begun.
  const canResend =
    open && !awaitingApproval && event.status !== 'InProgress' && event.startDate.slice(0, 10) >= today && notDelivered > 0;
  const stillDue = 'it has reached nobody yet. The hourly sweep tries again.';

  const reminderText = !event.sendReminders
    ? 'Off — turn on Send reminders in Edit'
    : event.reminderSentDate
      ? `Sent ${new Date(event.reminderSentDate).toLocaleString()}`
      : reminderDue
        ? `Due since ${day(event.reminderDueOn)} — ${stillDue}`
        : canRemind
          ? `Goes on ${day(event.reminderDueOn)}, ${event.reminderDaysBefore ?? 0} day${event.reminderDaysBefore === 1 ? '' : 's'} before the event, automatically`
          : 'Not sent';
  const chaseText = !event.requiresRsvp || !event.rsvpDeadline
    ? 'No RSVP deadline'
    : event.rsvpReminderSentDate
      ? `Chased ${new Date(event.rsvpReminderSentDate).toLocaleString()}`
      : chaseDue
        ? `Due since ${day(event.rsvpChaseDueOn)} — ${stillDue}`
        : canChase
          ? `Goes on ${day(event.rsvpChaseDueOn)}, automatically, to everybody who has not answered`
          : 'Not sent';
  const invitationsText = !guests.length
    ? 'Nobody invited yet'
    : awaitingApproval && notDelivered
      ? `${notDelivered} wait${notDelivered === 1 ? 's' : ''} for the approval`
      : `${guests.length - notDelivered} of ${guests.length} delivered${notDelivered ? ` — ${notDelivered} not delivered` : ''}`;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={event.eventName}
        description={`${event.eventNumber}${
          event.occurrenceNumber && event.occurrenceCount ? ` · occurrence ${event.occurrenceNumber} of ${event.occurrenceCount}` : ''
        } · organised by ${event.organizerName}`}
        backHref="/hr/company-schedule/events"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            {event.requiresApproval && <WorkflowApprovalActions {...workflow.actionProps} />}
            {open && (
              <>
                <Button variant="outline" onClick={() => setRescheduleOpen(true)}>
                  <CalendarClock className="mr-2 h-4 w-4" /> Reschedule
                </Button>
                {started && (
                  <Button variant="outline" onClick={() => setCompleteOpen(true)}>
                    <CheckCircle2 className="mr-2 h-4 w-4" /> Complete
                  </Button>
                )}
                <Button variant="outline" onClick={() => setCancelOpen(true)}>
                  <XCircle className="mr-2 h-4 w-4" /> Cancel
                </Button>
              </>
            )}
            {/* Lane 2c: "Show on intranet" marks it to announce; HR sends it here, once it is approved. */}
            {open && event.showOnIntranet && (
              <Button variant="outline" onClick={() => setAnnounceOpen(true)}>
                <Megaphone className="mr-2 h-4 w-4" /> Announce on the intranet
              </Button>
            )}
            {/* A cancelled or completed event can no longer be edited (lane 2a). */}
            {open && (
              <Button variant="outline" onClick={() => router.push(`/hr/company-schedule/events/${id}/edit`)}>
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </Button>
            )}
            {canDelete && (
              <Button variant="destructive" onClick={() => setDeleteOpen(true)}>
                <Trash2 className="mr-2 h-4 w-4" /> Delete
              </Button>
            )}
          </div>
        }
      />

      {/* Lane 2f-1 (D-12): made and flagged, not skipped — moving it is HR's call. */}
      {event.dayOffNote && open && (
        <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
          {event.dayOffNote} It is kept as scheduled; reschedule it if it should not go ahead that day.
        </p>
      )}

      {/* Lane 2h (C-51): made by Safety from a drill's next date — and kept in step with it. */}
      {event.source && (
        <p className="rounded-md border bg-muted/40 p-3 text-sm">
          From{' '}
          {event.source.link ? (
            <Link className="font-medium text-primary underline underline-offset-2" href={event.source.link}>
              {event.source.label}
            </Link>
          ) : (
            <span className="font-medium">{event.source.label}</span>
          )}
          . Its date follows the drill&apos;s next date in Safety: a change there moves or cancels it here.
        </p>
      )}

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
            {/* Lane 2f-1: a series' occurrence says which; a row saved as repeating before series existed says so. */}
            {event.recurrenceSeriesId && event.recurrencePattern
              ? `${RECURRENCE_PATTERN_LABELS[event.recurrencePattern]} — ${event.occurrenceNumber} of ${event.occurrenceCount ?? '?'}`
              : event.isRecurring
                ? `${spaced(event.recurrencePattern)} (no occurrences made)`
                : 'One-off'}
          </Detail>
          <Detail label="Audience">{spaced(event.scope)}</Detail>

          <Detail label="Location type">{spaced(event.locationType)}</Detail>
          <Detail label="Site">{event.locationName || '—'}</Detail>
          <Detail label="Venue">{event.venueName || '—'}</Detail>
          <Detail label="Organisation unit">{event.organizationUnitName || event.departmentName || '—'}</Detail>
          <Detail label="For">{event.audienceDescription || '—'}</Detail>

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

      <EventSeriesCard event={event} open={open} />

      <Card>
        <CardHeader><CardTitle>Invitations and reminders</CardTitle></CardHeader>
        <CardContent className="space-y-4 text-sm">
          <div className="grid gap-6 sm:grid-cols-3">
            <Detail label="Invitations">{invitationsText}</Detail>
            <Detail label="Event reminder">{reminderText}</Detail>
            <Detail label="RSVP chase">{chaseText}</Detail>
          </div>
          {/* Lane 2e-2: why nothing reaches the outside guests on a database with no mail server (Rule 7). */}
          {open && !event.mailServerSetUp && (
            <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
              No mail server is set up, so no email goes. Employees with a login are told in the app; guests from
              outside, and staff without a login, are not reached.
            </p>
          )}
          {awaitingApproval && open && (
            <p className="text-muted-foreground">
              Nobody is invited while the event awaits approval: its invitations go out when it is approved.
            </p>
          )}
          {(canRemind || canResend) && (
            <div className="flex flex-wrap gap-2">
              {canResend && (
                <Button variant="outline" onClick={() => resendInvitations.mutate()} disabled={resendInvitations.isPending}>
                  <Send className="mr-2 h-4 w-4" /> Send the undelivered invitation{notDelivered === 1 ? '' : 's'} ({notDelivered})
                </Button>
              )}
              {canRemind && (
                <Button variant="outline" onClick={() => remindNow.mutate()} disabled={remindNow.isPending}>
                  <BellRing className="mr-2 h-4 w-4" /> Send reminder now
                </Button>
              )}
              {canChase && (
                <Button variant="outline" onClick={() => chaseNow.mutate()} disabled={chaseNow.isPending}>
                  <MailQuestion className="mr-2 h-4 w-4" /> Chase unanswered now
                </Button>
              )}
            </div>
          )}
          <p className="text-muted-foreground">
            An invitation, a reminder or a chase counts as sent once it reaches somebody — by an email the mail
            server took, or in the app. The reminder and the chase are sent once; sending one here counts as that
            send, and one that reaches nobody stays due. Moving the event&apos;s date, or its RSVP deadline, lets them
            go again for the new date. Each invitation email carries a calendar entry; a move, a new venue or link,
            a postponement, a cancellation or being taken off the list sends guests the update.
          </p>
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
          {event.requiresApproval && <WorkflowTabTrigger value="workflow" {...workflow.tabProps} />}
        </TabsList>
        <TabsContent value="participants" className="pt-4">
          <ParticipantsPanel
            eventId={id}
            open={open}
            awaitingApproval={awaitingApproval}
            mailServerSetUp={event.mailServerSetUp}
            inSeries={!!event.recurrenceSeriesId}
          />
        </TabsContent>
        <TabsContent value="attendance" className="pt-4">
          {/* Lane 2d: a register once the event has started, never for a cancelled one — as the server rules. */}
          <AttendancePanel
            eventId={id}
            markable={started && !event.isCancelled}
            notMarkable={
              event.isCancelled
                ? 'This event was cancelled, so there is no attendance to mark.'
                : 'Attendance is marked once the event has started.'
            }
          />
        </TabsContent>
        <TabsContent value="tasks" className="pt-4"><TasksPanel eventId={id} /></TabsContent>
        <TabsContent value="attachments" className="pt-4">
          <AttachmentsPanel eventId={id} cancelled={event.isCancelled || event.status === 'Cancelled'} />
        </TabsContent>
        {event.requiresApproval && (
          <WorkflowTabContent
            {...workflow.tabProps}
            value="workflow"
            entityType="CompanyEvent"
            entityId={id}
            entityLabel="Company event"
            entityNumber={event.eventNumber}
            status={awaitingApproval ? 'PendingApproval' : event.status}
            onAfterAction={async () => {
              await refresh();
              await workflow.refresh();
            }}
          />
        )}
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
          {event.recurrenceSeriesId && (
            <SeriesScopeField
              value={cancelScope}
              onChange={setCancelScope}
              hint="This and following ends the series at this date; their rooms are cancelled with them."
            />
          )}
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

      <EventAnnounceDialog eventId={id} eventName={event.eventName} open={announceOpen} onOpenChange={setAnnounceOpen} />

      {/* Reschedule */}
      <Dialog open={rescheduleOpen} onOpenChange={setRescheduleOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reschedule</DialogTitle>
            <DialogDescription>
              The original dates are kept. Everybody invited is told why; accepted and tentative replies are
              asked again; room bookings for the event move with it. Leave the times empty to keep its hours.
            </DialogDescription>
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
          {event.requiresRsvp && (
            <div className="space-y-2">
              <Label htmlFor="newRsvpDeadline">New RSVP deadline</Label>
              <Input
                id="newRsvpDeadline"
                type="datetime-local"
                value={reschedule.newRsvpDeadline}
                onChange={(e) => setReschedule({ ...reschedule, newRsvpDeadline: e.target.value })}
              />
              <p className="text-xs text-muted-foreground">
                Needed only if the current one would fall after the new start. Empty keeps it.
              </p>
            </div>
          )}
          <div className="space-y-2">
            <Label htmlFor="rescheduleReason">Reason</Label>
            <Textarea
              id="rescheduleReason"
              rows={3}
              value={reschedule.rescheduleReason}
              onChange={(e) => setReschedule({ ...reschedule, rescheduleReason: e.target.value })}
            />
          </div>
          {event.recurrenceSeriesId && (
            <SeriesScopeField
              value={moveScope}
              onChange={setMoveScope}
              hint="The other dates move by the same number of days, to the new times when you give them; a new reply-by date keeps its distance from each date."
            />
          )}
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
