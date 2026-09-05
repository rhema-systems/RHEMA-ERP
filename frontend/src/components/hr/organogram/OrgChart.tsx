'use client';

import { useCallback, useMemo, useState } from 'react';
import { ChevronDown, ChevronRight, Users } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import {
  SYNTHETIC_ROOT_ID,
  initialsOf,
  matchesQuery,
  type OrganogramDimension,
  type OrganogramNode,
  type OrganogramTreeNode,
} from '@/types/hr/organogram';

/**
 * A top-down boxes-and-connectors org chart, rendered from the flat node list the API returns.
 *
 * Built without a charting dependency on purpose. The two candidates in the tree already
 * (`reactflow`, `recharts`) both want a laid-out graph, and neither ships a tree layout — adding
 * `d3-org-chart` (which the DTO's own comment assumes) would be a new dependency for one screen.
 * Connectors here are CSS borders, which costs nothing and themes itself.
 *
 * ⚠ It must survive TDC's real people dimension: 6,287 nodes, of which **6,105 hang off a single
 * root** because only 181 employees have a manager recorded. Rendering that naively locks the tab.
 * Three things stop it:
 *   - subtrees are **collapsed below `defaultExpandDepth`** and mount nothing until opened, so the
 *     DOM starts at a few dozen nodes rather than several thousand;
 *   - any sibling row longer than `SIBLING_PAGE` renders a page at a time behind "Show more", so
 *     one pathological fan-out cannot be opened into six thousand boxes by accident;
 *   - search filters to matches **and their ancestors**, which is the only way to find a person in
 *     a list that wide.
 */

/** Siblings rendered before the row pages. Chosen to stay under a screenful at normal zoom. */
const SIBLING_PAGE = 40;

interface OrgChartProps {
  roots: OrganogramTreeNode[];
  dimension: OrganogramDimension;
  /** Levels open on first render. The root counts as 0. */
  defaultExpandDepth?: number;
  query: string;
  selectedId: string | null;
  onSelect: (node: OrganogramNode) => void;
  zoom: number;
}

export function OrgChart({
  roots,
  dimension,
  defaultExpandDepth = 2,
  query,
  selectedId,
  onSelect,
  zoom,
}: OrgChartProps) {
  // Only the deviations from the depth rule are stored, so changing the rule does not fight a map
  // of every node the user has ever touched.
  const [overrides, setOverrides] = useState<Record<string, boolean>>({});

  const toggle = useCallback((id: string, isOpen: boolean) => {
    setOverrides((prev) => ({ ...prev, [id]: !isOpen }));
  }, []);

  /**
   * Ids on a path to a search hit. Empty when there is no query — and when a query matches nothing
   * the set stays empty too, which the caller distinguishes by counting hits, not by this set.
   */
  const onHitPath = useMemo(() => {
    if (!query.trim()) return null;
    const keep = new Set<string>();
    const walk = (entry: OrganogramTreeNode): boolean => {
      let hit = matchesQuery(entry.node, query);
      for (const child of entry.children) {
        if (walk(child)) hit = true;
      }
      if (hit) keep.add(entry.node.id);
      return hit;
    };
    for (const root of roots) walk(root);
    return keep;
  }, [roots, query]);

  if (!roots.length) return null;

  return (
    <div className="overflow-auto p-6">
      <style>{CONNECTOR_CSS}</style>
      <div
        className="org-chart inline-block origin-top-left"
        style={{ transform: `scale(${zoom})` }}
      >
        <ul className="org-level">
          {roots.map((root) => (
            <Branch
              key={root.node.id}
              entry={root}
              dimension={dimension}
              defaultExpandDepth={defaultExpandDepth}
              overrides={overrides}
              toggle={toggle}
              query={query}
              onHitPath={onHitPath}
              selectedId={selectedId}
              onSelect={onSelect}
            />
          ))}
        </ul>
      </div>
    </div>
  );
}

interface BranchProps {
  entry: OrganogramTreeNode;
  dimension: OrganogramDimension;
  defaultExpandDepth: number;
  overrides: Record<string, boolean>;
  toggle: (id: string, isOpen: boolean) => void;
  query: string;
  onHitPath: Set<string> | null;
  selectedId: string | null;
  onSelect: (node: OrganogramNode) => void;
}

