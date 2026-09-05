'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowUpRight,
  ClipboardList,
  Mail,
  MessagesSquare,
  Users,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  staffDirectoryService,
  type StaffDirectoryEntry,
} from '@/services/hr/staff-directory.service';

/**
 * My team (area 25 slice 13a).
 *
 * ⚠ **This page is honestly empty for most people, and says so.** Only 416 of 8,131 live
 * employees carry a `ManagerId` on DEFAULT (5.1%), so most staff manage nobody *and* have no
 * recorded manager. The empty state explains that the reporting line is missing from the record
 * rather than implying the feature is broken — the org-authority model that would populate it is
 * a deferred module, not something this screen can fix.
 *
 * The manager half is why the page is worth opening even for the ~95%: a page that tells you who
 * you report to has said something, even with no reports of your own.
 *
 * ⚠ The onward links are only the two manager surfaces that genuinely exist in the portal today
 * (development plans' team scope, check-ins' conducting tab). The D3 rule: never a link to a
 * destination the manager cannot actually reach.
 */

function initials(name: string) {
  const parts = name.split(/\s+/).filter(Boolean);
  if (!parts.length) return '·';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function ReportCard({ person }: { person: StaffDirectoryEntry }) {
  return (
    <Link
      href={`/me/directory/${person.id}`}
      className="flex items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-muted/50"
    >
      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
        {initials(person.fullName)}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate font-medium">{person.fullName}</span>
        <span className="block truncate text-sm text-muted-foreground">{person.positionTitle}</span>
        <span className="mt-0.5 flex items-center gap-1.5 truncate text-xs text-muted-foreground">
          <Mail className="h-3 w-3 shrink-0" />
          <span className="truncate">{person.emailAddress}</span>
        </span>
      </span>
    </Link>
  );
}

export default function MyTeamPage() {
  const { data: team, isLoading, isError } = useQuery({
    queryKey: ['me', 'my-team'],
    queryFn: () => staffDirectoryService.getMyTeam(),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="My team"
        description="Who you report to, and who reports to you."
        backHref="/me"
      />

      {isLoading ? (
        <div className="space-y-4">
          <Skeleton className="h-24" />
          <Skeleton className="h-40" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">
          Your team could not be loaded right now. Try again in a moment.
        </p>
      ) : (
        <>
          {/* ── Who I report to ──────────────────────────────────────────── */}
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">I report to</CardTitle>
            </CardHeader>
            <CardContent>
              {team?.managerId && team.managerName ? (
                <div className="flex flex-wrap items-center gap-3">
                  <Link
                    href={`/me/directory/${team.managerId}`}
                    className="flex min-w-0 flex-1 items-center gap-3 rounded-lg border p-3 hover:bg-muted/50"
                  >
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
                      {initials(team.managerName)}
                    </span>
                    <span className="min-w-0">
                      <span className="block truncate font-medium">{team.managerName}</span>
                      <span className="block truncate text-sm text-muted-foreground">
                        {team.managerPositionTitle ?? ''}
                      </span>
                    </span>
                  </Link>
                  {team.managerEmailAddress && (
                    <Button variant="outline" size="sm" asChild>
                      <a href={`mailto:${team.managerEmailAddress}`}>
                        <Mail className="mr-1.5 h-3.5 w-3.5" /> Email
                      </a>
                    </Button>
                  )}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  No manager is recorded on your employee record. If that looks wrong, HR can set
                  it — you can ask through{' '}
                  <Link href="/me/profile" className="text-primary hover:underline">
                    My profile
                  </Link>
                  .
                </p>
              )}
            </CardContent>
          </Card>

          {/* ── Who reports to me ────────────────────────────────────────── */}
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-sm font-medium">
                <Users className="h-4 w-4" />
                My direct reports
                {team?.managesAnyone && <Badge variant="secondary">{team.directReports.length}</Badge>}
              </CardTitle>
            </CardHeader>
            <CardContent>
              {!team?.managesAnyone ? (
                <div className="py-6">
                  <EmptyState
                    icon={Users}
                    title="Nobody reports to you"
                    description="If you manage people and they are not listed here, their employee records do not yet name you as their manager. HR maintains that link."
                  />
                </div>
              ) : (
                <div className="grid gap-3 sm:grid-cols-2">
                  {team.directReports.map((r) => (
                    <ReportCard key={r.id} person={r} />
                  ))}
                </div>
              )}
            </CardContent>
          </Card>

          {/* Only shown to someone who actually manages people — otherwise these two tabs are
              empty for them and the card is noise. */}
          {team?.managesAnyone && (
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium">Manager tools</CardTitle>
              </CardHeader>
              <CardContent className="grid gap-2 sm:grid-cols-2">
                <Button variant="outline" className="justify-start" asChild>
                  <Link href="/me/performance/development-plans">
                    <ClipboardList className="mr-2 h-4 w-4" />
                    My team&apos;s development plans
                    <ArrowUpRight className="ml-auto h-3.5 w-3.5 text-muted-foreground" />
                  </Link>
                </Button>
                <Button variant="outline" className="justify-start" asChild>
                  <Link href="/me/performance/check-ins">
                    <MessagesSquare className="mr-2 h-4 w-4" />
                    Check-ins I run
                    <ArrowUpRight className="ml-auto h-3.5 w-3.5 text-muted-foreground" />
                  </Link>
                </Button>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
