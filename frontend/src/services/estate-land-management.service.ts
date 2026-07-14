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
  areaSquareMeters?: number;
  valuationAmount?: number;
  currency: string;
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
  assetCode: string;
  name: string;
  description: string;
  location: string;
  purpose: string;
  zoningClassification: string;
  planningComplianceStatus: string;
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
}

interface ApiListResponse<T> {
  success?: boolean;
  data?: T[];
  message?: string;
}

export class EstateLandManagementService {
  async getLandBank(search?: string): Promise<EstateManagedAsset[]> {
    const response = await apiService.get<ApiListResponse<EstateManagedAsset>>('/estate/managed-assets', {
      assetType: EstateManagedAssetType.Land,
      status: EstateManagedAssetStatus.LandBank,
      search: search || undefined,
      take: 250,
    });

    return Array.isArray(response.data) ? response.data : [];
  }

  async createManualLand(payload: CreateManualExistingLand): Promise<EstateManagedAsset> {
    const response = await apiService.post<{ success?: boolean; data?: EstateManagedAsset; message?: string }>(
      '/estate/managed-assets/manual-land', payload
    );
    if (!response.data) throw new Error(response.message || 'Unable to create existing land asset.');
    return response.data;
  }

  async markReadyForProjectManagement(assetId: string): Promise<EstateManagedAsset> {
    const response = await apiService.post<{ success?: boolean; data?: EstateManagedAsset; message?: string }>(
      `/estate/managed-assets/${assetId}/ready-for-project-management`, {}
    );
    if (!response.data) throw new Error(response.message || 'Unable to make land ready for project management.');
    return response.data;
  }

  async getDocuments(assetId: string): Promise<EstateManagedAssetDocument[]> {
    const response = await apiService.get<{ success?: boolean; data?: EstateManagedAssetDocument[] }>(
      `/estate/managed-assets/${assetId}/documents`
    );
    return response.data || [];
  }

  async uploadDocument(assetId: string, file: File, documentType: string, documentName?: string): Promise<EstateManagedAssetDocument> {
    const form = new FormData();
    form.append('file', file);
    form.append('documentType', documentType);
    if (documentName) form.append('documentName', documentName);
    const response = await rawApiService.request<{ success?: boolean; data?: EstateManagedAssetDocument; message?: string }>(
      `/estate/managed-assets/${assetId}/documents`, { method: 'POST', body: form }
    );
    if (!response.data) throw new Error(response.message || 'Unable to upload land document.');
    return response.data;
  }

  async downloadDocument(assetId: string, documentId: string): Promise<Blob> {
    return rawApiService.downloadBlob(`/estate/managed-assets/${assetId}/documents/${documentId}/download`);
  }
}

export const estateLandManagementService = new EstateLandManagementService();
