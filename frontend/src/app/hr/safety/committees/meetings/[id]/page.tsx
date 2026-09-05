'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { CheckSquare, FileText, Loader2, MoreHorizontal, Pencil, Plus, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
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
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  DateField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyCommitteeService } from '@/services/hr/safety-committee.service';
import {
  SHE_MEETING_TYPE_OPTIONS,
  SHE_ACTION_PRIORITY_OPTIONS,
  SHE_ACTION_STATUS_OPTIONS,
} from '@/types/hr/safety-governance';
import type {
  SafetyMeetingActionItem,
  SafetyMeetingAttendee,
  SafetyMeetingDocument,
  SheActionItemPriority,
  SheActionItemStatus,
  SheSafetyMeetingType,
} from '@/types/hr/safety-governance';

/**
 * Meeting workspace: the record itself (minutes/topics/decisions are debrief fields — edited
 * here after the meeting), the attendee roll (one row per employee, duplicates refused), the
 * action items raised (status tracked to completion) and supporting documents.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtTime = (v?: string | null) => (v ? v.slice(0, 5) : null);
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const toTimeSpan = (v?: string) => (v && v.length > 0 ? `${v}:00` : null);

const debriefSchema = z.object({
  meetingDate: z.string().min(1, 'A date is required'),
  startTime: z.string().optional().or(z.literal('')),
  endTime: z.string().optional().or(z.literal('')),
  location: z.string().min(1, 'A location is required').max(200),
  type: z.string().min(1),
  agenda: z.string().max(3000).optional().or(z.literal('')),
  minutes: z.string().max(5000).optional().or(z.literal('')),
  topicsDiscussed: z.string().max(2000).optional().or(z.literal('')),
  decisionsMade: z.string().max(1000).optional().or(z.literal('')),
  facilitatorId: z.string().optional().or(z.literal('')),
  attendeesCount: z.coerce.number().int().nonnegative().optional().or(z.literal('')),
});
type DebriefForm = z.input<typeof debriefSchema>;

const attendeeSchema = z.object({
  employeeId: z.string().min(1, 'An employee is required'),
  attended: z.boolean(),
  signedDate: z.string().optional().or(z.literal('')),
});
type AttendeeForm = z.input<typeof attendeeSchema>;

const actionSchema = z.object({
  actionDescription: z.string().min(1, 'A description is required').max(1000),
  priority: z.string().min(1),
  status: z.string().min(1),
  assignedToId: z.string().optional().or(z.literal('')),
  dueDate: z.string().optional().or(z.literal('')),
  completionDate: z.string().optional().or(z.literal('')),
  completionNotes: z.string().max(500).optional().or(z.literal('')),
});
type ActionForm = z.input<typeof actionSchema>;

const documentSchema = z.object({
  fileName: z.string().min(1, 'A file name is required').max(255),
  filePath: z.string().min(1, 'A file path is required').max(500),
  description: z.string().max(300).optional().or(z.literal('')),
});
type DocumentForm = z.input<typeof documentSchema>;

const priorityVariant = (p: SheActionItemPriority) =>
  p === 'Critical' ? 'destructive' : p === 'High' ? 'default' : 'secondary';

export default function SafetyMeetingDetailPage() {
  const params = useParams<{ id: string }>();
  const meetingId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [debriefOpen, setDebriefOpen] = useState(false);
  const [attendeeOpen, setAttendeeOpen] = useState(false);
  const [actionOpen, setActionOpen] = useState(false);
  const [editingAction, setEditingAction] = useState<SafetyMeetingActionItem | null>(null);
  const [documentOpen, setDocumentOpen] = useState(false);
  const [pendingRemove, setPendingRemove] = useState<
    | { kind: 'attendee'; row: SafetyMeetingAttendee }
    | { kind: 'action'; row: SafetyMeetingActionItem }
    | { kind: 'document'; row: SafetyMeetingDocument }
    | null
  >(null);
  const [busy, setBusy] = useState(false);

  const { data: meeting, isLoading } = useQuery({
    queryKey: ['hr', 'safety-committees', 'meeting', meetingId],
    queryFn: () => safetyCommitteeService.getMeeting(meetingId),
    enabled: !!meetingId,
  });

  const debriefForm = useForm<DebriefForm>({ resolver: zodResolver(debriefSchema) });
  const attendeeForm = useForm<AttendeeForm>({ resolver: zodResolver(attendeeSchema) });
  const actionForm = useForm<ActionForm>({ resolver: zodResolver(actionSchema) });
  const documentForm = useForm<DocumentForm>({ resolver: zodResolver(documentSchema) });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-committees'] });

  const fail = (error: any, fallback: string) =>
    toast({ title: 'Error', description: error?.message || fallback, variant: 'destructive' });

  const openDebrief = () => {
    if (!meeting) return;
    debriefForm.reset({
      meetingDate: meeting.meetingDate.slice(0, 10),
      startTime: fmtTime(meeting.startTime) ?? '',
      endTime: fmtTime(meeting.endTime) ?? '',
      location: meeting.location,
      type: meeting.type,
      agenda: meeting.agenda ?? '',
      minutes: meeting.minutes ?? '',
      topicsDiscussed: meeting.topicsDiscussed ?? '',
      decisionsMade: meeting.decisionsMade ?? '',
      facilitatorId: meeting.facilitatorId ?? '',
      attendeesCount: meeting.attendeesCount ?? '',
    });
    setDebriefOpen(true);
  };

  const submitDebrief = debriefForm.handleSubmit(async (values) => {
    if (!meeting) return;
    setBusy(true);
    try {
      const v = debriefSchema.parse(values);
      await safetyCommitteeService.updateMeeting(meeting.id, {
        id: meeting.id,
        committeeId: meeting.committeeId,
        meetingDate: new Date(v.meetingDate).toISOString(),
        startTime: toTimeSpan(v.startTime),
        endTime: toTimeSpan(v.endTime),
        location: v.location,
        type: v.type as SheSafetyMeetingType,
        agenda: blank(v.agenda),
        minutes: blank(v.minutes),
        topicsDiscussed: blank(v.topicsDiscussed),
        decisionsMade: blank(v.decisionsMade),
        facilitatorId: blank(v.facilitatorId),
        attendeesCount: typeof v.attendeesCount === 'number' ? v.attendeesCount : null,
      });
      await refresh();
      toast({ title: 'Meeting updated' });
      setDebriefOpen(false);
    } catch (error: any) {
      fail(error, 'Saving the meeting failed.');
    } finally {
      setBusy(false);
    }
  });

  const openAddAttendee = () => {
    attendeeForm.reset({
      employeeId: '',
      attended: true,
      signedDate: new Date().toISOString().slice(0, 10),
    });
    setAttendeeOpen(true);
  };

  const submitAttendee = attendeeForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = attendeeSchema.parse(values);
      await safetyCommitteeService.addAttendee(meetingId, {
        meetingId,
        employeeId: v.employeeId,
        attended: v.attended,
        signedDate: v.signedDate ? new Date(v.signedDate).toISOString() : null,
      });
      await refresh();
      toast({ title: 'Attendee recorded' });
      setAttendeeOpen(false);
    } catch (error: any) {
      fail(error, 'Recording the attendee failed.');
    } finally {
      setBusy(false);
    }
  });

  const openAddAction = () => {
    setEditingAction(null);
    actionForm.reset({
      actionDescription: '',
      priority: 'Medium',
      status: 'Open',
      assignedToId: '',
      dueDate: '',
      completionDate: '',
      completionNotes: '',
    });
    setActionOpen(true);
  };

  const openEditAction = (a: SafetyMeetingActionItem) => {
    setEditingAction(a);
    actionForm.reset({
      actionDescription: a.actionDescription,
      priority: a.priority,
      status: a.status,
      assignedToId: a.assignedToId ?? '',
      dueDate: a.dueDate ? a.dueDate.slice(0, 10) : '',
      completionDate: a.completionDate ? a.completionDate.slice(0, 10) : '',
      completionNotes: a.completionNotes ?? '',
    });
    setActionOpen(true);
  };

  const submitAction = actionForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = actionSchema.parse(values);
      if (editingAction) {
        await safetyCommitteeService.updateActionItem(editingAction.id, {
          id: editingAction.id,
          actionDescription: v.actionDescription,
          priority: v.priority as SheActionItemPriority,
          status: v.status as SheActionItemStatus,
          assignedToId: blank(v.assignedToId),
          dueDate: v.dueDate ? new Date(v.dueDate).toISOString() : null,
          completionDate: v.completionDate ? new Date(v.completionDate).toISOString() : null,
          completionNotes: blank(v.completionNotes),
        });
      } else {
        await safetyCommitteeService.addActionItem(meetingId, {
          meetingId,
          actionDescription: v.actionDescription,
          priority: v.priority as SheActionItemPriority,
          assignedToId: blank(v.assignedToId),
          dueDate: v.dueDate ? new Date(v.dueDate).toISOString() : null,
        });
      }
      await refresh();
      toast({ title: editingAction ? 'Action item updated' : 'Action item raised' });
      setActionOpen(false);
    } catch (error: any) {
      fail(error, 'Saving the action item failed.');
    } finally {
      setBusy(false);
    }
  });

  const submitDocument = documentForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = documentSchema.parse(values);
      await safetyCommitteeService.addMeetingDocument(meetingId, {
        meetingId,
        fileName: v.fileName,
        filePath: v.filePath,
        description: blank(v.description),
      });
      await refresh();
      toast({ title: 'Document attached' });
      setDocumentOpen(false);
    } catch (error: any) {
      fail(error, 'Attaching the document failed.');
    } finally {
      setBusy(false);
    }
  });

  if (isLoading || !meeting) {
    return (
      <div className="text-muted-foreground flex items-center gap-2 p-6 text-sm">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading meeting…
      </div>
    );
  }

  const time =
    fmtTime(meeting.startTime) || fmtTime(meeting.endTime)
      ? `${fmtTime(meeting.startTime) ?? '?'} – ${fmtTime(meeting.endTime) ?? '?'}`
      : null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={meeting.meetingNumber}
        description={`${meeting.typeName} · ${fmtDate(meeting.meetingDate)}${time ? ` · ${time}` : ''} · ${meeting.location}`}
        backHref={
          meeting.committeeId
            ? `/hr/safety/committees/${meeting.committeeId}`
            : '/hr/safety/committees'
        }
        actions={
          <Button onClick={openDebrief}>
            <Pencil className="mr-2 h-4 w-4" /> Record minutes / edit
          </Button>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Record</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4 text-sm">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <div className="text-muted-foreground">Committee</div>
              <div className="font-medium">
                {meeting.committeeId ? (
                  <Link
                    href={`/hr/safety/committees/${meeting.committeeId}`}
                    className="text-primary hover:underline"
                  >
                    {meeting.committeeName}
                  </Link>
                ) : (
                  'Stand-alone'
                )}
              </div>
            </div>
            <div>
              <div className="text-muted-foreground">Facilitator</div>
              <div className="font-medium">{meeting.facilitatorName ?? '—'}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Headcount (reported)</div>
              <div className="font-medium">{meeting.attendeesCount ?? '—'}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Recorded attendees</div>
              <div className="font-medium">{meeting.attendees.length}</div>
            </div>
          </div>
          {meeting.agenda && (
            <div>
              <div className="text-muted-foreground">Agenda</div>
              <p className="whitespace-pre-wrap">{meeting.agenda}</p>
            </div>
          )}
          {meeting.minutes ? (
            <div>
              <div className="text-muted-foreground">Minutes</div>
              <p className="whitespace-pre-wrap">{meeting.minutes}</p>
            </div>
          ) : (
            <p className="text-muted-foreground italic">
              No minutes recorded yet — use “Record minutes / edit”.
            </p>
          )}
          {meeting.topicsDiscussed && (
            <div>
              <div className="text-muted-foreground">Topics discussed</div>
              <p className="whitespace-pre-wrap">{meeting.topicsDiscussed}</p>
            </div>
          )}
          {meeting.decisionsMade && (
            <div>
              <div className="text-muted-foreground">Decisions</div>
              <p className="whitespace-pre-wrap">{meeting.decisionsMade}</p>
            </div>
          )}
        </CardContent>
      </Card>

      <Tabs defaultValue="attendees">
        <TabsList>
          <TabsTrigger value="attendees">Attendees ({meeting.attendees.length})</TabsTrigger>
          <TabsTrigger value="actions">Action items ({meeting.actionItems.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({meeting.documents.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="attendees" className="mt-4 space-y-3">
          <div className="flex justify-end">
            <Button variant="outline" size="sm" onClick={openAddAttendee}>
              <Plus className="mr-2 h-4 w-4" /> Record attendee
            </Button>
          </div>
          {meeting.attendees.length === 0 ? (
            <EmptyState
              title="No attendees recorded"
              description="Record who was present — each employee once."
              icon={Users}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Attended</TableHead>
                      <TableHead>Signed</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {meeting.attendees.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="font-medium">{a.employeeName}</TableCell>
                        <TableCell>
                          {a.attended ? (
                            <Badge variant="default">Present</Badge>
                          ) : (
                            <Badge variant="secondary">Apologies</Badge>
                          )}
                        </TableCell>
                        <TableCell>{fmtDate(a.signedDate)}</TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8 text-red-600"
                            onClick={() => setPendingRemove({ kind: 'attendee', row: a })}
                          >
                            <MoreHorizontal className="h-4 w-4" />
                            <span className="sr-only">Remove</span>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="actions" className="mt-4 space-y-3">
          <div className="flex justify-end">
            <Button variant="outline" size="sm" onClick={openAddAction}>
              <Plus className="mr-2 h-4 w-4" /> Raise action
            </Button>
          </div>
          {meeting.actionItems.length === 0 ? (
            <EmptyState
              title="No action items"
              description="Actions raised here appear in the open/overdue queues until completed."
              icon={CheckSquare}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Action</TableHead>
                      <TableHead>Priority</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Assigned to</TableHead>
                      <TableHead>Due</TableHead>
                      <TableHead>Completed</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {meeting.actionItems.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell className="max-w-[320px] truncate" title={a.actionDescription}>
                          {a.actionDescription}
                        </TableCell>
                        <TableCell>
                          <Badge variant={priorityVariant(a.priority)}>{a.priorityName}</Badge>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={a.statusName} />
                        </TableCell>
                        <TableCell>{a.assignedToName ?? 'Unassigned'}</TableCell>
                        <TableCell>{fmtDate(a.dueDate)}</TableCell>
                        <TableCell>{fmtDate(a.completionDate)}</TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" className="h-8 w-8">
                                <MoreHorizontal className="h-4 w-4" />
                                <span className="sr-only">Actions</span>
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => openEditAction(a)}>
                                Update…
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                className="text-red-600"
                                onClick={() => setPendingRemove({ kind: 'action', row: a })}
                              >
                                Remove
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="documents" className="mt-4 space-y-3">
          <div className="flex justify-end">
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                documentForm.reset({ fileName: '', filePath: '', description: '' });
                setDocumentOpen(true);
              }}
            >
              <Plus className="mr-2 h-4 w-4" /> Attach document
            </Button>
          </div>
          {meeting.documents.length === 0 ? (
            <EmptyState
              title="No documents"
              description="Attach the signed minutes, sign-in sheet or presentations."
              icon={FileText}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>File</TableHead>
                      <TableHead>Description</TableHead>
                      <TableHead>Uploaded</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {meeting.documents.map((d) => (
                      <TableRow key={d.id}>
                        <TableCell className="font-medium">{d.fileName}</TableCell>
                        <TableCell>{d.description ?? '—'}</TableCell>
                        <TableCell>{fmtDate(d.uploadDate)}</TableCell>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8 text-red-600"
                            onClick={() => setPendingRemove({ kind: 'document', row: d })}
                          >
                            <MoreHorizontal className="h-4 w-4" />
                            <span className="sr-only">Remove</span>
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>

      {/* ── Debrief / edit dialog ── */}
      <Dialog open={debriefOpen} onOpenChange={(o) => !busy && setDebriefOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>Record minutes / edit meeting</DialogTitle>
            <DialogDescription>
              The meeting number is fixed. The reported headcount may include visitors — the
              recorded attendee list lives on its own tab.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitDebrief} className="space-y-4">
            <FieldRow>
              <DateField form={debriefForm} name="meetingDate" label="Date" required />
              <SelectField
                form={debriefForm}
                name="type"
                label="Type"
                required
                options={SHE_MEETING_TYPE_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <TextField form={debriefForm} name="startTime" label="Start (HH:mm)" placeholder="09:00" />
              <TextField form={debriefForm} name="endTime" label="End (HH:mm)" placeholder="10:30" />
            </FieldRow>
            <FieldRow>
              <TextField form={debriefForm} name="location" label="Location" required />
              <NumberField form={debriefForm} name="attendeesCount" label="Headcount (reported)" />
            </FieldRow>
            <TextareaField form={debriefForm} name="agenda" label="Agenda" rows={2} />
            <TextareaField form={debriefForm} name="minutes" label="Minutes" rows={5} />
            <TextareaField form={debriefForm} name="topicsDiscussed" label="Topics discussed" rows={2} />
            <TextareaField form={debriefForm} name="decisionsMade" label="Decisions made" rows={2} />
            <EmployeePickerField form={debriefForm} name="facilitatorId" label="Facilitator" />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDebriefOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Attendee dialog ── */}
      <Dialog open={attendeeOpen} onOpenChange={(o) => !busy && setAttendeeOpen(o)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Record attendee</DialogTitle>
            <DialogDescription>
              One row per employee — a repeat entry is refused.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitAttendee} className="space-y-4">
            <EmployeePickerField form={attendeeForm} name="employeeId" label="Employee" required />
            <SwitchField
              form={attendeeForm}
              name="attended"
              label="Attended"
              description="Off records an apology / no-show."
            />
            <DateField form={attendeeForm} name="signedDate" label="Signed date" />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setAttendeeOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Record
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Action item dialog ── */}
      <Dialog open={actionOpen} onOpenChange={(o) => !busy && setActionOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{editingAction ? 'Update action item' : 'Raise action item'}</DialogTitle>
            <DialogDescription>
              {editingAction
                ? 'Track the action to completion — set the status and completion date when done.'
                : 'New actions open in the Open queue; status is tracked from there.'}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitAction} className="space-y-4">
            <TextareaField
              form={actionForm}
              name="actionDescription"
              label="Action"
              required
              rows={2}
            />
            <FieldRow>
              <SelectField
                form={actionForm}
                name="priority"
                label="Priority"
                required
                options={SHE_ACTION_PRIORITY_OPTIONS}
              />
              {editingAction ? (
                <SelectField
                  form={actionForm}
                  name="status"
                  label="Status"
                  required
                  options={SHE_ACTION_STATUS_OPTIONS}
                />
              ) : (
                <div className="text-muted-foreground self-end pb-2 text-sm">Opens as “Open”.</div>
              )}
            </FieldRow>
            <FieldRow>
              <DateField form={actionForm} name="dueDate" label="Due date" />
              {editingAction && (
                <DateField form={actionForm} name="completionDate" label="Completion date" />
              )}
            </FieldRow>
            <EmployeePickerField form={actionForm} name="assignedToId" label="Assigned to" />
            {editingAction && (
              <TextareaField
                form={actionForm}
                name="completionNotes"
                label="Completion notes"
                rows={2}
              />
            )}
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setActionOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingAction ? 'Save' : 'Raise action'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Document dialog ── */}
      <Dialog open={documentOpen} onOpenChange={(o) => !busy && setDocumentOpen(o)}>
        <DialogContent className="sm:max-w-[480px]">
          <DialogHeader>
            <DialogTitle>Attach document</DialogTitle>
            <DialogDescription>Reference a stored file by its path.</DialogDescription>
          </DialogHeader>
          <form onSubmit={submitDocument} className="space-y-4">
            <TextField form={documentForm} name="fileName" label="File name" required />
            <TextField form={documentForm} name="filePath" label="File path" required />
            <TextareaField form={documentForm} name="description" label="Description" rows={2} />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setDocumentOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Attach
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingRemove !== null}
        onOpenChange={(open) => !open && setPendingRemove(null)}
        title={
          pendingRemove?.kind === 'attendee'
            ? `Remove ${pendingRemove.row.employeeName}?`
            : pendingRemove?.kind === 'action'
              ? 'Remove this action item?'
              : `Remove ${pendingRemove?.kind === 'document' ? pendingRemove.row.fileName : ''}?`
        }
        description="This removes the row from the meeting record."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingRemove) return;
          try {
            if (pendingRemove.kind === 'attendee')
              await safetyCommitteeService.removeAttendee(pendingRemove.row.id);
            else if (pendingRemove.kind === 'action')
              await safetyCommitteeService.removeActionItem(pendingRemove.row.id);
            else await safetyCommitteeService.removeMeetingDocument(pendingRemove.row.id);
            await refresh();
            toast({ title: 'Removed' });
          } catch (error: any) {
            fail(error, 'Removing failed.');
          } finally {
            setPendingRemove(null);
          }
        }}
      />
    </div>
  );
}
