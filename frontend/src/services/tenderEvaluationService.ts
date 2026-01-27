// Tender Evaluation Service - API calls for tender evaluation management

// ==================== INTERFACES ====================

export interface TenderEvaluationDto {
  id: string;
  tenderBidId: string;
  tenderEvaluatorId: string;
  evaluatorName: string;
  evaluatorRole: string;
  evaluationDate: string;
  status: string; // Draft, Submitted, Approved

  // Tender and Business Partner Information
  tenderId?: string;
  tenderNumber: string;
  tenderTitle: string;
  businessPartnerName: string;
  bidNumber: string;

  // Scores (0-100)
  priceScore?: number;
  qualityScore?: number;
  deliveryScore?: number;
  experienceScore?: number;
  technicalScore?: number;
  complianceScore?: number;
  totalScore?: number;

  // Comments
  evaluationCriteriaJson?: string;
  technicalComments?: string;
  commercialComments?: string;
  overallComments?: string;
  isRecommended: boolean;
  recommendation?: string;
}

export interface CreateEvaluationDto {
  tenderBidId: string;
  priceScore?: number;
  qualityScore?: number;
  deliveryScore?: number;
  experienceScore?: number;
  technicalScore?: number;
  complianceScore?: number;
  evaluationCriteriaJson?: string;
  technicalComments?: string;
  commercialComments?: string;
  overallComments?: string;
  isRecommended?: boolean;
  recommendation?: string;
}

export interface UpdateEvaluationDto {
  priceScore?: number;
  qualityScore?: number;
  deliveryScore?: number;
  experienceScore?: number;
  technicalScore?: number;
  complianceScore?: number;
  evaluationCriteriaJson?: string;
  technicalComments?: string;
  commercialComments?: string;
  overallComments?: string;
  isRecommended?: boolean;
  recommendation?: string;
}

export interface SubmitEvaluationDto {
  confirmSubmission: boolean;
}

export interface EvaluationScorecardDto {
  tenderBidId: string;
  bidNumber: string;
  businessPartnerName: string;
  totalBidAmount: number;
  currency?: string;
  bidStatus?: string;
  rank?: number;
  isRecommended: boolean;
  evaluations: TenderEvaluationDto[];
  averagePriceScore?: number;
  averageQualityScore?: number;
  averageDeliveryScore?: number;
  averageExperienceScore?: number;
  averageTechnicalScore?: number;
  averageComplianceScore?: number;
  averageTotalScore?: number;
  recommendationCount?: number;
  totalEvaluators?: number;
}

export interface ConsolidatedEvaluationDto {
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  bidScorecards: EvaluationScorecardDto[];
  generatedDate: string;
  generatedByName?: string;
}

export interface BidEvaluationSummaryDto {
  bidId: string;
  bidNumber: string;
  businessPartnerName: string;
  totalBidAmount: number;
  bidStatus?: string;
  isCompliant: boolean;
  nonComplianceReasons?: string;

  // Weighted Scores
  weightedPriceScore?: number;
  weightedQualityScore?: number;
  weightedDeliveryScore?: number;
  weightedExperienceScore?: number;
  finalScore?: number;

  // QCBS Scores
  technicalScore?: number;
  financialScore?: number;
  combinedScore?: number;
  isQualifiedTechnically?: boolean;
  disqualificationReason?: string;

  rank: number;
  recommendationCount: number;
  isRecommended: boolean;
}

export interface EvaluationReportDto {
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  tenderType: string;
  publishDate?: string;
  submissionDeadline?: string;
  estimatedValue?: number;
  currency?: string;

  // Evaluation Criteria
  priceWeightage: number;
  qualityWeightage: number;
  deliveryWeightage: number;
  experienceWeightage: number;

  // QCBS Configuration
  useQCBSEvaluation: boolean;
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
  lowestBidAmount?: number;
  qualifiedBidsCount?: number;
  disqualifiedBidsCount?: number;

  // Statistics
  totalBidsReceived: number;
  compliantBids: number;
  nonCompliantBids: number;
  evaluatedBids: number;

  // Bid Evaluations
  bidEvaluations: BidEvaluationSummaryDto[];

  // Recommendations
  recommendedBidId?: string;
  recommendedBidNumber?: string;
  recommendedBusinessPartnerName?: string;
  recommendedBidAmount?: number;
  recommendationJustification?: string;

  generatedDate: string;
  generatedByName?: string;
}

// ==================== API FUNCTIONS ====================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

function getAuthHeaders(): HeadersInit {
  // Check localStorage for token - try both possible keys
  let token: string | null = null;

  if (typeof window !== 'undefined') {
    token = localStorage.getItem('authToken') || localStorage.getItem('token');
  }

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  return headers;
}

// Get evaluation by ID
export async function getEvaluationById(id: string): Promise<TenderEvaluationDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/${id}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch evaluation');
  }

  return response.json();
}

// Get evaluations by bid ID
export async function getEvaluationsByBidId(bidId: string): Promise<TenderEvaluationDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/by-bid/${bidId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch bid evaluations');
  }

  return response.json();
}

// Get my evaluations (for evaluators)
export async function getMyEvaluations(): Promise<TenderEvaluationDto[]> {
  const headers = getAuthHeaders();
  console.log('🔐 getMyEvaluations - Authorization header present:', !!headers['Authorization']);
  console.log('🔐 getMyEvaluations - Token in localStorage:', !!localStorage.getItem('authToken'));

  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/my-evaluations`, {
    headers,
  });

  if (!response.ok) {
    console.error('❌ getMyEvaluations failed:', response.status, response.statusText);
    throw new Error(`Failed to fetch my evaluations: ${response.status} ${response.statusText}`);
  }

  return response.json();
}

// Create evaluation
export async function createEvaluation(data: CreateEvaluationDto): Promise<TenderEvaluationDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error('Failed to create evaluation');
  }

  return response.json();
}

// Update evaluation
export async function updateEvaluation(id: string, data: UpdateEvaluationDto): Promise<TenderEvaluationDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error('Failed to update evaluation');
  }

  return response.json();
}

// Submit evaluation
export async function submitEvaluation(id: string, data: SubmitEvaluationDto): Promise<TenderEvaluationDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/${id}/submit`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    throw new Error('Failed to submit evaluation');
  }

  return response.json();
}

// Delete evaluation
export async function deleteEvaluation(id: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to delete evaluation');
  }
}

// Get bid scorecard
export async function getBidScorecard(bidId: string): Promise<EvaluationScorecardDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/scorecard/${bidId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch bid scorecard');
  }

  return response.json();
}

// Get consolidated evaluations for a tender
export async function getConsolidatedEvaluations(tenderId: string): Promise<ConsolidatedEvaluationDto[]> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/consolidated/${tenderId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch consolidated evaluations');
  }

  return response.json();
}

// Get evaluation report for a tender
export async function getEvaluationReport(tenderId: string): Promise<EvaluationReportDto> {
  const response = await fetch(`${API_BASE_URL}/procurement/TenderEvaluations/report/${tenderId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error('Failed to fetch evaluation report');
  }

  return response.json();
}
