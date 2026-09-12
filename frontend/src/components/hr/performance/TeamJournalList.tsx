'use client';

import { useMemo, useState } from 'react';
import { BookLock, Eye, PenLine, Target, UserRound } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
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
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { formatDateTime } from '@/lib/hr/attendance-format';
import type { TeamJournalEntry } from '@/types/hr/journal';

/** Sentinel for the "everyone" option — Radix Select refuses an empty-string item value. */
const ALL = '__all__';

interface TeamJournalListProps {
  entries: TeamJournalEntry[];
  isLoading?: boolean;
}

/**
 * The manager's cross-team view: their own notes about reports, plus whatever those reports have
 * chosen to share.
 *
 * **Two provenances, visibly distinguished.** `isWrittenByManager` separates "what I observed" from
 * "what they told me", and conflating them would be misleading at appraisal time — a manager
 * re-reading this needs to know which claims are their own record and which are the employee's
 * account of themselves.
 *
 * ⚠ A `Private` badge here never means "someone else's private note leaked". The server only ever
 * returns the manager's *own* private notes; a report's private entries are not in this response at
 * all. The badge marks notes the subject cannot see.
 *
 * Filtering is done client-side from the loaded rows rather than by re-querying with an
 * `employeeId`: the endpoint rejects an id that is not a direct report, and the rows already carry
 * everyone the manager is entitled to.
 */
export function TeamJournalList({ entries, isLoading }: TeamJournalListProps) {
  const [subject, setSubject] = useState(ALL);
  const [source, setSource] = useState<'all' | 'mine' | 'theirs'>('all');

  const people = useMemo(() => {
    const seen = new Map<string, string>();
    for (const e of entries) seen.set(e.subjectEmployeeId, e.subjectEmployeeName);
    return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  }, [entries]);

  const filtered = useMemo(
    () =>
      entries.filter((e) => {
        if (subject !== ALL && e.subjectEmployeeId !== subject) return false;
        if (source === 'mine' && !e.isWrittenByManager) return false;
        if (source === 'theirs' && e.isWrittenByManager) return false;
        return true;
      }),
    [entries, subject, source],
  );

  if (isLoading) return <Skeleton className="h-48 w-full" />;

  if (entries.length === 0) {
    return (
      <Card>
        <CardContent className="p-0">
          <EmptyState
            icon={Eye}
            title="Nothing in the team journal"
            description="Notes you write about your reports appear here, along with anything they choose to share with you."
          />
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardContent className="flex flex-wrap items-end gap-4 p-4">
          <div className="min-w-[240px] flex-1 space-y-2">
            <Label htmlFor="team-journal-subject">Person</Label>
            <Select value={subject} onValueChange={setSubject}>
              <SelectTrigger id="team-journal-subject">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Everyone</SelectItem>
                {people.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <Tabs value={source} onValueChange={(v) => setSource(v as typeof source)}>
            <TabsList>
              <TabsTrigger value="all">All</TabsTrigger>
              <TabsTrigger value="mine">My notes</TabsTrigger>
              <TabsTrigger value="theirs">Shared with me</TabsTrigger>
            </TabsList>
          </Tabs>
        </CardContent>
      </Card>

      {filtered.length === 0 ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState title="Nothing matches those filters" />
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {filtered.map((e) => (
            <Card key={e.id}>
              <CardContent className="space-y-2 p-4">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="space-y-1">
                    <p className="font-medium leading-snug">{e.title}</p>
                    <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
                      <span className="inline-flex items-center gap-1">
                        <UserRound className="h-3 w-3" />
                        {e.subjectEmployeeName}
                      </span>
                      <span>·</span>
                      <span>{formatDateTime(e.entryDate)}</span>
                      <span>·</span>
                      <span>by {e.writtenByName}</span>
                    </p>
                  </div>
                  <div className="flex shrink-0 items-center gap-2">
                    {e.isWrittenByManager ? (
                      <Badge variant="outline" className="gap-1">
                        <PenLine className="h-3 w-3" />
                        My note
                      </Badge>
                    ) : (
                      <Badge variant="secondary" className="gap-1">
                        <Eye className="h-3 w-3" />
                        Shared with me
                      </Badge>
                    )}
                    {e.isPrivate && (
                      <Badge variant="outline" className="gap-1" title="Not visible to the subject">
                        <BookLock className="h-3 w-3" />
                        Private
                      </Badge>
                    )}
                  </div>
                </div>

                {e.relatedGoalTitle && (
                  <p className="flex items-center gap-2 text-xs text-muted-foreground">
                    <Target className="h-3 w-3" />
                    {e.relatedGoalTitle}
                  </p>
                )}

                <p className="whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">
                  {e.previewText}
                </p>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
