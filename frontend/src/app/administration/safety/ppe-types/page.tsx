'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
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
import { safetyPpeService } from '@/services/hr/safety-ppe.service';
import { SHE_PPE_CATEGORY_OPTIONS } from '@/types/hr/safety-ppe';
import type { PpeType, ShePpeCategory } from '@/types/hr/safety-ppe';

/**
 * The PPE type catalogue (FRD §8) — what kinds of protective equipment exist, their conformance
 * standard and lifespan, and whether items carry serial numbers or expiry dates. Inventory,
 * issuance and the job-role matrix all hang off a type. Codes are immutable after creation.
 */
const ppeTypeSchema = z.object({
  code: z.string().min(1, 'A code is required').max(20),
  name: z.string().min(1, 'A name is required').max(150),
  description: z.string().max(500).optional().or(z.literal('')),
  category: z.enum([
    'HeadProtection',
    'EyeFaceProtection',
    'HearingProtection',
    'RespiratoryProtection',
    'HandProtection',
    'FootProtection',
    'BodyProtection',
    'FallProtection',
    'HighVisibility',
    'Combination',
  ]),
  standard: z.string().max(100).optional().or(z.literal('')),
  lifespanMonths: z.coerce.number().min(1).max(600).optional(),
  requiresSerialNumber: z.boolean(),
  hasExpiryDate: z.boolean(),
  isActive: z.boolean(),
});

type PpeTypeForm = z.input<typeof ppeTypeSchema>;

const emptyPpeType: PpeTypeForm = {
  code: '',
  name: '',
  description: '',
  category: 'HeadProtection',
  standard: '',
  lifespanMonths: undefined,
  requiresSerialNumber: false,
  hasExpiryDate: false,
  isActive: true,
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function SafetyPpeTypesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="PPE Types"
        description="The catalogue behind PPE stock, issuance and the job-role matrix — categories, conformance standards, lifespans and tracking flags."
        backHref="/administration/safety"
      />

      <ResourceListPanel<PpeType, PpeTypeForm>
        title="PPE types"
        singular="PPE type"
        queryKey={['hr', 'safety-ppe', 'types']}
        dialogHint="The code is fixed once created."
        list={() => safetyPpeService.getTypes()}
        create={(values) => {
          const v = ppeTypeSchema.parse(values);
          return safetyPpeService.createType({
            ...v,
            category: v.category as ShePpeCategory,
            description: blank(v.description),
            standard: blank(v.standard),
            lifespanMonths: v.lifespanMonths ?? null,
          });
        }}
        update={(id, values) => {
          const v = ppeTypeSchema.parse(values);
          return safetyPpeService.updateType(id, {
            id,
            name: v.name,
            description: blank(v.description),
            category: v.category as ShePpeCategory,
            standard: blank(v.standard),
            lifespanMonths: v.lifespanMonths ?? null,
            requiresSerialNumber: v.requiresSerialNumber,
            hasExpiryDate: v.hasExpiryDate,
            isActive: v.isActive,
          });
        }}
        remove={(id) => safetyPpeService.removeType(id)}
        getId={(t) => t.id}
        emptyDescription="No PPE types yet. Stock and issuance cannot be recorded without them."
        columns={[
          { header: 'Code', cell: (t) => <span className="font-mono">{t.code}</span> },
          { header: 'Name', cell: (t) => <span className="font-medium">{t.name}</span> },
          {
            header: 'Category',
            cell: (t) =>
              SHE_PPE_CATEGORY_OPTIONS.find((o) => o.value === t.category)?.label ??
              t.categoryName,
          },
          {
            header: 'Standard',
            cell: (t) =>
              t.standard ? (
                <span className="font-mono text-sm">{t.standard}</span>
              ) : (
                <span className="text-muted-foreground">—</span>
              ),
          },
          {
            header: 'Lifespan',
            cell: (t) =>
              t.lifespanMonths ? (
                `${t.lifespanMonths} mo`
              ) : (
                <span className="text-muted-foreground">—</span>
              ),
          },
          {
            header: 'Tracking',
            cell: (t) => (
              <span className="flex gap-1">
                {t.requiresSerialNumber && <Badge variant="outline">Serial №</Badge>}
                {t.hasExpiryDate && <Badge variant="outline">Expires</Badge>}
                {!t.requiresSerialNumber && !t.hasExpiryDate && (
                  <span className="text-muted-foreground">—</span>
                )}
              </span>
            ),
          },
          {
            header: 'Status',
            cell: (t) => <StatusBadge status={t.isActive ? 'Active' : 'Inactive'} />,
          },
        ]}
        schema={ppeTypeSchema}
        emptyForm={emptyPpeType}
        toForm={(t) => ({
          code: t.code,
          name: t.name,
          description: t.description ?? '',
          category: t.category,
          standard: t.standard ?? '',
          lifespanMonths: t.lifespanMonths ?? undefined,
          requiresSerialNumber: t.requiresSerialNumber,
          hasExpiryDate: t.hasExpiryDate,
          isActive: t.isActive,
        })}
        renderFields={(form, editing) => (
          <div className="space-y-4">
            {editing ? (
              <div className="space-y-4">
                <p className="text-muted-foreground text-sm">
                  Code <span className="font-mono">{form.getValues('code')}</span> — fixed at
                  creation.
                </p>
                <TextField form={form} name="name" label="Name" required />
              </div>
            ) : (
              <FieldRow>
                <TextField form={form} name="code" label="Code" required />
                <TextField form={form} name="name" label="Name" required />
              </FieldRow>
            )}
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={SHE_PPE_CATEGORY_OPTIONS}
              />
              <TextField form={form} name="standard" label="Standard (e.g. EN 397)" />
            </FieldRow>
            <NumberField form={form} name="lifespanMonths" label="Lifespan (months)" />
            <SwitchField
              form={form}
              name="requiresSerialNumber"
              label="Requires a serial number"
              description="Issuances of this type carry the individual item's serial number."
            />
            <SwitchField
              form={form}
              name="hasExpiryDate"
              label="Has an expiry date"
              description="Issuances of this type carry an expiry date. No expiry alert fires yet — expiry is visible on the issuance register only."
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive types stay on existing records but are not offered for new stock or issuance."
            />
          </div>
        )}
      />
    </div>
  );
}
