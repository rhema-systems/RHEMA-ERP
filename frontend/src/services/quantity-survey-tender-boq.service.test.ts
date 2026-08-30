import { beforeEach, describe, expect, it, vi } from 'vitest';

const apiMocks = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  downloadBlob: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({
  apiService: apiMocks,
}));

import { quantitySurveyTenderBoqService } from './quantity-survey-tender-boq.service';

describe('supplier tender BoQ client routes', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('loads the supplier-owned context and latest submission from the bid-scoped external route', async () => {
    apiMocks.get.mockResolvedValueOnce({ tenderBidId: 'bid/one' });
    apiMocks.get.mockResolvedValueOnce(undefined);

    await quantitySurveyTenderBoqService.context('bid/one');
    await quantitySurveyTenderBoqService.latest('bid/one');

    expect(apiMocks.get).toHaveBeenNthCalledWith(
      1,
      '/external-portal/tender-bids/bid%2Fone/quantity-survey-boq/context'
    );
    expect(apiMocks.get).toHaveBeenNthCalledWith(
      2,
      '/external-portal/tender-bids/bid%2Fone/quantity-survey-boq/submissions/latest'
    );
  });

  it('keeps internal submission history on the permission-protected internal route', async () => {
    apiMocks.get.mockResolvedValueOnce([]);

    await quantitySurveyTenderBoqService.history('bid/one');

    expect(apiMocks.get).toHaveBeenCalledWith(
      '/quantity-survey/tender-bids/bid%2Fone/boq-submissions'
    );
  });
});
