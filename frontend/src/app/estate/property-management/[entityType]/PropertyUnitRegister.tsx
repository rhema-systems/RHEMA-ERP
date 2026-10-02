'use client';

import React from 'react';
import Link from 'next/link';
import {
  Building2,
  Database,
  Ellipsis,
  Eye,
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
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
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
  const [searchDraft, setSearchDraft] = React.useState('');
  const [search, setSearch] = React.useState('');
  const [typeFilter, setTypeFilter] = React.useState('all');
  const [statusFilter, setStatusFilter] = React.useState('all');
  const [sendingListingId, setSendingListingId] = React.useState<string | null>(
    null
  );
  const [importOpen, setImportOpen] = React.useState(false);
  const [viewAsset, setViewAsset] = React.useState<EstateManagedAsset | null>(null);

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
    includeLandDemarcations: true,
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

      if (asset.listingScope === 'demarcation') {
        if (!asset.parentAssetId) {
          throw new Error('Parent land reference is missing for this demarcation.');
        }

        await estateLandManagementService.updateLandDemarcationDisposition(
          asset.parentAssetId,
          asset.id,
          {
            isReadyForProjectManagement: false,
            isPublishedToExternalPortal: true,
            externalListingType: 'Sale',
            externalListingStatus: 'Draft',
            externalListingPrice: null,
            externalSalePrice:
              asset.externalSalePrice ??
              asset.externalListingPrice ??
              asset.targetSalePrice ??
              null,
            externalMonthlyRent: null,
            externalLeaseTermMonths: null,
            externalListingCurrency: asset.currency || 'GHS',
            externalListingNotes: null,
          }
        );
      } else {
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
      }
      toast.success(
        'Property sent to Portal Listings for commercial setup.'
      );
      await loadAssets();
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
      if (asset.listingScope === 'demarcation') {
        if (!asset.parentAssetId) {
          throw new Error('Parent land reference is missing for this demarcation.');
        }

        await estateLandManagementService.updateLandDemarcationDisposition(
          asset.parentAssetId,
          asset.id,
          {
            isReadyForProjectManagement: false,
            isPublishedToExternalPortal: false,
            externalListingType: 'None',
            externalListingStatus: 'Draft',
            externalListingPrice: null,
            externalSalePrice: null,
            externalMonthlyRent: null,
            externalLeaseTermMonths: null,
            externalListingCurrency: asset.currency || 'GHS',
            externalListingNotes: null,
          }
        );
      } else {
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
      }
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
            className="grid gap-2 sm:grid-cols-2 xl:grid-cols-[minmax(12rem,1fr)_11rem_11rem_auto]"
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
              <Table className="table-fixed">
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-[50%] sm:w-[38%] md:w-[32%]">Property / unit</TableHead>
                    <TableHead className="hidden md:table-cell md:w-[28%]">Location</TableHead>
                    <TableHead className="w-[34%] sm:w-[22%] md:w-[17%]">Status</TableHead>
                    <TableHead className="hidden sm:table-cell sm:w-[26%] md:w-[17%]">Portal</TableHead>
                    <TableHead className="w-[16%] text-right sm:w-[14%] md:w-[6%]">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {assets.map((asset) => {
                    const isPortalListing =
                      asset.externalListingType !== 'None';
                    const isDemarcationRow =
                      asset.listingScope === 'demarcation';
                    const portalSendUnavailableReason =
                      asset.assetType === EstateManagedAssetType.Land && !isDemarcationRow
                        ? 'List land through a demarcation.'
                        : isDemarcationRow && !asset.boundaryVerified
                          ? 'Verify this demarcation first.'
                          : isDemarcationRow && asset.isReadyForProjectManagement
                            ? 'Move this demarcation back to internal before sending it to Portal Listings.'
                        : asset.status !== EstateManagedAssetStatus.Available
                          ? 'Set the property status to Available first.'
                          : asset.sourceType !== EstateManagedAssetSourceType.Manual &&
                              asset.sourceType !== EstateManagedAssetSourceType.Imported &&
                              (asset.sourceType !== EstateManagedAssetSourceType.ProjectUnit ||
                                !asset.isPublishedFromProject)
                            ? 'Register or import the property, or publish it from Projects first.'
                            : null;

                    return (
                      <TableRow key={`${asset.listingScope || 'asset'}:${asset.id}`}>
                        <TableCell className="min-w-0">
                          <div className="truncate font-medium" title={getPropertyName(asset)}>
                            {getPropertyName(asset)}
                          </div>
                          <div className="truncate text-xs text-muted-foreground" title={asset.assetCode}>
                            {asset.assetCode} · {asset.unitType || typeLabels[asset.assetType]}
                          </div>
                        </TableCell>
                        <TableCell className="hidden md:table-cell">
                          <div className="flex min-w-0 items-center gap-1.5" title={getLocation(asset) || 'Not recorded'}>
                            <MapPin className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                            <span className="truncate">{getLocation(asset) || 'Not recorded'}</span>
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant="secondary" className="max-w-full whitespace-normal text-center">
                            {statusLabels[asset.status]}
                          </Badge>
                        </TableCell>
                        <TableCell className="hidden sm:table-cell">
                          <Badge variant="outline" className="max-w-full whitespace-normal text-center">
                            {isPortalListing ? asset.externalListingStatus || 'Draft' : 'Not sent'}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right">
                          <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                              <Button type="button" variant="ghost" size="icon" aria-label={`Actions for ${asset.name}`} title="Actions" disabled={sendingListingId === asset.id}>
                                {sendingListingId === asset.id
                                  ? <Loader2 className="h-4 w-4 animate-spin" />
                                  : <Ellipsis className="h-4 w-4" />}
                              </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end">
                              <DropdownMenuItem onSelect={() => setViewAsset(asset)}>
                                <Eye className="mr-2 h-4 w-4" /> View details
                              </DropdownMenuItem>
                              {isDemarcationRow ? (
                                <DropdownMenuItem asChild>
                                  <Link href="/estate/land-management">
                                    <Settings2 className="mr-2 h-4 w-4" /> Open land management
                                  </Link>
                                </DropdownMenuItem>
                              ) : (
                                <DropdownMenuItem asChild>
                                  <Link href={`/estate/property-management/EstatePropertyManagementOccupancyAvailability?assetId=${encodeURIComponent(asset.id)}`}>
                                    <Settings2 className="mr-2 h-4 w-4" /> Manage status
                                  </Link>
                                </DropdownMenuItem>
                              )}
                              {isPortalListing && asset.status === EstateManagedAssetStatus.Available ? (
                                <DropdownMenuItem asChild>
                                  <Link href={`/estate/property-management/listings?assetId=${encodeURIComponent(asset.id)}`}>
                                    <Globe2 className="mr-2 h-4 w-4" /> Open listing
                                  </Link>
                                </DropdownMenuItem>
                              ) : null}
                              {isPortalListing ? (
                                <DropdownMenuItem onSelect={() => void recallFromPortalListings(asset)}>
                                  <Globe2 className="mr-2 h-4 w-4" /> Recall from portal
                                </DropdownMenuItem>
                              ) : (
                                <>
                                  <DropdownMenuItem
                                    disabled={Boolean(portalSendUnavailableReason)}
                                    onSelect={() => void sendProjectPropertyToPortalListings(asset)}
                                  >
                                    <Globe2 className="mr-2 h-4 w-4" /> Send to portal
                                  </DropdownMenuItem>
                                  {portalSendUnavailableReason ? (
                                    <DropdownMenuLabel className="max-w-56 whitespace-normal text-xs font-normal text-muted-foreground">
                                      {portalSendUnavailableReason}
                                    </DropdownMenuLabel>
                                  ) : null}
                                </>
                              )}
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
          {totalPages > 1 ? <Pagination currentPage={page} totalPages={totalPages} totalItems={totalItems} pageSize={pageSize} onPageChange={setPage} /> : null}
        </CardContent>
      </Card>
      <Dialog open={Boolean(viewAsset)} onOpenChange={(open) => { if (!open) setViewAsset(null); }}>
        <DialogContent className="max-h-[85vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{viewAsset?.name || 'Property details'}</DialogTitle>
            <DialogDescription className="sr-only">Managed asset details</DialogDescription>
          </DialogHeader>
          {viewAsset ? (
            <dl className="grid gap-x-6 gap-y-4 text-sm sm:grid-cols-2">
              {[
                ['Property no.', viewAsset.assetCode],
                ['Asset type', typeLabels[viewAsset.assetType]],
                ['Location', getLocation(viewAsset) || 'Not recorded'],
                ['Use', viewAsset.purpose || viewAsset.zoningClassification || 'Not recorded'],
                ['Size', formatArea(viewAsset)],
                ['Lessee / owner', getCurrentLesseeOrOwner(viewAsset) || 'Not linked'],
                ['Tenancy date', formatDate(viewAsset.dateOfTenancy) || 'Not recorded'],
                ['Lease term', viewAsset.leaseTermYears ? `${viewAsset.leaseTermYears} years` : 'Not recorded'],
                ['Ground rent payable', formatGroundRent(viewAsset) || 'Not recorded'],
                ['File reference', viewAsset.propertyFileReference || 'Not recorded'],
                ['Source', sourceLabels[viewAsset.sourceType]],
                ['Project code', viewAsset.projectCode || 'Not recorded'],
                ['Status', statusLabels[viewAsset.status]],
                ['Portal listing', viewAsset.externalListingType !== 'None' ? viewAsset.externalListingStatus || 'Draft' : 'Not sent'],
              ].map(([label, value]) => (
                <div key={label} className="min-w-0 border-b pb-2">
                  <dt className="text-xs text-muted-foreground">{label}</dt>
                  <dd className="mt-1 break-words font-medium">{value}</dd>
                </div>
              ))}
            </dl>
          ) : null}
        </DialogContent>
      </Dialog>
      <EstateAssetImportDialog open={importOpen} onOpenChange={setImportOpen} onImported={() => void loadAssets()} />
    </div>
  );
}
