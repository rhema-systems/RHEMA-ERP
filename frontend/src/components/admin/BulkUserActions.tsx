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
  Users,
  UserCheck,
  UserX,
  Mail,
  Download,
  Trash2,
  Settings,
  Shield
} from 'lucide-react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { User } from '../../services/admin-api.service';
import { ClientOnly } from '../ui/client-only';

interface BulkUserActionsProps {
  selectedUserIds: string[];
  users: User[];
  onActionComplete: () => void;
}

export interface BulkUpdateData {
  userIds: string[];
  updates: {
    isActive?: boolean;
    roles?: string[];
    department?: string;
    sendNotification?: boolean;
    notificationMessage?: string;
  };
}

const bulkUpdateSchema = z.object({
  action: z.enum(['activate', 'deactivate', 'change_role', 'assign_department', 'send_notification']),
  roles: z.array(z.string()).optional(),
  department: z.string().optional(),
  sendNotification: z.boolean().default(false),
  notificationMessage: z.string().optional(),
});

type BulkUpdateFormData = z.infer<typeof bulkUpdateSchema>;

const emailSchema = z.object({
  subject: z.string().min(1, 'Subject is required'),
  message: z.string().min(1, 'Message is required'),
  includeUsernames: z.boolean().default(true),
});

type EmailFormData = z.infer<typeof emailSchema>;

