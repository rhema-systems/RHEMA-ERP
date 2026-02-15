'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';
import { Building2, Mail, ArrowLeft, CheckCircle, Loader2, Shield } from 'lucide-react';
import ReCAPTCHA from 'react-google-recaptcha';

import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { authService } from '../../services/auth';
import { settingsService } from '../../services/settings';
import { toast } from 'sonner';

const forgotPasswordSchema = z.object({
  email: z.string().email('Please enter a valid email address'),
});

type ForgotPasswordForm = z.infer<typeof forgotPasswordSchema>;

export default function ForgotPasswordPage() {
  const [isSubmitted, setIsSubmitted] = useState(false);
  const [captchaToken, setCaptchaToken] = useState<string | null>(null);
  const [captchaEnabled, setCaptchaEnabled] = useState(false);
  const [captchaProvider, setCaptchaProvider] = useState<'recaptcha' | 'hcaptcha'>('recaptcha');
  const [recaptchaSiteKey, setRecaptchaSiteKey] = useState<string | null>(null);
  const [hCaptchaSiteKey, setHCaptchaSiteKey] = useState<string | null>(null);

  useEffect(() => {
    // Best-effort: fetch public security settings for CAPTCHA
    settingsService.getPublicSecuritySettings().then((s) => {
      setCaptchaEnabled(!!s.captchaEnabled);
      setCaptchaProvider((s.captchaProvider || 'recaptcha') as any);
      setRecaptchaSiteKey(s.recaptchaSiteKey || null);
      setHCaptchaSiteKey(s.hCaptchaSiteKey || null);
    }).catch(() => {
      // ignore
    });
  }, []);

  const {
    register,
    handleSubmit,
    formState: { errors },
    getValues,
  } = useForm<ForgotPasswordForm>({
    resolver: zodResolver(forgotPasswordSchema),
  });

  const forgotPasswordMutation = useMutation({
    mutationFn: (email: string) => authService.forgotPassword({ email, captchaToken }),
    onSuccess: () => {
      setIsSubmitted(true);
    },
    onError: (error: any) => {
      const message = error.message || 'Failed to request password reset. Please try again.';
      console.error('Forgot password error:', message);
      toast.error(message);
    },
  });

  const onSubmit = (data: ForgotPasswordForm) => {
    if (captchaEnabled && !captchaToken) {
      toast.error('Please complete the CAPTCHA verification.');
      return;
    }
    forgotPasswordMutation.mutate(data.email);
  };

  if (isSubmitted) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100 dark:from-slate-900 dark:via-slate-800 dark:to-slate-900 p-4">
        {/* Background Pattern */}
        <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-blue-100 via-transparent to-transparent dark:from-blue-900/20"></div>
        <div className="absolute inset-0 bg-grid-slate-100 dark:bg-grid-slate-800/25 bg-[bottom_1px_center] dark:border-slate-800/25"></div>
        
        <div className="relative w-full max-w-md space-y-8">
          {/* Success Card */}
          <Card className="backdrop-blur-xl bg-white/80 dark:bg-slate-900/80 shadow-2xl border-white/20 dark:border-slate-800">
            <CardHeader className="text-center pb-6">
              <div className="flex justify-center mb-4">
                <div className="flex h-16 w-16 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/30">
                  <CheckCircle className="h-8 w-8 text-green-600 dark:text-green-400" />
                </div>
              </div>
              <CardTitle className="text-2xl font-bold">Check Your Email</CardTitle>
              <CardDescription className="text-center">
                We&rsquo;ve sent a password reset link to your email address
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="text-center space-y-4">
                <div className="p-4 bg-blue-50 dark:bg-blue-900/20 rounded-xl border border-blue-200 dark:border-blue-800">
                  <Mail className="h-6 w-6 text-blue-600 dark:text-blue-400 mx-auto mb-2" />
                  <p className="text-sm text-blue-800 dark:text-blue-300 font-medium">
                    Email sent to: {getValues('email')}
                  </p>
                </div>
                
                <div className="text-sm text-slate-600 dark:text-slate-400 space-y-2">
                  <p>Please check your email and click the reset link to create a new password.</p>
                <p>If you don&rsquo;t see the email, check your spam folder.</p>
                </div>
              </div>

              <div className="space-y-3">
                <Link href="/login">
                  <Button className="w-full h-11 text-base font-semibold">
                    <ArrowLeft className="h-4 w-4 mr-2" />
                    Back to Sign In
                  </Button>
                </Link>
                
                <Button
                  variant="outline"
                  className="w-full h-11 text-base font-medium"
                  onClick={() => setIsSubmitted(false)}
                >
                  Send Another Email
                </Button>
              </div>
            </CardContent>
          </Card>

          {/* Footer */}
          <div className="text-center text-sm text-slate-500 dark:text-slate-400">
            <p>© 2025 ERP System. All rights reserved.</p>
          </div>
        </div>
      </div>
    );
  }

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
            Enterprise Resource Planning Platform
          </p>
        </div>

        {/* Forgot Password Card */}
        <Card className="backdrop-blur-xl bg-white/80 dark:bg-slate-900/80 shadow-2xl border-white/20 dark:border-slate-800">
          <CardHeader className="space-y-1 pb-6">
            <CardTitle className="text-2xl font-bold text-center">Forgot Password?</CardTitle>
            <CardDescription className="text-center">
              Enter your email address and we&rsquo;ll send you a link to reset your password
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
              {/* Email Field */}
              <div className="space-y-2">
                <Label htmlFor="email">Email Address</Label>
                <div className="relative">
                  <Mail className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500 dark:text-slate-400" />
                  <Input
                    id="email"
                    type="email"
                    placeholder="Enter your email address"
                    className={`pl-10 ${errors.email ? 'border-red-500' : ''}`}
                    {...register('email')}
                  />
                </div>
                {errors.email && (
                  <p className="text-sm text-red-500 flex items-center gap-1">
                    <Shield className="h-3 w-3" />
                    {errors.email.message}
                  </p>
                )}
              </div>

              {/* Info Box */}
              <div className="p-4 bg-blue-50 dark:bg-blue-900/20 rounded-xl border border-blue-200 dark:border-blue-800">
                <p className="text-sm text-blue-800 dark:text-blue-300">
                  <strong>Note:</strong> If you don&rsquo;t remember your email, please contact your system administrator for assistance.
                </p>
              </div>

              {/* CAPTCHA */}
              {captchaEnabled ? (
                <div className="space-y-2 flex flex-col items-center">
                  {captchaProvider === 'recaptcha' && recaptchaSiteKey ? (
                    <ReCAPTCHA sitekey={recaptchaSiteKey} onChange={(t) => setCaptchaToken(t)} />
                  ) : captchaProvider === 'hcaptcha' && hCaptchaSiteKey ? (
                    <ReCAPTCHA sitekey={hCaptchaSiteKey} onChange={(t) => setCaptchaToken(t)} />
                  ) : (
                    <p className="text-sm text-slate-600 dark:text-slate-400 text-center">
                      CAPTCHA is enabled but not configured. Please contact your system administrator.
                    </p>
                  )}
                </div>
              ) : null}

              {/* Submit Button */}
              <Button
                type="submit"
                className="w-full h-12 text-base font-semibold"
                disabled={forgotPasswordMutation.isPending}
              >
                {forgotPasswordMutation.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Sending Reset Link...
                  </>
                ) : (
                  <>
                    <Mail className="h-4 w-4 mr-2" />
                    Send Reset Link
                  </>
                )}
              </Button>

              {/* Back to Login */}
              <div className="text-center">
                <Link
                  href="/login"
                  className="inline-flex items-center text-sm font-medium text-blue-600 hover:text-blue-500 dark:text-blue-400 dark:hover:text-blue-300 transition-colors"
                >
                  <ArrowLeft className="h-3 w-3 mr-1" />
                  Back to Sign In
                </Link>
              </div>
            </form>
          </CardContent>
        </Card>

        {/* Footer */}
        <div className="text-center text-sm text-slate-500 dark:text-slate-400">
          <p>© 2025 ERP System. All rights reserved.</p>
          <p className="mt-1">Secure enterprise management platform</p>
        </div>
      </div>
    </div>
  );
}
