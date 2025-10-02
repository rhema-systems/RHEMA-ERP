'use client';

import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { DashboardLayout } from '../../../../components/layout/dashboard-layout';
import { DataTable, Column } from '../../../../components/admin/data-table';
import { Badge } from '../../../../components/ui/badge';
import { Button } from '../../../../components/ui/button';
import { Input } from '../../../../components/ui/input';
import { Label } from '../../../../components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../../../../components/ui/select';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../../components/ui/card';
import { adminApiService, SecurityLog } from '../../../../services/admin-api.service';
import { 
  Shield, 
  User, 
  Calendar, 
  AlertTriangle,
  CheckCircle,
  XCircle,
  Filter,
  Download,
  RefreshCw,
  Monitor
} from 'lucide-react';
import { format } from 'date-fns';

const ACTION_COLORS: Record<string, string> = {
  LOGIN_SUCCESS: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300',
  LOGIN_FAILED: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300',
  LOGOUT: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300',
  PASSWORD_CHANGE: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-300',
  ACCOUNT_LOCKED: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-300',
  ACCOUNT_UNLOCKED: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300',
};

const ACTION_ICONS: Record<string, React.ComponentType<{ className?: string }>> = {
  LOGIN_SUCCESS: CheckCircle,
  LOGIN_FAILED: XCircle,
  LOGOUT: User,
  PASSWORD_CHANGE: Shield,
  ACCOUNT_LOCKED: AlertTriangle,
  ACCOUNT_UNLOCKED: CheckCircle,
};

