'use client';

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import {
    Command,
    CommandEmpty,
    CommandGroup,
    CommandInput,
    CommandItem,
    CommandList,
} from '@/components/ui/command';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { ReportingSegmentOption, SegmentStructure } from '@/types/finance';
import { Check, ChevronsUpDown, Loader2, RotateCcw } from 'lucide-react';

const reportingOptionCache = new Map<string, { items: ReportingSegmentOption[]; hasMore: boolean }>();

interface ReportingDimensionComboboxProps {
    dimension: SegmentStructure;
    value: string;
    onValueChange: (value: string) => void;
    disabled?: boolean;
}

export function ReportingDimensionCombobox({
    dimension,
    value,
    onValueChange,
    disabled = false,
}: ReportingDimensionComboboxProps) {
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');
    const [remoteOptions, setRemoteOptions] = useState<ReportingSegmentOption[]>([]);
    const [selectedRemoteOption, setSelectedRemoteOption] = useState<ReportingSegmentOption | null>(null);
    const [loading, setLoading] = useState(false);
    const [loadError, setLoadError] = useState(false);
    const [hasMore, setHasMore] = useState(false);
    const requestSequence = useRef(0);

    const localOptions = useMemo<ReportingSegmentOption[]>(() =>
        (dimension.lookupValues ?? [])
            .filter((option) => option.isActive)
            .sort((left, right) => left.displayOrder - right.displayOrder || left.segmentValue.localeCompare(right.segmentValue))
            .map((option) => ({
                segmentValue: option.segmentValue,
                description: option.description,
                accountCombinationCount: 0,
            })), [dimension.lookupValues]);

    const usesLocalOptions = dimension.lookupTableRequired || localOptions.length > 0;
    const options = usesLocalOptions ? localOptions : remoteOptions;

    useEffect(() => {
        if (!open || usesLocalOptions) return;

        const normalizedSearch = search.trim();
        if (normalizedSearch.length < 1) {
            setRemoteOptions([]);
            setHasMore(false);
            setLoading(false);
            setLoadError(false);
            return;
        }

        const cacheKey = `${dimension.id}:${normalizedSearch.toLocaleLowerCase()}`;
        const cached = reportingOptionCache.get(cacheKey);
        if (cached) {
            setRemoteOptions(cached.items);
            setHasMore(cached.hasMore);
            setLoadError(false);
            return;
        }

        const sequence = ++requestSequence.current;
        const controller = new AbortController();
        const timer = window.setTimeout(async () => {
            try {
                setLoading(true);
                setLoadError(false);
                const response = await financeDataService.getReportingSegmentOptions(
                    dimension.id,
                    normalizedSearch,
                    50,
                    controller.signal
                );
                if (sequence !== requestSequence.current) return;
                reportingOptionCache.set(cacheKey, response);
                setRemoteOptions(response.items);
                setHasMore(response.hasMore);
            } catch (error) {
                if (controller.signal.aborted || sequence !== requestSequence.current) return;
                console.error(`Could not load ${dimension.segmentName} reporting options:`, error);
                setRemoteOptions([]);
                setHasMore(false);
                setLoadError(true);
            } finally {
                if (sequence === requestSequence.current) setLoading(false);
            }
        }, 250);

        return () => {
            window.clearTimeout(timer);
            controller.abort();
        };
    }, [dimension.id, dimension.segmentName, open, search, usesLocalOptions]);

    const selectedOption = usesLocalOptions
        ? localOptions.find((option) => option.segmentValue === value)
        : selectedRemoteOption?.segmentValue === value
            ? selectedRemoteOption
            : remoteOptions.find((option) => option.segmentValue === value);

    const selectValue = (option: ReportingSegmentOption | null) => {
        if (option && !usesLocalOptions) setSelectedRemoteOption(option);
        onValueChange(option?.segmentValue ?? '');
        setOpen(false);
    };

    const emptyMessage = !usesLocalOptions && search.trim().length < 1
        ? `Type to search valid ${dimension.segmentName.toLocaleLowerCase()} values.`
        : loadError
            ? 'Options could not be loaded. Adjust the search to retry.'
            : `No matching ${dimension.segmentName.toLocaleLowerCase()} values.`;

    return (
        <Popover open={open} onOpenChange={setOpen}>
            <PopoverTrigger asChild>
                <Button
                    id={`report-segment-${dimension.id}`}
                    type="button"
                    variant="outline"
                    role="combobox"
                    aria-expanded={open}
                    aria-label={`Filter by ${dimension.segmentName}`}
                    className="w-full min-w-0 justify-between px-3 font-normal"
                    disabled={disabled || (usesLocalOptions && localOptions.length === 0)}
                >
                    <span className="truncate text-left">
                        {value
                            ? `${value}${selectedOption?.description ? ` - ${selectedOption.description}` : ''}`
                            : `All ${dimension.segmentName}`}
                    </span>
                    <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
                </Button>
            </PopoverTrigger>
            <PopoverContent className="w-[var(--radix-popover-trigger-width)] min-w-72 p-0" align="start">
                <Command shouldFilter={usesLocalOptions}>
                    <CommandInput
                        placeholder={`Search ${dimension.segmentName.toLocaleLowerCase()}...`}
                        value={search}
                        onValueChange={setSearch}
                    />
                    <CommandList>
                        <CommandGroup>
                            <CommandItem value="all" onSelect={() => selectValue(null)}>
                                <RotateCcw className="mr-2 h-4 w-4" />
                                All {dimension.segmentName}
                                <Check className={cn('ml-auto h-4 w-4', value ? 'opacity-0' : 'opacity-100')} />
                            </CommandItem>
                        </CommandGroup>
                        {loading ? (
                            <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
                                <Loader2 className="h-4 w-4 animate-spin" /> Loading valid values...
                            </div>
                        ) : (
                            <>
                                <CommandEmpty>{emptyMessage}</CommandEmpty>
                                <CommandGroup>
                                    {options.map((option) => (
                                        <CommandItem
                                            key={option.segmentValue}
                                            value={`${option.segmentValue} ${option.description}`}
                                            onSelect={() => selectValue(option)}
                                        >
                                            <Check
                                                className={cn(
                                                    'mr-2 h-4 w-4',
                                                    value === option.segmentValue ? 'opacity-100' : 'opacity-0'
                                                )}
                                            />
                                            <span className="min-w-0">
                                                <span className="block truncate font-medium">{option.segmentValue}</span>
                                                {option.description && (
                                                    <span className="block truncate text-xs text-muted-foreground">
                                                        {option.description}
                                                        {option.accountCombinationCount > 1
                                                            ? ` · ${option.accountCombinationCount} account combinations`
                                                            : ''}
                                                    </span>
                                                )}
                                            </span>
                                        </CommandItem>
                                    ))}
                                </CommandGroup>
                                {hasMore && (
                                    <div className="border-t px-3 py-2 text-xs text-muted-foreground">
                                        More matches exist. Refine the search to narrow the list.
                                    </div>
                                )}
                            </>
                        )}
                    </CommandList>
                </Command>
            </PopoverContent>
        </Popover>
    );
}
