import type { FinanceSourceDocumentDimension, FinanceSourceDocumentDimensionInput } from './finance';

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
  | 'UnderConstruction'
  | 'OnHold'
  | 'Acquired'
  | 'Capitalized'
  | 'PendingApproval'
  | 'Rejected';

export interface FixedAssetCategory {
  id: string;
  name: string;
  code: string;
  description?: string;
  defaultMethod: DepreciationMethod;
  defaultUsefulLifeMonths: number;
  defaultResidualValuePercent: number;
  defaultDiminishingBalanceRatePercent: number;
  defaultLifetimeProductionCapacity: number;
  assetAccountId: string;
  accumulatedDepreciationAccountId: string;
  depreciationExpenseAccountId: string;
  gainOnDisposalAccountId?: string;
  lossOnDisposalAccountId?: string;
  revaluationSurplusAccountId?: string;
  aucAccountId?: string;
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
  defaultDiminishingBalanceRatePercent: number;
  defaultLifetimeProductionCapacity: number;
  assetAccountId: string;
  accumulatedDepreciationAccountId: string;
  depreciationExpenseAccountId: string;
  gainOnDisposalAccountId?: string;
  lossOnDisposalAccountId?: string;
  revaluationSurplusAccountId?: string;
  aucAccountId?: string;
}

export interface UpdateFixedAssetCategoryDto extends CreateFixedAssetCategoryDto { }

export interface FixedAsset {
  id: string;
  assetCode: string;
  name: string;
  description?: string;
  location?: string;
  currentCustodianId?: string;
  currentCustodianName?: string;
  currentSegmentString?: string;
  currentSegmentLookupValueId?: string;
  fixedAssetCategoryId: string;
  fixedAssetCategoryName?: string;
  purchaseDate: string;
  placedInServiceDate?: string;
  capitalizationDate?: string;
  purchasePrice: number;
  installationCost: number;
  taxAmount: number;
  acquisitionCost: number;
  netBookValue: number;
  depreciationMethod: DepreciationMethod;
  depreciationConvention: DepreciationConvention;
  usefulLifeMonths: number;
  residualValue: number;
  diminishingBalanceRatePercent: number;
  lifetimeProductionCapacity: number;
  accumulatedProductionUnits: number;
  status: FixedAssetStatus;
  disposalDate?: string;
  functionalCurrencyCode: string;
  transactionCurrencyCode?: string;
  exchangeRate?: number;
  exchangeRateId?: string;
  exchangeRateDate?: string;
  sourceDocumentType?: string;
  sourceDocumentId?: string;
  sourceDocumentLineId?: string;
  journalEntryId?: string;
  postingEventId?: string;
  capitalizedAt?: string;
  capitalizationApprovalSnapshot?: FixedAssetCapitalizationApprovalSnapshot;
  financeDimensions?: FinanceSourceDocumentDimension;
  capitalizationApprovalSnapshotHash?: string;
  capitalizationApprovalWorkflowInstanceId?: string;
  capitalizationApprovalSubmittedByUserId?: string;
  capitalizationApprovalSubmittedAt?: string;
  capitalizationApprovalApprovedByUserId?: string;
  capitalizationApprovalApprovedAt?: string;
  capitalizationApprovalInvalidatedAt?: string;
  capitalizationApprovalInvalidationReason?: string;
  capitalizationReversalJournalEntryId?: string;
  capitalizationReversalPostingEventId?: string;
  capitalizationReversedAt?: string;
  capitalizationReversalReason?: string;
  maintenanceAssetId?: string;
  serialNumber?: string;
  createdAt: string;
  createdBy?: string;
  updatedAt?: string;
  updatedBy?: string;
  bookValues: FixedAssetBookValue[];
}

export interface FixedAssetCapitalizationApprovalSnapshot {
  version: number;
  fixedAssetId: string;
  assetCode: string;
  fixedAssetCategoryId: string;
  debitAccountId: string;
  creditAccountId: string;
  usesCategoryAucAccount: boolean;
  capitalizationDate: string;
  transactionAmount: number;
  functionalCurrencyCode: string;
  transactionCurrencyCode: string;
  exchangeRate: number;
  exchangeRateId?: string;
  exchangeRateDate: string;
  reference: string;
  reason: string;
  sourceDocumentType: string;
  sourceDocumentId: string;
  sourceDocumentLineId?: string;
  assetEvidenceHash: string;
}

