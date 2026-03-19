import { apiService } from './api.service';

export interface ComplianceRequirement {
  id: string;
  name: string;
  description: string;
  regulatoryBody: string;
  regulatoryAuthority: string;
  standard: string;
  assetCategories: string[];
  category: string;
  workOrderTypes: string[];
  frequency: 'Daily' | 'Weekly' | 'Monthly' | 'Quarterly' | 'Annually' | 'BiAnnually';
  frequencyDays: number;
  isActive: boolean;
  isMandatory: boolean;
  gracePeriodDays: number;
  requiredDocumentation: string[];
  requiredDocuments: string[];
  inspectionRequired: boolean;
  certificationRequired: boolean;
  auditTrailRequired: boolean;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  penaltyDescription: string;
  penalties?: CompliancePenalty[];
  createdAt: string;
  updatedAt: string;
}

export interface CompliancePenalty {
  type: 'Fine' | 'Shutdown' | 'License_Revocation' | 'Warning';
  amount?: number;
  currency?: string;
  description: string;
  severity: 'Low' | 'Medium' | 'High' | 'Critical';
}

export interface ComplianceStatus {
  id: string;
  assetId: string;
  assetName: string;
  assetType: string;
  requirementId: string;
  requirement: ComplianceRequirement;
  status: 'Compliant' | 'Non_Compliant' | 'At_Risk' | 'Overdue' | 'Pending';
  complianceStatus: 'Compliant' | 'Non-Compliant' | 'At Risk' | 'Overdue' | 'Pending';
  lastComplianceDate?: string;
  lastCheckDate?: string;
  nextDueDate: string;
  dueDate: string;
  daysUntilDue: number;
  lastInspectionId?: string;
  lastCertificationNumber?: string;
  certificateValidUntil?: string;
  responsiblePersonId?: string;
  responsiblePersonName?: string;
  title?: string;
  priority?: 'Low' | 'Medium' | 'High' | 'Critical';
  notes?: string;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  updatedAt: string;
}

export interface ComplianceReport {
  id: string;
  assetId: string;
  requirementId: string;
  title: string;
  description: string;
  type: string;
  reportDate: string;
  generatedDate: string;
  periodStart: string;
  periodEnd: string;
  reportedBy: string;
  status: 'Compliant' | 'Non_Compliant';
  evidence: ComplianceEvidence[];
  notes?: string;
  inspectionId?: string;
  certificateNumber?: string;
  validUntil?: string;
  approvedBy?: string;
  approvedAt?: string;
}

export interface ComplianceEvidence {
  id: string;
  type: 'Document' | 'Photo' | 'Certificate' | 'Inspection_Report' | 'Test_Result';
  fileId: string;
  fileName: string;
  description: string;
  uploadedAt: string;
  uploadedBy: string;
}

export interface ComplianceAuditLog {
  id: string;
  assetId: string;
  requirementId: string;
  action: 'Status_Change' | 'Report_Submitted' | 'Inspection_Completed' | 'Certificate_Updated' | 'Deadline_Missed';
  entityType: 'Asset' | 'Requirement' | 'Report' | 'Status';
  previousValue?: string;
  newValue?: string;
  performedBy: string;
  performedAt: string;
  timestamp: string;
  details?: string;
  ipAddress?: string;
}

export interface ComplianceDashboard {
  totalAssets: number;
  compliantAssets: number;
  atRiskAssets: number;
  overdueAssets: number;
  overdueItems: number;
  violations: number;
  complianceRate: number;
  upcomingDeadlines: ComplianceStatus[];
  recentViolations: ComplianceStatus[];
  criticalRequirements: ComplianceRequirement[];
  complianceByCategory: { category: string; compliant: number; total: number }[];
  complianceByRequirement: { requirement: string; compliant: number; total: number }[];
  auditActivity: ComplianceAuditLog[];
  totalRequirements: number;
  compliantRequirements: number;
  nonCompliantRequirements: number;
  pendingRequirements: number;
  lastAuditDate: string;
  nextAuditDate?: string;
}

export type AuditLog = ComplianceAuditLog;

