'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';
import {
  SYNTHETIC_ROOT_ID,
  ancestorsOf,
  indexForest,
  recordHref,
  type OrganogramDimension,
  type OrganogramNode,
  type OrganogramTreeNode,
} from '@/types/hr/organogram';
import { buildHeatScale, heatModesFor, levelVar, HEAT_MODES } from './heat';
import { boundsOf, layoutTree, type PlacedItem } from './layout';
import { exportFileStem, flattenForExport } from './export-data';
import { exportPdf, exportPng, exportTable, printElement } from './export';
import { ORG_CSS } from './org-styles';
import { OrgCanvas, useViewport, type Rect } from './OrgCanvas';
import { OrgDetailPanel } from './OrgDetailPanel';
import { OrgExportDialog, type ExportOptions, type ExportScope, type ExportScopeInfo } from './OrgExportDialog';
import { OrgLegend, type LevelInfo } from './OrgLegend';
import { OrgMinimap } from './OrgMinimap';
import { OrgNodeCard, type CardActions } from './OrgNodeCard';
import { OrgToolbar, type FocusCrumb } from './OrgToolbar';
import { StaticOrgChart, type ExportContext } from './StaticOrgChart';
import { serializeViewState, type OrganogramViewState } from './view-state';
import {
  buildVisibleForest,
  fullyExpandedCount,
  idsBelow,
  keepPaths,
  searchPredicate,
} from './visible-tree';

/**
 * The organogram workspace: toolbar, pan-and-zoom canvas, detail panel, legend, overview map and
 * export, over one flat node list the page has already turned into a forest.
 *
 * ⚠ It must survive TDC's real people dimension — 6,287 nodes, 6,105 of them under one root — and
 * it does so with three guards, all in `visible-tree.ts`: collapse below a depth, page any row wider
 * than `PAGE_SIZE`, and pin only search hits past the page. "Expand everything" refuses above
 * `EXPAND_LIMIT` cards rather than freezing the tab.
 *
 * Full screen is the whole document, not this element: Radix menus and selects portal to <body>,
 * and a fullscreen subtree cannot show them. So "Full screen" pins this component over the page
 * (`.org-immersive`) and asks the browser to fullscreen the document; either half working is fine.
 */

const PAGE_SIZE = 40;
const EXPAND_LIMIT = 2500;
const ZOOM_STEP = 1.25;

export interface OrgChartProps {
  roots: OrganogramTreeNode[];
  nodes: OrganogramNode[];
  dimension: OrganogramDimension;
  dimensionLabel: string;
  structureName: string | null;
  view: OrganogramViewState;
  onView: (patch: Partial<OrganogramViewState>) => void;
  tenant: { name: string | null; logoUrl: string | null };
  className?: string;
}

interface ExportJob {
  items: ReturnType<typeof buildVisibleForest>['items'];
  options: ExportOptions;
  context: ExportContext;
}

