'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Loader2,
  Pencil,
  Trash2,
  Stamp,
  PauseCircle,
  PlayCircle,
  Lock,
  CheckCircle2,
  XCircle,
} from 'lucide-react';
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
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { safetyPermitService } from '@/services/hr/safety-permit.service';
import { safetyRiskAssessmentService } from '@/services/hr/safety-risk-assessment.service';
import { locationService } from '@/services/hr/location.service';
import {
  SHE_PERMIT_TYPE_OPTIONS,
  GAS_TEST_MANDATORY_TYPES,
  type ShePermitToWork,
  type ShePermitToWorkWorker,
  type ShePermitToWorkExtension,
  type ShePermitToWorkDocument,
} from '@/types/hr/safety-permits';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtTime = (v?: string | null) => (v ? v.slice(0, 5) : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);
const toTimeSpan = (v: string) => (v.length === 5 ? `${v}:00` : v);

function InfoRow({ label, value }: { label: string; value?: string | number | null }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
      <span className="text-muted-foreground shrink-0">{label}</span>
      <span className="text-right font-medium">{value ?? '—'}</span>
    </div>
  );
}

// ── Edit schema ──────────────────────────────────────────────────────────────

const editSchema = z.object({
  permitType: z.enum(['HotWork', 'ConfinedSpaceEntry', 'WorkingAtHeight', 'Excavation', 'ElectricalIsolation', 'ChemicalHandling', 'CriticalLift', 'Demolition', 'General', 'RoadClosure']),
  workDescription: z.string().min(1, 'Describe the work').max(300),
  locationId: z.string().optional().or(z.literal('')),
  specificArea: z.string().max(200).optional().or(z.literal('')),
  plannedStartDate: z.string().min(1),
  plannedStartTime: z.string().min(1),
  plannedEndDate: z.string().min(1),
  plannedEndTime: z.string().min(1),
  actualStartDate: z.string().optional().or(z.literal('')),
  actualEndDate: z.string().optional().or(z.literal('')),
  hazardsIdentified: z.string().max(2000).optional().or(z.literal('')),
  controlMeasures: z.string().max(2000).optional().or(z.literal('')),
  ppeRequired: z.string().max(1000).optional().or(z.literal('')),
  gasTestResults: z.string().max(500).optional().or(z.literal('')),
  isolationDetails: z.string().max(500).optional().or(z.literal('')),
  riskAssessmentId: z.string().optional().or(z.literal('')),
});
type EditForm = z.input<typeof editSchema>;

// ── Workflow schemas ─────────────────────────────────────────────────────────

const approveSchema = z.object({
  approvedById: z.string().min(1, 'Choose the approver'),
  approvedDate: z.string().min(1),
  issuedById: z.string().optional().or(z.literal('')),
  issuedDate: z.string().optional().or(z.literal('')),
});
type ApproveForm = z.input<typeof approveSchema>;

const suspendSchema = z.object({
  suspendedById: z.string().min(1, 'Who is suspending it?'),
  suspendedDate: z.string().min(1),
  suspensionReason: z.string().min(1, 'A reason is required').max(500),
});
type SuspendForm = z.input<typeof suspendSchema>;

const closeSchema = z.object({
  closedById: z.string().min(1, 'Who is closing it?'),
  closedDate: z.string().min(1),
  workCompletedSatisfactorily: z.boolean(),
  areaLeftSafe: z.boolean(),
  reinstatementNotes: z.string().max(500).optional().or(z.literal('')),
  closureNotes: z.string().max(1000).optional().or(z.literal('')),
});
type CloseForm = z.input<typeof closeSchema>;

// ── Child schemas ────────────────────────────────────────────────────────────

