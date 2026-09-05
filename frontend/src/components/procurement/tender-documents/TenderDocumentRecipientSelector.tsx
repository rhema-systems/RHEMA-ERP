'use client';

import React, { useEffect, useState } from 'react';
import { Check, Loader2, Search } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  businessPartnerService,
  type BusinessPartnerDto,
} from '@/services/businessPartnerService';

const recipientTypes = ['Supplier', 'Contractor', 'Both'];
const pageSize = 10;

/** Uses the authenticated, tenant-scoped partner directory, not the AVL. */
export function TenderDocumentRecipientSelector({
  value,
  name,
  disabled,
  onSelect,
}: {
  value: string;
  name: string;
  disabled?: boolean;
  onSelect: (partner?: BusinessPartnerDto) => void;
}) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [partners, setPartners] = useState<BusinessPartnerDto[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);

  useEffect(() => {
    if (value) return;
    let cancelled = false;
    setLoading(true);
    setError('');
    const timer = setTimeout(async () => {
      try {
        // Separate type filters keep customers from crowding out supplier results.
        const results = await Promise.all(
          recipientTypes.map((partnerType) =>
            businessPartnerService.getPartners({
              page,
              pageSize,
              search: search.trim() || undefined,
              partnerType,
            })
          )
        );
        if (cancelled) return;
        const next = results
          .flatMap((result) => result.items)
          .filter(
            (partner) =>
              recipientTypes.includes(partner.partnerType) &&
              (partner.isActive === true ||
                (partner.isActive === undefined &&
                  ['Active', 'Approved'].includes(partner.status))) &&
              !partner.isBlacklisted
          );
        setPartners((current) =>
          [
            ...new Map(
              [...(page === 1 ? [] : current), ...next].map((partner) => [
                partner.id,
                partner,
              ])
            ).values(),
          ].sort((left, right) =>
            left.partnerName.localeCompare(right.partnerName)
          )
        );
        setHasMore(
          results.some((result) => page * pageSize < result.totalCount)
        );
      } catch {
        if (!cancelled)
          setError('Saved suppliers could not be loaded. Retry the search.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    }, 250);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [search, page, retry, value]);

  if (value) {
    return (
      <div className="flex items-center justify-between gap-3 rounded-lg border bg-muted/30 px-3 py-2">
        <span className="flex min-w-0 items-center gap-2 text-sm font-medium">
          <Check className="h-4 w-4 shrink-0 text-green-600" />
          <span className="truncate">{name}</span>
        </span>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={disabled}
          onClick={() => {
            setSearch('');
            setPage(1);
            setPartners([]);
            onSelect(undefined);
          }}
        >
          Change supplier
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-3.5 h-4 w-4 text-muted-foreground" />
        <Input
          aria-label="Search saved suppliers"
          placeholder="Search supplier name or code"
          className="pl-9"
          value={search}
          disabled={disabled}
          onChange={(event) => {
            setSearch(event.target.value);
            setPage(1);
            setPartners([]);
            setHasMore(false);
          }}
        />
      </div>
      {error ? (
        <div
          role="alert"
          className="flex items-center justify-between gap-2 text-sm text-destructive"
        >
          {error}
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={disabled}
            onClick={() => setRetry((current) => current + 1)}
          >
            Retry
          </Button>
        </div>
      ) : (
        <div
          className="max-h-40 overflow-y-auto rounded-lg border"
          aria-label="Saved supplier results"
          aria-busy={loading}
        >
          {partners.map((partner) => (
            <button
              key={partner.id}
              type="button"
              disabled={disabled || loading}
              className="flex w-full items-center justify-between gap-3 border-b px-3 py-2 text-left text-sm last:border-b-0 hover:bg-accent focus-visible:bg-accent focus-visible:outline-none disabled:opacity-50"
              onClick={() => onSelect(partner)}
            >
              <span className="font-medium">{partner.partnerName}</span>
              <span className="shrink-0 text-xs text-muted-foreground">
                {partner.partnerCode}
              </span>
            </button>
          ))}
          {loading && (
            <p
              role="status"
              className="flex items-center gap-2 px-3 py-2 text-sm text-muted-foreground"
            >
              <Loader2 className="h-4 w-4 animate-spin" /> Searching suppliers…
            </p>
          )}
          {!loading && partners.length === 0 && (
            <p className="px-3 py-2 text-sm text-muted-foreground">
              No matching active suppliers.
            </p>
          )}
          {!loading && hasMore && (
            <Button
              type="button"
              variant="ghost"
              className="w-full"
              disabled={disabled}
              onClick={() => setPage((current) => current + 1)}
            >
              Load more suppliers
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
