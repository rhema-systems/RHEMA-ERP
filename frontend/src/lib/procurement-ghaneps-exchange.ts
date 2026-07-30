import type {
  ProcurementGhanepsContentType,
  ProcurementGhanepsExchangeAcknowledgement,
  ProcurementGhanepsExchangePayload,
  ProcurementGhanepsExchangeOptions,
  ProcurementGhanepsExchangeOverview,
  ProcurementGhanepsSourceType,
} from '@/types/procurement-ghaneps-exchange';

export const GHANEPS_MAX_CONTENT_BYTES = 5_000_000;

const CONTENT_TYPE_METADATA: Record<
  ProcurementGhanepsContentType,
  { label: string; extension: string; accept: string }
> = {
  'application/json': {
    label: 'JSON',
    extension: '.json',
    accept: '.json,application/json',
  },
  'text/csv': {
    label: 'CSV',
    extension: '.csv',
    accept: '.csv,text/csv,application/csv',
  },
  'application/csv': {
    label: 'CSV',
    extension: '.csv',
    accept: '.csv,text/csv,application/csv',
  },
  'application/xml': {
    label: 'XML',
    extension: '.xml',
    accept: '.xml,application/xml,text/xml',
  },
  'text/xml': {
    label: 'XML',
    extension: '.xml',
    accept: '.xml,application/xml,text/xml',
  },
  'text/plain': {
    label: 'text',
    extension: '.txt',
    accept: '.txt,text/plain',
  },
};

export const isSupportedGhanepsContentType = (
  value?: string
): value is ProcurementGhanepsContentType =>
  Boolean(value && value in CONTENT_TYPE_METADATA);

export const ghanepsContentTypeMetadata = (
  contentType: ProcurementGhanepsContentType
) => CONTENT_TYPE_METADATA[contentType];

export const ghanepsSourceLabel = (sourceType: ProcurementGhanepsSourceType) =>
  sourceType === 'RequestForQuotation'
    ? 'Request for quotation'
    : sourceType === 'ExceptionalSourcing'
      ? 'Exceptional sourcing'
      : 'Tender';

export const ghanepsBackHref = (
  sourceType: ProcurementGhanepsSourceType,
  sourceId: string
) =>
  sourceType === 'RequestForQuotation'
    ? `/procurement/rfqs/${sourceId}/controls`
    : sourceType === 'ExceptionalSourcing'
      ? `/procurement/tenders/${sourceId}/exception-controls`
      : `/procurement/tenders/${sourceId}`;

export const hasGhanepsAction = (
  actions: readonly string[] | undefined,
  action: string
) => Boolean(actions?.some((candidate) => candidate === action));

export const createGhanepsIdempotencyKey = (action: string) =>
  `ghaneps-${action}-${crypto.randomUUID()}`;

export const ghanepsPayloadDownloadName = (
  payload: Pick<
    ProcurementGhanepsExchangePayload,
    'contentType' | 'fileName' | 'version'
  >
) => {
  const { extension } = ghanepsContentTypeMetadata(payload.contentType);
  const fallback = `ghaneps-payload-v${payload.version}${extension}`;
  let sanitized = (payload.fileName?.trim() || fallback)
    .replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_')
    .replace(/^\.+/, '')
    .trim();
  if (sanitized && !sanitized.toLowerCase().endsWith(extension))
    sanitized = `${sanitized.replace(/\.[^.]*$/, '')}${extension}`;
  return sanitized || fallback;
};

export const downloadGhanepsPayload = (
  payload: Pick<
    ProcurementGhanepsExchangePayload,
    'contentType' | 'fileName' | 'payloadContent' | 'version'
  >
) => {
  const blob = new Blob([payload.payloadContent], {
    type: payload.contentType,
  });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = ghanepsPayloadDownloadName(payload);
  anchor.rel = 'noopener';
  document.body.appendChild(anchor);
  try {
    anchor.click();
  } finally {
    anchor.remove();
    URL.revokeObjectURL(url);
  }
};

