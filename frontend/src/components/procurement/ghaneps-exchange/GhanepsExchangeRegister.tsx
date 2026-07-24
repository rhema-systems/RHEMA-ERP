'use client';

import React from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Download,
  FileOutput,
  History,
  RefreshCw,
  ShieldCheck,
  XCircle,
} from 'lucide-react';

import type { GhanepsExchangeAction } from './GhanepsExchangeActionDialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  downloadGhanepsAcknowledgement,
  downloadGhanepsPayload,
  ghanepsContentTypeMetadata,
  ghanepsExchangeCounts,
  hasGhanepsAction,
} from '@/lib/procurement-ghaneps-exchange';
import type {
  ProcurementGhanepsExchangeEvent,
  ProcurementGhanepsExchangeHistoryItem,
  ProcurementGhanepsExchangeMappingOption,
  ProcurementGhanepsExchangeOptions,
  ProcurementGhanepsExchangeOverview,
  ProcurementGhanepsExchangePayload,
} from '@/types/procurement-ghaneps-exchange';

const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';

const display = (value?: string) =>
  value
    ? value
        .replace(/([a-z])([A-Z])/g, '$1 $2')
        .replace(/[_-]+/g, ' ')
    : '—';

const shortHash = (value?: string) =>
  value ? `${value.slice(0, 10)}…${value.slice(-8)}` : '—';

const badgeVariant = (
  value?: string
): 'default' | 'destructive' | 'outline' | 'secondary' => {
  if (
    [
      'Succeeded',
      'Accepted',
      'Acknowledged',
      'Transferred',
      'Matched',
      'Resolved',
      'Reconciled',
    ].includes(value ?? '')
  )
    return 'default';
  if (
    ['Failed', 'Rejected', 'Mismatch', 'ReconciliationException'].includes(
      value ?? ''
    )
  )
    return 'destructive';
  return 'secondary';
};

export function GhanepsExchangeRegister({
  overview,
  options,
  history,
  canManage,
  hasConfiguredPermission,
  serverActions,
  eventActions,
  eventBlockedReasons,
  onAction,
}: {
  overview: ProcurementGhanepsExchangeOverview;
  options: ProcurementGhanepsExchangeOptions;
  history: ProcurementGhanepsExchangeHistoryItem[];
  canManage: boolean;
  hasConfiguredPermission: (permissionCode: string) => boolean;
  serverActions: string[];
  eventActions: Record<string, string[]>;
  eventBlockedReasons: Record<string, string[]>;
  onAction: (action: GhanepsExchangeAction) => void;
}) {
  const counts = ghanepsExchangeCounts(overview);
  const blockedReasons = options.blockedReasons;

  return (
    <div className="space-y-5" data-testid="ghaneps-exchange-register">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Summary label="Exchange events" value={counts.events} icon={History} />
        <Summary
          label="Export payloads"
          value={counts.exports}
          icon={FileOutput}
        />
        <Summary
          label="Acknowledged"
          value={counts.acknowledged}
          icon={CheckCircle2}
          ok={counts.acknowledged > 0}
        />
        <Summary
          label="Failed attempts"
          value={counts.failedAttempts}
          icon={XCircle}
          danger={counts.failedAttempts > 0}
        />
        <Summary
          label="Reconciled"
          value={counts.reconciled}
          detail={
            counts.mismatched > 0
              ? `${counts.mismatched} mismatch record(s)`
              : 'No retained mismatch'
          }
          icon={ShieldCheck}
          ok={counts.reconciled > 0 && counts.mismatched === 0}
        />
      </div>

      {blockedReasons.length > 0 && (
        <BlockedReasons
          title="GHANEPS exchange actions are blocked"
          reasons={blockedReasons}
        />
      )}

      <ProfileLineage options={options} />

      <MappingOptions
        options={options}
        canManage={canManage}
        serverActions={serverActions}
        onAction={onAction}
      />

      <section className="space-y-3" aria-labelledby="ghaneps-events-title">
        <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-end">
          <div>
            <h2 id="ghaneps-events-title" className="font-semibold">
              Exchange event register
            </h2>
            <p className="text-sm text-muted-foreground">
              Every payload version, attempt, acknowledgement, failure, retry,
              and reconciliation remains visible.
            </p>
          </div>
          <Badge variant="outline">{overview.events.length} retained</Badge>
        </div>
        {overview.events.length === 0 ? (
          <EmptyState text="No GHANEPS exchange event has been retained for this source." />
        ) : (
          overview.events.map((event) => (
            <ExchangeEventHistory
              key={event.id}
              event={event}
              canManage={canManage}
              hasConfiguredPermission={hasConfiguredPermission}
              allowedActions={eventActions[event.id] ?? []}
              blockedReasons={eventBlockedReasons[event.id] ?? []}
              onAction={onAction}
            />
          ))
        )}
      </section>

      <ImmutableTimeline history={history} />
    </div>
  );
}

