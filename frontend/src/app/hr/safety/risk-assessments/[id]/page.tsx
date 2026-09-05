'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Loader2, Pencil, Trash2, Stamp } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  RiskBadge,
  LIKELIHOOD_OPTIONS,
  SEVERITY_OPTIONS,
  previewRaLevel,
} from '@/components/hr/safety/RiskBadge';
import { safetyRiskAssessmentService } from '@/services/hr/safety-risk-assessment.service';
import { locationService } from '@/services/hr/location.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import {
  SHE_RISK_ASSESSMENT_TYPE_OPTIONS,
  SHE_RISK_ASSESSMENT_STATUS_OPTIONS,
  type SheRiskAssessment,
  type SheRiskAssessmentHazard,
  type SheRiskAssessmentAcknowledgement,
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

// ── Header edit schema ───────────────────────────────────────────────────────

const editSchema = z.object({
  title: z.string().min(1, 'A title is required').max(200),
  type: z.enum(['HIRA', 'JHA', 'PreTask', 'COSHH', 'FireRisk', 'EnvironmentalImpact', 'ErgoAssessment', 'Other']),
  scope: z.string().max(1000).optional().or(z.literal('')),
  locationId: z.string().optional().or(z.literal('')),
  specificActivity: z.string().max(200).optional().or(z.literal('')),
  organizationUnitId: z.string().optional().or(z.literal('')),
  status: z.enum(['Draft', 'PendingReview', 'PendingApproval', 'Approved', 'Active', 'Expired', 'Superseded', 'Withdrawn']),
  validFrom: z.string().optional().or(z.literal('')),
  validUntil: z.string().optional().or(z.literal('')),
  nextReviewDate: z.string().optional().or(z.literal('')),
});
type EditForm = z.input<typeof editSchema>;

// ── Approve schema ───────────────────────────────────────────────────────────

const approveSchema = z.object({
  approvedById: z.string().min(1, 'Choose the approver'),
  approvedDate: z.string().min(1, 'An approval date is required'),
  validFrom: z.string().optional().or(z.literal('')),
  validUntil: z.string().optional().or(z.literal('')),
  nextReviewDate: z.string().optional().or(z.literal('')),
});
type ApproveForm = z.input<typeof approveSchema>;

// ── Hazard line schema ───────────────────────────────────────────────────────

const lineSchema = z.object({
  itemNumber: z.coerce.number().min(1).max(999),
  hazardDescription: z.string().min(1, 'Describe the hazard').max(300),
  potentialConsequences: z.string().max(500).optional().or(z.literal('')),
  affectedPersons: z.string().max(300).optional().or(z.literal('')),
  inherentLikelihood: rating,
  inherentSeverity: rating,
  controlMeasures: z.string().max(2000).optional().or(z.literal('')),
  residualLikelihood: rating,
  residualSeverity: rating,
  responsiblePerson: z.string().max(200).optional().or(z.literal('')),
  targetDate: z.string().optional().or(z.literal('')),
});
type LineForm = z.input<typeof lineSchema>;
const emptyLine: LineForm = {
  itemNumber: 1,
  hazardDescription: '',
  potentialConsequences: '',
  affectedPersons: '',
  inherentLikelihood: '3',
  inherentSeverity: '3',
  controlMeasures: '',
  residualLikelihood: '2',
  residualSeverity: '2',
  responsiblePerson: '',
  targetDate: '',
};

// ── Acknowledgement schema ───────────────────────────────────────────────────

const ackSchema = z.object({
  employeeId: z.string().min(1, 'Choose the employee'),
  acknowledgedDate: z.string().min(1, 'A date is required'),
  comments: z.string().max(500).optional().or(z.literal('')),
});
type AckForm = z.input<typeof ackSchema>;
const emptyAck: AckForm = {
  employeeId: '',
  acknowledgedDate: new Date().toISOString().slice(0, 10),
  comments: '',
};

/**
 * One risk assessment, end to end: the hazard lines with inherent → residual scoring, the guarded
 * approval step, and workforce acknowledgements. Approval is refused once granted and for retired
 * assessments; signing is only possible while the assessment is Approved/Active, and each employee
 * can sign once.
 */
