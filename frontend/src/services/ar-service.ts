import { apiService } from './api.service';
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
    PaymentAllocationResultDto
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
    status?: string;
    includeBalances?: boolean;
}

export interface InvoiceQuery {
    page?: number;
    pageSize?: number;
    customerId?: string;
    startDate?: string;
    endDate?: string;
    status?: string;
    searchTerm?: string;
    isOpeningBalance?: boolean;
}

export interface PaymentQuery {
    page?: number;
    pageSize?: number;
    customerId?: string;
    startDate?: string;
    endDate?: string;
    status?: string;
}

class ArService {
    private readonly baseUrl = '/ar';

    // --- Customers ---

    public async getCustomers(query: CustomerQuery = {}): Promise<PagedResult<Customer>> {
        const params = new URLSearchParams();
        if (query.page) params.append('PageNumber', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.searchTerm) params.append('SearchTerm', query.searchTerm);
        if (query.status) params.append('Status', query.status);
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
        if (query.customerId) params.append('CustomerId', query.customerId);
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

    public async sendInvoice(id: string): Promise<Invoice> {
        return apiService.post<Invoice>(`${this.baseUrl}/invoices/${id}/send`);
    }

    public async voidInvoice(id: string, reason: string): Promise<Invoice> {
        return apiService.post<Invoice>(`${this.baseUrl}/invoices/${id}/void`, JSON.stringify(reason));
    }

    // --- Payments ---

    public async getPayments(query: PaymentQuery = {}): Promise<PagedResult<CustomerPayment>> {
        const params = new URLSearchParams();
        if (query.page) params.append('PageNumber', query.page.toString());
        if (query.pageSize) params.append('PageSize', query.pageSize.toString());
        if (query.customerId) params.append('CustomerId', query.customerId);
        if (query.startDate) params.append('StartDate', query.startDate);
        if (query.endDate) params.append('EndDate', query.endDate);
        if (query.status) params.append('Status', query.status);

        return apiService.get<PagedResult<CustomerPayment>>(`${this.baseUrl}/payments?${params.toString()}`);
    }

    public async getPayment(id: string): Promise<CustomerPayment> {
        return apiService.get<CustomerPayment>(`${this.baseUrl}/payments/${id}`);
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
        invoiceDate: string;
        dueDate?: string;
        earlyPaymentDiscountPercentage?: number;
        earlyPaymentDiscountDueDate?: string;
        isDiscountAvailable?: boolean;
        discountAmount?: number;
    }[]> {
        return apiService.get<{
            id: string;
            invoiceNumber: string;
            balanceAmount: number;
            invoiceDate: string;
            dueDate?: string;
            earlyPaymentDiscountPercentage?: number;
            earlyPaymentDiscountDueDate?: string;
            isDiscountAvailable?: boolean;
            discountAmount?: number;
        }[]>(`${this.baseUrl}/payments/customer/${customerId}/outstanding-invoices`);
    }

    // --- Reports ---

    public async getAgingReport(asOfDate?: string): Promise<AgingReport> {
        const params = new URLSearchParams();
        if (asOfDate) params.append('asOfDate', asOfDate);
        return apiService.get<AgingReport>(`${this.baseUrl}/reports/aging?${params.toString()}`);
    }

    public async getCustomerStatement(customerId: string, startDate: string, endDate: string): Promise<any> {
        const params = new URLSearchParams();
        params.append('fromDate', startDate);
        params.append('toDate', endDate);
        return apiService.get<any>(`${this.baseUrl}/reports/customer-statement/${customerId}?${params.toString()}`);
    }

    public async getCustomerDetailedLedger(query: {
        fromDate: string;
        toDate: string;
        customerIds?: string[];
        showCustomerCurrency?: boolean;
    }): Promise<CustomerDetailedLedgerReport> {
        const params = new URLSearchParams();
        params.append('fromDate', query.fromDate);
        params.append('toDate', query.toDate);
        query.customerIds?.forEach((customerId) => params.append('customerIds', customerId));
        if (query.showCustomerCurrency !== undefined) {
            params.append('showCustomerCurrency', query.showCustomerCurrency.toString());
        }

        return apiService.get<CustomerDetailedLedgerReport>(`${this.baseUrl}/reports/customer-detailed-ledger?${params.toString()}`);
    }

    public async downloadCustomerStatementCsv(query: {
        fromDate: string;
        toDate: string;
        customerIds?: string[];
        showCustomerCurrency?: boolean;
    }): Promise<Blob> {
        return apiService.postBlob('/finance/report-exports/export', {
            reportType: 'CustomerStatement',
            format: 'Csv',
            periodStart: query.fromDate,
            periodEnd: query.toDate,
            customerIds: query.customerIds ?? [],
            showCustomerCurrency: query.showCustomerCurrency === true,
        });
    }

    public async getCollectionsDashboard(): Promise<CollectionsDashboardStats> {
        return apiService.get<CollectionsDashboardStats>(`${this.baseUrl}/reports/collections-dashboard`);
    }

    public async getArSummary(): Promise<any> {
        return apiService.get<any>(`${this.baseUrl}/reports/ar-summary`);
    }
}

export const arService = new ArService();
