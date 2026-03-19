const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Helper function to get auth headers
const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

// ============================================================================
// Types
// ============================================================================

export interface SupplierPerformanceMetricDto {
  id: string;
  businessPartnerId: string;
  metricNumber: string;
  metricPeriod: string; // Monthly, Quarterly, Yearly
  year: number;
  month?: number;
  quarter?: number;
  
  // Delivery metrics
  totalOrders: number;
  onTimeDeliveries: number;
  lateDeliveries: number;
  onTimeDeliveryRate: number;
  averageDeliveryDelayDays: number;
  
  // Quality metrics
  totalItemsReceived: number;
  defectiveItems: number;
  rejectedItems: number;
  returnedItems: number;
  qualityAcceptanceRate: number;
  defectRate: number;
  
  // Cost metrics
  totalPurchaseValue: number;
  priceVariancePercentage: number;
  costCompetitivenessScore: number;
  
  // Service metrics
  averageResponseTimeHours: number;
  customerServiceRating: number;
  complaintsReceived: number;
  complaintsResolved: number;
  
  // Compliance metrics
  contractViolations: number;
  termsBreaches: number;
  complianceScore: number;
  
  // Innovation metrics
  improvementSuggestions: number;
  costSavingInitiatives: number;
  innovationScore: number;
  
  // Overall
  overallPerformanceScore: number;
  performanceGrade: string;
  
  calculatedAt: string;
  calculationDate?: string;
  calculatedBy?: string;
  notes?: string;
}

export interface QualityIncidentDto {
  id: string;
  businessPartnerId: string;
  incidentNumber: string;
  incidentType: string;
  incidentDate: string;
  purchaseOrderId?: string;
  purchaseOrderNumber?: string;
  receiptId?: string;
  receiptNumber?: string;
  
  description: string;
  severity: string;
  status: string;
  
  quantityAffected: number;
  estimatedCost: number;
  
  reportedById?: string;
  reportedByName?: string;
  reportedAt: string;
  
  acknowledgedAt?: string;
  supplierResponse?: string;
  correctiveAction?: string;
  
  resolvedAt?: string;
  resolutionNotes?: string;
  
  rootCause?: string;
  preventiveMeasures?: string;
}

export interface PerformanceReviewDto {
  id: string;
  businessPartnerId: string;
  partnerName?: string;
  reviewNumber: string;
  reviewPeriod: string;
  reviewYear?: number;
  reviewMonth?: number;
  reviewQuarter?: number;

  reviewDate: string;
  periodStartDate: string;
  periodEndDate: string;
  reviewedById?: string;
  reviewedByName?: string;

  deliveryPerformanceScore: number;
  qualityScore: number;
  costCompetitivenessScore: number;
  customerServiceScore: number;
  complianceScore: number;
  innovationScore: number;
  overallScore: number;
  overallGrade?: string;

  strengths?: string;
  weaknesses?: string;
  areasForImprovement?: string;
  recommendations?: string;
  actionItems?: string;

  status: string;
  submittedDate?: string;
  acknowledgedAt?: string;
  acknowledgedDate?: string;
  finalizedAt?: string;
  supplierComments?: string;
  supplierCommentsDate?: string;
  requiresFollowUp?: boolean;
  followUpDate?: string;
  notes?: string;
  createdAt: string;
}

export interface PerformanceTrendDto {
  period: string;
  onTimeDeliveryRate: number;
  qualityAcceptanceRate: number;
  overallScore: number;
}

export interface PerformanceReportCardDto {
  businessPartnerId: string;
  businessPartnerName: string;
  period: string;
  
  currentMetric: SupplierPerformanceMetricDto;
  previousMetric?: SupplierPerformanceMetricDto;
  trends: PerformanceTrendDto[];
  recentIncidents: QualityIncidentDto[];
  latestReview?: PerformanceReviewDto;
  
  performanceGrade: string;
  overallScore: number;
  recommendation: string;
}

// ============================================================================
// Service
// ============================================================================

class PerformanceTrackingService {
  private baseUrl = `${API_BASE_URL}/procurement/supplier-performance`;
  private incidentsUrl = `${API_BASE_URL}/procurement/quality-incidents`;
  private reviewsUrl = `${API_BASE_URL}/procurement/performance-reviews`;