function Branch(props: BranchProps) {
  const { entry, defaultExpandDepth, overrides, toggle, query, onHitPath } = props;
  const { node, children, depth } = entry;
  const [shown, setShown] = useState(SIBLING_PAGE);

  // A search hit is always on screen: the path to it opens regardless of the depth rule and
  // regardless of what the user collapsed before typing.
  const searching = onHitPath !== null;
  const isOpen = searching
    ? onHitPath.has(node.id)
    : (overrides[node.id] ?? depth < defaultExpandDepth);

  const visibleChildren = useMemo(() => {
    const list = searching ? children.filter((c) => onHitPath.has(c.node.id)) : children;
    return list;
  }, [children, searching, onHitPath]);

  const rendered = visibleChildren.slice(0, shown);
  const hidden = visibleChildren.length - rendered.length;
  const hasChildren = visibleChildren.length > 0;

  return (
    <li className="org-node">
      <NodeCard
        node={node}
        dimension={props.dimension}
        depth={depth}
        subtreeSize={entry.subtreeSize}
        childCount={children.length}
        isOpen={isOpen}
        hasChildren={hasChildren}
        onToggle={() => toggle(node.id, isOpen)}
        selected={props.selectedId === node.id}
        onSelect={() => props.onSelect(node)}
        highlighted={!!query.trim() && matchesQuery(node, query)}
      />

      {isOpen && hasChildren && (
        <ul className="org-level">
          {rendered.map((child) => (
            <Branch key={child.node.id} {...props} entry={child} />
          ))}
          {hidden > 0 && (
            <li className="org-node">
              <Button
                variant="outline"
                size="sm"
                className="org-card-shell h-auto py-3"
                onClick={() => setShown((n) => n + SIBLING_PAGE)}
              >
                Show {Math.min(hidden, SIBLING_PAGE)} more
                <span className="text-muted-foreground ml-1">of {hidden}</span>
              </Button>
            </li>
          )}
        </ul>
      )}
    </li>
  );
}

interface NodeCardProps {
  node: OrganogramNode;
  dimension: OrganogramDimension;
  depth: number;
  subtreeSize: number;
  childCount: number;
  isOpen: boolean;
  hasChildren: boolean;
  onToggle: () => void;
  selected: boolean;
  onSelect: () => void;
  highlighted: boolean;
}

function NodeCard({
  node,
  dimension,
  subtreeSize,
  childCount,
  isOpen,
  hasChildren,
  onToggle,
  selected,
  onSelect,
  highlighted,
}: NodeCardProps) {
  const isSynthetic = node.id === SYNTHETIC_ROOT_ID;
  const isPeople = dimension === 'people';

  return (
    <div
      className={cn(
        'org-card-shell bg-card relative rounded-lg border p-3 text-left shadow-sm transition-colors',
        selected && 'ring-primary border-primary ring-2',
        highlighted && !selected && 'border-amber-500 bg-amber-50 dark:bg-amber-950/30',
        !node.isActive && 'opacity-60',
        isSynthetic && 'border-dashed',
      )}
    >
      <button type="button" onClick={onSelect} className="w-full text-left">
        <div className="flex items-start gap-2">
          {isPeople && !isSynthetic && (
            <span className="bg-muted text-muted-foreground flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-semibold">
              {/* imageUrl is null for all 6,286 employees on record — initials are the only avatar. */}
              {initialsOf(node.name)}
            </span>
          )}
          <div className="min-w-0 flex-1">
            <div className="truncate text-sm leading-tight font-semibold" title={node.name}>
              {node.name}
            </div>
            {node.title && (
              <div className="text-muted-foreground truncate text-xs" title={node.title}>
                {node.title}
              </div>
            )}
            {node.headName && (
              <div className="text-muted-foreground mt-0.5 truncate text-xs">
                Head: {node.headName}
              </div>
            )}
          </div>
        </div>

        <div className="mt-2 flex flex-wrap items-center gap-1">
          {node.code && (
            <span className="text-muted-foreground bg-muted rounded px-1.5 py-0.5 font-mono text-[10px]">
              {node.code}
            </span>
          )}
          {node.badge && (
            <Badge
              variant={node.isVacant || !node.isActive ? 'outline' : 'secondary'}
              className="px-1.5 py-0 text-[10px]"
            >
              {node.badge}
            </Badge>
          )}
          <HeadcountChips node={node} />
        </div>
      </button>

      {hasChildren && (
        <button
          type="button"
          onClick={onToggle}
          aria-label={isOpen ? `Collapse ${node.name}` : `Expand ${node.name}`}
          className="bg-background hover:bg-accent absolute -bottom-3 left-1/2 z-10 flex h-6 -translate-x-1/2 items-center gap-0.5 rounded-full border px-1.5 text-[10px] font-medium"
        >
          {isOpen ? <ChevronDown className="h-3 w-3" /> : <ChevronRight className="h-3 w-3" />}
          {/* subtreeSize counts this node too, so what is hidden below is one less. */}
          {isOpen ? childCount : subtreeSize - 1}
        </button>
      )}
    </div>
  );
}

