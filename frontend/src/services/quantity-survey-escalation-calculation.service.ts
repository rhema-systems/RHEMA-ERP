import { apiService } from '@/services/api.service';

const root = '/quantity-survey/escalation-calculations';

export type QuantitySurveyEscalationImpactTargetType =
  'PaymentCertificate' | 'FinalAccount';

export interface QuantitySurveyEscalationCalculationLookup {
  id: string;
  label: string;
  group?: string | null;
  amount?: number | null;
  currencyCode?: string | null;
  status?: string | null;
}

export interface QuantitySurveyEscalationCalculationLookups {
  formulas: QuantitySurveyEscalationCalculationLookup[];
}

export interface QuantitySurveyEscalationImpactTargets {
  paymentCertificates: QuantitySurveyEscalationCalculationLookup[];
  finalAccounts: QuantitySurveyEscalationCalculationLookup[];
}

export interface QuantitySurveyEscalationCalculationLine {
  sequence: number;
  component: string | number;
  coefficient: number;
  indexFamilyId: string;
  indexFamilyCode: string;
  baseIndexValueId: string;
  currentIndexValueId: string;
  baseIndexValue: number;
  currentIndexValue: number;
  indexRatio: number;
  weightedContribution: number;
}

export interface QuantitySurveyEscalationCalculation {
  id: string;
  runReference: string;
  formulaId: string;
  formulaCode: string;
  formulaVersion: number;
  projectId: string;
  projectCode: string;
  projectName: string;
  contractId: string;
  contractNumber: string;
  baseIndexPeriod: string;
  currentIndexPeriod: string;
  calculationDate: string;
  impactTargetType: QuantitySurveyEscalationImpactTargetType | number;
  impactTargetId: string;
  impactTargetReference: string;
  impactTargetStatus: string;
  currencyCode: string;
  baseRate: number;
  revisedRate: number;
  adjustmentFactor: number;
  calculatedFluctuationAmount: number;
  reviewerAdjustmentAmount: number;
  reviewerAdjustmentReason?: string | null;
  approvedImpactAmount: number;
  impactApplicationStatus: string;
  authorityRoleName: string;
  workflowInstanceId?: string | null;
  status: string;
  approvalStatus: string;
  preparedById: string;
  preparedAt: string;
  approvedById?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  snapshotHash: string;
  rowVersion: string;
  lines: QuantitySurveyEscalationCalculationLine[];
}

export interface QuantitySurveyEscalationCalculationPage {
  items: QuantitySurveyEscalationCalculation[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateQuantitySurveyEscalationCalculation {
  clientRequestId: string;
  formulaId: string;
  impactTargetType: QuantitySurveyEscalationImpactTargetType;
  impactTargetId: string;
  currentIndexPeriod: string;
  reason: string;
}

export interface QuantitySurveyEscalationCalculationRevision {
  id: string;
  action: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson?: string | null;
  createdAt: string;
}

export const quantitySurveyEscalationCalculationService = {
  lookups: () =>
    apiService.get<QuantitySurveyEscalationCalculationLookups>(
      `${root}/lookups`
    ),
  impactTargets: (formulaId: string) =>
    apiService.get<QuantitySurveyEscalationImpactTargets>(
      `${root}/impact-targets`,
      { formulaId }
    ),
  list: (query: {
    projectId?: string;
    formulaId?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  }) => apiService.get<QuantitySurveyEscalationCalculationPage>(root, query),
  get: (id: string) =>
    apiService.get<QuantitySurveyEscalationCalculation>(`${root}/${id}`),
  calculate: (request: CreateQuantitySurveyEscalationCalculation) =>
    apiService.post<QuantitySurveyEscalationCalculation>(root, request),
  lifecycle: (
    id: string,
    action: 'submit' | 'approve' | 'reject',
    rowVersion: string,
    reason: string
  ) =>
    apiService.post<QuantitySurveyEscalationCalculation>(
      `${root}/${id}/${action}`,
      { rowVersion, reason }
    ),
  reviewAdjustment: (
    id: string,
    rowVersion: string,
    adjustmentAmount: number,
    reason: string
  ) =>
    apiService.post<QuantitySurveyEscalationCalculation>(
      `${root}/${id}/review-adjustment`,
      { rowVersion, adjustmentAmount, reason }
    ),
  history: (id: string) =>
    apiService.get<QuantitySurveyEscalationCalculationRevision[]>(
      `${root}/${id}/history`
    ),
};
