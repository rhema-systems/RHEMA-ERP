'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Loader2, Plus, Trash2, UserMinus, UserPlus, Users } from 'lucide-react';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Switch } from '@/components/ui/switch';
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
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { awardsService } from '@/services/hr/awards.service';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');
const today = () => new Date().toISOString().slice(0, 10);

/**
 * Award committees and their members.
 *
 * ⚠ **Membership is the entitlement to score, and the only one.** A committee member holds no HR
 * permission; adding somebody here is what lets them score, and deactivating them is what stops it.
 * That is why this screen matters more than its size suggests — it is the access control for the
 * whole scoring surface.
 *
 * ⚠ **`role` is free text, not an enum.** It is whatever HR types — "Chair", "Secretary" — and the
 * API takes up to 100 characters.
 *
 * ⚠ **A member is deactivated, never deleted.** The scores they gave stay attributable.
 */
export default function AwardCommitteesPage() {
  const queryClient = useQueryClient();
  const [selectedId, setSelectedId] = useState('');
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [addingMember, setAddingMember] = useState(false);
  const [removing, setRemoving] = useState(false);

  const [form, setForm] = useState({
    name: '', description: '', quorumRequired: 1, reviewDeadlineDays: 7,
    effectiveFrom: today(), isActive: true,
  });
  const [member, setMember] = useState({ employeeId: '', role: '', startDate: today() });

  const { data: committees, isLoading } = useQuery({
    queryKey: ['award-committees'],
    queryFn: () => awardsService.getCommittees(),
  });

  const { data: detail } = useQuery({
    queryKey: ['award-committee', selectedId],
    queryFn: () => awardsService.getCommitteeWithMembers(selectedId),
    enabled: Boolean(selectedId),
  });

  // ⚠ Asked of the server rather than counted here. Quorum is what the scoring service checks when
  // the committee decides; a client-side count that disagreed with it would tell HR a committee was
  // ready when the engine thinks otherwise.
  const { data: quorate } = useQuery({
    queryKey: ['award-committee-quorum', selectedId],
    queryFn: () => awardsService.hasQuorum(selectedId),
    enabled: Boolean(selectedId),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['award-committees'] });
    if (selectedId) queryClient.invalidateQueries({ queryKey: ['award-committee', selectedId] });
  };

  const create = useMutation({
    mutationFn: () => {
      const body = {
        name: form.name.trim(),
        description: form.description.trim() || null,
        quorumRequired: Number(form.quorumRequired),
        reviewDeadlineDays: Number(form.reviewDeadlineDays),
        effectiveFrom: new Date(form.effectiveFrom).toISOString().slice(0, 19),
        isActive: form.isActive,
      };
      return editingId
        ? awardsService.updateCommittee(editingId, { ...body, id: editingId })
        : awardsService.createCommittee(body);
    },
    onSuccess: () => {
      toast.success(editingId ? 'Saved.' : 'Committee created.');
      setCreating(false); setEditingId(null); invalidate();
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The committee was refused.'),
  });

  const addMember = useMutation({
    mutationFn: () =>
      awardsService.addCommitteeMember(selectedId, {
        employeeId: member.employeeId,
        role: member.role.trim() || null,
        startDate: new Date(member.startDate).toISOString().slice(0, 19),
        isActive: true,
      }),
    onSuccess: () => {
      toast.success('Added. They can now score nominations in front of this committee.');
      setAddingMember(false);
      setMember({ employeeId: '', role: '', startDate: today() });
      invalidate();
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The member was refused.'),
  });

  const removeCommittee = useMutation({
    mutationFn: () => awardsService.deleteCommittee(selectedId),
    onSuccess: () => {
      toast.success('Removed.');
      setRemoving(false); setSelectedId(''); invalidate();
    },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The removal was refused.'),
  });

  const deactivate = useMutation({
    mutationFn: (memberId: string) => awardsService.deactivateCommitteeMember(memberId),
    onSuccess: () => { toast.success('Deactivated. Their past scores remain.'); invalidate(); },
    onError: (e: any) => toast.error(e?.body?.detail || e?.message || 'The change was refused.'),
  });

  const rows = committees ?? [];
  const members = detail?.members ?? [];
  const activeMembers = members.filter((m) => m.isActive).length;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Award committees"
        description="Who scores nominations. Membership is the entitlement — there is no permission for it."
        backHref="/administration/hr/awards"
        actions={
          <Button onClick={() => setCreating(true)}>
            <Plus className="mr-2 h-4 w-4" />
            New committee
          </Button>
        }
      />

      <div className="grid gap-6 lg:grid-cols-[20rem_1fr]">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Committees</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {isLoading ? (
              <div className="flex items-center justify-center p-8">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            ) : rows.length === 0 ? (
              <EmptyState icon={Users} title="None yet" description="Create the first committee." />
            ) : (
              <div className="space-y-1 p-2">
                {rows.map((c) => (
                  <button
                    type="button"
                    key={c.id}
                    onClick={() => setSelectedId(c.id)}
                    className={`flex w-full items-center justify-between rounded px-3 py-2 text-left text-sm hover:bg-muted ${
                      c.id === selectedId ? 'bg-muted font-medium' : ''
                    }`}
                  >
                    <span className="truncate">{c.name}</span>
                    <span className="ml-2 shrink-0 text-xs text-muted-foreground">
                      {c.memberCount}
                    </span>
                  </button>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle className="text-base">
              {detail ? detail.name : 'Members'}
            </CardTitle>
            {selectedId && detail && (
              <div className="flex gap-2">
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => {
                    setForm({
                      name: detail.name,
                      description: detail.description ?? '',
                      quorumRequired: detail.quorumRequired,
                      reviewDeadlineDays: detail.reviewDeadlineDays,
                      effectiveFrom: detail.effectiveFrom.slice(0, 10),
                      isActive: detail.isActive,
                    });
                    setEditingId(detail.id);
                    setCreating(true);
                  }}
                >
                  Edit
                </Button>
                <Button size="sm" variant="outline" onClick={() => setAddingMember(true)}>
                  <UserPlus className="mr-2 h-4 w-4" />
                  Add member
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setRemoving(true)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            )}
          </CardHeader>
          <CardContent className="space-y-4">
            {!selectedId ? (
              <EmptyState
                icon={Users}
                title="Choose a committee"
                description="Its members are what decide who may score a nomination assigned to it."
              />
            ) : !detail ? (
              <div className="flex items-center justify-center p-8">
                <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
              </div>
            ) : (
              <>
                {/* Quorum is checked when the committee decides — a committee that cannot reach it
                    will never produce a result, and that is worth saying before it happens. */}
                {quorate === false && (
                  <Alert>
                    <AlertTriangle className="h-4 w-4" />
                    <AlertDescription>
                      This committee is <strong>not quorate</strong> — it needs{' '}
                      {detail.quorumRequired} and has {activeMembers} active{' '}
                      {activeMembers === 1 ? 'member' : 'members'}. It cannot produce a decision
                      until that is fixed.
                    </AlertDescription>
                  </Alert>
                )}

                <div className="grid gap-4 text-sm sm:grid-cols-3">
                  <div>
                    <p className="text-xs text-muted-foreground">Quorum</p>
                    <p>{detail.quorumRequired}</p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">Review deadline</p>
                    <p>{detail.reviewDeadlineDays} days</p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">Effective from</p>
                    <p>{fmtDate(detail.effectiveFrom)}</p>
                  </div>
                </div>

                {members.length === 0 ? (
                  <EmptyState
                    icon={Users}
                    title="No members"
                    description="Nobody can score a nomination assigned to this committee until somebody is on it."
                  />
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Member</TableHead>
                        <TableHead>Number</TableHead>
                        <TableHead>Role</TableHead>
                        <TableHead>Since</TableHead>
                        <TableHead>Active</TableHead>
                        <TableHead />
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {members.map((m) => (
                        <TableRow key={m.id}>
                          <TableCell>{m.employeeName}</TableCell>
                          {/* Blank on every read until slice 12 — the mapper never set it. */}
                          <TableCell className="text-muted-foreground">
                            {m.employeeNumber ?? '—'}
                          </TableCell>
                          <TableCell>{m.role ?? '—'}</TableCell>
                          <TableCell>{fmtDate(m.startDate)}</TableCell>
                          <TableCell>
                            {m.isActive ? (
                              <Badge variant="secondary">Active</Badge>
                            ) : (
                              <span className="text-muted-foreground">—</span>
                            )}
                          </TableCell>
                          <TableCell className="text-right">
                            {m.isActive && (
                              <Button
                                size="sm"
                                variant="ghost"
                                disabled={deactivate.isPending}
                                onClick={() => deactivate.mutate(m.id)}
                              >
                                <UserMinus className="mr-2 h-4 w-4" />
                                Deactivate
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </>
            )}
          </CardContent>
        </Card>
      </div>

      <Dialog
        open={creating}
        onOpenChange={(o) => { setCreating(o); if (!o) setEditingId(null); }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editingId ? 'Edit committee' : 'New committee'}</DialogTitle>
            <DialogDescription>
              {editingId
                ? 'Changing the quorum changes what it takes for this committee to decide.'
                : 'Add its members afterwards.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="cname">Name</Label>
              <Input id="cname" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="cdesc">Description</Label>
              <Textarea id="cdesc" rows={2} value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })} />
            </div>
            <div className="grid gap-4 sm:grid-cols-3">
              <div className="space-y-2">
                <Label htmlFor="quorum">Quorum</Label>
                <Input id="quorum" type="number" min={1} value={form.quorumRequired}
                  onChange={(e) => setForm({ ...form, quorumRequired: Number(e.target.value) })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="deadline">Review days</Label>
                <Input id="deadline" type="number" min={1} value={form.reviewDeadlineDays}
                  onChange={(e) => setForm({ ...form, reviewDeadlineDays: Number(e.target.value) })} />
              </div>
              <div className="space-y-2">
                <Label htmlFor="from">Effective from</Label>
                <Input id="from" type="date" value={form.effectiveFrom}
                  onChange={(e) => setForm({ ...form, effectiveFrom: e.target.value })} />
              </div>
            </div>
            <label className="flex items-center gap-2 text-sm">
              <Switch checked={form.isActive} onCheckedChange={(v) => setForm({ ...form, isActive: v })} />
              Active
            </label>
            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => { setCreating(false); setEditingId(null); }}
              >
                Cancel
              </Button>
              <Button disabled={!form.name.trim() || create.isPending} onClick={() => create.mutate()}>
                {create.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {editingId ? 'Save' : 'Create'}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={removing}
        onOpenChange={setRemoving}
        title={`Remove ${detail?.name}?`}
        description="Nominations already assigned to it keep their assignment, and the scores its members gave stay attributable — removal flags the committee rather than erasing it."
        variant="destructive"
        confirmText="Remove"
        isLoading={removeCommittee.isPending}
        onConfirm={async () => {
          try { await removeCommittee.mutateAsync(); } catch { return false; }
        }}
      />

      <Dialog open={addingMember} onOpenChange={setAddingMember}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Add a member to {detail?.name}</DialogTitle>
            <DialogDescription>
              This is what lets them score nominations in front of this committee.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label>Employee</Label>
              <EmployeePicker
                value={member.employeeId || null}
                onChange={(id) => setMember({ ...member, employeeId: id ?? '' })}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="role">Role</Label>
              <Input id="role" maxLength={100} value={member.role}
                onChange={(e) => setMember({ ...member, role: e.target.value })}
                placeholder="Chair, Secretary, Member…" />
              <p className="text-xs text-muted-foreground">Free text — whatever the committee calls it.</p>
            </div>
            <div className="space-y-2">
              <Label htmlFor="mfrom">From</Label>
              <Input id="mfrom" type="date" value={member.startDate}
                onChange={(e) => setMember({ ...member, startDate: e.target.value })} />
            </div>
            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setAddingMember(false)}>Cancel</Button>
              <Button disabled={!member.employeeId || addMember.isPending} onClick={() => addMember.mutate()}>
                {addMember.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                Add
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
