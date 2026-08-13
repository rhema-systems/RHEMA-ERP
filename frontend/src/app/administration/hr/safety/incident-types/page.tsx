'use client';

import { useState } from 'react';
import { z } from 'zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useToast } from '@/components/ui/use-toast';
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

/**
 * Manager for an incident type's default corrective actions — the templates attached here
 * auto-populate as corrective actions on every new incident of the type (FR-INC-004).
 */
function DefaultActionsCard() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [typeId, setTypeId] = useState('');
  const [templateId, setTemplateId] = useState('');
  const [deadlineDays, setDeadlineDays] = useState('');
  const [busy, setBusy] = useState(false);

  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'incident-types'],
    queryFn: () => safetyReferenceService.getIncidentTypes(),
  });
  const { data: templates = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'corrective-action-templates', 'active'],
    queryFn: () => safetyReferenceService.getCorrectiveActionTemplates(true),
  });
  const { data: selectedType } = useQuery({
    queryKey: ['hr', 'safety-reference', 'incident-types', 'detail', typeId],
    queryFn: () => safetyReferenceService.getIncidentType(typeId),
    enabled: !!typeId,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-reference', 'incident-types'] });

  const run = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await refresh();
      toast({ title: label });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Default corrective actions</CardTitle>
        <CardDescription>
          Templates attached to an incident type auto-populate as corrective actions on every new
          incident of that type. The deadline runs in days from the incident date.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex flex-wrap items-end gap-3">
          <div className="min-w-56 space-y-2">
            <Label>Incident type</Label>
            <Select value={typeId} onValueChange={setTypeId}>
              <SelectTrigger><SelectValue placeholder="Choose a type" /></SelectTrigger>
              <SelectContent>
                {types.map((t) => (
                  <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="min-w-56 space-y-2">
            <Label>Template</Label>
            <Select value={templateId} onValueChange={setTemplateId}>
              <SelectTrigger><SelectValue placeholder="Choose a template" /></SelectTrigger>
              <SelectContent>
                {templates.map((t) => (
                  <SelectItem key={t.id} value={t.id}>{`${t.code} — ${t.title}`}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="w-40 space-y-2">
            <Label htmlFor="deadline-days">Deadline (days)</Label>
            <Input
              id="deadline-days"
              type="number"
              min={1}
              value={deadlineDays}
              onChange={(e) => setDeadlineDays(e.target.value)}
              placeholder="Template default"
            />
          </div>
          <Button
            disabled={busy || !typeId || !templateId}
            onClick={() =>
              run('Default action attached', async () => {
                await safetyReferenceService.addIncidentTypeCorrectiveAction(typeId, {
                  incidentTypeId: typeId,
                  correctiveActionTemplateId: templateId,
                  displayOrder: (selectedType?.defaultCorrectiveActions.length ?? 0) + 1,
                  deadlineDays: deadlineDays ? Number(deadlineDays) : null,
                  isMandatory: true,
                });
                setTemplateId('');
                setDeadlineDays('');
              })
            }
          >
            Attach
          </Button>
        </div>

        {typeId && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>#</TableHead>
                <TableHead>Template</TableHead>
                <TableHead>Deadline</TableHead>
                <TableHead className="w-16" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {(selectedType?.defaultCorrectiveActions ?? []).length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="text-muted-foreground text-center text-sm">
                    No default actions on this type yet.
                  </TableCell>
                </TableRow>
              ) : (
                selectedType?.defaultCorrectiveActions.map((l) => (
                  <TableRow key={l.id}>
                    <TableCell>{l.displayOrder}</TableCell>
                    <TableCell>
                      <span className="font-mono">{l.correctiveActionTemplateCode}</span>{' '}
                      {l.correctiveActionTemplateTitle}
                    </TableCell>
                    <TableCell>
                      {l.deadlineDays ? `${l.deadlineDays} days from incident` : 'Template default'}
                    </TableCell>
                    <TableCell>
                      <Button
                        variant="ghost"
                        size="icon"
                        disabled={busy}
                        onClick={() =>
                          run('Default action removed', () =>
                            safetyReferenceService.removeIncidentTypeCorrectiveAction(l.id),
                          )
                        }
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}

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

      <DefaultActionsCard />
    </div>
  );
}
