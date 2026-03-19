'use client';

import React, { useState } from 'react';
import { Button } from '../ui/button';
import { Badge } from '../ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../ui/dropdown-menu';
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
import { Input } from '../ui/input';
import { Textarea } from '../ui/textarea';
import { Switch } from '../ui/switch';
import {
  ChevronDown,
  Building,
  Settings,
  Mail,
  Download,
  Trash2,
  Shield,
  Activity,
  Copy,
  Archive,
  RefreshCw,
  Zap
} from 'lucide-react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { Tenant } from '../../services/admin-api.service';
import { ClientOnly } from '../ui/client-only';

interface BulkTenantActionsProps {
  selectedTenantIds: string[];
  tenants: Tenant[];
  onActionComplete: () => void;
}

export interface BulkTenantUpdateData {
  tenantIds: string[];
  updates: {
    status?: 'Active' | 'Inactive' | 'Suspended';
    allowSelfRegistration?: boolean;
    requireEmailVerification?: boolean;
    ldapEnabled?: boolean;
    userAudience?: number;
    defaultPriority?: number;
    sendNotification?: boolean;
    notificationMessage?: string;
  };
}

const bulkUpdateSchema = z.object({
  action: z.enum(['activate', 'deactivate', 'suspend', 'update_settings', 'sync_ldap', 'send_notification']),
  status: z.enum(['Active', 'Inactive', 'Suspended']).optional(),
  allowSelfRegistration: z.boolean().optional(),
  requireEmailVerification: z.boolean().optional(),
  ldapEnabled: z.boolean().optional(),
  userAudience: z.number().optional(),
  defaultPriority: z.number().optional(),
  sendNotification: z.boolean().default(false),
  notificationMessage: z.string().optional(),
});

type BulkUpdateFormValues = z.input<typeof bulkUpdateSchema>;
type BulkUpdateFormData = z.output<typeof bulkUpdateSchema>;

const notificationSchema = z.object({
  subject: z.string().min(1, 'Subject is required'),
  message: z.string().min(1, 'Message is required'),
  includeTenantDetails: z.boolean().default(true),
});

type NotificationFormValues = z.input<typeof notificationSchema>;
type NotificationFormData = z.output<typeof notificationSchema>;

