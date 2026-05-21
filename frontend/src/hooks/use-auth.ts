'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { authService } from '../services/auth';
import { QUERY_KEYS } from '../config/api';
import type { User, LoginRequest } from '../types';
import { hasAllPermissionsAccess, hasAnyPermissionAccess, hasPermissionAccess } from '../lib/permissions';
import { buildTenantSelectRedirectUrl } from '../lib/auth-redirect';
import { getExternalPortalPath, isExternalPortalUser } from '../lib/auth-routing';

export function useAuth() {
  const queryClient = useQueryClient();
  const router = useRouter();

  // Get current user query
  const {
    data: user,
    isLoading,
    error,
  } = useQuery<User>({
    queryKey: QUERY_KEYS.CURRENT_USER,
    queryFn: authService.getCurrentUser,
    enabled: typeof window !== 'undefined' && authService.isAuthenticated(),
    retry: (failureCount, error: any) => {
      // Don't retry on 401 errors
      if (error?.response?.status === 401) {
        return false;
      }
      return failureCount < 2;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
  });

  // Login mutation
  const loginMutation = useMutation({
    mutationFn: (credentials: LoginRequest) => authService.login(credentials),
    onSuccess: (response) => {
      // Update the user cache
      queryClient.setQueryData(QUERY_KEYS.CURRENT_USER, response.user);
      // Invalidate tenant query to ensure fresh data
      queryClient.invalidateQueries({ queryKey: QUERY_KEYS.TENANTS });

      router.push(
        isExternalPortalUser(response.user)
          ? getExternalPortalPath()
          : buildTenantSelectRedirectUrl()
      );
    },
    onError: (error) => {
      console.error('Login failed:', error);
    },
  });

  // Logout mutation
  const logoutMutation = useMutation({
    mutationFn: authService.logout,
    onSuccess: () => {
      // Clear all queries
      queryClient.clear();
      // Redirect to login
      router.push('/login');
    },
    onError: (error) => {
      console.error('Logout failed:', error);
      // Still redirect to login even if API call fails
      queryClient.clear();
      router.push('/login');
    },
  });

  // Helper functions
  const isAuthenticated = authService.isAuthenticated();
  const currentUser = user ?? authService.getStoredUser();
  const hasRole = (role: string) => currentUser?.roles?.includes(role) ?? false;
  const hasAnyRole = (roles: string[]) => roles.some(role => currentUser?.roles?.includes(role));
  const hasAllRoles = (roles: string[]) => roles.every(role => currentUser?.roles?.includes(role));
  const hasPermission = (permission: string) => hasPermissionAccess(currentUser, permission);
  const hasAnyPermission = (permissions: string[]) => hasAnyPermissionAccess(currentUser, permissions);
  const hasAllPermissions = (permissions: string[]) => hasAllPermissionsAccess(currentUser, permissions);

  return {
    // State
    user,
    isLoading,
    error,
    isAuthenticated,
    
    // Actions
    login: loginMutation.mutate,
    logout: logoutMutation.mutate,
    
    // Mutation states
    isLoggingIn: loginMutation.isPending,
    isLoggingOut: logoutMutation.isPending,
    loginError: loginMutation.error,
    logoutError: logoutMutation.error,
    
    // Authorization helpers
    hasRole,
    hasAnyRole,
    hasAllRoles,
    hasPermission,
    hasAnyPermission,
    hasAllPermissions,
  };
}
