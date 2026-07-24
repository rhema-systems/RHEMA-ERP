import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  hasPermission: vi.fn(),
  invalidateQueries: vi.fn(),
  mutate: vi.fn(),
}));

vi.mock('@/hooks/use-auth', () => ({
  useAuth: () => ({ hasPermission: mocks.hasPermission }),
}));

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({
    invalidateQueries: mocks.invalidateQueries,
    setQueryData: vi.fn(),
  }),
  useMutation: () => ({
    isPending: false,
    mutate: mocks.mutate,
  }),
  useQuery: ({ queryKey }: { queryKey: string[] }) => {
    const lane = queryKey.at(-1);
    if (lane === 'history')
      return {
        data: [],
        isLoading: false,
        isFetching: false,
        isError: false,
      };
    if (lane === 'sod-status')
      return {
        data: {
          sourceType: 'Tender',
          sourceId: 'tender-1',
          sourceReference: 'TDR-001',
          allowed: true,
          code: 'SOD_ALLOWED',
          message: 'The current actor is not in the evaluator lineage.',
          currentActorUserId: 'reader-1',
          currentActorName: 'Read-only User',
          currentActorRoles: ['ProcurementReader'],
          evaluatorUserIds: ['evaluator-1'],
          independentApprovalActorUserIds: [],
          evaluatorLineage: [],
          sodDecisionId: 'sod-decision-1',
          sodControlCode: 'SOD-EVALUATOR-AWARD-APPROVER',
          correlationId: 'correlation-1',
          evaluatedAtUtc: '2026-07-24T12:00:00Z',
        },
        isLoading: false,
        isFetching: false,
        isError: false,
      };
    return {
      data: undefined,
      isLoading: false,
      isFetching: false,
      isError: false,
      error: undefined,
    };
  },
}));

import { AwardReadinessWorkspace } from './AwardReadinessWorkspace';

describe('award-readiness workspace authorization', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.hasPermission.mockReturnValue(false);
  });

  it('does not expose Evaluate to a read-only actor', () => {
    render(
      <AwardReadinessWorkspace sourceType="Tender" sourceId="tender-1" />
    );

    expect(mocks.hasPermission).toHaveBeenCalledWith(
      'procurement.tender.approve'
    );
    expect(
      screen.queryByRole('button', { name: 'Evaluate award readiness' })
    ).not.toBeInTheDocument();
    expect(
      screen.getByText('No award-readiness decision has been retained')
    ).toBeInTheDocument();
  });
});
