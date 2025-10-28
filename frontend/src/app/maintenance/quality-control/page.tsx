'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { 
  ClipboardCheck, 
  AlertTriangle, 
  CheckCircle, 
  XCircle, 
  Clock, 
  Plus, 
  Eye, 
  FileText,
  BarChart3,
  TrendingUp,
  Settings,
  Play,
  Shield,
  AlertCircle,
  Calendar,
  Target
} from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
} from 'recharts';

// Import new services
import { inspectionExecutionService, InspectionExecution, WorkOrderInfo } from '@/services/inspectionExecutionService';
import { qualityChecklistService, QualityChecklist } from '@/services/qualityChecklistService';
import { regulatoryComplianceService, ComplianceDashboard, ComplianceStatus } from '@/services/regulatoryComplianceService';

interface QualityInspection {
  id: string;
  workOrderId: string;
  workOrderTitle: string;
  assetName: string;
  inspectorName: string;
  scheduledDate: string;
  completedDate?: string;
  status: 'Pending' | 'In Progress' | 'Completed' | 'Failed' | 'Approved';
  result: 'Pass' | 'Fail' | 'Needs Review' | 'Not Inspected';
  score: number;
  notes: string;
  checklistItems: ChecklistItem[];
}

interface ChecklistItem {
  id: string;
  description: string;
  result: 'Pass' | 'Fail' | 'N/A';
  notes?: string;
  critical: boolean;
}

interface QualityMetrics {
  totalInspections: number;
  passedInspections: number;
  failedInspections: number;
  pendingInspections: number;
  averageScore: number;
  complianceRate: number;
}


