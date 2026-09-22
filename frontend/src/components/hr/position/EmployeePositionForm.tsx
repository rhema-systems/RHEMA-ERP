'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { CertificationPicker } from '@/components/hr/common/CertificationPicker';
import { CurrencyPicker } from '@/components/hr/common/CurrencyPicker';
import { employeePositionService } from '@/services/hr/employee-position.service';
import { preEmploymentCheckTemplateService } from '@/services/hr/offers.service';
import { SKILL_LEVEL_OPTIONS, type EmployeePosition } from '@/types/hr/position';
import type { StaffLevelListItem } from '@/types/hr/staff-level';
import type { SalaryGrade } from '@/types/hr/salary';
import type { Skill } from '@/types/hr/skill';

/** Just what the picker needs — the full BenefitPolicy type is not required here. */
export interface BenefitPolicySummary {
  id: string;
  policyName: string;
  policyCode?: string | null;
  isMandatory?: boolean;
}

const NONE = 'none';
const workModes = ['OnSite', 'Remote', 'Hybrid'] as const;
const skillLevels = ['Beginner', 'Intermediate', 'Advanced', 'Expert', 'Master'] as const;

// Optional integer entered as text ('' = not provided).
const optionalInt = z
  .string()
  .optional()
  .or(z.literal(''))
  .refine((v) => !v || /^\d+$/.test(v), 'Must be a whole number');

export const employeePositionSchema = z.object({
  title: z.string().min(1, 'Title is required').max(100),
  code: z.string().max(20).optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  organizationUnitId: z.string().min(1, 'Organization unit is required'),
  organizationLevelId: z.string().min(1, 'Select a unit to derive its level'),
  reportsToPositionId: z.string().optional().or(z.literal('')),
  staffLevelId: z.string().optional().or(z.literal('')),
  salaryGradeId: z.string().optional().or(z.literal('')),
  level: z.coerce.number().int('Must be a whole number').min(1, 'Must be at least 1'),
  expectedHeadcount: z.coerce.number().int('Must be a whole number').min(1, 'Must be at least 1'),
  workMode: z.enum(workModes),
  probationPeriodMonths: optionalInt,
  noticePeriodMonths: optionalInt,
  minimumExperienceYears: optionalInt,
  minimumAge: optionalInt,
  maximumAge: optionalInt,
  requiresCertification: z.boolean(),
  requiresGuarantor: z.boolean(),
  requiredGuarantorAmount: z.string().optional().or(z.literal('')),
  requiredGuarantorCurrencyCode: z.string().optional().or(z.literal('')),
  requiresLicense: z.boolean(),
  preEmploymentCheckTemplateId: z.string().nullable(),
  isActive: z.boolean(),
  skillRequirements: z.array(
    z.object({
      skillId: z.string().min(1, 'Select a skill'),
      requiredLevel: z.enum(skillLevels),
      isRequired: z.boolean(),
      priority: z.coerce.number().int().min(1),
    }),
  ),
  // What the post must hold (round 2, lane C2 — P-2). Sent as the whole set, like the two above.
  certificationRequirements: z.array(
    z.object({
      certificationId: z.string().min(1, 'Choose the certification'),
      isMandatory: z.boolean(),
      notes: z.string().max(500).optional().or(z.literal('')),
    }),
  ),
  positionBenefits: z.array(
    z.object({
      policyId: z.string().min(1, 'Select a benefit policy'),
      // '' = no expiry. Kept as text so an empty date input stays empty rather than
      // collapsing to an epoch date.
      expiryDate: z.string().optional().or(z.literal('')),
      positionAmount: z.string().optional().or(z.literal('')),
    }),
  ),
  // Named sets attached to the post (round 2, lane C3). Ids only: what each set contains is
  // maintained on its own master screen, and the post follows it. Sent as the whole set.
  benefitGroupIds: z.array(z.string()),
  skillSetIds: z.array(z.string()),
  certificationSetIds: z.array(z.string()),
});

export type EmployeePositionFormValues = z.infer<typeof employeePositionSchema>;

/**
 * A named set on offer, with what it contains (round 2, lane C3). The member ids are what lets the
 * form grey out an item a set already provides, so the user never composes a save the server will
 * refuse — the same courtesy `takenIds` already did for a policy chosen twice.
 */
