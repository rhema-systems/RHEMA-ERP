import { apiService } from './api.service';
import type { EhcTicketDetail } from './ehcTicketService';

export type ProspectActivityType = 'Contact' | 'Note' | 'FollowUp';

export interface PropertyEnquiryQueueItem {
  id: string;
  ticketNumber: string;
  subject: string;
  status: string;
  createdAt: string;
  requesterName: string;
}

export interface PropertyEnquiryProspect {
  ticketId: string;
  leadId: string;
  opportunityId?: string | null;
  salesAllocationId?: string | null;
  businessPartnerId?: string | null;
  businessPartnerCode?: string | null;
  businessPartnerName?: string | null;
  status: string;
  agreedAmount: number;
  currency: string;
  depositRequirementType: string;
  requiredDeposit: number;
  clearedDeposit: number;
  depositThresholdMet: boolean;
  qualifiedAt?: string | null;
  businessPartnerLinkedAt?: string | null;
}

export function canSearchOrLinkExistingCustomer(
  prospect?: PropertyEnquiryProspect | null
): boolean {
  return Boolean(
    prospect &&
      !prospect.businessPartnerId &&
      (prospect.status === 'Qualified' || prospect.status === 'Opportunity')
  );
}

export function canSubmitPropertyEstateHandoff(
  prospect: PropertyEnquiryProspect | null | undefined,
  handoffReady: boolean
): boolean {
  return Boolean(handoffReady && prospect?.businessPartnerId);
}

export interface BusinessPartnerMatch {
  id: string;
  partnerCode: string;
  partnerName: string;
  email?: string | null;
  phone?: string | null;
  approvalStatus?: string | null;
  isActive: boolean;
  matchedOn: string[];
}

export interface PropertyEnquiryDetail extends EhcTicketDetail {
  prospect?: PropertyEnquiryProspect | null;
}

export interface CreateProspectBusinessPartnerRequest {
  partnerName?: string | null;
  email?: string | null;
  phone?: string | null;
  physicalAddress?: string | null;
  city?: string | null;
  country?: string | null;
  postalCode?: string | null;
}

export interface CreateProspectActivityRequest {
  activityType: ProspectActivityType;
  notes: string;
  followUpAt?: string | null;
}

export interface QualifyProspectRequest {
  qualificationScore: number;
  agreedAmount: number;
  currency: string;
  notes?: string | null;
}

export interface CreateProspectOpportunityRequest {
  amount: number;
  currency: string;
  expectedCloseDate: string;
  reserveProperty: boolean;
  reservationDays: number;
  notes?: string | null;
}

export interface ProspectDepositReceipt {
  id: string;
  receiptNumber: string;
  amount: number;
  currency: string;
  paymentMethod: string;
  transactionReference?: string | null;
  status: string;
  receivedAt: string;
  clearedAt?: string | null;
  reversedAt?: string | null;
}

export interface RecordProspectDepositRequest {
  amount: number;
  currency: string;
  paymentMethod: string;
  transactionReference?: string | null;
  receivedAt?: string | null;
}

export type ProspectDepositRequirementType = 'Fixed' | 'Percentage' | 'Full';

export interface PropertyProspectDepositPolicy {
  id?: string;
  salesSaleableSourceId: string;
  requirementType: ProspectDepositRequirementType;
  fixedAmount?: number | null;
  percentage?: number | null;
  depositLiabilityAccountId: string;
  defaultBankAccountId?: string | null;
  defaultLiquidityAccountId?: string | null;
  isActive: boolean;
}

export type UpsertPropertyProspectDepositPolicyRequest =
  PropertyProspectDepositPolicy;

type Envelope<T> = {
  success: boolean;
  data: T;
  message?: string;
  totalCount?: number;
};

const baseUrl = '/ehc/internal/property-enquiries';

