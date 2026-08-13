'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { SHE_SNAPSHOT_PERIOD_TYPE_OPTIONS } from '@/types/hr/safety';
import type {
  ShePerformanceSnapshotCreateRequest,
  ShePerformanceSnapshotUpdateRequest,
} from '@/types/hr/safety';
import type { Location } from '@/types/hr/location';

/**
 * Create/edit form for a hand-reported SHE performance snapshot.
 *
 * Every figure here is REPORTED — the SHE officer types it in; nothing is computed from the
 * registers yet (that is the KPI-computation slice). The form says so, and the identity fields
 * (number, period, year, location, preparer) only exist in create mode: the server fixes them at
 * creation, and an edit sends figures only. Once management reviews a snapshot it locks — the
 * server refuses further edits with 422.
 */
export const snapshotFiguresSchema = z.object({
  totalAccidents: z.coerce.number().min(0),
  totalIncidents: z.coerce.number().min(0),
  totalNearMisses: z.coerce.number().min(0),
  totalDangerousOccurrences: z.coerce.number().min(0),
  totalFatalities: z.coerce.number().min(0),
  totalLostTimeInjuries: z.coerce.number().min(0),
  lostTimeInjuryFrequencyRate: z.coerce.number().min(0).optional(),
  totalManHoursWorked: z.coerce.number().min(0),
  totalLostDays: z.coerce.number().min(0),
  inspectionsPlanned: z.coerce.number().min(0),
  inspectionsConducted: z.coerce.number().min(0),
  inspectionsOverdue: z.coerce.number().min(0),
  correctiveActionsIssued: z.coerce.number().min(0),
  correctiveActionsCompleted: z.coerce.number().min(0),
  correctiveActionsOverdue: z.coerce.number().min(0),
  correctiveActionClosureRate: z.coerce.number().min(0).max(100).optional(),
  trainingProgramsPlanned: z.coerce.number().min(0),
  trainingProgramsConducted: z.coerce.number().min(0),
  totalTrainingHours: z.coerce.number().min(0),
  contractorsOnSite: z.coerce.number().min(0),
  contractorInspectionsConducted: z.coerce.number().min(0),
  contractorNonComplianceNoticesIssued: z.coerce.number().min(0),
  contractorComplianceRate: z.coerce.number().min(0).max(100).optional(),
  environmentalIncidents: z.coerce.number().min(0),
  environmentalIncidentsReportedToEpa: z.coerce.number().min(0),
  emergencyDrillsPlanned: z.coerce.number().min(0),
  emergencyDrillsConducted: z.coerce.number().min(0),
  ppeComplianceRate: z.coerce.number().min(0).max(100).optional(),
  housekeepingComplianceRating: z.coerce.number().min(0).max(100).optional(),
  regulatoryObligationsTotal: z.coerce.number().min(0),
  regulatoryObligationsCompliant: z.coerce.number().min(0),
  regulatoryObligationsNonCompliant: z.coerce.number().min(0),
  regulatoryObligationsExpiringSoon: z.coerce.number().min(0),
  managementComments: z.string().max(1000).optional().or(z.literal('')),
});

export const snapshotCreateSchema = snapshotFiguresSchema
  .extend({
    snapshotNumber: z.string().min(1, 'A snapshot number is required').max(30),
    periodType: z.enum(['Monthly', 'Quarterly', 'Annual']),
    year: z.coerce.number().min(2000).max(2100),
    periodNumber: z.coerce.number().min(1).max(12).optional(),
    locationId: z.string().optional().or(z.literal('')),
    preparedById: z.string().min(1, 'Choose who prepared this snapshot'),
  })
  // Monthly needs a month and quarterly a quarter; annual has no period number. The server
  // treats period+location as unique, so a wrong period here means a refused create later.
  .refine((v) => v.periodType === 'Annual' || v.periodNumber !== undefined, {
    message: 'Give the month (1–12) or quarter (1–4) this snapshot covers.',
    path: ['periodNumber'],
  })
  .refine(
    (v) => v.periodType !== 'Quarterly' || (v.periodNumber ?? 1) <= 4,
    { message: 'A quarter is 1–4.', path: ['periodNumber'] },
  );

