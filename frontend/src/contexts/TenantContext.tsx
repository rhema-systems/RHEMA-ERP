'use client';

import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { apiService } from '../services/api.service';
import { authService } from '../services/auth';
import type { Tenant } from '../types';

interface TenantContextType {
  tenants: Tenant[];
  currentTenant: Tenant | null;
  currentTenantCode: string | null;
  setCurrentTenantCode: (code: string | null) => void;
  isLoadingTenants: boolean;
}

const TenantContext = createContext<TenantContextType | undefined>(undefined);

interface TenantProviderProps {
  children: ReactNode;
}

const resolveStoredTenantCode = (): string | null => {
  if (typeof window === 'undefined') {
    return null;
  }

  const storedCode = localStorage.getItem('currentTenantCode');
  if (storedCode) {
    return storedCode;
  }

  const storedTenantRaw = localStorage.getItem('currentTenant');
  if (storedTenantRaw) {
    try {
      const storedTenant = JSON.parse(storedTenantRaw) as Partial<Tenant> & { tenantCode?: string };
      const tenantCode = storedTenant.code || storedTenant.tenantCode;
      if (typeof tenantCode === 'string' && tenantCode) {
        return tenantCode;
      }
    } catch {
      // ignore malformed tenant cache
    }
  }

  const storedUserRaw = localStorage.getItem('user');
  if (storedUserRaw) {
    try {
      const storedUser = JSON.parse(storedUserRaw) as { currentTenantCode?: string | null };
      if (typeof storedUser.currentTenantCode === 'string' && storedUser.currentTenantCode) {
        return storedUser.currentTenantCode;
      }
    } catch {
      // ignore malformed user cache
    }
  }

  return null;
};

export function TenantProvider({ children }: TenantProviderProps) {
  const [currentTenantCode, setCurrentTenantCodeState] = useState<string | null>(null);
  const [hasHydratedTenantCode, setHasHydratedTenantCode] = useState(false);

  // Get all tenants - only if authenticated
  const { data: apiTenants = [], isLoading: isLoadingTenants, error: tenantsError } = useQuery({
    queryKey: ['tenants'],
    queryFn: () => apiService.getTenants(),
    staleTime: 30 * 60 * 1000, // 30 minutes
    gcTime: 60 * 60 * 1000, // 1 hour
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    retry: (failureCount, error: any) => {
      // Don't retry on 401 errors - user is not authenticated
      if (error?.message?.includes('401') || error?.message?.includes('Unauthorized')) {
        return false;
      }
      return failureCount < 2;
    },
    retryDelay: 1000,
    enabled: typeof window !== 'undefined' && authService.isAuthenticated(), // Only run if authenticated
  });

  // Convert API tenants to frontend format
  const tenants: Tenant[] = apiTenants.map(t => ({
    id: t.id,
    name: t.name,
    code: t.code,
    description: t.description,
    isActive: t.isActive,
    createdAt: t.createdAt || new Date().toISOString(),
    logoUrl: t.logoUrl
  }));

  const storedCurrentTenant = authService.getCurrentTenant();
  const currentTenant =
    tenants.find(t => t.code === currentTenantCode) ||
    (storedCurrentTenant?.code === currentTenantCode ? storedCurrentTenant : null);

  // Initialize from localStorage on mount
  useEffect(() => {
    if (typeof window !== 'undefined') {
      setCurrentTenantCodeState(resolveStoredTenantCode());
      setHasHydratedTenantCode(true);
    }
  }, []);

  useEffect(() => {
    if (!hasHydratedTenantCode || currentTenantCode) {
      return;
    }

    const fallbackCode = resolveStoredTenantCode();
    if (fallbackCode) {
      setCurrentTenantCodeState(fallbackCode);
    }
  }, [currentTenantCode, hasHydratedTenantCode]);

  // Listen for tenant changes from auth service
  useEffect(() => {
    if (typeof window === 'undefined') return;

    const resolveCode = (detail: any): string | null => {
      if (!detail) return null;
      if (typeof detail === 'string') return detail;
      if (typeof detail === 'object') {
        const code = detail.code || detail.tenantCode || detail.currentTenantCode || null;
        return typeof code === 'string' ? code : null;
      }
      return null;
    };

    const handleTenantChanged = (event: CustomEvent) => {
      const code = resolveCode(event.detail);
      if (code !== null) setCurrentTenantCode(code);
    };

    const handleTenantSelected = (event: CustomEvent) => {
      const code = resolveCode(event.detail);
      if (code !== null) setCurrentTenantCode(code);
    };

    window.addEventListener('tenant-changed', handleTenantChanged as EventListener);
    window.addEventListener('tenant-selected', handleTenantSelected as EventListener);
    return () => {
      window.removeEventListener('tenant-changed', handleTenantChanged as EventListener);
      window.removeEventListener('tenant-selected', handleTenantSelected as EventListener);
    };
  }, []);

  // Update localStorage when tenant code changes
  const setCurrentTenantCode = (code: string | null) => {
    setCurrentTenantCodeState(code);
    if (typeof window !== 'undefined') {
      if (code) {
        localStorage.setItem('currentTenantCode', code);
      } else {
        localStorage.removeItem('currentTenantCode');
      }
    }
  };

  // Log tenant loading errors
  useEffect(() => {
    if (tenantsError) {
      console.error('TenantContext: Error loading tenants:', tenantsError);
    }
  }, [tenantsError]);

  const value: TenantContextType = {
    tenants,
    currentTenant,
    currentTenantCode,
    setCurrentTenantCode,
    isLoadingTenants: !hasHydratedTenantCode || isLoadingTenants,
  };

  return <TenantContext.Provider value={value}>{children}</TenantContext.Provider>;
}

export function useTenant() {
  const context = useContext(TenantContext);
  if (context === undefined) {
    throw new Error('useTenant must be used within a TenantProvider');
  }
  return context;
}
