'use client';

import Link from 'next/link';
import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  BookOpenCheck,
  CheckCircle2,
  Clock3,
  FileClock,
  Loader2,
  Plus,
  RefreshCw,
  Search,
  ShieldCheck,
} from 'lucide-react';
import { toast } from 'sonner';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import {
  procurementMethodLabel,
  tenderDocumentTemplateStatusLabel,
  validateTenderDocumentTemplate,
} from '@/lib/procurement-tender-document';
import { procurementTenderDocumentService as service } from '@/services/procurement-tender-document.service';
import type {
  ProcurementTenderDocumentSearch,
  ProcurementTenderDocumentTemplateStatus,
  SaveProcurementTenderDocumentTemplate,
} from '@/types/procurement-tender-document';
import type { ProcurementMethodType } from '@/types/procurement-policy';

const statuses: ProcurementTenderDocumentTemplateStatus[] = [
  'Draft',
  'PendingApproval',
  'Published',
  'Retired',
];

const methods = Object.keys(procurementMethodLabel) as ProcurementMethodType[];

const localInput = (value: Date) => {
  const shifted = new Date(value.getTime() - value.getTimezoneOffset() * 60_000);
  return shifted.toISOString().slice(0, 16);
};

const emptyForm = (): SaveProcurementTenderDocumentTemplate => ({
  templateCode: '',
  name: '',
  description: '',
  documentTypeCode: 'TENDER-DOCUMENT',
  effectiveFromUtc: localInput(new Date(Date.now() + 60 * 60 * 1000)),
  policySetId: '',
  policySetCode: '',
  policySetVersion: 0,
  sourceConfigurationProfileId: '',
  contentReference: '',
  contentChecksumSha256: '',
  workflowDefinitionId: '',
  applicableMethods: [],
  changeSummary: '',
});

const formatDate = (value?: string) =>
  value
    ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(
        new Date(value)
      )
    : '—';

