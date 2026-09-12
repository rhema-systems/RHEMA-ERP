'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import {
  Check,
  CheckCircle2,
  FileText,
  Loader2,
  Minus,
  Pencil,
  Printer,
  Send,
  ShieldCheck,
  Trash2,
  Undo2,
  XCircle,
} from 'lucide-react';
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  TextField,
  TextareaField,
  SelectField,
  SwitchField,
  DateField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { safetyEnvironmentalComplianceService } from '@/services/hr/safety-environmental-compliance.service';
import { SHE_ENV_WORK_CLASSIFICATION_OPTIONS } from '@/types/hr/safety-environment-compliance';
import type {
  SheEnvironmentalReview,
  SheEnvironmentalReviewStatus,
  SheEnvironmentalClearanceReport,
  SheEnvironmentalWorkClassification,
} from '@/types/hr/safety-environment-compliance';

/**
 * One environmental compliance review (FR-ENV-001–016): the full screening → approval →
 * clearance → commencement lifecycle, with the FR-ENV-011 audit trail. The server enforces
 * every gate — the buttons here only hide the obviously wrong moves.
 */
const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const blank = (v?: string) => (v && v.length > 0 ? v : null);

const editSchema = z.object({
  projectName: z.string().min(1, 'A project name is required').max(300),
  workClassification: z.string().min(1),
  projectReference: z.string().max(200).optional().or(z.literal('')),
  responsibleManagerId: z.string().optional().or(z.literal('')),
  plannedStartDate: z.string().optional().or(z.literal('')),
  description: z.string().min(1, 'A description is required').max(4000),
  applicableLaws: z.string().max(2000).optional().or(z.literal('')),
  permitRequired: z.boolean(),
  complianceChecklist: z.string().max(4000).optional().or(z.literal('')),
  notes: z.string().max(2000).optional().or(z.literal('')),
});
type EditForm = z.input<typeof editSchema>;

const screeningSchema = z.object({
  requiresRegistration: z.boolean(),
  requiresEnvironmentalPermit: z.boolean(),
  requiresFullEia: z.boolean(),
  requiresRiskAssessment: z.boolean(),
  requiresEpaSubmission: z.boolean(),
  requiresManagementApproval: z.boolean(),
  screeningNotes: z.string().max(2000).optional().or(z.literal('')),
});
type ScreeningForm = z.input<typeof screeningSchema>;

const SCREENING_FLAGS: { key: keyof ScreeningForm & string; label: string }[] = [
  { key: 'requiresRegistration', label: 'EPA registration required' },
  { key: 'requiresEnvironmentalPermit', label: 'Environmental permit required' },
  { key: 'requiresFullEia', label: 'Full EIA required' },
  { key: 'requiresRiskAssessment', label: 'Risk assessment required' },
  { key: 'requiresEpaSubmission', label: 'EPA submission required' },
  { key: 'requiresManagementApproval', label: 'Management approval required' },
];

function ReviewStatusBadge({
  status,
  statusName,
}: {
  status: SheEnvironmentalReviewStatus;
  statusName: string;
}) {
  switch (status) {
    case 'Submitted':
      return <Badge variant="secondary">{statusName}</Badge>;
    case 'CorrectionsRequested':
      return (
        <Badge variant="outline" className="border-amber-500 text-amber-600">
          {statusName}
        </Badge>
      );
    case 'Rejected':
      return <Badge variant="destructive">{statusName}</Badge>;
    case 'ClearanceIssued':
      return <Badge className="bg-green-600 text-white hover:bg-green-600">{statusName}</Badge>;
    default:
      return <Badge>{statusName}</Badge>;
  }
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-2 border-b py-1 text-sm">
      <dt className="text-muted-foreground shrink-0">{label}</dt>
      <dd className="text-right font-medium">{value}</dd>
    </div>
  );
}

