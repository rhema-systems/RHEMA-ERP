'use client';

import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Check, HelpCircle, Loader2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { companyCalendarService } from '@/services/hr/company-schedule.service';
import type { InvitationStatus, SeriesScope } from '@/types/hr/company-schedule';

const ANSWER_WORD: Record<string, string> = {
  Accepted: 'You accepted',
  Declined: 'You declined',
  Tentative: 'You said you may attend',
  Sent: 'You have not answered yet',
  NoResponse: 'You have not answered yet',
  NotSent: 'Your invitation has not been sent yet',
};

/** What the caller's own invitation says, in words. */
export const answerWords = (status?: string | null) => (status ? ANSWER_WORD[status] ?? status : '');

/**
 * The invitee's own answer (company-schedule lane 7, D-8) — Accept, Decline or "May attend", with an optional note, and on
 * a series this date, this and following dates, or every date (D-12). The server holds the window (the user's ruling):
 * once the invitation has gone, until the reply-by date or — with none — the start; when it refuses, `whyNot` says why
 * and no buttons are offered. The organiser is told in the app.
 */
export function InvitationAnswer({
  eventId,
  participantId,
  status,
  canAnswer,
  whyNot,
  inSeries,
  onAnswered,
}: {
  eventId: string;
  participantId: string;
  status?: InvitationStatus | null;
  canAnswer: boolean;
  whyNot?: string | null;
  inSeries: boolean;
  onAnswered?: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [comment, setComment] = useState('');
  const [scope, setScope] = useState<SeriesScope>('ThisOccurrence');
  const [busy, setBusy] = useState<InvitationStatus | null>(null);

  const answer = async (response: InvitationStatus) => {
    setBusy(response);
    try {
      const result = await companyCalendarService.reply(eventId, participantId, {
        response,
        comment: comment.trim() || null,
        scope: inSeries ? scope : 'ThisOccurrence',
      });
      const dates = result.eventNumbers.length;
      toast({
        title: response === 'Accepted' ? 'Accepted' : response === 'Declined' ? 'Declined' : 'Answered: may attend',
        description: [
          dates > 1 ? `For ${dates} dates.` : '',
          result.skipped ? `${result.skipped} date(s) could not be answered now and were left as they were.` : '',
          'The organiser is told.',
        ].filter(Boolean).join(' '),
      });
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-calendar'] });
      onAnswered?.();
    } catch (error: any) {
      toast({ title: 'Your answer was not recorded', description: error?.message ?? 'Please try again.', variant: 'destructive' });
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="space-y-3">
      <p className="text-sm font-medium">{answerWords(status)}</p>
      {canAnswer ? (
        <>
          {inSeries && (
            <div className="space-y-1">
              <Label>Which dates</Label>
              <Select value={scope} onValueChange={(v) => v && setScope(v as SeriesScope)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="ThisOccurrence">This date only</SelectItem>
                  <SelectItem value="ThisAndFollowing">This and the following dates</SelectItem>
                  <SelectItem value="WholeSeries">Every date still to come</SelectItem>
                </SelectContent>
              </Select>
            </div>
          )}
          <div className="space-y-1">
            <Label htmlFor={`answer-note-${participantId}`}>A note for the organiser (optional)</Label>
            <Textarea
              id={`answer-note-${participantId}`}
              rows={2}
              maxLength={1000}
              value={comment}
              onChange={(e) => setComment(e.target.value)}
            />
          </div>
          <div className="flex flex-wrap gap-2">
            <Button size="sm" disabled={!!busy} onClick={() => void answer('Accepted')}>
              {busy === 'Accepted' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Check className="mr-2 h-4 w-4" />}
              Accept
            </Button>
            <Button size="sm" variant="outline" disabled={!!busy} onClick={() => void answer('Tentative')}>
              {busy === 'Tentative' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <HelpCircle className="mr-2 h-4 w-4" />}
              May attend
            </Button>
            <Button size="sm" variant="outline" disabled={!!busy} onClick={() => void answer('Declined')}>
              {busy === 'Declined' ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <X className="mr-2 h-4 w-4" />}
              Decline
            </Button>
          </div>
        </>
      ) : (
        whyNot && <p className="text-sm text-muted-foreground">{whyNot}</p>
      )}
    </div>
  );
}
