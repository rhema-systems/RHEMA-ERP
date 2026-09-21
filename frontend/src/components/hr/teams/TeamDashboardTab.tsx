'use client';

/**
 * How a team is doing, at a glance.
 *
 * ⚠ **One server read, not six client calls.** The tiles have to agree with each other — an overdue
 * count that disagrees with the list below it is worse than no tile — and they can only be
 * guaranteed to if one query answers them all at one instant.
 *
 * ⚠ **The tiles that matter most are the ones nobody asks for.** A committee with no approved
 * charter, open work nobody owns, and decisions minuted with a responsible person that never became
 * a task: three ways a team quietly stops functioning, none of which shows up in a progress bar.
 *
 * Round 2, lane F2 (plan § 6.6).
 */

import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  CalendarClock,
  ClipboardList,
  FileWarning,
  Target,
  UserX,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { teamMeetingService } from '@/services/hr/team-meeting.service';
import { TEAM_REVIEW_STATUS_LABELS } from '@/types/hr/team-meeting';

function Stat({
  label,
  value,
  hint,
  tone,
}: {
  label: string;
  value: string | number;
  hint?: string;
  tone?: 'warn' | 'bad';
}) {
  const colour = tone === 'bad' ? 'text-red-600' : tone === 'warn' ? 'text-amber-600' : '';
  return (
    <div className="space-y-1">
      <p className="text-muted-foreground text-xs uppercase tracking-wide">{label}</p>
      <p className={`text-2xl font-semibold ${colour}`}>{value}</p>
      {hint && <p className="text-muted-foreground text-xs">{hint}</p>}
    </div>
  );
}

