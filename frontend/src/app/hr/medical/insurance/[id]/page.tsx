'use client';

import { use } from 'react';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Card, CardContent } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalInsuranceService } from '@/services/hr/medical-reference.service';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { NetworkFacilitiesPanel } from '@/components/hr/medical/NetworkFacilitiesPanel';
import { ProviderDocumentsPanel } from '@/components/hr/medical/ProviderDocumentsPanel';
import { PremiumRecordsPanel } from '@/components/hr/medical/PremiumRecordsPanel';
import { useAuth } from '@/hooks/use-auth';
import { MEDICAL_PLAN_TYPE_OPTIONS, MEDICAL_PROVIDER_TYPE_OPTIONS } from '@/types/hr/medical';
import type { MedicalInsurancePlan } from '@/types/hr/medical';

/**
 * One insurer and the plans it sells.
 *
 * The sub-limits (outpatient, inpatient, dental, optical, maternity) are caps WITHIN the annual
 * limit, not extra cover on top of it — the service enforces them that way when a claim is
 * approved, so entering them as additions will over-state what an employee is entitled to.
 */
const planSchema = z.object({
  name: z.string().min(1, 'Required').max(200),
  code: z.string().min(1, 'Required').max(50),
  planType: z.enum([
    'BasicPlan',
    'StandardPlan',
    'PremiumPlan',
    'ExecutivePlan',
    'FamilyPlan',
    'IndividualPlan',
    'CorporatePlan',
    'StudentPlan',
    'SeniorPlan',
    'MaternityPlan',
    'CatastrophicPlan',
    'CustomPlan',
  ]),
  description: z.string().max(1000).optional(),
  annualLimit: z.coerce.number().min(0, 'Cannot be negative'),
  outpatientLimit: z.coerce.number().min(0).optional(),
  inpatientLimit: z.coerce.number().min(0).optional(),
  dentalLimit: z.coerce.number().min(0).optional(),
  opticalLimit: z.coerce.number().min(0).optional(),
  maternityLimit: z.coerce.number().min(0).optional(),
  coversDependents: z.boolean(),
  maxDependents: z.coerce.number().min(0).optional(),
  monthlyPremium: z.coerce.number().min(0).optional(),
  annualPremium: z.coerce.number().min(0).optional(),
  effectiveDate: z.string().min(1, 'Required'),
  expiryDate: z.string().optional(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional(),
});

type PlanForm = z.input<typeof planSchema>;

const emptyPlan: PlanForm = {
  name: '',
  code: '',
  planType: 'StandardPlan',
  description: '',
  annualLimit: 0,
  outpatientLimit: undefined,
  inpatientLimit: undefined,
  dentalLimit: undefined,
  opticalLimit: undefined,
  maternityLimit: undefined,
  coversDependents: false,
  maxDependents: undefined,
  monthlyPremium: undefined,
  annualPremium: undefined,
  effectiveDate: '',
  expiryDate: '',
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MedicalInsuranceProviderDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const { hasAnyPermission, hasAnyRole } = useAuth();

  /**
   * Medical records are special-category personal data, and the controller's ladder reflects it:
   * create and update are `HR.Medical.Write`, **every delete is `HR.Medical.Admin`**, and the HR
   * role holds Write but not Admin. The role check beside each permission mirrors
   * `HrPermissions.RoleGrants`, which is what keeps a tenant working when its permission rows have
   * not been seeded.
   */
  const canWrite =
    hasAnyPermission(['HR.Medical.Write', 'HR.Medical.Admin']) || hasAnyRole(HR_ROLES);
  const canDelete =
    hasAnyPermission(['HR.Medical.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const { data: provider } = useQuery({
    queryKey: ['hr', 'medical-providers', id],
    queryFn: () => medicalInsuranceService.getProvider(id),
  });

  const typeLabel =
    MEDICAL_PROVIDER_TYPE_OPTIONS.find((o) => o.value === provider?.providerType)?.label ??
    provider?.providerType ??
    '';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={provider?.name ?? 'Provider'}
        description={
          provider
            ? `${typeLabel} · claims processed in ${provider.standardProcessingDays} days · submit within ${provider.claimSubmissionDeadlineDays} days of treatment`
            : 'Loading…'
        }
        backHref="/hr/medical/insurance"
      />

      {provider && (
        <Card>
          <CardContent className="grid gap-4 p-6 sm:grid-cols-2 lg:grid-cols-4">
            <Detail label="Code" value={provider.code} mono />
            <Detail label="Licence" value={provider.licenseNumber} mono />
            <Detail label="Claims hotline" value={provider.claimsHotline || provider.primaryPhone} />
            <Detail label="Claims email" value={provider.claimsEmail || provider.email} />
            <Detail label="Address" value={[provider.address, provider.city].filter(Boolean).join(', ')} />
            <div>
              <p className="text-sm text-muted-foreground">Status</p>
              <StatusBadge active={provider.isActive} />
            </div>
          </CardContent>
        </Card>
      )}

      <ResourceCollectionTab<MedicalInsurancePlan, PlanForm>
        parentId={id}
        title="plans"
        singular="plan"
        queryKey={['hr', 'medical-providers', id, 'plans']}
        invalidateKeys={[['hr', 'medical-providers']]}
        dialogHint="Sub-limits are caps within the annual limit, not cover on top of it."
        emptyDescription="This provider has no plans yet. A plan is what an employee policy points at."
        list={(providerId) => medicalInsuranceService.getPlansByProvider(providerId)}
        create={(providerId, values) => {
          const v = planSchema.parse(values);
          return medicalInsuranceService.createPlan({
            ...v,
            medicalInsuranceProviderId: providerId,
            description: blank(v.description),
            outpatientLimit: v.outpatientLimit ?? null,
            inpatientLimit: v.inpatientLimit ?? null,
            dentalLimit: v.dentalLimit ?? null,
            opticalLimit: v.opticalLimit ?? null,
            maternityLimit: v.maternityLimit ?? null,
            maxDependents: v.maxDependents ?? null,
            monthlyPremium: v.monthlyPremium ?? null,
            annualPremium: v.annualPremium ?? null,
            expiryDate: blank(v.expiryDate),
            notes: blank(v.notes),
          });
        }}
        update={(providerId, planId, values) => {
          const v = planSchema.parse(values);
          return medicalInsuranceService.updatePlan(planId, {
            id: planId,
            ...v,
            medicalInsuranceProviderId: providerId,
            description: blank(v.description),
            outpatientLimit: v.outpatientLimit ?? null,
            inpatientLimit: v.inpatientLimit ?? null,
            dentalLimit: v.dentalLimit ?? null,
            opticalLimit: v.opticalLimit ?? null,
            maternityLimit: v.maternityLimit ?? null,
            maxDependents: v.maxDependents ?? null,
            monthlyPremium: v.monthlyPremium ?? null,
            annualPremium: v.annualPremium ?? null,
            expiryDate: blank(v.expiryDate),
            notes: blank(v.notes),
          });
        }}
        remove={(_providerId, planId) => medicalInsuranceService.removePlan(planId)}
        getId={(p) => p.id}
        columns={[
          { header: 'Plan', cell: (p) => <span className="font-medium">{p.name}</span> },
          {
            header: 'Code',
            cell: (p) => <span className="font-mono text-sm text-muted-foreground">{p.code}</span>,
          },
          {
            header: 'Type',
            cell: (p) => MEDICAL_PLAN_TYPE_OPTIONS.find((o) => o.value === p.planType)?.label ?? p.planType,
          },
          { header: 'Annual limit', cell: (p) => money(p.annualLimit), className: 'text-right' },
          { header: 'Dependants', cell: (p) => (p.coversDependents ? p.maxDependents ?? 'Yes' : '—') },
          { header: 'Effective', cell: (p) => fmtDate(p.effectiveDate) },
          { header: 'Status', cell: (p) => <StatusBadge active={p.isActive} /> },
        ]}
        schema={planSchema as any}
        emptyForm={emptyPlan}
        toForm={(p) => ({
          ...emptyPlan,
          name: p.name,
          code: p.code,
          planType: p.planType,
          description: p.description ?? '',
          annualLimit: p.annualLimit,
          outpatientLimit: p.outpatientLimit ?? undefined,
          inpatientLimit: p.inpatientLimit ?? undefined,
          dentalLimit: p.dentalLimit ?? undefined,
          opticalLimit: p.opticalLimit ?? undefined,
          maternityLimit: p.maternityLimit ?? undefined,
          coversDependents: p.coversDependents,
          maxDependents: p.maxDependents ?? undefined,
          monthlyPremium: p.monthlyPremium ?? undefined,
          annualPremium: p.annualPremium ?? undefined,
          effectiveDate: p.effectiveDate?.slice(0, 10) ?? '',
          expiryDate: p.expiryDate?.slice(0, 10) ?? '',
          isActive: p.isActive,
          notes: p.notes ?? '',
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="name" label="Plan name" required />
              <TextField form={form} name="code" label="Code" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="planType"
                label="Type"
                required
                options={MEDICAL_PLAN_TYPE_OPTIONS}
              />
              <NumberField form={form} name="annualLimit" label="Annual limit" required />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />

            <p className="pt-2 text-sm font-medium">Sub-limits — caps within the annual limit</p>
            <FieldRow>
              <NumberField form={form} name="outpatientLimit" label="Outpatient" />
              <NumberField form={form} name="inpatientLimit" label="Inpatient" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="dentalLimit" label="Dental" />
              <NumberField form={form} name="opticalLimit" label="Optical" />
            </FieldRow>
            <NumberField form={form} name="maternityLimit" label="Maternity" />

            <SwitchField form={form} name="coversDependents" label="Covers dependants" />
            <NumberField form={form} name="maxDependents" label="Maximum dependants" />

            <FieldRow>
              <NumberField form={form} name="monthlyPremium" label="Monthly premium" />
              <NumberField form={form} name="annualPremium" label="Annual premium" />
            </FieldRow>
            <FieldRow>
              <DateField form={form} name="effectiveDate" label="Effective from" required />
              <DateField form={form} name="expiryDate" label="Expires" />
            </FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />

      {/*
        Three collections that had no screen at all. Ordered as an administrator uses them: who an
        employee may be treated by, what the provider is contractually and legally good for, and
        what we were billed.
      */}
      <NetworkFacilitiesPanel providerId={id} canWrite={canWrite} canDelete={canDelete} />

      <ProviderDocumentsPanel providerId={id} canWrite={canWrite} canDelete={canDelete} />

      <PremiumRecordsPanel providerId={id} canWrite={canWrite} />
    </div>
  );
}

function Detail({ label, value, mono }: { label: string; value?: string | null; mono?: boolean }) {
  return (
    <div>
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className={mono ? 'font-mono text-sm' : 'text-sm'}>{value || '—'}</p>
    </div>
  );
}
