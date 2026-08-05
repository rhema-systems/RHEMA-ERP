import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { stockAdjustmentService } from './stockAdjustmentService';

vi.mock('axios');
const mockedAxios = vi.mocked(axios, true);

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  localStorage.setItem('authToken', 'tenant-token');
  vi.stubGlobal('crypto', { randomUUID: vi.fn(() => '60700000-0000-4000-8000-000000000001') });
});

describe('stockAdjustmentService controlled lifecycle', () => {
  it('creates a draft with replay, correlation, exact location and central-DMS evidence', async () => {
    mockedAxios.post.mockResolvedValueOnce({ data: { id: 'adjustment-1', status: 'Draft' } });

    await stockAdjustmentService.create({
      warehouseId: 'warehouse-1',
      reasonCode: 'DAMAGE',
      description: 'Damaged during handling',
      items: [{ inventoryItemId: 'item-1', locationId: 'location-1', adjustmentQuantity: -1 }],
      evidence: [{ centralDocumentVersionId: 'version-1', evidenceReference: 'Damage photograph' }],
    });

    expect(mockedAxios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/inventory\/adjustments$/),
      expect.objectContaining({
        idempotencyKey: '60700000-0000-4000-8000-000000000001',
        correlationId: 'adjustment-ui:60700000-0000-4000-8000-000000000001',
        evidence: [{ centralDocumentVersionId: 'version-1', evidenceReference: 'Damage photograph' }],
        items: [expect.objectContaining({ locationId: 'location-1', adjustmentQuantity: -1 })],
      }),
      expect.objectContaining({ headers: expect.objectContaining({ Authorization: 'Bearer tenant-token' }) }),
    );
  });

  it('submits with the latest row version and an independent-workflow replay key', async () => {
    mockedAxios.get.mockResolvedValueOnce({ data: { id: 'adjustment-1', rowVersion: 'AQID', status: 'Draft' } });
    mockedAxios.post.mockResolvedValueOnce({ data: { id: 'adjustment-1', status: 'PendingApproval' } });

    await stockAdjustmentService.submit('adjustment-1');

    expect(mockedAxios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/inventory\/adjustments\/adjustment-1\/submit$/),
      expect.objectContaining({ rowVersion: 'AQID', idempotencyKey: '60700000-0000-4000-8000-000000000001' }),
      expect.anything(),
    );
  });

  it('carries concurrency and replay controls through decision, Finance post and reversal', async () => {
    mockedAxios.get.mockResolvedValue({ data: { id: 'adjustment-1', rowVersion: 'BAUG' } });
    mockedAxios.post.mockResolvedValue({ data: { id: 'adjustment-1' } });

    await stockAdjustmentService.decide('adjustment-1', true, 'Independent approval');
    await stockAdjustmentService.post('adjustment-1');
    await stockAdjustmentService.reverse('adjustment-1', 'Approved correction');

    const payloads = mockedAxios.post.mock.calls.map((call) => call[1]);
    expect(payloads[0]).toEqual(expect.objectContaining({ approved: true, rowVersion: 'BAUG' }));
    expect(payloads[1]).toEqual(expect.objectContaining({ rowVersion: 'BAUG', comment: expect.stringContaining('Finance') }));
    expect(payloads[2]).toEqual(expect.objectContaining({ rowVersion: 'BAUG', reason: 'Approved correction' }));
  });
});
