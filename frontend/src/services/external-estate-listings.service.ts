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
  externalGroundRentRequired?: boolean | null;
  externalPremiumChargeRequired?: boolean | null;
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

export interface ExternalListingEnquiry {
  id: string;
  ticketNumber: string;
  subject?: string | null;
  status: string;
}

export interface EnquiryPartnerProfile {
  id: string;
  partnerName: string;
  primaryEmail?: string | null;
  primaryPhone?: string | null;
  partnerType: string;
}

export interface CreatePropertyListingEnquiry {
  submissionId: string;
  message: string;
  businessPartnerId?: string;
  captchaToken?: string;
}

/**
 * Public contract deliberately excludes every internal assignment, workflow,
 * business-partner and opportunity field. The server derives the listing,
 * source, status and ownership from the route and authenticated tenant setup.
 */
export interface CreatePublicPropertyListingEnquiry {
  submissionId: string;
  contactName: string;
  contactPhone?: string;
  contactEmail?: string;
  preferredContactMethod: 'Email' | 'Phone';
  contactVerificationToken: string;
  message: string;
  captchaToken?: string;
}

export type PublicEnquiryContactChannel = 'Email' | 'Phone';

export interface PublicEnquiryContactChallenge {
  channel: PublicEnquiryContactChannel;
  maskedContact: string;
  expiresInSeconds: number;
}

export interface PublicEnquiryContactProfile {
  contactName: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  requiresPortalLogin: boolean;
  externalPortalPath?: string | null;
}

export interface PublicEnquiryContactVerification {
  verificationToken: string;
  expiresAtUtc: string;
  profile: PublicEnquiryContactProfile;
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
  pagination?: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
  };
}

function appendQueryParams(endpoint: string, query?: Record<string, unknown>) {
  if (!query) return endpoint;

  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue;
    params.append(key, String(value));
  }

  const queryString = params.toString();
  return queryString ? `${endpoint}?${queryString}` : endpoint;
}

export interface PublicEstateListingsPage {
  items: ExternalEstateListing[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

class ExternalEstateListingsService {
  async getEnquiryProfiles(): Promise<EnquiryPartnerProfile[]> {
    const response = await apiService.get<ApiResponse<EnquiryPartnerProfile[]>>(
      '/estate/external/enquiry-profiles'
    );
    return response.data || [];
  }

  async getCustomerProfiles(): Promise<ExternalCustomerProfile[]> {
    const response = await apiService.get<
      ApiResponse<ExternalCustomerProfile[]>
    >('/estate/external/customer-profiles');
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

  async getListingsPage(query: {
    listingId?: string;
    location?: string;
    listingType?: string;
    search?: string;
    businessPartnerId?: string;
    minPrice?: number;
    maxPrice?: number;
    page?: number;
    pageSize?: number;
  }): Promise<PublicEstateListingsPage> {
    const response = await apiService.get<ApiResponse<ExternalEstateListing[]>>(
      '/estate/external/listings',
      query
    );
    return {
      items: response.data || [],
      page: response.pagination?.page ?? query.page ?? 1,
      pageSize: response.pagination?.pageSize ?? query.pageSize ?? 10,
      totalCount: response.pagination?.totalCount ?? response.data?.length ?? 0,
      totalPages: response.pagination?.totalPages ?? 1,
      hasPreviousPage: response.pagination?.hasPreviousPage ?? false,
      hasNextPage: response.pagination?.hasNextPage ?? false,
    };
  }

  async getPublicListings(query: {
    location?: string;
    listingType?: string;
    search?: string;
    take?: number;
  }): Promise<ExternalEstateListing[]> {
    const response = await rawApiService.publicRequest<
      ApiResponse<ExternalEstateListing[]>
    >(appendQueryParams('/estate/public/listings', query), { method: 'GET' });
    return response.data || [];
  }

  async getPublicListingsPage(query: {
    location?: string;
    listingType?: string;
    search?: string;
    minPrice?: number;
    maxPrice?: number;
    page?: number;
    pageSize?: number;
  }): Promise<PublicEstateListingsPage> {
    const response = await rawApiService.publicRequest<
      ApiResponse<ExternalEstateListing[]>
    >(appendQueryParams('/estate/public/listings', query), { method: 'GET' });
    return {
      items: response.data || [],
      page: response.pagination?.page ?? query.page ?? 1,
      pageSize: response.pagination?.pageSize ?? query.pageSize ?? 10,
      totalCount: response.pagination?.totalCount ?? response.data?.length ?? 0,
      totalPages: response.pagination?.totalPages ?? 1,
      hasPreviousPage: response.pagination?.hasPreviousPage ?? false,
      hasNextPage: response.pagination?.hasNextPage ?? false,
    };
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

  async createEnquiry(
    listingId: string,
    payload: CreatePropertyListingEnquiry
  ): Promise<ExternalListingEnquiry> {
    const response = await apiService.post<ApiResponse<ExternalListingEnquiry>>(
      `/estate/external/listings/${listingId}/enquiries`,
      payload
    );
    return response.data;
  }

  async createPublicEnquiry(
    listingId: string,
    payload: CreatePublicPropertyListingEnquiry
  ): Promise<ExternalListingEnquiry> {
    const response = await rawApiService.publicRequest<
      ApiResponse<ExternalListingEnquiry>
    >(`/estate/public/listings/${listingId}/enquiries`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return response.data;
  }

  async requestPublicEnquiryContactChallenge(payload: {
    listingId: string;
    channel: PublicEnquiryContactChannel;
    contact: string;
    captchaToken?: string;
  }): Promise<PublicEnquiryContactChallenge> {
    const response = await rawApiService.publicRequest<
      ApiResponse<PublicEnquiryContactChallenge>
    >('/estate/public/property-enquiry-contacts/challenges', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return response.data;
  }

  async verifyPublicEnquiryContact(payload: {
    listingId: string;
    channel: PublicEnquiryContactChannel;
    contact: string;
    otpCode: string;
  }): Promise<PublicEnquiryContactVerification> {
    const response = await rawApiService.publicRequest<
      ApiResponse<PublicEnquiryContactVerification>
    >('/estate/public/property-enquiry-contacts/verifications', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
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
