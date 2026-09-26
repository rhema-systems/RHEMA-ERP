'use client';

import React from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import {
  Building2,
  CheckCircle2,
  Eye,
  ImagePlus,
  Loader2,
  MapPin,
  Search,
  Send,
  Star,
  Trash2,
} from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Pagination } from '@/components/ui/pagination';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Textarea } from '@/components/ui/textarea';
import {
  estateLandManagementService,
  EstateManagedAssetType,
  type EstateManagedAsset,
  type EstateManagedAssetDocument,
} from '@/services/estate-land-management.service';
import { getStatusBadgeClassName } from '@/lib/status-badge';
import { getListingPriceDefaults } from '@/lib/estate-listing-pricing';

const LISTINGS_PER_PAGE = 10;

function assetTypeLabel(value: EstateManagedAssetType) {
  if (value === EstateManagedAssetType.Land) return 'Land';
  if (value === EstateManagedAssetType.Facility) return 'Building';
  return 'Apartment / unit';
}

function formatMoney(value?: number, currency = 'GHS') {
  if (value == null || Number.isNaN(value)) return 'Not priced';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);
}

function includesSale(listingType: string) {
  return listingType === 'Sale' || listingType === 'SaleAndRent' || listingType === 'SaleAndLease';
}

function includesRecurringCharge(listingType: string) {
  return listingType === 'Rent' || listingType === 'Lease' || listingType === 'SaleAndRent' || listingType === 'SaleAndLease';
}

function isLeaseListing(listingType: string) {
  return listingType === 'Lease' || listingType === 'SaleAndLease';
}

function commercialSummary(asset: EstateManagedAsset) {
  const currency = asset.externalListingCurrency || asset.currency;
  if (asset.externalListingType === 'Rent') {
    return `${formatMoney(asset.externalMonthlyRent, currency)} / month`;
  }
  if (asset.externalListingType === 'Lease') {
    return `${formatMoney(asset.externalListingPrice ?? asset.externalMonthlyRent, currency)} full term`;
  }
  if (asset.externalListingType === 'SaleAndRent') {
    return `${formatMoney(asset.externalSalePrice, currency)} sale · ${formatMoney(asset.externalMonthlyRent, currency)} / month`;
  }
  if (asset.externalListingType === 'SaleAndLease') {
    return `${formatMoney(asset.externalSalePrice, currency)} sale · ${formatMoney(asset.externalListingPrice ?? asset.externalMonthlyRent, currency)} lease`;
  }
  return formatMoney(
    asset.externalSalePrice ||
      asset.externalListingPrice ||
      asset.valuationAmount,
    currency
  );
}

function listingStatusLabel(asset: EstateManagedAsset) {
  const status = asset.externalListingStatus?.trim();
  if (asset.isPublishedToExternalPortal && asset.externalListingStatus) {
    return asset.externalListingStatus;
  }
  if (status && status !== 'Draft') return status;
  if (asset.listingScope === 'demarcation') return 'Pending publication';
  return status || 'Draft';
}

function listingTypeLabel(asset: EstateManagedAsset) {
  if (asset.externalListingType === 'SaleAndRent') {
    return 'Sale and rent';
  }
  if (asset.externalListingType === 'SaleAndLease') {
    return 'Sale and lease';
  }
  if (asset.externalListingType && asset.externalListingType !== 'None') {
    return asset.externalListingType;
  }
  if (asset.isAvailableForSale && asset.isAvailableForLease) {
    return 'Sale';
  }
  if (asset.isAvailableForSale) return 'Sale';
  if (asset.isAvailableForLease) {
    return 'Rent';
  }
  return 'Not set';
}

function isWorkflowLockedListingStatus(status?: string | null) {
  const normalized = (status || '').trim().toLowerCase();
  return normalized === 'reserved' || normalized.includes('estate');
}

