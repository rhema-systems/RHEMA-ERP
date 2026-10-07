const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

function getAuthHeaders(includeJson: boolean = true): HeadersInit {
  const token = typeof window !== 'undefined'
    ? localStorage.getItem('token') || localStorage.getItem('authToken')
    : null;

  return {
    ...(includeJson ? { 'Content-Type': 'application/json' } : {}),
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

async function getErrorMessage(response: Response, fallback: string): Promise<string> {
  try {
    const text = await response.text();
    if (!text) {
      return fallback;
    }

    const payload = JSON.parse(text);

    if (typeof payload === 'string') {
      return payload;
    }

    if (payload?.message) {
      return payload.message as string;
    }

    if (payload?.error) {
      return payload.error as string;
    }

    if (payload?.detail) {
      return payload.detail as string;
    }

    if (payload?.title) {
      return payload.title as string;
    }
  } catch {
    // Ignore parsing errors and fall back to the default message.
  }

  return fallback;
}

async function parseResponse<T>(response: Response, fallbackMessage: string): Promise<T> {
  if (!response.ok) {
    throw new Error(await getErrorMessage(response, fallbackMessage));
  }

  return response.json();
}

function buildQuery(params: Record<string, string | number | boolean | null | undefined>): string {
  const query = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    query.append(key, String(value));
  });

  const serialized = query.toString();
  return serialized ? `?${serialized}` : '';
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious?: boolean;
  hasNext?: boolean;
}

export interface CrmAccountOverviewDto {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  registrationStatus: string;
  customerType?: string;
  salesTerritory?: string;
  riskLevel?: string;
  performanceRating?: number;
  relatedLeadCount: number;
  openOpportunityCount: number;
  openOpportunityValue: number;
  weightedPipelineValue: number;
  activeQuoteCount: number;
  activeQuoteValue: number;
  totalProjectCount: number;
  activeProjectCount: number;
  projectValue: number;
  projectValuesByCurrency?: CrmCurrencyAmountDto[];
  projectsWithoutCurrencyCount?: number;
  totalContractCount: number;
  activeContractCount: number;
  contractValue: number;
  contractValuesByCurrency?: CrmCurrencyAmountDto[];
  contractsWithoutCurrencyCount?: number;
  tenderInvitationCount: number;
  tenderBidCount: number;
  tenderAwardCount: number;
  tenderAwardedValue: number;
  tenderAwardedValuesByCurrency?: CrmCurrencyAmountDto[];
  tenderAwardsWithoutCurrencyCount?: number;
  hasOpenFollowUp: boolean;
  isAtRisk: boolean;
  healthScore: number;
  healthCategory: string;
  nextMilestoneDate?: string;
}

export interface CrmAccountContactDto {
  contactId: string;
  contactName: string;
  contactTitle?: string;
  department?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  isPrimary: boolean;
}

export interface CrmContactListItemDto {
  contactId: string;
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  registrationStatus: string;
  contactName: string;
  contactTitle?: string;
  department?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  isPrimary: boolean;
  salesTerritory?: string;
  customerType?: string;
  hasOpenFollowUp: boolean;
  isAtRisk: boolean;
  healthScore: number;
  healthCategory: string;
  openOpportunityCount: number;
  openOpportunityValue: number;
  activeProjectCount: number;
  activeContractCount: number;
  nextMilestoneDate?: string;
}

export interface CrmReadinessListItemDto {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  registrationStatus: string;
  customerType?: string;
  salesTerritory?: string;
  documentCount: number;
  verifiedDocumentCount: number;
  expiringDocumentCount: number;
  expiredDocumentCount: number;
  licenseCount: number;
  expiringLicenseCount: number;
  expiredLicenseCount: number;
  financialRecordCount: number;
  latestFinancialYear?: number;
  latestAnnualRevenue?: number;
  creditRating?: string;
  openOpportunityCount: number;
  activeProjectCount: number;
  activeContractCount: number;
  isAtRisk: boolean;
  healthScore: number;
  healthCategory: string;
  readinessScore: number;
  readinessCategory: string;
  hasCriticalGap: boolean;
  nextComplianceDate?: string;
}

export interface CrmReadinessDocumentDto {
  documentId: string;
  documentType: string;
  documentName: string;
  isVerified: boolean;
  issueDate?: string;
  expiryDate?: string;
  uploadedAt: string;
  isExpired: boolean;
  isExpiringSoon: boolean;
}

export interface CrmReadinessLicenseDto {
  licenseId: string;
  licenseTypeName: string;
  licenseNumber: string;
  status: string;
  issuingAuthority?: string;
  issueDate?: string;
  expiryDate?: string;
  isExpired: boolean;
  isExpiringSoon: boolean;
}

export interface CrmReadinessFinancialDto {
  financialId: string;
  financialYear: number;
  annualRevenue?: number;
  netProfit?: number;
  totalAssets?: number;
  totalLiabilities?: number;
  creditRating?: string;
  isAudited: boolean;
  auditorName?: string;
  auditDate?: string;
}

export interface CrmReadinessSignalDto {
  label: string;
  severity: string;
  scoreImpact: number;
}

export interface CrmReadinessDetailDto extends CrmReadinessListItemDto {
  primaryContactName?: string;
  primaryEmail?: string;
  primaryPhone?: string;
  documents: CrmReadinessDocumentDto[];
  licenses: CrmReadinessLicenseDto[];
  financials: CrmReadinessFinancialDto[];
  signals: CrmReadinessSignalDto[];
}

export interface CrmRiskListItemDto {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  registrationStatus: string;
  customerType?: string;
  salesTerritory?: string;
  riskLevel?: string;
  performanceRating?: number;
  isBlacklisted: boolean;
  isOnCreditHold: boolean;
  openIncidentCount: number;
  criticalIncidentCount: number;
  pendingAppealCount: number;
  openReviewFollowUpCount: number;
  latestMetricScore?: number;
  latestMetricGrade?: string;
  latestMetricPeriod?: string;
  latestMetricCalculatedAt?: string;
  latestReviewDate?: string;
  openOpportunityCount: number;
  activeProjectCount: number;
  activeContractCount: number;
  isAtRisk: boolean;
  healthScore: number;
  healthCategory: string;
  riskScore: number;
  riskCategory: string;
  requiresEscalation: boolean;
  nextMilestoneDate?: string;
}

export interface CrmRiskPerformanceMetricDto {
  metricId: string;
  metricPeriod: string;
  year: number;
  month?: number;
  quarter?: number;
  overallPerformanceScore: number;
  performanceGrade?: string;
  onTimeDeliveryRate: number;
  qualityAcceptanceRate: number;
  complianceScore: number;
  complaintsReceived: number;
  complaintsResolved: number;
  contractViolations: number;
  calculatedAt: string;
}

export interface CrmRiskIncidentDto {
  incidentId: string;
  incidentNumber: string;
  incidentDate: string;
  incidentType: string;
  severity: string;
  status: string;
  description: string;
  financialImpact?: number;
  requiresSupplierResponse: boolean;
  supplierResponseDate?: string;
  resolvedDate?: string;
}

export interface CrmRiskReviewDto {
  reviewId: string;
  reviewNumber: string;
  reviewDate: string;
  reviewPeriod: string;
  overallScore: number;
  overallGrade?: string;
  status: string;
  requiresFollowUp: boolean;
  followUpDate?: string;
}

