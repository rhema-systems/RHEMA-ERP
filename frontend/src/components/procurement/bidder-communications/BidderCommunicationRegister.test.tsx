import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { BidderCommunicationRegister } from './BidderCommunicationRegister';
import type {
  ProcurementBidderCommunicationOverview,
  ProcurementBidderCommunicationRecipient,
} from '@/types/procurement-bidder-communication';

const recipient = (
  overrides: Partial<ProcurementBidderCommunicationRecipient> = {}
): ProcurementBidderCommunicationRecipient => ({
  id: 'recipient-1',
  businessPartnerId: 'supplier-1',
  outcome: 'Successful',
  bidOrQuoteIds: ['bid-1'],
  partnerCode: 'SUP-001',
  partnerName: 'Qualified Supplier Ltd',
  recipientEmail: 'supplier@example.test',
  lineageHash: 'a'.repeat(64),
  integrityHash: 'b'.repeat(64),
  letterVersions: [
    {
      id: 'letter-1',
      version: 1,
      templateVersionId: 'template-version-1',
      templateReference: 'SUCCESS-LETTER v3',
      templateChecksumSha256: 'c'.repeat(64),
      contentReference: 'LETTER-001',
      contentChecksumSha256: 'd'.repeat(64),
      workflowDefinitionId: 'workflow-definition-1',
      workflowInstanceId: 'workflow-instance-1',
      approvalReference: 'APPROVAL-001',
      approvalEvidenceReference: 'EVIDENCE-001',
      approvedAtUtc: '2026-07-24T10:00:00Z',
      approvedByUserId: 'approver-1',
      approvedByName: 'Procurement Approver',
      integrityHash: 'e'.repeat(64),
      dispatches: [
        {
          id: 'dispatch-1',
          sequence: 1,
          channel: 'SupplierPortal',
          destination: 'supplier-1',
          dispatchReference: 'DISPATCH-001',
          dispatchEvidenceReference: 'EVIDENCE-002',
          dispatchedAtUtc: '2026-07-24T11:00:00Z',
          dispatchedByUserId: 'officer-1',
          dispatchedByName: 'Procurement Officer',
          integrityHash: 'f'.repeat(64),
          deliveries: [
            {
              id: 'delivery-1',
              sequence: 1,
              outcome: 'Delivered',
              occurredAtUtc: '2026-07-24T11:01:00Z',
              providerReference: 'PROVIDER-001',
              evidenceReference: 'EVIDENCE-003',
              integrityHash: '1'.repeat(64),
            },
          ],
          acknowledgements: [
            {
              id: 'ack-1',
              sequence: 1,
              outcome: 'Received',
              acknowledgedAtUtc: '2026-07-24T12:00:00Z',
              acknowledgedByUserId: 'supplier-user-1',
              acknowledgedByBusinessPartnerId: 'supplier-1',
              acknowledgementChannel: 'SupplierPortal',
              acknowledgementReference: 'ACK-001',
              evidenceReference: 'EVIDENCE-004',
              integrityHash: '2'.repeat(64),
            },
          ],
        },
      ],
    },
  ],
  appeals: [],
  securityInstruments: [
    {
      id: 'security-1',
      tenderBidId: 'bid-1',
      instrumentType: 'BidBond',
      instrumentReference: 'BOND-001',
      issuerName: 'Secure Bank',
      amount: 50000,
      currencyCode: 'GHS',
      issuedAtUtc: '2026-06-01T00:00:00Z',
      expiresAtUtc: '2026-10-01T00:00:00Z',
      evidenceReference: 'EVIDENCE-005',
      registeredAtUtc: '2026-07-24T09:00:00Z',
      integrityHash: '3'.repeat(64),
      actions: [],
      allowedActions: ['Release'],
      blockedReasons: [],
    },
  ],
  allowedActions: ['Acknowledge'],
  ...overrides,
});

