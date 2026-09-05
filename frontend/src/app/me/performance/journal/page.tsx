'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookLock, Eye, NotebookPen, Plus, Users } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { CycleSelect } from '@/components/hr/performance/CycleSelect';
import { JournalEntryCard } from '@/components/hr/performance/JournalEntryCard';
import { JournalEntryDialog } from '@/components/hr/performance/JournalEntryDialog';
import { TeamJournalList } from '@/components/hr/performance/TeamJournalList';
import { useToast } from '@/hooks/use-toast';
import { employeeService } from '@/services/hr/employee.service';
import { performanceJournalService } from '@/services/hr/journal.service';
import type { PerformanceJournalEntry } from '@/types/hr/journal';

/**
 * The performance journal — evidence gathered as it happens, so the year-end appraisal is not
 * written from memory.
 *
 * Three views, because there are genuinely three jobs:
 *   • **My journal** — my own notes, private by default. Nobody else can read a private one,
 *     including HR; sharing is a deliberate act, per entry.
 *   • **About my team** — the notes I keep on one report. Mine, so I see my private ones here.
 *   • **Team journal** — everything visible to me as a manager: my notes about reports, plus
 *     whatever they have chosen to share. Previews only; it is for scanning.
 *
 * ⚠ Nothing on this screen sends an employee id for the caller — every route derives it from the
 * token. Keep it that way: the id-bearing versions of these routes were how anyone could read
 * anyone's private journal.
 */
