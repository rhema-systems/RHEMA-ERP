'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Download, Gift, Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
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
import { Textarea } from '@/components/ui/textarea';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { CurrencyPicker } from '@/components/hr/common/CurrencyPicker';
import { useToast } from '@/hooks/use-toast';
import { formatMoney } from '@/lib/hr/attendance-format';
import { jobOfferService } from '@/services/hr/offers.service';
import type { CreateJobOfferBenefit, JobOfferBenefit } from '@/types/hr/offers';

interface BenefitForm extends CreateJobOfferBenefit {
  id?: string;
}

const blank = (currencyCode: string | null, nextOrder: number): BenefitForm => ({
  benefitName: '',
  description: '',
  monetaryValue: null,
  currencyCode,
  isMonetary: true,
  displayOrder: nextOrder,
});

/**
 * The offer's benefit lines — what the candidate gets beyond the salary.
 *
 * Two sources, deliberately distinguished. **Import** pulls the entitlements attached to the
 * position's grade: the standard package, and idempotent, so pressing it twice adds nothing.
 * **Add** is for the line that was negotiated for this person and that no position can supply —
 * relocation, a retention bonus, a car the grade does not carry.
 *
 * ⚠ Editing is confined to Draft and Pending-Approval, matching the server. Once an offer is
 * approved the package is what was signed off, and changing it means revising the offer rather than
 * quietly editing a line. The buttons disappear rather than failing, but the server refuses anyway.
 */
