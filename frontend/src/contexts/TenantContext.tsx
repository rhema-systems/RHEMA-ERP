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

export function TenantProvider({ children }: TenantProviderProps) {
  const [currentTenantCode, setCurrentTenantCodeState] = useState<string | null>(null);

  // Get all tenants - only if authenticated
  const { data: apiTenants = [], isLoading: isLoadingTenants, error: tenantsError } = useQuery({
    queryKey: ['tenants'],
    queryFn: async () => {
      console.log('TenantContext: Calling getTenants API...');
      const result = await apiService.getTenants();
      console.log('TenantContext: API response:', result);
      return result;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    retry: (failureCount, error: any) => {
      console.log('TenantContext: Query failed, retry attempt:', failureCount, error);
      // Don't retry on 401 errors - user is not authenticated
      if (error?.message?.includes('401') || error?.message?.includes('Unauthorized')) {
        console.log('TenantContext: 401 error detected, stopping retries');
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

  // Get current tenant based on stored code
  const currentTenant = tenants.find(t => t.code === currentTenantCode) || null;

  // Initialize from localStorage on mount
  useEffect(() => {
    if (typeof window !== 'undefined') {
      const storedCode = localStorage.getItem('currentTenantCode');
      setCurrentTenantCodeState(storedCode);
    }
  }, []);

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
      console.log('TenantContext: Received tenant-changed event:', event.detail, '=>', code);
      if (code !== null) setCurrentTenantCode(code);
    };

    const handleTenantSelected = (event: CustomEvent) => {
      const code = resolveCode(event.detail);
      console.log('TenantContext: Received tenant-selected event:', event.detail, '=>', code);
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
    console.log('TenantContext: Setting tenant code:', code);
    setCurrentTenantCodeState(code);
    if (typeof window !== 'undefined') {
      if (code) {
        localStorage.setItem('currentTenantCode', code);
      } else {
        localStorage.removeItem('currentTenantCode');
      }
    }
  };

  // Debug logging for tenant state changes
  useEffect(() => {
    console.log('TenantContext state:', {
      currentTenantCode,
      currentTenant: currentTenant?.name,
      tenantCount: tenants.length,
      isLoadingTenants,
      error: tenantsError?.message
    });
  }, [currentTenantCode, currentTenant, tenants, isLoadingTenants, tenantsError]);

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
    isLoadingTenants,
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
