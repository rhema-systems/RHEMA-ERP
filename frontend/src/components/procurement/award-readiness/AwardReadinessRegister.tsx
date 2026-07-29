'use client';

import React from 'react';
import Link from 'next/link';
import {
  AlertTriangle,
  CheckCircle2,
  ClipboardCheck,
  FileCheck2,
  FileLock2,
  History,
  Loader2,
  Route,
  Scale,
  ShieldCheck,
  UserCheck,
  Users,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  awardReadinessGroupLabel,
  awardReadinessRemediationHref,
  awardReadinessSodPresentation,
  awardReadinessStatusLabel,
  hasAwardReadinessAction,
  isAwardReadinessSodPolicyEffective,
  isCurrentActorAwardEvaluator,
} from '@/lib/procurement-award-readiness';
import type {
  ProcurementAwardReadinessDecision,
  ProcurementAwardReadinessPrerequisiteStatus,
  ProcurementAwardReadinessSourceType,
  ProcurementEvaluatorAwardApproverSodStatus,
} from '@/types/procurement-award-readiness';

const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';

const shortId = (value?: string) =>
  value ? (value.length > 16 ? `${value.slice(0, 8)}…${value.slice(-6)}` : value) : '—';

const roleLabel = (value: string) =>
  value
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .trim()
    .toLowerCase()
    .replace(/\b\w/g, (letter) => letter.toUpperCase());

const prerequisiteVariant = (
  status: ProcurementAwardReadinessPrerequisiteStatus
): 'default' | 'destructive' | 'outline' =>
  status === 'Passed'
    ? 'default'
    : status === 'Failed'
      ? 'destructive'
      : 'outline';

const statusVariant = (
  value: string
): 'default' | 'destructive' | 'outline' | 'secondary' => {
  if (
    [
      'Ready',
      'Passed',
      'Active',
      'Completed',
      'Approved',
      'Locked',
      'Eligible',
    ].includes(value)
  )
    return 'default';
  if (
    ['Blocked', 'Failed', 'Expired', 'Revoked', 'Recalled', 'Ineligible'].includes(
      value
    )
  )
    return 'destructive';
  return value === 'NotApplicable' ? 'outline' : 'secondary';
};

export interface AwardReadinessRegisterProps {
  sourceType: ProcurementAwardReadinessSourceType;
  sourceId: string;
  decision?: ProcurementAwardReadinessDecision;
  history: ProcurementAwardReadinessDecision[];
  sodStatus?: ProcurementEvaluatorAwardApproverSodStatus;
  isSodStatusLoading: boolean;
  sodStatusError?: string;
  canEvaluate: boolean;
  isEvaluating: boolean;
  onEvaluate: () => void;
}

