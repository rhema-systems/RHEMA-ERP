import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  downloadBlob: vi.fn(),
}));

vi.mock('@/services/api.service', () => ({ apiService: api }));

import { quantitySurveyPriceIndexImportService as service } from './quantity-survey-price-index-import.service';

describe('quantity survey price-index import API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('stages the controlled source as multipart with idempotency and authority', async () => {
    const file = new File(['Period,IndexValue'], 'gss-index.csv', {
      type: 'text/csv',
    });

    await service.stage(
      'family-1',
      file,
      'role-1',
      'GSS bulletin published.',
      'request-1'
    );

    const [url, body] = api.post.mock.calls[0];
    expect(url).toBe('/quantity-survey/price-index-imports/stage/family-1');
    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).get('file')).toBe(file);
    expect((body as FormData).get('clientRequestId')).toBe('request-1');
    expect((body as FormData).get('authorityRoleId')).toBe('role-1');
    expect((body as FormData).get('reason')).toBe('GSS bulletin published.');
  });

  it('uses separate read audit and maker-checker lifecycle endpoints', async () => {
    await service.list({ indexFamilyId: 'family-1', status: 'Approved' });
    await service.history('import-1');
    await service.lifecycle(
      'import-1',
      'approve',
      'row-version',
      'Independent review complete.'
    );

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/price-index-imports',
      { indexFamilyId: 'family-1', status: 'Approved' }
    );
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/price-index-imports/import-1/history'
    );
    expect(api.post).toHaveBeenCalledWith(
      '/quantity-survey/price-index-imports/import-1/approve',
      {
        rowVersion: 'row-version',
        reason: 'Independent review complete.',
      }
    );
  });

  it('downloads a family-scoped template', async () => {
    await service.template('family / one');

    expect(api.downloadBlob).toHaveBeenCalledWith(
      '/quantity-survey/price-index-imports/template/family%20%2F%20one'
    );
  });
});
