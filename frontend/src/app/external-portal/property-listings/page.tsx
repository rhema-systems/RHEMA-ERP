'use client';

import React from 'react';
import Link from 'next/link';
import {
  Building2,
  CalendarDays,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  FileText,
  Filter,
  Home,
  ImageIcon,
  Loader2,
  MapPin,
  Ruler,
  Search,
  Send,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import {
  externalEstateListingsService,
  type ExternalCustomerProfile,
  type ExternalListingEnquiry,
  type ExternalEstateListing,
} from '@/services/external-estate-listings.service';

function formatMoney(value?: number | null, currency = 'GHS') {
  if (value == null) return 'Price on request';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
    maximumFractionDigits: 0,
  }).format(value);
}

function formatLeaseTerm(months?: number | null) {
  if (!months) return 'Duration on request';
  if (months % 12 === 0) {
    const years = months / 12;
    return `${years} year${years === 1 ? '' : 's'}`;
  }
  return `${months} month${months === 1 ? '' : 's'}`;
}

function listingPriceSummary(listing: ExternalEstateListing) {
  if (listing.externalListingType === 'Rent') {
    if (isLandListing(listing)) {
      return `${formatMoney(listing.groundRentPayable, listing.externalListingCurrency)} annual ground rent`;
    }
    return `${formatMoney(listing.externalMonthlyRent, listing.externalListingCurrency)} / month`;
  }
  if (listing.externalListingType === 'SaleAndRent') {
    return `Sale ${formatMoney(listing.externalSalePrice, listing.externalListingCurrency)} · Rent ${formatMoney(listing.externalMonthlyRent, listing.externalListingCurrency)} / month`;
  }
  return formatMoney(
    listing.externalSalePrice ?? listing.externalListingPrice,
    listing.externalListingCurrency
  );
}

function listingTypeLabel(value: string) {
  if (value === 'SaleAndRent') return 'Sale and rent';
  if (value === 'Sale') return 'For sale';
  if (value === 'Rent') return 'For rent';
  return value;
}

function areaLabel(listing: ExternalEstateListing) {
  if (listing.areaSquareMeters) {
    return `${listing.areaSquareMeters.toLocaleString(undefined, {
      maximumFractionDigits: 2,
    })} sqm`;
  }

  if (listing.areaValue && listing.areaUnit) {
    return `${listing.areaValue.toLocaleString()} ${listing.areaUnit}`;
  }

  return 'Area not recorded';
}

function locationLabel(listing: ExternalEstateListing) {
  return (
    listing.location ||
    [listing.town, listing.district, listing.region]
      .filter(Boolean)
      .join(', ') ||
    'Location not recorded'
  );
}

