'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { TextField, NumberField, SelectField, SwitchField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';
import {
  SHE_CHECKLIST_FIELD_TYPE_OPTIONS,
  type SheInspectionChecklist,
  type SheInspectionChecklistField,
} from '@/types/hr/safety-inspections';
import { blank, moveWithin } from './builder-utils';

/**
 * The header fields of the form ("Vendor name", "Weather", "Organization unit"). Choice fields carry
 * their options '|'-separated; reference kinds (Employee / Location / Organization unit) resolve to
 * a picker on the run screen. "Department" on paper is an Organization unit field here.
 */
const schema = z.object({
  displayOrder: z.coerce.number().min(0).max(999),
  label: z.string().min(1, 'A label is required').max(150),
  fieldType: z.enum(['Text', 'LongText', 'Number', 'Date', 'Time', 'YesNo', 'Choice', 'Employee', 'Location', 'OrganizationUnit']),
  isRequired: z.boolean(),
  choiceOptions: z.string().max(1000).optional().or(z.literal('')),
  helpText: z.string().max(300).optional().or(z.literal('')),
});
type Form = z.input<typeof schema>;

export function ChecklistFieldsTab({ checklist }: { checklist: SheInspectionChecklist }) {
  const id = checklist.id;
  const locked = checklist.isStructureLocked;
  const fields = () => safetyChecklistService.getById(id).then((c) => c.fields);
  const reorder = async (fieldId: string, delta: -1 | 1) => {
    const ids = (await fields()).map((f) => f.id);
    await safetyChecklistService.reorderFields(id, moveWithin(ids, fieldId, delta));
  };

  return (
    <ResourceCollectionTab<SheInspectionChecklistField, Form>
      parentId={id}
      title="header fields"
      singular="field"
      queryKey={['hr', 'safety-checklist', id, 'fields']}
      invalidateKeys={[['hr', 'safety-checklist', id]]}
      list={fields}
      readOnly={locked}
      create={(checklistId, values) => {
        const v = schema.parse(values);
        return safetyChecklistService.addField(checklistId, {
          checklistId,
          displayOrder: v.displayOrder,
          label: v.label,
          fieldType: v.fieldType,
          isRequired: v.isRequired,
          choiceOptions: blank(v.choiceOptions),
          helpText: blank(v.helpText),
        });
      }}
      update={(_checklistId, fieldId, values) => {
        const v = schema.parse(values);
        return safetyChecklistService.updateField(fieldId, {
          id: fieldId,
          displayOrder: v.displayOrder,
          label: v.label,
          fieldType: v.fieldType,
          isRequired: v.isRequired,
          choiceOptions: blank(v.choiceOptions),
          helpText: blank(v.helpText),
        });
      }}
      remove={(_checklistId, fieldId) => safetyChecklistService.removeField(fieldId)}
      actions={[
        { label: 'Move up', run: (f) => reorder(f.id, -1), visible: () => !locked },
        { label: 'Move down', run: (f) => reorder(f.id, 1), visible: () => !locked },
      ]}
      columns={[
        { header: '#', cell: (f) => <span className="tabular-nums">{f.displayOrder}</span> },
        { header: 'Label', cell: (f) => <span className="font-medium">{f.label}</span> },
        { header: 'Type', cell: (f) => SHE_CHECKLIST_FIELD_TYPE_OPTIONS.find((o) => o.value === f.fieldType)?.label ?? f.fieldTypeName },
        { header: 'Required', cell: (f) => (f.isRequired ? <Badge variant="secondary">Required</Badge> : '—') },
        {
          header: 'Options / help',
          cell: (f) => (f.fieldType === 'Choice' ? f.choices.join(' · ') : f.helpText ?? '—'),
        },
      ]}
      schema={schema}
      emptyForm={{ displayOrder: checklist.fields.length + 1, label: '', fieldType: 'Text', isRequired: false, choiceOptions: '', helpText: '' }}
      toForm={(f) => ({
        displayOrder: f.displayOrder,
        label: f.label,
        fieldType: f.fieldType,
        isRequired: f.isRequired,
        choiceOptions: f.choiceOptions ?? '',
        helpText: f.helpText ?? '',
      })}
      renderFields={(f) => (
        <>
          <FieldRow>
            <NumberField form={f} name="displayOrder" label="Order" required />
            <TextField form={f} name="label" label="Label" required placeholder="e.g. Vendor name" />
          </FieldRow>
          <SelectField form={f} name="fieldType" label="Type" required options={SHE_CHECKLIST_FIELD_TYPE_OPTIONS} />
          {f.watch('fieldType') === 'Choice' && (
            <TextField
              form={f}
              name="choiceOptions"
              label="Options (separate with |)"
              placeholder="Initial|Routine|Follow-up|Complaint Investigation"
            />
          )}
          <TextField form={f} name="helpText" label="Help text" />
          <SwitchField form={f} name="isRequired" label="Required" description="The inspection cannot be completed while a required field is empty." />
        </>
      )}
      getId={(f) => f.id}
      emptyDescription="No header fields yet — add what the form asks for above the checklist (site, vendor, weather…)."
      dialogHint={locked ? 'This template is published; create a new version to change its fields.' : undefined}
    />
  );
}
