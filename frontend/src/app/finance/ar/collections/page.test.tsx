import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ArCollectionsPage from './page';

const serviceMock = vi.hoisted(() => ({
  getWorkQueue: vi.fn(),
  getSummary: vi.fn(),
  getAssignees: vi.fn(),
  generateTasks: vi.fn(),
  createTask: vi.fn(),
  updateTask: vi.fn(),
  recordReminder: vi.fn(),
  getHistory: vi.fn(),
}));
const toastMock = vi.hoisted(() => vi.fn());

vi.mock('@/services/finance/ar-collection-data.service', () => ({
  arCollectionDataService: serviceMock,
}));
vi.mock('@/hooks/use-toast', () => ({ useToast: () => ({ toast: toastMock }) }));
vi.mock('@/hooks/use-auth', () => ({ useAuth: () => ({ hasPermission: () => true }) }));

const summary = {
  asOfDate: '2026-09-02',
  overdueExposureCount: 2,
  unassignedExposureCount: 1,
  overdueFollowUpCount: 0,
  promiseToPayCount: 0,
  breachedPromiseCount: 0,
  nativeCurrencyTotals: [
    { currencyCode: 'GHS', outstandingAmount: 300_000, promisedAmount: 25_000 },
    { currencyCode: 'USD', outstandingAmount: 70_000, promisedAmount: 0 },
  ],
  functionalCurrencyCode: 'GHS',
  functionalOutstandingTotal: 1_000_000,
  functionalPromisedTotal: 25_000,
  functionalTotalBasis: 'Historical posting and settlement carrying basis',
};

const unresolvedRow = {
  settlementBalanceId: 'balance-1',
  customerId: 'partner-1',
  customerCode: '',
  customerName: 'Unresolved business partner',
  isPartnerResolved: false,
  partnerResolutionMessage: 'The stored business partner could not be resolved for this tenant.',
  invoiceId: 'invoice-1',
  invoiceNumber: 'INV-USD-001',
  transactionDate: '2026-07-01',
  dueDate: '2026-08-01',
  currencyCode: 'USD',
  outstandingAmount: 70_000,
  daysOverdue: 32,
  agingBucket: '31-60',
  taskReference: 'COL-001',
  taskStatus: 'Pending' as const,
  priority: 5,
  promisedAmount: 0,
  isFollowUpOverdue: false,
  isPromiseBreached: false,
};

function arrangeSuccess(items = [unresolvedRow], totals: unknown = summary) {
  serviceMock.getWorkQueue.mockResolvedValue({
    asOfDate: '2026-09-02', page: 1, pageSize: 25, totalCount: items.length,
    totalPages: items.length ? 1 : 0, items,
  });
  serviceMock.getSummary.mockResolvedValue(totals);
  serviceMock.getAssignees.mockResolvedValue([{ userId: 'officer-1', displayName: 'Collection Officer' }]);
}

describe('AR Collections workspace states and currency presentation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows a distinct loading state without rendering fallback zero totals', () => {
    serviceMock.getWorkQueue.mockReturnValue(new Promise(() => undefined));
    serviceMock.getSummary.mockReturnValue(new Promise(() => undefined));
    serviceMock.getAssignees.mockReturnValue(new Promise(() => undefined));

    render(<ArCollectionsPage />);

    expect(screen.getByTestId('collection-loading')).toBeInTheDocument();
    expect(screen.queryByText(/GHS\s*0\.00/)).not.toBeInTheDocument();
    expect(screen.queryByText('No overdue exposure matches these filters.')).not.toBeInTheDocument();
  });

  it('shows an explicit request error and Retry instead of a false empty workspace', async () => {
    serviceMock.getWorkQueue.mockRejectedValue(new Error('Reference ID: AR-COL-500'));
    serviceMock.getSummary.mockResolvedValue(summary);
    serviceMock.getAssignees.mockResolvedValue([]);

    render(<ArCollectionsPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Reference ID: AR-COL-500');
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
    expect(screen.queryByText('No overdue exposure matches these filters.')).not.toBeInTheDocument();
    expect(screen.queryByText('Overdue exposures')).not.toBeInTheDocument();
  });

  it('retries a failed load and then renders the successful workspace', async () => {
    serviceMock.getWorkQueue
      .mockRejectedValueOnce(new Error('Temporary failure'))
      .mockResolvedValueOnce({ asOfDate: '2026-09-02', page: 1, pageSize: 25, totalCount: 1, totalPages: 1, items: [unresolvedRow] });
    serviceMock.getSummary.mockResolvedValue(summary);
    serviceMock.getAssignees.mockResolvedValue([]);
    render(<ArCollectionsPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('INV-USD-001')).toBeInTheDocument();
    expect(serviceMock.getWorkQueue).toHaveBeenCalledTimes(2);
  });

  it('renders native totals separately, labels functional evidence, and keeps unresolved rows readable', async () => {
    arrangeSuccess();
    render(<ArCollectionsPage />);

    expect(await screen.findByText('INV-USD-001')).toBeInTheDocument();
    expect(screen.getByText('Native outstanding by currency')).toBeInTheDocument();
    expect(screen.getAllByText(/GH.300,000\.00/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/US.70,000\.00/).length).toBeGreaterThan(0);
    expect(screen.getByText('Functional-currency outstanding')).toBeInTheDocument();
    expect(screen.getByText('Historical posting and settlement carrying basis')).toBeInTheDocument();
    expect(screen.getByText('Unresolved partner')).toBeInTheDocument();
    expect(screen.getByText(unresolvedRow.partnerResolutionMessage)).toBeInTheDocument();
  });

  it('renders a genuine empty response as an empty state', async () => {
    arrangeSuccess([], { ...summary, overdueExposureCount: 0, nativeCurrencyTotals: [], functionalOutstandingTotal: undefined, functionalCurrencyCode: undefined, functionalTotalUnavailableReason: 'No overdue exposure.' });
    render(<ArCollectionsPage />);

    expect(await screen.findByText('No overdue exposure matches these filters.')).toBeInTheDocument();
    expect(screen.getByText('No overdue native-currency exposure.')).toBeInTheDocument();
  });

  it('makes generation assignment explicit and defaults it to the current operator', async () => {
    arrangeSuccess();
    serviceMock.generateTasks.mockResolvedValue({
      eligibleCount: 1, createdCount: 1, existingCount: 0, refreshedCount: 0,
      autoResolvedCount: 0, reactivatedCount: 0, skippedUnresolvedPartnerCount: 0,
    });
    render(<ArCollectionsPage />);

    fireEvent.click(await screen.findByRole('button', { name: 'Generate follow-up tasks' }));
    expect(screen.getByText('Assign generated tasks to')).toBeInTheDocument();
    expect(screen.getByText(/not driven by an approval workflow/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Generate tasks' }));

    await waitFor(() => expect(serviceMock.generateTasks).toHaveBeenCalledWith(expect.objectContaining({ assignedToId: undefined })));
  });
});
