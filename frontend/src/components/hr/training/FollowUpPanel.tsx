'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { ClipboardPen, MessageSquarePlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { useToast } from '@/components/ui/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { TextareaField } from '@/components/hr/employee/tabs/fields';
import { trainingNominationService } from '@/services/hr/training-nomination.service';
import { TRAINING_ASSESSMENT_TYPE_OPTIONS } from '@/types/hr/training-delivery';
import type { TrainingFollowUpAssessment } from '@/types/hr/training-delivery';

const typeLabel = (v: string) =>
  TRAINING_ASSESSMENT_TYPE_OPTIONS.find((o) => o.value === v)?.label ?? v;

/**
 * Post-training assessments: did the learning actually get applied on the job?
 *
 * The employee's half is self-reported (submitted from their own screens); what a manager adds here
 * is the observation. Recording one stamps the manager from the token, which is why the Manager
 * column fills in on save rather than needing a refresh.
 */
export function FollowUpPanel({ scheduleId }: { scheduleId: string }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [target, setTarget] = useState<TrainingFollowUpAssessment | null>(null);
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'training', 'schedules', scheduleId, 'follow-up'];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => trainingNominationService.getFollowUps(scheduleId),
  });

  const form = useForm<{ managerObservationNotes: string }>({
    defaultValues: { managerObservationNotes: '' },
  });

  const submitObservation = form.handleSubmit(async (values) => {
    if (!target) return;
    setBusy(true);
    try {
      await trainingNominationService.submitManagerObservation(target.id, values.managerObservationNotes);
      await queryClient.invalidateQueries({ queryKey });
      toast({ title: 'Observation recorded' });
      form.reset({ managerObservationNotes: '' });
      setTarget(null);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to record the observation.',
        variant: 'destructive',
      });
    } finally {
      setBusy(false);
    }
  });

  const rows = data ?? [];

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>Follow-up assessments</CardTitle>
          <CardDescription>
            Whether the training stuck — the employee&apos;s own account, plus a manager&apos;s observation.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Assessed</TableHead>
                  <TableHead>Uses skills</TableHead>
                  <TableHead>Manager</TableHead>
                  <TableHead>Further training</TableHead>
                  <TableHead className="w-[70px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(7)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={7}>
                      <EmptyState
                        icon={ClipboardPen}
                        title="No follow-up assessments"
                        description="These are raised after the training, usually 30 to 90 days later."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((a) => (
                    <TableRow key={a.id}>
                      <TableCell className="font-medium">{a.employeeName}</TableCell>
                      <TableCell className="text-muted-foreground">{typeLabel(a.assessmentType)}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(a.assessmentDate).toLocaleDateString()}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {typeof a.frequencyOfUse === 'number' ? `${a.frequencyOfUse}/5` : '—'}
                      </TableCell>
                      <TableCell>
                        {a.managerName ? (
                          <span className="text-sm">
                            {a.managerName}
                            {a.managerSubmittedDate && (
                              <div className="text-xs text-muted-foreground">
                                {new Date(a.managerSubmittedDate).toLocaleDateString()}
                              </div>
                            )}
                          </span>
                        ) : (
                          <span className="text-xs text-amber-600">Not observed</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <Badge variant={a.recommendFurtherTraining ? 'secondary' : 'outline'}>
                          {a.recommendFurtherTraining ? 'Recommended' : 'No'}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => {
                            form.reset({ managerObservationNotes: a.managerObservationNotes ?? '' });
                            setTarget(a);
                          }}
                        >
                          <MessageSquarePlus className="h-4 w-4" />
                          <span className="sr-only">Add observation</span>
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={target !== null} onOpenChange={(o) => !o && setTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Manager observation</DialogTitle>
            <DialogDescription>
              {target
                ? `What have you seen from ${target.employeeName} since the training? You will be recorded as the observer.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3 py-2">
            {target?.howSkillsApplied && (
              <div className="rounded-md border bg-muted/50 p-3 text-sm">
                <p className="font-medium">Their own account</p>
                <p className="mt-1 text-muted-foreground">{target.howSkillsApplied}</p>
              </div>
            )}
            {target?.barriersToApplication && (
              <div className="rounded-md border bg-muted/50 p-3 text-sm">
                <p className="font-medium">Barriers they raised</p>
                <p className="mt-1 text-muted-foreground">{target.barriersToApplication}</p>
              </div>
            )}
            <TextareaField form={form} name="managerObservationNotes" label="Your observation" rows={4} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submitObservation} disabled={busy}>
              Record observation
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
