import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { procurementPlanService } from './procurementPlanningService';

beforeEach(() => { localStorage.clear(); });
afterEach(() => { vi.unstubAllGlobals(); });

describe('procurement plan deletion feedback', () => {
  it.each([
    ['plan', () => procurementPlanService.deletePlan('plan-1')],
    ['item', () => procurementPlanService.removeItem('plan-1', 'item-1')],
  ] as const)('preserves server detail and code for %s deletion', async (_name, remove) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      detail: 'Only draft plans may be changed.', code: 'PLAN_NOT_DRAFT',
    }), { status: 409 })));
    await expect(remove()).rejects.toThrow('Only draft plans may be changed. (PLAN_NOT_DRAFT)');
  });
});
