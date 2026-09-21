'use client';

/**
 * The teams and committees I am on, and what I owe them.
 *
 * ⚠ **`/me/teams`, not `/me/team`.** The existing `/me/team` (area 25) is the MANAGER's
 * direct-reports page — who reports to me on the org chart. This is a different thing entirely:
 * the working groups and committees I sit on, which in a matrix organisation is usually several
 * and rarely the same people. The two are cross-linked rather than merged.
 *
 * ⚠ **My tasks come first, across every team, because that is what a member opens this page for.**
 * The per-team breakdown is below it. A member who is also a lead sees the same page; the full
 * planning tabs live on the team's own record, where the roster and the charter are.
 *
 * ⚠ **Every list is scoped by the SERVER to teams the caller belongs to.** A non-member gets 403
 * from a team's tasks, not an empty list, so nothing here can leak another team's work.
 *
 * Round 2, lane F1 (plan § 6.6.3).
 */

import Link from 'next/link';
import { useQueries, useQuery } from '@tanstack/react-query';
import { ArrowUpRight, ClipboardList, Users2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useAuth } from '@/hooks/use-auth';
import { teamService } from '@/services/hr/team.service';
import { teamActivityService } from '@/services/hr/team-activity.service';
import { teamMemberRoleLabel, teamTypeLabel } from '@/types/hr/team';
import { TEAM_TASK_STATUS_LABELS, type TeamTask } from '@/types/hr/team-activity';

const STATUS_VARIANT: Record<string, 'default' | 'secondary' | 'outline' | 'destructive'> = {
  NotStarted: 'outline',
  InProgress: 'secondary',
  Blocked: 'destructive',
  Completed: 'default',
  Cancelled: 'outline',
};

export default function MyTeamsPage() {
  const { user } = useAuth();
  const employeeId = user?.employeeId ?? '';

  const { data: memberships = [], isLoading } = useQuery({
    queryKey: ['me', 'teams', employeeId],
    queryFn: () => teamService.getForEmployee(employeeId),
    enabled: !!employeeId,
  });

  // One query per team rather than one big read: there is no cross-team task endpoint, and adding
  // one would need its own authorisation story. A person sits on a handful of teams, not hundreds.
  const taskQueries = useQueries({
    queries: memberships.map((m) => ({
      queryKey: ['me', 'team-tasks', m.teamId],
      // ⚠ `mine: true` is resolved from the TOKEN server-side. This screen does not send a member id.
      queryFn: () => teamActivityService.getTasks(m.teamId, { mine: true }),
      enabled: !!m.teamId,
    })),
  });

  const openTasks: { task: TeamTask; teamName: string; teamId: string }[] = memberships.flatMap(
    (m, i) =>
      (taskQueries[i]?.data ?? [])
        .filter((t) => t.status !== 'Completed' && t.status !== 'Cancelled')
        .map((task) => ({ task, teamName: m.teamName ?? 'Team', teamId: m.teamId })),
  );

  // Overdue first, then by due date, then the undated. What a member needs to see is what is late.
  openTasks.sort((a, b) => {
    if (a.task.isOverdue !== b.task.isOverdue) return a.task.isOverdue ? -1 : 1;
    if (!a.task.dueDate) return 1;
    if (!b.task.dueDate) return -1;
    return a.task.dueDate.localeCompare(b.task.dueDate);
  });

  if (!employeeId) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="My Teams" description="The teams and committees I am on." />
        <EmptyState
          icon={Users2}
          title="Your login is not linked to an employee record"
          description="Team membership hangs off the employee register, so there is nothing to show until an administrator links them."
        />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="My Teams"
        description="The working groups and committees I sit on, and what I owe them."
      />

      {isLoading ? (
        <Skeleton className="h-40 w-full" />
      ) : memberships.length === 0 ? (
        <EmptyState
          icon={Users2}
          title="You are not on any team yet"
          description="Teams and committees are set up by HR or a team lead. Looking for the people who report to you instead?"
          action={
            <Button variant="outline" asChild>
              <Link href="/me/team">Go to My Team</Link>
            </Button>
          }
        />
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-base">
                <ClipboardList className="h-4 w-4" />
                What I owe ({openTasks.length})
              </CardTitle>
              <CardDescription>
                Open tasks assigned to me, across every team. Overdue first.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {openTasks.length === 0 ? (
                <p className="text-muted-foreground text-sm">
                  Nothing outstanding. Anything assigned to you will appear here.
                </p>
              ) : (
                <ul className="divide-y">
                  {openTasks.map(({ task, teamName, teamId }) => (
                    <li key={task.id} className="flex items-center gap-3 py-2">
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-medium">{task.title}</p>
                        <p className="text-muted-foreground text-xs">
                          {teamName}
                          {task.objectiveTitle ? ` · ${task.objectiveTitle}` : ''}
                          {task.dueDate ? ` · due ${task.dueDate.slice(0, 10)}` : ''}
                        </p>
                        {/* A blocked task that cannot say why tells nobody anything. */}
                        {task.status === 'Blocked' && task.blockedReason && (
                          <p className="text-muted-foreground truncate text-xs italic">
                            {task.blockedReason}
                          </p>
                        )}
                      </div>
                      {task.isOverdue && <Badge variant="destructive">Overdue</Badge>}
                      <Badge variant={STATUS_VARIANT[task.status] ?? 'outline'}>
                        {TEAM_TASK_STATUS_LABELS[task.status]}
                      </Badge>
                      <Button variant="ghost" size="sm" asChild>
                        <Link href={`/administration/hr/organization/teams/${teamId}`}>
                          <ArrowUpRight className="h-4 w-4" />
                        </Link>
                      </Button>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>

          <div className="grid gap-4 md:grid-cols-2">
            {memberships.map((m, i) => {
              const tasks = taskQueries[i]?.data ?? [];
              const open = tasks.filter((t) => t.status !== 'Completed' && t.status !== 'Cancelled');
              return (
                <Card key={m.id}>
                  <CardHeader className="pb-3">
                    <CardTitle className="flex items-center justify-between gap-2 text-base">
                      <span className="truncate">{m.teamName ?? 'Team'}</span>
                      <Badge variant="outline">{teamMemberRoleLabel(m.role)}</Badge>
                    </CardTitle>
                    <CardDescription>
                      Joined {m.joinDate?.slice(0, 10)}
                      {m.allocationPercent ? ` · ${m.allocationPercent}% of my time` : ''}
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <p className="text-sm">
                      {open.length === 0
                        ? 'Nothing assigned to me here.'
                        : `${open.length} open task${open.length === 1 ? '' : 's'} assigned to me.`}
                    </p>
                    <Button variant="outline" size="sm" asChild>
                      <Link href={`/administration/hr/organization/teams/${m.teamId}`}>
                        Open the team
                      </Link>
                    </Button>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}