export type SnapshotFiguresFormValues = z.infer<typeof snapshotFiguresSchema>;
export type SnapshotCreateFormValues = z.infer<typeof snapshotCreateSchema>;

export const emptyFigures: SnapshotFiguresFormValues = {
  totalAccidents: 0,
  totalIncidents: 0,
  totalNearMisses: 0,
  totalDangerousOccurrences: 0,
  totalFatalities: 0,
  totalLostTimeInjuries: 0,
  lostTimeInjuryFrequencyRate: undefined,
  totalManHoursWorked: 0,
  totalLostDays: 0,
  inspectionsPlanned: 0,
  inspectionsConducted: 0,
  inspectionsOverdue: 0,
  correctiveActionsIssued: 0,
  correctiveActionsCompleted: 0,
  correctiveActionsOverdue: 0,
  correctiveActionClosureRate: undefined,
  trainingProgramsPlanned: 0,
  trainingProgramsConducted: 0,
  totalTrainingHours: 0,
  contractorsOnSite: 0,
  contractorInspectionsConducted: 0,
  contractorNonComplianceNoticesIssued: 0,
  contractorComplianceRate: undefined,
  environmentalIncidents: 0,
  environmentalIncidentsReportedToEpa: 0,
  emergencyDrillsPlanned: 0,
  emergencyDrillsConducted: 0,
  ppeComplianceRate: undefined,
  housekeepingComplianceRating: undefined,
  regulatoryObligationsTotal: 0,
  regulatoryObligationsCompliant: 0,
  regulatoryObligationsNonCompliant: 0,
  regulatoryObligationsExpiringSoon: 0,
  managementComments: '',
};

export const emptySnapshot: SnapshotCreateFormValues = {
  ...emptyFigures,
  snapshotNumber: '',
  periodType: 'Monthly',
  year: new Date().getFullYear(),
  periodNumber: undefined,
  locationId: '',
  preparedById: '',
};

const optional = (v: number | undefined) => (v === undefined ? null : v);
const blankText = (v?: string) => (v && v.length > 0 ? v : null);

const figuresPayload = (v: SnapshotFiguresFormValues) => ({
  totalAccidents: v.totalAccidents,
  totalIncidents: v.totalIncidents,
  totalNearMisses: v.totalNearMisses,
  totalDangerousOccurrences: v.totalDangerousOccurrences,
  totalFatalities: v.totalFatalities,
  totalLostTimeInjuries: v.totalLostTimeInjuries,
  lostTimeInjuryFrequencyRate: optional(v.lostTimeInjuryFrequencyRate),
  totalManHoursWorked: v.totalManHoursWorked,
  totalLostDays: v.totalLostDays,
  inspectionsPlanned: v.inspectionsPlanned,
  inspectionsConducted: v.inspectionsConducted,
  inspectionsOverdue: v.inspectionsOverdue,
  correctiveActionsIssued: v.correctiveActionsIssued,
  correctiveActionsCompleted: v.correctiveActionsCompleted,
  correctiveActionsOverdue: v.correctiveActionsOverdue,
  correctiveActionClosureRate: optional(v.correctiveActionClosureRate),
  trainingProgramsPlanned: v.trainingProgramsPlanned,
  trainingProgramsConducted: v.trainingProgramsConducted,
  totalTrainingHours: v.totalTrainingHours,
  contractorsOnSite: v.contractorsOnSite,
  contractorInspectionsConducted: v.contractorInspectionsConducted,
  contractorNonComplianceNoticesIssued: v.contractorNonComplianceNoticesIssued,
  contractorComplianceRate: optional(v.contractorComplianceRate),
  environmentalIncidents: v.environmentalIncidents,
  environmentalIncidentsReportedToEpa: v.environmentalIncidentsReportedToEpa,
  emergencyDrillsPlanned: v.emergencyDrillsPlanned,
  emergencyDrillsConducted: v.emergencyDrillsConducted,
  ppeComplianceRate: optional(v.ppeComplianceRate),
  housekeepingComplianceRating: optional(v.housekeepingComplianceRating),
  regulatoryObligationsTotal: v.regulatoryObligationsTotal,
  regulatoryObligationsCompliant: v.regulatoryObligationsCompliant,
  regulatoryObligationsNonCompliant: v.regulatoryObligationsNonCompliant,
  regulatoryObligationsExpiringSoon: v.regulatoryObligationsExpiringSoon,
  managementComments: blankText(v.managementComments),
});

