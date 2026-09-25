'use client';

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle } from 'lucide-react';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { useLeavePermissions } from '@/components/hr/leave/use-leave-permissions';
import { RelieverChooser, clashLine, toRosterRelievers } from '@/components/hr/leave/LeavePlanRelievers';
import {
  CancelPlanDialog,
  ProposePlanDatesDialog,
  RejectPlanDialog,
  SuggestPlanDatesDialog,
} from '@/components/hr/leave/LeavePlanDialogs';
import { LeavePlanDetailDialog, canCancelPlan } from '@/components/hr/leave/LeavePlanDetailDialog';
import { useAuth } from '@/hooks/use-auth';
import { leavePlanService } from '@/services/hr/leave.service';
import { leaveTypeService } from '@/services/hr/leave-type.service';
import { employeeRelieverService } from '@/services/hr/employee-reliever.service';
import type { LeavePlan } from '@/types/hr/leave-request';
import { DateField, FieldRow, SelectField, TextareaField } from '@/components/hr/employee/tabs/fields';

/**
 * The plan's sub-type. Its own component because `renderFields` runs inside a render callback and a
 * query there would be a conditionally-called hook. The payload and `toForm` have always carried
 * `leaveSubTypeId`; the dialog simply never offered it, so a plan could not name the variant of
 * leave it was for (closure plan L-16). Hidden when the chosen type has no sub-types.
 */
function PlanSubTypeField({ form }: { form: any }) {
  const leaveTypeId = form.watch('leaveTypeId') || '';

  const { data: subTypes } = useQuery({
    queryKey: ['hr', 'leave-types', leaveTypeId, 'sub-types', 'active'],
    queryFn: () => leaveTypeService.getSubTypes(leaveTypeId, true),
    enabled: !!leaveTypeId,
  });

  if (!leaveTypeId || !subTypes?.length) return null;

  return (
    <SelectField
      form={form}
      name="leaveSubTypeId"
      label="Sub-type"
      options={subTypes.map((st) => ({ value: st.id, label: st.subTypeName }))}
      allowEmpty
    />
  );
}

/**
 * Both reliever slots, with the chosen employee's own reliever list offered first (round 5 lane E2).
 * A component for the same reason as the sub-type field — the roster is a query.
 *
 * Leaving both slots empty is fine: the server fills them from the roster on save, skipping anyone
 * who is not free over the dates.
 */
function PlanRelieverFields({ form }: { form: any }) {
  const employeeId: string = form.watch('employeeId') || '';
  const startDate: string = form.watch('startDate') || '';
  const endDate: string = form.watch('endDate') || '';
  const planId: string = form.watch('id') || '';
  const relieverId: string = form.watch('relieverId') || '';
  const secondRelieverId: string = form.watch('secondRelieverId') || '';

  const { data: roster, isLoading } = useQuery({
    queryKey: ['hr', 'employee-relievers', employeeId, 'active'],
    queryFn: () => employeeRelieverService.getForEmployee(employeeId, true),
    enabled: !!employeeId,
  });
  const offered = employeeId ? toRosterRelievers(roster) : undefined;

  return (
    <>
      <RelieverChooser
        label="Reliever"
        value={relieverId || null}
        valueLabel={form.watch('relieverLabel') || null}
        onChange={(id, label) => {
          form.setValue('relieverId', id ?? '');
          form.setValue('relieverLabel', label ?? '');
        }}
        roster={offered}
        rosterLoading={isLoading}
        excludeIds={[secondRelieverId, employeeId]}
        startDate={startDate}
        endDate={endDate}
        excludePlanId={planId || undefined}
      />
      <RelieverChooser
        label="Second reliever"
        value={secondRelieverId || null}
        valueLabel={form.watch('secondRelieverLabel') || null}
        onChange={(id, label) => {
          form.setValue('secondRelieverId', id ?? '');
          form.setValue('secondRelieverLabel', label ?? '');
        }}
        roster={offered}
        rosterLoading={isLoading}
        excludeIds={[relieverId, employeeId]}
        startDate={startDate}
        endDate={endDate}
        excludePlanId={planId || undefined}
      />
      {!relieverId && !secondRelieverId && (
        <p className="text-xs text-muted-foreground">
          Leave both empty and they are filled from the employee&apos;s reliever list when the plan is
          saved, skipping anyone who is not free over the dates.
        </p>
      )}
    </>
  );
}

