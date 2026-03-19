'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Separator } from '@/components/ui/separator';
import { 
  AlertTriangle, 
  Search, 
  Clock, 
  TrendingDown, 
  FileText,
  ChevronRight,
  CheckCircle,
  XCircle,
  Download,
  Filter
} from 'lucide-react';
import { BaseLineChart, BaseBarChart } from '../charts/BaseCharts';
import { AnalyticsErrorDisplay, AnalyticsLoadingSkeleton } from '../common/ErrorHandling';
import { useRootCauseAnalysis } from '../../../hooks/useAssetAnalytics';
import { cn } from '@/lib/utils';

// #region Types

interface RootCause {
  id: string;
  category: string;
  description: string;
  probability: number;
  impact: number;
  riskScore: number;
  evidence: Evidence[];
  recommendations: Recommendation[];
  status: 'identified' | 'investigating' | 'resolved' | 'dismissed';
}

interface Evidence {
  id: string;
  type: 'sensor_data' | 'maintenance_log' | 'operator_report' | 'visual_inspection';
  description: string;
  timestamp: string;
  confidence: number;
  source: string;
}

interface Recommendation {
  id: string;
  title: string;
  description: string;
  priority: 'low' | 'medium' | 'high' | 'critical';
  estimatedCost: number;
  estimatedTime: string;
  expectedImpact: number;
}

interface FailurePattern {
  pattern: string;
  frequency: number;
  averageDowntime: number;
  totalCost: number;
  trend: 'increasing' | 'stable' | 'decreasing';
}

interface RootCauseAnalysisProps {
  assetId?: string;
  workOrderId?: string;
  failureType?: string;
  className?: string;
  onAnalysisComplete?: (analysis: any) => void;
}

// #endregion

// #region Mock Data

const mockRootCauses: RootCause[] = [
  {
    id: '1',
    category: 'Mechanical',
    description: 'Bearing wear due to insufficient lubrication',
    probability: 85,
    impact: 90,
    riskScore: 76.5,
    status: 'identified',
    evidence: [
      {
        id: '1',
        type: 'sensor_data',
        description: 'Vibration levels increased 300% over past 30 days',
        timestamp: '2024-01-15T10:30:00Z',
        confidence: 95,
        source: 'Vibration Sensor VIB-001',
      },
      {
        id: '2',
        type: 'maintenance_log',
        description: 'Last lubrication performed 6 months ago (overdue by 3 months)',
        timestamp: '2024-01-14T09:15:00Z',
        confidence: 100,
        source: 'CMMS Database',
      },
      {
        id: '3',
        type: 'visual_inspection',
        description: 'Grease contamination and metal particles observed',
        timestamp: '2024-01-13T14:20:00Z',
        confidence: 80,
        source: 'Technician Report TR-2024-001',
      },
    ],
    recommendations: [
      {
        id: '1',
        title: 'Immediate Bearing Replacement',
        description: 'Replace worn bearings and implement proper lubrication schedule',
        priority: 'critical',
        estimatedCost: 2500,
        estimatedTime: '8 hours',
        expectedImpact: 95,
      },
      {
        id: '2',
        title: 'Implement Predictive Maintenance',
        description: 'Install condition monitoring sensors for early detection',
        priority: 'medium',
        estimatedCost: 5000,
        estimatedTime: '2 days',
        expectedImpact: 80,
      },
    ],
  },
  {
    id: '2',
    category: 'Electrical',
    description: 'Motor overheating due to phase imbalance',
    probability: 70,
    impact: 75,
    riskScore: 52.5,
    status: 'investigating',
    evidence: [
      {
        id: '4',
        type: 'sensor_data',
        description: 'Temperature readings consistently 15°C above normal',
        timestamp: '2024-01-15T08:45:00Z',
        confidence: 90,
        source: 'Temperature Sensor TEMP-002',
      },
      {
        id: '5',
        type: 'operator_report',
        description: 'Unusual motor noise reported by operators',
        timestamp: '2024-01-14T16:30:00Z',
        confidence: 70,
        source: 'Operator John Smith',
      },
    ],
    recommendations: [
      {
        id: '3',
        title: 'Electrical System Audit',
        description: 'Comprehensive electrical system inspection and phase balance correction',
        priority: 'high',
        estimatedCost: 1500,
        estimatedTime: '4 hours',
        expectedImpact: 85,
      },
    ],
  },
  {
    id: '3',
    category: 'Process',
    description: 'Feed rate fluctuations causing quality issues',
    probability: 60,
    impact: 65,
    riskScore: 39,
    status: 'resolved',
    evidence: [
      {
        id: '6',
        type: 'sensor_data',
        description: 'Flow rate variance increased by 25%',
        timestamp: '2024-01-12T11:20:00Z',
        confidence: 85,
        source: 'Flow Sensor FLOW-003',
      },
    ],
    recommendations: [
      {
        id: '4',
        title: 'Flow Control Calibration',
        description: 'Recalibrate flow control systems and update control parameters',
        priority: 'medium',
        estimatedCost: 800,
        estimatedTime: '3 hours',
        expectedImpact: 75,
      },
    ],
  },
];

