import type { OrganogramNode } from '@/types/hr/organogram';

/**
 * The organogram layout engine — pure, synchronous, and unit-tested.
 *
 * Every card is the same size for a given density, which is what lets the layout be computed
 * without measuring the DOM: a subtree's width is a function of its shape alone, so the whole
 * chart is placed in one pass and the renderer only has to draw what the engine says. That is also
 * what makes export trustworthy — the offscreen copy is laid out by the same numbers as the screen.
 *
 * Two layout devices matter on real data:
 *   - **Stacked leaves.** A parent with many childless children (a department of thirty clerks, or
 *     the six thousand people who hang off TDC's root because no manager is recorded) lays them out
 *     in vertical columns with a spine and stubs — the classic org-chart idiom — instead of a row
 *     six thousand cards wide. Rows of that width are what made the old chart unreadable.
 *   - **Orientation.** Deep, narrow structures (a chain of positions) read better left-to-right.
 *     The engine lays everything out top-down and transposes; nothing else knows the difference.
 */

export type Orientation = 'vertical' | 'horizontal';
export type Density = 'compact' | 'comfortable' | 'detailed';

/** Card dimensions per density. Fixed on purpose — see the file comment. */
export const CARD_SIZES: Record<Density, { w: number; h: number }> = {
  compact: { w: 200, h: 56 },
  comfortable: { w: 240, h: 96 },
  detailed: { w: 280, h: 128 },
};

/** A card the layout has to place. Either a real node or a "Show N more" placeholder. */
export interface LayoutItem {
  id: string;
  kind: 'node' | 'more';
  node: OrganogramNode | null;
  /** For `more`: how many siblings the placeholder stands for. */
  hidden: number;
  /** Id of the layout parent (the focus root's parent is null even if it has one in the data). */
  parentId: string | null;
  children: LayoutItem[];
  /** Depth relative to the layout roots — 0 for a root. */
  depth: number;
  /** How many real children this node has in the data, open or not. */
  childCount: number;
  /** Nodes below this one in the data, this one excluded. */
  descendantCount: number;
  isOpen: boolean;
}

export interface PlacedItem {
  item: LayoutItem;
  x: number;
  y: number;
  w: number;
  h: number;
}

export interface EdgePath {
  id: string;
  from: string;
  to: string;
  d: string;
  dotted: boolean;
  /** Axis-aligned bounding box, for viewport culling. */
  bbox: { x: number; y: number; w: number; h: number };
}

export interface LayoutResult {
  placed: PlacedItem[];
  byId: Map<string, PlacedItem>;
  edges: EdgePath[];
  width: number;
  height: number;
  orientation: Orientation;
}

export interface LayoutOptions {
  orientation: Orientation;
  density: Density;
  /** Lay childless siblings out in columns once there are this many of them. 0 disables. */
  stackLeavesFrom: number;
  /** Rows per stacked column before a new column starts. */
  stackColumnMax: number;
  /** Outer margin around the whole chart. */
  padding: number;
}

export const DEFAULT_LAYOUT_OPTIONS: LayoutOptions = {
  orientation: 'vertical',
  density: 'comfortable',
  stackLeavesFrom: 4,
  stackColumnMax: 8,
  padding: 48,
};

const GAP_X = 28;
const GAP_Y = 64;
const STACK_INDENT = 28;
const STACK_GAP_Y = 12;
const CORNER = 10;

type Point = [number, number];

interface Slot {
  kind: 'branch' | 'stack';
  width: number;
  child?: LayoutItem;
  leaves?: LayoutItem[];
  columns?: number;
}

