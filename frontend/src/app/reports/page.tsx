'use client';

import React, { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { TenantGuard } from '../../components/auth/tenant-guard';
import { Button } from '../../components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { Badge } from '../../components/ui/badge';
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '../../components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '../../components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../../components/ui/dropdown-menu';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../components/ui/select';
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
  Package,
  Shield,
  CheckCircle,
  Star,
  History,
  Play,
  ChevronRight,
  BookOpen,
  Bookmark
} from 'lucide-react';
import { Input } from '../../components/ui/input';
import { useToast } from '../../hooks/use-toast';
import { reportsService, ReportDefinition, ReportAnalytics } from '../../services/reports';
import { useIsClient } from '../../lib/ssr-utils';
import ReportResultsDialog from '../../components/reports/ReportResultsDialog';

// User-facing interfaces with tenant awareness
interface Module {
  id: string;
  name: string;
  description: string;
  icon: string;
  reportCount: number;
  color: string;
}

interface UserReportDefinition extends ReportDefinition {
  moduleId: string;
  moduleName: string;
  isPublished: boolean;
  canExecute: boolean;
  lastExecuted?: string;
  executionCount?: number;
  avgExecutionTime?: string;
  parameters?: ReportParameter[];
}

interface ReportParameter {
  name: string;
  type: string;
  value: any;
  required?: boolean;
  options?: string[];
  label: string;
}

