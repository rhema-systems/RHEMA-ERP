import type {
  ProcurementBidderCommunicationOverview,
  ProcurementBidderCommunicationSourceType,
} from '@/types/procurement-bidder-communication';

const normalize = (value: string) =>
  value.replace(/[^a-z0-9]/gi, '').toLowerCase();

export const hasBidderCommunicationAction = (
  actions: string[] | undefined,
  ...expected: string[]
) =>
  expected.some((candidate) =>
    actions?.some((action) => normalize(action) === normalize(candidate))
  );

export const createBidderCommunicationIdempotencyKey = (action: string) =>
  `tdc0211-${normalize(action)}-${crypto.randomUUID()}`;

export const bidderCommunicationSourceLabel = (
  sourceType: ProcurementBidderCommunicationSourceType
) => {
  if (sourceType === 'RequestForQuotation') return 'Request for quotation';
  if (sourceType === 'ExceptionalSourcing') return 'Exceptional sourcing';
  return 'Tender';
};

export const bidderCommunicationInternalBackHref = (
  sourceType: ProcurementBidderCommunicationSourceType,
  sourceId: string
) => {
  if (sourceType === 'RequestForQuotation')
    return `/procurement/rfqs/${sourceId}/award-readiness`;
  if (sourceType === 'ExceptionalSourcing')
    return `/procurement/tenders/${sourceId}/award-readiness?sourceType=ExceptionalSourcing`;
  return `/procurement/tenders/${sourceId}/award-readiness`;
};

export const bidderCommunicationExternalBackHref = (
  sourceType: ProcurementBidderCommunicationSourceType,
  sourceId: string
) =>
  sourceType === 'RequestForQuotation'
    ? `/external-portal/rfqs/${sourceId}`
    : `/external-portal/tenders/${sourceId}`;

export const bidderCommunicationStandstillLabel = (
  overview: ProcurementBidderCommunicationOverview,
  now = Date.now()
) => {
  if (overview.standstillElapsed) return 'Elapsed';
  const days = Math.max(
    Math.ceil(
      (new Date(overview.standstillEndsAtUtc).getTime() - now) /
        86_400_000
    ),
    0
  );
  return `${days} day${days === 1 ? '' : 's'} remaining`;
};

export const bidderCommunicationOutcomeCounts = (
  overview: ProcurementBidderCommunicationOverview
) => ({
  successful: overview.recipients.filter(
    (recipient) => recipient.outcome === 'Successful'
  ).length,
  unsuccessful: overview.recipients.filter(
    (recipient) => recipient.outcome === 'Unsuccessful'
  ).length,
  approvedLetters: overview.recipients.flatMap(
    (recipient) => recipient.letterVersions
  ).length,
  dispatches: overview.recipients
    .flatMap((recipient) => recipient.letterVersions)
    .flatMap((letter) => letter.dispatches).length,
});
