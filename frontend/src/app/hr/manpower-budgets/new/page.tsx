'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Loader2, Save } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { useQuery } from '@tanstack/react-query';
import { policySettingsService } from '@/services/hr/policy-settings.service';
import {
  ManpowerBudgetFormFields,
  emptyManpowerBudgetForm,
  fiscalPeriodFor,
  manpowerBudgetFormIsComplete,
  manpowerBudgetPayload,
} from '@/components/hr/manpower/ManpowerBudgetFormFields';

const thisYear = new Date().getFullYear();

/**
 * Drafting a manpower budget.
 *
 * ⚠ **There is no approver field.** The approvers come from the workflow definition (FR-HR-135:
 * Department Head → HR → Managing Director) and the approval is stamped from the token of whoever
 * completes the chain. Until slice 7 this endpoint took `approvedById` as a QUERY PARAMETER, so any
 * caller could record any employee as having authorised the headcount.
 *
 * ⚠ **`totalBudget` is not on this form either** — the API computes it from the four component
 * budgets. Sending it would be sending a number the server is about to overwrite.
 *
 * The fields live in `ManpowerBudgetFormFields`, shared with the edit page (round 2b, lane R1).
 */
export default function NewManpowerBudgetPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState(() => emptyManpowerBudgetForm(thisYear + 1));

  // The empty form assumes a calendar year; once the tenant's fiscal start month is known the
  // untouched default period follows it (R2). A period the user has already edited is left alone.
  const { data: policy } = useQuery({
    queryKey: ['hr', 'policy-settings'],
    queryFn: () => policySettingsService.get(),
    staleTime: 5 * 60 * 1000,
  });
  useEffect(() => {
    if (!policy) return;
    const calendar = fiscalPeriodFor(form.fiscalYear, 1);
    if (form.periodStartDate !== calendar.start || form.periodEndDate !== calendar.end) return;
    const fiscal = fiscalPeriodFor(form.fiscalYear, policy.fiscalYearStartMonth);
    if (fiscal.start !== calendar.start) setForm((f) => ({ ...f, periodStartDate: fiscal.start, periodEndDate: fiscal.end }));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [policy]);

  const complete = manpowerBudgetFormIsComplete(form);

  const save = async () => {
    if (!complete) return;
    setSaving(true);
    try {
      const created = await jobArchitectureService.createBudget(manpowerBudgetPayload(form));
      toast.success(`${created.budgetNumber} created`);
      router.push(`/hr/manpower-budgets/${created.id}`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not save the budget');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title="New manpower recruitment budget"
        description="Authorise headcount and its cost for a unit and a fiscal year."
        backHref="/hr/manpower-budgets"
        actions={
          <Button onClick={save} disabled={!complete || saving}>
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save draft
          </Button>
        }
      />

      <ManpowerBudgetFormFields value={form} onChange={setForm} autoFillFromBaseline />

      <p className="text-sm text-muted-foreground">
        Add the budget lines — one per position — on the budget once it is saved. A budget with no
        lines authorises no posts and cannot be submitted.
      </p>
    </div>
  );
}
