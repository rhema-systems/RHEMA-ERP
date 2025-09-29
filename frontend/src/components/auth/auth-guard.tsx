'use client';

import { useEffect, ReactNode } from 'react';
import { useRouter } from 'next/navigation';
import { authService } from '../../services/auth';

interface AuthGuardProps {
  children: ReactNode;
  fallback?: ReactNode;
  redirectTo?: string;
}

export function AuthGuard({ 
  children, 
  fallback = null, 
  redirectTo = '/login' 
}: AuthGuardProps) {
  const router = useRouter();

  useEffect(() => {
    if (typeof window !== 'undefined' && !authService.isAuthenticated()) {
      router.push(redirectTo);
    }
  }, [router, redirectTo]);

  // Don't render anything if not authenticated
  if (typeof window !== 'undefined' && !authService.isAuthenticated()) {
    return <>{fallback}</>;
  }

  return <>{children}</>;
}