export interface CrmRiskAppealDto {
  appealId: string;
  appealNumber: string;
  appealDate: string;
  status: string;
  removeBlacklist?: boolean;
  reviewedDate?: string;
  approvedDate?: string;
  newBlacklistExpiryDate?: string;
}

export interface CrmRiskSignalDto {
  label: string;
  severity: string;
  scoreImpact: number;
}

export interface CrmRiskDetailDto extends CrmRiskListItemDto {
  primaryContactName?: string;
  primaryEmail?: string;
  primaryPhone?: string;
  creditLimit?: number;
  outstandingBalance?: number;
  performanceMetrics: CrmRiskPerformanceMetricDto[];
  incidents: CrmRiskIncidentDto[];
  reviews: CrmRiskReviewDto[];
  appeals: CrmRiskAppealDto[];
  signals: CrmRiskSignalDto[];
}

export interface CrmCollaborationListItemDto {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  registrationStatus: string;
  customerType?: string;
  salesTerritory?: string;
  latestApplicationNumber?: string;
  latestRegistrationLifecycleStatus?: string;
  latestRegistrationSubmittedDate?: string;
  latestRegistrationApprovedDate?: string;
  registrationDocumentCount: number;
  portalUserCount: number;
  activePortalUserCount: number;
  adminUserCount: number;
  tenderAssignmentCount: number;
  assignedTenderCount: number;
  portalProjectCount: number;
  collaborationProjectCount: number;
  openOpportunityCount: number;
  activeProjectCount: number;
  activeContractCount: number;
  isAtRisk: boolean;
  healthScore: number;
  healthCategory: string;
  collaborationScore: number;
  collaborationCategory: string;
  requiresEnablement: boolean;
  nextMilestoneDate?: string;
}

export interface CrmCollaborationRegistrationDto {
  registrationId: string;
  applicationNumber: string;
  status: string;
  createdAt: string;
  submittedDate?: string;
  reviewedDate?: string;
  approvedDate?: string;
  documentCount: number;
  verifiedDocumentCount: number;
  rejectedDocumentCount: number;
  lastStatusChangeDate?: string;
}

export interface CrmCollaborationPortalUserDto {
  portalUserId: string;
  userId: string;
  userName: string;
  fullName: string;
  email?: string;
  role: string;
  isActive: boolean;
  grantedAt: string;
  notes?: string;
}

export interface CrmCollaborationTenderAssignmentDto {
  assignmentId: string;
  tenderId: string;
  tenderNumber: string;
  tenderTitle: string;
  assignmentType: string;
  assignedToUserName?: string;
  assignedAt: string;
  notes?: string;
}

export interface CrmCollaborationProjectDto {
  projectId: string;
  projectCode: string;
  title: string;
  status: string;
  externalPortalAccessEnabled: boolean;
  externalCollaborationEnabled: boolean;
  targetEndDate?: string;
}

export interface CrmCollaborationSignalDto {
  label: string;
  severity: string;
  scoreImpact: number;
}

export interface CrmCollaborationDetailDto extends CrmCollaborationListItemDto {
  primaryContactName?: string;
  primaryEmail?: string;
  primaryPhone?: string;
  registrations: CrmCollaborationRegistrationDto[];
  portalUsers: CrmCollaborationPortalUserDto[];
  tenderAssignments: CrmCollaborationTenderAssignmentDto[];
  projects: CrmCollaborationProjectDto[];
  signals: CrmCollaborationSignalDto[];
}

export interface CrmServiceListItemDto {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  registrationStatus: string;
  customerType?: string;
  salesTerritory?: string;
  portalUserCount: number;
  activePortalUserCount: number;
  ticketCount: number;
  openTicketCount: number;
  overdueTicketCount: number;
  complaintTicketCount: number;
  helpdeskTicketCount: number;
  enquiryTicketCount: number;
  linkedProblemCount: number;
  openProblemCount: number;
  resolvedTicketCount30Days: number;
  averageFeedbackRating?: number;
  feedbackResponseCount: number;
  openOpportunityCount: number;
  activeContractCount: number;
  isAtRisk: boolean;
  healthScore: number;
  healthCategory: string;
  serviceScore: number;
  serviceCategory: string;
  requiresAttention: boolean;
  hasSlaBreachRisk: boolean;
  lastTicketCreatedAt?: string;
  lastResolvedAt?: string;
  nextMilestoneDate?: string;
}

export interface CrmServiceTicketDto {
  ticketId: string;
  ticketNumber: string;
  ticketType: string;
  priority: string;
  source: string;
  status: string;
  subject?: string;
  categoryName?: string;
  requesterName?: string;
  assignedToName?: string;
  createdAt: string;
  firstResponseDueAt?: string;
  resolutionDueAt?: string;
  resolvedAt?: string;
  feedbackRating?: number;
  isOpen: boolean;
  isOverdue: boolean;
  isComplaint: boolean;
}

export interface CrmServiceProblemDto {
  problemId: string;
  problemNumber: string;
  title: string;
  status: string;
  priority: string;
  ownerName?: string;
  createdFromTicketId?: string;
  linkedTicketCount: number;
  createdAt: string;
}

export interface CrmServiceSignalDto {
  label: string;
  severity: string;
  scoreImpact: number;
}

export interface CrmServiceDetailDto extends CrmServiceListItemDto {
  primaryContactName?: string;
  primaryEmail?: string;
  primaryPhone?: string;
  tickets: CrmServiceTicketDto[];
  problems: CrmServiceProblemDto[];
  signals: CrmServiceSignalDto[];
}

export interface CrmCampaignListItemDto {
  campaignId: string;
  name: string;
  campaignType: string;
  campaignStatus: string;
  startDate: string;
  endDate?: string;
  budget: number;
  actualCost: number;
  expectedRevenue: number;
  actualRevenue: number;
  roiPercent: number;
  targetAudience: number;
  actualAudience: number;
  responseCount: number;
  responseRate: number;
  leadsGenerated: number;
  opportunitiesGenerated: number;
  memberCount: number;
  activeMemberCount: number;
  respondedMemberCount: number;
  qualifiedLeadCount: number;
  convertedLeadCount: number;
  openOpportunityCount: number;
  weightedPipelineValue: number;
  influencedAccountCount: number;
  isActive: boolean;
  isEndingSoon: boolean;
  createdAt: string;
}

export interface CrmCampaignMemberDto {
  memberId: string;
  leadId: string;
  leadName: string;
  companyName?: string;
  leadStatus: string;
  qualificationScore: number;
  estimatedValue: number;
  convertedBusinessPartnerId?: string;
  convertedBusinessPartnerName?: string;
  memberStatus: string;
  dateAdded: string;
  responseDate?: string;
  responseType?: string;
  notes?: string;
  openOpportunityCount: number;
  weightedPipelineValue: number;
  needsFollowUp: boolean;
}

export interface CrmCampaignInfluenceAccountDto {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  convertedLeadCount: number;
  openOpportunityCount: number;
  weightedPipelineValue: number;
}

export interface CrmCampaignDetailDto extends CrmCampaignListItemDto {
  description?: string;
  notes?: string;
  members: CrmCampaignMemberDto[];
  influencedAccounts: CrmCampaignInfluenceAccountDto[];
  opportunities: CrmOpportunityOverviewDto[];
}

export interface CrmOpportunityOverviewDto {
  opportunityId: string;
  name: string;
  stageDefinitionId?: string;
  stage: string;
  amount: number;
  currency: string;
  probability: number;
  weightedValue: number;
  expectedCloseDate: string;
  opportunityType: string;
  leadSource: string;
  customerId?: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  leadId?: string;
  leadName?: string;
}

