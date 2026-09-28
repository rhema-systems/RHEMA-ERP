'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Play, Flag, Package } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  NumberField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  OnboardingTaskBoard,
  MandatoryOutstandingNote,
} from '@/components/hr/orientation/OnboardingTaskBoard';
import { onboardingPlanService } from '@/services/hr/onboarding.service';
import {
  ONBOARDING_TASK_CATEGORY_OPTIONS,
  ONBOARDING_ASSET_TYPE_OPTIONS,
  ONBOARDING_ASSET_STATUS_OPTIONS,
} from '@/types/hr/onboarding';
import type { OnboardingTask, OnboardingAsset } from '@/types/hr/onboarding';

const taskSchema = z.object({
  taskName: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(2000).optional().or(z.literal('')),
  category: z.enum([
    'Documentation',
    'SystemAccess',
    'Orientation',
    'Training',
    'EquipmentSetup',
    'PayrollSetup',
    'PolicyAcknowledgement',
    'MeetAndGreet',
    'HealthAndSafety',
    'Compliance',
    'Other',
  ]),
  dueDate: z.string().min(1, 'A due date is required'),
  isMandatory: z.boolean(),
  assignedToId: z.string().optional().or(z.literal('')),
  requiresVerification: z.boolean(),
  displayOrder: z.coerce.number().min(0).max(9999),
});
type TaskForm = z.infer<typeof taskSchema>;
const emptyTask: TaskForm = {
  taskName: '',
  description: '',
  category: 'Documentation',
  dueDate: new Date().toISOString().slice(0, 10),
  isMandatory: true,
  assignedToId: '',
  requiresVerification: false,
  displayOrder: 0,
};

