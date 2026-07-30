import { describe, expect, it, vi } from 'vitest';

import {
  createGhanepsIdempotencyKey,
  downloadGhanepsAcknowledgement,
  downloadGhanepsPayload,
  ghanepsBackHref,
  ghanepsExchangeCounts,
  hasGhanepsAction,
  readGhanepsContentFile,
  ghanepsPayloadDownloadName,
  validateGhanepsContent,
  isGhanepsProfileFailClosed,
} from './procurement-ghaneps-exchange';
import type {
  ProcurementGhanepsExchangeEvent,
  ProcurementGhanepsExchangeOptions,
  ProcurementGhanepsExchangeOverview,
} from '@/types/procurement-ghaneps-exchange';

export const ghanepsOptions = (): ProcurementGhanepsExchangeOptions => ({
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  sourceVariant: 'NCT',
  configurationProfileId: 'profile-1',
  configurationProfileCode: 'PROC-DEFAULT',
  configurationProfileVersion: 3,
  configurationDecisionId: 'decision-9',
  exchangeProfileCode: 'GHANEPS-PHASE-1',
  configurationValueHash: 'a'.repeat(64),
  effectiveFromUtc: '2026-07-01T00:00:00Z',
  frequency: 'PerEvent',
  owner: 'ICT/PPA Desk',
  acknowledgementRule: 'Retain the exact acknowledgement.',
  reconciliationRule: 'Match reference and checksum.',
  allowedActions: ['PrepareExport'],
  blockedReasons: [],
  mappings: [
    {
      mappingKey: 'TENDER-PUB',
      eventFamily: 'TenderPublication',
      direction: 'Export',
      externalEventCode: 'TENDER_PUBLISHED',
      templateReference: 'TENDER-PUB-v1',
      schemaReference: 'GHANEPS-TENDER-v1',
      payloadVersion: '1.0',
      referenceField: 'tenderReference',
      payloadContentType: 'application/json',
      acknowledgementContentType: 'application/json',
      acknowledgementPermissionCode: 'procurement.tender.approve',
      reconciliationPermissionCode: 'procurement.tender.approve',
      acknowledgementRequired: true,
      reconciliationRequired: true,
      maximumRetryAttempts: 3,
    },
  ],
});