/**
 * The two numbers an org chart is read for, kept apart on purpose.
 *
 * `employeeCount` is what sits at this node; `totalEmployeeCount` is the subtree. They differ
 * wherever staff sit in the children rather than the parent, which on TDC's live structure is nine
 * of 41 units — a Directorate whose direct count is zero and whose real count is in the hundreds.
 * Showing only one of them is what made the number wrong before slice 4.
 */
function HeadcountChips({ node }: { node: OrganogramNode }) {
  if (node.employeeCount === null && node.totalEmployeeCount === null) return null;

  const direct = node.employeeCount ?? 0;
  const total = node.totalEmployeeCount ?? direct;

  return (
    <span className="text-muted-foreground ml-auto inline-flex items-center gap-1 text-[10px]">
      <Users className="h-3 w-3" />
      {total > direct ? (
        <span title={`${direct} directly, ${total} including everything below`}>
          {direct} · <strong className="text-foreground">{total}</strong>
        </span>
      ) : (
        <span title={`${direct} on strength`}>{direct}</span>
      )}
      {node.expectedHeadcount ? <span className="opacity-70">/ {node.expectedHeadcount}</span> : null}
    </span>
  );
}

/**
 * Connector lines.
 *
 * The classic nested-list org chart: each level is a flex row, each node draws a stub up to a
 * horizontal rule shared by its siblings, and first/last children trim that rule to the corner.
 * Kept next to the component rather than in globals.css so the whole chart is one file to delete.
 */
const CONNECTOR_CSS = `
.org-chart ul.org-level {
  display: flex;
  justify-content: center;
  align-items: flex-start;
  padding-top: 1.75rem;
  position: relative;
  margin: 0;
  list-style: none;
}
.org-chart > ul.org-level { padding-top: 0; }

.org-chart li.org-node {
  position: relative;
  padding: 0 0.6rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  list-style: none;
}

/* the stub rising from each node to its siblings' shared rule */
.org-chart ul.org-level > li.org-node::before {
  content: '';
  position: absolute;
  top: -1.75rem;
  left: 50%;
  height: 1.75rem;
  border-left: 1px solid var(--border);
}

/* the shared rule itself, drawn as each node's half of it */
.org-chart ul.org-level > li.org-node::after {
  content: '';
  position: absolute;
  top: -1.75rem;
  left: 0;
  width: 100%;
  border-top: 1px solid var(--border);
}
.org-chart ul.org-level > li.org-node:first-child::after { left: 50%; width: 50%; }
.org-chart ul.org-level > li.org-node:last-child::after { width: 50%; }
.org-chart ul.org-level > li.org-node:only-child::after { display: none; }

/* the stub dropping from a parent into its children's rule */
.org-chart li.org-node > ul.org-level::before {
  content: '';
  position: absolute;
  top: 0;
  left: 50%;
  height: 1.75rem;
  border-left: 1px solid var(--border);
}

.org-chart .org-card-shell { width: 13rem; }
`;
