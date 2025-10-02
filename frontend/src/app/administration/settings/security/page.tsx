'use client';

import { useState, useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Shield, Key, Clock, Bot, Save, Lock, Users, Loader2, FileText } from 'lucide-react';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '../../../../components/ui/select';

import { Button } from '../../../../components/ui/button';
import type { SecuritySettings as SecuritySettingsDto } from '../../../../services/settings';
import { Input } from '../../../../components/ui/input';
import { Label } from '../../../../components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../../components/ui/tabs';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../../components/ui/card';
import { Switch } from '../../../../components/ui/switch';
import { useToast } from '../../../../hooks/use-toast';
import { settingsService } from '../../../../services/settings';
import { DashboardLayout } from '../../../../components/layout/dashboard-layout';

const passwordPolicySchema = z.object({
  passwordMinLength: z.number().min(8, 'Password length must be at least 8 characters').max(128, 'Password length cannot exceed 128 characters'),
  passwordRequireUppercase: z.boolean(),
  passwordRequireLowercase: z.boolean(),
  passwordRequireDigits: z.boolean(),
  passwordRequireSpecialChars: z.boolean(),
  passwordMaxAge: z.number().min(0, 'Password expiry days must be 0 or greater').max(365, 'Password expiry cannot exceed 365 days').nullable(),
  passwordPreventReuse: z.number().min(0, 'Password history must be 0 or greater').max(24, 'Password history cannot exceed 24').nullable(),
});

const sessionSchema = z.object({
  sessionTimeoutMinutes: z.number().min(5, 'Session timeout must be at least 5 minutes').max(1440),
  jwtTokenLifetimeMinutes: z.number().min(5, 'Token lifetime must be at least 5 minutes').max(480),
  preventConcurrentLogin: z.enum(['Disabled', 'LogoutFromAllDevices', 'PreventSubsequentLogins']),
});

const lockoutSchema = z.object({
  maxFailedLoginAttempts: z.number().min(1).max(20),
  accountLockoutMinutes: z.number().min(1).max(1440),
  rateLimitLoginMaxAttempts: z.number().min(1).max(20),
  rateLimitLoginWindowMinutes: z.number().min(1).max(60),
  rateLimitLoginBlockDurationMinutes: z.number().min(1).max(1440),
});

const recaptchaSchema = z.object({
  captchaEnabled: z.boolean(),
  captchaProvider: z.enum(['recaptcha', 'hcaptcha']),
  recaptchaSiteKey: z.string().optional().nullable().or(z.literal('')),
  recaptchaSecretKey: z.string().optional().nullable().or(z.literal('')),
  hCaptchaSiteKey: z.string().optional().nullable().or(z.literal('')),
  hCaptchaSecretKey: z.string().optional().nullable().or(z.literal('')),
});

const legalSchema = z.object({
  termsOfServiceUrl: z.string().url('Must be a valid URL').optional().nullable().or(z.literal('')),
  privacyPolicyUrl: z.string().url('Must be a valid URL').optional().nullable().or(z.literal('')),
});

// Use the imported DTO type directly, ensuring schema matches its shape

