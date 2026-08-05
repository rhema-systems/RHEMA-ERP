'use client';

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
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
import { benefitPolicyService } from '@/services/hr/benefits.service';
import { formatDate, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import type { BenefitPolicy, EnumOption } from '@/types/hr/benefits';

/**
 * Benefit policies — the schemes employees enrol into.
 *
 * Every enum here comes from `GET /lookups` rather than a hardcoded list, so the selects track
 * the backend's enums without this file needing to change. The lookup's `name` is what goes back
 * on the wire; `label` is only for display.
 *
 * A policy carries a lot: the headline terms, plus a nested `definition` block covering delivery,
 * valuation, tax and the payroll link. The dialog groups them rather than presenting forty flat
 * fields. Per-grade values and dependant relations live on the policy's own page — they are
 * collections, not fields.
 */
const policySchema = z
  .object({
    policyName: z.string().min(1, 'A name is required').max(200),
    policyCode: z.string().max(50).optional(),
    description: z.string().max(1000).optional(),
    policyType: z.string().min(1, 'Required'),
    recipient: z.string().min(1, 'Required'),
    maxDependents: z.coerce.number().min(0).optional(),
    coverageLimit: z.coerce.number().min(0),
    limitPeriod: z.string().min(1, 'Required'),
    effectiveFrom: z.string().min(1, 'Required'),
    effectiveTo: z.string().optional(),
    isMandatory: z.boolean(),
    isActive: z.boolean(),

    // Nested definition block.
    deliveryType: z.string().min(1, 'Required'),
    currency: z.string().length(3, 'Use a 3-letter currency code'),
    frequency: z.string().min(1, 'Required'),
    calculationBasis: z.string().min(1, 'Required'),
    isTaxable: z.boolean(),
    taxTreatment: z.string().min(1, 'Required'),
    taxablePercentage: z.coerce.number().min(0).max(100).optional(),
    valuationMethod: z.string().min(1, 'Required'),
    flatValue: z.coerce.number().min(0).optional(),
    valuationRate: z.coerce.number().min(0).optional(),
    valuationCap: z.coerce.number().min(0).optional(),
    taxExemptThreshold: z.coerce.number().min(0).optional(),
    isPensionable: z.boolean(),
    affectsGrossPay: z.boolean(),
    affectsNetPay: z.boolean(),
    contributionResponsibility: z.string().min(1, 'Required'),
    employerContributionRate: z.coerce.number().min(0).max(100).optional(),
    employeeContributionRate: z.coerce.number().min(0).max(100).optional(),
    minServiceMonths: z.coerce.number().min(0).optional(),
    availableDuringProbation: z.boolean(),
    payComponentId: z.string().optional(),
  })
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
    message: 'The effective-to date cannot be before the effective-from date',
    path: ['effectiveTo'],
  });

type PolicyForm = z.input<typeof policySchema>;

const emptyPolicy: PolicyForm = {
  policyName: '',
  policyCode: '',
  description: '',
  policyType: '',
  recipient: '',
  maxDependents: undefined,
  coverageLimit: 0,
  limitPeriod: '',
  effectiveFrom: '',
  effectiveTo: '',
  isMandatory: false,
  isActive: true,
  deliveryType: '',
  currency: 'GHS',
  frequency: '',
  calculationBasis: '',
  isTaxable: true,
  taxTreatment: '',
  taxablePercentage: undefined,
  valuationMethod: '',
  flatValue: undefined,
  valuationRate: undefined,
  valuationCap: undefined,
  taxExemptThreshold: undefined,
  isPensionable: false,
  affectsGrossPay: true,
  affectsNetPay: true,
  contributionResponsibility: '',
  employerContributionRate: undefined,
  employeeContributionRate: undefined,
  minServiceMonths: undefined,
  availableDuringProbation: true,
  payComponentId: '',
};

const toDateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');

/** Lookup options arrive as {value,name,label}; the API wants `name` back. */
const toOptions = (list?: EnumOption[]) =>
  (list ?? []).map((o) => ({ value: o.name, label: o.label || o.name }));