type ComplianceReportGeneration = {
  title: string;
  description: string;
  requirementIds: string[];
  assetIds: string[];
  dateRange: {
    start: string;
    end: string;
  };
  format: 'pdf' | 'excel' | 'csv';
  includePhotos: boolean;
  includeSignatures: boolean;
};

const getRequirementRiskLevel = (requirement: Pick<ComplianceRequirement, 'penalties'>): ComplianceRequirement['riskLevel'] => {
  const severities = requirement.penalties?.map((penalty) => penalty.severity) ?? [];

  if (severities.includes('Critical')) return 'Critical';
  if (severities.includes('High')) return 'High';
  if (severities.includes('Medium')) return 'Medium';
  return 'Low';
};

const getLegacyComplianceStatus = (status: ComplianceStatus['status']): ComplianceStatus['complianceStatus'] => {
  switch (status) {
    case 'Non_Compliant':
      return 'Non-Compliant';
    case 'At_Risk':
      return 'At Risk';
    default:
      return status;
  }
};

const enrichRequirement = (requirement: ComplianceRequirement): ComplianceRequirement => ({
  ...requirement,
  regulatoryAuthority: requirement.regulatoryAuthority || requirement.regulatoryBody,
  category: requirement.category || requirement.assetCategories[0] || 'General',
  requiredDocuments: requirement.requiredDocuments?.length ? requirement.requiredDocuments : requirement.requiredDocumentation,
  riskLevel: requirement.riskLevel || getRequirementRiskLevel(requirement),
  penaltyDescription: requirement.penaltyDescription || requirement.penalties?.map((penalty) => penalty.description).join('; ') || 'No penalty specified',
});

const enrichStatus = (status: ComplianceStatus): ComplianceStatus => {
  const requirement = enrichRequirement(status.requirement);

  return {
    ...status,
    requirement,
    assetType: status.assetType || requirement.category,
    complianceStatus: status.complianceStatus || getLegacyComplianceStatus(status.status),
    lastCheckDate: status.lastCheckDate || status.lastComplianceDate,
    dueDate: status.dueDate || status.nextDueDate,
    title: status.title || requirement.name,
    priority: status.priority || status.riskLevel,
  };
};

const enrichReport = (report: ComplianceReport, requirementName?: string): ComplianceReport => ({
  ...report,
  title: report.title || `${requirementName || 'Compliance'} Report`,
  description: report.description || report.notes || 'Compliance submission',
  type: report.type || 'Compliance',
  generatedDate: report.generatedDate || report.reportDate,
  periodStart: report.periodStart || report.reportDate,
  periodEnd: report.periodEnd || report.validUntil || report.reportDate,
});

const enrichAuditLog = (log: ComplianceAuditLog): ComplianceAuditLog => ({
  ...log,
  entityType: log.entityType || 'Asset',
  timestamp: log.timestamp || log.performedAt,
});

const enrichDashboard = (dashboard: ComplianceDashboard): ComplianceDashboard => {
  const upcomingDeadlines = dashboard.upcomingDeadlines.map(enrichStatus);
  const recentViolations = dashboard.recentViolations.map(enrichStatus);
  const criticalRequirements = dashboard.criticalRequirements.map(enrichRequirement);
  const auditActivity = dashboard.auditActivity.map(enrichAuditLog);
  const totalRequirements = dashboard.totalRequirements || criticalRequirements.length;
  const nonCompliantRequirements = dashboard.nonCompliantRequirements || recentViolations.length;
  const compliantRequirements =
    dashboard.compliantRequirements || Math.max(Math.round((dashboard.complianceRate / 100) * totalRequirements), 0);
  const pendingRequirements =
    dashboard.pendingRequirements || Math.max(totalRequirements - compliantRequirements - nonCompliantRequirements, 0);

  return {
    ...dashboard,
    upcomingDeadlines,
    recentViolations,
    criticalRequirements,
    overdueItems: dashboard.overdueItems || dashboard.overdueAssets,
    violations: dashboard.violations || recentViolations.length,
    auditActivity,
    totalRequirements,
    compliantRequirements,
    nonCompliantRequirements,
    pendingRequirements,
    lastAuditDate: dashboard.lastAuditDate || auditActivity[0]?.performedAt || new Date().toISOString(),
    nextAuditDate: dashboard.nextAuditDate || upcomingDeadlines[0]?.dueDate,
  };
};

