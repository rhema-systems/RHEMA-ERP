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
import { fiscalCalendarService } from '@/services/hr/company-schedule.service';
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
    // ⚠ 365, not 3650, matching the entity and the update DTO — a committee task cannot usefully
    // remind more than a year ahead.
    teamTaskReminderLeadDays: z.coerce.number().int().min(0).max(365),
    salaryChangeRequiresApproval: z.boolean(),
    certificationExpiryLeadDays: z.coerce.number().int().min(0).max(3650),

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
    // Round 5, lane L2b: empty means no cap, so '' is a real answer here.
    settlementLeaveDaysCap: z.union([z.coerce.number().int().min(1).max(366), z.literal('')]).optional(),
    medicalBoardQuorum: z.coerce.number().int().min(1).max(20),
    // Round 5, lane K-II-b (PNDCL 187). Empty is a real answer for the months and the ceiling.
    permanentTotalIncapacityMonths: z.union([z.coerce.number().int().min(1).max(600), z.literal('')]).optional(),
    temporaryIncapacityMaxMonths: z.coerce.number().int().min(1).max(120),
    compensationEarningsCeiling: z.union([z.coerce.number().positive(), z.literal('')]).optional(),
    attendanceRateIncludesApprovedLeave: z.boolean(),

    allowInServiceEncashment: z.boolean(),
    leaveStartingReminderDays: z.coerce.number().int().min(0).max(180),
    leaveClosureGraceDays: z.coerce.number().int().min(0).max(180),
    leaveUndecidedChaseDays: z.coerce.number().int().min(0).max(180),
    mandatoryLeaveChaseFromMonth: z.coerce.number().int().min(1).max(12),
    leaveCarryOverExpiryReminderDays: z.coerce.number().int().min(0).max(365),
    leaveYearStartMonth: z.string(),
    onboardingTaskDueLeadDays: z.coerce.number().int().min(0).max(90),
    orientationDueLeadDays: z.coerce.number().int().min(0).max(90),
    orientationCertificateExpiryLeadDays: z.coerce.number().int().min(0).max(365),
    orientationChaseAfterDays: z.coerce.number().int().min(1).max(90),
    companyEventRsvpChaseLeadDays: z.coerce.number().int().min(0).max(60),

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

  // Company-schedule lane 4b (D-6, the user's ruling): while Finance has a fiscal calendar it decides every fiscal year,
  // so the start month below is shown read-only with Finance's own start. A failed read leaves the field editable.
  const { data: fiscalCalendar } = useQuery({
    queryKey: ['hr', 'fiscal-calendar'],
    queryFn: () => fiscalCalendarService.get(),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });
  const financeNextYear = fiscalCalendar?.years.length ? fiscalCalendar.nextYear : null;

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
      teamTaskReminderLeadDays: data.teamTaskReminderLeadDays,
      salaryChangeRequiresApproval: data.salaryChangeRequiresApproval,
      certificationExpiryLeadDays: data.certificationExpiryLeadDays,

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
      settlementLeaveDaysCap: data.settlementLeaveDaysCap ?? '',
      medicalBoardQuorum: data.medicalBoardQuorum ?? 1,
      permanentTotalIncapacityMonths: data.permanentTotalIncapacityMonths ?? '',
      temporaryIncapacityMaxMonths: data.temporaryIncapacityMaxMonths ?? 24,
      compensationEarningsCeiling: data.compensationEarningsCeiling ?? '',
      attendanceRateIncludesApprovedLeave: data.attendanceRateIncludesApprovedLeave,
      allowInServiceEncashment: data.allowInServiceEncashment,
      leaveStartingReminderDays: data.leaveStartingReminderDays,
      leaveClosureGraceDays: data.leaveClosureGraceDays,
      leaveUndecidedChaseDays: data.leaveUndecidedChaseDays,
      mandatoryLeaveChaseFromMonth: data.mandatoryLeaveChaseFromMonth,
      leaveCarryOverExpiryReminderDays: data.leaveCarryOverExpiryReminderDays,
      leaveYearStartMonth: String(data.leaveYearStartMonth ?? 1),
      onboardingTaskDueLeadDays: data.onboardingTaskDueLeadDays,
      orientationDueLeadDays: data.orientationDueLeadDays,
      orientationCertificateExpiryLeadDays: data.orientationCertificateExpiryLeadDays,
      orientationChaseAfterDays: data.orientationChaseAfterDays,
      companyEventRsvpChaseLeadDays: data.companyEventRsvpChaseLeadDays ?? 2,
    });
  }, [data, form]);

  const genderSpecific = !!form.watch('useGenderSpecificRetirementAge');
  const proceduralDays = Number(form.watch('proceduralAbsenceDays') ?? 0);

  // The "two daily-rate bases" comparison that sat here left with leave settings audit 2 (L-73,
  // L-74): HR holds no rate at all now. It records days, and Finance values them.

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
        teamTaskReminderLeadDays: Number(v.teamTaskReminderLeadDays),
        salaryChangeRequiresApproval: v.salaryChangeRequiresApproval,
        certificationExpiryLeadDays: Number(v.certificationExpiryLeadDays),

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
        // ⚠ Sent every time, as null when emptied: the server keeps its default (56) for a save
        // that leaves the field out, so only an explicit null removes the cap.
        settlementLeaveDaysCap: orNullNumber(v.settlementLeaveDaysCap),
        // ⚠ Sent every time: a save that leaves it out resets it to 1.
        medicalBoardQuorum: Number(v.medicalBoardQuorum),
        // ⚠ Both sent every time, as null when emptied: the months keep 96 on a save that omits
        // them, and the ceiling would be cleared by one.
        permanentTotalIncapacityMonths: orNullNumber(v.permanentTotalIncapacityMonths),
        temporaryIncapacityMaxMonths: Number(v.temporaryIncapacityMaxMonths),
        compensationEarningsCeiling: orNullNumber(v.compensationEarningsCeiling),
        attendanceRateIncludesApprovedLeave: v.attendanceRateIncludesApprovedLeave,
        allowInServiceEncashment: v.allowInServiceEncashment,
        leaveStartingReminderDays: Number(v.leaveStartingReminderDays),
        leaveClosureGraceDays: Number(v.leaveClosureGraceDays),
        leaveUndecidedChaseDays: Number(v.leaveUndecidedChaseDays),
        mandatoryLeaveChaseFromMonth: Number(v.mandatoryLeaveChaseFromMonth),
        leaveCarryOverExpiryReminderDays: Number(v.leaveCarryOverExpiryReminderDays),
        leaveYearStartMonth: Number(v.leaveYearStartMonth),
        onboardingTaskDueLeadDays: Number(v.onboardingTaskDueLeadDays),
        orientationDueLeadDays: Number(v.orientationDueLeadDays),
        orientationCertificateExpiryLeadDays: Number(v.orientationCertificateExpiryLeadDays),
        orientationChaseAfterDays: Number(v.orientationChaseAfterDays),
        companyEventRsvpChaseLeadDays: Number(v.companyEventRsvpChaseLeadDays),
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
            <FieldRow>
              <NumberField
                form={form}
                name="retirementCountdownLeadDays"
                label="Upcoming retirement (days)"
                required
              />
              <NumberField
                form={form}
                name="teamTaskReminderLeadDays"
                label="Committee task due (days)"
                required
              />
            </FieldRow>
            {/* Round 3, lane S (D-1). On: the Salary tab's direct writes lock and a change of pay
                is raised as a request, approved on the engine and applied to HR and payroll. */}
            <div className="pt-2">
              <SwitchField
                form={form}
                name="salaryChangeRequiresApproval"
                label="Salary changes require an approved request"
                description="Grade placement, pay basis and the salary figure change only through an approved salary change request. Staff movements and hires are unaffected."
              />
            </div>
            {/*
              ⚠ Lane C2's field, wired through by F2. It was on the entity and read by the
              certification expiry sweep, but reached neither DTO — the engine honoured it and
              nobody could change it. A credential with its own lead days overrides this.
            */}
            <NumberField
              form={form}
              name="certificationExpiryLeadDays"
              label="Credential expiry (days)"
              required
            />
            {/*
              ⚠ Days, not weeks, on purpose — and said here because the figure looks wrong beside
              its neighbours. A committee action item is something somebody does on Tuesday; the
              others are dates people plan a month around.
            */}
            <p className="text-muted-foreground text-xs">
              Committee task reminders default to 3 days, unlike the rest — an action item minuted
              at a meeting is short-horizon work. Set it to 0 to remind only once a task is due.
            </p>
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

            {/*
              Leave settings audit 2 (L-74): "Final settlement — days per year" was the divisor HR
              used to price notice pay and leave owed on exit. Finance values a leaver's pay now, so
              HR holds no rate.
            */}

            {/* Round 5, lane L2b: FR-HR-152's cap, visible and changeable instead of a constant. */}
            <div className="space-y-2">
              <NumberField
                form={form}
                name="settlementLeaveDaysCap"
                label="Final settlement — most days of annual leave paid"
              />
              <p className="text-sm text-muted-foreground">
                A leaver is paid for the annual leave they are owed on their last day: this leave
                year&apos;s share, built up to that day, plus carried days not yet lapsed, less what
                they took or already cashed in. This caps the days paid (FR-HR-152 says 56).{' '}
                <strong>Leave it empty for no cap.</strong> Nothing is paid on summary dismissal
                (Labour Act, s.30(3)), whatever this says. The amount on the statement is indicative:
                Finance confirms it.
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

        {/* Round 5, lane K-II-a: a board decides each case at a sitting, by the members present. */}
        <Card>
          <CardHeader>
            <CardTitle>Medical boards</CardTitle>
            <CardDescription>
              How many of a board&apos;s deciding members must be present for it to decide a case.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <NumberField
              form={form}
              name="medicalBoardQuorum"
              label="Quorum — deciding members present"
              required
            />
            <p className="text-sm text-muted-foreground">
              A board decides each case at a recorded sitting, and the members present are the panel
              that decided it. A chair or member counts; a secretary or observer attends without
              deciding. A case cannot be decided at a sitting with fewer deciding members than this.
              The default, <strong>1</strong>, is the least a finding can rest on; set it to your
              organisation&apos;s rule for how many doctors must sit.
            </p>

            {/* Round 5, lane K-II-b: the Workmen's Compensation Act's figures, as defaults. */}
            <div className="grid gap-4 pt-2 sm:grid-cols-3">
              <NumberField
                form={form}
                name="permanentTotalIncapacityMonths"
                label="Months' earnings — permanent total incapacity"
              />
              <NumberField
                form={form}
                name="temporaryIncapacityMaxMonths"
                label="Longest temporary incapacity (months)"
                required
              />
              <NumberField
                form={form}
                name="compensationEarningsCeiling"
                label="Earnings ceiling — a year"
              />
            </div>
            <p className="text-sm text-muted-foreground">
              The Workmen&apos;s Compensation Act pays <strong>96</strong> months&apos; earnings for permanent
              total incapacity (s.5) and a partial incapacity its percentage of that; temporary incapacity
              is paid through payroll for at most <strong>24</strong> months (s.7). Leave the months empty
              and no figure is worked out. The Act computes compensation on at most a set amount of a
              year&apos;s earnings (s.36) — its 25,000 cedis predates redenomination and no revision was
              found, so <strong>this is empty until your organisation or counsel names the ceiling in
              force</strong>; every figure says whether a ceiling applied. The figure a board case shows
              is indicative: the labour officer notifies the amount due, and it is paid to the Court.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Leave — encashment</CardTitle>
            <CardDescription>
              Whether unused leave can be cashed in while still employed. HR approves the days;
              Finance puts the money on them when it pays.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <SwitchField
              form={form}
              name="allowInServiceEncashment"
              label="Allow leave to be encashed while still employed"
              description="Off: leave is only ever paid out when somebody leaves, and the in-service encashment screen refuses. On: employees may convert unused days to cash, for whichever leave types are marked convertible. This switch decides whether the route exists at all - the per-type setting still decides which leave may use it."
            />
            <p className="text-sm text-muted-foreground">
              FR-HR-046 says leave is encashed <em>only on exit, no other route</em>, and the Labour
              Act makes an agreement to give up annual leave void (s.31) — so this is off, and the
              employee portal hides its encashment screen. Turn it on only if this
              organisation&apos;s policy differs; even then, only annual leave, from the current
              leave year, up to the days built up so far, can be cashed in.
            </p>

            {/*
              Leave settings audit 2 (L-73): what a day of cashed-in leave is worth is Finance's
              figure, entered when it marks the days paid. HR holds no rate, so the comparison of
              two bases that sat here is gone.
            */}
          </CardContent>
        </Card>

        {/*
          ⚠ Its own card, ABOVE the encashment and reminder ones, because it is the setting the
          other two are measured against — carry-over expiry and the forfeiture cut-off are both
          counted from the start of this year, not from January (entitlement plan C1).
        */}
        <Card>
          <CardHeader>
            <CardTitle>Leave — the leave year</CardTitle>
            <CardDescription>
              When this organisation&apos;s leave year begins. Everything else in leave is measured
              from it.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <SelectField
              form={form}
              name="leaveYearStartMonth"
              label="Leave year starts"
              options={FISCAL_YEAR_MONTHS}
              required
            />
            <p className="text-sm text-muted-foreground">
              A leave year is <strong>named after the calendar year it starts in</strong> — with an
              April start, March 2028 belongs to leave year 2027. Entitlement, carry-over expiry and
              the forfeiture cut-off are all counted from this month.
            </p>
            <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm dark:border-amber-900 dark:bg-amber-950/40">
              <p className="font-medium">⚠ Set this during setup. It cannot be changed later.</p>
              <p className="mt-1">
                Once any leave has been recorded, the save is refused — and not out of caution.
                Moving the boundary changes which leave year some dates fall in while the records
                already written keep their current labels. Most figures could be re-derived;{' '}
                <strong>a carry-over that has already run could not</strong>, because it was
                computed against boundaries that would no longer exist.
              </p>
              <p className="mt-1">
                ⚠ This is <strong>not</strong> the fiscal year above. Plenty of organisations run
                the two apart, so they are separate settings on purpose.
              </p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Leave — reminder cadence</CardTitle>
            <CardDescription>
              How persistently the leave module chases people. All five were fixed in code until
              now, so changing any of them used to need a release.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">
              <p className="font-medium text-foreground">Who is told, in the app and by email</p>
              <ul className="mt-1 list-disc space-y-0.5 pl-5">
                <li>Leave starting soon, and carried days about to lapse: the employee.</li>
                <li>
                  A request waiting: whoever its approval step is asking. The employee instead, when
                  the approver has suggested other dates.
                </li>
                <li>
                  Leave not closed: the line manager once the return is reported, otherwise HR.
                </li>
                <li>
                  Annual leave not yet planned or taken: the employee, their supervisor (one message
                  naming all their people) and HR (one summary).
                </li>
                <li>
                  When someone has served the qualifying period for annual leave: the employee and HR.
                </li>
              </ul>
              <p className="mt-1">
                Anyone who cannot be told directly, for example because they have no login, is
                passed to HR with the reason.
              </p>
            </div>
            <FieldRow>
              <NumberField
                form={form}
                name="leaveStartingReminderDays"
                label="Ask the employee about approved leave this many days ahead"
                required
              />
              <NumberField
                form={form}
                name="leaveClosureGraceDays"
                label="Grace before chasing unclosed leave"
                required
              />
            </FieldRow>
            <FieldRow>
              <NumberField
                form={form}
                name="leaveUndecidedChaseDays"
                label="Chase an undecided request after this many days"
                required
              />
              <NumberField
                form={form}
                name="leaveCarryOverExpiryReminderDays"
                label="Warn this many days before carry-over expires"
                required
              />
            </FieldRow>
            <NumberField
              form={form}
              name="mandatoryLeaveChaseFromMonth"
              label="Start chasing annual leave not yet planned or taken from month of the leave year"
              required
            />
            <p className="text-sm text-muted-foreground">
              Counted from the month the leave year starts: month 9 is September when the leave year
              starts in January, December when it starts in April. Late enough that the chase is not
              noise, early enough that there is still a quarter of the year in which to take the
              leave. Chasing from the first month says nothing; chasing in the last is too late to
              act on.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Orientation &amp; onboarding — reminders</CardTitle>
            <CardDescription>
              The daily reminder sweep tells people what is due: onboarding tasks go to their
              assignee, else the plan&apos;s coordinator; orientations, assessments, acknowledgements
              and certificates to the participant. Each person gets one notification listing theirs,
              and the same by email.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <NumberField
                form={form}
                name="onboardingTaskDueLeadDays"
                label="Remind about an onboarding task this many days before it is due"
                required
              />
              <NumberField
                form={form}
                name="orientationDueLeadDays"
                label="Remind about an orientation this many days before it is due"
                required
              />
            </FieldRow>
            <FieldRow>
              <NumberField
                form={form}
                name="orientationCertificateExpiryLeadDays"
                label="Warn this many days before an orientation certificate expires"
                required
              />
              <NumberField
                form={form}
                name="orientationChaseAfterDays"
                label="Chase something waiting on a person after this many days"
                required
              />
            </FieldRow>
            <p className="text-sm text-muted-foreground">
              &quot;Waiting on a person&quot; is a completed task nobody has signed off, an assessment
              not yet attempted, or an acknowledgement not yet signed. Overdue items are reminded when
              they fall due, again after a week and after a fortnight; anything more than 90 days
              overdue is treated as history.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Company schedule reminders</CardTitle>
            <CardDescription>
              Sent automatically, once each. An event&apos;s own reminder goes the number of days
              before it that its form asks for, when <strong>Send reminders</strong> is on. Everybody
              who has not answered an invitation is chased once, ahead of the event&apos;s RSVP deadline.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FieldRow>
              <NumberField
                form={form}
                name="companyEventRsvpChaseLeadDays"
                label="Chase unanswered invitations this many days before the RSVP deadline"
                required
              />
            </FieldRow>
            <p className="text-sm text-muted-foreground">
              Only live events are reminded: scheduled, confirmed or rescheduled, and approved where
              approval is required. Moving an event&apos;s date lets it be reminded again for the new
              date. Pressing <strong>Send reminder now</strong> or <strong>Chase unanswered now</strong> on
              the event counts as the send, so nobody is told twice.
            </p>
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
              {/* Company-schedule lane 4b (D-6): Finance's fiscal calendar decides; this month only while Finance has none. */}
              <SelectField
                form={form}
                name="fiscalYearStartMonth"
                label="Fiscal year starts (when Finance has no calendar)"
                options={FISCAL_YEAR_MONTHS}
                required
                disabled={!!financeNextYear}
                description={
                  financeNextYear
                    ? `Finance's calendar decides: its years start on ${new Date(`${financeNextYear.startDate.slice(0, 10)}T00:00:00Z`)
                        .toLocaleDateString(undefined, { day: 'numeric', month: 'long', timeZone: 'UTC' })} (next, FY${financeNextYear.fiscalYear}). `
                      + 'This month is not used while Finance has fiscal years.'
                    : undefined
                }
              />
            </FieldRow>
            <p className="text-xs text-muted-foreground">
              Requisitions and manpower budgets take their fiscal year from Finance&apos;s fiscal calendar, and a year Finance
              has not opened yet continues its sequence. This month is used only while Finance has no fiscal year at all.
            </p>
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
