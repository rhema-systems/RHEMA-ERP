'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQueryClient } from '@tanstack/react-query';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  DateField,
  DateTimeField,
  FieldRow,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
  fromIsoInstant,
  toIsoInstant,
} from '@/components/hr/employee/tabs/fields';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { companyEventService } from '@/services/hr/company-schedule.service';
import {
  EVENT_ATTACHMENT_TYPES,
  EVENT_TASK_CATEGORIES,
  EVENT_TASK_SETTABLE_STATUSES,
  INVITATION_ANSWERS,
  PARTICIPANT_ROLES,
  SERIES_SCOPE_LABELS,
  SERIES_SCOPES,
  TASK_PRIORITIES,
} from '@/types/hr/company-schedule';
import type {
  EventAttachment,
  EventAttendance,
  EventParticipant,
  EventTask,
  SeriesScope,
} from '@/types/hr/company-schedule';
import { SeriesGuestDialog, describeSeriesGuest } from './SeriesGuestDialog';
import type { SeriesGuestAction } from './SeriesGuestDialog';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');
const opts = (v: readonly string[]) => v.map((x) => ({ value: x, label: spaced(x) }));
const orNull = (s?: string) => (s && s.trim() ? s.trim() : null);

/**
 * The four collections that hang off an event. Each is a `ResourceCollectionTab` — the same
 * table-with-dialog the employee profile uses — so the only thing written here is what differs:
 * columns, schema, fields and the service calls.
 */

// ── Participants ──────────────────────────────────────────────────────────────

const participantSchema = z
  .object({
    employeeId: z.string().optional().or(z.literal('')),
    externalParticipantName: z.string().max(100).optional().or(z.literal('')),
    externalParticipantEmail: z.string().max(100).email('Enter a valid email').optional().or(z.literal('')),
    externalParticipantOrganization: z.string().max(100).optional().or(z.literal('')),
    role: z.string().min(1, 'Role is required'),
    isRequired: z.boolean(),
    specialRequirements: z.string().max(1000).optional().or(z.literal('')),
    // Lane 2f-2a: on a recurring event, which dates the guest is invited to.
    scope: z.string(),
  })
  .refine((v) => !!v.employeeId || !!v.externalParticipantName?.trim(), {
    message: 'Pick an employee, or name a guest from outside',
    path: ['employeeId'],
  })
  .refine(
    (v) => !(v.employeeId && (v.externalParticipantName || v.externalParticipantEmail || v.externalParticipantOrganization)),
    { message: 'A guest is either an employee or someone from outside, not both', path: ['externalParticipantName'] },
  )
  // Lane 2d: the server refuses an outside guest with no address — the invitation goes to it.
  .refine((v) => !!v.employeeId || !!v.externalParticipantEmail?.trim(), {
    message: 'A guest from outside needs an email address — the invitation goes to it',
    path: ['externalParticipantEmail'],
  });

type ParticipantForm = z.infer<typeof participantSchema>;

const emptyParticipant: ParticipantForm = {
  employeeId: '',
  externalParticipantName: '',
  externalParticipantEmail: '',
  externalParticipantOrganization: '',
  role: 'Attendee',
  isRequired: true,
  specialRequirements: '',
  scope: 'ThisOccurrence',
};

/**
 * The guest list (lane 2d). Guests are corrected in place (C-22) and removed on Write. A cancelled or
 * completed event's list is its record: no adds, edits, removals or answers — the server refuses them too.
 *
 * On a recurring event (lane 2f-2a, D-12) a guest can be invited to this and following dates or every date, and
 * answered for or taken off several dates at once — each told once, listing the dates.
 */
