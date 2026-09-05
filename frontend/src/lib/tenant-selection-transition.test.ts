import { afterEach, describe, expect, it, vi } from 'vitest';

import { completeTenantSelectionTransition } from './tenant-selection-transition';

describe('completeTenantSelectionTransition', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('commits tenant state, drains the old query cache, and performs a hard replace', async () => {
    const calls: string[] = [];

    await completeTenantSelectionTransition({
      tenantCode: 'TDC',
      target: '/dashboard',
      setCurrentTenantCode: (code) => calls.push(`tenant:${code}`),
      cancelQueries: async () => {
        calls.push('cancel');
      },
      removeQueries: () => calls.push('remove'),
      replaceLocation: (target) => calls.push(`replace:${target}`),
    });

    expect(calls).toEqual([
      'tenant:TDC',
      'cancel',
      'remove',
      'replace:/dashboard',
    ]);
  });

  it('does not leave the selection screen stuck when cache cleanup fails', async () => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    const replaceLocation = vi.fn();

    await completeTenantSelectionTransition({
      tenantCode: 'TDC',
      target: '/dashboard',
      setCurrentTenantCode: vi.fn(),
      cancelQueries: () => Promise.reject(new Error('cancel failed')),
      removeQueries: () => {
        throw new Error('remove failed');
      },
      replaceLocation,
    });

    expect(replaceLocation).toHaveBeenCalledWith('/dashboard');
  });
});