export interface SubmitFixedAssetCapitalizationDto {
  capitalizationDate: string;
  creditAccountId?: string;
  reference?: string;
  reason: string;
  comments?: string;
  amount?: number;
  transactionCurrencyCode?: string;
  exchangeRate?: number;
  exchangeRateId?: string;
  exchangeRateDate?: string;
  financeDimensions?: FinanceSourceDocumentDimensionInput;
}

export interface FixedAssetApprovalActionDto {
  reason?: string;
  comments?: string;
}

export interface FixedAssetBookValue {
  id: string;
  fixedAssetId: string;
  accountingBookId: string;
  bookClassification: string;
  accountingBookName?: string;
  acquisitionCost: number;
  accumulatedDepreciation: number;
  netBookValue: number;
  residualValue: number;
  usefulLifeMonths: number;
  remainingUsefulLifeMonths?: number;
  depreciationMethod: DepreciationMethod;
  depreciationConvention: DepreciationConvention;
  diminishingBalanceRatePercent: number;
  lifetimeProductionCapacity: number;
  accumulatedProductionUnits: number;
  placedInServiceDate?: string;
  openingAsOfDate?: string;
  openingYtdDepreciation: number;
  lastDepreciationDate?: string;
  openingPostedToGl: boolean;
  openingPostedDate?: string;
  openingSource: string;
  capitalizationDate?: string;
  capitalizationJournalEntryId?: string;
  capitalizationPostingEventId?: string;
  capitalizationReversalJournalEntryId?: string;
  capitalizationReversalPostingEventId?: string;
  capitalizationReversedAt?: string;
  sourceDocumentType?: string;
  sourceDocumentId?: string;
  sourceDocumentLineId?: string;
}

export type FixedAssetCapitalizationReversalStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Posted'
  | 'Failed';

/**
 * Immutable maker-checker evidence for FR-GL-008/FR-GL-010. The original and
 * reversal posting IDs let the workspace show the exact ledger lineage instead
 * of presenting a destructive "undo" action.
 */
export interface FixedAssetCapitalizationReversal {
  id: string;
  fixedAssetId: string;
  assetCode: string;
  assetName: string;
  originalPostingEventId: string;
  originalJournalEntryId: string;
  reversalPostingEventId?: string;
  reversalJournalEntryId?: string;
  status: FixedAssetCapitalizationReversalStatus;
  reason: string;
  impactAssessment: string;
  requestedReversalDate: string;
  requestedByUserId: string;
  requestedByUserName: string;
  requestedAt: string;
  reviewedByUserId?: string;
  reviewedByUserName?: string;
  reviewedAt?: string;
  reviewComment?: string;
  postedAt?: string;
  failureReason?: string;
}

export interface RequestFixedAssetCapitalizationReversalDto {
  reversalDate: string;
  reason: string;
  impactAssessment: string;
}

export interface ReviewFixedAssetCapitalizationReversalDto {
  approved: boolean;
  reviewComment: string;
}

export interface CreateFixedAssetDto {
  assetCode: string;
  name: string;
  description?: string;
  location?: string;
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
  diminishingBalanceRatePercent: number;
  lifetimeProductionCapacity: number;
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
  bookClassification?: string;
  productionUsageEntries?: FixedAssetProductionUsage[];
  financeDimensions?: FinanceSourceDocumentDimensionInput;
}

export interface FixedAssetProductionUsage {
  fixedAssetId: string;
  bookClassification?: string;
  unitsConsumed: number;
  evidenceReference: string;
  evidenceNotes?: string;
}

