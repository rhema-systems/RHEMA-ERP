import { describe, expect, it } from 'vitest';
import { buildPlannedCostPayload, plannedCostTotal, PlannedCostLine } from './purchase-order-landed-costs';
const cost = (patch: Partial<PlannedCostLine> = {}): PlannedCostLine => ({
  tempId: 'cost', costType: 1, description: 'Freight', amount: 100, currency: 'GHS', exchangeRate: 1,
  allocationMethod: 'ByValue', ...patch
});
describe('PO planned landed cost scope', () => {
  it('keeps shared and targeted estimates separate, including new lines', () => {
    const result = buildPlannedCostPayload([cost(), cost({ purchaseOrderLineKey: 'B', amount: 40 })],
      [{ tempId: 'A' }, { tempId: 'B' }], 'GHS', '');
    expect(result.items[0].purchaseOrderLineIndex).toBeUndefined();
    expect(result.items[1]).toMatchObject({ purchaseOrderLineIndex: 1, amount: 40, allocationMethod: 'ByQuantity' });
  });
  it('tracks the exact row after reorder, not the inventory code', () => {
    const result = buildPlannedCostPayload([cost({ purchaseOrderLineKey: 'B' })],
      [{ tempId: 'B' }, { tempId: 'A' }], 'GHS', '');
    expect(result.items[0].purchaseOrderLineIndex).toBe(0);
  });
  it('rejects orphaned costs instead of turning them into whole-PO costs', () => {
    expect(() => buildPlannedCostPayload([cost({ purchaseOrderLineKey: 'deleted' })], [], 'GHS', '')).toThrow('removed');
  });
  it.each([0, -1, NaN, Infinity])('rejects invalid amount %s before saving the PO', amount => {
    expect(() => buildPlannedCostPayload([cost({ amount })], [], 'GHS', '')).toThrow();
  });
  it('persists removal of all estimates with an empty replacement', () => {
    expect(buildPlannedCostPayload([], [], 'GHS', '').items).toEqual([]);
  });
  it('totals converted estimates once without changing item prices', () => {
    expect(plannedCostTotal([cost(), cost({ amount: 20, exchangeRate: 2, purchaseOrderLineKey: 'A' })])).toBe(140);
  });
});
