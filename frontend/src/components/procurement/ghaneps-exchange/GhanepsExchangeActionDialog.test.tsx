import React from 'react';
import {
  fireEvent,
  render,
  screen,
  waitFor,
} from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const serviceMocks = vi.hoisted(() => ({
  prepareExport: vi.fn(),
  recordImport: vi.fn(),
  recordAttempt: vi.fn(),
  retry: vi.fn(),
  recordAcknowledgement: vi.fn(),
  reconcile: vi.fn(),
}));

vi.mock('@/services/procurement-ghaneps-exchange.service', () => ({
  procurementGhanepsExchangeService: serviceMocks,
}));

import { GhanepsExchangeActionDialog } from './GhanepsExchangeActionDialog';
import type {
  ProcurementGhanepsExchangeAcknowledgement,
  ProcurementGhanepsExchangeAttempt,
  ProcurementGhanepsExchangeEvent,
  ProcurementGhanepsExchangeMappingOption,
  ProcurementGhanepsExchangePayload,
} from '@/types/procurement-ghaneps-exchange';

const mapping: ProcurementGhanepsExchangeMappingOption = {
  mappingKey: 'TENDER-PUB',
  eventFamily: 'TenderPublication',
  direction: 'Export',
  externalEventCode: 'TENDER_PUBLISHED',
  templateReference: 'TENDER-PUB-v1',
  schemaReference: 'GHANEPS-TENDER-v1',
  payloadVersion: '1.0',
  referenceField: 'tenderReference',
  payloadContentType: 'application/json',
  acknowledgementContentType: 'text/xml',
  acknowledgementPermissionCode: 'procurement.tender.approve',
  reconciliationPermissionCode: 'procurement.tender.approve',
  acknowledgementRequired: true,
  reconciliationRequired: true,
  maximumRetryAttempts: 3,
};

const event = {
  id: 'event-1',
  eventFamily: 'TenderPublication',
  eventReference: 'TDR-001',
  status: 'Failed',
  requestFingerprint: '7'.repeat(64),
  integrityHash: 'a'.repeat(64),
  sourceIntegrityHash: 'b'.repeat(64),
  payloadContentType: 'application/json',
  acknowledgementContentType: 'text/xml',
  acknowledgementPermissionCode: 'procurement.tender.approve',
  reconciliationPermissionCode: 'procurement.tender.approve',
  rowVersion: 'AAAA',
  attempts: [],
} as unknown as ProcurementGhanepsExchangeEvent;

const payload = {
  id: 'payload-1',
  version: 1,
  direction: 'Export',
  payloadChecksumSha256: 'c'.repeat(64),
} as ProcurementGhanepsExchangePayload;

const priorAttempt = {
  id: 'attempt-1',
  payloadId: 'payload-1',
  attemptNumber: 1,
  outcome: 'Succeeded',
  integrityHash: 'd'.repeat(64),
} as ProcurementGhanepsExchangeAttempt;

const rejectedAcknowledgement = {
  id: 'ack-1',
  attemptId: 'attempt-1',
  payloadId: 'payload-1',
  acknowledgementReference: 'ACK-REJECT-1',
  outcome: 'Rejected',
} as ProcurementGhanepsExchangeAcknowledgement;