function ProfileLineage({
  options,
}: {
  options: ProcurementGhanepsExchangeOptions;
}) {
  const acknowledgementPermissions = [
    ...new Set(
      options.mappings.map((mapping) => mapping.acknowledgementPermissionCode)
    ),
  ].join(', ');
  const reconciliationPermissions = [
    ...new Set(
      options.mappings.map((mapping) => mapping.reconciliationPermissionCode)
    ),
  ].join(', ');
  return (
    <section
      className="rounded-lg border p-4"
      aria-labelledby="ghaneps-profile-title"
    >
      <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-start">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h2 id="ghaneps-profile-title" className="font-semibold">
              Effective DEC-009 exchange profile
            </h2>
            <Badge>DEC-009</Badge>
            <Badge variant="outline">
              profile v{options.configurationProfileVersion}
            </Badge>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            {options.configurationProfileCode} · {options.exchangeProfileCode}
          </p>
        </div>
        <Badge variant="secondary">{options.frequency}</Badge>
      </div>
      <dl className="mt-4 grid gap-4 text-sm md:grid-cols-2 xl:grid-cols-4">
        <Detail label="Owner" value={options.owner} />
        <Detail
          label="Effective period"
          value={`${formatDate(options.effectiveFromUtc)} → ${formatDate(
            options.effectiveToUtc
          )}`}
        />
        <Detail
          label="Configuration profile"
          value={options.configurationProfileId}
          mono
        />
        <Detail
          label="DEC-009 decision"
          value={options.configurationDecisionId}
          mono
        />
        <Detail
          label="DEC-009 value hash"
          value={shortHash(options.configurationValueHash)}
          mono
        />
        <Detail
          label="Source variant"
          value={options.sourceVariant}
        />
        <Detail
          label="Acknowledgement rule"
          value={options.acknowledgementRule}
          wide
        />
        <Detail
          label="Reconciliation rule"
          value={options.reconciliationRule}
          wide
        />
        <Detail
          label="Configured acknowledgement permission"
          value={acknowledgementPermissions}
          mono
          wide
        />
        <Detail
          label="Configured reconciliation permission"
          value={reconciliationPermissions}
          mono
          wide
        />
      </dl>
    </section>
  );
}

function MappingOptions({
  options,
  canManage,
  serverActions,
  onAction,
}: {
  options: ProcurementGhanepsExchangeOptions;
  canManage: boolean;
  serverActions: string[];
  onAction: (action: GhanepsExchangeAction) => void;
}) {
  return (
    <section
      className="space-y-3 rounded-lg border p-4"
      aria-labelledby="ghaneps-options-title"
    >
      <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-start">
        <div>
          <h2 id="ghaneps-options-title" className="font-semibold">
            Approved DEC-009 mappings
          </h2>
          <p className="text-sm text-muted-foreground">
            Mapping, event family, external code, template, schema, payload
            version, and reference field are server-derived.
          </p>
        </div>
        <Badge variant="outline">{options.mappings.length} mapping(s)</Badge>
      </div>
      <div className="grid gap-3 lg:grid-cols-2">
        {options.mappings.map((mapping) => (
          <MappingCard
            key={mapping.mappingKey}
            mapping={mapping}
            canManage={canManage}
            serverActions={serverActions}
            onAction={onAction}
          />
        ))}
      </div>
    </section>
  );
}

