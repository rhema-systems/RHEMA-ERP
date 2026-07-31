import { beforeEach, describe, expect, it, vi } from 'vitest';

import { apiService } from '@/services/api.service';
import { procurementFrameworkAgreementService as service } from './procurement-framework-agreement.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    downloadBlob: vi.fn(),
  },
}));

describe('procurementFrameworkAgreementService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('uses the dedicated tenant-safe register endpoints', async () => {
    vi.mocked(apiService.get).mockResolvedValue({ items: [] });

    await service.search({ status: 'Published', effectiveOnly: true });
    await service.sourceOptions();

    expect(apiService.get).toHaveBeenNthCalledWith(
      1,
      '/procurement/framework-agreements',
      { status: 'Published', effectiveOnly: true }
    );
    expect(apiService.get).toHaveBeenNthCalledWith(
      2,
      '/procurement/framework-agreements/options/sources'
    );
  });

  it('uploads documents through multipart controlled storage', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'document-1' });
    const file = new File(['safe'], 'agreement.pdf', {
      type: 'application/pdf',
    });

    await service.uploadDocument(
      'agreement-1',
      file,
      'Signed agreement',
      'Executed copy',
      true
    );

    const [url, body] = vi.mocked(apiService.post).mock.calls[0];
    expect(url).toBe('/procurement/framework-agreements/agreement-1/documents');
    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).get('file')).toBe(file);
  });

  it('keeps extension approval separate from agreement publication', async () => {
    vi.mocked(apiService.post).mockResolvedValue({ id: 'extension-1' });

    await service.decideExtension('agreement-1', 'extension-1', {
      rowVersion: 'row-version',
      approve: true,
      comment: 'Approved by the extension workflow.',
      evidence: [
        {
          referenceKind: 'ExternalReference',
          reference: 'MINUTE-001',
        },
      ],
    });

    expect(apiService.post).toHaveBeenCalledWith(
      '/procurement/framework-agreements/agreement-1/extensions/extension-1/decision',
      expect.objectContaining({ approve: true })
    );
  });
});
