'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { DashboardLayout } from '../../components/layout/dashboard-layout';
import { Button } from '../../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card';
import { Badge } from '../../components/ui/badge';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '../../components/ui/dialog';
import { ConfirmationDialog } from '../../components/ui/confirmation-dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../../components/ui/dropdown-menu';
import {
  Database,
  Plus,
  Search,
  Settings,
  TestTube,
  Eye,
  Edit,
  Trash2,
  RefreshCw,
  Loader2,
  CheckCircle,
  XCircle,
  Clock,
  AlertCircle
} from 'lucide-react';
import { useToast } from '../../hooks/use-toast';
import { 
  dataSourcesService, 
  DataSource, 
  CreateDataSourceDto, 
  DataSourceType,
  ConnectionStatus 
} from '../../services/dataSources';

export default function DataSourcesPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
  const [selectedDataSource, setSelectedDataSource] = useState<DataSource | null>(null);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [dataSourceToDelete, setDataSourceToDelete] = useState<string | null>(null);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Fetch data sources
  const {
    data: dataSources = [],
    isLoading,
    error,
    refetch
  } = useQuery({
    queryKey: ['dataSources'],
    queryFn: () => dataSourcesService.getDataSources(),
    refetchOnWindowFocus: false,
  });

  // Test connection mutation
  const testConnectionMutation = useMutation({
    mutationFn: (dataSourceId: string) => 
      dataSourcesService.testDataSourceConnection(dataSourceId),
    onSuccess: (result, dataSourceId) => {
      if (result.isSuccess) {
        toast({
          title: 'Connection Successful',
          description: `Connected in ${result.responseTime}ms`,
        });
      } else {
        toast({
          title: 'Connection Failed',
          description: result.errorMessage || 'Unknown error',
          variant: 'destructive',
        });
      }
      queryClient.invalidateQueries({ queryKey: ['dataSources'] });
    },
    onError: (error: any) => {
      toast({
        title: 'Test Failed',
        description: error.response?.data?.message || 'Failed to test connection',
        variant: 'destructive',
      });
    },
  });

  // Delete data source mutation
  const deleteDataSourceMutation = useMutation({
    mutationFn: dataSourcesService.deleteDataSource,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['dataSources'] });
      toast({
        title: 'Data Source Deleted',
        description: 'Data source has been deleted successfully',
      });
    },
    onError: (error: any) => {
      toast({
        title: 'Delete Failed',
        description: error.response?.data?.message || 'Failed to delete data source',
        variant: 'destructive',
      });
    },
  });

  const filteredDataSources = dataSources.filter(ds =>
    ds.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    ds.description?.toLowerCase().includes(searchQuery.toLowerCase()) ||
    ds.typeName.toLowerCase().includes(searchQuery.toLowerCase())
  );

  const getStatusIcon = (status: ConnectionStatus) => {
    switch (status) {
      case ConnectionStatus.Connected:
        return <CheckCircle className="h-4 w-4 text-green-600" />;
      case ConnectionStatus.Error:
        return <XCircle className="h-4 w-4 text-red-600" />;
      case ConnectionStatus.Testing:
        return <Loader2 className="h-4 w-4 text-blue-600 animate-spin" />;
      case ConnectionStatus.Disconnected:
        return <AlertCircle className="h-4 w-4 text-gray-600" />;
      default:
        return <Clock className="h-4 w-4 text-gray-400" />;
    }
  };

  const handleTestConnection = (dataSourceId: string) => {
    testConnectionMutation.mutate(dataSourceId);
  };

  const handleDeleteDataSource = (dataSourceId: string) => {
    setDataSourceToDelete(dataSourceId);
    setShowDeleteDialog(true);
  };

  const confirmDeleteDataSource = () => {
    if (dataSourceToDelete) {
      deleteDataSourceMutation.mutate(dataSourceToDelete);
      setDataSourceToDelete(null);
    }
  };

  if (isLoading) {
    return (
      <DashboardLayout>
        <div className="flex items-center justify-center min-h-screen">
          <div className="text-center space-y-4">
            <Loader2 className="h-8 w-8 animate-spin mx-auto" />
            <p className="text-muted-foreground">Loading data sources...</p>
          </div>
        </div>
      </DashboardLayout>
    );
  }

  if (error) {
    return (
      <DashboardLayout>
        <div className="flex items-center justify-center min-h-screen">
          <div className="text-center space-y-4">
            <div className="text-red-600">
              <Database className="h-12 w-12 mx-auto mb-4" />
              <h2 className="text-xl font-semibold">Error Loading Data Sources</h2>
              <p className="text-muted-foreground mt-2">
                Failed to load data sources. Please try again.
              </p>
            </div>
            <Button onClick={() => refetch()}>
              <RefreshCw className="h-4 w-4 mr-2" />
              Retry
            </Button>
          </div>
        </div>
      </DashboardLayout>
    );
  }

  return (
    <DashboardLayout>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Data Sources</h1>
            <p className="text-muted-foreground">
              Manage external data connections for reports and analytics
            </p>
          </div>
          
          <div className="flex items-center space-x-2">
            <Button 
              variant="outline" 
              onClick={() => refetch()}
              disabled={isLoading}
            >
              <RefreshCw className={`h-4 w-4 mr-2 ${isLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
            <Button onClick={() => setIsCreateDialogOpen(true)}>
              <Plus className="h-4 w-4 mr-2" />
              Add Data Source
            </Button>
          </div>
        </div>

        {/* Summary Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Total Sources</p>
                  <p className="text-2xl font-bold">{dataSources.length}</p>
                </div>
                <Database className="h-8 w-8 text-blue-600" />
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Active Sources</p>
                  <p className="text-2xl font-bold">
                    {dataSources.filter(ds => ds.isActive).length}
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
                  <p className="text-sm font-medium text-muted-foreground">Connected</p>
                  <p className="text-2xl font-bold">
                    {dataSources.filter(ds => ds.status === ConnectionStatus.Connected).length}
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
                  <p className="text-sm font-medium text-muted-foreground">Errors</p>
                  <p className="text-2xl font-bold">
                    {dataSources.filter(ds => ds.status === ConnectionStatus.Error).length}
                  </p>
                </div>
                <XCircle className="h-8 w-8 text-red-600" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Search Bar */}
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground h-4 w-4" />
          <Input
            placeholder="Search data sources..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="pl-10"
          />
        </div>

        {/* Data Sources Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredDataSources.map((dataSource) => (
            <Card key={dataSource.id} className="hover:shadow-lg transition-shadow">
              <CardHeader className="pb-3">
                <div className="flex items-start justify-between">
                  <div className="flex items-center space-x-2">
                    <div className="p-2 bg-blue-100 rounded-lg">
                      <Database className="h-5 w-5 text-blue-600" />
                    </div>
                    <div>
                      <CardTitle className="text-lg">{dataSource.name}</CardTitle>
                      <div className="flex items-center space-x-2 mt-1">
                        <Badge variant="outline">{dataSource.typeName}</Badge>
                        {!dataSource.isActive && (
                          <Badge variant="secondary">Inactive</Badge>
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
                        onClick={() => handleTestConnection(dataSource.id)}
                        disabled={testConnectionMutation.isPending}
                      >
                        <TestTube className="h-4 w-4 mr-2" />
                        Test Connection
                      </DropdownMenuItem>
                      <DropdownMenuItem onClick={() => setSelectedDataSource(dataSource)}>
                        <Eye className="h-4 w-4 mr-2" />
                        View Details
                      </DropdownMenuItem>
                      <DropdownMenuSeparator />
                      <DropdownMenuItem>
                        <Edit className="h-4 w-4 mr-2" />
                        Edit
                      </DropdownMenuItem>
                      <DropdownMenuItem 
                        onClick={() => handleDeleteDataSource(dataSource.id)}
                        className="text-red-600"
                        disabled={deleteDataSourceMutation.isPending}
                      >
                        <Trash2 className="h-4 w-4 mr-2" />
                        Delete
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>
              </CardHeader>
              
              <CardContent className="space-y-4">
                <p className="text-sm text-muted-foreground">
                  {dataSource.description || 'No description provided'}
                </p>

                {/* Connection Details */}
                <div className="space-y-2 text-sm">
                  {dataSource.host && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Host:</span>
                      <span>{dataSource.host}{dataSource.port ? `:${dataSource.port}` : ''}</span>
                    </div>
                  )}
                  {dataSource.databaseName && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Database:</span>
                      <span>{dataSource.databaseName}</span>
                    </div>
                  )}
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Usage:</span>
                    <span>{dataSource.usageCount} times</span>
                  </div>
                </div>

                {/* Status */}
                <div className="flex items-center justify-between pt-4 border-t">
                  <div className="flex items-center space-x-2">
                    {getStatusIcon(dataSource.status)}
                    <span className={`text-sm ${dataSourcesService.getConnectionStatusColor(dataSource.status)}`}>
                      {dataSourcesService.getConnectionStatusLabel(dataSource.status)}
                    </span>
                  </div>
                  <div className="flex space-x-2">
                    <Button 
                      size="sm" 
                      variant="outline"
                      onClick={() => handleTestConnection(dataSource.id)}
                      disabled={testConnectionMutation.isPending}
                    >
                      {testConnectionMutation.isPending && testConnectionMutation.variables === dataSource.id ? (
                        <Loader2 className="h-3 w-3 animate-spin" />
                      ) : (
                        <TestTube className="h-3 w-3" />
                      )}
                    </Button>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>

        {filteredDataSources.length === 0 && (
          <Card>
            <CardContent className="p-12 text-center">
              <Database className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
              <h3 className="text-lg font-medium mb-2">No Data Sources Found</h3>
              <p className="text-muted-foreground mb-4">
                {searchQuery ? 'Try adjusting your search criteria' : 'Get started by adding your first data source'}
              </p>
              <Button onClick={() => setIsCreateDialogOpen(true)}>
                <Plus className="h-4 w-4 mr-2" />
                Add Data Source
              </Button>
            </CardContent>
          </Card>
        )}

        {/* Create Data Source Dialog - Placeholder */}
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Add New Data Source</DialogTitle>
              <DialogDescription>
                Connect to external databases, APIs, or file sources for reporting
              </DialogDescription>
            </DialogHeader>
            <div className="p-4 text-center text-muted-foreground">
              <Database className="h-12 w-12 mx-auto mb-4" />
              <p>Data source creation form will be implemented soon.</p>
              <p>This will include connection settings for all supported data source types.</p>
            </div>
          </DialogContent>
        </Dialog>

        {/* View Data Source Details Dialog */}
        <Dialog open={!!selectedDataSource} onOpenChange={() => setSelectedDataSource(null)}>
          <DialogContent className="max-w-3xl">
            <DialogHeader>
              <DialogTitle>{selectedDataSource?.name}</DialogTitle>
              <DialogDescription>
                {selectedDataSource?.description || 'Data source details'}
              </DialogDescription>
            </DialogHeader>
            {selectedDataSource && (
              <div className="space-y-6">
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label className="text-sm font-medium">Type</Label>
                    <p className="text-sm text-muted-foreground">{selectedDataSource.typeName}</p>
                  </div>
                  <div>
                    <Label className="text-sm font-medium">Status</Label>
                    <div className="flex items-center space-x-2 mt-1">
                      {getStatusIcon(selectedDataSource.status)}
                      <span className={`text-sm ${dataSourcesService.getConnectionStatusColor(selectedDataSource.status)}`}>
                        {dataSourcesService.getConnectionStatusLabel(selectedDataSource.status)}
                      </span>
                    </div>
                  </div>
                  <div>
                    <Label className="text-sm font-medium">Created By</Label>
                    <p className="text-sm text-muted-foreground">{selectedDataSource.createdBy}</p>
                  </div>
                  <div>
                    <Label className="text-sm font-medium">Usage Count</Label>
                    <p className="text-sm text-muted-foreground">{selectedDataSource.usageCount} times</p>
                  </div>
                </div>
                
                {selectedDataSource.lastConnectionError && (
                  <div className="p-4 bg-red-50 rounded-lg">
                    <Label className="text-sm font-medium text-red-800">Last Connection Error</Label>
                    <p className="text-sm text-red-700 mt-1">{selectedDataSource.lastConnectionError}</p>
                  </div>
                )}
              </div>
            )}
          </DialogContent>
        </Dialog>

        {/* Delete Confirmation Dialog */}
        <ConfirmationDialog
          open={showDeleteDialog}
          onOpenChange={setShowDeleteDialog}
          title="Delete Data Source"
          description="Are you sure you want to delete this data source? This action cannot be undone and may affect existing reports that depend on this connection."
          confirmText="Delete"
          cancelText="Cancel"
          variant="destructive"
          onConfirm={confirmDeleteDataSource}
          isLoading={deleteDataSourceMutation.isPending}
        />
      </div>
    </DashboardLayout>
  );
}
