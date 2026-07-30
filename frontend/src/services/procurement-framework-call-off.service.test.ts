import { beforeEach, describe, expect, it, vi } from 'vitest';

import { apiService } from '@/services/api.service';
import { procurementFrameworkCallOffService as service } from './procurement-framework-call-off.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('procurementFrameworkCallOffService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated tenant-safe register and options endpoints', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ items: [] });

    await service.search({ status: 'PendingApproval', agreementId: 'agreement-1' });
    await service.options();

    expect(apiService.get).toHaveBeenNthCalledWith(
      1,
      '/procurement/framework-call-offs',
      { status: 'PendingApproval', agreementId: 'agreement-1' }
    );
    expect(apiService.get).toHaveBeenNthCalledWith(
      2,
      '/procurement/framework-call-offs/options'
    );
  });

  it('keeps approval and issue as separate shared-control transitions', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'call-off-1' });
    const request = {
      rowVersion: 'row-version',
      comment: 'Workflow evidence retained.',
      evidence: [
        {
          referenceKind: 'ExternalReference' as const,
          reference: 'MINUTE-0402',
        },
      ],
    };

    await service.decide('call-off-1', { ...request, approved: true });
    await service.issue('call-off-1', request);

    expect(apiService.post).toHaveBeenNthCalledWith(
      1,
      '/procurement/framework-call-offs/call-off-1/decision',
      expect.objectContaining({ approved: true })
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      2,
      '/procurement/framework-call-offs/call-off-1/issue',
      request
    );
  });
});
