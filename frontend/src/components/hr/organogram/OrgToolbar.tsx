'use client';

import {
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronsDownUp,
  ChevronsUpDown,
  Download,
  Home,
  Maximize2,
  Minimize2,
  Minus,
  Plus,
  Scan,
  Search,
  SlidersHorizontal,
  X,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';
import type { OrganogramDimension } from '@/types/hr/organogram';
import type { HeatModeSpec } from './heat';
import type { OrganogramViewState } from './view-state';

export interface FocusCrumb {
  id: string;
  name: string;
}

export interface OrgToolbarProps {
  view: OrganogramViewState;
  onView: (patch: Partial<OrganogramViewState>) => void;
  dimension: OrganogramDimension;
  heatModes: HeatModeSpec[];
  hits: { count: number; index: number } | null;
  onPrevHit: () => void;
  onNextHit: () => void;
  matchesOnly: boolean;
  onMatchesOnly: (v: boolean) => void;
  zoom: number;
  onZoomIn: () => void;
  onZoomOut: () => void;
  onZoomReset: () => void;
  onFit: () => void;
  onExpandAll: () => void;
  onCollapseAll: () => void;
  legendOpen: boolean;
  onLegend: (v: boolean) => void;
  minimapOpen: boolean;
  onMinimap: (v: boolean) => void;
  fullscreen: boolean;
  onFullscreen: () => void;
  onExport: () => void;
  /** Root-first chain to the focused node, or null when the whole chart is shown. */
  focusChain: FocusCrumb[] | null;
  onFocus: (id: string | null) => void;
  visibleCount: number;
  searchRef?: React.RefObject<HTMLInputElement | null>;
  className?: string;
}

const DEPTHS = [1, 2, 3, 4, 5, 99];

export function OrgToolbar(p: OrgToolbarProps) {
  const { view, onView } = p;
  const searching = view.query.trim().length > 0;
  const focusChain = p.focusChain;

  return (
    <TooltipProvider delayDuration={300}>
      <div className={cn('org-toolbar', p.className)}>
        <div className="org-toolbar-row">
          <div className="relative min-w-[14rem] flex-1 md:max-w-sm">
            <Search className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 h-4 w-4 -translate-y-1/2" />
            <Input
              ref={p.searchRef}
              value={view.query}
              onChange={(e) => onView({ query: e.target.value })}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  if (e.shiftKey) p.onPrevHit();
                  else p.onNextHit();
                }
                if (e.key === 'Escape') onView({ query: '' });
              }}
              placeholder="Search by name, title or code…  ( / )"
              className="h-9 pr-28 pl-8"
              aria-label="Search the chart"
            />
            {searching && (
              <div className="absolute top-1/2 right-1 flex -translate-y-1/2 items-center gap-0.5">
                <span className="text-muted-foreground px-1 text-xs tabular-nums">
                  {p.hits && p.hits.count > 0 ? `${p.hits.index + 1} / ${p.hits.count}` : '0 / 0'}
                </span>
                <Button variant="ghost" size="icon" className="h-7 w-7" onClick={p.onPrevHit} disabled={!p.hits?.count} aria-label="Previous match">
                  <ChevronLeft className="h-4 w-4" />
                </Button>
                <Button variant="ghost" size="icon" className="h-7 w-7" onClick={p.onNextHit} disabled={!p.hits?.count} aria-label="Next match">
                  <ChevronRight className="h-4 w-4" />
                </Button>
                <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => onView({ query: '' })} aria-label="Clear search">
                  <X className="h-4 w-4" />
                </Button>
              </div>
            )}
          </div>

          {focusChain && (
            <nav className="org-focus-crumbs" aria-label="Focused branch">
              <Tip label="Show the whole chart">
                <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => p.onFocus(null)} aria-label="Show the whole chart">
                  <Home className="h-3.5 w-3.5" />
                </Button>
              </Tip>
              {focusChain.map((c, i) => (
                <span key={c.id} className="flex items-center gap-1">
                  <ChevronRight className="text-muted-foreground h-3 w-3" />
                  {i === focusChain.length - 1 ? (
                    <span className="max-w-[12rem] truncate text-sm font-medium">{c.name}</span>
                  ) : (
                    <button type="button" className="org-crumb max-w-[10rem] truncate text-sm" onClick={() => p.onFocus(c.id)}>
                      {c.name}
                    </button>
                  )}
                </span>
              ))}
            </nav>
          )}

          <div className="ml-auto flex flex-wrap items-center gap-1.5">
            <DropdownMenu>
              <Tip label="Layout, density, colours and filters">
                <DropdownMenuTrigger asChild>
                  <Button variant="outline" size="sm" className="h-9 gap-1.5">
                    <SlidersHorizontal className="h-4 w-4" />
                    View
                    <ChevronDown className="h-3.5 w-3.5 opacity-60" />
                  </Button>
                </DropdownMenuTrigger>
              </Tip>
              <DropdownMenuContent align="end" className="w-64">
                <DropdownMenuLabel>Orientation</DropdownMenuLabel>
                <DropdownMenuRadioGroup value={view.orientation} onValueChange={(v) => onView({ orientation: v as OrganogramViewState['orientation'] })}>
                  <DropdownMenuRadioItem value="vertical">Top down</DropdownMenuRadioItem>
                  <DropdownMenuRadioItem value="horizontal">Left to right</DropdownMenuRadioItem>
                </DropdownMenuRadioGroup>
                <DropdownMenuSeparator />
                <DropdownMenuLabel>Card size</DropdownMenuLabel>
                <DropdownMenuRadioGroup value={view.density} onValueChange={(v) => onView({ density: v as OrganogramViewState['density'] })}>
                  <DropdownMenuRadioItem value="compact">Compact</DropdownMenuRadioItem>
                  <DropdownMenuRadioItem value="comfortable">Comfortable</DropdownMenuRadioItem>
                  <DropdownMenuRadioItem value="detailed">Detailed</DropdownMenuRadioItem>
                </DropdownMenuRadioGroup>
                <DropdownMenuSeparator />
                <DropdownMenuLabel>Colour by</DropdownMenuLabel>
                <DropdownMenuRadioGroup value={view.heat} onValueChange={(v) => onView({ heat: v as OrganogramViewState['heat'] })}>
                  {p.heatModes.map((m) => (
                    <DropdownMenuRadioItem key={m.key} value={m.key}>
                      <span className="flex flex-col">
                        <span>{m.label}</span>
                        <span className="text-muted-foreground text-xs">{m.description}</span>
                      </span>
                    </DropdownMenuRadioItem>
                  ))}
                </DropdownMenuRadioGroup>
                <DropdownMenuSeparator />
                <DropdownMenuLabel>Show</DropdownMenuLabel>
                <DropdownMenuCheckboxItem checked={view.stackLeaves} onCheckedChange={(v) => onView({ stackLeaves: !!v })} disabled={view.orientation === 'horizontal'}>
                  Stack wide rows into columns
                </DropdownMenuCheckboxItem>
                <DropdownMenuCheckboxItem checked={view.vacantOnly} onCheckedChange={(v) => onView({ vacantOnly: !!v })}>
                  Vacancies only
                </DropdownMenuCheckboxItem>
                <DropdownMenuCheckboxItem checked={view.hideInactive} onCheckedChange={(v) => onView({ hideInactive: !!v })}>
                  Hide inactive
                </DropdownMenuCheckboxItem>
                <DropdownMenuCheckboxItem checked={p.matchesOnly} onCheckedChange={(v) => p.onMatchesOnly(!!v)}>
                  Search shows matches only
                </DropdownMenuCheckboxItem>
                <DropdownMenuSeparator />
                <DropdownMenuCheckboxItem checked={p.legendOpen} onCheckedChange={(v) => p.onLegend(!!v)}>
                  Legend
                </DropdownMenuCheckboxItem>
                <DropdownMenuCheckboxItem checked={p.minimapOpen} onCheckedChange={(v) => p.onMinimap(!!v)}>
                  Overview map
                </DropdownMenuCheckboxItem>
              </DropdownMenuContent>
            </DropdownMenu>

            <div className="flex items-center gap-1">
              <span className="text-muted-foreground hidden text-xs sm:inline">Levels</span>
              <Select value={String(view.expandDepth)} onValueChange={(v) => onView({ expandDepth: Number(v) })}>
                <SelectTrigger className="h-9 w-[4.75rem]" aria-label="Levels open">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DEPTHS.map((n) => (
                    <SelectItem key={n} value={String(n)}>
                      {n === 99 ? 'All' : n}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="org-toolbar-group">
              <Tip label="Expand everything">
                <Button variant="ghost" size="icon" className="h-9 w-9" onClick={p.onExpandAll} aria-label="Expand everything">
                  <ChevronsUpDown className="h-4 w-4" />
                </Button>
              </Tip>
              <Tip label="Collapse to the top level">
                <Button variant="ghost" size="icon" className="h-9 w-9" onClick={p.onCollapseAll} aria-label="Collapse to the top level">
                  <ChevronsDownUp className="h-4 w-4" />
                </Button>
              </Tip>
            </div>

            <div className="org-toolbar-group">
              <Tip label="Zoom out  ( − )">
                <Button variant="ghost" size="icon" className="h-9 w-9" onClick={p.onZoomOut} aria-label="Zoom out">
                  <Minus className="h-4 w-4" />
                </Button>
              </Tip>
              <button type="button" className="w-12 text-center text-xs tabular-nums hover:underline" onClick={p.onZoomReset} title="Reset to 100%">
                {Math.round(p.zoom * 100)}%
              </button>
              <Tip label="Zoom in  ( + )">
                <Button variant="ghost" size="icon" className="h-9 w-9" onClick={p.onZoomIn} aria-label="Zoom in">
                  <Plus className="h-4 w-4" />
                </Button>
              </Tip>
              <Tip label="Fit to screen  ( F )">
                <Button variant="ghost" size="icon" className="h-9 w-9" onClick={p.onFit} aria-label="Fit to screen">
                  <Scan className="h-4 w-4" />
                </Button>
              </Tip>
            </div>

            <Tip label="Print or export">
              <Button variant="outline" size="sm" className="h-9 gap-1.5" onClick={p.onExport}>
                <Download className="h-4 w-4" />
                Export
              </Button>
            </Tip>
            <Tip label={p.fullscreen ? 'Exit full screen  ( Esc )' : 'Full screen'}>
              <Button variant={p.fullscreen ? 'default' : 'outline'} size="icon" className="h-9 w-9" onClick={p.onFullscreen} aria-label={p.fullscreen ? 'Exit full screen' : 'Full screen'}>
                {p.fullscreen ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
              </Button>
            </Tip>
          </div>
        </div>
      </div>
    </TooltipProvider>
  );
}

function Tip({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>{children}</TooltipTrigger>
      <TooltipContent side="bottom">{label}</TooltipContent>
    </Tooltip>
  );
}