export interface AssetDepreciationSchedule {
  id: string;
  fixedAssetId: string;
  fixedAssetDepreciationRunId?: string;
  accountingBookId?: string;
  bookClassification: string;
  fiscalPeriodId: string;
  depreciationAmount: number;
  depreciationMethodSnapshot: DepreciationMethod;
  diminishingBalanceRatePercentSnapshot: number;
  lifetimeProductionCapacitySnapshot: number;
  periodProductionUnits: number;
  cumulativeProductionUnitsBefore: number;
  cumulativeProductionUnitsAfter: number;
  productionEvidenceReference?: string;
  productionEvidenceNotes?: string;
  placedInServiceDateSnapshot?: string;
  depreciationConventionSnapshot: DepreciationConvention;
  conventionFactor: number;
  conventionBasis: string;
  conventionEligibleFromDate?: string;
  conventionEligibleToDate?: string;
  fiscalPeriodStartDateSnapshot?: string;
  fiscalPeriodEndDateSnapshot?: string;
  fiscalYearStartDateSnapshot?: string;
  fiscalYearEndDateSnapshot?: string;
  accumulatedDepreciation: number;
  netBookValue: number;
  isPosted: boolean;
  postedDate?: string;
  journalEntryId?: string;
  isProjected: boolean;
  correctionSequence: number;
  isReversed: boolean;
  reversedAt?: string;
  reversalJournalEntryId?: string;
  reversalPostingEventId?: string;
  depreciationReversalId?: string;
}

export interface FixedAssetLocationOption {
  id: string;
  locationLevelId: string;
  code: string;
  name: string;
  displayName: string;
  levelName?: string;
  parentLocationId?: string;
  isLeaf: boolean;
}

export interface FixedAssetDepreciationRun {
  id: string;
  fiscalPeriodId: string;
  fixedAssetId?: string;
  bookClassification: string;
  postingDate: string;
  status: 'Calculated' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Posted' | 'Failed' | 'Reversed';
  totalDepreciationAmount: number;
  correctionSequence: number;
  journalEntryId?: string;
  postingEventId?: string;
  calculatedAt?: string;
  postedAt?: string;
  failedAt?: string;
  failureReason?: string;
  assetCount: number;
  preparedBy?: string;
}

export type FixedAssetDepreciationReversalStatus =
  | 'PendingApproval'
  | 'Approved'
  | 'Rejected'
  | 'Posted'
  | 'Failed';

/**
 * Maker-checker and journal lineage for a posted depreciation correction.
 * Original run evidence is retained even after the compensating entry is posted.
 */
export interface FixedAssetDepreciationReversal {
  id: string;
  originalDepreciationRunId: string;
  periodCode: string;
  bookClassification: string;
  totalDepreciationAmount: number;
  originalCorrectionSequence: number;
  originalPostingEventId: string;
  originalJournalEntryId: string;
  reversalPostingEventId?: string;
  reversalJournalEntryId?: string;
  status: FixedAssetDepreciationReversalStatus;
  reason: string;
  impactAssessment: string;
  requestedReversalDate: string;
  requestedByUserId: string;
  requestedByUserName: string;
  requestedAt: string;
  reviewedByUserId?: string;
  reviewedByUserName?: string;
  reviewedAt?: string;
  reviewComment?: string;
  postedAt?: string;
  failureReason?: string;
}

export interface RequestFixedAssetDepreciationReversalDto {
  reversalDate: string;
  reason: string;
  impactAssessment: string;
}

export interface ReviewFixedAssetDepreciationReversalDto {
  approved: boolean;
  reviewComment: string;
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
  revaluationSurplusAccounts: FixedAssetGlAccountOption[];
  aucAccounts: FixedAssetGlAccountOption[];
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
  | 'Custodial'
  | 'GlReclassification';

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
  fromSegmentString?: string;
  fromSegmentLookupValueId?: string;
  toSegmentString?: string;
  toSegmentLookupValueId?: string;
  fromFixedAssetCategoryId?: string;
  fromFixedAssetCategoryName?: string;
  toFixedAssetCategoryId?: string;
  toFixedAssetCategoryName?: string;
  accountingBookId?: string;
  bookClassification: string;
  fromAssetAccountId?: string;
  toAssetAccountId?: string;
  fromAccumulatedDepreciationAccountId?: string;
  toAccumulatedDepreciationAccountId?: string;
  fromAccumulatedImpairmentAccountId?: string;
  toAccumulatedImpairmentAccountId?: string;
  fromRevaluationSurplusAccountId?: string;
  toRevaluationSurplusAccountId?: string;
  reclassificationAssetCarryingAmount: number;
  reclassificationAccumulatedDepreciation: number;
  reclassificationAccumulatedImpairment: number;
  reclassificationRevaluationSurplus: number;
  accountingDate?: string;
  fiscalPeriodId?: string;
  reason?: string;
  transferCost?: number;
  requestedById?: string;
  requestedByName?: string;
  approvedById?: string;
  approvedByName?: string;
  approvedAt?: string;
  completedAt?: string;
  postedAt?: string;
  failedAt?: string;
  comments?: string;
  failureReason?: string;
  referenceNumber?: string;
  idempotencyKey?: string;
  workflowInstanceId?: string;
  journalEntryId?: string;
  postingEventId?: string;
  createdAt: string;
  financeDimensions?: FinanceSourceDocumentDimension;
}

