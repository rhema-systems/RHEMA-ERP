'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { MessagesSquare, TriangleAlert } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { formatDate, formatDateTime, humanizeEnum } from '@/lib/hr/attendance-format';
import { appraisalConversationService } from '@/services/hr/conversations.service';

/**
 * Appraisal conversations — the scheduled meetings that punctuate a cycle.
 *
 * Two lists, because "conversations I owe someone" and "conversations about me" are different
 * jobs. Both come from routes that take the employee from the token, so neither needs an id.
 *
 * ⚠ The diary deliberately includes **overdue** conversations — the ones whose date has passed
 * and which were never held. Those are the rows that need action, so they are flagged rather than
 * filtered out.
 */
type Scope = 'diary' | 'mine';

export default function ConversationsPage() {
  const [scope, setScope] = useState<Scope>('diary');

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['hr', 'conversations', scope],
    queryFn: () =>
      scope === 'diary'
        ? appraisalConversationService.getMyDiary()
        : appraisalConversationService.getMine(),
    retry: false,
  });

  const rows = data ?? [];
  const now = Date.now();

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Conversations"
        description="Kick-off, quarterly, mid-year and final review meetings held against an appraisal — the agenda before, the notes after."
        backHref="/hr/performance"
      />

      <Tabs value={scope} onValueChange={(v) => setScope(v as Scope)}>
        <TabsList>
          <TabsTrigger value="diary">My diary</TabsTrigger>
          <TabsTrigger value="mine">About me</TabsTrigger>
        </TabsList>
      </Tabs>

      <Card>
        <CardContent className="p-0">
          {isError ? (
            <EmptyState
              icon={TriangleAlert}
              title="Could not load conversations"
              description={
                (error as Error)?.message ??
                'Your account may not be linked to an employee record.'
              }
            />
          ) : isLoading ? (
            <div className="space-y-2 p-4">
              {[0, 1].map((i) => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              icon={MessagesSquare}
              title={scope === 'diary' ? 'Nothing outstanding' : 'No conversations yet'}
              description={
                scope === 'diary'
                  ? 'Conversations you schedule from an appraisal appear here until they have been held.'
                  : 'Meetings your manager schedules against your appraisal appear here.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Conversation</TableHead>
                  <TableHead>{scope === 'diary' ? 'Appraisal' : 'Held by'}</TableHead>
                  <TableHead>Scheduled</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead className="w-24" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((row) => {
                  const overdue =
                    !row.isCompleted &&
                    !!row.scheduledDate &&
                    new Date(row.scheduledDate).getTime() < now;

                  return (
                    <TableRow key={row.id}>
                      <TableCell>
                        <div className="font-medium">{humanizeEnum(row.type)}</div>
                        {row.agenda && (
                          <div className="max-w-md truncate text-xs text-muted-foreground">
                            {row.agenda}
                          </div>
                        )}
                      </TableCell>
                      <TableCell className="text-sm">
                        {scope === 'diary'
                          ? (row.appraisalNumber ?? '—')
                          : (row.conductedByName ?? row.scheduledByName ?? '—')}
                      </TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        {formatDateTime(row.scheduledDate)}
                      </TableCell>
                      <TableCell>
                        {row.isCompleted ? (
                          <Badge variant="default">Held {formatDate(row.heldDate)}</Badge>
                        ) : overdue ? (
                          <Badge variant="destructive">Overdue</Badge>
                        ) : (
                          <Badge variant="secondary">Scheduled</Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button variant="ghost" size="sm" asChild>
                          <Link href={`/hr/performance/conversations/${row.id}`}>Open</Link>
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
