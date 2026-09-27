'use client';

import { use, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useToast } from '@/hooks/use-toast';
import {
  AlertTriangle,
  CheckCircle2,
  Info,
  Loader2,
  RefreshCw,
  ShieldAlert,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { FinancePostingCard } from '@/components/hr/common/FinancePostingCard';
import { DocumentUploadField } from '@/components/hr/common/DocumentUploadField';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { separationService } from '@/services/hr/separation.service';
import { ExitInterviewTab } from '@/components/hr/separations/exit-interview-tab';
import { SeparationMedicalBoardPanel } from '@/components/hr/separations/SeparationMedicalBoardPanel';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { WorkflowTabContent, WorkflowTabTrigger } from '@/components/workflow/WorkflowRecordTab';
import { useWorkflowRecord } from '@/hooks/useWorkflowRecord';
import type {
  SeparationDetail,
  SettlementLine,
  SeparationDocumentCategory,
  SeparationType,
  SettlementLineCategory,
} from '@/types/hr/separation';
import { PAY_LINE_CATEGORIES } from '@/types/hr/separation';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

const DOCUMENT_CATEGORIES: { value: SeparationDocumentCategory; label: string }[] = [
  { value: 'ResignationLetter', label: 'Resignation letter' },
  { value: 'AcceptanceOrNoticeLetter', label: 'Acceptance / notice letter' },
  { value: 'ClearanceForm', label: 'Signed clearance form' },
  { value: 'SettlementStatement', label: 'Settlement statement' },
  { value: 'ExitInterviewRecord', label: 'Exit interview record' },
  { value: 'MedicalReport', label: 'Medical report' },
  { value: 'DeathCertificate', label: 'Death certificate' },
  { value: 'Other', label: 'Other' },
];

/**
 * The two routes that cannot be submitted without a particular document. Used to tell somebody
 * that *before* the submit button refuses them.
 */
const DOC_REQUIRED_BY: Partial<Record<SeparationType, SeparationDocumentCategory>> = {
  MedicalRetirement: 'MedicalReport',
  Death: 'DeathCertificate',
};

const SETTLEMENT_CATEGORIES: { value: SettlementLineCategory; label: string }[] = [
  { value: 'UnpaidSalary', label: 'Unpaid salary' },
  { value: 'NoticePay', label: 'Notice pay' },
  { value: 'LeaveEncashment', label: 'Leave encashment' },
  { value: 'GratuityOrEndOfService', label: 'Gratuity / end of service' },
  { value: 'BenefitPayment', label: 'Benefit payment' },
  { value: 'PensionRelated', label: 'Pension-related' },
  { value: 'OtherEarning', label: 'Other earning' },
  { value: 'LoanRepayment', label: 'Loan repayment' },
  { value: 'SalaryAdvanceRecovery', label: 'Salary advance recovery' },
  { value: 'TravelAdvanceRecovery', label: 'Travel advance recovery' },
  { value: 'PropertyRecovery', label: 'Property recovery' },
  { value: 'TaxDeduction', label: 'Tax deduction' },
  { value: 'OtherDeduction', label: 'Other deduction' },
];

/**
 * ⚠ Which side of the statement a category falls on is STORED, not inferred, on the server — a
 * benefit can be a payment or a clawback. This list only sets the default for a line somebody adds
 * by hand, so the form does not start with the sign wrong.
 */
const DEDUCTION_CATEGORIES: SettlementLineCategory[] = [
  'LoanRepayment', 'SalaryAdvanceRecovery', 'TravelAdvanceRecovery',
  'PropertyRecovery', 'TaxDeduction', 'OtherDeduction',
];

const money = (amount: number | null | undefined, currency: string) =>
  amount == null ? null : `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className="text-sm">{children ?? '—'}</div>
    </div>
  );
}

export default function SeparationDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [error, setError] = useState<string | null>(null);
  const [reason, setReason] = useState('');
  const [docCategory, setDocCategory] = useState<SeparationDocumentCategory>('Other');
  const [editingLineId, setEditingLineId] = useState<string | null>(null);
  const [lineAmount, setLineAmount] = useState('');
  const [lineSource, setLineSource] = useState('');
  // Leave settings audit 2: on a pay line HR edits the days, never the amount.
  const [lineDays, setLineDays] = useState('');
  const [newLineDays, setNewLineDays] = useState('');
  const [newLineDescription, setNewLineDescription] = useState('');
  const [newLineCategory, setNewLineCategory] = useState<SettlementLineCategory>('OtherEarning');
  const [noticeChoice, setNoticeChoice] = useState<'waive' | 'payInLieu' | 'neither'>('neither');
  const [noticeReason, setNoticeReason] = useState('');

  const { data: separation, isLoading } = useQuery({
    queryKey: ['separation', id],
    queryFn: () => separationService.getById(id),
  });

  // Clearance and settlement are only fetched once the record has reached them — asking earlier
  // would 404 on the settlement and show an error for a state that is simply not there yet.
  const clearanceReached = separation
    && ['ClearanceInProgress', 'ClearanceCompleted', 'SettlementPending',
        'SettlementUnderReview', 'SettlementApproved', 'Completed'].includes(separation.status);

  const settlementReached = separation
    && ['SettlementPending', 'SettlementUnderReview', 'SettlementApproved', 'Completed']
      .includes(separation.status);

  // An interview belongs to an exit that is actually going ahead, so the tab only opens once the
  // separation has been approved.
  const approvedOrLater = separation
    && ['Approved', 'ClearanceInProgress', 'ClearanceCompleted', 'SettlementPending',
        'SettlementUnderReview', 'SettlementApproved', 'Completed'].includes(separation.status);

  /**
   * ⚠ Reports what it DID, not merely that it worked. A refresh that adds nothing is the normal
   * case, and a toast saying "done" leaves the user unable to tell that from a failure.
   */
  const refreshAssets = useMutation({
    mutationFn: () => separationService.refreshClearanceAssets(id),
    onSuccess: async (updated) => {
      const before = clearance?.totalItems ?? 0;
      const added = (updated?.totalItems ?? 0) - before;
      await queryClient.invalidateQueries({ queryKey: ['separation-clearance', id] });
      toast({
        title: added > 0
          ? `${added} line${added === 1 ? '' : 's'} added from the asset register`
          : 'Nothing new to add',
        description: added > 0
          ? 'Anything issued since the form was drawn is now on it. Lines already answered were left alone.'
          : 'Everything on the register is already on this form.',
      });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Could not re-read the asset register',
        description: e?.response?.data?.message ?? e?.message,
      }),
  });

  const { data: clearance } = useQuery({
    queryKey: ['separation-clearance', id],
    queryFn: () => separationService.getClearance(id),
    enabled: !!clearanceReached,
  });

  const { data: settlement } = useQuery({
    queryKey: ['separation-settlement', id],
    queryFn: () => separationService.getSettlement(id),
    enabled: !!settlementReached,
  });

  const { data: documents } = useQuery({
    queryKey: ['separation-documents', id],
    queryFn: () => separationService.getDocuments(id),
  });

  // Awaited, not fire-and-forget: the workflow hook's afterAction chains onto this, and an
  // approval that returns before the record has been refetched shows the old status for a beat.
  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['separation', id] }),
      queryClient.invalidateQueries({ queryKey: ['separation-clearance', id] }),
      queryClient.invalidateQueries({ queryKey: ['separation-settlement', id] }),
    ]);
  };

  /**
   * FR-HR-092 runs on the generic workflow engine, so the Managing Director signs exits from the
   * same inbox as everything else rather than from a screen only this module has.
   *
   * ⚠ This page never sets a status. The service drives the engine and
   * `EmployeeSeparationWorkflowStatusAdapter` maps the outcome onto the record — so we refetch and
   * let it decide. A screen that also wrote a status would be a second opinion about a decision the
   * engine owns.
   *
   * The notice settlement is deliberately NOT here: it is its own act, below, because the engine's
   * approve action carries a comment and nothing else, and that decision changes what the leaver is
   * paid.
   */
  const workflow = useWorkflowRecord({
    recallPrompt: 'reason',
    entityType: 'EmployeeSeparation',
    entityId: id,
    entityLabel: 'Separation',
    entityNumber: separation?.separationNumber,
    status: separation?.status ?? 'Draft',
    canSubmit: separation?.status === 'Draft',
    canApproveReject: separation?.status === 'PendingApproval',
    enabled: !!separation,
    commands: {
      submit: () => separationService.submit(id),
      approve: (ctx) => separationService.approve(id, { notes: ctx.comments || null }),
      reject: (ctx) => separationService.reject(id, { reason: ctx.comments || '' }),
      afterAction: refresh,
    },
    onOpenWorkflows: () => router.push('/administration/workflow'),
  });

  const act = useMutation({
    mutationFn: (fn: () => Promise<unknown>) => fn(),
    onSuccess: () => { setError(null); setReason(''); refresh(); },
    onError: (e: Error) => setError(e.message),
  });

  const run = (fn: () => Promise<unknown>) => act.mutate(fn);

  if (isLoading || !separation) {
    return (
      <div className="flex items-center justify-center py-24 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  const s: SeparationDetail = separation;

  return (
    <div className="space-y-6">
      <PageHeader
        title={s.separationNumber}
        description={`${s.employeeName} — ${s.separationTypeName}`}
        backHref="/hr/separations"
        actions={<Badge variant="secondary">{s.statusName}</Badge>}
      />

      {error && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/*
        ⚠ Completed but never applied — the defect this area exists to close. Shown at the top of
        the record, not buried in a tab: somebody is recorded as still employed after leaving.
      */}
      {s.status === 'Completed' && !s.employeeRecordUpdated && (
        <Alert variant="destructive">
          <ShieldAlert className="h-4 w-4" />
          <AlertDescription>
            This separation is complete but was never applied to the employee’s record — they are
            still counted as active staff. Raise this with an administrator.
          </AlertDescription>
        </Alert>
      )}

      {/*
        FR-HR-092 made visible BEFORE somebody is refused. A screen that offers a sign button to
        whoever can see the record teaches people that the system is arbitrary.
      */}
      {s.status === 'PendingApproval' && (
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            {s.requiresManagingDirectorSignature
              ? 'This separation needs the Managing Director’s signature. HR may approve only a termination for absence beyond the organisation’s threshold (FR-HR-092).'
              : 'This is a procedural separation — absence beyond the organisation’s threshold — so HR may approve it without the Managing Director.'}
          </AlertDescription>
        </Alert>
      )}

      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="clearance">Clearance</TabsTrigger>
          <TabsTrigger value="settlement">Settlement</TabsTrigger>
          <TabsTrigger value="exit-interview">Exit interview</TabsTrigger>
          <WorkflowTabTrigger value="workflow" />
          <TabsTrigger value="documents">Documents ({documents?.length ?? 0})</TabsTrigger>
        </TabsList>

        {/* ── Overview ─────────────────────────────────────────────────────── */}
        <TabsContent value="overview" className="space-y-4">
          <Card>
            <CardHeader><CardTitle className="text-base">The exit</CardTitle></CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              <Field label="Employee">{s.employeeName}</Field>
              <Field label="Employee number">{s.employeeNumber}</Field>
              <Field label="Position">{s.positionTitle}</Field>
              <Field label="Unit">{s.organizationUnitName}</Field>
              <Field label="Route out">{s.separationTypeName}</Field>
              <Field label="Reason">{s.reasonCategoryName}</Field>
              <Field label="Raised on">{fmtDate(s.initiatedOn)}</Field>
              <Field label="Raised by">
                {s.isSystemInitiated ? 'The system' : s.initiatedByName}
              </Field>
              <Field label="Last working day">{fmtDate(s.lastWorkingDay)}</Field>
              <Field label="Employment ends">{fmtDate(s.effectiveDate)}</Field>
              {s.reasonNotes && (
                <div className="sm:col-span-2 lg:col-span-3">
                  <Field label="Notes">
                    <p className="whitespace-pre-wrap">{s.reasonNotes}</p>
                  </Field>
                </div>
              )}
            </CardContent>
          </Card>

          {/*
            ⚠ Round 5, lane K-II-a: the control for `MedicalBoardId`, which nothing could set. Shown
            for a medical retirement, or whenever a board is still named (the type changed since).
          */}
          {(s.separationType === 'MedicalRetirement' || s.medicalBoardId) && (
            <SeparationMedicalBoardPanel separation={s} onChanged={refresh} />
          )}

          <Card>
            <CardHeader><CardTitle className="text-base">Notice</CardTitle></CardHeader>
            <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="Given on">{fmtDate(s.noticeGivenOn)}</Field>
              <Field label="Required">{s.noticeRequiredDays} days</Field>
              {/* ⚠ Null means unknown, not zero — the dates it is counted from are missing. */}
              <Field label="Served">
                {s.noticeServedDays == null ? 'Not known yet' : `${s.noticeServedDays} days`}
              </Field>
              <Field label="Short by">
                {s.noticeShortfallDays == null ? '—' : `${s.noticeShortfallDays} days`}
              </Field>
              {s.isNoticeWaived && (
                <div className="sm:col-span-2 lg:col-span-4">
                  <Field label="Notice waived">{s.noticeWaiverReason}</Field>
                </div>
              )}
              {s.isNoticePaidInLieu && (
                <div className="sm:col-span-2 lg:col-span-4">
                  <Badge variant="outline">Unserved notice to be paid in lieu</Badge>
                </div>
              )}
            </CardContent>
          </Card>

          {(s.approvedById || s.rejectedById) && (
            <Card>
              <CardHeader><CardTitle className="text-base">The decision</CardTitle></CardHeader>
              <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {s.approvedById && <>
                  <Field label="Approved by">{s.approvedByName}</Field>
                  <Field label="Approved on">{fmtDate(s.approvedOn)}</Field>
                  <Field label="Notes">{s.approvalNotes}</Field>
                </>}
                {s.rejectedById && <>
                  <Field label="Refused by">{s.rejectedByName}</Field>
                  <Field label="Refused on">{fmtDate(s.rejectedOn)}</Field>
                  <Field label="Grounds">{s.rejectionReason}</Field>
                </>}
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader><CardTitle className="text-base">What happens next</CardTitle></CardHeader>
            <CardContent className="space-y-3">
              {/*
                Submit, approve, refuse and recall all come from the engine. W1: never build a
                module-specific approval UI — the MD signs exits in the same inbox as everything
                else, and a refusal recorded here shows up in the same audit trail as any other.
              */}
              <WorkflowApprovalActions {...workflow.actionProps} />

              {/*
                ⚠ The notice settlement is its own act, not part of approving. It decides money —
                waived notice costs nothing, notice paid in lieu is paid — and the approval it used
                to ride on now runs on the generic workflow engine, whose approve action carries a
                comment and nothing else. Recording "neither" is a real answer and is what clears
                the block; never deciding is what the block is for.
              */}
              {s.requiresNoticeDecision && (
                <div className="space-y-3 rounded-md border border-amber-300 p-3">
                  <div>
                    <p className="text-sm font-medium">
                      {s.noticeShortfallDays} day{s.noticeShortfallDays === 1 ? '' : 's'} of notice
                      were not served
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Say what happens to them before the settlement is prepared. Left undecided,
                      the statement simply omits notice pay that may be owed.
                    </p>
                  </div>
                  <Select
                    value={noticeChoice}
                    onValueChange={(v) => setNoticeChoice(v as typeof noticeChoice)}
                  >
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="neither">Neither — nothing is settled</SelectItem>
                      <SelectItem value="waive">Waive the balance</SelectItem>
                      <SelectItem value="payInLieu">Pay it in lieu</SelectItem>
                    </SelectContent>
                  </Select>
                  {noticeChoice === 'waive' && (
                    <div className="space-y-1.5">
                      <Label htmlFor="noticeReason">Why the notice is being waived</Label>
                      <Textarea
                        id="noticeReason"
                        rows={2}
                        value={noticeReason}
                        onChange={(e) => setNoticeReason(e.target.value)}
                      />
                    </div>
                  )}
                  <Button
                    onClick={() => run(() => separationService.recordNoticeDecision(id, {
                      waiveNotice: noticeChoice === 'waive',
                      payNoticeInLieu: noticeChoice === 'payInLieu',
                      reason: noticeChoice === 'waive' ? noticeReason : null,
                    }))}
                    disabled={act.isPending || (noticeChoice === 'waive' && !noticeReason.trim())}
                  >
                    Record the notice decision
                  </Button>
                </div>
              )}

              {s.status === 'Approved' && (
                <Button onClick={() => run(() => separationService.startClearance(id))} disabled={act.isPending}>
                  Start clearance
                </Button>
              )}

              {s.status === 'ClearanceCompleted' && (
                <Button onClick={() => run(() => separationService.prepareSettlement(id))} disabled={act.isPending}>
                  Prepare the settlement
                </Button>
              )}

              {s.status === 'SettlementApproved' && (
                <div className="space-y-2">
                  <Button onClick={() => run(() => separationService.complete(id))} disabled={act.isPending}>
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Complete and apply to the employee record
                  </Button>
                  <p className="text-xs text-muted-foreground">
                    This is the step that marks the employee as having left. Until it is done they
                    remain on strength.
                  </p>
                </div>
              )}

              {['Completed', 'Cancelled', 'Rejected'].includes(s.status) && (
                <p className="text-sm text-muted-foreground">
                  Nothing further — this separation is {s.statusName.toLowerCase()}.
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Clearance ────────────────────────────────────────────────────── */}
        <TabsContent value="clearance" className="space-y-4">
          {!clearanceReached ? (
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                Clearance begins once the separation has been approved — FR-HR-091 requires the
                clearance form before entitlements are computed.
              </AlertDescription>
            </Alert>
          ) : !clearance ? (
            <div className="py-8 text-center text-muted-foreground">Loading the clearance form…</div>
          ) : (
            <>
              {/*
                FR-HR-183. The form is drawn once, at the moment clearance starts — anything issued
                to the leaver after that is simply absent from it, which is how a laptop handed over
                during someone's notice period leaves with them. The endpoint existed and had no
                caller of any kind.

                ⚠ Offered as a plain button because it is safe to press twice: it adds only what is
                missing, reprices only lines nobody has answered, and never touches an answered one.
              */}
              <div className="flex justify-end">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => refreshAssets.mutate()}
                  disabled={refreshAssets.isPending}
                >
                  {refreshAssets.isPending
                    ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    : <RefreshCw className="mr-2 h-4 w-4" />}
                  Re-read the asset register
                </Button>
              </div>

              <Card>
                <CardContent className="grid gap-4 pt-6 sm:grid-cols-3 lg:grid-cols-6">
                  <Field label="Lines">{clearance.totalItems}</Field>
                  <Field label="Cleared">{clearance.clearedItems}</Field>
                  <Field label="Blocked">{clearance.blockedItems}</Field>
                  <Field label="Waived">{clearance.waivedItems}</Field>
                  <Field label="Still outstanding">{clearance.mandatoryOutstanding}</Field>
                  <Field label="Owed">
                    {clearance.totalOutstandingAmount.toLocaleString(undefined, {
                      minimumFractionDigits: 2, maximumFractionDigits: 2,
                    })}
                  </Field>
                </CardContent>
              </Card>

              {clearance.blockedReason && (
                <Alert>
                  <Info className="h-4 w-4" />
                  <AlertDescription>{clearance.blockedReason}</AlertDescription>
                </Alert>
              )}

              <Card>
                <CardContent className="space-y-2 pt-6">
                  {clearance.items.map((item) => (
                    <div key={item.id} className="flex flex-wrap items-center justify-between gap-2 rounded border p-3">
                      <div>
                        <div className="font-medium">
                          {item.name}
                          {item.isMandatory && <span className="ml-1 text-xs text-muted-foreground">(required)</span>}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {[item.owningOrganizationUnitName, item.signedOffBy, item.notes]
                            .filter(Boolean).join(' · ') || item.kindName}
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        {item.outstandingAmount != null && (
                          <span className="text-sm tabular-nums">
                            {item.outstandingAmount.toLocaleString(undefined, {
                              minimumFractionDigits: 2, maximumFractionDigits: 2,
                            })}
                          </span>
                        )}
                        <Badge variant={item.status === 'Blocked' ? 'destructive' : 'secondary'}>
                          {item.statusName}
                        </Badge>
                      </div>
                    </div>
                  ))}
                </CardContent>
              </Card>

              {clearance.canComplete && (
                <Button onClick={() => run(() => separationService.completeClearance(id))} disabled={act.isPending}>
                  Complete the clearance
                </Button>
              )}
            </>
          )}
        </TabsContent>

        {/* ── Settlement ───────────────────────────────────────────────────── */}
        <TabsContent value="settlement" className="space-y-4">
          {!settlementReached ? (
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                The settlement is prepared once clearance is complete — FR-HR-091 puts the clearance
                form ahead of computing what is owed.
              </AlertDescription>
            </Alert>
          ) : !settlement ? (
            <div className="py-8 text-center text-muted-foreground">Loading the statement…</div>
          ) : (
            <>
              <Card>
                <CardContent className="grid gap-4 pt-6 sm:grid-cols-2 lg:grid-cols-4">
                  <Field label="Earnings">{money(settlement.grossEarnings, settlement.currencyCode)}</Field>
                  <Field label="Deductions">{money(settlement.totalDeductions, settlement.currencyCode)}</Field>
                  <Field label="Net payable">
                    <strong>{money(settlement.netPayable, settlement.currencyCode)}</strong>
                  </Field>
                  <Field label="Review">{settlement.reviewOutcomeName}</Field>
                </CardContent>
              </Card>

              {/* What Finance holds for this settlement: posted when Internal Audit releases it (lane 8, slice 3). */}
              <FinancePostingCard sourceDocumentId={settlement.id} invalidateKeys={[['separation-settlement', id]]} />

              {/*
                ⚠ The most important warning on this screen. A statement with unvalued lines has a
                net figure that is NOT the whole story, and somebody could sign it believing it is.
              */}
              {settlement.uncomputedLines > 0 && (() => {
                // Leave settings audit 2 (P2/P3): pay lines wait on Finance, not on HR.
                const awaitingFinance = settlement.lines.filter(
                  (l) => l.isPayLine && l.computation === 'CannotCompute',
                ).length;
                const other = settlement.uncomputedLines - awaitingFinance;
                return (
                  <Alert variant="destructive">
                    <AlertTriangle className="h-4 w-4" />
                    <AlertDescription>
                      The net figure above is incomplete.
                      {awaitingFinance > 0 &&
                        ` ${awaitingFinance} pay line(s) await Finance, which values them in Pay to value — HR records the days, not the money.`}
                      {other > 0 &&
                        ` ${other} other line(s) could not be valued: enter each amount with its source, or remove the line if nothing is owed.`}
                    </AlertDescription>
                  </Alert>
                );
              })()}

              {settlement.returnCount > 0 && (
                <Alert>
                  <Info className="h-4 w-4" />
                  <AlertDescription>
                    Returned by Internal Audit {settlement.returnCount} time(s).
                    {settlement.reviewNotes ? ` Findings: ${settlement.reviewNotes}` : ''}
                  </AlertDescription>
                </Alert>
              )}

              <Card>
                <CardHeader><CardTitle className="text-base">The statement</CardTitle></CardHeader>
                <CardContent className="space-y-2">
                  {settlement.lines.map((line: SettlementLine) => (
                    <div key={line.id} className="flex flex-wrap items-center justify-between gap-2 rounded border p-3">
                      <div className="min-w-[240px]">
                        <div className="font-medium">{line.description}</div>
                        <div className="text-xs text-muted-foreground">
                          {line.basis ?? line.categoryName}
                          {line.sourceReference ? ` · source: ${line.sourceReference}` : ''}
                        </div>
                      </div>
                      <div className="flex items-center gap-3">
                        {/*
                          ⚠ NEVER render an uncomputed line as 0.00. Null means "we do not know",
                          and a zero on a document somebody signs is a claim nobody checked.
                        */}
                        {line.days != null && (
                          <span className="text-sm text-muted-foreground tabular-nums">{line.days} day(s)</span>
                        )}
                        {line.computation === 'CannotCompute' ? (
                          line.isPayLine ? (
                            // Leave settings audit 2: HR records the days; Finance puts the money on them.
                            <Badge variant="outline" className="border-amber-400 text-amber-700 dark:text-amber-300">
                              Awaiting Finance
                            </Badge>
                          ) : (
                            <Badge variant="destructive">Not computed</Badge>
                          )
                        ) : (
                          <span className={`tabular-nums ${line.isDeduction ? 'text-rose-600 dark:text-rose-400' : ''}`}>
                            {line.isDeduction ? '−' : ''}{money(line.amount, settlement.currencyCode)}
                          </span>
                        )}
                        {line.computation === 'ManuallyEntered' && (
                          <Badge variant="outline" className="text-xs">Entered by hand</Badge>
                        )}
                        {line.computation === 'ValuedByFinance' && (
                          <Badge variant="outline" className="text-xs">
                            Valued by Finance{line.valuedByName ? ` — ${line.valuedByName}` : ''}
                          </Badge>
                        )}

                        {/*
                          Editable only while the statement is a draft awaiting finalisation. On a
                          pay line HR may change the DAYS (which sends it back to Finance), never the
                          amount; a pay line with no days has nothing for HR to change.
                          ⚠ Keyed on the status, not isFinalised: a statement Internal Audit
                          returned keeps its finalised date as history, and is editable again.
                        */}
                        {s.status === 'SettlementPending' && (
                          <>
                            {(!line.isPayLine || line.days != null) && (
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                  setEditingLineId(editingLineId === line.id ? null : line.id);
                                  setLineAmount(line.amount == null ? '' : String(line.amount));
                                  setLineSource(line.sourceReference ?? '');
                                  setLineDays(line.days == null ? '' : String(line.days));
                                }}
                              >
                                {line.isPayLine
                                  ? 'Amend the days'
                                  : line.computation === 'CannotCompute' ? 'Enter the amount' : 'Amend'}
                              </Button>
                            )}
                            {!line.isSystemGenerated && (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => run(() => separationService.deleteSettlementLine(line.id))}
                                disabled={act.isPending}
                              >
                                Remove
                              </Button>
                            )}
                          </>
                        )}
                      </div>

                      {editingLineId === line.id && line.isPayLine && (
                        <div className="mt-3 w-full space-y-3 rounded bg-muted/40 p-3">
                          <div className="w-[160px] space-y-1">
                            <Label className="text-xs">Days</Label>
                            <Input
                              type="number"
                              step="0.5"
                              min={0}
                              value={lineDays}
                              onChange={(e) => setLineDays(e.target.value)}
                            />
                          </div>
                          <p className="text-xs text-muted-foreground">
                            HR records the days; Finance values them. Changing the days after Finance
                            has valued the line clears its figure, and the line waits for Finance again.
                          </p>
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              disabled={act.isPending || lineDays === ''}
                              onClick={() => run(async () => {
                                await separationService.updateSettlementLine(line.id, { days: Number(lineDays) });
                                setEditingLineId(null);
                              })}
                            >
                              Save
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => setEditingLineId(null)}>
                              Cancel
                            </Button>
                          </div>
                        </div>
                      )}

                      {editingLineId === line.id && !line.isPayLine && (
                        <div className="mt-3 w-full space-y-3 rounded bg-muted/40 p-3">
                          <div className="flex flex-wrap gap-3">
                            <div className="w-[160px] space-y-1">
                              <Label className="text-xs">Amount</Label>
                              <Input
                                type="number"
                                step="0.01"
                                min={0}
                                value={lineAmount}
                                onChange={(e) => setLineAmount(e.target.value)}
                              />
                            </div>
                            <div className="min-w-[240px] flex-1 space-y-1">
                              <Label className="text-xs">Where the figure came from</Label>
                              <Input
                                value={lineSource}
                                onChange={(e) => setLineSource(e.target.value)}
                                placeholder="e.g. August payroll register, line 214"
                              />
                            </div>
                          </div>
                          {/*
                            ⚠ The source is required whenever an amount is supplied, and the server
                            refuses without it. FR-HR-185 puts Internal Audit in front of this
                            statement, and a figure nobody can trace is one they cannot check.
                          */}
                          <p className="text-xs text-muted-foreground">
                            Internal Audit reviews this statement before anybody is paid, so every
                            hand-entered figure has to say where it came from.
                          </p>
                          <div className="flex gap-2">
                            <Button
                              size="sm"
                              disabled={act.isPending || lineAmount === '' || !lineSource.trim()}
                              onClick={() => run(async () => {
                                await separationService.updateSettlementLine(line.id, {
                                  amount: Number(lineAmount),
                                  sourceReference: lineSource.trim(),
                                });
                                setEditingLineId(null);
                              })}
                            >
                              Save
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => setEditingLineId(null)}>
                              Cancel
                            </Button>
                          </div>
                        </div>
                      )}
                    </div>
                  ))}

                  {/* A line HR adds by hand — the FR-HR-184 items the system cannot work out. */}
                  {s.status === 'SettlementPending' && (
                    <div className="flex flex-wrap items-end gap-3 rounded border border-dashed p-3">
                      <div className="min-w-[220px] flex-1 space-y-1">
                        <Label className="text-xs">Add a line</Label>
                        <Input
                          value={newLineDescription}
                          onChange={(e) => setNewLineDescription(e.target.value)}
                          placeholder="What is it for?"
                        />
                      </div>
                      <div className="w-[200px] space-y-1">
                        <Label className="text-xs">Category</Label>
                        <Select
                          value={newLineCategory}
                          onValueChange={(v) => setNewLineCategory(v as SettlementLineCategory)}
                        >
                          <SelectTrigger><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {SETTLEMENT_CATEGORIES.map((c) => (
                              <SelectItem key={c.value} value={c.value}>{c.label}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      {PAY_LINE_CATEGORIES.includes(newLineCategory) && (
                        <div className="w-[120px] space-y-1">
                          <Label className="text-xs">Days (if any)</Label>
                          <Input
                            type="number"
                            step="0.5"
                            min={0}
                            value={newLineDays}
                            onChange={(e) => setNewLineDays(e.target.value)}
                          />
                        </div>
                      )}
                      <Button
                        size="sm"
                        disabled={act.isPending || !newLineDescription.trim()}
                        onClick={() => run(async () => {
                          await separationService.addSettlementLine(id, {
                            category: newLineCategory,
                            description: newLineDescription.trim(),
                            isDeduction: DEDUCTION_CATEGORIES.includes(newLineCategory),
                            days: PAY_LINE_CATEGORIES.includes(newLineCategory) && newLineDays !== ''
                              ? Number(newLineDays)
                              : null,
                          });
                          setNewLineDescription('');
                          setNewLineDays('');
                        })}
                      >
                        Add
                      </Button>
                      <p className="w-full text-xs text-muted-foreground">
                        {PAY_LINE_CATEGORIES.includes(newLineCategory)
                          ? 'Pay: added with its days and facts only. Finance values it in Pay to value, and the statement stays open until it has.'
                          : 'Added without an amount, as something owed but not yet valued — which holds the statement open until somebody supplies the figure.'}
                      </p>
                    </div>
                  )}
                </CardContent>
              </Card>

              {/* Statements prepared before leave settings audit 2 keep the rate HR worked out. */}
              {settlement.dailyRateBasis && (
                <p className="text-xs text-muted-foreground">
                  Rate basis (HR&apos;s, before Finance valued pay): {settlement.dailyRateBasis}
                </p>
              )}

              {settlement.canFinalise && (
                <Button onClick={() => run(() => separationService.finaliseSettlement(id))} disabled={act.isPending}>
                  Finalise for Internal Audit
                </Button>
              )}

              {settlement.blockedReason && !settlement.canFinalise && (
                <p className="text-sm text-muted-foreground">{settlement.blockedReason}</p>
              )}

              {/*
                FR-HR-185. Shown to everyone so the state is legible, but the buttons only work for
                Internal Audit — HR and the Managing Director are both refused by the server.
              */}
              {s.status === 'SettlementUnderReview' && (
                <Card>
                  <CardHeader><CardTitle className="text-base">Internal Audit review</CardTitle></CardHeader>
                  <CardContent className="space-y-3">
                    <Alert>
                      <Info className="h-4 w-4" />
                      <AlertDescription>
                        Only Internal Audit may pass or return this statement — not HR, and not the
                        Managing Director who approved the separation (FR-HR-185).
                      </AlertDescription>
                    </Alert>
                    <div className="space-y-2">
                      <Label>Findings</Label>
                      <Textarea rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Button
                        onClick={() => run(() => separationService.approveSettlementReview(id, { notes: reason || null }))}
                        disabled={act.isPending}
                      >
                        Pass — release for payment
                      </Button>
                      <Button
                        variant="destructive"
                        onClick={() => run(() => separationService.returnSettlement(id, { notes: reason }))}
                        disabled={act.isPending || !reason.trim()}
                      >
                        Return with findings
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              )}
            </>
          )}
        </TabsContent>

        {/* ── Documents ────────────────────────────────────────────────────── */}
        <TabsContent value="exit-interview" className="space-y-4">
          <ExitInterviewTab separationId={id} canRecord={!!approvedOrLater} />
        </TabsContent>

        <WorkflowTabContent
          value="workflow"
          entityType="EmployeeSeparation"
          entityId={id}
          entityLabel="Separation"
          entityNumber={s.separationNumber}
          status={s.status}
          onAfterAction={async () => {
            await refresh();
            await workflow.refresh();
          }}
        />

        <TabsContent value="documents" className="space-y-4">
          <Card>
            <CardHeader><CardTitle className="text-base">Attach a file</CardTitle></CardHeader>
            <CardContent className="space-y-4">
              {/*
                ⚠ The CATEGORY is not a label — it is what makes two routes work at all. A medical
                retirement cannot be submitted without a MedicalReport and a death without a
                DeathCertificate, so a picker that quietly defaults everything to "Other" would
                block those exits with no explanation. It is chosen before the file, deliberately.
              */}
              <div className="w-[280px] space-y-2">
                <Label>What is this?</Label>
                <Select
                  value={docCategory}
                  onValueChange={(v) => setDocCategory(v as SeparationDocumentCategory)}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {DOCUMENT_CATEGORIES.map((c) => (
                      <SelectItem key={c.value} value={c.value}>{c.label}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {DOC_REQUIRED_BY[s.separationType] === docCategory && (
                  <p className="text-xs text-muted-foreground">
                    {/* ⚠ Lane K-II-a: a medical retirement has a second way through. */}
                    {s.separationType === 'MedicalRetirement'
                      ? 'This separation cannot be submitted until this document is attached, or a medical board’s finding recommending retirement is linked on the Overview.'
                      : 'This separation cannot be submitted until this document is attached.'}
                  </p>
                )}
              </div>

              <DocumentUploadField
                label="File"
                endpoint={`/hr/separations/${id}/documents`}
                fields={{ category: docCategory }}
                onUploaded={() => {
                  queryClient.invalidateQueries({ queryKey: ['separation-documents', id] });
                }}
              />
            </CardContent>
          </Card>

          <Card>
            <CardContent className="space-y-2 pt-6">
              {(documents ?? []).length === 0 ? (
                <p className="py-6 text-center text-sm text-muted-foreground">
                  Nothing attached yet. A medical retirement needs its medical report and a death its
                  certificate before either can be submitted.
                </p>
              ) : (
                (documents ?? []).map((d) => (
                  <div key={d.id} className="flex items-center justify-between gap-2 rounded border p-3">
                    <div>
                      <div className="font-medium">{d.fileName}</div>
                      <div className="text-xs text-muted-foreground">
                        {[d.categoryName, d.uploadedByName, fmtDate(d.uploadedOn)].filter(Boolean).join(' · ')}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {d.isRegisteredInDms && <Badge variant="outline" className="text-xs">In the repository</Badge>}
                      {/*
                        Removable only while the separation is a draft. After submission the
                        attachments are part of what was approved and what the settlement was
                        computed against, so taking one away would rewrite the record silently.
                      */}
                      {s.status === 'Draft' && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => run(async () => {
                            await separationService.deleteDocument(d.id);
                            queryClient.invalidateQueries({ queryKey: ['separation-documents', id] });
                          })}
                          disabled={act.isPending}
                        >
                          Remove
                        </Button>
                      )}
                    </div>
                  </div>
                ))
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
