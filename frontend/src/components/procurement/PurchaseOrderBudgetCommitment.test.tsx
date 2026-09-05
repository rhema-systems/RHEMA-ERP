import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import { PurchaseOrderBudgetCommitment } from './PurchaseOrderBudgetCommitment';

describe('PurchaseOrderBudgetCommitment', () => {
  it('shows the PO commitment reference, final status and ordered lifecycle', () => {
    const markup = renderToStaticMarkup(
      <PurchaseOrderBudgetCommitment
        commitment={{
          commitmentId: 'commitment-006',
          reference: 'PO-COM-2026-006',
          status: 'Committed',
          reservationStatus: 'Reserved',
          amount: 125000,
          reservedAmount: 200000,
          formallyCommittedAmount: 125000,
          currency: 'GHS',
          reservationSequence: 1,
          history: [
            {
              sequence: 2,
              status: 'Committed',
              event: 'FormalCommitment',
              action: 'FormallyCommitted',
              amount: 125000,
              occurredAtUtc: '2026-08-29T10:01:00Z',
              actorName: 'Independent PO Approver',
              correlationId: 'corr-commit-006',
            },
            {
              sequence: 1,
              status: 'Reserved',
              event: 'Reservation',
              action: 'BudgetCommitmentReserved',
              amount: 125000,
              occurredAtUtc: '2026-08-29T10:00:59Z',
              actorName: 'Independent PO Approver',
              correlationId: 'corr-reserve-006',
            },
          ],
        }}
      />
    );

    expect(markup).toContain('PO-COM-2026-006');
    expect(markup).toContain('PO commitment status');
    expect(markup).toContain('Reservation envelope');
    expect(markup).toContain('Reserved');
    expect(markup).toContain('PO formally committed');
    expect(markup).toContain('GHS');
    expect(markup.indexOf('BudgetCommitmentReserved')).toBeLessThan(
      markup.indexOf('FormalCommitment')
    );
    expect(markup).toContain('Final PO approval reserved and committed');
  });

  it('describes a contract child PO as an allocation rather than another formal commitment', () => {
    const markup = renderToStaticMarkup(
      <PurchaseOrderBudgetCommitment
        commitment={{
          commitmentId: 'contract-commitment-006',
          reference: 'CON-COM-2026-006',
          status: 'Allocated',
          reservationStatus: 'Reserved',
          amount: 25000,
          reservedAmount: 100000,
          formallyCommittedAmount: 100000,
          currency: 'GHS',
          reservationSequence: 1,
          history: [],
        }}
      />
    );

    expect(markup).toContain('PO allocated to contract');
    expect(markup).toContain('PO allocated exposure');
    expect(markup).toContain('existing contract commitment');
  });
});
