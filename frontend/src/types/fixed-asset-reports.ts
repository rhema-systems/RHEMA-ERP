import { FixedAssetStatus, DisposalType, AssetCondition } from './fixed-assets';

export interface FixedAssetReportQuery {
    fromDate?: string;
    toDate?: string;
    categoryId?: string;
    status?: FixedAssetStatus;
    searchTerm?: string;
    bookClassification?: string;
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
