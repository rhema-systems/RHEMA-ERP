import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';

export enum EstateManagedAssetType {
  Land = 0,
  Property = 1,
  Facility = 2,
}

export enum EstateManagedAssetStatus {
  LandBank = 0,
  UnderDevelopment = 1,
  Available = 2,
  Reserved = 3,
  Leased = 4,
  Occupied = 5,
  Sold = 6,
  Retired = 7,
}

export enum EstateManagedAssetSourceType {
  Manual = 0,
  LandAcquisition = 1,
  ProjectUnit = 2,
}

export interface EstateManagedAsset {
  id: string;
  assetCode: string;
  name: string;
  description?: string;
  location?: string;
  purpose?: string;
  zoningClassification?: string;
  planningComplianceStatus?: string;
  gisLayerReference?: string;
  gisProvider: string;
  gisFeatureId?: string;
  gisSourceCrs?: string;
  gisSyncStatus: string;
  gisLastSyncedAt?: string;
  boundaryVerified: boolean;
  boundaryCoordinates?: string;
  surveyPlanNumber?: string;
  mapSheetNumber?: string;
  cadastreDescription?: string;
  region?: string;
  district?: string;
  town?: string;
  areaValue?: number;
  areaUnit?: string;
  surveyorName?: string;
  surveyDate?: string;
  beaconCount?: number;
  ownershipHistory: ExistingLandOwner[];
  isReadyForProjectManagement: boolean;
  assetType: EstateManagedAssetType;
  status: EstateManagedAssetStatus;
  sourceType: EstateManagedAssetSourceType;
  landAcquisitionId?: string;
  projectCode?: string;
  projectTitle?: string;
  projectUnitCode?: string;
  areaSquareMeters?: number;
  valuationAmount?: number;
  currency: string;
  isAvailableForLease: boolean;
  isAvailableForSale: boolean;
  isPublishedToExternalPortal: boolean;
  externalListingType: string;
  externalListingStatus: string;
  externalListingPrice?: number;
  externalListingCurrency: string;
  externalListingNotes?: string;
  externalPublishedAt?: string;
  primaryListingImageDocumentId?: string | null;
  notes?: string;
}

export interface ExistingLandOwner {
  ownerName: string;
  ownershipType: string;
  interestHeld: string;
  identificationType: string;
  identificationNumber: string;
  contactNumber: string;
  address: string;
  ownershipStartDate: string;
  ownershipEndDate?: string;
  ownershipPercentage: number;
  isCurrentOwner: boolean;
}

export interface CreateManualExistingLand {
  assetCode?: string;
  name: string;
  description: string;
  location: string;
  purpose: string;
  zoningClassification: string;
  planningComplianceStatus: string;
  gisLayerReference?: string;
  cadastreDescription: string;
  region: string;
  district: string;
  town: string;
  areaValue: number;
  areaUnit: string;
  areaSquareMeters: number;
  surveyorName: string;
  surveyDate: string;
  surveyPlanNumber: string;
  mapSheetNumber: string;
  beaconCount: number;
  boundaryCoordinates: string;
  boundaryVerified: boolean;
  valuationAmount: number;
  currency: string;
  notes: string;
  isReadyForProjectManagement: boolean;
  ownershipHistory: ExistingLandOwner[];
}

export interface UpdateEstateManagedLandDemarcation {
  cadastreDescription: string;
  region: string;
  district: string;
  town: string;
  areaValue: number;
  areaUnit: string;
  areaSquareMeters?: number;
  surveyorName: string;
  surveyDate?: string;
  surveyPlanNumber: string;
  mapSheetNumber: string;
  beaconCount: number;
  boundaryCoordinates: string;
  boundaryVerified: boolean;
  isReadyForProjectManagement: boolean;
}

export interface EstateManagedAssetDocument {
  id: string;
  estateManagedAssetId: string;
  fileName: string;
  documentType: string;
  documentName?: string;
  contentType?: string;
  fileSize: number;
  uploadedAt: string;
  uploadedBy?: string;
  isListingImage: boolean;
  isPrimaryListingImage: boolean;
  centralDocumentRecordId?: string | null;
  centralDocumentReference?: string | null;
  publishedToCentralDmsAt?: string | null;
}

export interface EstateCentralDmsPublication {
  id: string;
  documentReference: string;
  title: string;
  sourceModule: string;
  sourceLabel: string;
  sourceEntityType?: string | null;
  sourceRecordReference?: string | null;
  sourceRecordId?: string | null;
  metadataTemplateCode?: string | null;
  repositoryStatus: string;
  repositoryPath?: string | null;
  currentVersion?: string | null;
  versionStatus: string;
  annotationStatus: string;
  commentStatus: string;
  accessProfile: string;
  retentionStatus: string;
  lifecycleStatus: string;
  publishedToCentralDmsAt?: string | null;
}

