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
    BankTransferPreview,
    StartReconciliationDto,
    CreateManualMatchDto,
    CreateReconciliationAdjustmentDto,
    ReconciliationAdjustment,
    ReconciliationSummary,
    CashPositionSummary,
    CashFlowSummary,
    LiquidityAccount,
    LiquidityAccountEntry,
    BankDeposit,
    BankDepositStatus,
    BankingSetupStatus,
    DepositPolicy,
    LiquidityAccountType,
    ReturnedChequeCase,
    ReturnedChequeCaseStatus,
    PostedLiquidityPaymentCandidate,
    CashTransactionTrace,
    ReverseCashTransactionDto,
    CashierTillSession,
    CashierTillSessionStatus,
} from '@/types/cash-management';
import { apiService } from '@/services/api.service';
import type { FinanceSourceDocumentDimensionInput } from '@/types/finance';

// =============================================================================
// CASH MANAGEMENT DATA SERVICE
// =============================================================================

class CashManagementDataService {
    // ===== CASHIER / TILL CUSTODY =====

    async getCashierTillSessions(filters?: {
        status?: CashierTillSessionStatus;
        liquidityAccountId?: string;
        businessDate?: string;
    }): Promise<CashierTillSession[]> {
        const query = new URLSearchParams();
        if (filters?.status) query.set('status', filters.status);
        if (filters?.liquidityAccountId) query.set('liquidityAccountId', filters.liquidityAccountId);
        if (filters?.businessDate) query.set('businessDate', filters.businessDate);
        // Keep the optional suffix separate so the route-contract test can statically verify the
        // endpoint while callers still avoid a trailing question mark when no filters are set.
        const suffix = query.size ? `?${query.toString()}` : '';
        return apiService.get<CashierTillSession[]>(`/finance/cashier-tills/sessions${suffix}`);
    }

    async getCashierTillSession(id: string): Promise<CashierTillSession> {
        return apiService.get<CashierTillSession>(`/finance/cashier-tills/sessions/${id}`);
    }

    async openCashierTillSession(dto: {
        liquidityAccountId: string;
        businessDate: string;
        openingFloatAmount: number;
        openingNotes?: string;
        openingEvidenceFileId?: string;
    }): Promise<CashierTillSession> {
        return apiService.post<CashierTillSession>('/finance/cashier-tills/sessions', dto);
    }

    async updateCashierTillOpening(id: string, dto: {
        openingFloatAmount: number;
        openingNotes?: string;
        openingEvidenceFileId?: string;
        rowVersion: string;
    }): Promise<CashierTillSession> {
        return apiService.put<CashierTillSession>(
            `/finance/cashier-tills/sessions/${id}/opening-details`,
            dto,
        );
    }

    async cancelCashierTillSession(
        id: string,
        reason: string,
        rowVersion: string,
    ): Promise<CashierTillSession> {
        return apiService.post<CashierTillSession>(
            `/finance/cashier-tills/sessions/${id}/cancel`,
            { reason, rowVersion },
        );
    }

    async submitCashierTillCount(id: string, dto: {
        countLines: Array<{ denomination: number; quantity: number }>;
        varianceReason?: string;
        closingEvidenceFileId?: string;
        rowVersion: string;
    }): Promise<CashierTillSession> {
        return apiService.post<CashierTillSession>(
            `/finance/cashier-tills/sessions/${id}/submit-count`,
            dto,
        );
    }

    async approveCashierTillClosure(
        id: string,
        comments: string,
        rowVersion: string,
    ): Promise<CashierTillSession> {
        return apiService.post<CashierTillSession>(
            `/finance/cashier-tills/sessions/${id}/approve-closure`,
            { comments, rowVersion },
        );
    }

    async returnCashierTillForRecount(
        id: string,
        comments: string,
        rowVersion: string,
    ): Promise<CashierTillSession> {
        return apiService.post<CashierTillSession>(
            `/finance/cashier-tills/sessions/${id}/return-for-recount`,
            { comments, rowVersion },
        );
    }

