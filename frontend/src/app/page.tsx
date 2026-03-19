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
      const user = authService.getStoredUser();
      const isSupportHost = typeof window !== 'undefined' && window.location.hostname.toLowerCase().startsWith('support.');
      const isExternalUser = user?.roles?.includes('ExternalUser') ?? false;

      if (isExternalUser) {
        // External users go to External Portal. On support.* host, middleware rewrites / to the support area.
        router.push(isSupportHost ? '/' : '/external-portal');
      } else {
        // Internal users go to dashboard
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
