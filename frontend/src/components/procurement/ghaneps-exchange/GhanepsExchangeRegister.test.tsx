import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GhanepsExchangeRegister } from './GhanepsExchangeRegister';
import type {
  ProcurementGhanepsExchangeEvent,
  ProcurementGhanepsExchangeOptions,
  ProcurementGhanepsExchangeOverview,
} from '@/types/procurement-ghaneps-exchange';

const options = (
  allowedActions: string[] = ['PrepareExport', 'RecordImport']
): ProcurementGhanepsExchangeOptions => ({
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
  acknowledgementRule: 'Retain exact acknowledgement.',
  reconciliationRule: 'Match reference and checksum.',
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
      acknowledgementContentType: 'application/xml',
      acknowledgementPermissionCode: 'procurement.tender.approve',
      reconciliationPermissionCode: 'procurement.tender.reconcile',
      acknowledgementRequired: true,
      reconciliationRequired: true,
      maximumRetryAttempts: 3,
    },
  ],
  allowedActions,
  blockedReasons: [],
});

const event = (): ProcurementGhanepsExchangeEvent => ({
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
  acknowledgementContentType: 'application/xml',
  acknowledgementPermissionCode: 'procurement.tender.approve',
  reconciliationPermissionCode: 'procurement.tender.reconcile',
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
  acknowledgementRule: 'Retain exact acknowledgement.',
  reconciliationRule: 'Match reference and checksum.',
  acknowledgementRequired: true,
  reconciliationRequired: true,
  maximumRetryAttempts: 3,
  status: 'ReconciliationException',
  requestFingerprint: '7'.repeat(64),
  correlationId: 'correlation-1',
  preparedByUserId: 'user-1',
  preparedByName: 'Procurement Officer',
  preparedAtUtc: '2026-07-24T09:01:00Z',
  evidenceReference: 'EVIDENCE-1',
  integrityHash: 'd'.repeat(64),
  rowVersion: 'AAAA',
  allowedActions: [],
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
      recordedByName: 'Procurement Officer',
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
      attemptedByName: 'Procurement Officer',
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
      reconciledByName: 'Procurement Reviewer',
      evidenceReference: 'EVIDENCE-3',
      integrityHash: '3'.repeat(64),
    },
  ],
  history: [],
});

const overview = (
  retainedEvent = event()
): ProcurementGhanepsExchangeOverview => ({
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  sourceVariant: 'NCT',
  events: [retainedEvent],
});

