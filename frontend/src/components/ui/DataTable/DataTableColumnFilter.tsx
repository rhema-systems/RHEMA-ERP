"use client";

import React, { useState } from 'react';
import { Column } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { FilterIcon, XIcon } from 'lucide-react';

export interface DataTableColumnFilterProps<TData, TValue> {
  column: Column<TData, TValue>;
  title?: string;
  options?: Array<{
    label: string;
    value: string;
    icon?: React.ComponentType<{ className?: string }>;
  }>;
}

export function DataTableColumnFilter<TData, TValue>({
  column,
  title,
  options,
}: DataTableColumnFilterProps<TData, TValue>) {
  const [isOpen, setIsOpen] = useState(false);
  const [filterValue, setFilterValue] = useState<string>(
    (column.getFilterValue() as string) || ''
  );

  const facetedUniqueValues = column.getFacetedUniqueValues();
  const sortedUniqueValues = facetedUniqueValues
    ? Array.from(facetedUniqueValues.keys()).sort()
    : [];

  const selectedValue = column.getFilterValue() as string;
  const hasFilter = !!selectedValue;

  const handleFilterChange = (value: string | undefined) => {
    setFilterValue(value || '');
    column.setFilterValue(value || undefined);
  };

  const handleClearFilter = () => {
    handleFilterChange(undefined);
    setIsOpen(false);
  };

  const handleApplyFilter = () => {
    column.setFilterValue(filterValue || undefined);
    setIsOpen(false);
  };

  // If we have predefined options, use them
  if (options && options.length > 0) {
    return (
      <Popover open={isOpen} onOpenChange={setIsOpen}>
        <PopoverTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            className="h-6 w-6 p-0"
            title={`Filter ${title || column.id}`}
          >
            <FilterIcon className={`h-3 w-3 ${hasFilter ? 'text-primary' : 'text-muted-foreground'}`} />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-56" align="start">
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <h4 className="font-medium leading-none">{title || column.id}</h4>
              {hasFilter && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 w-6 p-0"
                  onClick={handleClearFilter}
                  title="Clear filter"
                >
                  <XIcon className="h-3 w-3" />
                </Button>
              )}
            </div>
            
            <Select value={selectedValue || '__all__'} onValueChange={v => handleFilterChange(v === '__all__' ? undefined : v)}>
              <SelectTrigger>
                <SelectValue placeholder="All" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all__">All</SelectItem>
                {options.map((option) => {
                  const IconComponent = option.icon;
                  return (
                    <SelectItem key={option.value} value={option.value}>
                      <div className="flex items-center space-x-2">
                        {IconComponent && <IconComponent className="h-3 w-3" />}
                        <span>{option.label}</span>
                      </div>
                    </SelectItem>
                  );
                })}
              </SelectContent>
            </Select>

            {hasFilter && (
              <div className="text-xs text-muted-foreground">
                Filtered by: <Badge variant="secondary" className="text-xs">{selectedValue}</Badge>
              </div>
            )}
          </div>
        </PopoverContent>
      </Popover>
    );
  }

  // If we have faceted values (unique values from the column), create a select
  if (sortedUniqueValues.length > 0 && sortedUniqueValues.length <= 50) {
    return (
      <Popover open={isOpen} onOpenChange={setIsOpen}>
        <PopoverTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            className="h-6 w-6 p-0"
            title={`Filter ${title || column.id}`}
          >
            <FilterIcon className={`h-3 w-3 ${hasFilter ? 'text-primary' : 'text-muted-foreground'}`} />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-56" align="start">
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <h4 className="font-medium leading-none">{title || column.id}</h4>
              {hasFilter && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 w-6 p-0"
                  onClick={handleClearFilter}
                  title="Clear filter"
                >
                  <XIcon className="h-3 w-3" />
                </Button>
              )}
            </div>
            
            <Select value={selectedValue || '__all__'} onValueChange={v => handleFilterChange(v === '__all__' ? undefined : v)}>
              <SelectTrigger>
                <SelectValue placeholder="All" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all__">All</SelectItem>
                {sortedUniqueValues.map((value) => (
                  <SelectItem key={String(value)} value={String(value)}>
                    <div className="flex items-center justify-between w-full">
                      <span>{String(value) || '(Empty)'}</span>
                      <Badge variant="secondary" className="ml-2 text-xs">
                        {facetedUniqueValues.get(value)}
                      </Badge>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            {hasFilter && (
              <div className="text-xs text-muted-foreground">
                Filtered by: <Badge variant="secondary" className="text-xs">{selectedValue}</Badge>
              </div>
            )}
          </div>
        </PopoverContent>
      </Popover>
    );
  }

  // Default to text input filter
  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          className="h-6 w-6 p-0"
          title={`Filter ${title || column.id}`}
        >
          <FilterIcon className={`h-3 w-3 ${hasFilter ? 'text-primary' : 'text-muted-foreground'}`} />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-56" align="start">
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="font-medium leading-none">{title || column.id}</h4>
            {hasFilter && (
              <Button
                variant="ghost"
                size="sm"
                className="h-6 w-6 p-0"
                onClick={handleClearFilter}
                title="Clear filter"
              >
                <XIcon className="h-3 w-3" />
              </Button>
            )}
          </div>
          
          <Input
            placeholder={`Filter ${title || column.id}...`}
            value={filterValue}
            onChange={(event) => setFilterValue(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                handleApplyFilter();
              } else if (event.key === 'Escape') {
                setIsOpen(false);
              }
            }}
            className="h-8"
            autoFocus
          />

          <div className="flex items-center space-x-2">
            <Button
              size="sm"
              onClick={handleApplyFilter}
              className="flex-1 h-7"
            >
              Apply
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={handleClearFilter}
              className="flex-1 h-7"
            >
              Clear
            </Button>
          </div>

          {hasFilter && (
            <div className="text-xs text-muted-foreground">
              Current filter: <Badge variant="secondary" className="text-xs">{selectedValue}</Badge>
            </div>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}

export default DataTableColumnFilter;