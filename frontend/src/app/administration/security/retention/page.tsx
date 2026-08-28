'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Save, ShieldCheck } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { auditGovernanceService } from '@/services/audit-governance.service';
import {
  dataRetentionService,
  type UpdateDataRetentionPolicyRequest,
} from '@/services/dataRetentionService';

function fmtUtc(iso?: string | null) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString();
}

export default function DataRetentionAdminPage() {
  const qc = useQueryClient();

  const {
    data: policy,
    isLoading: policyLoading,
    error: policyError,
  } = useQuery({
    queryKey: ['admin', 'retention', 'policy'],
    queryFn: () => dataRetentionService.getPolicy(),
  });

  const { data: runs, isLoading: runsLoading } = useQuery({
    queryKey: ['admin', 'retention', 'runs'],
    queryFn: () => dataRetentionService.listRuns(50),
  });

  const [form, setForm] = useState<UpdateDataRetentionPolicyRequest>({
    enabled: true,
    auditLogRetentionDays: 2555,
    securityLogRetentionDays: 365,
    notificationRetentionDays: 180,
    ehcAuditEventRetentionDays: 365,
    workflowAuditRetentionDays: 2555,
  });

  // Hydrate form when loaded (once).
  useEffect(() => {
    if (!policy) return;
    setForm({
      enabled: policy.enabled,
      auditLogRetentionDays: policy.auditLogRetentionDays,
      securityLogRetentionDays: policy.securityLogRetentionDays,
      notificationRetentionDays: policy.notificationRetentionDays,
      ehcAuditEventRetentionDays: policy.ehcAuditEventRetentionDays,
      workflowAuditRetentionDays: policy.workflowAuditRetentionDays,
    });
  }, [policy?.id]);

  const save = useMutation({
    mutationFn: async () => {
      await dataRetentionService.updatePolicy({
        enabled: !!form.enabled,
        auditLogRetentionDays: Math.max(
          2555,
          Number(form.auditLogRetentionDays) || 2555
        ),
        securityLogRetentionDays: Number(form.securityLogRetentionDays) || 365,
        notificationRetentionDays:
          Number(form.notificationRetentionDays) || 180,
        ehcAuditEventRetentionDays:
          Number(form.ehcAuditEventRetentionDays) || 365,
        workflowAuditRetentionDays: Math.max(
          2555,
          Number(form.workflowAuditRetentionDays) || 2555
        ),
      });
    },
    onSuccess: async () => {
      await qc.invalidateQueries({
        queryKey: ['admin', 'retention', 'policy'],
      });
      await qc.invalidateQueries({ queryKey: ['admin', 'retention', 'runs'] });
    },
  });

  const { data: coverage, isLoading: coverageLoading } = useQuery({
    queryKey: ['admin', 'audit-governance', 'coverage'],
    queryFn: () => auditGovernanceService.coverage(),
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Data Retention</h1>
        <p className="text-slate-600 mt-1">
          Configure tenant retention while preserving immutable statutory audit
          records.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Policy</CardTitle>
          <CardDescription>
            Audit and workflow evidence have a non-reducible seven-year
            (2,555-day) minimum.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {policyLoading ? (
            <div className="text-slate-600">Loading...</div>
          ) : policyError ? (
            <div className="text-red-600">Failed to load policy.</div>
          ) : null}

          <div className="flex items-center gap-3">
            <Switch
              checked={!!form.enabled}
              onCheckedChange={(v) => setForm((f) => ({ ...f, enabled: !!v }))}
            />
            <span className="text-sm text-slate-700">
              {form.enabled ? 'Enabled' : 'Disabled'}
            </span>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
            <div className="space-y-2">
              <Label>Audit logs (days)</Label>
              <Input
                type="number"
                min={2555}
                max={36500}
                value={String(form.auditLogRetentionDays)}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    auditLogRetentionDays: Number(e.target.value),
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Security logs (days)</Label>
              <Input
                type="number"
                min={1}
                max={3650}
                value={String(form.securityLogRetentionDays)}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    securityLogRetentionDays: Number(e.target.value),
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Notifications (days)</Label>
              <Input
                type="number"
                min={1}
                max={3650}
                value={String(form.notificationRetentionDays)}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    notificationRetentionDays: Number(e.target.value),
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>EHC audit events (days)</Label>
              <Input
                type="number"
                min={1}
                max={3650}
                value={String(form.ehcAuditEventRetentionDays)}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    ehcAuditEventRetentionDays: Number(e.target.value),
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label>Workflow evidence (days)</Label>
              <Input
                type="number"
                min={2555}
                max={36500}
                value={String(form.workflowAuditRetentionDays)}
                onChange={(e) =>
                  setForm((f) => ({
                    ...f,
                    workflowAuditRetentionDays: Number(e.target.value),
                  }))
                }
              />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Button onClick={() => save.mutate()} disabled={save.isPending}>
              <Save className="h-4 w-4 mr-2" />
              {save.isPending ? 'Saving...' : 'Save'}
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <ShieldCheck className="h-5 w-5" /> Audit event coverage
          </CardTitle>
          <CardDescription>
            Shared semantic coverage used by procurement, inventory, and future
            module contributors.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {coverageLoading ? (
            <div className="text-slate-600">Loading...</div>
          ) : coverage ? (
            <div className="space-y-3">
              <div className="flex items-center gap-2">
                <Badge
                  className={
                    coverage.isComplete
                      ? 'bg-green-100 text-green-800'
                      : 'bg-red-100 text-red-800'
                  }
                >
                  {coverage.isComplete
                    ? 'Complete'
                    : `${coverage.missingOperations.length} missing`}
                </Badge>
                <span className="text-sm text-slate-600">
                  Verified {fmtUtc(coverage.verifiedAtUtc)}
                </span>
              </div>
              <div className="flex flex-wrap gap-2">
                {coverage.definitions.map((item) => (
                  <Badge
                    key={`${item.module}-${String(item.operation)}`}
                    variant="outline"
                    title={item.emittedActions.join(', ')}
                  >
                    {String(item.operation)}
                  </Badge>
                ))}
              </div>
            </div>
          ) : (
            <div className="text-red-600">Coverage could not be loaded.</div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Recent Runs</CardTitle>
          <CardDescription>
            Shows the latest retention job runs for this tenant.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {runsLoading ? (
            <div className="text-slate-600">Loading...</div>
          ) : !runs || runs.length === 0 ? (
            <div className="text-slate-600">No runs yet.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="text-left text-slate-500">
                  <tr>
                    <th className="py-2 pr-4">Started</th>
                    <th className="py-2 pr-4">Completed</th>
                    <th className="py-2 pr-4">Status</th>
                    <th className="py-2 pr-4">Counts</th>
                    <th className="py-2 pr-4">Error</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {runs.map((r) => (
                    <tr key={r.id} className="hover:bg-slate-50">
                      <td className="py-2 pr-4">{fmtUtc(r.startedAtUtc)}</td>
                      <td className="py-2 pr-4">{fmtUtc(r.completedAtUtc)}</td>
                      <td className="py-2 pr-4">
                        {r.success ? (
                          <Badge className="bg-green-100 text-green-800">
                            Success
                          </Badge>
                        ) : (
                          <Badge className="bg-red-100 text-red-800">
                            Failed
                          </Badge>
                        )}
                      </td>
                      <td
                        className="py-2 pr-4 font-mono text-xs text-slate-700 max-w-[420px] truncate"
                        title={r.countsJson || ''}
                      >
                        {r.countsJson || '—'}
                      </td>
                      <td
                        className="py-2 pr-4 text-xs text-slate-700 max-w-[420px] truncate"
                        title={r.error || ''}
                      >
                        {r.error || '—'}
                      </td>
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
