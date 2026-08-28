'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, ClipboardCheck, FileText, Landmark, Loader2, Map, Settings } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';
import {
  planningProcedureService,
  type PlanningProcedureWorkspace,
} from '@/services/planning-procedure.service';

export default function PlanningProcedureWorkspacePage() {
  const router = useRouter();
  const params = useParams<{ entityType?: string | string[] }>();
  const routeValue = Array.isArray(params?.entityType) ? params.entityType[0] : params?.entityType;
  const entityType = routeValue ? decodeURIComponent(routeValue) : '';
  const [workspace, setWorkspace] = React.useState<PlanningProcedureWorkspace | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const loadWorkspace = async () => {
      if (!entityType) {
        setWorkspace(null);
        setLoadError('Planning workspace was not found.');
        setIsLoading(false);
        return;
      }

      try {
        const data = await planningProcedureService.getProcedureWorkspace(entityType);
        if (mounted) {
          setWorkspace(data);
          setLoadError(data ? null : 'Planning workspace was not returned by the API.');
        }
      } catch {
        if (mounted) {
          setWorkspace(null);
          setLoadError('Unable to load planning workspace from the API.');
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void loadWorkspace();

    return () => {
      mounted = false;
    };
  }, [entityType]);

  if (isLoading) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading planning workspace
        </div>
      </div>
    );
  }

  if (!workspace) {
    return (
      <div className="space-y-4">
        <Button variant="ghost" className="w-fit gap-2 px-0" onClick={() => router.push('/development/planning')}>
          <ArrowLeft className="h-4 w-4" />
          Back to Planning
        </Button>
        <Card>
          <CardHeader>
            <CardTitle>Workspace unavailable</CardTitle>
            {loadError ? (
              <p className="text-sm text-muted-foreground">{loadError}</p>
            ) : null}
          </CardHeader>
        </Card>
      </div>
    );
  }

  const { procedure } = workspace;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4">
        <Button variant="ghost" className="w-fit gap-2 px-0" onClick={() => router.push('/development/planning')}>
          <ArrowLeft className="h-4 w-4" />
          Back to Planning
        </Button>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-2">
            <div className="flex flex-wrap gap-2">
              <Badge variant="outline">{procedure.entityType}</Badge>
              <Badge variant="secondary">{procedure.source}</Badge>
              <Badge variant="secondary">Workflow configured in setup</Badge>
            </div>
            <h1 className="text-3xl font-bold tracking-tight">{procedure.title}</h1>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link href={`/document-management?module=Planning&entityType=${encodeURIComponent(procedure.entityType)}`}>
                <FileText className="mr-2 h-4 w-4" />
                Documents
              </Link>
            </Button>
            <Button asChild variant="outline">
              <Link href={`/administration/workflow?q=${encodeURIComponent(procedure.entityType)}`}>
                <Settings className="mr-2 h-4 w-4" />
                Workflow setup
              </Link>
            </Button>
          </div>
        </div>
      </div>

      <PlanningSopOperations entityType={procedure.entityType} />

      <ProcedureCaseWorkspace module="Planning" entityType={procedure.entityType} defaultTitle={procedure.title} />
    </div>
  );
}

function PlanningSopOperations({ entityType }: { entityType: string }) {
  const cards = [
    {
      title: 'Configured Workflow',
      description: 'Planning stages, required documents, checklists, approvals, and assignments are maintained in Workflow Setup.',
      href: `/administration/workflow?q=${encodeURIComponent(entityType)}`,
      icon: Settings,
    },
    {
      title: 'Central DMS',
      description: 'Planning plans, layouts, site reports, committee evidence, searches, and responses should be stored in Central DMS.',
      href: `/document-management?module=Planning&entityType=${encodeURIComponent(entityType)}`,
      icon: FileText,
    },
    {
      title: 'Estate / Land Bank Check',
      description: 'Use Estate records and land bank references when Planning needs parcel, allocation, ownership, or regularization context.',
      href: '/estate/land-management',
      icon: Landmark,
    },
    {
      title: 'Project / HOD Approval',
      description: 'Route completed Planning recommendations, site plans, reports, and committee outputs back to Project/HOD approvals.',
      href: '/development/project-approvals',
      icon: ClipboardCheck,
    },
    {
      title: 'Planning Reports',
      description: 'Planning activity and SOP reports should be created in the central Reports module under Planning.',
      href: '/reports?module=planning',
      icon: Map,
    },
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle>Planning SOP Operations</CardTitle>
        <CardDescription>
          Operational controls around the live case workspace. The workflow itself is not hard-coded here.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {cards.map((card) => {
          const Icon = card.icon;
          return (
            <Button key={card.title} asChild variant="outline" className="h-auto justify-start whitespace-normal p-4 text-left">
              <Link href={card.href}>
                <span className="flex items-start gap-3">
                  <Icon className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>
                    <span className="block font-medium">{card.title}</span>
                    <span className="block text-xs font-normal text-muted-foreground">{card.description}</span>
                  </span>
                </span>
              </Link>
            </Button>
          );
        })}
      </CardContent>
    </Card>
  );
}
