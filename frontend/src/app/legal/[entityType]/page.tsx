'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, BarChart3, FileText, Loader2, Settings } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  legalProcedureService,
  type LegalProcedureWorkspace,
} from '@/services/legal-procedure.service';
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';

export default function LegalProcedureWorkspacePage() {
  const router = useRouter();
  const params = useParams<{ entityType?: string | string[] }>();
  const routeValue = Array.isArray(params?.entityType)
    ? params.entityType[0]
    : params?.entityType;
  const entityType = routeValue ? decodeURIComponent(routeValue) : '';
  const [workspace, setWorkspace] =
    React.useState<LegalProcedureWorkspace | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const loadWorkspace = async () => {
      if (!entityType) {
        setLoadError('Procedure workspace was not found.');
        setIsLoading(false);
        return;
      }

      try {
        const data =
          await legalProcedureService.getProcedureWorkspace(entityType);
        if (mounted) {
          setWorkspace(data);
          setLoadError(data ? null : 'Procedure workspace was not returned by the API.');
        }
      } catch {
        if (mounted) {
          setWorkspace(null);
          setLoadError('Unable to load procedure workspace from the API.');
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
          Loading procedure workspace
        </div>
      </div>
    );
  }

  if (!workspace || loadError) {
    return (
      <div className="space-y-4">
        <Button
          variant="ghost"
          className="w-fit gap-2 px-0"
          onClick={() => router.push('/legal')}
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Legal
        </Button>
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Workspace unavailable</CardTitle>
            <CardDescription>{loadError}</CardDescription>
          </CardHeader>
        </Card>
      </div>
    );
  }

  const { procedure } = workspace;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4">
        <Button
          variant="ghost"
          className="w-fit gap-2 px-0"
          onClick={() => router.push('/legal')}
        >
          <ArrowLeft className="h-4 w-4" />
          Back to Legal
        </Button>
        <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
          <div className="space-y-2">
            <Badge variant="outline" className="w-fit">
              {procedure.entityType}
            </Badge>
            <div>
              <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
                {procedure.title}
              </h1>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Badge variant="secondary">{procedure.source}</Badge>
            <Button asChild variant="outline" size="sm">
              <Link href={`/administration/workflow?entityType=${encodeURIComponent(procedure.entityType)}`}>
                <Settings className="mr-2 h-4 w-4" />
                Workflow setup
              </Link>
            </Button>
            <Button asChild variant="outline" size="sm">
              <Link href={`/document-management?module=Legal&entityType=${encodeURIComponent(procedure.entityType)}`}>
                <FileText className="mr-2 h-4 w-4" />
                DMS
              </Link>
            </Button>
            <Button asChild variant="outline" size="sm">
              <Link href="/reports?module=legal">
                <BarChart3 className="mr-2 h-4 w-4" />
                Reports
              </Link>
            </Button>
          </div>
        </div>
      </div>

      <ProcedureCaseWorkspace
        module="Legal"
        entityType={procedure.entityType}
        defaultTitle={procedure.title}
        workspaceType="Legal Matter"
        registerOnly
        caseBasePath={`/legal/${encodeURIComponent(procedure.entityType)}`}
      />
    </div>
  );
}
