'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CheckCircle2,
  Download,
  Eye,
  History,
  Send,
  Upload,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { DataTable, type DataTableColumn } from '@/components/ui/DataTable';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import type {
  QuantitySurveyEscalationLookupOption,
  QuantitySurveyIndexFamily,
} from '@/services/quantity-survey-escalation.service';
import {
  quantitySurveyPriceIndexImportService,
  saveQuantitySurveyPriceIndexTemplate,
  type QuantitySurveyPriceIndexImport,
} from '@/services/quantity-survey-price-index-import.service';

type LifecycleAction = 'submit' | 'approve' | 'reject';

interface Props {
  families: QuantitySurveyIndexFamily[];
  allowedIndexSources: QuantitySurveyEscalationLookupOption[];
  authorityRoles: QuantitySurveyEscalationLookupOption[];
  importFormat?: string | null;
  canManage: boolean;
  canApprove: boolean;
  canAudit: boolean;
}

const formatDate = (value?: string | null) =>
  value
    ? new Intl.DateTimeFormat('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }).format(new Date(value))
    : '—';

const statusVariant = (status: string) =>
  status === 'Approved'
    ? 'secondary'
    : status === 'Rejected' || status === 'Invalid'
      ? 'destructive'
      : 'default';

const sourceLabel = (value: string | number) =>
  ({
    0: 'GSS PBCI',
    1: 'Roads and Infrastructure',
    2: 'Controlled manual import',
    GssPbci: 'GSS PBCI',
    RoadsInfrastructure: 'Roads and Infrastructure',
    ControlledManualImport: 'Controlled manual import',
  })[String(value)] ?? String(value).replace(/([a-z])([A-Z])/g, '$1 $2');

const sourceValue = (value: string | number) =>
  ({
    0: 'GssPbci',
    1: 'RoadsInfrastructure',
    2: 'ControlledManualImport',
  })[String(value)] ?? String(value);

export function QuantitySurveyPriceIndexImportPanel({
  families,
  allowedIndexSources,
  authorityRoles,
  importFormat,
  canManage,
  canApprove,
  canAudit,
}: Props) {
  const client = useQueryClient();
  const { toast } = useToast();
  const allowedSourceValues = useMemo(
    () => new Set(allowedIndexSources.map((value) => value.value)),
    [allowedIndexSources]
  );
  const activeFamilies = useMemo(
    () =>
      families.filter(
        (value) =>
          value.isActive && allowedSourceValues.has(sourceValue(value.source))
      ),
    [allowedSourceValues, families]
  );
  const [search, setSearch] = useState('');
  const [familyFilter, setFamilyFilter] = useState('all');
  const [statusFilter, setStatusFilter] = useState('all');
  const [pagination, setPagination] = useState({ pageIndex: 0, pageSize: 20 });
  const [familyId, setFamilyId] = useState('');
  const [authorityRoleId, setAuthorityRoleId] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [reason, setReason] = useState('');
  const [stageRequestId, setStageRequestId] = useState(() =>
    crypto.randomUUID()
  );
  const [fileInputKey, setFileInputKey] = useState(0);
  const [detail, setDetail] = useState<QuantitySurveyPriceIndexImport | null>(
    null
  );
  const [lifecycle, setLifecycle] = useState<{
    value: QuantitySurveyPriceIndexImport;
    action: LifecycleAction;
  } | null>(null);
  const [lifecycleReason, setLifecycleReason] = useState('');
  const [historyId, setHistoryId] = useState<string | null>(null);

  const imports = useQuery({
    queryKey: [
      'quantity-survey-price-index-imports',
      search,
      familyFilter,
      statusFilter,
      pagination.pageIndex,
      pagination.pageSize,
    ],
    queryFn: () =>
      quantitySurveyPriceIndexImportService.list({
        search: search.trim() || undefined,
        indexFamilyId: familyFilter === 'all' ? undefined : familyFilter,
        status: statusFilter === 'all' ? undefined : statusFilter,
        page: pagination.pageIndex + 1,
        pageSize: pagination.pageSize,
      }),
  });
  const history = useQuery({
    queryKey: ['quantity-survey-price-index-import-history', historyId],
    queryFn: () => {
      if (!historyId) throw new Error('Select an import first.');
      return quantitySurveyPriceIndexImportService.history(historyId);
    },
    enabled: Boolean(historyId && canAudit),
  });

  const refresh = async () => {
    await Promise.all([
      client.invalidateQueries({
        queryKey: ['quantity-survey-price-index-imports'],
      }),
      client.invalidateQueries({
        queryKey: ['quantity-survey-price-index-import-history'],
      }),
    ]);
  };

  const downloadTemplate = useMutation({
    mutationFn: async () => {
      const selected = activeFamilies.find((value) => value.id === familyId);
      if (!selected) throw new Error('Select a controlled index family first.');
      const blob = await quantitySurveyPriceIndexImportService.template(
        selected.id
      );
      saveQuantitySurveyPriceIndexTemplate(blob, selected.code, importFormat);
    },
    onError: (error) =>
      toast({
        title: 'Template could not be downloaded',
        description: error instanceof Error ? error.message : undefined,
        variant: 'destructive',
      }),
  });

  const stage = useMutation({
    mutationFn: async () => {
      if (!file) throw new Error('Select a source file.');
      return quantitySurveyPriceIndexImportService.stage(
        familyId,
        file,
        authorityRoleId,
        reason,
        stageRequestId
      );
    },
    onSuccess: async (value) => {
      setFile(null);
      setReason('');
      setStageRequestId(crypto.randomUUID());
      setFileInputKey((current) => current + 1);
      setDetail(value);
      await refresh();
      toast({
        title:
          value.status === 'Invalid'
            ? 'Source staged with validation errors'
            : 'Price-index source staged',
        description:
          value.status === 'Invalid'
            ? 'Open the record to review each validation error.'
            : `${value.lineCount} row(s) retained with central DMS evidence.`,
        variant: value.status === 'Invalid' ? 'destructive' : 'success',
      });
    },
    onError: (error) =>
      toast({
        title: 'Price-index source could not be staged',
        description: error instanceof Error ? error.message : undefined,
        variant: 'destructive',
      }),
  });

  const lifecycleMutation = useMutation({
    mutationFn: () => {
      if (!lifecycle) throw new Error('Select an import action.');
      return quantitySurveyPriceIndexImportService.lifecycle(
        lifecycle.value.id,
        lifecycle.action,
        lifecycle.value.rowVersion,
        lifecycleReason
      );
    },
    onSuccess: async (value) => {
      setLifecycle(null);
      setLifecycleReason('');
      setDetail(value);
      await refresh();
      toast({
        title: `Price-index import ${value.status}`,
        variant: 'success',
      });
    },
    onError: (error) =>
      toast({
        title: 'Price-index action could not be completed',
        description: error instanceof Error ? error.message : undefined,
        variant: 'destructive',
      }),
  });

  const columns = useMemo<DataTableColumn<QuantitySurveyPriceIndexImport>[]>(
    () => [
      {
        accessorKey: 'indexFamilyCode',
        header: 'Index family',
        cell: ({ row }) => (
          <div>
            <div className="font-medium">{row.original.indexFamilyCode}</div>
            <div className="text-xs text-muted-foreground">
              {row.original.indexFamilyName}
            </div>
          </div>
        ),
      },
      {
        accessorKey: 'indexSource',
        header: 'Source',
        cell: ({ row }) => sourceLabel(row.original.indexSource),
      },
      {
        accessorKey: 'originalFileName',
        header: 'Source file',
      },
      {
        accessorKey: 'lineCount',
        header: 'Rows',
        cell: ({ row }) => (
          <span>
            {row.original.lineCount} / {row.original.errorCount} error(s)
          </span>
        ),
      },
      {
        accessorKey: 'status',
        header: 'Status',
        cell: ({ row }) => (
          <Badge variant={statusVariant(row.original.status)}>
            {row.original.status}
          </Badge>
        ),
      },
      {
        accessorKey: 'authorityRoleName',
        header: 'Approval authority',
      },
      {
        accessorKey: 'preparedAt',
        header: 'Prepared',
        cell: ({ row }) => formatDate(row.original.preparedAt),
      },
    ],
    []
  );

  const selectedFamily = activeFamilies.find((value) => value.id === familyId);
  const expectedExtension = importFormat === 'CSV' ? '.csv' : '.xlsx';
  const manualImportSupported = ['Controlled Excel', 'CSV'].includes(
    importFormat
  );
  const stageValid = Boolean(
    manualImportSupported &&
    familyId &&
    authorityRoleId &&
    file &&
    file.name.toLowerCase().endsWith(expectedExtension) &&
    reason.trim()
  );

  return (
    <div className="space-y-3">
      {canManage ? (
        <Card>
          <CardContent className="pt-5">
            {importFormat === undefined ? (
              <div className="rounded-md border p-3 text-sm text-muted-foreground">
                Loading the effective QS-DEC-006 import policy…
              </div>
            ) : importFormat === null ? (
              <div className="rounded-md border border-red-300 bg-red-50 p-3 text-sm text-red-900 dark:border-red-800 dark:bg-red-950/40 dark:text-red-100">
                The effective QS-DEC-006 import policy is unavailable. Resolve
                the policy error before staging index data.
              </div>
            ) : !manualImportSupported ? (
              <div className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-100">
                QS-DEC-006 currently requires an API source. A provider adapter
                and governed provider schema must be configured before imports
                can be staged; manual files are intentionally disabled.
              </div>
            ) : null}
            <div className="grid gap-3 lg:grid-cols-[minmax(220px,1.2fr)_minmax(220px,1fr)_minmax(220px,1.2fr)_minmax(260px,1.5fr)_auto] lg:items-end">
              <div className="space-y-1">
                <Label>Index family</Label>
                <Select
                  value={familyId || undefined}
                  onValueChange={(value) => {
                    setFamilyId(value);
                    setStageRequestId(crypto.randomUUID());
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select controlled family" />
                  </SelectTrigger>
                  <SelectContent>
                    {activeFamilies.map((value) => (
                      <SelectItem key={value.id} value={value.id}>
                        {value.code} · {value.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label>Approval authority</Label>
                <Select
                  value={authorityRoleId || undefined}
                  onValueChange={(value) => {
                    setAuthorityRoleId(value);
                    setStageRequestId(crypto.randomUUID());
                  }}
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select authority role" />
                  </SelectTrigger>
                  <SelectContent>
                    {authorityRoles.map((value) => (
                      <SelectItem key={value.value} value={value.value}>
                        {value.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-1">
                <Label>Controlled source ({expectedExtension})</Label>
                <Input
                  key={fileInputKey}
                  type="file"
                  accept={expectedExtension}
                  disabled={!manualImportSupported}
                  onChange={(event) => {
                    setFile(event.target.files?.[0] ?? null);
                    setStageRequestId(crypto.randomUUID());
                  }}
                />
              </div>
              <div className="space-y-1">
                <Label>Staging reason</Label>
                <Input
                  value={reason}
                  maxLength={1000}
                  placeholder="Reason and publisher bulletin context"
                  onChange={(event) => {
                    setReason(event.target.value);
                    setStageRequestId(crypto.randomUUID());
                  }}
                />
              </div>
              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="outline"
                  disabled={
                    !manualImportSupported ||
                    !selectedFamily ||
                    downloadTemplate.isPending
                  }
                  onClick={() => downloadTemplate.mutate()}
                >
                  <Download className="mr-2 h-4 w-4" /> Template
                </Button>
                <Button
                  type="button"
                  disabled={!stageValid || stage.isPending}
                  onClick={() => stage.mutate()}
                >
                  <Upload className="mr-2 h-4 w-4" /> Stage
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : null}

      <div className="grid gap-3 rounded-lg border bg-card p-3 md:grid-cols-[minmax(220px,2fr)_minmax(220px,1fr)_180px] md:items-end">
        <div className="space-y-1">
          <Label>Search</Label>
          <Input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value);
              setPagination((current) => ({ ...current, pageIndex: 0 }));
            }}
            placeholder="Family or source file…"
          />
        </div>
        <div className="space-y-1">
          <Label>Index family</Label>
          <Select
            value={familyFilter}
            onValueChange={(value) => {
              setFamilyFilter(value);
              setPagination((current) => ({ ...current, pageIndex: 0 }));
            }}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All index families</SelectItem>
              {activeFamilies.map((value) => (
                <SelectItem key={value.id} value={value.id}>
                  {value.code} · {value.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>Status</Label>
          <Select
            value={statusFilter}
            onValueChange={(value) => {
              setStatusFilter(value);
              setPagination((current) => ({ ...current, pageIndex: 0 }));
            }}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {[
                'Staged',
                'Invalid',
                'PendingApproval',
                'Approved',
                'Rejected',
              ].map((value) => (
                <SelectItem key={value} value={value}>
                  {value}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <DataTable
        key={`${search}:${familyFilter}:${statusFilter}`}
        compact
        title="Controlled price-index imports"
        description={`${imports.data?.totalCount ?? 0} source batch(es)`}
        data={imports.data?.items ?? []}
        columns={columns}
        loading={imports.isLoading}
        error={imports.error ? 'Failed to load price-index imports.' : null}
        enableExport
        exportFormats={['csv', 'excel']}
        exportFileName="quantity-survey-price-index-imports"
        pageSize={pagination.pageSize}
        pageSizeOptions={[20, 50, 100]}
        serverSidePagination
        totalRows={imports.data?.totalCount ?? 0}
        onPaginationChange={setPagination}
        emptyStateMessage="No price-index imports match the selected filters."
        rowActions={[
          {
            id: 'view',
            label: 'View import',
            icon: Eye,
            onClick: (row) => setDetail(row.original),
          },
          ...(canManage
            ? [
                {
                  id: 'submit',
                  label: 'Submit for approval',
                  icon: Send,
                  hidden: (row: { original: QuantitySurveyPriceIndexImport }) =>
                    row.original.status !== 'Staged',
                  onClick: (row: {
                    original: QuantitySurveyPriceIndexImport;
                  }) => {
                    setLifecycle({ value: row.original, action: 'submit' });
                    setLifecycleReason('');
                  },
                },
              ]
            : []),
          ...(canApprove
            ? [
                {
                  id: 'approve',
                  label: 'Approve',
                  icon: CheckCircle2,
                  hidden: (row: { original: QuantitySurveyPriceIndexImport }) =>
                    row.original.status !== 'PendingApproval',
                  onClick: (row: {
                    original: QuantitySurveyPriceIndexImport;
                  }) => {
                    setLifecycle({ value: row.original, action: 'approve' });
                    setLifecycleReason('');
                  },
                },
                {
                  id: 'reject',
                  label: 'Reject',
                  icon: XCircle,
                  hidden: (row: { original: QuantitySurveyPriceIndexImport }) =>
                    row.original.status !== 'PendingApproval',
                  onClick: (row: {
                    original: QuantitySurveyPriceIndexImport;
                  }) => {
                    setLifecycle({ value: row.original, action: 'reject' });
                    setLifecycleReason('');
                  },
                },
              ]
            : []),
          ...(canAudit
            ? [
                {
                  id: 'history',
                  label: 'Audit history',
                  icon: History,
                  onClick: (row: {
                    original: QuantitySurveyPriceIndexImport;
                  }) => setHistoryId(row.original.id),
                },
              ]
            : []),
        ]}
      />

      <Dialog
        open={Boolean(detail)}
        onOpenChange={(open) => !open && setDetail(null)}
      >
        <DialogContent className="max-h-[92vh] max-w-5xl">
          <DialogHeader>
            <DialogTitle>
              {detail?.indexFamilyCode} price-index import
            </DialogTitle>
            <DialogDescription>
              {detail?.evidenceLabel} · {detail?.originalFileName}
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[70vh] pr-4">
            <div className="space-y-4">
              <div className="grid gap-3 text-sm md:grid-cols-4">
                <div>
                  <span className="text-muted-foreground">Status</span>
                  <div>
                    <Badge variant={statusVariant(detail?.status ?? '')}>
                      {detail?.status}
                    </Badge>
                  </div>
                </div>
                <div>
                  <span className="text-muted-foreground">Source</span>
                  <div>{detail ? sourceLabel(detail.indexSource) : '—'}</div>
                </div>
                <div>
                  <span className="text-muted-foreground">Format</span>
                  <div>{detail?.importFormat}</div>
                </div>
                <div>
                  <span className="text-muted-foreground">Authority</span>
                  <div>{detail?.authorityRoleName}</div>
                </div>
              </div>
              {detail?.issues.length ? (
                <div className="space-y-2">
                  <h3 className="font-semibold text-destructive">
                    Validation errors
                  </h3>
                  {detail.issues.map((issue, index) => (
                    <div
                      key={`${issue.code}-${index}`}
                      className="rounded-md border border-destructive/30 p-2 text-sm"
                    >
                      <span className="font-medium">{issue.code}</span> ·{' '}
                      {issue.rowNumber ? `Row ${issue.rowNumber}, ` : ''}
                      {issue.field}: {issue.message}
                    </div>
                  ))}
                </div>
              ) : null}
              <div className="overflow-x-auto rounded-md border">
                <table className="w-full text-sm">
                  <thead className="bg-muted/50 text-left">
                    <tr>
                      <th className="p-2">Period</th>
                      <th className="p-2">Value</th>
                      <th className="p-2">Publication</th>
                      <th className="p-2">Source reference</th>
                      <th className="p-2">Version</th>
                      <th className="p-2">Current</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail?.values.map((value) => (
                      <tr key={value.id} className="border-t">
                        <td className="p-2">{value.indexPeriod.slice(0, 7)}</td>
                        <td className="p-2 tabular-nums">
                          {value.indexValue.toLocaleString(undefined, {
                            maximumFractionDigits: 6,
                          })}
                        </td>
                        <td className="p-2">
                          {value.publicationDate.slice(0, 10)}
                        </td>
                        <td className="p-2">{value.sourceReference}</td>
                        <td className="p-2">v{value.version}</td>
                        <td className="p-2">
                          {value.isCurrent ? 'Yes' : 'No'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </ScrollArea>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(lifecycle)}
        onOpenChange={(open) => !open && setLifecycle(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle className="capitalize">
              {lifecycle?.action} price-index import
            </DialogTitle>
            <DialogDescription>
              Maker-checker and the configured shared workflow are enforced by
              the API and database.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-1">
            <Label>Reason</Label>
            <Textarea
              value={lifecycleReason}
              maxLength={1000}
              onChange={(event) => setLifecycleReason(event.target.value)}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setLifecycle(null)}>
              Cancel
            </Button>
            <Button
              disabled={!lifecycleReason.trim() || lifecycleMutation.isPending}
              onClick={() => lifecycleMutation.mutate()}
            >
              Confirm
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={Boolean(historyId)}
        onOpenChange={(open) => !open && setHistoryId(null)}
      >
        <DialogContent className="max-h-[92vh] max-w-4xl">
          <DialogHeader>
            <DialogTitle>Price-index import history</DialogTitle>
            <DialogDescription>
              Append-only actor, role, reason, correlation and before/after
              evidence.
            </DialogDescription>
          </DialogHeader>
          <ScrollArea className="max-h-[70vh] pr-4">
            <div className="space-y-3">
              {history.data?.map((value) => (
                <Card key={value.id}>
                  <CardContent className="space-y-1 pt-4 text-sm">
                    <div className="flex justify-between gap-3">
                      <span className="font-semibold">{value.action}</span>
                      <span>{formatDate(value.createdAt)}</span>
                    </div>
                    <div>
                      {value.actorName} ·{' '}
                      {value.actorRoles || 'No role snapshot'}
                    </div>
                    <div className="text-muted-foreground">
                      {value.reason || 'No reason'} · Correlation{' '}
                      {value.correlationId}
                    </div>
                  </CardContent>
                </Card>
              ))}
              {!history.isLoading && !history.data?.length ? (
                <p className="text-sm text-muted-foreground">
                  No audit history is available.
                </p>
              ) : null}
            </div>
          </ScrollArea>
        </DialogContent>
      </Dialog>
    </div>
  );
}
