/**
 * Performance Analytics Service
 * 
 * Handles performance analytics data for quality control including:
 * - Inspection trends and metrics over time
 * - Inspector performance statistics
 * - Asset quality history and patterns
 * - Compliance trends and analytics
 * - Quality scoring patterns
 */

export interface QualityTrend {
  date: string;
  totalInspections: number;
  passedInspections: number;
  failedInspections: number;
  averageScore: number;
  complianceRate: number;
}

export interface InspectorPerformance {
  inspectorId: string;
  inspectorName: string;
  totalInspections: number;
  averageScore: number;
  passRate: number;
  averageTimeToComplete: number; // in minutes
  specializations: string[];
  certificationLevel: string;
  monthlyTrends: {
    month: string;
    inspections: number;
    averageScore: number;
    passRate: number;
  }[];
}

export interface AssetQualityMetrics {
  assetId: string;
  assetName: string;
  assetType: string;
  totalInspections: number;
  averageScore: number;
  passRate: number;
  lastInspectionDate: string;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  trendDirection: 'Improving' | 'Stable' | 'Declining';
  commonIssues: {
    issue: string;
    frequency: number;
    severity: 'Low' | 'Medium' | 'High';
  }[];
  qualityHistory: {
    date: string;
    score: number;
    result: 'Pass' | 'Fail';
    inspectorName: string;
  }[];
}

export interface ComplianceTrend {
  requirementId: string;
  requirementName: string;
  category: string;
  totalAssets: number;
  compliantAssets: number;
  complianceRate: number;
  upcomingDeadlines: number;
  overdueItems: number;
  trend: {
    month: string;
    complianceRate: number;
    violations: number;
  }[];
}

export interface PerformanceDashboardData {
  overallMetrics: {
    totalInspections: number;
    averageScore: number;
    complianceRate: number;
    activeInspectors: number;
    criticalIssues: number;
  };
  qualityTrends: QualityTrend[];
  topPerformers: InspectorPerformance[];
  assetPerformance: AssetQualityMetrics[];
  complianceTrends: ComplianceTrend[];
  alerts: {
    id: string;
    type: 'quality_decline' | 'compliance_risk' | 'inspector_performance' | 'asset_risk';
    severity: 'Low' | 'Medium' | 'High' | 'Critical';
    title: string;
    description: string;
    actionRequired: boolean;
    dueDate?: string;
  }[];
}

export interface DrillDownData {
  inspections: {
    id: string;
    workOrderId: string;
    assetName: string;
    inspectorName: string;
    date: string;
    score: number;
    result: 'Pass' | 'Fail';
    duration: number;
    criticalIssues: number;
  }[];
  filters: {
    dateRange: string;
    inspectors: string[];
    assets: string[];
    results: string[];
  };
}

class PerformanceAnalyticsService {
  private readonly API_BASE_URL = '/api/quality-control/analytics';

  async getPerformanceDashboard(dateRange?: string): Promise<PerformanceDashboardData> {
    try {
      const response = await fetch(`${this.API_BASE_URL}/dashboard?dateRange=${dateRange || '30d'}`);
      
      if (response.ok) {
        return await response.json();
      }
      
      // Fallback to mock data if API is not available
      console.log('API not available, using mock performance analytics data');
      return this.getMockPerformanceDashboard();
    } catch (error) {
      console.error('Error fetching performance dashboard:', error);
      return this.getMockPerformanceDashboard();
    }
  }

  async getInspectorPerformance(inspectorId?: string, dateRange?: string): Promise<InspectorPerformance[]> {
    try {
      const params = new URLSearchParams();
      if (inspectorId) params.append('inspectorId', inspectorId);
      if (dateRange) params.append('dateRange', dateRange);

      const response = await fetch(`${this.API_BASE_URL}/inspector-performance?${params.toString()}`);
      
      if (response.ok) {
        return await response.json();
      }
      
      // Fallback to mock data
      console.log('API not available, using mock inspector performance data');
      return this.getMockInspectorPerformance();
    } catch (error) {
      console.error('Error fetching inspector performance:', error);
      return this.getMockInspectorPerformance();
    }
  }

  async getAssetQualityMetrics(assetId?: string): Promise<AssetQualityMetrics[]> {
    try {
      const params = new URLSearchParams();
      if (assetId) params.append('assetId', assetId);

      const response = await fetch(`${this.API_BASE_URL}/asset-performance?${params.toString()}`);
      
      if (response.ok) {
        return await response.json();
      }
      
      // Fallback to mock data
      console.log('API not available, using mock asset quality metrics');
      return this.getMockAssetQualityMetrics();
    } catch (error) {
      console.error('Error fetching asset quality metrics:', error);
      return this.getMockAssetQualityMetrics();
    }
  }

