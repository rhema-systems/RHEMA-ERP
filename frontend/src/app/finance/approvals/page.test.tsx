import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ApprovalWorkbench } from '@/components/approvals/approval-workbench';
import {
  getFinanceApprovalQueueDefinitions,
  normalizeFinanceApprovalCurrencyCode,
} from '@/lib/finance/approval-queue-definitions';

const apiServiceMock = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({
  apiService: apiServiceMock,
  default: apiServiceMock,
}));

describe('Finance approval workbench', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('keeps supported ISO currency codes and contains malformed values', () => {
    expect(normalizeFinanceApprovalCurrencyCode(' usd ')).toBe('USD');
    expect(normalizeFinanceApprovalCurrencyCode('ABC')).toBe('GHS');
    expect(normalizeFinanceApprovalCurrencyCode('2026-01')).toBe('GHS');
  });

  it('renders a fixed-asset opening approval amount and status when the API currency is malformed', async () => {
    const amount = 987_654.32;
    apiServiceMock.get.mockResolvedValueOnce([
      {
        approvalId: 'approval-1',
        entityId: 'opening-batch-1',
        entityType: 'OpeningBalanceBatch',
        reference: 'OB-20260820-FA',
        title: 'Fixed-asset IFRS opening balances',
        detailHref: '/finance/opening-balances?batchId=opening-batch-1',
        documentType: 'Opening Balance',
        module: 'Migration',
        currentStep: 'Finance review',
        statusLabel: 'PendingApproval',
        submittedAt: '2026-08-20T10:00:00Z',
        amount,
        currencyCode: '2026-01',
        submittedBy: 'Finance Maker',
        canApprove: true,
        canReject: true,
      },
    ]);

    render(
      <ApprovalWorkbench
        title="Finance Approval Workbench"
        description="Finance approvals"
        definitions={getFinanceApprovalQueueDefinitions()}
      />
    );

    expect(
      await screen.findByText('Fixed-asset IFRS opening balances')
    ).toBeInTheDocument();
    expect(screen.getByText('PendingApproval')).toBeInTheDocument();

    const amountLabel = screen.getByText('Amount');
    const expectedAmount = new Intl.NumberFormat('en-GH', {
      style: 'currency',
      currency: 'GHS',
    }).format(amount);
    expect(amountLabel.nextElementSibling).toHaveTextContent(expectedAmount);
  });
});