export function layoutTree(roots: LayoutItem[], options: Partial<LayoutOptions> = {}): LayoutResult {
  const opts = { ...DEFAULT_LAYOUT_OPTIONS, ...options };
  const card = CARD_SIZES[opts.density];
  // The engine always lays out top-down. For the horizontal orientation it runs on a card whose
  // sides are swapped and transposes the result, so a card's depth extent is its real width.
  const horizontal = opts.orientation === 'horizontal';
  const nodeW = horizontal ? card.h : card.w;
  const nodeH = horizontal ? card.w : card.h;
  // Stacking is a vertical idiom: columns of leaves under a parent. Transposed it would turn into
  // rows of leaves beside one, which is exactly the fan-out it exists to avoid.
  const stackFrom = horizontal ? 0 : opts.stackLeavesFrom;

  const slotsOf = new Map<string, Slot[]>();
  const subtreeW = new Map<string, number>();

  const measure = (item: LayoutItem): number => {
    const cached = subtreeW.get(item.id);
    if (cached !== undefined) return cached;

    const visible = item.isOpen ? item.children : [];
    const slots: Slot[] = [];
    if (visible.length) {
      const leaves = stackFrom > 0 ? visible.filter((c) => !c.isOpen || c.children.length === 0) : [];
      const stack = stackFrom > 0 && leaves.length >= stackFrom;
      for (const child of visible) {
        if (stack && leaves.includes(child)) continue;
        slots.push({ kind: 'branch', width: measure(child), child });
      }
      if (stack) {
        const columns = Math.ceil(leaves.length / opts.stackColumnMax);
        const width = columns * (nodeW + STACK_INDENT) + (columns - 1) * GAP_X;
        slots.push({ kind: 'stack', width, leaves, columns });
      }
    }
    slotsOf.set(item.id, slots);
    const childrenW = slots.reduce((sum, s) => sum + s.width, 0) + GAP_X * Math.max(0, slots.length - 1);
    const width = Math.max(nodeW, childrenW);
    subtreeW.set(item.id, width);
    return width;
  };

  const placed: PlacedItem[] = [];
  const byId = new Map<string, PlacedItem>();
  const polylines: { id: string; from: string; to: string; points: Point[]; dotted: boolean }[] = [];
  let maxY = 0;

  const put = (item: LayoutItem, x: number, y: number) => {
    const p: PlacedItem = { item, x, y, w: nodeW, h: nodeH };
    placed.push(p);
    byId.set(item.id, p);
    if (y + nodeH > maxY) maxY = y + nodeH;
    return p;
  };

  const place = (item: LayoutItem, x0: number, y: number) => {
    const width = measure(item);
    const self = put(item, x0 + (width - nodeW) / 2, y);
    const slots = slotsOf.get(item.id) ?? [];
    if (!slots.length) return;

    const childrenW = slots.reduce((sum, s) => sum + s.width, 0) + GAP_X * (slots.length - 1);
    let cx = x0 + (width - childrenW) / 2;
    const cy = y + nodeH + GAP_Y;
    const ruleY = y + nodeH + GAP_Y / 2;
    const px = self.x + nodeW / 2;
    const pBottom = self.y + nodeH;

    for (const slot of slots) {
      if (slot.kind === 'branch' && slot.child) {
        place(slot.child, cx, cy);
        const c = byId.get(slot.child.id);
        if (c) {
          const ccx = c.x + nodeW / 2;
          polylines.push({
            id: `${item.id}->${slot.child.id}`,
            from: item.id,
            to: slot.child.id,
            points: [[px, pBottom], [px, ruleY], [ccx, ruleY], [ccx, c.y]],
            dotted: slot.child.node?.lineType === 'dotted',
          });
        }
      } else if (slot.kind === 'stack' && slot.leaves && slot.columns) {
        for (let col = 0; col < slot.columns; col++) {
          const colX = cx + col * (nodeW + STACK_INDENT + GAP_X);
          const spineX = colX + STACK_INDENT / 2;
          const leaves = slot.leaves.slice(col * opts.stackColumnMax, (col + 1) * opts.stackColumnMax);
          let lastMid = ruleY;
          leaves.forEach((leaf, i) => {
            const ly = cy + i * (nodeH + STACK_GAP_Y);
            const p = put(leaf, colX + STACK_INDENT, ly);
            const mid = p.y + nodeH / 2;
            lastMid = mid;
            polylines.push({
              id: `${item.id}->${leaf.id}`,
              from: item.id,
              to: leaf.id,
              points: [[spineX, mid], [p.x, mid]],
              dotted: leaf.node?.lineType === 'dotted',
            });
          });
          // The spine: from the parent, along the rule, down past the last leaf's stub.
          polylines.push({
            id: `${item.id}->spine:${col}`,
            from: item.id,
            to: `spine:${item.id}:${col}`,
            points: [[px, pBottom], [px, ruleY], [spineX, ruleY], [spineX, lastMid]],
            dotted: false,
          });
        }
      }
      cx += slot.width + GAP_X;
    }
  };

  let x = opts.padding;
  for (const root of roots) {
    place(root, x, opts.padding);
    x += measure(root) + GAP_X * 2;
  }
  const totalW = roots.length ? x - GAP_X * 2 + opts.padding : opts.padding * 2;
  const totalH = (roots.length ? maxY : opts.padding) + opts.padding;

  // Transpose for the horizontal orientation. Because the layout ran on the swapped card, every
  // anchor lands where it should: the old bottom-centre of a card is its new right-centre.
  if (horizontal) {
    for (const p of placed) {
      [p.x, p.y] = [p.y, p.x];
      [p.w, p.h] = [p.h, p.w];
    }
    for (const line of polylines) {
      line.points = line.points.map(([lx, ly]) => [ly, lx]);
    }
  }

  const edges: EdgePath[] = polylines.map((line) => ({
    id: line.id,
    from: line.from,
    to: line.to,
    d: roundedPath(line.points, CORNER),
    dotted: line.dotted,
    bbox: bboxOf(line.points),
  }));

  const width = horizontal ? totalH : totalW;
  const height = horizontal ? totalW : totalH;

  return { placed, byId, edges, width, height, orientation: opts.orientation };
}

