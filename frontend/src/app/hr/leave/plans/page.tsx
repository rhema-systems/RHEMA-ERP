'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle } from 'lucide-react';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { leavePlanService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import type { LeavePlan, LeaveRelieverClash } from '@/types/hr/leave-request';
import { DateField, FieldRow, SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';

const currentYear = new Date().getFullYear();
const years = [currentYear + 1, currentYear, currentYear - 1];

const schema = z
  .object({
    // Carried so an edit can exclude the plan itself from its own reliever check.
    id: z.string().optional().or(z.literal('')),
    employeeId: z.string().min(1, 'Employee is required'),
    leaveTypeId: z.string().min(1, 'Leave type is required'),
    leaveSubTypeId: z.string().optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
    relieverId: z.string().optional().or(z.literal('')),
    secondRelieverId: z.string().optional().or(z.literal('')),
    notes: z.string().max(1000).optional().or(z.literal('')),
  })
  .refine((v) => v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  });

type FormValues = z.infer<typeof schema>;

const fmt = (d: string) => d.slice(0, 10);

function clashLine(c: LeaveRelieverClash) {
  return `${c.relieverName || 'Reliever'} ${c.description} (${fmt(c.fromDate)} – ${fmt(c.toDate)})`;
}

/**
 * Whether a chosen reliever is actually free over the plan's dates, asked as the form is filled in.
 *
 * Finish-plan lane 4: TDC's demo feedback was that reliever clashes were not visible on the plan.
 * The server answers from three sources — the reliever's own plans, their own live leave requests,
 * and other plans already naming them — and this is advisory: a plan with a clash can still be
 * saved, because leave gets cancelled and dates move. What the form must never do is stay silent.
 */
