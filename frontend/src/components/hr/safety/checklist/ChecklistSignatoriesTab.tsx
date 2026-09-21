'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { TextField, NumberField, SelectField, SwitchField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';
import {
  SHE_CHECKLIST_SIGNATORY_KIND_OPTIONS,
  type SheInspectionChecklist,
  type SheInspectionChecklistSignatory,
} from '@/types/hr/safety-inspections';
import { moveWithin } from './builder-utils';

/**
 * Who signs the completed form, in print order. System users sign in-app as themselves (the
 * logged-in employee is recorded, never a typed name); external parties — vendor, cafeteria
 * operator — are a typed name and date.
 */
const schema = z.object({
  displayOrder: z.coerce.number().min(0).max(999),
  roleLabel: z.string().min(1, 'A role is required').max(150),
  kind: z.enum(['SystemUser', 'External']),
  isRequired: z.boolean(),
});
type Form = z.input<typeof schema>;

export function ChecklistSignatoriesTab({ checklist }: { checklist: SheInspectionChecklist }) {
  const id = checklist.id;
  const locked = checklist.isStructureLocked;
  const signatories = () => safetyChecklistService.getById(id).then((c) => c.signatories);
  const reorder = async (signatoryId: string, delta: -1 | 1) => {
    const ids = (await signatories()).map((s) => s.id);
    await safetyChecklistService.reorderSignatories(id, moveWithin(ids, signatoryId, delta));
  };

  return (
    <ResourceCollectionTab<SheInspectionChecklistSignatory, Form>
      parentId={id}
      title="signatories"
      singular="signatory"
      queryKey={['hr', 'safety-checklist', id, 'signatories']}
      invalidateKeys={[['hr', 'safety-checklist', id]]}
      list={signatories}
      readOnly={locked}
      create={(checklistId, values) => {
        const v = schema.parse(values);
        return safetyChecklistService.addSignatory(checklistId, {
          checklistId,
          displayOrder: v.displayOrder,
          roleLabel: v.roleLabel,
          kind: v.kind,
          isRequired: v.isRequired,
        });
      }}
      update={(_checklistId, signatoryId, values) => {
        const v = schema.parse(values);
        return safetyChecklistService.updateSignatory(signatoryId, {
          id: signatoryId,
          displayOrder: v.displayOrder,
          roleLabel: v.roleLabel,
          kind: v.kind,
          isRequired: v.isRequired,
        });
      }}
      remove={(_checklistId, signatoryId) => safetyChecklistService.removeSignatory(signatoryId)}
      actions={[
        { label: 'Move up', run: (s) => reorder(s.id, -1), visible: () => !locked },
        { label: 'Move down', run: (s) => reorder(s.id, 1), visible: () => !locked },
      ]}
      columns={[
        { header: '#', cell: (s) => <span className="tabular-nums">{s.displayOrder}</span> },
        { header: 'Role', cell: (s) => <span className="font-medium">{s.roleLabel}</span> },
        {
          header: 'Signs as',
          cell: (s) => (s.kind === 'SystemUser' ? 'System user (in-app)' : 'External party (typed name)'),
        },
        { header: 'Required', cell: (s) => (s.isRequired ? <Badge variant="secondary">Required</Badge> : '—') },
      ]}
      schema={schema}
      emptyForm={{ displayOrder: checklist.signatories.length + 1, roleLabel: '', kind: 'SystemUser', isRequired: true }}
      toForm={(s) => ({ displayOrder: s.displayOrder, roleLabel: s.roleLabel, kind: s.kind, isRequired: s.isRequired })}
      renderFields={(f) => (
        <>
          <FieldRow>
            <NumberField form={f} name="displayOrder" label="Order" required />
            <TextField form={f} name="roleLabel" label="Role" required placeholder="e.g. SHE Officer" />
          </FieldRow>
          <SelectField form={f} name="kind" label="Signs as" required options={SHE_CHECKLIST_SIGNATORY_KIND_OPTIONS} />
          <SwitchField form={f} name="isRequired" label="Required" description="Shown as outstanding on the inspection until signed." />
        </>
      )}
      getId={(s) => s.id}
      emptyDescription="No signatories yet — add who signs the completed form (vendor, SHE officer, site representative…)."
      dialogHint={locked ? 'This template is published; create a new version to change its signatories.' : undefined}
    />
  );
}
