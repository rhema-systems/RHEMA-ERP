'use client';

import { useEffect, useState } from 'react';
import { Loader2, Pencil, PlusCircle } from 'lucide-react';
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
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { organizationUnitHistoryService } from '@/services/hr/organization-unit-history.service';
import type { OrganizationUnitHistoryEntry } from '@/types/hr/organization';
import { changeSentence } from './UnitChangeLog';
import {
  HistoryStampFields,
  emptyHistoryStamp,
  stampError,
  type HistoryStamp,
} from './HistoryStampFields';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Fix the unit (the unit's own tab). Without it the dialog asks which unit. */
  unit?: { id: string; name: string } | null;
  /** Correct this row instead of recording a new one. */
  entry?: OrganizationUnitHistoryEntry | null;
  onDone: () => Promise<unknown> | void;
}

/**
 * Record a change-log entry by hand, or correct one's dates, reason and notes.
 *
 * Demo feedback round 2, O-3b. Both doors are admin-tier on the server (HR.Employee.Admin) and
 * as narrow as the ask allows: a hand-recorded entry moves no parent and appoints no head — it
 * is for the things the structure's own writers cannot say, a merger minuted before the units
 * were rebuilt, a renaming — and it must give a reason. A correction touches only what the user
 * supplied in the first place; what a row says CHANGED is not editable, because a row that
 * disagrees with what happened is fixed by recording what happened.
 *
 * ⚠ Correcting a row's start does not move the previous row's end. When both are wrong, open
 * both.
 */
export function UnitHistoryEntryDialog({ open, onOpenChange, unit, entry, onDone }: Props) {
  const { toast } = useToast();
  const editing = !!entry;
  const [unitId, setUnitId] = useState(unit?.id ?? '');
  const [reason, setReason] = useState('');
  const [stamp, setStamp] = useState<HistoryStamp>(emptyHistoryStamp);
  const [saving, setSaving] = useState(false);

  // Hydrate from the row being corrected, or reset for a fresh entry, whenever the dialog opens.
  useEffect(() => {
    if (!open) return;
    if (entry) {
      setUnitId(entry.organizationUnitId);
      setReason(entry.changeReason ?? '');
      setStamp({
        effectiveFrom: entry.effectiveFrom.slice(0, 10),
        effectiveTo: entry.effectiveTo ? entry.effectiveTo.slice(0, 10) : '',
        notes: entry.notes ?? '',
      });
    } else {
      setUnitId(unit?.id ?? '');
      setReason('');
      setStamp(emptyHistoryStamp());
    }
  }, [open, entry, unit?.id]);

  // A hand-recorded row, and a row that records no change of its own, must carry a reason.
  const reasonRequired = !editing || entry?.changeType === 'Other';
  const problem =
    stampError(stamp, true) ||
    (!unitId ? 'Choose the unit.' : '') ||
    (reasonRequired && !reason.trim() ? 'A reason is required.' : '') ||
    (reason.length > 500 ? 'Keep the reason under 500 characters.' : '');

  const submit = async () => {
    if (problem) return;
    setSaving(true);
    try {
      if (editing && entry) {
        await organizationUnitHistoryService.update(entry.id, {
          id: entry.id,
          effectiveFrom: stamp.effectiveFrom,
          effectiveTo: stamp.effectiveTo || null,
          changeReason: reason.trim() || null,
          notes: stamp.notes.trim() || null,
        });
        toast({ title: 'Entry corrected', description: 'The change log now carries the corrected dates.' });
      } else {
        await organizationUnitHistoryService.createManual({
          organizationUnitId: unitId,
          effectiveFrom: stamp.effectiveFrom,
          effectiveTo: stamp.effectiveTo || null,
          changeReason: reason.trim(),
          notes: stamp.notes.trim() || null,
        });
        toast({ title: 'Entry recorded', description: 'The change log carries the new entry.' });
      }
      await onDone();
      onOpenChange(false);
    } catch (error) {
      toast({
        title: editing ? 'Could not correct the entry' : 'Could not record the entry',
        description: (error as Error)?.message || 'The change log refused the entry.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>
            {editing ? 'Correct this entry' : unit ? `Record an entry for ${unit.name}` : 'Record an entry'}
          </DialogTitle>
          <DialogDescription>
            {editing
              ? 'Dates, reason and notes only. What the entry says changed cannot be rewritten — record what happened instead.'
              : 'For a change the structure cannot express by itself — a merger minuted before the units were rebuilt, a renaming, a correction to the record. It moves no unit and appoints no head.'}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {editing && entry ? (
            <div className="rounded-md border bg-muted/40 p-3 text-sm">
              <div className="font-medium">{entry.organizationUnitName ?? 'This unit'}</div>
              <div className="text-muted-foreground">{changeSentence(entry)}</div>
            </div>
          ) : unit ? null : (
            <OrganizationUnitPicker
              idPrefix="entry-unit"
              value={unitId}
              onChange={(id) => setUnitId(id)}
              levelLabel="Level"
              unitLabel="Unit"
            />
          )}

          <HistoryStampFields
            idPrefix="entry"
            value={stamp}
            onChange={setStamp}
            fromHint="The day the arrangement this entry describes took effect."
          />

          <div className="space-y-2">
            <Label htmlFor="entryReason">Reason {reasonRequired ? '*' : ''}</Label>
            <Textarea
              id="entryReason"
              rows={2}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="What this entry records, and why…"
            />
            {reasonRequired && (
              <p className="text-muted-foreground text-xs">
                An entry that records no reporting-line or leadership change has nothing but its
                reason to say what it is.
              </p>
            )}
          </div>

          {problem && (reason || unitId || stamp.effectiveTo) && (
            <p className="text-destructive text-sm">{problem}</p>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={saving || !!problem}>
            {saving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : editing ? (
              <Pencil className="mr-2 h-4 w-4" />
            ) : (
              <PlusCircle className="mr-2 h-4 w-4" />
            )}
            {editing ? 'Save correction' : 'Record entry'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
