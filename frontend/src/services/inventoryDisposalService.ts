import axios from 'axios';

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

type Mutation = { rowVersion: string; idempotencyKey: string; correlationId?: string; comment?: string };
const mutation = (value: InventoryDisposal, prefix: string, comment?: string): Mutation => ({
  rowVersion: value.rowVersion, idempotencyKey: `${prefix}:${crypto.randomUUID()}`, comment,
});

export const inventoryDisposalService = {
  async getAll(filters?: { status?: number; warehouseId?: string; take?: number }) {
    return (await axios.get<InventoryDisposal[]>(API_URL, { params: filters, headers: headers() })).data;
  },
  async getById(id: string) {
    return (await axios.get<InventoryDisposal>(`${API_URL}/${id}`, { headers: headers() })).data;
  },
  async create(request: { warehouseId: string; method: InventoryDisposalMethod; reason: string;
    identificationDetails: string; lines: Array<{ inventoryItemId: string; locationId: string; quantity: number;
      lotNumber?: string; batchNumber?: string; serialNumber?: string; conditionNotes?: string }>;
    evidence: DisposalEvidenceRequest[]; idempotencyKey?: string }) {
    return (await axios.post<InventoryDisposal>(API_URL,
      { ...request, idempotencyKey: request.idempotencyKey || `identify:${crypto.randomUUID()}` }, { headers: headers() })).data;
  },
  async verify(value: InventoryDisposal, verified: boolean, findings: string, evidence: DisposalEvidenceRequest[] = []) {
    return (await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/audit-verification`,
      { ...mutation(value, 'audit', findings), verified, findings, evidence }, { headers: headers() })).data;
  },
  async update(value: InventoryDisposal, request: { method: InventoryDisposalMethod; reason: string;
    identificationDetails: string; lines: Array<{ inventoryItemId: string; locationId: string; quantity: number;
      lotNumber?: string; batchNumber?: string; serialNumber?: string; conditionNotes?: string }>;
    evidence: DisposalEvidenceRequest[] }) {
    return (await axios.put<InventoryDisposal>(`${API_URL}/${value.id}`,
      { ...mutation(value, 'edit'), ...request }, { headers: headers() })).data;
  },
  async cancel(value: InventoryDisposal, reason: string) {
    return (await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/cancel`,
      mutation(value, 'cancel', reason), { headers: headers() })).data;
  },
  async schedule(value: InventoryDisposal, meetingAtUtc: string, committeeReference: string, memberUserIds: string[], comment?: string) {
    return (await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/committee/schedule`,
      { ...mutation(value, 'schedule', comment), meetingAtUtc, committeeReference, memberUserIds }, { headers: headers() })).data;
  },
  async vote(value: InventoryDisposal, recommendApproval: boolean, conflictDeclared: boolean, comment?: string) {
    return (await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/committee/vote`,
      { ...mutation(value, 'vote', comment), recommendApproval, conflictDeclared }, { headers: headers() })).data;
  },
  async submit(value: InventoryDisposal, comment?: string) {
    return (await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/submit`,
      mutation(value, 'submit', comment), { headers: headers() })).data;
  },
  async decide(value: InventoryDisposal, approved: boolean, comment?: string) {
    return (await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/decision`,
      { ...mutation(value, 'decision', comment), approved }, { headers: headers() })).data;
  },
  async stageExecution(value: InventoryDisposal, request: { proceedsAmount: number; proceedsAccountId?: string;
    buyerOrRecipient?: string; executionReference: string; evidence: DisposalEvidenceRequest[]; comment?: string; postImmediately?: boolean }) {
    return (await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/execution/stage`,
      { ...mutation(value, 'stage-execution', request.comment), ...request }, { headers: headers() })).data;
  },
  async complete(value: InventoryDisposal, comment?: string) {
    return (await axios.post<InventoryDisposal>(`${API_URL}/${value.id}/execution/complete`,
      { ...mutation(value, 'complete', comment), negativeStockOverrideIds: {} }, { headers: headers() })).data;
  },
};
