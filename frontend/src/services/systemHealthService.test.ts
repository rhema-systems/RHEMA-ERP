import { afterEach, describe, expect, it, vi } from 'vitest';
import { systemHealthService, type SystemHealthSnapshot } from './systemHealthService';

const snapshot: SystemHealthSnapshot = {
  status: 'Healthy',
  observedAtUtc: '2026-10-02T15:00:00Z',
  durationMilliseconds: 12,
  checks: [
    {
      name: 'database',
      status: 'Healthy',
      description: 'Database connection succeeded.',
      durationMilliseconds: 8,
      tags: ['ready'],
    },
  ],
};

describe('systemHealthService', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('loads the live readiness response without using a cached result', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      json: vi.fn().mockResolvedValue(snapshot),
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(systemHealthService.getReadiness()).resolves.toEqual(snapshot);
    expect(fetchMock).toHaveBeenCalledWith('/api/health/ready', {
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    });
  });

  it('returns a structured unhealthy snapshot from a 503 response', async () => {
    const unhealthy = { ...snapshot, status: 'Unhealthy' };
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: false,
      json: vi.fn().mockResolvedValue(unhealthy),
    }));

    await expect(systemHealthService.getReadiness()).resolves.toEqual(unhealthy);
  });
});
