'use client';

import { useEffect, useState } from 'react';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { teamService } from '@/services/hr/team.service';
import {
  TEAM_MEMBER_ROLES,
  teamMemberRoleLabel,
  type TeamMember,
  type TeamMemberRole,
} from '@/types/hr/team';

interface EditTeamMemberDialogProps {
  teamId: string;
  teamName: string;
  member: TeamMember | null;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<unknown> | void;
}

/**
 * Edits a membership: the role, the share of time, the primary flag, the dates, the notes.
 *
 * ⚠ This dialog exists because slice 12's parity sweep found `PUT teams/{id}/members/{memberId}`
 * wrapped and called by nothing — the roster RENDERED role, allocation and the primary flag and
 * offered no way to change any of them. It is also the only writer of a genuine role move, so
 * without it the membership-history tab could never show one: joining and leaving both record a row
 * whose previous and new role are the same, and the "Member → Coordinator" line the tab is built to
 * render was unreachable. A whole surface whose content no act in the product could produce.
 *
 * ⚠ The reason box appears only when the role actually moves, because that is the only case the
 * server records. Asking for a reason it will discard would teach people the field does nothing.
 */
export function EditTeamMemberDialog({
  teamId,
  teamName,
  member,
  onOpenChange,
  onSaved,
}: EditTeamMemberDialogProps) {
  const { toast } = useToast();
  const [role, setRole] = useState<TeamMemberRole>('Member');
  const [allocation, setAllocation] = useState('100');
  const [isPrimary, setIsPrimary] = useState(false);
  const [joinDate, setJoinDate] = useState('');
  const [leaveDate, setLeaveDate] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [notes, setNotes] = useState('');
  const [changeReason, setChangeReason] = useState('');
  const [saving, setSaving] = useState(false);

  // Load the row's own values whenever a different member is opened. Editing a form seeded from
  // the previous subject is the classic dialog defect, and `member.id` in the dependency list is
  // what stops it.
  useEffect(() => {
    if (!member) return;
    setRole(member.role);
    setAllocation(String(member.allocationPercent));
    setIsPrimary(member.isPrimary);
    setJoinDate(member.joinDate?.slice(0, 10) ?? '');
    setLeaveDate(member.leaveDate?.slice(0, 10) ?? '');
    setIsActive(member.isActive);
    setNotes(member.notes ?? '');
    setChangeReason('');
  }, [member?.id]);

  const allocationNumber = Number(allocation);
  const allocationValid =
    allocation.trim() !== '' && Number.isFinite(allocationNumber) &&
    allocationNumber >= 0 && allocationNumber <= 100;
  const roleMoved = !!member && role !== member.role;
  // The server refuses a leaving date before the joining date; say so before it does.
  const datesValid = !leaveDate || !joinDate || leaveDate >= joinDate;

  const submit = async () => {
    if (!member || !allocationValid || !datesValid) return;
    setSaving(true);
    try {
      await teamService.updateMember(teamId, member.id, {
        role,
        allocationPercent: allocationNumber,
        isPrimary,
        joinDate,
        leaveDate: leaveDate === '' ? null : leaveDate,
        isActive,
        notes: notes.trim() === '' ? null : notes.trim(),
        changeReason: roleMoved && changeReason.trim() !== '' ? changeReason.trim() : null,
      });
      await onSaved();
      toast({
        title: 'Membership updated',
        description: roleMoved
          ? `${member.employeeName ?? 'This member'} is now ${teamMemberRoleLabel(role)} on ${teamName}.`
          : `${member.employeeName ?? 'This member'}'s membership was updated.`,
      });
      onOpenChange(false);
    } catch (error) {
      toast({
        title: 'Could not update this membership',
        description: (error as Error)?.message || 'Failed to update the member.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={member !== null} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Edit {member?.employeeName ?? 'membership'}</DialogTitle>
          <DialogDescription>
            {member?.employeeNumber ? `${member.employeeNumber} · ` : ''}
            {teamName}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label>Role</Label>
              <Select value={role} onValueChange={(v) => setRole(v as TeamMemberRole)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TEAM_MEMBER_ROLES.map((r) => (
                    <SelectItem key={r.value} value={r.value}>
                      {r.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="editAllocation">Allocation (%)</Label>
              <Input
                id="editAllocation"
                type="number"
                min={0}
                max={100}
                value={allocation}
                onChange={(e) => setAllocation(e.target.value)}
              />
              {!allocationValid && allocation.trim() !== '' && (
                <p className="text-destructive text-sm">A whole percentage between 0 and 100.</p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="editJoinDate">Joined</Label>
              <Input
                id="editJoinDate"
                type="date"
                value={joinDate}
                onChange={(e) => setJoinDate(e.target.value)}
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="editLeaveDate">Left</Label>
              <Input
                id="editLeaveDate"
                type="date"
                value={leaveDate}
                onChange={(e) => setLeaveDate(e.target.value)}
              />
              {!datesValid && (
                <p className="text-destructive text-sm">
                  A leaving date cannot fall before the joining date.
                </p>
              )}
            </div>
          </div>

          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label htmlFor="editIsPrimary">Primary team</Label>
              <p className="text-muted-foreground text-xs">
                Setting this clears the flag on their other teams.
              </p>
            </div>
            <Switch id="editIsPrimary" checked={isPrimary} onCheckedChange={setIsPrimary} />
          </div>

          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label htmlFor="editIsActive">Active membership</Label>
              <p className="text-muted-foreground text-xs">
                Turning this off takes them off the current roster without erasing the record.
              </p>
            </div>
            <Switch id="editIsActive" checked={isActive} onCheckedChange={setIsActive} />
          </div>

          {roleMoved && (
            <div className="space-y-2">
              <Label htmlFor="editChangeReason">
                Why the role changed{member ? ` (${teamMemberRoleLabel(member.role)} → ${teamMemberRoleLabel(role)})` : ''}
              </Label>
              <Textarea
                id="editChangeReason"
                rows={2}
                value={changeReason}
                onChange={(e) => setChangeReason(e.target.value)}
                placeholder="Recorded on the membership history…"
              />
              <p className="text-muted-foreground text-xs">
                Only a role change is written to the history — an allocation or notes edit is not.
              </p>
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="editMemberNotes">Notes</Label>
            <Textarea
              id="editMemberNotes"
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !allocationValid || !datesValid}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            Save changes
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
