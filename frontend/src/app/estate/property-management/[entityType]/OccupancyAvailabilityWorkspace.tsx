'use client';

import React from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import {
  AlertTriangle,
  CheckCircle2,
  CreditCard,
  EyeOff,
  Home,
  Loader2,
  MoreHorizontal,
  RefreshCw,
  Search,
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
import { Input } from '@/components/ui/input';
import { Pagination } from '@/components/ui/pagination';
import { Label } from '@/components/ui/label';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
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
import { Textarea } from '@/components/ui/textarea';
import {
  estateLandManagementService,
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { assetMatchesWorkspacePrefill } from './property-workspace-utils';
import { useManagedAssetsPage } from './use-managed-assets-page';

const statusLabels: Record<EstateManagedAssetStatus, string> = {
  [EstateManagedAssetStatus.LandBank]: 'Land bank',
  [EstateManagedAssetStatus.UnderDevelopment]: 'Under development',
  [EstateManagedAssetStatus.Available]: 'Available',
  [EstateManagedAssetStatus.Reserved]: 'Reserved',
  [EstateManagedAssetStatus.Leased]: 'Leased',
  [EstateManagedAssetStatus.Occupied]: 'Occupied',
  [EstateManagedAssetStatus.Sold]: 'Sold',
  [EstateManagedAssetStatus.Retired]: 'Retired',
  [EstateManagedAssetStatus.UnderMaintenance]: 'Under maintenance',
  [EstateManagedAssetStatus.Blocked]: 'Blocked',
};

const controlledStatuses = [
  EstateManagedAssetStatus.Available,
  EstateManagedAssetStatus.Reserved,
  EstateManagedAssetStatus.Leased,
  EstateManagedAssetStatus.Occupied,
  EstateManagedAssetStatus.Sold,
  EstateManagedAssetStatus.UnderMaintenance,
  EstateManagedAssetStatus.Blocked,
  EstateManagedAssetStatus.Retired,
];

function formatDate(value?: string) {
  if (!value) return 'Not recorded';
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

function getPropertyReference(asset: EstateManagedAsset) {
  return asset.projectUnitCode || asset.assetCode;
}

function getOccupant(asset: EstateManagedAsset) {
  return asset.lesseeName || 'Not linked';
}

function getVisibilityLabel(asset: EstateManagedAsset) {
  if (!asset.isPublishedToExternalPortal) return 'Hidden';
  if (asset.externalListingStatus !== 'Published') {
    return asset.externalListingStatus || 'Draft';
  }
  return 'Visible on portal';
}

function getAvailabilityLabel(asset: EstateManagedAsset) {
  if (asset.status !== EstateManagedAssetStatus.Available) {
    return statusLabels[asset.status];
  }
  if (asset.isAvailableForLease && asset.isAvailableForSale) {
    return 'Available for sale and rent';
  }
  if (asset.isAvailableForLease) return 'Available for rent';
  if (asset.isAvailableForSale) return 'Available for sale';
  return 'Available - not listed';
}

function getDefaultNextStatus(asset: EstateManagedAsset) {
  if (asset.status === EstateManagedAssetStatus.Available) {
    return EstateManagedAssetStatus.Blocked;
  }
  if (asset.status === EstateManagedAssetStatus.Reserved) {
    return EstateManagedAssetStatus.Occupied;
  }
  if (asset.status === EstateManagedAssetStatus.UnderMaintenance) {
    return EstateManagedAssetStatus.Available;
  }
  if (asset.status === EstateManagedAssetStatus.Blocked) {
    return EstateManagedAssetStatus.Available;
  }
  return asset.status;
}

function buildHandoverHref(asset: EstateManagedAsset) {
  const reference = asset.propertyFileReference || getPropertyReference(asset);
  const params = new URLSearchParams({
    assetId: asset.id,
    title: `Move-in / handover - ${asset.name}`,
    referenceNumber: reference,
    applicantName: asset.lesseeName || '',
    sourceDepartment: 'Estate / Property Management',
    description: `Handover action for ${getPropertyReference(asset)}.`,
    field_sourceWorkspace: 'Occupancy / Availability Operations',
    field_propertyUnit: getPropertyReference(asset),
    field_sourceReference: reference,
    field_leaseReference: reference,
    field_occupantReference: asset.lesseeName || '',
    field_handoverType:
      asset.status === EstateManagedAssetStatus.Occupied ? 'Move-out' : 'Move-in',
    field_occupancyUpdate:
      asset.status === EstateManagedAssetStatus.Occupied
        ? 'Release unit'
        : 'Mark occupied',
  });
  return `/estate/property-management/EstatePropertyManagementMoveInMoveOutHandover?${params.toString()}`;
}

function buildBillingHref(asset: EstateManagedAsset) {
  const reference = asset.propertyFileReference || getPropertyReference(asset);
  const params = new URLSearchParams({
    assetId: asset.id,
    title: `Billing action - ${asset.name}`,
    referenceNumber: reference,
    applicantName: asset.lesseeName || '',
    sourceDepartment: 'Estate / Property Management',
    description: `Billing action for ${getPropertyReference(asset)}.`,
    field_sourceWorkspace: 'Occupancy / Availability Operations',
    field_propertyUnit: getPropertyReference(asset),
    field_sourceReference: reference,
    field_leaseReference: reference,
    field_occupantReference: asset.lesseeName || '',
    field_chargeType:
      asset.assetType === EstateManagedAssetType.Land ? 'Ground rent' : 'Rent',
    field_financeArActionRequested:
      asset.status === EstateManagedAssetStatus.Occupied
        ? 'Create invoice'
        : 'Hold billing',
  });
  return `/estate/property-management/EstatePropertyManagementBillingServiceCharge?${params.toString()}`;
}

function hasLeaseStartEvidence(asset: EstateManagedAsset) {
  return Boolean(asset.propertyFileReference && (asset.rightOfEntryDate || asset.dateOfTenancy));
}

export function OccupancyAvailabilityWorkspace() {
  const searchParams = useSearchParams();
  const prefillAssetId = searchParams.get('assetId');
  const prefillReference =
    searchParams.get('field_propertyUnit') || searchParams.get('referenceNumber');
  const initialSearch = prefillReference || '';
  const [searchDraft, setSearchDraft] = React.useState(initialSearch);
  const [search, setSearch] = React.useState(initialSearch);
  const [statusFilter, setStatusFilter] = React.useState('active');
  const [selectedAssetId, setSelectedAssetId] = React.useState('');
  const [nextStatus, setNextStatus] = React.useState<EstateManagedAssetStatus>(
    EstateManagedAssetStatus.Blocked
  );
  const [leaseAvailable, setLeaseAvailable] = React.useState(false);
  const [saleAvailable, setSaleAvailable] = React.useState(false);
  const [notes, setNotes] = React.useState('');
  const [isSaving, setIsSaving] = React.useState(false);
  const statusQuery =
    statusFilter === 'active'
      ? controlledStatuses.filter(
          (status) => status !== EstateManagedAssetStatus.Retired
        )
      : statusFilter === 'all' || statusFilter === 'portal' || statusFilter === 'hidden'
        ? undefined
        : [Number(statusFilter) as EstateManagedAssetStatus];
  const {
    assets,
    setAssets,
    page,
    setPage,
    isLoading,
    loadError,
    loadAssets,
    pageSize,
    totalPages,
    totalItems,
  } = useManagedAssetsPage({
    search: search || undefined,
    statuses: statusQuery,
    publishedToExternalPortal:
      statusFilter === 'portal'
        ? true
        : statusFilter === 'hidden'
          ? false
          : undefined,
    errorMessage: 'Unable to load occupancy and availability records.',
  });

  const selectedAsset = assets.find((asset) => asset.id === selectedAssetId);
  const selectedNeedsLeaseEvidence = selectedAsset
    ? (nextStatus === EstateManagedAssetStatus.Leased ||
        nextStatus === EstateManagedAssetStatus.Occupied) &&
      !hasLeaseStartEvidence(selectedAsset)
    : false;

  const summary = React.useMemo(
    () => ({
      available: assets.filter(
        (asset) => asset.status === EstateManagedAssetStatus.Available
      ).length,
      reserved: assets.filter(
        (asset) => asset.status === EstateManagedAssetStatus.Reserved
      ).length,
      occupied: assets.filter(
        (asset) =>
          asset.status === EstateManagedAssetStatus.Occupied ||
          asset.status === EstateManagedAssetStatus.Leased
      ).length,
      blocked: assets.filter(
        (asset) =>
          asset.status === EstateManagedAssetStatus.Blocked ||
          asset.status === EstateManagedAssetStatus.UnderMaintenance
      ).length,
    }),
    [assets]
  );

  const chooseAsset = (asset: EstateManagedAsset) => {
    setSelectedAssetId(asset.id);
    const status = getDefaultNextStatus(asset);
    setNextStatus(status);
    setLeaseAvailable(
      status === EstateManagedAssetStatus.Available && asset.isAvailableForLease
    );
    setSaleAvailable(
      status === EstateManagedAssetStatus.Available && asset.isAvailableForSale
    );
    setNotes('');
  };

  React.useEffect(() => {
    if (selectedAssetId || (!prefillAssetId && !prefillReference)) return;
    const matchedAsset = assets.find((asset) =>
      assetMatchesWorkspacePrefill(asset, prefillAssetId, prefillReference)
    );
    if (matchedAsset) {
      chooseAsset(matchedAsset);
    }
  }, [assets, prefillAssetId, prefillReference, selectedAssetId]);

  const saveOccupancy = async () => {
    if (!selectedAsset) {
      toast.error('Select a property or unit first.');
      return;
    }

    if (selectedNeedsLeaseEvidence) {
      toast.error(
        'Record the signed agreement reference and agreement start / move-in date in Lease Management first.'
      );
      return;
    }

    setIsSaving(true);
    try {
      const updated = await estateLandManagementService.updateOccupancy(
        selectedAsset.id,
        {
          status: nextStatus,
          isAvailableForLease:
            nextStatus === EstateManagedAssetStatus.Available
              ? leaseAvailable
              : false,
          isAvailableForSale:
            nextStatus === EstateManagedAssetStatus.Available
              ? saleAvailable
              : false,
          isPublishedToExternalPortal:
            nextStatus === EstateManagedAssetStatus.Available ? null : false,
          notes: notes.trim() || null,
        }
      );
      setAssets((current) =>
        current.map((asset) => (asset.id === updated.id ? updated : asset))
      );
      chooseAsset(updated);
      toast.success('Occupancy and availability updated.');
    } catch (error: unknown) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to update occupancy and availability.'
      );
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-3">
            <CardDescription>Available</CardDescription>
            <CardTitle className="text-2xl">{summary.available}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-3">
            <CardDescription>Reserved</CardDescription>
            <CardTitle className="text-2xl">{summary.reserved}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-3">
            <CardDescription>Leased / occupied</CardDescription>
            <CardTitle className="text-2xl">{summary.occupied}</CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-3">
            <CardDescription>Blocked / maintenance</CardDescription>
            <CardTitle className="text-2xl">{summary.blocked}</CardTitle>
          </CardHeader>
        </Card>
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_24rem]">
        <Card>
          <CardHeader className="gap-4">
            <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
              <div>
                <CardTitle className="flex items-center gap-2">
                  <Home className="h-5 w-5 text-primary" />
                  Occupancy and Availability Board
                </CardTitle>
                <CardDescription className="mt-2 max-w-3xl">
                  Control whether each property or unit is available, reserved,
                  occupied, sold, blocked, or under maintenance. Non-available
                  statuses automatically hide the portal listing and block new
                  customer requests.
                </CardDescription>
              </div>
              <Button
                type="button"
                variant="outline"
                size="icon"
                disabled={isLoading}
                onClick={() => void loadAssets(page)}
                aria-label="Refresh occupancy board"
              >
                <RefreshCw className="h-4 w-4" />
              </Button>
            </div>

            <form
              className="grid gap-2 lg:grid-cols-[minmax(14rem,1fr)_14rem_auto]"
              onSubmit={(event) => {
                event.preventDefault();
                setPage(1);
                setSearch(searchDraft.trim());
              }}
            >
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={searchDraft}
                  onChange={(event) => setSearchDraft(event.target.value)}
                  className="pl-9"
                  placeholder="Search property, unit, lessee, or file reference"
                />
              </div>
              <Select value={statusFilter} onValueChange={(value) => { setPage(1); setStatusFilter(value); }}>
                <SelectTrigger>
                  <SelectValue placeholder="Filter status" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="active">Active operations</SelectItem>
                  <SelectItem value="portal">Visible on portal</SelectItem>
                  <SelectItem value="hidden">Hidden from portal</SelectItem>
                  <SelectItem value="all">All records</SelectItem>
                  {controlledStatuses.map((status) => (
                    <SelectItem key={status} value={String(status)}>
                      {statusLabels[status]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button type="submit">Search</Button>
            </form>
          </CardHeader>

          <CardContent>
            {loadError ? (
              <div className="rounded-md border border-destructive/30 bg-destructive/5 p-6 text-center text-sm text-destructive">
                {loadError}
              </div>
            ) : null}
            {isLoading ? (
              <div className="flex items-center justify-center gap-2 py-12 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading occupancy records
              </div>
            ) : null}
            {!isLoading && !loadError && assets.length === 0 ? (
              <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
                No occupancy records match the current filter.
              </div>
            ) : null}

            {!isLoading && !loadError && assets.length > 0 ? (
              <div className="overflow-x-auto rounded-md border">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Property / unit</TableHead>
                      <TableHead>Occupant</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead>Availability</TableHead>
                      <TableHead>Portal</TableHead>
                      <TableHead>Lease / date</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {assets.map((asset) => (
                      <TableRow
                        key={asset.id}
                        className={
                          selectedAssetId === asset.id ? 'bg-muted/40' : ''
                        }
                      >
                        <TableCell>
                          <div className="font-medium">{asset.name}</div>
                          <div className="text-xs text-muted-foreground">
                            {getPropertyReference(asset)}
                          </div>
                        </TableCell>
                        <TableCell>{getOccupant(asset)}</TableCell>
                        <TableCell>
                          <Badge variant="secondary">
                            {statusLabels[asset.status]}
                          </Badge>
                        </TableCell>
                        <TableCell>{getAvailabilityLabel(asset)}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1.5">
                            {asset.isPublishedToExternalPortal ? (
                              <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />
                            ) : (
                              <EyeOff className="h-3.5 w-3.5 text-muted-foreground" />
                            )}
                            <span>{getVisibilityLabel(asset)}</span>
                          </div>
                        </TableCell>
                        <TableCell>
                          <div>{asset.propertyFileReference || 'No file ref'}</div>
                          <div className="text-xs text-muted-foreground">
                            {formatDate(
                              asset.rightOfEntryDate || asset.dateOfTenancy
                            )}
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button type="button" size="icon" variant="ghost" title={`Actions for ${asset.name}`} aria-label={`Actions for ${asset.name}`}>
                                <MoreHorizontal className="h-4 w-4" />
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onSelect={() => chooseAsset(asset)}>
                                Update status
                              </DropdownMenuItem>
                              <DropdownMenuItem asChild>
                                <Link href={buildHandoverHref(asset)}>Handover</Link>
                              </DropdownMenuItem>
                              <DropdownMenuItem asChild>
                                <Link href={buildBillingHref(asset)}>
                                  <CreditCard className="mr-2 h-4 w-4" /> Billing
                                </Link>
                              </DropdownMenuItem>
                            </DropdownMenuContent>
                          </DropdownMenu>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            ) : null}
            {totalPages > 1 ? <Pagination currentPage={page} totalPages={totalPages} totalItems={totalItems} pageSize={pageSize} onPageChange={setPage} /> : null}
          </CardContent>
        </Card>

        <Card className="h-fit">
          <CardHeader>
            <CardTitle>Update availability</CardTitle>
            <CardDescription>
              Status changes are applied to the master property record. Use
              cases/workflow only when an approval process is required.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {selectedAsset ? (
              <div className="rounded-md border bg-muted/20 p-3 text-sm">
                <div className="font-medium">{selectedAsset.name}</div>
                <div className="text-muted-foreground">
                  {getPropertyReference(selectedAsset)} · Current:{' '}
                  {statusLabels[selectedAsset.status]}
                </div>
              </div>
            ) : (
              <div className="rounded-md border border-dashed p-4 text-sm text-muted-foreground">
                Select a row from the board to update its occupancy or
                availability.
              </div>
            )}

            <div className="space-y-2">
              <Label>Status</Label>
              <Select
                value={String(nextStatus)}
                onValueChange={(value) => {
                  const status = Number(value) as EstateManagedAssetStatus;
                  setNextStatus(status);
                  if (status !== EstateManagedAssetStatus.Available) {
                    setLeaseAvailable(false);
                    setSaleAvailable(false);
                  }
                }}
                disabled={!selectedAsset}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select status" />
                </SelectTrigger>
                <SelectContent>
                  {controlledStatuses.map((status) => (
                    <SelectItem key={status} value={String(status)}>
                      {statusLabels[status]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {nextStatus === EstateManagedAssetStatus.Available ? (
              <div className="space-y-3 rounded-md border p-3">
                <div className="text-sm font-medium">
                  Availability when released
                </div>
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={leaseAvailable}
                    onChange={(event) =>
                      setLeaseAvailable(event.target.checked)
                    }
                  />
                  Available for rent / lease
                </label>
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={saleAvailable}
                    onChange={(event) => setSaleAvailable(event.target.checked)}
                  />
                  Available for sale
                </label>
                <div className="flex items-start gap-2 rounded-md bg-amber-500/10 p-2 text-xs text-amber-800">
                  <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                  Portal publishing still happens in Portal Listings after
                  commercial terms are checked.
                </div>
              </div>
            ) : (
              <div className="rounded-md border bg-muted/20 p-3 text-xs text-muted-foreground">
                Only Available properties can enter Portal Listings. All other
                statuses hide the listing and block new requests.
                {[EstateManagedAssetStatus.Reserved, EstateManagedAssetStatus.Blocked, EstateManagedAssetStatus.Retired].includes(nextStatus)
                  ? ' Future rent and ground-rent billing will be paused, and future staff duties cancelled. Existing invoices and attendance remain.'
                  : ''}
              </div>
            )}

            {selectedNeedsLeaseEvidence ? (
              <div className="flex items-start gap-2 rounded-md border border-amber-500/30 bg-amber-500/5 p-3 text-xs text-amber-900">
                <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                <div>
                  Marking as leased or occupied requires the signed agreement
                  reference plus the agreement start / move-in date from Lease
                  Management.
                  {selectedAsset ? (
                    <Button asChild variant="link" size="sm" className="h-auto px-0 py-0 text-xs text-amber-900">
                      <Link href="/estate/property-management/EstatePropertyManagementLease">
                        Open Lease Management
                      </Link>
                    </Button>
                  ) : null}
                </div>
              </div>
            ) : null}

            {selectedAsset && nextStatus === EstateManagedAssetStatus.Reserved
              && !selectedAsset.customerBusinessPartnerId && !selectedAsset.lesseeName ? (
                <div className="rounded-md border border-amber-500/30 bg-amber-500/5 p-3 text-xs text-amber-900">
                  Link the customer in Lease Management before reserving this property.{' '}
                  <Link className="underline" href="/estate/property-management/EstatePropertyManagementLease">Open Lease Management</Link>
                </div>
              ) : null}

            <div className="space-y-2">
              <Label htmlFor="occupancy-notes">Notes</Label>
              <Textarea
                id="occupancy-notes"
                value={notes}
                onChange={(event) => setNotes(event.target.value)}
                placeholder="Reason, approval reference, maintenance note, handover note..."
                rows={4}
                disabled={!selectedAsset}
              />
            </div>

            <Button
              type="button"
              className="w-full"
              disabled={!selectedAsset || isSaving || selectedNeedsLeaseEvidence}
              onClick={() => void saveOccupancy()}
            >
              {isSaving ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : null}
              Save occupancy update
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
