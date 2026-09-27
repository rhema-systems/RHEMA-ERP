import { beforeEach, describe, expect, it, vi } from 'vitest';

const compatibleApi = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  silentGet: vi.fn(),
}));

const rawApi = vi.hoisted(() => ({
  post: vi.fn(),
  request: vi.fn(),
}));

vi.mock('./compatibleApiService', () => ({
  compatibleApiService: compatibleApi,
}));

vi.mock('./api.service', () => ({
  apiService: rawApi,
}));

import { documentManagementService } from './document-management.service';

describe('document management API client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('uploads generation template Word sources as multipart form data', async () => {
    compatibleApi.post.mockResolvedValueOnce({
      data: {
        templateCode: 'LAND-AGREEMENT',
        title: 'Land agreement',
        hasWordTemplate: true,
      },
    });
    const file = new File(['template'], 'agreement-template.docx', {
      type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    });

    const result =
      await documentManagementService.uploadGenerationTemplateWordFile(
        'LAND-AGREEMENT',
        file
      );

    expect(result.hasWordTemplate).toBe(true);
    expect(compatibleApi.post).toHaveBeenCalledWith(
      '/document-management/document-templates/LAND-AGREEMENT/word-template',
      expect.any(FormData)
    );
    const form = compatibleApi.post.mock.calls[0][1] as FormData;
    expect(form.get('file')).toBe(file);
  });

  it('uses the raw client without the standard timeout for digital signing', async () => {
    rawApi.request.mockResolvedValueOnce({
      data: {
        record: { id: 'record-1', lifecycleStatus: 'Signed' },
        version: { id: 'version-2', status: 'Signed' },
      },
    });

    const result = await documentManagementService.updateGeneratedDocumentWorkflow(
      'record-1',
      { action: 'Sign', signatureRole: 'Authorised Signatory' }
    );

    expect(result.record.lifecycleStatus).toBe('Signed');
    expect(rawApi.request).toHaveBeenCalledWith(
      '/document-management/generated-documents/record-1/workflow',
      expect.objectContaining({
        method: 'POST',
        body: JSON.stringify({
          action: 'Sign',
          signatureRole: 'Authorised Signatory',
        }),
        signal: expect.any(AbortSignal),
      })
    );
  });

  it('searches viewable DMS records and links a selected record to a case document', async () => {
    compatibleApi.get.mockResolvedValueOnce({ data: [{ id: 'record-1' }] });
    compatibleApi.post.mockResolvedValueOnce({ data: { id: 'case-1' } });

    const records = await documentManagementService.getRecords(undefined, { search: 'lease', take: 50 });
    const linkedCase = await documentManagementService.attachRecordToCase('record-1', 'case-1', 'document-1');

    expect(records).toEqual([{ id: 'record-1' }]);
    expect(compatibleApi.get).toHaveBeenCalledWith('/document-management/records', { search: 'lease', take: 50 });
    expect(compatibleApi.post).toHaveBeenCalledWith(
      '/document-management/records/record-1/attach-to-case',
      { caseId: 'case-1', documentId: 'document-1' }
    );
    expect(linkedCase.id).toBe('case-1');
  });
});
