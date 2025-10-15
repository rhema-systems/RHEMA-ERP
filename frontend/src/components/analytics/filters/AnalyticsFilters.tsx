'use client';

import React, { useState } from 'react';
import { CalendarDays, Filter, RefreshCw, Settings, ChevronDown } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { DatePickerWithRange } from '@/components/ui/date-range-picker';
import { Checkbox } from '@/components/ui/checkbox';
import { Separator } from '@/components/ui/separator';
import { Badge } from '@/components/ui/badge';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import { DateRange } from 'react-day-picker';

// #region Filter Types

export interface DateRangeFilter {
  startDate: string;
  endDate: string;
}

export interface AssetFilter {
  assetIds: string[];
  assetTypes: string[];
  locations: string[];
  departments: string[];
}

export interface AnalyticsFilters {
  dateRange: DateRangeFilter;
  assets: AssetFilter;
  analysisType: string;
  granularity: 'hourly' | 'daily' | 'weekly' | 'monthly' | 'quarterly';
  includeDowntime: boolean;
  includeMaintenanceEvents: boolean;
  benchmarkComparison: boolean;
}

export interface FilterChangeEvent<T = any> {
  type: string;
  value: T;
}

// #endregion

// #region Date Range Filter Component

interface DateRangeFilterProps {
  value: DateRange | undefined;
  onChange: (dateRange: DateRange | undefined) => void;
  className?: string;
  presets?: Array<{
    label: string;
    value: { startDate: Date; endDate: Date };
  }>;
}

export const DateRangeFilterComponent: React.FC<DateRangeFilterProps> = ({
  value,
  onChange,
  className,
  presets = [
    {
      label: 'Last 7 days',
      value: {
        startDate: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000),
        endDate: new Date(),
      },
    },
    {
      label: 'Last 30 days',
      value: {
        startDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000),
        endDate: new Date(),
      },
    },
    {
      label: 'Last 90 days',
      value: {
        startDate: new Date(Date.now() - 90 * 24 * 60 * 60 * 1000),
        endDate: new Date(),
      },
    },
    {
      label: 'Last year',
      value: {
        startDate: new Date(Date.now() - 365 * 24 * 60 * 60 * 1000),
        endDate: new Date(),
      },
    },
  ],
}) => {
  return (
    <div className={cn('space-y-2', className)}>
      <Label className="text-sm font-medium">Date Range</Label>
      <div className="flex gap-2">
        <DatePickerWithRange
          value={value}
          onChange={onChange}
          className="flex-1"
        />
        <Popover>
          <PopoverTrigger asChild>
            <Button variant="outline" size="sm">
              <CalendarDays className="h-4 w-4 mr-2" />
              Presets
              <ChevronDown className="h-4 w-4 ml-2" />
            </Button>
          </PopoverTrigger>
          <PopoverContent className="w-56" align="end">
            <div className="space-y-2">
              {presets.map((preset) => (
                <Button
                  key={preset.label}
                  variant="ghost"
                  size="sm"
                  className="w-full justify-start"
                  onClick={() =>
                    onChange({
                      from: preset.value.startDate,
                      to: preset.value.endDate,
                    })
                  }
                >
                  {preset.label}
                </Button>
              ))}
            </div>
          </PopoverContent>
        </Popover>
      </div>
    </div>
  );
};

// #endregion

// #region Asset Selection Filter

interface AssetSelectionFilterProps {
  selectedAssets: string[];
  availableAssets: Array<{
    id: string;
    name: string;
    type: string;
    location: string;
    department: string;
  }>;
  onChange: (selectedAssets: string[]) => void;
  className?: string;
  multiSelect?: boolean;
}

