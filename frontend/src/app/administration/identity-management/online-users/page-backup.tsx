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
import { Label } from '@/components/ui/label';
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
  MapPinIcon,
  RefreshCwIcon,
  LogOutIcon,
  AlertTriangleIcon,
  MonitorIcon,
  SmartphoneIcon,
  TabletIcon,
  EyeIcon,
  MessageCircleIcon,
  Filter,
  Search,
  X,
  Users,
  PowerOff,
  Ban,
  Shield,
  Calendar,
  Globe,
} from 'lucide-react';
import { formatDistanceToNow, format } from 'date-fns';
import { useDashboardSignalR } from '@/hooks/useSignalR';
import { dashboardService } from '@/services/dashboard.service';
import { OnlineUser } from '@/services/signalr.service';
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
  includeCurrentUser: z.boolean().default(false),
  reason: z.string().min(3, 'Reason must be at least 3 characters').max(500, 'Reason cannot exceed 500 characters')
});

type FilterFormData = z.infer<typeof filterSchema>;
type TerminateReasonFormData = z.infer<typeof terminateReasonSchema>;
type BulkCriteriaFormData = z.infer<typeof bulkCriteriaSchema>;

// Enhanced interface that combines SignalR data with session management data
interface EnhancedUserSession extends UserSession {
  // Additional fields from OnlineUser for display
  email?: string;
  role?: string;
  status?: 'active' | 'idle' | 'inactive';
  connectionCount?: number;
}