  async getComplianceTrends(requirementId?: string): Promise<ComplianceTrend[]> {
    try {
      const params = new URLSearchParams();
      if (requirementId) params.append('requirementId', requirementId);

      const response = await fetch(`${this.API_BASE_URL}/compliance-trends?${params.toString()}`);
      
      if (response.ok) {
        return await response.json();
      }
      
      // Fallback to mock data
      console.log('API not available, using mock compliance trends');
      return this.getMockComplianceTrends();
    } catch (error) {
      console.error('Error fetching compliance trends:', error);
      return this.getMockComplianceTrends();
    }
  }

  async getDrillDownData(filters: {
    dateRange?: string;
    inspectorId?: string;
    assetId?: string;
    result?: string;
  }): Promise<DrillDownData> {
    try {
      const params = new URLSearchParams();
      Object.entries(filters).forEach(([key, value]) => {
        if (value) params.append(key, value);
      });

      const response = await fetch(`${this.API_BASE_URL}/drill-down?${params.toString()}`);
      
      if (response.ok) {
        return await response.json();
      }
      
      // Fallback to mock data
      console.log('API not available, using mock drill-down data');
      return this.getMockDrillDownData();
    } catch (error) {
      console.error('Error fetching drill-down data:', error);
      return this.getMockDrillDownData();
    }
  }

  // Utility methods for data analysis
  calculateTrendDirection(values: number[]): 'Improving' | 'Stable' | 'Declining' {
    if (values.length < 2) return 'Stable';
    
    const recent = values.slice(-3);
    const earlier = values.slice(0, values.length - 3);
    
    const recentAvg = recent.reduce((sum, val) => sum + val, 0) / recent.length;
    const earlierAvg = earlier.reduce((sum, val) => sum + val, 0) / earlier.length;
    
    const difference = recentAvg - earlierAvg;
    
    if (difference > 5) return 'Improving';
    if (difference < -5) return 'Declining';
    return 'Stable';
  }

  getRiskLevelFromScore(score: number): 'Low' | 'Medium' | 'High' | 'Critical' {
    if (score >= 90) return 'Low';
    if (score >= 80) return 'Medium';
    if (score >= 70) return 'High';
    return 'Critical';
  }

  // Mock data methods
  private getMockPerformanceDashboard(): PerformanceDashboardData {
    return {
      overallMetrics: {
        totalInspections: 247,
        averageScore: 87.3,
        complianceRate: 94.2,
        activeInspectors: 8,
        criticalIssues: 3
      },
      qualityTrends: [
        { date: '2024-01-01', totalInspections: 45, passedInspections: 42, failedInspections: 3, averageScore: 89.2, complianceRate: 93.3 },
        { date: '2024-01-02', totalInspections: 38, passedInspections: 36, failedInspections: 2, averageScore: 91.1, complianceRate: 94.7 },
        { date: '2024-01-03', totalInspections: 41, passedInspections: 38, failedInspections: 3, averageScore: 88.5, complianceRate: 92.7 },
        { date: '2024-01-04', totalInspections: 52, passedInspections: 49, failedInspections: 3, averageScore: 90.3, complianceRate: 94.2 },
        { date: '2024-01-05', totalInspections: 48, passedInspections: 46, failedInspections: 2, averageScore: 92.1, complianceRate: 95.8 },
        { date: '2024-01-06', totalInspections: 23, passedInspections: 22, failedInspections: 1, averageScore: 88.9, complianceRate: 95.7 }
      ],
      topPerformers: this.getMockInspectorPerformance().slice(0, 3),
      assetPerformance: this.getMockAssetQualityMetrics().slice(0, 5),
      complianceTrends: this.getMockComplianceTrends(),
      alerts: [
        {
          id: '1',
          type: 'compliance_risk',
          severity: 'High',
          title: 'Fire Safety Compliance Due Soon',
          description: '12 assets have fire safety inspections due within 7 days',
          actionRequired: true,
          dueDate: '2024-01-28'
        },
        {
          id: '2',
          type: 'quality_decline',
          severity: 'Medium',
          title: 'HVAC System Performance Declining',
          description: 'Building A HVAC systems showing 15% decrease in quality scores over last month',
          actionRequired: true
        },
        {
          id: '3',
          type: 'asset_risk',
          severity: 'Critical',
          title: 'Generator Requires Immediate Attention',
          description: 'Backup Generator 2 failed last 2 inspections with critical safety issues',
          actionRequired: true,
          dueDate: '2024-01-22'
        }
      ]
    };
  }

