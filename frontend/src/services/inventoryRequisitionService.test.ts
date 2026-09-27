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
  it.each([
    ['StockItem', 'Expense', 1, 1],
    ['FixedAsset', 'FixedAsset', 4, 2],
    [1, 1, 1, 1],
  ])('normalizes mapping enums %s / %s for display and editing', async (itemType, treatment, expectedItem, expectedTreatment) => {
    mockedAxios.get.mockResolvedValueOnce({ data: [{ id: 'rule-1', itemType, treatment, expenseAccount: '000-6700-0000 · UAT' }] });
    const [rule] = await inventoryRequisitionService.getIssueAccountingRules();
    expect(rule).toEqual(expect.objectContaining({ itemType: expectedItem, treatment: expectedTreatment }));
    expect(rule.expenseAccount).toContain('000-6700-0000');
  });

  it('carries receiver, replay key and requisition row version into issue', async () => {
    const request: IssueRequisitionDto = {
      receiverUserId: 'receiver-1',
      idempotencyKey: 'issue-key-1',
      rowVersion: 'AQID',
      movementReasonCode: 'DEPARTMENT_CONSUMPTION',
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

  it('loads controlled issue reasons and persists rule selectors without free-text owner IDs', async () => {
    mockedAxios.get.mockResolvedValueOnce({ data: { applicableMovementReasonCodes: ['ASSET_CUSTODY'] } });
    mockedAxios.post.mockResolvedValueOnce({ data: { id: 'rule-1', rowVersion: 'AQID', itemType: 'FixedAsset', treatment: 'FixedAsset' } });

    await inventoryRequisitionService.getIssueAccountingOptions('req-1');
    await inventoryRequisitionService.createIssueAccountingRule({
      inventoryCategoryId: 'category-1',
      itemType: 4,
      movementReasonCode: 'ASSET_CUSTODY',
      treatment: 2,
      fixedAssetCategoryId: 'asset-category-1',
      isActive: true,
      effectiveFromUtc: '2026-08-13T00:00:00.000Z',
    });

    expect(mockedAxios.get).toHaveBeenCalledWith(
      expect.stringMatching(/\/inventory\/issue-accounting\/options$/),
      expect.objectContaining({ params: { requisitionId: 'req-1' } }),
    );
    expect(mockedAxios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/inventory\/issue-accounting\/rules$/),
      expect.objectContaining({
        inventoryCategoryId: 'category-1',
        fixedAssetCategoryId: 'asset-category-1',
        movementReasonCode: 'ASSET_CUSTODY',
      }),
      expect.anything(),
    );
  });

  it('uses voucher row version and a fresh replay key for receiver acknowledgement', async () => {
    mockedAxios.post.mockResolvedValueOnce({ data: { id: 'voucher-1', status: 2 } });

    await inventoryRequisitionService.acknowledgeIssueVoucher('voucher-1', 'BAUG', 'Received intact.', [{ issueVoucherLineId: 'line-1', receivedQuantity: 92 }]);

    expect(mockedAxios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/issue-vouchers\/voucher-1\/acknowledge$/),
      { rowVersion: 'BAUG', comment: 'Received intact.', lines: [{ issueVoucherLineId: 'line-1', receivedQuantity: 92 }], idempotencyKey: '60600000-0000-4000-8000-000000000001' },
      expect.anything(),
    );
  });

  it('preserves the supplied receipt retry key and explicit zero quantities', async () => {
    mockedAxios.post.mockResolvedValueOnce({ data: { id: 'voucher-1', status: 1 } });
    const lines = [{ issueVoucherLineId: 'line-1', receivedQuantity: 0 }, { issueVoucherLineId: 'line-2', receivedQuantity: 3.1234 }];
    await inventoryRequisitionService.acknowledgeIssueVoucher('voucher-1', 'BAUG', 'Partial delivery', lines, 'same-retry');
    expect(mockedAxios.post).toHaveBeenCalledWith(expect.any(String), { rowVersion: 'BAUG', comment: 'Partial delivery', lines, idempotencyKey: 'same-retry' }, expect.anything());
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
