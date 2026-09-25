'use client';

/**
 * Area 25 slice 4 — the portal's own leave request form.
 *
 * The desk `LeaveRequestForm` is HR-shaped: an EmployeePicker for the subject and pickers
 * for relievers, all backed by the desk-gated employee search (`POST hr/Employees/paged`,
 * EmployeeReadPolicy) that a plain employee cannot call. Here the subject is ALWAYS the
 * signed-in employee, and reliever choices come from the caller's own roster
 * (`employee-relievers/mine`, self-armed) — with the server's auto-fill (roster by
 * priority, then the line manager) covering the empty case, which the copy says out loud.
 */

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Save, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { leaveService } from '@/services/hr/leave.service';
import { employeeRelieverService } from '@/services/hr/employee-reliever.service';
import { DateField, FieldRow, SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';

export const myLeaveRequestSchema = z
  .object({
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
  .refine(
    (v) =>
      !v.secondRelieverEmployeeId || v.secondRelieverEmployeeId !== v.relieverEmployeeId,
    {
      message: 'The two relievers must be different people',
      path: ['secondRelieverEmployeeId'],
    },
  );

export type MyLeaveRequestFormValues = z.infer<typeof myLeaveRequestSchema>;

export const emptyMyLeaveRequest: MyLeaveRequestFormValues = {
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

export const myLeaveRequestToPayload = (
  employeeId: string,
  v: MyLeaveRequestFormValues,
  saveAsDraft: boolean,
) => ({
  employeeId,
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

interface MyLeaveRequestFormProps {
  employeeId: string;
  defaultValues: MyLeaveRequestFormValues;
  onSubmit: (values: MyLeaveRequestFormValues, saveAsDraft: boolean) => Promise<void>;
  submitting: boolean;
  onCancel: () => void;
  /** Editing an existing draft only offers Save. */
  isEdit?: boolean;
}

export function MyLeaveRequestForm({
  employeeId,
  defaultValues,
  onSubmit,
  submitting,
  onCancel,
  isEdit = false,
}: MyLeaveRequestFormProps) {
  const form = useForm<MyLeaveRequestFormValues>({
    resolver: zodResolver(myLeaveRequestSchema) as any,
    defaultValues,
  });

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

  const { data: balances } = useQuery({
    queryKey: ['me', 'leave-balances', employeeId],
    queryFn: () => leaveService.getEmployeeBalances(employeeId),
  });

  // The caller's own roster is the whole universe of pickable relievers here — a plain
  // employee has no employee-search permission, and that is by design (D8: no new grants).
  const { data: roster } = useQuery({
    queryKey: ['me', 'relievers', 'mine'],
    queryFn: () => employeeRelieverService.getMine(true),
  });

  const relieverOptions = (roster ?? [])
    .filter((r) => r.relieverEmployeeId !== employeeId)
    .map((r) => ({
      value: r.relieverEmployeeId,
      label: `${r.relieverName} (priority ${r.priority})`,
    }));

  const balance = balances?.find((b) => b.leaveTypeId === leaveTypeId);
  const selectedType = leaveTypes?.find((t) => t.id === leaveTypeId);


  // Calendar-day span; the server computes the authoritative chargeable total.
  const spanDays =
    startDate && endDate && endDate >= startDate
      ? Math.round(
          (new Date(endDate).getTime() - new Date(startDate).getTime()) / 86_400_000,
        ) + 1
      : null;
  /*
   * ⚠ L-15, reshaped by G3 — see the note on the HR desk form. There is no uploader here
   * because the controlled upload gate needs a request id, so nothing exists to attach a file
   * to until the record does. What matters is not being REFUSED at submit with no warning.
   */
  const needsCertificate =
    !!selectedType?.requiresMedicalCertificate &&
    spanDays !== null &&
    spanDays > (selectedType.selfCertificationDays ?? 0);

  return (
    <Card>
      <form onSubmit={form.handleSubmit((v) => onSubmit(v, false))}>
        <CardHeader>
          <CardTitle>{isEdit ? 'Edit draft request' : 'Request leave'}</CardTitle>
          <CardDescription>
            {isEdit
              ? 'Only drafts can be edited. Submit it when the details are right.'
              : 'Submitting sends the request into the approval flow configured for leave.'}
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">
          <FieldRow>
            <SelectField
              form={form}
              name="leaveTypeId"
              label="Leave type"
              required
              options={(leaveTypes ?? []).map((t) => ({ value: t.id, label: t.name }))}
            />
            <SelectField
              form={form}
              name="leaveSubTypeId"
              label="Sub-type"
              options={(subTypes ?? []).map((s) => ({ value: s.id, label: s.subTypeName }))}
              allowEmpty
            />
          </FieldRow>

          {/* Say where the dates came from, so nobody wonders why the form arrived filled in. */}
          {form.watch('leavePlanId') && (
            <div className="rounded-md border border-primary/30 bg-primary/5 p-3 text-sm">
              Raised from your approved leave plan. These are the dates you agreed — change them if
              they have moved, and the request stays linked to the plan.
            </div>
          )}

          {/*
            Two figures, and the one that binds goes first. `availableDays` is the policy balance
            (entitled for the whole year); `accruedAvailableDays` is what the server's create check
            enforces, which on an accruing type counts only what has accrued so far. Showing only the
            policy figure invited requests the server then refused (closure plan L-14).
          */}
          {/* Round 5, A5: an OTHER kind is a limit, not a balance, and reads as one. */}
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
              <span className="text-muted-foreground">
                Your balance for {balance.leaveTypeName}:{' '}
              </span>
              <span className="font-medium">
                {balance.accruedAvailableDays} days you can take now
              </span>
              <span className="text-muted-foreground">
                {' '}
                (entitled {balance.entitledDays}, used {balance.usedDays}, pending{' '}
                {balance.pendingDays})
              </span>
              {balance.accruedAvailableDays !== balance.availableDays && (
                <p className="mt-1 text-xs text-muted-foreground">
                  {balance.availableDays} days for the full year — this leave type accrues, so{' '}
                  {Math.round((balance.availableDays - balance.accruedAvailableDays) * 100) / 100}{' '}
                  of them have not accrued yet.
                </p>
              )}
            </div>
          )}

          {selectedType?.minDaysNotice ? (
            <p className="text-xs text-muted-foreground">
              This leave type needs at least {selectedType.minDaysNotice} day
              {selectedType.minDaysNotice === 1 ? '' : 's'} notice before it starts. Drafts
              can be saved any time.
            </p>
          ) : null}

          {/* Said before Submit, not after it is refused. */}
          {needsCertificate && (
            <div className="rounded-md border border-amber-300/60 bg-amber-50 p-3 text-sm dark:border-amber-900/60 dark:bg-amber-950/40">
              <p className="font-medium">
                You will need a medical certificate — excuse duty — for this.
              </p>
              <p className="mt-1">
                {selectedType?.name} lets you take {selectedType?.selfCertificationDays ?? 0} day(s)
                on your own word, and you have chosen {spanDays}.{' '}
                <strong>Save it as a draft</strong>, attach the certificate, then submit it.
              </p>
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

          {relieverOptions.length > 0 ? (
            <FieldRow>
              <SelectField
                form={form}
                name="relieverEmployeeId"
                label="Reliever"
                options={relieverOptions}
                allowEmpty
              />
              <SelectField
                form={form}
                name="secondRelieverEmployeeId"
                label="Second reliever"
                options={relieverOptions}
                allowEmpty
              />
            </FieldRow>
          ) : (
            <p className="rounded-md border bg-muted/40 p-3 text-xs text-muted-foreground">
              You have no pre-defined relievers, so one will be assigned automatically when
              you submit — usually your manager. Ask HR to set up your reliever roster if
              someone specific should cover for you.
            </p>
          )}
          {relieverOptions.length > 0 && (
            <p className="text-xs text-muted-foreground">
              Leave the reliever empty to let the system assign from your roster (or your
              manager) automatically.
            </p>
          )}

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
