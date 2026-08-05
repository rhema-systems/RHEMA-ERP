'use client';

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
import type { OrganizationLevel, OrganizationUnitSummary } from '@/types/hr/organization';
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
  requiresLicense: z.boolean(),
  isActive: z.boolean(),
  skillRequirements: z.array(
    z.object({
      skillId: z.string().min(1, 'Select a skill'),
      requiredLevel: z.enum(skillLevels),
      isRequired: z.boolean(),
      priority: z.coerce.number().int().min(1),
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
});

export type EmployeePositionFormValues = z.infer<typeof employeePositionSchema>;

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
  requiresLicense: false,
  isActive: true,
  skillRequirements: [],
  positionBenefits: [],
};

interface EmployeePositionFormProps {
  levels: OrganizationLevel[];
  units: OrganizationUnitSummary[];
  positions: EmployeePosition[];
  staffLevels: StaffLevelListItem[];
  /** Defined in Payroll and mirrored into HR — read-only here. */
  salaryGrades: SalaryGrade[];
  skills: Skill[];
  /** Active benefit policies an entitlement can point at. */
  benefitPolicies: BenefitPolicySummary[];
  defaultValues: EmployeePositionFormValues;
  onSubmit: (values: EmployeePositionFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
}

export function EmployeePositionForm({
  levels,
  units,
  positions,
  staffLevels,
  salaryGrades,
  skills,
  benefitPolicies,
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

  const levelId = form.watch('organizationLevelId');
  const unitId = form.watch('organizationUnitId');
  const reportsTo = form.watch('reportsToPositionId') || NONE;
  const workMode = form.watch('workMode');
  const requiresCertification = form.watch('requiresCertification');
  const requiresGuarantor = form.watch('requiresGuarantor');
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

  // Cascading selection: units are filtered to the chosen level.
  const unitsForLevel = units.filter((u) => u.organizationLevelId === levelId);

  const handleLevelChange = (value: string) => {
    form.setValue('organizationLevelId', value, { shouldValidate: true });
    // Clear the unit if it no longer belongs to the newly-selected level.
    const stillValid = units.some((u) => u.id === unitId && u.organizationLevelId === value);
    if (!stillValid) {
      form.setValue('organizationUnitId', '', { shouldValidate: true });
    }
  };

  return (
    <Card>
      <form onSubmit={form.handleSubmit(onSubmit)}>
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

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="organizationLevelId">Organization Level</Label>
              <Select value={levelId || undefined} onValueChange={handleLevelChange}>
                <SelectTrigger id="organizationLevelId">
                  <SelectValue placeholder="Select a level" />
                </SelectTrigger>
                <SelectContent>
                  {levels.map((l) => (
                    <SelectItem key={l.id} value={l.id}>
                      {l.name} (L{l.levelNumber})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {form.formState.errors.organizationLevelId && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.organizationLevelId.message}
                </p>
              )}
            </div>
            <div className="space-y-2">
              <Label htmlFor="organizationUnitId">Organization Unit</Label>
              <Select
                value={unitId || undefined}
                disabled={!levelId}
                onValueChange={(value) =>
                  form.setValue('organizationUnitId', value, { shouldValidate: true })
                }
              >
                <SelectTrigger id="organizationUnitId">
                  <SelectValue placeholder={levelId ? 'Select a unit' : 'Select a level first'} />
                </SelectTrigger>
                <SelectContent>
                  {unitsForLevel.length === 0 ? (
                    <div className="px-2 py-1.5 text-sm text-muted-foreground">
                      No units at this level.
                    </div>
                  ) : (
                    unitsForLevel.map((u) => (
                      <SelectItem key={u.id} value={u.id}>
                        {u.name}
                      </SelectItem>
                    ))
                  )}
                </SelectContent>
              </Select>
              {form.formState.errors.organizationUnitId && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.organizationUnitId.message}
                </p>
              )}
            </div>
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label htmlFor="reportsToPositionId">Reports To</Label>
              <Select
                value={reportsTo}
                onValueChange={(value) =>
                  form.setValue('reportsToPositionId', value === NONE ? '' : value)
                }
              >
                <SelectTrigger id="reportsToPositionId">
                  <SelectValue placeholder="None" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>None</SelectItem>
                  {positions.map((p) => (
                    <SelectItem key={p.id} value={p.id}>
                      {p.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
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

          <div className="space-y-3 rounded-md border p-4">
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
                          {skills.map((s) => (
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
                              .filter((p) => !takenIds.has(p.id))
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
            <div className="flex items-center justify-between">
              <Label htmlFor="requiresLicense">Requires License</Label>
              <Switch
                id="requiresLicense"
                checked={requiresLicense}
                onCheckedChange={(v) => form.setValue('requiresLicense', v)}
              />
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
