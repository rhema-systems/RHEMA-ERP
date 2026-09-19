'use client';

import React, { useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

export interface SearchableOption {
    value: string;
    label: string;
}

interface SearchableOptionPickerProps {
    label: string;
    value: string;
    options: SearchableOption[];
    onChange: (value: string) => void;
    placeholder: string;
    searchPlaceholder: string;
    emptyMessage: string;
    disabled?: boolean;
}

export function SearchableOptionPicker({ label, value, options, onChange, placeholder, searchPlaceholder, emptyMessage, disabled }: SearchableOptionPickerProps) {
    const [open, setOpen] = useState(false);
    const selected = options.find(option => option.value === value);

    return <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
            <Button type="button" variant="outline" role="combobox" aria-label={label} aria-expanded={open} disabled={disabled} className="w-full min-w-0 justify-between font-normal">
                <span className="truncate">{selected?.label ?? (value || placeholder)}</span>
                <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
            </Button>
        </PopoverTrigger>
        <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
            <Command>
                <CommandInput placeholder={searchPlaceholder} aria-label={`Search ${label.toLowerCase()}`} />
                <CommandList>
                    <CommandEmpty>{emptyMessage}</CommandEmpty>
                    <CommandGroup>{options.map(option => <CommandItem key={option.value} value={option.label} onSelect={() => { onChange(option.value); setOpen(false); }}>
                        <Check className={cn('mr-2 h-4 w-4', value === option.value ? 'opacity-100' : 'opacity-0')} />
                        <span className="truncate">{option.label}</span>
                    </CommandItem>)}</CommandGroup>
                </CommandList>
            </Command>
        </PopoverContent>
    </Popover>;
}
