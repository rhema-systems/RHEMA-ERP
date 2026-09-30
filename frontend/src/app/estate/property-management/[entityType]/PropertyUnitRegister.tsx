'use client';

import React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  Building2,
  Database,
  Globe2,
  Loader2,
  MapPin,
  RefreshCw,
  Search,
  Settings2,
  Upload,
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
  estateLandManagementService,
  EstateManagedAssetSourceType,
  EstateManagedAssetStatus,
  EstateManagedAssetType,
  type EstateManagedAsset,
} from '@/services/estate-land-management.service';
import { useManagedAssetsPage } from './use-managed-assets-page';
import { EstateAssetImportDialog } from './EstateAssetImportDialog';

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

const typeLabels: Record<EstateManagedAssetType, string> = {
  [EstateManagedAssetType.Land]: 'Land / plot',
  [EstateManagedAssetType.Property]: 'Property / unit',
  [EstateManagedAssetType.Facility]: 'Facility',
};

const sourceLabels: Record<EstateManagedAssetSourceType, string> = {
  [EstateManagedAssetSourceType.Manual]: 'Estate records',
  [EstateManagedAssetSourceType.LandAcquisition]: 'Land acquisition',
  [EstateManagedAssetSourceType.ProjectUnit]: 'Project unit',
  [EstateManagedAssetSourceType.Imported]: 'Excel import',
};

function formatArea(asset: EstateManagedAsset) {
  if (asset.areaValue != null) {
    return `${new Intl.NumberFormat().format(asset.areaValue)} ${
      asset.areaUnit || ''
    }`.trim();
  }
  if (asset.areaSquareMeters != null) {
    return `${new Intl.NumberFormat().format(asset.areaSquareMeters)} m²`;
  }
  return 'Not recorded';
}

function formatDate(value?: string) {
  if (!value) return null;
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

function formatGroundRent(asset: EstateManagedAsset) {
  if (asset.groundRentPayable == null) return null;
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: asset.currency || 'GHS',
  }).format(asset.groundRentPayable);
}

function getLocation(asset: EstateManagedAsset) {
  return (
    asset.location ||
    [asset.town, asset.district, asset.region].filter(Boolean).join(', ')
  );
}

function getPropertyName(asset: EstateManagedAsset) {
  const hierarchy = [asset.blockName, asset.floorLabel]
    .filter(Boolean)
    .join(' · ');
  return hierarchy ? `${asset.name} · ${hierarchy}` : asset.name;
}

function getCurrentLesseeOrOwner(asset: EstateManagedAsset) {
  if (asset.lesseeName) return asset.lesseeName;
  return (
    asset.ownershipHistory?.find((owner) => owner.isCurrentOwner)?.ownerName ||
    asset.ownershipHistory?.[0]?.ownerName
  );
}

