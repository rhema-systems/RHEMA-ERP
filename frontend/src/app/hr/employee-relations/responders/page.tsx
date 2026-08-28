'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Trash2, CheckCircle2, MinusCircle, Pencil } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { employeeRelationsResponderService } from '@/services/hr/employee-relations-admin.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import {
  GRIEVANCE_LADDER,
  type GrievanceEscalationLevel, type EmployeeRelationsResponder, type UpsertResponderRequest,
} from '@/types/hr/employee-relations';

/** The tenant-wide default scope — the fallback used when no unit row matches. */
const ALL_UNITS = '__default__';
/** Every scope at once. Not a server concept: this is the only view that uses `getAll`. */
const EVERY_SCOPE = '__everything__';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const levelLabel = (v: string) => GRIEVANCE_LADDER.find((l) => l.value === v)?.label ?? v;

/**
 * Who answers each rung — FR-HR-084, area 9c slice 5.
 *
 * ⚠ **The gaps are the point of this screen, not an error state.** TDC has no org-authority data —
 * no organisation unit has a head recorded — which is the wall four separate requirements have hit.
 * This matrix is what replaced guessing at one: HR names the answerer explicitly, per unit, with a
 * tenant-wide fallback. It covers **0 of 48 units today**, and every case in an uncovered unit
 * arrives unassigned for HR to route by hand, exactly as every case did before the matrix existed.
 * So the coverage panel renders a miss as a legible gap rather than as a failure.
 */
