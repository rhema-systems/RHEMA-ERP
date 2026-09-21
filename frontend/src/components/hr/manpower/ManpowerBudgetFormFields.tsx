'use client';

import { useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Sparkles } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { policySettingsService } from '@/services/hr/policy-settings.service';
import type { ManpowerBudget, ManpowerPlanningBaseline } from '@/types/hr/job-architecture';
import { PlanningBaselinePanel } from './PlanningBaselinePanel';

/**
 * The fiscal period for a year, from the tenant's `fiscalYearStartMonth` (round 2b, R2). A
 * fiscal year starting in July 2027 is labelled by the year it STARTS in and runs to June 2028.
 */
export function fiscalPeriodFor(fiscalYear: number, startMonth: number): { start: string; end: string } {
  const m = Math.min(12, Math.max(1, Math.floor(startMonth || 1)));
  const start = new Date(Date.UTC(fiscalYear, m - 1, 1));
  const end = new Date(Date.UTC(fiscalYear + 1, m - 1, 0)); // day 0 of the next-year month = last day before it
  const iso = (d: Date) => d.toISOString().slice(0, 10);
  return { start: iso(start), end: iso(end) };
}

/**
 * The manpower recruitment budget's own fields — one form for the create page and the edit page.
 *
 * Round 2b (demo feedback, lane R1). The create page and the detail page's "Correct" dialog were
 * two hand-written forms that had drifted: the dialog showed 12 of the create form's 17 fields and,
 * because the update is a REPLACE, silently wrote zero over the ones it did not show. One form,
 * two hosts, and what can be typed on creation can be corrected afterwards.
 *
 * ⚠ Unit is a Level → Unit cascade (`OrganizationUnitPicker`, lane B1), and the LEVEL is stored
 * too: `ManpowerBudget.OrganizationLevelId` has been on the entity since area 17 and the flat
 * dropdown never sent it. The picker clears the unit when the level changes, so the level is taken
 * from whichever callback fired last.
 *
 * ⚠ Not on this form, on purpose: `totalBudget` (the API computes it from the four component
 * budgets), `actualSpent` / `variance` (nothing in HR writes them — Finance's actuals), and any
 * approver (stamped from the token of whoever completes FR-HR-135's chain).
 */
export interface ManpowerBudgetFormState {
  fiscalYear: number;
  organizationUnitId: string;
  organizationLevelId: string;
  periodStartDate: string;
  periodEndDate: string;
  currentHeadcount: number;
  currentSalaryCost: number;
  plannedHeadcount: number;
  plannedSalaryCost: number;
  plannedNewHires: number;
  plannedTerminations: number;
  plannedPromotions: number;
  plannedTransfers: number;
  salaryBudget: number;
  benefitsBudget: number;
  recruitmentBudget: number;
  trainingBudget: number;
  businessJustification: string;
}

export function emptyManpowerBudgetForm(fiscalYear: number): ManpowerBudgetFormState {
  return {
    fiscalYear,
    organizationUnitId: '',
    organizationLevelId: '',
    periodStartDate: `${fiscalYear}-01-01`,
    periodEndDate: `${fiscalYear}-12-31`,
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
  };
}

/** Seed the form from a stored budget — every field, so a save round-trips what was there. */
export function manpowerBudgetFormFromBudget(b: ManpowerBudget): ManpowerBudgetFormState {
  return {
    fiscalYear: b.fiscalYear,
    organizationUnitId: b.organizationUnitId ?? '',
    organizationLevelId: b.organizationLevelId ?? '',
    periodStartDate: b.periodStartDate?.slice(0, 10) ?? '',
    periodEndDate: b.periodEndDate?.slice(0, 10) ?? '',
    currentHeadcount: b.currentHeadcount ?? 0,
    currentSalaryCost: b.currentSalaryCost ?? 0,
    plannedHeadcount: b.plannedHeadcount ?? 0,
    plannedSalaryCost: b.plannedSalaryCost ?? 0,
    plannedNewHires: b.plannedNewHires ?? 0,
    plannedTerminations: b.plannedTerminations ?? 0,
    plannedPromotions: b.plannedPromotions ?? 0,
    plannedTransfers: b.plannedTransfers ?? 0,
    salaryBudget: b.salaryBudget ?? 0,
    benefitsBudget: b.benefitsBudget ?? 0,
    recruitmentBudget: b.recruitmentBudget ?? 0,
    trainingBudget: b.trainingBudget ?? 0,
    businessJustification: b.businessJustification ?? '',
  };
}

/**
 * The write payload, the same shape for create and update. The scope fields are sent on both:
 * the create DTO requires them, and the update DTO treats them as "change to this" (null there
 * would mean "unchanged", which a form that shows the value has no reason to send).
 */
