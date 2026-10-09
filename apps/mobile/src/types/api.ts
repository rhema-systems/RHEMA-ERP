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

export interface MobilePosBankAccountOption {
  bankAccountId: string;
  accountName: string;
  bankName: string;
  maskedAccountNumber: string;
  currencyCode: string;
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

export interface MobilePosOfflinePaymentMethodSnapshot {
  paymentMethodId: string;
  code: string;
  name: string;
  type: string;
  requiresReference: boolean;
  requireExternalAuthorizationReference: boolean;
}

export interface MobilePosOfflineGrantPolicySnapshot {
  policyId: string;
  policyName: string;
  policyVersionUtc: string;
  currencyCode: string;
  defaultWalkInBusinessPartnerId: string;
  defaultWalkInBusinessPartnerRoleId: string;
  maximumTransactionAmount?: number;
  maximumAggregateAmount?: number;
  maximumTransactionCount?: number;
  maximumOfflineAgeMinutes: number;
  allowPartialPayment: boolean;
  allowProvisionalReceipt: boolean;
  allowDayEndSubmissionWithPendingSync: boolean;
  allowedCommandTypes: string[];
  allowedPaymentMethods: MobilePosOfflinePaymentMethodSnapshot[];
}

export interface MobilePosOfflineGrant {
  id: string;
  version: number;
  token: string;
  tenantId: string;
  userId: string;
  mobilePosDeviceId: string;
  mobilePosStoreId: string;
  mobilePosTillId: string;
  cashierTillSessionId: string;
  mobilePosOfflinePolicyId: string;
  issuedAtUtc: string;
  expiresAtUtc: string;
  revocationEpoch: number;
  policySnapshotHash: string;
  policy: MobilePosOfflineGrantPolicySnapshot;
}

export interface MobilePosCustomerSearchResult {
  businessPartnerId: string;
  businessPartnerRoleId: string;
  code: string;
  name: string;
  email?: string;
  phone?: string;
  currencyCode: string;
  isDefaultWalkInCustomer: boolean;
}

export interface OutstandingInvoice {
  id: string;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate?: string;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  daysOverdue: number;
  currencyCode: string;
  earlyPaymentDiscountPercentage?: number;
  earlyPaymentDiscountDueDate?: string;
  isDiscountAvailable: boolean;
  requiresTaxAdjustmentForDiscount: boolean;
  discountAmount?: number;
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

export interface MobilePosCatalogueItem {
  inventoryItemId: string;
  itemCode: string;
  name: string;
  description?: string;
  barcode?: string;
  alternateBarcode?: string;
  qrCode?: string;
  itemType: string;
  unitOfMeasureId?: string;
  unitOfMeasureCode: string;
  unitPrice: number;
  currencyCode: string;
  defaultTaxGroupId?: string;
  availableQuantity?: number;
  isAvailable: boolean;
  changedAtUtc: string;
}

export interface MobilePosCatalogueChangePage {
  snapshotAtUtc: string;
  nextCursor?: string;
  hasMore: boolean;
  upserts: MobilePosCatalogueItem[];
  tombstoneInventoryItemIds: string[];
}

export interface MobilePosSalePreviewLineInput {
  clientLineId: string;
  inventoryItemId: string;
  quantity: number;
  discountPercentage: number;
}

export interface MobilePosSalePreviewRequest {
  installationId: string;
  businessPartnerId?: string;
  businessPartnerRoleId?: string;
  lines: MobilePosSalePreviewLineInput[];
}

export interface MobilePosSalePreviewLine {
  clientLineId: string;
  inventoryItemId: string;
  itemCode: string;
  description: string;
  quantity: number;
  unitPrice: number;
  grossAmount: number;
  discountPercentage: number;
  discountAmount: number;
  netAmount: number;
  taxGroupId?: string;
  taxTreatment: number;
  taxAmount: number;
  lineTotal: number;
  unitOfMeasureId?: string;
  unitOfMeasureCode: string;
}

export interface MobilePosSalePreview {
  businessPartnerId: string;
  businessPartnerRoleId: string;
  customerCode: string;
  customerName: string;
  usedStoreDefaultCustomer: boolean;
  currencyCode: string;
  currencyDecimalPlaces: number;
  subTotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  calculatedAtUtc: string;
  lines: MobilePosSalePreviewLine[];
}

export interface MobilePosTenderInput {
  paymentMethodId: string;
  amount: number;
  externalReference?: string;
  liquidityAccountId?: string;
  bankAccountId?: string;
}

export interface MobilePosCompleteSaleRequest {
  installationId: string;
  clientMutationId: string;
  localReference: string;
  businessPartnerId?: string;
  businessPartnerRoleId?: string;
  occurredAtUtc: string;
  expectedSubTotal: number;
  expectedTaxAmount: number;
  expectedDiscountAmount: number;
  expectedTotalAmount: number;
  lines: Array<{
    clientLineId: string;
    inventoryItemId: string;
    quantity: number;
    unitPrice: number;
    discountPercentage: number;
    taxGroupId?: string;
    taxTreatment: number;
  }>;
  tenders: MobilePosTenderInput[];
}

export interface MobilePosSaleTenderResult {
  tenderId: string;
  paymentMethodId: string;
  amount: number;
  customerPaymentId: string;
  paymentNumber: string;
  paymentStatus: string;
}

export interface MobilePosSaleResult {
  saleId: string;
  mutationReceiptId: string;
  isReplay: boolean;
  localReference: string;
  invoiceId: string;
  invoiceNumber: string;
  invoiceStatus: string;
  currencyCode: string;
  subTotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  tenders: MobilePosSaleTenderResult[];
}

export interface MobilePosReceiptReprintRequest {
  installationId: string;
  clientEventId: string;
  reason?: string;
}

export interface MobilePosReceiptLine {
  sequence: number;
  description: string;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  taxAmount: number;
  lineTotal: number;
  unitOfMeasureCode: string;
}

export interface MobilePosReceiptTender {
  sequence: number;
  paymentMethodCode: string;
  paymentMethodName: string;
  amount: number;
  externalReference?: string;
  customerPaymentId: string;
  paymentNumber: string;
  paymentStatus: string;
}

export interface MobilePosReceipt {
  receiptId: string;
  copyType: "ORIGINAL" | "REPRINT";
  copyNumber: number;
  reprintCount: number;
  auditEventId?: string;
  generatedAtUtc: string;
  reprintReason?: string;
  qrReference: string;
  tenantId: string;
  tenantCode: string;
  tenantName: string;
  storeId: string;
  storeCode: string;
  storeName: string;
  locationName: string;
  tillId: string;
  tillNumber: string;
  tillName: string;
  tillSessionId: string;
  tillSessionNumber: string;
  businessDate: string;
  deviceId: string;
  deviceName: string;
  cashierUserId: string;
  cashierName: string;
  businessPartnerId: string;
  businessPartnerRoleId: string;
  customerCode: string;
  customerName: string;
  usedStoreDefaultCustomer: boolean;
  invoiceId: string;
  invoiceNumber: string;
  invoiceStatus: string;
  localReference: string;
  occurredAtUtc: string;
  currencyCode: string;
  subTotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  lines: MobilePosReceiptLine[];
  tenders: MobilePosReceiptTender[];
}
