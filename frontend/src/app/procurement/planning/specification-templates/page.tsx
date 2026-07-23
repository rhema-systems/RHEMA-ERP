'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import {
  BookTemplate,
  CheckCircle2,
  Clock3,
  FileText,
  Plus,
  RefreshCw,
  XCircle,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useAuth } from '@/hooks/use-auth';
import { procurementSpecificationTemplateStatusTone } from '@/lib/procurement-specification-template';
import { procurementSpecificationTemplateService } from '@/services/procurement-specification-template.service';
import type {
  ProcurementSpecificationTemplateKind,
  ProcurementSpecificationTemplateSearch,
  ProcurementSpecificationTemplateStatus,
} from '@/types/procurement-specification-template';

const statuses: ProcurementSpecificationTemplateStatus[] = [
  'Draft',
  'PendingApproval',
  'Published',
  'Retired',
];
const kinds: ProcurementSpecificationTemplateKind[] = [
  'Goods',
  'Works',
  'Services',
];
const date = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(
        new Date(value)
      )
    : '—';

export default function ProcurementSpecificationTemplatesPage() {
  const { hasRole } = useAuth();
  const [filters, setFilters] =
    useState<ProcurementSpecificationTemplateSearch>({ page: 1, pageSize: 25 });
  const canManage = [
    'SuperAdmin',
    'TenantAdmin',
    'TDC_PROCUREMENT_OFFICER',
    'TDC_SENIOR_PROCUREMENT_OFFICER',
    'TDC_HEAD_OF_PROCUREMENT',
  ].some(hasRole);
  const summary = useQuery({
    queryKey: ['procurement-specification-template-summary'],
    queryFn: procurementSpecificationTemplateService.summary,
  });
  const register = useQuery({
    queryKey: ['procurement-specification-templates', filters],
    queryFn: () => procurementSpecificationTemplateService.search(filters),
  });
  const loadError = summary.isError || register.isError;
  const summaryCards: Array<[string, number, LucideIcon]> = [
    ['Template families', summary.data?.templateFamilyCount ?? 0, BookTemplate],
    ['Drafts', summary.data?.draftCount ?? 0, FileText],
    ['Pending approval', summary.data?.pendingApprovalCount ?? 0, Clock3],
    ['Published', summary.data?.publishedCount ?? 0, CheckCircle2],
    ['Effective now', summary.data?.effectiveCount ?? 0, CheckCircle2],
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-start">
        <div>
          <h1 className="text-3xl font-bold">
            Specification &amp; TOR templates
          </h1>
          <p className="mt-1 max-w-4xl text-muted-foreground">
            Versioned standard content for Goods, Works, and Services, governed
            through Draft, independent approval, publication, replacement,
            retirement, and immutable evidence history.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            onClick={() =>
              void Promise.all([summary.refetch(), register.refetch()])
            }
          >
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
          {canManage && (
            <Button asChild>
              <Link href="/procurement/planning/specification-templates/new">
                <Plus className="mr-2 h-4 w-4" /> New Draft
              </Link>
            </Button>
          )}
        </div>
      </div>

      {loadError && (
        <Alert variant="destructive">
          <XCircle className="h-4 w-4" />
          <AlertTitle>Template register could not be loaded</AlertTitle>
          <AlertDescription>
            Check the tenant session and procurement role, then retry.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {summaryCards.map(([label, value, Icon]) => (
          <Card key={String(label)}>
            <CardContent className="flex items-center justify-between p-5">
              <div>
                <p className="text-sm text-muted-foreground">{String(label)}</p>
                <p className="mt-1 text-2xl font-semibold">{String(value)}</p>
              </div>
              <Icon className="h-5 w-5 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Controlled template register</CardTitle>
          <CardDescription>
            Open a revision to review all eight mandatory sections, lifecycle
            actions, workflow state, and evidence timeline.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_180px_190px_170px]">
            <Input
              aria-label="Search specification templates"
              placeholder="Search code, name, or description"
              value={filters.search ?? ''}
              onChange={(event) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  search: event.target.value || undefined,
                }))
              }
            />
            <Select
              value={filters.kind ?? 'all'}
              onValueChange={(kind) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  kind:
                    kind === 'all'
                      ? undefined
                      : (kind as ProcurementSpecificationTemplateKind),
                }))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="All types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All types</SelectItem>
                {kinds.map((kind) => (
                  <SelectItem key={kind} value={kind}>
                    {kind}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={filters.status ?? 'all'}
              onValueChange={(status) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  status:
                    status === 'all'
                      ? undefined
                      : (status as ProcurementSpecificationTemplateStatus),
                }))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {statuses.map((status) => (
                  <SelectItem key={status} value={status}>
                    {status === 'PendingApproval' ? 'Pending approval' : status}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button
              variant={filters.effectiveOnly ? 'default' : 'outline'}
              onClick={() =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  effectiveOnly: !current.effectiveOnly,
                }))
              }
            >
              Effective only
            </Button>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Template</TableHead>
                  <TableHead>Type / version</TableHead>
                  <TableHead>Effective period</TableHead>
                  <TableHead>Control</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="text-right">Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(register.data?.items ?? []).map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>
                      <div className="font-medium">{item.templateCode}</div>
                      <div className="max-w-80 truncate text-xs text-muted-foreground">
                        {item.name}
                      </div>
                    </TableCell>
                    <TableCell>
                      {item.kind} · v{item.version}
                      {item.isDefault && (
                        <Badge variant="secondary" className="ml-2">
                          Default
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell>
                      <div>
                        {date(item.effectiveFromUtc)} –{' '}
                        {date(item.effectiveToUtc)}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {item.isEffective
                          ? 'Effective now'
                          : 'Not currently effective'}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="text-sm">
                        {item.workflowDefinitionName ??
                          'Direct independent approval'}
                      </div>
                      <div className="text-xs text-muted-foreground">
                        {item.isPublicationReady
                          ? 'All sections complete'
                          : 'Draft sections incomplete'}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={procurementSpecificationTemplateStatusTone(
                          item.status
                        )}
                      >
                        {item.status === 'PendingApproval'
                          ? 'Pending approval'
                          : item.status}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <Button asChild size="sm" variant="outline">
                        <Link
                          href={`/procurement/planning/specification-templates/${item.id}`}
                        >
                          Open
                        </Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
                {!register.isLoading && !register.data?.items.length && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="py-10 text-center text-muted-foreground"
                    >
                      No specification templates match the current filters.
                    </TableCell>
                  </TableRow>
                )}
                {register.isLoading && (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="py-10 text-center text-muted-foreground"
                    >
                      Loading templates…
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
          <Pagination
            currentPage={register.data?.page ?? 1}
            totalPages={Math.max(
              1,
              Math.ceil(
                (register.data?.totalCount ?? 0) /
                  (register.data?.pageSize ?? 25)
              )
            )}
            totalItems={register.data?.totalCount ?? 0}
            pageSize={register.data?.pageSize ?? 25}
            onPageChange={(page) =>
              setFilters((current) => ({ ...current, page }))
            }
            onPageSizeChange={(pageSize) =>
              setFilters((current) => ({ ...current, page: 1, pageSize }))
            }
          />
        </CardContent>
      </Card>
    </div>
  );
}
