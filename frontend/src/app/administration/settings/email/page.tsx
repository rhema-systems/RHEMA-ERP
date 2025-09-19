'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { DashboardLayout } from '../../../../components/layout/dashboard-layout';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../../components/ui/card';
import { Button } from '../../../../components/ui/button';
import { Input } from '../../../../components/ui/input';
import { Label } from '../../../../components/ui/label';
import { Switch } from '../../../../components/ui/switch';
import { Textarea } from '../../../../components/ui/textarea';
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
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { adminApiService, EmailSettings } from '../../../../services/admin-api.service';
import { useToast } from '../../../../hooks/use-toast';
import { 
  Mail, 
  Send, 
  Server, 
  Shield, 
  CheckCircle, 
  XCircle,
  AlertTriangle,
  Settings
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

export default function EmailSettingsPage() {
  const [isTestDialogOpen, setIsTestDialogOpen] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
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

  if (isLoading) {
    return (
      <DashboardLayout>
        <div className="flex items-center justify-center h-64">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
        </div>
      </DashboardLayout>
    );
  }

  return (
    <DashboardLayout>
      <div className="space-y-6">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Email Settings</h1>
          <p className="text-muted-foreground">
            Configure SMTP settings for system email notifications
          </p>
        </div>

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
      </div>
    </DashboardLayout>
  );
}