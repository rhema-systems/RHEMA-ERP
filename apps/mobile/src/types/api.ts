export type RuntimeEnvironment = "TEST" | "UAT" | "PRODUCTION";

export interface ServerProfile {
  environment: RuntimeEnvironment;
  apiBaseUrl: string;
}

export interface TenantInfo {
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  isDefault: boolean;
  accessLevel: string;
}

export interface UserInfo {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  currentTenantId?: string;
  currentTenantCode?: string;
  currentTenantName?: string;
  accessibleTenants: TenantInfo[];
  isActive: boolean;
  mustChangePassword?: boolean;
  roles: string[];
  permissions: string[];
  authenticationProvider: string;
  employeeId?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
  tenantCode?: string;
  rememberMe: boolean;
  twoFactorCode?: string;
}

export interface LoginResponse {
  token?: string;
  refreshToken?: string;
  expiresAt?: string;
  user?: UserInfo;
  requiresTwoFactor: boolean;
  twoFactorToken?: string;
}

export interface SelectTenantResponse {
  token: string;
  refreshToken: string;
  expiresAt: string;
  user: UserInfo;
}

export type MobilePosDeviceStatus = "Pending" | "Active" | "Suspended" | "Revoked" | "Retired" | number;

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
  mobilePosStoreId?: string;
  storeName?: string;
  mobilePosTillId?: string;
  tillNumber?: string;
  statusReason?: string;
  lastSeenAtUtc?: string;
  revocationEpoch: number;
}

export interface MobilePosStore {
  id: string;
  code: string;
  name: string;
  status: string | number;
  currencyCode: string;
  timeZoneId: string;
  defaultWalkInBusinessPartnerId: string;
  defaultWalkInBusinessPartnerRoleId: string;
  defaultWalkInCustomerCode: string;
  defaultWalkInCustomerName: string;
  offlinePolicyId?: string;
  offlinePolicyName?: string;
}

export interface MobilePosPaymentMethod {
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
  status: string | number;
  liquidityAccountId: string;
  liquidityAccountCode: string;
  currencyCode: string;
  paymentMethods: MobilePosPaymentMethod[];
}

export interface MobilePosOfflinePolicy {
  id: string;
  name: string;
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
}

export interface MobilePosBootstrap {
  environmentName: RuntimeEnvironment;
  userId: string;
  userName: string;
  device: MobilePosDevice;
  store: MobilePosStore;
  till: MobilePosTill;
  offlinePolicy?: MobilePosOfflinePolicy;
  currentTillSessionId?: string;
  serverTimeUtc: string;
}

export interface DeviceEnrollmentRequest {
  installationId: string;
  deviceName: string;
  manufacturer?: string;
  model?: string;
  operatingSystemVersion?: string;
  appVersion?: string;
  printerAdapterKey?: string;
  scannerAdapterKey?: string;
}
