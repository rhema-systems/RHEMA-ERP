'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import {
  ManpowerBudgetFormFields,
  emptyManpowerBudgetForm,
  manpowerBudgetFormFromBudget,
  manpowerBudgetFormIsComplete,
  manpowerBudgetPayload,
} from '@/components/hr/manpower/ManpowerBudgetFormFields';

/**
 * Editing a manpower budget — the same form as the create page, seeded from the record.
 *
 * Round 2b, lane R1. The server allows this only while the budget is **Draft or Rejected**: one
 * out for approval is what its three approvers are reading, and an approved one is the record. A
 * rejected budget is editable on purpose — correct and resubmit is the loop that status exists for.
 *
 * ⚠ The update is a REPLACE. Every figure on the form is sent, seeded from the budget, so nothing
 * is zeroed by omission (which is exactly what the old "Correct" dialog did to promotions,
 * transfers and actual spend).
 */
export default function EditManpowerBudgetPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const qc = useQueryClient();
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState(() => emptyManpowerBudgetForm(new Date().getFullYear() + 1));
  const [seeded, setSeeded] = useState(false);

  const { data: budget, isLoading, isError } = useQuery({
    queryKey: ['manpower-budget', id],
    queryFn: () => jobArchitectureService.getBudget(id),
    enabled: !!id,
  });

  useEffect(() => {
    if (!budget || seeded) return;
    setForm(manpowerBudgetFormFromBudget(budget));
    setSeeded(true);
  }, [budget, seeded]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
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

  const status = (budget.statusName ?? budget.status) as string;
  const editable = status === 'Draft' || status === 'Rejected';
  const complete = manpowerBudgetFormIsComplete(form);

  const save = async () => {
    if (!editable || !complete) return;
    setSaving(true);
    try {
      await jobArchitectureService.updateBudget(id, { id, ...manpowerBudgetPayload(form) });
      await qc.invalidateQueries({ queryKey: ['manpower-budget', id] });
      await qc.invalidateQueries({ queryKey: ['manpower-budgets'] });
      toast.success('Budget saved');
      router.push(`/hr/manpower-budgets/${id}`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not save the budget');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={`Edit ${budget.budgetNumber}`}
        description={`${budget.organizationUnitName ?? 'Manpower budget'} ${budget.fiscalYear}`}
        backHref={`/hr/manpower-budgets/${id}`}
        actions={
          <Button onClick={save} disabled={!editable || !complete || saving}>
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save
          </Button>
        }
      />

      {!editable && (
        <EmptyState
          title={`A ${status} budget cannot be edited`}
          description="Only a draft or rejected budget can be changed. One awaiting approval is what its approvers are reading; an approved one is the record."
        />
      )}

      {editable && (
        <>
          {status === 'Rejected' && budget.rejectionReason && (
            <div className="rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-900">
              <span className="font-medium">Sent back: </span>
              {budget.rejectionReason}
            </div>
          )}
          <ManpowerBudgetFormFields value={form} onChange={setForm} />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => router.push(`/hr/manpower-budgets/${id}`)}>
              Cancel
            </Button>
            <Button onClick={save} disabled={!complete || saving}>
              <Save className="mr-2 h-4 w-4" />
              {saving ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
