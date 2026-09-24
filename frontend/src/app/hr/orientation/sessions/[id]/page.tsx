'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Users, Trash2, Video, MapPin, Repeat } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  OrientationSessionForm,
  toSessionFormValues,
  toSessionRequest,
  type OrientationSessionFormValues,
} from '@/components/hr/orientation/OrientationSessionForm';
import { OrientationAttendanceRegister } from '@/components/hr/orientation/OrientationAttendanceRegister';
import {
  FacilitatorFields,
  FacilitatorRegisterNote,
  emptyFacilitator,
  facilitatorAsItStands,
  facilitatorName,
  facilitatorRequest,
  facilitatorSchema,
  facilitatorSource,
  facilitatorToForm,
  type FacilitatorForm,
} from '@/components/hr/orientation/FacilitatorFields';
import { RunSessionAgainDialog } from '@/components/hr/orientation/CopyDialogs';
import { orientationSessionService } from '@/services/hr/orientation-session.service';
import { employeeOrientationService } from '@/services/hr/employee-orientation.service';
import {
  ORIENTATION_SESSION_STATUS_OPTIONS,
  ORIENTATION_DELIVERY_MODE_OPTIONS,
  ORIENTATION_FACILITATOR_ROLE_OPTIONS,
  OCCUPYING_ENROLLMENT_STATUSES,
} from '@/types/hr/orientation';
import type {
  OrientationSessionStatus,
  OrientationSessionFacilitator,
} from '@/types/hr/orientation';

const deliveryLabel = (v: string) =>
  ORIENTATION_DELIVERY_MODE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const roleLabel = (v: string) =>
  ORIENTATION_FACILITATOR_ROLE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmtWhen = (iso?: string | null) =>
  iso ? new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—';

/**
 * One scheduled run of a programme: what it is, who is delivering it, who is on it, and the
 * register.
 *
 * Seats remaining are derived from `maxParticipants` rather than the DTO's `availableSeats`, which
 * is 0 for an uncapped session and would read as full — the same correction the sessions list makes.
 */
