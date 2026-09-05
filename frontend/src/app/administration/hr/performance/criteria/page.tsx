'use client';

import { z } from 'zod';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { TextField, TextareaField, SwitchField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { appraisalCriteriaService } from '@/services/hr/appraisal.service';
import type { AppraisalCriterion } from '@/types/hr/appraisal';

/**
 * Appraisal criteria — the qualitative half of an appraisal form.
 *
 * A template item scores either a criterion ("Judgement", "Teamwork") or a KPI. The split is
 * deliberate: a KPI carries a measurement type, a unit and a target, while a criterion is
 * judged against the grade bands the template item defines and nothing else.
 *
 * Deactivating keeps a criterion out of new template items without disturbing the templates
 * already scoring against it.
 */
const criterionSchema = z.object({
  code: z.string().max(50).optional(),
  criteriaName: z.string().min(1, 'Required').max(200),
  description: z.string().max(1000).optional(),
  isActive: z.boolean(),
});

type CriterionForm = z.input<typeof criterionSchema>;

const emptyCriterion: CriterionForm = {
  code: '',
  criteriaName: '',
  description: '',
  isActive: true,
};

const toPayload = (values: CriterionForm) => {
  const v = criterionSchema.parse(values);
  return {
    code: v.code || null,
    criteriaName: v.criteriaName,
    description: v.description || null,
    isActive: v.isActive,
  };
};

export default function AppraisalCriteriaPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Appraisal Criteria"
        description="The named behaviours and competencies an appraisal form can score, alongside its KPIs."
        backHref="/administration/hr/performance"
      />

      <ResourceListPanel<AppraisalCriterion, CriterionForm>
        title="criteria"
        singular="criterion"
        queryKey={['hr', 'appraisal-criteria']}
        dialogHint="Describe what the criterion means so two managers grade it the same way."
        emptyDescription="Add the competencies your appraisal forms score."
        list={() => appraisalCriteriaService.getAll()}
        create={(values) => appraisalCriteriaService.create(toPayload(values))}
        update={(id, values) => appraisalCriteriaService.update(id, { id, ...toPayload(values) })}
        remove={(id) => appraisalCriteriaService.remove(id)}
        getId={(r) => r.id}
        columns={[
          { header: 'Code', cell: (r) => r.code || '—' },
          { header: 'Criterion', cell: (r) => <span className="font-medium">{r.criteriaName}</span> },
          {
            header: 'Description',
            cell: (r) => (
              <span className="line-clamp-1 text-muted-foreground">{r.description || '—'}</span>
            ),
          },
          { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
        ]}
        schema={criterionSchema as any}
        emptyForm={emptyCriterion}
        toForm={(r) => ({
          code: r.code ?? '',
          criteriaName: r.criteriaName,
          description: r.description ?? '',
          isActive: r.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="code" label="Code" placeholder="e.g. COMP-TEAM" />
              <TextField
                form={form}
                name="criteriaName"
                label="Criterion"
                required
                placeholder="e.g. Teamwork"
              />
            </FieldRow>
            <TextareaField
              form={form}
              name="description"
              label="Description"
              rows={3}
              placeholder="What this criterion is asking about, and what a strong showing looks like."
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive criteria stay on existing templates but cannot be added to new items."
            />
          </>
        )}
      />
    </div>
  );
}
