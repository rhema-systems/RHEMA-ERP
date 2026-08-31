'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Phone, Plus, Star, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { locationContactService } from '@/services/hr/location-contact.service';
import type { LocationContact } from '@/types/hr/location';

/**
 * Who to ring at a location.
 *
 * ⚠ **Nothing in the product has ever written one of these.** Four endpoints, no caller, and the
 * create was broken besides — it never stamped `TenantId`, so every insert died on the tenant
 * foreign key and came back as a bare 500. Fixed 2026-08-31 and proven by
 * `hr-tierb-tail/probe-lane2-groupA.mjs` before this panel was written.
 *
 * ⚠ **A contact is either a named employee or a written-down name**, not both and not neither: the
 * DTO takes an optional `employeeId` and an optional `contactName`, and a row with neither is a
 * phone number belonging to nobody. The form insists on one of them.
 *
 * ⚠ **Primary is a promotion.** Setting it clears whoever held it, server-side, so the whole list is
 * refetched afterwards rather than the one row being patched in place.
 */
export function LocationContactsPanel({ locationId }: { locationId: string }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState<null | {
    id?: string; employeeId: string; contactName: string; phone: string; email: string; isPrimary: boolean;
  }>(null);
  const [removing, setRemoving] = useState<LocationContact | null>(null);

  const queryKey = ['hr', 'location-contacts', locationId];

  const { data: contacts, isLoading } = useQuery({
    queryKey,
    queryFn: () => locationContactService.getForLocation(locationId),
    enabled: Boolean(locationId),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey });

  const save = useMutation({
    mutationFn: () => {
      if (!form) throw new Error('Nothing to save.');
      const body = {
        locationId,
        employeeId: form.employeeId || null,
        contactName: form.contactName.trim() || null,
        phone: form.phone.trim() || null,
        email: form.email.trim() || null,
        isPrimary: form.isPrimary,
      };
      // The id goes in the body as well as the route; see the service's remark.
      return form.id
        ? locationContactService.update(form.id, { ...body, id: form.id })
        : locationContactService.create(body);
    },
    onSuccess: () => {
      toast({ title: form?.id ? 'Contact saved' : 'Contact added' });
      setForm(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The contact was refused',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const makePrimary = useMutation({
    mutationFn: (id: string) => locationContactService.setPrimary(id),
    onSuccess: () => {
      toast({ title: 'Primary contact changed' });
      invalidate();
    },
    onError: (e: any) =>
      toast({ variant: 'destructive', title: 'That change was refused', description: e?.body?.detail ?? e?.message }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => locationContactService.remove(id),
    onSuccess: () => {
      toast({ title: 'Contact removed' });
      setRemoving(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({ variant: 'destructive', title: 'It could not be removed', description: e?.body?.detail ?? e?.message }),
  });

  const rows = contacts ?? [];
  const nameOf = (c: LocationContact) => c.employeeName ?? c.contactName ?? '—';

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="flex items-center gap-2 text-base">
          <Phone className="h-4 w-4" />
          Contacts
        </CardTitle>
        <Button
          size="sm"
          variant="outline"
          onClick={() =>
            setForm({ employeeId: '', contactName: '', phone: '', email: '', isPrimary: rows.length === 0 })
          }
        >
          <Plus className="mr-2 h-4 w-4" />
          Add a contact
        </Button>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="flex justify-center py-8">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            title="No contacts"
            description="Name someone to ring at this location — an employee, or a name and number."
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Who</TableHead>
                <TableHead>Phone</TableHead>
                <TableHead>Email</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="font-medium">
                    <span className="flex items-center gap-2">
                      {nameOf(c)}
                      {c.isPrimary && <Badge variant="secondary">Primary</Badge>}
                      {c.employeeId && !c.employeeName && (
                        <span className="text-xs text-muted-foreground">(employee)</span>
                      )}
                    </span>
                  </TableCell>
                  <TableCell>{c.phone ?? '—'}</TableCell>
                  <TableCell>{c.email ?? '—'}</TableCell>
                  <TableCell className="text-right">
                    {!c.isPrimary && (
                      <Button
                        size="sm"
                        variant="ghost"
                        title="Make this the primary contact"
                        disabled={makePrimary.isPending}
                        onClick={() => makePrimary.mutate(c.id)}
                      >
                        <Star className="h-4 w-4" />
                      </Button>
                    )}
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() =>
                        setForm({
                          id: c.id,
                          employeeId: c.employeeId ?? '',
                          contactName: c.contactName ?? '',
                          phone: c.phone ?? '',
                          email: c.email ?? '',
                          isPrimary: c.isPrimary,
                        })
                      }
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => setRemoving(c)}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={Boolean(form)} onOpenChange={(o) => !o && setForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{form?.id ? 'Edit contact' : 'Add a contact'}</DialogTitle>
            <DialogDescription>
              Either name an employee or write the contact down — a number belonging to nobody helps
              no one.
            </DialogDescription>
          </DialogHeader>
          {form && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Employee</Label>
                <EmployeePicker
                  value={form.employeeId || null}
                  onChange={(v) => setForm({ ...form, employeeId: v ?? '' })}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="lcname">Or a name</Label>
                <Input id="lcname" value={form.contactName} maxLength={200}
                  onChange={(e) => setForm({ ...form, contactName: e.target.value })}
                  placeholder="The gate house, a duty officer…" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="lcphone">Phone</Label>
                  <Input id="lcphone" value={form.phone} maxLength={30}
                    onChange={(e) => setForm({ ...form, phone: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="lcemail">Email</Label>
                  <Input id="lcemail" type="email" value={form.email} maxLength={200}
                    onChange={(e) => setForm({ ...form, email: e.target.value })} />
                </div>
              </div>
              <div className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <Label htmlFor="lcprimary">Primary contact</Label>
                  <p className="text-xs text-muted-foreground">
                    Setting this takes it from whoever holds it now.
                  </p>
                </div>
                <Switch id="lcprimary" checked={form.isPrimary}
                  onCheckedChange={(v) => setForm({ ...form, isPrimary: v })} />
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setForm(null)}>Cancel</Button>
                <Button
                  disabled={save.isPending || (!form.employeeId && !form.contactName.trim())}
                  onClick={() => save.mutate()}
                >
                  {save.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  {form.id ? 'Save' : 'Add'}
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <ConfirmationDialog
        open={Boolean(removing)}
        onOpenChange={(o) => !o && setRemoving(null)}
        title={`Remove ${removing ? nameOf(removing) : 'this contact'}?`}
        description="The location keeps its other contacts."
        confirmText="Remove"
        variant="destructive"
        onConfirm={() => { if (removing) remove.mutate(removing.id); }}
        isLoading={remove.isPending}
      />
    </Card>
  );
}