const mockFailurePatterns: FailurePattern[] = [
  {
    pattern: 'Bearing Failures',
    frequency: 12,
    averageDowntime: 6.5,
    totalCost: 18000,
    trend: 'increasing',
  },
  {
    pattern: 'Electrical Faults',
    frequency: 8,
    averageDowntime: 4.2,
    totalCost: 12000,
    trend: 'stable',
  },
  {
    pattern: 'Process Deviations',
    frequency: 15,
    averageDowntime: 2.8,
    totalCost: 9000,
    trend: 'decreasing',
  },
  {
    pattern: 'Control System Issues',
    frequency: 6,
    averageDowntime: 8.1,
    totalCost: 15000,
    trend: 'stable',
  },
];

const mockTrendData = [
  { month: 'Jan', mechanical: 4, electrical: 2, process: 5, control: 2 },
  { month: 'Feb', mechanical: 6, electrical: 3, process: 4, control: 1 },
  { month: 'Mar', mechanical: 8, electrical: 2, process: 3, control: 3 },
  { month: 'Apr', mechanical: 5, electrical: 4, process: 6, control: 2 },
  { month: 'May', mechanical: 7, electrical: 1, process: 2, control: 2 },
  { month: 'Jun', mechanical: 9, electrical: 3, process: 4, control: 1 },
];

// #endregion

