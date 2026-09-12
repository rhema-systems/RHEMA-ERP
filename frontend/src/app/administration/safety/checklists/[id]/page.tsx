'use client';

import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import { RiskBadge } from '@/components/hr/safety/RiskBadge';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';
import { SHE_RISK_LEVEL_OPTIONS } from '@/types/hr/safety-hazards';
import type { SheInspectionChecklistItem } from '@/types/hr/safety-inspections';

const blank = (v?: string) => (v && v.length > 0 ? v : null);

const itemSchema = z.object({
  itemOrder: z.coerce.number().min(0).max(999),
  category: z.string().min(1, 'A category is required').max(100),
  itemDescription: z.string().min(1, 'Describe what is checked').max(500),
  isMandatory: z.boolean(),
  regulatoryReference: z.string().max(200).optional().or(z.literal('')),
  associatedRiskLevel: z
    .enum(['Negligible', 'Low', 'Medium', 'High', 'Critical'])
    .optional()
    .or(z.literal('')),
});
type ItemForm = z.input<typeof itemSchema>;
const emptyItem: ItemForm = {
  itemOrder: 1,
  category: '',
  itemDescription: '',
  isMandatory: false,
  regulatoryReference: '',
  associatedRiskLevel: '',
};

/** One checklist template and its ordered items. */
export default function ChecklistDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;

  const { data: checklist, isLoading } = useQuery({
    queryKey: ['hr', 'safety-checklist', id],
    queryFn: () => safetyChecklistService.getById(id),
    enabled: !!id,
  });

  if (isLoading || !checklist) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${checklist.checklistNumber} — ${checklist.name}`}
        description={checklist.description ?? undefined}
        backHref="/administration/safety/checklists"
        actions={
          <div className="flex items-center gap-2">
            <Badge variant="outline">{checklist.typeName}</Badge>
            <Badge variant="outline" className="tabular-nums">
              v{checklist.version}
            </Badge>
            <StatusBadge status={checklist.isActive ? 'Active' : 'Inactive'} />
          </div>
        }
      />

      <ResourceCollectionTab<SheInspectionChecklistItem, ItemForm>
        parentId={id}
        title="items"
        singular="item"
        queryKey={['hr', 'safety-checklist', id, 'items']}
        invalidateKeys={[['hr', 'safety-checklist', id]]}
        list={async () => (await safetyChecklistService.getById(id)).items}
        create={(checklistId, values) => {
          const v = itemSchema.parse(values);
          return safetyChecklistService.addItem(checklistId, {
            checklistId,
            itemOrder: v.itemOrder,
            category: v.category,
            itemDescription: v.itemDescription,
            isMandatory: v.isMandatory,
            regulatoryReference: blank(v.regulatoryReference),
            associatedRiskLevel: v.associatedRiskLevel === '' ? null : v.associatedRiskLevel,
          });
        }}
        update={(_checklistId, itemId, values) => {
          const v = itemSchema.parse(values);
          return safetyChecklistService.updateItem(itemId, {
            id: itemId,
            itemOrder: v.itemOrder,
            category: v.category,
            itemDescription: v.itemDescription,
            isMandatory: v.isMandatory,
            regulatoryReference: blank(v.regulatoryReference),
            associatedRiskLevel: v.associatedRiskLevel === '' ? null : v.associatedRiskLevel,
          });
        }}
        remove={(_checklistId, itemId) => safetyChecklistService.removeItem(itemId)}
        columns={[
          { header: '#', cell: (i) => <span className="tabular-nums">{i.itemOrder}</span> },
          { header: 'Category', cell: (i) => i.category },
          { header: 'Check', cell: (i) => i.itemDescription },
          {
            header: 'Mandatory',
            cell: (i) => (i.isMandatory ? <Badge variant="secondary">Mandatory</Badge> : '—'),
          },
          { header: 'Reg. reference', cell: (i) => i.regulatoryReference ?? '—' },
          {
            header: 'Risk',
            cell: (i) =>
              i.associatedRiskLevel ? (
                <RiskBadge level={i.associatedRiskLevel} label={i.associatedRiskLevelName ?? undefined} />
              ) : (
                '—'
              ),
          },
        ]}
        schema={itemSchema}
        emptyForm={emptyItem}
        toForm={(i) => ({
          itemOrder: i.itemOrder,
          category: i.category,
          itemDescription: i.itemDescription,
          isMandatory: i.isMandatory,
          regulatoryReference: i.regulatoryReference ?? '',
          associatedRiskLevel: i.associatedRiskLevel ?? '',
        })}
        renderFields={(f) => (
          <>
            <FieldRow>
              <NumberField form={f} name="itemOrder" label="Order" required />
              <TextField form={f} name="category" label="Category" required placeholder="e.g. Housekeeping" />
            </FieldRow>
            <TextareaField form={f} name="itemDescription" label="What is checked" rows={2} />
            <FieldRow>
              <TextField form={f} name="regulatoryReference" label="Regulatory reference" />
              <SelectField
                form={f}
                name="associatedRiskLevel"
                label="Associated risk"
                allowEmpty
                options={SHE_RISK_LEVEL_OPTIONS}
              />
            </FieldRow>
            <SwitchField
              form={f}
              name="isMandatory"
              label="Mandatory"
              description="Mandatory items must be assessed on every inspection using this checklist."
            />
          </>
        )}
        getId={(i) => i.id}
        emptyDescription="Add the checks this template walks through, in order."
      />
    </div>
  );
}
