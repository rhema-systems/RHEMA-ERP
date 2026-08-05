'use client';

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
import { DateField, FieldRow, SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';

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

  const employeeId = form.watch('employeeId');
  const leaveTypeId = form.watch('leaveTypeId');
  const startDate = form.watch('startDate');
  const endDate = form.watch('endDate');

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const { data: subTypes } = useQuery({
    queryKey: ['hr', 'leave-types', leaveTypeId, 'sub-types'],
    queryFn: () => leaveTypeService.getSubTypes(leaveTypeId),
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

  return (
    <Card className="max-w-3xl">
      <form onSubmit={form.handleSubmit((v) => onSubmit(v, false))}>
        <CardHeader>
          <CardTitle>Leave Request</CardTitle>
          <CardDescription>
            Submitting starts the approval workflow configured for leave requests.
          </CardDescription>
        </CardHeader>

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
            />
            <SelectField
              form={form}
              name="leaveSubTypeId"
              label="Sub-type"
              options={(subTypes ?? []).map((s) => ({ value: s.id, label: s.subTypeName }))}
              allowEmpty
            />
          </FieldRow>

          {balance && (
            <div className="rounded-md border bg-muted/40 p-3 text-sm">
              <span className="text-muted-foreground">Available for {balance.leaveTypeName}: </span>
              <span className="font-medium">{balance.availableDays} days</span>
              <span className="text-muted-foreground">
                {' '}
                (entitled {balance.entitledDays}, used {balance.usedDays}, pending{' '}
                {balance.pendingDays})
              </span>
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
  leavePlanId: null,
  saveAsDraft,
});
