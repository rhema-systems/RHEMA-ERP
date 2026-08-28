'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, BarChart3, FileCheck2, FileText, Gavel, Loader2, Settings } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  legalProcedureService,
  type LegalProcedureWorkspace,
  type LegalWorkspaceField,
} from '@/services/legal-procedure.service';
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';

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

function legalWorkspacePurpose(entityType: string) {
  if (entityType === 'LegalProcedure') {
    return 'Legal Procedure Manual controls intake, minuting, due diligence, drafting, approval, execution, dispatch, and record return without replacing the configured workflow.';
  }

  if (entityType === 'LegalOpinionAdvisory') {
    return 'Legal Opinion / Advisory matters focus on issue intake, source documents, research notes, advice memo drafting, confidentiality, approval, recipient dispatch, and closure evidence.';
  }

  if (entityType === 'LegalExternalCounsel') {
    return 'External Counsel matters focus on counsel instructions, retainer and fee references, matter assignment, deliverables, invoice/AP links, performance, confidentiality, and closeout.';
  }

  if (entityType === 'LegalCourtProcess' || entityType === 'LegalOtherCourtProcess') {
    return 'Court matters focus on service date, docket control, deadline tracking, response preparation, filing evidence, hearings, and legal closeout.';
  }

  if (entityType === 'LegalMortgage' || entityType === 'LegalMortgageInPrinciple') {
    return 'Mortgage matters focus on property-file review, payment evidence, consent or in-principle drafting, legal vetting, signature routing, client release, and Estate file return.';
  }

  if (entityType === 'LegalTerminationRecognition') {
    return 'Termination / Recognition matters focus on due diligence, site evidence, notice control, notice expiry, payment confirmation, declaration execution, and Estate record update.';
  }

  return 'Instrument matters focus on source-file review, party verification, drafting, legal vetting, approvals, execution, registration or filing, and records update.';
}

