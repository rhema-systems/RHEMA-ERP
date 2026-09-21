'use client';

import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';

export interface HistoryStamp {
  effectiveFrom: string;
  effectiveTo: string;
  notes: string;
}

export const emptyHistoryStamp = (): HistoryStamp => ({
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: '',
  notes: '',
});

/** '' for a well-formed stamp, otherwise the sentence to show. */
export function stampError(stamp: HistoryStamp, fromRequired = false): string {
  if (fromRequired && !stamp.effectiveFrom) return 'Say when this took effect.';
  if (stamp.effectiveFrom && stamp.effectiveTo && stamp.effectiveTo < stamp.effectiveFrom)
    return 'Cannot end before it begins.';
  if (stamp.notes.length > 2000) return 'Keep the notes under 2000 characters.';
  return '';
}

/** The stamp as the API wants it: blanks become nulls. */
export function stampPayload(stamp: HistoryStamp) {
  return {
    effectiveFrom: stamp.effectiveFrom || null,
    effectiveTo: stamp.effectiveTo || null,
    notes: stamp.notes.trim() || null,
  };
}

interface HistoryStampFieldsProps {
  value: HistoryStamp;
  onChange: (next: HistoryStamp) => void;
  idPrefix?: string;
  disabled?: boolean;
  /** Copy under "Effective from". Defaults to the open-ended wording. */
  fromHint?: string;
}

/**
 * Effective from / effective to / notes — the three user-supplied fields of a change-log row,
 * shared by the move dialog, the change-of-head dialog and the hand-recorded entry dialog so the
 * three cannot describe the same fields differently (demo feedback round 2, O-3b).
 */
export function HistoryStampFields({
  value,
  onChange,
  idPrefix = 'stamp',
  disabled,
  fromHint = 'Today if left blank.',
}: HistoryStampFieldsProps) {
  const set = (patch: Partial<HistoryStamp>) => onChange({ ...value, ...patch });
  const inverted = Boolean(
    value.effectiveFrom && value.effectiveTo && value.effectiveTo < value.effectiveFrom,
  );

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label htmlFor={`${idPrefix}-from`}>Effective from</Label>
          <Input
            id={`${idPrefix}-from`}
            type="date"
            value={value.effectiveFrom}
            onChange={(e) => set({ effectiveFrom: e.target.value })}
            disabled={disabled}
          />
          <p className="text-muted-foreground text-xs">{fromHint}</p>
        </div>
        <div className="space-y-2">
          <Label htmlFor={`${idPrefix}-to`}>Effective to</Label>
          <Input
            id={`${idPrefix}-to`}
            type="date"
            value={value.effectiveTo}
            onChange={(e) => set({ effectiveTo: e.target.value })}
            disabled={disabled}
          />
          <p className="text-muted-foreground text-xs">
            Leave blank unless it has already ended; the next change of the same kind closes it.
          </p>
          {inverted && <p className="text-destructive text-sm">Cannot end before it begins.</p>}
        </div>
      </div>
      <div className="space-y-2">
        <Label htmlFor={`${idPrefix}-notes`}>Notes</Label>
        <Textarea
          id={`${idPrefix}-notes`}
          rows={2}
          value={value.notes}
          onChange={(e) => set({ notes: e.target.value })}
          placeholder="Memo reference, minute number, who approved it…"
          disabled={disabled}
        />
      </div>
    </div>
  );
}
