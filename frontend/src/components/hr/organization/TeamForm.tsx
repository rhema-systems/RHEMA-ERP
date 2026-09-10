'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { FinanceAccountPicker } from '@/components/hr/common/FinanceAccountPicker';
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
import {
  TEAM_STATUSES,
  TEAM_TYPES,
  type CreateTeamRequest,
  type TeamStatus,
  type TeamSummary,
  type TeamType,
} from '@/types/hr/team';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

/** Selects cannot hold an empty string as a value, so absence needs a sentinel. */
const NONE = 'none';

export const teamSchema = z
  .object({
    name: z.string().trim().min(1, 'A team name is required').max(200),
    code: z.string().trim().min(1, 'A team code is required').max(50),
    description: z.string().max(1000).optional().or(z.literal('')),
    teamType: z.string().min(1),
    status: z.string().min(1),
    organizationUnitId: z.string().optional().or(z.literal('')),
    teamLeadId: z.string().optional().or(z.literal('')),
    parentTeamId: z.string().optional().or(z.literal('')),
    costCenterCode: z.string().max(100).optional().or(z.literal('')),
    // Round 2, lane B2 — see the note on the unit form.
    financeAccountId: z.string().optional().or(z.literal('')),
    projectCode: z.string().max(50).optional().or(z.literal('')),
    teamEmail: z
      .string()
      .max(200)
      .optional()
      .or(z.literal(''))
      .refine((v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v), 'Enter a valid email address.'),
    effectiveFrom: z.string().min(1, 'A start date is required'),
    effectiveTo: z.string().optional().or(z.literal('')),
    maxMembers: z
      .union([z.coerce.number().int().min(1).max(10000), z.literal('')])
      .optional(),
    sequence: z.coerce.number().int('Must be a whole number').min(1, 'Must be at least 1'),
    isActive: z.boolean(),
    notes: z.string().max(2000).optional().or(z.literal('')),
  })
  // Mirrors the server rule rather than hoping the user never hits it. The server still enforces
  // it — a client check that is the only check is not a rule, it is a suggestion.
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
    message: 'A team cannot end before it starts.',
    path: ['effectiveTo'],
  });

/**
 * The values a submit handler receives — the schema's OUTPUT, so `sequence` is a number.
 */
export type TeamFormValues = z.output<typeof teamSchema>;

/**
 * What the form actually holds — the schema's INPUT.
 *
 * These differ, and the difference is not cosmetic: `z.coerce.number()` accepts `unknown` and
 * yields `number`, so a form typed on the output alone will not accept the resolver once the
 * schema is wrapped in an object-level `.refine()`. Declaring both on `useForm` is the fix
 * react-hook-form provides for exactly this; casting the resolver would silence the error and
 * leave the handler lying about what it is handed.
 */
export type TeamFormInput = z.input<typeof teamSchema>;

export const emptyTeam: TeamFormInput = {
  name: '',
  code: '',
  description: '',
  teamType: 'Permanent',
  status: 'Draft',
  organizationUnitId: '',
  teamLeadId: '',
  parentTeamId: '',
  costCenterCode: '',
  financeAccountId: '',
  projectCode: '',
  teamEmail: '',
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: '',
  maxMembers: '',
  sequence: 1,
  isActive: true,
  notes: '',
};

