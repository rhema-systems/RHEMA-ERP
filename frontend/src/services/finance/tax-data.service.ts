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

// Request/response contracts for the POST /finance/tax-reports/* endpoints
// (TaxReportRequestDto / GhanaTaxSnapshotReportDto on the backend).
interface TaxSnapshotReportRequest {
    fromDate?: string;
    toDate?: string;
}

interface TaxSnapshotReportLine {
    taxCalculationId: string;
    sourceModule: string;
    sourceDocumentType: string;
    sourceDocumentId: string;
    sourceDocumentNumber: string;
    sourceDocumentDate: string;
    counterpartyId?: string | null;
    counterpartyName?: string | null;
    taxCode: string;
    taxName: string;
    baseAmount: number;
    taxableAmount: number;
    taxRate: number;
    taxAmount: number;
}

interface TaxSnapshotReport {
    reportType: string;
    fromDate: string;
    toDate: string;
    lines: TaxSnapshotReportLine[];
}

export interface VATReconciliationSummary {
    period: string;
    outputVAT: number;
    inputVAT: number;
    netVATPayable: number;
    outputNHIL: number;
    inputNHIL: number;
    netNHILPayable: number;
    outputGETFL: number;
    inputGETFL: number;
    netGETFLPayable: number;
    totalPayable: number;
}

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

    // ===== REPORTS =====
    // The Ghana tax reports controller exposes POST /finance/tax-reports/* endpoints that read
    // posted tax snapshots; these helpers adapt snapshot lines to the shapes the report pages render.

    private buildTaxReportRequest(startDate?: string, endDate?: string): TaxSnapshotReportRequest {
        return {
            fromDate: startDate || undefined,
            toDate: endDate || undefined,
        };
    }

    private groupSnapshotLinesByDocument(lines: TaxSnapshotReportLine[]): TransactionWithTax[] {
        const byDocument = new Map<string, TransactionWithTax>();

        for (const line of lines) {
            let transaction = byDocument.get(line.sourceDocumentId);
            if (!transaction) {
                transaction = {
                    id: line.sourceDocumentId,
                    date: (line.sourceDocumentDate ?? '').slice(0, 10),
                    reference: line.sourceDocumentNumber,
                    partnerName: line.counterpartyName ?? '',
                    description: `${line.sourceModule} ${line.sourceDocumentType}`.trim(),
                    baseAmount: line.baseAmount,
                    totalTax: 0,
                    totalAmount: 0,
                    taxCalculations: [],
                };
                byDocument.set(line.sourceDocumentId, transaction);
            }

            transaction.taxCalculations.push({ taxTypeCode: line.taxCode, taxAmount: line.taxAmount });
            transaction.totalTax += line.taxAmount;
        }

        const transactions = Array.from(byDocument.values());
        for (const transaction of transactions) {
            transaction.totalAmount = transaction.baseAmount + transaction.totalTax;
        }

        return transactions.sort((a, b) => a.date.localeCompare(b.date) || a.reference.localeCompare(b.reference));
    }

    private static sumTaxByCode(lines: TaxSnapshotReportLine[], code: string): number {
        return lines
            .filter((line) => line.taxCode.toUpperCase().includes(code))
            .reduce((sum, line) => sum + line.taxAmount, 0);
    }

    async getPurchaseTransactions(startDate?: string, endDate?: string): Promise<TransactionWithTax[]> {
        const report = await apiService.post<TaxSnapshotReport>(
            '/finance/tax-reports/input-tax',
            this.buildTaxReportRequest(startDate, endDate));

        return this.groupSnapshotLinesByDocument(report.lines ?? []);
    }

    async getSalesTransactions(startDate?: string, endDate?: string): Promise<TransactionWithTax[]> {
        const report = await apiService.post<TaxSnapshotReport>(
            '/finance/tax-reports/output-tax',
            this.buildTaxReportRequest(startDate, endDate));

        return this.groupSnapshotLinesByDocument(report.lines ?? []);
    }

    async getVATReconciliation(startDate?: string, endDate?: string): Promise<VATReconciliationSummary> {
        const request = this.buildTaxReportRequest(startDate, endDate);
        const [outputReport, inputReport] = await Promise.all([
            apiService.post<TaxSnapshotReport>('/finance/tax-reports/output-tax', request),
            apiService.post<TaxSnapshotReport>('/finance/tax-reports/input-tax', request),
        ]);

        const outputLines = outputReport.lines ?? [];
        const inputLines = inputReport.lines ?? [];

        const outputVAT = TaxDataService.sumTaxByCode(outputLines, 'VAT');
        const inputVAT = TaxDataService.sumTaxByCode(inputLines, 'VAT');
        const outputNHIL = TaxDataService.sumTaxByCode(outputLines, 'NHIL');
        const inputNHIL = TaxDataService.sumTaxByCode(inputLines, 'NHIL');
        const outputGETFL = TaxDataService.sumTaxByCode(outputLines, 'GETF');
        const inputGETFL = TaxDataService.sumTaxByCode(inputLines, 'GETF');

        const netVATPayable = outputVAT - inputVAT;
        const netNHILPayable = outputNHIL - inputNHIL;
        const netGETFLPayable = outputGETFL - inputGETFL;

        return {
            period: startDate && endDate ? `${startDate} to ${endDate}` : 'All posted activity',
            outputVAT,
            inputVAT,
            netVATPayable,
            outputNHIL,
            inputNHIL,
            netNHILPayable,
            outputGETFL,
            inputGETFL,
            netGETFLPayable,
            totalPayable: netVATPayable + netNHILPayable + netGETFLPayable,
        };
    }

    async getWHTSummary(startDate?: string, endDate?: string): Promise<WHTSummaryEntry[]> {
        const queryParams = new URLSearchParams();
        if (startDate) queryParams.append('fromDate', startDate);
        if (endDate) queryParams.append('toDate', endDate);

        const report = await apiService.get<{
            bySupplier?: Array<{
                supplierName: string;
                taxId?: string | null;
                totalInvoiceAmount: number;
                totalWithholdingTax: number;
                totalNetPayment: number;
                transactionCount: number;
            }>;
        }>(`/ap/reports/withholding-tax${queryParams.toString() ? `?${queryParams}` : ''}`);

        return (report.bySupplier || []).map(supplier => ({
            supplierName: supplier.supplierName,
            supplierTIN: supplier.taxId || undefined,
            taxType: 'WHT',
            transactionCount: supplier.transactionCount,
            grossAmount: supplier.totalInvoiceAmount,
            whtRate: supplier.totalInvoiceAmount > 0
                ? (supplier.totalWithholdingTax / supplier.totalInvoiceAmount) * 100
                : 0,
            whtAmount: supplier.totalWithholdingTax,
            netAmount: supplier.totalNetPayment,
        }));
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