export default function QualityControlPage() {
  const router = useRouter();
  const [inspections, setInspections] = useState<QualityInspection[]>([]);
  const [qualityTrendData, setQualityTrendData] = useState<any[]>([]);
  const [inspectionStatusData, setInspectionStatusData] = useState<any[]>([]);
  const [selectedInspection, setSelectedInspection] = useState<QualityInspection | null>(null);
  const [isViewDialogOpen, setIsViewDialogOpen] = useState(false);
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  
  // New state for Phase 2 features
  const [activeInspections, setActiveInspections] = useState<InspectionExecution[]>([]);
  const [completedWorkOrders, setCompletedWorkOrders] = useState<WorkOrderInfo[]>([]);
  const [complianceDashboard, setComplianceDashboard] = useState<ComplianceDashboard | null>(null);
  const [upcomingDeadlines, setUpcomingDeadlines] = useState<ComplianceStatus[]>([]);
  const [totalChecklists, setTotalChecklists] = useState(0);

  const getStatusBadge = (status: QualityInspection['status']) => {
    const colors = {
      'Pending': 'bg-yellow-100 text-yellow-800',
      'In Progress': 'bg-blue-100 text-blue-800',
      'Completed': 'bg-green-100 text-green-800',
      'Failed': 'bg-red-100 text-red-800',
      'Approved': 'bg-green-100 text-green-800',
    };

    return (
      <Badge className={colors[status]}>
        {status}
      </Badge>
    );
  };

  const getResultBadge = (result: QualityInspection['result']) => {
    const colors = {
      'Pass': 'bg-green-100 text-green-800',
      'Fail': 'bg-red-100 text-red-800',
      'Needs Review': 'bg-yellow-100 text-yellow-800',
      'Not Inspected': 'bg-gray-100 text-gray-800',
    };

    const icons = {
      'Pass': <CheckCircle className="h-3 w-3 mr-1" />,
      'Fail': <XCircle className="h-3 w-3 mr-1" />,
      'Needs Review': <AlertTriangle className="h-3 w-3 mr-1" />,
      'Not Inspected': <Clock className="h-3 w-3 mr-1" />,
    };

    return (
      <Badge className={colors[result]}>
        {icons[result]}
        {result}
      </Badge>
    );
  };

  const getChecklistItemBadge = (result: ChecklistItem['result']) => {
    const colors = {
      'Pass': 'bg-green-100 text-green-800',
      'Fail': 'bg-red-100 text-red-800',
      'N/A': 'bg-gray-100 text-gray-800',
    };

    return (
      <Badge className={colors[result]} variant="outline">
        {result}
      </Badge>
    );
  };

  const calculateMetrics = (): QualityMetrics => {
    const completed = inspections.filter(i => i.status === 'Completed');
    const passed = completed.filter(i => i.result === 'Pass').length;
    const failed = completed.filter(i => i.result === 'Fail').length;
    const pending = inspections.filter(i => i.status === 'Pending').length;
    const averageScore = completed.length > 0 
      ? completed.reduce((sum, i) => sum + i.score, 0) / completed.length 
      : 0;
    const complianceRate = completed.length > 0 ? (passed / completed.length) * 100 : 0;

    return {
      totalInspections: inspections.length,
      passedInspections: passed,
      failedInspections: failed,
      pendingInspections: pending,
      averageScore,
      complianceRate,
    };
  };

  const metrics = calculateMetrics();

  useEffect(() => {
    loadDashboardData();
  }, []);

  const loadDashboardData = async () => {
    try {
      setLoading(true);
      
      // Try to load quality inspections, fallback to mock data
      try {
        const [inspectionsResponse, trendDataResponse, statusDataResponse] = await Promise.all([
          fetch('/api/maintenance/quality-control/inspections'),
          fetch('/api/maintenance/quality-control/trend-data'),
          fetch('/api/maintenance/quality-control/status-data')
        ]);
        
        if (inspectionsResponse.ok) {
          const inspectionsData = await inspectionsResponse.json();
          setInspections(inspectionsData);
        }
        
        if (trendDataResponse.ok) {
          const trendData = await trendDataResponse.json();
          setQualityTrendData(trendData);
        }
        
        if (statusDataResponse.ok) {
          const statusData = await statusDataResponse.json();
          setInspectionStatusData(statusData);
        }
      } catch (fetchError) {
        console.log('Quality control API not available, using mock data');
        // Mock data for quality inspections
        setInspections([
          {
            id: '1',
            workOrderId: 'WO-2024-001',
            workOrderNumber: 'WO-2024-001',
            assetName: 'HVAC Unit 1',
            inspectorId: '1',
            inspectorName: 'John Smith',
            checklist: { id: '1', name: 'HVAC Maintenance Checklist' },
            status: 'Completed',
            result: 'Pass',
            score: 95,
            scheduledDate: new Date().toISOString(),
            completedDate: new Date().toISOString(),
            notes: 'All systems operating normally'
          },
          {
            id: '2',
            workOrderId: 'WO-2024-002',
            workOrderNumber: 'WO-2024-002',
            assetName: 'Elevator 1',
            inspectorId: '2',
            inspectorName: 'Sarah Wilson',
            checklist: { id: '2', name: 'Elevator Safety Checklist' },
            status: 'In Progress',
            result: 'Not Inspected',
            score: 0,
            scheduledDate: new Date().toISOString(),
            notes: ''
          }
        ]);
        setQualityTrendData([]);
        setInspectionStatusData([]);
      }
      
      // Try to load active inspections, fallback to mock data
      try {
        const activeInspectionsData = await inspectionExecutionService.getActiveInspections();
        setActiveInspections(activeInspectionsData);
      } catch (activeError) {
        console.log('Active inspections API not available, using mock data');
        setActiveInspections([
          {
            id: '1',
            workOrderId: 'WO-2024-003',
            workOrderNumber: 'WO-2024-003',
            title: 'Generator Maintenance Inspection',
            assetName: 'Emergency Generator',
            checklist: { id: '3', name: 'Generator Inspection Checklist' },
            status: 'In Progress',
            progress: 45,
            dueDate: new Date().toISOString()
          }
        ]);
      }
      
      // Try to load completed work orders, fallback to mock data
      try {
        const workOrdersData = await inspectionExecutionService.getWorkOrdersForInspection();
        setCompletedWorkOrders(workOrdersData);
      } catch (workOrderError) {
        console.log('Work orders API not available, using mock data');
        setCompletedWorkOrders([
          {
            id: 'WO-2024-004',
            workOrderNumber: 'WO-2024-004',
            title: 'HVAC Filter Replacement',
            assetName: 'Main Building HVAC Unit 2',
            priority: 'Medium',
            completedDate: new Date().toISOString(),
            technician: 'Mike Johnson'
          },
          {
            id: 'WO-2024-005',
            workOrderNumber: 'WO-2024-005',
            title: 'Fire System Test',
            assetName: 'Fire Suppression System',
            priority: 'High',
            completedDate: new Date().toISOString(),
            technician: 'Lisa Davis'
          }
        ]);
      }
      
      // Try to load compliance data, fallback to mock data
      try {
        const complianceData = await regulatoryComplianceService.getComplianceDashboard();
        setComplianceDashboard(complianceData);
        
        const deadlines = await regulatoryComplianceService.getUpcomingDeadlines(30);
        setUpcomingDeadlines(deadlines);
      } catch (complianceError) {
        console.log('Compliance API not available, using mock data');
        setComplianceDashboard({
          totalRequirements: 15,
          compliantRequirements: 12,
          nonCompliantRequirements: 2,
          pendingRequirements: 1,
          complianceRate: 85.7,
          lastAuditDate: new Date().toISOString(),
          nextAuditDate: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString()
        });
        setUpcomingDeadlines([
          {
            id: '1',
            title: 'Annual Safety Inspection',
            dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString(),
            priority: 'High',
            status: 'Pending'
          }
        ]);
      }
      
      // Try to load checklists count, fallback to mock data
      try {
        const checklists = await qualityChecklistService.getAllChecklists();
        setTotalChecklists(checklists.length);
      } catch (checklistError) {
        console.log('Checklists API not available, using mock data');
        setTotalChecklists(5);
      }
      
    } catch (error) {
      console.error('Error loading dashboard data:', error);
      // Set fallback values
      setInspections([]);
      setActiveInspections([]);
      setCompletedWorkOrders([]);
      setTotalChecklists(0);
    } finally {
      setLoading(false);
    }
  };

  const startInspection = (workOrderId: string) => {
    router.push(`/maintenance/quality-control/inspect/${workOrderId}`);
  };

  const navigateToChecklistManagement = () => {
    router.push('/administration/maintenance/quality-checklists');
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Quality Control Dashboard</h1>
          <p className="text-muted-foreground">
            Monitor inspections, compliance, and quality metrics
          </p>
        </div>
        
        <div className="flex items-center space-x-2">
          <Button variant="outline" onClick={navigateToChecklistManagement}>
            <Settings className="mr-2 h-4 w-4" />
            Manage Checklists
          </Button>
          
          <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="mr-2 h-4 w-4" />
                Start Inspection
              </Button>
            </DialogTrigger>
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>Start Quality Inspection</DialogTitle>
                <DialogDescription>
                  Select a completed work order to begin quality inspection.
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="workOrder">Completed Work Orders</Label>
                  <div className="space-y-2 max-h-64 overflow-y-auto">
                    {loading ? (
                      <div className="text-center py-4">Loading work orders...</div>
                    ) : completedWorkOrders.length === 0 ? (
                      <div className="text-center py-4 text-muted-foreground">
                        No completed work orders available for inspection
                      </div>
                    ) : (
                      completedWorkOrders.map((workOrder) => (
                        <div key={workOrder.id} className="border rounded-lg p-3 hover:bg-muted/50 transition-colors cursor-pointer" onClick={() => {
                          startInspection(workOrder.id);
                          setIsCreateDialogOpen(false);
                        }}>
                          <div className="flex items-center justify-between">
                            <div>
                              <p className="font-medium text-sm">{workOrder.workOrderNumber}</p>
                              <p className="text-sm text-muted-foreground">{workOrder.title}</p>
                              <p className="text-xs text-muted-foreground">{workOrder.assetName}</p>
                            </div>
                            <div className="flex items-center space-x-2">
                              <Badge variant="outline">{workOrder.priority}</Badge>
                              <Button size="sm" variant="ghost">
                                <Play className="h-4 w-4" />
                              </Button>
                            </div>
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setIsCreateDialogOpen(false)}>
                  Cancel
                </Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
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
            <BreadcrumbPage>Quality Control</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Quick Actions */}
      {!loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          <Card className="cursor-pointer hover:shadow-md transition-shadow" onClick={() => setIsCreateDialogOpen(true)}>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-lg font-semibold">Start Inspection</p>
                  <p className="text-sm text-muted-foreground">{completedWorkOrders.length} work orders ready</p>
                </div>
                <Play className="h-8 w-8 text-blue-500" />
              </div>
            </CardContent>
          </Card>
          
          <Card className="cursor-pointer hover:shadow-md transition-shadow" onClick={navigateToChecklistManagement}>
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-lg font-semibold">Manage Checklists</p>
                  <p className="text-sm text-muted-foreground">{totalChecklists} checklists available</p>
                </div>
                <Settings className="h-8 w-8 text-purple-500" />
              </div>
            </CardContent>
          </Card>
          
          <Card className="cursor-pointer hover:shadow-md transition-shadow">
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-lg font-semibold">Compliance Status</p>
                  <p className="text-sm text-muted-foreground">
                    {complianceDashboard ? `${complianceDashboard.complianceRate.toFixed(0)}% compliant` : 'Loading...'}
                  </p>
                </div>
                <Shield className="h-8 w-8 text-green-500" />
              </div>
            </CardContent>
          </Card>
          
          <Card className="cursor-pointer hover:shadow-md transition-shadow">
            <CardContent className="pt-6">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-lg font-semibold">Active Inspections</p>
                  <p className="text-sm text-muted-foreground">{activeInspections.length} in progress</p>
                </div>
                <ClipboardCheck className="h-8 w-8 text-orange-500" />
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Quality Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-6 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Inspections</CardTitle>
            <ClipboardCheck className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{metrics.totalInspections}</div>
            <p className="text-xs text-muted-foreground">All time</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Passed</CardTitle>
            <CheckCircle className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">{metrics.passedInspections}</div>
            <p className="text-xs text-muted-foreground">Quality approved</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Failed</CardTitle>
            <XCircle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{metrics.failedInspections}</div>
            <p className="text-xs text-muted-foreground">Require rework</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Pending</CardTitle>
            <Clock className="h-4 w-4 text-yellow-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-yellow-600">{metrics.pendingInspections}</div>
            <p className="text-xs text-muted-foreground">Awaiting inspection</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Avg Score</CardTitle>
            <BarChart3 className="h-4 w-4 text-blue-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">{metrics.averageScore.toFixed(1)}</div>
            <p className="text-xs text-muted-foreground">Quality rating</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Compliance Rate</CardTitle>
            <TrendingUp className="h-4 w-4 text-purple-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-purple-600">{metrics.complianceRate.toFixed(1)}%</div>
            <p className="text-xs text-muted-foreground">Pass rate</p>
          </CardContent>
        </Card>
      </div>

      {/* Compliance Alerts */}
      {!loading && upcomingDeadlines.length > 0 && (
        <Card className="border-yellow-200 bg-yellow-50">
          <CardHeader>
            <CardTitle className="flex items-center space-x-2 text-yellow-800">
              <AlertCircle className="h-5 w-5" />
              <span>Upcoming Compliance Deadlines</span>
            </CardTitle>
            <CardDescription>
              These assets have compliance requirements due within 30 days
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {upcomingDeadlines.slice(0, 3).map((item) => (
                <div key={item.id} className="flex items-center justify-between p-3 bg-white rounded-lg">
                  <div>
                    <p className="font-medium text-sm">{item.assetName}</p>
                    <p className="text-xs text-muted-foreground">{item.requirement.name}</p>
                  </div>
                  <div className="text-right">
                    <Badge className={regulatoryComplianceService.getRiskLevelColor(item.riskLevel)}>
                      {item.daysUntilDue} days left
                    </Badge>
                  </div>
                </div>
              ))}
              {upcomingDeadlines.length > 3 && (
                <p className="text-xs text-muted-foreground text-center pt-2">
                  +{upcomingDeadlines.length - 3} more compliance items due soon
                </p>
              )}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Charts */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Quality Trends</CardTitle>
            <CardDescription>Monthly inspection results and average scores</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={qualityTrendData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="month" />
                <YAxis />
                <Tooltip />
                <Bar dataKey="passed" fill="#10b981" name="Passed" />
                <Bar dataKey="failed" fill="#ef4444" name="Failed" />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Inspection Status Distribution</CardTitle>
            <CardDescription>Current status of all inspections</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={inspectionStatusData}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={100}
                  paddingAngle={2}
                  dataKey="value"
                >
                  {inspectionStatusData.map((entry, index) => (
                    <Cell key={`cell-${index}`} fill={entry.color} />
                  ))}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
            <div className="flex flex-wrap justify-center gap-4 mt-4">
              {inspectionStatusData.map((item) => (
                <div key={item.name} className="flex items-center space-x-2">
                  <div
                    className="w-3 h-3 rounded-full"
                    style={{ backgroundColor: item.color }}
                  />
                  <span className="text-sm">{item.name} ({item.value})</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Active Inspections */}
      {!loading && activeInspections.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center space-x-2">
              <ClipboardCheck className="h-5 w-5" />
              <span>Active Inspections</span>
            </CardTitle>
            <CardDescription>
              Inspections currently in progress
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {activeInspections.map((inspection) => (
                <div key={inspection.id} className="border rounded-lg p-4 hover:bg-muted/50 transition-colors">
                  <div className="flex items-center justify-between">
                    <div>
                      <p className="font-medium text-sm">{inspection.workOrderNumber}</p>
                      <p className="text-sm text-muted-foreground">{inspection.workOrderTitle}</p>
                      <p className="text-xs text-muted-foreground">{inspection.assetName}</p>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Badge className="bg-blue-100 text-blue-800">
                        {inspection.status}
                      </Badge>
                      <Button size="sm" onClick={() => startInspection(inspection.workOrderId)}>
                        <Eye className="mr-2 h-4 w-4" />
                        Continue
                      </Button>
                    </div>
                  </div>
                  {inspection.overallScore !== undefined && (
                    <div className="mt-2 flex items-center space-x-2">
                      <Progress value={(inspection.itemResponses.filter(r => r.result !== 'N/A').length / inspection.checklist.items.length) * 100} className="flex-1 h-2" />
                      <span className="text-xs text-muted-foreground">
                        {inspection.itemResponses.filter(r => r.result !== 'N/A').length}/{inspection.checklist.items.length} items
                      </span>
                    </div>
                  )}
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Inspections Table */}
      <Card>
        <CardHeader>
          <CardTitle>Quality Inspections</CardTitle>
          <CardDescription>All quality control inspections and their results</CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Work Order</TableHead>
                <TableHead>Asset</TableHead>
                <TableHead>Inspector</TableHead>
                <TableHead>Scheduled Date</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Result</TableHead>
                <TableHead>Score</TableHead>
                <TableHead>Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {inspections.map((inspection) => (
                <TableRow key={inspection.id}>
                  <TableCell>
                    <div>
                      <p className="font-medium">{inspection.workOrderId}</p>
                      <p className="text-sm text-muted-foreground">{inspection.workOrderTitle}</p>
                    </div>
                  </TableCell>
                  <TableCell>{inspection.assetName}</TableCell>
                  <TableCell>{inspection.inspectorName}</TableCell>
                  <TableCell>{new Date(inspection.scheduledDate).toLocaleDateString()}</TableCell>
                  <TableCell>{getStatusBadge(inspection.status)}</TableCell>
                  <TableCell>{getResultBadge(inspection.result)}</TableCell>
                  <TableCell>
                    {inspection.status === 'Completed' ? (
                      <div className="flex items-center space-x-2">
                        <span className="font-medium">{inspection.score}</span>
                        <div className="w-20">
                          <Progress value={inspection.score} className="h-2" />
                        </div>
                      </div>
                    ) : (
                      <span className="text-muted-foreground">-</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => {
                        setSelectedInspection(inspection);
                        setIsViewDialogOpen(true);
                      }}
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

      {/* View Inspection Dialog */}
      <Dialog open={isViewDialogOpen} onOpenChange={setIsViewDialogOpen}>
        <DialogContent className="max-w-4xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Quality Inspection Details</DialogTitle>
            <DialogDescription>
              Complete inspection results and checklist
            </DialogDescription>
          </DialogHeader>
          {selectedInspection && (
            <Tabs defaultValue="overview" className="space-y-4">
              <TabsList>
                <TabsTrigger value="overview">Overview</TabsTrigger>
                <TabsTrigger value="checklist">Inspection Checklist</TabsTrigger>
                <TabsTrigger value="notes">Notes & Comments</TabsTrigger>
              </TabsList>
              
              <TabsContent value="overview" className="space-y-4">
                <div className="grid grid-cols-2 gap-6">
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Work Order</Label>
                      <p className="text-sm font-medium">{selectedInspection.workOrderId}</p>
                      <p className="text-sm text-muted-foreground">{selectedInspection.workOrderTitle}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Asset</Label>
                      <p className="text-sm">{selectedInspection.assetName}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Inspector</Label>
                      <p className="text-sm">{selectedInspection.inspectorName}</p>
                    </div>
                  </div>
                  <div className="space-y-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                      <div className="pt-1">{getStatusBadge(selectedInspection.status)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Result</Label>
                      <div className="pt-1">{getResultBadge(selectedInspection.result)}</div>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Quality Score</Label>
                      <div className="flex items-center space-x-2 pt-1">
                        <span className="text-2xl font-bold">{selectedInspection.score}</span>
                        <div className="w-32">
                          <Progress value={selectedInspection.score} className="h-3" />
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
                
                <div className="border-t pt-4">
                  <h4 className="text-sm font-medium mb-4">Inspection Dates</h4>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Scheduled</Label>
                      <p className="text-sm">{new Date(selectedInspection.scheduledDate).toLocaleDateString()}</p>
                    </div>
                    <div>
                      <Label className="text-sm font-medium text-muted-foreground">Completed</Label>
                      <p className="text-sm">
                        {selectedInspection.completedDate 
                          ? new Date(selectedInspection.completedDate).toLocaleDateString()
                          : 'Not completed'
                        }
                      </p>
                    </div>
                  </div>
                </div>
              </TabsContent>
              
              <TabsContent value="checklist">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Inspection Checklist</h4>
                  {selectedInspection.checklistItems.length > 0 ? (
                    <div className="space-y-3">
                      {selectedInspection.checklistItems.map((item) => (
                        <div key={item.id} className="border rounded-lg p-4">
                          <div className="flex items-start justify-between">
                            <div className="flex-1">
                              <div className="flex items-center space-x-2">
                                <p className="font-medium text-sm">{item.description}</p>
                                {item.critical && (
                                  <Badge variant="destructive" className="text-xs">Critical</Badge>
                                )}
                              </div>
                              {item.notes && (
                                <p className="text-sm text-muted-foreground mt-1">{item.notes}</p>
                              )}
                            </div>
                            <div>{getChecklistItemBadge(item.result)}</div>
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-sm text-muted-foreground text-center py-8">
                      No checklist items available for this inspection.
                    </p>
                  )}
                </div>
              </TabsContent>
              
              <TabsContent value="notes">
                <div className="space-y-4">
                  <h4 className="text-sm font-medium">Inspector Notes & Comments</h4>
                  <div className="border rounded-lg p-4">
                    <p className="text-sm">
                      {selectedInspection.notes || 'No additional notes provided.'}
                    </p>
                  </div>
                </div>
              </TabsContent>
            </Tabs>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsViewDialogOpen(false)}>
              Close
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}