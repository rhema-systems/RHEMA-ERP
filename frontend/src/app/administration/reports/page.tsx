'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { TenantGuard } from '../../../components/auth/tenant-guard';
import { Button } from '../../../components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/card';
import { Badge } from '../../../components/ui/badge';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '../../../components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '../../../components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../../../components/ui/dropdown-menu';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../components/ui/select';
import {
  BarChart3,
  PieChart,
  TrendingUp,
  FileText,
  Download,
  Calendar,
  Users,
  Building,
  DollarSign,
  Activity,
  Filter,
  Plus,
  Search,
  RefreshCw,
  Settings,
  Eye,
  Share2,
  Copy,
  Trash2,
  Edit,
  ChevronDown,
  Clock,
  Target,
  Zap,
  Database,
  Loader2,
  Shield,
  UserCheck,
  Globe,
  CheckCircle,
  XCircle,
  AlertCircle,
  AlertTriangle,
  Package
} from 'lucide-react';
import { Input } from '../../../components/ui/input';
import { ConfirmationDialog } from '../../../components/ui/confirmation-dialog';
import { useToast } from '../../../hooks/use-toast';
import { reportsService, ReportDefinition, ReportAnalytics, ReportRoleAssignment } from '../../../services/reports';
import { adminApiService } from '../../../services/admin-api.service';
import { useIsClient } from '../../../lib/ssr-utils';
import ReportBuilder from '../../../components/reports/ReportBuilder';
import AnalyticsDashboard from '../../../components/reports/AnalyticsDashboard';
import ReportTemplates from '../../../components/reports/ReportTemplates';
import DataExportTools from '../../../components/reports/DataExportTools';
import ReportResultsDialog from '../../../components/reports/ReportResultsDialog';
import ReportRoleAssignmentDialog from '../../../components/reports/ReportRoleAssignmentDialog';

// Enhanced interfaces for admin functionality
interface Module {
  id: string;
  name: string; // maps to moduleName from API
  description?: string;
  icon?: string;
}

interface Role {
  id: string;
  name: string;
  description: string;
}

interface EnhancedReportDefinition extends ReportDefinition {
  moduleId?: string;
  moduleName?: string;
  assignedRoles?: string[];
  publishedBy?: string;
  publishedAt?: string;
  isPublished: boolean;
  tenantId: string;
  version: number;
}

// Helper function to map module names to icon names
const getModuleIconName = (moduleName: string): string => {
  switch (moduleName.toLowerCase()) {
    case 'financial':
      return 'DollarSign';
    case 'sales':
      return 'TrendingUp';
    case 'human resources':
    case 'hr':
      return 'Users';
    case 'inventory':
      return 'Package';
    case 'operations':
      return 'Activity';
    case 'procurement':
      return 'ShoppingCart';
    case 'marketing':
      return 'Megaphone';
    default:
      return 'FileText';
  }
};

