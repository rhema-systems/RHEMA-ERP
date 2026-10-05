 
'use client';

import React, { Suspense, useCallback, useEffect, useRef, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Eye, EyeOff, Loader2, Shield, Check, Mail, LockKeyhole } from 'lucide-react';
import ReCAPTCHA from 'react-google-recaptcha';

import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { authService } from '../../services/auth';
import { settingsService } from '../../services/settings';
import { apiService } from '../../services/api.service';
import { tenantService } from '../../services/tenant';
import { browserSessionCoordinator } from '../../services/browser-session-coordinator';
import { LoginPresentation } from '../../components/auth/LoginPresentation';
import {
  DEFAULT_LOGIN_PAGE_STYLE,
  loginAppearanceService,
  type LoginPageStyle,
} from '../../services/login-appearance';
import type { LoginRequest, LoginResponse, OtpChannel } from '../../types';
import {
  buildTenantSelectRedirectUrl,
  getRedirectTargetFromSearchParams,
  resolveRedirectTarget,
} from '../../lib/auth-redirect';
import {
  getExternalPortalPath,
  isCandidateUser,
  isConsultantClientUser,
  isExternalPortalUser,
} from '../../lib/auth-routing';

const makeLoginSchema = (requireRecaptcha: boolean) => z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  recaptchaToken: requireRecaptcha
    ? z.string().min(1, 'Please complete the reCAPTCHA verification')
    : z.string().optional(),
  rememberMe: z.boolean().optional(),
});

type LoginForm = z.infer<ReturnType<typeof makeLoginSchema>>;

