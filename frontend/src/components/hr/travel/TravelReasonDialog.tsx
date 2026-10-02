'use client';

import { useState } from 'react';
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

/**
 * A lifecycle action that asks for a reason before it runs — return for revision, request a change,
 * recall, submit after departure (travel final closure, lane 1). One dialog so each action reads the
 * same way: what will happen, the reason box, a confirm that stays disabled until a required reason
 * is typed. The server's 1000-character limit is the box's.
 */
export function TravelReasonDialog({
  open,
  onOpenChange,
  title,
  description,
  label = 'Reason',
  placeholder,
  confirmLabel,
  optional = false,
  pending = false,
  destructive = false,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: React.ReactNode;
  label?: string;
  placeholder?: string;
  confirmLabel: string;
  /** True where the server takes the action without a reason (recall). */
  optional?: boolean;
  pending?: boolean;
  destructive?: boolean;
  /** Resolves when the action succeeded; the dialog then closes and clears itself. */
  onConfirm: (reason: string) => Promise<unknown>;
}) {
  const [reason, setReason] = useState('');
  const close = (next: boolean) => {
    if (!next) setReason('');
    onOpenChange(next);
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <div className="space-y-2">
          <Label htmlFor="travel-reason">
            {label}
            {optional && <span className="ml-1 text-muted-foreground">(optional)</span>}
          </Label>
          <Textarea
            id="travel-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={3}
            maxLength={1000}
            placeholder={placeholder}
          />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => close(false)}>
            Not now
          </Button>
          <Button
            variant={destructive ? 'destructive' : 'default'}
            disabled={pending || (!optional && !reason.trim())}
            onClick={async () => {
              try {
                await onConfirm(reason.trim());
                close(false);
              } catch {
                // The caller's mutation reports the error; the dialog stays open with the text kept.
              }
            }}
          >
            {confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
