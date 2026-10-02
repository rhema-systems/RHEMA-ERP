import { apiService } from './api.service';
import { DOCUMENT_TYPES, documentOutputService } from './document-output.service';
import type {
    Customer,
    CustomerCreateRequest,
    CustomerUpdateRequest,
    Invoice,
    InvoiceCreateRequest,
    CustomerPayment,
    PaymentCreateRequest,
    PaymentAllocationRequest,
    AgingReport,
    CustomerDetailedLedgerReport,
    CollectionsDashboardStats,
    PaymentAllocation,
    PaymentAllocationResultDto,
    CustomerPaymentTrace,
    ReverseCustomerPaymentRequest
} from '../types/ar';

// Pagination and Filtering Types
export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
}

export interface CustomerQuery {
    page?: number;
    pageSize?: number;
    searchTerm?: string;
    isActive?: boolean;
    transactionReadiness?: 'Ready' | 'NotReady';
    includeBalances?: boolean;
}

export interface InvoiceQuery {
    page?: number;
    pageSize?: number;
    businessPartnerId?: string;
    startDate?: string;
    endDate?: string;
    status?: string;
    searchTerm?: string;
    isOpeningBalance?: boolean;
}

export interface PaymentQuery {
    page?: number;
    pageSize?: number;
    businessPartnerId?: string;
    startDate?: string;
    endDate?: string;
    status?: string;
    paymentMethod?: string;
    paymentMethodId?: string;
}

export type EstateArSource = 'facilities' | 'property-management';

// Estate requests billing work, while Finance AR remains the owner of invoices and payments.
export interface EstateArResultNotificationRequest {
    actionType: string;
    financeArEntityId?: string | null;
    financeArReference?: string | null;
    customerId?: string | null;
    customerName?: string | null;
    amount?: number | null;
    currencyCode?: string | null;
    sourceRecordReference?: string | null;
    propertyUnit?: string | null;
    notes?: string | null;
    actionUrl?: string | null;
}

class ArService {
    private readonly baseUrl = '/ar';

    // --- Customers ---

    public async getCustomers(query: CustomerQuery = {}): Promise<PagedResult<Customer>> {
        const params = new URLSearchParams();
        if (query.page) params.append('PageNumber', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.searchTerm) params.append('SearchTerm', query.searchTerm);
        if (query.isActive !== undefined) params.append('IsActive', query.isActive.toString());
        if (query.transactionReadiness) params.append('TransactionReadiness', query.transactionReadiness);
        if (query.includeBalances !== undefined) params.append('IncludeBalances', query.includeBalances.toString());

        return apiService.get<PagedResult<Customer>>(`${this.baseUrl}/customers?${params.toString()}`);
    }

    public async getCustomer(id: string): Promise<Customer> {
        return apiService.get<Customer>(`${this.baseUrl}/customers/${id}`);
    }

    public async createCustomer(data: CustomerCreateRequest): Promise<Customer> {
        return apiService.post<Customer>(`${this.baseUrl}/customers`, data);
    }

    public async updateCustomer(id: string, data: CustomerUpdateRequest): Promise<Customer> {
        return apiService.put<Customer>(`${this.baseUrl}/customers/${id}`, data);
    }

    public async checkCreditLimit(customerId: string, amount: number): Promise<{ isApproved: boolean; currentBalance: number; newBalance: number; creditLimit: number; message: string }> {
        return apiService.post(`${this.baseUrl}/customers/${customerId}/check-credit`, { amount });
    }

    // --- Invoices ---

    public async getInvoices(query: InvoiceQuery = {}): Promise<PagedResult<Invoice>> {
        const params = new URLSearchParams();
        if (query.page) params.append('PageNumber', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.businessPartnerId) params.append('BusinessPartnerId', query.businessPartnerId);
        if (query.startDate) params.append('StartDate', query.startDate);
        if (query.endDate) params.append('EndDate', query.endDate);
        if (query.status) params.append('Status', query.status);
        if (query.searchTerm) params.append('SearchTerm', query.searchTerm);
        if (query.isOpeningBalance !== undefined) params.append('IsOpeningBalance', query.isOpeningBalance.toString());

        return apiService.get<PagedResult<Invoice>>(`${this.baseUrl}/invoices?${params.toString()}`);
    }

