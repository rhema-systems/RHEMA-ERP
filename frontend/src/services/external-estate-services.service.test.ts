import { beforeEach, describe, expect, it, vi } from 'vitest';

const compatibleApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
}));

const rawApi = vi.hoisted(() => ({
  request: vi.fn(),
  downloadBlob: vi.fn(),
}));

vi.mock('./compatibleApiService', () => ({
  compatibleApiService: compatibleApi,
}));

vi.mock('./api.service', () => ({
  apiService: rawApi,
}));

import { externalEstateServicesService } from './external-estate-services.service';

describe('external estate services API client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('uploads the customer-signed agreement without changing approval dates', async () => {
    rawApi.request.mockResolvedValueOnce({
      data: {
        id: 'request-1',
        fieldValues: {
          moveInDate: '2026-09-01',
          billingStartDate: '2026-09-01',
        },
      },
    });
    const file = new File(['signed'], 'signed-agreement.pdf', {
      type: 'application/pdf',
    });

    await externalEstateServicesService.uploadSignedAgreement(
      'request-1',
      file,
      'Signed by customer'
    );

    expect(rawApi.request).toHaveBeenCalledWith(
      '/estate/external/requests/request-1/signed-agreement',
      {
        method: 'POST',
        body: expect.any(FormData),
      }
    );
    const form = rawApi.request.mock.calls[0][1].body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('notes')).toBe('Signed by customer');
    expect(form.has('moveInDate')).toBe(false);
  });

  it('downloads the approved agreement through the PDF-only endpoint', async () => {
    const pdf = new Blob(['pdf'], { type: 'application/pdf' });
    rawApi.downloadBlob.mockResolvedValueOnce(pdf);

    await expect(
      externalEstateServicesService.downloadGeneratedAgreement('request 1')
    ).resolves.toBe(pdf);

    expect(rawApi.downloadBlob).toHaveBeenCalledWith(
      '/estate/external/requests/request%201/agreement'
    );
  });
});
