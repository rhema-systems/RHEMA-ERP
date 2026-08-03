import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { inventoryDirectedOperationService, type DirectedSuggestion, type DirectedTask } from './inventoryDirectedOperationService';

vi.mock('axios');
const mockedAxios = vi.mocked(axios, true);

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  localStorage.setItem('authToken', 'token');
  vi.stubGlobal('crypto', { randomUUID: vi.fn(() => '00000000-0000-4000-8000-000000000001') });
});

const suggestion = { warehouseId: 'warehouse-1', suggestionKey: 'a'.repeat(64), explanation: 'Replenish the pick face.' } as DirectedSuggestion;
const task = { id: 'task-1', rowVersion: 'AQID', status: 'Assigned' } as DirectedTask;

describe('inventoryDirectedOperationService', () => {
  it('keeps suggestion discovery within the selected warehouse and task type', async () => {
    mockedAxios.get.mockResolvedValueOnce({ data: [suggestion] });
    await inventoryDirectedOperationService.suggestions('warehouse-1', 'Replenishment');
    expect(mockedAxios.get).toHaveBeenCalledWith(expect.stringContaining('/suggestions'), expect.objectContaining({
      params: { warehouseId: 'warehouse-1', taskType: 'Replenishment', take: 250 },
    }));
  });

  it('creates only from the server suggestion key with a replay key', async () => {
    mockedAxios.post.mockResolvedValueOnce({ data: task });
    await inventoryDirectedOperationService.create(suggestion, 'user-1');
    expect(mockedAxios.post).toHaveBeenCalledWith(expect.stringMatching(/\/tasks$/), expect.objectContaining({
      warehouseId: 'warehouse-1', suggestionKey: 'a'.repeat(64), assignedToUserId: 'user-1',
      idempotencyKey: '00000000-0000-4000-8000-000000000001',
    }), expect.anything());
  });

  it('always carries the loaded row version into lifecycle mutations', async () => {
    mockedAxios.post.mockResolvedValue({ data: task });
    await inventoryDirectedOperationService.start(task, 'Started at the assigned bin.');
    await inventoryDirectedOperationService.confirm(task, { comment: 'Quantity confirmed.' });
    await inventoryDirectedOperationService.reconcile(task);
    await inventoryDirectedOperationService.cancel(task, 'Work was reassigned.');
    for (const call of mockedAxios.post.mock.calls)
      expect(call[1]).toEqual(expect.objectContaining({ rowVersion: 'AQID' }));
  });
});
