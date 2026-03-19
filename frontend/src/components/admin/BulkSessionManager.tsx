'use client';

import React, { useState, useEffect, useCallback } from 'react';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';
import { Input } from '../ui/input';
import { Label } from '../ui/label';
import { Textarea } from '../ui/textarea';
import { Checkbox } from '../ui/checkbox';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../ui/table';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
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
  Shield,
  Users,
  Monitor,
  Clock,
  MapPin,
  Globe,
  Smartphone,
  Laptop,
  Tablet,
  MoreVertical,
  Power,
  PowerOff,
  Filter,
  RefreshCw,
  AlertTriangle,
  CheckCircle,
  XCircle,
  Search,
  Calendar,
  X,
  UserX,
  Ban
} from 'lucide-react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { formatDistanceToNow, format } from 'date-fns';
import { securityService, UserSession, SessionFilter, BulkSessionOperationResponse, TerminateSessionsByCriteriaRequest } from '../../services/security';
import { useToast } from '../../hooks/use-toast';
import { ClientOnly } from '../ui/client-only';

// Form schemas
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

type FilterFormValues = z.input<typeof filterSchema>;
type FilterFormData = z.output<typeof filterSchema>;
type TerminateReasonFormValues = z.input<typeof terminateReasonSchema>;
type TerminateReasonFormData = z.output<typeof terminateReasonSchema>;
type BulkCriteriaFormValues = z.input<typeof bulkCriteriaSchema>;
type BulkCriteriaFormData = z.output<typeof bulkCriteriaSchema>;

interface BulkSessionManagerProps {
  className?: string;
}