function MappingCard({
  mapping,
  canManage,
  serverActions,
  onAction,
}: {
  mapping: ProcurementGhanepsExchangeMappingOption;
  canManage: boolean;
  serverActions: string[];
  onAction: (action: GhanepsExchangeAction) => void;
}) {
  const canExport =
    canManage &&
    ['Export', 'Bidirectional'].includes(mapping.direction) &&
    hasGhanepsAction(serverActions, 'PrepareExport');
  const canImport =
    canManage &&
    ['Import', 'Bidirectional'].includes(mapping.direction) &&
    hasGhanepsAction(serverActions, 'RecordImport');
  return (
    <div className="rounded-md border bg-muted/20 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="outline">{display(mapping.eventFamily)}</Badge>
        <p className="font-medium">{mapping.mappingKey}</p>
        <Badge variant="secondary">{mapping.direction}</Badge>
      </div>
      <p className="mt-2 text-xs text-muted-foreground">
        {mapping.externalEventCode} · {mapping.templateReference} ·{' '}
        {mapping.schemaReference} · payload {mapping.payloadVersion}
      </p>
      <p className="mt-1 text-xs text-muted-foreground">
        Reference field: {mapping.referenceField} · max retries{' '}
        {mapping.maximumRetryAttempts}
      </p>
      <p className="mt-1 text-xs text-muted-foreground">
        Payload {mapping.payloadContentType} · acknowledgement{' '}
        {mapping.acknowledgementContentType}
      </p>
      <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
        Acknowledgement permission {mapping.acknowledgementPermissionCode} ·
        reconciliation permission {mapping.reconciliationPermissionCode}
      </p>
      <div className="mt-3 flex flex-wrap gap-2">
        {canExport && (
          <Button
            size="sm"
            onClick={() => onAction({ type: 'prepare-export', mapping })}
          >
            <FileOutput className="mr-2 h-4 w-4" />
            Prepare export
          </Button>
        )}
        {canImport && (
          <Button
            size="sm"
            variant="outline"
            onClick={() => onAction({ type: 'record-import', mapping })}
          >
            Record import
          </Button>
        )}
      </div>
    </div>
  );
}

