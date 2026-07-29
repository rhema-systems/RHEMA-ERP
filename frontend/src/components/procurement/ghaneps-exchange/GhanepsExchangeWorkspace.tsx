'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowLeft,
  Loader2,
  MailCheck,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react';

import {
  GhanepsExchangeActionDialog,
  type GhanepsExchangeAction,
} from './GhanepsExchangeActionDialog';
import { GhanepsExchangeRegister } from './GhanepsExchangeRegister';
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
  ghanepsBackHref,
  ghanepsSourceLabel,
  isGhanepsProfileFailClosed,
} from '@/lib/procurement-ghaneps-exchange';
import { procurementGhanepsExchangeService as service } from '@/services/procurement-ghaneps-exchange.service';
import type { ProcurementGhanepsSourceType } from '@/types/procurement-ghaneps-exchange';

const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : 'The request could not be completed.';

const errorStatus = (error: unknown) =>
  typeof error === 'object' && error !== null && 'status' in error
    ? Number((error as { status?: unknown }).status)
    : undefined;

export function GhanepsExchangeWorkspace({
  sourceType,
  sourceId,
}: {
  sourceType: ProcurementGhanepsSourceType;
  sourceId: string;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const [action, setAction] = useState<GhanepsExchangeAction>();
  const canManage = hasPermission('procurement.tender.administer');
  const rootKey = ['procurement-ghaneps-exchanges', sourceType, sourceId];

  const overview = useQuery({
    queryKey: [...rootKey, 'overview'],
    queryFn: () => service.getOverview(sourceType, sourceId),
    enabled: Boolean(sourceId),
    retry: false,
  });
  const options = useQuery({
    queryKey: [...rootKey, 'options'],
    queryFn: () => service.getOptions(sourceType, sourceId),
    enabled: Boolean(sourceId),
    retry: false,
  });
  const status = useQuery({
    queryKey: [...rootKey, 'status'],
    queryFn: () => service.getStatus(sourceType, sourceId),
    enabled: Boolean(sourceId),
    retry: false,
  });
  const history = useQuery({
    queryKey: [...rootKey, 'history'],
    queryFn: () => service.getHistory(sourceType, sourceId),
    enabled: Boolean(sourceId),
    retry: false,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: rootKey });
    await Promise.all([
      overview.refetch(),
      options.refetch(),
      status.refetch(),
      history.refetch(),
    ]);
  };

  const loading =
    overview.isLoading ||
    options.isLoading ||
    status.isLoading ||
    history.isLoading;
  const firstError =
    overview.error ?? options.error ?? status.error ?? history.error;
  const failed =
    overview.isError ||
    options.isError ||
    status.isError ||
    history.isError;
  const backHref = ghanepsBackHref(sourceType, sourceId);
  const sourceLabel = ghanepsSourceLabel(sourceType);

  if (loading)
    return (
      <div
        className="flex min-h-[420px] flex-col items-center justify-center gap-2"
        data-testid="ghaneps-exchange-loading"
      >
        <Loader2 className="h-7 w-7 animate-spin" />
        <p className="text-sm text-muted-foreground">
          Loading authoritative GHANEPS exchange history…
        </p>
      </div>
    );

  if (failed)
    return (
      <Unavailable
        backHref={backHref}
        sourceLabel={sourceLabel}
        error={firstError}
        conflict={errorStatus(firstError) === 409}
      />
    );

  const sourceMismatch =
    !overview.data ||
    !options.data ||
    !status.data ||
    overview.data.sourceType !== sourceType ||
    options.data.sourceType !== sourceType ||
    status.data.sourceType !== sourceType ||
    overview.data.sourceId.toLowerCase() !== sourceId.toLowerCase() ||
    options.data.sourceId.toLowerCase() !== sourceId.toLowerCase() ||
    status.data.sourceId.toLowerCase() !== sourceId.toLowerCase();
  const configurationInvalid = isGhanepsProfileFailClosed(options.data);
  if (sourceMismatch || configurationInvalid)
    return (
      <Unavailable
        backHref={backHref}
        sourceLabel={sourceLabel}
        error={
          new Error(
            sourceMismatch
              ? 'The server response did not match the exact requested source.'
              : 'No exact Published/effective DEC-009 profile and mapping could be verified.'
          )
        }
        conflict
      />
    );

  const eventActions = Object.fromEntries(
    status.data.events.map((event) => [event.id, event.allowedActions])
  );
  const eventBlockedReasons = Object.fromEntries(
    status.data.events.map((event) => [event.id, event.blockedReasons])
  );
  const bidderCommunicationsHref =
    sourceType === 'RequestForQuotation'
      ? `/procurement/rfqs/${sourceId}/bidder-communications`
      : `/procurement/tenders/${sourceId}/bidder-communications${
          sourceType === 'ExceptionalSourcing'
            ? '?sourceType=ExceptionalSourcing'
            : ''
        }`;

  return (
    <div
      className="space-y-6 p-4 md:p-6"
      data-testid="ghaneps-exchange-workspace"
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
              GHANEPS tender and award exchange
            </h1>
            <Badge variant="outline">{sourceLabel}</Badge>
            <Badge variant="secondary">{overview.data.sourceVariant}</Badge>
            <Badge
              variant={
                options.data.blockedReasons.length > 0
                  ? 'destructive'
                  : 'default'
              }
            >
              {options.data.blockedReasons.length > 0
                ? 'Action blocked'
                : 'DEC-009 effective'}
            </Badge>
          </div>
          <p className="mt-1 max-w-4xl text-sm text-muted-foreground">
            {overview.data.sourceReference} · exact source/event/profile and
            DEC-009 mapping lineage, immutable payload versions, transport
            attempts, acknowledgements, failures, retries, and reconciliation.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {sourceType !== 'RequestForQuotation' && (
            <Select
              value={sourceType}
              onValueChange={(value) =>
                router.push(
                  `/procurement/tenders/${sourceId}/ghaneps-exchange${
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
                <SelectItem value="Tender">Tender</SelectItem>
                <SelectItem value="ExceptionalSourcing">
                  Exceptional sourcing
                </SelectItem>
              </SelectContent>
            </Select>
          )}
          <Button asChild variant="outline" size="sm">
            <Link href={bidderCommunicationsHref}>
              <MailCheck className="mr-2 h-4 w-4" />
              Bidder communications
            </Link>
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void refresh()}
            disabled={
              overview.isFetching ||
              options.isFetching ||
              status.isFetching ||
              history.isFetching
            }
          >
            <RefreshCw
              className={`mr-2 h-4 w-4 ${
                overview.isFetching ||
                options.isFetching ||
                status.isFetching ||
                history.isFetching
                  ? 'animate-spin'
                  : ''
              }`}
            />
            Refresh
          </Button>
        </div>
      </div>

      <Alert>
        <ShieldCheck className="h-4 w-4" />
        <AlertTitle>Server-authoritative exchange boundary</AlertTitle>
        <AlertDescription>
          Source, status, event family, mapping, Published/effective DEC-009
          lineage, checksums, retry limits, actor, tenant, SOD, notification,
          shared evidence, and immutable audit are revalidated by the server.
          This client sends no tenant identifier and cannot overwrite history.
        </AlertDescription>
      </Alert>

      <GhanepsExchangeRegister
        overview={overview.data}
        options={options.data}
        history={history.data ?? []}
        canManage={canManage}
        hasConfiguredPermission={hasPermission}
        serverActions={options.data.allowedActions}
        eventActions={eventActions}
        eventBlockedReasons={eventBlockedReasons}
        onAction={setAction}
      />

      <GhanepsExchangeActionDialog
        sourceType={sourceType}
        sourceId={sourceId}
        sourceReference={overview.data.sourceReference}
        action={action}
        onOpenChange={(open) => !open && setAction(undefined)}
        onChanged={refresh}
      />
    </div>
  );
}

function Unavailable({
  backHref,
  sourceLabel,
  error,
  conflict,
}: {
  backHref: string;
  sourceLabel: string;
  error: unknown;
  conflict: boolean;
}) {
  return (
    <div
      className="space-y-4 p-4 md:p-6"
      data-testid={
        conflict
          ? 'ghaneps-exchange-conflict'
          : 'ghaneps-exchange-unavailable'
      }
    >
      <Button asChild variant="ghost" size="sm">
        <Link href={backHref}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Return to {sourceLabel.toLowerCase()}
        </Link>
      </Button>
      <Alert className="border-destructive/50 bg-destructive/5">
        <AlertTriangle className="h-4 w-4" />
        <AlertTitle>
          {conflict
            ? 'GHANEPS exchange configuration is not authoritative'
            : 'GHANEPS exchange is unavailable'}
        </AlertTitle>
        <AlertDescription>
          {errorMessage(error)} Access remains fail closed. Confirm one exact
          Published/effective DEC-009 profile and mapping exists for this
          source before retrying.
        </AlertDescription>
      </Alert>
    </div>
  );
}
