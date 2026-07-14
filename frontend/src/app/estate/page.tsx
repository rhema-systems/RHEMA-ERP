'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import {
  ArrowRight,
  ArrowRightLeft,
  BadgeCheck,
  BarChart3,
  Building2,
  ClipboardList,
  Database,
  FileCheck2,
  FilePenLine,
  FileSignature,
  FileText,
  Home,
  Landmark,
  Loader2,
  MapPin,
  RefreshCw,
  Search,
  Trees,
  Users,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth } from '@/hooks/use-auth';
import { estateProcedureService, type EstateProcedure } from '@/services/estate-procedure.service';

const fallbackProcedures: EstateProcedure[] = [
  {
    title: 'Secretarial and Estates Registry',
    entityType: 'EstateRegistrySecretariat',
    source: 'Estates Operational Manual',
    summary: 'Incoming files, letters, forms purchase, file dispatch, movement tracing, typing, client updates, and departmental registry controls.',
    icon: 'ClipboardList',
    stageCount: 7,
    accent: 'slate',
  },
  {
    title: 'Estate Records Management',
    entityType: 'EstateRecordsManagement',
    source: 'Estates Operational Manual',
    summary: 'Estate registers, HOS ledger cards, record updates, transfer amendments, agency notifications, and building permit ownership verification.',
    icon: 'Database',
    stageCount: 8,
    accent: 'indigo',
  },
  {
    title: 'Land and Landed Property Inspection',
    entityType: 'EstateInspection',
    source: 'Estates Operational Manual',
    summary: 'Site inspection, site report preparation, neighbourhood details, current development capture, and photographic evidence.',
    icon: 'MapPin',
    stageCount: 5,
    accent: 'teal',
  },
  {
    title: 'Search Application',
    entityType: 'EstateSearchApplication',
    source: 'Estates Operational Manual',
    summary: 'Search request intake, ground-rent arrears control, property-file review, and search report preparation.',
    icon: 'Search',
    stageCount: 5,
    accent: 'sky',
  },
  {
    title: 'Change of Address and Record Amendment',
    entityType: 'EstateRecordAmendment',
    source: 'Estates Operational Manual',
    summary: 'Address updates, statutory declaration support, arrears checks, and Revenue and Estate Records amendment routing.',
    icon: 'FilePenLine',
    stageCount: 5,
    accent: 'cyan',
  },
  {
    title: 'Certified True Copies',
    entityType: 'EstateCertifiedTrueCopy',
    source: 'Estates Operational Manual',
    summary: 'Certified copy request, arrears verification, fee/payment confirmation, document preparation, and certification routing.',
    icon: 'FileCheck2',
    stageCount: 5,
    accent: 'emerald',
  },
  {
    title: 'Joint Ownership / Addition of Name',
    entityType: 'EstateJointOwnership',
    source: 'Estates Operational Manual',
    summary: 'Additional-name requests, lease checks, cadastral plan routing, deed of variation, registration, detachment, and records amendment.',
    icon: 'Users',
    stageCount: 7,
    accent: 'violet',
  },
  {
    title: 'Transfer / Portion Transfer of Plot',
    entityType: 'EstateTransfer',
    source: 'Estates Operational Manual',
    summary: 'Transfer request processing, fee calculation, HOE and MD approval, Legal routing, completion, and records amendment.',
    icon: 'ArrowRightLeft',
    stageCount: 7,
    accent: 'blue',
  },
  {
    title: 'Assignment',
    entityType: 'EstateAssignment',
    source: 'Estates Operational Manual',
    summary: 'Consent to assign, draft deed review, arrears and development checks, Legal routing, registration, detachment, and completion notices.',
    icon: 'FileSignature',
    stageCount: 7,
    accent: 'purple',
  },
  {
    title: 'Lease Preparation',
    entityType: 'EstateLeasePreparation',
    source: 'Estates Operational Manual',
    summary: 'Lease request processing, substantial development checks, cadastral requirements, invoice/payment routing, Legal preparation, and records update.',
    icon: 'FileText',
    stageCount: 8,
    accent: 'amber',
  },
  {
    title: 'Lease Surrender and Renewal',
    entityType: 'EstateLeaseRenewal',
    source: 'Estates Operational Manual',
    summary: 'Renewal requirements, surrender option processing, term threshold checks, committee review, invoicing, approval, and lease renewal close-out.',
    icon: 'RefreshCw',
    stageCount: 8,
    accent: 'lime',
  },
  {
    title: 'Serviced Plots and HOS Allocation',
    entityType: 'EstateServicedPlotAllocation',
    source: 'Estates Operational Manual',
    summary: 'HOS unit allocation, serviced plot allocation, payment book updates, Offer Letters, Right of Entry letters, and quarterly reporting.',
    icon: 'Landmark',
    stageCount: 8,
    accent: 'green',
  },
  {
    title: 'Lands / Partially Serviced Schedule',
    entityType: 'EstateLandsPartiallyServiced',
    source: 'Estates Operational Manual',
    summary: 'Application intake, proposal letters, Land Management Fee and ground rent determination, Offer and Right of Entry preparation, and reports.',
    icon: 'Home',
    stageCount: 7,
    accent: 'orange',
  },
  {
    title: 'Housing and Home Ownership Scheme',
    entityType: 'EstateHousingHomeOwnership',
    source: 'Estates Questionnaire Response',
    summary: 'Recognition of tenancy, rental-to-HOS conversion, purchase completion, Offer Letter preparation, lease request, and records update.',
    icon: 'Building2',
    stageCount: 7,
    accent: 'rose',
  },
  {
    title: 'Traditional Lands',
    entityType: 'EstateTraditionalLands',
    source: 'Estates Operational Manual',
    summary: 'Traditional lands proposal processing, fee determination, allocation review, Offer and Right of Entry preparation, and quarterly reporting.',
    icon: 'Trees',
    stageCount: 6,
    accent: 'emerald',
  },
  {
    title: 'Tenancy Regularisation',
    entityType: 'EstateTenancyRegularisation',
    source: 'Estates Operational Manual',
    summary: 'Regularisation communities, tenancy validation, documentation, fee/payment checks, approvals, and records amendment.',
    icon: 'BadgeCheck',
    stageCount: 7,
    accent: 'yellow',
  },
  {
    title: 'Reporting and Controls',
    entityType: 'EstateReportingControls',
    source: 'Estates Questionnaire Response',
    summary: 'Quarterly productivity reports, rent roll, debtor lists, allocation reports, approval thresholds, segregation controls, audit trail, and policy enforcement.',
    icon: 'BarChart3',
    stageCount: 6,
    accent: 'fuchsia',
  },
];

