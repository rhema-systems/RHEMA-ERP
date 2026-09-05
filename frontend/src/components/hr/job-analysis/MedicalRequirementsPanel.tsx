'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { SelectField, SwitchField, TextareaField } from '@/components/hr/employee/tabs/fields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import {
  MEDICAL_REQUIREMENT_CATEGORIES,
  type JobMedicalRequirement,
  type MedicalRequirementCategory,
} from '@/types/hr/job-architecture';
import { childKey, labelFor, nullIfBlank, type ChildPanelProps } from './shared';

const schema = z.object({
  category: z.string().min(1, 'Choose a category'),
  requirementDescription: z.string().trim().min(1, 'Describe the requirement').max(1000),
  rationale: z.string().max(1000).optional(),
  contraindications: z.string().max(1000).optional(),
  isMandatory: z.boolean(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  category: 'Medical',
  requirementDescription: '',
  rationale: '',
  contraindications: '',
  isMandatory: true,
};

/**
 * Medical, mental and sensory requirements the job carries — and what would rule someone out.
 *
 * `contraindications` is the field that earns this collection its place: the C# remarks give
 * "not suitable if asthmatic" for a dusty environment as the worked example. It is the only place
 * in the job description that says what disqualifies rather than what is wanted, so the form asks
 * for it in those words rather than as a second free-text box.
 *
 * ⚠ **This is a health criterion attached to a job, and it screens people.** `rationale` is
 * optional to the API and the form asks for it anyway, for the same reason the physical-demands
 * panel asks for a justification: a medical bar with no stated reason cannot be defended later.
 */
export function MedicalRequirementsPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  return (
    <ResourceCollectionTab<JobMedicalRequirement, Form>
      parentId={jobDescriptionId}
      title="medical requirements"
      singular="medical requirement"
      queryKey={childKey(jobDescriptionId, 'medical-requirements')}
      invalidateKeys={invalidateKeys}
      readOnly={!canAuthor}
      dialogHint="A health, mental or sensory requirement the job carries."
      emptyDescription="Record any health, mental or sensory requirement the job carries."
      dialogClassName="sm:max-w-[640px]"
      list={(id) => jobArchitectureService.getMedicalRequirements(id)}
      create={(id, v) =>
        jobArchitectureService.addMedicalRequirement(id, {
          category: v.category as MedicalRequirementCategory,
          requirementDescription: v.requirementDescription.trim(),
          rationale: nullIfBlank(v.rationale),
          contraindications: nullIfBlank(v.contraindications),
          isMandatory: v.isMandatory,
        })
      }
      update={(_id, requirementId, v) =>
        jobArchitectureService.updateMedicalRequirement(requirementId, {
          category: v.category as MedicalRequirementCategory,
          requirementDescription: v.requirementDescription.trim(),
          rationale: nullIfBlank(v.rationale),
          contraindications: nullIfBlank(v.contraindications),
          isMandatory: v.isMandatory,
        })
      }
      remove={
        canDelete
          ? (_id, requirementId) => jobArchitectureService.deleteMedicalRequirement(requirementId)
          : undefined
      }
      getId={(m) => m.id}
      columns={[
        { header: 'Category', cell: (m) => labelFor(MEDICAL_REQUIREMENT_CATEGORIES, m.category) },
        { header: 'Requirement', cell: (m) => m.requirementDescription },
        {
          header: 'Rules out',
          cell: (m) => (
            <span className="text-muted-foreground">{m.contraindications || '—'}</span>
          ),
        },
        {
          header: '',
          cell: (m) =>
            m.isMandatory ? <Badge variant="outline">Mandatory</Badge> : <Badge variant="secondary">Desirable</Badge>,
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(m) => ({
        category: m.category,
        requirementDescription: m.requirementDescription,
        rationale: m.rationale ?? '',
        contraindications: m.contraindications ?? '',
        isMandatory: m.isMandatory,
      })}
      renderFields={(form) => (
        <>
          <SelectField
            form={form}
            name="category"
            label="Category"
            required
            options={MEDICAL_REQUIREMENT_CATEGORIES}
          />
          <TextareaField
            form={form}
            name="requirementDescription"
            label="Requirement"
            rows={2}
            placeholder="e.g. Fit to wear a full-face respirator (annual face-fit test)."
          />
          <TextareaField
            form={form}
            name="rationale"
            label="Why the job requires it"
            rows={2}
            placeholder="A health requirement screens people — state what about the work makes it necessary."
          />
          <TextareaField
            form={form}
            name="contraindications"
            label="What would rule someone out"
            rows={2}
            placeholder="e.g. Not suitable for anyone with a diagnosed respiratory condition."
          />
          <SwitchField
            form={form}
            name="isMandatory"
            label="Mandatory"
            description="Turn off where the requirement is preferred rather than a bar to appointment."
          />
        </>
      )}
    />
  );
}
