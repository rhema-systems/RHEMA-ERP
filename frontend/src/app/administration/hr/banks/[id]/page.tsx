'use client';

import { useState } from 'react';
import { useParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, MapPin, Pencil, Plus, Power, PowerOff, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { bankService } from '@/services/hr/bank.service';
import { countryService } from '@/services/hr/country.service';
import type { BankBranch } from '@/types/hr/bank';

const NO_COUNTRY = '__none__';

/**
 * A bank's branches.
 *
 * ⚠ Note the asymmetry in the routes, which is the controller's and not a mistake here: a branch is
 * CREATED under its bank (`hr/banks/{bankId}/branches`) and then addressed on its own
 * (`hr/bank-branches/{id}`). The service keeps both shapes rather than pretending to one base.
 */
export default function BankBranchesPage() {
  const params = useParams();
  const bankId = String(params?.id ?? '');
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [form, setForm] = useState<null | {
    id?: string; name: string; code: string; address: string; city: string;
    countryId: string; phoneNumber: string; email: string; isActive: boolean;
  }>(null);
  const [removing, setRemoving] = useState<BankBranch | null>(null);

  const { data: bank } = useQuery({
    queryKey: ['hr', 'banks', bankId],
    queryFn: () => bankService.getBank(bankId),
    enabled: Boolean(bankId),
  });

  const { data: branches, isLoading } = useQuery({
    queryKey: ['hr', 'banks', bankId, 'branches'],
    queryFn: () => bankService.getBranches(bankId),
    enabled: Boolean(bankId),
  });

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'all'],
    queryFn: () => countryService.getAll(),
    enabled: Boolean(form),
  });

  // The bank's own row carries branchCount, so it has to be refetched too when a branch appears.
  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['hr', 'banks', bankId, 'branches'] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'banks', bankId] });
    queryClient.invalidateQueries({ queryKey: ['hr', 'banks'] });
  };

  const save = useMutation({
    mutationFn: () => {
      if (!form) throw new Error('Nothing to save.');
      const body = {
        name: form.name.trim(),
        code: form.code.trim() || null,
        address: form.address.trim() || null,
        city: form.city.trim() || null,
        countryId: form.countryId === NO_COUNTRY || !form.countryId ? null : form.countryId,
        phoneNumber: form.phoneNumber.trim() || null,
        email: form.email.trim() || null,
        isActive: form.isActive,
      };
      return form.id
        ? bankService.updateBranch(form.id, { ...body, id: form.id })
        : bankService.createBranch(bankId, { ...body, bankId });
    },
    onSuccess: () => {
      toast({ title: form?.id ? 'Branch saved' : 'Branch added' });
      setForm(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The branch was refused',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const setActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      active ? bankService.activateBranch(id) : bankService.deactivateBranch(id),
    onSuccess: () => invalidate(),
    onError: (e: any) =>
      toast({ variant: 'destructive', title: 'That change was refused', description: e?.body?.detail ?? e?.message }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => bankService.deleteBranch(id),
    onSuccess: () => {
      toast({ title: 'Branch deleted' });
      setRemoving(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The branch could not be deleted',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const rows = branches ?? [];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={bank ? `${bank.name} — branches` : 'Branches'}
        description="Where an account is held. A branch can be retired without losing the accounts that name it."
        backHref="/administration/hr/banks"
        actions={
          <Button
            onClick={() =>
              setForm({
                name: '', code: '', address: '', city: '',
                countryId: bank?.countryId ?? NO_COUNTRY, phoneNumber: '', email: '', isActive: true,
              })
            }
          >
            <Plus className="mr-2 h-4 w-4" />
            Add a branch
          </Button>
        }
      />

      <Card>
        <CardContent className="pt-6">
          {isLoading ? (
            <div className="flex justify-center py-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              title="No branches yet"
              description="Add the branches staff accounts are held at."
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Branch</TableHead>
                  <TableHead>Code</TableHead>
                  <TableHead>Where</TableHead>
                  <TableHead>Contact</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((b) => (
                  <TableRow key={b.id}>
                    <TableCell className="font-medium">
                      <span className="flex items-center gap-2">
                        <MapPin className="h-3.5 w-3.5 text-muted-foreground" />
                        {b.name}
                      </span>
                    </TableCell>
                    <TableCell className="font-mono text-xs">{b.code ?? '—'}</TableCell>
                    <TableCell>
                      <div>{b.city ?? '—'}</div>
                      {b.address && (
                        <div className="text-xs text-muted-foreground">{b.address}</div>
                      )}
                    </TableCell>
                    <TableCell>
                      <div>{b.phoneNumber ?? '—'}</div>
                      {b.email && <div className="text-xs text-muted-foreground">{b.email}</div>}
                    </TableCell>
                    <TableCell>
                      {b.isActive ? (
                        <Badge variant="secondary">Active</Badge>
                      ) : (
                        <span className="text-muted-foreground">Retired</span>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() =>
                          setForm({
                            id: b.id,
                            name: b.name,
                            code: b.code ?? '',
                            address: b.address ?? '',
                            city: b.city ?? '',
                            countryId: b.countryId ?? NO_COUNTRY,
                            phoneNumber: b.phoneNumber ?? '',
                            email: b.email ?? '',
                            isActive: b.isActive,
                          })
                        }
                      >
                        <Pencil className="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={setActive.isPending}
                        onClick={() => setActive.mutate({ id: b.id, active: !b.isActive })}
                        title={b.isActive ? 'Retire this branch' : 'Put it back in use'}
                      >
                        {b.isActive ? <PowerOff className="h-4 w-4" /> : <Power className="h-4 w-4" />}
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setRemoving(b)}>
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

      <Dialog open={Boolean(form)} onOpenChange={(o) => !o && setForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{form?.id ? 'Edit branch' : 'Add a branch'}</DialogTitle>
            <DialogDescription>Only the name is required.</DialogDescription>
          </DialogHeader>
          {form && (
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="brname">Name</Label>
                  <Input id="brname" value={form.name} maxLength={200}
                    onChange={(e) => setForm({ ...form, name: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="brcode">Code</Label>
                  <Input id="brcode" value={form.code} maxLength={20}
                    onChange={(e) => setForm({ ...form, code: e.target.value })} />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="braddr">Address</Label>
                <Input id="braddr" value={form.address} maxLength={300}
                  onChange={(e) => setForm({ ...form, address: e.target.value })} />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="brcity">City</Label>
                  <Input id="brcity" value={form.city} maxLength={100}
                    onChange={(e) => setForm({ ...form, city: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label>Country</Label>
                  <Select value={form.countryId} onValueChange={(v) => setForm({ ...form, countryId: v })}>
                    <SelectTrigger><SelectValue placeholder="Not stated" /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value={NO_COUNTRY}>Not stated</SelectItem>
                      {(countries ?? []).map((c) => (
                        <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="brphone">Phone</Label>
                  <Input id="brphone" value={form.phoneNumber} maxLength={30}
                    onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })} />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="bremail">Email</Label>
                  <Input id="bremail" type="email" value={form.email} maxLength={200}
                    onChange={(e) => setForm({ ...form, email: e.target.value })} />
                </div>
              </div>
              <div className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <Label htmlFor="bractive">In use</Label>
                  <p className="text-xs text-muted-foreground">
                    A retired branch stays on existing accounts and leaves the pickers.
                  </p>
                </div>
                <Switch id="bractive" checked={form.isActive}
                  onCheckedChange={(v) => setForm({ ...form, isActive: v })} />
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setForm(null)}>Cancel</Button>
                <Button disabled={!form.name.trim() || save.isPending} onClick={() => save.mutate()}>
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
        title={`Delete ${removing?.name ?? 'this branch'}?`}
        description="Deleting is for a branch entered in error. To take one out of use while keeping the accounts that name it, retire it instead."
        confirmText="Delete"
        variant="destructive"
        onConfirm={() => { if (removing) remove.mutate(removing.id); }}
      />
    </div>
  );
}
