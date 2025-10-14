'use client';

import React, { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Breadcrumb, BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator } from '@/components/ui/breadcrumb';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { 
  Shield,
  AlertTriangle, 
  CheckCircle, 
  XCircle, 
  Clock,
  FileText,
  Download,
  Upload,
  Search,
  Filter,
  Calendar,
  Plus,
  Eye,
  Edit,
  Trash2,
  AlertCircle,
  CheckSquare,
  Building,
  Gavel,
  History,
  RefreshCw
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

import { regulatoryComplianceService, ComplianceDashboard, ComplianceStatus, ComplianceRequirement, ComplianceReport, AuditLog } from '@/services/regulatoryComplianceService';

interface ComplianceReportGeneration {
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
}

export default function CompliancePage() {
  const [loading, setLoading] = useState(true);
  const [complianceDashboard, setComplianceDashboard] = useState<ComplianceDashboard | null>(null);
  const [complianceStatuses, setComplianceStatuses] = useState<ComplianceStatus[]>([]);
  const [requirements, setRequirements] = useState<ComplianceRequirement[]>([]);
  const [reports, setReports] = useState<ComplianceReport[]>([]);
  const [auditLogs, setAuditLogs] = useState<AuditLog[]>([]);
  
  const [selectedStatus, setSelectedStatus] = useState<ComplianceStatus | null>(null);
  const [isStatusDialogOpen, setIsStatusDialogOpen] = useState(false);
  const [isReportDialogOpen, setIsReportDialogOpen] = useState(false);
  const [isUpdateDialogOpen, setIsUpdateDialogOpen] = useState(false);
  
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [riskFilter, setRiskFilter] = useState('all');
  
  const [reportGeneration, setReportGeneration] = useState<ComplianceReportGeneration>({
    title: '',
    description: '',
    requirementIds: [],
    assetIds: [],
    dateRange: {
      start: new Date().toISOString().split('T')[0],
      end: new Date().toISOString().split('T')[0]
    },
    format: 'pdf',
    includePhotos: true,
    includeSignatures: true
  });

  useEffect(() => {
    loadComplianceData();
  }, []);

  const loadComplianceData = async () => {
    try {
      setLoading(true);
      
      const [dashboard, statuses, reqs, reportsList, logs] = await Promise.all([
        regulatoryComplianceService.getComplianceDashboard(),
        regulatoryComplianceService.getAllComplianceStatuses(),
        regulatoryComplianceService.getAllRequirements(),
        regulatoryComplianceService.getComplianceReports(),
        regulatoryComplianceService.getAuditLogs()
      ]);
      
      setComplianceDashboard(dashboard);
      setComplianceStatuses(statuses);
      setRequirements(reqs);
      setReports(reportsList);
      setAuditLogs(logs);
    } catch (error) {
      console.error('Error loading compliance data:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleUpdateStatus = async (statusId: string, updates: any) => {
    try {
      await regulatoryComplianceService.updateComplianceStatus(statusId, updates);
      await loadComplianceData();
      setIsUpdateDialogOpen(false);
    } catch (error) {
      console.error('Error updating compliance status:', error);
    }
  };

  const handleGenerateReport = async () => {
    try {
      const report = await regulatoryComplianceService.generateReport(reportGeneration);
      console.log('Generated report:', report);
      setIsReportDialogOpen(false);
      await loadComplianceData();
    } catch (error) {
      console.error('Error generating report:', error);
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

  const getComplianceStatusColor = (status: string) => {
    switch (status) {
      case 'Compliant':
        return 'bg-green-100 text-green-800';
      case 'Non-Compliant':
        return 'bg-red-100 text-red-800';
      case 'Pending Review':
        return 'bg-yellow-100 text-yellow-800';
      case 'Overdue':
        return 'bg-red-100 text-red-800';
      case 'Grace Period':
        return 'bg-orange-100 text-orange-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const filteredStatuses = complianceStatuses.filter(status => {
    const matchesSearch = status.assetName.toLowerCase().includes(searchTerm.toLowerCase()) ||
                         status.requirement.name.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesStatus = statusFilter === 'all' || status.complianceStatus === statusFilter;
    const matchesRisk = riskFilter === 'all' || status.riskLevel === riskFilter;
    
    return matchesSearch && matchesStatus && matchesRisk;
  });

  if (loading || !complianceDashboard) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-center h-64">
          <div className="text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-gray-900 mx-auto"></div>
            <p className="mt-4 text-muted-foreground">Loading compliance data...</p>
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
          <h1 className="text-3xl font-bold tracking-tight">Regulatory Compliance</h1>
          <p className="text-muted-foreground">
            Manage compliance status, generate reports, and track audit trails
          </p>
        </div>
        
        <div className="flex items-center space-x-2">
          <Button variant="outline" onClick={() => setIsReportDialogOpen(true)}>
            <FileText className="mr-2 h-4 w-4" />
            Generate Report
          </Button>
          
          <Button onClick={loadComplianceData}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh Data
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
            <BreadcrumbPage>Compliance</BreadcrumbPage>
          </BreadcrumbItem>
        </BreadcrumbList>
      </Breadcrumb>

      {/* Dashboard Overview */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Overall Compliance</CardTitle>
            <Shield className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">{complianceDashboard.complianceRate.toFixed(1)}%</div>
            <div className="flex items-center mt-2">
              <Progress value={complianceDashboard.complianceRate} className="flex-1 h-2 mr-2" />
              <span className="text-xs text-muted-foreground">
                {complianceDashboard.compliantAssets}/{complianceDashboard.totalAssets}
              </span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Overdue Items</CardTitle>
            <AlertTriangle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{complianceDashboard.overdueItems}</div>
            <p className="text-xs text-muted-foreground">Require immediate attention</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Upcoming Deadlines</CardTitle>
            <Clock className="h-4 w-4 text-yellow-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-yellow-600">{complianceDashboard.upcomingDeadlines}</div>
            <p className="text-xs text-muted-foreground">Next 30 days</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Critical Violations</CardTitle>
            <XCircle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{complianceDashboard.violations}</div>
            <p className="text-xs text-muted-foreground">Active violations</p>
          </CardContent>
        </Card>
      </div>

      {/* Tabs for different compliance views */}
      <Tabs defaultValue="status" className="space-y-4">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="status">Compliance Status</TabsTrigger>
          <TabsTrigger value="requirements">Requirements</TabsTrigger>
          <TabsTrigger value="reports">Reports</TabsTrigger>
          <TabsTrigger value="audit">Audit Trail</TabsTrigger>
        </TabsList>

        <TabsContent value="status" className="space-y-4">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>Asset Compliance Status</CardTitle>
                  <CardDescription>Current compliance status for all monitored assets</CardDescription>
                </div>
                
                <div className="flex items-center space-x-2">
                  <div className="relative">
                    <Search className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                    <Input
                      placeholder="Search assets or requirements..."
                      value={searchTerm}
                      onChange={(e) => setSearchTerm(e.target.value)}
                      className="pl-9 w-64"
                    />
                  </div>
                  
                  <Select value={statusFilter} onValueChange={setStatusFilter}>
                    <SelectTrigger className="w-40">
                      <SelectValue placeholder="Status" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Status</SelectItem>
                      <SelectItem value="Compliant">Compliant</SelectItem>
                      <SelectItem value="Non-Compliant">Non-Compliant</SelectItem>
                      <SelectItem value="Pending Review">Pending Review</SelectItem>
                      <SelectItem value="Overdue">Overdue</SelectItem>
                    </SelectContent>
                  </Select>
                  
                  <Select value={riskFilter} onValueChange={setRiskFilter}>
                    <SelectTrigger className="w-32">
                      <SelectValue placeholder="Risk" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Risk</SelectItem>
                      <SelectItem value="Low">Low</SelectItem>
                      <SelectItem value="Medium">Medium</SelectItem>
                      <SelectItem value="High">High</SelectItem>
                      <SelectItem value="Critical">Critical</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Asset</TableHead>
                    <TableHead>Requirement</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Risk Level</TableHead>
                    <TableHead>Due Date</TableHead>
                    <TableHead>Last Check</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filteredStatuses.map((status) => (
                    <TableRow key={status.id}>
                      <TableCell>
                        <div>
                          <p className="font-medium text-sm">{status.assetName}</p>
                          <p className="text-xs text-muted-foreground">{status.assetType}</p>
                        </div>
                      </TableCell>
                      <TableCell>
                        <div>
                          <p className="font-medium text-sm">{status.requirement.name}</p>
                          <p className="text-xs text-muted-foreground">{status.requirement.category}</p>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge className={getComplianceStatusColor(status.complianceStatus)}>
                          {status.complianceStatus}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <Badge className={getRiskLevelColor(status.riskLevel)}>
                          {status.riskLevel}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center space-x-1">
                          <Calendar className="h-3 w-3" />
                          <span className="text-sm">{new Date(status.dueDate).toLocaleDateString()}</span>
                          {status.daysUntilDue <= 7 && (
                            <AlertCircle className="h-3 w-3 text-red-500" />
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-sm">
                        {status.lastCheckDate ? new Date(status.lastCheckDate).toLocaleDateString() : 'Never'}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center space-x-1">
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => {
                              setSelectedStatus(status);
                              setIsStatusDialogOpen(true);
                            }}
                          >
                            <Eye className="h-4 w-4" />
                          </Button>
                          
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => {
                              setSelectedStatus(status);
                              setIsUpdateDialogOpen(true);
                            }}
                          >
                            <Edit className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="requirements" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Compliance Requirements</CardTitle>
              <CardDescription>All regulatory requirements and their details</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {requirements.map((requirement) => (
                  <div key={requirement.id} className="border rounded-lg p-4">
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center space-x-2">
                          <h4 className="font-medium">{requirement.name}</h4>
                          <Badge variant="outline">{requirement.category}</Badge>
                          <Badge className={getRiskLevelColor(requirement.riskLevel)}>
                            {requirement.riskLevel}
                          </Badge>
                        </div>
                        <p className="text-sm text-muted-foreground mt-1">{requirement.description}</p>
                        
                        <div className="mt-3 grid grid-cols-2 gap-4 text-sm">
                          <div>
                            <Label className="text-xs text-muted-foreground">Frequency</Label>
                            <p>{requirement.frequency}</p>
                          </div>
                          <div>
                            <Label className="text-xs text-muted-foreground">Authority</Label>
                            <p>{requirement.regulatoryAuthority}</p>
                          </div>
                          <div>
                            <Label className="text-xs text-muted-foreground">Penalty</Label>
                            <p className="text-red-600 font-medium">{requirement.penaltyDescription}</p>
                          </div>
                          <div>
                            <Label className="text-xs text-muted-foreground">Grace Period</Label>
                            <p>{requirement.gracePeriodDays} days</p>
                          </div>
                        </div>
                        
                        {requirement.requiredDocuments.length > 0 && (
                          <div className="mt-3">
                            <Label className="text-xs text-muted-foreground">Required Documents</Label>
                            <div className="flex flex-wrap gap-1 mt-1">
                              {requirement.requiredDocuments.map((doc, index) => (
                                <Badge key={index} variant="secondary" className="text-xs">
                                  {doc}
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                      
                      <div className="flex items-center space-x-1">
                        <Button size="sm" variant="outline">
                          <FileText className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="reports" className="space-y-4">
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle>Compliance Reports</CardTitle>
                  <CardDescription>Generated compliance reports and documentation</CardDescription>
                </div>
                <Button onClick={() => setIsReportDialogOpen(true)}>
                  <Plus className="mr-2 h-4 w-4" />
                  Generate Report
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Report Title</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Generated Date</TableHead>
                    <TableHead>Period</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {reports.map((report) => (
                    <TableRow key={report.id}>
                      <TableCell>
                        <div>
                          <p className="font-medium text-sm">{report.title}</p>
                          <p className="text-xs text-muted-foreground">{report.description}</p>
                        </div>
                      </TableCell>
                      <TableCell>{report.type}</TableCell>
                      <TableCell>{new Date(report.generatedDate).toLocaleDateString()}</TableCell>
                      <TableCell>
                        {new Date(report.periodStart).toLocaleDateString()} - {new Date(report.periodEnd).toLocaleDateString()}
                      </TableCell>
                      <TableCell>
                        <Badge className={report.status === 'completed' ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800'}>
                          {report.status}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center space-x-1">
                          <Button size="sm" variant="outline">
                            <Eye className="h-4 w-4" />
                          </Button>
                          <Button size="sm" variant="outline">
                            <Download className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="audit" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Audit Trail</CardTitle>
              <CardDescription>Complete audit log of all compliance-related activities</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                {auditLogs.map((log) => (
                  <div key={log.id} className="border rounded-lg p-3">
                    <div className="flex items-start space-x-3">
                      <div className="flex-shrink-0">
                        <div className={`w-8 h-8 rounded-full flex items-center justify-center ${
                          log.action.includes('update') ? 'bg-blue-100 text-blue-600' :
                          log.action.includes('create') ? 'bg-green-100 text-green-600' :
                          log.action.includes('delete') ? 'bg-red-100 text-red-600' :
                          'bg-gray-100 text-gray-600'
                        }`}>
                          <History className="h-4 w-4" />
                        </div>
                      </div>
                      <div className="flex-1">
                        <div className="flex items-center justify-between">
                          <div>
                            <p className="font-medium text-sm">{log.action}</p>
                            <p className="text-xs text-muted-foreground">
                              by {log.performedBy} • {new Date(log.timestamp).toLocaleString()}
                            </p>
                          </div>
                          <Badge variant="outline" className="text-xs">
                            {log.entityType}
                          </Badge>
                        </div>
                        {log.details && (
                          <p className="text-sm text-muted-foreground mt-1">{log.details}</p>
                        )}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Status Detail Dialog */}
      <Dialog open={isStatusDialogOpen} onOpenChange={setIsStatusDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Compliance Status Details</DialogTitle>
            <DialogDescription>
              Detailed information about compliance status and requirements
            </DialogDescription>
          </DialogHeader>
          {selectedStatus && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Asset</Label>
                  <p className="text-sm font-medium">{selectedStatus.assetName}</p>
                </div>
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Requirement</Label>
                  <p className="text-sm font-medium">{selectedStatus.requirement.name}</p>
                </div>
              </div>
              
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Status</Label>
                  <div className="pt-1">
                    <Badge className={getComplianceStatusColor(selectedStatus.complianceStatus)}>
                      {selectedStatus.complianceStatus}
                    </Badge>
                  </div>
                </div>
                <div>
                  <Label className="text-sm font-medium text-muted-foreground">Risk Level</Label>
                  <div className="pt-1">
                    <Badge className={getRiskLevelColor(selectedStatus.riskLevel)}>
                      {selectedStatus.riskLevel}
                    </Badge>
                  </div>
                </div>
              </div>
              
              <div>
                <Label className="text-sm font-medium text-muted-foreground">Notes</Label>
                <p className="text-sm">{selectedStatus.notes || 'No additional notes available.'}</p>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Report Generation Dialog */}
      <Dialog open={isReportDialogOpen} onOpenChange={setIsReportDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Generate Compliance Report</DialogTitle>
            <DialogDescription>
              Create a comprehensive compliance report with customizable options
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="title">Report Title</Label>
                <Input
                  id="title"
                  value={reportGeneration.title}
                  onChange={(e) => setReportGeneration(prev => ({ ...prev, title: e.target.value }))}
                  placeholder="Enter report title"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="format">Format</Label>
                <Select
                  value={reportGeneration.format}
                  onValueChange={(value: 'pdf' | 'excel' | 'csv') => setReportGeneration(prev => ({ ...prev, format: value }))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="pdf">PDF</SelectItem>
                    <SelectItem value="excel">Excel</SelectItem>
                    <SelectItem value="csv">CSV</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea
                id="description"
                value={reportGeneration.description}
                onChange={(e) => setReportGeneration(prev => ({ ...prev, description: e.target.value }))}
                placeholder="Brief description of the report purpose"
                rows={3}
              />
            </div>
            
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="startDate">Start Date</Label>
                <Input
                  id="startDate"
                  type="date"
                  value={reportGeneration.dateRange.start}
                  onChange={(e) => setReportGeneration(prev => ({ 
                    ...prev, 
                    dateRange: { ...prev.dateRange, start: e.target.value } 
                  }))}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="endDate">End Date</Label>
                <Input
                  id="endDate"
                  type="date"
                  value={reportGeneration.dateRange.end}
                  onChange={(e) => setReportGeneration(prev => ({ 
                    ...prev, 
                    dateRange: { ...prev.dateRange, end: e.target.value } 
                  }))}
                />
              </div>
            </div>
            
            <div className="space-y-3">
              <Label>Include Additional Content</Label>
              <div className="space-y-2">
                <div className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    id="includePhotos"
                    checked={reportGeneration.includePhotos}
                    onChange={(e) => setReportGeneration(prev => ({ ...prev, includePhotos: e.target.checked }))}
                    className="rounded"
                  />
                  <Label htmlFor="includePhotos" className="text-sm">Include inspection photos</Label>
                </div>
                <div className="flex items-center space-x-2">
                  <input
                    type="checkbox"
                    id="includeSignatures"
                    checked={reportGeneration.includeSignatures}
                    onChange={(e) => setReportGeneration(prev => ({ ...prev, includeSignatures: e.target.checked }))}
                    className="rounded"
                  />
                  <Label htmlFor="includeSignatures" className="text-sm">Include digital signatures</Label>
                </div>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsReportDialogOpen(false)}>
              Cancel
            </Button>
            <Button onClick={handleGenerateReport}>
              <FileText className="mr-2 h-4 w-4" />
              Generate Report
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}