// Mock data for fallback
const mockRequirements: ComplianceRequirement[] = [
  {
    id: '1',
    name: 'Vehicle Safety Inspection',
    description: 'Annual safety inspection required for all fleet vehicles',
    regulatoryBody: 'Department of Transportation',
    regulatoryAuthority: 'Department of Transportation',
    standard: 'DOT-VSI-2024',
    assetCategories: ['Vehicle'],
    category: 'Vehicle',
    workOrderTypes: ['Safety', 'Regulatory'],
    frequency: 'Annually',
    frequencyDays: 365,
    isActive: true,
    isMandatory: true,
    gracePeriodDays: 30,
    requiredDocumentation: ['Safety Certificate', 'Inspection Report'],
    requiredDocuments: ['Safety Certificate', 'Inspection Report'],
    inspectionRequired: true,
    certificationRequired: true,
    auditTrailRequired: true,
    riskLevel: 'Critical',
    penaltyDescription: 'Fine for operating without valid safety inspection; Vehicle shutdown until compliance is achieved',
    penalties: [
      {
        type: 'Fine',
        amount: 500,
        currency: 'USD',
        description: 'Fine for operating without valid safety inspection',
        severity: 'High'
      },
      {
        type: 'Shutdown',
        description: 'Vehicle shutdown until compliance is achieved',
        severity: 'Critical'
      }
    ],
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z'
  },
  {
    id: '2',
    name: 'Fire Safety System Certification',
    description: 'Annual fire safety system certification for building compliance',
    regulatoryBody: 'Fire Department',
    regulatoryAuthority: 'Fire Department',
    standard: 'NFPA 10-2024',
    assetCategories: ['Fire Safety', 'HVAC'],
    category: 'Fire Safety',
    workOrderTypes: ['Safety', 'Regulatory'],
    frequency: 'Annually',
    frequencyDays: 365,
    isActive: true,
    isMandatory: true,
    gracePeriodDays: 15,
    requiredDocumentation: ['Fire Safety Certificate', 'System Test Report'],
    requiredDocuments: ['Fire Safety Certificate', 'System Test Report'],
    inspectionRequired: true,
    certificationRequired: true,
    auditTrailRequired: true,
    riskLevel: 'Critical',
    penaltyDescription: 'Fine for non-compliant fire safety systems',
    penalties: [
      {
        type: 'Fine',
        amount: 2000,
        currency: 'USD',
        description: 'Fine for non-compliant fire safety systems',
        severity: 'Critical'
      }
    ],
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z'
  },
  {
    id: '3',
    name: 'Elevator Safety Inspection',
    description: 'Monthly elevator safety inspections required by state law',
    regulatoryBody: 'State Elevator Safety Board',
    regulatoryAuthority: 'State Elevator Safety Board',
    standard: 'ASME A17.1-2024',
    assetCategories: ['Elevator'],
    category: 'Elevator',
    workOrderTypes: ['Safety', 'Regulatory'],
    frequency: 'Monthly',
    frequencyDays: 30,
    isActive: true,
    isMandatory: true,
    gracePeriodDays: 7,
    requiredDocumentation: ['Monthly Inspection Report', 'Safety Certificate'],
    requiredDocuments: ['Monthly Inspection Report', 'Safety Certificate'],
    inspectionRequired: true,
    certificationRequired: false,
    auditTrailRequired: true,
    riskLevel: 'Critical',
    penaltyDescription: 'Elevator shutdown until inspection is completed',
    penalties: [
      {
        type: 'Shutdown',
        description: 'Elevator shutdown until inspection is completed',
        severity: 'Critical'
      }
    ],
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z'
  }
];

