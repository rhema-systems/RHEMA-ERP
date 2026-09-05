'use client';

import { useRef, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Eye, EyeOff, Loader2, Mail } from 'lucide-react';
import ReCAPTCHA from 'react-google-recaptcha';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import PasswordMeter from '@/components/ui/password-meter';
import PhoneInput from '@/components/ui/phone-input';
import { settingsService } from '@/services/settings';
import { publicCareersService } from '@/services/hr/careers.service';

const registerSchema = z.object({
  firstName: z.string().min(2, 'First name must be at least 2 characters'),
  lastName: z.string().min(2, 'Last name must be at least 2 characters'),
  username: z.string().min(3, 'Username must be at least 3 characters'),
  email: z.string().email('Please enter a valid email address'),
  phoneNumber: z.string().min(10, 'Please enter a valid phone number'),
  password: z.string().min(1, 'Password is required'),
  acceptPrivacy: z.boolean().refine((v) => v === true, 'You must accept the privacy policy'),
  acceptTerms: z.boolean().refine((v) => v === true, 'You must accept the terms of use'),
  recaptchaToken: z.string().optional(),
});

type RegisterForm = z.infer<typeof registerSchema>;

/**
 * Candidate self-registration (POST api/auth/register-candidate). Mirrors the business-partner
 * register page, with the careers deltas: the tenant is passed explicitly (the job board is
 * tenant-specific) and the account gets the Candidate role, never ExternalUser. Activation is by
 * SMS OTP — the same /verify-otp page the partner flow uses.
 */
