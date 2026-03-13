'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Plus, RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ehcAdminService, type CreateEhcComplianceAuditExportAdmin, type CreateEhcLegalHoldAdmin, type UpsertEhcRetentionCategoryExceptionAdmin } from '@/services/ehcAdminService';

const toIsoUtc = (dtLocal: string): string => {
  if (!dtLocal) return '';
  const d = new Date(dtLocal);
  return d.toISOString();
};

export default function HelpdeskComplianceAdminPage() {
  const qc = useQueryClient();

  const { data: summary } = useQuery({
    queryKey: ['ehc', 'admin', 'compliance', 'summary'],
    queryFn: () => ehcAdminService.getComplianceSummary(),
  });

  const { data: categories } = useQuery({
    queryKey: ['ehc', 'admin', 'categories'],
    queryFn: () => ehcAdminService.listCategories(),
  });

  const { data: retentionExceptions } = useQuery({
    queryKey: ['ehc', 'admin', 'compliance', 'retention', 'categoryExceptions'],
    queryFn: () => ehcAdminService.listRetentionCategoryExceptions(),
  });

  const { data: legalHolds } = useQuery({
    queryKey: ['ehc', 'admin', 'compliance', 'legalHolds'],
    queryFn: () => ehcAdminService.listLegalHolds(true),
  });

  const { data: auditExports } = useQuery({
    queryKey: ['ehc', 'admin', 'compliance', 'auditExports'],
    queryFn: () => ehcAdminService.listAuditExports(),
  });

  const { data: retentionRuns } = useQuery({
    queryKey: ['ehc', 'admin', 'compliance', 'retentionRuns'],
    queryFn: () => ehcAdminService.listComplianceRetentionRuns(20),
  });

  const categoryOptions = useMemo(() => {
    const items = categories || [];
    return items.map((c) => ({ id: c.id, label: c.name })).sort((a, b) => a.label.localeCompare(b.label));
  }, [categories]);

  const [exceptionForm, setExceptionForm] = useState<UpsertEhcRetentionCategoryExceptionAdmin>({
    categoryId: '',
    isActive: true,
    auditEventRetentionDays: 365,
    notes: '',
  });

  const saveException = useMutation({
    mutationFn: () => ehcAdminService.upsertRetentionCategoryException({ ...exceptionForm, notes: exceptionForm.notes || null }),
    onSuccess: async () => {
      setExceptionForm({ categoryId: '', isActive: true, auditEventRetentionDays: 365, notes: '' });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'retention', 'categoryExceptions'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'summary'] });
    },
  });

  const [holdForm, setHoldForm] = useState<CreateEhcLegalHoldAdmin>({
    ticketId: '',
    reason: '',
    referenceNumber: '',
  });

  const createHold = useMutation({
    mutationFn: () => ehcAdminService.createLegalHold({ ...holdForm, reason: holdForm.reason || null, referenceNumber: holdForm.referenceNumber || null }),
    onSuccess: async () => {
      setHoldForm({ ticketId: '', reason: '', referenceNumber: '' });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'legalHolds'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'summary'] });
    },
  });

  const releaseHold = useMutation({
    mutationFn: (id: string) => ehcAdminService.releaseLegalHold(id),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'legalHolds'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'summary'] });
    },
  });

  const [exportForm, setExportForm] = useState<{ fromLocal: string; toLocal: string; ticketId: string; categoryId: string }>({
    fromLocal: '',
    toLocal: '',
    ticketId: '',
    categoryId: '',
  });

  const createExport = useMutation({
    mutationFn: async () => {
      const fromUtc = toIsoUtc(exportForm.fromLocal);
      const toUtc = toIsoUtc(exportForm.toLocal);

      const payload: CreateEhcComplianceAuditExportAdmin = {
        fromUtc,
        toUtc,
        ticketId: exportForm.ticketId || null,
        categoryId: exportForm.categoryId || null,
      };
      return ehcAdminService.createAuditExport(payload);
    },
    onSuccess: async () => {
      setExportForm({ fromLocal: '', toLocal: '', ticketId: '', categoryId: '' });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'auditExports'] });
      await qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'summary'] });
    },
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Helpdesk Compliance</h1>
        <p className="text-slate-600 mt-1">Legal holds, retention exceptions, and immutable audit exports.</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card>
          <CardHeader>
            <CardTitle>Legal Holds</CardTitle>
            <CardDescription>Active ticket holds</CardDescription>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{summary?.activeLegalHolds ?? 0}</CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Retention Exceptions</CardTitle>
            <CardDescription>Category overrides</CardDescription>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{summary?.activeRetentionCategoryExceptions ?? 0}</CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Audit Exports</CardTitle>
            <CardDescription>Generated exports</CardDescription>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{summary?.auditExports ?? 0}</CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Retention Exceptions (Category)</CardTitle>
          <CardDescription>Override EHC audit-event retention days per category (legal holds always win).</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="space-y-2 md:col-span-2">
              <Label>Category</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={exceptionForm.categoryId}
                onChange={(e) => setExceptionForm((f) => ({ ...f, categoryId: e.target.value }))}
              >
                <option value="">Select category</option>
                {categoryOptions.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label>Retention days</Label>
              <Input
                type="number"
                min={1}
                max={3650}
                value={String(exceptionForm.auditEventRetentionDays)}
                onChange={(e) => setExceptionForm((f) => ({ ...f, auditEventRetentionDays: Number(e.target.value) }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Active</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={exceptionForm.isActive ? 'true' : 'false'}
                onChange={(e) => setExceptionForm((f) => ({ ...f, isActive: e.target.value === 'true' }))}
              >
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </select>
            </div>
          </div>
          <div className="space-y-2">
            <Label>Notes</Label>
            <Input value={exceptionForm.notes || ''} onChange={(e) => setExceptionForm((f) => ({ ...f, notes: e.target.value }))} placeholder="Optional" />
          </div>
          <div className="flex items-center gap-2">
            <Button onClick={() => saveException.mutate()} disabled={saveException.isPending || !exceptionForm.categoryId}>
              <Plus className="h-4 w-4 mr-2" />
              {saveException.isPending ? 'Saving...' : 'Save exception'}
            </Button>
            <Button
              variant="outline"
              onClick={() => qc.invalidateQueries({ queryKey: ['ehc', 'admin', 'compliance', 'retention', 'categoryExceptions'] })}
            >
              <RefreshCw className="h-4 w-4 mr-2" />
              Refresh
            </Button>
          </div>

          {!retentionExceptions || retentionExceptions.length === 0 ? (
            <div className="text-slate-600">No retention exceptions.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-slate-500">
                  <tr>
                    <th className="py-2 pr-4">Category</th>
                    <th className="py-2 pr-4">Active</th>
                    <th className="py-2 pr-4">Days</th>
                    <th className="py-2 pr-4">Notes</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {retentionExceptions.map((x) => (
                    <tr
                      key={x.id}
                      className="hover:bg-slate-50 cursor-pointer"
                      onClick={() =>
                        setExceptionForm({
                          categoryId: x.categoryId,
                          isActive: x.isActive,
                          auditEventRetentionDays: x.auditEventRetentionDays,
                          notes: x.notes || '',
                        })
                      }
                    >
                      <td className="py-2 pr-4 font-medium text-slate-900">{x.categoryName || x.categoryId}</td>
                      <td className="py-2 pr-4">{x.isActive ? 'Yes' : 'No'}</td>
                      <td className="py-2 pr-4">{x.auditEventRetentionDays}</td>
                      <td className="py-2 pr-4">{x.notes || ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Legal Holds</CardTitle>
          <CardDescription>Prevent EHC audit events from being deleted by retention jobs for specific tickets.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label>Ticket ID</Label>
              <Input value={holdForm.ticketId} onChange={(e) => setHoldForm((f) => ({ ...f, ticketId: e.target.value }))} placeholder="GUID" />
            </div>
            <div className="space-y-2">
              <Label>Reference #</Label>
              <Input value={holdForm.referenceNumber || ''} onChange={(e) => setHoldForm((f) => ({ ...f, referenceNumber: e.target.value }))} placeholder="Optional" />
            </div>
            <div className="space-y-2">
              <Label>Reason</Label>
              <Input value={holdForm.reason || ''} onChange={(e) => setHoldForm((f) => ({ ...f, reason: e.target.value }))} placeholder="Optional" />
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Button onClick={() => createHold.mutate()} disabled={createHold.isPending || !holdForm.ticketId.trim()}>
              <Plus className="h-4 w-4 mr-2" />
              {createHold.isPending ? 'Creating...' : 'Create legal hold'}
            </Button>
          </div>

          {!legalHolds || legalHolds.length === 0 ? (
            <div className="text-slate-600">No legal holds.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-slate-500">
                  <tr>
                    <th className="py-2 pr-4">Ticket</th>
                    <th className="py-2 pr-4">Active</th>
                    <th className="py-2 pr-4">Reference</th>
                    <th className="py-2 pr-4">Reason</th>
                    <th className="py-2 pr-4"></th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {legalHolds.map((h) => (
                    <tr key={h.id}>
                      <td className="py-2 pr-4 font-medium text-slate-900">{h.ticketNumber || h.ticketId}</td>
                      <td className="py-2 pr-4">{h.isActive ? 'Yes' : 'No'}</td>
                      <td className="py-2 pr-4">{h.referenceNumber || ''}</td>
                      <td className="py-2 pr-4">{h.reason || ''}</td>
                      <td className="py-2 pr-4 text-right">
                        {h.isActive ? (
                          <Button variant="outline" size="sm" onClick={() => releaseHold.mutate(h.id)} disabled={releaseHold.isPending}>
                            Release
                          </Button>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Immutable Audit Exports</CardTitle>
          <CardDescription>Create JSON exports of EHC ticket audit events for compliance review.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="space-y-2">
              <Label>From (local)</Label>
              <Input
                type="datetime-local"
                value={exportForm.fromLocal}
                onChange={(e) => setExportForm((f) => ({ ...f, fromLocal: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>To (local)</Label>
              <Input
                type="datetime-local"
                value={exportForm.toLocal}
                onChange={(e) => setExportForm((f) => ({ ...f, toLocal: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Category (optional)</Label>
              <select
                className="w-full h-10 rounded-md border border-input bg-background px-3 text-sm"
                value={exportForm.categoryId || ''}
                onChange={(e) => setExportForm((f) => ({ ...f, categoryId: e.target.value }))}
              >
                <option value="">Any</option>
                {categoryOptions.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label>Ticket ID (optional)</Label>
              <Input value={exportForm.ticketId || ''} onChange={(e) => setExportForm((f) => ({ ...f, ticketId: e.target.value }))} placeholder="GUID" />
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Button onClick={() => createExport.mutate()} disabled={createExport.isPending || !exportForm.fromLocal || !exportForm.toLocal}>
              <Plus className="h-4 w-4 mr-2" />
              {createExport.isPending ? 'Creating...' : 'Create export'}
            </Button>
          </div>

          {!auditExports || auditExports.length === 0 ? (
            <div className="text-slate-600">No audit exports.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-slate-500">
                  <tr>
                    <th className="py-2 pr-4">Created</th>
                    <th className="py-2 pr-4">Range</th>
                    <th className="py-2 pr-4">Rows</th>
                    <th className="py-2 pr-4">SHA-256</th>
                    <th className="py-2 pr-4"></th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {auditExports.map((x) => (
                    <tr key={x.id}>
                      <td className="py-2 pr-4">{new Date(x.createdAtUtc).toLocaleString()}</td>
                      <td className="py-2 pr-4">
                        {new Date(x.fromUtc).toLocaleString()} → {new Date(x.toUtc).toLocaleString()}
                      </td>
                      <td className="py-2 pr-4">{x.rowCount}</td>
                      <td className="py-2 pr-4 font-mono text-xs">{x.sha256}</td>
                      <td className="py-2 pr-4 text-right">
                        {x.publicUrl ? (
                          <a href={x.publicUrl} target="_blank" rel="noreferrer">
                            <Button variant="outline" size="sm">
                              <Download className="h-4 w-4 mr-2" />
                              Download
                            </Button>
                          </a>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Retention Runs</CardTitle>
          <CardDescription>Recent data-retention job runs and their purge counts.</CardDescription>
        </CardHeader>
        <CardContent>
          {!retentionRuns || retentionRuns.length === 0 ? (
            <div className="text-slate-600">No retention runs recorded yet.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-slate-500">
                  <tr>
                    <th className="py-2 pr-4">Started</th>
                    <th className="py-2 pr-4">Success</th>
                    <th className="py-2 pr-4">Counts</th>
                    <th className="py-2 pr-4">Error</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {retentionRuns.map((r) => (
                    <tr key={r.startedAtUtc}>
                      <td className="py-2 pr-4">{new Date(r.startedAtUtc).toLocaleString()}</td>
                      <td className="py-2 pr-4">{r.success ? 'Yes' : 'No'}</td>
                      <td className="py-2 pr-4 font-mono text-xs max-w-[420px] truncate">{r.countsJson || ''}</td>
                      <td className="py-2 pr-4 text-red-700 max-w-[420px] truncate">{r.error || ''}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
