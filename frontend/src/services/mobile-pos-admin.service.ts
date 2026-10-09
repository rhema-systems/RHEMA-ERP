import { apiService } from './api.service';

export type MobilePosStoreStatus = 1 | 2 | 3 | 4;
export type MobilePosTillStatus = 1 | 2 | 3 | 4;
export type MobilePosDeviceStatus = 1 | 2 | 3 | 4 | 5;

export interface MobilePosReferenceOption {
  id: string;
  code: string;
  name: string;
  secondary?: string;
}

export interface MobilePosCustomerReference {
  businessPartnerId: string;
  businessPartnerRoleId: string;
  code: string;
  name: string;
}

export interface MobilePosDimensionReference {
  definitionId: string;
  code: string;
  name: string;
  values: MobilePosReferenceOption[];
}

export interface MobilePosAdministrationReferences {
  customers: MobilePosCustomerReference[];
  locations: MobilePosReferenceOption[];
  warehouses: MobilePosReferenceOption[];
  companyProfiles: MobilePosReferenceOption[];
  cashTills: MobilePosReferenceOption[];
  paymentMethods: MobilePosReferenceOption[];
  users: MobilePosReferenceOption[];
  dimensions: MobilePosDimensionReference[];
}

export interface MobilePosStore {
  id: string;
  code: string;
  name: string;
  status: MobilePosStoreStatus;
  companyProfileId?: string;
  locationId: string;
  locationName: string;
  warehouseId?: string;
  warehouseName?: string;
  currencyCode: string;
  timeZoneId: string;
  defaultWalkInBusinessPartnerId: string;
  defaultWalkInBusinessPartnerRoleId: string;
  defaultWalkInCustomerCode: string;
  defaultWalkInCustomerName: string;
  offlinePolicyId?: string;
  offlinePolicyName?: string;
  notes?: string;
  rowVersion: string;
  dimensionDefaults: Array<{
    financeDimensionDefinitionId: string;
    dimensionCode: string;
    financeDimensionValueId: string;
    valueCode: string;
    valueName: string;
  }>;
}

export interface MobilePosStoreInput {
  code: string;
  name: string;
  status: MobilePosStoreStatus;
  companyProfileId?: string;
  locationId: string;
  warehouseId?: string;
  currencyCode: string;
  timeZoneId: string;
  defaultWalkInBusinessPartnerId: string;
  defaultWalkInBusinessPartnerRoleId: string;
  offlinePolicyId?: string;
  notes?: string;
  rowVersion?: string;
  dimensionDefaults: Array<{
    financeDimensionDefinitionId: string;
    financeDimensionValueId: string;
  }>;
}

export interface MobilePosOfflinePolicy {
  id: string;
  name: string;
  isActive: boolean;
  authorizationWindowMinutes: number;
  maximumTransactionAmount?: number;
  maximumAggregateAmount?: number;
  maximumTransactionCount?: number;
  maximumOfflineAgeMinutes: number;
  allowCashSale: boolean;
  allowCashReceipt: boolean;
  allowPartialPayment: boolean;
  allowReturns: boolean;
  allowReversals: boolean;
  allowProvisionalReceipt: boolean;
  allowDayEndSubmissionWithPendingSync: boolean;
  requireExternalReferenceForElectronicTender: boolean;
  rowVersion: string;
}

export type MobilePosOfflinePolicyInput = Omit<MobilePosOfflinePolicy, 'id'>;

export interface MobilePosTillPaymentMethod {
  paymentMethodId: string;
  code: string;
  name: string;
  type: string;
  requiresBankAccount: boolean;
  requiresReference: boolean;
  allowOnline: boolean;
  allowOffline: boolean;
  requireExternalAuthorizationReference: boolean;
  displayOrder: number;
}

export interface MobilePosTill {
  id: string;
  mobilePosStoreId: string;
  storeCode: string;
  storeName: string;
  tillNumber: string;
  name: string;
  status: MobilePosTillStatus;
  liquidityAccountId: string;
  liquidityAccountCode: string;
  currencyCode: string;
  notes?: string;
  lastHeartbeatAtUtc?: string;
  rowVersion: string;
  paymentMethods: MobilePosTillPaymentMethod[];
}

export interface MobilePosTillInput {
  mobilePosStoreId: string;
  tillNumber: string;
  name: string;
  status: MobilePosTillStatus;
  liquidityAccountId: string;
  notes?: string;
  rowVersion?: string;
  paymentMethods: Array<{
    paymentMethodId: string;
    allowOnline: boolean;
    allowOffline: boolean;
    requireExternalAuthorizationReference: boolean;
    displayOrder: number;
  }>;
}

export interface MobilePosDevice {
  id: string;
  deviceName: string;
  manufacturer?: string;
  model?: string;
  operatingSystemVersion?: string;
  appVersion?: string;
  printerAdapterKey?: string;
  scannerAdapterKey?: string;
  status: MobilePosDeviceStatus;
  requestedByUserId: string;
  requestedAtUtc: string;
  mobilePosStoreId?: string;
  storeName?: string;
  mobilePosTillId?: string;
  tillNumber?: string;
  approvedAtUtc?: string;
  revokedAtUtc?: string;
  statusReason?: string;
  lastSeenAtUtc?: string;
  lastSyncAtUtc?: string;
  revocationEpoch: number;
  rowVersion: string;
}

const base = '/administration/mobile-pos/v1';

export const mobilePosAdminService = {
  references: () => apiService.get<MobilePosAdministrationReferences>(`${base}/references`),
  stores: () => apiService.get<MobilePosStore[]>(`${base}/stores`),
  saveStore: (id: string | undefined, input: MobilePosStoreInput) =>
    id
      ? apiService.put<MobilePosStore>(`${base}/stores/${id}`, input)
      : apiService.post<MobilePosStore>(`${base}/stores`, input),
  policies: () => apiService.get<MobilePosOfflinePolicy[]>(`${base}/offline-policies`),
  savePolicy: (id: string | undefined, input: MobilePosOfflinePolicyInput) =>
    id
      ? apiService.put<MobilePosOfflinePolicy>(`${base}/offline-policies/${id}`, input)
      : apiService.post<MobilePosOfflinePolicy>(`${base}/offline-policies`, input),
  tills: () => apiService.get<MobilePosTill[]>(`${base}/tills`),
  saveTill: (id: string | undefined, input: MobilePosTillInput) =>
    id
      ? apiService.put<MobilePosTill>(`${base}/tills/${id}`, input)
      : apiService.post<MobilePosTill>(`${base}/tills`, input),
  assignUser: (input: {
    userId: string;
    mobilePosStoreId: string;
    effectiveFromUtc: string;
    effectiveToUtc?: string;
    reason: string;
  }) => apiService.post<void>(`${base}/user-store-assignments`, input),
  devices: () => apiService.get<MobilePosDevice[]>(`${base}/devices`),
  approveDevice: (
    id: string,
    input: { mobilePosStoreId: string; mobilePosTillId: string; reason: string; rowVersion: string }
  ) => apiService.post<MobilePosDevice>(`${base}/devices/${id}/approve`, input),
  revokeDevice: (id: string, input: { reason: string; rowVersion: string }) =>
    apiService.post<MobilePosDevice>(`${base}/devices/${id}/revoke`, input),
};
