import { apiService } from '@/services/api.service';

const root = '/quantity-survey/escalation-formulas';

export interface QuantitySurveyEscalationLookupOption {
  value: string;
  label: string;
  group?: string | null;
}

export interface QuantitySurveyEscalationPolicy {
  configurationProfileId: string;
  configurationDecisionId: string;
  formulaType: string;
  materialCoefficient: number;
  labourCoefficient: number;
  plantCoefficient: number;
  otherCoefficient: number;
  importFormat: string;
  totalCoefficient: number;
}

export interface QuantitySurveyEscalationLookups {
  sources: Record<string, QuantitySurveyEscalationLookupOption[]>;
  policy: QuantitySurveyEscalationPolicy;
}

export interface QuantitySurveyIndexFamily {
  id: string;
  code: string;
  name: string;
  source: string | number;
  publisher: string;
  description?: string | null;
  isActive: boolean;
  rowVersion: string;
}

export interface SaveQuantitySurveyIndexFamily {
  code: string;
  name: string;
  source: string;
  publisher: string;
  description?: string | null;
  isActive: boolean;
  reason: string;
  rowVersion?: string | null;
}

export interface QuantitySurveyEscalationComponent {
  id: string;
  sequence: number;
  component: string | number;
  coefficient: number;
  indexFamilyId: string;
  indexSource: string | number;
  indexFamilyCode: string;
  indexFamilyName: string;
}

export interface QuantitySurveyEscalationFormula {
  id: string;
  formulaKey: string;
  code: string;
  name: string;
  version: number;
  projectId: string;
  projectCode: string;
  projectName: string;
  contractId: string;
  contractNumber: string;
  contractTitle: string;
  contractClauseReference: string;
  formulaType: string | number;
  baseDate: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  authorityRoleId: string;
  authorityRoleName: string;
  configurationProfileId: string;
  configurationDecisionId: string;
  approvalWorkflowDefinitionId: string;
  centralDocumentRecordId: string;
  centralDocumentVersionId: string;
  evidenceLabel: string;
  supersedesFormulaId?: string | null;
  status: string;
  approvalStatus: string;
  preparedById: string;
  preparedAt: string;
  submittedById?: string | null;
  submittedAt?: string | null;
  approvedById?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  snapshotHash: string;
  rowVersion: string;
  components: QuantitySurveyEscalationComponent[];
}

export interface QuantitySurveyEscalationFormulaPage {
  items: QuantitySurveyEscalationFormula[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface QuantitySurveyEscalationFormulaQuery {
  search?: string;
  projectId?: string;
  contractId?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface SaveQuantitySurveyEscalationFormula {
  clientRequestId: string;
  code: string;
  name: string;
  projectId: string;
  contractId: string;
  contractClauseReference: string;
  formulaType: string;
  baseDate: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  authorityRoleId: string;
  centralDocumentVersionId: string;
  sourceFormulaId?: string | null;
  components: Array<{
    component: string;
    coefficient: number;
    indexFamilyId: string;
  }>;
  reason: string;
  rowVersion?: string;
}

export interface QuantitySurveyEscalationRevision {
  id: string;
  action: string;
  actorUserId: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson?: string | null;
  createdAt: string;
}

export const quantitySurveyEscalationService = {
  lookups: () =>
    apiService.get<QuantitySurveyEscalationLookups>(`${root}/lookups`),
  indexFamilies: (search?: string, includeInactive = true) =>
    apiService.get<QuantitySurveyIndexFamily[]>(`${root}/index-families`, {
      search: search?.trim() || undefined,
      includeInactive,
    }),
  createIndexFamily: (request: SaveQuantitySurveyIndexFamily) =>
    apiService.post<QuantitySurveyIndexFamily>(
      `${root}/index-families`,
      request
    ),
  updateIndexFamily: (id: string, request: SaveQuantitySurveyIndexFamily) =>
    apiService.put<QuantitySurveyIndexFamily>(
      `${root}/index-families/${id}`,
      request
    ),
  list: (query: QuantitySurveyEscalationFormulaQuery) =>
    apiService.get<QuantitySurveyEscalationFormulaPage>(root, query),
  get: (id: string) =>
    apiService.get<QuantitySurveyEscalationFormula>(`${root}/${id}`),
  create: (request: SaveQuantitySurveyEscalationFormula) =>
    apiService.post<QuantitySurveyEscalationFormula>(root, request),
  update: (id: string, request: SaveQuantitySurveyEscalationFormula) =>
    apiService.put<QuantitySurveyEscalationFormula>(`${root}/${id}`, request),
  lifecycle: (
    id: string,
    action: 'submit' | 'approve' | 'reject' | 'retire',
    rowVersion: string,
    reason: string
  ) =>
    apiService.post<QuantitySurveyEscalationFormula>(
      `${root}/${id}/${action}`,
      { rowVersion, reason }
    ),
  history: (id: string) =>
    apiService.get<QuantitySurveyEscalationRevision[]>(`${root}/${id}/history`),
};
