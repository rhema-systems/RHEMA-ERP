 
"use client";

import React, { useState, useMemo, useEffect, useCallback } from 'react';
import { Badge } from '../ui/badge';
import { NoSSR } from '../NoSSR';
import { Avatar, AvatarFallback } from '../ui/avatar';
import { Button } from '../ui/button';
import { Card, CardContent } from '../ui/card';
import { Input } from '../ui/input';
import { Textarea } from '../ui/textarea';
import { Checkbox } from '../ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '../ui/form';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '../ui/select';
import {
  UserCheckIcon,
  MailIcon,
  ClockIcon,
  RefreshCwIcon,
  LogOutIcon,
  AlertTriangleIcon,
  MonitorIcon,
  SmartphoneIcon,
  TabletIcon,
  EyeIcon,
  Filter,
  Search,
  X,
  Users,
  PowerOff,
  Ban,
  Shield,
} from 'lucide-react';
import { formatDistanceToNow, format } from 'date-fns';
import { useDashboardSignalR } from '../../hooks/useSignalR';
import { useToast } from '../../hooks/use-toast';
import { securityService, UserSession, SessionFilter, BulkSessionOperationResponse, TerminateSessionsByCriteriaRequest } from '../../services/security';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { DataTable, DataTableColumn, DataTableAction } from '../ui/DataTable';

// Form schemas for session management
const terminateReasonSchema = z.object({
  reason: z.string().min(3, 'Reason must be at least 3 characters').max(500, 'Reason cannot exceed 500 characters')
});

type TerminateReasonFormData = z.infer<typeof terminateReasonSchema>;

interface EnhancedUserSession extends UserSession {
  status?: 'active' | 'idle' | 'inactive';
  connectionCount?: number;
}

type TableCellProps<T> = {
  row: {
    original: T;
  };
};

