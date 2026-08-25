'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { CheckCircle2, Save, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useToast } from '@/hooks/use-toast';
import { formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { appraisalConversationService } from '@/services/hr/conversations.service';
import { CONVERSATION_TYPE_OPTIONS, type ConversationType } from '@/types/hr/conversations';

/** `2026-08-08T14:30:00Z` → `2026-08-08T14:30`, which is what a datetime-local input wants. */
function toLocalInput(value?: string | null): string {
  if (!value) return '';
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}T${pad(
    parsed.getHours(),
  )}:${pad(parsed.getMinutes())}`;
}

/**
 * One appraisal conversation: the agenda before it, the notes and takeaways after.
 *
 * **Completing is a one-way door.** It stamps the held date and notifies the employee that the
 * notes are up, and a completed conversation can no longer be edited — which is the point, since
 * the notes are then the record of what was said.
 */
export default function ConversationDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState({
    type: 'KickOff' as ConversationType,
    scheduledDate: '',
    agenda: '',
    postMeetingNotes: '',
    keyTakeaways: '',
  });

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'conversation', id],
    queryFn: () => appraisalConversationService.getById(id),
    enabled: !!id,
    retry: false,
  });

  useEffect(() => {
    if (!data) return;
    setForm({
      type: data.type,
      scheduledDate: toLocalInput(data.scheduledDate),
      agenda: data.agenda ?? '',
      postMeetingNotes: data.postMeetingNotes ?? '',
      keyTakeaways: data.keyTakeaways ?? '',
    });
  }, [data]);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'conversation', id] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'conversations'] });
  };

  const fail = (title: string) => (e: Error) =>
    toast({ title, description: e.message, variant: 'destructive' });

  const save = useMutation({
    mutationFn: () => {
      if (!data) throw new Error('The conversation is still loading.');
      return appraisalConversationService.update(id, {
        id,
        appraisalId: data.appraisalId,
        scheduledById: data.scheduledById ?? null,
        conductedById: data.conductedById ?? null,
        type: form.type,
        scheduledDate: form.scheduledDate ? new Date(form.scheduledDate).toISOString() : null,
        agenda: form.agenda.trim() || null,
        postMeetingNotes: form.postMeetingNotes.trim() || null,
        keyTakeaways: form.keyTakeaways.trim() || null,
        // Both ignored by the server on this path — completing is its own action.
        isCompleted: false,
        reviewEventId: data.reviewEventId ?? null,
      });
    },
    onSuccess: () => {
      toast({ title: 'Saved' });
      refresh();
    },
    onError: fail('Could not save'),
  });

  const complete = useMutation({
    mutationFn: () =>
      appraisalConversationService.complete(id, {
        postMeetingNotes: form.postMeetingNotes.trim() || null,
        keyTakeaways: form.keyTakeaways.trim() || null,
      }),
    onSuccess: () => {
      toast({
        title: 'Conversation recorded',
        description: 'The employee has been notified that the notes are up.',
      });
      refresh();
    },
    onError: fail('Could not complete'),
  });

  if (isLoading) {
    return (
      <div className="space-y-6 p-6">
        <Skeleton className="h-10 w-1/3" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Conversation" backHref="/hr/performance/conversations" />
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={TriangleAlert}
              title="Could not load this conversation"
              description={
                (error as Error)?.message ??
                'It may have been removed, or it may be about someone whose appraisal you cannot see.'
              }
            />
          </CardContent>
        </Card>
      </div>
    );
  }

  const done = data.isCompleted;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={humanizeEnum(data.type)}
        description={
          data.appraisalNumber
            ? `Appraisal ${data.appraisalNumber}`
            : 'Scheduled against an appraisal'
        }
        backHref="/hr/performance/conversations"
        actions={
          !done ? (
            <div className="flex items-center gap-2">
              <Button variant="outline" onClick={() => save.mutate()} disabled={save.isPending}>
                <Save className="mr-2 h-4 w-4" />
                Save
              </Button>
              <Button onClick={() => complete.mutate()} disabled={complete.isPending}>
                <CheckCircle2 className="mr-2 h-4 w-4" />
                {complete.isPending ? 'Recording…' : 'Mark held'}
              </Button>
            </div>
          ) : undefined
        }
      />

      <div className="flex flex-wrap items-center gap-3">
        <StatusBadge status={done ? 'Completed' : 'Scheduled'} />
        <span className="text-sm text-muted-foreground">
          {done
            ? `Held ${formatDateTime(data.heldDate)}`
            : `Scheduled for ${formatDateTime(data.scheduledDate)}`}
        </span>
        {data.scheduledByName && (
          <span className="text-sm text-muted-foreground">
            Scheduled by {data.scheduledByName}
          </span>
        )}
        {data.appraisalId && (
          <Button variant="link" size="sm" className="h-auto p-0" asChild>
            {/* The subject's own view moved to the portal; this desk screen opens the manager's. */}
            <Link href={`/hr/performance/team-appraisals/${data.appraisalId}`}>Open the appraisal</Link>
          </Button>
        )}
      </div>

      {done && (
        <Alert>
          <CheckCircle2 className="h-4 w-4" />
          <AlertTitle>Held and recorded</AlertTitle>
          <AlertDescription>
            A completed conversation is read-only: the notes are the record of what was said.
            Schedule another one if there is more to discuss.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Before the meeting</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="cv-type">Type</Label>
              <Select
                value={form.type}
                onValueChange={(v) => setForm((p) => ({ ...p, type: v as ConversationType }))}
                disabled={done}
              >
                <SelectTrigger id="cv-type">
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
              <Label htmlFor="cv-date">Scheduled for</Label>
              <Input
                id="cv-date"
                type="datetime-local"
                value={form.scheduledDate}
                onChange={(e) => setForm((p) => ({ ...p, scheduledDate: e.target.value }))}
                disabled={done}
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="cv-agenda">Agenda</Label>
            <Textarea
              id="cv-agenda"
              rows={4}
              maxLength={2000}
              value={form.agenda}
              onChange={(e) => setForm((p) => ({ ...p, agenda: e.target.value }))}
              disabled={done}
              placeholder="What will be covered, so nobody walks in cold."
            />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">After the meeting</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="cv-notes">Notes</Label>
            <Textarea
              id="cv-notes"
              rows={5}
              maxLength={4000}
              value={form.postMeetingNotes}
              onChange={(e) => setForm((p) => ({ ...p, postMeetingNotes: e.target.value }))}
              disabled={done}
              placeholder="What was actually discussed."
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="cv-takeaways">Key takeaways</Label>
            <Textarea
              id="cv-takeaways"
              rows={3}
              maxLength={2000}
              value={form.keyTakeaways}
              onChange={(e) => setForm((p) => ({ ...p, keyTakeaways: e.target.value }))}
              disabled={done}
              placeholder="The two or three things to act on. These go into the notification the employee gets."
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