  private getMockInspectorPerformance(): InspectorPerformance[] {
    return [
      {
        inspectorId: '1',
        inspectorName: 'Emma Wilson',
        totalInspections: 78,
        averageScore: 94.2,
        passRate: 96.2,
        averageTimeToComplete: 45,
        specializations: ['HVAC', 'Electrical', 'Fire Safety'],
        certificationLevel: 'Senior',
        monthlyTrends: [
          { month: '2023-10', inspections: 25, averageScore: 93.1, passRate: 96.0 },
          { month: '2023-11', inspections: 27, averageScore: 94.8, passRate: 96.3 },
          { month: '2023-12', inspections: 26, averageScore: 94.7, passRate: 96.2 }
        ]
      },
      {
        inspectorId: '2',
        inspectorName: 'David Chen',
        totalInspections: 65,
        averageScore: 91.8,
        passRate: 93.8,
        averageTimeToComplete: 52,
        specializations: ['Electrical', 'Mechanical', 'Safety'],
        certificationLevel: 'Senior',
        monthlyTrends: [
          { month: '2023-10', inspections: 22, averageScore: 90.5, passRate: 93.2 },
          { month: '2023-11', inspections: 21, averageScore: 92.1, passRate: 94.1 },
          { month: '2023-12', inspections: 22, averageScore: 92.8, passRate: 94.1 }
        ]
      },
      {
        inspectorId: '3',
        inspectorName: 'Sarah Johnson',
        totalInspections: 56,
        averageScore: 89.4,
        passRate: 91.1,
        averageTimeToComplete: 48,
        specializations: ['Plumbing', 'HVAC', 'General'],
        certificationLevel: 'Intermediate',
        monthlyTrends: [
          { month: '2023-10', inspections: 19, averageScore: 88.2, passRate: 89.5 },
          { month: '2023-11', inspections: 18, averageScore: 89.8, passRate: 91.7 },
          { month: '2023-12', inspections: 19, averageScore: 90.2, passRate: 92.1 }
        ]
      },
      {
        inspectorId: '4',
        inspectorName: 'Michael Brown',
        totalInspections: 48,
        averageScore: 92.6,
        passRate: 95.8,
        averageTimeToComplete: 41,
        specializations: ['Fire Safety', 'Emergency Systems'],
        certificationLevel: 'Senior',
        monthlyTrends: [
          { month: '2023-10', inspections: 16, averageScore: 91.8, passRate: 95.0 },
          { month: '2023-11', inspections: 16, averageScore: 93.1, passRate: 96.3 },
          { month: '2023-12', inspections: 16, averageScore: 93.0, passRate: 96.1 }
        ]
      }
    ];
  }

  private getMockAssetQualityMetrics(): AssetQualityMetrics[] {
    return [
      {
        assetId: '1',
        assetName: 'Building A - HVAC Unit 1',
        assetType: 'HVAC',
        totalInspections: 24,
        averageScore: 92.1,
        passRate: 95.8,
        lastInspectionDate: '2024-01-20',
        riskLevel: 'Low',
        trendDirection: 'Stable',
        commonIssues: [
          { issue: 'Filter replacement needed', frequency: 3, severity: 'Low' },
          { issue: 'Calibration drift', frequency: 2, severity: 'Medium' }
        ],
        qualityHistory: [
          { date: '2024-01-20', score: 95, result: 'Pass', inspectorName: 'Emma Wilson' },
          { date: '2024-01-06', score: 89, result: 'Pass', inspectorName: 'David Chen' },
          { date: '2023-12-23', score: 92, result: 'Pass', inspectorName: 'Emma Wilson' },
          { date: '2023-12-09', score: 91, result: 'Pass', inspectorName: 'Sarah Johnson' },
          { date: '2023-11-25', score: 94, result: 'Pass', inspectorName: 'Emma Wilson' }
        ]
      },
      {
        assetId: '2',
        assetName: 'Backup Generator 1',
        assetType: 'Generator',
        totalInspections: 18,
        averageScore: 78.3,
        passRate: 72.2,
        lastInspectionDate: '2024-01-19',
        riskLevel: 'High',
        trendDirection: 'Declining',
        commonIssues: [
          { issue: 'Load testing failures', frequency: 5, severity: 'High' },
          { issue: 'Battery issues', frequency: 3, severity: 'Medium' },
          { issue: 'Fuel system problems', frequency: 2, severity: 'High' }
        ],
        qualityHistory: [
          { date: '2024-01-19', score: 72, result: 'Fail', inspectorName: 'David Chen' },
          { date: '2024-01-05', score: 68, result: 'Fail', inspectorName: 'Michael Brown' },
          { date: '2023-12-22', score: 82, result: 'Pass', inspectorName: 'David Chen' },
          { date: '2023-12-08', score: 75, result: 'Fail', inspectorName: 'Emma Wilson' },
          { date: '2023-11-24', score: 88, result: 'Pass', inspectorName: 'David Chen' }
        ]
      },
      {
        assetId: '3',
        assetName: 'Building C - Fire Safety System',
        assetType: 'Fire Safety',
        totalInspections: 16,
        averageScore: 96.8,
        passRate: 100.0,
        lastInspectionDate: '2024-01-18',
        riskLevel: 'Low',
        trendDirection: 'Improving',
        commonIssues: [
          { issue: 'Minor sensor drift', frequency: 1, severity: 'Low' }
        ],
        qualityHistory: [
          { date: '2024-01-18', score: 98, result: 'Pass', inspectorName: 'Michael Brown' },
          { date: '2024-01-04', score: 97, result: 'Pass', inspectorName: 'Michael Brown' },
          { date: '2023-12-21', score: 96, result: 'Pass', inspectorName: 'Emma Wilson' },
          { date: '2023-12-07', score: 95, result: 'Pass', inspectorName: 'Michael Brown' },
          { date: '2023-11-23', score: 97, result: 'Pass', inspectorName: 'Michael Brown' }
        ]
      }
    ];
  }

