import { apiService } from './api.service';
import { DOCUMENT_TYPES, documentOutputService } from './document-output.service';
import type {
    VendorInvoice,
    VendorInvoiceDistribution,
    ApBudgetCell,
    VendorInvoiceCreateRequest,
    VendorInvoiceUpdateRequest,
    VendorPayment,
    VendorPaymentCreateRequest,
    SubmitVendorPaymentRequest,
    VendorPaymentControl,
    VendorPaymentAllocationCreateRequest,
    ReverseVendorPaymentRequest,
    VendorPaymentTrace,
    PaymentBatch,
    PaymentBatchCreateRequest,
    ApAgingReport,
    SupplierDetailedLedgerReport,
    CashRequirementForecast,
    ApSummaryStats,
    OutstandingVendorInvoice,
    InvoiceMatchingResult,
    VendorPaymentInvoiceReadiness,
    InvoicePaymentSodReadiness,
    VendorInvoiceMatchException,
    VendorInvoiceMatchExceptionOverview,
    CreateVendorInvoiceMatchExceptionRequest,
    VendorInvoiceMatchExceptionEvidenceRequest,
    VendorInvoiceMatchExceptionReport,
    VendorInvoiceMatchExceptionStatus,
    ProcurementFinanceReconciliationReport,
    ProcurementAcceptedSupplyOptions,
    ApInvoiceSupplier,
    ApInvoiceSupplierEntryOption,
    ApGoodsInvoiceEntry,
    SupplierDebitNote,
    SupplierDebitNoteCreateRequest,
    SupplierDebitNoteUpdateRequest,
    SupplierDebitNoteStatus,
    SupplierDebitNoteApplication,
    SupplierDebitNoteApplicationRequest,
    SupplierDebitNoteApplicationResult
} from '../types/ap';
import type { FinanceSourceDocumentDimension } from '../types/finance';
import type { PurchaseOrderSupplierDefaultsDto } from './purchasingService';

// Re-using the PagedResult structure from ar-service
export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

export interface VendorInvoiceQuery {
    procurementOnly?: boolean;
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    businessPartnerId?: string;
    status?: string;
    approvalStatus?: string;
    matchingStatus?: string;
    fromDate?: string;
    toDate?: string;
    dueFromDate?: string;
    dueToDate?: string;
    overdueOnly?: boolean;
    isOpeningBalance?: boolean;
    sortBy?: string;
    sortDescending?: boolean;
}

export interface VendorPaymentQuery {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    businessPartnerId?: string;
    status?: string;
    paymentMethod?: string;
    paymentMethodId?: string;
    paymentBatchId?: string;
    fromDate?: string;
    toDate?: string;
    sortBy?: string;
    sortDescending?: boolean;
}

export interface PaymentBatchQuery {
    page?: number;
    pageSize?: number;
    status?: string;
    fromDate?: string;
    toDate?: string;
    sortBy?: string;
    sortDescending?: boolean;
}

export interface SupplierDebitNoteQuery {
    inventoryPurchaseReturnId?: string;
    vendorId?: string;
    supplierId?: string;
    originalVendorInvoiceId?: string;
    status?: SupplierDebitNoteStatus;
    fromDate?: string;
    toDate?: string;
    search?: string;
}

export interface InvoiceSupplierDefaults extends PurchaseOrderSupplierDefaultsDto {
    withholdingDefault?: {
        required: boolean;
        taxId?: string | null;
        rate: number;
        taxPayableAccountId?: string | null;
        message?: string | null;
    };
}

export class AccountsPayableService {
    constructor(private readonly invoiceBaseUrl = '/ap/invoices') {}
    public async getInvoiceSupplierDefaults(businessPartnerId: string, purchaseOrderId?: string, invoiceDate?: string, businessPartnerRoleId?: string): Promise<InvoiceSupplierDefaults | null> {
        const query = new URLSearchParams({ businessPartnerId });
        if (purchaseOrderId) query.set('purchaseOrderId', purchaseOrderId);
        if (invoiceDate) query.set('invoiceDate', invoiceDate);
        if (businessPartnerRoleId) query.set('businessPartnerRoleId', businessPartnerRoleId);
        return apiService.get<InvoiceSupplierDefaults | null>(`${this.invoiceBaseUrl}/supplier-defaults?${query}`);
    }
    private readonly baseUrl = '/ap';

