'use client';

import React from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  FileCheck2,
  History,
  MailCheck,
  Scale,
  ShieldCheck,
} from 'lucide-react';

import type { BidderCommunicationAction } from './BidderCommunicationActionDialog';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  bidderCommunicationOutcomeCounts,
  bidderCommunicationStandstillLabel,
  hasBidderCommunicationAction,
} from '@/lib/procurement-bidder-communication';
import type {
  ProcurementBidderCommunicationOverview,
  ProcurementBidderCommunicationRecipient,
  ProcurementBidderLetterDispatch,
  ProcurementBidderLetterVersion,
  ProcurementTenderSecurityInstrument,
} from '@/types/procurement-bidder-communication';

const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '—';

const shortId = (value?: string) =>
  value
    ? value.length > 18
      ? `${value.slice(0, 8)}…${value.slice(-6)}`
      : value
    : '—';

const display = (value?: string) =>
  value
    ? value
        .replace(/([a-z])([A-Z])/g, '$1 $2')
        .replace(/[_-]+/g, ' ')
    : '—';

const statusVariant = (
  value?: string
): 'default' | 'destructive' | 'outline' | 'secondary' => {
  if (
    [
      'Successful',
      'Approved',
      'Delivered',
      'Received',
      'Accepted',
      'Upheld',
      'Released',
      'Returned',
      'Eligible',
    ].includes(value ?? '')
  )
    return 'default';
  if (
    [
      'Unsuccessful',
      'Failed',
      'Disputed',
      'Dismissed',
      'Forfeited',
      'Blocked',
    ].includes(value ?? '')
  )
    return 'destructive';
  return 'secondary';
};

export function BidderCommunicationRegister({
  overview,
  external,
  canManage,
  canApprove,
  onAction,
}: {
  overview: ProcurementBidderCommunicationOverview;
  external: boolean;
  canManage: boolean;
  canApprove: boolean;
  onAction: (action: BidderCommunicationAction) => void;
}) {
  const counts = bidderCommunicationOutcomeCounts(overview);
  const externalScopeInvalid = external && overview.recipients.length !== 1;
  const recipients = externalScopeInvalid ? [] : overview.recipients;

  if (externalScopeInvalid)
    return (
      <div
        data-testid="external-bidder-communication-status"
        className="space-y-5"
      >
        <Alert className="border-destructive/50 bg-destructive/5">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Supplier scope could not be verified</AlertTitle>
          <AlertDescription>
            The external API must return exactly one recipient for the
            authenticated supplier. No communication, appeal, security, count,
            or history data is rendered until that ownership boundary is
            restored.
          </AlertDescription>
        </Alert>
      </div>
    );

  return (
    <div
      className="space-y-5"
      data-testid={
        external
          ? 'external-bidder-communication-status'
          : 'bidder-communication-register'
      }
    >
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Summary
          label="Recipient result"
          value={`${counts.successful} / ${counts.unsuccessful}`}
          detail="Successful / unsuccessful"
          ok={counts.successful > 0}
        />
        <Summary
          label="Approved letters"
          value={String(counts.approvedLetters)}
          detail={`${counts.dispatches} retained dispatches`}
          ok={
            counts.approvedLetters >= overview.recipients.length &&
            overview.recipients.length > 0
          }
        />
        <Summary
          label="Standstill"
          value={bidderCommunicationStandstillLabel(overview)}
          detail={`${formatDate(overview.standstillStartsAtUtc)} → ${formatDate(
            overview.standstillEndsAtUtc
          )}`}
          ok={overview.standstillElapsed}
        />
        <Summary
          label="Appeal window"
          value={overview.appealWindowOpen ? 'Open' : 'Closed'}
          detail={`${formatDate(overview.appealWindowEndsAtUtc)} · ${
            overview.hasOpenAppeals ? 'open appeal retained' : 'no open appeal'
          }`}
          ok={!overview.hasOpenAppeals}
        />
      </div>

      {overview.blockedReasons.length > 0 && (
        <Alert className="border-amber-500/40 bg-amber-500/5">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>Communication or security actions are blocked</AlertTitle>
          <AlertDescription>
            <ul className="mt-2 list-disc space-y-1 pl-5">
              {overview.blockedReasons.map((reason, index) => (
                <li key={`${reason}-${index}`}>{reason}</li>
              ))}
            </ul>
          </AlertDescription>
        </Alert>
      )}

      {recipients.length === 0 ? (
        <Alert className="border-destructive/50 bg-destructive/5">
          <AlertTriangle className="h-4 w-4" />
          <AlertTitle>No server-derived recipient is available</AlertTitle>
          <AlertDescription>
            Access remains fail closed until award and supplier lineage resolve
            an exact recipient.
          </AlertDescription>
        </Alert>
      ) : (
        <section className="space-y-3" aria-labelledby="recipient-register-title">
          <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-end">
            <div>
              <h2 id="recipient-register-title" className="font-semibold">
                {external
                  ? 'My award communication and security status'
                  : 'Server-derived recipient register'}
              </h2>
              <p className="text-sm text-muted-foreground">
                Immutable approved versions, dispatch, delivery,
                acknowledgement, appeal, and tender-security history.
              </p>
            </div>
            <Badge variant="outline">
              {recipients.length} recipient{recipients.length === 1 ? '' : 's'}
            </Badge>
          </div>
          {recipients.map((recipient) => (
            <RecipientHistory
              key={recipient.id}
              recipient={recipient}
              overview={overview}
              external={external}
              canManage={canManage}
              canApprove={canApprove}
              onAction={onAction}
            />
          ))}
        </section>
      )}

      <SourceHistory overview={overview} />
    </div>
  );
}

