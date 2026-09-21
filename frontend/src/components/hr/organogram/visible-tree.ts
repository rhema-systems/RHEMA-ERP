import {
  matchesQuery,
  type OrganogramNode,
  type OrganogramTreeNode,
} from '@/types/hr/organogram';
import type { LayoutItem } from './layout';

/**
 * Turns the data forest into the forest the layout engine will place: what is open, what is
 * filtered out, and which wide rows are paged. Pure, so the same call drives the screen and the
 * export and they cannot disagree about what "the current view" is.
 *
 * ⚠ Paging is the guard that keeps TDC's live people dimension (6,105 children under one node)
 * from being expanded into six thousand cards by one click. A search hit is pinned past the page
 * so it is always on screen; nothing else is.
 */

export interface VisibleForestOptions {
  /** The forest to render — the whole data forest, or one subtree when focused. */
  roots: OrganogramTreeNode[];
  /** Data depth of the roots, so depth rules are relative to what is on screen. */
  baseDepth: number;
  /** Levels open below the roots by default. The roots count as level 0. */
  expandDepth: number;
  /** Per-node open/closed decisions the user has made. */
  overrides: Record<string, boolean>;
  /** When set, only these ids render. Must include every kept node's ancestors. */
  keep: Set<string> | null;
  /** Ids that must render even past the page limit (search hits, the selected node). */
  pinned: Set<string> | null;
  hideInactive: boolean;
  /** Deepest relative level to render, or null for no limit. */
  maxDepth: number | null;
  pageSize: number;
  /** Extra pages the user has opened, by parent id: how many children to show. */
  pageShown: Record<string, number>;
}

export interface VisibleForest {
  items: LayoutItem[];
  /** Cards that would render, placeholders excluded. */
  count: number;
}

export const MORE_SUFFIX = '__more';
export const moreIdFor = (parentId: string) => `${parentId}${MORE_SUFFIX}`;
export const parentOfMoreId = (id: string) => id.slice(0, -MORE_SUFFIX.length);

export function buildVisibleForest(opts: VisibleForestOptions): VisibleForest {
  let count = 0;

  const admit = (entry: OrganogramTreeNode, relDepth: number): boolean => {
    if (opts.keep && !opts.keep.has(entry.node.id)) return false;
    if (opts.hideInactive && !entry.node.isActive) return false;
    if (opts.maxDepth !== null && relDepth > opts.maxDepth) return false;
    return true;
  };

  const build = (entry: OrganogramTreeNode, parentId: string | null): LayoutItem => {
    const relDepth = entry.depth - opts.baseDepth;
    const admitted = entry.children.filter((c) => admit(c, relDepth + 1));

    // A kept path opens itself: when a search or filter is active the user has asked to see
    // these nodes, and a collapsed ancestor would hide them. An explicit override still wins.
    const keptBelow = opts.keep !== null && admitted.length > 0;
    const isOpen =
      admitted.length > 0 &&
      (opts.overrides[entry.node.id] ?? (keptBelow || relDepth < opts.expandDepth));

    count += 1;
    const item: LayoutItem = {
      id: entry.node.id,
      kind: 'node',
      node: entry.node,
      hidden: 0,
      parentId,
      children: [],
      depth: relDepth,
      childCount: entry.children.length,
      descendantCount: entry.subtreeSize - 1,
      isOpen,
    };
    if (!isOpen) return item;

    const shown = opts.pageShown[entry.node.id] ?? opts.pageSize;
    const page = admitted.slice(0, shown);
    if (opts.pinned) {
      for (const c of admitted.slice(shown)) {
        if (opts.pinned.has(c.node.id)) page.push(c);
      }
    }
    item.children = page.map((c) => build(c, entry.node.id));
    const hidden = admitted.length - page.length;
    if (hidden > 0) {
      item.children.push({
        id: moreIdFor(entry.node.id),
        kind: 'more',
        node: null,
        hidden,
        parentId: entry.node.id,
        children: [],
        depth: relDepth + 1,
        childCount: 0,
        descendantCount: 0,
        isOpen: false,
      });
    }
    return item;
  };

  const items = opts.roots.filter((r) => admit(r, 0)).map((r) => build(r, null));
  return { items, count };
}

/**
 * The ids on a path to any node the predicate accepts — the node, and every ancestor of it. Used
 * for search ("show matches and how to reach them") and for the vacancy filter.
 */
export function keepPaths(
  roots: OrganogramTreeNode[],
  predicate: (node: OrganogramNode) => boolean,
): { keep: Set<string>; hits: string[] } {
  const keep = new Set<string>();
  const hits: string[] = [];
  const walk = (entry: OrganogramTreeNode): boolean => {
    let kept = false;
    if (predicate(entry.node)) {
      kept = true;
      hits.push(entry.node.id);
    }
    for (const child of entry.children) {
      if (walk(child)) kept = true;
    }
    if (kept) keep.add(entry.node.id);
    return kept;
  };
  for (const root of roots) walk(root);
  return { keep, hits };
}

export function searchPredicate(query: string) {
  return (node: OrganogramNode) => matchesQuery(node, query);
}

/** Every id at and below the entries given — for "expand all below". */
export function idsBelow(entries: OrganogramTreeNode[]): string[] {
  const out: string[] = [];
  const queue = [...entries];
  for (let i = 0; i < queue.length; i++) {
    out.push(queue[i].node.id);
    queue.push(...queue[i].children);
  }
  return out;
}

/** Cards a fully expanded rendering of these entries would need — the export safety number. */
export function fullyExpandedCount(entries: OrganogramTreeNode[]): number {
  return entries.reduce((sum, e) => sum + e.subtreeSize, 0);
}