export default function TenderDocumentTemplatesPage() {
  const queryClient = useQueryClient();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('procurement.tender.administer');
  const [filters, setFilters] = useState<ProcurementTenderDocumentSearch>({
    page: 1,
    pageSize: 25,
  });
  const [createOpen, setCreateOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<SaveProcurementTenderDocumentTemplate>(
    emptyForm()
  );

  const summary = useQuery({
    queryKey: ['procurement-tender-document-template-summary'],
    queryFn: service.templateSummary,
  });
  const templates = useQuery({
    queryKey: ['procurement-tender-document-templates', filters],
    queryFn: () => service.searchTemplates(filters),
  });
  const workflows = useQuery({
    queryKey: ['procurement-tender-document-workflows'],
    queryFn: service.workflowOptions,
  });
  const policies = useQuery({
    queryKey: ['procurement-tender-document-policies'],
    queryFn: service.policyOptions,
  });

  const selectedPolicy = useMemo(
    () => policies.data?.find((item) => item.id === form.policySetId),
    [form.policySetId, policies.data]
  );

  const choosePolicy = (policySetId: string) => {
    const policy = policies.data?.find((item) => item.id === policySetId);
    setForm((current) => ({
      ...current,
      policySetId,
      policySetCode: policy?.code ?? '',
      policySetVersion: policy?.version ?? 0,
      sourceConfigurationProfileId:
        policy?.sourceConfigurationProfileId ?? '',
    }));
  };

  const toggleMethod = (method: ProcurementMethodType, checked: boolean) =>
    setForm((current) => ({
      ...current,
      applicableMethods: checked
        ? [...new Set([...current.applicableMethods, method])]
        : current.applicableMethods.filter((item) => item !== method),
    }));

  const create = async () => {
    const validation = validateTenderDocumentTemplate(form);
    if (validation) {
      toast.error(validation);
      return;
    }
    const request: SaveProcurementTenderDocumentTemplate = {
      ...form,
      effectiveFromUtc: new Date(form.effectiveFromUtc).toISOString(),
      effectiveToUtc: form.effectiveToUtc
        ? new Date(form.effectiveToUtc).toISOString()
        : undefined,
    };
    try {
      setSaving(true);
      const created = await service.createTemplate(request);
      toast.success('Controlled tender-document Draft created');
      setCreateOpen(false);
      setForm(emptyForm());
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ['procurement-tender-document-template-summary'],
        }),
        queryClient.invalidateQueries({
          queryKey: ['procurement-tender-document-templates'],
        }),
      ]);
      window.location.href = `/procurement/tender-documents/${created.id}`;
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to create the controlled template'
      );
    } finally {
      setSaving(false);
    }
  };

  const loadError = summary.isError || templates.isError;
  const cards = [
    ['Families', summary.data?.templateFamilyCount ?? 0, BookOpenCheck],
    ['Drafts', summary.data?.draftCount ?? 0, FileClock],
    ['Pending approval', summary.data?.pendingApprovalCount ?? 0, Clock3],
    ['Published', summary.data?.publishedCount ?? 0, ShieldCheck],
    ['Effective now', summary.data?.effectiveCount ?? 0, CheckCircle2],
  ] as const;

  return (
    <div
      className="space-y-6 p-6"
      data-testid="tender-document-template-list-page"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">
            Controlled tender documents
          </h1>
          <p className="max-w-4xl text-sm text-muted-foreground">
            History-first reusable versions, exact policy and workflow lineage,
            approval, content checksums, and retained supersession history.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            onClick={() =>
              void Promise.all([summary.refetch(), templates.refetch()])
            }
          >
            <RefreshCw className="mr-2 h-4 w-4" /> Refresh
          </Button>
          {canManage && (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="mr-2 h-4 w-4" /> New controlled Draft
            </Button>
          )}
        </div>
      </div>

      {loadError && (
        <Alert variant="destructive">
          <AlertTitle>Document register could not be loaded</AlertTitle>
          <AlertDescription>
            Check the tenant session and procurement capability, then retry.
          </AlertDescription>
        </Alert>
      )}

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {cards.map(([label, value, Icon]) => (
          <Card key={label}>
            <CardContent className="flex items-center justify-between p-4">
              <div>
                <p className="text-xs uppercase text-muted-foreground">
                  {label}
                </p>
                <p className="mt-1 text-2xl font-semibold">{value}</p>
              </div>
              <Icon className="h-5 w-5 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Version and approval history</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-3 md:grid-cols-[minmax(0,1fr)_210px_210px]">
            <div className="relative">
              <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                aria-label="Search tender document templates"
                className="pl-9"
                placeholder="Search code, name, or document type"
                value={filters.search ?? ''}
                onChange={(event) =>
                  setFilters((current) => ({
                    ...current,
                    page: 1,
                    search: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <Select
              value={filters.status ?? 'all'}
              onValueChange={(value) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  status:
                    value === 'all'
                      ? undefined
                      : (value as ProcurementTenderDocumentTemplateStatus),
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
                    {tenderDocumentTemplateStatusLabel[status]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={filters.method ?? 'all'}
              onValueChange={(value) =>
                setFilters((current) => ({
                  ...current,
                  page: 1,
                  method:
                    value === 'all'
                      ? undefined
                      : (value as ProcurementMethodType),
                }))
              }
            >
              <SelectTrigger>
                <SelectValue placeholder="All methods" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All methods</SelectItem>
                {methods.map((method) => (
                  <SelectItem key={method} value={method}>
                    {procurementMethodLabel[method]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Template</TableHead>
                  <TableHead>Version / type</TableHead>
                  <TableHead>Applicable methods</TableHead>
                  <TableHead>Policy lineage</TableHead>
                  <TableHead>Effective period</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {(templates.data?.items ?? []).map((item) => (
                  <TableRow key={item.id}>
                    <TableCell>
                      <div className="font-medium">{item.templateCode}</div>
                      <div className="max-w-64 truncate text-xs text-muted-foreground">
                        {item.name}
                      </div>
                    </TableCell>
                    <TableCell>
                      v{item.version} · {item.documentTypeCode}
                    </TableCell>
                    <TableCell>
                      <div className="flex max-w-72 flex-wrap gap-1">
                        {item.applicableMethods.map((method) => (
                          <Badge key={method} variant="outline">
                            {procurementMethodLabel[method] ?? method}
                          </Badge>
                        ))}
                      </div>
                    </TableCell>
                    <TableCell>
                      {item.policySetCode} · v{item.policySetVersion}
                    </TableCell>
                    <TableCell className="text-xs">
                      {formatDate(item.effectiveFromUtc)} –{' '}
                      {formatDate(item.effectiveToUtc)}
                      <div className="text-muted-foreground">
                        {item.isEffective
                          ? 'Effective now'
                          : 'Historical or future'}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={
                          item.status === 'Published'
                            ? 'default'
                            : item.status === 'Retired'
                              ? 'outline'
                              : 'secondary'
                        }
                      >
                        {tenderDocumentTemplateStatusLabel[item.status]}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      <Button asChild size="sm" variant="outline">
                        <Link href={`/procurement/tender-documents/${item.id}`}>
                          Open history
                        </Link>
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
                {templates.isLoading && (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="py-10 text-center text-muted-foreground"
                    >
                      <Loader2 className="mx-auto h-5 w-5 animate-spin" />
                    </TableCell>
                  </TableRow>
                )}
                {!templates.isLoading && !templates.data?.items.length && (
                  <TableRow>
                    <TableCell
                      colSpan={7}
                      className="py-10 text-center text-muted-foreground"
                    >
                      No controlled document versions match this view.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>

          <Pagination
            currentPage={templates.data?.page ?? 1}
            totalPages={Math.max(
              1,
              Math.ceil(
                (templates.data?.totalCount ?? 0) /
                  (templates.data?.pageSize ?? 25)
              )
            )}
            totalItems={templates.data?.totalCount ?? 0}
            pageSize={templates.data?.pageSize ?? 25}
            onPageChange={(page) =>
              setFilters((current) => ({ ...current, page }))
            }
            onPageSizeChange={(pageSize) =>
              setFilters((current) => ({ ...current, page: 1, pageSize }))
            }
          />
        </CardContent>
      </Card>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Create controlled tender-document Draft</DialogTitle>
            <DialogDescription>
              Store shared content references and immutable lineage only. Upload
              and workflow execution remain owned by the shared platforms.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Template code">
              <Input
                value={form.templateCode}
                onChange={(event) =>
                  setForm({ ...form, templateCode: event.target.value })
                }
              />
            </Field>
            <Field label="Name">
              <Input
                value={form.name}
                onChange={(event) =>
                  setForm({ ...form, name: event.target.value })
                }
              />
            </Field>
            <Field label="Document type code">
              <Input
                value={form.documentTypeCode}
                onChange={(event) =>
                  setForm({ ...form, documentTypeCode: event.target.value })
                }
              />
            </Field>
            <Field label="Effective from">
              <Input
                type="datetime-local"
                value={form.effectiveFromUtc}
                onChange={(event) =>
                  setForm({ ...form, effectiveFromUtc: event.target.value })
                }
              />
            </Field>
            <div className="md:col-span-2">
              <Field label="Description">
                <Textarea
                  rows={3}
                  value={form.description}
                  onChange={(event) =>
                    setForm({ ...form, description: event.target.value })
                  }
                />
              </Field>
            </div>
            <div className="md:col-span-2">
              <Field label="Exact Published procurement policy">
                <Select value={form.policySetId} onValueChange={choosePolicy}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select current policy" />
                  </SelectTrigger>
                  <SelectContent>
                    {(policies.data ?? []).map((policy) => (
                      <SelectItem key={policy.id} value={policy.id}>
                        {policy.code} · v{policy.version} ·{' '}
                        {policy.sourceConfigurationProfileCode}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              {selectedPolicy && (
                <p className="mt-1 text-xs text-muted-foreground">
                  Source configuration:{' '}
                  {selectedPolicy.sourceConfigurationProfileCode}
                </p>
              )}
            </div>
            <div className="md:col-span-2">
              <Field label="Exact Published approval workflow">
                <Select
                  value={form.workflowDefinitionId}
                  onValueChange={(workflowDefinitionId) =>
                    setForm({ ...form, workflowDefinitionId })
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Select workflow" />
                  </SelectTrigger>
                  <SelectContent>
                    {(workflows.data ?? []).map((workflow) => (
                      <SelectItem key={workflow.id} value={workflow.id}>
                        {workflow.name} · v{workflow.version}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
            </div>
            <div className="md:col-span-2">
              <Field label="Shared content reference">
                <Input
                  value={form.contentReference}
                  onChange={(event) =>
                    setForm({ ...form, contentReference: event.target.value })
                  }
                  placeholder="Workflow evidence, file-upload, or governed external reference"
                />
              </Field>
            </div>
            <div className="md:col-span-2">
              <Field label="Content SHA-256 checksum">
                <Input
                  value={form.contentChecksumSha256}
                  onChange={(event) =>
                    setForm({
                      ...form,
                      contentChecksumSha256: event.target.value,
                    })
                  }
                  maxLength={64}
                />
              </Field>
            </div>
            <div className="md:col-span-2 space-y-2">
              <Label>Applicable procurement methods</Label>
              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                {methods.map((method) => (
                  <label
                    key={method}
                    className="flex items-center gap-2 rounded border p-3 text-sm"
                  >
                    <Checkbox
                      checked={form.applicableMethods.includes(method)}
                      onCheckedChange={(checked) =>
                        toggleMethod(method, Boolean(checked))
                      }
                    />
                    {procurementMethodLabel[method]}
                  </label>
                ))}
              </div>
            </div>
            <div className="md:col-span-2">
              <Field label="Change summary">
                <Textarea
                  value={form.changeSummary}
                  onChange={(event) =>
                    setForm({ ...form, changeSummary: event.target.value })
                  }
                />
              </Field>
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setCreateOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => void create()}
              disabled={
                saving ||
                workflows.isLoading ||
                policies.isLoading ||
                !selectedPolicy
              }
            >
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create controlled Draft
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
    </div>
  );
}
