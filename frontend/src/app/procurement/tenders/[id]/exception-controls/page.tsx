'use client';

import Link from 'next/link';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useParams } from 'next/navigation';
import {
  ArrowLeft,
  CheckCircle2,
  FileCheck2,
  Loader2,
  MailCheck,
  RefreshCw,
  Share2,
  ShieldAlert,
} from 'lucide-react';
import { toast } from 'sonner';
import NegotiationInviteDialog from '@/components/procurement/awards/NegotiationInviteDialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import {
  exceptionalMethodLabel,
  exceptionalSourcingStatusLabel,
  getExceptionalSourcingActions,
  validateExceptionalPreparation,
} from '@/lib/procurement-exceptional-sourcing-control';
import { getNegotiationByTenderAndBid } from '@/services/negotiationService';
import { procurementExceptionalSourcingControlService as service } from '@/services/procurement-exceptional-sourcing-control.service';
import { procurementAwardReadinessService } from '@/services/procurement-award-readiness.service';
import { hasAwardReadinessAction } from '@/lib/procurement-award-readiness';
import { useAuth } from '@/hooks/use-auth';
import {
  ProcurementExceptionalSourcingControlStatus as Status,
  type PrepareExceptionalSourcingRequest,
  type ProcurementExceptionalSourcingControl,
  type ProcurementExceptionalSourcingReadiness,
} from '@/types/procurement-exceptional-sourcing-control';
import type { ProcurementAwardReadinessDecision } from '@/types/procurement-award-readiness';

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleString() : '—';