export function ParticipantsPanel({
  eventId,
  open,
  awaitingApproval = false,
  mailServerSetUp = true,
  inSeries = false,
}: {
  eventId: string;
  open: boolean;
  /** F-33 (lane 2e-1): guests added now are invited when the event is approved. */
  awaitingApproval?: boolean;
  /** Lane 2e-2: why an invitation reached nobody, for the toast. */
  mailServerSetUp?: boolean;
  /** Lane 2f-2a: the event is one date of a series, so guest actions can reach other dates. */
  inSeries?: boolean;
}) {
  const queryClient = useQueryClient();
  const [seriesAction, setSeriesAction] = useState<SeriesGuestAction | null>(null);
  const key = ['hr', 'company-schedule', 'events', eventId, 'participants'];
  // Lane 2e-2 (R4-6.3): an invitation is Sent only once it reached the guest — by an email the mail server took,
  // or in the app. Not yet sent is either waiting for the approval or not delivered.
  const whyNotDelivered = `${
    mailServerSetUp ? 'the mail server took no email for them' : 'no mail server is set up'
  }, and they have no login to be told in the app. Send it again from Invitations and reminders once that is fixed.`;

  return (
    <>
    <ResourceCollectionTab<EventParticipant, ParticipantForm>
      parentId={eventId}
      title="participants"
      singular="participant"
      queryKey={key}
      // Lane 2f-2a: an add with a series scope puts the guest on other dates too.
      invalidateKeys={
        inSeries
          ? [['hr', 'company-schedule', 'events']]
          : [['hr', 'company-schedule', 'events', eventId, 'detail']]
      }
      readOnly={!open}
      dialogHint={
        awaitingApproval
          ? 'The event awaits approval: the invitation goes when it is approved, not now.'
          : 'Invite an employee, or a guest from outside with their email address — the invitation goes there.'
      }
      savedDescription={(saved, editing) => {
        // Lane 2f-2a: added to several dates — which, which were passed over, and who the one invitation reached.
        const series = (saved as EventParticipant | undefined)?.series;
        if (!editing && series) return `Invited to ${describeSeriesGuest(series, 'already invited')}`;
        if ((saved as EventParticipant | undefined)?.invitationStatus !== 'NotSent') return null;
        if (awaitingApproval) return editing ? null : 'Added. The invitation goes when the event is approved.';
        // Added — or corrected (a new outside address re-sends it) — and it reached nobody.
        return editing
          ? `Saved. Their invitation has not reached them: ${whyNotDelivered}`
          : `Added, but the invitation reached nobody: ${whyNotDelivered}`;
      }}
      emptyDescription={open ? 'Nobody has been invited to this event yet.' : 'Nobody was invited to this event.'}
      list={(id) => companyEventService.getParticipants(id)}
      create={(id, v) =>
        companyEventService.addParticipant(id, {
          employeeId: orNull(v.employeeId),
          externalParticipantName: orNull(v.externalParticipantName),
          externalParticipantEmail: orNull(v.externalParticipantEmail),
          externalParticipantOrganization: orNull(v.externalParticipantOrganization),
          role: v.role as EventParticipant['role'],
          isRequired: v.isRequired,
          specialRequirements: orNull(v.specialRequirements),
          scope: inSeries ? (v.scope as SeriesScope) : undefined,
        })
      }
      update={(_id, participantId, v) =>
        companyEventService.updateParticipant(participantId, {
          externalParticipantName: orNull(v.externalParticipantName),
          externalParticipantEmail: orNull(v.externalParticipantEmail),
          externalParticipantOrganization: orNull(v.externalParticipantOrganization),
          role: v.role as EventParticipant['role'],
          isRequired: v.isRequired,
          specialRequirements: orNull(v.specialRequirements),
        })
      }
      remove={(_id, participantId) => companyEventService.removeParticipant(participantId)}
      getId={(p) => p.id}
      columns={[
        { header: 'Participant', cell: (p) => p.participantName },
        {
          header: 'Kind',
          cell: (p) => (p.employeeId ? 'Employee' : p.externalParticipantOrganization || 'External'),
        },
        { header: 'Role', cell: (p) => spaced(p.role) },
        { header: 'Required', cell: (p) => (p.isRequired ? 'Yes' : 'Optional') },
        {
          header: 'Invitation',
          // Not sent: it waits for the event's approval (F-33), or it reached nobody (lane 2e-2).
          cell: (p) => (
            <StatusBadge
              status={
                p.invitationStatus !== 'NotSent'
                  ? spaced(p.invitationStatus)
                  : awaitingApproval
                    ? 'Waits for approval'
                    : 'Not delivered'
              }
            />
          ),
        },
        { header: 'Responded', cell: (p) => p.responseDate?.slice(0, 10) ?? '—' },
      ]}
      actions={[
        ...INVITATION_ANSWERS.map((response) => ({
          label: `Record ${response.toLowerCase()}`,
          visible: (p: EventParticipant) => open && p.invitationStatus !== response,
          run: async (p: EventParticipant) => {
            await companyEventService.respondToInvitation(eventId, {
              participantId: p.id,
              response,
              responseComments: null,
            });
            await queryClient.invalidateQueries({ queryKey: key });
          },
        })),
        // Lane 2f-2a: the same guest on several dates at once, told once.
        ...(inSeries
          ? [
              {
                label: 'Answer for several dates…',
                visible: () => open,
                run: async (p: EventParticipant) => setSeriesAction({ mode: 'answer', guest: p }),
              },
              {
                label: 'Take off several dates…',
                visible: () => open,
                destructive: true,
                run: async (p: EventParticipant) => setSeriesAction({ mode: 'remove', guest: p }),
              },
            ]
          : []),
      ]}
      schema={participantSchema}
      emptyForm={emptyParticipant}
      toForm={(p) => ({
        employeeId: p.employeeId ?? '',
        externalParticipantName: p.externalParticipantName ?? '',
        externalParticipantEmail: p.externalParticipantEmail ?? '',
        externalParticipantOrganization: p.externalParticipantOrganization ?? '',
        role: p.role,
        isRequired: p.isRequired,
        specialRequirements: p.specialRequirements ?? '',
        scope: 'ThisOccurrence',
      })}
      renderFields={(form, editing) => {
        // An employee guest stays who they are: uninvite and invite the other person instead.
        const employeeGuest = editing && !!form.watch('employeeId');
        return (
          <>
            {(!editing || employeeGuest) && (
              <EmployeePickerField form={form} name="employeeId" label="Employee" disabled={editing} />
            )}
            {employeeGuest ? (
              <p className="text-xs text-muted-foreground">
                To invite someone else instead, remove this guest and invite them.
              </p>
            ) : (
              <>
                {!editing && <p className="text-xs text-muted-foreground">Or add someone from outside the organisation:</p>}
                <FieldRow>
                  <TextField form={form} name="externalParticipantName" label="External name" />
                  <TextField form={form} name="externalParticipantEmail" label="External email" type="email" />
                </FieldRow>
                <TextField form={form} name="externalParticipantOrganization" label="External organisation" />
                {editing && (
                  <p className="text-xs text-muted-foreground">A changed email address is sent the invitation.</p>
                )}
              </>
            )}
            <FieldRow>
              <SelectField form={form} name="role" label="Role" required options={opts(PARTICIPANT_ROLES)} />
              <SwitchField form={form} name="isRequired" label="Attendance required" />
            </FieldRow>
            <TextareaField form={form} name="specialRequirements" label="Special requirements" />
            {inSeries && !editing && (
              <>
                <SelectField
                  form={form}
                  name="scope"
                  label="Which dates"
                  options={SERIES_SCOPES.map((s) => ({ value: s, label: SERIES_SCOPE_LABELS[s] }))}
                />
                <p className="text-xs text-muted-foreground">
                  A date that has started, been completed or been cancelled is passed over, as is one they are already
                  on. They are invited once, listing the dates.
                </p>
              </>
            )}
          </>
        );
      }}
    />
    <SeriesGuestDialog eventId={eventId} action={seriesAction} onClose={() => setSeriesAction(null)} />
    </>
  );
}