function FlagRow({ label, value }: { label: string; value: boolean }) {
  return (
    <div className="flex items-center gap-2 py-1 text-sm">
      {value ? (
        <Check className="h-4 w-4 shrink-0 text-green-600" />
      ) : (
        <Minus className="text-muted-foreground h-4 w-4 shrink-0" />
      )}
      <span className={value ? 'font-medium' : 'text-muted-foreground'}>{label}</span>
    </div>
  );
}

type CommentAction = 'corrections' | 'approve' | 'reject' | 'mgmt' | 'clearance' | 'commence';

const COMMENT_ACTIONS: Record<
  CommentAction,
  { title: string; description: string; confirm: string; requiresComments: boolean; done: string }
> = {
  corrections: {
    title: 'Request corrections',
    description: 'Sends the review back to the submitter — say what needs fixing.',
    confirm: 'Request corrections',
    requiresComments: true,
    done: 'Corrections requested',
  },
  approve: {
    title: 'Approve review',
    description: 'The officer approval. Refused until the screening determination is recorded.',
    confirm: 'Approve',
    requiresComments: false,
    done: 'Review approved',
  },
  reject: {
    title: 'Reject review',
    description: 'A rejected review is compliance archive — the work does not proceed.',
    confirm: 'Reject',
    requiresComments: false,
    done: 'Review rejected',
  },
  mgmt: {
    title: 'Management approval',
    description: 'Records the management sign-off the screening determined was required.',
    confirm: 'Management approve',
    requiresComments: false,
    done: 'Management approval recorded',
  },
  clearance: {
    title: 'Issue clearance',
    description:
      'FR-ENV-016 — refused until every screening-determined gate is satisfied.',
    confirm: 'Issue clearance',
    requiresComments: false,
    done: 'Clearance issued',
  },
  commence: {
    title: 'Approve commencement',
    description: 'Confirms the cleared work may commence.',
    confirm: 'Approve commencement',
    requiresComments: false,
    done: 'Commencement approved',
  },
};

