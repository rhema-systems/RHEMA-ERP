'use client';

import React from 'react';
import Link from 'next/link';
import {
  Building2,
  CalendarDays,
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
import {
  externalEstateListingsService,
  type ExternalEstateListing,
} from '@/services/external-estate-listings.service';

const salesPublicEnquiryPath = '/external-portal/property-listings';

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

function isLeaseListingType(value: string) {
  return value === 'Lease' || value === 'SaleAndLease';
}

function listingPriceSummary(listing: ExternalEstateListing) {
  if (listing.externalListingType === 'Rent') {
    return `${formatMoney(listing.externalMonthlyRent, listing.externalListingCurrency)} / month`;
  }
  if (listing.externalListingType === 'Lease') {
    return `${formatMoney(listing.externalMonthlyRent, listing.externalListingCurrency)} / year`;
  }
  if (listing.externalListingType === 'SaleAndRent') {
    return `Sale ${formatMoney(listing.externalSalePrice, listing.externalListingCurrency)} · Rent ${formatMoney(listing.externalMonthlyRent, listing.externalListingCurrency)} / month`;
  }
  if (listing.externalListingType === 'SaleAndLease') {
    return `Sale ${formatMoney(listing.externalSalePrice, listing.externalListingCurrency)} · Lease ${formatMoney(listing.externalMonthlyRent, listing.externalListingCurrency)} / year`;
  }
  return formatMoney(
    listing.externalSalePrice ?? listing.externalListingPrice,
    listing.externalListingCurrency
  );
}

function listingTypeLabel(value: string) {
  if (value === 'SaleAndRent') return 'Sale and rent';
  if (value === 'SaleAndLease') return 'Sale and lease';
  if (value === 'Sale') return 'For sale';
  if (value === 'Rent') return 'For rent';
  if (value === 'Lease') return 'For lease';
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

function buildPublicImageUrl(listing: ExternalEstateListing) {
  if (!listing.primaryImageUrl) return listingFallbackImage(listing);
  const apiBase = process.env.NEXT_PUBLIC_API_URL || '/api';
  return `${apiBase}${listing.primaryImageUrl}`;
}

function buildSalesEnquiryHref(listing: ExternalEstateListing) {
  const params = new URLSearchParams({
    source: 'estate-public-listing',
    listingId: listing.id,
    listingReference: listing.assetCode,
    listingName: listing.name,
    listingType: listing.externalListingType,
    currency: listing.externalListingCurrency || 'GHS',
  });

  return `${salesPublicEnquiryPath}?${params.toString()}`;
}

function ListingImage({ listing }: { listing: ExternalEstateListing }) {
  const [failed, setFailed] = React.useState(false);
  const src = failed ? listingFallbackImage(listing) : buildPublicImageUrl(listing);

  return (
    <div className="relative aspect-[4/3] overflow-hidden bg-slate-100">
      <img
        src={src}
        alt=""
        className="h-full w-full object-cover"
        onError={() => setFailed(true)}
      />
      <div className="absolute right-3 top-3 rounded-md bg-white/90 p-2 text-slate-500 shadow-sm">
        <ImageIcon className="h-4 w-4" />
      </div>
    </div>
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

export default function PublicPropertyListingsPage() {
  const pageSize = 10;
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
  const [error, setError] = React.useState<string | null>(null);

  const selected = React.useMemo(
    () => listings.find((listing) => listing.id === selectedId) || listings[0],
    [listings, selectedId]
  );

  const loadListings = React.useCallback(async (pageNumber = 1) => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await externalEstateListingsService.getPublicListingsPage({
        search,
        location,
        listingType,
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
      setError('Could not load public property listings.');
    } finally {
      setIsLoading(false);
    }
  }, [listingType, location, maxPrice, minPrice, pageSize, search]);

  React.useEffect(() => {
    void loadListings();
  }, [loadListings]);

  return (
    <div className="min-h-screen bg-slate-50">
      <main className="mx-auto max-w-7xl space-y-6 px-4 py-6 sm:px-6 lg:px-8">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">
              Property Listings
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Browse properties available for sale, rent, or lease.
            </p>
          </div>
          <Badge variant="outline" className="w-fit bg-white">
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
                <SelectItem value="Lease">For lease</SelectItem>
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
            <AlertTitle>Listings not available</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        ) : null}

        <div className="grid items-start gap-4 xl:grid-cols-[minmax(0,1fr)_420px]">
          <div className="space-y-4">
            <div className="grid auto-rows-max content-start gap-4 md:grid-cols-2 2xl:grid-cols-3">
              {isLoading ? (
                <div className="col-span-full flex items-center justify-center gap-2 rounded-md border bg-white py-16 text-sm text-slate-500">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Loading listings
                </div>
              ) : null}

              {!isLoading && listings.length === 0 ? (
                <div className="col-span-full rounded-md border border-dashed bg-white py-16 text-center text-sm text-slate-500">
                  No public listings found.
                </div>
              ) : null}

              {listings.map((listing) => {
                const active = selected?.id === listing.id;
                return (
                  <button
                    key={listing.id}
                    type="button"
                    onClick={() => setSelectedId(listing.id)}
                    className={`overflow-hidden rounded-md border bg-white text-left shadow-sm transition ${
                      active ? 'border-blue-600 ring-2 ring-blue-100' : ''
                    }`}
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
                        <span className="text-slate-500">
                          {areaLabel(listing)}
                        </span>
                      </div>
                    </div>
                  </button>
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
                          label={
                            isLeaseListingType(selected.externalListingType)
                              ? 'Lease duration'
                              : 'Rental duration'
                          }
                          value="Duration on request"
                        />
                      ) : null}
                    </div>

                    <div className="rounded-md bg-slate-50 p-4">
                      <div className="text-xs text-slate-500">
                        {selected.externalListingType === 'Sale'
                          ? 'Sale price'
                          : isLeaseListingType(selected.externalListingType)
                            ? 'Lease amount per year'
                            : 'Rent per month'}
                      </div>
                      <div className="mt-1 text-xl font-semibold text-slate-900">
                        {formatMoney(
                          selected.externalListingType === 'Sale'
                            ? (selected.externalSalePrice ??
                                selected.externalListingPrice)
                            : (selected.externalMonthlyRent ??
                              selected.externalListingPrice),
                          selected.externalListingCurrency
                        )}
                      </div>
                      <div className="mt-1 text-sm text-slate-500">
                        {selected.externalListingType !== 'Sale'
                          ? 'Sales will continue the enquiry'
                          : listingTypeLabel(selected.externalListingType)}
                      </div>
                    </div>

                    {selected.externalListingNotes ? (
                      <p className="text-sm leading-6 text-slate-600">
                        {selected.externalListingNotes}
                      </p>
                    ) : null}
                  </div>

                  <Button className="w-full" asChild>
                    <Link href={buildSalesEnquiryHref(selected)}>
                      <Send className="mr-2 h-4 w-4" />
                      Enquiry
                    </Link>
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
      </main>
    </div>
  );
}