describe('GHANEPS exchange controlled action dialog', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders the exact source reference read-only for prepare export', () => {
    render(
      <GhanepsExchangeActionDialog
        sourceType="Tender"
        sourceId="tender-1"
        sourceReference="TDR-001"
        action={{ type: 'prepare-export', mapping }}
        onOpenChange={vi.fn()}
        onChanged={vi.fn()}
      />
    );

    expect(
      screen.getByText('tenderReference (exact source reference)')
    ).toBeVisible();
    expect(screen.getByText('TDR-001')).toBeVisible();
    expect(
      screen.queryByRole('textbox', { name: /tenderReference/i })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('textbox', { name: 'Content type *' })
    ).not.toBeInTheDocument();
    expect(screen.getByText('JSON · application/json')).toBeVisible();
  });

  it('requires a corrected replacement payload after rejected acknowledgement', () => {
    render(
      <GhanepsExchangeActionDialog
        sourceType="Tender"
        sourceId="tender-1"
        sourceReference="TDR-001"
        action={{
          type: 'retry',
          event,
          payload,
          priorAttempt,
          rejectedAcknowledgement,
          requiresCorrectedPayload: true,
        }}
        onOpenChange={vi.fn()}
        onChanged={vi.fn()}
      />
    );

    expect(screen.getByText('Corrected replacement payload *')).toBeVisible();
    expect(
      screen.getByText(
        'A corrected replacement payload is required after a rejected acknowledgement.'
      )
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Record retry' })
    ).toBeDisabled();
  });

  it('labels independent mismatch resolution distinctly', () => {
    render(
      <GhanepsExchangeActionDialog
        sourceType="Tender"
        sourceId="tender-1"
        sourceReference="TDR-001"
        action={{
          type: 'reconcile',
          event,
          resolveExistingMismatch: true,
        }}
        onOpenChange={vi.fn()}
        onChanged={vi.fn()}
      />
    );

    expect(
      screen.getByRole('heading', {
        name: 'Resolve GHANEPS reconciliation mismatch',
      })
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Resolve mismatch' })
    ).toBeDisabled();
  });

  it('requires the server-mandated transport reference for a successful attempt', () => {
    render(
      <GhanepsExchangeActionDialog
        sourceType="Tender"
        sourceId="tender-1"
        sourceReference="TDR-001"
        action={{ type: 'record-attempt', event, payload }}
        onOpenChange={vi.fn()}
        onChanged={vi.fn()}
      />
    );

    fireEvent.click(screen.getByRole('combobox'));
    fireEvent.click(screen.getByRole('option', { name: 'Succeeded' }));

    expect(
      screen.getByText(
        'A successful attempt requires a transport reference.'
      )
    ).toBeVisible();
    fireEvent.change(screen.getByLabelText('Transport reference'), {
      target: { value: 'GHANEPS-SUBMIT-1' },
    });
    expect(
      screen.getByRole('button', { name: 'Record attempt' })
    ).toBeEnabled();
  });

  it('loads a bounded JSON import file into the editable payload fields', async () => {
    render(
      <GhanepsExchangeActionDialog
        sourceType="Tender"
        sourceId="tender-1"
        sourceReference="TDR-001"
        action={{ type: 'record-import', mapping }}
        onOpenChange={vi.fn()}
        onChanged={vi.fn()}
      />
    );
    const file = new File(['{"externalReference":"GH-001"}'], 'import.json', {
      type: 'application/json',
    });
    Object.defineProperty(file, 'arrayBuffer', {
      value: vi.fn().mockResolvedValue(
        new TextEncoder().encode('{"externalReference":"GH-001"}').buffer
      ),
    });

    fireEvent.change(screen.getByLabelText(/Load JSON file/i), {
      target: { files: [file] },
    });

    await waitFor(() =>
      expect(screen.getByLabelText('Payload content (JSON) *')).toHaveValue(
        '{"externalReference":"GH-001"}'
      )
    );
    expect(screen.getByLabelText('File name')).toHaveValue('import.json');
    expect(
      screen.queryByRole('textbox', { name: /Content type/i })
    ).not.toBeInTheDocument();
  });

  it('rejects an oversized JSON file before reading or submission', async () => {
    render(
      <GhanepsExchangeActionDialog
        sourceType="Tender"
        sourceId="tender-1"
        sourceReference="TDR-001"
        action={{ type: 'record-import', mapping }}
        onOpenChange={vi.fn()}
        onChanged={vi.fn()}
      />
    );
    const file = new File(['{}'], 'oversized.json', {
      type: 'application/json',
    });
    Object.defineProperty(file, 'size', { value: 5_000_001 });

    fireEvent.change(screen.getByLabelText(/Load JSON file/i), {
      target: { files: [file] },
    });

    expect(
      await screen.findByText(
        'JSON files must not exceed 5,000,000 UTF-8 bytes.'
      )
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Record import' })
    ).toBeDisabled();
  });

  it('reuses the action idempotency key after an uncertain network result', async () => {
    serviceMocks.prepareExport.mockRejectedValue(
      new Error('Network response was interrupted')
    );
    render(
      <GhanepsExchangeActionDialog
        sourceType="Tender"
        sourceId="tender-1"
        sourceReference="TDR-001"
        action={{ type: 'prepare-export', mapping }}
        onOpenChange={vi.fn()}
        onChanged={vi.fn()}
      />
    );
    fireEvent.change(screen.getByLabelText('Payload content (JSON) *'), {
      target: { value: '{"reference":"TDR-001"}' },
    });
    const submit = screen.getByRole('button', { name: 'Prepare export' });

    fireEvent.click(submit);
    await waitFor(() =>
      expect(serviceMocks.prepareExport).toHaveBeenCalledTimes(1)
    );
    await waitFor(() => expect(submit).toBeEnabled());
    fireEvent.click(submit);
    await waitFor(() =>
      expect(serviceMocks.prepareExport).toHaveBeenCalledTimes(2)
    );

    const firstKey =
      serviceMocks.prepareExport.mock.calls[0][2].idempotencyKey;
    const secondKey =
      serviceMocks.prepareExport.mock.calls[1][2].idempotencyKey;
    expect(firstKey).toBe(secondKey);
    expect(serviceMocks.prepareExport.mock.calls[0][2]).toMatchObject({
      payloadContent: '{"reference":"TDR-001"}',
    });
    expect(serviceMocks.prepareExport.mock.calls[0][2]).not.toHaveProperty(
      'contentType'
    );
  });
});
