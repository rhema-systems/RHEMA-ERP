'use client';

import React from 'react';
import Link from 'next/link';
import { PropertyEnquiryDialog } from '@/components/estate/PropertyEnquiryDialog';
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
  ZoomIn,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
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
    return `${formatMoney(listing.externalMonthlyRent, listing.externalListingCurrency)} / month`;
  }
  if (listing.externalListingType === 'Lease') {
    return `${formatMoney(listing.externalListingPrice ?? listing.externalMonthlyRent, listing.externalListingCurrency)} full term`;
  }
  if (listing.externalListingType === 'SaleAndRent') {
    return `Sale ${formatMoney(listing.externalSalePrice, listing.externalListingCurrency)} · Rent ${formatMoney(listing.externalMonthlyRent, listing.externalListingCurrency)} / month`;
  }
  if (listing.externalListingType === 'SaleAndLease') {
    return `Sale ${formatMoney(listing.externalSalePrice, listing.externalListingCurrency)} · Lease ${formatMoney(listing.externalListingPrice ?? listing.externalMonthlyRent, listing.externalListingCurrency)} full term`;
  }
  return formatMoney(
    listing.externalSalePrice ?? listing.externalListingPrice,
    listing.externalListingCurrency
  );
}

function isLeaseListingType(value: string) {
  return value === 'Lease' || value === 'SaleAndLease';
}

function listingTypeLabel(value: string) {
  if (value === 'SaleAndRent') return 'Sale and rent';
  if (value === 'SaleAndLease') return 'Sale and lease';
  if (value === 'Sale') return 'For sale';
  if (value === 'Rent') return 'For rent';
  if (value === 'Lease') return 'For lease';
  return value;
}

function listingTypeBadgeClass(value: string) {
  if (value === 'Sale') return 'border-emerald-200 bg-emerald-100 text-emerald-900 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-200';
  if (value === 'Rent') return 'border-blue-200 bg-blue-100 text-blue-900 dark:border-blue-800 dark:bg-blue-950 dark:text-blue-200';
  if (value === 'Lease') return 'border-amber-200 bg-amber-100 text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200';
  if (value === 'SaleAndRent') return 'border-cyan-200 bg-cyan-100 text-cyan-900 dark:border-cyan-800 dark:bg-cyan-950 dark:text-cyan-200';
  if (value === 'SaleAndLease') return 'border-violet-200 bg-violet-100 text-violet-900 dark:border-violet-800 dark:bg-violet-950 dark:text-violet-200';
  return 'border-border bg-muted text-foreground';
}