    async reopenCashierTillAsCorrection(
        id: string,
        reason: string,
        rowVersion: string,
        openingEvidenceFileId?: string,
    ): Promise<CashierTillSession> {
        return apiService.post<CashierTillSession>(
            `/finance/cashier-tills/sessions/${id}/reopen-as-correction`,
            { reason, rowVersion, openingEvidenceFileId },
        );
    }

    // ===== BANKING & SETTLEMENT =====

    async getBankingSetup(): Promise<BankingSetupStatus> {
        return apiService.get<BankingSetupStatus>('/finance/banking/setup');
    }

    async completeBankingSetup(dto: {
        depositPolicy: DepositPolicy;
        requirePrimaryEvidence: boolean;
        accounts: Array<{
            accountType: LiquidityAccountType;
            code: string;
            name: string;
            currency: string;
            glAccountId: string;
        }>;
    }): Promise<BankingSetupStatus> {
        return apiService.post<BankingSetupStatus>('/finance/banking/setup', dto);
    }

    async getLiquidityAccounts(activeOnly = false): Promise<LiquidityAccount[]> {
        return apiService.get<LiquidityAccount[]>(
            `/finance/banking/liquidity-accounts?activeOnly=${activeOnly}`,
        );
    }

    async createLiquidityAccount(dto: {
        code: string;
        name: string;
        accountType: LiquidityAccountType;
        currency: string;
        glAccountId: string;
        bankAccountId?: string;
        providerName?: string;
        providerAccountReference?: string;
        allowsNegativeBalance: boolean;
        allowsManualAllocations: boolean;
        notes?: string;
    }): Promise<LiquidityAccount> {
        return apiService.post<LiquidityAccount>('/finance/banking/liquidity-accounts', dto);
    }

    async getEligibleLiquidityEntries(currency?: string): Promise<LiquidityAccountEntry[]> {
        const query = currency ? `?currency=${encodeURIComponent(currency)}` : '';
        return apiService.get<LiquidityAccountEntry[]>(`/finance/banking/eligible-entries${query}`);
    }

    async getPostedPaymentCandidates(): Promise<PostedLiquidityPaymentCandidate[]> {
        return apiService.get<PostedLiquidityPaymentCandidate[]>('/finance/banking/posted-payment-candidates');
    }

    async registerPostedPayment(
        accountTransactionId: string,
        entryType: 'CashExpense' | 'PettyCashReplenishment' | 'CustomerRefund' | 'OtherPayment',
    ): Promise<LiquidityAccountEntry> {
        return apiService.post<LiquidityAccountEntry>('/finance/banking/posted-payment-entries', {
            accountTransactionId,
            entryType,
        });
    }

    async getBankDeposits(
        status?: BankDepositStatus,
        includeAllocations = false,
        limit = 200,
    ): Promise<BankDeposit[]> {
        const params = new URLSearchParams();
        if (status) params.set('status', status);
        if (includeAllocations) params.set('includeAllocations', 'true');
        params.set('limit', String(limit));
        return apiService.get<BankDeposit[]>(`/finance/banking/deposits?${params.toString()}`);
    }

    async getBankDeposit(id: string): Promise<BankDeposit> {
        return apiService.get<BankDeposit>(`/finance/banking/deposits/${id}`);
    }

    async createBankDeposit(dto: {
        bankAccountId: string;
        depositDate: string;
        depositReference: string;
        notes?: string;
        allocations: Array<{
            liquidityAccountEntryId: string;
            allocationType: 'Receipt' | 'Deduction';
            amount: number;
            notes?: string;
        }>;
        financeDimensions?: FinanceSourceDocumentDimensionInput;
    }): Promise<BankDeposit> {
        return apiService.post<BankDeposit>('/finance/banking/deposits', dto);
    }

    async updateBankDepositDimensions(
        id: string,
        dto: FinanceSourceDocumentDimensionInput,
    ): Promise<BankDeposit> {
        return apiService.put<BankDeposit>(`/finance/banking/deposits/${id}/dimensions`, dto);
    }

    async submitBankDeposit(id: string): Promise<BankDeposit> {
        return apiService.post<BankDeposit>(`/finance/banking/deposits/${id}/submit`, {});
    }

