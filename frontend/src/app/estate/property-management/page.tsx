'use client';

import React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  BarChart3,
  Building2,
  CreditCard,
  Images,
  FileSignature,
  FileText,
  Home,
  KeyRound,
  Loader2,
  Users,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { estatePropertyManagementService } from '@/services/estate-property-management.service';
import type { FacilitiesProcedure } from '@/services/estate-facilities.service';

const procedureIcons: Record<
  string,
  React.ComponentType<{ className?: string }>
> = {
  Building2,
  CreditCard,
  FileSignature,
  FileText,
  Home,
  KeyRound,
  Users,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  cyan: 'text-cyan-700 dark:text-cyan-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  lime: 'text-lime-700 dark:text-lime-300',
  sky: 'text-sky-700 dark:text-sky-300',
  teal: 'text-teal-700 dark:text-teal-300',
  violet: 'text-violet-700 dark:text-violet-300',
};

export default function EstatePropertyManagementPage() {
  const router = useRouter();
  const [procedures, setProcedures] = React.useState<FacilitiesProcedure[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const loadProcedures = async () => {
      try {
        const data = await estatePropertyManagementService.getProcedures();
        if (mounted) {
          setProcedures(data);
          setLoadError(
            data.length === 0
              ? 'No property management workspaces were returned by the API.'
              : null
          );
        }
      } catch {
        if (mounted) {
          setProcedures([]);
          setLoadError(
            'Unable to load property management workspaces from the API.'
          );
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void loadProcedures();

    return () => {
      mounted = false;
    };
  }, []);

  const openWorkspace = (entityType: string) => {
    router.push(
      `/estate/property-management/${encodeURIComponent(entityType)}`
    );
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Estate property management
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              Property Management
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
              Property and commercial lifecycle for received units, tenants,
              occupants, leases, availability, and Finance AR handoffs.
            </p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild>
            <Link href="/estate/property-management/dashboard">
              <BarChart3 className="mr-2 h-4 w-4" />
              Dashboard
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/estate/property-management/listings">
              <Images className="mr-2 h-4 w-4" />
              Portal Listings
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/estate/facilities">
              <Building2 className="mr-2 h-4 w-4" />
              Facilities Management
            </Link>
          </Button>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {!isLoading && loadError ? (
          <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
            <CardContent className="py-12 text-center text-sm text-muted-foreground">
              {loadError}
            </CardContent>
          </Card>
        ) : null}
        {isLoading ? (
          <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
            <CardContent className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading property management workspaces
            </CardContent>
          </Card>
        ) : null}

        {!isLoading &&
          procedures.map((procedure) => {
            const Icon = procedureIcons[procedure.icon] || Building2;
            const accent =
              accentClasses[procedure.accent] || accentClasses.teal;
            const isPropertyUnitRegister =
              procedure.entityType === 'EstatePropertyManagementPropertyUnit';

            return (
              <Card
                key={procedure.entityType}
                className="border-border bg-card text-card-foreground"
              >
                <CardHeader className="space-y-3">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                      <Icon className={`h-5 w-5 ${accent}`} />
                    </div>
                    <Badge variant="secondary">
                      {isPropertyUnitRegister
                        ? 'Estate records'
                        : `${procedure.stageCount} stages`}
                    </Badge>
                  </div>
                  <div>
                    <CardTitle className="text-base leading-6">
                      {procedure.title}
                    </CardTitle>
                    <CardDescription className="mt-1">
                      {procedure.source}
                    </CardDescription>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{procedure.entityType}</Badge>
                    <Badge variant="outline">Property workspace</Badge>
                  </div>
                  <Button
                    variant="outline"
                    className="w-full justify-between"
                    onClick={() => openWorkspace(procedure.entityType)}
                  >
                    {isPropertyUnitRegister
                      ? 'Open register'
                      : 'Open workspace'}
                    <ArrowRight className="h-4 w-4" />
                  </Button>
                </CardContent>
              </Card>
            );
          })}
      </div>
    </div>
  );
}
