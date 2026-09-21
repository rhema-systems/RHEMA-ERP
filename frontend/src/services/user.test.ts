import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({ directory: vi.fn(), request: vi.fn() }));
vi.mock('./workflow-api.service', () => ({ workflowApiService: { getWorkflowDirectoryUsers: mocks.directory } }));
vi.mock('./api.service', () => ({ apiService: { request: mocks.request } }));
import { userService } from './user';

describe('project user selection', () => {
  beforeEach(() => vi.clearAllMocks());
  it('uses the tenant-scoped active-user directory without requiring user administration', async () => {
    mocks.directory.mockResolvedValue([{ id: 'reviewer', userName: 'qs.reviewer', firstName: 'QS', lastName: 'Reviewer' }]);
    const users = await userService.searchAssignableUsers('QS');
    expect(mocks.directory).toHaveBeenCalledWith('QS');
    expect(mocks.request).not.toHaveBeenCalled();
    expect(users).toEqual([{ id: 'reviewer', username: 'qs.reviewer', firstName: 'QS', lastName: 'Reviewer', email: '', isActive: true, roles: [] }]);
  });
});
