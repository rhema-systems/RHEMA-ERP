'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Boxes, Loader2, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useDebounce } from '@/hooks/use-debounce';
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { locationService } from '@/services/hr/location.service';
import { useToast } from '@/hooks/use-toast';
import { ASSET_CONDITIONS } from '@/types/hr/assets';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

const NONE = '__none__';
const fmtNum = (v?: number | null) =>
  v === null || v === undefined ? '—' : v.toLocaleString(undefined, { maximumFractionDigits: 2 });

/**
 * Registering an HR asset from a Finance fixed asset — AST-11, decision D1.
 *
 * ⚠ Slice 2b built this whole seam — the picker, the link, the boundary from both sides — and
 * **nothing in the UI called it**. `fixed-assets/linkable` was in the service and used by no screen.
 *
 * ⚠ **Rows another HR asset already claims are shown, greyed out, not filtered away.** That is the
 * backend's choice and this screen honours it: somebody hunting for a laptop that is already
 * registered needs to see *why* it is unavailable, not an empty list.
 *
 * ⚠ **HR's asset type is asked for, never derived from Finance's category.** Finance's categories
 * are accounting classes — "Office Equipment" is a depreciation rate, not a thing you hand to a
 * person. The money stays Finance's: the resulting asset is `source: FixedAssetsModule` and HR
 * refuses to edit its purchase figures.
 */
