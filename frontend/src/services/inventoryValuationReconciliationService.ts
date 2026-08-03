import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/valuation-reconciliations`;

export type InventoryValuationReconciliationStatus = 0 | 1 | 2;
export type InventoryValuationReconciliationActionType = 0 | 1;

export type InventoryValuationReconciliationException = {
  code: string; area: string; severity: string; message: string; reference?: string;
  expectedAmount?: number; actualAmount?: number; varianceAmount?: number;
};
export type InventoryValuationReconciliationAction = {
  sequence: number; actionType: InventoryValuationReconciliationActionType;
  previousStatus?: InventoryValuationReconciliationStatus; newStatus: InventoryValuationReconciliationStatus;
  actorUserId: string; occurredAtUtc: string; reason?: string; integrityHash: string;
};
export type InventoryValuationReconciliation = {
  id: string; reconciliationNumber: string; fiscalPeriodId: string; fiscalPeriodCode: string;
  fiscalPeriodName: string; isYearEnd: boolean; cutoffDateUtc: string;
  status: InventoryValuationReconciliationStatus; functionalCurrencyCode: string;
  inventoryControlAccountId: string; inventoryControlAccountCode: string; inventoryControlAccountName: string;
  receiptInventoryValue: number; postedLandedCostValue: number; landedCostInventoryValue: number;
  landedCostVarianceValue: number; inventorySubledgerValue: number; inventoryBalanceCacheValue: number;
  currentMovementValue: number; generalLedgerValue: number; reconciliationVariance: number;
  toleranceAmount: number; receiptExceptionCount: number; landedCostExceptionCount: number;
  valuationExceptionCount: number; generalLedgerExceptionCount: number; exceptionCount: number;
  snapshotHash: string; generatedById: string; generatedAtUtc: string; frozenById?: string;
  frozenAtUtc?: string; periodModuleLockId?: string; correlationId: string; rowVersion: string;
  exceptions: InventoryValuationReconciliationException[]; actions: InventoryValuationReconciliationAction[];
};

const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || localStorage.getItem('token') || ''}`,
  'Content-Type': 'application/json',
});
const mutationHeaders = () => ({ ...headers(), 'X-Correlation-ID': crypto.randomUUID() });

export const inventoryValuationReconciliationService = {
  async getAll(filters?: { fiscalPeriodId?: string; status?: number; take?: number }) {
    return (await axios.get<InventoryValuationReconciliation[]>(API_URL,
      { params: filters, headers: headers() })).data;
  },
  async getById(id: string) {
    return (await axios.get<InventoryValuationReconciliation>(`${API_URL}/${id}`,
      { headers: headers() })).data;
  },
  async generate(fiscalPeriodId: string, toleranceAmount: number) {
    return (await axios.post<InventoryValuationReconciliation>(`${API_URL}/generate`,
      { fiscalPeriodId, toleranceAmount, idempotencyKey: `generate:${crypto.randomUUID()}` },
      { headers: mutationHeaders() })).data;
  },
  async freeze(value: InventoryValuationReconciliation, reason: string) {
    return (await axios.post<InventoryValuationReconciliation>(`${API_URL}/${value.id}/freeze`,
      { rowVersion: value.rowVersion, reason, idempotencyKey: `freeze:${crypto.randomUUID()}` },
      { headers: mutationHeaders() })).data;
  },
};
