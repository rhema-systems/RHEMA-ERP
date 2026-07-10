'use client';

import * as React from 'react';
import { CheckCircle, FileText, Loader2, Upload, XCircle } from 'lucide-react';
import { toast } from 'sonner';

import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { workflowApiService } from '@/services/workflow-api.service';
import type {
  WorkflowApprovalChecklistResponseDto,
  WorkflowQualityCheckDto,
  WorkflowTaskAttachmentDto,
  WorkflowSignaturePolicyDto,
  WorkflowSignatureSubmissionDto,
} from '@/types/workflow';
import { WorkflowSignatureMethod } from '@/types/workflow';

export type WorkflowApprovalDialogMode = 'approve' | 'reject';

export interface WorkflowApprovalCommentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: WorkflowApprovalDialogMode;
  title: string;
  description?: React.ReactNode;
  entitySummary?: React.ReactNode;
  checklistItems?: WorkflowQualityCheckDto[];
  stepInstanceId?: string;
  initialAttachments?: WorkflowTaskAttachmentDto[];
  signaturePolicy?: WorkflowSignaturePolicyDto;
  onConfirm: (
    comments: string,
    checklistResponses?: WorkflowApprovalChecklistResponseDto[],
    signature?: WorkflowSignatureSubmissionDto
  ) => void | boolean | Promise<void | boolean>;
  isLoading?: boolean;
}

