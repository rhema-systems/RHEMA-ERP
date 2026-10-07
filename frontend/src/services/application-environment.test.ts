import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ publicRequest: vi.fn() }));

vi.mock('./api.service', () => ({
  apiService: { publicRequest: mocks.publicRequest },
}));

import {
  applicationEnvironmentService,
  normalizeApplicationEnvironment,
  UNKNOWN_APPLICATION_ENVIRONMENT,
} from './application-environment';

describe('applicationEnvironmentService', () => {
  beforeEach(() => vi.clearAllMocks());

  it.each(['Production', 'Test', 'UAT', 'Staging', 'Development'] as const)(
    'accepts the supported %s environment',
    (environment) => {
      expect(normalizeApplicationEnvironment({
        environment,
        configurationValid: true,
        isProduction: environment === 'Production',
        displayName: `${environment} Environment`,
        message: `${environment} message`,
        dataIsolationConfirmed: false,
        applicationVersion: '2026.10.05',
        buildId: 'a8f27c1',
        deployedAtUtc: '2026-10-05T18:42:00Z',
      })).toMatchObject({ environment, isProduction: environment === 'Production' });
    },
  );

  it.each([undefined, null, {}, { environment: 'QA', configurationValid: true }, { environment: 'Production', configurationValid: false }])(
    'uses the visibly unsafe fallback for invalid response %j',
    (response) => {
      expect(normalizeApplicationEnvironment(response)).toEqual(UNKNOWN_APPLICATION_ENVIRONMENT);
    },
  );

  it('reads only the safe descriptor fields from the anonymous endpoint', async () => {
    mocks.publicRequest.mockResolvedValue({
      environment: 'UAT',
      configurationValid: true,
      displayName: 'UAT Environment',
      message: 'For acceptance testing',
      applicationVersion: '2026.10.05',
      buildId: 'a8f27c1',
      deployedAtUtc: null,
      connectionString: 'must-not-flow-to-the-client-context',
      secretKey: 'must-not-flow-to-the-client-context',
    });

    const descriptor = await applicationEnvironmentService.getPublicEnvironment();

    expect(mocks.publicRequest).toHaveBeenCalledWith('/public/config/environment', {
      method: 'GET',
      cache: 'no-store',
    });
    expect(descriptor).not.toHaveProperty('connectionString');
    expect(descriptor).not.toHaveProperty('secretKey');
  });

  it('never falls back to Production when the endpoint fails', async () => {
    mocks.publicRequest.mockRejectedValue(new Error('offline'));
    await expect(applicationEnvironmentService.getPublicEnvironment())
      .resolves.toEqual(UNKNOWN_APPLICATION_ENVIRONMENT);
  });
});
