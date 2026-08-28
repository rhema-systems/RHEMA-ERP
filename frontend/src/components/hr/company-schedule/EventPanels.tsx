'use client';

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
import { companyEventService } from '@/services/hr/company-schedule.service';
import {
  EVENT_ATTACHMENT_TYPES,
  EVENT_TASK_CATEGORIES,
  EVENT_TASK_STATUSES,
  INVITATION_STATUSES,
  PARTICIPANT_ROLES,
  TASK_PRIORITIES,
} from '@/types/hr/company-schedule';
import type {
  EventAttachment,
  EventAttendance,
  EventParticipant,
  EventTask,
} from '@/types/hr/company-schedule';

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
  })
  .refine((v) => !!v.employeeId || !!v.externalParticipantName, {
    message: 'Pick an employee, or name an external participant',
    path: ['employeeId'],
  })
  .refine((v) => !(v.employeeId && v.externalParticipantName), {
    message: 'A participant is either an employee or an external guest, not both',
    path: ['externalParticipantName'],
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
};

export function ParticipantsPanel({ eventId }: { eventId: string }) {
  const queryClient = useQueryClient();
  const key = ['hr', 'company-schedule', 'events', eventId, 'participants'];

  return (
    <ResourceCollectionTab<EventParticipant, ParticipantForm>
      parentId={eventId}
      title="participants"
      singular="participant"
      queryKey={key}
      invalidateKeys={[['hr', 'company-schedule', 'events', eventId, 'detail']]}
      dialogHint="Invite an employee, or add an external guest."
      emptyDescription="Nobody has been invited to this event yet."
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
        })
      }
      // The API has no participant update — rows are added and removed, never edited.
      allowUpdate={false}
      update={async () => undefined}
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
        { header: 'Invitation', cell: (p) => <StatusBadge status={spaced(p.invitationStatus)} /> },
        { header: 'Responded', cell: (p) => p.responseDate?.slice(0, 10) ?? '—' },
      ]}
      actions={INVITATION_STATUSES.filter((s) => s === 'Accepted' || s === 'Declined' || s === 'Tentative').map(
        (response) => ({
          label: `Record ${response.toLowerCase()}`,
          visible: (p: EventParticipant) => p.invitationStatus !== response,
          run: async (p: EventParticipant) => {
            await companyEventService.respondToInvitation(eventId, {
              participantId: p.id,
              response,
              responseComments: null,
            });
            await queryClient.invalidateQueries({ queryKey: key });
          },
        }),
      )}
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
      })}
      renderFields={(form) => (
        <>
          <EmployeePickerField form={form} name="employeeId" label="Employee" />
          <p className="text-xs text-muted-foreground">Or add someone from outside the organisation:</p>
          <FieldRow>
            <TextField form={form} name="externalParticipantName" label="External name" />
            <TextField form={form} name="externalParticipantEmail" label="External email" type="email" />
          </FieldRow>
          <TextField form={form} name="externalParticipantOrganization" label="External organisation" />
          <FieldRow>
            <SelectField form={form} name="role" label="Role" required options={opts(PARTICIPANT_ROLES)} />
            <SwitchField form={form} name="isRequired" label="Attendance required" />
          </FieldRow>
          <TextareaField form={form} name="specialRequirements" label="Special requirements" />
        </>
      )}
    />
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

export function AttendancePanel({ eventId }: { eventId: string }) {
  const queryClient = useQueryClient();
  const key = ['hr', 'company-schedule', 'events', eventId, 'attendance'];

  return (
    <ResourceCollectionTab<EventAttendance, AttendanceForm>
      parentId={eventId}
      title="attendance"
      singular="attendance record"
      queryKey={key}
      dialogHint="You are recorded as the person who marked it."
      emptyDescription="No attendance has been marked for this event."
      list={(id) => companyEventService.getAttendance(id)}
      create={(id, v) =>
        companyEventService.markAttendance(id, {
          employeeId: v.employeeId,
          attended: v.attended,
          checkInTime: toIsoInstant(v.checkInTime),
          absenceReason: v.attended ? null : orNull(v.absenceReason),
          notes: orNull(v.notes),
        })
      }
      // Marking again for the same employee is how a record is corrected — there is no PUT.
      allowUpdate={false}
      update={async () => undefined}
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
          visible: (a) => a.attended && !a.checkOutTime,
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
      renderFields={(form) => {
        const attended = form.watch('attended');
        return (
          <>
            <EmployeePickerField form={form} name="employeeId" label="Employee" required />
            <SwitchField form={form} name="attended" label="Attended" />
            {attended ? (
              <DateTimeField form={form} name="checkInTime" label="Check-in time" />
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

const attachmentSchema = z.object({
  fileName: z.string().min(1, 'File name is required').max(200),
  filePath: z.string().min(1, 'File path is required').max(500),
  type: z.string().min(1, 'Type is required'),
  description: z.string().max(1000).optional().or(z.literal('')),
});

type AttachmentForm = z.infer<typeof attachmentSchema>;

const emptyAttachment: AttachmentForm = {
  fileName: '',
  filePath: '',
  type: 'Agenda',
  description: '',
};

export function AttachmentsPanel({ eventId }: { eventId: string }) {
  return (
    <ResourceCollectionTab<EventAttachment, AttachmentForm>
      parentId={eventId}
      title="attachments"
      singular="attachment"
      queryKey={['hr', 'company-schedule', 'events', eventId, 'attachments']}
      dialogHint="Agendas, minutes, presentations and handouts for this event."
      emptyDescription="No documents are attached to this event."
      list={(id) => companyEventService.getAttachments(id)}
      create={(id, v) =>
        companyEventService.addAttachment(id, {
          fileName: v.fileName.trim(),
          filePath: v.filePath.trim(),
          type: v.type as EventAttachment['type'],
          description: orNull(v.description),
        })
      }
      allowUpdate={false}
      update={async () => undefined}
      remove={(_id, attachmentId) => companyEventService.removeAttachment(attachmentId)}
      getId={(a) => a.id}
      columns={[
        { header: 'File', cell: (a) => a.fileName },
        { header: 'Type', cell: (a) => spaced(a.type) },
        { header: 'Description', cell: (a) => a.description || '—' },
        { header: 'Uploaded', cell: (a) => a.uploadDate?.slice(0, 10) ?? '—' },
      ]}
      schema={attachmentSchema}
      emptyForm={emptyAttachment}
      toForm={(a) => ({
        fileName: a.fileName,
        filePath: a.filePath,
        type: a.type,
        description: a.description ?? '',
      })}
      renderFields={(form) => (
        <>
          <FieldRow>
            <TextField form={form} name="fileName" label="File name" required />
            <SelectField form={form} name="type" label="Type" required options={opts(EVENT_ATTACHMENT_TYPES)} />
          </FieldRow>
          <TextField form={form} name="filePath" label="File path" required />
          <TextareaField form={form} name="description" label="Description" />
        </>
      )}
    />
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
        { header: 'Status', cell: (t) => <StatusBadge status={spaced(t.status)} /> },
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
        status: t.status,
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
              <SelectField form={form} name="status" label="Status" options={opts(EVENT_TASK_STATUSES)} />
            )}
          </FieldRow>
        </>
      )}
    />
  );
}