export interface CrmOpportunityListItemDto extends CrmOpportunityOverviewDto {
  actualCloseDate?: string;
  isClosingSoon: boolean;
  createdAt: string;
}

export interface CrmOpportunityDetailDto extends CrmOpportunityListItemDto {
  description?: string;
  assignedToId?: string;
  competitors?: string;
  notes?: string;
  lossReason?: string;
  quotes: CrmQuoteListItemDto[];
  relatedContracts: CrmContractSummaryDto[];
  relatedProjects: CrmProjectSummaryDto[];
  conversionChain: CrmConversionChainDto;
}

export interface CrmLeadListItemDto {
  leadId: string;
  firstName: string;
  lastName: string;
  fullName: string;
  companyName?: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  leadSource: string;
  leadStatus: string;
  qualificationScore: number;
  estimatedValue: number;
  lastContactDate?: string;
  nextFollowUpDate?: string;
  assignedToId?: string;
  opportunityCount: number;
  needsFollowUp: boolean;
  createdAt: string;
}

export interface CrmLeadDetailDto extends CrmLeadListItemDto {
  mobile?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  notes?: string;
  convertedBusinessPartnerId?: string;
  convertedDate?: string;
  propertyEnquiryTicketId?: string;
  propertyEnquiryTicketNumber?: string;
  propertyEnquiryCurrency?: string;
  opportunities: CrmOpportunityOverviewDto[];
}

export interface CrmQuoteSummaryDto {
  quoteId: string;
  opportunityId: string;
  quoteName: string;
  quoteStatus: string;
  value: number;
  currency: string;
  validUntil: string;
  opportunityName?: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  isExpiringSoon: boolean;
}

export interface CrmQuoteListItemDto extends CrmQuoteSummaryDto {
  documentNumber: string;
  documentDate: string;
  createdAt: string;
  sentDate?: string;
  acceptedDate?: string;
  leadId?: string;
  leadName?: string;
  isAccepted: boolean;
}

export interface CrmQuoteLineItemDto {
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  productCode?: string;
  unit?: string;
  discountAmount: number;
  taxAmount: number;
}

export interface CrmHealthSignalDto {
  label: string;
  direction: string;
  scoreImpact: number;
}

export interface CrmConversionChainNodeDto {
  stage: string;
  entityType: string;
  entityId: string;
  title: string;
  status: string;
  amount?: number;
  currency?: string;
  referenceDate?: string;
  relationshipType: string;
  relationshipNote?: string;
  referenceCode?: string;
}

export interface CrmConversionChainDto {
  opportunityId: string;
  opportunityName: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  leadId?: string;
  leadName?: string;
  nodes: CrmConversionChainNodeDto[];
}

export interface CrmQuoteDetailDto extends CrmQuoteListItemDto {
  proposal?: string;
  convertedInvoiceId?: string;
  lineItems: CrmQuoteLineItemDto[];
  conversionChain: CrmConversionChainDto;
}

export interface CrmActivitySummaryDto {
  activityId: string;
  subject: string;
  activityType: string;
  activityStatus: string;
  activityDate: string;
  dueDate?: string;
  requiresFollowUp: boolean;
  nextFollowUpDate?: string;
  priority: number;
  assignedToId?: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  leadId?: string;
  leadName?: string;
  opportunityId?: string;
  opportunityName?: string;
  relatedEntityType?: string;
  relatedEntityId?: string;
  relatedEntityHref?: string;
}

export interface CrmActivityListItemDto extends CrmActivitySummaryDto {
  isOverdue: boolean;
  createdAt: string;
}

export interface CrmActivityDetailDto extends CrmActivityListItemDto {
  description?: string;
  duration?: number;
  location?: string;
  attendees?: string;
  externalAttendees?: string;
  internalAttendees?: CrmActivityEmployeeAttendeeDto[];
  outcome?: string;
  notes?: string;
  propertyEnquiryTicketId?: string;
  propertyEnquiryTicketNumber?: string;
  propertyEnquirySubject?: string;
}

export interface CrmActivityEmployeeAttendeeDto {
  employeeId: string;
  employeeNumber: string;
  displayName: string;
}

export interface CrmProjectSummaryDto {
  projectId: string;
  projectCode: string;
  title: string;
  status: string;
  value: number;
  progressPercent: number;
  businessPartnerId?: string;
  contractId?: string;
  relationshipType: string;
  targetEndDate?: string;
}

export interface CrmProjectListItemDto extends CrmProjectSummaryDto {
  resolvedBusinessPartnerId?: string;
  businessPartnerName?: string;
  contractNumber?: string;
  contractTitle?: string;
  startDate?: string;
  actualEndDate?: string;
  statusRemarks?: string;
  isOverdue: boolean;
  isLinkedToActiveContract: boolean;
}

export interface CrmProjectDetailDto extends CrmProjectListItemDto {
  summary?: string;
  businessCase?: string;
  objectives?: string;
  methodology: string;
  estimatedBudget?: number;
  approvedBudget?: number;
  actualCost?: number;
  budgetStatus: string;
  externalPortalAccessEnabled: boolean;
  externalCollaborationEnabled: boolean;
}

export interface CrmContractSummaryDto {
  contractId: string;
  contractNumber: string;
  contractTitle: string;
  status: string;
  contractValue: number;
  businessPartnerId: string;
  tenderAwardId: string;
  tenderId: string;
  relationshipType: string;
  endDate?: string;
}

export interface CrmContractListItemDto extends CrmContractSummaryDto {
  businessPartnerName?: string;
  currency: string;
  contractType: string;
  startDate?: string;
  signedDate?: string;
  paymentTerms?: string;
  projectCount: number;
  activeProjectCount: number;
  isActive: boolean;
  isExpiringSoon: boolean;
}

export interface CrmContractDetailDto extends CrmContractListItemDto {
  scopeOfWork?: string;
  deliverables?: string;
  specialConditions?: string;
  penaltyClause?: string;
  signedByName?: string;
  contractorSignatoryName?: string;
  activatedAt?: string;
  completedAt?: string;
  terminatedAt?: string;
  terminationReason?: string;
  notes?: string;
}

export interface CrmTenderSummaryDto {
  entityId: string;
  tenderId: string;
  entityType: string;
  businessPartnerId: string;
  businessPartnerName?: string;
  tenderNumber: string;
  tenderTitle: string;
  tenderType: string;
  currency: string;
  referenceNumber: string;
  status: string;
  amount: number;
  createdAt: string;
  submissionDeadline?: string;
  relatedContractId?: string;
  relatedContractNumber?: string;
}

export interface CrmTenderListItemDto extends CrmTenderSummaryDto {
  tenderStatus: string;
  publishDate?: string;
  awardDate?: string;
  isClosingSoon: boolean;
}

export interface CrmTenderDetailDto extends CrmTenderListItemDto {
  tenderBidId?: string;
  tenderAwardId?: string;
  relatedContractTitle?: string;
  invitedDate?: string;
  viewedDate?: string;
  responseDate?: string;
  declineReason?: string;
  submittedDate?: string;
  totalScore?: number;
  rank?: number;
  deliveryDays?: number;
  paymentTerms?: string;
  warrantyTerms?: string;
  isCompliant?: boolean;
  nonComplianceReasons?: string;
  originalBidAmount?: number;
  isNegotiated?: boolean;
  awardJustification?: string;
  purchaseOrderId?: string;
  notes?: string;
  relationshipNote?: string;
}