export default function OrientationSessionDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [savingOverview, setSavingOverview] = useState(false);
  const [pendingStatus, setPendingStatus] = useState<OrientationSessionStatus | null>(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [rerunOpen, setRerunOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'orientation-sessions', id];
  const { data: session, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => orientationSessionService.getById(id),
    enabled: !!id,
  });

  const { data: roster = [], isLoading: loadingRoster } = useQuery({
    queryKey: ['hr', 'orientation-sessions', id, 'enrollments'],
    queryFn: () => employeeOrientationService.getBySession(id),
    enabled: !!id,
  });

  // Round 4, lane R: what closing the session will do to its participants, read when the Completed
  // confirmation opens, so it can say so before it is pressed. On a programme that is only its
  // session, the people the register shows attending complete; on any other it completes nobody.
  const { data: completionPreview, isLoading: loadingPreview } = useQuery({
    queryKey: ['hr', 'orientation-sessions', id, 'completion-preview'],
    queryFn: () => employeeOrientationService.getSessionCompletionPreview(id),
    enabled: !!id && pendingStatus === 'Completed',
    staleTime: 0,
    retry: false,
  });

  const completedDescription = (() => {
    const stamp = 'The actual end time is stamped automatically if it has not been set.';
    if (loadingPreview) return `${stamp} Checking the register…`;
    if (!completionPreview) return stamp;
    const programme = completionPreview.programTitle ?? 'the programme';
    if (!completionPreview.completesByAttendance)
      return `${stamp} It completes nobody: ${programme} is completed by its own content, assessment and declaration.`;
    const n = completionPreview.willComplete;
    const m = completionPreview.notShownAttending;
    const people = (k: number) => `${k} participant${k === 1 ? '' : 's'}`;
    const attended = completionPreview.requiresAcknowledgement
      ? `${people(n)} the register shows attending will have their attendance confirmed; each completes ${programme} once their declaration is signed.`
      : `${people(n)} the register shows attending will complete ${programme}.`;
    const rest =
      m > 0
        ? ` ${people(m)} ${m === 1 ? 'is' : 'are'} not shown attending and stay open: mark the register first, or mark them completed from the enrolments screen.`
        : '';
    return `${stamp} ${attended}${rest}`;
  })();

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-sessions'] }),
    ]);

  const handleOverviewSubmit = async (values: OrientationSessionFormValues) => {
    setSavingOverview(true);
    try {
      await orientationSessionService.update(id, {
        id,
        ...toSessionRequest(values),
        // Actuals are only on the update payload; the shared request builder leaves them out
        // because the create endpoint has no field for them.
        actualStartAt: values.actualStartAt ? new Date(values.actualStartAt).toISOString() : null,
        actualEndAt: values.actualEndAt ? new Date(values.actualEndAt).toISOString() : null,
      } as any);
      await invalidate();
      toast({ title: 'Saved', description: 'Session updated.' });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to update the session.',
        variant: 'destructive',
      });
    } finally {
      setSavingOverview(false);
    }
  };

  const changeStatus = async () => {
    if (!pendingStatus) return false;
    setBusy(true);
    try {
      await orientationSessionService.changeStatus(id, { sessionId: id, newStatus: pendingStatus });
      await invalidate();
      if (pendingStatus === 'Completed')
        await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-enrollments'] });
      toast({ title: 'Status changed', description: `Now ${pendingStatus}.` });
      setPendingStatus(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to change the status.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    setBusy(true);
    try {
      await orientationSessionService.remove(id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'orientation-sessions'] });
      toast({ title: 'Session deleted' });
      router.push('/hr/orientation/sessions');
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete the session.',
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
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !session) {
    return (
      <div className="p-6">
        <EmptyState title="Session not found" description="It may have been removed." />
      </div>
    );
  }

  const occupying = roster.filter((e) =>
    OCCUPYING_ENROLLMENT_STATUSES.includes(e.enrollmentStatus),
  ).length;
  const seatsLeft =
    session.maxParticipants == null ? null : Math.max(0, session.maxParticipants - occupying);
  const isFinished = session.status === 'Completed' || session.status === 'Cancelled';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={session.title}
        description={`${session.sessionCode}${
          session.programTitle ? ` · ${session.programTitle}` : ''
        } · ${deliveryLabel(session.deliveryMode)}`}
        backHref="/hr/orientation/sessions"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={session.status} />
            <Select
              value=""
              onValueChange={(v) => setPendingStatus(v as OrientationSessionStatus)}
            >
              <SelectTrigger className="w-[180px]">
                <SelectValue placeholder="Change status…" />
              </SelectTrigger>
              <SelectContent>
                {ORIENTATION_SESSION_STATUS_OPTIONS.filter((o) => o.value !== session.status).map(
                  (o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ),
                )}
              </SelectContent>
            </Select>
            <Button variant="outline" onClick={() => setRerunOpen(true)}>
              <Repeat className="mr-2 h-4 w-4" />
              Run again
            </Button>
            <Button
              variant="outline"
              size="icon"
              onClick={() => setConfirmDelete(true)}
              aria-label="Delete session"
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Starts', value: fmtWhen(session.scheduledStartAt) },
          { label: 'Ends', value: fmtWhen(session.scheduledEndAt) },
          {
            label: 'Enrolled',
            value: `${occupying}${session.maxParticipants ? ` / ${session.maxParticipants}` : ''}`,
            icon: Users,
            hint:
              seatsLeft === null
                ? 'Uncapped'
                : seatsLeft === 0
                  ? session.allowWaitlist
                    ? 'Full — new enrollments are waitlisted'
                    : 'Full — new enrollments are refused'
                  : `${seatsLeft} seat${seatsLeft === 1 ? '' : 's'} left`,
            tone: seatsLeft === 0 ? 'warning' : 'default',
          },
          {
            label: 'Enrollment closes',
            value: fmtWhen(session.enrollmentDeadlineAt),
          },
        ]}
      />

      {(session.venueDescription || session.virtualMeetingUrl) && (
        <Card>
          <CardContent className="flex flex-wrap items-center gap-x-6 gap-y-2 py-4 text-sm">
            {session.venueDescription && (
              <span className="flex items-center gap-2">
                <MapPin className="text-muted-foreground h-4 w-4" />
                {session.venueDescription}
              </span>
            )}
            {session.virtualMeetingUrl && (
              <a
                href={session.virtualMeetingUrl}
                target="_blank"
                rel="noreferrer"
                className="text-primary flex items-center gap-2 hover:underline"
              >
                <Video className="h-4 w-4" />
                Joining link
              </a>
            )}
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="overview">
        <TabsList className="flex-wrap">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="facilitators">
            Facilitators ({session.facilitators.length})
          </TabsTrigger>
          <TabsTrigger value="roster">Roster ({roster.length})</TabsTrigger>
          <TabsTrigger value="attendance">Attendance</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          <OrientationSessionForm
            defaultValues={toSessionFormValues(session)}
            onSubmit={handleOverviewSubmit}
            submitting={savingOverview}
            submitLabel="Save changes"
            onCancel={() => router.push('/hr/orientation/sessions')}
            showActuals
          />
        </TabsContent>

        <TabsContent value="facilitators" className="pt-4">
          <ResourceCollectionTab<OrientationSessionFacilitator, FacilitatorForm>
            parentId={id}
            title="facilitators"
            singular="facilitator"
            queryKey={['hr', 'orientation-sessions', id, 'facilitators']}
            invalidateKeys={[queryKey]}
            dialogHint="An employee, a vendor or trainer from the training register, or someone else from outside."
            emptyDescription="Nobody is down to deliver this session yet."
            list={() => orientationSessionService.getFacilitators(id)}
            create={(sessionId, values) =>
              orientationSessionService.addFacilitator(sessionId, {
                sessionId,
                ...facilitatorRequest(values),
              })
            }
            update={(_s, facilitatorId, values) =>
              orientationSessionService.updateFacilitator(facilitatorId, {
                id: facilitatorId,
                ...facilitatorRequest(values),
              })
            }
            remove={(_s, facilitatorId) =>
              orientationSessionService.removeFacilitator(facilitatorId)
            }
            getId={(f) => f.id}
            actions={[
              {
                label: (f) => (f.hasConfirmed ? 'Mark unconfirmed' : 'Mark confirmed'),
                // The whole facilitator as it stands, register pick included — the update writes every
                // field, and a pick left out would turn it into a typed facilitator.
                run: (f) =>
                  orientationSessionService.updateFacilitator(f.id, {
                    id: f.id,
                    ...facilitatorAsItStands(f),
                    hasConfirmed: !f.hasConfirmed,
                  }),
              },
            ]}
            columns={[
              {
                header: 'Facilitator',
                cell: (f) => (
                  <div>
                    <span className="font-medium">{facilitatorName(f)}</span>
                    <div className="text-muted-foreground text-xs">{facilitatorSource(f)}</div>
                    <FacilitatorRegisterNote note={f.registerNote} />
                  </div>
                ),
              },
              { header: 'Role', cell: (f) => roleLabel(f.role) },
              {
                header: 'Confirmed',
                cell: (f) =>
                  f.hasConfirmed ? (
                    <Badge variant="default">Confirmed</Badge>
                  ) : (
                    <Badge variant="secondary">Awaiting</Badge>
                  ),
              },
              { header: 'Notes', cell: (f) => f.notes || '—' },
            ]}
            schema={facilitatorSchema as any}
            emptyForm={emptyFacilitator}
            toForm={facilitatorToForm}
            renderFields={(form) => <FacilitatorFields form={form} />}
          />
        </TabsContent>

        <TabsContent value="roster" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {loadingRoster ? (
                <p className="text-muted-foreground p-6 text-sm">Loading roster…</p>
              ) : roster.length === 0 ? (
                <EmptyState
                  icon={Users}
                  title="Nobody enrolled yet"
                  description="Enrol participants onto this session from the Enrollments screen."
                  action={
                    <Button asChild size="sm" variant="outline">
                      <Link href="/hr/orientation/enrollments">Go to enrollments</Link>
                    </Button>
                  }
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Participant</TableHead>
                      <TableHead>Enrollment</TableHead>
                      <TableHead>Completion</TableHead>
                      <TableHead className="text-right">Progress</TableHead>
                      <TableHead>Enrolled</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {roster.map((e) => (
                      <TableRow key={e.id}>
                        <TableCell className="font-medium">
                          {e.employeeName ?? '—'}
                          {e.employeeNumber && (
                            <div className="text-muted-foreground text-xs">{e.employeeNumber}</div>
                          )}
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={e.enrollmentStatus} />
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={e.completionStatus} />
                        </TableCell>
                        <TableCell className="text-right tabular-nums">
                          {e.progressPercentage}%
                        </TableCell>
                        <TableCell className="text-muted-foreground">
                          {new Date(e.enrolledAt).toLocaleDateString()}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="attendance" className="pt-4">
          <OrientationAttendanceRegister
            sessionId={id}
            readOnly={session.status === 'Cancelled'}
          />
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={pendingStatus !== null}
        onOpenChange={(o) => !o && setPendingStatus(null)}
        title={`Change status to ${pendingStatus}?`}
        description={
          pendingStatus === 'Completed'
            ? completedDescription
            : pendingStatus === 'InProgress'
              ? 'The actual start time is stamped automatically if it has not been set.'
              : pendingStatus === 'Cancelled'
                ? 'Participants keep their records, but this run will not go ahead.'
                : 'This changes who can enrol on the session.'
        }
        confirmText="Change status"
        isLoading={busy}
        onConfirm={changeStatus}
      />

      <ConfirmationDialog
        open={confirmDelete}
        onOpenChange={setConfirmDelete}
        title="Delete this session?"
        description={
          isFinished
            ? 'This cannot be undone.'
            : 'A session with anyone holding a seat cannot be deleted — cancel it instead. This cannot be undone.'
        }
        confirmText="Delete"
        variant="destructive"
        isLoading={busy}
        onConfirm={remove}
      />

      <RunSessionAgainDialog
        source={
          rerunOpen
            ? {
                id: session.id,
                title: session.title,
                scheduledStartAt: session.scheduledStartAt,
                scheduledEndAt: session.scheduledEndAt,
              }
            : null
        }
        onClose={() => setRerunOpen(false)}
      />
    </div>
  );
}