export function manpowerBudgetPayload(f: ManpowerBudgetFormState) {
  return {
    fiscalYear: f.fiscalYear,
    organizationUnitId: f.organizationUnitId || null,
    organizationLevelId: f.organizationLevelId || null,
    periodStartDate: f.periodStartDate,
    periodEndDate: f.periodEndDate,
    currentHeadcount: f.currentHeadcount,
    currentSalaryCost: f.currentSalaryCost,
    plannedHeadcount: f.plannedHeadcount,
    plannedSalaryCost: f.plannedSalaryCost,
    plannedNewHires: f.plannedNewHires,
    plannedTerminations: f.plannedTerminations,
    plannedPromotions: f.plannedPromotions,
    plannedTransfers: f.plannedTransfers,
    salaryBudget: f.salaryBudget,
    benefitsBudget: f.benefitsBudget,
    recruitmentBudget: f.recruitmentBudget,
    trainingBudget: f.trainingBudget,
    businessJustification: f.businessJustification.trim() || null,
  };
}

export function manpowerBudgetFormIsComplete(f: ManpowerBudgetFormState) {
  return (
    !!f.organizationUnitId &&
    f.fiscalYear >= 2000 &&
    f.fiscalYear <= 2100 &&
    !!f.periodStartDate &&
    !!f.periodEndDate &&
    f.periodEndDate >= f.periodStartDate
  );
}

