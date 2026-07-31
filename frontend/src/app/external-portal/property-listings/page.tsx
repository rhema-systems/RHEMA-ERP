'use client';

import React from 'react';
import {
  Building2,
  CalendarDays,
  CheckCircle2,
  FileText,
  Filter,
  Home,
  ImageIcon,
  Loader2,
  MapPin,
  Phone,
  Ruler,
  Search,
  Send,
  UserRound,
} from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
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
  externalEstateListingsService,
  type ExternalEstateListing,
  type ExternalListingRequest,
} from '@/services/external-estate-listings.service';

type ListingIntent = 'Sale' | 'Rent';

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

function availableIntents(
  listing?: ExternalEstateListing | null
): ListingIntent[] {
  if (!listing) return [];
  if (listing.externalListingType === 'SaleAndRent') return ['Rent', 'Sale'];
  return listing.externalListingType === 'Sale' ? ['Sale'] : ['Rent'];
}

function defaultIntent(listing?: ExternalEstateListing | null): ListingIntent {
  return listing?.externalListingType === 'Sale' ? 'Sale' : 'Rent';
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
  const [listings, setListings] = React.useState<ExternalEstateListing[]>([]);
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [search, setSearch] = React.useState('');
  const [location, setLocation] = React.useState('');
  const [listingType, setListingType] = React.useState('all');
  const [requestIntent, setRequestIntent] =
    React.useState<ListingIntent>('Rent');
  const [applicantName, setApplicantName] = React.useState('');
  const [contact, setContact] = React.useState('');
  const [offerAmount, setOfferAmount] = React.useState('');
  const [message, setMessage] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [createdRequest, setCreatedRequest] =
    React.useState<ExternalListingRequest | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const selected = React.useMemo(
    () => listings.find((listing) => listing.id === selectedId) || listings[0],
    [listings, selectedId]
  );

  const requestOptions = React.useMemo(
    () => availableIntents(selected),
    [selected]
  );

  const loadListings = React.useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await externalEstateListingsService.getListings({
        search,
        location,
        listingType,
        take: 120,
      });
      setListings(data);
      setSelectedId((current) =>
        current && data.some((listing) => listing.id === current)
          ? current
          : (data[0]?.id ?? null)
      );
    } catch {
      setError('Could not load property listings.');
    } finally {
      setIsLoading(false);
    }
  }, [listingType, location, search]);

  React.useEffect(() => {
    void loadListings();
  }, [loadListings]);

  React.useEffect(() => {
    setRequestIntent(defaultIntent(selected));
    setOfferAmount('');
    setCreatedRequest(null);
  }, [selected?.id, selected?.externalListingType]);

  const submitRequest = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selected) return;

    if (!applicantName.trim() || !contact.trim()) {
      setError('Enter your name and contact before submitting the request.');
      return;
    }
    if (
      requestIntent === 'Sale' &&
      (!offerAmount || Number(offerAmount) <= 0)
    ) {
      setError('Enter a positive bid amount before submitting your bid.');
      return;
    }

    setIsSubmitting(true);
    setCreatedRequest(null);
    setError(null);
    try {
      const created = await externalEstateListingsService.createRequest(
        selected.id,
        {
          requestType: requestIntent,
          applicantName: applicantName.trim(),
          contact: contact.trim(),
          offerAmount:
            requestIntent === 'Sale' ? Number(offerAmount) : undefined,
          message: message.trim(),
        }
      );
      setCreatedRequest(created);
      setMessage('');
    } catch {
      setError('Could not submit request for this listing.');
    } finally {
      setIsSubmitting(false);
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
            Browse available estate units and submit a purchase bid or rental
            request.
          </p>
        </div>
        <Badge variant="outline" className="w-fit">
          {isLoading ? (
            <>
              <Loader2 className="mr-2 h-3 w-3 animate-spin" />
              Loading
            </>
          ) : (
            `${listings.length} available`
          )}
        </Badge>
      </div>

      <Card>
        <CardContent className="grid gap-3 p-4 md:grid-cols-[1fr_1fr_180px_auto]">
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
          <Button onClick={() => void loadListings()}>
            <Search className="mr-2 h-4 w-4" />
            Search
          </Button>
        </CardContent>
      </Card>

      {error ? (
        <Alert variant="destructive">
          <AlertTitle>Request not completed</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      ) : null}

      {createdRequest ? (
        <Alert className="border-green-200 bg-green-50 text-green-800">
          <CheckCircle2 className="h-4 w-4 text-green-700" />
          <AlertTitle>Request submitted</AlertTitle>
          <AlertDescription>
            {createdRequest.referenceNumber || createdRequest.title} is now in{' '}
            {createdRequest.currentStageName}.
          </AlertDescription>
        </Alert>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_420px]">
        <div className="grid gap-4 md:grid-cols-2 2xl:grid-cols-3">
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
                    <span className="text-slate-500">{areaLabel(listing)}</span>
                  </div>
                </div>
              </button>
            );
          })}
        </div>

        <Card className="h-fit">
          <CardHeader>
            <CardTitle className="text-base">Customer bid / request</CardTitle>
          </CardHeader>
          <CardContent className="space-y-5">
            {selected ? (
              <>
                <div className="space-y-4">
                  <div className="flex items-start gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border bg-slate-50">
                      {requestIntent === 'Sale' ? (
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
                  </div>

                  <div className="rounded-md bg-slate-50 p-4">
                    <div className="text-xs text-slate-500">
                      {requestIntent === 'Sale'
                        ? 'Sale price'
                        : 'Rent per month'}
                    </div>
                    <div className="mt-1 text-xl font-semibold text-slate-900">
                      {formatMoney(
                        requestIntent === 'Sale'
                          ? (selected.externalSalePrice ??
                              selected.externalListingPrice)
                          : (selected.externalMonthlyRent ??
                              selected.externalListingPrice),
                        selected.externalListingCurrency
                      )}
                    </div>
                    <div className="mt-1 text-sm text-slate-500">
                      {requestIntent === 'Rent'
                        ? formatLeaseTerm(selected.externalLeaseTermMonths)
                        : listingTypeLabel(selected.externalListingType)}
                    </div>
                  </div>

                  {selected.externalListingNotes ? (
                    <p className="text-sm leading-6 text-slate-600">
                      {selected.externalListingNotes}
                    </p>
                  ) : null}
                </div>

                <form className="space-y-4" onSubmit={submitRequest}>
                  <div className="space-y-2">
                    <Label>Request type</Label>
                    <Select
                      value={requestIntent}
                      onValueChange={(value) =>
                        setRequestIntent(value as ListingIntent)
                      }
                    >
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {requestOptions.map((option) => (
                          <SelectItem key={option} value={option}>
                            {option === 'Sale'
                              ? 'Buy this property'
                              : 'Rent this property'}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Name</Label>
                    <div className="relative">
                      <UserRound className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                      <Input
                        value={applicantName}
                        onChange={(event) =>
                          setApplicantName(event.target.value)
                        }
                        className="pl-9"
                        required
                      />
                    </div>
                  </div>
                  <div className="space-y-2">
                    <Label>Phone or email</Label>
                    <div className="relative">
                      <Phone className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                      <Input
                        value={contact}
                        onChange={(event) => setContact(event.target.value)}
                        className="pl-9"
                        required
                      />
                    </div>
                  </div>
                  {requestIntent === 'Sale' ? (
                    <div className="space-y-2">
                      <Label>
                        Bid amount ({selected.externalListingCurrency || 'GHS'})
                      </Label>
                      <Input
                        type="number"
                        min="0.01"
                        step="0.01"
                        value={offerAmount}
                        onChange={(event) => setOfferAmount(event.target.value)}
                        placeholder="Enter your purchase offer"
                        required
                      />
                    </div>
                  ) : null}
                  <div className="space-y-2">
                    <Label>Message</Label>
                    <Textarea
                      className="min-h-[120px]"
                      value={message}
                      onChange={(event) => setMessage(event.target.value)}
                      placeholder="Preferred viewing time, financing, lease period, or other notes."
                    />
                  </div>
                  <Button className="w-full" disabled={isSubmitting}>
                    {isSubmitting ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Send className="mr-2 h-4 w-4" />
                    )}
                    {requestIntent === 'Sale' ? 'Submit bid' : 'Submit request'}
                  </Button>
                </form>
              </>
            ) : (
              <div className="rounded-md border border-dashed p-8 text-center text-sm text-slate-500">
                <FileText className="mx-auto mb-3 h-8 w-8 text-slate-400" />
                Select a listing to submit a request.
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
