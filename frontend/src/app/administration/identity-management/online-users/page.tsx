"use client";

import React, { useState, useMemo, useEffect, useCallback } from 'react';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { DataTable, DataTableColumn, DataTableAction } from '@/components/ui/DataTable';
import { Badge } from '@/components/ui/badge';
import { NoSSR } from '@/components/NoSSR';
import { Avatar, AvatarImage, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
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
  Calendar,
} from 'lucide-react';
import { formatDistanceToNow, format } from 'date-fns';
import { useDashboardSignalR } from '@/hooks/useSignalR';
import { useToast } from '@/hooks/use-toast';
import { securityService, UserSession, SessionFilter, BulkSessionOperationResponse, TerminateSessionsByCriteriaRequest } from '@/services/security';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { ClientOnly } from '@/components/ui/client-only';

// Form schemas for session management
const filterSchema = z.object({
  username: z.string().optional(),
  ipAddress: z.string().optional(),
  deviceType: z.enum(['Desktop', 'Mobile', 'Tablet', '']).optional(),
  loginTimeAfter: z.date().optional(),
  lastActivityAfter: z.date().optional()
});

const terminateReasonSchema = z.object({
  reason: z.string().min(3, 'Reason must be at least 3 characters').max(500, 'Reason cannot exceed 500 characters')
});

const bulkCriteriaSchema = z.object({
  ipAddressPattern: z.string().optional(),
  deviceType: z.string().optional(),
  browser: z.string().optional(),
  loginTimeBefore: z.date().optional(),
  lastActivityBefore: z.date().optional(),
  includeCurrentUser: z.boolean().optional().default(false),
  reason: z.string().min(3, 'Reason must be at least 3 characters').max(500, 'Reason cannot exceed 500 characters')
});

type FilterFormData = z.infer<typeof filterSchema>;
type TerminateReasonFormData = z.infer<typeof terminateReasonSchema>;
type BulkCriteriaFormData = z.infer<typeof bulkCriteriaSchema>;

// Enhanced interface that combines SignalR data with session management data
interface EnhancedUserSession extends UserSession {
  // Additional computed fields for display  
  status?: 'active' | 'idle' | 'inactive';
  connectionCount?: number;
}