function ExchangeEventHistory({
  event,
  canManage,
  hasConfiguredPermission,
  allowedActions,
  blockedReasons,
  onAction,
}: {
  event: ProcurementGhanepsExchangeEvent;
  canManage: boolean;
  hasConfiguredPermission: (permissionCode: string) => boolean;
  allowedActions: string[];
  blockedReasons: string[];
  onAction: (action: GhanepsExchangeAction) => void;
}) {
  const latestPayload = event.payloads.at(-1);
  const latestAttempt = event.attempts.at(-1);
  const retryPayload =
    event.payloads.find((payload) => payload.id === latestAttempt?.payloadId) ??
    latestPayload;
  const latestAcknowledgement = event.acknowledgements.at(-1);
  const retryAfterRejectedAcknowledgement =
    latestAcknowledgement?.outcome === 'Rejected' &&
    latestAcknowledgement.attemptId === latestAttempt?.id;
  const latestReconciliation = event.reconciliations.at(-1);
  const reconciliationTerminal = ['Matched', 'Resolved'].includes(
    latestReconciliation?.outcome ?? ''
  );
  const resolveExistingMismatch =
    latestReconciliation?.outcome === 'Mismatch' &&
    hasGhanepsAction(allowedActions, 'ResolveReconciliation');
  const canRecordInitialReconciliation =
    latestReconciliation?.outcome !== 'Mismatch' &&
    hasGhanepsAction(allowedActions, 'Reconcile');
  const canAcknowledge = hasConfiguredPermission(
    event.acknowledgementPermissionCode
  );
  const canReconcile = hasConfiguredPermission(
    event.reconciliationPermissionCode
  );
  return (
    <article className="rounded-lg border">
      <div className="flex flex-col justify-between gap-3 border-b bg-muted/20 p-4 lg:flex-row lg:items-start">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline">{display(event.eventFamily)}</Badge>
            <p className="font-medium">{event.eventReference}</p>
            <Badge variant={badgeVariant(event.status)}>
              {display(event.status)}
            </Badge>
            <Badge variant="secondary">{event.direction}</Badge>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            {event.mappingKey} → {event.externalEventCode} · source{' '}
            {event.sourceReference} / {event.sourceVariant}
          </p>
          <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
            Source integrity {event.sourceIntegrityHash}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {canManage &&
            latestPayload &&
            hasGhanepsAction(allowedActions, 'RecordAttempt') && (
              <Button
                size="sm"
                variant="outline"
                onClick={() =>
                  onAction({
                    type: 'record-attempt',
                    event,
                    payload: latestPayload,
                  })
                }
              >
                Record attempt
              </Button>
            )}
          {canManage &&
            retryPayload &&
            latestAttempt &&
            hasGhanepsAction(allowedActions, 'Retry') && (
              <Button
                size="sm"
                variant="outline"
                onClick={() =>
                  onAction({
                    type: 'retry',
                    event,
                    payload: retryPayload,
                    priorAttempt: latestAttempt,
                    rejectedAcknowledgement:
                      retryAfterRejectedAcknowledgement
                        ? latestAcknowledgement
                        : undefined,
                    requiresCorrectedPayload:
                      retryAfterRejectedAcknowledgement,
                  })
                }
              >
                <RefreshCw className="mr-2 h-4 w-4" />
                {retryAfterRejectedAcknowledgement
                  ? 'Retry rejected acknowledgement'
                  : 'Retry failed attempt'}
              </Button>
            )}
          {canAcknowledge &&
            hasGhanepsAction(allowedActions, 'RecordAcknowledgement') && (
              <Button
                size="sm"
                variant="outline"
                onClick={() => onAction({ type: 'acknowledge', event })}
              >
                Record acknowledgement
              </Button>
            )}
          {canReconcile &&
            !reconciliationTerminal &&
            (canRecordInitialReconciliation ||
              resolveExistingMismatch) && (
              <Button
                size="sm"
                onClick={() =>
                  onAction({
                    type: 'reconcile',
                    event,
                    resolveExistingMismatch,
                  })
                }
              >
                {resolveExistingMismatch ? 'Resolve mismatch' : 'Reconcile'}
              </Button>
            )}
        </div>
      </div>
      <div className="space-y-4 p-4">
        {blockedReasons.length > 0 && (
          <BlockedReasons
            title="This exchange event is blocked"
            reasons={blockedReasons}
          />
        )}
        <EventLineage event={event} />
        <div className="grid gap-3 xl:grid-cols-2">
          <HistoryGroup title="Immutable payload versions">
            {event.payloads.length === 0 ? (
              <EmptyLine text="No payload version retained." />
            ) : (
              event.payloads.map((payload) => (
                <PayloadHistory key={payload.id} payload={payload} />
              ))
            )}
          </HistoryGroup>
          <HistoryGroup title="Submission and retry attempts">
            {event.attempts.length === 0 ? (
              <EmptyLine text="No transport attempt retained." />
            ) : (
              event.attempts.map((attempt) => (
                <div key={attempt.id} className="rounded border p-2.5 text-xs">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={badgeVariant(attempt.outcome)}>
                      {attempt.outcome}
                    </Badge>
                    <span className="font-medium">
                      {attempt.isRetry ? 'Retry' : 'Attempt'} #
                      {attempt.attemptNumber}
                    </span>
                    <span className="text-muted-foreground">
                      {formatDate(attempt.attemptedAtUtc)}
                    </span>
                  </div>
                  <p className="mt-1 text-muted-foreground">
                    {attempt.transportReference ?? 'No transport reference'} ·{' '}
                    {attempt.failureCode ?? 'No failure code'} ·{' '}
                    {attempt.attemptedByName} · evidence{' '}
                    {attempt.evidenceReference ?? '—'}
                  </p>
                  {attempt.supersedesAttemptId && (
                    <p className="mt-1 font-mono text-[11px] text-muted-foreground">
                      Supersedes {attempt.supersedesAttemptId}
                    </p>
                  )}
                  {attempt.failureMessage && (
                    <p className="mt-1 text-destructive">
                      {attempt.failureMessage}
                    </p>
                  )}
                  <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                    Request {attempt.requestFingerprint}
                  </p>
                </div>
              ))
            )}
          </HistoryGroup>
          <HistoryGroup title="Acknowledgement history">
            {event.acknowledgements.length === 0 ? (
              <EmptyLine text="No acknowledgement retained." />
            ) : (
              event.acknowledgements.map((acknowledgement) => (
                <div
                  key={acknowledgement.id}
                  className="rounded border p-2.5 text-xs"
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={badgeVariant(acknowledgement.outcome)}>
                      {acknowledgement.outcome}
                    </Badge>
                    <span className="font-medium">
                      {acknowledgement.acknowledgementReference}
                    </span>
                    <span className="text-muted-foreground">
                      acknowledgement #{acknowledgement.sequence}
                    </span>
                  </div>
                  <p className="mt-1 text-muted-foreground">
                    {formatDate(acknowledgement.acknowledgedAtUtc)} ·{' '}
                    {acknowledgement.acknowledgedByName} ·{' '}
                    {acknowledgement.externalStatusCode ??
                      'No external status code'}{' '}
                    · evidence {acknowledgement.evidenceReference}
                  </p>
                  <p className="mt-1 font-mono text-[11px] text-muted-foreground">
                    Attempt {acknowledgement.attemptId} · payload{' '}
                    {acknowledgement.payloadId}
                  </p>
                  <p className="mt-1 break-all font-mono text-[11px]">
                    {acknowledgement.acknowledgementChecksumSha256}
                  </p>
                  <p className="mt-1 text-muted-foreground">
                    Representation {acknowledgement.contentType}
                  </p>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    className="mt-2 h-7 px-2 text-xs"
                    onClick={() =>
                      downloadGhanepsAcknowledgement(acknowledgement)
                    }
                  >
                    <Download className="mr-1.5 h-3.5 w-3.5" />
                    Download{' '}
                    {
                      ghanepsContentTypeMetadata(
                        acknowledgement.contentType
                      ).label
                    }{' '}
                    acknowledgement
                  </Button>
                  <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                    Request {acknowledgement.requestFingerprint}
                  </p>
                </div>
              ))
            )}
          </HistoryGroup>
          <HistoryGroup title="Reconciliation history">
            {event.reconciliations.length === 0 ? (
              <EmptyLine text="No reconciliation decision retained." />
            ) : (
              event.reconciliations.map((item) => (
                <div key={item.id} className="rounded border p-2.5 text-xs">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant={badgeVariant(item.outcome)}>
                      {item.outcome}
                    </Badge>
                    <span className="font-medium">
                      decision #{item.sequence}
                    </span>
                  </div>
                  <p className="mt-1 text-muted-foreground">
                    Expected {item.expectedReference} · actual{' '}
                    {item.actualReference ?? '—'}
                  </p>
                  <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                    Attempt {item.attemptId} · payload {item.payloadId}
                  </p>
                  <p className="mt-1 text-muted-foreground">
                    {formatDate(item.reconciledAtUtc)} ·{' '}
                    {item.reconciledByName} · evidence {item.evidenceReference}
                  </p>
                  <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                    Expected {item.expectedChecksumSha256} · actual{' '}
                    {item.actualChecksumSha256 ?? '—'}
                  </p>
                  <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                    Request {item.requestFingerprint}
                  </p>
                  {item.notes && <p className="mt-1">{item.notes}</p>}
                </div>
              ))
            )}
          </HistoryGroup>
        </div>
      </div>
    </article>
  );
}