function formatPublishedDate(value?: string | null) {
  if (!value) return 'Publication date not recorded';
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

function listingFallbackImage(listing: ExternalEstateListing) {
  const assetType = String(listing.assetType).toLowerCase();
  const unitType = String(listing.unitType ?? '').toLowerCase();
  if (assetType === '0' || assetType === 'land' || unitType.includes('land')) {
    return '/images/estate/listing-land-fallback.png';
  }

  return '/images/estate/listing-apartment-fallback.png';
}

function parsePriceFilter(value: string) {
  const parsed = Number(value.trim());
  return value.trim() && Number.isFinite(parsed) && parsed >= 0
    ? parsed
    : undefined;
}

function isLandListing(listing?: ExternalEstateListing | null) {
  if (!listing) return false;
  const assetType = String(listing.assetType).toLowerCase();
  return assetType === '0' || assetType === 'land';
}

function ListingImage({ listing }: { listing: ExternalEstateListing }) {
  const [imageUrl, setImageUrl] = React.useState<string | null>(null);

  React.useEffect(() => {
    let active = true;
    let objectUrl: string | null = null;

    const load = async () => {
      try {
        const blob =
          await externalEstateListingsService.getListingImage(listing);
        if (!blob || !active) return;
        objectUrl = URL.createObjectURL(blob);
        setImageUrl(objectUrl);
      } catch {
        setImageUrl(null);
      }
    };

    void load();

    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [listing]);

  if (!imageUrl) {
    return (
      <div className="relative aspect-[4/3] overflow-hidden bg-slate-100">
        <img
          src={listingFallbackImage(listing)}
          alt=""
          className="h-full w-full object-cover"
        />
        <div className="absolute right-3 top-3 rounded-md bg-white/90 p-2 text-slate-500 shadow-sm">
          <ImageIcon className="h-4 w-4" />
        </div>
      </div>
    );
  }

  return (
    <img
      src={imageUrl}
      alt={listing.name}
      className="aspect-[4/3] w-full object-cover"
    />
  );
}

function ListingStat({
  icon: Icon,
  label,
  value,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-start gap-3 rounded-md border bg-white p-3">
      <Icon className="mt-0.5 h-4 w-4 shrink-0 text-blue-700" />
      <div className="min-w-0">
        <div className="text-xs text-slate-500">{label}</div>
        <div className="truncate text-sm font-medium text-slate-900">
          {value}
        </div>
      </div>
    </div>
  );
}

export default function ExternalPropertyListingsPage() {
  const pageSize = 10;
  const { toast } = useToast();
  const [customerProfiles, setCustomerProfiles] = React.useState<
    ExternalCustomerProfile[]
  >([]);
  const [selectedCustomerId, setSelectedCustomerId] = React.useState('');
  const [areProfilesLoading, setAreProfilesLoading] = React.useState(true);
  const [listings, setListings] = React.useState<ExternalEstateListing[]>([]);
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [search, setSearch] = React.useState('');
  const [location, setLocation] = React.useState('');
  const [listingType, setListingType] = React.useState('all');
  const [minPrice, setMinPrice] = React.useState('');
  const [maxPrice, setMaxPrice] = React.useState('');
  const [page, setPage] = React.useState(1);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);
  const [hasPreviousPage, setHasPreviousPage] = React.useState(false);
  const [hasNextPage, setHasNextPage] = React.useState(false);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [submittingListingId, setSubmittingListingId] = React.useState<string | null>(null);
  const [createdRequest, setCreatedRequest] =
    React.useState<ExternalListingEnquiry | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const selectedCustomer = React.useMemo(
    () =>
      customerProfiles.find((profile) => profile.id === selectedCustomerId) ||
      null,
    [customerProfiles, selectedCustomerId]
  );
  const selected = React.useMemo(
    () => listings.find((listing) => listing.id === selectedId) || listings[0],
    [listings, selectedId]
  );

  const loadListings = React.useCallback(async (pageNumber = 1) => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await externalEstateListingsService.getListingsPage({
        search,
        location,
        listingType,
        businessPartnerId: selectedCustomerId || undefined,
        minPrice: parsePriceFilter(minPrice),
        maxPrice: parsePriceFilter(maxPrice),
        page: pageNumber,
        pageSize,
      });
      setListings(result.items);
      setPage(result.page);
      setTotalCount(result.totalCount);
      setTotalPages(result.totalPages);
      setHasPreviousPage(result.hasPreviousPage);
      setHasNextPage(result.hasNextPage);
      setSelectedId((current) =>
        current && result.items.some((listing) => listing.id === current)
          ? current
          : (result.items[0]?.id ?? null)
      );
    } catch {
      setError('Could not load property listings.');
    } finally {
      setIsLoading(false);
    }
  }, [listingType, location, maxPrice, minPrice, pageSize, search, selectedCustomerId]);

  React.useEffect(() => {
    let mounted = true;

    const loadProfiles = async () => {
      try {
        const profiles =
          await externalEstateListingsService.getCustomerProfiles();
        if (!mounted) return;
        setCustomerProfiles(profiles);
        setSelectedCustomerId((current) => current || profiles[0]?.id || '');
      } catch {
        if (mounted) {
          setError('Could not load the Business Partners linked to your account.');
        }
      } finally {
        if (mounted) setAreProfilesLoading(false);
      }
    };

    void loadProfiles();
    return () => {
      mounted = false;
    };
  }, []);

  React.useEffect(() => {
    void loadListings();
  }, [loadListings]);

  const submitEnquiry = async (listing: ExternalEstateListing) => {
    setSelectedId(listing.id);
    if (!selectedCustomer) {
      setError('Select the Business Partner placing this enquiry.');
      return;
    }

    setIsSubmitting(true);
    setSubmittingListingId(listing.id);
    setCreatedRequest(null);
    setError(null);
    try {
      const created = await externalEstateListingsService.createEnquiry(
        listing.id,
        {
          businessPartnerId: selectedCustomer.id,
        }
      );
      setCreatedRequest(created);
      toast({
        title: 'Enquiry sent',
        description: `${created.referenceNumber || created.title} is now with Sales.`,
        variant: 'success',
      });
      await loadListings();
    } catch (submitError) {
      setError(
        submitError instanceof Error
          ? submitError.message
          : 'Could not submit enquiry for this listing.'
      );
    } finally {
      setIsSubmitting(false);
      setSubmittingListingId(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">
            Property Listings
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            Browse available estate units and send purchase, rent, or lease
            enquiries to Sales.
          </p>
        </div>
        <Badge variant="outline" className="w-fit">
          {isLoading ? (
            <>
              <Loader2 className="mr-2 h-3 w-3 animate-spin" />
              Loading
            </>
          ) : (
            `${totalCount} available`
          )}
        </Badge>
      </div>

      <Card>
        <CardContent className="grid gap-3 p-4 md:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_180px_160px_160px_auto]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <Input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search apartment, unit, land"
              className="pl-9"
            />
          </div>
          <div className="relative">
            <MapPin className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <Input
              value={location}
              onChange={(event) => setLocation(event.target.value)}
              placeholder="Location"
              className="pl-9"
            />
          </div>
          <Select value={listingType} onValueChange={setListingType}>
            <SelectTrigger>
              <Filter className="mr-2 h-4 w-4 text-slate-500" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All listings</SelectItem>
              <SelectItem value="Sale">For sale</SelectItem>
              <SelectItem value="Rent">For rent</SelectItem>
            </SelectContent>
            </Select>
          <Input
            type="number"
            min="0"
            step="0.01"
            value={minPrice}
            onChange={(event) => setMinPrice(event.target.value)}
            placeholder="Minimum price"
            aria-label="Minimum price"
          />
          <Input
            type="number"
            min="0"
            step="0.01"
            value={maxPrice}
            onChange={(event) => setMaxPrice(event.target.value)}
            placeholder="Maximum price"
            aria-label="Maximum price"
          />
          <Button onClick={() => void loadListings(1)}>
            <Search className="mr-2 h-4 w-4" />
            Search
          </Button>
        </CardContent>
      </Card>

      {error ? (
        <Alert variant="destructive">
          <AlertTitle>Enquiry not completed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      {createdRequest ? (
        <Alert className="border-green-200 bg-green-50 text-green-800">
          <CheckCircle2 className="h-4 w-4 text-green-700" />
          <AlertTitle>Enquiry sent to Sales</AlertTitle>
          <AlertDescription>
            {createdRequest.referenceNumber || createdRequest.title} is now in{' '}
            {createdRequest.currentStageName}. Sales will complete their process
            before handing the transaction back to Estate.
          </AlertDescription>
        </Alert>
      ) : null}

      {areProfilesLoading ? (
        <Alert>
          <Loader2 className="h-4 w-4 animate-spin" />
          <AlertTitle>Loading customer account</AlertTitle>
          <AlertDescription>
            We are checking the Business Partner account linked to your portal login.
          </AlertDescription>
        </Alert>
      ) : customerProfiles.length === 0 ? (
        <Alert>
          <FileText className="h-4 w-4" />
          <AlertTitle>Business Partner required</AlertTitle>
          <AlertDescription className="space-y-3">
            <p>
              Register and obtain approval for a Customer Business Partner before
              sending a property enquiry.
            </p>
            <Button asChild size="sm" variant="outline">
              <Link href="/external-portal/business-partner">
                Open Business Partner Registration
              </Link>
            </Button>
          </AlertDescription>
        </Alert>
      ) : null}

      <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_420px]">
        <div className="space-y-4">
          <div className="grid auto-rows-max content-start gap-4 md:grid-cols-2 2xl:grid-cols-3">
            {isLoading ? (
              <div className="col-span-full flex items-center justify-center gap-2 rounded-md border py-16 text-sm text-slate-500">
                <Loader2 className="h-4 w-4 animate-spin" />
                Loading listings
              </div>
            ) : null}

            {!isLoading && listings.length === 0 ? (
              <div className="col-span-full rounded-md border border-dashed py-16 text-center text-sm text-slate-500">
                No listings found.
              </div>
            ) : null}

            {listings.map((listing) => {
              const active = selectedId === listing.id;
              return (
                <div
                  key={listing.id}
                  className={`overflow-hidden rounded-md border bg-white text-left shadow-sm transition ${
                    active ? 'border-blue-600 ring-2 ring-blue-100' : ''
                  }`}
                >
                  <button
                    type="button"
                    onClick={() => setSelectedId(listing.id)}
                    className="block w-full text-left"
                  >
                    <ListingImage listing={listing} />
                    <div className="space-y-3 p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <div className="truncate font-semibold text-slate-900">
                            {listing.name}
                          </div>
                          <div className="mt-1 truncate text-xs text-slate-500">
                            {listing.assetCode}
                          </div>
                        </div>
                        <Badge variant="secondary">
                          {listingTypeLabel(listing.externalListingType)}
                        </Badge>
                      </div>
                      <div className="flex items-center gap-2 text-sm text-slate-600">
                        <MapPin className="h-4 w-4 shrink-0" />
                        <span className="truncate">{locationLabel(listing)}</span>
                      </div>
                      <div className="flex items-center justify-between gap-3 text-sm">
                        <span className="font-semibold text-slate-900">
                          {listingPriceSummary(listing)}
                        </span>
                        <span className="text-slate-500">{areaLabel(listing)}</span>
                      </div>
                    </div>
                  </button>
                </div>
              );
            })}
          </div>

          {!isLoading && totalCount > 0 ? (
            <div className="flex items-center justify-between rounded-md border bg-white px-3 py-2 text-sm text-slate-600">
              <span>
                Page {page} of {totalPages}
              </span>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!hasPreviousPage}
                  onClick={() => void loadListings(page - 1)}
                >
                  <ChevronLeft className="mr-1 h-4 w-4" />
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={!hasNextPage}
                  onClick={() => void loadListings(page + 1)}
                >
                  Next
                  <ChevronRight className="ml-1 h-4 w-4" />
                </Button>
              </div>
            </div>
          ) : null}
        </div>

        <Card className="h-fit">
          <CardHeader>
            <CardTitle className="text-base">Property values</CardTitle>
          </CardHeader>
          <CardContent className="space-y-5">
            {selected ? (
              <>
                <div className="space-y-4">
                  <div className="flex items-start gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border bg-slate-50">
                      {selected.externalListingType === 'Sale' ? (
                        <Building2 className="h-5 w-5 text-blue-700" />
                      ) : (
                        <Home className="h-5 w-5 text-blue-700" />
                      )}
                    </div>
                    <div className="min-w-0">
                      <h2 className="truncate font-semibold text-slate-900">
                        {selected.name}
                      </h2>
                      <p className="mt-1 text-sm text-slate-500">
                        {selected.sourceLabel}
                      </p>
                    </div>
                  </div>

                  <div className="grid gap-2">
                    <ListingStat
                      icon={MapPin}
                      label="Location"
                      value={locationLabel(selected)}
                    />
                    <ListingStat
                      icon={Ruler}
                      label="Area"
                      value={areaLabel(selected)}
                    />
                    <ListingStat
                      icon={CalendarDays}
                      label="Published"
                      value={formatPublishedDate(selected.externalPublishedAt)}
                    />
                    {selected.externalListingType !== 'Sale' ? (
                      <ListingStat
                        icon={CalendarDays}
                        label="Rental duration"
                        value={formatLeaseTerm(
                          selected.externalLeaseTermMonths
                        )}
                      />
                    ) : null}
                    {selected.externalListingType !== 'Sale' &&
                    isLandListing(selected) ? (
                      <ListingStat
                        icon={FileText}
                        label="Annual ground rent"
                        value={formatMoney(
                          selected.groundRentPayable,
                          selected.externalListingCurrency
                        )}
                      />
                    ) : null}
                  </div>

                  <div className="rounded-md bg-slate-50 p-4">
                    <div className="text-xs text-slate-500">
                      {selected.externalListingType === 'Sale'
                        ? 'Sale price'
                        : isLandListing(selected)
                          ? 'Annual ground rent'
                          : 'Rent per month'}
                    </div>
                    <div className="mt-1 text-xl font-semibold text-slate-900">
                      {formatMoney(
                        selected.externalListingType === 'Sale'
                          ? (selected.externalSalePrice ??
                              selected.externalListingPrice)
                          : isLandListing(selected)
                            ? selected.groundRentPayable
                            : (selected.externalMonthlyRent ??
                              selected.externalListingPrice),
                        selected.externalListingCurrency
                      )}
                    </div>
                    <div className="mt-1 text-sm text-slate-500">
                      {selected.externalListingType !== 'Sale'
                        ? `${formatLeaseTerm(
                            selected.externalLeaseTermMonths
                          )} · Sales will continue the enquiry`
                        : listingTypeLabel(selected.externalListingType)}
                    </div>
                  </div>

                  {selected.externalListingNotes ? (
                    <p className="text-sm leading-6 text-slate-600">
                      {selected.externalListingNotes}
                    </p>
                  ) : null}
                </div>

                <Button
                  className="w-full"
                  disabled={
                    isSubmitting ||
                    areProfilesLoading ||
                    customerProfiles.length === 0
                  }
                  onClick={() => void submitEnquiry(selected)}
                >
                  {submittingListingId === selected.id ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Send className="mr-2 h-4 w-4" />
                  )}
                  Enquiry
                </Button>
              </>
            ) : (
              <div className="rounded-md border border-dashed p-8 text-center text-sm text-slate-500">
                <FileText className="mx-auto mb-3 h-8 w-8 text-slate-400" />
                Select a listing to view property values.
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