export default function RiskAssessmentDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editOpen, setEditOpen] = useState(false);
  const [approveOpen, setApproveOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const { data: ra, isLoading } = useQuery({
    queryKey: ['hr', 'safety-risk-assessment', id],
    queryFn: () => safetyRiskAssessmentService.getById(id),
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

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) as any });
  const approveForm = useForm<ApproveForm>({
    resolver: zodResolver(approveSchema) as any,
    defaultValues: {
      approvedById: '',
      approvedDate: new Date().toISOString().slice(0, 10),
      validFrom: '',
      validUntil: '',
      nextReviewDate: '',
    },
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-risk-assessment', id] });

  const openEdit = (r: SheRiskAssessment) => {
    editForm.reset({
      title: r.title,
      type: r.type,
      scope: r.scope ?? '',
      locationId: r.locationId ?? '',
      specificActivity: r.specificActivity ?? '',
      organizationUnitId: r.organizationUnitId ?? '',
      status: r.status,
      validFrom: r.validFrom ? r.validFrom.slice(0, 10) : '',
      validUntil: r.validUntil ? r.validUntil.slice(0, 10) : '',
      nextReviewDate: r.nextReviewDate ? r.nextReviewDate.slice(0, 10) : '',
    });
    setEditOpen(true);
  };

  const onSaveEdit = async (values: EditForm) => {
    const v = editSchema.parse(values);
    setSaving(true);
    try {
      await safetyRiskAssessmentService.update(id, {
        id,
        title: v.title,
        type: v.type,
        scope: blank(v.scope),
        locationId: blank(v.locationId),
        specificActivity: blank(v.specificActivity),
        organizationUnitId: blank(v.organizationUnitId),
        status: v.status,
        validFrom: blank(v.validFrom),
        validUntil: blank(v.validUntil),
        nextReviewDate: blank(v.nextReviewDate),
      });
      await invalidate();
      toast({ title: 'Saved', description: 'Assessment updated.' });
      setEditOpen(false);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to save the assessment.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const onApprove = async (values: ApproveForm) => {
    const v = approveSchema.parse(values);
    setSaving(true);
    try {
      await safetyRiskAssessmentService.approve(id, {
        riskAssessmentId: id,
        approvedById: v.approvedById,
        approvedDate: v.approvedDate,
        validFrom: blank(v.validFrom),
        validUntil: blank(v.validUntil),
        nextReviewDate: blank(v.nextReviewDate),
      });
      await invalidate();
      toast({ title: 'Approved', description: 'The assessment is approved — sign-off can begin.' });
      setApproveOpen(false);
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to approve the assessment.',
        variant: 'destructive',
      });
    } finally {
      setSaving(false);
    }
  };

  const onDelete = async () => {
    try {
      await safetyRiskAssessmentService.remove(id);
      toast({ title: 'Removed', description: 'Assessment removed.' });
      router.push('/hr/safety/risk-assessments');
    } catch (e: any) {
      toast({
        title: 'Error',
        description: e?.message || 'Failed to remove the assessment.',
        variant: 'destructive',
      });
    }
  };

  if (isLoading || !ra) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  const canApprove = ['Draft', 'PendingReview', 'PendingApproval'].includes(ra.status);
  const canSign = ra.status === 'Approved' || ra.status === 'Active';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={`${ra.assessmentNumber} — ${ra.title}`}
        description={`${ra.typeName} · version ${ra.version}`}
        backHref="/hr/safety/risk-assessments"
        actions={
          <div className="flex gap-2">
            {canApprove && (
              <Button onClick={() => setApproveOpen(true)}>
                <Stamp className="mr-2 h-4 w-4" />
                Approve
              </Button>
            )}
            <Button variant="outline" onClick={() => openEdit(ra)}>
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

      <div className="grid gap-6 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Assessment</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
              <span className="text-muted-foreground">Status</span>
              <StatusBadge status={ra.statusName} />
            </div>
            <InfoRow label="Location" value={ra.locationName} />
            <InfoRow label="Specific activity" value={ra.specificActivity} />
            <InfoRow label="Organization unit" value={ra.organizationUnitName} />
            {ra.scope && (
              <p className="text-muted-foreground mt-3 whitespace-pre-wrap text-sm">{ra.scope}</p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Authorship</CardTitle>
          </CardHeader>
          <CardContent>
            <InfoRow label="Prepared by" value={ra.preparedByName} />
            <InfoRow label="Prepared" value={fmtDate(ra.preparedDate)} />
            <InfoRow label="Reviewed by" value={ra.reviewedByName} />
            <InfoRow label="Reviewed" value={fmtDate(ra.reviewedDate)} />
            <InfoRow label="Approved by" value={ra.approvedByName} />
            <InfoRow label="Approved" value={fmtDate(ra.approvedDate)} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Validity</CardTitle>
          </CardHeader>
          <CardContent>
            <InfoRow label="Valid from" value={fmtDate(ra.validFrom)} />
            <InfoRow label="Valid until" value={fmtDate(ra.validUntil)} />
            <InfoRow label="Next review" value={fmtDate(ra.nextReviewDate)} />
            {!canSign && (
              <p className="text-muted-foreground mt-3 text-sm">
                Acknowledgements open once the assessment is approved.
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      <Tabs defaultValue="hazards">
        <TabsList>
          <TabsTrigger value="hazards">Hazard lines ({ra.assessedHazards.length})</TabsTrigger>
          <TabsTrigger value="acknowledgements">
            Acknowledgements ({ra.acknowledgements.length})
          </TabsTrigger>
        </TabsList>

        <TabsContent value="hazards" className="mt-4">
          <ResourceCollectionTab<SheRiskAssessmentHazard, LineForm>
            parentId={id}
            title="hazard lines"
            singular="hazard line"
            queryKey={['hr', 'safety-risk-assessment', id, 'hazards']}
            invalidateKeys={[['hr', 'safety-risk-assessment', id]]}
            list={async () =>
              [...(await safetyRiskAssessmentService.getById(id)).assessedHazards].sort(
                (a, b) => a.itemNumber - b.itemNumber,
              )
            }
            create={(raId, values) => {
              const v = lineSchema.parse(values);
              return safetyRiskAssessmentService.addHazard(raId, {
                riskAssessmentId: raId,
                itemNumber: v.itemNumber,
                hazardDescription: v.hazardDescription,
                potentialConsequences: blank(v.potentialConsequences),
                affectedPersons: blank(v.affectedPersons),
                inherentLikelihood: Number(v.inherentLikelihood),
                inherentSeverity: Number(v.inherentSeverity),
                controlMeasures: blank(v.controlMeasures),
                residualLikelihood: Number(v.residualLikelihood),
                residualSeverity: Number(v.residualSeverity),
                responsiblePerson: blank(v.responsiblePerson),
                targetDate: blank(v.targetDate),
              });
            }}
            update={(_raId, lineId, values) => {
              const v = lineSchema.parse(values);
              return safetyRiskAssessmentService.updateHazard(lineId, {
                id: lineId,
                itemNumber: v.itemNumber,
                hazardDescription: v.hazardDescription,
                potentialConsequences: blank(v.potentialConsequences),
                affectedPersons: blank(v.affectedPersons),
                inherentLikelihood: Number(v.inherentLikelihood),
                inherentSeverity: Number(v.inherentSeverity),
                controlMeasures: blank(v.controlMeasures),
                residualLikelihood: Number(v.residualLikelihood),
                residualSeverity: Number(v.residualSeverity),
                responsiblePerson: blank(v.responsiblePerson),
                targetDate: blank(v.targetDate),
              });
            }}
            remove={(_raId, lineId) => safetyRiskAssessmentService.removeHazard(lineId)}
            columns={[
              { header: '#', cell: (l) => l.itemNumber },
              { header: 'Hazard', cell: (l) => l.hazardDescription },
              {
                header: 'Inherent',
                cell: (l) => (
                  <span className="tabular-nums">
                    {l.inherentRiskScore}{' '}
                    <RiskBadge level={l.inherentRiskLevel} label={l.inherentRiskLevelName} />
                  </span>
                ),
              },
              {
                header: 'Residual',
                cell: (l) => (
                  <span className="tabular-nums">
                    {l.residualRiskScore}{' '}
                    <RiskBadge level={l.residualRiskLevel} label={l.residualRiskLevelName} />
                  </span>
                ),
              },
              { header: 'Responsible', cell: (l) => l.responsiblePerson ?? '—' },
              { header: 'Target', cell: (l) => fmtDate(l.targetDate) },
            ]}
            schema={lineSchema}
            emptyForm={emptyLine}
            toForm={(l) => ({
              itemNumber: l.itemNumber,
              hazardDescription: l.hazardDescription,
              potentialConsequences: l.potentialConsequences ?? '',
              affectedPersons: l.affectedPersons ?? '',
              inherentLikelihood: String(l.inherentLikelihood) as LineForm['inherentLikelihood'],
              inherentSeverity: String(l.inherentSeverity) as LineForm['inherentSeverity'],
              controlMeasures: l.controlMeasures ?? '',
              residualLikelihood: String(l.residualLikelihood) as LineForm['residualLikelihood'],
              residualSeverity: String(l.residualSeverity) as LineForm['residualSeverity'],
              responsiblePerson: l.responsiblePerson ?? '',
              targetDate: l.targetDate ? l.targetDate.slice(0, 10) : '',
            })}
            renderFields={(f) => {
              const inh = Number(f.watch('inherentLikelihood')) * Number(f.watch('inherentSeverity'));
              const res = Number(f.watch('residualLikelihood')) * Number(f.watch('residualSeverity'));
              return (
                <>
                  <FieldRow>
                    <NumberField form={f} name="itemNumber" label="Item #" required />
                    <TextField form={f} name="affectedPersons" label="Who is affected" />
                  </FieldRow>
                  <TextareaField
                    form={f}
                    name="hazardDescription"
                    label="Hazard"
                    rows={2}
                    placeholder="What could cause harm during this task"
                  />
                  <TextareaField
                    form={f}
                    name="potentialConsequences"
                    label="Potential consequences"
                    rows={2}
                  />
                  <FieldRow>
                    <SelectField
                      form={f}
                      name="inherentLikelihood"
                      label="Inherent likelihood"
                      required
                      options={LIKELIHOOD_OPTIONS}
                    />
                    <SelectField
                      form={f}
                      name="inherentSeverity"
                      label="Inherent severity"
                      required
                      options={SEVERITY_OPTIONS}
                    />
                  </FieldRow>
                  <TextareaField
                    form={f}
                    name="controlMeasures"
                    label="Control measures"
                    rows={3}
                    placeholder="What brings the risk down to the residual level"
                  />
                  <FieldRow>
                    <SelectField
                      form={f}
                      name="residualLikelihood"
                      label="Residual likelihood"
                      required
                      options={LIKELIHOOD_OPTIONS}
                    />
                    <SelectField
                      form={f}
                      name="residualSeverity"
                      label="Residual severity"
                      required
                      options={SEVERITY_OPTIONS}
                    />
                  </FieldRow>
                  <p className="text-muted-foreground text-sm">
                    Preview — inherent {inh} ({previewRaLevel(inh)}), residual {res} (
                    {previewRaLevel(res)}). Stored scores are computed server-side.
                  </p>
                  <FieldRow>
                    <TextField form={f} name="responsiblePerson" label="Responsible person" />
                    <DateField form={f} name="targetDate" label="Target date" />
                  </FieldRow>
                </>
              );
            }}
            getId={(l) => l.id}
            dialogClassName="sm:max-w-[640px]"
            emptyDescription="Break the assessed task into hazard lines, each scored inherent → residual."
          />
        </TabsContent>

        <TabsContent value="acknowledgements" className="mt-4">
          <ResourceCollectionTab<SheRiskAssessmentAcknowledgement, AckForm>
            parentId={id}
            title="acknowledgements"
            singular="acknowledgement"
            queryKey={['hr', 'safety-risk-assessment', id, 'acknowledgements']}
            invalidateKeys={[['hr', 'safety-risk-assessment', id]]}
            list={async () => (await safetyRiskAssessmentService.getById(id)).acknowledgements}
            create={(raId, values) => {
              const v = ackSchema.parse(values);
              return safetyRiskAssessmentService.addAcknowledgement(raId, {
                riskAssessmentId: raId,
                employeeId: v.employeeId,
                acknowledgedDate: v.acknowledgedDate,
                comments: blank(v.comments),
              });
            }}
            update={() => Promise.reject(new Error('A signature cannot be edited'))}
            allowUpdate={false}
            allowCreate={canSign}
            columns={[
              { header: 'Employee', cell: (a) => a.employeeName },
              { header: 'Signed', cell: (a) => fmtDate(a.acknowledgedDate) },
              { header: 'Comments', cell: (a) => a.comments ?? '—' },
            ]}
            schema={ackSchema}
            emptyForm={emptyAck}
            toForm={(a) => ({
              employeeId: a.employeeId,
              acknowledgedDate: a.acknowledgedDate.slice(0, 10),
              comments: a.comments ?? '',
            })}
            renderFields={(f) => (
              <>
                <EmployeePickerField form={f} name="employeeId" label="Employee" required />
                <FieldRow>
                  <DateField form={f} name="acknowledgedDate" label="Acknowledged on" required />
                  <div />
                </FieldRow>
                <TextareaField form={f} name="comments" label="Comments" rows={2} />
              </>
            )}
            getId={(a) => a.id}
            emptyDescription={
              canSign
                ? 'Record who has read and understood this assessment. Each employee signs once.'
                : 'Sign-off opens once the assessment is approved.'
            }
            dialogHint="Employees can also sign for themselves from their portal; recording here is for paper sign-off sheets."
          />
        </TabsContent>
      </Tabs>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-[640px]">
          <form onSubmit={editForm.handleSubmit(onSaveEdit)}>
            <DialogHeader>
              <DialogTitle>Edit assessment</DialogTitle>
              <DialogDescription>
                Header fields only — hazard lines and acknowledgements are managed on their tabs.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <TextField form={editForm} name="title" label="Title" required />
                <SelectField
                  form={editForm}
                  name="type"
                  label="Type"
                  required
                  options={SHE_RISK_ASSESSMENT_TYPE_OPTIONS}
                />
              </FieldRow>
              <TextareaField form={editForm} name="scope" label="Scope" rows={3} />
              <FieldRow>
                <SelectField
                  form={editForm}
                  name="locationId"
                  label="Location"
                  allowEmpty
                  options={locations.map((l) => ({ value: l.id, label: l.name }))}
                />
                <TextField form={editForm} name="specificActivity" label="Specific activity" />
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
                  name="status"
                  label="Status"
                  required
                  options={SHE_RISK_ASSESSMENT_STATUS_OPTIONS}
                />
              </FieldRow>
              <FieldRow>
                <DateField form={editForm} name="validFrom" label="Valid from" />
                <DateField form={editForm} name="validUntil" label="Valid until" />
              </FieldRow>
              <DateField form={editForm} name="nextReviewDate" label="Next review" />
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

      {/* ── Approve dialog ── */}
      <Dialog open={approveOpen} onOpenChange={setApproveOpen}>
        <DialogContent className="sm:max-w-[560px]">
          <form onSubmit={approveForm.handleSubmit(onApprove)}>
            <DialogHeader>
              <DialogTitle>Approve {ra.assessmentNumber}</DialogTitle>
              <DialogDescription>
                Approval fixes the assessment and opens workforce sign-off. A retired assessment
                cannot be re-approved — it needs a new version.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <EmployeePickerField
                  form={approveForm}
                  name="approvedById"
                  label="Approved by"
                  required
                />
                <DateField form={approveForm} name="approvedDate" label="Approval date" required />
              </FieldRow>
              <FieldRow>
                <DateField form={approveForm} name="validFrom" label="Valid from" />
                <DateField form={approveForm} name="validUntil" label="Valid until" />
              </FieldRow>
              <DateField form={approveForm} name="nextReviewDate" label="Next review" />
            </div>
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => setApproveOpen(false)}
                disabled={saving}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={saving}>
                {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Approve
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Remove this assessment?"
        description="It comes off the register along with its hazard lines and acknowledgements."
        confirmText="Remove"
        variant="destructive"
        onConfirm={onDelete}
      />
    </div>
  );
}