    // --- Vendor Invoices ---

    public async getInvoices(query: VendorInvoiceQuery = {}): Promise<PagedResult<VendorInvoice>> {
        const params = new URLSearchParams();
        if (query.procurementOnly) params.append('ProcurementOnly', 'true');
        if (query.page) params.append('Page', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.searchTerm) params.append('SearchTerm', query.searchTerm);
        if (query.businessPartnerId) params.append('BusinessPartnerId', query.businessPartnerId);
        if (query.status) params.append('Status', query.status);
        if (query.approvalStatus) params.append('ApprovalStatus', query.approvalStatus);
        if (query.matchingStatus) params.append('MatchingStatus', query.matchingStatus);
        if (query.fromDate) params.append('FromDate', query.fromDate);
        if (query.toDate) params.append('ToDate', query.toDate);
        if (query.dueFromDate) params.append('DueFromDate', query.dueFromDate);
        if (query.dueToDate) params.append('DueToDate', query.dueToDate);
        if (query.overdueOnly !== undefined) params.append('OverdueOnly', query.overdueOnly.toString());
        if (query.isOpeningBalance !== undefined) params.append('IsOpeningBalance', query.isOpeningBalance.toString());
        if (query.sortBy) params.append('SortBy', query.sortBy);
        if (query.sortDescending !== undefined) params.append('SortDescending', query.sortDescending.toString());

        return apiService.get<PagedResult<VendorInvoice>>(`${this.invoiceBaseUrl}?${params.toString()}`);
    }

    public async getInvoice(id: string): Promise<VendorInvoice> {
        return apiService.get<VendorInvoice>(`${this.invoiceBaseUrl}/${id}`);
    }

    /** Returns canonical Supplier.Id values through a tenant-scoped Finance read model. */
    public async getInvoiceSuppliers(): Promise<ApInvoiceSupplier[]> {
        return apiService.get<ApInvoiceSupplier[]>(`${this.invoiceBaseUrl}/suppliers`);
    }

    public async getInvoiceDistribution(id: string): Promise<VendorInvoiceDistribution> {
        return apiService.get<VendorInvoiceDistribution>(`${this.invoiceBaseUrl}/${id}/distribution`);
    }

    /** Returns canonical Business Partner AP roles, including corrective readiness feedback. */
    public async getInvoiceSupplierEntryOptions(): Promise<ApInvoiceSupplierEntryOption[]> {
        return apiService.get<ApInvoiceSupplierEntryOption[]>(`${this.invoiceBaseUrl}/entry-suppliers`);
    }

    public async getGoodsInvoiceEntry(purchaseOrderId: string, currentInvoiceId?: string): Promise<ApGoodsInvoiceEntry> {
        const query = new URLSearchParams({ purchaseOrderId });
        if (currentInvoiceId) query.set('currentInvoiceId', currentInvoiceId);
        return apiService.get<ApGoodsInvoiceEntry>(`${this.invoiceBaseUrl}/goods-entry?${query}`);
    }

    public async getInvoiceBudgetCells(budgetDate: string, accountId: string): Promise<ApBudgetCell[]> {
        const query = new URLSearchParams({ budgetDate, accountId });
        return apiService.get<ApBudgetCell[]>(`${this.invoiceBaseUrl}/budget-cells?${query.toString()}`);
    }