describe('GHANEPS history-first exchange register', () => {
  it('renders exact profile, mapping, payload, failure, reconciliation, and immutable timeline lineage', () => {
    render(
      <GhanepsExchangeRegister
        overview={overview()}
        options={options()}
        history={[
          {
            exchangeEventId: 'event-1',
            eventFamily: 'TenderPublication',
            eventReference: 'PUB-001',
            kind: 'Reconciliation',
            recordId: 'reconciliation-1',
            sequence: 1,
            outcome: 'Mismatch',
            reference: 'PUB-002',
            evidenceReference: 'EVIDENCE-3',
            actorUserId: 'user-2',
            actorName: 'Procurement Reviewer',
            occurredAtUtc: '2026-07-24T10:00:00Z',
            integrityHash: '3'.repeat(64),
          },
        ]}
        canManage={false}
        hasConfiguredPermission={() => false}
        serverActions={[]}
        eventActions={{}}
        eventBlockedReasons={{}}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.getAllByText(/GHANEPS-PHASE-1/).length
    ).toBeGreaterThan(0);
    expect(screen.getAllByText('TENDER-PUB').length).toBeGreaterThan(0);
    expect(
      screen.getAllByText('procurement.tender.approve').length
    ).toBeGreaterThan(0);
    expect(
      screen.getAllByText('procurement.tender.reconcile').length
    ).toBeGreaterThan(0);
    expect(screen.getByText('7'.repeat(64))).toBeVisible();
    expect(screen.getByText('Payload v1')).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Download JSON payload' })
    ).toBeVisible();
    expect(screen.getAllByText('Failed').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Mismatch').length).toBeGreaterThan(0);
    expect(
      screen.getByText('Attempt attempt-1 · payload payload-1')
    ).toBeVisible();
    expect(screen.getByTestId('ghaneps-exchange-timeline')).toHaveTextContent(
      'Tender Publication / PUB-001'
    );
  });

  it('requires both client permission and authoritative server actions', () => {
    const { rerender } = render(
      <GhanepsExchangeRegister
        overview={overview()}
        options={options()}
        history={[]}
        canManage={true}
        hasConfiguredPermission={() => true}
        serverActions={[]}
        eventActions={{ 'event-1': [] }}
        eventBlockedReasons={{}}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.queryByRole('button', { name: 'Prepare export' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Record attempt' })
    ).not.toBeInTheDocument();

    rerender(
      <GhanepsExchangeRegister
        overview={overview()}
        options={options()}
        history={[]}
        canManage={true}
        hasConfiguredPermission={() => true}
        serverActions={['PrepareExport']}
        eventActions={{
          'event-1': [
            'RecordAttempt',
            'Retry',
            'RecordAcknowledgement',
            'ResolveReconciliation',
          ],
        }}
        eventBlockedReasons={{}}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.getByRole('button', { name: 'Prepare export' })
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Record attempt' })
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Retry failed attempt' })
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Record acknowledgement' })
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Resolve mismatch' })
    ).toBeVisible();
  });

  it('uses each immutable configured permission for its matching action gate', () => {
    render(
      <GhanepsExchangeRegister
        overview={overview()}
        options={options()}
        history={[]}
        canManage
        hasConfiguredPermission={(permissionCode) =>
          permissionCode === 'procurement.tender.reconcile'
        }
        serverActions={[]}
        eventActions={{
          'event-1': ['RecordAcknowledgement', 'ResolveReconciliation'],
        }}
        eventBlockedReasons={{}}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.queryByRole('button', { name: 'Record acknowledgement' })
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Resolve mismatch' })
    ).toBeVisible();
  });

  it('offers a corrected-payload retry after a rejected acknowledgement', () => {
    const rejectedEvent = event();
    rejectedEvent.status = 'Failed';
    rejectedEvent.attempts = [
      {
        ...rejectedEvent.attempts[0],
        outcome: 'Succeeded',
        failureCode: undefined,
        failureMessage: undefined,
        transportReference: 'GHANEPS-SUBMIT-1',
      },
    ];
    rejectedEvent.acknowledgements = [
      {
        id: 'ack-1',
        attemptId: 'attempt-1',
        payloadId: 'payload-1',
        sequence: 1,
        outcome: 'Rejected',
        acknowledgementReference: 'ACK-REJECT-1',
        externalStatusCode: 'INVALID_PAYLOAD',
        contentType: 'application/xml',
        acknowledgementContent: '<ack />',
        acknowledgementChecksumSha256: '4'.repeat(64),
        requestFingerprint: '7'.repeat(64),
        acknowledgedAtUtc: '2026-07-24T11:00:00Z',
        acknowledgedByUserId: 'user-2',
        acknowledgedByName: 'Procurement Reviewer',
        evidenceReference: 'EVIDENCE-4',
        integrityHash: '5'.repeat(64),
      },
    ];
    const onAction = vi.fn();

    render(
      <GhanepsExchangeRegister
        overview={overview(rejectedEvent)}
        options={options()}
        history={[]}
        canManage
        hasConfiguredPermission={() => true}
        serverActions={[]}
        eventActions={{ 'event-1': ['Retry'] }}
        eventBlockedReasons={{}}
        onAction={onAction}
      />
    );

    expect(
      screen.getByRole('button', {
        name: 'Download XML acknowledgement',
      })
    ).toBeVisible();
    fireEvent.click(
      screen.getByRole('button', {
        name: 'Retry rejected acknowledgement',
      })
    );
    expect(onAction).toHaveBeenCalledWith(
      expect.objectContaining({
        type: 'retry',
        requiresCorrectedPayload: true,
        rejectedAcknowledgement: expect.objectContaining({
          acknowledgementReference: 'ACK-REJECT-1',
        }),
      })
    );
  });

  it('binds initial reconciliation and mismatch resolution to their exact server actions', () => {
    const mismatchEvent = event();
    const { rerender } = render(
      <GhanepsExchangeRegister
        overview={overview(mismatchEvent)}
        options={options()}
        history={[]}
        canManage
        hasConfiguredPermission={() => true}
        serverActions={[]}
        eventActions={{ 'event-1': ['Reconcile'] }}
        eventBlockedReasons={{}}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.queryByRole('button', { name: 'Reconcile' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Resolve mismatch' })
    ).not.toBeInTheDocument();

    const unreconciledEvent = event();
    unreconciledEvent.status = 'Transferred';
    unreconciledEvent.reconciliations = [];
    rerender(
      <GhanepsExchangeRegister
        overview={overview(unreconciledEvent)}
        options={options()}
        history={[]}
        canManage
        hasConfiguredPermission={() => true}
        serverActions={[]}
        eventActions={{ 'event-1': ['ResolveReconciliation'] }}
        eventBlockedReasons={{}}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.queryByRole('button', { name: 'Reconcile' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Resolve mismatch' })
    ).not.toBeInTheDocument();
  });

  it('does not offer reconciliation after a terminal matched decision', () => {
    const terminalEvent = event();
    terminalEvent.status = 'Reconciled';
    terminalEvent.reconciliations = [
      {
        ...terminalEvent.reconciliations[0],
        outcome: 'Matched',
        actualReference: 'PUB-001',
        actualChecksumSha256: 'e'.repeat(64),
      },
    ];

    render(
      <GhanepsExchangeRegister
        overview={overview(terminalEvent)}
        options={options()}
        history={[]}
        canManage
        hasConfiguredPermission={() => true}
        serverActions={[]}
        eventActions={{ 'event-1': ['ResolveReconciliation'] }}
        eventBlockedReasons={{}}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.queryByRole('button', { name: 'Reconcile' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Resolve mismatch' })
    ).not.toBeInTheDocument();
  });

  it('keeps a valid empty exchange source history-first and explicit', () => {
    render(
      <GhanepsExchangeRegister
        overview={{ ...overview(), events: [] }}
        options={options()}
        history={[]}
        canManage={false}
        hasConfiguredPermission={() => false}
        serverActions={[]}
        eventActions={{}}
        eventBlockedReasons={{}}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.getByText(
        'No GHANEPS exchange event has been retained for this source.'
      )
    ).toBeVisible();
    expect(
      screen.getByText(
        'No immutable GHANEPS history entry has been retained.'
      )
    ).toBeVisible();
  });
});
