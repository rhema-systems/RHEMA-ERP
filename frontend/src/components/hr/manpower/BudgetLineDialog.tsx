'use client';

import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { SalaryScalePicker, type SalaryScaleSelection } from '@/components/hr/common/SalaryScalePicker';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import type { EmployeePosition } from '@/types/hr/position';
import type { BudgetPriority, ManpowerBudgetLine } from '@/types/hr/job-architecture';

const PRIORITIES: BudgetPriority[] = ['Critical', 'High', 'Medium', 'Low'];

export interface BudgetLineFormState {
  positionId: string;
  scale: SalaryScaleSelection;
  /** '' = read it from the scale; a number = typed (Manual). */
  plannedAverageSalary: string;
  plannedCount: string;
  plannedNewPositions: string;
  plannedEliminations: string;
  currentCount: string;
  currentFilled: string;
  currentVacant: string;
  currentAverageSalary: string;
  currentTotalCost: string;
  quarter: string;
  targetFillDate: string;
  priority: BudgetPriority;
  isCritical: boolean;
  notes: string;
}

export const emptyBudgetLineForm = (): BudgetLineFormState => ({
  positionId: '',
  scale: { gradeId: '', levelId: '', notchId: '' },
  plannedAverageSalary: '',
  plannedCount: '1',
  plannedNewPositions: '1',
  plannedEliminations: '0',
  currentCount: '0',
  currentFilled: '0',
  currentVacant: '0',
  currentAverageSalary: '0',
  currentTotalCost: '0',
  quarter: '',
  targetFillDate: '',
  priority: 'Medium',
  isCritical: false,
  notes: '',
});

export const budgetLineFormFromLine = (l: ManpowerBudgetLine): BudgetLineFormState => ({
  positionId: l.positionId,
  scale: { gradeId: l.salaryGradeId ?? '', levelId: l.salaryLevelId ?? '', notchId: l.salaryNotchId ?? '' },
  // A figure read from the scale is shown but NOT re-sent as typed: leaving this blank keeps
  // "from the scale" as the source unless the user overtypes it.
  plannedAverageSalary: l.plannedSalarySource === 'Manual' ? String(l.plannedAverageSalary ?? 0) : '',
  plannedCount: String(l.plannedCount ?? 0),
  plannedNewPositions: String(l.plannedNewPositions ?? 0),
  plannedEliminations: String(l.plannedEliminations ?? 0),
  currentCount: String(l.currentCount ?? 0),
  currentFilled: String(l.currentFilled ?? 0),
  currentVacant: String(l.currentVacant ?? 0),
  currentAverageSalary: String(l.currentAverageSalary ?? 0),
  currentTotalCost: String(l.currentTotalCost ?? 0),
  quarter: l.quarter ? String(l.quarter) : '',
  targetFillDate: l.targetFillDate ? l.targetFillDate.slice(0, 10) : '',
  priority: l.priority ?? 'Medium',
  isCritical: Boolean(l.isCritical),
  notes: l.notes ?? '',
});

/** The write payload. `plannedAverageSalary: null` tells the server to read it from the scale. */
export const budgetLinePayload = (f: BudgetLineFormState) => ({
  positionId: f.positionId,
  salaryGradeId: f.scale.gradeId || null,
  salaryLevelId: f.scale.levelId || null,
  salaryNotchId: f.scale.notchId || null,
  plannedAverageSalary: f.plannedAverageSalary.trim() === '' ? null : Number(f.plannedAverageSalary),
  plannedCount: Number(f.plannedCount || 0),
  plannedNewPositions: Number(f.plannedNewPositions || 0),
  plannedEliminations: Number(f.plannedEliminations || 0),
  currentCount: Number(f.currentCount || 0),
  currentFilled: Number(f.currentFilled || 0),
  currentVacant: Number(f.currentVacant || 0),
  currentAverageSalary: Number(f.currentAverageSalary || 0),
  currentTotalCost: Number(f.currentTotalCost || 0),
  // Ignored by the server since R3 (it computes average × count); sent so older builds still bind.
  plannedTotalCost: 0,
  quarter: f.quarter ? Number(f.quarter) : null,
  targetFillDate: f.targetFillDate || null,
  priority: f.priority,
  isCritical: f.isCritical,
  notes: f.notes.trim() || null,
});

/**
 * One budget line — add or correct (round 2b, R3, decision D-1).
 *
 * "Once you select a position, the salary that goes with it should display": choosing a post
 * pre-selects its grade from the salary scale (122 of 212 live positions carry one), the picker
 * offers the notches, and the average salary fills itself from the chosen place — notch amount,
 * else level mid-point, else grade minimum — with a caption saying which. Typing over it makes
 * the figure Manual; clearing the box goes back to the scale. The server re-resolves the same way
 * and records the source, so a screen that shows "from notch 3 of M2" is quoting the record.
 */
