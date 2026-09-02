'use client';

import React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  BarChart3,
  Briefcase,
  Building2,
  ClipboardCheck,
  CreditCard,
  Database,
  Images,
  FileSignature,
  FileText,
  Home,
  KeyRound,
  Landmark,
  Loader2,
  MessageSquare,
  Users,
  Wrench,
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
import {
  getProcedureStageLabel,
  getProcedureWorkspaceActionLabel,
} from '@/lib/procedure-workspace';

const procedureIcons: Record<
  string,
  React.ComponentType<{ className?: string }>
> = {
  Building2,
  Briefcase,
  ClipboardCheck,
  CreditCard,
  Database,
  FileSignature,
  FileText,
  Home,
  KeyRound,
  Landmark,
  MessageSquare,
  Users,
  Wrench,
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
        const propertyData = await estatePropertyManagementService.getProcedures();
        if (mounted) {
          setProcedures(propertyData);
          setLoadError(
            propertyData.length === 0
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

  const openWorkspace = (entityType: string) =>
    router.push(`/estate/property-management/${encodeURIComponent(entityType)}`);

  const renderProcedureCard = (
    procedure: FacilitiesProcedure
  ) => {
    const Icon = procedureIcons[procedure.icon] || Building2;
    const accent = accentClasses[procedure.accent] || accentClasses.teal;
    const isPropertyUnitRegister =
      procedure.entityType === 'EstatePropertyManagementPropertyUnit';
    const isLeaseManagement =
      procedure.entityType === 'EstatePropertyManagementLease';
    const isWorkflowManagedRequest =
      procedure.entityType === 'EstatePropertyManagementListingApplication';

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
                  : isLeaseManagement
                    ? 'Lease register'
                    : isWorkflowManagedRequest
                      ? 'Workflow managed'
                      : getProcedureStageLabel(
                          procedure.workspaceType,
                          procedure.stageCount
                        )}
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
            <Badge variant="outline">{procedure.workspaceType ?? 'Case Workflow'}</Badge>
          </div>
          <Button
            variant="outline"
            className="w-full justify-between"
            onClick={() => openWorkspace(procedure.entityType)}
          >
            {getProcedureWorkspaceActionLabel(procedure.workspaceType)}
            <ArrowRight className="h-4 w-4" />
          </Button>
        </CardContent>
      </Card>
    );
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Estate operations
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              Property Management Operations
            </h1>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
              Property and unit registers, listings, customer requests,
              leases, occupants, availability, handover, billing, and records.
            </p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild>
            <Link href="/estate/property-management/dashboard">
              <BarChart3 className="mr-2 h-4 w-4" />
              Property Dashboard
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/estate/property-management/listings">
              <Images className="mr-2 h-4 w-4" />
              Portal Listings
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/estate/facilities/dashboard">
              <Building2 className="mr-2 h-4 w-4" />
              Facilities & Corporate Services
            </Link>
          </Button>
        </div>
      </div>

      <Card className="border-border bg-card text-card-foreground">
        <CardContent className="p-4 text-sm text-muted-foreground">
          Estate casework owns applications, approvals, allocations, and lease
          preparation. This hub starts after an approved property or unit is
          handed over, then keeps lease, occupancy, billing, records,
          property records and customer transactions connected. Facilities and
          corporate service work is managed in its separate workspace.
        </CardContent>
      </Card>

      <div>
        <h2 className="text-lg font-semibold">Property, Lease & Customer Operations</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Use these for the property register, listings, requests, lease setup,
          occupants, billing, ground rent, handover, availability, and records.
        </p>
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
          procedures.map((procedure) => renderProcedureCard(procedure))}
      </div>
    </div>
  );
}
