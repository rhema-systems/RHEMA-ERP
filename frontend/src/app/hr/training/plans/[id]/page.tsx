'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, Send, CheckCircle2 } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  NumberField,
  DateField,
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import {
  TrainingPlanForm,
  type TrainingPlanFormValues,
} from '@/components/hr/training/TrainingPlanForm';
import { trainingPlanService } from '@/services/hr/training-plan.service';
import { trainingProgramService } from '@/services/hr/training-program.service';
import type { TrainingPlanItem, TrainingPlanBudgetLine } from '@/types/hr/training';

const itemFormSchema = z.object({
  programId: z.string().optional().or(z.literal('')),
  trainingTitle: z.string().min(1, 'Required').max(200),
  description: z.string().max(2000).optional().or(z.literal('')),
  quarter: z.coerce.number().min(1).max(4),
  plannedStartDate: z.string().optional().or(z.literal('')),
  plannedEndDate: z.string().optional().or(z.literal('')),
  estimatedParticipants: z.coerce.number().min(0).max(10000),
  estimatedCost: z.coerce.number().min(0),
  isCompleted: z.boolean(),
  completionDate: z.string().optional().or(z.literal('')),
  actualParticipants: z.coerce.number().min(0).max(10000).optional(),
  actualCost: z.coerce.number().min(0).optional(),
});
type ItemForm = z.infer<typeof itemFormSchema>;
const emptyItemForm: ItemForm = {
  programId: '',
  trainingTitle: '',
  description: '',
  quarter: 1,
  plannedStartDate: '',
  plannedEndDate: '',
  estimatedParticipants: 0,
  estimatedCost: 0,
  isCompleted: false,
  completionDate: '',
  actualParticipants: undefined,
  actualCost: undefined,
};

const budgetLineFormSchema = z.object({
  category: z.string().min(1, 'Required').max(100),
  budgetedAmount: z.coerce.number().min(0),
  actualAmount: z.coerce.number().min(0),
  committedAmount: z.coerce.number().min(0),
  notes: z.string().max(1000).optional().or(z.literal('')),
});
type BudgetLineForm = z.infer<typeof budgetLineFormSchema>;
const emptyBudgetLineForm: BudgetLineForm = {
  category: '',
  budgetedAmount: 0,
  actualAmount: 0,
  committedAmount: 0,
  notes: '',
};

