'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Edit3, Plus, RefreshCw, Trash2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import {
  quantitySurveyCatalogueService,
  type QuantitySurveyUnitOption,
} from '@/services/quantity-survey-catalogue.service';
import type {
  CreateProjectCatalogEntryDto,
  ProjectCatalogEntryDto,
} from '@/services/projectService';

const CATALOGUES = [
  { value: 'qs-sections', label: 'TDC sections' },
  { value: 'qs-trades', label: 'Trades' },
  { value: 'qs-cost-codes', label: 'Cost codes' },
  { value: 'qs-measurement-codes', label: 'Measurement codes' },
] as const;

const STANDARDS = [
  { value: 'Smm7', label: 'SMM7' },
  { value: 'Cesmm3', label: 'CESMM3' },
  { value: 'Cesmm4', label: 'CESMM4' },
  { value: 'TdcLocal', label: 'TDC Local' },
] as const;

const today = () => new Date().toISOString().slice(0, 10);

const emptyForm = (catalogType: string): CreateProjectCatalogEntryDto => ({
  catalogType,
  code: '',
  name: '',
  description: '',
  effectiveFrom: today(),
  sortOrder: 10,
  isActive: true,
});

type Cell<T> = { row: { original: T } };

export default function QuantitySurveyCatalogueAdminPage() {
  const client = useQueryClient();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('quantity-survey.configuration.manage');
  const [catalogType, setCatalogType] = useState('qs-sections');
  const [standard, setStandard] = useState('all');
  const [status, setStatus] = useState('all');
  const [effectiveAt, setEffectiveAt] = useState('');
  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<CreateProjectCatalogEntryDto>(() =>
    emptyForm('qs-sections')
  );

  const entries = useQuery({
    queryKey: [
      'quantity-survey-catalogues',
      catalogType,
      standard,
      effectiveAt,
    ],
    queryFn: () =>
      quantitySurveyCatalogueService.list({
        catalogType,
        standardCode:
          catalogType === 'qs-measurement-codes' && standard !== 'all'
            ? standard
            : undefined,
        effectiveAt: effectiveAt || undefined,
        includeInactive: true,
      }),
  });
  const units = useQuery({
    queryKey: ['quantity-survey-catalogue-units'],
    queryFn: quantitySurveyCatalogueService.unitsOfMeasure,
  });

  const visibleEntries = useMemo(
    () =>
      (entries.data ?? []).filter((entry) =>
        status === 'all'
          ? true
          : status === 'active'
            ? entry.isActive
            : !entry.isActive
      ),
    [entries.data, status]
  );

  const save = useMutation({
    mutationFn: () =>
      editingId
        ? quantitySurveyCatalogueService.update(editingId, form)
        : quantitySurveyCatalogueService.create(form),
    onSuccess: async () => {
      setOpen(false);
      setEditingId(null);
      setForm(emptyForm(catalogType));
      await client.invalidateQueries({
        queryKey: ['quantity-survey-catalogues'],
      });
      toast({
        title: 'QS catalogue saved',
        description:
          'The controlled catalogue entry is available for its effective period.',
        variant: 'success',
      });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to save catalogue entry',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => quantitySurveyCatalogueService.delete(id),
    onSuccess: async () => {
      await client.invalidateQueries({
        queryKey: ['quantity-survey-catalogues'],
      });
      toast({ title: 'QS catalogue entry removed', variant: 'success' });
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to remove catalogue entry',
        description: error.message,
        variant: 'destructive',
      }),
  });

  const beginCreate = () => {
    setEditingId(null);
    setForm(emptyForm(catalogType));
    setOpen(true);
  };

  const beginEdit = (entry: ProjectCatalogEntryDto) => {
    setEditingId(entry.id);
    setForm({
      catalogType: entry.catalogType,
      code: entry.code,
      name: entry.name,
      description: entry.description ?? '',
      standardCode: entry.standardCode,
      measurementRule: entry.measurementRule,
      defaultUnitOfMeasure: entry.defaultUnitOfMeasure,
      effectiveFrom: entry.effectiveFrom?.slice(0, 10) ?? today(),
      effectiveTo: entry.effectiveTo?.slice(0, 10),
      sortOrder: entry.sortOrder,
      isActive: entry.isActive,
    });
    setOpen(true);
  };

  const columns = useMemo<Array<DataTableColumn<ProjectCatalogEntryDto>>>(
    () => [
      {
        id: 'code',
        header: 'Code',
        accessorKey: 'code',
        cell: ({ row }: Cell<ProjectCatalogEntryDto>) => (
          <Badge variant="outline">{row.original.code}</Badge>
        ),
      },
      { id: 'name', header: 'Name', accessorKey: 'name' },
      {
        id: 'standard',
        header: 'Standard',
        accessorKey: 'standardCode',
        cell: ({ row }: Cell<ProjectCatalogEntryDto>) =>
          row.original.standardCode ?? 'TDC classification',
      },
      {
        id: 'unit',
        header: 'Default UOM',
        accessorKey: 'defaultUnitOfMeasure',
        cell: ({ row }: Cell<ProjectCatalogEntryDto>) =>
          row.original.defaultUnitOfMeasure ?? '—',
      },
      {
        id: 'period',
        header: 'Effective period',
        cell: ({ row }: Cell<ProjectCatalogEntryDto>) =>
          `${row.original.effectiveFrom?.slice(0, 10) ?? 'Not set'} – ${row.original.effectiveTo?.slice(0, 10) ?? 'Open-ended'}`,
      },
      {
        id: 'status',
        header: 'Status',
        accessorKey: 'isActive',
        cell: ({ row }: Cell<ProjectCatalogEntryDto>) => (
          <Badge variant={row.original.isActive ? 'secondary' : 'outline'}>
            {row.original.isActive ? 'Active' : 'Inactive'}
          </Badge>
        ),
      },
    ],
    []
  );

  const selectedLabel =
    CATALOGUES.find((item) => item.value === catalogType)?.label ??
    'QS catalogue';
  const measurementCatalogue = catalogType === 'qs-measurement-codes';
  const saveDisabled =
    save.isPending ||
    !form.code.trim() ||
    !form.name.trim() ||
    !form.effectiveFrom ||
    (measurementCatalogue && !form.standardCode);

  return (
    <div className="space-y-4">
      <div className="flex flex-col justify-between gap-3 md:flex-row md:items-center">
        <div>
          <h1 className="text-2xl font-bold">Quantity survey catalogues</h1>
          <p className="text-sm text-muted-foreground">
            Tenant-controlled BoQ classifications and licensed measurement-code
            references.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => entries.refetch()}>
            <RefreshCw
              className={`mr-2 h-4 w-4 ${entries.isFetching ? 'animate-spin' : ''}`}
            />
            Refresh
          </Button>
          {canManage ? (
            <Button onClick={beginCreate}>
              <Plus className="mr-2 h-4 w-4" /> Add entry
            </Button>
          ) : null}
        </div>
      </div>

      <div className="grid gap-3 rounded-lg border bg-card p-3 md:grid-cols-4">
        <div className="space-y-1">
          <Label>Catalogue</Label>
          <Select
            value={catalogType}
            onValueChange={(value) => {
              setCatalogType(value);
              setStandard('all');
            }}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {CATALOGUES.map((item) => (
                <SelectItem key={item.value} value={item.value}>
                  {item.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>Status</Label>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              <SelectItem value="active">Active</SelectItem>
              <SelectItem value="inactive">Inactive</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>Standard</Label>
          <Select
            value={standard}
            onValueChange={setStandard}
            disabled={!measurementCatalogue}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All standards</SelectItem>
              {STANDARDS.map((item) => (
                <SelectItem key={item.value} value={item.value}>
                  {item.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label>Effective at</Label>
          <Input
            type="date"
            value={effectiveAt}
            onChange={(event) => setEffectiveAt(event.target.value)}
          />
        </div>
      </div>

      <DataTable
        compact
        title={selectedLabel}
        description="Search and export the tenant catalogue below. Official SMM7/CESMM contents are loaded only from a TDC-approved licensed source."
        data={visibleEntries}
        columns={columns}
        loading={entries.isLoading}
        error={
          entries.error ? 'Failed to load the quantity-survey catalogue.' : null
        }
        enableSearch
        searchPlaceholder="Search code, name, standard, rule or unit…"
        enableExport
        exportFormats={['csv', 'excel']}
        exportFileName={`quantity-survey-${catalogType}`}
        enablePagination
        pageSize={20}
        emptyStateMessage="No approved entries have been loaded for this catalogue."
        rowActions={
          canManage
            ? [
                {
                  id: 'edit',
                  label: 'Edit entry',
                  icon: Edit3,
                  onClick: (row) => beginEdit(row.original),
                },
                {
                  id: 'delete',
                  label: 'Remove entry',
                  icon: Trash2,
                  variant: 'destructive',
                  onClick: (row) => {
                    if (
                      window.confirm(
                        `Remove ${row.original.code} from this catalogue?`
                      )
                    ) {
                      remove.mutate(row.original.id);
                    }
                  },
                },
              ]
            : []
        }
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {editingId ? 'Edit catalogue entry' : 'Add catalogue entry'}
            </DialogTitle>
            <DialogDescription>
              Codes and descriptions must come from the approved TDC or licensed
              standards source.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Catalogue</Label>
              <Select value={form.catalogType} disabled>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CATALOGUES.map((item) => (
                    <SelectItem key={item.value} value={item.value}>
                      {item.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Code</Label>
              <Input
                value={form.code}
                onChange={(event) =>
                  setForm((value) => ({ ...value, code: event.target.value }))
                }
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Name</Label>
              <Input
                value={form.name}
                onChange={(event) =>
                  setForm((value) => ({ ...value, name: event.target.value }))
                }
              />
            </div>
            <div className="space-y-2 md:col-span-2">
              <Label>Description</Label>
              <Textarea
                rows={3}
                value={form.description ?? ''}
                onChange={(event) =>
                  setForm((value) => ({
                    ...value,
                    description: event.target.value,
                  }))
                }
              />
            </div>
            {measurementCatalogue ? (
              <>
                <div className="space-y-2">
                  <Label>Measurement standard</Label>
                  <Select
                    value={form.standardCode ?? ''}
                    onValueChange={(value) =>
                      setForm((current) => ({
                        ...current,
                        standardCode: value,
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select standard" />
                    </SelectTrigger>
                    <SelectContent>
                      {STANDARDS.map((item) => (
                        <SelectItem key={item.value} value={item.value}>
                          {item.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2">
                  <Label>Default unit of measure</Label>
                  <Select
                    value={form.defaultUnitOfMeasure ?? 'none'}
                    onValueChange={(value) =>
                      setForm((current) => ({
                        ...current,
                        defaultUnitOfMeasure:
                          value === 'none' ? undefined : value,
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select unit" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">No default unit</SelectItem>
                      {(units.data ?? []).map(
                        (unit: QuantitySurveyUnitOption) => (
                          <SelectItem key={unit.code} value={unit.code}>
                            {unit.code} - {unit.name}
                          </SelectItem>
                        )
                      )}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-2 md:col-span-2">
                  <Label>Measurement rule</Label>
                  <Textarea
                    rows={4}
                    value={form.measurementRule ?? ''}
                    onChange={(event) =>
                      setForm((value) => ({
                        ...value,
                        measurementRule: event.target.value || undefined,
                      }))
                    }
                  />
                </div>
              </>
            ) : null}
            <div className="space-y-2">
              <Label>Effective from</Label>
              <Input
                type="date"
                value={form.effectiveFrom?.slice(0, 10) ?? ''}
                onChange={(event) =>
                  setForm((value) => ({
                    ...value,
                    effectiveFrom: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Effective to (optional)</Label>
              <Input
                type="date"
                value={form.effectiveTo?.slice(0, 10) ?? ''}
                onChange={(event) =>
                  setForm((value) => ({
                    ...value,
                    effectiveTo: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Sort order</Label>
              <Input
                type="number"
                value={form.sortOrder ?? 0}
                onChange={(event) =>
                  setForm((value) => ({
                    ...value,
                    sortOrder: Number(event.target.value || 0),
                  }))
                }
              />
            </div>
            <div className="flex items-center justify-between rounded-md border p-3">
              <Label>Active</Label>
              <Switch
                checked={form.isActive !== false}
                onCheckedChange={(checked) =>
                  setForm((value) => ({ ...value, isActive: checked }))
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button disabled={saveDisabled} onClick={() => save.mutate()}>
              {save.isPending ? 'Saving…' : 'Save entry'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