interface ApiListResponse<T> {
  success?: boolean;
  data?: T[];
  message?: string;
}

export interface UpdateEstateManagedAssetListing {
  isPublishedToExternalPortal: boolean;
  externalListingType: string;
  externalListingStatus: string;
  externalListingPrice?: number | null;
  externalListingCurrency: string;
  externalListingNotes?: string | null;
}

export interface EstateManagedAssetQuery {
  assetType?: EstateManagedAssetType;
  status?: EstateManagedAssetStatus;
  search?: string;
  availableForLease?: boolean;
  availableForSale?: boolean;
  take?: number;
}

const assetTypeNames: Record<EstateManagedAssetType, string> = {
  [EstateManagedAssetType.Land]: 'Land',
  [EstateManagedAssetType.Property]: 'Property',
  [EstateManagedAssetType.Facility]: 'Facility',
};

const assetStatusNames: Record<EstateManagedAssetStatus, string> = {
  [EstateManagedAssetStatus.LandBank]: 'LandBank',
  [EstateManagedAssetStatus.UnderDevelopment]: 'UnderDevelopment',
  [EstateManagedAssetStatus.Available]: 'Available',
  [EstateManagedAssetStatus.Reserved]: 'Reserved',
  [EstateManagedAssetStatus.Leased]: 'Leased',
  [EstateManagedAssetStatus.Occupied]: 'Occupied',
  [EstateManagedAssetStatus.Sold]: 'Sold',
  [EstateManagedAssetStatus.Retired]: 'Retired',
};

const enumMatches = <T extends number>(
  actual: T | string | null | undefined,
  expected: T | undefined,
  names: Record<T, string>
) => {
  if (expected === undefined) return true;
  if (actual === expected) return true;
  return String(actual).toLowerCase() === names[expected].toLowerCase();
};

const enumValue = <T extends number>(
  actual: T | string | null | undefined,
  names: Record<T, string>,
  fallback: T
): T => {
  if (typeof actual === 'number' && actual in names) return actual;
  const normalized = String(actual ?? '').toLowerCase();
  const match = Object.entries(names).find(
    ([, name]) => String(name).toLowerCase() === normalized
  );
  return match ? (Number(match[0]) as T) : fallback;
};

// The API serializes enums as names; normalize them once so every Estate screen
// can safely use the numeric TypeScript enums for counts, labels, and actions.
const normalizeManagedAsset = (asset: EstateManagedAsset): EstateManagedAsset => ({
  ...asset,
  assetType: enumValue(
    asset.assetType,
    assetTypeNames,
    EstateManagedAssetType.Land
  ),
  status: enumValue(
    asset.status,
    assetStatusNames,
    EstateManagedAssetStatus.LandBank
  ),
  sourceType: enumValue(
    asset.sourceType,
    {
      [EstateManagedAssetSourceType.Manual]: 'Manual',
      [EstateManagedAssetSourceType.LandAcquisition]: 'LandAcquisition',
      [EstateManagedAssetSourceType.ProjectUnit]: 'ProjectUnit',
    },
    EstateManagedAssetSourceType.Manual
  ),
});

const assetMatchesQuery = (
  asset: EstateManagedAsset,
  query: EstateManagedAssetQuery
) =>
  enumMatches(asset.assetType, query.assetType, assetTypeNames) &&
  enumMatches(asset.status, query.status, assetStatusNames) &&
  (query.availableForLease === undefined ||
    asset.isAvailableForLease === query.availableForLease) &&
  (query.availableForSale === undefined ||
    asset.isAvailableForSale === query.availableForSale);

const buildManagedAssetQueryParams = (query: EstateManagedAssetQuery) => ({
  search: query.search || undefined,
  assetType:
    query.assetType === undefined ? undefined : assetTypeNames[query.assetType],
  status: query.status === undefined ? undefined : assetStatusNames[query.status],
  availableForLease: query.availableForLease,
  availableForSale: query.availableForSale,
  take: query.take || 250,
});

export class EstateLandManagementService {
  async getLandBank(search?: string): Promise<EstateManagedAsset[]> {
    const query: EstateManagedAssetQuery = {
      search,
      assetType: EstateManagedAssetType.Land,
      status: EstateManagedAssetStatus.LandBank,
      take: 250,
    };
    const response = await apiService.get<ApiListResponse<EstateManagedAsset>>(
      '/estate/managed-assets',
      buildManagedAssetQueryParams(query)
    );

    return Array.isArray(response.data)
      ? response.data
          .map(normalizeManagedAsset)
          .filter((asset) => assetMatchesQuery(asset, query))
      : [];
  }

