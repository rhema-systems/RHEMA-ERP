'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, MessageSquare, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { jobOfferService } from '@/services/hr/offers.service';

/**
 * Internal notes on an offer — an append-only log, unlike the candidate note panel. The controller
 * has no update or delete route for these, so a correction is a new note rather than an edit.
 */
export function OfferNotesPanel({ offerId }: { offerId: string }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [body, setBody] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'offer-notes', offerId],
    queryFn: () => jobOfferService.getNotes(offerId),
    enabled: !!offerId,
  });

  const add = useMutation({
    mutationFn: () => jobOfferService.addNote(offerId, body.trim()),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'offer-notes', offerId] });
      setOpen(false);
      setBody('');
      toast({ title: 'Note added' });
    },
    onError: (e: any) => toast({ title: 'Could not save', description: e?.message, variant: 'destructive' }),
  });

  const notes = data ?? [];

  return (
    <div className="space-y-4">
      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        </div>
      ) : notes.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={MessageSquare}
              title="No notes"
              description="A log of anything worth remembering about this offer — a call with the candidate, a change of mind."
            />
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {notes.map((n) => (
            <Card key={n.id}>
              <CardContent className="space-y-2 pt-5">
                <p className="whitespace-pre-wrap text-sm">{n.body}</p>
                <p className="text-xs text-muted-foreground">
                  {formatDateTime(n.createdAt)}
                  {n.authorName ? ` · ${n.authorName}` : ''}
                </p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      <Button variant="outline" onClick={() => setOpen(true)}>
        <Plus className="mr-2 h-4 w-4" />
        Add note
      </Button>

      <Dialog open={open} onOpenChange={(o) => (o ? setOpen(true) : (setOpen(false), setBody('')))}>
        <DialogContent className="sm:max-w-[560px]">
          <DialogHeader>
            <DialogTitle>Add note</DialogTitle>
            <DialogDescription>Notes cannot be edited or removed once saved.</DialogDescription>
          </DialogHeader>
          <Textarea value={body} onChange={(e) => setBody(e.target.value)} rows={6} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button disabled={!body.trim() || add.isPending} onClick={() => add.mutate()}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
