'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, CheckCircle2, XCircle, Flag, Users } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
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
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { TextField, DateField, TimeField, TextareaField, FieldRow } from '@/components/hr/employee/tabs/fields';
import {
  TrainingScheduleForm,
  toScheduleRequest,
  type TrainingScheduleFormValues,
} from '@/components/hr/training/TrainingScheduleForm';
import { NomineesPanel } from '@/components/hr/training/NomineesPanel';
import { BulkCompletionPanel } from '@/components/hr/training/BulkCompletionPanel';
import { WaitlistPanel } from '@/components/hr/training/WaitlistPanel';
import { AttendanceRegister } from '@/components/hr/training/AttendanceRegister';
import { FeedbackPanel } from '@/components/hr/training/FeedbackPanel';
import { FollowUpPanel } from '@/components/hr/training/FollowUpPanel';
import { trainingScheduleService } from '@/services/hr/training-schedule.service';
import { SCHEDULE_STATUS_OPTIONS } from '@/types/hr/training-delivery';
// Priority is shared with slice 1's catalog types, not redefined per slice.
import { TRAINING_PRIORITY_OPTIONS } from '@/types/hr/training';
import type { TrainingSession } from '@/types/hr/training-delivery';

const sessionSchema = z.object({
  topic: z.string().min(1, 'Required').max(200),
  date: z.string().min(1, 'Required'),
  startTime: z.string().optional().or(z.literal('')),
  endTime: z.string().optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
});
type SessionForm = z.infer<typeof sessionSchema>;
const emptySession: SessionForm = { topic: '', date: '', startTime: '', endTime: '', description: '' };

