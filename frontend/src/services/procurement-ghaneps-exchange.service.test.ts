import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { procurementGhanepsExchangeService as service } from './procurement-ghaneps-exchange.service';

describe('procurement GHANEPS exchange API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses role-neutral source-scoped read paths', async () => {
    await service.getOverview('Tender', 'tender-1');
    await service.getOptions('RequestForQuotation', 'rfq-1');
    await service.getStatus('ExceptionalSourcing', 'case-1');
    await service.getHistory('Tender', 'tender-1');
    await service.getEvent('Tender', 'tender-1', 'event-1');

    expect(api.get.mock.calls.map(([path]) => path)).toEqual([
      '/procurement/ghaneps-exchanges/Tender/tender-1',
      '/procurement/ghaneps-exchanges/RequestForQuotation/rfq-1/options',
      '/procurement/ghaneps-exchanges/ExceptionalSourcing/case-1/status',
      '/procurement/ghaneps-exchanges/Tender/tender-1/history',
      '/procurement/ghaneps-exchanges/Tender/tender-1/events/event-1',
    ]);
  });

  it('targets exact append-only export, import, attempt, retry, acknowledgement, and reconciliation routes', async () => {
    const payload = {
      sourceType: 'Tender' as const,
      sourceId: 'tender-1',
      eventFamily: 'TenderPublication' as const,
      mappingKey: 'TENDER-PUB',
      eventReference: 'PUB-001',
      payloadContent: '{}',
      evidenceReference: 'EVIDENCE-1',
      idempotencyKey: 'ghaneps-action-1',
    };
    await service.prepareExport('Tender', 'tender-1', payload);
    await service.recordImport('Tender', 'tender-1', {
      ...payload,
      transportReference: 'TRANSPORT-1',
    });
    await service.recordAttempt('Tender', 'tender-1', 'event-1', {
      payloadId: 'payload-1',
      outcome: 'Failed',
      failureCode: 'TIMEOUT',
      failureMessage: 'Gateway timeout',
      evidenceReference: 'EVIDENCE-2',
      idempotencyKey: 'ghaneps-attempt-1',
      expectedRowVersion: 'AAAA',
    });
    await service.retry('Tender', 'tender-1', 'event-1', {
      payloadId: 'payload-1',
      outcome: 'Succeeded',
      transportReference: 'TRANSPORT-2',
      evidenceReference: 'EVIDENCE-3',
      idempotencyKey: 'ghaneps-retry-1',
      expectedRowVersion: 'AAAB',
    });
    await service.recordAcknowledgement(
      'Tender',
      'tender-1',
      'event-1',
      {
        outcome: 'Accepted',
        acknowledgementReference: 'ACK-001',
        acknowledgementContent: '{}',
        evidenceReference: 'EVIDENCE-4',
        idempotencyKey: 'ghaneps-ack-1',
        expectedRowVersion: 'AAAC',
      }
    );
    await service.reconcile('Tender', 'tender-1', 'event-1', {
      resolveExistingMismatch: false,
      actualReference: 'ACK-001',
      actualChecksumSha256: 'a'.repeat(64),
      evidenceReference: 'EVIDENCE-5',
      idempotencyKey: 'ghaneps-reconcile-1',
      expectedRowVersion: 'AAAD',
    });

    expect(api.post.mock.calls.map(([path]) => path)).toEqual([
      '/procurement/ghaneps-exchanges/Tender/tender-1/exports',
      '/procurement/ghaneps-exchanges/Tender/tender-1/imports',
      '/procurement/ghaneps-exchanges/Tender/tender-1/events/event-1/attempts',
      '/procurement/ghaneps-exchanges/Tender/tender-1/events/event-1/retry',
      '/procurement/ghaneps-exchanges/Tender/tender-1/events/event-1/acknowledgements',
      '/procurement/ghaneps-exchanges/Tender/tender-1/events/event-1/reconciliations',
    ]);
  });
});
