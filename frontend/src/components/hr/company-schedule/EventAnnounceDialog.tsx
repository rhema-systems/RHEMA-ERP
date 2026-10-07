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
import { companyEventService } from '@/services/hr/company-schedule.service';

/**
 * Announcing an event on the intranet to its audience (company-schedule final closure, lane 2c).
 *
 * ⚠ **HR's click, never a save's side effect** — the rule closures follow (L1-1). "Show on intranet"
 * marks the event as one to announce; this dialog sends it, once the event is approved and while it is
 * ahead. The audience is the event's own (its scope and visibility), worked out by the server, and the
 * words are the server's, shown exactly as they will go out — as text, never markup.
 */
export function EventAnnounceDialog({
  eventId,
  eventName,
  open,
  onOpenChange,
}: {
  eventId: string;
  eventName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data: preview, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'company-schedule', 'events', eventId, 'announcement'],
    queryFn: () => companyEventService.getAnnouncementPreview(eventId),
    enabled: open,
  });

  const announce = useMutation({
    mutationFn: () => companyEventService.announce(eventId),
    onSuccess: async (published) => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'announcements'] });
      toast({
        title: 'Announced',
        description: `"${published.title}" is published to the ${preview?.staffReached ?? ''} staff the event is for. It is listed under HR → Announcements, where it can be archived.`,
      });
      onOpenChange(false);
    },
    onError: (e: any) =>
      toast({ title: 'Not announced', description: e?.message || 'The announcement could not be published.', variant: 'destructive' }),
  });

  return (
    <Dialog open={open} onOpenChange={(next) => !announce.isPending && onOpenChange(next)}>
      <DialogContent className="sm:max-w-[600px]">
        <DialogHeader>
          <DialogTitle>Announce on the intranet — {eventName}</DialogTitle>
          <DialogDescription>
            Publishes an announcement to everyone the event is for. They see it in their announcements and
            notifications at once, and it cannot be unsent — archive it later if it is wrong.
          </DialogDescription>
        </DialogHeader>

        {isLoading ? (
          <div className="flex items-center gap-2 py-6 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Counting who it reaches…
          </div>
        ) : isError ? (
          <p className="py-4 text-sm text-red-600">{(error as any)?.message || 'The preview could not be loaded.'}</p>
        ) : preview ? (
          <div className="space-y-4">
            {preview.canAnnounce ? (
              <p className="text-sm">
                It goes to <span className="font-semibold">{preview.staffReached}</span> active staff.
              </p>
            ) : (
              <p className="rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
                {preview.reason}
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
          <Button onClick={() => announce.mutate()} disabled={!preview?.canAnnounce || announce.isPending}>
            {announce.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Megaphone className="mr-2 h-4 w-4" />}
            {preview?.canAnnounce ? `Announce to ${preview.staffReached} staff` : 'Announce'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
