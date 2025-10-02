'use client';

import React, { useState } from 'react';
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
  Loader2
} from 'lucide-react';
import { Input } from '../../components/ui/input';
import { ConfirmationDialog } from '../../components/ui/confirmation-dialog';
import { useToast } from '../../hooks/use-toast';
import { reportsService, ReportDefinition, ReportAnalytics } from '../../services/reports';
import { useIsClient } from '../../lib/ssr-utils';
import ReportBuilder from '../../components/reports/ReportBuilder';
import AnalyticsDashboard from '../../components/reports/AnalyticsDashboard';
import ReportTemplates from '../../components/reports/ReportTemplates';
import DataExportTools from '../../components/reports/DataExportTools';
import ReportResultsDialog from '../../components/reports/ReportResultsDialog';

export default function ReportsPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedType, setSelectedType] = useState<string>('all');
  const [isBuilderOpen, setIsBuilderOpen] = useState(false);
  const [selectedReportId, setSelectedReportId] = useState<string | null>(null);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [reportToDelete, setReportToDelete] = useState<string | null>(null);
  const [executingReportId, setExecutingReportId] = useState<string | null>(null);
  const [showResultsDialog, setShowResultsDialog] = useState(false);
  const [selectedReportForResults, setSelectedReportForResults] = useState<{ id: string; name: string } | null>(null);
  const isClient = useIsClient();
  
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Fetch reports
  const {
    data: reports = [],
    isLoading: reportsLoading,
    error: reportsError,
    refetch: refetchReports
  } = useQuery({
    queryKey: ['reports', selectedType !== 'all' ? selectedType : undefined],
    queryFn: () => reportsService.getReports(
      selectedType !== 'all' ? selectedType : undefined
    ),
    refetchOnWindowFocus: false,
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

  // Mutations
  const executeReportMutation = useMutation({
    mutationFn: ({ reportId, params }: { reportId: string; params?: any }) => {
      setExecutingReportId(reportId);
      return reportsService.executeReport(reportId, { parameters: params, includeMetadata: true });
    },
    onSuccess: (data) => {
      setExecutingReportId(null);
      toast({
        title: 'Report Executed Successfully',
        description: `Generated ${data.totalRows} rows in ${data.executionTime}`,
      });
      // Could open a results dialog here
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
      queryClient.invalidateQueries({ queryKey: ['reports'] });
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

  const toggleFavoriteMutation = useMutation({
    mutationFn: reportsService.toggleFavorite,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reports'] });
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update favorite status',
        variant: 'destructive',
      });
    },
  });

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

  const getStatusBadge = (status: string) => {
    switch (status.toLowerCase()) {
      case 'published':
        return <Badge variant="default">Published</Badge>;
      case 'draft':
        return <Badge variant="secondary">Draft</Badge>;
      case 'scheduled':
        return <Badge variant="outline">Scheduled</Badge>;
      default:
        return <Badge variant="secondary">{status}</Badge>;
    }
  };

  const filteredReports = reports.filter(report => {
    const matchesSearch = report.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                         report.description.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesType = selectedType === 'all' || report.type.toLowerCase() === selectedType.toLowerCase();
    return matchesSearch && matchesType;
  });

  const handleRunReport = (reportId: string) => {
    executeReportMutation.mutate({ reportId });
  };

  const handleScheduleReport = (reportId: string) => {
    // TODO: Open schedule dialog
    toast({
      title: 'Schedule Report',
      description: 'Report scheduling dialog will be implemented soon.',
    });
  };

  const handleExportReport = async (reportId: string, format: 'pdf' | 'csv' | 'xlsx' | 'json') => {
    try {
      const blob = await reportsService.exportReport(reportId, { format });
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.style.display = 'none';
      a.href = url;
      a.download = `report-${reportId}.${format}`;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      
      toast({
        title: 'Export Successful',
        description: `Report exported as ${format.toUpperCase()}`,
      });
    } catch (error: any) {
      toast({
        title: 'Export Failed',
        description: error.response?.data?.message || 'Failed to export report',
        variant: 'destructive',
      });
    }
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
  };

  const handleToggleFavorite = (reportId: string) => {
    toggleFavoriteMutation.mutate(reportId);
  };

  const handleViewResults = (reportId: string, reportName: string) => {
    setSelectedReportForResults({ id: reportId, name: reportName });
    setShowResultsDialog(true);
  };

  const handleDuplicateReport = (reportId: string) => {
    // TODO: Implement report duplication
    toast({
      title: 'Duplicate Report',
      description: 'Report duplication feature will be implemented soon.',
    });
  };

  const handleShareReport = (reportId: string) => {
    // TODO: Implement report sharing
    toast({
      title: 'Share Report',
      description: 'Report sharing feature will be implemented soon.',
    });
  };

  const handleEditReport = (reportId: string) => {
    // TODO: Implement report editing
    toast({
      title: 'Edit Report',
      description: 'Report editing feature will be implemented soon.',
    });
  };

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
            <h1 className="text-3xl font-bold tracking-tight">Reports & Analytics</h1>
            <p className="text-muted-foreground">
              Create, manage, and analyze comprehensive business reports
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

        {/* Analytics Overview Cards */}
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
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
                  <p className="text-sm font-medium text-muted-foreground">Data Sources</p>
                  <p className="text-2xl font-bold">
                    {analyticsLoading ? (
                      <Loader2 className="h-6 w-6 animate-spin" />
                    ) : (
                      analytics?.dataSourcesConnected || 0
                    )}
                  </p>
                </div>
                <Database className="h-8 w-8 text-purple-600" />
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

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Avg Time</p>
                  <p className="text-2xl font-bold">
                    {analyticsLoading ? (
                      <Loader2 className="h-6 w-6 animate-spin" />
                    ) : (
                      `${analytics?.avgGenerationTime || 0}s`
                    )}
                  </p>
                </div>
                <Target className="h-8 w-8 text-red-600" />
              </div>
            </CardContent>
          </Card>
        </div>

        <Tabs defaultValue="reports" className="space-y-4">
          <TabsList>
            <TabsTrigger value="reports">My Reports</TabsTrigger>
            <TabsTrigger value="analytics">Analytics Dashboard</TabsTrigger>
            <TabsTrigger value="templates">Templates</TabsTrigger>
            <TabsTrigger value="exports">Data Export</TabsTrigger>
          </TabsList>

          <TabsContent value="reports" className="space-y-4">
            {/* Search and Filter Bar */}
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
                {filteredReports.map((report) => (
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
                              {getStatusBadge(report.status)}
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
                            {executingReportId === report.id ? 'Running...' : 'Run Now'}
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => handleViewResults(report.id, report.name)}>
                            <Eye className="h-4 w-4 mr-2" />
                            View Results
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem onClick={() => handleScheduleReport(report.id)}>
                            <Calendar className="h-4 w-4 mr-2" />
                            Schedule
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => handleExportReport(report.id, 'pdf')}>
                            <Download className="h-4 w-4 mr-2" />
                            Export PDF
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => handleDuplicateReport(report.id)}>
                            <Copy className="h-4 w-4 mr-2" />
                            Duplicate
                          </DropdownMenuItem>
                          <DropdownMenuItem onClick={() => handleShareReport(report.id)}>
                            <Share2 className="h-4 w-4 mr-2" />
                            Share
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem onClick={() => handleEditReport(report.id)}>
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
                      {report.lastRun && (
                        <div className="flex justify-between">
                          <span>Last run:</span>
                          <span>{isClient ? new Date(report.lastRun).toLocaleDateString() : new Date(report.lastRun).toISOString().split('T')[0]}</span>
                        </div>
                      )}
                      {report.isScheduled && report.nextRun && (
                        <div className="flex justify-between">
                          <span>Next run:</span>
                          <span>{isClient ? new Date(report.nextRun).toLocaleDateString() : new Date(report.nextRun).toISOString().split('T')[0]}</span>
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
                        Run
                      </Button>
                      <Button 
                        variant="outline" 
                        size="sm" 
                        onClick={() => handleViewResults(report.id, report.name)}
                      >
                        <Eye className="h-4 w-4 mr-2" />
                        View
                      </Button>
                      <Button 
                        variant="outline" 
                        size="sm" 
                        onClick={() => handleToggleFavorite(report.id)}
                        disabled={toggleFavoriteMutation.isPending}
                      >
                        {report.isFavorite ? (
                          <span className="text-yellow-600">★</span>
                        ) : (
                          <span>☆</span>
                        )}
                      </Button>
                    </div>
                  </CardContent>
                </Card>
                ))}
              </div>
            )}

            {filteredReports.length === 0 && (
              <Card>
                <CardContent className="p-8 text-center">
                  <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                  <h3 className="text-lg font-medium mb-2">No reports found</h3>
                  <p className="text-muted-foreground mb-4">
                    {searchQuery || selectedType !== 'all' 
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

          <TabsContent value="analytics">
            <AnalyticsDashboard />
          </TabsContent>

          <TabsContent value="templates">
            <ReportTemplates onCreateFromTemplate={(template) => setIsBuilderOpen(true)} />
          </TabsContent>

          <TabsContent value="exports">
            <DataExportTools />
          </TabsContent>
        </Tabs>

        {/* Report Builder Dialog */}
        <Dialog open={isBuilderOpen} onOpenChange={setIsBuilderOpen}>
          <DialogContent className="max-w-6xl h-[90vh] flex flex-col p-0">
            <DialogHeader className="px-6 py-4 border-b flex-shrink-0">
              <DialogTitle>Report Builder</DialogTitle>
              <DialogDescription>
                Create custom reports with drag-and-drop interface
              </DialogDescription>
            </DialogHeader>
            <div className="flex-1 overflow-y-auto px-6 py-4">
              <ReportBuilder onClose={() => setIsBuilderOpen(false)} />
            </div>
          </DialogContent>
        </Dialog>

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
    </DashboardLayout>
    </TenantGuard>
  );
}
