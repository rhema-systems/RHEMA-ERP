'use client';

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, Loader2, Mail, Pencil, Trash2, UserPlus, XCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { PanelMemberPicker, type PanelSelection } from '@/components/hr/recruitment/PanelMemberPicker';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { jobInterviewService } from '@/services/hr/interviews.service';
import {
  PANELIST_ROLES,
  type JobInterviewDetail,
  type JobInterviewPanelistRole,
} from '@/types/hr/interviews';

/**
 * The panel: who is sitting, whether they have confirmed, and whether they turned up.
 *
 * Confirmation is the panelist's own act — they click a link in their assignment email, or confirm
 * from their own diary. HR can confirm on someone's behalf (they take the phone call), but one
 * panelist cannot confirm for another, so no button here offers to.
 */
export function InterviewPanelPanel({
  interview,
  canManage,
}: {
  interview: JobInterviewDetail;
  canManage: boolean;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();
  const [addOpen, setAddOpen] = useState(false);
  const [newEmployees, setNewEmployees] = useState<PanelSelection[]>([]);
  const [newExternals, setNewExternals] = useState<PanelSelection[]>([]);
  const [newRole, setNewRole] = useState<JobInterviewPanelistRole>('Member');
  const [editing, setEditing] = useState<{
    id: string;
    external: boolean;
    name: string;
    role: JobInterviewPanelistRole;
    isRequired: boolean;
  } | null>(null);

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'interview', interview.id] });

  const addMembers = useMutation({
    mutationFn: async () => {
      for (const member of newEmployees) {
        await jobInterviewService.addPanelist(interview.id, {
          jobInterviewId: interview.id,
          employeeId: member.id,
          role: newRole,
          isRequired: newRole === 'Chair',
        });
      }
      for (const member of newExternals) {
        await jobInterviewService.addExternalPanelist(interview.id, {
          jobInterviewId: interview.id,
          associateId: member.id,
          role: newRole,
          isRequired: false,
        });
      }
    },
    onSuccess: () => {
      toast({ title: 'Panel updated' });
      setNewEmployees([]);
      setNewExternals([]);
      setAddOpen(false);
      invalidate();
    },
    onError: (error: any) =>
      toast({
        title: 'Could not add to the panel',
        description: error?.message ?? 'Someone already on the panel cannot be added twice.',
        variant: 'destructive',
      }),
  });

  const saveEdit = useMutation({
    mutationFn: () => {
      if (!editing) return Promise.resolve(undefined as unknown);
      const payload = { id: editing.id, role: editing.role, isRequired: editing.isRequired };
      return editing.external
        ? jobInterviewService.updateExternalPanelist(editing.id, payload)
        : jobInterviewService.updatePanelist(editing.id, payload);
    },
    onSuccess: () => {
      toast({ title: 'Panelist updated' });
      setEditing(null);
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not update the panelist', description: error?.message, variant: 'destructive' }),
  });

  const removeInternal = useMutation({
    mutationFn: (panelistId: string) => jobInterviewService.removePanelist(panelistId),
    onSuccess: () => {
      toast({ title: 'Removed from the panel' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not remove', description: error?.message, variant: 'destructive' }),
  });

  const removeExternal = useMutation({
    mutationFn: (id: string) => jobInterviewService.removeExternalPanelist(id),
    onSuccess: () => {
      toast({ title: 'Removed from the panel' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not remove', description: error?.message, variant: 'destructive' }),
  });

  const attendance = useMutation({
    mutationFn: ({ id, attended, external }: { id: string; attended: boolean; external: boolean }) =>
      external
        ? jobInterviewService.recordExternalPanelistAttendance(id, attended, attended ? null : 'Did not attend')
        : jobInterviewService.recordPanelistAttendance(id, attended, attended ? null : 'Did not attend'),
    onSuccess: () => {
      toast({ title: 'Attendance recorded' });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not record attendance', description: error?.message, variant: 'destructive' }),
  });

  const notify = useMutation({
    mutationFn: () =>
      // null broadcasts to the whole group — [] would skip it. The distinction is load-bearing.
      jobInterviewService.sendPanelistNotifications(interview.id, {
        employeeIds: null,
        externalAssociateIds: null,
      }),
    onSuccess: (result) => {
      toast({
        title: `Assignment emails sent to ${result.sent} of ${result.totalRequested}`,
        description: result.skipped > 0 ? `${result.skipped} could not be sent.` : undefined,
      });
      invalidate();
    },
    onError: (error: any) =>
      toast({ title: 'Could not notify the panel', description: error?.message, variant: 'destructive' }),
  });

  const hasPanel = interview.panelists.length > 0 || interview.externalPanelists.length > 0;

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
        <div>
          <CardTitle className="text-base">Panel</CardTitle>
          <CardDescription>
            Panel members can open this interview and file their own scorecard. Nobody else can.
          </CardDescription>
        </div>
        {canManage && (
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={!hasPanel || notify.isPending} onClick={() => notify.mutate()}>
              {notify.isPending ? (
                <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />
              ) : (
                <Mail className="mr-1.5 h-4 w-4" />
              )}
              Send assignments
            </Button>
            <Button size="sm" onClick={() => setAddOpen(true)}>
              <UserPlus className="mr-1.5 h-4 w-4" />
              Add
            </Button>
          </div>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {!hasPanel ? (
          <div className="py-10">
            <EmptyState
              icon={UserPlus}
              title="No panel yet"
              description="Nobody can score this interview until someone is assigned to it."
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Member</TableHead>
                <TableHead className="w-[150px]">Role</TableHead>
                <TableHead className="w-[160px]">Confirmed</TableHead>
                <TableHead className="w-[150px]">Attended</TableHead>
                {canManage && <TableHead className="w-[60px]" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {interview.panelists.map((member) => (
                <TableRow key={member.id}>
                  <TableCell>
                    <div className="font-medium">{member.employeeName}</div>
                    <div className="text-xs text-muted-foreground">
                      {member.employeePositionTitle ?? 'Internal'}
                    </div>
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={member.role} />
                  </TableCell>
                  <TableCell>
                    {member.isConfirmed ? (
                      <span className="flex items-center gap-1.5 text-sm">
                        <CheckCircle2 className="h-4 w-4 text-green-600" />
                        {formatDateTime(member.confirmationDate)}
                      </span>
                    ) : member.invitationSentDate ? (
                      <span className="text-sm text-muted-foreground">Awaiting reply</span>
                    ) : (
                      <span className="text-sm text-muted-foreground">Not invited</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {member.attended === null || member.attended === undefined ? (
                      <div className="flex gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label="Mark attended"
                          onClick={() => attendance.mutate({ id: member.id, attended: true, external: false })}
                        >
                          <CheckCircle2 className="h-4 w-4 text-green-600" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label="Mark absent"
                          onClick={() => attendance.mutate({ id: member.id, attended: false, external: false })}
                        >
                          <XCircle className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    ) : (
                      <StatusBadge status={member.attended ? 'Attended' : 'NoShow'} />
                    )}
                  </TableCell>
                  {canManage && (
                    <TableCell>
                      <div className="flex gap-0.5">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Edit ${member.employeeName}'s role`}
                          onClick={() =>
                            setEditing({
                              id: member.id,
                              external: false,
                              name: member.employeeName,
                              role: member.role,
                              isRequired: member.isRequired,
                            })
                          }
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Remove ${member.employeeName}`}
                          onClick={() => removeInternal.mutate(member.id)}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </TableCell>
                  )}
                </TableRow>
              ))}

              {interview.externalPanelists.map((member) => (
                <TableRow key={member.id}>
                  <TableCell>
                    <div className="font-medium">{member.associateName}</div>
                    <div className="text-xs text-muted-foreground">
                      {member.associateOrganization ?? 'External'} · no ERP login
                    </div>
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={member.role} />
                  </TableCell>
                  <TableCell>
                    {member.isConfirmed ? (
                      <span className="flex items-center gap-1.5 text-sm">
                        <CheckCircle2 className="h-4 w-4 text-green-600" />
                        {formatDateTime(member.confirmationDate)}
                      </span>
                    ) : member.invitationSentDate ? (
                      <span className="text-sm text-muted-foreground">Awaiting reply</span>
                    ) : (
                      <span className="text-sm text-muted-foreground">Not invited</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {member.attended === null || member.attended === undefined ? (
                      <div className="flex gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label="Mark attended"
                          onClick={() => attendance.mutate({ id: member.id, attended: true, external: true })}
                        >
                          <CheckCircle2 className="h-4 w-4 text-green-600" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label="Mark absent"
                          onClick={() => attendance.mutate({ id: member.id, attended: false, external: true })}
                        >
                          <XCircle className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    ) : (
                      <StatusBadge status={member.attended ? 'Attended' : 'NoShow'} />
                    )}
                  </TableCell>
                  {canManage && (
                    <TableCell>
                      <div className="flex gap-0.5">
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Edit ${member.associateName}'s role`}
                          onClick={() =>
                            setEditing({
                              id: member.id,
                              external: true,
                              name: member.associateName,
                              role: member.role,
                              isRequired: member.isRequired,
                            })
                          }
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="icon"
                          aria-label={`Remove ${member.associateName}`}
                          onClick={() => removeExternal.mutate(member.id)}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      {/* edit a panelist's role — one dialog for internal and external rows */}
      <Dialog open={!!editing} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent className="sm:max-w-sm">
          <DialogHeader>
            <DialogTitle>Edit panelist</DialogTitle>
            <DialogDescription>{editing?.name}</DialogDescription>
          </DialogHeader>
          {editing && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Role on the panel</Label>
                <Select
                  value={editing.role}
                  onValueChange={(v) =>
                    setEditing((e) => (e ? { ...e, role: v as JobInterviewPanelistRole } : e))
                  }
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {PANELIST_ROLES.map((role) => (
                      <SelectItem key={role} value={role}>
                        {humanizeEnum(role)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={editing.isRequired}
                  onCheckedChange={(v) =>
                    setEditing((e) => (e ? { ...e, isRequired: v === true } : e))
                  }
                />
                Attendance required for the interview to proceed
              </label>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>
              Cancel
            </Button>
            <Button disabled={saveEdit.isPending} onClick={() => saveEdit.mutate()}>
              {saveEdit.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={addOpen} onOpenChange={setAddOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Add to the panel</DialogTitle>
            <DialogDescription>
              Everyone added here gains access to this interview and its candidates.
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Role on the panel</Label>
              <Select value={newRole} onValueChange={(v) => setNewRole(v as JobInterviewPanelistRole)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PANELIST_ROLES.map((role) => (
                    <SelectItem key={role} value={role}>
                      {humanizeEnum(role)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <PanelMemberPicker
              employees={newEmployees}
              externals={newExternals}
              onEmployeesChange={setNewEmployees}
              onExternalsChange={setNewExternals}
            />
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setAddOpen(false)}>
              Cancel
            </Button>
            <Button
              disabled={addMembers.isPending || (newEmployees.length === 0 && newExternals.length === 0)}
              onClick={() => addMembers.mutate()}
            >
              {addMembers.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Add to panel
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
