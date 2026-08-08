import axios from 'axios';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';
const API_URL = `${API_BASE_URL}/inventory/analytics`;

export type InventoryActivityClassification = 0 | 1 | 2 | 3;
export type InventoryAgeingBand = {
  key: string; label: string; fromDays: number; toDays?: number;
  quantity: number; value: number; itemLocationCount: number;
};
export type InventoryItemLocationAnalytics = {
  inventoryItemId: string; itemCode: string; itemName: string; categoryName: string; unitOfMeasure: string;
  warehouseId: string; warehouseCode: string; warehouseName: string; locationId?: string;
  locationCode?: string; locationName?: string; quantityOnHand: number; quantityAllocated: number;
  quantityAvailable: number; quantityOnOrder: number; inventoryValue: number; averageUnitCost: number;
  reorderLevel: number; lastMovementDateUtc?: string; lastReceiptDateUtc?: string; lastIssueDateUtc?: string;
  daysSinceActivity: number; activityClassification: InventoryActivityClassification;
  currentStockoutDays?: number; averageDailyDemand: number; estimatedDaysOfCover?: number;
  oldestStockAgeDays: number; oldestAgeingBand: string; expiredQuantity: number; expiredValue: number;
  expiringQuantity: number; expiringValue: number; disposalCandidate: boolean; replenishmentCandidate: boolean;
  replenishmentRecommendationId?: string; replenishmentRecommendationNumber?: string;
  replenishmentRecommendationStatus?: string; replenishmentPurchaseRequisitionId?: string;
  replenishmentPurchaseRequisitionNumber?: string; recommendedActionCode: string; recommendedAction: string;
  ageingBands: InventoryAgeingBand[];
};
export type InventoryAnalytics = {
  asOfUtc: string; slowMovingDays: number; nonMovingDays: number; expiryWarningDays: number;
  summary: {
    itemLocationCount: number; quantityOnHand: number; inventoryValue: number;
    slowMovingCount: number; slowMovingValue: number; nonMovingCount: number; nonMovingValue: number;
    stockoutCount: number; disposalCandidateCount: number; replenishmentCandidateCount: number;
    expiredQuantity: number; expiredValue: number; expiringQuantity: number; expiringValue: number;
  };
  ageingBands: InventoryAgeingBand[];
  items: InventoryItemLocationAnalytics[];
};

const headers = () => ({
  Authorization: `Bearer ${localStorage.getItem('authToken') || localStorage.getItem('token') || ''}`,
});

export const inventoryAnalyticsService = {
  async get(filters: { warehouseId?: string; categoryId?: string; slowMovingDays: number;
    nonMovingDays: number; expiryWarningDays: number; take?: number }) {
    return (await axios.get<InventoryAnalytics>(API_URL, { params: filters, headers: headers() })).data;
  },
};
