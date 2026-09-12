'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Trash2, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { employeeService } from '@/services/hr/employee.service';
import type { ExpatriateFamilyMember } from '@/types/hr/employee-subresources';

const fmt = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * ⚠ The SAME vocabulary a dependant uses. Two kinship lists for one idea would drift, and the
 * question this panel exists to answer — how many residence permits this posting owes, and for
 * whom — needs a value you can group by rather than prose.
 */
const RELATIONSHIPS = [
  'Spouse', 'Son', 'Daughter', 'Mother', 'Father', 'Brother', 'Sister',
  'Uncle', 'Aunt', 'Nephew', 'Niece', 'Grandfather', 'Grandmother', 'Other',
] as const;

const BLANK = {
  fullName: '', relationship: 'Spouse', relationshipDescription: '',
  passportNumber: '', passportExpiryDate: '',
  residentPermitNumber: '', residentPermitIssueDate: '', residentPermitExpiryDate: '',
  arrivalDate: '', departureDate: '', notes: '',
};

const lapsing = (d?: string | null) =>
  !!d && (new Date(d).getTime() - Date.now()) / 86_400_000 < 90;

/**
 * Who actually accompanied an expatriate assignee.
 *
 * ⚠ **`familyAccompanying` was a bare bool.** The posting could assert a family had come and never
 * say who they were, so nobody could count the residence permits owed or see whose lapsed next.
 * Each accompanying person carries their own permit on their own clock.
 *
 * ⚠ Recording a member SETS the flag server-side, so the two cannot disagree.
 */
