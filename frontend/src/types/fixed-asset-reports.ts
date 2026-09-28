import { FixedAssetStatus, DisposalType, AssetCondition } from './fixed-assets';

export interface FixedAssetReportQuery {
    fromDate?: string;
    toDate?: string;
    assetId?: string;
    categoryId?: string;
    accountId?: string;
    fiscalPeriodId?: string;
    status?: FixedAssetStatus;
    searchTerm?: string;
    bookClassification?: string;
    location?: string;
    segmentString?: string;
}

export interface FixedAssetRegisterItem {
    assetCode: string;
    name: string;
    categoryName: string;
    acquisitionDate: string;
    cost: number;
    accumulatedDepreciation: number;
    netBookValue: number;
    bookClassification: string;
    status: FixedAssetStatus;
    serialNumber?: string;
    location?: string;
}

export interface FixedAssetRegister {
    items: FixedAssetRegisterItem[];
    totalCost: number;
    totalAccumulatedDepreciation: number;
    totalNetBookValue: number;
}

export interface AssetDisposalReportItem {
    assetCode: string;
    name: string;
    disposalDate: string;
    disposalType: DisposalType;
    saleProceeds: number;
    disposalCost: number;
    netBookValue: number;
    gainLoss: number;
    buyerName?: string;
}

export interface AssetTransferReportItem {
    assetCode: string;
    name: string;
    transferDate: string;
    fromLocation?: string;
    toLocation?: string;
    fromDepartment?: string;
    toDepartment?: string;
    reason?: string;
}

export interface FixedAssetAdditionsReport {
    items: FixedAssetAdditionReportItem[];
    totalCapitalizedCost: number;
    totalPostedGlCost: number;
    totalVariance: number;
}

export interface FixedAssetAdditionReportItem {
    assetId: string;
    assetCode: string;
    name: string;
    categoryName: string;
    capitalizationDate?: string;
    sourceDocumentType?: string;
    functionalCurrencyCode: string;
    transactionCurrencyCode?: string;
    capitalizedCost: number;
    postedGlCost: number;
    variance: number;
    missingPostingReference: boolean;
}

export interface FixedAssetDepreciationReport {
    items: FixedAssetDepreciationReportItem[];
    totalDepreciation: number;
    totalPostedExpense: number;
    totalPostedAccumulatedDepreciation: number;
    totalVariance: number;
}

export interface FixedAssetDepreciationReportItem {
    scheduleId: string;
    fixedAssetId: string;
    assetCode: string;
    assetName: string;
    bookClassification: string;
    postingDate?: string;
    depreciationAmount: number;
    accumulatedDepreciation: number;
    netBookValue: number;
    postedExpenseDebit: number;
    postedAccumulatedDepreciationCredit: number;
    variance: number;
    hasPostedGlReference: boolean;
}

export interface FixedAssetAccumulatedDepreciationReport {
    items: FixedAssetAccumulatedDepreciationReportItem[];
    totalSubledgerAccumulatedDepreciation: number;
    totalPostedGlAccumulatedDepreciation: number;
    totalVariance: number;
}

export interface FixedAssetAccumulatedDepreciationReportItem {
    fixedAssetId: string;
    assetCode: string;
    assetName: string;
    bookClassification: string;
    subledgerAccumulatedDepreciation: number;
    postedGlAccumulatedDepreciation: number;
    variance: number;
}

export interface FixedAssetValuationMovementReport {
    items: FixedAssetValuationMovementReportItem[];
    totalRevaluationIncrease: number;
    totalRevaluationDecrease: number;
    totalImpairmentLoss: number;
    totalImpairmentReversal: number;
    totalPostedGlMovement: number;
}

export interface FixedAssetValuationMovementReportItem {
    valuationId: string;
    fixedAssetId: string;
    assetCode: string;
    assetName: string;
    valuationType: string;
    accountingDate: string;
    bookClassification: string;
    carryingAmountBefore: number;
    carryingAmountAfter: number;
    revaluationSurplus: number;
    revaluationDeficit: number;
    impairmentLoss: number;
    impairmentReversal: number;
    postedGlMovement: number;
    hasPostedGlReference: boolean;
}

export interface FixedAssetRollForwardReport {
    fromDate?: string;
    toDate?: string;
    rows: FixedAssetRollForwardRow[];
    totals: FixedAssetRollForwardRow;
}

export interface FixedAssetRollForwardRow {
    categoryId?: string;
    categoryName: string;
    bookClassification: string;
    assetCount: number;
    openingCost: number;
    openingAccumulatedDepreciation: number;
    openingAccumulatedImpairment: number;
    additions: number;
    revaluationIncrease: number;
    revaluationDecrease: number;
    impairmentAdditions: number;
    impairmentReversals: number;
    depreciationCharge: number;
    disposals: number;
    closingCost: number;
    closingAccumulatedDepreciation: number;
    closingAccumulatedImpairment: number;
    closingNetBookValue: number;
}

export interface FixedAssetGlReconciliationReport {
    asOfDate?: string;
    rows: FixedAssetGlReconciliationRow[];
    diagnostics: FixedAssetReportingDiagnostic[];
    totalGlBalance: number;
    totalSubledgerBalance: number;
    totalVariance: number;
    isReconciled: boolean;
}

export interface FixedAssetGlReconciliationRow {
    area: string;
    accountId?: string;
    accountNumber?: string;
    accountName?: string;
    glBalance: number;
    subledgerBalance: number;
    variance: number;
    sourceDocumentCount: number;
    missingPostingReferenceCount: number;
    glWithoutSourceReferenceCount: number;
    subledgerWithoutGlCount: number;
    presentationWarning?: string;
}

export interface FixedAssetReportingDiagnostic {
    code: string;
    severity: string;
    message: string;
    fixedAssetId?: string;
    accountId?: string;
}

export interface AssetConditionSummary {
    condition: AssetCondition;
    count: number;
    percentage: number;
}

export interface VerificationSummary {
    sessionId: string;
    sessionName: string;
    startDate: string;
    endDate?: string;
    totalItems: number;
    verifiedItems: number;
    missingItems: number;
    conditionSummary: AssetConditionSummary[];
}
