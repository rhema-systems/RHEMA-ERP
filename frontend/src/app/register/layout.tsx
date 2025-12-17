'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { authService } from '@/services/auth';

export default function RegisterLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();

  useEffect(() => {
    // Check if user is authenticated
    if (typeof window === 'undefined') return;

    const user = authService.getStoredUser();
    
    // If not authenticated, redirect to login
    if (!user) {
      router.push('/login');
      return;
    }

    // If user is LDAP (internal), redirect to dashboard
    if (user.authenticationProvider !== 'Local') {
      router.push('/dashboard');
      return;
    }
  }, [router]);

  // For external users (Local authentication), use external portal layout
  return <>{children}</>;
}

