'use client';

import { z } from 'zod';
import { Badge } from '@/components/ui/badge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  FieldRow,
  NumberField,
  SelectField,
  SwitchField,
  TextareaField,
} from '@/components/hr/employee/tabs/fields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import {
  EXPOSURE_LEVELS,
  WORK_ENVIRONMENT_TYPES,
  type ExposureLevel,
  type JobWorkingCondition,
  type WorkEnvironmentType,
} from '@/types/hr/job-architecture';
import { childKey, labelFor, nullIfBlank, optionalNumber, type ChildPanelProps } from './shared';

const schema = z.object({
  environmentType: z.string().min(1, 'Choose an environment'),
  description: z.string().trim().min(1, 'Describe the conditions').max(1000),
  exposureLevel: z.string().min(1, 'Choose an exposure level'),
  requiresPPE: z.boolean(),
  ppeRequirements: z.string().max(500).optional(),
  travelPercentage: optionalNumber(0, 100),
  travelRequirements: z.string().optional(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  environmentType: 'Office',
  description: '',
  exposureLevel: 'None',
  requiresPPE: false,
  ppeRequirements: '',
  travelPercentage: null,
  travelRequirements: '',
};

/**
 * Where the work happens, what the holder is exposed to, and how much of it is travel.
 *
 * ⚠ **`requiresPPE` and `ppeRequirements` are free text and are NOT the PPE tab.** This pair says
 * "this environment calls for protective equipment" in prose; the PPE requirements collection links
 * to the safety module's PPE catalogue and is what an issuance is actually driven from. Recording
 * a hard hat here and nowhere else means nobody is ever issued one.
 *
 * ⚠ **The casing is `requiresPPE` / `ppeRequirements`**, both of them irregular. The DTO declares
 * `RequiresPPE` and `PPERequirements`, and the camel-case policy lower-cases only the leading run,
 * which is why the two fields disagree with each other on the wire.
 */
export function WorkingConditionsPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  return (
    <ResourceCollectionTab<JobWorkingCondition, Form>
      parentId={jobDescriptionId}
      title="working conditions"
      singular="working condition"
      queryKey={childKey(jobDescriptionId, 'working-conditions')}
      invalidateKeys={invalidateKeys}
      readOnly={!canAuthor}
      dialogHint="An environment the holder works in and what it exposes them to."
      emptyDescription="Record the environments the holder works in and what they are exposed to."
      dialogClassName="sm:max-w-[640px]"
      list={(id) => jobArchitectureService.getWorkingConditions(id)}
      create={(id, v) =>
        jobArchitectureService.addWorkingCondition(id, {
          environmentType: v.environmentType as WorkEnvironmentType,
          description: v.description.trim(),
          exposureLevel: v.exposureLevel as ExposureLevel,
          requiresPPE: v.requiresPPE,
          ppeRequirements: v.requiresPPE ? nullIfBlank(v.ppeRequirements) : null,
          travelPercentage: v.travelPercentage,
          travelRequirements: nullIfBlank(v.travelRequirements),
        })
      }
      update={(_id, conditionId, v) =>
        jobArchitectureService.updateWorkingCondition(conditionId, {
          environmentType: v.environmentType as WorkEnvironmentType,
          description: v.description.trim(),
          exposureLevel: v.exposureLevel as ExposureLevel,
          requiresPPE: v.requiresPPE,
          ppeRequirements: v.requiresPPE ? nullIfBlank(v.ppeRequirements) : null,
          travelPercentage: v.travelPercentage,
          travelRequirements: nullIfBlank(v.travelRequirements),
        })
      }
      remove={
        canDelete
          ? (_id, conditionId) => jobArchitectureService.deleteWorkingCondition(conditionId)
          : undefined
      }
      getId={(c) => c.id}
      columns={[
        { header: 'Environment', cell: (c) => labelFor(WORK_ENVIRONMENT_TYPES, c.environmentType) },
        { header: 'Description', cell: (c) => c.description },
        { header: 'Exposure', cell: (c) => labelFor(EXPOSURE_LEVELS, c.exposureLevel) },
        {
          header: 'Travel',
          cell: (c) => (c.travelPercentage == null ? '—' : `${c.travelPercentage}%`),
        },
        {
          header: '',
          cell: (c) => (c.requiresPPE ? <Badge variant="outline">PPE</Badge> : null),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(c) => ({
        environmentType: c.environmentType,
        description: c.description,
        exposureLevel: c.exposureLevel,
        requiresPPE: c.requiresPPE,
        ppeRequirements: c.ppeRequirements ?? '',
        travelPercentage: c.travelPercentage ?? null,
        travelRequirements: c.travelRequirements ?? '',
      })}
      renderFields={(form) => {
        const needsPpe = !!form.watch('requiresPPE');
        return (
          <>
            <FieldRow>
              <SelectField
                form={form}
                name="environmentType"
                label="Environment"
                required
                options={WORK_ENVIRONMENT_TYPES}
              />
              <SelectField
                form={form}
                name="exposureLevel"
                label="Exposure level"
                required
                options={EXPOSURE_LEVELS}
              />
            </FieldRow>

            <TextareaField
              form={form}
              name="description"
              label="Description"
              rows={2}
              placeholder="e.g. Plant floor with continuous machine noise above 85 dB."
            />

            <SwitchField
              form={form}
              name="requiresPPE"
              label="Protective equipment is required here"
              description="Also add the items to the PPE tab — issuance is driven from there, not from this note."
            />

            {needsPpe && (
              <TextareaField
                form={form}
                name="ppeRequirements"
                label="Protective equipment needed"
                rows={2}
                placeholder="e.g. Ear defenders and steel toe-capped boots at all times on the floor."
              />
            )}

            <NumberField
              form={form}
              name="travelPercentage"
              label="Travel (% of time)"
              placeholder="Leave blank if the role does not travel"
            />
            <TextareaField
              form={form}
              name="travelRequirements"
              label="Travel requirements"
              rows={2}
              placeholder="e.g. Two site visits a month, occasional overnight stays."
            />
          </>
        );
      }}
    />
  );
}