// ── Attendance ────────────────────────────────────────────────────────────────

const attendanceSchema = z.object({
  employeeId: z.string().min(1, 'Pick an employee'),
  attended: z.boolean(),
  checkInTime: z.string().optional().or(z.literal('')),
  absenceReason: z.string().max(1000).optional().or(z.literal('')),
  notes: z.string().max(1000).optional().or(z.literal('')),
});

type AttendanceForm = z.infer<typeof attendanceSchema>;

const emptyAttendance: AttendanceForm = {
  employeeId: '',
  attended: true,
  checkInTime: '',
  absenceReason: '',
  notes: '',
};

/**
 * The register (lane 2d). It is taken once the event has started and never for a cancelled one;
 * editing a row marks it again (F-1: the check-in is kept unless a new one is given), and a wrong row is
 * removed on Write (C-21).
 */
export function AttendancePanel({
  eventId,
  markable,
  notMarkable,
}: {
  eventId: string;
  /** Started and not cancelled — when the server takes a register. */
  markable: boolean;
  /** Why not, when it cannot be marked. */
  notMarkable?: string;
}) {
  const queryClient = useQueryClient();
  const key = ['hr', 'company-schedule', 'events', eventId, 'attendance'];
  const mark = (id: string, v: AttendanceForm) =>
    companyEventService.markAttendance(id, {
      employeeId: v.employeeId,
      attended: v.attended,
      checkInTime: v.attended ? toIsoInstant(v.checkInTime) : null,
      absenceReason: v.attended ? null : orNull(v.absenceReason),
      notes: orNull(v.notes),
    });

  return (
    <ResourceCollectionTab<EventAttendance, AttendanceForm>
      parentId={eventId}
      title="attendance"
      singular="attendance record"
      queryKey={key}
      invalidateKeys={[['hr', 'company-schedule', 'events', eventId, 'detail']]}
      dialogHint="You are recorded as the person who marked it. Leave the check-in blank for now, or give the time they arrived."
      emptyDescription={markable ? 'No attendance has been marked for this event.' : notMarkable}
      allowCreate={markable}
      allowUpdate={markable}
      list={(id) => companyEventService.getAttendance(id)}
      create={mark}
      update={(id, _attendanceId, v) => mark(id, v)}
      remove={(id, attendanceId) => companyEventService.removeAttendance(id, attendanceId)}
      getId={(a) => a.id}
      columns={[
        { header: 'Employee', cell: (a) => a.employeeName },
        { header: 'Attended', cell: (a) => <StatusBadge status={a.attended ? 'Present' : 'Absent'} /> },
        { header: 'Checked in', cell: (a) => (a.checkInTime ? new Date(a.checkInTime).toLocaleString() : '—') },
        { header: 'Checked out', cell: (a) => (a.checkOutTime ? new Date(a.checkOutTime).toLocaleString() : '—') },
        { header: 'Reason', cell: (a) => a.absenceReason || '—' },
        { header: 'Marked by', cell: (a) => a.markedByName || '—' },
      ]}
      actions={[
        {
          label: 'Check out',
          visible: (a) => markable && a.attended && !!a.checkInTime && !a.checkOutTime,
          run: async (a) => {
            await companyEventService.checkOut(a.id, null);
            await queryClient.invalidateQueries({ queryKey: key });
          },
        },
      ]}
      schema={attendanceSchema}
      emptyForm={emptyAttendance}
      toForm={(a) => ({
        employeeId: a.employeeId,
        attended: a.attended,
        checkInTime: fromIsoInstant(a.checkInTime),
        absenceReason: a.absenceReason ?? '',
        notes: a.notes ?? '',
      })}
      renderFields={(form, editing) => {
        const attended = form.watch('attended');
        return (
          <>
            <EmployeePickerField form={form} name="employeeId" label="Employee" required disabled={editing} />
            <SwitchField form={form} name="attended" label="Attended" />
            {attended ? (
              <>
                <DateTimeField form={form} name="checkInTime" label="Check-in time" />
                <p className="text-xs text-muted-foreground">
                  On one of the event&apos;s days and not still to come. Blank keeps the time already recorded, or now.
                </p>
              </>
            ) : (
              <TextareaField form={form} name="absenceReason" label="Reason for absence" />
            )}
            <TextareaField form={form} name="notes" label="Notes" />
          </>
        );
      }}
    />
  );
}

