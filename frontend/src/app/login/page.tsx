'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Building2, Eye, EyeOff, Loader2, Shield, Users } from 'lucide-react';

import { Button } from '../../components/ui/button';
import { Input } from '../../components/ui/input';
import { Label } from '../../components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card';
import { authService } from '../../services/auth';
import { useTenant } from '../../contexts/TenantContext';
import type { LoginRequest, Tenant } from '../../types';

const loginSchema = z.object({
  username: z.string().min(3, 'Username must be at least 3 characters'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  tenantCode: z.string().min(1, 'Please select a tenant'),
  rememberMe: z.boolean().optional(),
});

type LoginForm = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const [showPassword, setShowPassword] = useState(false);
  const router = useRouter();
  const { tenants, isLoadingTenants } = useTenant();

  // Use tenant context data
  const isLoading = isLoadingTenants;
  const isError = false; // Context handles errors internally
  
  console.log('LoginPage: tenant loading state:', { isLoading, tenantCount: tenants.length, tenants });

  const {
    register,
    handleSubmit,
    formState: { errors },
    setError,
    setValue,
    watch,
  } = useForm<LoginForm>({
    resolver: zodResolver(loginSchema),
    defaultValues: {
      username: 'admin',
      password: 'Admin123!',
      tenantCode: '', // Always start with empty selection
      rememberMe: false,
    },
  });

  const loginMutation = useMutation({
    mutationFn: (data: LoginRequest) => authService.login(data),
    onSuccess: (response) => {
      // Redirect to dashboard on successful login
      router.push('/dashboard');
    },
    onError: (error: any) => {
      const message = error.message || 'Login failed. Please try again.';
      setError('root', { message });
    },
  });

  const onSubmit = (data: LoginForm) => {
    loginMutation.mutate(data);
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
            Enterprise Resource Planning Platform
          </p>
        </div>

        {/* Login Card */}
        <Card className="backdrop-blur-xl bg-white/80 dark:bg-slate-900/80 shadow-2xl border-white/20 dark:border-slate-800">
          <CardHeader className="space-y-1 pb-6">
            <CardTitle className="text-2xl font-bold text-center">Sign In</CardTitle>
            <CardDescription className="text-center">
              Enter your credentials to access your account
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
              {/* Tenant Selection */}
              <div className="space-y-2">
                <Label htmlFor="tenantCode" className="text-xs font-semibold text-slate-600 uppercase tracking-wider">
                  TENANT
                </Label>
                <div className="flex items-stretch gap-3">
                  <div className="flex-1">
                    <select
                      id="tenantCode"
                      className={`w-full px-3 py-2 border border-slate-300 rounded-md shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 ${errors.tenantCode ? 'border-red-500' : ''}`}
                      {...register('tenantCode')}
                      disabled={isLoading}
                    >
                      <option value="">Select a tenant</option>
                      {tenants.map((tenant) => (
                        <option key={tenant.code} value={tenant.code}>
                          {tenant.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="flex-shrink-0">
                    <button
                      type="button"
                      className="text-xs text-blue-600 hover:text-blue-500 border border-blue-600 hover:border-blue-500 px-4 py-2 rounded transition-colors whitespace-nowrap h-full"
                    >
                      switch
                    </button>
                  </div>
                </div>
                {errors.tenantCode && (
                  <p className="text-sm text-red-500 flex items-center gap-1">
                    <Shield className="h-3 w-3" />
                    {errors.tenantCode.message}
                  </p>
                )}
                {isError && (
                  <p className="text-sm text-red-500 flex items-center gap-1">
                    <Shield className="h-3 w-3" />
                    Failed to load tenants. Please refresh the page.
                  </p>
                )}
              </div>

              {/* Username Field */}
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
                <p className="text-xs text-slate-500 italic">Default username: admin</p>
                {errors.username && (
                  <p className="text-sm text-red-500 flex items-center gap-1">
                    <Shield className="h-3 w-3" />
                    {errors.username.message}
                  </p>
                )}
              </div>

              {/* Password Field */}
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
                <p className="text-xs text-slate-500 italic">Default password: Admin123!</p>
                {errors.password && (
                  <p className="text-sm text-red-500 flex items-center gap-1">
                    <Shield className="h-3 w-3" />
                    {errors.password.message}
                  </p>
                )}
              </div>

              {/* Remember Me & Forgot Password */}
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

              {/* Error Message */}
              {errors.root && (
                <div className="rounded-lg bg-red-50 dark:bg-red-900/20 p-4 border border-red-200 dark:border-red-800">
                  <p className="text-sm text-red-700 dark:text-red-400 flex items-center gap-2">
                    <Shield className="h-4 w-4" />
                    {errors.root.message}
                  </p>
                </div>
              )}

              {/* Submit Button */}
              <Button
                type="submit"
                className="w-full h-12 text-base font-semibold"
                disabled={loginMutation.isPending}
              >
                {loginMutation.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Signing in...
                  </>
                ) : (
                  'Sign In'
                )}
              </Button>
            </form>

            {/* Demo Credentials */}
            <div className="mt-8 p-4 bg-slate-50 dark:bg-slate-800 rounded-xl border border-slate-200 dark:border-slate-700">
              <h4 className="text-sm font-semibold text-slate-900 dark:text-slate-100 mb-2">
                Demo Credentials
              </h4>
              <div className="space-y-1 text-xs text-slate-600 dark:text-slate-400">
                <p><strong>Admin:</strong> admin / Admin123!</p>
                <p><strong>Manager:</strong> manager / Manager123!</p>
                <p><strong>Employee:</strong> employee / Employee123!</p>
              </div>
            </div>
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