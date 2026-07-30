import { apiService } from '@/services/api.service';
import type {
  AcknowledgePurchaseOrderAmendmentRequest,
  CreatePurchaseOrderAmendmentRequest,
  DecidePurchaseOrderAmendmentRequest,
  DispatchPurchaseOrderAmendmentRequest,
  PurchaseOrderAmendment,
  PurchaseOrderAmendmentAcknowledgement,
  PurchaseOrderAmendmentDispatch,
  PurchaseOrderAmendmentLifecycleRequest,
  PurchaseOrderAmendmentOverview,
} from '@/types/procurement-purchase-order-amendment';

const root = '/procurement/purchase-order-amendments';

export const procurementPurchaseOrderAmendmentService = {
  overview: (purchaseOrderId: string) =>
    apiService.get<PurchaseOrderAmendmentOverview>(
      `${root}/purchase-orders/${purchaseOrderId}`
    ),
  externalOverview: () =>
    apiService.get<PurchaseOrderAmendment[]>(`${root}/external`),
  create: (
    purchaseOrderId: string,
    request: CreatePurchaseOrderAmendmentRequest
  ) =>
    apiService.post<PurchaseOrderAmendment>(
      `${root}/purchase-orders/${purchaseOrderId}`,
      request
    ),
  submit: (
    amendmentId: string,
    request: PurchaseOrderAmendmentLifecycleRequest
  ) =>
    apiService.post<PurchaseOrderAmendment>(
      `${root}/${amendmentId}/submit`,
      request
    ),
  decide: (
    amendmentId: string,
    request: DecidePurchaseOrderAmendmentRequest
  ) =>
    apiService.post<PurchaseOrderAmendment>(
      `${root}/${amendmentId}/decision`,
      request
    ),
  dispatch: (
    amendmentId: string,
    request: DispatchPurchaseOrderAmendmentRequest
  ) =>
    apiService.post<PurchaseOrderAmendmentDispatch>(
      `${root}/${amendmentId}/dispatches`,
      request
    ),
  acknowledge: (
    dispatchId: string,
    request: AcknowledgePurchaseOrderAmendmentRequest,
    external = false
  ) =>
    apiService.post<PurchaseOrderAmendmentAcknowledgement>(
      external
        ? `${root}/external/dispatches/${dispatchId}/acknowledgements`
        : `${root}/dispatches/${dispatchId}/acknowledgements`,
      request
    ),
};
