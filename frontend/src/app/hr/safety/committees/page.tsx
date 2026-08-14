'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, MoreHorizontal, Plus, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
  DateField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyCommitteeService } from '@/services/hr/safety-committee.service';
import type { SafetyCommittee, SafetyMeetingActionItem } from '@/types/hr/safety-governance';

/**
 * Safety committees register: active committees plus the meeting action-item tracker
 * (open and overdue queues across all meetings). Committee detail carries members and
 * meetings; meeting detail carries attendees, action items and documents.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const committeeSchema = z.object({
  committeeName: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(500).optional().or(z.literal('')),
  establishedDate: z.string().min(1, 'An established date is required'),
  chairPersonId: z.string().optional().or(z.literal('')),
  meetingFrequencyDays: z.coerce.number().int().positive().optional().or(z.literal('')),
  meetingSchedule: z.string().max(200).optional().or(z.literal('')),
  isActive: z.boolean(),
});
type CommitteeForm = z.input<typeof committeeSchema>;

const priorityVariant = (p: SafetyMeetingActionItem['priority']) =>
  p === 'Critical' ? 'destructive' : p === 'High' ? 'default' : 'secondary';

function ActionItemsTable({ items, overdue }: { items: SafetyMeetingActionItem[]; overdue?: boolean }) {
  if (items.length === 0) {
    return (
      <EmptyState
        title={overdue ? 'Nothing overdue' : 'No open action items'}
        description={
          overdue
            ? 'Every meeting action is inside its due date.'
            : 'Actions raised in safety meetings appear here until completed.'
        }
        icon={Users}
      />
    );
  }
  return (
    <Card>
      <CardContent className="p-0">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Meeting</TableHead>
              <TableHead>Action</TableHead>
              <TableHead>Priority</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Assigned to</TableHead>
              <TableHead>Due</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((a) => (
              <TableRow key={a.id}>
                <TableCell>
                  <Link
                    href={`/hr/safety/committees/meetings/${a.meetingId}`}
                    className="font-mono text-primary hover:underline"
                  >
                    {a.meetingNumber ?? 'Meeting'}
                  </Link>
                </TableCell>
                <TableCell className="max-w-[360px] truncate" title={a.actionDescription}>
                  {a.actionDescription}
                </TableCell>
                <TableCell>
                  <Badge variant={priorityVariant(a.priority)}>{a.priorityName}</Badge>
                </TableCell>
                <TableCell>
                  <StatusBadge status={a.statusName} />
                </TableCell>
                <TableCell>{a.assignedToName ?? 'Unassigned'}</TableCell>
                <TableCell className={overdue ? 'text-destructive font-medium' : undefined}>
                  {fmtDate(a.dueDate)}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </CardContent>
    </Card>
  );
}

export default function SafetyCommitteesPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = useState<SafetyCommittee | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: committees = [], isLoading } = useQuery({
    queryKey: ['hr', 'safety-committees', 'active'],
    queryFn: () => safetyCommitteeService.getActiveCommittees(),
  });
  const { data: openItems = [] } = useQuery({
    queryKey: ['hr', 'safety-committees', 'action-items', 'open'],
    queryFn: () => safetyCommitteeService.getOpenActionItems(),
  });
  const { data: overdueItems = [] } = useQuery({
    queryKey: ['hr', 'safety-committees', 'action-items', 'overdue'],
    queryFn: () => safetyCommitteeService.getOverdueActionItems(),
  });

  const form = useForm<CommitteeForm>({ resolver: zodResolver(committeeSchema) });

  const openCreate = () => {
    setEditingId(null);
    form.reset({
      committeeName: '',
      description: '',
      establishedDate: new Date().toISOString().slice(0, 10),
      chairPersonId: '',
      meetingFrequencyDays: '',
      meetingSchedule: '',
      isActive: true,
    });
    setDialogOpen(true);
  };

  const openEdit = (c: SafetyCommittee) => {
    setEditingId(c.id);
    form.reset({
      committeeName: c.committeeName,
      description: c.description ?? '',
      establishedDate: c.establishedDate.slice(0, 10),
      chairPersonId: c.chairPersonId ?? '',
      meetingFrequencyDays: c.meetingFrequencyDays ?? '',
      meetingSchedule: c.meetingSchedule ?? '',
      isActive: c.isActive,
    });
    setDialogOpen(true);
  };

  const submit = form.handleSubmit(async (values) => {
    setBusy(true);
    try {
      const v = committeeSchema.parse(values);
      const payload = {
        committeeName: v.committeeName,
        description: blank(v.description),
        establishedDate: new Date(v.establishedDate).toISOString(),
        chairPersonId: blank(v.chairPersonId),
        meetingFrequencyDays: typeof v.meetingFrequencyDays === 'number' ? v.meetingFrequencyDays : null,
        meetingSchedule: blank(v.meetingSchedule),
        isActive: v.isActive,
      };
      if (editingId) {
        await safetyCommitteeService.updateCommittee(editingId, { id: editingId, ...payload });
      } else {
        await safetyCommitteeService.createCommittee(payload);
      }
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-committees'] });
      toast({ title: editingId ? 'Committee updated' : 'Committee created' });
      setDialogOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Saving the committee failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Safety Committees"
        description="Committees with their members and meetings, plus the action-item queues raised in those meetings. Overdue action-item chasing is manual — the reminder engine does not cover corrective actions (that is the unified CA tracker, next slice)."
        backHref="/hr/safety"
        actions={
          <Button onClick={openCreate}>
            <Plus className="mr-2 h-4 w-4" /> New committee
          </Button>
        }
      />

      <Tabs defaultValue="committees">
        <TabsList>
          <TabsTrigger value="committees">Committees ({committees.length})</TabsTrigger>
          <TabsTrigger value="open">Open actions ({openItems.length})</TabsTrigger>
          <TabsTrigger value="overdue">Overdue actions ({overdueItems.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="committees" className="mt-4">
          {isLoading ? null : committees.length === 0 ? (
            <EmptyState
              title="No active committees"
              description="Establish a committee to start recording its meetings and actions."
              icon={Users}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Committee</TableHead>
                      <TableHead>Chairperson</TableHead>
                      <TableHead>Established</TableHead>
                      <TableHead>Meets</TableHead>
                      <TableHead>Members</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="w-[60px]" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {committees.map((c) => (
                      <TableRow key={c.id}>
                        <TableCell>
                          <Link
                            href={`/hr/safety/committees/${c.id}`}
                            className="font-medium text-primary hover:underline"
                          >
                            {c.committeeName}
                          </Link>
                        </TableCell>
                        <TableCell>{c.chairPersonName ?? '—'}</TableCell>
                        <TableCell>{fmtDate(c.establishedDate)}</TableCell>
                        <TableCell>
                          {c.meetingSchedule ??
                            (c.meetingFrequencyDays ? `Every ${c.meetingFrequencyDays} days` : '—')}
                        </TableCell>
                        <TableCell>{c.members.filter((m) => m.isActive).length}</TableCell>
                        <TableCell>
                          <StatusBadge status={c.isActive ? 'Active' : 'Inactive'} />
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
                              <DropdownMenuItem onClick={() => openEdit(c)}>Edit…</DropdownMenuItem>
                              <DropdownMenuItem
                                className="text-red-600"
                                onClick={() => setPendingDelete(c)}
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

        <TabsContent value="open" className="mt-4">
          <ActionItemsTable items={openItems} />
        </TabsContent>
        <TabsContent value="overdue" className="mt-4">
          <ActionItemsTable items={overdueItems} overdue />
        </TabsContent>
      </Tabs>

      <Dialog open={dialogOpen} onOpenChange={(o) => !busy && setDialogOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit committee' : 'New committee'}</DialogTitle>
            <DialogDescription>
              Members are managed on the committee page after creation.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submit} className="space-y-4">
            <TextField form={form} name="committeeName" label="Committee name" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <DateField form={form} name="establishedDate" label="Established" required />
              <NumberField form={form} name="meetingFrequencyDays" label="Meeting frequency (days)" />
            </FieldRow>
            <TextField
              form={form}
              name="meetingSchedule"
              label="Meeting schedule"
              placeholder="e.g. First Tuesday of the month, 09:00"
            />
            <EmployeePickerField form={form} name="chairPersonId" label="Chairperson" />
            <SwitchField form={form} name="isActive" label="Active" />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setDialogOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingId ? 'Save' : 'Create committee'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => !open && setPendingDelete(null)}
        title={`Remove ${pendingDelete?.committeeName}?`}
        description="This removes the committee from the register. Its meetings remain reachable from the action queues."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          if (!pendingDelete) return;
          try {
            await safetyCommitteeService.removeCommittee(pendingDelete.id);
            await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-committees'] });
            toast({ title: 'Removed', description: pendingDelete.committeeName });
          } catch (error: any) {
            toast({
              title: 'Error',
              description: error?.message || 'Removing failed.',
              variant: 'destructive',
            });
          } finally {
            setPendingDelete(null);
          }
        }}
      />
    </div>
  );
}