export function ExpatriateFamilyPanel({
  employeeId, assignmentId,
}: { employeeId: string; assignmentId: string }) {
  const { toast } = useToast();
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<ExpatriateFamilyMember | null>(null);
  const [form, setForm] = useState(BLANK);

  const { data: members, isLoading } = useQuery({
    queryKey: ['expatriate-family', assignmentId],
    queryFn: () => employeeService.getExpatriateFamily(employeeId, assignmentId),
    enabled: !!assignmentId,
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ['expatriate-family', assignmentId] });
    // The assignment's own flag may have flipped as a side effect of recording somebody.
    void qc.invalidateQueries({ queryKey: ['hr', 'employees', employeeId, 'expatriate-assignments'] });
  };

  const payload = () => ({
    fullName: form.fullName.trim(),
    relationship: form.relationship,
    relationshipDescription: form.relationship === 'Other'
      ? (form.relationshipDescription || null)
      : null,
    passportNumber: form.passportNumber || null,
    passportExpiryDate: form.passportExpiryDate || null,
    residentPermitNumber: form.residentPermitNumber || null,
    residentPermitIssueDate: form.residentPermitIssueDate || null,
    residentPermitExpiryDate: form.residentPermitExpiryDate || null,
    arrivalDate: form.arrivalDate || null,
    departureDate: form.departureDate || null,
    notes: form.notes || null,
  });

  const save = useMutation({
    mutationFn: () => editing
      ? employeeService.updateExpatriateFamilyMember(employeeId, assignmentId, editing.id, payload())
      : employeeService.addExpatriateFamilyMember(employeeId, assignmentId, payload()),
    onSuccess: () => {
      toast({ title: editing ? 'Family member updated' : 'Family member recorded' });
      setOpen(false); setEditing(null); setForm(BLANK); refresh();
    },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not saved',
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const remove = useMutation({
    mutationFn: (id: string) =>
      employeeService.removeExpatriateFamilyMember(employeeId, assignmentId, id),
    onSuccess: () => { toast({ title: 'Family member removed' }); refresh(); },
    onError: (e: unknown) => toast({
      variant: 'destructive', title: 'Not removed',
      description: e instanceof Error ? e.message : 'Refused',
    }),
  });

  const openEdit = (m: ExpatriateFamilyMember) => {
    setEditing(m);
    setForm({
      fullName: m.fullName,
      relationship: m.relationship,
      relationshipDescription: m.relationshipDescription ?? '',
      passportNumber: m.passportNumber ?? '',
      passportExpiryDate: m.passportExpiryDate?.slice(0, 10) ?? '',
      residentPermitNumber: m.residentPermitNumber ?? '',
      residentPermitIssueDate: m.residentPermitIssueDate?.slice(0, 10) ?? '',
      residentPermitExpiryDate: m.residentPermitExpiryDate?.slice(0, 10) ?? '',
      arrivalDate: m.arrivalDate?.slice(0, 10) ?? '',
      departureDate: m.departureDate?.slice(0, 10) ?? '',
      notes: m.notes ?? '',
    });
    setOpen(true);
  };

  return (
    <div className="space-y-3 rounded-md border p-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="flex items-center gap-2 font-medium">
            <Users className="h-4 w-4" />
            Accompanying family
          </p>
          <p className="text-xs text-muted-foreground">
            Each person needs their own residence permit, on their own clock.
          </p>
        </div>
        <Button
          size="sm"
          onClick={() => { setEditing(null); setForm(BLANK); setOpen(true); }}
        >
          <Plus className="mr-2 h-4 w-4" />
          Add
        </Button>
      </div>

      {isLoading ? (
        <div className="flex justify-center p-4">
          <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
        </div>
      ) : !members || members.length === 0 ? (
        <EmptyState
          icon={Users}
          title="Nobody recorded"
          description="Adding somebody here also marks the assignment as having family accompanying."
        />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Relationship</TableHead>
              <TableHead>Passport</TableHead>
              <TableHead>Residence permit</TableHead>
              <TableHead className="w-[90px]" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {members.map((m) => (
              <TableRow key={m.id} className={m.departureDate ? 'opacity-60' : undefined}>
                <TableCell>
                  <p className="font-medium">{m.fullName}</p>
                  {/* Departed rather than deleted: they did accompany the posting, and the record
                      should keep saying so. */}
                  {m.departureDate && (
                    <p className="text-xs text-muted-foreground">
                      departed {fmt(m.departureDate)}
                    </p>
                  )}
                </TableCell>
                <TableCell>
                  {m.relationship === 'Other'
                    ? (m.relationshipDescription || 'Other')
                    : m.relationship}
                </TableCell>
                <TableCell className="text-sm">
                  {m.passportNumber ?? '—'}
                  {m.passportExpiryDate && (
                    <span className="block text-xs text-muted-foreground">
                      to {fmt(m.passportExpiryDate)}
                    </span>
                  )}
                </TableCell>
                <TableCell className="text-sm">
                  {m.residentPermitNumber ?? <span className="text-muted-foreground">none</span>}
                  {m.residentPermitExpiryDate && (
                    lapsing(m.residentPermitExpiryDate) && !m.departureDate ? (
                      <Badge variant="destructive" className="ml-2">
                        expires {fmt(m.residentPermitExpiryDate)}
                      </Badge>
                    ) : (
                      <span className="block text-xs text-muted-foreground">
                        to {fmt(m.residentPermitExpiryDate)}
                      </span>
                    )
                  )}
                </TableCell>
                <TableCell>
                  <div className="flex justify-end gap-1">
                    <Button variant="ghost" size="sm" onClick={() => openEdit(m)}>Edit</Button>
                    <Button
                      variant="ghost" size="icon"
                      aria-label={`Remove ${m.fullName}`}
                      onClick={() => remove.mutate(m.id)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={open} onOpenChange={(o) => { setOpen(o); if (!o) { setEditing(null); setForm(BLANK); } }}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit family member' : 'Add family member'}</DialogTitle>
            <DialogDescription>
              Recording somebody here marks the assignment as having family accompanying.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-2">
              <Label>Full name</Label>
              <Input
                value={form.fullName}
                onChange={(e) => setForm((f) => ({ ...f, fullName: e.target.value }))}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Relationship</Label>
                <Select
                  value={form.relationship}
                  onValueChange={(v) => setForm((f) => ({ ...f, relationship: v }))}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {RELATIONSHIPS.map((r) => (
                      <SelectItem key={r} value={r}>{r}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              {/* Asked only when it means something — the same pairing a dependant uses. */}
              {form.relationship === 'Other' && (
                <div className="space-y-2">
                  <Label>Describe</Label>
                  <Input
                    value={form.relationshipDescription}
                    onChange={(e) => setForm((f) => ({ ...f, relationshipDescription: e.target.value }))}
                  />
                </div>
              )}
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Passport number</Label>
                <Input
                  value={form.passportNumber}
                  onChange={(e) => setForm((f) => ({ ...f, passportNumber: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Passport expiry</Label>
                <Input
                  type="date" value={form.passportExpiryDate}
                  onChange={(e) => setForm((f) => ({ ...f, passportExpiryDate: e.target.value }))}
                />
              </div>
            </div>
            <div className="grid grid-cols-3 gap-3">
              <div className="space-y-2">
                <Label>Residence permit</Label>
                <Input
                  value={form.residentPermitNumber}
                  onChange={(e) => setForm((f) => ({ ...f, residentPermitNumber: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Issued</Label>
                <Input
                  type="date" value={form.residentPermitIssueDate}
                  onChange={(e) => setForm((f) => ({ ...f, residentPermitIssueDate: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Expires</Label>
                <Input
                  type="date" value={form.residentPermitExpiryDate}
                  onChange={(e) => setForm((f) => ({ ...f, residentPermitExpiryDate: e.target.value }))}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Arrived</Label>
                <Input
                  type="date" value={form.arrivalDate}
                  onChange={(e) => setForm((f) => ({ ...f, arrivalDate: e.target.value }))}
                />
              </div>
              <div className="space-y-2">
                <Label>Departed</Label>
                <Input
                  type="date" value={form.departureDate}
                  onChange={(e) => setForm((f) => ({ ...f, departureDate: e.target.value }))}
                />
              </div>
            </div>
            <p className="text-xs text-muted-foreground">
              Set a departure date when somebody goes home ahead of the assignee — the row stays
              true rather than being deleted.
            </p>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => save.mutate()} disabled={save.isPending || !form.fullName.trim()}>
              {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
