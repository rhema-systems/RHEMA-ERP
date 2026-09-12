import type { LandedCostAllocationMethod, UpsertPurchaseOrderLandedCostPlanDto } from '@/services/purchasingService';

export interface PlannedCostLine {
  tempId: string;
  purchaseOrderLineKey?: string;
  costType: number;
  description: string;
  amount: number;
  currency: string;
  exchangeRate: number;
  allocationMethod: LandedCostAllocationMethod;
  supplierId?: string;
  referenceNumber?: string;
  notes?: string;
}

export const plannedCostTotal = (costs: PlannedCostLine[]) =>
  costs.reduce((sum, c) => sum + Math.round(c.amount * c.exchangeRate * 100) / 100, 0);

export function buildPlannedCostPayload(costs: PlannedCostLine[],
  lines: { tempId: string }[], currency: string, notes: string): UpsertPurchaseOrderLandedCostPlanDto {
  if (!currency.trim()) throw new Error('Enter the planned landed cost currency.');
  return {
    currency: currency.trim().toUpperCase(), notes: notes.trim() || undefined,
    items: costs.map(c => {
      if (!c.description.trim() || !Number.isFinite(c.amount) || c.amount <= 0 ||
          !Number.isFinite(c.exchangeRate) || c.exchangeRate <= 0 || !c.currency.trim())
        throw new Error('Complete the description, positive amount, currency and exchange rate for every planned cost.');
      const index = c.purchaseOrderLineKey ? lines.findIndex(l => l.tempId === c.purchaseOrderLineKey) : undefined;
      if (index === -1) throw new Error('A planned cost belongs to a removed PO line. Remove that cost before saving.');
      return {
        purchaseOrderLineIndex: index, costType: c.costType, description: c.description.trim(),
        amount: c.amount, currency: c.currency.trim().toUpperCase(), exchangeRate: c.exchangeRate,
        allocationMethod: c.purchaseOrderLineKey ? 'ByQuantity' : c.allocationMethod,
        supplierId: c.supplierId || undefined, referenceNumber: c.referenceNumber?.trim() || undefined,
        notes: c.notes
      };
    })
  };
}
