'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Megaphone } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useToast } from '@/components/ui/use-toast';
import { businessClosureService } from '@/services/hr/company-schedule.service';

/** The preview's query key, shared with the offer on the closures screen so both read one fetch. */
export const closureAnnouncementKey = (closureId: string) =>
  ['hr', 'company-schedule', 'closures', closureId, 'announcement'] as const;

/**
 * Announcing a business closure to the staff it covers (company-schedule final closure, L1-1).
 *
 * ⚠ **HR's click, never a save's side effect.** An announcement reaches every covered person at
 * once and cannot be unsent, and a closure is often typed, corrected, then confirmed. So the screen
 * offers this dialog; nothing publishes until its button is pressed.
 *
 * The audience is the closure's own scope, worked out by the server — the same people leave and the
 * diaries treat as covered — and the words are the server's, shown here exactly as they will go out.
 * The body is plain text and is rendered as text, never markup (it reaches every covered employee).
 */
export function ClosureAnnounceDialog({
  closureId,
  closureTitle,
  open,
  onOpenChange,
  onAnnounced,
}: {
  closureId: string | null;
  closureTitle?: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onAnnounced?: () => void;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: preview, isLoading, isError, error } = useQuery({
    queryKey: closureAnnouncementKey(closureId ?? ''),
    queryFn: () => businessClosureService.getAnnouncementPreview(closureId ?? ''),
    enabled: open && !!closureId,
  });

  const announce = useMutation({
    mutationFn: () => {
      if (!closureId) throw new Error('No closure is selected.');
      return businessClosureService.announce(closureId);
    },
    onSuccess: async (published) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'announcements'] });
      toast({
        title: 'Announced',
        description: `"${published.title}" is published to ${preview?.staffCovered ?? 'the'} staff it covers. It is listed under HR → Announcements, where it can be archived.`,
      });
      onAnnounced?.();
      onOpenChange(false);
    },
    onError: (e: any) =>
      toast({
        title: 'Not announced',
        description: e?.message || 'The announcement could not be published.',
        variant: 'destructive',
      }),
  });

  const reach = preview?.staffCovered ?? 0;

  return (
    <Dialog open={open} onOpenChange={(next) => !announce.isPending && onOpenChange(next)}>
      <DialogContent className="sm:max-w-[600px]">
        <DialogHeader>
          <DialogTitle>Announce the closure{closureTitle ? ` — ${closureTitle}` : ''}</DialogTitle>
          <DialogDescription>
            Publishes an announcement to the staff the closure covers. They see it in their announcements and
            notifications at once, and it cannot be unsent — archive it later if it is wrong.
          </DialogDescription>
        </DialogHeader>

        {isLoading ? (
          <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Counting the staff it covers…
          </div>
        ) : isError ? (
          <p className="py-4 text-sm text-red-600">
            {(error as any)?.message || 'The preview could not be loaded.'}
          </p>
        ) : preview ? (
          <div className="space-y-4">
            {preview.canAnnounce ? (
              <p className="text-sm">
                It goes to <span className="font-semibold">{reach}</span> active staff
                {reach === 1 ? ' member' : ''}.
              </p>
            ) : (
              <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
                No active staff are covered by this closure, so there is nobody to tell. Check its site or unit.
              </p>
            )}
            <div className="space-y-2 rounded-md border bg-muted/40 p-4">
              <p className="text-xs uppercase tracking-wide text-muted-foreground">What they will read</p>
              <p className="font-semibold">{preview.title}</p>
              <p className="text-sm">{preview.summary}</p>
              <p className="whitespace-pre-line text-sm text-muted-foreground">{preview.body}</p>
            </div>
          </div>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={announce.isPending}>
            Not now
          </Button>
          <Button
            onClick={() => announce.mutate()}
            disabled={!preview?.canAnnounce || announce.isPending}
          >
            {announce.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Megaphone className="mr-2 h-4 w-4" />
            )}
            {preview?.canAnnounce ? `Announce to ${reach} staff` : 'Announce'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
