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
import { qualificationService } from '@/services/hr/lookup.service';
import {
  QUALIFICATION_TYPES,
  type JobQualification,
  type QualificationType,
} from '@/types/hr/job-architecture';
import { childKey, idOrNull, labelFor, nullIfBlank, optionalNumber, type ChildPanelProps } from './shared';

const schema = z.object({
  type: z.string().min(1, 'Choose a type'),
  qualificationId: z.string().optional(),
  title: z.string().trim().min(1, 'The title is required').max(200),
  description: z.string().max(1000),
  isRequired: z.boolean(),
  jobSpecificRequirements: z.string().max(500).optional(),
  monetaryValue: optionalNumber(0),
  jobResponsibilityId: z.string().optional(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  type: 'Education',
  qualificationId: '',
  title: '',
  description: '',
  isRequired: true,
  jobSpecificRequirements: '',
  monetaryValue: null,
  jobResponsibilityId: '',
};

/**
 * What the holder must have: education, experience, certifications, licences, languages.
 *
 * ⚠ **The responsibility link is create-only.** `CreateJobQualificationDto` carries
 * `jobResponsibilityId`; `UpdateJobQualificationDto` does not declare it at all. So a qualification
 * can be pinned to one responsibility when it is added and never afterwards moved or unpinned —
 * the field is shown on add and hidden on edit, because rendering it on edit would offer a change
 * the API silently discards.
 *
 * ⚠ **`title` is what people read; `qualificationId` is what a rule can match on.** The catalogue
 * link is optional and the title is required, so a row can name a degree the catalogue has never
 * heard of. Only the linked ones can be compared against what an employee actually holds.
 */
export function QualificationsPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  const { data: catalogue } = useQuery({
    queryKey: ['hr', 'qualifications', 'active'],
    queryFn: () => qualificationService.getActive(),
    staleTime: 5 * 60 * 1000,
  });

  const { data: responsibilities } = useQuery({
    queryKey: childKey(jobDescriptionId, 'responsibilities'),
    queryFn: () => jobArchitectureService.getResponsibilities(jobDescriptionId),
    enabled: !!jobDescriptionId,
  });

  const catalogueOptions = (catalogue ?? []).map((q) => ({
    value: q.id,
    label: q.shortCode ? `${q.name} (${q.shortCode})` : q.name,
  }));

  const responsibilityOptions = (responsibilities ?? []).map((r) => ({
    value: r.id,
    label:
      r.responsibilityDescription.length > 80
        ? `${r.responsibilityDescription.slice(0, 80)}…`
        : r.responsibilityDescription,
  }));

  return (
    <ResourceCollectionTab<JobQualification, Form>
      parentId={jobDescriptionId}
      title="qualifications"
      singular="qualification"
      queryKey={childKey(jobDescriptionId, 'qualifications')}
      invalidateKeys={invalidateKeys}
      readOnly={!canAuthor}
      dialogHint="Education, experience, a certification or a licence the job calls for."
      emptyDescription="Record what the holder must have — education, experience, certifications, licences."
      dialogClassName="sm:max-w-[640px]"
      list={(id) => jobArchitectureService.getQualifications(id)}
      create={(id, v) =>
        jobArchitectureService.addQualification(id, {
          jobResponsibilityId: idOrNull(v.jobResponsibilityId),
          type: v.type as QualificationType,
          qualificationId: idOrNull(v.qualificationId),
          title: v.title.trim(),
          description: (v.description ?? '').trim(),
          isRequired: v.isRequired,
          jobSpecificRequirements: nullIfBlank(v.jobSpecificRequirements),
          monetaryValue: v.monetaryValue,
        })
      }
      update={(_id, qualificationId, v) =>
        jobArchitectureService.updateQualification(qualificationId, {
          type: v.type as QualificationType,
          qualificationId: idOrNull(v.qualificationId),
          title: v.title.trim(),
          description: (v.description ?? '').trim(),
          isRequired: v.isRequired,
          jobSpecificRequirements: nullIfBlank(v.jobSpecificRequirements),
          monetaryValue: v.monetaryValue,
        })
      }
      remove={
        canDelete
          ? (_id, qualificationId) => jobArchitectureService.deleteQualification(qualificationId)
          : undefined
      }
      getId={(q) => q.id}
      columns={[
        { header: 'Qualification', cell: (q) => q.title },
        { header: 'Type', cell: (q) => labelFor(QUALIFICATION_TYPES, q.type) },
        {
          header: 'Catalogue',
          cell: (q) => <span className="text-muted-foreground">{q.qualificationName || '—'}</span>,
        },
        {
          header: '',
          cell: (q) =>
            q.isRequired ? <Badge variant="outline">Required</Badge> : <Badge variant="secondary">Desirable</Badge>,
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(q) => ({
        type: q.type,
        qualificationId: q.qualificationId ?? '',
        title: q.title,
        description: q.description ?? '',
        isRequired: q.isRequired,
        jobSpecificRequirements: q.jobSpecificRequirements ?? '',
        monetaryValue: q.monetaryValue ?? null,
        jobResponsibilityId: q.jobResponsibilityId ?? '',
      })}
      renderFields={(form, editing) => (
        <>
          <FieldRow>
            <SelectField form={form} name="type" label="Type" required options={QUALIFICATION_TYPES} />
            <SelectField
              form={form}
              name="qualificationId"
              label="From the catalogue"
              allowEmpty
              emptyLabel="Not in the catalogue"
              placeholder="Optional"
              options={catalogueOptions}
            />
          </FieldRow>

          <TextField
            form={form}
            name="title"
            label="Title"
            required
            placeholder="e.g. Bachelor's degree in Accounting"
          />
          <TextareaField
            form={form}
            name="description"
            label="Description"
            rows={2}
            placeholder="Optional — what the qualification has to cover."
          />
          <TextareaField
            form={form}
            name="jobSpecificRequirements"
            label="Job-specific requirements"
            rows={2}
            placeholder="e.g. Must include a manufacturing costing module."
          />

          <FieldRow>
            <SwitchField
              form={form}
              name="isRequired"
              label="Required"
              description="Off means desirable."
            />
            <NumberField
              form={form}
              name="monetaryValue"
              label="Value in the job valuation"
              step="0.01"
              placeholder="Optional"
            />
          </FieldRow>

          {/* Create-only: the update DTO has no such field, so editing it would change nothing. */}
          {!editing && responsibilityOptions.length > 0 && (
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
          {editing && (
            <p className="text-xs text-muted-foreground">
              Which responsibility a qualification belongs to is fixed when it is added. To move it,
              remove it and add it again.
            </p>
          )}
        </>
      )}
    />
  );
}
