/**
 * Cash Management Data Service
 * Service layer for cash management module API calls
 */

import type {
    BankAccount,
    CashTransaction,
    BankStatement,
    BankStatementLine,
    BankReconciliation,
    ReconciliationMatch,
    Cheque,
    PaymentMethod,
    CreateBankAccountDto,
    UpdateBankAccountDto,
    CreateCashReceiptDto,
    CreateCashPaymentDto,
    CreateBankTransferDto,
    StartReconciliationDto,
    CashPositionSummary,
    CashFlowSummary,
} from '@/types/cash-management';
import { apiService } from '@/services/api.service';

// =============================================================================
// CASH MANAGEMENT DATA SERVICE
// =============================================================================

class CashManagementDataService {
    // ===== BANK ACCOUNTS =====

    async getBankAccounts(): Promise<BankAccount[]> {
        return apiService.get<BankAccount[]>('/finance/bank-accounts');
    }

    async getBankAccountById(id: string): Promise<BankAccount | null> {
        return apiService.get<BankAccount>(`/finance/bank-accounts/${id}`);
    }

    async getActiveBankAccounts(): Promise<BankAccount[]> {
        return apiService.get<BankAccount[]>('/finance/bank-accounts/active');
    }

    async createBankAccount(dto: CreateBankAccountDto): Promise<BankAccount> {
        return apiService.post<BankAccount>('/finance/bank-accounts', dto);
    }

    async updateBankAccount(id: string, dto: UpdateBankAccountDto): Promise<BankAccount> {
        return apiService.put<BankAccount>(`/finance/bank-accounts/${id}`, dto);
    }

    async deleteBankAccount(id: string): Promise<void> {
        return apiService.delete(`/finance/bank-accounts/${id}`);
    }

    // ===== CASH TRANSACTIONS =====

