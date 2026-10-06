import * as React from 'react';
import { AlertTriangle } from 'lucide-react';

import { Button } from './button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from './dialog';
import { EnvironmentBadge } from '../environment/EnvironmentBadge';
import { useApplicationEnvironment } from '../../contexts/ApplicationEnvironmentContext';

export interface ConfirmationDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: React.ReactNode;
  confirmText?: string;
  cancelText?: string;
  variant?: 'default' | 'destructive';
  /**
   * Return `false` to keep the dialog open (e.g. validation or API error).
   * Any other return value will allow the dialog to close automatically.
   */
  onConfirm: () => void | boolean | Promise<void | boolean>;
  /** Called only by the Cancel button, not by Escape or the close icon. */
  onCancel?: () => void;
  isLoading?: boolean;
  confirmDisabled?: boolean;
  maxWidth?: string;
  children?: React.ReactNode;
}

export function ConfirmationDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmText = 'Confirm',
  cancelText = 'Cancel',
  variant = 'default',
  onConfirm,
  onCancel,
  isLoading = false,
  confirmDisabled = false,
  maxWidth = '425px',
  children,
}: ConfirmationDialogProps) {
  const { environment } = useApplicationEnvironment();
  const confirmInFlight = React.useRef(false);
  const [isConfirming, setIsConfirming] = React.useState(false);

  React.useEffect(() => {
    if (!open) {
      confirmInFlight.current = false;
      setIsConfirming(false);
    }
  }, [open]);

  const handleConfirm = async () => {
    if (confirmInFlight.current || isLoading) return;

    confirmInFlight.current = true;
    setIsConfirming(true);
    try {
      const result = await onConfirm();
      if (result !== false) {
        onOpenChange(false);
      }
    } finally {
      confirmInFlight.current = false;
      setIsConfirming(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-h-[calc(100dvh-2rem)] overflow-y-auto"
        style={{ maxWidth }}
      >
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {variant === 'destructive' && (
              <AlertTriangle className="h-5 w-5 text-red-500" />
            )}
            {title}
          </DialogTitle>
          {description &&
            (typeof description === 'string' ? (
              <DialogDescription>{description}</DialogDescription>
            ) : (
              <DialogDescription asChild>
                <div className="text-sm text-muted-foreground">
                  {description}
                </div>
              </DialogDescription>
            ))}
        </DialogHeader>
        {variant === 'destructive' && (
          <div className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-xs dark:border-neutral-700 dark:bg-neutral-900/70">
            <span className="font-medium text-slate-600 dark:text-neutral-300">Environment</span>
            <EnvironmentBadge compact />
            <span className="sr-only">{environment.message}</span>
          </div>
        )}
        {children && <div className="py-4">{children}</div>}
        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => {
              onCancel?.();
              onOpenChange(false);
            }}
            disabled={isLoading || isConfirming}
          >
            {cancelText}
          </Button>
          <Button
            variant={variant === 'destructive' ? 'destructive' : 'default'}
            onClick={handleConfirm}
            disabled={isLoading || isConfirming || confirmDisabled}
          >
            {isLoading || isConfirming ? 'Please wait...' : confirmText}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