export default function AdministrationReportsPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedType, setSelectedType] = useState<string>('all');
  const [selectedStatus, setSelectedStatus] = useState<string>('all');
  const [isBuilderOpen, setIsBuilderOpen] = useState(false);
  const [selectedReportId, setSelectedReportId] = useState<string | null>(null);
  const [editingReport, setEditingReport] = useState<ReportDefinition | null>(null);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [reportToDelete, setReportToDelete] = useState<string | null>(null);
  const [executingReportId, setExecutingReportId] = useState<string | null>(null);
  const [showResultsDialog, setShowResultsDialog] = useState(false);
  const [selectedReportForResults, setSelectedReportForResults] = useState<{ id: string; name: string } | null>(null);
  
  // Module/Role assignment dialogs
  const [showModuleAssignDialog, setShowModuleAssignDialog] = useState(false);
  const [showRoleAssignDialog, setShowRoleAssignDialog] = useState(false);
  const [selectedReportForAssignment, setSelectedReportForAssignment] = useState<string | null>(null);
  const [selectedModuleForAssignment, setSelectedModuleForAssignment] = useState<string | null>(null);
  const [showPublishDialog, setShowPublishDialog] = useState(false);
  
  const isClient = useIsClient();
  
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Fetch tenant modules from API
  const { data: tenantModules = [], error: modulesError } = useQuery({
    queryKey: ['tenant-modules'],
    queryFn: () => adminApiService.getTenantModules(),
    refetchOnWindowFocus: false,
    retry: 1,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });

  // Map tenant modules to the expected Module interface, with fallback for errors
  const modules = tenantModules.length > 0 ? tenantModules.map(module => ({
    id: module.id,
    name: module.moduleName,
    description: module.description,
    icon: getModuleIconName(module.moduleName)
  })) : [
    // Fallback modules if API fails
    { id: 'financial', name: 'Financial', description: 'Financial reports and analytics', icon: 'DollarSign' },
    { id: 'sales', name: 'Sales', description: 'Sales performance and CRM reports', icon: 'TrendingUp' },
    { id: 'hr', name: 'Human Resources', description: 'HR and employee reports', icon: 'Users' },
    { id: 'inventory', name: 'Inventory', description: 'Stock and inventory reports', icon: 'Package' },
    { id: 'operations', name: 'Operations', description: 'Operational efficiency reports', icon: 'Activity' },
  ];

  // Fetch roles
  const { data: roles = [], error: rolesError } = useQuery({
    queryKey: ['roles'],
    queryFn: () => adminApiService.getRoles(),
    refetchOnWindowFocus: false,
    retry: 1,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });

  // Debug logging for API errors
  React.useEffect(() => {
    if (modulesError) {
      console.warn('Failed to fetch tenant modules:', modulesError);
    }
    if (rolesError) {
      console.warn('Failed to fetch roles:', rolesError);
    }
  }, [modulesError, rolesError]);

  // Fetch reports (now enhanced for admin)
  const {
    data: reports = [],
    isLoading: reportsLoading,
    error: reportsError,
    refetch: refetchReports
  } = useQuery({
    queryKey: ['admin-reports', selectedType !== 'all' ? selectedType : undefined, selectedStatus !== 'all' ? selectedStatus : undefined],
    queryFn: () => {
      console.log('Fetching reports...');
      return reportsService.getReportsForAdmin(
        selectedType !== 'all' ? selectedType : undefined,
        selectedStatus !== 'all' ? selectedStatus : undefined
      );
    },
    refetchOnWindowFocus: false,
    onSuccess: (data) => {
      console.log('Reports fetched successfully:', data.length, 'reports');
    },
    onError: (error) => {
      console.error('Failed to fetch reports:', error);
    }
  });

  // Fetch analytics
  const {
    data: analytics,
    isLoading: analyticsLoading
  } = useQuery({
    queryKey: ['reportAnalytics'],
    queryFn: () => reportsService.getReportAnalytics(),
    refetchOnWindowFocus: false,
  });

  // Enhanced mutations for admin functionality
  const executeReportMutation = useMutation({
    mutationFn: ({ reportId, params }: { reportId: string; params?: any }) => {
      setExecutingReportId(reportId);
      return reportsService.executeReport(reportId, { parameters: params, maxRows: 1000, includeMetadata: true });
    },
    onSuccess: (data) => {
      setExecutingReportId(null);
      toast({
        title: 'Report Executed Successfully',
        description: `Generated ${data.totalRows} rows in ${data.executionTime}`,
      });
    },
    onError: (error: any) => {
      setExecutingReportId(null);
      toast({
        title: 'Execution Failed',
        description: error.response?.data?.message || 'Failed to execute report',
        variant: 'destructive',
      });
    },
  });

  const deleteReportMutation = useMutation({
    mutationFn: reportsService.deleteReport,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-reports'] });
      toast({
        title: 'Report Deleted',
        description: 'Report has been deleted successfully',
      });
    },
    onError: (error: any) => {
      toast({
        title: 'Delete Failed',
        description: error.response?.data?.message || 'Failed to delete report',
        variant: 'destructive',
      });
    },
  });

  const publishReportMutation = useMutation({
    mutationFn: (reportId: string) => {
      return reportsService.publishReport(reportId);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-reports'] });
      toast({
        title: 'Report Published',
        description: 'Report is now available to assigned users',
      });
    },
    onError: (error: any) => {
      toast({
        title: 'Publish Failed',
        description: error.response?.data?.message || 'Failed to publish report',
        variant: 'destructive',
      });
    },
  });

  const assignModuleMutation = useMutation({
    mutationFn: ({ reportId, moduleId }: { reportId: string; moduleId: string }) => {
      return reportsService.assignReportToModule(reportId, moduleId);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-reports'] });
      toast({
        title: 'Module Assigned',
        description: 'Report has been assigned to the selected module',
      });
    },
    onError: (error: any) => {
      toast({
        title: 'Assignment Failed',
        description: error.response?.data?.message || 'Failed to assign module to report',
        variant: 'destructive',
      });
    },
  });


  // Helper functions
  const getTypeIcon = (type: string) => {
    switch (type.toLowerCase()) {
      case 'financial':
        return <DollarSign className="h-4 w-4" />;
      case 'user':
      case 'users':
        return <Users className="h-4 w-4" />;
      case 'tenant':
      case 'tenants':
        return <Building className="h-4 w-4" />;
      case 'operational':
      case 'operations':
        return <Activity className="h-4 w-4" />;
      default:
        return <FileText className="h-4 w-4" />;
    }
  };

  const getTypeColor = (type: string) => {
    switch (type.toLowerCase()) {
      case 'financial':
        return 'bg-green-100 text-green-700';
      case 'user':
      case 'users':
        return 'bg-blue-100 text-blue-700';
      case 'tenant':
      case 'tenants':
        return 'bg-purple-100 text-purple-700';
      case 'operational':
      case 'operations':
        return 'bg-orange-100 text-orange-700';
      default:
        return 'bg-gray-100 text-gray-700';
    }
  };

  const getStatusBadge = (status: string, isPublished?: boolean) => {
    if (isPublished) {
      return <Badge variant="default" className="bg-green-600"><CheckCircle className="h-3 w-3 mr-1" />Published</Badge>;
    }
    
    switch (status.toLowerCase()) {
      case 'published':
        return <Badge variant="default" className="bg-green-600"><CheckCircle className="h-3 w-3 mr-1" />Published</Badge>;
      case 'draft':
        return <Badge variant="secondary"><AlertCircle className="h-3 w-3 mr-1" />Draft</Badge>;
      case 'scheduled':
        return <Badge variant="outline"><Clock className="h-3 w-3 mr-1" />Scheduled</Badge>;
      default:
        return <Badge variant="secondary">{status}</Badge>;
    }
  };

  const getModuleIcon = (moduleId: string) => {
    const module = modules.find(m => m.id === moduleId);
    switch (module?.icon) {
      case 'DollarSign':
        return <DollarSign className="h-3 w-3" />;
      case 'TrendingUp':
        return <TrendingUp className="h-3 w-3" />;
      case 'Users':
        return <Users className="h-3 w-3" />;
      case 'Package':
        return <Package className="h-3 w-3" />;
      case 'Activity':
        return <Activity className="h-3 w-3" />;
      default:
        return <FileText className="h-3 w-3" />;
    }
  };

  // Event handlers
  const handleRunReport = (reportId: string) => {
    executeReportMutation.mutate({ reportId });
  };

  const handleViewResults = (reportId: string, reportName: string) => {
    setSelectedReportForResults({ id: reportId, name: reportName });
    setShowResultsDialog(true);
  };

  const handlePublishReport = (reportId: string) => {
    setSelectedReportForAssignment(reportId);
    setShowPublishDialog(true);
  };

  const handleAssignModule = (reportId: string) => {
    setSelectedReportForAssignment(reportId);
    setShowModuleAssignDialog(true);
  };

  const handleAssignRoles = (reportId: string) => {
    setSelectedReportForAssignment(reportId);
    setShowRoleAssignDialog(true);
  };

  const handleEditReport = (report: ReportDefinition) => {
    setEditingReport(report);
    setIsBuilderOpen(true);
  };

  const handleDeleteReport = (reportId: string) => {
    setReportToDelete(reportId);
    setShowDeleteDialog(true);
  };

  const confirmDeleteReport = () => {
    if (reportToDelete) {
      deleteReportMutation.mutate(reportToDelete);
      setReportToDelete(null);
    }
    setShowDeleteDialog(false);
  };

  // Filter reports
  const filteredReports = reports.filter(report => {
    const matchesSearch = report.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                         report.description.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesType = selectedType === 'all' || report.type.toLowerCase() === selectedType.toLowerCase();
    const matchesStatus = selectedStatus === 'all' || 
      (selectedStatus === 'published' && (report as EnhancedReportDefinition).isPublished) ||
      (selectedStatus === 'draft' && !(report as EnhancedReportDefinition).isPublished);
    return matchesSearch && matchesType && matchesStatus;
  });

  if (reportsError) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="text-center space-y-4">
          <div className="text-red-600">
            <FileText className="h-12 w-12 mx-auto mb-4" />
            <h2 className="text-xl font-semibold">Error Loading Reports</h2>
            <p className="text-muted-foreground mt-2">
              Failed to load reports. Please try again.
            </p>
          </div>
          <Button onClick={() => refetchReports()}>
            <RefreshCw className="h-4 w-4 mr-2" />
            Retry
          </Button>
        </div>
      </div>
    );
  }

  return (
    <TenantGuard>
      <div className="space-y-6">
        {/* API Error Warnings */}
        {(modulesError || rolesError) && (
          <div className="bg-yellow-50 border border-yellow-200 rounded-md p-4">
            <div className="flex">
              <AlertTriangle className="h-5 w-5 text-yellow-400" />
              <div className="ml-3">
                <h3 className="text-sm font-medium text-yellow-800">
                  Some features may be limited
                </h3>
                <div className="mt-2 text-sm text-yellow-700">
                  <ul className="list-disc pl-5 space-y-1">
                    {modulesError && <li>Unable to load tenant modules - using fallback data</li>}
                    {rolesError && <li>Unable to load roles - role assignment may not work</li>}
                  </ul>
                </div>
              </div>
            </div>
          </div>
        )}
        
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Reports Administration</h1>
            <p className="text-muted-foreground">
              Create, manage, and publish reports for your organization
            </p>
          </div>
          
          <div className="flex items-center space-x-2">
            <Button 
              variant="outline" 
              onClick={() => refetchReports()}
              disabled={reportsLoading}
            >
              <RefreshCw className={`h-4 w-4 mr-2 ${reportsLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
            <Button onClick={() => setIsBuilderOpen(true)}>
              <Plus className="h-4 w-4 mr-2" />
              New Report
            </Button>
          </div>
        </div>

        {/* Enhanced Analytics Overview Cards */}
        <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-8 gap-4">
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Total Reports</p>
                  <p className="text-2xl font-bold">
                    {analyticsLoading ? (
                      <Loader2 className="h-6 w-6 animate-spin" />
                    ) : (
                      analytics?.totalReports || 0
                    )}
                  </p>
                </div>
                <FileText className="h-8 w-8 text-blue-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Published</p>
                  <p className="text-2xl font-bold">
                    {analyticsLoading ? (
                      <Loader2 className="h-6 w-6 animate-spin" />
                    ) : (
                      filteredReports.filter(r => (r as EnhancedReportDefinition).isPublished).length
                    )}
                  </p>
                </div>
                <CheckCircle className="h-8 w-8 text-green-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Draft</p>
                  <p className="text-2xl font-bold">
                    {analyticsLoading ? (
                      <Loader2 className="h-6 w-6 animate-spin" />
                    ) : (
                      filteredReports.filter(r => !(r as EnhancedReportDefinition).isPublished).length
                    )}
                  </p>
                </div>
                <AlertCircle className="h-8 w-8 text-orange-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Modules</p>
                  <p className="text-2xl font-bold">{modules.length}</p>
                </div>
                <Package className="h-8 w-8 text-purple-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Roles</p>
                  <p className="text-2xl font-bold">{roles.length}</p>
                </div>
                <UserCheck className="h-8 w-8 text-indigo-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Scheduled</p>
                  <p className="text-2xl font-bold">
                    {analyticsLoading ? (
                      <Loader2 className="h-6 w-6 animate-spin" />
                    ) : (
                      analytics?.scheduledReports || 0
                    )}
                  </p>
                </div>
                <Clock className="h-8 w-8 text-green-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Run Today</p>
                  <p className="text-2xl font-bold">
                    {analyticsLoading ? (
                      <Loader2 className="h-6 w-6 animate-spin" />
                    ) : (
                      analytics?.reportsRunToday || 0
                    )}
                  </p>
                </div>
                <Zap className="h-8 w-8 text-yellow-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Exports</p>
                  <p className="text-2xl font-bold">
                    {analyticsLoading ? (
                      <Loader2 className="h-6 w-6 animate-spin" />
                    ) : (
                      analytics?.totalExports || 0
                    )}
                  </p>
                </div>
                <Download className="h-8 w-8 text-orange-600" />
              </div>
            </CardContent>
          </Card>
        </div>

        <Tabs defaultValue="reports" className="space-y-4">
          <TabsList>
            <TabsTrigger value="reports">Report Management</TabsTrigger>
            <TabsTrigger value="assignments">Role Assignments</TabsTrigger>
            <TabsTrigger value="modules">Module Assignment</TabsTrigger>
            <TabsTrigger value="analytics">Analytics Dashboard</TabsTrigger>
            <TabsTrigger value="templates">Templates</TabsTrigger>
          </TabsList>

          <TabsContent value="reports" className="space-y-4">
            {/* Enhanced Search and Filter Bar */}
            <div className="flex flex-col sm:flex-row gap-4">
              <div className="relative flex-1">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground h-4 w-4" />
                <Input
                  placeholder="Search reports..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pl-10"
                />
              </div>
              
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="outline">
                    <Filter className="h-4 w-4 mr-2" />
                    Type: {selectedType === 'all' ? 'All' : selectedType}
                    <ChevronDown className="h-4 w-4 ml-2" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent>
                  <DropdownMenuItem onClick={() => setSelectedType('all')}>
                    All Types
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => setSelectedType('financial')}>
                    Financial
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setSelectedType('user')}>
                    User Reports
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setSelectedType('tenant')}>
                    Tenant Analytics
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setSelectedType('operational')}>
                    Operational
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>

              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="outline">
                    <Shield className="h-4 w-4 mr-2" />
                    Status: {selectedStatus === 'all' ? 'All' : selectedStatus}
                    <ChevronDown className="h-4 w-4 ml-2" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent>
                  <DropdownMenuItem onClick={() => setSelectedStatus('all')}>
                    All Status
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => setSelectedStatus('published')}>
                    Published
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => setSelectedStatus('draft')}>
                    Draft
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>

            {/* Enhanced Reports Grid */}
            {reportsLoading ? (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {[...Array(6)].map((_, i) => (
                  <Card key={i} className="animate-pulse">
                    <CardHeader className="pb-3">
                      <div className="flex items-start justify-between">
                        <div className="flex items-center space-x-2">
                          <div className="w-10 h-10 bg-gray-200 rounded-lg"></div>
                          <div>
                            <div className="h-5 bg-gray-200 rounded w-32 mb-2"></div>
                            <div className="h-4 bg-gray-200 rounded w-20"></div>
                          </div>
                        </div>
                        <div className="w-8 h-8 bg-gray-200 rounded"></div>
                      </div>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-2">
                        <div className="h-4 bg-gray-200 rounded w-full"></div>
                        <div className="h-4 bg-gray-200 rounded w-3/4"></div>
                        <div className="h-8 bg-gray-200 rounded w-full mt-4"></div>
                      </div>
                    </CardContent>
                  </Card>
                ))}
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {filteredReports.map((report) => {
                  const enhancedReport = report as EnhancedReportDefinition;
                  return (
                    <Card key={report.id} className="hover:shadow-lg transition-shadow">
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div className="flex items-center space-x-2">
                            <div className={`p-2 rounded-lg ${getTypeColor(report.type)}`}>
                              {getTypeIcon(report.type)}
                            </div>
                            <div>
                              <CardTitle className="text-lg">{report.name}</CardTitle>
                              <div className="flex items-center space-x-2 mt-1">
                                {getStatusBadge(report.status, enhancedReport.isPublished)}
                                {enhancedReport.moduleId && (
                                  <Badge variant="outline" className="text-xs">
                                    {getModuleIcon(enhancedReport.moduleId)}
                                    <span className="ml-1">{enhancedReport.moduleName}</span>
                                  </Badge>
                                )}
                                {report.isFavorite && (
                                  <Badge variant="outline" className="text-yellow-600">
                                    ★ Favorite
                                  </Badge>
                                )}
                              </div>
                            </div>
                          </div>
                        
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button variant="ghost" size="sm">
                                <Settings className="h-4 w-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem 
                                onClick={() => handleRunReport(report.id)}
                                disabled={executingReportId === report.id}
                              >
                                {executingReportId === report.id ? (
                                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                                ) : (
                                  <Zap className="h-4 w-4 mr-2" />
                                )}
                                {executingReportId === report.id ? 'Running...' : 'Test Run'}
                              </DropdownMenuItem>
                              <DropdownMenuItem onClick={() => handleViewResults(report.id, report.name)}>
                                <Eye className="h-4 w-4 mr-2" />
                                View Results
                              </DropdownMenuItem>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem onClick={() => handleAssignModule(report.id)}>
                                <Package className="h-4 w-4 mr-2" />
                                Assign Module
                              </DropdownMenuItem>
                              <DropdownMenuItem onClick={() => handleAssignRoles(report.id)}>
                                <UserCheck className="h-4 w-4 mr-2" />
                                Assign Roles
                              </DropdownMenuItem>
                              <DropdownMenuSeparator />
                              {!enhancedReport.isPublished ? (
                                <DropdownMenuItem 
                                  onClick={() => handlePublishReport(report.id)}
                                  className="text-green-600"
                                >
                                  <CheckCircle className="h-4 w-4 mr-2" />
                                  Publish Report
                                </DropdownMenuItem>
                              ) : (
                                <DropdownMenuItem disabled>
                                  <CheckCircle className="h-4 w-4 mr-2" />
                                  Published
                                </DropdownMenuItem>
                              )}
                              <DropdownMenuItem>
                                <Copy className="h-4 w-4 mr-2" />
                                Duplicate
                              </DropdownMenuItem>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem onClick={() => handleEditReport(report)}>
                                <Edit className="h-4 w-4 mr-2" />
                                Edit
                              </DropdownMenuItem>
                              <DropdownMenuItem 
                                onClick={() => handleDeleteReport(report.id)}
                                className="text-red-600"
                              >
                                <Trash2 className="h-4 w-4 mr-2" />
                                Delete
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </div>
                      </CardHeader>
                      
                      <CardContent>
                        <CardDescription className="mb-4">
                          {report.description}
                        </CardDescription>
                        
                        <div className="space-y-2 text-sm text-muted-foreground">
                          <div className="flex justify-between">
                            <span>Created by:</span>
                            <span>{report.createdBy}</span>
                          </div>
                          {enhancedReport.publishedBy && (
                            <div className="flex justify-between">
                              <span>Published by:</span>
                              <span>{enhancedReport.publishedBy}</span>
                            </div>
                          )}
                          {enhancedReport.publishedAt && (
                            <div className="flex justify-between">
                              <span>Published:</span>
                              <span>{isClient ? new Date(enhancedReport.publishedAt).toLocaleDateString() : new Date(enhancedReport.publishedAt).toISOString().split('T')[0]}</span>
                            </div>
                          )}
                          {enhancedReport.assignedRoles && enhancedReport.assignedRoles.length > 0 && (
                            <div className="flex justify-between">
                              <span>Assigned roles:</span>
                              <span>{enhancedReport.assignedRoles.length} role(s)</span>
                            </div>
                          )}
                        </div>
                        
                        <div className="flex space-x-2 mt-4">
                          <Button 
                            size="sm" 
                            className="flex-1" 
                            onClick={() => handleRunReport(report.id)}
                            disabled={executingReportId === report.id}
                          >
                            {executingReportId === report.id ? (
                              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                            ) : (
                              <Zap className="h-4 w-4 mr-2" />
                            )}
                            Test
                          </Button>
                          <Button 
                            variant="outline" 
                            size="sm" 
                            onClick={() => handleViewResults(report.id, report.name)}
                          >
                            <Eye className="h-4 w-4 mr-2" />
                            View
                          </Button>
                          {!enhancedReport.isPublished ? (
                            <Button 
                              variant="outline" 
                              size="sm" 
                              onClick={() => handlePublishReport(report.id)}
                              className="text-green-600 border-green-600 hover:bg-green-50"
                            >
                              <CheckCircle className="h-4 w-4" />
                            </Button>
                          ) : (
                            <Button variant="outline" size="sm" disabled>
                              <CheckCircle className="h-4 w-4 text-green-600" />
                            </Button>
                          )}
                        </div>
                      </CardContent>
                    </Card>
                  );
                })}
              </div>
            )}

            {filteredReports.length === 0 && (
              <Card>
                <CardContent className="p-8 text-center">
                  <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                  <h3 className="text-lg font-medium mb-2">No reports found</h3>
                  <p className="text-muted-foreground mb-4">
                    {searchQuery || selectedType !== 'all' || selectedStatus !== 'all'
                      ? 'Try adjusting your search or filter criteria'
                      : 'Get started by creating your first report'
                    }
                  </p>
                  <Button onClick={() => setIsBuilderOpen(true)}>
                    <Plus className="h-4 w-4 mr-2" />
                    Create New Report
                  </Button>
                </CardContent>
              </Card>
            )}
          </TabsContent>

          <TabsContent value="assignments" className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle>Role Assignment Overview</CardTitle>
                <CardDescription>
                  Manage report access permissions across all roles and reports in your organization
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                  {/* Reports with Role Assignments */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center space-x-2">
                        <FileText className="h-5 w-5" />
                        <span>Reports by Assignment Status</span>
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-4">
                        {filteredReports.map((report) => {
                          const enhancedReport = report as EnhancedReportDefinition;
                          const assignedRoleCount = enhancedReport.assignedRoles?.length || 0;
                          return (
                            <div key={report.id} className="flex items-center justify-between p-3 border rounded-lg">
                              <div className="flex items-center space-x-3">
                                <div className={`p-2 rounded-lg ${getTypeColor(report.type)}`}>
                                  {getTypeIcon(report.type)}
                                </div>
                                <div>
                                  <p className="font-medium">{report.name}</p>
                                  <p className="text-sm text-muted-foreground">
                                    {assignedRoleCount} role(s) assigned
                                  </p>
                                </div>
                              </div>
                              <div className="flex items-center space-x-2">
                                {assignedRoleCount > 0 ? (
                                  <Badge variant="default">
                                    <UserCheck className="h-3 w-3 mr-1" />
                                    {assignedRoleCount} roles
                                  </Badge>
                                ) : (
                                  <Badge variant="secondary">
                                    <AlertTriangle className="h-3 w-3 mr-1" />
                                    No access
                                  </Badge>
                                )}
                                <Button
                                  size="sm"
                                  variant="outline"
                                  onClick={() => handleAssignRoles(report.id)}
                                >
                                  <Settings className="h-4 w-4 mr-1" />
                                  Configure
                                </Button>
                              </div>
                            </div>
                          );
                        })}
                        {filteredReports.length === 0 && (
                          <div className="text-center py-8">
                            <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                            <h3 className="text-lg font-medium mb-2">No Reports Found</h3>
                            <p className="text-muted-foreground">
                              Create some reports first to manage role assignments.
                            </p>
                          </div>
                        )}
                      </div>
                    </CardContent>
                  </Card>

                  {/* Roles Overview */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="text-lg flex items-center space-x-2">
                        <Shield className="h-5 w-5" />
                        <span>Available Roles</span>
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-4">
                        {roles.map((role) => {
                          const assignedReportCount = filteredReports.filter(r => 
                            (r as EnhancedReportDefinition).assignedRoles?.includes(role.name)
                          ).length;
                          return (
                            <div key={role.id} className="flex items-center justify-between p-3 border rounded-lg">
                              <div className="flex items-center space-x-3">
                                <div className="p-2 rounded-lg bg-blue-100 text-blue-700">
                                  <Shield className="h-4 w-4" />
                                </div>
                                <div>
                                  <p className="font-medium">{role.name}</p>
                                  <p className="text-sm text-muted-foreground">
                                    {role.description}
                                  </p>
                                </div>
                              </div>
                              <div className="flex items-center space-x-2">
                                <Badge variant="outline">
                                  {assignedReportCount} reports
                                </Badge>
                                {role.isSystemRole && (
                                  <Badge variant="secondary" className="text-xs">
                                    System
                                  </Badge>
                                )}
                              </div>
                            </div>
                          );
                        })}
                        {roles.length === 0 && (
                          <div className="text-center py-8">
                            <Shield className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                            <h3 className="text-lg font-medium mb-2">No Roles Found</h3>
                            <p className="text-muted-foreground">
                              Create roles first to assign them to reports.
                            </p>
                          </div>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                </div>

                {/* Quick Actions */}
                <Card className="mt-6">
                  <CardHeader>
                    <CardTitle className="text-lg">Quick Actions</CardTitle>
                    <CardDescription>
                      Common role assignment operations
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <div className="flex flex-wrap gap-3">
                      <Button variant="outline" className="flex items-center space-x-2">
                        <Plus className="h-4 w-4" />
                        <span>Bulk Assign Roles</span>
                      </Button>
                      <Button variant="outline" className="flex items-center space-x-2">
                        <Download className="h-4 w-4" />
                        <span>Export Assignments</span>
                      </Button>
                      <Button variant="outline" className="flex items-center space-x-2">
                        <Shield className="h-4 w-4" />
                        <span>Create New Role</span>
                      </Button>
                      <Button variant="outline" className="flex items-center space-x-2">
                        <Eye className="h-4 w-4" />
                        <span>View Access Report</span>
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="modules" className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle>Module Management</CardTitle>
                <CardDescription>
                  Organize reports by business modules and assign them to appropriate user groups
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {modules.map((module) => (
                    <Card key={module.id}>
                      <CardHeader className="pb-3">
                        <div className="flex items-center space-x-2">
                          <div className="p-2 rounded-lg bg-blue-100 text-blue-700">
                            {getModuleIcon(module.id)}
                          </div>
                          <div>
                            <CardTitle className="text-lg">{module.name}</CardTitle>
                          </div>
                        </div>
                      </CardHeader>
                      <CardContent>
                        <p className="text-sm text-muted-foreground mb-4">
                          {module.description}
                        </p>
                        <div className="flex justify-between items-center">
                          <span className="text-sm text-muted-foreground">
                            {filteredReports.filter(r => (r as EnhancedReportDefinition).moduleId === module.id).length} reports
                          </span>
                          <Button size="sm" variant="outline">
                            <Settings className="h-4 w-4 mr-2" />
                            Configure
                          </Button>
                        </div>
                      </CardContent>
                    </Card>
                  ))}
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="analytics">
            <AnalyticsDashboard />
          </TabsContent>

          <TabsContent value="templates">
            <ReportTemplates onCreateFromTemplate={(template) => setIsBuilderOpen(true)} />
          </TabsContent>
        </Tabs>

        {/* Report Builder Dialog */}
        <Dialog open={isBuilderOpen} onOpenChange={(open) => {
          setIsBuilderOpen(open);
          if (!open) {
            setEditingReport(null);
          }
        }}>
          <DialogContent className="max-w-6xl h-[90vh] flex flex-col p-0">
            <DialogHeader className="px-6 py-4 border-b flex-shrink-0">
              <DialogTitle>{editingReport ? 'Edit Report' : 'Report Builder'}</DialogTitle>
              <DialogDescription>
                {editingReport ? 'Modify your existing report' : 'Create custom reports with drag-and-drop interface'}
              </DialogDescription>
            </DialogHeader>
            <div className="flex-1 overflow-y-auto px-6 py-4">
              <ReportBuilder 
                onClose={() => {
                  setIsBuilderOpen(false);
                  setEditingReport(null);
                }} 
                onReportCreated={() => {
                  setIsBuilderOpen(false);
                  setEditingReport(null);
                  refetchReports();
                }}
                onTemplateCreated={() => {
                  // Optionally handle template creation
                  toast({
                    title: 'Template Created',
                    description: 'Report template has been saved and can be reused',
                  });
                }}
                editingReportId={editingReport?.id}
                editingReport={editingReport}
              />
            </div>
          </DialogContent>
        </Dialog>

        {/* Module Assignment Dialog */}
        <Dialog open={showModuleAssignDialog} onOpenChange={(open) => {
          setShowModuleAssignDialog(open);
          if (!open) {
            setSelectedReportForAssignment(null);
            setSelectedModuleForAssignment(null);
          }
        }}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Assign Module</DialogTitle>
              <DialogDescription>
                Choose which module this report belongs to
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-3 max-h-96 overflow-y-auto">
                {modules.map((module) => (
                  <Card 
                    key={module.id} 
                    className={`cursor-pointer hover:bg-muted/50 transition-colors ${
                      selectedModuleForAssignment === module.id ? 'ring-2 ring-primary bg-muted/50' : ''
                    }`} 
                    onClick={() => setSelectedModuleForAssignment(module.id)}
                  >
                    <CardHeader className="pb-3">
                      <div className="flex items-center justify-between">
                        <div className="flex items-center space-x-3">
                          <div className="p-2 rounded-lg bg-blue-100 text-blue-700">
                            {getModuleIcon(module.id)}
                          </div>
                          <div>
                            <CardTitle className="text-base">{module.name}</CardTitle>
                            <CardDescription className="text-sm">
                              {module.description}
                            </CardDescription>
                          </div>
                        </div>
                        {selectedModuleForAssignment === module.id && (
                          <CheckCircle className="h-5 w-5 text-primary" />
                        )}
                      </div>
                    </CardHeader>
                  </Card>
                ))}
              </div>
              
              <div className="flex justify-end space-x-3 pt-4 border-t">
                <Button 
                  variant="outline" 
                  onClick={() => {
                    setShowModuleAssignDialog(false);
                    setSelectedReportForAssignment(null);
                    setSelectedModuleForAssignment(null);
                  }}
                >
                  Cancel
                </Button>
                <Button 
                  onClick={() => {
                    if (selectedReportForAssignment && selectedModuleForAssignment) {
                      assignModuleMutation.mutate({ 
                        reportId: selectedReportForAssignment, 
                        moduleId: selectedModuleForAssignment 
                      });
                      setShowModuleAssignDialog(false);
                      setSelectedReportForAssignment(null);
                      setSelectedModuleForAssignment(null);
                    }
                  }}
                  disabled={!selectedModuleForAssignment || assignModuleMutation.isPending}
                >
                  {assignModuleMutation.isPending ? (
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  ) : (
                    <Package className="h-4 w-4 mr-2" />
                  )}
                  {assignModuleMutation.isPending ? 'Assigning...' : 'Assign Module'}
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>

        {/* Role Assignment Dialog */}
        <ReportRoleAssignmentDialog
          open={showRoleAssignDialog}
          onOpenChange={(open) => {
            setShowRoleAssignDialog(open);
            if (!open) {
              setSelectedReportForAssignment(null);
            }
          }}
          reportId={selectedReportForAssignment}
          reportName={reports.find(r => r.id === selectedReportForAssignment)?.name}
        />

        {/* Publish Confirmation Dialog */}
        <ConfirmationDialog
          open={showPublishDialog}
          onOpenChange={setShowPublishDialog}
          title="Publish Report"
          description="Are you sure you want to publish this report? Once published, it will be available to users with assigned roles."
          confirmText="Publish"
          cancelText="Cancel"
          variant="default"
          onConfirm={() => {
            if (selectedReportForAssignment) {
              publishReportMutation.mutate(selectedReportForAssignment);
              setSelectedReportForAssignment(null);
            }
            setShowPublishDialog(false);
          }}
          isLoading={publishReportMutation.isPending}
        />

        {/* Delete Confirmation Dialog */}
        <ConfirmationDialog
          open={showDeleteDialog}
          onOpenChange={setShowDeleteDialog}
          title="Delete Report"
          description="Are you sure you want to delete this report? This action cannot be undone and all associated schedules will also be removed."
          confirmText="Delete"
          cancelText="Cancel"
          variant="destructive"
          onConfirm={confirmDeleteReport}
          isLoading={deleteReportMutation.isPending}
        />

        {/* Report Results Dialog */}
        <ReportResultsDialog
          reportId={selectedReportForResults?.id || null}
          reportName={selectedReportForResults?.name}
          open={showResultsDialog}
          onOpenChange={(open) => {
            setShowResultsDialog(open);
            if (!open) {
              setSelectedReportForResults(null);
            }
          }}
        />
      </div>
    </TenantGuard>
  );
}