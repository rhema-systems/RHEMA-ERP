'use client';

import * as React from 'react';
import { CheckCircle, XCircle } from 'lucide-react';

import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import type { WorkflowApprovalChecklistResponseDto, WorkflowQualityCheckDto } from '@/types/workflow';

export type WorkflowApprovalDialogMode = 'approve' | 'reject';

export interface WorkflowApprovalCommentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: WorkflowApprovalDialogMode;
  title: string;
  description?: React.ReactNode;
  entitySummary?: React.ReactNode;
  checklistItems?: WorkflowQualityCheckDto[];
  onConfirm: (
    comments: string,
    checklistResponses?: WorkflowApprovalChecklistResponseDto[]
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
  onConfirm,
  isLoading = false,
}: WorkflowApprovalCommentDialogProps) {
  const [comments, setComments] = React.useState('');
  const [checklistState, setChecklistState] = React.useState<Record<string, WorkflowApprovalChecklistResponseDto>>({});

  React.useEffect(() => {
    if (!open) {
      setComments('');
      setChecklistState({});
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
  }, [open, checklistItems]);

  const isReject = mode === 'reject';
  const commentsRequired = isReject;
  const approvalChecklistItems = isReject ? [] : checklistItems;
  const requiredChecklistIncomplete = approvalChecklistItems.some((item, index) => {
    if (item.isRequired === false) return false;
    const response = checklistState[getChecklistKey(item, index)];
    return !response?.isSatisfied;
  });
  const confirmDisabled = (commentsRequired && !comments.trim()) || requiredChecklistIncomplete;

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
      };
    });
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
      onConfirm={() => onConfirm(comments.trim(), buildChecklistResponses())}
      isLoading={isLoading}
      confirmDisabled={confirmDisabled}
      maxWidth="560px"
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
                </div>
              );
            })}
          </div>
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
