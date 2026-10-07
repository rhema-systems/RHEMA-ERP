import React from 'react';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const state = vi.hoisted(() => ({ environment: 'Production' }));

vi.mock('../../contexts/ApplicationEnvironmentContext', () => ({
  useApplicationEnvironment: () => ({
    environment: {
      environment: state.environment,
      isProduction: state.environment === 'Production',
      displayName: `${state.environment} Environment`,
      message: `${state.environment} safety message`,
      configurationValid: state.environment !== 'Unknown',
      dataIsolationConfirmed: false,
      applicationVersion: '1.0.0',
      buildId: null,
      deployedAtUtc: null,
    },
  }),
}));

import { EnvironmentBadge } from './EnvironmentBadge';
import { EnvironmentBanner } from './EnvironmentBanner';

describe('environment indicators', () => {
  beforeEach(() => { state.environment = 'Production'; });

  it('keeps Production free of the warning banner while retaining a subtle badge', () => {
    render(<><EnvironmentBanner /><EnvironmentBadge /></>);
    expect(screen.queryByTestId('environment-banner')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Environment: Production')).toBeInTheDocument();
  });

  it.each([
    ['Test', 'Test Environment'],
    ['UAT', 'UAT Environment'],
    ['Staging', 'Staging Environment'],
    ['Development', 'Development'],
    ['Unknown', 'Unknown Environment'],
  ])('shows a persistent textual indicator for %s', (environment, label) => {
    state.environment = environment;
    render(<EnvironmentBanner />);
    const banner = screen.getByTestId('environment-banner');
    expect(banner).toHaveAttribute('data-environment', environment);
    expect(banner).toHaveTextContent(label);
    expect(banner.className).toContain('dark:');
  });
});