  private getMockComplianceTrends(): ComplianceTrend[] {
    return [
      {
        requirementId: '1',
        requirementName: 'Fire Safety Annual Inspection',
        category: 'Fire Safety',
        totalAssets: 45,
        compliantAssets: 42,
        complianceRate: 93.3,
        upcomingDeadlines: 8,
        overdueItems: 1,
        trend: [
          { month: '2023-10', complianceRate: 91.1, violations: 4 },
          { month: '2023-11', complianceRate: 92.2, violations: 3 },
          { month: '2023-12', complianceRate: 93.3, violations: 3 }
        ]
      },
      {
        requirementId: '2',
        requirementName: 'HVAC Efficiency Standards',
        category: 'Energy Efficiency',
        totalAssets: 28,
        compliantAssets: 26,
        complianceRate: 92.9,
        upcomingDeadlines: 3,
        overdueItems: 0,
        trend: [
          { month: '2023-10', complianceRate: 89.3, violations: 3 },
          { month: '2023-11', complianceRate: 91.1, violations: 2 },
          { month: '2023-12', complianceRate: 92.9, violations: 2 }
        ]
      },
      {
        requirementId: '3',
        requirementName: 'Electrical Safety Compliance',
        category: 'Electrical Safety',
        totalAssets: 67,
        compliantAssets: 64,
        complianceRate: 95.5,
        upcomingDeadlines: 5,
        overdueItems: 2,
        trend: [
          { month: '2023-10', complianceRate: 94.0, violations: 4 },
          { month: '2023-11', complianceRate: 94.8, violations: 3 },
          { month: '2023-12', complianceRate: 95.5, violations: 3 }
        ]
      }
    ];
  }

  private getMockDrillDownData(): DrillDownData {
    return {
      inspections: [
        {
          id: '1',
          workOrderId: 'WO-2024-001',
          assetName: 'Building A - HVAC Unit 1',
          inspectorName: 'Emma Wilson',
          date: '2024-01-20',
          score: 95,
          result: 'Pass',
          duration: 45,
          criticalIssues: 0
        },
        {
          id: '2',
          workOrderId: 'WO-2024-002',
          assetName: 'Backup Generator 1',
          inspectorName: 'David Chen',
          date: '2024-01-19',
          score: 72,
          result: 'Fail',
          duration: 68,
          criticalIssues: 2
        },
        {
          id: '3',
          workOrderId: 'WO-2024-004',
          assetName: 'Building C - Fire Safety',
          inspectorName: 'Michael Brown',
          date: '2024-01-18',
          score: 98,
          result: 'Pass',
          duration: 38,
          criticalIssues: 0
        }
      ],
      filters: {
        dateRange: 'Last 30 Days',
        inspectors: ['Emma Wilson', 'David Chen', 'Sarah Johnson', 'Michael Brown'],
        assets: ['Building A - HVAC Unit 1', 'Backup Generator 1', 'Building C - Fire Safety'],
        results: ['Pass', 'Fail']
      }
    };
  }
}

export const performanceAnalyticsService = new PerformanceAnalyticsService();
export default performanceAnalyticsService;