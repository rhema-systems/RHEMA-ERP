'use client';

import * as React from 'react';
import { CheckCircle, Clock, FileText, Loader2, User, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { workflowApiService } from '@/services/workflow-api.service';
import type { WorkflowApprovalStatus, WorkflowEntityAuditDto, WorkflowStepInstanceStatus } from '@/types/workflow';

function formatDateTime(value?: string | Date) {
  if (!value) return '—';
  const d = typeof value === 'string' ? new Date(value) : value;
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString();
}

function stepStatusBadge(status: WorkflowStepInstanceStatus) {
  const map: Record<string, { label: string; cls: string }> = {
    Pending: { label: 'Pending', cls: 'bg-yellow-100 text-yellow-800' },
    InProgress: { label: 'In Progress', cls: 'bg-blue-100 text-blue-800' },
    Completed: { label: 'Completed', cls: 'bg-green-100 text-green-800' },
    Failed: { label: 'Failed', cls: 'bg-red-100 text-red-800' },
    Cancelled: { label: 'Cancelled', cls: 'bg-gray-100 text-gray-800' },
  };

  const key = String(status);
  const cfg = map[key] || { label: key, cls: 'bg-gray-100 text-gray-800' };
  return <Badge className={cfg.cls}>{cfg.label}</Badge>;
}

function approvalStatusBadge(status: WorkflowApprovalStatus) {
  const map: Record<string, { label: string; cls: string; icon: React.ReactNode }> = {
    Pending: { label: 'Pending', cls: 'bg-yellow-100 text-yellow-800', icon: <Clock className="h-3 w-3 mr-1" /> },
    Approved: { label: 'Approved', cls: 'bg-green-100 text-green-800', icon: <CheckCircle className="h-3 w-3 mr-1" /> },
    Rejected: { label: 'Rejected', cls: 'bg-red-100 text-red-800', icon: <XCircle className="h-3 w-3 mr-1" /> },
    Delegated: { label: 'Delegated', cls: 'bg-indigo-100 text-indigo-800', icon: <User className="h-3 w-3 mr-1" /> },
    Expired: { label: 'Expired', cls: 'bg-gray-100 text-gray-800', icon: <Clock className="h-3 w-3 mr-1" /> },
    MoreInfoRequested: { label: 'More Info', cls: 'bg-blue-100 text-blue-800', icon: <Clock className="h-3 w-3 mr-1" /> },
  };

  const key = String(status);
  const cfg = map[key] || { label: key, cls: 'bg-gray-100 text-gray-800', icon: <FileText className="h-3 w-3 mr-1" /> };
  return (
    <Badge className={cfg.cls}>
      {cfg.icon}
      {cfg.label}
    </Badge>
  );
}

export function WorkflowApprovalHistoryPanel({ entityType, entityId }: { entityType: string; entityId: string }) {
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [audit, setAudit] = React.useState<WorkflowEntityAuditDto | null>(null);

  React.useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await workflowApiService.getWorkflowEntityAudit(entityType, entityId);
        if (!mounted) return;
        setAudit(data);
      } catch (e: any) {
        if (!mounted) return;
        setError(e?.message || 'Failed to load workflow audit');
      } finally {
        if (mounted) setLoading(false);
      }
    })();

    return () => {
      mounted = false;
    };
  }, [entityType, entityId]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error) {
    return (
      <Card className="border-red-200 bg-red-50">
        <CardContent className="pt-6">
          <div className="flex items-center gap-2">
            <XCircle className="h-5 w-5 text-red-600" />
            <p className="text-red-900">{error}</p>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (!audit) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Approval History</CardTitle>
          <CardDescription>No workflow history found for this record.</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Approval History</CardTitle>
        <CardDescription>
          Workflow: <span className="font-medium">{audit.workflowName}</span> • Status:{' '}
          <span className="font-medium">{String(audit.status)}</span>
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        <div className="text-sm text-muted-foreground">
          Started: <span className="text-foreground">{formatDateTime(audit.startedDate)}</span>
          {audit.completedDate && (
            <>
              {' '}
              • Completed: <span className="text-foreground">{formatDateTime(audit.completedDate)}</span>
            </>
          )}
        </div>

        <Separator />

        <div className="space-y-4">
          {audit.steps.map((step) => (
            <div key={step.stepInstanceId} className="rounded-lg border p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2">
                  <p className="font-medium">{step.stepName}</p>
                  {stepStatusBadge(step.status)}
                  <Badge variant="outline" className="text-xs">
                    {String(step.stepType)}
                  </Badge>
                </div>
                <div className="text-xs text-muted-foreground">
                  Started: {formatDateTime(step.startedDate)} • Completed: {formatDateTime(step.completedDate)}
                </div>
              </div>

              {(step.assignedToName || step.assignedToId) && (
                <div className="mt-2 text-sm text-muted-foreground">
                  Assigned To: <span className="text-foreground">{step.assignedToName || step.assignedToId}</span>
                </div>
              )}

              {step.comments && (
                <div className="mt-3 text-sm">
                  <div className="text-muted-foreground">Step Comment</div>
                  <div className="mt-1 rounded bg-muted p-2">{step.comments}</div>
                </div>
              )}

              {step.approvals?.length > 0 && (
                <>
                  <Separator className="my-4" />
                  <div className="space-y-3">
                    {step.approvals.map((a) => (
                      <div key={a.approvalId} className="flex flex-col gap-2">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <div className="flex items-center gap-2">
                            <p className="text-sm font-medium">
                              {a.approverName || a.approverRole || a.approverId || 'Approver'}
                            </p>
                            {approvalStatusBadge(a.status)}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            Requested: {formatDateTime(a.requestedDate)} • Processed: {formatDateTime(a.processedDate)}
                          </div>
                        </div>
                        {a.processedByName && a.processedByName !== a.approverName && (
                          <div className="text-xs text-muted-foreground">
                            Processed By: <span className="text-foreground">{a.processedByName}</span>
                          </div>
                        )}
                        {a.comments && (
                          <div className="rounded bg-muted p-2 text-sm">
                            {a.comments}
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}

