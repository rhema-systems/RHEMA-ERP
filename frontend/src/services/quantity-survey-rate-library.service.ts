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
  RateBuildUp = 12,
}

export enum QuantitySurveyRateComponent {
  Material = 0,
  Labour = 1,
  Plant = 2,
  Equipment = 3,
  Subcontract = 4,
  Overhead = 5,
  Profit = 6,
  Attendance = 7,
  Contingency = 8,
  Wastage = 9,
  Transport = 10,
  Other = 11,
}

export enum QuantitySurveyRateBuildUpCalculationMethod {
  QuantityTimesPublishedRate = 0,
  Percentage = 1,
  FixedAmount = 2,
}

export enum QuantitySurveyRateBuildUpPercentageBasis {
  MaterialSubtotal = 0,
  DirectCost = 1,
  RunningTotal = 2,
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

export enum QuantitySurveyHistoricalRateSourceType {
  CompletedBoqLine = 0,
  CertifiedValuation = 1,
  ProcurementPrice = 2,
  ActualProjectCost = 3,
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
  historicalSourceType?: QuantitySurveyHistoricalRateSourceType | null;
  historicalSourceId?: string | null;
  historicalProjectId?: string | null;
  historicalProjectCode?: string | null;
  historicalSourceLabel?: string | null;
  historicalUnitOfMeasure?: string | null;
  historicalQuantity?: number | null;
  historicalTotalAmount?: number | null;
  historicalSourceHash?: string | null;
  rateBuildUpId?: string | null;
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

export interface QuantitySurveyHistoricalRateSource {
  sourceType: QuantitySurveyHistoricalRateSourceType;
  sourceId: string;
  projectId: string;
  projectCode: string;
  projectTitle: string;
  projectTypeId?: string | null;
  locationId?: string | null;
  businessPartnerId?: string | null;
  sourceReference: string;
  sourceLabel: string;
  sourceDate: string;
  unitOfMeasure: string;
  quantity: number;
  unitRate: number;
  totalAmount: number;
  currencyCode: string;
  integrityHash: string;
  existingRateId?: string | null;
  canPromote: boolean;
}

export interface PrepareQuantitySurveyHistoricalRate {
  sourceType: QuantitySurveyHistoricalRateSourceType;
  sourceId: string;
  sourceIntegrityHash: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  projectTypeId?: string | null;
  locationId?: string | null;
  centralDocumentVersionId?: string | null;
  changeReason: string;
}

export interface QuantitySurveyRateBuildUpSource {
  rateLibraryItemId: string;
  rateId: string;
  itemCode: string;
  itemName: string;
  category: QuantitySurveyRateItemCategory;
  unitOfMeasure: string;
  rateVersion: number;
  unitRate: number;
  currencyId: string;
  currencyCode: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  projectTypeId?: string | null;
  locationId?: string | null;
  businessPartnerId?: string | null;
}

export interface QuantitySurveyRateBuildUpContext {
  allowedComponents: QuantitySurveyRateComponent[];
  requireProjectType: boolean;
  requireLocation: boolean;
  allowBusinessPartner: boolean;
  requireBusinessPartner: boolean;
  allowedProjectTypeIds: string[];
  allowedLocationIds: string[];
  maximumOverheadPercent: number;
  maximumProfitPercent: number;
  maximumContingencyPercent: number;
  maximumWastagePercent: number;
  decimalPlaces: number;
  sources: QuantitySurveyRateBuildUpSource[];
}

export interface QuantitySurveyRateBuildUpLineRequest {
  sequence: number;
  component: QuantitySurveyRateComponent;
  calculationMethod: QuantitySurveyRateBuildUpCalculationMethod;
  percentageBasis?: QuantitySurveyRateBuildUpPercentageBasis | null;
  description?: string;
  sourceRateId?: string | null;
  quantity?: number | null;
  percentage?: number | null;
  fixedAmount?: number | null;
}

export interface PreviewQuantitySurveyRateBuildUp {
  sourceDate: string;
  currencyId: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  projectTypeId?: string | null;
  locationId?: string | null;
  businessPartnerId?: string | null;
  centralDocumentVersionId?: string | null;
  lines: QuantitySurveyRateBuildUpLineRequest[];
}

export interface PrepareQuantitySurveyRateBuildUp extends PreviewQuantitySurveyRateBuildUp {
  clientRequestId: string;
  previewIntegrityHash: string;
  changeReason: string;
}

export interface QuantitySurveyRateBuildUpPreviewLine {
  sequence: number;
  component: QuantitySurveyRateComponent;
  calculationMethod: QuantitySurveyRateBuildUpCalculationMethod;
  percentageBasis?: QuantitySurveyRateBuildUpPercentageBasis | null;
  description: string;
  sourceRateLibraryItemId?: string | null;
  sourceRateId?: string | null;
  sourceItemCode?: string | null;
  sourceItemName?: string | null;
  sourceUnitOfMeasure?: string | null;
  sourceRateVersion?: number | null;
  sourceUnitRate?: number | null;
  quantity?: number | null;
  percentage?: number | null;
  fixedAmount?: number | null;
  basisAmount: number;
  calculatedAmount: number;
}

export interface QuantitySurveyRateBuildUpPreview {
  rateLibraryItemId: string;
  sourceDate: string;
  currencyId: string;
  currencyCode: string;
  configurationProfileId: string;
  configurationDecisionId: string;
  configurationProfileVersion: number;
  decimalPlaces: number;
  materialSubtotal: number;
  directCost: number;
  addOnCost: number;
  unitRate: number;
  integrityHash: string;
  lines: QuantitySurveyRateBuildUpPreviewLine[];
}

export interface QuantitySurveyRateBuildUp {
  id: string;
  rateLibraryItemId: string;
  version: number;
  clientRequestId: string;
  buildUpNumber: string;
  sourceDate: string;
  currencyId: string;
  currencyCode: string;
  configurationProfileVersion: number;
  decimalPlaces: number;
  materialSubtotal: number;
  directCost: number;
  addOnCost: number;
  unitRate: number;
  calculationHash: string;
  centralDocumentRecordId?: string | null;
  centralDocumentVersionId?: string | null;
  changeReason: string;
  preparedById: string;
  preparedAt: string;
  generatedRate: QuantitySurveyRate;
  lines: QuantitySurveyRateBuildUpPreviewLine[];
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
  historicalSources: (
    itemId: string,
    sourceType?: QuantitySurveyHistoricalRateSourceType,
    search?: string
  ) =>
    apiService.get<QuantitySurveyHistoricalRateSource[]>(
      `${root}/${itemId}/historical-sources`,
      {
        sourceType,
        search: search?.trim() || undefined,
      }
    ),
  rateBuildUpContext: (
    itemId: string,
    query: {
      sourceDate: string;
      effectiveAt: string;
      currencyId: string;
      projectTypeId?: string;
      locationId?: string;
      businessPartnerId?: string;
    }
  ) =>
    apiService.get<QuantitySurveyRateBuildUpContext>(
      `${root}/${itemId}/rate-build-up-context`,
      query
    ),
  rateBuildUps: (itemId: string) =>
    apiService.get<QuantitySurveyRateBuildUp[]>(
      `${root}/${itemId}/rate-build-ups`
    ),
  previewRateBuildUp: (
    itemId: string,
    request: PreviewQuantitySurveyRateBuildUp
  ) =>
    apiService.post<QuantitySurveyRateBuildUpPreview>(
      `${root}/${itemId}/rate-build-ups/preview`,
      request
    ),
  prepareRateBuildUp: (
    itemId: string,
    request: PrepareQuantitySurveyRateBuildUp
  ) =>
    apiService.post<QuantitySurveyRateBuildUp>(
      `${root}/${itemId}/rate-build-ups`,
      request
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
  prepareHistoricalRate: (
    itemId: string,
    request: PrepareQuantitySurveyHistoricalRate
  ) =>
    apiService.post<QuantitySurveyRate>(
      `${root}/${itemId}/historical-rate-promotions`,
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
