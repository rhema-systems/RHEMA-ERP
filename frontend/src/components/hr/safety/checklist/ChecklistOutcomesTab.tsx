'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { TextField, NumberField, TextareaField, SwitchField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';
import type { SheInspectionChecklist, SheInspectionChecklistOutcome } from '@/types/hr/safety-inspections';
import { bandLabel } from './checklist-scoring';
import { blank, decimalString, intString, moveWithin, numOrNull, numToStr } from './builder-utils';

/**
 * What a run can end in. Percentage mode: bands recommend an outcome (the band whose lower bound is
 * the greatest one at or below the score — so only "From %" really matters; "To %" is what prints).
 * Qualitative mode: the rating scale, no bands. One outcome may be the disqualifying one, forced by
 * any critical non-conformity.
 */
const schema = z.object({
  displayOrder: z.coerce.number().min(0).max(999),
  label: z.string().min(1, 'A label is required').max(150),
  description: z.string().max(500).optional().or(z.literal('')),
  minPercent: decimalString(100),
  maxPercent: decimalString(100),
  reinspectionWithinDays: intString(3650),
  isDisqualifying: z.boolean(),
});
type Form = z.input<typeof schema>;

export function ChecklistOutcomesTab({ checklist }: { checklist: SheInspectionChecklist }) {
  const id = checklist.id;
  const locked = checklist.isStructureLocked;
  const percentage = checklist.scoringMode === 'CompliancePercentage';
  const outcomes = () => safetyChecklistService.getById(id).then((c) => c.outcomes);
  const reorder = async (outcomeId: string, delta: -1 | 1) => {
    const ids = (await outcomes()).map((o) => o.id);
    await safetyChecklistService.reorderOutcomes(id, moveWithin(ids, outcomeId, delta));
  };

  const hint =
    checklist.scoringMode === 'None'
      ? 'This template has no scoring — outcomes are not used. Change the scoring mode on the Form tab to use them.'
      : percentage
        ? 'Give each decision its score band. One band must start at 0%, and bands must not overlap.'
        : 'These are the ratings the inspector chooses from. No bands.';

  return (
    <div className="space-y-3">
      <p className="text-muted-foreground text-sm">{hint}</p>
      <ResourceCollectionTab<SheInspectionChecklistOutcome, Form>
        parentId={id}
        title="outcomes"
        singular="outcome"
        queryKey={['hr', 'safety-checklist', id, 'outcomes']}
        invalidateKeys={[['hr', 'safety-checklist', id]]}
        list={outcomes}
        readOnly={locked}
        create={(checklistId, values) => {
          const v = schema.parse(values);
          return safetyChecklistService.addOutcome(checklistId, {
            checklistId,
            displayOrder: v.displayOrder,
            label: v.label,
            description: blank(v.description),
            minPercent: numOrNull(v.minPercent),
            maxPercent: numOrNull(v.maxPercent),
            reinspectionWithinDays: numOrNull(v.reinspectionWithinDays),
            isDisqualifying: v.isDisqualifying,
          });
        }}
        update={(_checklistId, outcomeId, values) => {
          const v = schema.parse(values);
          return safetyChecklistService.updateOutcome(outcomeId, {
            id: outcomeId,
            displayOrder: v.displayOrder,
            label: v.label,
            description: blank(v.description),
            minPercent: numOrNull(v.minPercent),
            maxPercent: numOrNull(v.maxPercent),
            reinspectionWithinDays: numOrNull(v.reinspectionWithinDays),
            isDisqualifying: v.isDisqualifying,
          });
        }}
        remove={(_checklistId, outcomeId) => safetyChecklistService.removeOutcome(outcomeId)}
        actions={[
          { label: 'Move up', run: (o) => reorder(o.id, -1), visible: () => !locked },
          { label: 'Move down', run: (o) => reorder(o.id, 1), visible: () => !locked },
        ]}
        columns={[
          { header: '#', cell: (o) => <span className="tabular-nums">{o.displayOrder}</span> },
          {
            header: 'Outcome',
            cell: (o) => (
              <div>
                <div className="font-medium">{o.label}</div>
                {o.description && o.description !== o.label ? (
                  <div className="text-muted-foreground text-xs">{o.description}</div>
                ) : null}
              </div>
            ),
          },
          { header: 'Score band', cell: (o) => <span className="tabular-nums">{bandLabel(o)}</span> },
          {
            header: 'Re-inspect within',
            cell: (o) => (o.reinspectionWithinDays ? `${o.reinspectionWithinDays} days` : '—'),
          },
          {
            header: 'Disqualifying',
            cell: (o) => (o.isDisqualifying ? <Badge variant="destructive">Disqualifying</Badge> : '—'),
          },
        ]}
        schema={schema}
        emptyForm={{
          displayOrder: checklist.outcomes.length + 1,
          label: '',
          description: '',
          minPercent: '',
          maxPercent: '',
          reinspectionWithinDays: '',
          isDisqualifying: false,
        }}
        toForm={(o) => ({
          displayOrder: o.displayOrder,
          label: o.label,
          description: o.description ?? '',
          minPercent: numToStr(o.minPercent),
          maxPercent: numToStr(o.maxPercent),
          reinspectionWithinDays: numToStr(o.reinspectionWithinDays),
          isDisqualifying: o.isDisqualifying,
        })}
        renderFields={(f) => (
          <>
            <FieldRow>
              <NumberField form={f} name="displayOrder" label="Order" required />
              <TextField form={f} name="label" label="Outcome" required placeholder="e.g. Approved with Corrective Actions" />
            </FieldRow>
            <TextareaField form={f} name="description" label="Decision text (as printed)" rows={2} />
            {percentage && (
              <FieldRow>
                <TextField form={f} name="minPercent" label="From %" placeholder="e.g. 85" />
                <TextField form={f} name="maxPercent" label="To %" placeholder="e.g. 94.99" />
              </FieldRow>
            )}
            <TextField form={f} name="reinspectionWithinDays" label="Re-inspection required within (days)" placeholder="e.g. 7" />
            <SwitchField
              form={f}
              name="isDisqualifying"
              label="Disqualifying outcome"
              description="Forced whenever a critical non-conformity is recorded. Only one outcome may be marked."
            />
          </>
        )}
        getId={(o) => o.id}
        emptyDescription="No outcomes yet — add the decisions or ratings the inspection can end in."
        dialogHint={locked ? 'This template is published; create a new version to change its outcomes.' : undefined}
      />
    </div>
  );
}
