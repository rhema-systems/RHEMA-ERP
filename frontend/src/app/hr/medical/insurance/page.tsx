'use client';

import { z } from 'zod';
import Link from 'next/link';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { medicalInsuranceService } from '@/services/hr/medical-reference.service';
import { MEDICAL_PROVIDER_TYPE_OPTIONS } from '@/types/hr/medical';
import type { MedicalInsuranceProviderSummary } from '@/types/hr/medical';

/**
 * Insurers the organisation buys medical cover from. Their plans hang off the provider — open a
 * provider to manage them.
 *
 * The two day counts are the ones that bite operationally: standard processing days is what an
 * employee is told to expect, and the submission deadline is how long after treatment a claim can
 * still be sent.
 */
const providerSchema = z.object({
  name: z.string().min(1, 'Required').max(300),
  shortName: z.string().max(100).optional(),
  code: z.string().min(1, 'Required').max(50),
  providerType: z.enum([
    'HealthInsurance',
    'LifeInsurance',
    'HMO',
    'PPO',
    'NationalHealthInsurance',
    'PrivateHealthInsurance',
    'GroupInsurance',
    'TravelInsurance',
    'DentalInsurance',
    'VisionInsurance',
  ]),
  licenseNumber: z.string().min(1, 'Required').max(100),
  address: z.string().min(1, 'Required').max(500),
  city: z.string().max(100).optional(),
  primaryPhone: z.string().min(1, 'Required').max(50),
  claimsHotline: z.string().max(50).optional(),
  email: z.string().email('Not a valid email').max(255),
  claimsEmail: z.string().email('Not a valid email').max(255).optional().or(z.literal('')),
  website: z.string().max(255).optional(),
  standardProcessingDays: z.coerce.number().min(1, 'At least 1 day'),
  claimSubmissionDeadlineDays: z.coerce.number().min(1, 'At least 1 day'),
  isActive: z.boolean(),
  notes: z.string().max(2000).optional(),
});

type ProviderForm = z.input<typeof providerSchema>;

const emptyProvider: ProviderForm = {
  name: '',
  shortName: '',
  code: '',
  providerType: 'HealthInsurance',
  licenseNumber: '',
  address: '',
  city: '',
  primaryPhone: '',
  claimsHotline: '',
  email: '',
  claimsEmail: '',
  website: '',
  standardProcessingDays: 14,
  claimSubmissionDeadlineDays: 30,
  isActive: true,
  notes: '',
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

const typeLabel = (v: string) =>
  MEDICAL_PROVIDER_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

export default function MedicalInsuranceProvidersPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Insurance Providers"
        description="Insurers and schemes the organisation holds medical cover with. Open a provider to manage the plans it sells."
        backHref="/hr/medical"
      />

      <ResourceListPanel<MedicalInsuranceProviderSummary, ProviderForm>
        title="providers"
        singular="provider"
        queryKey={['hr', 'medical-providers']}
        dialogHint="Processing days and the submission deadline come from the policy document — they are what staff get told when they ask."
        emptyDescription="No insurers have been registered yet. Add one before recording employee policies."
        list={() => medicalInsuranceService.getProviders()}
        create={(values) => {
          const v = providerSchema.parse(values);
          return medicalInsuranceService.createProvider({
            ...v,
            shortName: blank(v.shortName),
            city: blank(v.city),
            claimsHotline: blank(v.claimsHotline),
            claimsEmail: blank(v.claimsEmail),
            website: blank(v.website),
            notes: blank(v.notes),
          });
        }}
        update={(id, values) => {
          const v = providerSchema.parse(values);
          return medicalInsuranceService.updateProvider(id, {
            id,
            ...v,
            shortName: blank(v.shortName),
            city: blank(v.city),
            claimsHotline: blank(v.claimsHotline),
            claimsEmail: blank(v.claimsEmail),
            website: blank(v.website),
            notes: blank(v.notes),
          });
        }}
        remove={(id) => medicalInsuranceService.removeProvider(id)}
        getId={(p) => p.id}
        columns={[
          {
            header: 'Provider',
            cell: (p) => (
              <Link
                href={`/hr/medical/insurance/${p.id}`}
                className="font-medium text-primary hover:underline"
              >
                {p.name}
              </Link>
            ),
          },
          {
            header: 'Code',
            cell: (p) => <span className="font-mono text-sm text-muted-foreground">{p.code}</span>,
          },
          { header: 'Type', cell: (p) => typeLabel(p.providerType) },
          { header: 'Phone', cell: (p) => p.primaryPhone || '—' },
          { header: 'Plans', cell: (p) => p.planCount, className: 'text-right' },
          { header: 'Status', cell: (p) => <StatusBadge active={p.isActive} /> },
        ]}
        schema={providerSchema as any}
        emptyForm={emptyProvider}
        toForm={(p) => ({
          ...emptyProvider,
          name: p.name,
          code: p.code,
          providerType: p.providerType,
          primaryPhone: p.primaryPhone,
          isActive: p.isActive,
          // licenseNumber, address and email are required by the API but absent from the summary
          // row; the dialog re-collects them rather than sending blanks.
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="name" label="Provider name" required />
              <TextField form={form} name="code" label="Code" required />
            </FieldRow>
            <FieldRow>
              <SelectField
                form={form}
                name="providerType"
                label="Type"
                required
                options={MEDICAL_PROVIDER_TYPE_OPTIONS}
              />
              <TextField form={form} name="shortName" label="Short name" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="licenseNumber" label="Licence number" required />
              <TextField form={form} name="city" label="City" />
            </FieldRow>
            <TextField form={form} name="address" label="Address" required />
            <FieldRow>
              <TextField form={form} name="primaryPhone" label="Primary phone" required />
              <TextField form={form} name="claimsHotline" label="Claims hotline" />
            </FieldRow>
            <FieldRow>
              <TextField form={form} name="email" label="Email" required />
              <TextField form={form} name="claimsEmail" label="Claims email" />
            </FieldRow>
            <TextField form={form} name="website" label="Website" />
            <FieldRow>
              <NumberField
                form={form}
                name="standardProcessingDays"
                label="Standard processing (days)"
                required
              />
              <NumberField
                form={form}
                name="claimSubmissionDeadlineDays"
                label="Submission deadline (days)"
                required
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