function RecipientHistory({
  recipient,
  overview,
  external,
  canManage,
  canApprove,
  onAction,
}: {
  recipient: ProcurementBidderCommunicationRecipient;
  overview: ProcurementBidderCommunicationOverview;
  external: boolean;
  canManage: boolean;
  canApprove: boolean;
  onAction: (action: BidderCommunicationAction) => void;
}) {
  const canApproveLetter =
    !external &&
    canApprove &&
    hasBidderCommunicationAction(overview.allowedActions, 'ApproveLetter');
  const canFileAppeal =
    external &&
    hasBidderCommunicationAction(recipient.allowedActions, 'FileAppeal');
  const canRegisterSecurity =
    !external &&
    canManage &&
    hasBidderCommunicationAction(overview.allowedActions, 'RegisterSecurity');

  return (
    <article
      className="overflow-hidden rounded-lg border"
      data-testid={`bidder-recipient-${recipient.id}`}
    >
      <div className="flex flex-col justify-between gap-3 border-b bg-muted/20 p-4 lg:flex-row lg:items-start">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-semibold">{recipient.partnerName}</h3>
            <Badge variant="outline">{recipient.partnerCode}</Badge>
            <Badge variant={statusVariant(recipient.outcome)}>
              {recipient.outcome}
            </Badge>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            {recipient.bidOrQuoteIds.length} bid or quote record
            {recipient.bidOrQuoteIds.length === 1 ? '' : 's'} · supplier{' '}
            {shortId(recipient.businessPartnerId)}
          </p>
          <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
            lineage {recipient.lineageHash} · integrity {recipient.integrityHash}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {canApproveLetter && (
            <Button
              size="sm"
              variant="outline"
              onClick={() =>
                onAction({ type: 'approve-letter', recipient })
              }
            >
              Approve letter version
            </Button>
          )}
          {canFileAppeal && (
            <Button
              size="sm"
              variant="outline"
              onClick={() => onAction({ type: 'file-appeal', recipient })}
            >
              File appeal
            </Button>
          )}
          {canRegisterSecurity && (
            <Button
              size="sm"
              variant="outline"
              onClick={() =>
                onAction({ type: 'register-security', recipient })
              }
            >
              Register security
            </Button>
          )}
        </div>
      </div>

      <div className="grid divide-y xl:grid-cols-2 xl:divide-x xl:divide-y-0">
        <div className="p-4">
          <SectionTitle
            icon={FileCheck2}
            title="Approved letter versions and dispatch"
            detail={`${recipient.letterVersions.length} approved · ${recipient.letterVersions
              .flatMap((letter) => letter.dispatches)
              .length} dispatches`}
          />
          <LetterHistory
            recipient={recipient}
            external={external}
            canManage={canManage}
            onAction={onAction}
          />
        </div>

        <div className="grid divide-y">
          <div className="p-4">
            <SectionTitle
              icon={Scale}
              title="Appeal history"
              detail={`${recipient.appeals.length} retained appeal${
                recipient.appeals.length === 1 ? '' : 's'
              }`}
            />
            {recipient.appeals.length === 0 ? (
              <EmptyLine text="No appeal has been filed." />
            ) : (
              <div className="mt-3 space-y-2">
                {recipient.appeals.map((appeal) => (
                  <div
                    key={appeal.id}
                    className="flex flex-col justify-between gap-2 rounded-md border p-3 sm:flex-row sm:items-start"
                  >
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-medium">Appeal #{appeal.sequence}</p>
                        <Badge
                          variant={
                            appeal.decision
                              ? statusVariant(appeal.decision.outcome)
                              : 'secondary'
                          }
                        >
                          {appeal.decision
                            ? display(appeal.decision.outcome)
                            : 'Pending'}
                        </Badge>
                      </div>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {appeal.grounds}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        Filed {formatDate(appeal.filedAtUtc)} · evidence{' '}
                        {appeal.evidenceReference}
                      </p>
                      {appeal.decision && (
                        <p className="mt-1 text-xs text-muted-foreground">
                          Decision {appeal.decision.decisionReference} ·{' '}
                          {appeal.decision.reason} ·{' '}
                          {formatDate(appeal.decision.decidedAtUtc)}
                        </p>
                      )}
                    </div>
                    {!external && canApprove && !appeal.decision && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() =>
                          onAction({ type: 'resolve-appeal', appeal })
                        }
                      >
                        Resolve
                      </Button>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="p-4">
            <SectionTitle
              icon={ShieldCheck}
              title="Tender security release and return"
              detail={`${recipient.securityInstruments.length} instrument${
                recipient.securityInstruments.length === 1 ? '' : 's'
              }`}
            />
            <SecurityHistory
              recipient={recipient}
              external={external}
              canApprove={canApprove}
              onAction={onAction}
            />
          </div>
        </div>
      </div>
    </article>
  );
}

function LetterHistory({
  recipient,
  external,
  canManage,
  onAction,
}: {
  recipient: ProcurementBidderCommunicationRecipient;
  external: boolean;
  canManage: boolean;
  onAction: (action: BidderCommunicationAction) => void;
}) {
  if (recipient.letterVersions.length === 0)
    return <EmptyLine text="No approved letter version has been retained." />;
  return (
    <div className="mt-3 space-y-3">
      {recipient.letterVersions.map((letter) => (
        <div key={letter.id} className="rounded-md border">
          <div className="grid gap-2 border-b p-3 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-start">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-medium">Version {letter.version}</p>
                <Badge variant="default">Approved</Badge>
                <Badge variant="outline">{letter.templateReference}</Badge>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {letter.contentReference} · approved{' '}
                {formatDate(letter.approvedAtUtc)} by {letter.approvedByName}
              </p>
              <p className="mt-1 break-all font-mono text-[11px] text-muted-foreground">
                template {letter.templateChecksumSha256} · content{' '}
                {letter.contentChecksumSha256}
              </p>
            </div>
            {!external && canManage && (
              <Button
                size="sm"
                variant="outline"
                className="sm:self-start"
                onClick={() =>
                  onAction({ type: 'dispatch', recipient, letter })
                }
              >
                Dispatch exact version
              </Button>
            )}
          </div>
          {letter.dispatches.length === 0 ? (
            <p className="p-3 text-sm text-muted-foreground">
              This approved version has not been dispatched.
            </p>
          ) : (
            <div className="divide-y">
              {letter.dispatches.map((dispatch) => (
                <DispatchHistory
                  key={dispatch.id}
                  dispatch={dispatch}
                  recipient={recipient}
                  external={external}
                  canManage={canManage}
                  onAction={onAction}
                />
              ))}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function DispatchHistory({
  dispatch,
  recipient,
  external,
  canManage,
  onAction,
}: {
  dispatch: ProcurementBidderLetterDispatch;
  recipient: ProcurementBidderCommunicationRecipient;
  external: boolean;
  canManage: boolean;
  onAction: (action: BidderCommunicationAction) => void;
}) {
  const canAcknowledge =
    external &&
    hasBidderCommunicationAction(recipient.allowedActions, 'Acknowledge');
  return (
    <div className="p-3">
      <div className="flex flex-col justify-between gap-2 sm:flex-row sm:items-start">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant="outline">{display(dispatch.channel)}</Badge>
            <p className="font-medium">{dispatch.dispatchReference}</p>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            {dispatch.destination} · {formatDate(dispatch.dispatchedAtUtc)} ·{' '}
            {dispatch.dispatchedByName} · evidence{' '}
            {dispatch.dispatchEvidenceReference}
          </p>
          <p className="mt-2 text-xs text-muted-foreground">
            Delivery:{' '}
            {dispatch.deliveries.length
              ? dispatch.deliveries
                  .map(
                    (item) =>
                      `${display(item.outcome)} ${formatDate(item.occurredAtUtc)}`
                  )
                  .join(' · ')
              : 'Pending'}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            Acknowledgement:{' '}
            {dispatch.acknowledgements.length
              ? dispatch.acknowledgements
                  .map(
                    (item) =>
                      `${display(item.outcome)} ${item.acknowledgementReference}`
                  )
                  .join(' · ')
              : 'Pending'}
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {!external && canManage && (
            <Button
              size="sm"
              variant="outline"
              onClick={() => onAction({ type: 'delivery', dispatch })}
            >
              Record delivery
            </Button>
          )}
          {canAcknowledge && (
            <Button
              size="sm"
              variant="outline"
              onClick={() => onAction({ type: 'acknowledge', dispatch })}
            >
              Acknowledge
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

function SecurityHistory({
  recipient,
  external,
  canApprove,
  onAction,
}: {
  recipient: ProcurementBidderCommunicationRecipient;
  external: boolean;
  canApprove: boolean;
  onAction: (action: BidderCommunicationAction) => void;
}) {
  if (recipient.securityInstruments.length === 0)
    return <EmptyLine text="No tender security instrument is registered." />;
  return (
    <div className="mt-3 space-y-3">
      {recipient.securityInstruments.map((security) => (
        <div key={security.id} className="rounded-md border p-3">
          <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-medium">
                  {display(security.instrumentType)}
                </p>
                <Badge variant="outline">{security.instrumentReference}</Badge>
                <Badge
                  variant={
                    security.currentAction
                      ? statusVariant(security.currentAction)
                      : 'secondary'
                  }
                >
                  {security.currentAction
                    ? display(security.currentAction)
                    : 'Held'}
                </Badge>
              </div>
              <p className="mt-1 text-xs text-muted-foreground">
                {security.issuerName} · {security.currencyCode}{' '}
                {security.amount.toLocaleString()} · expires{' '}
                {formatDate(security.expiresAtUtc)}
              </p>
              {security.blockedReasons.length > 0 && (
                <p className="mt-2 text-xs text-amber-700">
                  {security.blockedReasons.join(' ')}
                </p>
              )}
            </div>
            {!external && canApprove && !security.currentAction && (
              <SecurityActions
                security={security}
                onAction={onAction}
              />
            )}
          </div>
          {security.actions.length > 0 && (
            <div className="mt-3 divide-y rounded-md bg-muted/30 px-3">
              {security.actions.map((action) => (
                <div
                  key={action.id}
                  className="flex flex-col justify-between gap-1 py-2 text-xs sm:flex-row"
                >
                  <span>
                    {display(action.actionType)} · {action.actionReference} ·{' '}
                    {action.reason}
                  </span>
                  <span className="text-muted-foreground">
                    {action.actionedByName} · {formatDate(action.actionedAtUtc)}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function SecurityActions({
  security,
  onAction,
}: {
  security: ProcurementTenderSecurityInstrument;
  onAction: (action: BidderCommunicationAction) => void;
}) {
  const mappings = [
    ['Release', 'Released'],
    ['Return', 'Returned'],
    ['Forfeit', 'Forfeited'],
  ] as const;
  return (
    <div className="flex flex-wrap gap-2">
      {mappings.map(([allowedAction, actionType]) =>
        hasBidderCommunicationAction(
          security.allowedActions,
          allowedAction
        ) ? (
          <Button
            key={allowedAction}
            size="sm"
            variant="outline"
            onClick={() =>
              onAction({
                type: 'security-action',
                security,
                securityAction: actionType,
              })
            }
          >
            {allowedAction}
          </Button>
        ) : null
      )}
    </div>
  );
}

function SourceHistory({
  overview,
}: {
  overview: ProcurementBidderCommunicationOverview;
}) {
  const events = overview.recipients.flatMap((recipient) => [
    ...recipient.letterVersions.flatMap((letter) => [
      {
        key: `letter-${letter.id}`,
        at: letter.approvedAtUtc,
        title: `Letter version ${letter.version} approved`,
        detail: `${recipient.partnerName} · ${letter.approvalReference} · ${letter.approvedByName}`,
        hash: letter.integrityHash,
      },
      ...letter.dispatches.flatMap((dispatch) => [
        {
          key: `dispatch-${dispatch.id}`,
          at: dispatch.dispatchedAtUtc,
          title: `Letter dispatched by ${display(dispatch.channel)}`,
          detail: `${recipient.partnerName} · ${dispatch.dispatchReference} · ${dispatch.dispatchedByName}`,
          hash: dispatch.integrityHash,
        },
        ...dispatch.deliveries.map((delivery) => ({
          key: `delivery-${delivery.id}`,
          at: delivery.occurredAtUtc,
          title: `Delivery ${display(delivery.outcome)}`,
          detail: `${recipient.partnerName} · ${delivery.providerReference}`,
          hash: delivery.integrityHash,
        })),
        ...dispatch.acknowledgements.map((acknowledgement) => ({
          key: `ack-${acknowledgement.id}`,
          at: acknowledgement.acknowledgedAtUtc,
          title: `Acknowledgement ${display(acknowledgement.outcome)}`,
          detail: `${recipient.partnerName} · ${acknowledgement.acknowledgementReference}`,
          hash: acknowledgement.integrityHash,
        })),
      ]),
    ]),
    ...recipient.appeals.flatMap((appeal) => [
      {
        key: `appeal-${appeal.id}`,
        at: appeal.filedAtUtc,
        title: 'Appeal filed',
        detail: `${recipient.partnerName} · appeal #${appeal.sequence}`,
        hash: appeal.integrityHash,
      },
      ...(appeal.decision
        ? [
            {
              key: `appeal-decision-${appeal.decision.id}`,
              at: appeal.decision.decidedAtUtc,
              title: `Appeal ${display(appeal.decision.outcome)}`,
              detail: `${recipient.partnerName} · ${appeal.decision.decisionReference}`,
              hash: appeal.decision.integrityHash,
            },
          ]
        : []),
    ]),
    ...recipient.securityInstruments.flatMap((security) => [
      {
        key: `security-${security.id}`,
        at: security.registeredAtUtc,
        title: 'Tender security registered',
        detail: `${recipient.partnerName} · ${security.instrumentReference}`,
        hash: security.integrityHash,
      },
      ...security.actions.map((action) => ({
        key: `security-action-${action.id}`,
        at: action.actionedAtUtc,
        title: `Tender security ${display(action.actionType)}`,
        detail: `${recipient.partnerName} · ${action.actionReference} · ${action.actionedByName}`,
        hash: action.integrityHash,
      })),
    ]),
  ]);
  return (
    <section
      className="rounded-lg border"
      data-testid="bidder-communication-timeline"
    >
      <div className="flex items-center gap-2 border-b bg-muted/30 px-4 py-3">
        <History className="h-4 w-4" />
        <h2 className="font-semibold">Immutable communication timeline</h2>
      </div>
      {events.length === 0 ? (
        <p className="p-4 text-sm text-muted-foreground">
          No communication or security events have been retained.
        </p>
      ) : (
        <div className="divide-y">
          {events
            .sort(
              (a, b) =>
                new Date(b.at).getTime() - new Date(a.at).getTime()
            )
            .map((event) => (
              <div
                key={event.key}
                className="grid gap-2 px-4 py-3 md:grid-cols-[minmax(0,1fr)_auto]"
              >
                <div>
                  <p className="font-medium">{event.title}</p>
                  <p className="text-sm text-muted-foreground">
                    {event.detail}
                  </p>
                </div>
                <div className="text-left text-xs text-muted-foreground md:text-right">
                  <p>{formatDate(event.at)}</p>
                  <p className="font-mono">{shortId(event.hash)}</p>
                </div>
              </div>
            ))}
        </div>
      )}
      <p className="break-all border-t px-4 py-3 font-mono text-[11px] text-muted-foreground">
        Register {overview.id} · award family {overview.awardFamily} · award{' '}
        {overview.awardReference} · readiness #
        {overview.awardReadinessDecisionSequence} · source hash{' '}
        {overview.awardReadinessSourceIntegrityHash} · recipient snapshot{' '}
        {overview.recipientSnapshotHash} · register hash {overview.integrityHash}
      </p>
    </section>
  );
}

function Summary({
  label,
  value,
  detail,
  ok,
}: {
  label: string;
  value: string;
  detail: string;
  ok: boolean;
}) {
  return (
    <div className="rounded-lg border p-4">
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs uppercase tracking-wide text-muted-foreground">
          {label}
        </p>
        {ok ? (
          <CheckCircle2 className="h-4 w-4 text-emerald-600" />
        ) : (
          <AlertTriangle className="h-4 w-4 text-amber-600" />
        )}
      </div>
      <p className="mt-2 text-xl font-semibold">{value}</p>
      <p className="mt-1 text-xs text-muted-foreground">{detail}</p>
    </div>
  );
}

function SectionTitle({
  icon: Icon,
  title,
  detail,
}: {
  icon: typeof MailCheck;
  title: string;
  detail: string;
}) {
  return (
    <div className="flex items-start gap-2">
      <Icon className="mt-0.5 h-4 w-4 text-muted-foreground" />
      <div>
        <h4 className="font-medium">{title}</h4>
        <p className="text-xs text-muted-foreground">{detail}</p>
      </div>
    </div>
  );
}

function EmptyLine({ text }: { text: string }) {
  return (
    <p className="mt-3 rounded-md border border-dashed p-3 text-sm text-muted-foreground">
      {text}
    </p>
  );
}
