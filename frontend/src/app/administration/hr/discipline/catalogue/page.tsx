'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowDown,
  ArrowUp,
  ChevronDown,
  ChevronRight,
  Gavel,
  Loader2,
  Pencil,
  Plus,
  Trash2,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { HR_ADMIN_ROLES, HR_ROLES } from '@/components/hr/common/PermissionGate';
import { useAuth } from '@/hooks/use-auth';
import { useToast } from '@/hooks/use-toast';
import { disciplineLookupService } from '@/services/hr/discipline.service';
import { ACTION_AUTHORITY_OPTIONS } from '@/types/hr/discipline';
import type {
  DisciplinaryActionAuthority,
  DisciplinaryActionTypeSummary,
  StaffOffenseProcedure,
  StaffOffenseSummary,
} from '@/types/hr/discipline';

/**
 * The offence catalogue and the sanction catalogue.
 *
 * Both were read-only in the product: the case screens picked from them, and nothing could add to
 * them or correct them, so a client was stuck with whatever the seed shipped. The offence half
 * carries the procedure ladder a case climbs — `initialise` copies these steps onto a case and
 * stamps its due dates from `expectedCompletionDays`, so an offence with no steps produces a case
 * with no procedure.
 *
 * ⚠ **Edits load the record by id.** Neither list read is enough: the offence summary has no
 * `offenseDescription` and the action-type summary has neither `description` nor either default,
 * and both updates write all of those. A dialog bound to the row would blank them on save.
 *
 * ⚠ **Sequence is renumbered by the reorder endpoint, not by editing a step's number.** Two steps
 * cannot share a sequence, so moving one by hand would collide with whatever already held that
 * position; the arrows below send the whole order and let the server assign.
 */

type OffenseForm = {
  offenseCode: string;
  offenseName: string;
  offenseDescription: string;
  isActive: boolean;
};

type StepForm = {
  stepName: string;
  stepDescription: string;
  expectedCompletionDays: string;
};

type ActionTypeForm = {
  code: string;
  name: string;
  description: string;
  isActive: boolean;
  defaultSuspensionDays: string;
  defaultFineAmount: string;
  minimumAuthority: DisciplinaryActionAuthority;
};

const EMPTY_OFFENSE: OffenseForm = {
  offenseCode: '', offenseName: '', offenseDescription: '', isActive: true,
};
const EMPTY_STEP: StepForm = { stepName: '', stepDescription: '', expectedCompletionDays: '' };
const EMPTY_ACTION: ActionTypeForm = {
  code: '', name: '', description: '', isActive: true,
  defaultSuspensionDays: '', defaultFineAmount: '', minimumAuthority: 'Hr',
};

