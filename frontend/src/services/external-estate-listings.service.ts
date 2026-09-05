import { compatibleApiService as apiService } from './compatibleApiService';
import { apiService as rawApiService } from './api.service';

export interface ExternalEstateListing {
  id: string;
  assetCode: string;
  name: string;
  assetType: string | number;
  status: string | number;
  description?: string | null;
  location?: string | null;
  region?: string | null;
  district?: string | null;
  town?: string | null;
  blockName?: string | null;
  floorLabel?: string | null;
  unitType?: string | null;
  areaSquareMeters?: number | null;
  areaValue?: number | null;
  areaUnit?: string | null;
  externalListingType: string;
  externalListingPrice?: number | null;
  externalSalePrice?: number | null;
  externalMonthlyRent?: number | null;
  externalLeaseTermMonths?: number | null;
  groundRentPayable?: number | null;
  groundRentRatePerAcre?: number | null;
  groundRentComputed?: number | null;
  externalListingCurrency: string;
  externalListingNotes?: string | null;
  externalPublishedAt?: string | null;
  primaryImageDocumentId?: string | null;
  primaryImageUrl?: string | null;
  sourceLabel: string;
}

export interface ExternalListingRequest {
  id: string;
  module: string;
  entityType: string;
  title: string;
  referenceNumber?: string | null;
  status: string;
  currentStageName: string;
  currentAssignedRole?: string | null;
  createdAt: string;
  documents?: ExternalListingRequestDocument[];
}

export interface ExternalListingRequestDocument {
  id: string;
  name: string;
  requiredFrom?: string | null;
  providedBy: string;
  isMandatory: boolean;
  fileName?: string | null;
}

export interface CreateExternalListingRequest {
  requestType?: string;
  businessPartnerId?: string;
  applicantName?: string;
  contact?: string;
  offerAmount?: number;
  message?: string;
}

export interface ExternalCustomerProfile {
  id: string;
  partnerName: string;
  primaryEmail?: string | null;
  primaryPhone?: string | null;
  physicalAddress?: string | null;
  customerAccountNumber: string;
  currency?: string | null;
}

interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

class ExternalEstateListingsService {
  async getCustomerProfiles(): Promise<ExternalCustomerProfile[]> {
    const response = await apiService.get<ApiResponse<ExternalCustomerProfile[]>>(
      '/estate/external/customer-profiles'
    );
    return response.data || [];
  }

  async getListings(query: {
    location?: string;
    listingType?: string;
    search?: string;
    businessPartnerId?: string;
    take?: number;
  }): Promise<ExternalEstateListing[]> {
    const response = await apiService.get<ApiResponse<ExternalEstateListing[]>>(
      '/estate/external/listings',
      query
    );
    return response.data || [];
  }

  async getListingImage(listing: ExternalEstateListing): Promise<Blob | null> {
    if (!listing.primaryImageUrl) return null;
    return rawApiService.downloadBlob(listing.primaryImageUrl);
  }

  async createRequest(
    listingId: string,
    payload: CreateExternalListingRequest
  ): Promise<ExternalListingRequest> {
    const response = await apiService.post<ApiResponse<ExternalListingRequest>>(
      `/estate/external/listings/${listingId}/requests`,
      payload
    );
    return response.data;
  }

  async uploadCustomerIntakeDocument(
    requestId: string,
    documentId: string,
    file: File
  ): Promise<void> {
    const form = new FormData();
    form.append('file', file);
    await rawApiService.request(
      `/estate/external/requests/${requestId}/customer-intake-documents/${documentId}/upload`,
      { method: 'POST', body: form }
    );
  }
}

export const externalEstateListingsService =
  new ExternalEstateListingsService();
