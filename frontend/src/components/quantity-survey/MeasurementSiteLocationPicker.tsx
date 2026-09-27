'use client';

import React, { useId, useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';

export function measurementSiteOptions(values: Array<string | null | undefined>) {
  const seen = new Set<string>();
  return values.flatMap((raw) => {
    const value = raw?.trim();
    if (!value || value.length > 300 || seen.has(value.toLocaleLowerCase())) return [];
    seen.add(value.toLocaleLowerCase());
    return [value];
  });
}

export function MeasurementSiteLocationPicker({ value, suggestions, required, onChange }: {
  value: string;
  suggestions: string[];
  required: boolean;
  onChange: (value: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const inputId = useId();
  const options = measurementSiteOptions([...suggestions, value]);
  return <div className="space-y-2">
    <Label htmlFor={inputId}>Site location {required ? '*' : '(optional)'}</Label>
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button type="button" variant="outline" role="combobox" aria-label="Choose site location" aria-expanded={open} className="w-full justify-between font-normal">
          <span className="truncate">{value || 'Select project location'}</span>
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
        <Command>
          <CommandInput aria-label="Search site locations" placeholder="Search project locations..." />
          <CommandList>
            <CommandEmpty>No matching site. Enter a custom location below.</CommandEmpty>
            <CommandGroup>{options.map((option) => <CommandItem key={option} value={option} onSelect={() => { onChange(option); setOpen(false); }}>
              <Check className={`mr-2 h-4 w-4 ${value === option ? '' : 'opacity-0'}`} />
              {option}
            </CommandItem>)}</CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
    <Input id={inputId} aria-label="Site location details" value={value} maxLength={300} onChange={(event) => onChange(event.target.value)} placeholder="Add precise details or enter a custom location" />
    <p className="text-xs text-muted-foreground">Choose the project site or a previous measurement location, then refine the details if needed.</p>
  </div>;
}
