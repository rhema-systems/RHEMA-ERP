'use client';

import { useState } from 'react';
import { Loader2, UserPlus } from 'lucide-react';
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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { teamService } from '@/services/hr/team.service';
import { TEAM_MEMBER_ROLES, type TeamMemberRole } from '@/types/hr/team';

interface AddTeamMemberDialogProps {
  teamId: string;
  teamName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onAdded: () => Promise<unknown> | void;
}

/**
 * Adds someone to a team.
 *
 * The server holds four rules this form cannot: the person must still be on strength, they must
 * not already be a current member, the team's cap must not be exceeded, and a leaving date cannot
 * precede the joining date. Each comes back as a 400 with a sentence, and the sentence is what the
 * toast shows — that is the whole reason the controller maps the rules rather than letting them
 * arrive as a canned 500.
 */
export function AddTeamMemberDialog({
  teamId,
  teamName,
  open,
  onOpenChange,
  onAdded,
}: AddTeamMemberDialogProps) {
  const { toast } = useToast();
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [role, setRole] = useState<TeamMemberRole>('Member');
  const [allocation, setAllocation] = useState('100');
  const [isPrimary, setIsPrimary] = useState(false);
  const [joinDate, setJoinDate] = useState(new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);

  const reset = () => {
    setEmployeeId(null);
    setEmployeeLabel(null);
    setRole('Member');
    setAllocation('100');
    setIsPrimary(false);
    setJoinDate(new Date().toISOString().slice(0, 10));
    setNotes('');
  };

  const allocationNumber = Number(allocation);
  const allocationValid =
    allocation.trim() !== '' && Number.isFinite(allocationNumber) &&
    allocationNumber >= 0 && allocationNumber <= 100;

  const handleSubmit = async () => {
    if (!employeeId || !allocationValid) return;
    setSaving(true);
    try {
      await teamService.addMember(teamId, {
        employeeId,
        role,
        allocationPercent: allocationNumber,
        isPrimary,
        joinDate,
        leaveDate: null,
        notes: notes.trim() === '' ? null : notes.trim(),
      });
      await onAdded();
      toast({ title: 'Added to the team', description: `${employeeLabel} joined ${teamName}.` });
      reset();
      onOpenChange(false);
    } catch (error) {
      toast({
        title: 'Could not add this person',
        description: (error as Error)?.message || 'Failed to add the member.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) reset();
        onOpenChange(next);
      }}
    >
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Add a member to {teamName}</DialogTitle>
          <DialogDescription>
            Someone can be on more than one team at once — that is what the allocation is for.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label>Employee *</Label>
            <EmployeePicker
              value={employeeId}
              onChange={(id, label) => {
                setEmployeeId(id);
                setEmployeeLabel(label);
              }}
              placeholder="Search by name or staff number…"
            />
          </div>

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
              <Label htmlFor="allocation">Allocation (%)</Label>
              <Input
                id="allocation"
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
              <Label htmlFor="joinDate">Joined</Label>
              <Input
                id="joinDate"
                type="date"
                value={joinDate}
                onChange={(e) => setJoinDate(e.target.value)}
              />
            </div>

            <div className="flex items-center justify-between rounded-md border p-3">
              <div>
                <Label htmlFor="isPrimary">Primary team</Label>
                <p className="text-muted-foreground text-xs">
                  Setting this clears the flag on their other teams.
                </p>
              </div>
              <Switch id="isPrimary" checked={isPrimary} onCheckedChange={setIsPrimary} />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="memberNotes">Notes</Label>
            <Textarea
              id="memberNotes"
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
          <Button onClick={handleSubmit} disabled={saving || !employeeId || !allocationValid}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <UserPlus className="mr-2 h-4 w-4" />
            )}
            Add member
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
