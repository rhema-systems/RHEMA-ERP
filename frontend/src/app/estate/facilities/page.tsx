'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  Briefcase,
  Building2,
  ClipboardCheck,
  Database,
  FileSignature,
  FileText,
  Loader2,
  MessageSquare,
  Wrench,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { estateFacilitiesService, type FacilitiesProcedure } from '@/services/estate-facilities.service';

const fallbackProcedures: FacilitiesProcedure[] = [
  {
    title: 'Property and Site Management',
    entityType: 'EstateFacilityPropertySite',
    source: 'Facilities Management ERP Module',
    summary: 'Site creation, property records, unit and space capture, occupancy status, and availability monitoring.',
    icon: 'Building2',
    stageCount: 5,
    accent: 'teal',
  },
  {
    title: 'Lease Management',
    entityType: 'EstateFacilityLease',
    source: 'Facilities Management ERP BRS',
    summary: 'Client details, lease dates, rental amount, renewal alerts, termination tracking, and lease status.',
    icon: 'FileSignature',
    stageCount: 7,
    accent: 'sky',
  },
  {
    title: 'Maintenance Management',
    entityType: 'EstateFacilityMaintenance',
    source: 'Facilities Management ERP Module',
    summary: 'Maintenance request, complaint logging, priority assignment, contractor assignment, completion, inspection, and closure.',
    icon: 'Wrench',
    stageCount: 8,
    accent: 'amber',
  },
  {
    title: 'Complaint Management',
    entityType: 'EstateFacilityComplaint',
    source: 'Facilities Management ERP BRS',
    summary: 'Tenant/customer complaints, issue tracking, escalation, resolution updates, and closure reporting.',
    icon: 'MessageSquare',
    stageCount: 6,
    accent: 'rose',
  },
  {
    title: 'Service Provider Management',
    entityType: 'EstateFacilityServiceProvider',
    source: 'Facilities Management ERP BRS',
    summary: 'Contractor database, service categories, contracts, rates, performance records, and invoice history.',
    icon: 'Briefcase',
    stageCount: 6,
    accent: 'violet',
  },
  {
    title: 'Staff and Cleaner Management',
    entityType: 'EstateFacilityStaffCleaner',
    source: 'Facilities Management ERP Module',
    summary: 'Facilities staff and cleaner assignment, duty monitoring, attendance support, and supervision workflows.',
    icon: 'ClipboardCheck',
    stageCount: 5,
    accent: 'emerald',
  },
  {
    title: 'Asset Register',
    entityType: 'EstateFacilityAssetRegister',
    source: 'Facilities Management ERP BRS',
    summary: 'Asset number, description, location, cost, warranty, maintenance history, and current status register.',
    icon: 'Database',
    stageCount: 6,
    accent: 'indigo',
  },
  {
    title: 'Facilities Document Control',
    entityType: 'EstateFacilityDocument',
    source: 'Facilities Management ERP BRS',
    summary: 'Upload, storage, search, and retrieval of leases, contracts, certificates, invoices, payment records, and property documents.',
    icon: 'FileText',
    stageCount: 5,
    accent: 'lime',
  },
];

const procedureIcons: Record<string, React.ComponentType<{ className?: string }>> = {
  Briefcase,
  Building2,
  ClipboardCheck,
  Database,
  FileSignature,
  FileText,
  MessageSquare,
  Wrench,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  indigo: 'text-indigo-700 dark:text-indigo-300',
  lime: 'text-lime-700 dark:text-lime-300',
  rose: 'text-rose-700 dark:text-rose-300',
  sky: 'text-sky-700 dark:text-sky-300',
  teal: 'text-teal-700 dark:text-teal-300',
  violet: 'text-violet-700 dark:text-violet-300',
};

export default function EstateFacilitiesPage() {
  const router = useRouter();
  const [procedures, setProcedures] = React.useState<FacilitiesProcedure[]>(fallbackProcedures);
  const [isLoading, setIsLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;

    const loadProcedures = async () => {
      try {
        const data = await estateFacilitiesService.getProcedures();
        if (mounted && data.length > 0) {
          setProcedures(data);
        }
      } catch {
        if (mounted) {
          setProcedures(fallbackProcedures);
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
    router.push(`/estate/facilities/${encodeURIComponent(entityType)}`);
  };

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="space-y-2">
            <Badge variant="outline" className="w-fit">
              Estate facilities
            </Badge>
            <div>
              <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
                Facilities Management
              </h1>
              <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
                Estate-owned facilities operations for sites, leases, maintenance, complaints, providers, field teams, assets, and documents.
              </p>
            </div>
          </div>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {isLoading ? (
            <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
              <CardContent className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading facilities procedures
              </CardContent>
            </Card>
          ) : null}

          {!isLoading && procedures.map((procedure) => {
            const Icon = procedureIcons[procedure.icon] || FileText;
            const accent = accentClasses[procedure.accent] || accentClasses.teal;

            return (
              <Card key={procedure.entityType} className="border-border bg-card text-card-foreground">
                <CardHeader className="space-y-3">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                      <Icon className={`h-5 w-5 ${accent}`} />
                    </div>
                    <Badge variant="secondary">{procedure.stageCount} stages</Badge>
                  </div>
                  <div>
                    <CardTitle className="text-base leading-6">{procedure.title}</CardTitle>
                    <CardDescription className="mt-1">{procedure.source}</CardDescription>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <p className="text-sm leading-6 text-muted-foreground">{procedure.summary}</p>
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{procedure.entityType}</Badge>
                    <Badge variant="outline">Procedure workspace</Badge>
                  </div>
                  <Button variant="outline" className="w-full justify-between" onClick={() => openWorkspace(procedure.entityType)}>
                    Open workspace
                    <ArrowRight className="h-4 w-4" />
                  </Button>
                </CardContent>
              </Card>
            );
          })}
        </div>
      </div>
    </div>
  );
}