export default function SecurityLogsPage() {
  const [filters, setFilters] = useState({
    action: 'all',
    username: '',
    success: 'all',
    dateFrom: '',
    dateTo: '',
    ipAddress: '',
  });

  // Fetch security logs data
  const { data: securityLogs = [], isLoading, refetch } = useQuery({
    queryKey: ['security-logs'],
    queryFn: () => adminApiService.getSecurityLogs(),
  });

  // Filter logs based on current filters
  const filteredLogs = securityLogs.filter((log) => {
    if (filters.action && filters.action !== 'all' && log.action !== filters.action) return false;
    if (filters.username && !log.username?.toLowerCase().includes(filters.username.toLowerCase())) return false;
    if (filters.success && filters.success !== 'all' && log.success.toString() !== filters.success) return false;
    if (filters.dateFrom && new Date(log.timestamp) < new Date(filters.dateFrom)) return false;
    if (filters.dateTo && new Date(log.timestamp) > new Date(filters.dateTo)) return false;
    if (filters.ipAddress && !log.ipAddress.includes(filters.ipAddress)) return false;
    return true;
  });

  const handleExport = () => {
    const csvContent = [
      ['Timestamp', 'Username', 'Action', 'Success', 'IP Address', 'User Agent', 'Details'],
      ...filteredLogs.map(log => [
        format(new Date(log.timestamp), 'yyyy-MM-dd HH:mm:ss'),
        log.username || 'N/A',
        log.action,
        log.success ? 'Yes' : 'No',
        log.ipAddress,
        log.userAgent || 'N/A',
        log.details || 'N/A'
      ])
    ].map(row => row.join(',')).join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `security-logs-${format(new Date(), 'yyyy-MM-dd')}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  const clearFilters = () => {
    setFilters({
      action: 'all',
      username: '',
      success: 'all',
      dateFrom: '',
      dateTo: '',
      ipAddress: '',
    });
  };

  // Calculate summary statistics
  const stats = {
    total: filteredLogs.length,
    successful: filteredLogs.filter(log => log.success).length,
    failed: filteredLogs.filter(log => !log.success).length,
    uniqueUsers: new Set(filteredLogs.map(log => log.username).filter(Boolean)).size,
    uniqueIPs: new Set(filteredLogs.map(log => log.ipAddress)).size,
  };

  const columns: Column<SecurityLog>[] = [
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
      label: 'Username',
      sortable: true,
      render: (username: string) => (
        <div className="flex items-center gap-2">
          <User className="h-4 w-4 text-muted-foreground" />
          <span className="font-medium">{username || 'Unknown'}</span>
        </div>
      ),
    },
    {
      key: 'action',
      label: 'Action',
      render: (action: string, log: SecurityLog) => {
        const Icon = ACTION_ICONS[action] || Shield;
        return (
          <div className="flex items-center gap-2">
            <Icon className="h-4 w-4" />
            <Badge className={ACTION_COLORS[action] || 'bg-gray-100 text-gray-800'}>
              {action.replace('_', ' ')}
            </Badge>
          </div>
        );
      },
    },
    {
      key: 'success',
      label: 'Result',
      render: (success: boolean) => (
        <div className="flex items-center gap-2">
          {success ? (
            <CheckCircle className="h-4 w-4 text-green-500" />
          ) : (
            <XCircle className="h-4 w-4 text-red-500" />
          )}
          <Badge variant={success ? 'default' : 'destructive'}>
            {success ? 'Success' : 'Failed'}
          </Badge>
        </div>
      ),
    },
    {
      key: 'ipAddress',
      label: 'IP Address',
      render: (ipAddress: string) => (
        <div className="flex items-center gap-2">
          <Monitor className="h-4 w-4 text-muted-foreground" />
          <Badge variant="outline" className="font-mono text-xs">
            {ipAddress}
          </Badge>
        </div>
      ),
    },
    {
      key: 'details',
      label: 'Details',
      render: (details: string) => (
        <span className="text-sm text-muted-foreground">
          {details || '-'}
        </span>
      ),
    },
  ];

  return (
    <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold tracking-tight">Security Logs</h1>
            <p className="text-muted-foreground">
              Monitor authentication events and security-related activities
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

        {/* Summary Statistics */}
        <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Total Events</p>
                  <p className="text-2xl font-bold">{stats.total}</p>
                </div>
                <Shield className="h-8 w-8 text-muted-foreground" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Successful</p>
                  <p className="text-2xl font-bold text-green-600">{stats.successful}</p>
                </div>
                <CheckCircle className="h-8 w-8 text-green-500" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Failed</p>
                  <p className="text-2xl font-bold text-red-600">{stats.failed}</p>
                </div>
                <XCircle className="h-8 w-8 text-red-500" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Unique Users</p>
                  <p className="text-2xl font-bold">{stats.uniqueUsers}</p>
                </div>
                <User className="h-8 w-8 text-muted-foreground" />
              </div>
            </CardContent>
          </Card>
          
          <Card>
            <CardContent className="p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Unique IPs</p>
                  <p className="text-2xl font-bold">{stats.uniqueIPs}</p>
                </div>
                <Monitor className="h-8 w-8 text-muted-foreground" />
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Filters */}
        <Card>
          <CardHeader>
            <div className="flex items-center gap-2">
              <Filter className="h-4 w-4" />
              <CardTitle className="text-lg">Filters</CardTitle>
            </div>
            <CardDescription>
              Filter security logs by various criteria
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-6 gap-4">
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
                    <SelectItem value="LOGIN_SUCCESS">Login Success</SelectItem>
                    <SelectItem value="LOGIN_FAILED">Login Failed</SelectItem>
                    <SelectItem value="LOGOUT">Logout</SelectItem>
                    <SelectItem value="PASSWORD_CHANGE">Password Change</SelectItem>
                    <SelectItem value="ACCOUNT_LOCKED">Account Locked</SelectItem>
                    <SelectItem value="ACCOUNT_UNLOCKED">Account Unlocked</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="username">Username</Label>
                <Input
                  id="username"
                  placeholder="Filter by username"
                  value={filters.username}
                  onChange={(e) => setFilters({ ...filters, username: e.target.value })}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="success">Result</Label>
                <Select
                  value={filters.success}
                  onValueChange={(value) => setFilters({ ...filters, success: value })}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="All results" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All results</SelectItem>
                    <SelectItem value="true">Success</SelectItem>
                    <SelectItem value="false">Failed</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="space-y-2">
                <Label htmlFor="ipAddress">IP Address</Label>
                <Input
                  id="ipAddress"
                  placeholder="Filter by IP"
                  value={filters.ipAddress}
                  onChange={(e) => setFilters({ ...filters, ipAddress: e.target.value })}
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
                Showing {filteredLogs.length} of {securityLogs.length} records
              </div>
            </div>
          </CardContent>
        </Card>

        <DataTable
          title="Security Events"
          description="Authentication and security-related system events"
          data={filteredLogs}
          columns={columns}
          loading={isLoading}
          searchable={false} // We have custom filters
          actions={false} // No edit/delete actions for security logs
        />
      </div>
  );
}
