import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { inventoryManagementService } from './inventoryManagementService';

vi.mock('axios');
const mockedAxios = vi.mocked(axios, true);

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  localStorage.setItem('authToken', 'tenant-token');
  mockedAxios.get.mockResolvedValue({ data: [] });
  mockedAxios.post.mockResolvedValue({ data: {} });
});

describe('inventoryManagementService controlled physical counts', () => {
  const control = { rowVersion: 'AQID', idempotencyKey: 'count-key', correlationId: 'count:1', comment: 'Controlled stage' };

  it('sends independent recount and the three explicit control stages', async () => {
    await inventoryManagementService.recordPhysicalCountRecount('count-1', {
      ...control, physicalCountItemId: 'line-1', itemRowVersion: 'BAUG', recountedQuantity: 7,
      investigationNotes: 'Independent recount reconciled the bin.',
    });
    await inventoryManagementService.decidePhysicalCountStores('count-1', { ...control, approved: true });
    await inventoryManagementService.decidePhysicalCountFinance('count-1', { ...control, approved: true });
    await inventoryManagementService.attestPhysicalCountAudit('count-1', { ...control, approved: true });

    expect(mockedAxios.post.mock.calls.map(call => call[0])).toEqual([
      expect.stringMatching(/\/count-1\/recount$/), expect.stringMatching(/\/count-1\/stores-decision$/),
      expect.stringMatching(/\/count-1\/finance-decision$/), expect.stringMatching(/\/count-1\/audit-attestation$/),
    ]);
    expect(mockedAxios.post.mock.calls[0][1]).toEqual(expect.objectContaining({ rowVersion: 'AQID', itemRowVersion: 'BAUG', idempotencyKey: 'count-key' }));
  });

  it('uses dedicated schedule and controlled Finance-post endpoints', async () => {
    await inventoryManagementService.getCycleCountSchedules();
    await inventoryManagementService.generateDueCycleCounts();
    await inventoryManagementService.postControlledPhysicalCount('count-1', control);

    expect(mockedAxios.get.mock.calls[0][0]).toMatch(/\/physical-counts\/cycle-schedules$/);
    expect(mockedAxios.post.mock.calls[0][0]).toMatch(/\/physical-counts\/cycle-schedules\/generate$/);
    expect(mockedAxios.post.mock.calls[1][0]).toMatch(/\/count-1\/controlled-post$/);
    expect(mockedAxios.post.mock.calls[1][1]).toEqual(expect.objectContaining({ rowVersion: 'AQID', idempotencyKey: 'count-key' }));
  });
});
