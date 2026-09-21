'use client';

import React from 'react';
import Link from 'next/link';
import {
  ArrowLeft,
  ArrowRight,
  Eye,
  Filter,
  Loader2,
  Search,
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
import { Input } from '@/components/ui/input';
import { Pagination } from '@/components/ui/pagination';
import { usePaginatedItems } from '@/hooks/use-paginated-items';
import { getStatusBadgeClassName } from '@/lib/status-badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  CentralDocumentViewerDialog,
  type CentralDocumentViewerFile,
} from '@/components/document-management/CentralDocumentViewerDialog';
import {
  documentManagementService,
  type CentralDocumentRecord,
} from '@/services/document-management.service';

const moduleFilters = [
  'All modules',
  'Estate / Facilities',
  'Estate / Property Management',
  'Project Management',
  'Maintenance Management',
  'Finance',
  'Finance AR',
  'Finance AP',
  'Helpdesk / Complaint Management',
  'HR / Payroll',
  'Procurement',
  'Legal',
];

function includesText(value: string | null | undefined, query: string) {
  return (value || '').toLowerCase().includes(query);
}

function metadataBadgeVariant(status?: string | null) {
  if (status === 'Complete') return 'default';
  if (status === 'Missing template') return 'destructive';
  return 'outline';
}