export interface CrmFollowUpOverviewDto {
  entityType: string;
  entityId: string;
  title: string;
  status: string;
  dueDate: string;
  context?: string;
}

export interface CrmOverviewDto {
  totalLeadCount: number;
  qualifiedLeadCount: number;
  leadsNeedingFollowUpCount: number;
  openOpportunityCount: number;
  openOpportunityValue: number;
  weightedPipelineValue: number;
  openOpportunityValuesByCurrency?: CrmCurrencyAmountDto[];
  weightedPipelineValuesByCurrency?: CrmCurrencyAmountDto[];
  openOpportunitiesWithoutCurrencyCount?: number;
  activeQuoteCount: number;
  activeQuoteValue: number;
  activeQuoteValuesByCurrency?: CrmCurrencyAmountDto[];
  activeQuotesWithoutCurrencyCount?: number;
  activeAccountCount: number;
  atRiskAccountCount: number;
  averageAccountHealthScore: number;
  activeProjectCount: number;
  activeContractCount: number;
  expiringContractCount: number;
  accounts: CrmAccountOverviewDto[];
  opportunities: CrmOpportunityOverviewDto[];
  followUps: CrmFollowUpOverviewDto[];
}

export interface CrmCurrencyAmountDto {
  currency: string;
  amount: number;
}

export interface CrmPipelineStageReportDto {
  stage: string;
  opportunityCount: number;
  totalValue: number;
  weightedValue: number;
  quoteCount: number;
}

export interface CrmAccountHealthReportDto {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  healthScore: number;
  healthCategory: string;
  riskLevel?: string;
  performanceRating?: number;
  openOpportunityCount: number;
  openOpportunityValue: number;
  activeProjectCount: number;
  activeContractCount: number;
  hasOpenFollowUp: boolean;
  isAtRisk: boolean;
  nextMilestoneDate?: string;
}

export interface CrmReportingDto {
  totalLeadCount: number;
  qualifiedLeadCount: number;
  convertedLeadCount: number;
  leadConversionRate: number;
  openOpportunityCount: number;
  openOpportunityValue: number;
  weightedPipelineValue: number;
  totalQuoteCount: number;
  acceptedQuoteCount: number;
  acceptedQuoteValue: number;
  quoteAcceptanceRate: number;
  activeAccountCount: number;
  averageAccountHealthScore: number;
  atRiskAccountCount: number;
  pipelineByStage: CrmPipelineStageReportDto[];
  accountHealth: CrmAccountHealthReportDto[];
  closingOpportunities: CrmOpportunityListItemDto[];
  expiringContracts: CrmContractSummaryDto[];
}

export interface CrmForecastBucketDto {
  periodStart: string;
  periodLabel: string;
  opportunityCount: number;
  renewalOpportunityCount: number;
  campaignBackedOpportunityCount: number;
  bestCaseValue: number;
  weightedValue: number;
  commitValue: number;
  quoteCoverageValue: number;
  renewalContractValue: number;
  renewalCoverageValue: number;
}

export interface CrmForecastDealDto {
  opportunityId: string;
  name: string;
  stage: string;
  opportunityType: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  leadId?: string;
  leadName?: string;
  amount: number;
  currency: string;
  probability: number;
  weightedValue: number;
  expectedCloseDate: string;
  quoteCount: number;
  campaignCount: number;
  forecastCategory: string;
  campaignContext?: string;
}

export interface CrmForecastRenewalDto {
  contractId: string;
  contractNumber: string;
  contractTitle: string;
  businessPartnerId: string;
  businessPartnerName: string;
  endDate?: string;
  contractValue: number;
  renewalOpportunityCount: number;
  renewalWeightedValue: number;
  renewalCommitValue: number;
  coverageGapValue: number;
  activeCampaignCount: number;
  coverageCategory: string;
}

export interface CrmForecastDto {
  horizonMonths: number;
  horizonStart: string;
  horizonEnd: string;
  opportunityCount: number;
  bestCaseValue: number;
  weightedPipelineValue: number;
  commitValue: number;
  campaignBackedWeightedValue: number;
  renewalContractValue: number;
  renewalCoverageValue: number;
  renewalGapValue: number;
  averageProbability: number;
  buckets: CrmForecastBucketDto[];
  highConfidenceDeals: CrmForecastDealDto[];
  renewalWatchlist: CrmForecastRenewalDto[];
}

export interface CrmConversionStageMetricDto {
  stage: string;
  entityCount: number;
  relatedOpportunityCount: number;
  totalValue: number;
  valuesByCurrency?: CrmCurrencyAmountDto[];
  unspecifiedCurrencyCount?: number;
  conversionRate: number;
}

export interface CrmConversionJourneyDto {
  opportunityId: string;
  opportunityName: string;
  stage: string;
  opportunityType: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  leadId?: string;
  leadName?: string;
  amount: number;
  currency: string;
  weightedValue: number;
  expectedCloseDate: string;
  quoteCount: number;
  contractCount: number;
  projectCount: number;
  coverageStatus: string;
  leakageReason?: string;
  chain: CrmConversionChainDto;
}

export interface CrmConversionLeakDto {
  entityType: string;
  entityId: string;
  name: string;
  status: string;
  leakageStage: string;
  leakageReason: string;
  businessPartnerId?: string;
  businessPartnerName?: string;
  amount?: number;
  currency?: string;
  referenceDate?: string;
}

export interface CrmConversionsDto {
  horizonMonths: number;
  horizonStart: string;
  horizonEnd: string;
  leadCount: number;
  leadWithOpportunityCount: number;
  opportunityCount: number;
  quotedOpportunityCount: number;
  contractBackedOpportunityCount: number;
  projectBackedOpportunityCount: number;
  leadToOpportunityRate: number;
  opportunityToQuoteRate: number;
  opportunityToContractRate: number;
  opportunityToProjectRate: number;
  totalOpportunityValue: number;
  weightedPipelineValue: number;
  totalOpportunityValuesByCurrency?: CrmCurrencyAmountDto[];
  weightedPipelineValuesByCurrency?: CrmCurrencyAmountDto[];
  opportunitiesWithoutCurrencyCount?: number;
  funnel: CrmConversionStageMetricDto[];
  journeys: CrmConversionJourneyDto[];
  leakage: CrmConversionLeakDto[];
}

export interface CrmAccountDetailDto {
  businessPartnerId: string;
  partnerCode: string;
  partnerName: string;
  partnerType: string;
  registrationStatus: string;
  customerType?: string;
  salesTerritory?: string;
  riskLevel?: string;
  performanceRating?: number;
  primaryContactName?: string;
  primaryContactTitle?: string;
  primaryEmail?: string;
  primaryPhone?: string;
  website?: string;
  physicalAddress?: string;
  physicalCity?: string;
  physicalCountry?: string;
  paymentTerms?: string;
  currency?: string;
  creditLimit?: number;
  outstandingBalance?: number;
  isOnCreditHold: boolean;
  creditHoldReason?: string;
  customerSince?: string;
  notes?: string;
  hasOpenFollowUp: boolean;
  isAtRisk: boolean;
  healthScore: number;
  healthCategory: string;
  nextMilestoneDate?: string;
  relatedLeadCount: number;
  openOpportunityCount: number;
  openOpportunityValue: number;
  weightedPipelineValue: number;
  activeQuoteCount: number;
  activeQuoteValue: number;
  activeProjectCount: number;
  projectValue: number;
  activeContractCount: number;
  contractValue: number;
  expiringContractCount: number;
  tenderInvitationCount: number;
  tenderBidCount: number;
  tenderAwardCount: number;
  tenderAwardedValue: number;
  contacts: CrmAccountContactDto[];
  leads: CrmLeadListItemDto[];
  opportunities: CrmOpportunityOverviewDto[];
  quotes: CrmQuoteSummaryDto[];
  activities: CrmActivitySummaryDto[];
  projects: CrmProjectSummaryDto[];
  contracts: CrmContractSummaryDto[];
  tenders: CrmTenderSummaryDto[];
  healthSignals: CrmHealthSignalDto[];
}