    public async getInvoice(id: string): Promise<Invoice> {
        return apiService.get<Invoice>(`${this.baseUrl}/invoices/${id}`);
    }

    public async createInvoice(data: InvoiceCreateRequest): Promise<Invoice> {
        return apiService.post<Invoice>(`${this.baseUrl}/invoices`, data);
    }

    public async submitInvoiceForApproval(id: string): Promise<Invoice> {
        return apiService.post<Invoice>(`${this.baseUrl}/invoices/${id}/send`);
    }

    public async postInvoice(id: string): Promise<Invoice> {
        return apiService.post<Invoice>(`${this.baseUrl}/invoices/${id}/post`);
    }

    public async voidInvoice(id: string, reason: string): Promise<Invoice> {
        return apiService.post<Invoice>(`${this.baseUrl}/invoices/${id}/void`, JSON.stringify(reason));
    }

    // --- Payments ---

    public async getPayments(query: PaymentQuery = {}): Promise<PagedResult<CustomerPayment>> {
        const params = new URLSearchParams();
        if (query.page) params.append('PageNumber', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.businessPartnerId) params.append('BusinessPartnerId', query.businessPartnerId);
        if (query.startDate) params.append('StartDate', query.startDate);
        if (query.endDate) params.append('EndDate', query.endDate);
        if (query.status) params.append('Status', query.status);
        if (query.paymentMethod) params.append('PaymentMethod', query.paymentMethod);
        if (query.paymentMethodId) params.append('PaymentMethodId', query.paymentMethodId);

        return apiService.get<PagedResult<CustomerPayment>>(`${this.baseUrl}/payments?${params.toString()}`);
    }

    public async getPayment(id: string): Promise<CustomerPayment> {
        return apiService.get<CustomerPayment>(`${this.baseUrl}/payments/${id}`);
    }

    public async getPaymentTrace(id: string): Promise<CustomerPaymentTrace> {
        return apiService.get<CustomerPaymentTrace>(`${this.baseUrl}/payments/${id}/trace`);
    }

    /**
     * Posts an immutable correction rather than editing the receipt. The service name mirrors
     * the accounting action so callers do not confuse this with deletion or legacy voiding.
     */
    public async reversePayment(id: string, request: ReverseCustomerPaymentRequest): Promise<CustomerPayment> {
        return apiService.post<CustomerPayment>(`${this.baseUrl}/payments/${id}/reverse`, request);
    }

    public async createPayment(data: PaymentCreateRequest): Promise<CustomerPayment> {
        return apiService.post<CustomerPayment>(`${this.baseUrl}/payments`, data);
    }

    public async allocatePayment(data: PaymentAllocationRequest): Promise<PaymentAllocationResultDto> {
        return apiService.post<PaymentAllocationResultDto>(`${this.baseUrl}/payments/${data.customerPaymentId}/allocate`, data);
    }

    public async getOutstandingInvoices(customerId: string): Promise<{
        id: string;
        invoiceNumber: string;
        balanceAmount: number;
        currencyCode: string;
        invoiceDate: string;
        dueDate?: string;
        earlyPaymentDiscountPercentage?: number;
        earlyPaymentDiscountDueDate?: string;
        isDiscountAvailable?: boolean;
        requiresTaxAdjustmentForDiscount?: boolean;
        discountAmount?: number;
    }[]> {
        return apiService.get<{
            id: string;
            invoiceNumber: string;
            balanceAmount: number;
            currencyCode: string;
            invoiceDate: string;
            dueDate?: string;
            earlyPaymentDiscountPercentage?: number;
            earlyPaymentDiscountDueDate?: string;
            isDiscountAvailable?: boolean;
            requiresTaxAdjustmentForDiscount?: boolean;
            discountAmount?: number;
        }[]>(`${this.baseUrl}/payments/customer/${customerId}/outstanding-invoices`);
    }

    // --- Reports ---

    public async getAgingReport(asOfDate?: string): Promise<AgingReport> {
        const params = new URLSearchParams();
        if (asOfDate) params.append('asOfDate', asOfDate);
        return apiService.get<AgingReport>(`${this.baseUrl}/reports/aging?${params.toString()}`);
    }

