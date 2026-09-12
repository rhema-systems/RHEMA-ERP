'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Target } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  FieldRow,
  NumberField,
  SelectField,
  TextareaField,
  TextField,
} from '@/components/hr/employee/tabs/fields';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import {
  RESPONSIBILITY_TYPES,
  type JobResponsibility,
  type JobResponsibilityKpi,
  type ResponsibilityType,
} from '@/types/hr/job-architecture';
import { childKey, labelFor, nullIfBlank, optionalNumber, type ChildPanelProps } from './shared';

const schema = z.object({
  responsibilityDescription: z.string().trim().min(1, 'Describe the responsibility').max(1000),
  type: z.string().min(1, 'Choose a type'),
  percentageOfTime: optionalNumber(0, 100),
  importanceWeight: optionalNumber(1, 10),
});

type Form = z.infer<typeof schema>;

const empty: Form = {
  responsibilityDescription: '',
  type: 'Core',
  percentageOfTime: null,
  importanceWeight: null,
};

const kpiSchema = z.object({
  kpiStatement: z.string().trim().min(1, 'State the measure').max(500),
  targetOrStandard: z.string().max(500).optional(),
  unitOfMeasure: z.string().max(100).optional(),
  weight: optionalNumber(0, 100),
  sequenceNumber: z.coerce.number().int().min(0),
});

type KpiForm = z.infer<typeof kpiSchema>;

const emptyKpi: KpiForm = {
  kpiStatement: '',
  targetOrStandard: '',
  unitOfMeasure: '',
  weight: null,
  sequenceNumber: 0,
};

/**
 * The responsibilities, and the KPIs that hang off whichever one is selected.
 *
 * ⚠ **KPIs are addressed through the responsibility, not through the job description.** The routes
 * are `responsibilities/{id}/kpis` to add and `kpis/{id}` to change — there is no way to reach a
 * KPI from the job description id alone, which is why this panel is master/detail rather than two
 * independent tables. Selecting a responsibility is the only way to author its measures.
 *
 * ⚠ **`percentageOfTime` does not have to add up to 100 and nothing checks that it does.** The
 * header shows the running total so an author can see when it does not; the API accepts any set.
 *
 * The create DTO also takes nested `qualifications` and `competencies` arrays. They are not offered
 * here: rows created that way are unreachable from the qualification and competency panels' own add
 * dialogs but identical once saved, so two doors onto one collection would only invite confusion.
 */
