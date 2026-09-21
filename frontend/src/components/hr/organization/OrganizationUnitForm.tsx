'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { FinanceAccountPicker } from '@/components/hr/common/FinanceAccountPicker';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import type { OrganizationLevel } from '@/types/hr/organization';

const today = () => new Date().toISOString().slice(0, 10);

export const organizationUnitSchema = z
  .object({
    name: z.string().min(1, 'Name is required').max(200),
    code: z.string().max(50).optional().or(z.literal('')),
    accountCode: z.string().max(100).optional().or(z.literal('')),
    // Round 2, lane B2. The ID is what is stored; accountCode above becomes the snapshot
    // the server writes from the chosen account on every save.
    financeAccountId: z.string().optional().or(z.literal('')),
    description: z.string().max(1000).optional().or(z.literal('')),
    organizationLevelId: z.string().min(1, 'Level is required'),
    parentUnitId: z.string().optional().or(z.literal('')),
    headEmployeeId: z.string().optional().or(z.literal('')),
    sequence: z.coerce.number().int('Must be a whole number').min(1, 'Must be at least 1'),
    isActive: z.boolean(),
    // The history row's own fields (round 2, O-3b). Max lengths match OrganizationUnitHistory.
    changeReason: z.string().max(500, 'Keep the reason under 500 characters').optional().or(z.literal('')),
    effectiveFrom: z.string().optional().or(z.literal('')),
    effectiveTo: z.string().optional().or(z.literal('')),
    notes: z.string().max(2000, 'Keep the notes under 2000 characters').optional().or(z.literal('')),
  })
  .superRefine((v, ctx) => {
    if (v.effectiveFrom && v.effectiveTo && v.effectiveTo < v.effectiveFrom) {
      ctx.addIssue({
        code: 'custom',
        path: ['effectiveTo'],
        message: 'Cannot end before it begins',
      });
    }
  });

export type OrganizationUnitFormValues = z.infer<typeof organizationUnitSchema>;

export const emptyOrganizationUnit: OrganizationUnitFormValues = {
  name: '',
  code: '',
  accountCode: '',
  financeAccountId: '',
  description: '',
  organizationLevelId: '',
  parentUnitId: '',
  headEmployeeId: '',
  sequence: 1,
  isActive: true,
  changeReason: '',
  effectiveFrom: today(),
  effectiveTo: '',
  notes: '',
};

