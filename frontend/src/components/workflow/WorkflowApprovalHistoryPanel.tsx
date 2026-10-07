'use client';

import * as React from 'react';
import dynamic from 'next/dynamic';
import { CheckCircle, Clock, Download, Eye, FileText, Loader2, Upload, User, XCircle } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Separator } from '@/components/ui/separator';
import { WorkflowApprovalActions } from '@/components/workflow/WorkflowApprovalActions';
import { useToast } from '@/hooks/use-toast';
import { workflowApiService } from '@/services/workflow-api.service';
import { getWorkflowVisibility } from '@/components/workflow/workflowVisibility';
import type {
  WorkflowApprovalChecklistResponseDto,
  WorkflowApprovalStatus,
  WorkflowEntityAuditDto,
  WorkflowEntitySummaryDto,
  WorkflowStepAuditDto,
  WorkflowStepInstanceStatus,
  WorkflowTaskAttachmentDto,
} from '@/types/workflow';

const ProcedurePdfViewer = dynamic(() => import('@/components/procedures/ProcedurePdfViewer'), { ssr: false });

export interface WorkflowApprovalHistoryPanelProps {
  entityType: string;
  entityId: string;
  entityLabel?: string;
  entityNumber?: string;
  status?: string;
  currentStepName?: string;
  workflowSummary?: WorkflowEntitySummaryDto;
  workflowSummaryLoading?: boolean;
  workflowSummaryError?: string;
  loadWorkflowSummary?: boolean;
  canSubmit?: boolean;
  canApproveReject?: boolean;
  onSubmit?: () => Promise<void>;
  onApprove?: (comments: string, checklistResponses?: WorkflowApprovalChecklistResponseDto[]) => Promise<void>;
  onReject?: (comments: string, checklistResponses?: WorkflowApprovalChecklistResponseDto[]) => Promise<void>;
  onAfterAction?: () => Promise<void>;
  onOpenWorkflows?: () => void;
  showActions?: boolean;
}

const stepStatusLabels: Record<number, string> = {
  0: 'Pending',
  1: 'In Progress',
  2: 'Completed',
  3: 'Cancelled',
  4: 'Failed',
};

const approvalStatusLabels: Record<number, string> = {
  0: 'Pending',
  1: 'Approved',
  2: 'Rejected',
  3: 'Delegated',
  4: 'Expired',
  5: 'More Info Requested',
};

const instanceStatusLabels: Record<number, string> = {
  0: 'Created',
  1: 'In Progress',
  2: 'Completed',
  3: 'Cancelled',
  4: 'Failed',
  5: 'Suspended',
  6: 'Waiting',
};

const stepTypeLabels: Record<number, string> = {
  0: 'Task',
  1: 'Automatic',
  2: 'Approval',
  3: 'Decision',
  4: 'Script',
  5: 'Notification',
  6: 'Sub Workflow',
  7: 'Validation',
  8: 'Quality Control',
};

function enumLabel(value: unknown, labels: Record<number, string>) {
  if (typeof value === 'number') {
    return labels[value] || String(value);
  }

  if (typeof value === 'string') {
    const numeric = Number(value);
    if (!Number.isNaN(numeric) && value.trim() !== '') {
      return labels[numeric] || value;
    }

    return value.replace(/([a-z])([A-Z])/g, '$1 $2');
  }

  return 'Unknown';
}

function formatDateTime(value?: string | Date) {
  if (!value) return '-';
  const d = typeof value === 'string' ? new Date(value) : value;
  if (Number.isNaN(d.getTime())) return '-';
  return d.toLocaleString();
}

