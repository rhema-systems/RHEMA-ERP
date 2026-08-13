'use client';

import { z } from 'zod';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { TextField, TextareaField, SwitchField } from '@/components/hr/employee/tabs/fields';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import type { SheInjuryType } from '@/types/hr/safety';

/** How an injury is classified on an incident's involved-person record. Codes are immutable. */
const injuryTypeSchema = z.object({
  code: z.string().min(1, 'A code is required').max(20),
  name: z.string().min(1, 'A name is required').max(150),
  description: z.string().max(500).optional().or(z.literal('')),
  isActive: z.boolean(),
});

type InjuryTypeForm = z.input<typeof injuryTypeSchema>;

const emptyInjuryType: InjuryTypeForm = { code: '', name: '', description: '', isActive: true };

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function SafetyInjuryTypesPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Injury Types"
        description="The injury classification used on incident records."
        backHref="/administration/hr/safety"
      />

      <ResourceListPanel<SheInjuryType, InjuryTypeForm>
        title="injury types"
        singular="injury type"
        queryKey={['hr', 'safety-reference', 'injury-types']}
        dialogHint="The code is fixed once created."
        list={() => safetyReferenceService.getInjuryTypes()}
        create={(values) => {
          const v = injuryTypeSchema.parse(values);
          return safetyReferenceService.createInjuryType({ ...v, description: blank(v.description) });
        }}
        update={(id, values) => {
          const v = injuryTypeSchema.parse(values);
          return safetyReferenceService.updateInjuryType(id, {
            id,
            name: v.name,
            description: blank(v.description),
            isActive: v.isActive,
          });
        }}
        remove={(id) => safetyReferenceService.removeInjuryType(id)}
        getId={(t) => t.id}
        emptyDescription="No injury types yet."
        columns={[
          { header: 'Code', cell: (t) => <span className="font-mono">{t.code}</span> },
          { header: 'Name', cell: (t) => <span className="font-medium">{t.name}</span> },
          { header: 'Description', cell: (t) => t.description ?? '—' },
          {
            header: 'Status',
            cell: (t) => <StatusBadge status={t.isActive ? 'Active' : 'Inactive'} />,
          },
        ]}
        schema={injuryTypeSchema}
        emptyForm={emptyInjuryType}
        toForm={(t) => ({
          code: t.code,
          name: t.name,
          description: t.description ?? '',
          isActive: t.isActive,
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
            <TextField form={form} name="name" label="Name" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive types stay on existing incidents but are not offered for new ones."
            />
          </div>
        )}
      />
    </div>
  );
}