export default function CentralDocumentRecordsPage() {
  const [records, setRecords] = React.useState<CentralDocumentRecord[]>([]);
  const [moduleFilter, setModuleFilter] = React.useState('All modules');
  const [search, setSearch] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [viewerFile, setViewerFile] =
    React.useState<CentralDocumentViewerFile | null>(null);

  React.useEffect(() => {
    let mounted = true;

    const load = async () => {
      setIsLoading(true);
      try {
        const data = await documentManagementService.getRecords(
          moduleFilter === 'All modules' ? undefined : moduleFilter
        );
        if (mounted) {
          setRecords(data);
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
  }, [moduleFilter]);

  const normalizedSearch = search.trim().toLowerCase();
  const visibleRecords = records.filter((record) => {
    if (!normalizedSearch) return true;
    return (
      includesText(record.documentReference, normalizedSearch) ||
      includesText(record.title, normalizedSearch) ||
      includesText(record.sourceModule, normalizedSearch) ||
      includesText(record.sourceLabel, normalizedSearch) ||
      includesText(record.sourceRecordReference, normalizedSearch) ||
      includesText(record.metadataTemplateCode, normalizedSearch) ||
      includesText(record.metadataCompleteness?.status, normalizedSearch)
    );
  });
  const recordPages = usePaginatedItems(visibleRecords, 10);

  return (
    <>
      <CentralDocumentViewerDialog
        file={viewerFile}
        open={Boolean(viewerFile)}
        enableAnnotations={false}
        onOpenChange={(open) => {
          if (!open) setViewerFile(null);
        }}
      />

      <div className="space-y-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="space-y-2">
            <Badge variant="outline" className="w-fit">
              Central DMS register
            </Badge>
            <div>
              <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
                Document Records
              </h1>
              <p className="mt-2 max-w-4xl text-sm text-muted-foreground">
                Central records published from source modules into DMS, with
                source labels, repository state, versions, annotations, access,
                and retention status.
              </p>
            </div>
          </div>
          <Button asChild variant="outline">
            <Link href="/document-management">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Central DMS
            </Link>
          </Button>
        </div>

        <Card className="border-border bg-card text-card-foreground">
          <CardContent className="grid gap-3 pt-6 md:grid-cols-[260px_1fr]">
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm font-medium">
                <Filter className="h-4 w-4" />
                Source module
              </div>
              <Select value={moduleFilter} onValueChange={setModuleFilter}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {moduleFilters.map((module) => (
                    <SelectItem key={module} value={module}>
                      {module}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-sm font-medium">
                <Search className="h-4 w-4" />
                Search register
              </div>
              <Input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search reference, title, source, template, or record"
              />
            </div>
          </CardContent>
        </Card>

        {isLoading ? (
          <Card className="border-border bg-card text-card-foreground">
            <CardContent className="flex items-center justify-center gap-2 py-10 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading DMS records
            </CardContent>
          </Card>
        ) : null}

        <Card className="border-border bg-card text-card-foreground">
          <CardHeader>
            <CardTitle>Records register</CardTitle>
            <CardDescription>
              Select a record to preview the file or open its full DMS details.
            </CardDescription>
          </CardHeader>
          <CardContent>
          {!isLoading && visibleRecords.length === 0 ? (
            <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
              No DMS records found
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[980px] text-sm">
                <thead className="border-b bg-muted/40 text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2 font-medium">Reference</th>
                    <th className="px-3 py-2 font-medium">Title</th>
                    <th className="px-3 py-2 font-medium">Source</th>
                    <th className="px-3 py-2 font-medium">Metadata</th>
                    <th className="px-3 py-2 font-medium">Repository</th>
                    <th className="px-3 py-2 font-medium">Version</th>
                    <th className="px-3 py-2 text-right font-medium">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {recordPages.items.map((record) => {
                    const metadataCompleteness = record.metadataCompleteness;

                    return (
                      <tr key={record.id}>
                        <td className="px-3 py-3 align-top">
                          <Badge variant="outline">
                            {record.documentReference}
                          </Badge>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <div className="font-medium">{record.title}</div>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {record.sourceRecordReference ||
                              record.sourceRecordId ||
                              'No source record reference'}
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <Badge variant="secondary">
                            {record.sourceModule}
                          </Badge>
                          <div className="mt-1 max-w-[16rem] truncate text-xs text-muted-foreground">
                            {record.sourceLabel}
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <Badge
                            variant={metadataBadgeVariant(
                              metadataCompleteness?.status
                            )}
                            className={getStatusBadgeClassName(
                              metadataCompleteness?.status
                            )}
                          >
                            {metadataCompleteness
                              ? `${metadataCompleteness.status} ${metadataCompleteness.percentage}%`
                              : 'Not checked'}
                          </Badge>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {record.metadataTemplateCode || 'No template'}
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <Badge
                            variant="outline"
                            className={getStatusBadgeClassName(
                              record.repositoryStatus
                            )}
                          >
                            {record.repositoryStatus}
                          </Badge>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <Badge
                            variant="outline"
                            className={getStatusBadgeClassName(
                              record.currentVersion || 'No version'
                            )}
                          >
                            {record.currentVersion || 'No version'}
                          </Badge>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {record.annotationStatus}
                          </div>
                        </td>
                        <td className="px-3 py-3 text-right align-top">
                          <div className="flex justify-end gap-2">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() =>
                                setViewerFile({
                                  title: record.title,
                                  fileName:
                                    record.currentVersion ||
                                    record.documentReference,
                                  repositoryPath: record.repositoryPath,
                                  externalDocumentUrl:
                                    record.externalDocumentUrl,
                                  sourceLabel: record.sourceLabel,
                                  version: record.currentVersion,
                                })
                              }
                            >
                              <Eye className="mr-2 h-4 w-4" />
                              View
                            </Button>
                            <Button asChild size="sm">
                              <Link
                                href={`/document-management/records/${record.id}`}
                              >
                                Open
                                <ArrowRight className="ml-2 h-4 w-4" />
                              </Link>
                            </Button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
          {visibleRecords.length > recordPages.pageSize ? (
            <Pagination
              currentPage={recordPages.currentPage}
              totalPages={recordPages.totalPages}
              totalItems={recordPages.totalItems}
              pageSize={recordPages.pageSize}
              onPageChange={recordPages.setCurrentPage}
            />
          ) : null}
          </CardContent>
        </Card>
      </div>
    </>
  );
}
