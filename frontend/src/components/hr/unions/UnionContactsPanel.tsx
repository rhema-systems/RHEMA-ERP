'use client';

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus, Star, Trash2, UserRound } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
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
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { useToast } from '@/hooks/use-toast';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { unionService } from '@/services/hr/union.service';
import type { UnionContact } from '@/types/hr/union';

interface UnionContactsPanelProps {
  unionId: string;
  unionName: string;
  canWrite: boolean;
  /** Called after any write so the parent can refetch the union (its contact trio mirrors the primary). */
  onChanged?: () => Promise<unknown> | void;
}

type Kind = 'internal' | 'external';

/**
 * The people the company deals with at a union (round 3, lane U; decision D-8).
 *
 * A contact is one of our employees who holds a union office — a shop steward, a branch chair — OR
 * an external person at the union's own office. Exactly one is primary while any exist, and the
 * union's legacy contact-person/email/phone trio is a mirror of that row: the server rewrites the
 * trio on every contact save, so the job description and the offer letter that still read the trio
 * keep working without a change.
 *
 * ⚠ The server's rules, shown here so the screen does not fight them: the first contact becomes
 * primary whatever the switch said; a new primary demotes the old one; the ONLY primary cannot be
 * demoted (400 in words — make another one primary first); deleting the primary promotes the
 * oldest remaining contact.
 */
