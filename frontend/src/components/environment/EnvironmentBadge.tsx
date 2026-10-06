'use client';

import React from 'react';
import { AlertTriangle } from 'lucide-react';

import { cn } from '../../lib/utils';
import { useApplicationEnvironment } from '../../contexts/ApplicationEnvironmentContext';
import { ENVIRONMENT_PRESENTATION } from './environment-presentation';

interface EnvironmentBadgeProps {
  className?: string;
  hideProduction?: boolean;
  compact?: boolean;
}

export function EnvironmentBadge({
  className,
  hideProduction = false,
  compact = false,
}: EnvironmentBadgeProps) {
  const { environment } = useApplicationEnvironment();
  if (hideProduction && environment.isProduction) return null;

  const presentation = ENVIRONMENT_PRESENTATION[environment.environment];
  return (
    <span
      data-testid="environment-badge"
      data-environment={environment.environment}
      aria-label={`Environment: ${presentation.label}`}
      title={environment.message}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border font-semibold uppercase tracking-[0.08em]',
        compact ? 'px-2 py-0.5 text-[10px]' : 'px-2.5 py-1 text-[11px]',
        presentation.badgeClassName,
        className,
      )}
    >
      {environment.environment === 'Unknown' && <AlertTriangle aria-hidden="true" className="h-3.5 w-3.5" />}
      {presentation.label}
    </span>
  );
}
