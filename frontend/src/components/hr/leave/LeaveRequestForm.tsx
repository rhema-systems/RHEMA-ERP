'use client';

import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Save, Send } from 'lucide-react';
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
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { leaveService } from '@/services/hr/leave.service';
import { employeeRelieverService } from '@/services/hr/employee-reliever.service';
import { DateField, FieldRow, SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { fmtDay } from './AccrualStatementPanel';

export const leaveRequestSchema = z
  .object({
    employeeId: z.string().min(1, 'Employee is required'),
    leaveTypeId: z.string().min(1, 'Leave type is required'),
    leaveSubTypeId: z.string().optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
    reason: z.string().min(1, 'A reason is required').max(1000),
    relieverEmployeeId: z.string().optional().or(z.literal('')),
    secondRelieverEmployeeId: z.string().optional().or(z.literal('')),
    relieverNotes: z.string().max(1000).optional().or(z.literal('')),
    handoverNotes: z.string().max(2000).optional().or(z.literal('')),
    /** Set only when the request was raised from an approved plan; never edited on the form. */
    leavePlanId: z.string().optional().or(z.literal('')),
  })
  .refine((v) => v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  })
  .refine((v) => !v.secondRelieverEmployeeId || !!v.relieverEmployeeId, {
    message: 'Set the first reliever before adding a second',
    path: ['secondRelieverEmployeeId'],
  })
  .refine((v) => v.relieverEmployeeId !== v.employeeId || !v.relieverEmployeeId, {
    message: 'An employee cannot relieve themselves',
    path: ['relieverEmployeeId'],
  });

export type LeaveRequestFormValues = z.infer<typeof leaveRequestSchema>;

export const emptyLeaveRequest: LeaveRequestFormValues = {
  employeeId: '',
  leaveTypeId: '',
  leaveSubTypeId: '',
  startDate: '',
  endDate: '',
  reason: '',
  relieverEmployeeId: '',
  secondRelieverEmployeeId: '',
  relieverNotes: '',
  handoverNotes: '',
  leavePlanId: '',
};

interface LeaveRequestFormProps {
  defaultValues: LeaveRequestFormValues;
  onSubmit: (values: LeaveRequestFormValues, saveAsDraft: boolean) => Promise<void>;
  submitting: boolean;
  onCancel: () => void;
  /** Editing an existing draft only offers Save. */
  isEdit?: boolean;
}

