import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  hasPermission: vi.fn(),
  invalidateQueries: vi.fn(),
  refetch: vi.fn(),
  overview: {
    data: {
      sourceType: 'Tender',
      sourceId: 'tender-1',
      sourceReference: 'TDR-001',
      sourceVariant: 'NCT',
      events: [],
    },
    isLoading: false,
    isFetching: false,
    isError: false,
    error: undefined as unknown,
  },
  options: {
    data: {
      isConfigured: true,
      configurationMessage: undefined as string | undefined,
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
      reconciliationRule: 'Match exact reference and checksum.',
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
      allowedActions: ['PrepareExport'],
      blockedReasons: [],
    },
    isLoading: false,
    isFetching: false,
    isError: false,
    error: undefined as unknown,
  },
  status: {
    data: {
      sourceType: 'Tender',
      sourceId: 'tender-1',
      sourceReference: 'TDR-001',
      sourceVariant: 'NCT',
      events: [],
    },
    isLoading: false,
    isFetching: false,
    isError: false,
    error: undefined as unknown,
  },
  history: {
    data: [],
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
    ...mocks[queryKey.at(-1) as 'overview' | 'options' | 'status' | 'history'],
    refetch: mocks.refetch,
  }),
}));

vi.mock('./GhanepsExchangeRegister', () => ({
  GhanepsExchangeRegister: ({
    canManage,
    hasConfiguredPermission,
  }: {
    canManage: boolean;
    hasConfiguredPermission: (permissionCode: string) => boolean;
  }) =>
    React.createElement(
      'div',
      { 'data-testid': 'mock-ghaneps-register' },
      `manage:${canManage}|ack:${hasConfiguredPermission(
        'procurement.tender.approve'
      )}|reconcile:${hasConfiguredPermission(
        'procurement.tender.reconcile'
      )}`
    ),
}));

vi.mock('./GhanepsExchangeActionDialog', () => ({
  GhanepsExchangeActionDialog: () => null,
}));

import { GhanepsExchangeWorkspace } from './GhanepsExchangeWorkspace';

describe('GHANEPS exchange workspace fail-closed states', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.hasPermission.mockReturnValue(false);
    mocks.overview.isLoading = false;
    mocks.options.isLoading = false;
    mocks.options.data.isConfigured = true;
    mocks.options.data.configurationMessage = undefined;
    mocks.status.isLoading = false;
    mocks.history.isLoading = false;
    mocks.options.data.mappings = [
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
    ];
  });

  it('renders a bounded loading state until every authoritative read completes', () => {
    mocks.options.isLoading = true;

    render(
      <GhanepsExchangeWorkspace sourceType="Tender" sourceId="tender-1" />
    );

    expect(screen.getByTestId('ghaneps-exchange-loading')).toBeVisible();
    expect(
      screen.queryByTestId('mock-ghaneps-register')
    ).not.toBeInTheDocument();
  });

  it('shows an advisory when GHANEPS is not configured', () => {
    mocks.options.data.isConfigured = false;
    mocks.options.data.configurationMessage =
      'GHANEPS exchange is optional and has not been configured for this tenant.';
    mocks.options.data.mappings = [];

    render(
      <GhanepsExchangeWorkspace sourceType="Tender" sourceId="tender-1" />
    );

    expect(
      screen.getByTestId('ghaneps-exchange-not-configured')
    ).toHaveTextContent(
      'GHANEPS exchange is optional and has not been configured for this tenant.'
    );
    expect(
      screen.queryByTestId('mock-ghaneps-register')
    ).not.toBeInTheDocument();
  });

  it('passes the client permission evaluator separately from server action gates', () => {
    render(
      <GhanepsExchangeWorkspace sourceType="Tender" sourceId="tender-1" />
    );

    expect(mocks.hasPermission).toHaveBeenCalledWith(
      'procurement.tender.administer'
    );
    expect(mocks.hasPermission).toHaveBeenCalledWith(
      'procurement.tender.approve'
    );
    expect(mocks.hasPermission).toHaveBeenCalledWith(
      'procurement.tender.reconcile'
    );
    expect(screen.getByTestId('mock-ghaneps-register')).toHaveTextContent(
      'manage:false|ack:false|reconcile:false'
    );
  });
});