export default function BenefitPoliciesPage() {
  const router = useRouter();

  const { data: lookups } = useQuery({
    queryKey: ['hr', 'benefit-policy-lookups'],
    queryFn: () => benefitPolicyService.getLookups(),
  });

  const payComponentOptions = useMemo(
    () =>
      (lookups?.payComponents ?? []).map((c) => ({
        value: c.id,
        label: `${c.code} · ${c.name}`,
      })),
    [lookups],
  );

  /** Splits the flat form back into the headline fields plus the nested definition block. */
  const toPayload = (values: PolicyForm) => {
    const v = policySchema.parse(values);
    const blank = (s?: string) => (s && s.length > 0 ? s : null);
    return {
      policyName: v.policyName,
      policyCode: blank(v.policyCode),
      description: blank(v.description),
      policyType: v.policyType,
      recipient: v.recipient,
      maxDependents: v.maxDependents ?? null,
      employeeContribution: null,
      employerContribution: null,
      coverageLimit: v.coverageLimit,
      limitPeriod: v.limitPeriod,
      effectiveFrom: v.effectiveFrom,
      effectiveTo: blank(v.effectiveTo),
      isMandatory: v.isMandatory,
      definition: {
        deliveryType: v.deliveryType,
        currency: v.currency,
        frequency: v.frequency,
        calculationBasis: v.calculationBasis,
        isTaxable: v.isTaxable,
        taxTreatment: v.taxTreatment,
        taxablePercentage: v.taxablePercentage ?? null,
        valuationMethod: v.valuationMethod,
        flatValue: v.flatValue ?? null,
        valuationRate: v.valuationRate ?? null,
        valuationCap: v.valuationCap ?? null,
        taxExemptThreshold: v.taxExemptThreshold ?? null,
        isPensionable: v.isPensionable,
        affectsGrossPay: v.affectsGrossPay,
        affectsNetPay: v.affectsNetPay,
        contributionResponsibility: v.contributionResponsibility,
        employerContributionRate: v.employerContributionRate ?? null,
        employeeContributionRate: v.employeeContributionRate ?? null,
        minServiceMonths: v.minServiceMonths ?? null,
        availableDuringProbation: v.availableDuringProbation,
        payComponentId: blank(v.payComponentId),
      },
    };
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Benefit Policies"
        description="Benefit schemes, their coverage terms, valuation and tax treatment."
        backHref="/administration/hr/compensation"
      />

      <ResourceListPanel<BenefitPolicy, PolicyForm>
        title="benefit policies"
        singular="policy"
        queryKey={['hr', 'benefit-policies']}
        dialogClassName="sm:max-w-[720px]"
        dialogHint="Per-grade values and dependant rules are managed on the policy's own page once it exists."
        list={() => benefitPolicyService.getAll()}
        create={(values) => benefitPolicyService.create(toPayload(values) as any)}
        update={(id, values) => {
          const v = policySchema.parse(values);
          return benefitPolicyService.update(id, {
            id,
            ...toPayload(values),
            isActive: v.isActive,
          } as any);
        }}
        remove={(id) => benefitPolicyService.remove(id)}
        getId={(p) => p.id}
        actions={[
          {
            label: 'Grade values & rules',
            run: async (p) => {
              router.push(`/administration/hr/compensation/benefit-policies/${p.id}`);
            },
          },
        ]}
        columns={[
          {
            header: 'Policy',
            cell: (p) => (
              <div className="flex items-center gap-2">
                <span className="font-medium">{p.policyName}</span>
                {p.isMandatory && <Badge variant="secondary">Mandatory</Badge>}
              </div>
            ),
          },
          { header: 'Code', cell: (p) => p.policyCode || '—' },
          { header: 'Type', cell: (p) => humanizeEnum(p.policyType) },
          { header: 'Recipient', cell: (p) => humanizeEnum(p.recipient) },
          {
            header: 'Coverage',
            cell: (p) => `${formatMoney(p.coverageLimit, p.definition?.currency ?? 'GHS')} / ${humanizeEnum(p.limitPeriod)}`,
            className: 'text-right',
          },
          { header: 'From', cell: (p) => formatDate(p.effectiveFrom) },
          { header: 'Status', cell: (p) => <StatusBadge active={p.isActive} /> },
          {
            header: '',
            cell: (p) => (
              <Button
                variant="link"
                size="sm"
                className="h-auto p-0"
                onClick={(e) => {
                  e.stopPropagation();
                  router.push(`/administration/hr/compensation/benefit-policies/${p.id}`);
                }}
              >
                Open
              </Button>
            ),
          },
        ]}
        schema={policySchema as any}
        emptyForm={emptyPolicy}
        toForm={(p) => ({
          policyName: p.policyName,
          policyCode: p.policyCode ?? '',
          description: p.description ?? '',
          policyType: p.policyType,
          recipient: p.recipient,
          maxDependents: p.maxDependents ?? undefined,
          coverageLimit: p.coverageLimit,
          limitPeriod: p.limitPeriod,
          effectiveFrom: toDateInput(p.effectiveFrom),
          effectiveTo: toDateInput(p.effectiveTo),
          isMandatory: p.isMandatory,
          isActive: p.isActive,
          deliveryType: p.definition?.deliveryType ?? '',
          currency: p.definition?.currency ?? 'GHS',
          frequency: p.definition?.frequency ?? '',
          calculationBasis: p.definition?.calculationBasis ?? '',
          isTaxable: p.definition?.isTaxable ?? true,
          taxTreatment: p.definition?.taxTreatment ?? '',
          taxablePercentage: p.definition?.taxablePercentage ?? undefined,
          valuationMethod: p.definition?.valuationMethod ?? '',
          flatValue: p.definition?.flatValue ?? undefined,
          valuationRate: p.definition?.valuationRate ?? undefined,
          valuationCap: p.definition?.valuationCap ?? undefined,
          taxExemptThreshold: p.definition?.taxExemptThreshold ?? undefined,
          isPensionable: p.definition?.isPensionable ?? false,
          affectsGrossPay: p.definition?.affectsGrossPay ?? true,
          affectsNetPay: p.definition?.affectsNetPay ?? true,
          contributionResponsibility: p.definition?.contributionResponsibility ?? '',
          employerContributionRate: p.definition?.employerContributionRate ?? undefined,
          employeeContributionRate: p.definition?.employeeContributionRate ?? undefined,
          minServiceMonths: p.definition?.minServiceMonths ?? undefined,
          availableDuringProbation: p.definition?.availableDuringProbation ?? true,
          payComponentId: p.definition?.payComponentId ?? '',
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="policyName" label="Policy name" required />
              <TextField form={form} name="policyCode" label="Code" />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="policyType"
                label="Policy type"
                required
                options={toOptions(lookups?.policyTypes)}
              />
              <SelectField
                form={form}
                name="recipient"
                label="Recipient"
                required
                options={toOptions(lookups?.recipients)}
              />
            </FieldRow>

            <p className="pt-2 text-sm font-medium">Coverage</p>
            <FieldRow>
              <NumberField form={form} name="coverageLimit" label="Coverage limit" step="0.01" required />
              <SelectField
                form={form}
                name="limitPeriod"
                label="Limit period"
                required
                options={toOptions(lookups?.limitPeriods)}
              />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="maxDependents" label="Max dependants" />
              <TextField form={form} name="currency" label="Currency" required />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="effectiveFrom" label="Effective from" required />
              <DateField form={form} name="effectiveTo" label="Effective to" />
            </FieldRow>
            <SwitchField
              form={form}
              name="isMandatory"
              label="Mandatory"
              description="Eligible employees are enrolled automatically rather than opting in."
            />
            <SwitchField form={form} name="isActive" label="Active" />

            <p className="pt-2 text-sm font-medium">Delivery &amp; valuation</p>
            <FieldRow>
              <SelectField
                form={form}
                name="deliveryType"
                label="Delivery"
                required
                options={toOptions(lookups?.deliveryTypes)}
              />
              <SelectField
                form={form}
                name="frequency"
                label="Frequency"
                required
                options={toOptions(lookups?.frequencies)}
              />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="calculationBasis"
                label="Calculation basis"
                required
                options={toOptions(lookups?.calculationBases)}
              />
              <SelectField
                form={form}
                name="valuationMethod"
                label="Valuation method"
                required
                options={toOptions(lookups?.valuationMethods)}
              />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="flatValue" label="Flat value" step="0.01" />
              <NumberField form={form} name="valuationRate" label="Valuation rate (%)" step="0.01" />
            </FieldRow>
            <NumberField form={form} name="valuationCap" label="Valuation cap" step="0.01" />

            <p className="pt-2 text-sm font-medium">Tax &amp; statutory</p>
            <SwitchField form={form} name="isTaxable" label="Taxable" />
            <FieldRow>
              <SelectField
                form={form}
                name="taxTreatment"
                label="Tax treatment"
                required
                options={toOptions(lookups?.taxTreatments)}
              />
              <NumberField
                form={form}
                name="taxablePercentage"
                label="Taxable percentage"
                step="0.01"
              />
            </FieldRow>
            <NumberField
              form={form}
              name="taxExemptThreshold"
              label="Tax-exempt threshold"
              step="0.01"
            />
            <SwitchField form={form} name="isPensionable" label="Pensionable" />
            <SwitchField form={form} name="affectsGrossPay" label="Affects gross pay" />
            <SwitchField form={form} name="affectsNetPay" label="Affects net pay" />

            <p className="pt-2 text-sm font-medium">Contribution &amp; eligibility</p>
            <SelectField
              form={form}
              name="contributionResponsibility"
              label="Who contributes"
              required
              options={toOptions(lookups?.contributionResponsibilities)}
            />
            <FieldRow>
              <NumberField
                form={form}
                name="employerContributionRate"
                label="Employer rate (%)"
                step="0.01"
              />
              <NumberField
                form={form}
                name="employeeContributionRate"
                label="Employee rate (%)"
                step="0.01"
              />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="minServiceMonths" label="Min service (months)" />
              <SwitchField
                form={form}
                name="availableDuringProbation"
                label="Available during probation"
              />
            </FieldRow>

            <p className="pt-2 text-sm font-medium">Payroll link</p>
            <SelectField
              form={form}
              name="payComponentId"
              label="Pay component"
              options={payComponentOptions}
              allowEmpty
              emptyLabel="Not linked"
            />
            <p className="text-xs text-muted-foreground">
              Optional. Links this benefit's money to the pay component payroll resolves it
              through, keeping the element logic in one place.
            </p>
          </>
        )}
      />
    </div>
  );
}
