'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { ordinal } from '@/components/hr/company-schedule/milestoneWords';
import { CalendarCheck, CalendarDays, CalendarClock, CalendarRange, DoorOpen, Flag, Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { NavCardGrid } from '@/components/hr/common/NavCardGrid';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { useAuth } from '@/hooks/use-auth';
import { companyEventService } from '@/services/hr/company-schedule.service';

const spaced = (s?: string | null) => (s ? s.replace(/([a-z])([A-Z])/g, '$1 $2') : '—');

/**
 * The company schedule landing page. Operational screens live here; the things that get set up
 * once — rooms, milestones, closures, fiscal years — live under Administration.
 *
 * ⚠ Lane 5a (R4-3.1, the user's ruling): the summary is the HR desk's (`HR.Company.Read`). Anyone else used to see all four
 * cards say "Nothing scheduled" — the server had refused them, and the page read the refusal as an empty diary. They
 * now see one line saying whose the summary is, and the pages that are theirs.
 */
export default function CompanySchedulePage() {
  const { user, isLoading: authLoading, hasPermission } = useAuth();
  const canRead = !!user && hasPermission('HR.Company.Read');
  // Lane 2g-1 (D-9): the four lists in one read — it made four.
  const { data: dashboard, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'company-schedule', 'dashboard'],
    queryFn: () => companyEventService.getDashboard(),
    enabled: canRead,
    retry: (count, e: any) => e?.status !== 403 && count < 2,
  });
  const refused = (!authLoading && !!user && !canRead) || (error as any)?.status === 403;
  const events = dashboard?.upcomingEvents;
  const pending = dashboard?.pendingBookings;
  const closures = dashboard?.upcomingClosures;
  const milestones = dashboard?.upcomingMilestones;

  if (refused) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Company schedule" description="Events, room bookings, closures and milestones." />
        <Card>
          <CardContent className="space-y-3 p-6 text-sm">
            <p>
              This summary of the company&apos;s events, room bookings, closures and milestones is for the HR desk.
            </p>
            <p className="text-muted-foreground">What is yours to see:</p>
            <Link
              href="/hr/company-schedule/my-schedule"
              className="inline-flex items-center gap-2 font-medium text-primary underline-offset-4 hover:underline"
            >
              <CalendarRange className="h-4 w-4" /> My Schedule — your meetings, events, bookings, leave and closures
            </Link>
          </CardContent>
        </Card>
      </div>
    );
  }

  if (authLoading || (canRead && isLoading)) {
    return (
      <div className="flex items-center gap-2 p-6 text-sm text-muted-foreground">
        <Loader2 className="h-4 w-4 animate-spin" /> Loading the company schedule…
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Company schedule"
        description="Events, room bookings, closures and the milestones on the company calendar."
      />

      {isError && (
        <p className="rounded-md border border-destructive/40 p-3 text-sm text-destructive">
          The summary could not be loaded — that is not an empty schedule. Reload the page to try again.
        </p>
      )}

      <NavCardGrid
        items={[
          {
            title: 'Events',
            description: 'Meetings, training days, conferences and company occasions.',
            href: '/hr/company-schedule/events',
            icon: CalendarDays,
          },
          {
            title: 'Room bookings',
            description: 'Who has which room, and what is waiting on approval.',
            href: '/hr/company-schedule/bookings',
            icon: CalendarCheck,
          },
          {
            title: 'Meeting rooms',
            description: 'The rooms people can book and the rules for booking them.',
            href: '/administration/hr/company-schedule/rooms',
            icon: DoorOpen,
          },
          {
            title: 'Business closures',
            description: 'Days the organisation is shut, company-wide or per site.',
            href: '/administration/hr/company-schedule/closures',
            icon: CalendarClock,
          },
          {
            title: 'Milestones',
            description: 'Anniversaries, achievements and dates worth marking.',
            href: '/administration/hr/company-schedule/milestones',
            icon: Flag,
          },
        ]}
      />

      {!isError && (
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Next 30 days</CardTitle>
          </CardHeader>
          <CardContent>
            {(events ?? []).length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing scheduled in the next month.</p>
            ) : (
              <ul className="space-y-3">
                {(events ?? []).slice(0, 6).map((e) => (
                  <li key={e.id} className="flex items-start justify-between gap-3 text-sm">
                    <div>
                      <p className="font-medium">{e.eventName}</p>
                      <p className="text-muted-foreground">
                        {e.startDate.slice(0, 10)} · {spaced(e.category)} · {e.organizerName}
                      </p>
                    </div>
                    <StatusBadge status={spaced(e.status)} />
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Bookings awaiting approval {pending?.length ? `(${pending.length})` : ''}
            </CardTitle>
          </CardHeader>
          <CardContent>
            {(pending ?? []).length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing is waiting on a decision.</p>
            ) : (
              <ul className="space-y-3">
                {(pending ?? []).slice(0, 6).map((b) => (
                  <li key={b.id} className="flex items-start justify-between gap-3 text-sm">
                    <div>
                      <p className="font-medium">{b.roomName}</p>
                      <p className="text-muted-foreground">
                        {new Date(b.startDateTime).toLocaleString(undefined, {
                          dateStyle: 'medium',
                          timeStyle: 'short',
                        })}{' '}
                        · {b.bookedByName}
                      </p>
                    </div>
                    <StatusBadge status={spaced(b.status)} />
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Closures ahead</CardTitle>
          </CardHeader>
          <CardContent>
            {(closures ?? []).length === 0 ? (
              <p className="text-sm text-muted-foreground">No closures in the next two months.</p>
            ) : (
              <ul className="space-y-3">
                {(closures ?? []).slice(0, 6).map((c) => (
                  <li key={c.id} className="flex items-start justify-between gap-3 text-sm">
                    <div>
                      <p className="font-medium">{c.title}</p>
                      <p className="text-muted-foreground">
                        {c.startDate.slice(0, 10)}
                        {c.endDate.slice(0, 10) !== c.startDate.slice(0, 10)
                          ? ` → ${c.endDate.slice(0, 10)}`
                          : ''}{' '}
                        · {c.scopeDescription || '—'}
                      </p>
                    </div>
                    <StatusBadge status={c.isPaidClosure ? 'Paid' : 'Unpaid'} />
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">Milestones ahead</CardTitle>
          </CardHeader>
          <CardContent>
            {(milestones ?? []).length === 0 ? (
              <p className="text-sm text-muted-foreground">Nothing coming up in the next quarter.</p>
            ) : (
              <ul className="space-y-3">
                {(milestones ?? []).slice(0, 6).map((m) => (
                  <li key={m.id} className="text-sm">
                    <p className="font-medium">{m.title}</p>
                    <p className="text-muted-foreground">
                      {/* Lane 4a: the occurrence ahead — a yearly milestone's anniversary, not its first date. */}
                      {m.occurrenceDate.slice(0, 10)} · {spaced(m.category)}
                      {m.isRecurringAnnually && m.yearsSince > 0 ? ` · ${ordinal(m.yearsSince)} anniversary` : ''}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>
      </div>
      )}
    </div>
  );
}
