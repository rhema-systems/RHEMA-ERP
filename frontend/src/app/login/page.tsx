'use client';

import { useState, useEffect, Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Building2, Eye, EyeOff, Loader2, Shield, Users, UserPlus, Check } from 'lucide-react';
import ReCAPTCHA from 'react-google-recaptcha';

import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { authService } from '../../services/auth';
import { settingsService } from '../../services/settings';
import { apiService } from '../../services/api.service';
import type { LoginRequest } from '../../types';

const makeLoginSchema = (requireRecaptcha: boolean) => z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  recaptchaToken: requireRecaptcha
    ? z.string().min(1, 'Please complete the reCAPTCHA verification')
    : z.string().optional(),
  rememberMe: z.boolean().optional(),
});

type LoginForm = z.infer<ReturnType<typeof makeLoginSchema>>;

// Component that uses useSearchParams - must be wrapped in Suspense
function LoginFormWithSearchParams() {
  const [showPassword, setShowPassword] = useState(false);
  const [failedAttempts, setFailedAttempts] = useState(0);
  const [successMessage, setSuccessMessage] = useState('');
  const [showTwoFactor, setShowTwoFactor] = useState(false);
  const [twoFactorToken, setTwoFactorToken] = useState('');
  const [twoFactorCode, setTwoFactorCode] = useState('');
  const [storedLoginData, setStoredLoginData] = useState<{ username: string; password: string; rememberMe: boolean } | null>(null);
  const router = useRouter();
  const searchParams = useSearchParams();

  // Check for success message from URL parameters
  useEffect(() => {
    const message = searchParams.get('message');
    if (message) {
      setSuccessMessage(decodeURIComponent(message));
      // Message will stay until user submits the form - no auto-clear
    }
  }, [searchParams]);
  

  // Fetch public security settings to determine if reCAPTCHA should be shown
  const { data: securitySettings } = useQuery({
    queryKey: ['publicSecuritySettings'],
    queryFn: () => settingsService.getPublicSecuritySettings(),
  });

  // Fetch tenants to check if any allow self-registration
  const { data: tenants, isLoading: tenantsLoading, error: tenantsError } = useQuery({
    queryKey: ['tenants'],
    queryFn: () => apiService.getTenants(),
    retry: 3,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
    staleTime: 10 * 60 * 1000, // 10 minutes
    refetchOnWindowFocus: false, // Disable refetch on window focus to prevent the error
    refetchOnMount: false, // Only fetch once
    refetchOnReconnect: false,
  });

  // Debug: Log tenant data
  console.log('🏢 Tenants query state:', { 
    tenants, 
    tenantsLoading, 
    tenantsError: tenantsError?.message,
    hasData: !!tenants,
    tenantCount: tenants?.length || 0
  });
  
  if (tenants) {
    console.log('🔍 Tenant self-registration status:', 
      tenants.map(t => ({ 
        name: t.name, 
        code: t.code, 
        allowSelfRegistration: t.allowSelfRegistration, 
        isActive: t.isActive 
      }))
    );
  }

  // Check if any tenant allows self-registration
  const allowSelfRegistration = tenants?.some(tenant => tenant.allowSelfRegistration && tenant.isActive) ?? false;
  
  console.log('✨ Create Account button will be shown:', allowSelfRegistration);
  console.log('📋 Button visibility logic:', {
    hasTenants: !!tenants,
    tenantCount: tenants?.length || 0,
    tenantsWithSelfReg: tenants?.filter(t => t.allowSelfRegistration).length || 0,
    activeTenants: tenants?.filter(t => t.isActive).length || 0,
    finalDecision: allowSelfRegistration
  });

  // Determine if reCAPTCHA should be shown based on settings and failed attempts
  const shouldShowRecaptcha = () => {
    if (!securitySettings) return false;
    
    // Only show CAPTCHA after failed attempts or suspicious activity, not always
    // Show after 2 or more failed attempts if configured (and we have a valid site key)
    if (securitySettings.captchaEnabled &&
        failedAttempts >= 2 &&
        (securitySettings.recaptchaSiteKey || securitySettings.hCaptchaSiteKey)) {
      return true;
    }
    
    // Show after X failed attempts based on maxFailedLoginAttempts setting
    if (securitySettings.maxFailedLoginAttempts && 
        failedAttempts >= Math.max(1, Math.floor(securitySettings.maxFailedLoginAttempts / 2)) &&
        (securitySettings.recaptchaSiteKey || securitySettings.hCaptchaSiteKey)) {
      return true;
    }
    
    return false;
  };

  const showRecaptcha = shouldShowRecaptcha();

  const {
    register,
    handleSubmit,
    formState: { errors },
    setError,
    setValue,
    watch,
  } = useForm<LoginForm>({
    resolver: zodResolver(makeLoginSchema(showRecaptcha)),
    defaultValues: {
      username: '',
      password: '',
      recaptchaToken: '',
      rememberMe: false,
    },
  });

  const loginMutation = useMutation({
    mutationFn: (data: LoginRequest) => authService.login(data),
    onSuccess: (response) => {
      // Check if 2FA is required
      if (response.requiresTwoFactor) {
        setTwoFactorToken(response.twoFactorToken || '');
        setShowTwoFactor(true);
        // Don't clear stored login data since we need it for 2FA step
        return;
      }
      
      // Clear stored login data after successful complete login
      setStoredLoginData(null);
      
      // After successful login, redirect to tenant selection
      if (response.token) {
        router.push('/tenant-select');
      }
    },
    onError: (error: any) => {
      console.error('Login error details:', {
        message: error.message,
        status: error.status,
        statusText: error.statusText,
        response: error.response
      });
      
      const message = error.message || 'Login failed. Please try again.';
      setError('root', { message });
      setFailedAttempts(prev => prev + 1);
      
      // If this was a 2FA error, clear the code for retry
      if (showTwoFactor) {
        setTwoFactorCode('');
        // Don't clear stored login data yet - user might try again
      } else {
        // Clear stored login data on first-step errors
        setStoredLoginData(null);
      }
    },
  });

  const onSubmit = (data: LoginForm) => {
    // Clear success message when user attempts to login
    setSuccessMessage('');

    // If this is the first step (no 2FA yet), persist credentials for the next step
    if (!showTwoFactor) {
      setStoredLoginData({
        username: data.username,
        password: data.password,
        rememberMe: !!data.rememberMe,
      });
    }

    // Build payload — reuse stored credentials during 2FA step
    const effectiveUsername = showTwoFactor ? (storedLoginData?.username ?? data.username) : data.username;
    const effectivePassword = showTwoFactor ? (storedLoginData?.password ?? data.password) : data.password;
    const effectiveRemember = showTwoFactor ? (storedLoginData?.rememberMe ?? !!data.rememberMe) : !!data.rememberMe;

    // Clean the 2FA code once and use it consistently
    const cleaned2fa = showTwoFactor ? twoFactorCode.replace(/\D/g, '') : undefined;

    // During 2FA step, only proceed if we have exactly 6 digits
    if (showTwoFactor && cleaned2fa!.length !== 6) {
      console.warn('🚫 2FA submission blocked: code not 6 digits', cleaned2fa);
      return; // Don't submit if 2FA code is not exactly 6 digits
    }

    const loginData: LoginRequest = {
      username: effectiveUsername,
      password: effectivePassword,
      tenantCode: undefined, // will be set during tenant selection
      rememberMe: effectiveRemember,
      twoFactorCode: cleaned2fa,
    };

    console.log('🚀 Submitting login request:', {
      hasUsername: !!loginData.username,
      hasPassword: !!loginData.password,
      showTwoFactor,
      twoFactorCodeLength: cleaned2fa?.length ?? 0,
      twoFactorCodeValue: cleaned2fa,
      rawTwoFactorCode: twoFactorCode,
      twoFactorCode: showTwoFactor ? loginData.twoFactorCode : 'not required',
      fullPayload: loginData
    });

    loginMutation.mutate(loginData);
  };

  // Handle 2FA code input changes
  const handleTwoFactorCodeChange = (value: string) => {
    const cleanedValue = value.replace(/\D/g, '').slice(0, 6);
    setTwoFactorCode(cleanedValue);
  };
  
  // Handle keyboard events for 2FA input
  const handleTwoFactorKeyDown = (e: React.KeyboardEvent) => {
    // Allow manual submission with Enter key when code is complete
    if (e.key === 'Enter' && twoFactorCode.replace(/\D/g, '').length === 6 && !loginMutation.isPending) {
      console.log('⌨️ Manual submit via Enter key');
      handleSubmit(onSubmit)();
    }
  };

  return (
    <div className="min-h-screen flex items-start justify-center p-4 pt-16 relative overflow-hidden">
      {/* Background Image */}
      <div 
        className="absolute inset-0 bg-cover bg-center bg-no-repeat" 
        style={{backgroundImage: 'url(/login.svg)'}}
      ></div>
      
      <div className="relative w-full max-w-md space-y-8">
        {/* Logo and Header */}
        <div className="text-center">
          <div className="flex justify-center mb-6">
            <div className="relative">
              <div className="flex h-20 w-20 items-center justify-center rounded-2xl bg-gradient-to-r from-blue-600 to-indigo-600 shadow-lg ring-1 ring-white/10">
                <Building2 className="h-10 w-10 text-white" />
              </div>
              <div className="absolute -inset-1 bg-gradient-to-r from-blue-600 to-indigo-600 rounded-2xl blur opacity-25"></div>
            </div>
          </div>
          <h1 className="text-4xl font-bold tracking-tight text-white drop-shadow-lg">
            ERP System
          </h1>
          <p className="mt-2 text-white/90 drop-shadow">
            Enterprise Resource Planning Platform
          </p>
        </div>

        {/* Login Card */}
        <Card className="backdrop-blur-xl bg-white/95 dark:bg-slate-900/95 shadow-2xl border border-white/30 dark:border-slate-700/50 rounded-2xl">
          <CardHeader className="space-y-1 pb-6">
            <CardTitle className="text-2xl font-bold text-center">
              {showTwoFactor ? '2FA Verification' : 'Sign In'}
            </CardTitle>
            <CardDescription className="text-center">
              {showTwoFactor 
                ? 'Please enter your authentication code to complete login'
                : 'Enter your credentials to access your account'
              }
            </CardDescription>
            
            {/* Step Indicator */}
            {showTwoFactor && (
              <div className="flex items-center justify-center space-x-2 mt-4">
                <div className="flex items-center space-x-2">
                  <div className="w-6 h-6 rounded-full bg-green-500 flex items-center justify-center">
                    <Check className="w-3 h-3 text-white" />
                  </div>
                  <span className="text-xs text-green-600 font-medium">Credentials</span>
                </div>
                <div className="w-8 h-px bg-slate-300"></div>
                <div className="flex items-center space-x-2">
                  <div className="w-6 h-6 rounded-full bg-blue-500 flex items-center justify-center">
                    <Shield className="w-3 h-3 text-white" />
                  </div>
                  <span className="text-xs text-blue-600 font-medium">2FA Code</span>
                </div>
              </div>
            )}
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">

              {/* Username Field - Hidden during 2FA step */}
              {!showTwoFactor && (
                <div className="space-y-2">
                <Label htmlFor="username" className="text-xs font-semibold text-slate-600 uppercase tracking-wider">
                  USER NAME OR EMAIL ADDRESS
                </Label>
                <div className="relative">
                  <Input
                    id="username"
                    type="text"
                    placeholder="admin"
                    className={errors.username ? 'border-red-500' : ''}
                    {...register('username')}
                  />
                </div>
                {errors.username && (
                  <p className="text-sm text-red-500 flex items-center gap-1">
                    <Shield className="h-3 w-3" />
                    {errors.username.message}
                  </p>
                )}
                </div>
              )}

              {/* Password Field - Hidden during 2FA step */}
              {!showTwoFactor && (
                <div className="space-y-2">
                <Label htmlFor="password" className="text-xs font-semibold text-slate-600 uppercase tracking-wider">
                  PASSWORD
                </Label>
                <div className="relative">
                  <Input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    placeholder="Admin123!"
                    className={`pr-12 ${errors.password ? 'border-red-500' : ''}`}
                    {...register('password')}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="absolute right-2 top-1/2 -translate-y-1/2 h-8 w-8 p-0 hover:bg-transparent"
                    onClick={() => setShowPassword(!showPassword)}
                  >
                    {showPassword ? (
                      <EyeOff className="h-4 w-4 text-slate-500" />
                    ) : (
                      <Eye className="h-4 w-4 text-slate-500" />
                    )}
                  </Button>
                </div>
                {errors.password && (
                  <p className="text-sm text-red-500 flex items-center gap-1">
                    <Shield className="h-3 w-3" />
                    {errors.password.message}
                  </p>
                )}
                </div>
              )}

              {/* Two-Factor Authentication Field - Only shown when required */}
              {showTwoFactor && (
                <div className="space-y-2">
                  <Label htmlFor="twoFactorCode" className="text-xs font-semibold text-slate-600 uppercase tracking-wider">
                    AUTHENTICATION CODE
                  </Label>
                  <div className="relative">
                    <Input
                      id="twoFactorCode"
                      type="text"
                      value={twoFactorCode}
                      onChange={(e) => handleTwoFactorCodeChange(e.target.value)}
                      onKeyDown={handleTwoFactorKeyDown}
                      placeholder="Enter 6-digit code"
                      className="text-center text-lg font-mono tracking-widest"
                      maxLength={6}
                      autoComplete="one-time-code"
                      inputMode="numeric"
                      autoFocus
                    />
                    <div className="absolute right-3 top-1/2 -translate-y-1/2">
                      <Shield className="h-4 w-4 text-blue-600" />
                    </div>
                  </div>
                  <p className="text-xs text-center">
                    {twoFactorCode.replace(/\D/g, '').length === 6 ? (
                      <span className="text-green-600 font-medium">
                        ✓ Code complete - click "Verify Code" or press Enter to submit
                      </span>
                    ) : (
                      <span className="text-slate-500">
                        Enter the 6-digit code from your authenticator app
                      </span>
                    )}
                  </p>
                  <div className="flex justify-center mt-2">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setShowTwoFactor(false);
                        setTwoFactorCode('');
                        setTwoFactorToken('');
                        setStoredLoginData(null); // Clear stored credentials when going back
                      }}
                      className="text-slate-500 hover:text-slate-700"
                    >
                      ← Back to login
                    </Button>
                  </div>
                </div>
              )}

              {/* Remember Me & Forgot Password - Hidden during 2FA step */}
              {!showTwoFactor && (
                <div className="flex items-center justify-between">
                <div className="flex items-center space-x-2">
                  <input
                    id="rememberMe"
                    type="checkbox"
                    className="h-4 w-4 rounded border-slate-300 text-blue-600 focus:ring-blue-500"
                    {...register('rememberMe')}
                  />
                  <Label htmlFor="rememberMe" className="text-sm font-normal">
                    Remember me for 30 days
                  </Label>
                </div>
                <a
                  href="/forgot-password"
                  className="text-sm font-medium text-blue-600 hover:text-blue-500 dark:text-blue-400 dark:hover:text-blue-300 transition-colors"
                >
                  Forgot password?
                </a>
                </div>
              )}

              {/* Success Message */}
              {successMessage && (
                <div className="rounded-lg bg-green-50 dark:bg-green-900/20 p-4 border border-green-200 dark:border-green-800">
                  <p className="text-sm text-green-700 dark:text-green-400 flex items-center gap-2">
                    <Shield className="h-4 w-4" />
                    {successMessage}
                  </p>
                </div>
              )}

              {/* Error Message */}
              {errors.root && (
                <div className="rounded-lg bg-red-50 dark:bg-red-900/20 p-4 border border-red-200 dark:border-red-800">
                  <p className="text-sm text-red-700 dark:text-red-400 flex items-center gap-2">
                    <Shield className="h-4 w-4" />
                    {errors.root.message}
                  </p>
                </div>
              )}

              {/* ReCAPTCHA - Only shown when required */}
              {showRecaptcha && securitySettings && (
                <div className="space-y-4">
                  <div className="flex justify-center">
                    <ReCAPTCHA
                      sitekey={securitySettings.captchaProvider === 'recaptcha' 
                        ? securitySettings.recaptchaSiteKey || '' 
                        : securitySettings.hCaptchaSiteKey || ''}
                      onChange={(token) => setValue('recaptchaToken', token || '')}
                    />
                  </div>
                  {errors.recaptchaToken && (
                    <p className="text-sm text-red-500 text-center">{errors.recaptchaToken.message}</p>
                  )}
                  {failedAttempts > 0 && (
                    <p className="text-sm text-amber-600 dark:text-amber-400 text-center">
                      Additional verification required due to multiple failed login attempts
                    </p>
                  )}
                  <p className="text-xs text-center text-slate-500 dark:text-slate-400">
                    Using {securitySettings.captchaProvider === 'recaptcha' ? 'Google reCAPTCHA' : 'hCAPTCHA'}
                  </p>
                </div>
              )}

              {/* Submit Button */}
              <Button
                type="submit"
                className="w-full h-12 text-base font-semibold"
                disabled={loginMutation.isPending || (showTwoFactor && twoFactorCode.replace(/\D/g, '').length !== 6)}
              >
                {loginMutation.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    {showTwoFactor ? 'Verifying...' : 'Signing in...'}
                  </>
                ) : (
                  showTwoFactor ? 'Verify Code' : 'Sign In'
                )}
              </Button>
            </form>

            {/* Create Account Link - Only shown if any tenant allows self-registration */}
            {allowSelfRegistration && (
              <div className="mt-6 text-center">
                <div className="relative">
                  <div className="absolute inset-0 flex items-center">
                    <span className="w-full border-t border-slate-200 dark:border-slate-700" />
                  </div>
                  <div className="relative flex justify-center text-sm">
                    <span className="bg-white dark:bg-slate-900 px-2 text-slate-500 dark:text-slate-400">or</span>
                  </div>
                </div>
                <div className="mt-6">
                  <Link href="/register">
                    <Button
                      type="button"
                      variant="outline"
                      className="w-full h-12 text-base font-semibold border-slate-300 dark:border-slate-600 hover:bg-slate-50 dark:hover:bg-slate-800"
                    >
                      <UserPlus className="mr-2 h-4 w-4" />
                      Create Account
                    </Button>
                  </Link>
                  <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
                    New to the platform? Create an external user account
                  </p>
                </div>
              </div>
            )}

          </CardContent>
        </Card>

        {/* Footer */}
        <div className="text-center text-sm text-white/70 drop-shadow">
          <p>© 2025 ERP System. All rights reserved.</p>
          <p className="mt-1">Secure enterprise management platform</p>
        </div>
      </div>
    </div>
  );
}

// Loading component for Suspense fallback
function LoginPageLoading() {
  return (
    <div className="min-h-screen flex items-start justify-center p-4 pt-16 relative overflow-hidden">
      {/* Background Image */}
      <div 
        className="absolute inset-0 bg-cover bg-center bg-no-repeat" 
        style={{backgroundImage: 'url(/login.svg)'}}
      ></div>
      
      <div className="relative flex items-center space-x-3 backdrop-blur-sm bg-white/95 dark:bg-slate-900/95 p-8 rounded-2xl shadow-2xl border border-white/30">
        <Loader2 className="h-8 w-8 animate-spin text-blue-600" />
        <span className="text-slate-900 dark:text-slate-100 font-medium text-lg">Loading login page...</span>
      </div>
    </div>
  );
}

// Main page component that wraps LoginFormWithSearchParams in Suspense
export default function LoginPage() {
  return (
    <Suspense fallback={<LoginPageLoading />}>
      <LoginFormWithSearchParams />
    </Suspense>
  );
}
