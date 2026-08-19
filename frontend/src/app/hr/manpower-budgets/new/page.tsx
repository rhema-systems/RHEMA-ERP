'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';

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
 */
export default function NewManpowerBudgetPage() {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({
    fiscalYear: thisYear + 1,
    organizationUnitId: '',
    periodStartDate: `${thisYear + 1}-01-01`,
    periodEndDate: `${thisYear + 1}-12-31`,
    currentHeadcount: 0,
    currentSalaryCost: 0,
    plannedHeadcount: 0,
    plannedSalaryCost: 0,
    plannedNewHires: 0,
    plannedTerminations: 0,
    plannedPromotions: 0,
    plannedTransfers: 0,
    salaryBudget: 0,
    benefitsBudget: 0,
    recruitmentBudget: 0,
    trainingBudget: 0,
    businessJustification: '',
  });

  const { data: units } = useQuery({
    queryKey: ['organization-units', 'all'],
    queryFn: () => organizationUnitService.getAll(),
  });

  const set = (key: string, value: unknown) => setForm((f) => ({ ...f, [key]: value }));

  const save = async () => {
    if (!form.organizationUnitId) return;
    setSaving(true);
    try {
      const created = await jobArchitectureService.createBudget(form as never);
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
        title="New manpower budget"
        description="Authorise headcount and its cost for a unit and a fiscal year."
        backHref="/hr/manpower-budgets"
        actions={
          <Button onClick={save} disabled={!form.organizationUnitId || saving}>
            {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
            Save draft
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle>Scope</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label>Organisation unit *</Label>
            <Select
              value={form.organizationUnitId}
              onValueChange={(v) => set('organizationUnitId', v)}
            >
              <SelectTrigger>
                <SelectValue placeholder="Choose the unit" />
              </SelectTrigger>
              <SelectContent>
                {(units ?? []).map((u: { id: string; name: string }) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Fiscal year *</Label>
            <Input
              type="number"
              min={2000}
              max={2100}
              value={form.fiscalYear}
              onChange={(e) => {
                const year = Number(e.target.value);
                setForm((f) => ({
                  ...f,
                  fiscalYear: year,
                  periodStartDate: `${year}-01-01`,
                  periodEndDate: `${year}-12-31`,
                }));
              }}
            />
            {/* The API validates this range, so the input mirrors it rather than discovering it. */}
            <p className="text-xs text-muted-foreground">Between 2000 and 2100.</p>
          </div>

          <div className="space-y-2">
            <Label>Period start</Label>
            <Input
              type="date"
              value={form.periodStartDate}
              onChange={(e) => set('periodStartDate', e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>Period end</Label>
            <Input
              type="date"
              value={form.periodEndDate}
              onChange={(e) => set('periodEndDate', e.target.value)}
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Headcount</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-3">
          <NumberField label="Current headcount" value={form.currentHeadcount} onChange={(v) => set('currentHeadcount', v)} />
          <NumberField label="Planned headcount" value={form.plannedHeadcount} onChange={(v) => set('plannedHeadcount', v)} />
          <NumberField label="Planned new hires" value={form.plannedNewHires} onChange={(v) => set('plannedNewHires', v)} />
          <NumberField label="Planned terminations" value={form.plannedTerminations} onChange={(v) => set('plannedTerminations', v)} />
          <NumberField label="Planned promotions" value={form.plannedPromotions} onChange={(v) => set('plannedPromotions', v)} />
          <NumberField label="Planned transfers" value={form.plannedTransfers} onChange={(v) => set('plannedTransfers', v)} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Cost</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <NumberField label="Current salary cost" value={form.currentSalaryCost} onChange={(v) => set('currentSalaryCost', v)} />
          <NumberField label="Planned salary cost" value={form.plannedSalaryCost} onChange={(v) => set('plannedSalaryCost', v)} />
          <NumberField label="Salary budget" value={form.salaryBudget} onChange={(v) => set('salaryBudget', v)} />
          <NumberField label="Benefits budget" value={form.benefitsBudget} onChange={(v) => set('benefitsBudget', v)} />
          <NumberField label="Recruitment budget" value={form.recruitmentBudget} onChange={(v) => set('recruitmentBudget', v)} />
          <NumberField label="Training budget" value={form.trainingBudget} onChange={(v) => set('trainingBudget', v)} />
          <div className="sm:col-span-2 rounded-md bg-muted p-3 text-sm">
            Total budget:{' '}
            <span className="font-semibold">
              {(form.salaryBudget + form.benefitsBudget + form.recruitmentBudget + form.trainingBudget).toLocaleString()}
            </span>
            <span className="ml-2 text-xs text-muted-foreground">computed by the system on save</span>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Justification</CardTitle>
        </CardHeader>
        <CardContent>
          <Textarea
            rows={4}
            value={form.businessJustification}
            onChange={(e) => set('businessJustification', e.target.value)}
            placeholder="Why this unit needs the posts it is asking for."
          />
        </CardContent>
      </Card>

      <p className="text-sm text-muted-foreground">
        Add the budget lines — one per position — on the budget once it is saved. A budget with no
        lines authorises no posts and cannot be submitted.
      </p>
    </div>
  );
}

function NumberField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <Input type="number" min={0} value={value} onChange={(e) => onChange(Number(e.target.value))} />
    </div>
  );
}
