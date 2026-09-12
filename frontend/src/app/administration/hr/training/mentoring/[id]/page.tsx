'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Loader2, Pencil, Plus, Trash2, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { DateField, TextareaField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import {
  MentoringProgramForm,
  toMentoringProgramRequest,
  type MentoringProgramFormValues,
} from '@/components/hr/training/MentoringProgramForm';
import { mentoringService } from '@/services/hr/mentoring.service';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

interface PairFormValues {
  mentorId: string;
  menteeId: string;
  startDate: string;
  endDate: string;
  goals: string;
  focusAreas: string;
}

export default function MentoringProgramDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [editing, setEditing] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [pairOpen, setPairOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  const programKey = ['hr', 'mentoring', 'programs', id];
  const pairsKey = ['hr', 'mentoring', 'programs', id, 'pairs'];

  const { data: program, isLoading } = useQuery({
    queryKey: programKey,
    queryFn: () => mentoringService.getProgramById(id),
    enabled: !!id,
  });
  const { data: pairs } = useQuery({
    queryKey: pairsKey,
    queryFn: () => mentoringService.getPairsForProgram(id),
    enabled: !!id,
  });

  const pairForm = useForm<PairFormValues>({
    defaultValues: {
      mentorId: '',
      menteeId: '',
      startDate: new Date().toISOString().slice(0, 10),
      endDate: '',
      goals: '',
      focusAreas: '',
    },
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!program) {
    return (
      <div className="p-6">
        <EmptyState title="Programme not found" description="It may have been removed." />
      </div>
    );
  }

  const saveProgram = async (values: MentoringProgramFormValues) => {
    setSubmitting(true);
    try {
      await mentoringService.updateProgram(id, toMentoringProgramRequest(values));
      await queryClient.invalidateQueries({ queryKey: ['hr', 'mentoring', 'programs'] });
      toast({ title: 'Saved' });
      setEditing(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to save.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const createPair = async () => {
    const v = pairForm.getValues();
    if (!v.mentorId || !v.menteeId) {
      toast({
        title: 'Both people are required',
        description: 'Pick a mentor and a mentee.',
        variant: 'destructive',
      });
      return;
    }
    if (v.mentorId === v.menteeId) {
      toast({
        title: 'Pick two different people',
        description: 'Someone cannot mentor themselves.',
        variant: 'destructive',
      });
      return;
    }

    setSubmitting(true);
    try {
      await mentoringService.createPair({
        programId: id,
        mentorId: v.mentorId,
        menteeId: v.menteeId,
        startDate: new Date(v.startDate).toISOString(),
        endDate: v.endDate ? new Date(v.endDate).toISOString() : null,
        goals: v.goals || null,
        focusAreas: v.focusAreas || null,
      });
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: pairsKey }),
        queryClient.invalidateQueries({ queryKey: programKey }),
      ]);
      toast({ title: 'Pair created' });
      pairForm.reset();
      setPairOpen(false);
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to create the pair.',
        variant: 'destructive',
      });
    } finally {
      setSubmitting(false);
    }
  };

  const removeProgram = async (): Promise<boolean> => {
    try {
      await mentoringService.deleteProgram(id);
      await queryClient.invalidateQueries({ queryKey: ['hr', 'mentoring', 'programs'] });
      toast({ title: 'Programme deleted' });
      router.push('/administration/hr/training/mentoring');
      return true;
    } catch (error: any) {
      toast({
        title: 'Error',
        description: error?.message || 'Failed to delete the programme.',
        variant: 'destructive',
      });
      return false;
    }
  };

  if (editing) {
    return (
      <div className="space-y-6 p-6">
        <PageHeader
          title={`Edit ${program.programName}`}
          backHref={`/administration/hr/training/mentoring/${id}`}
        />
        <MentoringProgramForm
          defaultValues={{
            programName: program.programName,
            description: program.description ?? '',
            objectives: program.objectives ?? '',
            startDate: program.startDate.slice(0, 10),
            endDate: program.endDate ? program.endDate.slice(0, 10) : '',
            sessionsPerMonth: program.sessionsPerMonth?.toString() ?? '',
            minutesPerSession: program.minutesPerSession?.toString() ?? '',
            isActive: program.isActive,
            coordinatedById: program.coordinatedById ?? '',
          }}
          onSubmit={saveProgram}
          submitting={submitting}
          submitLabel="Save changes"
          onCancel={() => setEditing(false)}
        />
      </div>
    );
  }

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={program.programName}
        description={program.coordinatedByName ? `Coordinated by ${program.coordinatedByName}` : undefined}
        backHref="/administration/hr/training/mentoring"
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
              <Pencil className="mr-2 h-4 w-4" /> Edit
            </Button>
            <Button size="sm" onClick={() => setPairOpen(true)}>
              <Plus className="mr-2 h-4 w-4" /> Add pair
            </Button>
            <Button variant="outline" size="sm" onClick={() => setDeleteOpen(true)}>
              <Trash2 className="mr-2 h-4 w-4" /> Delete
            </Button>
          </div>
        }
      />

      <MetricTiles
        tiles={[
          { label: 'Pairs', value: program.totalPairsCount, icon: UserCheck },
          { label: 'Active pairs', value: program.activePairsCount },
          { label: 'Runs', value: `${fmt(program.startDate)} – ${fmt(program.endDate)}` },
          {
            label: 'Cadence',
            value: program.sessionsPerMonth
              ? `${program.sessionsPerMonth}/month · ${program.minutesPerSession ?? '—'}m`
              : '—',
          },
        ]}
      />

      {(program.description || program.objectives) && (
        <Card>
          <CardHeader>
            <CardTitle>About</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            {program.description && (
              <div>
                <p className="text-sm text-muted-foreground">Description</p>
                <p className="font-medium">{program.description}</p>
              </div>
            )}
            {program.objectives && (
              <div>
                <p className="text-sm text-muted-foreground">Objectives</p>
                <p className="font-medium">{program.objectives}</p>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle>Pairs</CardTitle>
          <CardDescription>
            Open one to see its session log. Private notes stay with whoever wrote them — a
            coordinator sees that sessions happened, not what was said in them.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Mentor</TableHead>
                  <TableHead>Mentee</TableHead>
                  <TableHead>Started</TableHead>
                  <TableHead className="text-right">Sessions</TableHead>
                  <TableHead>Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(pairs ?? []).length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5}>
                      <EmptyState
                        icon={UserCheck}
                        title="No pairs yet"
                        description="Add a mentor and mentee to start the scheme."
                      />
                    </TableCell>
                  </TableRow>
                ) : (
                  (pairs ?? []).map((p) => (
                    <TableRow
                      key={p.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => router.push(`/me/mentoring/${p.id}`)}
                    >
                      <TableCell className="font-medium">{p.mentorName}</TableCell>
                      <TableCell>{p.menteeName}</TableCell>
                      <TableCell className="text-muted-foreground">{fmt(p.startDate)}</TableCell>
                      <TableCell className="text-right">{p.totalSessionsCount}</TableCell>
                      <TableCell>
                        <StatusBadge status={p.statusName} />
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      <Dialog open={pairOpen} onOpenChange={setPairOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Add a pair</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <EmployeePickerField form={pairForm} name="mentorId" label="Mentor" required />
            <EmployeePickerField form={pairForm} name="menteeId" label="Mentee" required />
            <FieldRow>
              <DateField form={pairForm} name="startDate" label="Start date" required />
              <DateField form={pairForm} name="endDate" label="Expected end date" />
            </FieldRow>
            <TextareaField form={pairForm} name="goals" label="Goals" rows={3} />
            <TextareaField form={pairForm} name="focusAreas" label="Focus areas" rows={2} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPairOpen(false)} disabled={submitting}>
              Cancel
            </Button>
            <Button onClick={createPair} disabled={submitting}>
              {submitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Create pair
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete this programme?"
        description={
          program.totalPairsCount > 0
            ? `This programme has ${program.totalPairsCount} pair(s). The server will refuse if they must be kept.`
            : 'This cannot be undone.'
        }
        confirmText="Delete"
        variant="destructive"
        onConfirm={removeProgram}
      />
    </div>
  );
}
