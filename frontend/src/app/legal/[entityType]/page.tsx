'use client';

import React from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, BarChart3, FileText, Loader2, Settings } from 'lucide-react';

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
} from '@/services/legal-procedure.service';
import { ProcedureCaseWorkspace } from '@/components/procedures/ProcedureCaseWorkspace';

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
        registerOnly
        caseBasePath={`/legal/${encodeURIComponent(procedure.entityType)}`}
      />
    </div>
  );
}
