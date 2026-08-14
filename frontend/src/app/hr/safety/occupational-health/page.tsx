'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyOccupationalHealthService } from '@/services/hr/safety-occupational-health.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_HEALTH_SURVEILLANCE_TYPE_OPTIONS,
  SHE_HEALTH_SURVEILLANCE_RESULT_OPTIONS,
  SHE_FIRST_AID_STATION_TYPE_OPTIONS,
  SHE_WELLNESS_PROGRAM_TYPE_OPTIONS,
  SHE_WELLNESS_PROGRAM_STATUS_OPTIONS,
} from '@/types/hr/safety-health';
import type {
  SheHealthSurveillanceSummary,
  SheHealthSurveillanceType,
  SheHealthSurveillanceResult,
  SheFirstAidStation,
  SheFirstAidStationType,
  SheWellnessProgram,
  SheWellnessProgramType,
  SheWellnessProgramStatus,
} from '@/types/hr/safety-health';

/**
 * Occupational health (FR-SHE-140–143): health surveillance, first-aid stations and wellness
 * programs. Access rides the HR.Medical.* policies — surveillance results and restrictions
 * are medical-grade data. Due queues here are polled; nothing reminds anyone automatically
 * until the slice-13 job engine, so this screen IS the recall list.
 *
 * Surveillance rows in the table are the summary shape; editing re-reads the full record.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);

const surveillanceSchema = z.object({
  surveillanceNumber: z.string().min(1, 'A number is required').max(30),
  employeeId: z.string().min(1, 'An employee is required'),
  type: z.string().min(1),
  exposureHazard: z.string().max(300).optional().or(z.literal('')),
  examinationDate: z.string().min(1, 'An examination date is required'),
  nextExaminationDate: z.string().optional().or(z.literal('')),
  examiningPhysician: z.string().max(200).optional().or(z.literal('')),
  result: z.string().min(1),
  findings: z.string().max(2000).optional().or(z.literal('')),
  recommendations: z.string().max(1000).optional().or(z.literal('')),
  workRestrictionIssued: z.boolean(),
  workRestrictionDetails: z.string().max(500).optional().or(z.literal('')),
  recordedById: z.string().min(1, 'A recorder is required'),
});
type SurveillanceForm = z.input<typeof surveillanceSchema>;

const stationSchema = z.object({
  stationCode: z.string().min(1, 'A code is required').max(30),
  name: z.string().min(1, 'A name is required').max(200),
  locationId: z.string().min(1, 'A location is required'),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  type: z.string().min(1),
  responsibleAiderId: z.string().optional().or(z.literal('')),
  lastInspectionDate: z.string().optional().or(z.literal('')),
  nextInspectionDate: z.string().optional().or(z.literal('')),
  isFullyStocked: z.boolean(),
  stockingDeficiencies: z.string().max(500).optional().or(z.literal('')),
  isActive: z.boolean(),
  notes: z.string().max(300).optional().or(z.literal('')),
});
type StationForm = z.input<typeof stationSchema>;

const programSchema = z.object({
  programCode: z.string().min(1, 'A code is required').max(30),
  title: z.string().min(1, 'A title is required').max(200),
  description: z.string().max(1000).optional().or(z.literal('')),
  type: z.string().min(1),
  startDate: z.string().min(1, 'A start date is required'),
  endDate: z.string().optional().or(z.literal('')),
  coordinatorId: z.string().optional().or(z.literal('')),
  status: z.string().min(1),
  participantsCount: z.coerce.number().min(0).optional(),
  outcomes: z.string().max(1000).optional().or(z.literal('')),
  isActive: z.boolean(),
});
type ProgramForm = z.input<typeof programSchema>;

/** The edit dialog re-reads the full record — summary rows lack the clinical fields. */
const surveillanceEditSchema = surveillanceSchema.omit({
  surveillanceNumber: true,
  employeeId: true,
  recordedById: true,
});
type SurveillanceEditForm = z.input<typeof surveillanceEditSchema>;

