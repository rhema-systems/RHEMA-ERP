/**
 * Organogram — the uniform flat-node shape every dimension returns.
 *
 * Transcribed from a live `GET api/Organogram/{dimension}` (`SLICE=4 node probe-ui-payloads.mjs`
 * in dev-harness/hr-tierb-tail) and cross-read against `OrganogramDTOs.cs`. Deliberately not
 * inferred from the endpoint names — see [[hr-travel-area-survey]]: a TypeScript type written from
 * an endpoint name is fiction that type-checks.
 *
 * The server returns a FLAT list; the client builds the tree. That is the contract, and it is why
 * `buildTree` below lives beside the type rather than inside a component.
 */

/** The five projections, all five of them now reachable and all five writable. */
export type OrganogramDimension = 'units' | 'positions' | 'people' | 'locations' | 'teams';

/** The id the server gives the synthetic root it prepends when a dimension has several roots. */
export const SYNTHETIC_ROOT_ID = '__root__';

export interface OrganogramNode {
  /** Stable node id. The entity's guid, or `__root__` for the synthetic root. */
  id: string;
  /** Null marks a root. The server has already re-rooted danglers and broken any cycle. */
  parentId: string | null;
  /** Unit name / position title / employee full name / location / team. */
  name: string;
  /** Level name / owning unit / job title / location level / team type. */
  title: string | null;
  code: string | null;
  /** Unit head or team lead. Measured 0/41 on units — TDC has recorded no unit heads. */
  headName: string | null;
  /** Employee photo. Measured 0/6,286 — `PicturePath` is empty for every employee. */
  imageUrl: string | null;
  /** Status chip: "Vacant", "Vacant lead", "Inactive", "3/5". */
  badge: string | null;
  isVacant: boolean;
  isActive: boolean;
  /** Headcount attached directly to this node. On strength only (leavers excluded, slice 4). */
  employeeCount: number | null;
  /** Headcount at and below this node. Null on locations and teams, which carry no headcount. */
  totalEmployeeCount: number | null;
  expectedHeadcount: number | null;
  lineType: 'solid' | 'dotted';
  /** Free key/value detail for the side panel: "Unit", "Email", "Staff level", "City", … */
  meta: Record<string, string>;
}

export interface OrganogramResponse {
  dimension: string;
  nodes: OrganogramNode[];
  nodeCount: number;
  generatedAtUtc: string;
}

export interface OrganogramDimensionSpec {
  key: OrganogramDimension;
  label: string;
  /** What the parent→child edge actually means, said in the tab's own words. */
  description: string;
  /** True where the dimension carries a headcount, i.e. where the count chips render. */
  hasHeadcount: boolean;
  /** True where the view needs a location structure chosen before it can be fetched. */
  needsStructure: boolean;
}

/**
 * The dimensions the screen renders — all five, per the plan's Decision 5.
 *
 * ⚠ `teams` nearly did not make it. Slice 4 measured that `Team` had exactly one consumer in the
 * entire repository — `OrganogramService` itself — with no controller, no service, no seeder and
 * no writer of any kind, so it projected a table nothing could ever fill, and the tab was dropped
 * on the grounds that a view which can never contain anything is a broken promise on a screen.
 * Slice 4b built the register instead, which is the better answer to the same problem: the
 * entities, the tables and the EF configuration were all already there, and only the application
 * layer was missing. The tab is back and the renderer needed no change to take it.
 */
export const ORGANOGRAM_DIMENSIONS: OrganogramDimensionSpec[] = [
  {
    key: 'units',
    label: 'Units',
    description: 'The org backbone — each unit sits under its parent unit.',
    hasHeadcount: true,
    needsStructure: false,
  },
  {
    key: 'positions',
    label: 'Positions',
    description: 'Establishment posts — each post reports to another post.',
    hasHeadcount: true,
    needsStructure: false,
  },
  {
    key: 'people',
    label: 'People',
    description: 'Reporting lines — each person sits under their recorded manager.',
    hasHeadcount: true,
    needsStructure: false,
  },
  {
    key: 'teams',
    label: 'Teams',
    description: 'Working groups — each sub-team sits under its parent team.',
    hasHeadcount: true,
    needsStructure: false,
  },
  {
    key: 'locations',
    label: 'Locations',
    description: 'Geography — each site sits under its parent location.',
    hasHeadcount: false,
    needsStructure: true,
  },
];

