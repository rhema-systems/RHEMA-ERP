'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import Link from 'next/link';
import { CalendarPlus, MessagesSquare } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { appraisalConversationService } from '@/services/hr/conversations.service';
import { CONVERSATION_TYPE_OPTIONS, type ConversationType } from '@/types/hr/conversations';

interface ConversationsPanelProps {
  appraisalId: string;
  /** Managers and HR schedule; the appraisee reads. */
  canSchedule?: boolean;
}

/**
 * The conversations held against one appraisal, embedded on the appraisal screens.
 *
 * Scheduling lives here rather than on the conversations list because a conversation has no
 * meaning without an appraisal — the list is a worklist, this is where one is created. Whoever
 * schedules it is recorded as holding it, and the employee is notified.
 */
export function ConversationsPanel({ appraisalId, canSchedule = false }: ConversationsPanelProps) {
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({
    type: 'MidYear' as ConversationType,
    scheduledDate: '',
    agenda: '',
  });

  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'conversations', 'by-appraisal', appraisalId],
    queryFn: () => appraisalConversationService.getByAppraisal(appraisalId),
    enabled: !!appraisalId,
    retry: false,
  });

  const create = useMutation({
    mutationFn: () =>
      appraisalConversationService.create({
        appraisalId,
        type: form.type,
        scheduledDate: form.scheduledDate ? new Date(form.scheduledDate).toISOString() : null,
        agenda: form.agenda.trim() || null,
        // The server stamps the signed-in employee as the scheduler (closure D-74).
      }),
    onSuccess: () => {
      toast({ title: 'Conversation scheduled', description: 'The employee has been notified.' });
      setOpen(false);
      setForm({ type: 'MidYear', scheduledDate: '', agenda: '' });
      queryClient.invalidateQueries({ queryKey: ['hr', 'conversations'] });
    },
    onError: (e: Error) =>
      toast({ title: 'Could not schedule', description: e.message, variant: 'destructive' }),
  });

  const rows = data ?? [];
  const now = Date.now();

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="flex items-center gap-2 text-base">
          <MessagesSquare className="h-4 w-4" />
          Conversations
        </CardTitle>
        {canSchedule && (
          <Button size="sm" onClick={() => setOpen(true)}>
            <CalendarPlus className="mr-2 h-4 w-4" />
            Schedule
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {isError ? (
          <EmptyState
            title="Could not load conversations"
            description={(error as Error)?.message ?? 'Try again in a moment.'}
          />
        ) : isLoading ? (
          <div className="space-y-2 p-4">
            <Skeleton className="h-12 w-full" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            icon={MessagesSquare}
            title="No conversations scheduled"
            description={
              canSchedule
                ? 'Kick-off, mid-year and final review meetings are recorded here, with the notes that came out of them.'
                : 'Meetings your manager schedules against this appraisal appear here.'
            }
          />
        ) : (
          <div className="divide-y">
            {rows.map((row) => {
              const overdue =
                !row.isCompleted &&
                !!row.scheduledDate &&
                new Date(row.scheduledDate).getTime() < now;

              return (
                <div key={row.id} className="flex items-start justify-between gap-4 p-4">
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{humanizeEnum(row.type)}</span>
                      {row.isCompleted ? (
                        <Badge variant="default">Held {formatDate(row.heldDate)}</Badge>
                      ) : overdue ? (
                        <Badge variant="destructive">Overdue</Badge>
                      ) : (
                        <Badge variant="secondary">
                          {formatDateTime(row.scheduledDate)}
                        </Badge>
                      )}
                    </div>
                    {row.keyTakeaways ? (
                      <p className="max-w-2xl text-sm text-muted-foreground">
                        {row.keyTakeaways}
                      </p>
                    ) : row.agenda ? (
                      <p className="max-w-2xl text-sm text-muted-foreground">{row.agenda}</p>
                    ) : null}
                  </div>
                  <Button variant="ghost" size="sm" asChild>
                    <Link href={`/hr/performance/conversations/${row.id}`}>Open</Link>
                  </Button>
                </div>
              );
            })}
          </div>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Schedule a conversation</DialogTitle>
            <DialogDescription>
              You are recorded as holding it, and the employee is notified with the date.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="cvp-type">Type</Label>
                <Select
                  value={form.type}
                  onValueChange={(v) => setForm((p) => ({ ...p, type: v as ConversationType }))}
                >
                  <SelectTrigger id="cvp-type">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CONVERSATION_TYPE_OPTIONS.map((o) => (
                      <SelectItem key={o.value} value={o.value}>
                        {o.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="cvp-date">Scheduled for</Label>
                <Input
                  id="cvp-date"
                  type="datetime-local"
                  value={form.scheduledDate}
                  onChange={(e) => setForm((p) => ({ ...p, scheduledDate: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="cvp-agenda">Agenda</Label>
              <Textarea
                id="cvp-agenda"
                rows={4}
                maxLength={2000}
                value={form.agenda}
                onChange={(e) => setForm((p) => ({ ...p, agenda: e.target.value }))}
                placeholder="What will be covered, so nobody walks in cold."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button
              onClick={() => create.mutate()}
              disabled={!form.scheduledDate || create.isPending}
            >
              {create.isPending ? 'Scheduling…' : 'Schedule'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