interface OrganizationUnitFormProps {
  levels: OrganizationLevel[];
  defaultValues: OrganizationUnitFormValues;
  onSubmit: (values: OrganizationUnitFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  /** Level cannot be changed once a unit exists (enforced server-side). */
  isEdit?: boolean;
  /** The unit being edited, so the parent picker never offers it or anything beneath it. */
  unitId?: string;
  /** Name of the currently-assigned head, to seed the picker on edit. */
  initialHeadLabel?: string | null;
}

/**
 * The unit form. Two things changed in demo feedback round 2:
 *
 * - **The parent is chosen level-first** (O-1). The flat list of every unit is gone; the picker
 *   offers the tiers above the chosen level within its structure, then the units at the chosen
 *   tier, and never this unit or anything beneath it. A root-level unit has no parent and the
 *   picker does not appear.
 * - **The history row's dates are the user's** (O-3a/O-3b). Creating a unit records its initial
 *   placement — until now the log began with the first restructure — and the create form asks
 *   when that placement took effect. On edit, a reparent or change of head asks for the same
 *   dates, the reason and notes, in the amber block that already asked for the reason.
 */
export function OrganizationUnitForm({
  levels,
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  isEdit,
  unitId,
  initialHeadLabel,
}: OrganizationUnitFormProps) {
  const form = useForm<OrganizationUnitFormValues>({
    resolver: zodResolver(organizationUnitSchema) as any,
    defaultValues,
  });

  const isActive = form.watch('isActive');
  const selectedLevelId = form.watch('organizationLevelId');
  const parentUnitId = form.watch('parentUnitId') || '';
  const headEmployeeId = form.watch('headEmployeeId') || null;

  const selectedLevel = levels.find((l) => l.id === selectedLevelId);
  const levelRequiresHead = selectedLevel?.requiresHead ?? false;
  const isRootLevel = selectedLevel?.isRootLevel ?? false;

  // The two edits the change log exists to record. Comparing against the values the form loaded with
  // is what keeps the reason box from appearing on a plain rename — the server writes no history row
  // for one, so asking for a reason would be asking for something nothing will keep.
  const parentChanged =
    Boolean(isEdit) && (form.watch('parentUnitId') || '') !== (defaultValues.parentUnitId || '');
  const headChanged =
    Boolean(isEdit) && (form.watch('headEmployeeId') || '') !== (defaultValues.headEmployeeId || '');
  const recordsHistory = !isEdit || parentChanged || headChanged;

  const submit = form.handleSubmit(async (values) => {
    // The server refuses a parentless unit at any level but the root, and a parent on a root-level
    // unit. Say so here, on the field, rather than after a round trip.
    if (selectedLevel && !isRootLevel && !values.parentUnitId) {
      form.setError('parentUnitId', { message: 'A unit below the root level needs a parent unit.' });
      return;
    }
    await onSubmit({ ...values, parentUnitId: isRootLevel ? '' : values.parentUnitId });
  });

  const err = (name: keyof OrganizationUnitFormValues) =>
    form.formState.errors[name]?.message as string | undefined;

  return (
    <Card>
      <form onSubmit={submit}>
        <CardHeader>
          <CardTitle>Unit Details</CardTitle>
          <CardDescription>
            A unit is an actual node in the hierarchy (e.g. &quot;Finance Division&quot;), placed at a level and
            under a parent unit at a higher level.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" placeholder="Finance Division" {...form.register('name')} />
              {err('name') && <p className="text-sm text-red-500">{err('name')}</p>}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="FIN" {...form.register('code')} />
              {err('code') && <p className="text-sm text-red-500">{err('code')}</p>}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="organizationLevelId">Level</Label>
            <Select
              value={selectedLevelId || undefined}
              disabled={isEdit}
              onValueChange={(value) => {
                form.setValue('organizationLevelId', value, { shouldValidate: true });
                // A parent chosen for the old level may sit at or below the new one; the picker
                // re-bounds itself and drops the value, but clearing here keeps the form honest
                // even before it re-renders.
                const next = levels.find((l) => l.id === value);
                const current = levels.find((l) => l.id === selectedLevelId);
                if (!next || !current || next.levelNumber !== current.levelNumber) {
                  form.setValue('parentUnitId', '');
                }
              }}
            >
              <SelectTrigger id="organizationLevelId">
                <SelectValue placeholder="Select a level" />
              </SelectTrigger>
              <SelectContent>
                {levels.map((l) => (
                  <SelectItem key={l.id} value={l.id}>
                    {l.name} (L{l.levelNumber})
                    {l.structureName ? ` · ${l.structureName}` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {isEdit && (
              <p className="text-xs text-muted-foreground">Level cannot be changed after creation.</p>
            )}
            {err('organizationLevelId') && (
              <p className="text-sm text-red-500">{err('organizationLevelId')}</p>
            )}
          </div>

          <div className="space-y-2 rounded-md border p-4">
            <Label>Parent unit</Label>
            {!selectedLevel ? (
              <p className="text-sm text-muted-foreground">Choose this unit&apos;s level first.</p>
            ) : isRootLevel ? (
              <p className="text-sm text-muted-foreground">
                &quot;{selectedLevel.name}&quot; is the root level, so this unit sits at the top of its
                structure and has no parent.
              </p>
            ) : (
              <OrganizationUnitPicker
                idPrefix="parent"
                value={parentUnitId}
                onChange={(id) =>
                  form.setValue('parentUnitId', id, { shouldValidate: true, shouldDirty: true })
                }
                structureId={selectedLevel.structureId}
                maxLevelNumber={selectedLevel.levelNumber - 1}
                excludeSubtreeOf={unitId}
                levelLabel="Parent level"
                unitLabel="Parent unit"
                levelPlaceholder="Which level is the parent at?"
                noLevelsMessage="No level sits above this one in its structure."
                hint={`Pick the parent's level, then the unit. Any level above "${selectedLevel.name}" (L${selectedLevel.levelNumber}) qualifies — levels may be skipped.`}
                error={err('parentUnitId')}
              />
            )}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              {/*
                Round 2, lane B2. Was a free-text box; now Finance's chart decides what a valid code
                is. The unit stores the account's ID and keeps its code as a display snapshot.
              */}
              <FinanceAccountPicker
                id="financeAccountId"
                label="Account code"
                value={form.watch('financeAccountId') || null}
                onChange={(id) => form.setValue('financeAccountId', id ?? '', { shouldDirty: true })}
                description="The chart-of-accounts row this unit is charged to. Finance owns the chart."
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="sequence">Sequence</Label>
              <Input id="sequence" type="number" min={1} {...form.register('sequence')} />
              <p className="text-xs text-muted-foreground">Display order among siblings.</p>
              {err('sequence') && <p className="text-sm text-red-500">{err('sequence')}</p>}
            </div>
          </div>

          <div className="space-y-2">
            <Label>Head Employee</Label>
            <EmployeePicker
              value={headEmployeeId}
              initialLabel={initialHeadLabel}
              onChange={(id) => form.setValue('headEmployeeId', id ?? '')}
            />
            <p className="text-xs text-muted-foreground">
              {levelRequiresHead
                ? 'This level is configured to have a head — optional now, assign one later once employees exist. Once one is assigned it cannot be cleared, only replaced.'
                : 'Optional. Can be assigned later.'}
            </p>
          </div>

          {recordsHistory && (
            <div className="space-y-4 rounded-md border border-amber-200 bg-amber-50 p-4">
              <div>
                <Label className="text-base">
                  {isEdit ? 'Record this change' : 'Initial placement record'}
                </Label>
                <p className="text-xs text-muted-foreground">
                  {!isEdit
                    ? 'Creating the unit writes its initial placement to the change log — where it sits and, if named, who heads it. Say when that took effect; a structure entered after the fact can carry the date it really started.'
                    : parentChanged && headChanged
                      ? 'This edit moves the unit and changes its head. Both are recorded on the unit change log.'
                      : parentChanged
                        ? 'This edit moves the unit, which is recorded on the unit change log.'
                        : 'This edit changes who heads the unit, which is recorded on the unit change log.'}
                </p>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="effectiveFrom">Effective from</Label>
                  <Input id="effectiveFrom" type="date" {...form.register('effectiveFrom')} />
                  <p className="text-xs text-muted-foreground">Today if left blank.</p>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="effectiveTo">Effective to</Label>
                  <Input id="effectiveTo" type="date" {...form.register('effectiveTo')} />
                  <p className="text-xs text-muted-foreground">
                    Leave blank unless this arrangement has already ended; otherwise the next change
                    closes it.
                  </p>
                  {err('effectiveTo') && <p className="text-sm text-red-500">{err('effectiveTo')}</p>}
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="changeReason">Reason</Label>
                <Textarea
                  id="changeReason"
                  placeholder={
                    isEdit
                      ? 'e.g. 2026 restructure — Estates moved under Operations'
                      : 'e.g. Board resolution 14/2026 establishing the unit'
                  }
                  rows={2}
                  {...form.register('changeReason')}
                />
                <p className="text-xs text-muted-foreground">
                  Optional, but the log can record what changed and when without it — never why.
                </p>
                {err('changeReason') && <p className="text-sm text-red-500">{err('changeReason')}</p>}
              </div>

              <div className="space-y-2">
                <Label htmlFor="notes">Notes</Label>
                <Textarea
                  id="notes"
                  placeholder="Memo reference, minute number, who approved it…"
                  rows={2}
                  {...form.register('notes')}
                />
                {err('notes') && <p className="text-sm text-red-500">{err('notes')}</p>}
              </div>
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              placeholder="Optional description"
              rows={3}
              {...form.register('description')}
            />
          </div>

          <div className="flex items-center justify-between rounded-md border p-4">
            <div className="space-y-0.5">
              <Label htmlFor="isActive">Active</Label>
              <p className="text-xs text-muted-foreground">
                Inactive units are hidden from assignment.
              </p>
            </div>
            <Switch
              id="isActive"
              checked={isActive}
              onCheckedChange={(v) => form.setValue('isActive', v)}
            />
          </div>
        </CardContent>
        <CardFooter className="flex justify-end space-x-2">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Save className="mr-2 h-4 w-4" />
            )}
            {submitLabel}
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}
