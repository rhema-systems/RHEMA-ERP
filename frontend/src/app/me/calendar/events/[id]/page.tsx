'use client';

import { use, type ReactNode } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ExternalLink, Loader2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { InvitationAnswer } from '@/components/hr/company-schedule/InvitationAnswer';
import { companyCalendarService } from '@/services/hr/company-schedule.service';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="grid gap-1 sm:grid-cols-[10rem_1fr]">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  );
}

/**
 * An event as staff see it (company-schedule final closure lane 7): what a guest's notice opens (the user's ruling), and
 * what the calendar opens for anyone who is not on the HR desk. What, when, where, how to join, who organises it and who
 * it is for — and the reader's own invitation, answered here (D-8). The meeting password only to its guests and its
 * organiser; no budget, no other guest's answer. The HR desk is offered its full page.
 */
export default function MyCalendarEventPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const event = useQuery({
    queryKey: ['hr', 'company-calendar', 'event', id],
    queryFn: () => companyCalendarService.getEvent(id),
    retry: false,
  });

  if (event.isLoading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }
  const e = event.data;
  if (!e) {
    return (
      <div className="space-y-6">
        <PageHeader title="Event" backHref="/me/calendar" />
        <Card>
          <CardContent className="py-8 text-center text-sm text-muted-foreground">
            This event was not found — or it is not one you are invited to or that is for you.
          </CardContent>
        </Card>
      </div>
    );
  }

  const where = [e.siteName, e.venueName, e.venueAddress].filter(Boolean).join(', ');
  const online = e.locationType === 'Virtual' || e.locationType === 'Hybrid';

  return (
    <div className="space-y-6">
      <PageHeader
        title={e.eventName}
        description={`${e.eventNumber} · ${spaced(e.category)}`}
        backHref="/me/calendar"
        actions={
          e.canOpenHrPage ? (
            <Button asChild variant="outline">
              <Link href={`/hr/company-schedule/events/${e.id}`}>
                <ExternalLink className="mr-2 h-4 w-4" /> Open the HR page
              </Link>
            </Button>
          ) : undefined
        }
      />

      <div className="flex flex-wrap gap-2">
        {e.isCancelled ? <Badge variant="destructive">Cancelled</Badge> : <Badge variant="secondary">{spaced(e.status)}</Badge>}
        {e.awaitingApproval && <Badge variant="outline">Awaiting approval</Badge>}
        {e.isOrganiser && <Badge variant="outline">You organise it</Badge>}
        {e.seriesId && e.occurrenceNumber && (
          <Badge variant="outline">Date {e.occurrenceNumber} of {e.occurrenceCount ?? '?'}</Badge>
        )}
      </div>

      {e.isCancelled && e.cancellationReason && (
        <p className="rounded-md border border-destructive/40 p-3 text-sm">Cancelled: {e.cancellationReason}</p>
      )}

      <Card>
        <CardHeader><CardTitle className="text-base">The event</CardTitle></CardHeader>
        <CardContent>
          <dl className="space-y-3">
            <Row label="When">{e.when}</Row>
            {where && <Row label="Where">{where}</Row>}
            {e.rooms.length > 0 && <Row label="Rooms">{e.rooms.join(' · ')}</Row>}
            {online && (
              <Row label="Joining">
                {e.onlineMeetingLink ? (
                  <a className="text-primary underline underline-offset-2" href={e.onlineMeetingLink} target="_blank" rel="noreferrer noopener">
                    Join online
                  </a>
                ) : (
                  'The link is not set yet'
                )}
                {e.meetingPassword && <span className="ml-2 text-muted-foreground">· password {e.meetingPassword}</span>}
              </Row>
            )}
            <Row label="Organiser">{e.organizerName ?? '—'}</Row>
            <Row label="For">{e.audienceDescription}</Row>
            {e.requiresRsvp && e.rsvpDeadline && (
              <Row label="Reply by">
                {new Date(e.rsvpDeadline).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
              </Row>
            )}
            {e.description && <Row label="About it"><span className="whitespace-pre-line">{e.description}</span></Row>}
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Your invitation</CardTitle></CardHeader>
        <CardContent>
          {e.myInvitation ? (
            <InvitationAnswer
              eventId={e.id}
              participantId={e.myInvitation.participantId}
              status={e.myInvitation.status}
              canAnswer={e.myInvitation.canAnswer}
              whyNot={e.myInvitation.whyNot}
              inSeries={!!e.seriesId}
              onAnswered={() => void event.refetch()}
            />
          ) : (
            <p className="text-sm text-muted-foreground">
              {e.isOrganiser
                ? 'You organise this event.'
                : 'You are not on its guest list — it is on your calendar because it is for you, and there is nothing to answer.'}
            </p>
          )}
          {e.myInvitation?.responseComments && (
            <p className="mt-3 text-sm text-muted-foreground">Your note: {e.myInvitation.responseComments}</p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