export interface CreateCrmLeadDto {
  firstName: string;
  lastName: string;
  companyName?: string;
  jobTitle?: string;
  email?: string;
  phone?: string;
  mobile?: string;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
  leadSource: string;
  leadStatus: string;
  qualificationScore: number;
  estimatedValue: number;
  lastContactDate?: string;
  nextFollowUpDate?: string;
  assignedToId?: string;
  notes?: string;
}

export type UpdateCrmLeadDto = CreateCrmLeadDto;

export interface CreateCrmOpportunityDto {
  name: string;
  description?: string;
  businessPartnerId?: string;
  leadId?: string;
  stageDefinitionId?: string;
  stage?: string;
  probability: number;
  amount: number;
  currency: string;
  expectedCloseDate: string;
  actualCloseDate?: string;
  leadSource: string;
  opportunityType: string;
  assignedToId?: string;
  competitors?: string;
  notes?: string;
  lossReason?: string;
}

export type UpdateCrmOpportunityDto = CreateCrmOpportunityDto;

export interface CrmOpportunityStageDefinitionDto {
  stageId: string;
  code: string;
  name: string;
  sortOrder: number;
  isActive: boolean;
  isClosed: boolean;
  isWon: boolean;
  isLost: boolean;
  defaultProbability?: number;
}

export interface UpdateCrmOpportunityStageDto extends Omit<CrmOpportunityStageDefinitionDto, 'stageId'> {
  stageId?: string;
}

export interface CreateCrmActivityDto {
  subject: string;
  activityType: string;
  description?: string;
  activityDate: string;
  dueDate?: string;
  activityStatus: string;
  priority: number;
  duration?: number;
  assignedToId?: string;
  businessPartnerId?: string;
  leadId?: string;
  opportunityId?: string;
  propertyEnquiryTicketId?: string;
  location?: string;
  attendees?: string;
  externalAttendees?: string;
  internalAttendeeEmployeeIds?: string[];
  outcome?: string;
  notes?: string;
  requiresFollowUp: boolean;
  nextFollowUpDate?: string;
}

export type UpdateCrmActivityDto = CreateCrmActivityDto;

export interface CreateCrmCampaignDto {
  name: string;
  campaignType: string;
  description?: string;
  startDate: string;
  endDate?: string;
  campaignStatus: string;
  budget: number;
  actualCost: number;
  expectedRevenue: number;
  actualRevenue: number;
  targetAudience: number;
  actualAudience: number;
  responseCount: number;
  leadsGenerated: number;
  opportunitiesGenerated: number;
  notes?: string;
}

export type UpdateCrmCampaignDto = CreateCrmCampaignDto;

export interface CreateCrmCampaignMemberDto {
  leadId: string;
  memberStatus: string;
  responseDate?: string;
  responseType?: string;
  notes?: string;
}

export interface UpdateCrmCampaignMemberDto {
  memberStatus: string;
  responseDate?: string;
  responseType?: string;
  notes?: string;
}

export interface GetCrmLeadsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  followUpOnly?: boolean;
}

export interface GetCrmAccountsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  healthCategory?: string;
  atRiskOnly?: boolean;
  partnerType?: string;
}

export interface GetCrmContactsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  businessPartnerId?: string;
  department?: string;
  primaryOnly?: boolean;
  atRiskOnly?: boolean;
  partnerType?: string;
}

export interface GetCrmReadinessParams {
  page?: number;
  pageSize?: number;
  search?: string;
  readinessCategory?: string;
  expiringOnly?: boolean;
  missingFinancialsOnly?: boolean;
  partnerType?: string;
}

export interface GetCrmRiskParams {
  page?: number;
  pageSize?: number;
  search?: string;
  riskCategory?: string;
  escalationOnly?: boolean;
  openIncidentOnly?: boolean;
  partnerType?: string;
}

export interface GetCrmCollaborationParams {
  page?: number;
  pageSize?: number;
  search?: string;
  collaborationCategory?: string;
  enablementOnly?: boolean;
  pendingOnboardingOnly?: boolean;
  partnerType?: string;
}

export interface GetCrmServiceParams {
  page?: number;
  pageSize?: number;
  search?: string;
  serviceCategory?: string;
  attentionOnly?: boolean;
  overdueOnly?: boolean;
  partnerType?: string;
}

export interface GetCrmCampaignsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  campaignType?: string;
  activeOnly?: boolean;
  businessPartnerId?: string;
  leadId?: string;
}

export interface GetCrmConversionsParams {
  months?: number;
  opportunityType?: string;
  businessPartnerId?: string;
}

export interface GetCrmForecastParams {
  months?: number;
  opportunityType?: string;
  businessPartnerId?: string;
}

export interface GetCrmOpportunitiesParams {
  page?: number;
  pageSize?: number;
  search?: string;
  stage?: string;
  stageDefinitionId?: string;
  reachedStageDefinitionId?: string;
  stageEnteredFrom?: string;
  stageEnteredTo?: string;
  businessPartnerId?: string;
  leadId?: string;
  opportunityType?: string;
}

export interface GetCrmActivitiesParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  activityType?: string;
  followUpOnly?: boolean;
  businessPartnerId?: string;
  opportunityId?: string;
  leadId?: string;
}

export interface GetCrmQuotesParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  businessPartnerId?: string;
  opportunityId?: string;
  leadId?: string;
}

export interface GetCrmProjectsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  businessPartnerId?: string;
  contractId?: string;
}

export interface GetCrmContractsParams {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: string;
  businessPartnerId?: string;
  expiringOnly?: boolean;
}

export interface GetCrmTendersParams {
  page?: number;
  pageSize?: number;
  search?: string;
  entityType?: string;
  status?: string;
  businessPartnerId?: string;
  tenderId?: string;
}

