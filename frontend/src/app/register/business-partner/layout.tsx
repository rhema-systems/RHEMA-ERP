'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { authService } from '@/services/auth';
import { ExternalSidebar } from '@/components/external-portal/external-sidebar';
import { ExternalNavbar } from '@/components/external-portal/external-navbar';

export default function BusinessPartnerRegistrationLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const [isValidating, setIsValidating] = useState(true);

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

    setIsValidating(false);
  }, [router]);

  if (isValidating) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading...</p>
        </div>
      </div>
    );
  }

  // Use external portal layout (sidebar + navbar)
  return (
    <div className="min-h-screen bg-gray-50">
      <div className="flex h-screen">
        {/* External Portal Sidebar */}
        <ExternalSidebar />

        {/* Main Content Area */}
        <div className="flex-1 flex flex-col overflow-hidden">
          {/* External Portal Navbar */}
          <ExternalNavbar />

          {/* Page Content */}
          <main className="flex-1 overflow-y-auto">
            <div className="container mx-auto p-6">
              {children}
            </div>
          </main>
        </div>
      </div>
    </div>
  );
}

