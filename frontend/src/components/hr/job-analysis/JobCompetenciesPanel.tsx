'use client';

import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextareaField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { skillService } from '@/services/hr/skill.service';
import {
  JOB_COMPETENCY_TYPES,
  PROFICIENCY_LEVELS,
  type CompetencyType,
  type JobCompetency,
  type ProficiencyLevel,
} from '@/types/hr/job-architecture';
import { childKey, idOrNull, labelFor, nullIfBlank, optionalNumber, type ChildPanelProps } from './shared';

const schema = z.object({
  competencyName: z.string().trim().min(1, 'Name the competency').max(200),
  competencyId: z.string().optional(),
  skillId: z.string().optional(),
  description: z.string().max(1000).optional(),
  type: z.string().min(1, 'Choose a type'),
  requiredLevel: z.string().min(1, 'Choose the level required'),
  isCritical: z.boolean(),
  monetaryValue: optionalNumber(0),
  jobResponsibilityId: z.string().optional(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  competencyName: '',
  competencyId: '',
  skillId: '',
  description: '',
  type: 'Technical',
  requiredLevel: 'Proficient',
  isCritical: false,
  monetaryValue: null,
  jobResponsibilityId: '',
};

/**
 * The competencies the job needs and the level it needs them at.
 *
 * ⚠ **Three names for one row, and only two of them do anything.** `competencyName` is required
 * free text and is what the job description prints. `competencyId` links to the competency
 * framework and is what the gap analysis matches an employee's assessment against — without it
 * this row cannot be compared to anybody. `skillId` links to the skill catalogue. A row can carry
 * all three; a row with only the name is a sentence in a document.
 *
 * ⚠ **`type` here has four values and is not `CompetencyCategory`.** The framework catalogue is
 * categorised by a six-value enum that includes `Functional`; this one does not. Offering
 * Functional here produces a 400 from the enum converter, which is exactly what this screen's
 * first draft would have done — the TypeScript union had it.
 *
 * ⚠ **The responsibility link** says the job needs this competency BECAUSE of that accountability;
 * "the job as a whole" is a legitimate answer, not an empty field. Create-only until 2026-09-07 —
 * see `QualificationsPanel` for what changed and why.
 */
export function JobCompetenciesPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  const { data: framework } = useQuery({
    queryKey: ['hr', 'competencies', 'active'],
    queryFn: () => jobArchitectureService.getActiveCompetencies(),
    staleTime: 5 * 60 * 1000,
  });

  const { data: skills } = useQuery({
    queryKey: ['hr', 'skills', 'active'],
    queryFn: () => skillService.getActive(),
    staleTime: 5 * 60 * 1000,
  });

  const { data: responsibilities } = useQuery({
    queryKey: childKey(jobDescriptionId, 'responsibilities'),
    queryFn: () => jobArchitectureService.getResponsibilities(jobDescriptionId),
    enabled: !!jobDescriptionId,
  });

  const frameworkOptions = (framework ?? []).map((c) => ({
    value: c.id,
    label: c.code ? `${c.name} (${c.code})` : c.name,
  }));

  const skillOptions = (skills ?? []).map((s) => ({
    value: s.id,
    label: s.category ? `${s.name} — ${s.category}` : s.name,
  }));

  const responsibilityOptions = (responsibilities ?? []).map((r) => ({
    value: r.id,
    label:
      r.responsibilityDescription.length > 80
        ? `${r.responsibilityDescription.slice(0, 80)}…`
        : r.responsibilityDescription,
  }));

  const responsibilityLabels = new Map(responsibilityOptions.map((o) => [o.value, o.label]));

  return (
    <ResourceCollectionTab<JobCompetency, Form>
      parentId={jobDescriptionId}
      title="competencies"
      singular="competency"
      queryKey={childKey(jobDescriptionId, 'competencies')}
      invalidateKeys={invalidateKeys}
      readOnly={!canAuthor}
      dialogHint="Something the holder must be able to do, and how well."
      emptyDescription="Record what the holder must be able to do. Linking to the framework is what makes a gap analysis possible."
      dialogClassName="sm:max-w-[640px]"
      list={(id) => jobArchitectureService.getJobCompetencies(id)}
      create={(id, v) =>
        jobArchitectureService.addJobCompetency(id, {
          jobResponsibilityId: idOrNull(v.jobResponsibilityId),
          skillId: idOrNull(v.skillId),
          competencyId: idOrNull(v.competencyId),
          competencyName: v.competencyName.trim(),
          description: nullIfBlank(v.description),
          type: v.type as CompetencyType,
          requiredLevel: v.requiredLevel as ProficiencyLevel,
          isCritical: v.isCritical,
          monetaryValue: v.monetaryValue,
        })
      }
      update={(_id, competencyRowId, v) =>
        jobArchitectureService.updateJobCompetency(competencyRowId, {
          jobResponsibilityId: idOrNull(v.jobResponsibilityId),
          skillId: idOrNull(v.skillId),
          competencyId: idOrNull(v.competencyId),
          competencyName: v.competencyName.trim(),
          description: nullIfBlank(v.description),
          type: v.type as CompetencyType,
          requiredLevel: v.requiredLevel as ProficiencyLevel,
          isCritical: v.isCritical,
          monetaryValue: v.monetaryValue,
        })
      }
      remove={
        canDelete
          ? (_id, competencyRowId) => jobArchitectureService.deleteJobCompetency(competencyRowId)
          : undefined
      }
      getId={(c) => c.id}
      columns={[
        { header: 'Competency', cell: (c) => c.competencyName },
        { header: 'Type', cell: (c) => labelFor(JOB_COMPETENCY_TYPES, c.type) },
        { header: 'Level required', cell: (c) => labelFor(PROFICIENCY_LEVELS, c.requiredLevel) },
        {
          header: 'Framework',
          cell: (c) =>
            c.competencyId ? (
              <span className="text-muted-foreground">{c.masterCompetencyName || 'Linked'}</span>
            ) : (
              <Badge variant="secondary">Unlinked</Badge>
            ),
        },
        {
          // ⚠ The attachment was write-only — settable on add, shown nowhere afterwards. The
          // responsibility is WHY a competency is required, and it is also how the valuation
          // groups it, so a list that hides it hides the argument. Resolved client-side from the
          // list already fetched for the picker; the DTO carries only the id.
          header: 'For responsibility',
          cell: (c) => (
            <span className="text-muted-foreground">
              {c.jobResponsibilityId
                ? responsibilityLabels.get(c.jobResponsibilityId) ?? 'Another responsibility'
                : 'The job as a whole'}
            </span>
          ),
        },
        { header: '', cell: (c) => (c.isCritical ? <Badge variant="outline">Critical</Badge> : null) },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(c) => ({
        competencyName: c.competencyName,
        competencyId: c.competencyId ?? '',
        skillId: c.skillId ?? '',
        description: c.description ?? '',
        type: c.type,
        requiredLevel: c.requiredLevel,
        isCritical: c.isCritical,
        monetaryValue: c.monetaryValue ?? null,
        jobResponsibilityId: c.jobResponsibilityId ?? '',
      })}
      renderFields={(form) => (
        <>
          <TextField
            form={form}
            name="competencyName"
            label="Competency"
            required
            placeholder="e.g. Financial reporting under IFRS"
          />

          <FieldRow>
            <SelectField form={form} name="type" label="Type" required options={JOB_COMPETENCY_TYPES} />
            <SelectField
              form={form}
              name="requiredLevel"
              label="Level required"
              required
              options={PROFICIENCY_LEVELS}
            />
          </FieldRow>

          <SelectField
            form={form}
            name="competencyId"
            label="From the competency framework"
            allowEmpty
            emptyLabel="Not in the framework"
            placeholder="Optional — needed for gap analysis"
            options={frameworkOptions}
          />
          <SelectField
            form={form}
            name="skillId"
            label="Related skill"
            allowEmpty
            emptyLabel="None"
            placeholder="Optional"
            options={skillOptions}
          />

          <TextareaField
            form={form}
            name="description"
            label="Description"
            rows={2}
            placeholder="Optional — what competent performance looks like for this job."
          />

          <FieldRow>
            <SwitchField
              form={form}
              name="isCritical"
              label="Critical"
              description="The job cannot be done without it."
            />
            <NumberField
              form={form}
              name="monetaryValue"
              label="Value in the job valuation"
              step="0.01"
              placeholder="Optional"
            />
          </FieldRow>

          {/* Editable now: `JobResponsibilityId` reached the update DTO on 2026-09-07. The API
              refuses a responsibility from ANOTHER job description, so the options are this
              document's own and nothing else. */}
          {responsibilityOptions.length > 0 && (
            <SelectField
              form={form}
              name="jobResponsibilityId"
              label="Attach to a responsibility"
              allowEmpty
              emptyLabel="The job as a whole"
              placeholder="The job as a whole"
              options={responsibilityOptions}
            />
          )}
          <p className="text-xs text-muted-foreground">
            Why the job needs it. Leave it on the job as a whole when it is not there for one
            particular accountability.
          </p>
        </>
      )}
    />
  );
}