/** An orthogonal polyline as an SVG path with rounded corners. Collinear points are skipped. */
export function roundedPath(points: Point[], radius: number): string {
  const pts = dedupe(points);
  if (pts.length === 0) return '';
  if (pts.length === 1) return `M${f(pts[0][0])},${f(pts[0][1])}`;
  let d = `M${f(pts[0][0])},${f(pts[0][1])}`;
  for (let i = 1; i < pts.length - 1; i++) {
    const [px, py] = pts[i - 1];
    const [x, y] = pts[i];
    const [nx, ny] = pts[i + 1];
    const inLen = Math.hypot(x - px, y - py);
    const outLen = Math.hypot(nx - x, ny - y);
    const r = Math.min(radius, inLen / 2, outLen / 2);
    if (r <= 0.5) {
      d += ` L${f(x)},${f(y)}`;
      continue;
    }
    const ux = (x - px) / inLen;
    const uy = (y - py) / inLen;
    const vx = (nx - x) / outLen;
    const vy = (ny - y) / outLen;
    d += ` L${f(x - ux * r)},${f(y - uy * r)} Q${f(x)},${f(y)} ${f(x + vx * r)},${f(y + vy * r)}`;
  }
  const [lx, ly] = pts[pts.length - 1];
  d += ` L${f(lx)},${f(ly)}`;
  return d;
}

function dedupe(points: Point[]): Point[] {
  const out: Point[] = [];
  for (const p of points) {
    const last = out[out.length - 1];
    if (last && Math.abs(last[0] - p[0]) < 0.01 && Math.abs(last[1] - p[1]) < 0.01) continue;
    out.push(p);
  }
  // Drop the middle of any three collinear points so corners are only drawn where the line turns.
  for (let i = 1; i < out.length - 1; ) {
    const [ax, ay] = out[i - 1];
    const [bx, by] = out[i];
    const [cx, cy] = out[i + 1];
    const collinear = (ax === bx && bx === cx) || (ay === by && by === cy);
    if (collinear) out.splice(i, 1);
    else i++;
  }
  return out;
}

function bboxOf(points: Point[]) {
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const [x, y] of points) {
    if (x < minX) minX = x;
    if (y < minY) minY = y;
    if (x > maxX) maxX = x;
    if (y > maxY) maxY = y;
  }
  return { x: minX, y: minY, w: maxX - minX, h: maxY - minY };
}

const f = (n: number) => (Math.round(n * 100) / 100).toString();

/** Bounding box of a set of placed items, for fit-to-view and centre-on. */
export function boundsOf(items: PlacedItem[]): { x: number; y: number; w: number; h: number } | null {
  if (!items.length) return null;
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const p of items) {
    if (p.x < minX) minX = p.x;
    if (p.y < minY) minY = p.y;
    if (p.x + p.w > maxX) maxX = p.x + p.w;
    if (p.y + p.h > maxY) maxY = p.y + p.h;
  }
  return { x: minX, y: minY, w: maxX - minX, h: maxY - minY };
}
