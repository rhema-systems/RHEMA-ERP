'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Pencil, Trash2, Lock, CheckCircle2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { RiskBadge } from '@/components/hr/safety/RiskBadge';
import { safetyInspectionService } from '@/services/hr/safety-inspection.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { safetyChecklistService } from '@/services/hr/safety-checklist.service';
import { locationService } from '@/services/hr/location.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { SHE_RISK_LEVEL_OPTIONS, SHE_HAZARD_RISK_LEVEL_OPTIONS, SHE_HAZARD_STATUS_OPTIONS } from '@/types/hr/safety-hazards';
import { SHE_CORRECTIVE_ACTION_STATUS_OPTIONS } from '@/types/hr/safety-incidents';
import {
  SHE_INSPECTION_TYPE_OPTIONS,
  SHE_INSPECTION_CATEGORY_OPTIONS,
  SHE_INSPECTION_STATUS_OPTIONS,
  SHE_COMPLIANCE_STATUS_OPTIONS,
  type SafetyInspection,
  type SafetyInspectionItem,
  type SafetyInspectionHazard,
  type SafetyInspectionHazardAction,
  type SafetyInspectionDocument,
} from '@/types/hr/safety-inspections';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

function InfoRow({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
      <span className="text-muted-foreground shrink-0">{label}</span>
      <span className="text-right font-medium">{value ?? '—'}</span>
    </div>
  );
}

// ── Edit schema (header + outcome) ───────────────────────────────────────────

const editSchema = z.object({
  inspectionDate: z.string().min(1, 'A date is required'),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  type: z.enum(['Routine', 'Planned', 'Unplanned', 'FollowUp', 'PreTask', 'PostIncident', 'Regulatory', 'Management']),
  category: z.enum(['General', 'FireSafety', 'Construction', 'Equipment', 'Chemical', 'Electrical', 'WorkingAtHeight', 'ConfinedSpace', 'ManualHandling', 'Environmental', 'Housekeeping']),
  checklistId: z.string().optional().or(z.literal('')),
  inspectorId: z.string().min(1, 'Choose the inspector'),
  externalInspectorName: z.string().max(200).optional().or(z.literal('')),
  externalInspectorOrganization: z.string().max(200).optional().or(z.literal('')),
  findingsAndObservations: z.string().max(2000).optional().or(z.literal('')),
  recommendedActions: z.string().max(2000).optional().or(z.literal('')),
  positiveObservations: z.string().max(1000).optional().or(z.literal('')),
  status: z.enum(['Scheduled', 'InProgress', 'PendingCorrectiveActions', 'Completed', 'Closed', 'Overdue']),
  overallRiskRating: z.enum(['Negligible', 'Low', 'Medium', 'High', 'Critical']).optional().or(z.literal('')),
  complianceScore: z.coerce.number().min(0).max(100).optional(),
  complianceDeadline: z.string().optional().or(z.literal('')),
  nextInspectionDueDate: z.string().optional().or(z.literal('')),
});
type EditForm = z.input<typeof editSchema>;

// ── Close schema ─────────────────────────────────────────────────────────────

const closeSchema = z.object({
  closedById: z.string().min(1, 'Who is closing it?'),
  closedDate: z.string().min(1, 'A close date is required'),
});
type CloseForm = z.input<typeof closeSchema>;

// ── Finding (item) schema ────────────────────────────────────────────────────

const itemSchema = z.object({
  itemDescription: z.string().min(1, 'Describe the check').max(500),
  status: z.enum(['Compliant', 'NonCompliant', 'PartiallyCompliant', 'NotApplicable', 'NotAssessed']),
  deficiencyNoted: z.string().max(1000).optional().or(z.literal('')),
  actionRequired: z.string().max(500).optional().or(z.literal('')),
  riskLevel: z.enum(['Negligible', 'Low', 'Medium', 'High', 'Critical']).optional().or(z.literal('')),
  targetDate: z.string().optional().or(z.literal('')),
  responsiblePersonId: z.string().optional().or(z.literal('')),
  isResolved: z.boolean(),
  resolvedDate: z.string().optional().or(z.literal('')),
  resolutionNotes: z.string().max(500).optional().or(z.literal('')),
  resolvedById: z.string().optional().or(z.literal('')),
});
type ItemForm = z.input<typeof itemSchema>;
const emptyItem: ItemForm = {
  itemDescription: '',
  status: 'NotAssessed',
  deficiencyNoted: '',
  actionRequired: '',
  riskLevel: '',
  targetDate: '',
  responsiblePersonId: '',
  isResolved: false,
  resolvedDate: '',
  resolutionNotes: '',
  resolvedById: '',
};

