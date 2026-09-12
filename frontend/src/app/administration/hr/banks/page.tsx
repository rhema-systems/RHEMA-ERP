'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Pencil, Plus, Power, PowerOff, Trash2 } from 'lucide-react';
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
import type { Bank } from '@/types/hr/bank';

/** Radix needs a sentinel: an empty string is not a valid SelectItem value. */
const NO_COUNTRY = '__none__';

/**
 * Banks — the reference data an employee's bank account points at.
 *
 * ⚠ **Ten write endpoints and no screen at all until now.** A bank could not be added, renamed,
 * retired or removed from the product; `POST/PUT/DELETE hr/banks` sat in the ledger's
 * confirmed-unreachable list, where both instruments agreed nothing called them.
 *
 * ⚠ **Retiring and deleting are different acts, and the screen says so.** Deactivating keeps a bank
 * on file for the accounts already pointing at it and only takes it out of pickers; deleting is for
 * one entered in error. A bank with branches should be retired, not deleted.
 */
export default function BanksPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [search, setSearch] = useState('');
  const [form, setForm] = useState<null | {
    id?: string; name: string; code: string; swiftCode: string; countryId: string; isActive: boolean;
  }>(null);
  const [removing, setRemoving] = useState<Bank | null>(null);

  const { data: banks, isLoading } = useQuery({
    queryKey: ['hr', 'banks'],
    queryFn: () => bankService.getBanks(),
  });

  const { data: countries } = useQuery({
    queryKey: ['hr', 'countries', 'all'],
    queryFn: () => countryService.getAll(),
    enabled: Boolean(form),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['hr', 'banks'] });

  const save = useMutation({
    mutationFn: () => {
      if (!form) throw new Error('Nothing to save.');
      const body = {
        name: form.name.trim(),
        code: form.code.trim().toUpperCase(),
        swiftCode: form.swiftCode.trim() || null,
        countryId: form.countryId === NO_COUNTRY || !form.countryId ? null : form.countryId,
        isActive: form.isActive,
      };
      return form.id
        ? bankService.updateBank(form.id, { ...body, id: form.id })
        : bankService.createBank(body);
    },
    onSuccess: () => {
      toast({ title: form?.id ? 'Bank saved' : 'Bank added' });
      setForm(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The bank was refused',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const setActive = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      active ? bankService.activateBank(id) : bankService.deactivateBank(id),
    onSuccess: (_d, v) => {
      toast({ title: v.active ? 'Back in use' : 'Retired from the pickers' });
      invalidate();
    },
    onError: (e: any) =>
      toast({ variant: 'destructive', title: 'That change was refused', description: e?.body?.detail ?? e?.message }),
  });

  const remove = useMutation({
    mutationFn: (id: string) => bankService.deleteBank(id),
    onSuccess: () => {
      toast({ title: 'Bank deleted' });
      setRemoving(null);
      invalidate();
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'The bank could not be deleted',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  const rows = (banks ?? []).filter((b) => {
    const q = search.trim().toLowerCase();
    if (!q) return true;
    return [b.name, b.code, b.swiftCode, b.countryName].some((v) => v?.toLowerCase().includes(q));
  });

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Banks"
        description="The banks and branches an employee's account can point at."
        backHref="/administration/hr"
        actions={
          <Button
            onClick={() =>
              setForm({ name: '', code: '', swiftCode: '', countryId: NO_COUNTRY, isActive: true })
            }
          >
            <Plus className="mr-2 h-4 w-4" />
            Add a bank
          </Button>
        }
      />

      <Card>
        <CardContent className="space-y-4 pt-6">
          <Input
            placeholder="Search by name, code, SWIFT or country"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="max-w-sm"
          />

          {isLoading ? (
            <div className="flex justify-center py-10">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : rows.length === 0 ? (
            <EmptyState
              title={search ? 'Nothing matches that' : 'No banks yet'}
              description={
                search
                  ? 'Try a different name or code.'
                  : 'Add the banks staff are paid into. Each one can carry its branches.'
              }
            />
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Bank</TableHead>
                  <TableHead>Code</TableHead>
                  <TableHead>SWIFT</TableHead>
                  <TableHead>Country</TableHead>
                  <TableHead className="text-right">Branches</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((b) => (
                  <TableRow key={b.id}>
                    <TableCell className="font-medium">
                      <Link href={`/administration/hr/banks/${b.id}`} className="hover:underline">
                        {b.name}
                      </Link>
                    </TableCell>
                    <TableCell className="font-mono text-xs">{b.code}</TableCell>
                    <TableCell className="font-mono text-xs">{b.swiftCode ?? '—'}</TableCell>
                    <TableCell>{b.countryName ?? '—'}</TableCell>
                    <TableCell className="text-right tabular-nums">{b.branchCount}</TableCell>
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
                            code: b.code,
                            swiftCode: b.swiftCode ?? '',
                            countryId: b.countryId ?? NO_COUNTRY,
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
                        title={b.isActive ? 'Retire from the pickers' : 'Put back in use'}
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
            <DialogTitle>{form?.id ? 'Edit bank' : 'Add a bank'}</DialogTitle>
            <DialogDescription>
              The code is what staff records refer to; SWIFT is for international transfers.
            </DialogDescription>
          </DialogHeader>
          {form && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="bname">Name</Label>
                <Input id="bname" value={form.name} maxLength={200}
                  onChange={(e) => setForm({ ...form, name: e.target.value })} />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="bcode">Code</Label>
                  <Input id="bcode" value={form.code} maxLength={20}
                    onChange={(e) => setForm({ ...form, code: e.target.value })} />
                  <p className="text-xs text-muted-foreground">Stored upper-case.</p>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="bswift">SWIFT</Label>
                  <Input id="bswift" value={form.swiftCode} maxLength={11}
                    onChange={(e) => setForm({ ...form, swiftCode: e.target.value })} />
                </div>
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
              <div className="flex items-center justify-between rounded-md border p-3">
                <div>
                  <Label htmlFor="bactive">In use</Label>
                  <p className="text-xs text-muted-foreground">
                    A retired bank stays on existing accounts and leaves the pickers.
                  </p>
                </div>
                <Switch id="bactive" checked={form.isActive}
                  onCheckedChange={(v) => setForm({ ...form, isActive: v })} />
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" onClick={() => setForm(null)}>Cancel</Button>
                <Button
                  disabled={!form.name.trim() || !form.code.trim() || save.isPending}
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
        title={`Delete ${removing?.name ?? 'this bank'}?`}
        description={
          removing && removing.branchCount > 0
            ? `It carries ${removing.branchCount} branch${removing.branchCount === 1 ? '' : 'es'}. Deleting is for a bank entered in error — to take it out of use while keeping the records that point at it, retire it instead.`
            : 'Deleting is for a bank entered in error. To take one out of use while keeping the records that point at it, retire it instead.'
        }
        confirmText="Delete"
        variant="destructive"
        onConfirm={() => { if (removing) remove.mutate(removing.id); }}
      />
    </div>
  );
}
