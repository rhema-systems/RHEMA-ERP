import { apiService } from './api.service';
import type {
    VendorInvoice,
    VendorInvoiceCreateRequest,
    VendorInvoiceUpdateRequest,
    VendorPayment,
    VendorPaymentCreateRequest,
    VendorPaymentAllocationCreateRequest,
    PaymentBatch,
    PaymentBatchCreateRequest,
    ApAgingReport,
    CashRequirementForecast,
    ApSummaryStats,
    OutstandingVendorInvoice
} from '../types/ap';

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
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    supplierId?: string;
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
    supplierId?: string;
    status?: string;
    paymentMethod?: string;
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

class AccountsPayableService {
    private readonly baseUrl = '/ap';

    // --- Vendor Invoices ---

    public async getInvoices(query: VendorInvoiceQuery = {}): Promise<PagedResult<VendorInvoice>> {
        const params = new URLSearchParams();
        if (query.page) params.append('Page', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.searchTerm) params.append('SearchTerm', query.searchTerm);
        if (query.supplierId) params.append('SupplierId', query.supplierId);
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

        return apiService.get<PagedResult<VendorInvoice>>(`${this.baseUrl}/invoices?${params.toString()}`);
    }

    public async getInvoice(id: string): Promise<VendorInvoice> {
        return apiService.get<VendorInvoice>(`${this.baseUrl}/invoices/${id}`);
    }

    public async createInvoice(data: VendorInvoiceCreateRequest): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.baseUrl}/invoices`, data);
    }

    public async updateInvoice(id: string, data: VendorInvoiceUpdateRequest): Promise<VendorInvoice> {
        return apiService.put<VendorInvoice>(`${this.baseUrl}/invoices/${id}`, data);
    }

    public async approveInvoice(id: string, comments?: string): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.baseUrl}/invoices/${id}/approve`, comments || 'Approved');
    }

    public async submitInvoiceForApproval(id: string): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.baseUrl}/invoices/${id}/submit`, {});
    }

    public async rejectInvoice(id: string, comments: string): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.baseUrl}/invoices/${id}/reject`, comments);
    }

    public async voidInvoice(id: string, comments?: string): Promise<VendorInvoice> {
        return apiService.post<VendorInvoice>(`${this.baseUrl}/invoices/${id}/void`, comments || 'Voided by user');
    }

    // --- Vendor Payments ---

    public async getPayments(query: VendorPaymentQuery = {}): Promise<PagedResult<VendorPayment>> {
        const params = new URLSearchParams();
        if (query.page) params.append('Page', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.searchTerm) params.append('SearchTerm', query.searchTerm);
        if (query.supplierId) params.append('SupplierId', query.supplierId);
        if (query.status) params.append('Status', query.status);
        if (query.paymentMethod) params.append('PaymentMethod', query.paymentMethod);
        if (query.paymentBatchId) params.append('PaymentBatchId', query.paymentBatchId);
        if (query.fromDate) params.append('FromDate', query.fromDate);
        if (query.toDate) params.append('ToDate', query.toDate);
        if (query.sortBy) params.append('SortBy', query.sortBy);
        if (query.sortDescending !== undefined) params.append('SortDescending', query.sortDescending.toString());

        return apiService.get<PagedResult<VendorPayment>>(`${this.baseUrl}/vendor-payments?${params.toString()}`);
    }

    public async getPayment(id: string): Promise<VendorPayment> {
        return apiService.get<VendorPayment>(`${this.baseUrl}/vendor-payments/${id}`);
    }

    public async createPayment(data: VendorPaymentCreateRequest): Promise<VendorPayment> {
        return apiService.post<VendorPayment>(`${this.baseUrl}/vendor-payments`, data);
    }

    public async allocatePayment(paymentId: string, data: VendorPaymentAllocationCreateRequest): Promise<any> {
        return apiService.post<any>(`${this.baseUrl}/vendor-payments/${paymentId}/allocate`, data);
    }

    public async getOutstandingInvoices(supplierId: string): Promise<OutstandingVendorInvoice[]> {
        return apiService.get<OutstandingVendorInvoice[]>(`${this.baseUrl}/vendor-payments/supplier/${supplierId}/outstanding-invoices`);
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
        return apiService.get<ApAgingReport>(`${this.baseUrl}/reports/aging?${params.toString()}`);
    }

    public async getCashRequirementForecast(asOfDate?: string): Promise<CashRequirementForecast> {
        const params = new URLSearchParams();
        if (asOfDate) params.append('AsOfDate', asOfDate);
        return apiService.get<CashRequirementForecast>(`${this.baseUrl}/reports/cash-requirement?${params.toString()}`);
    }

    public async getSupplierStatement(supplierId: string, fromDate: string, toDate: string): Promise<any> {
        const params = new URLSearchParams();
        params.append('supplierId', supplierId);
        params.append('FromDate', fromDate);
        params.append('ToDate', toDate);
        return apiService.get<any>(`${this.baseUrl}/reports/supplier-statement?${params.toString()}`);
    }

    public async getApSummary(): Promise<ApSummaryStats> {
        return apiService.get<ApSummaryStats>(`${this.baseUrl}/reports/ap-summary`);
    }

    // --- Supplier Returns & Debit Notes ---

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
