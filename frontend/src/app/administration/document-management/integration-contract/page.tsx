'use client';

import React from 'react';
import Link from 'next/link';
import {
  AlertTriangle,
  BookTemplate,
  CheckCircle2,
  GitBranch,
  Loader2,
  RefreshCw,
  ShieldCheck,
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
import { documentManagementService } from '@/services/document-management.service';

const requiredPackage = [
  'Source module',
  'Source label',
  'Source entity type',
  'Source record ID',
  'Source record reference',
  'Document title',
  'Document type',
  'Repository path or file reference',
  'Metadata template code',
  'Version number',
  'Annotation requirement',
  'Access profile',
  'Retention policy',
];

const sourceContracts = [
  {
    module: 'Estate / Facilities',
    sourceLabel: 'Source: Estate / Facilities -> Central DMS',
    terms: ['estate / facilities'],
  },
  {
    module: 'Estate / Property Management',
    sourceLabel: 'Source: Estate / Property Management -> Central DMS',
    terms: ['estate / property', 'property management'],
  },
  {
    module: 'Project Management',
    sourceLabel: 'Source: Project Management -> Central DMS',
    terms: ['project'],
  },
  {
    module: 'Maintenance Management',
    sourceLabel: 'Source: Maintenance Management -> Central DMS',
    terms: ['maintenance'],
  },
  {
    module: 'Helpdesk / Complaint Management',
    sourceLabel: 'Source: Helpdesk / Complaint Management -> Central DMS',
    terms: ['helpdesk', 'complaint'],
  },
  {
    module: 'Finance AR / AP',
    sourceLabel: 'Source: Finance -> Central DMS',
    terms: ['finance'],
  },
  {
    module: 'HR / Payroll',
    sourceLabel: 'Source: HR / Payroll -> Central DMS',
    terms: ['hr', 'payroll'],
  },
  {
    module: 'Procurement / Legal',
    sourceLabel: 'Source: Procurement / Legal -> Central DMS',
    terms: ['procurement', 'legal'],
  },
];

const lifecycleSteps = [
  {
    title: 'Source Module Owns The Business Record',
    detail:
      'The owning module keeps its workflow, approvals, status, and business rules.',
  },
  {
    title: 'Source Publishes A Document Package',
    detail:
      'The module sends document metadata, source reference, and file/repository reference to Central DMS.',
  },
  {
    title: 'Central DMS Creates Or Reuses A Record',
    detail:
      'DMS checks duplicates, assigns document reference, applies template, access, retention, and version state.',
  },
  {
    title: 'DMS Returns The Reference',
    detail:
      'The source module stores the DMS record ID/reference for future viewing, annotation, comments, and audit.',
  },
];

export default function DmsIntegrationContractPage() {
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [evidence, setEvidence] = React.useState({
    metadataModules: [] as string[],
    generationModules: [] as string[],
    accessModules: [] as string[],
    retentionModules: [] as string[],
    pendingModules: [] as string[],
  });

  const loadEvidence = React.useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const [metadata, generation, access, retention, pending] =
        await Promise.all([
          documentManagementService.getMetadataTemplates(),
          documentManagementService.getGenerationTemplates(),
          documentManagementService.getAccessRules(),
          documentManagementService.getRetentionPolicies(),
          documentManagementService.getIntegrationQueue(),
        ]);
      setEvidence({
        metadataModules: metadata
          .filter((item) => item.isActive !== false)
          .map((item) => item.module),
        generationModules: generation
          .filter((item) => item.isActive)
          .map((item) => item.module),
        accessModules: access
          .filter((item) => item.isActive)
          .map((item) => item.module || ''),
        retentionModules: retention
          .filter((item) => item.isActive)
          .map((item) => item.module || ''),
        pendingModules: pending.map((item) => item.sourceModule),
      });
    } catch (caughtError) {
      setError(
        caughtError instanceof Error
          ? caughtError.message
          : 'Could not load DMS integration readiness.'
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    void loadEvidence();
  }, [loadEvidence]);

  const countMatches = (modules: string[], terms: string[]) =>
    modules.filter((module) => {
      const normalized = module.toLowerCase();
      return terms.some((term) => normalized.includes(term));
    }).length;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            Administration / DMS setup
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              DMS Integration Contract
            </h1>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            variant="outline"
            onClick={() => void loadEvidence()}
            disabled={isLoading}
          >
            {isLoading ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="mr-2 h-4 w-4" />
            )}
            Refresh
          </Button>
          <Button asChild variant="outline">
            <Link href="/administration/document-management/metadata-templates">
              <BookTemplate className="mr-2 h-4 w-4" />
              Metadata setup
            </Link>
          </Button>
          <Button asChild>
            <Link href="/document-management/CentralDocumentIntegrationQueue">
              <Workflow className="mr-2 h-4 w-4" />
              Integration queue
            </Link>
          </Button>
        </div>
      </div>

      {error ? (
        <div className="flex items-center gap-2 rounded-md border border-destructive/30 bg-destructive/10 p-3 text-sm text-destructive">
          <AlertTriangle className="h-4 w-4" />
          {error}
        </div>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[1.1fr_0.9fr]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
              <GitBranch className="h-5 w-5 text-primary" />
            </div>
            <CardTitle>Required Publish Package</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-2 sm:grid-cols-2">
              {requiredPackage.map((item) => (
                <div
                  key={item}
                  className="flex items-center gap-2 rounded-md border bg-background px-3 py-2 text-sm"
                >
                  <CheckCircle2 className="h-4 w-4 text-primary" />
                  {item}
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
              <ShieldCheck className="h-5 w-5 text-primary" />
            </div>
            <CardTitle>Ownership</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2">
            <Badge variant="secondary">DMS owns controls</Badge>
            <Badge variant="outline">Modules own records</Badge>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        {sourceContracts.map((contract) => {
          const metadataCount = countMatches(
            evidence.metadataModules,
            contract.terms
          );
          const templateCount = countMatches(
            evidence.generationModules,
            contract.terms
          );
          const accessCount = countMatches(
            evidence.accessModules,
            contract.terms
          );
          const retentionCount = countMatches(
            evidence.retentionModules,
            contract.terms
          );
          const pendingCount = countMatches(
            evidence.pendingModules,
            contract.terms
          );
          const configuredControls = [
            metadataCount,
            accessCount,
            retentionCount,
          ].filter(Boolean).length;
          const readiness =
            configuredControls === 3
              ? 'Ready'
              : configuredControls > 0 || templateCount > 0
                ? 'Partially configured'
                : 'Not configured';

          return (
            <Card
              key={contract.module}
              className="border-border bg-card text-card-foreground"
            >
              <CardHeader>
                <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <CardTitle className="text-base">
                      {contract.module}
                    </CardTitle>
                    <Badge variant="outline" className="mt-2 w-fit">
                      {contract.sourceLabel}
                    </Badge>
                  </div>
                  <Badge
                    variant={readiness === 'Ready' ? 'default' : 'outline'}
                    className="w-fit"
                  >
                    {isLoading ? 'Checking' : readiness}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
                  {[
                    ['Metadata', metadataCount],
                    ['Templates', templateCount],
                    ['Access', accessCount],
                    ['Retention', retentionCount],
                    ['Pending', pendingCount],
                  ].map(([label, count]) => (
                    <div
                      key={String(label)}
                      className="rounded-md border bg-background p-3 text-center"
                    >
                      <div className="text-lg font-semibold">
                        {isLoading ? '-' : count}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {label}
                      </div>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
