'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Info, Loader2, Plus, Trash2, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Switch } from '@/components/ui/switch';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
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
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { probationConfirmingAuthorityService } from '@/services/hr/probation.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import { toast } from 'sonner';

/**
 * Who confirms probation, per organisation unit and staff category (FR-HR-032, decision D-2).
 *
 * ⚠ **This exists because the org data cannot answer the question.** FR-HR-032 routes the month-5
 * confirmation form to "the head"; on the reference tenant 0 of 41 units carry a head and 7% of
 * employees a manager, so deriving it would resolve to nobody for most staff. Naming the authority
 * here is the alternative — and it is why the register can say, at creation time, who will have to
 * act on a probation.
 *
 * Most specific rule wins: unit + level, then unit, then level, then the tenant-wide default. A
 * tenant can start with one default row and refine later.
 */
export default function ConfirmingAuthoritiesPage() {
  const qc = useQueryClient();
  const [adding, setAdding] = useState(false);

  const { data: rules, isLoading } = useQuery({
    queryKey: ['probation-authorities'],
    queryFn: () => probationConfirmingAuthorityService.getAll(),
  });

  const remove = useMutation({
    mutationFn: (id: string) => probationConfirmingAuthorityService.remove(id),
    onSuccess: () => {
      toast.success('Rule removed');
      void qc.invalidateQueries({ queryKey: ['probation-authorities'] });
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  const hasDefault = (rules ?? []).some((r) => r.specificity === 0 && r.isActive);

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Probation confirming authorities"
        description="Who signs off probation for each part of the organisation."
        backHref="/administration/hr"
        actions={
          <Button onClick={() => setAdding(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Add a rule
          </Button>
        }
      />

      {!isLoading && !hasDefault && (
        // ⚠ Worth saying out loud: with no tenant-wide default, any employee not covered by a
        // narrower rule has NO confirming authority, and their confirmation form will be raised
        // with nobody to send it to.
        <Alert>
          <Info className="h-4 w-4" />
          <AlertDescription>
            There is no tenant-wide default. Anyone not covered by a rule below will have no
            confirming authority, and their confirmation form will be queued unassigned.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="flex justify-center p-12">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : !rules || rules.length === 0 ? (
            <EmptyState
              icon={UserCheck}
              title="No confirming authority set"
              description="Add at least a tenant-wide default, or probation confirmations will have nobody to route to."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Scope</TableHead>
                  <TableHead>Confirms</TableHead>
                  <TableHead>Precedence</TableHead>
                  <TableHead>Active</TableHead>
                  <TableHead className="text-right" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {/* The API returns these most-specific-first, which is the order resolution applies
                    them — so the list reads the way the rule works. */}
                {rules.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell className="font-medium">{r.scope}</TableCell>
                    <TableCell>
                      {r.authorityEmployeeName}
                      <div className="text-xs text-muted-foreground">{r.authorityEmployeeNumber}</div>
                    </TableCell>
                    <TableCell>
                      <Badge variant="outline">
                        {r.specificity === 3
                          ? 'Unit + level'
                          : r.specificity === 2
                            ? 'Unit'
                            : r.specificity === 1
                              ? 'Staff level'
                              : 'Default'}
                      </Badge>
                    </TableCell>
                    <TableCell>{r.isActive ? 'Yes' : 'No'}</TableCell>
                    <TableCell className="text-right">
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => remove.mutate(r.id)}
                        disabled={remove.isPending}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {adding && (
        <AddRuleDialog
          onClose={() => setAdding(false)}
          onDone={() => void qc.invalidateQueries({ queryKey: ['probation-authorities'] })}
        />
      )}
    </div>
  );
}

function AddRuleDialog({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const [unitId, setUnitId] = useState<string>('any');
  const [levelId, setLevelId] = useState<string>('any');
  const [authorityId, setAuthorityId] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [notes, setNotes] = useState('');

  const { data: units } = useQuery({
    queryKey: ['org-units'],
    queryFn: () => organizationUnitService.getAll(),
  });
  const { data: levels } = useQuery({
    queryKey: ['staff-levels'],
    queryFn: () => staffLevelService.getAll(),
  });

  const create = useMutation({
    mutationFn: () =>
      probationConfirmingAuthorityService.create({
        // 'any' is the UI's word for "this dimension is not part of the rule". It must reach the
        // API as null, not as the string 'any' — the API keys the uniqueness of a rule on the pair.
        organizationUnitId: unitId === 'any' ? null : unitId,
        staffLevelId: levelId === 'any' ? null : levelId,
        authorityEmployeeId: authorityId,
        isActive,
        notes: notes.trim() || undefined,
      }),
    onSuccess: () => {
      toast.success('Confirming authority set');
      onDone();
      onClose();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add a confirming authority</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label>Organisation unit</Label>
            <Select value={unitId} onValueChange={setUnitId}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="any">All units</SelectItem>
                {(units ?? []).map((u) => (
                  <SelectItem key={u.id} value={u.id}>
                    {u.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Staff category</Label>
            <Select value={levelId} onValueChange={setLevelId}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="any">All categories</SelectItem>
                {(levels ?? []).map((l) => (
                  <SelectItem key={l.id} value={l.id}>
                    {l.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Who confirms</Label>
            <EmployeePicker value={authorityId} onChange={(id) => setAuthorityId(id ?? '')} />
          </div>

          <div className="flex items-center gap-3">
            <Switch checked={isActive} onCheckedChange={setIsActive} id="active" />
            <Label htmlFor="active">Active</Label>
          </div>

          <div className="space-y-2">
            <Label>Notes</Label>
            <Textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={2} />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button disabled={!authorityId || create.isPending} onClick={() => create.mutate()}>
            {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Save rule
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
