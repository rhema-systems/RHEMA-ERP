/**
 * Capital Projects Data Service
 */

import { apiService } from '@/services/api.service';

// ── Types ──────────────────────────────────────────────────────────

export type ProjectStatus = 'Planning' | 'InProgress' | 'OnHold' | 'Completed' | 'Cancelled';
export type ProjectCostSourceType = 'VendorInvoice' | 'ExpenseRecord' | 'InventoryDispatch' | 'ManualJournal';

export interface CapitalProjectList {
  id: string;
  projectCode: string;
  name: string;
  startDate: string;
  targetCompletionDate?: string;
  totalBudgetAmount: number;
  totalAccumulatedCost: number;
  capitalizedAmount: number;
  status: ProjectStatus;
  createdAt: string;
}

export interface ProjectCostLine {
  id: string;
  sourceDocumentType: ProjectCostSourceType;
  sourceDocumentId?: string;
  sourceDocumentReference?: string;
  amount: number;
  transactionDate: string;
  description?: string;
  createdAt: string;
  createdBy?: string;
}

export interface ProjectSettlementRule {
  id: string;
  targetFixedAssetCategoryId: string;
  targetFixedAssetCategoryName?: string;
  proposedAssetName: string;
  allocationPercentage: number;
  allocatedAmount: number;
  resultingFixedAssetId?: string;
}

export interface CapitalProjectDetail extends CapitalProjectList {
  description?: string;
  actualCompletionDate?: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
  costLines: ProjectCostLine[];
  settlementRules: ProjectSettlementRule[];
}

export interface CreateCapitalProjectDto {
  projectCode: string;
  name: string;
  description?: string;
  startDate: string;
  targetCompletionDate?: string;
  totalBudgetAmount: number;
}

export interface AddProjectCostDto {
  sourceDocumentType: ProjectCostSourceType;
  sourceDocumentId?: string;
  sourceDocumentReference?: string;
  amount: number;
  transactionDate: string;
  description?: string;
}

export interface AddSettlementRuleDto {
  targetFixedAssetCategoryId: string;
  proposedAssetName: string;
  allocationPercentage: number;
}

// ── Service ──────────────────────────────────────────────────────

class CapitalProjectService {
  async getAll(): Promise<CapitalProjectList[]> {
    return apiService.get<CapitalProjectList[]>('/finance/capital-projects');
  }

  async getById(id: string): Promise<CapitalProjectDetail> {
    return apiService.get<CapitalProjectDetail>(`/finance/capital-projects/${id}`);
  }

  async create(dto: CreateCapitalProjectDto): Promise<CapitalProjectDetail> {
    return apiService.post<CapitalProjectDetail>('/finance/capital-projects', dto);
  }

  async update(id: string, dto: Partial<CreateCapitalProjectDto>): Promise<CapitalProjectDetail> {
    return apiService.put<CapitalProjectDetail>(`/finance/capital-projects/${id}`, dto);
  }

  async delete(id: string): Promise<void> {
    return apiService.delete(`/finance/capital-projects/${id}`);
  }

  async updateStatus(id: string, status: ProjectStatus, reason?: string): Promise<CapitalProjectDetail> {
    return apiService.patch<CapitalProjectDetail>(`/finance/capital-projects/${id}/status`, { status, reason });
  }

  async postCost(id: string, dto: AddProjectCostDto): Promise<CapitalProjectDetail> {
    return apiService.post<CapitalProjectDetail>(`/finance/capital-projects/${id}/costs`, dto);
  }

  async removeCost(id: string, costId: string): Promise<CapitalProjectDetail> {
    return apiService.delete(`/finance/capital-projects/${id}/costs/${costId}`);
  }

  async addSettlementRule(id: string, dto: AddSettlementRuleDto): Promise<CapitalProjectDetail> {
    return apiService.post<CapitalProjectDetail>(`/finance/capital-projects/${id}/settlement-rules`, dto);
  }

  async removeSettlementRule(id: string, ruleId: string): Promise<CapitalProjectDetail> {
    return apiService.delete(`/finance/capital-projects/${id}/settlement-rules/${ruleId}`);
  }

  async capitalize(id: string): Promise<CapitalProjectDetail> {
    return apiService.post<CapitalProjectDetail>(`/finance/capital-projects/${id}/capitalize`);
  }
}

export const capitalProjectService = new CapitalProjectService();
