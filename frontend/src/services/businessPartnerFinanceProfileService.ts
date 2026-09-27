const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

const authHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
};

async function result<T>(response: Response): Promise<T> {
  if (response.ok) return response.json();
  const body = await response.json().catch(() => null);
  const message = body?.detail || body?.title || 'Finance profile request failed.';
  throw new Error(body?.code ? `${message} (${body.code})` : message);
}

export interface BusinessPartnerApWhtDefault {
  id: string;
  categoryCode: string;
  categoryName?: string;
  withholdingTaxId: string;
  withholdingTaxCode: string;
  withholdingTaxName: string;
  rate: number;
  isDefaultForAp: boolean;
  isActive: boolean;
}

export interface BusinessPartnerApProfile {
  id: string;
  businessPartnerRoleId: string;
  versionNumber: number;
  status: 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Superseded';
  effectiveFrom: string;
  effectiveTo?: string;
  apReferenceNumber?: string;
  paymentTermId?: string;
  defaultTaxGroupId?: string;
  defaultExpenseAccountId?: string;
  subjectToWithholding: boolean;
  submittedById?: string;
  approvedById?: string;
  decisionReason?: string;
  withholdingDefaults: BusinessPartnerApWhtDefault[];
}

export interface BusinessPartnerArProfile {
  id: string;
  businessPartnerRoleId: string;
  versionNumber: number;
  status: 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Superseded';
  effectiveFrom: string;
  effectiveTo?: string;
  arReferenceNumber?: string;
  paymentTermId?: string;
  creditLimit?: number;
  isWithholdingAgent: boolean;
  submittedById?: string;
  approvedById?: string;
  decisionReason?: string;
}

export interface BusinessPartnerFinanceRole {
  id: string;
  roleType: 'Supplier' | 'Contractor' | 'Customer';
  status: 'Active' | 'Inactive';
  activeFromUtc: string;
  apProfiles: BusinessPartnerApProfile[];
  arProfiles: BusinessPartnerArProfile[];
}

export interface BusinessPartnerFinanceProfileSet {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  taxIdentificationNumber?: string;
  roles: BusinessPartnerFinanceRole[];
}

export interface SaveApProfile {
  businessPartnerRoleId: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  apReferenceNumber?: string | null;
  paymentTermId?: string | null;
  defaultTaxGroupId?: string | null;
  defaultExpenseAccountId?: string | null;
  subjectToWithholding: boolean;
  withholdingDefaults: Array<{
    categoryCode: string;
    categoryName?: string;
    withholdingTaxId: string;
    isDefaultForAp: boolean;
    isActive: boolean;
  }>;
}

export interface SaveArProfile {
  businessPartnerRoleId: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  arReferenceNumber?: string | null;
  paymentTermId?: string | null;
  creditLimit?: number | null;
  isWithholdingAgent: boolean;
}

const root = `${API_BASE_URL}/finance/business-partner-profiles`;

export const businessPartnerFinanceProfileService = {
  async get(partnerId: string) {
    return result<BusinessPartnerFinanceProfileSet>(
      await fetch(`${root}/${partnerId}`, { headers: authHeaders() })
    );
  },
  async saveAp(partnerId: string, profileId: string | null, request: SaveApProfile) {
    return result<BusinessPartnerApProfile>(
      await fetch(`${root}/${partnerId}/ap${profileId ? `/${profileId}` : ''}`, {
        method: profileId ? 'PUT' : 'POST', headers: authHeaders(), body: JSON.stringify(request),
      })
    );
  },
  async saveAr(partnerId: string, profileId: string | null, request: SaveArProfile) {
    return result<BusinessPartnerArProfile>(
      await fetch(`${root}/${partnerId}/ar${profileId ? `/${profileId}` : ''}`, {
        method: profileId ? 'PUT' : 'POST', headers: authHeaders(), body: JSON.stringify(request),
      })
    );
  },
  async decide(partnerId: string, ledger: 'ap' | 'ar', profileId: string, action: 'submit' | 'approve' | 'reject', reason?: string) {
    const response = await fetch(`${root}/${partnerId}/${ledger}/${profileId}/${action}`, {
      method: 'POST', headers: authHeaders(), body: JSON.stringify({ reason: reason || null }),
    });
    return ledger === 'ap' ? result<BusinessPartnerApProfile>(response) : result<BusinessPartnerArProfile>(response);
  },
};
