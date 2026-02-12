export type DepreciationMethod =
  | 'StraightLine'
  | 'DecliningBalance'
  | 'DoubleDecliningBalance'
  | 'SumOfYearsDigits'
  | 'UnitsOfProduction'
  | 'None';

export type DepreciationConvention =
  | 'FullMonth'
  | 'MidMonth'
  | 'HalfYear'
  | 'ActualDays';

export type FixedAssetStatus =
  | 'Draft'
  | 'Active'
  | 'FullyDepreciated'
  | 'Disposed'
  | 'HeldForSale'
  | 'WrittenOff'
  | 'UnderConstruction';

export interface FixedAssetCategory {
  id: string;
  name: string;
  code: string;
  description?: string;
  defaultMethod: DepreciationMethod;
  defaultUsefulLifeMonths: number;
  defaultResidualValuePercent: number;
  assetAccountId: string;
  accumulatedDepreciationAccountId: string;
  depreciationExpenseAccountId: string;
  gainOnDisposalAccountId?: string;
  lossOnDisposalAccountId?: string;
  createdAt: string;
  createdBy?: string;
}

export interface CreateFixedAssetCategoryDto {
  name: string;
  code: string;
  description?: string;
  defaultMethod: DepreciationMethod;
  defaultUsefulLifeMonths: number;
  defaultResidualValuePercent: number;
  assetAccountId: string;
  accumulatedDepreciationAccountId: string;
  depreciationExpenseAccountId: string;
  gainOnDisposalAccountId?: string;
  lossOnDisposalAccountId?: string;
}

export interface UpdateFixedAssetCategoryDto extends CreateFixedAssetCategoryDto { }

