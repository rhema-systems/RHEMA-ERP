'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Pencil, Trash2 } from 'lucide-react';
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
import {
  RiskBadge,
  LIKELIHOOD_OPTIONS,
  SEVERITY_OPTIONS,
  previewHazardLevel,
} from '@/components/hr/safety/RiskBadge';
import { safetyHazardService } from '@/services/hr/safety-hazard.service';
import { safetyReferenceService } from '@/services/hr/safety-reference.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_HAZARD_CATEGORY_OPTIONS,
  SHE_HAZARD_STATUS_OPTIONS,
  SHE_HIERARCHY_OF_CONTROL_OPTIONS,
  SHE_CONTROL_STATUS_OPTIONS,
  type SheHazard,
  type SheHazardControl,
  type SheHazardCorrectiveAction,
} from '@/types/hr/safety-hazards';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const rating = z.enum(['1', '2', '3', '4', '5']);

function InfoRow({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
      <span className="text-muted-foreground shrink-0">{label}</span>
      <span className="text-right font-medium">{value ?? '—'}</span>
    </div>
  );
}

// ── Edit (assessment) schema ─────────────────────────────────────────────────

const editSchema = z.object({
  code: z.string().max(30).optional().or(z.literal('')),
  name: z.string().min(1, 'A name is required').max(200),
  category: z.enum([
    'Physical',
    'Chemical',
    'Biological',
    'Ergonomic',
    'Psychosocial',
    'Electrical',
    'Mechanical',
    'FireExplosion',
    'SlipTripFall',
    'WorkingAtHeight',
    'ConfinedSpace',
    'Radiation',
    'Environmental',
    'Traffic',
    'Other',
  ]),
  description: z.string().min(1, 'A description is required').max(2000),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  inherentLikelihood: rating,
  inherentSeverity: rating,
  residualLikelihood: rating,
  residualSeverity: rating,
  status: z.enum(['Identified', 'UnderAssessment', 'ControlsInPlace', 'Monitoring', 'Resolved', 'Closed']),
  ownerId: z.string().optional().or(z.literal('')),
  reviewDueDate: z.string().optional().or(z.literal('')),
  lastReviewedDate: z.string().optional().or(z.literal('')),
  lastReviewedById: z.string().optional().or(z.literal('')),
  isActive: z.boolean(),
});
type EditForm = z.input<typeof editSchema>;

// ── Controls schema ──────────────────────────────────────────────────────────

const controlSchema = z.object({
  controlLevel: z.enum(['Elimination', 'Substitution', 'Engineering', 'Administrative', 'PPE']),
  controlDescription: z.string().min(1, 'Describe the control').max(500),
  status: z.enum(['Planned', 'Implemented', 'Verified', 'Ineffective', 'Superseded']),
  responsiblePersonId: z.string().optional().or(z.literal('')),
  implementationDate: z.string().optional().or(z.literal('')),
  reviewDate: z.string().optional().or(z.literal('')),
});
type ControlForm = z.input<typeof controlSchema>;
const emptyControl: ControlForm = {
  controlLevel: 'Engineering',
  controlDescription: '',
  status: 'Planned',
  responsiblePersonId: '',
  implementationDate: '',
  reviewDate: '',
};

// ── Corrective-action link schema ────────────────────────────────────────────

const caSchema = z.object({
  correctiveActionTemplateId: z.string().min(1, 'Choose a template'),
  deadlineDays: z.coerce.number().min(0).max(365).optional(),
  isMandatory: z.boolean(),
  displayOrder: z.coerce.number().min(0).max(999),
});
type CaForm = z.input<typeof caSchema>;
const emptyCa: CaForm = {
  correctiveActionTemplateId: '',
  deadlineDays: undefined,
  isMandatory: true,
  displayOrder: 0,
};

/**
 * One hazard: the inherent → residual risk picture, the hierarchy of controls that gets it
 * there, and the corrective-action templates linked to it. Scores and levels are recomputed
 * server-side from the four 1–5 factors on every save — nothing here submits a score.
 */