export default function CandidateRegisterPage() {
  const [showPassword, setShowPassword] = useState(false);
  const [phoneNumber, setPhoneNumber] = useState('+233');
  const recaptchaRef = useRef<ReCAPTCHA>(null);
  const router = useRouter();

  const { data: securitySettings } = useQuery({
    queryKey: ['securitySettings'],
    queryFn: () => settingsService.getPublicSecuritySettings(),
  });

  const tenant = useQuery({
    queryKey: ['careers', 'tenant'],
    queryFn: () => publicCareersService.resolveTenant(),
    staleTime: Infinity,
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
    defaultValues: { acceptPrivacy: false, acceptTerms: false },
  });

  const watchedPassword = watch('password', '');

  const registerMutation = useMutation({
    mutationFn: (data: RegisterForm) => {
      const { acceptPrivacy: _p, acceptTerms: _t, ...payload } = data;
      return publicCareersService.register(tenant.data?.id ?? '', {
        ...payload,
        recaptchaToken: payload.recaptchaToken || '',
      });
    },
    onSuccess: (response) => {
      router.push(`/verify-otp?phone=${encodeURIComponent(response.phoneNumber)}`);
    },
    onError: (error: any) => {
      setError('root', { message: error?.message || 'Registration failed. Please try again.' });
    },
  });

  const onSubmit = async (data: RegisterForm) => {
    if (!tenant.data?.id) {
      setError('root', { message: 'The careers portal is not available right now.' });
      return;
    }
    if (securitySettings?.captchaEnabled && recaptchaRef.current) {
      try {
        const token = await recaptchaRef.current.executeAsync();
        setValue('recaptchaToken', token || '');
        data.recaptchaToken = token || '';
      } catch {
        setError('recaptchaToken', { message: 'CAPTCHA verification failed. Please try again.' });
        return;
      }
    }
    registerMutation.mutate(data);
  };

  return (
    <div className="mx-auto max-w-md">
      <Card>
        <CardHeader className="space-y-1">
          <CardTitle className="text-xl">Create your candidate account</CardTitle>
          <CardDescription>
            One account tracks every application you make{tenant.data?.name ? ` at ${tenant.data.name}` : ''}.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="firstName">First name</Label>
                <Input id="firstName" {...register('firstName')} />
                {errors.firstName && <p className="text-sm text-red-500">{errors.firstName.message}</p>}
              </div>
              <div className="space-y-2">
                <Label htmlFor="lastName">Last name</Label>
                <Input id="lastName" {...register('lastName')} />
                {errors.lastName && <p className="text-sm text-red-500">{errors.lastName.message}</p>}
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="username">Username</Label>
              <Input id="username" {...register('username')} />
              {errors.username && <p className="text-sm text-red-500">{errors.username.message}</p>}
            </div>

            <div className="space-y-2">
              <Label htmlFor="email">Email address</Label>
              <div className="relative">
                <Input id="email" type="email" {...register('email')} />
                <Mail className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              </div>
              {errors.email && <p className="text-sm text-red-500">{errors.email.message}</p>}
              <p className="text-xs text-muted-foreground">
                You&apos;ll confirm this address later — it&apos;s how we match you to any existing
                candidate record.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="phoneNumber">Phone number</Label>
              <PhoneInput
                value={phoneNumber}
                onChange={(value) => {
                  setPhoneNumber(value);
                  setValue('phoneNumber', value);
                }}
                error={!!errors.phoneNumber}
                placeholder="Enter your phone number"
              />
              {errors.phoneNumber && <p className="text-sm text-red-500">{errors.phoneNumber.message}</p>}
              <p className="text-xs text-muted-foreground">
                A verification code is sent here to activate your account.
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <div className="relative">
                <Input id="password" type={showPassword ? 'text' : 'password'} {...register('password')} />
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
              {errors.password && <p className="text-sm text-red-500">{errors.password.message}</p>}
              <PasswordMeter
                password={watchedPassword || ''}
                securitySettings={
                  securitySettings || {
                    passwordMinLength: 8,
                    passwordRequireUppercase: true,
                    passwordRequireLowercase: true,
                    passwordRequireDigits: true,
                    passwordRequireSpecialChars: true,
                  }
                }
              />
            </div>

            <div className="space-y-3">
              <div className="flex items-center space-x-2">
                <Checkbox
                  id="acceptPrivacy"
                  checked={watch('acceptPrivacy') || false}
                  onCheckedChange={(checked) => setValue('acceptPrivacy', checked === true)}
                />
                <Label htmlFor="acceptPrivacy" className="text-sm text-muted-foreground">
                  I accept the{' '}
                  {securitySettings?.privacyPolicyUrl ? (
                    <a
                      href={securitySettings.privacyPolicyUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-primary hover:underline"
                    >
                      Privacy Policy
                    </a>
                  ) : (
                    <span className="text-primary">Privacy Policy</span>
                  )}
                </Label>
              </div>
              {errors.acceptPrivacy && <p className="text-sm text-red-500">{errors.acceptPrivacy.message}</p>}

              <div className="flex items-center space-x-2">
                <Checkbox
                  id="acceptTerms"
                  checked={watch('acceptTerms') || false}
                  onCheckedChange={(checked) => setValue('acceptTerms', checked === true)}
                />
                <Label htmlFor="acceptTerms" className="text-sm text-muted-foreground">
                  I accept the{' '}
                  {securitySettings?.termsOfServiceUrl ? (
                    <a
                      href={securitySettings.termsOfServiceUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-primary hover:underline"
                    >
                      Terms of Service
                    </a>
                  ) : (
                    <span className="text-primary">Terms of Service</span>
                  )}
                </Label>
              </div>
              {errors.acceptTerms && <p className="text-sm text-red-500">{errors.acceptTerms.message}</p>}
            </div>

            {securitySettings?.captchaEnabled && (
              <>
                <div className="flex justify-center">
                  <ReCAPTCHA
                    ref={recaptchaRef}
                    sitekey={
                      securitySettings.captchaProvider === 'recaptcha'
                        ? securitySettings.recaptchaSiteKey || ''
                        : securitySettings.hCaptchaSiteKey || ''
                    }
                    size="invisible"
                    onChange={(token) => setValue('recaptchaToken', token || '')}
                  />
                </div>
                {errors.recaptchaToken && (
                  <p className="text-center text-sm text-red-500">{errors.recaptchaToken.message}</p>
                )}
              </>
            )}

            {errors.root && (
              <div className="rounded-lg bg-red-50 p-4 dark:bg-red-950/40">
                <p className="text-sm text-red-500">{errors.root.message}</p>
              </div>
            )}

            <Button type="submit" className="w-full" disabled={registerMutation.isPending || tenant.isLoading}>
              {registerMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Creating account…
                </>
              ) : (
                'Create account'
              )}
            </Button>
          </form>

          <div className="mt-4 text-center">
            <p className="text-sm text-muted-foreground">
              Already have an account?{' '}
              <Link href="/login?redirect=%2Fexternal-portal%2Fcareers" className="text-primary hover:underline">
                Sign in
              </Link>
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
