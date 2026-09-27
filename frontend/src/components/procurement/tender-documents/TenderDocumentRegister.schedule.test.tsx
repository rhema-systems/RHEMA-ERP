import React from 'react';
import { render, screen } from '@testing-library/react';
import { expect, it } from 'vitest';
import { TenderDocumentRegister } from './TenderDocumentRegister';
import type { ProcurementTenderDocumentReadiness, ProcurementTenderDocumentRegister as Register } from '@/types/procurement-tender-document';

it('retains original and effective opening times and the governed opening-date history', () => {
  const previousOpening = '2030-09-05T17:05:00Z';
  const nextOpening = '2030-09-06T17:05:00Z';
  const register = {
    blockedReasons: [], originalOpeningScheduledAtUtc: previousOpening, openingScheduledAtUtc: nextOpening,
    originalSubmissionDeadlineUtc: '2030-09-05T17:00:00Z', effectiveSubmissionDeadlineUtc: '2030-09-06T17:00:00Z',
    originalBidValidityUntilUtc: '2030-10-05T17:00:00Z', effectiveBidValidityUntilUtc: '2030-10-05T17:00:00Z',
    integrityHash: 'test-integrity-hash', feeMode: 'Free', currencyCode: 'GHS', issuances: [],
    changes: [{ id: 'schedule-1', sequence: 1, changeType: 'UnpublishedScheduleReschedule', status: 'Approved',
      reason: 'Audited unpublished schedule correction', integrityHash: 'change-integrity-hash',
      recipients: [], allowedActions: [], blockedReasons: [], previousOpeningScheduledAtUtc: previousOpening, newOpeningScheduledAtUtc: nextOpening }],
  } as unknown as Register;
  const readiness: ProcurementTenderDocumentReadiness = {
    sourceType: 'Tender', sourceId: 'tender-1', sourceReference: 'TND-001', hasRegister: true,
    ready: true, issuanceCount: 0, pendingAcknowledgementCount: 0, pendingChangeCount: 0, blockedReasons: [], allowedActions: [],
  };
  render(<TenderDocumentRegister readiness={readiness} register={register} />);
  expect(screen.getByText(/^Original opening time:/).parentElement).toHaveTextContent(new Date(previousOpening).toLocaleString());
  expect(screen.getByText(/^Effective opening time:/).parentElement).toHaveTextContent(new Date(nextOpening).toLocaleString());
  expect(screen.getByText(/^Previous opening time:/).parentElement).toHaveTextContent(new Date(previousOpening).toLocaleString());
  expect(screen.getByText(/^New opening time:/).parentElement).toHaveTextContent(new Date(nextOpening).toLocaleString());
  expect(screen.getByText(/Pre-publication schedule change/)).toBeInTheDocument();
});