    async approveBankDeposit(id: string, comments?: string): Promise<BankDeposit> {
        return apiService.post<BankDeposit>(`/finance/banking/deposits/${id}/approve`, { comments });
    }

    async rejectBankDeposit(id: string, reason: string): Promise<BankDeposit> {
        return apiService.post<BankDeposit>(`/finance/banking/deposits/${id}/reject`, { reason });
    }

    async returnBankDeposit(id: string, comments: string): Promise<BankDeposit> {
        return apiService.post<BankDeposit>(`/finance/banking/deposits/${id}/return`, { comments });
    }

    async postBankDeposit(id: string): Promise<BankDeposit> {
        return apiService.post<BankDeposit>(`/finance/banking/deposits/${id}/post`, {});
    }

    async confirmBankDeposit(id: string, dto: {
        bankConfirmationReference: string;
        bankConfirmationDate: string;
        confirmationEvidenceFileId?: string;
        notes?: string;
        rowVersion: string;
    }): Promise<BankDeposit> {
        return apiService.post<BankDeposit>(`/finance/banking/deposits/${id}/confirm`, dto);
    }

    async uploadBankingEvidence(file: File): Promise<string> {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('category', 'finance-banking-evidence');
        const response = await apiService.post<Record<string, string>>('/fileupload/single', formData);
        const fileId = response.fileId || response.id || response.fileUploadRecordId;
        if (!fileId) throw new Error('Upload succeeded but the server did not return a file ID.');
        return fileId;
    }

    async linkBankDepositAttachment(
        depositId: string,
        fileUploadRecordId: string,
        documentType = 'DepositSlip',
        isPrimaryEvidence = true,
    ): Promise<BankDeposit> {
        return apiService.post<BankDeposit>(`/finance/banking/deposits/${depositId}/attachments`, {
            fileUploadRecordId,
            documentType,
            isPrimaryEvidence,
        });
    }

    async getReturnedCheques(status?: ReturnedChequeCaseStatus): Promise<ReturnedChequeCase[]> {
        const query = status ? `?status=${encodeURIComponent(status)}` : '';
        return apiService.get<ReturnedChequeCase[]>(`/finance/banking/returned-cheques${query}`);
    }

    async createReturnedCheque(dto: {
        customerPaymentId: string;
        bankDepositBatchId?: string;
        bankAccountId: string;
        returnDate: string;
        bankReference: string;
        returnReason: string;
        bankChargeAmount: number;
        chargeTreatment: 'CustomerRecoverable' | 'BankChargeExpense' | 'Split';
        customerRecoverableChargeAmount?: number;
        expenseChargeAmount?: number;
        drawerBank?: string;
        notes?: string;
        financeDimensions?: FinanceSourceDocumentDimensionInput;
    }): Promise<ReturnedChequeCase> {
        return apiService.post<ReturnedChequeCase>('/finance/banking/returned-cheques', dto);
    }

    async updateReturnedChequeDimensions(
        id: string,
        dto: FinanceSourceDocumentDimensionInput,
    ): Promise<ReturnedChequeCase> {
        return apiService.put<ReturnedChequeCase>(`/finance/banking/returned-cheques/${id}/dimensions`, dto);
    }

    async submitReturnedCheque(id: string): Promise<ReturnedChequeCase> {
        return apiService.post<ReturnedChequeCase>(`/finance/banking/returned-cheques/${id}/submit`, {});
    }

    async approveReturnedCheque(id: string, comments?: string): Promise<ReturnedChequeCase> {
        return apiService.post<ReturnedChequeCase>(
            `/finance/banking/returned-cheques/${id}/approve`,
            { comments },
        );
    }

    async linkReturnedChequeAttachment(
        id: string,
        fileUploadRecordId: string,
        documentType = 'BankReturnAdvice',
        isPrimaryEvidence = true,
    ): Promise<ReturnedChequeCase> {
        return apiService.post<ReturnedChequeCase>(`/finance/banking/returned-cheques/${id}/attachments`, {
            fileUploadRecordId,
            documentType,
            isPrimaryEvidence,
        });
    }

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

