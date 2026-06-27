const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
function getAuthHeaders(): Record<string, string> {
  const token = typeof window !== 'undefined' ? (localStorage.getItem('token') || localStorage.getItem('authToken')) : null;
  return { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) };
}

export interface CompetitorSummary {
  id: string; name: string; industry?: string; threatLevel: string;
  estimatedMarketShare: number; isActive: boolean;
  dealCount: number; openDeals: number; wonDeals: number; lostDeals: number;
  totalDealValue: number; openDealValue: number; winRate: number; createdAt: string;
}
export interface CompetitorDetail extends CompetitorSummary {
  website?: string; description?: string; strengths?: string; weaknesses?: string;
  keyProducts?: string; pricingStrategy?: string; deals: CompetitorDeal[];
}
export interface CompetitorDeal {
  id: string; opportunityName?: string; customerName?: string;
  threatLevel: string; outcome: string; dealValue: number;
  competitorProposal?: string; ourDifferentiator?: string;
  lessonsLearned?: string; reportedDate: string; resolvedDate?: string;
}
export interface CreateCompetitor {
  name: string; website?: string; industry?: string; description?: string;
  strengths?: string; weaknesses?: string; keyProducts?: string;
  pricingStrategy?: string; estimatedMarketShare: number; threatLevel: string;
}
export interface CreateCompetitorDeal {
  competitorId: string; opportunityId?: string; opportunityName?: string;
  customerName?: string; threatLevel: string; dealValue: number;
  competitorProposal?: string; ourDifferentiator?: string;
}
export interface CompetitorThreatBreakdown {
  threatLevel: string; competitorCount: number; dealCount: number; openDealValue: number;
}
export interface CompetitorIndustryBreakdown {
  industry: string; competitorCount: number; averageMarketShare: number;
}
export interface CompetitorAnalytics {
  totalCompetitors: number; activeCompetitors: number;
  highThreatCompetitors: number; criticalThreatCompetitors: number;
  openCompetitiveDeals: number; wonDeals: number; lostDeals: number;
  totalCompetitiveDealValue: number; openCompetitiveDealValue: number;
  winRate: number; averageMarketShare: number;
  threatBreakdown: CompetitorThreatBreakdown[];
  industryBreakdown: CompetitorIndustryBreakdown[];
  recentDeals: CompetitorDeal[];
}

export const competitorService = {
  async getCompetitors(page = 1, pageSize = 20, search?: string, threatLevel?: string) {
    const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() });
    if (search) params.append('search', search);
    if (threatLevel) params.append('threatLevel', threatLevel);
    const res = await fetch(`${API_BASE_URL}/sales/competitors?${params}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch competitors');
    return { data: await res.json() };
  },
  async getAnalytics() {
    const res = await fetch(`${API_BASE_URL}/sales/competitors/analytics`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch competitor analytics');
    const data = (await res.json()) as CompetitorAnalytics;
    return { data };
  },
  async getCompetitorById(id: string) {
    const res = await fetch(`${API_BASE_URL}/sales/competitors/${id}`, { headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to fetch competitor');
    return { data: await res.json() };
  },
  async createCompetitor(data: CreateCompetitor) {
    const res = await fetch(`${API_BASE_URL}/sales/competitors`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to create competitor');
    return { data: await res.json() };
  },
  async updateCompetitor(id: string, data: CreateCompetitor) {
    const res = await fetch(`${API_BASE_URL}/sales/competitors/${id}`, { method: 'PUT', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to update competitor');
    return { data: await res.json() };
  },
  async trackDeal(data: CreateCompetitorDeal) {
    const res = await fetch(`${API_BASE_URL}/sales/competitors/deals`, { method: 'POST', headers: getAuthHeaders(), body: JSON.stringify(data) });
    if (!res.ok) throw new Error(await res.text() || 'Failed to track deal');
    return { data: await res.json() };
  },
  async updateDealOutcome(dealId: string, outcome: string, lessonsLearned?: string) {
    const params = new URLSearchParams({ outcome });
    if (lessonsLearned) params.append('lessonsLearned', lessonsLearned);
    const res = await fetch(`${API_BASE_URL}/sales/competitors/deals/${dealId}/outcome?${params}`, { method: 'POST', headers: getAuthHeaders() });
    if (!res.ok) throw new Error('Failed to update deal outcome');
    return { data: await res.json() };
  },
};
