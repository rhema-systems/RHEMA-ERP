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
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalBenefitSchemeService } from '@/services/hr/medical-reference.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import type { MedicalBenefitTier } from '@/types/hr/medical';

/**
 * One benefit scheme and the tiers inside it.
 *
 * A tier is the entitlement for a staff level: an annual limit, optional caps within it, and
 * whether dependants are covered. Leaving the staff level unset makes the tier a general one —
 * useful for a scheme that does not vary by grade.
 *
 * As on insurance plans, the sub-limits are caps WITHIN the annual limit rather than cover on top
 * of it.
 */
const tierSchema = z.object({
  tierName: z.string().min(1, 'Required').max(100),
  staffLevelId: z.string().optional(),
  tierDescription: z.string().max(500).optional(),
  annualLimit: z.coerce.number().min(0, 'Cannot be negative'),
  inpatientLimit: z.coerce.number().min(0).optional(),
  outpatientLimit: z.coerce.number().min(0).optional(),
  dentalLimit: z.coerce.number().min(0).optional(),
  opticalLimit: z.coerce.number().min(0).optional(),
  maternityLimit: z.coerce.number().min(0).optional(),
  mentalHealthLimit: z.coerce.number().min(0).optional(),
  prescriptionLimit: z.coerce.number().min(0).optional(),
  coversDependents: z.boolean(),
  maxDependents: z.coerce.number().min(0).optional(),
  dependentAnnualLimit: z.coerce.number().min(0).optional(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional(),
});

type TierForm = z.input<typeof tierSchema>;

const emptyTier: TierForm = {
  tierName: '',
  staffLevelId: '',
  tierDescription: '',
  annualLimit: 0,
  inpatientLimit: undefined,
  outpatientLimit: undefined,
  dentalLimit: undefined,
  opticalLimit: undefined,
  maternityLimit: undefined,
  mentalHealthLimit: undefined,
  prescriptionLimit: undefined,
  coversDependents: false,
  maxDependents: undefined,
  dependentAnnualLimit: undefined,
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MedicalBenefitSchemeDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);

  const { data: scheme } = useQuery({
    queryKey: ['hr', 'medical-schemes', id],
    queryFn: () => medicalBenefitSchemeService.getScheme(id),
  });

  const { data: staffLevels = [] } = useQuery({
    queryKey: ['hr', 'staff-levels', 'active'],
    queryFn: () => staffLevelService.getActive(),
  });

  // The "all staff" choice is SelectField's allowEmpty item: Radix throws on an item whose value is ''.
  const staffLevelOptions = staffLevels.map((l) => ({ value: l.id, label: l.name }));

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={scheme?.name ?? 'Benefit scheme'}
        description={
          scheme
            ? `Effective ${fmtDate(scheme.effectiveDate)}${scheme.expiryDate ? ` — ${fmtDate(scheme.expiryDate)}` : ''}`
            : 'Loading…'
        }
        backHref="/hr/medical/schemes"
      />

      {scheme && (
        <Card>
          <CardContent className="grid gap-4 p-6 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <p className="text-sm text-muted-foreground">Code</p>
              <p className="font-mono text-sm">{scheme.code}</p>
            </div>
            <div className="sm:col-span-2">
              <p className="text-sm text-muted-foreground">Description</p>
              <p className="text-sm">{scheme.description || '—'}</p>
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Status</p>
              <StatusBadge active={scheme.isActive} />
            </div>
          </CardContent>
        </Card>
      )}

      <ResourceCollectionTab<MedicalBenefitTier, TierForm>
        parentId={id}
        title="tiers"
        singular="tier"
        queryKey={['hr', 'medical-schemes', id, 'tiers']}
        invalidateKeys={[['hr', 'medical-schemes']]}
        dialogHint="Sub-limits are caps within the annual limit. Leave the staff level unset for a tier that applies to everyone."
        emptyDescription="This scheme has no tiers yet. Until it has one, it grants nothing."
        list={(schemeId) => medicalBenefitSchemeService.getTiersByScheme(schemeId)}
        create={(schemeId, values) => {
          const v = tierSchema.parse(values);
          return medicalBenefitSchemeService.createTier({
            ...v,
            schemeId,
            staffLevelId: blank(v.staffLevelId),
            tierDescription: blank(v.tierDescription),
            inpatientLimit: v.inpatientLimit ?? null,
            outpatientLimit: v.outpatientLimit ?? null,
            dentalLimit: v.dentalLimit ?? null,
            opticalLimit: v.opticalLimit ?? null,
            maternityLimit: v.maternityLimit ?? null,
            mentalHealthLimit: v.mentalHealthLimit ?? null,
            prescriptionLimit: v.prescriptionLimit ?? null,
            maxDependents: v.maxDependents ?? null,
            dependentAnnualLimit: v.dependentAnnualLimit ?? null,
            notes: blank(v.notes),
          });
        }}
        update={(schemeId, tierId, values) => {
          const v = tierSchema.parse(values);
          return medicalBenefitSchemeService.updateTier(tierId, {
            id: tierId,
            ...v,
            schemeId,
            staffLevelId: blank(v.staffLevelId),
            tierDescription: blank(v.tierDescription),
            inpatientLimit: v.inpatientLimit ?? null,
            outpatientLimit: v.outpatientLimit ?? null,
            dentalLimit: v.dentalLimit ?? null,
            opticalLimit: v.opticalLimit ?? null,
            maternityLimit: v.maternityLimit ?? null,
            mentalHealthLimit: v.mentalHealthLimit ?? null,
            prescriptionLimit: v.prescriptionLimit ?? null,
            maxDependents: v.maxDependents ?? null,
            dependentAnnualLimit: v.dependentAnnualLimit ?? null,
            notes: blank(v.notes),
          });
        }}
        remove={(_schemeId, tierId) => medicalBenefitSchemeService.removeTier(tierId)}
        getId={(t) => t.id}
        columns={[
          { header: 'Tier', cell: (t) => <span className="font-medium">{t.tierName}</span> },
          { header: 'Staff level', cell: (t) => t.staffLevelName || 'All staff' },
          { header: 'Annual limit', cell: (t) => money(t.annualLimit), className: 'text-right' },
          { header: 'Inpatient', cell: (t) => money(t.inpatientLimit), className: 'text-right' },
          { header: 'Outpatient', cell: (t) => money(t.outpatientLimit), className: 'text-right' },
          {
            header: 'Dependants',
            cell: (t) => (t.coversDependents ? t.maxDependents ?? 'Yes' : '—'),
          },
          { header: 'Status', cell: (t) => <StatusBadge active={t.isActive} /> },
        ]}
        schema={tierSchema as any}
        emptyForm={emptyTier}
        toForm={(t) => ({
          ...emptyTier,
          tierName: t.tierName,
          staffLevelId: t.staffLevelId ?? '',
          tierDescription: t.tierDescription ?? '',
          annualLimit: t.annualLimit,
          inpatientLimit: t.inpatientLimit ?? undefined,
          outpatientLimit: t.outpatientLimit ?? undefined,
          dentalLimit: t.dentalLimit ?? undefined,
          opticalLimit: t.opticalLimit ?? undefined,
          maternityLimit: t.maternityLimit ?? undefined,
          mentalHealthLimit: t.mentalHealthLimit ?? undefined,
          prescriptionLimit: t.prescriptionLimit ?? undefined,
          coversDependents: t.coversDependents,
          maxDependents: t.maxDependents ?? undefined,
          dependentAnnualLimit: t.dependentAnnualLimit ?? undefined,
          isActive: t.isActive,
          notes: t.notes ?? '',
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="tierName" label="Tier name" required />
              <SelectField
                form={form}
                name="staffLevelId"
                label="Staff level"
                options={staffLevelOptions}
                allowEmpty
                emptyLabel="All staff — not tied to a level"
              />
            </FieldRow>
            <TextareaField form={form} name="tierDescription" label="Description" rows={2} />
            <NumberField form={form} name="annualLimit" label="Annual limit" required />

            <p className="pt-2 text-sm font-medium">Sub-limits — caps within the annual limit</p>
            <FieldRow>
              <NumberField form={form} name="inpatientLimit" label="Inpatient" />
              <NumberField form={form} name="outpatientLimit" label="Outpatient" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="dentalLimit" label="Dental" />
              <NumberField form={form} name="opticalLimit" label="Optical" />
            </FieldRow>
            <FieldRow>
              <NumberField form={form} name="maternityLimit" label="Maternity" />
              <NumberField form={form} name="mentalHealthLimit" label="Mental health" />
            </FieldRow>
            <NumberField form={form} name="prescriptionLimit" label="Prescriptions" />

            <SwitchField form={form} name="coversDependents" label="Covers dependants" />
            <FieldRow>
              <NumberField form={form} name="maxDependents" label="Maximum dependants" />
              <NumberField
                form={form}
                name="dependentAnnualLimit"
                label="Dependant annual limit"
              />
            </FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