export function LeaveRequestForm({
  defaultValues,
  onSubmit,
  submitting,
  onCancel,
  isEdit = false,
}: LeaveRequestFormProps) {
  const form = useForm<LeaveRequestFormValues>({
    resolver: zodResolver(leaveRequestSchema) as any,
    defaultValues,
  });

  // ── the reliever roster, as a DEFAULT source ────────────────────────────────
  // Decision 3 of the areas 19-23 plan, and the promise `LeaveRequest.RelieverEmployeeId` and
  // `SecondRelieverEmployeeId` have carried in their entity comments since the port — *"pre-defined
  // relievers populate both slots by priority"* — which nothing kept, because until slice 7 nothing
  // read `EmployeeReliever` at all.
  //
  // ⚠ Three things this must NOT do, each of which would turn a convenience into a defect:
  //   · never on an edit. A saved request's relievers are what was agreed; re-seeding them would
  //     silently rewrite the record from master data that has moved on since.
  //   · never over a value already in the field, including one the user has just cleared on purpose.
  //   · never more than once per subject. `seededFor` holds the employee the roster was applied for,
  //     so switching employee re-seeds and re-rendering does not.
  const [rosterApplied, setRosterApplied] = useState<string[] | null>(null);
  const seededFor = useRef<string | null>(null);
  const subjectId = form.watch('employeeId');

  const { data: roster } = useQuery({
    queryKey: ['hr', 'employee-relievers', 'employee', subjectId],
    queryFn: () => employeeRelieverService.getForEmployee(subjectId, true),
    // Only for the person the request is for, and only while composing a new one. An HR actor
    // raising a request on someone else's behalf reads that person's roster; the endpoint is gated
    // self-or-HR, so a 403 here would mean the caller had no business seeing it anyway.
    enabled: !isEdit && !!subjectId,
  });

  useEffect(() => {
    if (isEdit || !subjectId || !roster) return;
    if (seededFor.current === subjectId) return;
    seededFor.current = subjectId;

    // Already ordered by priority server-side; the filter drops anyone who cannot legitimately
    // cover — themselves, or a row switched off.
    const usable = roster.filter((r) => r.isActive && r.relieverEmployeeId !== subjectId);
    if (usable.length === 0) return;

    const applied: string[] = [];
    if (!form.getValues('relieverEmployeeId') && usable[0]) {
      form.setValue('relieverEmployeeId', usable[0].relieverEmployeeId, { shouldValidate: true });
      applied.push(`${usable[0].relieverName} (priority ${usable[0].priority})`);
    }
    if (!form.getValues('secondRelieverEmployeeId') && usable[1]) {
      form.setValue('secondRelieverEmployeeId', usable[1].relieverEmployeeId, { shouldValidate: true });
      applied.push(`${usable[1].relieverName} (priority ${usable[1].priority})`);
    }
    setRosterApplied(applied.length ? applied : null);
  }, [isEdit, subjectId, roster, form]);

  const employeeId = form.watch('employeeId');
  const leaveTypeId = form.watch('leaveTypeId');
  const startDate = form.watch('startDate');
  const endDate = form.watch('endDate');

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const { data: subTypes } = useQuery({
    queryKey: ['hr', 'leave-types', leaveTypeId, 'sub-types', 'active'],
    queryFn: () => leaveTypeService.getSubTypes(leaveTypeId, true),
    enabled: !!leaveTypeId,
  });

  // Show what the employee actually has left for the chosen type — the most common
  // reason a request gets rejected downstream.
  const { data: balances } = useQuery({
    queryKey: ['hr', 'leave-balances', 'employee', employeeId],
    queryFn: () => leaveService.getEmployeeBalances(employeeId),
    enabled: !!employeeId,
  });

  const balance = balances?.find((b) => b.leaveTypeId === leaveTypeId);
  const selectedType = leaveTypes?.find((t) => t.id === leaveTypeId);

  // Calendar-day span; the server computes the authoritative working-day total.
  const spanDays =
    startDate && endDate && endDate >= startDate
      ? Math.round(
          (new Date(endDate).getTime() - new Date(startDate).getTime()) / 86_400_000,
        ) + 1
      : null;


  /*
   * ⚠ L-15, reshaped by G3. The finding was "no attachment can be added while raising a
   * request", and the fix is NOT an uploader here: the controlled upload gate needs a request id,
   * so there is nothing to attach a file to until the record exists. Save as draft already does
   * that, and both forms have the button.
   *
   * What G3 changed is the cost of not knowing. A sick-leave request longer than the
   * self-certification period is now REFUSED at submit, so somebody fills the whole form, presses
   * Submit, and is told to go and get a certificate - having never been warned. That is the actual
   * complaint, and it is answered by saying so BEFORE the button, and naming the route.
   */
  const needsCertificate =
    !!selectedType?.requiresMedicalCertificate &&
    spanDays !== null &&
    spanDays > (selectedType.selfCertificationDays ?? 0);
  return (
    <Card className="max-w-3xl">
      <form onSubmit={form.handleSubmit((v) => onSubmit(v, false))}>
        <CardHeader>
          <CardTitle>Leave Request</CardTitle>
          <CardDescription>
            Submitting starts the approval workflow configured for leave requests.
          </CardDescription>
        </CardHeader>


        {/* Said before Submit, not after it is refused. */}
        {needsCertificate && (
          <div className="mx-6 rounded-md border border-amber-300/60 bg-amber-50 p-3 text-sm dark:border-amber-900/60 dark:bg-amber-950/40">
            <p className="font-medium">
              This needs excuse duty — a medical certificate — attached before it can be submitted.
            </p>
            <p className="mt-1">
              {selectedType?.name} allows {selectedType?.selfCertificationDays ?? 0} day(s) on the
              employee&apos;s own word, and this is {spanDays}.{' '}
              <strong>Save as draft</strong>, attach the certificate on the request, then submit it.
            </p>
          </div>
        )}

        {/* Say where the dates came from, so nobody wonders why the form arrived filled in. */}
        {form.watch('leavePlanId') && (
          <div className="mx-6 rounded-md border border-primary/30 bg-primary/5 p-3 text-sm">
            Raised from an approved leave plan. The dates and relievers are the ones planned — change
            them here if they have moved, and the request will still be linked to the plan. For any
            other kind of leave, raise a request without the plan.
          </div>
        )}

        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Employee</Label>
            <EmployeePicker
              value={form.watch('employeeId') || null}
              onChange={(v) => form.setValue('employeeId', v ?? '', { shouldValidate: true })}
            />
            {form.formState.errors.employeeId && (
              <p className="text-sm text-red-500">{form.formState.errors.employeeId.message}</p>
            )}
          </div>

          <FieldRow>
            <SelectField
              form={form}
              name="leaveTypeId"
              label="Leave type"
              required
              options={(leaveTypes ?? []).map((t) => ({ value: t.id, label: t.name }))}
              // A request raised from a plan is that plan's leave; the server refuses any other.
              disabled={!!form.watch('leavePlanId')}
              description={form.watch('leavePlanId') ? 'The leave the plan is for.' : undefined}
            />
            <SelectField
              form={form}
              name="leaveSubTypeId"
              label="Sub-type"
              options={(subTypes ?? []).map((s) => ({ value: s.id, label: s.subTypeName }))}
              allowEmpty
            />
          </FieldRow>

          {/*
            Two figures, and the one that binds goes first. `availableDays` is the policy balance
            (entitled for the whole year); `accruedAvailableDays` is what the server's create check
            enforces, which on an accruing type counts only what has accrued so far. Showing only the
            policy figure invited requests the server then refused (closure plan L-14).
          */}
          {/*
            Round 5, A5: an OTHER kind is a limit, not a balance, and reads as one. The same figures,
            put the way sick or casual leave is actually thought about.
          */}
          {balance && (balance.leaveTypeCategory ?? selectedType?.category) === 'Other' && (
            <div className="rounded-md border bg-muted/40 p-3 text-sm">
              <span className="text-muted-foreground">{balance.leaveTypeName}: </span>
              <span className="font-medium">Limit {balance.entitledDays}</span>
              <span className="text-muted-foreground">
                {' · '}
                {balance.usedDays} used
                {balance.pendingDays > 0 ? ` · ${balance.pendingDays} waiting` : ''}
                {' · '}
              </span>
              <span className="font-medium">{balance.accruedAvailableDays} left</span>
            </div>
          )}
          {balance && (balance.leaveTypeCategory ?? selectedType?.category) !== 'Other' && (
            <div className="rounded-md border bg-muted/40 p-3 text-sm">
              <span className="text-muted-foreground">Can be taken now for {balance.leaveTypeName}: </span>
              <span className="font-medium">{balance.accruedAvailableDays} days</span>
              <span className="text-muted-foreground">
                {' '}
                (entitled {balance.entitledDays}, used {balance.usedDays}, pending{' '}
                {balance.pendingDays})
              </span>
              {balance.accruedAvailableDays !== balance.availableDays && (
                <p className="mt-1 text-xs text-muted-foreground">
                  {balance.availableDays} days for the full year — this leave type accrues, so{' '}
                  {Math.round((balance.availableDays - balance.accruedAvailableDays) * 100) / 100}{' '}
                  of them have not accrued yet
                  {balance.accruedAsOf ? ` (as at ${fmtDay(balance.accruedAsOf)})` : ''}.
                </p>
              )}
            </div>
          )}

          <FieldRow>
            <DateField form={form} name="startDate" label="Start date" required />
            <DateField form={form} name="endDate" label="End date" required />
          </FieldRow>

          {spanDays !== null && (
            <p className="text-xs text-muted-foreground">
              {spanDays} calendar day{spanDays === 1 ? '' : 's'} selected
              {selectedType && !selectedType.countWeekendsAsLeave
                ? ' — weekends are not counted, so the chargeable total will be lower.'
                : '.'}
            </p>
          )}

          <TextareaField form={form} name="reason" label="Reason" rows={3} />

          {rosterApplied && (
            // Filling a field without saying so is how a form starts lying to the person using it.
            <p className="rounded-md border border-blue-200 bg-blue-50 p-3 text-xs text-blue-900">
              Filled from this employee&apos;s reliever roster: {rosterApplied.join(', ')}. Change
              either one freely — the roster is only a starting point, and what you save here is
              what counts for this request.
            </p>
          )}

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Reliever</Label>
              <EmployeePicker
                value={form.watch('relieverEmployeeId') || null}
                onChange={(v) =>
                  form.setValue('relieverEmployeeId', v ?? '', { shouldValidate: true })
                }
              />
              {form.formState.errors.relieverEmployeeId && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.relieverEmployeeId.message}
                </p>
              )}
              {selectedType?.requiresReliever && !form.watch('relieverEmployeeId') && (
                <p className="text-xs text-amber-600">This leave type expects a reliever.</p>
              )}
            </div>
            <div className="space-y-2">
              <Label>Second reliever</Label>
              <EmployeePicker
                value={form.watch('secondRelieverEmployeeId') || null}
                onChange={(v) =>
                  form.setValue('secondRelieverEmployeeId', v ?? '', { shouldValidate: true })
                }
              />
              {form.formState.errors.secondRelieverEmployeeId && (
                <p className="text-sm text-red-500">
                  {form.formState.errors.secondRelieverEmployeeId.message}
                </p>
              )}
            </div>
          </div>

          <TextareaField form={form} name="relieverNotes" label="Reliever notes" />
          <TextareaField form={form} name="handoverNotes" label="Handover notes" />
        </CardContent>

        <CardFooter className="flex justify-end gap-2">
          <Button variant="outline" type="button" onClick={onCancel} disabled={submitting}>
            Cancel
          </Button>
          {!isEdit && (
            <Button
              variant="outline"
              type="button"
              disabled={submitting}
              onClick={form.handleSubmit((v) => onSubmit(v, true))}
            >
              <Save className="mr-2 h-4 w-4" />
              Save as draft
            </Button>
          )}
          <Button type="submit" disabled={submitting}>
            {submitting ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Send className="mr-2 h-4 w-4" />
            )}
            {isEdit ? 'Save changes' : 'Submit request'}
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}

export const leaveRequestFormToPayload = (v: LeaveRequestFormValues, saveAsDraft: boolean) => ({
  employeeId: v.employeeId,
  leaveTypeId: v.leaveTypeId,
  leaveSubTypeId: v.leaveSubTypeId || null,
  startDate: v.startDate,
  endDate: v.endDate,
  reason: v.reason,
  relieverEmployeeId: v.relieverEmployeeId || null,
  secondRelieverEmployeeId: v.secondRelieverEmployeeId || null,
  relieverNotes: v.relieverNotes || null,
  handoverNotes: v.handoverNotes || null,
  // Written when the request came from an approved plan, so the planning cycle joins up instead of
  // dead-ending and the employee re-keying their own dates (closure plan L-9).
  leavePlanId: v.leavePlanId || null,
  saveAsDraft,
});