export default function ResponderMatrixPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [scope, setScope] = useState<string>(EVERY_SCOPE);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<EmployeeRelationsResponder | null>(null);

  const [fLevel, setFLevel] = useState<GrievanceEscalationLevel>('Supervisor');
  const [fScope, setFScope] = useState<string>(ALL_UNITS);
  const [fResponder, setFResponder] = useState<string | null>(null);
  const [fResponderLabel, setFResponderLabel] = useState<string | null>(null);
  const [fFrom, setFFrom] = useState('');
  const [fTo, setFTo] = useState('');
  const [fNotes, setFNotes] = useState('');

  const everything = scope === EVERY_SCOPE;
  const unitId = scope === ALL_UNITS || everything ? null : scope;

  const { data: units } = useQuery({
    queryKey: ['hr', 'organization-units'],
    queryFn: () => organizationUnitService.getAll(),
  });

  // Filtered on the SERVER, not in the browser: `getForScope` is what the scope view means, and
  // filtering `getAll` client-side would quietly answer a different question — it cannot tell a
  // unit with no rows apart from a unit that was never asked about.
  const { data: rows, isLoading } = useQuery({
    queryKey: ['hr', 'employee-relations', 'responders', 'rows', scope],
    queryFn: () => (everything
      ? employeeRelationsResponderService.getAll()
      : employeeRelationsResponderService.getForScope(unitId)),
  });

  // Coverage is per scope, so there is nothing to resolve for "every scope at once".
  const { data: coverage } = useQuery({
    queryKey: ['hr', 'employee-relations', 'responders', 'coverage', unitId],
    queryFn: () => employeeRelationsResponderService.getCoverage(unitId),
    enabled: !everything,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'employee-relations', 'responders'] });

  const openForm = (row: EmployeeRelationsResponder | null) => {
    setEditing(row);
    setFLevel(row?.level ?? 'Supervisor');
    setFScope(row?.organizationUnitId ?? ALL_UNITS);
    setFResponder(row?.responderEmployeeId ?? null);
    setFResponderLabel(row?.responderName ?? null);
    setFFrom(row?.effectiveFrom?.slice(0, 10) ?? '');
    setFTo(row?.effectiveTo?.slice(0, 10) ?? '');
    setFNotes(row?.notes ?? '');
    setFormOpen(true);
  };

  const save = useMutation({
    mutationFn: (payload: UpsertResponderRequest) =>
      editing
        ? employeeRelationsResponderService.update(editing.id, payload)
        : employeeRelationsResponderService.create(payload),
    onSuccess: () => { refresh(); setFormOpen(false); setEditing(null); },
    // The overlap refusal names the person already answering, which is the whole use of the message.
    onError: (e: any) => toast({ title: 'Not saved', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => employeeRelationsResponderService.remove(id),
    onSuccess: refresh,
    // ⚠ A 403 here is expected for an HR-role user: deleting is Discipline ADMIN, not Write.
    onError: (e: any) => toast({ title: 'Not removed', description: e?.message, variant: 'destructive' }),
  });

  const submit = () => {
    if (!fResponder) return;
    save.mutate({
      organizationUnitId: fScope === ALL_UNITS ? null : fScope,
      level: fLevel,
      responderEmployeeId: fResponder,
      effectiveFrom: fFrom || null,
      effectiveTo: fTo || null,
      notes: fNotes.trim() || null,
    });
  };

  const unitList = units ?? [];
  const scopeRows = rows ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Who answers each rung"
        description="Names the person who answers a grievance at each level of the escalation route, per organisation unit, with a tenant-wide fallback. A rung with nobody named is not an error — the case simply arrives for HR to route by hand."
        backHref="/hr/employee-relations"
        actions={
          <Button onClick={() => openForm(null)}>
            <Plus className="mr-2 h-4 w-4" /> Name a responder
          </Button>
        }
      />

      <div className="flex flex-wrap items-end gap-2">
        <div>
          <Label className="text-xs text-muted-foreground">Scope</Label>
          <Select value={scope} onValueChange={setScope}>
            <SelectTrigger className="w-80"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value={EVERY_SCOPE}>Every scope (the whole matrix)</SelectItem>
              <SelectItem value={ALL_UNITS}>All units (the default fallback)</SelectItem>
              {unitList.map((u) => (
                <SelectItem key={u.id} value={u.id}>{u.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      {!everything && (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            What this scope resolves to
            {coverage && (
              <Badge variant={coverage.coveredLevels === 6 ? 'default' : 'outline'} className="ml-2">
                {coverage.coveredLevels} of 6 rungs covered
              </Badge>
            )}
          </CardTitle>
          <p className="mt-1 text-xs text-muted-foreground">
            The answer the router would give today, resolving unit row → tenant default → nobody.
          </p>
        </CardHeader>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Rung</TableHead>
                <TableHead>Answers</TableHead>
                <TableHead>Resolved by</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(coverage?.levels ?? []).map((l) => (
                <TableRow key={l.level}>
                  <TableCell className="font-medium">{levelLabel(l.level)}</TableCell>
                  <TableCell>
                    {l.resolved ? (
                      <span className="inline-flex items-center gap-1">
                        <CheckCircle2 className="h-3 w-3 text-green-600" />{l.responderName}
                      </span>
                    ) : (
                      // Not an error, and not styled as one: this is the ordinary state.
                      <span className="inline-flex items-center gap-1 text-muted-foreground">
                        <MinusCircle className="h-3 w-3" />nobody named — HR routes by hand
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{l.resolvedBy}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            {everything ? 'Every assignment in the matrix' : 'Assignments in this scope'}
          </CardTitle>
          <p className="mt-1 text-xs text-muted-foreground">
            Including past and future ones. Two people cannot answer one rung for one scope on the
            same day, but consecutive periods are how a handover is recorded.
          </p>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex items-center justify-center p-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : scopeRows.length === 0 ? (
            <EmptyState
              title={everything ? 'The matrix is empty' : 'Nothing named in this scope'}
              description="Grievances here arrive unassigned, and HR names the answerer on the case itself. That is the ordinary state, not a fault."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  {everything && <TableHead>Scope</TableHead>}
                  <TableHead>Rung</TableHead>
                  <TableHead>Answers</TableHead>
                  <TableHead>From</TableHead>
                  <TableHead>Until</TableHead>
                  <TableHead>In force</TableHead>
                  <TableHead>Notes</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {scopeRows.map((r) => (
                  <TableRow key={r.id} className={r.isCurrent ? '' : 'opacity-60'}>
                    {/* scopeName is server-computed: the unit's name, or "All units (default)". */}
                    {everything && <TableCell>{r.scopeName}</TableCell>}
                    <TableCell className="font-medium">{levelLabel(r.level)}</TableCell>
                    <TableCell>
                      <div>{r.responderName}</div>
                      {r.responderEmployeeNumber && (
                        <div className="text-xs text-muted-foreground">{r.responderEmployeeNumber}</div>
                      )}
                    </TableCell>
                    <TableCell>{fmtDate(r.effectiveFrom)}</TableCell>
                    <TableCell>{fmtDate(r.effectiveTo)}</TableCell>
                    <TableCell>
                      {r.isCurrent
                        ? <Badge>today</Badge>
                        : <span className="text-xs text-muted-foreground">not today</span>}
                    </TableCell>
                    <TableCell className="max-w-xs truncate">{r.notes ?? '—'}</TableCell>
                    <TableCell className="text-right">
                      <Button variant="ghost" size="sm" onClick={() => openForm(r)}>
                        <Pencil className="h-3 w-3" />
                      </Button>
                      <Button variant="ghost" size="sm" onClick={() => remove.mutate(r.id)}>
                        <Trash2 className="h-3 w-3" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        Removing an assignment needs the Discipline <strong>Admin</strong> permission, not Write:
        who answered a rung last year is a fact about the cases decided last year, so it is kept as a
        soft delete and read by the audit trail as well as by the router.
      </p>

      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? 'Change this assignment' : 'Name a responder'}</DialogTitle>
            <DialogDescription>
              Who answers a grievance at this rung, for this scope. A unit row wins over the
              tenant-wide default; if neither exists, the case arrives unassigned.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>Scope</Label>
              <Select value={fScope} onValueChange={setFScope}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={ALL_UNITS}>All units (the default fallback)</SelectItem>
                  {unitList.map((u) => (
                    <SelectItem key={u.id} value={u.id}>{u.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Rung</Label>
              <Select value={fLevel} onValueChange={(v) => setFLevel(v as GrievanceEscalationLevel)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {GRIEVANCE_LADDER.map((l) => (
                    <SelectItem key={l.value} value={l.value}>{l.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Who answers</Label>
              <EmployeePicker
                value={fResponder}
                initialLabel={fResponderLabel}
                onChange={(v, label) => { setFResponder(v); setFResponderLabel(label); }}
              />
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1">
                <Label htmlFor="r-from">From</Label>
                <Input id="r-from" type="date" value={fFrom} onChange={(e) => setFFrom(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="r-to">Until</Label>
                <Input id="r-to" type="date" value={fTo} onChange={(e) => setFTo(e.target.value)} />
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              Leave both blank for an open-ended assignment. Dates are how a handover is recorded —
              overlapping ones are refused, because two people answering one rung on one day would
              resolve arbitrarily.
            </p>
            <div className="space-y-1">
              <Label htmlFor="r-notes">Notes</Label>
              <Textarea id="r-notes" rows={2} value={fNotes} onChange={(e) => setFNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)}>Cancel</Button>
            <Button disabled={!fResponder || save.isPending} onClick={submit}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
