'use client';

import { useState, type ReactNode } from 'react';
import { useParams } from 'next/navigation';
import { z } from 'zod';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
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
import { safetyEquipmentService } from '@/services/hr/safety-equipment.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { locationService } from '@/services/hr/location.service';
import { SHE_CORRECTIVE_ACTION_STATUS_OPTIONS } from '@/types/hr/safety-incidents';
import { SHE_INSPECTION_TYPE_OPTIONS } from '@/types/hr/safety-inspections';
import {
  SHE_SAFETY_EQUIPMENT_STATUS_OPTIONS,
  SHE_SAFETY_EQUIPMENT_TYPE_OPTIONS,
  SHE_INSPECTION_RESULT_OPTIONS,
} from '@/types/hr/safety-equipment';
import type {
  SafetyEquipmentInspection,
  SafetyEquipmentInspectionAction,
  SafetyEquipmentMaintenance,
  SheInspectionResult,
  SheSafetyEquipmentStatus,
  SheSafetyEquipmentType,
} from '@/types/hr/safety-equipment';
import type { SheInspectionType } from '@/types/hr/safety-inspections';
import type { SheCorrectiveActionStatus } from '@/types/hr/safety-incidents';

/**
 * Equipment detail: identity + schedule dates, an edit dialog (the only place status changes,
 * including out-of-service with its reason), the inspection history with corrective actions
 * (flattened across inspections, the slice-4 shape), and maintenance records. Due dates shown
 * here are checked manually — no reminder fires until the slice-13 job engine.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const dateOrNull = (v?: string) => (v && v.length > 0 ? new Date(v).toISOString() : null);

const editSchema = z.object({
  name: z.string().min(1).max(200),
  description: z.string().max(500).optional().or(z.literal('')),
  type: z.string().min(1),
  locationId: z.string().min(1, 'A location is required'),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  manufacturer: z.string().max(100).optional().or(z.literal('')),
  model: z.string().max(100).optional().or(z.literal('')),
  serialNumber: z.string().max(100).optional().or(z.literal('')),
  requiresRegularInspection: z.boolean(),
  inspectionFrequencyDays: z.coerce.number().min(0).max(3650),
  requiresCertification: z.boolean(),
  certificationExpiryDate: z.string().optional().or(z.literal('')),
  status: z.string().min(1),
  outOfServiceDate: z.string().optional().or(z.literal('')),
  outOfServiceReason: z.string().max(500).optional().or(z.literal('')),
  expiryDate: z.string().optional().or(z.literal('')),
  nextMaintenanceDueDate: z.string().optional().or(z.literal('')),
  responsiblePersonId: z.string().optional().or(z.literal('')),
  notes: z.string().max(500).optional().or(z.literal('')),
});

type EditForm = z.input<typeof editSchema>;

const inspectionSchema = z.object({
  inspectionDate: z.string().min(1, 'A date is required'),
  inspectionType: z.string().min(1),
  inspectedById: z.string().min(1, 'An inspector is required'),
  result: z.string().min(1),
  findings: z.string().max(2000).optional().or(z.literal('')),
  deficienciesNoted: z.string().max(2000).optional().or(z.literal('')),
  nextInspectionDate: z.string().optional().or(z.literal('')),
});

type InspectionForm = z.input<typeof inspectionSchema>;

const emptyInspection: InspectionForm = {
  inspectionDate: new Date().toISOString().slice(0, 10),
  inspectionType: 'Routine',
  inspectedById: '',
  result: 'Pass',
  findings: '',
  deficienciesNoted: '',
  nextInspectionDate: '',
};

const actionSchema = z.object({
  safetyEquipmentInspectionId: z.string().min(1, 'An inspection is required'),
  correctiveActionTemplateId: z.string().min(1, 'A template is required'),
  status: z.string().min(1),
  dueDate: z.string().optional().or(z.literal('')),
  completionDate: z.string().optional().or(z.literal('')),
  completionNotes: z.string().max(500).optional().or(z.literal('')),
  assignedToId: z.string().optional().or(z.literal('')),
});

type ActionForm = z.input<typeof actionSchema>;

const emptyAction: ActionForm = {
  safetyEquipmentInspectionId: '',
  correctiveActionTemplateId: '',
  status: 'Pending',
  dueDate: '',
  completionDate: '',
  completionNotes: '',
  assignedToId: '',
};

const maintenanceSchema = z.object({
  maintenanceDate: z.string().min(1, 'A date is required'),
  maintenanceType: z.string().max(100).optional().or(z.literal('')),
  description: z.string().max(1000).optional().or(z.literal('')),
  equipmentTakenOutOfService: z.boolean(),
  outOfServiceStart: z.string().optional().or(z.literal('')),
  outOfServiceEnd: z.string().optional().or(z.literal('')),
  cost: z.coerce.number().min(0).optional(),
  performedBy: z.string().max(200).optional().or(z.literal('')),
  notes: z.string().max(500).optional().or(z.literal('')),
});

type MaintenanceForm = z.input<typeof maintenanceSchema>;

const emptyMaintenance: MaintenanceForm = {
  maintenanceDate: new Date().toISOString().slice(0, 10),
  maintenanceType: '',
  description: '',
  equipmentTakenOutOfService: false,
  outOfServiceStart: '',
  outOfServiceEnd: '',
  cost: undefined,
  performedBy: '',
  notes: '',
};

type FlatAction = SafetyEquipmentInspectionAction & { inspectionLabel: string };

function Fact({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div>
      <div className="text-muted-foreground text-xs">{label}</div>
      <div className="text-sm font-medium">{value ?? '—'}</div>
    </div>
  );
}

export default function SafetyEquipmentDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [editOpen, setEditOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  const { data: equipment } = useQuery({
    queryKey: ['hr', 'safety-equipment', id],
    queryFn: () => safetyEquipmentService.getById(id),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });
  const { data: caTemplates = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'corrective-action-templates', 'active'],
    queryFn: () => safetyReferenceService.getCorrectiveActionTemplates(true),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) });

  const openEdit = () => {
    if (!equipment) return;
    editForm.reset({
      name: equipment.name,
      description: equipment.description ?? '',
      type: equipment.type,
      locationId: equipment.locationId,
      specificArea: equipment.specificArea ?? '',
      manufacturer: equipment.manufacturer ?? '',
      model: equipment.model ?? '',
      serialNumber: equipment.serialNumber ?? '',
      requiresRegularInspection: equipment.requiresRegularInspection,
      inspectionFrequencyDays: equipment.inspectionFrequencyDays,
      requiresCertification: equipment.requiresCertification,
      certificationExpiryDate: equipment.certificationExpiryDate?.slice(0, 10) ?? '',
      status: equipment.status,
      outOfServiceDate: equipment.outOfServiceDate?.slice(0, 10) ?? '',
      outOfServiceReason: equipment.outOfServiceReason ?? '',
      expiryDate: equipment.expiryDate?.slice(0, 10) ?? '',
      nextMaintenanceDueDate: equipment.nextMaintenanceDueDate?.slice(0, 10) ?? '',
      responsiblePersonId: equipment.responsiblePersonId ?? '',
      notes: equipment.notes ?? '',
    });
    setEditOpen(true);
  };

  const submitEdit = editForm.handleSubmit(async (values) => {
    if (!equipment) return;
    setBusy(true);
    try {
      const v = editSchema.parse(values);
      await safetyEquipmentService.update(id, {
        id,
        name: v.name,
        description: blank(v.description),
        type: v.type as SheSafetyEquipmentType,
        locationId: v.locationId,
        specificArea: blank(v.specificArea),
        manufacturer: blank(v.manufacturer),
        model: blank(v.model),
        serialNumber: blank(v.serialNumber),
        purchaseDate: equipment.purchaseDate,
        installationDate: equipment.installationDate,
        requiresRegularInspection: v.requiresRegularInspection,
        inspectionFrequencyDays: v.inspectionFrequencyDays,
        lastInspectionDate: equipment.lastInspectionDate,
        nextInspectionDueDate: equipment.nextInspectionDueDate,
        requiresCertification: v.requiresCertification,
        certificationExpiryDate: v.requiresCertification
          ? dateOrNull(v.certificationExpiryDate)
          : null,
        certificationDocumentPath: equipment.certificationDocumentPath,
        status: v.status as SheSafetyEquipmentStatus,
        outOfServiceDate: v.status === 'OutOfService' ? dateOrNull(v.outOfServiceDate) : null,
        outOfServiceReason: v.status === 'OutOfService' ? blank(v.outOfServiceReason) : null,
        expiryDate: dateOrNull(v.expiryDate),
        lastMaintenanceDate: equipment.lastMaintenanceDate,
        nextMaintenanceDueDate: dateOrNull(v.nextMaintenanceDueDate),
        responsiblePersonId: blank(v.responsiblePersonId),
        notes: blank(v.notes),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-equipment'] });
      toast({ title: 'Equipment updated' });
      setEditOpen(false);
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

  if (!equipment) return null;

  const editStatus = editForm.watch('status');
  const editRequiresCert = !!editForm.watch('requiresCertification');
  const inspectionLabel = (i: SafetyEquipmentInspection) =>
    `${fmtDate(i.inspectionDate)} — ${i.inspectionTypeName} (${i.resultName})`;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${equipment.equipmentNumber} — ${equipment.name}`}
        description={
          SHE_SAFETY_EQUIPMENT_TYPE_OPTIONS.find((o) => o.value === equipment.type)?.label ??
          equipment.typeName
        }
        backHref="/hr/safety/equipment"
        actions={
          <Button variant="outline" onClick={openEdit}>
            <Pencil className="mr-2 h-4 w-4" /> Edit
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-3 text-base">
            <StatusBadge status={equipment.statusName} />
            {equipment.status === 'OutOfService' && equipment.outOfServiceReason && (
              <span className="text-muted-foreground text-sm font-normal">
                {equipment.outOfServiceReason}
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="grid grid-cols-2 gap-4 md:grid-cols-4">
          <Fact
            label="Location"
            value={`${equipment.locationName}${equipment.specificArea ? ` · ${equipment.specificArea}` : ''}`}
          />
          <Fact label="Organization unit" value={equipment.organizationUnitName} />
          <Fact
            label="Make / model"
            value={
              equipment.manufacturer || equipment.model
                ? `${equipment.manufacturer ?? ''} ${equipment.model ?? ''}`.trim()
                : null
            }
          />
          <Fact label="Serial number" value={equipment.serialNumber} />
          <Fact label="Responsible person" value={equipment.responsiblePersonName} />
          <Fact
            label="Last inspection"
            value={fmtDate(equipment.lastInspectionDate)}
          />
          <Fact
            label="Next inspection due"
            value={
              equipment.nextInspectionDueDate &&
              new Date(equipment.nextInspectionDueDate) < new Date() ? (
                <Badge variant="destructive">Overdue {fmtDate(equipment.nextInspectionDueDate)}</Badge>
              ) : (
                fmtDate(equipment.nextInspectionDueDate)
              )
            }
          />
          <Fact
            label="Certification expiry"
            value={
              equipment.requiresCertification ? (
                equipment.certificationExpiryDate &&
                new Date(equipment.certificationExpiryDate) < new Date() ? (
                  <Badge variant="destructive">
                    Expired {fmtDate(equipment.certificationExpiryDate)}
                  </Badge>
                ) : (
                  fmtDate(equipment.certificationExpiryDate)
                )
              ) : (
                'Not required'
              )
            }
          />
          <Fact label="Last maintenance" value={fmtDate(equipment.lastMaintenanceDate)} />
          <Fact label="Next maintenance due" value={fmtDate(equipment.nextMaintenanceDueDate)} />
          <Fact label="Equipment expiry" value={fmtDate(equipment.expiryDate)} />
          <Fact label="Installed" value={fmtDate(equipment.installationDate)} />
        </CardContent>
      </Card>

      <Tabs defaultValue="inspections">
        <TabsList>
          <TabsTrigger value="inspections">
            Inspections ({equipment.inspections.length})
          </TabsTrigger>
          <TabsTrigger value="actions">
            Corrective actions ({equipment.inspections.reduce((n, i) => n + i.inspectionActions.length, 0)})
          </TabsTrigger>
          <TabsTrigger value="maintenance">
            Maintenance ({equipment.maintenanceRecords.length})
          </TabsTrigger>
        </TabsList>

        {/* ── Inspections ── */}
        <TabsContent value="inspections" className="mt-4">
          <ResourceCollectionTab<SafetyEquipmentInspection, InspectionForm>
            parentId={id}
            title="inspections"
            singular="inspection"
            queryKey={['hr', 'safety-equipment', id, 'inspections']}
            invalidateKeys={[['hr', 'safety-equipment']]}
            list={() => safetyEquipmentService.getInspections(id)}
            create={(equipmentId, values) => {
              const v = inspectionSchema.parse(values);
              return safetyEquipmentService.addInspection(equipmentId, {
                equipmentId,
                inspectionDate: new Date(v.inspectionDate).toISOString(),
                inspectionType: v.inspectionType as SheInspectionType,
                inspectedById: v.inspectedById,
                result: v.result as SheInspectionResult,
                findings: blank(v.findings),
                deficienciesNoted: blank(v.deficienciesNoted),
                nextInspectionDate: dateOrNull(v.nextInspectionDate),
              });
            }}
            update={(_equipmentId, inspectionId, values) => {
              const v = inspectionSchema.parse(values);
              return safetyEquipmentService.updateInspection(inspectionId, {
                id: inspectionId,
                inspectionDate: new Date(v.inspectionDate).toISOString(),
                inspectionType: v.inspectionType as SheInspectionType,
                result: v.result as SheInspectionResult,
                findings: blank(v.findings),
                deficienciesNoted: blank(v.deficienciesNoted),
                nextInspectionDate: dateOrNull(v.nextInspectionDate),
              });
            }}
            columns={[
              { header: 'Date', cell: (i) => fmtDate(i.inspectionDate) },
              { header: 'Type', cell: (i) => i.inspectionTypeName },
              { header: 'Inspector', cell: (i) => i.inspectedByName },
              { header: 'Result', cell: (i) => <StatusBadge status={i.resultName} /> },
              { header: 'Next due', cell: (i) => fmtDate(i.nextInspectionDate) },
              {
                header: 'Actions',
                cell: (i) => (
                  <Badge variant={i.inspectionActions.length > 0 ? 'secondary' : 'outline'}>
                    {i.inspectionActions.length}
                  </Badge>
                ),
              },
            ]}
            schema={inspectionSchema}
            emptyForm={emptyInspection}
            toForm={(i) => ({
              inspectionDate: i.inspectionDate.slice(0, 10),
              inspectionType: i.inspectionType,
              inspectedById: i.inspectedById,
              result: i.result,
              findings: i.findings ?? '',
              deficienciesNoted: i.deficienciesNoted ?? '',
              nextInspectionDate: i.nextInspectionDate?.slice(0, 10) ?? '',
            })}
            renderFields={(f, editing) => (
              <>
                <FieldRow>
                  <DateField form={f} name="inspectionDate" label="Inspection date" required />
                  <SelectField
                    form={f}
                    name="inspectionType"
                    label="Type"
                    required
                    options={SHE_INSPECTION_TYPE_OPTIONS}
                  />
                </FieldRow>
                <FieldRow>
                  {!editing ? (
                    <EmployeePickerField form={f} name="inspectedById" label="Inspected by" />
                  ) : (
                    <div />
                  )}
                  <SelectField
                    form={f}
                    name="result"
                    label="Result"
                    required
                    options={SHE_INSPECTION_RESULT_OPTIONS}
                  />
                </FieldRow>
                <TextareaField form={f} name="findings" label="Findings" rows={2} />
                <TextareaField form={f} name="deficienciesNoted" label="Deficiencies noted" rows={2} />
                <DateField form={f} name="nextInspectionDate" label="Next inspection date" />
              </>
            )}
            getId={(i) => i.id}
            dialogClassName="sm:max-w-[600px]"
            emptyDescription="No inspections recorded. Recording one also updates the equipment's last/next inspection dates."
          />
        </TabsContent>

        {/* ── Corrective actions (flattened across inspections) ── */}
        <TabsContent value="actions" className="mt-4">
          <ResourceCollectionTab<FlatAction, ActionForm>
            parentId={id}
            title="corrective actions"
            singular="corrective action"
            queryKey={['hr', 'safety-equipment', id, 'actions']}
            invalidateKeys={[['hr', 'safety-equipment']]}
            list={async () => {
              const full = await safetyEquipmentService.getById(id);
              return full.inspections.flatMap((i) =>
                i.inspectionActions.map((a) => ({ ...a, inspectionLabel: inspectionLabel(i) })),
              );
            }}
            create={(_equipmentId, values) => {
              const v = actionSchema.parse(values);
              return safetyEquipmentService.addInspectionAction(v.safetyEquipmentInspectionId, {
                safetyEquipmentInspectionId: v.safetyEquipmentInspectionId,
                correctiveActionTemplateId: v.correctiveActionTemplateId,
                status: v.status as SheCorrectiveActionStatus,
                dueDate: dateOrNull(v.dueDate),
                assignedToId: blank(v.assignedToId),
              });
            }}
            update={(_equipmentId, actionId, values) => {
              const v = actionSchema.parse(values);
              return safetyEquipmentService.updateInspectionAction(actionId, {
                id: actionId,
                status: v.status as SheCorrectiveActionStatus,
                dueDate: dateOrNull(v.dueDate),
                completionDate: dateOrNull(v.completionDate),
                completionNotes: blank(v.completionNotes),
                assignedToId: blank(v.assignedToId),
              });
            }}
            remove={(_equipmentId, actionId) =>
              safetyEquipmentService.removeInspectionAction(actionId)
            }
            columns={[
              { header: 'Inspection', cell: (a) => a.inspectionLabel },
              { header: 'Action', cell: (a) => a.correctiveActionTemplateTitle },
              { header: 'Status', cell: (a) => <StatusBadge status={a.statusName} /> },
              { header: 'Assigned to', cell: (a) => a.assignedToName ?? '—' },
              { header: 'Due', cell: (a) => fmtDate(a.dueDate) },
              { header: 'Completed', cell: (a) => fmtDate(a.completionDate) },
            ]}
            schema={actionSchema}
            emptyForm={emptyAction}
            toForm={(a) => ({
              safetyEquipmentInspectionId: a.safetyEquipmentInspectionId,
              correctiveActionTemplateId: a.correctiveActionTemplateId,
              status: a.status,
              dueDate: a.dueDate?.slice(0, 10) ?? '',
              completionDate: a.completionDate?.slice(0, 10) ?? '',
              completionNotes: a.completionNotes ?? '',
              assignedToId: a.assignedToId ?? '',
            })}
            renderFields={(f, editing) => (
              <>
                {!editing && (
                  <SelectField
                    form={f}
                    name="safetyEquipmentInspectionId"
                    label="Inspection"
                    required
                    options={equipment.inspections.map((i) => ({
                      value: i.id,
                      label: inspectionLabel(i),
                    }))}
                  />
                )}
                {!editing && (
                  <SelectField
                    form={f}
                    name="correctiveActionTemplateId"
                    label="Template"
                    required
                    options={caTemplates.map((t) => ({ value: t.id, label: t.title }))}
                  />
                )}
                <FieldRow>
                  <SelectField
                    form={f}
                    name="status"
                    label="Status"
                    required
                    options={SHE_CORRECTIVE_ACTION_STATUS_OPTIONS}
                  />
                  <EmployeePickerField form={f} name="assignedToId" label="Assigned to" />
                </FieldRow>
                <FieldRow>
                  <DateField form={f} name="dueDate" label="Due date" />
                  {editing ? <DateField form={f} name="completionDate" label="Completed on" /> : <div />}
                </FieldRow>
                {editing && (
                  <TextareaField form={f} name="completionNotes" label="Completion notes" rows={2} />
                )}
              </>
            )}
            getId={(a) => a.id}
            dialogClassName="sm:max-w-[600px]"
            emptyDescription={
              equipment.inspections.length === 0
                ? 'Record an inspection first — corrective actions attach to inspections.'
                : 'Attach corrective actions to inspections that found deficiencies.'
            }
          />
        </TabsContent>

        {/* ── Maintenance ── */}
        <TabsContent value="maintenance" className="mt-4">
          <ResourceCollectionTab<SafetyEquipmentMaintenance, MaintenanceForm>
            parentId={id}
            title="maintenance records"
            singular="maintenance record"
            queryKey={['hr', 'safety-equipment', id, 'maintenance']}
            invalidateKeys={[['hr', 'safety-equipment']]}
            list={async () => {
              const full = await safetyEquipmentService.getById(id);
              return full.maintenanceRecords;
            }}
            create={(equipmentId, values) => {
              const v = maintenanceSchema.parse(values);
              return safetyEquipmentService.addMaintenance(equipmentId, {
                equipmentId,
                maintenanceDate: new Date(v.maintenanceDate).toISOString(),
                maintenanceType: blank(v.maintenanceType),
                description: blank(v.description),
                equipmentTakenOutOfService: v.equipmentTakenOutOfService,
                outOfServiceStart: v.equipmentTakenOutOfService
                  ? dateOrNull(v.outOfServiceStart)
                  : null,
                outOfServiceEnd: v.equipmentTakenOutOfService ? dateOrNull(v.outOfServiceEnd) : null,
                cost: v.cost ?? null,
                performedBy: blank(v.performedBy),
                notes: blank(v.notes),
              });
            }}
            update={(_equipmentId, maintenanceId, values) => {
              const v = maintenanceSchema.parse(values);
              return safetyEquipmentService.updateMaintenance(maintenanceId, {
                id: maintenanceId,
                maintenanceDate: new Date(v.maintenanceDate).toISOString(),
                maintenanceType: blank(v.maintenanceType),
                description: blank(v.description),
                equipmentTakenOutOfService: v.equipmentTakenOutOfService,
                outOfServiceStart: v.equipmentTakenOutOfService
                  ? dateOrNull(v.outOfServiceStart)
                  : null,
                outOfServiceEnd: v.equipmentTakenOutOfService ? dateOrNull(v.outOfServiceEnd) : null,
                cost: v.cost ?? null,
                performedBy: blank(v.performedBy),
                notes: blank(v.notes),
              });
            }}
            remove={(_equipmentId, maintenanceId) =>
              safetyEquipmentService.removeMaintenance(maintenanceId)
            }
            columns={[
              { header: 'Date', cell: (m) => fmtDate(m.maintenanceDate) },
              { header: 'Type', cell: (m) => m.maintenanceType ?? '—' },
              { header: 'Performed by', cell: (m) => m.performedBy ?? '—' },
              {
                header: 'Out of service',
                cell: (m) =>
                  m.equipmentTakenOutOfService
                    ? `${fmtDate(m.outOfServiceStart)} → ${fmtDate(m.outOfServiceEnd)}`
                    : '—',
              },
              { header: 'Cost', cell: (m) => (m.cost != null ? m.cost.toFixed(2) : '—') },
            ]}
            schema={maintenanceSchema}
            emptyForm={emptyMaintenance}
            toForm={(m) => ({
              maintenanceDate: m.maintenanceDate.slice(0, 10),
              maintenanceType: m.maintenanceType ?? '',
              description: m.description ?? '',
              equipmentTakenOutOfService: m.equipmentTakenOutOfService,
              outOfServiceStart: m.outOfServiceStart?.slice(0, 10) ?? '',
              outOfServiceEnd: m.outOfServiceEnd?.slice(0, 10) ?? '',
              cost: m.cost ?? undefined,
              performedBy: m.performedBy ?? '',
              notes: m.notes ?? '',
            })}
            renderFields={(f) => {
              const oos = !!f.watch('equipmentTakenOutOfService');
              return (
                <>
                  <FieldRow>
                    <DateField form={f} name="maintenanceDate" label="Maintenance date" required />
                    <TextField form={f} name="maintenanceType" label="Type (e.g. Recharge, Service)" />
                  </FieldRow>
                  <TextareaField form={f} name="description" label="Description" rows={2} />
                  <SwitchField
                    form={f}
                    name="equipmentTakenOutOfService"
                    label="Equipment taken out of service"
                  />
                  {oos && (
                    <FieldRow>
                      <DateField form={f} name="outOfServiceStart" label="Out of service from" />
                      <DateField form={f} name="outOfServiceEnd" label="Back in service" />
                    </FieldRow>
                  )}
                  <FieldRow>
                    <TextField form={f} name="performedBy" label="Performed by (person or firm)" />
                    <NumberField form={f} name="cost" label="Cost" />
                  </FieldRow>
                  <TextareaField form={f} name="notes" label="Notes" rows={2} />
                </>
              );
            }}
            getId={(m) => m.id}
            dialogClassName="sm:max-w-[600px]"
            emptyDescription="No maintenance recorded. Recording one also updates the equipment's last-maintenance date."
          />
        </TabsContent>
      </Tabs>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={(o) => !busy && setEditOpen(o)}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit equipment</DialogTitle>
            <DialogDescription>
              {equipment.equipmentNumber} — the number itself cannot change.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEdit} className="space-y-4">
            <FieldRow>
              <TextField form={editForm} name="name" label="Name" required />
              <SelectField
                form={editForm}
                name="type"
                label="Type"
                required
                options={SHE_SAFETY_EQUIPMENT_TYPE_OPTIONS}
              />
            </FieldRow>
            <TextareaField form={editForm} name="description" label="Description" rows={2} />
            <FieldRow>
              <SelectField
                form={editForm}
                name="locationId"
                label="Location"
                required
                options={locations.map((l) => ({ value: l.id, label: l.name }))}
              />
              <TextField form={editForm} name="specificArea" label="Specific area" />
            </FieldRow>
            <FieldRow>
              <TextField form={editForm} name="manufacturer" label="Manufacturer" />
              <TextField form={editForm} name="model" label="Model" />
            </FieldRow>
            <FieldRow>
              <TextField form={editForm} name="serialNumber" label="Serial number" />
              <EmployeePickerField form={editForm} name="responsiblePersonId" label="Responsible person" />
            </FieldRow>
            <SwitchField
              form={editForm}
              name="requiresRegularInspection"
              label="Requires regular inspection"
            />
            <FieldRow>
              <NumberField
                form={editForm}
                name="inspectionFrequencyDays"
                label="Inspection frequency (days)"
              />
              <DateField form={editForm} name="nextMaintenanceDueDate" label="Next maintenance due" />
            </FieldRow>
            <SwitchField form={editForm} name="requiresCertification" label="Requires certification" />
            {editRequiresCert && (
              <DateField form={editForm} name="certificationExpiryDate" label="Certification expiry" />
            )}
            <FieldRow>
              <SelectField
                form={editForm}
                name="status"
                label="Status"
                required
                options={SHE_SAFETY_EQUIPMENT_STATUS_OPTIONS}
              />
              <DateField form={editForm} name="expiryDate" label="Equipment expiry" />
            </FieldRow>
            {editStatus === 'OutOfService' && (
              <FieldRow>
                <DateField form={editForm} name="outOfServiceDate" label="Out of service since" />
                <TextField form={editForm} name="outOfServiceReason" label="Reason" />
              </FieldRow>
            )}
            <TextareaField form={editForm} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button type="button" variant="outline" disabled={busy} onClick={() => setEditOpen(false)}>
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
