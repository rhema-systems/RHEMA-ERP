'use client';

import React from 'react';
import { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowLeft,
  FileCheck2,
  Loader2,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react';

import {
  BidderCommunicationActionDialog,
  type BidderCommunicationAction,
} from './BidderCommunicationActionDialog';
import { BidderCommunicationRegister } from './BidderCommunicationRegister';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAuth } from '@/hooks/use-auth';
import {
  bidderCommunicationExternalBackHref,
  bidderCommunicationInternalBackHref,
  bidderCommunicationSourceLabel,
} from '@/lib/procurement-bidder-communication';
import { procurementAwardReadinessService } from '@/services/procurement-award-readiness.service';
import { procurementBidderCommunicationService as service } from '@/services/procurement-bidder-communication.service';
import type { ProcurementBidderCommunicationSourceType } from '@/types/procurement-bidder-communication';

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'The request could not be completed.';
const errorStatus = (error: unknown) =>
  typeof error === 'object' && error !== null && 'status' in error
    ? Number((error as { status?: unknown }).status)
    : undefined;

export function BidderCommunicationWorkspace({
  sourceType,
  sourceId,
  external = false,
}: {
  sourceType: ProcurementBidderCommunicationSourceType;
  sourceId: string;
  external?: boolean;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [action, setAction] = useState<BidderCommunicationAction>();
  const canManage =
    !external && hasPermission('procurement.tender.administer');
  const canApprove =
    !external && hasPermission('procurement.tender.approve');
  const queryKey = [
    'procurement-bidder-communications',
    sourceType,
    sourceId,
    external ? 'external' : 'internal',
  ];

  const overview = useQuery({
    queryKey,
    queryFn: () =>
      external
        ? service.getExternalStatus(sourceType, sourceId)
        : service.getOverview(sourceType, sourceId),
    enabled: Boolean(sourceId),
    retry: false,
  });
  const awardReadiness = useQuery({
    queryKey: [
      'procurement-bidder-communications-award-readiness',
      sourceType,
      sourceId,
    ],
    queryFn: () => procurementAwardReadinessService.latest(sourceType, sourceId),
    enabled: !external && Boolean(sourceId),
    retry: false,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    await overview.refetch();
  };

  const backHref = external
    ? bidderCommunicationExternalBackHref(sourceType, sourceId)
    : bidderCommunicationInternalBackHref(sourceType, sourceId);
  const sourceLabel = bidderCommunicationSourceLabel(sourceType);

  if (overview.isLoading)
    return (
      <div
        className="flex min-h-[420px] flex-col items-center justify-center gap-2"
        data-testid="bidder-communication-loading"
      >
        <Loader2 className="h-7 w-7 animate-spin" />
        <p className="text-sm text-muted-foreground">
          Loading authoritative bidder communication status…
        </p>
      </div>
    );

  const registerMissing =
    !external && overview.isError && errorStatus(overview.error) === 404;
  if (registerMissing)
    return (
      <div
        className="space-y-6 p-4 md:p-6"
        data-testid="bidder-communication-not-initialized"
      >
        <Button asChild variant="ghost" size="sm">
          <Link href={backHref}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            {sourceLabel}
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold">
            Bidder communications and tender-security register
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {sourceLabel} {sourceId} · no register has yet been retained.
          </p>
        </div>
        <Alert>
          <ShieldCheck className="h-4 w-4" />
          <AlertTitle>Initialize from the current award decision</AlertTitle>
          <AlertDescription>
            The server derives successful and unsuccessful recipients. Enter
            only the authorized standstill and appeal-window dates; supplier
            identities cannot be supplied by this client.
          </AlertDescription>
        </Alert>
        <div className="flex min-h-48 flex-col items-center justify-center rounded-lg border border-dashed p-6 text-center">
          {awardReadiness.isLoading ? (
            <>
              <Loader2 className="mb-3 h-7 w-7 animate-spin" />
              <p className="text-sm text-muted-foreground">
                Verifying current award readiness…
              </p>
            </>
          ) : awardReadiness.data?.isReady &&
            awardReadiness.data.isCurrent &&
            canManage ? (
            <>
              <FileCheck2 className="mb-3 h-8 w-8 text-muted-foreground" />
              <p className="font-medium">
                Award-readiness decision #
                {awardReadiness.data.decisionSequence} is current and Ready
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                {awardReadiness.data.sourceReference} · recipients will be
                derived from its exact recommendation and award.
              </p>
              <Button
                className="mt-4"
                onClick={() => setAction({ type: 'initialize' })}
              >
                Initialize communication register
              </Button>
            </>
          ) : (
            <>
              <AlertTriangle className="mb-3 h-8 w-8 text-amber-600" />
              <p className="font-medium">Initialization is not available</p>
              <p className="mt-1 max-w-xl text-sm text-muted-foreground">
                A current Ready award-readiness decision and the procurement
                administration permission are required.
              </p>
            </>
          )}
        </div>
        <BidderCommunicationActionDialog
          sourceType={sourceType}
          sourceId={sourceId}
          awardReadiness={awardReadiness.data}
          action={action}
          external={false}
          onOpenChange={(open) => !open && setAction(undefined)}
          onChanged={refresh}
        />
      </div>
    );

  if (overview.isError || !overview.data)
    return (
      <div
        className="space-y-4 p-4 md:p-6"
        data-testid="bidder-communication-unavailable"
      >
        <Button asChild variant="ghost" size="sm">
          <Link href={backHref}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Return to {sourceLabel.toLowerCase()}
          </Link>
        </Button>
        <Alert className="border-destructive/50 bg-destructive/5">
          <ShieldCheck className="h-4 w-4" />
          <AlertTitle>
            {external
              ? 'My bidder communication status is unavailable'
              : 'Bidder communication register is unavailable'}
          </AlertTitle>
          <AlertDescription>
            {errorMessage(overview.error)} Access remains fail closed. The
            source may be missing, outside the current tenant or supplier, or
            this account may not be authorized.
          </AlertDescription>
        </Alert>
      </div>
    );

  return (
    <div
      className="space-y-6 p-4 md:p-6"
      data-testid={
        external
          ? 'external-bidder-communication-workspace'
          : 'bidder-communication-workspace'
      }
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
              {external
                ? 'My award result, communications and tender security'
                : 'Bidder communications and tender-security register'}
            </h1>
            <Badge variant="outline">{sourceLabel}</Badge>
            <Badge
              variant={
                overview.data.blockedReasons.length > 0
                  ? 'destructive'
                  : 'default'
              }
            >
              {overview.data.blockedReasons.length > 0
                ? 'Action blocked'
                : 'Active'}
            </Badge>
          </div>
          <p className="mt-1 max-w-4xl text-sm text-muted-foreground">
            {overview.data.sourceReference} · server-derived recipients,
            immutable approved letters, dispatch,
            delivery and acknowledgement history, standstill, appeals, and
            tender-security release or return.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {!external && sourceType !== 'RequestForQuotation' && (
            <Select
              value={sourceType}
              onValueChange={(value) =>
                router.push(
                  `/procurement/tenders/${sourceId}/bidder-communications${
                    value === 'ExceptionalSourcing'
                      ? '?sourceType=ExceptionalSourcing'
                      : ''
                  }`
                )
              }
            >
              <SelectTrigger className="w-[210px]">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Tender">
                  Tender / legacy award
                </SelectItem>
                <SelectItem value="ExceptionalSourcing">
                  Exceptional sourcing
                </SelectItem>
              </SelectContent>
            </Select>
          )}
          {!external && (
            <Button asChild variant="outline" size="sm">
              <Link href={backHref}>
                <FileCheck2 className="mr-2 h-4 w-4" />
                Award readiness
              </Link>
            </Button>
          )}
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void refresh()}
            disabled={overview.isFetching}
          >
            <RefreshCw
              className={`mr-2 h-4 w-4 ${
                overview.isFetching ? 'animate-spin' : ''
              }`}
            />
            Refresh
          </Button>
        </div>
      </div>

      <Alert>
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>
          {external
            ? 'Supplier-scoped authoritative status'
            : 'Award-derived shared-control boundary'}
        </AlertTitle>
        <AlertDescription>
          {external
            ? 'Only the authenticated supplier result is requested and rendered. Acknowledgement and appeal actions apply to the exact visible dispatch and outcome.'
            : 'Recipients come only from the current award/readiness lineage. Letter approval, dispatch, standstill, appeals, security eligibility, shared evidence, notification, workflow, SOD, and immutable audit remain server-authoritative.'}
        </AlertDescription>
      </Alert>

      <BidderCommunicationRegister
        overview={overview.data}
        external={external}
        canManage={canManage}
        canApprove={canApprove}
        onAction={setAction}
      />

      <BidderCommunicationActionDialog
        sourceType={sourceType}
        sourceId={sourceId}
        overview={overview.data}
        action={action}
        external={external}
        onOpenChange={(open) => !open && setAction(undefined)}
        onChanged={refresh}
      />
    </div>
  );
}