const mockComplianceStatuses: ComplianceStatus[] = [
  {
    id: '1',
    assetId: 'FLEET-001',
    assetName: 'Ford Transit Van 001',
    assetType: 'Vehicle',
    requirementId: '1',
    requirement: mockRequirements[0],
    status: 'At_Risk',
    complianceStatus: 'At Risk',
    lastComplianceDate: '2023-11-15',
    lastCheckDate: '2023-11-15',
    nextDueDate: '2024-11-15',
    dueDate: '2024-11-15',
    daysUntilDue: 30,
    lastInspectionId: 'INS-2023-001',
    lastCertificationNumber: 'DOT-CERT-2023-001',
    certificateValidUntil: '2024-11-15',
    responsiblePersonId: 'USER-001',
    responsiblePersonName: 'John Smith',
    title: 'Vehicle Safety Inspection',
    priority: 'Medium',
    notes: 'Inspection due soon, schedule appointment',
    riskLevel: 'Medium',
    updatedAt: '2024-10-15T00:00:00Z'
  },
  {
    id: '2',
    assetId: 'FIRE-001',
    assetName: 'Building A Fire Safety System',
    assetType: 'Fire Safety',
    requirementId: '2',
    requirement: mockRequirements[1],
    status: 'Compliant',
    complianceStatus: 'Compliant',
    lastComplianceDate: '2024-01-15',
    lastCheckDate: '2024-01-15',
    nextDueDate: '2025-01-15',
    dueDate: '2025-01-15',
    daysUntilDue: 90,
    lastInspectionId: 'INS-2024-002',
    lastCertificationNumber: 'FIRE-CERT-2024-001',
    certificateValidUntil: '2025-01-15',
    responsiblePersonId: 'USER-002',
    responsiblePersonName: 'Sarah Davis',
    title: 'Fire Safety System Certification',
    priority: 'Low',
    notes: 'System recently inspected and certified',
    riskLevel: 'Low',
    updatedAt: '2024-01-15T00:00:00Z'
  },
  {
    id: '3',
    assetId: 'ELEV-001',
    assetName: 'Main Elevator A1',
    assetType: 'Elevator',
    requirementId: '3',
    requirement: mockRequirements[2],
    status: 'Overdue',
    complianceStatus: 'Overdue',
    lastComplianceDate: '2024-09-01',
    lastCheckDate: '2024-09-01',
    nextDueDate: '2024-10-01',
    dueDate: '2024-10-01',
    daysUntilDue: -15,
    lastInspectionId: 'INS-2024-003',
    responsiblePersonId: 'USER-003',
    responsiblePersonName: 'Mike Johnson',
    title: 'Elevator Safety Inspection',
    priority: 'Critical',
    notes: 'URGENT: Inspection overdue, elevator at risk of shutdown',
    riskLevel: 'Critical',
    updatedAt: '2024-10-01T00:00:00Z'
  }
];

class RegulatoryComplianceService {
  private useBackend = true; // Set to false to force mock data

  async getAllRequirements(): Promise<ComplianceRequirement[]> {
    return this.getComplianceRequirements();
  }

  async getAllComplianceStatuses(assetId?: string): Promise<ComplianceStatus[]> {
    return this.getComplianceStatuses(assetId);
  }