export default function EstatePropertyListingsPage() {
  const searchParams = useSearchParams();
  const requestedAssetId = searchParams.get('assetId');
  const requestedListingType = searchParams.get('listingType');
  const [assets, setAssets] = React.useState<EstateManagedAsset[]>([]);
  const [documents, setDocuments] = React.useState<
    EstateManagedAssetDocument[]
  >([]);
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [requestedAssetUnavailable, setRequestedAssetUnavailable] =
    React.useState(false);
  const [search, setSearch] = React.useState('');
  const [listingPage, setListingPage] = React.useState(1);
  const [listingHasNextPage, setListingHasNextPage] = React.useState(false);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [isUploading, setIsUploading] = React.useState(false);
  const [form, setForm] = React.useState({
    isPublishedToExternalPortal: false,
    externalListingType: 'Rent',
    externalListingStatus: 'Published',
    externalSalePrice: '',
    externalMonthlyRent: '',
    externalLeaseAmount: '',
    externalGroundRentRequired: '',
    externalPremiumChargeRequired: '',
    externalListingCurrency: 'GHS',
    externalListingNotes: '',
  });

  const selected = React.useMemo(
    () => assets.find((asset) => asset.id === selectedId),
    [assets, selectedId]
  );

  const loadAssets = React.useCallback(
    async (query?: string, page = 1) => {
      setIsLoading(true);
      try {
        const skip = Math.max(0, page - 1) * LISTINGS_PER_PAGE;
        const take = LISTINGS_PER_PAGE + 1;
        const [managedAssets, demarcations] = await Promise.all([
          estateLandManagementService.getManagedAssets({
            search: query,
            portalListingCandidates: true,
            skip,
            take,
          }),
          estateLandManagementService.getPortalListingDemarcations(
            query,
            skip,
            take
          ),
        ]);
        const nonLandAssets = managedAssets.filter(
          (asset) => asset.assetType !== EstateManagedAssetType.Land
        );
        setListingHasNextPage(
          nonLandAssets.length > LISTINGS_PER_PAGE ||
            demarcations.length > LISTINGS_PER_PAGE
        );
        const data = [
          ...nonLandAssets.slice(0, LISTINGS_PER_PAGE),
          ...demarcations.slice(0, LISTINGS_PER_PAGE),
        ];
        setAssets(data);
        const requestedAsset = requestedAssetId
          ? data.find((asset) => asset.id === requestedAssetId)
          : undefined;
        if (requestedAssetId) {
          setRequestedAssetUnavailable(!requestedAsset);
          setSelectedId(requestedAsset?.id ?? null);
        } else {
          setRequestedAssetUnavailable(false);
          setSelectedId((current) =>
            current && data.some((asset) => asset.id === current)
              ? current
              : null
          );
        }
      } catch {
        if (requestedAssetId) {
          setSelectedId(null);
          setRequestedAssetUnavailable(true);
        }
        toast.error('Unable to load managed assets.');
      } finally {
        setIsLoading(false);
      }
    },
    [requestedAssetId]
  );

  const loadDocuments = React.useCallback(async (assetId: string) => {
    try {
      setDocuments(await estateLandManagementService.getDocuments(assetId));
    } catch {
      setDocuments([]);
    }
  }, []);

  React.useEffect(() => {
    void loadAssets();
  }, [loadAssets]);

  React.useEffect(() => {
    if (!selected) {
      setDocuments([]);
      return;
    }

    const requestedType =
      requestedListingType === 'Sale' ||
      requestedListingType === 'Rent' ||
      requestedListingType === 'Lease'
        ? requestedListingType
        : null;
    const savedListingType =
      selected.externalListingType && selected.externalListingType !== 'None'
        ? selected.externalListingType
        : null;
    const listingPrices = getListingPriceDefaults(selected);
    setForm({
      isPublishedToExternalPortal: selected.isPublishedToExternalPortal,
      externalListingType:
        savedListingType ??
        requestedType ??
        'Rent',
      externalListingStatus:
        selected.externalListingStatus ||
        (selected.listingScope === 'demarcation' ? 'Draft' : 'Published'),
      externalSalePrice: listingPrices.salePrice == null
        ? ''
        : String(listingPrices.salePrice),
      externalMonthlyRent:
        selected.externalMonthlyRent == null &&
        includesRecurringCharge(savedListingType ?? selected.externalListingType)
          ? selected.externalListingPrice == null
            ? ''
            : String(selected.externalListingPrice)
          : selected.externalMonthlyRent == null
            ? ''
            : String(selected.externalMonthlyRent),
      externalLeaseAmount: listingPrices.leasePrice == null
        ? ''
        : String(listingPrices.leasePrice),
      externalGroundRentRequired:
        selected.externalGroundRentRequired == null
          ? selected.isPublishedToExternalPortal && selected.groundRentPayable != null && selected.groundRentPayable > 0
            ? 'Yes'
            : ''
          : selected.externalGroundRentRequired ? 'Yes' : 'No',
      externalPremiumChargeRequired:
        selected.externalPremiumChargeRequired == null
          ? ''
          : selected.externalPremiumChargeRequired ? 'Yes' : 'No',
      externalListingCurrency: selected.externalListingCurrency || 'GHS',
      externalListingNotes: selected.externalListingNotes || '',
    });
    void loadDocuments(
      selected.listingScope === 'demarcation'
        ? selected.parentAssetId || selected.id
        : selected.id
    );
  }, [loadDocuments, requestedListingType, selected]);

  const orderedAssets = React.useMemo(
    () =>
      [...assets].sort((left, right) => {
        const rank = (asset: EstateManagedAsset) => {
          const status = listingStatusLabel(asset).toLowerCase();
          if (status.includes('published')) return 0;
          if (status.includes('pending')) return 1;
          if (status.includes('draft')) return 2;
          return 3;
        };
        const rankDelta = rank(left) - rank(right);
        if (rankDelta !== 0) return rankDelta;
        return left.name.localeCompare(right.name);
      }),
    [assets]
  );
  const listingTotalPages = Math.max(
    1,
    listingHasNextPage ? listingPage + 1 : listingPage
  );
  const pagedAssets = orderedAssets.slice(0, LISTINGS_PER_PAGE);
  const listingTotalItems = listingHasNextPage
    ? listingPage * LISTINGS_PER_PAGE + 1
    : (listingPage - 1) * LISTINGS_PER_PAGE + pagedAssets.length;

  React.useEffect(() => {
    setListingPage((current) => Math.min(current, listingTotalPages));
  }, [listingTotalPages]);

  const listingImages = documents.filter((document) => document.isListingImage);
  const publishedCount = assets.filter(
    (asset) =>
      asset.isPublishedToExternalPortal &&
      asset.externalListingStatus === 'Published'
  ).length;
  const listingIncludesSale = includesSale(form.externalListingType);
  const listingIncludesCharge = includesRecurringCharge(
    form.externalListingType
  );
  const listingIsLease = isLeaseListing(form.externalListingType);
  const selectedIsLand = selected?.assetType === EstateManagedAssetType.Land;
  const selectedListingStatus = selected ? listingStatusLabel(selected) : '';
  const listingLockedByWorkflow = isWorkflowLockedListingStatus(
    selectedListingStatus
  );
  const listingPublicationBlockedByGroundRent = Boolean(
    selected &&
      form.isPublishedToExternalPortal &&
      listingIncludesCharge &&
      selectedIsLand &&
      form.externalGroundRentRequired === 'Yes' &&
      !(selected.groundRentPayable != null && selected.groundRentPayable > 0)
  );
  const listingPublicationNeedsGroundRentChoice = Boolean(
    form.isPublishedToExternalPortal && listingIncludesCharge && selectedIsLand && !form.externalGroundRentRequired
  );
  const listingPublicationNeedsPremiumChoice = Boolean(
    form.isPublishedToExternalPortal && listingIncludesCharge && !form.externalPremiumChargeRequired
  );
  const recurringAmount = !listingIsLease && form.externalMonthlyRent
    ? Number(form.externalMonthlyRent)
    : null;
  const leaseAmount = listingIsLease && form.externalLeaseAmount
    ? Number(form.externalLeaseAmount)
    : null;
  const listingPriceDefaults = selected ? getListingPriceDefaults(selected) : null;
  const landBankSalePrice = listingPriceDefaults?.landBankPrice ?? null;

  const saveListing = async () => {
    if (!selected) return;
    if (listingLockedByWorkflow) {
      toast.error(
        'This listing is reserved for an active Estate case and cannot be republished manually.'
      );
      return;
    }
    if (listingPublicationBlockedByGroundRent) {
      toast.error(
        selected.listingScope === 'demarcation'
          ? 'Assess annual ground rent for this demarcated land portion before publishing.'
          : 'Assess annual ground rent before publishing this land listing.'
      );
      return;
    }
    if (listingPublicationNeedsGroundRentChoice) {
      toast.error('Select whether annual ground rent is required.');
      return;
    }
    if (listingPublicationNeedsPremiumChoice) {
      toast.error('Select whether a premium charge is required.');
      return;
    }

    setIsSaving(true);
    try {
      const resolvedListingType = form.externalListingType;
      const resolvedIncludesSale = includesSale(resolvedListingType);
      const resolvedIncludesCharge = includesRecurringCharge(resolvedListingType);
      const payload = {
        isPublishedToExternalPortal: form.isPublishedToExternalPortal,
        externalListingType: resolvedListingType,
        externalListingStatus: form.externalListingStatus,
        externalListingPrice: resolvedIncludesSale
          ? form.externalSalePrice
            ? Number(form.externalSalePrice)
            : null
          : listingIsLease ? leaseAmount : recurringAmount,
        externalSalePrice:
          resolvedIncludesSale && form.externalSalePrice
            ? Number(form.externalSalePrice)
            : null,
        externalMonthlyRent: recurringAmount,
        externalGroundRentRequired: selectedIsLand && resolvedIncludesCharge
          ? form.externalGroundRentRequired === 'Yes'
          : null,
        externalPremiumChargeRequired: resolvedIncludesCharge
          ? form.externalPremiumChargeRequired === 'Yes'
          : null,
        externalLeaseTermMonths: null,
        externalListingCurrency: form.externalListingCurrency,
        externalListingNotes: form.externalListingNotes,
      };

      if (selected.listingScope === 'demarcation' && selected.parentAssetId) {
        await estateLandManagementService.updateLandDemarcationDisposition(
          selected.parentAssetId,
          selected.id,
          {
            isReadyForProjectManagement: false,
            ...payload,
          }
        );
        await loadAssets(search, listingPage);
      } else {
        const updated = await estateLandManagementService.updateExternalListing(
          selected.id,
          payload
        );
        setAssets((current) =>
          current.map((asset) => (asset.id === updated.id ? updated : asset))
        );
      }
      toast.success('Portal listing updated.');
    } catch (error: any) {
      toast.error(error?.message || 'Unable to update portal listing.');
    } finally {
      setIsSaving(false);
    }
  };

  const removeListing = async () => {
    if (!selected) return;
    if (listingLockedByWorkflow) {
      toast.error(
        'This listing is reserved for an active Estate case and cannot be removed manually.'
      );
      return;
    }
    setIsSaving(true);
    try {
      const payload = {
        isPublishedToExternalPortal: false,
        externalListingType: 'None',
        externalListingStatus: 'Draft',
        externalListingPrice: null,
        externalSalePrice: null,
        externalMonthlyRent: null,
        externalGroundRentRequired: null,
        externalPremiumChargeRequired: null,
        externalLeaseTermMonths: null,
        externalListingCurrency: form.externalListingCurrency,
        externalListingNotes: null,
      };
      if (selected.listingScope === 'demarcation' && selected.parentAssetId) {
        await estateLandManagementService.updateLandDemarcationDisposition(
          selected.parentAssetId,
          selected.id,
          {
            isReadyForProjectManagement: false,
            ...payload,
          }
        );
      } else {
        await estateLandManagementService.updateExternalListing(
          selected.id,
          payload
        );
      }
      toast.success('Listing removed from Portal Listings.');
      setSelectedId(null);
      await loadAssets(search, listingPage);
    } catch (error: any) {
      toast.error(error?.message || 'Unable to remove the portal listing.');
    } finally {
      setIsSaving(false);
    }
  };

  const uploadImage = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.currentTarget.value = '';
    if (!file || !selected) return;

    setIsUploading(true);
    try {
      const documentAssetId =
        selected.listingScope === 'demarcation'
          ? selected.parentAssetId || selected.id
          : selected.id;
      await estateLandManagementService.uploadDocument(
        documentAssetId,
        file,
        'Listing Image',
        file.name,
        {
          isListingImage: true,
          isPrimaryListingImage: listingImages.length === 0,
        }
      );
      await loadDocuments(documentAssetId);
      toast.success('Listing image attached.');
    } catch (error: any) {
      toast.error(error?.message || 'Unable to upload listing image.');
    } finally {
      setIsUploading(false);
    }
  };

  const setPrimaryImage = async (documentId: string) => {
    if (!selected) return;
    try {
      const documentAssetId =
        selected.listingScope === 'demarcation'
          ? selected.parentAssetId || selected.id
          : selected.id;
      await estateLandManagementService.setPrimaryListingImage(
        documentAssetId,
        documentId
      );
      await loadDocuments(documentAssetId);
      toast.success('Primary listing image updated.');
    } catch (error: any) {
      toast.error(error?.message || 'Unable to set primary listing image.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <Badge variant="outline" className="mb-2 w-fit">
            Estate / Property Management
          </Badge>
          <h1 className="text-2xl font-semibold tracking-normal sm:text-3xl">
            Portal Listings
          </h1>
        </div>
        <div className="flex flex-wrap gap-2">
          <Badge variant="secondary" className="px-3 py-2">
            {publishedCount} published
          </Badge>
          <Button asChild variant="outline">
            <Link href="/estate/property-management">
              <Building2 className="mr-2 h-4 w-4" />
              Workspaces
            </Link>
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader>
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <CardTitle className="text-base">Portal inventory</CardTitle>
            <form
              className="flex w-full gap-2 lg:w-[28rem]"
              onSubmit={(event) => {
                event.preventDefault();
                setListingPage(1);
                void loadAssets(search, 1);
              }}
            >
              <div className="relative flex-1">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Search location, code, name"
                  className="pl-9"
                />
              </div>
              <Button type="submit" variant="outline" size="icon">
                <Search className="h-4 w-4" />
              </Button>
            </form>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {isLoading ? (
            <div className="flex items-center justify-center gap-2 rounded-md border py-12 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Loading assets
            </div>
          ) : null}

          {!isLoading && orderedAssets.length === 0 ? (
            <div className="rounded-md border border-dashed p-8 text-center text-sm text-muted-foreground">
              No portal listing candidates match the current search.
            </div>
          ) : null}

          {!isLoading && orderedAssets.length > 0 ? (
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full min-w-[980px] text-sm">
                <thead className="border-b bg-muted/40 text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2 font-medium">Property</th>
                    <th className="px-3 py-2 font-medium">Asset type</th>
                    <th className="px-3 py-2 font-medium">Request type</th>
                    <th className="px-3 py-2 font-medium">Price</th>
                    <th className="px-3 py-2 font-medium">Location</th>
                    <th className="px-3 py-2 font-medium">Status</th>
                    <th className="px-3 py-2 text-right font-medium">
                      Action
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {pagedAssets.map((asset) => {
                    const active = selected?.id === asset.id;
                    const status = listingStatusLabel(asset);
                    return (
                      <tr
                        key={asset.id}
                        className={active ? 'bg-primary/5' : 'bg-background'}
                      >
                        <td className="px-3 py-3 align-top">
                          <div className="font-medium">{asset.name}</div>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {asset.assetCode}
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          {assetTypeLabel(asset.assetType)}
                        </td>
                        <td className="px-3 py-3 align-top">
                          <Badge variant="secondary">
                            {listingTypeLabel(asset)}
                          </Badge>
                        </td>
                        <td className="px-3 py-3 align-top">
                          {commercialSummary(asset)}
                        </td>
                        <td className="px-3 py-3 align-top text-muted-foreground">
                          <div className="flex max-w-[18rem] items-center gap-2">
                            <MapPin className="h-3.5 w-3.5 shrink-0" />
                            <span className="truncate">
                              {asset.location || 'Location not recorded'}
                            </span>
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <Badge
                            variant="outline"
                            className={getStatusBadgeClassName(status)}
                          >
                            {status}
                          </Badge>
                        </td>
                        <td className="px-3 py-3 text-right align-top">
                          <Button
                            type="button"
                            size="sm"
                            variant={active ? 'default' : 'outline'}
                            className="gap-2"
                            onClick={() => {
                              setRequestedAssetUnavailable(false);
                              setSelectedId(asset.id);
                            }}
                          >
                            <Eye className="h-4 w-4" />
                            View
                          </Button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : null}

          {listingTotalPages > 1 ? (
            <Pagination
              currentPage={listingPage}
              totalPages={listingTotalPages}
              totalItems={listingTotalItems}
              pageSize={LISTINGS_PER_PAGE}
              onPageChange={(page) => {
                setListingPage(page);
                void loadAssets(search, page);
              }}
            />
          ) : null}
        </CardContent>
      </Card>

      <Dialog
        open={Boolean(selected) || requestedAssetUnavailable}
        onOpenChange={(open) => {
          if (!open) {
            setSelectedId(null);
            setRequestedAssetUnavailable(false);
          }
        }}
      >
        <DialogContent className="max-h-[90vh] max-w-5xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{selected?.name || 'Portal listing'}</DialogTitle>
            <DialogDescription>
              Review the listing details, commercial terms, visibility, and
              portal images.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-5">
            {requestedAssetUnavailable ? (
              <div className="rounded-md border border-dashed p-6 text-sm text-muted-foreground">
                The requested managed asset could not be loaded. Search for the
                asset and select it explicitly before editing a listing.
              </div>
            ) : selected ? (
              <>
                <div className="grid gap-3 md:grid-cols-3">
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Type</div>
                    <div className="mt-1 font-medium">
                      {assetTypeLabel(selected.assetType)}
                    </div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Price</div>
                    <div className="mt-1 font-medium">
                      {commercialSummary(selected)}
                    </div>
                  </div>
                  <div className="rounded-md border p-3">
                    <div className="text-xs text-muted-foreground">Source</div>
                    <div className="mt-1 font-medium">
                      Estate / Property Management
                    </div>
                  </div>
                </div>

                <div className="grid gap-4 md:grid-cols-2">
                  <div className="space-y-2">
                    <Label>Customer portal visibility</Label>
                    <Select
                      disabled={listingLockedByWorkflow}
                      value={
                        form.externalListingStatus === 'Published'
                          ? 'published'
                          : 'draft'
                      }
                      onValueChange={(value) =>
                        setForm((current) => ({
                          ...current,
                          isPublishedToExternalPortal: value === 'published',
                          externalListingStatus:
                            value === 'published' ? 'Published' : 'Draft',
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="draft">Not visible</SelectItem>
                        <SelectItem value="published">Visible</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Listing type</Label>
                    <Select
                      disabled={listingLockedByWorkflow}
                      value={
                        listingIsLease
                          ? 'Lease'
                          : listingIncludesCharge
                            ? 'Rent'
                            : 'Sale'
                      }
                      onValueChange={(value) =>
                        setForm((current) => ({
                          ...current,
                          externalListingType: value,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Sale">Sale</SelectItem>
                        <SelectItem value="Rent">Rent</SelectItem>
                        <SelectItem value="Lease">Lease</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Status</Label>
                    <Select
                      disabled={listingLockedByWorkflow}
                      value={form.externalListingStatus}
                      onValueChange={(value) =>
                        setForm((current) => ({
                          ...current,
                          externalListingStatus: value,
                        }))
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="Published">Published</SelectItem>
                        <SelectItem value="Paused">Paused</SelectItem>
                        <SelectItem value="Draft">Draft</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Currency</Label>
                    <Input
                      disabled={listingLockedByWorkflow}
                      value={form.externalListingCurrency}
                      onChange={(event) =>
                        setForm((current) => ({
                          ...current,
                          externalListingCurrency:
                            event.target.value.toUpperCase(),
                        }))
                      }
                    />
                  </div>
                </div>

                <div className="rounded-md border p-4">
                  <div className="mb-4">
                    <div className="font-medium">Commercial terms</div>
                    <div className="text-sm text-muted-foreground">
                      Complete the applicable terms before publishing.
                    </div>
                  </div>
                  <div className="grid gap-4 md:grid-cols-2">
                    {listingIncludesSale ? (
                      <div className="space-y-2">
                        <Label>Sale price</Label>
                        <Input
                          type="number"
                          min="0"
                          disabled={listingLockedByWorkflow}
                          value={form.externalSalePrice}
                          onChange={(event) =>
                            setForm((current) => ({
                              ...current,
                              externalSalePrice: event.target.value,
                            }))
                          }
                          placeholder={
                            landBankSalePrice != null
                              ? `Land Bank ${formatMoney(
                                  landBankSalePrice,
                                  form.externalListingCurrency ||
                                    selected.currency
                                )}`
                              : 'Enter sale price'
                          }
                        />
                        {landBankSalePrice != null ? (
                          <p className="text-xs text-muted-foreground">
                            Land Bank price:{' '}
                            {formatMoney(
                              landBankSalePrice,
                              form.externalListingCurrency || selected.currency
                            )}
                            . This is the default; the portal listing price may be changed.
                          </p>
                        ) : null}
                      </div>
                    ) : null}
                    {listingIncludesCharge ? (
                      <div className="space-y-2">
                        <Label>
                          {listingIsLease ? 'Full-term lease amount' : 'Rent per month'}
                        </Label>
                        <Input
                          type="number"
                          min="0"
                          disabled={listingLockedByWorkflow}
                          value={listingIsLease ? form.externalLeaseAmount : form.externalMonthlyRent}
                          onChange={(event) =>
                            setForm((current) => ({
                              ...current,
                              [listingIsLease ? 'externalLeaseAmount' : 'externalMonthlyRent']: event.target.value,
                            }))
                          }
                          placeholder={listingIsLease ? 'Enter full lease amount' : 'Enter monthly rent'}
                        />
                        {listingIsLease && landBankSalePrice != null ? (
                          <p className="text-xs text-muted-foreground">
                            Land Bank amount: {formatMoney(landBankSalePrice, form.externalListingCurrency || selected.currency)}. This is the default and may be changed for the listing.
                          </p>
                        ) : null}
                        {listingIsLease && listingPriceDefaults?.legacyRecurringLeasePrice && selected.isPublishedToExternalPortal ? (
                          <p className="text-xs text-amber-700">
                            Current published amount: {formatMoney(selected.externalListingPrice, form.externalListingCurrency || selected.currency)}. Save this listing to apply the Land Bank full-term amount.
                          </p>
                        ) : null}
                        <p className="text-xs text-muted-foreground">
                          Sales will capture the agreed customer duration before
                          handing the case to Estate.
                        </p>
                      </div>
                    ) : null}
                    {listingIncludesCharge ? (
                      <div className="space-y-2">
                        <Label>Premium charge required?</Label>
                        <Select
                          value={form.externalPremiumChargeRequired}
                          onValueChange={(value) => setForm((current) => ({ ...current, externalPremiumChargeRequired: value }))}
                          disabled={listingLockedByWorkflow}
                        >
                          <SelectTrigger><SelectValue placeholder="Select Yes or No" /></SelectTrigger>
                          <SelectContent>
                            <SelectItem value="Yes">Yes</SelectItem>
                            <SelectItem value="No">No</SelectItem>
                          </SelectContent>
                        </Select>
                      </div>
                    ) : null}
                  </div>
                </div>

                {listingIncludesCharge && selectedIsLand ? (
                  <div className="space-y-3">
                    <div className="space-y-2">
                      <Label>Annual ground rent required?</Label>
                      <Select
                        value={form.externalGroundRentRequired}
                        onValueChange={(value) => setForm((current) => ({ ...current, externalGroundRentRequired: value }))}
                        disabled={listingLockedByWorkflow}
                      >
                        <SelectTrigger><SelectValue placeholder="Select Yes or No" /></SelectTrigger>
                        <SelectContent>
                          <SelectItem value="Yes">Yes</SelectItem>
                          <SelectItem value="No">No</SelectItem>
                        </SelectContent>
                      </Select>
                    </div>
                    {form.externalGroundRentRequired === 'Yes' ? (
                  <div
                    className={`rounded-md border p-4 ${
                      selected.groundRentPayable != null &&
                      selected.groundRentPayable > 0
                        ? 'border-emerald-200 bg-emerald-50 text-emerald-900'
                        : 'border-amber-200 bg-amber-50 text-amber-900'
                    }`}
                  >
                    <div className="font-medium">Ground rent assessment</div>
                    <p className="mt-1 text-sm">
                      {selected.groundRentPayable != null &&
                      selected.groundRentPayable > 0
                        ? `Annual ground rent assessed at ${formatMoney(
                            selected.groundRentPayable,
                            form.externalListingCurrency || selected.currency
                          )}. It is billed separately from ${listingIsLease ? 'the full-term lease amount' : 'monthly rent'}. Billing will only start after customer acceptance, signed agreement, and move-in / agreement start date.`
                        : 'Assess and approve annual ground rent through the applicable Estate SOP before publishing. Billing account setup happens later after customer acceptance, signed agreement, and move-in / agreement start date.'}
                    </p>
                    {selected.groundRentPayable == null ||
                    selected.groundRentPayable <= 0 ? (
                      <div className="mt-3 flex flex-wrap gap-2">
                        <Button
                          asChild
                          type="button"
                          size="sm"
                          variant="outline"
                          className="bg-white"
                        >
                          <Link
                            href={`/estate/property-management/EstatePropertyManagementGroundRent?assetId=${encodeURIComponent(
                              selected.id
                            )}`}
                          >
                            Set up ground rent
                          </Link>
                        </Button>
                      </div>
                    ) : null}
                  </div>
                    ) : null}
                  </div>
                ) : null}

                {listingLockedByWorkflow ? (
                  <div className="rounded-md border border-violet-200 bg-violet-50 p-4 text-sm text-violet-900">
                    This listing is reserved for an active Estate case. It is no
                    longer visible to external customers and can only be
                    released by rejecting the Estate application workflow.
                  </div>
                ) : null}

                <div className="space-y-2">
                  <Label>Listing notes</Label>
                  <Textarea
                    disabled={listingLockedByWorkflow}
                    value={form.externalListingNotes}
                    onChange={(event) =>
                      setForm((current) => ({
                        ...current,
                        externalListingNotes: event.target.value,
                      }))
                    }
                  />
                </div>

                <div className="flex flex-wrap justify-end gap-2">
                  <Button
                    variant="outline"
                    onClick={() => void removeListing()}
                    disabled={isSaving || listingLockedByWorkflow}
                  >
                    <Trash2 className="mr-2 h-4 w-4" />
                    Remove from Portal Listings
                  </Button>
                  <Button
                    onClick={saveListing}
                    disabled={
                      isSaving ||
                      listingLockedByWorkflow ||
                      listingPublicationBlockedByGroundRent ||
                      listingPublicationNeedsGroundRentChoice ||
                      listingPublicationNeedsPremiumChoice
                    }
                    title={
                      listingLockedByWorkflow
                        ? 'Reserved listings cannot be republished manually.'
                        : listingPublicationNeedsPremiumChoice
                        ? 'Select whether a premium charge is required.'
                        : listingPublicationNeedsGroundRentChoice
                        ? 'Select whether annual ground rent is required.'
                        : listingPublicationBlockedByGroundRent
                        ? selected.listingScope === 'demarcation'
                          ? 'Assess annual ground rent for this demarcated land portion before publishing.'
                          : 'Assess annual ground rent before publishing this land listing.'
                        : undefined
                    }
                  >
                    {isSaving ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Send className="mr-2 h-4 w-4" />
                    )}
                    Save listing
                  </Button>
                </div>

                <div className="rounded-md border p-4">
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                    <div>
                      <div className="font-medium">Listing Images</div>
                      <div className="text-sm text-muted-foreground">
                        {listingImages.length} image
                        {listingImages.length === 1 ? '' : 's'}
                      </div>
                    </div>
                    <div className="relative">
                      <Input
                        type="file"
                        accept="image/*"
                        disabled={isUploading}
                        onChange={uploadImage}
                      />
                      {isUploading ? (
                        <Loader2 className="absolute right-3 top-2.5 h-4 w-4 animate-spin" />
                      ) : (
                        <ImagePlus className="pointer-events-none absolute right-3 top-2.5 h-4 w-4 text-muted-foreground" />
                      )}
                    </div>
                  </div>

                  <div className="mt-4 space-y-2">
                    {listingImages.length === 0 ? (
                      <div className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
                        No listing images attached.
                      </div>
                    ) : null}
                    {listingImages.map((document) => (
                      <div
                        key={document.id}
                        className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 text-sm"
                      >
                        <div className="min-w-0">
                          <div className="truncate font-medium">
                            {document.documentName || document.fileName}
                          </div>
                          {document.isPrimaryListingImage ? (
                            <Badge variant="secondary" className="mt-1">
                              <CheckCircle2 className="mr-1 h-3 w-3" />
                              Primary
                            </Badge>
                          ) : null}
                        </div>
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={document.isPrimaryListingImage}
                          onClick={() => void setPrimaryImage(document.id)}
                        >
                          <Star className="mr-1 h-4 w-4" />
                          Primary
                        </Button>
                      </div>
                    ))}
                  </div>
                </div>
              </>
            ) : (
              <div className="rounded-md border py-16 text-center text-sm text-muted-foreground">
                No Portal Listing candidate is selected.
              </div>
            )}
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
