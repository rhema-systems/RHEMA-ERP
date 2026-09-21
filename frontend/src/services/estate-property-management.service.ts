import { compatibleApiService as apiService } from './compatibleApiService';
import { resolveProcedureWorkspaceType } from '@/lib/procedure-workspace';
import type {
  CreateFacilitiesArInvoiceRequest,
  CreateFacilitiesArPaymentRequest,
  FacilitiesArInvoice,
  FacilitiesArPayment,
  FacilitiesArResultNotificationRequest,
  FacilitiesProcedure,
  FacilitiesProcedureWorkspace,
} from './estate-facilities.service';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface EstateRentBillingActivationResult {
  activated: boolean;
  activatedAt: string;
  nextBillingDate?: string | null;
  invoiceId?: string | null;
  invoiceNumber?: string | null;
  message: string;
}

export interface EstateRentPenaltyTermsResult {
  assetId: string;
  gracePeriodDays: number;
  penaltyMethod: string;
  penaltyValue: number;
  penaltyCapAmount?: number | null;
  message: string;
}

export interface EstateRentPenaltyAssessmentResult {
  penaltyInvoiceId: string;
  penaltyInvoiceNumber: string;
  penaltyAmount: number;
  currencyCode: string;
  sourceInvoiceId: string;
  sourceInvoiceNumber: string;
  message: string;
}

export interface EstateRentPenaltyStatus {
  assetId: string;
  isOverdue: boolean;
  canAssessPenalty: boolean;
  dueDate?: string | null;
  graceEndsOn?: string | null;
  outstandingAmount: number;
}

export interface EstateSaleInvoiceResult {
  invoiceId: string;
  invoiceNumber: string;
  amount: number;
  currencyCode: string;
  status: string;
  message: string;
}

export interface EstateSaleCompletionResult {
  assetId: string;
  assetCode: string;
  purchaserCustomerId: string;
  invoiceId: string | null;
  invoiceNumber: string | null;
  message: string;
}

export interface EstateSalePaymentStatusResult {
  invoiceId: string | null;
  invoiceNumber: string | null;
  invoiceStatus: string;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  paymentStatus: string;
  ownershipTransferStatus: string;
  message: string;
}

export interface EstatePremiumChargeInvoiceResult {
  invoiceId: string | null;
  invoiceNumber: string | null;
  invoiceStatus: string;
  amount: number;
  paidAmount: number;
  balanceAmount: number;
  currencyCode: string;
  paymentStatus: string;
  message: string;
}

class EstatePropertyManagementService {
  async getProcedures(): Promise<FacilitiesProcedure[]> {
    const response = await apiService.get<ApiResponse<FacilitiesProcedure[]>>(
      '/estate/property-management/procedures',
    );
    return (response.data || []).map((procedure) => ({
      ...procedure,
      workspaceType: resolveProcedureWorkspaceType(
        procedure.entityType,
        procedure.workspaceType,
      ),
    }));
  }

  async getProcedureWorkspace(
    entityType: string,
  ): Promise<FacilitiesProcedureWorkspace | null> {
    const response = await apiService.get<
      ApiResponse<FacilitiesProcedureWorkspace>
    >(
      `/estate/property-management/procedures/${encodeURIComponent(entityType)}`,
    );
    if (!response.data) {
      return null;
    }

    return {
      ...response.data,
      procedure: {
        ...response.data.procedure,
        workspaceType: resolveProcedureWorkspaceType(
          response.data.procedure.entityType,
          response.data.procedure.workspaceType,
        ),
      },
    };
  }

  async createArInvoice(
    request: CreateFacilitiesArInvoiceRequest,
    propertyUnit?: string | null,
  ): Promise<FacilitiesArInvoice> {
    return apiService.post<FacilitiesArInvoice>(
      '/estate/property-management/ar-billing/invoices',
      {
        invoice: request,
        sourceRecordReference: request.reference || null,
        propertyUnit: propertyUnit || null,
      },
    );
  }

  async activateRentBilling(
    assetId: string,
  ): Promise<EstateRentBillingActivationResult> {
    return apiService.post<EstateRentBillingActivationResult>(
      `/estate/property-management/ar-billing/rent/${encodeURIComponent(assetId)}/activate`,
      {},
    );
  }

  async updateRentPenaltyTerms(
    assetId: string,
    request: {
      gracePeriodDays: number;
      penaltyMethod: string;
      penaltyValue: number;
      penaltyCapAmount?: number | null;
    },
  ): Promise<EstateRentPenaltyTermsResult> {
    return apiService.put<EstateRentPenaltyTermsResult>(
      `/estate/property-management/ar-billing/rent/${encodeURIComponent(assetId)}/penalty-terms`,
      request,
    );
  }

  async assessRentPenalty(
    assetId: string,
  ): Promise<EstateRentPenaltyAssessmentResult> {
    return apiService.post<EstateRentPenaltyAssessmentResult>(
      `/estate/property-management/ar-billing/rent/${encodeURIComponent(assetId)}/penalties/assess`,
      {},
    );
  }

  async getRentPenaltyStatuses(): Promise<EstateRentPenaltyStatus[]> {
    return apiService.get<EstateRentPenaltyStatus[]>(
      '/estate/property-management/ar-billing/rent/penalty-statuses',
    );
  }

  async createSaleInvoice(
    procedureCaseId: string,
  ): Promise<EstateSaleInvoiceResult> {
    return apiService.post<EstateSaleInvoiceResult>(
      `/estate/property-management/ar-billing/sale/${encodeURIComponent(procedureCaseId)}/invoice`,
      {},
    );
  }

  async completeSaleOwnership(
    procedureCaseId: string,
  ): Promise<EstateSaleCompletionResult> {
    return apiService.post<EstateSaleCompletionResult>(
      `/estate/property-management/ar-billing/sale/${encodeURIComponent(procedureCaseId)}/complete-ownership`,
      {},
    );
  }

  async syncSalePaymentStatus(
    procedureCaseId: string,
  ): Promise<EstateSalePaymentStatusResult> {
    return apiService.post<EstateSalePaymentStatusResult>(
      `/estate/property-management/ar-billing/sale/${encodeURIComponent(procedureCaseId)}/sync-payment-status`,
      {},
    );
  }

  async createPremiumChargeInvoice(
    procedureCaseId: string,
  ): Promise<EstatePremiumChargeInvoiceResult> {
    return apiService.post<EstatePremiumChargeInvoiceResult>(
      `/estate/property-management/ar-billing/premium/${encodeURIComponent(procedureCaseId)}/invoice`,
      {},
    );
  }

  async syncPremiumChargePaymentStatus(
    procedureCaseId: string,
  ): Promise<EstatePremiumChargeInvoiceResult> {
    return apiService.post<EstatePremiumChargeInvoiceResult>(
      `/estate/property-management/ar-billing/premium/${encodeURIComponent(procedureCaseId)}/sync-payment-status`,
      {},
    );
  }

  async createArPayment(
    request: CreateFacilitiesArPaymentRequest,
    propertyUnit?: string | null,
  ): Promise<FacilitiesArPayment> {
    return apiService.post<FacilitiesArPayment>(
      '/estate/property-management/ar-billing/payments',
      {
        payment: request,
        sourceRecordReference: request.transactionReference || null,
        propertyUnit: propertyUnit || null,
      },
    );
  }

  async notifyArResult(
    request: FacilitiesArResultNotificationRequest,
  ): Promise<void> {
    await apiService.post(
      '/estate/property-management/ar-billing/results',
      request,
    );
  }
}

export const estatePropertyManagementService =
  new EstatePropertyManagementService();