function areaLabel(listing: ExternalEstateListing) {
  const plotSuffix =
    listing.plotEquivalentCount != null && listing.squareMetersPerPlot != null
      ? ` (${listing.plotEquivalentCount.toLocaleString(undefined, {
          maximumFractionDigits: 2,
        })} plot${listing.plotEquivalentCount === 1 ? '' : 's'})`
      : '';

  if (listing.areaSquareMeters) {
    return `${listing.areaSquareMeters.toLocaleString(undefined, {
      maximumFractionDigits: 2,
    })} sqm${plotSuffix}`;
  }

  if (listing.areaValue && listing.areaUnit) {
    return `${listing.areaValue.toLocaleString()} ${listing.areaUnit}${plotSuffix}`;
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

function ListingImage({ listing, onSelect, onEnquiry }: {
  listing: ExternalEstateListing;
  onSelect: () => void;
  onEnquiry: () => void;
}) {
  const [imageUrl, setImageUrl] = React.useState<string | null>(null);
  const [previewOpen, setPreviewOpen] = React.useState(false);

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

  const previewUrl = imageUrl || listingFallbackImage(listing);

  return (
    <>
      <button
        type="button"
        className="relative block w-full overflow-hidden bg-slate-100"
        aria-label={`Preview image for ${listing.name}`}
        onClick={() => { onSelect(); setPreviewOpen(true); }}
      >
        <img
          src={previewUrl}
          alt={listing.name}
          className="aspect-[4/3] w-full object-cover"
        />
        <span className="absolute bottom-3 right-3 rounded-md bg-white/95 p-2 text-slate-900 shadow-sm">
          {imageUrl ? <ZoomIn className="h-4 w-4" /> : <ImageIcon className="h-4 w-4" />}
        </span>
      </button>
      <Dialog open={previewOpen} onOpenChange={setPreviewOpen}>
        <DialogContent className="w-[min(96vw,1100px)] max-w-none bg-background p-4 text-foreground">
          <DialogHeader className="pr-8">
            <DialogTitle>{listing.name}</DialogTitle>
            <DialogDescription>{locationLabel(listing)}</DialogDescription>
          </DialogHeader>
          <img
            src={previewUrl}
            alt={listing.name}
            className="max-h-[70dvh] w-full object-contain"
          />
          <div className="flex justify-end">
            <Button onClick={() => { setPreviewOpen(false); onEnquiry(); }}>
              <Send className="mr-2 h-4 w-4" />
              Enquiry
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
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
  const [enquiryListing, setEnquiryListing] = React.useState<ExternalEstateListing | null>(null);
  const [createdRequest, setCreatedRequest] =
    React.useState<ExternalListingEnquiry | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const openedLinkedListing = React.useRef(false);

  React.useEffect(() => {
    const listingId = new URLSearchParams(window.location.search).get('listingId');
    if (!listingId || openedLinkedListing.current) return;
    openedLinkedListing.current = true;
    void externalEstateListingsService.getListingsPage({ listingId }).then(result => {
      const listing = result.items.find(item => item.id === listingId);
      if (listing) { setSelectedId(listing.id); setEnquiryListing(listing); }
      else setError('This property is no longer available for enquiry.');
    }).catch(() => setError('Could not load the selected property. Please try again.'));
  }, []);

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
  }, [listingType, location, maxPrice, minPrice, pageSize, search]);

  React.useEffect(() => {
    void loadListings();
  }, [loadListings]);

  const enquiryCreated = (ticket: ExternalListingEnquiry) => {
    setCreatedRequest(ticket); setEnquiryListing(null);
    toast({ title: 'Enquiry sent', description: `${ticket.ticketNumber} is with Sales and Marketing.`, variant: 'success' });
  };

  return (
    <div className="space-y-6">
      {enquiryListing && <PropertyEnquiryDialog key={enquiryListing.id} listing={enquiryListing} onClose={() => setEnquiryListing(null)} onCreated={enquiryCreated} />}
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
          <AlertTitle>Enquiry not completed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      {createdRequest ? (
        <Alert className="border-green-200 bg-green-50 text-green-800">
          <CheckCircle2 className="h-4 w-4 text-green-700" />
          <AlertTitle>Enquiry sent to Sales</AlertTitle>
          <AlertDescription>
            {createdRequest.ticketNumber} is with Sales and Marketing.{' '}
            <Link className="underline font-medium" href={`/external-portal/support/tickets/${createdRequest.id}`}>View your enquiry and replies</Link>
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
                  <ListingImage
                    listing={listing}
                    onSelect={() => setSelectedId(listing.id)}
                    onEnquiry={() => setEnquiryListing(listing)}
                  />
                  <button
                    type="button"
                    onClick={() => setSelectedId(listing.id)}
                    className="block w-full text-left"
                  >
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
                        <Badge variant="outline" className={listingTypeBadgeClass(listing.externalListingType)}>
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
                  {active ? (
                    <div className="px-4 pb-4 xl:hidden">
                      <Button
                        className="w-full"
                        size="sm"
                        onClick={() => setEnquiryListing(listing)}
                      >
                        <Send className="mr-2 h-4 w-4" />
                        Enquiry
                      </Button>
                    </div>
                  ) : null}
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

        <Card className="h-fit xl:sticky xl:top-4 xl:flex xl:max-h-[calc(100dvh-2rem)] xl:flex-col">
          <CardHeader className="shrink-0 space-y-3">
            <CardTitle className="text-base">Property values</CardTitle>
            {selected ? (
              <Button className="w-full" onClick={() => setEnquiryListing(selected)}>
                <Send className="mr-2 h-4 w-4" />
                Enquiry
              </Button>
            ) : null}
          </CardHeader>
          <CardContent className="space-y-5 xl:min-h-0 xl:overflow-y-auto">
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
                      <h2 className="truncate font-semibold text-foreground">
                        {selected.name}
                      </h2>
                      <p className="mt-1 text-sm text-muted-foreground">
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
                          ? 'Full-term lease amount'
                          : 'Rent per month'}
                    </div>
                    <div className="mt-1 text-xl font-semibold text-slate-900">
                      {formatMoney(
                        selected.externalListingType === 'Sale'
                          ? (selected.externalSalePrice ??
                              selected.externalListingPrice)
                          : isLeaseListingType(selected.externalListingType)
                            ? (selected.externalListingPrice ?? selected.externalMonthlyRent)
                            : (selected.externalMonthlyRent ?? selected.externalListingPrice),
                        selected.externalListingCurrency
                      )}
                    </div>
                    <div className="mt-1 text-sm text-slate-500">
                      {selected.externalListingType !== 'Sale'
                        ? 'Sales will continue the enquiry'
                        : listingTypeLabel(selected.externalListingType)}
                    </div>
                    {selected.externalGroundRentRequired && selected.groundRentPayable != null ? (
                      <div className="mt-2 border-t border-slate-200 pt-2 text-sm text-slate-600">
                        Annual ground rent: {formatMoney(selected.groundRentPayable, selected.externalListingCurrency)} separately
                      </div>
                    ) : null}
                    {selected.externalPremiumChargeRequired ? (
                      <div className="mt-1 text-sm text-slate-600">
                        Premium charge:{' '}
                        {formatMoney(
                          selected.externalPremiumChargeAmount,
                          selected.externalListingCurrency
                        )}
                      </div>
                    ) : null}
                  </div>

                  {selected.externalListingNotes ? (
                    <p className="text-sm leading-6 text-foreground">
                      {selected.externalListingNotes}
                    </p>
                  ) : null}
                </div>

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