  // Supplier Performance Metrics
  async getMetricById(id: string): Promise<SupplierPerformanceMetricDto> {
    const response = await fetch(`${this.baseUrl}/${id}`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error('Failed to get metric');
    return response.json();
  }

  async getMetricsByBusinessPartner(businessPartnerId: string): Promise<SupplierPerformanceMetricDto[]> {
    const response = await fetch(`${this.baseUrl}/business-partner/${businessPartnerId}`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error('Failed to get metrics');
    return response.json();
  }

  async getPerformanceTrends(businessPartnerId: string, period: string = 'Monthly', count: number = 12): Promise<PerformanceTrendDto[]> {
    const response = await fetch(`${this.baseUrl}/business-partner/${businessPartnerId}/trends?period=${period}&count=${count}`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error('Failed to get trends');
    return response.json();
  }

  async getPerformanceReportCard(businessPartnerId: string, period: string = 'Monthly', year?: number, month?: number, quarter?: number): Promise<PerformanceReportCardDto> {
    const params = new URLSearchParams({ period });
    if (year) params.append('year', year.toString());
    if (month) params.append('month', month.toString());
    if (quarter) params.append('quarter', quarter.toString());
    const response = await fetch(`${this.baseUrl}/business-partner/${businessPartnerId}/report-card?${params}`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error('Failed to get report card');
    return response.json();
  }

  async calculateMetrics(businessPartnerId: string, metricPeriod: string, year: number, month?: number, quarter?: number): Promise<SupplierPerformanceMetricDto> {
    const response = await fetch(`${this.baseUrl}/calculate`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ businessPartnerId, metricPeriod, year, month, quarter })
    });
    if (!response.ok) throw new Error('Failed to calculate metrics');
    return response.json();
  }

  async deleteMetric(id: string): Promise<void> {
    const response = await fetch(`${this.baseUrl}/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to delete metric');
  }

  // Quality Incidents
  async getIncidentById(id: string): Promise<QualityIncidentDto> {
    const response = await fetch(`${this.incidentsUrl}/${id}`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error('Failed to get incident');
    return response.json();
  }

  async getIncidentsByBusinessPartner(businessPartnerId: string): Promise<QualityIncidentDto[]> {
    const response = await fetch(`${this.incidentsUrl}/business-partner/${businessPartnerId}`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error('Failed to get incidents');
    return response.json();
  }

  async createIncident(incident: Partial<QualityIncidentDto>): Promise<QualityIncidentDto> {
    const response = await fetch(this.incidentsUrl, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(incident)
    });
    if (!response.ok) throw new Error('Failed to create incident');
    return response.json();
  }

  async updateIncident(id: string, incident: Partial<QualityIncidentDto>): Promise<QualityIncidentDto> {
    const response = await fetch(`${this.incidentsUrl}/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(incident)
    });
    if (!response.ok) throw new Error('Failed to update incident');
    return response.json();
  }

  async acknowledgeIncident(id: string, responseText: string): Promise<QualityIncidentDto> {
    const response = await fetch(`${this.incidentsUrl}/${id}/acknowledge`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ response: responseText })
    });
    if (!response.ok) throw new Error('Failed to acknowledge incident');
    return response.json();
  }

  async resolveIncident(id: string, resolutionNotes: string, rootCause?: string, preventiveMeasures?: string): Promise<QualityIncidentDto> {
    const response = await fetch(`${this.incidentsUrl}/${id}/resolve`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ resolutionNotes, rootCause, preventiveMeasures })
    });
    if (!response.ok) throw new Error('Failed to resolve incident');
    return response.json();
  }

  async deleteIncident(id: string): Promise<void> {
    const response = await fetch(`${this.incidentsUrl}/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to delete incident');
  }

  // Performance Reviews
  async getReviewById(id: string): Promise<PerformanceReviewDto> {
    const response = await fetch(`${this.reviewsUrl}/${id}`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error('Failed to get review');
    return response.json();
  }

  async getReviewsByBusinessPartner(businessPartnerId: string): Promise<PerformanceReviewDto[]> {
    const response = await fetch(`${this.reviewsUrl}/business-partner/${businessPartnerId}`, { headers: getAuthHeaders() });
    if (!response.ok) throw new Error('Failed to get reviews');
    return response.json();
  }

  async createReview(review: Partial<PerformanceReviewDto>): Promise<PerformanceReviewDto> {
    const response = await fetch(this.reviewsUrl, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(review)
    });
    if (!response.ok) throw new Error('Failed to create review');
    return response.json();
  }

  async updateReview(id: string, review: Partial<PerformanceReviewDto>): Promise<PerformanceReviewDto> {
    const response = await fetch(`${this.reviewsUrl}/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(review)
    });
    if (!response.ok) throw new Error('Failed to update review');
    return response.json();
  }

  async submitReview(id: string): Promise<PerformanceReviewDto> {
    const response = await fetch(`${this.reviewsUrl}/${id}/submit`, {
      method: 'POST',
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to submit review');
    return response.json();
  }

  async acknowledgeReview(id: string): Promise<PerformanceReviewDto> {
    const response = await fetch(`${this.reviewsUrl}/${id}/acknowledge`, {
      method: 'POST',
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to acknowledge review');
    return response.json();
  }

  async finalizeReview(id: string): Promise<PerformanceReviewDto> {
    const response = await fetch(`${this.reviewsUrl}/${id}/finalize`, {
      method: 'POST',
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to finalize review');
    return response.json();
  }

  async deleteReview(id: string): Promise<void> {
    const response = await fetch(`${this.reviewsUrl}/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error('Failed to delete review');
  }
}

export const performanceTrackingService = new PerformanceTrackingService();