export function OfferBenefitsPanel({
  offerId,
  currencyCode,
  editable,
}: {
  offerId: string;
  currencyCode?: string | null;
  editable: boolean;
}) {
  const queryClient = useQueryClient();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState<BenefitForm>(() => blank(currencyCode ?? null, 1));

  const benefits = useQuery({
    queryKey: ['hr', 'offer-benefits', offerId],
    queryFn: () => jobOfferService.getBenefits(offerId),
    enabled: !!offerId,
  });

  const rows = benefits.data ?? [];

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['hr', 'offer-benefits', offerId] });
    // The offer's own read carries the benefit list too, so it goes stale at the same moment.
    await queryClient.invalidateQueries({ queryKey: ['hr', 'offers', offerId] });
  };

  const openFor = (benefit?: JobOfferBenefit) => {
    if (benefit) {
      setForm({
        id: benefit.id,
        benefitName: benefit.benefitName,
        description: benefit.description ?? '',
        monetaryValue: benefit.monetaryValue ?? null,
        currencyCode: benefit.currencyCode ?? currencyCode ?? null,
        isMonetary: benefit.isMonetary,
        displayOrder: benefit.displayOrder,
      });
    } else {
      const nextOrder = rows.reduce((max, r) => Math.max(max, r.displayOrder), 0) + 1;
      setForm(blank(currencyCode ?? null, nextOrder));
    }
    setOpen(true);
  };

  const save = useMutation({
    mutationFn: () => {
      const payload: CreateJobOfferBenefit = {
        benefitName: form.benefitName.trim(),
        description: form.description?.trim() || null,
        // A non-monetary benefit carries no figure — sending one would render a value on a line
        // that has none ("Company car: GHS 0.00").
        monetaryValue: form.isMonetary ? (form.monetaryValue ?? null) : null,
        currencyCode: form.isMonetary ? (form.currencyCode || null) : null,
        isMonetary: form.isMonetary,
        displayOrder: form.displayOrder,
      };
      return form.id
        ? jobOfferService.updateBenefit(form.id, payload)
        : jobOfferService.addBenefit(offerId, payload);
    },
    onSuccess: async () => {
      await refresh();
      setOpen(false);
      toast({ title: form.id ? 'Benefit updated' : 'Benefit added' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not save it', description: e?.message, variant: 'destructive' }),
  });

  const remove = useMutation({
    mutationFn: (benefitId: string) => jobOfferService.removeBenefit(benefitId),
    onSuccess: async () => {
      await refresh();
      toast({ title: 'Benefit removed' });
    },
    onError: (e: any) =>
      toast({ title: 'Could not remove it', description: e?.message, variant: 'destructive' }),
  });

  const importFromGrade = useMutation({
    mutationFn: () => jobOfferService.importBenefits(offerId),
    onSuccess: async (imported) => {
      await refresh();
      toast({
        title: imported.length === 0 ? 'Nothing new to import' : `Imported ${imported.length}`,
        description:
          imported.length === 0
            ? 'The position grade offers nothing this offer does not already carry.'
            : undefined,
      });
    },
    onError: (e: any) =>
      toast({ title: 'Import failed', description: e?.message, variant: 'destructive' }),
  });

  const monetaryTotal = rows
    .filter((r) => r.isMonetary)
    .reduce((sum, r) => sum + (r.monetaryValue ?? 0), 0);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Benefits</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground">
            {rows.length === 0
              ? 'Nothing on the package yet.'
              : `${rows.length} line${rows.length === 1 ? '' : 's'}${
                  monetaryTotal > 0 ? ` · ${formatMoney(monetaryTotal, currencyCode ?? 'GHS')} of stated value` : ''
                }`}
          </p>
        </div>
        {editable && (
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="outline"
              onClick={() => importFromGrade.mutate()}
              disabled={importFromGrade.isPending}
            >
              {importFromGrade.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Download className="mr-2 h-4 w-4" />
              )}
              Import from grade
            </Button>
            <Button size="sm" onClick={() => openFor()}>
              <Plus className="mr-2 h-4 w-4" /> Add a benefit
            </Button>
          </div>
        )}
      </CardHeader>

      <CardContent className="p-0">
        {benefits.isLoading ? (
          <div className="flex items-center justify-center py-10">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : rows.length === 0 ? (
          <div className="py-8">
            <EmptyState
              icon={Gift}
              title="No benefits on this offer"
              description={
                editable
                  ? 'Import the position grade’s standard package, then add anything negotiated on top.'
                  : 'Nothing was attached to the package.'
              }
            />
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12">#</TableHead>
                <TableHead>Benefit</TableHead>
                <TableHead>Detail</TableHead>
                <TableHead className="text-right">Value</TableHead>
                {editable && <TableHead className="w-24" />}
              </TableRow>
            </TableHeader>
            <TableBody>
              {[...rows]
                .sort((a, b) => a.displayOrder - b.displayOrder)
                .map((b) => (
                  <TableRow key={b.id}>
                    <TableCell className="text-muted-foreground tabular-nums">{b.displayOrder}</TableCell>
                    <TableCell className="font-medium">{b.benefitName}</TableCell>
                    <TableCell className="max-w-[320px] text-sm text-muted-foreground">
                      {b.description || '—'}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {b.isMonetary && b.monetaryValue != null
                        ? formatMoney(b.monetaryValue, b.currencyCode ?? currencyCode ?? 'GHS')
                        : 'In kind'}
                    </TableCell>
                    {editable && (
                      <TableCell>
                        <div className="flex justify-end">
                          <Button variant="ghost" size="icon" onClick={() => openFor(b)}>
                            <Pencil className="h-4 w-4" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => remove.mutate(b.id)}
                            disabled={remove.isPending}
                          >
                            <Trash2 className="h-4 w-4" />
                          </Button>
                        </div>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
            </TableBody>
          </Table>
        )}
      </CardContent>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{form.id ? 'Edit benefit' : 'Add a benefit'}</DialogTitle>
            <DialogDescription>
              This is the line as it will read on the offer letter, so word it the way the candidate
              should see it.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-2">
            <div className="space-y-1.5">
              <Label htmlFor="benefitName">Benefit</Label>
              <Input
                id="benefitName"
                value={form.benefitName}
                onChange={(e) => setForm({ ...form, benefitName: e.target.value })}
                placeholder="e.g. Relocation allowance"
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="benefitDescription">Detail</Label>
              <Textarea
                id="benefitDescription"
                rows={2}
                value={form.description ?? ''}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                placeholder="Any conditions or timing the candidate should know about."
              />
            </div>

            <div className="flex items-center gap-2">
              <Checkbox
                id="isMonetary"
                checked={form.isMonetary}
                onCheckedChange={(checked) => setForm({ ...form, isMonetary: checked === true })}
              />
              <Label htmlFor="isMonetary" className="font-normal">
                This benefit has a stated cash value
              </Label>
            </div>

            {form.isMonetary && (
              <div className="grid grid-cols-3 gap-3">
                <div className="col-span-2 space-y-1.5">
                  <Label htmlFor="monetaryValue">Value</Label>
                  <Input
                    id="monetaryValue"
                    type="number"
                    step="0.01"
                    value={form.monetaryValue ?? ''}
                    onChange={(e) =>
                      setForm({
                        ...form,
                        monetaryValue: e.target.value === '' ? null : Number(e.target.value),
                      })
                    }
                  />
                </div>
                <div className="space-y-1.5">
                  {/* ⚠ Round 4, lane G3. A benefit line carries its own currency and was the
                      third free-text box on this path; AddBenefit and UpdateBenefit both
                      validate it server-side now. */}
                  <Label htmlFor="benefitCurrency">Currency</Label>
                  <CurrencyPicker
                    id="benefitCurrency"
                    value={form.currencyCode ?? ''}
                    onChange={(code) => setForm({ ...form, currencyCode: code })}
                    allowEmpty
                    emptyLabel="Same as the offer"
                  />
                </div>
              </div>
            )}

            <div className="space-y-1.5">
              <Label htmlFor="displayOrder">Order on the letter</Label>
              <Input
                id="displayOrder"
                type="number"
                min={1}
                value={form.displayOrder}
                onChange={(e) => setForm({ ...form, displayOrder: Number(e.target.value) || 1 })}
                className="w-32"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Cancel
            </Button>
            <Button onClick={() => save.mutate()} disabled={!form.benefitName.trim() || save.isPending}>
              {save.isPending ? 'Saving…' : form.id ? 'Save' : 'Add'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