  async getComplianceReports(): Promise<ComplianceReport[]> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<ComplianceReport[]>('/compliance/reports');
        return response.map((report) => enrichReport(report));
      }
    } catch (error) {
      console.warn('Backend unavailable, using derived mock reports:', error);
    }

    return mockComplianceStatuses.map((status) =>
      enrichReport(
        {
          id: `REP-${status.id}`,
          assetId: status.assetId,
          requirementId: status.requirementId,
          title: `${status.requirement.name} Report`,
          description: status.notes || `${status.requirement.name} compliance report`,
          type: 'Compliance',
          reportDate: status.lastComplianceDate || status.updatedAt,
          generatedDate: status.lastComplianceDate || status.updatedAt,
          periodStart: status.lastComplianceDate || status.updatedAt,
          periodEnd: status.certificateValidUntil || status.nextDueDate,
          reportedBy: status.responsiblePersonName || 'System',
          status: status.status === 'Non_Compliant' || status.status === 'Overdue' ? 'Non_Compliant' : 'Compliant',
          evidence: [],
          notes: status.notes,
          inspectionId: status.lastInspectionId,
          certificateNumber: status.lastCertificationNumber,
          validUntil: status.certificateValidUntil,
          approvedBy: status.status === 'Compliant' ? 'System' : undefined,
          approvedAt: status.status === 'Compliant' ? status.updatedAt : undefined,
        },
        status.requirement.name
      )
    );
  }

  async getAuditLogs(assetId?: string, requirementId?: string): Promise<ComplianceAuditLog[]> {
    return this.getAuditLog(assetId, requirementId);
  }

  async generateReport(report: ComplianceReportGeneration): Promise<ComplianceReport> {
    const requirementId = report.requirementIds[0] || mockRequirements[0]?.id || '';
    const assetId = report.assetIds[0] || mockComplianceStatuses[0]?.assetId || '';
    const requirement = (await this.getComplianceRequirementById(requirementId)) || mockRequirements[0];

    const generatedReport = await this.submitComplianceReport({
      assetId,
      requirementId,
      title: report.title,
      description: report.description,
      type: report.format.toUpperCase(),
      reportDate: new Date().toISOString(),
      generatedDate: new Date().toISOString(),
      periodStart: report.dateRange.start,
      periodEnd: report.dateRange.end,
      reportedBy: 'System',
      status: 'Compliant',
      evidence: [],
      notes: report.description,
      validUntil: report.dateRange.end,
    });

    return enrichReport(generatedReport, requirement?.name);
  }

  async getComplianceRequirements(): Promise<ComplianceRequirement[]> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<ComplianceRequirement[]>('/compliance/requirements');
        return response.map((requirement) => enrichRequirement(requirement));
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    return mockRequirements.map((requirement) => enrichRequirement(requirement));
  }

  async getComplianceRequirementById(id: string): Promise<ComplianceRequirement | null> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<ComplianceRequirement>(`/compliance/requirements/${id}`);
        return enrichRequirement(response);
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    const requirement = mockRequirements.find(r => r.id === id);
    return requirement ? enrichRequirement(requirement) : null;
  }

  async getComplianceStatuses(assetId?: string): Promise<ComplianceStatus[]> {
    try {
      if (this.useBackend) {
        const queryString = assetId ? `?assetId=${assetId}` : '';
        const response = await apiService.request<ComplianceStatus[]>(`/compliance/statuses${queryString}`);
        return response.map((status) => enrichStatus(status));
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    if (assetId) {
      return mockComplianceStatuses.filter(s => s.assetId === assetId).map((status) => enrichStatus(status));
    }
    
    return mockComplianceStatuses.map((status) => enrichStatus(status));
  }

  async getComplianceStatusById(id: string): Promise<ComplianceStatus | null> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<ComplianceStatus>(`/compliance/statuses/${id}`);
        return enrichStatus(response);
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    const status = mockComplianceStatuses.find(s => s.id === id);
    return status ? enrichStatus(status) : null;
  }

  async getComplianceDashboard(): Promise<ComplianceDashboard> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<ComplianceDashboard>('/compliance/dashboard');
        return enrichDashboard(response);
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Calculate dashboard data from mock data
    const totalAssets = mockComplianceStatuses.length;
    const compliantAssets = mockComplianceStatuses.filter(s => s.status === 'Compliant').length;
    const atRiskAssets = mockComplianceStatuses.filter(s => s.status === 'At_Risk').length;
    const overdueAssets = mockComplianceStatuses.filter(s => s.status === 'Overdue').length;
    const complianceRate = totalAssets > 0 ? (compliantAssets / totalAssets) * 100 : 0;
    
    const upcomingDeadlines = mockComplianceStatuses
      .filter(s => s.daysUntilDue <= 30 && s.daysUntilDue > 0)
      .sort((a, b) => a.daysUntilDue - b.daysUntilDue);
    
    const recentViolations = mockComplianceStatuses
      .filter(s => s.status === 'Overdue' || s.status === 'Non_Compliant')
      .sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime());
    
    const criticalRequirements = mockRequirements.filter(r => r.isMandatory);
    const compliantRequirements = mockComplianceStatuses.filter((status) => status.status === 'Compliant').length;
    
    return enrichDashboard({
      totalAssets,
      compliantAssets,
      atRiskAssets,
      overdueAssets,
      overdueItems: overdueAssets,
      violations: recentViolations.length,
      complianceRate,
      upcomingDeadlines,
      recentViolations,
      criticalRequirements,
      complianceByCategory: [
        { category: 'Vehicle', compliant: 0, total: 1 },
        { category: 'Fire Safety', compliant: 1, total: 1 },
        { category: 'Elevator', compliant: 0, total: 1 }
      ],
      complianceByRequirement: mockRequirements.map(req => ({
        requirement: req.name,
        compliant: mockComplianceStatuses.filter(s => s.requirementId === req.id && s.status === 'Compliant').length,
        total: mockComplianceStatuses.filter(s => s.requirementId === req.id).length
      })),
      auditActivity: [],
      totalRequirements: mockRequirements.length,
      compliantRequirements,
      nonCompliantRequirements: recentViolations.length,
      pendingRequirements: Math.max(mockRequirements.length - compliantRequirements - recentViolations.length, 0),
      lastAuditDate: new Date().toISOString(),
      nextAuditDate: upcomingDeadlines[0]?.nextDueDate,
    });
  }

  async updateComplianceStatus(id: string, updates: Partial<ComplianceStatus>): Promise<ComplianceStatus> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<ComplianceStatus>(`/compliance/statuses/${id}`, {
          method: 'PUT',
          body: JSON.stringify(updates),
        });
        return enrichStatus(response);
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Mock update for fallback
    const statusIndex = mockComplianceStatuses.findIndex(s => s.id === id);
    if (statusIndex === -1) {
      throw new Error('Compliance status not found');
    }
    
    mockComplianceStatuses[statusIndex] = {
      ...mockComplianceStatuses[statusIndex],
      ...updates,
      updatedAt: new Date().toISOString()
    };
    
    return enrichStatus(mockComplianceStatuses[statusIndex]);
  }

  async submitComplianceReport(report: Omit<ComplianceReport, 'id' | 'approvedAt' | 'approvedBy'>): Promise<ComplianceReport> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<ComplianceReport>('/compliance/reports', {
          method: 'POST',
          body: JSON.stringify(report),
        });
        return enrichReport(response);
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Mock report creation
    const newReport: ComplianceReport = {
      ...report,
      id: `REP-${Date.now()}`,
      title: report.title || 'Compliance Report',
      description: report.description || report.notes || 'Compliance submission',
      type: report.type || 'Compliance',
      generatedDate: report.generatedDate || report.reportDate,
      periodStart: report.periodStart || report.reportDate,
      periodEnd: report.periodEnd || report.validUntil || report.reportDate,
      approvedAt: report.status === 'Compliant' ? new Date().toISOString() : undefined,
      approvedBy: report.status === 'Compliant' ? 'System' : undefined
    };
    
    // Update compliance status
    const statusIndex = mockComplianceStatuses.findIndex(s => 
      s.assetId === report.assetId && s.requirementId === report.requirementId
    );
    
    if (statusIndex > -1) {
      mockComplianceStatuses[statusIndex].status = report.status;
      mockComplianceStatuses[statusIndex].lastComplianceDate = report.reportDate;
      mockComplianceStatuses[statusIndex].lastCertificationNumber = report.certificateNumber;
      mockComplianceStatuses[statusIndex].certificateValidUntil = report.validUntil;
      mockComplianceStatuses[statusIndex].updatedAt = new Date().toISOString();
      
      // Calculate next due date
      const requirement = mockRequirements.find(r => r.id === report.requirementId);
      if (requirement) {
        const nextDue = new Date(report.reportDate);
        nextDue.setDate(nextDue.getDate() + requirement.frequencyDays);
        mockComplianceStatuses[statusIndex].nextDueDate = nextDue.toISOString();
        
        const today = new Date();
        const daysUntilDue = Math.ceil((nextDue.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
        mockComplianceStatuses[statusIndex].daysUntilDue = daysUntilDue;
        
        // Update risk level based on days until due
        if (daysUntilDue < 0) {
          mockComplianceStatuses[statusIndex].riskLevel = 'Critical';
        } else if (daysUntilDue <= 7) {
          mockComplianceStatuses[statusIndex].riskLevel = 'High';
        } else if (daysUntilDue <= 30) {
          mockComplianceStatuses[statusIndex].riskLevel = 'Medium';
        } else {
          mockComplianceStatuses[statusIndex].riskLevel = 'Low';
        }
      }
    }
    
    return enrichReport(newReport);
  }

  async getAuditLog(assetId?: string, requirementId?: string): Promise<ComplianceAuditLog[]> {
    try {
      if (this.useBackend) {
        const params: any = {};
        if (assetId) params.assetId = assetId;
        if (requirementId) params.requirementId = requirementId;
        
        const queryParams = new URLSearchParams(params).toString();
        const queryString = queryParams ? `?${queryParams}` : '';
        const response = await apiService.request<ComplianceAuditLog[]>(`/compliance/audit-log${queryString}`);
        return response.map((log) => enrichAuditLog(log));
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Return empty audit log for mock
    return [];
  }

  async getUpcomingDeadlines(days: number = 30): Promise<ComplianceStatus[]> {
    const statuses = await this.getComplianceStatuses();
    return statuses
      .filter(s => s.daysUntilDue <= days && s.daysUntilDue > 0)
      .sort((a, b) => a.daysUntilDue - b.daysUntilDue);
  }

  async getOverdueItems(): Promise<ComplianceStatus[]> {
    const statuses = await this.getComplianceStatuses();
    return statuses
      .filter(s => s.status === 'Overdue' || s.daysUntilDue < 0)
      .sort((a, b) => a.daysUntilDue - b.daysUntilDue);
  }

  async validateWorkOrderCompliance(workOrderId: string, assetId: string, workOrderType: string): Promise<{
    isCompliant: boolean;
    violations: ComplianceStatus[];
    warnings: ComplianceStatus[];
    recommendations: string[];
  }> {
    try {
      if (this.useBackend) {
        const response = await apiService.request<{
          isCompliant: boolean;
          violations: ComplianceStatus[];
          warnings: ComplianceStatus[];
          recommendations: string[];
        }>('/compliance/validate-work-order', {
          method: 'POST',
          body: JSON.stringify({
            workOrderId,
            assetId,
            workOrderType
          }),
        });
        return response;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Mock validation
    const assetStatuses = await this.getComplianceStatuses(assetId);
    const violations = assetStatuses.filter(s => s.status === 'Overdue' || s.status === 'Non_Compliant');
    const warnings = assetStatuses.filter(s => s.status === 'At_Risk' && s.daysUntilDue <= 30);
    
    const recommendations: string[] = [];
    if (violations.length > 0) {
      recommendations.push('Resolve all compliance violations before proceeding with work order');
    }
    if (warnings.length > 0) {
      recommendations.push('Consider updating compliance items that are due soon');
    }
    
    return {
      isCompliant: violations.length === 0,
      violations,
      warnings,
      recommendations
    };
  }

  getRiskLevelColor(riskLevel: string): string {
    switch (riskLevel) {
      case 'Low': return 'bg-green-100 text-green-800';
      case 'Medium': return 'bg-yellow-100 text-yellow-800';
      case 'High': return 'bg-orange-100 text-orange-800';
      case 'Critical': return 'bg-red-100 text-red-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'Compliant': return 'bg-green-100 text-green-800';
      case 'At_Risk': return 'bg-yellow-100 text-yellow-800';
      case 'Overdue': return 'bg-red-100 text-red-800';
      case 'Non_Compliant': return 'bg-red-100 text-red-800';
      case 'Pending': return 'bg-blue-100 text-blue-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  }
}

export const regulatoryComplianceService = new RegulatoryComplianceService();
