'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
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
import { assetRegisterService } from '@/services/hr/asset-register.service';
import { locationService } from '@/services/hr/location.service';
import { ASSET_CONDITIONS, COMPANY_ASSET_STATUSES } from '@/types/hr/assets';
import type { CompanyAsset, UpdateCompanyAssetRequest } from '@/types/hr/assets';
import { OrganizationUnitPicker } from '@/components/hr/common/OrganizationUnitPicker';

const NONE = '__none__';

/** The shape the form edits — every field a string, converted once on submit. */
export interface AssetFormValues {
  assetNumber: string;
  assetTag: string;
  assetName: string;
  description: string;
  additionalRemarks: string;
  assetTypeId: string;
  manufacturer: string;
  modelNumber: string;
  serialNumber: string;
  purchaseDate: string;
  purchaseCost: string;
  supplier: string;
  invoiceNumber: string;
  hasWarranty: boolean;
  warrantyStartDate: string;
  warrantyEndDate: string;
  warrantyProvider: string;
  color: string;
  size: string;
  specifications: string;
  status: string;
  condition: string;
  locationId: string;
  locationDetails: string;
  unitId: string;
  isAssignable: boolean;
  requiresRegularMaintenance: boolean;
  maintenanceIntervalDays: string;
  nextMaintenanceDate: string;
  isInsured: boolean;
  insurancePolicyNumber: string;
  insuredValue: string;
  insuranceExpiryDate: string;
  isRentable: boolean;
  standardRentalAmount: string;
  rentalCurrencyCode: string;
}

export const EMPTY_ASSET_FORM: AssetFormValues = {
  assetNumber: '', assetTag: '', assetName: '', description: '', additionalRemarks: '',
  assetTypeId: '', manufacturer: '', modelNumber: '', serialNumber: '',
  purchaseDate: '', purchaseCost: '', supplier: '', invoiceNumber: '',
  hasWarranty: false, warrantyStartDate: '', warrantyEndDate: '', warrantyProvider: '',
  color: '', size: '', specifications: '',
  status: 'Available', condition: 'Good',
  locationId: NONE, locationDetails: '', unitId: NONE,
  isAssignable: true,
  requiresRegularMaintenance: false, maintenanceIntervalDays: '', nextMaintenanceDate: '',
  isInsured: false, insurancePolicyNumber: '', insuredValue: '', insuranceExpiryDate: '',
  isRentable: false, standardRentalAmount: '', rentalCurrencyCode: 'GHS',
};

export const assetToFormValues = (a: CompanyAsset): AssetFormValues => ({
  assetNumber: a.assetNumber ?? '',
  assetTag: a.assetTag ?? '',
  assetName: a.assetName ?? '',
  description: a.description ?? '',
  additionalRemarks: a.additionalRemarks ?? '',
  assetTypeId: a.assetTypeId,
  manufacturer: a.manufacturer ?? '',
  modelNumber: a.modelNumber ?? '',
  serialNumber: a.serialNumber ?? '',
  purchaseDate: a.purchaseDate ?? '',
  purchaseCost: a.purchaseCost === null ? '' : String(a.purchaseCost),
  supplier: a.supplier ?? '',
  invoiceNumber: a.invoiceNumber ?? '',
  hasWarranty: a.hasWarranty,
  warrantyStartDate: a.warrantyStartDate ?? '',
  warrantyEndDate: a.warrantyEndDate ?? '',
  warrantyProvider: a.warrantyProvider ?? '',
  color: a.color ?? '',
  size: a.size ?? '',
  specifications: a.specifications ?? '',
  status: a.status,
  condition: a.condition,
  locationId: a.locationId ?? NONE,
  locationDetails: a.locationDetails ?? '',
  unitId: a.unitId ?? NONE,
  isAssignable: a.isAssignable,
  requiresRegularMaintenance: a.requiresRegularMaintenance,
  maintenanceIntervalDays: a.maintenanceIntervalDays === null ? '' : String(a.maintenanceIntervalDays),
  nextMaintenanceDate: a.nextMaintenanceDate ?? '',
  isInsured: a.isInsured,
  insurancePolicyNumber: a.insurancePolicyNumber ?? '',
  insuredValue: a.insuredValue === null ? '' : String(a.insuredValue),
  insuranceExpiryDate: a.insuranceExpiryDate ?? '',
  isRentable: a.isRentable,
  standardRentalAmount: a.standardRentalAmount === null ? '' : String(a.standardRentalAmount),
  rentalCurrencyCode: a.rentalCurrencyCode ?? 'GHS',
});