export default function SecuritySettingsPage() {
  const [activeTab, setActiveTab] = useState('password');
  const { toast } = useToast();
  const queryClient = useQueryClient();

  // Fetch current settings
  const { data: settings, isLoading, error } = useQuery({
    queryKey: ['securitySettings'],
    queryFn: () => {
      console.log('🔄 useQuery: Calling settingsService.getSecuritySettings()');
      return settingsService.getSecuritySettings();
    },
  });
  
  console.log('🔍 Query state:', { 
    hasSettings: !!settings, 
    isLoading, 
    hasError: !!error, 
    errorMessage: error?.message,
    settingsData: settings 
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
    watch,
    setValue,
    reset,
  } = useForm<SecuritySettingsDto>({
    resolver: zodResolver(
      z.object({
        ...passwordPolicySchema.shape,
        ...sessionSchema.shape,
        ...lockoutSchema.shape,
        ...recaptchaSchema.shape,
        ...legalSchema.shape,
      })
    ),
    defaultValues: {
      // Password Policy
      passwordMinLength: 8,
      passwordRequireUppercase: true,
      passwordRequireLowercase: true,
      passwordRequireDigits: true,
      passwordRequireSpecialChars: true,
      passwordMaxAge: 90,
      passwordPreventReuse: 5,

      // Session Settings
      sessionTimeoutMinutes: 30,
      jwtTokenLifetimeMinutes: 60,
      preventConcurrentLogin: 'Disabled' as const,

      // Lockout Settings
      maxFailedLoginAttempts: 5,
      accountLockoutMinutes: 30,
      rateLimitLoginMaxAttempts: 5,
      rateLimitLoginWindowMinutes: 15,
      rateLimitLoginBlockDurationMinutes: 30,

      // CAPTCHA Settings
      captchaEnabled: false,
      captchaProvider: 'recaptcha' as const,
      recaptchaSiteKey: null,
      recaptchaSecretKey: null,
      hCaptchaSiteKey: null,
      hCaptchaSecretKey: null,

      // Legal URLs
      termsOfServiceUrl: null,
      privacyPolicyUrl: null,
    },
  });

  // Update form when settings are loaded
  useEffect(() => {
    if (!settings) {
      console.log('❌ No settings data available yet');
      return;
    }

    console.log('\n🎯 Form reset triggered - processing settings data');
    console.log('📊 Raw settings from API:', settings);
    
    // Prepare clean data for form reset
    const cleanSettings = {
      // Password Policy
      passwordMinLength: settings.passwordMinLength || 8,
      passwordRequireUppercase: Boolean(settings.passwordRequireUppercase),
      passwordRequireLowercase: Boolean(settings.passwordRequireLowercase),
      passwordRequireDigits: Boolean(settings.passwordRequireDigits),
      passwordRequireSpecialChars: Boolean(settings.passwordRequireSpecialChars),
      passwordMaxAge: settings.passwordMaxAge,
      passwordPreventReuse: settings.passwordPreventReuse,
      
      // Session Settings
      sessionTimeoutMinutes: settings.sessionTimeoutMinutes || 30,
      jwtTokenLifetimeMinutes: settings.jwtTokenLifetimeMinutes || 60,
      preventConcurrentLogin: settings.preventConcurrentLogin || 'Disabled',
      
      // Lockout Settings
      maxFailedLoginAttempts: settings.maxFailedLoginAttempts || 5,
      accountLockoutMinutes: settings.accountLockoutMinutes || 30,
      rateLimitLoginMaxAttempts: settings.rateLimitLoginMaxAttempts || 5,
      rateLimitLoginWindowMinutes: settings.rateLimitLoginWindowMinutes || 15,
      rateLimitLoginBlockDurationMinutes: settings.rateLimitLoginBlockDurationMinutes || 30,
      
      // CAPTCHA Settings
      captchaEnabled: Boolean(settings.captchaEnabled),
      captchaProvider: settings.captchaProvider || 'recaptcha',
      recaptchaSiteKey: settings.recaptchaSiteKey || '',
      recaptchaSecretKey: settings.recaptchaSecretKey || '',
      hCaptchaSiteKey: settings.hCaptchaSiteKey || '',
      hCaptchaSecretKey: settings.hCaptchaSecretKey || '',
      
      // Legal URLs
      termsOfServiceUrl: settings.termsOfServiceUrl || '',
      privacyPolicyUrl: settings.privacyPolicyUrl || '',
    } as SecuritySettingsDto;
    
    console.log('🧹 Clean settings for form:', cleanSettings);
    console.log('🔄 Calling form reset...');
    
    // Reset form with clean data
    reset(cleanSettings);
    
    // Verify form was updated
    setTimeout(() => {
      const currentFormValues = watch();
      console.log('\n✅ Form reset complete - current values:');
      console.log('🔐 Password fields:', {
        passwordMinLength: currentFormValues.passwordMinLength,
        passwordRequireUppercase: currentFormValues.passwordRequireUppercase,
        passwordMaxAge: currentFormValues.passwordMaxAge,
      });
      console.log('⏱️ Session fields:', {
        sessionTimeoutMinutes: currentFormValues.sessionTimeoutMinutes,
        preventConcurrentLogin: currentFormValues.preventConcurrentLogin,
      });
      console.log('🚫 Lockout fields:', {
        maxFailedLoginAttempts: currentFormValues.maxFailedLoginAttempts,
        accountLockoutMinutes: currentFormValues.accountLockoutMinutes,
      });
      console.log('🤖 CAPTCHA fields:', {
        captchaEnabled: currentFormValues.captchaEnabled,
        captchaProvider: currentFormValues.captchaProvider,
        recaptchaSiteKey: currentFormValues.recaptchaSiteKey,
      });
      console.log('📋 Legal fields:', {
        termsOfServiceUrl: currentFormValues.termsOfServiceUrl,
        privacyPolicyUrl: currentFormValues.privacyPolicyUrl,
      });
    }, 200);
  }, [settings, reset, watch]);
  
  // Debug: Log current form values to see what's actually in the form
  const currentValues = watch();
  
  // Log current form values every few seconds to see if they're updating
  useEffect(() => {
    const interval = setInterval(() => {
      console.log('\n🔍 CURRENT FORM VALUES SNAPSHOT:');
      console.log('📊 All form data:', currentValues);
      console.log('🔐 Password tab values:', {
        passwordMinLength: currentValues.passwordMinLength,
        passwordRequireUppercase: currentValues.passwordRequireUppercase,
        passwordMaxAge: currentValues.passwordMaxAge,
      });
      console.log('🚫 Lockout tab values:', {
        maxFailedLoginAttempts: currentValues.maxFailedLoginAttempts,
        accountLockoutMinutes: currentValues.accountLockoutMinutes,
      });
      console.log('🤖 reCAPTCHA tab values:', {
        captchaEnabled: currentValues.captchaEnabled,
        captchaProvider: currentValues.captchaProvider,
        recaptchaSiteKey: currentValues.recaptchaSiteKey,
      });
      console.log('📋 Legal tab values:', {
        termsOfServiceUrl: currentValues.termsOfServiceUrl,
        privacyPolicyUrl: currentValues.privacyPolicyUrl,
      });
    }, 5000); // Log every 5 seconds
    
    return () => clearInterval(interval);
  }, [currentValues]);

  const updateMutation = useMutation({
    mutationFn: (data: SecuritySettingsDto) => {
      console.log('🚀 Submitting security settings:', data);
      return settingsService.updateSecuritySettings(data);
    },
    onSuccess: (updatedData) => {
      console.log('✅ Settings saved successfully:', updatedData);
      
      // Invalidate and refetch the security settings query
      queryClient.invalidateQueries({ queryKey: ['securitySettings'] });
      
      toast({
        title: 'Settings Updated',
        description: 'Security settings have been updated successfully.',
        variant: 'default',
      });
    },
    onError: (error: any) => {
      console.error('❌ Failed to save settings:', error);
      toast({
        title: 'Error',
        description: error.message || 'Failed to update security settings.',
        variant: 'destructive',
      });
    },
  });

  const onSubmit = (data: SecuritySettingsDto) => {
    console.log('📤 Form submission - raw data:', data);
    
    // Clean up form data before sending to API
    const cleanData: SecuritySettingsDto = {
      ...data,
      // Convert empty strings to null for optional fields
      recaptchaSiteKey: data.recaptchaSiteKey?.trim() || null,
      recaptchaSecretKey: data.recaptchaSecretKey?.trim() || null,
      hCaptchaSiteKey: data.hCaptchaSiteKey?.trim() || null,
      hCaptchaSecretKey: data.hCaptchaSecretKey?.trim() || null,
      termsOfServiceUrl: data.termsOfServiceUrl?.trim() || null,
      privacyPolicyUrl: data.privacyPolicyUrl?.trim() || null,
    };
    
    console.log('🧹 Form submission - cleaned data:', cleanData);
    updateMutation.mutate(cleanData);
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <Loader2 className="h-8 w-8 animate-spin" />
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Security Settings</h1>
          <p className="text-sm text-slate-600">
            Configure security policies and authentication settings
          </p>
        </div>
        <Button type="submit" disabled={updateMutation.isPending}>
          {updateMutation.isPending ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Saving...
            </>
          ) : (
            <>
              <Save className="mr-2 h-4 w-4" />
              Save Changes
            </>
          )}
        </Button>
      </div>

      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <TabsList className="grid grid-cols-5 gap-4">
          <TabsTrigger value="password" className="flex items-center gap-2">
            <Key className="h-4 w-4" />
            Password Policy
          </TabsTrigger>
          <TabsTrigger value="session" className="flex items-center gap-2">
            <Clock className="h-4 w-4" />
            Session
          </TabsTrigger>
          <TabsTrigger value="lockout" className="flex items-center gap-2">
            <Shield className="h-4 w-4" />
            Lockout
          </TabsTrigger>
          <TabsTrigger value="recaptcha" className="flex items-center gap-2">
            <Bot className="h-4 w-4" />
            reCAPTCHA
          </TabsTrigger>
          <TabsTrigger value="legal" className="flex items-center gap-2">
            <FileText className="h-4 w-4" />
            Legal
          </TabsTrigger>
        </TabsList>

        <TabsContent value="password">
          <Card>
            <CardHeader>
              <CardTitle>Password Policy</CardTitle>
              <CardDescription>
                Configure password requirements and expiration settings
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="passwordMinLength">Minimum Password Length</Label>
                <Input
                  id="passwordMinLength"
                  type="number"
                  {...register('passwordMinLength', { valueAsNumber: true })}
                />
                {errors.passwordMinLength && (
                  <p className="text-sm text-red-500">{errors.passwordMinLength.message}</p>
                )}
              </div>

              <div className="space-y-4">
                <div className="flex items-center gap-2">
                  <Switch 
                    id="passwordRequireUppercase"
                    checked={watch('passwordRequireUppercase')}
                    onCheckedChange={(checked) => setValue('passwordRequireUppercase', checked)}
                  />
                  <Label htmlFor="passwordRequireUppercase">Require Uppercase Letters</Label>
                </div>

                <div className="flex items-center gap-2">
                  <Switch 
                    id="passwordRequireLowercase"
                    checked={watch('passwordRequireLowercase')}
                    onCheckedChange={(checked) => setValue('passwordRequireLowercase', checked)}
                  />
                  <Label htmlFor="passwordRequireLowercase">Require Lowercase Letters</Label>
                </div>

                <div className="flex items-center gap-2">
                  <Switch 
                    id="passwordRequireDigits"
                    checked={watch('passwordRequireDigits')}
                    onCheckedChange={(checked) => setValue('passwordRequireDigits', checked)}
                  />
                  <Label htmlFor="passwordRequireDigits">Require Numbers</Label>
                </div>

                <div className="flex items-center gap-2">
                  <Switch 
                    id="passwordRequireSpecialChars"
                    checked={watch('passwordRequireSpecialChars')}
                    onCheckedChange={(checked) => setValue('passwordRequireSpecialChars', checked)}
                  />
                  <Label htmlFor="passwordRequireSpecialChars">Require Special Characters</Label>
                </div>
              </div>

              <div className="space-y-2">
                <Label htmlFor="passwordPreventReuse">Password History</Label>
                <Input
                  id="passwordPreventReuse"
                  type="number"
                  {...register('passwordPreventReuse', { valueAsNumber: true })}
                />
                <p className="text-sm text-slate-500">
                  Number of previous passwords that cannot be reused (0 to disable)
                </p>
                {errors.passwordPreventReuse && (
                  <p className="text-sm text-red-500">{errors.passwordPreventReuse.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="passwordMaxAge">Password Expiry (Days)</Label>
                <Input
                  id="passwordMaxAge"
                  type="number"
                  {...register('passwordMaxAge', { valueAsNumber: true })}
                />
                <p className="text-sm text-slate-500">
                  Days before password expires (0 to disable)
                </p>
                {errors.passwordMaxAge && (
                  <p className="text-sm text-red-500">{errors.passwordMaxAge.message}</p>
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="session">
          <Card>
            <CardHeader>
              <CardTitle>Session Settings</CardTitle>
              <CardDescription>
                Configure session timeout and concurrent login settings
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="sessionTimeoutMinutes">Session Timeout (Minutes)</Label>
                <Input
                  id="sessionTimeoutMinutes"
                  type="number"
                  {...register('sessionTimeoutMinutes', { valueAsNumber: true })}
                />
                <p className="text-sm text-slate-500">
                  Duration of user inactivity before automatic logout
                </p>
                {errors.sessionTimeoutMinutes && (
                  <p className="text-sm text-red-500">{errors.sessionTimeoutMinutes.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="jwtTokenLifetimeMinutes">Token Lifetime (Minutes)</Label>
                <Input
                  id="jwtTokenLifetimeMinutes"
                  type="number"
                  {...register('jwtTokenLifetimeMinutes', { valueAsNumber: true })}
                />
                <p className="text-sm text-slate-500">
                  How long authentication tokens remain valid
                </p>
                {errors.jwtTokenLifetimeMinutes && (
                  <p className="text-sm text-red-500">{errors.jwtTokenLifetimeMinutes.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="preventConcurrentLogin">Concurrent Login Prevention</Label>
                <Select
                  onValueChange={(value) => setValue('preventConcurrentLogin', value as any)}
                  value={watch('preventConcurrentLogin')}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select a policy" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Disabled">
                      <div className="space-y-1">
                        <div className="font-medium">Disabled</div>
                        <div className="text-xs text-slate-500">
                          Allow users to login from multiple devices
                        </div>
                      </div>
                    </SelectItem>
                    <SelectItem value="LogoutFromAllDevices">
                      <div className="space-y-1">
                        <div className="font-medium">Logout from all devices</div>
                        <div className="text-xs text-slate-500">
                          New login terminates all existing sessions
                        </div>
                      </div>
                    </SelectItem>
                    <SelectItem value="PreventSubsequentLogins">
                      <div className="space-y-1">
                        <div className="font-medium">Prevent subsequent logins</div>
                        <div className="text-xs text-slate-500">
                          Block new logins until user logs out from current session
                        </div>
                      </div>
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="lockout">
          <Card>
            <CardHeader className="pb-4">
              <CardTitle>Account Lockout</CardTitle>
              <CardDescription>
                Configure account lockout settings for failed login attempts
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* Account Lockout Settings */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-1">
                  <Label htmlFor="maxFailedLoginAttempts">Maximum Failed Attempts</Label>
                  <Input
                    id="maxFailedLoginAttempts"
                    type="number"
                    {...register('maxFailedLoginAttempts', { valueAsNumber: true })}
                  />
                  <p className="text-xs text-slate-500">
                    Number of failed attempts before account is locked
                  </p>
                  {errors.maxFailedLoginAttempts && (
                    <p className="text-xs text-red-500">{errors.maxFailedLoginAttempts.message}</p>
                  )}
                </div>

                <div className="space-y-1">
                  <Label htmlFor="accountLockoutMinutes">Lockout Duration (Minutes)</Label>
                  <Input
                    id="accountLockoutMinutes"
                    type="number"
                    {...register('accountLockoutMinutes', { valueAsNumber: true })}
                  />
                  <p className="text-xs text-slate-500">
                    How long accounts remain locked after too many failed attempts
                  </p>
                  {errors.accountLockoutMinutes && (
                    <p className="text-xs text-red-500">{errors.accountLockoutMinutes.message}</p>
                  )}
                </div>
              </div>

              {/* Rate Limiting Settings */}
              <div className="pt-2 border-t">
                <h3 className="font-medium text-sm text-slate-700 dark:text-slate-300 mb-3">
                  Rate Limiting Settings
                </h3>
                
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="space-y-1">
                    <Label htmlFor="rateLimitLoginMaxAttempts">
                      Max Attempts
                    </Label>
                    <Input
                      id="rateLimitLoginMaxAttempts"
                      type="number"
                      {...register('rateLimitLoginMaxAttempts', { valueAsNumber: true })}
                    />
                    <p className="text-xs text-slate-500">
                      Max attempts within time window
                    </p>
                    {errors.rateLimitLoginMaxAttempts && (
                      <p className="text-xs text-red-500">{errors.rateLimitLoginMaxAttempts.message}</p>
                    )}
                  </div>

                  <div className="space-y-1">
                    <Label htmlFor="rateLimitLoginWindowMinutes">
                      Time Window (Min)
                    </Label>
                    <Input
                      id="rateLimitLoginWindowMinutes"
                      type="number"
                      {...register('rateLimitLoginWindowMinutes', { valueAsNumber: true })}
                    />
                    <p className="text-xs text-slate-500">
                      Time period for tracking attempts
                    </p>
                    {errors.rateLimitLoginWindowMinutes && (
                      <p className="text-xs text-red-500">{errors.rateLimitLoginWindowMinutes.message}</p>
                    )}
                  </div>

                  <div className="space-y-1">
                    <Label htmlFor="rateLimitLoginBlockDurationMinutes">
                      Block Duration (Min)
                    </Label>
                    <Input
                      id="rateLimitLoginBlockDurationMinutes"
                      type="number"
                      {...register('rateLimitLoginBlockDurationMinutes', { valueAsNumber: true })}
                    />
                    <p className="text-xs text-slate-500">
                      How long to block after rate limit
                    </p>
                    {errors.rateLimitLoginBlockDurationMinutes && (
                      <p className="text-xs text-red-500">{errors.rateLimitLoginBlockDurationMinutes.message}</p>
                    )}
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="recaptcha">
          <Card>
            <CardHeader>
              <CardTitle>reCAPTCHA Settings</CardTitle>
              <CardDescription>
                Configure Google reCAPTCHA for enhanced security
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="flex items-center justify-between p-4 border rounded-lg">
                <div className="space-y-1">
                  <Label className="text-base font-medium">Enable CAPTCHA Protection</Label>
                  <p className="text-sm text-muted-foreground">
                    Add CAPTCHA verification to login forms for enhanced security
                  </p>
                </div>
                <Switch 
                  checked={watch('captchaEnabled')}
                  onCheckedChange={(checked) => setValue('captchaEnabled', checked)}
                />
              </div>

              {watch('captchaEnabled') && (
                <div className="space-y-4 p-4 bg-muted rounded-lg">
                  <div className="space-y-2">
                    <Label>CAPTCHA Provider</Label>
                    <Select
                      onValueChange={(value) => setValue('captchaProvider', value as 'recaptcha' | 'hcaptcha')}
                      value={watch('captchaProvider')}
                    >
                      <SelectTrigger className="w-full">
                        <SelectValue placeholder="Choose provider" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="recaptcha">Google reCAPTCHA v2</SelectItem>
                        <SelectItem value="hcaptcha">hCAPTCHA</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>

                  {watch('captchaProvider') === 'recaptcha' ? (
                    <div className="space-y-4">
                      <div className="text-sm text-muted-foreground p-3 bg-background rounded border-l-4 border-blue-500">
                        <strong>Setup:</strong> Get your keys from{' '}
                        <a href="https://www.google.com/recaptcha/admin" target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:underline">
                          Google reCAPTCHA Admin
                        </a>
                      </div>
                      
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <div className="space-y-2">
                          <Label htmlFor="recaptchaSiteKey">Site Key (Public)</Label>
                          <Input
                            id="recaptchaSiteKey"
                            placeholder="6Lc..."
                            {...register('recaptchaSiteKey')}
                          />
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="recaptchaSecretKey">Secret Key (Private)</Label>
                          <Input
                            id="recaptchaSecretKey"
                            type="password"
                            placeholder="6Lc..."
                            {...register('recaptchaSecretKey')}
                          />
                        </div>
                      </div>
                    </div>
                  ) : (
                    <div className="space-y-4">
                      <div className="text-sm text-muted-foreground p-3 bg-background rounded border-l-4 border-purple-500">
                        <strong>Setup:</strong> Get your keys from{' '}
                        <a href="https://www.hcaptcha.com/" target="_blank" rel="noopener noreferrer" className="text-purple-600 hover:underline">
                          hCAPTCHA Dashboard
                        </a>
                      </div>
                      
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <div className="space-y-2">
                          <Label htmlFor="hCaptchaSiteKey">Site Key (Public)</Label>
                          <Input
                            id="hCaptchaSiteKey"
                            placeholder="10000000-ffff-ffff..."
                            {...register('hCaptchaSiteKey')}
                          />
                        </div>
                        <div className="space-y-2">
                          <Label htmlFor="hCaptchaSecretKey">Secret Key (Private)</Label>
                          <Input
                            id="hCaptchaSecretKey"
                            type="password"
                            placeholder="0x0000000000000..."
                            {...register('hCaptchaSecretKey')}
                          />
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="legal">
          <Card>
            <CardHeader>
              <CardTitle>Legal URLs</CardTitle>
              <CardDescription>
                Configure URLs for your Terms of Service and Privacy Policy pages
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="termsOfServiceUrl">Terms of Service URL</Label>
                <Input
                  id="termsOfServiceUrl"
                  type="url"
                  placeholder="https://yourcompany.com/terms"
                  {...register('termsOfServiceUrl')}
                />
                <p className="text-sm text-slate-500">
                  URL to your Terms of Service page that will be displayed on the registration form
                </p>
                {errors.termsOfServiceUrl && (
                  <p className="text-sm text-red-500">{errors.termsOfServiceUrl.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="privacyPolicyUrl">Privacy Policy URL</Label>
                <Input
                  id="privacyPolicyUrl"
                  type="url"
                  placeholder="https://yourcompany.com/privacy"
                  {...register('privacyPolicyUrl')}
                />
                <p className="text-sm text-slate-500">
                  URL to your Privacy Policy page that will be displayed on the registration form
                </p>
                {errors.privacyPolicyUrl && (
                  <p className="text-sm text-red-500">{errors.privacyPolicyUrl.message}</p>
                )}
              </div>

              <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                <div className="flex items-start gap-3">
                  <FileText className="h-5 w-5 text-blue-600 mt-0.5" />
                  <div>
                    <h4 className="font-medium text-blue-900">Legal Compliance</h4>
                    <p className="text-sm text-blue-800 mt-1">
                      These URLs will be displayed to users during registration. Ensure your
                      Terms of Service and Privacy Policy are up to date and accessible at
                      these URLs to maintain compliance with data protection regulations.
                    </p>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
      </form>
  );
}
