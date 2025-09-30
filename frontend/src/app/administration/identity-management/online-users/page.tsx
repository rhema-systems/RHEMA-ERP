"use client";

import React, { useState, useMemo, useEffect } from 'react';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { DataTable, DataTableColumn, DataTableAction } from '@/components/ui/DataTable';
import { Badge } from '@/components/ui/badge';
import { NoSSR } from '@/components/NoSSR';
import { Avatar, AvatarImage, AvatarFallback } from '@/components/ui/avatar';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
} from 'lucide-react';
import { formatDistanceToNow, format } from 'date-fns';
import { useDashboardSignalR } from '@/hooks/useSignalR';
import { dashboardService } from '@/services/dashboard.service';
import { OnlineUser } from '@/services/signalr.service';
import { useToast } from '@/hooks/use-toast';

// Enhanced online user interface with session details
interface EnhancedOnlineUser extends OnlineUser {
  sessionId: string;
  ipAddress?: string;
  userAgent?: string;
  deviceType?: 'desktop' | 'mobile' | 'tablet';
  browser?: string;
  sessionDuration?: string;
  lastActivity: string;
  loginTime: string;
  isIdle?: boolean;
  connectionCount?: number;
}


export default function OnlineUsersPage() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [onlineUsers, setOnlineUsers] = useState<EnhancedOnlineUser[]>([]);
  const [lastUpdated, setLastUpdated] = useState<Date>(new Date());
  
  const { toast } = useToast();
  const { 
    dashboardData, 
    isConnected: isSignalRConnected, 
    userSessionUpdates 
  } = useDashboardSignalR();

  // Fetch online users data
  const fetchOnlineUsers = async () => {
    try {
      setLoading(true);
      setError(null);
      
      const users = await dashboardService.getDashboardData();
      
      // Transform online users data to enhanced format
      const enhancedUsers: EnhancedOnlineUser[] = users.onlineUsers.map(user => ({
        ...user,
        sessionId: `session_${user.userId}_${Date.now()}`,
        deviceType: getDeviceType(user.location || ''),
        browser: getBrowserFromUserAgent(user.location || ''),
        sessionDuration: getSessionDuration(user.lastActivity),
        loginTime: user.lastActivity, // This should come from session data
        isIdle: isUserIdle(user.lastActivity),
        connectionCount: 1, // This should come from session data
        ipAddress: '192.168.1.1', // This should come from session data
        userAgent: user.location || 'Unknown',
      }));
      
      setOnlineUsers(enhancedUsers);
      setLastUpdated(new Date());
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch online users';
      setError(errorMessage);
      toast({
        title: "Error",
        description: errorMessage,
        variant: "destructive",
      });
    } finally {
      setLoading(false);
    }
  };

  // Update online users from SignalR dashboard data
  useEffect(() => {
    if (dashboardData?.onlineUsers) {
      const enhancedUsers: EnhancedOnlineUser[] = dashboardData.onlineUsers.map(user => ({
        ...user,
        sessionId: `session_${user.userId}_${Date.now()}`,
        deviceType: getDeviceType(user.location || ''),
        browser: getBrowserFromUserAgent(user.location || ''),
        sessionDuration: getSessionDuration(user.lastActivity),
        loginTime: user.lastActivity,
        isIdle: isUserIdle(user.lastActivity),
        connectionCount: 1,
        ipAddress: '192.168.1.1',
        userAgent: user.location || 'Unknown',
      }));
      
      setOnlineUsers(enhancedUsers);
      setLastUpdated(new Date());
    }
  }, [dashboardData]);

  // Handle real-time session updates
  useEffect(() => {
    if (userSessionUpdates.length > 0) {
      const latestUpdate = userSessionUpdates[0];
      
      if (latestUpdate.type === 'logout') {
        // Remove user from online list
        setOnlineUsers(prev => prev.filter(user => user.userId !== latestUpdate.userId));
        
        toast({
          title: "User Logged Out",
          description: `${latestUpdate.userName} has logged out`,
        });
      } else if (latestUpdate.type === 'login') {
        // Refresh data to include new user
        fetchOnlineUsers();
        
        toast({
          title: "User Logged In",
          description: `${latestUpdate.userName} has logged in`,
        });
      }
    }
  }, [userSessionUpdates, toast]);

  // Load data on component mount
  useEffect(() => {
    fetchOnlineUsers();
  }, []);

  // Define table columns
  const columns = useMemo<DataTableColumn<EnhancedOnlineUser>[]>(() => [
    {
      accessorKey: 'userName',
      header: 'User',
      cell: ({ row }) => {
        const user = row.original;
        const initials = user.userName.substring(0, 2).toUpperCase();
        
        return (
          <div className="flex items-center space-x-3">
            <Avatar className="h-8 w-8">
              <AvatarImage src={`https://api.dicebear.com/7.x/initials/svg?seed=${user.userName}`} />
              <AvatarFallback className="text-xs">{initials}</AvatarFallback>
            </Avatar>
            <div className="flex flex-col">
              <span className="font-medium">{user.userName}</span>
              <div className="flex items-center space-x-1 text-xs text-muted-foreground">
                <MailIcon className="h-3 w-3" />
                <span>{user.email}</span>
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
        const role = row.getValue('role') as string;
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
        const user = row.original;
        const isIdle = user.isIdle;
        
        return (
          <div className="flex items-center space-x-2">
            <div className={`w-2 h-2 rounded-full ${isIdle ? 'bg-yellow-500' : 'bg-green-500'}`} />
            <Badge variant={isIdle ? 'secondary' : 'default'}>
              {isIdle ? 'Idle' : 'Active'}
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
        const DeviceIcon = deviceType === 'mobile' ? SmartphoneIcon : 
                           deviceType === 'tablet' ? TabletIcon : MonitorIcon;
        
        return (
          <div className="flex items-center space-x-2">
            <DeviceIcon className="h-4 w-4 text-muted-foreground" />
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
        const duration = row.original.sessionDuration;
        return (
          <div className="flex items-center space-x-1">
            <ClockIcon className="h-3 w-3 text-muted-foreground" />
            <span className="text-sm">{duration}</span>
          </div>
        );
      },
      enableSorting: true,
    },
    {
      accessorKey: 'lastActivity',
      header: 'Last Activity',
      cell: ({ row }) => {
        const lastActivity = new Date(row.getValue('lastActivity'));
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

  // Define row actions
  const rowActions = useMemo<DataTableAction<EnhancedOnlineUser>[]>(() => [
    {
      id: 'view',
      label: 'View Session Details',
      icon: EyeIcon,
      onClick: (row) => {
        toast({
          title: "Session Details",
          description: `Viewing details for ${row.original.userName}`,
        });
      },
      variant: 'ghost',
    },
    {
      id: 'message',
      label: 'Send Message',
      icon: MessageCircleIcon,
      onClick: (row) => {
        toast({
          title: "Send Message",
          description: `Sending message to ${row.original.userName}`,
        });
      },
      variant: 'ghost',
    },
    {
      id: 'terminate',
      label: 'Terminate Session',
      icon: LogOutIcon,
      onClick: (row) => {
        toast({
          title: "Session Terminated",
          description: `Session terminated for ${row.original.userName}`,
          variant: "destructive",
        });
      },
      variant: 'ghost',
      disabled: (row) => row.original.role === 'SuperAdmin',
    },
  ], [toast]);

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
