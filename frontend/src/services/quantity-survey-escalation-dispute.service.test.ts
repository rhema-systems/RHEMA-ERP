import { beforeEach, describe, expect, it, vi } from 'vitest';

const api = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  downloadBlob: vi.fn(),
}));
vi.mock('@/services/api.service', () => ({ apiService: api }));

import { quantitySurveyEscalationDisputeService as service } from './quantity-survey-escalation-dispute.service';

describe('quantity survey escalation dispute API client', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads only controlled approved calculation lookups', async () => {
    await service.calculationLookups();
    expect(api.get).toHaveBeenCalledWith(
      '/quantity-survey/escalation-disputes/calculation-lookups'
    );
  });

  it('keeps client request and row version on response and independent resolution', async () => {
    await service.respond('dispute-1', {
      clientRequestId: 'response-1',
      rowVersion: 'rv-1',
      response: 'Contractor response retained.',
    });
    await service.resolve('dispute-1', {
      clientRequestId: 'resolve-1',
      rowVersion: 'rv-2',
      outcome: 'PartiallyAccepted',
      resolutionNotes: 'Independent resolution notes.',
    });
    expect(api.post).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/escalation-disputes/dispute-1/contractor-response',
      {
        clientRequestId: 'response-1',
        rowVersion: 'rv-1',
        response: 'Contractor response retained.',
      }
    );
    expect(api.post).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/escalation-disputes/dispute-1/resolve',
      {
        clientRequestId: 'resolve-1',
        rowVersion: 'rv-2',
        outcome: 'PartiallyAccepted',
        resolutionNotes: 'Independent resolution notes.',
      }
    );
  });

  it('uploads controlled evidence using multipart data without a free-text record id', async () => {
    const file = new File(['evidence'], 'response.pdf', {
      type: 'application/pdf',
    });
    await service.addAttachment('dispute-1', {
      clientRequestId: 'attachment-1',
      attachmentType: 'ContractorSubmission',
      title: 'Contractor response evidence',
      file,
    });
    expect(api.post).toHaveBeenCalledWith(
      '/quantity-survey/escalation-disputes/dispute-1/attachments',
      expect.any(FormData)
    );
    const form = api.post.mock.calls[0][1] as FormData;
    expect(form.get('clientRequestId')).toBe('attachment-1');
    expect(form.get('attachmentType')).toBe('ContractorSubmission');
    const retainedFile = form.get('file') as File;
    expect(retainedFile.name).toBe('response.pdf');
    expect(retainedFile.type).toBe('application/pdf');
  });

  it('uses dedicated audited PDF ZIP and DMS content routes', async () => {
    await service.auditPack('dispute-1', 'pdf');
    await service.auditPack('dispute-1', 'zip');
    await service.attachmentContent('dispute-1', 'attachment-1');
    expect(api.downloadBlob).toHaveBeenNthCalledWith(
      1,
      '/quantity-survey/escalation-disputes/dispute-1/audit-pack',
      { format: 'pdf' }
    );
    expect(api.downloadBlob).toHaveBeenNthCalledWith(
      2,
      '/quantity-survey/escalation-disputes/dispute-1/audit-pack',
      { format: 'zip' }
    );
    expect(api.downloadBlob).toHaveBeenNthCalledWith(
      3,
      '/quantity-survey/escalation-disputes/dispute-1/attachments/attachment-1/content'
    );
  });
});
