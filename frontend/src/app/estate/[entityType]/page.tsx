'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  CreditCard,
  FileText,
  Loader2,
  Settings,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Card,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';
import {
  estateProcedureService,
  findEstateProcedure,
  type EstateProcedure,
} from '@/services/estate-procedure.service';

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
          <div>
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

      <ProcedureCaseWorkspace
        module="Estate"
        entityType={procedure.entityType}
        defaultTitle={procedure.title}
        workspaceType={procedure.workspaceType}
        registerOnly
        caseBasePath={`/estate/${encodeURIComponent(procedure.entityType)}`}
      />
    </div>
  );
}
