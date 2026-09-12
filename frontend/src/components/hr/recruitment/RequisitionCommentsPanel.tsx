'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, MessageSquare, Reply, Send } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime } from '@/lib/hr/attendance-format';
import { staffRequisitionService } from '@/services/hr/recruitment.service';
import type { RequisitionComment } from '@/types/hr/recruitment';

function CommentRow({
  comment,
  onReply,
  depth = 0,
}: {
  comment: RequisitionComment;
  onReply: (parentId: string) => void;
  depth?: number;
}) {
  return (
    <div className={depth > 0 ? 'ml-8 border-l pl-4' : ''}>
      <div className="py-3">
        <div className="flex items-baseline justify-between gap-2">
          <span className="text-sm font-medium">{comment.authorName || 'Unknown'}</span>
          <span className="text-xs text-muted-foreground">{formatDateTime(comment.postedDate)}</span>
        </div>
        <p className="mt-1 whitespace-pre-wrap text-sm">{comment.body}</p>
        {/* Only one level of reply is offered. The API models arbitrary nesting, but a
            requisition discussion that goes deeper than a reply is a conversation that should
            be happening somewhere else. */}
        {depth === 0 && (
          <Button
            variant="ghost"
            size="sm"
            className="mt-1 h-7 px-2 text-xs"
            onClick={() => onReply(comment.id)}
          >
            <Reply className="mr-1 h-3 w-3" /> Reply
          </Button>
        )}
      </div>
      {(comment.replies ?? []).map((r) => (
        <CommentRow key={r.id} comment={r} onReply={onReply} depth={depth + 1} />
      ))}
    </div>
  );
}

/** The discussion on a requisition — questions from an approver, answers from the requester. */
export function RequisitionCommentsPanel({ requisitionId }: { requisitionId: string }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [body, setBody] = useState('');
  const [replyTo, setReplyTo] = useState<string | null>(null);

  const comments = useQuery({
    queryKey: ['hr', 'requisition-comments', requisitionId],
    queryFn: () => staffRequisitionService.getComments(requisitionId),
    enabled: !!requisitionId,
  });

  const post = useMutation({
    mutationFn: () => staffRequisitionService.addComment(requisitionId, body.trim(), replyTo),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['hr', 'requisition-comments', requisitionId] });
      setBody('');
      setReplyTo(null);
    },
    onError: (e: any) =>
      toast({ title: 'Could not post', description: e?.message, variant: 'destructive' }),
  });

  const rows = comments.data ?? [];

  return (
    <Card>
      <CardHeader className="pb-3">
        <CardTitle className="text-base">Discussion</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {comments.isLoading ? (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            icon={MessageSquare}
            title="No comments"
            description="Ask a question or leave context for whoever reviews this."
          />
        ) : (
          <div className="divide-y">
            {rows.map((c) => (
              <CommentRow key={c.id} comment={c} onReply={setReplyTo} />
            ))}
          </div>
        )}

        <div className="space-y-2 border-t pt-4">
          {replyTo && (
            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <span>Replying to a comment</span>
              <Button variant="ghost" size="sm" className="h-6 px-2" onClick={() => setReplyTo(null)}>
                Cancel reply
              </Button>
            </div>
          )}
          <Textarea
            rows={3}
            value={body}
            onChange={(e) => setBody(e.target.value)}
            placeholder="Add a comment…"
          />
          <div className="flex justify-end">
            <Button size="sm" onClick={() => post.mutate()} disabled={!body.trim() || post.isPending}>
              <Send className="mr-2 h-4 w-4" />
              {post.isPending ? 'Posting…' : 'Post'}
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
