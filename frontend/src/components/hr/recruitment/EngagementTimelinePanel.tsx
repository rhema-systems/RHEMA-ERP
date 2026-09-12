'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Lock, MessageSquarePlus, Trash2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import { Textarea } from '@/components/ui/textarea';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { formatDate, humanizeEnum } from '@/lib/hr/attendance-format';
import { talentPoolService } from '@/services/hr/talent-pool.service';
import { ENGAGEMENT_EVENT_TYPES, type EngagementEventType } from '@/types/hr/talent-pool';

/**
 * The contact log for a pooled candidate — every touch keeps them warm and moves the pool's
 * dormancy clock. Logging an event also stamps the candidate's LastEngagedDate server-side.
 * Deleting one is Admin-tier: it rewrites a history the dormancy sweep depends on.
 */
export function EngagementTimelinePanel({
  candidateId,
  canManage,
  canAdmin,
}: {
  candidateId: string;
  canManage: boolean;
  canAdmin: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [open, setOpen] = useState(false);
  const [eventType, setEventType] = useState<EngagementEventType>('PhoneCall');
  const [eventDate, setEventDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [subject, setSubject] = useState('');
  const [notes, setNotes] = useState('');
  const [isInternal, setIsInternal] = useState(false);

  const events = useQuery({
    queryKey: ['hr', 'candidate-engagement', candidateId],
    queryFn: () => talentPoolService.getEvents(candidateId),
    enabled: !!candidateId,
  });

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'candidate-engagement', candidateId] });
    await queryClient.invalidateQueries({ queryKey: ['hr', 'talent-pool-candidate', candidateId] });
  };

  const log = useMutation({
    mutationFn: () =>
      talentPoolService.logEvent(candidateId, {
        eventType,
        eventDate,
        subject: subject.trim() || null,
        notes: notes.trim() || null,
        isInternal,
      }),
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setSubject('');
      setNotes('');
      setIsInternal(false);
      toast({ title: 'Engagement logged' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not log it', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (eventId: string) => talentPoolService.deleteEvent(eventId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Event deleted' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not delete it', description: e?.message, variant: 'destructive' }),
  });

  return (
    <Card>
      <CardHeader className="flex flex-row items-start justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Engagement</CardTitle>
          <CardDescription>Every contact with this candidate, newest first.</CardDescription>
        </div>
        {canManage && (
          <Button size="sm" onClick={() => setOpen(true)}>
            <MessageSquarePlus className="mr-1.5 h-4 w-4" />
            Log contact
          </Button>
        )}
      </CardHeader>
      <CardContent>
        {events.isLoading ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : (events.data ?? []).length === 0 ? (
          <EmptyState
            title="No engagement yet"
            description="A candidate nobody contacts goes dormant — the pool report counts on this log."
          />
        ) : (
          <ol className="space-y-4">
            {(events.data ?? []).map((e) => (
              <li key={e.id} className="flex gap-3 border-l-2 border-muted pl-4">
                <div className="flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="outline">{humanizeEnum(e.eventType)}</Badge>
                    <span className="text-sm font-medium">{e.subject || humanizeEnum(e.eventType)}</span>
                    {e.isInternal && (
                      <span className="flex items-center gap-1 text-xs text-muted-foreground">
                        <Lock className="h-3 w-3" />
                        Internal note
                      </span>
                    )}
                  </div>
                  <div className="mt-0.5 text-xs text-muted-foreground">
                    {formatDate(e.eventDate)}
                    {e.recordedByName ? ` · by ${e.recordedByName}` : ''}
                  </div>
                  {e.notes && <p className="mt-1 whitespace-pre-wrap text-sm">{e.notes}</p>}
                </div>
                {canAdmin && (
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label="Delete this event"
                    onClick={() => remove.mutate(e.id)}
                  >
                    <Trash2 className="h-4 w-4 text-destructive" />
                  </Button>
                )}
              </li>
            ))}
          </ol>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Log an engagement</DialogTitle>
            <DialogDescription>
              Recorded under your name and moves the candidate&apos;s last-engaged date.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label>Type</Label>
                <Select value={eventType} onValueChange={(v) => setEventType(v as EngagementEventType)}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {ENGAGEMENT_EVENT_TYPES.map((t) => (
                      <SelectItem key={t} value={t}>
                        {humanizeEnum(t)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="event-date">Date</Label>
                <Input
                  id="event-date"
                  type="date"
                  value={eventDate}
                  onChange={(e) => setEventDate(e.target.value)}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="event-subject">Subject</Label>
              <Input
                id="event-subject"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                maxLength={300}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="event-notes">Notes</Label>
              <Textarea
                id="event-notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                maxLength={2000}
              />
            </div>
            <label className="flex items-center gap-2 text-sm">
              <Checkbox checked={isInternal} onCheckedChange={(v) => setIsInternal(v === true)} />
              Internal note (not a contact with the candidate)
            </label>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button disabled={!eventDate || log.isPending} onClick={() => log.mutate()}>
              {log.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Log it
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
