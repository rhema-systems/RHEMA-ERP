'use client';

import Link from 'next/link';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import {
  ArrowLeft,
  CheckCircle2,
  FileCheck2,
  Loader2,
  LockKeyhole,
  MailCheck,
  RefreshCw,
  Share2,
  ShieldCheck,
  Users,
} from 'lucide-react';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useWorkflowSummary } from '@/hooks/useWorkflowSummary';
import {
  buildInitialEvaluationLines,
  getRfqControlStage,
  getRfqOpeningBlocker,
  groupEvaluationOptions,
} from '@/lib/procurement-rfq-control';
import {
  rfqService,
  type ProcurementRfqControlDto,
  type SaveProcurementRfqEvaluationRequest,
} from '@/services/rfqService';
import { procurementAwardReadinessService } from '@/services/procurement-award-readiness.service';
import { hasAwardReadinessAction } from '@/lib/procurement-award-readiness';
import type { ProcurementAwardReadinessDecision } from '@/types/procurement-award-readiness';

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';
const formatAmount = (value: number) =>
  new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(value);

export default function ProcurementRfqControlsPage() {
  const params = useParams<{ id: string }>();
  const rfqId = params.id;
  const workflow = useWorkflowSummary({ entityType: 'TENDER_EVALUATION', entityId: rfqId });
  const { user, hasPermission } = useAuth();
  const [control, setControl] = useState<ProcurementRfqControlDto | null>(null);
  const [awardGate, setAwardGate] =
    useState<ProcurementAwardReadinessDecision | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [openingEvidence, setOpeningEvidence] = useState('');
  const [observerName, setObserverName] = useState('');
  const [observerRole, setObserverRole] = useState('Independent observer');
  const [observerSignature, setObserverSignature] = useState('');
  const [officerSignature, setOfficerSignature] = useState('');
  const [securityReferences, setSecurityReferences] = useState<
    Record<string, string>
  >({});
  const [awardMode, setAwardMode] = useState<'WinnerTakesAll' | 'SplitAward'>(
    'WinnerTakesAll'
  );
  const [evaluationReason, setEvaluationReason] = useState('');
  const [evaluationEvidence, setEvaluationEvidence] = useState('');
  const [evaluationLines, setEvaluationLines] = useState<
    SaveProcurementRfqEvaluationRequest['lines']
  >([]);
  const [approvalReference, setApprovalReference] = useState('');
  const [approvalComments, setApprovalComments] = useState('');

  const load = useCallback(async () => {
    if (!rfqId) return;
    try {
      setLoading(true);
      const next = await rfqService.getControls(rfqId);
      setControl(next);
      setAwardMode(next.evaluation?.awardMode ?? 'WinnerTakesAll');
      setEvaluationReason(next.evaluation?.recommendationReason ?? '');
      setEvaluationEvidence(next.evaluation?.evidenceReference ?? '');
      setEvaluationLines(buildInitialEvaluationLines(next));
      try {
        setAwardGate(
          await procurementAwardReadinessService.latest(
            'RequestForQuotation',
            rfqId
          )
        );
      } catch {
        setAwardGate(null);
      }
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Failed to load RFQ controls'
      );
    } finally {
      setLoading(false);
    }
  }, [rfqId]);

  useEffect(() => {
    void load();
  }, [load]);

  const stage = control ? getRfqControlStage(control) : null;
  const openingBlocker = control ? getRfqOpeningBlocker(control) : null;
  const evaluatedQuoteIds = new Set(
    control?.evaluation?.lines.map((line) => line.quoteId) ?? []
  );
  const awardGateAllows = Boolean(
    awardGate?.isReady &&
      awardGate.isCurrent &&
      hasAwardReadinessAction(awardGate.allowedActions, 'RecordAward') &&
      hasPermission('procurement.tender.approve') &&
      awardGate.recommendation.subjectIds.length > 0 &&
      awardGate.recommendation.subjectIds.every((id) =>
        evaluatedQuoteIds.has(id)
      )
  );
  const optionGroups = useMemo(
    () => groupEvaluationOptions(control?.evaluationOptions ?? []),
    [control?.evaluationOptions]
  );
  const actorName =
    [user?.firstName, user?.lastName].filter(Boolean).join(' ') ||
    user?.username ||
    'Opening officer';

  const run = async (
    key: string,
    action: () => Promise<unknown>,
    success: string
  ) => {
    try {
      setBusy(key);
      await action();
      toast.success(success);
      await load();
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'RFQ control action failed'
      );
    } finally {
      setBusy(null);
    }
  };

  const completeOpening = () => {
    if (!user?.id)
      return toast.error(
        'The authenticated opening officer could not be resolved.'
      );
    return run(
      'opening',
      () =>
        rfqService.completeOpening(rfqId, {
          evidenceReference: openingEvidence,
          participants: [
            {
              participantUserId: user.id,
              participantName: actorName,
              roleName: 'Opening officer',
              isObserver: false,
              signatureReference: officerSignature,
            },
            {
              participantName: observerName,
              roleName: observerRole,
              isObserver: true,
              signatureReference: observerSignature,
            },
          ],
          securities: Object.entries(securityReferences)
            .filter(([, reference]) => reference.trim())
            .map(([receiptId, securityReference]) => ({
              receiptId,
              securityReference,
            })),
        }),
      'The immutable opening register was completed.'
    );
  };

  const saveEvaluation = () =>
    run(
      'evaluation',
      () =>
        rfqService.saveEvaluation(rfqId, {
          awardMode,
          recommendationReason: evaluationReason,
          evidenceReference: evaluationEvidence,
          rowVersion: control?.evaluation?.rowVersion,
          lines: evaluationLines,
        }),
      'Evaluation recommendation saved.'
    );

  const submitCurrentEvaluation = () => {
    const rowVersion = control?.evaluation?.rowVersion;
    if (!rowVersion)
      return toast.error(
        'Reload the current Draft evaluation before submission.'
      );
    return run(
      'submit',
      () => rfqService.submitEvaluation(rfqId, rowVersion),
      workflow.visibility.direct
        ? 'Evaluation completed. No approval workflow was required.'
        : 'Evaluation submitted to the configured workflow.'
    );
  };

  const decideCurrentEvaluation = (action: 'Approve' | 'Reject') => {
    const rowVersion = control?.evaluation?.rowVersion;
    if (!rowVersion)
      return toast.error('Reload the submitted evaluation before deciding it.');
    return run(
      action.toLowerCase(),
      () =>
        rfqService.decideEvaluation(rfqId, {
          action,
          approvalReference,
          comments: approvalComments,
          rowVersion,
        }),
      action === 'Approve'
        ? 'Workflow approval step completed.'
        : 'Workflow rejection completed.'
    );
  };

  const handoffAward = () => {
    if (!awardGateAllows) {
      toast.error(
        'A current server-derived Ready decision for the exact RFQ recommendation is required.'
      );
      return;
    }
    const mode = control?.evaluation?.awardMode;
    if (!mode)
      return toast.error(
        'Reload the approved evaluation before award handoff.'
      );
    return run(
      'award',
      () => rfqService.awardAndCreatePurchaseOrders(rfqId, { mode }),
      control?.evaluation?.approvalRequired === false
        ? 'Completed RFQ recommendation handed off to purchase order creation.'
        : 'Approved RFQ award handed off to purchase order creation.'
    );
  };

  const updateEvaluationLine = (
    rfqItemId: string,
    changes: Partial<SaveProcurementRfqEvaluationRequest['lines'][number]>
  ) =>
    setEvaluationLines((current) =>
      current.map((line) =>
        line.rfqItemId === rfqItemId ? { ...line, ...changes } : line
      )
    );

  if (loading && !control) {
    return (
      <div className="flex min-h-[420px] items-center justify-center">
        <Loader2 className="h-7 w-7 animate-spin" />
      </div>
    );
  }

  if (!control) {
    return (
      <Card>
        <CardContent className="py-12 text-center text-sm text-muted-foreground">
          RFQ control history is unavailable.
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6" data-testid="rfq-control-workspace">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Button asChild variant="ghost" size="sm" className="-ml-3 mb-2">
            <Link href="/procurement/rfqs">
              <ArrowLeft className="mr-2 h-4 w-4" />
              RFQs
            </Link>
          </Button>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold">
              {control.rfqNumber} statutory controls
            </h1>
            <Badge variant="outline">{control.rfqStatus}</Badge>
            <Badge>{stage}</Badge>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            History-first RFQ receipt, opening, evaluation, approval, and award
            handoff.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button asChild variant="outline">
            <Link href={`/procurement/rfqs/${rfqId}/committee-controls`}>
              <Users className="mr-2 h-4 w-4" />
              Committee controls
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/procurement/rfqs/${rfqId}/award-readiness`}>
              <ShieldCheck className="mr-2 h-4 w-4" />
              Award readiness
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/procurement/rfqs/${rfqId}/bidder-communications`}>
              <MailCheck className="mr-2 h-4 w-4" />
              Bidder communications
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={`/procurement/rfqs/${rfqId}/ghaneps-exchange`}>
              <Share2 className="mr-2 h-4 w-4" />
              GHANEPS exchange
            </Link>
          </Button>
          <Button
            variant="outline"
            onClick={() => void load()}
            disabled={loading || busy !== null}
          >
            <RefreshCw
              className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`}
            />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Summary label="Method rule" value={control.methodRuleCode} />
        <Summary
          label="Qualified invitations"
          value={`${control.qualifiedInvitationCount} / ${control.minimumQuotationCount}`}
          good={
            control.qualifiedInvitationCount >= control.minimumQuotationCount
          }
        />
        <Summary
          label="On-time receipts"
          value={`${control.onTimeReceiptCount} / ${control.minimumQuotationCount}`}
          good={control.minimumCompetitionMet}
        />
        <Summary
          label="Late receipts"
          value={String(control.lateReceiptCount)}
        />
        <Summary
          label="Commercial envelope"
          value={control.quotesRemainSealed ? 'Sealed' : 'Opened'}
          good={!control.quotesRemainSealed}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <LockKeyhole className="h-4 w-4" />
            Immutable quotation receipt log
          </CardTitle>
        </CardHeader>
        <CardContent>
          {control.receipts.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No quotation receipts have been registered.
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="p-2">Receipt</th>
                    <th className="p-2">Received</th>
                    <th className="p-2">Disposition</th>
                    <th className="p-2">Commercial access</th>
                    <th className="p-2">Integrity</th>
                  </tr>
                </thead>
                <tbody>
                  {control.receipts.map((receipt) => (
                    <tr key={receipt.id} className="border-b last:border-0">
                      <td className="p-2 font-medium">
                        {receipt.receiptNumber}
                      </td>
                      <td className="p-2">
                        {formatDate(receipt.receivedAtUtc)}
                      </td>
                      <td className="p-2">
                        <Badge
                          variant={
                            receipt.disposition === 'OnTimeAccepted'
                              ? 'secondary'
                              : 'destructive'
                          }
                        >
                          {receipt.disposition}
                        </Badge>
                      </td>
                      <td className="p-2">
                        {receipt.openedAtUtc
                          ? `Opened ${formatDate(receipt.openedAtUtc)}`
                          : 'Sealed'}
                      </td>
                      <td className="p-2 font-mono text-xs">
                        {receipt.integrityHash.slice(0, 12)}…
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {!control.openingRegister ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Controlled opening session
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {openingBlocker ? (
              <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
                {openingBlocker}
              </div>
            ) : null}
            <div className="grid gap-4 md:grid-cols-2">
              <Field label="Opening evidence reference">
                <Input
                  value={openingEvidence}
                  onChange={(event) => setOpeningEvidence(event.target.value)}
                  placeholder="Signed register / evidence reference"
                />
              </Field>
              <Field label="Opening officer signature">
                <Input
                  value={officerSignature}
                  onChange={(event) => setOfficerSignature(event.target.value)}
                  placeholder="Signature evidence reference"
                />
              </Field>
              <Field label="Observer name">
                <Input
                  value={observerName}
                  onChange={(event) => setObserverName(event.target.value)}
                />
              </Field>
              <Field label="Observer role">
                <Input
                  value={observerRole}
                  onChange={(event) => setObserverRole(event.target.value)}
                />
              </Field>
              <Field label="Observer signature">
                <Input
                  value={observerSignature}
                  onChange={(event) => setObserverSignature(event.target.value)}
                  placeholder="Signature evidence reference"
                />
              </Field>
            </div>
            {control.receipts.length > 0 ? (
              <div className="space-y-2">
                <Label>Quotation security references (where applicable)</Label>
                {control.receipts.map((receipt) => (
                  <div
                    key={receipt.id}
                    className="grid gap-2 sm:grid-cols-[180px_1fr]"
                  >
                    <div className="self-center text-sm">
                      {receipt.receiptNumber}
                    </div>
                    <Input
                      value={securityReferences[receipt.id] ?? ''}
                      onChange={(event) =>
                        setSecurityReferences((current) => ({
                          ...current,
                          [receipt.id]: event.target.value,
                        }))
                      }
                      placeholder="Bid security / guarantee reference"
                    />
                  </div>
                ))}
              </div>
            ) : null}
            <Button
              onClick={() => void completeOpening()}
              disabled={
                Boolean(openingBlocker) ||
                busy !== null ||
                !openingEvidence ||
                !officerSignature ||
                !observerName ||
                !observerSignature
              }
            >
              {busy === 'opening' && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Complete and lock opening register
            </Button>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <FileCheck2 className="h-4 w-4" />
              Signed opening register
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-3">
              <Summary
                label="Closed"
                value={formatDate(control.openingRegister.closedAtUtc)}
              />
              <Summary
                label="Evidence"
                value={control.openingRegister.evidenceReference}
              />
              <Summary
                label="Integrity"
                value={`${control.openingRegister.integrityHash.slice(0, 16)}…`}
              />
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="p-2">Receipt / supplier</th>
                    <th className="p-2">Declared price</th>
                    <th className="p-2">Security</th>
                    <th className="p-2">Outcome</th>
                  </tr>
                </thead>
                <tbody>
                  {control.openingRegister.entries.map((entry) => (
                    <tr key={entry.id} className="border-b last:border-0">
                      <td className="p-2">
                        <div className="font-medium">{entry.receiptNumber}</div>
                        <div className="text-muted-foreground">
                          {entry.businessPartnerName}
                        </div>
                      </td>
                      <td className="p-2">
                        {formatAmount(entry.declaredAmount)}
                      </td>
                      <td className="p-2">{entry.securityReference || '—'}</td>
                      <td className="p-2">
                        {entry.rejectionReason || 'Accepted for evaluation'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="flex flex-wrap gap-2">
              {control.openingRegister.participants.map((participant) => (
                <Badge key={participant.id} variant="outline">
                  {participant.participantName} · {participant.roleName}
                  {participant.isObserver ? ' · observer' : ''}
                </Badge>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {control.openingRegister &&
      (!control.evaluation || control.evaluation.status === 'Draft') ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Evaluation and recommendation
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {!control.minimumCompetitionMet ? (
              <div className="rounded-md border border-red-300 bg-red-50 p-3 text-sm text-red-900">
                Evaluation is blocked until {control.minimumQuotationCount}{' '}
                on-time quotations are recorded.
              </div>
            ) : null}
            <div className="grid gap-4 md:grid-cols-2">
              <Field label="Award mode">
                <select
                  className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                  value={awardMode}
                  onChange={(event) =>
                    setAwardMode(event.target.value as typeof awardMode)
                  }
                >
                  <option value="WinnerTakesAll">Winner takes all</option>
                  <option value="SplitAward">Split award by line</option>
                </select>
              </Field>
              <Field label="Evaluation evidence reference">
                <Input
                  value={evaluationEvidence}
                  onChange={(event) =>
                    setEvaluationEvidence(event.target.value)
                  }
                />
              </Field>
            </div>
            <Field label="Recommendation rationale">
              <Textarea
                value={evaluationReason}
                onChange={(event) => setEvaluationReason(event.target.value)}
                rows={3}
              />
            </Field>
            <div className="space-y-3">
              {optionGroups.map((group) => {
                const line = evaluationLines.find(
                  (item) => item.rfqItemId === group.rfqItemId
                );
                return (
                  <div key={group.rfqItemId} className="rounded-md border p-3">
                    <div className="mb-3 font-medium">
                      Line {group.lineNumber}: {group.description}
                    </div>
                    <div className="grid gap-3 lg:grid-cols-5">
                      <Field label="Recommended quotation">
                        <select
                          className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                          value={line?.quoteId ?? ''}
                          onChange={(event) =>
                            updateEvaluationLine(group.rfqItemId, {
                              quoteId: event.target.value,
                            })
                          }
                        >
                          {group.options.map((option) => (
                            <option key={option.quoteId} value={option.quoteId}>
                              {option.businessPartnerName} ·{' '}
                              {formatAmount(option.lineTotal)}
                            </option>
                          ))}
                        </select>
                      </Field>
                      {(
                        [
                          'technicalScore',
                          'commercialScore',
                          'totalScore',
                        ] as const
                      ).map((key) => (
                        <Field
                          key={key}
                          label={
                            key === 'technicalScore'
                              ? 'Technical'
                              : key === 'commercialScore'
                                ? 'Commercial'
                                : 'Total score'
                          }
                        >
                          <Input
                            type="number"
                            min={0}
                            max={100}
                            value={line?.[key] ?? 0}
                            onChange={(event) =>
                              updateEvaluationLine(group.rfqItemId, {
                                [key]: Number(event.target.value),
                              })
                            }
                          />
                        </Field>
                      ))}
                      <Field label="Line rationale">
                        <Input
                          value={line?.recommendationReason ?? ''}
                          onChange={(event) =>
                            updateEvaluationLine(group.rfqItemId, {
                              recommendationReason: event.target.value,
                            })
                          }
                        />
                      </Field>
                    </div>
                  </div>
                );
              })}
            </div>
            <div className="flex flex-wrap gap-2">
              <Button
                onClick={() => void saveEvaluation()}
                disabled={
                  !control.minimumCompetitionMet ||
                  busy !== null ||
                  !evaluationReason ||
                  !evaluationEvidence ||
                  evaluationLines.some((line) => !line.quoteId)
                }
              >
                {busy === 'evaluation' && (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                )}
                Save recommendation
              </Button>
              {control.evaluation?.status === 'Draft' ? (
                <Button
                  variant="secondary"
                  onClick={() => void submitCurrentEvaluation()}
                  disabled={busy !== null || !workflow.visibility.known}
                >
                  {busy === 'submit' && (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  )}
                  {workflow.visibility.direct ? 'Complete evaluation' : 'Submit for approval'}
                </Button>
              ) : null}
            </div>
          </CardContent>
        </Card>
      ) : null}

      {control.evaluation ? (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <ShieldCheck className="h-4 w-4" />
              {control.evaluation.approvalRequired === false ? 'Recommendation and handoff' : 'Recommendation approval and handoff'}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-3 sm:grid-cols-4">
              <Summary
                label="Evaluation"
                value={control.evaluation.approvalRequired === false && control.evaluation.status === 'Approved' ? 'Complete' : control.evaluation.status}
                good={control.evaluation.status === 'Approved'}
              />
              <Summary
                label="Award mode"
                value={control.evaluation.awardMode}
              />
              {control.evaluation.approvalRequired !== false && <Summary
                label="Workflow"
                value={
                  control.evaluation.workflowInstanceId
                    ? `${control.evaluation.workflowInstanceId.slice(0, 12)}…`
                    : 'Not started'
                }
              />}
              <Summary
                label="Evidence"
                value={control.evaluation.evidenceReference}
              />
            </div>
            {workflow.error && <p role="alert" className="text-sm text-destructive">{workflow.error}</p>}
            {control.evaluation.status === 'Submitted' && workflow.visibility.showApprovalControls ? (
              <>
                <div className="grid gap-4 md:grid-cols-2">
                  <Field label="Approval reference">
                    <Input
                      value={approvalReference}
                      onChange={(event) =>
                        setApprovalReference(event.target.value)
                      }
                    />
                  </Field>
                  <Field label="Comments">
                    <Input
                      value={approvalComments}
                      onChange={(event) =>
                        setApprovalComments(event.target.value)
                      }
                    />
                  </Field>
                </div>
                <div className="flex gap-2">
                  <Button
                    onClick={() => void decideCurrentEvaluation('Approve')}
                    disabled={busy !== null || !approvalReference}
                  >
                    {busy === 'approve' && (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    )}
                    Approve
                  </Button>
                  <Button
                    variant="destructive"
                    onClick={() => void decideCurrentEvaluation('Reject')}
                    disabled={busy !== null || !approvalReference}
                  >
                    Reject
                  </Button>
                </div>
              </>
            ) : null}
            {control.evaluation.status === 'Approved' &&
            control.rfqStatus !== 'Awarded' ? (
              <div className="space-y-3">
                {!awardGateAllows && (
                  <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
                    Award handoff remains blocked until the immutable
                    award-readiness decision is current and Ready for the exact
                    recommended quote set.{' '}
                    <Link
                      className="font-medium underline"
                      href={`/procurement/rfqs/${rfqId}/award-readiness`}
                    >
                      Review readiness and remediation
                    </Link>
                    .
                  </p>
                )}
                <Button
                  onClick={() => void handoffAward()}
                  disabled={busy !== null || !awardGateAllows}
                >
                  {busy === 'award' ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                  )}
                  Create controlled LPO / purchase order
                </Button>
              </div>
            ) : null}
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}

function Summary({
  label,
  value,
  good,
}: {
  label: string;
  value: string;
  good?: boolean;
}) {
  return (
    <div
      className={`rounded-md border p-3 ${good ? 'border-emerald-300 bg-emerald-50/60' : 'bg-card'}`}
    >
      <div className="text-xs uppercase tracking-wide text-muted-foreground">
        {label}
      </div>
      <div className="mt-1 break-words font-medium">{value}</div>
    </div>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-1.5">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
