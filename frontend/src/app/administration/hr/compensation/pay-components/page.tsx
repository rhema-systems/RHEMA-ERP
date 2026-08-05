'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2, RefreshCw, Info, Lock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  DateField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { payComponentService } from '@/services/hr/compensation.service';
import { formatDate, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import {
  PAY_COMPONENT_TYPE_OPTIONS,
  CALCULATION_BASIS_OPTIONS,
  TAX_TREATMENT_OPTIONS,
} from '@/types/hr/compensation';
import type { PayComponent } from '@/types/hr/compensation';

/**
 * The pay-component master, under split ownership.
 *
 * Payroll owns the component master; HR mirrors it. A mirrored row can only have its HR-owned
 * attributes edited — pension, tax treatment, gross-pay effect and effective dating — because
 * everything else would be overwritten by the next sync. Rather than let someone fill in a form
 * that will 409, the dialog shows the payroll-owned fields as read-only text for those rows and
 * offers inputs only for what can actually be saved.
 *
 * Components HR defined itself (the six from the emolument seeder) are fully editable, so they
 * get the whole form.
 */

/** Everything editable on an HR-defined component. Mirrored rows submit only the HR-owned half. */
const componentSchema = z
  .object({
    // Payroll-owned — present only for HR-defined components.
    code: z.string().max(50).optional(),
    name: z.string().max(150).optional(),
    description: z.string().max(1000).optional(),
    componentType: z.enum(['Allowance', 'Deduction', 'BenefitInKindNotional']).optional(),
    calculationBasis: z.enum(['FixedAmount', 'PercentageOfBasic']).optional(),
    defaultAmount: z.coerce.number().min(0).optional(),
    isTaxable: z.boolean().optional(),
    isActive: z.boolean().optional(),

    // HR-owned — always editable.
    isPensionable: z.boolean(),
    affectsGrossPay: z.boolean(),
    statutoryTreatment: z.enum(['None', 'PAYE', 'WithholdingTax']),
    effectiveFrom: z.string().min(1, 'Required'),
    effectiveTo: z.string().optional(),
  })
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
    message: 'The effective-to date cannot be before the effective-from date',
    path: ['effectiveTo'],
  });

type ComponentForm = z.input<typeof componentSchema>;

const emptyComponent: ComponentForm = {
  code: '',
  name: '',
  description: '',
  componentType: 'Allowance',
  calculationBasis: 'FixedAmount',
  defaultAmount: undefined,
  isTaxable: true,
  isActive: true,
  isPensionable: false,
  affectsGrossPay: true,
  statutoryTreatment: 'PAYE',
  effectiveFrom: '',
  effectiveTo: '',
};

/** `2026-08-04T00:00:00` → `2026-08-04` for a date input. */
const toDateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');

function ReadOnlyRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b py-1.5 last:border-0">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

