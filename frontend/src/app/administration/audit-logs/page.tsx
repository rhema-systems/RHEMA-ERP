'use client';

import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { DashboardLayout } from '../../../components/layout/dashboard-layout';
import { DataTable, Column } from '../../../components/admin/data-table';
import { Badge } from '../../../components/ui/badge';
import { Button } from '../../../components/ui/button';
import { Input } from '../../../components/ui/input';
import { Label } from '../../../components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/dialog';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/card';
import { adminApiService, AuditLog } from '../../../services/admin-api.service';
import { 
  FileText, 
  User, 
  Calendar, 
  Activity, 
  Eye,
  Filter,
  Download,
  RefreshCw
} from 'lucide-react';
import { format } from 'date-fns';

const ACTION_COLORS: Record<string, string> = {
  CREATE: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300',
  READ: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300',
  UPDATE: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300',
  DELETE: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300',
};

export default function AuditLogsPage() {
  const [selectedLog, setSelectedLog] = useState<AuditLog | null>(null);
  const [filters, setFilters] = useState({
    action: 'all',
    resource: '',
    user: '',
    dateFrom: '',
    dateTo: '',
  });

  // Fetch audit logs data
  const { data: auditLogs = [], isLoading, refetch } = useQuery({
    queryKey: ['audit-logs'],
    queryFn: () => adminApiService.getAuditLogs(),
  });

  // Filter logs based on current filters
  const filteredLogs = auditLogs.filter((log) => {
    if (filters.action && filters.action !== 'all' && log.action !== filters.action) return false;
    if (filters.resource && !log.resource.toLowerCase().includes(filters.resource.toLowerCase())) return false;
    if (filters.user && !log.username.toLowerCase().includes(filters.user.toLowerCase())) return false;
    if (filters.dateFrom && new Date(log.timestamp) < new Date(filters.dateFrom)) return false;
    if (filters.dateTo && new Date(log.timestamp) > new Date(filters.dateTo)) return false;
    return true;
  });

  const handleViewDetails = (log: AuditLog) => {
    setSelectedLog(log);
  };

  const handleExport = () => {
    // In a real application, this would generate and download a CSV/Excel file
    const csvContent = [
      ['Timestamp', 'User', 'Action', 'Resource', 'Resource ID', 'IP Address'],
      ...filteredLogs.map(log => [
        format(new Date(log.timestamp), 'yyyy-MM-dd HH:mm:ss'),
        log.username,
        log.action,
        log.resource,
        log.resourceId || '',
        log.ipAddress
      ])
    ].map(row => row.join(',')).join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `audit-logs-${format(new Date(), 'yyyy-MM-dd')}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  const clearFilters = () => {
    setFilters({
      action: 'all',
      resource: '',
      user: '',
      dateFrom: '',
      dateTo: '',
    });
  };

  const columns: Column<AuditLog>[] = [
    {
      key: 'timestamp',
      label: 'Timestamp',
      sortable: true,
      render: (timestamp: Date) => (
        <div className="flex items-center gap-2">
          <Calendar className="h-4 w-4 text-muted-foreground" />
          <div>
            <div className="font-medium text-sm">
              {format(new Date(timestamp), 'MMM dd, yyyy')}
            </div>
            <div className="text-xs text-muted-foreground">
              {format(new Date(timestamp), 'HH:mm:ss')}
            </div>
          </div>
        </div>
      ),
    },
    {
      key: 'username',
      label: 'User',
      sortable: true,
      render: (username: string) => (
        <div className="flex items-center gap-2">
          <User className="h-4 w-4 text-muted-foreground" />
          <span className="font-medium">{username}</span>
        </div>
      ),
    },
    {
      key: 'action',
      label: 'Action',
      render: (action: string) => (
        <Badge className={ACTION_COLORS[action] || 'bg-gray-100 text-gray-800'}>
          {action}
        </Badge>
      ),
    },
    {
      key: 'resource',
      label: 'Resource',
      render: (resource: string, log: AuditLog) => (
        <div className="flex items-center gap-2">
          <FileText className="h-4 w-4 text-muted-foreground" />
          <div>
            <div className="font-medium text-sm">{resource}</div>
            {log.resourceId && (
              <div className="text-xs text-muted-foreground">
                ID: {log.resourceId}
              </div>
            )}
          </div>
        </div>
      ),
    },
    {
      key: 'ipAddress',
      label: 'IP Address',
      render: (ipAddress: string) => (
        <Badge variant="outline" className="font-mono text-xs">
          {ipAddress}
        </Badge>
      ),
    },
  ];

  return (
    <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Audit Logs</h1>
            <p className="text-muted-foreground">
              Track system activities and user actions for security and compliance
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="outline" onClick={() => refetch()} disabled={isLoading}>
              <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
            <Button variant="outline" onClick={handleExport}>
              <Download className="h-4 w-4" />
              Export
            </Button>
          </div>
        </div>

        {/* Filters */}
        <Card>
          <CardHeader>
            <div className="flex items-center gap-2">
              <Filter className="h-4 w-4" />
              <CardTitle className="text-lg">Filters</CardTitle>
            </div>
            <CardDescription>
              Filter audit logs by various criteria
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
              <div className="space-y-2">
                <Label htmlFor="action">Action</Label>
                <Select
                  value={filters.action}
                  onValueChange={(value) => setFilters({ ...filters, action: value })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="All actions" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All actions</SelectItem>
                    <SelectItem value="CREATE">Create</SelectItem>
                    <SelectItem value="READ">Read</SelectItem>
                    <SelectItem value="UPDATE">Update</SelectItem>
                    <SelectItem value="DELETE">Delete</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="resource">Resource</Label>
                <Input
                  id="resource"
                  placeholder="Filter by resource"
                  value={filters.resource}
                  onChange={(e) => setFilters({ ...filters, resource: e.target.value })}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="user">User</Label>
                <Input
                  id="user"
                  placeholder="Filter by username"
                  value={filters.user}
                  onChange={(e) => setFilters({ ...filters, user: e.target.value })}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="dateFrom">Date From</Label>
                <Input
                  id="dateFrom"
                  type="date"
                  value={filters.dateFrom}
                  onChange={(e) => setFilters({ ...filters, dateFrom: e.target.value })}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="dateTo">Date To</Label>
                <Input
                  id="dateTo"
                  type="date"
                  value={filters.dateTo}
                  onChange={(e) => setFilters({ ...filters, dateTo: e.target.value })}
                />
              </div>
            </div>

            <div className="flex items-center gap-2 mt-4">
              <Button variant="outline" size="sm" onClick={clearFilters}>
                Clear Filters
              </Button>
              <div className="text-sm text-muted-foreground">
                Showing {filteredLogs.length} of {auditLogs.length} records
              </div>
            </div>
          </CardContent>
        </Card>

        <DataTable
          title="System Activity"
          description={`Comprehensive log of all system activities and user actions`}
          data={filteredLogs}
          columns={columns}
          loading={isLoading}
          searchable={false} // We have custom filters
          actions={true}
          onView={handleViewDetails}
          customActions={(log: AuditLog) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => handleViewDetails(log)}
              className="h-8 px-2"
            >
              <Eye className="h-4 w-4" />
            </Button>
          )}
        />

        {/* Log Details Dialog */}
        <Dialog open={!!selectedLog} onOpenChange={() => setSelectedLog(null)}>
          <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Activity className="h-5 w-5" />
                Audit Log Details
              </DialogTitle>
              <DialogDescription>
                Detailed information about the selected audit log entry
              </DialogDescription>
            </DialogHeader>

            {selectedLog && (
              <div className="space-y-6">
                {/* Basic Information */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-2">
                    <Label className="text-sm font-medium">Timestamp</Label>
                    <div className="text-sm">
                      {format(new Date(selectedLog.timestamp), 'PPpp')}
                    </div>
                  </div>
                  
                  <div className="space-y-2">
                    <Label className="text-sm font-medium">User</Label>
                    <div className="flex items-center gap-2">
                      <User className="h-4 w-4 text-muted-foreground" />
                      <span>{selectedLog.username}</span>
                      <Badge variant="outline" className="text-xs">
                        ID: {selectedLog.userId}
                      </Badge>
                    </div>
                  </div>

                  <div className="space-y-2">
                    <Label className="text-sm font-medium">Action</Label>
                    <Badge className={ACTION_COLORS[selectedLog.action] || 'bg-gray-100 text-gray-800'}>
                      {selectedLog.action}
                    </Badge>
                  </div>

                  <div className="space-y-2">
                    <Label className="text-sm font-medium">IP Address</Label>
                    <Badge variant="outline" className="font-mono">
                      {selectedLog.ipAddress}
                    </Badge>
                  </div>

                  <div className="space-y-2">
                    <Label className="text-sm font-medium">Resource</Label>
                    <div className="flex items-center gap-2">
                      <FileText className="h-4 w-4 text-muted-foreground" />
                      <span>{selectedLog.resource}</span>
                    </div>
                  </div>

                  {selectedLog.resourceId && (
                    <div className="space-y-2">
                      <Label className="text-sm font-medium">Resource ID</Label>
                      <Badge variant="outline" className="font-mono">
                        {selectedLog.resourceId}
                      </Badge>
                    </div>
                  )}
                </div>

                {/* Changes */}
                {(selectedLog.oldValues || selectedLog.newValues) && (
                  <div className="space-y-4">
                    <Label className="text-base font-semibold">Changes</Label>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      {selectedLog.oldValues && (
                        <div className="space-y-2">
                          <Label className="text-sm font-medium text-red-600">Previous Values</Label>
                          <div className="bg-red-50 dark:bg-red-950 p-3 rounded-md border">
                            <pre className="text-xs overflow-x-auto">
                              {JSON.stringify(selectedLog.oldValues, null, 2)}
                            </pre>
                          </div>
                        </div>
                      )}

                      {selectedLog.newValues && (
                        <div className="space-y-2">
                          <Label className="text-sm font-medium text-green-600">New Values</Label>
                          <div className="bg-green-50 dark:bg-green-950 p-3 rounded-md border">
                            <pre className="text-xs overflow-x-auto">
                              {JSON.stringify(selectedLog.newValues, null, 2)}
                            </pre>
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                )}
              </div>
            )}
          </DialogContent>
        </Dialog>
      </div>
  );
}