interface TeamFormProps {
  /** Candidate parents. On edit, the team itself must already be filtered out by the caller. */
  parentCandidates: TeamSummary[];
  defaultValues: TeamFormInput;
  onSubmit: (values: TeamFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  initialLeadLabel?: string | null;
}

export function TeamForm({
  parentCandidates,
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  initialLeadLabel,
}: TeamFormProps) {
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useForm<TeamFormInput, unknown, TeamFormValues>({
    resolver: zodResolver(teamSchema),
    defaultValues,
  });

  const teamType = watch('teamType') as TeamType;
  const typeHint = TEAM_TYPES.find((t) => t.value === teamType)?.hint;
  const statusHint = TEAM_STATUSES.find((s) => s.value === watch('status'))?.hint;

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Identity</CardTitle>
          <CardDescription>
            What the team is called and what kind of group it is. The code must be unique across the
            tenant.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="name">Name *</Label>
            <Input id="name" {...register('name')} />
            {errors.name && <p className="text-destructive text-sm">{errors.name.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="code">Code *</Label>
            <Input id="code" {...register('code')} />
            {errors.code && <p className="text-destructive text-sm">{errors.code.message}</p>}
          </div>

          <div className="space-y-2">
            <Label>Type</Label>
            <Select value={teamType} onValueChange={(v) => setValue('teamType', v)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {TEAM_TYPES.map((t) => (
                  <SelectItem key={t.value} value={t.value}>
                    {t.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {typeHint && <p className="text-muted-foreground text-xs">{typeHint}</p>}
          </div>

          <div className="space-y-2">
            <Label>Status</Label>
            <Select value={watch('status')} onValueChange={(v) => setValue('status', v)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {TEAM_STATUSES.map((s) => (
                  <SelectItem key={s.value} value={s.value}>
                    {s.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {statusHint && <p className="text-muted-foreground text-xs">{statusHint}</p>}
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="description">Description</Label>
            <Textarea id="description" rows={2} {...register('description')} />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Placement</CardTitle>
          <CardDescription>
            Where the team sits. All of these are optional — a cross-functional team belongs to no
            single unit, which is exactly what distinguishes it from an organization unit.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2 md:col-span-2">
            {/* Level-first, like every unit choice since demo feedback round 2 (O-6). */}
            <OrganizationUnitPicker
              idPrefix="owning-unit"
              value={watch('organizationUnitId') || ''}
              onChange={(id) => setValue('organizationUnitId', id, { shouldDirty: true })}
              allowNone="None — cross-functional"
              levelLabel="Owning unit's level"
              unitLabel="Owning unit"
            />
          </div>

          <div className="space-y-2">
            <Label>Parent team</Label>
            <Select
              value={watch('parentTeamId') || NONE}
              onValueChange={(v) => setValue('parentTeamId', v === NONE ? '' : v)}
            >
              <SelectTrigger>
                <SelectValue placeholder="None" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>None — a top-level team</SelectItem>
                {parentCandidates.map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label>Team lead</Label>
            <EmployeePicker
              value={watch('teamLeadId') || null}
              initialLabel={initialLeadLabel}
              onChange={(id) => setValue('teamLeadId', id ?? '')}
              placeholder="Search for the team lead…"
            />
            <p className="text-muted-foreground text-xs">
              Not necessarily the head of the owning unit — that is the point of a team.
            </p>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Period and size</CardTitle>
          <CardDescription>
            When the team runs and how large it may get. A cap is enforced when members are added.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="effectiveFrom">Effective from *</Label>
            <Input id="effectiveFrom" type="date" {...register('effectiveFrom')} />
            {errors.effectiveFrom && (
              <p className="text-destructive text-sm">{errors.effectiveFrom.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="effectiveTo">Effective to</Label>
            <Input id="effectiveTo" type="date" {...register('effectiveTo')} />
            <p className="text-muted-foreground text-xs">
              Leave blank for an open-ended team. Typical for permanent teams.
            </p>
            {errors.effectiveTo && (
              <p className="text-destructive text-sm">{errors.effectiveTo.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="maxMembers">Maximum members</Label>
            <Input id="maxMembers" type="number" min={1} {...register('maxMembers')} />
            <p className="text-muted-foreground text-xs">Blank means no cap.</p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="sequence">Display order</Label>
            <Input id="sequence" type="number" min={1} {...register('sequence')} />
            {errors.sequence && (
              <p className="text-destructive text-sm">{errors.sequence.message}</p>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>References</CardTitle>
          <CardDescription>Codes and contacts other systems know this team by.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="projectCode">Project code</Label>
            <Input id="projectCode" {...register('projectCode')} />
          </div>

          <div className="space-y-2">
            {/* Round 2, lane B2 — the chart of accounts decides, not free text. */}
            <FinanceAccountPicker
              id="financeAccountId"
              label="Cost centre code"
              value={watch('financeAccountId') || null}
              onChange={(id) => setValue('financeAccountId', id ?? '', { shouldDirty: true })}
              description="The chart-of-accounts row this team's costs are charged to."
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="teamEmail">Team email</Label>
            <Input id="teamEmail" type="email" {...register('teamEmail')} />
            {errors.teamEmail && (
              <p className="text-destructive text-sm">{errors.teamEmail.message}</p>
            )}
          </div>

          <div className="flex items-center justify-between rounded-md border p-3">
            <div>
              <Label htmlFor="isActive">Active</Label>
              <p className="text-muted-foreground text-xs">
                Inactive teams stay in the register and off the live views.
              </p>
            </div>
            <Switch
              id="isActive"
              checked={watch('isActive')}
              onCheckedChange={(v) => setValue('isActive', v)}
            />
          </div>

          <div className="space-y-2 md:col-span-2">
            <Label htmlFor="notes">Notes</Label>
            <Textarea id="notes" rows={3} {...register('notes')} />
          </div>
        </CardContent>
        <CardFooter className="justify-end gap-2">
          <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
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
      </Card>
    </form>
  );
}

/**
 * Form values → the create/update payload.
 *
 * Kept beside the form because the two must change together. Three things it exists to get right,
 * each of which the form's own shape makes easy to get wrong:
 *   - the select sentinels and empty strings become `null`, not `""`;
 *   - `maxMembers` is a union of number and `''`, and `''` must not reach the API as `0`;
 *   - `teamType` and `status` are plain strings in the schema (zod cannot narrow a select's value
 *     for us) and are cast back to their unions here, in one place, rather than at each call site.
 */
export function toCreateRequest(values: TeamFormValues): CreateTeamRequest {
  const blankToNull = (v: string | undefined) => {
    const trimmed = (v ?? '').trim();
    return trimmed === '' ? null : trimmed;
  };

  return {
    name: values.name.trim(),
    code: values.code.trim(),
    description: blankToNull(values.description),
    teamType: values.teamType as TeamType,
    status: values.status as TeamStatus,
    organizationUnitId: blankToNull(values.organizationUnitId),
    teamLeadId: blankToNull(values.teamLeadId),
    parentTeamId: blankToNull(values.parentTeamId),
    locationId: null,
    shiftId: null,
    costCenterCode: blankToNull(values.costCenterCode),
    financeAccountId: blankToNull(values.financeAccountId),
    projectCode: blankToNull(values.projectCode),
    teamEmail: blankToNull(values.teamEmail),
    effectiveFrom: values.effectiveFrom,
    effectiveTo: blankToNull(values.effectiveTo),
    maxMembers:
      values.maxMembers === '' || values.maxMembers === undefined ? null : Number(values.maxMembers),
    sequence: values.sequence,
    isActive: values.isActive,
    notes: blankToNull(values.notes),
  };
}

/** Seeds the form from a saved team. The inverse of `toCreateRequest`. */
export function toFormValues(team: {
  name: string;
  code: string;
  description?: string | null;
  teamType: TeamType;
  status: TeamStatus;
  organizationUnitId?: string | null;
  teamLeadId?: string | null;
  parentTeamId?: string | null;
  costCenterCode?: string | null;
  financeAccountId?: string | null;
  projectCode?: string | null;
  teamEmail?: string | null;
  effectiveFrom: string;
  effectiveTo?: string | null;
  maxMembers?: number | null;
  sequence: number;
  isActive: boolean;
  notes?: string | null;
}): TeamFormInput {
  return {
    name: team.name,
    code: team.code,
    description: team.description ?? '',
    teamType: team.teamType,
    status: team.status,
    organizationUnitId: team.organizationUnitId ?? '',
    teamLeadId: team.teamLeadId ?? '',
    parentTeamId: team.parentTeamId ?? '',
    costCenterCode: team.costCenterCode ?? '',
    financeAccountId: team.financeAccountId ?? '',
    projectCode: team.projectCode ?? '',
    teamEmail: team.teamEmail ?? '',
    // DateOnly serialises as YYYY-MM-DD, which is already what <input type="date"> wants. Slicing
    // to 10 anyway costs nothing and survives the day someone widens the field to a DateTime.
    effectiveFrom: (team.effectiveFrom ?? '').slice(0, 10),
    effectiveTo: (team.effectiveTo ?? '').slice(0, 10),
    maxMembers: team.maxMembers ?? '',
    sequence: team.sequence,
    isActive: team.isActive,
    notes: team.notes ?? '',
  };
}