// ── Discovered-hazard schema ─────────────────────────────────────────────────

const hazardSchema = z.object({
  hazardDescription: z.string().min(1, 'Describe the hazard').max(500),
  status: z.enum(['Identified', 'UnderAssessment', 'ControlsInPlace', 'Monitoring', 'Resolved', 'Closed']),
  initialRiskLevel: z.enum(['VeryLow', 'Low', 'Medium', 'High', 'VeryHigh', 'Critical']),
  residualRiskLevel: z.enum(['VeryLow', 'Low', 'Medium', 'High', 'VeryHigh', 'Critical']),
  reviewDueDate: z.string().optional().or(z.literal('')),
  ownerId: z.string().optional().or(z.literal('')),
});
type HazardForm = z.input<typeof hazardSchema>;
const emptyHazard: HazardForm = {
  hazardDescription: '',
  status: 'Identified',
  initialRiskLevel: 'Medium',
  residualRiskLevel: 'Medium',
  reviewDueDate: '',
  ownerId: '',
};

// ── Corrective-action schema (flattened across hazards) ──────────────────────

const actionSchema = z.object({
  inspectionHazardId: z.string().min(1, 'Choose the hazard this action addresses'),
  correctiveActionTemplateId: z.string().min(1, 'Choose a template'),
  status: z.enum(['Pending', 'InProgress', 'Completed', 'Verified', 'Overdue', 'Cancelled']),
  dueDate: z.string().optional().or(z.literal('')),
  completionDate: z.string().optional().or(z.literal('')),
  completionNotes: z.string().max(500).optional().or(z.literal('')),
  assignedToId: z.string().optional().or(z.literal('')),
});
type ActionForm = z.input<typeof actionSchema>;
const emptyAction: ActionForm = {
  inspectionHazardId: '',
  correctiveActionTemplateId: '',
  status: 'Pending',
  dueDate: '',
  completionDate: '',
  completionNotes: '',
  assignedToId: '',
};

type FlatAction = SafetyInspectionHazardAction & { hazardDescription: string };

// ── Document schema ──────────────────────────────────────────────────────────

const documentSchema = z.object({
  fileName: z.string().min(1, 'A file name is required').max(255),
  filePath: z.string().min(1, 'A file path is required').max(500),
  description: z.string().max(500).optional().or(z.literal('')),
  uploadedById: z.string().min(1, 'Who uploaded it?'),
});
type DocumentForm = z.input<typeof documentSchema>;
const emptyDocument: DocumentForm = {
  fileName: '',
  filePath: '',
  description: '',
  uploadedById: '',
};

/**
 * One inspection, end to end: the walk's findings against the checklist, hazards discovered on
 * the way (each with corrective actions), documents, and the guarded close-out — the server
 * refuses to close while a finding is unresolved or a corrective action is open.
 */