    public async createInvoice(data: VendorInvoiceCreateRequest): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.invoiceBaseUrl}`, data);
    }

    public async getAcceptedSupplyOptions(
        purchaseOrderId: string
    ): Promise<ProcurementAcceptedSupplyOptions> {
        return apiService.get<ProcurementAcceptedSupplyOptions>(
            `${this.invoiceBaseUrl}/accepted-supply-options?purchaseOrderId=${encodeURIComponent(purchaseOrderId)}`
        );
    }

    public async updateInvoice(id: string, data: VendorInvoiceUpdateRequest): Promise<VendorInvoice> {
        return apiService.put<VendorInvoice>(`${this.invoiceBaseUrl}/${id}`, data);
    }

    public async approveInvoice(id: string, comments?: string): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.invoiceBaseUrl}/${id}/approve`, comments || 'Approved');
    }

    public async submitInvoiceForApproval(id: string): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.invoiceBaseUrl}/${id}/submit`, {});
    }

    public async refreshInvoiceBudget(id: string): Promise<FinanceSourceDocumentDimension> {
        return apiService.post<FinanceSourceDocumentDimension>(
            `${this.invoiceBaseUrl}/${id}/budget-refresh`,
            {},
        );
    }

    public async getThreeWayMatchReadiness(id: string): Promise<InvoiceMatchingResult> {
        return apiService.get<InvoiceMatchingResult>(`${this.invoiceBaseUrl}/${id}/match/readiness`);
    }

    public async performThreeWayMatch(id: string): Promise<InvoiceMatchingResult> {
        return apiService.post<InvoiceMatchingResult>(`${this.invoiceBaseUrl}/${id}/match/three-way`, {});
    }

    public async getMatchExceptionOverview(id: string): Promise<VendorInvoiceMatchExceptionOverview> {
        return apiService.get<VendorInvoiceMatchExceptionOverview>(`${this.invoiceBaseUrl}/${id}/match-exceptions`);
    }

    public async requestMatchException(
        id: string,
        data: CreateVendorInvoiceMatchExceptionRequest
    ): Promise<VendorInvoiceMatchException> {
        return apiService.post<VendorInvoiceMatchException>(`${this.invoiceBaseUrl}/${id}/match-exceptions`, data);
    }

    public async decideMatchException(
        exceptionId: string,
        approved: boolean,
        comment: string,
        rowVersion: string
    ): Promise<VendorInvoiceMatchException> {
        return apiService.post<VendorInvoiceMatchException>(`${this.invoiceBaseUrl}/match-exceptions/${exceptionId}/decision`, {
            approved,
            comment,
            rowVersion,
        });
    }

    public async cancelMatchException(
        exceptionId: string,
        reason: string,
        rowVersion: string
    ): Promise<VendorInvoiceMatchException> {
        return apiService.post<VendorInvoiceMatchException>(`${this.invoiceBaseUrl}/match-exceptions/${exceptionId}/cancel`, {
            reason,
            rowVersion,
        });
    }

    public async completeMatchExceptionCorrectiveAction(
        exceptionId: string,
        completionNote: string,
        rowVersion: string,
        evidence: VendorInvoiceMatchExceptionEvidenceRequest[]
    ): Promise<VendorInvoiceMatchException> {
        return apiService.post<VendorInvoiceMatchException>(
            `${this.invoiceBaseUrl}/match-exceptions/${exceptionId}/corrective-action/complete`,
            { completionNote, rowVersion, evidence }
        );
    }

    public async rejectInvoice(id: string, comments: string): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.invoiceBaseUrl}/${id}/reject`, comments);
    }

    public async voidInvoice(id: string, comments?: string): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.invoiceBaseUrl}/${id}/void`, comments || 'Voided by user');
    }

    // --- Vendor Payments ---

    public async getPayments(query: VendorPaymentQuery = {}): Promise<PagedResult<VendorPayment>> {
        const params = new URLSearchParams();
        if (query.page) params.append('Page', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.searchTerm) params.append('SearchTerm', query.searchTerm);
        if (query.businessPartnerId) params.append('BusinessPartnerId', query.businessPartnerId);
        if (query.status) params.append('Status', query.status);
        if (query.paymentMethod) params.append('PaymentMethod', query.paymentMethod);
        if (query.paymentMethodId) params.append('PaymentMethodId', query.paymentMethodId);
        if (query.paymentBatchId) params.append('PaymentBatchId', query.paymentBatchId);
        if (query.fromDate) params.append('FromDate', query.fromDate);
        if (query.toDate) params.append('ToDate', query.toDate);
        if (query.sortBy) params.append('SortBy', query.sortBy);
        if (query.sortDescending !== undefined) params.append('SortDescending', query.sortDescending.toString());

        return apiService.get<PagedResult<VendorPayment>>(`${this.baseUrl}/payments?${params.toString()}`);
    }

    public async getPayment(id: string): Promise<VendorPayment> {
        return apiService.get<VendorPayment>(`${this.baseUrl}/payments/${id}`);
    }

    public async createPayment(data: VendorPaymentCreateRequest): Promise<VendorPayment> {
        return apiService.post<VendorPayment>(`${this.baseUrl}/payments`, data);
    }

    /**
     * Freezes the applicable evidence/authority policy and starts maker-checker approval. This is
     * intentionally distinct from posting: only the completed workflow may authorize posting.
     */
    public async submitPayment(
        id: string,
        request: SubmitVendorPaymentRequest = {
            isExceptionalPayment: false,
            requestEvidenceException: false,
        },
    ): Promise<VendorPayment> {
        return apiService.post<VendorPayment>(`${this.baseUrl}/payments/${id}/submit`, request);
    }

    public async getPaymentControl(id: string): Promise<VendorPaymentControl> {
        return apiService.get<VendorPaymentControl>(`${this.baseUrl}/payments/${id}/control`);
    }

    public async postPayment(id: string): Promise<VendorPayment> {
        return apiService.post<VendorPayment>(`${this.baseUrl}/payments/${id}/post`, {});
    }

    public async getPaymentTrace(id: string): Promise<VendorPaymentTrace> {
        return apiService.get<VendorPaymentTrace>(`${this.baseUrl}/payments/${id}/trace`);
    }

    /**
     * Creates a linked compensating posting. The API keeps the original payment and journal
     * immutable, so the UI intentionally calls this "reverse" rather than "void" or "delete".
     */
    public async reversePayment(id: string, request: ReverseVendorPaymentRequest): Promise<VendorPayment> {
        return apiService.post<VendorPayment>(`${this.baseUrl}/payments/${id}/reverse`, request);
    }

    public async allocatePayment(
        paymentId: string,
        data: VendorPaymentAllocationCreateRequest | VendorPaymentAllocationCreateRequest[],
    ): Promise<any> {
        // The backend applies a set of advance allocations in one serializable transaction. Keep
        // the single-row form compatible, while allowing the allocation workspace to submit all
        // selected invoices atomically rather than partially consuming a currency lot.
        const allocations = Array.isArray(data) ? data : [data];
        return apiService.post<any>(`${this.baseUrl}/payments/${paymentId}/allocate`, allocations);
    }

    public async getOutstandingInvoices(supplierId: string): Promise<OutstandingVendorInvoice[]> {
        return apiService.get<OutstandingVendorInvoice[]>(`${this.baseUrl}/payments/supplier/${supplierId}/outstanding-invoices`);
    }

    public async getInvoicePaymentReadiness(invoiceId: string): Promise<VendorPaymentInvoiceReadiness> {
        return apiService.get<VendorPaymentInvoiceReadiness>(`${this.baseUrl}/payments/invoices/${invoiceId}/readiness`);
    }

    public async getPaymentSodReadiness(id: string): Promise<InvoicePaymentSodReadiness> {
        return apiService.get<InvoicePaymentSodReadiness>(`${this.baseUrl}/payments/${id}/sod-readiness`);
    }

    public async getSupplierDebitNoteApplications(paymentId: string): Promise<SupplierDebitNoteApplication[]> {
        return apiService.get<SupplierDebitNoteApplication[]>(
            `${this.baseUrl}/payments/${paymentId}/supplier-debit-note-applications`
        );
    }

    public async applySupplierDebitNotes(
        paymentId: string,
        applications: SupplierDebitNoteApplicationRequest[],
    ): Promise<SupplierDebitNoteApplicationResult> {
        return apiService.post<SupplierDebitNoteApplicationResult>(
            `${this.baseUrl}/payments/${paymentId}/supplier-debit-note-applications`,
            applications,
        );
    }

    public async reverseSupplierDebitNoteApplication(applicationId: string, reason: string): Promise<void> {
        return apiService.post<void>(
            `${this.baseUrl}/payments/supplier-debit-note-applications/${applicationId}/reverse`,
            reason,
        );
    }

    // --- Payment Batches ---

    public async getBatches(query: PaymentBatchQuery = {}): Promise<PagedResult<PaymentBatch>> {
        const params = new URLSearchParams();
        if (query.page) params.append('Page', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.status) params.append('Status', query.status);
        if (query.fromDate) params.append('FromDate', query.fromDate);
        if (query.toDate) params.append('ToDate', query.toDate);
        if (query.sortBy) params.append('SortBy', query.sortBy);
        if (query.sortDescending !== undefined) params.append('SortDescending', query.sortDescending.toString());

        return apiService.get<PagedResult<PaymentBatch>>(`${this.baseUrl}/payment-batches?${params.toString()}`);
    }

    public async getBatch(id: string): Promise<PaymentBatch> {
        return apiService.get<PaymentBatch>(`${this.baseUrl}/payment-batches/${id}`);
    }

    public async getBatchSodReadiness(id: string): Promise<InvoicePaymentSodReadiness> {
        return apiService.get<InvoicePaymentSodReadiness>(`${this.baseUrl}/payment-batches/${id}/sod-readiness`);
    }

    public async createBatch(data: PaymentBatchCreateRequest): Promise<PaymentBatch> {
        return apiService.post<PaymentBatch>(`${this.baseUrl}/payment-batches`, data);
    }

    public async approveBatch(id: string): Promise<PaymentBatch> {
        return apiService.post<PaymentBatch>(`${this.baseUrl}/payment-batches/${id}/approve`);
    }

    public async processBatch(id: string): Promise<PaymentBatch> {
        return apiService.post<PaymentBatch>(`${this.baseUrl}/payment-batches/${id}/process`);
    }

    // --- Reports ---

    public async getAgingReport(asOfDate?: string): Promise<ApAgingReport> {
        const params = new URLSearchParams();
        if (asOfDate) params.append('AsOfDate', asOfDate);
        return apiService.silentGet<ApAgingReport>(`${this.baseUrl}/reports/aging?${params.toString()}`);
    }

    public async downloadAgingReportCsv(asOfDate: string): Promise<Blob> {
        return apiService.postBlob('/finance/report-exports/export', {
            reportType: 'ApAging',
            format: 'Csv',
            asOfDate,
        });
    }

    public async getCashRequirementForecast(asOfDate?: string): Promise<CashRequirementForecast> {
        const params = new URLSearchParams();
        if (asOfDate) params.append('AsOfDate', asOfDate);
        return apiService.get<CashRequirementForecast>(`${this.baseUrl}/reports/cash-forecast?${params.toString()}`);
    }

    public async getSupplierStatement(businessPartnerId: string, fromDate: string, toDate: string): Promise<any> {
        const params = new URLSearchParams();
        params.append('businessPartnerId', businessPartnerId);
        params.append('FromDate', fromDate);
        params.append('ToDate', toDate);
        return apiService.get<any>(`${this.baseUrl}/reports/supplier-statement?${params.toString()}`);
    }

    public async getSupplierDetailedLedger(query: {
        fromDate: string;
        toDate: string;
        businessPartnerIds?: string[];
        showSupplierCurrency?: boolean;
    }): Promise<SupplierDetailedLedgerReport> {
        const params = new URLSearchParams();
        params.append('fromDate', query.fromDate);
        params.append('toDate', query.toDate);
        query.businessPartnerIds?.forEach((businessPartnerId) => params.append('businessPartnerIds', businessPartnerId));
        if (query.showSupplierCurrency !== undefined) {
            params.append('showSupplierCurrency', query.showSupplierCurrency.toString());
        }

        return apiService.get<SupplierDetailedLedgerReport>(`${this.baseUrl}/reports/supplier-detailed-ledger?${params.toString()}`);
    }

    public async downloadSupplierStatementCsv(query: {
        fromDate: string;
        toDate: string;
        businessPartnerIds?: string[];
        showSupplierCurrency?: boolean;
    }): Promise<Blob> {
        return apiService.postBlob('/finance/report-exports/export', {
            reportType: 'SupplierStatement',
            format: 'Csv',
            periodStart: query.fromDate,
            periodEnd: query.toDate,
            businessPartnerIds: query.businessPartnerIds ?? [],
            showSupplierCurrency: query.showSupplierCurrency === true,
        });
    }

    /**
     * Downloads the controlled supplier statement through the shared document-output pipeline.
     * PDF and XLSX therefore receive the same period, supplier selection and currency basis as
     * the on-screen detailed ledger instead of rebuilding balances in the browser.
     */
    public async downloadSupplierStatementDocument(query: {
        fromDate: string;
        toDate: string;
        businessPartnerIds?: string[];
        showSupplierCurrency?: boolean;
        format: 'pdf' | 'xlsx';
    }): Promise<void> {
        await documentOutputService.downloadReportDocument(
            DOCUMENT_TYPES.financeApSupplierStatement,
            {
                fromDate: query.fromDate,
                toDate: query.toDate,
                businessPartnerIds: query.businessPartnerIds ?? [],
                showSupplierCurrency: query.showSupplierCurrency === true,
            },
            { format: query.format }
        );
    }

    public async printSupplierStatementDocument(query: {
        fromDate: string;
        toDate: string;
        businessPartnerIds?: string[];
        showSupplierCurrency?: boolean;
    }): Promise<void> {
        await documentOutputService.printReportDocument(
            DOCUMENT_TYPES.financeApSupplierStatement,
            {
                fromDate: query.fromDate,
                toDate: query.toDate,
                businessPartnerIds: query.businessPartnerIds ?? [],
                showSupplierCurrency: query.showSupplierCurrency === true,
            },
            { format: 'pdf' }
        );
    }

    public async downloadAgingReportPdf(asOfDate: string): Promise<void> {
        await documentOutputService.downloadReportDocument(
            DOCUMENT_TYPES.financeApAgingReport,
            { asOfDate },
            { format: 'pdf' }
        );
    }

    public async printAgingReport(asOfDate: string): Promise<void> {
        await documentOutputService.printReportDocument(
            DOCUMENT_TYPES.financeApAgingReport,
            { asOfDate },
            { format: 'pdf' }
        );
    }

    public async downloadCashRequirementsPdf(asOfDate: string): Promise<void> {
        await documentOutputService.downloadReportDocument(
            DOCUMENT_TYPES.financeApCashRequirements,
            { asOfDate },
            { format: 'pdf' }
        );
    }

    public async printCashRequirements(asOfDate: string): Promise<void> {
        await documentOutputService.printReportDocument(
            DOCUMENT_TYPES.financeApCashRequirements,
            { asOfDate },
            { format: 'pdf' }
        );
    }

    public async downloadMatchExceptionReportPdf(query: {
        fromDate: string;
        toDate: string;
        status?: VendorInvoiceMatchExceptionStatus;
        supplierId?: string;
    }): Promise<void> {
        await documentOutputService.downloadReportDocument(
            DOCUMENT_TYPES.financeApMatchExceptionReport,
            query,
            { format: 'pdf' }
        );
    }

    public async printMatchExceptionReport(query: {
        fromDate: string;
        toDate: string;
        status?: VendorInvoiceMatchExceptionStatus;
        supplierId?: string;
    }): Promise<void> {
        await documentOutputService.printReportDocument(
            DOCUMENT_TYPES.financeApMatchExceptionReport,
            query,
            { format: 'pdf' }
        );
    }

    public async downloadProcurementFinanceReconciliationPdf(query: {
        asOfDate?: string;
        purchaseOrderId?: string;
    } = {}): Promise<void> {
        await documentOutputService.downloadReportDocument(
            DOCUMENT_TYPES.financeApProcurementReconciliation,
            query,
            { format: 'pdf' }
        );
    }

    public async printProcurementFinanceReconciliation(query: {
        asOfDate?: string;
        purchaseOrderId?: string;
    } = {}): Promise<void> {
        await documentOutputService.printReportDocument(
            DOCUMENT_TYPES.financeApProcurementReconciliation,
            query,
            { format: 'pdf' }
        );
    }

    public async getApSummary(): Promise<ApSummaryStats> {
        return apiService.get<ApSummaryStats>(`${this.baseUrl}/reports/summary`);
    }

    public async getThreeWayMatchExceptionReport(query: {
        fromDate: string;
        toDate: string;
        status?: VendorInvoiceMatchExceptionStatus;
        supplierId?: string;
    }): Promise<VendorInvoiceMatchExceptionReport> {
        return apiService.get<VendorInvoiceMatchExceptionReport>(`${this.baseUrl}/reports/three-way-match-exceptions`, query);
    }

    public async downloadThreeWayMatchExceptionReport(query: {
        fromDate: string;
        toDate: string;
        status?: VendorInvoiceMatchExceptionStatus;
        supplierId?: string;
    }): Promise<Blob> {
        return apiService.downloadBlob(`${this.baseUrl}/reports/three-way-match-exceptions/export`, {
            ...query,
            format: 'Csv',
        });
    }

    public async getProcurementFinanceReconciliation(query: {
        asOfDate?: string;
        purchaseOrderId?: string;
    } = {}): Promise<ProcurementFinanceReconciliationReport> {
        return apiService.get<ProcurementFinanceReconciliationReport>(
            `${this.baseUrl}/reports/procurement-reconciliation`,
            query
        );
    }

    public async downloadProcurementFinanceReconciliation(query: {
        asOfDate?: string;
        purchaseOrderId?: string;
    } = {}): Promise<Blob> {
        return apiService.downloadBlob(`${this.baseUrl}/reports/procurement-reconciliation/export`, {
            ...query,
            format: 'Csv',
        });
    }

    // --- Finance-owned Supplier Debit Notes ---

    public async getSupplierDebitNotes(query: SupplierDebitNoteQuery = {}): Promise<SupplierDebitNote[]> {
        return apiService.get<SupplierDebitNote[]>(`${this.baseUrl}/supplier-debit-notes`, { ...query });
    }

    public async getSupplierDebitNote(id: string): Promise<SupplierDebitNote> {
        return apiService.get<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes/${id}`);
    }

    public async createSupplierDebitNote(data: SupplierDebitNoteCreateRequest): Promise<SupplierDebitNote> {
        return apiService.post<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes`, data);
    }

    public async updateSupplierDebitNote(
        id: string,
        data: SupplierDebitNoteUpdateRequest,
    ): Promise<SupplierDebitNote> {
        return apiService.put<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes/${id}`, data);
    }

    public async submitSupplierDebitNote(id: string): Promise<SupplierDebitNote> {
        return apiService.post<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes/${id}/submit`, {});
    }

    public async updateInventoryReturnCreditHeader(id: string, data: {
        supplierCreditNoteReference: string; creditDate: string; reason?: string; rowVersion: string;
    }): Promise<SupplierDebitNote> {
        return apiService.put<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes/${id}/inventory-return-header`, data);
    }

    public async decideSupplierDebitNote(
        id: string,
        approve: boolean,
        comments?: string,
    ): Promise<SupplierDebitNote> {
        return apiService.post<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes/${id}/approval`, {
            approve,
            comments,
            rejectionReason: approve ? undefined : comments,
        });
    }

    public async postSupplierDebitNote(id: string): Promise<SupplierDebitNote> {
        return apiService.post<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes/${id}/post`, {});
    }

    public async cancelSupplierDebitNote(id: string, reason: string): Promise<SupplierDebitNote> {
        return apiService.post<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes/${id}/cancel`, { reason });
    }

    public async reverseSupplierDebitNote(
        id: string,
        reason: string,
        reversalDate?: string,
    ): Promise<SupplierDebitNote> {
        return apiService.post<SupplierDebitNote>(`${this.baseUrl}/supplier-debit-notes/${id}/reverse`, {
            reason,
            reversalDate,
        });
    }

    // --- Quarantined historical Supplier Returns (read-only in the UI) ---

    public async getSupplierReturns(): Promise<any[]> {
        return apiService.get<any[]>(`${this.baseUrl}/supplier-returns`);
    }

    public async getSupplierReturn(id: string): Promise<any> {
        return apiService.get<any>(`${this.baseUrl}/supplier-returns/${id}`);
    }

    public async createSupplierReturn(data: any): Promise<any> {
        return apiService.post<any>(`${this.baseUrl}/supplier-returns`, data);
    }

    public async approveSupplierReturn(id: string): Promise<any> {
        return apiService.post<any>(`${this.baseUrl}/supplier-returns/${id}/approve`);
    }
}

export const accountsPayableService = new AccountsPayableService();