const num = (v: string) => (v.trim() === '' ? null : Number(v));
const str = (v: string) => (v.trim() === '' ? null : v.trim());
const id = (v: string) => (v === NONE || v === '' ? null : v);

/**
 * Builds the payload BOTH the create and the update take.
 *
 * ⚠ The update is **full replace** (defect D-j): a key left off is written as null. That is why
 * this returns every field every time rather than a diff, and why the edit screen loads the record
 * into the form first. `nextMaintenanceDate` is included here but stripped by the create caller —
 * the create payload has no such field, and a key an endpoint does not know binds to nothing and
 * still answers 201, which is how slice 11 lost a fixture.
 */
export const formValuesToPayload = (v: AssetFormValues): Omit<UpdateCompanyAssetRequest, 'id'> => ({
  assetNumber: v.assetNumber.trim(),
  assetTag: v.assetTag.trim(),
  assetName: v.assetName.trim(),
  description: str(v.description),
  additionalRemarks: str(v.additionalRemarks),
  assetTypeId: v.assetTypeId,
  manufacturer: str(v.manufacturer),
  modelNumber: str(v.modelNumber),
  serialNumber: str(v.serialNumber),
  purchaseDate: str(v.purchaseDate),
  purchaseCost: num(v.purchaseCost),
  supplier: str(v.supplier),
  invoiceNumber: str(v.invoiceNumber),
  hasWarranty: v.hasWarranty,
  warrantyStartDate: v.hasWarranty ? str(v.warrantyStartDate) : null,
  warrantyEndDate: v.hasWarranty ? str(v.warrantyEndDate) : null,
  warrantyProvider: v.hasWarranty ? str(v.warrantyProvider) : null,
  color: str(v.color),
  size: str(v.size),
  specifications: str(v.specifications),
  // ⚠ The NUMBER on the way in, although every read gives the name back.
  status: COMPANY_ASSET_STATUSES.find((s) => s.label === v.status)?.value ?? 1,
  condition: ASSET_CONDITIONS.find((c) => c.label === v.condition)?.value ?? 2,
  locationId: id(v.locationId),
  locationDetails: str(v.locationDetails),
  unitId: id(v.unitId),
  isAssignable: v.isAssignable,
  requiresRegularMaintenance: v.requiresRegularMaintenance,
  maintenanceIntervalDays: v.requiresRegularMaintenance ? num(v.maintenanceIntervalDays) : null,
  nextMaintenanceDate: v.requiresRegularMaintenance ? str(v.nextMaintenanceDate) : null,
  isInsured: v.isInsured,
  insurancePolicyNumber: v.isInsured ? str(v.insurancePolicyNumber) : null,
  insuredValue: v.isInsured ? num(v.insuredValue) : null,
  insuranceExpiryDate: v.isInsured ? str(v.insuranceExpiryDate) : null,
  isRentable: v.isRentable,
  standardRentalAmount: v.isRentable ? num(v.standardRentalAmount) : null,
  rentalCurrencyCode: v.isRentable ? str(v.rentalCurrencyCode) : null,
});

interface Props {
  values: AssetFormValues;
  onChange: (values: AssetFormValues) => void;
  onSubmit: () => void;
  saving: boolean;
  submitLabel: string;
  /**
   * True on an asset sourced from Finance. Its purchase figures are Finance's (decision D1), so the
   * form shows them and refuses to edit them rather than pretending HR can change them.
   */
  financeOwned?: boolean;
  /** The create form has no such field — see the note on `formValuesToPayload`. */
  allowNextMaintenanceDate?: boolean;
}