export default function ExceptionalSourcingControlsPage() {
  const { id: tenderId } = useParams<{ id: string }>();
  const { hasPermission } = useAuth();
  const [control, setControl] =
    useState<ProcurementExceptionalSourcingControl | null>(null);
  const [readiness, setReadiness] =
    useState<ProcurementExceptionalSourcingReadiness | null>(null);
  const [awardGate, setAwardGate] =
    useState<ProcurementAwardReadinessDecision | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [actionError, setActionError] = useState('');
  const [busy, setBusy] = useState<string | null>(null);
  const [selectedSuppliers, setSelectedSuppliers] = useState<string[]>([]);
  const [justification, setJustification] = useState('');
  const [justificationEvidence, setJustificationEvidence] = useState('');
  const [supplierEvidence, setSupplierEvidence] = useState('');
  const [quoteReference, setQuoteReference] = useState('');
  const [quoteEvidence, setQuoteEvidence] = useState('');
  const [quotePrices, setQuotePrices] = useState<Record<string, string>>({});
  const [confirmPrepare, setConfirmPrepare] = useState(false);
  const [evidence, setEvidence] = useState<
    Record<string, { evidenceReference: string; verificationReference: string }>
  >({});
  const [boardReference, setBoardReference] = useState('');
  const [mdReference, setMdReference] = useState('');
  const [ppaReference, setPpaReference] = useState('');
  const [approvalComments, setApprovalComments] = useState('');
  const [selectedBidId, setSelectedBidId] = useState('');
  const [negotiationOpen, setNegotiationOpen] = useState(false);
  const [negotiationId, setNegotiationId] = useState('');
  const [negotiationPlan, setNegotiationPlan] = useState('');
  const [negotiationMinutes, setNegotiationMinutes] = useState('');
  const [negotiationOutcome, setNegotiationOutcome] = useState('');
  const [recommendationReason, setRecommendationReason] = useState('');
  const [recommendationEvidence, setRecommendationEvidence] = useState('');
  const [awardReference, setAwardReference] = useState('');
  const [awardEvidence, setAwardEvidence] = useState('');
  const [contractReference, setContractReference] = useState('');
  const [contractEvidence, setContractEvidence] = useState('');
  const [acceptanceReference, setAcceptanceReference] = useState('');
  const [acceptanceEvidence, setAcceptanceEvidence] = useState('');
  const [filingReference, setFilingReference] = useState('');
  const [filingEvidence, setFilingEvidence] = useState('');
  const [exceptionReport, setExceptionReport] = useState('');
  const [exceptionReportEvidence, setExceptionReportEvidence] = useState('');

  const load = useCallback(async () => {
    if (!tenderId) return;
    setLoading(true);
    setLoadError('');
    const [controlResult, readinessResult, awardGateResult] =
      await Promise.allSettled([
        service.get(tenderId),
        service.readiness(tenderId),
        procurementAwardReadinessService.latest(
          'ExceptionalSourcing',
          tenderId
        ),
      ]);
    const nextControl =
      controlResult.status === 'fulfilled' ? controlResult.value : null;
    const nextReadiness =
      readinessResult.status === 'fulfilled' ? readinessResult.value : null;
    setControl(nextControl);
    setReadiness(nextReadiness);
    setAwardGate(
      awardGateResult.status === 'fulfilled' ? awardGateResult.value : null
    );
    if (nextControl) {
      setBoardReference(nextControl.boardApprovalReference ?? '');
      setMdReference(nextControl.managingDirectorApprovalReference ?? '');
      setPpaReference(nextControl.ppaApprovalReference ?? '');
      setNegotiationId(nextControl.negotiationId ?? '');
      setSelectedBidId(
        nextControl.recommendedBidId ?? nextControl.bids[0]?.bidId ?? ''
      );
    }
    if (nextReadiness) {
      setEvidence((current) =>
        Object.fromEntries(
          nextReadiness.evidenceRequirements.map((item) => [
            item.requirementKey,
            current[item.requirementKey] ?? {
              evidenceReference: '',
              verificationReference: '',
            },
          ])
        )
      );
    }
    if (!nextControl && !nextReadiness) {
      const reason =
        controlResult.status === 'rejected'
          ? controlResult.reason
          : readinessResult.status === 'rejected'
            ? readinessResult.reason
            : null;
      setLoadError(
        reason instanceof Error
          ? reason.message
          : 'This tender is not ready for controlled noncompetitive sourcing.'
      );
    }
    setLoading(false);
  }, [tenderId]);

  useEffect(() => {
    void load();
  }, [load]);
  const actions = useMemo(
    () => (control ? getExceptionalSourcingActions(control) : null),
    [control]
  );
  const selectedBid = control?.bids.find(
    (item) => item.bidId === selectedBidId
  );
  const awardGateAllows = Boolean(
    awardGate?.isReady &&
    awardGate.isCurrent &&
    hasAwardReadinessAction(awardGate.allowedActions, 'RecordAward') &&
    hasPermission('procurement.tender.approve') &&
    control?.recommendedBidId &&
    awardGate.recommendation.subjectIds.includes(control.recommendedBidId)
  );

  const run = async (
    key: string,
    action: () => Promise<unknown>,
    success: string
  ) => {
    try {
      setBusy(key);
      setActionError('');
      await action();
      toast.success(success);
      await load();
      return true;
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Exceptional-sourcing action failed';
      setActionError(message);
      toast.error(message);
      return false;
    } finally {
      setBusy(null);
    }
  };

  const prepare = async () => {
    if (!readiness) return false;
    const request: PrepareExceptionalSourcingRequest = {
      quotation: readiness.method === 5 ? { reference: quoteReference, evidenceReference: quoteEvidence,
        items: (readiness.quotationItems ?? []).map(item => ({ tenderItemId: item.tenderItemId, unitPrice: Number(quotePrices[item.tenderItemId]) })) } : undefined,
      justification,
      justificationEvidenceReference: justificationEvidence,
      supplierSelectionEvidenceReference: supplierEvidence,
      businessPartnerIds: selectedSuppliers,
      evidenceChecklist: readiness.evidenceRequirements.map((item) => ({
        requirementKey: item.requirementKey,
        evidenceReference:
          evidence[item.requirementKey]?.evidenceReference ?? '',
        verificationReference:
          evidence[item.requirementKey]?.verificationReference ?? '',
      })),
    };
    const error = validateExceptionalPreparation(readiness, request);
    if (error) {
      setActionError(error);
      toast.error(error);
      return false;
    }
    return run(
      'prepare',
      () => service.prepare(tenderId, request),
      'Exceptional-sourcing record prepared'
    );
  };

  const onNegotiationComplete = async () => {
    if (!selectedBidId) return;
    const completed = await getNegotiationByTenderAndBid(
      tenderId,
      selectedBidId
    );
    if (completed) setNegotiationId(completed.id);
    toast.success(
      'Negotiation completed. Record its signed plan, minutes, and outcome below.'
    );
  };

  if (loading)
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <Loader2 className="h-7 w-7 animate-spin" />
      </div>
    );
  if (loadError)
    return (
      <div className="space-y-4 p-6">
        <Button variant="ghost" asChild>
          <Link href={`/procurement/tenders/${tenderId}`}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Tender
          </Link>
        </Button>
        <Card>
          <CardContent className="p-6 text-sm text-destructive">
            {loadError}
          </CardContent>
        </Card>
      </div>
    );

  return (
    <div
      className="space-y-6 p-6"
      data-testid="exceptional-sourcing-control-page"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <Button variant="ghost" asChild className="mb-2 px-0">
            <Link href={(control?.method ?? readiness?.method) === 5
              ? `/procurement/purchase-requisitions/${control?.sourceRequisitionId ?? readiness?.sourceRequisitionId}`
              : `/procurement/tenders/${tenderId}`}>
              <ArrowLeft className="mr-2 h-4 w-4" />
              {(control?.method ?? readiness?.method) === 5 ? 'Requisition' : 'Tender'}
            </Link>
          </Button>
          <h1 className="text-2xl font-semibold">
            {exceptionalMethodLabel(control?.method ?? readiness?.method ?? -1)}{' '}
            controls
          </h1>
          <p className="text-sm text-muted-foreground">
            {control?.tenderNumber ?? readiness?.tenderNumber} ·{' '}
            {control?.tenderTitle ?? readiness?.tenderTitle}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {control && (
            <Badge
              variant={
                control.status === Status.Rejected ? 'destructive' : 'secondary'
              }
            >
              {exceptionalSourcingStatusLabel[control.status]}
            </Badge>
          )}
          {!control && <Badge variant="outline">Preparation required</Badge>}
          {control && control.status >= Status.Recommended && <Button asChild variant="outline" size="sm">
            <Link
              href={`/procurement/tenders/${tenderId}/award-readiness?sourceType=ExceptionalSourcing`}
            >
              <ShieldAlert className="mr-2 h-4 w-4" />
              Award readiness
            </Link>
          </Button>}
          {(control?.method ?? readiness?.method) !== 5 && <Button asChild variant="outline" size="sm">
            <Link
              href={`/procurement/tenders/${tenderId}/bidder-communications?sourceType=ExceptionalSourcing`}
            >
              <MailCheck className="mr-2 h-4 w-4" />
              Bidder communications
            </Link>
          </Button>}
          {(control?.method ?? readiness?.method) !== 5 && <Button asChild variant="outline" size="sm">
            <Link
              href={`/procurement/tenders/${tenderId}/ghaneps-exchange?sourceType=ExceptionalSourcing`}
            >
              <Share2 className="mr-2 h-4 w-4" />
              GHANEPS exchange
            </Link>
          </Button>}
          <Button variant="outline" size="sm" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
        </div>
      </div>

      {actionError && !confirmPrepare && <p role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{actionError}</p>}
      {!control && readiness && (
        <Card data-testid="exceptional-preparation">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ShieldAlert className="h-5 w-5" />
              {readiness.method === 5
                ? 'Petty-purchase supplier and DEC-005 evidence'
                : 'Justification, suppliers, and DEC-006 evidence'}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="grid gap-3 md:grid-cols-3">
              <Summary label="Method rule" value={readiness.methodRuleCode} />
              <Summary
                label="Exception rule"
                value={readiness.exceptionRuleCode}
              />
              <Summary
                label="Authority route"
                value={readiness.authorityRouteReference || 'Configured independent workflow'}
              />
            </div>
            {readiness.method !== 5 && readiness.tenderStatus !== 'Approved' && (
              <p className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
                Complete the tender-document approval first. Current status:{' '}
                {readiness.tenderStatus}.
              </p>
            )}
            {readiness.justificationRequired && (
              <Field label="Controlled sourcing justification">
                <Textarea
                  value={justification}
                  onChange={(event) => setJustification(event.target.value)}
                  rows={4}
                />
              </Field>
            )}
            <div className="grid gap-3 md:grid-cols-2">
              {readiness.justificationRequired && (
                <Field label="Justification evidence">
                  <Input
                    value={justificationEvidence}
                    onChange={(event) =>
                      setJustificationEvidence(event.target.value)
                    }
                  />
                </Field>
              )}
              <Field label="Supplier-selection evidence">
                <Input
                  value={supplierEvidence}
                  onChange={(event) => setSupplierEvidence(event.target.value)}
                />
              </Field>
            </div>
            <div>
              <Label>
                Eligible supplier identity · select at least{' '}
                {readiness.minimumSupplierCount}
              </Label>
              <div className="mt-2 grid gap-2 md:grid-cols-2">
                {readiness.supplierOptions.map((supplier) => (
                  <label
                    key={supplier.businessPartnerId}
                    className="flex items-center gap-3 rounded border p-3 text-sm"
                  >
                    <input
                      type="checkbox"
                      checked={selectedSuppliers.includes(
                        supplier.businessPartnerId
                      )}
                      onChange={(event) =>
                        setSelectedSuppliers((current) =>
                          event.target.checked
                            ? [...current, supplier.businessPartnerId]
                            : current.filter(
                                (id) => id !== supplier.businessPartnerId
                              )
                        )
                      }
                    />
                    <span>
                      <strong>{supplier.supplierName}</strong>
                      <br />
                      <span className="text-xs text-muted-foreground">
                        {supplier.partnerCode} · {supplier.registrationStatus}
                      </span>
                    </span>
                  </label>
                ))}
                {readiness.supplierOptions.length === 0 && (
                  <p className="text-sm text-muted-foreground">
                    No active supplier candidates are available.
                  </p>
                )}
              </div>
            </div>
            <div className="space-y-3">
              <Label>Mandatory verified evidence checklist</Label>
              {readiness.evidenceRequirements.map((item) => (
                <div
                  key={item.requirementKey}
                  className="grid gap-3 rounded border p-3 md:grid-cols-2"
                  data-testid={`evidence-${item.requirementKey}`}
                >
                  <Field
                    label={`${item.evidenceName} · ${item.requirementKey}`}
                  >
                    <Input
                      value={
                        evidence[item.requirementKey]?.evidenceReference ?? ''
                      }
                      onChange={(event) =>
                        setEvidence((current) => ({
                          ...current,
                          [item.requirementKey]: {
                            ...current[item.requirementKey],
                            evidenceReference: event.target.value,
                          },
                        }))
                      }
                    />
                  </Field>
                  <Field label="Shared verification reference">
                    <Input
                      value={
                        evidence[item.requirementKey]?.verificationReference ??
                        ''
                      }
                      onChange={(event) =>
                        setEvidence((current) => ({
                          ...current,
                          [item.requirementKey]: {
                            ...current[item.requirementKey],
                            verificationReference: event.target.value,
                          },
                        }))
                      }
                    />
                  </Field>
                </div>
              ))}
            </div>
            {readiness.method === 5 && <section className="space-y-3 rounded-lg border p-4" aria-label="Supplier quotation">
              <h2 className="font-semibold">Supplier quotation</h2>
              <p className="text-sm text-muted-foreground">Quantities are locked. Total must not exceed {readiness.currency} {readiness.estimatedValue?.toLocaleString()}.</p>
              <Field label="Quotation reference"><Input aria-label="Quotation reference" value={quoteReference} onChange={event => setQuoteReference(event.target.value)}/></Field>
              <Field label="Quotation evidence"><Input aria-label="Quotation evidence" value={quoteEvidence} onChange={event => setQuoteEvidence(event.target.value)}/></Field>
              {(readiness.quotationItems ?? []).map(item => <Field key={item.tenderItemId} label={`${item.description} · ${item.quantity} ${item.unitOfMeasure ?? ''} · unit price (${readiness.currency})`}>
                <Input aria-label={`Unit price for ${item.description}`} type="number" min="0.01" step="0.01" value={quotePrices[item.tenderItemId] ?? ''}
                  onChange={event => setQuotePrices(current => ({ ...current, [item.tenderItemId]: event.target.value }))}/>
              </Field>)}
            </section>}
            <Button
              onClick={() => setConfirmPrepare(true)}
              disabled={busy !== null || (readiness.tenderStatus !== 'Approved' && !(readiness.method === 5 && readiness.tenderStatus === 'Draft'))}
            >
              {busy === 'prepare' && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Prepare immutable record
            </Button>
            <ConfirmationDialog open={confirmPrepare} onOpenChange={setConfirmPrepare} title="Lock supplier, quotation and evidence?"
              description="These details will be retained for independent approval. No award, order or payment is created."
              confirmText="Prepare record" onConfirm={prepare} isLoading={busy === 'prepare'}>
              {actionError && <p role="alert" className="text-sm text-destructive">{actionError}</p>}
            </ConfirmationDialog>
          </CardContent>
        </Card>
      )}

      {control && actions && (
        <>
          {control.method === 5 && control.status === Status.Awarded && control.sourceRequisitionId &&
            <Card><CardHeader><CardTitle>Next: Purchase order</CardTitle></CardHeader><CardContent>
              <p className="mb-3 text-sm text-muted-foreground">The quotation award is complete. Create the PO from this approved source, then follow PO approval and receipt. No payment has been made.</p>
              <Button asChild><Link href={`/procurement/purchase-requisitions/${control.sourceRequisitionId}`}>Continue to purchase order</Link></Button>
            </CardContent></Card>}
          <div className="grid gap-3 md:grid-cols-4">
            <Summary
              label="Method / exception"
              value={`${exceptionalMethodLabel(control.method)} · ${control.exceptionRuleCode}`}
            />
            <Summary
              label={control.method === 5 ? 'Approval route' : 'Authority route'}
              value={control.authorityRouteReference || (control.method === 5 ? 'Configured independent workflow' : '—')}
            />
            <Summary
              label="Suppliers / evidence"
              value={`${control.suppliers.length} / ${control.evidenceChecklist.length}`}
            />
            <Summary
              label={control.method === 5 ? 'Quotation amount' : 'Integrity'}
              value={control.method === 5 && control.bids[0]
                ? `${control.bids[0].currency} ${control.bids[0].bidAmount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
                : `${control.integrityHash.slice(0, 16)}…`}
            />
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CheckCircle2 className="h-5 w-5" />
                {control.method === 5 ? 'Purchase progress' : 'Immutable statutory history'}
              </CardTitle>
            </CardHeader>
            <CardContent className="grid gap-3 md:grid-cols-2">
              {control.milestones.map((item) => (
                <div
                  key={item.code}
                  className="rounded border p-3"
                  data-testid={`milestone-${item.code}`}
                >
                  <div className="flex justify-between gap-2">
                    <span className="font-medium">{item.label}</span>
                    <Badge
                      variant={item.completedAtUtc ? 'default' : 'outline'}
                    >
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
                <CardTitle>Justification and supplier identity</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                <Line label="Justification" value={control.justification} />
                <Line
                  label="Justification evidence"
                  value={control.justificationEvidenceReference}
                />
                <Line
                  label="Selection evidence"
                  value={control.supplierSelectionEvidenceReference}
                />
                {control.suppliers.map((supplier) => (
                  <div
                    key={supplier.businessPartnerId}
                    className="rounded border p-2"
                  >
                    <strong>{supplier.supplierName}</strong>
                    {control.method !== 5 && <p className="text-xs text-muted-foreground">
                      {supplier.businessPartnerId}
                    </p>}
                  </div>
                ))}
              </CardContent>
            </Card>
            <Card>
              <CardHeader>
                <CardTitle>{control.method === 5 ? 'Evidence references' : 'Verified evidence checklist'}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {control.evidenceChecklist.map((item) => (
                  <div
                    key={item.evidenceRuleId}
                    className="rounded border p-2 text-sm"
                  >
                    <strong>{item.evidenceName}</strong>
                    <p className="text-xs text-muted-foreground">
                      {item.requirementKey} · {item.evidenceReference} ·
                      verified {item.verificationReference}
                    </p>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>

          {actions.canSubmitApproval && (
            <ActionCard title="Submit exact sourcing-approval workflow">
              <p className="text-sm text-muted-foreground">
                This locks the preparation and starts the authority route
                selected by the current policy.
              </p>
              <Button
                onClick={() =>
                  void run(
                    'submit',
                    () => service.submitApproval(tenderId, control.rowVersion),
                    'Approval workflow submitted'
                  )
                }
                disabled={busy !== null}
              >
                Submit approval
              </Button>
            </ActionCard>
          )}

          {actions.canDecideApproval && !hasPermission('procurement.tender.approve') && (
            <p className="rounded-md border p-3 text-sm text-muted-foreground">Awaiting the configured independent approver. Your account cannot approve this purchase.</p>
          )}
          {actions.canDecideApproval && hasPermission('procurement.tender.approve') && (
            <ActionCard title="Record authority decision">
              <div className="grid gap-3 md:grid-cols-3">
                {control.boardApprovalRequired && (
                  <Field label="Board approval reference">
                    <Input
                      value={boardReference}
                      onChange={(event) =>
                        setBoardReference(event.target.value)
                      }
                    />
                  </Field>
                )}
                {control.managingDirectorApprovalRequired && (
                  <Field label="Managing Director reference">
                    <Input
                      value={mdReference}
                      onChange={(event) => setMdReference(event.target.value)}
                    />
                  </Field>
                )}
                {control.ppaApprovalRequired && (
                  <Field label="PPA approval reference">
                    <Input
                      value={ppaReference}
                      onChange={(event) => setPpaReference(event.target.value)}
                    />
                  </Field>
                )}
              </div>
              <Field label="Comments">
                <Textarea
                  value={approvalComments}
                  onChange={(event) => setApprovalComments(event.target.value)}
                />
              </Field>
              <div className="flex gap-2">
                <Button
                  onClick={() =>
                    void run(
                      'approve',
                      () =>
                        service.decideApproval(tenderId, {
                          action: 'Approve',
                          boardApprovalReference: boardReference || undefined,
                          managingDirectorApprovalReference:
                            mdReference || undefined,
                          ppaApprovalReference: ppaReference || undefined,
                          comments: approvalComments,
                          rowVersion: control.rowVersion,
                        }),
                      'Authority decision recorded'
                    )
                  }
                  disabled={busy !== null}
                >
                  Approve step
                </Button>
                <Button
                  variant="destructive"
                  onClick={() =>
                    void run(
                      'reject',
                      () =>
                        service.decideApproval(tenderId, {
                          action: 'Reject',
                          comments: approvalComments,
                          rowVersion: control.rowVersion,
                        }),
                      'Rejection recorded'
                    )
                  }
                  disabled={busy !== null}
                >
                  Reject
                </Button>
              </div>
            </ActionCard>
          )}

          {actions.canNegotiate && (
            <ActionCard title="Complete and record negotiation">
              <BidSelect
                control={control}
                value={selectedBidId}
                onChange={setSelectedBidId}
              />
              <Button
                variant="outline"
                onClick={() => setNegotiationOpen(true)}
                disabled={!selectedBidId}
              >
                Open shared negotiation
              </Button>
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Completed negotiation ID">
                  <Input
                    value={negotiationId}
                    onChange={(event) => setNegotiationId(event.target.value)}
                  />
                </Field>
                <Field label="Approved plan reference">
                  <Input
                    value={negotiationPlan}
                    onChange={(event) => setNegotiationPlan(event.target.value)}
                  />
                </Field>
                <Field label="Signed minutes evidence">
                  <Input
                    value={negotiationMinutes}
                    onChange={(event) =>
                      setNegotiationMinutes(event.target.value)
                    }
                  />
                </Field>
                <Field label="Outcome reference">
                  <Input
                    value={negotiationOutcome}
                    onChange={(event) =>
                      setNegotiationOutcome(event.target.value)
                    }
                  />
                </Field>
              </div>
              <Button
                onClick={() =>
                  void run(
                    'negotiation',
                    () =>
                      service.negotiation(tenderId, {
                        negotiationId,
                        planReference: negotiationPlan,
                        minutesEvidenceReference: negotiationMinutes,
                        outcomeReference: negotiationOutcome,
                        rowVersion: control.rowVersion,
                      }),
                    'Negotiation evidence recorded'
                  )
                }
                disabled={busy !== null}
              >
                Record negotiation
              </Button>
            </ActionCard>
          )}

          {actions.canRecommend && !hasPermission('procurement.tender.evaluate') && (
            <p className="text-sm text-muted-foreground">
              Awaiting a recommendation from an authorized evaluator.
            </p>
          )}
          {actions.canRecommend && hasPermission('procurement.tender.evaluate') && (
            <ActionCard title={control.method === 5 ? 'Recommend approved quotation' : 'Record negotiated recommendation'}>
              <BidSelect
                control={control}
                value={selectedBidId}
                onChange={setSelectedBidId}
              />
              <Field label="Recommendation reason">
                <Textarea
                  value={recommendationReason}
                  onChange={(event) =>
                    setRecommendationReason(event.target.value)
                  }
                />
              </Field>
              <Field label="Signed recommendation evidence">
                <Input
                  value={recommendationEvidence}
                  onChange={(event) =>
                    setRecommendationEvidence(event.target.value)
                  }
                />
              </Field>
              <Button
                onClick={() =>
                  void run(
                    'recommend',
                    () =>
                      service.recommendation(tenderId, {
                        bidId: selectedBidId,
                        reason: recommendationReason,
                        evidenceReference: recommendationEvidence,
                        rowVersion: control.rowVersion,
                      }),
                    'Recommendation recorded'
                  )
                }
                disabled={busy !== null || !selectedBidId}
              >
                Record recommendation
              </Button>
            </ActionCard>
          )}

          {actions.canAward && !awardGateAllows && (
            <ActionCard title="Award-readiness gate">
              <p className="text-sm text-muted-foreground">
                A current server-derived Ready decision for the exact{' '}
                {control.method === 5 ? 'approved quotation' : 'negotiated recommendation'}{' '}
                is required before the controlled award action is available.
              </p>
              <Button asChild variant="outline">
                <Link
                  href={`/procurement/tenders/${tenderId}/award-readiness?sourceType=ExceptionalSourcing`}
                >
                  Review blocked reasons and re-evaluate
                </Link>
              </Button>
            </ActionCard>
          )}
          {actions.canAward && awardGateAllows && hasPermission('procurement.tender.approve') && (
            <ActionCard title="Record controlled award">
              <Field label="Award reference">
                <Input
                  value={awardReference}
                  onChange={(event) => setAwardReference(event.target.value)}
                />
              </Field>
              <Field label="Award evidence">
                <Input
                  value={awardEvidence}
                  onChange={(event) => setAwardEvidence(event.target.value)}
                />
              </Field>
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
                disabled={busy !== null}
              >
                Record award
              </Button>
            </ActionCard>
          )}

          {actions.canContract && (
            <ActionCard title="Link executed contract">
              <Field label="Contract reference">
                <Input
                  value={contractReference}
                  onChange={(event) => setContractReference(event.target.value)}
                />
              </Field>
              <Field label="Executed contract evidence">
                <Input
                  value={contractEvidence}
                  onChange={(event) => setContractEvidence(event.target.value)}
                />
              </Field>
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
                    'Contract linked'
                  )
                }
                disabled={busy !== null}
              >
                Record contract
              </Button>
            </ActionCard>
          )}

          {actions.canAccept && (
            <ActionCard title="Record successful supplier acceptance">
              <Field label="Acceptance reference">
                <Input
                  value={acceptanceReference}
                  onChange={(event) =>
                    setAcceptanceReference(event.target.value)
                  }
                />
              </Field>
              <Field label="Acceptance evidence">
                <Input
                  value={acceptanceEvidence}
                  onChange={(event) =>
                    setAcceptanceEvidence(event.target.value)
                  }
                />
              </Field>
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
                    'Supplier acceptance recorded'
                  )
                }
                disabled={busy !== null}
              >
                Record acceptance
              </Button>
            </ActionCard>
          )}

          {actions.canFile && (
            <ActionCard title="Complete mandatory post-award filing">
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="PPA filing reference">
                  <Input
                    value={filingReference}
                    onChange={(event) => setFilingReference(event.target.value)}
                  />
                </Field>
                <Field label="PPA filing evidence">
                  <Input
                    value={filingEvidence}
                    onChange={(event) => setFilingEvidence(event.target.value)}
                  />
                </Field>
                <Field label="Exception report reference">
                  <Input
                    value={exceptionReport}
                    onChange={(event) => setExceptionReport(event.target.value)}
                  />
                </Field>
                <Field label="Exception report evidence">
                  <Input
                    value={exceptionReportEvidence}
                    onChange={(event) =>
                      setExceptionReportEvidence(event.target.value)
                    }
                  />
                </Field>
              </div>
              <Button
                onClick={() =>
                  void run(
                    'filing',
                    () =>
                      service.filing(tenderId, {
                        filingReference,
                        filingEvidenceReference: filingEvidence,
                        exceptionReportReference: exceptionReport,
                        exceptionReportEvidenceReference:
                          exceptionReportEvidence,
                        rowVersion: control.rowVersion,
                      }),
                    'Post-award filing completed'
                  )
                }
                disabled={busy !== null}
              >
                Complete filing
              </Button>
            </ActionCard>
          )}

          {actions.immutable && (
            <Card>
              <CardContent className="flex items-center gap-3 p-5 text-sm">
                <FileCheck2 className="h-5 w-5 text-emerald-600" />
                This final statutory record is read-only. Its evidence, actors,
                references, and integrity hash remain available for audit.
              </CardContent>
            </Card>
          )}
        </>
      )}

      {control && selectedBid && (
        <NegotiationInviteDialog
          open={negotiationOpen}
          onOpenChange={setNegotiationOpen}
          tenderId={tenderId}
          tenderBidId={selectedBid.bidId}
          businessPartnerName={selectedBid.supplierName}
          onNegotiationComplete={() => void onNegotiationComplete()}
        />
      )}
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
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">{children}</CardContent>
    </Card>
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
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
function Summary({ label, value }: { label: string; value: string }) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs uppercase text-muted-foreground">{label}</p>
        <p className="mt-1 break-words font-medium">{value || '—'}</p>
      </CardContent>
    </Card>
  );
}
function Line({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span className="text-muted-foreground">{label}: </span>
      <span>{value || '—'}</span>
    </div>
  );
}
function BidSelect({
  control,
  value,
  onChange,
}: {
  control: ProcurementExceptionalSourcingControl;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <Field label="Supplier bid">
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger>
          <SelectValue placeholder="Select bid" />
        </SelectTrigger>
        <SelectContent>
          {control.bids.map((bid) => (
            <SelectItem key={bid.bidId} value={bid.bidId}>
              {bid.bidNumber} · {bid.supplierName} · {bid.currency}{' '}
              {bid.bidAmount.toLocaleString()}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </Field>
  );
}
