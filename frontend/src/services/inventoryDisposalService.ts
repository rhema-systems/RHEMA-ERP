import axios from 'axios';
import type { FinanceSourceDocumentDimensionInput } from '@/types/finance';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/disposals`;
const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || localStorage.getItem('token') || ''}`,
  'Content-Type': 'application/json',
  'X-Correlation-ID': crypto.randomUUID(),
});

export type InventoryDisposalStatus = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11;
export type InventoryDisposalMethod = 1 | 2 | 3 | 4 | 5;
export type InventoryDisposalActionType = 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15;
export type DisposalEvidenceRequest = { centralDocumentVersionId: string; evidenceReference: string };
export type InventoryDisposal = {
  id: string; disposalNumber: string; warehouseId: string; warehouseCode: string; warehouseName: string;
  status: InventoryDisposalStatus; method: InventoryDisposalMethod; reason: string; identificationDetails: string;
  approvalRequired: boolean; canEdit: boolean; canSubmit: boolean; canApprove: boolean;
  canStageExecution: boolean; canComplete: boolean; canCancel: boolean; currencyCode: string;
  postedStockValue?: number;
  canGenerateWaybill?: boolean;
  accountingVersion?: number;
  auctionInvoiceId?: string;
  auctionInvoiceNumber?: string;
  canCreateAuctionInvoice?: boolean;
  requestedById: string; requestedByName: string; requestedAtUtc: string; auditVerifiedById?: string;
  auditVerifiedAtUtc?: string; auditFindings?: string; committeeMeetingAtUtc?: string;
  committeeReference?: string; authorityRoute: string; workflowInstanceId?: string; approvedById?: string;
  approvedAtUtc?: string; stockAdjustmentId?: string; proceedsAmount: number; buyerOrRecipient?: string;
  executionReference?: string; proceedsPostingEventId?: string; proceedsJournalEntryId?: string;
  completedAtUtc?: string; totalQuantity: number; totalValue: number; rowVersion: string;
  lines: Array<{ id: string; inventoryItemId: string; itemCode: string; itemName: string; locationId: string;
    locationCode: string; unitOfMeasure?: string; quantity: number; unitCost: number; totalValue: number; lotNumber?: string;
    batchNumber?: string; serialNumber?: string; conditionNotes?: string }>;
  evidence: Array<{ id: string; centralDocumentVersionId: string; fileUploadRecordId: string; stage: string;
    evidenceReference: string; documentReference: string; versionNumber: string }>;
  committeeMembers: Array<{ memberUserId: string; memberName: string; recommendApproval?: boolean;
    conflictDeclared: boolean; votedAtUtc?: string; comment?: string }>;
  actions: Array<{ sequence: number; actionType: InventoryDisposalActionType; actorUserId: string;
    actorName: string; occurredAtUtc: string; comment?: string }>;
};

const enumValue = <T extends number>(value: T | string, names: Record<string, T>): T => {
  if (typeof value === 'number') return value;
  const key = value.replace(/[^a-z0-9]/gi, '').toLowerCase();
  return names[key] ?? (Number(value) as T);
};

const normalizeDisposal = (value: InventoryDisposal): InventoryDisposal => ({
  ...value,
  status: enumValue(value.status, {
    identified: 1, auditverified: 2, committeescheduled: 3, committeerecommended: 4,
    pendingapproval: 5, approved: 6, adjustmentpending: 7, completed: 8, rejected: 9,
    cancelled: 10, readyforexecution: 11,
  }) as InventoryDisposalStatus,
  method: enumValue(value.method, { auction: 1, sale: 2, writeoff: 3, donation: 4, destruction: 5 }) as InventoryDisposalMethod,
  canGenerateWaybill: enumValue(value.method, { auction: 1, sale: 2, writeoff: 3, donation: 4, destruction: 5 }) !== 2 &&
    [6, 7, 8, 11].includes(enumValue(value.status, { approved: 6, adjustmentpending: 7, completed: 8, readyforexecution: 11 })),
  actions: (value.actions ?? []).map(action => ({
    ...action,
    actionType: enumValue(action.actionType, {
      identified: 1, auditverified: 2, auditrejected: 3, committeescheduled: 4,
      committeevoterecorded: 5, committeerecommended: 6, committeerejected: 7, submitted: 8,
      approved: 9, rejected: 10, adjustmentstaged: 11, completed: 12, cancelled: 13,
      edited: 14, approvalnotrequired: 15,
    }) as InventoryDisposalActionType,
  })),
});

type Mutation = { rowVersion: string; idempotencyKey: string; correlationId?: string; comment?: string };
const mutation = (value: InventoryDisposal, prefix: string, comment?: string): Mutation => ({
  rowVersion: value.rowVersion, idempotencyKey: `${prefix}:${crypto.randomUUID()}`, comment,
});
const current = async (value: InventoryDisposal): Promise<InventoryDisposal> =>
  normalizeDisposal((await axios.get<InventoryDisposal>(`${API_URL}/${value.id}`, { headers: headers() })).data);