export default function PayComponentsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [showInactive, setShowInactive] = useState(false);
  const [syncing, setSyncing] = useState(false);

  const { data: components } = useQuery({
    queryKey: ['hr', 'pay-components', showInactive],
    queryFn: () => payComponentService.getAll(!showInactive),
  });

  const mirrored = (components ?? []).filter((c) => c.isPayrollDefined).length;

  const runSync = async () => {
    setSyncing(true);
    try {
      const r = await payComponentService.sync();
      await queryClient.invalidateQueries({ queryKey: ['hr', 'pay-components'] });
      toast({
        title: r.skippedAsUnchanged ? 'Already up to date' : 'Synced from Payroll',
        description: r.skippedAsUnchanged
          ? 'Nothing has changed in Payroll since the last pass.'
          : `${r.created} added, ${r.updated} updated, ${r.deactivated} deactivated.` +
            (r.warnings.length ? ` ${r.warnings.length} warning(s).` : ''),
      });
      // Warnings are the useful part when payroll data is messy, so surface them individually.
      r.warnings.forEach((w) =>
        toast({ title: 'Sync warning', description: w, variant: 'destructive' }),
      );
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to sync from Payroll.',
        variant: 'destructive',
      });
    } finally {
      setSyncing(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Pay Components"
        description="Allowances and deductions that emoluments, benefits and leave encashment are built from."
        backHref="/administration/hr/compensation"
        actions={
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <Switch id="showInactive" checked={showInactive} onCheckedChange={setShowInactive} />
              <label htmlFor="showInactive" className="text-sm">
                Show inactive
              </label>
            </div>
            <Button variant="outline" onClick={runSync} disabled={syncing}>
              {syncing ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Sync from Payroll
            </Button>
          </div>
        }
      />

      <Card>
        <CardContent className="flex items-start gap-3 p-4 text-sm">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
          <div className="space-y-1">
            <p>
              Payroll is the source of truth for the component master. HR mirrors it and reconciles
              on read, so a component added in Payroll appears here shortly afterwards.
            </p>
            <p className="text-muted-foreground">
              On a mirrored component you can edit pension, tax treatment, gross-pay effect and
              effective dates — Payroll models none of those. Its code, name, type, basis, amount
              and active flag are edited in Payroll.
              {mirrored > 0 && ` ${mirrored} of ${components?.length ?? 0} shown are mirrored.`}
            </p>
          </div>
        </CardContent>
      </Card>

      <ResourceListPanel<PayComponent, ComponentForm>
        title="pay components"
        singular="component"
        queryKey={['hr', 'pay-components', showInactive]}
        dialogClassName="sm:max-w-[620px]"
        // Creating in HR is always refused — new components must come from Payroll, or the two
        // parallel sets start diverging again. No add affordance is offered rather than letting
        // someone fill in a form that would 409.
        allowCreate={false}
        list={() => payComponentService.getAll(!showInactive)}
        create={async () => undefined}
        update={(id, values) => {
          const v = componentSchema.parse(values);
          const existing = (components ?? []).find((c) => c.id === id);

          const hrAttributes = {
            isPensionable: v.isPensionable,
            affectsGrossPay: v.affectsGrossPay,
            statutoryTreatment: v.statutoryTreatment,
            effectiveFrom: v.effectiveFrom,
            effectiveTo: v.effectiveTo || null,
          };

          // Mirrored rows: only the HR-owned half is sendable, the rest would 409.
          if (existing?.isPayrollDefined) {
            return payComponentService.updateHrAttributes(id, hrAttributes);
          }

          return payComponentService.update(id, {
            code: v.code ?? '',
            name: v.name ?? '',
            description: v.description || null,
            componentType: v.componentType ?? 'Allowance',
            calculationBasis: v.calculationBasis ?? 'FixedAmount',
            defaultAmount: v.defaultAmount ?? null,
            isTaxable: v.isTaxable ?? true,
            isActive: v.isActive ?? true,
            ...hrAttributes,
          });
        }}
        getId={(c) => c.id}
        actions={[
          {
            label: 'Deactivate',
            visible: (c) => c.isActive && !c.isPayrollDefined,
            destructive: true,
            run: async (c) => {
              await payComponentService.deactivate(c.id);
              await queryClient.invalidateQueries({ queryKey: ['hr', 'pay-components'] });
            },
            confirm: {
              title: 'Deactivate this component?',
              description:
                'It stops applying to new emoluments. Existing assignments keep their history.',
            },
          },
        ]}
        emptyDescription="Components are defined in Payroll. Use “Sync from Payroll” to pull them in."
        columns={[
          {
            header: 'Code',
            cell: (c) => (
              <div className="flex items-center gap-2">
                <span className="font-medium">{c.code}</span>
                {c.isPayrollDefined && (
                  <Badge variant="outline" className="gap-1">
                    <Lock className="h-3 w-3" /> Payroll
                  </Badge>
                )}
              </div>
            ),
          },
          { header: 'Name', cell: (c) => c.name },
          { header: 'Type', cell: (c) => humanizeEnum(c.componentType) },
          {
            header: 'Default',
            cell: (c) =>
              c.defaultAmount == null
                ? '—'
                : c.calculationBasis === 'PercentageOfBasic'
                  ? `${c.defaultAmount}% of basic`
                  : formatMoney(c.defaultAmount),
            className: 'text-right',
          },
          { header: 'Taxable', cell: (c) => (c.isTaxable ? 'Yes' : 'No') },
          { header: 'Pensionable', cell: (c) => (c.isPensionable ? 'Yes' : 'No') },
          { header: 'Tax treatment', cell: (c) => humanizeEnum(c.statutoryTreatment) },
          { header: 'Effective', cell: (c) => formatDate(c.effectiveFrom) },
          { header: 'Status', cell: (c) => <StatusBadge active={c.isActive} /> },
        ]}
        schema={componentSchema as any}
        emptyForm={emptyComponent}
        toForm={(c) => ({
          code: c.code,
          name: c.name,
          description: c.description ?? '',
          componentType: c.componentType,
          calculationBasis: c.calculationBasis,
          defaultAmount: c.defaultAmount ?? undefined,
          isTaxable: c.isTaxable,
          isActive: c.isActive,
          isPensionable: c.isPensionable,
          affectsGrossPay: c.affectsGrossPay,
          statutoryTreatment: c.statutoryTreatment,
          effectiveFrom: toDateInput(c.effectiveFrom),
          effectiveTo: toDateInput(c.effectiveTo),
        })}
        renderFields={(form) => {
          // The dialog does not carry the row, so provenance is recovered by matching the code
          // it was populated from.
          const code = form.watch('code');
          const existing = (components ?? []).find((c) => c.code === code);
          const isMirrored = !!existing?.isPayrollDefined;

          return (
            <>
              {isMirrored ? (
                <div className="rounded-md border bg-muted/40 p-3">
                  <p className="mb-2 flex items-center gap-2 text-xs font-medium text-muted-foreground">
                    <Lock className="h-3 w-3" /> Defined in Payroll — edit these there
                  </p>
                  <ReadOnlyRow label="Code" value={existing?.code} />
                  <ReadOnlyRow label="Name" value={existing?.name} />
                  <ReadOnlyRow
                    label="Type"
                    value={humanizeEnum(existing?.componentType)}
                  />
                  <ReadOnlyRow
                    label="Calculation"
                    value={humanizeEnum(existing?.calculationBasis)}
                  />
                  <ReadOnlyRow
                    label="Default amount"
                    value={
                      existing?.defaultAmount == null
                        ? '—'
                        : existing.calculationBasis === 'PercentageOfBasic'
                          ? `${existing.defaultAmount}% of basic`
                          : formatMoney(existing.defaultAmount)
                    }
                  />
                  <ReadOnlyRow label="Taxable" value={existing?.isTaxable ? 'Yes' : 'No'} />
                  <ReadOnlyRow label="Active" value={existing?.isActive ? 'Yes' : 'No'} />
                </div>
              ) : (
                <>
                  <FieldRow>
                    <TextField form={form} name="code" label="Code" required />
                    <TextField form={form} name="name" label="Name" required />
                  </FieldRow>
                  <TextareaField form={form} name="description" label="Description" rows={2} />
                  <FieldRow>
                    <SelectField
                      form={form}
                      name="componentType"
                      label="Type"
                      required
                      options={PAY_COMPONENT_TYPE_OPTIONS}
                    />
                    <SelectField
                      form={form}
                      name="calculationBasis"
                      label="Calculation basis"
                      required
                      options={CALCULATION_BASIS_OPTIONS}
                    />
                  </FieldRow>
                  <NumberField
                    form={form}
                    name="defaultAmount"
                    label={
                      form.watch('calculationBasis') === 'PercentageOfBasic'
                        ? 'Default (% of basic)'
                        : 'Default amount'
                    }
                    step="0.01"
                  />
                  <SwitchField form={form} name="isTaxable" label="Taxable" />
                  <SwitchField form={form} name="isActive" label="Active" />
                </>
              )}

              <p className="pt-2 text-sm font-medium">HR attributes</p>
              <p className="text-xs text-muted-foreground">
                Payroll models none of these, so they survive every sync.
              </p>
              <SwitchField
                form={form}
                name="isPensionable"
                label="Pensionable"
                description="Counts toward pension / SSNIT contributions."
              />
              <SwitchField
                form={form}
                name="affectsGrossPay"
                label="Affects gross pay"
                description="Turn off for notional benefit-in-kind lines."
              />
              <SelectField
                form={form}
                name="statutoryTreatment"
                label="Tax treatment"
                required
                options={TAX_TREATMENT_OPTIONS}
              />
              <FieldRow>
                <DateField form={form} name="effectiveFrom" label="Effective from" required />
                <DateField form={form} name="effectiveTo" label="Effective to" />
              </FieldRow>
              <p className="text-xs text-muted-foreground">
                Emoluments and the leave-encashment rate only count a component inside this window.
              </p>
            </>
          );
        }}
      />
    </div>
  );
}
