'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { CalendarDays, Loader2, MoreHorizontal, Plus, Users } from 'lucide-react';
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
  TextareaField,
  SelectField,
  DateField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyCommitteeService } from '@/services/hr/safety-committee.service';
import { SHE_MEETING_TYPE_OPTIONS } from '@/types/hr/safety-governance';
import type {
  SafetyCommitteeMember,
  SafetyMeetingSummary,
  SheSafetyMeetingType,
} from '@/types/hr/safety-governance';

/**
 * Committee detail: the roster (a rejoin is a NEW membership row — a second active row for
 * the same person is refused) and the committee's meetings. Meeting numbers are user-entered
 * and unique; minutes are recorded on the meeting page after it happens.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const memberSchema = z.object({
  employeeId: z.string().min(1, 'An employee is required'),
  role: z.string().min(1, 'A role is required').max(100),
  joinDate: z.string().min(1, 'A join date is required'),
  endDate: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});
type MemberForm = z.input<typeof memberSchema>;

const meetingSchema = z.object({
  meetingNumber: z.string().min(1, 'A meeting number is required').max(30),
  meetingDate: z.string().min(1, 'A date is required'),
  startTime: z.string().optional().or(z.literal('')),
  endTime: z.string().optional().or(z.literal('')),
  location: z.string().min(1, 'A location is required').max(200),
  type: z.string().min(1),
  agenda: z.string().max(3000).optional().or(z.literal('')),
  facilitatorId: z.string().optional().or(z.literal('')),
});
type MeetingForm = z.input<typeof meetingSchema>;

const toTimeSpan = (v?: string) => (v && v.length > 0 ? `${v}:00` : null);

export default function SafetyCommitteeDetailPage() {
  const params = useParams<{ id: string }>();
  const committeeId = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [memberDialogOpen, setMemberDialogOpen] = useState(false);
  const [editingMember, setEditingMember] = useState<SafetyCommitteeMember | null>(null);
  const [pendingMemberRemove, setPendingMemberRemove] = useState<SafetyCommitteeMember | null>(null);
  const [meetingDialogOpen, setMeetingDialogOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: committee, isLoading } = useQuery({
    queryKey: ['hr', 'safety-committees', committeeId],
    queryFn: () => safetyCommitteeService.getCommittee(committeeId),
    enabled: !!committeeId,
  });
  const { data: meetings = [] } = useQuery({
    queryKey: ['hr', 'safety-committees', committeeId, 'meetings'],
    queryFn: () => safetyCommitteeService.getMeetingsByCommittee(committeeId),
    enabled: !!committeeId,
  });

  const memberForm = useForm<MemberForm>({ resolver: zodResolver(memberSchema) });
  const meetingForm = useForm<MeetingForm>({ resolver: zodResolver(meetingSchema) });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-committees'] });

  const openAddMember = () => {
    setEditingMember(null);
    memberForm.reset({
      employeeId: '',
      role: 'Member',
      joinDate: new Date().toISOString().slice(0, 10),
      endDate: '',
      isActive: true,
    });
    setMemberDialogOpen(true);
  };

  const openEditMember = (m: SafetyCommitteeMember) => {
    setEditingMember(m);
    memberForm.reset({
      employeeId: m.employeeId,
      role: m.role,
      joinDate: m.joinDate.slice(0, 10),
      endDate: m.endDate ? m.endDate.slice(0, 10) : '',
      isActive: m.isActive,
    });
    setMemberDialogOpen(true);
  };

  const submitMember = memberForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = memberSchema.parse(values);
      if (editingMember) {
        await safetyCommitteeService.updateMember(editingMember.id, {
          id: editingMember.id,
          role: v.role,
          endDate: v.endDate ? new Date(v.endDate).toISOString() : null,
          isActive: v.isActive,
        });
      } else {
        await safetyCommitteeService.addMember(committeeId, {
          committeeId,
          employeeId: v.employeeId,
          role: v.role,
          joinDate: new Date(v.joinDate).toISOString(),
          endDate: v.endDate ? new Date(v.endDate).toISOString() : null,
          isActive: v.isActive,
        });
      }
      await refresh();
      toast({ title: editingMember ? 'Membership updated' : 'Member added' });
      setMemberDialogOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Saving the membership failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  const openRecordMeeting = () => {
    meetingForm.reset({
      meetingNumber: '',
      meetingDate: new Date().toISOString().slice(0, 10),
      startTime: '',
      endTime: '',
      location: '',
      type: 'CommitteeMeeting',
      agenda: '',
      facilitatorId: '',
    });
    setMeetingDialogOpen(true);
  };

  const submitMeeting = meetingForm.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = meetingSchema.parse(values);
      await safetyCommitteeService.createMeeting({
        committeeId,
        meetingNumber: v.meetingNumber,
        meetingDate: new Date(v.meetingDate).toISOString(),
        startTime: toTimeSpan(v.startTime),
        endTime: toTimeSpan(v.endTime),
        location: v.location,
        type: v.type as SheSafetyMeetingType,
        agenda: blank(v.agenda),
        facilitatorId: blank(v.facilitatorId),
      });
      await refresh();
      toast({
        title: 'Meeting recorded',
        description: 'Open it to record attendees, minutes and action items.',
      });
      setMeetingDialogOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Recording the meeting failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  if (isLoading || !committee) {
    return (
      <div className="text-muted-foreground flex items-center gap-2 p-6 text-sm">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading committee…
      </div>
    );
  }

  const activeMembers = committee.members.filter((m) => m.isActive);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={committee.committeeName}
        description={committee.description ?? 'Safety committee'}
        backHref="/hr/safety/committees"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={openAddMember}>
              <Plus className="mr-2 h-4 w-4" /> Add member
            </Button>
            <Button onClick={openRecordMeeting}>
              <Plus className="mr-2 h-4 w-4" /> Record meeting
            </Button>
          </div>
        }
      />

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Overview</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <div className="text-muted-foreground">Chairperson</div>
            <div className="font-medium">{committee.chairPersonName ?? '—'}</div>
          </div>
          <div>
            <div className="text-muted-foreground">Established</div>
            <div className="font-medium">{fmtDate(committee.establishedDate)}</div>
          </div>
          <div>
            <div className="text-muted-foreground">Meets</div>
            <div className="font-medium">
              {committee.meetingSchedule ??
                (committee.meetingFrequencyDays
                  ? `Every ${committee.meetingFrequencyDays} days`
                  : '—')}
            </div>
          </div>
          <div>
            <div className="text-muted-foreground">Status</div>
            <StatusBadge status={committee.isActive ? 'Active' : 'Inactive'} />
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="members">
        <TabsList>
          <TabsTrigger value="members">
            Members ({activeMembers.length} active / {committee.members.length})
          </TabsTrigger>
          <TabsTrigger value="meetings">Meetings ({meetings.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="members" className="mt-4">
          {committee.members.length === 0 ? (
            <EmptyState
              title="No members yet"
              description="Add the chair, secretary and representatives to build the roster."
              icon={Users}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Member</TableHead>
                      <TableHead>Role</TableHead>
                      <TableHead>Joined</TableHead>
                      <TableHead>Ended</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {committee.members.map((m) => (
                      <TableRow key={m.id}>
                        <TableCell className="font-medium">{m.employeeName}</TableCell>
                        <TableCell>{m.role}</TableCell>
                        <TableCell>{fmtDate(m.joinDate)}</TableCell>
                        <TableCell>{fmtDate(m.endDate)}</TableCell>
                        <TableCell>
                          <StatusBadge status={m.isActive ? 'Active' : 'Ended'} />
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="icon" className="h-8 w-8">
                                <MoreHorizontal className="h-4 w-4" />
                                <span className="sr-only">Actions</span>
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onClick={() => openEditMember(m)}>
                                Edit…
                              </DropdownMenuItem>
                              <DropdownMenuItem
                                className="text-red-600"
                                onClick={() => setPendingMemberRemove(m)}
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

        <TabsContent value="meetings" className="mt-4">
          {meetings.length === 0 ? (
            <EmptyState
              title="No meetings recorded"
              description="Record a meeting to start the minutes and action trail."
              icon={CalendarDays}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Meeting</TableHead>
                      <TableHead>Date</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Location</TableHead>
                      <TableHead>Attendance</TableHead>
                      <TableHead>Open actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {meetings.map((m: SafetyMeetingSummary) => (
                      <TableRow key={m.id}>
                        <TableCell>
                          <Link
                            href={`/hr/safety/committees/meetings/${m.id}`}
                            className="font-mono text-primary hover:underline"
                          >
                            {m.meetingNumber}
                          </Link>
                        </TableCell>
                        <TableCell>{fmtDate(m.meetingDate)}</TableCell>
                        <TableCell>{m.typeName}</TableCell>
                        <TableCell>{m.location}</TableCell>
                        <TableCell>{m.attendeesCount ?? '—'}</TableCell>
                        <TableCell>
                          {m.openActionItemCount > 0 ? (
                            <Badge variant="default">{m.openActionItemCount}</Badge>
                          ) : (
                            <span className="text-muted-foreground text-sm">None</span>
                          )}
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

      {/* ── Member dialog ── */}
      <Dialog open={memberDialogOpen} onOpenChange={(o) => !busy && setMemberDialogOpen(o)}>
        <DialogContent className="sm:max-w-[520px]">
          <DialogHeader>
            <DialogTitle>{editingMember ? 'Edit membership' : 'Add member'}</DialogTitle>
            <DialogDescription>
              {editingMember
                ? `${editingMember.employeeName} — the member is fixed; adjust the role or tenure.`
                : 'An employee can hold only one active membership per committee; a rejoin after an ended one is a new row.'}
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitMember} className="space-y-4">
            {!editingMember && (
              <EmployeePickerField form={memberForm} name="employeeId" label="Employee" required />
            )}
            <TextField
              form={memberForm}
              name="role"
              label="Role"
              required
              placeholder="e.g. Secretary, Departmental rep"
            />
            <FieldRow>
              {!editingMember ? (
                <DateField form={memberForm} name="joinDate" label="Join date" required />
              ) : (
                <div className="text-muted-foreground self-end pb-2 text-sm">
                  Joined {fmtDate(editingMember.joinDate)}
                </div>
              )}
              <DateField form={memberForm} name="endDate" label="End date" />
            </FieldRow>
            <SwitchField
              form={memberForm}
              name="isActive"
              label="Active"
              description="Turn off (with an end date) when the member leaves the committee."
            />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setMemberDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingMember ? 'Save' : 'Add member'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Record meeting dialog ── */}
      <Dialog open={meetingDialogOpen} onOpenChange={(o) => !busy && setMeetingDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Record meeting</DialogTitle>
            <DialogDescription>
              Minutes, attendees and action items are recorded on the meeting page afterwards.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitMeeting} className="space-y-4">
            <FieldRow>
              <TextField
                form={meetingForm}
                name="meetingNumber"
                label="Meeting number (unique)"
                required
                placeholder="e.g. HSE-CM-2026-08"
              />
              <DateField form={meetingForm} name="meetingDate" label="Date" required />
            </FieldRow>
            <FieldRow>
              <TextField form={meetingForm} name="startTime" label="Start (HH:mm)" placeholder="09:00" />
              <TextField form={meetingForm} name="endTime" label="End (HH:mm)" placeholder="10:30" />
            </FieldRow>
            <FieldRow>
              <TextField form={meetingForm} name="location" label="Location" required />
              <SelectField
                form={meetingForm}
                name="type"
                label="Type"
                required
                options={SHE_MEETING_TYPE_OPTIONS}
              />
            </FieldRow>
            <TextareaField form={meetingForm} name="agenda" label="Agenda" rows={3} />
            <EmployeePickerField form={meetingForm} name="facilitatorId" label="Facilitator" />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setMeetingDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Record meeting
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingMemberRemove !== null}
        onOpenChange={(open) => !open && setPendingMemberRemove(null)}
        title={`Remove ${pendingMemberRemove?.employeeName}?`}
        description="This removes the membership row. For a member leaving normally, prefer an end date with Active off — that keeps the tenure on record."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingMemberRemove) return;
          try {
            await safetyCommitteeService.removeMember(pendingMemberRemove.id);
            await refresh();
            toast({ title: 'Member removed', description: pendingMemberRemove.employeeName });
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Removing failed.',
              variant: 'destructive',
            });
          } finally {
            setPendingMemberRemove(null);
          }
        }}
      />
    </div>
  );
}
