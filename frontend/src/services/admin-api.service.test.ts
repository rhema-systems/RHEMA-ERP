import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ request: vi.fn() }));

vi.mock('./api.service', () => ({ apiService: api }));

import { adminApiService, getAdminProblemMessage } from './admin-api.service';

describe('admin temporary-password API', () => {
  beforeEach(() => vi.clearAllMocks());

  it('posts only the supplied temporary password and audit reason', async () => {
    api.request.mockResolvedValue(undefined);

    await adminApiService.resetUserPassword('user/id', {
      newPassword: 'StrongTemporary1!',
      reason: 'User requested account recovery.',
    });

    expect(api.request).toHaveBeenCalledWith(
      '/user/user%2Fid/reset-password',
      {
        method: 'POST',
        body: JSON.stringify({
          newPassword: 'StrongTemporary1!',
          reason: 'User requested account recovery.',
        }),
      }
    );
  });

  it('surfaces ProblemDetails detail and code', async () => {
    const error = Object.assign(new Error('generic failure'), {
      response: {
        title: 'Password reset rejected',
        detail: 'The password was used recently.',
        extensions: { code: 'USER_PASSWORD_REUSED' },
      },
    });
    api.request.mockRejectedValue(error);

    await expect(
      adminApiService.resetUserPassword('user-1', {
        newPassword: 'StrongTemporary1!',
        reason: 'User requested account recovery.',
      })
    ).rejects.toThrow(
      'The password was used recently. (USER_PASSWORD_REUSED)'
    );
    expect(
      getAdminProblemMessage(error, 'Fallback')
    ).toBe('The password was used recently. (USER_PASSWORD_REUSED)');
  });
});
