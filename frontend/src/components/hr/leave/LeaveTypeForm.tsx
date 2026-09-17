'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
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
import { ENCASHMENT_RATE_BASIS_OPTIONS } from '@/types/hr/leave';
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
export function leaveTypeFormToRequest(
  v: LeaveTypeFormValues,
  allowanceComponentIds: string[] = [],
) {
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
    allowanceComponentIds,
  };
}
