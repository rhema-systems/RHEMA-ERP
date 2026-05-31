/**
 * Tax Data Service
 * Service layer for simplified tax module API calls
 */

import type {
    Tax,
    TaxGroup,
    TaxGroupComponent,
    CreateTaxDto,
    UpdateTaxDto,
    CreateTaxGroupDto,
    UpdateTaxGroupDto,
    CreateTaxGroupComponentDto,
    UpdateTaxGroupComponentDto,
    TaxCalculationRequest,
    TaxCalculationResult,
    TaxThresholdStatus,
    TaxRule,
    CreateTaxRuleDto,
    UpdateTaxRuleDto,
    TransactionWithTax,
    WHTSummaryEntry
} from '@/types/tax';
import { apiService } from '@/services/api.service';

// =============================================================================
// TAX DATA SERVICE
// =============================================================================

class TaxDataService {
    // ===== TAXES =====

    async getTaxes(filters?: {
        isActive?: boolean;
        applicability?: string;
        category?: string;
    }): Promise<Tax[]> {
        const queryParams = new URLSearchParams();
        if (filters?.isActive !== undefined) queryParams.append('isActive', String(filters.isActive));
        if (filters?.applicability) queryParams.append('applicability', filters.applicability);
        if (filters?.category) queryParams.append('category', filters.category);

        const endpoint = `/finance/tax/taxes${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<Tax[]>(endpoint);
    }

    async getTaxById(id: string): Promise<Tax> {
        return apiService.get<Tax>(`/finance/tax/taxes/${id}`);
    }

    async createTax(dto: CreateTaxDto): Promise<Tax> {
        return apiService.post<Tax>('/finance/tax/taxes', dto);
    }

    async updateTax(id: string, dto: UpdateTaxDto): Promise<Tax> {
        return apiService.put<Tax>(`/finance/tax/taxes/${id}`, dto);
    }

    async deleteTax(id: string): Promise<void> {
        return apiService.delete(`/finance/tax/taxes/${id}`);
    }

    async toggleTaxStatus(id: string): Promise<Tax> {
        return apiService.patch<Tax>(`/finance/tax/taxes/${id}/toggle-status`, {});
    }

    // ===== TAX GROUPS =====

    async getTaxGroups(filters?: {
        isActive?: boolean;
        applicability?: string;
        isDefault?: boolean;
    }): Promise<TaxGroup[]> {
        const queryParams = new URLSearchParams();
        if (filters?.isActive !== undefined) queryParams.append('isActive', String(filters.isActive));
        if (filters?.applicability) queryParams.append('applicability', filters.applicability);
        if (filters?.isDefault !== undefined) queryParams.append('isDefault', String(filters.isDefault));

        const endpoint = `/finance/tax/groups${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<TaxGroup[]>(endpoint);
    }

    async getTaxGroupById(id: string): Promise<TaxGroup> {
        return apiService.get<TaxGroup>(`/finance/tax/groups/${id}`);
    }

    async createTaxGroup(dto: CreateTaxGroupDto): Promise<TaxGroup> {
        return apiService.post<TaxGroup>('/finance/tax/groups', dto);
    }

    async updateTaxGroup(id: string, dto: UpdateTaxGroupDto): Promise<TaxGroup> {
        return apiService.put<TaxGroup>(`/finance/tax/groups/${id}`, dto);
    }

    async deleteTaxGroup(id: string): Promise<void> {
        return apiService.delete(`/finance/tax/groups/${id}`);
    }

    async toggleTaxGroupStatus(id: string): Promise<TaxGroup> {
        return apiService.patch<TaxGroup>(`/finance/tax/groups/${id}/toggle-status`, {});
    }

    // ===== TAX GROUP COMPONENTS =====

    async addComponent(groupId: string, dto: CreateTaxGroupComponentDto): Promise<TaxGroupComponent> {
        return apiService.post<TaxGroupComponent>(`/finance/tax/groups/${groupId}/components`, dto);
    }

    async updateComponent(groupId: string, componentId: string, dto: UpdateTaxGroupComponentDto): Promise<TaxGroupComponent> {
        return apiService.put<TaxGroupComponent>(`/finance/tax/groups/components/${componentId}`, dto);
    }

    async removeComponent(groupId: string, componentId: string): Promise<void> {
        return apiService.delete(`/finance/tax/groups/components/${componentId}`);
    }

    // ===== TAX CALCULATIONS =====

    async calculateTax(request: TaxCalculationRequest): Promise<TaxCalculationResult> {
        return apiService.post<TaxCalculationResult>('/finance/tax/calculate', request);
    }

    async validateTaxCalculation(request: TaxCalculationRequest): Promise<boolean> {
        // Implementation might vary based on backend requirements
        return true;
    }

    // ===== TAX RATE HISTORY =====
    async getTaxRateHistory(taxId: string): Promise<any[]> {
        return apiService.get<any[]>(`/finance/tax/taxes/${taxId}/history`);
    }

    // ===== SEEDING =====
    async seedGhanaTaxes(): Promise<void> {
        return apiService.post<void>('/finance/tax/seed/ghana', {});
    }

    // ===== REPORTS (Mock/Placeholder until Backend Implemented) =====
    async getPurchaseTransactions(): Promise<any[]> { // Should be TransactionWithTax[]
        // TODO: Implement backend endpoint /finance/reports/input-vat
        return [];
    }

    async getSalesTransactions(): Promise<any[]> { // Should be TransactionWithTax[]
        // TODO: Implement backend endpoint /finance/reports/output-vat
        return [];
    }

    async getWHTSummary(startDate?: string, endDate?: string): Promise<any[]> { // Should be WHTSummaryEntry[]
        // TODO: Implement backend endpoint /finance/reports/wht-summary
        return [];
    }

    // ===== TAX RULES =====

    async getTaxRules(): Promise<TaxRule[]> {
        return apiService.get<TaxRule[]>('/finance/tax/rules');
    }

    async getTaxRuleById(id: string): Promise<TaxRule> {
        return apiService.get<TaxRule>(`/finance/tax/rules/${id}`);
    }

    async createTaxRule(dto: CreateTaxRuleDto): Promise<TaxRule> {
        return apiService.post<TaxRule>('/finance/tax/rules', dto);
    }

    async updateTaxRule(id: string, dto: UpdateTaxRuleDto): Promise<void> {
        return apiService.put<void>(`/finance/tax/rules/${id}`, dto);
    }

    async deleteTaxRule(id: string): Promise<void> {
        return apiService.delete(`/finance/tax/rules/${id}`);
    }

    async seedTaxRules(): Promise<void> {
        return apiService.post('/finance/tax/rules/seed', {});
    }
}

export const taxDataService = new TaxDataService();