// ── tree building ─────────────────────────────────────────────────────────────

export interface OrganogramTreeNode {
  node: OrganogramNode;
  children: OrganogramTreeNode[];
  /** 0 for the root of the returned forest. */
  depth: number;
  /** Nodes at and below this one, this one included. */
  subtreeSize: number;
}

export interface OrganogramHealth {
  /** Nodes excluding the synthetic root. */
  realNodeCount: number;
  /** Real nodes with no real parent — the ones the hierarchy does not actually place. */
  unlinkedCount: number;
  /** Deepest chain, counting the synthetic root as depth 0. */
  maxDepth: number;
  /** The widest single fan-out anywhere in the tree. */
  widestFanOut: number;
  /** True when the server had to invent a root because the dimension has several. */
  hasSyntheticRoot: boolean;
}

/**
 * Turns the flat list into a forest.
 *
 * Defensive on purpose even though the server now breaks cycles and re-roots danglers: this
 * function is the last thing between a bad payload and a hung browser tab, and the cost of the
 * guard is one Set.
 */
export function buildTree(nodes: OrganogramNode[]): OrganogramTreeNode[] {
  const wrapped = new Map<string, OrganogramTreeNode>();
  for (const node of nodes) {
    wrapped.set(node.id, { node, children: [], depth: 0, subtreeSize: 1 });
  }

  const roots: OrganogramTreeNode[] = [];
  for (const entry of wrapped.values()) {
    const parentId = entry.node.parentId;
    const parent = parentId ? wrapped.get(parentId) : undefined;
    if (parent && parent !== entry) parent.children.push(entry);
    else roots.push(entry);
  }

  // Iterative walk. Recursion would be shorter and would also blow the stack on a payload the
  // server has not vetted; `seen` covers the case where the server's own cycle guard was bypassed.
  const seen = new Set<string>();
  const ordered: OrganogramTreeNode[] = [...roots];
  for (const r of roots) r.depth = 0;

  // Walked by index rather than popped: the array grows as it is read, which is the same traversal
  // without a `pop()` that TypeScript can only be told is safe.
  for (let i = 0; i < ordered.length; i++) {
    const entry = ordered[i];
    if (seen.has(entry.node.id)) {
      entry.children = [];
      continue;
    }
    seen.add(entry.node.id);
    for (const child of entry.children) {
      child.depth = entry.depth + 1;
      ordered.push(child);
    }
  }

  for (let i = ordered.length - 1; i >= 0; i--) {
    const entry = ordered[i];
    entry.subtreeSize = 1 + entry.children.reduce((sum, c) => sum + c.subtreeSize, 0);
  }

  return roots;
}

/** Facts about the shape of a dimension, for the screen to state rather than let the user infer. */
export function measureHealth(nodes: OrganogramNode[], roots: OrganogramTreeNode[]): OrganogramHealth {
  const hasSyntheticRoot = nodes.some((n) => n.id === SYNTHETIC_ROOT_ID);
  const real = nodes.filter((n) => n.id !== SYNTHETIC_ROOT_ID);
  const unlinkedCount = real.filter((n) => !n.parentId || n.parentId === SYNTHETIC_ROOT_ID).length;

  let maxDepth = 0;
  let widestFanOut = 0;
  const queue = [...roots];
  for (let i = 0; i < queue.length; i++) {
    const entry = queue[i];
    if (entry.depth > maxDepth) maxDepth = entry.depth;
    if (entry.children.length > widestFanOut) widestFanOut = entry.children.length;
    queue.push(...entry.children);
  }

  return {
    realNodeCount: real.length,
    unlinkedCount,
    maxDepth,
    widestFanOut,
    hasSyntheticRoot,
  };
}

/** Case-insensitive match on the labels a person would actually search by. */
export function matchesQuery(node: OrganogramNode, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return false;
  return (
    node.name.toLowerCase().includes(q) ||
    (node.title ?? '').toLowerCase().includes(q) ||
    (node.code ?? '').toLowerCase().includes(q) ||
    (node.headName ?? '').toLowerCase().includes(q)
  );
}

/** Initials for the avatar, since `imageUrl` is empty for every employee on record. */
export function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (!parts.length) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}
