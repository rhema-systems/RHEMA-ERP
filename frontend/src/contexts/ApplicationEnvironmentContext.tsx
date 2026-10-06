'use client';

import React, { createContext, useContext, useEffect, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';

import {
  applicationEnvironmentService,
  type ApplicationEnvironmentName,
  type PublicApplicationEnvironment,
  UNKNOWN_APPLICATION_ENVIRONMENT,
} from '../services/application-environment';

interface ApplicationEnvironmentContextValue {
  environment: PublicApplicationEnvironment;
  isLoading: boolean;
  isError: boolean;
}

const ApplicationEnvironmentContext = createContext<ApplicationEnvironmentContextValue>({
  environment: UNKNOWN_APPLICATION_ENVIRONMENT,
  isLoading: true,
  isError: false,
});

export function getEnvironmentDocumentTitle(environment: ApplicationEnvironmentName): string {
  return environment === 'Production'
    ? 'RHEMA-ERP'
    : `RHEMA-ERP [${environment.toUpperCase()}]`;
}

export function ApplicationEnvironmentProvider({ children }: { children: ReactNode }) {
  const query = useQuery({
    queryKey: ['publicApplicationEnvironment'],
    queryFn: () => applicationEnvironmentService.getPublicEnvironment(),
    staleTime: Number.POSITIVE_INFINITY,
    gcTime: Number.POSITIVE_INFINITY,
    refetchOnMount: false,
    refetchOnReconnect: false,
    refetchOnWindowFocus: false,
    retry: 1,
  });
  const environment = query.data ?? UNKNOWN_APPLICATION_ENVIRONMENT;

  useEffect(() => {
    document.title = getEnvironmentDocumentTitle(environment.environment);
  }, [environment.environment]);

  return (
    <ApplicationEnvironmentContext.Provider
      value={{ environment, isLoading: query.isPending, isError: query.isError }}
    >
      {children}
    </ApplicationEnvironmentContext.Provider>
  );
}

export const useApplicationEnvironment = () => useContext(ApplicationEnvironmentContext);
