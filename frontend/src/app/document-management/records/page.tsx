'use client';

import React from 'react';
import Link from 'next/link';
import {
  ArrowLeft,
  ArrowRight,
  Eye,
  FileText,
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

        <div className="space-y-3">
          {!isLoading && visibleRecords.length === 0 ? (
            <Card className="border-border bg-card text-card-foreground">
              <CardHeader>
                <CardTitle>No DMS records found</CardTitle>
              </CardHeader>
            </Card>
          ) : null}

          {recordPages.items.map((record) => {
            const metadataCompleteness = record.metadataCompleteness;

            return (
              <Card
                key={record.id}
                className="border-border bg-card text-card-foreground"
              >
                <CardContent className="p-4">
                  <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <FileText className="h-4 w-4 text-primary" />
                        <Badge variant="outline">
                          {record.documentReference}
                        </Badge>
                        <Badge variant="secondary">{record.sourceModule}</Badge>
                      </div>
                      <h2 className="mt-3 text-base font-semibold">
                        {record.title}
                      </h2>
                      <p className="mt-1 text-sm text-muted-foreground">
                        {record.sourceRecordReference ||
                          record.sourceRecordId ||
                          'No source record reference'}
                      </p>
                      <p className="mt-1 text-xs text-muted-foreground">
                        {record.sourceLabel}
                      </p>
                    </div>

                    <div className="flex flex-wrap gap-2 xl:justify-end">
                      <Badge variant="outline">
                        {record.metadataTemplateCode || 'No template'}
                      </Badge>
                      <Badge
                        variant={metadataBadgeVariant(
                          metadataCompleteness?.status
                        )}
                      >
                        Metadata:{' '}
                        {metadataCompleteness
                          ? `${metadataCompleteness.status} ${metadataCompleteness.percentage}%`
                          : 'Not checked'}
                      </Badge>
                      <Badge variant="outline">{record.repositoryStatus}</Badge>
                      <Badge variant="outline">
                        {record.currentVersion || 'No version'}
                      </Badge>
                      <Badge variant="outline">{record.annotationStatus}</Badge>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() =>
                          setViewerFile({
                            title: record.title,
                            fileName:
                              record.currentVersion || record.documentReference,
                            repositoryPath: record.repositoryPath,
                            externalDocumentUrl: record.externalDocumentUrl,
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
                          Open record
                          <ArrowRight className="ml-2 h-4 w-4" />
                        </Link>
                      </Button>
                    </div>
                  </div>
                </CardContent>
              </Card>
            );
          })}
          {visibleRecords.length > recordPages.pageSize ? (
            <Pagination
              currentPage={recordPages.currentPage}
              totalPages={recordPages.totalPages}
              totalItems={recordPages.totalItems}
              pageSize={recordPages.pageSize}
              onPageChange={recordPages.setCurrentPage}
            />
          ) : null}
        </div>
      </div>
    </>
  );
}
