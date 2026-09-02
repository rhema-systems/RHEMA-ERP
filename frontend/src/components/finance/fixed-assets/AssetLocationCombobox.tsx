'use client';

import { useMemo, useState } from 'react';
import { Check, ChevronsUpDown, MapPin } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import type { FixedAssetLocationOption } from '@/types/fixed-assets';

interface AssetLocationComboboxProps {
  options: FixedAssetLocationOption[];
  value?: string;
  onValueChange: (option?: FixedAssetLocationOption) => void;
  placeholder?: string;
  disabled?: boolean;
  allowClear?: boolean;
  id?: string;
}

export function AssetLocationCombobox({
  options,
  value,
  onValueChange,
  placeholder = 'Select an organization location',
  disabled = false,
  allowClear = true,
  id,
}: AssetLocationComboboxProps) {
  const [open, setOpen] = useState(false);
  const selected = useMemo(() => options.find(option => option.id === value), [options, value]);
  const grouped = useMemo(() => {
    const groups = new Map<string, FixedAssetLocationOption[]>();
    for (const option of options) {
      const key = option.levelName || 'Locations';
      groups.set(key, [...(groups.get(key) || []), option]);
    }
    return Array.from(groups.entries());
  }, [options]);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          className="w-full justify-between font-normal"
        >
          <span className="flex min-w-0 items-center gap-2">
            <MapPin className="h-4 w-4 shrink-0 text-muted-foreground" />
            <span className={cn('truncate', !selected && 'text-muted-foreground')}>
              {selected ? `${selected.code} · ${selected.displayName}` : placeholder}
            </span>
          </span>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[420px] p-0" align="start">
        <Command>
          <CommandInput placeholder="Search code, name, hierarchy, or level..." />
          <CommandList>
            <CommandEmpty>No active organization locations found.</CommandEmpty>
            {allowClear && (
              <CommandGroup>
                <CommandItem
                  value="clear no location"
                  onSelect={() => {
                    onValueChange(undefined);
                    setOpen(false);
                  }}
                >
                  <Check className={cn('mr-2 h-4 w-4', !value ? 'opacity-100' : 'opacity-0')} />
                  No location
                </CommandItem>
              </CommandGroup>
            )}
            {grouped.map(([level, levelOptions]) => (
              <CommandGroup key={level} heading={level}>
                {levelOptions.map(option => (
                  <CommandItem
                    key={option.id}
                    value={`${option.code} ${option.name} ${option.displayName} ${option.levelName || ''}`}
                    onSelect={() => {
                      onValueChange(option);
                      setOpen(false);
                    }}
                  >
                    <Check className={cn('mr-2 h-4 w-4', value === option.id ? 'opacity-100' : 'opacity-0')} />
                    <div className="flex min-w-0 flex-col">
                      <span className="font-medium">{option.code} · {option.name}</span>
                      <span className="truncate text-xs text-muted-foreground">{option.displayName}</span>
                    </div>
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
