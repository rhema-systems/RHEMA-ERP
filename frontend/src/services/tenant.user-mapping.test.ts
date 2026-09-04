import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  addUserToTenant: vi.fn(),
  removeUserFromTenant: vi.fn(),
}));

vi.mock('./api.service', () => ({ apiService: api }));

import { TenantService } from './tenant';

describe('tenant user-mapping client', () => {
  const service = new TenantService();

  beforeEach(() => vi.clearAllMocks());

  it('waits for the authorized API grant and returns its persisted mapping', async () => {
    const request = {
      userId: 'user-1',
      tenantId: 'tenant-1',
      expiresAt: '',
    };
    const persisted = { userId: 'user-1', tenantId: 'tenant-1', isActive: true };
    api.addUserToTenant.mockResolvedValue(persisted);

    await expect(service.addUserToTenant(request)).resolves.toBe(persisted);

    expect(api.addUserToTenant).toHaveBeenCalledWith({
      ...request,
      expiresAt: null,
    });
  });

  it('propagates grant failures so the page cannot show false success', async () => {
    const failure = new Error('Tenant mapping was rejected');
    api.addUserToTenant.mockRejectedValue(failure);

    await expect(service.addUserToTenant({ userId: 'user-1', tenantId: 'tenant-1' }))
      .rejects.toBe(failure);
  });

  it('waits for the authorized API revoke and propagates failures', async () => {
    api.removeUserFromTenant.mockResolvedValue(undefined);
    await expect(service.removeUserFromTenant('user-1', 'tenant-1')).resolves.toBeUndefined();
    expect(api.removeUserFromTenant).toHaveBeenCalledWith('user-1', 'tenant-1');

    const failure = new Error('Tenant revoke was rejected');
    api.removeUserFromTenant.mockRejectedValue(failure);
    await expect(service.removeUserFromTenant('user-1', 'tenant-1')).rejects.toBe(failure);
  });
});
