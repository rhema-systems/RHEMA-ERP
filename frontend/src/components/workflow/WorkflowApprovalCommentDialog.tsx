'use client';

import * as React from 'react';
import { CheckCircle, XCircle } from 'lucide-react';

import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';

export type WorkflowApprovalDialogMode = 'approve' | 'reject';

export interface WorkflowApprovalCommentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: WorkflowApprovalDialogMode;
  title: string;
  description?: React.ReactNode;
  entitySummary?: React.ReactNode;
  onConfirm: (comments: string) => void | boolean | Promise<void | boolean>;
  isLoading?: boolean;
}

export function WorkflowApprovalCommentDialog({
  open,
  onOpenChange,
  mode,
  title,
  description,
  entitySummary,
  onConfirm,
  isLoading = false,
}: WorkflowApprovalCommentDialogProps) {
  const [comments, setComments] = React.useState('');

  React.useEffect(() => {
    if (!open) setComments('');
  }, [open]);

  const isReject = mode === 'reject';
  const commentsRequired = isReject;
  const confirmDisabled = commentsRequired && !comments.trim();

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
      onConfirm={() => onConfirm(comments.trim())}
      isLoading={isLoading}
      confirmDisabled={confirmDisabled}
    >
      {entitySummary && <div className="space-y-2">{entitySummary}</div>}

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