export function BulkSessionManager({ className }: BulkSessionManagerProps) {
  const { toast } = useToast();
  const [sessions, setSessions] = useState<UserSession[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [selectedSessions, setSelectedSessions] = useState<Set<string>>(new Set());
  const [showFilters, setShowFilters] = useState(false);
  const [showTerminateDialog, setShowTerminateDialog] = useState(false);
  const [showBulkTerminateDialog, setShowBulkTerminateDialog] = useState(false);
  const [showCriteriaTerminateDialog, setShowCriteriaTerminateDialog] = useState(false);
  const [terminatingSession, setTerminatingSession] = useState<string | null>(null);
  const [bulkOperationInProgress, setBulkOperationInProgress] = useState(false);
  const [lastOperationResult, setLastOperationResult] = useState<BulkSessionOperationResponse | null>(null);

  // Form instances
  const filterForm = useForm<FilterFormValues, any, FilterFormData>({
    resolver: zodResolver(filterSchema),
    defaultValues: {
      username: '',
      ipAddress: '',
      deviceType: '',
      loginTimeAfter: undefined,
      lastActivityAfter: undefined
    }
  });

  const terminateReasonForm = useForm<TerminateReasonFormValues, any, TerminateReasonFormData>({
    resolver: zodResolver(terminateReasonSchema),
    defaultValues: {
      reason: ''
    }
  });

  const bulkCriteriaForm = useForm<BulkCriteriaFormValues, any, BulkCriteriaFormData>({
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

  // Load sessions with filters
  const loadSessions = useCallback(async (filters?: SessionFilter) => {
    try {
      setRefreshing(true);
      const data = await securityService.getAllActiveSessions(filters);
      setSessions(data);
    } catch (error) {
      console.error('Failed to load sessions:', error);
      toast({ title: 'Error', description: 'Failed to load active sessions', variant: 'destructive' });
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [toast]);

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

  // Utility functions
  const getDeviceIcon = (deviceType: string) => {
    switch (deviceType.toLowerCase()) {
      case 'mobile':
        return <Smartphone className="h-4 w-4" />;
      case 'tablet':
        return <Tablet className="h-4 w-4" />;
      case 'desktop':
      default:
        return <Laptop className="h-4 w-4" />;
    }
  };

  const getSessionDurationColor = (loginTime: Date) => {
    const hours = (Date.now() - loginTime.getTime()) / (1000 * 60 * 60);
    if (hours > 24) return 'text-red-600';
    if (hours > 8) return 'text-orange-600';
    return 'text-green-600';
  };

  const getActivityStatus = (lastActivity: Date) => {
    const minutes = (Date.now() - lastActivity.getTime()) / (1000 * 60);
    if (minutes < 5) return { status: 'active', color: 'bg-green-500' };
    if (minutes < 30) return { status: 'idle', color: 'bg-yellow-500' };
    return { status: 'inactive', color: 'bg-red-500' };
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <RefreshCw className="h-8 w-8 animate-spin" />
        <span className="ml-2">Loading active sessions...</span>
      </div>
    );
  }

  return (
    <div className={className}>
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center space-x-2">
          <Shield className="h-6 w-6 text-blue-600" />
          <h2 className="text-2xl font-semibold">Session Management</h2>
          <Badge variant="outline" className="ml-2">
            {sessions.length} active sessions
          </Badge>
        </div>
        
        <div className="flex items-center space-x-2">
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
            <RefreshCw className={`h-4 w-4 mr-2 ${refreshing ? 'animate-spin' : ''}`} />
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
            
            <Button
              variant="outline"
              size="sm"
              onClick={() => setSelectedSessions(new Set())}
            >
              Clear Selection
            </Button>
          </div>
        </div>
      )}

      {/* Sessions Table */}
      <div className="border rounded-lg">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-12">
                <Checkbox
                  checked={selectedSessions.size === sessions.length && sessions.length > 0}
                  onCheckedChange={handleSelectAll}
                />
              </TableHead>
              <TableHead>User</TableHead>
              <TableHead>Device</TableHead>
              <TableHead>Location</TableHead>
              <TableHead>Session Info</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-12"></TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {sessions.map((session) => {
              const activityStatus = getActivityStatus(session.lastActivityTime);
              return (
                <TableRow key={session.sessionId}>
                  <TableCell>
                    <Checkbox
                      checked={selectedSessions.has(session.sessionId)}
                      onCheckedChange={(checked) => 
                        handleSelectSession(session.sessionId, checked as boolean)
                      }
                    />
                  </TableCell>
                  
                  <TableCell>
                    <div>
                      <div className="font-medium">{session.username}</div>
                      <div className="text-sm text-muted-foreground">{session.userId}</div>
                    </div>
                  </TableCell>
                  
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      {getDeviceIcon(session.deviceType)}
                      <div>
                        <div className="font-medium">{session.deviceType}</div>
                        <div className="text-sm text-muted-foreground">
                          {session.browser} • {session.operatingSystem}
                        </div>
                      </div>
                    </div>
                  </TableCell>
                  
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <MapPin className="h-4 w-4 text-muted-foreground" />
                      <div>
                        <div className="font-medium">{session.location || 'Unknown'}</div>
                        <div className="text-sm text-muted-foreground">{session.ipAddress}</div>
                      </div>
                    </div>
                  </TableCell>
                  
                  <TableCell>
                    <div>
                      <div className="text-sm">
                        <Clock className="h-3 w-3 inline mr-1" />
                        Login: {format(session.loginTime, 'MMM d, HH:mm')}
                      </div>
                      <div className="text-sm text-muted-foreground">
                        Duration: <span className={getSessionDurationColor(session.loginTime)}>
                          {formatDistanceToNow(session.loginTime)}
                        </span>
                      </div>
                      <div className="text-sm text-muted-foreground">
                        Last activity: {formatDistanceToNow(session.lastActivityTime)} ago
                      </div>
                    </div>
                  </TableCell>
                  
                  <TableCell>
                    <div className="flex items-center space-x-2">
                      <div className={`w-2 h-2 rounded-full ${activityStatus.color}`}></div>
                      <span className="text-sm capitalize">{activityStatus.status}</span>
                    </div>
                  </TableCell>
                  
                  <TableCell>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="sm">
                          <MoreVertical className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem onClick={() => handleTerminateSession(session.sessionId)}>
                          <PowerOff className="h-4 w-4 mr-2" />
                          Terminate Session
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem>
                          <UserX className="h-4 w-4 mr-2" />
                          Terminate All User Sessions
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
        
        {sessions.length === 0 && (
          <div className="text-center py-8 text-muted-foreground">
            No active sessions found
          </div>
        )}
      </div>

      {/* Last Operation Result */}
      {lastOperationResult && (
        <div className="mt-4 p-4 bg-muted rounded-lg">
          <h4 className="font-medium mb-2">Last Operation Result</h4>
          <div className="grid grid-cols-3 gap-4 text-sm">
            <div>
              <div className="text-muted-foreground">Total Requested</div>
              <div className="font-medium">{lastOperationResult.totalRequested}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Successful</div>
              <div className="font-medium text-green-600">{lastOperationResult.successful}</div>
            </div>
            <div>
              <div className="text-muted-foreground">Failed</div>
              <div className="font-medium text-red-600">{lastOperationResult.failed}</div>
            </div>
          </div>
          {lastOperationResult.message && (
            <div className="mt-2 text-sm text-muted-foreground">
              {lastOperationResult.message}
            </div>
          )}
        </div>
      )}

      {/* Terminate Single Session Dialog */}
      <Dialog open={showTerminateDialog} onOpenChange={setShowTerminateDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Terminate Session</DialogTitle>
            <DialogDescription>
              Please provide a reason for terminating this session.
            </DialogDescription>
          </DialogHeader>
          
          <Form {...terminateReasonForm}>
            <form onSubmit={terminateReasonForm.handleSubmit(confirmTerminateSession)} className="space-y-4">
              <FormField
                control={terminateReasonForm.control}
                name="reason"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Reason</FormLabel>
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
              
              <DialogFooter>
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
              You are about to terminate {selectedSessions.size} session{selectedSessions.size !== 1 ? 's' : ''}. 
              Please provide a reason.
            </DialogDescription>
          </DialogHeader>
          
          <Form {...terminateReasonForm}>
            <form onSubmit={terminateReasonForm.handleSubmit(confirmBulkTerminate)} className="space-y-4">
              <FormField
                control={terminateReasonForm.control}
                name="reason"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Reason</FormLabel>
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
              
              <DialogFooter>
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
              Terminate sessions that match specific criteria. Use with caution.
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
                        <Input placeholder="e.g., 192.168.1" {...field} />
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
                      <FormControl>
                        <Input placeholder="e.g., Mobile" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                
                <FormField
                  control={bulkCriteriaForm.control}
                  name="browser"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Browser</FormLabel>
                      <FormControl>
                        <Input placeholder="e.g., Chrome" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                
                <FormField
                  control={bulkCriteriaForm.control}
                  name="includeCurrentUser"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
                      <FormControl>
                        <Checkbox
                          checked={field.value}
                          onCheckedChange={field.onChange}
                        />
                      </FormControl>
                      <div className="space-y-1 leading-none">
                        <FormLabel>Include Current User</FormLabel>
                        <p className="text-sm text-muted-foreground">
                          Terminate sessions belonging to the current user
                        </p>
                      </div>
                    </FormItem>
                  )}
                />
              </div>
              
              <FormField
                control={bulkCriteriaForm.control}
                name="reason"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Reason</FormLabel>
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
              
              <div className="flex items-center space-x-2 p-3 bg-yellow-50 dark:bg-yellow-950/20 border border-yellow-200 dark:border-yellow-800 rounded-lg">
                <AlertTriangle className="h-4 w-4 text-yellow-600" />
                <span className="text-sm text-yellow-800 dark:text-yellow-200">
                  This will terminate sessions matching the specified criteria. Review carefully before proceeding.
                </span>
              </div>
              
              <DialogFooter>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowCriteriaTerminateDialog(false)}
                >
                  Cancel
                </Button>
                <Button type="submit" variant="destructive" disabled={bulkOperationInProgress}>
                  {bulkOperationInProgress ? 'Processing...' : 'Terminate Sessions'}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>
    </div>
  );
}

export default BulkSessionManager;