function RelieverClashCheck({
  label,
  relieverId,
  startDate,
  endDate,
  excludePlanId,
}: {
  label: string;
  relieverId: string;
  startDate: string;
  endDate: string;
  excludePlanId?: string;
}) {
  const ready = !!relieverId && !!startDate && !!endDate && endDate >= startDate;
  const { data, isFetching } = useQuery({
    queryKey: ['hr', 'leave-plans', 'reliever-clashes', relieverId, startDate, endDate, excludePlanId ?? ''],
    queryFn: () => leavePlanService.getRelieverClashes(relieverId, startDate, endDate, excludePlanId),
    enabled: ready,
  });

  if (!ready) return null;
  if (isFetching && !data) {
    return <p className="text-xs text-muted-foreground">Checking the {label.toLowerCase()}&apos;s diary…</p>;
  }
  const clashes = data ?? [];
  if (clashes.length === 0) {
    return (
      <p className="text-xs text-emerald-700 dark:text-emerald-400">
        {label}: nothing in their diary over these dates.
      </p>
    );
  }
  return (
    <Alert>
      <AlertTriangle className="h-4 w-4" />
      <AlertTitle>
        {label}: {clashes.length} clash{clashes.length === 1 ? '' : 'es'} over these dates
      </AlertTitle>
      <AlertDescription>
        <ul className="mt-1 list-disc space-y-1 pl-4 text-sm">
          {clashes.map((c, i) => (
            <li key={`${c.source}-${i}`}>{clashLine(c)}</li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-muted-foreground">
          You can still save the plan — this is a warning, not a rule.
        </p>
      </AlertDescription>
    </Alert>
  );
}

/** The reliever cell on the register: names, and a red badge when the server found clashes. */
function RelieverCell({ plan }: { plan: LeavePlan }) {
  const names = [plan.relieverName, plan.secondRelieverName].filter(Boolean).join(' · ');
  const clashes = plan.relieverClashes ?? [];
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      <span>{names || '—'}</span>
      {clashes.length > 0 && (
        <Badge
          variant="destructive"
          className="gap-1"
          title={clashes.map(clashLine).join('\n')}
        >
          <AlertTriangle className="h-3 w-3" />
          {clashes.length} clash{clashes.length === 1 ? '' : 'es'}
        </Badge>
      )}
    </div>
  );
}

/**
 * Annual leave plans. Submit/approve/reject go through the workflow engine
 * (LeavePlanWorkflowStatusAdapter); a manager can also suggest different dates, which the
 * employee then accepts or declines.
 */
export default function LeavePlansPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [year, setYear] = useState(String(currentYear));
  const [suggestFor, setSuggestFor] = useState<LeavePlan | null>(null);
  const [rejectFor, setRejectFor] = useState<LeavePlan | null>(null);
  const [suggestStart, setSuggestStart] = useState('');
  const [suggestEnd, setSuggestEnd] = useState('');
  const [suggestNotes, setSuggestNotes] = useState('');
  const [rejectReason, setRejectReason] = useState('');

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const queryKey = ['hr', 'leave-plans', year];
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'leave-plans'] });

  const suggestMutation = useMutation({
    mutationFn: () =>
      leavePlanService.suggestChanges(suggestFor?.id ?? '', {
        suggestedStartDate: suggestStart,
        suggestedEndDate: suggestEnd,
        notes: suggestNotes || null,
      }),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Sent', description: 'Suggested dates sent to the employee.' });
      setSuggestFor(null);
      setSuggestStart('');
      setSuggestEnd('');
      setSuggestNotes('');
    },
    onError: (e: any) =>
      toast({ title: 'Error', description: e?.message || 'Failed.', variant: 'destructive' }),
  });

  const rejectMutation = useMutation({
    mutationFn: () => leavePlanService.reject(rejectFor?.id ?? '', rejectReason.trim()),
    onSuccess: async () => {
      await invalidate();
      toast({ title: 'Rejected', description: 'Leave plan rejected.' });
      setRejectFor(null);
      setRejectReason('');
    },
    onError: (e: any) =>
      toast({ title: 'Error', description: e?.message || 'Failed.', variant: 'destructive' }),
  });

  const empty: FormValues = {
    id: '',
    employeeId: '',
    leaveTypeId: '',
    leaveSubTypeId: '',
    startDate: '',
    endDate: '',
    relieverId: '',
    secondRelieverId: '',
    notes: '',
  };

  // Who planned it is stamped server-side from the token: PlannedBy is an Employee foreign key,
  // and the login's user id this screen used to send was never one.
  const toPayload = (v: FormValues) => ({
    employeeId: v.employeeId,
    leaveTypeId: v.leaveTypeId,
    leaveSubTypeId: v.leaveSubTypeId || null,
    startDate: v.startDate,
    endDate: v.endDate,
    relieverId: v.relieverId || null,
    secondRelieverId: v.secondRelieverId || null,
    notes: v.notes || null,
    year: Number(year),
    organizationLevelId: null,
    organizationUnitId: null,
    positionId: null,
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Plans"
        description="Planned leave for the year, approved ahead of the actual request."
      />

      <Card>
        <CardHeader>
          <CardTitle>Year</CardTitle>
        </CardHeader>
        <CardContent className="max-w-xs">
          <Select value={year} onValueChange={setYear}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {years.map((y) => (
                <SelectItem key={y} value={String(y)}>
                  {y}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </CardContent>
      </Card>

      <ResourceCollectionTab<LeavePlan, FormValues>
        parentId={year}
        title="leave plans"
        singular="leave plan"
        queryKey={queryKey}
        dialogHint="Plan leave ahead for the year."
        emptyDescription="No plans recorded for this year."
        getId={(p) => p.id}
        list={() => leavePlanService.getByYear(Number(year))}
        create={(_p, v) => leavePlanService.create(toPayload(v))}
        update={(_p, id, v) => leavePlanService.update(id, toPayload(v))}
        actions={[
          {
            label: 'Submit for approval',
            visible: (p) => p.status === 'Draft',
            run: (p) => leavePlanService.submit(p.id),
          },
          {
            label: 'Approve',
            visible: (p) => p.status === 'Submitted' || p.status === 'ChangesSuggested',
            run: (p) => leavePlanService.approve(p.id),
            confirm: {
              title: 'Approve leave plan?',
              description: 'The workflow engine records the decision and advances the plan.',
            },
          },
          {
            label: 'Reject…',
            destructive: true,
            visible: (p) => p.status === 'Submitted' || p.status === 'ChangesSuggested',
            run: async (p) => setRejectFor(p),
          },
          {
            label: 'Suggest different dates…',
            visible: (p) => p.status === 'Submitted',
            run: async (p) => {
              setSuggestFor(p);
              setSuggestStart(p.startDate?.slice(0, 10) ?? '');
              setSuggestEnd(p.endDate?.slice(0, 10) ?? '');
            },
          },
          {
            label: 'Accept suggested dates',
            visible: (p) => p.status === 'ChangesSuggested' && !!p.suggestedStartDate,
            run: (p) =>
              leavePlanService.respondToSuggestion(p.id, {
                accept: true,
                startDate: p.suggestedStartDate ?? null,
                endDate: p.suggestedEndDate ?? null,
                notes: null,
              }),
          },
          {
            label: 'Decline suggestion',
            visible: (p) => p.status === 'ChangesSuggested',
            run: (p) => leavePlanService.respondToSuggestion(p.id, { accept: false }),
          },
          {
            label: 'Cancel plan',
            destructive: true,
            visible: (p) => !['Cancelled', 'Rejected'].includes(p.status),
            confirm: { title: 'Cancel this leave plan?' },
            run: (p) => leavePlanService.cancel(p.id),
          },
        ]}
        columns={[
          { header: 'Employee', cell: (p) => p.employeeName },
          {
            header: 'Leave type',
            cell: (p) => `${p.leaveTypeName}${p.leaveSubTypeName ? ` · ${p.leaveSubTypeName}` : ''}`,
          },
          { header: 'From', cell: (p) => p.startDate?.slice(0, 10) },
          { header: 'To', cell: (p) => p.endDate?.slice(0, 10) },
          {
            header: 'Suggested',
            cell: (p) =>
              p.suggestedStartDate
                ? `${p.suggestedStartDate.slice(0, 10)} → ${p.suggestedEndDate?.slice(0, 10) ?? ''}`
                : '—',
          },
          { header: 'Reliever', cell: (p) => <RelieverCell plan={p} /> },
          { header: 'Status', cell: (p) => <StatusBadge status={p.status} /> },
        ]}
        schema={schema}
        emptyForm={empty}
        dialogClassName="sm:max-w-[680px]"
        toForm={(p) => ({
          id: p.id,
          employeeId: p.employeeId,
          leaveTypeId: p.leaveTypeId,
          leaveSubTypeId: p.leaveSubTypeId ?? '',
          startDate: p.startDate?.slice(0, 10) ?? '',
          endDate: p.endDate?.slice(0, 10) ?? '',
          relieverId: p.relieverId ?? '',
          secondRelieverId: p.secondRelieverId ?? '',
          notes: p.notes ?? '',
        })}
        renderFields={(form) => (
          <>
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
            <SelectField
              form={form}
              name="leaveTypeId"
              label="Leave type"
              required
              options={(leaveTypes ?? []).map((t) => ({ value: t.id, label: t.name }))}
            />
            <FieldRow>
              <DateField form={form} name="startDate" label="Start date" required />
              <DateField form={form} name="endDate" label="End date" required />
            </FieldRow>
            <div className="space-y-2">
              <Label>Reliever</Label>
              <EmployeePicker
                value={form.watch('relieverId') || null}
                onChange={(v) => form.setValue('relieverId', v ?? '')}
              />
              <RelieverClashCheck
                label="Reliever"
                relieverId={form.watch('relieverId') || ''}
                startDate={form.watch('startDate') || ''}
                endDate={form.watch('endDate') || ''}
                excludePlanId={form.watch('id') || undefined}
              />
            </div>
            <div className="space-y-2">
              <Label>Second reliever</Label>
              <EmployeePicker
                value={form.watch('secondRelieverId') || null}
                onChange={(v) => form.setValue('secondRelieverId', v ?? '')}
              />
              <RelieverClashCheck
                label="Second reliever"
                relieverId={form.watch('secondRelieverId') || ''}
                startDate={form.watch('startDate') || ''}
                endDate={form.watch('endDate') || ''}
                excludePlanId={form.watch('id') || undefined}
              />
            </div>
            <TextareaField form={form} name="notes" label="Notes" />
          </>
        )}
      />

      <ConfirmationDialog
        open={suggestFor !== null}
        onOpenChange={(open) => !open && setSuggestFor(null)}
        title="Suggest different dates"
        description="The employee is asked to accept or decline these dates."
        confirmText="Send suggestion"
        isLoading={suggestMutation.isPending}
        onConfirm={async () => {
          if (!suggestStart || !suggestEnd) {
            toast({ title: 'Both dates are required', variant: 'destructive' });
            return false;
          }
          await suggestMutation.mutateAsync();
          return true;
        }}
      >
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label htmlFor="suggestStart">Suggested start</Label>
              <input
                id="suggestStart"
                type="date"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={suggestStart}
                onChange={(e) => setSuggestStart(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="suggestEnd">Suggested end</Label>
              <input
                id="suggestEnd"
                type="date"
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm"
                value={suggestEnd}
                onChange={(e) => setSuggestEnd(e.target.value)}
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="suggestNotes">Notes</Label>
            <Textarea
              id="suggestNotes"
              rows={3}
              value={suggestNotes}
              onChange={(e) => setSuggestNotes(e.target.value)}
            />
          </div>
        </div>
      </ConfirmationDialog>

      <ConfirmationDialog
        open={rejectFor !== null}
        onOpenChange={(open) => !open && setRejectFor(null)}
        title="Reject leave plan?"
        confirmText="Reject"
        variant="destructive"
        isLoading={rejectMutation.isPending}
        onConfirm={async () => {
          if (!rejectReason.trim()) {
            toast({ title: 'A reason is required', variant: 'destructive' });
            return false;
          }
          await rejectMutation.mutateAsync();
          return true;
        }}
      >
        <div className="space-y-2">
          <Label htmlFor="planRejectReason">Reason</Label>
          <Textarea
            id="planRejectReason"
            rows={3}
            value={rejectReason}
            onChange={(e) => setRejectReason(e.target.value)}
          />
        </div>
      </ConfirmationDialog>
    </div>
  );
}
