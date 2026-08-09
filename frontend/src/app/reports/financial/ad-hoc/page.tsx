'use client';

import { useEffect, useMemo, useState } from 'react';
import { Download, Play, Plus, Save, ShieldCheck, Trash2 } from 'lucide-react';

import { TenantGuard } from '@/components/auth/tenant-guard';
import { DashboardLayout } from '@/components/layout/dashboard-layout';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/components/ui/use-toast';
import {
  financeAdHocReportsService,
  type FinanceAdHocColumn,
  type FinanceAdHocDefinition,
  type FinanceAdHocFilter,
  type FinanceAdHocWorkspace,
  type SaveFinanceAdHocDefinition,
} from '@/services/financeAdHocReports';
import type { ReportResult } from '@/services/reports';

const aggregationOptions: FinanceAdHocColumn['aggregation'][] = [
  'None',
  'Count',
  'Sum',
  'Average',
  'Minimum',
  'Maximum',
];
const filterOperators = [
  'Equals',
  'NotEquals',
  'Contains',
  'StartsWith',
  'GreaterThan',
  'GreaterThanOrEqual',
  'LessThan',
  'LessThanOrEqual',
  'Between',
  'IsBlank',
  'IsNotBlank',
];

const emptyForm = (datasetCode = ''): SaveFinanceAdHocDefinition => ({
  name: '',
  description: '',
  datasetCode,
  columns: [],
  filters: [],
  sorts: [],
  visibility: 'Private',
  maximumRows: 5000,
});

function message(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'The Finance report action could not be completed.';
}