export function ManpowerBudgetFormFields({
  value,
  onChange,
  autoFillFromBaseline = false,
}: {
  value: ManpowerBudgetFormState;
  onChange: (next: ManpowerBudgetFormState) => void;
  /**
   * Create page: the first baseline that arrives fills the three system-known figures without a
   * click. The edit page leaves what was typed and offers the button instead.
   */
  autoFillFromBaseline?: boolean;
}) {
  const set = <K extends keyof ManpowerBudgetFormState>(key: K, v: ManpowerBudgetFormState[K]) =>
    onChange({ ...value, [key]: v });

  const total = value.salaryBudget + value.benefitsBudget + value.recruitmentBudget + value.trainingBudget;

  // The period defaults come from the tenant's fiscal year, not the calendar.
  const { data: policy } = useQuery({
    queryKey: ['hr', 'policy-settings'],
    queryFn: () => policySettingsService.get(),
    staleTime: 5 * 60 * 1000,
  });
  const fiscalStartMonth = policy?.fiscalYearStartMonth ?? 1;

  // The planning baseline: what the system knows about this unit for this period (R2). Fetched
  // once unit + period are set; re-fetched when any of the three change.
  const canBaseline =
    !!value.organizationUnitId && !!value.periodStartDate && !!value.periodEndDate &&
    value.periodEndDate >= value.periodStartDate;
  const baseline = useQuery({
    queryKey: ['manpower-baseline', value.organizationUnitId, value.periodStartDate, value.periodEndDate],
    queryFn: () =>
      jobArchitectureService.getPlanningBaseline(value.organizationUnitId, value.periodStartDate, value.periodEndDate),
    enabled: canBaseline,
    staleTime: 60 * 1000,
  });

  const applyBaseline = (b: ManpowerPlanningBaseline) =>
    onChange({
      ...value,
      currentHeadcount: b.currentHeadcount,
      currentSalaryCost: Math.round(b.currentSalaryCost * 100) / 100,
      plannedTerminations: b.suggestedPlannedTerminations,
    });

  // One automatic fill per baseline result on the create page — never over something typed.
  const filledFor = useRef<string | null>(null);
  useEffect(() => {
    if (!autoFillFromBaseline || !baseline.data) return;
    const key = `${baseline.data.organizationUnitId}|${baseline.data.periodStart}|${baseline.data.periodEnd}`;
    if (filledFor.current === key) return;
    filledFor.current = key;
    applyBaseline(baseline.data);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoFillFromBaseline, baseline.data]);

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>Scope</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <OrganizationUnitPicker
              value={value.organizationUnitId}
              onChange={(unitId, unit) =>
                onChange({
                  ...value,
                  organizationUnitId: unitId,
                  organizationLevelId: unit?.organizationLevelId ?? value.organizationLevelId,
                })
              }
              onLevelChange={(levelId) => set('organizationLevelId', levelId)}
              unitLabel="Organisation unit *"
              idPrefix="mb-unit"
              showCode
              hint="The budget covers this unit and the units under it."
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="mb-year">Fiscal year *</Label>
            <Input
              id="mb-year"
              type="number"
              min={2000}
              max={2100}
              value={value.fiscalYear}
              onChange={(e) => {
                const year = Number(e.target.value);
                // Changing the year re-derives the period from the tenant's fiscal year; the
                // dates stay editable.
                const ok = Number.isFinite(year) && year >= 2000 && year <= 2100;
                const period = ok ? fiscalPeriodFor(year, fiscalStartMonth) : null;
                onChange({
                  ...value,
                  fiscalYear: year,
                  periodStartDate: period ? period.start : value.periodStartDate,
                  periodEndDate: period ? period.end : value.periodEndDate,
                });
              }}
            />
            {/* The API validates this range, so the input mirrors it rather than discovering it. */}
            <p className="text-xs text-muted-foreground">
              Between 2000 and 2100.
              {fiscalStartMonth !== 1 && ` The fiscal year starts in month ${fiscalStartMonth}; the period follows it.`}
            </p>
          </div>
          <div />

          <div className="space-y-2">
            <Label htmlFor="mb-from">Period start *</Label>
            <Input id="mb-from" type="date" value={value.periodStartDate} onChange={(e) => set('periodStartDate', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="mb-to">Period end *</Label>
            <Input id="mb-to" type="date" value={value.periodEndDate} onChange={(e) => set('periodEndDate', e.target.value)} />
            {value.periodStartDate && value.periodEndDate && value.periodEndDate < value.periodStartDate && (
              <p className="text-xs text-destructive">The period must end after it starts.</p>
            )}
          </div>
        </CardContent>
      </Card>

      {/* What the system knows, before anyone types (R2). The three figures it can supply are
          marked on the fields below; the lists and the position table are the evidence. */}
      <PlanningBaselinePanel
        baseline={baseline.data}
        loading={canBaseline && baseline.isLoading}
        error={baseline.isError ? 'The planning baseline could not be loaded.' : undefined}
        idle={!canBaseline}
        actions={
          baseline.data && (
            <Button type="button" variant="outline" size="sm" onClick={() => applyBaseline(baseline.data!)}>
              <Sparkles className="mr-2 h-4 w-4" />
              Use these figures
            </Button>
          )
        }
        onRefresh={canBaseline ? () => baseline.refetch() : undefined}
      />

      <Card>
        <CardHeader>
          <CardTitle>Headcount</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-3">
          <NumberField id="mb-cur-head" label="Current headcount" value={value.currentHeadcount} onChange={(v) => set('currentHeadcount', v)}
            hint={baseline.data ? `System: ${baseline.data.currentHeadcount} on strength in the unit and below` : undefined} />
          <NumberField id="mb-plan-head" label="Planned headcount" value={value.plannedHeadcount} onChange={(v) => set('plannedHeadcount', v)} />
          <NumberField id="mb-hires" label="Planned new hires" value={value.plannedNewHires} onChange={(v) => set('plannedNewHires', v)} />
          <NumberField id="mb-terms" label="Planned terminations" value={value.plannedTerminations} onChange={(v) => set('plannedTerminations', v)}
            hint={baseline.data ? `System: ${baseline.data.exitsDueTotal} exit${baseline.data.exitsDueTotal === 1 ? '' : 's'} due in the period (retirements, contract ends, separations)` : undefined} />
          <NumberField id="mb-promos" label="Planned promotions" value={value.plannedPromotions} onChange={(v) => set('plannedPromotions', v)} />
          <NumberField id="mb-transfers" label="Planned transfers" value={value.plannedTransfers} onChange={(v) => set('plannedTransfers', v)} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Cost</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <NumberField id="mb-cur-cost" label="Current salary cost" value={value.currentSalaryCost} onChange={(v) => set('currentSalaryCost', v)}
            hint={baseline.data ? `System estimate: ${baseline.data.currentSalaryCost.toLocaleString()} monthly basic pay` : undefined} />
          <NumberField id="mb-plan-cost" label="Planned salary cost" value={value.plannedSalaryCost} onChange={(v) => set('plannedSalaryCost', v)} />
          <NumberField id="mb-salary" label="Salary budget" value={value.salaryBudget} onChange={(v) => set('salaryBudget', v)} />
          <NumberField id="mb-benefits" label="Benefits budget" value={value.benefitsBudget} onChange={(v) => set('benefitsBudget', v)} />
          <NumberField id="mb-recruit" label="Recruitment budget" value={value.recruitmentBudget} onChange={(v) => set('recruitmentBudget', v)} />
          <NumberField id="mb-training" label="Training budget" value={value.trainingBudget} onChange={(v) => set('trainingBudget', v)} />
          <div className="rounded-md bg-muted p-3 text-sm sm:col-span-2">
            Total budget: <span className="font-semibold">{total.toLocaleString()}</span>
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
            id="mb-justification"
            rows={4}
            value={value.businessJustification}
            onChange={(e) => set('businessJustification', e.target.value)}
            placeholder="Why this unit needs the posts it is asking for."
            maxLength={2000}
          />
        </CardContent>
      </Card>
    </div>
  );
}

function NumberField({
  id,
  label,
  value,
  onChange,
  hint,
}: {
  id: string;
  label: string;
  value: number;
  onChange: (v: number) => void;
  hint?: string;
}) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="number"
        min={0}
        value={value}
        onChange={(e) => onChange(e.target.value === '' ? 0 : Number(e.target.value))}
      />
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