export function AssetForm({
  values, onChange, onSubmit, saving, submitLabel,
  financeOwned = false, allowNextMaintenanceDate = false,
}: Props) {
  const [error, setError] = useState<string | null>(null);

  const { data: types = [] } = useQuery({
    queryKey: ['hr', 'assets', 'types'],
    queryFn: () => assetRegisterService.getTypes(),
  });
  const { data: locations = [] } = useQuery({
    queryKey: ['hr', 'locations'],
    queryFn: () => locationService.getAll(),
  });

  const set = <K extends keyof AssetFormValues>(key: K, value: AssetFormValues[K]) =>
    onChange({ ...values, [key]: value });

  const submit = () => {
    if (!values.assetName.trim()) return setError('Give the asset a name.');
    if (!values.assetTypeId) return setError('Choose an asset type.');
    setError(null);
    onSubmit();
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader><CardTitle className="text-base">What it is</CardTitle></CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label>Asset name *</Label>
            <Input value={values.assetName} onChange={(e) => set('assetName', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Asset type *</Label>
            <Select value={values.assetTypeId} onValueChange={(v) => set('assetTypeId', v)}>
              <SelectTrigger><SelectValue placeholder="Choose a type" /></SelectTrigger>
              <SelectContent>
                {types.map((t) => <SelectItem key={t.id} value={t.id}>{t.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Asset number</Label>
            <Input
              value={values.assetNumber}
              placeholder="Generated if left blank"
              onChange={(e) => set('assetNumber', e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>Asset tag</Label>
            <Input value={values.assetTag} onChange={(e) => set('assetTag', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Manufacturer</Label>
            <Input value={values.manufacturer} onChange={(e) => set('manufacturer', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Model</Label>
            <Input value={values.modelNumber} onChange={(e) => set('modelNumber', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Serial number</Label>
            <Input value={values.serialNumber} onChange={(e) => set('serialNumber', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Colour</Label>
            <Input value={values.color} onChange={(e) => set('color', e.target.value)} />
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label>Description</Label>
            <Textarea rows={2} value={values.description}
              onChange={(e) => set('description', e.target.value)} />
          </div>
          <div className="space-y-2 sm:col-span-2">
            {/* AST-7 — a free column the change document asked for by name. */}
            <Label>Additional remarks</Label>
            <Textarea rows={2} value={values.additionalRemarks}
              onChange={(e) => set('additionalRemarks', e.target.value)} />
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Where it is, and what state it is in</CardTitle></CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-2">
            <Label>Status</Label>
            <Select value={values.status} onValueChange={(v) => set('status', v)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {COMPANY_ASSET_STATUSES.map((s) =>
                  <SelectItem key={s.label} value={s.label}>{s.text}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>Condition</Label>
            <Select value={values.condition} onValueChange={(v) => set('condition', v)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {ASSET_CONDITIONS.map((c) =>
                  <SelectItem key={c.label} value={c.label}>{c.text}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <OrganizationUnitPicker
              value={values.unitId === NONE ? '' : values.unitId}
              onChange={(id) => set('unitId', id || NONE)}
              allowNone="Not set"
              unitLabel="Organisation unit"
              idPrefix="asset-form-unit"
            />
          </div>
          <div className="space-y-2">
            <Label>Location</Label>
            <Select value={values.locationId} onValueChange={(v) => set('locationId', v)}>
              <SelectTrigger><SelectValue placeholder="Not set" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>Not set</SelectItem>
                {locations.map((l) => <SelectItem key={l.id} value={l.id}>{l.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label>Location details</Label>
            <Input value={values.locationDetails}
              placeholder="Room, desk, shelf…"
              onChange={(e) => set('locationDetails', e.target.value)} />
          </div>
          <div className="flex items-center gap-2 sm:col-span-2">
            <Checkbox
              id="isAssignable"
              checked={values.isAssignable}
              onCheckedChange={(c) => set('isAssignable', c === true)}
            />
            <Label htmlFor="isAssignable" className="font-normal">
              Can be issued to an employee
            </Label>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">What it cost</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          {financeOwned && (
            <p className="sm:col-span-2 rounded-md bg-muted p-3 text-sm text-muted-foreground">
              This asset came from the Finance fixed-asset register. Its purchase figures,
              depreciation and disposal belong to Finance and are shown here read-only.
            </p>
          )}
          <div className="space-y-2">
            <Label>Purchase date</Label>
            <Input type="date" disabled={financeOwned} value={values.purchaseDate}
              onChange={(e) => set('purchaseDate', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Purchase cost</Label>
            <Input type="number" step="0.01" disabled={financeOwned} value={values.purchaseCost}
              onChange={(e) => set('purchaseCost', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Supplier</Label>
            <Input disabled={financeOwned} value={values.supplier}
              onChange={(e) => set('supplier', e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label>Invoice number</Label>
            <Input disabled={financeOwned} value={values.invoiceNumber}
              onChange={(e) => set('invoiceNumber', e.target.value)} />
          </div>
          <div className="flex items-center gap-2 sm:col-span-2">
            <Checkbox id="hasWarranty" checked={values.hasWarranty}
              onCheckedChange={(c) => set('hasWarranty', c === true)} />
            <Label htmlFor="hasWarranty" className="font-normal">Under warranty</Label>
          </div>
          {values.hasWarranty && (
            <>
              <div className="space-y-2">
                <Label>Warranty starts</Label>
                <Input type="date" value={values.warrantyStartDate}
                  onChange={(e) => set('warrantyStartDate', e.target.value)} />
              </div>
              <div className="space-y-2">
                <Label>Warranty ends</Label>
                <Input type="date" value={values.warrantyEndDate}
                  onChange={(e) => set('warrantyEndDate', e.target.value)} />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <Label>Warranty provider</Label>
                <Input value={values.warrantyProvider}
                  onChange={(e) => set('warrantyProvider', e.target.value)} />
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Servicing — AST-1</CardTitle></CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="flex items-center gap-2 sm:col-span-2">
            <Checkbox id="requiresRegularMaintenance" checked={values.requiresRegularMaintenance}
              onCheckedChange={(c) => set('requiresRegularMaintenance', c === true)} />
            <Label htmlFor="requiresRegularMaintenance" className="font-normal">
              Needs servicing on a schedule
            </Label>
          </div>
          {values.requiresRegularMaintenance && (
            <>
              <div className="space-y-2">
                <Label>Service every (days)</Label>
                <Input type="number" value={values.maintenanceIntervalDays}
                  onChange={(e) => set('maintenanceIntervalDays', e.target.value)} />
              </div>
              {allowNextMaintenanceDate ? (
                <div className="space-y-2">
                  <Label>Next service due</Label>
                  <Input type="date" value={values.nextMaintenanceDate}
                    onChange={(e) => set('nextMaintenanceDate', e.target.value)} />
                </div>
              ) : (
                <p className="self-end text-sm text-muted-foreground">
                  The first service date is set once the asset exists — until then it shows on the
                  &ldquo;never scheduled&rdquo; list, which is where it should be.
                </p>
              )}
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Insurance — AST-4</CardTitle></CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="flex items-center gap-2 sm:col-span-2">
            <Checkbox id="isInsured" checked={values.isInsured}
              onCheckedChange={(c) => set('isInsured', c === true)} />
            <Label htmlFor="isInsured" className="font-normal">Insured</Label>
          </div>
          {values.isInsured && (
            <>
              <div className="space-y-2">
                <Label>Policy number</Label>
                <Input value={values.insurancePolicyNumber}
                  onChange={(e) => set('insurancePolicyNumber', e.target.value)} />
              </div>
              <div className="space-y-2">
                <Label>Insured for</Label>
                <Input type="number" step="0.01" value={values.insuredValue}
                  onChange={(e) => set('insuredValue', e.target.value)} />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <Label>Cover lapses on</Label>
                <Input type="date" value={values.insuranceExpiryDate}
                  onChange={(e) => set('insuranceExpiryDate', e.target.value)} />
                {/* The undated watchlist exists because this is so often left blank. */}
                {!values.insuranceExpiryDate && (
                  <p className="text-xs text-muted-foreground">
                    Left blank, this policy appears on no renewal list and can never become due —
                    it will show under &ldquo;never dated&rdquo; instead.
                  </p>
                )}
              </div>
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Rental — AST-9</CardTitle></CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="flex items-center gap-2 sm:col-span-2">
            <Checkbox id="isRentable" checked={values.isRentable}
              onCheckedChange={(c) => set('isRentable', c === true)} />
            <Label htmlFor="isRentable" className="font-normal">
              A holder can be charged for this — staff housing, a vehicle
            </Label>
          </div>
          {values.isRentable && (
            <>
              <div className="space-y-2">
                <Label>Standard rate</Label>
                <Input type="number" step="0.01" value={values.standardRentalAmount}
                  onChange={(e) => set('standardRentalAmount', e.target.value)} />
                <p className="text-xs text-muted-foreground">
                  The default when terms are set on an assignment — not a charge in itself.
                </p>
              </div>
              <div className="space-y-2">
                <Label>Currency</Label>
                <Input maxLength={3} value={values.rentalCurrencyCode}
                  onChange={(e) => set('rentalCurrencyCode', e.target.value.toUpperCase())} />
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {error && <p className="text-sm text-destructive">{error}</p>}

      <div className="flex justify-end">
        <Button onClick={submit} disabled={saving}>
          {saving ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Save className="mr-2 h-4 w-4" />}
          {submitLabel}
        </Button>
      </div>
    </div>
  );
}
