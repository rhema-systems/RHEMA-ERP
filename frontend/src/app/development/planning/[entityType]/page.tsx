'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, FileText, Loader2, Settings } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle } from '@/components/ui/card';
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
            <Badge variant="outline">{procedure.entityType}</Badge>
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

      <ProcedureCaseWorkspace module="Planning" entityType={procedure.entityType} defaultTitle={procedure.title} />
    </div>
  );
}