export function BulkTenantActions({
  selectedTenantIds,
  tenants,
  onActionComplete
}: BulkTenantActionsProps) {
  const selectedTenants = tenants.filter(tenant => selectedTenantIds.includes(tenant.id));

  const [showUpdateDialog, setShowUpdateDialog] = useState(false);
  const [showNotificationDialog, setShowNotificationDialog] = useState(false);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const updateForm = useForm<BulkUpdateFormValues, any, BulkUpdateFormData>({
    resolver: zodResolver(bulkUpdateSchema),
    defaultValues: {
      action: 'activate',
      sendNotification: false,
    },
  });

  const notificationForm = useForm<NotificationFormValues, any, NotificationFormData>({
    resolver: zodResolver(notificationSchema),
    defaultValues: {
      subject: '',
      message: '',
      includeTenantDetails: true,
    },
  });

  const handleBulkUpdate = async (data: BulkUpdateFormData) => {
    setIsLoading(true);
    try {
      // Mock bulk update operation
      console.log('Bulk update action:', data.action);
      console.log('Selected tenants:', selectedTenants.length);
      
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 2000));
      
      setShowUpdateDialog(false);
      updateForm.reset();
      onActionComplete();
    } catch (error) {
      console.error('Bulk update failed:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleBulkNotification = async (data: NotificationFormData) => {
    setIsLoading(true);
    try {
      // Mock bulk notification operation
      console.log('Bulk notification to:', selectedTenants.length, 'tenants');
      console.log('Subject:', data.subject);
      console.log('Message:', data.message);
      
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      setShowNotificationDialog(false);
      notificationForm.reset();
      onActionComplete();
    } catch (error) {
      console.error('Bulk notification failed:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleBulkDelete = async () => {
    setIsLoading(true);
    try {
      // Mock bulk delete operation
      console.log('Bulk deleting:', selectedTenants.length, 'tenants');
      
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 2500));
      
      setShowDeleteDialog(false);
      onActionComplete();
    } catch (error) {
      console.error('Bulk delete failed:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleExport = (format: 'csv' | 'excel') => {
    console.log('Exporting', selectedTenants.length, 'tenants as', format);
    // Mock export logic
    const filename = `tenants_export_${new Date().toISOString().split('T')[0]}.${format}`;
    console.log('Would download file:', filename);
    onActionComplete();
  };

  const handleDuplicate = () => {
    console.log('Duplicating', selectedTenants.length, 'tenants');
    // Mock duplicate logic
    onActionComplete();
  };

  const handleArchive = () => {
    console.log('Archiving', selectedTenants.length, 'tenants');
    // Mock archive logic
    onActionComplete();
  };

  if (selectedTenants.length === 0) {
    return null;
  }

  const activeCount = selectedTenants.filter(t => t.isActive).length;
  const inactiveCount = selectedTenants.length - activeCount;
  const ldapEnabledCount = selectedTenants.filter(t => t.ldapEnabled).length;

  return (
    <>
      <div className="flex items-center justify-between p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <div className="flex items-center space-x-4">
          <div className="flex items-center space-x-2">
            <Building className="h-5 w-5 text-blue-600" />
            <span className="font-medium text-blue-900">
              {selectedTenants.length} tenant{selectedTenants.length !== 1 ? 's' : ''} selected
            </span>
          </div>
          
          <div className="flex items-center space-x-2">
            <Badge variant="outline" className="text-green-700 border-green-300">
              {activeCount} active
            </Badge>
            {inactiveCount > 0 && (
              <Badge variant="outline" className="text-red-700 border-red-300">
                {inactiveCount} inactive
              </Badge>
            )}
            {ldapEnabledCount > 0 && (
              <Badge variant="outline" className="text-blue-700 border-blue-300">
                {ldapEnabledCount} LDAP
              </Badge>
            )}
          </div>
        </div>

        <div className="flex items-center space-x-2">
          {/* Bulk Actions Dropdown */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm">
                <Settings className="h-4 w-4 mr-2" />
                Bulk Actions
                <ChevronDown className="h-4 w-4 ml-2" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              <DropdownMenuItem onClick={() => setShowUpdateDialog(true)}>
                <Shield className="h-4 w-4 mr-2" />
                Update Settings
              </DropdownMenuItem>
              
              <DropdownMenuItem onClick={() => setShowNotificationDialog(true)}>
                <Mail className="h-4 w-4 mr-2" />
                Send Notification
              </DropdownMenuItem>
              
              <DropdownMenuSeparator />
              
              <DropdownMenuItem onClick={handleDuplicate}>
                <Copy className="h-4 w-4 mr-2" />
                Duplicate Tenants
              </DropdownMenuItem>
              
              <DropdownMenuItem onClick={handleArchive}>
                <Archive className="h-4 w-4 mr-2" />
                Archive Tenants
              </DropdownMenuItem>
              
              <DropdownMenuSeparator />
              
              <DropdownMenuItem onClick={() => handleExport('csv')}>
                <Download className="h-4 w-4 mr-2" />
                Export as CSV
              </DropdownMenuItem>
              
              <DropdownMenuItem onClick={() => handleExport('excel')}>
                <Download className="h-4 w-4 mr-2" />
                Export as Excel
              </DropdownMenuItem>
              
              <DropdownMenuSeparator />
              
              <DropdownMenuItem 
                onClick={() => setShowDeleteDialog(true)}
                className="text-red-600 dark:text-red-400"
              >
                <Trash2 className="h-4 w-4 mr-2" />
                Delete Tenants
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>

          <Button variant="outline" size="sm" onClick={onActionComplete}>
            Clear Selection
          </Button>
        </div>
      </div>

      {/* Bulk Update Dialog */}
      <Dialog open={showUpdateDialog} onOpenChange={setShowUpdateDialog}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Bulk Update Tenants</DialogTitle>
            <DialogDescription>
              Apply changes to {selectedTenants.length} selected tenant{selectedTenants.length !== 1 ? 's' : ''}
            </DialogDescription>
          </DialogHeader>

          <Form {...updateForm}>
            <form onSubmit={updateForm.handleSubmit(handleBulkUpdate)} className="space-y-4">
              <FormField
                control={updateForm.control}
                name="action"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Action</FormLabel>
                    <ClientOnly fallback={
                      <div className="h-10 bg-muted/50 rounded-md border" />
                    }>
                      <Select onValueChange={field.onChange} defaultValue={field.value}>
                        <FormControl>
                          <SelectTrigger>
                            <SelectValue placeholder="Select action" />
                          </SelectTrigger>
                        </FormControl>
                        <SelectContent>
                          <SelectItem value="activate">Activate Tenants</SelectItem>
                          <SelectItem value="deactivate">Deactivate Tenants</SelectItem>
                          <SelectItem value="suspend">Suspend Tenants</SelectItem>
                          <SelectItem value="update_settings">Update Settings</SelectItem>
                          <SelectItem value="sync_ldap">Sync LDAP</SelectItem>
                          <SelectItem value="send_notification">Send Notification</SelectItem>
                        </SelectContent>
                      </Select>
                    </ClientOnly>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {updateForm.watch('action') === 'update_settings' && (
                <div className="space-y-4">
                  <FormField
                    control={updateForm.control}
                    name="allowSelfRegistration"
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                        <div className="space-y-0.5">
                          <FormLabel>Self Registration</FormLabel>
                        </div>
                        <FormControl>
                          <Switch
                            checked={field.value}
                            onCheckedChange={field.onChange}
                          />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  
                  <FormField
                    control={updateForm.control}
                    name="requireEmailVerification"
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                        <div className="space-y-0.5">
                          <FormLabel>Email Verification</FormLabel>
                        </div>
                        <FormControl>
                          <Switch
                            checked={field.value}
                            onCheckedChange={field.onChange}
                          />
                        </FormControl>
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={updateForm.control}
                    name="userAudience"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>User Audience</FormLabel>
                        <ClientOnly fallback={
                          <div className="h-10 bg-muted/50 rounded-md border" />
                        }>
                          <Select 
                            onValueChange={(value) => field.onChange(parseInt(value))}
                            value={field.value?.toString() || ''}
                          >
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue placeholder="Select audience" />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              <SelectItem value="1">Internal</SelectItem>
                              <SelectItem value="2">External</SelectItem>
                              <SelectItem value="3">Both</SelectItem>
                            </SelectContent>
                          </Select>
                        </ClientOnly>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={updateForm.control}
                    name="defaultPriority"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Default Priority</FormLabel>
                        <FormControl>
                          <Input
                            type="number"
                            min="1"
                            max="100"
                            placeholder="10"
                            {...field}
                            onChange={(e) => field.onChange(parseInt(e.target.value))}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
              )}

              {updateForm.watch('action') === 'send_notification' && (
                <>
                  <FormField
                    control={updateForm.control}
                    name="sendNotification"
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                        <div className="space-y-0.5">
                          <FormLabel>Send Notification</FormLabel>
                        </div>
                        <FormControl>
                          <Switch
                            checked={field.value}
                            onCheckedChange={field.onChange}
                          />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                  
                  {updateForm.watch('sendNotification') && (
                    <FormField
                      control={updateForm.control}
                      name="notificationMessage"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Message</FormLabel>
                          <FormControl>
                            <Textarea
                              placeholder="Enter notification message"
                              {...field}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  )}
                </>
              )}

              <DialogFooter>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowUpdateDialog(false)}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={isLoading}>
                  {isLoading ? (
                    <>
                      <RefreshCw className="h-4 w-4 mr-2 animate-spin" />
                      Updating...
                    </>
                  ) : (
                    'Update Tenants'
                  )}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Bulk Notification Dialog */}
      <Dialog open={showNotificationDialog} onOpenChange={setShowNotificationDialog}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Send Bulk Notification</DialogTitle>
            <DialogDescription>
              Send notification to {selectedTenants.length} tenant administrator{selectedTenants.length !== 1 ? 's' : ''}
            </DialogDescription>
          </DialogHeader>

          <Form {...notificationForm}>
            <form onSubmit={notificationForm.handleSubmit(handleBulkNotification)} className="space-y-4">
              <FormField
                control={notificationForm.control}
                name="subject"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Subject</FormLabel>
                    <FormControl>
                      <Input placeholder="Notification subject" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={notificationForm.control}
                name="message"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Message</FormLabel>
                    <FormControl>
                      <Textarea
                        placeholder="Enter your message..."
                        rows={4}
                        {...field}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={notificationForm.control}
                name="includeTenantDetails"
                render={({ field }) => (
                  <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                    <div className="space-y-0.5">
                      <FormLabel>Include Tenant Details</FormLabel>
                    </div>
                    <FormControl>
                      <Switch
                        checked={field.value}
                        onCheckedChange={field.onChange}
                      />
                    </FormControl>
                  </FormItem>
                )}
              />

              <DialogFooter>
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowNotificationDialog(false)}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={isLoading}>
                  {isLoading ? (
                    <>
                      <RefreshCw className="h-4 w-4 mr-2 animate-spin" />
                      Sending...
                    </>
                  ) : (
                    'Send Notification'
                  )}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <Dialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Tenants</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete {selectedTenants.length} tenant{selectedTenants.length !== 1 ? 's' : ''}?
              <br /><br />
              <strong className="text-destructive">This will permanently delete:</strong>
              <ul className="mt-2 ml-4 list-disc text-sm">
                <li>All tenant data and configurations</li>
                <li>All users associated with these tenants</li>
                <li>All tenant-specific settings and branding</li>
                <li>Any custom integrations or LDAP configurations</li>
              </ul>
              <br />
              <strong className="text-destructive">This action cannot be undone.</strong>
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setShowDeleteDialog(false)}
            >
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleBulkDelete}
              disabled={isLoading}
            >
              {isLoading ? (
                <>
                  <RefreshCw className="h-4 w-4 mr-2 animate-spin" />
                  Deleting...
                </>
              ) : (
                'Delete All Tenants'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

export default BulkTenantActions;
