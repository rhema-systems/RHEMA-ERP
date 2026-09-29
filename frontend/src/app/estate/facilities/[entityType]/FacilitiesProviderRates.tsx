'use client';

import React from 'react';
import { Pencil, Plus } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { estateFacilitiesService, type FacilitiesProviderOption, type FacilitiesProviderRate, type FacilitiesProviderRateRequest } from '@/services/estate-facilities.service';

const emptyRate = (): FacilitiesProviderRateRequest => ({
  contractId: null,
  serviceName: '',
  unitOfMeasure: '',
  rate: 0,
  currency: 'GHS',
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: null,
  isActive: true,
});

export function FacilitiesProviderRates({ provider, canManage, onClose }: {
  provider: FacilitiesProviderOption | null;
  canManage: boolean;
  onClose: () => void;
}) {
  const [rates, setRates] = React.useState<FacilitiesProviderRate[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [saving, setSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [editingId, setEditingId] = React.useState<string | null>(null);
  const [formOpen, setFormOpen] = React.useState(false);
  const [form, setForm] = React.useState<FacilitiesProviderRateRequest>(emptyRate);

  React.useEffect(() => {
    if (!provider) return;
    let active = true;
    setLoading(true);
    setError(null);
    setRates([]);
    setFormOpen(false);
    void estateFacilitiesService.getProviderRates(provider.id)
      .then((items) => { if (active) setRates(items); })
      .catch(() => { if (active) setError('Unable to load service rates.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [provider]);

  const edit = (rate: FacilitiesProviderRate) => {
    setEditingId(rate.id);
    setForm({
      contractId: rate.contractId ?? null,
      serviceName: rate.serviceName,
      unitOfMeasure: rate.unitOfMeasure,
      rate: rate.rate,
      currency: rate.currency,
      effectiveFrom: rate.effectiveFrom.slice(0, 10),
      effectiveTo: rate.effectiveTo?.slice(0, 10) ?? null,
      isActive: rate.isActive,
    });
    setError(null);
    setFormOpen(true);
  };

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!provider) return;
    setSaving(true);
    setError(null);
    try {
      if (editingId) await estateFacilitiesService.updateProviderRate(provider.id, editingId, form);
      else await estateFacilitiesService.createProviderRate(provider.id, form);
      setRates(await estateFacilitiesService.getProviderRates(provider.id));
      setFormOpen(false);
      setEditingId(null);
    } catch {
      setError('Unable to save rate. Check the provider, contract, dates, and any overlapping active rates.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={provider !== null} onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent className="max-w-4xl" aria-describedby={undefined}>
        <DialogHeader><DialogTitle>{provider?.partnerName} service rates</DialogTitle></DialogHeader>
        {error ? <p role="alert" className="text-sm text-destructive">{error}</p> : null}
        {canManage && !formOpen ? (
          <div><Button size="sm" onClick={() => { setEditingId(null); setForm(emptyRate()); setFormOpen(true); }}>
            <Plus className="mr-1 h-4 w-4" /> Add rate
          </Button></div>
        ) : null}
        {formOpen ? (
          <form onSubmit={(event) => void save(event)} className="grid gap-3 border-b pb-4 sm:grid-cols-2">
            <div className="space-y-1"><Label htmlFor="provider-service">Service</Label>
              <Input id="provider-service" maxLength={160} required value={form.serviceName}
                onChange={(event) => setForm({ ...form, serviceName: event.target.value })} /></div>
            <div className="space-y-1"><Label htmlFor="provider-unit">Unit</Label>
              <Input id="provider-unit" maxLength={40} required placeholder="e.g. visit, square metre"
                value={form.unitOfMeasure} onChange={(event) => setForm({ ...form, unitOfMeasure: event.target.value })} /></div>
            <div className="space-y-1"><Label htmlFor="provider-rate">Rate</Label>
              <Input id="provider-rate" type="number" min="0.0001" step="0.0001" required value={form.rate || ''}
                onChange={(event) => setForm({ ...form, rate: Number(event.target.value) })} /></div>
            <div className="space-y-1"><Label htmlFor="provider-currency">Currency</Label>
              <Input id="provider-currency" maxLength={3} required value={form.currency}
                onChange={(event) => setForm({ ...form, currency: event.target.value.toUpperCase() })} /></div>
            <div className="space-y-1"><Label htmlFor="provider-contract">Contract</Label>
              <Select value={form.contractId || 'none'} onValueChange={(value) => {
                const contract = provider?.contracts.find((item) => item.id === value);
                setForm({ ...form, contractId: value === 'none' ? null : value,
                  effectiveTo: contract?.endDate?.slice(0, 10) ?? form.effectiveTo });
              }}>
                <SelectTrigger id="provider-contract"><SelectValue /></SelectTrigger>
                <SelectContent><SelectItem value="none">No linked contract</SelectItem>
                  {form.contractId && !provider?.contracts.some((contract) => contract.id === form.contractId)
                    ? <SelectItem value={form.contractId}>Existing contract</SelectItem> : null}
                  {provider?.contracts.map((contract) => <SelectItem key={contract.id} value={contract.id}>{contract.contractNumber}</SelectItem>)}</SelectContent>
              </Select></div>
            <div className="space-y-1"><Label htmlFor="provider-start">Effective from</Label>
              <Input id="provider-start" type="date" required value={form.effectiveFrom.slice(0, 10)}
                onChange={(event) => setForm({ ...form, effectiveFrom: event.target.value })} /></div>
            <div className="space-y-1"><Label htmlFor="provider-end">Effective to</Label>
              <Input id="provider-end" type="date" value={form.effectiveTo?.slice(0, 10) ?? ''}
                onChange={(event) => setForm({ ...form, effectiveTo: event.target.value || null })} /></div>
            <label className="flex items-center gap-2 self-end text-sm"><input type="checkbox" checked={form.isActive}
              onChange={(event) => setForm({ ...form, isActive: event.target.checked })} /> Active</label>
            <div className="flex gap-2 sm:col-span-2"><Button size="sm" type="submit" disabled={saving}>{saving ? 'Saving...' : 'Save rate'}</Button>
              <Button size="sm" type="button" variant="outline" onClick={() => setFormOpen(false)}>Cancel</Button></div>
          </form>
        ) : null}
        <div className="max-h-[55vh] overflow-auto rounded-md border">
          <Table><TableHeader><TableRow>
            <TableHead>Service</TableHead><TableHead>Unit</TableHead><TableHead className="text-right">Rate</TableHead>
            <TableHead>Contract</TableHead><TableHead>Effective</TableHead><TableHead>Status</TableHead>
            {canManage ? <TableHead className="w-12"><span className="sr-only">Edit</span></TableHead> : null}
          </TableRow></TableHeader><TableBody>
            {loading ? <TableRow><TableCell colSpan={canManage ? 7 : 6}>Loading rates...</TableCell></TableRow>
              : rates.length === 0 ? <TableRow><TableCell colSpan={canManage ? 7 : 6}>No service rates recorded.</TableCell></TableRow>
                : rates.map((rate) => <TableRow key={rate.id}>
                  <TableCell>{rate.serviceName}</TableCell><TableCell>{rate.unitOfMeasure}</TableCell>
                  <TableCell className="text-right">{rate.currency} {rate.rate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}</TableCell>
                  <TableCell>{rate.contractNumber || (rate.contractId ? 'Linked contract' : '-')}</TableCell>
                  <TableCell>{rate.effectiveFrom.slice(0, 10)}{rate.effectiveTo ? ` to ${rate.effectiveTo.slice(0, 10)}` : ''}</TableCell>
                  <TableCell>{rate.isActive ? 'Active' : 'Inactive'}</TableCell>
                  {canManage ? <TableCell><Button size="icon" variant="ghost" title={`Edit ${rate.serviceName} rate`}
                    aria-label={`Edit ${rate.serviceName} rate`} onClick={() => edit(rate)}><Pencil className="h-4 w-4" /></Button></TableCell> : null}
                </TableRow>)}
          </TableBody></Table>
        </div>
      </DialogContent>
    </Dialog>
  );
}