export default function PerformanceJournalPage() {
  const [tab, setTab] = useState('mine');
  const [cycleId, setCycleId] = useState('');
  const [subjectId, setSubjectId] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<PerformanceJournalEntry | null>(null);

  const queryClient = useQueryClient();
  const { toast } = useToast();

  const { data: mine, isLoading: mineLoading } = useQuery({
    queryKey: ['hr', 'journal', 'mine', cycleId],
    queryFn: () => performanceJournalService.getMine(cycleId || undefined),
  });

  // Direct reports drive the "About my team" picker. Managers only — an individual contributor
  // gets an empty list and the tab says so rather than erroring.
  const { data: reports } = useQuery({
    queryKey: ['hr', 'my-direct-reports'],
    queryFn: () => employeeService.getMyDirectReports(),
    staleTime: 5 * 60 * 1000,
    retry: false,
  });

  const { data: about, isLoading: aboutLoading } = useQuery({
    queryKey: ['hr', 'journal', 'about', subjectId, cycleId],
    queryFn: () => performanceJournalService.getAbout(subjectId, cycleId || undefined),
    enabled: !!subjectId,
  });

  const { data: team, isLoading: teamLoading } = useQuery({
    queryKey: ['hr', 'journal', 'team', cycleId],
    queryFn: () =>
      performanceJournalService.getTeam(cycleId ? { appraisalCycleId: cycleId } : {}),
    retry: false,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'journal'] });

  const failed = (title: string) => (err: unknown) =>
    toast({
      variant: 'destructive',
      title,
      description: (err as Error)?.message ?? 'Please try again.',
    });

  const setPrivacy = useMutation({
    mutationFn: ({ id, isPrivate }: { id: string; isPrivate: boolean }) =>
      performanceJournalService.setPrivacy(id, isPrivate),
    onSuccess: (_r, { isPrivate }) => {
      refresh();
      toast({
        title: isPrivate ? 'Entry is private again' : 'Entry shared with your manager',
        description: isPrivate
          ? 'Only you can read it.'
          : 'Your line manager and HR can now read it.',
      });
    },
    onError: failed('Could not change the privacy of this entry'),
  });

  const remove = useMutation({
    mutationFn: (id: string) => performanceJournalService.delete(id),
    onSuccess: () => {
      refresh();
      toast({ title: 'Entry deleted' });
    },
    onError: failed('Could not delete the entry'),
  });

  const reportOptions = useMemo(
    () => (reports ?? []).map((r) => ({ id: r.id, name: r.fullName ?? r.employeeNumber ?? r.id })),
    [reports],
  );

  const openNew = () => {
    setEditing(null);
    setDialogOpen(true);
  };

  const openEdit = (entry: PerformanceJournalEntry) => {
    setEditing(entry);
    setDialogOpen(true);
  };

  const privateCount = (mine ?? []).filter((e) => e.isPrivate).length;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Performance journal"
        description="Evidence recorded as it happens, so the year-end appraisal is not written from memory."
        actions={
          <Button onClick={openNew}>
            <Plus className="mr-2 h-4 w-4" />
            New entry
          </Button>
        }
      />

      <CycleSelect value={cycleId} onChange={setCycleId} allowAll />

      <Tabs value={tab} onValueChange={setTab}>
        <TabsList>
          <TabsTrigger value="mine">
            <NotebookPen className="mr-2 h-4 w-4" />
            My journal
          </TabsTrigger>
          <TabsTrigger value="about">
            <Users className="mr-2 h-4 w-4" />
            About my team
          </TabsTrigger>
          <TabsTrigger value="team">
            <Eye className="mr-2 h-4 w-4" />
            Team journal
          </TabsTrigger>
        </TabsList>

        {/* ── My journal ────────────────────────────────────────────────────── */}
        <TabsContent value="mine" className="space-y-4 pt-4">
          <Alert>
            <BookLock className="h-4 w-4" />
            <AlertTitle>Private entries are yours alone</AlertTitle>
            <AlertDescription>
              An entry marked private cannot be read by your line manager or by HR. Sharing one is
              a deliberate act, and you can take it back at any time.
              {privateCount > 0 && (
                <>
                  {' '}
                  <span className="font-medium">
                    {privateCount} of {(mine ?? []).length} entries
                  </span>{' '}
                  {privateCount === 1 ? 'is' : 'are'} private.
                </>
              )}
            </AlertDescription>
          </Alert>

          {mineLoading ? (
            <Skeleton className="h-48 w-full" />
          ) : (mine ?? []).length === 0 ? (
            <Card>
              <CardContent className="p-0">
                <EmptyState
                  icon={NotebookPen}
                  title="Nothing in your journal yet"
                  description="Note things down as they happen — wins, difficulties, feedback you were given."
                  action={
                    <Button size="sm" onClick={openNew}>
                      <Plus className="mr-2 h-4 w-4" />
                      New entry
                    </Button>
                  }
                />
              </CardContent>
            </Card>
          ) : (
            <div className="space-y-3">
              {(mine ?? []).map((entry) => (
                <JournalEntryCard
                  key={entry.id}
                  entry={entry}
                  onEdit={() => openEdit(entry)}
                  onDelete={() => remove.mutate(entry.id)}
                  onTogglePrivacy={() =>
                    setPrivacy.mutate({ id: entry.id, isPrivate: !entry.isPrivate })
                  }
                />
              ))}
            </div>
          )}
        </TabsContent>

        {/* ── About my team ─────────────────────────────────────────────────── */}
        <TabsContent value="about" className="space-y-4 pt-4">
          {reportOptions.length === 0 ? (
            <Card>
              <CardContent className="p-0">
                <EmptyState
                  icon={Users}
                  title="Nobody reports to you"
                  description="This view is for the notes a manager keeps about their direct reports."
                />
              </CardContent>
            </Card>
          ) : (
            <>
              <Card>
                <CardContent className="flex flex-wrap items-end gap-4 p-4">
                  <div className="min-w-[280px] flex-1 space-y-2">
                    <Label htmlFor="subject">Direct report</Label>
                    <Select value={subjectId} onValueChange={setSubjectId}>
                      <SelectTrigger id="subject">
                        <SelectValue placeholder="Choose someone…" />
                      </SelectTrigger>
                      <SelectContent>
                        {reportOptions.map((r) => (
                          <SelectItem key={r.id} value={r.id}>
                            {r.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </CardContent>
              </Card>

              {!subjectId ? (
                <Card>
                  <CardContent className="p-0">
                    <EmptyState
                      title="Choose a direct report"
                      description="Their notes — the ones you wrote — will appear here."
                    />
                  </CardContent>
                </Card>
              ) : aboutLoading ? (
                <Skeleton className="h-48 w-full" />
              ) : (about ?? []).length === 0 ? (
                <Card>
                  <CardContent className="p-0">
                    <EmptyState
                      icon={NotebookPen}
                      title="No notes about this person yet"
                      description="Record what you observe as it happens — it is what the year-end conversation is built on."
                      action={
                        <Button size="sm" onClick={openNew}>
                          <Plus className="mr-2 h-4 w-4" />
                          New entry
                        </Button>
                      }
                    />
                  </CardContent>
                </Card>
              ) : (
                <div className="space-y-3">
                  {/* These are the caller's own entries, so private ones appear — they wrote them. */}
                  {(about ?? []).map((entry) => (
                    <JournalEntryCard
                      key={entry.id}
                      entry={entry}
                      onEdit={() => openEdit(entry)}
                      onDelete={() => remove.mutate(entry.id)}
                      onTogglePrivacy={() =>
                        setPrivacy.mutate({ id: entry.id, isPrivate: !entry.isPrivate })
                      }
                    />
                  ))}
                </div>
              )}
            </>
          )}
        </TabsContent>

        {/* ── Team journal ──────────────────────────────────────────────────── */}
        <TabsContent value="team" className="space-y-4 pt-4">
          <TeamJournalList
            entries={team ?? []}
            isLoading={teamLoading}
          />
        </TabsContent>
      </Tabs>

      <JournalEntryDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        entry={editing}
        defaultCycleId={cycleId}
        defaultSubjectId={tab === 'about' ? subjectId : ''}
        reportOptions={reportOptions}
        onSaved={refresh}
      />
    </div>
  );
}