  async getManagedAssets(
    query: EstateManagedAssetQuery = {}
  ): Promise<EstateManagedAsset[]> {
    const response = await apiService.get<ApiListResponse<EstateManagedAsset>>(
      '/estate/managed-assets',
      buildManagedAssetQueryParams(query)
    );

    return Array.isArray(response.data)
      ? response.data
          .map(normalizeManagedAsset)
          .filter((asset) => assetMatchesQuery(asset, query))
      : [];
  }

  async updateExternalListing(
    assetId: string,
    payload: UpdateEstateManagedAssetListing
  ): Promise<EstateManagedAsset> {
    const response = await apiService.patch<{
      success?: boolean;
      data?: EstateManagedAsset;
      message?: string;
    }>(`/estate/managed-assets/${assetId}/external-listing`, payload);
    if (!response.data) {
      throw new Error(response.message || 'Unable to update portal listing.');
    }
    return normalizeManagedAsset(response.data);
  }

  async createManualLand(
    payload: CreateManualExistingLand
  ): Promise<EstateManagedAsset> {
    const response = await apiService.post<{
      success?: boolean;
      data?: EstateManagedAsset;
      message?: string;
    }>('/estate/managed-assets/manual-land', payload);
    if (!response.data)
      throw new Error(
        response.message || 'Unable to create existing land asset.'
      );
    return normalizeManagedAsset(response.data);
  }

  async markReadyForProjectManagement(
    assetId: string
  ): Promise<EstateManagedAsset> {
    const response = await apiService.post<{
      success?: boolean;
      data?: EstateManagedAsset;
      message?: string;
    }>(`/estate/managed-assets/${assetId}/ready-for-project-management`, {});
    if (!response.data)
      throw new Error(
        response.message || 'Unable to make land ready for project management.'
      );
    return normalizeManagedAsset(response.data);
  }

  async updateLandDemarcation(
    assetId: string,
    payload: UpdateEstateManagedLandDemarcation
  ): Promise<EstateManagedAsset> {
    const response = await apiService.patch<{
      success?: boolean;
      data?: EstateManagedAsset;
      message?: string;
    }>(`/estate/managed-assets/${assetId}/demarcation`, payload);
    if (!response.data)
      throw new Error(response.message || 'Unable to update land demarcation.');
    return normalizeManagedAsset(response.data);
  }

  async getDocuments(assetId: string): Promise<EstateManagedAssetDocument[]> {
    const response = await apiService.get<{
      success?: boolean;
      data?: EstateManagedAssetDocument[];
    }>(`/estate/managed-assets/${assetId}/documents`);
    return response.data || [];
  }

  async uploadDocument(
    assetId: string,
    file: File,
    documentType: string,
    documentName?: string,
    options?: { isListingImage?: boolean; isPrimaryListingImage?: boolean }
  ): Promise<EstateManagedAssetDocument> {
    const form = new FormData();
    form.append('file', file);
    form.append('documentType', documentType);
    if (documentName) form.append('documentName', documentName);
    if (options?.isListingImage) form.append('isListingImage', 'true');
    if (options?.isPrimaryListingImage)
      form.append('isPrimaryListingImage', 'true');
    const response = await rawApiService.request<{
      success?: boolean;
      data?: EstateManagedAssetDocument;
      message?: string;
    }>(`/estate/managed-assets/${assetId}/documents`, {
      method: 'POST',
      body: form,
    });
    if (!response.data)
      throw new Error(response.message || 'Unable to upload land document.');
    return response.data;
  }

  async setPrimaryListingImage(
    assetId: string,
    documentId: string
  ): Promise<EstateManagedAssetDocument> {
    const response = await apiService.post<{
      success?: boolean;
      data?: EstateManagedAssetDocument;
      message?: string;
    }>(
      `/estate/managed-assets/${assetId}/documents/${documentId}/primary-listing-image`,
      {}
    );
    if (!response.data) {
      throw new Error(
        response.message || 'Unable to set the primary listing image.'
      );
    }
    return response.data;
  }

  async downloadDocument(assetId: string, documentId: string): Promise<Blob> {
    return rawApiService.downloadBlob(
      `/estate/managed-assets/${assetId}/documents/${documentId}/download`
    );
  }

  async publishDocumentToCentralDms(
    assetId: string,
    documentId: string
  ): Promise<EstateCentralDmsPublication> {
    const response = await apiService.post<{
      success?: boolean;
      data?: EstateCentralDmsPublication;
      message?: string;
    }>(
      `/estate/managed-assets/${assetId}/documents/${documentId}/publish-to-dms`,
      {}
    );
    if (!response.data)
      throw new Error(
        response.message || 'Unable to publish document to Central DMS.'
      );
    return response.data;
  }
}

export const estateLandManagementService = new EstateLandManagementService();
