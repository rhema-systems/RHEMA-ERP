'use client';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';
import type { ManpowerBudget } from '@/types/hr/job-architecture';

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
}: {
  value: ManpowerBudgetFormState;
  onChange: (next: ManpowerBudgetFormState) => void;
}) {
  const set = <K extends keyof ManpowerBudgetFormState>(key: K, v: ManpowerBudgetFormState[K]) =>
    onChange({ ...value, [key]: v });

  const total = value.salaryBudget + value.benefitsBudget + value.recruitmentBudget + value.trainingBudget;

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
                // Changing the year re-derives a calendar-year period; the dates stay editable.
                onChange({
                  ...value,
                  fiscalYear: year,
                  periodStartDate: Number.isFinite(year) && year > 0 ? `${year}-01-01` : value.periodStartDate,
                  periodEndDate: Number.isFinite(year) && year > 0 ? `${year}-12-31` : value.periodEndDate,
                });
              }}
            />
            {/* The API validates this range, so the input mirrors it rather than discovering it. */}
            <p className="text-xs text-muted-foreground">Between 2000 and 2100.</p>
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

      <Card>
        <CardHeader>
          <CardTitle>Headcount</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-3">
          <NumberField id="mb-cur-head" label="Current headcount" value={value.currentHeadcount} onChange={(v) => set('currentHeadcount', v)} />
          <NumberField id="mb-plan-head" label="Planned headcount" value={value.plannedHeadcount} onChange={(v) => set('plannedHeadcount', v)} />
          <NumberField id="mb-hires" label="Planned new hires" value={value.plannedNewHires} onChange={(v) => set('plannedNewHires', v)} />
          <NumberField id="mb-terms" label="Planned terminations" value={value.plannedTerminations} onChange={(v) => set('plannedTerminations', v)} />
          <NumberField id="mb-promos" label="Planned promotions" value={value.plannedPromotions} onChange={(v) => set('plannedPromotions', v)} />
          <NumberField id="mb-transfers" label="Planned transfers" value={value.plannedTransfers} onChange={(v) => set('plannedTransfers', v)} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Cost</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <NumberField id="mb-cur-cost" label="Current salary cost" value={value.currentSalaryCost} onChange={(v) => set('currentSalaryCost', v)} />
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
}: {
  id: string;
  label: string;
  value: number;
  onChange: (v: number) => void;
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
    </div>
  );
}