export const AssetSelectionFilter: React.FC<AssetSelectionFilterProps> = ({
  selectedAssets,
  availableAssets,
  onChange,
  className,
  multiSelect = true,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedTypes, setSelectedTypes] = useState<string[]>([]);
  const [selectedLocations, setSelectedLocations] = useState<string[]>([]);

  const filteredAssets = availableAssets.filter((asset) => {
    const matchesSearch = asset.name.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesType = selectedTypes.length === 0 || selectedTypes.includes(asset.type);
    const matchesLocation = selectedLocations.length === 0 || selectedLocations.includes(asset.location);
    return matchesSearch && matchesType && matchesLocation;
  });

  const assetTypes = Array.from(new Set(availableAssets.map(a => a.type)));
  const assetLocations = Array.from(new Set(availableAssets.map(a => a.location)));

  const handleAssetToggle = (assetId: string) => {
    if (multiSelect) {
      const newSelection = selectedAssets.includes(assetId)
        ? selectedAssets.filter(id => id !== assetId)
        : [...selectedAssets, assetId];
      onChange(newSelection);
    } else {
      onChange([assetId]);
    }
  };

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle className="text-sm">Asset Selection</CardTitle>
        <CardDescription>
          {selectedAssets.length} of {availableAssets.length} assets selected
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Search */}
        <div>
          <Input
            placeholder="Search assets..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="h-9"
          />
        </div>

        {/* Type Filter */}
        <div>
          <Label className="text-xs font-medium text-muted-foreground mb-2 block">
            Filter by Type
          </Label>
          <div className="flex flex-wrap gap-1">
            {assetTypes.map((type) => (
              <Badge
                key={type}
                variant={selectedTypes.includes(type) ? 'default' : 'secondary'}
                className="cursor-pointer text-xs"
                onClick={() => {
                  setSelectedTypes(prev =>
                    prev.includes(type)
                      ? prev.filter(t => t !== type)
                      : [...prev, type]
                  );
                }}
              >
                {type}
              </Badge>
            ))}
          </div>
        </div>

        {/* Location Filter */}
        <div>
          <Label className="text-xs font-medium text-muted-foreground mb-2 block">
            Filter by Location
          </Label>
          <div className="flex flex-wrap gap-1">
            {assetLocations.map((location) => (
              <Badge
                key={location}
                variant={selectedLocations.includes(location) ? 'default' : 'secondary'}
                className="cursor-pointer text-xs"
                onClick={() => {
                  setSelectedLocations(prev =>
                    prev.includes(location)
                      ? prev.filter(l => l !== location)
                      : [...prev, location]
                  );
                }}
              >
                {location}
              </Badge>
            ))}
          </div>
        </div>

        <Separator />

        {/* Asset List */}
        <div className="space-y-2 max-h-60 overflow-y-auto">
          {filteredAssets.map((asset) => (
            <div
              key={asset.id}
              className="flex items-center space-x-2 p-2 rounded-lg hover:bg-muted cursor-pointer"
              onClick={() => handleAssetToggle(asset.id)}
            >
              <Checkbox
                checked={selectedAssets.includes(asset.id)}
                onChange={() => handleAssetToggle(asset.id)}
              />
              <div className="flex-1 min-w-0">
                <div className="text-sm font-medium truncate">{asset.name}</div>
                <div className="text-xs text-muted-foreground">
                  {asset.type} • {asset.location}
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Selection Actions */}
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => onChange(availableAssets.map(a => a.id))}
            className="flex-1"
          >
            Select All
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => onChange([])}
            className="flex-1"
          >
            Clear
          </Button>
        </div>
      </CardContent>
    </Card>
  );
};

// #endregion

// #region Analysis Configuration Filter

interface AnalysisConfigFilterProps {
  analysisType: string;
  granularity: 'hourly' | 'daily' | 'weekly' | 'monthly' | 'quarterly';
  includeDowntime: boolean;
  includeMaintenanceEvents: boolean;
  benchmarkComparison: boolean;
  onAnalysisTypeChange: (value: string) => void;
  onGranularityChange: (value: 'hourly' | 'daily' | 'weekly' | 'monthly' | 'quarterly') => void;
  onIncludeDowntimeChange: (value: boolean) => void;
  onIncludeMaintenanceEventsChange: (value: boolean) => void;
  onBenchmarkComparisonChange: (value: boolean) => void;
  className?: string;
}

