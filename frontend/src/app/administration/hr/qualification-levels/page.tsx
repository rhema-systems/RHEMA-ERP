'use client';

/**
 * The qualification ladder — the rungs a qualification can sit on.
 *
 * ⚠ **A level is not a type.** `QualificationType` is a CATEGORY — Education, Certification,
 * License, Membership — and it cannot answer "is a Master's higher than a Diploma", which is what
 * shortlisting and succession actually need. The two coexist because they answer different
 * questions; neither substitutes for the other.
 *
 * ⚠ **Ties are allowed on purpose.** A Higher National Diploma and a Bachelor's degree may be
 * treated as equivalent, and forcing a strict order would make the system assert a ranking the
 * organisation does not hold.
 */

import { z } from 'zod';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { referenceDimensionService } from '@/services/hr/lookup.service';
import type { QualificationLevel } from '@/types/hr/lookups';

const levelSchema = z.object({
  name: z.string().min(1, 'A name is required').max(100),
  code: z.string().max(20).optional(),
  description: z.string().max(500).optional(),
  rank: z.coerce.number().int('Whole numbers only').min(0).max(1000),
  isActive: z.boolean(),
});

type LevelForm = z.input<typeof levelSchema>;

const emptyLevel: LevelForm = {
  name: '',
  code: '',
  description: '',
  rank: 0,
  isActive: true,
};

/** Blank optionals must reach the API as null, not as empty strings. */
function toPayload(values: LevelForm) {
  const parsed = levelSchema.parse(values);
  return {
    ...parsed,
    code: parsed.code || null,
    description: parsed.description || null,
  };
}

export default function QualificationLevelsPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Qualification Levels"
        description="The academic and professional ladder qualifications are ranked on."
        backHref="/administration/hr"
      />

      <ResourceListPanel<QualificationLevel, LevelForm>
        title="qualification levels"
        singular="level"
        queryKey={['hr', 'qualification-levels']}
        // Retiring a rung that qualifications sit on is the risk this screen has to make visible,
        // so the count is a column rather than a surprise at delete time.
        invalidateKeys={[['hr', 'qualifications']]}
        dialogHint="Rank ascending — higher means more advanced. Give two rungs the same rank to treat them as equivalent."
        list={() => referenceDimensionService.getQualificationLevels()}
        create={(values) => referenceDimensionService.createQualificationLevel(toPayload(values) as any)}
        update={(id, values) =>
          referenceDimensionService.updateQualificationLevel(id, toPayload(values) as any)
        }
        remove={(id) => referenceDimensionService.removeQualificationLevel(id)}
        getId={(l) => l.id}
        columns={[
          { header: 'Level', cell: (l) => <span className="font-medium">{l.name}</span> },
          { header: 'Code', cell: (l) => l.code || '—' },
          { header: 'Rank', cell: (l) => l.rank, className: 'text-right' },
          {
            header: 'Qualifications',
            // ⚠ Zero is not decoration. A rung nothing sits on can be retired freely; one that
            // carries qualifications cannot, and the number is the only warning before the attempt.
            cell: (l) =>
              l.qualificationCount > 0 ? (
                l.qualificationCount
              ) : (
                <span className="text-muted-foreground">none</span>
              ),
            className: 'text-right',
          },
          { header: 'Status', cell: (l) => <StatusBadge active={l.isActive} /> },
        ]}
        schema={levelSchema as any}
        emptyForm={emptyLevel}
        toForm={(l) => ({
          name: l.name,
          code: l.code ?? '',
          description: l.description ?? '',
          rank: l.rank,
          isActive: l.isActive,
        })}
        renderFields={(form) => (
          <>
            <FieldRow>
              <TextField form={form} name="name" label="Level name" required />
              <TextField form={form} name="code" label="Code" placeholder="BSC, MSC, PHD" />
            </FieldRow>
            <NumberField form={form} name="rank" label="Rank" required />
            <TextareaField form={form} name="description" label="Description" rows={2} />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="A retired rung stops being offered on new qualifications; those already on it keep it."
            />
          </>
        )}
      />
    </div>
  );
}
