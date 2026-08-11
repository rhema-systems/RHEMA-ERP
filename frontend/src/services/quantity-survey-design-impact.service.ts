import { apiService } from '@/services/api.service';

const root = '/quantity-survey/design-revision-impacts';

export type DesignImpactRoute = 'Measurement' | 'Variation' | 0 | 1;
export type DesignImpactType =
  | 'RemeasurementRequired'
  | 'QuantityIncrease'
  | 'QuantityDecrease'
  | 'ScopeAddition'
  | 'Omission'
  | 'RateReviewOnly'
  | number;

export interface DesignImpactLookup {
  id: string;
  label: string;
  group?: string | null;
  description?: string | null;
  supersedesDrawingId?: string | null;
}

export interface DesignImpactLookups {
  drawings: DesignImpactLookup[];
  approvedBoqLines: DesignImpactLookup[];
}

export interface DesignImpactLineInput {
  projectBoqVersionLineId: string;
  impactType: DesignImpactType;
  indicativeQuantity?: number;
  impactReason: string;
}

export interface DesignImpactLine extends DesignImpactLineInput {
  id: string;
  boqLineKey: string;
  lineReference: string;
  description: string;
  unit?: string | null;
  previousQuantity: number;
}

export interface DesignImpact {
  id: string;
  projectId: string;
  previousDrawingId: string;
  revisedDrawingId: string;
  previousDrawingLabel: string;
  revisedDrawingLabel: string;
  impactNumber: string;
  title: string;
  changeSummary: string;
  route: DesignImpactRoute;
  status: string;
  approvalStatus: string;
  workflowInstanceId?: string | null;
  submittedAt?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  rowVersion: string;
  lines: DesignImpactLine[];
}

export interface CreateDesignImpactRequest {
  clientRequestId: string;
  projectId: string;
  previousDrawingId: string;
  revisedDrawingId: string;
  route: 'Measurement' | 'Variation';
  title: string;
  changeSummary: string;
  lines: DesignImpactLineInput[];
}

export interface DesignImpactLifecycleRequest {
  clientRequestId: string;
  reason: string;
  rowVersion: string;
}

export const quantitySurveyDesignImpactService = {
  lookups: (projectId: string) => apiService.get<DesignImpactLookups>(`${root}/lookups`, { projectId }),
  list: (projectId: string) => apiService.get<DesignImpact[]>(root, { projectId }),
  create: (request: CreateDesignImpactRequest) => apiService.post<DesignImpact>(root, request),
  submit: (id: string, request: DesignImpactLifecycleRequest) => apiService.post<DesignImpact>(`${root}/${id}/submit`, request),
  approve: (id: string, request: DesignImpactLifecycleRequest) => apiService.post<DesignImpact>(`${root}/${id}/approve`, request),
  reject: (id: string, request: DesignImpactLifecycleRequest) => apiService.post<DesignImpact>(`${root}/${id}/reject`, request),
};