export function AwardReadinessRegister({
  sourceType,
  sourceId,
  decision,
  history,
  sodStatus,
  isSodStatusLoading,
  sodStatusError,
  canEvaluate,
  isEvaluating,
  onEvaluate,
}: AwardReadinessRegisterProps) {
  const serverAllowsEvaluation =
    !decision ||
    hasAwardReadinessAction(decision.allowedActions, 'EvaluateReadiness');
  const showEvaluate = canEvaluate && serverAllowsEvaluation;
  const sodPreflightUnavailable =
    isSodStatusLoading || Boolean(sodStatusError) || !sodStatus;
  const evaluationDisabled =
    isEvaluating || sodPreflightUnavailable || sodStatus?.allowed !== true;

  if (!decision) {
    return (
      <div className="space-y-6">
        <EvaluatorAwardApproverSodStatus
          status={sodStatus}
          isLoading={isSodStatusLoading}
          error={sodStatusError}
        />
        <Card className="border-dashed" data-testid="award-readiness-empty">
          <CardContent className="flex min-h-72 flex-col items-center justify-center p-8 text-center">
            <ShieldCheck className="mb-4 h-10 w-10 text-muted-foreground" />
            <h2 className="text-lg font-semibold">
              No award-readiness decision has been retained
            </h2>
            <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
              Run the authoritative server evaluation to retain the first
              immutable decision. The client cannot mark this source ready or
              supply an alternate recommendation.
            </p>
            {showEvaluate && (
              <Button
                className="mt-5"
                onClick={onEvaluate}
                disabled={evaluationDisabled}
              >
                <ShieldCheck className="mr-2 h-4 w-4" />
                {isEvaluating ? 'Evaluating…' : 'Evaluate award readiness'}
              </Button>
            )}
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6" data-testid="award-readiness-register">
      <EvaluatorAwardApproverSodStatus
        status={sodStatus}
        isLoading={isSodStatusLoading}
        error={sodStatusError}
      />
      <ReadinessOverview decision={decision} />

      {decision.blockedReasons.length > 0 && (
        <Alert className="border-amber-500/40 bg-amber-500/5">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Award remains blocked</AlertTitle>
          <AlertDescription>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              {decision.blockedReasons.map((reason, index) => (
                <li key={`${reason}-${index}`}>{reason}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      <div className="flex justify-end">
        {decision.allowedActions.map((action) => (
          <Badge key={action} variant="outline" className="mr-2">
            Server action: {roleLabel(action)}
          </Badge>
        ))}
        {showEvaluate ? (
          <Button onClick={onEvaluate} disabled={evaluationDisabled}>
            <ShieldCheck className="mr-2 h-4 w-4" />
            {isEvaluating ? 'Evaluating…' : 'Re-evaluate current readiness'}
          </Button>
        ) : (
          <p className="text-sm text-muted-foreground">
            A fresh decision is unavailable to this actor or is not allowed by
            the server.
          </p>
        )}
      </div>

      <PrerequisiteRegister
        decision={decision}
        sourceType={sourceType}
        sourceId={sourceId}
      />
      <RecommendationAndActors decision={decision} />
      <EvaluationLineage decision={decision} />
      <SupplierLineage
        decision={decision}
        sourceType={sourceType}
        sourceId={sourceId}
      />
      <AuthorityAndEvidence
        decision={decision}
        sourceType={sourceType}
        sourceId={sourceId}
      />
      <DecisionHistory decision={decision} history={history} />
    </div>
  );
}

function EvaluatorAwardApproverSodStatus({
  status,
  isLoading,
  error,
}: {
  status?: ProcurementEvaluatorAwardApproverSodStatus;
  isLoading: boolean;
  error?: string;
}) {
  if (isLoading) {
    return (
      <Card data-testid="award-readiness-sod-status">
        <CardContent className="flex min-h-40 items-center justify-center p-6 text-sm text-muted-foreground">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          Checking current-actor evaluator and award-approver separation…
        </CardContent>
      </Card>
    );
  }

  if (!status || error) {
    return (
      <Card data-testid="award-readiness-sod-status">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Scale className="h-5 w-5" />
            Evaluator versus award approver SOD
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Alert className="border-destructive/50 bg-destructive/5">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>SOD preflight is unavailable</AlertTitle>
            <AlertDescription>
              {error ||
                'The server did not return an authoritative current-actor SOD status.'}{' '}
              Evaluation remains disabled until the status can be verified.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  const presentation = awardReadinessSodPresentation(status);
  const policyEffective = isAwardReadinessSodPolicyEffective(status);
  const actorIsEvaluator = isCurrentActorAwardEvaluator(status);
  const uniqueEvaluatorCount = new Set(status.evaluatorUserIds).size;
  const policyLabel =
    presentation === 'NotApplicable'
      ? 'Not applicable'
      : policyEffective
        ? 'Effective hard stop'
        : 'Incomplete · fail closed';
  const outcomeTitle =
    presentation === 'Allowed'
      ? 'Current actor is allowed to evaluate readiness'
      : presentation === 'NotApplicable'
        ? 'Evaluator/approver SOD is not applicable'
        : 'Current actor is blocked from evaluating readiness';

  return (
    <Card data-testid="award-readiness-sod-status">
      <CardHeader>
        <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Scale className="h-5 w-5" />
              Evaluator versus award approver SOD
            </CardTitle>
            <p className="mt-1 text-sm text-muted-foreground">
              Live, tenant-safe status for the authenticated actor; retained
              award decisions below remain immutable history.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Badge
              variant={
                presentation === 'Allowed'
                  ? 'default'
                  : presentation === 'Blocked'
                    ? 'destructive'
                    : 'outline'
              }
            >
              {presentation === 'NotApplicable'
                ? 'Not applicable'
                : presentation}
            </Badge>
            <Badge variant={policyEffective ? 'default' : 'destructive'}>
              {policyLabel}
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-5">
        <Alert
          className={
            presentation === 'Blocked'
              ? 'border-destructive/50 bg-destructive/5'
              : presentation === 'Allowed'
                ? 'border-emerald-500/40 bg-emerald-500/5'
                : undefined
          }
        >
          {presentation === 'Allowed' ? (
            <CheckCircle2 className="h-4 w-4" />
          ) : (
            <AlertTriangle className="h-4 w-4" />
          )}
          <AlertTitle>{outcomeTitle}</AlertTitle>
          <AlertDescription>
            {status.message}{' '}
            <span className="font-mono text-xs">{status.code}</span>
          </AlertDescription>
        </Alert>

        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <Lineage
            label="Current actor"
            value={`${status.currentActorName} · ${
              actorIsEvaluator ? 'Evaluator' : 'Not an evaluator'
            }`}
          />
          <Lineage
            label="Evaluator lineage"
            value={`${uniqueEvaluatorCount} evaluator${
              uniqueEvaluatorCount === 1 ? '' : 's'
            } · ${status.evaluatorLineage.length} record${
              status.evaluatorLineage.length === 1 ? '' : 's'
            }`}
          />
          <Lineage
            label="Independent approval actors"
            value={String(status.independentApprovalActorUserIds.length)}
          />
          <Lineage
            label="Readiness decision"
            value={
              status.readinessDecisionSequence === undefined
                ? 'Not yet retained'
                : `#${status.readinessDecisionSequence} · ${
                    status.readinessDecisionIsCurrent
                      ? 'Current'
                      : 'Historical'
                  }`
            }
          />
        </div>

        <div className="grid gap-4 lg:grid-cols-2">
          <div className="rounded-md border p-4">
            <p className="text-xs uppercase tracking-wide text-muted-foreground">
              Current actor and evaluator identities
            </p>
            <p className="mt-2 font-medium">{status.currentActorName}</p>
            <p className="font-mono text-xs text-muted-foreground">
              {status.currentActorUserId}
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              {status.currentActorRoles.length > 0 ? (
                status.currentActorRoles.map((role) => (
                  <Badge key={role} variant="outline">
                    {roleLabel(role)}
                  </Badge>
                ))
              ) : (
                <span className="text-sm text-muted-foreground">
                  No current actor roles returned.
                </span>
              )}
            </div>
            <p className="mt-4 text-sm font-medium">
              Retained evaluator user IDs
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              {status.evaluatorUserIds.map((userId) => (
                <Badge key={userId} variant="secondary" className="font-mono">
                  {shortId(userId)}
                </Badge>
              ))}
            </div>
          </div>

          <div className="rounded-md border p-4">
            <p className="text-xs uppercase tracking-wide text-muted-foreground">
              Effective hard-stop and policy lineage
            </p>
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <Lineage
                label="Control"
                value={status.sodControlCode || '—'}
              />
              <Lineage
                label="Policy"
                value={
                  status.sodPolicyCode
                    ? `${status.sodPolicyCode} v${
                        status.sodPolicyVersion ?? '—'
                      }`
                    : policyLabel
                }
              />
              <Lineage label="Rule" value={status.sodRuleCode || '—'} />
              <Lineage
                label="Source method"
                value={status.sourceMethodRuleCode || '—'}
              />
            </div>
            <p className="mt-3 break-all font-mono text-[11px] text-muted-foreground">
              SOD decision {status.sodDecisionId || '—'} · policy{' '}
              {status.sodPolicySetId || '—'} · rule {status.sodRuleId || '—'} ·
              source key {status.sodSourceDecisionKey || '—'}
            </p>
          </div>
        </div>

        <div>
          <div className="mb-3 flex flex-col justify-between gap-2 sm:flex-row sm:items-end">
            <div>
              <h3 className="font-medium">Authoritative evaluator lineage</h3>
              <p className="text-sm text-muted-foreground">
                Committee appointments and retained or recalled score attempts
                resolved by the server for this source.
              </p>
            </div>
            <Badge variant="outline">
              {status.evaluatorLineage.length} lineage record
              {status.evaluatorLineage.length === 1 ? '' : 's'}
            </Badge>
          </div>
          {status.evaluatorLineage.length === 0 ? (
            <p className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
              No evaluator lineage was returned. The server must resolve
              unambiguous evaluator history before readiness can be evaluated.
            </p>
          ) : (
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full min-w-[1060px] text-sm">
                <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2">Family / evaluator</th>
                    <th className="px-3 py-2">Phase / attempt</th>
                    <th className="px-3 py-2">Evaluation lineage</th>
                    <th className="px-3 py-2">Score subject</th>
                    <th className="px-3 py-2">State</th>
                    <th className="px-3 py-2">Evaluated / integrity</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {status.evaluatorLineage.map((lineage, index) => (
                    <tr
                      key={`${lineage.family}-${lineage.evaluatorUserId}-${
                        lineage.scoreSheetId ?? lineage.evaluationId ?? index
                      }`}
                    >
                      <td className="px-3 py-3">
                        <p className="font-medium">
                          {roleLabel(lineage.family)}
                        </p>
                        <p className="font-mono text-xs text-muted-foreground">
                          {shortId(lineage.evaluatorUserId)}
                        </p>
                      </td>
                      <td className="px-3 py-3">
                        {lineage.phase || '—'}
                        {lineage.attempt === undefined
                          ? ''
                          : ` · attempt ${lineage.attempt}`}
                      </td>
                      <td className="px-3 py-3 font-mono text-xs">
                        <p>evaluation {shortId(lineage.evaluationId)}</p>
                        <p>committee {shortId(lineage.committeeControlId)}</p>
                        <p>appointment {shortId(lineage.appointmentId)}</p>
                        <p>score {shortId(lineage.scoreSheetId)}</p>
                      </td>
                      <td className="px-3 py-3">
                        <p>{lineage.scoreSubjectType || '—'}</p>
                        <p className="font-mono text-xs text-muted-foreground">
                          {shortId(lineage.scoreSubjectId)}
                        </p>
                      </td>
                      <td className="px-3 py-3">
                        <Badge
                          variant={
                            lineage.isRecalledAttempt
                              ? 'destructive'
                              : lineage.isRetainedAttempt
                                ? 'default'
                                : 'outline'
                          }
                        >
                          {lineage.isRecalledAttempt
                            ? 'Recalled'
                            : lineage.isRetainedAttempt
                              ? 'Retained'
                              : 'Evaluation'}
                        </Badge>
                        {lineage.scoreStatus && (
                          <p className="mt-1 text-xs text-muted-foreground">
                            {roleLabel(lineage.scoreStatus)}
                          </p>
                        )}
                      </td>
                      <td className="px-3 py-3">
                        <p>{formatDate(lineage.evaluatedAtUtc)}</p>
                        <p className="font-mono text-xs text-muted-foreground">
                          {shortId(lineage.integrityHash)}
                        </p>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <p className="break-all font-mono text-[11px] text-muted-foreground">
          Readiness decision {status.readinessDecisionId || '—'} · source hash{' '}
          {status.readinessSourceIntegrityHash || '—'} · decision hash{' '}
          {status.readinessIntegrityHash || '—'} · correlation{' '}
          {status.correlationId} · evaluated {formatDate(status.evaluatedAtUtc)}
        </p>
      </CardContent>
    </Card>
  );
}

function ReadinessOverview({
  decision,
}: {
  decision: ProcurementAwardReadinessDecision;
}) {
  const passedGroups = decision.prerequisiteGroups.filter(
    (group) => group.status === 'Passed'
  ).length;
  const applicableGroups = decision.prerequisiteGroups.filter(
    (group) => group.status !== 'NotApplicable'
  ).length;
  const lockedAttempts = decision.evaluations.flatMap(
    (evaluation) => evaluation.scoreAttempts
  ).filter((attempt) => attempt.status === 'Locked').length;
  const eligibleSuppliers = decision.suppliers.filter(
    (supplier) => supplier.isEligible
  ).length;
  const cards = [
    {
      label: 'Server outcome',
      value: decision.status,
      detail: decision.isCurrent ? 'Current decision' : 'Historical / stale',
      ok: decision.status === 'Ready' && decision.isCurrent,
    },
    {
      label: 'Prerequisites',
      value: `${passedGroups}/${applicableGroups}`,
      detail: 'Applicable groups passed',
      ok: passedGroups === applicableGroups,
    },
    {
      label: 'Score lineage',
      value: `${lockedAttempts} locked`,
      detail: `${decision.evaluations.length} retained evaluation records`,
      ok:
        lockedAttempts > 0 ||
        decision.prerequisiteGroups.some(
          (group) =>
            group.group === 'ScoreIntegrity' &&
            group.status === 'NotApplicable'
        ),
    },
    {
      label: 'Suppliers',
      value: `${eligibleSuppliers}/${decision.suppliers.length}`,
      detail: 'Current eligible suppliers',
      ok:
        decision.suppliers.length > 0 &&
        eligibleSuppliers === decision.suppliers.length,
    },
  ];

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map((card) => (
        <Card key={card.label}>
          <CardContent className="p-4">
            <div className="flex items-center justify-between gap-2">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">
                {card.label}
              </p>
              {card.ok ? (
                <CheckCircle2 className="h-4 w-4 text-emerald-600" />
              ) : (
                <AlertTriangle className="h-4 w-4 text-amber-600" />
              )}
            </div>
            <p className="mt-2 text-xl font-semibold">{card.value}</p>
            <p className="mt-1 text-xs text-muted-foreground">{card.detail}</p>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

function PrerequisiteRegister({
  decision,
  sourceType,
  sourceId,
}: {
  decision: ProcurementAwardReadinessDecision;
  sourceType: ProcurementAwardReadinessSourceType;
  sourceId: string;
}) {
  return (
    <Card data-testid="award-readiness-prerequisites">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <ClipboardCheck className="h-5 w-5" />
          Server-derived prerequisite register
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {decision.prerequisiteGroups.map((group) => (
          <div key={group.group} className="rounded-md border">
            <div className="flex flex-col justify-between gap-3 border-b bg-muted/30 p-4 sm:flex-row sm:items-center">
              <div>
                <p className="font-medium">
                  {awardReadinessGroupLabel[group.group]}
                </p>
                <p className="text-xs text-muted-foreground">
                  {group.items.length} retained check
                  {group.items.length === 1 ? '' : 's'}
                </p>
              </div>
              <Badge variant={prerequisiteVariant(group.status)}>
                {awardReadinessStatusLabel[group.status]}
              </Badge>
            </div>
            {group.items.length === 0 ? (
              <p className="p-4 text-sm text-muted-foreground">
                No source-applicable checks were returned for this group.
              </p>
            ) : (
              <div className="divide-y">
                {group.items.map((item) => (
                  <div
                    key={`${group.group}-${item.code}-${item.lineageId ?? ''}`}
                    className="grid gap-3 p-4 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-start"
                  >
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-medium">{item.label}</p>
                        <Badge variant={prerequisiteVariant(item.status)}>
                          {awardReadinessStatusLabel[item.status]}
                        </Badge>
                        <span className="font-mono text-[11px] text-muted-foreground">
                          {item.code}
                        </span>
                      </div>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {item.message}
                      </p>
                      {item.remediation && (
                        <p className="mt-2 text-sm">
                          <span className="font-medium">Remediation:</span>{' '}
                          {item.remediation}
                        </p>
                      )}
                      {(item.lineageType || item.lineageId || item.lineageHash) && (
                        <p className="mt-2 break-all font-mono text-[11px] text-muted-foreground">
                          {item.lineageType ?? 'Lineage'} ·{' '}
                          {shortId(item.lineageId)}
                          {item.lineageHash
                            ? ` · hash ${shortId(item.lineageHash)}`
                            : ''}
                        </p>
                      )}
                    </div>
                    {item.status === 'Failed' && (
                      <Button asChild variant="outline" size="sm">
                        <Link
                          href={awardReadinessRemediationHref(
                            sourceType,
                            sourceId,
                            group.group
                          )}
                        >
                          Open existing control
                        </Link>
                      </Button>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

function RecommendationAndActors({
  decision,
}: {
  decision: ProcurementAwardReadinessDecision;
}) {
  const recommendation = decision.recommendation;
  return (
    <Card data-testid="award-readiness-recommendation">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <UserCheck className="h-5 w-5" />
          Current recommendation and decision actors
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <Lineage label="Subject type" value={recommendation.subjectType || '—'} />
          <Lineage
            label="Recommended subject(s)"
            value={
              recommendation.subjectIds.map(shortId).join(', ') ||
              'No unambiguous subject'
            }
          />
          <Lineage
            label="Supplier identity"
            value={
              recommendation.businessPartnerIds.map(shortId).join(', ') ||
              'No unambiguous supplier'
            }
          />
          <Lineage
            label="Recommendation actor"
            value={shortId(recommendation.recommendedByUserId)}
          />
        </div>
        <div className="rounded-md border p-4">
          <p className="text-sm font-medium">Recommendation basis</p>
          <p className="mt-1 text-sm text-muted-foreground">
            {recommendation.reason || 'No recommendation reason retained.'}
          </p>
          <p className="mt-2 text-xs text-muted-foreground">
            {recommendation.evidenceReference || 'No evidence reference'} ·{' '}
            {formatDate(recommendation.recommendedAtUtc)}
          </p>
        </div>
        <div className="grid gap-4 md:grid-cols-3">
          <Lineage
            label="Readiness evaluated by"
            value={`${decision.evaluatedByName} · ${shortId(
              decision.evaluatedByUserId
            )}`}
          />
          <Lineage
            label="Authority approver"
            value={shortId(decision.authority.approvedByUserId)}
          />
          <Lineage
            label="Approval actors"
            value={
              decision.authority.approvalActorUserIds.map(shortId).join(', ') ||
              '—'
            }
          />
        </div>
      </CardContent>
    </Card>
  );
}

function EvaluationLineage({
  decision,
}: {
  decision: ProcurementAwardReadinessDecision;
}) {
  return (
    <Card data-testid="award-readiness-score-lineage">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FileLock2 className="h-5 w-5" />
          Completed evaluations and immutable score-attempt lineage
        </CardTitle>
      </CardHeader>
      <CardContent>
        {decision.evaluations.length === 0 ? (
          <EmptyState
            icon={FileLock2}
            title="No applicable evaluation lineage was returned"
            detail="The prerequisite register remains authoritative for whether an evaluation is required for this source."
          />
        ) : (
          <div className="space-y-4">
            {decision.evaluations.map((evaluation) => (
              <div key={evaluation.evaluationId} className="rounded-md border">
                <div className="flex flex-col justify-between gap-2 border-b bg-muted/30 p-4 sm:flex-row sm:items-center">
                  <div>
                    <p className="font-medium">
                      {evaluation.evaluationType} · {evaluation.phase}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {shortId(evaluation.evaluationId)} ·{' '}
                      {formatDate(evaluation.completedAtUtc)}
                    </p>
                  </div>
                  <Badge variant={statusVariant(evaluation.status)}>
                    {evaluation.status}
                  </Badge>
                </div>
                <div className="p-4">
                  <p className="mb-3 break-all text-xs text-muted-foreground">
                    Evidence: {evaluation.evidenceReference || '—'}
                    {evaluation.integrityHash
                      ? ` · hash ${shortId(evaluation.integrityHash)}`
                      : ''}
                  </p>
                  {evaluation.scoreAttempts.length === 0 ? (
                    <p className="text-sm text-muted-foreground">
                      No committee score attempts are applicable or retained.
                    </p>
                  ) : (
                    <div className="overflow-x-auto rounded-md border">
                      <table className="w-full min-w-[940px] text-sm">
                        <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                          <tr>
                            <th className="px-3 py-2">Subject</th>
                            <th className="px-3 py-2">Evaluator</th>
                            <th className="px-3 py-2">Phase / attempt</th>
                            <th className="px-3 py-2">Lock state</th>
                            <th className="px-3 py-2">Evidence / lineage</th>
                            <th className="px-3 py-2">Recall history</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y">
                          {evaluation.scoreAttempts.map((attempt) => (
                            <tr key={attempt.scoreSheetId}>
                              <td className="px-3 py-3">
                                <p className="font-medium">
                                  {attempt.scoreSubjectType}
                                </p>
                                <p className="font-mono text-xs text-muted-foreground">
                                  {shortId(attempt.scoreSubjectId)}
                                </p>
                              </td>
                              <td className="px-3 py-3">
                                <p>{attempt.submittedByName}</p>
                                <p className="text-xs text-muted-foreground">
                                  {formatDate(attempt.submittedAtUtc)}
                                </p>
                              </td>
                              <td className="px-3 py-3">
                                {attempt.phase} · attempt {attempt.attempt}
                              </td>
                              <td className="px-3 py-3">
                                <Badge variant={statusVariant(attempt.status)}>
                                  {attempt.status}
                                </Badge>
                              </td>
                              <td className="px-3 py-3">
                                <p>{attempt.evidenceReference}</p>
                                <p className="font-mono text-xs text-muted-foreground">
                                  {shortId(attempt.integrityHash)}
                                </p>
                              </td>
                              <td className="px-3 py-3">
                                {attempt.recallIds.length
                                  ? attempt.recallIds.map(shortId).join(', ')
                                  : 'None'}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function SupplierLineage({
  decision,
  sourceType,
  sourceId,
}: {
  decision: ProcurementAwardReadinessDecision;
  sourceType: ProcurementAwardReadinessSourceType;
  sourceId: string;
}) {
  return (
    <Card data-testid="award-readiness-supplier-lineage">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Users className="h-5 w-5" />
          Supplier, prequalification, verification, and due-diligence lineage
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-5">
        {decision.suppliers.length === 0 ? (
          <EmptyState
            icon={Users}
            title="No recommended supplier was resolved"
            detail="The server will keep the recommendation and supplier prerequisite groups blocked until the source resolves one eligible supplier."
          />
        ) : (
          decision.suppliers.map((supplier) => (
            <div key={supplier.businessPartnerId} className="rounded-md border p-4">
              <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-medium">{supplier.partnerName}</p>
                    <Badge variant="outline">{supplier.partnerCode}</Badge>
                    <Badge
                      variant={supplier.isEligible ? 'default' : 'destructive'}
                    >
                      {supplier.isEligible ? 'Eligible' : 'Ineligible'}
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {supplier.validationCode} ·{' '}
                    {shortId(supplier.businessPartnerId)}
                  </p>
                </div>
                {!supplier.isEligible && (
                  <Button asChild variant="outline" size="sm">
                    <Link
                      href={awardReadinessRemediationHref(
                        sourceType,
                        sourceId,
                        'SupplierEligibility'
                      )}
                    >
                      Review supplier controls
                    </Link>
                  </Button>
                )}
              </div>
              {(supplier.errors.length > 0 || supplier.warnings.length > 0) && (
                <div className="mt-4 grid gap-3 md:grid-cols-2">
                  <IssueList title="Validation errors" values={supplier.errors} />
                  <IssueList title="Warnings" values={supplier.warnings} />
                </div>
              )}
              <div className="mt-4">
                <p className="mb-2 text-sm font-medium">
                  Prequalification status
                </p>
                {supplier.prequalification.length === 0 ? (
                  <p className="text-sm text-muted-foreground">
                    No prequalification entry is applicable or retained.
                  </p>
                ) : (
                  <div className="grid gap-3 lg:grid-cols-2">
                    {supplier.prequalification.map((entry) => (
                      <div key={entry.entryId} className="rounded-md bg-muted/30 p-3">
                        <div className="flex items-center justify-between gap-2">
                          <p className="font-medium">
                            Category {shortId(entry.categoryId)}
                          </p>
                          <Badge variant={statusVariant(entry.status)}>
                            {entry.status}
                          </Badge>
                        </div>
                        <p className="mt-2 text-xs text-muted-foreground">
                          Valid {formatDate(entry.validFromUtc)} →{' '}
                          {formatDate(entry.expiresAtUtc)}
                        </p>
                        <p className="mt-1 text-xs text-muted-foreground">
                          {entry.approvalReference} ·{' '}
                          {entry.approvalEvidenceReference}
                        </p>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ))
        )}

        <div>
          <h3 className="mb-3 flex items-center gap-2 font-medium">
            <ClipboardCheck className="h-4 w-4" />
            Existing award-verification records
          </h3>
          {decision.verifications.length === 0 ? (
            <div className="rounded-md border border-dashed p-5 text-sm text-muted-foreground">
              No applicable completed verification snapshot was returned. Use the
              existing award-verification control; this workspace does not
              recreate its checklist or document UI.
              {sourceType !== 'RequestForQuotation' && (
                <Button asChild variant="link" className="ml-1 h-auto p-0">
                  <Link href={`/procurement/tenders/${sourceId}?tab=verification`}>
                    Open verification
                  </Link>
                </Button>
              )}
            </div>
          ) : (
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full min-w-[880px] text-sm">
                <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2">Supplier / bid</th>
                    <th className="px-3 py-2">Verification</th>
                    <th className="px-3 py-2">Completed</th>
                    <th className="px-3 py-2">Items / documents</th>
                    <th className="px-3 py-2">Snapshot integrity</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {decision.verifications.map((verification) => (
                    <tr key={verification.verificationBidderId}>
                      <td className="px-3 py-3">
                        <p>{shortId(verification.businessPartnerId)}</p>
                        <p className="text-xs text-muted-foreground">
                          bid {shortId(verification.tenderBidId)}
                        </p>
                      </td>
                      <td className="px-3 py-3">
                        <Badge
                          variant={statusVariant(
                            verification.verificationStatus
                          )}
                        >
                          {verification.verificationStatus}
                        </Badge>
                        <p className="mt-1 text-xs text-muted-foreground">
                          Bidder: {verification.bidderStatus}
                        </p>
                      </td>
                      <td className="px-3 py-3">
                        {formatDate(verification.completedAtUtc)}
                      </td>
                      <td className="px-3 py-3">
                        {verification.itemResultIds.length} /{' '}
                        {verification.documentIds.length}
                      </td>
                      <td className="px-3 py-3 font-mono text-xs">
                        {shortId(verification.snapshotIntegrityHash)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function AuthorityAndEvidence({
  decision,
  sourceType,
  sourceId,
}: {
  decision: ProcurementAwardReadinessDecision;
  sourceType: ProcurementAwardReadinessSourceType;
  sourceId: string;
}) {
  const authority = decision.authority;
  return (
    <div className="grid gap-6 xl:grid-cols-2">
      <Card data-testid="award-readiness-authority">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Route className="h-5 w-5" />
            Exact authority and shared-workflow lineage
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <Lineage
              label="Method rule"
              value={authority.methodRuleCode || shortId(authority.methodRuleId)}
            />
            <Lineage
              label="Authority route"
              value={
                authority.authorityRouteReference ||
                shortId(authority.authorityRouteId)
              }
            />
            <Lineage
              label="Workflow"
              value={shortId(authority.workflowDefinitionId)}
            />
            <Lineage
              label="Workflow instance"
              value={shortId(authority.workflowInstanceId)}
            />
            <Lineage
              label="Workflow outcome"
              value={authority.workflowStatus || '—'}
            />
            <Lineage
              label="Approval"
              value={`${authority.approvalReference || '—'} · ${formatDate(
                authority.approvedAtUtc
              )}`}
            />
          </div>
          <p className="text-sm text-muted-foreground">
            Workflow decisions, evidence files, SOD, and approval execution remain
            owned by their shared controls.
          </p>
          <Button asChild variant="outline">
            <Link
              href={awardReadinessRemediationHref(
                sourceType,
                sourceId,
                'AuthorityAndWorkflow'
              )}
            >
              Open existing approval controls
            </Link>
          </Button>
        </CardContent>
      </Card>

      <Card data-testid="award-readiness-evidence">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileCheck2 className="h-5 w-5" />
            Required evidence register
          </CardTitle>
        </CardHeader>
        <CardContent>
          {decision.evidence.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No evidence requirements were returned.
            </p>
          ) : (
            <div className="divide-y rounded-md border">
              {decision.evidence.map((evidence) => (
                <div
                  key={`${evidence.requirementKey}-${evidence.referenceId ?? ''}`}
                  className="flex flex-col justify-between gap-2 p-3 sm:flex-row sm:items-center"
                >
                  <div>
                    <p className="font-medium">{evidence.label}</p>
                    <p className="text-xs text-muted-foreground">
                      {evidence.requirementKey} ·{' '}
                      {evidence.reference || shortId(evidence.referenceId)}
                    </p>
                  </div>
                  <Badge
                    variant={evidence.isAvailable ? 'default' : 'destructive'}
                  >
                    {evidence.isAvailable ? 'Available' : 'Missing'}
                  </Badge>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function DecisionHistory({
  decision,
  history,
}: {
  decision: ProcurementAwardReadinessDecision;
  history: ProcurementAwardReadinessDecision[];
}) {
  const decisions = history.length > 0 ? history : [decision];
  return (
    <Card data-testid="award-readiness-immutable-history">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <History className="h-5 w-5" />
          Immutable decision and control timeline
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-6">
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full min-w-[900px] text-sm">
            <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-3 py-2">Decision</th>
                <th className="px-3 py-2">Outcome</th>
                <th className="px-3 py-2">Actor / time</th>
                <th className="px-3 py-2">Source state</th>
                <th className="px-3 py-2">Integrity</th>
                <th className="px-3 py-2">Correlation</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {[...decisions]
                .sort((a, b) => b.decisionSequence - a.decisionSequence)
                .map((item) => (
                  <tr key={item.id}>
                    <td className="px-3 py-3">
                      <p className="font-medium">#{item.decisionSequence}</p>
                      <p className="font-mono text-xs text-muted-foreground">
                        {shortId(item.id)}
                      </p>
                    </td>
                    <td className="px-3 py-3">
                      <Badge variant={statusVariant(item.status)}>
                        {item.status}
                      </Badge>
                      {!item.isCurrent && (
                        <Badge variant="outline" className="ml-2">
                          Historical
                        </Badge>
                      )}
                    </td>
                    <td className="px-3 py-3">
                      <p>{item.evaluatedByName}</p>
                      <p className="text-xs text-muted-foreground">
                        {formatDate(item.evaluatedAtUtc)}
                      </p>
                    </td>
                    <td className="px-3 py-3 font-mono text-xs">
                      {shortId(item.sourceIntegrityHash)}
                    </td>
                    <td className="px-3 py-3 font-mono text-xs">
                      {shortId(item.integrityHash)}
                    </td>
                    <td className="px-3 py-3 font-mono text-xs">
                      {shortId(item.correlationId)}
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>

        <div className="relative ml-2 border-l">
          {decision.timeline.length === 0 ? (
            <p className="pl-6 text-sm text-muted-foreground">
              No source control events were returned with this decision.
            </p>
          ) : (
            [...decision.timeline]
              .sort(
                (a, b) =>
                  new Date(b.occurredAtUtc).getTime() -
                  new Date(a.occurredAtUtc).getTime()
              )
              .map((entry, index) => (
                <div
                  key={`${entry.eventType}-${entry.occurredAtUtc}-${index}`}
                  className="relative pb-6 pl-6 last:pb-0"
                >
                  <span className="absolute -left-1.5 top-1.5 h-3 w-3 rounded-full border bg-background" />
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-medium">{roleLabel(entry.eventType)}</p>
                    <span className="text-xs text-muted-foreground">
                      {formatDate(entry.occurredAtUtc)}
                    </span>
                  </div>
                  <p className="mt-1 break-all text-sm text-muted-foreground">
                    Actor {shortId(entry.actorUserId)}
                    {entry.reference ? ` · ${entry.reference}` : ''}
                    {entry.integrityHash
                      ? ` · hash ${shortId(entry.integrityHash)}`
                      : ''}
                  </p>
                </div>
              ))
          )}
        </div>
        <p className="break-all font-mono text-[11px] text-muted-foreground">
          Decision hash: {decision.integrityHash} · source hash:{' '}
          {decision.sourceIntegrityHash} · idempotency: {decision.idempotencyKey}
        </p>
      </CardContent>
    </Card>
  );
}

function Lineage({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border p-3">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">
        {label}
      </p>
      <p className="mt-1 break-words text-sm font-medium">{value}</p>
    </div>
  );
}

function IssueList({ title, values }: { title: string; values: string[] }) {
  return (
    <div className="rounded-md bg-muted/30 p-3">
      <p className="text-sm font-medium">{title}</p>
      {values.length === 0 ? (
        <p className="mt-1 text-xs text-muted-foreground">None</p>
      ) : (
        <ul className="mt-2 list-disc space-y-1 pl-5 text-xs text-muted-foreground">
          {values.map((value, index) => (
            <li key={`${value}-${index}`}>{value}</li>
          ))}
        </ul>
      )}
    </div>
  );
}

function EmptyState({
  icon: Icon,
  title,
  detail,
}: {
  icon: typeof Users;
  title: string;
  detail: string;
}) {
  return (
    <div className="flex min-h-40 flex-col items-center justify-center rounded-md border border-dashed p-6 text-center">
      <Icon className="mb-3 h-8 w-8 text-muted-foreground" />
      <p className="font-medium">{title}</p>
      <p className="mt-1 max-w-2xl text-sm text-muted-foreground">{detail}</p>
    </div>
  );
}