export const inventoryDisposalService = {
  async createAuctionInvoice(value: InventoryDisposal, request: { businessPartnerId: string; invoiceDate: string;
    financeDimensions?: FinanceSourceDocumentDimensionInput;
    idempotencyKey: string; lines: Array<{ disposalLineId: string; unitPrice: number; taxTreatment: number; taxGroupId?: string }> }) {
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/auction-invoice`, {
      ...request, rowVersion: value.rowVersion,
    }, { headers: headers() })).data);
  },
  async downloadWaybill(value: InventoryDisposal) {
    try {
      return (await axios.get<Blob>(`${API_URL}/${value.id}/waybill`, {
        headers: headers(), responseType: 'blob',
      })).data;
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.data instanceof Blob) {
        const body = await error.response.data.text();
        try { error.response.data = JSON.parse(body); } catch { /* Keep the original transport error. */ }
      }
      throw error;
    }
  },
  async getAll(filters?: { status?: number; warehouseId?: string; take?: number }) {
    const values = (await axios.get<InventoryDisposal[]>(API_URL, { params: filters, headers: headers() })).data;
    return values.map(normalizeDisposal);
  },
  async getById(id: string) {
    return normalizeDisposal((await axios.get<InventoryDisposal>(`${API_URL}/${id}`, { headers: headers() })).data);
  },
  async create(request: { warehouseId: string; method: InventoryDisposalMethod; reason: string;
    identificationDetails: string; lines: Array<{ inventoryItemId: string; locationId: string; quantity: number;
      lotNumber?: string; batchNumber?: string; serialNumber?: string; conditionNotes?: string }>;
    evidence: DisposalEvidenceRequest[]; idempotencyKey?: string }) {
    return normalizeDisposal((await axios.post<InventoryDisposal>(API_URL,
      { ...request, idempotencyKey: request.idempotencyKey || `identify:${crypto.randomUUID()}` }, { headers: headers() })).data);
  },
  async verify(value: InventoryDisposal, verified: boolean, findings: string, evidence: DisposalEvidenceRequest[] = []) {
    const latest = await current(value);
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/audit-verification`,
      { ...mutation(latest, 'audit', findings), verified, findings, evidence }, { headers: headers() })).data);
  },
  async update(value: InventoryDisposal, request: { method: InventoryDisposalMethod; reason: string;
    identificationDetails: string; lines: Array<{ inventoryItemId: string; locationId: string; quantity: number;
      lotNumber?: string; batchNumber?: string; serialNumber?: string; conditionNotes?: string }>;
    evidence: DisposalEvidenceRequest[] }) {
    const latest = await current(value);
    return normalizeDisposal((await axios.put<InventoryDisposal>(`${API_URL}/${value.id}`,
      { ...mutation(latest, 'edit'), ...request }, { headers: headers() })).data);
  },
  async cancel(value: InventoryDisposal, reason: string) {
    const latest = await current(value);
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/cancel`,
      mutation(latest, 'cancel', reason), { headers: headers() })).data);
  },
  async schedule(value: InventoryDisposal, meetingAtUtc: string, committeeReference: string, memberUserIds: string[], comment?: string) {
    const latest = await current(value);
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/committee/schedule`,
      { ...mutation(latest, 'schedule', comment), meetingAtUtc, committeeReference, memberUserIds }, { headers: headers() })).data);
  },
  async vote(value: InventoryDisposal, recommendApproval: boolean, conflictDeclared: boolean, comment?: string) {
    const latest = await current(value);
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/committee/vote`,
      { ...mutation(latest, 'vote', comment), recommendApproval, conflictDeclared }, { headers: headers() })).data);
  },
  async submit(value: InventoryDisposal, comment?: string) {
    const latest = await current(value);
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/submit`,
      mutation(latest, 'submit', comment), { headers: headers() })).data);
  },
  async decide(value: InventoryDisposal, approved: boolean, comment?: string) {
    const latest = await current(value);
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/decision`,
      { ...mutation(latest, 'decision', comment), approved }, { headers: headers() })).data);
  },
  async stageExecution(value: InventoryDisposal, request: { proceedsAmount: number; proceedsAccountId?: string;
    buyerOrRecipient?: string; executionReference: string; evidence: DisposalEvidenceRequest[]; comment?: string; postImmediately?: boolean }) {
    const latest = await current(value);
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/execution/stage`,
      { ...mutation(latest, 'stage-execution', request.comment), ...request }, { headers: headers() })).data);
  },
  async complete(value: InventoryDisposal, comment?: string) {
    const latest = await current(value);
    return normalizeDisposal((await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/execution/complete`,
      { ...mutation(latest, 'complete', comment), negativeStockOverrideIds: {} }, { headers: headers() })).data);
  },
};
