'use client';

import * as React from 'react';
import { AlertTriangle } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';

interface WorkflowReasonDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: React.ReactNode;
  reasonLabel?: string;
  reasonPlaceholder?: string;
  confirmText?: string;
  cancelText?: string;
  requireReason?: boolean;
  variant?: 'default' | 'destructive';
  isLoading?: boolean;
  onConfirm: (reason: string) => void | Promise<void>;
}

export function WorkflowReasonDialog({
  open,
  onOpenChange,
  title,
  description,
  reasonLabel = 'Reason',
  reasonPlaceholder = 'Enter a clear audit reason',
  confirmText = 'Confirm',
  cancelText = 'Cancel',
  requireReason = true,
  variant = 'default',
  isLoading = false,
  onConfirm,
}: WorkflowReasonDialogProps) {
  const [reason, setReason] = React.useState('');
  const [touched, setTouched] = React.useState(false);
  const trimmedReason = reason.trim();
  const invalid = requireReason && touched && !trimmedReason;

  React.useEffect(() => {
    if (open) {
      setReason('');
      setTouched(false);
    }
  }, [open]);

  const handleConfirm = async () => {
    setTouched(true);
    if (requireReason && !trimmedReason) return;
    await onConfirm(trimmedReason);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {variant === 'destructive' ? <AlertTriangle className="h-5 w-5 text-destructive" /> : null}
            {title}
          </DialogTitle>
          {description ? (
            typeof description === 'string'
              ? <DialogDescription>{description}</DialogDescription>
              : <DialogDescription asChild><div>{description}</div></DialogDescription>
          ) : null}
        </DialogHeader>
        <div className="space-y-2">
          <Label>{reasonLabel}{requireReason ? '' : ' (optional)'}</Label>
          <Textarea
            value={reason}
            onChange={event => setReason(event.target.value)}
            onBlur={() => setTouched(true)}
            placeholder={reasonPlaceholder}
            rows={4}
          />
          {invalid ? <p className="text-sm text-destructive">This field is required.</p> : null}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isLoading}>{cancelText}</Button>
          <Button
            variant={variant === 'destructive' ? 'destructive' : 'default'}
            onClick={() => void handleConfirm()}
            disabled={isLoading || (requireReason && touched && !trimmedReason)}
          >
            {confirmText}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
