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
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import {
  PHYSICAL_DEMAND_FREQUENCIES,
  PHYSICAL_DEMAND_TYPES,
  type JobPhysicalDemand,
  type PhysicalDemandFrequency,
  type PhysicalDemandType,
} from '@/types/hr/job-architecture';
import { childKey, labelFor, nullIfBlank, optionalNumber, type ChildPanelProps } from './shared';

const schema = z.object({
  demandType: z.string().min(1, 'Choose a demand'),
  demandDescription: z.string().trim().min(1, 'Describe the demand'),
  frequency: z.string().min(1, 'Choose how often'),
  weightOrForceKg: optionalNumber(0),
  distanceOrDuration: z.string().max(200).optional(),
  isEssential: z.boolean(),
  notesOrExamples: z.string().max(1000).optional(),
  isPhysicalAttribute: z.boolean(),
  attributeRequirement: z.string().max(500).optional(),
  justification: z.string().max(1000).optional(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  demandType: 'Sitting',
  demandDescription: '',
  frequency: 'Occasionally',
  weightOrForceKg: null,
  distanceOrDuration: '',
  isEssential: true,
  notesOrExamples: '',
  isPhysicalAttribute: false,
  attributeRequirement: '',
  justification: '',
};

/**
 * What the job asks of the holder's body, and how often.
 *
 * ⚠ **`isPhysicalAttribute` changes what the row means, so the form changes with it.** A demand is
 * something the job *does* ("lifts 20 kg sacks onto a pallet"); an attribute is something the
 * holder must *be* ("normal colour vision"). The second is a screening criterion about a person,
 * which is why `attributeRequirement` and `justification` exist and why this panel only shows them
 * once the switch is on — an attribute recorded without a stated reason is the shape a disability
 * complaint is made of. The API does not require the justification. The screen asks for it.
 *
 * `isEssential` defaults on, matching the create DTO's own default.
 */
export function PhysicalDemandsPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  return (
    <ResourceCollectionTab<JobPhysicalDemand, Form>
      parentId={jobDescriptionId}
      title="physical demands"
      singular="physical demand"
      queryKey={childKey(jobDescriptionId, 'physical-demands')}
      invalidateKeys={invalidateKeys}
      readOnly={!canAuthor}
      dialogHint="Something the job asks of the holder's body, or an attribute they must have."
      emptyDescription="Record what the job asks of the holder physically, and how often."
      dialogClassName="sm:max-w-[640px]"
      list={(id) => jobArchitectureService.getPhysicalDemands(id)}
      create={(id, v) =>
        jobArchitectureService.addPhysicalDemand(id, {
          demandType: v.demandType as PhysicalDemandType,
          demandDescription: v.demandDescription.trim(),
          frequency: v.frequency as PhysicalDemandFrequency,
          weightOrForceKg: v.weightOrForceKg,
          distanceOrDuration: nullIfBlank(v.distanceOrDuration),
          isEssential: v.isEssential,
          notesOrExamples: nullIfBlank(v.notesOrExamples),
          isPhysicalAttribute: v.isPhysicalAttribute,
          attributeRequirement: v.isPhysicalAttribute ? nullIfBlank(v.attributeRequirement) : null,
          justification: v.isPhysicalAttribute ? nullIfBlank(v.justification) : null,
        })
      }
      update={(_id, demandId, v) =>
        jobArchitectureService.updatePhysicalDemand(demandId, {
          demandType: v.demandType as PhysicalDemandType,
          demandDescription: v.demandDescription.trim(),
          frequency: v.frequency as PhysicalDemandFrequency,
          weightOrForceKg: v.weightOrForceKg,
          distanceOrDuration: nullIfBlank(v.distanceOrDuration),
          isEssential: v.isEssential,
          notesOrExamples: nullIfBlank(v.notesOrExamples),
          isPhysicalAttribute: v.isPhysicalAttribute,
          attributeRequirement: v.isPhysicalAttribute ? nullIfBlank(v.attributeRequirement) : null,
          justification: v.isPhysicalAttribute ? nullIfBlank(v.justification) : null,
        })
      }
      remove={
        canDelete ? (_id, demandId) => jobArchitectureService.deletePhysicalDemand(demandId) : undefined
      }
      getId={(d) => d.id}
      columns={[
        { header: 'Demand', cell: (d) => labelFor(PHYSICAL_DEMAND_TYPES, d.demandType) },
        { header: 'Description', cell: (d) => d.demandDescription },
        { header: 'Frequency', cell: (d) => labelFor(PHYSICAL_DEMAND_FREQUENCIES, d.frequency) },
        {
          header: 'Load',
          cell: (d) =>
            d.weightOrForceKg != null
              ? `${d.weightOrForceKg} kg`
              : d.distanceOrDuration || '—',
        },
        {
          header: '',
          cell: (d) => (
            <div className="flex flex-wrap gap-1">
              {d.isEssential && <Badge variant="outline">Essential</Badge>}
              {d.isPhysicalAttribute && <Badge variant="secondary">Attribute</Badge>}
            </div>
          ),
        },
      ]}
      schema={schema}
      emptyForm={empty}
      toForm={(d) => ({
        demandType: d.demandType,
        demandDescription: d.demandDescription,
        frequency: d.frequency,
        weightOrForceKg: d.weightOrForceKg ?? null,
        distanceOrDuration: d.distanceOrDuration ?? '',
        isEssential: d.isEssential,
        notesOrExamples: d.notesOrExamples ?? '',
        isPhysicalAttribute: d.isPhysicalAttribute,
        attributeRequirement: d.attributeRequirement ?? '',
        justification: d.justification ?? '',
      })}
      renderFields={(form) => {
        const isAttribute = !!form.watch('isPhysicalAttribute');
        return (
          <>
            <FieldRow>
              <SelectField
                form={form}
                name="demandType"
                label="Demand"
                required
                options={PHYSICAL_DEMAND_TYPES}
              />
              <SelectField
                form={form}
                name="frequency"
                label="Frequency"
                required
                options={PHYSICAL_DEMAND_FREQUENCIES}
              />
            </FieldRow>

            <TextareaField
              form={form}
              name="demandDescription"
              label="Description"
              rows={2}
              placeholder="e.g. Lifts 20 kg cement bags from ground level onto a pallet."
            />

            <FieldRow>
              <NumberField
                form={form}
                name="weightOrForceKg"
                label="Weight or force (kg)"
                step="0.1"
                placeholder="Leave blank if not applicable"
              />
              <TextField
                form={form}
                name="distanceOrDuration"
                label="Distance or duration"
                placeholder="e.g. up to 50 m, 2 hours at a stretch"
              />
            </FieldRow>

            <SwitchField
              form={form}
              name="isEssential"
              label="Essential to the job"
              description="An essential demand cannot be reassigned or adjusted away."
            />

            <SwitchField
              form={form}
              name="isPhysicalAttribute"
              label="This is an attribute of the person, not an action"
              description="e.g. normal colour vision, rather than something the holder does."
            />

            {isAttribute && (
              <>
                <TextField
                  form={form}
                  name="attributeRequirement"
                  label="Attribute required"
                  placeholder="e.g. Normal colour vision (Ishihara)"
                />
                <TextareaField
                  form={form}
                  name="justification"
                  label="Why the job requires it"
                  rows={2}
                  placeholder="An attribute screens people rather than describing work — state the reason."
                />
              </>
            )}
          </>
        );
      }}
    />
  );
}
