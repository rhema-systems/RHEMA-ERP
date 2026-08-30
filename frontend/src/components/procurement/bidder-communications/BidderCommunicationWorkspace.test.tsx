import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  hasPermission: vi.fn(),
  invalidateQueries: vi.fn(),
  refetch: vi.fn(),
  overview: {
    data: undefined as unknown,
    isLoading: false,
    isFetching: false,
    isError: true,
    error: Object.assign(new Error('Not found'), { status: 404 }),
  },
  awardReadiness: {
    data: {
      id: 'decision-1',
      decisionSequence: 4,
      sourceReference: 'TDR-001',
      isReady: true,
      isCurrent: true,
      integrityHash: 'a'.repeat(64),
    } as unknown,
    isLoading: false,
    isFetching: false,
    isError: false,
    error: undefined as unknown,
  },
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: mocks.hasPermission }),
}));

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({
    invalidateQueries: mocks.invalidateQueries,
  }),
  useQuery: ({ queryKey }: { queryKey: string[] }) => ({
    ...(queryKey[0] ===
    'procurement-bidder-communications-award-readiness'
      ? mocks.awardReadiness
      : mocks.overview),
    refetch: mocks.refetch,
  }),
}));

import { BidderCommunicationWorkspace } from './BidderCommunicationWorkspace';

describe('bidder-communication workspace authorization', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.hasPermission.mockReturnValue(false);
    mocks.overview.data = undefined;
    mocks.overview.isError = true;
    mocks.overview.error = Object.assign(new Error('Not found'), {
      status: 404,
    });
  });

  it('does not expose initialization to a read-only actor', () => {
    render(
      <BidderCommunicationWorkspace sourceType="Tender" sourceId="tender-1" />
    );

    expect(mocks.hasPermission).toHaveBeenCalledWith(
      'procurement.tender.administer'
    );
    expect(
      screen.queryByRole('button', {
        name: 'Initialize communication register',
      })
    ).not.toBeInTheDocument();
    expect(screen.getByText('Initialization is not available')).toBeVisible();
  });

  it('offers initialization only when current readiness and permission agree', () => {
    mocks.hasPermission.mockImplementation(
      (permission: string) => permission === 'procurement.tender.administer'
    );

    render(
      <BidderCommunicationWorkspace sourceType="Tender" sourceId="tender-1" />
    );

    expect(
      screen.getByRole('button', {
        name: 'Initialize communication register',
      })
    ).toBeVisible();
    expect(
      screen.getByText(/decision #4 is current and Ready/)
    ).toBeVisible();
  });

  it('fails closed when external supplier scope cannot be loaded', () => {
    mocks.overview.error = Object.assign(new Error('Forbidden'), {
      status: 403,
    });

    render(
      <BidderCommunicationWorkspace
        sourceType="Tender"
        sourceId="tender-1"
        external
      />
    );

    expect(
      screen.getByTestId('bidder-communication-unavailable')
    ).toHaveTextContent('Access remains fail closed');
    expect(
      screen.queryByTestId('external-bidder-communication-status')
    ).not.toBeInTheDocument();
  });

  it('shows a neutral pending state when an award communication is not published yet', () => {
    render(
      <BidderCommunicationWorkspace
        sourceType="RequestForQuotation"
        sourceId="rfq-1"
        external
      />
    );

    expect(
      screen.getByTestId('external-bidder-communication-pending')
    ).toHaveTextContent('Award result pending');
    expect(screen.getByText(/No award communication has been published/)).toBeVisible();
    expect(
      screen.queryByTestId('bidder-communication-unavailable')
    ).not.toBeInTheDocument();
  });
});