export function BulkUserActions({
  selectedUserIds,
  users,
  onActionComplete
}: BulkUserActionsProps) {
  const selectedUsers = users.filter(user => selectedUserIds.includes(user.id));
  const availableRoles = Array.from(new Set(users.flatMap(user => user.roles)));
  const [showUpdateDialog, setShowUpdateDialog] = useState(false);
  const [showEmailDialog, setShowEmailDialog] = useState(false);
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const updateForm = useForm<BulkUpdateFormData>({
    resolver: zodResolver(bulkUpdateSchema),
    defaultValues: {
      action: 'activate',
      sendNotification: false,
    },
  });

  const emailForm = useForm<EmailFormData>({
    resolver: zodResolver(emailSchema),
    defaultValues: {
      subject: '',
      message: '',
      includeUsernames: true,
    },
  });

  const handleBulkUpdate = async (data: BulkUpdateFormData) => {
    setIsLoading(true);
    try {
      // Mock bulk update operation
      console.log('Bulk update action:', data.action);
      console.log('Selected users:', selectedUsers.length);
      
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1000));
      
      setShowUpdateDialog(false);
      updateForm.reset();
      onActionComplete();
    } catch (error) {
      console.error('Bulk update failed:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleBulkEmail = async (data: EmailFormData) => {
    setIsLoading(true);
    try {
      // Mock bulk email operation
      console.log('Bulk email to:', selectedUsers.length, 'users');
      console.log('Subject:', data.subject);
      console.log('Message:', data.message);
      
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      setShowEmailDialog(false);
      emailForm.reset();
      onActionComplete();
    } catch (error) {
      console.error('Bulk email failed:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleBulkDelete = async () => {
    setIsLoading(true);
    try {
      // Mock bulk delete operation
      console.log('Bulk deleting:', selectedUsers.length, 'users');
      
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1200));
      
      setShowDeleteDialog(false);
      onActionComplete();
    } catch (error) {
      console.error('Bulk delete failed:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleExport = (format: 'csv' | 'excel') => {
    console.log('Exporting', selectedUsers.length, 'users as', format);
    // Mock export logic
    const filename = `users_export_${new Date().toISOString().split('T')[0]}.${format}`;
    console.log('Would download file:', filename);
    onActionComplete();
  };
  if (selectedUsers.length === 0) {
    return null;
  }

  const activeCount = selectedUsers.filter(u => u.isActive).length;
  const inactiveCount = selectedUsers.length - activeCount;

  return (
    <div className="flex items-center justify-between p-4 bg-blue-50 dark:bg-blue-950/20 border border-blue-200 dark:border-blue-800 rounded-lg">
      <div className="flex items-center space-x-3">
        <div className="flex items-center space-x-2">
          <Users className="h-5 w-5 text-blue-600" />
          <span className="font-medium text-blue-900 dark:text-blue-100">
            {selectedUsers.length} user{selectedUsers.length !== 1 ? 's' : ''} selected
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
        </div>
      </div>

      <div className="flex items-center space-x-2">
        {/* Bulk Actions Dropdown */}
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="outline" size="sm">
              <Settings className="h-4 w-4 mr-2" />
              Actions
              <ChevronDown className="h-4 w-4 ml-2" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuItem onClick={() => setShowUpdateDialog(true)}>
              <Shield className="h-4 w-4 mr-2" />
              Bulk Update
            </DropdownMenuItem>
            
            <DropdownMenuSeparator />
            
            <DropdownMenuItem onClick={() => setShowEmailDialog(true)}>
              <Mail className="h-4 w-4 mr-2" />
              Send Email
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
              Delete Users
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>

        <Button variant="outline" size="sm" onClick={onActionComplete}>
          Clear Selection
        </Button>
      </div>

      {/* Bulk Update Dialog */}
      <Dialog open={showUpdateDialog} onOpenChange={setShowUpdateDialog}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Bulk Update Users</DialogTitle>
            <DialogDescription>
              Apply changes to {selectedUsers.length} selected user{selectedUsers.length !== 1 ? 's' : ''}
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
                          <SelectItem value="activate">Activate Users</SelectItem>
                          <SelectItem value="deactivate">Deactivate Users</SelectItem>
                          <SelectItem value="change_role">Change Role</SelectItem>
                          <SelectItem value="assign_department">Assign Department</SelectItem>
                          <SelectItem value="send_notification">Send Notification</SelectItem>
                        </SelectContent>
                      </Select>
                    </ClientOnly>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {updateForm.watch('action') === 'change_role' && (
                <FormField
                  control={updateForm.control}
                  name="roles"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>New Role</FormLabel>
                      <ClientOnly fallback={
                        <div className="h-10 bg-muted/50 rounded-md border" />
                      }>
                        <Select 
                          onValueChange={(value) => field.onChange([value])}
                          value={field.value?.[0] || ''}
                        >
                          <FormControl>
                            <SelectTrigger>
                              <SelectValue placeholder="Select role" />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            {availableRoles.map((role) => (
                              <SelectItem key={role} value={role}>
                                {role}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </ClientOnly>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              {updateForm.watch('action') === 'assign_department' && (
                <FormField
                  control={updateForm.control}
                  name="department"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Department</FormLabel>
                      <FormControl>
                        <Input placeholder="Enter department name" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
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
                  {isLoading ? 'Updating...' : 'Update Users'}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Bulk Email Dialog */}
      <Dialog open={showEmailDialog} onOpenChange={setShowEmailDialog}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Send Email to Users</DialogTitle>
            <DialogDescription>
              Send an email to {selectedUsers.length} selected user{selectedUsers.length !== 1 ? 's' : ''}
            </DialogDescription>
          </DialogHeader>

          <Form {...emailForm}>
            <form onSubmit={emailForm.handleSubmit(handleBulkEmail)} className="space-y-4">
              <FormField
                control={emailForm.control}
                name="subject"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Subject</FormLabel>
                    <FormControl>
                      <Input placeholder="Email subject" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={emailForm.control}
                name="message"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Message</FormLabel>
                    <FormControl>
                      <Textarea
                        placeholder="Email message"
                        className="min-h-32"
                        {...field}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={emailForm.control}
                name="includeUsernames"
                render={({ field }) => (
                  <FormItem className="flex flex-row items-center justify-between rounded-lg border p-3">
                    <div className="space-y-0.5">
                      <FormLabel>Include recipient list</FormLabel>
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
                  onClick={() => setShowEmailDialog(false)}
                >
                  Cancel
                </Button>
                <Button type="submit" disabled={isLoading}>
                  {isLoading ? 'Sending...' : 'Send Email'}
                </Button>
              </DialogFooter>
            </form>
          </Form>
        </DialogContent>
      </Dialog>

      {/* Bulk Delete Dialog */}
      <Dialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Users</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete {selectedUsers.length} selected user{selectedUsers.length !== 1 ? 's' : ''}?
              This action cannot be undone.
            </DialogDescription>
          </DialogHeader>

          <div className="py-4">
            <div className="text-sm font-medium mb-2">Users to be deleted:</div>
            <div className="max-h-32 overflow-y-auto border rounded p-2 text-sm">
              {selectedUsers.map((user) => (
                <div key={user.id} className="flex justify-between py-1">
                  <span>{user.username}</span>
                  <span className="text-muted-foreground">{user.email}</span>
                </div>
              ))}
            </div>
          </div>

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
              {isLoading ? 'Deleting...' : 'Delete Users'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

export default BulkUserActions;