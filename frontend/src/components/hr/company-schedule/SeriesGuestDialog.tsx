'use client';

import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Loader2 } from 'lucide-react';
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
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/components/ui/use-toast';
import { companyEventService } from '@/services/hr/company-schedule.service';
import { INVITATION_ANSWERS, SERIES_SCOPE_LABELS } from '@/types/hr/company-schedule';
import type {
  EventParticipant,
  EventSeriesGuestResult,
  InvitationStatus,
  SeriesScope,
} from '@/types/hr/company-schedule';
import { describeReach } from './noticeReach';

/** A guest action across a series' dates, asked for from a guest's row (lane 2f-2a, D-12). */
export interface SeriesGuestAction {
  mode: 'answer' | 'remove';
  guest: EventParticipant;
}

/**
 * What a guest action with a series scope did, for its toast: the dates, those passed over and why, those left alone,
 * those waiting for the approval, and who the one notice reached.
 *
 * @param passedOver Why a date the scope covered was passed over: "already invited", "not invited".
 */
export function describeSeriesGuest(r: EventSeriesGuestResult, passedOver: string): string {
  const parts = [`${r.eventNumbers.length} date(s): ${r.eventNumbers.join(', ')}.`];
  if (r.skipped) parts.push(`${r.skipped} passed over (${passedOver}).`);
  if (r.closed) parts.push(`${r.closed} left alone: started, completed or cancelled.`);
  if (r.waiting) parts.push(`${r.waiting} invitation(s) wait for the approval.`);
  const reach = describeReach(r.told, 'Told once');
  if (reach) parts.push(reach);
  return parts.join(' ');
}

const SCOPES: SeriesScope[] = ['ThisAndFollowing', 'WholeSeries'];

/**
 * Records one guest's answer for several dates of a series, or takes them off several dates — this and following, or
 * every date still to come (lane 2f-2a). A single date is answered or removed from the row's own actions.
 */
export function SeriesGuestDialog({
  eventId,
  action,
  onClose,
}: {
  eventId: string;
  action: SeriesGuestAction | null;
  onClose: () => void;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [scope, setScope] = useState<SeriesScope>('ThisAndFollowing');
  const [answer, setAnswer] = useState<InvitationStatus>('Accepted');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!action) return;
    setScope('ThisAndFollowing');
    setAnswer('Accepted');
  }, [action]);

  if (!action) return null;
  const { mode, guest } = action;
  const removing = mode === 'remove';

  const run = async () => {
    setBusy(true);
    try {
      const result = removing
        ? await companyEventService.removeParticipant(guest.id, scope)
        : await companyEventService.respondToInvitation(eventId, {
            participantId: guest.id,
            response: answer,
            responseComments: null,
            scope,
          });
      // Every date's guest list, page and register row may have changed.
      await queryClient.invalidateQueries({ queryKey: ['hr', 'company-schedule', 'events'] });
      toast({
        title: removing ? `${guest.participantName} taken off the guest list` : `${answer} recorded for ${guest.participantName}`,
        description: describeSeriesGuest(result, 'not invited'),
      });
      onClose();
    } catch (error: any) {
      toast({
        title: removing ? 'Could not take them off' : 'Could not record the answer',
        description: error?.response?.data?.detail ?? error?.message ?? 'Please try again.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            {removing ? `Take ${guest.participantName} off several dates` : `Answer for ${guest.participantName} on several dates`}
          </DialogTitle>
          <DialogDescription>
            {removing
              ? 'They are taken off every date chosen that they are on, and told once, listing the dates. '
              : 'The same answer is recorded on every date chosen that they are invited to. '}
            A date that has started, been completed or been cancelled is left as it is.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label>Which dates</Label>
            <Select value={scope} onValueChange={(v) => v && setScope(v as SeriesScope)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {SCOPES.map((s) => (
                  <SelectItem key={s} value={s}>{SERIES_SCOPE_LABELS[s]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {!removing && (
            <div className="space-y-2">
              <Label>Answer</Label>
              <Select value={answer} onValueChange={(v) => v && setAnswer(v as InvitationStatus)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {INVITATION_ANSWERS.map((a) => (
                    <SelectItem key={a} value={a}>{a}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button variant={removing ? 'destructive' : 'default'} onClick={run} disabled={busy}>
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {removing ? 'Take them off' : 'Record'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
