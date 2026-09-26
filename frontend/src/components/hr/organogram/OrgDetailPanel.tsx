'use client';

import Link from 'next/link';
import {
  Ban,
  ChevronRight,
  Crosshair,
  Download,
  ExternalLink,
  Mail,
  Phone,
  UserX,
  X,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import {
  SYNTHETIC_ROOT_ID,
  ancestorsOf,
  initialsOf,
  type ForestIndex,
  type OrganogramDimension,
  type OrganogramTreeNode,
} from '@/types/hr/organogram';
import type { HeatSwatch } from './heat';
import { DIMENSION_ICONS } from './OrgNodeCard';

/**
 * Everything recorded against the selected box, in a panel that lives inside the chart frame so
 * it is still there in full-screen. The chart is for recognising; this is for reading.
 */

export interface OrgDetailPanelProps {
  entry: OrganogramTreeNode | null;
  dimension: OrganogramDimension;
  index: ForestIndex;
  heat: HeatSwatch | null;
  recordHref: string | null;
  open: boolean;
  onClose: () => void;
  onSelect: (id: string) => void;
  onFocus: (id: string) => void;
  onExportBranch: (id: string) => void;
}

const CHILD_LIMIT = 12;

export function OrgDetailPanel({
  entry,
  dimension,
  index,
  heat,
  recordHref,
  open,
  onClose,
  onSelect,
  onFocus,
  onExportBranch,
}: OrgDetailPanelProps) {
  const node = entry?.node ?? null;
  const synthetic = node?.id === SYNTHETIC_ROOT_ID;
  const Icon = DIMENSION_ICONS[dimension];
  const isPeople = dimension === 'people';
  const ancestors = entry ? ancestorsOf(index, entry.node.id).filter((a) => a.node.id !== SYNTHETIC_ROOT_ID) : [];
  const children = entry?.children ?? [];

  return (
    <aside className={cn('org-panel', open && node ? 'org-panel-open' : '')} aria-hidden={!open || !node}>
      {node && entry && (
        <>
          <header className="org-panel-header">
            <div className="flex items-start gap-3">
              {!synthetic && (
                <span className="org-avatar org-avatar-lg" aria-hidden>
                  {isPeople ? initialsOf(node.name) : <Icon className="h-5 w-5" />}
                </span>
              )}
              <div className="min-w-0 flex-1">
                <h3 className="text-base leading-tight font-semibold break-words">{node.name}</h3>
                <p className="text-muted-foreground text-sm">
                  {synthetic
                    ? 'A placeholder the server adds when a dimension has more than one top-level node. It is not a record.'
                    : (node.title ?? 'No type recorded')}
                </p>
                <div className="mt-1.5 flex flex-wrap gap-1">
                  {node.code && (
                    <Badge variant="outline" className="font-mono text-[11px]">
                      {node.code}
                    </Badge>
                  )}
                  {!node.isActive && (
                    <Badge variant="outline" className="gap-1">
                      <Ban className="h-3 w-3" /> Inactive
                    </Badge>
                  )}
                  {node.isVacant && !synthetic && (
                    <Badge variant="outline" className="gap-1 border-[var(--org-status-critical)] text-[var(--org-status-critical)]">
                      <UserX className="h-3 w-3" /> {node.badge ?? 'Vacant'}
                    </Badge>
                  )}
                  {node.badge && !node.isVacant && node.isActive && <Badge variant="secondary">{node.badge}</Badge>}
                  {heat && (
                    <Badge variant="outline" className="gap-1">
                      <span className="org-legend-swatch" style={{ background: heat.color }} />
                      {heat.label}
                    </Badge>
                  )}
                </div>
              </div>
              <Button variant="ghost" size="icon" className="-mr-2 -mt-1 h-8 w-8 shrink-0" onClick={onClose} aria-label="Close details">
                <X className="h-4 w-4" />
              </Button>
            </div>
          </header>

          <div className="org-panel-body">
            <Kpis entry={entry} dimension={dimension} />

            {ancestors.length > 0 && (
              <section>
                <h4>Placement</h4>
                <ol className="org-crumbs">
                  {ancestors.map((a) => (
                    <li key={a.node.id}>
                      <button type="button" onClick={() => onSelect(a.node.id)} className="org-crumb">
                        {a.node.name}
                      </button>
                      <ChevronRight className="text-muted-foreground h-3 w-3" />
                    </li>
                  ))}
                  <li>
                    <span className="org-crumb org-crumb-current">{node.name}</span>
                  </li>
                </ol>
              </section>
            )}

            {children.length > 0 && (
              <section>
                <h4>
                  Directly below <span className="text-muted-foreground font-normal">· {children.length.toLocaleString()}</span>
                </h4>
                <ul className="org-panel-list">
                  {children.slice(0, CHILD_LIMIT).map((c) => (
                    <li key={c.node.id}>
                      <button type="button" className="org-panel-row" onClick={() => onSelect(c.node.id)}>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate font-medium">{c.node.name}</span>
                          {c.node.title && <span className="text-muted-foreground block truncate text-xs">{c.node.title}</span>}
                        </span>
                        {c.subtreeSize > 1 && (
                          <span className="text-muted-foreground shrink-0 text-xs tabular-nums">+{(c.subtreeSize - 1).toLocaleString()}</span>
                        )}
                        {c.node.isVacant && <UserX className="text-muted-foreground h-3.5 w-3.5 shrink-0" />}
                      </button>
                    </li>
                  ))}
                </ul>
                {children.length > CHILD_LIMIT && (
                  <p className="text-muted-foreground mt-1 text-xs">
                    And {(children.length - CHILD_LIMIT).toLocaleString()} more — focus on this branch to see them all.
                  </p>
                )}
              </section>
            )}

            {node.holders && node.holders.length > 0 && (
              <section>
                <h4>
                  Held by <span className="text-muted-foreground font-normal">· {(node.employeeCount ?? node.holders.length).toLocaleString()}</span>
                </h4>
                <ul className="org-panel-list org-panel-list-plain">
                  {node.holders.map((h) => (
                    <li key={h}>{h}</li>
                  ))}
                  {node.holdersTruncated && <li className="text-muted-foreground">and others…</li>}
                </ul>
              </section>
            )}

            <Recorded node={entry.node} dimension={dimension} />
          </div>

          {!synthetic && (
            <footer className="org-panel-footer">
              {recordHref && (
                <Button asChild size="sm">
                  <Link href={recordHref}>
                    <ExternalLink className="h-4 w-4" /> Open record
                  </Link>
                </Button>
              )}
              {children.length > 0 && (
                <Button variant="outline" size="sm" onClick={() => onFocus(node.id)}>
                  <Crosshair className="h-4 w-4" /> Focus
                </Button>
              )}
              <Button variant="outline" size="sm" onClick={() => onExportBranch(node.id)}>
                <Download className="h-4 w-4" /> Export branch
              </Button>
            </footer>
          )}
        </>
      )}
    </aside>
  );
}

function Kpis({ entry, dimension }: { entry: OrganogramTreeNode; dimension: OrganogramDimension }) {
  const n = entry.node;
  const tiles: { label: string; value: string }[] = [];
  const below = entry.subtreeSize - 1;

  if (dimension === 'units') {
    tiles.push({ label: 'Staff in unit', value: fmt(n.employeeCount) });
    tiles.push({ label: 'Staff incl. below', value: fmt(n.totalEmployeeCount) });
    if (n.positionCount !== null) tiles.push({ label: 'Posts', value: fmt(n.positionCount) });
    if (n.vacantPositionCount !== null) tiles.push({ label: 'Vacant posts', value: fmt(n.vacantPositionCount) });
    tiles.push({ label: 'Units below', value: fmt(below) });
  } else if (dimension === 'positions') {
    tiles.push({ label: 'Holders', value: fmt(n.employeeCount) });
    tiles.push({ label: 'Establishment', value: fmt(n.expectedHeadcount) });
    tiles.push({ label: 'Posts reporting in', value: fmt(entry.children.length) });
    tiles.push({ label: 'Staff incl. below', value: fmt(n.totalEmployeeCount) });
  } else if (dimension === 'people') {
    tiles.push({ label: 'Direct reports', value: fmt(entry.children.length) });
    tiles.push({ label: 'Total reports', value: fmt(below) });
  } else if (dimension === 'teams') {
    tiles.push({ label: 'Members', value: fmt(n.employeeCount) });
    if (n.expectedHeadcount) tiles.push({ label: 'Cap', value: fmt(n.expectedHeadcount) });
    tiles.push({ label: 'Sub-teams', value: fmt(entry.children.length) });
  } else {
    tiles.push({ label: 'Locations below', value: fmt(below) });
    tiles.push({ label: 'Directly below', value: fmt(entry.children.length) });
  }

  return (
    <div className="org-kpis">
      {tiles.map((t) => (
        <div key={t.label} className="org-kpi">
          <span className="org-kpi-value">{t.value}</span>
          <span className="org-kpi-label">{t.label}</span>
        </div>
      ))}
    </div>
  );
}

const fmt = (v: number | null | undefined) => (v === null || v === undefined ? '—' : v.toLocaleString());

function Recorded({ node, dimension }: { node: OrganogramTreeNode['node']; dimension: OrganogramDimension }) {
  const rows: [string, React.ReactNode][] = [];
  if (node.headName) rows.push([dimension === 'teams' ? 'Lead' : 'Head', node.headName]);
  for (const [k, v] of Object.entries(node.meta)) {
    if (k === 'Email') {
      rows.push([k, <a key={k} href={`mailto:${v}`} className="inline-flex items-center gap-1 underline-offset-2 hover:underline"><Mail className="h-3 w-3" />{v}</a>]);
    } else if (k === 'Phone') {
      rows.push([k, <a key={k} href={`tel:${v}`} className="inline-flex items-center gap-1 underline-offset-2 hover:underline"><Phone className="h-3 w-3" />{v}</a>]);
    } else {
      rows.push([k, v]);
    }
  }
  if (!rows.length) return null;
  return (
    <section>
      <h4>Recorded</h4>
      <dl className="org-panel-dl">
        {rows.map(([k, v]) => (
          <div key={k}>
            <dt>{k}</dt>
            <dd>{v}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
