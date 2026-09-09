'use client';

import { use, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  History,
  Loader2,
  Pencil,
  TriangleAlert,
  UserMinus,
  UserPlus,
  Users2,
} from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import {
  TeamForm,
  toCreateRequest,
  toFormValues,
  type TeamFormValues,
} from '@/components/hr/organization/TeamForm';
import { AddTeamMemberDialog } from '@/components/hr/organization/AddTeamMemberDialog';
import { EditTeamMemberDialog } from '@/components/hr/organization/EditTeamMemberDialog';
import { teamService } from '@/services/hr/team.service';
import {
  type TeamMember,
  teamMemberRoleLabel,
  teamStatusLabel,
  teamTypeLabel,
} from '@/types/hr/team';

/**
 * A team, its roster and its membership history.
 *
 * The roster shows former members as well as current ones, deliberately: removing someone stamps a
 * leaving date rather than deleting the row, because who was on a team and when is part of what the
 * register is for.
 */
export default function TeamDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [submitting, setSubmitting] = useState(false);
  const [addOpen, setAddOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<TeamMember | null>(null);
  const [removeTarget, setRemoveTarget] = useState<TeamMember | null>(null);
  const [removing, setRemoving] = useState(false);

  const teamQuery = useQuery({
    queryKey: ['hr', 'teams', id, 'detail'],
    queryFn: () => teamService.getDetail(id),
  });

  const { data: allTeams } = useQuery({
    queryKey: ['hr', 'teams', 'summary'],
    queryFn: () => teamService.getSummary(),
  });

  const historyQuery = useQuery({
    queryKey: ['hr', 'teams', id, 'member-history'],
    queryFn: () => teamService.getMemberHistory(id),
  });

  const team = teamQuery.data;

  // A team cannot be its own parent, and the server also refuses a cycle. Filtering the team out
  // here removes the most obvious way to attempt it rather than leaving the user to be told off.
  const parentCandidates = useMemo(
    () => (allTeams ?? []).filter((t) => t.id !== id),
    [allTeams, id],
  );

  const current = (team?.members ?? []).filter((m) => m.isCurrent);
  const former = (team?.members ?? []).filter((m) => !m.isCurrent);

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['hr', 'teams'] }),
      queryClient.invalidateQueries({ queryKey: ['organogram', 'teams'] }),
    ]);

  const handleSave = async (values: TeamFormValues) => {
    setSubmitting(true);
    try {
      await teamService.update(id, { id, ...toCreateRequest(values) });
      await refresh();
      toast({ title: 'Saved', description: 'The team was updated.' });
    } catch (error) {
      toast({
        title: 'Could not save the team',
        description: (error as Error)?.message || 'Failed to update the team.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleRemove = async () => {
    if (!removeTarget) return false;
    setRemoving(true);
    try {
      await teamService.removeMember(id, removeTarget.id, {});
      await refresh();
      toast({
        title: 'Removed from the team',
        description: `${removeTarget.employeeName} is no longer on ${team?.name}.`,
      });
      setRemoveTarget(null);
      return true;
    } catch (error) {
      toast({
        title: 'Could not remove the member',
        description: (error as Error)?.message || 'Failed to remove the member.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setRemoving(false);
    }
  };

  if (teamQuery.isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="text-muted-foreground h-6 w-6 animate-spin" />
      </div>
    );
  }

  if (teamQuery.isError || !team) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader title="Team" backHref="/administration/hr/organization/teams" />
        <Alert variant="destructive">
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>That team could not be loaded</AlertTitle>
          <AlertDescription>{(teamQuery.error as Error)?.message}</AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={team.name}
        description={`${teamTypeLabel(team.teamType)} · ${team.code}`}
        backHref="/administration/hr/organization/teams"
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant={team.status === 'Active' ? 'secondary' : 'outline'}>
              {teamStatusLabel(team.status)}
            </Badge>
            {team.hasLapsed && <Badge variant="outline">Lapsed</Badge>}
            <Badge variant="outline">
              {team.memberCount} member{team.memberCount === 1 ? '' : 's'}
            </Badge>
          </div>
        }
      />

      {team.hasLapsed && team.status === 'Active' && (
        <Alert>
          <TriangleAlert className="h-4 w-4" />
          <AlertTitle>This team is still marked Active but its end date has passed</AlertTitle>
          <AlertDescription>
            It ended on {team.effectiveTo}. Set the status to Dissolved, or extend the end date.
          </AlertDescription>
        </Alert>
      )}

      <Tabs defaultValue="members">
        <TabsList>
          <TabsTrigger value="members">Members ({current.length})</TabsTrigger>
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="history">History</TabsTrigger>
        </TabsList>

        <TabsContent value="members" className="space-y-4 pt-4">
          <Card>
            <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
              <div>
                <CardTitle>Roster</CardTitle>
                <CardDescription>
                  Who is on the team, their role and how much of their time it takes.
                  {team.maxMembers
                    ? ` Capped at ${team.maxMembers}.`
                    : ' No cap has been set.'}
                </CardDescription>
              </div>
              <Button onClick={() => setAddOpen(true)}>
                <UserPlus className="mr-2 h-4 w-4" /> Add member
              </Button>
            </CardHeader>
            <CardContent>
              {current.length === 0 ? (
                <EmptyState
                  icon={Users2}
                  title="Nobody is on this team yet"
                  description="A team with no members is a label. Add the people who actually work in it."
                  action={
                    <Button size="sm" onClick={() => setAddOpen(true)}>
                      <UserPlus className="mr-2 h-4 w-4" /> Add member
                    </Button>
                  }
                />
              ) : (
                <MemberTable
                  members={current}
                  onEdit={setEditTarget}
                  onRemove={setRemoveTarget}
                  showLeaveDate={false}
                />
              )}
            </CardContent>
          </Card>

          {former.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Former members ({former.length})</CardTitle>
                <CardDescription>
                  Kept on purpose. Removing someone ends their membership; it does not erase that
                  they were on the team.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <MemberTable members={former} onEdit={setEditTarget} showLeaveDate />
              </CardContent>
            </Card>
          )}
        </TabsContent>

        <TabsContent value="details" className="pt-4">
          <TeamForm
            parentCandidates={parentCandidates}
            defaultValues={toFormValues(team)}
            onSubmit={handleSave}
            submitting={submitting}
            submitLabel="Save changes"
            onCancel={() => router.push('/administration/hr/organization/teams')}
            initialLeadLabel={team.teamLeadName}
          />
        </TabsContent>

        <TabsContent value="history" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>Membership history</CardTitle>
              <CardDescription>
                Joining, leaving and role changes. A role change is recorded only when the role
                actually moves — a log that records everything is one nobody reads.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {historyQuery.isLoading ? (
                <div className="flex justify-center py-8">
                  <Loader2 className="text-muted-foreground h-5 w-5 animate-spin" />
                </div>
              ) : !historyQuery.data?.length ? (
                <EmptyState
                  icon={History}
                  title="Nothing recorded yet"
                  description="Entries appear as people join, change role or leave."
                />
              ) : (
                <div className="rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Employee</TableHead>
                        <TableHead>Change</TableHead>
                        <TableHead>From</TableHead>
                        <TableHead>To</TableHead>
                        <TableHead>Reason</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {historyQuery.data.map((h) => (
                        <TableRow key={h.id}>
                          <TableCell className="font-medium">{h.employeeName ?? '—'}</TableCell>
                          <TableCell className="text-muted-foreground">
                            {h.previousRole === h.newRole
                              ? teamMemberRoleLabel(h.newRole)
                              : `${teamMemberRoleLabel(h.previousRole)} → ${teamMemberRoleLabel(h.newRole)}`}
                          </TableCell>
                          <TableCell className="text-muted-foreground">{h.effectiveFrom}</TableCell>
                          <TableCell className="text-muted-foreground">
                            {h.effectiveTo ?? '—'}
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {h.changeReason ?? '—'}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <AddTeamMemberDialog
        teamId={id}
        teamName={team.name}
        open={addOpen}
        onOpenChange={setAddOpen}
        onAdded={refresh}
      />

      <EditTeamMemberDialog
        teamId={id}
        teamName={team.name}
        member={editTarget}
        onOpenChange={(open) => !open && setEditTarget(null)}
        onSaved={refresh}
      />

      <ConfirmationDialog
        open={removeTarget !== null}
        onOpenChange={(open) => !open && setRemoveTarget(null)}
        title="Remove from team"
        description={
          removeTarget
            ? `Remove ${removeTarget.employeeName} from "${team.name}"? Their membership is ended and dated today; the record of it is kept.`
            : ''
        }
        confirmText="Remove"
        variant="destructive"
        isLoading={removing}
        onConfirm={handleRemove}
      />
    </div>
  );
}

function MemberTable({
  members,
  onEdit,
  onRemove,
  showLeaveDate,
}: {
  members: TeamMember[];
  onEdit?: (member: TeamMember) => void;
  onRemove?: (member: TeamMember) => void;
  showLeaveDate: boolean;
}) {
  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Employee</TableHead>
            <TableHead>Position</TableHead>
            <TableHead>Role</TableHead>
            <TableHead className="text-right">Allocation</TableHead>
            <TableHead>Joined</TableHead>
            {showLeaveDate && <TableHead>Left</TableHead>}
            {(onEdit || onRemove) && <TableHead className="w-[110px]"></TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {members.map((m) => (
            <TableRow key={m.id}>
              <TableCell>
                <div className="flex flex-col">
                  <span className="flex items-center gap-2 font-medium">
                    {m.employeeName ?? '—'}
                    {m.isPrimary && (
                      <Badge variant="secondary" className="px-1.5 py-0 text-[10px]">
                        Primary team
                      </Badge>
                    )}
                  </span>
                  {m.employeeNumber && (
                    <span className="text-muted-foreground text-xs">{m.employeeNumber}</span>
                  )}
                </div>
              </TableCell>
              <TableCell className="text-muted-foreground">{m.positionTitle ?? '—'}</TableCell>
              <TableCell className="text-muted-foreground">
                {teamMemberRoleLabel(m.role)}
              </TableCell>
              <TableCell className="text-right tabular-nums">{m.allocationPercent}%</TableCell>
              <TableCell className="text-muted-foreground">{m.joinDate}</TableCell>
              {showLeaveDate && (
                <TableCell className="text-muted-foreground">{m.leaveDate ?? '—'}</TableCell>
              )}
              {(onEdit || onRemove) && (
                <TableCell>
                  <div className="flex justify-end gap-1">
                    {onEdit && (
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`Edit ${m.employeeName}`}
                        onClick={() => onEdit(m)}
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                    )}
                    {onRemove && (
                      <Button
                        variant="ghost"
                        size="icon"
                        aria-label={`Remove ${m.employeeName}`}
                        onClick={() => onRemove(m)}
                      >
                        <UserMinus className="h-4 w-4" />
                      </Button>
                    )}
                  </div>
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
