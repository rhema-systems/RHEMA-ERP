import { compatibleApiService as apiService } from './compatibleApiService';
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

class EstatePropertyManagementService {
  async getProcedures(): Promise<FacilitiesProcedure[]> {
    const response = await apiService.get<ApiResponse<FacilitiesProcedure[]>>(
      '/estate/property-management/procedures',
    );
    return response.data || [];
  }

  async getProcedureWorkspace(
    entityType: string,
  ): Promise<FacilitiesProcedureWorkspace | null> {
    const response = await apiService.get<
      ApiResponse<FacilitiesProcedureWorkspace>
    >(
      `/estate/property-management/procedures/${encodeURIComponent(entityType)}`,
    );
    return response.data || null;
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