export const ghanepsEvent = (): ProcurementGhanepsExchangeEvent => ({
  id: 'event-1',
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  sourceVariant: 'NCT',
  sourceOccurredAtUtc: '2026-07-24T09:00:00Z',
  sourceIntegrityHash: 'b'.repeat(64),
  eventFamily: 'TenderPublication',
  direction: 'Export',
  mappingKey: 'TENDER-PUB',
  externalEventCode: 'TENDER_PUBLISHED',
  eventReference: 'PUB-001',
  referenceField: 'tenderReference',
  payloadContentType: 'application/json',
  acknowledgementContentType: 'application/json',
  acknowledgementPermissionCode: 'procurement.tender.approve',
  reconciliationPermissionCode: 'procurement.tender.approve',
  configurationProfileId: 'profile-1',
  configurationProfileCode: 'PROC-DEFAULT',
  configurationProfileVersion: 3,
  configurationDecisionId: 'decision-9',
  exchangeProfileCode: 'GHANEPS-PHASE-1',
  configurationValueHash: 'a'.repeat(64),
  mappingIntegrityHash: 'c'.repeat(64),
  configurationEffectiveFromUtc: '2026-07-01T00:00:00Z',
  frequency: 'PerEvent',
  owner: 'ICT/PPA Desk',
  acknowledgementRule: 'Retain the exact acknowledgement.',
  reconciliationRule: 'Match reference and checksum.',
  acknowledgementRequired: true,
  reconciliationRequired: true,
  maximumRetryAttempts: 3,
  status: 'ReconciliationException',
  requestFingerprint: '7'.repeat(64),
  correlationId: 'correlation-1',
  preparedByUserId: 'user-1',
  preparedByName: 'Officer',
  preparedAtUtc: '2026-07-24T09:01:00Z',
  evidenceReference: 'EVIDENCE-1',
  integrityHash: 'd'.repeat(64),
  rowVersion: 'AAAA',
  allowedActions: ['RecordAttempt', 'Retry', 'RecordAcknowledgement', 'Reconcile'],
  blockedReasons: [],
  payloads: [
    {
      id: 'payload-1',
      version: 1,
      direction: 'Export',
      templateReference: 'TENDER-PUB-v1',
      schemaReference: 'GHANEPS-TENDER-v1',
      externalPayloadVersion: '1.0',
      contentType: 'application/json',
      fileName: 'TDR-001.json',
      payloadContent: '{}',
      payloadChecksumSha256: 'e'.repeat(64),
      recordedAtUtc: '2026-07-24T09:01:00Z',
      recordedByUserId: 'user-1',
      recordedByName: 'Officer',
      evidenceReference: 'EVIDENCE-1',
      integrityHash: 'f'.repeat(64),
    },
  ],
  attempts: [
    {
      id: 'attempt-1',
      payloadId: 'payload-1',
      attemptNumber: 1,
      isRetry: false,
      outcome: 'Failed',
      failureCode: 'TIMEOUT',
      failureMessage: 'Gateway timeout',
      requestFingerprint: '9'.repeat(64),
      attemptedAtUtc: '2026-07-24T09:06:00Z',
      attemptedByUserId: 'user-1',
      attemptedByName: 'Officer',
      evidenceReference: 'EVIDENCE-2',
      integrityHash: '1'.repeat(64),
    },
  ],
  acknowledgements: [],
  reconciliations: [
    {
      id: 'reconciliation-1',
      attemptId: 'attempt-1',
      payloadId: 'payload-1',
      sequence: 1,
      outcome: 'Mismatch',
      expectedReference: 'PUB-001',
      actualReference: 'PUB-002',
      expectedChecksumSha256: 'e'.repeat(64),
      actualChecksumSha256: '2'.repeat(64),
      requestFingerprint: '8'.repeat(64),
      notes: 'Checksum mismatch',
      reconciledAtUtc: '2026-07-24T10:00:00Z',
      reconciledByUserId: 'user-2',
      reconciledByName: 'Reviewer',
      evidenceReference: 'EVIDENCE-3',
      integrityHash: '3'.repeat(64),
    },
  ],
  history: [],
});

const overview = (): ProcurementGhanepsExchangeOverview => ({
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  sourceVariant: 'NCT',
  events: [ghanepsEvent()],
});

