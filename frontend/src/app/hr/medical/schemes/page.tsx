'use client';

import { z } from 'zod';
import Link from 'next/link';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalBenefitSchemeService } from '@/services/hr/medical-reference.service';
import type { MedicalBenefitSchemeSummary } from '@/types/hr/medical';

/**
 * The organisation's own medical benefit schemes — what it undertakes to cover, as distinct from
 * what an insurer covers. Entitlement is expressed as tiers per staff level; open a scheme to
 * manage them.
 */
const schemeSchema = z.object({
  name: z.string().min(1, 'Required').max(200),
  code: z.string().min(1, 'Required').max(50),
  description: z.string().max(1000).optional(),
  effectiveDate: z.string().min(1, 'Required'),
  expiryDate: z.string().optional(),
  isActive: z.boolean(),
  notes: z.string().max(1000).optional(),
});

type SchemeForm = z.input<typeof schemeSchema>;

const emptyScheme: SchemeForm = {
  name: '',
  code: '',
  description: '',
  effectiveDate: '',
  expiryDate: '',
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MedicalBenefitSchemesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Benefit Schemes"
        description="What the organisation itself covers medically, and at what level for each grade of staff. Open a scheme to manage its tiers."
        backHref="/hr/medical"
      />

      <ResourceListPanel<MedicalBenefitSchemeSummary, SchemeForm>
        title="schemes"
        singular="scheme"
        queryKey={['hr', 'medical-schemes']}
        dialogHint="A scheme on its own grants nothing — entitlement comes from the tiers inside it."
        emptyDescription="No benefit schemes have been set up yet. A scheme holds the tiers that define what each staff level is entitled to."
        list={() => medicalBenefitSchemeService.getSchemes()}
        create={(values) => {
          const v = schemeSchema.parse(values);
          return medicalBenefitSchemeService.createScheme({
            ...v,
            description: blank(v.description),
            expiryDate: blank(v.expiryDate),
            notes: blank(v.notes),
          });
        }}
        update={(id, values) => {
          const v = schemeSchema.parse(values);
          return medicalBenefitSchemeService.updateScheme(id, {
            id,
            ...v,
            description: blank(v.description),
            expiryDate: blank(v.expiryDate),
            notes: blank(v.notes),
          });
        }}
        remove={(id) => medicalBenefitSchemeService.removeScheme(id)}
        getId={(s) => s.id}
        columns={[
          {
            header: 'Scheme',
            cell: (s) => (
              <Link
                href={`/hr/medical/schemes/${s.id}`}
                className="font-medium text-primary hover:underline"
              >
                {s.name}
              </Link>
            ),
          },
          {
            header: 'Code',
            cell: (s) => <span className="font-mono text-sm text-muted-foreground">{s.code}</span>,
          },
          { header: 'Effective', cell: (s) => fmtDate(s.effectiveDate) },
          { header: 'Tiers', cell: (s) => s.tierCount, className: 'text-right' },
          { header: 'Status', cell: (s) => <StatusBadge active={s.isActive} /> },
        ]}
        schema={schemeSchema as any}
        emptyForm={emptyScheme}
        toForm={(s) => ({
          ...emptyScheme,
          name: s.name,
          code: s.code,
          effectiveDate: s.effectiveDate?.slice(0, 10) ?? '',
          isActive: s.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="name" label="Scheme name" required />
              <TextField form={form} name="code" label="Code" required />
            </FieldRow>
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <DateField form={form} name="effectiveDate" label="Effective from" required />
              <DateField form={form} name="expiryDate" label="Expires" />
            </FieldRow>
            <SwitchField form={form} name="isActive" label="Active" />
            <TextareaField form={form} name="notes" label="Notes" rows={2} />
          </>
        )}
      />
    </div>
  );
}