class CrmService {
  async getOverview(take: number = 10): Promise<CrmOverviewDto> {
    const response = await fetch(`${API_BASE_URL}/crm/overview?take=${take}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmOverviewDto>(response, 'Failed to load CRM overview');
  }

  async getAccounts(params: GetCrmAccountsParams = {}): Promise<PagedResult<CrmAccountOverviewDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/accounts${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      healthCategory: params.healthCategory,
      atRiskOnly: params.atRiskOnly ?? false,
      partnerType: params.partnerType,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmAccountOverviewDto>>(response, 'Failed to load CRM accounts');
  }

  async getAccountDetail(businessPartnerId: string, take: number = 10): Promise<CrmAccountDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/accounts/${businessPartnerId}?take=${take}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmAccountDetailDto>(response, 'Failed to load CRM account detail');
  }

  async getContacts(params: GetCrmContactsParams = {}): Promise<PagedResult<CrmContactListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/contacts${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      businessPartnerId: params.businessPartnerId,
      department: params.department,
      primaryOnly: params.primaryOnly ?? false,
      atRiskOnly: params.atRiskOnly ?? false,
      partnerType: params.partnerType,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmContactListItemDto>>(response, 'Failed to load CRM contacts');
  }

  async getReadiness(params: GetCrmReadinessParams = {}): Promise<PagedResult<CrmReadinessListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/readiness${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      readinessCategory: params.readinessCategory,
      expiringOnly: params.expiringOnly ?? false,
      missingFinancialsOnly: params.missingFinancialsOnly ?? false,
      partnerType: params.partnerType,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmReadinessListItemDto>>(response, 'Failed to load CRM readiness');
  }

  async getReadinessDetail(businessPartnerId: string, take: number = 10): Promise<CrmReadinessDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/readiness/${businessPartnerId}?take=${take}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmReadinessDetailDto>(response, 'Failed to load CRM readiness detail');
  }

  async getRisk(params: GetCrmRiskParams = {}): Promise<PagedResult<CrmRiskListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/risk${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      riskCategory: params.riskCategory,
      escalationOnly: params.escalationOnly ?? false,
      openIncidentOnly: params.openIncidentOnly ?? false,
      partnerType: params.partnerType,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmRiskListItemDto>>(response, 'Failed to load CRM account risk');
  }

  async getRiskDetail(businessPartnerId: string, take: number = 10): Promise<CrmRiskDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/risk/${businessPartnerId}?take=${take}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmRiskDetailDto>(response, 'Failed to load CRM account risk detail');
  }

  async getCollaboration(params: GetCrmCollaborationParams = {}): Promise<PagedResult<CrmCollaborationListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/collaboration${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      collaborationCategory: params.collaborationCategory,
      enablementOnly: params.enablementOnly ?? false,
      pendingOnboardingOnly: params.pendingOnboardingOnly ?? false,
      partnerType: params.partnerType,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmCollaborationListItemDto>>(response, 'Failed to load CRM collaboration');
  }

  async getCollaborationDetail(businessPartnerId: string, take: number = 10): Promise<CrmCollaborationDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/collaboration/${businessPartnerId}?take=${take}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmCollaborationDetailDto>(response, 'Failed to load CRM collaboration detail');
  }

  async getService(params: GetCrmServiceParams = {}): Promise<PagedResult<CrmServiceListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/service${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      serviceCategory: params.serviceCategory,
      attentionOnly: params.attentionOnly ?? false,
      overdueOnly: params.overdueOnly ?? false,
      partnerType: params.partnerType,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmServiceListItemDto>>(response, 'Failed to load CRM service');
  }

  async getServiceDetail(businessPartnerId: string, take: number = 10): Promise<CrmServiceDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/service/${businessPartnerId}?take=${take}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmServiceDetailDto>(response, 'Failed to load CRM service detail');
  }

  async getCampaigns(params: GetCrmCampaignsParams = {}): Promise<PagedResult<CrmCampaignListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/campaigns${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      status: params.status,
      campaignType: params.campaignType,
      activeOnly: params.activeOnly ?? false,
      businessPartnerId: params.businessPartnerId,
      leadId: params.leadId,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmCampaignListItemDto>>(response, 'Failed to load CRM campaigns');
  }

  async getCampaign(campaignId: string): Promise<CrmCampaignDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/campaigns/${campaignId}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmCampaignDetailDto>(response, 'Failed to load CRM campaign');
  }

  async createCampaign(dto: CreateCrmCampaignDto): Promise<CrmCampaignDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/campaigns`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmCampaignDetailDto>(response, 'Failed to create CRM campaign');
  }

  async updateCampaign(campaignId: string, dto: UpdateCrmCampaignDto): Promise<CrmCampaignDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/campaigns/${campaignId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmCampaignDetailDto>(response, 'Failed to update CRM campaign');
  }

  async deleteCampaign(campaignId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/crm/campaigns/${campaignId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(false),
    });

    if (!response.ok) {
      throw new Error(await getErrorMessage(response, 'Failed to delete CRM campaign'));
    }
  }

  async addCampaignMember(campaignId: string, dto: CreateCrmCampaignMemberDto): Promise<CrmCampaignMemberDto> {
    const response = await fetch(`${API_BASE_URL}/crm/campaigns/${campaignId}/members`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmCampaignMemberDto>(response, 'Failed to add CRM campaign member');
  }

  async updateCampaignMember(
    campaignId: string,
    memberId: string,
    dto: UpdateCrmCampaignMemberDto,
  ): Promise<CrmCampaignMemberDto> {
    const response = await fetch(`${API_BASE_URL}/crm/campaigns/${campaignId}/members/${memberId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmCampaignMemberDto>(response, 'Failed to update CRM campaign member');
  }

  async deleteCampaignMember(campaignId: string, memberId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/crm/campaigns/${campaignId}/members/${memberId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(false),
    });

    if (!response.ok) {
      throw new Error(await getErrorMessage(response, 'Failed to delete CRM campaign member'));
    }
  }

  async getReporting(take: number = 10): Promise<CrmReportingDto> {
    const response = await fetch(`${API_BASE_URL}/crm/reports?take=${take}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmReportingDto>(response, 'Failed to load CRM reporting');
  }

  async getForecast(params: GetCrmForecastParams = {}): Promise<CrmForecastDto> {
    const response = await fetch(`${API_BASE_URL}/crm/forecast${buildQuery({
      months: params.months ?? 6,
      opportunityType: params.opportunityType,
      businessPartnerId: params.businessPartnerId,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmForecastDto>(response, 'Failed to load CRM forecast');
  }

  async getConversions(params: GetCrmConversionsParams = {}): Promise<CrmConversionsDto> {
    const response = await fetch(`${API_BASE_URL}/crm/conversions${buildQuery({
      months: params.months ?? 6,
      opportunityType: params.opportunityType,
      businessPartnerId: params.businessPartnerId,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmConversionsDto>(response, 'Failed to load CRM conversions');
  }

  async getLeads(params: GetCrmLeadsParams = {}): Promise<PagedResult<CrmLeadListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/leads${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      status: params.status,
      followUpOnly: params.followUpOnly ?? false,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmLeadListItemDto>>(response, 'Failed to load CRM leads');
  }

  async getLead(leadId: string): Promise<CrmLeadDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/leads/${leadId}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmLeadDetailDto>(response, 'Failed to load CRM lead');
  }

  async createLead(dto: CreateCrmLeadDto): Promise<CrmLeadDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/leads`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmLeadDetailDto>(response, 'Failed to create CRM lead');
  }

  async updateLead(leadId: string, dto: UpdateCrmLeadDto): Promise<CrmLeadDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/leads/${leadId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmLeadDetailDto>(response, 'Failed to update CRM lead');
  }

  async deleteLead(leadId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/crm/leads/${leadId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(false),
    });

    if (!response.ok) {
      throw new Error(await getErrorMessage(response, 'Failed to delete CRM lead'));
    }
  }

  async getOpportunities(params: GetCrmOpportunitiesParams = {}): Promise<PagedResult<CrmOpportunityListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/opportunities${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      stage: params.stage,
      stageDefinitionId: params.stageDefinitionId,
      reachedStageDefinitionId: params.reachedStageDefinitionId,
      stageEnteredFrom: params.stageEnteredFrom,
      stageEnteredTo: params.stageEnteredTo,
      businessPartnerId: params.businessPartnerId,
      leadId: params.leadId,
      opportunityType: params.opportunityType,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmOpportunityListItemDto>>(response, 'Failed to load CRM opportunities');
  }

  async getOpportunity(opportunityId: string): Promise<CrmOpportunityDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/opportunities/${opportunityId}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmOpportunityDetailDto>(response, 'Failed to load CRM opportunity');
  }

  async getOpportunityStages(includeInactive = false): Promise<CrmOpportunityStageDefinitionDto[]> {
    const response = await fetch(`${API_BASE_URL}/crm/opportunity-stages${buildQuery({ includeInactive })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmOpportunityStageDefinitionDto[]>(response, 'Failed to load opportunity stages');
  }

  async updateOpportunityStages(stages: UpdateCrmOpportunityStageDto[]): Promise<CrmOpportunityStageDefinitionDto[]> {
    const response = await fetch(`${API_BASE_URL}/crm/opportunity-stages`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify({ stages }),
    });

    return parseResponse<CrmOpportunityStageDefinitionDto[]>(response, 'Failed to update opportunity stages');
  }

  async getQuotes(params: GetCrmQuotesParams = {}): Promise<PagedResult<CrmQuoteListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/quotes${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      status: params.status,
      businessPartnerId: params.businessPartnerId,
      opportunityId: params.opportunityId,
      leadId: params.leadId,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmQuoteListItemDto>>(response, 'Failed to load CRM quotes');
  }

  async getQuote(quoteId: string): Promise<CrmQuoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/quotes/${quoteId}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmQuoteDetailDto>(response, 'Failed to load CRM quote');
  }

  async getProjects(params: GetCrmProjectsParams = {}): Promise<PagedResult<CrmProjectListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/projects${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      status: params.status,
      businessPartnerId: params.businessPartnerId,
      contractId: params.contractId,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmProjectListItemDto>>(response, 'Failed to load CRM projects');
  }

  async getProject(projectId: string): Promise<CrmProjectDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/projects/${projectId}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmProjectDetailDto>(response, 'Failed to load CRM project');
  }

  async getContracts(params: GetCrmContractsParams = {}): Promise<PagedResult<CrmContractListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/contracts${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      status: params.status,
      businessPartnerId: params.businessPartnerId,
      expiringOnly: params.expiringOnly ?? false,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmContractListItemDto>>(response, 'Failed to load CRM contracts');
  }

  async getContract(contractId: string): Promise<CrmContractDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/contracts/${contractId}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmContractDetailDto>(response, 'Failed to load CRM contract');
  }

  async getTenders(params: GetCrmTendersParams = {}): Promise<PagedResult<CrmTenderListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/tenders${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      entityType: params.entityType,
      status: params.status,
      businessPartnerId: params.businessPartnerId,
      tenderId: params.tenderId,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmTenderListItemDto>>(response, 'Failed to load CRM tenders');
  }

  async getTender(entityType: string, entityId: string): Promise<CrmTenderDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/tenders/${entityType}/${entityId}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmTenderDetailDto>(response, 'Failed to load CRM tender');
  }