// One authentication implementation is composed into either public presentation shell.
// This component uses useSearchParams and must remain inside Suspense.
function SharedAuthenticationForm() {
  const [showPassword, setShowPassword] = useState(false);
  const [failedAttempts, setFailedAttempts] = useState(0);
  const [successMessage, setSuccessMessage] = useState('');
  const [showTwoFactor, setShowTwoFactor] = useState(false);
  const [twoFactorToken, setTwoFactorToken] = useState('');
  const [twoFactorCode, setTwoFactorCode] = useState('');
  const [storedLoginData, setStoredLoginData] = useState<{ username: string; password: string; rememberMe: boolean } | null>(null);
  const [authMode] = useState<'password' | 'otp'>('password');
  const [otpStage, setOtpStage] = useState<'request' | 'verify'>('request');
  const [otpChannel, setOtpChannel] = useState<OtpChannel>('Email');
  const [otpIdentifier, setOtpIdentifier] = useState('');
  const [otpCode, setOtpCode] = useState('');
  const [otpRecaptchaToken, setOtpRecaptchaToken] = useState('');
  const [otpInfoMessage, setOtpInfoMessage] = useState('');
  const [otpErrorMessage, setOtpErrorMessage] = useState('');
  const [otpRequiresTwoFactor, setOtpRequiresTwoFactor] = useState(false);
  const [otpTwoFactorCode, setOtpTwoFactorCode] = useState('');
  const [isRedirecting, setIsRedirecting] = useState(false);
  const redirectStartedRef = useRef(false);
  const lastAutoSubmittedTwoFactorCodeRef = useRef<string | null>(null);
  const lastAutoSubmittedOtpTwoFactorKeyRef = useRef<string | null>(null);
  const router = useRouter();
  const searchParams = useSearchParams();
  const redirectTarget = getRedirectTargetFromSearchParams(searchParams);

  const { data: loginAppearance, isPending: isLoginAppearancePending } = useQuery({
    queryKey: ['publicLoginAppearance'],
    queryFn: () => loginAppearanceService.getPublicLoginAppearance(),
    staleTime: 0,
    refetchOnMount: 'always',
    refetchOnWindowFocus: false,
  });
  const loginPageStyle: LoginPageStyle = loginAppearance?.loginPageStyle ?? DEFAULT_LOGIN_PAGE_STYLE;
  const loginBackgroundUrl = loginPageStyle === 'DarkPremium'
    ? loginAppearance?.darkBackgroundUrl
    : loginAppearance?.lightBackgroundUrl;
  const isDarkPremium = loginPageStyle === 'DarkPremium';
  const primaryActionClass = `h-10 w-full text-sm font-semibold ${isDarkPremium
    ? 'bg-blue-500 text-white hover:bg-blue-400 focus-visible:ring-sky-300'
    : 'bg-blue-600 text-white hover:bg-blue-700'}`;
  const fieldLabelClass = `text-xs font-semibold tracking-wide ${isDarkPremium ? 'text-slate-200' : 'text-slate-600'}`;
  const fieldControlClass = isDarkPremium
    ? 'border-white/15 bg-white/10 text-white placeholder:text-slate-400 focus-visible:ring-sky-400'
    : 'bg-white';
  const fieldIconClass = isDarkPremium ? 'text-slate-300' : 'text-slate-500';
  const mutedTextClass = isDarkPremium ? 'text-slate-300' : 'text-slate-500';

  // Check for success message from URL parameters
  useEffect(() => {
    const message = searchParams?.get('message');
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
  const { data: tenants } = useQuery({
    queryKey: ['tenants'],
    queryFn: () => apiService.getTenants(),
    retry: 3,
    retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
    staleTime: 10 * 60 * 1000, // 10 minutes
    refetchOnWindowFocus: false, // Disable refetch on window focus to prevent the error
    refetchOnMount: false, // Only fetch once
    refetchOnReconnect: false,
  });

  // Check if any tenant allows self-registration
  const allowSelfRegistration = tenants?.some(tenant => tenant.allowSelfRegistration && tenant.isActive) ?? false;

  // Determine if reCAPTCHA should be shown based on settings and failed attempts
  const shouldShowRecaptcha = () => {
    if (!securitySettings) return false;

    // If CAPTCHA is enabled for the tenant, always show it (backend enforces it when enabled)
    if (securitySettings.captchaEnabled &&
        (securitySettings.recaptchaSiteKey || securitySettings.hCaptchaSiteKey)) {
      return true;
    }
    
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

  const redirectAfterLogin = useCallback(async (response: LoginResponse) => {
    if (response.token && !response.requiresTwoFactor && !redirectStartedRef.current) {
      // The stored-session effect and either login mutation share one redirect.
      // Keep the guard after success while App Router finishes navigation.
      redirectStartedRef.current = true;
      setIsRedirecting(true);
      if (response.user?.mustChangePassword) {
        router.replace('/change-temporary-password');
        return;
      }

      const isCandidate = isCandidateUser(response.user);
      const isExternalUser =
        isExternalPortalUser(response.user) || isCandidate || isConsultantClientUser(response.user);

      // External portal users (business partners AND careers candidates) land in the portal,
      // while internal users continue to tenant selection.
      if (isExternalUser) {
        // Try to auto-select the best tenant (host-driven or single-tenant) to avoid an extra tenant-select step.
        try {
          const tenants = response.user?.accessibleTenants || [];

          let preferredTenantCode: string | null = null;

          if (tenants.length === 1) {
            preferredTenantCode = tenants[0].tenantCode;
          }

          if (!preferredTenantCode) {
            const publicSettings = await settingsService.getPublicSecuritySettings();
            const hostTenantCode = (publicSettings as any)?.tenantCode as string | null | undefined;
            const match = hostTenantCode ? tenants.find((tenant) => tenant.tenantCode === hostTenantCode) : null;
            preferredTenantCode = match?.tenantCode ?? null;
          }

          if (!preferredTenantCode) {
            const def = tenants.find((tenant) => tenant.isDefault) ?? null;
            preferredTenantCode = def?.tenantCode ?? null;
          }

          if (preferredTenantCode) {
            await tenantService.selectTenant(preferredTenantCode, false);

            router.replace(
              resolveRedirectTarget(
                redirectTarget,
                isCandidate
                  ? '/external-portal/careers'
                  : isConsultantClientUser(response.user)
                    ? '/external-portal/client-timesheets'
                    : getExternalPortalPath()
              )
            );
            return;
          }
        } catch {
          // ignore and fall back to tenant-select
        }

        // Fallback: tenant selection page auto-selects when possible.
        router.replace(buildTenantSelectRedirectUrl(redirectTarget));
      } else {
        // Internal users go to tenant selection
        router.replace(buildTenantSelectRedirectUrl(redirectTarget));
      }
    }
  }, [redirectTarget, router]);

  useEffect(() => {
    const storedToken = authService.getStoredToken();
    const storedUser = authService.getStoredUser();

    if (!storedToken || showTwoFactor) {
      return;
    }

    void redirectAfterLogin({
      token: storedToken,
      user: storedUser as LoginResponse['user'],
    });
  }, [redirectAfterLogin, showTwoFactor]);

  useEffect(() => browserSessionCoordinator.subscribe(event => {
    if (event.type !== 'login' || showTwoFactor) return;
    const storedToken = authService.getStoredToken();
    if (!storedToken) return;
    void redirectAfterLogin({
      token: storedToken,
      user: authService.getStoredUser() as LoginResponse['user'],
    });
  }), [redirectAfterLogin, showTwoFactor]);

  const loginMutation = useMutation({
    mutationFn: (data: LoginRequest) => authService.login(data),
    onSuccess: async (response) => {
      // Check if 2FA is required
      if (response.requiresTwoFactor) {
        setTwoFactorToken(response.twoFactorToken || '');
        setShowTwoFactor(true);
        // Don't clear stored login data since we need it for 2FA step
        return;
      }

      // Clear stored login data after successful complete login
      setStoredLoginData(null);

      await redirectAfterLogin(response);
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

  const requestOtpMutation = useMutation({
    mutationFn: (payload: { identifier: string; channel: OtpChannel; recaptchaToken?: string }) =>
      authService.requestLoginOtp({
        identifier: payload.identifier,
        channel: payload.channel,
        tenantCode: (securitySettings as any)?.tenantCode || undefined,
        recaptchaToken: payload.recaptchaToken,
      }),
    onSuccess: (resp) => {
      setOtpErrorMessage('');
      setOtpInfoMessage(resp.message || 'Code sent.');
      setOtpStage('verify');
    },
    onError: (error: any) => {
      const message = error.message || 'Failed to send code. Please try again.';
      setOtpErrorMessage(message);
    },
  });

  const verifyOtpMutation = useMutation({
    mutationFn: (payload: { identifier: string; channel: OtpChannel; otpCode: string; twoFactorCode?: string; recaptchaToken?: string }) =>
      authService.loginWithOtp({
        identifier: payload.identifier,
        channel: payload.channel,
        otpCode: payload.otpCode,
        tenantCode: (securitySettings as any)?.tenantCode || undefined,
        rememberMe: false,
        twoFactorCode: payload.twoFactorCode,
        recaptchaToken: payload.recaptchaToken,
      }),
    onSuccess: async (response) => {
      if (response.requiresTwoFactor) {
        setOtpRequiresTwoFactor(true);
        return;
      }

      setOtpRequiresTwoFactor(false);
      await redirectAfterLogin(response);
    },
    onError: (error: any) => {
      const message = error.message || 'Invalid code. Please try again.';
      setOtpErrorMessage(message);
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
    if (showTwoFactor && cleaned2fa?.length !== 6) {
      return; // Don't submit if 2FA code is not exactly 6 digits
    }

    const loginData: LoginRequest = {
      username: effectiveUsername,
      password: effectivePassword,
      tenantCode: undefined, // will be set during tenant selection
      rememberMe: effectiveRemember,
      twoFactorCode: cleaned2fa,
      recaptchaToken: (data as any).recaptchaToken || undefined,
    };

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

  useEffect(() => {
    const cleanedTwoFactorCode = twoFactorCode.replace(/\D/g, '');

    if (!showTwoFactor || cleanedTwoFactorCode.length !== 6) {
      lastAutoSubmittedTwoFactorCodeRef.current = null;
      return;
    }

    if (loginMutation.isPending || !storedLoginData) {
      return;
    }

    if (lastAutoSubmittedTwoFactorCodeRef.current === cleanedTwoFactorCode) {
      return;
    }

    lastAutoSubmittedTwoFactorCodeRef.current = cleanedTwoFactorCode;
    console.log('⚡ Auto-submitting password login 2FA code');
    void handleSubmit(onSubmit)();
  }, [handleSubmit, loginMutation.isPending, onSubmit, showTwoFactor, storedLoginData, twoFactorCode]);

  useEffect(() => {
    const cleanedOtpCode = otpCode.replace(/\D/g, '');
    const cleanedOtpTwoFactorCode = otpTwoFactorCode.replace(/\D/g, '');

    if (otpStage !== 'verify' || !otpRequiresTwoFactor || cleanedOtpCode.length !== 6 || cleanedOtpTwoFactorCode.length !== 6) {
      lastAutoSubmittedOtpTwoFactorKeyRef.current = null;
      return;
    }

    if (verifyOtpMutation.isPending) {
      return;
    }

    const submissionKey = `${otpIdentifier}:${otpChannel}:${cleanedOtpCode}:${cleanedOtpTwoFactorCode}`;
    if (lastAutoSubmittedOtpTwoFactorKeyRef.current === submissionKey) {
      return;
    }

    lastAutoSubmittedOtpTwoFactorKeyRef.current = submissionKey;
    setOtpErrorMessage('');
    console.log('⚡ Auto-submitting OTP 2FA code');
    verifyOtpMutation.mutate({
      identifier: otpIdentifier,
      channel: otpChannel,
      otpCode: cleanedOtpCode,
      twoFactorCode: cleanedOtpTwoFactorCode,
      recaptchaToken: otpRecaptchaToken || undefined,
    });
  }, [
    otpChannel,
    otpCode,
    otpIdentifier,
    otpRecaptchaToken,
    otpRequiresTwoFactor,
    otpStage,
    otpTwoFactorCode,
    verifyOtpMutation,
  ]);

  if (isLoginAppearancePending) {
    return <LoginPageLoading />;
  }

  if (isRedirecting) {
    return <LoginPageLoading message="Signing you in..." loginPageStyle={loginPageStyle} />;
  }

  return (
    <LoginPresentation style={loginPageStyle} backgroundUrl={loginBackgroundUrl}>
        <Card
          data-testid="shared-login-form"
          data-login-card
        >
          <CardHeader data-login-card-header className="space-y-1">
            <CardTitle data-login-card-title className="text-xl font-bold">
              {showTwoFactor
                ? '2FA Verification'
                : authMode === 'otp'
                  ? (otpRequiresTwoFactor ? '2FA Verification' : 'Sign In with Code')
                  : isDarkPremium
                    ? <>Sign In to <span>RHEMA-ERP</span></>
                    : <>Welcome to <span>RHEMA-ERP</span></>}
            </CardTitle>
            <CardDescription data-login-card-description className={isDarkPremium ? 'text-slate-300' : ''}>
              {showTwoFactor
                ? 'Please enter your authentication code to complete login'
                : authMode === 'otp'
                  ? (otpRequiresTwoFactor ? 'Enter your authenticator code to complete login' : 'We will send a one-time code to your email or phone')
                  : 'Enterprise Resource Planning Platform'
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
          <CardContent data-login-card-content>
            {authMode === 'password' ? (
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">

              {/* Username Field - Hidden during 2FA step */}
              {!showTwoFactor && (
                <div className="space-y-2">
                  <Label htmlFor="username" className={fieldLabelClass}>
                    Username or Email Address
                  </Label>
                  <div className="relative">
                    <Mail
                      aria-hidden="true"
                      className={`pointer-events-none absolute left-3 top-1/2 z-10 h-4 w-4 -translate-y-1/2 ${fieldIconClass}`}
                    />
                    <Input
                      data-login-field
                      id="username"
                      type="text"
                      placeholder="Enter your username or email"
                      autoComplete="username"
                      aria-invalid={Boolean(errors.username)}
                      aria-describedby={errors.username ? 'username-error' : undefined}
                      className={`pl-10 ${fieldControlClass} ${errors.username ? 'border-red-500' : ''}`}
                      {...register('username')}
                    />
                  </div>
                  {errors.username && (
                    <p id="username-error" role="alert" className="text-sm text-red-500 flex items-center gap-1">
                      <Shield className="h-3 w-3" />
                      {errors.username.message}
                    </p>
                  )}
                </div>
              )}

              {/* Password Field - Hidden during 2FA step */}
              {!showTwoFactor && (
                <div className="space-y-2">
                  <Label htmlFor="password" className={fieldLabelClass}>
                    Password
                  </Label>
                  <div className="relative">
                    <LockKeyhole
                      aria-hidden="true"
                      className={`pointer-events-none absolute left-3 top-1/2 z-10 h-4 w-4 -translate-y-1/2 ${fieldIconClass}`}
                    />
                    <Input
                      data-login-field
                      id="password"
                      type={showPassword ? 'text' : 'password'}
                      placeholder="Enter your password"
                      autoComplete="current-password"
                      aria-invalid={Boolean(errors.password)}
                      aria-describedby={errors.password ? 'password-error' : undefined}
                      className={`pl-10 pr-12 ${fieldControlClass} ${errors.password ? 'border-red-500' : ''}`}
                      {...register('password')}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="absolute right-2 top-1/2 -translate-y-1/2 h-8 w-8 p-0 hover:bg-transparent"
                      aria-label={showPassword ? 'Hide password' : 'Show password'}
                      onClick={() => setShowPassword(!showPassword)}
                    >
                      {showPassword ? (
                        <EyeOff className={`h-4 w-4 ${isDarkPremium ? 'text-slate-300' : 'text-slate-500'}`} />
                      ) : (
                        <Eye className={`h-4 w-4 ${isDarkPremium ? 'text-slate-300' : 'text-slate-500'}`} />
                      )}
                    </Button>
                  </div>
                  {errors.password && (
                    <p id="password-error" role="alert" className="text-sm text-red-500 flex items-center gap-1">
                      <Shield className="h-3 w-3" />
                      {errors.password.message}
                    </p>
                  )}
                </div>
              )}

              {/* Two-Factor Authentication Field - Only shown when required */}
              {showTwoFactor && (
                <div className="space-y-2">
                  <Label htmlFor="twoFactorCode" className={fieldLabelClass}>
                    AUTHENTICATION CODE
                  </Label>
                  <div className="relative">
                    <Input
                      data-login-field
                      id="twoFactorCode"
                      type="text"
                      value={twoFactorCode}
                      onChange={(e) => handleTwoFactorCodeChange(e.target.value)}
                      onKeyDown={handleTwoFactorKeyDown}
                      placeholder="Enter 6-digit code"
                      className={`text-center text-lg font-mono tracking-widest ${fieldControlClass}`}
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
                    {loginMutation.isPending ? (
                      <span className="text-blue-600 font-medium">
                        Verifying your code...
                      </span>
                    ) : twoFactorCode.replace(/\D/g, '').length === 6 ? (
                      <span className="text-green-600 font-medium">
                        ✓ Code complete - verifying automatically. If needed, you can still click "Verify Code".
                      </span>
                    ) : (
                      <span className={mutedTextClass}>
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
                      className={isDarkPremium ? 'text-slate-300 hover:text-white' : 'text-slate-500 hover:text-slate-700'}
                    >
                      ← Back to login
                    </Button>
                  </div>
                </div>
              )}

              {/* Password recovery - Hidden during 2FA step */}
              {!showTwoFactor && (
                <div className="flex justify-end">
                  <a
                    href="/forgot-password"
                    className={isDarkPremium
                      ? 'text-sm font-medium text-sky-300 transition-colors hover:text-sky-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-400'
                      : 'text-sm font-medium text-blue-600 transition-colors hover:text-blue-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500'}
                  >
                    Forgot password?
                  </a>
                </div>
              )}

              {/* Success Message */}
              {successMessage && (
                <div role="status" aria-live="polite" className="rounded-lg bg-green-50 p-4 border border-green-200">
                  <p className="text-sm text-green-700 flex items-center gap-2">
                    <Shield className="h-4 w-4" />
                    {successMessage}
                  </p>
                </div>
              )}

              {/* Error Message */}
              {errors.root && (
                <div role="alert" aria-live="assertive" className="rounded-lg bg-red-50 p-4 border border-red-200">
                  <p className="text-sm text-red-700 flex items-center gap-2">
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
                    <p className={`text-sm text-center ${isDarkPremium ? 'text-amber-300' : 'text-amber-600'}`}>
                      Additional verification required due to multiple failed login attempts
                    </p>
                  )}
                  <p className={`text-xs text-center ${mutedTextClass}`}>
                    Using {securitySettings.captchaProvider === 'recaptcha' ? 'Google reCAPTCHA' : 'hCAPTCHA'}
                  </p>
                </div>
              )}

              {/* Submit Button */}
              <Button
                data-login-primary-action
                type="submit"
                className={primaryActionClass}
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
            ) : (
              <div className="space-y-4">
                {/* Info message */}
                {otpInfoMessage && (
                  <div className="rounded-lg bg-blue-50 dark:bg-blue-900/20 p-4 border border-blue-200 dark:border-blue-800">
                    <p className="text-sm text-blue-700 dark:text-blue-400 flex items-center gap-2">
                      <Shield className="h-4 w-4" />
                      {otpInfoMessage}
                    </p>
                  </div>
                )}

                {/* Error message */}
                {otpErrorMessage && (
                  <div role="alert" aria-live="assertive" className="rounded-lg bg-red-50 p-4 border border-red-200">
                    <p className="text-sm text-red-700 flex items-center gap-2">
                      <Shield className="h-4 w-4" />
                      {otpErrorMessage}
                    </p>
                  </div>
                )}

                {/* Channel */}
                {!otpRequiresTwoFactor && (
                  <div className="space-y-2">
                    <Label htmlFor="otpChannel" className={fieldLabelClass}>
                      CHANNEL
                    </Label>
                    <select
                      id="otpChannel"
                      className={`h-10 w-full rounded-md border px-3 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-2 ${fieldControlClass}`}
                      value={otpChannel}
                      onChange={(e) => setOtpChannel(e.target.value as OtpChannel)}
                      disabled={otpStage === 'verify'}
                    >
                      <option value="Email">Email</option>
                      <option value="Sms">SMS</option>
                    </select>
                  </div>
                )}

                {/* Identifier */}
                {!otpRequiresTwoFactor && (
                  <div className="space-y-2">
                    <Label htmlFor="otpIdentifier" className={fieldLabelClass}>
                      {otpChannel === 'Email' ? 'EMAIL ADDRESS' : 'PHONE NUMBER'}
                    </Label>
                    <Input
                      data-login-field
                      id="otpIdentifier"
                      type={otpChannel === 'Email' ? 'email' : 'tel'}
                      placeholder={otpChannel === 'Email' ? 'you@company.com' : '+233XXXXXXXXX'}
                      value={otpIdentifier}
                      onChange={(e) => setOtpIdentifier(e.target.value)}
                      disabled={otpStage === 'verify'}
                      className={fieldControlClass}
                    />
                  </div>
                )}

                {/* OTP code */}
                {otpStage === 'verify' && (
                  <div className="space-y-2">
                    <Label htmlFor="otpCode" className={fieldLabelClass}>
                      ONE-TIME CODE
                    </Label>
                    <Input
                      data-login-field
                      id="otpCode"
                      inputMode="numeric"
                      placeholder="123456"
                      value={otpCode}
                      onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                      className={fieldControlClass}
                    />
                    <div className="flex items-center justify-between">
                      <Button
                        type="button"
                        variant="link"
                        className="px-0"
                        onClick={() => {
                          setOtpErrorMessage('');
                          requestOtpMutation.mutate({
                            identifier: otpIdentifier,
                            channel: otpChannel,
                            recaptchaToken: otpRecaptchaToken || undefined,
                          });
                        }}
                        disabled={requestOtpMutation.isPending}
                      >
                        Resend code
                      </Button>
                      <Button
                        type="button"
                        variant="link"
                        className="px-0"
                        onClick={() => {
                          setOtpStage('request');
                          setOtpCode('');
                          setOtpRequiresTwoFactor(false);
                          setOtpTwoFactorCode('');
                        }}
                      >
                        Change channel
                      </Button>
                    </div>
                  </div>
                )}

                {/* 2FA code (if enabled) */}
                {otpRequiresTwoFactor && (
                  <div className="space-y-2">
                    <Label htmlFor="otpTwoFactorCode" className={fieldLabelClass}>
                      AUTHENTICATOR CODE
                    </Label>
                    <Input
                      data-login-field
                      id="otpTwoFactorCode"
                      inputMode="numeric"
                      placeholder="123456"
                      value={otpTwoFactorCode}
                      onChange={(e) => setOtpTwoFactorCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                      className={fieldControlClass}
                    />
                    <p className="text-xs text-center">
                      {verifyOtpMutation.isPending ? (
                        <span className="text-blue-600 font-medium">
                          Verifying your codes...
                        </span>
                      ) : otpCode.replace(/\D/g, '').length === 6 && otpTwoFactorCode.replace(/\D/g, '').length === 6 ? (
                        <span className="text-green-600 font-medium">
                          ✓ Codes complete - signing you in automatically.
                        </span>
                      ) : (
                        <span className={mutedTextClass}>
                          Enter the 6-digit code from your authenticator app
                        </span>
                      )}
                    </p>
                  </div>
                )}

                {/* ReCAPTCHA */}
                {showRecaptcha && securitySettings && (
                  <div className="space-y-4">
                    <div className="flex justify-center">
                      <ReCAPTCHA
                        sitekey={securitySettings.captchaProvider === 'recaptcha'
                          ? securitySettings.recaptchaSiteKey || ''
                          : securitySettings.hCaptchaSiteKey || ''}
                        onChange={(token) => setOtpRecaptchaToken(token || '')}
                      />
                    </div>
                    <p className={`text-xs text-center ${mutedTextClass}`}>
                      Using {securitySettings.captchaProvider === 'recaptcha' ? 'Google reCAPTCHA' : 'hCAPTCHA'}
                    </p>
                  </div>
                )}

                {/* Action buttons */}
                {otpStage === 'request' ? (
                  <Button
                    data-login-primary-action
                    type="button"
                    className={primaryActionClass}
                    disabled={requestOtpMutation.isPending || !otpIdentifier.trim()}
                    onClick={() => {
                      setOtpErrorMessage('');
                      setOtpInfoMessage('');
                      requestOtpMutation.mutate({
                        identifier: otpIdentifier,
                        channel: otpChannel,
                        recaptchaToken: otpRecaptchaToken || undefined,
                      });
                    }}
                  >
                    {requestOtpMutation.isPending ? (
                      <>
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Sending...
                      </>
                    ) : (
                      'Send code'
                    )}
                  </Button>
                ) : (
                  <Button
                    data-login-primary-action
                    type="button"
                    className={primaryActionClass}
                    disabled={verifyOtpMutation.isPending || otpCode.replace(/\D/g, '').length !== 6 || (otpRequiresTwoFactor && otpTwoFactorCode.replace(/\D/g, '').length !== 6)}
                    onClick={() => {
                      setOtpErrorMessage('');
                      verifyOtpMutation.mutate({
                        identifier: otpIdentifier,
                        channel: otpChannel,
                        otpCode,
                        twoFactorCode: otpRequiresTwoFactor ? otpTwoFactorCode : undefined,
                        recaptchaToken: otpRecaptchaToken || undefined,
                      });
                    }}
                  >
                    {verifyOtpMutation.isPending ? (
                      <>
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Verifying...
                      </>
                    ) : (
                      'Sign in'
                    )}
                  </Button>
                )}
              </div>
            )}

            {/* Preserve the supplier application entry point and its existing availability policy. */}
            {allowSelfRegistration && (
              <div className={`mt-4 border-t pt-4 text-center ${isDarkPremium ? 'border-white/15' : 'border-slate-200'}`}>
                <Button data-login-secondary-action asChild variant="outline" className={isDarkPremium
                  ? 'h-10 w-full border-white/20 bg-white/5 text-sm font-semibold text-slate-100 hover:bg-white/10 hover:text-white'
                  : 'h-10 w-full text-sm font-semibold'}>
                  <Link href="/supplier-application">
                    <Shield className="mr-2 h-4 w-4" />
                    Apply as a supplier
                  </Link>
                </Button>
              </div>
            )}

          </CardContent>
        </Card>
    </LoginPresentation>
  );
}

// Loading component for Suspense fallback
function LoginPageLoading({
  message = 'Loading login page...',
  loginPageStyle,
}: {
  message?: string;
  loginPageStyle?: LoginPageStyle;
}) {
  if (!loginPageStyle) {
    return (
      <main className="grid min-h-svh place-items-center bg-slate-950 px-6 text-slate-100">
        <div
          role="status"
          data-login-loading-style="Pending"
          className="flex items-center space-x-3 rounded-2xl border border-white/10 bg-white/5 p-6 shadow-2xl"
        >
          <Loader2 className="h-8 w-8 animate-spin text-sky-300" />
          <span className="font-medium">{message}</span>
        </div>
      </main>
    );
  }

  const isDarkPremium = loginPageStyle === 'DarkPremium';

  return (
    <LoginPresentation style={loginPageStyle}>
      <div
        role="status"
        data-login-loading-style={loginPageStyle}
        className={`relative flex items-center space-x-3 rounded-2xl border p-6 shadow-2xl backdrop-blur-sm ${isDarkPremium
          ? 'border-white/15 bg-slate-950/80 text-white shadow-black/40'
          : 'border-white/30 bg-white/95 text-slate-900'}`}
      >
        <Loader2 className={`h-8 w-8 animate-spin ${isDarkPremium ? 'text-sky-300' : 'text-blue-600'}`} />
        <span className="font-medium">{message}</span>
      </div>
    </LoginPresentation>
  );
}

// Main page component that wraps the shared authentication form in Suspense.
export default function LoginPage() {
  return (
    <Suspense fallback={<LoginPageLoading />}>
      <SharedAuthenticationForm />
    </Suspense>
  );
}
