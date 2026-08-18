import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import {
  QuantitySurveyHistoricalRateSourceType,
  QuantitySurveyRateBuildUpCalculationMethod,
  QuantitySurveyRateComponent,
  quantitySurveyRateLibraryService as service,
} from './quantity-survey-rate-library.service';

describe('quantity survey rate-library API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the governed historical-source query and promotion routes', async () => {
    await service.historicalSources(
      'item-1',
      QuantitySurveyHistoricalRateSourceType.ActualProjectCost,
      'project 2026'
    );
    await service.prepareHistoricalRate('item-1', {
      sourceType: QuantitySurveyHistoricalRateSourceType.ActualProjectCost,
      sourceId: 'cost-1',
      sourceIntegrityHash: 'A'.repeat(64),
      effectiveFrom: '2026-08-08',
      effectiveTo: null,
      projectTypeId: 'project-type-1',
      locationId: 'location-1',
      centralDocumentVersionId: 'document-version-1',
      changeReason: 'Promote verified final project cost.',
    });

    expect(api.get).toHaveBeenCalledWith(
      '/quantity-survey/rate-library/item-1/historical-sources',
      {
        sourceType: QuantitySurveyHistoricalRateSourceType.ActualProjectCost,
        search: 'project 2026',
      }
    );
    expect(api.post).toHaveBeenCalledWith(
      '/quantity-survey/rate-library/item-1/historical-rate-promotions',
      expect.objectContaining({
        sourceId: 'cost-1',
        sourceIntegrityHash: 'A'.repeat(64),
        projectTypeId: 'project-type-1',
        locationId: 'location-1',
        centralDocumentVersionId: 'document-version-1',
      })
    );
  });

  it('uses governed preview and idempotent prepare routes for rate build-ups', async () => {
    const preview = {
      sourceDate: '2026-08-08',
      currencyId: 'currency-1',
      effectiveFrom: '2026-08-08',
      lines: [
        {
          sequence: 1,
          component: QuantitySurveyRateComponent.Material,
          calculationMethod:
            QuantitySurveyRateBuildUpCalculationMethod.QuantityTimesPublishedRate,
          sourceRateId: 'rate-1',
          quantity: 2,
        },
      ],
    };

    await service.rateBuildUpContext('item-1', {
      sourceDate: '2026-08-08',
      effectiveAt: '2026-08-08',
      currencyId: 'currency-1',
    });
    await service.previewRateBuildUp('item-1', preview);
    await service.prepareRateBuildUp('item-1', {
      ...preview,
      clientRequestId: 'request-1',
      previewIntegrityHash: 'A'.repeat(64),
      changeReason: 'Prepare governed rate build-up.',
    });

    expect(api.get).toHaveBeenCalledWith(
      '/quantity-survey/rate-library/item-1/rate-build-up-context',
      {
        sourceDate: '2026-08-08',
        effectiveAt: '2026-08-08',
        currencyId: 'currency-1',
      }
    );
    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/rate-library/item-1/rate-build-ups/preview',
      preview
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/rate-library/item-1/rate-build-ups',
      expect.objectContaining({
        clientRequestId: 'request-1',
        previewIntegrityHash: 'A'.repeat(64),
      })
    );
  });
});