export default function HazardDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const { data: hazard, isLoading } = useQuery({
    queryKey: ['hr', 'safety-hazard', id],
    queryFn: () => safetyHazardService.getById(id),
    enabled: !!id,
  });

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const { data: caTemplates = [] } = useQuery({
    queryKey: ['hr', 'safety-reference', 'corrective-action-templates', 'active'],
    queryFn: () => safetyReferenceService.getCorrectiveActionTemplates(true),
  });

  const form = useForm<EditForm>({ resolver: zodResolver(editSchema) as any });

  const openEdit = (h: SheHazard) => {
    form.reset({
      code: h.code ?? '',
      name: h.name,
      category: h.category,
      description: h.description,
      locationId: h.locationId ?? '',
      specificArea: h.specificArea ?? '',
      inherentLikelihood: String(h.inherentLikelihood) as EditForm['inherentLikelihood'],
      inherentSeverity: String(h.inherentSeverity) as EditForm['inherentSeverity'],
      residualLikelihood: String(h.residualLikelihood) as EditForm['residualLikelihood'],
      residualSeverity: String(h.residualSeverity) as EditForm['residualSeverity'],
      status: h.status,
      ownerId: h.ownerId ?? '',
      reviewDueDate: h.reviewDueDate ? h.reviewDueDate.slice(0, 10) : '',
      lastReviewedDate: h.lastReviewedDate ? h.lastReviewedDate.slice(0, 10) : '',
      lastReviewedById: h.lastReviewedById ?? '',
      isActive: h.isActive,
    });
    setEditOpen(true);
  };

  const onSaveEdit = async (values: EditForm) => {
    const v = editSchema.parse(values);
    setSaving(true);
    try {
      await safetyHazardService.update(id, {
        id,
        code: blank(v.code),
        name: v.name,
        category: v.category,
        description: v.description,
        locationId: blank(v.locationId),
        specificArea: blank(v.specificArea),
        inherentLikelihood: Number(v.inherentLikelihood),
        inherentSeverity: Number(v.inherentSeverity),
        residualLikelihood: Number(v.residualLikelihood),
        residualSeverity: Number(v.residualSeverity),
        status: v.status,
        ownerId: blank(v.ownerId),
        reviewDueDate: blank(v.reviewDueDate),
        lastReviewedDate: blank(v.lastReviewedDate),
        lastReviewedById: blank(v.lastReviewedById),
        isActive: v.isActive,
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'safety-hazard', id] });
      toast({ title: 'Saved', description: 'Hazard updated.' });
      setEditOpen(false);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to save the hazard.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const onDelete = async () => {
    try {
      await safetyHazardService.remove(id);
      toast({ title: 'Removed', description: 'Hazard removed from the register.' });
      router.push('/hr/safety/hazards');
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to remove the hazard.',
        variant: 'destructive',
      });
    }
  };

  if (isLoading || !hazard) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  const editLikelihood = Number(form.watch('residualLikelihood') || hazard.residualLikelihood);
  const editSeverity = Number(form.watch('residualSeverity') || hazard.residualSeverity);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={hazard.name}
        description={hazard.code ? `Hazard ${hazard.code}` : 'Hazard register entry'}
        backHref="/hr/safety/hazards"
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={() => openEdit(hazard)}>
              <Pencil className="mr-2 h-4 w-4" />
              Edit / assess
            </Button>
            <Button variant="outline" className="text-red-600" onClick={() => setDeleteOpen(true)}>
              <Trash2 className="mr-2 h-4 w-4" />
              Remove
            </Button>
          </div>
        }
      />

      <div className="grid gap-6 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Identification</CardTitle>
          </CardHeader>
          <CardContent>
            <InfoRow label="Category" value={hazard.categoryName} />
            <InfoRow label="Location" value={hazard.locationName} />
            <InfoRow label="Specific area" value={hazard.specificArea} />
            <InfoRow label="Owner" value={hazard.ownerName} />
            <InfoRow label="Reported by" value={hazard.reportedByName} />
            <div className="flex items-baseline justify-between gap-4 py-1.5 text-sm">
              <span className="text-muted-foreground">Status</span>
              <span className="flex items-center gap-2">
                {!hazard.isActive && <Badge variant="outline">Inactive</Badge>}
                <StatusBadge status={hazard.statusName} />
              </span>
            </div>
            <p className="text-muted-foreground mt-3 whitespace-pre-wrap text-sm">
              {hazard.description}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Risk</CardTitle>
          </CardHeader>
          <CardContent>
            <InfoRow
              label="Inherent (uncontrolled)"
              value={`${hazard.inherentLikelihood} × ${hazard.inherentSeverity} = ${hazard.inherentRiskScore}`}
            />
            <InfoRow
              label="Residual (with controls)"
              value={`${hazard.residualLikelihood} × ${hazard.residualSeverity} = ${hazard.residualRiskScore}`}
            />
            <div className="flex items-baseline justify-between gap-4 py-1.5 text-sm">
              <span className="text-muted-foreground">Residual risk level</span>
              <RiskBadge level={hazard.residualRiskLevel} label={hazard.residualRiskLevelName} />
            </div>
            {hazard.residualRiskScore >= 12 && (
              <p className="mt-3 rounded-md bg-red-50 p-2 text-sm text-red-800 dark:bg-red-950 dark:text-red-200">
                High residual risk — this hazard shows on the high-risk view until controls bring
                the score under 12.
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Review</CardTitle>
          </CardHeader>
          <CardContent>
            <InfoRow label="Review due" value={fmtDate(hazard.reviewDueDate)} />
            <InfoRow label="Last reviewed" value={fmtDate(hazard.lastReviewedDate)} />
            <InfoRow label="Reviewed by" value={hazard.lastReviewedByName} />
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="controls">
        <TabsList>
          <TabsTrigger value="controls">Controls ({hazard.controls.length})</TabsTrigger>
          <TabsTrigger value="corrective-actions">
            Corrective actions ({hazard.correctiveActions.length})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="controls" className="mt-4">
          <ResourceCollectionTab<SheHazardControl, ControlForm>
            parentId={id}
            title="controls"
            singular="control"
            queryKey={['hr', 'safety-hazard', id, 'controls']}
            invalidateKeys={[['hr', 'safety-hazard', id]]}
            list={async () => (await safetyHazardService.getById(id)).controls}
            create={(hazardId, values) => {
              const v = controlSchema.parse(values);
              return safetyHazardService.addControl(hazardId, {
                hazardId,
                controlLevel: v.controlLevel,
                controlDescription: v.controlDescription,
                status: v.status,
                responsiblePersonId: blank(v.responsiblePersonId),
                implementationDate: blank(v.implementationDate),
                reviewDate: blank(v.reviewDate),
              });
            }}
            update={(_hazardId, controlId, values) => {
              const v = controlSchema.parse(values);
              return safetyHazardService.updateControl(controlId, {
                id: controlId,
                controlLevel: v.controlLevel,
                controlDescription: v.controlDescription,
                status: v.status,
                responsiblePersonId: blank(v.responsiblePersonId),
                implementationDate: blank(v.implementationDate),
                reviewDate: blank(v.reviewDate),
              });
            }}
            remove={(_hazardId, controlId) => safetyHazardService.removeControl(controlId)}
            columns={[
              { header: 'Level', cell: (c) => c.controlLevelName },
              { header: 'Control', cell: (c) => c.controlDescription },
              { header: 'Status', cell: (c) => <StatusBadge status={c.statusName} /> },
              { header: 'Responsible', cell: (c) => c.responsiblePersonName ?? '—' },
              { header: 'Implemented', cell: (c) => fmtDate(c.implementationDate) },
            ]}
            schema={controlSchema}
            emptyForm={emptyControl}
            toForm={(c) => ({
              controlLevel: c.controlLevel,
              controlDescription: c.controlDescription,
              status: c.status,
              responsiblePersonId: c.responsiblePersonId ?? '',
              implementationDate: c.implementationDate ? c.implementationDate.slice(0, 10) : '',
              reviewDate: c.reviewDate ? c.reviewDate.slice(0, 10) : '',
            })}
            renderFields={(f) => (
              <>
                <SelectField
                  form={f}
                  name="controlLevel"
                  label="Hierarchy level"
                  required
                  options={SHE_HIERARCHY_OF_CONTROL_OPTIONS}
                />
                <TextareaField
                  form={f}
                  name="controlDescription"
                  label="Control"
                  rows={3}
                  placeholder="What is (or will be) in place"
                />
                <FieldRow>
                  <SelectField
                    form={f}
                    name="status"
                    label="Status"
                    required
                    options={SHE_CONTROL_STATUS_OPTIONS}
                  />
                  <EmployeePickerField form={f} name="responsiblePersonId" label="Responsible" />
                </FieldRow>
                <FieldRow>
                  <DateField form={f} name="implementationDate" label="Implementation date" />
                  <DateField form={f} name="reviewDate" label="Review date" />
                </FieldRow>
              </>
            )}
            getId={(c) => c.id}
            emptyDescription="Record the controls that bring this hazard's risk down — most effective level first."
            dialogHint="Eliminate before you substitute; PPE is the last resort, not the first."
          />
        </TabsContent>

        <TabsContent value="corrective-actions" className="mt-4">
          <ResourceCollectionTab<SheHazardCorrectiveAction, CaForm>
            parentId={id}
            title="corrective actions"
            singular="corrective action"
            queryKey={['hr', 'safety-hazard', id, 'corrective-actions']}
            invalidateKeys={[['hr', 'safety-hazard', id]]}
            list={async () => (await safetyHazardService.getById(id)).correctiveActions}
            create={(hazardId, values) => {
              const v = caSchema.parse(values);
              return safetyHazardService.addCorrectiveAction(hazardId, {
                hazardId,
                correctiveActionTemplateId: v.correctiveActionTemplateId,
                deadlineDays: v.deadlineDays ?? null,
                isMandatory: v.isMandatory,
                displayOrder: v.displayOrder,
              });
            }}
            update={() => Promise.reject(new Error('Corrective-action links cannot be edited'))}
            allowUpdate={false}
            remove={(_hazardId, caId) => safetyHazardService.removeCorrectiveAction(caId)}
            columns={[
              { header: '#', cell: (a) => a.displayOrder },
              { header: 'Action', cell: (a) => a.correctiveActionTemplateTitle },
              { header: 'Deadline (days)', cell: (a) => a.deadlineDays ?? '—' },
              {
                header: 'Mandatory',
                cell: (a) =>
                  a.isMandatory ? <Badge variant="secondary">Mandatory</Badge> : 'Optional',
              },
            ]}
            schema={caSchema}
            emptyForm={emptyCa}
            toForm={(a) => ({
              correctiveActionTemplateId: a.correctiveActionTemplateId,
              deadlineDays: a.deadlineDays ?? undefined,
              isMandatory: a.isMandatory,
              displayOrder: a.displayOrder,
            })}
            renderFields={(f) => (
              <>
                <SelectField
                  form={f}
                  name="correctiveActionTemplateId"
                  label="Template"
                  required
                  options={caTemplates.map((t) => ({ value: t.id, label: t.title }))}
                />
                <FieldRow>
                  <NumberField form={f} name="deadlineDays" label="Deadline (days)" />
                  <NumberField form={f} name="displayOrder" label="Display order" />
                </FieldRow>
                <SwitchField form={f} name="isMandatory" label="Mandatory" />
              </>
            )}
            getId={(a) => a.id}
            emptyDescription="Link corrective-action templates that this hazard requires."
          />
        </TabsContent>
      </Tabs>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-[640px]">
          <form onSubmit={form.handleSubmit(onSaveEdit)}>
            <DialogHeader>
              <DialogTitle>Edit / assess hazard</DialogTitle>
              <DialogDescription>
                The scores and risk level are recomputed from the four ratings on save. Residual
                preview:{' '}
                <span className="font-medium">
                  {editLikelihood * editSeverity} ({previewHazardLevel(editLikelihood * editSeverity)})
                </span>
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <TextField form={form} name="code" label="Code" placeholder="e.g. HAZ-014" />
                <SelectField
                  form={form}
                  name="category"
                  label="Category"
                  required
                  options={SHE_HAZARD_CATEGORY_OPTIONS}
                />
              </FieldRow>
              <TextField form={form} name="name" label="Name" required />
              <TextareaField form={form} name="description" label="Description" rows={3} />
              <FieldRow>
                <SelectField
                  form={form}
                  name="locationId"
                  label="Location"
                  allowEmpty
                  options={locations.map((l) => ({ value: l.id, label: l.name }))}
                />
                <TextField form={form} name="specificArea" label="Specific area" />
              </FieldRow>
              <FieldRow>
                <SelectField
                  form={form}
                  name="inherentLikelihood"
                  label="Inherent likelihood"
                  required
                  options={LIKELIHOOD_OPTIONS}
                />
                <SelectField
                  form={form}
                  name="inherentSeverity"
                  label="Inherent severity"
                  required
                  options={SEVERITY_OPTIONS}
                />
              </FieldRow>
              <FieldRow>
                <SelectField
                  form={form}
                  name="residualLikelihood"
                  label="Residual likelihood"
                  required
                  options={LIKELIHOOD_OPTIONS}
                />
                <SelectField
                  form={form}
                  name="residualSeverity"
                  label="Residual severity"
                  required
                  options={SEVERITY_OPTIONS}
                />
              </FieldRow>
              <FieldRow>
                <SelectField
                  form={form}
                  name="status"
                  label="Status"
                  required
                  options={SHE_HAZARD_STATUS_OPTIONS}
                />
                <EmployeePickerField form={form} name="ownerId" label="Owner" />
              </FieldRow>
              <FieldRow>
                <DateField form={form} name="reviewDueDate" label="Review due" />
                <DateField form={form} name="lastReviewedDate" label="Last reviewed" />
              </FieldRow>
              <EmployeePickerField form={form} name="lastReviewedById" label="Reviewed by" />
              <SwitchField
                form={form}
                name="isActive"
                label="Active"
                description="Inactive hazards drop off the active register but keep their history."
              />
            </div>
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => setEditOpen(false)}
                disabled={saving}
              >
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

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Remove this hazard?"
        description="It comes off the register along with its controls and corrective-action links."
        confirmText="Remove"
        variant="destructive"
        onConfirm={onDelete}
      />
    </div>
  );
}
