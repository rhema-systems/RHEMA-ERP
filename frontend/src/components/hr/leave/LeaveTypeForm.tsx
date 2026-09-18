'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { ENCASHMENT_RATE_BASIS_OPTIONS, LEAVE_YEAR_END_BASIS_OPTIONS } from '@/types/hr/leave';
import {
  ColorField,
  hexColorSchema,
  DateField as _DateField,
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { payComponentService } from '@/services/hr/compensation.service';

export const leaveTypeSchema = z
  .object({
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
    mandatoryAnnualLeave: z.boolean(),
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
  .refine((v) => v.maxDaysPerYear >= v.defaultDaysPerYear, {
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
  mandatoryAnnualLeave: false,
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

interface LeaveTypeFormProps {
  defaultValues: LeaveTypeFormValues;
  onSubmit: (values: LeaveTypeFormValues) => Promise<void>;
  submitting: boolean;
  submitLabel: string;
  onCancel: () => void;
  showActive?: boolean;
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

  const allowCarryOver = form.watch('allowCarryOver');
  const allowCashConversion = form.watch('allowCashConversion');
  const rateBasis = form.watch('encashmentRateBasis');
  const selectedAllowances = form.watch('allowanceComponentIds') ?? [];

  // Only allowances feed the derived rate — a deduction or a tax component would make the
  // arithmetic meaningless, so the list is filtered rather than left for somebody to get right.
  const { data: payComponents, isLoading: componentsLoading } = useQuery({
    queryKey: ['hr', 'pay-components', 'allowances'],
    queryFn: () => payComponentService.getAll(true),
  });
  const allowanceOptions = (payComponents ?? []).filter((c) => c.componentType === 'Allowance');
  const requiresCertificate = form.watch('requiresMedicalCertificate');
  const selfCertDays = form.watch('selfCertificationDays');
  const boardDays = form.watch('medicalBoardThresholdDays');

  return (
    <Card className="max-w-3xl">
      <form onSubmit={form.handleSubmit(onSubmit)}>
        <CardHeader>
          <CardTitle>Leave Type Details</CardTitle>
          <CardDescription>
            Entitlement, carry-over and encashment rules for a kind of leave.
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-6">
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

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Entitlement</h3>
            <FieldRow>
              <NumberField
                form={form}
                name="defaultDaysPerYear"
                label="Default days per year"
                required
              />
              <NumberField form={form} name="maxDaysPerYear" label="Max days per year" required />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="minDaysNotice" label="Minimum notice (days)" />
              <NumberField
                form={form}
                name="minServiceMonthsToAccess"
                label="Min service to access (months)"
              />
            </FieldRow>
            <FieldRow>
              <SwitchField form={form} name="countWeekendsAsLeave" label="Count weekends" />
              <SwitchField form={form} name="countHolidaysAsLeave" label="Count holidays" />
            </FieldRow>
            <FieldRow>
              <SwitchField
                form={form}
                name="mandatoryAnnualLeave"
                label="Mandatory annual leave"
                description="Subject to compliance tracking."
              />
              <SwitchField form={form} name="hasSubTypes" label="Has sub-types" />
            </FieldRow>
          </section>

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Workflow</h3>
            <FieldRow>
              <SwitchField form={form} name="requiresApproval" label="Requires approval" />
              <SwitchField form={form} name="requiresReliever" label="Requires a reliever" />
            </FieldRow>
          </section>

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Medical evidence</h3>
            <SwitchField
              form={form}
              name="requiresMedicalCertificate"
              label="Requires excuse duty (a medical certificate)"
              description="Off for most leave. On for sick leave and its relatives: an absence longer than the self-certification period cannot be submitted until a certificate is attached."
            />
            {requiresCertificate && (
              <>
                <FieldRow>
                  <NumberField
                    form={form}
                    name="selfCertificationDays"
                    label="Self-certification days"
                  />
                  <NumberField
                    form={form}
                    name="medicalBoardThresholdDays"
                    label="Medical board threshold (days per year)"
                  />
                </FieldRow>
                <p className="text-sm text-muted-foreground">
                  An absence of <strong>{selfCertDays || 0}</strong> day(s) or fewer needs nothing
                  but the employee&apos;s own word.{' '}
                  {boardDays ? (
                    <>
                      Once this leave type reaches <strong>{boardDays}</strong> day(s) in one year
                      &mdash; counted across every request, not per request &mdash; a medical
                      board&apos;s recommendation must be attached as well.
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

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Carry-over</h3>
            <SwitchField form={form} name="allowCarryOver" label="Allow carry-over" />
            {allowCarryOver && (
              <FieldRow>
                <NumberField form={form} name="maxCarryOverDays" label="Max carry-over days" />
                <NumberField
                  form={form}
                  name="carryOverExpiryMonths"
                  label="Carry-over expires after (months)"
                />
              </FieldRow>
            )}
            <NumberField
              form={form}
              name="forfeitUnusedAfterMonths"
              label="Forfeit unused after (months)"
            />

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
                  A mid-year joiner carries <strong>what they built up</strong>. Somebody who
                  accrued 3.5 days and took none carries 3.5, not the full cap. Forfeiture follows
                  the same reading, so the days they never accrued are simply not forfeited —
                  they were never theirs to lose.
                </>
              ) : (
                <>
                  A mid-year joiner carries <strong>what the year owed them</strong>, up to the cap
                  above — so somebody who accrued 3.5 days and took none still carries the full
                  five. ⚠ This is what both runs did before the setting existed, which is why it is
                  the default.
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

          <section className="space-y-4">
            <h3 className="text-sm font-medium text-muted-foreground">Encashment</h3>
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
                  <NumberField
                    form={form}
                    name="encashmentRatePerDay"
                    label="Rate per day"
                    step="0.01"
                  />
                </FieldRow>
                <NumberField
                  form={form}
                  name="encashmentWorkingDaysPerMonth"
                  label="Working days per month"
                  required
                />

                {/*
                  ⚠ L-12. These links decide what a day of encashed leave is WORTH: the derived rate
                  is (monthly basic + the allowances ticked here) divided by the working-days figure
                  above. They existed in the database and on the API from the start, and there was
                  no way to set them from any screen — so every leave type paid on basic alone
                  unless somebody called the API by hand.
                */}
                {rateBasis === 'DerivedFromEmoluments' && (
                  <div className="space-y-2">
                    <Label>Allowances included in the rate</Label>
                    {componentsLoading ? (
                      <p className="text-sm text-muted-foreground">Loading allowances…</p>
                    ) : allowanceOptions.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        No allowance pay components are defined. Payroll owns the component master;
                        HR mirrors it.
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
                                {c.code && (
                                  <span className="ml-1 text-xs text-muted-foreground">({c.code})</span>
                                )}
                              </span>
                            </label>
                          );
                        })}
                      </div>
                    )}
                    <p className="text-xs text-muted-foreground">
                      Tick nothing and an encashed day is worth basic pay alone.{' '}
                      <strong>Removing one lowers what people are paid</strong> for leave they have
                      already earned, so it is not a change to make casually.
                    </p>
                  </div>
                )}
              </>
            )}
          </section>

          {showActive && <SwitchField form={form} name="isActive" label="Active" />}
        </CardContent>

        <CardFooter className="flex justify-end gap-2">
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

/**
 * Shared by the new and edit pages: form values -> API payload. Allowance components are
 * owned by the Emoluments area, so the list is preserved rather than sent empty.
 */
/**
 * ⚠ `allowanceComponentIds` now comes from the FORM, not from a caller.
 *
 * It used to be a parameter each page had to remember to echo back, and forgetting it sent an empty
 * array — which the server reads as "remove them all" and which silently changed what a day of
 * encashed leave was worth (finding L-13). The form owns the value, so there is nothing to forget.
 */
export function leaveTypeFormToRequest(v: LeaveTypeFormValues) {
  const num = (s?: string) => (s && s.trim() ? Number(s) : null);
  return {
    name: v.name,
    code: v.code,
    description: v.description || null,
    isPaid: v.isPaid,
    defaultDaysPerYear: v.defaultDaysPerYear,
    maxDaysPerYear: v.maxDaysPerYear,
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
    mandatoryAnnualLeave: v.mandatoryAnnualLeave,
    requiresMedicalCertificate: v.requiresMedicalCertificate,
    selfCertificationDays: Number(v.selfCertificationDays || 0),
    // Blank means no board is ever required, which is not the same as a threshold of zero.
    medicalBoardThresholdDays: v.medicalBoardThresholdDays
      ? Number(v.medicalBoardThresholdDays)
      : null,
    encashmentRateBasis: v.encashmentRateBasis,
    encashmentRatePerDay: num(v.encashmentRatePerDay),
    encashmentWorkingDaysPerMonth: v.encashmentWorkingDaysPerMonth,
    allowanceComponentIds: v.allowanceComponentIds ?? [],
  };
}
