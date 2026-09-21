'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, ArrowRight, Pencil, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { employeeService } from '@/services/hr/employee.service';
import { movementService } from '@/services/hr/movement.service';
import { careerPathService } from '@/services/hr/career-path.service';
import { employeePositionService } from '@/services/hr/employee-position.service';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Plus } from 'lucide-react';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const money = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { minimumFractionDigits: 2 });

import type { CareerPathStep } from '@/services/hr/career-path.service';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

/**
 * One employee's career history: every position they have held, in order, with the movement that
 * caused each change.
 *
 * The steps are written by implementing a movement — closing the open one and opening the next — so
 * a gap here means a change was made to the employee record outside the movement process, not that
 * the history is broken.
 */
export default function CareerPathPage() {
  const { employeeId } = useParams<{ employeeId: string }>();
  const queryClient = useQueryClient();
  const { toast } = useToast();

  /**
   * ⚠ Only the four fields the server takes. The position, unit and salary came from the
   * movement that caused this step; changing them here would make the history disagree with the
   * record that produced it, so they are shown and not offered.
   */
  const [editing, setEditing] = useState<null | {
    id: string; positionTitle: string;
    endDate: string; isCurrent: boolean; achievements: string; keyProjects: string;
  }>(null);
  const [removing, setRemoving] = useState<CareerPathStep | null>(null);
  /**
   * ⚠ Recording a step by hand is for a gap the movement process did not produce — a change
   * made to the employee record directly, or history brought over from before the system. It is the
   * exception: a step that belongs to a movement should come from implementing that movement, and
   * one added here carries no movement link, which is how the two are told apart afterwards.
   */
  const [adding, setAdding] = useState<null | {
    positionId: string; organizationUnitId: string;
    startDate: string; endDate: string; isCurrent: boolean; salary: string;
    achievements: string; keyProjects: string;
  }>(null);

  const { data: employee } = useQuery({
    queryKey: ['hr', 'employee', employeeId],
    queryFn: () => employeeService.getById(employeeId),
  });

  const { data: steps = [], isLoading } = useQuery({
    queryKey: ['hr', 'career-paths', employeeId],
    queryFn: () => careerPathService.forEmployee(employeeId),
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['hr', 'career-paths', employeeId] });

  const { data: positions = [] } = useQuery({
    queryKey: ['hr', 'positions', 'all'],
    queryFn: () => employeePositionService.getAll(),
    enabled: adding !== null,
  });


  const add = useMutation({
    mutationFn: () => {
      if (!adding) throw new Error('Nothing to add.');
      return careerPathService.create({
        employeeId,
        positionId: adding.positionId,
        organizationUnitId: adding.organizationUnitId,
        startDate: new Date(adding.startDate).toISOString(),
        endDate: adding.endDate ? new Date(adding.endDate).toISOString() : null,
        isCurrent: adding.isCurrent,
        salary: Number(adding.salary || 0),
        achievements: adding.achievements.trim() || null,
        keyProjects: adding.keyProjects.trim() || null,
      });
    },
    onSuccess: () => {
      toast({ title: 'Step recorded' });
      setAdding(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The step was refused',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const save = useMutation({
    mutationFn: () => {
      if (!editing) throw new Error('Nothing to save.');
      return careerPathService.update(editing.id, {
        id: editing.id,
        endDate: editing.endDate ? new Date(editing.endDate).toISOString() : null,
        isCurrent: editing.isCurrent,
        achievements: editing.achievements.trim() || null,
        keyProjects: editing.keyProjects.trim() || null,
      });
    },
    onSuccess: () => {
      toast({ title: 'Step saved' });
      setEditing(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The change was refused',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => careerPathService.remove(id),
    onSuccess: () => {
      toast({ title: 'Step removed' });
      setRemoving(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'It could not be removed',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const { data: movements = [] } = useQuery({
    queryKey: ['hr', 'movements', 'by-employee', employeeId],
    queryFn: () => movementService.getByEmployee(employeeId),
  });

  const ordered = [...steps].sort(
    (a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime(),
  );

  const implemented = movements.filter((m) => m.status === 'Implemented');

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Career history"
        description={employee ? `${employee.fullName} — ${employee.employeeNumber}` : 'Loading…'}
        backHref="/hr/movements"
        actions={
          <Button
            variant="outline"
            onClick={() =>
              setAdding({
                positionId: '', organizationUnitId: '',
                startDate: '', endDate: '', isCurrent: false, salary: '',
                achievements: '', keyProjects: '',
              })
            }
          >
            <Plus className="mr-2 h-4 w-4" />
            Record a step
          </Button>
        }
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Positions held</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : ordered.length === 0 ? (
            <EmptyState
              title="No career history"
              description="Career steps are written when a movement is implemented. This employee has none yet."
            />
          ) : (
            <ol className="relative space-y-6 border-l pl-6">
              {ordered.map((step) => (
                <li key={step.id} className="relative">
                  <span
                    className={`absolute -left-[1.6875rem] mt-1.5 h-3 w-3 rounded-full border-2 border-background ${
                      step.isCurrent ? 'bg-primary' : 'bg-muted-foreground/40'
                    }`}
                  />
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium">{step.positionTitle ?? 'Position'}</span>
                    {step.isCurrent && <Badge>Current</Badge>}
                    {step.movementNumber && (
                      <Link
                        href={`/hr/movements/${step.movementId}`}
                        className="text-xs text-muted-foreground hover:underline"
                      >
                        via {step.movementNumber}
                      </Link>
                    )}
                  </div>
                  <div className="text-sm text-muted-foreground">
                    {step.organizationUnitName}
                    {step.locationName ? ` · ${step.locationName}` : ''}
                  </div>
                  <div className="mt-1 flex flex-wrap gap-x-6 text-xs text-muted-foreground">
                    <span>
                      {fmtDate(step.startDate)} <ArrowRight className="inline h-3 w-3" />{' '}
                      {step.endDate ? fmtDate(step.endDate) : 'present'}
                    </span>
                    <span>Salary {money(step.salary)}</span>
                    {step.salaryGradeName && <span>Grade {step.salaryGradeName}</span>}
                  </div>
                  {step.achievements && (
                    <p className="mt-2 text-sm">{step.achievements}</p>
                  )}
                  {step.keyProjects && (
                    <p className="mt-1 text-sm text-muted-foreground">{step.keyProjects}</p>
                  )}
                  <div className="mt-2 flex gap-1">
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() =>
                        setEditing({
                          id: step.id,
                          positionTitle: step.positionTitle ?? 'this step',
                          endDate: step.endDate ? step.endDate.slice(0, 10) : '',
                          isCurrent: step.isCurrent,
                          achievements: step.achievements ?? '',
                          keyProjects: step.keyProjects ?? '',
                        })
                      }
                    >
                      <Pencil className="mr-2 h-3.5 w-3.5" /> Annotate
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => setRemoving(step)}>
                      <Trash2 className="mr-2 h-3.5 w-3.5" /> Remove
                    </Button>
                  </div>
                </li>
              ))}
            </ol>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Movements applied</CardTitle>
        </CardHeader>
        <CardContent>
          {implemented.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No movement has been implemented for this employee yet.
            </p>
          ) : (
            <ul className="space-y-2 text-sm">
              {implemented.map((m) => (
                <li key={m.id} className="flex flex-wrap items-center justify-between gap-2">
                  <Link href={`/hr/movements/${m.id}`} className="font-medium hover:underline">
                    {m.movementNumber}
                  </Link>
                  <span className="text-muted-foreground">
                    {m.movementTypeName} · effective {fmtDate(m.effectiveDate)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing?.positionTitle}</DialogTitle>
            <DialogDescription>
              What they did in the post, and when it ended. The position, unit and salary came from
              the movement that caused this step — correct those on the movement.
            </DialogDescription>
          </DialogHeader>
          {editing && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="cpEnd">Ended</Label>
                  <Input id="cpEnd" type="date" value={editing.endDate}
                    onChange={(e) => setEditing({ ...editing, endDate: e.target.value })} />
                  <p className="text-xs text-muted-foreground">Blank means they are still in it.</p>
                </div>
                <div className="flex items-end">
                  <div className="flex w-full items-center justify-between rounded-md border p-3">
                    <Label htmlFor="cpCurrent">Current post</Label>
                    <Switch id="cpCurrent" checked={editing.isCurrent}
                      onCheckedChange={(v) => setEditing({ ...editing, isCurrent: v })} />
                  </div>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="cpAch">Achievements</Label>
                <Textarea id="cpAch" rows={3} value={editing.achievements}
                  onChange={(e) => setEditing({ ...editing, achievements: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="cpProj">Key projects</Label>
                <Textarea id="cpProj" rows={3} value={editing.keyProjects}
                  onChange={(e) => setEditing({ ...editing, keyProjects: e.target.value })} />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)}>Cancel</Button>
            <Button disabled={save.isPending} onClick={() => save.mutate()}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={adding !== null} onOpenChange={(o) => !o && setAdding(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Record a career step</DialogTitle>
            <DialogDescription>
              For history the movement process did not produce — a change made directly, or service
              from before this system. A step that belongs to a movement should come from
              implementing it.
            </DialogDescription>
          </DialogHeader>
          {adding && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Position</Label>
                <Select value={adding.positionId}
                  onValueChange={(v) => setAdding({ ...adding, positionId: v })}>
                  <SelectTrigger><SelectValue placeholder="Choose a position" /></SelectTrigger>
                  <SelectContent>
                    {positions.map((p: any) => (
                      <SelectItem key={p.id} value={p.id}>{p.title}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <OrganizationUnitPicker
                  value={adding.organizationUnitId}
                  onChange={(id) => setAdding({ ...adding, organizationUnitId: id })}
                  unitLabel="Organisation unit"
                  idPrefix="career-step-unit"
                />
              </div>
              <div className="grid grid-cols-3 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="cpFrom">From</Label>
                  <Input id="cpFrom" type="date" value={adding.startDate}
                    onChange={(e) => setAdding({ ...adding, startDate: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="cpTo">To</Label>
                  <Input id="cpTo" type="date" value={adding.endDate}
                    onChange={(e) => setAdding({ ...adding, endDate: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="cpSalary">Salary</Label>
                  <Input id="cpSalary" type="number" min={0} value={adding.salary}
                    onChange={(e) => setAdding({ ...adding, salary: e.target.value })} />
                </div>
              </div>
              <div className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <Label htmlFor="cpIsCurrent">Current post</Label>
                  <p className="text-xs text-muted-foreground">
                    Only one step should be current at a time.
                  </p>
                </div>
                <Switch id="cpIsCurrent" checked={adding.isCurrent}
                  onCheckedChange={(v) => setAdding({ ...adding, isCurrent: v })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="cpAddAch">Achievements</Label>
                <Textarea id="cpAddAch" rows={2} value={adding.achievements}
                  onChange={(e) => setAdding({ ...adding, achievements: e.target.value })} />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setAdding(null)}>Cancel</Button>
            <Button
              disabled={
                add.isPending || !adding?.positionId || !adding?.organizationUnitId || !adding?.startDate
              }
              onClick={() => add.mutate()}
            >
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={removing !== null}
        onOpenChange={(o) => !o && setRemoving(null)}
        title={`Remove the ${removing?.positionTitle ?? 'career'} step?`}
        description="For a step recorded in error. A step that genuinely happened belongs in the history even if the post has since changed — and one written by a movement will come back if that movement is re-implemented."
        confirmText="Remove"
        variant="destructive"
        onConfirm={() => { if (removing) remove.mutate(removing.id); }}
        isLoading={remove.isPending}
      />
    </div>
  );
}
