import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from './api.service';
import { arService } from './ar-service';

vi.mock('./api.service', () => ({
  apiService: {
    get: vi.fn(),
  },
}));

describe('accounts receivable payment queries', () => {
  beforeEach(() => vi.clearAllMocks());

  it('requests only posted, unapplied customer advances for payment-on-account selection', async () => {
    vi.mocked(apiService.get).mockResolvedValueOnce({ items: [] });

    await arService.getPayments({
      businessPartnerId: 'customer-1',
      status: 'Posted',
      hasUnallocatedAmount: true,
      isCustomerAdvance: true,
      page: 1,
      pageSize: 100,
    });

    const url = vi.mocked(apiService.get).mock.calls[0][0] as string;
    expect(url).toContain('BusinessPartnerId=customer-1');
    expect(url).toContain('Status=Posted');
    expect(url).toContain('HasUnallocatedAmount=true');
    expect(url).toContain('IsCustomerAdvance=true');
  });
});