// ── Attachments ───────────────────────────────────────────────────────────────

// Nothing is typed into a form any more: a file is uploaded (below), and a row cannot be edited.
const attachmentSchema = z.object({});
type AttachmentForm = z.infer<typeof attachmentSchema>;

/** "1.2 MB", "340 KB". */
const size = (bytes?: number | null) =>
  bytes == null ? '—' : bytes >= 1024 * 1024 ? `${(bytes / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;

/**
 * An event's files (lane 2h, C-18): uploaded through the gate — scanned, stored and downloadable — with what they are
 * and a line about them. A row from before the gate is a name and a path someone typed, with no file stored (F-54): it
 * reads "Reference only — no file stored" and offers no download. Removing one is Write (the user's ruling). A
 * cancelled event takes no more files.
 */
export function AttachmentsPanel({ eventId, cancelled = false }: { eventId: string; cancelled?: boolean }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const key = ['hr', 'company-schedule', 'events', eventId, 'attachments'];
  const [type, setType] = useState<string>('Agenda');
  const [description, setDescription] = useState('');

  return (
    <div className="space-y-4">
      {!cancelled && (
        <div className="space-y-3 rounded-md border p-4">
          <p className="text-sm font-medium">Add a file</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1">
              <Label>What it is</Label>
              <Select value={type} onValueChange={(v) => v && setType(v)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {EVENT_ATTACHMENT_TYPES.map((t) => (
                    <SelectItem key={t} value={t}>{spaced(t)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="attachmentDescription">About it (optional)</Label>
              <Input id="attachmentDescription" value={description} maxLength={1000} onChange={(e) => setDescription(e.target.value)} />
            </div>
          </div>
          <DocumentUploadField
            label="File"
            endpoint={companyEventService.attachmentUploadEndpoint(eventId)}
            fields={{ type, description: description.trim() || undefined }}
            maxSizeMb={25}
            helpText="Agendas, minutes, presentations and handouts. Each file is scanned before it is stored."
            onUploaded={async () => {
              await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events', eventId] });
              setDescription('');
              toast({ title: 'File attached' });
            }}
          />
        </div>
      )}
      <ResourceCollectionTab<EventAttachment, AttachmentForm>
        parentId={eventId}
        title="attachments"
        singular="attachment"
        queryKey={key}
        emptyDescription="No files are attached to this event."
        list={(id) => companyEventService.getAttachments(id)}
        allowCreate={false}
        create={async () => undefined}
        allowUpdate={false}
        update={async () => undefined}
        remove={(_id, attachmentId) => companyEventService.removeAttachment(attachmentId)}
        getId={(a) => a.id}
        columns={[
          {
            header: 'File',
            cell: (a) =>
              a.hasFile ? (
                a.fileName
              ) : (
                <span>
                  {a.fileName}
                  <span className="block text-xs text-muted-foreground">Reference only — no file stored</span>
                </span>
              ),
          },
          { header: 'Type', cell: (a) => spaced(a.type) },
          { header: 'About it', cell: (a) => a.description || '—' },
          { header: 'Size', cell: (a) => (a.hasFile ? size(a.fileSizeBytes) : '—') },
          { header: 'Added', cell: (a) => a.uploadDate?.slice(0, 10) ?? '—' },
        ]}
        actions={[
          {
            label: 'Download',
            visible: (a: EventAttachment) => a.hasFile,
            run: (a: EventAttachment) => companyEventService.downloadAttachment(a),
          },
        ]}
        schema={attachmentSchema}
        emptyForm={{}}
        toForm={() => ({})}
        renderFields={() => null}
      />
    </div>
  );
}

// ── Tasks ─────────────────────────────────────────────────────────────────────

const taskSchema = z.object({
  taskDescription: z.string().min(1, 'Describe the task').max(1000),
  category: z.string().min(1, 'Category is required'),
  assignedToId: z.string().optional().or(z.literal('')),
  dueDate: z.string().optional().or(z.literal('')),
  priority: z.string().min(1),
  status: z.string().min(1),
});

type TaskForm = z.infer<typeof taskSchema>;

const emptyTask: TaskForm = {
  taskDescription: '',
  category: 'Preparation',
  assignedToId: '',
  dueDate: '',
  priority: 'Medium',
  status: 'NotStarted',
};

export function TasksPanel({ eventId }: { eventId: string }) {
  const queryClient = useQueryClient();
  const key = ['hr', 'company-schedule', 'events', eventId, 'tasks'];

  return (
    <ResourceCollectionTab<EventTask, TaskForm>
      parentId={eventId}
      title="tasks"
      singular="task"
      queryKey={key}
      dialogHint="Everything that has to happen before, during and after the event."
      emptyDescription="No tasks have been raised for this event."
      list={(id) => companyEventService.getTasks(id)}
      create={(id, v) =>
        companyEventService.addTask(id, {
          taskDescription: v.taskDescription.trim(),
          category: v.category as EventTask['category'],
          assignedToId: orNull(v.assignedToId),
          dueDate: orNull(v.dueDate),
          priority: v.priority as EventTask['priority'],
        })
      }
      update={(_id, taskId, v) =>
        companyEventService.updateTask(taskId, {
          id: taskId,
          taskDescription: v.taskDescription.trim(),
          category: v.category as EventTask['category'],
          assignedToId: orNull(v.assignedToId),
          dueDate: orNull(v.dueDate),
          priority: v.priority as EventTask['priority'],
          status: v.status as EventTask['status'],
        })
      }
      remove={(_id, taskId) => companyEventService.removeTask(taskId)}
      getId={(t) => t.id}
      columns={[
        { header: 'Task', cell: (t) => t.taskDescription },
        { header: 'Stage', cell: (t) => spaced(t.category) },
        { header: 'Assigned to', cell: (t) => t.assignedToName || '—' },
        { header: 'Due', cell: (t) => t.dueDate?.slice(0, 10) ?? '—' },
        { header: 'Priority', cell: (t) => spaced(t.priority) },
        {
          header: 'Status',
          // Overdue is the server's reading of the due date (lane 2d), shown beside where the task stands; the hourly
          // sweep chases the assignee once (lane 2e-3), and says when.
          cell: (t) => (
            <span className="flex flex-wrap items-center gap-1">
              <StatusBadge status={spaced(t.status)} />
              {t.isOverdue && <StatusBadge status="Overdue" />}
              {t.isOverdue && t.overdueChasedAt && (
                <span className="text-xs text-muted-foreground">
                  assignee chased {new Date(t.overdueChasedAt).toLocaleDateString()}
                </span>
              )}
            </span>
          ),
        },
      ]}
      actions={[
        {
          label: 'Mark complete',
          visible: (t) => t.status !== 'Completed' && t.status !== 'Cancelled',
          run: async (t) => {
            await companyEventService.completeTask(t.id, null);
            await queryClient.invalidateQueries({ queryKey: key });
          },
        },
      ]}
      schema={taskSchema}
      emptyForm={emptyTask}
      toForm={(t) => ({
        taskDescription: t.taskDescription,
        category: t.category,
        assignedToId: t.assignedToId ?? '',
        dueDate: t.dueDate?.slice(0, 10) ?? '',
        priority: t.priority,
        // A legacy row stored as Overdue opens as in progress: Overdue is no longer something to set.
        status: t.status === 'Overdue' ? 'InProgress' : t.status,
      })}
      renderFields={(form, editing) => (
        <>
          <TextareaField form={form} name="taskDescription" label="Task" />
          <FieldRow>
            <SelectField form={form} name="category" label="Stage" required options={opts(EVENT_TASK_CATEGORIES)} />
            <SelectField form={form} name="priority" label="Priority" required options={opts(TASK_PRIORITIES)} />
          </FieldRow>
          <EmployeePickerField form={form} name="assignedToId" label="Assigned to" />
          <FieldRow>
            <DateField form={form} name="dueDate" label="Due date" />
            {/* Status is update-only: a new task is always NotStarted. */}
            {editing && (
              <SelectField form={form} name="status" label="Status" options={opts(EVENT_TASK_SETTABLE_STATUSES)} />
            )}
          </FieldRow>
        </>
      )}
    />
  );
}