export function OrgChart({ roots, nodes, dimension, dimensionLabel, structureName, view, onView, tenant, className }: OrgChartProps) {
  const [overrides, setOverrides] = useState<Record<string, boolean>>({});
  const [pageShown, setPageShown] = useState<Record<string, number>>({});
  const [matchesOnly, setMatchesOnly] = useState(true);
  const [legendOpen, setLegendOpen] = useState(true);
  const [minimapOpen, setMinimapOpen] = useState(true);
  const [immersive, setImmersive] = useState(false);
  const [panelOpen, setPanelOpen] = useState(false);
  const [exportOpen, setExportOpen] = useState(false);
  const [exportScope, setExportScope] = useState<ExportScope | undefined>(undefined);
  const [exportBranchId, setExportBranchId] = useState<string | null>(null);
  const [exportJob, setExportJob] = useState<ExportJob | null>(null);
  const [hitIndex, setHitIndex] = useState(0);

  const containerRef = useRef<HTMLDivElement>(null);
  const rootRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const staticRef = useRef<HTMLDivElement>(null);
  const exportResolve = useRef<((el: HTMLElement) => void) | null>(null);

  const viewport = useViewport(containerRef);
  const { t, setT, size, fit, centerOn, zoomBy, zoomTo, worldRect, isInView } = viewport;

  // ── data shape ────────────────────────────────────────────────────────────────
  const index = useMemo(() => indexForest(roots), [roots]);
  const hasSyntheticRoot = roots.length === 1 && roots[0].node.id === SYNTHETIC_ROOT_ID;
  const focusEntry = view.focusId ? index.byId.get(view.focusId) ?? null : null;
  const layoutRoots = useMemo(() => (focusEntry ? [focusEntry] : roots), [focusEntry, roots]);
  const baseDepth = focusEntry?.depth ?? 0;
  const selectedEntry = view.selectedId ? index.byId.get(view.selectedId) ?? null : null;

  const query = view.query.trim();
  const search = useMemo(() => (query ? keepPaths(layoutRoots, searchPredicate(query)) : null), [layoutRoots, query]);
  const vacant = useMemo(() => (view.vacantOnly ? keepPaths(layoutRoots, (n) => n.isVacant) : null), [layoutRoots, view.vacantOnly]);
  const hits = useMemo(() => {
    if (!search) return [];
    return vacant ? search.hits.filter((id) => vacant.keep.has(id)) : search.hits;
  }, [search, vacant]);
  const hitSet = useMemo(() => new Set(hits), [hits]);

  const keep = useMemo<Set<string> | null>(() => {
    const searchKeep = search && matchesOnly ? search.keep : null;
    if (searchKeep && vacant) return new Set([...searchKeep].filter((id) => vacant.keep.has(id)));
    return searchKeep ?? vacant?.keep ?? null;
  }, [search, matchesOnly, vacant]);

  const pinned = useMemo(() => {
    const s = new Set(hits);
    if (view.selectedId) s.add(view.selectedId);
    return s;
  }, [hits, view.selectedId]);

  const visible = useMemo(
    () =>
      buildVisibleForest({
        roots: layoutRoots,
        baseDepth,
        expandDepth: view.expandDepth,
        overrides,
        keep,
        pinned,
        hideInactive: view.hideInactive,
        maxDepth: null,
        pageSize: PAGE_SIZE,
        pageShown,
      }),
    [layoutRoots, baseDepth, view.expandDepth, overrides, keep, pinned, view.hideInactive, pageShown],
  );

  const layoutOptions = useMemo(
    () => ({ orientation: view.orientation, density: view.density, stackLeavesFrom: view.stackLeaves ? 4 : 0 }),
    [view.orientation, view.density, view.stackLeaves],
  );
  const layout = useMemo(() => layoutTree(visible.items, layoutOptions), [visible.items, layoutOptions]);

  const heatScale = useMemo(() => buildHeatScale(view.heat, dimension, nodes), [view.heat, dimension, nodes]);
  const hasDotted = useMemo(() => nodes.some((n) => n.lineType === 'dotted'), [nodes]);

  /** Data depth → level colour index. The synthetic root is not a level. */
  const bandDepth = useCallback((relDepth: number) => Math.max(0, relDepth + baseDepth - (hasSyntheticRoot ? 1 : 0)), [baseDepth, hasSyntheticRoot]);

  const levels = useMemo<LevelInfo[]>(() => {
    const titles = new Map<number, Set<string | null>>();
    const queue = [...layoutRoots];
    for (let i = 0; i < queue.length; i++) {
      const e = queue[i];
      if (e.node.id !== SYNTHETIC_ROOT_ID) {
        const d = bandDepth(e.depth - baseDepth);
        if (!titles.has(d)) titles.set(d, new Set());
        titles.get(d)?.add(e.node.title);
      }
      queue.push(...e.children);
    }
    return [...titles.entries()]
      .sort((a, b) => a[0] - b[0])
      .slice(0, 8)
      .map(([depth, set]) => ({ depth, label: set.size === 1 ? [...set][0] : null }));
  }, [layoutRoots, baseDepth, bandDepth]);

  const pathIds = useMemo(() => {
    if (!selectedEntry) return null;
    const s = new Set<string>([selectedEntry.node.id]);
    for (const a of ancestorsOf(index, selectedEntry.node.id)) s.add(a.node.id);
    return s;
  }, [selectedEntry, index]);

  const dimmedIds = useMemo(() => {
    if (!search || matchesOnly) return null;
    const s = new Set<string>();
    for (const p of layout.placed) if (p.item.kind === 'node' && !hitSet.has(p.item.id)) s.add(p.item.id);
    return s;
  }, [search, matchesOnly, layout.placed, hitSet]);

  // ── resets ────────────────────────────────────────────────────────────────────
  useEffect(() => {
    setOverrides({});
    setPageShown({});
    setPanelOpen(false);
  }, [dimension]);

  // "Levels open: 3" must mean the same thing on the tenth use as on the first.
  useEffect(() => setOverrides({}), [view.expandDepth, view.focusId]);

  useEffect(() => setHitIndex(0), [query, dimension]);

  // ── fit on first layout and on structural view changes ────────────────────────
  const fitKey = `${dimension}|${view.focusId ?? ''}|${view.orientation}|${view.density}|${view.stackLeaves}|${roots.length}`;
  const lastFit = useRef<string>('');
  useEffect(() => {
    if (!size.w || !size.h || !layout.placed.length) return;
    if (lastFit.current === fitKey) return;
    lastFit.current = fitKey;
    fit(boundsOf(layout.placed));
  }, [fitKey, size.w, size.h, layout.placed, fit]);

  // ── keep the selection and the current hit on screen ──────────────────────────
  const currentHit = hits[hitIndex] ?? null;
  useEffect(() => {
    if (!currentHit) return;
    const p = layout.byId.get(currentHit);
    if (p && !isInView(p, 24)) centerOn(p);
  }, [currentHit, layout.byId]);

  const revealRef = useRef<string | null>(null);
  useEffect(() => {
    const id = revealRef.current;
    if (!id) return;
    const p = layout.byId.get(id);
    if (p) {
      revealRef.current = null;
      if (!isInView(p, 24)) centerOn(p);
    }
  }, [layout.byId, view.selectedId]);

  // ── full screen ───────────────────────────────────────────────────────────────
  const exitFullscreen = useCallback(() => {
    setImmersive(false);
    if (document.fullscreenElement) document.exitFullscreen().catch(() => undefined);
  }, []);
  const toggleFullscreen = useCallback(() => {
    if (immersive) {
      exitFullscreen();
      return;
    }
    setImmersive(true);
    document.documentElement.requestFullscreen?.().catch(() => undefined);
  }, [immersive, exitFullscreen]);

  useEffect(() => {
    if (!immersive) return;
    document.body.style.overflow = 'hidden';
    const onChange = () => {
      if (!document.fullscreenElement) setImmersive(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !document.fullscreenElement) setImmersive(false);
    };
    document.addEventListener('fullscreenchange', onChange);
    window.addEventListener('keydown', onKey);
    return () => {
      document.body.style.overflow = '';
      document.removeEventListener('fullscreenchange', onChange);
      window.removeEventListener('keydown', onKey);
    };
  }, [immersive]);

  // Re-fit after the frame changes size (entering or leaving full screen).
  useEffect(() => {
    lastFit.current = '';
  }, [immersive]);

  // ── actions ───────────────────────────────────────────────────────────────────
  const select = useCallback(
    (id: string | null) => {
      onView({ selectedId: id });
      if (id) {
        // Open the way to it and make sure it is on screen once the layout settles.
        const ancestors = ancestorsOf(index, id);
        if (ancestors.length) {
          setOverrides((prev) => {
            const next = { ...prev };
            for (const a of ancestors) next[a.node.id] = true;
            return next;
          });
        }
        revealRef.current = id;
        setPanelOpen(true);
      }
    },
    [onView, index],
  );

  const toggle = useCallback((id: string, isOpen: boolean) => {
    setOverrides((prev) => ({ ...prev, [id]: !isOpen }));
  }, []);

  const showMore = useCallback((parentId: string) => {
    setPageShown((prev) => ({ ...prev, [parentId]: (prev[parentId] ?? PAGE_SIZE) + PAGE_SIZE }));
  }, []);

  const focus = useCallback(
    (id: string | null) => {
      onView({ focusId: id && id !== SYNTHETIC_ROOT_ID ? id : null });
    },
    [onView],
  );

  const expandBelow = useCallback(
    (id: string) => {
      const entry = index.byId.get(id);
      if (!entry) return;
      if (entry.subtreeSize > EXPAND_LIMIT) {
        toast.warning(`That branch holds ${entry.subtreeSize.toLocaleString()} boxes. Expand a smaller branch, or search for who you need.`);
        return;
      }
      setOverrides((prev) => {
        const next = { ...prev };
        for (const below of idsBelow([entry])) next[below] = true;
        return next;
      });
    },
    [index],
  );

  const collapseBelow = useCallback(
    (id: string) => {
      const entry = index.byId.get(id);
      if (!entry) return;
      setOverrides((prev) => {
        const next = { ...prev };
        for (const below of idsBelow(entry.children)) delete next[below];
        next[id] = false;
        return next;
      });
    },
    [index],
  );

  const expandAll = useCallback(() => {
    const total = fullyExpandedCount(layoutRoots);
    if (total > EXPAND_LIMIT) {
      toast.warning(`${total.toLocaleString()} boxes is too many to open at once. Focus on a branch first, or raise "Levels" a step at a time.`);
      return;
    }
    setOverrides(() => {
      const next: Record<string, boolean> = {};
      for (const id of idsBelow(layoutRoots)) next[id] = true;
      return next;
    });
  }, [layoutRoots]);

  const collapseAll = useCallback(() => {
    setOverrides({});
    setPageShown({});
    onView({ expandDepth: 1 });
  }, [onView]);

  const copyLink = useCallback(
    async (id: string) => {
      const qs = serializeViewState({ ...view, selectedId: id });
      const url = `${window.location.origin}${window.location.pathname}${qs ? `?${qs}` : ''}`;
      try {
        await navigator.clipboard.writeText(url);
        toast.success('Link copied. It opens this view with that box selected.');
      } catch {
        toast.error('The browser refused clipboard access. Copy the address bar instead.');
      }
    },
    [view],
  );

  const openExport = useCallback((scope?: ExportScope, branchId?: string) => {
    setExportScope(scope);
    setExportBranchId(branchId ?? null);
    setExportOpen(true);
  }, []);

  const actions = useMemo<CardActions>(
    () => ({
      onSelect: (id) => select(id),
      onToggle: toggle,
      onShowMore: showMore,
      onFocus: (id) => focus(id),
      onExpandBelow: expandBelow,
      onCollapseBelow: collapseBelow,
      onCopyLink: copyLink,
      onExportBranch: (id) => openExport('branch', id),
    }),
    [select, toggle, showMore, focus, expandBelow, collapseBelow, copyLink, openExport],
  );

  const stepHit = useCallback(
    (dir: 1 | -1) => {
      if (!hits.length) return;
      const next = (hitIndex + dir + hits.length) % hits.length;
      setHitIndex(next);
      select(hits[next]);
    },
    [hits, hitIndex, select],
  );

  // ── keyboard ──────────────────────────────────────────────────────────────────
  const onKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      const tag = (e.target as HTMLElement).tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA') return;
      const entry = selectedEntry;
      const parent = entry ? index.parentOf.get(entry.node.id) ?? null : null;
      const move = (id: string | undefined) => id && select(id);
      switch (e.key) {
        case 'ArrowUp':
          e.preventDefault();
          if (view.orientation === 'vertical') move(parent?.node.id);
          else if (parent) move(parent.children[Math.max(0, parent.children.findIndex((c) => c === entry) - 1)]?.node.id);
          break;
        case 'ArrowDown':
          e.preventDefault();
          if (view.orientation === 'vertical') {
            if (entry?.children.length) {
              setOverrides((prev) => ({ ...prev, [entry.node.id]: true }));
              move(entry.children[0].node.id);
            }
          } else if (parent) move(parent.children[Math.min(parent.children.length - 1, parent.children.findIndex((c) => c === entry) + 1)]?.node.id);
          break;
        case 'ArrowLeft':
          e.preventDefault();
          if (view.orientation === 'vertical') {
            if (parent) move(parent.children[Math.max(0, parent.children.findIndex((c) => c === entry) - 1)]?.node.id);
          } else move(parent?.node.id);
          break;
        case 'ArrowRight':
          e.preventDefault();
          if (view.orientation === 'vertical') {
            if (parent) move(parent.children[Math.min(parent.children.length - 1, parent.children.findIndex((c) => c === entry) + 1)]?.node.id);
          } else if (entry?.children.length) {
            setOverrides((prev) => ({ ...prev, [entry.node.id]: true }));
            move(entry.children[0].node.id);
          }
          break;
        case 'Enter':
        case ' ':
          if (entry?.children.length) {
            e.preventDefault();
            const item = layout.byId.get(entry.node.id)?.item;
            toggle(entry.node.id, item?.isOpen ?? false);
          }
          break;
        case 'Escape':
          if (panelOpen) setPanelOpen(false);
          else if (view.selectedId) onView({ selectedId: null });
          break;
        case 'f':
        case 'F':
          fit(boundsOf(layout.placed));
          break;
        case '+':
        case '=':
          zoomBy(ZOOM_STEP);
          break;
        case '-':
        case '_':
          zoomBy(1 / ZOOM_STEP);
          break;
        case '/':
          e.preventDefault();
          searchRef.current?.focus();
          break;
        case 'Home':
          e.preventDefault();
          move(layoutRoots[0]?.node.id);
          break;
      }
    },
    [selectedEntry, index, select, view.orientation, view.selectedId, layout, toggle, panelOpen, onView, fit, zoomBy, layoutRoots],
  );

  // ── export ────────────────────────────────────────────────────────────────────
  useEffect(() => {
    if (!exportJob || !staticRef.current || !exportResolve.current) return;
    const el = staticRef.current;
    const resolve = exportResolve.current;
    let raf = requestAnimationFrame(() => {
      raf = requestAnimationFrame(() => resolve(el));
    });
    return () => cancelAnimationFrame(raf);
  }, [exportJob]);

  const scopes = useMemo<ExportScopeInfo>(() => {
    const branchEntry = exportBranchId ? index.byId.get(exportBranchId) ?? null : selectedEntry;
    return {
      view: visible.count,
      branch: branchEntry && branchEntry.node.id !== SYNTHETIC_ROOT_ID ? branchEntry.subtreeSize : null,
      whole: fullyExpandedCount(roots) - (hasSyntheticRoot ? 1 : 0),
      branchName: branchEntry && branchEntry.node.id !== SYNTHETIC_ROOT_ID ? branchEntry.node.name : null,
    };
  }, [exportBranchId, index, selectedEntry, visible.count, roots, hasSyntheticRoot]);

  const runExport = useCallback(
    async (options: ExportOptions) => {
      const branchEntry = exportBranchId ? index.byId.get(exportBranchId) ?? null : selectedEntry;
      let items = visible.items;
      let scopeLabel = 'What was on screen';
      let scopeRoots = layoutRoots;
      if (options.scope === 'branch' && branchEntry) {
        scopeRoots = [branchEntry];
        scopeLabel = `${branchEntry.node.name} and everything below`;
      } else if (options.scope === 'whole') {
        scopeRoots = roots;
        scopeLabel = 'Whole chart';
      }
      if (options.scope !== 'view') {
        items = buildVisibleForest({
          roots: scopeRoots,
          baseDepth: scopeRoots[0]?.depth ?? 0,
          expandDepth: 99,
          overrides: {},
          keep: view.vacantOnly ? keepPaths(scopeRoots, (n) => n.isVacant).keep : null,
          pinned: null,
          hideInactive: view.hideInactive,
          maxDepth: null,
          pageSize: Number.MAX_SAFE_INTEGER,
          pageShown: {},
        }).items;
      }

      const filters: string[] = [];
      if (view.vacantOnly) filters.push('Vacancies only');
      if (view.hideInactive) filters.push('Inactive hidden');
      if (query && options.scope === 'view') filters.push(`Search “${query}”`);
      if (view.heat !== 'none') filters.push(`Coloured by ${HEAT_MODES.find((m) => m.key === view.heat)?.label.toLowerCase()}`);

      const countItems = (list: typeof items): number => list.reduce((n, i) => n + (i.kind === 'node' ? 1 : 0) + countItems(i.children), 0);
      const context: ExportContext = {
        tenantName: tenant.name,
        tenantLogoUrl: tenant.logoUrl,
        dimensionLabel,
        structureName,
        scopeLabel,
        filters,
        generatedAt: new Date(),
        nodeCount: countItems(items),
      };
      const stem = exportFileStem(dimension, options.scope === 'branch' && branchEntry ? branchEntry.node.name : options.scope);

      if (options.format === 'xlsx' || options.format === 'csv') {
        const entries = itemsToEntries(items);
        const { rows, columns } = flattenForExport(entries, dimension);
        await exportTable(rows, columns, options.format, stem, context);
        toast.success(`${rows.length.toLocaleString()} rows exported.`);
        return;
      }

      const element = await new Promise<HTMLElement>((resolve) => {
        exportResolve.current = resolve;
        setExportJob({ items, options, context });
      });
      try {
        if ('fonts' in document) await document.fonts.ready;
        if (options.format === 'png') await exportPng(element, stem);
        else if (options.format === 'pdf') await exportPdf(element, stem, options.page, context);
        else printElement(element, `${dimensionLabel} organogram`);
        if (options.format !== 'print') toast.success('Export ready.');
      } finally {
        exportResolve.current = null;
        setExportJob(null);
      }
    },
    [exportBranchId, index, selectedEntry, visible.items, layoutRoots, roots, view.vacantOnly, view.hideInactive, view.heat, query, tenant, dimensionLabel, structureName, dimension],
  );

  // ── render ────────────────────────────────────────────────────────────────────
  const focusChain = useMemo<FocusCrumb[] | null>(() => {
    if (!focusEntry) return null;
    return [...ancestorsOf(index, focusEntry.node.id), focusEntry]
      .filter((e) => e.node.id !== SYNTHETIC_ROOT_ID)
      .map((e) => ({ id: e.node.id, name: e.node.name }));
  }, [focusEntry, index]);

  const renderCard = useCallback(
    (p: PlacedItem) => {
      const node = p.item.node;
      return (
        <OrgNodeCard
          key={p.item.id}
          placed={p}
          dimension={dimension}
          density={view.density}
          orientation={view.orientation}
          bandColor={levelVar(bandDepth(p.item.depth))}
          heat={node && heatScale ? heatScale.colorFor(node) : null}
          selected={view.selectedId === p.item.id}
          onPath={!!pathIds && pathIds.has(p.item.id)}
          hit={hitSet.has(p.item.id)}
          dimmed={!!dimmedIds && dimmedIds.has(p.item.id)}
          recordHref={recordHref(dimension, p.item.id)}
          actions={actions}
        />
      );
    },
    [dimension, view.density, view.orientation, view.selectedId, bandDepth, heatScale, pathIds, hitSet, dimmedIds, actions],
  );

  const navigateTo = useCallback(
    (center: { x: number; y: number }) => {
      const r: Rect = { x: center.x, y: center.y, w: 0, h: 0 };
      centerOn(r);
    },
    [centerOn],
  );

  return (
    <div
      ref={rootRef}
      className={cn('org-root', immersive && 'org-immersive', panelOpen && selectedEntry && 'org-root-panel-open', className)}
    >
      <style>{ORG_CSS}</style>
      <OrgToolbar
        view={view}
        onView={onView}
        dimension={dimension}
        heatModes={heatModesFor(dimension)}
        hits={search ? { count: hits.length, index: Math.min(hitIndex, Math.max(0, hits.length - 1)) } : null}
        onPrevHit={() => stepHit(-1)}
        onNextHit={() => stepHit(1)}
        matchesOnly={matchesOnly}
        onMatchesOnly={setMatchesOnly}
        zoom={t.k}
        onZoomIn={() => zoomBy(ZOOM_STEP)}
        onZoomOut={() => zoomBy(1 / ZOOM_STEP)}
        onZoomReset={() => zoomTo(1)}
        onFit={() => fit(boundsOf(layout.placed))}
        onExpandAll={expandAll}
        onCollapseAll={collapseAll}
        legendOpen={legendOpen}
        onLegend={setLegendOpen}
        minimapOpen={minimapOpen}
        onMinimap={setMinimapOpen}
        fullscreen={immersive}
        onFullscreen={toggleFullscreen}
        onExport={() => openExport()}
        focusChain={focusChain}
        onFocus={focus}
        visibleCount={visible.count}
        searchRef={searchRef}
      />

      <div className="org-stage">
        <OrgCanvas
          layout={layout}
          transform={t}
          onTransform={setT}
          containerRef={containerRef}
          worldRect={worldRect}
          renderCard={renderCard}
          pathIds={pathIds}
          dimmedIds={dimmedIds}
          onBackgroundClick={() => setPanelOpen(false)}
          onKeyDown={onKeyDown}
        />

        {search && hits.length === 0 && (
          <div className="org-hint">Nothing matches “{query}”{view.vacantOnly ? ' among vacancies' : ''}.</div>
        )}
        {!search && visible.count < layoutRoots.reduce((n, r) => n + r.subtreeSize, 0) && layout.placed.length > 0 && (
          <div className="org-hint">
            Showing {visible.count.toLocaleString()} of {fullyExpandedCount(layoutRoots).toLocaleString()} boxes · double-click a box or use its toggle to open it
          </div>
        )}

        {legendOpen && <OrgLegend levels={levels} heat={heatScale} hasDotted={hasDotted} />}
        {minimapOpen && layout.placed.length > 1 && (
          <div className="org-minimap-wrap">
            <OrgMinimap layout={layout} transform={t} worldRect={worldRect} selectedId={view.selectedId} onNavigate={navigateTo} />
          </div>
        )}

        <OrgDetailPanel
          entry={selectedEntry}
          dimension={dimension}
          index={index}
          heat={selectedEntry && heatScale ? heatScale.colorFor(selectedEntry.node) : null}
          recordHref={selectedEntry ? recordHref(dimension, selectedEntry.node.id) : null}
          open={panelOpen}
          onClose={() => setPanelOpen(false)}
          onSelect={(id) => select(id)}
          onFocus={(id) => focus(id)}
          onExportBranch={(id) => openExport('branch', id)}
        />
      </div>

      <OrgExportDialog
        open={exportOpen}
        onOpenChange={(o) => {
          setExportOpen(o);
          if (!o) setExportBranchId(null);
        }}
        dimension={dimension}
        scopes={scopes}
        initialScope={exportScope}
        onRun={runExport}
      />

      {exportJob && (
        <div className="org-export-host" aria-hidden>
          <StaticOrgChart
            ref={staticRef}
            items={exportJob.items}
            layoutOptions={layoutOptions}
            dimension={dimension}
            heat={view.heat}
            levels={levels}
            hasDotted={hasDotted}
            showLegend={exportJob.options.legend}
            context={exportJob.context}
          />
        </div>
      )}
    </div>
  );
}

/** Layout items back into tree entries, placeholders dropped — for exporting "what is on screen". */
function itemsToEntries(items: ReturnType<typeof buildVisibleForest>['items']): OrganogramTreeNode[] {
  const convert = (list: typeof items, depth: number): OrganogramTreeNode[] =>
    list
      .filter((i) => i.kind === 'node' && i.node)
      .map((i) => {
        const children = convert(i.children, depth + 1);
        return {
          node: i.node as OrganogramNode,
          children,
          depth,
          subtreeSize: 1 + children.reduce((s, c) => s + c.subtreeSize, 0),
        };
      });
  return convert(items, 0);
}
