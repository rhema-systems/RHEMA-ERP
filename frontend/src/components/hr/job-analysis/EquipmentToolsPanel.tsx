'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { GraduationCap } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  FieldRow,
  SelectField,
  SwitchField,
  TextareaField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { trainingProgramService } from '@/services/hr/training-program.service';
import {
  EQUIPMENT_TYPES,
  PROFICIENCY_LEVELS,
  type EquipmentType,
  type JobEquipmentTool,
  type JobEquipmentTraining,
  type ProficiencyLevel,
} from '@/types/hr/job-architecture';
import { childKey, idOrNull, labelFor, nullIfBlank, type ChildPanelProps } from './shared';

const schema = z.object({
  itemName: z.string().trim().min(1, 'Name the item').max(200),
  type: z.string().min(1, 'Choose a type'),
  descriptionOrSpecification: z.string().max(500),
  requiredProficiency: z.string().min(1, 'Choose the level required'),
  isEssential: z.boolean(),
  trainingRequired: z.string().max(1000).optional(),
  linkedQualificationId: z.string().optional(),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  itemName: '',
  type: 'SoftwareApplication',
  descriptionOrSpecification: '',
  requiredProficiency: 'WorkingKnowledge',
  isEssential: true,
  trainingRequired: '',
  linkedQualificationId: '',
};

const trainingSchema = z.object({
  requirementText: z.string().trim().min(1, 'State what training is needed').max(500),
  trainingProgramId: z.string().optional(),
  isMandatory: z.boolean(),
});

type TrainingForm = z.infer<typeof trainingSchema>;

const emptyTraining: TrainingForm = {
  requirementText: '',
  trainingProgramId: '',
  isMandatory: true,
};

/**
 * The equipment and tools the job uses, and the training each one needs.
 *
 * ⚠ **Training hangs off the TOOL, not off the job description.** `equipment-tools/{id}/training`
 * to add, `equipment-training/{id}` to change — so a tool must exist and be selected before its
 * training can be authored, and removing a tool takes its training with it. That is why this is
 * master/detail; there is no job-description-wide list of equipment training to render.
 *
 * ⚠ **`trainingRequired` on the tool and the training collection are two different things.** The
 * first is a free-text note on the tool; the second is itemised rows that can each link to a
 * training program in the catalogue and be marked mandatory. A note is not a requirement anything
 * can act on, so the form labels it as a summary and points to the itemised list.
 */
export function EquipmentToolsPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const toolsKey = childKey(jobDescriptionId, 'equipment-tools');

  const { data: tools } = useQuery({
    queryKey: toolsKey,
    queryFn: () => jobArchitectureService.getEquipmentTools(jobDescriptionId),
    enabled: !!jobDescriptionId,
  });

  const { data: programs } = useQuery({
    queryKey: ['hr', 'training-programs', 'all'],
    queryFn: () => trainingProgramService.getAll(),
    staleTime: 5 * 60 * 1000,
  });

  /**
   * ⚠ **The qualification picker reads THIS job description's own qualification rows, not the
   * qualification catalogue.** The field is called `LinkedQualificationId` and the DTO says only
   * `Guid?`, so the catalogue is the obvious guess and it is wrong: the FK is
   * `FK_JobEquipmentTools_JobQualifications_LinkedQualificationId`, and the navigation is typed
   * `JobQualification`. Pointing it at a catalogue id fails the constraint and the request 500s —
   * which is exactly what this panel's first version did on every save.
   *
   * It also means the link is only offerable once the Qualifications tab has rows, so the field
   * hides itself rather than presenting an empty dropdown with no way to fill it.
   */
  const { data: jobQualifications } = useQuery({
    queryKey: childKey(jobDescriptionId, 'qualifications'),
    queryFn: () => jobArchitectureService.getQualifications(jobDescriptionId),
    enabled: !!jobDescriptionId,
  });

  const selected = useMemo(
    () => (tools ?? []).find((t) => t.id === selectedId) ?? null,
    [tools, selectedId],
  );

  const programOptions = (programs ?? []).map((p) => ({
    value: p.id,
    label: p.programCode ? `${p.programName} (${p.programCode})` : p.programName,
  }));

  const qualificationOptions = (jobQualifications ?? []).map((q) => ({
    value: q.id,
    label: q.title,
  }));

  return (
    <div className="space-y-6">
      <ResourceCollectionTab<JobEquipmentTool, Form>
        parentId={jobDescriptionId}
        title="equipment and tools"
        singular="item"
        queryKey={toolsKey}
        invalidateKeys={invalidateKeys}
        readOnly={!canAuthor}
        dialogHint="Something the holder operates or works with."
        emptyDescription="Record the software, machinery, vehicles and tools the job uses."
        dialogClassName="sm:max-w-[640px]"
        list={(id) => jobArchitectureService.getEquipmentTools(id)}
        create={(id, v) =>
          jobArchitectureService.addEquipmentTool(id, {
            itemName: v.itemName.trim(),
            type: v.type as EquipmentType,
            descriptionOrSpecification: (v.descriptionOrSpecification ?? '').trim(),
            requiredProficiency: v.requiredProficiency as ProficiencyLevel,
            isEssential: v.isEssential,
            trainingRequired: nullIfBlank(v.trainingRequired),
            linkedQualificationId: idOrNull(v.linkedQualificationId),
          })
        }
        update={(_id, toolId, v) =>
          jobArchitectureService.updateEquipmentTool(toolId, {
            itemName: v.itemName.trim(),
            type: v.type as EquipmentType,
            descriptionOrSpecification: (v.descriptionOrSpecification ?? '').trim(),
            requiredProficiency: v.requiredProficiency as ProficiencyLevel,
            isEssential: v.isEssential,
            trainingRequired: nullIfBlank(v.trainingRequired),
            linkedQualificationId: idOrNull(v.linkedQualificationId),
          })
        }
        remove={
          canDelete
            ? async (_id, toolId) => {
                if (selectedId === toolId) setSelectedId(null);
                return jobArchitectureService.deleteEquipmentTool(toolId);
              }
            : undefined
        }
        getId={(t) => t.id}
        columns={[
          { header: 'Item', cell: (t) => t.itemName },
          { header: 'Type', cell: (t) => labelFor(EQUIPMENT_TYPES, t.type) },
          { header: 'Proficiency', cell: (t) => labelFor(PROFICIENCY_LEVELS, t.requiredProficiency) },
          {
            header: '',
            cell: (t) => (t.isEssential ? <Badge variant="outline">Essential</Badge> : null),
          },
          {
            header: 'Training',
            cell: (t) => {
              const count = (t.trainingRequirements ?? []).length;
              return (
                <Button
                  variant={selectedId === t.id ? 'secondary' : 'ghost'}
                  size="sm"
                  onClick={() => setSelectedId(selectedId === t.id ? null : t.id)}
                >
                  <GraduationCap className="mr-2 h-4 w-4" />
                  {count === 0 ? 'Add training' : `${count} requirement${count === 1 ? '' : 's'}`}
                </Button>
              );
            },
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(t) => ({
          itemName: t.itemName,
          type: t.type,
          descriptionOrSpecification: t.descriptionOrSpecification ?? '',
          requiredProficiency: t.requiredProficiency,
          isEssential: t.isEssential,
          trainingRequired: t.trainingRequired ?? '',
          linkedQualificationId: t.linkedQualificationId ?? '',
        })}
        renderFields={(form) => (
          <>
            <TextField
              form={form}
              name="itemName"
              label="Item"
              required
              placeholder="e.g. Toyota Hilux, SAP FI module, 5-tonne forklift"
            />
            <FieldRow>
              <SelectField form={form} name="type" label="Type" required options={EQUIPMENT_TYPES} />
              <SelectField
                form={form}
                name="requiredProficiency"
                label="Proficiency required"
                required
                options={PROFICIENCY_LEVELS}
              />
            </FieldRow>
            <TextareaField
              form={form}
              name="descriptionOrSpecification"
              label="Description or specification"
              rows={2}
              placeholder="Optional — model, version, capacity."
            />
            {/* Only offerable once the Qualifications tab has rows — see the note above. */}
            {qualificationOptions.length > 0 && (
              <SelectField
                form={form}
                name="linkedQualificationId"
                label="Qualification this item requires"
                allowEmpty
                emptyLabel="None"
                placeholder="Optional — one of this job's qualifications"
                options={qualificationOptions}
              />
            )}
            <TextareaField
              form={form}
              name="trainingRequired"
              label="Training summary"
              rows={2}
              placeholder="A note. Itemise the actual requirements under Training on this row."
            />
            <SwitchField
              form={form}
              name="isEssential"
              label="Essential"
              description="The job cannot be done without this item."
            />
          </>
        )}
      />

      {selected && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Training for: <span className="font-normal">{selected.itemName}</span>
            </CardTitle>
            <div className="flex flex-wrap items-center gap-2 pt-1">
              <Badge variant="outline">{labelFor(EQUIPMENT_TYPES, selected.type)}</Badge>
              <Button variant="ghost" size="sm" onClick={() => setSelectedId(null)}>
                Close
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <ResourceCollectionTab<JobEquipmentTraining, TrainingForm>
              parentId={selected.id}
              title="training requirements"
              singular="training requirement"
              queryKey={['hr', 'job-analysis', 'equipment-tools', selected.id, 'training']}
              invalidateKeys={[toolsKey, ...invalidateKeys]}
              readOnly={!canAuthor}
              dialogHint="Training the holder needs before using this item."
              emptyDescription="Linking a training program is what lets a nomination be raised from this requirement."
              list={(toolId) => jobArchitectureService.getEquipmentTrainings(toolId)}
              create={(toolId, v) =>
                jobArchitectureService.addEquipmentTraining(toolId, {
                  requirementText: v.requirementText.trim(),
                  trainingProgramId: idOrNull(v.trainingProgramId),
                  isMandatory: v.isMandatory,
                })
              }
              update={(_toolId, trainingId, v) =>
                jobArchitectureService.updateEquipmentTraining(trainingId, {
                  requirementText: v.requirementText.trim(),
                  trainingProgramId: idOrNull(v.trainingProgramId),
                  isMandatory: v.isMandatory,
                })
              }
              remove={
                canDelete
                  ? (_toolId, trainingId) => jobArchitectureService.deleteEquipmentTraining(trainingId)
                  : undefined
              }
              getId={(t) => t.id}
              columns={[
                { header: 'Requirement', cell: (t) => t.requirementText },
                {
                  header: 'Program',
                  cell: (t) =>
                    t.trainingProgramId ? (
                      <span className="text-muted-foreground">{t.trainingProgramName || 'Linked'}</span>
                    ) : (
                      <Badge variant="secondary">Unlinked</Badge>
                    ),
                },
                {
                  header: '',
                  cell: (t) =>
                    t.isMandatory ? (
                      <Badge variant="outline">Mandatory</Badge>
                    ) : (
                      <Badge variant="secondary">Optional</Badge>
                    ),
                },
              ]}
              schema={trainingSchema}
              emptyForm={emptyTraining}
              toForm={(t) => ({
                requirementText: t.requirementText,
                trainingProgramId: t.trainingProgramId ?? '',
                isMandatory: t.isMandatory,
              })}
              renderFields={(form) => (
                <>
                  <TextareaField
                    form={form}
                    name="requirementText"
                    label="Requirement"
                    rows={2}
                    placeholder="e.g. Certified forklift operator training, refreshed every three years."
                  />
                  <SelectField
                    form={form}
                    name="trainingProgramId"
                    label="Training program"
                    allowEmpty
                    emptyLabel="Not in the catalogue"
                    placeholder="Optional — links this to the training module"
                    options={programOptions}
                  />
                  <SwitchField
                    form={form}
                    name="isMandatory"
                    label="Mandatory"
                    description="Turn off where the training is recommended rather than required before use."
                  />
                </>
              )}
            />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
