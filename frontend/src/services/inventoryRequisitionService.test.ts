import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { inventoryRequisitionService, type IssueRequisitionDto } from './inventoryRequisitionService';

vi.mock('axios');
const mockedAxios = vi.mocked(axios, true);

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  localStorage.setItem('authToken', 'tenant-token');
  vi.stubGlobal('crypto', { randomUUID: vi.fn(() => '60600000-0000-4000-8000-000000000001') });
});

describe('inventoryRequisitionService controlled issue lifecycle', () => {
  it('carries receiver, replay key and requisition row version into issue', async () => {
    const request: IssueRequisitionDto = {
      receiverUserId: 'receiver-1',
      idempotencyKey: 'issue-key-1',
      rowVersion: 'AQID',
      items: [{ itemId: 'line-1', issuedQuantity: 2 }],
    };
    mockedAxios.post.mockResolvedValueOnce({ data: { voucher: { id: 'voucher-1' } } });

    const result = await inventoryRequisitionService.issue('req-1', request);

    expect(result).toEqual({ id: 'voucher-1' });
    expect(mockedAxios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/inventory\/requisitions\/req-1\/issue$/),
      expect.objectContaining(request),
      expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer tenant-token' }) }),
    );
  });

  it('uses voucher row version and a fresh replay key for receiver acknowledgement', async () => {
    mockedAxios.post.mockResolvedValueOnce({ data: { id: 'voucher-1', status: 2 } });

    await inventoryRequisitionService.acknowledgeIssueVoucher('voucher-1', 'BAUG', 'Received intact.');

    expect(mockedAxios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/issue-vouchers\/voucher-1\/acknowledge$/),
      { rowVersion: 'BAUG', comment: 'Received intact.', idempotencyKey: '60600000-0000-4000-8000-000000000001' },
      expect.anything(),
    );
  });

  it('downloads immutable SIV evidence as a blob from the protected API', async () => {
    const blob = new Blob(['SIV'], { type: 'application/pdf' });
    mockedAxios.get.mockResolvedValueOnce({ data: blob });

    await expect(inventoryRequisitionService.downloadIssueVoucher('voucher-1')).resolves.toBe(blob);
    expect(mockedAxios.get).toHaveBeenCalledWith(
      expect.stringMatching(/\/issue-vouchers\/voucher-1\/download$/),
      expect.objectContaining({ responseType: 'blob' }),
    );
  });
});

describe('inventoryRequisitionService controlled return lifecycle', () => {
  const voucher = { id: 'return-1', rowVersion: 'CAgJ', status: 'PendingApproval' } as never;

  it('submits exact return lines, replay/concurrency and central-DMS evidence', async () => {
    mockedAxios.post.mockResolvedValueOnce({ data: { id: 'return-1' } });
    const request = {
      items: [{ itemId: 'line-1', returnedQuantity: 1, locationId: 'location-1' }],
      reasonCode: 'DEFECTIVE',
      reason: 'Defective on use',
      idempotencyKey: 'return-key-1',
      rowVersion: 'AQID',
      evidence: [{ centralDocumentVersionId: 'version-1', evidenceReference: 'Inspection report' }],
    };

    await inventoryRequisitionService.returnItems('req-1', request);

    expect(mockedAxios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/inventory\/requisitions\/req-1\/return$/),
      expect.objectContaining(request),
      expect.anything(),
    );
  });

  it('uses voucher row version and fresh replay keys for decision, post and reversal', async () => {
    mockedAxios.post.mockResolvedValue({ data: voucher });

    await inventoryRequisitionService.decideReturnVoucher(voucher, true, 'Approved independently');
    await inventoryRequisitionService.postReturnVoucher(voucher);
    await inventoryRequisitionService.reverseReturnVoucher(voucher, 'Approved correction');

    const payloads = mockedAxios.post.mock.calls.map((call) => call[1]);
    expect(payloads[0]).toEqual(expect.objectContaining({ approved: true, rowVersion: 'CAgJ' }));
    expect(payloads[1]).toEqual(expect.objectContaining({ rowVersion: 'CAgJ' }));
    expect(payloads[2]).toEqual(expect.objectContaining({ rowVersion: 'CAgJ', reason: 'Approved correction' }));
  });

  it('downloads immutable SRV evidence as a protected PDF blob', async () => {
    const blob = new Blob(['SRV'], { type: 'application/pdf' });
    mockedAxios.get.mockResolvedValueOnce({ data: blob });

    await expect(inventoryRequisitionService.downloadReturnVoucher('return-1')).resolves.toBe(blob);
    expect(mockedAxios.get).toHaveBeenCalledWith(
      expect.stringMatching(/\/return-vouchers\/return-1\/download$/),
      expect.objectContaining({ responseType: 'blob' }),
    );
  });
});