export default function DisciplineCataloguePage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const { hasAnyPermission, hasAnyRole } = useAuth();

  // Create and update are Discipline.Write; every delete is Discipline.Admin.
  const canWrite =
    hasAnyPermission(['HR.Discipline.Write', 'HR.Discipline.Admin']) || hasAnyRole(HR_ROLES);
  const canDelete =
    hasAnyPermission(['HR.Discipline.Admin']) || hasAnyRole(HR_ADMIN_ROLES);

  const [expanded, setExpanded] = useState<string | null>(null);
  const [offenseDialog, setOffenseDialog] = useState<null | { id: string | null }>(null);
  const [offenseForm, setOffenseForm] = useState<OffenseForm>(EMPTY_OFFENSE);
  const [stepDialog, setStepDialog] = useState<null | { offenseId: string; id: string | null }>(null);
  const [stepForm, setStepForm] = useState<StepForm>(EMPTY_STEP);
  const [actionDialog, setActionDialog] = useState<null | { id: string | null }>(null);
  const [actionForm, setActionForm] = useState<ActionTypeForm>(EMPTY_ACTION);
  const [confirming, setConfirming] = useState<null | { kind: string; id: string; label: string }>(null);

  const offensesKey = ['hr', 'discipline', 'offenses'];
  const actionTypesKey = ['hr', 'discipline', 'action-types'];

  const { data: offenses = [], isLoading } = useQuery({
    queryKey: offensesKey,
    queryFn: () => disciplineLookupService.getOffenses(),
  });

  const { data: detail, isLoading: detailLoading } = useQuery({
    queryKey: [...offensesKey, expanded, 'with-procedures'],
    enabled: expanded !== null,
    queryFn: () => disciplineLookupService.getOffenseWithProcedures(expanded as string),
  });

  const { data: actionTypes = [] } = useQuery({
    queryKey: actionTypesKey,
    queryFn: () => disciplineLookupService.getActionTypes(),
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: offensesKey });
    queryClient.invalidateQueries({ queryKey: actionTypesKey });
  };
  const onError = (e: Error) =>
    toast({ title: 'The change was refused', description: e.message, variant: 'destructive' });

  // ── offences ───────────────────────────────────────────────────────────────
  const saveOffense = useMutation<unknown, Error>({
    mutationFn: () => {
      const payload = {
        offenseCode: offenseForm.offenseCode.trim(),
        offenseName: offenseForm.offenseName.trim(),
        offenseDescription: offenseForm.offenseDescription.trim(),
        isActive: offenseForm.isActive,
      };
      const id = offenseDialog?.id;
      return id
        ? disciplineLookupService.updateOffense(id, { ...payload, id })
        : disciplineLookupService.createOffense(payload);
    },
    onSuccess: () => {
      refresh();
      setOffenseDialog(null);
      toast({ title: offenseDialog?.id ? 'Offence updated' : 'Offence added' });
    },
    onError,
  });

  /** Loads the record before opening, because the list row has no description. */
  const openOffense = async (row?: StaffOffenseSummary) => {
    if (!row) {
      setOffenseForm(EMPTY_OFFENSE);
      setOffenseDialog({ id: null });
      return;
    }
    try {
      const full = await disciplineLookupService.getOffenseWithProcedures(row.id);
      setOffenseForm({
        offenseCode: full.offenseCode,
        offenseName: full.offenseName,
        offenseDescription: full.offenseDescription ?? '',
        isActive: full.isActive,
      });
      setOffenseDialog({ id: row.id });
    } catch (e) {
      onError(e as Error);
    }
  };

  // ── the ladder ─────────────────────────────────────────────────────────────
  const saveStep = useMutation<unknown, Error>({
    mutationFn: () => {
      if (!stepDialog) throw new Error('No step is being edited.');
      const offenseId = stepDialog.offenseId;
      const base = {
        offenseId,
        stepName: stepForm.stepName.trim(),
        stepDescription: stepForm.stepDescription.trim(),
        expectedCompletionDays:
          stepForm.expectedCompletionDays.trim() === ''
            ? null
            : Number(stepForm.expectedCompletionDays),
      };
      const id = stepDialog.id;
      if (id) {
        const current = detail?.procedures.find((s) => s.id === id);
        // Sequence is carried through untouched — the arrows are what change order.
        return disciplineLookupService.updateProcedure(id, {
          ...base, id, sequence: current?.sequence ?? 1,
        });
      }
      const next = (detail?.procedures.length ?? 0) + 1;
      return disciplineLookupService.addProcedure(offenseId, { ...base, sequence: next });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: offensesKey });
      setStepDialog(null);
      toast({ title: stepDialog?.id ? 'Step updated' : 'Step added' });
    },
    onError,
  });

  const reorder = useMutation<unknown, Error, { offenseId: string; ids: string[] }>({
    mutationFn: ({ offenseId, ids }) => disciplineLookupService.reorderProcedures(offenseId, ids),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: offensesKey });
      toast({ title: 'Order saved' });
    },
    onError,
  });

  const move = (steps: StaffOffenseProcedure[], index: number, by: -1 | 1) => {
    const next = [...steps].sort((a, b) => a.sequence - b.sequence);
    const target = index + by;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target], next[index]];
    reorder.mutate({ offenseId: expanded as string, ids: next.map((s) => s.id) });
  };

  // ── action types ───────────────────────────────────────────────────────────
  const saveActionType = useMutation<unknown, Error>({
    mutationFn: () => {
      const payload = {
        code: actionForm.code.trim(),
        name: actionForm.name.trim(),
        description: actionForm.description.trim(),
        isActive: actionForm.isActive,
        defaultSuspensionDays:
          actionForm.defaultSuspensionDays.trim() === ''
            ? null
            : Number(actionForm.defaultSuspensionDays),
        defaultFineAmount:
          actionForm.defaultFineAmount.trim() === '' ? null : Number(actionForm.defaultFineAmount),
        minimumAuthority: actionForm.minimumAuthority,
      };
      const id = actionDialog?.id;
      return id
        ? disciplineLookupService.updateActionType(id, { ...payload, id })
        : disciplineLookupService.createActionType(payload);
    },
    onSuccess: () => {
      refresh();
      setActionDialog(null);
      toast({ title: actionDialog?.id ? 'Sanction updated' : 'Sanction added' });
    },
    onError,
  });

  /** Loads by id — the row has neither the description nor either default. */
  const openActionType = async (row?: DisciplinaryActionTypeSummary) => {
    if (!row) {
      setActionForm(EMPTY_ACTION);
      setActionDialog({ id: null });
      return;
    }
    try {
      const full = await disciplineLookupService.getActionType(row.id);
      setActionForm({
        code: full.code,
        name: full.name,
        description: full.description ?? '',
        isActive: full.isActive,
        defaultSuspensionDays:
          full.defaultSuspensionDays === null || full.defaultSuspensionDays === undefined
            ? '' : String(full.defaultSuspensionDays),
        defaultFineAmount:
          full.defaultFineAmount === null || full.defaultFineAmount === undefined
            ? '' : String(full.defaultFineAmount),
        minimumAuthority: full.minimumAuthority,
      });
      setActionDialog({ id: row.id });
    } catch (e) {
      onError(e as Error);
    }
  };

  const remove = useMutation<unknown, Error>({
    mutationFn: () => {
      const c = confirming;
      if (!c) throw new Error('Nothing is selected for removal.');
      if (c.kind === 'offense') return disciplineLookupService.deleteOffense(c.id);
      if (c.kind === 'step') return disciplineLookupService.deleteProcedure(c.id);
      return disciplineLookupService.deleteActionType(c.id);
    },
    onSuccess: () => {
      refresh();
      setConfirming(null);
      toast({ title: 'Removed' });
    },
    onError,
  });

  const steps = [...(detail?.procedures ?? [])].sort((a, b) => a.sequence - b.sequence);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Discipline catalogue"
        description="What counts as misconduct, the procedure each offence must follow, and the sanctions available — the reference data every disciplinary case is built from."
        backHref="/administration/hr"
        actions={
          canWrite ? (
            <Button onClick={() => openOffense()}>
              <Plus className="mr-2 h-4 w-4" /> Add an offence
            </Button>
          ) : undefined
        }
      />

      <Tabs defaultValue="offenses">
        <TabsList>
          <TabsTrigger value="offenses">Offences ({offenses.length})</TabsTrigger>
          <TabsTrigger value="sanctions">Sanctions ({actionTypes.length})</TabsTrigger>
        </TabsList>

        {/* ── offences ───────────────────────────────────────────────────── */}
        <TabsContent value="offenses" className="mt-4 space-y-4">
          {isLoading ? (
            <div className="flex justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : offenses.length === 0 ? (
            <EmptyState
              title="No offences"
              description="The catalogue is empty, so no case can name what happened. Add the first offence."
              icon={Gavel}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-10" />
                      <TableHead>Code</TableHead>
                      <TableHead>Offence</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {offenses.map((o) => (
                      <TableRow key={o.id}>
                        <TableCell>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setExpanded(expanded === o.id ? null : o.id)}
                            aria-label="Show the procedure"
                          >
                            {expanded === o.id
                              ? <ChevronDown className="h-4 w-4" />
                              : <ChevronRight className="h-4 w-4" />}
                          </Button>
                        </TableCell>
                        <TableCell className="font-mono text-sm">{o.offenseCode}</TableCell>
                        <TableCell className="font-medium">{o.offenseName}</TableCell>
                        <TableCell>
                          {o.isActive
                            ? <Badge variant="secondary">Active</Badge>
                            : <Badge variant="outline">Retired</Badge>}
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            {canWrite && (
                              <Button variant="ghost" size="sm" onClick={() => openOffense(o)}>
                                <Pencil className="h-4 w-4" />
                              </Button>
                            )}
                            {canDelete && (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => setConfirming({
                                  kind: 'offense', id: o.id, label: o.offenseName,
                                })}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}

          {expanded && (
            <Card>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle className="text-base">
                  The procedure for {detail?.offenseName ?? '…'}
                </CardTitle>
                {canWrite && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => { setStepForm(EMPTY_STEP); setStepDialog({ offenseId: expanded, id: null }); }}
                  >
                    <Plus className="mr-2 h-4 w-4" /> Add a step
                  </Button>
                )}
              </CardHeader>
              <CardContent className="p-0">
                {detailLoading ? (
                  <div className="flex justify-center p-8">
                    <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
                  </div>
                ) : steps.length === 0 ? (
                  <div className="p-6">
                    <EmptyState
                      title="No steps"
                      description="A case raised for this offence would have no procedure to climb — initialising it would copy nothing."
                      icon={Gavel}
                    />
                  </div>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-16">Step</TableHead>
                        <TableHead>Name</TableHead>
                        <TableHead>What happens</TableHead>
                        <TableHead className="text-right">Days</TableHead>
                        <TableHead className="text-right" />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {steps.map((s, i) => (
                        <TableRow key={s.id}>
                          <TableCell className="tabular-nums">{s.sequence}</TableCell>
                          <TableCell className="font-medium">{s.stepName}</TableCell>
                          <TableCell className="text-sm text-muted-foreground">
                            {s.stepDescription}
                          </TableCell>
                          <TableCell className="text-right tabular-nums">
                            {s.expectedCompletionDays ?? '—'}
                          </TableCell>
                          <TableCell className="text-right">
                            <div className="flex justify-end gap-1">
                              {canWrite && (
                                <>
                                  <Button
                                    variant="ghost" size="sm"
                                    disabled={i === 0 || reorder.isPending}
                                    onClick={() => move(steps, i, -1)}
                                    aria-label="Move up"
                                  >
                                    <ArrowUp className="h-4 w-4" />
                                  </Button>
                                  <Button
                                    variant="ghost" size="sm"
                                    disabled={i === steps.length - 1 || reorder.isPending}
                                    onClick={() => move(steps, i, 1)}
                                    aria-label="Move down"
                                  >
                                    <ArrowDown className="h-4 w-4" />
                                  </Button>
                                  <Button
                                    variant="ghost" size="sm"
                                    onClick={() => {
                                      setStepForm({
                                        stepName: s.stepName,
                                        stepDescription: s.stepDescription,
                                        expectedCompletionDays:
                                          s.expectedCompletionDays === null
                                          || s.expectedCompletionDays === undefined
                                            ? '' : String(s.expectedCompletionDays),
                                      });
                                      setStepDialog({ offenseId: expanded, id: s.id });
                                    }}
                                  >
                                    <Pencil className="h-4 w-4" />
                                  </Button>
                                </>
                              )}
                              {canDelete && (
                                <Button
                                  variant="ghost" size="sm"
                                  onClick={() => setConfirming({
                                    kind: 'step', id: s.id, label: s.stepName,
                                  })}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            </Card>
          )}
        </TabsContent>

        {/* ── sanctions ──────────────────────────────────────────────────── */}
        <TabsContent value="sanctions" className="mt-4 space-y-4">
          {canWrite && (
            <div className="flex justify-end">
              <Button variant="outline" onClick={() => openActionType()}>
                <Plus className="mr-2 h-4 w-4" /> Add a sanction
              </Button>
            </div>
          )}
          {actionTypes.length === 0 ? (
            <EmptyState
              title="No sanctions"
              description="Nothing can be issued against a case until the catalogue names at least one."
              icon={Gavel}
            />
          ) : (
            <Card>
              <CardContent className="p-0">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Code</TableHead>
                      <TableHead>Sanction</TableHead>
                      <TableHead>Who may issue it</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {actionTypes.map((t) => (
                      <TableRow key={t.id}>
                        <TableCell className="font-mono text-sm">{t.code}</TableCell>
                        <TableCell className="font-medium">{t.name}</TableCell>
                        <TableCell>{t.minimumAuthorityName}</TableCell>
                        <TableCell>
                          {t.isActive
                            ? <Badge variant="secondary">Active</Badge>
                            : <Badge variant="outline">Retired</Badge>}
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-1">
                            {canWrite && (
                              <Button variant="ghost" size="sm" onClick={() => openActionType(t)}>
                                <Pencil className="h-4 w-4" />
                              </Button>
                            )}
                            {canDelete && (
                              <Button
                                variant="ghost" size="sm"
                                onClick={() => setConfirming({
                                  kind: 'action', id: t.id, label: t.name,
                                })}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          )}
        </TabsContent>
      </Tabs>

      {/* ── the offence dialog ───────────────────────────────────────────── */}
      <Dialog open={offenseDialog !== null} onOpenChange={(o) => !o && setOffenseDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{offenseDialog?.id ? 'Edit the offence' : 'Add an offence'}</DialogTitle>
            <DialogDescription>
              The code is how a case names what happened, so it should stay stable once cases exist.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-2">
                <Label>Code *</Label>
                <Input
                  value={offenseForm.offenseCode}
                  onChange={(e) => setOffenseForm((f) => ({ ...f, offenseCode: e.target.value }))}
                />
              </div>
              <div className="col-span-2 space-y-2">
                <Label>Name *</Label>
                <Input
                  value={offenseForm.offenseName}
                  onChange={(e) => setOffenseForm((f) => ({ ...f, offenseName: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea
                value={offenseForm.offenseDescription}
                onChange={(e) => setOffenseForm((f) => ({ ...f, offenseDescription: e.target.value }))}
              />
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="offence-active"
                checked={offenseForm.isActive}
                onCheckedChange={(v) => setOffenseForm((f) => ({ ...f, isActive: v === true }))}
              />
              <Label htmlFor="offence-active">
                Available to new cases (retiring it leaves existing cases alone)
              </Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOffenseDialog(null)}>Cancel</Button>
            <Button
              onClick={() => saveOffense.mutate()}
              disabled={
                saveOffense.isPending
                || !offenseForm.offenseCode.trim()
                || !offenseForm.offenseName.trim()
              }
            >
              {saveOffense.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── the step dialog ──────────────────────────────────────────────── */}
      <Dialog open={stepDialog !== null} onOpenChange={(o) => !o && setStepDialog(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{stepDialog?.id ? 'Edit the step' : 'Add a step'}</DialogTitle>
            <DialogDescription>
              A case initialised for this offence copies these steps and dates each one from the
              days below. Order is set with the arrows, not here.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Step name *</Label>
              <Input
                value={stepForm.stepName}
                onChange={(e) => setStepForm((f) => ({ ...f, stepName: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>What happens</Label>
              <Textarea
                value={stepForm.stepDescription}
                onChange={(e) => setStepForm((f) => ({ ...f, stepDescription: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Expected completion (days)</Label>
              <Input
                type="number"
                min={0}
                value={stepForm.expectedCompletionDays}
                onChange={(e) => setStepForm((f) => ({ ...f, expectedCompletionDays: e.target.value }))}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStepDialog(null)}>Cancel</Button>
            <Button
              onClick={() => saveStep.mutate()}
              disabled={saveStep.isPending || !stepForm.stepName.trim()}
            >
              {saveStep.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── the sanction dialog ──────────────────────────────────────────── */}
      <Dialog open={actionDialog !== null} onOpenChange={(o) => !o && setActionDialog(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{actionDialog?.id ? 'Edit the sanction' : 'Add a sanction'}</DialogTitle>
            <DialogDescription>
              The defaults below seed a sanction when it is issued; the issuer can still change them.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-2">
                <Label>Code *</Label>
                <Input
                  value={actionForm.code}
                  onChange={(e) => setActionForm((f) => ({ ...f, code: e.target.value }))}
                />
              </div>
              <div className="col-span-2 space-y-2">
                <Label>Name *</Label>
                <Input
                  value={actionForm.name}
                  onChange={(e) => setActionForm((f) => ({ ...f, name: e.target.value }))}
                />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Textarea
                value={actionForm.description}
                onChange={(e) => setActionForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            <div className="space-y-2">
              <Label>Who may issue it</Label>
              <Select
                value={actionForm.minimumAuthority}
                onValueChange={(v) =>
                  setActionForm((f) => ({ ...f, minimumAuthority: v as DisciplinaryActionAuthority }))}
              >
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ACTION_AUTHORITY_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <p className="text-xs text-muted-foreground">
                {ACTION_AUTHORITY_OPTIONS.find((o) => o.value === actionForm.minimumAuthority)?.hint}
              </p>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Default suspension (days)</Label>
                <Input
                  type="number"
                  min={0}
                  value={actionForm.defaultSuspensionDays}
                  onChange={(e) => setActionForm((f) => ({ ...f, defaultSuspensionDays: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Default fine</Label>
                <Input
                  type="number"
                  step="0.01"
                  min={0}
                  value={actionForm.defaultFineAmount}
                  onChange={(e) => setActionForm((f) => ({ ...f, defaultFineAmount: e.target.value }))}
                />
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Checkbox
                id="action-active"
                checked={actionForm.isActive}
                onCheckedChange={(v) => setActionForm((f) => ({ ...f, isActive: v === true }))}
              />
              <Label htmlFor="action-active">Available to new cases</Label>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActionDialog(null)}>Cancel</Button>
            <Button
              onClick={() => saveActionType.mutate()}
              disabled={saveActionType.isPending || !actionForm.code.trim() || !actionForm.name.trim()}
            >
              {saveActionType.isPending ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ── one confirm for all three ────────────────────────────────────── */}
      <Dialog open={confirming !== null} onOpenChange={(o) => !o && setConfirming(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Remove {confirming?.label}?</DialogTitle>
            <DialogDescription>
              {confirming?.kind === 'offense'
                ? 'Cases already raised under this offence keep it. If it is simply no longer used, retiring it is the gentler option.'
                : confirming?.kind === 'step'
                  ? 'Cases whose procedure was already initialised keep the step they copied.'
                  : 'Sanctions already issued keep this type.'}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirming(null)}>Cancel</Button>
            <Button variant="destructive" onClick={() => remove.mutate()} disabled={remove.isPending}>
              {remove.isPending ? 'Removing…' : 'Remove'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