export interface NamedSetOption {
  id: string;
  name: string;
  code?: string | null;
  isActive: boolean;
  memberIds: string[];
  memberCount: number;
}

/**
 * The attach/detach strip that sits above each individual list (round 2, lane C3, plan § 6.4.3:
 * "sets above individuals"). Deliberately a row of toggles rather than a dialog: the whole point of
 * a set is that attaching it is one click, and seeing what is attached is the same glance.
 */
function SetAttachPanel({
  label,
  hint,
  sets,
  attachedIds,
  memberNoun,
  onToggle,
}: {
  label: string;
  hint: string;
  sets: NamedSetOption[];
  attachedIds: string[];
  memberNoun: string;
  onToggle: (setId: string) => void;
}) {
  // A retired set already attached still shows, so it can be seen and detached; a retired set that
  // is NOT attached is not offered, because the server refuses to attach one.
  const offered = sets.filter((s) => s.isActive || attachedIds.includes(s.id));
  if (offered.length === 0) return null;

  return (
    <div className="space-y-2 rounded-md border border-dashed p-3">
      <div>
        <h5 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{label}</h5>
        <p className="text-xs text-muted-foreground">{hint}</p>
      </div>
      <div className="flex flex-wrap gap-2">
        {offered.map((s) => {
          const attached = attachedIds.includes(s.id);
          return (
            <button
              key={s.id}
              type="button"
              onClick={() => onToggle(s.id)}
              aria-pressed={attached}
              className={`rounded-full border px-3 py-1 text-xs transition ${
                attached
                  ? 'border-primary bg-primary/10 font-medium text-primary'
                  : 'border-input text-muted-foreground hover:bg-accent'
              }`}
            >
              {s.name}
              <span className="ml-1 opacity-70">
                ({s.memberCount} {memberNoun}
                {s.memberCount === 1 ? '' : 's'})
              </span>
              {!s.isActive && <span className="ml-1 opacity-70">· retired</span>}
            </button>
          );
        })}
      </div>
    </div>
  );
}

/** Every item id the attached sets provide, and which set provides each. */
function coverageOf(sets: NamedSetOption[], attachedIds: string[]): Map<string, string> {
  const map = new Map<string, string>();
  for (const set of sets) {
    if (!attachedIds.includes(set.id)) continue;
    for (const memberId of set.memberIds) {
      if (!map.has(memberId)) map.set(memberId, set.name);
    }
  }
  return map;
}

export const emptyEmployeePosition: EmployeePositionFormValues = {
  title: '',
  code: '',
  description: '',
  organizationUnitId: '',
  organizationLevelId: '',
  reportsToPositionId: '',
  staffLevelId: '',
  salaryGradeId: '',
  level: 1,
  expectedHeadcount: 1,
  workMode: 'OnSite',
  probationPeriodMonths: '',
  noticePeriodMonths: '',
  minimumExperienceYears: '',
  minimumAge: '',
  maximumAge: '',
  requiresCertification: false,
  requiresGuarantor: false,
  requiredGuarantorAmount: '',
  requiredGuarantorCurrencyCode: '',
  requiresLicense: false,
  preEmploymentCheckTemplateId: null,
  isActive: true,
  skillRequirements: [],
  positionBenefits: [],
  certificationRequirements: [],
  benefitGroupIds: [],
  skillSetIds: [],
  certificationSetIds: [],
};