const resultVariant = (v: string): 'default' | 'secondary' | 'destructive' | 'outline' =>
  v === 'Normal' ? 'secondary' : v === 'TemporarilyUnfit' || v === 'PermanentlyUnfit' ? 'destructive' : 'default';

export default function OccupationalHealthPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editingSurveillance, setEditingSurveillance] = useState<{
    id: string;
    number: string;
    /** Round-tripped untouched — no screen edits it, and omitting it would wipe it. */
    documentPath: string | null;
  } | null>(null);
  const [busy, setBusy] = useState(false);
  const editForm = useForm<SurveillanceEditForm>({ resolver: zodResolver(surveillanceEditSchema) });

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });
  const { data: dueForExam = [] } = useQuery({
    queryKey: ['hr', 'safety-occ-health', 'surveillance', 'due'],
    queryFn: () => safetyOccupationalHealthService.getSurveillanceDueForExamination(30),
  });
  const { data: withRestrictions = [] } = useQuery({
    queryKey: ['hr', 'safety-occ-health', 'surveillance', 'restrictions'],
    queryFn: () => safetyOccupationalHealthService.getSurveillanceWithRestrictions(),
  });
  const { data: stationsDue = [] } = useQuery({
    queryKey: ['hr', 'safety-occ-health', 'stations', 'due'],
    queryFn: () => safetyOccupationalHealthService.getFirstAidStationsDueForInspection(30),
  });
  const { data: underStocked = [] } = useQuery({
    queryKey: ['hr', 'safety-occ-health', 'stations', 'under-stocked'],
    queryFn: () => safetyOccupationalHealthService.getUnderStockedStations(),
  });

  const locationOptions = locations.map((l) => ({ value: l.id, label: l.name }));

  const openSurveillanceEdit = async (row: SheHealthSurveillanceSummary) => {
    const full = await safetyOccupationalHealthService.getSurveillanceById(row.id);
    editForm.reset({
      type: full.type,
      exposureHazard: full.exposureHazard ?? '',
      examinationDate: full.examinationDate.slice(0, 10),
      nextExaminationDate: full.nextExaminationDate?.slice(0, 10) ?? '',
      examiningPhysician: full.examiningPhysician ?? '',
      result: full.result,
      findings: full.findings ?? '',
      recommendations: full.recommendations ?? '',
      workRestrictionIssued: full.workRestrictionIssued,
      workRestrictionDetails: full.workRestrictionDetails ?? '',
    });
    setEditingSurveillance({
      id: full.id,
      number: full.surveillanceNumber,
      documentPath: full.documentPath ?? null,
    });
  };

  const submitSurveillanceEdit = editForm.handleSubmit(async (values) => {
    if (!editingSurveillance) return;
    setBusy(true);
    try {
      const v = surveillanceEditSchema.parse(values);
      await safetyOccupationalHealthService.updateSurveillance(editingSurveillance.id, {
        id: editingSurveillance.id,
        type: v.type as SheHealthSurveillanceType,
        exposureHazard: blank(v.exposureHazard),
        examinationDate: new Date(v.examinationDate).toISOString(),
        nextExaminationDate: dateOrNull(v.nextExaminationDate),
        examiningPhysician: blank(v.examiningPhysician),
        result: v.result as SheHealthSurveillanceResult,
        findings: blank(v.findings),
        recommendations: blank(v.recommendations),
        workRestrictionIssued: v.workRestrictionIssued,
        workRestrictionDetails: blank(v.workRestrictionDetails),
        documentPath: editingSurveillance.documentPath,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-occ-health'] });
      toast({ title: 'Surveillance record updated' });
      setEditingSurveillance(null);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Updating failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Occupational Health"
        description="Health surveillance, first-aid stations and wellness programs. Access requires medical permissions — results and restrictions are medical-grade data. Recall dates are checked here manually; no automatic reminders fire yet."
        backHref="/hr/safety"
      />

      {(dueForExam.length > 0 || withRestrictions.length > 0) && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Surveillance watch list</CardTitle>
            <CardDescription>
              Re-examinations due within 30 days, and everyone under an active work restriction.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {dueForExam.map((s) => (
              <Badge key={`due-${s.id}`} variant="secondary">
                {s.employeeName} · {s.typeName} · due {fmtDate(s.nextExaminationDate)}
              </Badge>
            ))}
            {withRestrictions.map((s) => (
              <Badge key={`res-${s.id}`} variant="outline" className="border-destructive/50">
                {s.employeeName} · restricted · {s.typeName}
              </Badge>
            ))}
          </CardContent>
        </Card>
      )}

      {(stationsDue.length > 0 || underStocked.length > 0) && (
        <Card className="border-destructive/50">
          <CardHeader>
            <CardTitle className="text-base text-destructive">First-aid station chase list</CardTitle>
            <CardDescription>
              Inspections due within 30 days, and stations reporting stock deficiencies.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            {stationsDue.map((s) => (
              <Badge key={`insp-${s.id}`} variant="outline" className="border-destructive/50">
                {s.stationCode} · {s.name} · inspect {fmtDate(s.nextInspectionDate)}
              </Badge>
            ))}
            {underStocked.map((s) => (
              <Badge key={`stock-${s.id}`} variant="destructive">
                {s.stationCode} · under-stocked
              </Badge>
            ))}
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="surveillance">
        <TabsList>
          <TabsTrigger value="surveillance">Health surveillance</TabsTrigger>
          <TabsTrigger value="stations">First-aid stations</TabsTrigger>
          <TabsTrigger value="wellness">Wellness programs</TabsTrigger>
        </TabsList>

        {/* ── Surveillance ── */}
        <TabsContent value="surveillance" className="mt-4">
          <ResourceListPanel<SheHealthSurveillanceSummary, SurveillanceForm>
            title="surveillance records"
            singular="surveillance record"
            queryKey={['hr', 'safety-occ-health', 'surveillance']}
            invalidateKeys={[
              ['hr', 'safety-occ-health', 'surveillance', 'due'],
              ['hr', 'safety-occ-health', 'surveillance', 'restrictions'],
            ]}
            list={() => safetyOccupationalHealthService.getSurveillance()}
            create={(values) => {
              const v = surveillanceSchema.parse(values);
              return safetyOccupationalHealthService.createSurveillance({
                surveillanceNumber: v.surveillanceNumber,
                employeeId: v.employeeId,
                type: v.type as SheHealthSurveillanceType,
                exposureHazard: blank(v.exposureHazard),
                examinationDate: new Date(v.examinationDate).toISOString(),
                nextExaminationDate: dateOrNull(v.nextExaminationDate),
                examiningPhysician: blank(v.examiningPhysician),
                result: v.result as SheHealthSurveillanceResult,
                findings: blank(v.findings),
                recommendations: blank(v.recommendations),
                workRestrictionIssued: v.workRestrictionIssued,
                workRestrictionDetails: blank(v.workRestrictionDetails),
                recordedById: v.recordedById,
              });
            }}
            update={() => Promise.reject(new Error('Edited through the full-record dialog.'))}
            allowUpdate={false}
            remove={(id) => safetyOccupationalHealthService.removeSurveillance(id)}
            actions={[
              {
                label: 'Edit…',
                run: (row) => openSurveillanceEdit(row),
              },
            ]}
            columns={[
              {
                header: 'Number',
                cell: (s) => <span className="font-mono">{s.surveillanceNumber}</span>,
              },
              { header: 'Employee', cell: (s) => <span className="font-medium">{s.employeeName}</span> },
              { header: 'Type', cell: (s) => s.typeName },
              { header: 'Examined', cell: (s) => fmtDate(s.examinationDate) },
              {
                header: 'Next due',
                cell: (s) =>
                  s.nextExaminationDate && new Date(s.nextExaminationDate) < new Date() ? (
                    <Badge variant="destructive">Overdue {fmtDate(s.nextExaminationDate)}</Badge>
                  ) : (
                    fmtDate(s.nextExaminationDate)
                  ),
              },
              {
                header: 'Result',
                cell: (s) => <Badge variant={resultVariant(s.result)}>{s.resultName}</Badge>,
              },
              {
                header: 'Restriction',
                cell: (s) =>
                  s.workRestrictionIssued ? (
                    <Badge variant="destructive">Restricted</Badge>
                  ) : (
                    <span className="text-muted-foreground text-sm">None</span>
                  ),
              },
            ]}
            schema={surveillanceSchema}
            emptyForm={{
              surveillanceNumber: '',
              employeeId: '',
              type: 'PeriodicMedical',
              exposureHazard: '',
              examinationDate: new Date().toISOString().slice(0, 10),
              nextExaminationDate: '',
              examiningPhysician: '',
              result: 'Normal',
              findings: '',
              recommendations: '',
              workRestrictionIssued: false,
              workRestrictionDetails: '',
              recordedById: '',
            }}
            toForm={(s) => ({
              surveillanceNumber: s.surveillanceNumber,
              employeeId: '',
              type: s.type,
              exposureHazard: '',
              examinationDate: s.examinationDate.slice(0, 10),
              nextExaminationDate: s.nextExaminationDate?.slice(0, 10) ?? '',
              examiningPhysician: '',
              result: s.result,
              findings: '',
              recommendations: '',
              workRestrictionIssued: s.workRestrictionIssued,
              workRestrictionDetails: '',
              recordedById: '',
            })}
            renderFields={(f) => (
              <>
                <FieldRow>
                  <TextField
                    form={f}
                    name="surveillanceNumber"
                    label="Surveillance number (unique)"
                    required
                  />
                  <SelectField
                    form={f}
                    name="type"
                    label="Surveillance type"
                    required
                    options={SHE_HEALTH_SURVEILLANCE_TYPE_OPTIONS}
                  />
                </FieldRow>
                <FieldRow>
                  <EmployeePickerField form={f} name="employeeId" label="Employee" required />
                  <EmployeePickerField form={f} name="recordedById" label="Recorded by" required />
                </FieldRow>
                <FieldRow>
                  <DateField form={f} name="examinationDate" label="Examination date" required />
                  <DateField form={f} name="nextExaminationDate" label="Next examination due" />
                </FieldRow>
                <FieldRow>
                  <TextField form={f} name="exposureHazard" label="Exposure hazard" />
                  <TextField form={f} name="examiningPhysician" label="Examining physician" />
                </FieldRow>
                <SelectField
                  form={f}
                  name="result"
                  label="Result"
                  required
                  options={SHE_HEALTH_SURVEILLANCE_RESULT_OPTIONS}
                />
                <TextareaField form={f} name="findings" label="Findings" rows={2} />
                <TextareaField form={f} name="recommendations" label="Recommendations" rows={2} />
                <SwitchField form={f} name="workRestrictionIssued" label="Work restriction issued" />
                <TextareaField
                  form={f}
                  name="workRestrictionDetails"
                  label="Restriction details"
                  rows={2}
                />
              </>
            )}
            getId={(s) => s.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Exposure-driven medical surveillance — audiometry, spirometry, periodic medicals and more."
          />
        </TabsContent>

        {/* ── First-aid stations ── */}
        <TabsContent value="stations" className="mt-4">
          <ResourceListPanel<SheFirstAidStation, StationForm>
            title="first-aid stations"
            singular="station"
            queryKey={['hr', 'safety-occ-health', 'stations']}
            invalidateKeys={[
              ['hr', 'safety-occ-health', 'stations', 'due'],
              ['hr', 'safety-occ-health', 'stations', 'under-stocked'],
            ]}
            list={() => safetyOccupationalHealthService.getFirstAidStations()}
            create={(values) => {
              const v = stationSchema.parse(values);
              return safetyOccupationalHealthService.createFirstAidStation({
                stationCode: v.stationCode,
                name: v.name,
                locationId: v.locationId,
                specificArea: blank(v.specificArea),
                type: v.type as SheFirstAidStationType,
                responsibleAiderId: blank(v.responsibleAiderId),
                isFullyStocked: v.isFullyStocked,
                isActive: v.isActive,
                notes: blank(v.notes),
              });
            }}
            update={(id, values) => {
              const v = stationSchema.parse(values);
              return safetyOccupationalHealthService.updateFirstAidStation(id, {
                id,
                name: v.name,
                locationId: v.locationId,
                specificArea: blank(v.specificArea),
                type: v.type as SheFirstAidStationType,
                responsibleAiderId: blank(v.responsibleAiderId),
                lastInspectionDate: dateOrNull(v.lastInspectionDate),
                nextInspectionDate: dateOrNull(v.nextInspectionDate),
                isFullyStocked: v.isFullyStocked,
                stockingDeficiencies: blank(v.stockingDeficiencies),
                isActive: v.isActive,
                notes: blank(v.notes),
              });
            }}
            remove={(id) => safetyOccupationalHealthService.removeFirstAidStation(id)}
            columns={[
              { header: 'Code', cell: (s) => <span className="font-mono">{s.stationCode}</span> },
              { header: 'Station', cell: (s) => <span className="font-medium">{s.name}</span> },
              {
                header: 'Where',
                cell: (s) => `${s.locationName}${s.specificArea ? ` · ${s.specificArea}` : ''}`,
              },
              { header: 'Type', cell: (s) => s.typeName },
              { header: 'Responsible aider', cell: (s) => s.responsibleAiderName ?? '—' },
              {
                header: 'Next inspection',
                cell: (s) =>
                  s.nextInspectionDate && new Date(s.nextInspectionDate) < new Date() ? (
                    <Badge variant="destructive">Overdue {fmtDate(s.nextInspectionDate)}</Badge>
                  ) : (
                    fmtDate(s.nextInspectionDate)
                  ),
              },
              {
                header: 'Stock',
                cell: (s) =>
                  s.isFullyStocked ? (
                    <Badge variant="secondary">Stocked</Badge>
                  ) : (
                    <Badge variant="destructive">Deficient</Badge>
                  ),
              },
              {
                header: 'Status',
                cell: (s) => <StatusBadge status={s.isActive ? 'Active' : 'Inactive'} />,
              },
            ]}
            schema={stationSchema}
            emptyForm={{
              stationCode: '',
              name: '',
              locationId: '',
              specificArea: '',
              type: 'FullKit',
              responsibleAiderId: '',
              lastInspectionDate: '',
              nextInspectionDate: '',
              isFullyStocked: true,
              stockingDeficiencies: '',
              isActive: true,
              notes: '',
            }}
            toForm={(s) => ({
              stationCode: s.stationCode,
              name: s.name,
              locationId: s.locationId,
              specificArea: s.specificArea ?? '',
              type: s.type,
              responsibleAiderId: s.responsibleAiderId ?? '',
              lastInspectionDate: s.lastInspectionDate?.slice(0, 10) ?? '',
              nextInspectionDate: s.nextInspectionDate?.slice(0, 10) ?? '',
              isFullyStocked: s.isFullyStocked,
              stockingDeficiencies: s.stockingDeficiencies ?? '',
              isActive: s.isActive,
              notes: s.notes ?? '',
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  {!editing ? (
                    <TextField form={f} name="stationCode" label="Station code (unique)" required />
                  ) : (
                    <div className="text-muted-foreground self-end pb-2 text-sm">
                      <span className="font-mono">{f.getValues('stationCode')}</span> — fixed at
                      creation.
                    </div>
                  )}
                  <TextField form={f} name="name" label="Station name" required />
                </FieldRow>
                <FieldRow>
                  <SelectField
                    form={f}
                    name="locationId"
                    label="Location"
                    required
                    options={locationOptions}
                  />
                  <TextField form={f} name="specificArea" label="Specific area" />
                </FieldRow>
                <FieldRow>
                  <SelectField
                    form={f}
                    name="type"
                    label="Station type"
                    required
                    options={SHE_FIRST_AID_STATION_TYPE_OPTIONS}
                  />
                  <EmployeePickerField form={f} name="responsibleAiderId" label="Responsible aider" />
                </FieldRow>
                {editing && (
                  <FieldRow>
                    <DateField form={f} name="lastInspectionDate" label="Last inspected" />
                    <DateField form={f} name="nextInspectionDate" label="Next inspection due" />
                  </FieldRow>
                )}
                <SwitchField form={f} name="isFullyStocked" label="Fully stocked" />
                {editing && (
                  <TextareaField
                    form={f}
                    name="stockingDeficiencies"
                    label="Stocking deficiencies"
                    rows={2}
                  />
                )}
                <TextareaField form={f} name="notes" label="Notes" rows={2} />
                <SwitchField form={f} name="isActive" label="Active" />
              </>
            )}
            getId={(s) => s.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Kits, AEDs and medical rooms with their inspection cadence and responsible first aiders."
          />
        </TabsContent>

        {/* ── Wellness programs ── */}
        <TabsContent value="wellness" className="mt-4">
          <ResourceListPanel<SheWellnessProgram, ProgramForm>
            title="wellness programs"
            singular="program"
            queryKey={['hr', 'safety-occ-health', 'wellness']}
            list={() => safetyOccupationalHealthService.getWellnessPrograms()}
            create={(values) => {
              const v = programSchema.parse(values);
              return safetyOccupationalHealthService.createWellnessProgram({
                programCode: v.programCode,
                title: v.title,
                description: blank(v.description),
                type: v.type as SheWellnessProgramType,
                startDate: new Date(v.startDate).toISOString(),
                endDate: dateOrNull(v.endDate),
                coordinatorId: blank(v.coordinatorId),
                isActive: v.isActive,
              });
            }}
            update={(id, values) => {
              const v = programSchema.parse(values);
              return safetyOccupationalHealthService.updateWellnessProgram(id, {
                id,
                title: v.title,
                description: blank(v.description),
                type: v.type as SheWellnessProgramType,
                startDate: new Date(v.startDate).toISOString(),
                endDate: dateOrNull(v.endDate),
                coordinatorId: blank(v.coordinatorId),
                status: v.status as SheWellnessProgramStatus,
                participantsCount: v.participantsCount ?? null,
                outcomes: blank(v.outcomes),
                isActive: v.isActive,
              });
            }}
            remove={(id) => safetyOccupationalHealthService.removeWellnessProgram(id)}
            columns={[
              { header: 'Code', cell: (p) => <span className="font-mono">{p.programCode}</span> },
              { header: 'Program', cell: (p) => <span className="font-medium">{p.title}</span> },
              { header: 'Type', cell: (p) => p.typeName },
              {
                header: 'Runs',
                cell: (p) => `${fmtDate(p.startDate)} – ${fmtDate(p.endDate)}`,
              },
              { header: 'Coordinator', cell: (p) => p.coordinatorName ?? '—' },
              { header: 'Participants', cell: (p) => p.participantsCount ?? '—' },
              { header: 'Status', cell: (p) => <StatusBadge status={p.status} /> },
            ]}
            schema={programSchema}
            emptyForm={{
              programCode: '',
              title: '',
              description: '',
              type: 'HealthScreening',
              startDate: new Date().toISOString().slice(0, 10),
              endDate: '',
              coordinatorId: '',
              status: 'Planned',
              participantsCount: undefined,
              outcomes: '',
              isActive: true,
            }}
            toForm={(p) => ({
              programCode: p.programCode,
              title: p.title,
              description: p.description ?? '',
              type: p.type,
              startDate: p.startDate.slice(0, 10),
              endDate: p.endDate?.slice(0, 10) ?? '',
              coordinatorId: p.coordinatorId ?? '',
              status: p.status,
              participantsCount: p.participantsCount ?? undefined,
              outcomes: p.outcomes ?? '',
              isActive: p.isActive,
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  {!editing ? (
                    <TextField form={f} name="programCode" label="Program code (unique)" required />
                  ) : (
                    <div className="text-muted-foreground self-end pb-2 text-sm">
                      <span className="font-mono">{f.getValues('programCode')}</span> — fixed at
                      creation.
                    </div>
                  )}
                  <TextField form={f} name="title" label="Title" required />
                </FieldRow>
                <TextareaField form={f} name="description" label="Description" rows={2} />
                <FieldRow>
                  <SelectField
                    form={f}
                    name="type"
                    label="Program type"
                    required
                    options={SHE_WELLNESS_PROGRAM_TYPE_OPTIONS}
                  />
                  <EmployeePickerField form={f} name="coordinatorId" label="Coordinator" />
                </FieldRow>
                <FieldRow>
                  <DateField form={f} name="startDate" label="Starts" required />
                  <DateField form={f} name="endDate" label="Ends" />
                </FieldRow>
                {editing && (
                  <>
                    <FieldRow>
                      <SelectField
                        form={f}
                        name="status"
                        label="Status"
                        required
                        options={SHE_WELLNESS_PROGRAM_STATUS_OPTIONS}
                      />
                      <NumberField form={f} name="participantsCount" label="Participants" />
                    </FieldRow>
                    <TextareaField form={f} name="outcomes" label="Outcomes" rows={2} />
                  </>
                )}
                <SwitchField form={f} name="isActive" label="Active" />
              </>
            )}
            getId={(p) => p.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Screenings, awareness campaigns and other wellbeing programs, with outcomes captured on completion."
          />
        </TabsContent>
      </Tabs>

      {/* ── Surveillance edit dialog (full record — the table rows are summaries) ── */}
      <Dialog
        open={editingSurveillance !== null}
        onOpenChange={(o) => !busy && !o && setEditingSurveillance(null)}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[640px]">
          <DialogHeader>
            <DialogTitle>Edit surveillance record</DialogTitle>
            <DialogDescription>
              <span className="font-mono">{editingSurveillance?.number}</span> — the number,
              employee and recorder are fixed at creation.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitSurveillanceEdit} className="space-y-4">
            <FieldRow>
              <SelectField
                form={editForm}
                name="type"
                label="Surveillance type"
                required
                options={SHE_HEALTH_SURVEILLANCE_TYPE_OPTIONS}
              />
              <SelectField
                form={editForm}
                name="result"
                label="Result"
                required
                options={SHE_HEALTH_SURVEILLANCE_RESULT_OPTIONS}
              />
            </FieldRow>
            <FieldRow>
              <DateField form={editForm} name="examinationDate" label="Examination date" required />
              <DateField form={editForm} name="nextExaminationDate" label="Next examination due" />
            </FieldRow>
            <FieldRow>
              <TextField form={editForm} name="exposureHazard" label="Exposure hazard" />
              <TextField form={editForm} name="examiningPhysician" label="Examining physician" />
            </FieldRow>
            <TextareaField form={editForm} name="findings" label="Findings" rows={2} />
            <TextareaField form={editForm} name="recommendations" label="Recommendations" rows={2} />
            <SwitchField
              form={editForm}
              name="workRestrictionIssued"
              label="Work restriction issued"
            />
            <TextareaField
              form={editForm}
              name="workRestrictionDetails"
              label="Restriction details"
              rows={2}
            />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setEditingSurveillance(null)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