function EventLineage({ event }: { event: ProcurementGhanepsExchangeEvent }) {
  return (
    <dl className="grid gap-3 rounded-md border bg-muted/10 p-3 text-xs sm:grid-cols-2 xl:grid-cols-4">
      <Detail
        label="Profile / DEC-009"
        value={`${event.configurationProfileCode} v${event.configurationProfileVersion} / ${event.configurationDecisionId}`}
      />
      <Detail
        label="Exchange profile / effective period"
        value={`${event.exchangeProfileCode} · ${formatDate(
          event.configurationEffectiveFromUtc
        )} → ${formatDate(event.configurationEffectiveToUtc)}`}
        wide
      />
      <Detail
        label="DEC-009 value hash"
        value={event.configurationValueHash}
        mono
        wide
      />
      <Detail
        label="Mapping integrity"
        value={shortHash(event.mappingIntegrityHash)}
        mono
      />
      <Detail
        label="Reference field / owner"
        value={`${event.referenceField} · ${event.owner} · ${event.frequency}`}
        wide
      />
      <Detail
        label="Preparation request fingerprint"
        value={event.requestFingerprint}
        mono
        wide
      />
      <Detail
        label="Prepared"
        value={`${formatDate(event.preparedAtUtc)} · ${event.preparedByName}`}
      />
      <Detail label="Correlation" value={event.correlationId} mono />
      <Detail
        label="Preparation evidence"
        value={event.evidenceReference ?? '—'}
      />
      <Detail
        label="Configured representations"
        value={`Payload ${event.payloadContentType} · acknowledgement ${event.acknowledgementContentType}`}
        wide
      />
      <Detail
        label="Configured acknowledgement permission"
        value={event.acknowledgementPermissionCode}
        mono
        wide
      />
      <Detail
        label="Configured reconciliation permission"
        value={event.reconciliationPermissionCode}
        mono
        wide
      />
      <Detail
        label="Acknowledgement control"
        value={
          event.acknowledgementRequired
            ? `Required · ${event.acknowledgementRule}`
            : 'Not required'
        }
        wide
      />
      <Detail
        label="Reconciliation control"
        value={
          event.reconciliationRequired
            ? `Required · ${event.reconciliationRule}`
            : 'Not required'
        }
        wide
      />
    </dl>
  );
}