export default function FinanceAdHocReportBuilderPage() {
  const { toast } = useToast();
  const [workspace, setWorkspace] = useState<FinanceAdHocWorkspace | null>(
    null
  );
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [form, setForm] = useState<SaveFinanceAdHocDefinition>(emptyForm());
  const [result, setResult] = useState<ReportResult | null>(null);
  const [busy, setBusy] = useState(false);

  const dataset = useMemo(
    () => workspace?.datasets.find((item) => item.code === form.datasetCode),
    [workspace, form.datasetCode]
  );
  const selectedDefinition = workspace?.definitions.find(
    (item) => item.id === selectedId
  );
  const canMaintainSelection =
    !selectedDefinition || selectedDefinition.canMaintain;

  const load = async (preferredId?: string) => {
    const data = await financeAdHocReportsService.getWorkspace();
    setWorkspace(data);
    const chosen = data.definitions.find((item) => item.id === preferredId);
    if (chosen) selectDefinition(chosen);
    else if (!selectedId && !form.datasetCode)
      setForm(emptyForm(data.datasets[0]?.code));
  };

  useEffect(() => {
    void load().catch((error) =>
      toast({
        title: 'Builder unavailable',
        description: message(error),
        variant: 'destructive',
      })
    );
    // Loading is intentionally once-per-entry; subsequent refreshes follow successful mutations.
  }, []);

  const selectDefinition = (definition: FinanceAdHocDefinition) => {
    setSelectedId(definition.id);
    setResult(null);
    setForm({
      name: definition.name,
      description: definition.description,
      datasetCode: definition.datasetCode,
      columns: definition.columns,
      filters: definition.filters,
      sorts: definition.sorts,
      visibility: definition.visibility,
      maximumRows: definition.maximumRows,
      rowVersion: definition.rowVersion,
    });
  };

  const newDefinition = () => {
    setSelectedId(null);
    setResult(null);
    setForm(emptyForm(workspace?.datasets[0]?.code));
  };

  const changeDataset = (datasetCode: string) => {
    // Catalogue fields are dataset-specific. Clearing the declarative selections prevents stale
    // field keys being submitted after the user changes the business dataset.
    setForm((current) => ({
      ...current,
      datasetCode,
      columns: [],
      filters: [],
      sorts: [],
    }));
    setResult(null);
  };

  const toggleColumn = (field: string, checked: boolean) =>
    setForm((current) => ({
      ...current,
      columns: checked
        ? [...current.columns, { field, aggregation: 'None' }]
        : current.columns.filter((item) => item.field !== field),
      sorts: current.sorts.filter((item) => item.field !== field),
    }));

  const save = async () => {
    if (!form.name.trim() || form.columns.length === 0) {
      toast({
        title: 'Complete the definition',
        description: 'Enter a name and select at least one column.',
        variant: 'destructive',
      });
      return;
    }
    setBusy(true);
    try {
      const saved = selectedId
        ? await financeAdHocReportsService.update(selectedId, form)
        : await financeAdHocReportsService.create(form);
      await load(saved.id);
      toast({
        title: 'Report definition saved',
        description: `${saved.name} is ready to run.`,
      });
    } catch (error) {
      toast({
        title: 'Save failed',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    const definition = workspace?.definitions.find(
      (item) => item.id === selectedId
    );
    if (
      !definition ||
      !definition.canMaintain ||
      !window.confirm(`Delete “${definition.name}”?`)
    )
      return;
    setBusy(true);
    try {
      await financeAdHocReportsService.delete(definition);
      setSelectedId(null);
      setResult(null);
      const data = await financeAdHocReportsService.getWorkspace();
      setWorkspace(data);
      setForm(emptyForm(data.datasets[0]?.code));
      toast({ title: 'Definition deleted' });
    } catch (error) {
      toast({
        title: 'Delete failed',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const run = async () => {
    if (!selectedId) return;
    setBusy(true);
    try {
      setResult(
        await financeAdHocReportsService.execute(selectedId, {
          page: 1,
          pageSize: 100,
          maxRows: form.maximumRows,
        })
      );
    } catch (error) {
      toast({
        title: 'Report run failed',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const exportResult = async (format: 'xlsx' | 'pdf') => {
    if (!selectedId) return;
    setBusy(true);
    try {
      const exported = await financeAdHocReportsService.export(selectedId, {
        format,
        includeHeaders: true,
      });
      const url = URL.createObjectURL(exported.blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = exported.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      toast({
        title: 'Export failed',
        description: message(error),
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  const addFilter = () => {
    const field = dataset?.fields.find((item) => item.canFilter)?.key;
    if (field)
      setForm((current) => ({
        ...current,
        filters: [...current.filters, { field, operator: 'Equals', value: '' }],
      }));
  };

  const updateFilter = (index: number, patch: Partial<FinanceAdHocFilter>) =>
    setForm((current) => ({
      ...current,
      filters: current.filters.map((item, itemIndex) =>
        itemIndex === index ? { ...item, ...patch } : item
      ),
    }));

  return (
    <TenantGuard>
      <DashboardLayout>
        <div className="space-y-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">
                Finance ad hoc report builder
              </h1>
              <p className="mt-1 max-w-3xl text-sm text-muted-foreground">
                Build authorised analyses from curated Finance datasets. Tenant
                isolation, field rules, row limits, execution history, and
                export evidence are enforced by the server.
              </p>
            </div>
            <Badge variant="outline" className="gap-1">
              <ShieldCheck className="h-3.5 w-3.5" /> Governed — no direct SQL
            </Badge>
          </div>

          <div className="grid gap-4 xl:grid-cols-[300px_1fr]">
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Saved definitions</CardTitle>
                <CardDescription>
                  {workspace?.privateDefinitions ?? 0} private ·{' '}
                  {workspace?.sharedDefinitions ?? 0} Finance-shared
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-2">
                <Button
                  className="w-full"
                  variant="outline"
                  onClick={newDefinition}
                >
                  <Plus className="mr-2 h-4 w-4" />
                  New definition
                </Button>
                {workspace?.definitions.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => selectDefinition(item)}
                    className={`w-full rounded-md border p-3 text-left text-sm ${selectedId === item.id ? 'border-primary bg-primary/5' : 'hover:bg-muted'}`}
                  >
                    <span className="block font-medium">{item.name}</span>
                    <span className="mt-1 block text-xs text-muted-foreground">
                      {item.datasetName} · {item.visibility}
                      {!item.canMaintain && ' · read-only'}
                    </span>
                  </button>
                ))}
              </CardContent>
            </Card>

            <div className="space-y-4">
              <Card>
                <CardHeader>
                  <CardTitle className="text-base">Definition</CardTitle>
                  <CardDescription>
                    Select business fields; the server owns every underlying
                    join and predicate.
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-5">
                  <fieldset
                    disabled={!canMaintainSelection}
                    className="space-y-5"
                  >
                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="space-y-2">
                        <Label htmlFor="report-name">Report name</Label>
                        <Input
                          id="report-name"
                          value={form.name}
                          maxLength={200}
                          onChange={(event) =>
                            setForm({ ...form, name: event.target.value })
                          }
                        />
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="dataset">Dataset</Label>
                        <select
                          id="dataset"
                          className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                          value={form.datasetCode}
                          onChange={(event) =>
                            changeDataset(event.target.value)
                          }
                        >
                          {workspace?.datasets.map((item) => (
                            <option key={item.code} value={item.code}>
                              {item.name}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="description">Intent / description</Label>
                      <Textarea
                        id="description"
                        value={form.description}
                        maxLength={1000}
                        onChange={(event) =>
                          setForm({ ...form, description: event.target.value })
                        }
                      />
                    </div>
                    {dataset && (
                      <p className="rounded-md bg-muted p-3 text-sm text-muted-foreground">
                        {dataset.description}
                      </p>
                    )}

                    <div>
                      <Label>Columns and calculations</Label>
                      <div className="mt-2 grid gap-2 md:grid-cols-2">
                        {dataset?.fields.map((field) => {
                          const selected = form.columns.find(
                            (item) => item.field === field.key
                          );
                          return (
                            <div
                              key={field.key}
                              className="flex items-center gap-3 rounded-md border p-2.5"
                            >
                              <Checkbox
                                aria-label={`Select ${field.label}`}
                                checked={!!selected}
                                onCheckedChange={(value) =>
                                  toggleColumn(field.key, value === true)
                                }
                              />
                              <span className="min-w-0 flex-1 text-sm">
                                {field.label}
                              </span>
                              {selected && (
                                <select
                                  aria-label={`${field.label} calculation`}
                                  className="rounded border bg-background p-1 text-xs"
                                  value={selected.aggregation}
                                  onChange={(event) =>
                                    setForm((current) => ({
                                      ...current,
                                      columns: current.columns.map((item) =>
                                        item.field === field.key
                                          ? {
                                              ...item,
                                              aggregation: event.target
                                                .value as FinanceAdHocColumn['aggregation'],
                                            }
                                          : item
                                      ),
                                    }))
                                  }
                                >
                                  {aggregationOptions
                                    .filter(
                                      (option) =>
                                        option === 'None' ||
                                        option === 'Count' ||
                                        field.canAggregate
                                    )
                                    .map((option) => (
                                      <option key={option}>{option}</option>
                                    ))}
                                </select>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    </div>

                    <div>
                      <div className="flex items-center justify-between">
                        <Label>Filters</Label>
                        <Button
                          type="button"
                          size="sm"
                          variant="outline"
                          onClick={addFilter}
                        >
                          Add filter
                        </Button>
                      </div>
                      <div className="mt-2 space-y-2">
                        {form.filters.map((filter, index) => (
                          <div
                            key={`${index}-${filter.field}`}
                            className="grid gap-2 rounded-md border p-2 md:grid-cols-[1fr_170px_1fr_1fr_auto]"
                          >
                            <select
                              aria-label={`Filter ${index + 1} field`}
                              className="rounded border bg-background px-2 text-sm"
                              value={filter.field}
                              onChange={(event) =>
                                updateFilter(index, {
                                  field: event.target.value,
                                })
                              }
                            >
                              {dataset?.fields
                                .filter((item) => item.canFilter)
                                .map((field) => (
                                  <option key={field.key} value={field.key}>
                                    {field.label}
                                  </option>
                                ))}
                            </select>
                            <select
                              aria-label={`Filter ${index + 1} operator`}
                              className="rounded border bg-background px-2 text-sm"
                              value={filter.operator}
                              onChange={(event) =>
                                updateFilter(index, {
                                  operator: event.target.value,
                                })
                              }
                            >
                              {filterOperators.map((operator) => (
                                <option key={operator}>{operator}</option>
                              ))}
                            </select>
                            <Input
                              aria-label={`Filter ${index + 1} value`}
                              placeholder="Value"
                              value={filter.value ?? ''}
                              disabled={filter.operator.startsWith('Is')}
                              onChange={(event) =>
                                updateFilter(index, {
                                  value: event.target.value,
                                })
                              }
                            />
                            <Input
                              aria-label={`Filter ${index + 1} ending value`}
                              placeholder="To"
                              value={filter.valueTo ?? ''}
                              disabled={filter.operator !== 'Between'}
                              onChange={(event) =>
                                updateFilter(index, {
                                  valueTo: event.target.value,
                                })
                              }
                            />
                            <Button
                              aria-label={`Remove filter ${index + 1}`}
                              size="icon"
                              variant="ghost"
                              onClick={() =>
                                setForm((current) => ({
                                  ...current,
                                  filters: current.filters.filter(
                                    (_, itemIndex) => itemIndex !== index
                                  ),
                                }))
                              }
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                        ))}
                      </div>
                    </div>

                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="space-y-2">
                        <Label htmlFor="sort-field">Sort preview by</Label>
                        <select
                          id="sort-field"
                          className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                          value={form.sorts[0]?.field ?? ''}
                          onChange={(event) =>
                            setForm((current) => ({
                              ...current,
                              sorts: event.target.value
                                ? [
                                    {
                                      field: event.target.value,
                                      descending:
                                        current.sorts[0]?.descending ?? false,
                                    },
                                  ]
                                : [],
                            }))
                          }
                        >
                          <option value="">First selected column</option>
                          {form.columns.map((column) => (
                            <option key={column.field} value={column.field}>
                              {dataset?.fields.find(
                                (field) => field.key === column.field
                              )?.label ?? column.field}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="flex items-end gap-2 pb-2">
                        <Checkbox
                          id="sort-descending"
                          checked={form.sorts[0]?.descending ?? false}
                          disabled={!form.sorts.length}
                          onCheckedChange={(value) =>
                            setForm((current) => ({
                              ...current,
                              sorts: current.sorts.map((sort, index) =>
                                index === 0
                                  ? { ...sort, descending: value === true }
                                  : sort
                              ),
                            }))
                          }
                        />
                        <Label htmlFor="sort-descending">
                          Descending order
                        </Label>
                      </div>
                    </div>

                    <div className="grid gap-4 md:grid-cols-2">
                      <div className="space-y-2">
                        <Label htmlFor="visibility">Visibility</Label>
                        <select
                          id="visibility"
                          className="h-10 w-full rounded-md border bg-background px-3 text-sm"
                          value={form.visibility}
                          disabled={!canMaintainSelection}
                          onChange={(event) =>
                            setForm({
                              ...form,
                              visibility: event.target.value as
                                | 'Private'
                                | 'Finance',
                            })
                          }
                        >
                          <option value="Private">Private — only me</option>
                          <option value="Finance">
                            Finance — authorised report users
                          </option>
                        </select>
                      </div>
                      <div className="space-y-2">
                        <Label htmlFor="row-limit">
                          Maximum rows (1–5,000)
                        </Label>
                        <Input
                          id="row-limit"
                          type="number"
                          min={1}
                          max={5000}
                          value={form.maximumRows}
                          disabled={!canMaintainSelection}
                          onChange={(event) =>
                            setForm({
                              ...form,
                              maximumRows: Number(event.target.value),
                            })
                          }
                        />
                      </div>
                    </div>
                  </fieldset>

                  {!canMaintainSelection && (
                    <p className="text-sm text-muted-foreground">
                      This is a shared, read-only definition owned by{' '}
                      {selectedDefinition?.ownerName || 'another Finance user'}.
                      You can run or export it without altering the owner&apos;s
                      design.
                    </p>
                  )}
                  <div className="flex flex-wrap gap-2">
                    <Button
                      onClick={save}
                      disabled={busy || !canMaintainSelection}
                    >
                      <Save className="mr-2 h-4 w-4" />
                      Save definition
                    </Button>
                    <Button
                      variant="outline"
                      onClick={run}
                      disabled={busy || !selectedId}
                    >
                      <Play className="mr-2 h-4 w-4" />
                      Run preview
                    </Button>
                    <Button
                      variant="outline"
                      onClick={() => void exportResult('xlsx')}
                      disabled={busy || !selectedId}
                    >
                      <Download className="mr-2 h-4 w-4" />
                      Excel
                    </Button>
                    <Button
                      variant="outline"
                      onClick={() => void exportResult('pdf')}
                      disabled={busy || !selectedId}
                    >
                      PDF
                    </Button>
                    {selectedId && selectedDefinition?.canMaintain && (
                      <Button
                        variant="destructive"
                        onClick={remove}
                        disabled={busy}
                      >
                        <Trash2 className="mr-2 h-4 w-4" />
                        Delete
                      </Button>
                    )}
                  </div>
                </CardContent>
              </Card>

              {result && (
                <Card>
                  <CardHeader>
                    <CardTitle className="text-base">
                      Preview — {result.reportName}
                    </CardTitle>
                    <CardDescription>
                      {result.totalRows.toLocaleString()} governed row(s);
                      showing up to {result.pageSize} on this page.
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <div className="overflow-auto rounded-md border">
                      <table className="w-full text-sm">
                        <thead className="bg-muted">
                          <tr>
                            {result.columns.map((column) => (
                              <th
                                key={column.name}
                                className="whitespace-nowrap px-3 py-2 text-left font-medium"
                              >
                                {column.displayName ?? column.name}
                              </th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {result.data.map((row, index) => (
                            <tr key={index} className="border-t">
                              {result.columns.map((column) => (
                                <td
                                  key={column.name}
                                  className="whitespace-nowrap px-3 py-2"
                                >
                                  {row[column.name] == null
                                    ? '—'
                                    : String(row[column.name])}
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </CardContent>
                </Card>
              )}
            </div>
          </div>
        </div>
      </DashboardLayout>
    </TenantGuard>
  );
}