export function toSnapshotCreateRequest(
  v: SnapshotCreateFormValues,
): ShePerformanceSnapshotCreateRequest {
  return {
    ...figuresPayload(v),
    snapshotNumber: v.snapshotNumber,
    periodType: v.periodType,
    year: v.year,
    periodNumber: v.periodType === 'Annual' ? null : optional(v.periodNumber),
    locationId: blankText(v.locationId),
    preparedById: v.preparedById,
  };
}

export function toSnapshotUpdateRequest(
  id: string,
  v: SnapshotFiguresFormValues,
): ShePerformanceSnapshotUpdateRequest {
  return { id, ...figuresPayload(v) };
}

/** The reported-figure sections, shared verbatim between create and edit. */
function FigureSections({ form }: { form: any }) {
  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Incidents & injuries</CardTitle>
          <CardDescription>Counts for the period, as reported.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="totalAccidents" label="Accidents" />
            <NumberField form={form} name="totalIncidents" label="All incidents" />
            <NumberField form={form} name="totalNearMisses" label="Near misses" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="totalDangerousOccurrences" label="Dangerous occurrences" />
            <NumberField form={form} name="totalFatalities" label="Fatalities" />
            <NumberField form={form} name="totalLostTimeInjuries" label="Lost-time injuries" />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="lostTimeInjuryFrequencyRate"
              label="LTIFR (reported)"
              step="0.01"
            />
            <NumberField form={form} name="totalManHoursWorked" label="Man-hours worked" />
            <NumberField form={form} name="totalLostDays" label="Lost days" />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Inspections & corrective actions</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="inspectionsPlanned" label="Inspections planned" />
            <NumberField form={form} name="inspectionsConducted" label="Conducted" />
            <NumberField form={form} name="inspectionsOverdue" label="Overdue" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="correctiveActionsIssued" label="CAs issued" />
            <NumberField form={form} name="correctiveActionsCompleted" label="Completed" />
            <NumberField form={form} name="correctiveActionsOverdue" label="Overdue" />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="correctiveActionClosureRate"
              label="CA closure rate % (reported)"
              step="0.1"
            />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Training & contractors</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="trainingProgramsPlanned" label="Training planned" />
            <NumberField form={form} name="trainingProgramsConducted" label="Conducted" />
            <NumberField form={form} name="totalTrainingHours" label="Training hours" />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="contractorsOnSite" label="Contractors on site" />
            <NumberField
              form={form}
              name="contractorInspectionsConducted"
              label="Contractor inspections"
            />
            <NumberField
              form={form}
              name="contractorNonComplianceNoticesIssued"
              label="Non-compliance notices"
            />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="contractorComplianceRate"
              label="Contractor compliance % (reported)"
              step="0.1"
            />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Environment, drills & compliance</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <NumberField form={form} name="environmentalIncidents" label="Environmental incidents" />
            <NumberField
              form={form}
              name="environmentalIncidentsReportedToEpa"
              label="Reported to EPA"
            />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="emergencyDrillsPlanned" label="Drills planned" />
            <NumberField form={form} name="emergencyDrillsConducted" label="Drills conducted" />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="ppeComplianceRate"
              label="PPE compliance % (reported)"
              step="0.1"
            />
            <NumberField
              form={form}
              name="housekeepingComplianceRating"
              label="Housekeeping rating % (reported)"
              step="0.1"
            />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="regulatoryObligationsTotal" label="Obligations total" />
            <NumberField form={form} name="regulatoryObligationsCompliant" label="Compliant" />
          </FieldRow>
          <FieldRow>
            <NumberField
              form={form}
              name="regulatoryObligationsNonCompliant"
              label="Non-compliant"
            />
            <NumberField
              form={form}
              name="regulatoryObligationsExpiringSoon"
              label="Expiring soon"
            />
          </FieldRow>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Notes</CardTitle>
        </CardHeader>
        <CardContent>
          <TextareaField
            form={form}
            name="managementComments"
            label="Comments"
            rows={3}
            placeholder="Context for the review — spikes, campaigns, anything the figures alone do not say."
          />
        </CardContent>
      </Card>
    </>
  );
}