interface EmployeePositionFormProps {
  /** Every position in the tenant — the "show all positions" set for matrix / dotted-line cases. */
  positions: EmployeePosition[];
  /** The position being edited; it is never offered as its own reports-to. */
  excludeId?: string;
  staffLevels: StaffLevelListItem[];
  /** Defined in Payroll and mirrored into HR — read-only here. */
  salaryGrades: SalaryGrade[];
  skills: Skill[];
  /** Active benefit policies an entitlement can point at. */
  benefitPolicies: BenefitPolicySummary[];
  /**
   * The named sets on offer (round 2, lane C3). Attaching one is equivalent to attaching every
   * item in it, so the individual pickers below exclude whatever an attached set already provides.
   */
  benefitGroups?: NamedSetOption[];
  skillSets?: NamedSetOption[];
  certificationSets?: NamedSetOption[];
  /**
   * Currencies FINANCE holds, for the guarantor requirement.
   * ⚠ A prop rather than a fetch, like every other list here: this form is presentational and its
   * two callers already own their data-loading.
   */
  currencies?: { code: string; name: string }[];
  defaultValues: EmployeePositionFormValues;
  onSubmit: (values: EmployeePositionFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function EmployeePositionForm({
  positions,
  excludeId,
  staffLevels,
  salaryGrades,
  skills,
  benefitPolicies,
  benefitGroups = [],
  skillSets = [],
  certificationSets = [],
  currencies,
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
}: EmployeePositionFormProps) {
  const form = useForm<EmployeePositionFormValues>({
    resolver: zodResolver(employeePositionSchema) as any,
    defaultValues,
  });

  // Round 2, lane C3 — what the attached sets already provide, recomputed as they are attached.
  const attachedBenefitGroupIds = form.watch('benefitGroupIds') ?? [];
  const attachedSkillSetIds = form.watch('skillSetIds') ?? [];
  const attachedCertificationSetIds = form.watch('certificationSetIds') ?? [];
  const benefitCoverage = coverageOf(benefitGroups, attachedBenefitGroupIds);
  const skillCoverage = coverageOf(skillSets, attachedSkillSetIds);
  const certificationCoverage = coverageOf(certificationSets, attachedCertificationSetIds);

  /** Attach or detach one set, keeping the field a plain id array. */
  const toggleSet = (
    field: 'benefitGroupIds' | 'skillSetIds' | 'certificationSetIds',
    setId: string,
  ) => {
    const current: string[] = form.getValues(field) ?? [];
    const next = current.includes(setId) ? current.filter((x) => x !== setId) : [...current, setId];
    form.setValue(field, next, { shouldDirty: true, shouldValidate: true });
  };

  const unitId = form.watch('organizationUnitId');
  const reportsTo = form.watch('reportsToPositionId') || NONE;
  const [showAllPositions, setShowAllPositions] = useState(false);

  // Reports-to, narrowed to the chosen unit and everything above it (round 2, C1 / § 6.1.4). The
  // server walks the parent chain; a matrix or dotted-line case widens to the whole tenant.
  const { data: scopedPositions = [], isLoading: scopedLoading } = useQuery({
    queryKey: ['hr', 'employee-positions', 'organization-unit', unitId, 'ancestors'],
    queryFn: () => employeePositionService.getByOrganizationUnit(unitId, true),
    enabled: !!unitId,
    staleTime: 60 * 1000,
  });

  const reportsToOptions = useMemo(() => {
    const source = showAllPositions ? positions : scopedPositions;
    const current = form.getValues('reportsToPositionId');
    const list = source.filter((p) => p.id !== excludeId && (p.isActive || p.id === current));
    // The stored value is always offered, even when it sits outside the unit's ancestry — an edit
    // form must show what is stored, and hiding it would silently clear the reporting line on save.
    if (current && !list.some((p) => p.id === current)) {
      const stored = positions.find((p) => p.id === current);
      if (stored) list.push(stored);
    }
    return list.sort(
      (a, b) => a.organizationUnitName.localeCompare(b.organizationUnitName) || a.title.localeCompare(b.title),
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [showAllPositions, positions, scopedPositions, excludeId, reportsTo]);
  const workMode = form.watch('workMode');
  const requiresCertification = form.watch('requiresCertification');
  const requiresGuarantor = form.watch('requiresGuarantor');
  // The check sets a post can start an offer from. HR-only setup data; an empty list simply means
  // the picker offers only "Use the organisation default", which is a legitimate state.
  const checkTemplates = useQuery({
    queryKey: ['hr', 'pre-employment-check-templates'],
    queryFn: () => preEmploymentCheckTemplateService.getAll(),
  });

  const requiresLicense = form.watch('requiresLicense');
  const isActive = form.watch('isActive');
  const staffLevelValue = form.watch('staffLevelId') || NONE;
  const salaryGradeValue = form.watch('salaryGradeId') || NONE;

  const { fields: skillFields, append: appendSkill, remove: removeSkill } = useFieldArray({
    control: form.control,
    name: 'skillRequirements',
  });

  const {
    fields: benefitFields,
    append: appendBenefit,
    remove: removeBenefit,
  } = useFieldArray({ control: form.control, name: 'positionBenefits' });

  const {
    fields: certificationFields,
    append: appendCertification,
    remove: removeCertification,
  } = useFieldArray({ control: form.control, name: 'certificationRequirements' });
  const chosenCertificationIds = form
    .watch('certificationRequirements')
    .map((r) => r.certificationId)
    .filter(Boolean);
  const wantsCertifications = requiresCertification || requiresLicense;
  const [certificationRuleError, setCertificationRuleError] = useState<string | null>(null);

  // The server refuses a switched-on position that names nothing; saying it here saves the trip.
  const submit = form.handleSubmit(async (values) => {
    if ((values.requiresCertification || values.requiresLicense) && values.certificationRequirements.length === 0) {
      setCertificationRuleError(
        'This position requires a certification or licence, so say which one — add at least one required credential, or turn the switch off.',
      );
      return;
    }
    setCertificationRuleError(null);
    await onSubmit(values);
  });

  return (
    <Card>
      <form onSubmit={submit}>
        <CardHeader>
          <CardTitle>Position Details</CardTitle>
          <CardDescription>
            Define a job position within an organization unit.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="title">Title</Label>
              <Input id="title" placeholder="Accountant" {...form.register('title')} />
              {form.formState.errors.title && (
                <p className="text-sm text-red-500">{form.formState.errors.title.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="code">Code</Label>
              <Input id="code" placeholder="POS-ACC" {...form.register('code')} />
              {form.formState.errors.code && (
                <p className="text-sm text-red-500">{form.formState.errors.code.message}</p>
              )}
            </div>
          </div>

          {/*
            The level → unit cascade this form always had, now the shared picker (demo feedback
            round 2, O-6). The position stores BOTH ids, so the level the user picks is captured
            too; on edit the picker derives the level from the stored unit.
          */}
          <OrganizationUnitPicker
            idPrefix="position-unit"
            value={unitId || ''}
            onChange={(id, unit) => {
              form.setValue('organizationUnitId', id, { shouldValidate: true, shouldDirty: true });
              if (unit) form.setValue('organizationLevelId', unit.organizationLevelId, { shouldValidate: true });
            }}
            onLevelChange={(levelId) =>
              form.setValue('organizationLevelId', levelId, { shouldValidate: true, shouldDirty: true })
            }
            levelLabel="Organization Level"
            unitLabel="Organization Unit"
            error={
              (form.formState.errors.organizationUnitId?.message as string | undefined) ??
              (form.formState.errors.organizationLevelId?.message as string | undefined)
            }
          />

          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label htmlFor="reportsToPositionId">Reports To</Label>
              <Select
                value={reportsTo}
                disabled={!unitId && !showAllPositions}
                onValueChange={(value) =>
                  form.setValue('reportsToPositionId', value === NONE ? '' : value)
                }
              >
                <SelectTrigger id="reportsToPositionId">
                  <SelectValue
                    placeholder={
                      !unitId && !showAllPositions
                        ? 'Choose the unit first'
                        : scopedLoading && !showAllPositions
                          ? 'Loading…'
                          : 'None'
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {reportsToOptions.length === 0 && (unitId || showAllPositions) && !scopedLoading && (
                    <div className="px-2 py-1.5 text-sm text-muted-foreground">
                      {showAllPositions
                        ? 'No other positions.'
                        : 'No positions in this unit or above it — show all positions to pick one elsewhere.'}
                    </div>
                  )}
                  {reportsToOptions.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.title} · {p.organizationUnitName}
                      {p.code ? ` · ${p.code}` : ''}
                      {p.isActive ? '' : ' (inactive)'}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <label className="flex items-center gap-2 text-xs text-muted-foreground">
                <input
                  type="checkbox"
                  className="h-3.5 w-3.5"
                  checked={showAllPositions}
                  onChange={(e) => setShowAllPositions(e.target.checked)}
                />
                Show all positions (matrix or dotted-line reporting)
              </label>
              <p className="text-xs text-muted-foreground">
                {showAllPositions
                  ? 'Every position in the organisation.'
                  : 'Positions in the chosen unit and the units above it.'}
              </p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="staffLevelId">Staff Level</Label>
              <Select
                value={staffLevelValue}
                onValueChange={(value) =>
                  form.setValue('staffLevelId', value === NONE ? '' : value)
                }
              >
                <SelectTrigger id="staffLevelId">
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {staffLevels.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="salaryGradeId">Salary Grade</Label>
              <Select
                value={salaryGradeValue}
                onValueChange={(value) =>
                  form.setValue('salaryGradeId', value === NONE ? '' : value)
                }
              >
                <SelectTrigger id="salaryGradeId">
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {salaryGrades.map((g) => (
                    <SelectItem key={g.id} value={g.id}>
                      {g.code} — {g.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Grades are defined in Payroll and mirrored here.
              </p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="workMode">Work Mode</Label>
              <Select
                value={workMode}
                onValueChange={(value) => form.setValue('workMode', value as typeof workModes[number])}
              >
                <SelectTrigger id="workMode">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="OnSite">On-site</SelectItem>
                  <SelectItem value="Remote">Remote</SelectItem>
                  <SelectItem value="Hybrid">Hybrid</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label htmlFor="level">Rank Level</Label>
              <Input id="level" type="number" min={1} {...form.register('level')} />
              {form.formState.errors.level && (
                <p className="text-sm text-red-500">{form.formState.errors.level.message}</p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="expectedHeadcount">Expected Headcount</Label>
              <Input id="expectedHeadcount" type="number" min={1} {...form.register('expectedHeadcount')} />
              {form.formState.errors.expectedHeadcount && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.expectedHeadcount.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="minimumExperienceYears">Min. Experience (yrs)</Label>
              <Input
                id="minimumExperienceYears"
                type="number"
                min={0}
                {...form.register('minimumExperienceYears')}
              />
              {form.formState.errors.minimumExperienceYears && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.minimumExperienceYears.message}
                </p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-4 gap-4">
            <div className="space-y-2">
              <Label htmlFor="minimumAge">Min. Age</Label>
              <Input id="minimumAge" type="number" min={0} {...form.register('minimumAge')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="maximumAge">Max. Age</Label>
              <Input id="maximumAge" type="number" min={0} {...form.register('maximumAge')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="probationPeriodMonths">Probation (mo)</Label>
              <Input
                id="probationPeriodMonths"
                type="number"
                min={0}
                {...form.register('probationPeriodMonths')}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="noticePeriodMonths">Notice (mo)</Label>
              <Input
                id="noticePeriodMonths"
                type="number"
                min={0}
                {...form.register('noticePeriodMonths')}
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea
              id="description"
              placeholder="Optional description"
              rows={3}
              {...form.register('description')}
            />
          </div>

          {wantsCertifications && (
            <div className="space-y-3 rounded-md border p-4">
              <SetAttachPanel
                label="Certification sets"
                hint="Attach a set and the post requires every credential in it — the regulator's bundle, maintained in one place."
                sets={certificationSets}
                attachedIds={attachedCertificationSetIds}
                memberNoun="credential"
                onToggle={(id) => toggleSet('certificationSetIds', id)}
              />
              <div className="flex items-center justify-between">
                <div>
                  <h4 className="text-sm font-semibold">Required certifications and licences</h4>
                  <p className="text-xs text-muted-foreground">
                    The switches say the post needs one; these rows say which. Pick the certifying body,
                    then the credential it issues.
                  </p>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => appendCertification({ certificationId: '', isMandatory: true, notes: '' })}
                >
                  <Plus className="mr-2 h-4 w-4" /> Add credential
                </Button>
              </div>

              {certificationFields.length === 0 && (
                <p className="text-sm text-destructive">Say which one — add at least one required credential.</p>
              )}

              {certificationFields.map((row, index) => (
                <div key={row.id} className="space-y-3 rounded-md border bg-muted/30 p-3">
                  <CertificationPicker
                    idPrefix={`position-certification-${index}`}
                    value={form.watch(`certificationRequirements.${index}.certificationId`)}
                    onChange={(id) =>
                      form.setValue(`certificationRequirements.${index}.certificationId`, id, {
                        shouldValidate: true,
                        shouldDirty: true,
                      })
                    }
                    // Lane C3: what a set provides is excluded as well as what another row holds.
                    excludeIds={[...chosenCertificationIds, ...certificationCoverage.keys()].filter(
                      (id) => id !== form.watch(`certificationRequirements.${index}.certificationId`),
                    )}
                    error={
                      form.formState.errors.certificationRequirements?.[index]?.certificationId?.message as
                        | string
                        | undefined
                    }
                  />
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-[auto_1fr_auto] sm:items-center">
                    <label className="flex items-center gap-2 text-sm">
                      <Switch
                        checked={form.watch(`certificationRequirements.${index}.isMandatory`)}
                        onCheckedChange={(v) => form.setValue(`certificationRequirements.${index}.isMandatory`, v)}
                      />
                      Mandatory
                    </label>
                    <Input
                      placeholder="Notes (optional)"
                      {...form.register(`certificationRequirements.${index}.notes`)}
                    />
                    <Button type="button" variant="ghost" size="sm" onClick={() => removeCertification(index)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              ))}
              {certificationRuleError && <p className="text-sm text-red-500">{certificationRuleError}</p>}
            </div>
          )}

          <div className="space-y-3 rounded-md border p-4">
            <SetAttachPanel
              label="Skill sets"
              hint="Attach a set and the post requires everything in it. Anything a set provides cannot also be listed individually below."
              sets={skillSets}
              attachedIds={attachedSkillSetIds}
              memberNoun="skill"
              onToggle={(id) => toggleSet('skillSetIds', id)}
            />
            <div className="flex items-center justify-between">
              <div>
                <h4 className="text-sm font-semibold">Skill Requirements</h4>
                <p className="text-xs text-muted-foreground">Skills expected for this position.</p>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() =>
                  appendSkill({ skillId: '', requiredLevel: 'Beginner', isRequired: true, priority: 1 })
                }
              >
                <Plus className="mr-2 h-4 w-4" /> Add Skill
              </Button>
            </div>

            {skillFields.length === 0 ? (
              <p className="text-sm text-muted-foreground">No skill requirements added.</p>
            ) : (
              <div className="space-y-3">
                {skillFields.map((field, index) => (
                  <div
                    key={field.id}
                    className="grid grid-cols-1 gap-3 rounded-md border p-3 sm:grid-cols-[1fr_150px_110px_120px_auto] sm:items-end"
                  >
                    <div className="space-y-1">
                      <Label className="text-xs">Skill</Label>
                      <Select
                        value={form.watch(`skillRequirements.${index}.skillId`) || undefined}
                        onValueChange={(v) =>
                          form.setValue(`skillRequirements.${index}.skillId`, v, { shouldValidate: true })
                        }
                      >
                        <SelectTrigger>
                          <SelectValue placeholder="Select a skill" />
                        </SelectTrigger>
                        <SelectContent>
                          {skills
                            .filter((s) => {
                              // Already on another row (X-1 — the server used to keep the first
                              // silently), or already provided by an attached set (lane C3's rule,
                              // which the server now refuses). The row's own value always stays.
                              const own = form.watch(`skillRequirements.${index}.skillId`);
                              if (s.id === own) return true;
                              const onAnotherRow = (form.watch('skillRequirements') ?? []).some(
                                (r, i) => i !== index && r.skillId === s.id,
                              );
                              return !onAnotherRow && !skillCoverage.has(s.id);
                            })
                            .map((s) => (
                              <SelectItem key={s.id} value={s.id}>
                                {s.name}
                                {s.category ? ` · ${s.category}` : ''}
                              </SelectItem>
                            ))}
                        </SelectContent>
                      </Select>
                      {form.formState.errors.skillRequirements?.[index]?.skillId && (
                        <p className="text-xs text-red-500">
                          {form.formState.errors.skillRequirements[index]?.skillId?.message}
                        </p>
                      )}
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs">Required Level</Label>
                      <Select
                        value={form.watch(`skillRequirements.${index}.requiredLevel`)}
                        onValueChange={(v) =>
                          form.setValue(
                            `skillRequirements.${index}.requiredLevel`,
                            v as (typeof skillLevels)[number],
                          )
                        }
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {SKILL_LEVEL_OPTIONS.map((o) => (
                            <SelectItem key={o.value} value={o.value}>
                              {o.label}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1">
                      <Label className="text-xs">Priority</Label>
                      <Input type="number" min={1} {...form.register(`skillRequirements.${index}.priority`)} />
                    </div>
                    <div className="flex items-center justify-between gap-2 rounded-md border px-3 py-2">
                      <Label className="text-xs">Required</Label>
                      <Switch
                        checked={form.watch(`skillRequirements.${index}.isRequired`)}
                        onCheckedChange={(v) =>
                          form.setValue(`skillRequirements.${index}.isRequired`, v)
                        }
                      />
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      onClick={() => removeSkill(index)}
                      aria-label="Remove skill"
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="space-y-3 rounded-md border p-4">
            <SetAttachPanel
              label="Benefit groups"
              hint="Attach a group and the post carries every benefit in it. A benefit needing its own amount is listed individually instead — not both."
              sets={benefitGroups}
              attachedIds={attachedBenefitGroupIds}
              memberNoun="benefit"
              onToggle={(id) => toggleSet('benefitGroupIds', id)}
            />
            <div className="flex items-center justify-between">
              <div>
                <h4 className="text-sm font-semibold">Benefit Entitlements</h4>
                <p className="text-xs text-muted-foreground">
                  Policies everyone holding this position is entitled to. Reconciling an
                  employee&rsquo;s benefits turns these into actual enrolments.
                </p>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => appendBenefit({ policyId: '', expiryDate: '', positionAmount: '' })}
              >
                <Plus className="mr-2 h-4 w-4" /> Add Entitlement
              </Button>
            </div>

            {benefitFields.length === 0 ? (
              <p className="text-sm text-muted-foreground">No benefit entitlements added.</p>
            ) : (
              <div className="space-y-3">
                {benefitFields.map((field, index) => {
                  // A policy already chosen on another row cannot be chosen again: the server
                  // keys entitlements on (position, policy) and would silently keep only one.
                  const takenIds = new Set(
                    form
                      .watch('positionBenefits')
                      .map((b, i) => (i === index ? '' : b.policyId))
                      .filter(Boolean),
                  );

                  return (
                    <div
                      key={field.id}
                      className="grid grid-cols-1 gap-3 rounded-md border p-3 sm:grid-cols-[1fr_150px_150px_auto] sm:items-end"
                    >
                      <div className="space-y-1">
                        <Label className="text-xs">Benefit policy</Label>
                        <Select
                          value={form.watch(`positionBenefits.${index}.policyId`) || undefined}
                          onValueChange={(v) =>
                            form.setValue(`positionBenefits.${index}.policyId`, v, {
                              shouldValidate: true,
                            })
                          }
                        >
                          <SelectTrigger>
                            <SelectValue
                              placeholder={
                                benefitPolicies.length ? 'Select a policy' : 'No active policies'
                              }
                            />
                          </SelectTrigger>
                          <SelectContent>
                            {benefitPolicies
                              // ⚠ Lane C3 widened this: a policy an attached GROUP already provides
                              // is excluded too, because the server refuses to hold it both ways.
                              .filter((p) => !takenIds.has(p.id) && !benefitCoverage.has(p.id))
                              .map((p) => (
                                <SelectItem key={p.id} value={p.id}>
                                  {p.policyName}
                                  {p.policyCode ? ` · ${p.policyCode}` : ''}
                                  {p.isMandatory ? ' · mandatory' : ''}
                                </SelectItem>
                              ))}
                          </SelectContent>
                        </Select>
                        {form.formState.errors.positionBenefits?.[index]?.policyId && (
                          <p className="text-xs text-red-500">
                            {form.formState.errors.positionBenefits[index]?.policyId?.message}
                          </p>
                        )}
                      </div>
                      <div className="space-y-1">
                        <Label className="text-xs">Amount</Label>
                        <Input
                          type="number"
                          step="0.01"
                          min={0}
                          placeholder="Policy default"
                          {...form.register(`positionBenefits.${index}.positionAmount`)}
                        />
                      </div>
                      <div className="space-y-1">
                        <Label className="text-xs">Expires</Label>
                        <Input
                          type="date"
                          {...form.register(`positionBenefits.${index}.expiryDate`)}
                        />
                      </div>
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        onClick={() => removeBenefit(index)}
                        aria-label="Remove entitlement"
                      >
                        <Trash2 className="h-4 w-4 text-destructive" />
                      </Button>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div className="space-y-4 rounded-md border p-4">
            <div className="flex items-center justify-between">
              <Label htmlFor="requiresCertification">Requires Certification</Label>
              <Switch
                id="requiresCertification"
                checked={requiresCertification}
                onCheckedChange={(v) => form.setValue('requiresCertification', v)}
              />
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="requiresGuarantor">Requires Guarantor</Label>
              <Switch
                id="requiresGuarantor"
                checked={requiresGuarantor}
                onCheckedChange={(v) => form.setValue('requiresGuarantor', v)}
              />
            </div>
            {/* ⚠ The flag alone could demand a guarantor and never say for how much, so "is this
                cashier properly guaranteed?" was a question the system could pose and not answer.
                Leaving the amount blank is a legitimate answer — "a guarantor, sum unspecified" —
                and the compliance read then tests only that one exists. */}
            {requiresGuarantor && (
              <div className="grid grid-cols-2 gap-3 rounded-md border p-3">
                <div className="space-y-2">
                  <Label htmlFor="requiredGuarantorAmount">Amount required</Label>
                  <Input
                    id="requiredGuarantorAmount"
                    type="number"
                    step="0.01"
                    placeholder="Leave blank for no set amount"
                    {...form.register('requiredGuarantorAmount')}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="requiredGuarantorCurrencyCode">Currency</Label>
                  <CurrencyPicker
                    id="requiredGuarantorCurrencyCode"
                    value={form.watch('requiredGuarantorCurrencyCode') || ''}
                    onChange={(v) => form.setValue('requiredGuarantorCurrencyCode', v)}
                    options={currencies}
                    allowEmpty
                    emptyLabel="HR default"
                    placeholder="HR default"
                  />
                </div>
                <p className="col-span-2 text-xs text-muted-foreground">
                  Each holder&apos;s Documents tab shows whether their guarantors meet this. Sureties
                  stated in another currency are counted and shown separately, never converted.
                </p>
              </div>
            )}
            <div className="flex items-center justify-between">
              <Label htmlFor="requiresLicense">Requires License</Label>
              <Switch
                id="requiresLicense"
                checked={requiresLicense}
                onCheckedChange={(v) => form.setValue('requiresLicense', v)}
              />
            </div>
            {/* Round 4, lane H1. The check set an offer for this post starts from. ⚠ Leaving it
                unset does not mean "no checks": the offer falls back to the tenant’s SINGLE active
                template, and seeds nothing when there are several, because an offer letter tells a
                candidate what to produce and guessing between templates would commit the company
                to checks nobody chose. */}
            <div className="space-y-1.5">
              <Label htmlFor="preEmploymentCheckTemplateId">Pre-employment checks</Label>
              <Select
                value={form.watch('preEmploymentCheckTemplateId') ?? 'none'}
                onValueChange={(v) =>
                  form.setValue('preEmploymentCheckTemplateId', v === 'none' ? null : v)
                }
              >
                <SelectTrigger id="preEmploymentCheckTemplateId">
                  <SelectValue placeholder="Use the organisation default" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Use the organisation default</SelectItem>
                  {(checkTemplates.data ?? [])
                    .filter((t) => t.isActive)
                    .map((t) => (
                      <SelectItem key={t.id} value={t.id}>
                        {t.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                Seeded onto an offer when it is raised, and printed on the offer letter as what the
                candidate must produce.
              </p>
            </div>
            <div className="flex items-center justify-between">
              <Label htmlFor="isActive">Active</Label>
              <Switch
                id="isActive"
                checked={isActive}
                onCheckedChange={(v) => form.setValue('isActive', v)}
              />
            </div>
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