const assetSchema = z.object({
  assetType: z.enum([
    'Laptop',
    'Desktop',
    'MobilePhone',
    'AccessCard',
    'ParkingPass',
    'Uniform',
    'SystemAccount',
    'EmailAccount',
    'SoftwareLicence',
    'Keys',
    'Other',
  ]),
  assetName: z.string().min(1, 'A name is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  assetTag: z.string().max(100).optional().or(z.literal('')),
  serialNumber: z.string().max(100).optional().or(z.literal('')),
  requiredByDate: z.string().optional().or(z.literal('')),
  status: z.enum(['Pending', 'Ordered', 'Ready', 'Issued', 'Acknowledged', 'NotRequired']),
  acknowledgedByEmployee: z.boolean(),
  notes: z.string().max(1000).optional().or(z.literal('')),
});
type AssetForm = z.infer<typeof assetSchema>;
const emptyAsset: AssetForm = {
  assetType: 'Laptop',
  assetName: '',
  description: '',
  assetTag: '',
  serialNumber: '',
  requiredByDate: '',
  status: 'Pending',
  acknowledgedByEmployee: false,
  notes: '',
};

const categoryLabel = (v: string) =>
  ONBOARDING_TASK_CATEGORY_OPTIONS.find((o) => o.value === v)?.label ?? v;
const assetTypeLabel = (v: string) =>
  ONBOARDING_ASSET_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

/**
 * One new hire's onboarding: their tasks as a board, plus the kit and accounts being provisioned.
 *
 * Start and complete are explicit acts rather than side effects of the task list — a plan can be
 * complete on paper while somebody is still waiting on a laptop, and closing it is a judgement.
 */
export default function OnboardingPlanDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [confirmAction, setConfirmAction] = useState<'start' | 'complete' | null>(null);
  const [busy, setBusy] = useState(false);

  const planKey = ['hr', 'onboarding-plans', id];
  const { data: plan, isLoading, isError } = useQuery({
    queryKey: planKey,
    queryFn: () => onboardingPlanService.getWithDetails(id),
    enabled: !!id,
  });

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: planKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'onboarding-plans'] }),
    ]);

  const runAction = async () => {
    if (!confirmAction) return false;
    setBusy(true);
    try {
      if (confirmAction === 'start') await onboardingPlanService.start(id);
      else await onboardingPlanService.complete(id);
      await invalidate();
      toast({ title: confirmAction === 'start' ? 'Plan started' : 'Plan completed' });
      setConfirmAction(null);
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Action failed.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (isError || !plan) {
    return (
      <div className="p-6">
        <EmptyState title="Plan not found" description="It may have been removed." />
      </div>
    );
  }

  const pct = plan.totalTasks > 0 ? Math.round((plan.completedTasks / plan.totalTasks) * 100) : 0;
  const isFinished = plan.status === 'Completed' || plan.status === 'Cancelled';
  const tasks: OnboardingTask[] = plan.tasks ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={plan.employeeName}
        description={`${plan.employeeNumber} · starts ${fmt(plan.startDate)}${
          plan.templatePlanName ? ` · from “${plan.templatePlanName}”` : ''
        }`}
        backHref="/hr/orientation/onboarding"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={plan.statusName ?? plan.status} />
            {plan.status === 'NotStarted' && (
              <Button size="sm" onClick={() => setConfirmAction('start')}>
                <Play className="mr-2 h-4 w-4" />
                Start
              </Button>
            )}
            {!isFinished && plan.status !== 'NotStarted' && (
              <Button size="sm" onClick={() => setConfirmAction('complete')}>
                <Flag className="mr-2 h-4 w-4" />
                Mark completed
              </Button>
            )}
          </div>
        }
      />

      <MetricTiles
        tiles={[
          {
            label: 'Tasks done',
            value: `${plan.completedTasks} of ${plan.totalTasks}`,
            hint: `${pct}% complete`,
          },
          {
            label: 'Overdue',
            value: plan.overdueTasks,
            tone: plan.overdueTasks > 0 ? 'danger' : 'default',
          },
          { label: 'Target completion', value: fmt(plan.targetCompletionDate) },
          {
            label: 'Assets',
            value: (plan.assets ?? []).length,
            icon: Package,
            hint: `${(plan.assets ?? []).filter((a) => a.status === 'Issued' || a.status === 'Acknowledged').length} issued`,
          },
        ]}
      />

      <Card>
        <CardContent className="space-y-3 py-4">
          <Progress value={pct} className="h-2" />
          <div className="text-muted-foreground flex flex-wrap gap-x-6 gap-y-1 text-sm">
            <span>Buddy: {plan.assignedBuddyName ?? '—'}</span>
            <span>Coordinator: {plan.onboardingCoordinatorName ?? '—'}</span>
            {plan.actualCompletionDate && (
              <span>Completed {fmt(plan.actualCompletionDate)}</span>
            )}
          </div>
          {plan.notes && <p className="text-sm">{plan.notes}</p>}
          {plan.templateSelectionReason && (
            // Round 4, lane I4: a plan the system created on hire says which template it chose and why.
            <p className="text-muted-foreground text-sm">
              <span className="font-medium">Created automatically.</span> {plan.templateSelectionReason}
            </p>
          )}
          <MandatoryOutstandingNote tasks={tasks} />
        </CardContent>
      </Card>

      <Tabs defaultValue="board">
        <TabsList>
          <TabsTrigger value="board">Board</TabsTrigger>
          <TabsTrigger value="tasks">All tasks ({plan.totalTasks})</TabsTrigger>
          <TabsTrigger value="assets">Assets ({(plan.assets ?? []).length})</TabsTrigger>
        </TabsList>

        <TabsContent value="board" className="pt-4">
          <OnboardingTaskBoard planId={id} readOnly={isFinished} onChanged={invalidate} />
        </TabsContent>

        <TabsContent value="tasks" className="pt-4">
          <ResourceCollectionTab<OnboardingTask, TaskForm>
            parentId={id}
            title="tasks"
            singular="task"
            queryKey={['hr', 'onboarding-plans', id, 'tasks']}
            invalidateKeys={[planKey, ['hr', 'onboarding-plans']]}
            readOnly={isFinished}
            dialogHint="An extra step for this hire, beyond whatever the template supplied."
            emptyDescription="No tasks on this plan."
            list={() => onboardingPlanService.getTasks(id)}
            create={(planId, values) =>
              onboardingPlanService.addTask(planId, {
                onboardingPlanId: planId,
                taskTemplateId: null,
                ...values,
                description: blank(values.description),
                assignedToId: blank(values.assignedToId),
                assignedOrganizationUnitId: null,
              })
            }
            update={(_p, taskId, values) =>
              // The update payload is narrower than create — category and mandatory are fixed once
              // the task exists, so they are not sent.
              onboardingPlanService.updateTask(taskId, {
                id: taskId,
                taskName: values.taskName,
                description: blank(values.description),
                dueDate: values.dueDate,
                assignedToId: blank(values.assignedToId),
                assignedOrganizationUnitId: null,
                requiresVerification: values.requiresVerification,
                displayOrder: values.displayOrder,
              })
            }
            getId={(t) => t.id}
            columns={[
              { header: '#', cell: (t) => t.displayOrder, className: 'w-[60px]' },
              {
                header: 'Task',
                cell: (t) => (
                  <div>
                    <span className="font-medium">{t.taskName}</span>
                    <div className="text-muted-foreground mt-0.5 text-xs">
                      {categoryLabel(t.category)}
                      {t.isMandatory ? ' · mandatory' : ''}
                      {t.requiresVerification ? ' · needs verifying' : ''}
                    </div>
                  </div>
                ),
              },
              {
                header: 'Due',
                cell: (t) => (
                  <span className={t.isOverdue ? 'font-medium text-red-600' : undefined}>
                    {fmt(t.dueDate)}
                  </span>
                ),
              },
              {
                header: 'Assigned',
                cell: (t) =>
                  t.assignedToName ??
                  t.assignedOrganizationUnitName ??
                  t.ownerPositionTitle ??
                  '—',
              },
              {
                header: 'Status',
                cell: (t) => <StatusBadge status={t.statusName ?? t.status} />,
              },
              {
                header: 'Signed off',
                cell: (t) =>
                  t.verifiedByName ? (
                    <span className="text-xs">
                      {t.verifiedByName}
                      <br />
                      {fmt(t.verifiedDate)}
                    </span>
                  ) : t.awaitingVerification ? (
                    <Badge variant="secondary">Awaiting</Badge>
                  ) : (
                    '—'
                  ),
              },
            ]}
            schema={taskSchema as any}
            emptyForm={emptyTask}
            toForm={(t) => ({
              taskName: t.taskName,
              description: t.description ?? '',
              category: t.category,
              dueDate: t.dueDate.slice(0, 10),
              isMandatory: t.isMandatory,
              assignedToId: t.assignedToId ?? '',
              requiresVerification: t.requiresVerification,
              displayOrder: t.displayOrder,
            })}
            renderFields={(form, editing) => (
              <>
                <TextField form={form} name="taskName" label="Task" required />
                <TextareaField form={form} name="description" label="Description" rows={2} />
                <FieldRow>
                  <SelectField
                    form={form}
                    name="category"
                    label="Category"
                    required
                    options={ONBOARDING_TASK_CATEGORY_OPTIONS}
                  />
                  <DateField form={form} name="dueDate" label="Due date" required />
                </FieldRow>
                <EmployeePickerField form={form} name="assignedToId" label="Assigned to" />
                <FieldRow>
                  <NumberField form={form} name="displayOrder" label="Display order" required />
                  <SwitchField
                    form={form}
                    name="requiresVerification"
                    label="Requires verification"
                    description="A second person has to sign it off; whoever completes it cannot."
                  />
                </FieldRow>
                {!editing && (
                  <SwitchField
                    form={form}
                    name="isMandatory"
                    label="Mandatory"
                    description="Fixed once the task exists."
                  />
                )}
              </>
            )}
          />
        </TabsContent>

        <TabsContent value="assets" className="pt-4">
          <ResourceCollectionTab<OnboardingAsset, AssetForm>
            parentId={id}
            title="assets"
            singular="asset"
            queryKey={['hr', 'onboarding-plans', id, 'assets']}
            invalidateKeys={[planKey]}
            readOnly={isFinished}
            dialogHint="Kit, cards and accounts this hire needs. Status is tracked from ordered through to acknowledged."
            emptyDescription="Nothing being provisioned for this hire."
            list={() => onboardingPlanService.getAssets(id)}
            create={(planId, values) =>
              onboardingPlanService.addAsset(planId, {
                onboardingPlanId: planId,
                assetType: values.assetType,
                assetName: values.assetName,
                description: blank(values.description),
                assetTag: blank(values.assetTag),
                serialNumber: blank(values.serialNumber),
                requiredByDate: blank(values.requiredByDate),
                notes: blank(values.notes),
              })
            }
            update={(_p, assetId, values) =>
              onboardingPlanService.updateAsset(assetId, {
                id: assetId,
                status: values.status,
                assetTag: blank(values.assetTag),
                serialNumber: blank(values.serialNumber),
                requiredByDate: blank(values.requiredByDate),
                provisionedDate: null,
                provisionedById: null,
                issuedToEmployeeDate: null,
                acknowledgedByEmployee: values.acknowledgedByEmployee,
                acknowledgementDate: null,
                acknowledgementDocumentPath: null,
                notes: blank(values.notes),
              })
            }
            getId={(a) => a.id}
            columns={[
              {
                header: 'Asset',
                cell: (a) => (
                  <div>
                    <span className="font-medium">{a.assetName}</span>
                    <div className="text-muted-foreground mt-0.5 text-xs">
                      {assetTypeLabel(a.assetType)}
                      {a.assetTag ? ` · ${a.assetTag}` : ''}
                      {a.serialNumber ? ` · ${a.serialNumber}` : ''}
                    </div>
                  </div>
                ),
              },
              { header: 'Needed by', cell: (a) => fmt(a.requiredByDate) },
              { header: 'Issued', cell: (a) => fmt(a.issuedToEmployeeDate) },
              {
                header: 'Acknowledged',
                cell: (a) =>
                  a.acknowledgedByEmployee ? (
                    <Badge variant="default">Yes</Badge>
                  ) : (
                    <Badge variant="outline">No</Badge>
                  ),
              },
              { header: 'Status', cell: (a) => <StatusBadge status={a.status} /> },
            ]}
            schema={assetSchema as any}
            emptyForm={emptyAsset}
            toForm={(a) => ({
              assetType: a.assetType,
              assetName: a.assetName,
              description: a.description ?? '',
              assetTag: a.assetTag ?? '',
              serialNumber: a.serialNumber ?? '',
              requiredByDate: a.requiredByDate ? a.requiredByDate.slice(0, 10) : '',
              status: a.status,
              acknowledgedByEmployee: a.acknowledgedByEmployee,
              notes: a.notes ?? '',
            })}
            renderFields={(form, editing) => (
              <>
                <FieldRow>
                  <SelectField
                    form={form}
                    name="assetType"
                    label="Type"
                    required
                    options={ONBOARDING_ASSET_TYPE_OPTIONS}
                  />
                  <TextField form={form} name="assetName" label="Name" required />
                </FieldRow>
                {!editing && (
                  <TextareaField form={form} name="description" label="Description" rows={2} />
                )}
                <FieldRow>
                  <TextField form={form} name="assetTag" label="Asset tag" />
                  <TextField form={form} name="serialNumber" label="Serial number" />
                </FieldRow>
                <FieldRow>
                  <DateField form={form} name="requiredByDate" label="Needed by" />
                  {editing ? (
                    <SelectField
                      form={form}
                      name="status"
                      label="Status"
                      required
                      options={ONBOARDING_ASSET_STATUS_OPTIONS}
                    />
                  ) : (
                    <div />
                  )}
                </FieldRow>
                {editing && (
                  <SwitchField
                    form={form}
                    name="acknowledgedByEmployee"
                    label="Acknowledged by the employee"
                  />
                )}
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={confirmAction !== null}
        onOpenChange={(o) => !o && setConfirmAction(null)}
        title={confirmAction === 'start' ? 'Start this plan?' : 'Mark the plan completed?'}
        description={
          confirmAction === 'start'
            ? 'The plan moves to in progress and its tasks become live work.'
            : 'This closes the plan. Any outstanding tasks stay on the record as they are, and the plan can no longer be edited.'
        }
        confirmText={confirmAction === 'start' ? 'Start' : 'Mark completed'}
        isLoading={busy}
        onConfirm={runAction}
      >
        {confirmAction === 'complete' && <MandatoryOutstandingNote tasks={tasks} />}
      </ConfirmationDialog>
    </div>
  );
}
