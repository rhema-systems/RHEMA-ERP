'use client';

import { z } from 'zod';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { SHE_REGULATORY_DOMAIN_OPTIONS } from '@/types/hr/safety';
import type { SheRegulatoryBody } from '@/types/hr/safety';

/**
 * The authorities SHE answers to — EPA, GNFS, the Labour Department and the rest. Reportable
 * incident types point here, and the regulatory-compliance register (a later slice) hangs its
 * obligations off these records.
 */
const regulatoryBodySchema = z.object({
  name: z.string().min(1, 'A name is required').max(200),
  shortName: z.string().max(100).optional().or(z.literal('')),
  contactAddress: z.string().max(300).optional().or(z.literal('')),
  phone: z.string().max(50).optional().or(z.literal('')),
  email: z.string().max(100).email('Not a valid email').optional().or(z.literal('')),
  website: z.string().max(200).optional().or(z.literal('')),
  domain: z.enum([
    'OccupationalHealth',
    'OccupationalSafety',
    'EnvironmentalProtection',
    'FireSafety',
    'ChemicalControl',
    'Construction',
    'Labour',
    'PublicHealth',
  ]),
  isActive: z.boolean(),
});

type RegulatoryBodyForm = z.input<typeof regulatoryBodySchema>;

const emptyRegulatoryBody: RegulatoryBodyForm = {
  name: '',
  shortName: '',
  contactAddress: '',
  phone: '',
  email: '',
  website: '',
  domain: 'OccupationalSafety',
  isActive: true,
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function SafetyRegulatoryBodiesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Regulatory Bodies"
        description="The authorities reportable incidents and compliance obligations point at."
        backHref="/administration/hr/safety"
      />

      <ResourceListPanel<SheRegulatoryBody, RegulatoryBodyForm>
        title="regulatory bodies"
        singular="regulatory body"
        queryKey={['hr', 'safety-reference', 'regulatory-bodies']}
        list={() => safetyReferenceService.getRegulatoryBodies()}
        create={(values) => {
          const v = regulatoryBodySchema.parse(values);
          return safetyReferenceService.createRegulatoryBody({
            ...v,
            shortName: blank(v.shortName),
            contactAddress: blank(v.contactAddress),
            phone: blank(v.phone),
            email: blank(v.email),
            website: blank(v.website),
          });
        }}
        update={(id, values) => {
          const v = regulatoryBodySchema.parse(values);
          return safetyReferenceService.updateRegulatoryBody(id, {
            id,
            ...v,
            shortName: blank(v.shortName),
            contactAddress: blank(v.contactAddress),
            phone: blank(v.phone),
            email: blank(v.email),
            website: blank(v.website),
          });
        }}
        remove={(id) => safetyReferenceService.removeRegulatoryBody(id)}
        getId={(b) => b.id}
        emptyDescription="No regulatory bodies yet. Reportable incident types need somewhere to report to."
        columns={[
          {
            header: 'Name',
            cell: (b) => (
              <div>
                <span className="font-medium">{b.name}</span>
                {b.shortName && (
                  <span className="text-muted-foreground ml-2 text-xs">{b.shortName}</span>
                )}
              </div>
            ),
          },
          {
            header: 'Domain',
            cell: (b) =>
              SHE_REGULATORY_DOMAIN_OPTIONS.find((o) => o.value === b.domain)?.label ??
              b.domainName,
          },
          { header: 'Phone', cell: (b) => b.phone ?? '—' },
          { header: 'Email', cell: (b) => b.email ?? '—' },
          {
            header: 'Status',
            cell: (b) => <StatusBadge status={b.isActive ? 'Active' : 'Inactive'} />,
          },
        ]}
        schema={regulatoryBodySchema}
        emptyForm={emptyRegulatoryBody}
        toForm={(b) => ({
          name: b.name,
          shortName: b.shortName ?? '',
          contactAddress: b.contactAddress ?? '',
          phone: b.phone ?? '',
          email: b.email ?? '',
          website: b.website ?? '',
          domain: b.domain,
          isActive: b.isActive,
        })}
        renderFields={(form) => (
          <div className="space-y-4">
            <FieldRow>
              <TextField form={form} name="name" label="Name" required />
              <TextField form={form} name="shortName" label="Short name" placeholder="e.g. EPA" />
            </FieldRow>
            <SelectField
              form={form}
              name="domain"
              label="Domain"
              required
              options={SHE_REGULATORY_DOMAIN_OPTIONS}
            />
            <TextareaField form={form} name="contactAddress" label="Contact address" rows={2} />
            <FieldRow>
              <TextField form={form} name="phone" label="Phone" type="tel" />
              <TextField form={form} name="email" label="Email" type="email" />
            </FieldRow>
            <TextField form={form} name="website" label="Website" />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive bodies stay on existing records but are not offered for new ones."
            />
          </div>
        )}
      />
    </div>
  );
}
