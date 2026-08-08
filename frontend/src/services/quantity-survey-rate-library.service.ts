import { apiService } from '@/services/api.service';

const root = '/quantity-survey/rate-library';

export enum QuantitySurveyRateItemCategory {
  StandardItem = 0,
  Material = 1,
  Labour = 2,
  Plant = 3,
  Equipment = 4,
  Subcontract = 5,
}

export enum QuantitySurveyRateSourceType {
  Baseline = 0,
  InventoryStandardCost = 1,
  InventoryAverageCost = 2,
  InventoryLastPurchaseCost = 3,
  PurchaseOrder = 4,
  SupplierQuotation = 5,
  ContractorQuotation = 6,
  FrameworkAgreement = 7,
  HistoricalProject = 8,
  LabourSchedule = 9,
  PlantHire = 10,
  MarketSurvey = 11,
}

export enum QuantitySurveyRateLifecycleStatus {
  Draft = 0,
  Published = 1,
  Retired = 2,
}

export enum QuantitySurveyMarketSurveyPriceBasis {
  CurrentMarketPrice = 0,
  AverageSurveyPrice = 1,
  LowestSurveyPrice = 2,
  HighestSurveyPrice = 3,
  ForecastedPrice = 4,
}

export interface QuantitySurveyLookupOption {
  value: string;
  label: string;
  group?: string | null;
}

export interface QuantitySurveyRateLibraryLookups {
  sources: Record<string, QuantitySurveyLookupOption[]>;
}

export interface QuantitySurveyRate {
  id: string;
  rateLibraryItemId: string;
  version: number;
  unitRate: number;
  currencyId: string;
  currencyCode: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  projectTypeId?: string | null;
  projectTypeLabel?: string | null;
  locationId?: string | null;
  locationLabel?: string | null;
  businessPartnerId?: string | null;
  businessPartnerLabel?: string | null;
  sourceType: QuantitySurveyRateSourceType;
  sourceReference?: string | null;
  sourceDate: string;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
  evidenceLabel?: string | null;
  marketAnalysisId?: string | null;
  marketAnalysisCode?: string | null;
  previousRateId?: string | null;
  previousUnitRate?: number | null;
  previousCurrencyCode?: string | null;
  varianceAmount?: number | null;
  variancePercent?: number | null;
  marketSurveyQuoteCount?: number | null;
  nextReviewDueAt?: string | null;
  lifecycleStatus: QuantitySurveyRateLifecycleStatus;
  changeReason?: string | null;
  preparedById: string;
  preparedAt: string;
  publishedById?: string | null;
  publishedAt?: string | null;
  retiredById?: string | null;
  retiredAt?: string | null;
  rowVersion: string;
}

export interface QuantitySurveyRateLibraryItem {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  category: QuantitySurveyRateItemCategory;
  unitOfMeasureId: string;
  unitOfMeasureCode: string;
  unitOfMeasureName: string;
  projectCatalogEntryId?: string | null;
  projectCatalogEntryLabel?: string | null;
  inventoryItemId?: string | null;
  inventoryItemLabel?: string | null;
  isActive: boolean;
  rateCount: number;
  currentRate?: QuantitySurveyRate | null;
  rates: QuantitySurveyRate[];
  rowVersion: string;
}

