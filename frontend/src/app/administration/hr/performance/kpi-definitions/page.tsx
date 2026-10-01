'use client';

import { z } from 'zod';
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
import { kpiDefinitionService } from '@/services/hr/goals.service';
import { MEASUREMENT_TYPE_OPTIONS } from '@/types/hr/goals';
import type { KpiDefinition, MeasurementType } from '@/types/hr/goals';
import { humanizeEnum } from '@/lib/hr/attendance-format';

/**
 * KPI definitions — the reusable "how is this measured" half of a goal.
 *
 * An employee goal that names one inherits its measurement type and unit, so the same
 * indicator is scored the same way wherever it appears. Definitions carry no target: the
 * target is per goal, because the same KPI means different numbers for different people.
 *
 * Deactivating keeps a definition out of new goals and template criteria without touching what
 * already uses it. A definition in use — on a template criterion or an employee goal — is not
 * deleted and keeps its measurement type; its name, description, unit and tolerance stay editable
 * (performance closure E-g1 — the comment said the delete was refused, and nothing refused it).
 */
const kpiSchema = z.object({
  kpiName: z.string().min(1, 'Required').max(200),
  description: z.string().max(1000).optional(),
  measurementType: z.string().min(1, 'Required'),
  unit: z.string().max(50).optional(),
  tolerancePercent: z.coerce.number().min(0).max(100).optional(),
  isActive: z.boolean(),
});

type KpiForm = z.input<typeof kpiSchema>;

const emptyKpi: KpiForm = {
  kpiName: '',
  description: '',
  measurementType: 'NumericAbsolute',
  unit: '',
  tolerancePercent: undefined,
  isActive: true,
};

/** Blank tolerance means "exact" rather than zero tolerance, so it is sent as null. */
const toPayload = (values: KpiForm) => {
  const v = kpiSchema.parse(values);
  return {
    kpiName: v.kpiName,
    description: v.description || null,
    measurementType: v.measurementType as MeasurementType,
    unit: v.unit || null,
    tolerancePercent: v.tolerancePercent ?? null,
    isActive: v.isActive,
  };
};

export default function KpiDefinitionsPage() {
  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="KPI Definitions"
        description="How goals are measured: the measurement type, the unit and the tolerance a result may miss its target by."
        backHref="/administration/hr/performance"
      />

      <ResourceListPanel<KpiDefinition, KpiForm>
        title="KPI definitions"
        singular="KPI definition"
        queryKey={['hr', 'kpi-definitions']}
        dialogHint="Targets are set per goal — a definition only says how the indicator is measured. A definition in use keeps its measurement type."
        emptyDescription="Add the indicators your goals are scored against."
        list={() => kpiDefinitionService.getAll()}
        create={(values) => kpiDefinitionService.create(toPayload(values))}
        update={(id, values) => kpiDefinitionService.update(id, { id, ...toPayload(values) })}
        remove={(id) => kpiDefinitionService.remove(id)}
        getId={(r) => r.id}
        columns={[
          { header: 'Name', cell: (r) => <span className="font-medium">{r.kpiName}</span> },
          { header: 'Measurement', cell: (r) => humanizeEnum(r.measurementType) },
          { header: 'Unit', cell: (r) => r.unit || '—' },
          {
            header: 'Tolerance',
            cell: (r) => (r.tolerancePercent == null ? 'Exact' : `±${r.tolerancePercent}%`),
            className: 'text-right',
          },
          { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
        ]}
        schema={kpiSchema as any}
        emptyForm={emptyKpi}
        toForm={(r) => ({
          kpiName: r.kpiName,
          description: r.description ?? '',
          measurementType: r.measurementType,
          unit: r.unit ?? '',
          tolerancePercent: r.tolerancePercent ?? undefined,
          isActive: r.isActive,
        })}
        renderFields={(form) => (
          <>
            <TextField
              form={form}
              name="kpiName"
              label="KPI name"
              required
              placeholder="e.g. Customer satisfaction score"
            />
            <TextareaField
              form={form}
              name="description"
              label="Description"
              rows={2}
              placeholder="What this indicator captures and where the number comes from."
            />
            <FieldRow>
              <SelectField
                form={form}
                name="measurementType"
                label="Measurement type"
                required
                options={MEASUREMENT_TYPE_OPTIONS}
              />
              <TextField form={form} name="unit" label="Unit" placeholder="%, days, GHS…" />
            </FieldRow>
            <NumberField
              form={form}
              name="tolerancePercent"
              label="Tolerance (%)"
              step="0.1"
              placeholder="Blank for an exact target"
            />
            <SwitchField
              form={form}
              name="isActive"
              label="Active"
              description="Inactive definitions stay on existing goals but cannot be chosen for new ones."
            />
          </>
        )}
      />
    </div>
  );
}