export const AnalysisConfigFilter: React.FC<AnalysisConfigFilterProps> = ({
  analysisType,
  granularity,
  includeDowntime,
  includeMaintenanceEvents,
  benchmarkComparison,
  onAnalysisTypeChange,
  onGranularityChange,
  onIncludeDowntimeChange,
  onIncludeMaintenanceEventsChange,
  onBenchmarkComparisonChange,
  className,
}) => {
  const analysisTypes = [
    { value: 'oee', label: 'OEE Analysis' },
    { value: 'reliability', label: 'Reliability Analysis' },
    { value: 'performance', label: 'Performance Analysis' },
    { value: 'cost', label: 'Cost Analysis' },
    { value: 'energy', label: 'Energy Analysis' },
    { value: 'comprehensive', label: 'Comprehensive Analysis' },
  ];

  const granularityOptions = [
    { value: 'hourly', label: 'Hourly' },
    { value: 'daily', label: 'Daily' },
    { value: 'weekly', label: 'Weekly' },
    { value: 'monthly', label: 'Monthly' },
    { value: 'quarterly', label: 'Quarterly' },
  ];

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle className="text-sm flex items-center gap-2">
          <Settings className="h-4 w-4" />
          Analysis Configuration
        </CardTitle>
        <CardDescription>Configure your analysis parameters</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Analysis Type */}
        <div className="space-y-2">
          <Label htmlFor="analysis-type">Analysis Type</Label>
          <Select value={analysisType} onValueChange={onAnalysisTypeChange}>
            <SelectTrigger>
              <SelectValue placeholder="Select analysis type" />
            </SelectTrigger>
            <SelectContent>
              {analysisTypes.map((type) => (
                <SelectItem key={type.value} value={type.value}>
                  {type.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Granularity */}
        <div className="space-y-2">
          <Label htmlFor="granularity">Time Granularity</Label>
          <Select value={granularity} onValueChange={onGranularityChange}>
            <SelectTrigger>
              <SelectValue placeholder="Select granularity" />
            </SelectTrigger>
            <SelectContent>
              {granularityOptions.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <Separator />

        {/* Options */}
        <div className="space-y-3">
          <Label className="text-sm font-medium">Analysis Options</Label>
          
          <div className="flex items-center space-x-2">
            <Checkbox
              id="include-downtime"
              checked={includeDowntime}
              onCheckedChange={onIncludeDowntimeChange}
            />
            <Label htmlFor="include-downtime" className="text-sm">
              Include downtime analysis
            </Label>
          </div>

          <div className="flex items-center space-x-2">
            <Checkbox
              id="include-maintenance"
              checked={includeMaintenanceEvents}
              onCheckedChange={onIncludeMaintenanceEventsChange}
            />
            <Label htmlFor="include-maintenance" className="text-sm">
              Include maintenance events
            </Label>
          </div>

          <div className="flex items-center space-x-2">
            <Checkbox
              id="benchmark-comparison"
              checked={benchmarkComparison}
              onCheckedChange={onBenchmarkComparisonChange}
            />
            <Label htmlFor="benchmark-comparison" className="text-sm">
              Enable benchmark comparison
            </Label>
          </div>
        </div>
      </CardContent>
    </Card>
  );
};

// #endregion

// #region Combined Analytics Filter Panel

interface AnalyticsFilterPanelProps {
  filters: AnalyticsFilters;
  availableAssets: Array<{
    id: string;
    name: string;
    type: string;
    location: string;
    department: string;
  }>;
  onFiltersChange: (filters: Partial<AnalyticsFilters>) => void;
  onApplyFilters: () => void;
  onResetFilters: () => void;
  loading?: boolean;
  className?: string;
  collapsible?: boolean;
}

export const AnalyticsFilterPanel: React.FC<AnalyticsFilterPanelProps> = ({
  filters,
  availableAssets,
  onFiltersChange,
  onApplyFilters,
  onResetFilters,
  loading = false,
  className,
  collapsible = true,
}) => {
  const [isCollapsed, setIsCollapsed] = useState(false);

  const handleDateRangeChange = (dateRange: DateRange | undefined) => {
    if (dateRange?.from && dateRange?.to) {
      onFiltersChange({
        dateRange: {
          startDate: dateRange.from.toISOString(),
          endDate: dateRange.to.toISOString(),
        },
      });
    }
  };

  const handleAssetSelectionChange = (selectedAssets: string[]) => {
    onFiltersChange({
      assets: {
        ...filters.assets,
        assetIds: selectedAssets,
      },
    });
  };

  const currentDateRange: DateRange | undefined = filters.dateRange.startDate && filters.dateRange.endDate
    ? {
        from: new Date(filters.dateRange.startDate),
        to: new Date(filters.dateRange.endDate),
      }
    : undefined;

  if (collapsible && isCollapsed) {
    return (
      <Card className={className}>
        <CardContent className="p-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Filter className="h-4 w-4" />
              <span className="text-sm font-medium">Filters</span>
              <Badge variant="secondary">
                {filters.assets.assetIds.length} assets
              </Badge>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setIsCollapsed(false)}
            >
              <ChevronDown className="h-4 w-4" />
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={className}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Filter className="h-5 w-5" />
              Analytics Filters
            </CardTitle>
            <CardDescription>Configure your analysis parameters</CardDescription>
          </div>
          {collapsible && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setIsCollapsed(true)}
            >
              <ChevronDown className="h-4 w-4 rotate-180" />
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-6">
        {/* Date Range */}
        <DateRangeFilterComponent
          value={currentDateRange}
          onChange={handleDateRangeChange}
        />

        <Separator />

        {/* Analysis Configuration */}
        <AnalysisConfigFilter
          analysisType={filters.analysisType}
          granularity={filters.granularity}
          includeDowntime={filters.includeDowntime}
          includeMaintenanceEvents={filters.includeMaintenanceEvents}
          benchmarkComparison={filters.benchmarkComparison}
          onAnalysisTypeChange={(value) => onFiltersChange({ analysisType: value })}
          onGranularityChange={(value) => onFiltersChange({ granularity: value })}
          onIncludeDowntimeChange={(value) => onFiltersChange({ includeDowntime: value })}
          onIncludeMaintenanceEventsChange={(value) => onFiltersChange({ includeMaintenanceEvents: value })}
          onBenchmarkComparisonChange={(value) => onFiltersChange({ benchmarkComparison: value })}
        />

        <Separator />

        {/* Asset Selection */}
        <AssetSelectionFilter
          selectedAssets={filters.assets.assetIds}
          availableAssets={availableAssets}
          onChange={handleAssetSelectionChange}
        />

        {/* Action Buttons */}
        <div className="flex gap-2 pt-4">
          <Button
            onClick={onApplyFilters}
            disabled={loading}
            className="flex-1"
          >
            {loading && <RefreshCw className="mr-2 h-4 w-4 animate-spin" />}
            Apply Filters
          </Button>
          <Button
            variant="outline"
            onClick={onResetFilters}
            disabled={loading}
          >
            Reset
          </Button>
        </div>
      </CardContent>
    </Card>
  );
};

// #endregion

// #region Quick Filter Bar

interface QuickFilterBarProps {
  activeFilters: {
    dateRange?: string;
    assets?: number;
    analysisType?: string;
  };
  onClearFilter: (filterType: string) => void;
  onClearAll: () => void;
  className?: string;
}

export const QuickFilterBar: React.FC<QuickFilterBarProps> = ({
  activeFilters,
  onClearFilter,
  onClearAll,
  className,
}) => {
  const hasActiveFilters = Object.values(activeFilters).some(value => 
    value !== undefined && value !== null && value !== 0
  );

  if (!hasActiveFilters) {
    return null;
  }

  return (
    <div className={cn('flex items-center gap-2 p-3 bg-muted/50 rounded-lg', className)}>
      <span className="text-sm text-muted-foreground">Active filters:</span>
      
      {activeFilters.dateRange && (
        <Badge variant="secondary" className="gap-1">
          {activeFilters.dateRange}
          <button
            onClick={() => onClearFilter('dateRange')}
            className="hover:bg-muted-foreground/20 rounded-full p-0.5"
          >
            ×
          </button>
        </Badge>
      )}

      {activeFilters.assets && activeFilters.assets > 0 && (
        <Badge variant="secondary" className="gap-1">
          {activeFilters.assets} assets
          <button
            onClick={() => onClearFilter('assets')}
            className="hover:bg-muted-foreground/20 rounded-full p-0.5"
          >
            ×
          </button>
        </Badge>
      )}

      {activeFilters.analysisType && (
        <Badge variant="secondary" className="gap-1">
          {activeFilters.analysisType}
          <button
            onClick={() => onClearFilter('analysisType')}
            className="hover:bg-muted-foreground/20 rounded-full p-0.5"
          >
            ×
          </button>
        </Badge>
      )}

      <Button
        variant="ghost"
        size="sm"
        onClick={onClearAll}
        className="ml-auto text-xs"
      >
        Clear all
      </Button>
    </div>
  );
};

// #endregion