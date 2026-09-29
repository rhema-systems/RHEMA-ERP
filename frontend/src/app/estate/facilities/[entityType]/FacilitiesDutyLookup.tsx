'use client';

import React from 'react';
import { Check, ChevronsUpDown, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';

export function FacilitiesDutyLookup<T extends { id: string }>({
  id,
  label,
  selectedLabel,
  disabled = false,
  minimumQueryLength = 2,
  search,
  describe,
  onSelect,
}: {
  id?: string;
  label: string;
  selectedLabel?: string;
  disabled?: boolean;
  minimumQueryLength?: number;
  search: (query: string) => Promise<T[]>;
  describe: (option: T) => { title: string; detail?: string };
  onSelect: (option: T) => void;
}) {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState('');
  const [options, setOptions] = React.useState<T[]>([]);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState(false);

  React.useEffect(() => {
    if (!open || query.trim().length < minimumQueryLength) {
      setOptions([]);
      setLoading(false);
      return;
    }

    let active = true;
    setOptions([]);
    setLoading(true);
    const timeout = window.setTimeout(async () => {
      setError(false);
      try {
        const results = await search(query.trim());
        if (active) setOptions(results);
      } catch {
        if (active) {
          setOptions([]);
          setError(true);
        }
      } finally {
        if (active) setLoading(false);
      }
    }, 250);
    return () => {
      active = false;
      window.clearTimeout(timeout);
    };
  }, [minimumQueryLength, open, query, search]);

  return (
    <Popover open={open} onOpenChange={(nextOpen) => {
      setOpen(nextOpen);
      if (!nextOpen) setQuery('');
    }}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          role="combobox"
          aria-label={label}
          aria-expanded={open}
          disabled={disabled}
          className="h-9 w-full justify-between overflow-hidden font-normal"
        >
          <span className="truncate">{selectedLabel || `Search ${label.toLowerCase()}`}</span>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-[min(24rem,calc(100vw-2rem))] p-2">
        <Input
          autoFocus
          aria-label={`Search ${label.toLowerCase()}`}
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder={`Search ${label.toLowerCase()}...`}
        />
        <div className="mt-2 max-h-64 overflow-y-auto" role="listbox" aria-label={`${label} results`}>
          {loading ? (
            <div className="flex items-center gap-2 px-2 py-3 text-sm text-muted-foreground"><Loader2 className="h-4 w-4 animate-spin" /> Searching...</div>
          ) : error ? (
            <p className="px-2 py-3 text-sm text-destructive">Search failed. Try again.</p>
          ) : query.trim().length < minimumQueryLength ? (
            <p className="px-2 py-3 text-sm text-muted-foreground">Enter at least {minimumQueryLength} characters.</p>
          ) : options.length === 0 ? (
            <p className="px-2 py-3 text-sm text-muted-foreground">No matches found.</p>
          ) : options.map((option) => {
            const item = describe(option);
            return (
              <button
                key={option.id}
                type="button"
                role="option"
                aria-selected={selectedLabel === item.title}
                className="flex w-full items-start gap-2 rounded-sm px-2 py-2 text-left text-sm hover:bg-accent focus:bg-accent focus:outline-none"
                onClick={() => {
                  onSelect(option);
                  setOpen(false);
                  setQuery('');
                }}
              >
                <Check className={`mt-0.5 h-4 w-4 shrink-0 ${selectedLabel === item.title ? 'opacity-100' : 'opacity-0'}`} />
                <span className="min-w-0"><span className="block font-medium">{item.title}</span>{item.detail ? <span className="block text-xs text-muted-foreground">{item.detail}</span> : null}</span>
              </button>
            );
          })}
        </div>
      </PopoverContent>
    </Popover>
  );
}