export function TeamDashboardTab({
  teamId,
  onGoToTerms,
}: {
  teamId: string;
  /** Switches the page to the Terms tab. Optional so the tile degrades to plain text without it. */
  onGoToTerms?: () => void;
}) {
  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'teams', teamId, 'dashboard'],
    queryFn: () => teamMeetingService.getDashboard(teamId),
  });

  if (isLoading || !data) return <Skeleton className="h-64 w-full" />;

  const termsExpiring =
    data.termsDaysUntilExpiry !== null &&
    data.termsDaysUntilExpiry !== undefined &&
    data.termsDaysUntilExpiry <= 30;

  return (
    <div className="space-y-4">
      {/*
        ⚠ Stated, not left blank. A committee operating with no approved charter is the single most
        useful thing this screen can say, and an absent tile would read as "not loaded yet".
      */}
      {data.hasNoApprovedTerms && (
        <Card className="border-amber-500/50">
          <CardHeader className="flex flex-row items-start gap-3 space-y-0 pb-3">
            <FileWarning className="mt-0.5 h-5 w-5 text-amber-600" />
            <div>
              <CardTitle className="text-base">This team has no approved terms of reference</CardTitle>
              <CardDescription>
                It is operating without a charter. Draft one on the Terms of Reference tab and have it
                approved.
              </CardDescription>
              {onGoToTerms && (
                <button
                  type="button"
                  className="mt-2 text-xs underline"
                  onClick={onGoToTerms}
                >
                  Go to Terms of Reference
                </button>
              )}
            </div>
          </CardHeader>
        </Card>
      )}

      {termsExpiring && !data.hasNoApprovedTerms && (
        <Card className="border-amber-500/50">
          <CardHeader className="flex flex-row items-start gap-3 space-y-0 pb-3">
            <FileWarning className="mt-0.5 h-5 w-5 text-amber-600" />
            <div>
              <CardTitle className="text-base">
                {data.termsDaysUntilExpiry! < 0
                  ? `Terms of reference v${data.termsVersion} have lapsed`
                  : `Terms of reference v${data.termsVersion} lapse in ${data.termsDaysUntilExpiry} days`}
              </CardTitle>
              <CardDescription>Take a new version before the charter runs out.</CardDescription>
            </div>
          </CardHeader>
        </Card>
      )}

      <div className="grid gap-4 md:grid-cols-2">
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <Target className="h-4 w-4" />
              Objectives
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-3 gap-4">
              <Stat label="Active" value={data.objectivesActive} />
              <Stat label="Completed" value={data.objectivesCompleted} />
              <Stat
                label="Overdue"
                value={data.objectivesOverdue}
                tone={data.objectivesOverdue > 0 ? 'bad' : undefined}
              />
            </div>
            <div className="space-y-1">
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">Average progress, active objectives</span>
                <span>{data.averageProgressPercent}%</span>
              </div>
              <Progress value={data.averageProgressPercent} />
            </div>
            {/* ⚠ Advisory. Nothing refuses a write because the weights do not sum to 100. */}
            {data.objectiveWeightTotal > 0 && !data.objectiveWeightsBalanced && (
              <p className="text-muted-foreground text-xs">
                Weights add up to {data.objectiveWeightTotal}%, not 100% — fine while the year is
                still being planned.
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <ClipboardList className="h-4 w-4" />
              Tasks
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 gap-4">
              <Stat label="Open" value={data.tasksOpen} />
              <Stat
                label="Overdue"
                value={data.tasksOverdue}
                tone={data.tasksOverdue > 0 ? 'bad' : undefined}
              />
              <Stat label="Due this week" value={data.tasksDueThisWeek} />
              <Stat
                label="Blocked"
                value={data.tasksBlocked}
                tone={data.tasksBlocked > 0 ? 'warn' : undefined}
              />
            </div>
            {/* The count a lead most needs and least often has. */}
            {data.tasksUnassigned > 0 && (
              <p className="mt-4 flex items-center gap-2 text-xs text-amber-600">
                <UserX className="h-3.5 w-3.5" />
                {data.tasksUnassigned} open task{data.tasksUnassigned === 1 ? '' : 's'} nobody owns
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 text-base">
              <CalendarClock className="h-4 w-4" />
              Meetings
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {data.nextMeetingAt ? (
              <p>
                <span className="text-muted-foreground">Next: </span>
                {data.nextMeetingTitle} ·{' '}
                {new Date(data.nextMeetingAt).toLocaleString(undefined, {
                  day: 'numeric',
                  month: 'short',
                  hour: '2-digit',
                  minute: '2-digit',
                })}
              </p>
            ) : (
              <p className="text-muted-foreground">Nothing scheduled.</p>
            )}
            <p className="text-muted-foreground text-xs">
              {data.lastMeetingHeldAt
                ? `Last met ${new Date(data.lastMeetingHeldAt).toLocaleDateString()}`
                : 'No meeting has been recorded as held.'}
            </p>
            {/*
              A decision minuted with somebody responsible that never became a task is an action
              item nobody is chasing — the exact failure a minute book is prone to.
            */}
            {data.unactionedDecisions > 0 && (
              <p className="flex items-center gap-2 text-xs text-amber-600">
                <AlertTriangle className="h-3.5 w-3.5" />
                {data.unactionedDecisions} decision{data.unactionedDecisions === 1 ? '' : 's'} with
                somebody responsible and no task raised
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Last review</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            {data.lastReviewPeriodEnd ? (
              <>
                <p>
                  Period to {data.lastReviewPeriodEnd.slice(0, 10)}
                  {data.lastReviewRating ? ` · rated ${data.lastReviewRating}/5` : ''}
                </p>
                {data.lastReviewStatus && (
                  <Badge variant="outline">
                    {TEAM_REVIEW_STATUS_LABELS[data.lastReviewStatus]}
                  </Badge>
                )}
              </>
            ) : (
              <p className="text-muted-foreground">This team has never been reviewed.</p>
            )}
          </CardContent>
        </Card>
      </div>

      {data.termsOfReferenceId && (
        <p className="text-muted-foreground text-xs">
          Charter: version {data.termsVersion}
          {data.termsDaysUntilExpiry === null || data.termsDaysUntilExpiry === undefined
            ? ' · open-ended'
            : ` · ${data.termsDaysUntilExpiry} days left`}
          {onGoToTerms && (
            <>
              {' · '}
              {/*
                ⚠ A button, not an anchor. `#terms` would have looked like a link and done nothing —
                the tabs are client state, not page fragments.
              */}
              <button type="button" className="underline" onClick={onGoToTerms}>
                see Terms of Reference
              </button>
            </>
          )}
        </p>
      )}
    </div>
  );
}
