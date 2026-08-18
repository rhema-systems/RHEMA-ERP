'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { BookOpen, Eye, Plus, RefreshCw } from 'lucide-react';

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
import { quantitySurveyConfigurationService } from '@/services/quantity-survey-configuration.service';
import type {
  CreateQsProfileRequest,
  QsProfileStatus,
  QsProfileSummary,
} from '@/types/quantity-survey-configuration';

type Cell<T> = { row: { original: T } };

export default function QuantitySurveyConfigurationPage() {
  const router = useRouter();
  const client = useQueryClient();
  const { toast } = useToast();
  const { hasPermission } = useAuth();
  const canManage = hasPermission('quantity-survey.configuration.manage');
  const [status, setStatus] = useState<QsProfileStatus | 'All'>('All');
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<CreateQsProfileRequest>({
    name: 'TDC Quantity Survey Configuration',
    effectiveFrom: new Date().toISOString().slice(0, 10),
    isDefault: true,
  });
  const profiles = useQuery({
    queryKey: ['quantity-survey-configuration-profiles', status],
    queryFn: () =>
      quantitySurveyConfigurationService.list({
        status: status === 'All' ? undefined : status,
        pageSize: 100,
      }),
  });
  const create = useMutation({
    mutationFn: () =>
      quantitySurveyConfigurationService.create({
        ...form,
        name: form.name.trim(),
        effectiveTo: form.effectiveTo || undefined,
        changeSummary: form.changeSummary?.trim() || undefined,
      }),
    onSuccess: async (result) => {
      setOpen(false);
      await client.invalidateQueries({
        queryKey: ['quantity-survey-configuration-profiles'],
      });
      toast({
        title: 'QS draft created',
        description: 'All 17 controlled decisions were initialized.',
        variant: 'success',
      });
      router.push(
        `/administration/project-management/quantity-survey-config/${result.id}`
      );
    },
    onError: (error: Error) =>
      toast({
        title: 'Unable to create draft',
        description: error.message,
        variant: 'destructive',
      }),
  });
  const columns = useMemo<Array<DataTableColumn<QsProfileSummary>>>(
    () => [
      {
        id: 'profile',
        header: 'Configuration / version',
        accessorKey: 'name',
        cell: ({ row }: Cell<QsProfileSummary>) => (
          <button
            className="text-left"
            onClick={() =>
              router.push(
                `/administration/project-management/quantity-survey-config/${row.original.id}`
              )
            }
          >
            <span className="block font-medium text-primary hover:underline">
              {row.original.name}
            </span>
            <span className="text-xs text-muted-foreground">
              {row.original.profileCode} · v{row.original.version}
            </span>
          </button>
        ),
      },
      {
        id: 'status',
        header: 'Lifecycle',
        accessorKey: 'lifecycleStatus',
        cell: ({ row }: Cell<QsProfileSummary>) => (
          <Badge
            variant={
              row.original.lifecycleStatus === 'Published'
                ? 'default'
                : 'secondary'
            }
          >
            {row.original.lifecycleStatus}
          </Badge>
        ),
      },
      {
        id: 'period',
        header: 'Effective period',
        cell: ({ row }: Cell<QsProfileSummary>) =>
          `${new Date(row.original.effectiveFrom).toLocaleDateString()} – ${row.original.effectiveTo ? new Date(row.original.effectiveTo).toLocaleDateString() : 'Open-ended'}`,
      },
      {
        id: 'readiness',
        header: 'Decision readiness',
        cell: ({ row }: Cell<QsProfileSummary>) => (
          <div>
            <span className="font-medium">
              {row.original.completeDecisionCount}/
              {row.original.totalDecisionCount}
            </span>
            <span className="block text-xs text-muted-foreground">
              {row.original.isComplete
                ? 'Ready to validate'
                : 'Action required'}
            </span>
          </div>
        ),
      },
      {
        id: 'updated',
        header: 'Last activity',
        cell: ({ row }: Cell<QsProfileSummary>) =>
          new Date(row.original.updatedAt).toLocaleString(),
      },
    ],
    [router]
  );
  return (
    <div className="space-y-6">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div>
          <h1 className="text-3xl font-bold">Quantity survey configuration</h1>
          <p className="mt-1 text-muted-foreground">
            Versioned, effective-dated policy controls for QS-CFG-001 through
            QS-CFG-017.
          </p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link href="/administration/project-management/quantity-survey-catalogues">
              <BookOpen className="mr-2 h-4 w-4" />
              BoQ catalogues
            </Link>
          </Button>
          <Button variant="outline" onClick={() => profiles.refetch()}>
            <RefreshCw
              className={`mr-2 h-4 w-4 ${profiles.isFetching ? 'animate-spin' : ''}`}
            />
            Refresh
          </Button>
          {canManage && (
            <Button onClick={() => setOpen(true)}>
              <Plus className="mr-2 h-4 w-4" />
              New draft
            </Button>
          )}
        </div>
      </div>
      <div className="max-w-xs space-y-2">
        <Label>Lifecycle</Label>
        <Select
          value={status}
          onValueChange={(next) => setStatus(next as QsProfileStatus | 'All')}
        >
          <SelectTrigger>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {['All', 'Draft', 'Published', 'Retired'].map((item) => (
              <SelectItem key={item} value={item}>
                {item === 'All' ? 'All statuses' : item}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <DataTable
        compact
        title="QS configuration version history"
        description="Published and retired versions remain immutable; future versions may be scheduled without disabling the current effective profile."
        data={profiles.data?.items ?? []}
        columns={columns}
        loading={profiles.isLoading}
        error={
          profiles.error ? 'Failed to load QS configuration profiles.' : null
        }
        enableSearch
        enablePagination
        pageSize={20}
        emptyStateMessage="No QS configuration profile exists for this tenant."
        onRowDoubleClick={(row) =>
          router.push(
            `/administration/project-management/quantity-survey-config/${row.original.id}`
          )
        }
        rowActions={[
          {
            id: 'open',
            label: 'Open details',
            icon: Eye,
            onClick: (row) =>
              router.push(
                `/administration/project-management/quantity-survey-config/${row.original.id}`
              ),
          },
        ]}
      />
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create QS configuration draft</DialogTitle>
            <DialogDescription>
              The profile code is governed as TDC-QUANTITY-SURVEY and cannot be
              entered manually.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label>Name</Label>
              <Input
                value={form.name}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    name: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Effective from</Label>
              <Input
                type="date"
                value={form.effectiveFrom}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    effectiveFrom: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Effective to (optional)</Label>
              <Input
                type="date"
                value={form.effectiveTo ?? ''}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    effectiveTo: event.target.value || undefined,
                  }))
                }
              />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label>Change summary</Label>
              <Textarea
                value={form.changeSummary ?? ''}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    changeSummary: event.target.value,
                  }))
                }
              />
            </div>
            <div className="flex items-center gap-3 sm:col-span-2">
              <Switch
                checked={form.isDefault}
                onCheckedChange={(next) =>
                  setForm((current) => ({ ...current, isDefault: next }))
                }
              />
              <span className="text-sm">Default QS configuration family</span>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => create.mutate()}
              disabled={
                create.isPending || !form.name.trim() || !form.effectiveFrom
              }
            >
              {create.isPending ? 'Creating…' : 'Create draft'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