export const RootCauseAnalysis: React.FC<RootCauseAnalysisProps> = ({
  assetId,
  workOrderId,
  failureType,
  className,
  onAnalysisComplete,
}) => {
  const [selectedRootCause, setSelectedRootCause] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('causes');
  const [isAnalyzing, setIsAnalyzing] = useState(false);

  // This would use real API in production
  const mockAnalysisData = {
    data: {
      rootCauses: mockRootCauses,
      failurePatterns: mockFailurePatterns,
      trends: mockTrendData,
    },
    loading: false,
    error: null,
  };

  const handleStartAnalysis = async () => {
    setIsAnalyzing(true);
    
    // Simulate analysis process
    setTimeout(() => {
      setIsAnalyzing(false);
      onAnalysisComplete?.(mockAnalysisData.data);
    }, 3000);
  };

  const getRiskColor = (score: number) => {
    if (score >= 70) return 'text-red-600 bg-red-100';
    if (score >= 50) return 'text-orange-600 bg-orange-100';
    if (score >= 30) return 'text-yellow-600 bg-yellow-100';
    return 'text-green-600 bg-green-100';
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'resolved': return 'text-green-600 bg-green-100';
      case 'identified': return 'text-blue-600 bg-blue-100';
      case 'investigating': return 'text-orange-600 bg-orange-100';
      case 'dismissed': return 'text-gray-600 bg-gray-100';
      default: return 'text-gray-600 bg-gray-100';
    }
  };

  const getPriorityColor = (priority: string) => {
    switch (priority) {
      case 'critical': return 'text-red-600 bg-red-100';
      case 'high': return 'text-orange-600 bg-orange-100';
      case 'medium': return 'text-yellow-600 bg-yellow-100';
      case 'low': return 'text-green-600 bg-green-100';
      default: return 'text-gray-600 bg-gray-100';
    }
  };

  const getEvidenceIcon = (type: string) => {
    switch (type) {
      case 'sensor_data': return '📊';
      case 'maintenance_log': return '📋';
      case 'operator_report': return '👤';
      case 'visual_inspection': return '👁️';
      default: return '📄';
    }
  };

  const getTrendIcon = (trend: string) => {
    switch (trend) {
      case 'increasing': return <TrendingDown className="h-4 w-4 text-red-600" />;
      case 'decreasing': return <TrendingDown className="h-4 w-4 text-green-600 rotate-180" />;
      case 'stable': return <div className="h-4 w-4 border-t-2 border-gray-400" />;
      default: return null;
    }
  };

  if (mockAnalysisData.loading || isAnalyzing) {
    return (
      <div className={cn('space-y-6', className)}>
        <AnalyticsLoadingSkeleton variant="chart" />
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <AnalyticsLoadingSkeleton variant="list" />
          <AnalyticsLoadingSkeleton variant="table" />
        </div>
      </div>
    );
  }

  if (mockAnalysisData.error) {
    return (
      <AnalyticsErrorDisplay
        error={mockAnalysisData.error}
        onRetry={handleStartAnalysis}
        className={className}
      />
    );
  }

  const { rootCauses, failurePatterns, trends } = mockAnalysisData.data;

  return (
    <div className={cn('space-y-6', className)}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Root Cause Analysis</h2>
          <p className="text-muted-foreground">
            AI-powered failure investigation and analysis
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm">
            <Download className="h-4 w-4 mr-2" />
            Export Report
          </Button>
          <Button onClick={handleStartAnalysis} disabled={isAnalyzing} size="sm">
            <Search className="h-4 w-4 mr-2" />
            Run Analysis
          </Button>
        </div>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab} className="space-y-6">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="causes" className="flex items-center gap-2">
            <AlertTriangle className="h-4 w-4" />
            Root Causes
          </TabsTrigger>
          <TabsTrigger value="patterns" className="flex items-center gap-2">
            <TrendingDown className="h-4 w-4" />
            Failure Patterns
          </TabsTrigger>
          <TabsTrigger value="trends" className="flex items-center gap-2">
            <Clock className="h-4 w-4" />
            Historical Trends
          </TabsTrigger>
          <TabsTrigger value="recommendations" className="flex items-center gap-2">
            <FileText className="h-4 w-4" />
            Action Plans
          </TabsTrigger>
        </TabsList>

        {/* Root Causes Tab */}
        <TabsContent value="causes" className="space-y-6">
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Causes List */}
            <div className="lg:col-span-2">
              <Card>
                <CardHeader>
                  <CardTitle>Identified Root Causes</CardTitle>
                  <CardDescription>Potential failure causes ranked by risk score</CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="space-y-4">
                    {rootCauses.map((cause) => (
                      <div
                        key={cause.id}
                        className={cn(
                          'border rounded-lg p-4 cursor-pointer transition-colors',
                          selectedRootCause === cause.id
                            ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
                            : 'border-border hover:bg-muted/50'
                        )}
                        onClick={() => setSelectedRootCause(
                          selectedRootCause === cause.id ? null : cause.id
                        )}
                      >
                        <div className="flex items-start justify-between mb-2">
                          <div className="flex-1">
                            <div className="flex items-center gap-2 mb-1">
                              <h4 className="font-medium text-sm">{cause.category}</h4>
                              <Badge 
                                variant="secondary" 
                                className={getRiskColor(cause.riskScore)}
                              >
                                {cause.riskScore.toFixed(1)} Risk
                              </Badge>
                              <Badge 
                                variant="outline" 
                                className={getStatusColor(cause.status)}
                              >
                                {cause.status}
                              </Badge>
                            </div>
                            <p className="text-sm text-muted-foreground mb-2">
                              {cause.description}
                            </p>
                          </div>
                          <ChevronRight 
                            className={cn(
                              'h-4 w-4 text-muted-foreground transition-transform',
                              selectedRootCause === cause.id && 'rotate-90'
                            )} 
                          />
                        </div>
                        
                        <div className="grid grid-cols-2 gap-4 text-xs text-muted-foreground">
                          <div>
                            <span>Probability: </span>
                            <span className="font-medium">{cause.probability}%</span>
                          </div>
                          <div>
                            <span>Impact: </span>
                            <span className="font-medium">{cause.impact}%</span>
                          </div>
                        </div>
                        
                        <div className="mt-2">
                          <div className="flex justify-between text-xs mb-1">
                            <span>Risk Score</span>
                            <span>{cause.riskScore.toFixed(1)}</span>
                          </div>
                          <Progress value={cause.riskScore} className="h-2" />
                        </div>

                        {selectedRootCause === cause.id && (
                          <div className="mt-4 pt-4 border-t space-y-4">
                            {/* Evidence */}
                            <div>
                              <h5 className="font-medium text-sm mb-2">Supporting Evidence</h5>
                              <div className="space-y-2">
                                {cause.evidence.map((evidence) => (
                                  <div key={evidence.id} className="flex items-start gap-3 text-xs">
                                    <span className="text-lg">
                                      {getEvidenceIcon(evidence.type)}
                                    </span>
                                    <div className="flex-1">
                                      <p className="text-muted-foreground">
                                        {evidence.description}
                                      </p>
                                      <div className="flex items-center gap-4 mt-1 text-xs">
                                        <span>Source: {evidence.source}</span>
                                        <Badge variant="outline" className="px-1 py-0 text-xs">
                                          {evidence.confidence}% confidence
                                        </Badge>
                                      </div>
                                    </div>
                                  </div>
                                ))}
                              </div>
                            </div>

                            {/* Recommendations */}
                            <div>
                              <h5 className="font-medium text-sm mb-2">Recommended Actions</h5>
                              <div className="space-y-2">
                                {cause.recommendations.map((rec) => (
                                  <div key={rec.id} className="border rounded p-3 text-xs">
                                    <div className="flex items-center justify-between mb-1">
                                      <h6 className="font-medium">{rec.title}</h6>
                                      <Badge 
                                        variant="outline" 
                                        className={getPriorityColor(rec.priority)}
                                      >
                                        {rec.priority}
                                      </Badge>
                                    </div>
                                    <p className="text-muted-foreground mb-2">
                                      {rec.description}
                                    </p>
                                    <div className="flex items-center gap-4 text-xs">
                                      <span>Cost: ${rec.estimatedCost.toLocaleString()}</span>
                                      <span>Time: {rec.estimatedTime}</span>
                                      <span>Impact: {rec.expectedImpact}%</span>
                                    </div>
                                  </div>
                                ))}
                              </div>
                            </div>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>
            </div>

            {/* Risk Summary */}
            <div>
              <Card>
                <CardHeader>
                  <CardTitle>Risk Summary</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="text-center">
                    <div className="text-3xl font-bold text-red-600">
                      {rootCauses.length}
                    </div>
                    <p className="text-sm text-muted-foreground">Root Causes Identified</p>
                  </div>
                  
                  <Separator />
                  
                  <div className="space-y-3">
                    <div className="flex justify-between text-sm">
                      <span>Critical Risk</span>
                      <span className="font-medium text-red-600">
                        {rootCauses.filter(c => c.riskScore >= 70).length}
                      </span>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span>High Risk</span>
                      <span className="font-medium text-orange-600">
                        {rootCauses.filter(c => c.riskScore >= 50 && c.riskScore < 70).length}
                      </span>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span>Medium Risk</span>
                      <span className="font-medium text-yellow-600">
                        {rootCauses.filter(c => c.riskScore >= 30 && c.riskScore < 50).length}
                      </span>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span>Low Risk</span>
                      <span className="font-medium text-green-600">
                        {rootCauses.filter(c => c.riskScore < 30).length}
                      </span>
                    </div>
                  </div>

                  <Separator />

                  <div className="space-y-3">
                    <div className="flex justify-between text-sm">
                      <span>Resolved</span>
                      <div className="flex items-center gap-1">
                        <CheckCircle className="h-4 w-4 text-green-600" />
                        <span>{rootCauses.filter(c => c.status === 'resolved').length}</span>
                      </div>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span>In Progress</span>
                      <div className="flex items-center gap-1">
                        <Clock className="h-4 w-4 text-orange-600" />
                        <span>
                          {rootCauses.filter(c => ['identified', 'investigating'].includes(c.status)).length}
                        </span>
                      </div>
                    </div>
                    <div className="flex justify-between text-sm">
                      <span>Dismissed</span>
                      <div className="flex items-center gap-1">
                        <XCircle className="h-4 w-4 text-gray-600" />
                        <span>{rootCauses.filter(c => c.status === 'dismissed').length}</span>
                      </div>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>
          </div>
        </TabsContent>

        {/* Failure Patterns Tab */}
        <TabsContent value="patterns" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Failure Pattern Analysis</CardTitle>
              <CardDescription>Recurring failure patterns and their impact</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {failurePatterns.map((pattern, index) => (
                  <div key={index} className="border rounded-lg p-4">
                    <div className="flex items-center justify-between mb-2">
                      <h4 className="font-medium">{pattern.pattern}</h4>
                      <div className="flex items-center gap-2">
                        {getTrendIcon(pattern.trend)}
                        <Badge variant="outline">{pattern.trend}</Badge>
                      </div>
                    </div>
                    
                    <div className="grid grid-cols-3 gap-4 text-sm">
                      <div>
                        <span className="text-muted-foreground">Frequency: </span>
                        <span className="font-medium">{pattern.frequency} occurrences</span>
                      </div>
                      <div>
                        <span className="text-muted-foreground">Avg Downtime: </span>
                        <span className="font-medium">{pattern.averageDowntime}h</span>
                      </div>
                      <div>
                        <span className="text-muted-foreground">Total Cost: </span>
                        <span className="font-medium">${pattern.totalCost.toLocaleString()}</span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        {/* Historical Trends Tab */}
        <TabsContent value="trends" className="space-y-6">
          <BaseLineChart
            data={trends}
            xAxisKey="month"
            lines={[
              { dataKey: 'mechanical', name: 'Mechanical', color: '#ef4444' },
              { dataKey: 'electrical', name: 'Electrical', color: '#f59e0b' },
              { dataKey: 'process', name: 'Process', color: '#3b82f6' },
              { dataKey: 'control', name: 'Control System', color: '#10b981' },
            ]}
            title="Failure Trends by Category"
            description="Number of failures by category over time"
            height={400}
          />
        </TabsContent>

        {/* Recommendations Tab */}
        <TabsContent value="recommendations" className="space-y-6">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            {rootCauses.map((cause) =>
              cause.recommendations.map((rec) => (
                <Card key={rec.id}>
                  <CardHeader>
                    <div className="flex items-center justify-between">
                      <CardTitle className="text-lg">{rec.title}</CardTitle>
                      <Badge className={getPriorityColor(rec.priority)}>
                        {rec.priority}
                      </Badge>
                    </div>
                    <CardDescription>{cause.category} - {cause.description}</CardDescription>
                  </CardHeader>
                  <CardContent>
                    <p className="text-sm text-muted-foreground mb-4">
                      {rec.description}
                    </p>
                    
                    <div className="grid grid-cols-2 gap-4 text-sm mb-4">
                      <div>
                        <span className="text-muted-foreground">Estimated Cost:</span>
                        <div className="font-medium">${rec.estimatedCost.toLocaleString()}</div>
                      </div>
                      <div>
                        <span className="text-muted-foreground">Time Required:</span>
                        <div className="font-medium">{rec.estimatedTime}</div>
                      </div>
                    </div>
                    
                    <div className="mb-4">
                      <div className="flex justify-between text-sm mb-1">
                        <span>Expected Impact</span>
                        <span>{rec.expectedImpact}%</span>
                      </div>
                      <Progress value={rec.expectedImpact} className="h-2" />
                    </div>
                    
                    <div className="flex gap-2">
                      <Button size="sm" className="flex-1">
                        Create Work Order
                      </Button>
                      <Button variant="outline" size="sm">
                        More Details
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              ))
            )}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
};
