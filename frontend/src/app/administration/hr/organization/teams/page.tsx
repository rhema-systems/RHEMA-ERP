'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { MoreHorizontal, Pencil, Plus, Search, Trash2, Users2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { teamService } from '@/services/hr/team.service';
import { type Team, teamStatusLabel, teamTypeLabel } from '@/types/hr/team';

/**
 * The teams register.
 *
 * A team is who actually works together, as distinct from the organization unit a person is
 * formally posted into. Built in slice 4b: the entities, tables and EF configuration had existed
 * since the port, with no controller, no service and no writer of any kind behind them.
 */
const PAGE_SIZE = 20;

export default function TeamsPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<Team | null>(null);
  const [deleting, setDeleting] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'teams', page, PAGE_SIZE, search],
    queryFn: () => teamService.getPaged(page, PAGE_SIZE, search || undefined),
  });

  const teams = data?.items ?? [];

  const handleDelete = async () => {
    if (!deleteTarget) return false;
    setDeleting(true);
    try {
      await teamService.remove(deleteTarget.id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'teams'] });
      toast({ title: 'Dissolved', description: `"${deleteTarget.name}" was removed.` });
      setDeleteTarget(null);
      return true;
    } catch (error) {
      // The service refuses a team that still has members or sub-teams, and says which. Showing
      // that sentence is the point of mapping the rule to a 400 rather than letting it 500.
      toast({
        title: 'Cannot dissolve this team',
        description: (error as Error)?.message || 'Failed to delete the team.',
        variant: 'destructive',
      });
      return false;
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Teams"
        description="Working groups — permanent, project, task force, cross-functional, shift or committee — and who is in them."
        backHref="/administration/hr/organization"
        actions={
          <Button onClick={() => router.push('/administration/hr/organization/teams/new')}>
            <Plus className="mr-2 h-4 w-4" /> New Team
          </Button>
        }
      />

      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-4 space-y-0">
          <CardTitle>Teams</CardTitle>
          <div className="relative w-full max-w-xs">
            <Search className="text-muted-foreground absolute top-1/2 left-2 h-4 w-4 -translate-y-1/2" />
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              placeholder="Name, code or project code…"
              className="pl-8"
            />
          </div>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Lead</TableHead>
                  <TableHead>Owning unit</TableHead>
                  <TableHead className="text-right">Members</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(5)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[120px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : teams.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={Users2}
                        title={search ? 'No teams match that search' : 'No teams yet'}
                        description={
                          search
                            ? 'Clear the search to see the whole register.'
                            : 'Create the first team, then add its members. Teams appear on the organogram as their own view.'
                        }
                        action={
                          search ? undefined : (
                            <Button
                              size="sm"
                              onClick={() => router.push('/administration/hr/organization/teams/new')}
                            >
                              <Plus className="mr-2 h-4 w-4" /> New Team
                            </Button>
                          )
                        }
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  teams.map((team) => (
                    <TableRow
                      key={team.id}
                      className="hover:bg-muted/50 cursor-pointer"
                      onClick={() => router.push(`/administration/hr/organization/teams/${team.id}`)}
                    >
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="font-medium">{team.name}</span>
                          <span className="text-muted-foreground text-xs">
                            {team.code}
                            {team.projectCode ? ` · ${team.projectCode}` : ''}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {teamTypeLabel(team.teamType)}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {team.teamLeadName ?? <span className="italic">No lead</span>}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {team.organizationUnitName ?? '—'}
                      </TableCell>
                      <TableCell className="text-right tabular-nums">
                        {team.memberCount}
                        {team.maxMembers ? (
                          <span className="text-muted-foreground"> / {team.maxMembers}</span>
                        ) : null}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-wrap items-center gap-1">
                          <Badge variant={team.status === 'Active' ? 'secondary' : 'outline'}>
                            {teamStatusLabel(team.status)}
                          </Badge>
                          {/* Lapsed is not a status — it is the end date having passed while the
                              status still says otherwise, which is worth showing side by side. */}
                          {team.hasLapsed && <Badge variant="outline">Lapsed</Badge>}
                        </div>
                      </TableCell>
                      <TableCell>
                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button
                              variant="ghost"
                              className="h-8 w-8 p-0"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <span className="sr-only">Open menu</span>
                              <MoreHorizontal className="h-4 w-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuLabel>Actions</DropdownMenuLabel>
                            <DropdownMenuItem
                              onClick={(e) => {
                                e.stopPropagation();
                                router.push(`/administration/hr/organization/teams/${team.id}`);
                              }}
                            >
                              <Pencil className="mr-2 h-4 w-4" /> Open
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive focus:text-destructive"
                              onClick={(e) => {
                                e.stopPropagation();
                                setDeleteTarget(team);
                              }}
                            >
                              <Trash2 className="mr-2 h-4 w-4" /> Dissolve
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-end space-x-2 py-4">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={!data.hasPrevious}
              >
                Previous
              </Button>
              <div className="text-muted-foreground text-sm">
                Page {data.page} of {data.totalPages}
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => p + 1)}
                disabled={!data.hasNext}
              >
                Next
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Dissolve team"
        description={
          deleteTarget
            ? `Dissolve "${deleteTarget.name}"? Its membership history is kept. A team that still has members or sub-teams cannot be dissolved.`
            : ''
        }
        confirmText="Dissolve"
        variant="destructive"
        isLoading={deleting}
        onConfirm={handleDelete}
      />
    </div>
  );
}
