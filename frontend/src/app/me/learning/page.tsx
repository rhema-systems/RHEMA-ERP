'use client';

/**
 * Area 25 slice 6 — My Learning Paths: spec destination #15, re-homed from
 * /hr/training/my-learning (D3).
 *
 * The old page carried desk tabs ("everyone" / "by employee") alongside the learner's own
 * view; those org-wide reads now live on the desk register at /hr/training/enrollments,
 * and this page is purely the learner's: token-derived /mine, no employee id anywhere.
 */

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { Route, CheckCircle2 } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { learningPathService } from '@/services/hr/learning-path.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function MyLearningPage() {
  const router = useRouter();

  const { data: mine, isLoading } = useQuery({
    queryKey: ['me', 'learning', 'enrollments', 'mine'],
    queryFn: () => learningPathService.getMyEnrollments(),
  });

  const rows = mine ?? [];
  const tiles = useMemo(
    () => [
      { label: 'My paths', value: rows.length, icon: Route },
      { label: 'In progress', value: rows.filter((e) => !e.isCompleted).length },
      { label: 'Completed', value: rows.filter((e) => e.isCompleted).length, icon: CheckCircle2 },
      {
        label: 'Average progress',
        value: rows.length
          ? `${Math.round(rows.reduce((a, e) => a + e.progressPercentage, 0) / rows.length)}%`
          : '—',
      },
    ],
    [rows],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="My Learning Paths"
        description="Curricula you are working through, step by step."
        backHref="/me"
      />

      <MetricTiles tiles={tiles} />

      <Card>
        <CardHeader>
          <CardTitle>Enrolments</CardTitle>
          <CardDescription>Open one to see its steps and what is unlocked next.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Path</TableHead>
                  <TableHead>Enrolled</TableHead>
                  <TableHead>Target</TableHead>
                  <TableHead className="w-[220px]">Progress</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(4)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[90px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={4}>
                      <EmptyState
                        icon={Route}
                        title="You are not on a path"
                        description="Learning paths you are enrolled on will appear here."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((e) => (
                    <TableRow
                      key={e.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/me/learning/${e.id}`)}
                    >
                      <TableCell className="font-medium">{e.learningPathName}</TableCell>
                      <TableCell className="text-muted-foreground">{fmt(e.enrolledDate)}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {fmt(e.targetCompletionDate)}
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-2">
                          <Progress value={e.progressPercentage} className="h-2" />
                          <span className="w-10 text-right text-xs text-muted-foreground">
                            {e.progressPercentage}%
                          </span>
                          {e.isCompleted && (
                            <Badge variant="default" className="text-[10px]">
                              Done
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
