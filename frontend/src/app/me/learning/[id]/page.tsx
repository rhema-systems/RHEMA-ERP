'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Lock, CheckCircle2, Circle, RefreshCw, ChevronRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { learningPathService } from '@/services/hr/learning-path.service';
import type { EmployeeLearningPathStep } from '@/types/hr/learning-paths';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * One enrolment: the sequence, what is done, and what is still locked.
 *
 * A step is locked when its prerequisite is unfinished. That is computed here from the steps we
 * already hold rather than fetched per row — the prerequisite names another step in the same list,
 * so asking the server once per step would be N+1 for information already on screen.
 */
export default function EnrollmentDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [recalculating, setRecalculating] = useState(false);

  const queryKey = ['me', 'learning', 'enrollments', id];
  const { data: enrollment, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => learningPathService.getEnrollmentById(id),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !enrollment) {
    return (
      <div className="p-6">
        <EmptyState title="Enrolment not found" description="It may have been removed." />
      </div>
    );
  }

  const steps = enrollment.steps ?? [];
  const completedById = new Map(steps.map((s) => [s.learningPathProgramId, s.isCompleted]));
  const isLocked = (s: EmployeeLearningPathStep) =>
    !!s.prerequisitePathProgramId && completedById.get(s.prerequisitePathProgramId) !== true;

  const doneCount = steps.filter((s) => s.isCompleted).length;
  const nextStep = steps.find((s) => !s.isCompleted && !isLocked(s));

  const recalculate = async () => {
    setRecalculating(true);
    try {
      const updated = await learningPathService.recalculateProgress(id);
      await queryClient.invalidateQueries({ queryKey });
      toast({
        title: 'Progress recalculated',
        description: `Now ${updated.progressPercentage}%.`,
      });
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to recalculate.',
        variant: 'destructive',
      });
    } finally {
      setRecalculating(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={enrollment.learningPathName}
        description={`${enrollment.employeeName} · enrolled ${fmt(enrollment.enrolledDate)}`}
        backHref="/me/learning"
        actions={
          <Button variant="outline" size="sm" onClick={recalculate} disabled={recalculating}>
            {recalculating ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="mr-2 h-4 w-4" />
            )}
            Recalculate progress
          </Button>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Progress', value: `${enrollment.progressPercentage}%` },
          { label: 'Steps done', value: `${doneCount} of ${steps.length}` },
          { label: 'Target', value: fmt(enrollment.targetCompletionDate) },
          {
            label: 'Assigned by',
            value: enrollment.assignedByName ?? '—',
          },
        ]}
      />

      <Card>
        <CardContent className="py-4">
          <Progress value={enrollment.progressPercentage} className="h-2" />
          {nextStep && (
            <p className="mt-3 text-sm text-muted-foreground">
              Next up: <span className="font-medium text-foreground">{nextStep.programName}</span>
            </p>
          )}
          {!nextStep && !enrollment.isCompleted && steps.some((s) => !s.isCompleted) && (
            <p className="mt-3 text-sm text-amber-600">
              Everything still outstanding is waiting on a prerequisite.
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Steps</CardTitle>
          <CardDescription>
            In sequence. A locked step opens once the one it names is finished.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-2">
          {steps.length === 0 ? (
            <EmptyState
              icon={Circle}
              title="No steps"
              description="This enrolment has no steps, which means the path had no programmes when it was created."
            />
          ) : (
            steps.map((s) => {
              const locked = isLocked(s);
              return (
                <button
                  key={s.id}
                  type="button"
                  disabled={locked}
                  onClick={() => router.push(`/me/learning/${id}/steps/${s.id}`)}
                  className={`flex w-full items-center gap-3 rounded-md border p-3 text-left transition-colors ${
                    locked ? 'cursor-not-allowed opacity-60' : 'hover:bg-muted/50'
                  }`}
                >
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full border font-mono text-xs">
                    {s.sequenceOrder}
                  </span>
                  {s.isCompleted ? (
                    <CheckCircle2 className="h-5 w-5 shrink-0 text-green-600" />
                  ) : locked ? (
                    <Lock className="h-5 w-5 shrink-0 text-muted-foreground" />
                  ) : (
                    <Circle className="h-5 w-5 shrink-0 text-muted-foreground" />
                  )}
                  <span className="min-w-0 flex-1">
                    <span className="block font-medium">{s.programName}</span>
                    <span className="block text-xs text-muted-foreground">
                      {s.isCompleted
                        ? `Completed ${fmt(s.completedDate)}${
                            s.nominationNumber ? ` · ${s.nominationNumber}` : ''
                          }`
                        : locked
                          ? `Locked until "${s.prerequisiteProgramName}" is finished`
                          : 'Available now'}
                    </span>
                  </span>
                  {!s.isMandatory && (
                    <Badge variant="outline" className="shrink-0 text-[10px]">
                      Optional
                    </Badge>
                  )}
                  {!locked && <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />}
                </button>
              );
            })
          )}
        </CardContent>
      </Card>
    </div>
  );
}