function LegalWorkflowOverview({
  workspace,
}: {
  workspace: LegalProcedureWorkspace;
}) {
  const mandatoryDocuments = workspace.requiredDocuments.filter(
    (document) => document.isMandatory
  ).length;

  return (
    <Card className="border-border bg-card text-card-foreground">
      <CardHeader>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <CardTitle>{workspace.procedure.title} Workspace</CardTitle>
            <CardDescription className="mt-2 max-w-4xl">
              Dedicated Legal workspace. This screen does not create a generic
              procedure case; stages, checklists, documents, intake fields,
              outputs, and handoffs come from the Legal workflow/catalog.
            </CardDescription>
          </div>
          <Badge variant="outline">No generic case view</Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-5">
        <div className="grid gap-3 md:grid-cols-4">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">
              {workspace.stages.length}
            </div>
            <div className="text-sm text-muted-foreground">legal stages</div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">
              {mandatoryDocuments}
            </div>
            <div className="text-sm text-muted-foreground">
              mandatory documents
            </div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">
              {workspace.intakeFields.length}
            </div>
            <div className="text-sm text-muted-foreground">intake fields</div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="text-2xl font-semibold">
              {workspace.handoffs.length}
            </div>
            <div className="text-sm text-muted-foreground">handoffs</div>
          </div>
        </div>

        {workspace.stages.length === 0 ? (
          <div className="rounded-md border border-dashed border-border bg-muted/40 p-4">
            <div className="font-medium">Workflow not configured</div>
            <p className="mt-1 text-sm text-muted-foreground">
              No Legal stages, checklists, documents, fields, outputs, or
              handoffs are predefined here. Configure and publish the workflow
              in Administration &gt; Workflow Setup for this entity type.
            </p>
          </div>
        ) : null}

        <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(0,0.8fr)]">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="mb-3 flex items-center gap-2 font-medium">
              <Gavel className="h-4 w-4 text-primary" />
              Legal stage board
            </div>
            <div className="space-y-3">
              {workspace.stages.map((stage, index) => (
                <div
                  key={`${stage.name}-${index}`}
                  className="rounded-md border border-border bg-card p-3"
                >
                  <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                    <div>
                      <div className="font-medium">
                        {index + 1}. {stage.name}
                      </div>
                      <div className="mt-1 text-sm text-muted-foreground">
                        {stage.summary}
                      </div>
                    </div>
                    <Badge variant="secondary" className="w-fit">
                      {stage.owner}
                    </Badge>
                  </div>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {stage.checklist.slice(0, 4).map((item) => (
                      <Badge key={item} variant="outline">
                        {item}
                      </Badge>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>

          <div className="space-y-4">
            <div className="rounded-md border border-border bg-background p-4">
              <div className="mb-3 flex items-center gap-2 font-medium">
                <FileCheck2 className="h-4 w-4 text-primary" />
                Documents required
              </div>
              <div className="space-y-2">
                {workspace.requiredDocuments.map((document) => (
                  <div
                    key={document.name}
                    className="rounded-md border border-border bg-card p-3"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="font-medium">{document.name}</div>
                      <Badge
                        variant={document.isMandatory ? 'default' : 'outline'}
                      >
                        {document.isMandatory ? 'Required' : 'Optional'}
                      </Badge>
                    </div>
                    <div className="mt-1 text-sm text-muted-foreground">
                      From: {document.requiredFrom}
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div className="rounded-md border border-border bg-background p-4">
              <div className="mb-3 font-medium">Intake fields</div>
              <div className="grid gap-2">
                {workspace.intakeFields.map((field) => (
                  <div
                    key={field.key}
                    className="flex items-center justify-between gap-3 rounded-md border border-border bg-card p-3 text-sm"
                  >
                    <span className="font-medium">{field.label}</span>
                    <span className="text-muted-foreground">
                      {fieldDisplayValue(field)}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>

        <div className="grid gap-4 lg:grid-cols-2">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="mb-3 font-medium">Expected outputs</div>
            <div className="flex flex-wrap gap-2">
              {workspace.outputs.map((output) => (
                <Badge key={output} variant="secondary">
                  {output}
                </Badge>
              ))}
            </div>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="mb-3 font-medium">Legal handoffs</div>
            <div className="space-y-2">
              {workspace.handoffs.map((handoff) => (
                <div
                  key={`${handoff.fromRole}-${handoff.toRole}-${handoff.trigger}`}
                  className="rounded-md border border-border bg-card p-3 text-sm"
                >
                  <div className="font-medium">
                    {handoff.fromRole} → {handoff.toRole}
                  </div>
                  <div className="mt-1 text-muted-foreground">
                    {handoff.trigger}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function LegalMatterOperations({
  workspace,
}: {
  workspace: LegalProcedureWorkspace;
}) {
  return (
    <Card className="border-border bg-card text-card-foreground">
      <CardHeader>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <CardTitle>Legal Matter Operations</CardTitle>
            <CardDescription className="mt-2 max-w-4xl">
              {legalWorkspacePurpose(workspace.procedure.entityType)}
            </CardDescription>
          </div>
          <Badge variant="secondary">{workspace.procedure.source}</Badge>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <div className="rounded-md border border-border bg-background p-4">
            <div className="font-medium">Matter control</div>
            <p className="mt-2 text-sm text-muted-foreground">
              Track source department, applicant, property/file reference,
              deadlines, assigned officer, and current legal action.
            </p>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="font-medium">Document control</div>
            <p className="mt-2 text-sm text-muted-foreground">
              Keep request, file, draft, evidence, signed copy, dispatch, and
              records references visible without replacing Central DMS.
            </p>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="font-medium">Approval control</div>
            <p className="mt-2 text-sm text-muted-foreground">
              Show Head of Legal, Legal Officer, Managing Director, client,
              witness, court, or Estate handoff points clearly.
            </p>
          </div>
          <div className="rounded-md border border-border bg-background p-4">
            <div className="font-medium">Closeout control</div>
            <p className="mt-2 text-sm text-muted-foreground">
              Confirm final signatures, filing/dispatch evidence, records
              update, returned files, and legal closure notes.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

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

      <LegalMatterOperations workspace={workspace} />

      <ProcedureCaseWorkspace
        module="Legal"
        entityType={procedure.entityType}
        defaultTitle={procedure.title}
        workspaceType="Legal Matter"
      />

      <LegalWorkflowOverview workspace={workspace} />
    </div>
  );
}