export interface FixedAsset {
  id: string;
  assetCode: string;
  name: string;
  description?: string;
  fixedAssetCategoryId: string;
  fixedAssetCategoryName?: string;
  purchaseDate: string;
  placedInServiceDate?: string;
  purchasePrice: number;
  installationCost: number;
  taxAmount: number;
  acquisitionCost: number;
  netBookValue: number;
  depreciationMethod: DepreciationMethod;
  depreciationConvention: DepreciationConvention;
  usefulLifeMonths: number;
  residualValue: number;
  status: FixedAssetStatus;
  disposalDate?: string;
  maintenanceAssetId?: string;
  serialNumber?: string;
  createdAt: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface CreateFixedAssetDto {
  assetCode: string;
  name: string;
  description?: string;
  fixedAssetCategoryId: string;
  purchaseDate: string;
  placedInServiceDate?: string;
  purchasePrice: number;
  installationCost: number;
  taxAmount: number;
  acquisitionCost?: number;
  depreciationMethod: DepreciationMethod;
  depreciationConvention: DepreciationConvention;
  usefulLifeMonths: number;
  residualValue: number;
  maintenanceAssetId?: string;
  serialNumber?: string;
}

export interface UpdateFixedAssetDto extends CreateFixedAssetDto {
  status: FixedAssetStatus;
  disposalDate?: string;
}

export interface RunDepreciationDto {
  fiscalPeriodId: string;
  fixedAssetId?: string;
  postToGl: boolean;
  postingDate?: string;
}

export interface AssetDepreciationSchedule {
  id: string;
  fixedAssetId: string;
  fiscalPeriodId: string;
  depreciationAmount: number;
  accumulatedDepreciation: number;
  netBookValue: number;
  isPosted: boolean;
  postedDate?: string;
  journalEntryId?: string;
  isProjected: boolean;
}

export interface FixedAssetGlAccountOption {
  id: string;
  accountNumber: string;
  accountName: string;
  accountType: string;
  accountCategory?: string;
  accountSubCategory?: string;
}

export interface FixedAssetGlAccountOptions {
  assetAccounts: FixedAssetGlAccountOption[];
  accumulatedDepreciationAccounts: FixedAssetGlAccountOption[];
  depreciationExpenseAccounts: FixedAssetGlAccountOption[];
  gainOnDisposalAccounts: FixedAssetGlAccountOption[];
  lossOnDisposalAccounts: FixedAssetGlAccountOption[];
}

export type AssetTransferStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Completed'
  | 'Rejected'
  | 'Cancelled';

export type AssetTransferType =
  | 'Internal'
  | 'External'
  | 'Custodial';

export interface AssetTransfer {
  id: string;
  fixedAssetId: string;
  fixedAssetName?: string;
  assetCode?: string;
  transferDate: string;
  transferType: AssetTransferType;
  status: AssetTransferStatus;
  fromLocation?: string;
  fromCustodianId?: string;
  fromCustodianName?: string;
  toLocation: string;
  toCustodianId?: string;
  toCustodianName?: string;
  reason?: string;
  transferCost?: number;
  requestedById?: string;
  requestedByName?: string;
  approvedById?: string;
  approvedByName?: string;
  approvedAt?: string;
  comments?: string;
  referenceNumber?: string;
  createdAt: string;
}

export interface RequestAssetTransferDto {
  fixedAssetId: string;
  transferDate: string;
  transferType: AssetTransferType;
  toLocation: string;
  toCustodianId?: string;
  reason?: string;
  transferCost?: number;
}

export interface ApproveAssetTransferDto {
  comments?: string;
}

export type DisposalType =
  | 'Sale'
  | 'Scrap'
  | 'Donation'
  | 'DamageTheft';

export type AssetDisposalStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Approved'
  | 'Completed'
  | 'Rejected'
  | 'Cancelled';

export interface AssetDisposal {
  id: string;
  fixedAssetId: string;
  fixedAssetName?: string;
  assetCode?: string;
  disposalDate: string;
  disposalType: DisposalType;
  status: AssetDisposalStatus;
  reason?: string;
  saleProceeds: number;
  disposalCost: number;
  netBookValueAtDisposal: number;
  gainOrLoss: number;
  buyerName?: string;
  referenceNumber?: string;
  requestedById?: string;
  requestedByName?: string;
  approvedById?: string;
  approvedByName?: string;
  approvedAt?: string;
  comments?: string;
  createdAt: string;
}

export interface RequestAssetDisposalDto {
  fixedAssetId: string;
  disposalDate: string;
  disposalType: DisposalType;
  reason?: string;
  saleProceeds: number;
  disposalCost: number;
  buyerName?: string;
}

export interface ApproveAssetDisposalDto {
  comments?: string;
}

export type VerificationSessionStatus =
  | 'Draft'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled';

export type AssetCondition =
  | 'Excellent'
  | 'Good'
  | 'Fair'
  | 'Poor'
  | 'Broken'
  | 'Missing';

export interface AssetVerificationSession {
  id: string;
  sessionName: string;
  referenceNumber: string;
  status: VerificationSessionStatus;
  scheduledDate: string;
  completionDate?: string;
  description?: string;
  verifiedById?: string;
  verifiedByName?: string;
  totalItems: number;
  verifiedItems: number;
}

export interface CreateAssetVerificationSessionDto {
  sessionName: string;
  scheduledDate: string;
  description?: string;
  assetIds?: string[];
}

export interface AssetVerificationItem {
  id: string;
  sessionId: string;
  fixedAssetId: string;
  fixedAssetName?: string;
  assetCode?: string;
  isVerified: boolean;
  verificationDate?: string;
  condition: AssetCondition;
  currentLocation?: string;
  notes?: string;
  imageUrl?: string;
}

export interface VerifyAssetDto {
  condition: AssetCondition;
  currentLocation?: string;
  notes?: string;
  imageUrl?: string;
}

// Bulk Import Types
export interface BulkImportResult {
  totalRows: number;
  successCount: number;
  errorCount: number;
  errors: BulkImportError[];
  successfulAssetCodes: string[];
}

export interface BulkImportError {
  rowNumber: number;
  assetCode?: string;
  field: string;
  error: string;
}