export default function InspectionDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editOpen, setEditOpen] = useState(false);
  const [closeOpen, setCloseOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const { data: inspection, isLoading } = useQuery({
    queryKey: ['hr', 'safety-inspection', id],
    queryFn: () => safetyInspectionService.getById(id),
    enabled: !!id,
  });

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const { data: orgUnits = [] } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });

  const { data: checklists = [] } = useQuery({
    queryKey: ['hr', 'safety-checklists'],
    queryFn: () => safetyChecklistService.getAll(),
  });

  const { data: caTemplates = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'corrective-action-templates', 'active'],
    queryFn: () => safetyReferenceService.getCorrectiveActionTemplates(true),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) as any });
  const closeForm = useForm<CloseForm>({
    resolver: zodResolver(closeSchema) as any,
    defaultValues: { closedById: '', closedDate: new Date().toISOString().slice(0, 10) },
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-inspection', id] });

  const openEdit = (i: SafetyInspection) => {
    editForm.reset({
      inspectionDate: i.inspectionDate.slice(0, 10),
      locationId: i.locationId ?? '',
      specificArea: i.specificArea ?? '',
      organizationUnitId: i.organizationUnitId ?? '',
      type: i.type,
      category: i.category,
      checklistId: i.checklistId ?? '',
      inspectorId: i.inspectorId,
      externalInspectorName: i.externalInspectorName ?? '',
      externalInspectorOrganization: i.externalInspectorOrganization ?? '',
      findingsAndObservations: i.findingsAndObservations ?? '',
      recommendedActions: i.recommendedActions ?? '',
      positiveObservations: i.positiveObservations ?? '',
      status: i.status,
      overallRiskRating: i.overallRiskRating ?? '',
      complianceScore: i.complianceScore ?? undefined,
      complianceDeadline: i.complianceDeadline ? i.complianceDeadline.slice(0, 10) : '',
      nextInspectionDueDate: i.nextInspectionDueDate ? i.nextInspectionDueDate.slice(0, 10) : '',
    });
    setEditOpen(true);
  };

  const onSaveEdit = async (values: EditForm) => {
    const v = editSchema.parse(values);
    setSaving(true);
    try {
      await safetyInspectionService.update(id, {
        id,
        inspectionDate: v.inspectionDate,
        locationId: blank(v.locationId),
        specificArea: blank(v.specificArea),
        organizationUnitId: blank(v.organizationUnitId),
        type: v.type,
        category: v.category,
        checklistId: blank(v.checklistId),
        inspectorId: v.inspectorId,
        externalInspectorName: blank(v.externalInspectorName),
        externalInspectorOrganization: blank(v.externalInspectorOrganization),
        findingsAndObservations: blank(v.findingsAndObservations),
        recommendedActions: blank(v.recommendedActions),
        positiveObservations: blank(v.positiveObservations),
        status: v.status,
        overallRiskRating: v.overallRiskRating === '' ? null : v.overallRiskRating,
        complianceScore: v.complianceScore ?? null,
        complianceDeadline: blank(v.complianceDeadline),
        nextInspectionDueDate: blank(v.nextInspectionDueDate),
      });
      await invalidate();
      toast({ title: 'Saved', description: 'Inspection updated.' });
      setEditOpen(false);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to save the inspection.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const onClose = async (values: CloseForm) => {
    const v = closeSchema.parse(values);
    setSaving(true);
    try {
      await safetyInspectionService.close(id, {
        inspectionId: id,
        closedById: v.closedById,
        closedDate: v.closedDate,
      });
      await invalidate();
      toast({ title: 'Closed', description: 'The inspection is closed.' });
      setCloseOpen(false);
    } catch (e: any) {
      toast({
        title: 'Cannot close',
        description: e?.message || 'Failed to close the inspection.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const onDelete = async () => {
    try {
      await safetyInspectionService.remove(id);
      toast({ title: 'Removed', description: 'Inspection removed.' });
      router.push('/hr/safety/inspections');
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to remove the inspection.',
        variant: 'destructive',
      });
    }
  };

  if (isLoading || !inspection) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  const isClosed = inspection.status === 'Closed';
  const openItems = inspection.items.filter((t) => !t.isResolved).length;
  const openActions = inspection.hazards
    .flatMap((h) => h.actions)
    .filter((a) => !['Completed', 'Verified', 'Cancelled'].includes(a.status)).length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${inspection.inspectionNumber}`}
        description={`${inspection.typeName} · ${inspection.categoryName} · ${fmtDate(inspection.inspectionDate)}`}
        backHref="/hr/safety/inspections"
        actions={
          <div className="flex gap-2">
            {!isClosed && (
              <Button onClick={() => setCloseOpen(true)}>
                <Lock className="mr-2 h-4 w-4" />
                Close inspection
              </Button>
            )}
            <Button variant="outline" onClick={() => openEdit(inspection)}>
              <Pencil className="mr-2 h-4 w-4" />
              Edit
            </Button>
            <Button variant="outline" className="text-red-600" onClick={() => setDeleteOpen(true)}>
              <Trash2 className="mr-2 h-4 w-4" />
              Remove
            </Button>
          </div>
        }
      />

      {!isClosed && (openItems > 0 || openActions > 0) && (
        <p className="rounded-md bg-amber-50 p-3 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200">
          {openItems} unresolved finding{openItems === 1 ? '' : 's'} and {openActions} open
          corrective action{openActions === 1 ? '' : 's'} — the inspection cannot close until they
          are all resolved.
        </p>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Inspection</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
              <span className="text-muted-foreground">Status</span>
              <StatusBadge status={inspection.statusName} />
            </div>
            <InfoRow label="Location" value={inspection.locationName} />
            <InfoRow label="Specific area" value={inspection.specificArea} />
            <InfoRow label="Organization unit" value={inspection.organizationUnitName} />
            <InfoRow label="Checklist" value={inspection.checklistName} />
            <InfoRow label="Inspector" value={inspection.inspectorName} />
            {inspection.externalInspectorName && (
              <InfoRow
                label="External inspector"
                value={`${inspection.externalInspectorName}${inspection.externalInspectorOrganization ? ` (${inspection.externalInspectorOrganization})` : ''}`}
              />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Outcome</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
              <span className="text-muted-foreground">Overall risk</span>
              {inspection.overallRiskRating ? (
                <RiskBadge
                  level={inspection.overallRiskRating}
                  label={inspection.overallRiskRatingName ?? undefined}
                />
              ) : (
                <span className="font-medium">—</span>
              )}
            </div>
            <InfoRow
              label="Compliance score"
              value={inspection.complianceScore != null ? `${inspection.complianceScore}%` : null}
            />
            <InfoRow label="Compliance deadline" value={fmtDate(inspection.complianceDeadline)} />
            {inspection.findingsAndObservations && (
              <p className="text-muted-foreground mt-3 whitespace-pre-wrap text-sm">
                {inspection.findingsAndObservations}
              </p>
            )}
            {inspection.positiveObservations && (
              <p className="mt-2 flex items-start gap-1 text-sm text-green-700 dark:text-green-400">
                <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
                {inspection.positiveObservations}
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Lifecycle</CardTitle>
          </CardHeader>
          <CardContent>
            <InfoRow label="Next inspection due" value={fmtDate(inspection.nextInspectionDueDate)} />
            <InfoRow label="Closed" value={fmtDate(inspection.closedDate)} />
            <InfoRow label="Closed by" value={inspection.closedByName} />
            {inspection.recommendedActions && (
              <p className="text-muted-foreground mt-3 whitespace-pre-wrap text-sm">
                {inspection.recommendedActions}
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="items">
        <TabsList>
          <TabsTrigger value="items">Findings ({inspection.items.length})</TabsTrigger>
          <TabsTrigger value="hazards">Hazards ({inspection.hazards.length})</TabsTrigger>
          <TabsTrigger value="actions">
            Corrective actions ({inspection.hazards.reduce((n, h) => n + h.actions.length, 0)})
          </TabsTrigger>
          <TabsTrigger value="documents">Documents ({inspection.documents.length})</TabsTrigger>
        </TabsList>

        {/* ── Findings ── */}
        <TabsContent value="items" className="mt-4">
          <ResourceCollectionTab<SafetyInspectionItem, ItemForm>
            parentId={id}
            title="findings"
            singular="finding"
            queryKey={['hr', 'safety-inspection', id, 'items']}
            invalidateKeys={[['hr', 'safety-inspection', id]]}
            list={async () => (await safetyInspectionService.getById(id)).items}
            create={(inspectionId, values) => {
              const v = itemSchema.parse(values);
              return safetyInspectionService.addItem(inspectionId, {
                inspectionId,
                itemDescription: v.itemDescription,
                status: v.status,
                deficiencyNoted: blank(v.deficiencyNoted),
                actionRequired: blank(v.actionRequired),
                riskLevel: v.riskLevel === '' ? null : v.riskLevel,
                targetDate: blank(v.targetDate),
                responsiblePersonId: blank(v.responsiblePersonId),
              });
            }}
            update={(_inspectionId, itemId, values) => {
              const v = itemSchema.parse(values);
              return safetyInspectionService.updateItem(itemId, {
                id: itemId,
                itemDescription: v.itemDescription,
                status: v.status,
                deficiencyNoted: blank(v.deficiencyNoted),
                actionRequired: blank(v.actionRequired),
                riskLevel: v.riskLevel === '' ? null : v.riskLevel,
                targetDate: blank(v.targetDate),
                responsiblePersonId: blank(v.responsiblePersonId),
                isResolved: v.isResolved,
                resolvedDate: blank(v.resolvedDate),
                resolutionNotes: blank(v.resolutionNotes),
                resolvedById: blank(v.resolvedById),
              });
            }}
            remove={(_inspectionId, itemId) => safetyInspectionService.removeItem(itemId)}
            columns={[
              { header: 'Check', cell: (t) => t.itemDescription },
              { header: 'Status', cell: (t) => <StatusBadge status={t.statusName} /> },
              { header: 'Deficiency', cell: (t) => t.deficiencyNoted ?? '—' },
              {
                header: 'Risk',
                cell: (t) =>
                  t.riskLevel ? <RiskBadge level={t.riskLevel} label={t.riskLevelName ?? undefined} /> : '—',
              },
              { header: 'Responsible', cell: (t) => t.responsiblePersonName ?? '—' },
              { header: 'Target', cell: (t) => fmtDate(t.targetDate) },
              {
                header: 'Resolved',
                cell: (t) =>
                  t.isResolved ? (
                    <span className="flex items-center gap-1 text-green-700 dark:text-green-400">
                      <CheckCircle2 className="h-4 w-4" />
                      {fmtDate(t.resolvedDate)}
                    </span>
                  ) : (
                    <Badge variant="outline">Open</Badge>
                  ),
              },
            ]}
            schema={itemSchema}
            emptyForm={emptyItem}
            toForm={(t) => ({
              itemDescription: t.itemDescription,
              status: t.status,
              deficiencyNoted: t.deficiencyNoted ?? '',
              actionRequired: t.actionRequired ?? '',
              riskLevel: t.riskLevel ?? '',
              targetDate: t.targetDate ? t.targetDate.slice(0, 10) : '',
              responsiblePersonId: t.responsiblePersonId ?? '',
              isResolved: t.isResolved,
              resolvedDate: t.resolvedDate ? t.resolvedDate.slice(0, 10) : '',
              resolutionNotes: t.resolutionNotes ?? '',
              resolvedById: t.resolvedById ?? '',
            })}
            renderFields={(f, editing) => (
              <>
                <TextareaField form={f} name="itemDescription" label="What was checked" rows={2} />
                <FieldRow>
                  <SelectField
                    form={f}
                    name="status"
                    label="Compliance"
                    required
                    options={SHE_COMPLIANCE_STATUS_OPTIONS}
                  />
                  <SelectField
                    form={f}
                    name="riskLevel"
                    label="Risk"
                    allowEmpty
                    options={SHE_RISK_LEVEL_OPTIONS}
                  />
                </FieldRow>
                <TextareaField form={f} name="deficiencyNoted" label="Deficiency noted" rows={2} />
                <TextareaField form={f} name="actionRequired" label="Action required" rows={2} />
                <FieldRow>
                  <EmployeePickerField form={f} name="responsiblePersonId" label="Responsible" />
                  <DateField form={f} name="targetDate" label="Target date" />
                </FieldRow>
                {editing && (
                  <>
                    <SwitchField
                      form={f}
                      name="isResolved"
                      label="Resolved"
                      description="A closed inspection requires every finding resolved."
                    />
                    <FieldRow>
                      <DateField form={f} name="resolvedDate" label="Resolved on" />
                      <EmployeePickerField form={f} name="resolvedById" label="Resolved by" />
                    </FieldRow>
                    <TextareaField form={f} name="resolutionNotes" label="Resolution notes" rows={2} />
                  </>
                )}
              </>
            )}
            getId={(t) => t.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Record what was checked and what was found, item by item."
          />
        </TabsContent>

        {/* ── Discovered hazards ── */}
        <TabsContent value="hazards" className="mt-4">
          <ResourceCollectionTab<SafetyInspectionHazard, HazardForm>
            parentId={id}
            title="hazards"
            singular="hazard"
            queryKey={['hr', 'safety-inspection', id, 'hazards']}
            invalidateKeys={[['hr', 'safety-inspection', id]]}
            list={async () => (await safetyInspectionService.getById(id)).hazards}
            create={(inspectionId, values) => {
              const v = hazardSchema.parse(values);
              return safetyInspectionService.addHazard(inspectionId, {
                inspectionId,
                hazardDescription: v.hazardDescription,
                status: v.status,
                initialRiskLevel: v.initialRiskLevel,
                residualRiskLevel: v.residualRiskLevel,
                reviewDueDate: blank(v.reviewDueDate),
                ownerId: blank(v.ownerId),
              });
            }}
            update={(_inspectionId, hazardId, values) => {
              const v = hazardSchema.parse(values);
              return safetyInspectionService.updateHazard(hazardId, {
                id: hazardId,
                hazardDescription: v.hazardDescription,
                status: v.status,
                initialRiskLevel: v.initialRiskLevel,
                residualRiskLevel: v.residualRiskLevel,
                reviewDueDate: blank(v.reviewDueDate),
                ownerId: blank(v.ownerId),
              });
            }}
            remove={(_inspectionId, hazardId) => safetyInspectionService.removeHazard(hazardId)}
            columns={[
              { header: 'Hazard', cell: (h) => h.hazardDescription },
              { header: 'Status', cell: (h) => <StatusBadge status={h.statusName} /> },
              {
                header: 'Initial risk',
                cell: (h) => <RiskBadge level={h.initialRiskLevel} label={h.initialRiskLevelName} />,
              },
              {
                header: 'Residual risk',
                cell: (h) => <RiskBadge level={h.residualRiskLevel} label={h.residualRiskLevelName} />,
              },
              { header: 'Owner', cell: (h) => h.ownerName ?? '—' },
              { header: 'Actions', cell: (h) => <span className="tabular-nums">{h.actions.length}</span> },
            ]}
            schema={hazardSchema}
            emptyForm={emptyHazard}
            toForm={(h) => ({
              hazardDescription: h.hazardDescription,
              status: h.status,
              initialRiskLevel: h.initialRiskLevel,
              residualRiskLevel: h.residualRiskLevel,
              reviewDueDate: h.reviewDueDate ? h.reviewDueDate.slice(0, 10) : '',
              ownerId: h.ownerId ?? '',
            })}
            renderFields={(f) => (
              <>
                <TextareaField form={f} name="hazardDescription" label="Hazard" rows={2} />
                <FieldRow>
                  <SelectField
                    form={f}
                    name="initialRiskLevel"
                    label="Initial risk"
                    required
                    options={SHE_HAZARD_RISK_LEVEL_OPTIONS}
                  />
                  <SelectField
                    form={f}
                    name="residualRiskLevel"
                    label="Residual risk"
                    required
                    options={SHE_HAZARD_RISK_LEVEL_OPTIONS}
                  />
                </FieldRow>
                <FieldRow>
                  <SelectField
                    form={f}
                    name="status"
                    label="Status"
                    required
                    options={SHE_HAZARD_STATUS_OPTIONS}
                  />
                  <EmployeePickerField form={f} name="ownerId" label="Owner" />
                </FieldRow>
                <DateField form={f} name="reviewDueDate" label="Review due" />
              </>
            )}
            getId={(h) => h.id}
            emptyDescription="Hazards discovered during the walk. Corrective actions attach on their own tab."
          />
        </TabsContent>

        {/* ── Corrective actions (flattened) ── */}
        <TabsContent value="actions" className="mt-4">
          <ResourceCollectionTab<FlatAction, ActionForm>
            parentId={id}
            title="corrective actions"
            singular="corrective action"
            queryKey={['hr', 'safety-inspection', id, 'actions']}
            invalidateKeys={[['hr', 'safety-inspection', id]]}
            list={async () => {
              const full = await safetyInspectionService.getById(id);
              return full.hazards.flatMap((h) =>
                h.actions.map((a) => ({ ...a, hazardDescription: h.hazardDescription })),
              );
            }}
            create={(_inspectionId, values) => {
              const v = actionSchema.parse(values);
              return safetyInspectionService.addHazardAction(v.inspectionHazardId, {
                inspectionHazardId: v.inspectionHazardId,
                correctiveActionTemplateId: v.correctiveActionTemplateId,
                status: v.status,
                dueDate: blank(v.dueDate),
                assignedToId: blank(v.assignedToId),
              });
            }}
            update={(_inspectionId, actionId, values) => {
              const v = actionSchema.parse(values);
              return safetyInspectionService.updateHazardAction(actionId, {
                id: actionId,
                status: v.status,
                dueDate: blank(v.dueDate),
                completionDate: blank(v.completionDate),
                completionNotes: blank(v.completionNotes),
                assignedToId: blank(v.assignedToId),
              });
            }}
            remove={(_inspectionId, actionId) => safetyInspectionService.removeHazardAction(actionId)}
            columns={[
              { header: 'Hazard', cell: (a) => a.hazardDescription },
              { header: 'Action', cell: (a) => a.correctiveActionTemplateTitle },
              { header: 'Status', cell: (a) => <StatusBadge status={a.statusName} /> },
              { header: 'Assigned to', cell: (a) => a.assignedToName ?? '—' },
              { header: 'Due', cell: (a) => fmtDate(a.dueDate) },
              { header: 'Completed', cell: (a) => fmtDate(a.completionDate) },
            ]}
            schema={actionSchema}
            emptyForm={emptyAction}
            toForm={(a) => ({
              inspectionHazardId: a.inspectionHazardId,
              correctiveActionTemplateId: a.correctiveActionTemplateId,
              status: a.status,
              dueDate: a.dueDate ? a.dueDate.slice(0, 10) : '',
              completionDate: a.completionDate ? a.completionDate.slice(0, 10) : '',
              completionNotes: a.completionNotes ?? '',
              assignedToId: a.assignedToId ?? '',
            })}
            renderFields={(f, editing) => (
              <>
                {!editing && (
                  <SelectField
                    form={f}
                    name="inspectionHazardId"
                    label="Hazard"
                    required
                    options={inspection.hazards.map((h) => ({
                      value: h.id,
                      label: h.hazardDescription,
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
              inspection.hazards.length === 0
                ? 'Record a hazard first — corrective actions attach to hazards.'
                : 'Attach corrective actions to the discovered hazards.'
            }
          />
        </TabsContent>

        {/* ── Documents ── */}
        <TabsContent value="documents" className="mt-4">
          <ResourceCollectionTab<SafetyInspectionDocument, DocumentForm>
            parentId={id}
            title="documents"
            singular="document"
            queryKey={['hr', 'safety-inspection', id, 'documents']}
            invalidateKeys={[['hr', 'safety-inspection', id]]}
            list={async () => (await safetyInspectionService.getById(id)).documents}
            create={(inspectionId, values) => {
              const v = documentSchema.parse(values);
              return safetyInspectionService.addDocument(inspectionId, {
                inspectionId,
                fileName: v.fileName,
                filePath: v.filePath,
                description: blank(v.description),
                uploadedById: v.uploadedById,
              });
            }}
            update={() => Promise.reject(new Error('Documents cannot be edited'))}
            allowUpdate={false}
            remove={(_inspectionId, documentId) => safetyInspectionService.removeDocument(documentId)}
            columns={[
              { header: 'File', cell: (d) => <span className="font-medium">{d.fileName}</span> },
              { header: 'Description', cell: (d) => d.description ?? '—' },
              { header: 'Uploaded by', cell: (d) => d.uploadedByName },
              { header: 'Uploaded', cell: (d) => fmtDate(d.uploadDate) },
            ]}
            schema={documentSchema}
            emptyForm={emptyDocument}
            toForm={(d) => ({
              fileName: d.fileName,
              filePath: d.filePath,
              description: d.description ?? '',
              uploadedById: d.uploadedById,
            })}
            renderFields={(f) => (
              <>
                <FieldRow>
                  <TextField form={f} name="fileName" label="File name" required />
                  <TextField form={f} name="filePath" label="File path" required />
                </FieldRow>
                <TextareaField form={f} name="description" label="Description" rows={2} />
                <EmployeePickerField form={f} name="uploadedById" label="Uploaded by" required />
              </>
            )}
            getId={(d) => d.id}
            emptyDescription="Attach photos, reports and evidence from the inspection."
          />
        </TabsContent>
      </Tabs>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-[680px]">
          <form onSubmit={editForm.handleSubmit(onSaveEdit)}>
            <DialogHeader>
              <DialogTitle>Edit inspection</DialogTitle>
              <DialogDescription>
                Header, narrative and outcome — findings and hazards are managed on their tabs.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <SelectField form={editForm} name="type" label="Type" required options={SHE_INSPECTION_TYPE_OPTIONS} />
                <SelectField form={editForm} name="category" label="Category" required options={SHE_INSPECTION_CATEGORY_OPTIONS} />
              </FieldRow>
              <FieldRow>
                <DateField form={editForm} name="inspectionDate" label="Inspection date" required />
                <SelectField form={editForm} name="status" label="Status" required options={SHE_INSPECTION_STATUS_OPTIONS} />
              </FieldRow>
              <FieldRow>
                <SelectField
                  form={editForm}
                  name="locationId"
                  label="Location"
                  allowEmpty
                  options={locations.map((l) => ({ value: l.id, label: l.name }))}
                />
                <TextField form={editForm} name="specificArea" label="Specific area" />
              </FieldRow>
              <FieldRow>
                <SelectField
                  form={editForm}
                  name="organizationUnitId"
                  label="Organization unit"
                  allowEmpty
                  options={orgUnits.map((u) => ({ value: u.id, label: u.name }))}
                />
                <SelectField
                  form={editForm}
                  name="checklistId"
                  label="Checklist"
                  allowEmpty
                  options={checklists.map((c) => ({
                    value: c.id,
                    label: `${c.checklistNumber} — ${c.name}`,
                  }))}
                />
              </FieldRow>
              <FieldRow>
                <EmployeePickerField form={editForm} name="inspectorId" label="Inspector" required />
                <TextField form={editForm} name="externalInspectorName" label="External inspector" />
              </FieldRow>
              <TextField
                form={editForm}
                name="externalInspectorOrganization"
                label="External organization"
              />
              <TextareaField
                form={editForm}
                name="findingsAndObservations"
                label="Findings & observations"
                rows={3}
              />
              <TextareaField form={editForm} name="recommendedActions" label="Recommended actions" rows={2} />
              <TextareaField form={editForm} name="positiveObservations" label="Positive observations" rows={2} />
              <FieldRow>
                <SelectField
                  form={editForm}
                  name="overallRiskRating"
                  label="Overall risk rating"
                  allowEmpty
                  options={SHE_RISK_LEVEL_OPTIONS}
                />
                <NumberField form={editForm} name="complianceScore" label="Compliance score (%)" />
              </FieldRow>
              <FieldRow>
                <DateField form={editForm} name="complianceDeadline" label="Compliance deadline" />
                <DateField form={editForm} name="nextInspectionDueDate" label="Next inspection due" />
              </FieldRow>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditOpen(false)} disabled={saving}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving}>
                {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Save changes
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Close dialog ── */}
      <Dialog open={closeOpen} onOpenChange={setCloseOpen}>
        <DialogContent className="sm:max-w-[480px]">
          <form onSubmit={closeForm.handleSubmit(onClose)}>
            <DialogHeader>
              <DialogTitle>Close {inspection.inspectionNumber}</DialogTitle>
              <DialogDescription>
                The server refuses to close while any finding is unresolved or any corrective
                action is still open.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <EmployeePickerField form={closeForm} name="closedById" label="Closed by" required />
              <DateField form={closeForm} name="closedDate" label="Close date" required />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCloseOpen(false)} disabled={saving}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving}>
                {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Close inspection
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Remove this inspection?"
        description="It comes off the register along with its findings, hazards and documents."
        confirmText="Remove"
        variant="destructive"
        onConfirm={onDelete}
      />
    </div>
  );
}