export const propertyEnquiryService = {
  async list(page = 1): Promise<Envelope<PropertyEnquiryQueueItem[]>> {
    return apiService.request<Envelope<PropertyEnquiryQueueItem[]>>(
      `${baseUrl}?page=${page}`,
      { method: 'GET' }
    );
  },

  async get(id: string): Promise<PropertyEnquiryDetail> {
    const [ticketResponse, prospectResponse] = await Promise.all([
      apiService.request<Envelope<PropertyEnquiryDetail>>(`${baseUrl}/${id}`, {
        method: 'GET',
      }),
      apiService.request<Envelope<PropertyEnquiryProspect | null>>(
        `${baseUrl}/${id}/prospect`,
        { method: 'GET' }
      ),
    ]);
    return { ...ticketResponse.data, prospect: prospectResponse.data };
  },

  async qualify(
    id: string,
    request: QualifyProspectRequest
  ): Promise<PropertyEnquiryProspect> {
    const response = await apiService.request<
      Envelope<PropertyEnquiryProspect>
    >(`${baseUrl}/${id}/prospect/qualify`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return response.data;
  },

  async markContacted(
    id: string,
    notes?: string
  ): Promise<PropertyEnquiryProspect> {
    const response = await apiService.request<
      Envelope<PropertyEnquiryProspect>
    >(`${baseUrl}/${id}/prospect/contacted`, {
      method: 'POST',
      body: JSON.stringify({ notes: notes?.trim() || null }),
    });
    return response.data;
  },

  async disqualify(
    id: string,
    reason: string
  ): Promise<PropertyEnquiryProspect> {
    const response = await apiService.request<
      Envelope<PropertyEnquiryProspect>
    >(`${baseUrl}/${id}/prospect/disqualify`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    });
    return response.data;
  },

  async getBusinessPartnerMatches(id: string): Promise<BusinessPartnerMatch[]> {
    const response = await apiService.request<Envelope<BusinessPartnerMatch[]>>(
      `${baseUrl}/${id}/prospect/business-partner-matches`,
      { method: 'GET' }
    );
    return response.data ?? [];
  },

  async linkBusinessPartner(
    id: string,
    businessPartnerId: string
  ): Promise<PropertyEnquiryProspect> {
    const response = await apiService.request<
      Envelope<PropertyEnquiryProspect>
    >(`${baseUrl}/${id}/prospect/link-business-partner`, {
      method: 'POST',
      body: JSON.stringify({ businessPartnerId }),
    });
    return response.data;
  },

  async createBusinessPartner(
    id: string,
    request: CreateProspectBusinessPartnerRequest
  ): Promise<PropertyEnquiryProspect> {
    const response = await apiService.request<
      Envelope<PropertyEnquiryProspect>
    >(`${baseUrl}/${id}/prospect/create-business-partner`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return response.data;
  },

  async finalizeBusinessPartner(id: string): Promise<PropertyEnquiryProspect> {
    const response = await apiService.request<
      Envelope<PropertyEnquiryProspect>
    >(`${baseUrl}/${id}/prospect/finalize-business-partner`, {
      method: 'POST',
      body: '{}',
    });
    return response.data;
  },

  async createOpportunity(
    id: string,
    request: CreateProspectOpportunityRequest
  ): Promise<PropertyEnquiryProspect> {
    const response = await apiService.request<
      Envelope<PropertyEnquiryProspect>
    >(`${baseUrl}/${id}/prospect/opportunity`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return response.data;
  },

  async listDeposits(id: string): Promise<ProspectDepositReceipt[]> {
    const response = await apiService.request<
      Envelope<ProspectDepositReceipt[]>
    >(`${baseUrl}/${id}/prospect/deposits`, { method: 'GET' });
    return response.data ?? [];
  },

  async recordDeposit(
    id: string,
    request: RecordProspectDepositRequest
  ): Promise<ProspectDepositReceipt> {
    const response = await apiService.request<Envelope<ProspectDepositReceipt>>(
      `${baseUrl}/${id}/prospect/deposits`,
      { method: 'POST', body: JSON.stringify(request) }
    );
    return response.data;
  },

  async clearDeposit(
    id: string,
    receiptId: string,
    clearedAt?: string | null
  ): Promise<ProspectDepositReceipt> {
    const response = await apiService.request<Envelope<ProspectDepositReceipt>>(
      `${baseUrl}/${id}/prospect/deposits/${receiptId}/clear`,
      { method: 'POST', body: JSON.stringify({ clearedAt: clearedAt || null }) }
    );
    return response.data;
  },

  async reverseDeposit(
    id: string,
    receiptId: string,
    reason: string
  ): Promise<ProspectDepositReceipt> {
    const response = await apiService.request<Envelope<ProspectDepositReceipt>>(
      `${baseUrl}/${id}/prospect/deposits/${receiptId}/reverse`,
      { method: 'POST', body: JSON.stringify({ reason, reversalDate: null }) }
    );
    return response.data;
  },

  async getDepositPolicy(
    salesSaleableSourceId: string
  ): Promise<PropertyProspectDepositPolicy | null> {
    const response = await apiService.request<
      Envelope<PropertyProspectDepositPolicy | null>
    >(
      `${baseUrl}/prospect-deposit-policy?salesSaleableSourceId=${encodeURIComponent(
        salesSaleableSourceId
      )}`,
      { method: 'GET' }
    );
    return response.data ?? null;
  },

  async upsertDepositPolicy(
    request: UpsertPropertyProspectDepositPolicyRequest
  ): Promise<void> {
    await apiService.request(`${baseUrl}/prospect-deposit-policy`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
  },

  async sendEmail(id: string, body: string): Promise<void> {
    await apiService.request(`${baseUrl}/${id}/reply`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    });
  },

  async addActivity(
    id: string,
    request: CreateProspectActivityRequest
  ): Promise<void> {
    const suffix =
      request.activityType === 'FollowUp' && request.followUpAt
        ? ` Follow-up due: ${request.followUpAt}.`
        : '';
    await apiService.request(`${baseUrl}/${id}/internal-note`, {
      method: 'POST',
      body: JSON.stringify({
        body: `[${request.activityType}] ${request.notes}${suffix}`,
      }),
    });
  },
};

export default propertyEnquiryService;
