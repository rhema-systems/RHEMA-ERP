import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiService } from '@/services/api.service';
import { quantitySurveyMeasurementService as service } from './quantity-survey-measurement.service';

vi.mock('@/services/api.service', () => ({
  apiService: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    downloadBlob: vi.fn(),
  },
}));

describe('quantitySurveyMeasurementService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads controlled project lookups and tenant-scoped sheets', async () => {
    vi.mocked(apiService.get).mockResolvedValue({});
    await service.lookups('project-1');
    await service.list({ projectId: 'project-1', status: 'Draft' });
    expect(apiService.get).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/measurements/lookups',
      { projectId: 'project-1' }
    );
    expect(apiService.get).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/measurements',
      { projectId: 'project-1', status: 'Draft' }
    );
  });

  it('uses explicit create, update, and record lifecycle endpoints', async () => {
    vi.mocked(apiService.post).mockResolvedValue({});
    vi.mocked(apiService.put).mockResolvedValue({});
    const base = {
      clientRequestId: 'request-1',
      projectId: 'project-1',
      projectBoqVersionLineId: 'line-1',
      sourceType: 'Site' as const,
      title: 'Site measurement',
      measurementDate: '2026-08-10T00:00:00Z',
      siteLocation: 'Block A',
      lines: [],
    };
    await service.create(base);
    await service.update('sheet-1', { ...base, rowVersion: 'row-version' });
    await service.record('sheet-1', {
      clientRequestId: 'request-2',
      rowVersion: 'row-version',
    });
    expect(apiService.post).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/measurements',
      base
    );
    expect(apiService.put).toHaveBeenCalledWith(
      '/quantity-survey/measurements/sheet-1',
      { ...base, rowVersion: 'row-version' }
    );
    expect(apiService.post).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/measurements/sheet-1/record',
      { clientRequestId: 'request-2', rowVersion: 'row-version' }
    );
  });

  it('uploads evidence through a typed multipart request', async () => {
    vi.mocked(apiService.post).mockResolvedValue({});
    const file = new File(['evidence'], 'site.jpg', { type: 'image/jpeg' });
    await service.addAttachment('sheet-1', {
      clientRequestId: 'request-3',
      evidenceType: 'SitePhoto',
      title: 'Joint site photo',
      file,
    });
    expect(apiService.post).toHaveBeenCalledWith(
      '/quantity-survey/measurements/sheet-1/attachments',
      expect.any(FormData)
    );
    const form = vi.mocked(apiService.post).mock.calls[0][1] as FormData;
    expect(form.get('clientRequestId')).toBe('request-3');
    expect(form.get('evidenceType')).toBe('SitePhoto');
    expect(form.get('file')).toBeInstanceOf(File);
    expect((form.get('file') as File).name).toBe('site.jpg');
  });

  it('opens evidence and history only through governed endpoints', async () => {
    vi.mocked(apiService.get).mockResolvedValue([]);
    vi.mocked(apiService.downloadBlob).mockResolvedValue(new Blob());
    await service.history('sheet-1');
    await service.attachmentContent('sheet-1', 'attachment-1');
    expect(apiService.get).toHaveBeenCalledWith(
      '/quantity-survey/measurements/sheet-1/history'
    );
    expect(apiService.downloadBlob).toHaveBeenCalledWith(
      '/quantity-survey/measurements/sheet-1/attachments/attachment-1/content'
    );
  });
});
