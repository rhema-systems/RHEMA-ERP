'use client';

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { BadgeCheck, Award } from 'lucide-react';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { TextareaField } from '@/components/hr/employee/tabs/fields';
import { trainingCompletionService } from '@/services/hr/training-completion.service';
import { TRAINING_COMPLETION_STATUS_OPTIONS } from '@/types/hr/training-delivery';
import type { TrainingCompletion } from '@/types/hr/training-delivery';

const statusLabel = (v: string) =>
  TRAINING_COMPLETION_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;

/**
 * Completions still awaiting a manager's verification.
 *
 * Verification is not a formality: a verified, passed completion writes the programme's target
 * skills onto the employee's profile, which is why the confirm copy says so.
 */
export default function TrainingCompletionsPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [target, setTarget] = useState<TrainingCompletion | null>(null);
  const [busy, setBusy] = useState(false);

  const queryKey = ['hr', 'training', 'completions', 'pending-verification'];
  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => trainingCompletionService.getPendingVerification(),
  });

  const form = useForm<{ verificationNotes: string }>({ defaultValues: { verificationNotes: '' } });

  const rows = data ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Completion Verification"
        description="Outcomes a manager still has to confirm. Verifying a pass adds the programme's skills to the employee's profile."
        backHref="/hr/training"
      />

      <Card>
        <CardHeader>
          <CardTitle>Awaiting verification</CardTitle>
          <CardDescription>Recorded by whoever ran the training, confirmed by the manager.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nomination</TableHead>
                  <TableHead>Employee</TableHead>
                  <TableHead>Programme</TableHead>
                  <TableHead>Completed</TableHead>
                  <TableHead>Score</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Result</TableHead>
                  <TableHead className="w-[110px]"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoading ? (
                  [...Array(3)].map((_, i) => (
                    <TableRow key={i}>
                      {[...Array(8)].map((__, j) => (
                        <TableCell key={j}>
                          <Skeleton className="h-4 w-[80px]" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : rows.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={8}>
                      <EmptyState
                        icon={Award}
                        title="Nothing to verify"
                        description="Completion records appear here once they are recorded against a nomination."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  rows.map((c) => (
                    <TableRow key={c.id}>
                      <TableCell className="font-mono text-xs">{c.nominationNumber}</TableCell>
                      <TableCell className="font-medium">
                        {c.employeeName}
                        <div className="text-xs text-muted-foreground">{c.employeeNumber}</div>
                      </TableCell>
                      <TableCell>{c.programName}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(c.completionDate).toLocaleDateString()}
                      </TableCell>
                      <TableCell>{typeof c.finalScore === 'number' ? c.finalScore : '—'}</TableCell>
                      <TableCell>
                        <StatusBadge status={statusLabel(c.status)} />
                      </TableCell>
                      <TableCell>
                        <Badge variant={c.isPassed ? 'default' : 'destructive'}>
                          {c.isPassed ? 'Passed' : 'Not passed'}
                        </Badge>
                      </TableCell>
                      <TableCell>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => {
                            form.reset({ verificationNotes: '' });
                            setTarget(c);
                          }}
                        >
                          <BadgeCheck className="mr-2 h-4 w-4" /> Verify
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
            <DialogTitle>Verify completion</DialogTitle>
            <DialogDescription>
              {target
                ? target.isPassed
                  ? `Confirming ${target.employeeName} passed "${target.programName}". The programme's target skills will be added to their profile.`
                  : `Confirming ${target.employeeName}'s record for "${target.programName}". They did not pass, so no skills are added.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <TextareaField form={form} name="verificationNotes" label="Notes" rows={3} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTarget(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                if (!target) return;
                setBusy(true);
                try {
                  await trainingCompletionService.verify(target.id, {
                    verificationNotes: form.getValues('verificationNotes') || null,
                  });
                  await queryClient.invalidateQueries({ queryKey });
                  toast({ title: 'Verified' });
                  setTarget(null);
                } catch (error: any) {
                  toast({
                    title: 'Error',
                    description: error?.message || 'Failed to verify.',
                    variant: 'destructive',
                  });
                } finally {
                  setBusy(false);
                }
              }}
            >
              Verify
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