export default function RegisterFromFixedAssetPage() {
  const router = useRouter();
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 300);
  const [form, setForm] = useState({
    fixedAssetId: '',
    assetTypeId: '',
    assetTag: '',
    condition: 'Good',
    locationId: NONE,
    unitId: NONE,
    isAssignable: true,
    additionalRemarks: '',
  });
  const [error, setError] = useState<string | null>(null);

  const { data: linkable = [], isLoading } = useQuery({
    queryKey: ['hr', 'assets', 'linkable-fixed', debouncedSearch],
    queryFn: () => assetRegisterService.getLinkableFixedAssets(debouncedSearch || undefined),
  });
  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'assets', 'types'],
    queryFn: () => assetRegisterService.getTypes(),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const chosen = linkable.find((f) => f.id === form.fixedAssetId);

  const create = useMutation({
    mutationFn: () =>
      assetRegisterService.createFromFixedAsset({
        fixedAssetId: form.fixedAssetId,
        assetTypeId: form.assetTypeId,
        assetTag: form.assetTag.trim(),
        condition: ASSET_CONDITIONS.find((c) => c.label === form.condition)?.value ?? 2,
        locationId: form.locationId === NONE ? null : form.locationId,
        unitId: form.unitId === NONE ? null : form.unitId,
        isAssignable: form.isAssignable,
        additionalRemarks: form.additionalRemarks.trim() || null,
      }),
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['hr', 'assets'] });
      toast({ title: 'Registered from fixed assets', description: created.assetNumber });
      router.push(`/hr/assets/register/${created.id}`);
    },
    onError: (e: Error) =>
      toast({ title: 'Could not register it', description: e.message, variant: 'destructive' }),
  });

  const submit = () => {
    if (!form.fixedAssetId) return setError('Choose the fixed asset to register.');
    if (!form.assetTypeId) return setError('Choose an HR asset type.');
    setError(null);
    create.mutate();
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Register from fixed assets"
        description="Take an asset Finance already owns onto the HR register, so it can be issued to somebody."
        backHref="/hr/assets/register"
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Finance&rsquo;s register</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="relative">
            <Search className="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              className="pl-8"
              placeholder="Asset code, name, serial number…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          {isLoading ? (
            <div className="flex justify-center p-8">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
            </div>
          ) : linkable.length === 0 ? (
            <EmptyState
              icon={Boxes}
              title="Nothing to register"
              description="Finance's fixed-asset register holds no matching row."
            />
          ) : (
            <div className="max-h-96 overflow-y-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead />
                    <TableHead>Asset</TableHead>
                    <TableHead>Category</TableHead>
                    <TableHead>Serial</TableHead>
                    <TableHead className="text-right">Net book value</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {linkable.map((f) => (
                    <TableRow
                      key={f.id}
                      className={f.alreadyLinked
                        ? 'opacity-50'
                        : form.fixedAssetId === f.id ? 'bg-muted' : 'cursor-pointer'}
                      onClick={() => !f.alreadyLinked
                        && setForm((s) => ({ ...s, fixedAssetId: f.id }))}
                    >
                      <TableCell>
                        <Checkbox
                          checked={form.fixedAssetId === f.id}
                          disabled={f.alreadyLinked}
                          onCheckedChange={(c) => setForm((s) => ({
                            ...s, fixedAssetId: c === true ? f.id : '',
                          }))}
                        />
                      </TableCell>
                      <TableCell>
                        <div className="font-medium">{f.name}</div>
                        <div className="text-xs text-muted-foreground">{f.assetCode}</div>
                      </TableCell>
                      <TableCell>{f.categoryName ?? '—'}</TableCell>
                      <TableCell>{f.serialNumber ?? '—'}</TableCell>
                      <TableCell className="text-right">{fmtNum(f.netBookValue)}</TableCell>
                      <TableCell>
                        {f.alreadyLinked
                          ? <span className="text-muted-foreground">Already on the HR register</span>
                          : f.statusName ?? '—'}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What HR needs to know about it</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          {chosen && (
            <p className="sm:col-span-2 rounded-md bg-muted p-3 text-sm text-muted-foreground">
              Registering <span className="font-medium">{chosen.name}</span> ({chosen.assetCode}).
              Its purchase figures, depreciation and disposal stay with Finance — HR records who is
              holding it.
            </p>
          )}
          <div className="space-y-2">
            <Label>HR asset type *</Label>
            <Select
              value={form.assetTypeId}
              onValueChange={(v) => setForm((f) => ({ ...f, assetTypeId: v }))}
            >
              <SelectTrigger><SelectValue placeholder="Choose a type" /></SelectTrigger>
              <SelectContent>
                {types.map((t) => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}
              </SelectContent>
            </Select>
            <p className="text-xs text-muted-foreground">
              HR&rsquo;s own classification. Finance&rsquo;s category is an accounting class and does
              not map onto the things HR issues.
            </p>
          </div>
          <div className="space-y-2">
            <Label>Condition</Label>
            <Select
              value={form.condition}
              onValueChange={(v) => setForm((f) => ({ ...f, condition: v }))}
            >
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {ASSET_CONDITIONS.map((c) =>
                  <SelectItem key={c.label} value={c.label}>{c.text}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Asset tag</Label>
            <Input
              value={form.assetTag}
              onChange={(e) => setForm((f) => ({ ...f, assetTag: e.target.value }))}
            />
          </div>
          <div className="space-y-2">
            <OrganizationUnitPicker
              value={form.unitId === NONE ? '' : form.unitId}
              onChange={(id) => setForm((f) => ({ ...f, unitId: id || NONE }))}
              allowNone="Not set"
              unitLabel="Organisation unit"
              idPrefix="asset-unit"
            />
          </div>
          <div className="space-y-2">
            <Label>Location</Label>
            <Select
              value={form.locationId}
              onValueChange={(v) => setForm((f) => ({ ...f, locationId: v }))}
            >
              <SelectTrigger><SelectValue placeholder="Not set" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>Not set</SelectItem>
                {locations.map((l) => <SelectItem key={l.id} value={l.id}>{l.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="flex items-center gap-2 sm:col-span-2">
            <Checkbox
              id="isAssignable"
              checked={form.isAssignable}
              onCheckedChange={(c) => setForm((f) => ({ ...f, isAssignable: c === true }))}
            />
            <Label htmlFor="isAssignable" className="font-normal">
              Can be issued to an employee
            </Label>
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label>Additional remarks</Label>
            <Textarea
              rows={2}
              value={form.additionalRemarks}
              onChange={(e) => setForm((f) => ({ ...f, additionalRemarks: e.target.value }))}
            />
          </div>
        </CardContent>
      </Card>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex justify-end">
        <Button onClick={submit} disabled={create.isPending}>
          {create.isPending
            ? <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            : <Boxes className="mr-2 h-4 w-4" />}
          Register on the HR register
        </Button>
      </div>
    </div>
  );
}