export function PropertyUnitRegister() {
  const router = useRouter();
  const [searchDraft, setSearchDraft] = React.useState('');
  const [search, setSearch] = React.useState('');
  const [typeFilter, setTypeFilter] = React.useState('all');
  const [statusFilter, setStatusFilter] = React.useState('all');
  const [sendingListingId, setSendingListingId] = React.useState<string | null>(
    null
  );
  const [importOpen, setImportOpen] = React.useState(false);

  React.useEffect(() => {
    const importType = new URLSearchParams(window.location.search).get(
      'import'
    );
    if (importType === 'land' || importType === 'property') {
      setImportOpen(true);
    }
  }, []);
  const {
    assets,
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
    assetType:
      typeFilter === 'all'
        ? undefined
        : (Number(typeFilter) as EstateManagedAssetType),
    status:
      statusFilter === 'all'
        ? undefined
        : (Number(statusFilter) as EstateManagedAssetStatus),
    errorMessage: 'Unable to load the Property and Unit Register.',
  });

  const clearFilters = () => {
    setSearchDraft('');
    setSearch('');
    setPage(1);
    setTypeFilter('all');
    setStatusFilter('all');
  };

  const sendProjectPropertyToPortalListings = async (
    asset: EstateManagedAsset
  ) => {
    try {
      setSendingListingId(asset.id);
      if (asset.externalListingType !== 'None') {
        toast.info('This property is already in Portal Listings.');
        return;
      }

      const listingType = asset.isAvailableForSale ? 'Sale' : 'Rent';
      await estateLandManagementService.updateExternalListing(asset.id, {
        isPublishedToExternalPortal: false,
        externalListingType: listingType,
        externalListingStatus: 'Draft',
        externalListingPrice: null,
        externalSalePrice: null,
        externalMonthlyRent: null,
        externalLeaseTermMonths: null,
        externalListingCurrency: asset.currency || 'GHS',
        externalListingNotes: null,
      });
      toast.success(
        'Property sent to Portal Listings for commercial setup.'
      );
      router.push(
        `/estate/property-management/listings?assetId=${encodeURIComponent(asset.id)}`
      );
    } catch (error: unknown) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to send the property to Portal Listings.'
      );
    } finally {
      setSendingListingId(null);
    }
  };

  const recallFromPortalListings = async (asset: EstateManagedAsset) => {
    try {
      setSendingListingId(asset.id);
      await estateLandManagementService.updateExternalListing(asset.id, {
        isPublishedToExternalPortal: false,
        externalListingType: 'None',
        externalListingStatus: 'Draft',
        externalListingPrice: null,
        externalSalePrice: null,
        externalMonthlyRent: null,
        externalLeaseTermMonths: null,
        externalListingCurrency: asset.currency || 'GHS',
        externalListingNotes: null,
      });
      toast.success('Property recalled from Portal Listings.');
      await loadAssets();
    } catch (error: unknown) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Unable to recall the property from Portal Listings.'
      );
    } finally {
      setSendingListingId(null);
    }
  };

  const landCount = assets.filter(
    (asset) => asset.assetType === EstateManagedAssetType.Land
  ).length;
  const propertyUnitCount = assets.filter(
    (asset) => asset.assetType === EstateManagedAssetType.Property
  ).length;
  const occupiedCount = assets.filter(
    (asset) =>
      asset.status === EstateManagedAssetStatus.Occupied ||
      asset.status === EstateManagedAssetStatus.Leased
  ).length;

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {[
          { label: 'Register records', value: assets.length },
          { label: 'Land and plots', value: landCount },
          { label: 'Properties and units', value: propertyUnitCount },
          { label: 'Leased or occupied', value: occupiedCount },
        ].map((item) => (
          <Card key={item.label}>
            <CardHeader className="space-y-1 pb-3">
              <CardDescription>{item.label}</CardDescription>
              <CardTitle className="text-2xl">{item.value}</CardTitle>
            </CardHeader>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader className="gap-3">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Database className="h-5 w-5 text-primary" />
                Estate Property Master Register
              </CardTitle>
              <CardDescription className="mt-2 max-w-3xl">
                This is the read-only master record for property identity,
                location, use, ownership, source, and current status. Customer
                assignment and commercial terms are managed in Lease Management.
              </CardDescription>
            </div>
            <div className="flex items-center gap-2">
              <Button type="button" onClick={() => setImportOpen(true)}>
                <Upload className="mr-2 h-4 w-4" /> Import Property
              </Button>
              <Badge variant="outline">Estates Records</Badge>
            </div>
          </div>

          <form
            className="grid gap-2 lg:grid-cols-[minmax(16rem,1fr)_13rem_13rem_auto]"
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
                placeholder="Property number, name, project, use, or location"
                aria-label="Search the Property and Unit Register"
              />
            </div>
            <Select value={typeFilter} onValueChange={(value) => { setPage(1); setTypeFilter(value); }}>
              <SelectTrigger aria-label="Filter by record type">
                <SelectValue placeholder="All record types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All record types</SelectItem>
                {Object.entries(typeLabels).map(([value, label]) => (
                  <SelectItem key={value} value={value}>
                    {label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={statusFilter} onValueChange={(value) => { setPage(1); setStatusFilter(value); }}>
              <SelectTrigger aria-label="Filter by status">
                <SelectValue placeholder="All statuses" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                {Object.entries(statusLabels).map(([value, label]) => (
                  <SelectItem key={value} value={value}>
                    {label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <div className="flex gap-2">
              <Button type="submit">Search</Button>
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={clearFilters}
                aria-label="Clear filters"
              >
                <RefreshCw className="h-4 w-4" />
              </Button>
            </div>
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
              Loading Estate records
            </div>
          ) : null}
          {!isLoading && !loadError && assets.length === 0 ? (
            <div className="rounded-md border border-dashed p-10 text-center">
              <Building2 className="mx-auto h-8 w-8 text-muted-foreground" />
              <div className="mt-3 font-medium">No register records found</div>
              <div className="mt-1 text-sm text-muted-foreground">
                Change the search or filters and try again.
              </div>
            </div>
          ) : null}

          {!isLoading && !loadError && assets.length > 0 ? (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Property no.</TableHead>
                    <TableHead>Property / unit</TableHead>
                    <TableHead>Location</TableHead>
                    <TableHead>Use</TableHead>
                    <TableHead>Size</TableHead>
                    <TableHead>Lessee / owner</TableHead>
                    <TableHead>Lease record</TableHead>
                    <TableHead>File reference</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Source</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {assets.map((asset) => {
                    const lesseeOrOwner = getCurrentLesseeOrOwner(asset);
                    const isPortalListing =
                      asset.externalListingType !== 'None';
                    const canSendProjectProperty =
                      (asset.sourceType === EstateManagedAssetSourceType.Imported ||
                        (asset.sourceType === EstateManagedAssetSourceType.ProjectUnit &&
                          asset.isPublishedFromProject)) &&
                      asset.status === EstateManagedAssetStatus.Available &&
                      (asset.assetType === EstateManagedAssetType.Property ||
                        asset.assetType === EstateManagedAssetType.Facility);

                    return (
                      <TableRow key={asset.id}>
                        <TableCell className="font-medium">
                          <div>{asset.assetCode}</div>
                          {asset.projectUnitCode &&
                          asset.projectUnitCode !== asset.assetCode ? (
                            <div className="text-xs text-muted-foreground">
                              Unit {asset.projectUnitCode}
                            </div>
                          ) : null}
                        </TableCell>
                        <TableCell>
                          <div className="min-w-48 font-medium">
                            {getPropertyName(asset)}
                          </div>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {asset.unitType || typeLabels[asset.assetType]}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="flex min-w-40 items-start gap-1.5">
                            <MapPin className="mt-0.5 h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                            <span>{getLocation(asset) || 'Not recorded'}</span>
                          </div>
                        </TableCell>
                        <TableCell>
                          {asset.purpose ||
                            asset.zoningClassification ||
                            'Not recorded'}
                        </TableCell>
                        <TableCell className="whitespace-nowrap">
                          {formatArea(asset)}
                        </TableCell>
                        <TableCell>
                          <div className="min-w-36">
                            {lesseeOrOwner || 'Not linked'}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="min-w-44 space-y-0.5 text-xs">
                            {asset.dateOfTenancy ? (
                              <div>
                                Tenancy: {formatDate(asset.dateOfTenancy)}
                              </div>
                            ) : null}
                            {asset.leaseTermYears ? (
                              <div>Lease: {asset.leaseTermYears} years</div>
                            ) : null}
                            {asset.groundRentPayable != null ? (
                              <div>
                                Ground rent payable: {formatGroundRent(asset)}
                              </div>
                            ) : null}
                            {!asset.dateOfTenancy &&
                            !asset.leaseTermYears &&
                            asset.groundRentPayable == null ? (
                              <span className="text-muted-foreground">
                                Not recorded
                              </span>
                            ) : null}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="min-w-32">
                            {asset.propertyFileReference || 'Not recorded'}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant="secondary">
                            {statusLabels[asset.status]}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <div className="min-w-32">
                            {sourceLabels[asset.sourceType]}
                          </div>
                          {asset.projectCode ? (
                            <div className="mt-1 text-xs text-muted-foreground">
                              {asset.projectCode}
                            </div>
                          ) : null}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center justify-end gap-1">
                            <Button asChild variant="ghost" size="icon" title="Manage property status" aria-label={`Manage status for ${asset.name}`}>
                              <Link href={`/estate/property-management/EstatePropertyManagementOccupancyAvailability?assetId=${encodeURIComponent(asset.id)}`}>
                                <Settings2 className="h-4 w-4" />
                              </Link>
                            </Button>
                            {isPortalListing ? (
                              <div className="flex min-w-56 justify-end gap-2">
                                {asset.status === EstateManagedAssetStatus.Available ? (
                                  <Button
                                    variant="ghost"
                                    size="sm"
                                    disabled={sendingListingId === asset.id}
                                    onClick={() =>
                                      router.push(
                                        `/estate/property-management/listings?assetId=${encodeURIComponent(asset.id)}`
                                      )
                                    }
                                  >
                                    <Globe2 className="mr-2 h-3.5 w-3.5" />
                                    Open listing
                                  </Button>
                                ) : null}
                                <Button
                                  variant="outline"
                                  size="sm"
                                  disabled={sendingListingId === asset.id}
                                  onClick={() => void recallFromPortalListings(asset)}
                                >
                                  {sendingListingId === asset.id ? (
                                    <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />
                                  ) : null}
                                  Recall
                                </Button>
                              </div>
                            ) : canSendProjectProperty ? (
                              <div className="flex min-w-44 justify-end">
                              <Button
                                variant="ghost"
                                size="sm"
                                disabled={sendingListingId === asset.id}
                                onClick={() =>
                                  void sendProjectPropertyToPortalListings(asset)
                                }
                              >
                                {sendingListingId === asset.id ? (
                                  <Loader2 className="mr-2 h-3.5 w-3.5 animate-spin" />
                                ) : (
                                  <Globe2 className="mr-2 h-3.5 w-3.5" />
                                )}
                                Send to portal
                              </Button>
                              </div>
                            ) : (
                              <span className="block min-w-32 px-3 text-right text-muted-foreground">
                                —
                              </span>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          ) : null}
          {totalPages > 1 ? <Pagination currentPage={page} totalPages={totalPages} totalItems={totalItems} pageSize={pageSize} onPageChange={setPage} /> : null}
        </CardContent>
      </Card>
      <EstateAssetImportDialog open={importOpen} onOpenChange={setImportOpen} onImported={() => void loadAssets()} />
    </div>
  );
}
