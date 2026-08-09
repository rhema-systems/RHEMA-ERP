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
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { useAuth } from '@/hooks/use-auth';
import {
  estateProcedureService,
  type EstateProcedure,
} from '@/services/estate-procedure.service';
import {
  documentManagementService,
  type CentralDocumentRecord,
} from '@/services/document-management.service';
import {
  estateLandManagementService,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import {
  procedureCaseService,
  type ProcedureCaseSummary,
} from '@/services/procedure-case.service';
import {
  getProcedureStageLabel,
  getProcedureWorkspaceActionLabel,
} from '@/lib/procedure-workspace';

const procedureIcons: Record<
  string,
  React.ComponentType<{ className?: string }>
> = {
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

const adminRoles = [
  'admin',
  'SystemAdmin',
  'SuperAdmin',
  'TenantAdmin',
  'WorkflowAdmin',
];

const crossModuleFlows = [
  {
    title: 'Land Bank To Project',
    icon: ArrowRightLeft,
  },
  {
    title: 'Project To Property',
    icon: Home,
  },
  {
    title: 'Facilities To Existing Modules',
    icon: Building2,
  },
];

export default function EstateOperationsPage() {
  const router = useRouter();
  const { hasAnyRole } = useAuth();
  const canManageWorkflows = hasAnyRole(adminRoles);
  const [procedures, setProcedures] =
    React.useState<EstateProcedure[]>([]);
  const [estateCases, setEstateCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [propertyCases, setPropertyCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [facilitiesCases, setFacilitiesCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [landBank, setLandBank] = React.useState<EstateManagedAsset[]>([]);
  const [dmsRecords, setDmsRecords] = React.useState<CentralDocumentRecord[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;

    const loadEstateOperations = async () => {
      const [procedureResult, estateCaseResult, propertyCaseResult, facilitiesCaseResult, landBankResult, dmsResult] =
        await Promise.allSettled([
          estateProcedureService.getProcedures(),
          procedureCaseService.listModuleCases('Estate'),
          procedureCaseService.listModuleCases('PropertyManagement'),
          procedureCaseService.listModuleCases('Facilities'),
          estateLandManagementService.getLandBank(),
          documentManagementService.getRecords(),
        ]);

      if (!mounted) {
        return;
      }

      setProcedures(
        procedureResult.status === 'fulfilled' ? procedureResult.value : []
      );
      setEstateCases(estateCaseResult.status === 'fulfilled' ? estateCaseResult.value : []);
      setPropertyCases(propertyCaseResult.status === 'fulfilled' ? propertyCaseResult.value : []);
      setFacilitiesCases(facilitiesCaseResult.status === 'fulfilled' ? facilitiesCaseResult.value : []);
      setLandBank(landBankResult.status === 'fulfilled' ? landBankResult.value : []);
      setDmsRecords(dmsResult.status === 'fulfilled' ? dmsResult.value : []);
      setIsLoading(false);
    };

    void loadEstateOperations();

    return () => {
      mounted = false;
    };
  }, []);

  const openWorkspace = (entityType: string) => {
    router.push(`/estate/${encodeURIComponent(entityType)}`);
  };

  const estateDmsRecords = React.useMemo(
    () =>
      dmsRecords.filter(
        (record) =>
          record.sourceModule.toLowerCase().startsWith('estate') ||
          record.sourceLabel.toLowerCase().includes('source: estate')
      ),
    [dmsRecords]
  );

  const openEstateCases = estateCases.filter((item) => item.status !== 'Completed');
  const openPropertyCases = propertyCases.filter((item) => item.status !== 'Completed');
  const openFacilitiesCases = facilitiesCases.filter((item) => item.status !== 'Completed');
  const landReadyForProject = landBank.filter((item) => item.isReadyForProjectManagement);
  const dmsMetadataComplete = estateDmsRecords.filter(
    (record) =>
      record.metadataCompleteness?.status === 'Complete' ||
      (record.metadataCompleteness?.percentage ?? 0) >= 100
  );
  const dmsMetadataPercent =
    estateDmsRecords.length > 0
      ? Math.round((dmsMetadataComplete.length / estateDmsRecords.length) * 100)
      : 0;

  const estateAnalytics = [
    {
      label: 'Land Bank',
      value: landBank.length.toString(),
      icon: Landmark,
    },
    {
      label: 'Estate Open Work',
      value: openEstateCases.length.toString(),
      icon: ClipboardList,
    },
    {
      label: 'Property Operations',
      value: openPropertyCases.length.toString(),
      icon: Home,
    },
    {
      label: 'Facilities Open Work',
      value: openFacilitiesCases.length.toString(),
      icon: Building2,
    },
    {
      label: 'DMS Records',
      value: estateDmsRecords.length.toString(),
      icon: FileText,
    },
  ];

  const workflowProcedures = procedures.filter(
    (procedure) =>
      procedure.workspaceType !== 'Register' &&
      procedure.workspaceType !== 'Dashboard / Report'
  );
  const workflowEntityTypes = new Set(
    workflowProcedures.map((procedure) => procedure.entityType)
  );
  const estateWorkflowItems = estateCases.filter((item) =>
    workflowEntityTypes.has(item.entityType)
  );

  const estateReadiness = [
    {
      label: 'Project Pull Readiness',
      value: landBank.length > 0 ? Math.round((landReadyForProject.length / landBank.length) * 100) : 0,
    },
    {
      label: 'Estate Workflow Coverage',
      value:
        workflowProcedures.length > 0
          ? Math.round(
              (new Set(estateWorkflowItems.map((item) => item.entityType)).size /
                workflowProcedures.length) *
                100
            )
          : 0,
    },
    {
      label: 'Estate Work Completion',
      value:
        estateCases.length > 0
          ? Math.round(
              (estateCases.filter((item) => item.status === 'Completed').length / estateCases.length) * 100
            )
          : 0,
    },
    {
      label: 'Document Metadata Health',
      value: dmsMetadataPercent,
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Estate operations
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              Estate Operations
            </h1>
          </div>
        </div>
        {canManageWorkflows ? (
          <Button
            onClick={() => router.push('/administration/workflow?q=Estate')}
          >
            Workflow Admin
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        ) : null}
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {estateAnalytics.map((item) => {
          const Icon = item.icon;
          return (
            <Card
              key={item.label}
              className="border-border bg-card text-card-foreground"
            >
              <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0 pb-2">
                <div>
                  <CardDescription>{item.label}</CardDescription>
                  <CardTitle className="mt-1 text-2xl">{item.value}</CardTitle>
                </div>
                <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                  <Icon className="h-5 w-5 text-primary" />
                </div>
              </CardHeader>
              <CardContent />
            </Card>
          );
        })}
      </div>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.15fr)_minmax(360px,0.85fr)]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <BarChart3 className="h-5 w-5 text-primary" />
              <CardTitle>Estate Analytics</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {estateReadiness.map((item) => (
              <div key={item.label} className="space-y-2">
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <div className="text-sm font-medium">{item.label}</div>
                  </div>
                  <Badge variant="secondary">{item.value}%</Badge>
                </div>
                <div className="h-2 rounded-full bg-muted">
                  <div
                    className="h-2 rounded-full bg-primary"
                    style={{ width: `${item.value}%` }}
                  />
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <ArrowRightLeft className="h-5 w-5 text-primary" />
              <CardTitle>Cross-Module Flow</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {crossModuleFlows.map((flow) => {
              const Icon = flow.icon;
              return (
                <div
                  key={flow.title}
                  className="flex items-start gap-3 rounded-md border bg-background p-4"
                >
                  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-md border bg-muted">
                    <Icon className="h-4 w-4 text-primary" />
                  </div>
                  <div>
                    <div className="font-medium">{flow.title}</div>
                  </div>
                </div>
              );
            })}
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="flex items-center justify-between gap-4 p-5">
            <div className="flex items-start gap-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                <BarChart3 className="h-5 w-5 text-sky-700 dark:text-sky-300" />
              </div>
              <div>
                <h2 className="text-base font-semibold">Property Dashboard</h2>
              </div>
            </div>
            <Button
              variant="outline"
              size="icon"
              onClick={() =>
                router.push('/estate/property-management/dashboard')
              }
              aria-label="Open property dashboard"
            >
              <ArrowRight className="h-4 w-4" />
            </Button>
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="flex items-center justify-between gap-4 p-5">
            <div className="flex items-start gap-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                <BarChart3 className="h-5 w-5 text-indigo-700 dark:text-indigo-300" />
              </div>
              <div>
                <h2 className="text-base font-semibold">
                  Facilities Dashboard
                </h2>
              </div>
            </div>
            <Button
              variant="outline"
              size="icon"
              onClick={() => router.push('/estate/facilities/dashboard')}
              aria-label="Open facilities dashboard"
            >
              <ArrowRight className="h-4 w-4" />
            </Button>
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="flex items-center justify-between gap-4 p-5">
            <div className="flex items-start gap-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                <Landmark className="h-5 w-5 text-teal-700 dark:text-teal-300" />
              </div>
              <div>
                <h2 className="text-base font-semibold">Land Acquisition</h2>
              </div>
            </div>
            <Button
              variant="outline"
              size="icon"
              onClick={() => router.push('/estate/land-acquisition')}
              aria-label="Open land acquisition"
            >
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
              </div>
            </div>
            <Button
              variant="outline"
              size="icon"
              onClick={() => router.push('/estate/land-management')}
              aria-label="Open land management"
            >
              <ArrowRight className="h-4 w-4" />
            </Button>
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="flex items-center justify-between gap-4 p-5">
            <div className="flex items-start gap-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                <Home className="h-5 w-5 text-sky-700 dark:text-sky-300" />
              </div>
              <div>
                <h2 className="text-base font-semibold">Property Management</h2>
              </div>
            </div>
            <Button
              variant="outline"
              size="icon"
              onClick={() => router.push('/estate/property-management')}
              aria-label="Open property management"
            >
              <ArrowRight className="h-4 w-4" />
            </Button>
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="flex items-center justify-between gap-4 p-5">
            <div className="flex items-start gap-4">
              <div className="flex h-10 w-10 items-center justify-center rounded-md border border-border bg-muted">
                <Building2 className="h-5 w-5 text-indigo-700 dark:text-indigo-300" />
              </div>
              <div>
                <h2 className="text-base font-semibold">
                  Facilities Management
                </h2>
              </div>
            </div>
            <Button
              variant="outline"
              size="icon"
              onClick={() => router.push('/estate/facilities')}
              aria-label="Open facilities management"
            >
              <ArrowRight className="h-4 w-4" />
            </Button>
          </CardContent>
        </Card>
      </div>

      <div>
        <h2 className="text-lg font-semibold">
          Estate Casework &amp; Registers
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          SOP case workflows, departmental registers, operational queues,
          inspection events, and reporting controls.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {isLoading ? (
            <Card className="border-border bg-card text-card-foreground sm:col-span-2 xl:col-span-3">
              <CardContent className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading estate operations
              </CardContent>
            </Card>
          ) : null}

          {!isLoading &&
            procedures.map((procedure) => {
              const Icon = procedureIcons[procedure.icon] || FileText;
              const accent =
                accentClasses[procedure.accent] || accentClasses.teal;
              const isWorkflowManagedRequest =
                procedure.entityType ===
                'EstatePropertyManagementListingApplication';

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
                        {isWorkflowManagedRequest
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
                      <Badge variant="outline">
                        {procedure.workspaceType ?? 'Case Workflow'}
                      </Badge>
                    </div>
                    <Button
                      variant="outline"
                      className="w-full justify-between"
                      onClick={() => openWorkspace(procedure.entityType)}
                    >
                      {getProcedureWorkspaceActionLabel(
                        procedure.workspaceType
                      )}
                      <ArrowRight className="h-4 w-4" />
                    </Button>
                    {canManageWorkflows ? (
                      <Button
                        variant="ghost"
                        className="w-full justify-between"
                        onClick={() =>
                          router.push(
                            `/administration/workflow?entityType=${procedure.entityType}`
                          )
                        }
                      >
                        Workflow setup
                        <ArrowRight className="h-4 w-4" />
                      </Button>
                    ) : null}
                  </CardContent>
                </Card>
              );
            })}
        </div>
    </div>
  );
}
