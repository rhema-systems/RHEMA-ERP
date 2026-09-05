import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { civilEngineeringPermittingEngineeringReviewService } from './civil-engineering-permitting-engineering-review.service';

describe('civilEngineeringPermittingEngineeringReviewService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the development-file-scoped engineering-review routes', () => {
    const request = { clientRequestId: 'request', commentCategoryId: 'category', reviewComment: 'Drawing dimensions verified.', recommendedOutcome: 'RecommendApproval' as const, centralDocumentRecordId: 'record', centralDocumentVersionId: 'version' };
    civilEngineeringPermittingEngineeringReviewService.lookups('file');
    civilEngineeringPermittingEngineeringReviewService.list('file');
    civilEngineeringPermittingEngineeringReviewService.submit('file', request);
    expect(api.get).toHaveBeenNthCalledWith(1, '/projects/civil-engineering/development-approval-files/file/engineering-reviews/lookups');
    expect(api.get).toHaveBeenNthCalledWith(2, '/projects/civil-engineering/development-approval-files/file/engineering-reviews');
    expect(api.post).toHaveBeenCalledWith('/projects/civil-engineering/development-approval-files/file/engineering-reviews', request);
  });
});
