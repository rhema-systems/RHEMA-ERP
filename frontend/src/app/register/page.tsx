'use client';

import { useState, useRef } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Building2, Eye, EyeOff, Loader2, Shield, UserPlus, Mail, Phone } from 'lucide-react';
import ReCAPTCHA from 'react-google-recaptcha';

import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Checkbox } from '../../components/ui/checkbox';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { authService } from '../../services/auth';
import { settingsService } from '../../services/settings';
import PasswordMeter from '../../components/ui/password-meter';
import PhoneInput from '../../components/ui/phone-input';
// RegisterRequest is defined locally in this component

// Create the schema function that can be dynamic based on security settings
const createRegisterSchema = (captchaEnabled: boolean = false) => z.object({
  firstName: z.string().min(2, 'First name must be at least 2 characters'),
  lastName: z.string().min(2, 'Last name must be at least 2 characters'),
  username: z.string().min(3, 'Username must be at least 3 characters'),
  email: z.string().email('Please enter a valid email address'),
  phoneNumber: z.string().min(10, 'Please enter a valid phone number'),
  password: z.string().min(1, 'Password is required'), // We'll validate against security settings dynamically
  acceptPrivacy: z.boolean().refine(val => val === true, 'You must accept the privacy policy'),
  acceptTerms: z.boolean().refine(val => val === true, 'You must accept the terms of use'),
  recaptchaToken: captchaEnabled 
    ? z.string().min(1, 'Please complete the reCAPTCHA verification')
    : z.string().optional(),
});

// Default schema for initial setup
const registerSchema = createRegisterSchema(false);

type RegisterForm = z.infer<typeof registerSchema>;

type RegisterRequest = {
  firstName: string;
  lastName: string;
  username: string;
  email: string;
  phoneNumber: string;
  password: string;
  recaptchaToken: string;
};