export function BudgetLineDialog({
  open,
  onOpenChange,
  title,
  positions,
  value,
  onChange,
  onSubmit,
  busy,
  positionLocked,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  title: string;
  positions: EmployeePosition[];
  value: BudgetLineFormState;
  onChange: (next: BudgetLineFormState) => void;
  onSubmit: () => void | Promise<void>;
  busy?: boolean;
  /** Editing: the line's position is fixed (change the position by removing the line). */
  positionLocked?: boolean;
}) {
  const [resolved, setResolved] = useState<{ amount: number | null; caption: string }>({ amount: null, caption: '' });

  // The position's own grade, when it has one — the picker's starting point.
  const reference = useQuery({
    queryKey: ['position-salary-reference', value.positionId],
    queryFn: () => jobArchitectureService.getPositionSalaryReference(value.positionId),
    enabled: open && !!value.positionId,
    staleTime: 5 * 60 * 1000,
  });

  // When a position is chosen and no grade has been picked yet, start from the position's grade.
  useEffect(() => {
    if (!reference.data || value.scale.gradeId) return;
    if (reference.data.salaryGradeId) {
      onChange({ ...value, scale: { gradeId: reference.data.salaryGradeId, levelId: '', notchId: '' } });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reference.data]);

  const typed = value.plannedAverageSalary.trim() !== '';
  const effectiveAmount = typed ? Number(value.plannedAverageSalary) : resolved.amount;
  const plannedCount = Number(value.plannedCount || 0);
  const canSave = !!value.positionId && plannedCount >= 0 && (typed || resolved.amount != null);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="space-y-2">
            <Label htmlFor="bl-position">Position *</Label>
            <Select
              value={value.positionId}
              onValueChange={(v) => onChange({ ...value, positionId: v, scale: { gradeId: '', levelId: '', notchId: '' }, plannedAverageSalary: '' })}
              disabled={positionLocked}
            >
              <SelectTrigger id="bl-position">
                <SelectValue placeholder="Choose a position" />
              </SelectTrigger>
              <SelectContent>
                {positions.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.title}
                    <span className="ml-2 text-xs text-muted-foreground">
                      {[p.organizationUnitName, p.code].filter(Boolean).join(' · ')}
                    </span>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {reference.data && (
              <p className="text-xs text-muted-foreground">
                {reference.data.hasGrade
                  ? `This post carries grade ${reference.data.gradeCode} (${reference.data.minSalary?.toLocaleString()} – ${reference.data.maxSalary?.toLocaleString()}).`
                  : 'This post carries no salary grade; choose one below, or type the figure.'}
              </p>
            )}
          </div>

          <SalaryScalePicker
            value={value.scale}
            onChange={(scale) => onChange({ ...value, scale })}
            onResolved={(r) => setResolved({ amount: r.amount, caption: r.caption })}
            idPrefix="bl-scale"
          />

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="bl-avg">Average salary {typed ? '(typed)' : ''}</Label>
              <Input
                id="bl-avg"
                type="number"
                min={0}
                value={typed ? value.plannedAverageSalary : resolved.amount ?? ''}
                placeholder={resolved.amount == null ? 'Type a figure or choose a grade' : undefined}
                onChange={(e) => onChange({ ...value, plannedAverageSalary: e.target.value })}
              />
              <p className="text-xs text-muted-foreground">
                {typed
                  ? resolved.caption
                    ? `Entered by hand; the scale says ${resolved.amount?.toLocaleString()} (${resolved.caption}). Clear the box to use the scale.`
                    : 'Entered by hand.'
                  : resolved.caption
                    ? `From the scale: ${resolved.caption}.`
                    : 'Choose a grade and notch, or type a figure.'}
              </p>
            </div>
            <div className="space-y-2">
              <Label>Total cost</Label>
              <div className="rounded-md border bg-muted px-3 py-2 text-sm">
                {effectiveAmount != null ? (effectiveAmount * plannedCount).toLocaleString() : '—'}
                <span className="ml-2 text-xs text-muted-foreground">average × posts, computed on save</span>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-3 gap-4">
            <NumberField id="bl-planned" label="Posts authorised *" value={value.plannedCount} onChange={(v) => onChange({ ...value, plannedCount: v })}
              hint="Cannot be fewer than the number already in post." />
            <NumberField id="bl-new" label="New posts" value={value.plannedNewPositions} onChange={(v) => onChange({ ...value, plannedNewPositions: v })} />
            <NumberField id="bl-filled" label="Filled today" value={value.currentFilled} onChange={(v) => onChange({ ...value, currentFilled: v })} />
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label htmlFor="bl-quarter">Quarter to fill</Label>
              <Select value={value.quarter || '__none__'} onValueChange={(v) => onChange({ ...value, quarter: v === '__none__' ? '' : v })}>
                <SelectTrigger id="bl-quarter"><SelectValue placeholder="Any" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Any</SelectItem>
                  {['1', '2', '3', '4'].map((q) => <SelectItem key={q} value={q}>Q{q}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="bl-target">Target fill date</Label>
              <Input id="bl-target" type="date" value={value.targetFillDate} onChange={(e) => onChange({ ...value, targetFillDate: e.target.value })} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="bl-priority">Priority</Label>
              <Select value={value.priority} onValueChange={(v) => onChange({ ...value, priority: v as BudgetPriority })}>
                <SelectTrigger id="bl-priority"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {PRIORITIES.map((p) => <SelectItem key={p} value={p}>{p}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Checkbox id="bl-critical" checked={value.isCritical} onCheckedChange={(c) => onChange({ ...value, isCritical: c === true })} />
            <Label htmlFor="bl-critical">Critical post</Label>
          </div>

          <div className="space-y-2">
            <Label htmlFor="bl-notes">Notes</Label>
            <Textarea id="bl-notes" rows={2} maxLength={1000} value={value.notes} onChange={(e) => onChange({ ...value, notes: e.target.value })} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button disabled={!canSave || busy} onClick={() => onSubmit()}>
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function NumberField({ id, label, value, onChange, hint }: { id: string; label: string; value: string; onChange: (v: string) => void; hint?: string }) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} type="number" min={0} value={value} onChange={(e) => onChange(e.target.value)} />
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}
