'use client';

import React from 'react';
import { Activity, CalendarClock, CheckCircle2, GitCommitHorizontal, Server, ShieldAlert } from 'lucide-react';

import { EnvironmentBadge } from '../../../components/environment/EnvironmentBadge';
import { useApplicationEnvironment } from '../../../contexts/ApplicationEnvironmentContext';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../../components/ui/card';

const formatDeploymentDate = (value: string | null) => {
  if (!value) return 'Not supplied';
  const parsed = new Date(value);
  return Number.isNaN(parsed.valueOf()) ? 'Not supplied' : parsed.toLocaleString();
};

export default function SystemInformationPage() {
  const { environment, isLoading, isError } = useApplicationEnvironment();
  const healthyConfiguration = environment.configurationValid && !isError;

  const details = [
    { label: 'Application version', value: environment.applicationVersion, icon: Activity },
    { label: 'Build / commit', value: environment.buildId ?? 'Not supplied', icon: GitCommitHorizontal },
    { label: 'Deployment date', value: formatDeploymentDate(environment.deployedAtUtc), icon: CalendarClock },
  ];

  return (
    <div className="space-y-6" data-testid="system-information-page">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-slate-950 dark:text-white">System Information</h1>
          <p className="mt-1 text-sm text-slate-600 dark:text-neutral-400">
            Safe deployment identity for support, diagnostics, and environment verification.
          </p>
        </div>
        <EnvironmentBadge />
      </div>

      <Card className={healthyConfiguration ? '' : 'border-red-300 dark:border-red-700'}>
        <CardHeader>
          <div className="flex items-center gap-3">
            {healthyConfiguration
              ? <CheckCircle2 className="h-5 w-5 text-emerald-600" />
              : <ShieldAlert className="h-5 w-5 text-red-600" />}
            <div>
              <CardTitle>Environment identity</CardTitle>
              <CardDescription>
                {isLoading ? 'Loading authoritative server configuration...' : environment.message}
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {details.map(({ label, value, icon: Icon }) => (
            <div key={label} className="rounded-xl border border-slate-200 bg-slate-50 p-4 dark:border-neutral-700 dark:bg-neutral-900/60">
              <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-neutral-400">
                <Icon className="h-4 w-4" />
                {label}
              </div>
              <p className="mt-2 break-words font-medium text-slate-950 dark:text-white">{value}</p>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Server className="h-5 w-5 text-blue-600" />
            <CardTitle>Data isolation status</CardTitle>
          </div>
          <CardDescription>
            This status is explicitly supplied by deployment configuration. It is never inferred from the browser address.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-slate-700 dark:text-neutral-300">
            {environment.isProduction
              ? 'Production is the live application environment.'
              : environment.dataIsolationConfirmed
                ? 'Deployment configuration confirms that this environment is isolated from Production data.'
                : 'Production data isolation has not been confirmed. Verify database, storage, messaging, and external integration targets before testing.'}
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
