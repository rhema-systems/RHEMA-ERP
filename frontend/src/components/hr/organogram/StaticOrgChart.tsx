'use client';

import { forwardRef, useMemo } from 'react';
import { SYNTHETIC_ROOT_ID, initialsOf, type OrganogramDimension } from '@/types/hr/organogram';
import { buildHeatScale, levelVar, type HeatMode } from './heat';
import { layoutTree, type LayoutItem, type LayoutOptions } from './layout';
import { OrgLegend, type LevelInfo } from './OrgLegend';

/**
 * The chart as a document: laid out by the same engine as the screen, drawn once at 100% with every
 * card mounted, wrapped in a title block and footer, and always in the light palette. This is what
 * PNG, PDF and print capture — never the live canvas, whose culling and zoom would make the export
 * a picture of whatever happened to be on screen.
 *
 * Rendered offscreen only for the duration of an export. It uses its own class names and hex
 * colours because html2canvas cannot read the app's oklch() theme tokens.
 */

export interface ExportContext {
  tenantName: string | null;
  tenantLogoUrl: string | null;
  dimensionLabel: string;
  structureName: string | null;
  scopeLabel: string;
  filters: string[];
  generatedAt: Date;
  nodeCount: number;
}

export interface StaticOrgChartProps {
  items: LayoutItem[];
  layoutOptions: Partial<LayoutOptions>;
  dimension: OrganogramDimension;
  heat: HeatMode;
  levels: LevelInfo[];
  hasDotted: boolean;
  showLegend: boolean;
  context: ExportContext;
}

const PAD = 40;

export const StaticOrgChart = forwardRef<HTMLDivElement, StaticOrgChartProps>(function StaticOrgChart(
  { items, layoutOptions, dimension, heat, levels, hasDotted, showLegend, context },
  ref,
) {
  const layout = useMemo(() => layoutTree(items, { ...layoutOptions, padding: 24 }), [items, layoutOptions]);
  const nodes = useMemo(() => {
    const out: NonNullable<LayoutItem['node']>[] = [];
    const walk = (list: LayoutItem[]) => {
      for (const i of list) {
        if (i.node) out.push(i.node);
        walk(i.children);
      }
    };
    walk(items);
    return out;
  }, [items]);
  const scale = useMemo(() => buildHeatScale(heat, dimension, nodes), [heat, dimension, nodes]);
  const isPeople = dimension === 'people';
  const width = Math.max(720, layout.width + PAD * 2);

  return (
    <div ref={ref} className="org-static" style={{ width }}>
      <header className="org-static-head">
        {context.tenantLogoUrl && (
          <img src={context.tenantLogoUrl} alt="" className="org-static-logo" crossOrigin="anonymous" />
        )}
        <div className="min-w-0 flex-1">
          <div className="org-static-tenant">{context.tenantName ?? 'Organogram'}</div>
          <h1 className="org-static-title">
            {context.dimensionLabel} organogram
            {context.structureName ? <span className="org-static-muted"> · {context.structureName}</span> : null}
          </h1>
          <div className="org-static-sub">
            {context.scopeLabel} · {context.nodeCount.toLocaleString()} {context.nodeCount === 1 ? 'box' : 'boxes'}
            {context.filters.length ? ` · ${context.filters.join(' · ')}` : ''}
          </div>
        </div>
        <div className="org-static-stamp">
          Generated {context.generatedAt.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })}
        </div>
      </header>

      <div className="org-static-chart" style={{ width: layout.width, height: layout.height }}>
        <svg width={layout.width} height={layout.height} viewBox={`0 0 ${layout.width} ${layout.height}`} aria-hidden>
          {layout.edges.map((e) => (
            <path key={e.id} d={e.d} className={e.dotted ? 'org-edge org-edge-dotted' : 'org-edge'} />
          ))}
        </svg>
        {layout.placed.map((p) => {
          const { item } = p;
          const frame: React.CSSProperties = { left: p.x, top: p.y, width: p.w, height: p.h };
          if (item.kind === 'more' || !item.node) {
            return (
              <div key={item.id} className="org-scard org-scard-more" style={frame}>
                +{item.hidden.toLocaleString()} more not shown
              </div>
            );
          }
          const n = item.node;
          const synthetic = n.id === SYNTHETIC_ROOT_ID;
          const sw = scale?.colorFor(n) ?? null;
          const accent = sw?.color ?? levelVar(item.depth);
          const direct = n.employeeCount;
          const total = n.totalEmployeeCount ?? direct;
          return (
            <div
              key={item.id}
              className={[
                'org-scard',
                `org-scard-${layoutOptions.density ?? 'comfortable'}`,
                n.isVacant && !synthetic ? 'org-scard-vacant' : '',
                !n.isActive ? 'org-scard-inactive' : '',
                synthetic ? 'org-scard-synthetic' : '',
              ].join(' ')}
              style={{ ...frame, ['--org-accent' as string]: accent }}
            >
              <div className="org-scard-row">
                {!synthetic && layoutOptions.density !== 'compact' && (
                  <span className="org-scard-avatar">{isPeople ? initialsOf(n.name) : ''}</span>
                )}
                <div className="min-w-0 flex-1">
                  <div className="org-scard-name">{n.name}</div>
                  {n.title && <div className="org-scard-title">{n.title}</div>}
                  {layoutOptions.density === 'detailed' && n.headName && (
                    <div className="org-scard-title">{dimension === 'teams' ? 'Lead' : 'Head'}: {n.headName}</div>
                  )}
                </div>
              </div>
              {layoutOptions.density !== 'compact' && (
                <div className="org-scard-foot">
                  {n.code && <span className="org-scard-chip org-scard-code">{n.code}</span>}
                  {!n.isActive ? (
                    <span className="org-scard-chip">Inactive</span>
                  ) : n.isVacant && !synthetic ? (
                    <span className="org-scard-chip org-scard-chip-vacant">{n.badge ?? 'Vacant'}</span>
                  ) : n.badge ? (
                    <span className="org-scard-chip">{n.badge}</span>
                  ) : null}
                  {direct !== null && (
                    <span className="org-scard-count">
                      {total !== null && total > direct ? `${direct} · ${total}` : direct}
                      {n.expectedHeadcount ? ` / ${n.expectedHeadcount}` : ''}
                    </span>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <footer className="org-static-foot">
        {showLegend && <OrgLegend levels={levels} heat={scale} hasDotted={hasDotted} variant="static" />}
        <div className="org-static-footnote">
          {dimension === 'units' && 'Headcount reads direct · total: staff attached to the unit itself, then staff in it and everything beneath it. Leavers are excluded.'}
          {dimension === 'positions' && 'A post reads as vacant when nobody on strength holds it. The figure after the slash is the authorised establishment.'}
          {dimension === 'people' && 'Reporting lines as recorded. Contact details are not included in this export.'}
          {dimension === 'teams' && 'Membership counts live memberships of staff still on strength.'}
          {dimension === 'locations' && 'Sites as recorded under the chosen location structure.'}
          <span className="org-static-muted"> · RHEMA ERP · HR</span>
        </div>
      </footer>
    </div>
  );
});
