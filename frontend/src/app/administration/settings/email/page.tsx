'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { EmailTemplateDesigner } from '../../../../components/email-template-designer';
import { emailTemplateService, type EmailTemplate } from '../../../../services/email-template.service';
import { DashboardLayout } from '../../../../components/layout/dashboard-layout';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../../components/ui/card';
import { Button } from '../../../../components/ui/button';
import { Input } from '../../../../components/ui/input';
import { Label } from '../../../../components/ui/label';
import { Switch } from '../../../../components/ui/switch';
import { Textarea } from '../../../../components/ui/textarea';
import { Badge } from '../../../../components/ui/badge';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '../../../../components/ui/form';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../../components/ui/dialog';
import {
  Alert,
  AlertDescription,
  AlertTitle,
} from '../../../../components/ui/alert';
import { ConfirmationDialog } from '../../../../components/ui/confirmation-dialog';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { adminApiService, EmailSettings } from '../../../../services/admin-api.service';
import { useToast } from '../../../../hooks/use-toast';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../../components/ui/tabs';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../../../components/ui/select';
import {
  Mail, 
  Send, 
  Server, 
  Shield, 
  CheckCircle, 
  XCircle,
  AlertTriangle,
  Settings,
  FileText,
  Plus,
  Edit,
  Trash2,
  Database,
  Eye
} from 'lucide-react';

const emailSettingsSchema = z.object({
  smtpHost: z.string().min(1, 'SMTP Host is required'),
  smtpPort: z.number().min(1, 'SMTP Port must be greater than 0').max(65535, 'Invalid port number'),
  smtpUsername: z.string().min(1, 'SMTP Username is required'),
  smtpPassword: z.string().min(1, 'SMTP Password is required'),
  useTLS: z.boolean(),
  fromAddress: z.string().email('Invalid email address'),
  fromName: z.string().min(1, 'From Name is required'),
});

const testEmailSchema = z.object({
  testEmail: z.string().email('Invalid email address'),
  subject: z.string().min(1, 'Subject is required'),
  message: z.string().min(1, 'Message is required'),
});

type EmailSettingsFormData = z.infer<typeof emailSettingsSchema>;
type TestEmailFormData = z.infer<typeof testEmailSchema>;

// EmailTemplate type is now imported from the service