const currentYear = new Date().getFullYear();
const years = [currentYear + 1, currentYear, currentYear - 1];

const schema = z
  .object({
    // Carried so an edit can exclude the plan itself from its own reliever check.
    id: z.string().optional().or(z.literal('')),
    employeeId: z.string().min(1, 'Employee is required'),
    // The names the pickers show for an existing choice. Never sent: display only.
    employeeLabel: z.string().optional().or(z.literal('')),
    leaveTypeId: z.string().min(1, 'Leave type is required'),
    leaveSubTypeId: z.string().optional().or(z.literal('')),
    startDate: z.string().min(1, 'Start date is required'),
    endDate: z.string().min(1, 'End date is required'),
    relieverId: z.string().optional().or(z.literal('')),
    relieverLabel: z.string().optional().or(z.literal('')),
    secondRelieverId: z.string().optional().or(z.literal('')),
    secondRelieverLabel: z.string().optional().or(z.literal('')),
    notes: z.string().max(1000).optional().or(z.literal('')),
  })
  .refine((v) => v.endDate >= v.startDate, {
    message: 'End date cannot be before the start date',
    path: ['endDate'],
  })
  .refine((v) => !v.relieverId || v.relieverId !== v.employeeId, {
    message: 'An employee cannot be their own reliever',
    path: ['relieverId'],
  })
  .refine((v) => !v.secondRelieverId || v.secondRelieverId !== v.employeeId, {
    message: 'An employee cannot be their own reliever',
    path: ['secondRelieverId'],
  });

type FormValues = z.infer<typeof schema>;

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
 * employee then accepts or answers with dates of their own.
 *
 * Round 5 lane E: a row opens the plan's detail window, with every action it allows inside it; Edit
 * is offered only on a draft; and `?planId=` — the approvals inbox's link — opens that plan
 * directly, for the line manager too, who cannot read the list.
 */