describe('GHANEPS exchange presentation helpers', () => {
  it('derives route-neutral back links and exact action gates', () => {
    expect(ghanepsBackHref('Tender', '1')).toBe('/procurement/tenders/1');
    expect(ghanepsBackHref('RequestForQuotation', '2')).toBe(
      '/procurement/rfqs/2/controls'
    );
    expect(ghanepsBackHref('ExceptionalSourcing', '3')).toBe(
      '/procurement/tenders/3/exception-controls'
    );
    expect(hasGhanepsAction(['PrepareExport'], 'PrepareExport')).toBe(true);
    expect(hasGhanepsAction(['PrepareExport'], 'Retry')).toBe(false);
  });

  it('counts retained payload, attempt, acknowledgement, and reconciliation history', () => {
    expect(ghanepsExchangeCounts(overview())).toEqual({
      events: 1,
      exports: 1,
      failedAttempts: 1,
      acknowledged: 0,
      reconciled: 0,
      mismatched: 1,
    });
  });

  it('fails closed for incomplete DEC-009 lineage', () => {
    expect(isGhanepsProfileFailClosed(ghanepsOptions())).toBe(false);
    const incomplete = ghanepsOptions();
    incomplete.mappings = [];
    expect(isGhanepsProfileFailClosed(incomplete)).toBe(true);
    const unsupported = ghanepsOptions();
    unsupported.mappings[0].payloadContentType =
      'application/octet-stream' as 'application/json';
    expect(isGhanepsProfileFailClosed(unsupported)).toBe(true);
    const permissionless = ghanepsOptions();
    permissionless.mappings[0].acknowledgementPermissionCode = '';
    expect(isGhanepsProfileFailClosed(permissionless)).toBe(true);
  });

  it('generates a fresh bounded idempotency key', () => {
    vi.stubGlobal('crypto', { randomUUID: () => 'uuid-1' });
    expect(createGhanepsIdempotencyKey('export')).toBe(
      'ghaneps-export-uuid-1'
    );
    vi.unstubAllGlobals();
  });

  it('creates a safe local download from the exact retained payload', () => {
    const createObjectURL = vi.fn(() => 'blob:ghaneps-payload');
    const revokeObjectURL = vi.fn();
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    vi.stubGlobal('URL', { createObjectURL, revokeObjectURL });
    const payload = {
      ...ghanepsEvent().payloads[0],
      fileName: '../TDR:001.exe',
      payloadContent: '{"reference":"TDR-001"}',
    };

    expect(ghanepsPayloadDownloadName(payload)).toBe('_TDR_001.json');
    downloadGhanepsPayload(payload);

    expect(createObjectURL).toHaveBeenCalledWith(
      expect.objectContaining({
        size: payload.payloadContent.length,
        type: 'application/json',
      })
    );
    expect(click).toHaveBeenCalledOnce();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:ghaneps-payload');

    click.mockRestore();
    vi.unstubAllGlobals();
  });

  it('validates configured JSON, CSV, XML, and plain-text representations', () => {
    expect(validateGhanepsContent('{"ok":true}', 'application/json')).toBeUndefined();
    expect(validateGhanepsContent('not-json', 'application/json')).toBe(
      'Content must contain valid JSON.'
    );
    expect(validateGhanepsContent('a,b\n1,2', 'text/csv')).toBeUndefined();
    expect(validateGhanepsContent('<root />', 'application/xml')).toBeUndefined();
    expect(validateGhanepsContent('plain text', 'text/plain')).toBeUndefined();
    expect(validateGhanepsContent('unsafe\u0000text', 'text/plain')).toBe(
      'Content contains unsupported control characters.'
    );
  });

  it('downloads retained acknowledgement content with a safe configured extension', () => {
    const createObjectURL = vi.fn(() => 'blob:ghaneps-acknowledgement');
    const revokeObjectURL = vi.fn();
    const click = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    vi.stubGlobal('URL', { createObjectURL, revokeObjectURL });

    downloadGhanepsAcknowledgement({
      acknowledgementContent: '<ack />',
      contentType: 'application/xml',
      sequence: 2,
    });

    expect(createObjectURL).toHaveBeenCalledWith(
      expect.objectContaining({
        size: 7,
        type: 'application/xml',
      })
    );
    expect(click).toHaveBeenCalledOnce();
    expect(revokeObjectURL).toHaveBeenCalledWith(
      'blob:ghaneps-acknowledgement'
    );
    click.mockRestore();
    vi.unstubAllGlobals();
  });

  it('reads only bounded UTF-8 files matching the configured representation', async () => {
    const csv = new File(['a,b\n1,2'], 'exchange.csv', { type: 'text/csv' });
    Object.defineProperty(csv, 'arrayBuffer', {
      value: vi.fn().mockResolvedValue(
        new TextEncoder().encode('a,b\n1,2').buffer
      ),
    });
    await expect(readGhanepsContentFile(csv, 'text/csv')).resolves.toBe(
      'a,b\n1,2'
    );

    await expect(
      readGhanepsContentFile(
        new File(['{}'], 'spoofed.exe'),
        'application/json'
      )
    ).rejects.toThrow('Select a JSON file with a .json extension.');
  });
});