const overview = (
  overrides: Partial<ProcurementBidderCommunicationOverview> = {}
): ProcurementBidderCommunicationOverview => ({
  id: 'register-1',
  sourceType: 'Tender',
  sourceId: 'tender-1',
  sourceReference: 'TDR-001',
  awardFamily: 'FormalTender',
  awardId: 'award-1',
  awardReference: 'AWARD-001',
  awardedAtUtc: '2026-07-24T09:00:00Z',
  awardReadinessDecisionId: 'decision-1',
  awardReadinessDecisionSequence: 4,
  awardReadinessIntegrityHash: '4'.repeat(64),
  awardReadinessSourceIntegrityHash: '5'.repeat(64),
  awardReadinessIsCurrent: true,
  standstillStartsAtUtc: '2026-07-24T09:00:00Z',
  standstillEndsAtUtc: '2026-08-07T09:00:00Z',
  appealWindowEndsAtUtc: '2026-08-14T09:00:00Z',
  standstillAuthorityReference: 'PPA-STANDSTILL-001',
  standstillElapsed: false,
  appealWindowOpen: true,
  hasOpenAppeals: false,
  initializedAtUtc: '2026-07-24T09:30:00Z',
  initializedByUserId: 'officer-1',
  initializedByName: 'Procurement Officer',
  recipientSnapshotHash: '6'.repeat(64),
  integrityHash: '7'.repeat(64),
  rowVersion: 'AQID',
  recipients: [
    recipient(),
    recipient({
      id: 'recipient-2',
      businessPartnerId: 'supplier-2',
      outcome: 'Unsuccessful',
      bidOrQuoteIds: ['bid-2'],
      partnerCode: 'SUP-002',
      partnerName: 'Other Supplier Ltd',
      recipientEmail: 'other@example.test',
      letterVersions: [],
      securityInstruments: [],
      allowedActions: ['Acknowledge', 'FileAppeal'],
    }),
  ],
  allowedActions: ['ApproveLetter', 'RegisterSecurity'],
  blockedReasons: [],
  ...overrides,
});

describe('bidder communication history-first register', () => {
  it('renders exact recipient, letter, dispatch, delivery, acknowledgement, and security history', () => {
    const onAction = vi.fn();
    render(
      <BidderCommunicationRegister
        overview={overview()}
        external={false}
        canManage
        canApprove
        onAction={onAction}
      />
    );

    expect(
      screen.getByText('Server-derived recipient register')
    ).toBeInTheDocument();
    expect(screen.getByText('Qualified Supplier Ltd')).toBeInTheDocument();
    expect(screen.getByText('Other Supplier Ltd')).toBeInTheDocument();
    expect(screen.getByText('SUCCESS-LETTER v3')).toBeInTheDocument();
    expect(screen.getAllByText(/Delivered/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Received/).length).toBeGreaterThan(0);
    expect(screen.getByText('BOND-001')).toBeInTheDocument();
    expect(
      screen.getByText('Immutable communication timeline')
    ).toBeInTheDocument();

    fireEvent.click(
      screen.getAllByRole('button', { name: 'Approve letter version' })[0]
    );
    expect(onAction).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'approve-letter' })
    );
  });

  it('renders only the supplier-scoped status and external actions', () => {
    const onAction = vi.fn();
    render(
      <BidderCommunicationRegister
        overview={overview({
          recipients: [
            recipient({
              outcome: 'Unsuccessful',
              allowedActions: ['Acknowledge', 'FileAppeal'],
            }),
          ],
        })}
        external
        canManage={false}
        canApprove={false}
        onAction={onAction}
      />
    );

    expect(
      screen.getByTestId('external-bidder-communication-status')
    ).toBeInTheDocument();
    expect(
      screen.getByText('My award communication and security status')
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'Acknowledge' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'File appeal' })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Approve letter version' })
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: 'Register security' })
    ).not.toBeInTheDocument();
  });

  it('fails closed if the external endpoint returns an ambiguous recipient set', () => {
    render(
      <BidderCommunicationRegister
        overview={overview()}
        external
        canManage={false}
        canApprove={false}
        onAction={vi.fn()}
      />
    );

    expect(
      screen.getByText('Supplier scope could not be verified')
    ).toBeInTheDocument();
    expect(screen.queryByText('Qualified Supplier Ltd')).not.toBeInTheDocument();
    expect(screen.queryByText('Other Supplier Ltd')).not.toBeInTheDocument();
  });
});