const workerSchema = z.object({
  employeeId: z.string().optional().or(z.literal('')),
  workerName: z.string().min(1, 'A name is required').max(200),
  companyName: z.string().max(100).optional().or(z.literal('')),
  tradeOrRole: z.string().max(100).optional().or(z.literal('')),
  briefed: z.boolean(),
  briefedDate: z.string().optional().or(z.literal('')),
  signedOff: z.boolean(),
  signedDate: z.string().optional().or(z.literal('')),
});
type WorkerForm = z.input<typeof workerSchema>;
const emptyWorker: WorkerForm = {
  employeeId: '',
  workerName: '',
  companyName: '',
  tradeOrRole: '',
  briefed: false,
  briefedDate: '',
  signedOff: false,
  signedDate: '',
};

const extensionSchema = z.object({
  newEndDate: z.string().min(1, 'The new end date is required'),
  newEndTime: z.string().min(1, 'The new end time is required'),
  reason: z.string().min(1, 'A reason is required').max(500),
  approvedById: z.string().min(1, 'Who approved the extension?'),
  approvedDate: z.string().min(1),
});
type ExtensionForm = z.input<typeof extensionSchema>;
const emptyExtension: ExtensionForm = {
  newEndDate: '',
  newEndTime: '17:00',
  reason: '',
  approvedById: '',
  approvedDate: new Date().toISOString().slice(0, 10),
};

const documentSchema = z.object({
  fileName: z.string().min(1, 'A file name is required').max(255),
  filePath: z.string().min(1, 'A file path is required').max(500),
  description: z.string().max(200).optional().or(z.literal('')),
  uploadedById: z.string().min(1, 'Who uploaded it?'),
});
type DocumentForm = z.input<typeof documentSchema>;
const emptyDocument: DocumentForm = { fileName: '', filePath: '', description: '', uploadedById: '' };

/**
 * One permit, end to end: the safety sections that gate approval (FR-PTW-002), the
 * approve → active → suspend/resume → close lifecycle, authorised workers, extensions and
 * documents. Expiry is not automated — the window is what the SHE team works from.
 */
