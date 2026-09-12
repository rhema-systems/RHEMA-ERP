'use client';

import { useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowRightLeft, Loader2, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useDebounce } from '@/hooks/use-debounce';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { locationService } from '@/services/hr/location.service';
import { organizationUnitService } from '@/services/hr/organization-unit.service';
import { useToast } from '@/hooks/use-toast';
import { ASSET_TRANSFER_TYPES } from '@/types/hr/assets';

const today = () => new Date().toISOString().slice(0, 10);

/**
 * Raising a transfer — area 16 slice 12b.
 *
 * ⚠ Slices 3b and 4 built the whole approve-and-complete pipeline and there was **no way to start
 * one**. The transfers list could only ever show rows a harness had made.
 *
 * ⚠ **Only the destination is asked for.** Where the asset is coming *from* is taken from the
 * register server-side and never supplied by the caller — that is what keeps a transfer honest
 * about the asset's actual position, and it is why this form has no "from" field to fill in wrong.
 *
 * ⚠ **Exactly one destination, and it must match the type.** The API refuses a mismatch in words,
 * so the form shows the one field the chosen type needs and nothing else.
 * `DepartmentToDepartment` is absent from the picker: nothing anywhere records a department, so
 * completing one would move nothing and report success.
 */
export default function NewAssetTransferPage() {
  const router = useRouter();
  const params = useSearchParams();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [assetSearch, setAssetSearch] = useState('');
  const debouncedAssetSearch = useDebounce(assetSearch, 300);
  const [form, setForm] = useState({
    assetId: params.get('assetId') ?? '',
    transferDate: today(),
    type: 'EmployeeToEmployee',
    toEmployeeId: '',
    toLocationId: '',
    toUnitId: '',
    transferReason: '',
    notes: '',
  });
  const [error, setError] = useState<string | null>(null);

  // Every asset, not only the unassigned ones: a transfer is precisely the act of moving something
  // that is currently in somebody's hands.
  const { data: assets } = useQuery({
    queryKey: ['hr', 'assets', 'register', 'transfer-picker', debouncedAssetSearch],
    queryFn: () => assetRegisterService.getAssetsPaged({
      pageSize: 25, searchTerm: debouncedAssetSearch || undefined,
    }),
  });
  const { data: units = [] } = useQuery({
    queryKey: ['hr', 'organization-units', 'summary'],
    queryFn: () => organizationUnitService.getSummary(),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const chosen = (assets?.items ?? []).find((a) => a.id === form.assetId);

  const create = useMutation({
    mutationFn: () =>
      assetRegisterService.createTransfer({
        assetId: form.assetId,
        transferDate: form.transferDate,
        type: ASSET_TRANSFER_TYPES.find((t) => t.label === form.type)?.value ?? 1,
        // Exactly one, and only the one the type implies.
        toEmployeeId: form.type === 'EmployeeToEmployee' ? form.toEmployeeId : null,
        toLocationId: form.type === 'LocationToLocation' ? form.toLocationId : null,
        toUnitId: form.type === 'UnitToUnit' ? form.toUnitId : null,
        transferReason: form.transferReason || null,
        notes: form.notes || null,
      }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      toast({ title: 'Transfer raised as a draft', description: created.transferNumber });
      router.push(`/hr/assets/transfers/${created.id}`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not raise the transfer', description: e.message, variant: 'destructive' }),
  });

  const submit = () => {
    if (!form.assetId) return setError('Choose the asset that is moving.');
    if (form.type === 'EmployeeToEmployee' && !form.toEmployeeId) {
      return setError('Say which employee it is going to.');
    }
    if (form.type === 'LocationToLocation' && !form.toLocationId) {
      return setError('Say which location it is going to.');
    }
    if (form.type === 'UnitToUnit' && !form.toUnitId) {
      return setError('Say which unit it is going to.');
    }
    setError(null);
    create.mutate();
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Raise a transfer"
        description="Move an asset to another person, location or unit."
        backHref="/hr/assets/transfers"
      />

      <Card>
        <CardHeader><CardTitle className="text-base">What is moving</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Search the register</Label>
            <div className="relative">
              <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                className="pl-8"
                placeholder="Asset number, name, serial number…"
                value={assetSearch}
                onChange={(e) => setAssetSearch(e.target.value)}
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label>Asset *</Label>
            <Select
              value={form.assetId}
              onValueChange={(v) => setForm((f) => ({ ...f, assetId: v }))}
            >
              <SelectTrigger><SelectValue placeholder="Choose an asset" /></SelectTrigger>
              <SelectContent>
                {(assets?.items ?? []).map((a) => (
                  <SelectItem key={a.id} value={a.id}>
                    {a.assetName} — {a.assetNumber}
                    {a.currentAssignedToName ? ` (held by ${a.currentAssignedToName})` : ''}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          {chosen && (
            <p className="rounded-md bg-muted p-3 text-sm text-muted-foreground">
              Currently{' '}
              {chosen.isCurrentlyAssigned
                ? <>held by <span className="font-medium">{chosen.currentAssignedToName}</span></>
                : 'in store'}
              {chosen.unitName ? `, ${chosen.unitName}` : ''}
              {chosen.locationName ? `, ${chosen.locationName}` : ''}. Where it is coming from is
              read from the register — you only say where it is going.
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Where it is going</CardTitle></CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label>Kind of move</Label>
            <Select value={form.type} onValueChange={(v) => setForm((f) => ({ ...f, type: v }))}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {ASSET_TRANSFER_TYPES.map((t) => (
                  <SelectItem key={t.label} value={t.label}>{t.text}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Dated</Label>
            <Input
              type="date"
              value={form.transferDate}
              onChange={(e) => setForm((f) => ({ ...f, transferDate: e.target.value }))}
            />
          </div>

          {form.type === 'EmployeeToEmployee' && (
            <div className="space-y-2 sm:col-span-2">
              <Label>To which employee *</Label>
              <EmployeePicker
                value={form.toEmployeeId}
                onChange={(v) => setForm((f) => ({ ...f, toEmployeeId: v ?? '' }))}
              />
              <p className="text-xs text-muted-foreground">
                Nobody may approve a transfer into their own hands — the rule is on the record and
                checked by employee id, not by login.
              </p>
            </div>
          )}
          {form.type === 'LocationToLocation' && (
            <div className="space-y-2 sm:col-span-2">
              <Label>To which location *</Label>
              <Select
                value={form.toLocationId}
                onValueChange={(v) => setForm((f) => ({ ...f, toLocationId: v }))}
              >
                <SelectTrigger><SelectValue placeholder="Choose a location" /></SelectTrigger>
                <SelectContent>
                  {locations.map((l) => <SelectItem key={l.id} value={l.id}>{l.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          )}
          {form.type === 'UnitToUnit' && (
            <div className="space-y-2 sm:col-span-2">
              <Label>To which unit *</Label>
              <Select
                value={form.toUnitId}
                onValueChange={(v) => setForm((f) => ({ ...f, toUnitId: v }))}
              >
                <SelectTrigger><SelectValue placeholder="Choose a unit" /></SelectTrigger>
                <SelectContent>
                  {units.map((u) => <SelectItem key={u.id} value={u.id}>{u.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="space-y-2 sm:col-span-2">
            <Label>Why it is moving</Label>
            <Textarea
              rows={2}
              value={form.transferReason}
              onChange={(e) => setForm((f) => ({ ...f, transferReason: e.target.value }))}
            />
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label>Notes</Label>
            <Textarea
              rows={2}
              value={form.notes}
              onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
            />
          </div>
        </CardContent>
      </Card>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex items-center justify-between gap-4">
        <p className="text-sm text-muted-foreground">
          It is raised as a draft. Sending it for approval, and completing it once approved, are
          separate steps — and completion is what actually moves the asset.
        </p>
        <Button onClick={submit} disabled={create.isPending}>
          {create.isPending
            ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            : <ArrowRightLeft className="mr-2 h-4 w-4" />}
          Raise transfer
        </Button>
      </div>
    </div>
  );
}