const statusLabel = (v: string) => SCHEDULE_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const priorityLabel = (v: string) => TRAINING_PRIORITY_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function TrainingScheduleDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [savingOverview, setSavingOverview] = useState(false);
  const [confirmAction, setConfirmAction] = useState<'approve' | 'complete' | null>(null);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'training', 'schedules', id];
  const { data: schedule, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => trainingScheduleService.getById(id),
    enabled: !!id,
  });

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'schedules'] }),
    ]);

  const handleOverviewSubmit = async (values: TrainingScheduleFormValues) => {
    setSavingOverview(true);
    try {
      await trainingScheduleService.update(id, toScheduleRequest(values));
      await invalidate();
      toast({ title: 'Saved', description: 'Schedule updated.' });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to update.', variant: 'destructive' });
    } finally {
      setSavingOverview(false);
    }
  };

  const runAction = async () => {
    if (!confirmAction) return false;
    setBusy(true);
    try {
      if (confirmAction === 'approve') {
        await trainingScheduleService.approve(id);
      } else {
        await trainingScheduleService.complete(id, new Date().toISOString());
      }
      await invalidate();
      toast({
        title: confirmAction === 'approve' ? 'Approved' : 'Completed',
        description:
          confirmAction === 'complete'
            ? 'The delivering trainer has been credited with the session and its hours.'
            : 'Registration is now open.',
      });
      setConfirmAction(null);
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Action failed.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const runCancel = async () => {
    if (!cancelReason.trim()) {
      toast({ title: 'A reason is required', variant: 'destructive' });
      return;
    }
    setBusy(true);
    try {
      await trainingScheduleService.cancel(id, cancelReason.trim());
      await invalidate();
      toast({ title: 'Cancelled' });
      setCancelOpen(false);
      setCancelReason('');
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to cancel.', variant: 'destructive' });
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

  if (isError || !schedule) {
    return (
      <div className="p-6">
        <EmptyState title="Schedule not found" description="It may have been removed." />
      </div>
    );
  }

  const isPlanned = schedule.status === 'Planned';
  const isFinished = schedule.status === 'Completed' || schedule.status === 'Cancelled';
  const canComplete = schedule.status === 'InProgress' || schedule.status === 'RegistrationClosed';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${schedule.programName}`}
        description={`${schedule.scheduleNumber} · ${fmtDate(schedule.startDate)} – ${fmtDate(schedule.endDate)}`}
        backHref="/hr/training/schedules"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={statusLabel(schedule.status)} />
            {isPlanned && (
              <Button size="sm" onClick={() => setConfirmAction('approve')}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
              </Button>
            )}
            {canComplete && (
              <Button size="sm" onClick={() => setConfirmAction('complete')}>
                <Flag className="mr-2 h-4 w-4" /> Mark completed
              </Button>
            )}
            {!isFinished && (
              <Button variant="outline" size="sm" onClick={() => setCancelOpen(true)}>
                <XCircle className="mr-2 h-4 w-4" /> Cancel
              </Button>
            )}
          </div>
        }
      />

      <MetricTiles
        tiles={[
          {
            label: 'Seats taken',
            value: `${schedule.confirmedParticipantsCount} / ${schedule.maxParticipants}`,
            hint: schedule.slotsAvailable > 0 ? `${schedule.slotsAvailable} free` : 'Full — use the waitlist',
            icon: Users,
            tone: schedule.slotsAvailable <= 0 ? 'warning' : 'default',
          },
          { label: 'Delivered by', value: schedule.trainerName ?? schedule.vendorName ?? '—' },
          { label: 'Priority', value: priorityLabel(schedule.priority) },
          {
            label: 'Registration closes',
            value: fmtDate(schedule.registrationCloseDate),
          },
        ]}
      />

      {schedule.status === 'Cancelled' && schedule.cancellationReason && (
        <Card>
          <CardContent className="py-4">
            <p className="text-sm font-medium text-destructive">Cancelled {fmtDate(schedule.cancelledDate)}</p>
            <p className="mt-1 text-sm text-muted-foreground">{schedule.cancellationReason}</p>
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="overview">
        <TabsList className="flex-wrap">
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="sessions">Sessions ({schedule.sessions.length})</TabsTrigger>
          <TabsTrigger value="nominees">Nominees</TabsTrigger>
          <TabsTrigger value="waitlist">Waitlist</TabsTrigger>
          <TabsTrigger value="attendance">Attendance</TabsTrigger>
          <TabsTrigger value="completion">Completion</TabsTrigger>
          <TabsTrigger value="feedback">Feedback</TabsTrigger>
          <TabsTrigger value="follow-up">Follow-up</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          {isPlanned ? (
            <TrainingScheduleForm
              editingScheduleId={id}
              defaultValues={{
                programId: schedule.programId,
                startDate: schedule.startDate.slice(0, 10),
                endDate: schedule.endDate.slice(0, 10),
                startTime: schedule.startTime ?? '',
                endTime: schedule.endTime ?? '',
                venue: schedule.venue ?? '',
                venueAddress: schedule.venueAddress ?? '',
                onlineLink: schedule.onlineLink ?? '',
                trainerProfileId: schedule.trainerProfileId ?? '',
                vendorId: schedule.vendorId ?? '',
                maxParticipants: schedule.maxParticipants,
                priority: schedule.priority,
                registrationOpenDate: schedule.registrationOpenDate.slice(0, 10),
                registrationCloseDate: schedule.registrationCloseDate.slice(0, 10),
                actualCost: schedule.actualCost,
                budgetNotes: schedule.budgetNotes ?? '',
                trainingBudgetId: schedule.trainingBudgetId ?? '',
              }}
              onSubmit={handleOverviewSubmit}
              submitting={savingOverview}
              submitLabel="Save changes"
              onCancel={() => router.push('/hr/training/schedules')}
            />
          ) : (
            <Card>
              <CardContent className="grid gap-4 py-6 sm:grid-cols-2">
                <Detail label="Programme" value={`${schedule.programCode} — ${schedule.programName}`} />
                <Detail label="Venue" value={schedule.venue || '—'} />
                <Detail label="Address" value={schedule.venueAddress || '—'} />
                <Detail label="Online link" value={schedule.onlineLink || '—'} />
                <Detail label="Trainer" value={schedule.trainerName ?? '—'} />
                <Detail label="Vendor" value={schedule.vendorName ?? '—'} />
                <Detail label="Cost" value={schedule.actualCost.toLocaleString()} />
                <Detail label="Budget" value={schedule.budgetCode ?? 'Not budgeted'} />
                <Detail label="Approved by" value={schedule.approvedByName ?? '—'} />
                <Detail label="Approved on" value={fmtDate(schedule.approvalDate)} />
                <Detail label="Completed on" value={fmtDate(schedule.completionDate)} />
                <Detail label="Completion notes" value={schedule.completionNotes || '—'} />
                <p className="text-xs text-muted-foreground sm:col-span-2">
                  Only a planned schedule can be edited here.
                </p>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="sessions" className="pt-4">
          <ResourceCollectionTab<TrainingSession, SessionForm>
            parentId={id}
            title="sessions"
            singular="session"
            queryKey={['hr', 'training', 'schedules', id, 'sessions']}
            invalidateKeys={[queryKey]}
            dialogHint="The day-by-day breakdown of a multi-day run — topics and timings."
            list={() => trainingScheduleService.getSessions(id)}
            create={(scheduleId, values) =>
              trainingScheduleService.addSession(scheduleId, {
                ...values,
                date: new Date(values.date).toISOString(),
                startTime: values.startTime || null,
                endTime: values.endTime || null,
                description: values.description || null,
              })
            }
            update={(_p, sessionId, values) =>
              trainingScheduleService.updateSession(sessionId, {
                ...values,
                date: new Date(values.date).toISOString(),
                startTime: values.startTime || null,
                endTime: values.endTime || null,
                description: values.description || null,
              })
            }
            remove={(_p, sessionId) => trainingScheduleService.removeSession(sessionId)}
            getId={(s) => s.id}
            allowCreate={!isFinished}
            columns={[
              { header: 'Topic', cell: (s) => <span className="font-medium">{s.topic}</span> },
              { header: 'Date', cell: (s) => fmtDate(s.date) },
              {
                header: 'Time',
                cell: (s) => (s.startTime && s.endTime ? `${s.startTime} – ${s.endTime}` : '—'),
              },
              { header: 'Description', cell: (s) => s.description || '—' },
            ]}
            schema={sessionSchema as any}
            emptyForm={emptySession}
            toForm={(s) => ({
              topic: s.topic,
              date: s.date.slice(0, 10),
              startTime: s.startTime ?? '',
              endTime: s.endTime ?? '',
              description: s.description ?? '',
            })}
            renderFields={(form) => (
              <>
                <TextField form={form} name="topic" label="Topic" required />
                <DateField form={form} name="date" label="Date" required />
                <FieldRow>
                  <TimeField form={form} name="startTime" label="Start time" />
                  <TimeField form={form} name="endTime" label="End time" />
                </FieldRow>
                <TextareaField form={form} name="description" label="Description" rows={2} />
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="nominees" className="pt-4">
          <NomineesPanel scheduleId={id} readOnly={isFinished} />
        </TabsContent>

        <TabsContent value="waitlist" className="pt-4">
          <WaitlistPanel scheduleId={id} readOnly={isFinished} />
        </TabsContent>

        <TabsContent value="attendance" className="pt-4">
          <AttendanceRegister
            scheduleId={id}
            defaultDate={schedule.startDate}
            readOnly={schedule.status === 'Cancelled'}
          />
        </TabsContent>

        {/* ⚠ TDC asked for bulk completion and the endpoint was built and never called. */}
        <TabsContent value="completion" className="pt-4">
          <BulkCompletionPanel scheduleId={id} readOnly={schedule.status === 'Cancelled'} />
        </TabsContent>

        <TabsContent value="feedback" className="pt-4">
          <FeedbackPanel scheduleId={id} />
        </TabsContent>

        <TabsContent value="follow-up" className="pt-4">
          <FollowUpPanel scheduleId={id} />
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={confirmAction !== null}
        onOpenChange={(o) => !o && setConfirmAction(null)}
        title={confirmAction === 'approve' ? 'Approve this schedule?' : 'Mark as completed?'}
        description={
          confirmAction === 'approve'
            ? 'Registration opens and people can be nominated onto it.'
            : 'This credits the delivering trainer with the session and its taught hours. It cannot be undone.'
        }
        confirmText={confirmAction === 'approve' ? 'Approve' : 'Mark completed'}
        isLoading={busy}
        onConfirm={runAction}
      />

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Cancel this schedule</DialogTitle>
            <DialogDescription>
              Nominees keep their records but the run will not go ahead. You will be recorded as the
              person who cancelled it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2 py-2">
            <Label htmlFor="cancel-reason">Reason</Label>
            <Textarea
              id="cancel-reason"
              rows={3}
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder="Why is this being cancelled?"
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)} disabled={busy}>
              Keep it
            </Button>
            <Button variant="destructive" onClick={runCancel} disabled={busy}>
              Cancel schedule
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="font-medium">{value}</p>
    </div>
  );
}