export default function OnlineUsersPage() {
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
  const [lastOperationResult, setLastOperationResult] = useState<BulkSessionOperationResponse | null>(null);
  
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
      
      // Get actual session data from API
      const apiSessions = await securityService.getAllActiveSessions(filters);
      
      // Also get dashboard data for additional user info
      const dashboardUsers = dashboardData?.onlineUsers || [];
      
      // Merge session data with user info from SignalR
      const enhancedSessions: EnhancedUserSession[] = apiSessions.map(session => {
        const signalRUser = dashboardUsers.find(u => u.userId === session.userId);
        
        return {
          ...session,
          email: signalRUser?.email || 'N/A',
          role: signalRUser?.role || 'Unknown',
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
  }, [toast, dashboardData]);

  // Handle filter changes
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

  // Clear filters
  const clearFilters = () => {
    filterForm.reset();
    loadSessions();
    setShowFilters(false);
  };

  // Session selection handlers
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

  // Individual session termination
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

  // Bulk session termination
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
      
      setLastOperationResult(result);
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

  // Criteria-based termination
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
      
      setLastOperationResult(result);
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

  // Update from SignalR for real-time updates
  useEffect(() => {
    if (dashboardData?.onlineUsers) {
      // Trigger a reload when SignalR data changes to merge latest info
      loadSessions();
    }
  }, [dashboardData, loadSessions]);

  // Handle real-time session updates
  useEffect(() => {
    if (userSessionUpdates.length > 0) {
      const latestUpdate = userSessionUpdates[0];
      
      if (latestUpdate.type === 'logout') {
        // Remove user from session list
        setSessions(prev => prev.filter(session => session.userId !== latestUpdate.userId));
        
        toast({
          title: "User Logged Out",
          description: `${latestUpdate.userName} has logged out`,
        });
      } else if (latestUpdate.type === 'login') {
        // Refresh data to include new user
        loadSessions();
        
        toast({
          title: "User Logged In",
          description: `${latestUpdate.userName} has logged in`,
        });
      }
    }
  }, [userSessionUpdates, toast, loadSessions]);

  // Initial load
  useEffect(() => {
    loadSessions();
  }, [loadSessions]);

  // Auto refresh every 30 seconds
  useEffect(() => {
    const interval = setInterval(() => {
      if (!showTerminateDialog && !showBulkTerminateDialog && !showCriteriaTerminateDialog && !bulkOperationInProgress) {
        loadSessions();
      }
    }, 30000);

    return () => clearInterval(interval);
  }, [loadSessions, showTerminateDialog, showBulkTerminateDialog, showCriteriaTerminateDialog, bulkOperationInProgress]);

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

  // Define table columns for enhanced session management
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
      filterFn: 'equals',
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
      filterFn: 'equals',
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
      accessorKey: 'browser',
      header: 'Browser',
      cell: ({ row }) => (
        <span className="text-sm">{row.original.browser}</span>
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
  ], []);

  // Define row actions for functional session management
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

  // Toolbar actions
  const handleRefresh = () => {
    fetchOnlineUsers();
  };

  const handleTerminateSelected = (selectedUsers: EnhancedOnlineUser[]) => {
    const userNames = selectedUsers.map(u => u.userName).join(', ');
    toast({
      title: "Sessions Terminated",
      description: `Terminated sessions for: ${userNames}`,
      variant: "destructive",
    });
  };

  const handleBroadcastMessage = (selectedUsers: EnhancedOnlineUser[]) => {
    const userCount = selectedUsers.length;
    toast({
      title: "Message Sent",
      description: `Broadcast message sent to ${userCount} user(s)`,
    });
  };

  // Calculate summary stats
  const stats = {
    totalOnline: onlineUsers.length,
    activeUsers: onlineUsers.filter(u => !u.isIdle).length,
    idleUsers: onlineUsers.filter(u => u.isIdle).length,
    adminUsers: onlineUsers.filter(u => u.role === 'SuperAdmin' || u.role === 'TenantAdmin').length,
  };

  return (
    <DashboardLayout>
      <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Online Users</h1>
          <p className="text-muted-foreground">
            Monitor and manage currently active user sessions
          </p>
        </div>
        
        <div className="flex items-center space-x-2">
          <Badge variant={isSignalRConnected ? 'default' : 'destructive'} className="flex items-center space-x-1">
            <div className={`w-2 h-2 rounded-full ${isSignalRConnected ? 'bg-green-500' : 'bg-red-500'}`} />
            <span>{isSignalRConnected ? 'Live' : 'Disconnected'}</span>
          </Badge>
          
          <Button variant="outline" onClick={handleRefresh} disabled={loading}>
            <RefreshCwIcon className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
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
          data={onlineUsers}
          columns={columns}
          title="Online User Sessions"
          description={`Last updated: ${format(lastUpdated, 'MMM dd, yyyy HH:mm:ss')}`}
          loading={loading}
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
          searchPlaceholder="Search users..."
          
          // Export
          enableExport={true}
          exportFileName={`online-users-${format(new Date(), 'yyyy-MM-dd')}`}
          exportFormats={['csv', 'excel']}
          
          // Actions
          rowActions={rowActions}
          toolbarActions={{
            refresh: handleRefresh,
            customActions: [
              {
                id: 'broadcast',
                label: 'Broadcast Message',
                icon: MessageCircleIcon,
                onClick: handleBroadcastMessage,
                variant: 'outline',
                requiresSelection: true,
              },
              {
                id: 'terminate',
                label: 'Terminate Sessions',
                icon: LogOutIcon,
                onClick: handleTerminateSelected,
                variant: 'destructive',
                requiresSelection: true,
              },
            ],
          }}
          
          // Mobile optimization
          enableMobileCards={true}
          hideColumnsOnMobile={['ipAddress', 'browser', 'sessionDuration']}
          
          // Styling
          striped={true}
          hoverable={true}
          emptyStateMessage="No users are currently online"
        />
      </NoSSR>
      </div>
    </DashboardLayout>
  );
}

// Helper functions
function getDeviceType(userAgent: string): 'desktop' | 'mobile' | 'tablet' {
  const ua = userAgent.toLowerCase();
  if (ua.includes('mobile')) return 'mobile';
  if (ua.includes('tablet') || ua.includes('ipad')) return 'tablet';
  return 'desktop';
}

function getBrowserFromUserAgent(userAgent: string): string {
  const ua = userAgent.toLowerCase();
  if (ua.includes('chrome')) return 'Chrome';
  if (ua.includes('firefox')) return 'Firefox';
  if (ua.includes('safari')) return 'Safari';
  if (ua.includes('edge')) return 'Edge';
  return 'Unknown';
}

function getSessionDuration(lastActivity: string): string {
  const now = new Date();
  const activityTime = new Date(lastActivity);
  const diffMs = now.getTime() - activityTime.getTime();
  const hours = Math.floor(diffMs / (1000 * 60 * 60));
  const minutes = Math.floor((diffMs % (1000 * 60 * 60)) / (1000 * 60));
  
  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  return `${minutes}m`;
}

function isUserIdle(lastActivity: string): boolean {
  const now = new Date();
  const activityTime = new Date(lastActivity);
  const diffMinutes = (now.getTime() - activityTime.getTime()) / (1000 * 60);
  return diffMinutes > 10; // Consider idle after 10 minutes
}