export interface RequestAssetTransferDto {
  fixedAssetId: string;
  transferDate: string;
  transferType: AssetTransferType;
  toLocation: string;
  toCustodianId?: string;
  toSegmentString?: string;
  toSegmentLookupValueId?: string;
  toFixedAssetCategoryId?: string;
  accountingBookId?: string;
  bookClassification?: string;
  accountingDate?: string;
  reason?: string;
  transferCost?: number;
  idempotencyKey?: string;
  financeDimensions?: FinanceSourceDocumentDimensionInput;
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

export type AssetDisposalScope = 'WholeAsset' | 'PartialPortion' | 'Component';

export type AssetDisposalSettlementMode = 'NotApplicable' | 'CreditSale' | 'ImmediateReceipt';

export type AssetDisposalSettlementStatus = 'NotApplicable' | 'Pending' | 'Invoiced' | 'Settled' | 'Failed';

export type AssetDisposalSaleTaxTreatment = 'Standard' | 'Exempt' | 'ZeroRated' | 'OutOfScope';

export interface AssetDisposal {
  id: string;
  fixedAssetId: string;
  fixedAssetName?: string;
  assetCode?: string;
  disposalDate: string;
  disposalType: DisposalType;
  disposalScope: AssetDisposalScope;
  disposedPortionPercent: number;
  componentReference?: string;
  componentDescription?: string;
  allocationEvidenceReference?: string;
  allocationEvidenceNotes?: string;
  status: AssetDisposalStatus;
  reason?: string;
  saleProceeds: number;
  disposalCost: number;
  netProceeds: number;
  proceedsCurrencyCode: string;
  proceedsFunctionalAmount: number;
  proceedsExchangeRateId?: string;
  proceedsExchangeRateValue: number;
  proceedsExchangeRateSource: string;
  proceedsExchangeRateDate: string;
  proceedsExchangeRateType: string;
  proceedsExchangeRateQuoteSide: string;
  netBookValueAtDisposal: number;
  finalDepreciationAmount: number;
  finalDepreciationFromDate?: string;
  finalDepreciationToDate?: string;
  finalDepreciationPeriodDays: number;
  finalDepreciationEligibleDays: number;
  finalDepreciationProrationBasis?: string;
  finalDepreciationMethodSnapshot?: DepreciationMethod;
  finalDepreciationScheduleId?: string;
  finalDepreciationProductionUnits: number;
  finalDepreciationDiminishingRatePercent: number;
  finalDepreciationLifetimeProductionCapacity: number;
  finalDepreciationCumulativeProductionUnitsBefore: number;
  finalDepreciationCumulativeProductionUnitsAfter: number;
  finalDepreciationEvidenceReference?: string;
  finalDepreciationEvidenceNotes?: string;
  gainOrLoss: number;
  remainingNetBookValueAfterDisposal: number;
  revaluationSurplusAtDisposal: number;
  revaluationSurplusAccountId?: string;
  retainedEarningsAccountId?: string;
  revaluationSurplusTransferAmount: number;
  buyerName?: string;
  buyerBusinessPartnerId?: string;
  settlementMode: AssetDisposalSettlementMode;
  settlementStatus: AssetDisposalSettlementStatus;
  saleTaxGroupId?: string;
  saleTaxTreatment: AssetDisposalSaleTaxTreatment;
  settlementPaymentTermId?: string;
  financeDimensions?: FinanceSourceDocumentDimension;
  settlementPaymentMethodId?: string;
  settlementBankAccountId?: string;
  settlementLiquidityAccountId?: string;
  settlementReference?: string;
  customerInvoiceId?: string;
  customerPaymentId?: string;
  settlementInvoiceAmount: number;
  settlementTaxAmount: number;
  settlementCompletedAt?: string;
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
  disposalScope: AssetDisposalScope;
  disposedPortionPercent: number;
  componentReference?: string;
  componentDescription?: string;
  allocationEvidenceReference?: string;
  allocationEvidenceNotes?: string;
  reason?: string;
  saleProceeds: number;
  disposalCost: number;
  proceedsCurrencyCode?: string;
  proceedsExchangeRateId?: string;
  buyerName?: string;
  buyerBusinessPartnerId?: string;
  settlementMode?: AssetDisposalSettlementMode;
  saleTaxGroupId?: string;
  saleTaxTreatment?: AssetDisposalSaleTaxTreatment;
  settlementPaymentTermId?: string;
  settlementPaymentMethodId?: string;
  settlementBankAccountId?: string;
  settlementLiquidityAccountId?: string;
  settlementReference?: string;
  finalDepreciationProductionUnits?: number;
  finalDepreciationEvidenceReference?: string;
  finalDepreciationEvidenceNotes?: string;
  financeDimensions?: FinanceSourceDocumentDimensionInput;
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
  isDryRun: boolean;
  errors: BulkImportError[];
  successfulAssetCodes: string[];
}

export interface BulkImportError {
  rowNumber: number;
  assetCode?: string;
  field: string;
  error: string;
}

// ===== VALUATIONS / IMPAIRMENT =====

export type ValuationType = 'Revaluation' | 'Impairment' | 'ImpairmentReversal';

export interface CreateAssetValuationDto {
  fixedAssetId: string;
  valuationDate: string;
  valuationType: ValuationType;
  fairValue: number;
  sourceImpairmentValuationId?: string;
  unimpairedCarryingAmountCap?: number;
  revisedUsefulLifeMonths?: number;
  valuerName?: string;
  valuationMethod?: string;
  valuationReportReference?: string;
  reason?: string;
  notes?: string;
  financeDimensions?: FinanceSourceDocumentDimensionInput;
}

export interface CreateBulkAssetValuationDto {
  fixedAssetIds: string[];
  valuationDate: string;
  valuationType: ValuationType;
  indexPercentage: number;
  valuerName?: string;
  valuationMethod?: string;
  valuationReportReference?: string;
  reason?: string;
  notes?: string;
  financeDimensions?: FinanceSourceDocumentDimensionInput;
}

export interface AssetValuation {
  id: string;
  fixedAssetId: string;
  assetCode?: string;
  assetName?: string;
  valuationDate: string;
  valuationType: ValuationType;
  carryingAmountBefore: number;
  fairValue: number;
  carryingAmountAfter: number;
  revaluationSurplus: number;
  revaluationDeficit: number;
  impairmentLoss: number;
  impairmentReversal: number;
  sourceImpairmentValuationId?: string;
  outstandingImpairmentBefore: number;
  unimpairedCarryingAmountCap: number;
  revisedUsefulLifeMonths?: number;
  valuerName?: string;
  valuationMethod?: string;
  valuationReportReference?: string;
  reason?: string;
  notes?: string;
  isPostedToGL: boolean;
  isCorrected: boolean;
  correctionId?: string;
  correctedAt?: string;
  journalEntryId?: string;
  postedDate?: string;
  createdAt: string;
  financeDimensions?: FinanceSourceDocumentDimension;
}

export interface RequestAssetValuationCorrectionDto {
  reason: string;
  impactAssessment: string;
  reversalDate?: string;
}

export interface ReviewAssetValuationCorrectionDto {
  approved: boolean;
  reviewComment: string;
}

export interface AssetValuationCorrection {
  id: string;
  originalValuationId: string;
  fixedAssetId: string;
  assetCode?: string;
  valuationType: string;
  status: 'PendingApproval' | 'Approved' | 'Rejected' | 'Posted';
  reason: string;
  impactAssessment: string;
  requestedReversalDate: string;
  requestedByUserName: string;
  requestedAt: string;
  reviewedByUserName?: string;
  reviewedAt?: string;
  reviewComment?: string;
  originalJournalEntryId: string;
  reversalJournalEntryId?: string;
  postedAt?: string;
  failureReason?: string;
}

export interface BulkOperationResult<T> {
  totalCount: number;
  successCount: number;
  failureCount: number;
  successfulItems: T[];
  errors: string[];
}

// ===== BULK DISPOSALS =====

export interface RequestBulkAssetDisposalDto {
  fixedAssetIds: string[];
  disposalDate: string;
  disposalType: DisposalType;
  reason?: string;
  saleProceeds?: number;
  disposalCost?: number;
  buyerName?: string;
}
