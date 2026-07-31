'use client';

import Link from 'next/link';
import React from 'react';
import { FileSignature, Landmark, Loader2, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  estateLandManagementService,
  EstateManagedAssetStatus,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';

function formatMoney(value: number, currency: string) {
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);
}

function getPlotSizeAcres(asset?: EstateManagedAsset) {
  if (!asset) return null;
  const unit = asset.areaUnit?.trim().toLowerCase();
  if (asset.areaValue != null && (unit === 'acre' || unit === 'acres')) {
    return asset.areaValue;
  }
  if (asset.areaSquareMeters != null) {
    return asset.areaSquareMeters / 4046.8564224;
  }
  return null;
}

export function LeaseSetupWorkspace() {
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [customers, setCustomers] = React.useState<BusinessPartnerDto[]>([]);
  const [selectedAssetId, setSelectedAssetId] = React.useState('');
  const [customerId, setCustomerId] = React.useState('');
  const [dateOfTenancy, setDateOfTenancy] = React.useState('');
  const [rightOfEntryDate, setRightOfEntryDate] = React.useState('');
  const [leaseTermYears, setLeaseTermYears] = React.useState('');
  const [propertyFileReference, setPropertyFileReference] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [loadError, setLoadError] = React.useState<string | null>(null);

  const loadOptions = React.useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const [managedAssets, activeCustomers] = await Promise.all([
        estateLandManagementService.getManagedAssets({
          status: EstateManagedAssetStatus.Available,
          availableForLease: true,
          take: 500,
        }),
        businessPartnerService.getActivePartners('Customer'),
      ]);

      setAssets(
        managedAssets.filter(
          (asset) =>
            asset.status === EstateManagedAssetStatus.Available &&
            asset.isAvailableForLease &&
            !asset.customerBusinessPartnerId
        )
      );
      setCustomers(activeCustomers);
    } catch {
      setAssets([]);
      setCustomers([]);
      setLoadError('Unable to load available properties and customers.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    void loadOptions();
  }, [loadOptions]);

  const selectedAsset = assets.find((asset) => asset.id === selectedAssetId);
  const selectedCustomer = customers.find(
    (customer) => customer.id === customerId
  );
  const currency = selectedAsset?.currency || 'GHS';
  const plotSizeAcres = getPlotSizeAcres(selectedAsset);

  const clearForm = () => {
    setSelectedAssetId('');
    setCustomerId('');
    setDateOfTenancy('');
    setRightOfEntryDate('');
    setLeaseTermYears('');
    setPropertyFileReference('');
  };

  const saveLeaseSetup = async () => {
    if (!selectedAsset || !selectedCustomer) {
      toast.error('Select an available property or unit and a customer.');
      return;
    }

    const termYears = leaseTermYears === '' ? null : Number(leaseTermYears);
    if (
      termYears != null &&
      (!Number.isInteger(termYears) || termYears < 1 || termYears > 999)
    ) {
      toast.error('Lease term must be a whole number between 1 and 999.');
      return;
    }

    setIsSaving(true);
    try {
      await estateLandManagementService.updateRegister(selectedAsset.id, {
        dateOfTenancy: dateOfTenancy || null,
        rightOfEntryDate: rightOfEntryDate || null,
        leaseTermYears: termYears,
        customerBusinessPartnerId: selectedCustomer.id,
        lesseeName: selectedCustomer.partnerName,
        lesseeAddress: selectedCustomer.physicalAddress || null,
        propertyFileReference: propertyFileReference.trim() || null,
      });

      setAssets((current) =>
        current.filter((asset) => asset.id !== selectedAsset.id)
      );
      clearForm();
      toast.success(
        'Lease setup saved. Continue the lease case for approvals and handoffs.'
      );
    } catch (error: unknown) {
      toast.error(
        error instanceof Error ? error.message : 'Unable to save lease setup.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Card className="border-primary/20">
      <CardHeader>
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <FileSignature className="h-5 w-5 text-primary" />
              New Lease Assignment
            </CardTitle>
            <CardDescription className="mt-2 max-w-3xl">
              Attach a customer and set the lease terms here. Ground rent is
              inherited from the approved Estates assessment and is not
              recalculated in Lease Management. Only assets released as
              Available for Lease are offered for selection.
            </CardDescription>
          </div>
          <div className="flex items-center gap-2">
            <Badge variant="outline">Lease Management</Badge>
            <Button
              type="button"
              size="icon"
              variant="outline"
              disabled={isLoading}
              onClick={() => void loadOptions()}
              aria-label="Refresh available properties and customers"
            >
              <RefreshCw className="h-4 w-4" />
            </Button>
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-6">
        {loadError ? (
          <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
            {loadError}
          </div>
        ) : null}

        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="lease-property">Available property / unit</Label>
            <Select
              value={selectedAssetId}
              onValueChange={setSelectedAssetId}
              disabled={isLoading}
            >
              <SelectTrigger id="lease-property">
                <SelectValue
                  placeholder={
                    isLoading
                      ? 'Loading available properties'
                      : 'Select an available property'
                  }
                />
              </SelectTrigger>
              <SelectContent>
                {assets.map((asset) => (
                  <SelectItem key={asset.id} value={asset.id}>
                    {asset.assetCode} · {asset.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {!isLoading && assets.length === 0 ? (
              <p className="text-xs text-muted-foreground">
                No unassigned Available property or unit is currently eligible.
              </p>
            ) : null}
          </div>

          <div className="space-y-2">
            <Label htmlFor="lease-customer">Customer</Label>
            <Select
              value={customerId}
              onValueChange={setCustomerId}
              disabled={isLoading}
            >
              <SelectTrigger id="lease-customer">
                <SelectValue placeholder="Select an active customer" />
              </SelectTrigger>
              <SelectContent>
                {customers.map((customer) => (
                  <SelectItem key={customer.id} value={customer.id}>
                    {customer.partnerCode} · {customer.partnerName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="lease-tenancy-date">Date of tenancy</Label>
            <Input
              id="lease-tenancy-date"
              type="date"
              value={dateOfTenancy}
              onChange={(event) => setDateOfTenancy(event.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lease-entry-date">Right-of-entry date</Label>
            <Input
              id="lease-entry-date"
              type="date"
              value={rightOfEntryDate}
              onChange={(event) => setRightOfEntryDate(event.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lease-term">Lease term (years)</Label>
            <Input
              id="lease-term"
              type="number"
              min={1}
              max={999}
              step={1}
              value={leaseTermYears}
              onChange={(event) => setLeaseTermYears(event.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="lease-file-reference">
              Property file reference
            </Label>
            <Input
              id="lease-file-reference"
              maxLength={120}
              value={propertyFileReference}
              onChange={(event) => setPropertyFileReference(event.target.value)}
            />
          </div>
        </div>

        <div className="space-y-4 rounded-lg border bg-muted/20 p-4">
          <div>
            <div className="flex items-center gap-2 font-medium">
              <Landmark className="h-4 w-4 text-primary" />
              Approved Ground Rent Assessment
            </div>
            <p className="mt-1 text-sm text-muted-foreground">
              The Estates SOP calculation is plot size in acres multiplied by
              the approved ground-rent rate per acre. The payable amount is the
              computed result rounded up to the next whole amount.
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-md border bg-background p-3">
              <div className="text-xs text-muted-foreground">
                Plot size used
              </div>
              <div className="mt-1 font-semibold">
                {plotSizeAcres == null
                  ? 'Not recorded'
                  : `${plotSizeAcres.toFixed(4)} acres`}
              </div>
            </div>
            <div className="rounded-md border bg-background p-3">
              <div className="text-xs text-muted-foreground">
                Approved rate per acre
              </div>
              <div className="mt-1 font-semibold">
                {selectedAsset?.groundRentRatePerAcre == null
                  ? 'Not recorded'
                  : formatMoney(selectedAsset.groundRentRatePerAcre, currency)}
              </div>
            </div>
            <div className="rounded-md border bg-background p-3">
              <div className="text-xs text-muted-foreground">
                Ground rent computed
              </div>
              <div className="mt-1 font-semibold">
                {selectedAsset?.groundRentComputed == null
                  ? 'Not recorded'
                  : formatMoney(selectedAsset.groundRentComputed, currency)}
              </div>
            </div>
            <div className="rounded-md border bg-background p-3">
              <div className="text-xs text-muted-foreground">
                Ground rent payable
              </div>
              <div className="mt-1 font-semibold">
                {selectedAsset?.groundRentPayable == null
                  ? 'Not recorded'
                  : formatMoney(selectedAsset.groundRentPayable, currency)}
              </div>
            </div>
          </div>

          {selectedAsset && selectedAsset.groundRentPayable == null ? (
            <div className="space-y-3 rounded-md border border-amber-500/30 bg-amber-500/5 p-3">
              <p className="text-sm">
                No approved ground-rent assessment is recorded for this asset.
                Complete the applicable Estates SOP case before relying on a
                ground-rent figure in the lease.
              </p>
              <div className="flex flex-wrap gap-2">
                <Button asChild type="button" size="sm" variant="outline">
                  <Link href="/estate/EstateLandsPartiallyServiced">
                    Lands / Partially Serviced
                  </Link>
                </Button>
                <Button asChild type="button" size="sm" variant="outline">
                  <Link href="/estate/EstateTraditionalLands">
                    Traditional Lands
                  </Link>
                </Button>
                <Button asChild type="button" size="sm" variant="outline">
                  <Link href="/estate/EstateTenancyRegularisation">
                    Regularisation
                  </Link>
                </Button>
              </div>
            </div>
          ) : null}
        </div>

        <div className="flex flex-wrap justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            disabled={isSaving}
            onClick={clearForm}
          >
            Clear
          </Button>
          <Button
            type="button"
            disabled={isSaving || isLoading}
            onClick={() => void saveLeaseSetup()}
          >
            {isSaving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : null}
            Save lease setup
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
