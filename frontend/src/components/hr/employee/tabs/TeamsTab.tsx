'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Users2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { teamService } from '@/services/hr/team.service';
import { teamMemberRoleLabel } from '@/types/hr/team';

/**
 * Every team this person works on, current and past.
 *
 * ⚠ Read-only, and that is the decision rather than an omission. Membership is written from the
 * TEAM side because that is where the server's rules live: the team's member cap, "already a
 * current member", "someone who has left cannot be added", and the one-primary-team-per-person
 * rule that reaches across every team at once. An editor here would either restate those rules in a
 * second place or discover them one 400 at a time.
 *
 * ⚠ The allocation column is the reason this tab is worth having at all. A person's time can be
 * split across several teams, and nothing else in the employee record shows that it adds up to more
 * than 100% — the roster on any single team cannot see the others.
 */
export function TeamsTab({ employeeId }: { employeeId: string }) {
  const { data: memberships = [], isLoading } = useQuery({
    // `currentOnly: false` — a former membership is part of the record, exactly as the team's own
    // roster keeps its leavers.
    queryKey: ['hr', 'employees', employeeId, 'teams'],
    queryFn: () => teamService.getForEmployee(employeeId, false),
    enabled: !!employeeId,
  });

  const current = memberships.filter((m) => m.isCurrent);
  const former = memberships.filter((m) => !m.isCurrent);
  const committed = current.reduce((sum, m) => sum + (m.allocationPercent ?? 0), 0);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (!memberships.length) {
    return (
      <EmptyState
        icon={Users2}
        title="Not on any team"
        description="Teams are made up from the team's own screen, under Administration › Organization › Teams."
      />
    );
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Current teams ({current.length})</CardTitle>
          <CardDescription>
            {committed > 100
              ? `This person's time is allocated at ${committed}% across their teams — more than one person's worth.`
              : `${committed}% of this person's time is allocated across their teams.`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {current.length === 0 ? (
            <p className="text-muted-foreground text-sm">
              No current membership — only the former ones below.
            </p>
          ) : (
            <MembershipTable rows={current} showLeaveDate={false} />
          )}
        </CardContent>
      </Card>

      {former.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Former teams ({former.length})</CardTitle>
            <CardDescription>
              Kept on purpose. Leaving a team ends the membership; it does not erase it.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <MembershipTable rows={former} showLeaveDate />
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function MembershipTable({
  rows,
  showLeaveDate,
}: {
  rows: Awaited<ReturnType<typeof teamService.getForEmployee>>;
  showLeaveDate: boolean;
}) {
  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Team</TableHead>
            <TableHead>Role</TableHead>
            <TableHead className="text-right">Allocation</TableHead>
            <TableHead>Joined</TableHead>
            {showLeaveDate && <TableHead>Left</TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((m) => (
            <TableRow key={m.id}>
              <TableCell>
                <div className="flex items-center gap-2">
                  <Link
                    href={`/administration/hr/organization/teams/${m.teamId}`}
                    className="font-medium hover:underline"
                  >
                    {m.teamName ?? '—'}
                  </Link>
                  {m.isPrimary && (
                    <Badge variant="secondary" className="px-1.5 py-0 text-[10px]">
                      Primary team
                    </Badge>
                  )}
                </div>
              </TableCell>
              <TableCell className="text-muted-foreground">{teamMemberRoleLabel(m.role)}</TableCell>
              <TableCell className="text-right tabular-nums">{m.allocationPercent}%</TableCell>
              <TableCell className="text-muted-foreground">{m.joinDate}</TableCell>
              {showLeaveDate && (
                <TableCell className="text-muted-foreground">{m.leaveDate ?? '—'}</TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