    async getCashTransactionTrace(id: string): Promise<CashTransactionTrace | null> {
        return apiService.get<CashTransactionTrace>(`/finance/cash-transactions/${id}/trace`);
    }

    async reverseCashTransaction(id: string, dto: ReverseCashTransactionDto): Promise<CashTransaction> {
        return apiService.post<CashTransaction>(`/finance/cash-transactions/${id}/reverse`, dto);
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

    async previewBankTransfer(dto: CreateBankTransferDto): Promise<BankTransferPreview> {
        // Preview and capture intentionally share one server-side resolver; keeping rate
        // selection out of the browser prevents an editable/manual rate from bypassing policy.
        return apiService.post<BankTransferPreview>('/finance/cash-transactions/transfer/preview', dto);
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

    async importBankStatement(
        bankAccountId: string,
        file: File,
        statementNumber?: string,
        notes?: string,
    ): Promise<BankStatement> {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('bankAccountId', bankAccountId);
        if (statementNumber?.trim()) formData.append('statementNumber', statementNumber.trim());
        if (notes?.trim()) formData.append('notes', notes.trim());
        return apiService.post<BankStatement>('/finance/bank-statements/import', formData);
    }

    async getStatementLines(statementId: string): Promise<BankStatementLine[]> {
        return apiService.get<BankStatementLine[]>(`/finance/bank-statements/${statementId}/lines`);
    }

    // ===== BANK RECONCILIATION =====

    async getBankReconciliations(bankAccountId?: string): Promise<BankReconciliation[]> {
        if (!bankAccountId) {
            return [];
        }

        return apiService.get<BankReconciliation[]>(`/finance/bank-reconciliation/bank-account/${bankAccountId}`);
    }

    async getBankReconciliationById(id: string): Promise<BankReconciliation | null> {
        return apiService.get<BankReconciliation>(`/finance/bank-reconciliation/${id}`);
    }

    async startReconciliation(dto: StartReconciliationDto): Promise<BankReconciliation> {
        return apiService.post<BankReconciliation>('/finance/bank-reconciliation/start', dto);
    }

    async getReconciliationMatches(reconciliationId: string): Promise<ReconciliationMatch[]> {
        return apiService.get<ReconciliationMatch[]>(`/finance/bank-reconciliation/${reconciliationId}/matches`);
    }

    async getReconciliationSummary(reconciliationId: string): Promise<ReconciliationSummary> {
        return apiService.get<ReconciliationSummary>(`/finance/bank-reconciliation/${reconciliationId}/summary`);
    }

    async autoMatchReconciliation(reconciliationId: string): Promise<ReconciliationMatch[]> {
        return apiService.post<ReconciliationMatch[]>(`/finance/bank-reconciliation/${reconciliationId}/auto-match`, {});
    }

    async createReconciliationMatch(reconciliationId: string, match: CreateManualMatchDto): Promise<ReconciliationMatch> {
        return apiService.post<ReconciliationMatch>(`/finance/bank-reconciliation/${reconciliationId}/manual-match`, match);
    }

    async removeReconciliationMatch(matchId: string): Promise<ReconciliationMatch> {
        return apiService.delete<ReconciliationMatch>(`/finance/bank-reconciliation/match/${matchId}`);
    }

    async postReconciliationAdjustment(reconciliationId: string, dto: CreateReconciliationAdjustmentDto): Promise<ReconciliationAdjustment> {
        return apiService.post<ReconciliationAdjustment>(`/finance/bank-reconciliation/${reconciliationId}/adjustments/post`, dto);
    }

    async completeReconciliation(id: string): Promise<BankReconciliation> {
        return apiService.post<BankReconciliation>(`/finance/bank-reconciliation/${id}/finalize`, {});
    }

    async approveReconciliation(id: string): Promise<BankReconciliation> {
        return apiService.post<BankReconciliation>(`/finance/bank-reconciliation/${id}/approve`, {});
    }

    async cancelReconciliation(id: string, reason: string): Promise<BankReconciliation> {
        return apiService.post<BankReconciliation>(`/finance/bank-reconciliation/${id}/cancel`, { reason });
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