function PayloadHistory({
  payload,
}: {
  payload: ProcurementGhanepsExchangePayload;
}) {
  const contentMetadata = ghanepsContentTypeMetadata(payload.contentType);
  return (
    <div className="rounded border p-2.5 text-xs">
      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="outline">{payload.direction}</Badge>
        <span className="font-medium">Payload v{payload.version}</span>
        <span className="text-muted-foreground">
          {payload.fileName ?? `Retained ${contentMetadata.label} content`} ·{' '}
          {payload.contentType}
        </span>
        <Button
          type="button"
          size="sm"
          variant="outline"
          className="ml-auto h-7 px-2 text-xs"
          onClick={() => downloadGhanepsPayload(payload)}
        >
          <Download className="mr-1.5 h-3.5 w-3.5" />
          Download {contentMetadata.label} payload
        </Button>
      </div>
      <p className="mt-1 text-muted-foreground">
        {payload.templateReference} · {payload.schemaReference} · external
        payload {payload.externalPayloadVersion}
      </p>
      <p className="mt-1 text-muted-foreground">
        {formatDate(payload.recordedAtUtc)} · {payload.recordedByName} ·
        evidence {payload.evidenceReference ?? '—'}
      </p>
      <p className="mt-1 break-all font-mono text-[11px]">
        SHA-256 {payload.payloadChecksumSha256}
      </p>
    </div>
  );
}

