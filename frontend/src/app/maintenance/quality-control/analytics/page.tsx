'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  TrendingUp, 
  TrendingDown, 
  Minus,
  BarChart3, 
  Users, 
  Target,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Clock,
  Filter,
  Download,
  Eye,
  Calendar,
  Award,
  Wrench,
  Shield,
  Activity
} from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  BarChart,
  Bar,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  AreaChart,
  Area,
} from 'recharts';

import { performanceAnalyticsService, PerformanceDashboardData, InspectorPerformance, AssetQualityMetrics, ComplianceTrend, DrillDownData } from '@/services/performanceAnalyticsService';

const COLORS = ['#0088FE', '#00C49F', '#FFBB28', '#FF8042', '#8884d8', '#82ca9d'];

export default function AnalyticsPage() {
  const [loading, setLoading] = useState(true);
  const [dashboardData, setDashboardData] = useState<PerformanceDashboardData | null>(null);
  const [selectedDateRange, setSelectedDateRange] = useState('30d');
  const [selectedInspector, setSelectedInspector] = useState<string>('all');
  const [selectedAsset, setSelectedAsset] = useState<string>('all');
  const [drillDownData, setDrillDownData] = useState<DrillDownData | null>(null);
  const [isDrillDownOpen, setIsDrillDownOpen] = useState(false);

  useEffect(() => {
    loadAnalyticsData();
  }, [selectedDateRange]);

  const loadAnalyticsData = async () => {
    try {
      setLoading(true);
      const data = await performanceAnalyticsService.getPerformanceDashboard(selectedDateRange);
      setDashboardData(data);
    } catch (error) {
      console.error('Error loading analytics data:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleDrillDown = async (filters: any) => {
    try {
      const data = await performanceAnalyticsService.getDrillDownData({
        dateRange: selectedDateRange,
        ...filters
      });
      setDrillDownData(data);
      setIsDrillDownOpen(true);
    } catch (error) {
      console.error('Error loading drill-down data:', error);
    }
  };

  const getTrendIcon = (direction: 'Improving' | 'Stable' | 'Declining') => {
    switch (direction) {
      case 'Improving':
        return <TrendingUp className="h-4 w-4 text-green-500" />;
      case 'Declining':
        return <TrendingDown className="h-4 w-4 text-red-500" />;
      default:
        return <Minus className="h-4 w-4 text-yellow-500" />;
    }
  };

  const getRiskLevelColor = (riskLevel: string) => {
    switch (riskLevel) {
      case 'Low':
        return 'bg-green-100 text-green-800';
      case 'Medium':
        return 'bg-yellow-100 text-yellow-800';
      case 'High':
        return 'bg-orange-100 text-orange-800';
      case 'Critical':
        return 'bg-red-100 text-red-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const getAlertSeverityColor = (severity: string) => {
    switch (severity) {
      case 'Low':
        return 'border-blue-200 bg-blue-50';
      case 'Medium':
        return 'border-yellow-200 bg-yellow-50';
      case 'High':
        return 'border-orange-200 bg-orange-50';
      case 'Critical':
        return 'border-red-200 bg-red-50';
      default:
        return 'border-gray-200 bg-gray-50';
    }
  };

  if (loading || !dashboardData) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-center h-64">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-gray-900 mx-auto"></div>
            <p className="mt-4 text-muted-foreground">Loading analytics...</p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Performance Analytics</h1>
          <p className="text-muted-foreground">
            Comprehensive quality control performance insights and trends
          </p>
        </div>
        
        <div className="flex items-center space-x-2">
          <Select value={selectedDateRange} onValueChange={setSelectedDateRange}>
            <SelectTrigger className="w-40">
              <SelectValue placeholder="Date range" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="7d">Last 7 days</SelectItem>
              <SelectItem value="30d">Last 30 days</SelectItem>
              <SelectItem value="90d">Last 90 days</SelectItem>
              <SelectItem value="1y">Last year</SelectItem>
            </SelectContent>
          </Select>
          
          <Button variant="outline">
            <Download className="mr-2 h-4 w-4" />
            Export Report
          </Button>
        </div>
      </div>

      {/* Breadcrumbs */}
      <Breadcrumb>
        <BreadcrumbList>
          <BreadcrumbItem>
            <BreadcrumbLink href="/dashboard">Dashboard</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/maintenance">Maintenance</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbLink href="/maintenance/quality-control">Quality Control</BreadcrumbLink>
          </BreadcrumbItem>
          <BreadcrumbSeparator />
          <BreadcrumbItem>
            <BreadcrumbPage>Analytics</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Overall Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Inspections</CardTitle>
            <BarChart3 className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{dashboardData.overallMetrics.totalInspections}</div>
            <p className="text-xs text-muted-foreground">All time</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Average Score</CardTitle>
            <Target className="h-4 w-4 text-blue-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">{dashboardData.overallMetrics.averageScore}</div>
            <p className="text-xs text-muted-foreground">Quality rating</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Compliance Rate</CardTitle>
            <Shield className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">{dashboardData.overallMetrics.complianceRate}%</div>
            <p className="text-xs text-muted-foreground">Pass rate</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Active Inspectors</CardTitle>
            <Users className="h-4 w-4 text-purple-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-purple-600">{dashboardData.overallMetrics.activeInspectors}</div>
            <p className="text-xs text-muted-foreground">Team members</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Critical Issues</CardTitle>
            <AlertTriangle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{dashboardData.overallMetrics.criticalIssues}</div>
            <p className="text-xs text-muted-foreground">Require attention</p>
          </CardContent>
        </Card>
      </div>

      {/* Alerts Section */}
      {dashboardData.alerts.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center space-x-2">
              <AlertTriangle className="h-5 w-5" />
              <span>Performance Alerts</span>
            </CardTitle>
            <CardDescription>
              Issues requiring immediate attention
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {dashboardData.alerts.map((alert) => (
                <div key={alert.id} className={`border rounded-lg p-4 ${getAlertSeverityColor(alert.severity)}`}>
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <div className="flex items-center space-x-2">
                        <h4 className="font-medium text-sm">{alert.title}</h4>
                        <Badge className={getRiskLevelColor(alert.severity)}>
                          {alert.severity}
                        </Badge>
                      </div>
                      <p className="text-sm text-muted-foreground mt-1">{alert.description}</p>
                      {alert.dueDate && (
                        <p className="text-xs text-muted-foreground mt-2 flex items-center">
                          <Calendar className="h-3 w-3 mr-1" />
                          Due: {new Date(alert.dueDate).toLocaleDateString()}
                        </p>
                      )}
                    </div>
                    {alert.actionRequired && (
                      <Button size="sm" variant="outline">
                        Take Action
                      </Button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Analytics Tabs */}
      <Tabs defaultValue="trends" className="space-y-4">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="trends">Quality Trends</TabsTrigger>
          <TabsTrigger value="inspectors">Inspector Performance</TabsTrigger>
          <TabsTrigger value="assets">Asset Performance</TabsTrigger>
          <TabsTrigger value="compliance">Compliance Analytics</TabsTrigger>
        </TabsList>

        <TabsContent value="trends" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Quality Trends Over Time</CardTitle>
              <CardDescription>Inspection volumes, scores, and pass rates</CardDescription>
            </CardHeader>
            <CardContent>
              <ResponsiveContainer width="100%" height={400}>
                <AreaChart data={dashboardData.qualityTrends}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="date" />
                  <YAxis yAxisId="left" />
                  <YAxis yAxisId="right" orientation="right" />
                  <Tooltip />
                  <Area yAxisId="left" type="monotone" dataKey="totalInspections" stackId="1" stroke="#8884d8" fill="#8884d8" fillOpacity={0.3} />
                  <Area yAxisId="left" type="monotone" dataKey="passedInspections" stackId="2" stroke="#82ca9d" fill="#82ca9d" fillOpacity={0.3} />
                  <Line yAxisId="right" type="monotone" dataKey="averageScore" stroke="#ff7300" strokeWidth={3} />
                </AreaChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="inspectors" className="space-y-4">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <Card>
              <CardHeader>
                <CardTitle>Top Performers</CardTitle>
                <CardDescription>Best performing inspectors by score and pass rate</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-4">
                  {dashboardData.topPerformers.map((inspector, index) => (
                    <div key={inspector.inspectorId} className="flex items-center space-x-4 p-3 rounded-lg border">
                      <div className="flex items-center justify-center w-8 h-8 rounded-full bg-blue-100 text-blue-800 font-bold">
                        {index + 1}
                      </div>
                      <div className="flex-1">
                        <div className="flex items-center justify-between">
                          <div>
                            <p className="font-medium text-sm">{inspector.inspectorName}</p>
                            <p className="text-xs text-muted-foreground">{inspector.certificationLevel} • {inspector.specializations.join(', ')}</p>
                          </div>
                          <div className="text-right">
                            <p className="font-bold text-sm">{inspector.averageScore}</p>
                            <p className="text-xs text-muted-foreground">{inspector.passRate}% pass</p>
                          </div>
                        </div>
                        <div className="mt-2 flex items-center space-x-2">
                          <Progress value={inspector.averageScore} className="flex-1 h-2" />
                          <span className="text-xs text-muted-foreground">{inspector.totalInspections} insp.</span>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Inspector Performance Comparison</CardTitle>
                <CardDescription>Average scores by inspector</CardDescription>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={dashboardData.topPerformers}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="inspectorName" tick={{ fontSize: 12 }} />
                    <YAxis />
                    <Tooltip />
                    <Bar dataKey="averageScore" fill="#8884d8" />
                  </BarChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="assets" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Asset Performance Overview</CardTitle>
              <CardDescription>Quality metrics and trends for critical assets</CardDescription>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Asset</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Avg Score</TableHead>
                    <TableHead>Pass Rate</TableHead>
                    <TableHead>Risk Level</TableHead>
                    <TableHead>Trend</TableHead>
                    <TableHead>Last Inspection</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {dashboardData.assetPerformance.map((asset) => (
                    <TableRow key={asset.assetId}>
                      <TableCell>
                        <div>
                          <p className="font-medium text-sm">{asset.assetName}</p>
                          <p className="text-xs text-muted-foreground">{asset.totalInspections} inspections</p>
                        </div>
                      </TableCell>
                      <TableCell>{asset.assetType}</TableCell>
                      <TableCell>
                        <div className="flex items-center space-x-2">
                          <span className="font-medium">{asset.averageScore}</span>
                          <div className="w-16">
                            <Progress value={asset.averageScore} className="h-2" />
                          </div>
                        </div>
                      </TableCell>
                      <TableCell>{asset.passRate}%</TableCell>
                      <TableCell>
                        <Badge className={getRiskLevelColor(asset.riskLevel)}>
                          {asset.riskLevel}
                        </Badge>
                      </TableCell>
                      <TableCell>{getTrendIcon(asset.trendDirection)}</TableCell>
                      <TableCell>{new Date(asset.lastInspectionDate).toLocaleDateString()}</TableCell>
                      <TableCell>
                        <Button 
                          size="sm" 
                          variant="outline"
                          onClick={() => handleDrillDown({ assetId: asset.assetId })}
                        >
                          <Eye className="h-4 w-4" />
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="compliance" className="space-y-4">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <Card>
              <CardHeader>
                <CardTitle>Compliance Overview</CardTitle>
                <CardDescription>Current compliance status by requirement type</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-4">
                  {dashboardData.complianceTrends.map((trend) => (
                    <div key={trend.requirementId} className="border rounded-lg p-4">
                      <div className="flex items-center justify-between mb-2">
                        <div>
                          <p className="font-medium text-sm">{trend.requirementName}</p>
                          <p className="text-xs text-muted-foreground">{trend.category}</p>
                        </div>
                        <Badge className={getRiskLevelColor(trend.complianceRate >= 95 ? 'Low' : trend.complianceRate >= 85 ? 'Medium' : 'High')}>
                          {trend.complianceRate.toFixed(1)}%
                        </Badge>
                      </div>
                      <div className="flex items-center justify-between text-sm">
                        <div className="flex items-center space-x-4">
                          <span className="text-muted-foreground">
                            <CheckCircle className="h-4 w-4 inline mr-1" />
                            {trend.compliantAssets}/{trend.totalAssets}
                          </span>
                          {trend.upcomingDeadlines > 0 && (
                            <span className="text-yellow-600">
                              <Clock className="h-4 w-4 inline mr-1" />
                              {trend.upcomingDeadlines} upcoming
                            </span>
                          )}
                          {trend.overdueItems > 0 && (
                            <span className="text-red-600">
                              <AlertTriangle className="h-4 w-4 inline mr-1" />
                              {trend.overdueItems} overdue
                            </span>
                          )}
                        </div>
                      </div>
                      <div className="mt-2">
                        <Progress value={trend.complianceRate} className="h-2" />
                      </div>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Compliance Trends</CardTitle>
                <CardDescription>Compliance rates over time</CardDescription>
              </CardHeader>
              <CardContent>
                <ResponsiveContainer width="100%" height={300}>
                  <LineChart data={dashboardData.complianceTrends[0]?.trend || []}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="month" />
                    <YAxis />
                    <Tooltip />
                    <Line type="monotone" dataKey="complianceRate" stroke="#8884d8" strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          </div>
        </TabsContent>
      </Tabs>

      {/* Drill Down Dialog */}
      <Dialog open={isDrillDownOpen} onOpenChange={setIsDrillDownOpen}>
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Detailed Inspection Data</DialogTitle>
            <DialogDescription>
              Drill-down view of inspections with applied filters
            </DialogDescription>
          </DialogHeader>
          {drillDownData && (
            <div className="space-y-4">
              <div className="flex items-center space-x-2">
                <Filter className="h-4 w-4" />
                <span className="text-sm text-muted-foreground">
                  Showing {drillDownData.inspections.length} inspections for {drillDownData.filters.dateRange}
                </span>
              </div>
              
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Work Order</TableHead>
                    <TableHead>Asset</TableHead>
                    <TableHead>Inspector</TableHead>
                    <TableHead>Date</TableHead>
                    <TableHead>Score</TableHead>
                    <TableHead>Result</TableHead>
                    <TableHead>Duration</TableHead>
                    <TableHead>Issues</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {drillDownData.inspections.map((inspection) => (
                    <TableRow key={inspection.id}>
                      <TableCell className="font-medium">{inspection.workOrderId}</TableCell>
                      <TableCell>{inspection.assetName}</TableCell>
                      <TableCell>{inspection.inspectorName}</TableCell>
                      <TableCell>{new Date(inspection.date).toLocaleDateString()}</TableCell>
                      <TableCell>
                        <div className="flex items-center space-x-2">
                          <span>{inspection.score}</span>
                          <div className="w-16">
                            <Progress value={inspection.score} className="h-1" />
                          </div>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge className={inspection.result === 'Pass' ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'}>
                          {inspection.result}
                        </Badge>
                      </TableCell>
                      <TableCell>{inspection.duration}min</TableCell>
                      <TableCell>
                        {inspection.criticalIssues > 0 ? (
                          <Badge variant="destructive">{inspection.criticalIssues}</Badge>
                        ) : (
                          <span className="text-muted-foreground">None</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}