interface CreateProps {
  locations: Location[];
  onSubmit: (values: SnapshotCreateFormValues) => Promise<void>;
  submitting: boolean;
  onCancel: () => void;
}

export function SnapshotCreateForm({ locations, onSubmit, submitting, onCancel }: CreateProps) {
  const form = useForm<SnapshotCreateFormValues>({
    resolver: zodResolver(snapshotCreateSchema) as any,
    defaultValues: emptySnapshot,
  });
  const periodType = form.watch('periodType');

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Snapshot</CardTitle>
          <CardDescription>
            One snapshot per period and location — the identity below is fixed once created.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <FieldRow>
            <TextField
              form={form}
              name="snapshotNumber"
              label="Snapshot number"
              required
              placeholder="e.g. SHE-KPI-2026-08"
            />
            <SelectField
              form={form}
              name="periodType"
              label="Period"
              required
              options={SHE_SNAPSHOT_PERIOD_TYPE_OPTIONS}
            />
          </FieldRow>
          <FieldRow>
            <NumberField form={form} name="year" label="Year" required />
            {periodType !== 'Annual' && (
              <NumberField
                form={form}
                name="periodNumber"
                label={periodType === 'Monthly' ? 'Month (1–12)' : 'Quarter (1–4)'}
                required
              />
            )}
          </FieldRow>
          <FieldRow>
            <SelectField
              form={form}
              name="locationId"
              label="Location"
              allowEmpty
              emptyLabel="Whole organisation"
              options={locations.map((l) => ({ value: l.id, label: l.name }))}
            />
            <EmployeePickerField form={form} name="preparedById" label="Prepared by" required />
          </FieldRow>
        </CardContent>
      </Card>

      <FigureSections form={form} />

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          Create snapshot
        </Button>
      </div>
    </form>
  );
}

interface EditProps {
  defaultValues: SnapshotFiguresFormValues;
  snapshotNumber: string;
  onSubmit: (values: SnapshotFiguresFormValues) => Promise<void>;
  submitting: boolean;
  onCancel: () => void;
}

export function SnapshotEditForm({
  defaultValues,
  snapshotNumber,
  onSubmit,
  submitting,
  onCancel,
}: EditProps) {
  const form = useForm<SnapshotFiguresFormValues>({
    resolver: zodResolver(snapshotFiguresSchema) as any,
    defaultValues,
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            Correcting <span className="font-mono">{snapshotNumber}</span>
          </CardTitle>
          <CardDescription>
            Figures only — the period, location and preparer are fixed. A reviewed snapshot is
            locked and the server will refuse this save.
          </CardDescription>
        </CardHeader>
      </Card>

      <FigureSections form={form} />

      <div className="flex justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={submitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          Save figures
        </Button>
      </div>
    </form>
  );
}