function ImmutableTimeline({
  history,
}: {
  history: ProcurementGhanepsExchangeHistoryItem[];
}) {
  return (
    <section
      className="rounded-lg border p-4"
      data-testid="ghaneps-exchange-timeline"
      aria-labelledby="ghaneps-history-title"
    >
      <div className="flex items-center justify-between gap-2">
        <div>
          <h2 id="ghaneps-history-title" className="font-semibold">
            Immutable exchange timeline
          </h2>
          <p className="text-sm text-muted-foreground">
            Server-retained history only; the client does not infer source or
            status transitions.
          </p>
        </div>
        <Badge variant="outline">{history.length} entries</Badge>
      </div>
      {history.length === 0 ? (
        <EmptyState text="No immutable GHANEPS history entry has been retained." />
      ) : (
        <ol className="mt-4 space-y-3 border-l pl-5">
          {history.map((item) => (
            <li
              key={`${item.kind}-${item.recordId}-${item.sequence}`}
              className="relative"
            >
              <span className="absolute -left-[25px] top-1.5 h-2 w-2 rounded-full bg-primary" />
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-medium">{display(item.kind)}</p>
                <Badge variant={badgeVariant(item.outcome)}>
                  {display(item.outcome)}
                </Badge>
              </div>
              <p className="mt-1 text-sm text-muted-foreground">
                {formatDate(item.occurredAtUtc)} ·{' '}
                {display(item.eventFamily)} / {item.eventReference} ·{' '}
                {item.reference ?? '—'} · {item.actorName}
              </p>
              <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                {item.integrityHash}
              </p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

function Summary({
  label,
  value,
  detail,
  icon: Icon,
  ok,
  danger,
}: {
  label: string;
  value: number;
  detail?: string;
  icon: typeof FileOutput;
  ok?: boolean;
  danger?: boolean;
}) {
  return (
    <div className="rounded-lg border p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs uppercase tracking-wide text-muted-foreground">
          {label}
        </p>
        <Icon
          className={`h-4 w-4 ${
            danger
              ? 'text-destructive'
              : ok
                ? 'text-emerald-600'
                : 'text-muted-foreground'
          }`}
        />
      </div>
      <p className="mt-2 text-xl font-semibold">{value}</p>
      {detail && (
        <p className="mt-1 text-xs text-muted-foreground">{detail}</p>
      )}
    </div>
  );
}

function Detail({
  label,
  value,
  mono,
  wide,
}: {
  label: string;
  value: string;
  mono?: boolean;
  wide?: boolean;
}) {
  return (
    <div className={wide ? 'sm:col-span-2' : undefined}>
      <dt className="text-muted-foreground">{label}</dt>
      <dd
        className={`mt-1 ${
          mono ? 'break-all font-mono text-[11px]' : 'font-medium'
        }`}
      >
        {value}
      </dd>
    </div>
  );
}

function HistoryGroup({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {title}
      </p>
      {children}
    </div>
  );
}

function BlockedReasons({
  title,
  reasons,
}: {
  title: string;
  reasons: string[];
}) {
  return (
    <Alert className="border-amber-500/40 bg-amber-500/5">
      <AlertTriangle className="h-4 w-4" />
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <ul className="mt-2 list-disc space-y-1 pl-5">
          {reasons.map((reason, index) => (
            <li key={`${reason}-${index}`}>{reason}</li>
          ))}
        </ul>
      </AlertDescription>
    </Alert>
  );
}

function EmptyLine({ text }: { text: string }) {
  return (
    <p className="rounded border border-dashed p-3 text-xs text-muted-foreground">
      {text}
    </p>
  );
}

function EmptyState({ text }: { text: string }) {
  return (
    <div className="mt-3 flex min-h-28 items-center justify-center rounded-md border border-dashed p-5 text-center text-sm text-muted-foreground">
      {text}
    </div>
  );
}
