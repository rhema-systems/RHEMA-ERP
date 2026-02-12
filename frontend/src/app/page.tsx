'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { authService } from '../services/auth';

export default function Home() {
  const router = useRouter();
  const [isClient, setIsClient] = useState(false);

  useEffect(() => {
    setIsClient(true);
  }, []);

  useEffect(() => {
    if (!isClient) return;

    // Check if user is authenticated (only on client side)
    if (authService.isAuthenticated()) {
      // Route based on authentication provider and roles
      const user = authService.getStoredUser();

      // Check if user has admin privileges
      const isSuperAdmin = user?.roles?.includes('SuperAdmin');
      const isAdministrator = user?.roles?.includes('Administrator');
      const username = user?.username?.toLowerCase() || '';
      const isAdminUser = username === 'admin';
      const hasAdminPrivileges = isSuperAdmin || isAdministrator || isAdminUser;

      // Only redirect to external portal if user is Local auth AND doesn't have admin privileges
      if (user?.authenticationProvider === 'Local' && !hasAdminPrivileges) {
        // External users go to external portal
        router.push('/external-portal');
      } else {
        // Internal users (LDAP) or admins go to dashboard
        router.push('/dashboard');
      }
    } else {
      router.push('/login');
    }
  }, [router, isClient]);

  // Show loading state while redirecting
  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-50 via-blue-50 to-indigo-100">
      <div className="text-center">
        <div className="animate-spin rounded-full h-12 w-12 border-4 border-blue-600 border-t-transparent mx-auto mb-4"></div>
        <p className="text-slate-600">Loading ERP System...</p>
      </div>
    </div>
  );
}
