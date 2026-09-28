'use client';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { useState } from 'react';

export function ReactQueryProvider({ children }: { children: React.ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 5 * 60 * 1000, // 5 minutes
            gcTime: 30 * 60 * 1000, // 30 minutes
            refetchOnWindowFocus: false,
            refetchOnReconnect: false,
            retry: (failureCount, error: any) => {
              // Don't retry on 4xx errors. ⚠ `apiService` puts the HTTP status on `error.status`
              // and the parsed body on `error.response`, so `error.response.status` was always
              // undefined and every 400/403/404 was retried three times before the screen heard
              // about it (round 5 lane E1b). Both places are read, for any caller that differs.
              const status = error?.status ?? error?.response?.status;
              if (status >= 400 && status < 500) {
                return false;
              }
              return failureCount < 3;
            },
          },
          mutations: {
            retry: false,
          },
        },
      })
  );

  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
