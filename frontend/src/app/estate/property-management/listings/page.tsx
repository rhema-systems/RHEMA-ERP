'use client';

import React from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import {
  Building2,
  CheckCircle2,
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
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
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

function formatLeaseTerm(months?: number) {
  if (!months) return 'Duration not set';
  if (months % 12 === 0) {
    const years = months / 12;
    return `${years} year${years === 1 ? '' : 's'}`;
  }
  return `${months} month${months === 1 ? '' : 's'}`;
}

function commercialSummary(asset: EstateManagedAsset) {
  const currency = asset.externalListingCurrency || asset.currency;
  if (asset.externalListingType === 'Rent') {
    return `${formatMoney(asset.externalMonthlyRent, currency)} / month · ${formatLeaseTerm(asset.externalLeaseTermMonths)}`;
  }
  if (asset.externalListingType === 'SaleAndRent') {
    return `${formatMoney(asset.externalSalePrice, currency)} sale · ${formatMoney(asset.externalMonthlyRent, currency)} / month`;
  }
  return formatMoney(
    asset.externalSalePrice ||
      asset.externalListingPrice ||
      asset.valuationAmount,
    currency
  );
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
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [isUploading, setIsUploading] = React.useState(false);
  const [form, setForm] = React.useState({
    isPublishedToExternalPortal: false,
    externalListingType: 'Rent',
    externalListingStatus: 'Published',
    externalSalePrice: '',
    externalMonthlyRent: '',
    externalLeaseDuration: '',
    externalLeaseDurationUnit: 'months',
    externalListingCurrency: 'GHS',
    externalListingNotes: '',
  });

  const selected = React.useMemo(
    () => assets.find((asset) => asset.id === selectedId),
    [assets, selectedId]
  );

  const loadAssets = React.useCallback(
    async (query?: string) => {
      setIsLoading(true);
      try {
        const data = await estateLandManagementService.getManagedAssets({
          search: query,
          portalListingCandidates: true,
          take: 300,
        });
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
              : (data[0]?.id ?? null)
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
    if (!selected) return;

    const leaseTermMonths = selected.externalLeaseTermMonths;
    const durationUsesYears =
      leaseTermMonths != null &&
      leaseTermMonths > 0 &&
      leaseTermMonths % 12 === 0;
    setForm({
      isPublishedToExternalPortal: selected.isPublishedToExternalPortal,
      externalListingType:
        selected.externalListingType && selected.externalListingType !== 'None'
          ? selected.externalListingType
          : requestedListingType === 'Sale' ||
              requestedListingType === 'Rent' ||
              requestedListingType === 'SaleAndRent'
            ? requestedListingType
            : selected.assetType === EstateManagedAssetType.Land
              ? 'Sale'
              : 'Rent',
      externalListingStatus: selected.externalListingStatus || 'Published',
      externalSalePrice:
        selected.externalSalePrice == null &&
        selected.externalListingType !== 'Rent'
          ? selected.externalListingPrice == null
            ? ''
            : String(selected.externalListingPrice)
          : selected.externalSalePrice == null
            ? ''
            : String(selected.externalSalePrice),
      externalMonthlyRent:
        selected.externalMonthlyRent == null &&
        selected.externalListingType === 'Rent'
          ? selected.externalListingPrice == null
            ? ''
            : String(selected.externalListingPrice)
          : selected.externalMonthlyRent == null
            ? ''
            : String(selected.externalMonthlyRent),
      externalLeaseDuration:
        leaseTermMonths == null
          ? ''
          : String(durationUsesYears ? leaseTermMonths / 12 : leaseTermMonths),
      externalLeaseDurationUnit: durationUsesYears ? 'years' : 'months',
      externalListingCurrency: selected.externalListingCurrency || 'GHS',
      externalListingNotes: selected.externalListingNotes || '',
    });
    void loadDocuments(selected.id);
  }, [loadDocuments, requestedListingType, selected]);

  const listingImages = documents.filter((document) => document.isListingImage);
  const publishedCount = assets.filter(
    (asset) => asset.isPublishedToExternalPortal
  ).length;
  const includesSale =
    form.externalListingType === 'Sale' ||
    form.externalListingType === 'SaleAndRent';
  const includesRent =
    form.externalListingType === 'Rent' ||
    form.externalListingType === 'SaleAndRent';
  const rentPublicationBlockedByGroundRent = Boolean(
    selected &&
      form.isPublishedToExternalPortal &&
      includesRent &&
      selected.assetType === EstateManagedAssetType.Land &&
      !(selected.groundRentPayable != null && selected.groundRentPayable > 0)
  );

  const saveListing = async () => {
    if (!selected) return;
    setIsSaving(true);
    try {
      const updated = await estateLandManagementService.updateExternalListing(
        selected.id,
        {
          isPublishedToExternalPortal: form.isPublishedToExternalPortal,
          externalListingType: form.externalListingType,
          externalListingStatus: form.externalListingStatus,
          externalListingPrice: includesSale
            ? form.externalSalePrice
              ? Number(form.externalSalePrice)
              : null
            : form.externalMonthlyRent
              ? Number(form.externalMonthlyRent)
              : null,
          externalSalePrice:
            includesSale && form.externalSalePrice
              ? Number(form.externalSalePrice)
              : null,
          externalMonthlyRent:
            includesRent && form.externalMonthlyRent
              ? Number(form.externalMonthlyRent)
              : null,
          externalLeaseTermMonths:
            includesRent && form.externalLeaseDuration
              ? Number(form.externalLeaseDuration) *
                (form.externalLeaseDurationUnit === 'years' ? 12 : 1)
              : null,
          externalListingCurrency: form.externalListingCurrency,
          externalListingNotes: form.externalListingNotes,
        }
      );
      setAssets((current) =>
        current.map((asset) => (asset.id === updated.id ? updated : asset))
      );
      toast.success('Portal listing updated.');
    } catch (error: any) {
      toast.error(error?.message || 'Unable to update portal listing.');
    } finally {
      setIsSaving(false);
    }
  };

  const removeListing = async () => {
    if (!selected) return;
    setIsSaving(true);
    try {
      await estateLandManagementService.updateExternalListing(selected.id, {
        isPublishedToExternalPortal: false,
        externalListingType: 'None',
        externalListingStatus: 'Draft',
        externalListingPrice: null,
        externalSalePrice: null,
        externalMonthlyRent: null,
        externalLeaseTermMonths: null,
        externalListingCurrency: form.externalListingCurrency,
        externalListingNotes: null,
      });
      toast.success('Asset removed from Portal Listings.');
      setSelectedId(null);
      await loadAssets(search);
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
      await estateLandManagementService.uploadDocument(
        selected.id,
        file,
        'Listing Image',
        file.name,
        {
          isListingImage: true,
          isPrimaryListingImage: listingImages.length === 0,
        }
      );
      await loadDocuments(selected.id);
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
      await estateLandManagementService.setPrimaryListingImage(
        selected.id,
        documentId
      );
      await loadDocuments(selected.id);
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

      <div className="grid gap-6 lg:grid-cols-[380px_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Portal inventory</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <form
              className="flex gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                void loadAssets(search);
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

            <div className="space-y-2">
              {isLoading ? (
                <div className="flex items-center justify-center gap-2 rounded-md border py-12 text-sm text-muted-foreground">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Loading assets
                </div>
              ) : null}
              {!isLoading &&
                assets.map((asset) => {
                  const active = selected?.id === asset.id;
                  return (
                    <button
                      key={asset.id}
                      type="button"
                      onClick={() => {
                        setRequestedAssetUnavailable(false);
                        setSelectedId(asset.id);
                      }}
                      className={`w-full rounded-md border p-3 text-left ${
                        active
                          ? 'border-primary bg-primary/5'
                          : 'bg-background hover:bg-muted'
                      }`}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate text-sm font-semibold">
                            {asset.name}
                          </p>
                          <p className="mt-1 truncate text-xs text-muted-foreground">
                            {asset.assetCode}
                          </p>
                        </div>
                        {asset.isPublishedToExternalPortal ? (
                          <Badge variant="secondary">Published</Badge>
                        ) : (
                          <Badge variant="outline">Draft</Badge>
                        )}
                      </div>
                      <div className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
                        <MapPin className="h-3.5 w-3.5 shrink-0" />
                        <span className="truncate">
                          {asset.location || 'Location not recorded'}
                        </span>
                      </div>
                    </button>
                  );
                })}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              {selected?.name || 'Select an asset'}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-5">
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
                    <Label>Portal visibility</Label>
                    <Select
                      value={
                        form.isPublishedToExternalPortal ? 'published' : 'draft'
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
                      value={form.externalListingType}
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
                        <SelectItem value="SaleAndRent">
                          Sale and rent
                        </SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Status</Label>
                    <Select
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
                    {includesSale ? (
                      <div className="space-y-2">
                        <Label>Sale price</Label>
                        <Input
                          type="number"
                          min="0"
                          value={form.externalSalePrice}
                          onChange={(event) =>
                            setForm((current) => ({
                              ...current,
                              externalSalePrice: event.target.value,
                            }))
                          }
                          placeholder="Enter sale price"
                        />
                      </div>
                    ) : null}
                    {includesRent ? (
                      <>
                        <div className="space-y-2">
                          <Label>Rent per month</Label>
                          <Input
                            type="number"
                            min="0"
                            value={form.externalMonthlyRent}
                            onChange={(event) =>
                              setForm((current) => ({
                                ...current,
                                externalMonthlyRent: event.target.value,
                              }))
                            }
                            placeholder="Enter monthly rent"
                          />
                        </div>
                        <div className="space-y-2">
                          <Label>Rental duration</Label>
                          <div className="grid grid-cols-[1fr_130px] gap-2">
                            <Input
                              type="number"
                              min="1"
                              value={form.externalLeaseDuration}
                              onChange={(event) =>
                                setForm((current) => ({
                                  ...current,
                                  externalLeaseDuration: event.target.value,
                                }))
                              }
                              placeholder="Duration"
                            />
                            <Select
                              value={form.externalLeaseDurationUnit}
                              onValueChange={(value) =>
                                setForm((current) => ({
                                  ...current,
                                  externalLeaseDurationUnit: value,
                                }))
                              }
                            >
                              <SelectTrigger>
                                <SelectValue />
                              </SelectTrigger>
                              <SelectContent>
                                <SelectItem value="months">Months</SelectItem>
                                <SelectItem value="years">Years</SelectItem>
                              </SelectContent>
                            </Select>
                          </div>
                        </div>
                      </>
                    ) : null}
                  </div>
                </div>

                {includesRent &&
                selected.assetType === EstateManagedAssetType.Land ? (
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
                          )}. This can be shown on the portal, but billing will only start after customer acceptance, signed agreement, and move-in / agreement start date.`
                        : 'Assess and approve annual ground rent through the applicable Estate SOP before publishing this land rental listing. Billing account setup happens later after customer acceptance, signed agreement, and move-in / agreement start date.'}
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
                          <Link href="/estate/EstateLandsPartiallyServiced">
                            Partially serviced land SOP
                          </Link>
                        </Button>
                        <Button
                          asChild
                          type="button"
                          size="sm"
                          variant="outline"
                          className="bg-white"
                        >
                          <Link href="/estate/EstateTenancyRegularisation">
                            Regularisation SOP
                          </Link>
                        </Button>
                      </div>
                    ) : null}
                  </div>
                ) : null}

                <div className="space-y-2">
                  <Label>Listing notes</Label>
                  <Textarea
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
                    disabled={isSaving}
                  >
                    <Trash2 className="mr-2 h-4 w-4" />
                    Remove from Portal Listings
                  </Button>
                  <Button
                    onClick={saveListing}
                    disabled={isSaving || rentPublicationBlockedByGroundRent}
                    title={
                      rentPublicationBlockedByGroundRent
                        ? 'Assess annual ground rent before publishing this land rental listing.'
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
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