export default function EmailSettingsPage() {
  const [isTestDialogOpen, setIsTestDialogOpen] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [isTemplateDesignerOpen, setIsTemplateDesignerOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<EmailTemplate | undefined>(undefined);
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);
  const [templateToDelete, setTemplateToDelete] = useState<string | null>(null);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const form = useForm<EmailSettingsFormData>({
    resolver: zodResolver(emailSettingsSchema),
    defaultValues: {
      smtpHost: '',
      smtpPort: 587,
      smtpUsername: '',
      smtpPassword: '',
      useTLS: true,
      fromAddress: '',
      fromName: '',
    },
  });

  const testForm = useForm<TestEmailFormData>({
    resolver: zodResolver(testEmailSchema),
    defaultValues: {
      testEmail: '',
      subject: 'Test Email from ERP System',
      message: 'This is a test email to verify your SMTP configuration is working correctly.',
    },
  });

  // Fetch email settings data
  const { data: emailSettings, isLoading } = useQuery({
    queryKey: ['email-settings'],
    queryFn: () => adminApiService.getEmailSettings(),
  });

  // Fetch email templates
  const { data: templates, isLoading: templatesLoading } = useQuery({
    queryKey: ['email-templates'],
    queryFn: () => emailTemplateService.getTemplates()
  });

  // Update form when data loads
  React.useEffect(() => {
    if (emailSettings) {
      form.reset({
        smtpHost: emailSettings.smtpHost,
        smtpPort: emailSettings.smtpPort,
        smtpUsername: emailSettings.smtpUsername,
        smtpPassword: emailSettings.smtpPassword,
        useTLS: emailSettings.useTLS,
        fromAddress: emailSettings.fromAddress,
        fromName: emailSettings.fromName,
      });
    }
  }, [emailSettings, form]);

  // Update email settings mutation
  const updateSettingsMutation = useMutation({
    mutationFn: (settings: EmailSettings) => adminApiService.updateEmailSettings(settings),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['email-settings'] });
      toast({
        title: 'Success',
        description: 'Email settings updated successfully',
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to update email settings',
        variant: 'destructive',
      });
    },
  });

  // Test email mutation
  const testEmailMutation = useMutation({
    mutationFn: (data: { settings: EmailSettings; testEmail: string }) => 
      adminApiService.testEmailSettings(data.settings, data.testEmail),
    onSuccess: (result) => {
      setTestResult(result);
      if (result.success) {
        toast({
          title: 'Test Successful',
          description: result.message,
        });
      } else {
        toast({
          title: 'Test Failed',
          description: result.message,
          variant: 'destructive',
        });
      }
    },
    onError: () => {
      setTestResult({
        success: false,
        message: 'Failed to send test email. Please check your connection.',
      });
      toast({
        title: 'Error',
        description: 'Failed to send test email',
        variant: 'destructive',
      });
    },
  });

  // Create/Update template mutation
  const templateMutation = useMutation({
    mutationFn: async (template: EmailTemplate) => {
      if (template.id) {
        return emailTemplateService.updateTemplate(template.id, template);
      } else {
        return emailTemplateService.createTemplate(template);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['email-templates'] });
      toast({
        title: 'Success',
        description: 'Email template saved successfully'
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to save email template',
        variant: 'destructive'
      });
    }
  });

  // Delete template mutation
  const deleteTemplateMutation = useMutation({
    mutationFn: (templateId: string) => emailTemplateService.deleteTemplate(templateId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['email-templates'] });
      toast({
        title: 'Success',
        description: 'Email template deleted successfully'
      });
    },
    onError: () => {
      toast({
        title: 'Error',
        description: 'Failed to delete email template',
        variant: 'destructive'
      });
    }
  });

  const onSubmit = (data: EmailSettingsFormData) => {
    updateSettingsMutation.mutate(data);
  };

  const onTestEmail = (data: TestEmailFormData) => {
    const currentSettings = form.getValues();
    testEmailMutation.mutate({
      settings: currentSettings,
      testEmail: data.testEmail,
    });
  };

  const handleTestEmail = () => {
    // Reset test result when opening dialog
    setTestResult(null);
    setIsTestDialogOpen(true);
  };

  const handleCreateTemplate = () => {
    setEditingTemplate(undefined);
    setIsTemplateDesignerOpen(true);
  };

  const handleEditTemplate = (template: EmailTemplate) => {
    setEditingTemplate(template);
    setIsTemplateDesignerOpen(true);
  };

  const handleDeleteTemplate = (templateId: string) => {
    setTemplateToDelete(templateId);
    setShowDeleteDialog(true);
  };

  const confirmDeleteTemplate = () => {
    if (templateToDelete) {
      deleteTemplateMutation.mutate(templateToDelete);
      setTemplateToDelete(null);
    }
  };

  const handleSaveTemplate = (template: EmailTemplate) => {
    templateMutation.mutate(template);
  };

  // Filter templates based on search and category
  const filteredTemplates = templates?.filter(template => {
    const matchesSearch = !searchQuery || 
      template.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      template.description?.toLowerCase().includes(searchQuery.toLowerCase()) ||
      template.module.toLowerCase().includes(searchQuery.toLowerCase());
    
    const matchesCategory = selectedCategory === 'all' || 
      template.category?.toLowerCase() === selectedCategory;
    
    return matchesSearch && matchesCategory;
  }) || [];

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Email Settings</h1>
          <p className="text-muted-foreground">
            Configure SMTP settings and manage email templates
          </p>
        </div>

        <Tabs defaultValue="settings" className="space-y-6">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="settings" className="flex items-center gap-2">
              <Server className="h-4 w-4" />
              Email Settings
            </TabsTrigger>
            <TabsTrigger value="templates" className="flex items-center gap-2">
              <FileText className="h-4 w-4" />
              Email Templates
            </TabsTrigger>
          </TabsList>

          <TabsContent value="settings">

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* SMTP Configuration */}
          <div className="lg:col-span-2">
            <Card>
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Server className="h-5 w-5 text-primary" />
                  <CardTitle>SMTP Configuration</CardTitle>
                </div>
                <CardDescription>
                  Configure your SMTP server settings for sending emails
                </CardDescription>
              </CardHeader>
              <CardContent>
                <Form {...form}>
                  <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <FormField
                        control={form.control}
                        name="smtpHost"
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>SMTP Host *</FormLabel>
                            <FormControl>
                              <Input placeholder="smtp.gmail.com" {...field} />
                            </FormControl>
                            <FormDescription>
                              SMTP server hostname or IP address
                            </FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name="smtpPort"
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>SMTP Port *</FormLabel>
                            <FormControl>
                              <Input 
                                type="number" 
                                placeholder="587" 
                                {...field}
                                onChange={(e) => field.onChange(parseInt(e.target.value) || 0)}
                              />
                            </FormControl>
                            <FormDescription>
                              Common ports: 25, 587, 465, 2525
                            </FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name="smtpUsername"
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Username *</FormLabel>
                            <FormControl>
                              <Input placeholder="your-email@domain.com" {...field} />
                            </FormControl>
                            <FormDescription>
                              SMTP authentication username
                            </FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name="smtpPassword"
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Password *</FormLabel>
                            <FormControl>
                              <Input 
                                type="password" 
                                placeholder="••••••••" 
                                {...field} 
                              />
                            </FormControl>
                            <FormDescription>
                              SMTP authentication password
                            </FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                    </div>

                    <FormField
                      control={form.control}
                      name="useTLS"
                      render={({ field }) => (
                        <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                          <div className="space-y-0.5">
                            <div className="flex items-center gap-2">
                              <Shield className="h-4 w-4" />
                              <FormLabel className="text-base">Use TLS/SSL</FormLabel>
                            </div>
                            <FormDescription>
                              Enable secure connection using TLS/SSL encryption
                            </FormDescription>
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

                    <div className="space-y-4">
                      <div className="flex items-center gap-2">
                        <Mail className="h-4 w-4" />
                        <h3 className="text-lg font-medium">Email Settings</h3>
                      </div>

                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <FormField
                          control={form.control}
                          name="fromAddress"
                          render={({ field }) => (
                            <FormItem>
                              <FormLabel>From Email Address *</FormLabel>
                              <FormControl>
                                <Input 
                                  type="email" 
                                  placeholder="noreply@yourcompany.com" 
                                  {...field} 
                                />
                              </FormControl>
                              <FormDescription>
                                Email address used as sender
                              </FormDescription>
                              <FormMessage />
                            </FormItem>
                          )}
                        />

                        <FormField
                          control={form.control}
                          name="fromName"
                          render={({ field }) => (
                            <FormItem>
                              <FormLabel>From Name *</FormLabel>
                              <FormControl>
                                <Input placeholder="ERP System" {...field} />
                              </FormControl>
                              <FormDescription>
                                Display name for sent emails
                              </FormDescription>
                              <FormMessage />
                            </FormItem>
                          )}
                        />
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <Button
                        type="submit"
                        disabled={updateSettingsMutation.isPending}
                        className="flex items-center gap-2"
                      >
                        <Settings className="h-4 w-4" />
                        {updateSettingsMutation.isPending ? 'Saving...' : 'Save Settings'}
                      </Button>

                      <Button
                        type="button"
                        variant="outline"
                        onClick={handleTestEmail}
                        className="flex items-center gap-2"
                      >
                        <Send className="h-4 w-4" />
                        Test Email
                      </Button>
                    </div>
                  </form>
                </Form>
              </CardContent>
            </Card>
          </div>

          {/* Quick Setup Guide */}
          <div className="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Quick Setup Guide</CardTitle>
                <CardDescription>
                  Common SMTP configurations for popular providers
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-2">
                  <h4 className="font-medium">Gmail</h4>
                  <div className="text-sm text-muted-foreground space-y-1">
                    <p>Host: smtp.gmail.com</p>
                    <p>Port: 587 (TLS)</p>
                    <p>Use app passwords for 2FA accounts</p>
                  </div>
                </div>

                <div className="space-y-2">
                  <h4 className="font-medium">Outlook/Office 365</h4>
                  <div className="text-sm text-muted-foreground space-y-1">
                    <p>Host: smtp.office365.com</p>
                    <p>Port: 587 (TLS)</p>
                    <p>Use your full email address</p>
                  </div>
                </div>

                <div className="space-y-2">
                  <h4 className="font-medium">SendGrid</h4>
                  <div className="text-sm text-muted-foreground space-y-1">
                    <p>Host: smtp.sendgrid.net</p>
                    <p>Port: 587 (TLS)</p>
                    <p>Username: apikey</p>
                  </div>
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle className="text-lg">Security Notes</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-sm text-muted-foreground">
                <div className="flex items-start gap-2">
                  <Shield className="h-4 w-4 mt-0.5 text-primary" />
                  <p>Always use TLS/SSL for secure communication</p>
                </div>
                <div className="flex items-start gap-2">
                  <AlertTriangle className="h-4 w-4 mt-0.5 text-yellow-500" />
                  <p>Use app-specific passwords for accounts with 2FA</p>
                </div>
                <div className="flex items-start gap-2">
                  <CheckCircle className="h-4 w-4 mt-0.5 text-green-500" />
                  <p>Test your configuration before saving</p>
                </div>
              </CardContent>
            </Card>
          </div>
        </div>
          </TabsContent>

          <TabsContent value="templates">
            <div className="space-y-6">
              <Card>
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="flex items-center gap-2">
                        <FileText className="h-5 w-5 text-primary" />
                        <CardTitle>Email Templates</CardTitle>
                      </div>
                      <CardDescription>
                        Create and manage professional email templates with dynamic database field integration
                      </CardDescription>
                    </div>
                    <Button 
                      onClick={handleCreateTemplate}
                      className="flex items-center gap-2"
                    >
                      <Plus className="h-4 w-4" />
                      Create Template
                    </Button>
                  </div>
                </CardHeader>
                <CardContent>
                  {templatesLoading ? (
                    <div className="flex items-center justify-center py-8">
                      <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
                      <span className="ml-2 text-muted-foreground">Loading templates...</span>
                    </div>
                  ) : templates && templates.length > 0 ? (
                    <>
                      {/* Search and Filter Bar */}
                      <div className="flex flex-col sm:flex-row gap-4 mb-6">
                        <div className="flex-1">
                          <Input
                            placeholder="Search templates..."
                            value={searchQuery}
                            onChange={(e) => setSearchQuery(e.target.value)}
                            className="max-w-sm"
                          />
                        </div>
                        <Select value={selectedCategory} onValueChange={setSelectedCategory}>
                          <SelectTrigger className="w-[180px]">
                            <SelectValue placeholder="Filter by category" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="all">All Categories</SelectItem>
                            <SelectItem value="notification">Notifications</SelectItem>
                            <SelectItem value="marketing">Marketing</SelectItem>
                            <SelectItem value="transactional">Transactional</SelectItem>
                            <SelectItem value="system">System</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                      
                      {/* Results Counter */}
                      <div className="flex items-center justify-between mb-4">
                        <p className="text-sm text-muted-foreground">
                          Showing {filteredTemplates.length} of {templates.length} templates
                        </p>
                      </div>
                      
                      {filteredTemplates.length === 0 ? (
                        <div className="text-center py-12">
                          <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                          <h3 className="text-lg font-medium mb-2">No templates found</h3>
                          <p className="text-muted-foreground mb-4">
                            {searchQuery || selectedCategory !== 'all' 
                              ? 'No templates match your current filters. Try adjusting your search or category filter.'
                              : 'No templates have been created yet.'}
                          </p>
                          {(!searchQuery && selectedCategory === 'all') && (
                            <Button 
                              onClick={handleCreateTemplate}
                              className="flex items-center gap-2"
                            >
                              <Plus className="h-4 w-4" />
                              Create Your First Template
                            </Button>
                          )}
                        </div>
                      ) : (
                        <div className="space-y-4">
                          <div className="grid gap-4">
                          {filteredTemplates.map((template) => (
                          <div key={template.id} className="flex items-center justify-between p-4 border rounded-lg hover:bg-muted/50 transition-colors">
                            <div className="space-y-1 flex-1">
                              <div className="flex items-center gap-2">
                                <h4 className="font-medium">{template.name}</h4>
                                {template.module && (
                                  <Badge variant="secondary">{template.module}</Badge>
                                )}
                                {template.category && (
                                  <Badge variant="outline">{template.category}</Badge>
                                )}
                              </div>
                              {template.description && (
                                <p className="text-sm text-muted-foreground">
                                  {template.description}
                                </p>
                              )}
                              <div className="flex items-center gap-4 text-xs text-muted-foreground">
                                {template.tableName && (
                                  <span className="flex items-center gap-1">
                                    <Database className="h-3 w-3" />
                                    {template.tableName}
                                  </span>
                                )}
                                {template.createdAt && (
                                  <span>Created: {new Date(template.createdAt).toLocaleDateString()}</span>
                                )}
                                {template.modifiedAt && (
                                  <span>Modified: {new Date(template.modifiedAt).toLocaleDateString()}</span>
                                )}
                              </div>
                            </div>
                            <div className="flex items-center gap-2 ml-4">
                              <Button 
                                variant="ghost" 
                                size="sm"
                                onClick={() => handleEditTemplate(template)}
                              >
                                <Edit className="h-4 w-4" />
                              </Button>
                              <Button 
                                variant="ghost" 
                                size="sm"
                                onClick={() => template.id && handleDeleteTemplate(template.id)}
                                className="text-destructive hover:text-destructive"
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </div>
                          </div>
                        ))}
                          </div>
                        </div>
                      )}
                    </>
                  ) : (
                    <div className="text-center py-12">
                      <FileText className="h-12 w-12 text-muted-foreground mx-auto mb-4" />
                      <h3 className="text-lg font-semibold mb-2">No templates yet</h3>
                      <p className="text-muted-foreground mb-4">
                        Create your first email template to get started with automated notifications and dynamic content.
                      </p>
                      <Button 
                        onClick={handleCreateTemplate}
                        className="flex items-center gap-2"
                      >
                        <Plus className="h-4 w-4" />
                        Create Your First Template
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>
            </div>
          </TabsContent>
        </Tabs>

        {/* Test Email Dialog */}
        <Dialog open={isTestDialogOpen} onOpenChange={setIsTestDialogOpen}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Send className="h-5 w-5" />
                Test Email Configuration
              </DialogTitle>
              <DialogDescription>
                Send a test email to verify your SMTP settings are working correctly
              </DialogDescription>
            </DialogHeader>

            {testResult && (
              <Alert className={testResult.success ? 'border-green-200' : 'border-red-200'}>
                {testResult.success ? (
                  <CheckCircle className="h-4 w-4 text-green-500" />
                ) : (
                  <XCircle className="h-4 w-4 text-red-500" />
                )}
                <AlertTitle>
                  {testResult.success ? 'Test Successful' : 'Test Failed'}
                </AlertTitle>
                <AlertDescription>{testResult.message}</AlertDescription>
              </Alert>
            )}

            <Form {...testForm}>
              <form onSubmit={testForm.handleSubmit(onTestEmail)} className="space-y-4">
                <FormField
                  control={testForm.control}
                  name="testEmail"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Test Email Address *</FormLabel>
                      <FormControl>
                        <Input 
                          type="email" 
                          placeholder="test@example.com" 
                          {...field} 
                        />
                      </FormControl>
                      <FormDescription>
                        Email address to send the test message to
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={testForm.control}
                  name="subject"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Subject *</FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={testForm.control}
                  name="message"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Message *</FormLabel>
                      <FormControl>
                        <Textarea 
                          rows={4} 
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
                    onClick={() => setIsTestDialogOpen(false)}
                  >
                    Cancel
                  </Button>
                  <Button
                    type="submit"
                    disabled={testEmailMutation.isPending}
                    className="flex items-center gap-2"
                  >
                    <Send className="h-4 w-4" />
                    {testEmailMutation.isPending ? 'Sending...' : 'Send Test Email'}
                  </Button>
                </DialogFooter>
              </form>
            </Form>
          </DialogContent>
        </Dialog>

        <EmailTemplateDesigner
          isOpen={isTemplateDesignerOpen}
          onClose={() => {
            setIsTemplateDesignerOpen(false);
            setEditingTemplate(undefined);
          }}
          template={editingTemplate}
          onSave={handleSaveTemplate}
        />

        {/* Delete Confirmation Dialog */}
        <ConfirmationDialog
          open={showDeleteDialog}
          onOpenChange={setShowDeleteDialog}
          title="Delete Email Template"
          description="Are you sure you want to delete this email template? This action cannot be undone and may affect automated notifications that use this template."
          confirmText="Delete"
          cancelText="Cancel"
          variant="destructive"
          onConfirm={confirmDeleteTemplate}
          isLoading={deleteTemplateMutation.isPending}
        />
      </div>
  );
}
