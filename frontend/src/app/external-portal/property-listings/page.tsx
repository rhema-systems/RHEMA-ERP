'use client';

import React from 'react';
import {
  Building2,
  Home,
  ImageIcon,
  Loader2,
  MapPin,
  Search,
  Send,
} from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
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
} from '@/services/external-estate-listings.service';

function formatMoney(value?: number | null, currency = 'GHS') {
  if (value == null) return 'Price on request';
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency,
  }).format(value);
}

function listingTypeLabel(value: string) {
  if (value === 'SaleAndRent') return 'Sale and rent';
  return value;
}

function areaLabel(listing: ExternalEstateListing) {
  if (listing.areaSquareMeters)
    return `${listing.areaSquareMeters.toLocaleString(undefined, {
      maximumFractionDigits: 2,
    })} sqm`;
  if (listing.areaValue && listing.areaUnit)
    return `${listing.areaValue.toLocaleString()} ${listing.areaUnit}`;
  return 'Area not recorded';
}

function ListingImage({ listing }: { listing: ExternalEstateListing }) {
  const [imageUrl, setImageUrl] = React.useState<string | null>(null);

  React.useEffect(() => {
    let active = true;
    let objectUrl: string | null = null;

    const load = async () => {
      try {
        const blob = await externalEstateListingsService.getListingImage(
          listing
        );
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
      <div className="flex aspect-[4/3] items-center justify-center bg-slate-100 text-slate-400">
        <ImageIcon className="h-10 w-10" />
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

export default function ExternalPropertyListingsPage() {
  const [listings, setListings] = React.useState<ExternalEstateListing[]>([]);
  const [selectedId, setSelectedId] = React.useState<string | null>(null);
  const [search, setSearch] = React.useState('');
  const [location, setLocation] = React.useState('');
  const [listingType, setListingType] = React.useState('all');
  const [applicantName, setApplicantName] = React.useState('');
  const [contact, setContact] = React.useState('');
  const [message, setMessage] = React.useState('');
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [notice, setNotice] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const selected = React.useMemo(
    () => listings.find((listing) => listing.id === selectedId) || listings[0],
    [listings, selectedId]
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

  const submitRequest = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!selected) return;

    setIsSubmitting(true);
    setNotice(null);
    setError(null);
    try {
      const created = await externalEstateListingsService.createRequest(
        selected.id,
        {
          requestType:
            selected.externalListingType === 'Sale' ? 'Sale' : 'Rent',
          applicantName,
          contact,
          message,
        }
      );
      setNotice(`Request ${created.referenceNumber || created.title} submitted.`);
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
              placeholder="Search land, apartment, building"
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
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All</SelectItem>
              <SelectItem value="Sale">Sale</SelectItem>
              <SelectItem value="Rent">Rent</SelectItem>
            </SelectContent>
          </Select>
          <Button onClick={() => void loadListings()}>
            <Search className="mr-2 h-4 w-4" />
            Search
          </Button>
        </CardContent>
      </Card>

      {error ? (
        <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
          {error}
        </div>
      ) : null}
      {notice ? (
        <div className="rounded-md border border-green-200 bg-green-50 p-3 text-sm text-green-700">
          {notice}
        </div>
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
                    <span className="truncate">
                      {listing.location || 'Location not recorded'}
                    </span>
                  </div>
                  <div className="flex items-center justify-between gap-3 text-sm">
                    <span className="font-semibold text-slate-900">
                      {formatMoney(
                        listing.externalListingPrice,
                        listing.externalListingCurrency
                      )}
                    </span>
                    <span className="text-slate-500">{areaLabel(listing)}</span>
                  </div>
                </div>
              </button>
            );
          })}
        </div>

        <Card className="h-fit">
          <CardContent className="space-y-5 p-5">
            {selected ? (
              <>
                <div className="space-y-3">
                  <div className="flex items-start gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-md border bg-slate-50">
                      {selected.externalListingType === 'Sale' ? (
                        <Building2 className="h-5 w-5 text-blue-700" />
                      ) : (
                        <Home className="h-5 w-5 text-blue-700" />
                      )}
                    </div>
                    <div>
                      <h2 className="font-semibold text-slate-900">
                        {selected.name}
                      </h2>
                      <p className="mt-1 text-sm text-slate-500">
                        {selected.sourceLabel}
                      </p>
                    </div>
                  </div>
                  <div className="grid gap-2 text-sm text-slate-600">
                    <div>{selected.location || 'Location not recorded'}</div>
                    <div>
                      {formatMoney(
                        selected.externalListingPrice,
                        selected.externalListingCurrency
                      )}
                    </div>
                    <div>{areaLabel(selected)}</div>
                  </div>
                  {selected.externalListingNotes ? (
                    <p className="text-sm leading-6 text-slate-600">
                      {selected.externalListingNotes}
                    </p>
                  ) : null}
                </div>

                <form className="space-y-4" onSubmit={submitRequest}>
                  <div className="space-y-2">
                    <Label>Name</Label>
                    <Input
                      value={applicantName}
                      onChange={(event) =>
                        setApplicantName(event.target.value)
                      }
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Contact</Label>
                    <Input
                      value={contact}
                      onChange={(event) => setContact(event.target.value)}
                    />
                  </div>
                  <div className="space-y-2">
                    <Label>Message</Label>
                    <Textarea
                      className="min-h-[120px]"
                      value={message}
                      onChange={(event) => setMessage(event.target.value)}
                    />
                  </div>
                  <Button className="w-full" disabled={isSubmitting}>
                    {isSubmitting ? (
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    ) : (
                      <Send className="mr-2 h-4 w-4" />
                    )}
                    Place request
                  </Button>
                </form>
              </>
            ) : (
              <div className="rounded-md border border-dashed p-8 text-center text-sm text-slate-500">
                Select a listing.
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
