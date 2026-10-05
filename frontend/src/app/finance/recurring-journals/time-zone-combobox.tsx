'use client';

import { useMemo, useState } from 'react';
import { Check, ChevronsUpDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

interface Props { value: string; onChange: (value: string) => void; }

const fallbackZones = ['Africa/Accra', 'Africa/Abidjan', 'Africa/Lagos', 'Africa/Nairobi', 'Europe/London', 'UTC'];

export function TimeZoneCombobox({ value, onChange }: Props) {
  const [open, setOpen] = useState(false);
  const zones = useMemo(() => {
    const intl = Intl as typeof Intl & { supportedValuesOf?: (key: 'timeZone') => string[] };
    const supported = typeof intl.supportedValuesOf === 'function' ? intl.supportedValuesOf('timeZone') : fallbackZones;
    return Array.from(new Set([...supported, value].filter(Boolean))).sort();
  }, [value]);

  return <Popover open={open} onOpenChange={setOpen}>
    <PopoverTrigger asChild><Button id="timezone" type="button" variant="outline" role="combobox" aria-label="Schedule time zone" aria-expanded={open} className="w-full justify-between font-normal"><span className="truncate">{value || 'Select a time zone'}</span><ChevronsUpDown className="h-4 w-4 opacity-50" /></Button></PopoverTrigger>
    <PopoverContent className="w-[360px] p-0" align="start"><Command><CommandInput placeholder="Search IANA time zones..." /><CommandList><CommandEmpty>No supported time zone found.</CommandEmpty><CommandGroup>{zones.map(zone => <CommandItem key={zone} value={zone} onSelect={() => { onChange(zone); setOpen(false); }}><Check className={cn('mr-2 h-4 w-4', zone === value ? 'opacity-100' : 'opacity-0')} />{zone}</CommandItem>)}</CommandGroup></CommandList></Command></PopoverContent>
  </Popover>;
}
