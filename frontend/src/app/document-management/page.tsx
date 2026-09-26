'use client';

import React from 'react';
import Link from 'next/link';
import { ArrowRight, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Pagination } from '@/components/ui/pagination';
import { usePaginatedItems } from '@/hooks/use-paginated-items';
import {
  documentManagementService,
  type CentralDocumentDashboard,
  type CentralDocumentRegisterItem,
  type CentralDocumentWorkspaceItem,
} from '@/services/document-management.service';

const operationalWorkspaceTypes = new Set([
  'CentralDocumentRegister',
  'CentralDocumentMetadataTemplate',
  'CentralDocumentVersion',
  'CentralDocumentGovernance',
  'CentralDocumentIntegrationQueue',
]);

export default function DocumentManagementPage() {
  const [workspaces, setWorkspaces] = React.useState<
    CentralDocumentWorkspaceItem[]
  >([]);
  const [dashboard, setDashboard] =
    React.useState<CentralDocumentDashboard | null>(null);
  const [register, setRegister] = React.useState<CentralDocumentRegisterItem[]>(
    []
  );
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const registerPages = usePaginatedItems(register, 10);

  React.useEffect(() => {
    let mounted = true;
    Promise.all([
      documentManagementService.getWorkspaces(),
      documentManagementService.getDashboard(),
      documentManagementService.getRegister(),
    ])
      .then(([workspaceData, dashboardData, registerData]) => {
        if (!mounted) return;
        setWorkspaces(workspaceData);
        setDashboard(dashboardData);
        setRegister(registerData);
        setLoadError(null);
      })
      .catch(() => {
        if (mounted) setLoadError('Unable to load Central DMS data.');
      })
      .finally(() => {
        if (mounted) setIsLoading(false);
      });
    return () => {
      mounted = false;
    };
  }, []);

  const attentionQueues = (dashboard?.moduleQueues ?? [])
    .map((queue) => ({
      ...queue,
      total:
        queue.pendingMetadata +
        queue.pendingVersion +
        queue.openAnnotations +
        queue.retentionReviews,
    }))
    .filter((queue) => queue.total > 0)
    .sort((a, b) => b.total - a.total);

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold">Central Document Management</h1>
        <div className="flex flex-wrap gap-2">
          <Button asChild size="sm" variant="outline">
            <Link href="/administration/document-management/metadata-templates">
              Metadata setup
            </Link>
          </Button>
          <Button asChild size="sm" variant="outline">
            <Link href="/administration/document-management/access-retention">
              Access & retention
            </Link>
          </Button>
          <Button asChild size="sm" variant="outline">
            <Link href="/estate/property-management/EstatePropertyManagementDocumentRecordIndex">
              Property records
            </Link>
          </Button>
        </div>
      </header>

      {isLoading ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Loading documents
        </p>
      ) : null}
      {loadError ? (
        <p role="alert" className="text-sm text-destructive">
          {loadError}
        </p>
      ) : null}

      <section className="space-y-2">
        <h2 className="text-base font-semibold">Workspaces</h2>
        <div className="overflow-x-auto border-y">
          <table className="w-full min-w-[480px] text-sm">
            <thead className="bg-muted/40 text-left text-muted-foreground">
              <tr>
                <th className="px-3 py-2 font-medium">Workspace</th>
                <th className="w-24 px-3 py-2 text-right font-medium">Open</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {workspaces
                .filter((workspace) =>
                  operationalWorkspaceTypes.has(workspace.entityType)
                )
                .map((workspace) => (
                  <tr key={workspace.entityType}>
                    <td className="px-3 py-2.5 font-medium">
                      {workspace.title}
                    </td>
                    <td className="px-3 py-2 text-right">
                      <Button
                        asChild
                        size="icon"
                        variant="ghost"
                        title={`Open ${workspace.title}`}
                      >
                        <Link
                          aria-label={`Open ${workspace.title}`}
                          href={
                            workspace.entityType === 'CentralDocumentRegister'
                              ? '/document-management/records'
                              : `/document-management/${workspace.entityType}`
                          }
                        >
                          <ArrowRight className="h-4 w-4" />
                        </Link>
                      </Button>
                    </td>
                  </tr>
                ))}
              {!isLoading && workspaces.length === 0 ? (
                <tr>
                  <td
                    colSpan={2}
                    className="px-3 py-6 text-center text-muted-foreground"
                  >
                    No workspaces available.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </section>

      <section className="space-y-2">
        <h2 className="text-base font-semibold">Needs attention</h2>
        <div className="overflow-x-auto border-y">
          <table className="w-full min-w-[650px] text-sm">
            <thead className="bg-muted/40 text-left text-muted-foreground">
              <tr>
                <th className="px-3 py-2 font-medium">Module</th>
                <th className="px-3 py-2 text-right font-medium">Metadata</th>
                <th className="px-3 py-2 text-right font-medium">Versions</th>
                <th className="px-3 py-2 text-right font-medium">
                  Annotations
                </th>
                <th className="px-3 py-2 text-right font-medium">Retention</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {attentionQueues.map((queue) => (
                <tr key={queue.module}>
                  <td className="px-3 py-2.5 font-medium">{queue.module}</td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {queue.pendingMetadata}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {queue.pendingVersion}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {queue.openAnnotations}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {queue.retentionReviews}
                  </td>
                </tr>
              ))}
              {!isLoading && attentionQueues.length === 0 ? (
                <tr>
                  <td
                    colSpan={5}
                    className="px-3 py-6 text-center text-muted-foreground"
                  >
                    Nothing needs attention.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </section>

      <section className="space-y-2">
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-base font-semibold">Documents</h2>
          <Button asChild size="sm" variant="outline">
            <Link href="/document-management/records">
              Full register <ArrowRight className="ml-2 h-4 w-4" />
            </Link>
          </Button>
        </div>
        <div className="overflow-x-auto border-y">
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-muted/40 text-left text-muted-foreground">
              <tr>
                <th className="px-3 py-2 font-medium">Reference</th>
                <th className="px-3 py-2 font-medium">Title</th>
                <th className="px-3 py-2 font-medium">Module</th>
                <th className="px-3 py-2 font-medium">Version</th>
                <th className="px-3 py-2 font-medium">Status</th>
                <th className="w-24 px-3 py-2 text-right font-medium">View</th>
              </tr>
            </thead>
            <tbody className="divide-y">
              {registerPages.items.map((document) => (
                <tr key={document.id || document.documentReference}>
                  <td className="px-3 py-2.5 font-medium">
                    {document.documentReference}
                  </td>
                  <td className="max-w-[340px] truncate px-3 py-2">
                    {document.title}
                  </td>
                  <td className="px-3 py-2">{document.module}</td>
                  <td className="px-3 py-2">{document.version}</td>
                  <td className="px-3 py-2">{document.repositoryStatus}</td>
                  <td className="px-3 py-2 text-right">
                    {document.id ? (
                      <Button
                        asChild
                        size="icon"
                        variant="ghost"
                        title="Open record"
                      >
                        <Link
                          aria-label={`Open ${document.title}`}
                          href={`/document-management/records/${document.id}`}
                        >
                          <ArrowRight className="h-4 w-4" />
                        </Link>
                      </Button>
                    ) : null}
                  </td>
                </tr>
              ))}
              {!isLoading && register.length === 0 ? (
                <tr>
                  <td
                    colSpan={6}
                    className="px-3 py-6 text-center text-muted-foreground"
                  >
                    No documents found.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
        {register.length > registerPages.pageSize ? (
          <Pagination
            currentPage={registerPages.currentPage}
            totalPages={registerPages.totalPages}
            totalItems={registerPages.totalItems}
            pageSize={registerPages.pageSize}
            onPageChange={registerPages.setCurrentPage}
          />
        ) : null}
      </section>
    </div>
  );
}
