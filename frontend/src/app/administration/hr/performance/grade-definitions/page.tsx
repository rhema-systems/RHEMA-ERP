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
import { appraisalGradeDefinitionService } from '@/services/hr/appraisal.service';
import { PERFORMANCE_RATING_OPTIONS } from '@/types/hr/appraisal';
import type { AppraisalGradeDefinition, PerformanceRating } from '@/types/hr/appraisal';
import { humanizeEnum } from '@/lib/hr/attendance-format';

/**
 * Grade definitions — the vocabulary an appraisal scores in.
 *
 * A grade is used at two levels, and the difference matters:
 *  • **Per item.** Every template item maps score bands onto grades, and those bands are set
 *    on the item, not here — the same grade can mean 80–100 on one item and 70–89 on another.
 *  • **Overall.** The optional band below maps a whole appraisal's score onto a rating. Fill
 *    it in only for the grades that describe an overall outcome; leave it blank on the rest.
 *
 * The overall bands are not validated against each other here, so keep them from overlapping —
 * a score sitting in two bands resolves to whichever is found first.
 */
const gradeSchema = z
  .object({
    gradeName: z.string().min(1, 'Required').max(50),
    description: z.string().max(500).optional(),
    isActive: z.boolean(),
    overallMinScore: z.coerce.number().min(0).max(100).optional(),
    overallMaxScore: z.coerce.number().min(0).max(100).optional(),
    mappedRating: z.string().optional(),
  })
  .refine(
    (v) =>
      v.overallMinScore === undefined ||
      v.overallMaxScore === undefined ||
      v.overallMinScore <= v.overallMaxScore,
    { message: 'The minimum cannot be above the maximum', path: ['overallMaxScore'] },
  );

type GradeForm = z.input<typeof gradeSchema>;

const emptyGrade: GradeForm = {
  gradeName: '',
  description: '',
  isActive: true,
  overallMinScore: undefined,
  overallMaxScore: undefined,
  mappedRating: '',
};

const toPayload = (values: GradeForm) => {
  const v = gradeSchema.parse(values);
  return {
    gradeName: v.gradeName,
    description: v.description || null,
    isActive: v.isActive,
    overallMinScore: v.overallMinScore ?? null,
    overallMaxScore: v.overallMaxScore ?? null,
    mappedRating: (v.mappedRating || null) as PerformanceRating | null,
  };
};

/** "70 – 84" when a band is set, otherwise a note that this grade is item-level only. */
function overallBand(r: AppraisalGradeDefinition) {
  if (r.overallMinScore == null && r.overallMaxScore == null) return null;
  return `${r.overallMinScore ?? 0} – ${r.overallMaxScore ?? 100}`;
}

export default function AppraisalGradeDefinitionsPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Grade Definitions"
        description="The grades appraisal items are scored into, and the score bands that map an overall result onto a rating."
        backHref="/administration/hr/performance"
      />

      <ResourceListPanel<AppraisalGradeDefinition, GradeForm>
        title="grades"
        singular="grade"
        queryKey={['hr', 'appraisal-grade-definitions']}
        dialogHint="Score bands here describe an overall appraisal result — per-item bands are set on the template item."
        emptyDescription="Add the grades your appraisal forms use."
        list={() => appraisalGradeDefinitionService.getAll()}
        create={(values) => appraisalGradeDefinitionService.create(toPayload(values))}
        update={(id, values) =>
          appraisalGradeDefinitionService.update(id, { id, ...toPayload(values) })
        }
        remove={(id) => appraisalGradeDefinitionService.remove(id)}
        getId={(r) => r.id}
        columns={[
          { header: 'Grade', cell: (r) => <span className="font-medium">{r.gradeName}</span> },
          {
            header: 'Description',
            cell: (r) => (
              <span className="line-clamp-1 text-muted-foreground">{r.description || '—'}</span>
            ),
          },
          {
            header: 'Overall band',
            cell: (r) => {
              const band = overallBand(r);
              return band ? (
                <span className="tabular-nums">{band}</span>
              ) : (
                <span className="text-muted-foreground">Item-level only</span>
              );
            },
          },
          {
            header: 'Rating',
            cell: (r) =>
              r.mappedRating ? (
                <Badge variant="outline">{humanizeEnum(r.mappedRating)}</Badge>
              ) : (
                '—'
              ),
          },
          { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
        ]}
        schema={gradeSchema as any}
        emptyForm={emptyGrade}
        toForm={(r) => ({
          gradeName: r.gradeName,
          description: r.description ?? '',
          isActive: r.isActive,
          overallMinScore: r.overallMinScore ?? undefined,
          overallMaxScore: r.overallMaxScore ?? undefined,
          mappedRating: r.mappedRating ?? '',
        })}
        renderFields={(form) => (
          <>
            <TextField
              form={form}
              name="gradeName"
              label="Grade"
              required
              placeholder="e.g. A, or Exceeds"
            />
            <TextareaField
              form={form}
              name="description"
              label="Description"
              rows={2}
              placeholder="What this grade means to a manager filling in a form."
            />
            <FieldRow>
              <NumberField
                form={form}
                name="overallMinScore"
                label="Overall min score"
                step="0.1"
                placeholder="Blank for item-level only"
              />
              <NumberField
                form={form}
                name="overallMaxScore"
                label="Overall max score"
                step="0.1"
                placeholder="Blank for item-level only"
              />
            </FieldRow>
            <SelectField
              form={form}
              name="mappedRating"
              label="Maps to rating"
              options={PERFORMANCE_RATING_OPTIONS}
              allowEmpty
              emptyLabel="No overall rating"
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive grades disappear from the band editor; bands already using them are untouched."
            />
          </>
        )}
      />
    </div>
  );
}
