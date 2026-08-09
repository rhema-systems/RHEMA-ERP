'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  ArrowRight,
  BarChart3,
  CreditCard,
  Database,
  FileText,
  Landmark,
  Loader2,
  Scale,
  Settings,
  Workflow,
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
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';
import { getProcedureStageLabel } from '@/lib/procedure-workspace';
import {
  estateProcedureService,
  findEstateProcedure,
  type EstateProcedure,
} from '@/services/estate-procedure.service';

type EstateSopControl = {
  title: string;
  description: string;
  href: string;
  action: string;
  icon: React.ComponentType<{ className?: string }>;
};

function getEstateSopControls(procedure: EstateProcedure): EstateSopControl[] {
  const encodedEntityType = encodeURIComponent(procedure.entityType);
  const controls: EstateSopControl[] = [
    {
      title: 'Configured Workflow',
      description:
        'Stages, documents, checklists, assignees, approvals, and outputs must come from Workflow Setup for this Estate SOP.',
      href: `/administration/workflow?entityType=${encodedEntityType}`,
      action: 'Configure workflow',
      icon: Workflow,
    },
    {
      title: 'Central DMS',
      description:
        'Publish source documents, final letters, search reports, signed instruments, file movement evidence, and record amendments into Central DMS.',
      href: `/document-management?module=Estate&entityType=${encodedEntityType}`,
      action: 'Open DMS',
      icon: FileText,
    },
    {
      title: 'Estate Records',
      description:
        'Keep property file reference, register changes, agency notifications, dispatch/return evidence, and amendment confirmation visible.',
      href: '/estate/EstateRecordsManagement',
      action: 'Open records',
      icon: Database,
    },
    {
      title: 'Finance / Revenue Check',
      description:
        'Use Finance for invoices, receipts, balances, arrears checks, deposits, fees, and cashier/payment references before SOP closeout.',
      href: `/finance/ar/invoices?source=Estate&entityType=${encodedEntityType}`,
      action: 'Open Finance AR',
      icon: CreditCard,
    },
    {
      title: 'Legal Handoff',
      description:
        'Route leases, assignments, transfers, mortgage consent, recognition, litigation, and executed instruments to Legal where required.',
      href: '/legal',
      action: 'Open Legal',
      icon: Scale,
    },
    {
      title: 'Central Reports',
      description:
        'Quarterly returns, rent roll, debtor lists, allocation activity, transfer/assignment activity, search output, and Board packs belong in central Reports.',
      href: '/reports?module=estate',
      action: 'Open reports',
      icon: BarChart3,
    },
  ];

  if (
    procedure.entityType === 'EstateServicedPlotAllocation' ||
    procedure.entityType === 'EstateLandsPartiallyServiced' ||
    procedure.entityType === 'EstateTraditionalLands' ||
    procedure.entityType === 'EstateAdditionalLand'
  ) {
    controls.splice(3, 0, {
      title: 'Land Bank / Allocation Source',
      description:
        'Validate availability, plot/demarcation reference, planning status, offer/right-of-entry evidence, and project/property handoff readiness.',
      href: '/estate/land-management',
      action: 'Open land bank',
      icon: Landmark,
    });
  }

  return controls;
}

