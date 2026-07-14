'use client';

import React from 'react';
import { useParams, useRouter } from 'next/navigation';
import {
  ArrowLeft,
  CheckCircle2,
  ClipboardList,
  FileText,
  Loader2,
  Send,
  Users,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';
import { Separator } from '@/components/ui/separator';
import {
  legalProcedureService,
  type LegalProcedureWorkspace,
  type LegalWorkspaceField,
} from '@/services/legal-procedure.service';

function fieldDisplayValue(field: LegalWorkspaceField) {
  if (field.type === 'select' && field.options && field.options.length > 0) {
    return field.options.join(' / ');
  }

  if (field.type === 'date') {
    return 'Date';
  }

  if (field.type === 'textarea') {
    return 'Long text';
  }

  return 'Text';
}

export default function LegalProcedureWorkspacePage() {
  const router = useRouter();
  const params = useParams<{ entityType?: string | string[] }>();
  const routeValue = Array.isArray(params?.entityType) ? params.entityType[0] : params?.entityType;
  const entityType = routeValue ? decodeURIComponent(routeValue) : '';
  const [workspace, setWorkspace] = React.useState<LegalProcedureWorkspace | null>(null);
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
        const data = await legalProcedureService.getProcedureWorkspace(entityType);
        if (mounted) {
          setWorkspace(data);
          setLoadError(data ? null : 'Procedure workspace was not found.');
        }
      } catch {
        if (mounted) {
          setLoadError('Procedure workspace was not found.');
          setWorkspace(null);
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
      <div className="min-h-screen bg-background text-foreground">
        <div className="mx-auto flex min-h-screen w-full max-w-7xl items-center justify-center px-4">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading procedure workspace
          </div>
        </div>
      </div>
    );
  }

  if (!workspace || loadError) {
    return (
      <div className="min-h-screen bg-background text-foreground">
        <div className="mx-auto flex w-full max-w-3xl flex-col gap-4 px-4 py-6 sm:px-6 lg:px-8">
          <Button variant="ghost" className="w-fit gap-2 px-0" onClick={() => router.push('/legal')}>
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
      </div>
    );
  }

  const { procedure } = workspace;

  return (
    <div className="min-h-screen bg-background text-foreground">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6 px-4 py-6 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-4">
          <Button variant="ghost" className="w-fit gap-2 px-0" onClick={() => router.push('/legal')}>
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
                <p className="mt-2 max-w-3xl text-sm leading-6 text-muted-foreground">
                  {procedure.summary}
                </p>
              </div>
            </div>
            <div className="flex flex-wrap gap-2">
              <Badge variant="secondary">{procedure.source}</Badge>
              <Badge variant="secondary">{workspace.stages.length} stages</Badge>
            </div>
          </div>
        </div>

        <ProcedureCaseWorkspace module="Legal" entityType={procedure.entityType} defaultTitle={procedure.title} />

        <div className="grid gap-4 lg:grid-cols-[minmax(0,1.35fr)_minmax(320px,0.65fr)]">
          <div className="space-y-4">
            <Card className="border-border bg-card text-card-foreground">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <ClipboardList className="h-5 w-5 text-primary" />
                  <CardTitle>Procedure Stages</CardTitle>
                </div>
                <CardDescription>Operational sequence for this legal procedure.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                {workspace.stages.map((stage, index) => (
                  <div key={`${stage.name}-${index}`} className="rounded-md border border-border bg-background p-4">
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                      <div>
                        <div className="flex items-center gap-2">
                          <Badge variant="outline">{index + 1}</Badge>
                          <h2 className="text-base font-semibold">{stage.name}</h2>
                        </div>
                        <p className="mt-2 text-sm leading-6 text-muted-foreground">{stage.summary}</p>
                      </div>
                      <Badge variant="secondary" className="w-fit">{stage.owner}</Badge>
                    </div>
                    <Separator className="my-4" />
                    <div className="grid gap-2 sm:grid-cols-2">
                      {stage.checklist.map((item) => (
                        <div key={item} className="flex items-start gap-2 text-sm text-muted-foreground">
                          <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-primary" />
                          <span>{item}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>

            <Card className="border-border bg-card text-card-foreground">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <Users className="h-5 w-5 text-primary" />
                  <CardTitle>Role Handoffs</CardTitle>
                </div>
                <CardDescription>Ownership changes that move the matter forward.</CardDescription>
              </CardHeader>
              <CardContent className="grid gap-3 md:grid-cols-2">
                {workspace.handoffs.map((handoff) => (
                  <div key={`${handoff.fromRole}-${handoff.toRole}-${handoff.trigger}`} className="rounded-md border border-border bg-background p-4">
                    <div className="flex items-center gap-2 text-sm font-medium">
                      <span>{handoff.fromRole}</span>
                      <Send className="h-4 w-4 text-muted-foreground" />
                      <span>{handoff.toRole}</span>
                    </div>
                    <p className="mt-2 text-sm leading-6 text-muted-foreground">{handoff.trigger}</p>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>

          <div className="space-y-4">
            <Card className="border-border bg-card text-card-foreground">
              <CardHeader>
                <CardTitle>Intake Fields</CardTitle>
                <CardDescription>Information captured when opening the matter.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {workspace.intakeFields.map((field) => (
                  <div key={field.key} className="rounded-md border border-border bg-background p-3">
                    <div className="text-sm font-medium">{field.label}</div>
                    <div className="mt-1 text-xs uppercase tracking-normal text-muted-foreground">{fieldDisplayValue(field)}</div>
                  </div>
                ))}
              </CardContent>
            </Card>

            <Card className="border-border bg-card text-card-foreground">
              <CardHeader>
                <div className="flex items-center gap-2">
                  <FileText className="h-5 w-5 text-primary" />
                  <CardTitle>Required Documents</CardTitle>
                </div>
              </CardHeader>
              <CardContent className="space-y-3">
                {workspace.requiredDocuments.map((document) => (
                  <div key={document.name} className="rounded-md border border-border bg-background p-3">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <div className="text-sm font-medium">{document.name}</div>
                        <div className="mt-1 text-sm text-muted-foreground">{document.requiredFrom}</div>
                      </div>
                      <Badge variant={document.isMandatory ? 'default' : 'outline'}>
                        {document.isMandatory ? 'Required' : 'Optional'}
                      </Badge>
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>

            <Card className="border-border bg-card text-card-foreground">
              <CardHeader>
                <CardTitle>Outputs</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {workspace.outputs.map((output) => (
                  <div key={output} className="flex items-start gap-2 text-sm text-muted-foreground">
                    <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-primary" />
                    <span>{output}</span>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>
        </div>
      </div>
    </div>
  );
}
