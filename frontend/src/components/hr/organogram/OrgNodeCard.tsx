'use client';

import { memo } from 'react';
import Link from 'next/link';
import {
  Ban,
  Briefcase,
  Building2,
  ChevronDown,
  ChevronRight,
  ChevronsDownUp,
  ChevronsUpDown,
  Crosshair,
  Download,
  Ellipsis,
  ExternalLink,
  Link2,
  MapPin,
  Users,
  UserX,
} from 'lucide-react';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';
import {
  SYNTHETIC_ROOT_ID,
  initialsOf,
  type OrganogramDimension,
  type OrganogramNode,
} from '@/types/hr/organogram';
import type { HeatSwatch } from './heat';
import type { Density, Orientation, PlacedItem } from './layout';

/**
 * One box on the chart.
 *
 * Absolutely positioned by the layout engine and sized by the density, so the card never measures
 * itself. Everything inside truncates to the fixed frame: a name that does not fit is available on
 * hover and in the detail panel, which is where reading happens — the card is for recognising.
 */

export interface CardActions {
  onSelect: (id: string) => void;
  onToggle: (id: string, isOpen: boolean) => void;
  onShowMore: (parentId: string) => void;
  onFocus: (id: string) => void;
  onExpandBelow: (id: string) => void;
  onCollapseBelow: (id: string) => void;
  onCopyLink: (id: string) => void;
  onExportBranch: (id: string) => void;
}

export interface OrgNodeCardProps {
  placed: PlacedItem;
  dimension: OrganogramDimension;
  density: Density;
  orientation: Orientation;
  /** The level band colour, a CSS expression. */
  bandColor: string;
  /** When a heat mode is on, the swatch for this node (overrides the band). */
  heat: HeatSwatch | null;
  selected: boolean;
  /** On the path from the selected node up to the root. */
  onPath: boolean;
  /** Matches the current search. */
  hit: boolean;
  /** Not a hit while a search is on and matches-only is off. */
  dimmed: boolean;
  recordHref: string | null;
  actions: CardActions;
}

export const DIMENSION_ICONS: Record<OrganogramDimension, typeof Building2> = {
  units: Building2,
  positions: Briefcase,
  people: Users,
  teams: Users,
  locations: MapPin,
};

function OrgNodeCardInner({
  placed,
  dimension,
  density,
  orientation,
  bandColor,
  heat,
  selected,
  onPath,
  hit,
  dimmed,
  recordHref,
  actions,
}: OrgNodeCardProps) {
  const { item, x, y, w, h } = placed;
  const frame: React.CSSProperties = {
    transform: `translate(${x}px, ${y}px)`,
    width: w,
    height: h,
  };

  if (item.kind === 'more' || !item.node) {
    return (
      <button
        type="button"
        className="org-card org-card-more"
        style={frame}
        onClick={() => item.parentId && actions.onShowMore(item.parentId)}
      >
        <span className="text-sm font-medium">Show {Math.min(item.hidden, 40)} more</span>
        <span className="text-muted-foreground text-xs">{item.hidden.toLocaleString()} not shown</span>
      </button>
    );
  }

  const node = item.node;
  const synthetic = node.id === SYNTHETIC_ROOT_ID;
  const isPeople = dimension === 'people';
  const Icon = DIMENSION_ICONS[dimension];
  const accent = heat?.color ?? bandColor;
  const hasChildren = item.childCount > 0;
  const toggleCount = item.isOpen ? item.childCount : item.descendantCount;

  return (
    <div
      className={cn(
        'org-card group',
        selected && 'org-card-selected',
        onPath && !selected && 'org-card-path',
        hit && 'org-card-hit',
        dimmed && 'org-card-dimmed',
        node.isVacant && !synthetic && 'org-card-vacant',
        !node.isActive && 'org-card-inactive',
        synthetic && 'org-card-synthetic',
        heat && 'org-card-heat',
        `org-card-${density}`,
      )}
      style={{ ...frame, ['--org-accent' as string]: accent }}
      data-node-id={node.id}
    >
      <button
        type="button"
        className="org-card-body"
        onClick={() => actions.onSelect(node.id)}
        onDoubleClick={() => hasChildren && actions.onToggle(node.id, item.isOpen)}
        aria-pressed={selected}
        aria-label={`${node.name}${node.title ? `, ${node.title}` : ''}`}
      >
        <div className="flex min-w-0 items-start gap-2.5">
          {!synthetic && density !== 'compact' && (
            <span className="org-avatar" aria-hidden>
              {isPeople ? initialsOf(node.name) : <Icon className="h-4 w-4" />}
            </span>
          )}
          {!synthetic && density === 'compact' && isPeople && (
            <span className="org-avatar org-avatar-sm" aria-hidden>
              {initialsOf(node.name)}
            </span>
          )}
          <div className="min-w-0 flex-1">
            <div className="org-card-name" title={node.name}>
              {node.name}
            </div>
            {node.title && (
              <div className="org-card-title" title={node.title}>
                {node.title}
              </div>
            )}
            {density === 'detailed' && <ThirdLine node={node} dimension={dimension} />}
          </div>
          {density === 'compact' && <HeadcountChip node={node} compact />}
        </div>

        {density !== 'compact' && (
          <div className="org-card-foot">
            {node.code && <span className="org-chip org-chip-code">{node.code}</span>}
            {!node.isActive ? (
              <span className="org-chip org-chip-status">
                <Ban className="h-3 w-3" /> Inactive
              </span>
            ) : node.isVacant && !synthetic ? (
              <span className="org-chip org-chip-status org-chip-vacant">
                <UserX className="h-3 w-3" /> {node.badge ?? 'Vacant'}
              </span>
            ) : node.badge ? (
              <span className="org-chip org-chip-status">{node.badge}</span>
            ) : null}
            <HeadcountChip node={node} />
          </div>
        )}
      </button>

      {!synthetic && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button type="button" className="org-card-menu" aria-label={`Actions for ${node.name}`}>
              <Ellipsis className="h-3.5 w-3.5" />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuItem onClick={() => actions.onFocus(node.id)}>
              <Crosshair className="mr-2 h-4 w-4" /> Focus on this branch
            </DropdownMenuItem>
            {hasChildren && (
              <>
                <DropdownMenuItem onClick={() => actions.onExpandBelow(node.id)}>
                  <ChevronsUpDown className="mr-2 h-4 w-4" /> Expand everything below
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => actions.onCollapseBelow(node.id)}>
                  <ChevronsDownUp className="mr-2 h-4 w-4" /> Collapse below
                </DropdownMenuItem>
              </>
            )}
            <DropdownMenuSeparator />
            {recordHref && (
              <DropdownMenuItem asChild>
                <Link href={recordHref}>
                  <ExternalLink className="mr-2 h-4 w-4" /> Open record
                </Link>
              </DropdownMenuItem>
            )}
            <DropdownMenuItem onClick={() => actions.onCopyLink(node.id)}>
              <Link2 className="mr-2 h-4 w-4" /> Copy link to this box
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => actions.onExportBranch(node.id)}>
              <Download className="mr-2 h-4 w-4" /> Export this branch…
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      )}

      {hasChildren && (
        <button
          type="button"
          onClick={() => actions.onToggle(node.id, item.isOpen)}
          aria-label={item.isOpen ? `Collapse ${node.name}` : `Expand ${node.name}`}
          aria-expanded={item.isOpen}
          className={cn('org-toggle', orientation === 'horizontal' ? 'org-toggle-right' : 'org-toggle-bottom')}
        >
          {item.isOpen ? (
            <ChevronDown className="h-3 w-3" />
          ) : orientation === 'horizontal' ? (
            <ChevronRight className="h-3 w-3" />
          ) : (
            <ChevronRight className="h-3 w-3" />
          )}
          {toggleCount.toLocaleString()}
        </button>
      )}
    </div>
  );
}