export default function RegisterPage() {
  const [showPassword, setShowPassword] = useState(false);
  const [phoneNumber, setPhoneNumber] = useState('+233');
  const recaptchaRef = useRef<ReCAPTCHA>(null);
  const router = useRouter();

  // Fetch security settings for CAPTCHA configuration and password validation
  const { data: securitySettings } = useQuery({
    queryKey: ['securitySettings'],
    queryFn: () => settingsService.getPublicSecuritySettings(),
  });

  const {
    register,
    handleSubmit,
    formState: { errors },
    setError,
    setValue,
    watch,
  } = useForm<RegisterForm>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      acceptPrivacy: false,
      acceptTerms: false,
    },
  });

  // Watch password for strength meter
  const watchedPassword = watch('password', '');

  const registerMutation = useMutation({
    mutationFn: async (data: RegisterRequest) => {
      const response = await fetch('/api/auth/register', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || 'Registration failed');
      }

      return await response.json();
    },
    onSuccess: (response) => {
      // Redirect to OTP verification page
      router.push(`/verify-otp?phone=${encodeURIComponent(response.phoneNumber)}`);
    },
    onError: (error: any) => {
      const message = error.message || 'Registration failed. Please try again.';
      setError('root', { message });
    },
  });

  const onSubmit = async (data: RegisterForm) => {
    // If CAPTCHA is enabled, execute it programmatically for invisible mode
    if (securitySettings?.captchaEnabled && recaptchaRef.current) {
      try {
        const recaptchaToken = await recaptchaRef.current.executeAsync();
        setValue('recaptchaToken', recaptchaToken || '');
        data.recaptchaToken = recaptchaToken || '';
      } catch (error) {
        console.error('reCAPTCHA execution failed:', error);
        setError('recaptchaToken', { message: 'CAPTCHA verification failed. Please try again.' });
        return;
      }
    }
    
    const { acceptPrivacy, acceptTerms, ...registerData } = data;
    
    // Ensure recaptchaToken is always present (empty string if CAPTCHA is disabled)
    const requestData: RegisterRequest = {
      ...registerData,
      recaptchaToken: registerData.recaptchaToken || ''
    };
    
    registerMutation.mutate(requestData);
  };

  const handleRecaptchaChange = (token: string | null) => {
    setValue('recaptchaToken', token || '');
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4">
      {/* Background Pattern */}
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-blue-100 via-transparent to-transparent dark:from-blue-900/20"></div>
      <div className="absolute inset-0 bg-grid-slate-100 dark:bg-grid-slate-800/25 bg-[bottom_1px_center] dark:border-slate-800/25"></div>
      
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
          <h1 className="text-4xl font-bold tracking-tight bg-gradient-to-r from-slate-900 to-slate-700 dark:from-white dark:to-slate-300 bg-clip-text text-transparent">
            ERP System
          </h1>
          <p className="mt-2 text-slate-600 dark:text-slate-400">
            Create your account
          </p>
        </div>

        {/* Register Card */}
        <Card className="backdrop-blur-xl bg-white/80 dark:bg-slate-900/80 shadow-2xl border-white/20 dark:border-slate-800">
          <CardHeader className="space-y-1">
            <CardTitle className="text-2xl font-bold text-center">Create Account</CardTitle>
            <CardDescription className="text-center">
              Enter your information to create an account
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
              {/* Name Fields */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="firstName">First Name</Label>
                  <Input
                    id="firstName"
                    {...register('firstName')}
                    className={errors.firstName ? 'border-red-500' : ''}
                  />
                  {errors.firstName && (
                    <p className="text-sm text-red-500">{errors.firstName.message}</p>
                  )}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="lastName">Last Name</Label>
                  <Input
                    id="lastName"
                    {...register('lastName')}
                    className={errors.lastName ? 'border-red-500' : ''}
                  />
                  {errors.lastName && (
                    <p className="text-sm text-red-500">{errors.lastName.message}</p>
                  )}
                </div>
              </div>

              {/* Username Field */}
              <div className="space-y-2">
                <Label htmlFor="username">Username</Label>
                <Input
                  id="username"
                  {...register('username')}
                  className={errors.username ? 'border-red-500' : ''}
                />
                {errors.username && (
                  <p className="text-sm text-red-500">{errors.username.message}</p>
                )}
              </div>

              {/* Email Field */}
              <div className="space-y-2">
                <Label htmlFor="email">Email Address</Label>
                <div className="relative">
                  <Input
                    id="email"
                    type="email"
                    {...register('email')}
                    className={errors.email ? 'border-red-500' : ''}
                  />
                  <Mail className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                </div>
                {errors.email && (
                  <p className="text-sm text-red-500">{errors.email.message}</p>
                )}
              </div>

              {/* Phone Number Field */}
              <div className="space-y-2">
                <Label htmlFor="phoneNumber">Phone Number</Label>
                <PhoneInput
                  value={phoneNumber}
                  onChange={(value) => {
                    setPhoneNumber(value);
                    setValue('phoneNumber', value);
                  }}
                  error={!!errors.phoneNumber}
                  placeholder="Enter your phone number"
                />
                {errors.phoneNumber && (
                  <p className="text-sm text-red-500">{errors.phoneNumber.message}</p>
                )}
              </div>

              {/* Password Field */}
              <div className="space-y-2">
                <Label htmlFor="password">Password</Label>
                <div className="relative">
                  <Input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    {...register('password')}
                    className={errors.password ? 'border-red-500' : ''}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="absolute right-2 top-1/2 -translate-y-1/2"
                    onClick={() => setShowPassword(!showPassword)}
                  >
                    {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  </Button>
                </div>
                {errors.password && (
                  <p className="text-sm text-red-500">{errors.password.message}</p>
                )}
                
                {/* Password Strength Meter */}
                <PasswordMeter 
                  password={watchedPassword || ''} 
                  securitySettings={securitySettings || {
                    passwordMinLength: 8,
                    passwordRequireUppercase: true,
                    passwordRequireLowercase: true,
                    passwordRequireDigits: true,
                    passwordRequireSpecialChars: true
                  }} 
                />
              </div>

              {/* Privacy Policy and Terms */}
              <div className="space-y-3">
                <div className="flex items-center space-x-2">
                  <Checkbox
                    id="acceptPrivacy"
                    checked={watch('acceptPrivacy') || false}
                    onCheckedChange={(checked) => setValue('acceptPrivacy', checked === true)}
                  />
                  <Label htmlFor="acceptPrivacy" className="text-sm text-slate-600 dark:text-slate-400">
                    I accept the{' '}
                    {securitySettings?.privacyPolicyUrl ? (
                      <a 
                        href={securitySettings.privacyPolicyUrl} 
                        target="_blank" 
                        rel="noopener noreferrer" 
                        className="text-blue-600 hover:underline"
                      >
                        Privacy Policy
                      </a>
                    ) : (
                      <span className="text-blue-600">Privacy Policy</span>
                    )}
                  </Label>
                </div>
                {errors.acceptPrivacy && (
                  <p className="text-sm text-red-500">{errors.acceptPrivacy.message}</p>
                )}

                <div className="flex items-center space-x-2">
                  <Checkbox
                    id="acceptTerms"
                    checked={watch('acceptTerms') || false}
                    onCheckedChange={(checked) => setValue('acceptTerms', checked === true)}
                  />
                  <Label htmlFor="acceptTerms" className="text-sm text-slate-600 dark:text-slate-400">
                    I accept the{' '}
                    {securitySettings?.termsOfServiceUrl ? (
                      <a 
                        href={securitySettings.termsOfServiceUrl} 
                        target="_blank" 
                        rel="noopener noreferrer" 
                        className="text-blue-600 hover:underline"
                      >
                        Terms of Service
                      </a>
                    ) : (
                      <span className="text-blue-600">Terms of Service</span>
                    )}
                  </Label>
                </div>
                {errors.acceptTerms && (
                  <p className="text-sm text-red-500">{errors.acceptTerms.message}</p>
                )}
              </div>

              {/* Smart reCAPTCHA - Invisible by default, shows challenge only when needed */}
              {securitySettings && securitySettings.captchaEnabled && (
                <>
                  <div className="flex justify-center">
                    <ReCAPTCHA
                      ref={recaptchaRef}
                      sitekey={securitySettings.captchaProvider === 'recaptcha' 
                        ? securitySettings.recaptchaSiteKey || '' 
                        : securitySettings.hCaptchaSiteKey || ''}
                      size="invisible" // Invisible mode - only shows challenge when Google detects risk
                      onChange={handleRecaptchaChange}
                    />
                  </div>
                  {errors.recaptchaToken && (
                    <p className="text-sm text-red-500 text-center">{errors.recaptchaToken.message}</p>
                  )}
                  <div className="text-center">
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                      🛡️ Protected by {securitySettings.captchaProvider === 'recaptcha' ? 'Google reCAPTCHA' : 'hCAPTCHA'}
                    </p>
                    <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">
                      Challenge will appear if suspicious activity is detected
                    </p>
                  </div>
                </>
              )}

              {/* Error Message */}
              {errors.root && (
                <div className="rounded-lg bg-red-50 p-4">
                  <p className="text-sm text-red-500">{errors.root.message}</p>
                </div>
              )}

              {/* Submit Button */}
              <Button
                type="submit"
                className="w-full"
                disabled={registerMutation.isPending}
              >
                {registerMutation.isPending ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    Creating Account...
                  </>
                ) : (
                  'Create Account'
                )}
              </Button>
            </form>

            {/* Login Link */}
            <div className="mt-4 text-center">
              <p className="text-sm text-slate-600">
                Already have an account?{' '}
                <Link href="/login" className="text-blue-600 hover:underline">
                  Sign in
                </Link>
              </p>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