function formatFileSize(bytes?: number) {
  if (!bytes || bytes <= 0) return '';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function safeDownloadFileName(fileName?: string) {
  return (fileName || 'workflow-document').replace(/[\\/:*?"<>|]+/g, '_');
}

function attachmentDownloadKey(stepInstanceId?: string, attachmentId?: string) {
  return `${stepInstanceId || 'step'}:${attachmentId || 'attachment'}`;
}

function stepStatusBadge(status: WorkflowStepInstanceStatus) {
  const label = enumLabel(status, stepStatusLabels);
  const map: Record<string, { cls: string }> = {
    Pending: { cls: 'bg-yellow-100 text-yellow-800' },
    'In Progress': { cls: 'bg-blue-100 text-blue-800' },
    InProgress: { cls: 'bg-blue-100 text-blue-800' },
    Completed: { cls: 'bg-green-100 text-green-800' },
    Failed: { cls: 'bg-red-100 text-red-800' },
    Cancelled: { cls: 'bg-gray-100 text-gray-800' },
  };

  const cfg = map[label] || { cls: 'bg-gray-100 text-gray-800' };
  return <Badge className={cfg.cls}>{label}</Badge>;
}

function approvalStatusBadge(status: WorkflowApprovalStatus) {
  const label = enumLabel(status, approvalStatusLabels);
  const map: Record<string, { cls: string; icon: React.ReactNode }> = {
    Pending: { cls: 'bg-yellow-100 text-yellow-800', icon: <Clock className="h-3 w-3 mr-1" /> },
    Approved: { cls: 'bg-green-100 text-green-800', icon: <CheckCircle className="h-3 w-3 mr-1" /> },
    Rejected: { cls: 'bg-red-100 text-red-800', icon: <XCircle className="h-3 w-3 mr-1" /> },
    Delegated: { cls: 'bg-indigo-100 text-indigo-800', icon: <User className="h-3 w-3 mr-1" /> },
    Expired: { cls: 'bg-gray-100 text-gray-800', icon: <Clock className="h-3 w-3 mr-1" /> },
    'More Info Requested': { cls: 'bg-blue-100 text-blue-800', icon: <Clock className="h-3 w-3 mr-1" /> },
    Queued: { cls: 'bg-slate-100 text-slate-700', icon: <Clock className="h-3 w-3 mr-1" /> },
  };

  const cfg = map[label] || { cls: 'bg-gray-100 text-gray-800', icon: <FileText className="h-3 w-3 mr-1" /> };
  return (
    <Badge className={cfg.cls}>
      {cfg.icon}
      {label}
    </Badge>
  );
}

function getTaskActionLabel(step: WorkflowStepAuditDto) {
  const action = step.taskConfig?.taskActionType?.trim();
  if (!action || action.toLowerCase() === 'general') {
    return null;
  }

  return action.toLowerCase() === 'document'
    ? 'Attach Document'
    : action.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function AttachmentRow({
  stepInstanceId,
  attachment,
  downloadingAttachmentId,
  onDownloadAttachment,
  onViewAttachment,
}: {
  stepInstanceId?: string;
  attachment: WorkflowTaskAttachmentDto;
  downloadingAttachmentId?: string | null;
  onDownloadAttachment?: (stepInstanceId: string, attachment: WorkflowTaskAttachmentDto) => void;
  onViewAttachment?: (stepInstanceId: string, attachment: WorkflowTaskAttachmentDto) => void;
}) {
  const canDownload = Boolean(stepInstanceId && attachment.id && onDownloadAttachment);
  const key = attachmentDownloadKey(stepInstanceId, attachment.id);
  const downloading = downloadingAttachmentId === key;

  return (
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-md border bg-background px-2 py-1.5 text-xs text-muted-foreground">
      <div className="flex min-w-0 flex-wrap items-center gap-2">
        <FileText className="h-3.5 w-3.5 shrink-0" />
        <span className="font-medium text-foreground">{attachment.fileName}</span>
        {attachment.documentType && <span>{attachment.documentType}</span>}
        {attachment.documentName && <span>{attachment.documentName}</span>}
        {attachment.uploadedByName && <span>by {attachment.uploadedByName}</span>}
        <span>{formatDateTime(attachment.uploadedAt)}</span>
        {attachment.fileSizeBytes ? <span>{formatFileSize(attachment.fileSizeBytes)}</span> : null}
      </div>
      <div className="flex items-center gap-1">
        {onViewAttachment && attachment.fileName?.toLowerCase().endsWith('.pdf') ? (
          <Button type="button" variant="ghost" size="sm" className="h-7 px-2 text-xs" disabled={!canDownload || downloading} onClick={() => stepInstanceId && onViewAttachment(stepInstanceId, attachment)}>
            <Eye className="mr-1 h-3.5 w-3.5" /> View
          </Button>
        ) : null}
        {onDownloadAttachment ? (
          <Button type="button" variant="ghost" size="sm" className="h-7 px-2 text-xs" disabled={!canDownload || downloading} onClick={() => stepInstanceId && onDownloadAttachment(stepInstanceId, attachment)}>
            {downloading ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Download className="mr-1 h-3.5 w-3.5" />}
            Download
          </Button>
        ) : null}
      </div>
    </div>
  );
}

function StepRequirementSummary({
  step,
  downloadingAttachmentId,
  onDownloadAttachment,
  onViewAttachment,
}: {
  step: WorkflowStepAuditDto;
  downloadingAttachmentId?: string | null;
  onDownloadAttachment?: (stepInstanceId: string, attachment: WorkflowTaskAttachmentDto) => void;
  onViewAttachment?: (stepInstanceId: string, attachment: WorkflowTaskAttachmentDto) => void;
}) {
  const taskLabel = getTaskActionLabel(step);
  const checklist = step.checklist || [];
  const checklistResponses = step.checklistResponses || [];
  const attachments = step.taskAttachments || [];

  if (!taskLabel && checklist.length === 0 && attachments.length === 0) {
    return null;
  }

  return (
    <div className="mt-3 space-y-3 rounded-md border bg-muted/30 p-3">
      {taskLabel && (
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-2 text-sm font-medium">
            <Upload className="h-4 w-4 text-blue-600" />
            {taskLabel}
            {step.taskConfig?.documentName && (
              <Badge variant="outline" className="text-xs">
                {step.taskConfig.documentName}
              </Badge>
            )}
          </div>
          {step.taskConfig?.instructions && (
            <p className="text-xs text-muted-foreground">{step.taskConfig.instructions}</p>
          )}
        </div>
      )}

      {checklist.length > 0 && (
        <div className="space-y-2">
          <p className="text-xs font-medium text-muted-foreground">Step Checklist</p>
          <div className="space-y-1.5">
            {checklist.map((item, index) => {
              const itemKey = (item.id || item.name).trim().toLowerCase();
              const response = checklistResponses.find((entry) =>
                (entry.id || entry.name).trim().toLowerCase() === itemKey
              );
              const itemAttachments = attachments.filter((attachment) =>
                (attachment.checklistItemId || attachment.requirementKey || '').trim().toLowerCase() === itemKey
              );

              return (
                <div key={item.id || `${item.name}-${index}`} className="rounded-md border bg-background px-2.5 py-2">
                  <div className="flex flex-wrap items-center gap-2 text-xs">
                    {response?.isSatisfied ? (
                      <CheckCircle className="h-3.5 w-3.5 text-green-600" />
                    ) : (
                      <XCircle className="h-3.5 w-3.5 text-muted-foreground" />
                    )}
                    <span className="font-medium text-foreground">{item.name}</span>
                    {item.isRequired && <Badge variant="secondary" className="text-[10px]">Required</Badge>}
                    {item.requiresDocument && (
                      <Badge variant="outline" className="text-[10px]">
                        {item.documentType || 'Document evidence'}
                      </Badge>
                    )}
                    {response?.completedByName && <span>by {response.completedByName}</span>}
                    {response?.completedAt && <span>{formatDateTime(response.completedAt)}</span>}
                  </div>
                  {response?.notes && <p className="mt-1 text-xs text-muted-foreground">{response.notes}</p>}
                  {itemAttachments.length > 0 && (
                    <div className="mt-2 space-y-1">
                      {itemAttachments.map((attachment) => (
                        <AttachmentRow
                          key={attachment.id}
                          stepInstanceId={step.stepInstanceId}
                          attachment={attachment}
                          downloadingAttachmentId={downloadingAttachmentId}
                          onDownloadAttachment={onDownloadAttachment}
                          onViewAttachment={onViewAttachment}
                        />
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}

      {attachments.some((attachment) => !attachment.checklistItemId) && (
        <div className="space-y-1">
          <p className="text-xs font-medium text-muted-foreground">Uploaded Documents</p>
          <div className="space-y-1">
            {attachments.filter((attachment) => !attachment.checklistItemId).map((attachment) => (
              <AttachmentRow
                key={attachment.id}
                stepInstanceId={step.stepInstanceId}
                attachment={attachment}
                downloadingAttachmentId={downloadingAttachmentId}
                onDownloadAttachment={onDownloadAttachment}
                onViewAttachment={onViewAttachment}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export function WorkflowApprovalHistoryPanel({
  entityType,
  entityId,
  entityLabel = 'Record',
  entityNumber,
  status,
  currentStepName,
  workflowSummary,
  workflowSummaryLoading,
  workflowSummaryError,
  loadWorkflowSummary = true,
  canSubmit,
  canApproveReject,
  onSubmit,
  onApprove,
  onReject,
  onAfterAction,
  onOpenWorkflows,
  showActions = true,
}: WorkflowApprovalHistoryPanelProps) {
  const { toast } = useToast();
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [audit, setAudit] = React.useState<WorkflowEntityAuditDto | null>(null);
  const [summary, setSummary] = React.useState<WorkflowEntitySummaryDto | null>(workflowSummary ?? null);
  const [downloadingAttachmentId, setDownloadingAttachmentId] = React.useState<string | null>(null);
  const [preview, setPreview] = React.useState<{ url: string; fileName: string } | null>(null);

  const effectiveSummary = workflowSummary ?? summary;
  const effectiveStatus = status || enumLabel(effectiveSummary?.status ?? audit?.status, instanceStatusLabels);
  const visibility = getWorkflowVisibility({
    summary: effectiveSummary, loading: loading || workflowSummaryLoading, error: error || workflowSummaryError,
  });

  const loadWorkflow = React.useCallback(async () => {
    if (!entityType || !entityId) return;

    try {
      setLoading(true);
      setError(null);
      const [auditData, summaryData] = await Promise.all([
        workflowApiService.getWorkflowEntityAudit(entityType, entityId),
        loadWorkflowSummary
          ? workflowApiService.getWorkflowEntitySummary(entityType, entityId)
          : Promise.resolve(undefined),
      ]);

      setAudit(auditData);
      if (summaryData) {
        setSummary(summaryData);
      }
    } catch (e: any) {
      setError(e?.message || 'Failed to load workflow details');
    } finally {
      setLoading(false);
    }
  }, [entityType, entityId, loadWorkflowSummary]);

  React.useEffect(() => {
    loadWorkflow();
  }, [loadWorkflow]);

  React.useEffect(() => {
    if (workflowSummary) {
      setSummary(workflowSummary);
    }
  }, [workflowSummary]);

  const runAfterAction = async () => {
    await loadWorkflow();
    if (onAfterAction) {
      await onAfterAction();
    }
  };

  const handleDownloadAttachment = React.useCallback(
    async (stepInstanceId: string, attachment: WorkflowTaskAttachmentDto) => {
      const key = attachmentDownloadKey(stepInstanceId, attachment.id);

      try {
        setDownloadingAttachmentId(key);
        const blob = await workflowApiService.downloadStepAttachment(stepInstanceId, attachment.id);
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = safeDownloadFileName(attachment.fileName);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      } catch (e: any) {
        toast({
          title: 'Failed to download document',
          description: e?.message || 'The workflow document could not be downloaded.',
          variant: 'destructive',
        });
      } finally {
        setDownloadingAttachmentId(null);
      }
    },
    [toast]
  );

  const handleViewAttachment = React.useCallback(async (stepInstanceId: string, attachment: WorkflowTaskAttachmentDto) => {
    const key = attachmentDownloadKey(stepInstanceId, attachment.id);
    try {
      setDownloadingAttachmentId(key);
      const blob = await workflowApiService.downloadStepAttachment(stepInstanceId, attachment.id);
      setPreview((current) => {
        if (current?.url) window.URL.revokeObjectURL(current.url);
        return { url: window.URL.createObjectURL(blob), fileName: attachment.fileName || 'Workflow document.pdf' };
      });
    } catch (e: any) {
      toast({ title: 'Failed to open document', description: e?.message || 'The workflow document could not be opened.', variant: 'destructive' });
    } finally {
      setDownloadingAttachmentId(null);
    }
  }, [toast]);

  React.useEffect(() => () => {
    if (preview?.url) window.URL.revokeObjectURL(preview.url);
  }, [preview?.url]);

  if (loading || workflowSummaryLoading) {
    return (
      <div className="flex items-center justify-center py-10">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error || workflowSummaryError) {
    return (
      <Card className="border-red-200 bg-red-50">
        <CardContent className="pt-6">
          <div className="flex items-center gap-2">
            <XCircle className="h-5 w-5 text-red-600" />
            <p className="text-red-900">{error || workflowSummaryError}</p>
          </div>
        </CardContent>
      </Card>
    );
  }

  if (visibility.direct && !audit) return null;

  if (!audit && !effectiveSummary?.hasActiveInstance) {
    return (
      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <CardTitle>Workflow</CardTitle>
              <CardDescription>No workflow history found for this record.</CardDescription>
            </div>
            {showActions && (canSubmit || canApproveReject) && (
              <WorkflowApprovalActions
                entityType={entityType}
                entityId={entityId}
                entityLabel={entityLabel}
                entityNumber={entityNumber}
                status={effectiveStatus}
                currentStepName={currentStepName}
                workflowSummary={effectiveSummary ?? undefined}
                canSubmit={canSubmit}
                canApproveReject={canApproveReject}
                onSubmit={onSubmit}
                onApprove={onApprove}
                onReject={onReject}
                onAfterAction={runAfterAction}
                onOpenWorkflows={onOpenWorkflows}
              />
            )}
          </div>
        </CardHeader>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {effectiveSummary?.hasActiveInstance && (
        <Card>
          <CardHeader>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <CardTitle>Current Workflow Step</CardTitle>
                <CardDescription>
                  {effectiveSummary.workflowName || audit?.workflowName || 'Workflow'} • {effectiveStatus}
                </CardDescription>
              </div>
              {showActions && (
                <WorkflowApprovalActions
                  entityType={entityType}
                  entityId={entityId}
                  entityLabel={entityLabel}
                  entityNumber={entityNumber}
                  status={effectiveStatus}
                  currentStepName={currentStepName || effectiveSummary.currentStepName}
                  workflowSummary={effectiveSummary}
                  canSubmit={canSubmit}
                  canApproveReject={canApproveReject}
                  onSubmit={onSubmit}
                  onApprove={onApprove}
                  onReject={onReject}
                  onAfterAction={runAfterAction}
                  onOpenWorkflows={onOpenWorkflows}
                />
              )}
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex flex-wrap items-center gap-2">
              {effectiveSummary.currentStepName && (
                <Badge variant="outline">Step: {effectiveSummary.currentStepName}</Badge>
              )}
              {effectiveSummary.currentStepType !== undefined && effectiveSummary.currentStepType !== null && (
                <Badge variant="secondary">{enumLabel(effectiveSummary.currentStepType, stepTypeLabels)}</Badge>
              )}
              {effectiveSummary.pendingApprovers?.map((approver, index) => (
                <Badge key={`${approver.approverId || approver.approverRole || approver.approverName}-${index}`} variant="outline">
                  Pending: {approver.approverName || approver.approverRole || 'Approver'}
                </Badge>
              ))}
            </div>

            {effectiveSummary.currentStepTaskConfig && (
              <div className="rounded-md border bg-blue-50 p-3 text-sm">
                <div className="flex flex-wrap items-center gap-2 font-medium text-blue-900">
                  <Upload className="h-4 w-4" />
                  {effectiveSummary.currentStepTaskConfig.requiresDocument ? 'Document Required' : 'Task Required'}
                  {effectiveSummary.currentStepTaskConfig.documentName && (
                    <Badge variant="outline" className="bg-white">
                      {effectiveSummary.currentStepTaskConfig.documentName}
                    </Badge>
                  )}
                </div>
                {effectiveSummary.currentStepTaskConfig.instructions && (
                  <p className="mt-1 text-blue-800">{effectiveSummary.currentStepTaskConfig.instructions}</p>
                )}
              </div>
            )}

            {effectiveSummary.currentStepTaskAttachments?.length ? (
              <div className="space-y-1">
                <p className="text-xs font-medium text-muted-foreground">Current Step Documents</p>
                {effectiveSummary.currentStepTaskAttachments.map((attachment) => (
                  <AttachmentRow
                    key={attachment.id}
                    stepInstanceId={effectiveSummary.currentStepInstanceId}
                    attachment={attachment}
                    downloadingAttachmentId={downloadingAttachmentId}
                    onDownloadAttachment={handleDownloadAttachment}
                    onViewAttachment={handleViewAttachment}
                  />
                ))}
              </div>
            ) : null}
          </CardContent>
        </Card>
      )}

      {audit && (
        <Card>
          <CardHeader>
            <CardTitle>{visibility.direct ? 'History' : 'Workflow History'}</CardTitle>
            <CardDescription>
              Workflow: <span className="font-medium">{audit.workflowName}</span> • Status:{' '}
              <span className="font-medium">{enumLabel(audit.status, instanceStatusLabels)}</span>
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
                <div key={step.stepInstanceId} className="rounded-md border p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="font-medium">{step.stepName}</p>
                      {stepStatusBadge(step.status)}
                      <Badge variant="outline" className="text-xs">
                        {enumLabel(step.stepType, stepTypeLabels)}
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

                  <StepRequirementSummary
                    step={step}
                    downloadingAttachmentId={downloadingAttachmentId}
                    onDownloadAttachment={handleDownloadAttachment}
                    onViewAttachment={handleViewAttachment}
                  />

                  {step.approvals?.length > 0 && (
                    <>
                      <Separator className="my-4" />
                      <div className="space-y-3">
                        {step.approvals.map((a) => (
                          <div key={a.approvalId} className="flex flex-col gap-2">
                            <div className="flex flex-wrap items-center justify-between gap-2">
                              <div className="flex flex-wrap items-center gap-2">
                                 <p className="text-sm font-medium">
                                   {a.approverName || a.approverRole || a.approverId || 'Approver'}
                                 </p>
                                 <Badge variant="outline" className="text-xs">Group {a.approvalGroup || 1}</Badge>
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
      )}
      <Dialog open={Boolean(preview)} onOpenChange={(open) => !open && setPreview(null)}>
        <DialogContent className="max-h-[92vh] max-w-6xl overflow-y-auto">
          <DialogHeader><DialogTitle>{preview?.fileName || 'Document preview'}</DialogTitle></DialogHeader>
          {preview ? <ProcedurePdfViewer fileUrl={preview.url} fileName={preview.fileName} /> : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}
