'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { Loader2, Plus, Trash2, UserPlus, Route, Lock, Users, Target } from 'lucide-react';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/components/ui/use-toast';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { MetricTiles } from '@/components/hr/common/MetricTiles';
import { EmployeePickerField } from '@/components/hr/attendance/EmployeePickerField';
import { SelectField, NumberField, SwitchField, TextareaField, DateField, FieldRow } from '@/components/hr/employee/tabs/fields';
import {
  LearningPathForm,
  toLearningPathRequest,
  type LearningPathFormValues,
} from '@/components/hr/training/LearningPathForm';
import { learningPathService } from '@/services/hr/learning-path.service';
import { trainingProgramService } from '@/services/hr/training-program.service';
import { skillService } from '@/services/hr/skill.service';
import { LEARNING_PATH_STATUS_OPTIONS } from '@/types/hr/learning-paths';
import { PROFICIENCY_LEVEL_OPTIONS } from '@/types/hr/training';
import type { LearningPathProgram, LearningPathSkill } from '@/types/hr/learning-paths';

const statusLabel = (v: string) =>
  LEARNING_PATH_STATUS_OPTIONS.find((o) => o.value === v)?.label ?? v;
const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

export default function LearningPathDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = (params?.id as string) ?? '';
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [saving, setSaving] = useState(false);
  const [busy, setBusy] = useState(false);
  const [addProgramOpen, setAddProgramOpen] = useState(false);
  const [addSkillOpen, setAddSkillOpen] = useState(false);
  const [enrollOpen, setEnrollOpen] = useState(false);
  const [removeProgram, setRemoveProgram] = useState<LearningPathProgram | null>(null);
  const [removeSkill, setRemoveSkill] = useState<LearningPathSkill | null>(null);

  const pathKey = ['hr', 'training', 'learning-paths', id];
  const enrolKey = ['hr', 'training', 'learning-paths', id, 'enrollments'];

  const { data: path, isLoading, isError } = useQuery({
    queryKey: pathKey,
    queryFn: () => learningPathService.getById(id),
    enabled: !!id,
  });
  const { data: enrollments, isLoading: loadingEnrol } = useQuery({
    queryKey: enrolKey,
    queryFn: () => learningPathService.getEnrollmentsForPath(id),
    enabled: !!id,
  });
  const { data: programs } = useQuery({
    queryKey: ['hr', 'training', 'programs', 'active'],
    queryFn: () => trainingProgramService.getActive(),
  });
  const { data: skills } = useQuery({
    queryKey: ['hr', 'skills'],
    queryFn: () => skillService.getAll(),
  });

  const programForm = useForm<{
    programId: string;
    sequenceOrder: string;
    isMandatory: boolean;
    prerequisitePathProgramId: string;
    notes: string;
  }>({
    defaultValues: {
      programId: '',
      sequenceOrder: '1',
      isMandatory: true,
      prerequisitePathProgramId: '',
      notes: '',
    },
  });
  const skillForm = useForm<{ skillId: string; targetProficiency: string }>({
    defaultValues: { skillId: '', targetProficiency: 'Proficient' },
  });
  const enrollForm = useForm<{ employeeId: string; targetCompletionDate: string }>({
    defaultValues: { employeeId: '', targetCompletionDate: '' },
  });

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: pathKey }),
      queryClient.invalidateQueries({ queryKey: enrolKey }),
      queryClient.invalidateQueries({ queryKey: ['hr', 'training', 'learning-paths'] }),
    ]);

  const handleSave = async (values: LearningPathFormValues) => {
    setSaving(true);
    try {
      await learningPathService.update(id, toLearningPathRequest(values));
      await invalidate();
      toast({ title: 'Saved' });
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || 'Failed to save.', variant: 'destructive' });
    } finally {
      setSaving(false);
    }
  };

  const run = async (label: string, fn: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await fn();
      await invalidate();
      toast({ title: label });
      return true;
    } catch (error: any) {
      toast({ title: 'Error', description: error?.message || `${label} failed.`, variant: 'destructive' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !path) {
    return (
      <div className="p-6">
        <EmptyState title="Learning path not found" description="It may have been removed." />
      </div>
    );
  }

  const programOptions = (programs ?? [])
    // A programme already in the sequence should not be offered twice.
    .filter((p) => !path.programs.some((existing) => existing.programId === p.id))
    .map((p) => ({ value: p.id, label: `${p.programCode} — ${p.programName}` }));
  const skillOptions = (skills ?? [])
    .filter((s: any) => !path.targetSkills.some((existing) => existing.skillId === s.id))
    .map((s: any) => ({ value: s.id, label: s.name }));
  // Only earlier steps can gate a later one — offering the whole list invites a cycle.
  const prerequisiteOptions = path.programs.map((p) => ({
    value: p.id,
    label: `${p.sequenceOrder}. ${p.programName}`,
  }));

  const enrolRows = enrollments ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={path.name}
        description={path.description}
        backHref="/administration/hr/training/learning-paths"
        actions={
          <div className="flex items-center gap-2">
            <StatusBadge status={statusLabel(path.status)} />
            <Button size="sm" onClick={() => setEnrollOpen(true)} disabled={path.totalProgramsCount === 0}>
              <UserPlus className="mr-2 h-4 w-4" /> Enrol someone
            </Button>
          </div>
        }
      />

      <MetricTiles
        tiles={[
          {
            label: 'Programmes',
            value: path.totalProgramsCount,
            hint: path.totalProgramsCount === 0 ? 'Nothing to enrol onto yet' : undefined,
            icon: Route,
            tone: path.totalProgramsCount === 0 ? 'warning' : 'default',
          },
          { label: 'Enrolled', value: path.enrollmentsCount, icon: Users },
          { label: 'Target skills', value: path.targetSkills.length, icon: Target },
          {
            label: 'Estimated',
            value: path.estimatedDurationDays ? `${path.estimatedDurationDays} days` : '—',
          },
        ]}
      />

      <Tabs defaultValue="sequence">
        <TabsList>
          <TabsTrigger value="sequence">Sequence ({path.programs.length})</TabsTrigger>
          <TabsTrigger value="skills">Target skills ({path.targetSkills.length})</TabsTrigger>
          <TabsTrigger value="enrollments">Enrolments ({enrolRows.length})</TabsTrigger>
          <TabsTrigger value="settings">Settings</TabsTrigger>
        </TabsList>

        <TabsContent value="sequence" className="pt-4">
          <Card>
            <CardHeader>
              <div className="flex items-start justify-between gap-4">
                <div>
                  <CardTitle>Programme sequence</CardTitle>
                  <CardDescription>
                    The order people work through. A prerequisite locks a step until the one it names
                    is finished.
                  </CardDescription>
                </div>
                <Button
                  size="sm"
                  onClick={() => {
                    programForm.reset({
                      programId: '',
                      sequenceOrder: String(path.programs.length + 1),
                      isMandatory: true,
                      prerequisitePathProgramId: '',
                      notes: '',
                    });
                    setAddProgramOpen(true);
                  }}
                >
                  <Plus className="mr-2 h-4 w-4" /> Add programme
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-[60px]">#</TableHead>
                      <TableHead>Programme</TableHead>
                      <TableHead>Unlocked by</TableHead>
                      <TableHead>Mandatory</TableHead>
                      <TableHead className="w-[70px]"></TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {path.programs.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={5}>
                          <EmptyState
                            icon={Route}
                            title="No programmes yet"
                            description="Add the programmes that make up this path, in the order they should be taken."
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      path.programs.map((p) => (
                        <TableRow key={p.id}>
                          <TableCell className="font-mono text-xs">{p.sequenceOrder}</TableCell>
                          <TableCell>
                            <div className="font-medium">{p.programName}</div>
                            <div className="font-mono text-[11px] text-muted-foreground">
                              {p.programCode}
                            </div>
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {p.prerequisiteProgramName ? (
                              <span className="inline-flex items-center gap-1.5 text-sm">
                                <Lock className="h-3.5 w-3.5" />
                                {p.prerequisiteProgramName}
                              </span>
                            ) : (
                              <span className="text-xs">Available from the start</span>
                            )}
                          </TableCell>
                          <TableCell>
                            {p.isMandatory ? (
                              <Badge variant="secondary" className="text-[10px]">
                                Mandatory
                              </Badge>
                            ) : (
                              <span className="text-muted-foreground">Optional</span>
                            )}
                          </TableCell>
                          <TableCell>
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-destructive"
                              onClick={() => setRemoveProgram(p)}
                            >
                              <Trash2 className="h-4 w-4" />
                              <span className="sr-only">Remove</span>
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
        </TabsContent>

        <TabsContent value="skills" className="pt-4">
          <Card>
            <CardHeader>
              <div className="flex items-start justify-between gap-4">
                <div>
                  <CardTitle>Target skills</CardTitle>
                  <CardDescription>What somebody should be able to do once they finish.</CardDescription>
                </div>
                <Button
                  size="sm"
                  onClick={() => {
                    skillForm.reset({ skillId: '', targetProficiency: 'Proficient' });
                    setAddSkillOpen(true);
                  }}
                >
                  <Plus className="mr-2 h-4 w-4" /> Add skill
                </Button>
              </div>
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Skill</TableHead>
                      <TableHead>Target level</TableHead>
                      <TableHead className="w-[70px]"></TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {path.targetSkills.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={3}>
                          <EmptyState
                            icon={Target}
                            title="No target skills"
                            description="Optional, but it is what makes the path's purpose explicit."
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      path.targetSkills.map((s) => (
                        <TableRow key={s.id}>
                          <TableCell className="font-medium">{s.skillName}</TableCell>
                          <TableCell className="text-muted-foreground">
                            {PROFICIENCY_LEVEL_OPTIONS.find((o) => o.value === s.targetProficiency)
                              ?.label ?? s.targetProficiency}
                          </TableCell>
                          <TableCell>
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-destructive"
                              onClick={() => setRemoveSkill(s)}
                            >
                              <Trash2 className="h-4 w-4" />
                              <span className="sr-only">Remove</span>
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
        </TabsContent>

        <TabsContent value="enrollments" className="pt-4">
          <Card>
            <CardHeader>
              <CardTitle>Enrolments</CardTitle>
              <CardDescription>Who is working through this path, and how far along.</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Enrolled</TableHead>
                      <TableHead>Target</TableHead>
                      <TableHead className="w-[200px]">Progress</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {loadingEnrol ? (
                      [...Array(3)].map((_, i) => (
                        <TableRow key={i}>
                          {[...Array(4)].map((__, j) => (
                            <TableCell key={j}>
                              <Skeleton className="h-4 w-[90px]" />
                            </TableCell>
                          ))}
                        </TableRow>
                      ))
                    ) : enrolRows.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={4}>
                          <EmptyState
                            icon={Users}
                            title="Nobody enrolled"
                            description={
                              path.totalProgramsCount === 0
                                ? 'Add programmes to the sequence first — an empty path has nothing to work through.'
                                : 'Enrol someone to start them on this path.'
                            }
                          />
                        </TableCell>
                      </TableRow>
                    ) : (
                      enrolRows.map((e) => (
                        <TableRow
                          key={e.id}
                          className="cursor-pointer hover:bg-muted/50"
                          onClick={() => router.push(`/me/learning/${e.id}`)}
                        >
                          <TableCell className="font-medium">{e.employeeName}</TableCell>
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
        </TabsContent>

        <TabsContent value="settings" className="pt-4">
          <LearningPathForm
            defaultValues={{
              name: path.name,
              description: path.description,
              organizationLevelId: path.organizationLevelId ?? '',
              organizationUnitId: path.organizationUnitId ?? '',
              positionId: path.positionId ?? '',
              status: path.status,
              estimatedDurationDays: path.estimatedDurationDays?.toString() ?? '',
              estimatedDurationHours: path.estimatedDurationHours?.toString() ?? '',
              providesCertificate: path.providesCertificate,
              completionCertificateName: path.completionCertificateName ?? '',
            }}
            onSubmit={handleSave}
            submitting={saving}
            submitLabel="Save changes"
            onCancel={() => router.push('/administration/hr/training/learning-paths')}
          />
        </TabsContent>
      </Tabs>

      <Dialog open={addProgramOpen} onOpenChange={setAddProgramOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a programme to the sequence</DialogTitle>
            <DialogDescription>
              Programmes already in this path are not offered again.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <SelectField form={programForm} name="programId" label="Programme" required options={programOptions} />
            <FieldRow>
              <NumberField form={programForm} name="sequenceOrder" label="Position in sequence" required />
              <SelectField
                form={programForm}
                name="prerequisitePathProgramId"
                label="Unlocked by"
                options={prerequisiteOptions}
                allowEmpty
                emptyLabel="Available from the start"
              />
            </FieldRow>
            <SwitchField form={programForm} name="isMandatory" label="Mandatory" />
            <TextareaField form={programForm} name="notes" label="Notes" rows={2} />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddProgramOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                const v = programForm.getValues();
                if (!v.programId) {
                  toast({ title: 'Pick a programme', variant: 'destructive' });
                  return;
                }
                const ok = await run('Added to the sequence', () =>
                  learningPathService.addProgram(id, {
                    programId: v.programId,
                    sequenceOrder: Number(v.sequenceOrder) || 1,
                    isMandatory: v.isMandatory,
                    prerequisitePathProgramId: v.prerequisitePathProgramId || null,
                    notes: v.notes || null,
                  }),
                );
                if (ok) setAddProgramOpen(false);
              }}
            >
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={addSkillOpen} onOpenChange={setAddSkillOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a target skill</DialogTitle>
            <DialogDescription>What this path is meant to build, and to what level.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <SelectField form={skillForm} name="skillId" label="Skill" required options={skillOptions} />
            <SelectField
              form={skillForm}
              name="targetProficiency"
              label="Target proficiency"
              required
              options={PROFICIENCY_LEVEL_OPTIONS}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddSkillOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                const v = skillForm.getValues();
                if (!v.skillId) {
                  toast({ title: 'Pick a skill', variant: 'destructive' });
                  return;
                }
                const ok = await run('Skill added', () =>
                  learningPathService.addSkill(id, {
                    skillId: v.skillId,
                    targetProficiency: v.targetProficiency as any,
                  }),
                );
                if (ok) setAddSkillOpen(false);
              }}
            >
              Add
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={enrollOpen} onOpenChange={setEnrollOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Enrol on this path</DialogTitle>
            <DialogDescription>
              A step is created for each of the {path.totalProgramsCount} programmes. You will be
              recorded as having assigned it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <EmployeePickerField form={enrollForm} name="employeeId" label="Employee" required />
            <DateField form={enrollForm} name="targetCompletionDate" label="Target completion" />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setEnrollOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button
              disabled={busy}
              onClick={async () => {
                const v = enrollForm.getValues();
                if (!v.employeeId) {
                  toast({ title: 'Pick an employee', variant: 'destructive' });
                  return;
                }
                const ok = await run('Enrolled', () =>
                  learningPathService.enroll({
                    employeeId: v.employeeId,
                    learningPathId: id,
                    enrolledDate: new Date().toISOString(),
                    targetCompletionDate: v.targetCompletionDate
                      ? new Date(v.targetCompletionDate).toISOString()
                      : null,
                  }),
                );
                if (ok) {
                  enrollForm.reset({ employeeId: '', targetCompletionDate: '' });
                  setEnrollOpen(false);
                }
              }}
            >
              Enrol
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={removeProgram !== null}
        onOpenChange={(o) => !o && setRemoveProgram(null)}
        title="Remove from sequence"
        description={
          removeProgram
            ? `Remove "${removeProgram.programName}"? Any later step that names it as a prerequisite will need a new one.`
            : ''
        }
        confirmText="Remove"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          if (!removeProgram) return false;
          const ok = await run('Removed', () => learningPathService.removeProgram(removeProgram.id));
          if (ok) setRemoveProgram(null);
          return ok;
        }}
      />

      <ConfirmationDialog
        open={removeSkill !== null}
        onOpenChange={(o) => !o && setRemoveSkill(null)}
        title="Remove target skill"
        description={removeSkill ? `Remove "${removeSkill.skillName}" from this path's targets?` : ''}
        confirmText="Remove"
        variant="destructive"
        isLoading={busy}
        onConfirm={async () => {
          if (!removeSkill) return false;
          const ok = await run('Removed', () => learningPathService.removeSkill(removeSkill.id));
          if (ok) setRemoveSkill(null);
          return ok;
        }}
      />
    </div>
  );
}
