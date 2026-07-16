'use client';

import React from 'react';
import Link from 'next/link';
import {
  ArrowRight,
  Building2,
  ClipboardList,
  FileText,
  Home,
  Loader2,
  RefreshCw,
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
import { estatePropertyManagementService } from '@/services/estate-property-management.service';
import {
  estateLandManagementService,
  EstateManagedAssetStatus,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import type { FacilitiesProcedure } from '@/services/estate-facilities.service';
import {
  procedureCaseService,
  type ProcedureCaseSummary,
} from '@/services/procedure-case.service';
import {
  documentManagementService,
  type CentralDocumentRecord,
} from '@/services/document-management.service';

function isOpen(status: string) {
  return !['Completed', 'Closed', 'Cancelled', 'Canceled'].includes(status);
}

function propertyDocuments(records: CentralDocumentRecord[]) {
  return records.filter((record) =>
    ['Estate / Property Management', 'PropertyManagement'].includes(
      record.sourceModule
    )
  );
}

export default function PropertyManagementDashboardPage() {
  const [procedures, setProcedures] = React.useState<FacilitiesProcedure[]>([]);
  const [cases, setCases] = React.useState<ProcedureCaseSummary[]>([]);
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [documents, setDocuments] = React.useState<CentralDocumentRecord[]>([]);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  const load = React.useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);

    const [procedureResult, caseResult, assetResult, documentResult] =
      await Promise.allSettled([
        estatePropertyManagementService.getProcedures(),
        procedureCaseService.listModuleCases('PropertyManagement'),
        estateLandManagementService.getManagedAssets({ take: 500 }),
        documentManagementService.getRecords(),
      ]);

    setProcedures(
      procedureResult.status === 'fulfilled' ? procedureResult.value : []
    );
    setCases(caseResult.status === 'fulfilled' ? caseResult.value : []);
    setAssets(assetResult.status === 'fulfilled' ? assetResult.value : []);
    setDocuments(
      documentResult.status === 'fulfilled'
        ? propertyDocuments(documentResult.value)
        : []
    );
    if (
      procedureResult.status === 'rejected' ||
      caseResult.status === 'rejected' ||
      assetResult.status === 'rejected' ||
      documentResult.status === 'rejected'
    ) {
      setLoadError('Some property management dashboard data could not be loaded.');
    }
    setIsLoading(false);
  }, []);

  React.useEffect(() => {
    void load();
  }, [load]);

  const openCases = cases.filter((item) => isOpen(item.status));
  const completedCases = cases.length - openCases.length;
  const availableAssets = assets.filter(
    (asset) => asset.status === EstateManagedAssetStatus.Available
  ).length;
  const publishedListings = assets.filter(
    (asset) => asset.isPublishedToExternalPortal
  ).length;
  const dmsIssues = documents.filter(
    (record) =>
      record.metadataCompleteness?.status !== 'Complete' ||
      record.repositoryStatus !== 'Linked'
  ).length;

  const stageQueue = React.useMemo(() => {
    const counts = new Map<string, number>();
    openCases.forEach((item) =>
      counts.set(item.currentStageName, (counts.get(item.currentStageName) ?? 0) + 1)
    );
    return [...counts.entries()]
      .map(([stage, count]) => ({ stage, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 6);
  }, [openCases]);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <Badge variant="outline" className="mb-2 w-fit">
            Estate / Property Management
          </Badge>
          <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
            Property Management Dashboard
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => void load()}>
            <RefreshCw className="mr-2 h-4 w-4" />
            Refresh
          </Button>
          <Button asChild>
            <Link href="/estate/property-management">
              <ClipboardList className="mr-2 h-4 w-4" />
              Workspaces
            </Link>
          </Button>
        </div>
      </div>

      {isLoading ? (
        <Card>
          <CardContent className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" />
            Loading property management dashboard
          </CardContent>
        </Card>
      ) : null}

      {loadError ? (
        <Card>
          <CardContent className="py-4 text-sm text-muted-foreground">
            {loadError}
          </CardContent>
        </Card>
      ) : null}

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {[
          { label: 'Workspaces', value: procedures.length, icon: ClipboardList },
          { label: 'Open cases', value: openCases.length, icon: Home },
          { label: 'Available assets', value: availableAssets, icon: Building2 },
          { label: 'Portal listings', value: publishedListings, icon: FileText },
        ].map((item) => {
          const Icon = item.icon;
          return (
            <Card key={item.label}>
              <CardHeader className="flex flex-row items-start justify-between gap-3 space-y-0 pb-2">
                <div>
                  <CardDescription>{item.label}</CardDescription>
                  <CardTitle className="mt-1 text-2xl">{item.value}</CardTitle>
                </div>
                <div className="flex h-10 w-10 items-center justify-center rounded-md border bg-muted">
                  <Icon className="h-5 w-5 text-primary" />
                </div>
              </CardHeader>
            </Card>
          );
        })}
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Open Stage Queue</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {stageQueue.length === 0 && !isLoading ? (
              <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                No open property management cases.
              </div>
            ) : null}
            {stageQueue.map((item) => (
              <div
                key={item.stage}
                className="flex items-center justify-between rounded-md border p-3"
              >
                <span className="font-medium">{item.stage}</span>
                <Badge variant="secondary">{item.count}</Badge>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Property Records Health</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-3">
            <div className="rounded-md border p-4">
              <div className="text-sm text-muted-foreground">DMS records</div>
              <div className="mt-2 text-2xl font-semibold">
                {documents.length}
              </div>
            </div>
            <div className="rounded-md border p-4">
              <div className="text-sm text-muted-foreground">DMS issues</div>
              <div className="mt-2 text-2xl font-semibold">{dmsIssues}</div>
            </div>
            <div className="rounded-md border p-4">
              <div className="text-sm text-muted-foreground">Completed</div>
              <div className="mt-2 text-2xl font-semibold">
                {completedCases}
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Recent Property Management Cases</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {cases.slice(0, 8).map((item) => (
            <div key={item.id} className="rounded-md border p-3">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="font-medium">
                    {item.referenceNumber || item.title}
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {item.currentStageName}
                  </div>
                </div>
                <Badge variant={isOpen(item.status) ? 'secondary' : 'outline'}>
                  {item.status}
                </Badge>
              </div>
              <div className="mt-3">
                <Button asChild size="sm" variant="outline">
                  <Link href={`/estate/property-management/${item.entityType}`}>
                    Open workspace
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Link>
                </Button>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
