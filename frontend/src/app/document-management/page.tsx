'use client';

import React from 'react';
import Link from 'next/link';
import {
  AlertTriangle,
  ArrowRight,
  BarChart3,
  BookTemplate,
  FileText,
  GitBranch,
  Loader2,
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
import {
  documentManagementService,
  type CentralDocumentDashboard,
  type CentralDocumentMetadataTemplate,
  type CentralDocumentRegisterItem,
  type CentralDocumentWorkspaceItem,
} from '@/services/document-management.service';

const iconMap: Record<string, React.ComponentType<{ className?: string }>> = {
  BookTemplate,
  FileText,
  GitBranch,
  ShieldCheck,
  Workflow,
};

const accentClasses: Record<string, string> = {
  amber: 'text-amber-700 dark:text-amber-300',
  cyan: 'text-cyan-700 dark:text-cyan-300',
  emerald: 'text-emerald-700 dark:text-emerald-300',
  indigo: 'text-indigo-700 dark:text-indigo-300',
  slate: 'text-slate-700 dark:text-slate-300',
  violet: 'text-violet-700 dark:text-violet-300',
};

const operationalWorkspaceTypes = new Set([
  'CentralDocumentRegister',
  'CentralDocumentMetadataTemplate',
  'CentralDocumentVersion',
  'CentralDocumentGovernance',
  'CentralDocumentIntegrationQueue',
]);

export default function DocumentManagementPage() {
  const [workspaces, setWorkspaces] = React.useState<CentralDocumentWorkspaceItem[]>([]);
  const [dashboard, setDashboard] = React.useState<CentralDocumentDashboard | null>(null);
  const [templates, setTemplates] = React.useState<
    CentralDocumentMetadataTemplate[]
  >([]);
  const [register, setRegister] = React.useState<CentralDocumentRegisterItem[]>(
    []
  );
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      try {
        const [workspaceData, dashboardData, templateData, registerData] =
          await Promise.all([
            documentManagementService.getWorkspaces(),
            documentManagementService.getDashboard(),
            documentManagementService.getMetadataTemplates(),
            documentManagementService.getRegister(),
          ]);

        if (!mounted) {
          return;
        }

        setWorkspaces(workspaceData);
        setDashboard(dashboardData);
        setTemplates(templateData);
        setRegister(registerData);
        setLoadError(null);
      } catch {
        if (mounted) {
          setWorkspaces([]);
          setDashboard(null);
          setLoadError('Unable to load Central DMS data from the API.');
        }
      } finally {
        if (mounted) {
          setIsLoading(false);
        }
      }
    };

    void load();

    return () => {
      mounted = false;
    };
  }, []);

  const attentionQueues = React.useMemo(
    () =>
      (dashboard?.moduleQueues ?? [])
        .map((queue) => ({
          ...queue,
          total:
            queue.pendingMetadata +
            queue.pendingVersion +
            queue.openAnnotations +
            queue.retentionReviews,
        }))
        .filter((queue) => queue.total > 0)
        .sort((a, b) => b.total - a.total),
    [dashboard?.moduleQueues]
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="space-y-2">
          <Badge variant="outline" className="w-fit">
            System document control
          </Badge>
          <div>
            <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
              Central Document Management
            </h1>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline">
            <Link href="/estate/property-management/EstatePropertyManagementDocumentRecordIndex">
              Property Records
            </Link>
          </Button>
          <Button asChild variant="outline">
            <Link href="/estate/facilities/EstateFacilityDocument">
              Facilities Documents
            </Link>
          </Button>
        </div>
      </div>

      {isLoading ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading central document management
          </CardContent>
        </Card>
      ) : null}

      {!isLoading && loadError ? (
        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="py-8 text-center text-sm text-muted-foreground">
            {loadError}
          </CardContent>
        </Card>
      ) : null}

      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <div className="flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-amber-600 dark:text-amber-300" />
            <CardTitle>DMS Needs Attention</CardTitle>
          </div>
        </CardHeader>
        <CardContent>
          {attentionQueues.length ? (
            <div className="grid gap-3 lg:grid-cols-2 xl:grid-cols-3">
              {attentionQueues.map((queue) => (
                <div
                  key={queue.module}
                  className="rounded-md border bg-background p-4"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <div className="font-medium">{queue.module}</div>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {queue.sourceLabel}
                      </p>
                    </div>
                    <Badge variant="secondary">{queue.total}</Badge>
                  </div>
                  <div className="mt-3 grid grid-cols-2 gap-2 text-xs text-muted-foreground">
                    <span>{queue.pendingMetadata} metadata</span>
                    <span>{queue.pendingVersion} versions</span>
                    <span>{queue.openAnnotations} annotations</span>
                    <span>{queue.retentionReviews} retention</span>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div className="rounded-md border bg-background p-4 text-sm text-muted-foreground">
              No DMS source module queues need action right now.
            </div>
          )}
        </CardContent>
      </Card>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {(dashboard?.metrics ?? []).map((item) => (
          <Card
            key={item.label}
            className="border-border bg-card text-card-foreground"
          >
            <CardHeader className="space-y-1 pb-2">
              <CardDescription>{item.label}</CardDescription>
              <CardTitle className="text-2xl">{item.value}</CardTitle>
            </CardHeader>
            <CardContent>
              <Badge variant="secondary" className="mt-3">
                {item.trend}
              </Badge>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        {!isLoading && workspaces.length === 0 ? (
          <Card className="border-border bg-card text-card-foreground xl:col-span-3">
            <CardContent className="py-12 text-center text-sm text-muted-foreground">
              No DMS workspaces were returned by the API.
            </CardContent>
          </Card>
        ) : null}
        {workspaces
          .filter((workspace) =>
            operationalWorkspaceTypes.has(workspace.entityType)
          )
          .map((workspace) => {
            const Icon = iconMap[workspace.icon] || FileText;
            const accent = accentClasses[workspace.accent] || 'text-primary';

            return (
              <Card
                key={workspace.entityType}
                className="border-border bg-card text-card-foreground"
              >
                <CardHeader className="space-y-3">
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
                      <Icon className={`h-5 w-5 ${accent}`} />
                    </div>
                    <Badge variant="secondary">{workspace.stageCount}</Badge>
                  </div>
                  <div>
                    <CardTitle className="text-base leading-6">
                      {workspace.title}
                    </CardTitle>
                    <CardDescription className="mt-1">
                      {workspace.source}
                    </CardDescription>
                  </div>
                </CardHeader>
                <CardContent className="space-y-4">
                  <Button
                    asChild
                    variant="outline"
                    className="w-full justify-between"
                  >
                    <Link
                      href={
                        workspace.entityType === 'CentralDocumentRegister'
                          ? '/document-management/records'
                          : `/document-management/${workspace.entityType}`
                      }
                    >
                      Open workspace
                      <ArrowRight className="h-4 w-4" />
                    </Link>
                  </Button>
                </CardContent>
              </Card>
            );
          })}
      </div>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(360px,0.8fr)]">
        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <div className="flex items-center gap-2">
              <BarChart3 className="h-5 w-5 text-primary" />
              <CardTitle>DMS Readiness</CardTitle>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            {(dashboard?.readiness ?? []).map((item) => (
              <div
                key={item.label}
                className="rounded-md border bg-background p-4"
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{item.label}</div>
                  </div>
                  <Badge variant="secondary">{item.value}</Badge>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Module Integration Queue</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {(dashboard?.moduleQueues ?? []).map((queue) => (
              <div
                key={queue.module}
                className="rounded-md border bg-background p-4"
              >
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <div className="font-medium">{queue.module}</div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {queue.sourceLabel}
                    </p>
                  </div>
                  <Badge variant="outline">
                    {queue.pendingMetadata +
                      queue.pendingVersion +
                      queue.openAnnotations +
                      queue.retentionReviews}
                  </Badge>
                </div>
                <div className="mt-3 grid grid-cols-4 gap-2 text-center text-xs text-muted-foreground">
                  <span>{queue.pendingMetadata} metadata</span>
                  <span>{queue.pendingVersion} versions</span>
                  <span>{queue.openAnnotations} notes</span>
                  <span>{queue.retentionReviews} reviews</span>
                </div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>

      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <CardTitle>Administration Setup Snapshot</CardTitle>
        </CardHeader>
        <CardContent className="mb-0 flex flex-wrap gap-2 pb-0">
          <Button asChild variant="outline" size="sm">
            <Link href="/administration/document-management/metadata-templates">
              Metadata setup
            </Link>
          </Button>
          <Button asChild variant="outline" size="sm">
            <Link href="/administration/document-management/access-retention">
              Access & retention setup
            </Link>
          </Button>
        </CardContent>
        <CardContent className="grid gap-3 lg:grid-cols-2">
          {templates.map((template) => (
            <div
              key={template.templateCode}
              className="rounded-md border bg-background p-4"
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="font-medium">{template.documentType}</div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {template.module}
                  </div>
                </div>
                <Badge variant="outline">{template.templateCode}</Badge>
              </div>
              <p className="mt-3 text-xs text-muted-foreground">
                {template.sourceLabel}
              </p>
              <div className="mt-3 flex flex-wrap gap-2">
                {template.requiredFields.slice(0, 4).map((field) => (
                  <Badge key={field} variant="secondary">
                    {field}
                  </Badge>
                ))}
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card className="border-border bg-card text-card-foreground">
        <CardHeader>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <CardTitle>Document Register</CardTitle>
            </div>
            <Button asChild variant="outline" size="sm">
              <Link href="/document-management/records">
                Open full register
                <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          {register.map((document) => (
            <div
              key={document.documentReference}
              className="rounded-md border bg-background p-4"
            >
              <div className="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
                <div>
                  <div className="font-medium">{document.title}</div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {document.documentReference} / {document.module} /{' '}
                    {document.sourceRecord}
                  </p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {document.sourceLabel}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Badge variant="outline">{document.templateCode}</Badge>
                  <Badge variant="secondary">{document.version}</Badge>
                  <Badge variant="outline">{document.repositoryStatus}</Badge>
                  <Badge variant="outline">{document.annotationStatus}</Badge>
                  {document.id ? (
                    <Button asChild size="sm" variant="outline">
                      <Link
                        href={`/document-management/records/${document.id}`}
                      >
                        Details
                      </Link>
                    </Button>
                  ) : null}
                </div>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