export default function LeavePlansPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const { canWrite } = useLeavePermissions();
  const currentEmployeeId = (user?.employeeId as string | undefined) ?? null;

  const [year, setYear] = useState(String(currentYear));
  const [openPlanId, setOpenPlanId] = useState<string | null>(searchParams.get('planId'));
  const [suggestFor, setSuggestFor] = useState<LeavePlan | null>(null);
  const [rejectFor, setRejectFor] = useState<LeavePlan | null>(null);
  const [proposeFor, setProposeFor] = useState<LeavePlan | null>(null);
  const [cancelFor, setCancelFor] = useState<LeavePlan | null>(null);
  const [prefill, setPrefill] = useState<FormValues | null>(null);

  const { data: leaveTypes } = useQuery({
    queryKey: ['hr', 'leave-types', 'active'],
    queryFn: () => leaveTypeService.getAll(true),
  });

  const queryKey = ['hr', 'leave-plans', year];
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'leave-plans'] });

  const closeDetail = () => {
    setOpenPlanId(null);
    // Drop the inbox's ?planId= so a refresh does not reopen the plan.
    if (searchParams.get('planId')) router.replace('/hr/leave/plans');
  };

  const empty: FormValues = {
    id: '',
    employeeId: '',
    employeeLabel: '',
    leaveTypeId: '',
    leaveSubTypeId: '',
    startDate: '',
    endDate: '',
    relieverId: '',
    relieverLabel: '',
    secondRelieverId: '',
    secondRelieverLabel: '',
    notes: '',
  };

  // Who planned it is stamped server-side from the token: PlannedBy is an Employee foreign key,
  // and the login's user id this screen used to send was never one. The plan's YEAR is likewise
  // derived server-side from its start date — this screen used to send the list filter's year, so a
  // January plan raised from the December list was filed under 2026 and then shown by neither
  // (closure plan L-17).
  const toPayload = (v: FormValues) => ({
    employeeId: v.employeeId,
    leaveTypeId: v.leaveTypeId,
    leaveSubTypeId: v.leaveSubTypeId || null,
    startDate: v.startDate,
    endDate: v.endDate,
    relieverId: v.relieverId || null,
    secondRelieverId: v.secondRelieverId || null,
    notes: v.notes || null,
    organizationLevelId: null,
    organizationUnitId: null,
    positionId: null,
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Leave Plans"
        description="Planned leave for the year, approved ahead of the actual request. Open a plan to act on it."
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
        dialogHint="Plan leave ahead for the year. Plan several periods if the leave is to be spread out."
        emptyDescription="No plans recorded for this year."
        getId={(p) => p.id}
        list={() => leavePlanService.getByYear(Number(year))}
        create={(_p, v) => leavePlanService.create(toPayload(v))}
        update={(_p, id, v) => leavePlanService.update(id, toPayload(v))}
        // A plan past Draft is not edited: its relievers change in the detail window, its dates go
        // back to the employee (round 5 lane E3/E4).
        canEditItem={(p) => p.status === 'Draft'}
        onOpenItem={(p) => setOpenPlanId(p.id)}
        openItemLabel="View details"
        prefill={prefill}
        onPrefillConsumed={() => setPrefill(null)}
        actions={[
          {
            // The planning cycle's missing last step. `LeaveRequest.LeavePlanId` has existed since
            // the port and nothing wrote it, so an approved plan dead-ended here and the employee
            // re-keyed the dates they had already agreed (closure plan L-9 / R-1). Offered only on
            // an APPROVED plan that has not been spent; the server enforces both again.
            label: 'Raise the leave request',
            visible: (p) => p.status === 'Approved' && !p.raisedLeaveRequestId,
            run: async (p) => {
              const q = new URLSearchParams({
                planId: p.id,
                employeeId: p.employeeId,
                leaveTypeId: p.leaveTypeId,
                startDate: p.startDate?.slice(0, 10) ?? '',
                endDate: p.endDate?.slice(0, 10) ?? '',
              });
              if (p.leaveSubTypeId) q.set('leaveSubTypeId', p.leaveSubTypeId);
              if (p.relieverId) q.set('relieverId', p.relieverId);
              if (p.secondRelieverId) q.set('secondRelieverId', p.secondRelieverId);
              router.push(`/hr/leave/requests/new?${q.toString()}`);
            },
          },
          {
            label: 'Submit for approval',
            visible: (p) => p.status === 'Draft',
            run: (p) => leavePlanService.submit(p.id),
          },
          {
            // Only while the plan is waiting for a decision. A ChangesSuggested plan is waiting for
            // the EMPLOYEE; its approval was withdrawn, and approving it used to come back as 403.
            label: 'Approve',
            visible: (p) => p.status === 'Submitted',
            run: (p) => leavePlanService.approve(p.id),
            confirm: {
              title: 'Approve leave plan?',
              description: 'The workflow engine records the decision and advances the plan.',
            },
          },
          {
            label: 'Reject…',
            destructive: true,
            visible: (p) => p.status === 'Submitted',
            run: async (p) => setRejectFor(p),
          },
          {
            label: 'Suggest different dates…',
            visible: (p) => p.status === 'Submitted',
            run: async (p) => setSuggestFor(p),
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
            // Was "Decline suggestion", which sent no dates and was always refused — declining a
            // suggestion means proposing dates of one's own (round 5 lane E4).
            label: 'Propose other dates…',
            visible: (p) => p.status === 'ChangesSuggested',
            run: async (p) => setProposeFor(p),
          },
          {
            // Spreading annual leave across the year is several plans (round 5 lane E6). This opens
            // a new plan for the same person, type and relievers, with the dates left to fill in.
            label: 'Plan another period',
            visible: (p) => p.status !== 'Cancelled' && p.status !== 'Rejected',
            run: async (p) =>
              setPrefill({
                ...empty,
                employeeId: p.employeeId,
                employeeLabel: p.employeeName,
                leaveTypeId: p.leaveTypeId,
                leaveSubTypeId: p.leaveSubTypeId ?? '',
                relieverId: p.relieverId ?? '',
                relieverLabel: p.relieverName ?? '',
                secondRelieverId: p.secondRelieverId ?? '',
                secondRelieverLabel: p.secondRelieverName ?? '',
              }),
          },
          {
            label: 'Cancel plan…',
            destructive: true,
            visible: (p) => canCancelPlan(p, canWrite, !!currentEmployeeId && p.employeeId === currentEmployeeId),
            run: async (p) => setCancelFor(p),
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
          {
            header: 'Request',
            cell: (p) =>
              p.raisedLeaveRequestId ? (
                <Link
                  href={`/hr/leave/requests/${p.raisedLeaveRequestId}`}
                  className="text-primary underline-offset-2 hover:underline"
                  onClick={(e) => e.stopPropagation()}
                >
                  {p.raisedLeaveRequestNumber ?? 'View'}
                </Link>
              ) : (
                '—'
              ),
          },
          { header: 'Status', cell: (p) => <StatusBadge status={p.status} /> },
        ]}
        schema={schema}
        emptyForm={empty}
        dialogClassName="sm:max-w-[680px]"
        toForm={(p) => ({
          id: p.id,
          employeeId: p.employeeId,
          employeeLabel: p.employeeName ?? '',
          leaveTypeId: p.leaveTypeId,
          leaveSubTypeId: p.leaveSubTypeId ?? '',
          startDate: p.startDate?.slice(0, 10) ?? '',
          endDate: p.endDate?.slice(0, 10) ?? '',
          relieverId: p.relieverId ?? '',
          relieverLabel: p.relieverName ?? '',
          secondRelieverId: p.secondRelieverId ?? '',
          secondRelieverLabel: p.secondRelieverName ?? '',
          notes: p.notes ?? '',
        })}
        renderFields={(form) => (
          <>
            <div className="space-y-2">
              <Label>Employee</Label>
              <EmployeePicker
                value={form.watch('employeeId') || null}
                initialLabel={form.watch('employeeLabel') || null}
                onChange={(v, label) => {
                  form.setValue('employeeId', v ?? '', { shouldValidate: true });
                  form.setValue('employeeLabel', label ?? '');
                }}
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
              // Plans are for annual leave (round 5, A4 / lane A2); the server refuses other kinds.
              options={(leaveTypes ?? [])
                .filter((t) => t.category === 'Annual')
                .map((t) => ({ value: t.id, label: t.name }))}
            />
            <PlanSubTypeField form={form} />
            <FieldRow>
              <DateField form={form} name="startDate" label="Start date" required />
              <DateField form={form} name="endDate" label="End date" required />
            </FieldRow>
            <PlanRelieverFields form={form} />
            {(form.formState.errors.relieverId || form.formState.errors.secondRelieverId) && (
              <p className="text-sm text-red-500">
                {form.formState.errors.relieverId?.message ?? form.formState.errors.secondRelieverId?.message}
              </p>
            )}
            <TextareaField form={form} name="notes" label="Notes" />
          </>
        )}
      />

      <LeavePlanDetailDialog
        planId={openPlanId}
        onClose={closeDetail}
        canWrite={canWrite}
        currentEmployeeId={currentEmployeeId}
        onChanged={invalidate}
      />
      <SuggestPlanDatesDialog plan={suggestFor} onClose={() => setSuggestFor(null)} onDone={invalidate} />
      <RejectPlanDialog plan={rejectFor} onClose={() => setRejectFor(null)} onDone={invalidate} />
      <ProposePlanDatesDialog plan={proposeFor} onClose={() => setProposeFor(null)} onDone={invalidate} />
      <CancelPlanDialog plan={cancelFor} onClose={() => setCancelFor(null)} onDone={invalidate} />
    </div>
  );
}