function EstateSopOperations({ procedure }: { procedure: EstateProcedure }) {
  const controls = getEstateSopControls(procedure);

  return (
    <Card className="border-border bg-card text-card-foreground">
      <CardHeader>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <CardTitle>Estate SOP Operations</CardTitle>
            <CardDescription className="mt-2 max-w-4xl">
              Practical operating layer for {procedure.title}. This page keeps
              SOP handoffs, records, DMS, finance, legal, and reporting visible
              without hard-coding workflow stages.
            </CardDescription>
          </div>
          <Badge variant="outline">Workflow configured in setup</Badge>
        </div>
      </CardHeader>
      <CardContent>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {controls.map((control) => {
            const Icon = control.icon;
            return (
              <div
                key={`${control.title}-${control.href}`}
                className="rounded-md border border-border bg-background p-4"
              >
                <div className="flex items-start gap-3">
                  <div className="flex h-9 w-9 items-center justify-center rounded-md border bg-muted">
                    <Icon className="h-4 w-4 text-primary" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="font-medium">{control.title}</div>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {control.description}
                    </p>
                    <Button asChild size="sm" variant="outline" className="mt-3">
                      <Link href={control.href}>
                        {control.action}
                        <ArrowRight className="ml-2 h-3.5 w-3.5" />
                      </Link>
                    </Button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}

export default function EstateProcedureWorkspacePage() {
  const router = useRouter();
  const params = useParams<{ entityType?: string | string[] }>();
  const routeValue = Array.isArray(params?.entityType) ? params.entityType[0] : params?.entityType;
  const entityType = routeValue ? decodeURIComponent(routeValue) : '';
  const [procedure, setProcedure] = React.useState<EstateProcedure | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);

  React.useEffect(() => {
    let mounted = true;

    const loadProcedure = async () => {
      if (!entityType) {
        setProcedure(null);
        setIsLoading(false);
        return;
      }

      try {
        const procedures = await estateProcedureService.getProcedures();
        if (mounted) {
          setProcedure(findEstateProcedure(entityType, procedures));
        }
      } catch {
        if (mounted) {
          setProcedure(null);
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void loadProcedure();

    return () => {
      mounted = false;
    };
  }, [entityType]);

  if (isLoading) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading estate workspace
        </div>
      </div>
    );
  }

  if (!procedure) {
    return (
      <div className="space-y-4">
        <Button variant="ghost" className="w-fit gap-2 px-0" onClick={() => router.push('/estate')}>
          <ArrowLeft className="h-4 w-4" />
          Back to Estate
        </Button>
        <Card>
          <CardHeader>
            <CardTitle>Workspace unavailable</CardTitle>
            <CardDescription>
              The Estate procedure catalogue did not return this entity type.
            </CardDescription>
          </CardHeader>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4">
        <Button variant="ghost" className="w-fit gap-2 px-0" onClick={() => router.push('/estate')}>
          <ArrowLeft className="h-4 w-4" />
          Back to Estate
        </Button>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-2">
            <div className="flex flex-wrap gap-2">
              <Badge variant="outline">{procedure.entityType}</Badge>
              <Badge variant="outline">
                {procedure.workspaceType ?? 'Case Workflow'}
              </Badge>
              <Badge variant="secondary">{procedure.source}</Badge>
              <Badge variant="secondary">
                {getProcedureStageLabel(
                  procedure.workspaceType,
                  procedure.stageCount
                )}
              </Badge>
            </div>
            <h1 className="text-3xl font-bold tracking-tight">{procedure.title}</h1>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link href={`/finance/ar/invoices?source=Estate&entityType=${encodeURIComponent(procedure.entityType)}`}>
                <CreditCard className="mr-2 h-4 w-4" />
                Finance AR
              </Link>
            </Button>
            <Button asChild variant="outline">
              <Link href={`/document-management?module=Estate&entityType=${encodeURIComponent(procedure.entityType)}`}>
                <FileText className="mr-2 h-4 w-4" />
                Documents
              </Link>
            </Button>
            <Button asChild variant="outline">
              <Link href={`/administration/workflow?entityType=${encodeURIComponent(procedure.entityType)}`}>
                <Settings className="mr-2 h-4 w-4" />
                Workflow setup
              </Link>
            </Button>
          </div>
        </div>
      </div>

      <EstateSopOperations procedure={procedure} />

      <ProcedureCaseWorkspace
        module="Estate"
        entityType={procedure.entityType}
        defaultTitle={procedure.title}
        workspaceType={procedure.workspaceType}
      />
    </div>
  );
}
