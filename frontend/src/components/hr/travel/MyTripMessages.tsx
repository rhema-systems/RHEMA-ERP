'use client';

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, MessageSquare, Reply, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { travelService } from '@/services/hr/travel.service';
import type { StaffTravelRequest, StaffTravelRequestComment } from '@/types/hr/travel';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const MAX = 2000;

/**
 * The traveller's conversation with the travel desk about their trip (travel final closure, lane 7, slice 7c2 — E7, D-41).
 *
 * What the desk shared arrives on the request's own read — internal notes never reach this browser (A6). The traveller
 * answers a note (a Response, threaded under it) or asks a question of their own (a Query); both are theirs and visible to
 * them. There is no edit or delete: what was said to the desk stands, as the desk's record of the trip.
 *
 * The read carries every comment flat (and replies again under their parent), so the threads are built here from
 * `parentCommentId`, each reply under the note that started its thread.
 */
export function MyTripMessages({ request }: { request: StaffTravelRequest }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { user } = useAuth();
  const [draft, setDraft] = useState('');
  const [replyTo, setReplyTo] = useState<string | null>(null);
  const [replyText, setReplyText] = useState('');

  // Second guard: the server already sends only what the desk shared.
  const comments = (request.comments ?? []).filter((c) => c.isVisibleToTraveller);
  const byId = new Map(comments.map((c) => [c.id, c]));
  const rootOf = (c: StaffTravelRequestComment) => {
    let at = c;
    for (let hops = 0; at.parentCommentId && byId.has(at.parentCommentId) && hops < 50; hops += 1) {
      at = byId.get(at.parentCommentId)!;
    }
    return at;
  };
  const threads = new Map<string, StaffTravelRequestComment[]>();
  for (const c of comments.slice().sort((a, b) => a.createdAt.localeCompare(b.createdAt))) {
    const root = rootOf(c);
    threads.set(root.id, [...(threads.get(root.id) ?? []), c]);
  }
  const ordered = [...threads.values()].sort((a, b) =>
    b[b.length - 1].createdAt.localeCompare(a[a.length - 1].createdAt));

  const send = useMutation({
    mutationFn: (p: { body: string; parentCommentId?: string | null }) => travelService.addMyComment(request.id, p),
    onSuccess: async (_, p) => {
      await queryClient.invalidateQueries({ queryKey: ['my-travel-request', request.id] });
      if (p.parentCommentId) { setReplyTo(null); setReplyText(''); } else setDraft('');
      toast({ title: 'Sent to the travel desk' });
    },
    onError: (e: Error) => toast({ variant: 'destructive', title: 'Could not send it', description: e.message }),
  });

  const who = (c: StaffTravelRequestComment) =>
    (user?.employeeId && c.authorId === user.employeeId ? 'You' : c.authorName || 'Travel desk');

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Write to the travel desk</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <Textarea value={draft} onChange={(e) => setDraft(e.target.value.slice(0, MAX))} rows={3}
            placeholder="A question about this trip — the travel desk sees it on your request." />
          <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-muted-foreground">
              Messages are kept with the trip and cannot be changed once sent.
            </span>
            <Button size="sm" disabled={!draft.trim() || send.isPending}
              onClick={() => send.mutate({ body: draft.trim() })}>
              {send.isPending && !replyTo ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Send className="mr-2 h-4 w-4" />}
              Send
            </Button>
          </div>
        </CardContent>
      </Card>

      {ordered.length === 0 ? (
        <p className="flex items-center gap-2 text-sm text-muted-foreground">
          <MessageSquare className="h-4 w-4" /> No messages on this trip yet.
        </p>
      ) : (
        ordered.map((thread) => {
          const root = thread[0];
          return (
            <Card key={root.id}>
              <CardContent className="space-y-3 pt-4">
                {thread.map((c) => (
                  <div key={c.id} className={c.parentCommentId ? 'ml-6 border-l pl-3' : ''}>
                    <div className="flex items-center justify-between gap-3">
                      <p className="text-sm font-medium">{who(c)}</p>
                      <p className="text-xs text-muted-foreground">{fmtDateTime(c.createdAt)}</p>
                    </div>
                    <p className="mt-1 whitespace-pre-wrap text-sm">{c.body}</p>
                  </div>
                ))}
                {replyTo === root.id ? (
                  <div className="ml-6 space-y-2">
                    <Textarea value={replyText} onChange={(e) => setReplyText(e.target.value.slice(0, MAX))} rows={2}
                      placeholder="Your reply" autoFocus />
                    <div className="flex justify-end gap-2">
                      <Button size="sm" variant="outline" onClick={() => { setReplyTo(null); setReplyText(''); }}>Cancel</Button>
                      <Button size="sm" disabled={!replyText.trim() || send.isPending}
                        onClick={() => send.mutate({ body: replyText.trim(), parentCommentId: thread[thread.length - 1].id })}>
                        {send.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Reply className="mr-2 h-4 w-4" />}
                        Reply
                      </Button>
                    </div>
                  </div>
                ) : (
                  <Button size="sm" variant="ghost" onClick={() => { setReplyTo(root.id); setReplyText(''); }}>
                    <Reply className="mr-2 h-4 w-4" /> Reply
                  </Button>
                )}
              </CardContent>
            </Card>
          );
        })
      )}
    </div>
  );
}