export default function EnvironmentalReviewDetailPage() {
  const params = useParams();
  const id = params.id as string;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [commentAction, setCommentAction] = useState<CommentAction | null>(null);
  const [comments, setComments] = useState('');
  const [screeningOpen, setScreeningOpen] = useState(false);
  const [epaOpen, setEpaOpen] = useState(false);
  const [epaDate, setEpaDate] = useState('');
  const [epaReference, setEpaReference] = useState('');
  const [epaNotes, setEpaNotes] = useState('');
  const [editOpen, setEditOpen] = useState(false);
  const [withdrawOpen, setWithdrawOpen] = useState(false);
  const [clearanceReport, setClearanceReport] = useState<SheEnvironmentalClearanceReport | null>(
    null,
  );
  const [busy, setBusy] = useState(false);

  const { data: review, isLoading } = useQuery({
    queryKey: ['hr', 'safety-env-compliance', 'reviews', 'detail', id],
    queryFn: () => safetyEnvironmentalComplianceService.getReview(id),
    enabled: !!id,
  });

  const screeningForm = useForm<ScreeningForm>({ resolver: zodResolver(screeningSchema) });
  const editForm = useForm<EditForm>({ resolver: zodResolver(editSchema) });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'safety-env-compliance'] });

  const act = async (title: string, fn: () => Promise<unknown>, close?: () => void) => {
    setBusy(true);
    try {
      await fn();
      await invalidate();
      toast({ title, description: review?.reviewNumber });
      close?.();
    } catch (err) {
      toast({
        title: 'Refused',
        description: (err as Error).message || 'The action failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }
  if (!review) {
    return (
      <div className="p-6">
        <EmptyState title="Review not found" description="Check the register." />
      </div>
    );
  }

  const r: SheEnvironmentalReview = review;
  const undecided = r.status === 'Submitted' || r.status === 'CorrectionsRequested';
  const screeningRecorded = !!r.screeningCompletedDate;
  const canRequestCorrections = r.status === 'Submitted';
  const canApprove = undecided;
  const canReject = undecided;
  const canManagementApprove =
    r.status === 'Approved' && r.requiresManagementApproval && !r.managementApprovedDate;
  const canRecordEpa =
    r.requiresEpaSubmission &&
    (r.status === 'Approved' || r.status === 'ClearanceIssued') &&
    !r.epaSubmissionDate;
  const canIssueClearance = r.status === 'Approved';
  const canApproveCommencement = r.status === 'ClearanceIssued' && !r.commencementApprovedDate;
  const canClearanceReport = r.status === 'Approved' || r.status === 'ClearanceIssued';

  const openScreening = () => {
    screeningForm.reset({
      requiresRegistration: r.requiresRegistration,
      requiresEnvironmentalPermit: r.requiresEnvironmentalPermit,
      requiresFullEia: r.requiresFullEia,
      requiresRiskAssessment: r.requiresRiskAssessment,
      requiresEpaSubmission: r.requiresEpaSubmission,
      requiresManagementApproval: r.requiresManagementApproval,
      screeningNotes: r.screeningNotes ?? '',
    });
    setScreeningOpen(true);
  };

  const openEdit = () => {
    editForm.reset({
      projectName: r.projectName,
      workClassification: r.workClassification,
      projectReference: r.projectReference ?? '',
      responsibleManagerId: r.responsibleManagerId ?? '',
      plannedStartDate: r.plannedStartDate?.slice(0, 10) ?? '',
      description: r.description,
      applicableLaws: r.applicableLaws ?? '',
      permitRequired: r.permitRequired,
      complianceChecklist: r.complianceChecklist ?? '',
      notes: r.notes ?? '',
    });
    setEditOpen(true);
  };

  const openCommentAction = (a: CommentAction) => {
    setComments('');
    setCommentAction(a);
  };

  const runCommentAction = () => {
    if (!commentAction) return;
    const c = blank(comments.trim() || undefined);
    const run = {
      corrections: () =>
        safetyEnvironmentalComplianceService.requestCorrections(r.id, comments.trim()),
      approve: () => safetyEnvironmentalComplianceService.approveReview(r.id, c),
      reject: () => safetyEnvironmentalComplianceService.rejectReview(r.id, c),
      mgmt: () => safetyEnvironmentalComplianceService.managementApprove(r.id, c),
      clearance: () => safetyEnvironmentalComplianceService.issueClearance(r.id, c),
      commence: () => safetyEnvironmentalComplianceService.approveCommencement(r.id, c),
    }[commentAction];
    void act(COMMENT_ACTIONS[commentAction].done, run, () => setCommentAction(null));
  };

  const submitScreening = screeningForm.handleSubmit((values) => {
    const v = screeningSchema.parse(values);
    void act(
      'Screening recorded',
      () =>
        safetyEnvironmentalComplianceService.recordScreening(r.id, {
          requiresRegistration: v.requiresRegistration,
          requiresEnvironmentalPermit: v.requiresEnvironmentalPermit,
          requiresFullEia: v.requiresFullEia,
          requiresRiskAssessment: v.requiresRiskAssessment,
          requiresEpaSubmission: v.requiresEpaSubmission,
          requiresManagementApproval: v.requiresManagementApproval,
          screeningNotes: blank(v.screeningNotes),
        }),
      () => setScreeningOpen(false),
    );
  });

  const submitEdit = editForm.handleSubmit((values) => {
    const v = editSchema.parse(values);
    void act(
      'Review updated',
      () =>
        safetyEnvironmentalComplianceService.updateReview(r.id, {
          id: r.id,
          projectName: v.projectName,
          workClassification: v.workClassification as SheEnvironmentalWorkClassification,
          projectReference: blank(v.projectReference),
          responsibleManagerId: blank(v.responsibleManagerId),
          plannedStartDate: blank(v.plannedStartDate),
          description: v.description,
          applicableLaws: blank(v.applicableLaws),
          permitRequired: v.permitRequired,
          complianceChecklist: blank(v.complianceChecklist),
          notes: blank(v.notes),
        }),
      () => setEditOpen(false),
    );
  });

  const submitEpa = () => {
    if (!epaDate) return;
    void act(
      'EPA submission recorded',
      () =>
        safetyEnvironmentalComplianceService.recordEpaSubmission(r.id, {
          submissionDate: epaDate,
          referenceNumber: blank(epaReference),
          notes: blank(epaNotes),
        }),
      () => setEpaOpen(false),
    );
  };

  const openClearanceReport = async () => {
    setBusy(true);
    try {
      const report = await safetyEnvironmentalComplianceService.getClearanceReport(r.id);
      setClearanceReport(report);
    } catch (err) {
      toast({
        title: 'Error',
        description: (err as Error).message || 'Loading the clearance report failed.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const dialogAction = commentAction ? COMMENT_ACTIONS[commentAction] : null;
  const rep = clearanceReport?.review;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={r.reviewNumber}
        description={r.projectName}
        backHref="/hr/safety/environmental/reviews"
        actions={
          <div className="flex flex-wrap justify-end gap-2">
            {canRequestCorrections && (
              <Button variant="outline" disabled={busy} onClick={() => openCommentAction('corrections')}>
                <Undo2 className="mr-2 h-4 w-4" /> Request corrections
              </Button>
            )}
            {canApprove && (
              <Button disabled={busy} onClick={() => openCommentAction('approve')}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> Approve
              </Button>
            )}
            {canReject && (
              <Button variant="destructive" disabled={busy} onClick={() => openCommentAction('reject')}>
                <XCircle className="mr-2 h-4 w-4" /> Reject
              </Button>
            )}
            {canManagementApprove && (
              <Button disabled={busy} onClick={() => openCommentAction('mgmt')}>
                <ShieldCheck className="mr-2 h-4 w-4" /> Management approve
              </Button>
            )}
            {canRecordEpa && (
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => {
                  setEpaDate(new Date().toISOString().slice(0, 10));
                  setEpaReference('');
                  setEpaNotes('');
                  setEpaOpen(true);
                }}
              >
                <Send className="mr-2 h-4 w-4" /> Record EPA submission
              </Button>
            )}
            {canIssueClearance && (
              <Button disabled={busy} onClick={() => openCommentAction('clearance')}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> Issue clearance
              </Button>
            )}
            {canApproveCommencement && (
              <Button disabled={busy} onClick={() => openCommentAction('commence')}>
                <CheckCircle2 className="mr-2 h-4 w-4" /> Approve commencement
              </Button>
            )}
            {canClearanceReport && (
              <Button variant="outline" disabled={busy} onClick={() => void openClearanceReport()}>
                <FileText className="mr-2 h-4 w-4" /> Clearance report
              </Button>
            )}
            {undecided && (
              <>
                <Button variant="outline" disabled={busy} onClick={openEdit}>
                  <Pencil className="mr-2 h-4 w-4" /> Edit
                </Button>
                <Button variant="outline" disabled={busy} onClick={() => setWithdrawOpen(true)}>
                  <Trash2 className="mr-2 h-4 w-4" /> Withdraw
                </Button>
              </>
            )}
          </div>
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        <ReviewStatusBadge status={r.status} statusName={r.statusName} />
        {r.permitRequired && <Badge variant="outline">Permit required</Badge>}
        {r.clearanceIssuedDate && <Badge variant="outline">Cleared {fmtDate(r.clearanceIssuedDate)}</Badge>}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Review</CardTitle>
          </CardHeader>
          <CardContent>
            <dl>
              <Row label="Project" value={r.projectName} />
              <Row label="Classification" value={r.workClassificationName} />
              <Row
                label="Project reference"
                value={r.projectReference ?? '—'}
              />
              <Row label="Unit" value={r.organizationUnitName ?? '—'} />
              <Row label="Responsible manager" value={r.responsibleManagerName ?? '—'} />
              <Row label="Submitted" value={`${r.submittedByName}, ${fmtDate(r.submittedDate)}`} />
              <Row label="Planned start" value={fmtDate(r.plannedStartDate)} />
              <Row label="Permit required" value={r.permitRequired ? 'Yes' : 'No'} />
            </dl>
            <p className="mt-3 text-sm">{r.description}</p>
            {r.applicableLaws && (
              <p className="text-muted-foreground mt-2 text-sm">Applicable laws: {r.applicableLaws}</p>
            )}
            {r.complianceChecklist && (
              <p className="text-muted-foreground mt-1 text-sm">Checklist: {r.complianceChecklist}</p>
            )}
            {r.notes && <p className="text-muted-foreground mt-1 text-sm">Notes: {r.notes}</p>}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0">
            <CardTitle className="text-base">Screening determination</CardTitle>
            <Button
              variant="outline"
              size="sm"
              disabled={busy || !undecided}
              onClick={openScreening}
            >
              Record screening
            </Button>
          </CardHeader>
          <CardContent>
            {SCREENING_FLAGS.map((f) => (
              <FlagRow
                key={f.key}
                label={f.label}
                value={r[f.key as keyof SheEnvironmentalReview] as boolean}
              />
            ))}
            {r.screeningNotes && (
              <p className="text-muted-foreground mt-2 text-sm">{r.screeningNotes}</p>
            )}
            <p className="text-muted-foreground mt-2 text-sm">
              {screeningRecorded
                ? `Screened by ${r.screenedByName ?? '—'}, ${fmtDate(r.screeningCompletedDate)}`
                : 'Screening not yet recorded — approval is refused until it is.'}
            </p>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Approvals & clearance</CardTitle>
        </CardHeader>
        <CardContent>
          <dl>
            <Row
              label="Officer approval"
              value={r.approvedDate ? `${r.approvedByName ?? '—'}, ${fmtDate(r.approvedDate)}` : '—'}
            />
            <Row
              label={`Management approval${r.requiresManagementApproval ? '' : ' (not required)'}`}
              value={
                r.managementApprovedDate
                  ? `${r.managementApprovedByName ?? '—'}, ${fmtDate(r.managementApprovedDate)}`
                  : '—'
              }
            />
            <Row
              label={`EPA submission${r.requiresEpaSubmission ? '' : ' (not required)'}`}
              value={
                r.epaSubmissionDate
                  ? `${r.epaSubmissionReference ?? 'no reference'}, ${fmtDate(r.epaSubmissionDate)}`
                  : '—'
              }
            />
            <Row
              label="Clearance"
              value={
                r.clearanceIssuedDate
                  ? `${r.clearanceIssuedByName ?? '—'}, ${fmtDate(r.clearanceIssuedDate)}`
                  : '—'
              }
            />
            <Row
              label="Commencement"
              value={
                r.commencementApprovedDate
                  ? `${r.commencementApprovedByName ?? '—'}, ${fmtDate(r.commencementApprovedDate)}`
                  : '—'
              }
            />
          </dl>
          {r.officerComments && (
            <p className="text-muted-foreground mt-3 text-sm">Officer comments: {r.officerComments}</p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Audit trail</CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {r.actions.length === 0 ? (
            <p className="text-muted-foreground px-6 pb-6 text-sm">No actions recorded.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>When</TableHead>
                  <TableHead>Action</TableHead>
                  <TableHead>Actor</TableHead>
                  <TableHead>Notes</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {r.actions.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell className="tabular-nums">{fmtDateTime(a.actionDate)}</TableCell>
                    <TableCell className="font-medium">{a.action}</TableCell>
                    <TableCell>{a.actorName}</TableCell>
                    <TableCell className="text-muted-foreground max-w-md text-sm">
                      {a.notes ?? '—'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* ── Comment-action dialog ── */}
      <Dialog open={commentAction !== null} onOpenChange={(o) => !busy && !o && setCommentAction(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{dialogAction?.title}</DialogTitle>
            <DialogDescription>{dialogAction?.description}</DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label>
              Comments{dialogAction?.requiresComments ? ' *' : ' (optional)'}
            </Label>
            <Textarea rows={3} value={comments} onChange={(e) => setComments(e.target.value)} />
          </div>
          <DialogFooter>
            <Button variant="outline" disabled={busy} onClick={() => setCommentAction(null)}>
              Cancel
            </Button>
            <Button
              variant={commentAction === 'reject' ? 'destructive' : 'default'}
              disabled={busy || (dialogAction?.requiresComments && comments.trim().length === 0)}
              onClick={runCommentAction}
            >
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {dialogAction?.confirm}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Screening dialog ── */}
      <Dialog open={screeningOpen} onOpenChange={(o) => !busy && setScreeningOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Record screening determination</DialogTitle>
            <DialogDescription>
              Which regulatory gates this work must pass. Approval is refused until this is
              recorded.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitScreening} className="space-y-3">
            {SCREENING_FLAGS.map((f) => (
              <SwitchField key={f.key} form={screeningForm} name={f.key} label={f.label} />
            ))}
            <TextareaField
              form={screeningForm}
              name="screeningNotes"
              label="Screening notes"
              rows={3}
            />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setScreeningOpen(false)}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={busy}>
                {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Record screening
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* ── EPA submission dialog ── */}
      <Dialog open={epaOpen} onOpenChange={(o) => !busy && setEpaOpen(o)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record EPA submission</DialogTitle>
            <DialogDescription>
              The screening determined an EPA submission is required — record when it went in.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Submission date *</Label>
              <Input type="date" value={epaDate} onChange={(e) => setEpaDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Reference number</Label>
              <Input value={epaReference} onChange={(e) => setEpaReference(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Notes</Label>
              <Textarea rows={2} value={epaNotes} onChange={(e) => setEpaNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" disabled={busy} onClick={() => setEpaOpen(false)}>
              Cancel
            </Button>
            <Button disabled={busy || !epaDate} onClick={submitEpa}>
              {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record submission
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── Edit dialog ── */}
      <Dialog open={editOpen} onOpenChange={(o) => !busy && setEditOpen(o)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>Edit {r.reviewNumber}</DialogTitle>
            <DialogDescription>
              Pre-decision edits only — once decided, the review is compliance archive.
            </DialogDescription>
          </DialogHeader>
          <form onSubmit={submitEdit} className="space-y-4">
            <TextField form={editForm} name="projectName" label="Project name" required />
            <FieldRow>
              <SelectField
                form={editForm}
                name="workClassification"
                label="Work classification"
                required
                options={SHE_ENV_WORK_CLASSIFICATION_OPTIONS}
              />
              <TextField form={editForm} name="projectReference" label="Project reference" />
            </FieldRow>
            <EmployeePickerField
              form={editForm}
              name="responsibleManagerId"
              label="Responsible manager"
              initialLabel={r.responsibleManagerName}
            />
            <DateField form={editForm} name="plannedStartDate" label="Planned start date" />
            <TextareaField form={editForm} name="description" label="Description" rows={3} required />
            <TextareaField form={editForm} name="applicableLaws" label="Applicable laws" rows={2} />
            <SwitchField form={editForm} name="permitRequired" label="Permit required" />
            <TextareaField
              form={editForm}
              name="complianceChecklist"
              label="Compliance checklist"
              rows={2}
            />
            <TextareaField form={editForm} name="notes" label="Notes" rows={2} />
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => setEditOpen(false)}
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

      {/* ── Clearance report dialog ── */}
      <Dialog open={clearanceReport !== null} onOpenChange={(o) => !o && setClearanceReport(null)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[680px]">
          <DialogHeader>
            <DialogTitle>Environmental clearance report</DialogTitle>
            <DialogDescription>
              A printable summary of the review, its screening determinations and approvals.
            </DialogDescription>
          </DialogHeader>
          {rep && (
            <div className="space-y-4 text-sm">
              <div>
                <p className="text-lg font-semibold">{rep.reviewNumber}</p>
                <p className="font-medium">{rep.projectName}</p>
                <p className="text-muted-foreground">
                  {rep.workClassificationName}
                  {rep.projectReference ? ` · Ref ${rep.projectReference}` : ''}
                </p>
              </div>
              <div>
                <p className="mb-1 font-semibold">Screening determinations</p>
                {SCREENING_FLAGS.map((f) => (
                  <FlagRow
                    key={f.key}
                    label={f.label}
                    value={rep[f.key as keyof SheEnvironmentalReview] as boolean}
                  />
                ))}
                {rep.screeningNotes && <p className="text-muted-foreground mt-1">{rep.screeningNotes}</p>}
                <p className="text-muted-foreground mt-1">
                  Screened by {rep.screenedByName ?? '—'}, {fmtDate(rep.screeningCompletedDate)}
                </p>
              </div>
              <div>
                <p className="mb-1 font-semibold">Approvals</p>
                <dl>
                  <Row
                    label="Officer approval"
                    value={
                      rep.approvedDate
                        ? `${rep.approvedByName ?? '—'}, ${fmtDate(rep.approvedDate)}`
                        : '—'
                    }
                  />
                  <Row
                    label="Management approval"
                    value={
                      rep.managementApprovedDate
                        ? `${rep.managementApprovedByName ?? '—'}, ${fmtDate(rep.managementApprovedDate)}`
                        : rep.requiresManagementApproval
                          ? 'Pending'
                          : 'Not required'
                    }
                  />
                  <Row
                    label="EPA submission"
                    value={
                      rep.epaSubmissionDate
                        ? `${rep.epaSubmissionReference ?? 'no reference'}, ${fmtDate(rep.epaSubmissionDate)}`
                        : rep.requiresEpaSubmission
                          ? 'Pending'
                          : 'Not required'
                    }
                  />
                  <Row
                    label="Clearance"
                    value={
                      rep.clearanceIssuedDate
                        ? `${rep.clearanceIssuedByName ?? '—'}, ${fmtDate(rep.clearanceIssuedDate)}`
                        : 'Not yet issued'
                    }
                  />
                  <Row
                    label="Commencement"
                    value={
                      rep.commencementApprovedDate
                        ? `${rep.commencementApprovedByName ?? '—'}, ${fmtDate(rep.commencementApprovedDate)}`
                        : '—'
                    }
                  />
                </dl>
              </div>
              <p className="text-muted-foreground">
                Generated {fmtDateTime(clearanceReport?.generatedAt)}
              </p>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setClearanceReport(null)}>
              Close
            </Button>
            <Button onClick={() => window.print()}>
              <Printer className="mr-2 h-4 w-4" /> Print
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={withdrawOpen}
        onOpenChange={setWithdrawOpen}
        title={`Withdraw ${r.reviewNumber}?`}
        description="This removes the undecided review. A decided review is compliance archive and cannot be withdrawn."
        confirmText="Withdraw"
        variant="destructive"
        onConfirm={async () => {
          try {
            await safetyEnvironmentalComplianceService.removeReview(r.id);
            await invalidate();
            toast({ title: 'Review withdrawn', description: r.reviewNumber });
            router.push('/hr/safety/environmental/reviews');
          } catch (err) {
            toast({
              title: 'Refused',
              description: (err as Error).message || 'Withdrawing failed.',
              variant: 'destructive',
            });
            return false;
          }
        }}
      />
    </div>
  );
}
