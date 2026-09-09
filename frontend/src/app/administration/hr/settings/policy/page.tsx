'use client';

import { useEffect, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, ShieldAlert, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import {
  NumberField,
  TextField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { policySettingsService } from '@/services/hr/policy-settings.service';
import {
  ENFORCEMENT_MODES,
  SALARY_STRUCTURE_SOURCES,
  SALARY_STRUCTURE_TIERS,
  type SalaryStructureSource,
  type SalaryStructureTiers,
  FISCAL_YEAR_MONTHS,
  type BudgetEnforcementMode,
} from '@/types/hr/policy-settings';

/**
 * Company-wide HR policy settings.
 *
 * Grouped by **the area that reads each knob**, not by data type, because that is the only framing
 * in which the consequences of a change are visible. Eleven services read this record: probation
 * confirmation dates, separation notice and retirement, succession ranking, requisition budget
 * checks, the long-service sweep and every reminder lead time.
 *
 * ⚠ Write is gated to SuperAdmin / TenantAdmin. HR can read this screen and will get a 403 on save
 * — three of these are trust boundaries (FR-HR-092, FR-HR-136), not preferences.
 */
const schema = z
  .object({
    compulsoryRetirementAge: z.coerce.number().int().min(40).max(100),
    voluntaryRetirementAge: z.coerce.number().int().min(40).max(100),
    useGenderSpecificRetirementAge: z.boolean(),
    maleRetirementAge: z.union([z.coerce.number().int().min(40).max(100), z.literal('')]).optional(),
    femaleRetirementAge: z.union([z.coerce.number().int().min(40).max(100), z.literal('')]).optional(),

    defaultProbationMonths: z.coerce.number().int().min(0).max(60),
    defaultResignationNoticeDays: z.coerce.number().int().min(0).max(365),
    defaultTerminationNoticeDays: z.coerce.number().int().min(0).max(365),
    proceduralAbsenceDays: z.coerce.number().int().min(0).max(365),

    vacancyAlertLeadDays: z.coerce.number().int().min(0).max(3650),
    reviewDueLeadDays: z.coerce.number().int().min(0).max(3650),
    contractExpiryLeadDays: z.coerce.number().int().min(0).max(3650),
    probationEndLeadDays: z.coerce.number().int().min(0).max(3650),
    retirementCountdownLeadDays: z.coerce.number().int().min(0).max(3650),

    longServiceMilestoneYears: z
      .string()
      .max(200)
      .refine(
        (v) =>
          v.trim() === '' ||
          v
            .split(',')
            .map((p) => p.trim())
            .filter(Boolean)
            .every((p) => /^\d+$/.test(p) && +p >= 1 && +p <= 100),
        'Whole years between 1 and 100, comma separated — for example 5,10,15,20,25.',
      ),
    defaultCurrencyCode: z
      .string()
      .trim()
      .regex(/^[A-Za-z]{3}$/, 'A three-letter ISO 4217 code, for example GHS.'),
    fiscalYearStartMonth: z.coerce.number().int().min(1).max(12),
    minimumWorkingAge: z.coerce.number().int().min(10).max(30),

    writtenQueryHours: z.coerce.number().int().min(1).max(720),
    queryResponseWindowHours: z.coerce.number().int().min(1).max(720),
    investigationDays: z.coerce.number().int().min(1).max(365),
    disciplineBacklogHorizonDays: z.coerce.number().int().min(1).max(3650),
    settlementDaysPerYear: z.coerce.number().int().min(1).max(366),
    attendanceRateIncludesApprovedLeave: z.boolean(),

    budgetEnforcementMode: z.string(),
    establishmentEnforcementMode: z.string(),
    salaryStructureTiers: z.string(),
    salaryStructureSource: z.string(),

    fitWeightPerformance: z.coerce.number().int().min(0).max(100),
    fitWeightCompetency: z.coerce.number().int().min(0).max(100),
    fitWeightPotential: z.coerce.number().int().min(0).max(100),
    fitWeightTenure: z.coerce.number().int().min(0).max(100),

    successionPlanNumberPrefix: z.string().trim().min(1).max(10),
  })
  // Mirrors the server's own rules, so the form refuses before the round trip rather than after.
  .refine((v) => v.voluntaryRetirementAge <= v.compulsoryRetirementAge, {
    path: ['voluntaryRetirementAge'],
    message: 'Voluntary retirement age cannot exceed the compulsory retirement age.',
  })
  .refine(
    (v) =>
      !v.useGenderSpecificRetirementAge ||
      v.maleRetirementAge !== '' ||
      v.femaleRetirementAge !== '',
    {
      path: ['maleRetirementAge'],
      message: 'Set at least one gender-specific age, or turn gender-specific retirement off.',
    },
  )
  .refine(
    (v) =>
      v.fitWeightPerformance + v.fitWeightCompetency + v.fitWeightPotential + v.fitWeightTenure > 0,
    {
      path: ['fitWeightPerformance'],
      message:
        'At least one weight must be above zero, or candidate ranking silently uses the built-in weights.',
    },
  );

type SettingsForm = z.input<typeof schema>;

/**
 * The two gender-specific ages are genuinely optional: blank means "fall back to the compulsory
 * age", which is not the same as zero. The zod union that expresses that widens the form's input
 * type to `unknown`, so the narrowing happens here rather than being asserted away at the call site.
 */
const orNullNumber = (v: unknown): number | null => {
  if (v === '' || v === null || v === undefined) return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
};

export default function PolicySettingsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'policy-settings'],
    queryFn: () => policySettingsService.get(),
  });

  const form = useForm<SettingsForm>({ resolver: zodResolver(schema) as any });

  useEffect(() => {
    if (!data) return;
    form.reset({
      compulsoryRetirementAge: data.compulsoryRetirementAge,
      voluntaryRetirementAge: data.voluntaryRetirementAge,
      useGenderSpecificRetirementAge: data.useGenderSpecificRetirementAge,
      maleRetirementAge: data.maleRetirementAge ?? '',
      femaleRetirementAge: data.femaleRetirementAge ?? '',

      defaultProbationMonths: data.defaultProbationMonths,
      defaultResignationNoticeDays: data.defaultResignationNoticeDays,
      defaultTerminationNoticeDays: data.defaultTerminationNoticeDays,
      proceduralAbsenceDays: data.proceduralAbsenceDays,

      vacancyAlertLeadDays: data.vacancyAlertLeadDays,
      reviewDueLeadDays: data.reviewDueLeadDays,
      contractExpiryLeadDays: data.contractExpiryLeadDays,
      probationEndLeadDays: data.probationEndLeadDays,
      retirementCountdownLeadDays: data.retirementCountdownLeadDays,

      longServiceMilestoneYears: data.longServiceMilestoneYears ?? '',
      defaultCurrencyCode: data.defaultCurrencyCode ?? 'GHS',
      fiscalYearStartMonth: data.fiscalYearStartMonth,
      minimumWorkingAge: data.minimumWorkingAge,

      budgetEnforcementMode: data.budgetEnforcementMode,
      establishmentEnforcementMode: data.establishmentEnforcementMode,
      salaryStructureTiers: data.salaryStructureTiers ?? 'GradeAndNotch',
      salaryStructureSource: data.salaryStructureSource ?? 'Payroll',

      fitWeightPerformance: data.fitWeightPerformance,
      fitWeightCompetency: data.fitWeightCompetency,
      fitWeightPotential: data.fitWeightPotential,
      fitWeightTenure: data.fitWeightTenure,

      successionPlanNumberPrefix: data.successionPlanNumberPrefix ?? 'SP',

      writtenQueryHours: data.writtenQueryHours,
      queryResponseWindowHours: data.queryResponseWindowHours,
      investigationDays: data.investigationDays,
      disciplineBacklogHorizonDays: data.disciplineBacklogHorizonDays,
      settlementDaysPerYear: data.settlementDaysPerYear,
      attendanceRateIncludesApprovedLeave: data.attendanceRateIncludesApprovedLeave,
    });
  }, [data, form]);

  const genderSpecific = !!form.watch('useGenderSpecificRetirementAge');
  const proceduralDays = Number(form.watch('proceduralAbsenceDays') ?? 0);

  // The weights are relative, so what actually matters is their share. Show it.
  const weights = [
    Number(form.watch('fitWeightPerformance') ?? 0),
    Number(form.watch('fitWeightCompetency') ?? 0),
    Number(form.watch('fitWeightPotential') ?? 0),
    Number(form.watch('fitWeightTenure') ?? 0),
  ];
  const weightShares = useMemo(() => {
    const total = weights.reduce((a, b) => a + b, 0);
    if (total <= 0) return null;
    const labels = ['Performance', 'Competency', 'Potential', 'Tenure'];
    return labels.map((label, i) => `${label} ${Math.round((weights[i] / total) * 100)}%`);
  }, weights);

  const save = useMutation({
    mutationFn: (v: SettingsForm) =>
      policySettingsService.update({
        compulsoryRetirementAge: Number(v.compulsoryRetirementAge),
        voluntaryRetirementAge: Number(v.voluntaryRetirementAge),
        useGenderSpecificRetirementAge: v.useGenderSpecificRetirementAge,
        maleRetirementAge: orNullNumber(v.maleRetirementAge),
        femaleRetirementAge: orNullNumber(v.femaleRetirementAge),

        defaultProbationMonths: Number(v.defaultProbationMonths),
        defaultResignationNoticeDays: Number(v.defaultResignationNoticeDays),
        defaultTerminationNoticeDays: Number(v.defaultTerminationNoticeDays),
        proceduralAbsenceDays: Number(v.proceduralAbsenceDays),

        vacancyAlertLeadDays: Number(v.vacancyAlertLeadDays),
        reviewDueLeadDays: Number(v.reviewDueLeadDays),
        contractExpiryLeadDays: Number(v.contractExpiryLeadDays),
        probationEndLeadDays: Number(v.probationEndLeadDays),
        retirementCountdownLeadDays: Number(v.retirementCountdownLeadDays),

        longServiceMilestoneYears: v.longServiceMilestoneYears.trim(),
        defaultCurrencyCode: v.defaultCurrencyCode.trim().toUpperCase(),
        fiscalYearStartMonth: Number(v.fiscalYearStartMonth),
        minimumWorkingAge: Number(v.minimumWorkingAge),

        budgetEnforcementMode: v.budgetEnforcementMode as BudgetEnforcementMode,
        establishmentEnforcementMode: v.establishmentEnforcementMode as BudgetEnforcementMode,
        salaryStructureTiers: v.salaryStructureTiers as SalaryStructureTiers,
        salaryStructureSource: v.salaryStructureSource as SalaryStructureSource,

        fitWeightPerformance: Number(v.fitWeightPerformance),
        fitWeightCompetency: Number(v.fitWeightCompetency),
        fitWeightPotential: Number(v.fitWeightPotential),
        fitWeightTenure: Number(v.fitWeightTenure),

        successionPlanNumberPrefix: v.successionPlanNumberPrefix.trim().toUpperCase(),

        writtenQueryHours: Number(v.writtenQueryHours),
        queryResponseWindowHours: Number(v.queryResponseWindowHours),
        investigationDays: Number(v.investigationDays),
        disciplineBacklogHorizonDays: Number(v.disciplineBacklogHorizonDays),
        settlementDaysPerYear: Number(v.settlementDaysPerYear),
        attendanceRateIncludesApprovedLeave: v.attendanceRateIncludesApprovedLeave,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'policy-settings'] });
      toast({
        title: 'Policy settings saved',
        description: 'Probation, separation, succession and the reminder sweeps use these immediately.',
      });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not save policy settings',
        description:
          e?.status === 403
            ? 'Changing these requires an administrator. HR can view them but not change them.'
            : e?.message ?? 'Unexpected error.',
      }),
  });

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="HR Policy Settings"
        description="Tenant-wide rules that probation, separation, succession, recruitment and every reminder sweep read."
        backHref="/administration/hr/settings"
      />

      <Alert>
        <ShieldAlert className="h-4 w-4" />
        <AlertTitle>These change behaviour elsewhere, immediately</AlertTitle>
        <AlertDescription>
          Eleven services read this record. There is no draft and no approval step — a saved value is
          in force on the next confirmation date, notice calculation, candidate ranking, requisition
          check and reminder run. Changing them requires an administrator.
        </AlertDescription>
      </Alert>

      <form onSubmit={form.handleSubmit((v) => save.mutate(v))} className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Retirement</CardTitle>
            <CardDescription>
              Read by separation (retirement dates and the retirement sweep) and by succession, which
              uses them to work out how many service years a candidate has left.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <NumberField form={form} name="compulsoryRetirementAge" label="Compulsory retirement age" required />
              <NumberField form={form} name="voluntaryRetirementAge" label="Earliest voluntary retirement age" required />
            </FieldRow>
            <SwitchField
              form={form}
              name="useGenderSpecificRetirementAge"
              label="Use gender-specific retirement ages"
              description="When off, the compulsory age applies to everyone. A blank age falls back to it."
            />
            {genderSpecific && (
              <FieldRow>
                <NumberField form={form} name="maleRetirementAge" label="Male retirement age" />
                <NumberField form={form} name="femaleRetirementAge" label="Female retirement age" />
              </FieldRow>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Probation &amp; notice</CardTitle>
            <CardDescription>
              The probation length drives confirmation dates and the on-probation flag. The notice
              periods are the defaults a resignation or termination is measured against.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <NumberField form={form} name="defaultProbationMonths" label="Default probation (months)" required />
              <NumberField form={form} name="defaultResignationNoticeDays" label="Resignation notice (days)" required />
            </FieldRow>
            <NumberField form={form} name="defaultTerminationNoticeDays" label="Termination notice (days)" required />
          </CardContent>
        </Card>

        <Card className="border-amber-500/40">
          <CardHeader>
            <CardTitle>Termination authority — FR-HR-092</CardTitle>
            <CardDescription>
              The Managing Director signs every termination <em>except</em> procedural ones, which HR
              approves. This number is the whole definition of &ldquo;procedural&rdquo;.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <NumberField
              form={form}
              name="proceduralAbsenceDays"
              label="Unauthorised absence before a termination counts as procedural (days)"
              required
            />
            {proceduralDays === 0 ? (
              <Alert>
                <TriangleAlert className="h-4 w-4" />
                <AlertTitle>Every termination will require the Managing Director</AlertTitle>
                <AlertDescription>
                  At zero there is no procedural exception at all — HR cannot approve any
                  termination on its own.
                </AlertDescription>
              </Alert>
            ) : (
              <p className="text-sm text-muted-foreground">
                An employee absent without authorisation for {proceduralDays} day
                {proceduralDays === 1 ? '' : 's'} or more can be terminated by HR without the
                Managing Director&rsquo;s signature. Raising this number narrows the exception;
                lowering it widens it.
              </p>
            )}
          </CardContent>
        </Card>

        <Card className="border-amber-500/40">
          <CardHeader>
            <CardTitle>Establishment &amp; budget enforcement — FR-HR-136</CardTitle>
            <CardDescription>
              Whether hiring beyond an approved number is stopped or merely flagged.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <SelectField
                form={form}
                name="budgetEnforcementMode"
                label="Manpower budget check"
                options={ENFORCEMENT_MODES}
                required
              />
              <SelectField
                form={form}
                name="establishmentEnforcementMode"
                label="Approved establishment check"
                options={ENFORCEMENT_MODES}
                required
              />
            </FieldRow>
            <p className="text-sm text-muted-foreground">
              The two default differently on purpose. The budget check reads numbers a department
              typed into a budget line, so it warns. The establishment check only ever fires for
              positions whose headcount went the whole way up the FR-HR-135 chain to Department Head,
              HR and the Managing Director — so it blocks, because warning about exceeding something
              three people authorised would make the authorisation pointless. A position nobody has
              established is never constrained, whatever these are set to.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Salary structure</CardTitle>
            <CardDescription>
              How many tiers the salary scale has, and who maintains it.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <SelectField
                form={form}
                name="salaryStructureTiers"
                label="Tiers"
                options={SALARY_STRUCTURE_TIERS}
                required
              />
              <SelectField
                form={form}
                name="salaryStructureSource"
                label="Maintained in"
                options={SALARY_STRUCTURE_SOURCES}
                required
              />
            </FieldRow>
            <p className="text-sm text-muted-foreground">
              Two-tier places a person on a grade and a notch; three-tier puts a level between them.
              The setting changes what the screens ask for and what a placement needs — the scale
              itself is stored the same way in both cases, with a two-tier grade carrying one
              implicit level nobody sees.
            </p>
            <p className="text-sm text-muted-foreground">
              <strong>Payroll has no level tier</strong>, so three-tier is refused while Payroll is the
              source. Switching back to two-tier is refused while any grade holds more than one
              level — which notches survive is a decision, not something to collapse silently.
              Moving the source from HR back to Payroll makes payroll&apos;s rows overwrite HR&apos;s
              on the next read wherever the grade codes match.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Alert lead times</CardTitle>
            <CardDescription>
              How far ahead each reminder sweep starts flagging. Every one of these is read by a
              scheduled sweep, so a change is visible on the next run.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <NumberField form={form} name="vacancyAlertLeadDays" label="Anticipated vacancy (days)" required />
              <NumberField form={form} name="reviewDueLeadDays" label="Review due (days)" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="contractExpiryLeadDays" label="Contract expiry (days)" required />
              <NumberField form={form} name="probationEndLeadDays" label="Probation end (days)" required />
            </FieldRow>
            <NumberField
              form={form}
              name="retirementCountdownLeadDays"
              label="Upcoming retirement (days)"
              required
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Disciplinary &amp; settlement clocks</CardTitle>
            <CardDescription>
              Figures the system had to assume because they are not in the specification. Every one
              starts at what the code did before, so nothing here changed when it became editable —
              but none of them is a confirmed answer from TDC yet.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <NumberField
                form={form}
                name="writtenQueryHours"
                label="Written query due within (hours)"
                required
              />
              <NumberField
                form={form}
                name="queryResponseWindowHours"
                label="Employee's answer window (hours)"
                required
              />
            </FieldRow>
            <p className="text-sm text-muted-foreground">
              FR-HR-177 sets 48 hours for issuing the query. The answer window is{' '}
              <strong>not in the specification at all</strong> — 72 hours is this system&apos;s
              assumption, and it is what makes the natural-justice gate workable. TDC may want it in
              working days, which needs the holiday calendar loaded first.
            </p>
            <FieldRow>
              <NumberField
                form={form}
                name="investigationDays"
                label="Investigation tracked within (days)"
                required
              />
              <NumberField
                form={form}
                name="disciplineBacklogHorizonDays"
                label="Reminders stop chasing after (days)"
                required
              />
            </FieldRow>
            <p className="text-sm text-muted-foreground">
              Only the chasing stops. A breach stays on the record and in every report — a reminder
              engine that shouts for ever trains people to ignore it.
            </p>

            {/* ⚠ The one setting on this page that changes what a person is paid. */}
            <div className="rounded-md border border-amber-300 bg-amber-50 p-4 dark:border-amber-900 dark:bg-amber-950/40">
              <NumberField
                form={form}
                name="settlementDaysPerYear"
                label="Final settlement — days per year"
                required
              />
              <p className="mt-2 text-sm">
                <strong>This one moves money.</strong> A daily rate is monthly pay × 12 ÷ this
                number: <strong>365</strong> for calendar days, <strong>360</strong> for thirty-day
                months, <strong>264</strong> for a 22-day working month. On TDC&apos;s own worked
                example the answers run from <strong>GHS 3,156.16</strong> to{' '}
                <strong>GHS 4,363.64</strong> — a 38% spread on the same facts.
              </p>
              <p className="mt-2 text-sm">
                Every settlement records which basis produced it, so changing this never rewrites
                one already computed. That makes an early settlement auditable; it does not make it
                right. <strong>Do not run real final settlements until TDC has confirmed the
                basis.</strong>
              </p>
            </div>

            <SwitchField
              form={form}
              name="attendanceRateIncludesApprovedLeave"
              label="Approved leave counts as an expected working day"
              description="On: leave sits in the attendance denominator, so a day on approved leave lowers the rate and DaysOnLeave shows why. Off: leave leaves the calculation entirely, as weekends and public holidays already do. Applies to today's rate, the trend and the chronic-absentee ranking together."
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Succession fit weights</CardTitle>
            <CardDescription>
              How succession ranks candidates. These are <strong>relative</strong> — they need not
              sum to 100, and a component with no data is skipped and the rest renormalised.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <NumberField form={form} name="fitWeightPerformance" label="Performance" required />
              <NumberField form={form} name="fitWeightCompetency" label="Competency" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="fitWeightPotential" label="Potential" required />
              <NumberField form={form} name="fitWeightTenure" label="Tenure" required />
            </FieldRow>
            {weightShares ? (
              <p className="text-sm text-muted-foreground">
                Effective share: {weightShares.join(' · ')}
              </p>
            ) : (
              <Alert variant="destructive">
                <TriangleAlert className="h-4 w-4" />
                <AlertTitle>All four weights are zero</AlertTitle>
                <AlertDescription>
                  Ranking would fall back to the built-in weights (35 / 30 / 20 / 15) while this
                  screen showed zeros. Set at least one above zero.
                </AlertDescription>
              </Alert>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Organisation-wide defaults</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <TextField form={form} name="defaultCurrencyCode" label="Default currency (ISO 4217)" required />
              <SelectField
                form={form}
                name="fiscalYearStartMonth"
                label="Fiscal year starts"
                options={FISCAL_YEAR_MONTHS}
                required
              />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="minimumWorkingAge" label="Minimum working age" required />
              <TextField
                form={form}
                name="successionPlanNumberPrefix"
                label="Succession plan number prefix"
                placeholder="SP"
                required
              />
            </FieldRow>
            <TextField
              form={form}
              name="longServiceMilestoneYears"
              label="Long-service milestone years"
              placeholder="5,10,15,20,25"
            />
            <p className="text-sm text-muted-foreground">
              The organisation&rsquo;s shared notion of service milestones, used by succession for
              service-years context. <strong>This does not govern long-service awards.</strong> That
              ladder lives in its own rows under Awards &amp; Recognition, deliberately, so that a
              tenant which never opened this page does not silently get these years instead of the
              ladder HR set. Leave it blank to declare no tenant-wide milestones.
            </p>
          </CardContent>
        </Card>

        <div className="flex justify-end">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save policy settings
          </Button>
        </div>
      </form>
    </div>
  );
}
