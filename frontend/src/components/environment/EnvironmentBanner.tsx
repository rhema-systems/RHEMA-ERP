'use client';

import React from 'react';
import { AlertTriangle, FlaskConical } from 'lucide-react';

import { cn } from '../../lib/utils';
import { useApplicationEnvironment } from '../../contexts/ApplicationEnvironmentContext';
import { ENVIRONMENT_PRESENTATION } from './environment-presentation';

export function EnvironmentBanner({ className }: { className?: string }) {
  const { environment } = useApplicationEnvironment();
  if (environment.isProduction) return null;

  const presentation = ENVIRONMENT_PRESENTATION[environment.environment];
  const Icon = environment.environment === 'Unknown' ? AlertTriangle : FlaskConical;

  return (
    <div
      data-testid="environment-banner"
      data-environment={environment.environment}
      role={environment.environment === 'Unknown' ? 'alert' : 'status'}
      aria-label={`${presentation.label}. ${environment.message}`}
      className={cn(
        'z-[60] flex min-h-8 shrink-0 items-center justify-center gap-2 border-b px-3 py-1 text-center text-xs font-semibold shadow-sm',
        presentation.bannerClassName,
        className,
      )}
    >
      <Icon aria-hidden="true" className="h-3.5 w-3.5 shrink-0" />
      <strong className="uppercase tracking-[0.08em]">{presentation.label}</strong>
      <span aria-hidden="true" className="hidden sm:inline">-</span>
      <span className="hidden font-medium sm:inline">{environment.message}</span>
    </div>
  );
}
