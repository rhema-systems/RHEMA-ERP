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
    WHTSummaryEntry,
    WhtCertificate,
    WhtCertificateQuery,
    GenerateWhtCertificateDto,
    FinancePagedResult,
    TaxConfigurationVersion,
    WhtCalculationRequest,
    WhtCalculationResult,
    WhtRemittance,
    WhtRemittanceLiability
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

interface TaxWithholdingReportLine {
    sourceDocumentId: string;
    counterpartyName?: string | null;
    taxCode?: string | null;
    taxName?: string | null;
    taxRate: number;
    taxableBase: number;
    withholdingAmount: number;
}

interface TaxWithholdingReport {
    lines: TaxWithholdingReportLine[];
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

    async getTaxConfigurationVersions(id: string): Promise<TaxConfigurationVersion[]> {
        return apiService.get<TaxConfigurationVersion[]>(`/finance/tax/taxes/${id}/versions`);
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

    async toggleTaxStatus(id: string, isActive: boolean): Promise<Tax> {
        // Activation is part of the tax update contract (UpdateTaxDto.IsActive);
        // there is no dedicated toggle endpoint on the backend.
        return apiService.put<Tax>(`/finance/tax/taxes/${id}`, { isActive });
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

    async getActiveTaxGroups(applicability?: string): Promise<TaxGroup[]> {
        const queryParams = new URLSearchParams();
        if (applicability) queryParams.append('applicability', applicability);

        // The general groups endpoint intentionally returns the administrative list.
        // Transaction entry screens must use the active endpoint so inactive or
        // wrong-applicability tax groups are not offered to users.
        const endpoint = `/finance/tax/groups/active${queryParams.toString() ? `?${queryParams}` : ''}`;
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

    async toggleTaxGroupStatus(id: string, isActive: boolean): Promise<TaxGroup> {
        // Activation is part of the tax group update contract (UpdateTaxGroupDto.IsActive).
        return apiService.put<TaxGroup>(`/finance/tax/groups/${id}`, { isActive });
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
        const report = await apiService.post<TaxWithholdingReport>(
            '/finance/tax-reports/wht-payable',
            this.buildTaxReportRequest(startDate, endDate));
        const groups = new Map<string, WHTSummaryEntry & { documents: Set<string> }>();

        for (const line of report.lines ?? []) {
            const supplierName = line.counterpartyName?.trim() || 'Unknown supplier';
            const taxType = line.taxCode?.trim() || line.taxName?.trim() || 'WHT';
            const key = `${supplierName}\u0000${taxType}\u0000${line.taxRate}`;
            const item = groups.get(key) ?? {
                supplierName,
                taxType,
                transactionCount: 0,
                grossAmount: 0,
                whtRate: line.taxRate,
                whtAmount: 0,
                netAmount: 0,
                documents: new Set<string>(),
            };
            item.documents.add(line.sourceDocumentId);
            item.grossAmount += line.taxableBase;
            item.whtAmount += line.withholdingAmount;
            item.netAmount += line.taxableBase - line.withholdingAmount;
            groups.set(key, item);
        }

        return Array.from(groups.values())
            .map(({ documents, ...item }) => ({ ...item, transactionCount: documents.size }))
            .sort((left, right) => left.supplierName.localeCompare(right.supplierName)
                || left.taxType.localeCompare(right.taxType));
    }

    async getWhtCertificates(query: WhtCertificateQuery = {}): Promise<FinancePagedResult<WhtCertificate>> {
        // Keep the route literal visible to the Finance route-contract test and let the
        // shared API client encode optional query values consistently.
        return apiService.get<FinancePagedResult<WhtCertificate>>('/finance/tax/wht-certificates', {
            page: query.page,
            pageSize: query.pageSize,
            searchTerm: query.searchTerm,
            supplierId: query.supplierId,
            fromDate: query.fromDate,
            toDate: query.toDate,
            status: query.status && query.status !== 'All' ? query.status : undefined,
        });
    }

    async generateWhtCertificate(
        vendorPaymentId: string,
        request: GenerateWhtCertificateDto = {}
    ): Promise<WhtCertificate> {
        return apiService.post<WhtCertificate>(
            `/finance/tax/wht-certificates/${vendorPaymentId}/generate`,
            request);
    }

    async reissueWhtCertificate(vendorPaymentId: string, reason: string): Promise<WhtCertificate> {
        return apiService.post<WhtCertificate>(
            `/finance/tax/wht-certificates/${vendorPaymentId}/reissue`,
            { reason });
    }

    async cancelWhtCertificate(vendorPaymentId: string, reason: string): Promise<WhtCertificate> {
        return apiService.post<WhtCertificate>(
            `/finance/tax/wht-certificates/${vendorPaymentId}/cancel`,
            { reason });
    }

    async calculateApWithholding(request: WhtCalculationRequest): Promise<WhtCalculationResult> {
        return apiService.post<WhtCalculationResult>('/finance/tax/wht-certificates/calculate', request);
    }

    async getUnremittedWhtLiabilities(fromDate?: string, toDate?: string, currencyCode = 'GHS'): Promise<WhtRemittanceLiability[]> {
        return apiService.get<WhtRemittanceLiability[]>('/finance/tax/wht-certificates/remittances/liabilities', {
            fromDate,
            toDate,
            currencyCode,
        });
    }

    async getWhtRemittances(query: Record<string, unknown> = {}): Promise<FinancePagedResult<WhtRemittance>> {
        return apiService.get<FinancePagedResult<WhtRemittance>>('/finance/tax/wht-certificates/remittances', query);
    }

    async createWhtRemittance(request: {
        periodFrom: string;
        periodTo: string;
        dueDate?: string;
        currencyCode: string;
        vendorPaymentIds: string[];
        notes?: string;
    }): Promise<WhtRemittance> {
        return apiService.post<WhtRemittance>('/finance/tax/wht-certificates/remittances', request);
    }

    async submitWhtRemittance(id: string, submissionReference: string): Promise<WhtRemittance> {
        return apiService.post<WhtRemittance>(`/finance/tax/wht-certificates/remittances/${id}/submit`, {
            submissionReference,
        });
    }

    async markWhtRemittancePaid(id: string, request: {
        paymentDate: string;
        paymentReference: string;
        authorityReceiptReference?: string;
    }): Promise<WhtRemittance> {
        return apiService.post<WhtRemittance>(`/finance/tax/wht-certificates/remittances/${id}/paid`, request);
    }

    async cancelWhtRemittance(id: string, reason: string): Promise<WhtRemittance> {
        return apiService.post<WhtRemittance>(`/finance/tax/wht-certificates/remittances/${id}/cancel`, { reason });
    }

    async downloadWhtRegister(fromDate?: string, toDate?: string): Promise<Blob> {
        const query = new URLSearchParams();
        if (fromDate) query.set('fromDate', fromDate);
        if (toDate) query.set('toDate', toDate);
        return apiService.downloadBlob(`/finance/tax/wht-certificates/register/export${query.size ? `?${query}` : ''}`);
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
