import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  procurementPlanService,
  type CreateProcurementPlanItemDto,
} from './procurementPlanningService';

beforeEach(() => { localStorage.clear(); });
afterEach(() => { vi.unstubAllGlobals(); });

const item: CreateProcurementPlanItemDto = {
  itemDescription: 'UAT item', estimatedQuantity: 1, estimatedUnitPrice: 750,
  unitOfMeasure: 'EA', priority: 'Medium', isCritical: false,
};

describe.each(['add', 'update'] as const)('plan item %s request', action => {
  const save = (data: CreateProcurementPlanItemDto) => action === 'add'
    ? procurementPlanService.addItem('plan-1', data)
    : procurementPlanService.updateItem('item-1', data);

  it.each([undefined, '', '   ', '2026-09-15'])('serializes optional date %j safely', async requiredDate => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('{}', { status: 200 }));
    vi.stubGlobal('fetch', fetchMock);
    const data = { ...item, requiredDate };
    await save(data);
    const body = JSON.parse(fetchMock.mock.calls[0][1].body);
    expect(body.requiredDate).toBe(requiredDate?.trim() || null);
    expect(body.estimatedQuantity).toBe(1);
    expect(body.estimatedUnitPrice).toBe(750);
    expect(data.requiredDate).toBe(requiredDate);
  });

  it('shows the server field-validation message instead of a generic failure', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      title: 'Validation failed', errors: { requiredDate: ['Enter a valid required date.'] },
    }), { status: 400 })));
    await expect(save(item)).rejects.toThrow('Enter a valid required date.');
  });
});