  async createOpportunity(dto: CreateCrmOpportunityDto): Promise<CrmOpportunityDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/opportunities`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmOpportunityDetailDto>(response, 'Failed to create CRM opportunity');
  }

  async updateOpportunity(opportunityId: string, dto: UpdateCrmOpportunityDto): Promise<CrmOpportunityDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/opportunities/${opportunityId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmOpportunityDetailDto>(response, 'Failed to update CRM opportunity');
  }

  async deleteOpportunity(opportunityId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/crm/opportunities/${opportunityId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(false),
    });

    if (!response.ok) {
      throw new Error(await getErrorMessage(response, 'Failed to delete CRM opportunity'));
    }
  }

  async getActivities(params: GetCrmActivitiesParams = {}): Promise<PagedResult<CrmActivityListItemDto>> {
    const response = await fetch(`${API_BASE_URL}/crm/activities${buildQuery({
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 25,
      search: params.search,
      status: params.status,
      activityType: params.activityType,
      followUpOnly: params.followUpOnly ?? false,
      businessPartnerId: params.businessPartnerId,
      opportunityId: params.opportunityId,
      leadId: params.leadId,
    })}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<PagedResult<CrmActivityListItemDto>>(response, 'Failed to load CRM activities');
  }

  async getActivity(activityId: string): Promise<CrmActivityDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/activities/${activityId}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });

    return parseResponse<CrmActivityDetailDto>(response, 'Failed to load CRM activity');
  }

  async createActivity(dto: CreateCrmActivityDto): Promise<CrmActivityDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/activities`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmActivityDetailDto>(response, 'Failed to create CRM activity');
  }

  async updateActivity(activityId: string, dto: UpdateCrmActivityDto): Promise<CrmActivityDetailDto> {
    const response = await fetch(`${API_BASE_URL}/crm/activities/${activityId}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });

    return parseResponse<CrmActivityDetailDto>(response, 'Failed to update CRM activity');
  }

  async deleteActivity(activityId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/crm/activities/${activityId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(false),
    });

    if (!response.ok) {
      throw new Error(await getErrorMessage(response, 'Failed to delete CRM activity'));
    }
  }

  // ── Lead Checklists ──

  async getLeadChecklists(leadId: string): Promise<CrmLeadChecklistDto[]> {
    const response = await fetch(`${API_BASE_URL}/sales/leads/${leadId}/checklists`, {
      headers: getAuthHeaders(false),
    });
    return parseResponse<CrmLeadChecklistDto[]>(response, 'Failed to load checklists');
  }

  async createLeadChecklist(leadId: string, dto: CreateCrmLeadChecklistDto): Promise<CrmLeadChecklistDto> {
    const response = await fetch(`${API_BASE_URL}/sales/leads/${leadId}/checklists`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    return parseResponse<CrmLeadChecklistDto>(response, 'Failed to create checklist');
  }

  async toggleChecklistItem(checklistId: string, itemId: string, isDone: boolean): Promise<CrmLeadChecklistDto> {
    const response = await fetch(`${API_BASE_URL}/sales/checklists/${checklistId}/items/${itemId}`, {
      method: 'PATCH',
      headers: getAuthHeaders(),
      body: JSON.stringify({ isDone }),
    });
    return parseResponse<CrmLeadChecklistDto>(response, 'Failed to toggle checklist item');
  }

  async deleteChecklist(checklistId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/sales/checklists/${checklistId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(false),
    });
    if (!response.ok) throw new Error(await getErrorMessage(response, 'Failed to delete checklist'));
  }

  // ── Opportunity Cost Lines ──

  async getOpportunityCostLines(opportunityId: string): Promise<CrmOpportunityCostLineDto[]> {
    const response = await fetch(`${API_BASE_URL}/sales/opportunities/${opportunityId}/cost-lines`, {
      headers: getAuthHeaders(false),
    });
    return parseResponse<CrmOpportunityCostLineDto[]>(response, 'Failed to load cost lines');
  }

  async createOpportunityCostLine(opportunityId: string, dto: CreateCrmOpportunityCostLineDto): Promise<CrmOpportunityCostLineDto> {
    const response = await fetch(`${API_BASE_URL}/sales/opportunities/${opportunityId}/cost-lines`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    return parseResponse<CrmOpportunityCostLineDto>(response, 'Failed to create cost line');
  }

  async deleteOpportunityCostLine(costLineId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/sales/cost-lines/${costLineId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(false),
    });
    if (!response.ok) throw new Error(await getErrorMessage(response, 'Failed to delete cost line'));
  }

  // ── SWOT Analysis ──

  async getSwotAnalyses(entityType: string, entityId: string): Promise<CrmSwotAnalysisDto[]> {
    const response = await fetch(`${API_BASE_URL}/sales/swot/${entityType}/${entityId}`, {
      headers: getAuthHeaders(false),
    });
    return parseResponse<CrmSwotAnalysisDto[]>(response, 'Failed to load SWOT analyses');
  }

  async createSwotAnalysis(dto: CreateCrmSwotAnalysisDto): Promise<CrmSwotAnalysisDto> {
    const response = await fetch(`${API_BASE_URL}/sales/swot`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    return parseResponse<CrmSwotAnalysisDto>(response, 'Failed to create SWOT analysis');
  }

  async deleteSwotAnalysis(analysisId: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/sales/swot/${analysisId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(false),
    });
    if (!response.ok) throw new Error(await getErrorMessage(response, 'Failed to delete SWOT analysis'));
  }

  // Commercial quote operations use the Sales quote API; keep these names distinct
  // from the CRM quote list/detail methods above.
  async getSalesQuotes(
    page: number = 1,
    pageSize: number = 20,
    search?: string,
    status?: string,
    opportunityId?: string,
    customerId?: string
  ): Promise<PagedResult<QuoteSummaryDto>> {
    const queryParams: Record<string, string | number | boolean | null | undefined> = {
      page,
      pageSize,
      search,
      status,
      opportunityId,
      customerId,
    };
    const response = await fetch(`${API_BASE_URL}/sales/quotes${buildQuery(queryParams)}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });
    return parseResponse<PagedResult<QuoteSummaryDto>>(response, 'Failed to load quotes');
  }

  async getSalesQuote(id: string): Promise<QuoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/quotes/${id}`, {
      headers: getAuthHeaders(false),
      cache: 'no-store',
    });
    return parseResponse<QuoteDetailDto>(response, 'Failed to load quote detail');
  }

  async createQuote(dto: CreateQuoteDto): Promise<QuoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/quotes`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    return parseResponse<QuoteDetailDto>(response, 'Failed to create quote');
  }

  async updateQuote(id: string, dto: UpdateQuoteDto): Promise<QuoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/quotes/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(dto),
    });
    return parseResponse<QuoteDetailDto>(response, 'Failed to update quote');
  }

  async sendQuote(id: string): Promise<QuoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/quotes/${id}/send`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    return parseResponse<QuoteDetailDto>(response, 'Failed to send quote');
  }

  async acceptQuote(id: string): Promise<QuoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/quotes/${id}/accept`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    return parseResponse<QuoteDetailDto>(response, 'Failed to accept quote');
  }

  async rejectQuote(id: string, reason?: string): Promise<QuoteDetailDto> {
    const response = await fetch(`${API_BASE_URL}/sales/quotes/${id}/reject${reason ? `?reason=${encodeURIComponent(reason)}` : ''}`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    return parseResponse<QuoteDetailDto>(response, 'Failed to reject quote');
  }

  async convertToSalesOrder(id: string): Promise<{ salesOrderId: string }> {
    const response = await fetch(`${API_BASE_URL}/sales/quotes/${id}/convert-to-sales-order`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });
    return parseResponse<{ salesOrderId: string }>(response, 'Failed to convert quote to sales order');
  }
}

export const crmService = new CrmService();

// ── Lead Checklist Types ──

export interface CrmLeadChecklistDto {
  id: string;
  leadId: string;
  title: string;
  description?: string;
  isCompleted: boolean;
  completedAt?: string;
  createdAt: string;
  items: CrmLeadChecklistItemDto[];
}

export interface CrmLeadChecklistItemDto {
  id: string;
  taskName: string;
  isDone: boolean;
  completedAt?: string;
  sortOrder: number;
}

export interface CreateCrmLeadChecklistDto {
  title: string;
  description?: string;
  items: { taskName: string; sortOrder: number }[];
}

// ── Opportunity Cost Line Types ──

export interface CrmOpportunityCostLineDto {
  id: string;
  opportunityId: string;
  expenseAccountId: string;
  amount: number;
  dateIncurred: string;
  description: string;
  isPostedToGL: boolean;
  journalEntryId?: string;
  postedDate?: string;
  createdAt: string;
}

export interface CreateCrmOpportunityCostLineDto {
  expenseAccountId: string;
  amount: number;
  dateIncurred: string;
  description: string;
}

// ── SWOT Analysis Types ──

export interface CrmSwotAnalysisDto {
  id: string;
  relatedEntityType: string;
  relatedEntityId: string;
  title: string;
  analysisDate: string;
  authorName?: string;
  summary?: string;
  createdAt: string;
  entries: CrmSwotEntryDto[];
}

export interface CrmSwotEntryDto {
  id: string;
  category: string; // Strength | Weakness | Opportunity | Threat
  description: string;
  impactScore: number;
  likelihoodScore: number;
  mitigationAction?: string;
  dateIdentified: string;
}

export interface CreateCrmSwotAnalysisDto {
  relatedEntityType: string;
  relatedEntityId: string;
  title: string;
  authorName?: string;
  summary?: string;
  entries: CreateCrmSwotEntryDto[];
}

export interface CreateCrmSwotEntryDto {
  category: string;
  description: string;
  impactScore: number;
  likelihoodScore: number;
  mitigationAction?: string;
}

// ── Quote DTO Interfaces ──
export interface QuoteSummaryDto {
  id: string;
  documentNumber: string;
  quoteName: string;
  quoteStatus: string;
  opportunityId: string;
  opportunityName?: string;
  customerName?: string;
  totalAmount: number;
  taxAmount: number;
  currency: string;
  exchangeRate: number;
  validUntil: string;
  sentDate?: string;
  acceptedDate?: string;
  lineCount: number;
  createdAt: string;
}

export interface QuoteDetailDto extends QuoteSummaryDto {
  businessPartnerId?: string;
  subTotal: number;
  discountAmount: number;
  shippingAmount: number;
  proposal?: string;
  convertedInvoiceId?: string;
  convertedInvoiceNumber?: string;
  taxGroupId?: string;
  baseCurrencyAmount: number;
  lineItems: QuoteLineItemDto[];
}

export interface QuoteLineItemDto {
  id: string;
  quoteId: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  productCode?: string;
  unit?: string;
  discountPercentage: number;
  discountAmount: number;
  taxAmount: number;
  taxCode?: string;
  taxGroupId?: string;
}

export interface CreateQuoteDto {
  opportunityId: string;
  businessPartnerId?: string;
  quoteName: string;
  validUntil: string;
  shippingAmount?: number;
  proposal?: string;
  currency?: string;
  exchangeRate?: number;
  taxGroupId?: string;
  lineItems: CreateQuoteLineItemDto[];
}

export interface CreateQuoteLineItemDto {
  description: string;
  quantity: number;
  unitPrice: number;
  productCode?: string;
  unit?: string;
  discountPercentage?: number;
  taxAmount?: number;
  taxCode?: string;
  taxGroupId?: string;
}

export interface UpdateQuoteDto {
  quoteName?: string;
  validUntil?: string;
  shippingAmount?: number;
  proposal?: string;
  currency?: string;
  exchangeRate?: number;
  taxGroupId?: string;
}

