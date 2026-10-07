import { apiService } from '@/services/api.service';

export interface FinanceExchangeRateOverrideRequest {
    id: string;
    sourceDocumentType: string;
    sourceDocumentId: string;
    sourceDocumentReference?: string;
    transactionCurrencyCode: string;
    functionalCurrencyCode: string;
    governedExchangeRateId: string;
    governedRate: number;
    governedRateSource: string;
    governedRateEffectiveDate: string;
    governedRateType: string;
    governedQuoteSide: string;
    requestedRate: number;
    reason: string;
    status: 'PendingApproval' | 'Approved' | 'Rejected' | 'Superseded' | 'Consumed';
    workflowInstanceId?: string;
    requestedByUserId: string;
    requestedAtUtc: string;
    approvedByUserId?: string;
    approvedAtUtc?: string;
    rejectedByUserId?: string;
    rejectedAtUtc?: string;
    rejectionReason?: string;
    consumedByPostingEventId?: string;
    consumedAtUtc?: string;
}

export interface CreateFinanceExchangeRateOverrideRequest {
    sourceDocumentType: string;
    sourceDocumentId: string;
    transactionCurrencyCode: string;
    governedExchangeRateId: string;
    requestedRate: number;
    reason: string;
}

export const exchangeRateOverrideService = {
    list(sourceDocumentType: string, sourceDocumentId: string) {
        return apiService.get<FinanceExchangeRateOverrideRequest[]>(
            '/finance/exchange-rate-overrides',
            { sourceDocumentType, sourceDocumentId },
        );
    },

    create(command: CreateFinanceExchangeRateOverrideRequest) {
        return apiService.post<FinanceExchangeRateOverrideRequest>(
            '/finance/exchange-rate-overrides',
            command,
        );
    },
};