export default function TrainingPlanDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [savingOverview, setSavingOverview] = useState(false);
  const [confirmAction, setConfirmAction] = useState<'submit' | 'approve' | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: plan, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'plans', id],
    queryFn: () => trainingPlanService.getById(id),
    enabled: !!id,
  });

  const { data: programs } = useQuery({
    queryKey: ['hr', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const programOptions = (programs ?? []).map((p) => ({ value: p.id, label: `${p.programCode} — ${p.programName}` }));

  const invalidatePlan = () => queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'plans', id] });

  const handleOverviewSubmit = async (values: TrainingPlanFormValues) => {
    setSavingOverview(true);
    try {
      await trainingPlanService.update(id, {
        year: values.year,
        organizationLevelId: values.organizationLevelId || null,
        organizationUnitId: values.organizationUnitId || null,
        notes: values.notes || null,
      });
      await Promise.all([invalidatePlan(), queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'plans'] })]);
      toast({ title: 'Saved', description: 'Plan updated.' });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to update plan.', variant: 'destructive' });
    } finally {
      setSavingOverview(false);
    }
  };

  const runWorkflowAction = async () => {
    if (!confirmAction) return false;
    setBusy(true);
    try {
      if (confirmAction === 'submit') await trainingPlanService.submit(id);
      else await trainingPlanService.approve(id);
      await Promise.all([invalidatePlan(), queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'plans'] })]);
      toast({ title: confirmAction === 'submit' ? 'Submitted' : 'Approved', description: 'Plan status updated.' });
      setConfirmAction(null);
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Action failed.', variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
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

  const isDraft = plan.status === 'Draft';
  const isPendingApproval = plan.status === 'PendingApproval';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={plan.planNumber}
        description={`${plan.year} — ${plan.organizationUnitName || 'Company-wide'}`}
        backHref="/hr/training/plans"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={plan.status} />
            {isDraft && (
              <Button variant="outline" size="sm" onClick={() => setConfirmAction('submit')}>
                <Send className="mr-2 h-4 w-4" /> Submit for approval
              </Button>
            )}
            {isPendingApproval && (
              <Button size="sm" onClick={() => setConfirmAction('approve')}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
              </Button>
            )}
          </div>
        }
      />

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="items">Items ({plan.totalItemsCount})</TabsTrigger>
          <TabsTrigger value="budget-lines">Budget Lines ({plan.budgetLines.length})</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          {isDraft ? (
            <TrainingPlanForm
              defaultValues={{
                year: plan.year,
                organizationLevelId: plan.organizationLevelId ?? '',
                organizationUnitId: plan.organizationUnitId ?? '',
                notes: plan.notes ?? '',
              }}
              onSubmit={handleOverviewSubmit}
              submitting={savingOverview}
              submitLabel="Save changes"
              onCancel={() => router.push('/hr/training/plans')}
            />
          ) : (
            <Card>
              <CardContent className="grid gap-4 py-6 sm:grid-cols-2">
                <div>
                  <p className="text-sm text-muted-foreground">Organization level</p>
                  <p className="font-medium">{plan.organizationLevelName ?? 'Any'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Organization unit</p>
                  <p className="font-medium">{plan.organizationUnitName ?? 'Any (company-wide)'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Approved by</p>
                  <p className="font-medium">{plan.approvedByName ?? '—'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Approval date</p>
                  <p className="font-medium">
                    {plan.approvalDate ? new Date(plan.approvalDate).toLocaleDateString() : '—'}
                  </p>
                </div>
                <div className="sm:col-span-2">
                  <p className="text-sm text-muted-foreground">Notes</p>
                  <p className="font-medium">{plan.notes || '—'}</p>
                </div>
                <p className="text-xs text-muted-foreground sm:col-span-2">
                  An approved or completed plan cannot be edited here.
                </p>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="items" className="pt-4">
          <ResourceCollectionTab<TrainingPlanItem, ItemForm>
            parentId={id}
            title="items"
            singular="item"
            queryKey={['hr', 'training', 'plans', id, 'items']}
            invalidateKeys={[['hr', 'training', 'plans', id]]}
            dialogHint="A planned training activity for the year, optionally linked to a catalog program."
            list={() => trainingPlanService.getItems(id)}
            create={(planId, values) =>
              trainingPlanService.addItem(planId, { ...values, programId: values.programId || null } as any)
            }
            update={(_planId, itemId, values) =>
              trainingPlanService.updateItem(itemId, { ...values, programId: values.programId || null } as any)
            }
            remove={(_planId, itemId) => trainingPlanService.removeItem(itemId)}
            getId={(i) => i.id}
            columns={[
              { header: 'Title', cell: (i) => <span className="font-medium">{i.trainingTitle}</span> },
              { header: 'Program', cell: (i) => i.programCode || '—' },
              { header: 'Quarter', cell: (i) => `Q${i.quarter}` },
              { header: 'Est. participants', cell: (i) => i.estimatedParticipants },
              { header: 'Est. cost', cell: (i) => i.estimatedCost.toLocaleString() },
              { header: 'Completed', cell: (i) => (i.isCompleted ? 'Yes' : 'No') },
            ]}
            schema={itemFormSchema as any}
            emptyForm={emptyItemForm}
            toForm={(i) => ({
              programId: i.programId ?? '',
              trainingTitle: i.trainingTitle,
              description: i.description ?? '',
              quarter: i.quarter,
              plannedStartDate: i.plannedStartDate?.slice(0, 10) ?? '',
              plannedEndDate: i.plannedEndDate?.slice(0, 10) ?? '',
              estimatedParticipants: i.estimatedParticipants,
              estimatedCost: i.estimatedCost,
              isCompleted: i.isCompleted,
              completionDate: i.completionDate?.slice(0, 10) ?? '',
              actualParticipants: i.actualParticipants ?? undefined,
              actualCost: i.actualCost ?? undefined,
            })}
            renderFields={(form, editing) => {
              const completed = !!form.watch('isCompleted');
              return (
                <>
                  <TextField form={form} name="trainingTitle" label="Training title" required />
                  <SelectField
                    form={form}
                    name="programId"
                    label="Catalog program"
                    options={programOptions}
                    allowEmpty
                    emptyLabel="Not from the catalog"
                  />
                  <TextareaField form={form} name="description" label="Description" rows={2} />
                  <FieldRow>
                    <SelectField
                      form={form}
                      name="quarter"
                      label="Quarter"
                      required
                      options={[
                        { value: '1', label: 'Q1' },
                        { value: '2', label: 'Q2' },
                        { value: '3', label: 'Q3' },
                        { value: '4', label: 'Q4' },
                      ]}
                    />
                    <NumberField form={form} name="estimatedParticipants" label="Est. participants" required />
                  </FieldRow>
                  <FieldRow>
                    <DateField form={form} name="plannedStartDate" label="Planned start" />
                    <DateField form={form} name="plannedEndDate" label="Planned end" />
                  </FieldRow>
                  <NumberField form={form} name="estimatedCost" label="Estimated cost" step="0.01" required />
                  {/* Completion is edit-only: CreateTrainingPlanItemDto carries no completion
                      fields, so offering these on the add dialog silently discarded whatever the
                      user entered. An item is planned first and marked complete afterwards. */}
                  {editing ? (
                    <>
                      <SwitchField form={form} name="isCompleted" label="Completed" />
                      {completed && (
                        <>
                          <DateField form={form} name="completionDate" label="Completion date" />
                          <FieldRow>
                            <NumberField form={form} name="actualParticipants" label="Actual participants" />
                            <NumberField form={form} name="actualCost" label="Actual cost" step="0.01" />
                          </FieldRow>
                        </>
                      )}
                    </>
                  ) : (
                    <p className="text-xs text-muted-foreground">
                      Mark the item complete and record actuals after it has been delivered — edit it then.
                    </p>
                  )}
                </>
              );
            }}
          />
        </TabsContent>

        <TabsContent value="budget-lines" className="pt-4">
          <ResourceCollectionTab<TrainingPlanBudgetLine, BudgetLineForm>
            parentId={id}
            title="budget lines"
            singular="budget line"
            queryKey={['hr', 'training', 'plans', id, 'budget-lines']}
            invalidateKeys={[['hr', 'training', 'plans', id]]}
            dialogHint="A budget category tracked against this plan (e.g. Venue, Facilitation, Materials)."
            list={() => trainingPlanService.getBudgetLines(id)}
            create={(planId, values) => trainingPlanService.addBudgetLine(planId, values)}
            update={(_planId, lineId, values) => trainingPlanService.updateBudgetLine(lineId, values)}
            remove={(_planId, lineId) => trainingPlanService.removeBudgetLine(lineId)}
            getId={(b) => b.id}
            columns={[
              { header: 'Category', cell: (b) => <span className="font-medium">{b.category}</span> },
              { header: 'Budgeted', cell: (b) => b.budgetedAmount.toLocaleString() },
              { header: 'Actual', cell: (b) => b.actualAmount.toLocaleString() },
              { header: 'Committed', cell: (b) => b.committedAmount.toLocaleString() },
              { header: 'Variance', cell: (b) => b.variance.toLocaleString() },
            ]}
            schema={budgetLineFormSchema as any}
            emptyForm={emptyBudgetLineForm}
            toForm={(b) => ({
              category: b.category,
              budgetedAmount: b.budgetedAmount,
              actualAmount: b.actualAmount,
              committedAmount: b.committedAmount,
              notes: b.notes ?? '',
            })}
            renderFields={(form, editing) => (
              <>
                <TextField form={form} name="category" label="Category" required />
                <NumberField form={form} name="budgetedAmount" label="Budgeted amount" step="0.01" required />
                {editing && (
                  <FieldRow>
                    <NumberField form={form} name="actualAmount" label="Actual amount" step="0.01" required />
                    <NumberField form={form} name="committedAmount" label="Committed amount" step="0.01" required />
                  </FieldRow>
                )}
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={confirmAction !== null}
        onOpenChange={(open) => !open && setConfirmAction(null)}
        title={confirmAction === 'submit' ? 'Submit plan for approval?' : 'Approve plan?'}
        description={
          confirmAction === 'submit'
            ? `"${plan.planNumber}" will move to Pending Approval.`
            : `"${plan.planNumber}" will be marked Approved.`
        }
        confirmText={confirmAction === 'submit' ? 'Submit' : 'Approve'}
        isLoading={busy}
        onConfirm={runWorkflowAction}
      />
    </div>
  );
}
