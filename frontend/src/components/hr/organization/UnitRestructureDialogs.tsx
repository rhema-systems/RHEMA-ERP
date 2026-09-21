'use client';

import { useState } from 'react';
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
import { useToast } from '@/components/ui/use-toast';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { organizationLevelService } from '@/services/hr/organization-level.service';
import type { OrganizationUnit } from '@/types/hr/organization';
import {
  HistoryStampFields,
  emptyHistoryStamp,
  stampError,
  stampPayload,
  type HistoryStamp,
} from './HistoryStampFields';

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
 *
 * Demo feedback round 2 (O-1, O-3b): the new parent is chosen level-first through the shared
 * picker, and both dialogs carry the dates the change took effect and notes beyond the reason.
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
  const [toRoot, setToRoot] = useState(false);
  const [reason, setReason] = useState('');
  const [stamp, setStamp] = useState<HistoryStamp>(emptyHistoryStamp);
  const [saving, setSaving] = useState(false);

  const { data: levels = [] } = useQuery({
    queryKey: ['hr', 'organization-levels', 'all'],
    queryFn: () => organizationLevelService.getAll(),
    enabled: open,
  });

  const ownLevel = levels.find((l) => l.id === unit.organizationLevelId);
  const canRoot = !!ownLevel?.isRootLevel && !!unit.parentUnitId;

  const reset = () => {
    setParentId('');
    setToRoot(false);
    setReason('');
    setStamp(emptyHistoryStamp());
  };

  const target = toRoot ? ROOT : parentId;
  const ready = !!target && !!reason.trim() && !stampError(stamp);

  const submit = async () => {
    if (!ready) return;
    setSaving(true);
    try {
      await organizationUnitService.move(unit.id, {
        newParentId: toRoot ? null : parentId,
        changeReason: reason.trim(),
        ...stampPayload(stamp),
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
      <DialogContent className="sm:max-w-xl">
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
            {/*
              ⚠ The server's rules, restated so the picker cannot offer a move it will refuse: same
              STRUCTURE, a higher tier (a LOWER level number — skipping allowed, the rule slice 3
              reconciled both endpoints onto), never this unit or anything beneath it, and never
              the parent it already has (a no-op the log would not record).
            */}
            {ownLevel ? (
              <OrganizationUnitPicker
                idPrefix="move-parent"
                value={toRoot ? '' : parentId}
                onChange={(id) => {
                  setParentId(id);
                  if (id) setToRoot(false);
                }}
                structureId={ownLevel.structureId}
                maxLevelNumber={ownLevel.levelNumber - 1}
                excludeSubtreeOf={unit.id}
                excludeIds={unit.parentUnitId ? [unit.parentUnitId] : undefined}
                levelLabel="Parent level"
                unitLabel="Parent unit"
                levelPlaceholder="Which level should it report to?"
                noLevelsMessage="Nothing sits above this unit's level in its structure."
                disabled={toRoot}
              />
            ) : (
              <p className="text-muted-foreground text-sm">Loading the unit&apos;s level…</p>
            )}
            {canRoot && (
              <Button
                type="button"
                variant={toRoot ? 'secondary' : 'outline'}
                size="sm"
                onClick={() => {
                  setToRoot((r) => !r);
                  setParentId('');
                }}
              >
                {toRoot ? 'Choose a parent instead' : 'Move to the top of the structure (no parent)'}
              </Button>
            )}
            <p className="text-muted-foreground text-xs">
              {unit.parentUnitName
                ? `Currently under ${unit.parentUnitName}.`
                : 'Currently at the root.'}{' '}
              Pick the parent&apos;s level, then the unit. Only levels above{' '}
              {ownLevel?.name ? `"${ownLevel.name}"` : 'this one'} in the same structure are offered,
              and never this unit or one beneath it.
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="moveReason">Why *</Label>
            <Textarea
              id="moveReason"
              rows={2}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="The reason this unit is being moved…"
            />
          </div>

          <HistoryStampFields idPrefix="move" value={stamp} onChange={setStamp} />
          <p className="text-muted-foreground text-xs">
            The change log has no delete. What you write here can be corrected later, never removed.
          </p>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !ready}>
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
  const [stamp, setStamp] = useState<HistoryStamp>(emptyHistoryStamp);
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
    setStamp(emptyHistoryStamp());
  };

  // Reappointing the sitting head records nothing, so it is refused here rather than reported as a
  // success that leaves the log unchanged.
  const sameAsNow = !clearing && !!employeeId && employeeId === unit.headEmployeeId;
  const ready = (clearing || !!employeeId) && !sameAsNow && !!reason.trim() && !stampError(stamp);

  const submit = async () => {
    if (!ready) return;
    setSaving(true);
    try {
      await organizationUnitService.changeHead(unit.id, {
        newHeadEmployeeId: clearing ? null : employeeId,
        changeReason: reason.trim(),
        ...stampPayload(stamp),
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
      <DialogContent className="sm:max-w-xl">
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
              rows={2}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Appointment, secondment, retirement…"
            />
          </div>

          <HistoryStampFields idPrefix="head" value={stamp} onChange={setStamp} />
          <p className="text-muted-foreground text-xs">
            The change log has no delete. What you write here can be corrected later, never removed.
          </p>
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
