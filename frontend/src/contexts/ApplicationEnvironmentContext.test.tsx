import React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { applicationEnvironmentService } from '../services/application-environment';
import {
  ApplicationEnvironmentProvider,
  getEnvironmentDocumentTitle,
  useApplicationEnvironment,
} from './ApplicationEnvironmentContext';

function Consumer({ name }: { name: string }) {
  const { environment } = useApplicationEnvironment();
  return <span>{name}: {environment.environment}</span>;
}

describe('ApplicationEnvironmentProvider', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('loads the public descriptor once for all consumers and updates the browser title', async () => {
    const getEnvironment = vi.spyOn(applicationEnvironmentService, 'getPublicEnvironment')
      .mockResolvedValue({
        environment: 'UAT',
        isProduction: false,
        displayName: 'UAT Environment',
        message: 'For acceptance testing',
        configurationValid: true,
        dataIsolationConfirmed: true,
        applicationVersion: '2026.10.05',
        buildId: 'a8f27c1',
        deployedAtUtc: null,
      });
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={queryClient}>
        <ApplicationEnvironmentProvider>
          <Consumer name="first" />
          <Consumer name="second" />
        </ApplicationEnvironmentProvider>
      </QueryClientProvider>,
    );

    await screen.findByText('first: UAT');
    expect(screen.getByText('second: UAT')).toBeInTheDocument();
    expect(getEnvironment).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(document.title).toBe('RHEMA-ERP [UAT]'));
  });

  it.each([
    ['Production', 'RHEMA-ERP'],
    ['Test', 'RHEMA-ERP [TEST]'],
    ['UAT', 'RHEMA-ERP [UAT]'],
    ['Staging', 'RHEMA-ERP [STAGING]'],
    ['Development', 'RHEMA-ERP [DEVELOPMENT]'],
    ['Unknown', 'RHEMA-ERP [UNKNOWN]'],
  ] as const)('uses an environment-specific title for %s', (environment, title) => {
    expect(getEnvironmentDocumentTitle(environment)).toBe(title);
  });
});