    async getCashTransactions(fromDate?: string, toDate?: string): Promise<CashTransaction[]> {
        const queryParams = new URLSearchParams();
        if (fromDate) queryParams.append('fromDate', fromDate);
        if (toDate) queryParams.append('toDate', toDate);

        const endpoint = `/finance/cash-transactions${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<CashTransaction[]>(endpoint);
    }

    async getCashTransactionById(id: string): Promise<CashTransaction | null> {
        return apiService.get<CashTransaction>(`/finance/cash-transactions/${id}`);
    }

    async getTransactionsByBankAccount(bankAccountId: string, fromDate?: string, toDate?: string): Promise<CashTransaction[]> {
        const queryParams = new URLSearchParams();
        if (fromDate) queryParams.append('fromDate', fromDate);
        if (toDate) queryParams.append('toDate', toDate);

        const endpoint = `/finance/cash-transactions/bank-account/${bankAccountId}${queryParams.toString() ? `?${queryParams}` : ''}`;
        return apiService.get<CashTransaction[]>(endpoint);
    }

    async getUnreconciledTransactions(bankAccountId: string): Promise<CashTransaction[]> {
        return apiService.get<CashTransaction[]>(`/finance/cash-transactions/bank-account/${bankAccountId}/unreconciled`);
    }

    async createCashReceipt(dto: CreateCashReceiptDto): Promise<CashTransaction> {
        return apiService.post<CashTransaction>('/finance/cash-transactions/receipt', dto);
    }

    async createCashPayment(dto: CreateCashPaymentDto): Promise<CashTransaction> {
        return apiService.post<CashTransaction>('/finance/cash-transactions/payment', dto);
    }

    async createBankTransfer(dto: CreateBankTransferDto): Promise<{ fromTransaction: CashTransaction; toTransaction: CashTransaction }> {
        return apiService.post<{ fromTransaction: CashTransaction; toTransaction: CashTransaction }>('/finance/cash-transactions/transfer', dto);
    }

    async deleteCashTransaction(id: string): Promise<void> {
        return apiService.delete(`/finance/cash-transactions/${id}`);
    }

    // ===== BANK STATEMENTS =====

    async getBankStatements(bankAccountId?: string): Promise<BankStatement[]> {
        const endpoint = bankAccountId
            ? `/finance/bank-statements?bankAccountId=${bankAccountId}`
            : '/finance/bank-statements';
        return apiService.get<BankStatement[]>(endpoint);
    }

    async getBankStatementById(id: string): Promise<BankStatement> {
        return apiService.get<BankStatement>(`/finance/bank-statements/${id}`);
    }

    async importBankStatement(bankAccountId: string, file: File): Promise<BankStatement> {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('bankAccountId', bankAccountId);
        return apiService.post<BankStatement>('/finance/bank-statements/import', formData);
    }

    async getStatementLines(statementId: string): Promise<BankStatementLine[]> {
        return apiService.get<BankStatementLine[]>(`/finance/bank-statements/${statementId}/lines`);
    }

    // ===== BANK RECONCILIATION =====

    async getBankReconciliations(bankAccountId?: string): Promise<BankReconciliation[]> {
        const endpoint = bankAccountId
            ? `/finance/bank-reconciliations?bankAccountId=${bankAccountId}`
            : '/finance/bank-reconciliations';
        return apiService.get<BankReconciliation[]>(endpoint);
    }

    async getBankReconciliationById(id: string): Promise<BankReconciliation | null> {
        return apiService.get<BankReconciliation>(`/finance/bank-reconciliations/${id}`);
    }

    async startReconciliation(dto: StartReconciliationDto): Promise<BankReconciliation> {
        return apiService.post<BankReconciliation>('/finance/bank-reconciliations', dto);
    }

    async getReconciliationMatches(reconciliationId: string): Promise<ReconciliationMatch[]> {
        return apiService.get<ReconciliationMatch[]>(`/finance/bank-reconciliations/${reconciliationId}/matches`);
    }

    async createReconciliationMatch(reconciliationId: string, match: Partial<ReconciliationMatch>): Promise<ReconciliationMatch> {
        return apiService.post<ReconciliationMatch>(`/finance/bank-reconciliations/${reconciliationId}/matches`, match);
    }

    async completeReconciliation(id: string): Promise<BankReconciliation> {
        return apiService.put<BankReconciliation>(`/finance/bank-reconciliations/${id}/complete`, {});
    }

    // ===== CHEQUES =====

    async getCheques(bankAccountId?: string): Promise<Cheque[]> {
        const endpoint = bankAccountId
            ? `/finance/cheques?bankAccountId=${bankAccountId}`
            : '/finance/cheques';
        return apiService.get<Cheque[]>(endpoint);
    }

    async getChequeById(id: string): Promise<Cheque> {
        return apiService.get<Cheque>(`/finance/cheques/${id}`);
    }

    async createCheque(dto: Partial<Cheque>): Promise<Cheque> {
        return apiService.post<Cheque>('/finance/cheques', dto);
    }

    async updateChequeStatus(id: string, status: string): Promise<Cheque> {
        return apiService.put<Cheque>(`/finance/cheques/${id}/status`, { status });
    }

    async voidCheque(id: string, reason: string): Promise<Cheque> {
        return apiService.put<Cheque>(`/finance/cheques/${id}/void`, { reason });
    }

    // ===== PAYMENT METHODS =====

    async getPaymentMethods(): Promise<PaymentMethod[]> {
        return apiService.get<PaymentMethod[]>('/finance/payment-methods');
    }

    async getActivePaymentMethods(): Promise<PaymentMethod[]> {
        return apiService.get<PaymentMethod[]>('/finance/payment-methods?isActive=true');
    }

    async createPaymentMethod(dto: Partial<PaymentMethod>): Promise<PaymentMethod> {
        return apiService.post<PaymentMethod>('/finance/payment-methods', dto);
    }

    async updatePaymentMethod(id: string, dto: Partial<PaymentMethod>): Promise<PaymentMethod> {
        return apiService.put<PaymentMethod>(`/finance/payment-methods/${id}`, dto);
    }

    // ===== CASH POSITION & REPORTS =====

    async getCashPosition(): Promise<CashPositionSummary> {
        return apiService.get<CashPositionSummary>('/finance/cash-reports/position');
    }

    async getCashFlow(fromDate: string, toDate: string): Promise<CashFlowSummary> {
        return apiService.get<CashFlowSummary>(`/finance/cash-reports/cash-flow?fromDate=${fromDate}&toDate=${toDate}`);
    }
}

// Export singleton instance
export const cashManagementDataService = new CashManagementDataService();