/** The third line on the detailed card: who leads it, who holds it, or where they sit. */
function ThirdLine({ node, dimension }: { node: OrganogramNode; dimension: OrganogramDimension }) {
  let text: string | null = null;
  if (dimension === 'units' || dimension === 'teams') {
    text = node.headName
      ? `${dimension === 'teams' ? 'Lead' : 'Head'}: ${node.headName}`
      : node.positionCount
        ? `${node.positionCount} post${node.positionCount === 1 ? '' : 's'}${node.vacantPositionCount ? `, ${node.vacantPositionCount} vacant` : ''}`
        : null;
  } else if (dimension === 'positions') {
    if (node.holders?.length) {
      const shown = node.holders.slice(0, 2).join(', ');
      const rest = (node.employeeCount ?? node.holders.length) - 2;
      text = rest > 0 ? `${shown} +${rest}` : shown;
    }
  } else if (dimension === 'people') {
    text = node.meta.Unit ?? null;
  } else if (dimension === 'locations') {
    text = node.meta.City ?? null;
  }
  if (!text) return null;
  return (
    <div className="org-card-line3" title={text}>
      {text}
    </div>
  );
}

/**
 * Direct · total, kept apart on purpose: a directorate whose staff sit in its departments has a
 * direct count of zero and a real count in the hundreds. Showing one number was what made the
 * old chart wrong.
 */
function HeadcountChip({ node, compact = false }: { node: OrganogramNode; compact?: boolean }) {
  if (node.employeeCount === null && node.totalEmployeeCount === null) return null;
  const direct = node.employeeCount ?? 0;
  const total = node.totalEmployeeCount ?? direct;
  const title =
    total > direct
      ? `${direct} directly, ${total} including everything below`
      : `${direct} on strength`;
  return (
    <span className={cn('org-chip org-chip-count ml-auto', compact && 'org-chip-count-compact')} title={title}>
      <Users className="h-3 w-3" />
      {total > direct ? (
        <>
          {direct} · <strong>{total}</strong>
        </>
      ) : (
        <strong>{direct}</strong>
      )}
      {node.expectedHeadcount ? <span className="opacity-70">/{node.expectedHeadcount}</span> : null}
    </span>
  );
}

export const OrgNodeCard = memo(OrgNodeCardInner);