export function UnionContactsPanel({ unionId, unionName, canWrite, onChanged }: UnionContactsPanelProps) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const queryKey = ['hr', 'unions', unionId, 'contacts'] as const;

  const { data, isLoading } = useQuery({ queryKey, queryFn: () => unionService.getContacts(unionId) });
  const contacts = data ?? [];

  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<UnionContact | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<UnionContact | null>(null);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    await onChanged?.();
  };

  const remove = useMutation({
    mutationFn: (id: string) => unionService.removeContact(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Contact removed' });
      setDeleteTarget(null);
    },
    onError: (e: unknown) =>
      toast({
        variant: 'destructive',
        title: 'Could not remove the contact',
        description: e instanceof Error ? e.message : 'The server refused the change.',
      }),
  });

  return (
    <>
      <Card>
        <CardHeader className="flex flex-row items-start justify-between space-y-0">
          <div>
            <CardTitle>Contacts</CardTitle>
            <CardDescription>
              Who to speak to at {unionName}: an employee holding a union office, or someone at the
              union&apos;s own office. The primary contact is the one printed on the union&apos;s record.
            </CardDescription>
          </div>
          {canWrite && (
            <Button
              size="sm"
              onClick={() => {
                setEditing(null);
                setOpen(true);
              }}
            >
              <Plus className="mr-2 h-4 w-4" /> Add contact
            </Button>
          )}
        </CardHeader>
        <CardContent>
          <div className="rounded-md border">
            {isLoading ? (
              <div className="space-y-2 p-4">
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
              </div>
            ) : contacts.length === 0 ? (
              <EmptyState
                icon={UserRound}
                title="No contacts recorded"
                description="Add the shop steward or the union's general secretary. The first contact becomes the primary one."
              />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Person</TableHead>
                    <TableHead>Role</TableHead>
                    <TableHead>Reach</TableHead>
                    <TableHead className="w-[120px]"></TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {contacts.map((c) => (
                    <TableRow key={c.id} data-testid="union-contact-row">
                      <TableCell>
                        <div className="flex flex-col">
                          <span className="flex items-center gap-2 font-medium">
                            {c.displayName}
                            {c.isPrimary && (
                              <Badge className="bg-emerald-100 text-emerald-800 hover:bg-emerald-100">
                                <Star className="mr-1 h-3 w-3" /> Primary
                              </Badge>
                            )}
                          </span>
                          <span className="text-xs text-muted-foreground">
                            {c.isInternal
                              ? `Employee${c.employeeNumber ? ` · ${c.employeeNumber}` : ''}`
                              : 'External'}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell>{c.role}</TableCell>
                      <TableCell className="text-sm text-muted-foreground">
                        <div className="flex flex-col">
                          {c.email && <span>{c.email}</span>}
                          {c.phone && <span className="text-xs">{c.phone}</span>}
                          {!c.email && !c.phone && '—'}
                        </div>
                      </TableCell>
                      <TableCell>
                        {canWrite && (
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => {
                                setEditing(c);
                                setOpen(true);
                              }}
                            >
                              <Pencil className="h-4 w-4" />
                              <span className="sr-only">Edit</span>
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-destructive hover:text-destructive"
                              onClick={() => setDeleteTarget(c)}
                            >
                              <Trash2 className="h-4 w-4" />
                              <span className="sr-only">Remove</span>
                            </Button>
                          </div>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </div>
        </CardContent>
      </Card>

      <UnionContactDialog
        unionId={unionId}
        contact={editing}
        isFirst={contacts.length === 0}
        open={open}
        onOpenChange={setOpen}
        onSaved={refresh}
      />

      <ConfirmationDialog
        open={deleteTarget !== null}
        onOpenChange={(o) => !o && setDeleteTarget(null)}
        title="Remove contact"
        description={
          deleteTarget
            ? deleteTarget.isPrimary && contacts.length > 1
              ? `Remove ${deleteTarget.displayName}? They are the primary contact; the oldest remaining contact becomes primary.`
              : `Remove ${deleteTarget.displayName} from ${unionName}'s contacts?`
            : ''
        }
        confirmText="Remove"
        variant="destructive"
        isLoading={remove.isPending}
        onConfirm={async () => {
          if (!deleteTarget) return false;
          await remove.mutateAsync(deleteTarget.id).catch(() => undefined);
          return true;
        }}
      />
    </>
  );
}

function UnionContactDialog({
  unionId,
  contact,
  isFirst,
  open,
  onOpenChange,
  onSaved,
}: {
  unionId: string;
  contact: UnionContact | null;
  isFirst: boolean;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => Promise<unknown> | void;
}) {
  const { toast } = useToast();
  const [kind, setKind] = useState<Kind>('internal');
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [externalName, setExternalName] = useState('');
  const [role, setRole] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [isPrimary, setIsPrimary] = useState(false);
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (!open) return;
    setKind(contact ? (contact.isInternal ? 'internal' : 'external') : 'internal');
    setEmployeeId(contact?.employeeId ?? null);
    setEmployeeLabel(contact?.isInternal ? contact.displayName : null);
    setExternalName(contact?.externalName ?? '');
    setRole(contact?.role ?? '');
    setEmail(contact?.email ?? '');
    setPhone(contact?.phone ?? '');
    setIsPrimary(contact?.isPrimary ?? isFirst);
    setNotes(contact?.notes ?? '');
  }, [open, contact, isFirst]);

  const personChosen = kind === 'internal' ? !!employeeId : externalName.trim().length > 0;
  const canSave = personChosen && role.trim().length > 0;

  const save = useMutation({
    mutationFn: () => {
      const body = {
        employeeId: kind === 'internal' ? employeeId : null,
        externalName: kind === 'external' ? externalName.trim() : null,
        email: email.trim() || null,
        phone: phone.trim() || null,
        role: role.trim(),
        isPrimary,
        notes: notes.trim() || null,
      };
      return contact
        ? unionService.updateContact(contact.id, { id: contact.id, ...body })
        : unionService.addContact(unionId, body);
    },
    onSuccess: async () => {
      await onSaved();
      toast({ title: contact ? 'Contact updated' : 'Contact added' });
      onOpenChange(false);
    },
    // The server's refusals are sentences ("Make another contact the primary first…"); show them.
    onError: (e: unknown) =>
      toast({
        variant: 'destructive',
        title: contact ? 'Could not update the contact' : 'Could not add the contact',
        description: e instanceof Error ? e.message : 'The server refused the change.',
      }),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle>{contact ? 'Edit contact' : 'New contact'}</DialogTitle>
          <DialogDescription>
            One of our employees who holds a union office, or a person at the union&apos;s office.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="grid grid-cols-2 gap-2 rounded-md border p-1">
            {(
              [
                ['internal', 'An employee of ours'],
                ['external', 'An external person'],
              ] as [Kind, string][]
            ).map(([value, label]) => (
              <Button
                key={value}
                type="button"
                variant={kind === value ? 'default' : 'ghost'}
                size="sm"
                onClick={() => setKind(value)}
                data-testid={`union-contact-kind-${value}`}
              >
                {label}
              </Button>
            ))}
          </div>

          {kind === 'internal' ? (
            <div className="space-y-2">
              <Label>Employee</Label>
              <EmployeePicker
                value={employeeId}
                initialLabel={employeeLabel}
                onChange={(id, label) => {
                  setEmployeeId(id);
                  setEmployeeLabel(label);
                }}
                placeholder="Search by name or staff number…"
              />
              <p className="text-xs text-muted-foreground">
                Their name comes from the employee record; email and phone below are for union
                business and may differ from the ones on file.
              </p>
            </div>
          ) : (
            <div className="space-y-2">
              <Label htmlFor="uc-name">Name</Label>
              <Input
                id="uc-name"
                placeholder="Kofi Mensah"
                value={externalName}
                onChange={(e) => setExternalName(e.target.value)}
                maxLength={200}
              />
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="uc-role">Role at the union</Label>
            <Input
              id="uc-role"
              placeholder="Shop Steward · Branch Chair · General Secretary"
              value={role}
              onChange={(e) => setRole(e.target.value)}
              maxLength={100}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="uc-email">Email</Label>
              <Input
                id="uc-email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                maxLength={150}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="uc-phone">Phone</Label>
              <Input
                id="uc-phone"
                placeholder="+233 …"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                maxLength={50}
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="uc-notes">Notes</Label>
            <Textarea
              id="uc-notes"
              rows={2}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              maxLength={500}
              placeholder="Availability, which branch, anything worth knowing before the call."
            />
          </div>

          <div className="flex items-center justify-between rounded-md border p-4">
            <div className="space-y-0.5">
              <Label htmlFor="uc-primary">Primary contact</Label>
              <p className="text-xs text-muted-foreground">
                {isFirst && !contact
                  ? 'The first contact is always the primary one.'
                  : 'Making this person primary demotes the current one. The union record prints the primary contact.'}
              </p>
            </div>
            <Switch
              id="uc-primary"
              checked={isPrimary}
              disabled={isFirst && !contact}
              onCheckedChange={setIsPrimary}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={save.isPending}>
            Cancel
          </Button>
          <Button onClick={() => save.mutate()} disabled={!canSave || save.isPending}>
            {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {contact ? 'Save changes' : 'Add contact'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