    public async downloadAgingReportCsv(asOfDate: string): Promise<Blob> {
        return apiService.postBlob('/finance/report-exports/export', {
            reportType: 'ArAging',
            format: 'Csv',
            asOfDate,
        });
    }

    public async downloadAgingReportPdf(asOfDate: string): Promise<void> {
        await documentOutputService.downloadReportDocument(
            DOCUMENT_TYPES.financeArAgingReport,
            { asOfDate },
            { format: 'pdf' }
        );
    }

    public async printAgingReport(asOfDate: string): Promise<void> {
        await documentOutputService.printReportDocument(
            DOCUMENT_TYPES.financeArAgingReport,
            { asOfDate },
            { format: 'pdf' }
        );
    }

    public async getCustomerStatement(businessPartnerId: string, startDate: string, endDate: string): Promise<any> {
        const params = new URLSearchParams();
        params.append('fromDate', startDate);
        params.append('toDate', endDate);
        return apiService.get<any>(`${this.baseUrl}/reports/customer-statement/${businessPartnerId}?${params.toString()}`);
    }

    public async getCustomerDetailedLedger(query: {
        fromDate: string;
        toDate: string;
        businessPartnerIds?: string[];
        showCustomerCurrency?: boolean;
    }): Promise<CustomerDetailedLedgerReport> {
        const params = new URLSearchParams();
        params.append('fromDate', query.fromDate);
        params.append('toDate', query.toDate);
        query.businessPartnerIds?.forEach((businessPartnerId) => params.append('businessPartnerIds', businessPartnerId));
        if (query.showCustomerCurrency !== undefined) {
            params.append('showCustomerCurrency', query.showCustomerCurrency.toString());
        }

        return apiService.get<CustomerDetailedLedgerReport>(`${this.baseUrl}/reports/customer-detailed-ledger?${params.toString()}`);
    }

    public async downloadCustomerStatementCsv(query: {
        fromDate: string;
        toDate: string;
        businessPartnerIds?: string[];
        showCustomerCurrency?: boolean;
    }): Promise<Blob> {
        return apiService.postBlob('/finance/report-exports/export', {
            reportType: 'CustomerStatement',
            format: 'Csv',
            periodStart: query.fromDate,
            periodEnd: query.toDate,
            businessPartnerIds: query.businessPartnerIds ?? [],
            showCustomerCurrency: query.showCustomerCurrency === true,
        });
    }

    public async downloadCustomerStatementPdf(query: {
        fromDate: string;
        toDate: string;
        businessPartnerIds?: string[];
        showCustomerCurrency?: boolean;
    }): Promise<void> {
        await documentOutputService.downloadReportDocument(
            DOCUMENT_TYPES.financeArCustomerStatement,
            {
                fromDate: query.fromDate,
                toDate: query.toDate,
                businessPartnerIds: query.businessPartnerIds ?? [],
                showCustomerCurrency: query.showCustomerCurrency === true,
            },
            { format: 'pdf' }
        );
    }

    public async printCustomerStatement(query: {
        fromDate: string;
        toDate: string;
        businessPartnerIds?: string[];
        showCustomerCurrency?: boolean;
    }): Promise<void> {
        await documentOutputService.printReportDocument(
            DOCUMENT_TYPES.financeArCustomerStatement,
            {
                fromDate: query.fromDate,
                toDate: query.toDate,
                businessPartnerIds: query.businessPartnerIds ?? [],
                showCustomerCurrency: query.showCustomerCurrency === true,
            },
            { format: 'pdf' }
        );
    }

    public async getCollectionsDashboard(): Promise<CollectionsDashboardStats> {
        return apiService.get<CollectionsDashboardStats>(`${this.baseUrl}/reports/collections-dashboard`);
    }

    public async getArSummary(): Promise<any> {
        return apiService.get<any>(`${this.baseUrl}/reports/ar-summary`);
    }

    public async notifyEstateArResult(source: EstateArSource, data: EstateArResultNotificationRequest): Promise<void> {
        const basePath = source === 'facilities'
            ? '/estate/facilities/ar-billing/results'
            : '/estate/property-management/ar-billing/results';

        await apiService.post(basePath, data);
    }
}

export const arService = new ArService();
