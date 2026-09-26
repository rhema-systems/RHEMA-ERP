'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Eye, FileCheck, Plus, RefreshCw, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/hooks/use-toast';
import { fileUploadPolicyService, type FileUploadPolicyDto } from '@/services/fileUploadPolicyService';

const bytesToMb = (b?: number | null) => (b && b > 0 ? Math.round((b / 1024 / 1024) * 10) / 10 : 0);
const mbToBytes = (mb?: number | null) => {
  const n = Number(mb || 0);
  if (!Number.isFinite(n) || n <= 0) return null;
  return Math.round(n * 1024 * 1024);
};

const bytesToGb = (b?: number | null) => (b && b > 0 ? Math.round((b / 1024 / 1024 / 1024) * 100) / 100 : 0);
const gbToBytes = (gb?: number | null) => {
  const n = Number(gb || 0);
  if (!Number.isFinite(n) || n <= 0) return null;
  return Math.round(n * 1024 * 1024 * 1024);
};

const formatBytes = (bytes: number) => {
  if (!bytes || bytes < 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round((bytes / 1024) * 10) / 10} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${Math.round((bytes / 1024 / 1024) * 10) / 10} MB`;
  return `${Math.round((bytes / 1024 / 1024 / 1024) * 100) / 100} GB`;
};

export default function FileUploadPoliciesPage() {
  const qc = useQueryClient();
  const { toast } = useToast();

  const [open, setOpen] = useState(false);
  const [editingCategory, setEditingCategory] = useState<string | null>(null);
  const [newCategory, setNewCategory] = useState('');
  const [form, setForm] = useState<{
    isEnabled: boolean;
    maxFileSizeMb: number;
    maxTenantTotalGb: number;
    maxCategoryTotalGb: number;
    allowedExtensionsCsv: string;
    allowedMimeTypesCsv: string;
    requireVirusScan: boolean;
  }>({
    isEnabled: true,
    maxFileSizeMb: 10,
    maxTenantTotalGb: 5,
    maxCategoryTotalGb: 1,
    allowedExtensionsCsv: '',
    allowedMimeTypesCsv: '',
    requireVirusScan: false,
  });

  const { data: policies = [], isLoading: policiesLoading, refetch: refetchPolicies } = useQuery({
    queryKey: ['settings', 'file-uploads', 'policies'],
    queryFn: () => fileUploadPolicyService.listPolicies(),
  });

  const { data: usage, isLoading: usageLoading, refetch: refetchUsage } = useQuery({
    queryKey: ['settings', 'file-uploads', 'usage'],
    queryFn: () => fileUploadPolicyService.getUsage(),
  });

  const globalPolicy = useMemo(() => policies.find((p) => p.category === '*') || null, [policies]);
  const categoryPolicies = useMemo(() => policies.filter((p) => p.category !== '*'), [policies]);

  const openEdit = (p: FileUploadPolicyDto) => {
    setEditingCategory(p.category);
    setForm({
      isEnabled: !!p.isEnabled,
      maxFileSizeMb: bytesToMb(p.maxFileSizeBytes),
      maxTenantTotalGb: bytesToGb(p.maxTenantTotalBytes),
      maxCategoryTotalGb: bytesToGb(p.maxCategoryTotalBytes),
      allowedExtensionsCsv: p.allowedExtensionsCsv || '',
      allowedMimeTypesCsv: p.allowedMimeTypesCsv || '',
      requireVirusScan: !!p.requireVirusScan,
    });
    setOpen(true);
  };

  const openCreate = () => {
    const cat = (newCategory || '').trim();
    if (!cat) {
      toast({ title: 'Category required', description: 'Enter a category (e.g. ehc-ticket). Use * for tenant default.', variant: 'destructive' });
      return;
    }

    setEditingCategory(cat);
    setForm({
      isEnabled: true,
      maxFileSizeMb: bytesToMb(globalPolicy?.maxFileSizeBytes) || 10,
      maxTenantTotalGb: bytesToGb(globalPolicy?.maxTenantTotalBytes) || 5,
      maxCategoryTotalGb: bytesToGb(globalPolicy?.maxCategoryTotalBytes) || 1,
      allowedExtensionsCsv: '',
      allowedMimeTypesCsv: '',
      requireVirusScan: false,
    });
    setOpen(true);
  };

  const save = useMutation({
    mutationFn: async () => {
      if (!editingCategory) throw new Error('Category not set');
      await fileUploadPolicyService.upsertPolicy(editingCategory, {
        isEnabled: form.isEnabled,
        maxFileSizeBytes: mbToBytes(form.maxFileSizeMb),
        maxTenantTotalBytes: editingCategory === '*' ? gbToBytes(form.maxTenantTotalGb) : null,
        maxCategoryTotalBytes: gbToBytes(form.maxCategoryTotalGb),
        allowedExtensionsCsv: form.allowedExtensionsCsv.trim() || null,
        allowedMimeTypesCsv: form.allowedMimeTypesCsv.trim() || null,
        requireVirusScan: form.requireVirusScan,
      });
    },
    onSuccess: async () => {
      setOpen(false);
      await qc.invalidateQueries({ queryKey: ['settings', 'file-uploads', 'policies'] });
      toast({ title: 'Saved', description: 'Upload policy updated.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to save policy', variant: 'destructive' });
    },
  });

  const del = useMutation({
    mutationFn: async (category: string) => {
      await fileUploadPolicyService.deletePolicy(category);
    },
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['settings', 'file-uploads', 'policies'] });
      toast({ title: 'Deleted', description: 'Policy removed.', variant: 'success' });
    },
    onError: (err: any) => {
      toast({ title: 'Error', description: err?.message || 'Failed to delete policy', variant: 'destructive' });
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold flex items-center gap-2">
            <FileCheck className="h-7 w-7" />
            File Uploads
          </h1>
          <div className="mt-2 text-sm text-slate-600">Per-tenant size/type rules and storage quotas (applies to external portal + authenticated uploads).</div>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            onClick={() => {
              refetchPolicies();
              refetchUsage();
            }}
          >
            <RefreshCw className="h-4 w-4 mr-2" />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Policies</CardTitle>
            <CardDescription>Use `*` as the tenant default policy. Add category-specific policies for overrides.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center gap-2">
              <Input value={newCategory} onChange={(e) => setNewCategory(e.target.value)} placeholder="New category e.g. ehc-ticket" className="max-w-sm" />
              <Button onClick={openCreate}>
                <Plus className="h-4 w-4 mr-2" />
                Add / Edit
              </Button>
            </div>

            {policiesLoading ? (
              <div className="text-sm text-slate-600">Loading policies…</div>
            ) : (
              <div className="rounded-md border overflow-hidden">
                <div className="grid grid-cols-12 gap-2 bg-slate-50 px-3 py-2 text-xs font-medium text-slate-600">
                  <div className="col-span-4">Category</div>
                  <div className="col-span-2">Enabled</div>
                  <div className="col-span-2">Max file</div>
                  <div className="col-span-2">Quota</div>
                  <div className="col-span-2 text-right">Actions</div>
                </div>
                {(policies || []).map((p) => (
                  <div key={p.category} className="grid grid-cols-12 gap-2 px-3 py-2 border-t items-center text-sm">
                    <div className="col-span-4 font-medium text-slate-900">{p.category}</div>
                    <div className="col-span-2 text-slate-700">{p.isEnabled ? 'Yes' : 'No'}</div>
                    <div className="col-span-2 text-slate-700">{p.maxFileSizeBytes ? `${bytesToMb(p.maxFileSizeBytes)} MB` : 'Default'}</div>
                    <div className="col-span-2 text-slate-700">
                      {p.category === '*'
                        ? p.maxTenantTotalBytes
                          ? `${bytesToGb(p.maxTenantTotalBytes)} GB tenant`
                          : '—'
                        : p.maxCategoryTotalBytes
                          ? `${bytesToGb(p.maxCategoryTotalBytes)} GB`
                          : 'Default'}
                    </div>
                    <div className="col-span-2 flex items-center justify-end gap-2">
                      <Button variant="outline" size="icon" title="Edit" onClick={() => openEdit(p)}>
                        <Eye className="h-4 w-4" />
                      </Button>
                      {p.category !== '*' ? (
                        <Button variant="outline" size="icon" title="Delete" onClick={() => del.mutate(p.category)} disabled={del.isPending}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Usage</CardTitle>
            <CardDescription>Recorded uploads for quota enforcement.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            {usageLoading ? (
              <div className="text-sm text-slate-600">Loading usage…</div>
            ) : (
              <>
                <div className="rounded-md border bg-slate-50 p-3">
                  <div className="text-xs text-slate-500">Tenant total</div>
                  <div className="text-lg font-semibold text-slate-900">{formatBytes(usage?.totalBytes || 0)}</div>
                  <div className="text-xs text-slate-500">{usage?.totalFiles || 0} files</div>
                </div>

                <div className="space-y-2">
                  {(usage?.byCategory || []).slice(0, 10).map((u) => (
                    <div key={u.category} className="flex items-center justify-between gap-3 text-sm">
                      <div className="truncate text-slate-700" title={u.category}>
                        {u.category}
                      </div>
                      <div className="text-slate-900 font-medium whitespace-nowrap">{formatBytes(u.bytes)}</div>
                    </div>
                  ))}
                  {!usage?.byCategory?.length ? <div className="text-sm text-slate-600">No uploads recorded yet.</div> : null}
                </div>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Upload policy</DialogTitle>
            <DialogDescription>{editingCategory ? `Category: ${editingCategory}` : '—'}</DialogDescription>
          </DialogHeader>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-sm">
            <div className="space-y-2">
              <Label>Enabled</Label>
              <div className="flex items-center gap-2">
                <Switch checked={form.isEnabled} onCheckedChange={(v) => setForm((f) => ({ ...f, isEnabled: !!v }))} />
                <div className="text-slate-600">{form.isEnabled ? 'On' : 'Off'}</div>
              </div>
            </div>

            <div className="space-y-2">
              <Label>Max file size (MB)</Label>
              <Input
                type="number"
                min={0}
                value={form.maxFileSizeMb}
                onChange={(e) => setForm((f) => ({ ...f, maxFileSizeMb: Number(e.target.value || 0) }))}
              />
            </div>

            {editingCategory === '*' ? (
              <div className="space-y-2">
                <Label>Tenant quota (GB)</Label>
                <Input
                  type="number"
                  min={0}
                  value={form.maxTenantTotalGb}
                  onChange={(e) => setForm((f) => ({ ...f, maxTenantTotalGb: Number(e.target.value || 0) }))}
                />
              </div>
            ) : (
              <div className="space-y-2">
                <Label>Category quota (GB)</Label>
                <Input
                  type="number"
                  min={0}
                  value={form.maxCategoryTotalGb}
                  onChange={(e) => setForm((f) => ({ ...f, maxCategoryTotalGb: Number(e.target.value || 0) }))}
                />
              </div>
            )}

            {editingCategory === '*' ? (
              <div className="space-y-2">
                <Label>Default category quota (GB)</Label>
                <Input
                  type="number"
                  min={0}
                  value={form.maxCategoryTotalGb}
                  onChange={(e) => setForm((f) => ({ ...f, maxCategoryTotalGb: Number(e.target.value || 0) }))}
                />
              </div>
            ) : null}

            <div className="space-y-2 sm:col-span-2">
              <Label>Allowed extensions (CSV)</Label>
              <Input
                value={form.allowedExtensionsCsv}
                onChange={(e) => setForm((f) => ({ ...f, allowedExtensionsCsv: e.target.value }))}
                placeholder=".jpg,.png,.pdf"
              />
              <div className="text-xs text-slate-500">Leave blank to use defaults.</div>
            </div>

            <div className="space-y-2 sm:col-span-2">
              <Label>Allowed MIME types (CSV)</Label>
              <Input
                value={form.allowedMimeTypesCsv}
                onChange={(e) => setForm((f) => ({ ...f, allowedMimeTypesCsv: e.target.value }))}
                placeholder="image/jpeg,application/pdf"
              />
              <div className="text-xs text-slate-500">Leave blank to use defaults.</div>
            </div>

            <div className="space-y-2 sm:col-span-2">
              <Label>Virus scan hook</Label>
              <div className="flex items-center gap-2">
                <Switch checked={form.requireVirusScan} onCheckedChange={(v) => setForm((f) => ({ ...f, requireVirusScan: !!v }))} />
                <div className="text-slate-600">{form.requireVirusScan ? 'Required' : 'Not required'}</div>
              </div>
              <div className="text-xs text-slate-500">Uploads are rejected if ClamAV cannot return a clean scan.</div>
            </div>
          </div>

          <div className="flex items-center justify-end gap-2 pt-2">
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending || !editingCategory}>
              {save.isPending ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}

