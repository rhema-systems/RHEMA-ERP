'use client';

import Link from 'next/link';
import { useCallback, useEffect, useId, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import {
  ArrowLeft,
  CheckCircle2,
  FileKey2,
  Loader2,
  LockKeyhole,
  RefreshCw,
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
  buildFinancialScores,
  buildTechnicalScores,
  getTenderControlReadiness,
  getTenderControlStatusLabel,
} from '@/lib/procurement-tender-control';
import { procurementTenderControlService as service } from '@/services/procurement-tender-control.service';
import { procurementAwardReadinessService } from '@/services/procurement-award-readiness.service';
import { hasAwardReadinessAction } from '@/lib/procurement-award-readiness';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import {
  ProcurementTenderControlStatus as Status,
  ProcurementTenderSubmissionDisposition as Disposition,
  ProcurementMethodType as Method,
  type ProcurementTenderControl,
} from '@/types/procurement-tender-control';
import type { ProcurementAwardReadinessDecision } from '@/types/procurement-award-readiness';

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';
const methodLabels: Partial<Record<Method, string>> = {
  [Method.NationalCompetitiveTendering]: 'NCT',
  [Method.InternationalCompetitiveTendering]: 'ICT',
  [Method.QualityBasedSelection]: 'QBS',
  [Method.QualityAndCostBasedSelection]: 'QCBS',
};
const methodLabel = (method: Method) =>
  methodLabels[method] ?? 'Controlled tender';
const isQualitySelection = (method: Method) =>
  method === Method.QualityBasedSelection ||
  method === Method.QualityAndCostBasedSelection;

export default function ProcurementTenderControlsPage() {
  const { id: tenderId } = useParams<{ id: string }>();
  const workflow = useWorkflowSummary({ entityType: 'TenderAward', entityId: tenderId });
  const { user, hasPermission } = useAuth();
  const [control, setControl] = useState<ProcurementTenderControl | null>(null);
  const [loadError, setLoadError] = useState<string>();
  const [awardGate, setAwardGate] =
    useState<ProcurementAwardReadinessDecision | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [opening, setOpening] = useState({
    evidence: '',
    observerName: '',
    observerRole: 'Independent observer',
    observerSignature: '',
    officerSignature: '',
  });
  const [technicalEvidence, setTechnicalEvidence] = useState('');
  const [technicalScores, setTechnicalScores] = useState<
    ReturnType<typeof buildTechnicalScores>
  >([]);
  const [financialEvidence, setFinancialEvidence] = useState('');
  const [financialReason, setFinancialReason] = useState('');
  const [financialScores, setFinancialScores] = useState<
    ReturnType<typeof buildFinancialScores>
  >([]);
  const [recommendedBidId, setRecommendedBidId] = useState('');
  const [authorityReference, setAuthorityReference] = useState('');
  const [ppaReference, setPpaReference] = useState('');
  const [approvalComments, setApprovalComments] = useState('');
  const [awardReference, setAwardReference] = useState('');
  const [awardEvidence, setAwardEvidence] = useState('');
  const [contractReference, setContractReference] = useState('');
  const [contractEvidence, setContractEvidence] = useState('');
  const [acceptanceReference, setAcceptanceReference] = useState('');
  const [acceptanceEvidence, setAcceptanceEvidence] = useState('');
  const [focusedBidId, setFocusedBidId] = useState<string>();

  useEffect(() => {
    setFocusedBidId(
      new URLSearchParams(window.location.search).get('bidId') || undefined
    );
  }, []);

  const load = useCallback(async () => {
    if (!tenderId) return;
    try {
      setLoading(true);
      setLoadError(undefined);
      const next = await service.get(tenderId);
      setControl(next);
      setTechnicalScores(buildTechnicalScores(next));
      setFinancialScores(buildFinancialScores(next));
      const nextFinancialScores = buildFinancialScores(next);
      setRecommendedBidId(
        next.recommendedBidId ?? nextFinancialScores[0]?.bidId ?? ''
      );
      setAuthorityReference(next.authorityApprovalReference ?? '');
      setPpaReference(next.ppaApprovalReference ?? '');
      if (next.status === Status.Approved) {
        try {
          setAwardGate(
            await procurementAwardReadinessService.latest('Tender', tenderId)
          );
        } catch {
          setAwardGate(null);
        }
      } else {
        setAwardGate(null);
      }
    } catch (error) {
      setControl(null);
      setLoadError(
        getProcurementProblemMessage(
          error,
          'Failed to load controlled tender lifecycle.'
        )
      );
    } finally {
      setLoading(false);
    }
  }, [tenderId]);

  useEffect(() => {
    void load();
  }, [load]);
  const readiness = useMemo(
    () => (control ? getTenderControlReadiness(control) : null),
    [control]
  );
  const actorName =
    [user?.firstName, user?.lastName].filter(Boolean).join(' ') ||
    user?.username ||
    'Opening officer';
  const awardGateAllows = Boolean(
    awardGate?.isReady &&
      awardGate.isCurrent &&
      hasAwardReadinessAction(awardGate.allowedActions, 'RecordAward') &&
      hasPermission('procurement.tender.approve') &&
      control?.recommendedBidId &&
      awardGate.recommendation.subjectIds.includes(control.recommendedBidId)
  );
  const focusedBid = control?.submissionReceipts.find(
    (item) => item.tenderBidId === focusedBidId
  );

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
        error instanceof Error ? error.message : 'Tender control action failed'
      );
    } finally {
      setBusy(null);
    }
  };

  if (loading)
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <Loader2 className="h-7 w-7 animate-spin" />
      </div>
    );
  if (!control || !readiness)
    return (
      <div className="space-y-4 p-6">
        <Button variant="ghost" asChild className="px-0">
          <Link href={`/procurement/bids?tenderId=${encodeURIComponent(tenderId)}`}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Tender bids
          </Link>
        </Button>
        <Card className="border-red-200">
          <CardHeader>
            <CardTitle>
              This historical tender cannot enter controlled evaluation
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p className="text-sm text-destructive">
              {loadError ?? 'The controlled tender lifecycle was not found.'}
            </p>
            {(loadError?.includes('TENDER_CONTROL_NOT_FOUND') ||
              loadError?.includes(
                'Advertise this NCT/ICT tender through the statutory control'
              )) && (
              <p className="text-sm text-muted-foreground">
                This tender was published without its mandatory controlled
                document register and statutory tender record. Existing bids
                cannot be scored until an administrator completes a reviewed
                historical-data migration. For UAT, create a replacement tender
                through the current controlled publication flow; bypassing this
                check would create an invalid audit trail.
              </p>
            )}
            <div className="flex flex-wrap gap-2">
              <Button asChild>
                <Link
                  href={`/procurement/bids?tenderId=${encodeURIComponent(tenderId)}`}
                >
                  Return to tender bids
                </Link>
              </Button>
              <Button asChild variant="outline">
                <Link href={`/procurement/tenders/${tenderId}`}>
                  Open tender details
                </Link>
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>
    );

  return (
    <div className="space-y-6 p-6" data-testid="controlled-tender-page">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Button variant="ghost" asChild className="mb-2 px-0">
            <Link href={`/procurement/tenders/${tenderId}`}>
              <ArrowLeft className="mr-2 h-4 w-4" />
              Tender
            </Link>
          </Button>
          <h1 className="text-2xl font-semibold">
            {methodLabel(control.method)} statutory controls
          </h1>
          <p className="text-sm text-muted-foreground">
            {control.tenderNumber} · {control.tenderTitle}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Badge
            variant={
              control.status === Status.Rejected ? 'destructive' : 'secondary'
            }
          >
            {getTenderControlStatusLabel(control)}
          </Badge>
          <Button asChild variant="outline" size="sm">
            <Link href={`/procurement/tenders/${tenderId}/committee-controls`}>
              <Users className="mr-2 h-4 w-4" />
              Committee controls
            </Link>
          </Button>
          <Button asChild variant="outline" size="sm">
            <Link href={`/procurement/tenders/${tenderId}/award-readiness`}>
              <ShieldCheck className="mr-2 h-4 w-4" />
              Award readiness
            </Link>
          </Button>
          <Button variant="outline" size="sm" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-4">
        <Summary
          title="Method and rule"
          value={`${methodLabel(control.method)} · ${control.methodRuleCode}`}
        />
        <Summary
          title="Authority route"
          value={control.authorityRouteReference}
        />
        <Summary
          title="On-time / late"
          value={`${readiness.onTime} / ${readiness.late}`}
        />
        <Summary
          title="Record integrity"
          value={control.integrityHash.slice(0, 16) + '…'}
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <CheckCircle2 className="h-5 w-5" />
            Statutory lifecycle
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-2">
          {control.milestones.map((item) => (
            <div
              key={item.code}
              className="rounded-lg border p-3"
              data-testid={`milestone-${item.code}`}
            >
              <div className="flex items-center justify-between">
                <span className="font-medium">{item.label}</span>
                <Badge variant={item.completedAtUtc ? 'default' : 'outline'}>
                  {item.completedAtUtc ? 'Complete' : 'Pending'}
                </Badge>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {formatDate(item.completedAtUtc)}
                {item.reference ? ` · ${item.reference}` : ''}
              </p>
            </div>
          ))}
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Advertisement and approved documents</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            <Line
              label="Advertisement"
              value={`${control.advertisementReference} · ${control.publicationChannel}`}
            />
            <Line
              label="Approved document"
              value={`${control.tenderDocumentReference} v${control.tenderDocumentVersion}`}
            />
            <Line
              label="Document fee"
              value={
                control.documentFee === 0
                  ? 'Free'
                  : control.documentFee.toFixed(2)
              }
            />
            <Line
              label="Submission deadline"
              value={formatDate(control.submissionDeadlineUtc)}
            />
            <Line
              label="Public opening"
              value={formatDate(control.openingScheduledAtUtc)}
            />
            <Line
              label="Evidence"
              value={control.advertisementEvidenceReference}
            />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <LockKeyhole className="h-5 w-5" />
              Sealed submission register
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {control.submissionReceipts.length === 0 && (
              <p className="text-sm text-muted-foreground">
                No statutory receipts yet.
              </p>
            )}
            {control.submissionReceipts.map((item) => (
              <div
                key={item.id}
                className="rounded border p-3"
                data-testid={`submission-${item.disposition === Disposition.OnTimeAccepted ? 'on-time' : 'late'}`}
              >
                <div className="flex justify-between gap-2">
                  <span className="font-medium">
                    {readiness.sealed
                      ? 'Sealed bidder'
                      : item.businessPartnerName}
                  </span>
                  <Badge
                    variant={
                      item.disposition === Disposition.OnTimeAccepted
                        ? 'secondary'
                        : 'destructive'
                    }
                  >
                    {item.disposition === Disposition.OnTimeAccepted
                      ? 'On time'
                      : 'Late / rejected'}
                  </Badge>
                </div>
                <p className="text-xs text-muted-foreground">
                  {item.receiptNumber} · {formatDate(item.receivedAtUtc)}
                </p>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      {control.status === Status.Advertised && (
        <div className="grid gap-4 lg:grid-cols-2">
          <Card>
            <CardHeader>
              <CardTitle>Controlled tender-document register</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-sm text-muted-foreground">
                Approved-version binding, paid or free issue records, addenda,
                extensions and recipient acknowledgements are maintained in the
                dedicated register. Direct issue entry is disabled here so the
                immutable version and receipt lineage cannot be bypassed.
              </p>
              <Button asChild>
                <Link
                  href={`/procurement/tenders/${tenderId}/document-controls`}
                >
                  <FileKey2 className="mr-2 h-4 w-4" />
                  Open document register
                </Link>
              </Button>
            </CardContent>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle>Complete signed public opening</CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3">
              <Field
                label="Opening evidence"
                value={opening.evidence}
                onChange={(value) =>
                  setOpening({ ...opening, evidence: value })
                }
              />
              <Field
                label="Officer signature reference"
                value={opening.officerSignature}
                onChange={(value) =>
                  setOpening({ ...opening, officerSignature: value })
                }
              />
              <Field
                label="Observer name"
                value={opening.observerName}
                onChange={(value) =>
                  setOpening({ ...opening, observerName: value })
                }
              />
              <Field
                label="Observer signature reference"
                value={opening.observerSignature}
                onChange={(value) =>
                  setOpening({ ...opening, observerSignature: value })
                }
              />
              <Button
                disabled={!readiness.canOpen || busy === 'opening' || !user?.id}
                onClick={() =>
                  void run(
                    'opening',
                    () =>
                      service.opening(tenderId, {
                        evidenceReference: opening.evidence,
                        participants: [
                          {
                            userId: user?.id,
                            name: actorName,
                            role: 'Opening officer',
                            isObserver: false,
                            signatureReference: opening.officerSignature,
                          },
                          {
                            name: opening.observerName,
                            role: opening.observerRole,
                            isObserver: true,
                            signatureReference: opening.observerSignature,
                          },
                        ],
                      }),
                    'Public opening register signed'
                  )
                }
              >
                {busy === 'opening' && (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                )}
                Complete public opening
              </Button>
              {!readiness.canOpen && (
                <p className="text-xs text-muted-foreground">
                  The deadline must pass and at least one on-time sealed
                  submission must exist; the server also enforces the configured
                  competition minimum.
                </p>
              )}
            </CardContent>
          </Card>
        </div>
      )}

      <section id="evaluation-workspace" className="scroll-mt-6 space-y-4">
        <Card className="border-blue-200 bg-blue-50/40">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ShieldCheck className="h-5 w-5 text-blue-700" />
              Tender bid evaluation
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-sm text-muted-foreground">
              {focusedBid
                ? `${focusedBid.businessPartnerName} is highlighted below. Complete the current phase for every eligible bid, then save the signed committee evaluation.`
                : 'Score every eligible bid in the current phase, then save the signed committee evaluation.'}
            </p>
            <Button asChild variant="outline" size="sm">
              <Link
                href={`/procurement/bids?tenderId=${encodeURIComponent(tenderId)}`}
              >
                <Users className="mr-2 h-4 w-4" />
                Tender bids
              </Link>
            </Button>
          </CardContent>
        </Card>

        {readiness.canTechnical && (
          <EvaluationCard
            title="Signed technical evaluation"
            evidence={technicalEvidence}
            setEvidence={setTechnicalEvidence}
          >
            {isQualitySelection(control.method) && (
              <p className="text-xs text-muted-foreground">
                Qualification is calculated from the locked{' '}
                {control.minimumTechnicalScore} technical threshold.
              </p>
            )}
            {technicalScores.map((score, index) => (
              <ScoreRow
                key={score.bidId}
                label={
                  control.submissionReceipts.find(
                    (item) => item.tenderBidId === score.bidId
                  )?.businessPartnerName ?? score.bidId
                }
                score={score.score}
                onScore={(value) =>
                  setTechnicalScores(
                    technicalScores.map((item, i) =>
                      i === index
                        ? {
                            ...item,
                            score: value,
                            qualified: isQualitySelection(control.method)
                              ? value >= control.minimumTechnicalScore
                              : item.qualified,
                          }
                        : item
                    )
                  )
                }
                qualified={score.qualified}
                focused={score.bidId === focusedBidId}
                qualifiedLocked={isQualitySelection(control.method)}
                onQualified={(value) =>
                  setTechnicalScores(
                    technicalScores.map((item, i) =>
                      i === index ? { ...item, qualified: value } : item
                    )
                  )
                }
              />
            ))}
            <Button
              disabled={busy === 'technical'}
              onClick={() =>
                void run(
                  'technical',
                  () =>
                    service.technicalEvaluation(tenderId, {
                      evidenceReference: technicalEvidence,
                      scores: technicalScores,
                      rowVersion: control.rowVersion,
                    }),
                  'Technical evaluation signed'
                )
              }
            >
              Save technical evaluation
            </Button>
          </EvaluationCard>
        )}

        {readiness.canFinancial && (
          <EvaluationCard
            title="Signed financial evaluation and recommendation"
            evidence={financialEvidence}
            setEvidence={setFinancialEvidence}
          >
            {control.method === Method.QualityBasedSelection && (
              <p className="text-xs text-muted-foreground">
                QBS opens the financial/negotiation review only for the uniquely
                highest-ranked qualified technical bid.
              </p>
            )}
            {control.method === Method.QualityAndCostBasedSelection && (
              <p className="text-xs text-muted-foreground">
                QCBS financial and combined scores are calculated by the server
                from evaluated amounts using {control.technicalWeight}/
                {control.financialWeight} weights.
              </p>
            )}
            {financialScores.map((score, index) => (
              <ScoreRow
                key={score.bidId}
                label={
                  control.submissionReceipts.find(
                    (item) => item.tenderBidId === score.bidId
                  )?.businessPartnerName ?? score.bidId
                }
                score={score.score}
                scoreLocked={isQualitySelection(control.method)}
                onScore={(value) =>
                  setFinancialScores(
                    financialScores.map((item, i) =>
                      i === index ? { ...item, score: value } : item
                    )
                  )
                }
                evaluatedAmount={score.evaluatedAmount}
                focused={score.bidId === focusedBidId}
                onEvaluatedAmount={(value) =>
                  setFinancialScores(
                    financialScores.map((item, i) =>
                      i === index ? { ...item, evaluatedAmount: value } : item
                    )
                  )
                }
              />
            ))}
            <Label>Recommended bid</Label>
            <select
              className="h-10 rounded-md border bg-background px-3"
              value={recommendedBidId}
              onChange={(event) => setRecommendedBidId(event.target.value)}
            >
              {financialScores.map((item) => (
                <option key={item.bidId} value={item.bidId}>
                  {control.submissionReceipts.find(
                    (receipt) => receipt.tenderBidId === item.bidId
                  )?.businessPartnerName ?? item.bidId}
                </option>
              ))}
            </select>
            <Label>Recommendation reason</Label>
            <Textarea
              value={financialReason}
              onChange={(event) => setFinancialReason(event.target.value)}
            />
            <Button
              disabled={busy === 'financial'}
              onClick={() =>
                void run(
                  'financial',
                  () =>
                    service.financialEvaluation(tenderId, {
                      evidenceReference: financialEvidence,
                      scores: financialScores,
                      recommendedBidId,
                      recommendationReason: financialReason,
                      rowVersion: control.rowVersion,
                    }),
                  'Financial evaluation signed'
                )
              }
            >
              Save financial recommendation
            </Button>
          </EvaluationCard>
        )}
        {!readiness.canTechnical && !readiness.canFinancial && (
          <Card>
            <CardContent className="pt-6 text-sm text-muted-foreground">
              Scoring is not open at the current lifecycle stage (
              {getTenderControlStatusLabel(control)}). Complete the pending
              opening, committee, or prior evaluation phase first.
            </CardContent>
          </Card>
        )}
      </section>

      {readiness.canSubmitApproval &&
        hasPermission('procurement.tender.approve') && (
          <ActionCard title={workflow.visibility.direct ? 'Complete recommendation' : 'Submit authority workflow'}>
            <p className="text-sm text-muted-foreground">
              {workflow.visibility.direct
                ? 'Complete the evaluation recommendation. Source, committee and statutory checks still apply.'
                : `Route ${control.authorityRouteReference}; shared workflow and SOD are server enforced.`}
            </p>
            {workflow.error && <p role="alert" className="text-sm text-destructive">{workflow.error}</p>}
            {workflow.visibility.direct && control.ppaApprovalRequired && (
              <Field label="PPA/central reference (required)" value={ppaReference} onChange={setPpaReference} />
            )}
            <Button
              disabled={busy !== null || !workflow.visibility.known ||
                (workflow.visibility.direct && control.ppaApprovalRequired && !ppaReference.trim())}
              onClick={() =>
                void run(
                  'submit',
                  () => service.submitApproval(tenderId, control.rowVersion, ppaReference || undefined),
                  workflow.visibility.direct ? 'Recommendation completed' : 'Recommendation submitted'
                )
              }
            >
              {workflow.visibility.direct ? 'Complete recommendation' : 'Submit for approval'}
            </Button>
          </ActionCard>
        )}

      {readiness.canDecide && workflow.visibility.showApprovalControls && hasPermission('procurement.tender.approve') && (
        <ActionCard title="Authority and PPA decision">
          <Field
            label="Authority approval reference"
            value={authorityReference}
            onChange={setAuthorityReference}
          />
          <Field
            label={`PPA/central reference${control.ppaApprovalRequired ? ' (required)' : ''}`}
            value={ppaReference}
            onChange={setPpaReference}
          />
          <Label>Comments</Label>
          <Textarea
            value={approvalComments}
            onChange={(event) => setApprovalComments(event.target.value)}
          />
          <div className="flex gap-2">
            <Button
              onClick={() =>
                void run(
                  'approve',
                  () =>
                    service.decideApproval(tenderId, {
                      action: 'approve',
                      authorityApprovalReference: authorityReference,
                      ppaApprovalReference: ppaReference || undefined,
                      comments: approvalComments,
                      rowVersion: control.rowVersion,
                    }),
                  'Approval step completed'
                )
              }
            >
              Approve
            </Button>
            <Button
              variant="destructive"
              onClick={() =>
                void run(
                  'reject',
                  () =>
                    service.decideApproval(tenderId, {
                      action: 'reject',
                      authorityApprovalReference: authorityReference,
                      ppaApprovalReference: ppaReference || undefined,
                      comments: approvalComments,
                      rowVersion: control.rowVersion,
                    }),
                  'Rejection recorded'
                )
              }
            >
              Reject
            </Button>
          </div>
        </ActionCard>
      )}

      {readiness.canAward && !awardGateAllows && (
        <ActionCard title="Award-readiness gate">
          <p className="text-sm text-muted-foreground">
            The source is locally at the award stage, but a current
            server-derived Ready decision with the exact recommended bid and
            allowed award action is still required.
          </p>
          <Button asChild variant="outline">
            <Link href={`/procurement/tenders/${tenderId}/award-readiness`}>
              Review blocked reasons and re-evaluate
            </Link>
          </Button>
        </ActionCard>
      )}
      {readiness.canAward && awardGateAllows && (
        <ActionCard title="Record approved award">
          <Field
            label="Award reference"
            value={awardReference}
            onChange={setAwardReference}
          />
          <Field
            label="Award evidence"
            value={awardEvidence}
            onChange={setAwardEvidence}
          />
          <Button
            onClick={() =>
              void run(
                'award',
                () =>
                  service.award(tenderId, {
                    bidId: control.recommendedBidId,
                    awardReference,
                    evidenceReference: awardEvidence,
                    rowVersion: control.rowVersion,
                  }),
                'Award recorded'
              )
            }
          >
            Record award
          </Button>
        </ActionCard>
      )}
      {readiness.canContract && (
        <ActionCard title="Record executed contract">
          <Field
            label="Contract reference"
            value={contractReference}
            onChange={setContractReference}
          />
          <Field
            label="Contract evidence"
            value={contractEvidence}
            onChange={setContractEvidence}
          />
          <Button
            onClick={() =>
              void run(
                'contract',
                () =>
                  service.contract(tenderId, {
                    contractReference,
                    evidenceReference: contractEvidence,
                    rowVersion: control.rowVersion,
                  }),
                'Contract recorded'
              )
            }
          >
            Record contract
          </Button>
        </ActionCard>
      )}
      {readiness.canAccept && (
        <ActionCard title="Record successful bidder acceptance">
          <Field
            label="Acceptance reference"
            value={acceptanceReference}
            onChange={setAcceptanceReference}
          />
          <Field
            label="Acceptance evidence"
            value={acceptanceEvidence}
            onChange={setAcceptanceEvidence}
          />
          <Button
            onClick={() =>
              void run(
                'acceptance',
                () =>
                  service.acceptance(tenderId, {
                    acceptanceReference,
                    evidenceReference: acceptanceEvidence,
                    rowVersion: control.rowVersion,
                  }),
                'Bidder acceptance recorded'
              )
            }
          >
            Complete statutory record
          </Button>
        </ActionCard>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5" />
            Document issue / sale register
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {control.documentIssues.length === 0 ? (
            <p className="text-sm text-muted-foreground">No issue records.</p>
          ) : (
            control.documentIssues.map((item) => (
              <div key={item.id} className="rounded border p-3">
                <div className="flex justify-between">
                  <span className="font-medium">{item.recipientName}</span>
                  <Badge variant="outline">
                    {item.amountPaid > 0 ? 'Paid' : 'Free'}
                  </Badge>
                </div>
                <p className="text-xs text-muted-foreground">
                  {item.issueReceiptNumber} · {formatDate(item.issuedAtUtc)} ·{' '}
                  {item.evidenceReference}
                </p>
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function Summary({ title, value }: { title: string; value: string }) {
  return (
    <Card>
      <CardContent className="pt-6">
        <p className="text-xs text-muted-foreground">{title}</p>
        <p className="mt-1 break-all font-semibold">{value}</p>
      </CardContent>
    </Card>
  );
}
function Line({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4 border-b py-2 last:border-0">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right font-medium">{value}</span>
    </div>
  );
}
function Field({
  label,
  value,
  onChange,
  type = 'text',
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
}) {
  const id = useId();
  return (
    <div className="space-y-1">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </div>
  );
}
function ActionCard({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FileKey2 className="h-5 w-5" />
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="grid gap-3">{children}</CardContent>
    </Card>
  );
}
function EvaluationCard({
  title,
  evidence,
  setEvidence,
  children,
}: {
  title: string;
  evidence: string;
  setEvidence: (value: string) => void;
  children: React.ReactNode;
}) {
  return (
    <ActionCard title={title}>
      <Field
        label="Signed evidence reference"
        value={evidence}
        onChange={setEvidence}
      />
      {children}
    </ActionCard>
  );
}
function ScoreRow({
  label,
  score,
  onScore,
  scoreLocked,
  qualified,
  qualifiedLocked,
  onQualified,
  evaluatedAmount,
  onEvaluatedAmount,
  focused,
}: {
  label: string;
  score: number;
  onScore: (value: number) => void;
  scoreLocked?: boolean;
  qualified?: boolean;
  qualifiedLocked?: boolean;
  onQualified?: (value: boolean) => void;
  evaluatedAmount?: number;
  onEvaluatedAmount?: (value: number) => void;
  focused?: boolean;
}) {
  return (
    <div
      className={`grid items-end gap-2 rounded border p-3 md:grid-cols-[1fr_140px_160px] ${
        focused ? 'border-blue-500 bg-blue-50 ring-1 ring-blue-300' : ''
      }`}
    >
      <span className="flex items-center gap-2 text-sm font-medium">
        {label}
        {focused && <Badge variant="secondary">Selected bid</Badge>}
      </span>
      {!scoreLocked && (
        <Field
          label="Score / 100"
          type="number"
          value={String(score)}
          onChange={(value) => onScore(Number(value))}
        />
      )}
      {onQualified && (
        <label className="flex h-10 items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={qualified}
            disabled={qualifiedLocked}
            onChange={(event) => onQualified(event.target.checked)}
          />
          Qualified
        </label>
      )}
      {onEvaluatedAmount && (
        <Field
          label="Evaluated amount"
          type="number"
          value={String(evaluatedAmount ?? 0)}
          onChange={(value) => onEvaluatedAmount(Number(value))}
        />
      )}
    </div>
  );
}
