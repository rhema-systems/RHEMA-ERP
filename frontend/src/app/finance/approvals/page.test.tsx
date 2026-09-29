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

  it('shows an assigned accounting-book transition but routes its decision to the book page', async () => {
    apiServiceMock.get.mockResolvedValueOnce([{
      approvalId: 'approval-book-1',
      entityId: 'book-1',
      entityType: 'AccountingBookLifecycle',
      reference: 'IFRS',
      title: 'IFRS Primary: Configuring to Initializing',
      detailHref: '/finance/settings/accounting-books',
      documentType: 'Accounting Book',
      module: 'Finance Settings',
      currentStep: 'Financial Controller Review',
      statusLabel: 'Pending transition',
      decisionOnDetailPage: true,
      canApprove: false,
      canReject: false,
    }]);

    render(
      <ApprovalWorkbench
        title="Finance Approval Workbench"
        description="Finance approvals"
        definitions={getFinanceApprovalQueueDefinitions()}
      />
    );

    expect(await screen.findByText('IFRS Primary: Configuring to Initializing')).toBeInTheDocument();
    expect(screen.getByText('Total Pending').nextElementSibling).toHaveTextContent('1');
    expect(screen.getByRole('link', { name: 'Review & decide' })).toHaveAttribute(
      'href', '/finance/settings/accounting-books'
    );
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reject' })).not.toBeInTheDocument();
  });

  it('shows an assigned Business Partner identity once and routes the decision to its record', async () => {
    apiServiceMock.get.mockResolvedValue([{
      approvalId: 'approval-partner-1',
      entityId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      entityType: 'BusinessPartner',
      reference: 'SUP260001',
      title: 'Akwaaba Technical Services Ltd',
      detailHref: '/procurement/business-partners/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      documentType: 'Business Partner',
      module: 'Procurement / Finance Master Data',
      currentStep: 'Financial Controller Review',
      statusLabel: 'Pending Approval',
      decisionOnDetailPage: true,
      canApprove: false,
      canReject: false,
    }]);

    render(
      <ApprovalWorkbench
        title="Finance Approval Workbench"
        description="Finance approvals"
        definitions={getFinanceApprovalQueueDefinitions()}
      />
    );

    expect(await screen.findByText('Akwaaba Technical Services Ltd')).toBeInTheDocument();
    expect(screen.getAllByText('Akwaaba Technical Services Ltd')).toHaveLength(1);
    expect(screen.getByRole('link', { name: 'Review & decide' })).toHaveAttribute(
      'href', '/procurement/business-partners/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'
    );
  });
});
