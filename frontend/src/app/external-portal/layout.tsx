'use client';

import { ExternalSidebar } from '@/components/external-portal/external-sidebar';
import { ExternalNavbar } from '@/components/external-portal/external-navbar';
import { AuthGuard } from '@/components/auth/auth-guard';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { getAuthenticatedHomePath, isCandidateUser, isConsultantClientUser, isExternalPortalUser } from '@/lib/auth-routing';
import { authService } from '@/services/auth';

// External portal is auth-dependent and uses browser-only storage; disable static generation.
export const dynamic = 'force-dynamic';

export default function ExternalPortalLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const [isValidating, setIsValidating] = useState(true);
  const [isAuthorized, setIsAuthorized] = useState(false);

  useEffect(() => {
    // Verify user is authenticated and has Local authentication provider
    const user = authService.getStoredUser();

    if (!user) {
      router.push('/login');
      setIsValidating(false);
      return;
    }

    // Careers candidates and consultant-client contacts share this shell — the sidebar
    // shows each their own menu.
    if (!isExternalPortalUser(user) && !isCandidateUser(user) && !isConsultantClientUser(user)) {
      router.push(getAuthenticatedHomePath(user));
      setIsValidating(false);
      return;
    }

    setIsAuthorized(true);
    setIsValidating(false);
  }, [router]);

  // Show loading state while validating
  if (isValidating) {
    return (
      <div className="flex h-screen items-center justify-center bg-gray-50">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading...</p>
        </div>
      </div>
    );
  }

  // Don't render portal if not authorized
  if (!isAuthorized) {
    return null;
  }

  return (
    <AuthGuard>
      <div className="flex h-screen overflow-hidden bg-gray-50">
        {/* Sidebar */}
        <ExternalSidebar />

        {/* Main Content Area */}
        <div className="flex-1 flex flex-col overflow-hidden">
          {/* Navbar */}
          <ExternalNavbar />

          {/* Page Content */}
          <main className="flex-1 overflow-y-auto p-6">
            {children}
          </main>
        </div>
      </div>
    </AuthGuard>
  );
}

