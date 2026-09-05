'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, CheckCircle2 } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { DateField, TextField, TextareaField, NumberField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TrainingBudgetForm,
  type TrainingBudgetFormValues,
} from '@/components/hr/training/TrainingBudgetForm';
import { trainingBudgetService } from '@/services/hr/training-budget.service';
import type { TrainingBudgetTransaction } from '@/types/hr/training';

const transactionFormSchema = z.object({
  description: z.string().min(1, 'Required').max(500),
  amount: z.coerce.number(),
  transactionDate: z.string().min(1, 'Required'),
  recordedById: z.string().min(1, 'Required'),
  reference: z.string().max(100).optional().or(z.literal('')),
  glAccountCode: z.string().max(50).optional().or(z.literal('')),
  voucherNumber: z.string().max(50).optional().or(z.literal('')),
  notes: z.string().max(1000).optional().or(z.literal('')),
});
type TransactionForm = z.infer<typeof transactionFormSchema>;
const emptyTransactionForm: TransactionForm = {
  description: '',
  amount: 0,
  transactionDate: new Date().toISOString().slice(0, 10),
  recordedById: '',
  reference: '',
  glAccountCode: '',
  voucherNumber: '',
  notes: '',
};

export default function TrainingBudgetDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [savingOverview, setSavingOverview] = useState(false);
  const [confirmApprove, setConfirmApprove] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: budget, isLoading, isError } = useQuery({
    queryKey: ['hr', 'training', 'budgets', id],
    queryFn: () => trainingBudgetService.getById(id),
    enabled: !!id,
  });

  const invalidateBudget = () => queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'budgets', id] });

  const handleOverviewSubmit = async (values: TrainingBudgetFormValues) => {
    setSavingOverview(true);
    try {
      await trainingBudgetService.update(id, {
        year: values.year,
        quarter: values.quarter ? Number(values.quarter) : null,
        organizationLevelId: values.organizationLevelId || null,
        organizationUnitId: values.organizationUnitId || null,
        currency: values.currency,
        allocatedAmount: values.allocatedAmount,
        glAccountCode: values.glAccountCode || null,
        costCenterCode: values.costCenterCode || null,
        notes: values.notes || null,
      });
      await Promise.all([invalidateBudget(), queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'budgets'] })]);
      toast({ title: 'Saved', description: 'Budget updated.' });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to update budget.', variant: 'destructive' });
    } finally {
      setSavingOverview(false);
    }
  };

  const handleApprove = async () => {
    setBusy(true);
    try {
      await trainingBudgetService.approve(id);
      await Promise.all([invalidateBudget(), queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'budgets'] })]);
      toast({ title: 'Approved', description: 'Budget approved.' });
      setConfirmApprove(false);
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to approve budget.', variant: 'destructive' });
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

  if (isError || !budget) {
    return (
      <div className="p-6">
        <EmptyState title="Budget not found" description="It may have been removed." />
      </div>
    );
  }

  const isDraft = budget.status === 'Draft';
  const canRecordSpend = budget.status === 'Approved' || budget.status === 'Active';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={budget.budgetCode}
        description={`${budget.periodDescription} — ${budget.organizationUnitName || 'Company-wide'}`}
        backHref="/administration/hr/training/budgets"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={budget.status} />
            {isDraft && (
              <Button size="sm" onClick={() => setConfirmApprove(true)}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
              </Button>
            )}
          </div>
        }
      />

      <Card>
        <CardContent className="grid gap-4 py-6 sm:grid-cols-3">
          <div>
            <p className="text-sm text-muted-foreground">Allocated</p>
            <p className="text-lg font-semibold">{budget.currency} {budget.allocatedAmount.toLocaleString()}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Spent</p>
            <p className="text-lg font-semibold">{budget.currency} {budget.spentAmount.toLocaleString()}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Remaining</p>
            <p className="text-lg font-semibold">{budget.currency} {budget.remainingAmount.toLocaleString()}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Committed</p>
            <p className="font-medium">{budget.currency} {budget.committedAmount.toLocaleString()}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Utilization</p>
            <p className="font-medium">{budget.utilizationRate}%</p>
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="transactions">Transactions</TabsTrigger>
        </TabsList>

        <TabsContent value="overview" className="pt-4">
          {isDraft ? (
            <TrainingBudgetForm
              defaultValues={{
                budgetCode: budget.budgetCode,
                year: budget.year,
                quarter: budget.quarter ? String(budget.quarter) : '',
                organizationLevelId: budget.organizationLevelId ?? '',
                organizationUnitId: budget.organizationUnitId ?? '',
                currency: budget.currency,
                allocatedAmount: budget.allocatedAmount,
                glAccountCode: budget.glAccountCode ?? '',
                costCenterCode: budget.costCenterCode ?? '',
                notes: budget.notes ?? '',
              }}
              onSubmit={handleOverviewSubmit}
              submitting={savingOverview}
              submitLabel="Save changes"
              onCancel={() => router.push('/administration/hr/training/budgets')}
              budgetCodeEditable={false}
            />
          ) : (
            <Card>
              <CardContent className="grid gap-4 py-6 sm:grid-cols-2">
                <div>
                  <p className="text-sm text-muted-foreground">Organization level</p>
                  <p className="font-medium">{budget.organizationLevelName ?? 'Any'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Organization unit</p>
                  <p className="font-medium">{budget.organizationUnitName ?? 'Any (company-wide)'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Approved by</p>
                  <p className="font-medium">{budget.approvedByName ?? '—'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Approval date</p>
                  <p className="font-medium">
                    {budget.approvalDate ? new Date(budget.approvalDate).toLocaleDateString() : '—'}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">GL account code</p>
                  <p className="font-medium">{budget.glAccountCode || '—'}</p>
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Cost center code</p>
                  <p className="font-medium">{budget.costCenterCode || '—'}</p>
                </div>
                <div className="sm:col-span-2">
                  <p className="text-sm text-muted-foreground">Notes</p>
                  <p className="font-medium">{budget.notes || '—'}</p>
                </div>
                <p className="text-xs text-muted-foreground sm:col-span-2">
                  An approved or closed budget cannot be edited here.
                </p>
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="transactions" className="pt-4">
          <ResourceCollectionTab<TrainingBudgetTransaction, TransactionForm>
            parentId={id}
            title="transactions"
            singular="transaction"
            queryKey={['hr', 'training', 'budgets', id, 'transactions']}
            invalidateKeys={[['hr', 'training', 'budgets', id]]}
            dialogHint="Record spend (positive amount) or a refund/credit (negative amount) against this budget."
            list={() => trainingBudgetService.getTransactions(id)}
            create={(budgetId, values) => trainingBudgetService.recordTransaction(budgetId, values as any)}
            update={async () => {}}
            allowUpdate={false}
            allowCreate={canRecordSpend}
            getId={(t) => t.id}
            columns={[
              { header: 'Date', cell: (t) => new Date(t.transactionDate).toLocaleDateString() },
              { header: 'Description', cell: (t) => t.description },
              {
                header: 'Amount',
                cell: (t) => (
                  <span className={t.amount < 0 ? 'text-green-600' : undefined}>
                    {t.amountType} {Math.abs(t.amount).toLocaleString()}
                  </span>
                ),
              },
              { header: 'Recorded by', cell: (t) => t.recordedByName || '—' },
              { header: 'Reference', cell: (t) => t.reference || '—' },
            ]}
            emptyDescription={
              canRecordSpend
                ? 'Record the first transaction against this budget.'
                : 'Transactions can only be recorded once the budget is approved.'
            }
            schema={transactionFormSchema as any}
            emptyForm={emptyTransactionForm}
            toForm={(t) => ({
              description: t.description,
              amount: t.amount,
              transactionDate: t.transactionDate.slice(0, 10),
              recordedById: t.recordedById ?? '',
              reference: t.reference ?? '',
              glAccountCode: t.glAccountCode ?? '',
              voucherNumber: t.voucherNumber ?? '',
              notes: t.notes ?? '',
            })}
            renderFields={(form) => (
              <>
                <TextField form={form} name="description" label="Description" required />
                <FieldRow>
                  <NumberField
                    form={form}
                    name="amount"
                    label="Amount (negative = credit/refund)"
                    step="0.01"
                    required
                  />
                  <DateField form={form} name="transactionDate" label="Transaction date" required />
                </FieldRow>
                <EmployeePickerField form={form} name="recordedById" label="Recorded by" required />
                <FieldRow>
                  <TextField form={form} name="reference" label="Reference" />
                  <TextField form={form} name="voucherNumber" label="Voucher number" />
                </FieldRow>
                <TextField form={form} name="glAccountCode" label="GL account code" />
                <TextareaField form={form} name="notes" label="Notes" rows={2} />
              </>
            )}
          />
        </TabsContent>
      </Tabs>

      <ConfirmationDialog
        open={confirmApprove}
        onOpenChange={setConfirmApprove}
        title="Approve budget?"
        description={`"${budget.budgetCode}" will be marked Approved.`}
        confirmText="Approve"
        isLoading={busy}
        onConfirm={handleApprove}
      />
    </div>
  );
}