export default function UserReportsPage() {
  const router = useRouter();
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedModule, setSelectedModule] = useState<string>('all');
  const [selectedReportId, setSelectedReportId] = useState<string | null>(null);
  const [executingReportId, setExecutingReportId] = useState<string | null>(null);
  const [showResultsDialog, setShowResultsDialog] = useState(false);
  const [selectedReportForResults, setSelectedReportForResults] = useState<{ id: string; name: string } | null>(null);
  const [showFiltersDialog, setShowFiltersDialog] = useState(false);
  const [reportFilters, setReportFilters] = useState<Record<string, any>>({});
  const [showFavorites, setShowFavorites] = useState(false);
  const isClient = useIsClient();
  
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Mock modules data (in real app, this would be filtered by user's tenant and role permissions)
  const modules: Module[] = [
    { 
      id: 'financial', 
      name: 'Financial Reports', 
      description: 'Financial statements, budgets, and analysis', 
      icon: 'DollarSign', 
      reportCount: 12,
      color: 'bg-green-100 text-green-700 border-green-200'
    },
    { 
      id: 'sales', 
      name: 'Sales Reports', 
      description: 'Sales performance, pipelines, and forecasts', 
      icon: 'TrendingUp', 
      reportCount: 8,
      color: 'bg-blue-100 text-blue-700 border-blue-200'
    },
    { 
      id: 'hr', 
      name: 'HR Reports', 
      description: 'Employee data, payroll, and performance', 
      icon: 'Users', 
      reportCount: 15,
      color: 'bg-purple-100 text-purple-700 border-purple-200'
    },
    { 
      id: 'inventory', 
      name: 'Inventory Reports', 
      description: 'Stock levels, movements, and valuations', 
      icon: 'Package', 
      reportCount: 6,
      color: 'bg-orange-100 text-orange-700 border-orange-200'
    },
    { 
      id: 'operations', 
      name: 'Operations Reports', 
      description: 'Operational KPIs, efficiency, and metrics', 
      icon: 'Activity', 
      reportCount: 10,
      color: 'bg-indigo-100 text-indigo-700 border-indigo-200'
    },
    {
      id: 'maintenance',
      name: 'Maintenance Reports',
      description: 'Asset movements, inspections, work orders, parts issues, and maintenance costs',
      icon: 'Activity',
      reportCount: 5,
      color: 'bg-cyan-100 text-cyan-700 border-cyan-200'
    },
  ];

  // Fetch published reports available to current user's role and tenant
  const {
    data: reports = [],
    isLoading: reportsLoading,
    error: reportsError,
    refetch: refetchReports
  } = useQuery({
    queryKey: ['user-reports', selectedModule !== 'all' ? selectedModule : undefined],
    queryFn: () => reportsService.getReports(
      selectedModule !== 'all' ? selectedModule : undefined,
      'published' // Only fetch published reports for users
    ),
    refetchOnWindowFocus: false,
  });

  // Fetch user's recent report executions
  const {
    data: recentReports = [],
    isLoading: recentLoading
  } = useQuery({
    queryKey: ['user-recent-reports'],
    queryFn: () => {
      // TODO: Implement getUserRecentReports API call
      return Promise.resolve([]);
    },
    refetchOnWindowFocus: false,
  });

  // Execute report mutation with parameter support
  const executeReportMutation = useMutation({
    mutationFn: ({ reportId, params }: { reportId: string; params?: any }) => {
      setExecutingReportId(reportId);
      return reportsService.executeReport(reportId, { 
        parameters: { ...reportFilters, ...params }, 
        maxRows: 1000, 
        includeMetadata: true 
      });
    },
    onSuccess: (data) => {
      setExecutingReportId(null);
      setShowFiltersDialog(false);
      setReportFilters({});
      // Automatically open results dialog
      const report = reports.find(r => r.id === selectedReportId);
      if (report) {
        setSelectedReportForResults({ id: report.id, name: report.name });
        setShowResultsDialog(true);
      }
      toast({
        title: 'Report Generated Successfully',
        description: `Generated ${data.totalRows} rows in ${data.executionTime}`,
      });
    },
    onError: (error: any) => {
      setExecutingReportId(null);
      toast({
        title: 'Report Generation Failed',
        description: error.response?.data?.message || 'Failed to generate report',
        variant: 'destructive',
      });
    },
  });

  const toggleFavoriteMutation = useMutation({
    mutationFn: reportsService.toggleFavorite,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['user-reports'] });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update favorite status',
        variant: 'destructive',
      });
    },
  });

  // Helper functions
  const getModuleIcon = (iconName: string) => {
    switch (iconName) {
      case 'DollarSign':
        return <DollarSign className="h-5 w-5" />;
      case 'TrendingUp':
        return <TrendingUp className="h-5 w-5" />;
      case 'Users':
        return <Users className="h-5 w-5" />;
      case 'Package':
        return <Package className="h-5 w-5" />;
      case 'Activity':
        return <Activity className="h-5 w-5" />;
      default:
        return <FileText className="h-5 w-5" />;
    }
  };

  const getReportIcon = (type: string) => {
    switch (type.toLowerCase()) {
      case 'financial':
        return <DollarSign className="h-4 w-4" />;
      case 'sales':
        return <TrendingUp className="h-4 w-4" />;
      case 'hr':
      case 'users':
        return <Users className="h-4 w-4" />;
      case 'inventory':
        return <Package className="h-4 w-4" />;
      case 'operations':
        return <Activity className="h-4 w-4" />;
      default:
        return <FileText className="h-4 w-4" />;
    }
  };

  const getReportTypeColor = (type: string) => {
    switch (type.toLowerCase()) {
      case 'financial':
        return 'bg-green-100 text-green-700';
      case 'sales':
        return 'bg-blue-100 text-blue-700';
      case 'hr':
      case 'users':
        return 'bg-purple-100 text-purple-700';
      case 'inventory':
        return 'bg-orange-100 text-orange-700';
      case 'operations':
        return 'bg-indigo-100 text-indigo-700';
      default:
        return 'bg-gray-100 text-gray-700';
    }
  };

  // Event handlers
  const handleRunReport = (reportId: string, reportName?: string) => {
    const report = reports.find(r => r.id === reportId) as UserReportDefinition;
    
    // If report has parameters, show filters dialog
    if (report?.parameters && report.parameters.length > 0) {
      setSelectedReportId(reportId);
      setShowFiltersDialog(true);
    } else {
      // Execute directly if no parameters needed
      executeReportMutation.mutate({ reportId });
      setSelectedReportId(reportId);
    }
  };

  const handleViewResults = (reportId: string, reportName: string) => {
    setSelectedReportForResults({ id: reportId, name: reportName });
    setShowResultsDialog(true);
  };

  const handleToggleFavorite = (reportId: string) => {
    toggleFavoriteMutation.mutate(reportId);
  };

  const handleExportReport = async (reportId: string, format: 'pdf' | 'csv' | 'xlsx' | 'json') => {
    try {
      const { fileName, blob } = await reportsService.exportReport(reportId, { format });
      
      // Create a download link and trigger the download
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.style.display = 'none';
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      
      // Clean up
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      
      toast({
        title: 'Export Successful',
        description: `Report exported as ${format.toUpperCase()}: ${fileName}`,
      });
    } catch (error: any) {
      console.error('Export error:', error);
      toast({
        title: 'Export Failed',
        description: error.message || 'Failed to export report',
        variant: 'destructive',
      });
    }
  };

  // Filter reports
  const filteredReports = reports.filter(report => {
    const matchesSearch = report.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                         report.description.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesModule = selectedModule === 'all' || (report as UserReportDefinition).moduleId === selectedModule;
    const matchesFavorites = !showFavorites || report.isFavorite;
    return matchesSearch && matchesModule && matchesFavorites;
  });

  // Get current module info
  const currentModule = selectedModule !== 'all' ? modules.find(m => m.id === selectedModule) : null;

  if (reportsError) {
    return (
      <DashboardLayout>
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
      </DashboardLayout>
    );
  }

  return (
    <TenantGuard>
      <DashboardLayout>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Reports</h1>
            <p className="text-muted-foreground">
              Access published reports organized by business modules
            </p>
          </div>
          
          <div className="flex items-center space-x-2">
            <Button 
              variant={showFavorites ? "default" : "outline"}
              onClick={() => setShowFavorites(!showFavorites)}
            >
              <Star className={`h-4 w-4 mr-2 ${showFavorites ? 'fill-current' : ''}`} />
              Favorites
            </Button>
            <Button 
              variant="outline" 
              onClick={() => refetchReports()}
              disabled={reportsLoading}
            >
              <RefreshCw className={`h-4 w-4 mr-2 ${reportsLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          </div>
        </div>

        {/* Quick Stats */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Available Reports</p>
                  <p className="text-2xl font-bold">{filteredReports.length}</p>
                </div>
                <FileText className="h-8 w-8 text-blue-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Favorites</p>
                  <p className="text-2xl font-bold">{reports.filter(r => r.isFavorite).length}</p>
                </div>
                <Star className="h-8 w-8 text-yellow-600" />
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
                  <p className="text-sm font-medium text-muted-foreground">Recent Runs</p>
                  <p className="text-2xl font-bold">{recentReports.length}</p>
                </div>
                <History className="h-8 w-8 text-green-600" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Module Navigation */}
        {selectedModule === 'all' && (
          <div>
            <h2 className="text-xl font-semibold mb-4">Browse by Module</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {modules.map((module) => (
                <Card
                  key={module.id}
                  className="cursor-pointer hover:shadow-lg transition-shadow"
                  onClick={() => {
                    if (module.id === 'hr') {
                      router.push('/reports/hr');
                      return;
                    }

                    if (module.id === 'maintenance') {
                      router.push('/maintenance/reports');
                      return;
                    }

                    setSelectedModule(module.id);
                  }}
                >
                  <CardHeader className="pb-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center space-x-3">
                        <div className={`p-3 rounded-lg ${module.color}`}>
                          {getModuleIcon(module.icon)}
                        </div>
                        <div>
                          <CardTitle className="text-lg">{module.name}</CardTitle>
                          <CardDescription className="text-sm">
                            {module.reportCount} reports available
                          </CardDescription>
                        </div>
                      </div>
                      <ChevronRight className="h-5 w-5 text-muted-foreground" />
                    </div>
                  </CardHeader>
                  <CardContent>
                    <p className="text-sm text-muted-foreground">
                      {module.description}
                    </p>
                  </CardContent>
                </Card>
              ))}
            </div>
          </div>
        )}

        {/* Reports List */}
        {selectedModule !== 'all' && (
          <div className="space-y-4">
            {/* Breadcrumb */}
            <div className="flex items-center space-x-2 text-sm text-muted-foreground">
              <Button variant="ghost" size="sm" onClick={() => setSelectedModule('all')}>
                Reports
              </Button>
              <ChevronRight className="h-4 w-4" />
              <span className="font-medium text-foreground">
                {currentModule?.name}
              </span>
            </div>

            {/* Module Header */}
            {currentModule && (
              <Card className={`border-l-4 ${currentModule.color}`}>
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-4">
                      <div className={`p-3 rounded-lg ${currentModule.color}`}>
                        {getModuleIcon(currentModule.icon)}
                      </div>
                      <div>
                        <CardTitle className="text-xl">{currentModule.name}</CardTitle>
                        <CardDescription>{currentModule.description}</CardDescription>
                      </div>
                    </div>
                    <Badge variant="secondary">
                      {filteredReports.length} reports
                    </Badge>
                  </div>
                </CardHeader>
              </Card>
            )}

            {/* Search and Filter */}
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
            </div>

            {/* Reports Grid */}
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
                  const userReport = report as UserReportDefinition;
                  return (
                    <Card key={report.id} className="hover:shadow-lg transition-shadow">
                      <CardHeader className="pb-3">
                        <div className="flex items-start justify-between">
                          <div className="flex items-center space-x-2">
                            <div className={`p-2 rounded-lg ${getReportTypeColor(report.type)}`}>
                              {getReportIcon(report.type)}
                            </div>
                            <div className="flex-1">
                              <CardTitle className="text-lg">{report.name}</CardTitle>
                              <div className="flex items-center space-x-2 mt-1">
                                <Badge variant="outline" className="text-xs">
                                  <CheckCircle className="h-3 w-3 mr-1" />
                                  Published
                                </Badge>
                                {report.isFavorite && (
                                  <Badge variant="outline" className="text-yellow-600 text-xs">
                                    <Star className="h-3 w-3 mr-1 fill-current" />
                                    Favorite
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
                              <DropdownMenuItem onClick={() => handleViewResults(report.id, report.name)}>
                                <Eye className="h-4 w-4 mr-2" />
                                View Results
                              </DropdownMenuItem>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem onClick={() => handleExportReport(report.id, 'pdf')}>
                                <Download className="h-4 w-4 mr-2" />
                                Export PDF
                              </DropdownMenuItem>
                              <DropdownMenuItem onClick={() => handleExportReport(report.id, 'xlsx')}>
                                <Download className="h-4 w-4 mr-2" />
                                Export Excel
                              </DropdownMenuItem>
                              <DropdownMenuItem onClick={() => handleExportReport(report.id, 'csv')}>
                                <Download className="h-4 w-4 mr-2" />
                                Export CSV
                              </DropdownMenuItem>
                              <DropdownMenuSeparator />
                              <DropdownMenuItem onClick={() => handleToggleFavorite(report.id)}>
                                <Star className={`h-4 w-4 mr-2 ${report.isFavorite ? 'fill-current' : ''}`} />
                                {report.isFavorite ? 'Remove from Favorites' : 'Add to Favorites'}
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
                          {userReport.lastExecuted && (
                            <div className="flex justify-between">
                              <span>Last run:</span>
                              <span>{isClient ? new Date(userReport.lastExecuted).toLocaleDateString() : new Date(userReport.lastExecuted).toISOString().split('T')[0]}</span>
                            </div>
                          )}
                          {userReport.avgExecutionTime && (
                            <div className="flex justify-between">
                              <span>Avg time:</span>
                              <span>{userReport.avgExecutionTime}</span>
                            </div>
                          )}
                        </div>
                        
                        <div className="flex space-x-2 mt-4">
                          <Button 
                            size="sm" 
                            className="flex-1" 
                            onClick={() => handleRunReport(report.id, report.name)}
                            disabled={executingReportId === report.id}
                          >
                            {executingReportId === report.id ? (
                              <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                            ) : (
                              <Play className="h-4 w-4 mr-2" />
                            )}
                            Run Report
                          </Button>
                          <Button 
                            variant="outline" 
                            size="sm" 
                            onClick={() => handleToggleFavorite(report.id)}
                            disabled={toggleFavoriteMutation.isPending}
                          >
                            <Star className={`h-4 w-4 ${report.isFavorite ? 'fill-current text-yellow-600' : ''}`} />
                          </Button>
                        </div>
                      </CardContent>
                    </Card>
                  );
                })}
              </div>
            )}

            {filteredReports.length === 0 && !reportsLoading && (
              <Card>
                <CardContent className="p-8 text-center">
                  <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                  <h3 className="text-lg font-medium mb-2">No reports found</h3>
                  <p className="text-muted-foreground mb-4">
                    {searchQuery || showFavorites
                      ? 'Try adjusting your search criteria or filters'
                      : currentModule 
                        ? `No published reports available in ${currentModule.name} module`
                        : 'No reports available'
                    }
                  </p>
                  {searchQuery && (
                    <Button variant="outline" onClick={() => setSearchQuery('')}>
                      Clear Search
                    </Button>
                  )}
                </CardContent>
              </Card>
            )}
          </div>
        )}

        {/* Report Filters Dialog */}
        <Dialog open={showFiltersDialog} onOpenChange={setShowFiltersDialog}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Report Parameters</DialogTitle>
              <DialogDescription>
                Configure parameters for this report
              </DialogDescription>
            </DialogHeader>
            <div className="space-y-4">
              {/* TODO: Render dynamic report parameters based on report definition */}
              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium mb-2">Date Range</label>
                  <div className="grid grid-cols-2 gap-2">
                    <Input
                      type="date"
                      placeholder="Start Date"
                      onChange={(e) => setReportFilters(prev => ({ ...prev, startDate: e.target.value }))}
                    />
                    <Input
                      type="date"
                      placeholder="End Date"
                      onChange={(e) => setReportFilters(prev => ({ ...prev, endDate: e.target.value }))}
                    />
                  </div>
                </div>
                
                <div>
                  <label className="block text-sm font-medium mb-2">Department</label>
                  <Select onValueChange={(value) => setReportFilters(prev => ({ ...prev, department: value }))}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select Department" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All Departments</SelectItem>
                      <SelectItem value="sales">Sales</SelectItem>
                      <SelectItem value="marketing">Marketing</SelectItem>
                      <SelectItem value="operations">Operations</SelectItem>
                      <SelectItem value="finance">Finance</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
              
              <div className="flex justify-end space-x-2 pt-4">
                <Button variant="outline" onClick={() => setShowFiltersDialog(false)}>
                  Cancel
                </Button>
                <Button 
                  onClick={() => selectedReportId && executeReportMutation.mutate({ reportId: selectedReportId })}
                  disabled={executeReportMutation.isPending}
                >
                  {executeReportMutation.isPending ? (
                    <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  ) : (
                    <Play className="h-4 w-4 mr-2" />
                  )}
                  Generate Report
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>

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
    </DashboardLayout>
    </TenantGuard>
  );
}