export default function OnlineUsersPage() {
  console.log('🚀 OnlineUsersPage component initialized');
  
  const { toast } = useToast();
  const [sessions, setSessions] = useState<EnhancedUserSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date>(new Date());
  
  // Session management state
  const [selectedSessions, setSelectedSessions] = useState<Set<string>>(new Set());
  const [showFilters, setShowFilters] = useState(false);
  const [showTerminateDialog, setShowTerminateDialog] = useState(false);
  const [showBulkTerminateDialog, setShowBulkTerminateDialog] = useState(false);
  const [showCriteriaTerminateDialog, setShowCriteriaTerminateDialog] = useState(false);
  const [terminatingSession, setTerminatingSession] = useState<string | null>(null);
  const [bulkOperationInProgress, setBulkOperationInProgress] = useState(false);
  
  // SignalR for real-time updates
  const { 
    dashboardData, 
    isConnected: isSignalRConnected, 
    userSessionUpdates 
  } = useDashboardSignalR();
  
  // Form instances
  const filterForm = useForm<FilterFormData>({
    resolver: zodResolver(filterSchema),
    defaultValues: {
      username: '',
      ipAddress: '',
      deviceType: '',
      loginTimeAfter: undefined,
      lastActivityAfter: undefined
    }
  });

  const terminateReasonForm = useForm<TerminateReasonFormData>({
    resolver: zodResolver(terminateReasonSchema),
    defaultValues: {
      reason: ''
    }
  });

  const bulkCriteriaForm = useForm<BulkCriteriaFormData>({
    resolver: zodResolver(bulkCriteriaSchema),
    defaultValues: {
      ipAddressPattern: '',
      deviceType: '',
      browser: '',
      loginTimeBefore: undefined,
      lastActivityBefore: undefined,
      includeCurrentUser: false,
      reason: ''
    }
  });

  // Load sessions with filters - integrates API data with SignalR updates
  const loadSessions = useCallback(async (filters?: SessionFilter) => {
    try {
      setRefreshing(true);
      setError(null);
      
      console.log('🔍 Loading sessions with filters:', filters);
      
      // Get actual session data from API
      const apiSessions = await securityService.getAllActiveSessions(filters);
      console.log('📡 API Sessions received:', apiSessions);
      console.log('🔍 First session sample:', apiSessions[0]);
      if (apiSessions.length > 0) {
        const first = apiSessions[0];
        console.log('🔍 First session role:', first.role);
        console.log('🔍 First session email:', first.email);
        console.log('🔍 All keys in first session:', Object.keys(first));
        console.log('🔍 Role type:', typeof first.role);
        console.log('🔍 Is role defined?:', first.role !== undefined);
        console.log('🔍 Is role null?:', first.role === null);
        console.log('🔍 Role stringified:', JSON.stringify(first.role));
      }
      
      // Also get dashboard data for additional user info
      const dashboardUsers = dashboardData?.onlineUsers || [];
      console.log('📊 Dashboard users:', dashboardUsers);
      
      // Enhance session data with computed fields
      const enhancedSessions: EnhancedUserSession[] = apiSessions.map(session => {
        console.log('🔧 Processing session BEFORE enhancement:', {
          sessionId: session.sessionId,
          username: session.username,
          role: session.role,
          email: session.email,
          fullSession: session
        });
        
        const enhanced = {
          ...session,
          status: getActivityStatus(session.lastActivityTime),
          connectionCount: 1,
        };
        
        console.log('🔧 Processing session AFTER enhancement:', {
          sessionId: enhanced.sessionId,
          username: enhanced.username,
          role: enhanced.role,
          email: enhanced.email,
          fullEnhanced: enhanced
        });
        
        return enhanced;
      });
      
      console.log('✅ Enhanced sessions:', enhancedSessions);
      setSessions(enhancedSessions);
      setLastUpdated(new Date());
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load active sessions';
      setError(errorMessage);
      console.error('❌ Failed to load sessions:', err);
      toast({ 
        title: 'Error', 
        description: errorMessage, 
        variant: 'destructive' 
      });
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [toast, dashboardData]);

  // Utility functions
  const getDeviceIcon = (deviceType: string) => {
    switch (deviceType.toLowerCase()) {
      case 'mobile':
        return <SmartphoneIcon className="h-4 w-4" />;
      case 'tablet':
        return <TabletIcon className="h-4 w-4" />;
      case 'desktop':
      default:
        return <MonitorIcon className="h-4 w-4" />;
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
  const handleFilter = (data: FilterFormData) => {
    const filters: SessionFilter = {};
    if (data.username) filters.username = data.username;
    if (data.ipAddress) filters.ipAddress = data.ipAddress;
    if (data.deviceType) filters.deviceType = data.deviceType;
    if (data.loginTimeAfter) filters.loginTimeAfter = data.loginTimeAfter;
    if (data.lastActivityAfter) filters.lastActivityAfter = data.lastActivityAfter;

    loadSessions(filters);
    setShowFilters(false);
  };

  const clearFilters = () => {
    filterForm.reset();
    loadSessions();
    setShowFilters(false);
  };

  const handleSelectSession = (sessionId: string, checked: boolean) => {
    const newSelection = new Set(selectedSessions);
    if (checked) {
      newSelection.add(sessionId);
    } else {
      newSelection.delete(sessionId);
    }
    setSelectedSessions(newSelection);
  };

  const handleSelectAll = (checked: boolean) => {
    if (checked) {
      setSelectedSessions(new Set(sessions.map(s => s.sessionId)));
    } else {
      setSelectedSessions(new Set());
    }
  };

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

  const handleBulkTerminate = () => {
    if (selectedSessions.size === 0) {
      toast({ title: 'Error', description: 'Please select sessions to terminate', variant: 'destructive' });
      return;
    }
    setShowBulkTerminateDialog(true);
  };

  const confirmBulkTerminate = async (data: TerminateReasonFormData) => {
    try {
      setBulkOperationInProgress(true);
      const result = await securityService.bulkTerminateSessions(
        Array.from(selectedSessions),
        data.reason
      );
      
      toast({ 
        title: 'Bulk Termination Complete', 
        description: `Terminated ${result.successful} of ${result.totalRequested} sessions`
      });
      
      setShowBulkTerminateDialog(false);
      terminateReasonForm.reset();
      setSelectedSessions(new Set());
      await loadSessions();
    } catch (error) {
      console.error('Failed to bulk terminate sessions:', error);
      toast({ title: 'Error', description: 'Failed to bulk terminate sessions', variant: 'destructive' });
    } finally {
      setBulkOperationInProgress(false);
    }
  };

  const handleTerminateByCriteria = () => {
    setShowCriteriaTerminateDialog(true);
  };

  const confirmTerminateByCriteria = async (data: BulkCriteriaFormData) => {
    try {
      setBulkOperationInProgress(true);
      const criteria: TerminateSessionsByCriteriaRequest = {
        ...data,
        loginTimeBefore: data.loginTimeBefore,
        lastActivityBefore: data.lastActivityBefore,
        includeCurrentUser: data.includeCurrentUser
      };
      
      const result = await securityService.terminateSessionsByCriteria(criteria);
      
      toast({ 
        title: 'Criteria-based Termination Complete', 
        description: `Terminated ${result.successful} of ${result.totalRequested} sessions matching criteria`
      });
      
      setShowCriteriaTerminateDialog(false);
      bulkCriteriaForm.reset();
      await loadSessions();
    } catch (error) {
      console.error('Failed to terminate sessions by criteria:', error);
      toast({ title: 'Error', description: 'Failed to terminate sessions by criteria', variant: 'destructive' });
    } finally {
      setBulkOperationInProgress(false);
    }
  };

  // Effects
  useEffect(() => {
    loadSessions();
  }, [loadSessions]);

  useEffect(() => {
    const interval = setInterval(() => {
      if (!showTerminateDialog && !showBulkTerminateDialog && !showCriteriaTerminateDialog && !bulkOperationInProgress) {
        loadSessions();
      }
    }, 30000);

    return () => clearInterval(interval);
  }, [loadSessions, showTerminateDialog, showBulkTerminateDialog, showCriteriaTerminateDialog, bulkOperationInProgress]);

  useEffect(() => {
    if (userSessionUpdates.length > 0) {
      const latestUpdate = userSessionUpdates[0];
      
      if (latestUpdate.type === 'logout') {
        setSessions(prev => prev.filter(session => session.userId !== latestUpdate.userId));
        toast({
          title: "User Logged Out",
          description: `${latestUpdate.userName} has logged out`,
        });
      } else if (latestUpdate.type === 'login') {
        loadSessions();
        toast({
          title: "User Logged In",
          description: `${latestUpdate.userName} has logged in`,
        });
      }
    }
  }, [userSessionUpdates, toast, loadSessions]);

  // Table columns
  const columns = useMemo<DataTableColumn<EnhancedUserSession>[]>(() => [
    {
      id: 'select',
      header: ({ table }) => (
        <Checkbox
          checked={table.getIsAllPageRowsSelected()}
          onCheckedChange={(value) => {
            table.toggleAllPageRowsSelected(!!value);
            handleSelectAll(!!value);
          }}
          aria-label="Select all"
        />
      ),
      cell: ({ row }) => (
        <Checkbox
          checked={selectedSessions.has(row.original.sessionId)}
          onCheckedChange={(value) => handleSelectSession(row.original.sessionId, !!value)}
          aria-label="Select row"
        />
      ),
      enableSorting: false,
      enableHiding: false,
    },
    {
      accessorKey: 'username',
      header: 'User',
      cell: ({ row }) => {
        const session = row.original;
        const initials = session.username.substring(0, 2).toUpperCase();
        
        return (
          <div className="flex items-center space-x-3">
            <Avatar className="h-8 w-8">
              <AvatarImage src={`https://api.dicebear.com/7.x/initials/svg?seed=${session.username}`} />
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
      cell: ({ row }) => {
        const session = row.original;
        const role = session.role;
        
        console.log('🏷️ Role cell rendering:', { 
          sessionId: session.sessionId, 
          role, 
          roleType: typeof role,
          roleIsUndefined: role === undefined,
          roleIsNull: role === null,
          roleIsEmptyString: role === '',
          originalObject: session,
          allKeys: Object.keys(session)
        });
        
        const roleColors = {
          'SuperAdmin': 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
          'TenantAdmin': 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200',
          'Manager': 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
          'Employee': 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
        };
        
        // Display the actual role value or show debugging info
        const displayRole = role || 'MISSING_ROLE';
        
        return (
          <Badge 
            variant="secondary" 
            className={roleColors[role as keyof typeof roleColors] || ''}
          >
            {displayRole}
          </Badge>
        );
      },
      enableSorting: true,
    },
    {
      accessorKey: 'status',
      header: 'Status',
      cell: ({ row }) => {
        const session = row.original;
        const status = session.status;
        
        return (
          <div className="flex items-center space-x-2">
            <div className={`w-2 h-2 rounded-full ${getActivityStatusColor(status!)}`} />
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
      cell: ({ row }) => {
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
      cell: ({ row }) => (
        <code className="text-xs bg-muted px-2 py-1 rounded">
          {row.original.ipAddress}
        </code>
      ),
    },
    {
      accessorKey: 'sessionDuration',
      header: 'Session Duration',
      cell: ({ row }) => {
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
      cell: ({ row }) => {
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
  ], [selectedSessions, handleSelectAll, handleSelectSession, getDeviceIcon, getSessionDurationColor, getActivityStatusColor]);

  // Row actions
  const rowActions = useMemo<DataTableAction<EnhancedUserSession>[]>(() => [
    {
      id: 'view',
      label: 'View Session Details',
      icon: EyeIcon,
      onClick: (row) => {
        toast({
          title: "Session Details",
          description: `Session ID: ${row.original.sessionId}\nIP: ${row.original.ipAddress}\nDevice: ${row.original.deviceType}`,
        });
      },
      variant: 'ghost',
    },
    {
      id: 'terminate',
      label: 'Terminate Session',
      icon: LogOutIcon,
      onClick: (row) => {
        handleTerminateSession(row.original.sessionId);
      },
      variant: 'ghost',
      disabled: (row) => row.original.role === 'SuperAdmin',
    },
  ], [toast, handleTerminateSession]);

  // Calculate summary stats
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
              <h1 className="text-3xl font-bold tracking-tight">Session Management</h1>
              <p className="text-muted-foreground">
                Monitor and manage currently active user sessions
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
              onClick={() => setShowFilters(!showFilters)}
            >
              <Filter className="h-4 w-4 mr-2" />
              Filters
            </Button>
            
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

        {/* Filters Panel */}
        {showFilters && (
          <div className="mb-6 p-4 bg-muted rounded-lg">
            <Form {...filterForm}>
              <form onSubmit={filterForm.handleSubmit(handleFilter)} className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  <FormField
                    control={filterForm.control}
                    name="username"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Username</FormLabel>
                        <FormControl>
                          <Input placeholder="Filter by username" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  
                  <FormField
                    control={filterForm.control}
                    name="ipAddress"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>IP Address</FormLabel>
                        <FormControl>
                          <Input placeholder="Filter by IP address" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  
                  <FormField
                    control={filterForm.control}
                    name="deviceType"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Device Type</FormLabel>
                        <ClientOnly fallback={
                          <div className="h-10 bg-muted/50 rounded-md border" />
                        }>
                          <Select onValueChange={v => field.onChange(v === '__all__' ? '' : v)} value={field.value || '__all__'}>
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue placeholder="All device types" />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              <SelectItem value="__all__">All device types</SelectItem>
                              <SelectItem value="Desktop">Desktop</SelectItem>
                              <SelectItem value="Mobile">Mobile</SelectItem>
                              <SelectItem value="Tablet">Tablet</SelectItem>
                            </SelectContent>
                          </Select>
                        </ClientOnly>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
                
                <div className="flex items-center space-x-2">
                  <Button type="submit" size="sm">
                    <Search className="h-4 w-4 mr-2" />
                    Apply Filters
                  </Button>
                  
                  <Button type="button" variant="outline" size="sm" onClick={clearFilters}>
                    <X className="h-4 w-4 mr-2" />
                    Clear
                  </Button>
                </div>
              </form>
            </Form>
          </div>
        )}

        {/* Bulk Actions Bar */}
        {selectedSessions.size > 0 && (
          <div className="mb-4 p-3 bg-blue-50 dark:bg-blue-950/20 border border-blue-200 dark:border-blue-800 rounded-lg flex items-center justify-between">
            <div className="flex items-center space-x-2">
              <Users className="h-4 w-4 text-blue-600" />
              <span className="font-medium text-blue-900 dark:text-blue-100">
                {selectedSessions.size} session{selectedSessions.size !== 1 ? 's' : ''} selected
              </span>
            </div>
            
            <div className="flex items-center space-x-2">
              <Button
                variant="destructive"
                size="sm"
                onClick={handleBulkTerminate}
                disabled={bulkOperationInProgress}
              >
                <PowerOff className="h-4 w-4 mr-2" />
                Terminate Selected
              </Button>
              
              <Button
                variant="outline"
                size="sm"
                onClick={handleTerminateByCriteria}
                disabled={bulkOperationInProgress}
              >
                <Ban className="h-4 w-4 mr-2" />
                Terminate by Criteria
              </Button>
            </div>
          </div>
        )}

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
            
            // Row selection
            enableRowSelection={true}
            
            // Pagination
            enablePagination={true}
            pageSize={20}
            pageSizeOptions={[10, 20, 50, 100]}
            
            // Sorting and filtering
            enableSorting={true}
            enableGlobalFilter={true}
            enableColumnFilters={true}
            searchPlaceholder="Search sessions..."
            
            // Export
            enableExport={true}
            exportFileName={`active-sessions-${format(new Date(), 'yyyy-MM-dd')}`}
            exportFormats={['csv', 'excel']}
            
            // Actions
            rowActions={rowActions}
            
            // Mobile optimization
            enableMobileCards={true}
            hideColumnsOnMobile={['ipAddress', 'browser', 'sessionDuration']}
            
            // Styling
            striped={true}
            hoverable={true}
            emptyStateMessage="No active sessions found"
          />
        </NoSSR>

        {/* Terminate Session Dialog */}
        <Dialog open={showTerminateDialog} onOpenChange={setShowTerminateDialog}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Terminate Session</DialogTitle>
              <DialogDescription>
                Please provide a reason for terminating this session. This action cannot be undone.
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
                        <Textarea
                          placeholder="Enter reason for termination..."
                          {...field}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <DialogFooter className="mt-4">
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => setShowTerminateDialog(false)}
                  >
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

        {/* Bulk Terminate Dialog */}
        <Dialog open={showBulkTerminateDialog} onOpenChange={setShowBulkTerminateDialog}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Bulk Terminate Sessions</DialogTitle>
              <DialogDescription>
                You are about to terminate {selectedSessions.size} sessions. Please provide a reason.
              </DialogDescription>
            </DialogHeader>
            <Form {...terminateReasonForm}>
              <form onSubmit={terminateReasonForm.handleSubmit(confirmBulkTerminate)}>
                <FormField
                  control={terminateReasonForm.control}
                  name="reason"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Termination Reason</FormLabel>
                      <FormControl>
                        <Textarea
                          placeholder="Enter reason for bulk termination..."
                          {...field}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <DialogFooter className="mt-4">
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => setShowBulkTerminateDialog(false)}
                  >
                    Cancel
                  </Button>
                  <Button type="submit" variant="destructive" disabled={bulkOperationInProgress}>
                    {bulkOperationInProgress ? 'Terminating...' : 'Terminate Sessions'}
                  </Button>
                </DialogFooter>
              </form>
            </Form>
          </DialogContent>
        </Dialog>

        {/* Criteria-based Terminate Dialog */}
        <Dialog open={showCriteriaTerminateDialog} onOpenChange={setShowCriteriaTerminateDialog}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>Terminate Sessions by Criteria</DialogTitle>
              <DialogDescription>
                Define criteria to terminate multiple sessions at once. Be careful with this operation.
              </DialogDescription>
            </DialogHeader>
            <Form {...bulkCriteriaForm}>
              <form onSubmit={bulkCriteriaForm.handleSubmit(confirmTerminateByCriteria)} className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <FormField
                    control={bulkCriteriaForm.control}
                    name="ipAddressPattern"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>IP Address Pattern</FormLabel>
                        <FormControl>
                          <Input placeholder="192.168.1." {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                  
                  <FormField
                    control={bulkCriteriaForm.control}
                    name="deviceType"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Device Type</FormLabel>
                        <ClientOnly fallback={
                          <div className="h-10 bg-muted/50 rounded-md border" />
                        }>
                          <Select onValueChange={v => field.onChange(v === '__all__' ? '' : v)} value={field.value || '__all__'}>
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue placeholder="Any device type" />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              <SelectItem value="__all__">Any device type</SelectItem>
                              <SelectItem value="Desktop">Desktop</SelectItem>
                              <SelectItem value="Mobile">Mobile</SelectItem>
                              <SelectItem value="Tablet">Tablet</SelectItem>
                            </SelectContent>
                          </Select>
                        </ClientOnly>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
                
                <FormField
                  control={bulkCriteriaForm.control}
                  name="browser"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Browser</FormLabel>
                      <FormControl>
                        <Input placeholder="Chrome, Firefox, Safari..." {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={bulkCriteriaForm.control}
                  name="includeCurrentUser"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                      <FormControl>
                        <Checkbox
                          checked={field.value}
                          onCheckedChange={field.onChange}
                        />
                      </FormControl>
                      <div className="space-y-1 leading-none">
                        <FormLabel>
                          Include Current User Sessions
                        </FormLabel>
                      </div>
                    </FormItem>
                  )}
                />
                
                <FormField
                  control={bulkCriteriaForm.control}
                  name="reason"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Termination Reason</FormLabel>
                      <FormControl>
                        <Textarea
                          placeholder="Enter reason for criteria-based termination..."
                          {...field}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                
                <DialogFooter>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => setShowCriteriaTerminateDialog(false)}
                  >
                    Cancel
                  </Button>
                  <Button type="submit" variant="destructive" disabled={bulkOperationInProgress}>
                    {bulkOperationInProgress ? 'Terminating...' : 'Terminate by Criteria'}
                  </Button>
                </DialogFooter>
              </form>
            </Form>
          </DialogContent>
        </Dialog>
      </div>
  );
}
