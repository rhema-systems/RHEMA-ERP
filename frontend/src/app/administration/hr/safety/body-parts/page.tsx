'use client';

import { z } from 'zod';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { TextField, SwitchField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import type { SheBodyPart } from '@/types/hr/safety';

/** The injured-body-part catalogue. Region is free text ("Upper limb", "Head & neck"). */
const bodyPartSchema = z.object({
  code: z.string().min(1, 'A code is required').max(20),
  name: z.string().min(1, 'A name is required').max(100),
  region: z.string().max(100).optional().or(z.literal('')),
  isActive: z.boolean(),
});

type BodyPartForm = z.input<typeof bodyPartSchema>;

const emptyBodyPart: BodyPartForm = { code: '', name: '', region: '', isActive: true };

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function SafetyBodyPartsPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Body Parts"
        description="What an injury record can point at, grouped by body region."
        backHref="/administration/hr/safety"
      />

      <ResourceListPanel<SheBodyPart, BodyPartForm>
        title="body parts"
        singular="body part"
        queryKey={['hr', 'safety-reference', 'body-parts']}
        dialogHint="The code is fixed once created."
        list={() => safetyReferenceService.getBodyParts()}
        create={(values) => {
          const v = bodyPartSchema.parse(values);
          return safetyReferenceService.createBodyPart({ ...v, region: blank(v.region) });
        }}
        update={(id, values) => {
          const v = bodyPartSchema.parse(values);
          return safetyReferenceService.updateBodyPart(id, {
            id,
            name: v.name,
            region: blank(v.region),
            isActive: v.isActive,
          });
        }}
        remove={(id) => safetyReferenceService.removeBodyPart(id)}
        getId={(p) => p.id}
        emptyDescription="No body parts yet."
        columns={[
          { header: 'Code', cell: (p) => <span className="font-mono">{p.code}</span> },
          { header: 'Name', cell: (p) => <span className="font-medium">{p.name}</span> },
          { header: 'Region', cell: (p) => p.region ?? '—' },
          {
            header: 'Status',
            cell: (p) => <StatusBadge status={p.isActive ? 'Active' : 'Inactive'} />,
          },
        ]}
        schema={bodyPartSchema}
        emptyForm={emptyBodyPart}
        toForm={(p) => ({
          code: p.code,
          name: p.name,
          region: p.region ?? '',
          isActive: p.isActive,
        })}
        renderFields={(form, editing) => (
          <div className="space-y-4">
            {editing ? (
              <p className="text-muted-foreground text-sm">
                Code <span className="font-mono">{form.getValues('code')}</span> — fixed at creation.
              </p>
            ) : (
              <TextField form={form} name="code" label="Code" required />
            )}
            <FieldRow>
              <TextField form={form} name="name" label="Name" required />
              <TextField form={form} name="region" label="Region" placeholder="e.g. Upper limb" />
            </FieldRow>
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive parts stay on existing records but are not offered for new ones."
            />
          </div>
        )}
      />
    </div>
  );
}