export function ResponsibilitiesPanel({
  jobDescriptionId,
  canAuthor,
  canDelete,
  invalidateKeys,
}: ChildPanelProps) {
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const responsibilitiesKey = childKey(jobDescriptionId, 'responsibilities');

  const { data: responsibilities } = useQuery({
    queryKey: responsibilitiesKey,
    queryFn: () => jobArchitectureService.getResponsibilities(jobDescriptionId),
    enabled: !!jobDescriptionId,
  });

  const selected = useMemo(
    () => (responsibilities ?? []).find((r) => r.id === selectedId) ?? null,
    [responsibilities, selectedId],
  );

  const totalPercentage = (responsibilities ?? []).reduce(
    (sum, r) => sum + (r.percentageOfTime ?? 0),
    0,
  );

  return (
    <div className="space-y-6">
      {totalPercentage > 0 && (
        <p className="text-sm text-muted-foreground">
          Time allocated across the responsibilities: <strong>{totalPercentage}%</strong>
          {totalPercentage !== 100 && ' — the parts do not add up to a whole week.'}
        </p>
      )}

      <ResourceCollectionTab<JobResponsibility, Form>
        parentId={jobDescriptionId}
        title="responsibilities"
        singular="responsibility"
        queryKey={responsibilitiesKey}
        invalidateKeys={invalidateKeys}
        readOnly={!canAuthor}
        dialogHint="Something the holder is accountable for."
        emptyDescription="Responsibilities are what the holder is accountable for, as distinct from the duties they perform."
        list={(id) => jobArchitectureService.getResponsibilities(id)}
        create={(id, v) =>
          jobArchitectureService.addResponsibility(id, {
            responsibilityDescription: v.responsibilityDescription.trim(),
            type: v.type as ResponsibilityType,
            percentageOfTime: v.percentageOfTime,
            importanceWeight: v.importanceWeight,
          })
        }
        update={(_id, responsibilityId, v) =>
          jobArchitectureService.updateResponsibility(responsibilityId, {
            responsibilityDescription: v.responsibilityDescription.trim(),
            type: v.type as ResponsibilityType,
            percentageOfTime: v.percentageOfTime,
            importanceWeight: v.importanceWeight,
          })
        }
        remove={
          canDelete
            ? (_id, responsibilityId) => jobArchitectureService.deleteResponsibility(responsibilityId)
            : undefined
        }
        getId={(r) => r.id}
        columns={[
          { header: 'Responsibility', cell: (r) => r.responsibilityDescription },
          { header: 'Type', cell: (r) => labelFor(RESPONSIBILITY_TYPES, r.type) },
          {
            header: '% of time',
            cell: (r) => (r.percentageOfTime == null ? '—' : `${r.percentageOfTime}%`),
            className: 'text-right',
          },
          {
            header: 'Weight',
            cell: (r) => r.importanceWeight ?? '—',
            className: 'text-right',
          },
          {
            header: 'KPIs',
            cell: (r) => {
              const count = (r.kpis ?? []).length;
              return (
                <Button
                  variant={selectedId === r.id ? 'secondary' : 'ghost'}
                  size="sm"
                  onClick={() => setSelectedId(selectedId === r.id ? null : r.id)}
                >
                  <Target className="mr-2 h-4 w-4" />
                  {count === 0 ? 'Add measures' : `${count} measure${count === 1 ? '' : 's'}`}
                </Button>
              );
            },
          },
        ]}
        schema={schema}
        emptyForm={empty}
        toForm={(r) => ({
          responsibilityDescription: r.responsibilityDescription,
          type: r.type,
          percentageOfTime: r.percentageOfTime ?? null,
          importanceWeight: r.importanceWeight ?? null,
        })}
        renderFields={(form) => (
          <>
            <TextareaField
              form={form}
              name="responsibilityDescription"
              label="Responsibility"
              rows={3}
              placeholder="e.g. Accountable for the accuracy of the monthly management accounts."
            />
            <SelectField form={form} name="type" label="Type" required options={RESPONSIBILITY_TYPES} />
            <FieldRow>
              <NumberField
                form={form}
                name="percentageOfTime"
                label="% of time"
                placeholder="Optional"
              />
              <NumberField
                form={form}
                name="importanceWeight"
                label="Importance (1–10)"
                placeholder="Optional"
              />
            </FieldRow>
          </>
        )}
      />

      {selected && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Measures for: <span className="font-normal">{selected.responsibilityDescription}</span>
            </CardTitle>
            <div className="flex flex-wrap items-center gap-2 pt-1">
              <Badge variant="outline">{labelFor(RESPONSIBILITY_TYPES, selected.type)}</Badge>
              <Button variant="ghost" size="sm" onClick={() => setSelectedId(null)}>
                Close
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <ResourceCollectionTab<JobResponsibilityKpi, KpiForm>
              parentId={selected.id}
              title="measures"
              singular="measure"
              queryKey={['hr', 'job-analysis', 'responsibilities', selected.id, 'kpis']}
              invalidateKeys={[responsibilitiesKey, ...invalidateKeys]}
              readOnly={!canAuthor}
              dialogHint="How performance against this responsibility is judged."
              emptyDescription="A responsibility with no measure cannot be appraised against."
              list={(responsibilityId) => jobArchitectureService.getResponsibilityKpis(responsibilityId)}
              create={(responsibilityId, v) =>
                jobArchitectureService.addResponsibilityKpi(responsibilityId, {
                  kpiStatement: v.kpiStatement.trim(),
                  targetOrStandard: nullIfBlank(v.targetOrStandard),
                  unitOfMeasure: nullIfBlank(v.unitOfMeasure),
                  weight: v.weight,
                  sequenceNumber: v.sequenceNumber,
                })
              }
              update={(_responsibilityId, kpiId, v) =>
                jobArchitectureService.updateResponsibilityKpi(kpiId, {
                  kpiStatement: v.kpiStatement.trim(),
                  targetOrStandard: nullIfBlank(v.targetOrStandard),
                  unitOfMeasure: nullIfBlank(v.unitOfMeasure),
                  weight: v.weight,
                  sequenceNumber: v.sequenceNumber,
                })
              }
              remove={
                canDelete
                  ? (_responsibilityId, kpiId) => jobArchitectureService.deleteResponsibilityKpi(kpiId)
                  : undefined
              }
              getId={(k) => k.id}
              columns={[
                { header: '#', cell: (k) => k.sequenceNumber, className: 'w-[60px]' },
                { header: 'Measure', cell: (k) => k.kpiStatement },
                {
                  header: 'Target',
                  cell: (k) =>
                    [k.targetOrStandard, k.unitOfMeasure].filter(Boolean).join(' ') || '—',
                },
                {
                  header: 'Weight',
                  cell: (k) => (k.weight == null ? '—' : `${k.weight}%`),
                  className: 'text-right',
                },
              ]}
              schema={kpiSchema}
              emptyForm={emptyKpi}
              toForm={(k) => ({
                kpiStatement: k.kpiStatement,
                targetOrStandard: k.targetOrStandard ?? '',
                unitOfMeasure: k.unitOfMeasure ?? '',
                weight: k.weight ?? null,
                sequenceNumber: k.sequenceNumber,
              })}
              renderFields={(form) => (
                <>
                  <TextareaField
                    form={form}
                    name="kpiStatement"
                    label="Measure"
                    rows={2}
                    placeholder="e.g. Management accounts issued within five working days of month end."
                  />
                  <FieldRow>
                    <TextField
                      form={form}
                      name="targetOrStandard"
                      label="Target or standard"
                      placeholder="e.g. 5"
                    />
                    <TextField
                      form={form}
                      name="unitOfMeasure"
                      label="Unit"
                      placeholder="e.g. working days"
                    />
                  </FieldRow>
                  <FieldRow>
                    <NumberField form={form} name="weight" label="Weight (%)" placeholder="Optional" />
                    <NumberField form={form} name="sequenceNumber" label="Order" />
                  </FieldRow>
                </>
              )}
            />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