export function WorkflowApprovalCommentDialog({
  open,
  onOpenChange,
  mode,
  title,
  description,
  entitySummary,
  checklistItems = [],
  stepInstanceId,
  initialAttachments = [],
  signaturePolicy,
  onConfirm,
  isLoading = false,
}: WorkflowApprovalCommentDialogProps) {
  const [comments, setComments] = React.useState('');
  const [checklistState, setChecklistState] = React.useState<Record<string, WorkflowApprovalChecklistResponseDto>>({});
  const [attachments, setAttachments] = React.useState<WorkflowTaskAttachmentDto[]>(initialAttachments);
  const [uploadingKey, setUploadingKey] = React.useState<string | null>(null);
  const [signatureAccepted, setSignatureAccepted] = React.useState(false);
  const [certificateBase64, setCertificateBase64] = React.useState('');
  const [externalReference, setExternalReference] = React.useState('');

  React.useEffect(() => {
    if (!open) {
      setComments('');
      setChecklistState({});
      setAttachments([]);
      setUploadingKey(null);
      setSignatureAccepted(false);
      setCertificateBase64('');
      setExternalReference('');
      return;
    }

    const initialState: Record<string, WorkflowApprovalChecklistResponseDto> = {};
    checklistItems.forEach((item, index) => {
      const key = getChecklistKey(item, index);
      initialState[key] = {
        id: item.id,
        name: item.name,
        isSatisfied: false,
        notes: '',
      };
    });
    setChecklistState(initialState);
    setAttachments(initialAttachments);

    if (stepInstanceId) {
      workflowApiService.getStepAttachments(stepInstanceId)
        .then((storedAttachments) => {
          setAttachments((current) => {
            const byId = new Map([...storedAttachments, ...current].map((attachment) => [attachment.id, attachment]));
            return Array.from(byId.values());
          });
        })
        .catch(() => {
          // The upload action will still surface a specific error if evidence is required.
        });
    }
  }, [open, checklistItems, initialAttachments, stepInstanceId]);

  const isReject = mode === 'reject';
  const commentsRequired = isReject;
  const approvalChecklistItems = isReject ? [] : checklistItems;
  const requiredChecklistIncomplete = approvalChecklistItems.some((item, index) => {
    if (item.isRequired === false) return false;
    const response = checklistState[getChecklistKey(item, index)];
    return !response?.isSatisfied;
  });
  const requiredDocumentIncomplete = approvalChecklistItems.some((item, index) => {
    if (!item.requiresDocument) return false;
    const response = checklistState[getChecklistKey(item, index)];
    if (item.isRequired === false && response?.isSatisfied !== true) return false;
    return getChecklistAttachments(item, index, attachments).length === 0;
  });
  const confirmDisabled =
    (commentsRequired && !comments.trim()) ||
    requiredChecklistIncomplete ||
    requiredDocumentIncomplete ||
    uploadingKey !== null ||
    (!isReject && signaturePolicy?.isRequired === true && !signatureAccepted) ||
    (!isReject && signaturePolicy?.isRequired === true && signaturePolicy.method === WorkflowSignatureMethod.DigitalCertificate && !certificateBase64) ||
    (!isReject && signaturePolicy?.isRequired === true && signaturePolicy.method === WorkflowSignatureMethod.ExternalProvider && !externalReference.trim());

  const buildSignature = (): WorkflowSignatureSubmissionDto | undefined => {
    if (isReject || signaturePolicy?.isRequired !== true) return undefined;
    return {
      method: signaturePolicy.method,
      attestation: signaturePolicy.attestationText,
      certificateBase64: certificateBase64 || undefined,
      externalReference: externalReference.trim() || undefined,
      signedAt: new Date().toISOString(),
    };
  };

  const setChecklistResponse = (
    item: WorkflowQualityCheckDto,
    index: number,
    updates: Partial<WorkflowApprovalChecklistResponseDto>
  ) => {
    const key = getChecklistKey(item, index);
    setChecklistState((current) => {
      const existing = current[key];

      return {
        ...current,
        [key]: {
          ...existing,
          ...updates,
          id: item.id,
          name: item.name,
          isSatisfied: updates.isSatisfied ?? existing?.isSatisfied ?? false,
          notes: updates.notes ?? existing?.notes ?? '',
        },
      };
    });
  };

  const buildChecklistResponses = (): WorkflowApprovalChecklistResponseDto[] | undefined => {
    if (approvalChecklistItems.length === 0) {
      return undefined;
    }

    return approvalChecklistItems.map((item, index) => {
      const key = getChecklistKey(item, index);
      return {
        id: item.id,
        name: item.name,
        isSatisfied: checklistState[key]?.isSatisfied === true,
        notes: checklistState[key]?.notes?.trim() || undefined,
        attachmentIds: getChecklistAttachments(item, index, attachments).map((attachment) => attachment.id),
      };
    });
  };

  const uploadChecklistDocument = async (item: WorkflowQualityCheckDto, index: number, file?: File) => {
    if (!file || !stepInstanceId) {
      if (file && !stepInstanceId) {
        toast.error('Cannot upload document', {
          description: 'The current workflow step could not be identified. Refresh the page and try again.',
        });
      }
      return;
    }

    const key = getChecklistKey(item, index);
    try {
      setUploadingKey(key);
      const attachment = await workflowApiService.uploadStepAttachment(
        stepInstanceId,
        file,
        key,
        item.documentName,
        item.documentType
      );
      setAttachments((current) => [
        ...current.filter((existing) => existing.id !== attachment.id),
        attachment,
      ]);
      toast.success('Document attached', { description: file.name });
    } catch (error: any) {
      toast.error('Failed to attach document', { description: error?.message || undefined });
    } finally {
      setUploadingKey(null);
    }
  };

  return (
    <ConfirmationDialog
      open={open}
      onOpenChange={onOpenChange}
      title={title}
      description={description}
      variant={isReject ? 'destructive' : 'default'}
      confirmText={
        isLoading ? 'Processing...' : isReject ? 'Reject' : 'Approve'
      }
      cancelText="Cancel"
      onConfirm={() => onConfirm(comments.trim(), buildChecklistResponses(), buildSignature())}
      isLoading={isLoading}
      confirmDisabled={confirmDisabled}
      maxWidth="680px"
    >
      {entitySummary && <div className="space-y-2">{entitySummary}</div>}

      {approvalChecklistItems.length > 0 && (
        <div className="space-y-3 rounded-md border bg-muted/30 p-3">
          <div>
            <div className="text-sm font-medium">Approval Checklist</div>
            <div className="text-xs text-muted-foreground">
              Complete all required checks before approving.
            </div>
          </div>
          <div className="space-y-3">
            {approvalChecklistItems.map((item, index) => {
              const key = getChecklistKey(item, index);
              const response = checklistState[key];
              const itemAttachments = getChecklistAttachments(item, index, attachments);
              const isUploading = uploadingKey === key;
              return (
                <div key={key} className="space-y-2 rounded-md border bg-background p-3">
                  <div className="flex items-start gap-2">
                    <Checkbox
                      checked={response?.isSatisfied === true}
                      onCheckedChange={(checked) =>
                        setChecklistResponse(item, index, { isSatisfied: checked === true })
                      }
                    />
                    <div className="min-w-0 flex-1">
                      <div className="text-sm font-medium">
                        {item.name}
                        {item.isRequired !== false && <span className="text-red-600"> *</span>}
                      </div>
                      {item.description && (
                        <div className="text-xs text-muted-foreground">{item.description}</div>
                      )}
                    </div>
                  </div>
                  <Textarea
                    value={response?.notes || ''}
                    onChange={(e) => setChecklistResponse(item, index, { notes: e.target.value })}
                    placeholder="Notes (optional)"
                    rows={2}
                  />
                  {item.requiresDocument && (
                    <div className="space-y-2 rounded-md border bg-muted/20 p-3">
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div>
                          <div className="flex items-center gap-2 text-sm font-medium">
                            <FileText className="h-4 w-4 text-blue-600" />
                            {item.documentName || 'Supporting document'}
                            {(item.isRequired !== false || response?.isSatisfied === true) && (
                              <span className="text-red-600">*</span>
                            )}
                          </div>
                          {item.documentType && (
                            <div className="text-xs text-muted-foreground">{item.documentType}</div>
                          )}
                        </div>
                        {itemAttachments.length > 0 && (
                          <span className="text-xs font-medium text-green-700">
                            {itemAttachments.length} attached
                          </span>
                        )}
                      </div>
                      <div className="relative">
                        <Input
                          type="file"
                          accept=".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt"
                          aria-label={`Upload ${item.documentName || item.name}`}
                          disabled={isLoading || isUploading || !stepInstanceId}
                          onChange={(event) => {
                            const file = event.target.files?.[0];
                            void uploadChecklistDocument(item, index, file);
                            event.currentTarget.value = '';
                          }}
                        />
                        {isUploading && (
                          <div className="pointer-events-none absolute inset-y-0 right-3 flex items-center">
                            <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                          </div>
                        )}
                      </div>
                      {itemAttachments.length > 0 ? (
                        <div className="space-y-1">
                          {itemAttachments.map((attachment) => (
                            <div key={attachment.id} className="flex items-center gap-2 text-xs text-muted-foreground">
                              <CheckCircle className="h-3.5 w-3.5 text-green-600" />
                              <span className="font-medium text-foreground">{attachment.fileName}</span>
                              {attachment.uploadedByName && <span>by {attachment.uploadedByName}</span>}
                            </div>
                          ))}
                        </div>
                      ) : (
                        <div className="flex items-center gap-2 text-xs text-muted-foreground">
                          <Upload className="h-3.5 w-3.5" />
                          Attach evidence before approving this item.
                        </div>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      )}

      {!isReject && signaturePolicy?.isRequired && (
        <div className="space-y-3 rounded-md border p-3">
          <div className="text-sm font-medium">Electronic signature</div>
          <label className="flex items-start gap-2 text-sm"><Checkbox checked={signatureAccepted} onCheckedChange={checked => setSignatureAccepted(checked === true)} />
            <span>{signaturePolicy.attestationText}</span></label>
          {signaturePolicy.method === WorkflowSignatureMethod.DigitalCertificate && (
            <div className="space-y-2"><Label>Signing certificate</Label><Input type="file" accept=".cer,.crt" onChange={event => {
              const file = event.target.files?.[0]; if (!file) return; const reader = new FileReader();
              reader.onload = () => setCertificateBase64(String(reader.result || '').split(',').pop() || ''); reader.readAsDataURL(file);
            }} /></div>
          )}
          {signaturePolicy.method === WorkflowSignatureMethod.ExternalProvider && (
            <div className="space-y-2"><Label>External signature reference</Label><Input value={externalReference} onChange={event => setExternalReference(event.target.value)} /></div>
          )}
        </div>
      )}

      <div className="space-y-2">
        <Label htmlFor="workflow-approval-comments">
          {isReject ? 'Comment *' : 'Comment (Optional)'}
        </Label>
        <Textarea
          id="workflow-approval-comments"
          value={comments}
          onChange={(e) => setComments(e.target.value)}
          placeholder={
            isReject
              ? 'Explain why you are rejecting this...'
              : 'Add any comments (optional)...'
          }
          rows={4}
        />
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          {isReject ? (
            <>
              <XCircle className="h-4 w-4 text-red-600" />
              Rejection requires a comment.
            </>
          ) : (
            <>
              <CheckCircle className="h-4 w-4 text-green-600" />
              Approval comment is optional.
            </>
          )}
        </div>
      </div>
    </ConfirmationDialog>
  );
}

function getChecklistKey(item: WorkflowQualityCheckDto, index: number) {
  return item.id || item.name || `check-${index + 1}`;
}

function getChecklistAttachments(
  item: WorkflowQualityCheckDto,
  index: number,
  attachments: WorkflowTaskAttachmentDto[]
) {
  const key = getChecklistKey(item, index).trim().toLowerCase();
  return attachments.filter((attachment) =>
    (attachment.checklistItemId || attachment.requirementKey || '').trim().toLowerCase() === key
  );
}
