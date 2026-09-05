'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowRightLeft, Loader2, UserCog } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
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
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import type { OrganizationUnit } from '@/types/hr/organization';

/**
 * The two acts the organisation-unit change log exists to record, each on its own command.
 *
 * ⚠ Why these are not just the edit form. Both operations are reachable through the ordinary
 * `PUT`, and slice 3 made that path record history rather than performing them in silence. What it
 * cannot do is tell the two apart: the form carries ONE reason box for a save that may reparent the
 * unit AND change its head, and the log records those as two independent effective-dated series.
 * A save that did both stamped the same sentence on two unrelated rows. These commands each carry
 * the reason for the single act they perform, which is what the log was built to hold.
 *
 * ⚠ Both are no-ops when nothing changes. Re-sending a unit's current parent, or reappointing the
 * sitting head, returns success and records nothing — so neither dialog offers the current value as
 * a choice, rather than letting someone submit a change that will not appear in the log.
 */

const ROOT = '__root__';

interface Props {
  unit: OrganizationUnit;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDone: () => Promise<unknown> | void;
}

// ── move ────────────────────────────────────────────────────────────────────

export function MoveUnitDialog({ unit, open, onOpenChange, onDone }: Props) {
  const { toast } = useToast();
  const [parentId, setParentId] = useState<string>('');
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const { data: units = [] } = useQuery({
    queryKey: ['hr', 'organization-units', 'all'],
    queryFn: () => organizationUnitService.getAll(),
    enabled: open,
  });
  const { data: levels = [] } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
    enabled: open,
  });

  const ownLevel = levels.find((l) => l.id === unit.organizationLevelId);

  /**
   * ⚠ Three of the server's rules, restated so the picker cannot offer a move it will refuse:
   * the parent must be in the same STRUCTURE, it must sit at a higher tier (a LOWER level number —
   * level-skipping is allowed, which is the rule slice 3 reconciled the two endpoints onto), and it
   * must not be the unit itself or anything beneath it.
   *
   * The descendant test reads `path`, which is `/ancestor/…/self`. That is only trustworthy because
   * slice 3 made a reparent cascade the path to every descendant; before that a stale path would
   * have let this list offer a unit its own child.
   */
  const candidates = useMemo(() => {
    if (!ownLevel) return [];
    const levelById = new Map(levels.map((l) => [l.id, l]));
    return units
      .filter((u) => {
        if (u.id === unit.id || !u.isActive) return false;
        if (u.path?.split('/').includes(unit.id)) return false;
        const level = levelById.get(u.organizationLevelId);
        if (!level || level.structureId !== ownLevel.structureId) return false;
        return level.levelNumber < ownLevel.levelNumber;
      })
      .filter((u) => u.id !== unit.parentUnitId)
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [units, levels, ownLevel, unit.id, unit.parentUnitId]);

  const canRoot = !!ownLevel?.isRootLevel && !!unit.parentUnitId;

  const reset = () => {
    setParentId('');
    setReason('');
  };

  const submit = async () => {
    if (!parentId || !reason.trim()) return;
    setSaving(true);
    try {
      await organizationUnitService.move(unit.id, {
        newParentId: parentId === ROOT ? null : parentId,
        changeReason: reason.trim(),
      });
      await onDone();
      toast({
        title: 'Unit moved',
        description: `"${unit.name}" was reparented and the change log records why.`,
      });
      reset();
      onOpenChange(false);
    } catch (error) {
      // The service's hierarchy rules arrive as a 400 carrying a sentence. Show the sentence.
      toast({
        title: 'Could not move this unit',
        description: (error as Error)?.message || 'Failed to move the unit.',
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
          <DialogTitle>Move {unit.name}</DialogTitle>
          <DialogDescription>
            A restructure. It is recorded on the unit&apos;s change log with the reason you give, and
            every unit beneath this one moves with it.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label>New parent *</Label>
            <Select value={parentId} onValueChange={setParentId}>
              <SelectTrigger>
                <SelectValue placeholder="Choose where it should report…" />
              </SelectTrigger>
              <SelectContent>
                {canRoot && <SelectItem value={ROOT}>— the root (no parent) —</SelectItem>}
                {candidates.map((u) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.name}
                    {u.levelName ? ` · ${u.levelName}` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <p className="text-muted-foreground text-xs">
              {unit.parentUnitName
                ? `Currently under ${unit.parentUnitName}.`
                : 'Currently at the root.'}{' '}
              Only units in the same structure at a tier above{' '}
              {ownLevel?.name ? `"${ownLevel.name}"` : 'this one'} are listed, and never this unit or
              one beneath it.
            </p>
            {!candidates.length && !canRoot && (
              <p className="text-destructive text-sm">
                There is nowhere this unit can move to — nothing sits above its level in this
                structure.
              </p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="moveReason">Why *</Label>
            <Textarea
              id="moveReason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="The reason this unit is being moved…"
            />
            <p className="text-muted-foreground text-xs">
              The change log has no delete. What you write here is permanent.
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !parentId || !reason.trim()}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <ArrowRightLeft className="mr-2 h-4 w-4" />
            )}
            Move unit
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── change of head ──────────────────────────────────────────────────────────

export function ChangeUnitHeadDialog({ unit, open, onOpenChange, onDone }: Props) {
  const { toast } = useToast();
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [clearing, setClearing] = useState(false);
  const [reason, setReason] = useState('');
  const [saving, setSaving] = useState(false);

  const { data: levels = [] } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
    enabled: open,
  });
  const requiresHead = !!levels.find((l) => l.id === unit.organizationLevelId)?.requiresHead;

  const reset = () => {
    setEmployeeId(null);
    setEmployeeLabel(null);
    setClearing(false);
    setReason('');
  };

  // Reappointing the sitting head records nothing, so it is refused here rather than reported as a
  // success that leaves the log unchanged.
  const sameAsNow = !clearing && !!employeeId && employeeId === unit.headEmployeeId;
  const ready = (clearing || !!employeeId) && !sameAsNow && !!reason.trim();

  const submit = async () => {
    if (!ready) return;
    setSaving(true);
    try {
      await organizationUnitService.changeHead(unit.id, {
        newHeadEmployeeId: clearing ? null : employeeId,
        changeReason: reason.trim(),
      });
      await onDone();
      toast({
        title: clearing ? 'Head removed' : 'Head appointed',
        description: clearing
          ? `"${unit.name}" no longer has a head.`
          : `${employeeLabel} now heads "${unit.name}".`,
      });
      reset();
      onOpenChange(false);
    } catch (error) {
      toast({
        title: 'Could not change the head',
        description: (error as Error)?.message || 'Failed to change the unit head.',
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
          <DialogTitle>
            {unit.headEmployeeId ? 'Change the head of' : 'Appoint a head for'} {unit.name}
          </DialogTitle>
          <DialogDescription>
            Recorded on the unit&apos;s change log as a change of leadership, separately from where
            the unit reports.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label>New head {clearing ? '' : '*'}</Label>
            <EmployeePicker
              value={employeeId}
              onChange={(id, label) => {
                setEmployeeId(id);
                setEmployeeLabel(label);
                setClearing(false);
              }}
              placeholder="Search by name or staff number…"
            />
            <p className="text-muted-foreground text-xs">
              {unit.headEmployeeName
                ? `Currently headed by ${unit.headEmployeeName}.`
                : 'This unit has no head today.'}
            </p>
            {sameAsNow && (
              <p className="text-destructive text-sm">
                That is already the head — reappointing them would record nothing.
              </p>
            )}
          </div>

          {unit.headEmployeeId && (
            <div className="rounded-md border p-3">
              {requiresHead ? (
                <p className="text-muted-foreground text-sm">
                  This unit&apos;s level requires a head, so the post cannot be left vacant — name a
                  successor instead.
                </p>
              ) : (
                <Button
                  type="button"
                  variant={clearing ? 'secondary' : 'outline'}
                  size="sm"
                  onClick={() => {
                    setClearing((c) => !c);
                    setEmployeeId(null);
                    setEmployeeLabel(null);
                  }}
                >
                  {clearing ? 'Keep a head after all' : 'Leave the post vacant'}
                </Button>
              )}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="headReason">Why *</Label>
            <Textarea
              id="headReason"
              rows={3}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Appointment, secondment, retirement…"
            />
            <p className="text-muted-foreground text-xs">
              The change log has no delete. What you write here is permanent.
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !ready}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <UserCog className="mr-2 h-4 w-4" />
            )}
            {clearing ? 'Remove the head' : 'Appoint'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