export const downloadGhanepsAcknowledgement = (
  acknowledgement: Pick<
    ProcurementGhanepsExchangeAcknowledgement,
    'acknowledgementContent' | 'contentType' | 'sequence'
  >
) => {
  const { extension } = ghanepsContentTypeMetadata(
    acknowledgement.contentType
  );
  const blob = new Blob([acknowledgement.acknowledgementContent], {
    type: acknowledgement.contentType,
  });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `ghaneps-acknowledgement-${acknowledgement.sequence}${extension}`;
  anchor.rel = 'noopener';
  document.body.appendChild(anchor);
  try {
    anchor.click();
  } finally {
    anchor.remove();
    URL.revokeObjectURL(url);
  }
};

export const validateGhanepsContent = (
  content: string,
  contentType: ProcurementGhanepsContentType,
  subject = 'Content'
) => {
  if (!content.trim()) return `${subject} is required.`;
  if (new TextEncoder().encode(content).byteLength > GHANEPS_MAX_CONTENT_BYTES)
    return `${subject} cannot exceed 5,000,000 UTF-8 bytes.`;

  if (contentType === 'application/json') {
    try {
      JSON.parse(content);
    } catch {
      return `${subject} must contain valid JSON.`;
    }
    return undefined;
  }

  if (/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/.test(content))
    return `${subject} contains unsupported control characters.`;

  if (contentType === 'application/xml' || contentType === 'text/xml') {
    if (/<!DOCTYPE/i.test(content))
      return `${subject} must not contain an XML document type declaration.`;
    const parsed = new DOMParser().parseFromString(content, 'application/xml');
    if (parsed.querySelector('parsererror'))
      return `${subject} must contain well-formed XML.`;
  }

  return undefined;
};

export const readGhanepsContentFile = async (
  file: File,
  contentType: ProcurementGhanepsContentType
) => {
  const metadata = ghanepsContentTypeMetadata(contentType);
  if (!file.name.toLowerCase().endsWith(metadata.extension))
    throw new Error(
      `Select a ${metadata.label} file with a ${metadata.extension} extension.`
    );
  if (file.size > GHANEPS_MAX_CONTENT_BYTES)
    throw new Error(
      `${metadata.label} files must not exceed 5,000,000 UTF-8 bytes.`
    );

  let content: string;
  try {
    content = new TextDecoder('utf-8', { fatal: true }).decode(
      await file.arrayBuffer()
    );
  } catch {
    throw new Error(
      `The selected ${metadata.label} file must be valid UTF-8 text.`
    );
  }

  const validation = validateGhanepsContent(
    content,
    contentType,
    `The selected ${metadata.label} file`
  );
  if (validation) throw new Error(validation);
  return content;
};

export const ghanepsExchangeCounts = (
  overview: ProcurementGhanepsExchangeOverview
) => {
  const exports = overview.events.flatMap((event) =>
    event.payloads.filter((payload) => payload.direction === 'Export')
  );
  const attempts = overview.events.flatMap((event) => event.attempts);
  const acknowledgements = overview.events.flatMap(
    (event) => event.acknowledgements
  );
  const reconciliations = overview.events.flatMap(
    (event) => event.reconciliations
  );
  return {
    events: overview.events.length,
    exports: exports.length,
    failedAttempts: attempts.filter((item) => item.outcome === 'Failed').length,
    acknowledged: acknowledgements.filter(
      (item) => item.outcome === 'Accepted'
    ).length,
    reconciled: reconciliations.filter((item) =>
      ['Matched', 'Resolved'].includes(item.outcome)
    ).length,
    mismatched: reconciliations.filter((item) => item.outcome === 'Mismatch')
      .length,
  };
};

export const isGhanepsProfileFailClosed = (
  options?: ProcurementGhanepsExchangeOptions
) =>
  !options ||
  !options.configurationProfileId ||
  !options.configurationDecisionId ||
  !options.configurationValueHash ||
  !options.exchangeProfileCode ||
  options.mappings.length === 0 ||
  options.mappings.some(
    (mapping) =>
      !isSupportedGhanepsContentType(mapping.payloadContentType) ||
      !isSupportedGhanepsContentType(mapping.acknowledgementContentType) ||
      !mapping.acknowledgementPermissionCode?.trim() ||
      !mapping.reconciliationPermissionCode?.trim()
  );