export interface QuantitySurveyRateLibraryPage {
  items: QuantitySurveyRateLibraryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface QuantitySurveyRateLibraryQuery {
  search?: string;
  category?: QuantitySurveyRateItemCategory;
  projectTypeId?: string;
  locationId?: string;
  businessPartnerId?: string;
  effectiveAt?: string;
  includeInactive?: boolean;
  page?: number;
  pageSize?: number;
}

export interface SaveQuantitySurveyRateLibraryItem {
  code: string;
  name: string;
  description?: string;
  category: QuantitySurveyRateItemCategory;
  unitOfMeasureId: string;
  projectCatalogEntryId?: string | null;
  inventoryItemId?: string | null;
  isActive?: boolean;
  rowVersion?: string;
  reason?: string;
}

export interface SaveQuantitySurveyRate {
  unitRate: number;
  currencyId: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  projectTypeId?: string | null;
  locationId?: string | null;
  businessPartnerId?: string | null;
  sourceType: QuantitySurveyRateSourceType;
  sourceReference?: string | null;
  sourceDate: string;
  centralDocumentVersionId?: string | null;
  changeReason: string;
  rowVersion?: string;
}

export interface QuantitySurveyRateLibraryRevision {
  id: string;
  rateLibraryItemId: string;
  rateId?: string | null;
  action: string;
  actorUserId: string;
  actorName: string;
  actorRoles?: string | null;
  correlationId: string;
  reason?: string | null;
  beforeJson?: string | null;
  afterJson?: string | null;
  createdAt: string;
}

export interface QuantitySurveyMarketSurveySource {
  id: string;
  analysisCode: string;
  title: string;
  itemCategory?: string | null;
  itemDescription?: string | null;
  analysisPeriodStart: string;
  analysisPeriodEnd: string;
  currencyCode: string;
  currentMarketPrice: number;
  averageSurveyPrice: number;
  lowestSurveyPrice: number;
  highestSurveyPrice: number;
  forecastedPrice: number;
  quoteCount: number;
  latestQuoteDate?: string | null;
  nextReviewDueAt: string;
  isOverdue: boolean;
}

export interface PrepareQuantitySurveyMarketSurveyUpdate {
  marketAnalysisId: string;
  priceBasis: QuantitySurveyMarketSurveyPriceBasis;
  effectiveFrom: string;
  effectiveTo?: string | null;
  projectTypeId?: string | null;
  locationId?: string | null;
  centralDocumentVersionId: string;
  changeReason: string;
}

export const quantitySurveyRateLibraryService = {
  lookups: () =>
    apiService.get<QuantitySurveyRateLibraryLookups>(`${root}/lookups`),
  list: (query?: QuantitySurveyRateLibraryQuery) =>
    apiService.get<QuantitySurveyRateLibraryPage>(
      root,
      query ? { ...query } : undefined
    ),
  get: (id: string) =>
    apiService.get<QuantitySurveyRateLibraryItem>(`${root}/${id}`),
  marketSurveySources: () =>
    apiService.get<QuantitySurveyMarketSurveySource[]>(
      `${root}/market-survey-sources`
    ),
  createItem: (request: SaveQuantitySurveyRateLibraryItem) =>
    apiService.post<QuantitySurveyRateLibraryItem>(root, request),
  updateItem: (id: string, request: SaveQuantitySurveyRateLibraryItem) =>
    apiService.put<QuantitySurveyRateLibraryItem>(`${root}/${id}`, request),
  createRate: (itemId: string, request: SaveQuantitySurveyRate) =>
    apiService.post<QuantitySurveyRate>(`${root}/${itemId}/rates`, request),
  prepareMarketSurveyUpdate: (
    itemId: string,
    request: PrepareQuantitySurveyMarketSurveyUpdate
  ) =>
    apiService.post<QuantitySurveyRate>(
      `${root}/${itemId}/market-survey-updates`,
      request
    ),
  updateRate: (
    itemId: string,
    rateId: string,
    request: SaveQuantitySurveyRate
  ) =>
    apiService.put<QuantitySurveyRate>(
      `${root}/${itemId}/rates/${rateId}`,
      request
    ),
  publishRate: (
    itemId: string,
    rateId: string,
    rowVersion: string,
    reason: string
  ) =>
    apiService.post<QuantitySurveyRate>(
      `${root}/${itemId}/rates/${rateId}/publish`,
      {
        rowVersion,
        reason,
      }
    ),
  retireRate: (
    itemId: string,
    rateId: string,
    rowVersion: string,
    reason: string
  ) =>
    apiService.post<QuantitySurveyRate>(
      `${root}/${itemId}/rates/${rateId}/retire`,
      {
        rowVersion,
        reason,
      }
    ),
  history: (itemId: string) =>
    apiService.get<QuantitySurveyRateLibraryRevision[]>(
      `${root}/${itemId}/history`
    ),
};