const procedureIcons: Record<string, React.ComponentType<{ className?: string }>> = {
  ArrowRightLeft,
  BadgeCheck,
  BarChart3,
  Building2,
  ClipboardList,
  Database,
  FileCheck2,
  FilePenLine,
  FileSignature,
  FileText,
  Home,
  Landmark,
  MapPin,
  RefreshCw,
  Search,
  Trees,
  Users,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  blue: 'text-blue-700 dark:text-blue-300',
  cyan: 'text-cyan-700 dark:text-cyan-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  fuchsia: 'text-fuchsia-700 dark:text-fuchsia-300',
  green: 'text-green-700 dark:text-green-300',
  indigo: 'text-indigo-700 dark:text-indigo-300',
  lime: 'text-lime-700 dark:text-lime-300',
  orange: 'text-orange-700 dark:text-orange-300',
  purple: 'text-purple-700 dark:text-purple-300',
  rose: 'text-rose-700 dark:text-rose-300',
  sky: 'text-sky-700 dark:text-sky-300',
  slate: 'text-slate-700 dark:text-slate-200',
  teal: 'text-teal-700 dark:text-teal-300',
  violet: 'text-violet-700 dark:text-violet-300',
  yellow: 'text-yellow-700 dark:text-yellow-300',
};

const adminRoles = ['admin', 'SystemAdmin', 'SuperAdmin', 'TenantAdmin', 'WorkflowAdmin'];

export default function EstateProceduresPage() {
  const router = useRouter();
  const { hasAnyRole } = useAuth();
  const canManageWorkflows = hasAnyRole(adminRoles);
  const [procedures, setProcedures] = React.useState<EstateProcedure[]>(fallbackProcedures);
  const [isLoading, setIsLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;

    const loadProcedures = async () => {
      try {
        const data = await estateProcedureService.getProcedures();
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

  const openWorkflow = (entityType: string) => {
    router.push(`/administration/workflow?entityType=${entityType}`);
  };

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="space-y-2">
            <Badge variant="outline" className="w-fit">
              Estate operations
            </Badge>
            <div>
              <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
                Estate Procedures
              </h1>
              <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
                Operational workflows for registry, records, land services, leases, allocations, housing, traditional lands, regularisation, and controls.
              </p>
            </div>
          </div>
          {canManageWorkflows ? (
            <Button onClick={() => router.push('/administration/workflow?q=Estate')}>
              Workflow Admin
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        ) : null}
      </div>

        <div className="grid gap-4 md:grid-cols-2">
          <Card className="border-border bg-card text-card-foreground">
            <CardContent className="flex items-center justify-between gap-4 p-5">
              <div className="flex items-start gap-4">
                <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                  <Landmark className="h-5 w-5 text-teal-700 dark:text-teal-300" />
                </div>
                <div>
                  <h2 className="text-base font-semibold">Land Acquisition</h2>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Run acquisition stages through the configured workflow and capture cadastral demarcation.
                  </p>
                </div>
              </div>
              <Button variant="outline" size="icon" onClick={() => router.push('/estate/land-acquisition')} aria-label="Open land acquisition">
                <ArrowRight className="h-4 w-4" />
              </Button>
            </CardContent>
          </Card>

          <Card className="border-border bg-card text-card-foreground">
            <CardContent className="flex items-center justify-between gap-4 p-5">
              <div className="flex items-start gap-4">
                <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                  <MapPin className="h-5 w-5 text-teal-700 dark:text-teal-300" />
                </div>
                <div>
                  <h2 className="text-base font-semibold">Land Management</h2>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Review demarcated land bank records before project management pulls them for planning.
                  </p>
                </div>
              </div>
              <Button variant="outline" size="icon" onClick={() => router.push('/estate/land-management')} aria-label="Open land management">
                <ArrowRight className="h-4 w-4" />
              </Button>
            </CardContent>
          </Card>
        </div>

        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {isLoading ? (
            <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
              <CardContent className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading estate procedures
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
                    <Badge variant="outline">Roles and assignments</Badge>
                  </div>
                  {canManageWorkflows ? (
                    <Button variant="outline" className="w-full justify-between" onClick={() => openWorkflow(procedure.entityType)}>
                      Configure workflow
                      <ArrowRight className="h-4 w-4" />
                    </Button>
                  ) : null}
                </CardContent>
              </Card>
            );
          })}
        </div>
      </div>
    </div>
  );
}
