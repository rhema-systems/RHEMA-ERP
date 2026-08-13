'use client';

import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { MessageSquare, Star } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { trainingNominationService } from '@/services/hr/training-nomination.service';

const avg = (values: (number | null | undefined)[]) => {
  const nums = values.filter((v): v is number => typeof v === 'number');
  if (!nums.length) return null;
  return Math.round((nums.reduce((a, b) => a + b, 0) / nums.length) * 10) / 10;
};

function Rating({ value }: { value?: number | null }) {
  if (typeof value !== 'number') return <span className="text-muted-foreground">—</span>;
  return (
    <span className="inline-flex items-center gap-1">
      <Star className="h-3.5 w-3.5 fill-amber-400 text-amber-400" />
      {value}
    </span>
  );
}

/**
 * Reaction feedback for a delivered run — read-only here: it is submitted by attendees, not entered
 * by HR. The trainer-knowledge column is called out because it is the one that feeds the trainer's
 * running average rating; the others describe the venue, materials and content.
 */
export function FeedbackPanel({ scheduleId }: { scheduleId: string }) {
  const { data, isLoading } = useQuery({
    queryKey: ['hr', 'training', 'schedules', scheduleId, 'feedback'],
    queryFn: () => trainingNominationService.getFeedback(scheduleId),
  });

  const rows = data ?? [];

  const tiles = useMemo(() => {
    const recommend = rows.filter((f) => f.wouldRecommend).length;
    return [
      { label: 'Responses', value: rows.length },
      { label: 'Overall', value: avg(rows.map((f) => f.overallSatisfactionRating)) ?? '—' },
      { label: 'Trainer', value: avg(rows.map((f) => f.trainerKnowledgeRating)) ?? '—' },
      {
        label: 'Would recommend',
        value: rows.length ? `${Math.round((recommend / rows.length) * 100)}%` : '—',
      },
    ];
  }, [rows]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Feedback</CardTitle>
        <CardDescription>
          Submitted by attendees. The trainer score is what feeds that trainer&apos;s overall rating.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {rows.length > 0 && <MetricTiles tiles={tiles} />}

        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Respondent</TableHead>
                <TableHead>Content</TableHead>
                <TableHead>Trainer</TableHead>
                <TableHead>Delivery</TableHead>
                <TableHead>Venue</TableHead>
                <TableHead>Overall</TableHead>
                <TableHead>Recommends</TableHead>
                <TableHead>Submitted</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                [...Array(3)].map((_, i) => (
                  <TableRow key={i}>
                    {[...Array(8)].map((__, j) => (
                      <TableCell key={j}>
                        <Skeleton className="h-4 w-[70px]" />
                      </TableCell>
                    ))}
                  </TableRow>
                ))
              ) : rows.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={8}>
                    <EmptyState
                      icon={MessageSquare}
                      title="No feedback yet"
                      description="Attendees submit this after the training has run."
                    />
                  </TableCell>
                </TableRow>
              ) : (
                rows.map((f) => (
                  <TableRow key={f.id}>
                    <TableCell className="font-medium">{f.employeeName}</TableCell>
                    <TableCell><Rating value={f.contentRelevanceRating} /></TableCell>
                    <TableCell><Rating value={f.trainerKnowledgeRating} /></TableCell>
                    <TableCell><Rating value={f.deliveryMethodRating} /></TableCell>
                    <TableCell><Rating value={f.venueFacilitiesRating} /></TableCell>
                    <TableCell><Rating value={f.overallSatisfactionRating} /></TableCell>
                    <TableCell>
                      <Badge variant={f.wouldRecommend ? 'default' : 'outline'}>
                        {f.wouldRecommend ? 'Yes' : 'No'}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {new Date(f.feedbackDate).toLocaleDateString()}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      </CardContent>
    </Card>
  );
}
