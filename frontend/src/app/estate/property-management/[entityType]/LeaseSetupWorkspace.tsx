'use client';

import Link from 'next/link';
import React from 'react';
import {
  AlertTriangle,
  ChevronDown,
  ClipboardCheck,
  CreditCard,
  FileText,
  FileSignature,
  FileUp,
  Landmark,
  Loader2,
  Home,
  Plus,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react';
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
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import { Pagination } from '@/components/ui/pagination';
import { Label } from '@/components/ui/label';
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
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';
import {
  estateLandManagementService,
  EstateManagedAssetType,
  EstateManagedAssetStatus,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import {
  estateGroundRentService,
  type GroundRentAccount,
} from '@/services/estate-ground-rent.service';
import { useManagedAssetsPage } from './use-managed-assets-page';

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

function leaseYearsFromListingTerm(termMonths?: number | null) {
  if (!termMonths || termMonths <= 0) return '';
  return String(Math.max(1, Math.ceil(termMonths / 12)));
}

function formatDate(value?: string) {
  if (!value) return 'Not recorded';
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

function getLeaseExpiryDate(asset: EstateManagedAsset) {
  const startDate = asset.dateOfTenancy || asset.rightOfEntryDate;
  if (!startDate || !asset.leaseTermYears) return 'Not recorded';

  const expiryDate = new Date(startDate);
  expiryDate.setFullYear(expiryDate.getFullYear() + asset.leaseTermYears);
  return formatDate(expiryDate.toISOString());
}

function getLeaseStatus(asset: EstateManagedAsset) {
  if (asset.status === EstateManagedAssetStatus.Leased) return 'Leased';
  if (asset.status === EstateManagedAssetStatus.Occupied) return 'Occupied';
  if (asset.status === EstateManagedAssetStatus.Reserved) return 'Reserved';
  if (asset.status === EstateManagedAssetStatus.Retired) return 'Closed';
  if (asset.status === EstateManagedAssetStatus.Sold) return 'Sold';
  return asset.customerBusinessPartnerId ? 'Assigned' : 'Draft';
}

function buildProcedurePrefillHref(
  basePath: string,
  asset: EstateManagedAsset,
  titlePrefix: string,
  requestType: string,
  extraFields: Record<string, string | null | undefined> = {}
) {
  const propertyReference = asset.projectUnitCode || asset.assetCode;
  const sourceReference = asset.propertyFileReference || propertyReference;
  const today = new Date().toISOString().slice(0, 10);
  const leaseStart = (asset.rightOfEntryDate || asset.dateOfTenancy || today).slice(
    0,
    10
  );
  const params = new URLSearchParams({
    assetId: asset.id,
    title: `${titlePrefix} - ${asset.name}`,
    referenceNumber: sourceReference,
    applicantName: asset.lesseeName || '',
    sourceDepartment: 'Estate / Property Management',
    receivedDate: today,
    description: `${titlePrefix} request for ${propertyReference} (${asset.name}).`,
    field_referenceNumber: sourceReference,
    field_requestType: requestType,
    field_applicantName: asset.lesseeName || '',
    field_propertyReference: propertyReference,
    field_propertyUnit: propertyReference,
    field_leaseReference: sourceReference,
    field_sourceWorkspace: 'Lease Management',
    field_sourceReference: sourceReference,
    field_occupantReference: asset.lesseeName || '',
    field_customerReference: asset.customerBusinessPartnerId || '',
    field_originatingDepartment: 'Estate',
    field_receivedDate: today,
  });

  Object.entries(extraFields).forEach(([key, value]) => {
    if (value != null && value !== '') {
      params.set(key.startsWith('field_') ? key : `field_${key}`, value);
    }
  });
  params.set('field_effectiveDate', extraFields.effectiveDate || leaseStart);

  return `${basePath}?${params.toString()}`;
}

function getLeaseStartDate(asset: EstateManagedAsset) {
  return (asset.rightOfEntryDate || asset.dateOfTenancy || new Date().toISOString()).slice(
    0,
    10
  );
}

function getOccupancyHandoffFields(asset: EstateManagedAsset) {
  const propertyReference = asset.projectUnitCode || asset.assetCode;
  const sourceReference = asset.propertyFileReference || propertyReference;
  return {
    occupancyAvailabilityReference: `OCC-${propertyReference}`,
    currentStatus: getLeaseStatus(asset),
    requestedStatus: 'Reserved',
    availabilityState: 'Reserved pending lease',
    leasingVisibility: 'Hidden',
    occupantReference: asset.lesseeName || '',
    leaseReference: sourceReference,
    billingImpact: 'Billing hold until signed agreement and move-in',
    approvalStatus: 'Draft',
    effectiveDate: getLeaseStartDate(asset),
  };
}

function getMoveInHandoffFields(asset: EstateManagedAsset) {
  const propertyReference = asset.projectUnitCode || asset.assetCode;
  const sourceReference = asset.propertyFileReference || propertyReference;
  return {
    handoverReference: `HND-${propertyReference}`,
    handoverType: 'Move-in',
    propertyUnit: propertyReference,
    occupantReference: asset.lesseeName || '',
    leaseReference: sourceReference,
    scheduledDate: getLeaseStartDate(asset),
    actualDate: getLeaseStartDate(asset),
    billingImpact: 'Start billing from move-in / agreement start date',
    occupancyUpdate: 'Mark occupied',
    signatureStatus: asset.propertyFileReference ? 'Signed' : 'Pending',
    documentStatus: asset.propertyFileReference
      ? 'Pending index'
      : 'Pending signed agreement',
  };
}

function getBillingHandoffFields(asset: EstateManagedAsset) {
  const propertyReference = asset.projectUnitCode || asset.assetCode;
  const sourceReference = asset.propertyFileReference || propertyReference;
  const isLand = asset.assetType === EstateManagedAssetType.Land;
  return {
    billingOperationReference: `BILL-${propertyReference}`,
    chargeType: isLand ? 'Ground rent' : 'Rent',
    currency: asset.currency || 'GHS',
    dueDate: getLeaseStartDate(asset),
    leaseReference: sourceReference,
    occupantReference: asset.lesseeName || '',
    financeArActionRequested: 'Create invoice',
    financeArStatus: 'Not sent',
    billingStatus: asset.propertyFileReference
      ? 'Ready after move-in'
      : 'Blocked - signed agreement pending',
  };
}

function getRecordIndexHandoffFields(asset: EstateManagedAsset) {
  const propertyReference = asset.projectUnitCode || asset.assetCode;
  const sourceReference = asset.propertyFileReference || propertyReference;
  return {
    recordIndexReference: `REC-${propertyReference}`,
    documentCategory: 'Lease / legal instrument',
    documentPurpose: 'Contract / instrument',
    metadataTemplate: 'Lease',
    dmsRepositoryStatus: 'Not linked',
    documentStatus: asset.propertyFileReference
      ? 'Pending metadata'
      : 'Pending signed agreement',
    leaseReference: sourceReference,
    occupantReference: asset.lesseeName || '',
  };
}

export function LeaseSetupWorkspace() {
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [customers, setCustomers] = React.useState<BusinessPartnerDto[]>([]);
  const [groundRentAccounts, setGroundRentAccounts] = React.useState<
    GroundRentAccount[]
  >([]);
  const [registerSearch, setRegisterSearch] = React.useState('');
  const [selectedAssetId, setSelectedAssetId] = React.useState('');
  const [customerId, setCustomerId] = React.useState('');
  const [dateOfTenancy, setDateOfTenancy] = React.useState('');
  const [rightOfEntryDate, setRightOfEntryDate] = React.useState('');
  const [leaseTermYears, setLeaseTermYears] = React.useState('');
  const [propertyFileReference, setPropertyFileReference] = React.useState('');
  const [signedAgreementFile, setSignedAgreementFile] =
    React.useState<File | null>(null);
  const [fileInputResetKey, setFileInputResetKey] = React.useState(0);
  const [isCreateDialogOpen, setIsCreateDialogOpen] = React.useState(false);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const {
    assets: leaseRecords,
    setAssets: setLeaseRecords,
    page: leasePage,
    setPage: setLeasePage,
    isLoading: isLeaseRegisterLoading,
    loadError: leaseRegisterError,
    loadAssets: loadLeaseRecords,
    pageSize: leasePageSize,
    totalPages: leaseTotalPages,
    totalItems: leaseTotalItems,
  } = useManagedAssetsPage({
    search: registerSearch || undefined,
    statuses: [
      EstateManagedAssetStatus.Reserved,
      EstateManagedAssetStatus.Leased,
      EstateManagedAssetStatus.Occupied,
    ],
    errorMessage: 'Unable to load the Lease Register.',
  });

  const loadOptions = React.useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const [availableAssets, activeCustomers, billingAccounts] =
        await Promise.all([
          estateLandManagementService.getManagedAssets({
            statuses: [
              EstateManagedAssetStatus.Available,
              EstateManagedAssetStatus.LandBank,
            ],
            availableForLease: true,
            take: 25,
          }),
          businessPartnerService.getActivePartners('Customer'),
          estateGroundRentService.getAccounts().catch(() => []),
        ]);

      setAssets(
        availableAssets.filter(
          (asset) =>
            (asset.status === EstateManagedAssetStatus.Available ||
              (asset.assetType === EstateManagedAssetType.Land &&
                asset.status === EstateManagedAssetStatus.LandBank)) &&
            asset.isAvailableForLease &&
            !asset.customerBusinessPartnerId
        )
      );
      setCustomers(activeCustomers);
      setGroundRentAccounts(billingAccounts);
    } catch {
      setAssets([]);
      setCustomers([]);
      setGroundRentAccounts([]);
      setLoadError(
        'Unable to load available properties, customers, and the Lease Register.'
      );
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
  const signedAgreementReference = propertyFileReference.trim();
  const leaseStartDate = rightOfEntryDate || dateOfTenancy;
  const signedReferenceNeedsDate =
    Boolean(signedAgreementReference || signedAgreementFile) && !leaseStartDate;
  const leaseStatusPreview = signedAgreementReference && leaseStartDate
    ? 'Leased - signed agreement and start / move-in date recorded'
    : 'Reserved - customer selected, agreement/signature or start date pending';
  const billingAccountByAssetId = React.useMemo(
    () =>
      new Map(
        groundRentAccounts.map((account) => [
          account.estateManagedAssetId,
          account,
        ])
      ),
    [groundRentAccounts]
  );

  const clearForm = () => {
    setSelectedAssetId('');
    setCustomerId('');
    setDateOfTenancy('');
    setRightOfEntryDate('');
    setLeaseTermYears('');
    setPropertyFileReference('');
    setSignedAgreementFile(null);
    setFileInputResetKey((current) => current + 1);
  };

  const chooseLeaseAsset = (assetId: string) => {
    setSelectedAssetId(assetId);
    const asset = assets.find((item) => item.id === assetId);
    setLeaseTermYears(leaseYearsFromListingTerm(asset?.externalLeaseTermMonths));
  };

  const saveLeaseSetup = async () => {
    if (!selectedAsset || !selectedCustomer) {
      toast.error('Select an available lease asset and a customer.');
      return;
    }

    if (signedReferenceNeedsDate) {
      toast.error(
        'Record the agreement start date or right-of-entry / move-in date with the signed agreement reference.'
      );
      return;
    }

    const termYears = leaseTermYears === '' ? null : Number(leaseTermYears);
    if (termYears == null) {
      toast.error(
        'Enter the lease term in years. Use the Sales term when it exists; otherwise Estate must record it here.'
      );
      return;
    }

    if (
      termYears != null &&
      (!Number.isInteger(termYears) || termYears < 1 || termYears > 999)
    ) {
      toast.error('Lease term must be a whole number between 1 and 999.');
      return;
    }

    setIsSaving(true);
    try {
      let signedAgreementReferenceToSave =
        propertyFileReference.trim() || null;
      if (signedAgreementFile) {
        const uploadedAgreement = await estateLandManagementService.uploadDocument(
          selectedAsset.id,
          signedAgreementFile,
          'Signed Lease Agreement',
          `Signed agreement - ${selectedAsset.assetCode}`
        );
        signedAgreementReferenceToSave =
          uploadedAgreement.centralDocumentReference ||
          uploadedAgreement.documentName ||
          uploadedAgreement.fileName;
      }

      const savedLease = await estateLandManagementService.updateRegister(
        selectedAsset.id,
        {
          dateOfTenancy: dateOfTenancy || null,
          rightOfEntryDate: rightOfEntryDate || null,
          leaseTermYears: termYears,
          customerBusinessPartnerId: selectedCustomer.id,
          lesseeName: selectedCustomer.partnerName,
          lesseeAddress: selectedCustomer.physicalAddress || null,
          propertyFileReference: signedAgreementReferenceToSave,
        }
      );

      setAssets((current) =>
        current.filter((asset) => asset.id !== selectedAsset.id)
      );
      setLeaseRecords((current) => [
        savedLease,
        ...current.filter((asset) => asset.id !== savedLease.id),
      ]);
      clearForm();
      setIsCreateDialogOpen(false);
      toast.success(
        'Lease setup saved and the asset was reserved. Continue Occupancy, Handover, Billing, and Records handoffs as required.'
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
    <div className="space-y-6">
      <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-5xl">
          <DialogHeader>
            <DialogTitle className="flex items-center gap-2">
              <FileSignature className="h-5 w-5 text-primary" />
              New Lease Assignment
            </DialogTitle>
          </DialogHeader>

      <div className="space-y-6">
        {loadError ? (
          <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
            {loadError}
          </div>
        ) : null}

        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <Label htmlFor="lease-property">Available property / land</Label>
            <Select
              value={selectedAssetId}
              onValueChange={chooseLeaseAsset}
              disabled={isLoading}
            >
              <SelectTrigger id="lease-property">
                <SelectValue
                  placeholder={
                    isLoading
                      ? 'Loading available lease assets'
                      : 'Select an available asset'
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
                No unassigned Available property, shop, apartment, or land parcel is currently eligible for lease setup.
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
              Signed agreement / property file
            </Label>
            <Input
              key={fileInputResetKey}
              id="lease-file-reference"
              type="file"
              accept=".pdf,.doc,.docx,.jpg,.jpeg,.png"
              onChange={(event) => {
                const file = event.target.files?.[0] ?? null;
                setSignedAgreementFile(file);
                setPropertyFileReference(file?.name ?? '');
              }}
            />
            <p className="text-xs text-muted-foreground">
              Upload the signed agreement or property file. The uploaded
              document reference will be saved on the lease record before
              billing can continue.
            </p>
            {signedAgreementFile ? (
              <Badge variant="secondary" className="w-fit gap-1">
                <FileUp className="h-3.5 w-3.5" />
                {signedAgreementFile.name}
              </Badge>
            ) : null}
          </div>
        </div>

        {selectedAsset && selectedCustomer ? (
          <div
            className={`flex items-start gap-3 rounded-md border p-3 text-sm ${
              signedReferenceNeedsDate
                ? 'border-amber-500/30 bg-amber-500/5 text-amber-900'
                : 'bg-muted/20 text-muted-foreground'
            }`}
          >
            {signedReferenceNeedsDate ? (
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            ) : (
              <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
            )}
            <div>
              <div className="font-medium text-foreground">
                Status after save: {leaseStatusPreview}
              </div>
              <p className="mt-1">
                Ground-rent billing remains held until there is a
                signed agreement reference and an agreement start / move-in
                date. Move-in / Handover then marks the lease active.
              </p>
            </div>
          </div>
        ) : null}

        <div className="space-y-4 rounded-lg border bg-muted/20 p-4">
          <div>
            <div className="flex items-center gap-2 font-medium">
              <Landmark className="h-4 w-4 text-primary" />
              Land Ground Rent Assessment
            </div>
            <p className="mt-1 text-sm text-muted-foreground">
              Ground rent applies to land leases only and is kept separate from
              apartment or house rent.
            </p>
          </div>

          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-md border bg-background p-3">
              <div className="text-xs text-muted-foreground">
                Plot size used
              </div>
              <div className="mt-1 font-semibold">
                {selectedAsset?.assetType !== EstateManagedAssetType.Land
                  ? 'Not applicable'
                  : plotSizeAcres == null
                  ? 'Not recorded'
                  : `${plotSizeAcres.toFixed(4)} acres`}
              </div>
            </div>
            <div className="rounded-md border bg-background p-3">
              <div className="text-xs text-muted-foreground">
                Approved rate per acre
              </div>
              <div className="mt-1 font-semibold">
                {selectedAsset?.assetType !== EstateManagedAssetType.Land
                  ? 'Not applicable'
                  : selectedAsset?.groundRentRatePerAcre == null
                  ? 'Not recorded'
                  : formatMoney(selectedAsset.groundRentRatePerAcre, currency)}
              </div>
            </div>
            <div className="rounded-md border bg-background p-3">
              <div className="text-xs text-muted-foreground">
                Ground rent computed
              </div>
              <div className="mt-1 font-semibold">
                {selectedAsset?.assetType !== EstateManagedAssetType.Land
                  ? 'Not applicable'
                  : selectedAsset?.groundRentComputed == null
                  ? 'Not recorded'
                  : formatMoney(selectedAsset.groundRentComputed, currency)}
              </div>
            </div>
            <div className="rounded-md border bg-background p-3">
              <div className="text-xs text-muted-foreground">
                Ground rent payable
              </div>
              <div className="mt-1 font-semibold">
                {selectedAsset?.assetType !== EstateManagedAssetType.Land
                  ? 'Not applicable'
                  : selectedAsset?.groundRentPayable == null
                  ? 'Not recorded'
                  : formatMoney(selectedAsset.groundRentPayable, currency)}
              </div>
            </div>
          </div>

          {selectedAsset &&
          selectedAsset.assetType === EstateManagedAssetType.Land &&
          selectedAsset.groundRentPayable == null ? (
            <div className="space-y-3 rounded-md border border-amber-500/30 bg-amber-500/5 p-3">
              <p className="text-sm">
                No approved ground-rent assessment is recorded for this asset.
                Complete the applicable Estates SOP case before relying on a
                ground-rent figure in the lease.
              </p>
              <div className="flex flex-wrap gap-2">
                <Button asChild type="button" size="sm" variant="outline">
                  <Link href="/estate/EstateTraditionalLands">
                    Traditional Lands
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
            disabled={isSaving || isLoading || signedReferenceNeedsDate}
            onClick={() => void saveLeaseSetup()}
          >
            {isSaving ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : null}
            Save lease setup
          </Button>
        </div>
      </div>
        </DialogContent>
      </Dialog>

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <CardTitle>Lease Register</CardTitle>
              <CardDescription className="mt-2">
                Saved lease assignments, tenancy dates,
                terms, ground rent, file references, and current operating
                status.
              </CardDescription>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="secondary">
                {leaseTotalItems} records
              </Badge>
              <Button
                type="button"
                variant="outline"
                size="icon"
                disabled={isLoading || isLeaseRegisterLoading}
                onClick={() =>
                  void Promise.all([loadOptions(), loadLeaseRecords(leasePage)])
                }
                aria-label="Refresh lease management data"
              >
                <RefreshCw className="h-4 w-4" />
              </Button>
              <Button
                type="button"
                className="gap-2"
                onClick={() => setIsCreateDialogOpen(true)}
              >
                <Plus className="h-4 w-4" />
                Add New Lease Assignment
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <Input
            value={registerSearch}
            onChange={(event) => {
              setLeasePage(1);
              setRegisterSearch(event.target.value);
            }}
            placeholder="Search by property, lessee, file reference, or status"
          />

          {isLeaseRegisterLoading ? (
            <div className="flex items-center justify-center gap-2 py-10 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading lease register
            </div>
          ) : null}

          {leaseRegisterError ? (
            <div className="rounded-md border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">
              {leaseRegisterError}
            </div>
          ) : null}

          {!isLeaseRegisterLoading && !leaseRegisterError && leaseRecords.length === 0 ? (
            <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
              {registerSearch
                ? 'No lease records match the search.'
                : 'No lease assignments have been saved yet.'}
            </div>
          ) : null}

          {!isLeaseRegisterLoading && leaseRecords.length > 0 ? (
            <div className="overflow-x-auto rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Property / land</TableHead>
                    <TableHead>Lessee</TableHead>
                    <TableHead>Tenancy date</TableHead>
                    <TableHead>Term / expiry</TableHead>
                    <TableHead>Ground rent</TableHead>
                    <TableHead>AR billing</TableHead>
                    <TableHead>File reference</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {leaseRecords.map((asset) => {
                    const billingAccount = billingAccountByAssetId.get(
                      asset.id
                    );

                    return (
                      <TableRow key={asset.id}>
                        <TableCell>
                          <div className="font-medium">{asset.name}</div>
                          <div className="text-xs text-muted-foreground">
                            {asset.assetCode}
                          </div>
                        </TableCell>
                        <TableCell>
                          {asset.lesseeName || 'Not recorded'}
                        </TableCell>
                        <TableCell>{formatDate(asset.dateOfTenancy)}</TableCell>
                        <TableCell>
                          <div>
                            {asset.leaseTermYears
                              ? `${asset.leaseTermYears} years`
                              : 'Not recorded'}
                          </div>
                          <div className="text-xs text-muted-foreground">
                            Expires: {getLeaseExpiryDate(asset)}
                          </div>
                        </TableCell>
                        <TableCell>
                          {asset.assetType !== EstateManagedAssetType.Land
                            ? 'Not applicable'
                            : asset.groundRentPayable == null
                            ? 'Not recorded'
                            : formatMoney(
                                asset.groundRentPayable,
                                asset.currency || 'GHS'
                              )}
                        </TableCell>
                        <TableCell>
                          <div className="flex flex-col items-start gap-1.5">
                            {asset.assetType !== EstateManagedAssetType.Land ? (
                              <>
                                <Badge variant="outline">
                                  Non-land lease charge
                                </Badge>
                                <span className="text-xs text-muted-foreground">
                                  Record the full-term lease amount separately;
                                  ground rent does not apply.
                                </span>
                              </>
                            ) : (
                              <>
                                <Badge
                                  variant={
                                    billingAccount?.status === 'Active'
                                      ? 'secondary'
                                      : 'outline'
                                  }
                                >
                                  {billingAccount
                                    ? billingAccount.status
                                    : 'Setup required'}
                                </Badge>
                                {billingAccount &&
                                billingAccount.outstandingAmount > 0 ? (
                                  <span className="text-xs text-muted-foreground">
                                    Outstanding:{' '}
                                    {formatMoney(
                                      billingAccount.outstandingAmount,
                                      billingAccount.currencyCode
                                    )}
                                  </span>
                                ) : null}
                                <Button
                                  asChild
                                  size="sm"
                                  variant="link"
                                  className="h-auto px-0 py-0 text-xs"
                                >
                                  <Link
                                    href={`/estate/property-management/EstatePropertyManagementGroundRent?assetId=${encodeURIComponent(
                                      asset.id
                                    )}`}
                                  >
                                    {billingAccount
                                      ? 'Manage billing'
                                      : 'Set up billing'}
                                  </Link>
                                </Button>
                              </>
                            )}
                          </div>
                        </TableCell>
                        <TableCell>
                          {asset.propertyFileReference || 'Not recorded'}
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline">
                            {getLeaseStatus(asset)}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button
                                type="button"
                                size="sm"
                                variant="outline"
                                className="gap-1"
                                aria-label={`Open actions for ${asset.name}`}
                              >
                                Actions
                                <ChevronDown className="h-3.5 w-3.5" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end" className="w-56">
                              <DropdownMenuItem asChild>
                                <Link
                                  href={buildProcedurePrefillHref(
                                    '/estate/EstateLeaseRenewal',
                                    asset,
                                    'Lease renewal',
                                    'Lease'
                                  )}
                                >
                                  <RefreshCw className="mr-2 h-3.5 w-3.5" />
                                  Renewal
                                </Link>
                              </DropdownMenuItem>
                              <DropdownMenuItem asChild>
                                <Link
                                  href={buildProcedurePrefillHref(
                                    '/legal/LegalTerminationRecognition',
                                    asset,
                                    'Lease termination',
                                    'Lease'
                                  )}
                                >
                                  <ShieldCheck className="mr-2 h-3.5 w-3.5" />
                                  Termination / Legal
                                </Link>
                              </DropdownMenuItem>
                              <DropdownMenuItem asChild>
                                <Link
                                  href={buildProcedurePrefillHref(
                                    '/estate/property-management/EstatePropertyManagementOccupancyAvailability',
                                    asset,
                                    'Reserve occupancy',
                                    'Occupancy / availability',
                                    getOccupancyHandoffFields(asset)
                                  )}
                                >
                                  <Home className="mr-2 h-3.5 w-3.5" />
                                  Occupancy / availability
                                </Link>
                              </DropdownMenuItem>
                              <DropdownMenuItem asChild>
                                <Link
                                  href={buildProcedurePrefillHref(
                                    '/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover',
                                    asset,
                                    'Move-in handover',
                                    'Move-in',
                                    getMoveInHandoffFields(asset)
                                  )}
                                >
                                  <ClipboardCheck className="mr-2 h-3.5 w-3.5" />
                                  Move-in / handover
                                </Link>
                              </DropdownMenuItem>
                              <DropdownMenuItem asChild>
                                <Link
                                  href={buildProcedurePrefillHref(
                                    '/estate/property-management/EstatePropertyManagementBillingServiceCharge',
                                    asset,
                                    'Billing start',
                                    'Billing',
                                    getBillingHandoffFields(asset)
                                  )}
                                >
                                  <CreditCard className="mr-2 h-3.5 w-3.5" />
                                  Billing
                                </Link>
                              </DropdownMenuItem>
                              <DropdownMenuItem asChild>
                                <Link
                                  href={buildProcedurePrefillHref(
                                    '/estate/property-management/EstatePropertyManagementDocumentRecordIndex',
                                    asset,
                                    'Lease records index',
                                    'Document record',
                                    getRecordIndexHandoffFields(asset)
                                  )}
                                >
                                  <FileText className="mr-2 h-3.5 w-3.5" />
                                  Records index
                                </Link>
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          ) : null}
          {leaseTotalPages > 1 ? <Pagination currentPage={leasePage} totalPages={leaseTotalPages} totalItems={leaseTotalItems} pageSize={leasePageSize} onPageChange={setLeasePage} /> : null}
        </CardContent>
      </Card>
    </div>
  );
}
