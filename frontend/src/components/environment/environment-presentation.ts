import type { ApplicationEnvironmentName } from '../../services/application-environment';

export interface EnvironmentPresentation {
  label: string;
  badgeClassName: string;
  bannerClassName: string;
}

export const ENVIRONMENT_PRESENTATION: Record<ApplicationEnvironmentName, EnvironmentPresentation> = {
  Production: {
    label: 'Production',
    badgeClassName: 'border-slate-300 bg-slate-100 text-slate-700 dark:border-neutral-600 dark:bg-neutral-800 dark:text-neutral-200',
    bannerClassName: 'border-slate-300 bg-slate-100 text-slate-800 dark:border-neutral-600 dark:bg-neutral-800 dark:text-neutral-100',
  },
  Test: {
    label: 'Test Environment',
    badgeClassName: 'border-blue-300 bg-blue-100 text-blue-900 dark:border-blue-600 dark:bg-blue-950 dark:text-blue-100',
    bannerClassName: 'border-blue-400 bg-blue-700 text-white dark:border-blue-500 dark:bg-blue-900',
  },
  UAT: {
    label: 'UAT Environment',
    badgeClassName: 'border-amber-300 bg-amber-100 text-amber-950 dark:border-amber-600 dark:bg-amber-950 dark:text-amber-100',
    bannerClassName: 'border-amber-400 bg-amber-500 text-amber-950 dark:border-amber-500 dark:bg-amber-700 dark:text-white',
  },
  Staging: {
    label: 'Staging Environment',
    badgeClassName: 'border-orange-300 bg-orange-100 text-orange-950 dark:border-orange-600 dark:bg-orange-950 dark:text-orange-100',
    bannerClassName: 'border-orange-400 bg-orange-600 text-white dark:border-orange-500 dark:bg-orange-800',
  },
  Development: {
    label: 'Development',
    badgeClassName: 'border-indigo-300 bg-indigo-100 text-indigo-950 dark:border-indigo-600 dark:bg-indigo-950 dark:text-indigo-100',
    bannerClassName: 'border-indigo-400 bg-indigo-700 text-white dark:border-indigo-500 dark:bg-indigo-900',
  },
  Unknown: {
    label: 'Unknown Environment',
    badgeClassName: 'border-red-400 bg-red-100 text-red-950 dark:border-red-500 dark:bg-red-950 dark:text-red-100',
    bannerClassName: 'border-red-400 bg-red-700 text-white dark:border-red-500 dark:bg-red-900',
  },
};
