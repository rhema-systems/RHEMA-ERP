'use client';

import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
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
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { SHE_INCIDENT_CATEGORY_OPTIONS } from '@/types/hr/safety';
import type { SheIncidentType } from '@/types/hr/safety';

/**
 * The incident-type catalogue. A reportable type names its regulatory body and reporting window —
 * that pair is what the incident screens use to flag statutory reporting, so a reportable type
 * without a body is legal but toothless. Codes are immutable after creation.
 */
const incidentTypeSchema = z.object({
  code: z.string().min(1, 'A code is required').max(20),
  name: z.string().min(1, 'A name is required').max(150),
  description: z.string().max(500).optional().or(z.literal('')),
  category: z.enum([
    'Accident',
    'NearMiss',
    'DangerousOccurrence',
    'OccupationalIllness',
    'EnvironmentalIncident',
    'PropertyDamage',
    'SecurityIncident',
    'FireIncident',
  ]),
  isReportable: z.boolean(),
  regulatoryBodyId: z.string().optional().or(z.literal('')),
  reportingWindowHours: z.coerce.number().min(1).max(8760).optional(),
  isActive: z.boolean(),
});

type IncidentTypeForm = z.input<typeof incidentTypeSchema>;

const emptyIncidentType: IncidentTypeForm = {
  code: '',
  name: '',
  description: '',
  category: 'Accident',
  isReportable: false,
  regulatoryBodyId: '',
  reportingWindowHours: undefined,
  isActive: true,
};

const blank = (v?: string) => (v && v.length > 0 ? v : null);

export default function SafetyIncidentTypesPage() {
  const { data: regulatoryBodies = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'regulatory-bodies'],
    queryFn: () => safetyReferenceService.getRegulatoryBodies(),
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Incident Types"
        description="The classification behind incident capture. Reportable types carry the authority and the statutory reporting window."
        backHref="/administration/hr/safety"
      />

      <ResourceListPanel<SheIncidentType, IncidentTypeForm>
        title="incident types"
        singular="incident type"
        queryKey={['hr', 'safety-reference', 'incident-types']}
        dialogHint="The code is fixed once created."
        list={() => safetyReferenceService.getIncidentTypes()}
        create={(values) => {
          const v = incidentTypeSchema.parse(values);
          return safetyReferenceService.createIncidentType({
            ...v,
            description: blank(v.description),
            regulatoryBodyId: blank(v.regulatoryBodyId),
            reportingWindowHours: v.isReportable ? (v.reportingWindowHours ?? null) : null,
          });
        }}
        update={(id, values) => {
          const v = incidentTypeSchema.parse(values);
          return safetyReferenceService.updateIncidentType(id, {
            id,
            name: v.name,
            description: blank(v.description),
            category: v.category,
            isReportable: v.isReportable,
            regulatoryBodyId: blank(v.regulatoryBodyId),
            reportingWindowHours: v.isReportable ? (v.reportingWindowHours ?? null) : null,
            isActive: v.isActive,
          });
        }}
        remove={(id) => safetyReferenceService.removeIncidentType(id)}
        getId={(t) => t.id}
        emptyDescription="No incident types yet. Incident capture cannot classify without them."
        columns={[
          { header: 'Code', cell: (t) => <span className="font-mono">{t.code}</span> },
          { header: 'Name', cell: (t) => <span className="font-medium">{t.name}</span> },
          {
            header: 'Category',
            cell: (t) =>
              SHE_INCIDENT_CATEGORY_OPTIONS.find((o) => o.value === t.category)?.label ??
              t.categoryName,
          },
          {
            header: 'Reportable',
            cell: (t) =>
              t.isReportable ? (
                <Badge variant="secondary">
                  {t.regulatoryBodyName ?? 'No authority'}
                  {t.reportingWindowHours ? ` · ${t.reportingWindowHours}h` : ''}
                </Badge>
              ) : (
                <span className="text-muted-foreground">—</span>
              ),
          },
          {
            header: 'Default CAs',
            cell: (t) => (
              <Badge variant={t.defaultCorrectiveActions.length > 0 ? 'secondary' : 'outline'}>
                {t.defaultCorrectiveActions.length}
              </Badge>
            ),
          },
          {
            header: 'Status',
            cell: (t) => <StatusBadge status={t.isActive ? 'Active' : 'Inactive'} />,
          },
        ]}
        schema={incidentTypeSchema}
        emptyForm={emptyIncidentType}
        toForm={(t) => ({
          code: t.code,
          name: t.name,
          description: t.description ?? '',
          category: t.category,
          isReportable: t.isReportable,
          regulatoryBodyId: t.regulatoryBodyId ?? '',
          reportingWindowHours: t.reportingWindowHours ?? undefined,
          isActive: t.isActive,
        })}
        renderFields={(form, editing) => {
          const isReportable = !!form.watch('isReportable');
          return (
            <div className="space-y-4">
              {editing ? (
                <div className="space-y-4">
                  <p className="text-muted-foreground text-sm">
                    Code <span className="font-mono">{form.getValues('code')}</span> — fixed at
                    creation.
                  </p>
                  <TextField form={form} name="name" label="Name" required />
                </div>
              ) : (
                <FieldRow>
                  <TextField form={form} name="code" label="Code" required />
                  <TextField form={form} name="name" label="Name" required />
                </FieldRow>
              )}
              <TextareaField form={form} name="description" label="Description" rows={2} />
              <SelectField
                form={form}
                name="category"
                label="Category"
                required
                options={SHE_INCIDENT_CATEGORY_OPTIONS}
              />
              <SwitchField
                form={form}
                name="isReportable"
                label="Reportable to an authority"
                description="Statutory reporting — incidents of this type are flagged until the authority is notified."
              />
              {isReportable && (
                <FieldRow>
                  <SelectField
                    form={form}
                    name="regulatoryBodyId"
                    label="Regulatory body"
                    allowEmpty
                    emptyLabel="Not set"
                    options={regulatoryBodies.map((b) => ({
                      value: b.id,
                      label: b.shortName ? `${b.shortName} — ${b.name}` : b.name,
                    }))}
                  />
                  <NumberField
                    form={form}
                    name="reportingWindowHours"
                    label="Reporting window (hours)"
                  />
                </FieldRow>
              )}
              <SwitchField
                form={form}
                name="isActive"
                label="Active"
                description="Inactive types stay on existing incidents but are not offered for new ones."
              />
            </div>
          );
        }}
      />
    </div>
  );
}