export function SessionManagementTab() {
  const { toast } = useToast();
  const [sessions, setSessions] = useState<EnhancedUserSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date>(new Date());
  
  // Session management state
  const [showTerminateDialog, setShowTerminateDialog] = useState(false);
  const [terminatingSession, setTerminatingSession] = useState<string | null>(null);
  
  // SignalR for real-time updates
  const { 
    dashboardData, 
    isConnected: isSignalRConnected, 
    userSessionUpdates 
  } = useDashboardSignalR();
  
  // Form instances
  const terminateReasonForm = useForm<TerminateReasonFormData>({
    resolver: zodResolver(terminateReasonSchema),
    defaultValues: {
      reason: ''
    }
  });

  // Load sessions with filters
  const loadSessions = useCallback(async (filters?: SessionFilter) => {
    try {
      setRefreshing(true);
      setError(null);
      
      const apiSessions = await securityService.getAllActiveSessions(filters);
      const enhancedSessions: EnhancedUserSession[] = apiSessions.map(session => {
        return {
          ...session,
          // Keep the API-provided role and email - don't override with SignalR data
          status: getActivityStatus(session.lastActivityTime),
          connectionCount: 1,
        };
      });
      
      setSessions(enhancedSessions);
      setLastUpdated(new Date());
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load active sessions';
      setError(errorMessage);
      console.error('Failed to load sessions:', err);
      toast({ 
        title: 'Error', 
        description: errorMessage, 
        variant: 'destructive' 
      });
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [toast]);

  // Utility functions
  const getDeviceIcon = (deviceType: string) => {
    switch (deviceType.toLowerCase()) {
      case 'mobile': return <SmartphoneIcon className="h-4 w-4" />;
      case 'tablet': return <TabletIcon className="h-4 w-4" />;
      default: return <MonitorIcon className="h-4 w-4" />;
    }
  };

  const getSessionDurationColor = (loginTime: Date) => {
    const hours = (Date.now() - loginTime.getTime()) / (1000 * 60 * 60);
    if (hours > 24) return 'text-red-600';
    if (hours > 8) return 'text-orange-600';
    return 'text-green-600';
  };

  const getActivityStatus = (lastActivity: Date): 'active' | 'idle' | 'inactive' => {
    const minutes = (Date.now() - lastActivity.getTime()) / (1000 * 60);
    if (minutes < 5) return 'active';
    if (minutes < 30) return 'idle';
    return 'inactive';
  };

  const getActivityStatusColor = (status: 'active' | 'idle' | 'inactive') => {
    switch (status) {
      case 'active': return 'bg-green-500';
      case 'idle': return 'bg-yellow-500';
      case 'inactive': return 'bg-red-500';
      default: return 'bg-gray-500';
    }
  };

  // Session management functions
  const handleTerminateSession = async (sessionId: string) => {
    setTerminatingSession(sessionId);
    setShowTerminateDialog(true);
  };

  const confirmTerminateSession = async (data: TerminateReasonFormData) => {
    if (!terminatingSession) return;
    try {
      await securityService.terminateSession(terminatingSession, data.reason);
      toast({ title: 'Success', description: 'Session terminated successfully' });
      setShowTerminateDialog(false);
      setTerminatingSession(null);
      terminateReasonForm.reset();
      await loadSessions();
    } catch (error) {
      console.error('Failed to terminate session:', error);
      toast({ title: 'Error', description: 'Failed to terminate session', variant: 'destructive' });
    }
  };

  // Table columns - Remove manual select column since DataTable handles it with enableRowSelection
  const columns = useMemo<DataTableColumn<EnhancedUserSession>[]>(() => [
    {
      accessorKey: 'username',
      header: 'User',
      cell: ({ row }: TableCellProps<EnhancedUserSession>) => {
        const session = row.original;
        const initials = session.username.substring(0, 2).toUpperCase();
        
        return (
          <div className="flex items-center space-x-3">
            <Avatar className="h-8 w-8">
              <AvatarFallback className="text-xs">{initials}</AvatarFallback>
            </Avatar>
            <div className="flex flex-col">
              <span className="font-medium">{session.username}</span>
              <div className="flex items-center space-x-1 text-xs text-muted-foreground">
                <MailIcon className="h-3 w-3" />
                <span>{session.email}</span>
              </div>
            </div>
          </div>
        );
      },
      enableSorting: true,
      enableHiding: false,
    },
    {
      accessorKey: 'role',
      header: 'Role',
      cell: ({ row }: TableCellProps<EnhancedUserSession>) => {
        const role = row.original.role;
        const roleColors = {
          'SuperAdmin': 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
          'TenantAdmin': 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200',
          'Manager': 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
          'Employee': 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
        };
        
        return (
          <Badge 
            variant="secondary" 
            className={roleColors[role as keyof typeof roleColors] || ''}
          >
            {role}
          </Badge>
        );
      },
      enableSorting: true,
    },
    {
      accessorKey: 'status',
      header: 'Status',
      cell: ({ row }: TableCellProps<EnhancedUserSession>) => {
        const session = row.original;
        const status = session.status ?? 'inactive';
        
        return (
          <div className="flex items-center space-x-2">
            <div className={`w-2 h-2 rounded-full ${getActivityStatusColor(status)}`} />
            <Badge variant={status === 'active' ? 'default' : 'secondary'}>
              {status ? status.charAt(0).toUpperCase() + status.slice(1) : 'Unknown'}
            </Badge>
          </div>
        );
      },
      enableSorting: true,
    },
    {
      accessorKey: 'deviceType',
      header: 'Device',
      cell: ({ row }: TableCellProps<EnhancedUserSession>) => {
        const deviceType = row.original.deviceType;
        
        return (
          <div className="flex items-center space-x-2">
            {getDeviceIcon(deviceType)}
            <span className="capitalize">{deviceType}</span>
          </div>
        );
      },
      enableSorting: true,
    },
    {
      accessorKey: 'ipAddress',
      header: 'IP Address',
      cell: ({ row }: TableCellProps<EnhancedUserSession>) => (
        <code className="text-xs bg-muted px-2 py-1 rounded">
          {row.original.ipAddress}
        </code>
      ),
    },
    {
      accessorKey: 'sessionDuration',
      header: 'Session Duration',
      cell: ({ row }: TableCellProps<EnhancedUserSession>) => {
        const loginTime = row.original.loginTime;
        const duration = formatDistanceToNow(new Date(loginTime));
        const durationColor = getSessionDurationColor(new Date(loginTime));
        
        return (
          <div className="flex items-center space-x-1">
            <ClockIcon className="h-3 w-3 text-muted-foreground" />
            <span className={`text-sm ${durationColor}`}>{duration}</span>
          </div>
        );
      },
      enableSorting: true,
    },
    {
      accessorKey: 'lastActivityTime',
      header: 'Last Activity',
      cell: ({ row }: TableCellProps<EnhancedUserSession>) => {
        const lastActivity = new Date(row.original.lastActivityTime);
        return (
          <div className="text-sm">
            <div>{formatDistanceToNow(lastActivity, { addSuffix: true })}</div>
            <div className="text-xs text-muted-foreground">
              {format(lastActivity, 'MMM dd, HH:mm')}
            </div>
          </div>
        );
      },
      enableSorting: true,
    },
  ], [getDeviceIcon, getSessionDurationColor, getActivityStatusColor]);

  // Row actions
  const rowActions = useMemo<DataTableAction<EnhancedUserSession>[]>(() => [
    {
      id: 'terminate',
      label: 'Terminate Session',
      icon: LogOutIcon,
      onClick: (row) => handleTerminateSession(row.original.sessionId),
      variant: 'ghost',
    },
  ], []);

  // Effects
  useEffect(() => {
    loadSessions();
  }, [loadSessions]);

  // Calculate stats
  const stats = {
    totalOnline: sessions.length,
    activeUsers: sessions.filter(s => s.status === 'active').length,
    idleUsers: sessions.filter(s => s.status === 'idle').length,
    adminUsers: sessions.filter(s => s.role === 'SuperAdmin' || s.role === 'TenantAdmin').length,
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <RefreshCwIcon className="h-8 w-8 animate-spin" />
        <span className="ml-2">Loading active sessions...</span>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-2">
          <Shield className="h-6 w-6 text-blue-600" />
          <div>
            <h2 className="text-2xl font-bold">Session Management</h2>
            <p className="text-muted-foreground">
              Monitor and manage active user sessions
            </p>
          </div>
          <Badge variant="outline" className="ml-2">
            {sessions.length} active sessions
          </Badge>
        </div>
        
        <div className="flex items-center space-x-2">
          <Badge variant={isSignalRConnected ? 'default' : 'destructive'} className="flex items-center space-x-1">
            <div className={`w-2 h-2 rounded-full ${isSignalRConnected ? 'bg-green-500' : 'bg-red-500'}`} />
            <span>{isSignalRConnected ? 'Live' : 'Disconnected'}</span>
          </Badge>
          
          <Button
            variant="outline"
            size="sm"
            onClick={() => loadSessions()}
            disabled={refreshing}
          >
            <RefreshCwIcon className={`h-4 w-4 mr-2 ${refreshing ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Total Online</p>
                <p className="text-2xl font-bold text-blue-600">{stats.totalOnline}</p>
              </div>
              <UserCheckIcon className="h-8 w-8 text-blue-600" />
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Active Users</p>
                <p className="text-2xl font-bold text-green-600">{stats.activeUsers}</p>
              </div>
              <div className="w-8 h-8 rounded-full bg-green-100 flex items-center justify-center">
                <div className="w-3 h-3 rounded-full bg-green-500" />
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Idle Users</p>
                <p className="text-2xl font-bold text-yellow-600">{stats.idleUsers}</p>
              </div>
              <div className="w-8 h-8 rounded-full bg-yellow-100 flex items-center justify-center">
                <div className="w-3 h-3 rounded-full bg-yellow-500" />
              </div>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Admin Users</p>
                <p className="text-2xl font-bold text-red-600">{stats.adminUsers}</p>
              </div>
              <AlertTriangleIcon className="h-8 w-8 text-red-600" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Data Table */}
      <NoSSR fallback={<div className="p-8 text-center text-muted-foreground">Loading table...</div>}>
        <DataTable
          data={sessions}
          columns={columns}
          title="Active User Sessions"
          description={`Last updated: ${format(lastUpdated, 'MMM dd, yyyy HH:mm:ss')}`}
          loading={refreshing}
          error={error}
          
          // Table configuration
          enableRowSelection={true}
          enablePagination={true}
          pageSize={20}
          enableSorting={true}
          enableGlobalFilter={true}
          searchPlaceholder="Search sessions..."
          rowActions={rowActions}
          striped={true}
          hoverable={true}
          emptyStateMessage="No active sessions found"
          
          // Mobile optimization  
          enableMobileCards={true}
          hideColumnsOnMobile={['ipAddress', 'sessionDuration']}
        />
      </NoSSR>

      {/* Dialogs */}
      <Dialog open={showTerminateDialog} onOpenChange={setShowTerminateDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Terminate Session</DialogTitle>
            <DialogDescription>
              Please provide a reason for terminating this session.
            </DialogDescription>
          </DialogHeader>
          <Form {...terminateReasonForm}>
            <form onSubmit={terminateReasonForm.handleSubmit(confirmTerminateSession)}>
              <FormField
                control={terminateReasonForm.control}
                name="reason"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Termination Reason</FormLabel>
                    <FormControl>
                      <Textarea placeholder="Enter reason..." {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <DialogFooter className="mt-4">
                <Button type="button" variant="outline" onClick={() => setShowTerminateDialog(false)}>
                  Cancel
                </Button>
                <Button type="submit" variant="destructive">
                  Terminate Session
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}
