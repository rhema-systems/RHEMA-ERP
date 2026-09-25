'use client';

/**
 * The leave-type form, starting from the KIND (round 5, decision A4 — "the simplification").
 *
 * The stakeholders' point was that the module looked more complicated than leave is. It was the
 * form: thirty settings on one page, most of which only one kind of leave ever uses. So the kind is
 * chosen first, the settings that kind uses come next, and everything else waits under "Advanced
 * settings" — still there, still saved, just not in the way.
 *
 *   Annual     the service gate, the build-up, carry-over, and encashment (exit only)
 *   Maternity  the length, the certificate, and a note on the statutory extensions
 *   Other      ONE number, the limit, plus paid, approval, reliever, notice, counting
 *
 * ⚠ Nothing is removed from the payload: a setting under Advanced is sent exactly as before, so
 * switching a type's kind never wipes a value the other kinds would have shown.
 */

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  ENCASHMENT_RATE_BASIS_OPTIONS,
  LEAVE_TYPE_CATEGORY_OPTIONS,
  LEAVE_YEAR_END_BASIS_OPTIONS,
  type LeaveTypeCategory,
} from '@/types/hr/leave';
import {
  ColorField,
  hexColorSchema,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { payComponentService } from '@/services/hr/compensation.service';
import { cn } from '@/lib/utils';

export const leaveTypeSchema = z
  .object({
    category: z.enum(['Annual', 'Maternity', 'Other']),
    name: z.string().min(1, 'Name is required').max(100),
    code: z.string().min(1, 'Code is required').max(20),
    description: z.string().max(1000).optional().or(z.literal('')),
    isPaid: z.boolean(),
    defaultDaysPerYear: z.coerce.number().int('Whole days only').min(0, 'Cannot be negative'),
    maxDaysPerYear: z.coerce.number().int('Whole days only').min(0, 'Cannot be negative'),
    minDaysNotice: z.string().optional().or(z.literal('')),
    requiresApproval: z.boolean(),
    calendarColor: hexColorSchema,
    hasSubTypes: z.boolean(),
    allowCarryOver: z.boolean(),
    maxCarryOverDays: z.string().optional().or(z.literal('')),
    carryOverExpiryMonths: z.string().optional().or(z.literal('')),
    forfeitUnusedAfterMonths: z.string().optional().or(z.literal('')),
    yearEndBasis: z.enum(['Granted', 'Earned']),
    proRateFirstYearEntitlement: z.boolean(),
    countWeekendsAsLeave: z.boolean(),
    countHolidaysAsLeave: z.boolean(),
    allowCashConversion: z.boolean(),
    requiresReliever: z.boolean(),
    requiresMedicalCertificate: z.boolean(),
    selfCertificationDays: z.string().optional().or(z.literal('')),
    medicalBoardThresholdDays: z.string().optional().or(z.literal('')),
    minServiceMonthsToAccess: z.string().optional().or(z.literal('')),
    encashmentRateBasis: z.enum(['DerivedFromEmoluments', 'Manual']),
    allowanceComponentIds: z.array(z.string()),
    encashmentRatePerDay: z.string().optional().or(z.literal('')),
    encashmentWorkingDaysPerMonth: z.coerce.number().int().min(1, 'Must be at least 1'),
    isActive: z.boolean(),
  })
  // ⚠ Not for Other: its one number is the limit, and the cap waits under Advanced — a check that
  // failed on a field nobody can see would block the save with no visible reason. The payload
  // raises the cap to the limit instead (leaveTypeFormToRequest).
  .refine((v) => v.category === 'Other' || v.maxDaysPerYear >= v.defaultDaysPerYear, {
    message: 'Max days cannot be below the default',
    path: ['maxDaysPerYear'],
  })
  // A manual encashment basis is meaningless without the rate it refers to.
  .refine((v) => v.encashmentRateBasis !== 'Manual' || !!v.encashmentRatePerDay, {
    message: 'Enter a rate, or derive it from emoluments',
    path: ['encashmentRatePerDay'],
  });

export type LeaveTypeFormValues = z.infer<typeof leaveTypeSchema>;

export const emptyLeaveType: LeaveTypeFormValues = {
  category: 'Other',
  name: '',
  code: '',
  description: '',
  isPaid: true,
  defaultDaysPerYear: 0,
  maxDaysPerYear: 0,
  minDaysNotice: '',
  requiresApproval: true,
  calendarColor: '',
  hasSubTypes: false,
  allowCarryOver: false,
  maxCarryOverDays: '',
  carryOverExpiryMonths: '',
  forfeitUnusedAfterMonths: '',
  yearEndBasis: 'Granted' as const,
  proRateFirstYearEntitlement: false,
  countWeekendsAsLeave: false,
  countHolidaysAsLeave: false,
  allowCashConversion: false,
  requiresReliever: false,
  requiresMedicalCertificate: false,
  selfCertificationDays: '3',
  medicalBoardThresholdDays: '90',
  minServiceMonthsToAccess: '',
  encashmentRateBasis: 'DerivedFromEmoluments',
  allowanceComponentIds: [],
  encashmentRatePerDay: '',
  encashmentWorkingDaysPerMonth: 22,
  isActive: true,
};

/** The fields each kind leaves under "Advanced settings" — used to open it when one of them fails. */
const ADVANCED_FIELDS: Record<LeaveTypeCategory, (keyof LeaveTypeFormValues)[]> = {
  Annual: ['requiresMedicalCertificate', 'selfCertificationDays', 'medicalBoardThresholdDays', 'hasSubTypes'],
  Maternity: [
    'minDaysNotice', 'minServiceMonthsToAccess', 'allowCarryOver', 'maxCarryOverDays',
    'carryOverExpiryMonths', 'forfeitUnusedAfterMonths', 'yearEndBasis', 'proRateFirstYearEntitlement',
    'allowCashConversion', 'encashmentRateBasis', 'encashmentRatePerDay', 'encashmentWorkingDaysPerMonth',
    'hasSubTypes',
  ],
  Other: [
    'maxDaysPerYear', 'minServiceMonthsToAccess', 'allowCarryOver', 'maxCarryOverDays',
    'carryOverExpiryMonths', 'forfeitUnusedAfterMonths', 'yearEndBasis', 'proRateFirstYearEntitlement',
    'allowCashConversion', 'encashmentRateBasis', 'encashmentRatePerDay', 'encashmentWorkingDaysPerMonth',
    'hasSubTypes',
  ],
};

interface LeaveTypeFormProps {
  defaultValues: LeaveTypeFormValues;
  onSubmit: (values: LeaveTypeFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  showActive?: boolean;
}

function SectionHeading({ children, hint }: { children: React.ReactNode; hint?: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <h3 className="text-sm font-medium text-muted-foreground">{children}</h3>
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

export function LeaveTypeForm({
  defaultValues,
  onSubmit,
  submitting,
  submitLabel,
  onCancel,
  showActive = true,
}: LeaveTypeFormProps) {
  const form = useForm<LeaveTypeFormValues>({
    resolver: zodResolver(leaveTypeSchema) as any,
    defaultValues,
  });
  const [advancedOpen, setAdvancedOpen] = useState(false);

  const category = form.watch('category');
  const allowCarryOver = form.watch('allowCarryOver');
  const allowCashConversion = form.watch('allowCashConversion');
  const rateBasis = form.watch('encashmentRateBasis');
  const selectedAllowances = form.watch('allowanceComponentIds') ?? [];
  const requiresCertificate = form.watch('requiresMedicalCertificate');
  const selfCertDays = form.watch('selfCertificationDays');
  const boardDays = form.watch('medicalBoardThresholdDays');

  // Only allowances feed the derived rate — a deduction or a tax component would make the
  // arithmetic meaningless, so the list is filtered rather than left for somebody to get right.
  const { data: payComponents, isLoading: componentsLoading } = useQuery({
    queryKey: ['hr', 'pay-components', 'allowances'],
    queryFn: () => payComponentService.getAll(true),
  });
  const allowanceOptions = (payComponents ?? []).filter((c) => c.componentType === 'Allowance');

  // A failed save whose error sits under Advanced opens it, so the reason is on screen.
  const onInvalid = (errors: Partial<Record<keyof LeaveTypeFormValues, unknown>>) => {
    if (ADVANCED_FIELDS[category].some((f) => f in errors)) setAdvancedOpen(true);
  };

  // ── The sections, each written once and placed by the kind ────────────────────────────────
  const counting = (
    <FieldRow>
      <SwitchField form={form} name="countWeekendsAsLeave" label="Count weekends" />
      <SwitchField form={form} name="countHolidaysAsLeave" label="Count holidays" />
    </FieldRow>
  );

  const workflow = (
    <section className="space-y-4">
      <SectionHeading>Approval and cover</SectionHeading>
      <FieldRow>
        <SwitchField form={form} name="requiresApproval" label="Requires approval" />
        <SwitchField form={form} name="requiresReliever" label="Requires a reliever" />
      </FieldRow>
    </section>
  );

  const notice = <NumberField form={form} name="minDaysNotice" label="Minimum notice (days)" />;
  const serviceGate = (
    <NumberField form={form} name="minServiceMonthsToAccess" label="Service before it can be taken (months)" />
  );
  const subTypes = (
    <SwitchField
      form={form}
      name="hasSubTypes"
      label="Has sub-types"
      description="Variants of this leave with their own names and caps, managed on the Sub-types tab."
    />
  );

  const medical = (
    <section className="space-y-4">
      <SectionHeading>Medical evidence</SectionHeading>
      <SwitchField
        form={form}
        name="requiresMedicalCertificate"
        label="Requires excuse duty (a medical certificate)"
        description="Off for most leave. On for sick leave, its relatives and maternity: an absence longer than the self-certification period cannot be submitted until a certificate is attached."
      />
      {requiresCertificate && (
        <>
          <FieldRow>
            <NumberField form={form} name="selfCertificationDays" label="Self-certification days" />
            <NumberField
              form={form}
              name="medicalBoardThresholdDays"
              label="Medical board threshold (days per year)"
            />
          </FieldRow>
          <p className="text-sm text-muted-foreground">
            An absence of <strong>{selfCertDays || 0}</strong> day(s) or fewer needs nothing but the
            employee&apos;s own word.{' '}
            {boardDays ? (
              <>
                Once this leave type reaches <strong>{boardDays}</strong> day(s) in one year &mdash;
                counted across every request, not per request &mdash; a medical board&apos;s
                recommendation must be attached as well.
              </>
            ) : (
              <>Leave the board threshold blank and no board is ever required.</>
            )}
          </p>
          <p className="text-sm text-muted-foreground">
            These are starting values, not rules from any authority. Set what this
            organisation&apos;s policy says.
          </p>
        </>
      )}
    </section>
  );

  const carryOver = (
    <section className="space-y-4">
      <SectionHeading>Carry-over</SectionHeading>
      <SwitchField form={form} name="allowCarryOver" label="Allow carry-over" />
      {allowCarryOver && (
        <FieldRow>
          <NumberField form={form} name="maxCarryOverDays" label="Max carry-over days" />
          <NumberField form={form} name="carryOverExpiryMonths" label="Carry-over expires after (months)" />
        </FieldRow>
      )}
      <NumberField form={form} name="forfeitUnusedAfterMonths" label="Forfeit unused after (months)" />

      {/*
        ⚠ Entitlement plan B2. This governs BOTH year-end runs, which is why it sits here
        under Carry-over rather than beside one of them, and why its label says so.
      */}
      <SelectField
        form={form}
        name="yearEndBasis"
        label="Carry-over and forfeiture count"
        required
        options={LEAVE_YEAR_END_BASIS_OPTIONS}
      />
      <p className="text-sm text-muted-foreground">
        {form.watch('yearEndBasis') === 'Earned' ? (
          <>
            A mid-year joiner carries <strong>what they built up</strong>. Somebody who accrued 3.5
            days and took none carries 3.5, not the full cap. Forfeiture follows the same reading, so
            the days they never accrued are simply not forfeited — they were never theirs to lose.
          </>
        ) : (
          <>
            A mid-year joiner carries <strong>what the year owed them</strong>, up to the cap above
            — so somebody who accrued 3.5 days and took none still carries the full five. ⚠ This is
            what both runs did before the setting existed, which is why it is the default.
          </>
        )}
      </p>

      {/*
        ⚠ Refused by the API alongside an incremental accrual policy. The switch is shown
        regardless, and the refusal explains why, rather than the control vanishing for
        reasons a user cannot see.
      */}
      <SwitchField
        form={form}
        name="proRateFirstYearEntitlement"
        label="Pro-rate the first year's entitlement"
        description="Scales a joiner's first year to the months they were here — 15 days becomes 3.75 for an October start. ⚠ Cannot be combined with incremental accrual, which already does this; the save will be refused and say so. Use it for leave that is GRANTED rather than earned."
      />
    </section>
  );

  const encashment = (
    <section className="space-y-4">
      <SectionHeading
        hint={
          category === 'Annual'
            ? 'Exit only. Leave is cashed in when somebody leaves, not while they are employed (round 5, decision A3). These settings value a leaver’s unused days.'
            : 'Only annual leave is ever cashed in; the setting has no effect on this kind.'
        }
      >
        Encashment
      </SectionHeading>
      <SwitchField
        form={form}
        name="allowCashConversion"
        label="Allow cash conversion"
        description="Lets unused days be encashed."
      />
      {allowCashConversion && (
        <>
          <FieldRow>
            <SelectField
              form={form}
              name="encashmentRateBasis"
              label="Rate basis"
              required
              options={ENCASHMENT_RATE_BASIS_OPTIONS}
            />
            <NumberField form={form} name="encashmentRatePerDay" label="Rate per day" step="0.01" />
          </FieldRow>
          <NumberField
            form={form}
            name="encashmentWorkingDaysPerMonth"
            label="Working days per month"
            required
          />

          {/*
            ⚠ L-12. These links decide what a day of encashed leave is WORTH: the derived rate is
            (monthly basic + the allowances ticked here) divided by the working-days figure above.
          */}
          {rateBasis === 'DerivedFromEmoluments' && (
            <div className="space-y-2">
              <Label>Allowances included in the rate</Label>
              {componentsLoading ? (
                <p className="text-sm text-muted-foreground">Loading allowances…</p>
              ) : allowanceOptions.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  No allowance pay components are defined. Payroll owns the component master; HR
                  mirrors it.
                </p>
              ) : (
                <div className="grid gap-2 rounded-md border p-3 sm:grid-cols-2">
                  {allowanceOptions.map((c) => {
                    const checked = selectedAllowances.includes(c.id);
                    return (
                      <label key={c.id} className="flex items-start gap-2 text-sm">
                        <input
                          type="checkbox"
                          className="mt-1"
                          checked={checked}
                          onChange={(e) =>
                            form.setValue(
                              'allowanceComponentIds',
                              e.target.checked
                                ? [...selectedAllowances, c.id]
                                : selectedAllowances.filter((x) => x !== c.id),
                              { shouldDirty: true },
                            )
                          }
                        />
                        <span>
                          {c.name}
                          {c.code && <span className="ml-1 text-xs text-muted-foreground">({c.code})</span>}
                        </span>
                      </label>
                    );
                  })}
                </div>
              )}
              <p className="text-xs text-muted-foreground">
                Tick nothing and an encashed day is worth basic pay alone.{' '}
                <strong>Removing one lowers what people are paid</strong> for leave they have already
                earned, so it is not a change to make casually.
              </p>
            </div>
          )}
        </>
      )}
    </section>
  );

  // ── What each kind shows up front, and what it leaves under Advanced ─────────────────────
  const essentials =
    category === 'Annual' ? (
      <>
        <section className="space-y-4">
          <SectionHeading hint="How the year's days build up month by month is the Accrual tab on the leave type's page, once it is saved.">
            Entitlement
          </SectionHeading>
          <FieldRow>
            <NumberField form={form} name="defaultDaysPerYear" label="Days per year" required />
            <NumberField form={form} name="maxDaysPerYear" label="Max days per year" required />
          </FieldRow>
          <FieldRow>
            {serviceGate}
            {notice}
          </FieldRow>
          <p className="text-xs text-muted-foreground">
            The Labour Act (s.20) grants annual leave after twelve months of continuous service,
            which is what the service gate is for.
          </p>
          {counting}
        </section>
        {carryOver}
        {encashment}
        {workflow}
      </>
    ) : category === 'Maternity' ? (
      <>
        <section className="space-y-4">
          <SectionHeading hint="Statutory maternity leave is at least twelve weeks (84 calendar days), so it usually counts weekends and holidays.">
            Length
          </SectionHeading>
          <FieldRow>
            <NumberField form={form} name="defaultDaysPerYear" label="Days" required />
            <NumberField form={form} name="maxDaysPerYear" label="Max days" required />
          </FieldRow>
          {counting}
          <p className="text-xs text-muted-foreground">
            No notice rule applies, because a birth can come early. Who may take it is on the
            Eligibility tab.
          </p>
        </section>
        {medical}
        <p className="text-xs text-muted-foreground">
          For maternity, switch the certificate on with 0 self-certification days, and leave the board
          threshold blank. A medical board is for long sickness; with a threshold set, an extension
          would send a new mother to one.
        </p>
        <p className="rounded-md border bg-muted/40 p-3 text-sm text-muted-foreground">
          <strong>Extensions.</strong> The Labour Act (s.57) adds two weeks for an abnormal or multiple
          birth, and more for certified illness. HR gives these as an adjustment on the balance, with
          the certificate attached. An approver confirms or rejects maternity leave, but never moves
          its dates.
        </p>
        {workflow}
      </>
    ) : (
      <>
        <section className="space-y-4">
          <SectionHeading hint="Staff see it as “limit · used · left”. HR gives fewer days by suggesting other dates.">
            The limit
          </SectionHeading>
          <FieldRow>
            <NumberField form={form} name="defaultDaysPerYear" label="Days per year (the limit)" required />
            {notice}
          </FieldRow>
          {counting}
          <p className="text-xs text-muted-foreground">Who may take it is on the Eligibility tab.</p>
        </section>
        {medical}
        {workflow}
      </>
    );

  const advanced =
    category === 'Annual' ? (
      <>
        {medical}
        {subTypes}
      </>
    ) : category === 'Maternity' ? (
      <>
        <FieldRow>
          {notice}
          {serviceGate}
        </FieldRow>
        <p className="text-xs text-muted-foreground">Minimum notice is ignored for maternity leave.</p>
        {carryOver}
        {encashment}
        {subTypes}
      </>
    ) : (
      <>
        <FieldRow>
          <NumberField form={form} name="maxDaysPerYear" label="Hard cap (days per year)" />
          {serviceGate}
        </FieldRow>
        <p className="text-xs text-muted-foreground">
          The cap never lowers the limit above it; it is raised to the limit when it is lower.
        </p>
        {carryOver}
        {encashment}
        {subTypes}
      </>
    );

  return (
    <Card className="max-w-3xl">
      <form onSubmit={form.handleSubmit(onSubmit, onInvalid)}>
        <CardHeader>
          <CardTitle>Leave Type Details</CardTitle>
          <CardDescription>Start with the kind of leave; the form asks for what that kind uses.</CardDescription>
        </CardHeader>

        <CardContent className="space-y-6">
          <section className="space-y-3">
            <SectionHeading>What kind of leave is this?</SectionHeading>
            <div className="grid gap-3 sm:grid-cols-3" role="radiogroup" aria-label="Kind of leave">
              {LEAVE_TYPE_CATEGORY_OPTIONS.map((option) => {
                const selected = category === option.value;
                return (
                  <button
                    key={option.value}
                    type="button"
                    role="radio"
                    aria-checked={selected}
                    onClick={() => form.setValue('category', option.value, { shouldDirty: true })}
                    className={cn(
                      'rounded-md border p-3 text-left transition-colors',
                      selected ? 'border-primary bg-primary/5 ring-1 ring-primary' : 'hover:bg-muted/50',
                    )}
                  >
                    <span className="block text-sm font-medium">{option.label}</span>
                    <span className="mt-1 block text-xs text-muted-foreground">{option.drives}</span>
                  </button>
                );
              })}
            </div>
          </section>

          <section className="space-y-4">
            <FieldRow>
              <TextField form={form} name="name" label="Name" placeholder="Annual Leave" required />
              <TextField form={form} name="code" label="Code" placeholder="ANL" required />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" />
            <FieldRow>
              <div className="space-y-2">
                <ColorField form={form} name="calendarColor" label="Calendar colour" />
                <p className="text-xs text-muted-foreground">Used on leave calendars.</p>
              </div>
              <SwitchField
                form={form}
                name="isPaid"
                label="Paid leave"
                description="Unpaid leave still consumes entitlement."
              />
            </FieldRow>
          </section>

          {essentials}

          <details
            className="rounded-md border p-4"
            open={advancedOpen}
            onToggle={(e) => setAdvancedOpen((e.target as HTMLDetailsElement).open)}
          >
            <summary className="cursor-pointer text-sm font-medium">Advanced settings</summary>
            <div className="mt-4 space-y-6">{advanced}</div>
          </details>

          {showActive && <SwitchField form={form} name="isActive" label="Active" />}
        </CardContent>

        <CardFooter className="flex justify-end gap-2">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" disabled={submitting}>
            {submitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            {submitLabel}
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}

/**
 * Shared by the new and edit pages: form values -> API payload.
 *
 * ⚠ `allowanceComponentIds` comes from the FORM, not from a caller. It used to be a parameter each
 * page had to remember to echo back, and forgetting it sent an empty array — which the server reads
 * as "remove them all" and which silently changed what a day of encashed leave was worth (finding
 * L-13). The form owns the value, so there is nothing to forget.
 */
export function leaveTypeFormToRequest(v: LeaveTypeFormValues) {
  const num = (s?: string) => (s && s.trim() ? Number(s) : null);
  return {
    category: v.category,
    name: v.name,
    code: v.code,
    description: v.description || null,
    isPaid: v.isPaid,
    defaultDaysPerYear: v.defaultDaysPerYear,
    // Other shows one number, the limit; its cap is raised to meet it rather than left below it.
    maxDaysPerYear:
      v.category === 'Other' ? Math.max(v.maxDaysPerYear, v.defaultDaysPerYear) : v.maxDaysPerYear,
    minDaysNotice: num(v.minDaysNotice),
    requiresApproval: v.requiresApproval,
    calendarColor: v.calendarColor || null,
    hasSubTypes: v.hasSubTypes,
    allowCarryOver: v.allowCarryOver,
    maxCarryOverDays: num(v.maxCarryOverDays),
    countWeekendsAsLeave: v.countWeekendsAsLeave,
    countHolidaysAsLeave: v.countHolidaysAsLeave,
    allowCashConversion: v.allowCashConversion,
    requiresReliever: v.requiresReliever,
    minServiceMonthsToAccess: num(v.minServiceMonthsToAccess),
    carryOverExpiryMonths: num(v.carryOverExpiryMonths),
    forfeitUnusedAfterMonths: num(v.forfeitUnusedAfterMonths),
    yearEndBasis: v.yearEndBasis,
    proRateFirstYearEntitlement: v.proRateFirstYearEntitlement,
    requiresMedicalCertificate: v.requiresMedicalCertificate,
    selfCertificationDays: Number(v.selfCertificationDays || 0),
    // Blank means no board is ever required, which is not the same as a threshold of zero.
    medicalBoardThresholdDays: v.medicalBoardThresholdDays ? Number(v.medicalBoardThresholdDays) : null,
    encashmentRateBasis: v.encashmentRateBasis,
    encashmentRatePerDay: num(v.encashmentRatePerDay),
    encashmentWorkingDaysPerMonth: v.encashmentWorkingDaysPerMonth,
    allowanceComponentIds: v.allowanceComponentIds ?? [],
  };
}