export default function PermitDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editOpen, setEditOpen] = useState(false);
  const [approveOpen, setApproveOpen] = useState(false);
  const [suspendOpen, setSuspendOpen] = useState(false);
  const [resumeOpen, setResumeOpen] = useState(false);
  const [closeOpen, setCloseOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [saving, setSaving] = useState(false);

  const { data: permit, isLoading } = useQuery({
    queryKey: ['hr', 'safety-permit', id],
    queryFn: () => safetyPermitService.getById(id),
    enabled: !!id,
  });

  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const { data: riskAssessments = [] } = useQuery({
    queryKey: ['hr', 'safety-risk-assessments'],
    queryFn: () => safetyRiskAssessmentService.getAll(),
  });

  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) as any });
  const approveForm = useForm<ApproveForm>({
    resolver: zodResolver(approveSchema) as any,
    defaultValues: {
      approvedById: '',
      approvedDate: new Date().toISOString().slice(0, 10),
      issuedById: '',
      issuedDate: new Date().toISOString().slice(0, 10),
    },
  });
  const suspendForm = useForm<SuspendForm>({
    resolver: zodResolver(suspendSchema) as any,
    defaultValues: {
      suspendedById: '',
      suspendedDate: new Date().toISOString().slice(0, 10),
      suspensionReason: '',
    },
  });
  const closeForm = useForm<CloseForm>({
    resolver: zodResolver(closeSchema) as any,
    defaultValues: {
      closedById: '',
      closedDate: new Date().toISOString().slice(0, 10),
      workCompletedSatisfactorily: true,
      areaLeftSafe: true,
      reinstatementNotes: '',
      closureNotes: '',
    },
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'safety-permit', id] });

  const run = async (label: string, fn: () => Promise<unknown>, closeDialog: () => void) => {
    setSaving(true);
    try {
      await fn();
      await invalidate();
      toast({ title: label });
      closeDialog();
    } catch (e: any) {
      toast({ title: 'Error', description: e?.message || `${label} failed.`, variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const openEdit = (p: ShePermitToWork) => {
    editForm.reset({
      permitType: p.permitType,
      workDescription: p.workDescription,
      locationId: p.locationId ?? '',
      specificArea: p.specificArea ?? '',
      plannedStartDate: p.plannedStartDate.slice(0, 10),
      plannedStartTime: p.plannedStartTime.slice(0, 5),
      plannedEndDate: p.plannedEndDate.slice(0, 10),
      plannedEndTime: p.plannedEndTime.slice(0, 5),
      actualStartDate: p.actualStartDate ? p.actualStartDate.slice(0, 10) : '',
      actualEndDate: p.actualEndDate ? p.actualEndDate.slice(0, 10) : '',
      hazardsIdentified: p.hazardsIdentified ?? '',
      controlMeasures: p.controlMeasures ?? '',
      ppeRequired: p.ppeRequired ?? '',
      gasTestResults: p.gasTestResults ?? '',
      isolationDetails: p.isolationDetails ?? '',
      riskAssessmentId: p.riskAssessmentId ?? '',
    });
    setEditOpen(true);
  };

  const onSaveEdit = (values: EditForm) => {
    const v = editSchema.parse(values);
    return run(
      'Saved',
      () =>
        safetyPermitService.update(id, {
          id,
          permitType: v.permitType,
          workDescription: v.workDescription,
          locationId: blank(v.locationId),
          specificArea: blank(v.specificArea),
          plannedStartDate: v.plannedStartDate,
          plannedStartTime: toTimeSpan(v.plannedStartTime),
          plannedEndDate: v.plannedEndDate,
          plannedEndTime: toTimeSpan(v.plannedEndTime),
          actualStartDate: blank(v.actualStartDate),
          actualEndDate: blank(v.actualEndDate),
          hazardsIdentified: blank(v.hazardsIdentified),
          controlMeasures: blank(v.controlMeasures),
          ppeRequired: blank(v.ppeRequired),
          gasTestResults: blank(v.gasTestResults),
          isolationDetails: blank(v.isolationDetails),
          riskAssessmentId: blank(v.riskAssessmentId),
        }),
      () => setEditOpen(false),
    );
  };

  if (isLoading || !permit) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  const canApprove = permit.status === 'Draft' || permit.status === 'PendingApproval';
  const canSuspend = permit.status === 'Active';
  const canResume = permit.status === 'Suspended';
  const canClose = permit.status === 'Active' || permit.status === 'Suspended';
  const canEdit = permit.status !== 'Completed' && permit.status !== 'Cancelled';
  const gasMandatory = GAS_TEST_MANDATORY_TYPES.includes(permit.permitType);

  const missing: string[] = [];
  if (!permit.hazardsIdentified?.trim()) missing.push('hazards identified');
  if (!permit.controlMeasures?.trim()) missing.push('control measures');
  if (gasMandatory && !permit.gasTestResults?.trim()) missing.push('gas test results');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={permit.permitNumber}
        description={`${permit.permitTypeName} · ${permit.workDescription}`}
        backHref="/hr/safety/permits"
        actions={
          <div className="flex flex-wrap gap-2">
            {canApprove && (
              <Button onClick={() => setApproveOpen(true)}>
                <Stamp className="mr-2 h-4 w-4" />
                Approve & issue
              </Button>
            )}
            {canSuspend && (
              <Button variant="outline" onClick={() => setSuspendOpen(true)}>
                <PauseCircle className="mr-2 h-4 w-4" />
                Suspend
              </Button>
            )}
            {canResume && (
              <Button onClick={() => setResumeOpen(true)}>
                <PlayCircle className="mr-2 h-4 w-4" />
                Resume
              </Button>
            )}
            {canClose && (
              <Button variant="outline" onClick={() => setCloseOpen(true)}>
                <Lock className="mr-2 h-4 w-4" />
                Close out
              </Button>
            )}
            {canEdit && (
              <Button variant="outline" onClick={() => openEdit(permit)}>
                <Pencil className="mr-2 h-4 w-4" />
                Edit
              </Button>
            )}
            <Button variant="outline" className="text-red-600" onClick={() => setDeleteOpen(true)}>
              <Trash2 className="mr-2 h-4 w-4" />
              Remove
            </Button>
          </div>
        }
      />

      {canApprove && missing.length > 0 && (
        <p className="rounded-md bg-amber-50 p-3 text-sm text-amber-900 dark:bg-amber-950 dark:text-amber-200">
          Approval is blocked until these mandatory sections are completed: {missing.join(', ')}.
        </p>
      )}
      {permit.isSuspended && permit.suspensionReason && (
        <p className="rounded-md bg-red-50 p-3 text-sm text-red-900 dark:bg-red-950 dark:text-red-200">
          Suspended {fmtDate(permit.suspendedDate)}
          {permit.suspendedByName ? ` by ${permit.suspendedByName}` : ''}: {permit.suspensionReason}
        </p>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Permit</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-baseline justify-between gap-4 border-b py-1.5 text-sm">
              <span className="text-muted-foreground">Status</span>
              <span className="flex items-center gap-2">
                {permit.isSuspended && <Badge variant="destructive">Suspended</Badge>}
                <StatusBadge status={permit.statusName} />
              </span>
            </div>
            <InfoRow label="Location" value={permit.locationName} />
            <InfoRow label="Specific area" value={permit.specificArea} />
            <InfoRow label="Requested by" value={permit.requestedByName} />
            <InfoRow label="Requested" value={fmtDate(permit.requestedDate)} />
            <InfoRow label="Contractor" value={permit.contractorName} />
            <InfoRow label="Linked risk assessment" value={permit.riskAssessmentNumber} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Window & authorisation</CardTitle>
          </CardHeader>
          <CardContent>
            <InfoRow
              label="Planned window"
              value={`${fmtDate(permit.plannedStartDate)} ${fmtTime(permit.plannedStartTime)} → ${fmtDate(permit.plannedEndDate)} ${fmtTime(permit.plannedEndTime)}`}
            />
            <InfoRow label="Actual start" value={fmtDate(permit.actualStartDate)} />
            <InfoRow label="Actual end" value={fmtDate(permit.actualEndDate)} />
            <InfoRow label="Approved by" value={permit.approvedByName} />
            <InfoRow label="Approved" value={fmtDate(permit.approvedDate)} />
            <InfoRow label="Issued by" value={permit.issuedByName} />
            <InfoRow label="Issued" value={fmtDate(permit.issuedDate)} />
            <p className="text-muted-foreground mt-3 text-xs">
              Expiry is not automated — permits past their window stay visible in the expiring
              queue until closed.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Safety sections</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <div>
              <p className="text-muted-foreground">Hazards identified</p>
              <p className="whitespace-pre-wrap font-medium">{permit.hazardsIdentified ?? '—'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Control measures</p>
              <p className="whitespace-pre-wrap font-medium">{permit.controlMeasures ?? '—'}</p>
            </div>
            <div>
              <p className="text-muted-foreground">PPE required</p>
              <p className="whitespace-pre-wrap font-medium">{permit.ppeRequired ?? '—'}</p>
            </div>
            <InfoRow
              label={gasMandatory ? 'Gas test (mandatory)' : 'Gas test'}
              value={permit.gasTestResults}
            />
            <InfoRow label="Isolation (LOTO)" value={permit.isolationDetails} />
          </CardContent>
        </Card>
      </div>

      {permit.status === 'Completed' && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Close-out</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-x-8 text-sm sm:grid-cols-2">
            <InfoRow label="Closed by" value={permit.closedByName} />
            <InfoRow label="Closed" value={fmtDate(permit.closedDate)} />
            <div className="flex items-baseline justify-between gap-4 border-b py-1.5">
              <span className="text-muted-foreground">Work completed satisfactorily</span>
              {permit.workCompletedSatisfactorily ? (
                <CheckCircle2 className="h-4 w-4 text-green-600" />
              ) : (
                <XCircle className="h-4 w-4 text-red-600" />
              )}
            </div>
            <div className="flex items-baseline justify-between gap-4 border-b py-1.5">
              <span className="text-muted-foreground">Area left safe</span>
              {permit.areaLeftSafe ? (
                <CheckCircle2 className="h-4 w-4 text-green-600" />
              ) : (
                <XCircle className="h-4 w-4 text-red-600" />
              )}
            </div>
            <InfoRow label="Reinstatement" value={permit.reinstatementNotes} />
            <InfoRow label="Closure notes" value={permit.closureNotes} />
          </CardContent>
        </Card>
      )}

      <Tabs defaultValue="workers">
        <TabsList>
          <TabsTrigger value="workers">Workers ({permit.authorisedWorkers.length})</TabsTrigger>
          <TabsTrigger value="extensions">Extensions ({permit.extensions.length})</TabsTrigger>
          <TabsTrigger value="documents">Documents ({permit.documents.length})</TabsTrigger>
        </TabsList>

        {/* ── Authorised workers ── */}
        <TabsContent value="workers" className="mt-4">
          <ResourceCollectionTab<ShePermitToWorkWorker, WorkerForm>
            parentId={id}
            title="workers"
            singular="worker"
            queryKey={['hr', 'safety-permit', id, 'workers']}
            invalidateKeys={[['hr', 'safety-permit', id]]}
            list={async () => (await safetyPermitService.getById(id)).authorisedWorkers}
            create={(permitId, values) => {
              const v = workerSchema.parse(values);
              return safetyPermitService.addWorker(permitId, {
                permitToWorkId: permitId,
                employeeId: blank(v.employeeId),
                workerName: v.workerName,
                companyName: blank(v.companyName),
                tradeOrRole: blank(v.tradeOrRole),
                briefed: v.briefed,
                briefedDate: blank(v.briefedDate),
                signedOff: v.signedOff,
                signedDate: blank(v.signedDate),
              });
            }}
            update={(_permitId, workerId, values) => {
              const v = workerSchema.parse(values);
              return safetyPermitService.updateWorker(workerId, {
                id: workerId,
                workerName: v.workerName,
                companyName: blank(v.companyName),
                tradeOrRole: blank(v.tradeOrRole),
                briefed: v.briefed,
                briefedDate: blank(v.briefedDate),
                signedOff: v.signedOff,
                signedDate: blank(v.signedDate),
              });
            }}
            remove={(_permitId, workerId) => safetyPermitService.removeWorker(workerId)}
            columns={[
              { header: 'Name', cell: (w) => <span className="font-medium">{w.workerName}</span> },
              { header: 'Company', cell: (w) => w.companyName ?? '—' },
              { header: 'Trade / role', cell: (w) => w.tradeOrRole ?? '—' },
              {
                header: 'Briefed',
                cell: (w) =>
                  w.briefed ? (
                    <span className="flex items-center gap-1 text-green-700 dark:text-green-400">
                      <CheckCircle2 className="h-4 w-4" />
                      {fmtDate(w.briefedDate)}
                    </span>
                  ) : (
                    <Badge variant="outline">Not briefed</Badge>
                  ),
              },
              {
                header: 'Signed off',
                cell: (w) =>
                  w.signedOff ? (
                    <span className="flex items-center gap-1 text-green-700 dark:text-green-400">
                      <CheckCircle2 className="h-4 w-4" />
                      {fmtDate(w.signedDate)}
                    </span>
                  ) : (
                    <Badge variant="outline">Pending</Badge>
                  ),
              },
            ]}
            schema={workerSchema}
            emptyForm={emptyWorker}
            toForm={(w) => ({
              employeeId: w.employeeId ?? '',
              workerName: w.workerName,
              companyName: w.companyName ?? '',
              tradeOrRole: w.tradeOrRole ?? '',
              briefed: w.briefed,
              briefedDate: w.briefedDate ? w.briefedDate.slice(0, 10) : '',
              signedOff: w.signedOff,
              signedDate: w.signedDate ? w.signedDate.slice(0, 10) : '',
            })}
            renderFields={(f, editing) => (
              <>
                {!editing && (
                  <EmployeePickerField
                    form={f}
                    name="employeeId"
                    label="Employee (leave empty for external workers)"
                  />
                )}
                <FieldRow>
                  <TextField form={f} name="workerName" label="Name" required />
                  <TextField form={f} name="companyName" label="Company" />
                </FieldRow>
                <TextField form={f} name="tradeOrRole" label="Trade / role" />
                <FieldRow>
                  <SwitchField form={f} name="briefed" label="Briefed" />
                  <DateField form={f} name="briefedDate" label="Briefed on" />
                </FieldRow>
                <FieldRow>
                  <SwitchField form={f} name="signedOff" label="Signed off" />
                  <DateField form={f} name="signedDate" label="Signed on" />
                </FieldRow>
              </>
            )}
            getId={(w) => w.id}
            emptyDescription="List everyone authorised to work under this permit, briefed and signed."
          />
        </TabsContent>

        {/* ── Extensions ── */}
        <TabsContent value="extensions" className="mt-4">
          <ResourceCollectionTab<ShePermitToWorkExtension, ExtensionForm>
            parentId={id}
            title="extensions"
            singular="extension"
            queryKey={['hr', 'safety-permit', id, 'extensions']}
            invalidateKeys={[['hr', 'safety-permit', id]]}
            list={async () =>
              [...(await safetyPermitService.getById(id)).extensions].sort(
                (a, b) => a.extensionNumber - b.extensionNumber,
              )
            }
            create={(permitId, values) => {
              const v = extensionSchema.parse(values);
              return safetyPermitService.addExtension(permitId, {
                permitToWorkId: permitId,
                newEndDate: v.newEndDate,
                newEndTime: toTimeSpan(v.newEndTime),
                reason: v.reason,
                approvedById: v.approvedById,
                approvedDate: v.approvedDate,
              });
            }}
            update={() => Promise.reject(new Error('Extensions cannot be edited'))}
            allowUpdate={false}
            allowCreate={permit.status === 'Active'}
            columns={[
              { header: '#', cell: (x) => <span className="tabular-nums">{x.extensionNumber}</span> },
              {
                header: 'New end',
                cell: (x) => `${fmtDate(x.newEndDate)} ${fmtTime(x.newEndTime)}`,
              },
              { header: 'Reason', cell: (x) => x.reason },
              { header: 'Approved by', cell: (x) => x.approvedByName },
              { header: 'Approved', cell: (x) => fmtDate(x.approvedDate) },
            ]}
            schema={extensionSchema}
            emptyForm={emptyExtension}
            toForm={(x) => ({
              newEndDate: x.newEndDate.slice(0, 10),
              newEndTime: x.newEndTime.slice(0, 5),
              reason: x.reason,
              approvedById: x.approvedById,
              approvedDate: x.approvedDate.slice(0, 10),
            })}
            renderFields={(f) => (
              <>
                <FieldRow>
                  <DateField form={f} name="newEndDate" label="New end date" required />
                  <TextField form={f} name="newEndTime" label="New end time (HH:mm)" required />
                </FieldRow>
                <TextareaField form={f} name="reason" label="Reason" rows={2} />
                <FieldRow>
                  <EmployeePickerField form={f} name="approvedById" label="Approved by" required />
                  <DateField form={f} name="approvedDate" label="Approved on" required />
                </FieldRow>
              </>
            )}
            getId={(x) => x.id}
            emptyDescription={
              permit.status === 'Active'
                ? 'Extend the validity window with an approved reason. Numbering is automatic.'
                : 'Only an active permit can be extended.'
            }
            dialogHint="The permit's planned end moves to the new date."
          />
        </TabsContent>

        {/* ── Documents ── */}
        <TabsContent value="documents" className="mt-4">
          <ResourceCollectionTab<ShePermitToWorkDocument, DocumentForm>
            parentId={id}
            title="documents"
            singular="document"
            queryKey={['hr', 'safety-permit', id, 'documents']}
            invalidateKeys={[['hr', 'safety-permit', id]]}
            list={async () => (await safetyPermitService.getById(id)).documents}
            create={(permitId, values) => {
              const v = documentSchema.parse(values);
              return safetyPermitService.addDocument(permitId, {
                permitToWorkId: permitId,
                fileName: v.fileName,
                filePath: v.filePath,
                description: blank(v.description),
                uploadedById: v.uploadedById,
              });
            }}
            update={() => Promise.reject(new Error('Documents cannot be edited'))}
            allowUpdate={false}
            remove={(_permitId, documentId) => safetyPermitService.removeDocument(documentId)}
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
            emptyDescription="Attach gas test certificates, isolation records and other evidence."
          />
        </TabsContent>
      </Tabs>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-[680px]">
          <form onSubmit={editForm.handleSubmit(onSaveEdit)}>
            <DialogHeader>
              <DialogTitle>Edit permit</DialogTitle>
              <DialogDescription>
                Completing the safety sections here is what unblocks approval.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <SelectField
                  form={editForm}
                  name="permitType"
                  label="Permit type"
                  required
                  options={SHE_PERMIT_TYPE_OPTIONS}
                />
                <SelectField
                  form={editForm}
                  name="locationId"
                  label="Location"
                  allowEmpty
                  options={locations.map((l) => ({ value: l.id, label: l.name }))}
                />
              </FieldRow>
              <TextareaField form={editForm} name="workDescription" label="Work description" rows={2} />
              <TextField form={editForm} name="specificArea" label="Specific area" />
              <FieldRow>
                <DateField form={editForm} name="plannedStartDate" label="Start date" required />
                <TextField form={editForm} name="plannedStartTime" label="Start time (HH:mm)" required />
              </FieldRow>
              <FieldRow>
                <DateField form={editForm} name="plannedEndDate" label="End date" required />
                <TextField form={editForm} name="plannedEndTime" label="End time (HH:mm)" required />
              </FieldRow>
              <FieldRow>
                <DateField form={editForm} name="actualStartDate" label="Actual start" />
                <DateField form={editForm} name="actualEndDate" label="Actual end" />
              </FieldRow>
              <TextareaField form={editForm} name="hazardsIdentified" label="Hazards identified" rows={3} />
              <TextareaField form={editForm} name="controlMeasures" label="Control measures" rows={3} />
              <TextareaField form={editForm} name="ppeRequired" label="PPE required" rows={2} />
              <FieldRow>
                <TextField form={editForm} name="gasTestResults" label="Gas test results" />
                <TextField form={editForm} name="isolationDetails" label="Isolation details (LOTO)" />
              </FieldRow>
              <SelectField
                form={editForm}
                name="riskAssessmentId"
                label="Linked risk assessment"
                allowEmpty
                emptyLabel="None"
                options={riskAssessments
                  .filter((r) => r.status === 'Approved' || r.status === 'Active')
                  .map((r) => ({ value: r.id, label: `${r.assessmentNumber} — ${r.title}` }))}
              />
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

      {/* ── Approve dialog ── */}
      <Dialog open={approveOpen} onOpenChange={setApproveOpen}>
        <DialogContent className="sm:max-w-[520px]">
          <form
            onSubmit={approveForm.handleSubmit((values) => {
              const v = approveSchema.parse(values);
              return run(
                'Permit approved',
                () =>
                  safetyPermitService.approve(id, {
                    permitId: id,
                    approvedById: v.approvedById,
                    approvedDate: v.approvedDate,
                    issuedById: blank(v.issuedById),
                    issuedDate: blank(v.issuedDate),
                  }),
                () => setApproveOpen(false),
              );
            })}
          >
            <DialogHeader>
              <DialogTitle>Approve & issue {permit.permitNumber}</DialogTitle>
              <DialogDescription>
                Approval activates the permit. The server refuses until the mandatory safety
                sections are complete.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <EmployeePickerField form={approveForm} name="approvedById" label="Approved by" required />
                <DateField form={approveForm} name="approvedDate" label="Approval date" required />
              </FieldRow>
              <FieldRow>
                <EmployeePickerField form={approveForm} name="issuedById" label="Issued by" />
                <DateField form={approveForm} name="issuedDate" label="Issue date" />
              </FieldRow>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setApproveOpen(false)} disabled={saving}>
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

      {/* ── Suspend dialog ── */}
      <Dialog open={suspendOpen} onOpenChange={setSuspendOpen}>
        <DialogContent className="sm:max-w-[520px]">
          <form
            onSubmit={suspendForm.handleSubmit((values) => {
              const v = suspendSchema.parse(values);
              return run(
                'Permit suspended',
                () =>
                  safetyPermitService.suspend(id, {
                    permitId: id,
                    suspendedById: v.suspendedById,
                    suspendedDate: v.suspendedDate,
                    suspensionReason: v.suspensionReason,
                  }),
                () => setSuspendOpen(false),
              );
            })}
          >
            <DialogHeader>
              <DialogTitle>Suspend {permit.permitNumber}</DialogTitle>
              <DialogDescription>Work stops until the permit is resumed.</DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <EmployeePickerField form={suspendForm} name="suspendedById" label="Suspended by" required />
                <DateField form={suspendForm} name="suspendedDate" label="Date" required />
              </FieldRow>
              <TextareaField form={suspendForm} name="suspensionReason" label="Reason" rows={2} />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setSuspendOpen(false)} disabled={saving}>
                Cancel
              </Button>
              <Button type="submit" variant="destructive" disabled={saving}>
                {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Suspend
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── Close dialog ── */}
      <Dialog open={closeOpen} onOpenChange={setCloseOpen}>
        <DialogContent className="sm:max-w-[560px]">
          <form
            onSubmit={closeForm.handleSubmit((values) => {
              const v = closeSchema.parse(values);
              return run(
                'Permit closed',
                () =>
                  safetyPermitService.close(id, {
                    permitId: id,
                    closedById: v.closedById,
                    closedDate: v.closedDate,
                    workCompletedSatisfactorily: v.workCompletedSatisfactorily,
                    areaLeftSafe: v.areaLeftSafe,
                    reinstatementNotes: blank(v.reinstatementNotes),
                    closureNotes: blank(v.closureNotes),
                  }),
                () => setCloseOpen(false),
              );
            })}
          >
            <DialogHeader>
              <DialogTitle>Close out {permit.permitNumber}</DialogTitle>
              <DialogDescription>
                Confirms the work is done and the area reinstated. A closed permit refuses edits.
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4 py-4">
              <FieldRow>
                <EmployeePickerField form={closeForm} name="closedById" label="Closed by" required />
                <DateField form={closeForm} name="closedDate" label="Close date" required />
              </FieldRow>
              <SwitchField
                form={closeForm}
                name="workCompletedSatisfactorily"
                label="Work completed satisfactorily"
              />
              <SwitchField form={closeForm} name="areaLeftSafe" label="Area left safe" />
              <TextareaField form={closeForm} name="reinstatementNotes" label="Reinstatement notes" rows={2} />
              <TextareaField form={closeForm} name="closureNotes" label="Closure notes" rows={2} />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setCloseOpen(false)} disabled={saving}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving}>
                {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Close permit
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={resumeOpen}
        onOpenChange={setResumeOpen}
        title={`Resume ${permit.permitNumber}?`}
        description="Work may restart under the existing controls."
        confirmText="Resume"
        onConfirm={() => run('Permit resumed', () => safetyPermitService.resume(id), () => setResumeOpen(false))}
      />

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Remove this permit?"
        description="It comes off the register along with its workers, extensions and documents."
        confirmText="Remove"
        variant="destructive"
        onConfirm={async () => {
          try {
            await safetyPermitService.remove(id);
            toast({ title: 'Removed', description: 'Permit removed.' });
            router.push('/hr/safety/permits');
          } catch (e: any) {
            toast({ title: 'Error', description: e?.message || 'Failed to remove.', variant: 'destructive' });
          }
        }}
      />
    </div>
  );
}
