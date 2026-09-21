'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Plus, Trash2 } from 'lucide-react';
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { SupplierPicker } from '@/components/hr/common/SupplierPicker';
import { useToast } from '@/hooks/use-toast';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import { preEmploymentCheckService } from '@/services/hr/offers.service';
import { PRE_EMPLOYMENT_CHECK_TYPES, type PreEmploymentCheckType } from '@/types/hr/offers';

/**
 * Which suppliers provide which pre-employment checks (round 3, lane G; register row R-7;
 * decision D-14). The check screens' provider dropdown reads this: choose the check type and the
 * providers set up for it are offered. Procurement's supplier record is untouched — this only says
 * what a supplier does for HR.
 */
export function CheckProvidersPanel({ canManage }: { canManage: boolean }) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [supplierId, setSupplierId] = useState<string | null>(null);
  const [supplierName, setSupplierName] = useState('');
  const [types, setTypes] = useState<PreEmploymentCheckType[]>([]);
  const [notes, setNotes] = useState('');

  const providers = useQuery({
    queryKey: ['hr', 'pre-employment-providers', 'all'],
    queryFn: () => preEmploymentCheckService.getProviders(undefined, true),
  });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['hr', 'pre-employment-providers'] });

  const add = useMutation({
    mutationFn: () => preEmploymentCheckService.addProviders({ supplierId: supplierId!, checkTypes: types, notes: notes.trim() || null }),
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      setSupplierId(null);
      setSupplierName('');
      setTypes([]);
      setNotes('');
      toast({ title: 'Provider set up' });
    },
    onError: (e: any) => toast({ title: 'Could not set it up', description: e?.data?.message ?? e?.message, variant: 'destructive' }),
  });
  const remove = useMutation({
    mutationFn: (id: string) => preEmploymentCheckService.removeProvider(id),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Removed' });
    },
    onError: (e: any) => toast({ title: 'Could not remove it', description: e?.data?.message ?? e?.message, variant: 'destructive' }),
  });

  // Grouped by supplier for the table: one row per supplier, its checks as badges.
  const rows = providers.data ?? [];
  const bySupplier = new Map<string, { name: string; code: string; active: boolean; items: typeof rows }>();
  for (const r of rows) {
    const g = bySupplier.get(r.supplierId) ?? { name: r.supplierName, code: r.supplierCode, active: r.supplierIsActive, items: [] };
    g.items.push(r);
    bySupplier.set(r.supplierId, g);
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Check service providers</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            Suppliers from the Procurement register and the checks each provides. The check screens
            offer them by check type.
          </p>
        </div>
        {canManage && (
          <Button size="sm" onClick={() => setOpen(true)}>
            <Plus className="mr-2 h-4 w-4" /> Add a provider
          </Button>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {providers.isLoading ? (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : bySupplier.size === 0 ? (
          <div className="py-6">
            <EmptyState title="No providers yet" description="Say which suppliers provide medical examinations, police clearance, background checks and the rest." />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Supplier</TableHead>
                <TableHead>Provides</TableHead>
                {canManage && <TableHead className="w-24" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {[...bySupplier.entries()].map(([id, g]) => (
                <TableRow key={id}>
                  <TableCell className="font-medium">
                    {g.name} <span className="text-xs text-muted-foreground">{g.code}</span>
                    {!g.active && <Badge variant="outline" className="ml-2">Inactive supplier</Badge>}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-wrap gap-1.5">
                      {g.items.map((r) => (
                        <Badge key={r.id} variant="secondary" className="gap-1 pr-1" title={r.notes ?? undefined}>
                          {humanizeEnum(r.checkType)}
                          {canManage && (
                            <button type="button" className="rounded-full hover:bg-muted" aria-label={`Remove ${humanizeEnum(r.checkType)}`} onClick={() => remove.mutate(r.id)}>
                              <Trash2 className="h-3 w-3" />
                            </button>
                          )}
                        </Badge>
                      ))}
                    </div>
                  </TableCell>
                  {canManage && <TableCell />}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Add a check provider</DialogTitle>
            <DialogDescription>Pick the supplier, then tick the checks it provides.</DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label>Supplier</Label>
              <SupplierPicker
                value={supplierId}
                onChange={(id, supplier) => {
                  setSupplierId(id);
                  setSupplierName(supplier?.name ?? '');
                }}
              />
              {supplierName && <p className="text-xs text-muted-foreground">{supplierName}</p>}
            </div>
            <div className="space-y-1.5">
              <Label>Provides</Label>
              <div className="grid grid-cols-2 gap-2">
                {PRE_EMPLOYMENT_CHECK_TYPES.map((t) => (
                  <label key={t} className="flex items-center gap-2 text-sm">
                    <Checkbox
                      checked={types.includes(t)}
                      onCheckedChange={(c) => setTypes((cur) => (c === true ? [...cur, t] : cur.filter((x) => x !== t)))}
                    />
                    {humanizeEnum(t)}
                  </label>
                ))}
              </div>
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="provider-notes">Notes</Label>
              <Input id="provider-notes" value={notes} maxLength={500} placeholder="Optional — turnaround, contact, account number" onChange={(e) => setNotes(e.target.value)} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>Cancel</Button>
            <Button onClick={() => add.mutate()} disabled={!supplierId || types.length === 0 || add.isPending}>
              {add.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
