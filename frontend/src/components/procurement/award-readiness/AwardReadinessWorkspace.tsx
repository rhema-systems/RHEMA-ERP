'use client';

import React from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  ClipboardCheck,
  Loader2,
  MailCheck,
  RefreshCw,
  ShieldCheck,
  Users,
} from 'lucide-react';
import { toast } from 'sonner';

import { AwardReadinessRegister } from './AwardReadinessRegister';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { useAuth } from '@/hooks/use-auth';
import {
  awardReadinessSourceLabel,
  createAwardReadinessEvaluationRequest,
} from '@/lib/procurement-award-readiness';
import { getProcurementProblemMessage } from '@/lib/procurement-tender-header-actions';
import { procurementAwardReadinessService as service } from '@/services/procurement-award-readiness.service';
import type {
  ProcurementAwardReadinessDecision,
  ProcurementAwardReadinessSourceType,
} from '@/types/procurement-award-readiness';

const errorStatus = (error: unknown) =>
  typeof error === 'object' && error !== null && 'status' in error
    ? Number((error as { status?: unknown }).status)
    : undefined;

export function AwardReadinessWorkspace({
  sourceType,
  sourceId,
}: {
  sourceType: ProcurementAwardReadinessSourceType;
  sourceId: string;
}) {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canEvaluate = hasPermission('procurement.tender.approve');
  const queryKey = ['procurement-award-readiness', sourceType, sourceId];

  const latest = useQuery({
    queryKey: [...queryKey, 'latest'],
    queryFn: () => service.latest(sourceType, sourceId),
    enabled: Boolean(sourceId),
    retry: false,
  });
  const history = useQuery({
    queryKey: [...queryKey, 'history'],
    queryFn: () => service.history(sourceType, sourceId, 50),
    enabled: Boolean(sourceId),
    retry: false,
  });
  const sodStatus = useQuery({
    queryKey: [...queryKey, 'sod-status'],
    queryFn: () => service.sodStatus(sourceType, sourceId),
    enabled: Boolean(sourceId),
    retry: false,
  });

  const retainedHistory = [...(history.data ?? [])].sort(
    (a, b) => b.decisionSequence - a.decisionSequence
  );
  const decision = latest.data ?? retainedHistory[0];

  const evaluate = useMutation({
    mutationFn: () =>
      service.evaluate(
        sourceType,
        sourceId,
        createAwardReadinessEvaluationRequest(decision)
      ),
    onSuccess: async (result) => {
      queryClient.setQueryData<ProcurementAwardReadinessDecision>(
        [...queryKey, 'latest'],
        result
      );
      await queryClient.invalidateQueries({
        queryKey: [...queryKey, 'history'],
      });
      await queryClient.invalidateQueries({
        queryKey: [...queryKey, 'sod-status'],
      });
      toast.success(
        result.isReady
          ? 'Award readiness passed and the immutable decision was retained.'
          : 'Award remains blocked; the immutable decision and remediation were retained.'
      );
    },
    onError: (error) => toast.error(getProcurementProblemMessage(error)),
  });

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: [...queryKey, 'latest'],
      }),
      queryClient.invalidateQueries({
        queryKey: [...queryKey, 'history'],
      }),
      queryClient.invalidateQueries({
        queryKey: [...queryKey, 'sod-status'],
      }),
    ]);
  };

  const isExceptional = sourceType === 'ExceptionalSourcing';
  const backHref =
    sourceType === 'RequestForQuotation'
      ? `/procurement/rfqs/${sourceId}/controls`
      : isExceptional
        ? `/procurement/tenders/${sourceId}/exception-controls`
        : `/procurement/tenders/${sourceId}`;
  const committeeHref =
    sourceType === 'RequestForQuotation'
      ? `/procurement/rfqs/${sourceId}/committee-controls`
      : `/procurement/tenders/${sourceId}/committee-controls`;
  const bidderCommunicationsHref =
    sourceType === 'RequestForQuotation'
      ? `/procurement/rfqs/${sourceId}/bidder-communications`
      : `/procurement/tenders/${sourceId}/bidder-communications${
          sourceType === 'ExceptionalSourcing'
            ? '?sourceType=ExceptionalSourcing'
            : ''
        }`;
  const sourceLabel = awardReadinessSourceLabel(sourceType);

  if ((latest.isLoading || history.isLoading) && !decision) {
    return (
      <div className="flex min-h-[420px] items-center justify-center">
        <Loader2 className="h-7 w-7 animate-spin" />
      </div>
    );
  }

  const latestFailedUnexpectedly =
    latest.isError && errorStatus(latest.error) !== 404;
  if (history.isError || latestFailedUnexpectedly) {
    const error = history.error ?? latest.error;
    return (
      <Card>
        <CardContent className="flex min-h-72 flex-col items-center justify-center p-8 text-center">
          <ShieldCheck className="mb-4 h-10 w-10 text-muted-foreground" />
          <h1 className="text-lg font-semibold">
            Award-readiness controls are unavailable
          </h1>
          <p className="mt-2 max-w-xl text-sm text-muted-foreground">
            {getProcurementProblemMessage(error)}
          </p>
          <Button asChild variant="outline" className="mt-5">
            <Link href={backHref}>Return to {sourceLabel.toLowerCase()}</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div
      className="space-y-6 p-4 md:p-6"
      data-testid="award-readiness-workspace"
    >
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <Button asChild variant="ghost" size="sm" className="-ml-3 mb-2">
            <Link href={backHref}>
              <ArrowLeft className="mr-2 h-4 w-4" />
              {sourceLabel}
            </Link>
          </Button>
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold">
              Recommendation approval and award readiness
            </h1>
            <Badge variant="outline">{sourceLabel}</Badge>
            {decision && (
              <Badge
                variant={
                  decision.status === 'Ready' && decision.isCurrent
                    ? 'default'
                    : 'destructive'
                }
              >
                {decision.status}
                {!decision.isCurrent ? ' · Historical' : ''}
              </Badge>
            )}
          </div>
          <p className="mt-1 max-w-4xl text-sm text-muted-foreground">
            {decision?.sourceReference ?? sourceId} · server-derived
            recommendation, evaluation and locked-score lineage, supplier
            eligibility, verification and due diligence, exact authority,
            workflow, evidence, actors, blocked remediation, and immutable
            decisions.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href={committeeHref}>
              <Users className="mr-2 h-4 w-4" />
              Committee controls
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href={bidderCommunicationsHref}>
              <MailCheck className="mr-2 h-4 w-4" />
              Bidder communications
            </Link>
          </Button>
          {sourceType !== 'RequestForQuotation' && (
            <Button asChild variant="outline">
              <Link href={`/procurement/tenders/${sourceId}?tab=verification`}>
                <ClipboardCheck className="mr-2 h-4 w-4" />
                Award verification
              </Link>
            </Button>
          )}
          <Button
            variant="outline"
            onClick={() => void refresh()}
            disabled={latest.isFetching || history.isFetching}
          >
            <RefreshCw
              className={`mr-2 h-4 w-4 ${
                latest.isFetching || history.isFetching ? 'animate-spin' : ''
              }`}
            />
            Refresh history
          </Button>
        </div>
      </div>

      <Alert>
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>Authoritative shared-control boundary</AlertTitle>
        <AlertDescription>
          This workspace evaluates and explains readiness; it cannot toggle
          readiness, approve a workflow, replace the recommendation, or create
          evidence. Existing sourcing, committee, verification, supplier,
          prequalification, authority, workflow, evidence, SOD, audit, and award
          services remain authoritative, and the final award boundary rechecks
          the current server decision.
        </AlertDescription>
      </Alert>

      <AwardReadinessRegister
        sourceType={sourceType}
        sourceId={sourceId}
        decision={decision}
        history={retainedHistory}
        sodStatus={sodStatus.data}
        isSodStatusLoading={sodStatus.isLoading}
        sodStatusError={
          sodStatus.isError
            ? getProcurementProblemMessage(sodStatus.error)
            : undefined
        }
        canEvaluate={canEvaluate}
        isEvaluating={evaluate.isPending}
        onEvaluate={() => evaluate.mutate()}
      />
    </div>
  );
}
